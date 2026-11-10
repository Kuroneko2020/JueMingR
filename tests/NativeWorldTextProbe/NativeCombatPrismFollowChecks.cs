using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Natural input -> Player/NPC/projectile updates exercise the real source
    // and cursor consumer. The old direct-target shadow is only a comparator;
    // every asserted candidate direction comes from original AI075 afterwards.
    internal static class NativeCombatPrismFollowChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private sealed class Sample
        {
            internal Vector2 Origin,Before,Cursor,Target;
            internal string Stage,Kind,Semantics;internal int Key,Slot;
            internal float Speed,Error,OldError,Turn;
        }
        private static object context,control,attack;
        private static string stage;
        private static bool withdraw;
        private static Vector2 shadow;
        private static bool shadowReady;
        private static double workTotal,workMax;private static int worldSteps,budgetStops;
        private static readonly List<Sample> samples=new List<Sample>();
        private static bool viewportCase,frozenWouldExit,clearLineSeen;
        private static AttackContact sealedContact;private static long sealedAbsolute,firstStrike;
        private static Vector2 sealedScreen,sealedOrigin;private static float sealedZoom;private static int sealedKey;
        private static Projectile damageSource;
        private static int beamDepth,beamMoves,lastMoves;private static PredictionStop beamStop,lastStop;private static string lastBeamError;private static AttackContact lastBeamContact;
        private static void BeforeOuter(Projectile __instance)
        {if(withdraw && __instance.type==633 && __instance.owner==Main.myPlayer)Call(control,"ResetPreparation");}
        private static void BeforeNative(Projectile __instance,out Sample __state)
        {
            __state=null;if(__instance.type!=633 || __instance.owner!=Main.myPlayer)return;
            var input=Get(attack,"consumedInput");
            bool current=(uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount && ReferenceEquals(GetOptional(input,"Source"),__instance);
            if(!current)MissingInput(__instance,input);
            Require(current,"each measured native AI has this actual registered source's same-step input");
            var identity=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(Get(context,"CombatObservation"),"Selection"),"Target");var n=Main.npc[identity.Slot];var p=Main.LocalPlayer;
            var cursor=Main.screenPosition+new Vector2(Main.mouseX,Main.mouseY);if(p.gravDir==-1)cursor.Y=Main.screenPosition.Y+Main.screenHeight-Main.mouseY;
            __state=new Sample{Origin=p.RotatedRelativePoint(p.MountedCenter),Before=__instance.velocity,Cursor=cursor,Target=n.Center+n.netOffset,Stage=stage,Kind=Get(input,"Kind").ToString(),Semantics=Get(input,"PointSemantics").ToString(),Key=(int)__instance.key,Slot=identity.Slot,Speed=p.velocity.Length()};
            Require(((IDictionary)Get(control,"shots")).Contains(__state.Key),"the moving controller retains its original full source identity");
            if(viewportCase)ObserveViewport(__instance,__state);
        }
        private static void AfterNative(Projectile __instance,Sample __state)
        {
            if(__state==null)return;var s=__state;
            var expected=Vector2.Normalize(Vector2.Lerp((s.Cursor-s.Origin).SafeNormalize(-Vector2.UnitY),s.Before.SafeNormalize(Vector2.UnitY),.92f))*Main.LocalPlayer.HeldItem.shootSpeed*__instance.scale;
            Require(Vector2.DistanceSquared(expected,__instance.velocity)<.00001f,"actual native AI alone applies its original .92 steering and speed after integer cursor mapping");
            var desired=(s.Target-s.Origin).SafeNormalize(Vector2.UnitY);s.Error=Angle(desired,__instance.velocity);s.Turn=Angle(s.Before,__instance.velocity);
            Require(s.Turn<5.1f,"no physical cursor can exceed the original per-AI turn limit");
            if(!shadowReady){shadow=s.Before.SafeNormalize(Vector2.UnitY);shadowReady=true;}
            shadow=Vector2.Normalize(Vector2.Lerp(desired,shadow,.92f));s.OldError=Angle(desired,shadow);samples.Add(s);
        }
        internal static void Run(object value){RunCore(value,false);}
        internal static void RunViewport(object value){RunCore(value,true);}
        private static void RunCore(object value,bool viewport)
        {
            context=value;var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");attack=Get(combat,"Attack");control=Get(attack,"Control");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3541,0,0);p.position=new Vector2(640,688-p.height);p.releaseUseItem=true;
            p.statMana=p.statManaMax=p.statManaMax2=200;p.inventory[54].SetDefaults(499);p.inventory[54].stack=999;
            var oldBoots=p.armor[3];var oldFlower=p.armor[4];var oldLight=p.armor[5];p.armor[3]=new Item();p.armor[3].SetDefaults(54);p.armor[4]=new Item();p.armor[4].SetDefaults(555);p.armor[5]=new Item();p.armor[5].SetDefaults(5107);
            for(int x=115;x<Main.maxTilesX-1;x++)NativeToolsChecks.Tile(x,43,1);
            var world=new CameraWorld(viewport?4f:1.25f);
            NPC dummy=null;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,viewport,false,true,50,false,false));
            Call(host,"Set",6,true);var settings=Get(host,"Settings");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !(bool)Get(settings,"Busy");});Require((bool)Get(settings,"CompletionSucceeded"),"real aim Set commits");
            var audit=new Harmony("JueMingR.Tests.PrismFollow");
            audit.Patch(typeof(Projectile).GetMethod("AI",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(BeforeOuter)){priority=Priority.First},transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod("RippleOutlet",Flags)));
            audit.Patch(typeof(Projectile).GetMethod("AI_075",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(BeforeNative)),postfix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(AfterNative)));
            if(viewport)
            {
                audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(AfterDamage)));
                audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(Strike)));
                var beam=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostBeamAttack",true);
                audit.Patch(beam.GetMethod("SolvePrepared",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(BeforeBeam)),postfix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(AfterBeam)),finalizer:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(BeamFinal)));
                audit.Patch(typeof(PlayerMotionContinuation).GetMethod("Advance",Flags),postfix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(AfterMotion)));
            }
            samples.Clear();shadowReady=false;withdraw=false;stage="stationary";workTotal=workMax=0;worldSteps=budgetStops=0;
            viewportCase=viewport;sealedContact=null;firstStrike=-1;frozenWouldExit=clearLineSeen=false;beamDepth=lastMoves=0;lastStop=PredictionStop.None;lastBeamError=null;lastBeamContact=null;
            try
            {
                // The fixed CPU world is only 120 tiles wide. Native borders
                // clamp its left start to 640. Magiluminescence legitimately
                // accelerates this run (x1.75, speed x1.15); do not spend 170
                // pre-run actions then walk out of the world/target radius.
                // Neither preparation nor measurement writes velocity.
                for(int i=0;i<80;i++)Step(false,1);
                dummy=ElevatedDummy(2,(int)((p.Center.X+400-16)/16));
                GeometryBounds(p,dummy,134);
                // Let the new actual tile/NPC enter the ordinary observation
                // owner before the first real Shoot; keep moving naturally.
                for(int i=0;i<2;i++)Step(false,1);for(int i=0;i<12;i++)Step(true,1);
                var parent=Main.projectile.Single(q=>q.active && q.owner==p.whoAmI && q.type==633);int key=(int)parent.key;
                if(viewport){FinishViewport(parent,host,p);return;}
                Console.WriteLine("PRISM FOLLOW measured-run entry player="+p.position+" velocity="+p.velocity+" maxRun="+p.maxRunSpeed+" accRun="+p.accRunSpeed+" acceleration="+p.runAcceleration+" dummy="+dummy.Center+" active="+dummy.active+" distance="+Vector2.Distance(p.Center,dummy.Center)+" radiusTiles="+((ObservationOptions)Get(host,"Options")).Radius);
                stage="holder-run";for(int i=0;i<120;i++)Step(true,1);
                var moving=samples.Where(s=>s.Stage=="holder-run" && s.Speed>=5).ToArray();
                Console.WriteLine("PRISM FOLLOW native moving samples="+moving.Length+" speedMax="+samples.Max(s=>s.Speed)+" errorMean="+(moving.Length==0?999:moving.Average(s=>s.Error))+" oldDirectMean="+(moving.Length==0?0:moving.Average(s=>s.OldError))+" errorMax="+(moving.Length==0?999:moving.Max(s=>s.Error))+" basic="+samples.Count(s=>s.Kind=="Basic")+" enhanced="+samples.Count(s=>s.Kind=="Enhanced"));
                Require(moving.Length>=30 && moving.All(s=>s.Slot==2 && s.Key==key),"one placed static dummy and one actual source undergo sustained native boot-assisted player movement");
                Require(moving.Average(s=>s.Error)<1 && moving.Average(s=>s.Error)<moving.Average(s=>s.OldError)*.35f,"actual post-AI beam tracking removes the direct-target lag on the same native movement, not only its generated cursor");
                Require(samples.All(s=>s.Semantics=="Direction"),"consumed mouse directions are never labelled as fixed target world points");
                stage="settle";for(int i=0;i<30;i++)Step(true,0);
                Require(samples.Where(s=>s.Stage=="settle").Skip(15).All(s=>s.Error<.3f),"steady target and holder settle without a feedback oscillation");
                // Withdraw only enhanced preparation before the real consumer;
                // source, native use, permissions and live target stay intact.
                stage="basic";withdraw=true;for(int i=0;i<12;i++)Step(true,-1);withdraw=false;
                Require(samples.Where(s=>s.Stage=="basic").All(s=>s.Kind=="Basic" && s.Error<.3f),"unknown/withdrawn future falls back to the same current-only steering law");
                stage="switch";dummy.active=false;var second=ElevatedDummy(3,Math.Max(8,(int)(p.Center.X/16)-18));
                for(int i=0;i<45;i++)Step(true,0);
                var switched=samples.Where(s=>s.Stage=="switch").ToArray();Require(switched.Length>=35 && switched.All(s=>s.Slot==3 && s.Key==key) && switched.Skip(38).All(s=>s.Error<.3f),"large target switch saturates legally and converges on the same original source");
                stage="moving-target";second.active=false;var movingTarget=Main.npc[4];movingTarget.SetDefaults(3);movingTarget.whoAmI=4;movingTarget.active=true;movingTarget.Center=p.Center+new Vector2(-260,-100);movingTarget.velocity=new Vector2(3,0);movingTarget.aiStyle=-1;movingTarget.noGravity=true;movingTarget.life=movingTarget.lifeMax=100000;movingTarget.knockBackResist=0;
                for(int i=0;i<40;i++)Step(true,0);
                var tracking=samples.Where(s=>s.Stage=="moving-target").ToArray();Require(tracking.Length==40 && tracking.Skip(30).All(s=>s.Slot==4 && s.Error<.5f),"actual constant-velocity native NPC is tracked after legal acquisition, with no fabricated future");
                audit.UnpatchAll(audit.Id);Call(host,"Set",6,false);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !(bool)Get(settings,"Busy");});
                NativeMapping(parent);
                Console.WriteLine("PRISM FOLLOW shared 6ms work steps="+worldSteps+" meanMs="+(workTotal/Math.Max(1,worldSteps))+" maxMs="+workMax+" budgetStops="+budgetStops+"; native/source/input continue on Basic, no budget override");
                Console.WriteLine("PASS prism follow native moving holder/static dummy, actual moving target, large switch, Basic withdrawal, steady/turn-limit/identity/gravity; CPU chain, not desktop or multiplayer");
            }
            finally
            {
                withdraw=false;audit.UnpatchAll(audit.Id);p.armor[3]=oldBoots;p.armor[4]=oldFlower;p.armor[5]=oldLight;p.gravDir=1;
                viewportCase=false;damageSource=null;
                try{NativeCombatObservationChecks.Save(host,new ObservationOptions());for(int i=0;i<8;i++)Step(false,0);}
                finally{world.Dispose();context=control=attack=null;}
            }
        }
        private static void Step(bool held,int movement)
        {
            // The preceding world's Draw already established the viewport.
            // Original Main runs Player/NPC/projectiles BEFORE this world's
            // DoDraw_UpdateCameraPosition; do not centre after Player.Update.
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");
            // Sample accepts WORLD-native input and projects it to raw pixels.
            // At zoom4 a native quarter-screen point projects outside the
            // client. Choose a legal raw quarter first, then invert that same
            // matrix; this is the physical cursor, unrelated to assisted aim.
            var physical=Main.screenPosition+Vector2.Transform(new Vector2(Main.screenWidth*.25f,Main.screenHeight*.25f),Matrix.Invert(Main.GameViewMatrix.ZoomMatrix));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),physical,held,movement);
            var raw=Terraria.GameInput.PlayerInput.MouseInfo;Require(raw.X>=0 && raw.X<Main.screenWidth && raw.Y>=0 && raw.Y<Main.screenHeight,"fixture physical input remains an actual client pixel under the current zoom");
            Call(combat,"Sample");Call(host,"SampleMouse");int x=Main.mouseX,y=Main.mouseY;
            NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);
            for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}Main.ProjectileUpdateLoopIndex=-1;
            Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require(Main.mouseX==x && Main.mouseY==y,"normal native chain returns the physical mouse");
            var work=Get(host,"PredictionWork");double used=Convert.ToDouble(Get(work,"UsedMilliseconds"));workTotal+=used;workMax=Math.Max(workMax,used);worldSteps++;if((bool)Get(work,"Exceeded"))budgetStops++;
            DrawCamera();
            if(viewportCase && sealedContact!=null && firstStrike<0)
            {
                var focus=Main.PlayerFocusedScreenPosition();
                Require(Main.screenPosition==new Vector2((int)focus.X,(int)focus.Y),"the sealed long positive stays in the actual normal following branch before first native receive, rather than accidentally reaching a world clamp");
            }
        }
        private static void DrawCamera(){typeof(Main).GetMethod("DoDraw_UpdateCameraPosition",Flags).Invoke(null,null);}
        private sealed class CameraWorld:IDisposable
        {
            private readonly float left=Main.leftWorld,right=Main.rightWorld,top=Main.topWorld,bottom=Main.bottomWorld,x=Main.cameraX,y=Main.cameraY;
            private readonly Vector2 screen=Main.screenPosition,zoom=Main.GameViewMatrix.Zoom,pan=Main.CurrentPan;
            private readonly Main instance=Main.instance;private readonly Terraria.Graphics.CameraModifiers.CameraModifierStack modifiers;
            private readonly object lerp=typeof(Main).GetField("cameraLerp",Flags).GetValue(null);
            internal CameraWorld(float renderZoom)
            {
                // The existing finite tile domain [0,1920] is this CPU scene's
                // play area; native WorldUtils still removes its real 640px
                // outer border. Both native BordersMovement and our predictor
                // read these fixed world facts. No per-action border changes.
                Main.leftWorld=Main.topWorld=-640;Main.rightWorld=Main.maxTilesX*16+640;Main.bottomWorld=Main.maxTilesY*16+640;
                Main.CurrentPan=Vector2.Zero;Main.cameraX=Main.cameraY=0;typeof(Main).GetField("cameraLerp",Flags).SetValue(null,0f);
                if(Main.instance==null){Main.instance=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(Main.instance);}
                modifiers=Main.instance.CameraModifiers;Main.instance.CameraModifiers=new Terraria.Graphics.CameraModifiers.CameraModifierStack();Main.GameViewMatrix.Zoom=new Vector2(renderZoom);DrawCamera();
            }
            public void Dispose()
            {
                Main.leftWorld=left;Main.rightWorld=right;Main.topWorld=top;Main.bottomWorld=bottom;Main.cameraX=x;Main.cameraY=y;Main.CurrentPan=pan;Main.GameViewMatrix.Zoom=zoom;Main.screenPosition=screen;typeof(Main).GetField("cameraLerp",Flags).SetValue(null,lerp);
                Main.instance.CameraModifiers=modifiers;Main.instance=instance;
            }
        }
        private static void BeforeDamage(Projectile __instance,out Projectile __state){__state=damageSource;damageSource=__instance;}
        private static void AfterDamage(Projectile __state){damageSource=__state;}
        private static void BeforeBeam(Projectile parent,out int __state)
        {__state=beamDepth;if(viewportCase && parent.type==633 && parent.ai[0]<=8){beamDepth++;if(__state==0){beamMoves=0;beamStop=PredictionStop.None;lastBeamError=null;}}}
        private static void AfterMotion(bool __result,PredictionStop stop)
        {if(beamDepth>0){beamMoves++;if(!__result)beamStop=stop;}}
        private static void AfterBeam(AttackContact __result,int __state)
        {if(beamDepth>__state){lastMoves=beamMoves;lastStop=beamStop;lastBeamContact=__result;}beamDepth=__state;}
        private static Exception BeamFinal(Exception __exception,int __state)
        {if(__exception!=null && beamDepth>__state){lastMoves=beamMoves;lastStop=beamStop;lastBeamError=__exception.GetType().Name;lastBeamContact=null;}beamDepth=__state;return __exception;}
        private static void Strike(NPC __instance)
        {
            if(viewportCase && sealedContact!=null && firstStrike<0 && __instance.whoAmI==2 && damageSource?.type==632 && (int)(Terraria.DataStructures.ProjectileKey)damageSource.ai[1]==sealedKey)
            {firstStrike=Main.GameUpdateCount;Console.WriteLine("PRISM VIEWPORT first native632 Strike absolute="+firstStrike+" parentKey="+sealedKey+" childKey="+damageSource.key+" expected="+sealedAbsolute);}
        }
        private static void ObserveViewport(Projectile parent,Sample sample)
        {
            var host=Get(context,"CombatObservation");clearLineSeen|=(bool)Get(Get(host,"Selection"),"ClearLine");
            if(sealedContact==null && parent.ai[0]>=1 && parent.ai[0]<=8)
            {
                var contact=GetOptional(attack,"ExpectedImpact") as AttackContact;
                var entry=((IDictionary)Get(control,"shots"))[(int)parent.key];var rawContact=GetOptional(entry,"Contact") as AttackContact;var timeline=GetOptional(control,"preparedTimeline") as NpcTrajectory;var work=Get(host,"PredictionWork");var lease=Get(entry,"PrismView");var physical=Terraria.GameInput.PlayerInput.MouseInfo;
                Console.WriteLine("PRISM VIEWPORT prefix charge="+parent.ai[0]+" GUC="+Main.GameUpdateCount+" phase="+Get(control,"preparedPhase")+" prepared="+Get(entry,"Prepared")+" children="+((ICollection)Get(entry,"Children")).Count+" point="+Get(entry,"HasPoint")+" rawContact="+(rawContact==null?"null":rawContact.Tick.ToString())+" public="+(contact==null?"null":contact.Tick.ToString())+" viewCurrent="+Get(lease,"Current")+" futureUnknown="+Get(lease,"Unknown")+" timeline="+(timeline==null?"null":timeline.SampleTick+"/"+timeline.Count+"/"+timeline.Stop+"/paused="+timeline.BudgetPaused)+" moves="+lastMoves+" motionStop="+lastStop+" lastBeam="+(lastBeamContact==null?"null":lastBeamContact.Tick.ToString())+" error="+(lastBeamError??"none")+" work="+Get(work,"UsedMilliseconds")+" exceeded="+Get(work,"Exceeded")+" raw="+physical.X+","+physical.Y+" native="+Main.mouseX+","+Main.mouseY+" velocity="+parent.velocity+" view="+Main.screenPosition+" zoom="+Main.GameViewMatrix.RenderZoom.X);
                if(contact!=null)
                {
                    Require(contact.Timeline.SampleTick+contact.Tick>=Main.GameUpdateCount+20,"the sealed positive really needs a long future displacement before first receive");
                    sealedContact=contact;sealedAbsolute=contact.Timeline.SampleTick+contact.Tick;sealedScreen=Main.screenPosition;sealedOrigin=sample.Origin;sealedZoom=Main.GameViewMatrix.RenderZoom.X;sealedKey=(int)parent.key;
                    Console.WriteLine("PRISM VIEWPORT seal GUC="+Main.GameUpdateCount+" phase="+Get(control,"preparedPhase")+" charge="+parent.ai[0]+" sample="+contact.Timeline.SampleTick+" tick="+contact.Tick+" receive="+contact.ReceiveTick+" target="+contact.Timeline.Identity.Slot+" key="+sealedKey+" view="+sealedScreen+" zoom="+sealedZoom+" playArea="+Terraria.WorldBuilding.WorldUtils.GetWorldPlayArea());
                    var preview=Call(control,"Preview",contact.Timeline,Get(control,"preparedPhase"),0,false) as AttackContact;
                    Require(preview!=null && (bool)Call(control,"PreviewCurrent",preview),"the same positive Contact reaches its actual read-only controller preview consumer");
                    var registry=(IDictionary)Get(control,"shots");var z=Main.GameViewMatrix.Zoom;
                    try{Main.GameViewMatrix.Zoom=z*.9f;Require(GetOptional(attack,"ExpectedImpact")==null && !(bool)Call(control,"PreviewCurrent",preview) && (bool)Get(entry,"HasPoint") && registry.Contains((int)parent.key),"same-GUC viewport mutation withdraws only stale contact/preview, preserving the current point and source");}
                    finally{Main.GameViewMatrix.Zoom=z;}
                }
            }
            if(sealedContact!=null && firstStrike<0)
            {
                var relative=sample.Origin-sealedScreen;float cx=Main.screenWidth/2f,cy=Main.screenHeight/2f;
                frozenWouldExit|=relative.X<cx+(1-cx)/sealedZoom || relative.X>cx+(Main.screenWidth-2-cx)/sealedZoom || relative.Y<cy+(1-cy)/sealedZoom || relative.Y>cy+(Main.screenHeight-2-cy)/sealedZoom;
            }
        }
        private static void FinishViewport(Projectile parent,object host,Player p)
        {
            Require(sealedContact!=null,"normal following camera produces a nonempty early frozen Contact under the existing budget");
            for(int i=0;i<80 && firstStrike<0;i++)Step(true,1);
            Console.WriteLine("PRISM VIEWPORT joint expected="+sealedAbsolute+" actual="+firstStrike+" frozenViewWouldExit="+frozenWouldExit+" originDisplacement="+(p.RotatedRelativePoint(p.MountedCenter)-sealedOrigin)+" clearLinePositive="+clearLineSeen);
            Require(firstStrike==sealedAbsolute && frozenWouldExit && clearLineSeen,"one action-before Contact matches the later first actual632 receive with enough motion to break the old frozen view; ClearLine stays model-positive");
            var timeline=Get(control,"preparedTimeline");var completed=Enum.Parse(Get(control,"preparedPhase").GetType(),"CompletedWorld");
            Main.CurrentPan=new Vector2(20,0);
            try
            {
                Require(Call(control,"Preview",timeline,completed,0,false)==null && (bool)Get(control,"PreviewFutureUnknown"),"observed unsupported pan marks only its future preview tail unknown");
                Step(true,1);Require(((IDictionary)Get(control,"shots")).Contains((int)parent.key),"actual current consumer remains registered and executes through unsupported future pan");
            }
            finally{Main.CurrentPan=Vector2.Zero;}
            Step(true,1);Require(Call(control,"Preview",Get(control,"preparedTimeline"),completed,0,false)!=null,"normal camera supports a positive again after the local unknown ends");
            // Finish this source before setting up a separate fixed boundary
            // scene; never move a boundary to clamp a running test actor.
            viewportCase=false;for(int i=0;i<8;i++)Step(false,0);EdgeViewport(host,p);
            Console.WriteLine("PASS prism future viewport: real Draw phase, sealed long positive/first native632 Strike, ClearLine/preview, scoped unknown/recovery, fixed world-edge clamp");
        }
        private static void EdgeViewport(object host,Player p)
        {
            var combat=Get(context,"Combat");NativeCombatObservationChecks.Save(host,new ObservationOptions());
            p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3541,0,0);p.position=new Vector2(1300,688-p.height);p.releaseUseItem=true;p.statMana=p.statManaMax=p.statManaMax2=200;
            Main.rightWorld=p.position.X+p.width+100+640;Main.CurrentPan=Vector2.Zero;ElevatedDummy(2,(int)((p.Center.X-200-16)/16));
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,true,50,false,true));DrawCamera();for(int i=0;i<2;i++)Step(false,0);Step(true,0);
            var assembly=combat.GetType().Assembly;var cameraType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostPrismSteering+Camera",true);var steering=assembly.GetType("JueMingR.TerrariaHost.Combat.HostPrismSteering",true);
            var camera=Activator.CreateInstance(cameraType,Flags,null,new object[]{p},null);var fixedView=Main.screenPosition;float start=p.position.X;
            for(int i=0;i<38;i++)
            {
                Step(true,1);Require((bool)Call(camera,"Next",p),"fixed native world edge remains a supported camera branch");
                Require((Vector2)Get(Get(camera,"View"),"Position")==Main.screenPosition && Main.screenPosition==fixedView,"predicted view agrees with actual native Draw clamp while the player moves relative to it");
            }
            var origin=p.RotatedRelativePoint(p.MountedCenter);var logical=origin+Vector2.UnitX*400;
            var live=(Vector2)Static(steering,"Point",origin,Vector2.UnitX*30,logical,p.gravDir);
            var predicted=(Vector2)Static(steering,"PointInView",origin,Vector2.UnitX*30,logical,p.gravDir,Get(camera,"View"));
            Require(p.position.X>start+50 && live==logical && predicted==live,"real short ray at the fixed edge keeps the same logical-point fallback in live and predicted view, rather than eternally centring the holder");
        }
        private static NPC ElevatedDummy(int slot,int x)
        {
            // The original NPC has gravity. Support the real placed 2x3 tile
            // with vanilla solid terrain; do not hold its body in mid-air.
            Require(x>=5 && x+1<Main.maxTilesX-5,"supported dummy remains inside the finite native world");
            for(int dx=0;dx<2;dx++)NativeToolsChecks.Tile(x+dx,35,1);
            return NativeCombatControlPreviewChecks.Dummy(slot,x,32);
        }
        private static void GeometryBounds(Player p,NPC dummy,int actions)
        {
            float v=p.velocity.X,end=p.position.X,increment=p.runAcceleration*.2f;
            for(int i=0;i<actions;i++){if(v<p.maxRunSpeed)v+=p.runAcceleration;else if(v<p.accRunSpeed)v+=increment;end+=v;}
            // Include the real neutral-input braking tail, not a timer or
            // velocity reset at the end of the measurement.
            for(int i=0;i<100 && v>0;i++){v=Math.Max(0,v-p.runSlowdown);end+=v;}
            float height=Math.Abs(p.Center.Y-dummy.Center.Y),horizontal=Math.Max(Math.Abs(p.Center.X-dummy.Center.X),Math.Abs(end+p.width/2f-dummy.Center.X));
            double far=Math.Sqrt(horizontal*horizontal+height*height),turn=(p.accRunSpeed+increment)/height*180/Math.PI;
            Console.WriteLine("PRISM FOLLOW finite geometry preRun="+p.position+" speed="+p.velocity+" boundEndIncludingBrake="+end+" dummy="+dummy.Center+" height="+height+" distanceBound="+far+" angularRateBoundDegrees="+turn+" nativeTurnLimitDegrees="+(Math.Asin(.08/.92)*180/Math.PI));
            Require(p.velocity.X>=5 && end+p.width<(Main.maxTilesX-1)*16,"real pre-run reaches the unchanged speed premise and the entire native run/braking tail fits the original finite world");
            Require(height>=110 && height<=150 && far<800 && turn<4.9,"one supported stationary dummy stays within the original minimum selection radius with a nonzero, physically trackable continuous angular demand");
        }
        private static void NativeMapping(Projectile parent)
        {
            var p=Main.LocalPlayer;var host=Get(context,"Combat").GetType().Assembly;var steering=host.GetType("JueMingR.TerrariaHost.Combat.HostPrismSteering",true);var scope=host.GetType("JueMingR.TerrariaHost.Combat.CombatCursorScope",true);
            var priorZoom=Main.GameViewMatrix.Zoom;
            try{foreach(float zoom in new[]{.5f,1.25f})foreach(float gravity in new[]{1f,-1f})
            {
                Main.GameViewMatrix.Zoom=new Vector2(zoom);p.gravDir=gravity;Main.screenPosition=p.MountedCenter-new Vector2(Main.screenWidth/2,Main.screenHeight/2);var origin=p.RotatedRelativePoint(p.MountedCenter);var before=parent.velocity;
                var logical=origin+before.SafeNormalize(Vector2.UnitX).RotatedBy(.03)*400;var point=(Vector2)Static(steering,"Point",origin,before,logical,gravity);var receipt=Static(scope,"Begin",point,true);
                try{RawPreimage();parent.AI();Require(Angle(parent.velocity,logical-origin)<.3f,"integer and reversed-gravity mouse is consumed by original AI, not merely an algebraic point assertion");}
                finally{Call(receipt,"End");}
                if(zoom<1)
                {
                    bool negativeX=false,negativeY=false;
                    // These input-geometry counterexamples do not overwrite
                    // the live parent's direction: they force each negative
                    // coordinate ray and then check the ORIGINAL native motor
                    // from whatever velocity the preceding real AI produced.
                    foreach(var axis in new[]{-Vector2.UnitX,-Vector2.UnitY,Vector2.UnitY})
                    {
                        var ray=(Vector2)Static(steering,"Point",origin,axis*30,origin+axis*400,gravity);var borrow=Static(scope,"Begin",ray,true);
                        try
                        {
                            RawPreimage();negativeX|=Main.mouseX<0;negativeY|=Main.mouseY<0;
                            var cursor=Main.screenPosition+new Vector2(Main.mouseX,gravity==-1?Main.screenHeight-Main.mouseY:Main.mouseY);
                            var expected=Vector2.Normalize(Vector2.Lerp((cursor-origin).SafeNormalize(-Vector2.UnitY),parent.velocity.SafeNormalize(Vector2.UnitY),.92f))*p.HeldItem.shootSpeed*parent.scale;
                            parent.AI();Require(Vector2.DistanceSquared(expected,parent.velocity)<.00001f,"negative native integer cursor is consumed by the original motor with unchanged inertia");
                        }
                        finally{Call(borrow,"End");}
                    }
                    Require(negativeX && negativeY,"zoom .5 counterexamples actually exercise negative native X and Y under each gravity");
                }
            }}finally{Main.GameViewMatrix.Zoom=priorZoom;}
            p.gravDir=1;var o=p.RotatedRelativePoint(p.MountedCenter);var old=parent.velocity;var opposite=o-old.SafeNormalize(Vector2.UnitX)*400;var a=(Vector2)Static(steering,"Point",o,old,opposite,p.gravDir);var b=(Vector2)Static(steering,"Point",o,old,opposite,p.gravDir);Require(a==b,"180-degree tie is stateless and deterministic");
            Require((Vector2)Static(steering,"Point",o,Vector2.Zero,opposite,p.gravDir)==opposite && (Vector2)Static(steering,"Point",o,old,o,p.gravDir)==o,"zero direction/distance preserves the original safe logical-point fallback");
        }
        private static float Angle(Vector2 a,Vector2 b)
        {a=a.SafeNormalize(Vector2.UnitY);b=b.SafeNormalize(Vector2.UnitY);return (float)(Math.Abs(Math.Atan2(a.X*b.Y-a.Y*b.X,Vector2.Dot(a,b)))*180/Math.PI);}
        private static object Static(Type type,string method,params object[] args)
        {return type.GetMethod(method,Flags).Invoke(null,args);}
        private static void RawPreimage()
        {
            float inverse=1f/Main.GameViewMatrix.RenderZoom.X,cx=Main.screenWidth/2f,cy=Main.screenHeight/2f;
            Require(Enumerable.Range(0,Main.screenWidth).Any(raw=>(int)(cx+(raw-cx)*inverse)==Main.mouseX) && Enumerable.Range(0,Main.screenHeight).Any(raw=>(int)(cy+(raw-cy)*inverse)==Main.mouseY),"both actual borrowed native ints have an integer raw-client preimage at .5/1.25 zoom, including negative axes and either gravity");
        }
        private static void MissingInput(Projectile shot,object input)
        {
            // Preserve the FIRST failed consumer, before throwing or any
            // completed-world refresh. These are explicitly prefix snapshots,
            // not a replacement for an actual production gate trace.
            var p=Main.LocalPlayer;var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var selection=Get(host,"Selection");var registry=(IDictionary)Get(control,"shots");
            var entry=registry.Contains((int)shot.key)?registry[(int)shot.key]:null;var source=entry==null?null:GetOptional(entry,"Source");var previous=GetOptional(input,"Source") as Projectile;
            Console.WriteLine("PRISM FOLLOW FIRST missing-input prefix snapshot stage="+stage+" GUC="+Main.GameUpdateCount+" charge="+shot.ai[0]+" key="+shot.key+" inputStep="+Get(attack,"consumedInputStep")+" kind="+Get(input,"Kind")+" inputSource="+(previous==null?"null":previous.key.ToString())+" registered="+(entry!=null)+" registryCount="+registry.Count+" source="+(source!=null)+" sourceSelection="+(source==null?"none":Get(source,"Selection"))+" actualSelection="+Get(Get(context,"Tools"),"SelectionIntent")+" slot="+p.selectedItem+" held="+p.HeldItem.type+" player="+p.position+" velocity="+p.velocity+" hasTarget="+Get(selection,"HasTarget")+" target="+Get(selection,"Target")+" basic="+Get(attack,"BasicPermission")+" receiver="+Get(attack,"CurrentReceiver")+" admitted="+Call(combat,"Admitted",p,false)+" fresh="+Get(combat,"FreshGesture")+" channel="+p.channel+" use="+p.controlUseItem+" mana="+p.statMana+" sampled="+Get(combat,"sampledFrame")+" frame="+Get(Get(context,"Input"),"Frame")+" rawCanStart="+Get(Get(context,"Input"),"CanStartActions"));
            foreach(int slot in new[]{2,3,4}){var n=Main.npc[slot];Console.WriteLine("PRISM FOLLOW FIRST npc="+slot+" active="+n.active+" type="+n.type+" life="+n.life+" center="+n.Center+" distance="+Vector2.Distance(p.Center,n.Center)+" dontTake="+n.dontTakeDamage+" friendly="+n.friendly+" immortal="+n.immortal);}
            Console.WriteLine("PRISM FOLLOW FIRST prior successful samples="+samples.Count+" last="+(samples.Count==0?"none":samples[samples.Count-1].Stage+"/"+samples[samples.Count-1].Key+"/"+samples[samples.Count-1].Slot+"/"+samples[samples.Count-1].Speed));
        }
    }
}
