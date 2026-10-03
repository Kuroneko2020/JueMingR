using System;
using System.Diagnostics;
using JueMingR.Platform.Combat;
using Terraria;
#if JMR_AIM_LIGHT
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
#endif

namespace JueMingR.TerrariaHost.Combat.Prediction
{
    // Conditional call sites disappear, including argument evaluation, from
    // ordinary builds. In the diagnostic build every entry checks OFF before
    // clock/format/copy work and contains its own failures, including OOM.
    internal static class AimLightTrace
    {
#if JMR_AIM_LIGHT
        private static AimLightLog log;
        private sealed class Token {internal long Id;}
        private static ConditionalWeakTable<object,Token> tokens;
        private static long nextToken,gateKey=-1,published,cacheCapture=-1,cacheWorker,preparedCapture=-1,preparedWorker,frameTick=-1,prepared,issued,completed,textIssued,textCompleted,collisionIssued,collisionCompleted,collisionTextIssued,collisionTextCompleted,lastSummaryTick=-60;
        private static bool cacheValid,frameDemand,frameCache,frameCollision,framePrepared,frameDraw;
        private static string frameScene;
        private static object identityGate;
        private static Dictionary<string,long> presentationKeys;
        private static long Id(object value)
        {
            if(value==null)return 0;
            lock(identityGate){Token token;if(!tokens.TryGetValue(value,out token)){token=new Token{Id=++nextToken};tokens.Add(value,token);}return token.Id;}
        }
        private static bool On=>log!=null && log.Accepting;
        private static string Identity(NpcIdentity n)
        {return "world="+n.Session+";token="+Id(n.Token)+";slot="+n.Slot+";generation="+n.Generation+";type="+n.Type+";netId="+n.NetId;}
        private static string Key(NpcIdentity n,long tick,object worker)
        {return Identity(n)+";worker="+Id(worker)+";capture="+tick;}
        private static void Broken(Exception error){log?.Abort(error);}
        private static string F(float value){return value.ToString("R",CultureInfo.InvariantCulture);}
#endif
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Start(string directory,PredictionLaunchIdentity launch,string package)
        {
#if JMR_AIM_LIGHT
            // Only a launch authorized by Host composition can arm this run.
            // The private worker never constructs HostCombatObservation.
            try
            {
                if(log!=null || launch==null || Environment.GetEnvironmentVariable("JUEMINGR_AIM_LIGHT_OFF")=="1")return;
                tokens=new ConditionalWeakTable<object,Token>();
                identityGate=new object();presentationKeys=new Dictionary<string,long>();
                log=new AimLightLog(Path.Combine(directory,"JueMingRData","logs","aim-light"),launch.Hashes[2],()=>
                {
                    var assembly=typeof(AimLightTrace).Assembly;
                    return "source="+assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion+";mvid="+assembly.ManifestModule.ModuleVersionId+";protocol="+PredictionWire.Protocol+";game="+PredictionPipeProtocol.GameHash+";workerHash="+launch.Hashes[0]+";package="+(package??"unknown")+";preArm=unobserved;carryIn=unknown-unless-worker-create-observed;fullWorkerError=unavailable;actualCanHit=unavailable;fieldHistory=unavailable;replay=unavailable";
                });
            }
            catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void End(string reason)
        {
#if JMR_AIM_LIGHT
            if(!On)return;try{FinishFrame();log.Offer("presentation-totals",frameTick,"published="+published+";pathPrepared="+prepared+";pathIssued="+issued+";pathCompleted="+completed+";textIssued="+textIssued+";textCompleted="+textCompleted+";collisionIssued="+collisionIssued+";collisionCompleted="+collisionCompleted+";gpuVisible=unknown");log.Stop(reason);}catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Host(HostCombatObservation host,bool pathFailed,string stage,bool frame)
        {
#if JMR_AIM_LIGHT
            if(!On)return;
            try
            {
                var o=host.Options;var native=host.Prediction.Native;var worker=native?.Worker;var selected=host.Selection.HasTarget?host.Selection.Target:default(NpcIdentity);
                int flags=(o.Path?1:0)|(o.Collision?2:0)|(o.ClearLine?4:0)|(o.MouseCenter?8:0)|(o.Dummy?16:0)|(host.Settings.CanRun?32:0)|(host.Hooks.Ready?64:0)|(host.CanDraw?128:0)|(pathFailed?256:0)|(native!=null && native.Failed?512:0);
                long key=unchecked(((((((Id(selected.Token)*397+selected.Generation)*397+selected.Type)*397+selected.NetId)*397+host.Session)*397+flags)*397+(worker?.State??-1))*397+o.Radius+host.Prediction.Cache.Required*31+(int)host.LayerStatus);
                if(key!=gateKey){gateKey=key;log.Offer("host-gate",(long)Main.GameUpdateCount,"stage="+stage+";"+Identity(selected)+";session="+host.Session+";flags="+flags+";pathWanted="+o.Path+";path="+host.Path+";pathFailed="+pathFailed+";nativeFailed="+(native?.Failed??false)+";worker="+Id(worker)+";state="+(worker?.State??-1)+";radius="+o.Radius+";required="+host.Prediction.Cache.Required+";layer="+host.LayerStatus);}
                if(frame && frameTick!=(long)Main.GameUpdateCount)
                {
                    FinishFrame();frameTick=(long)Main.GameUpdateCount;
                    frameScene="world="+host.Session+";type="+(host.Selection.HasTarget?selected.Type:-1)+";netId="+(host.Selection.HasTarget?selected.NetId:0);
                    frameDemand=host.Prediction.Cache.Required>0;frameCache=cacheValid;frameCollision=host.Collision;framePrepared=frameDraw=false;
                    if(frameTick-lastSummaryTick>=60)
                    {
                        lastSummaryTick=frameTick;
                        log.Offer("presentation-totals",frameTick,"published="+published+";pathPrepared="+prepared+";pathIssued="+issued+";pathCompleted="+completed+";pathTextIssued="+textIssued+";pathTextCompleted="+textCompleted+";collisionIssued="+collisionIssued+";collisionCompleted="+collisionCompleted+";otherTextIssued="+collisionTextIssued+";otherTextCompleted="+collisionTextCompleted+";gpuVisible=unknown");
                    }
                }
            }
            catch(Exception error){Broken(error);}
#endif
        }
#if JMR_AIM_LIGHT
        private static void FinishFrame(){if(frameTick>=0)log.Sample(frameScene,frameTick,frameDemand,frameCache,frameCollision,framePrepared,frameDraw);}
#endif
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Session(string stage,long tick,NpcIdentity current,PredictionWorkerClient worker,long pending,long accepted,int history,int npcs,int shots,string reason)
        {
#if JMR_AIM_LIGHT
            if(!On)return;try{log.Offer(stage,tick,Key(current,tick,worker)+";pendingCapture="+pending+";acceptedCapture="+accepted+";history="+history+";npcPages="+npcs+";projectilePages="+shots+";reason="+reason);}catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Relocation(string reason,Player player,double result)
        {
#if JMR_AIM_LIGHT
            if(!On)return;try{log.Offer("player-input",(long)Main.GameUpdateCount,"reason="+reason+";slot="+player.whoAmI+";result="+result.ToString("R",CultureInfo.InvariantCulture));}catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Capture(string stage,NpcIdentity identity,long tick,PredictionWorkerClient worker,int npcs,int shots,int chunks)
        {
#if JMR_AIM_LIGHT
            if(!On)return;try{log.Offer(stage,tick,Key(identity,tick,worker)+";npcPages="+npcs+";projectilePages="+shots+";chunks="+chunks);}catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Reply(PredictionWorkerClient worker,string stage,NpcIdentity identity,long capture,long sequence,int bytes,int replyBytes,NativePredictionResult result)
        {
#if JMR_AIM_LIGHT
            if(!On)return;try{log.Offer(stage,capture,Key(identity,capture,worker)+";wire="+sequence+";bytes="+bytes+";replyBytes="+replyBytes+";steps="+((result?.Frames?.Length??1)-1));}catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Receive(NpcIdentity identity,long capture,long tick,PredictionWorkerClient worker,NativePredictionResult result,int history,string outcome)
        {
#if JMR_AIM_LIGHT
            if(!On)return;
            try
            {
                int steps=(result?.Frames?.Length??1)-1;
                string detail=Key(identity,capture,worker)+";age="+(tick-capture)+";history="+history+";steps="+steps+";future="+(steps-(tick-capture))+";outcome="+outcome;
                log.Offer("receive-exit",tick,detail);
                if(result?.Error!=null)log.First("reply:"+result.Kind+":"+result.Slot+":"+result.LightField,tick,detail+";missingKind="+result.Kind+";slot="+result.Slot+";field="+result.LightField+";access=unknown;actor=unknown;phase=unknown;tileX="+result.TileX+";tileY="+result.TileY+";workerError="+result.Error);
            }
            catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Difference(long tick,int index,string reason,NativePredictionAlignment.Frame expected,NativePredictionAlignment.Frame actual)
        {
#if JMR_AIM_LIGHT
            if(!On)return;
            try
            {
                // Use only the already compared frames and the returned first
                // category/slot. Never reconstruct a past value from live Main.
                string detail="index="+index+";category="+reason+";expectedWorld="+expected.World+";actualWorld="+actual.World;
                int marker=(reason??"").LastIndexOf("slot=",StringComparison.Ordinal),slot;
                if(marker>=0 && int.TryParse(reason.Substring(marker+5),out slot))
                {
                    int[] slots=reason.StartsWith("NPC",StringComparison.Ordinal)?expected.Npcs:reason.StartsWith("Projectile",StringComparison.Ordinal)?expected.Projectiles:expected.Players;
                    ulong[] a=reason.StartsWith("NPC",StringComparison.Ordinal)?(reason.StartsWith("NPC identity",StringComparison.Ordinal)?expected.NpcIdentity:expected.NpcState):reason.StartsWith("Projectile",StringComparison.Ordinal)?(reason.StartsWith("Projectile identity",StringComparison.Ordinal)?expected.ProjectileIdentity:expected.ProjectileState):expected.PlayerPremise;
                    ulong[] b=reason.StartsWith("NPC",StringComparison.Ordinal)?(reason.StartsWith("NPC identity",StringComparison.Ordinal)?actual.NpcIdentity:actual.NpcState):reason.StartsWith("Projectile",StringComparison.Ordinal)?(reason.StartsWith("Projectile identity",StringComparison.Ordinal)?actual.ProjectileIdentity:actual.ProjectileState):actual.PlayerPremise;
                    int position=Array.IndexOf(slots,slot);detail+=";slot="+slot;
                    if(reason.StartsWith("NPC sampled shooting clock",StringComparison.Ordinal))
                    {if(position>=0 && position<expected.NpcShootClock.Length && position<actual.NpcShootClock.Length)detail+=";expectedClock="+F(expected.NpcShootClock[position])+";actualClock="+F(actual.NpcShootClock[position]);}
                    else if(position>=0 && position<a.Length && position<b.Length)detail+=";expectedHash="+a[position]+";actualHash="+b[position];
                }
                log.First("history:"+reason,tick,detail);
            }
            catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Fault(string stage,Exception businessError,long tick)
        {
#if JMR_AIM_LIGHT
            if(!On)return;try{log.First("exception:"+stage,tick,"type="+businessError.GetType().FullName+";assembly="+businessError.GetType().Assembly.FullName+";original="+businessError);}catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Actor(NPC target,long tick)
        {
#if JMR_AIM_LIGHT
            if(!On)return;try{log.Offer("captured-target",tick,"slot="+target.whoAmI+";token="+Id(target)+";type="+target.type+";netId="+target.netID+";generation="+target.generation+";target="+target.target+";life="+target.life+";ai="+F(target.ai[0])+","+F(target.ai[1])+","+F(target.ai[2])+","+F(target.ai[3])+";localAI="+F(target.localAI[0])+","+F(target.localAI[1])+","+F(target.localAI[2])+","+F(target.localAI[3])+";wet="+target.wet+";collideY="+target.collideY);}catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Player(Player player,long tick)
        {
#if JMR_AIM_LIGHT
            if(!On)return;try{log.Offer("captured-player",tick,"slot="+player.whoAmI+";dead="+player.dead+";life="+player.statLife+";immune="+player.immune+";immuneTime="+player.immuneTime+";position="+F(player.position.X)+","+F(player.position.Y)+";velocity="+F(player.velocity.X)+","+F(player.velocity.Y)+";inputs="+player.controlLeft+","+player.controlRight+","+player.controlJump+","+player.controlUseItem+";mount="+player.mount.Type+";tankPet="+player.tankPet);}catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Guardian(Projectile source,long tick)
        {
#if JMR_AIM_LIGHT
            if(!On)return;try{if(source.type==623)log.Offer("captured-guardian",tick,"slot="+source.whoAmI+";token="+Id(source)+";key="+(uint)source.key+";owner="+source.owner+";active="+source.active+";ai="+F(source.ai[0])+","+F(source.ai[1])+","+F(source.ai[2])+";localAI="+F(source.localAI[0])+","+F(source.localAI[1])+","+F(source.localAI[2]));}catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Hit(NativeImpactProof.Hit hit)
        {
#if JMR_AIM_LIGHT
            if(!On)return;try{log.Offer(hit.Kind==0?"actual-hit":"actual-birth",hit.Tick,"sourceSlot="+hit.SourceSlot+";type="+hit.SourceType+";owner="+hit.SourceOwner+";key="+hit.SourceKey+";generation="+hit.SourceGeneration+";epoch="+hit.SourceEpoch+";known="+hit.Known+";target="+hit.TargetSlot+";parentKind="+hit.ParentKind+";parentSlot="+hit.ParentSlot+";signature="+hit.Signature);}catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Cache(NpcTrajectory value,PredictionWorkerClient worker,string reason)
        {
#if JMR_AIM_LIGHT
            if(!On)return;
            try
            {
                if(value==null){if(cacheValid)log.Offer("cache-clear",(long)Main.GameUpdateCount,"capture="+cacheCapture+";worker="+cacheWorker+";reason="+reason);cacheValid=false;return;}
                published++;long owner=Id(worker);
                if(!cacheValid || cacheCapture!=value.CaptureTick || cacheWorker!=owner)log.Offer("cache-publish",(long)Main.GameUpdateCount,Key(value.Identity,value.CaptureTick,worker)+";count="+value.Count+";strategy="+value.Strategy);
                cacheValid=true;cacheCapture=value.CaptureTick;cacheWorker=owner;
            }
            catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void Presentation(string stage,NpcTrajectory path,int pathStrokes,int collisions,bool text)
        {
#if JMR_AIM_LIGHT
            if(!On)return;
            try
            {
                long tick=(long)Main.GameUpdateCount;
                if(stage=="prepare-enter"){framePrepared=true;preparedCapture=-1;preparedWorker=0;return;}
                if(stage=="draw-enter"){frameDraw=true;return;}
                if(stage=="prepared"){prepared+=pathStrokes;preparedCapture=path?.CaptureTick??-1;preparedWorker=cacheWorker;}
                if(stage=="issued"){issued+=pathStrokes;collisionIssued+=collisions;}
                if(stage=="completed"){completed+=pathStrokes;collisionCompleted+=collisions;}
                if(stage=="text-issued"){if(text)textIssued++;else collisionTextIssued++;}
                if(stage=="text-completed"){if(text)textCompleted++;else collisionTextCompleted++;}
                // Count every actual call, but avoid one formatted line for
                // every stroke. Per-frame gates/counters are compact records.
                if(stage!="issued" && stage!="completed" && stage!="text-issued" && stage!="text-completed")
                {
                    long key=unchecked((((pathStrokes*397L+collisions)*397+(path?.CaptureTick??-1))*397+preparedCapture)*397+cacheWorker+(text?1:0)),previous;
                    string group=stage=="prepared" || stage.StartsWith("prepare-",StringComparison.Ordinal)?"prepare-gate":stage.StartsWith("draw-",StringComparison.Ordinal)?"draw-gate":stage;
                    key=unchecked(key*397+stage.GetHashCode());
                    if(!presentationKeys.TryGetValue(group,out previous) || key!=previous){presentationKeys[group]=key;log.Offer("presentation",tick,"stage="+stage+";cacheCapture="+cacheCapture+";worker="+cacheWorker+";consumedCapture="+(path?.CaptureTick??-1)+";preparedCapture="+preparedCapture+";preparedWorker="+preparedWorker+";pathStrokes="+pathStrokes+";collision="+collisions+";text="+text+";gpuVisible=unknown");}
                }
            }
            catch(Exception error){Broken(error);}
#endif
        }
        [Conditional("JMR_AIM_LIGHT")]
        internal static void DrawCounts(int pathIssued,int pathCompleted,int otherIssued,int otherCompleted)
        {
#if JMR_AIM_LIGHT
            if(!On)return;try{issued+=pathIssued;completed+=pathCompleted;collisionIssued+=otherIssued;collisionCompleted+=otherCompleted;}catch(Exception error){Broken(error);}
#endif
        }
    }
}
