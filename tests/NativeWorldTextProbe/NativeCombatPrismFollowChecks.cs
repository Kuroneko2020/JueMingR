using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
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
        internal static void Run(object value)
        {
            context=value;var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");attack=Get(combat,"Attack");control=Get(attack,"Control");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3541,0,0);p.position=new Vector2(640,688-p.height);p.releaseUseItem=true;
            p.statMana=p.statManaMax=p.statManaMax2=200;p.inventory[54].SetDefaults(499);p.inventory[54].stack=999;
            var oldBoots=p.armor[3];var oldFlower=p.armor[4];var oldLight=p.armor[5];p.armor[3]=new Item();p.armor[3].SetDefaults(54);p.armor[4]=new Item();p.armor[4].SetDefaults(555);p.armor[5]=new Item();p.armor[5].SetDefaults(5107);
            for(int x=115;x<Main.maxTilesX-1;x++)NativeToolsChecks.Tile(x,43,1);
            Main.screenPosition=new Vector2(400,200);
            NPC dummy=null;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,true,50,false,false));
            Call(host,"Set",6,true);var settings=Get(host,"Settings");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !(bool)Get(settings,"Busy");});Require((bool)Get(settings,"CompletionSucceeded"),"real aim Set commits");
            var audit=new Harmony("JueMingR.Tests.PrismFollow");
            audit.Patch(typeof(Projectile).GetMethod("AI",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(BeforeOuter)){priority=Priority.First},transpiler:new HarmonyMethod(typeof(NativeCombatBeamChecks).GetMethod("RippleOutlet",Flags)));
            audit.Patch(typeof(Projectile).GetMethod("AI_075",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(BeforeNative)),postfix:new HarmonyMethod(typeof(NativeCombatPrismFollowChecks),nameof(AfterNative)));
            samples.Clear();shadowReady=false;withdraw=false;stage="stationary";workTotal=workMax=0;worldSteps=budgetStops=0;
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
                NativeCombatObservationChecks.Save(host,new ObservationOptions());for(int i=0;i<8;i++)Step(false,0);context=control=attack=null;
            }
        }
        private static void Step(bool held,int movement)
        {
            // The actual local holder remains in the client viewport as in a
            // normal following camera, rather than running out of a frozen
            // fixture camera and invalidating the new legal mouse-ray premise.
            Main.screenPosition=Main.LocalPlayer.Center-new Vector2(Main.screenWidth/2f,Main.screenHeight/2f);
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var physical=Main.screenPosition+new Vector2(Main.screenWidth*.25f,Main.screenHeight*.25f);NativeToolExecutionChecks.Sample(context,Get(context,"Input"),physical,held,movement);Call(combat,"Sample");Call(host,"SampleMouse");int x=Main.mouseX,y=Main.mouseY;
            NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);
            for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}Main.ProjectileUpdateLoopIndex=-1;
            Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require(Main.mouseX==x && Main.mouseY==y,"normal native chain returns the physical mouse");
            var work=Get(host,"PredictionWork");double used=Convert.ToDouble(Get(work,"UsedMilliseconds"));workTotal+=used;workMax=Math.Max(workMax,used);worldSteps++;if((bool)Get(work,"Exceeded"))budgetStops++;
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
