using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // These isolated stationary receivers test selection and the actual outer
    // preparation/display boundary. NPC family motion has separate original
    // oracles; no synthetic contact or private Solve is inserted here.
    internal static class NativeCombatSelectionPreviewChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static bool injectPreviewFault;private static int previewFaults;
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            try
            {
                NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                RankedChanges(context,host,input);
                foreach(bool mouse in new[]{false,true})foreach(bool clear in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(550,400);
                    var a=Scene(2,new Vector2(800,667));var b=Scene(3,new Vector2(600,667));
                    for(int y=0;y<Main.maxTilesY;y++){Main.tile[47,y].active(true);Main.tile[47,y].type=1;}
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,clear,mouse,false,50,true,true));
                    for(int i=0;i<3;i++)Step(context,a.Center,false);
                    bool originalThree=Selected(host)==(clear?3:2);
                    Console.WriteLine("SELECTION COLD originalThreeSignal="+originalThree+" mouse="+mouse+" clear="+clear+" selected="+Selected(host)+"; clear-line signal is observation-only, not a cold three-update deadline");
                    if(clear)
                        RankedAdvance(context,host,a.Center,3,true,"near blocked A/far actual reachable B respects centre and mode at a real fair preparation opportunity: mouse="+mouse,false);
                    else Require(originalThree,"nearest mode retains its centre-ranked target without a weapon-solve prerequisite: mouse="+mouse);
                    int selected=Selected(host);Step(context,a.Center,true);Require(Selected(host)==selected,"attack input does not change the selection contract");
                    // BeforeNpc releases reader0; the real display owns its
                    // validated immutable window independently of that reader.
                    var visible=GetOptional(host,"DisplayPath") as NpcTrajectory;
                    Console.WriteLine("SELECTION actual path mouse="+mouse+" clear="+clear+" selected="+selected+" display="+(visible==null?"none":visible.Identity.Slot+"/"+visible.SampleTick+"/"+visible.CaptureTick));
                    // Optional exhaustion may withdraw a display lease on the
                    // firing update. Observe one fixed two-period window and
                    // check every published owner, rather than treating that
                    // nullable cold frame as a guaranteed display deadline.
                    int visibleFrames=visible==null?0:1;
                    Require(visible==null || visible.Identity.Slot==selected,"The firing update cannot display another receiver's path.");
                    int period=(int)host.GetType().GetField("DisplayRefreshUpdates",Flags).GetRawConstantValue();
                    for(int i=0;i<period*2;i++)
                    {
                        Step(context,a.Center,false);Require(Selected(host)==selected,"Native cooldown retains the same ranked receiver.");
                        visible=GetOptional(host,"DisplayPath") as NpcTrajectory;
                        if(visible!=null){Require(visible.Identity.Slot==selected,"Every actual visible path uses the final shared selection.");visibleFrames++;}
                    }
                    Require(visibleFrames>0,"The fixed two-period window contains an actual path for the final shared selection.");
                    Console.WriteLine("PASS finite actual candidate selection mouse="+mouse+" clear="+clear+" selected="+selected);
                }
                foreach(int weapon in new[]{3278,5669})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.position=new Vector2(700,646);Main.screenPosition=new Vector2(550,400);
                    var a=Scene(2,new Vector2(1250,667));Scene(3,new Vector2(780,667));
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,50,true,true));Step(context,a.Center,false);
                    Require(Selected(host)==2,"nearest mouse target remains selected outside current yoyo/melee reach: "+weapon);
                }
                if((bool)Get(attack,"PreviewDemand"))
                {Idle(context,combat,host,input,attack);StaticImmunity(context,combat,host,input,attack);PreviewFaultIsolation(context,combat,host,input,attack);}
                else Require(GetOptional(attack,"ExpectedPreview")==null,"retired hypothetical display remains absent through selection changes");
            }
            finally{NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static void RankedChanges(object context,object host,object input)
        {
            foreach(bool mouse in new[]{false,true})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);
                p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(3104);p.inventory[54].stack=1;Main.screenPosition=new Vector2(350,400);
                var near=Scene(2,new Vector2(810,667));var middle=Scene(3,new Vector2(870,667));var far=Scene(4,new Vector2(460,667));var cursor=new Vector2(725,667);
                for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(47,y,1);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,mouse,false,50,true,false));
                var attack=Get(Get(context,"Combat"),"Attack");int cadence=(int)host.GetType().GetField("DisplayRefreshUpdates",Flags).GetRawConstantValue();
                // The owner requires cold work to remain visible in evidence,
                // not a cold eleven-update guarantee derived from cadence.
                // Preserve that exact diagnostic window, then establish a
                // separate fixed warm fixture before checking changed facts.
                // Neither interval grants BeforeNpc input or restarts a budget.
                bool coldWindowMet=false;
                for(int i=0;i<cadence*2+1;i++)
                {
                    Step(context,cursor,false,true);
                    coldWindowMet|=Selected(host)==4 && RankedSolution(host)!=null;
                    var farPlan=((Array)Get(attack,"candidatePlans")).GetValue(8);var farFacts=farPlan==null?default(NativeCombatAimChecks.SolutionFacts):NativeCombatAimChecks.ReadPlanSolution(attack,farPlan);
                    Console.WriteLine("RANKED POSTSTEP index="+i+" GUC="+Main.GameUpdateCount+" selected="+Selected(host)+" reachable="+Get(Get(host,"Selection"),"ClearLine")+" display="+(((NpcTrajectory)GetOptional(host,"DisplayPath"))?.Count??0)+" optionalTick="+Get(host,"optionalTick")+" farPrepared="+(farPlan!=null && (bool)Get(farPlan,"Prepared"))+" preparedTick="+(farPlan==null?0:Get(farPlan,"PreparedTick"))+" phase="+(farPlan==null?null:Get(farPlan,"Phase"))+" sample="+(farFacts.Timeline==null?-1:farFacts.Timeline.SampleTick)+" receive="+farFacts.ReceiveTick+" work="+Get(Get(host,"PredictionWork"),"UsedMilliseconds")+" exceeded="+Get(Get(host,"PredictionWork"),"Exceeded"));
                }
                Console.WriteLine("RANKED COLD DIAGNOSTIC updates="+(cadence*2+1)+" originalElevenUpdateSignal="+coldWindowMet+"; observation-only, not a product deadline or warm PASS");
                int warmProofs=0,recentWarmProofs=0,firstWarmProof=-1,hardWarmExits=0,warmDisplaySteps=0,warmDisplayGap=0,maxWarmDisplayGap=0,minimumDisplayCount=int.MaxValue,maximumDisplayCount=0;double maximumWarmWork=0;
                for(int i=0;i<60;i++)
                {
                    Step(context,cursor,false,true);
                    bool qualified=Selected(host)==4 && RankedSolution(host)!=null;
                    if(qualified){warmProofs++;if(firstWarmProof<0)firstWarmProof=i;if(i>=60-cadence)recentWarmProofs++;}
                    var display=GetOptional(host,"DisplayPath") as NpcTrajectory;
                    bool visible=Selected(host)==4 && (bool)Get(Get(host,"Selection"),"ClearLine") && display!=null && display.Identity.Slot==4;
                    if(visible){warmDisplaySteps++;warmDisplayGap=0;minimumDisplayCount=Math.Min(minimumDisplayCount,display.Count);maximumDisplayCount=Math.Max(maximumDisplayCount,display.Count);}else maxWarmDisplayGap=Math.Max(maxWarmDisplayGap,++warmDisplayGap);
                    var work=Get(host,"PredictionWork");if((bool)Get(work,"Exceeded"))hardWarmExits++;maximumWarmWork=Math.Max(maximumWarmWork,(double)Get(work,"UsedMilliseconds"));
                }
                var warmDisplay=GetOptional(host,"DisplayPath") as NpcTrajectory;
                Console.WriteLine("RANKED FIXED WARM SETUP updates=60 mouse="+mouse+" freshProofs="+warmProofs+" recentFiveUpdateProofs="+recentWarmProofs+" firstProofIndex="+firstWarmProof+" hardExits="+hardWarmExits+" maximumChargedMs="+maximumWarmWork+" selected="+Selected(host)+" reachable="+Get(Get(host,"Selection"),"ClearLine")+" displayIdentity="+(warmDisplay==null?"none":warmDisplay.Identity.ToString())+" displayCount="+(warmDisplay?.Count??0)+" actualFarPathSteps="+warmDisplaySteps+" longestPathGap="+maxWarmDisplayGap+" pathCountRange="+(warmDisplaySteps==0?0:minimumDisplayCount)+".."+maximumDisplayCount);
                Require(warmProofs>0 && warmDisplaySteps>0,"fixed sixty-update window contains actual farther selection proof AND validated path, separately from later changed-fact setup");
                // A four-update display lease and a five-update optional
                // cadence can leave the fixed window's last update between
                // proofs. Keep all sixty observations (including that gap),
                // then establish the mutation test's CURRENT positive premise
                // under the same bounded cadence/fair-cycle contract below.
                // This does not move the measured window or grant extra work.
                RankedAdvance(context,host,cursor,4,true,"changed-fact setup obtains a current far proof within existing optional cadence: mouse="+mouse,false);
                warmDisplay=GetOptional(host,"DisplayPath") as NpcTrajectory;
                Require(Selected(host)==4 && (bool)Get(Get(host,"Selection"),"ClearLine") && warmDisplay!=null && warmDisplay.Identity.Slot==4,"changed-fact setup owns the selected reachable farther NPC and its actually validated path: mouse="+mouse);
                Console.WriteLine("RANKED mutation setup current far proof obtained; fixed sixty-update observations above remain unchanged");
                for(int y=0;y<Main.maxTilesY;y++)Main.tile[47,y].ClearEverything();
                RankedAdvance(context,host,cursor,2,true,"near target wins at the bounded optional refresh when its wall opens even while old far stays reachable: mouse="+mouse,false);
                for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(47,y,1);
                RankedAdvance(context,host,cursor,4,true,"closing the same wall retires near proof and restores the farther solution within optional cadence: mouse="+mouse);
                var ordinary=RankedSolution(host);
                p.inventory[54].SetDefaults(1302);p.inventory[54].stack=999;
                RankedAdvance(context,host,cursor,4,true,"high-velocity ammo refreshes the farther solution within optional cadence: mouse="+mouse);var fast=RankedSolution(host);
                Require(Selected(host)==4 && fast!=null && ordinary!=null && fast.Value.NativeTick<ordinary.Value.NativeTick,"actual high-velocity ammunition preserves wall tiers but replaces the receive time: mouse="+mouse);
                p.inventory[54].TurnToAir();RankedAdvance(context,host,cursor,2,false,"no usable ammunition leaves nearest fallback without retaining old proof: mouse="+mouse);
                p.inventory[54].SetDefaults(3104);p.inventory[54].stack=1;RankedAdvance(context,host,cursor,4,true,"ordinary Endless Pouch restores the actual far solution: mouse="+mouse);
                middle.Center=new Vector2(600,667);RankedAdvance(context,host,cursor,3,true,"a moved formerly blocked middle candidate outranks old reachable far: mouse="+mouse);
                near.Center=new Vector2(660,667);RankedAdvance(context,host,cursor,2,true,"nearer movement is re-ranked within the reachable tier: mouse="+mouse);
                near.Center=new Vector2(810,667);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,mouse,false,50,true,false));
                Step(context,cursor,false,true);Require(Selected(host)==2,"nearest mode ignores the existing wall and usable farther attack: mouse="+mouse);
                Console.WriteLine("PASS three-candidate ranking/open-close wall/real ammo/movement/nearest: mouse="+mouse+" farStillActive="+far.active);
            }
        }
        private static void RankedAdvance(object context,object host,Vector2 cursor,int expected,bool contactExpected,string reason,bool retirePrior=true)
        {
            var attack=Get(Get(context,"Combat"),"Attack");var prior=RankedSolution(host);
            Step(context,cursor,false,true);
            // Opening the near wall changes distance competition, not the
            // still-reachable far contact's own dependencies. Closing a wall,
            // changing ammo or an intervening receiver invalidates old proof.
            Require(!retirePrior || prior==null || !SameRankedProof(prior,RankedSolution(host)),"invalidated hand/receiver facts immediately revoke the old candidate proof before optional re-solving");
            // Fairness advances on admitted World passes, not intervening
            // native updates. ResolveSelection alternates reserved-oldest and
            // changed-fact priority, so one full round has at most 2*N passes.
            // The actual cadence converts that round to a finite no-progress
            // backstop. This does NOT prove a solve fits a pass: this fixed
            // stationary gun fixture must still obtain its real fresh positive
            // and validated display, or fail. No validation read forces work.
            int cadence=(int)host.GetType().GetField("DisplayRefreshUpdates",Flags).GetRawConstantValue();
            int candidates=(int)Get(Get(host,"Selection"),"CandidateCount");
            int fairPasses=2*Math.Max(1,candidates),maximumUpdates=cadence*fairPasses,updates=1;
            long firstPass=(long)Get(attack,"candidateWorldPass");
            for(int remaining=maximumUpdates;remaining>0;remaining--)
            {
                var contact=RankedSolution(host);
                if(Selected(host)==expected && (contactExpected?contact!=null && contact.Value.Timeline.Identity.Slot==expected:contact==null))
                {RankedReceipt(host,reason,updates,(long)Get(attack,"candidateWorldPass")-firstPass,fairPasses,contact);return;}
                if((long)Get(attack,"candidateWorldPass")-firstPass>=fairPasses)break;
                Step(context,cursor,false,true);
                updates++;
            }
            var result=RankedSolution(host);
            bool reached=Selected(host)==expected && (contactExpected?result!=null && result.Value.Timeline.Identity.Slot==expected:result==null);
            RankedReceipt(host,reason,updates,(long)Get(attack,"candidateWorldPass")-firstPass,fairPasses,result);
            if(!reached)SelectionState(context,"finite-fair-round-failed:"+reason,updates,((ObservationOptions)Get(host,"Options")).MouseCenter,((ObservationOptions)Get(host,"Options")).ClearLine);
            Require(reached,reason);
        }
        private static void RankedReceipt(object host,string reason,int updates,long passes,int limit,NativeCombatAimChecks.SolutionFacts? solution)
        {Console.WriteLine("RANKED FAIR RESULT reason="+reason+" updates="+updates+" actualWorldPasses="+passes+" fairPassLimit="+limit+" selected="+Selected(host)+" freshProof="+solution.HasValue+" receive="+(solution.HasValue?solution.Value.ReceiveTick:-1)+" display="+SelectionPath(NativeCombatPresentationChecks.CurrentPath(host)));}
        private static bool SameRankedProof(NativeCombatAimChecks.SolutionFacts? a,NativeCombatAimChecks.SolutionFacts? b)
        {return a.HasValue && b.HasValue && ReferenceEquals(a.Value.Timeline,b.Value.Timeline) && a.Value.Point==b.Value.Point && a.Value.ReceiveTick==b.Value.ReceiveTick && a.Value.Delay==b.Value.Delay;}
        private static NativeCombatAimChecks.SolutionFacts? RankedSolution(object host)
        {
            var attack=Get(host,"Attack");var identity=(NpcIdentity)Get(Get(host,"Selection"),"Target");
            var display=NativeCombatPresentationChecks.CurrentPath(host);
            if(display==null || !display.Identity.Equals(identity))return null;
            // Admission is cleared by ResolveSelection. An admitted old plan is
            // usable only after that selection's dependency checks; raw plans
            // otherwise need this GUC's completed World preparation.
            var admitted=GetOptional(attack,"candidatePresentationPlan");
            if(admitted!=null && (bool)Get(admitted,"Prepared") && Get(admitted,"Phase").ToString()=="CompletedWorld")
            {var facts=NativeCombatAimChecks.ReadPlanSolution(attack,admitted);if(RankedPositive(facts,identity))return facts;}
            var plans=(Array)Get(attack,"candidatePlans");var plan=plans.GetValue(identity.Slot*2);
            if(plan==null || !(bool)Get(plan,"Prepared") || (uint)Get(plan,"PreparedTick")!=Main.GameUpdateCount || Get(plan,"Phase").ToString()!="CompletedWorld")return null;
            var value=NativeCombatAimChecks.ReadPlanSolution(attack,plan);
            return RankedPositive(value,identity) && value.Timeline.SampleTick==(long)Main.GameUpdateCount?(NativeCombatAimChecks.SolutionFacts?)value:null;
        }
        private static bool RankedPositive(NativeCombatAimChecks.SolutionFacts value,NpcIdentity identity)
        {return value.HasPoint && value.Reachability=="Reachable" && value.Timeline!=null && value.Timeline.Identity.Equals(identity) && value.ReceiveTick>=value.FirstTick && value.ReceiveTick<value.Timeline.Count && value.NativeTick>0 && !float.IsNaN(value.Point.X) && !float.IsNaN(value.Point.Y) && !float.IsInfinity(value.Point.X) && !float.IsInfinity(value.Point.Y);}
        private static void Idle(object context,object combat,object host,object input,object attack)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);var n=Scene(2,new Vector2(910,667));
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,false));
            int stack=p.inventory[54].stack,cycling=p.ammoCyclingOffset;
            for(int i=0;i<4;i++)
            {
                Step(context,new Vector2(650,550),false,true);var result=(AttackContact)GetOptional(attack,"ExpectedPreview");
                Require(result!=null && result.ReceiveTick>=0 && result.ReceiveTick<result.Timeline.Count,"idle held weapon creates a real read-only preview with Aim OFF");
                var impact=Get(Get(host,"World"),"Impact");Call(impact,"Capture");var zoom=Main.GameViewMatrix.ZoomMatrix;Call(impact,"Project",zoom,Matrix.Invert(zoom));
                var point=result.ReceiveBounds;var expected=Vector2.Transform(new Vector2(point.CenterX,point.CenterY)-Main.screenPosition,zoom);
                Require((bool)Get(impact,"Visible") && Vector2.DistanceSquared((Vector2)Get(impact,"Position"),expected)<.001f,"red marks the receive-time NPC path centre");
                Require(GetOptional(attack,"ExpectedImpact")==null && !p.controlUseItem && !p.channel && !Main.projectile.Any(q=>q.active),"preview has no native input capability or projectile birth");
            }
            Require(stack==p.inventory[54].stack && cycling==p.ammoCyclingOffset,"idle preview preserves resources and ammunition cycling across natural updates");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,true));Step(context,new Vector2(650,550),false);var preview=(AttackContact)Get(attack,"ExpectedPreview");Call(Get(Get(host,"World"),"Impact"),"Capture");
            Step(context,new Vector2(650,550),true);Require(GetOptional(attack,"ExpectedPreview")!=null,"ordinary cooldown retains a legal delayed preview after a natural shot");
            Require(((AttackContact)Get(attack,"ExpectedPreview")).Delay>0,"ordinary cooldown is represented as an actual future launch delay");
            Console.WriteLine("PASS natural idle path-only preview, centre/time mapping, pure observation and post-shot delayed preview");
        }
        private static void StaticImmunity(object context,object combat,object host,object input,object attack)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,1446,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.statMana=p.statManaMax=p.statManaMax2=200;Main.screenPosition=new Vector2(600,400);var n=NativeCombatControlPreviewChecks.Dummy(2,52,40);
            var shot=ContentSamples.ProjectilesByType[297];uint old=Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,2];
            try
            {
                Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,2]=0;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                for(int i=0;i<3;i++)Step(context,n.Center,false,true);
                Require(GetOptional(attack,"ExpectedPreview")!=null,"idle Spectre preview reaches the native placed dummy before a foreign static-immunity wait");
                // Arrange an already-observed foreign hit wait, then let the
                // actual outer clock expire it without rewriting its timestamp.
                uint expiry=Main.GameUpdateCount+130;Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,2]=expiry;
                Step(context,n.Center,false,true);Require(GetOptional(attack,"ExpectedPreview")==null,"a changed selected static immunity invalidates the old contact");
                int recovered=-1;
                for(int i=0;i<132;i++)
                {Step(context,n.Center,false,true);if(recovered<0 && GetOptional(attack,"ExpectedPreview")!=null)recovered=i;Require(Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,2]==expiry,"natural time advances independently of the stored static expiry");}
                Require(recovered>=0 && GetOptional(attack,"ExpectedPreview")!=null,"the same static expiry becomes a usable receive window again without stale null reuse");
                Console.WriteLine("PASS natural preview static-immunity dependency/expiry recovery: item=1446 projectile=297 recovered="+recovered);
            }
            finally{Projectile.perIDStaticNPCImmunity[shot.immunityIdentity,2]=old;}
        }
        private static void PreviewFaultIsolation(object context,object combat,object host,object input,object attack)
        {
            var audit=new Harmony("JueMingR.Tests.SelectionPreviewFault");
            audit.Patch(attack.GetType().GetMethod("PreparePlan",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSelectionPreviewChecks).GetMethod("PreviewFault",Flags)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);var n=Scene(2,new Vector2(910,667));
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,false));injectPreviewFault=true;previewFaults=0;
                for(int i=0;i<4;i++)Step(context,n.Center,false,true);
                Require(previewFaults==1 && !(bool)Get(attack,"Failed") && GetOptional(attack,"ExpectedPreview")==null && ((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(0)!=null,"same preview fault is tried once, leaving execution and the shared path healthy");
                injectPreviewFault=false;p.inventory[0]=new Item();p.inventory[0].SetDefaults(95);Step(context,n.Center,false,true);
                Require(GetOptional(attack,"ExpectedPreview")!=null && !(bool)Get(attack,"Failed"),"new healthy held instance recovers preview automatically");
                injectPreviewFault=true;Step(context,n.Center,false);Require(previewFaults==2,"new concrete context gets one bounded preview attempt");injectPreviewFault=false;
                var oldCenter=n.Center;typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{2,(byte)0});n=Scene(2,oldCenter);
                Step(context,n.Center,false,true);Require(GetOptional(attack,"ExpectedPreview")!=null,"new full target identity recovers preview without resetting the path");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,true,true));Step(context,n.Center,true);Require(Main.projectile.Any(q=>q.active && q.owner==0),"a recovered preview fault does not revoke the later real shot capability");
                Console.WriteLine("PASS natural preview fault isolation: bounded retry, healthy path/execution, new weapon and NPC identity recovery");
            }
            finally{injectPreviewFault=false;foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static void PreviewFault(){if(injectPreviewFault){previewFaults++;throw new InvalidOperationException("isolated preview preparation fault");}}
        internal static void Step(object context,Vector2 mouse,bool left,bool pureObservation=false,bool right=false)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");NativeToolExecutionChecks.Sample(context,input,mouse,left);if(right)Terraria.GameInput.PlayerInput.Triggers.Current.Right=true;Call(combat,"Sample");Call(host,"SampleMouse");int x=Main.mouseX,y=Main.mouseY;
            NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);
            foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);
            for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}Main.ProjectileUpdateLoopIndex=-1;
            var p=Main.LocalPlayer;int direction=p.direction,animation=p.itemAnimation,time=p.itemTime,mana=p.statMana,stack=p.inventory[54].stack,cycling=p.ammoCyclingOffset;bool use=p.controlUseItem,channel=p.channel;var random=Main.rand;
            var fields=typeof(Terraria.Utilities.UnifiedRandom).GetFields(Flags).Where(f=>!f.IsStatic).ToArray();var before=fields.Select(f=>Copy(f.GetValue(random))).ToArray();
            Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require(Main.mouseX==x && Main.mouseY==y,"physical cursor survives Update/ItemCheck/NPC/Projectile/presentation");
            if(pureObservation)Require(p.direction==direction && p.itemAnimation==animation && p.itemTime==time && p.statMana==mana && p.inventory[54].stack==stack && p.ammoCyclingOffset==cycling && p.controlUseItem==use && p.channel==channel && ReferenceEquals(random,Main.rand) && fields.Select((f,i)=>Equal(before[i],f.GetValue(random))).All(v=>v),"natural completed preview stage is read-only for input, facing, timers, resources and RNG");
        }
        private static NPC Scene(int slot,Vector2 centre)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=centre;n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);return n;}
        // Test-only facts after the real update owner has settled. These reads
        // never prepare a candidate, change a lease, or query input capability.
        private static void SelectionState(object context,string stage,int index,bool mouse,bool clear)
        {
            var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");var selection=Get(host,"Selection");var options=(ObservationOptions)Get(host,"Options");var player=Main.LocalPlayer;
            var display=GetOptional(host,"DisplayPath") as NpcTrajectory;var raw=((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(0);
            Console.WriteLine("SELECTION STATE stage="+stage+" index="+index+" tick="+Main.GameUpdateCount+" wantedMouse="+mouse+" wantedClear="+clear+" actualMouse="+options.MouseCenter+" actualClear="+options.ClearLine+" hasTarget="+Get(selection,"HasTarget")+" selected="+Selected(host)+" reachable="+Get(selection,"ClearLine")+" candidates="+Get(selection,"CandidateCount")+" worldPass="+Get(attack,"candidateWorldPass")+" player="+player.Center+" cursor="+Main.MouseWorld+" ammo="+player.inventory[54].type+":"+player.inventory[54].stack+" timers="+player.itemAnimation+"/"+player.itemTime+" basicPermission="+Get(attack,"BasicPermission")+" urgent="+Get(attack,"UrgentPreparation")+" optional="+Get(host,"OptionalPreparation")+" optionalTick="+Get(host,"optionalTick")+" leaseTick="+Get(host,"displayLeaseTick")+" display="+SelectionPath(display)+" raw="+SelectionPath(raw)+" work="+Get(Get(host,"PredictionWork"),"UsedMilliseconds")+" hard="+Get(Get(host,"PredictionWork"),"Exceeded"));
            var plans=(Array)Get(attack,"candidatePlans");var unknown=(bool[])Get(attack,"candidateUnknown");var attempts=(long[])Get(attack,"candidateAttempt");var requests=(int[])Get(attack,"candidateRequests");var depths=(int[])Get(attack,"candidateDepths");
            foreach(int slot in new[]{2,3,4})
            {
                int key=slot*2;var plan=plans.GetValue(key);bool prepared=plan!=null && (bool)Get(plan,"Prepared");var facts=prepared?NativeCombatAimChecks.ReadPlanSolution(attack,plan):default(NativeCombatAimChecks.SolutionFacts);
                Console.WriteLine("SELECTION CANDIDATE stage="+stage+" index="+index+" slot="+slot+" active="+Main.npc[slot].active+" position="+Main.npc[slot].Center+" unknown="+unknown[key]+" attempt="+attempts[key]+" request="+requests[key]+" depth="+depths[key]+" prepared="+prepared+" preparedTick="+(plan==null?-1:Get(plan,"PreparedTick"))+" phase="+(plan==null?"none":Get(plan,"Phase"))+" route="+facts.Route+" reachable="+facts.Reachability+" first="+facts.FirstTick+" receive="+facts.ReceiveTick+" native="+facts.NativeTick+" timeline="+SelectionPath(facts.Timeline));
            }
        }
        private static string SelectionPath(NpcTrajectory path)
        {return path==null?"none":path.Identity.Slot+"/"+path.SampleTick+"/"+path.CaptureTick+"/"+path.Count+"/"+path.Stop+"/paused="+path.BudgetPaused;}
        private static int Selected(object host){return ((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot;}
        private static object Copy(object value){var a=value as Array;return a==null?value:a.Clone();}
        private static bool Equal(object a,object b){var x=a as Array;var y=b as Array;return x==null?Equals(a,b):y!=null && x.Cast<object>().SequenceEqual(y.Cast<object>());}
    }
}
