using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Sole game-thread owner of sampling, observation history and publication.
    // A changed intent retires acceptance, not the process or its in-flight
    // request. This bounds the queue without starving a slower native worker.
    internal sealed class NativePredictionSession
    {
        private sealed class Request
        {
            internal NpcIdentity Identity;
            internal long Tick,Wall;
            internal int[] Npcs,Projectiles;
            internal NativeTerrainSnapshot Terrain;
            internal readonly List<NativePredictionAlignment.Frame> History=new List<NativePredictionAlignment.Frame>(61);
            internal readonly NativeTerrainUsage TerrainChanges=new NativeTerrainUsage();
            internal bool Retired;
            internal int Impact;
            internal double CaptureMs;
        }
        internal struct Measurement
        {
            internal long CaptureTick,ArriveTick;
            internal int Bytes,ReplyBytes,Age;
            internal double CaptureMs,EncodeMs,ExchangeMs,DecodeMs,AcceptMs,TotalMs,ResetMs,RestoreMs,AdvanceMs,WallAgeMs;
            internal string Outcome;
        }
        private readonly PredictionLaunchIdentity launch;
        private readonly NpcPredictionCache cache;
        private readonly SortedSet<int> npcs=new SortedSet<int>(),projectiles=new SortedSet<int>(),assets=new SortedSet<int>();
#if JMR_CONDITIONAL_RESEARCH
        // Query pages do not own AI or strict future alignment. This Session
        // owns exact roles; explicit native mutation replies promote a role.
        private readonly SortedSet<int> exactNpcs=new SortedSet<int>();
#endif
        private NpcIdentity current;
        private Request pending,acceptedRequest;
        private NativePredictionResult accepted;
        private NativeTerrainSnapshot terrain,acknowledgedTerrain;
        private NativeTerrainSnapshot checkedTerrain;
        private readonly NativeTerrainSnapshot.Comparison terrainComparison=new NativeTerrainSnapshot.Comparison();
        private bool checkedTerrainCurrent;
        private NativePredictionAlignment.Frame observation;
        private long lastTick=-1,lastAttempt=-100,version;
        private readonly SortedSet<int> extraChunks=new SortedSet<int>();
        private bool stopped;
        private int recoveries,refusalStreak;
        private long capacityShape=-1;
        private int observedRelocation;
        private bool environmentIntent,menuExpired;
        private long idleSince;
        internal PredictionWorkerClient Worker {get;private set;}
        internal readonly Queue<Measurement> Measurements=new Queue<Measurement>();
        internal long Requests,Published,Rejected,Refused,Observed;
        internal double ObserveMilliseconds,ObserveMaximum;
        internal string Reason {get;private set;}
        internal bool Failed {get;private set;}
        internal NativePredictionSession(PredictionLaunchIdentity launch,NpcPredictionCache cache)
        {
            this.launch=launch;this.cache=cache;
        }
        // Called from the already initialized Host's menu-capable preference
        // poll. It never observes Terraria entities or expands Gameplay gates.
        internal void PollEnvironment(bool requested,bool inWorld)
        {
            if(!requested && Worker==null)return;
            long now=Stopwatch.GetTimestamp();
            if(requested!=environmentIntent)
            {
                environmentIntent=requested;menuExpired=false;idleSince=0;
                if(!requested)DetachWorld();
            }
            if(!requested || !inWorld)
            {
                if(idleSince==0)idleSince=now;
                // A local lifetime choice: keep one dormant child through
                // quick toggles/world selection, release after three idle
                // minutes. A menu timeout cannot immediately restart itself.
                if(now-idleSince>Stopwatch.Frequency*180L){Stop();menuExpired=true;return;}
            }
            else{idleSince=0;menuExpired=false;}
            if(requested && !menuExpired)EnsureEnvironment();
            if(Worker!=null && !stopped && Worker.State==4)
            {
                WorkerFailed();
            }
        }
        private void EnsureEnvironment()
        {
            if(Failed)return;
            if(Worker==null || stopped && Worker.Closed)
            {Worker=launch.Start();stopped=false;acknowledgedTerrain=null;}
        }
        internal void DetachWorld()
        {
            ClearTarget();pending=null;acknowledgedTerrain=null;lastAttempt=-100;
            if(Worker!=null && !stopped && !Worker.Closed)Worker.ResetWorld();
        }
        internal void ClearTarget()
        {
            bool owned=current.Token!=null || accepted!=null;
            current=default(NpcIdentity);accepted=null;acceptedRequest=null;if(owned)cache.Publish(null);lastTick=-1;refusalStreak=0;capacityShape=-1;
            if(pending!=null)pending.Retired=true;
            npcs.Clear();projectiles.Clear();assets.Clear();terrain=null;
#if JMR_CONDITIONAL_RESEARCH
            exactNpcs.Clear();
#endif
            extraChunks.Clear();checkedTerrain=null;observation=null;terrainComparison.Clear();
        }
        // Stopping destroys the transport mailbox. Its request can never
        // complete in a later worker; retain pending only across target swaps.
        internal void Stop(){ClearTarget();pending=null;stopped=true;Worker?.Stop();}
        internal void Retry(){Stop();Failed=false;Reason=null;recoveries=0;}
        private void WorkerFailed()
        {
            Reason=Worker.Failure;
            bool retry=Worker.Recoverable && recoveries==0;
            if(retry)recoveries++;
            else Failed=true;
            // Clear every old mailbox/history before a new owner can start.
            // EnsureEnvironment waits for Closed; old leases/process/late
            // replies cannot overlap the replacement. One recovery budget is
            // replenished only by a genuinely accepted result or explicit Retry.
            Stop();
        }
        // Native/network callbacks only publish a fact. The game-thread owner
        // retires all results and in-flight history before its next Prepare;
        // never mutate the cache or worker mailbox from a receive callback.
        internal void ObservePlayerRelocation(){Interlocked.Exchange(ref observedRelocation,1);}
        internal void ObserveNpcImpact(NPC npc)
        {
            // Native hit callbacks record only a fact on the request which
            // already owned that page. Prepare owns cache retirement; no
            // projectile discovery or full-pool observation is introduced.
            MarkImpact(acceptedRequest,npc);MarkImpact(pending,npc);
        }
        private static void MarkImpact(Request request,NPC npc)
        {
            if(request!=null && (Array.IndexOf(request.Npcs,npc.whoAmI)>=0 || npc.realLife>=0 && Array.IndexOf(request.Npcs,npc.realLife)>=0))
                Interlocked.Exchange(ref request.Impact,1);
        }
        // An alternate synchronous strategy owns publication. Let at most the
        // already-running native request finish, then consume its mailbox
        // without sampling, retrying, or publishing to the shared cache.
        internal void DiscardRetiredResult()
        {if(pending!=null && pending.Retired && Worker!=null && Worker.TryTakeResult()!=null)pending=null;}
        internal void Prepare(NpcIdentity identity,long tick)
        {
            if(Failed)return;
            if(cache.Required==0){ClearTarget();return;}
            if(Interlocked.Exchange(ref observedRelocation,0)!=0)ClearTarget();
            if(!identity.Equals(current)){ClearTarget();current=identity;npcs.Add(identity.Slot);assets.Add(identity.Type);lastAttempt=-100;
#if JMR_CONDITIONAL_RESEARCH
                exactNpcs.Add(identity.Slot);
#endif
            }
            if(tick==lastTick)return;
            if(lastTick>=0 && tick!=lastTick+1){accepted=null;acceptedRequest=null;if(pending!=null)pending.Retired=true;cache.Publish(null);}
            lastTick=tick;
            // Reuse only within this completed game update. History frames
            // own their arrays; none is mutated or carried as a live cache
            // into the next update, world or changed dependency page set.
            checkedTerrain=null;observation=null;terrainComparison.Clear();
            EnsureEnvironment();
            if(stopped)return;
            // A real hit can be absent from the captured projectile pages.
            // Its affected background may have no prior route use, but the
            // new input must revoke both old publication and delayed replies.
            if(acceptedRequest!=null && acceptedRequest.Impact!=0)
            {accepted=null;acceptedRequest=null;cache.Publish(null);Reason="captured NPC impact";}
            if(pending!=null && pending.Impact!=0)pending.Retired=true;
            long started=PredictionPipeProtocol.Measure?Stopwatch.GetTimestamp():0;
            if(pending!=null && !pending.Retired && tick>pending.Tick)
            {
                bool exact;
                if(tick-pending.Tick>PredictionWire.MaximumAlignmentAge || !pending.Terrain.ObserveChanges(identity.Session,pending.TerrainChanges,terrainComparison,out exact)){pending.Retired=true;Reason="in-flight age or world extent changed";}
                else
                {
                    checkedTerrain=pending.Terrain;checkedTerrainCurrent=exact;
                    pending.History.Add(Observe(tick,pending.Npcs,pending.Projectiles,identity.Slot));
                }
            }
            if(accepted!=null)
            {
                long age=tick-acceptedRequest.Tick;
                string difference=!CanReuseProof(age,accepted.Frames.Length)?"expired":!acceptedRequest.Terrain.IsCurrentRelevant(identity.Session,accepted.TerrainUsage,terrainComparison)?"relevant terrain changed":NativePredictionAlignment.Difference(accepted.Frames[(int)age],Observe(tick,acceptedRequest.Npcs,acceptedRequest.Projectiles,identity.Slot));
                if(difference!=null){Reason=difference;accepted=null;acceptedRequest=null;cache.Publish(null);}
            }
            if(PredictionPipeProtocol.Measure){double observed=Milliseconds(Stopwatch.GetTimestamp()-started);ObserveMilliseconds+=observed;ObserveMaximum=Math.Max(ObserveMaximum,observed);}Observed++;
            var response=Worker.TryTakeResult();if(response!=null)Receive(response,tick);
            if(accepted!=null)
            {
                NpcTrajectory window;
                if(accepted.Trajectory.TryWindow(tick,cache.Required,++version,out window)){cache.Publish(window);Published++;}
                else{accepted=null;acceptedRequest=null;cache.Publish(null);Reason="remaining horizon exhausted";}
            }
            if(Worker.State==4)
            {
                WorkerFailed();return;
            }
            // One request may run to completion. Refresh after completion at
            // most once per three updates; changes revoke old presentation
            // immediately but do not cancel every in-flight attempt.
            if(capacityShape!=-1 && CapacityShape()!=capacityShape){capacityShape=-1;refusalStreak=0;lastAttempt=tick-3;}
            if(Worker.State==1 && pending==null && tick-lastAttempt>=3)Capture(identity,tick);
        }
        private void Receive(PredictionWorkerClient.DecodedReply response,long tick)
        {
            Request request=pending;pending=null;if(request==null)return;
            var m=default(Measurement);
            if(PredictionPipeProtocol.Measure)m=new Measurement{CaptureTick=request.Tick,ArriveTick=tick,Age=(int)Math.Min(int.MaxValue,tick-request.Tick),WallAgeMs=Milliseconds(Stopwatch.GetTimestamp()-request.Wall),CaptureMs=request.CaptureMs,Bytes=response.Bytes,ReplyBytes=response.ReplyBytes,EncodeMs=response.EncodeMs,ExchangeMs=response.ExchangeMs,DecodeMs=response.DecodeMs};
            long begin=PredictionPipeProtocol.Measure?Stopwatch.GetTimestamp():0;
            try
            {
                if(request.Retired || !request.Identity.Equals(current)){m.Outcome=request.Impact!=0?"retired captured NPC impact":"retired identity/age/terrain";Rejected++;return;}
                var result=response.Result;
                m.TotalMs=result.TotalMs;m.ResetMs=result.ResetMs;m.RestoreMs=result.RestoreMs;m.AdvanceMs=result.AdvanceMs;
                if(result.Error!=null)
                {
                    Refused++;m.Outcome=result.Error;Reason=result.Error;
                    // Only explicit missing pages are discoverable. Unknown
                    // fields/unsupported calls never become default values.
                    bool added=result.Kind==1 && result.Slot>=0 && result.Slot<=Main.maxNPCs && npcs.Count<=Main.maxNPCs && npcs.Add(result.Slot) || result.Kind==2 && result.Slot>=0 && result.Slot<=Main.maxProjectiles && projectiles.Count<=Main.maxProjectiles && projectiles.Add(result.Slot);
#if JMR_CONDITIONAL_RESEARCH
                    if(result.Kind==5 && result.Slot>=0 && result.Slot<=Main.maxNPCs){added|=npcs.Add(result.Slot);added|=exactNpcs.Add(result.Slot);}
#endif
                    if(result.Kind==0 && response.MissingAsset>=0 && assets.Count<128)added|=assets.Add(response.MissingAsset);
                    if(result.TileX>=0 && result.TileY>=0 && result.TileX<Main.maxTilesX && result.TileY<Main.maxTilesY)
                    {added|=AddMissingTerrain(request.Terrain,result.TileX/32,result.TileY/32);}
                    if(added){refusalStreak=0;capacityShape=-1;}
                    else
                    {
                        // Identical unsupported/capacity inputs back off, but
                        // a new target or discovered necessary page resets the
                        // delay. A first bounded refusal is not a universal
                        // one-second feature outage or a per-update retry loop.
                        bool capacity=result.Kind==-1;
                        int delay=Math.Min(capacity?30:60,6<<Math.Min(4,refusalStreak++));lastAttempt=tick+delay-3;
                        capacityShape=capacity?CapacityShape():-1;
                    }
                    return;
                }
                acknowledgedTerrain=request.Terrain;
                result.TerrainUsage.Validate(request.Terrain);
                if(result.TerrainUsage.Intersects(request.TerrainChanges))
                {m.Outcome="intervening relevant terrain changed";Reason=m.Outcome;Rejected++;return;}
                bool missing=false;foreach(int slot in result.Npcs)if(!npcs.Contains(slot)){if(npcs.Count>Main.maxNPCs)throw new InvalidDataException("NPC dependency capacity.");npcs.Add(slot);missing=true;
#if JMR_CONDITIONAL_RESEARCH
                    exactNpcs.Add(slot);
#endif
                }
                foreach(int slot in result.Projectiles)if(!projectiles.Contains(slot)){if(projectiles.Count>Main.maxProjectiles)throw new InvalidDataException("Projectile dependency capacity.");projectiles.Add(slot);missing=true;}
                if(missing){m.Outcome="newborn dependency pages require fresh observation";Rejected++;return;}
                if(request.History.Count!=tick-request.Tick+1)throw new InvalidDataException("Missing intervening observations.");
                for(int i=0;i<request.History.Count;i++)
                {string difference=NativePredictionAlignment.Difference(result.Frames[i],request.History[i]);if(difference!=null){m.Outcome="history "+i+": "+difference;Reason=m.Outcome;Rejected++;return;}}
                result.Trajectory=result.Trajectory.BindIdentity(request.Identity);
                NpcTrajectory window;if(!result.Trajectory.TryWindow(tick,cache.Required,version,out window)){m.Outcome="insufficient remaining horizon";Rejected++;return;}
                accepted=result;acceptedRequest=request;m.Outcome="accepted";Reason=null;recoveries=refusalStreak=0;capacityShape=-1;
            }
            catch(Exception error)
            {
                if(error is OutOfMemoryException)throw;m.Outcome="invalid result: "+error.Message;Reason=m.Outcome;Rejected++;
                Failed=true;Stop();
            }
            finally{if(PredictionPipeProtocol.Measure){m.AcceptMs=Milliseconds(Stopwatch.GetTimestamp()-begin);Measurements.Enqueue(m);while(Measurements.Count>256)Measurements.Dequeue();}}
        }
        private void Capture(NpcIdentity identity,long tick)
        {
            lastAttempt=tick;long begin=PredictionPipeProtocol.Measure?Stopwatch.GetTimestamp():0;
            try
            {
#if JMR_CONDITIONAL_RESEARCH
                // Gather native structural links only from exact actors. A
                // queried bunny's unrelated links cannot grow this closure.
                exactNpcs.RemoveWhere(slot=>slot!=identity.Slot && !Main.npc[slot].active);
                npcs.RemoveWhere(slot=>slot!=identity.Slot && !Main.npc[slot].active);
                int[] queries=npcs.Where(slot=>!exactNpcs.Contains(slot)).ToArray();
                foreach(int slot in queries)npcs.Remove(slot);
                GatherDependencies();exactNpcs.UnionWith(npcs);
                foreach(int slot in queries)npcs.Add(slot);
                ConditionalNpcQuery.CaptureRoles=exactNpcs.ToArray();
#else
                GatherDependencies();
#endif
                int[] ns=npcs.ToArray(),ps=projectiles.ToArray();foreach(int slot in ns)if(Main.npc[slot].type>=0)assets.Add(Main.npc[slot].type);
                var target=Main.npc[identity.Slot];int x=(int)target.Center.X/16,y=(int)target.Center.Y/16;
                // Disjoint observed neighborhoods: a distant player or linked
                // actor must not make the unrelated rectangle between them
                // part of every snapshot. Unprovided cells remain unknown.
                var chunks=new SortedSet<int>(extraChunks);AddRegion(chunks,x,y,64);
                foreach(int slot in ns)if(slot!=identity.Slot && Main.npc[slot].active
#if JMR_CONDITIONAL_RESEARCH
                    && exactNpcs.Contains(slot)
#endif
                    )AddRegion(chunks,(int)Main.npc[slot].Center.X/16,(int)Main.npc[slot].Center.Y/16,24);
                foreach(var player in Main.player)if(player!=null && player.active)AddRegion(chunks,(int)player.Center.X/16,(int)player.Center.Y/16,24);
                if(terrain==null || !TerrainCurrent(terrain,identity.Session) || !Covers(terrain,chunks))terrain=NativeTerrainSnapshot.CaptureChunksObserved(identity.Session,chunks,terrainComparison);
                var request=new Request{Identity=identity,Tick=tick,Wall=begin,Npcs=ns,Projectiles=ps,Terrain=terrain};
#if JMR_CONDITIONAL_RESEARCH
                request.Npcs=exactNpcs.ToArray();
#endif
                request.History.Add(Observe(tick,request.Npcs,ps,identity.Slot));
                var values=PredictionWire.FillProductionValues(Worker.BeginCapture(),ns,ps,identity.Slot,tick,PredictionWire.MaximumHorizon,terrain,assets.ToArray(),ReferenceEquals(terrain,acknowledgedTerrain));
                if(PredictionPipeProtocol.Measure)request.CaptureMs=Milliseconds(Stopwatch.GetTimestamp()-begin);
                var valueIdentity=new NpcIdentity(identity.Session,null,identity.Slot,identity.Generation,identity.Type,identity.NetId);
                if(Worker.TrySendValues(values,valueIdentity,tick,Main.netMode==1)){pending=request;Requests++;}
            }
            catch(Exception error)
            {
                if(error is OutOfMemoryException)throw;Reason="capture: "+error.Message;lastAttempt=tick+57;
            }
        }
        private void GatherDependencies()
        {
            if(NPC.brainOfGravity>=0 && NPC.brainOfGravity<Main.maxNPCs)AddNpcIndex(NPC.brainOfGravity);
            // Known index relationships only. Acquiring a complete page is
            // also permission to advance that actor, so unrelated nearby
            // actors must not be included merely to avoid a missing-page reply.
            // Bound by the native pool (including its sentinel), not a smaller
            // arbitrary page cap: a natural Destroyer needs 82 or 102 pages.
            // This does not authorize advancing the directory's other actors.
            bool changed;
            do
            {
                changed=false;
                foreach(int slot in npcs.ToArray())
                {
                    NPC n=Main.npc[slot];if(!n.active)continue;
                    if(n.aiStyle==12)changed|=AddNpcIndex(n.ai[1]);
                    if(n.aiStyle==11)
                        for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active && Main.npc[i].aiStyle==12 && Main.npc[i].ai[1]==slot)changed|=npcs.Add(i);
                    if(n.aiStyle==6 || n.aiStyle==37)
                    {if(n.ai[0]>0)changed|=AddNpcIndex(n.ai[0]);if(n.ai[1]>0)changed|=AddNpcIndex(n.ai[1]);if(n.realLife>=0)changed|=AddNpcIndex(n.realLife);}
                    if(n.type==396 || n.type==397 || n.type==400)changed|=AddNpcIndex(n.ai[3]);
                    if(n.type==398)for(int i=0;i<3;i++)changed|=AddNpcIndex(n.localAI[i]);
                    if(n.type==401)
                    {
                        changed|=AddNpcIndex(Math.Abs(n.ai[0])-1);
                        Projectile clot;if(Projectile.TryLookup((Terraria.DataStructures.ProjectileKey)n.ai[1],out clot))projectiles.Add(clot.whoAmI);
                    }
                }
                if(npcs.Count>Main.maxNPCs+1 || projectiles.Count>Main.maxProjectiles+1)throw new InvalidDataException("Native dependency capacity.");
            }while(changed);
            // Harpy's native 30/60/90 firing cycle can allocate three shots in
            // this horizon. Reserve actual inactive pages in native slot order;
            // do not construct replacement objects or advance unrelated shots.
            if(npcs.Any(slot=>Main.npc[slot].active && Main.npc[slot].type==48) && !Terraria.Testing.DebugOptions.Shared_RandomizeProjectileSlots)
            {int needed=3;for(int i=0;i<Main.maxProjectiles && needed>0;i++)if(!Main.projectile[i].active){projectiles.Add(i);needed--;}}
        }
        private bool AddNpcIndex(float value)
        {if(float.IsNaN(value) || float.IsInfinity(value) || value<0 || value>=Main.maxNPCs || value!=(int)value)throw new InvalidDataException("Invalid native NPC link.");return npcs.Add((int)value);}
        private long CapacityShape()
        {
            // Only while a capacity refusal is backed off. Bound by pages
            // already owned by this target, with no new observation/arrays or
            // whole-state hashing. Activity, identity/trail extent and crossing
            // the finite horizon can change result size and invite a new try.
            unchecked
            {
                long shape=17;
                foreach(int slot in npcs){var n=Main.npc[slot];shape=shape*31+(n.active?1:0);shape=shape*31+n.type;shape=shape*31+(n.oldPos?.Length??0);shape=shape*31+(n.timeLeft<=PredictionWire.MaximumHorizon?1:0);}
                foreach(int slot in projectiles){var p=Main.projectile[slot];shape=shape*31+(p.active?1:0);shape=shape*31+p.type;shape=shape*31+(p.oldPos?.Length??0);shape=shape*31+(p.timeLeft<=PredictionWire.MaximumHorizon?1:0);}
                foreach(var p in Main.player)if(p!=null && p.active)shape=shape*31+p.whoAmI+1;
                return shape==-1?0:shape;
            }
        }
        private bool TerrainCurrent(NativeTerrainSnapshot value,long world)
        {if(!ReferenceEquals(checkedTerrain,value)){checkedTerrain=value;checkedTerrainCurrent=value.IsCurrentObserved(world,terrainComparison);}return checkedTerrainCurrent;}
        private NativePredictionAlignment.Frame Observe(long tick,int[] ns,int[] ps,int selected)
        {
            if(observation==null || !Same(observation.Npcs,ns) || !Same(observation.Projectiles,ps))observation=NativePredictionAlignment.Observe(tick,ns,ps,selected);
            return observation;
        }
        private static bool Same(int[] a,int[] b)
        {if(ReferenceEquals(a,b))return true;if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        private bool AddMissingTerrain(NativeTerrainSnapshot prior,int x,int y)
        {
            // A moving player crossing a chunk corner otherwise takes several
            // full async retries one cell at a time. Acquire a bounded local
            // neighborhood around the actual missing page, never an assumed
            // whole-world/future sweep. Capacity and unknown-cell refusal stay.
            var union=new HashSet<int>(extraChunks);foreach(var chunk in prior.Chunks)union.Add(chunk.X*128+chunk.Y);
            bool added=false;
            for(int radius=0;radius<=1;radius++)for(int dx=-radius;dx<=radius;dx++)for(int dy=-radius;dy<=radius;dy++)
            {
                if(Math.Max(Math.Abs(dx),Math.Abs(dy))!=radius)continue;
                int cx=x+dx,cy=y+dy;if(cx<0 || cy<0 || cx*32>=Main.maxTilesX || cy*32>=Main.maxTilesY)continue;
                int key=cx*128+cy;if(union.Contains(key) || union.Count>=NativeTerrainSnapshot.MaximumChunks)continue;
                union.Add(key);added|=extraChunks.Add(key);
            }
            return added;
        }
        // One freshness limit applies even to natural-end/short-demand
        // windows, which TryWindow can otherwise keep after age 60. Their
        // future end remains real; continuing display needs a fresh capture.
        internal static bool CanReuseProof(long age,int count){return age>=0 && age<=PredictionWire.MaximumAlignmentAge && age<count;}
        private static void AddRegion(SortedSet<int> chunks,int x,int y,int radius)
        {NativeTerrainSnapshot.AddRegion(chunks,Math.Max(0,x-radius),Math.Max(0,y-radius),Math.Min(Main.maxTilesX-1,x+radius),Math.Min(Main.maxTilesY-1,y+radius));}
        private static bool Covers(NativeTerrainSnapshot terrain,SortedSet<int> keys)
        {int found=0;foreach(var chunk in terrain.Chunks)if(keys.Contains(chunk.X*128+chunk.Y))found++;return found==keys.Count;}
        private static double Milliseconds(long ticks){return ticks*1000.0/Stopwatch.Frequency;}
    }
}
