using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Runs on the existing initialized Main/device/content specialist. Input is
    // the real original mapping stage; world invokes its unchanged native body.
    // The sole population sink keeps the declared zero-NPC performance scene
    // fixed. Neither Player.Update nor its motion/resources are replaced.
    internal static class NativeMovementOriginalChecks
    {
        private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static readonly Keys[] None=new Keys[0],Jump=new[]{Keys.Space},JumpRight=new[]{Keys.Space,Keys.D};
        private sealed class Observation
        {
            internal Player Player;
            internal int PreJump,Starts,Ground,Extra,WingBoundary;
            internal readonly int[] Families=new int[5];
        }
        private static Observation current;
        private static Player throwPlayer;
        internal static void Run(Main game,object context,Action<Keys[]> input,Action draw,string output,Action<int,int,bool> pointer=null)
        {
            var movement=Get(context,"Movement");
            var world=(Action)Delegate.CreateDelegate(typeof(Action),game,typeof(Main).GetMethod("DoUpdateInWorld_Inner",F));
            var fences=new Harmony("JueMingR.Tests.Movement.Original");
            fences.Patch(typeof(NPC).GetMethod("SpawnNPC",F),prefix:new HarmonyMethod(typeof(NativeMovementOriginalChecks),nameof(NoSpawn)));
            fences.Patch(typeof(Player).GetMethod("JumpMovement",F),prefix:new HarmonyMethod(typeof(NativeMovementOriginalChecks),nameof(BeforeJump)),postfix:new HarmonyMethod(typeof(NativeMovementOriginalChecks),nameof(AfterJump)));
            fences.Patch(typeof(Player).GetMethod("JumpMovement",F),postfix:new HarmonyMethod(typeof(NativeMovementOriginalChecks),nameof(ThrowAfterJump)){priority=Priority.Last});
            var jumpInfo=Harmony.GetPatchInfo(typeof(Player).GetMethod("JumpMovement",F));
            Require(jumpInfo!=null && jumpInfo.Owners.Contains("JueMingR.Movement.Input1458"),"Real JumpMovement production hook installed beside test observation.");
            Require(Harmony.GetPatchInfo(typeof(Main).GetMethod("DoUpdateInWorld_Inner",F)).Owners.Contains("JueMingR.Movement.Input1458"),"Real Main world entry owns the Player.Update scope.");
            Main.autoSave=false;Main.gamePaused=false;Main.windSpeedCurrent=Main.windSpeedTarget=0;Main.GameZoomTarget=1;
            foreach(var n in Main.npc)n.active=false;foreach(var p in Main.projectile)p.active=false;foreach(var item in Main.item)item.TurnToAir();
            foreach(var p in Main.player)if(p!=null)p.active=false;
            int oldMode=Main.GameMode;bool ignoredErrors=Main.ignoreErrors;Main.ignoreErrors=false;
            try
            {
                // UpdateRuntime owns session admission. The owned scene must
                // already have its active local player before asking that gate.
                ResetPlayer();
                NativeQuickItemChecks.Until(()=>{Call(context,"UpdateRuntime");return (bool)Get(movement,"ControlsEnabled");});
                SetEnabled(movement,0,true);input(None);input(None);input(Jump);input(Jump);
                Main.LocalPlayer.releaseJump=false;
                Main.LocalPlayer.Update(Main.myPlayer);
                Require(!Main.LocalPlayer.justJumped,"Isolated Player.Update outside real Main world scope cannot receive an auxiliary edge.");
                world();Require(Main.LocalPlayer.justJumped && Main.LocalPlayer.jump>0,"Actual Main world -> mapped Player.Update -> original JumpMovement starts held jump.");
                ResourceAndCleanup(context,movement,input,world);
                MountedSources(context,movement,input,world);
                Prediction(context,movement,input,world,output);
                // Each pair uses identical initialized game, world, native
                // lighting/zoom, no NPCs, and the same drawing callback. Order
                // reverses on the second round to expose warmup/order effects.
                var labels=new[]{"off-idle","on-idle","manual-ground","assist-ground","manual-complex","assist-complex"};
                for(int round=0;round<2;round++)
                    foreach(string label in round==0?labels:labels.Reverse())
                        Measure(context,movement,input,world,draw,output,label,round);
                Measure(context,movement,input,world,draw,output,"carpet",0);
                Measure(context,movement,input,world,draw,output,"coexist",0);
                IdlePairs(context,movement,input,world,draw,output);
                Require(!Main.LocalPlayer.autoJump,"Auxiliary jump never leaves an equipment autoJump attribute behind.");
                NativeMovementDashChecks.Mapped(context,input,world);
                NativeMovementUiChecks.Run(game,context,input,draw,output,pointer);
                Console.WriteLine("PASS MOVEMENT ORIGINAL real Main/input/Player.Update/Jump/flight consumers; automatic owned-scene evidence, not owner acceptance or multiplayer");
            }
            finally
            {
                current=null;throwPlayer=null;SetEnabled(movement,0,false);SetEnabled(movement,1,false);SetEnabled(movement,2,false);
                input(None);input(None);fences.UnpatchAll(fences.Id);Main.GameMode=oldMode;Main.ignoreErrors=ignoredErrors;
            }
        }
        private static void SetEnabled(object movement,int feature,bool enabled)
        {if((bool)Call(movement,"IsEnabled",feature)!=enabled)Require((bool)Call(movement,"SetEnabled",feature,enabled),"Movement preference command feature="+feature);}
        private static void SettlePreferences(object movement)
        {
            var preferences=Get(movement,"preferences");
            NativeQuickItemChecks.Until(()=>
            {
                string status=Get(Get(preferences,"Snapshot"),"Status").ToString();
                if(status=="Loading" || status=="Pending")return false;
                Require(status=="Saved" || status=="Missing","Owned measurement preferences must settle successfully, actual="+status);
                return true;
            });
        }
        private static Player ResetPlayer(bool complex=false,bool carpet=false)
        {
            var p=new Player{active=true,whoAmI=Main.myPlayer,name="MOVEMENT owned",statLife=400,statLifeMax=400,statLifeMax2=400,immune=true,immuneTime=100000,creativeGodMode=true};
            p.position=new Vector2(6400,225*16-p.height);p.fallStart=p.fallStart2=(int)(p.position.Y/16);p.releaseJump=false;
            Main.player[Main.myPlayer]=p;
            if(Main.ActivePlayerFileData!=null)Main.ActivePlayerFileData.Player=p;
            if(complex)
            {
                int[] items={ItemID.BundleofBalloons,ItemID.FartinaJar,ItemID.TsunamiInABottle,carpet?ItemID.FlyingCarpet:ItemID.AngelWings,ItemID.RocketBoots};
                for(int i=0;i<items.Length;i++)p.armor[3+i].SetDefaults(items[i]);
            }
            return p;
        }
        private static void ResourceAndCleanup(object context,object movement,Action<Keys[]> input,Action world)
        {
            var p=ResetPlayer(true);input(None);input(None);world();world();
            p.wingTime=0;p.rocketTime=0;p.releaseJump=false;
            input(Jump);input(Jump);world();
            Require(p.justJumped && p.jump>0 && p.wingTime==p.wingTimeMax && p.rocketTime==p.rocketTimeMax && !p.autoJump,
                "Exhausted ground resources refill inside original later Update consumers; temporary autoJump closes afterwards.");
            // This remote original consumer has no local scope and must not
            // revoke a published local intention merely by sharing the hook.
            var source=Get(Get(context,"CombatObservation"),"Prediction");var read=source.GetType().GetMethod("ReadPlayer",F);
            var before=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});
            var remote=new Player{active=true,whoAmI=1,controlJump=true};remote.JumpMovement();
            var after=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});
            Require(before.AssistedJump && after.AssistedJump && before.AssistedJumpVersion==after.AssistedJumpVersion,
                "Remote/isolated JumpMovement does not cancel the real local published intent.");
            p=ResetPlayer();p.armor[3].SetDefaults(ItemID.FrogLeg);input(None);input(None);world();world();input(Jump);input(Jump);world();
            Require(p.autoJump && p.jumpSpeedBoost>0,"Original frog accessory is really equipped, not a helper attribute.");
            SetEnabled(movement,0,false);world();Require(p.autoJump,"Disabling helper preserves native frog autoJump.");
            SetEnabled(movement,0,true);p=ResetPlayer();input(None);input(None);world();world();input(Jump);input(Jump);
            throwPlayer=p;bool propagated=false;
            try{world();}catch(InvalidOperationException e){propagated=e.Message=="movement-owned-post-consumer-failure";}
            finally{throwPlayer=null;}
            Require(propagated && p.jump>0 && p.velocity.Y<0 && !p.autoJump && !p.releaseJump,
                "Real Player.Update finalizer preserves original exception and consumed motion while withdrawing only its temporary booleans.");
            Console.WriteLine("PASS MOVEMENT original exhausted-ground-refill/native-frog/remote-intent/post-consumer-exception scope cleanup");
        }
        private static void MountedSources(object context,object movement,Action<Keys[]> input,Action world)
        {
            SetEnabled(movement,0,true);
            var source=Get(Get(context,"CombatObservation"),"Prediction");var read=source.GetType().GetMethod("ReadPlayer",F);
            int[] mounts={MountID.Unicorn,MountID.Santank,MountID.WallOfFleshGoat,MountID.Basilisk};
            string[] families={"Unicorn","Santank","WallOfFleshGoat","Basilisk"};
            for(int m=0;m<mounts.Length;m++)
            {
                var p=ResetPlayer();p.mount.SetMount(mounts[m],p);p.position.Y=225*16-p.height;p.velocity=Vector2.Zero;p.fallStart=p.fallStart2=(int)(p.position.Y/16);
                input(None);input(None);world();world();
                Require((bool)typeof(Player).GetField("hasJumpOption_"+families[m]).GetValue(p),"Actual full Update mounts produce "+families[m]);
                var observation=new Observation{Player=p};current=observation;input(Jump);input(Jump);
                for(int k=0;k<150;k++){input(Jump);world();}
                current=null;
                Require(p.mount.Active && p.mount.Type==mounts[m] && observation.Ground>0 && observation.Extra>0,
                    "Actual full Update mapped mount ground + extra handoff "+families[m]);
                var sampled=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});
                Require(sampled.AssistedJump && sampled.Complex,"Mounted assistance remains an explicit conditional forecast, never ordinary ground quality.");
                Console.WriteLine("MOVEMENT MAIN MOUNT type="+mounts[m]+" family="+families[m]+" starts="+observation.Starts+" ground="+observation.Ground+" extra="+observation.Extra+" actual-effects-and-mapped-update=true");
            }
        }
        private static void Prediction(object context,object movement,Action<Keys[]> input,Action world,string output)
        {
            SetEnabled(movement,0,true);var p=ResetPlayer();input(None);input(None);input(Jump);input(Jump);world();
            var source=Get(Get(context,"CombatObservation"),"Prediction");var read=source.GetType().GetMethod("ReadPlayer",F);var terrain=(IPredictionTerrain)Get(source,"Terrain");
            var predicted=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});Require(predicted.AssistedJump && !predicted.Complex,"Ordinary continuous intent is an explicit forecast premise.");
            var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface};
            int starts=0;float maximum=0;
            using(var log=new StreamWriter(Path.Combine(output,"movement-jump-prediction.csv")))
            {
                log.WriteLine("step,nativeY,predictedY,nativeVy,predictedVy,jump,error");
                for(int k=0;k<180;k++)
                {
                    terrain.Reset();PredictionStop stop;Require(PlayerMotionContinuation.Advance(ref predicted,environment,terrain,out stop),"Continuous ordinary forecast available: "+stop);
                    input(Jump);world();if(p.justJumped)starts++;
                    float error=Vector2.Distance(p.position,new Vector2(predicted.X,predicted.Y));maximum=Math.Max(maximum,error);
                    log.WriteLine(k+","+p.position.Y+","+predicted.Y+","+p.velocity.Y+","+predicted.Vy+","+p.jump+","+error);
                    Require(error<.003f && Math.Abs(p.velocity.Y-predicted.Vy)<.003f && p.jump==predicted.Jump,"Before-action forecast matches actual future held ground jump step="+k+" error="+error);
                }
            }
            Require(starts>=2,"180 actual original updates include repeated landing/jump events.");
            var old=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});SetEnabled(movement,0,false);var disabled=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});
            Require(!disabled.AssistedJump && !old.SameSample(disabled),"Disable retires the old forecast premise before reuse.");
            Console.WriteLine("MOVEMENT PREDICTION native-updates=180 repeated-ground-starts="+starts+" maximum-error="+maximum);
        }
        private static void Measure(object context,object movement,Action<Keys[]> input,Action world,Action draw,string output,string label,int round)
        {
            bool complex=label.Contains("complex") || label=="carpet",manual=label.StartsWith("manual",StringComparison.Ordinal),idle=label.EndsWith("idle",StringComparison.Ordinal),assist=!manual && label!="off-idle";
            SetEnabled(movement,0,assist);SetEnabled(movement,1,label=="coexist");SetEnabled(movement,2,label=="coexist");
            // Configuration saves are one-time setting work, not steady native
            // movement. The 150 ms document quiet window can outlive 40 fast
            // native frames, so explicitly finish it before warming/timing.
            SettlePreferences(movement);Main.dayTime=true;Main.time=24000;
            var p=ResetPlayer(complex,label=="carpet");if(label=="coexist")p.armor[3].SetDefaults(3097);var observed=new Observation{Player=p};current=observed;
            input(None);input(None);world();world();
            int count=complex?420:240,warm=40;var updates=new double[count-warm];var frames=new double[count-warm];long allocation=0;int collections0=GC.CollectionCount(0),collections1=GC.CollectionCount(1),collections2=GC.CollectionCount(2),carpetTicks=0,dashTicks=0;float wingSpent=0;int rocketSpent=0;
            long firstSpike=-1;double peak=0;bool manualRelease=false;var clock=new Stopwatch();
            using(var log=new StreamWriter(Path.Combine(output,"movement-"+label+"-"+round+".csv")))
            {
                log.WriteLine("step,updateMs,frameMs,allocated,y,vy,jump,wingTime,rocketTime,rocketDelay,carpetTime");
                for(int k=0;k<count;k++)
                {
                    // A manual release occurs only after the held span ended.
                    // It spends one real mapped release before the next press;
                    // this is a comparable native action cadence, not a writer
                    // that forges native release/resource fields.
                    bool held=!idle;
                    if(manual)
                    {
                        bool edge=p.velocity.Y==0 || p.canJumpAgain_Cloud || p.canJumpAgain_Sandstorm || p.canJumpAgain_Blizzard || p.canJumpAgain_Fart || p.canJumpAgain_Sail ||
                            p.rocketBoots>0 && p.rocketTime>0 && !p.rocketRelease && (p.wingsLogic==0 || p.wingTime<=0);
                        if(!manualRelease && p.jump==0 && !p.releaseJump && edge){held=false;manualRelease=true;}
                        else if(manualRelease){held=true;manualRelease=false;}
                    }
                    float oldWing=p.wingTime;int oldRocket=p.rocketTime;
                    Keys[] keys=held?Jump:None;if(label=="coexist" && (k<3 || k>=7))keys=JumpRight;
                    long before=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;clock.Restart();input(keys);long updateStart=Stopwatch.GetTimestamp();world();long updateEnd=Stopwatch.GetTimestamp();draw();clock.Stop();
                    long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-before;
                    double updateMs=(updateEnd-updateStart)*1000.0/Stopwatch.Frequency,frameMs=clock.Elapsed.TotalMilliseconds;
                    if(k>=warm){updates[k-warm]=updateMs;frames[k-warm]=frameMs;allocation+=allocated;if(frameMs>peak){peak=frameMs;firstSpike=k;}}
                    if(p.wingTime<oldWing)wingSpent+=oldWing-p.wingTime;if(p.rocketTime<oldRocket)rocketSpent+=oldRocket-p.rocketTime;if(p.carpetFrame>=0)carpetTicks++;if(p.dashDelay<0)dashTicks++;
                    Require(p.wingTime<=p.wingTimeMax+p.rocketTimeMax*6+.01f && p.rocketTime<=p.rocketTimeMax,"Original resource bounds during "+label);
                    log.WriteLine(k+","+updateMs+","+frameMs+","+allocated+","+p.position.Y+","+p.velocity.Y+","+p.jump+","+p.wingTime+","+p.rocketTime+","+p.rocketDelay+","+p.carpetTime);
                }
            }
            current=null;
            if(label=="coexist")Require(dashTicks>0 && observed.Ground>1,"Three-enabled scene includes real repeated native dash and ground jumps.");
            if(!idle)Require(observed.Starts>=2,"Actual original action denominator "+label+" starts="+observed.Starts);
            if(complex)
            {
                Require(observed.Families.All(n=>n>0),"All five actual equipment-produced extra-jump families consumed: "+label+" "+String.Join(",",observed.Families));
                if(label=="carpet")Require(rocketSpent>0 && carpetTicks>0,"Original rocket then carpet resources really execute.");
                else Require(wingSpent>0 && observed.WingBoundary>0,"Native jump 1 -> 0 joins held wings without postponing remaining extra jumps.");
            }
            Console.WriteLine("MOVEMENT COST label="+label+" round="+round+" warm="+warm+" samples="+updates.Length+" starts="+observed.Starts+" ground="+observed.Ground+" extra="+observed.Extra+" families="+String.Join(",",observed.Families)+" wingSpent="+wingSpent+" rocketDelta="+rocketSpent+" carpetTicks="+carpetTicks+" dashTicks="+dashTicks+" allocated="+allocation+" gc="+(GC.CollectionCount(0)-collections0)+","+(GC.CollectionCount(1)-collections1)+","+(GC.CollectionCount(2)-collections2)+" first-max-frame="+firstSpike+" update="+Distribution(updates)+" full-mapped-world-draw="+Distribution(frames));
            // Sample release after a mapped latency pair; no new intent may be
            // replayed once both original input stages actually saw neutral.
            input(None);input(None);world();world();
            long version;var jump=Get(movement,"JumpHost");object[] args={p,0L};bool intent=(bool)jump.GetType().GetMethod("ReadIntent",F).Invoke(null,args);version=(long)args[1];
            Require(!intent,"Real mapped release stops future auxiliary requests: "+label+" version="+version);
        }
        private static void IdlePairs(object context,object movement,Action<Keys[]> input,Action world,Action draw,string output)
        {
            const int pairs=8,warm=16,count=48;var updateDiff=new double[pairs];var frameDiff=new double[pairs];
            SetEnabled(movement,1,false);SetEnabled(movement,2,false);
            using(var log=new StreamWriter(Path.Combine(output,"movement-idle-pairs-settled.csv")))
            {
                log.WriteLine("pair,order,enabled,step,updateMs,fullFrameMs,allocated");
                for(int pair=0;pair<pairs;pair++)
                {
                    var updateMeans=new double[2];var frameMeans=new double[2];
                    for(int order=0;order<2;order++)
                    {
                        bool enabled=(pair%2==0?order:1-order)==1;int index=enabled?1:0;
                        SetEnabled(movement,0,enabled);SettlePreferences(movement);ResetPlayer();Main.dayTime=true;Main.time=24000;
                        input(None);input(None);
                        for(int i=0;i<warm;i++){input(None);world();draw();}
                        var times=new double[count];var frames=new double[count];var allocatedFrames=new long[count];long allocations=0;int gc=GC.CollectionCount(0);
                        for(int i=0;i<count;i++)
                        {
                            long allocation=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize,start=Stopwatch.GetTimestamp();input(None);
                            long before=Stopwatch.GetTimestamp();world();long after=Stopwatch.GetTimestamp();draw();long end=Stopwatch.GetTimestamp();
                            long bytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocation;
                            times[i]=(after-before)*1000.0/Stopwatch.Frequency;frames[i]=(end-start)*1000.0/Stopwatch.Frequency;allocatedFrames[i]=bytes;allocations+=bytes;
                        }
                        int measuredGc=GC.CollectionCount(0)-gc;
                        for(int i=0;i<count;i++)log.WriteLine(pair+","+order+","+enabled+","+i+","+times[i]+","+frames[i]+","+allocatedFrames[i]);
                        updateMeans[index]=times.Average();frameMeans[index]=frames.Average();
                        Console.WriteLine("MOVEMENT SETTLED IDLE pair="+pair+" order="+order+" enabled="+enabled+" warm="+warm+" n="+count+" allocated="+allocations+" gc0="+measuredGc+" update="+Distribution(times)+" full-mapped-world-draw="+Distribution(frames));
                    }
                    updateDiff[pair]=updateMeans[1]-updateMeans[0];frameDiff[pair]=frameMeans[1]-frameMeans[0];
                }
            }
            // Keep every pair and sign. No fixed percentage threshold or a
            // single favorable mean can turn these scene data into a PASS.
            Console.WriteLine("MOVEMENT SETTLED IDLE on-minus-off update-ms="+String.Join(",",updateDiff)+" full-frame-ms="+String.Join(",",frameDiff)+" status=MEASURED-not-universal-performance-acceptance");
        }
        private static string Distribution(double[] values)
        {var sorted=values.OrderBy(x=>x).ToArray();return "mean:"+sorted.Average()+"/p50:"+sorted[sorted.Length/2]+"/p95:"+sorted[(int)((sorted.Length-1)*.95)]+"/max:"+sorted[sorted.Length-1];}
        private static bool NoSpawn(){return false;}
        private static void ThrowAfterJump(Player __instance)
        {if(ReferenceEquals(throwPlayer,__instance)){throwPlayer=null;throw new InvalidOperationException("movement-owned-post-consumer-failure");}}
        private static void BeforeJump(Player __instance)
        {if(current!=null && ReferenceEquals(current.Player,__instance))current.PreJump=__instance.jump;}
        private static void AfterJump(Player __instance)
        {
            var o=current;var p=__instance;if(o==null || !ReferenceEquals(o.Player,p))return;
            if(o.PreJump==1 && p.jump==0 && p.wingsLogic>0 && p.wingTime>0)o.WingBoundary++;
            if(o.PreJump>0 || p.jump<=0)return;
            o.Starts++;if(p.justJumped)o.Ground++;else{o.Extra++;if(p.isPerformingJump_Sandstorm)o.Families[0]++;else if(p.isPerformingJump_Blizzard)o.Families[1]++;else if(p.isPerformingJump_Fart)o.Families[2]++;else if(p.isPerformingJump_Sail)o.Families[3]++;else if(p.isPerformingJump_Cloud)o.Families[4]++;}
        }
    }
}
