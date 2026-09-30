using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            internal bool Retired;
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
#if PREDICTION_DIAGNOSTIC
        private readonly NativePredictionDiagnostic diagnostic;
#endif
        private readonly SortedSet<int> npcs=new SortedSet<int>(),projectiles=new SortedSet<int>(),assets=new SortedSet<int>();
        private NpcIdentity current;
        private Request pending,acceptedRequest;
        private NativePredictionResult accepted;
        private NativeTerrainSnapshot terrain,acknowledgedTerrain;
        private NativeTerrainSnapshot checkedTerrain;
        private bool checkedTerrainCurrent;
        private NativePredictionAlignment.Frame observation;
        private long lastTick=-1,lastAttempt=-100,version;
        private readonly SortedSet<int> extraChunks=new SortedSet<int>();
        private bool stopped;
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
#if PREDICTION_DIAGNOSTIC
            diagnostic=launch==null?null:new NativePredictionDiagnostic(launch.CacheDirectory);
#endif
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
                Reason=Worker.Failure;
#if PREDICTION_DIAGNOSTIC
                // A startup fault can close the Path gate before Prepare is
                // reached. This observation must not depend on an NPC target.
                diagnostic?.WorkerFault(Reason);
#endif
                Failed=true;Stop();
            }
#if PREDICTION_DIAGNOSTIC
            // The transport drains bounded stderr before publishing Closed;
            // this later poll preserves the cause behind a generic pipe EOF.
            if(Worker!=null && Worker.Closed)diagnostic?.WorkerExit(Worker.Diagnostics);
