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
using Terraria.GameInput;
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
                RelayHeight(context,movement,input,world,output);
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
                DashCostPairs(context,movement,input,world,draw,output);
                FixedHookCostPairs(context,movement,input,world,draw,output);
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
        private static void RelayHeight(object context,object movement,Action<Keys[]> input,Action world,string output)
        {
            foreach(int equipment in new[]{0,ItemID.CloudinaBottle,ItemID.SandstorminaBottle})
            {
                var paths=new List<Vector2>[2];int[] second=new int[2];float[] peak=new float[2];
                for(int mode=0;mode<2;mode++)
                {
                    bool assist=mode==1;SetEnabled(movement,0,assist);var p=ResetPlayer();if(equipment!=0)p.armor[3].SetDefaults(equipment);
                    input(None);input(None);world();world();var path=new List<Vector2>();int starts=0;peak[mode]=p.position.Y;
                    using(var log=new StreamWriter(Path.Combine(output,"movement-tail-"+equipment+"-"+(assist?"assist":"manual")+".csv")))
                    {
                        log.WriteLine("step,y,vy,jump,held,starts");
                        for(int k=0;k<180;k++)
                        {
                            // User-operated reference: hold the complete native
                            // jump, release while rising after its timer ends,
                            // then press at the first non-rising boundary.
                            bool opportunity=p.velocity.Y==0 || p.canJumpAgain_Cloud || p.canJumpAgain_Sandstorm;
                            bool held=assist || starts==0 || p.jump>0 || p.velocity.Y*p.gravDir>=0 && opportunity;
                            Keys[] keys=held?Jump:None;input(keys);input(keys);int before=p.jump;world();
                            if(before<=0 && p.jump>0){starts++;if(starts==2)second[mode]=k;}
                            path.Add(p.position);peak[mode]=Math.Min(peak[mode],p.position.Y);
                            log.WriteLine(k+","+p.position.Y+","+p.velocity.Y+","+p.jump+","+held+","+starts);
                            if(starts==3)break;
                        }
                    }
                    Require(starts==3,"Original-world manual/assisted trajectory completes the extra tail and reaches the next ground jump equipment="+equipment+" assist="+assist);
                    paths[mode]=path;
                }
                Require(second[0]==second[1] && paths[0].Count==paths[1].Count,"No extra wait or early relay versus independent native apex input equipment="+equipment);
                for(int i=0;i<paths[0].Count;i++)Require(Vector2.Distance(paths[0][i],paths[1][i])<.003f,"Native manual/assisted complete-height path equipment="+equipment+" step="+i);
                Require(Math.Abs(peak[0]-peak[1])<.003f,"Ordinary and long extra jump retain original full height equipment="+equipment);
                Console.WriteLine("MOVEMENT MAIN TAIL equipment="+equipment+" manualPeak="+peak[0]+" assistedPeak="+peak[1]+" second="+second[1]+" paths="+paths[1].Count);
            }
            // One real cloud -> wing/boot combination exercises the complete
            // Update consumer sequence; source conversion keeps running while
            // its jump input is masked. No resources are restored by the test.
            SetEnabled(movement,0,true);var flight=ResetPlayer();flight.armor[3].SetDefaults(ItemID.CloudinaBottle);flight.armor[4].SetDefaults(ItemID.AngelWings);flight.armor[5].SetDefaults(ItemID.RocketBoots);
            input(None);input(None);world();world();int last=0,coast=0;bool spent=false,converted=false;
            for(int k=0;k<150;k++)
            {
                int before=flight.jump,rocket=flight.rocketTime;float wing=flight.wingTime;
                input(Jump);input(Jump);world();
                if(rocket>0 && flight.rocketTime==0 && flight.wingTime>=wing+rocket*6-1)converted=true;
                if(before==1)
                {last++;Require(flight.jump==0 && flight.velocity.Y<0 && flight.wingTime>=wing && flight.rocketDelay==0,"Last complete native tick cannot ignite later wing/boot consumers.");}
                var state=(PredictionPlayerMotion)context.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource",true).GetMethod("ReadPlayer",F).Invoke(null,new object[]{flight});
                if(state.AssistedJumpPhase==MovementJumpPhase.Coast){coast++;Require(flight.wingTime>=wing && flight.rocketDelay==0,"Every owned natural tail preserves later native flight start.");}
                if(flight.wingTime<wing){spent=true;break;}
            }
            Require(last>=2 && coast>2 && converted && spent && !flight.canJumpAgain_Cloud,"Actual cloud then wing/boot handoff retains tails and native fuel conversion before eventual flight.");
            Console.WriteLine("PASS MOVEMENT MAIN TAIL native manual-height oracles; cloud-wing-boots lastTicks="+last+" coast="+coast+" nativeConversion="+converted+" nativeFlight="+spent);
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
            var host=context.GetType().Assembly;var motor=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePlayerMotion",true);
            var actor=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeActorContext",true);
            var wire=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true);object values=wire.GetField("Players",F).GetValue(null);
            byte[] sealedPlayer;
            using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes))
            {values.GetType().GetMethod("Write",F).Invoke(values,new object[]{writer,p});actor.GetMethod("WritePlayer",F).Invoke(null,new object[]{writer,p});motor.GetMethod("Write",F).Invoke(null,new object[]{writer,p});writer.Flush();sealedPlayer=bytes.ToArray();}
            var actualStates=new PredictionPlayerMotion[180];
            int starts=0;float maximum=0;
            using(var log=new StreamWriter(Path.Combine(output,"movement-jump-prediction.csv")))
            {
                log.WriteLine("step,nativeY,predictedY,nativeVy,predictedVy,jump,error");
                for(int k=0;k<180;k++)
                {
                    terrain.Reset();PredictionStop stop;Require(PlayerMotionContinuation.Advance(ref predicted,environment,terrain,out stop),"Continuous ordinary forecast available: "+stop);
                    input(Jump);world();if(p.justJumped)starts++;
                    actualStates[k]=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});
                    float error=Vector2.Distance(p.position,new Vector2(predicted.X,predicted.Y));maximum=Math.Max(maximum,error);
                    log.WriteLine(k+","+p.position.Y+","+predicted.Y+","+p.velocity.Y+","+predicted.Vy+","+p.jump+","+error);
                    Require(error<.003f && Math.Abs(p.velocity.Y-predicted.Vy)<.003f && p.jump==predicted.Jump && actualStates[k].AssistedJumpPhase==predicted.AssistedJumpPhase,"Before-action forecast matches actual future held ground jump/phase step="+k+" error="+error);
                }
            }
            Require(starts>=2,"180 actual original updates include repeated landing/jump events.");
            // The second ordinary consumer advances only its sealed copy. The
            // independent original-world answers above were recorded before
            // entering any restored profile scope; no live owner is consulted.
            var clone=new Player();object receiver=Activator.CreateInstance(motor,true);Main.player[Main.myPlayer]=clone;
            try
            {
                using((IDisposable)Call(receiver,"UseRestoredProfiles"))
                {
                    using(var reader=new BinaryReader(new MemoryStream(sealedPlayer,false)))
                    {values.GetType().GetMethod("Read",F).Invoke(values,new object[]{reader,clone});actor.GetMethod("ReadPlayer",F).Invoke(null,new object[]{reader,clone});Call(receiver,"Read",reader,Main.myPlayer);}
                    int coast=0;
                    for(int k=0;k<actualStates.Length;k++)
                    {
                        Call(receiver,"Advance");var expected=actualStates[k];byte[] profile;
                        using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes)){motor.GetMethod("Write",F).Invoke(null,new object[]{writer,clone});writer.Flush();profile=bytes.ToArray();}
                        if(profile.Last()==(byte)MovementJumpPhase.Coast)coast++;
                        Require(Math.Abs(clone.position.Y-expected.Y)<.003f && Math.Abs(clone.velocity.Y-expected.Vy)<.003f && clone.jump==expected.Jump && profile.Last()==(byte)expected.AssistedJumpPhase,"Sealed native ordinary copy advances held/coast/landing/restart step="+k);
                    }
                    Require(coast>0,"Native profile phase persists over multiple tail steps.");
                }
            }
            finally{Main.player[Main.myPlayer]=p;}
            var old=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});SetEnabled(movement,0,false);var disabled=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});
            Require(!disabled.AssistedJump && !old.SameSample(disabled),"Disable retires the old forecast premise before reuse.");
            Console.WriteLine("MOVEMENT PREDICTION native-updates=180 repeated-ground-starts="+starts+" maximum-error="+maximum);
            // A real accessory must not erase the already-started ordinary
            // span merely because its NEXT automatic edge is unmodelled.
            SetEnabled(movement,0,true);p=ResetPlayer();p.armor[3].SetDefaults(ItemID.CloudinaBottle);
            input(None);input(None);world();world();input(Jump);input(Jump);world();
            predicted=(PredictionPlayerMotion)read.Invoke(null,new object[]{p});
            Require(predicted.Complex && predicted.AssistedOrdinarySpan && predicted.Jump>1,"Real native equipment publishes a conditional handoff and a useful ordinary prefix.");
            int prefix=0;maximum=0;
            while(predicted.Jump>1 && predicted.Vy!=0)
            {
                terrain.Reset();PredictionStop stop;
                Require(PlayerMotionContinuation.Advance(ref predicted,environment,terrain,out stop),"Equipped near-player forecast remains available: "+stop);
                input(Jump);world();prefix++;
                float error=Vector2.Distance(p.position,new Vector2(predicted.X,predicted.Y));maximum=Math.Max(maximum,error);
                Require(error<.003f && Math.Abs(p.velocity.Y-predicted.Vy)<.003f && p.jump==predicted.Jump && p.canJumpAgain_Cloud,
                    "Equipped ordinary prefix matches full original Update without spending the future cloud jump step="+prefix+" error="+error);
            }
            Require(prefix>0 && !PlayerMotionContinuation.OrdinaryStep(predicted),"Supported equipped prefix ends before claiming the future equipment edge.");
            Console.WriteLine("MOVEMENT PREDICTION equipped-cloud-native-prefix="+prefix+" maximum-error="+maximum+" future-handoff=conditional");
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
                        if(!manualRelease && p.jump==0 && !p.releaseJump && p.velocity.Y*p.gravDir>=0 && edge){held=false;manualRelease=true;}
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
                else Require(wingSpent>0 && observed.WingBoundary>0,"Native jumps retain their complete tails before later original wings consume fuel.");
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
        private sealed class CostBlock
        {
            internal double[] Update,Frame;
            internal long[] Bytes;
            internal int[] Delay,Rocket,Shields;
            internal float[] Vx,X;
            internal int Starts,WarmStarts,Gc0,Gc1,Gc2;
        }
        private static CostBlock CostSample(object movement,Action<Keys[]> input,Action world,Action draw,bool dash,bool assist,int count)
        {
            const int warm=32;
            SetEnabled(movement,0,false);SetEnabled(movement,1,assist);SetEnabled(movement,2,false);SettlePreferences(movement);
            Main.dayTime=true;Main.time=24000;
            foreach(var dust in Main.dust)if(dust!=null)dust.active=false;
            foreach(var gore in Main.gore)if(gore!=null)gore.active=false;
            var p=ResetPlayer();if(dash)p.armor[3].SetDefaults(ItemID.Tabi);
            input(None);input(None);world();world();
            var right=new[]{Keys.D};var nativeDash=new[]{Keys.D,Keys.F19};
            var result=new CostBlock{Update=new double[count],Frame=new double[count],Bytes=new long[count],Delay=new int[count],Rocket=new int[count],Shields=new int[count],Vx=new float[count],X=new float[count]};
            int gc0=0,gc1=0,gc2=0;
            for(int k=-warm;k<count;k++)
            {
                if(k==0){gc0=GC.CollectionCount(0);gc1=GC.CollectionCount(1);gc2=GC.CollectionCount(2);}
                // The manual comparator uses a real mapped native Dash key,
                // never a velocity/cooldown/resource write. Two identical
                // native input opportunities expose the final key state in
                // BOTH arms; cadence is selected before the measured chain.
                Keys[] keys=!dash?None:!assist && p.dashDelay==0 && p.releaseDash?nativeDash:right;
                int beforeDelay=p.dashDelay;
                long allocated=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize,start=Stopwatch.GetTimestamp();
                input(keys);input(keys);long before=Stopwatch.GetTimestamp();world();long after=Stopwatch.GetTimestamp();draw();long end=Stopwatch.GetTimestamp();
                long bytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-allocated;
                bool launched=beforeDelay==0 && p.dashDelay<0;
                if(k<0){if(launched)result.WarmStarts++;continue;}
                if(launched)result.Starts++;
                result.Update[k]=(after-before)*1000.0/Stopwatch.Frequency;result.Frame[k]=(end-start)*1000.0/Stopwatch.Frequency;result.Bytes[k]=bytes;
                result.Delay[k]=p.dashDelay;result.Rocket[k]=p.rocketTime;result.Shields[k]=p.solarShields;result.Vx[k]=p.velocity.X;result.X[k]=p.position.X;
                Require(!p.dead && p.position.X<Main.maxTilesX*16-64,"Owned paired scene remains alive inside native world");
            }
            result.Gc0=GC.CollectionCount(0)-gc0;result.Gc1=GC.CollectionCount(1)-gc1;result.Gc2=GC.CollectionCount(2)-gc2;
            return result;
        }
        private static void WriteCost(StreamWriter log,string label,int pair,int order,CostBlock block)
        {
            for(int k=0;k<block.Update.Length;k++)log.WriteLine(label+","+pair+","+order+","+k+","+block.Update[k]+","+block.Frame[k]+","+block.Bytes[k]+","+block.Delay[k]+","+block.Vx[k]+","+block.X[k]+","+block.Rocket[k]+","+block.Shields[k]);
            Console.WriteLine("MOVEMENT PAIRED COST label="+label+" pair="+pair+" order="+order+" warm=32 measured="+block.Update.Length+" warmLaunches="+block.WarmStarts+" measuredLaunches="+block.Starts+" cooldownRange="+block.Delay.Min()+":"+block.Delay.Max()+" rocketRange="+block.Rocket.Min()+":"+block.Rocket.Max()+" solarRange="+block.Shields.Min()+":"+block.Shields.Max()+" allocated="+block.Bytes.Sum()+" gc="+block.Gc0+","+block.Gc1+","+block.Gc2+" update="+Distribution(block.Update)+" two-native-input-world-draw="+Distribution(block.Frame));
        }
        private static void DashCostPairs(object context,object movement,Action<Keys[]> input,Action world,Action draw,string output)
        {
            var configuration=PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard];var binding=configuration.KeyStatus["Dash"];
            object oldMode=Get(movement,"DashMode");configuration.KeyStatus["Dash"]=new List<string>{Keys.F19.ToString()};
            try
            {
                Call(movement,"SetDashMode",JueMingR.Features.Movement.ContinuousDashMode.Hold);
                using(var log=new StreamWriter(Path.Combine(output,"movement-dash-pairs.csv")))
                {
                    log.WriteLine("label,pair,order,step,updateMs,twoInputWorldDrawMs,allocated,dashDelay,vx,x,rocketTime,solarShields");
                    for(int pair=0;pair<4;pair++)
                    {
                        var blocks=new CostBlock[2];
                        for(int order=0;order<2;order++)
                        {
                            int index=pair%2==0?order:1-order;
                            blocks[index]=CostSample(movement,input,world,draw,true,index==1,240);
                            WriteCost(log,index==1?"assist-dash":"manual-native-dash",pair,order,blocks[index]);
                        }
                        var manual=blocks[0];var assisted=blocks[1];
                        Require(manual.Starts>=2 && manual.Starts==assisted.Starts && manual.WarmStarts==assisted.WarmStarts,"Actual repeated native dash launch denominators match");
                        Require(manual.Delay.SequenceEqual(assisted.Delay) && manual.Rocket.SequenceEqual(assisted.Rocket) && manual.Shields.SequenceEqual(assisted.Shields),"Real original cooldown/resource traces match at each measured update");
                        for(int k=0;k<manual.Vx.Length;k++)Require(Math.Abs(manual.Vx[k]-assisted.Vx[k])<.003f && Math.Abs(manual.X[k]-assisted.X[k])<.003f,"Equivalent mapped native/assisted movement trace step="+k);
                        Console.WriteLine("MOVEMENT DASH PAIR pair="+pair+" actualLaunches="+manual.Starts+" cooldownAndResourceTraceEqual=true update-on-minus-manual="+(assisted.Update.Average()-manual.Update.Average())+" full-on-minus-manual="+(assisted.Frame.Average()-manual.Frame.Average())+" status=MEASURED-not-universal-performance-acceptance");
                    }
                }
            }
            finally{configuration.KeyStatus["Dash"]=binding;SetEnabled(movement,1,false);Call(movement,"SetDashMode",oldMode);input(None);input(None);}
        }
        private static void FixedHookCostPairs(object context,object movement,Action<Keys[]> input,Action world,Action draw,string output)
        {
            var assembly=movement.GetType().Assembly;var shared=assembly.GetType("JueMingR.TerrariaHost.Movement.MovementHooks");var teleport=assembly.GetType("JueMingR.TerrariaHost.Movement.TeleportHooks");
            var dash=Get(movement,"DashHost");object permission=teleport.GetField("canCorrect",F).GetValue(null),report=teleport.GetField("reportFailure",F).GetValue(null);bool installed=true;
            Action<bool> hooks=enabled=>
            {
                if(enabled==installed)return;
                if(enabled)
                {
                    shared.GetMethod("Install",F).Invoke(null,new[]{movement});Call(dash,"Install");
                    Require((bool)teleport.GetMethod("TryInstall",F).Invoke(null,new[]{permission,report}),"Restore exact owned teleport permission and error delegate");
                }
                else{Call(movement,"OnSessionEnded");teleport.GetMethod("Uninstall",F).Invoke(null,null);Call(dash,"Uninstall");shared.GetMethod("Uninstall",F).Invoke(null,new[]{movement});}
                installed=enabled;
            };
            try
            {
                SetEnabled(movement,0,false);SetEnabled(movement,1,false);SetEnabled(movement,2,false);SettlePreferences(movement);
                using(var log=new StreamWriter(Path.Combine(output,"movement-fixed-hook-pairs.csv")))
                {
                    log.WriteLine("label,pair,order,step,updateMs,twoInputWorldDrawMs,allocated,dashDelay,vx,x,rocketTime,solarShields");
                    for(int pair=0;pair<4;pair++)for(int order=0;order<2;order++)
                    {
                        bool enabled=(pair%2==0?order:1-order)==1;hooks(enabled);
                        var patch=Harmony.GetPatchInfo(typeof(Player).GetMethod("Update",F,null,new[]{typeof(int)},null));
                        Require((patch!=null && patch.Owners.Contains("JueMingR.Movement.Input1458"))==enabled,"Same-byte installed/removed hook arm uses actual owned patch state");
                        var block=CostSample(movement,input,world,draw,false,false,96);WriteCost(log,enabled?"all-off-hooks-installed":"all-off-movement-hooks-removed",pair,order,block);
                        Require(block.Starts==0,"Both fixed-hook arms are actually idle");
                    }
                }
                Console.WriteLine("MOVEMENT FIXED HOOK COST same loaded assemblies; only three owned Movement hook owners toggled; Session/input/prefs and all other hooks remain; NOT a rebuilt BASE comparison");
            }
            finally{hooks(true);}
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
