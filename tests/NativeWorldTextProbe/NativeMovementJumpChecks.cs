using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual locked .8 consumers in the existing isolated, silent fixture.
    // Stage checks do not claim Main/physical-input/rendering or owner play.
    internal static class NativeMovementJumpChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            var host=context.GetType().Assembly;
            var type=host.GetType("JueMingR.TerrariaHost.Movement.MovementJumpHost",true);
            object jump=Activator.CreateInstance(type,true);Player saved=Main.LocalPlayer;float speed=Player.jumpSpeed;int height=Player.jumpHeight;int cases=0;
            NativeCombatLiveContextChecks.InitializeMount();NativeCombatWorkerAssetChecks.Initialize();
            for(int i=0;i<Main.gore.Length;i++)if(Main.gore[i]==null)Main.gore[i]=new Gore();
            try
            {
                Player.jumpSpeed=5.01f;Player.jumpHeight=15;
                foreach(string family in new[]{"Sandstorm","Blizzard","Fart","Sail","Cloud","Unicorn","Santank","WallOfFleshGoat","Basilisk"})
                {
                    var p=Local();p.velocity.Y=-1;p.releaseJump=false;
                    typeof(Player).GetField("canJumpAgain_"+family).SetValue(p,true);
                    typeof(Player).GetField("hasJumpOption_"+family).SetValue(p,true);
                    Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);
                    Require(!(bool)typeof(Player).GetField("canJumpAgain_"+family).GetValue(p),"Original consumes one "+family+" jump.");
                    int expected=family=="Sandstorm"?45:family=="Blizzard"?22:family=="Fart" || family=="Unicorn" || family=="Santank" || family=="WallOfFleshGoat"?30:family=="Sail"?18:11;
                    Require(p.jump==expected && p.velocity.Y==-5.01f && !p.releaseJump,"Original family duration/velocity/release "+family+" actual="+p.jump);
                    Call(jump,"EndUpdate",p);Require(!p.autoJump,"Scope contributes no accessory attributes to extra jump.");
                    int started=p.jump;
                    for(int k=0;k<started;k++){Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);Call(jump,"EndUpdate",p);Require(p.jump==started-k-1,"Held native span cannot be cut short: "+family);}
                    Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"EndUpdate",p);Require(p.jump==0 && !p.releaseJump,"Exhausted family is not replenished in air: "+family);
                }
                foreach(float gravity in new[]{1f,-1f})foreach(bool sliding in new[]{false,true})foreach(bool wet in new[]{false,true})
                {
                    var p=Local();p.gravDir=gravity;p.velocity.Y=wet?1:0;p.sliding=sliding;p.wet=wet;p.accFlipper=wet;p.slideDir=1;
                    Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);
                    Require(p.jump==15 && p.velocity.Y==-5.01f*gravity,"Native ground/wall/water jump follows gravity.");
                    if(sliding)Require(!p.autoJump,"Original wall consumer may clear autoJump.");
                    Call(jump,"EndUpdate",p);Require(!p.autoJump && !p.releaseJump,"Normal completion clears only temporary contribution.");
                }
                foreach(bool nativeAuto in new[]{false,true})
                {
                    var p=Local();p.autoJump=nativeAuto;Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);Call(jump,"EndUpdate",p);
                    Require(p.autoJump==nativeAuto && p.jump==15 && p.velocity.Y==-5.01f,"Native autoJump survives helper cleanup; no jump-speed boost.");
                }
                var down=Local();down.controlDown=true;Begin(jump,down,++cases);Call(jump,"BeforeJump",down,true,true,true,true,1L);down.JumpMovement();Call(jump,"EndUpdate",down);Require(down.jump==0 && down.controlDown && down.controlJump,"Down+Jump is untouched.");
                var held=Local();held.controlJump=false;Begin(jump,held,++cases);Before(jump,held);Require(!held.releaseJump && !held.autoJump,"Old held sample cannot re-create current cleared native Jump.");Call(jump,"EndUpdate",held);
                var isolated=Local();Before(jump,isolated);Require(!isolated.releaseJump,"Isolated same-name invocation has no update scope.");
                var clone=new Player{whoAmI=Main.myPlayer,active=true,controlJump=true};Begin(jump,clone,++cases);Before(jump,clone);Require(!clone.releaseJump && !clone.autoJump,"Same-number prediction clone never acquires real Player scope.");
                var local=Local();Begin(jump,local,++cases);Require((bool)Call(jump,"ReserveFallProtection",local),"Higher owner reserves before consumer.");Before(jump,local);Call(jump,"Cancel");Require(!local.releaseJump && !local.autoJump,"Disabled lower feature cannot write or undo higher reservation.");Call(jump,"EndUpdate",local);
                foreach(int mount in new[]{0,2,5,7,8,12,23,44,48,49,50,54,56,61,1,3,6,9,10,14,17,43,46,47,52,55,62,63,64,65})
                {
                    var p=Local();int mode=Main.netMode;bool dedicated=Main.dedServ;int me=Main.myPlayer;
                    try{Main.netMode=2;Main.dedServ=true;Main.myPlayer=1;p.mount.SetMount(mount,p);}finally{Main.netMode=mode;Main.dedServ=dedicated;Main.myPlayer=me;}
                    p.velocity.Y=0;p.jump=0;p.controlJump=true;p.releaseJump=false;p.autoJump=false;
                    bool excluded=(bool)type.GetMethod("ExcludedMount",Flags).Invoke(null,new object[]{p});
                    bool expected=Array.IndexOf(new[]{0,2,5,7,8,12,23,44,48,49,50,54,56,61},mount)>=0;
                    Require(excluded==expected,"Whole capability family classification mount="+mount);
                    Begin(jump,p,++cases);Before(jump,p);Require(p.releaseJump!=expected,"Only non-flight mount receives helper edge mount="+mount);Call(jump,"EndUpdate",p);
                }
                // These four source flags must come from actual Mount effects,
                // not from the stage fixture assigning a canJumpAgain field.
                int[] nativeMounts={MountID.Unicorn,MountID.Santank,MountID.WallOfFleshGoat,MountID.Basilisk};
                string[] mountFamilies={"Unicorn","Santank","WallOfFleshGoat","Basilisk"};
                for(int m=0;m<nativeMounts.Length;m++)
                {
                    var p=Local();int mode=Main.netMode;bool dedicated=Main.dedServ;int me=Main.myPlayer;
                    try{Main.netMode=2;Main.dedServ=true;Main.myPlayer=1;p.mount.SetMount(nativeMounts[m],p);}
                    finally{Main.netMode=mode;Main.dedServ=dedicated;Main.myPlayer=me;}
                    p.mount.UpdateEffects(p);
                    string family=mountFamilies[m];var option=typeof(Player).GetField("hasJumpOption_"+family);var remaining=typeof(Player).GetField("canJumpAgain_"+family);
                    Require((bool)option.GetValue(p),"Actual native Mount.UpdateEffects supplies "+family);
                    Player.jumpHeight=p.mount.JumpHeight(p.velocity.X);Player.jumpSpeed=p.mount.JumpSpeed(p.velocity.X);
                    p.controlJump=true;p.releaseJump=false;p.velocity.Y=0;p.jump=0;
                    Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);Call(jump,"EndUpdate",p);
                    Require(p.justJumped && p.jump>0 && (bool)remaining.GetValue(p),"Original mounted ground jump refreshes its own extra resource "+family);
                    p.justJumped=false;
                    for(int k=0;k<100 && (bool)remaining.GetValue(p);k++)
                    {Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);Call(jump,"EndUpdate",p);}
                    Require(p.mount.Active && p.mount.Type==nativeMounts[m] && p.jump>0 && !(bool)remaining.GetValue(p) && !p.justJumped,
                        "Actual mounted native source hands off to one consumed extra jump "+family);
                    Console.WriteLine("MOVEMENT MOUNT SOURCE type="+nativeMounts[m]+" family="+family+" jump="+p.jump+" original-effects-and-consumer=true complete-Main=false");
                }
                Player.jumpSpeed=5.01f;Player.jumpHeight=15;
                // canRocket is intentionally false before Update's later gate.
                var rocket=Local();rocket.velocity.Y=-1;rocket.rocketBoots=1;rocket.rocketTime=7;rocket.canRocket=false;
                Begin(jump,rocket,++cases);Before(jump,rocket);rocket.JumpMovement();Call(jump,"AfterJump",rocket,true,true,false);
                Require(rocket.rocketRelease && !rocket.canRocket && rocket.rocketTime==7,"Late rocket release does not falsify earlier canRocket or consume fuel.");Call(jump,"EndUpdate",rocket);Require(!rocket.rocketRelease,"Temporary rocket release closes after the full update.");
                InputTakeover(context,type);cases+=2;
                HeldSpanPrediction(context,jump);
                NativeMovementJumpWireChecks.Run(context);
                HelperWorkload(type);
                Console.WriteLine("PASS MOVEMENT JUMP original JumpMovement family/span/state cases="+cases+"; stage-consumer evidence only");
            }
            finally{Call(jump,"ClearSession");Main.player[Main.myPlayer]=saved;Player.jumpSpeed=speed;Player.jumpHeight=height;}
        }
        private static Player Local()
        {
            var p=new Player{whoAmI=Main.myPlayer,active=true,controlJump=true,releaseJump=false,position=new Vector2(700,900),gravDir=1};Main.player[Main.myPlayer]=p;return p;
        }
        private static void Begin(object jump,Player p,long tick){Call(jump,"BeginUpdate",p,1L,tick,1L);}
        private static void Before(object jump,Player p){Call(jump,"BeforeJump",p,true,true,true,false,1L);}
        private static void HeldSpanPrediction(object context,object jump)
        {
            var host=context.GetType().Assembly;
            var source=host.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource",true);
            var motor=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePlayerMotion",true);
            var actor=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeActorContext",true);
            var wire=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true);
            object values=wire.GetField("Players",Flags).GetValue(null);
            double previousSurface=Main.worldSurface;
            try
            {
            // This stage fixture has no loaded world (surface is normally 0).
            // Native Update always has a real altitude premise; the scalar's
            // optional-world fallback deliberately omits it. Compare equal
            // valid world inputs, then return the fixture's original global.
            Main.worldSurface=Main.maxTilesY*.5;
            Require(Main.worldSurface>0,"Prediction stage needs a finite loaded-world altitude premise.");
            var terrain=(IPredictionTerrain)Get(Get(Get(context,"CombatObservation"),"Prediction"),"Terrain");
            var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld};
            foreach(string family in new[]{"Cloud","Sandstorm","Blizzard","Fart","Sail","Carpet","CarpetCloud","Boots","BootsCloud","BootsReleaseOnly","WingsEmpty","WingsEmptyBootsEmpty","CarpetBoots"})
            {
                var p=Local();PreparePrefix(p,family);
                Begin(jump,p,1);Before(jump,p);Call(jump,"EndUpdate",p);
                var scalar=(PredictionPlayerMotion)source.GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{p});
                Require(scalar.Complex && scalar.AssistedOrdinarySpan && PlayerMotionContinuation.OrdinaryStep(scalar),"Future "+family+" handoff remains conditional while the near held span is ordinary.");
                byte[] bytes;
                using(var stream=new System.IO.MemoryStream())using(var writer=new System.IO.BinaryWriter(stream))
                {values.GetType().GetMethod("Write",Flags).Invoke(values,new object[]{writer,p});actor.GetMethod("WritePlayer",Flags).Invoke(null,new object[]{writer,p});motor.GetMethod("Write",Flags).Invoke(null,new object[]{writer,p});writer.Flush();bytes=stream.ToArray();}
                Call(jump,"ClearSession");p=new Player();Main.player[Main.myPlayer]=p;
                object receiver=Activator.CreateInstance(motor,true);
                using((IDisposable)Call(receiver,"UseRestoredProfiles"))
                {
                using(var reader=new System.IO.BinaryReader(new System.IO.MemoryStream(bytes,false)))
                {
                    values.GetType().GetMethod("Read",Flags).Invoke(values,new object[]{reader,p});actor.GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{reader,p});Call(receiver,"Read",reader,p.whoAmI);
                    Require(reader.BaseStream.Position==reader.BaseStream.Length,"Complete player/actor/motion pages restored for "+family);
                }
                Require((bool)motor.GetMethod("Conditional",Flags).Invoke(null,new object[]{p}) && (bool)motor.GetMethod("AssistedOrdinarySpan",Flags).Invoke(null,new object[]{p,true}),
                    "Frozen equipment/clock pages retain the ordinary prefix without a live input owner: "+family);
                var profile=((Array)motor.GetField("profiles",Flags).GetValue(receiver)).GetValue(p.whoAmI);
                // Actual original consumers construct the horizontal and jump
                // answer. The finite fixture has no speed-boosting gear; the
                // sampled altitude gravity is shared as an input, not a trigger.
                var manual=Local();PreparePrefix(manual,family);
                Player.jumpSpeed=5.01f;Player.jumpHeight=15;
                manual.HorizontalMovement();manual.JumpMovement();manual.CarpetMovement();
                float expected=manual.velocity.Y+scalar.Gravity*manual.gravDir,expectedX=manual.velocity.X;
                Main.player[Main.myPlayer]=p;
                float beforeGravity=p.gravity,sourceGravity=scalar.Gravity;
                motor.GetMethod("Step",Flags).Invoke(receiver,new[]{(object)p,profile});
                terrain.Reset();PredictionStop stop;Require(PlayerMotionContinuation.Advance(ref scalar,environment,terrain,out stop),"Actual future-origin consumer accepts supported prefix: "+family+" stop="+stop);
                Require(Math.Abs(p.velocity.X-expectedX)<.0001f && Math.Abs(scalar.Vx-expectedX)<.0001f && expectedX>.25f && Math.Abs(p.velocity.Y-expected)<.0001f && Math.Abs(scalar.Vy-expected)<.0001f && p.jump==manual.jump && scalar.Jump==manual.jump,
                    "Native/scalar held prefix matches original Horizontal+Jump+Carpet family="+family+" native="+p.velocity.Y+" scalar="+scalar.Vy+" expected="+expected+
                    " nativeX="+p.velocity.X+" scalarX="+scalar.Vx+" expectedX="+expectedX+" worldSurface="+Main.worldSurface+" worldWidth="+Main.maxTilesX+" remix="+Main.remixWorld+" beforeGravity="+beforeGravity+" sourceGravity="+sourceGravity+" defaultGravity="+scalar.DefaultGravity+" nativeGravity="+p.gravity);
                Require(p.canJumpAgain_Cloud==manual.canJumpAgain_Cloud && p.canJumpAgain_Sandstorm==manual.canJumpAgain_Sandstorm && p.canJumpAgain_Blizzard==manual.canJumpAgain_Blizzard && p.canJumpAgain_Fart==manual.canJumpAgain_Fart && p.canJumpAgain_Sail==manual.canJumpAgain_Sail && p.carpetTime==manual.carpetTime && p.carpetFrame==manual.carpetFrame && p.rocketTime==manual.rocketTime && p.rocketDelay==manual.rocketDelay && p.wingTime==manual.wingTime,
                    "Ordinary prefix preserves original future jump/carpet/boot/wing resources: "+family);
                Console.WriteLine("MOVEMENT HELD PREFIX family="+family+" vx="+p.velocity.X+" vy="+p.velocity.Y+" jump="+p.jump+" future=conditional resources=unchanged");
                scalar.Jump=1;Require(!PlayerMotionContinuation.OrdinaryStep(scalar),"No ordinary claim crosses the final action into "+family+".");
                }
                Call(jump,"ClearSession");
            }
            foreach(string boundary in new[]{"CarpetActive","RocketThrust","ResidualThrust","RocketReadyEdge","RocketEmptyEdge","WingFuel","WingBootTransfer","WingBootRefill"})
            {
                var p=Local();PreparePrefix(p,"Cloud");
                if(boundary=="CarpetActive"){p.carpet=true;p.carpetFrame=0;p.carpetTime=17;}
                else if(boundary=="RocketThrust"){p.rocketBoots=1;p.rocketDelay=3;p.rocketTime=7;}
                else if(boundary=="ResidualThrust")p.rocketDelay=3;
                else if(boundary=="RocketReadyEdge" || boundary=="RocketEmptyEdge"){p.rocketBoots=1;p.canRocket=p.rocketRelease=true;p.rocketTime=boundary=="RocketReadyEdge"?7:0;}
                else if(boundary=="WingFuel"){p.wingsLogic=1;p.wingTime=7;}
                else{p.wingsLogic=1;p.rocketBoots=1;p.rocketTime=boundary=="WingBootTransfer"?7:0;p.empressBrooch=boundary=="WingBootRefill";}
                Begin(jump,p,1);Before(jump,p);Call(jump,"EndUpdate",p);
                var scalar=(PredictionPlayerMotion)source.GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{p});
                Require(scalar.Complex && !scalar.AssistedOrdinarySpan && !PlayerMotionContinuation.OrdinaryStep(scalar),"An active or pending native flight consumer cannot borrow the held prefix: "+boundary);
                if(boundary=="WingFuel")Require(((int)motor.GetMethod("Mechanism",Flags).Invoke(null,new object[]{p})&32)!=0,"Fueled held wings were already conditional in the pre-assistance base.");
                Call(jump,"ClearSession");
            }
            var last=Local();PreparePrefix(last,"Carpet");last.jump=1;Player.jumpSpeed=5.01f;
            last.JumpMovement();last.CarpetMovement();
            Require(last.jump==0 && last.carpetFrame>=0 && last.carpetTime==299 && !last.canCarpet,
                "The unchanged native carpet really starts on the same action that ends the held span; the forecast must stop before jump=1.");
            var bootEdge=Local();PreparePrefix(bootEdge,"Boots");bootEdge.jump=1;
            Begin(jump,bootEdge,1);Before(jump,bootEdge);bootEdge.JumpMovement();Call(jump,"AfterJump",bootEdge,true,true,false);
            Require(bootEdge.jump==0 && bootEdge.rocketRelease && bootEdge.rocketTime==7 && !(bool)motor.GetMethod("AssistedOrdinarySpan",Flags).Invoke(null,new object[]{bootEdge,true}),
                "Ending the actual held jump can enable the later native rocket consumer without helper fuel writes.");
            Call(jump,"EndUpdate",bootEdge);Call(jump,"ClearSession");
            Console.WriteLine("PASS MOVEMENT JUMP held-prefix/native Horizontal+Jump+Carpet oracle; extra/carpet/boots/exhausted-wing and active-consumer boundaries");
            }
            finally{Main.worldSurface=previousSurface;Call(jump,"ClearSession");}
        }
        private static void PreparePrefix(Player p,string family)
        {
            p.jump=4;p.velocity=new Vector2(.25f,-2);p.controlRight=true;p.runAcceleration=.08f;p.runSlowdown=.2f;p.maxRunSpeed=p.accRunSpeed=3;
            if(family=="Cloud" || family=="Sandstorm" || family=="Blizzard" || family=="Fart" || family=="Sail")
            {typeof(Player).GetField("hasJumpOption_"+family).SetValue(p,true);typeof(Player).GetField("canJumpAgain_"+family).SetValue(p,true);}
            if(family.EndsWith("Cloud",StringComparison.Ordinal))p.hasJumpOption_Cloud=p.canJumpAgain_Cloud=true;
            if(family.StartsWith("Carpet",StringComparison.Ordinal)){p.carpet=p.canCarpet=true;p.carpetTime=17;}
            if(family.StartsWith("Boots",StringComparison.Ordinal) || family=="CarpetBoots"){p.rocketBoots=1;p.rocketTime=7;p.canRocket=true;p.rocketRelease=false;}
            if(family=="BootsReleaseOnly"){p.canRocket=false;p.rocketRelease=true;}
            if(family.StartsWith("WingsEmpty",StringComparison.Ordinal)){p.wingsLogic=1;p.wingTime=0;if(family=="WingsEmptyBootsEmpty")p.rocketBoots=1;}
        }
        private static void InputTakeover(object context,Type jumpType)
        {
            object movement=Get(context,"Movement"),input=Get(context,"Input");
            NativeQuickItemChecks.Until(()=>(bool)Get(movement,"ControlsEnabled"));
            bool enabled=(bool)Call(movement,"IsEnabled",0),options=Main.ingameOptionsWindow,fancy=Main.inFancyUI,onlyFancy=Main.onlyDrawFancyUI;
            var read=jumpType.GetMethod("ReadIntent",Flags);
            try
            {
                Main.ingameOptionsWindow=Main.inFancyUI=Main.onlyDrawFancyUI=false;
                Require((bool)Call(movement,"SetEnabled",0,true),"Input takeover fixture enables the owning jump preference.");
                foreach(bool optionsOwner in new[]{true,false})
                {
                    var p=Local();NativeQuickItemChecks.Sample(input,new Keys[0]);NativeQuickItemChecks.Sample(input,new Keys[0]);
                    PlayerInput.Triggers.Current.Jump=true;
                    Require((bool)Call(movement,"BeginUpdate",p),"Input takeover starts a trusted current local stage scope.");
                    try{Call(movement,"CaptureInput",p);Call(movement,"BeforeJump",p);}
                    finally{Call(movement,"EndUpdate",p);}
                    object[] before={p,0L};Require((bool)read.Invoke(null,before),"Trusted held jump publishes the forecast premise before takeover.");
                    Call(movement,"ObserveInput");object[] retained={p,0L};
                    Require((bool)read.Invoke(null,retained) && (long)retained[1]==(long)before[1],"The same allowed held input retains its premise and version.");
                    // Only the native owner changes: a neutral or stale sample
                    // must not make this a false-positive cancellation check.
                    if(optionsOwner)Main.ingameOptionsWindow=true;else Main.inFancyUI=true;
                    Call(movement,"ObserveInput");object[] after={p,0L};
                    Require(!(bool)read.Invoke(null,after) && (long)after[1]!=(long)before[1],"Native "+(optionsOwner?"options":"fancy UI")+" takeover retires old forecast before input-stage reuse.");
                    Main.ingameOptionsWindow=Main.inFancyUI=false;
                }
            }
            finally
            {
                Main.ingameOptionsWindow=options;Main.inFancyUI=fancy;Main.onlyDrawFancyUI=onlyFancy;
                Call(movement,"SetEnabled",0,enabled);Call(Get(movement,"JumpHost"),"ClearSession");
                NativeQuickItemChecks.Sample(input,new Keys[0]);
            }
        }
        private static void HelperWorkload(Type type)
        {
            object host=Activator.CreateInstance(type,true);
            var begin=(Action<Player,long,long,long>)Delegate.CreateDelegate(typeof(Action<Player,long,long,long>),host,type.GetMethod("BeginUpdate",Flags));
            var before=(Action<Player,bool,bool,bool,bool,long>)Delegate.CreateDelegate(typeof(Action<Player,bool,bool,bool,bool,long>),host,type.GetMethod("BeforeJump",Flags));
            var after=(Action<Player,bool,bool,bool>)Delegate.CreateDelegate(typeof(Action<Player,bool,bool,bool>),host,type.GetMethod("AfterJump",Flags));
            var end=(Action<Player>)Delegate.CreateDelegate(typeof(Action<Player>),host,type.GetMethod("EndUpdate",Flags));
            var allocationMethod=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",BindingFlags.Static|BindingFlags.Public);
            Require(allocationMethod!=null,"The actual CLR must expose the synchronous helper thread's allocation counter.");
            var threadAllocated=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),allocationMethod);
            long controlStart=threadAllocated();var control=new byte[64];long controlBytes=threadAllocated()-controlStart;GC.KeepAlive(control);
            Require(controlBytes>=64,"Thread allocation counter detects a deliberate managed allocation.");
            Console.WriteLine("MOVEMENT HELPER allocation-counter=current-thread positive-control-bytes="+controlBytes+" domain-side-counter=retained-not-attributed");
            AppDomain.MonitoringIsEnabled=true;long tick=0;
            foreach(int scenario in new[]{0,1,2,3})
            {
                var p=Local();p.velocity.Y=scenario==3?0:1;
                for(int i=0;i<256;i++){begin(p,2,++tick,1);before(p,scenario!=0,true,scenario>=2,false,1);after(p,true,scenario>=2,false);end(p);}
                int supplied=0;var clock=new System.Diagnostics.Stopwatch();
                long domainStart=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize,allocated=threadAllocated();clock.Start();
                for(int i=0;i<20000;i++)
                {
                    begin(p,2,++tick,1);before(p,scenario!=0,true,scenario>=2,false,1);
                    if(p.releaseJump)supplied++;
                    after(p,true,scenario>=2,false);end(p);
                }
                clock.Stop();long bytes=threadAllocated()-allocated,domainBytes=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-domainStart;
                // Only these synchronous strong-delegate calls belong to this
                // helper's allocation claim. Preference/UI workers share the
                // AppDomain: retain that aggregate separately, without assigning
                // a domain increment to this thread or relaxing its zero gate.
                domainStart=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;allocated=threadAllocated();
                for(int i=0;i<20000;i++){begin(p,2,++tick,1);before(p,scenario!=0,true,scenario>=2,false,1);after(p,true,scenario>=2,false);end(p);}
                long steady=threadAllocated()-allocated,domainSteady=AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize-domainStart;
                Require(steady==0,"Steady synchronous helper thread has no new managed objects scenario="+scenario+" bytes="+steady);
                Require(supplied==(scenario==3?20000:0),"Only native ground opportunity receives repeated per-update contributions.");
                Console.WriteLine("MOVEMENT HELPER scenario="+scenario+" iterations=20000 supplied="+supplied+" measured-ms="+clock.Elapsed.TotalMilliseconds+" warm-window-allocated="+bytes+" steady-allocated="+steady+" allocation=current-thread domain-warm-allocated="+domainBytes+" domain-steady-allocated="+domainSteady+" scope=helper-only-not-FPS");
            }
            Call(host,"ClearSession");
        }
    }
}