#endif
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
            current=default(NpcIdentity);accepted=null;acceptedRequest=null;cache.Publish(null);lastTick=-1;
            if(pending!=null)pending.Retired=true;
            npcs.Clear();projectiles.Clear();assets.Clear();terrain=null;
            extraChunks.Clear();checkedTerrain=null;observation=null;
        }
        // Stopping destroys the transport mailbox. Its request can never
        // complete in a later worker; retain pending only across target swaps.
        internal void Stop(){ClearTarget();pending=null;stopped=true;Worker?.Stop();}
        internal void Retry(){Stop();Failed=false;Reason=null;}
        // An alternate synchronous strategy owns publication. Let at most the
        // already-running native request finish, then consume its mailbox
        // without sampling, retrying, or publishing to the shared cache.
        internal void DiscardRetiredResult()
        {if(pending!=null && pending.Retired && Worker!=null && Worker.TryTakeResult()!=null)pending=null;}
        internal void Prepare(NpcIdentity identity,long tick)
        {
#if PREDICTION_DIAGNOSTIC
            try
            {
            diagnostic?.Observe(this,identity,tick,lastTick);
#endif
            if(Failed)return;
            if(cache.Required==0){ClearTarget();return;}
            if(!identity.Equals(current)){ClearTarget();current=identity;npcs.Add(identity.Slot);assets.Add(identity.Type);lastAttempt=-100;}
            if(tick==lastTick)return;
            if(lastTick>=0 && tick!=lastTick+1){accepted=null;acceptedRequest=null;if(pending!=null)pending.Retired=true;cache.Publish(null);}
            lastTick=tick;
            // Reuse only within this completed game update. History frames
            // own their arrays; none is mutated or carried as a live cache
            // into the next update, world or changed dependency page set.
            checkedTerrain=null;observation=null;
            EnsureEnvironment();
            if(stopped)return;
            long started=PredictionPipeProtocol.Measure?Stopwatch.GetTimestamp():0;
            if(pending!=null && !pending.Retired && tick>pending.Tick)
            {
                if(tick-pending.Tick>PredictionWire.MaximumAlignmentAge || !TerrainCurrent(pending.Terrain,identity.Session)){pending.Retired=true;Reason="in-flight age or terrain changed";}
                else pending.History.Add(Observe(tick,pending.Npcs,pending.Projectiles,identity.Slot));
            }
            if(accepted!=null)
            {
                long age=tick-acceptedRequest.Tick;
                string difference=!CanReuseProof(age,accepted.Frames.Length)?"expired":!TerrainCurrent(acceptedRequest.Terrain,identity.Session)?"terrain changed":NativePredictionAlignment.Difference(accepted.Frames[(int)age],Observe(tick,acceptedRequest.Npcs,acceptedRequest.Projectiles,identity.Slot));
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
                Reason=Worker.Failure;
#if PREDICTION_DIAGNOSTIC
                diagnostic?.WorkerFault(Reason);
#endif
                Failed=true;Stop();return;
            }
            // One request may run to completion. Refresh after completion at
            // most once per three updates; changes revoke old presentation
            // immediately but do not cancel every in-flight attempt.
            if(Worker.State==1 && pending==null && tick-lastAttempt>=3)Capture(identity,tick);
#if PREDICTION_DIAGNOSTIC
            }
            catch(Exception error){diagnostic?.WorkerFault("prepare: "+error);throw;}
#endif
        }
        private void Receive(PredictionWorkerClient.DecodedReply response,long tick)
        {
            Request request=pending;pending=null;if(request==null)return;
            var m=default(Measurement);
            if(PredictionPipeProtocol.Measure)m=new Measurement{CaptureTick=request.Tick,ArriveTick=tick,Age=(int)Math.Min(int.MaxValue,tick-request.Tick),WallAgeMs=Milliseconds(Stopwatch.GetTimestamp()-request.Wall),CaptureMs=request.CaptureMs,Bytes=response.Bytes,ReplyBytes=response.ReplyBytes,EncodeMs=response.EncodeMs,ExchangeMs=response.ExchangeMs,DecodeMs=response.DecodeMs};
            long begin=PredictionPipeProtocol.Measure?Stopwatch.GetTimestamp():0;
            try
            {
                if(request.Retired || !request.Identity.Equals(current)){m.Outcome="retired identity/age/terrain";Rejected++;return;}
                var result=response.Result;
                m.TotalMs=result.TotalMs;m.ResetMs=result.ResetMs;m.RestoreMs=result.RestoreMs;m.AdvanceMs=result.AdvanceMs;
                if(result.Error!=null)
                {
                    Refused++;m.Outcome=result.Error;Reason=result.Error;
                    // Only explicit missing pages are discoverable. Unknown
                    // fields/unsupported calls never become default values.
                    bool added=result.Kind==1 && result.Slot>=0 && result.Slot<=Main.maxNPCs && npcs.Count<=Main.maxNPCs && npcs.Add(result.Slot) || result.Kind==2 && result.Slot>=0 && result.Slot<=Main.maxProjectiles && projectiles.Count<=Main.maxProjectiles && projectiles.Add(result.Slot);
                    if(result.Kind==0 && response.MissingAsset>=0 && assets.Count<128)added|=assets.Add(response.MissingAsset);
                    if(result.TileX>=0 && result.TileY>=0 && result.TileX<Main.maxTilesX && result.TileY<Main.maxTilesY)
                    {added|=extraChunks.Add(result.TileX/32*128+result.TileY/32);terrain=null;}
                    if(!added)lastAttempt=tick+57;return;
                }
                acknowledgedTerrain=request.Terrain;
                bool missing=false;foreach(int slot in result.Npcs)if(!npcs.Contains(slot)){if(npcs.Count>Main.maxNPCs)throw new InvalidDataException("NPC dependency capacity.");npcs.Add(slot);missing=true;}
                foreach(int slot in result.Projectiles)if(!projectiles.Contains(slot)){if(projectiles.Count>Main.maxProjectiles)throw new InvalidDataException("Projectile dependency capacity.");projectiles.Add(slot);missing=true;}
                if(missing){m.Outcome="newborn dependency pages require fresh observation";Rejected++;return;}
                if(request.History.Count!=tick-request.Tick+1)throw new InvalidDataException("Missing intervening observations.");
                for(int i=0;i<request.History.Count;i++)
                {string difference=NativePredictionAlignment.Difference(result.Frames[i],request.History[i]);if(difference!=null){m.Outcome="history "+i+": "+difference;Reason=m.Outcome;Rejected++;return;}}
                result.Trajectory=result.Trajectory.BindIdentity(request.Identity);
                NpcTrajectory window;if(!result.Trajectory.TryWindow(tick,cache.Required,version,out window)){m.Outcome="insufficient remaining horizon";Rejected++;return;}
                accepted=result;acceptedRequest=request;m.Outcome="accepted";Reason=null;
            }
            catch(Exception error)
            {
                if(error is OutOfMemoryException)throw;m.Outcome="invalid result: "+error.Message;Reason=m.Outcome;Rejected++;
#if PREDICTION_DIAGNOSTIC
                diagnostic?.WorkerFault("receive: "+error);
#endif
                Failed=true;Stop();
            }
            finally{if(PredictionPipeProtocol.Measure){m.AcceptMs=Milliseconds(Stopwatch.GetTimestamp()-begin);Measurements.Enqueue(m);while(Measurements.Count>256)Measurements.Dequeue();}}
        }
        private void Capture(NpcIdentity identity,long tick)
        {
            lastAttempt=tick;long begin=PredictionPipeProtocol.Measure?Stopwatch.GetTimestamp():0;
            try
            {
                GatherDependencies();
                int[] ns=npcs.ToArray(),ps=projectiles.ToArray();foreach(int slot in ns)if(Main.npc[slot].type>=0)assets.Add(Main.npc[slot].type);
                var target=Main.npc[identity.Slot];int x=(int)target.Center.X/16,y=(int)target.Center.Y/16;
                // Disjoint observed neighborhoods: a distant player or linked
                // actor must not make the unrelated rectangle between them
                // part of every snapshot. Unprovided cells remain unknown.
                var chunks=new SortedSet<int>(extraChunks);AddRegion(chunks,x,y,64);
                foreach(int slot in ns)if(slot!=identity.Slot && Main.npc[slot].active)AddRegion(chunks,(int)Main.npc[slot].Center.X/16,(int)Main.npc[slot].Center.Y/16,24);
                foreach(var player in Main.player)if(player!=null && player.active)AddRegion(chunks,(int)player.Center.X/16,(int)player.Center.Y/16,24);
                if(terrain==null || !TerrainCurrent(terrain,identity.Session) || !Covers(terrain,chunks))terrain=NativeTerrainSnapshot.CaptureChunks(identity.Session,chunks);
                var request=new Request{Identity=identity,Tick=tick,Wall=begin,Npcs=ns,Projectiles=ps,Terrain=terrain};
                request.History.Add(Observe(tick,ns,ps,identity.Slot));
                var values=PredictionWire.FillProductionValues(Worker.BeginCapture(),ns,ps,identity.Slot,tick,PredictionWire.MaximumHorizon,terrain,assets.ToArray(),ReferenceEquals(terrain,acknowledgedTerrain));
                if(PredictionPipeProtocol.Measure)request.CaptureMs=Milliseconds(Stopwatch.GetTimestamp()-begin);
                var valueIdentity=new NpcIdentity(identity.Session,null,identity.Slot,identity.Generation,identity.Type,identity.NetId);
                if(Worker.TrySendValues(values,valueIdentity,tick,Main.netMode==1)){pending=request;Requests++;}
            }
            catch(Exception error)
            {
                if(error is OutOfMemoryException)throw;Reason="capture: "+error.Message;lastAttempt=tick+57;
#if PREDICTION_DIAGNOSTIC
                diagnostic?.CaptureFault(error);
#endif
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
        private bool TerrainCurrent(NativeTerrainSnapshot value,long world)
        {if(!ReferenceEquals(checkedTerrain,value)){checkedTerrain=value;checkedTerrainCurrent=value.IsCurrent(world);}return checkedTerrainCurrent;}
        private NativePredictionAlignment.Frame Observe(long tick,int[] ns,int[] ps,int selected)
        {
            if(observation==null || !Same(observation.Npcs,ns) || !Same(observation.Projectiles,ps))observation=NativePredictionAlignment.Observe(tick,ns,ps,selected);
            return observation;
        }
        private static bool Same(int[] a,int[] b)
        {if(ReferenceEquals(a,b))return true;if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
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
