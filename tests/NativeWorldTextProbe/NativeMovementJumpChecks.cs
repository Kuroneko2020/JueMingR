using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.IO;
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
        private static object stageMovement,stageInput;
        private static Player watchedFlight;
        private static int nativeWingStarts,maskedConversions;
        internal static void Run(object context)
        {
            var host=context.GetType().Assembly;
            var type=host.GetType("JueMingR.TerrariaHost.Movement.MovementJumpHost",true);
            stageMovement=Get(context,"Movement");stageInput=Get(context,"Input");
            NativeQuickItemChecks.Until(()=>(bool)Get(stageMovement,"ControlsEnabled"));Call(stageMovement,"SetEnabled",0,true);
            object jump=Get(stageMovement,"JumpHost");Player saved=Main.LocalPlayer;float speed=Player.jumpSpeed;int height=Player.jumpHeight;int cases=0;
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
                    End(jump,p);Require(!p.autoJump,"Scope contributes no accessory attributes to extra jump.");
                    int started=p.jump;
                    for(int k=0;k<started;k++){Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);End(jump,p);Require(p.jump==started-k-1,"Held native span cannot be cut short: "+family);}
                    Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);End(jump,p);Require(p.jump==0 && p.releaseJump,"Exhausted family coasts through native release without replenishing in air: "+family);
                }
                foreach(float gravity in new[]{1f,-1f})foreach(bool sliding in new[]{false,true})foreach(bool wet in new[]{false,true})
                {
                    var p=Local();p.gravDir=gravity;p.velocity.Y=wet?1:0;p.sliding=sliding;p.wet=wet;p.accFlipper=wet;p.slideDir=1;
                    Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);
                    Require(p.jump==15 && p.velocity.Y==-5.01f*gravity,"Native ground/wall/water jump follows gravity.");
                    if(sliding)Require(!p.autoJump,"Original wall consumer may clear autoJump.");
                    End(jump,p);Require(!p.autoJump && !p.releaseJump,"Normal completion clears only temporary contribution.");
                }
                foreach(bool nativeAuto in new[]{false,true})
                {
                    var p=Local();p.autoJump=nativeAuto;Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);End(jump,p);
                    Require(p.autoJump==nativeAuto && p.jump==15 && p.velocity.Y==-5.01f,"Native autoJump survives helper cleanup; no jump-speed boost.");
                }
                var down=Local();down.controlDown=true;Begin(jump,down,++cases);Call(jump,"BeforeJump",down,true,true,true,true,1L);down.JumpMovement();End(jump,down);Require(down.jump==0 && down.controlDown && down.controlJump,"Down+Jump is untouched.");
                var held=Local();held.controlJump=false;Begin(jump,held,++cases);Before(jump,held);Require(!held.releaseJump && !held.autoJump,"Old held sample cannot re-create current cleared native Jump.");End(jump,held);
                var isolated=Local();Before(jump,isolated);Require(!isolated.releaseJump,"Isolated same-name invocation has no update scope.");
                var clone=new Player{whoAmI=Main.myPlayer,active=true,controlJump=true};Begin(jump,clone,++cases);Before(jump,clone);Require(!clone.releaseJump && !clone.autoJump,"Same-number prediction clone never acquires real Player scope.");
                var local=Local();Begin(jump,local,++cases);Require((bool)Call(jump,"ReserveFallProtection",local),"Higher owner reserves before consumer.");Before(jump,local);Call(jump,"Cancel");Require(!local.releaseJump && !local.autoJump,"Disabled lower feature cannot write or undo higher reservation.");End(jump,local);
                foreach(int mount in new[]{0,2,5,7,8,12,23,44,48,49,50,54,56,61,1,3,6,9,10,14,17,43,46,47,52,55,62,63,64,65})
                {
                    var p=Local();int mode=Main.netMode;bool dedicated=Main.dedServ;int me=Main.myPlayer;
                    try{Main.netMode=2;Main.dedServ=true;Main.myPlayer=1;p.mount.SetMount(mount,p);}finally{Main.netMode=mode;Main.dedServ=dedicated;Main.myPlayer=me;}
                    p.velocity.Y=0;p.jump=0;p.controlJump=true;p.releaseJump=false;p.autoJump=false;
                    bool excluded=(bool)type.GetMethod("ExcludedMount",Flags).Invoke(null,new object[]{p});
                    bool expected=Array.IndexOf(new[]{0,2,5,7,8,12,23,44,48,49,50,54,56,61},mount)>=0;
                    Require(excluded==expected,"Whole capability family classification mount="+mount);
                    Begin(jump,p,++cases);Before(jump,p);Require(p.releaseJump!=expected,"Only non-flight mount receives helper edge mount="+mount);End(jump,p);
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
                    Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);End(jump,p);
                    Require(p.justJumped && p.jump>0 && (bool)remaining.GetValue(p),"Original mounted ground jump refreshes its own extra resource "+family);
                    p.justJumped=false;
                    for(int k=0;k<100 && (bool)remaining.GetValue(p);k++)
                    {Begin(jump,p,++cases);Before(jump,p);p.JumpMovement();Call(jump,"AfterJump",p,true,true,false);p.velocity.Y+=.4f*p.gravDir;End(jump,p);}
                    Require(p.mount.Active && p.mount.Type==nativeMounts[m] && p.jump>0 && !(bool)remaining.GetValue(p) && !p.justJumped,
                        "Actual mounted native source hands off to one consumed extra jump "+family);
                    Console.WriteLine("MOVEMENT MOUNT SOURCE type="+nativeMounts[m]+" family="+family+" jump="+p.jump+" original-effects-and-consumer=true complete-Main=false");
                }
                Player.jumpSpeed=5.01f;Player.jumpHeight=15;
                // canRocket is intentionally false before Update's later gate.
                var rocket=Local();rocket.velocity.Y=-1;rocket.rocketBoots=1;rocket.rocketTime=7;rocket.canRocket=false;
                Begin(jump,rocket,++cases);Before(jump,rocket);rocket.JumpMovement();Call(jump,"AfterJump",rocket,true,true,false);
                Require(rocket.rocketRelease && !rocket.canRocket && rocket.rocketTime==7,"Late rocket release does not falsify earlier canRocket or consume fuel.");End(jump,rocket);Require(!rocket.rocketRelease,"Temporary rocket release closes after the full update.");
                InputTakeover(context,type);cases+=2;
                CoastConsumers(context,jump,ref cases);
                FlightConsumers(context,jump,ref cases);
                HeldSpanPrediction(context,jump);
                NativeMovementJumpWireChecks.Run(context);
                HelperWorkload(type);
                Console.WriteLine("PASS MOVEMENT JUMP original JumpMovement family/span/state cases="+cases+"; stage-consumer evidence only");
            }
            finally{Call(stageMovement,"EndUpdate",Main.LocalPlayer);Call(jump,"ClearSession");Call(stageMovement,"SetEnabled",0,false);stageMovement=stageInput=null;Main.player[Main.myPlayer]=saved;Player.jumpSpeed=speed;Player.jumpHeight=height;}
        }
        private static Player Local()
        {
            var p=new Player{whoAmI=Main.myPlayer,active=true,controlJump=true,releaseJump=false,position=new Vector2(700,900),gravDir=1};Main.player[Main.myPlayer]=p;return p;
        }
        private static void Begin(object jump,Player p,long tick)
        {
            NativeQuickItemChecks.Sample(stageInput,new Keys[0]);PlayerInput.Triggers.Current.Jump=p.controlJump;PlayerInput.Triggers.Current.Down=p.controlDown;
            if((bool)Call(stageMovement,"BeginUpdate",p))Call(stageMovement,"CaptureInput",p);
        }
        private static void End(object jump,Player p){Call(stageMovement,"EndUpdate",p);}
        private static void Before(object jump,Player p){Call(stageMovement,"BeforeJump",p);}
        private static void CoastConsumers(object context,object jump,ref int cases)
        {
            var source=context.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource",true);
            foreach(float gravity in new[]{1f,-1f})foreach(string family in new[]{"ordinary","Cloud","Sandstorm"})
            {
                Call(jump,"ClearSession");var manual=Local();var assisted=Local();
                manual.gravDir=assisted.gravDir=gravity;manual.releaseJump=true;
                if(family!="ordinary")foreach(var p in new[]{manual,assisted})
                {typeof(Player).GetField("canJumpAgain_"+family).SetValue(p,true);typeof(Player).GetField("hasJumpOption_"+family).SetValue(p,true);}
                int manualStarts=0,assistStarts=0,manualSecond=-1,assistSecond=-1;float peakManual=manual.position.Y*gravity,peakAssist=assisted.position.Y*gravity;
                for(int step=0;step<120;step++)
                {
                    // Independent manual answer: retain the original held
                    // tick, then physically release during inertial ascent.
                    // This path never calls the assisted rule/phase producer.
                    Main.player[Main.myPlayer]=manual;
                    manual.controlJump=manualStarts==0 || manual.jump>0 || manual.velocity.Y*gravity>=0;
                    int before=manual.jump;manual.JumpMovement();
                    if(before<=0 && manual.jump>0){manualStarts++;if(manualStarts==2)manualSecond=step;}
                    manual.velocity.Y+=.4f*gravity;manual.position+=manual.velocity;
                    peakManual=Math.Min(peakManual,manual.position.Y*gravity);
                    Main.player[Main.myPlayer]=assisted;before=assisted.jump;
                    Begin(jump,assisted,++cases);Before(jump,assisted);assisted.JumpMovement();Call(jump,"AfterJump",assisted,true,true,false);
                    if(before<=0 && assisted.jump>0){assistStarts++;if(assistStarts==2)assistSecond=step;}
                    if(before==1)Require(assisted.velocity.Y==-Player.jumpSpeed*gravity,"Last native held tick executes fully before masking.");
                    assisted.velocity.Y+=.4f*gravity;assisted.position+=assisted.velocity;End(jump,assisted);
                    peakAssist=Math.Min(peakAssist,assisted.position.Y*gravity);
                    Require(manual.position==assisted.position && manual.velocity==assisted.velocity && manual.jump==assisted.jump,
                        "Original manual apex relay and assisted complete trajectory agree family="+family+" gravity="+gravity+" step="+step);
                    if(assisted.jump==0 && assisted.velocity.Y*gravity>=0 && (family=="ordinary" || assistStarts==2))break;
                }
                Require(peakManual==peakAssist && (family=="ordinary"?assistStarts==1:assistStarts==2 && manualSecond==assistSecond),"Independent native height/start boundary family="+family);
                Console.WriteLine("MOVEMENT COAST ORACLE family="+family+" grav="+gravity+" manualPeak="+peakManual+" assistPeak="+peakAssist+" manualSecond="+manualSecond+" assistSecond="+assistSecond);
            }
            Call(jump,"ClearSession");var carpet=Local();carpet.carpet=carpet.canCarpet=true;carpet.carpetTime=17;
            do
            {
                Begin(jump,carpet,++cases);Before(jump,carpet);int before=carpet.jump;
                carpet.JumpMovement();Call(jump,"AfterJump",carpet,true,true,false);
                if(before==1)
                {
                    Require(carpet.jump==0 && carpet.controlJump && !(bool)Call(jump,"EffectiveJump",carpet,true) && !(bool)Call(jump,"EffectiveJump",carpet,false),"Last held tick latches effective release without writing physical Jump.");
                    carpet.CarpetMovement();
                    Require(carpet.carpetFrame==-1 && carpet.carpetTime==17 && !carpet.rocketRelease,"Same-update original carpet and borrowed rocket edge wait for the owned tail.");
                    var sample=(PredictionPlayerMotion)source.GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{carpet});
                    Require(sample.AssistedJump && sample.HoldJump && sample.AssistedJumpPhase==MovementJumpPhase.Coast,"Direct capture preserves physical hold plus owned phase.");
                    var altered=sample;altered.AssistedJumpPhase=MovementJumpPhase.None;Require(!sample.SameSample(altered),"Phase alone changes exact sample identity.");
                    End(jump,carpet);break;
                }
                carpet.velocity.Y+=.4f;End(jump,carpet);
            }while(true);
            carpet.velocity.Y=.2f;Begin(jump,carpet,++cases);Before(jump,carpet);
            Require((bool)Call(jump,"EffectiveJump",carpet,true),"Coast ends across negative-to-positive velocity without an exact-zero wait.");
            Call(jump,"AfterJump",carpet,true,true,false);End(jump,carpet);
            Call(jump,"Cancel");Require((bool)Call(jump,"EffectiveJump",carpet,true),"Cancellation/scope exit leaves physical held Jump available.");
            Console.WriteLine("PASS MOVEMENT COAST native manual trajectory; last-held/carpet/rocket/direct phase/cross-sign/cancel");
        }
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
                Begin(jump,p,1);Before(jump,p);End(jump,p);
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
                Begin(jump,p,1);Before(jump,p);End(jump,p);
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
                "An unregistered pre-existing held span keeps its original rocket behavior without acquiring a coast.");
            End(jump,bootEdge);Call(jump,"ClearSession");
            Console.WriteLine("PASS MOVEMENT JUMP held-prefix/native Horizontal+Jump+Carpet oracle; extra/carpet/boots/exhausted-wing and active-consumer boundaries");
            }
            finally{Main.worldSurface=previousSurface;Call(jump,"ClearSession");}
        }
        private static void FlightConsumers(object context,object jump,ref int cases)
        {
            var update=typeof(Player).GetMethod("Update",new[]{typeof(int)});
            var isolated=new Harmony("JueMingR.Tests.Movement.NativeFlightSegment");
            isolated.Patch(update,transpiler:new HarmonyMethod(typeof(NativeMovementJumpChecks),nameof(IsolateFlight)){priority=Priority.Last});
            // These two outlets dispatch only particles/sound. Movement,
            // original gates, fuel conversion, fuel use and hover stay native.
            foreach(string visual in new[]{"WingAirVisuals","RocketBootVisuals"})isolated.Patch(AccessTools.Method(typeof(Player),visual),prefix:new HarmonyMethod(typeof(NativeMovementJumpChecks),nameof(NoFlightCosmetics)));
            isolated.Patch(AccessTools.Method(typeof(Player),"WingMovement"),prefix:new HarmonyMethod(typeof(NativeMovementJumpChecks),nameof(ObserveWingStart)));
            try
            {
                foreach(string kind in new[]{"wing","boots","wing-boots","hover"})
                {
                    Call(jump,"ClearSession");var p=Local();p.gravity=.4f;p.maxFallSpeed=10.01f;
                    watchedFlight=p;nativeWingStarts=maskedConversions=0;
                    if(kind!="boots"){p.wingsLogic=p.wings=kind=="hover"?37:1;p.wingTimeMax=20;p.wingTime=20;}
                    if(kind=="boots" || kind=="wing-boots"){p.rocketBoots=1;p.rocketTimeMax=p.rocketTime=7;}
                    if(kind=="wing-boots")p.empressBrooch=true;
                    bool ended=false,started=false,converted=false;int coast=0,last=0;
                    for(int k=0;k<75;k++)
                    {
                        int remaining=p.jump,rocket=p.rocketTime;float wing=p.wingTime;
                        if(kind=="hover" && remaining==1)p.tryKeepingHoveringDown=true;
                        p.justJumped=false;Begin(jump,p,++cases);
                        try{p.Update(Main.myPlayer);}
                        finally{End(jump,p);}
                        MovementJumpPhase phase;long version;bool intent=CallState(jump,p,out phase,out version);
                        if(kind=="wing-boots" && maskedConversions>0)converted=true;
                        if(remaining==1)
                        {
                            last++;Require(p.jump==0 && phase==MovementJumpPhase.Coast && Math.Abs(p.velocity.Y-(-5.01f+.4f))<.0001f,"Native Update retains final controlled tick and both late hover branches kind="+kind);
                        }
                        if(phase==MovementJumpPhase.Coast)
                        {coast++;Require(intent && p.controlJump && p.wingTime>=wing && p.rocketDelay==0 && (kind!="boots" || p.rocketTime==rocket),"Native Update wing/boot consumption waits through the entire owned tail kind="+kind);}
                        if(coast>0 && phase==MovementJumpPhase.None)ended=true;
                        if(ended && (kind=="boots"?p.rocketDelay>0 && p.rocketTime<rocket:nativeWingStarts>0)){started=true;break;}
                    }
                    Require(last==1 && coast>2 && ended && started && (kind!="wing-boots" || converted),"Actual native tail then flight consumer starts, retaining native conversion kind="+kind);
                    Console.WriteLine("MOVEMENT NATIVE FLIGHT kind="+kind+" last="+last+" coast="+coast+" maskedConversions="+maskedConversions+" nativeWingStarts="+nativeWingStarts+" wing="+p.wingTime+" rocket="+p.rocketTime+" delay="+p.rocketDelay);
                }
                Call(jump,"ClearSession");var flying=Local();flying.wings=flying.wingsLogic=1;flying.wingTimeMax=20;flying.wingTime=20;flying.jump=0;flying.velocity.Y=-2;flying.gravity=.4f;flying.maxFallSpeed=10.01f;
                Begin(jump,flying,++cases);try{flying.Update(Main.myPlayer);}finally{End(jump,flying);}
                MovementJumpPhase external;long ignored;CallState(jump,flying,out external,out ignored);
                Require(external==MovementJumpPhase.None && flying.wingTime<20,"Enabling during existing original flight never claims or masks its ascent.");
                Call(jump,"ClearSession");var early=Local();early.wings=early.wingsLogic=37;early.wingTimeMax=20;early.wingTime=20;early.tryKeepingHoveringDown=true;early.gravity=.4f;early.maxFallSpeed=10.01f;
                watchedFlight=early;nativeWingStarts=0;int earlyLast=0;
                for(int k=0;k<20;k++)
                {
                    int before=early.jump;early.justJumped=false;Begin(jump,early,++cases);try{early.Update(Main.myPlayer);}finally{End(jump,early);}
                    CallState(jump,early,out external,out ignored);
                    Require(external==MovementJumpPhase.None && early.controlJump && (bool)Get(jump,"intent") && nativeWingStarts==k+1,"Actual early hover flight retires only the registered jump tail, preserving held intent and continued native flight.");
                    if(before==1)earlyLast++;
                }
                Require(earlyLast==1 && early.wingTime<20,"Already-started native hover remains active across the later last held tick.");
                FrozenOrdinary(context,jump,ref cases);
                Console.WriteLine("PASS MOVEMENT NATIVE FLIGHT real retained Player.Update Jump/Carpet/wing/boot/conversion/late-hover IL; isolated scope, no Main/device claim");
            }
            finally{watchedFlight=null;isolated.UnpatchAll(isolated.Id);Call(jump,"ClearSession");}
        }
        private static bool CallState(object jump,Player p,out MovementJumpPhase phase,out long version)
        {
            object[] args={p,0L,MovementJumpPhase.None};bool result=(bool)jump.GetType().GetMethod("ReadState",Flags).Invoke(null,args);version=(long)args[1];phase=(MovementJumpPhase)args[2];return result;
        }
        private static bool NoFlightCosmetics(){return false;}
        private static void ObserveWingStart(Player __instance){if(ReferenceEquals(__instance,watchedFlight))nativeWingStarts++;}
        private static void ObserveConversion(Player p)
        {
            if(!ReferenceEquals(p,watchedFlight))return;MovementJumpPhase phase;long ignored;CallState(Get(stageMovement,"JumpHost"),p,out phase,out ignored);
            if(phase==MovementJumpPhase.Coast){Require(p.rocketTime==0,"Original conversion zero is observed before native refill.");maskedConversions++;}
        }
        private static IEnumerable<CodeInstruction> IsolateFlight(IEnumerable<CodeInstruction> source,ILGenerator generator)
        {
            var code=source.ToList();var jump=AccessTools.Method(typeof(Player),"JumpMovement");var grab=AccessTools.Method(typeof(Player),"GrabItems");
            int j=UniqueFlight(code,c=>c.Calls(jump)),g=UniqueFlight(code,c=>c.Calls(grab));
            int start=j-1,stop=-1;var dead=AccessTools.Field(typeof(Player),"dead");
            for(int i=g-1;i>=g-10;i--)if(code[i].LoadsField(dead)){stop=i-1;break;}
            Require(start>=0 && code[start].opcode==OpCodes.Ldarg_0 && stop>start && code[stop].opcode==OpCodes.Ldarg_0,"Exact native Jump load through before GrabItems dead gate.");
            var effective=stageMovement.GetType().Assembly.GetType("JueMingR.TerrariaHost.Movement.MovementHooks").GetMethod("EffectiveJump",Flags);
            int reads=0;for(int i=start;i<stop;i++)if(code[i].LoadsField(AccessTools.Field(typeof(Player),"controlJump")))
            {Require(code[i+1].opcode==OpCodes.Ldarg_0 && code[i+2].Calls(effective),"Every original flight/hover read retains the production stack filter.");reads++;}
            Require(reads==8 && code[start].blocks.Count==0 && code[stop].blocks.Count==0,"All eight actual Update reads and no crossed exception block.");
            foreach(string consumer in new[]{"CarpetMovement","WingMovement","WingFrame","RocketBootVisuals"})
            {int at=UniqueFlight(code,c=>(c.operand as MethodInfo)?.Name==consumer);Require(at>start && at<stop,"Native consumer retained: "+consumer);}
            Label entry=generator.DefineLabel();code[start].labels.Add(entry);
            var ret=new CodeInstruction(OpCodes.Ret);ret.labels.AddRange(code[stop].labels);code[stop].labels.Clear();code.Insert(stop,ret);
            int release=UniqueFlight(code,c=>c.LoadsField(AccessTools.Field(typeof(Player),"rocketRelease")));
            var conversions=Enumerable.Range(j,release-j).Where(i=>code[i].StoresField(AccessTools.Field(typeof(Player),"rocketTime")) && code[i-1].opcode==OpCodes.Ldc_I4_0).ToArray();
            Require(conversions.Length==2 && conversions[1]<stop,"Exact native no-boots zero and wing fuel-transfer zero are distinct.");
            code.InsertRange(conversions[1]+1,new[]{new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(NativeMovementJumpChecks),nameof(ObserveConversion)))});
            code.Insert(0,new CodeInstruction(OpCodes.Br,entry));
            Console.WriteLine("MOVEMENT FLIGHT IL start="+start+" stop="+stop+" nativeReads="+reads+" nativeMotionAndResources=unchanged");return code;
        }
        private static int UniqueFlight(List<CodeInstruction> code,Func<CodeInstruction,bool> match)
        {var found=Enumerable.Range(0,code.Count).Where(i=>match(code[i])).ToArray();Require(found.Length==1,"Unique fixed flight anchor count="+found.Length);return found[0];}
        private static void FrozenOrdinary(object context,object jump,ref int cases)
        {
            var host=context.GetType().Assembly;var motor=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePlayerMotion",true);
            var actor=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeActorContext",true);var wire=host.GetType("JueMingR.TerrariaHost.Combat.Prediction.PredictionWire",true);
            var source=host.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource",true);object values=wire.GetField("Players",Flags).GetValue(null);
            var saved=new Tile[6];bool solid=Main.tileSolid[1];double surface=Main.worldSurface;Player original=Main.LocalPlayer;var createdPlayers=new List<int>();
            try
            {
                // Native queries exclude forty bottom tiles. Keep this tiny
                // owned floor within the same declared original query region;
                // a low surface keeps ordinary gravity constant on its arc.
                Main.tileSolid[1]=true;Main.worldSurface=10;
                for(int x=42;x<48;x++){saved[x-42]=Main.tile[x,75];var tile=new Tile();tile.active(true);tile.type=1;Main.tile[x,75]=tile;}
                Call(jump,"ClearSession");var p=Local();p.position.Y=75*16-p.height;p.gravity=.4f;p.maxFallSpeed=10.01f;p.runSlowdown=.2f;p.runAcceleration=.08f;p.maxRunSpeed=p.accRunSpeed=3;
                Begin(jump,p,++cases);try{p.Update(Main.myPlayer);p.position+=p.TileCollision(p.position,p.velocity,false,false);}finally{End(jump,p);}
                var scalar=(PredictionPlayerMotion)source.GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{p});byte[] captured;
                using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes))
                {values.GetType().GetMethod("Write",Flags).Invoke(values,new object[]{writer,p});actor.GetMethod("WritePlayer",Flags).Invoke(null,new object[]{writer,p});motor.GetMethod("Write",Flags).Invoke(null,new object[]{writer,p});writer.Flush();captured=bytes.ToArray();}
                var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,WorldSurface=(float)Main.worldSurface};
                var terrain=(IPredictionTerrain)Get(Get(Get(context,"CombatObservation"),"Prediction"),"Terrain");var expected=new PredictionPlayerMotion[120];int starts=0,coast=0;
                for(int k=0;k<expected.Length;k++)
                {
                    terrain.Reset();PredictionStop stop;Require(PlayerMotionContinuation.Advance(ref scalar,environment,terrain,out stop),"Ordinary scalar copy advances: "+stop);
                    int before=p.jump;p.justJumped=false;Begin(jump,p,++cases);try{p.Update(Main.myPlayer);p.velocity=p.TileCollision(p.position,p.velocity,false,false);p.position+=p.velocity;}finally{End(jump,p);}
                    expected[k]=(PredictionPlayerMotion)source.GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{p});if(before<=0 && p.jump>0)starts++;if(expected[k].AssistedJumpPhase==MovementJumpPhase.Coast)coast++;
                    Require(Math.Abs(p.position.Y-scalar.Y)<.003f && Math.Abs(p.velocity.Y-scalar.Vy)<.003f && p.jump==scalar.Jump && expected[k].AssistedJumpPhase==scalar.AssistedJumpPhase,"Actual native ordinary slice and scalar held/coast/restart agree step="+k);
                }
                Require(starts>=2 && coast>2,"Ordinary original fixture includes natural tails and repeated landing starts.");
                // Advance walks the normal initialized player pool. Reuse the
                // same real-Player construction premise as the existing native
                // receiver fixture; QuickItem's smaller shell creates slot0.
                for(int slot=0;slot<Main.player.Length;slot++)if(Main.player[slot]==null){createdPlayers.Add(slot);Main.player[slot]=new Player{whoAmI=slot};}
                Call(jump,"ClearSession");var copy=new Player();Main.player[Main.myPlayer]=copy;object receiver=Activator.CreateInstance(motor,true);
                using((IDisposable)Call(receiver,"UseRestoredProfiles"))
                {
                    using(var reader=new BinaryReader(new MemoryStream(captured,false)))
                    {values.GetType().GetMethod("Read",Flags).Invoke(values,new object[]{reader,copy});actor.GetMethod("ReadPlayer",Flags).Invoke(null,new object[]{reader,copy});Call(receiver,"Read",reader,Main.myPlayer);}
                    for(int k=0;k<expected.Length;k++)
                    {
                        Call(receiver,"Advance");byte[] profile;using(var bytes=new MemoryStream())using(var writer=new BinaryWriter(bytes)){motor.GetMethod("Write",Flags).Invoke(null,new object[]{writer,copy});writer.Flush();profile=bytes.ToArray();}
                        Require(Math.Abs(copy.position.Y-expected[k].Y)<.003f && Math.Abs(copy.velocity.Y-expected[k].Vy)<.003f && copy.jump==expected[k].Jump && profile.Last()==(byte)expected[k].AssistedJumpPhase,"Frozen native Advance writes back phase across tail and next start step="+k);
                    }
                }
                Console.WriteLine("PASS MOVEMENT ordinary native/scalar/frozen-Advance steps=120 starts="+starts+" coast="+coast+" no-live-owner=true");
            }
            finally{foreach(int slot in createdPlayers)Main.player[slot]=null;Main.player[Main.myPlayer]=original;for(int x=42;x<48;x++)if(saved[x-42]!=null)Main.tile[x,75]=saved[x-42];Main.tileSolid[1]=solid;Main.worldSurface=surface;Call(jump,"ClearSession");}
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
                if(!(bool)Call(movement,"IsEnabled",0))Require((bool)Call(movement,"SetEnabled",0,true),"Input takeover fixture enables the owning jump preference.");
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
