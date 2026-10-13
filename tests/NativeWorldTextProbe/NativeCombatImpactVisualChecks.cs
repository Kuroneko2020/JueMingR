using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCombatImpactVisualChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static bool readFault,drawFault;
        private static int faults;
        private static void ReadFault(){if(readFault){faults++;throw new InvalidOperationException("controlled impact read failure");}}
        private static void DrawFault(){if(drawFault){faults++;throw new InvalidOperationException("controlled impact segment failure");}}
        private static bool Red(Color p){return p.A>100 && p.R>190 && p.G<110 && p.B<110;}
        private static bool PathInk(Color p){return p.A>100 && p.R>180 && p.G>150 && p.B>150;}
        private static void PrepareDisplayFixture(object context,object host,object attack,object combat,object input,NPC npc,bool pressed=true)
        {
            // These are already-prepared display components, not natural first-use
            // checks. Each independent camera/input premise gets a real update;
            // retain finite cold omissions without clearing the shared allowance.
            for(int attempt=0;attempt<6;attempt++)
            {
                NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,npc.Center,pressed);Call(combat,"Sample");
                NativeCombatAimChecks.Prepare(host,attack,npc,0);NativeCombatAimChecks.PrepareDisplay(host,attack);
                Call(Get(host,"World"),"Prepare");
                if(!(bool)Get(host,"Path") || GetOptional(host,"DisplayPath")!=null && GetOptional(Get(Get(host,"World"),"Impact"),"contact")!=null)return;
                var work=Get(host,"PredictionWork");Console.WriteLine("IMPACT component cold omission attempt="+attempt+" tick="+Main.GameUpdateCount+" used="+Get(work,"UsedMilliseconds")+" exceeded="+Get(work,"Exceeded")+" display="+(GetOptional(host,"DisplayPath")!=null));
            }
            Require(false,"finite component preparation must obtain a healthy display before pixel/revocation checks");
        }
        internal static void Run(object context,ProbeGraphics graphics,string output)
        {
            var retiredAttack=Get(Get(context,"Combat"),"Attack");
            if(!(bool)Get(retiredAttack,"PreviewDemand")){NativeCombatImpactRetirementChecks.Run(context,graphics);return;}

            if(Environment.GetEnvironmentVariable("JMR_IMPACT_VISUAL_ONLY")=="a2-rectangle"){CheckA2Rectangle(context,graphics);return;}
            CheckReceiveGeometry();
            if(Environment.GetEnvironmentVariable("JMR_IMPACT_VISUAL_ONLY")=="natural-slime"){NativeCombatNaturalImpactChecks.Run(context,graphics,Path.Combine(output,"natural-slime"));return;}
            if(Environment.GetEnvironmentVariable("JMR_IMPACT_VISUAL_ONLY")=="usage"){NativeCombatUsageChecks.Run(context,graphics,output);return;}
            Directory.CreateDirectory(output);var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var world=Get(host,"World");var impact=Get(world,"Impact");var input=Get(context,"Input");
            using(var futureSteps=NativeCombatUsageChecks.ObserveDrawSteps())
            {
            var harmony=new Harmony("JueMingR.Tests.ImpactVisual");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})harmony.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            harmony.Patch(attack.GetType().GetProperty("ExpectedPreview",Flags).GetGetMethod(true),prefix:new HarmonyMethod(typeof(NativeCombatImpactVisualChecks).GetMethod("ReadFault",Flags)));
            harmony.Patch(attack.GetType().GetMethod("PreviewCurrent",Flags),prefix:new HarmonyMethod(typeof(NativeCombatImpactVisualChecks).GetMethod("ReadFault",Flags)));
            harmony.Patch(attack.GetType().GetProperty("ExpectedImpact",Flags).GetGetMethod(true),prefix:new HarmonyMethod(typeof(NativeCombatImpactVisualChecks).GetMethod("ReadFault",Flags)));
            harmony.Patch(attack.GetType().GetMethod("PresentationCurrent",Flags),prefix:new HarmonyMethod(typeof(NativeCombatImpactVisualChecks).GetMethod("ReadFault",Flags)));
            harmony.Patch(impact.GetType().GetMethod("Segment",Flags),prefix:new HarmonyMethod(typeof(NativeCombatImpactVisualChecks).GetMethod("DrawFault",Flags)));
            try
            {
                Set(host,"LayerStatus",Enum.Parse(Get(host,"LayerStatus").GetType(),"Ready"));Main.screenWidth=960;Main.screenHeight=640;Main.UIScale=1;
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(810,646);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;Array.Clear(n.immune,0,n.immune.Length);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));
                var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
                foreach(float gravity in new[]{1f,-1f})foreach(float scale in new[]{.8f,1.4f})
                {
                    p.gravDir=gravity;Main.screenPosition=new Vector2(600,400);Main.GameViewMatrix.Zoom=Vector2.One;
                    NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");PrepareDisplayFixture(context,host,attack,combat,input,n);
                    var contact=(AttackContact)(GetOptional(attack,"ExpectedImpact")??GetOptional(attack,"ExpectedPreview"));var actual=(AttackContact)Get(impact,"contact");Require(contact!=null && ReferenceEquals(contact,actual) && contact.Timeline.Identity.Equals((JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target")) && contact.ReceiveTick>=0 && contact.ReceiveTick<contact.Timeline.Count,"render fixture captures the independently selected legal execution-first contact and its full receiver");
                    // Change camera AFTER preparation. The oracle uses the final
                    // camera independently of the production marker projection.
                    Main.screenPosition=new Vector2(640,420);Main.GameViewMatrix.Zoom=new Vector2(scale);var zoom=Main.GameViewMatrix.ZoomMatrix;
                    int steps=NativeCombatUsageChecks.ObservedNpcSteps;var pixels=graphics.Pixels(()=>Call(world,"Draw"),zoom);AssertReceiveRectanglePixels(contact,pixels,zoom,"final camera gravity="+gravity+" zoom="+scale);
                    Require(pixels.Any(PathInk) && NativeCombatUsageChecks.ObservedNpcSteps==steps,"path survives and actual Draw does not advance prediction");
                    graphics.Image(Path.Combine(output,"impact-"+gravity+"-"+scale+".png"),()=>Call(world,"Draw"),zoom);
                    Console.WriteLine("PASS actual impact rectangle Draw gravity="+gravity+" zoom="+scale+" redPixels="+pixels.Count(Red));
                }
                CheckRectangleRepresentatives(context,host,attack,combat,input,n,p,world,graphics,output);
                p.gravDir=1;Main.screenPosition=new Vector2(600,400);Main.GameViewMatrix.Zoom=Vector2.One;
                foreach(bool path in new[]{false,true})foreach(bool aim in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,path,false,false,false,25,false,aim));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");PrepareDisplayFixture(context,host,attack,combat,input,n);var pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                    Require(pixels.Any(Red)==path,"held-weapon preview follows its independent path demand with aim ON or OFF");if(path)Require(pixels.Any(PathInk),"path remains independently visible with aim OFF");
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");
                foreach(string stage in new[]{"capture","project","draw"})
                {
                    Call(host,"Set",6,true);p.releaseUseItem=true;PrepareDisplayFixture(context,host,attack,combat,input,n);var healthyContact=Get(attack,"ExpectedImpact");faults=0;
                    readFault=stage!="draw";drawFault=stage=="draw";bool leaked=false;
                    try{if(stage=="capture")Call(world,"Prepare");else graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);}catch(TargetInvocationException){leaked=true;}
                    Require(!leaked && (bool)Get(impact,"Failed") && !(bool)Get(world,"Failed") && !(bool)Get(attack,"Failed"),"impact "+stage+" failure is local to red presentation");
                    for(int i=0;i<3;i++){Call(world,"PollResources");Call(world,"Prepare");var pixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Require(!pixels.Any(Red) && pixels.Any(PathInk),"healthy path survives latched impact failure");}
                    Require(faults==1,"impact failure does not retry on each prepare/draw with injection still armed");readFault=drawFault=false;Call(host,"Set",6,true);Require(ReferenceEquals(healthyContact,Get(attack,"ExpectedImpact")),"display-only retry preserves the healthy prepared attack capability");Call(world,"Prepare");Require(!(bool)Get(impact,"Failed") && graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix).Any(Red),"explicit Aim retry restores only failed red display");
                }
                p.inventory[0].SetDefaults(39);p.inventory[54].SetDefaults(40);p.inventory[54].stack=999;p.releaseUseItem=true;n.position=new Vector2(1060,646);NativeToolExecutionChecks.Sample(context,input,n.Center,false);Call(combat,"Sample");PrepareDisplayFixture(context,host,attack,combat,input,n,false);var arc=(AttackContact)Get(attack,"ExpectedPreview");Require(arc!=null && Math.Abs(arc.AimY-arc.ImpactY)>12,"idle bow render fixture separates elevated AimInput from contact");var arcPixels=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);AssertReceiveRectanglePixels(arc,arcPixels,Main.GameViewMatrix.ZoomMatrix,"idle bow receiver instead of elevated aim input/contact edge");graphics.Image(Path.Combine(output,"impact-bow.png"),()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");PrepareDisplayFixture(context,host,attack,combat,input,n);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});Require(GetOptional(attack,"ExpectedImpact")==null,"real Shot retires its consumed actual Contact");Call(host,"Update",(ulong)Main.GameUpdateCount);Call(world,"Prepare");var consumed=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);
                // Consumption does not admit a new optional solve in the same
                // update. Observe the existing bounded scheduler over real
                // native updates, rather than require an immediate red frame.
                int recovery=0;
                while((!consumed.Any(Red) || !consumed.Any(PathInk)) && recovery<12)
                {recovery++;NativeCombatSelectionPreviewChecks.Step(context,n.Center,false,true);consumed=graphics.Pixels(()=>Call(world,"Draw"),Main.GameViewMatrix.ZoomMatrix);Console.WriteLine("IMPACT recovery step="+recovery+" guc="+Main.GameUpdateCount+" target="+Get(Get(host,"Selection"),"HasTarget")+" path="+(GetOptional(host,"DisplayPath")!=null)+" red="+consumed.Any(Red)+" npcActive="+n.active+" life="+n.life+" itemTime="+p.itemTime+" animation="+p.itemAnimation+" preview="+(GetOptional(attack,"ExpectedPreview")!=null)+" workMs="+Get(Get(host,"PredictionWork"),"UsedMilliseconds"));}
                Require(consumed.Any(Red) && consumed.Any(PathInk),"bounded scheduled observation recovers a useful preview after real Shot consumption while the path survives");
                var recovered=(AttackContact)(GetOptional(attack,"ExpectedImpact")??GetOptional(attack,"ExpectedPreview"));AssertReceiveRectanglePixels(recovered,consumed,Main.GameViewMatrix.ZoomMatrix,"post-consumption scheduled receive rectangle");Console.WriteLine("IMPACT consumption recovery realUpdates="+recovery);
                Console.WriteLine("PASS impact actual World.Draw pixels, final camera, toggles, consumption and Capture/Project/Draw local latch/recovery. Render fixture, not Main/game execution.");
                NativeCombatAttackCostChecks.Draw(context,graphics);
            }
            finally{readFault=drawFault=false;harmony.UnpatchAll(harmony.Id);Main.LocalPlayer.gravDir=1;Main.GameViewMatrix.Zoom=Vector2.One;NativeCombatObservationChecks.Save(host,new ObservationOptions());Call(world,"Recover");}
            }
            if(Environment.GetEnvironmentVariable("JMR_IMPACT_VISUAL_ONLY")=="component")return;
            // The default scope owns the natural source/presentation chain
            // too; selectors only shorten local runs, never define coverage.
            NativeCombatUsageChecks.Run(context,graphics,Path.Combine(output,"natural-usage"));
            NativeCombatNaturalImpactChecks.Run(context,graphics,Path.Combine(output,"natural-slime"));
        }
    }
}
