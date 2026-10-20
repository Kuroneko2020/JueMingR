using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatCandidateDemandChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NpcPredictionCache watchCache;private static int window,count;
        private static void Before(Player __instance){if(__instance.whoAmI==0 && watchCache!=null){window=watchCache.Required;count=watchCache.Read(1)?.Count??0;}}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatCadenceChecks.Save(combat,new CombatOptions());NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);
            float range=ProjectileID.Sets.YoyosMaximumRange[p.HeldItem.shoot];var far=Scene(2,p.Center+new Vector2(range+90,0));var near=Scene(3,p.Center+new Vector2(range/2,0));
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,25,true,true));NativeToolExecutionChecks.Sample(context,input,far.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);
            var selected=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target");Console.WriteLine("CANDIDATE yoyo range="+range+" target="+selected.Slot+" near="+near.Center+" far="+far.Center);Require(selected.Slot==2,"nearest mode retains the actual mouse-nearest target even outside yoyo attack reach");
            near.active=false;far.width=80;far.height=40;far.Center=p.Center+new Vector2(range+10,0);NativeToolExecutionChecks.Sample(context,input,far.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);selected=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target");Require(selected.Slot==2,"large receiver edge remains in range even when its centre is outside");
            Console.WriteLine("PASS strict nearest yoyo target and legal large receiver edge; attack reach does not change receiver qualification");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            SameObstacle(context,combat,host,input);ShortDemand(context,combat,host,input);BudgetPlanRetry(context);NativeCombatCurrentDemandChecks.Run(context);RunStress(context);
        }
        private static object retryPlan;private static bool interruptRetry;private static int retrySolves;
        private static void BeforeRetrySolve()
        {
            retrySolves++;
            if(!interruptRetry)return;
            interruptRetry=false;
            Require((bool)Get(retryPlan,"Prepared"),"The deterministic interruption occurs after input preparation and before solve completion.");
            throw new CombatPredictionBudgetExceededException();
        }
        // A component recovery counterexample, not a native consumer/timing
        // result. The trajectory is naturally published; only one budget signal
        // at the solve boundary is injected, never a Contact or input demand.
        private static void BudgetPlanRetry(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Scene(2,new Vector2(870,667));
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,50,true,true));
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");NpcTrajectory published=null;
            int cadence=(int)host.GetType().GetField("DisplayRefreshUpdates",Flags).GetRawConstantValue();
            // This component can run directly before its mother suite warms
            // native preparation. Wait only through two normal display periods;
            // never manufacture a Source publication or a native use.
            for(int i=0;i<cadence*2+1 && (published==null || published.Count<=32);i++)
            {NativeCombatSelectionPreviewChecks.Step(context,new Vector2(550,600),false,true);published=cache.Read(0);}
            Require(published!=null,"Budget retry first acquires a naturally published trajectory within bounded optional preparation.");
            // The recovery component needs a longer real fixture than display
            // is obliged to produce in its shared slice. Its existing isolated
            // component allowance prepares that exact live Source directly;
            // this is neither an optional-latency nor native-use assertion.
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
            {Call(Get(host,"Prediction"),"PreparePrefix",published.Identity,(long)Main.GameUpdateCount,NpcPredictionCache.Horizon,NpcPredictionCache.Horizon);published=cache.Read(0);}
            Require(published!=null && published.Count>32,"The real component Source publishes its complete recovery-test range without a fabricated trajectory.");
            var phase=Enum.Parse(Get(Get(attack,"preview"),"Phase").GetType(),"CompletedWorld");retryPlan=Activator.CreateInstance(Get(attack,"preview").GetType(),true);
            var progressPlans=(Array)Get(attack,"candidatePlans");var progressRequests=(int[])Get(attack,"candidateRequests");var progressUnknown=(bool[])Get(attack,"candidateUnknown");var progressSignificant=(bool[])Get(attack,"candidateSignificant");var progressAttempts=(long[])Get(attack,"candidateAttempt");
            int progressKey=published.Identity.Slot*2;object priorProgressPlan=progressPlans.GetValue(progressKey);int priorProgressRequest=progressRequests[progressKey];bool priorProgressUnknown=progressUnknown[progressKey],priorProgressSignificant=progressSignificant[published.Identity.Slot];long priorProgressAttempt=progressAttempts[progressKey];
            var audit=new Harmony("JueMingR.Tests.IncompletePlanRetry");
            try
            {
                interruptRetry=false;retrySolves=0;
                audit.Patch(attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostRepresentativeAttack",true).GetMethod("PrepareMotion",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(BeforeRetrySolve)));
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
                {
                    Call(attack,"PreparePlan",retryPlan,published,phase);var real=NativeCombatAimChecks.ReadPlanSolution(attack,retryPlan);
                    Require((bool)Get(retryPlan,"Prepared") && real.HasPoint && real.Reachability=="Reachable" && real.Timeline!=null && real.ReceiveTick>=real.FirstTick && real.ReceiveTick<real.Timeline.Count,"The same ordinary component completes a finite representative receive before interruption.");
                    NpcTrajectory contactPrefix;
                    Require(published.TryWindow(published.SampleTick,real.ReceiveTick+1,published.Version,out contactPrefix),"The actual receive has a complete immutable prefix inside its native publication.");
                    Call(retryPlan,"Clear");Call(attack,"PreparePlan",retryPlan,contactPrefix,phase);var shorter=NativeCombatAimChecks.ReadPlanSolution(attack,retryPlan);
                    Require((bool)Get(retryPlan,"Prepared") && shorter.HasPoint && shorter.Reachability=="Reachable" && shorter.ReceiveTick<contactPrefix.Count,"The representative completes on its actual shorter receive prefix.");
                    var borrow=attack.GetType().GetMethod("TryCandidatePresentationPrefix",Flags);var borrowArgs=new object[]{retryPlan,published,null};
                    Require((bool)borrow.Invoke(null,borrowArgs) && AttackIntercept.Equivalent(shorter.Timeline,(NpcTrajectory)borrowArgs[2]),"A completed candidate borrows its entire equal shorter prefix without another solve.");
                    NpcTrajectory insufficient;Require(published.TryWindow(published.SampleTick,0,published.Version,out insufficient),"The publication supplies an independently too-short view.");
                    borrowArgs=new object[]{retryPlan,insufficient,null};Require(!(bool)borrow.Invoke(null,borrowArgs),"A shorter selected publication cannot borrow the longer candidate coverage.");
                    NpcTrajectory otherSample;Require(published.TryWindow(published.SampleTick+1,real.ReceiveTick,published.Version,out otherSample),"The immutable publication can expose the next sample without a new capture.");
                    borrowArgs=new object[]{retryPlan,otherSample,null};Require(!(bool)borrow.Invoke(null,borrowArgs),"A different sample never borrows the old presentation even with equal geometry.");
                    var source=Get(host,"Prediction");var motionLease=Call(source,"FreezeCandidateTerrain",published);
                    Require(motionLease!=null && Call(source,"FreezeCandidateTerrain",contactPrefix)==null,"Only the exact currently owned Source publication can freeze its NPC-motion terrain; a derived view cannot claim scratch ownership.");
                    var presentationField=attack.GetType().GetField("candidatePresentationPlan",Flags);var priorPresentation=presentationField.GetValue(attack);
                    try
                    {
                        presentationField.SetValue(attack,retryPlan);retryPlan.GetType().GetField("MotionTerrain",Flags).SetValue(retryPlan,motionLease);
                        var displayArgs=new object[]{(long)Main.GameUpdateCount,null,null};
                        Require((bool)attack.GetType().GetMethod("TryCandidateDisplay",Flags).Invoke(attack,displayArgs),"The same completed World candidate can supply its pure-observation path and exact motion lease.");
                        int borrowedSolves=retrySolves;
                        host.GetType().GetMethod("CaptureDisplay",Flags,null,new[]{typeof(NpcTrajectory),motionLease.GetType()},null).Invoke(host,new[]{displayArgs[1],motionLease});
                        Call(host,"ValidateDisplay",(long)Main.GameUpdateCount);
                        Require(ReferenceEquals(GetOptional(host,"DisplayPath"),displayArgs[1]) && retrySolves==borrowedSolves && ReferenceEquals(cache.Read(0),published) && !NativeCombatAimChecks.ReadSolution(attack).HasPoint && !(bool)Get(attack,"hasConsumedInput") && GetOptional(attack,"ExpectedPreview")==null && !p.controlUseItem,"Publishing the completed NPC path performs zero hand solves, preserves the selected Source publication and grants neither input nor retired preview.");
                    }
                    finally{presentationField.SetValue(attack,priorPresentation);Call(attack,"ClearPreview");}
                    Call(retryPlan,"Clear");interruptRetry=true;retrySolves=0;
                    bool paused=false;try{Call(attack,"PreparePlan",retryPlan,published,phase);}catch(Exception error){if(error.GetBaseException() is CombatPredictionBudgetExceededException)paused=true;else throw;}
                    Require(paused && retrySolves==1 && !(bool)Get(retryPlan,"Prepared") && GetOptional(retryPlan,"Contact")==null && GetOptional(retryPlan,"Timeline")==null,"Interrupted null is unresolved and clears the plan rather than caching a completed miss.");
                    // This isolated work-policy check borrows the same real
                    // plan and published trajectory; it creates no Contact or
                    // native input opportunity and restores candidate ownership.
                    progressPlans.SetValue(retryPlan,progressKey);progressRequests[progressKey]=32;progressUnknown[progressKey]=true;progressSignificant[published.Identity.Slot]=false;
                    Require((int)Call(attack,"CandidateProgressRequired",published.Identity,progressKey,false)==32,"A budget-interrupted Plan retries its admitted request instead of doubling Source work before its missing solve.");
                    Require(ReferenceEquals(cache.Read(0),published) && !(bool)Get(attack,"Failed"),"The isolated plan interruption preserves Source's published prefix and does not record an attack fault.");
                    Call(attack,"PreparePlan",retryPlan,published,phase);var recovered=NativeCombatAimChecks.ReadPlanSolution(attack,retryPlan);
                    Require((bool)Get(retryPlan,"Prepared") && recovered.HasPoint && recovered.Reachability=="Reachable" && recovered.ReceiveTick==real.ReceiveTick && recovered.Point==real.Point && retrySolves>1,"The identical sample/phase retries the real finite preparation and recovers its result.");
                    int before=retrySolves;Call(attack,"PreparePlan",retryPlan,published,phase);var repeated=NativeCombatAimChecks.ReadPlanSolution(attack,retryPlan);
                    // The contract permits either a lawful equal-premise reuse or a
                    // bounded recomputation. Neither zero nor positive work is
                    // a required result of repeating this explicit component.
                    Require((bool)Get(retryPlan,"Prepared") && repeated.HasPoint && repeated.Point==recovered.Point && repeated.ReceiveTick==recovered.ReceiveTick && AttackIntercept.Equivalent(repeated.Timeline,recovered.Timeline) && (bool)Get(Get(retryPlan,"Terrain"),"Unchanged") && (bool)Call(retryPlan,"PresentationMembersCurrent",p,0),"A repeated explicit preparation preserves the result and its actual sample/player/terrain premises, independently of lawful reuse.");
                    Console.WriteLine("CANDIDATE repeated completed sample="+published.SampleTick+" receive="+repeated.ReceiveTick+" samePoint="+(repeated.Point==recovered.Point)+" actualPrepareMotionDelta="+(retrySolves-before)+" workCountIsNotASuccessPredicate=True");
                    Call(retryPlan,"Clear");for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(48,y,1);
                    Call(attack,"PreparePlan",retryPlan,published,phase);var blocked=NativeCombatAimChecks.ReadPlanSolution(attack,retryPlan);
                    Require((bool)Get(retryPlan,"Prepared") && blocked.HasPoint && blocked.Reachability=="Blocked" && blocked.ReceiveTick<0 && blocked.Contact==null,"The real complete wall blocks the finite representative without fabricating a complete-world miss.");
                    Require((bool)Get(Get(retryPlan,"Terrain"),"Unchanged"),"The completed finite result retains its actual unchanged terrain lease.");
                    int fullFuture=published.Count-1;
                    Require(fullFuture>32 && !(bool)Call(attack,"CandidateMissCurrent",retryPlan,published.Identity,published.SampleTick,phase,fullFuture),"A Balanced finite block never authorizes an old complete-world miss lease.");
                    NpcTrajectory shortPrefix;Require(published.TryWindow(published.SampleTick,32,published.Version,out shortPrefix),"The publication supplies the original shorter prefix.");
                    Call(retryPlan,"Clear");Call(attack,"PreparePlan",retryPlan,shortPrefix,phase);var shortResult=NativeCombatAimChecks.ReadPlanSolution(attack,retryPlan);
                    Require((bool)Get(retryPlan,"Prepared") && shortResult.Timeline!=null && shortResult.Timeline.Count==shortPrefix.Count && shortResult.Reachability!="Reachable" && shortResult.Contact==null,"The original shorter prefix completes only its finite blocked/unknown result.");
                    Require(!(bool)Call(attack,"CandidateMissCurrent",retryPlan,shortPrefix.Identity,shortPrefix.SampleTick,phase,32) && !(bool)Call(attack,"CandidateMissCurrent",retryPlan,shortPrefix.Identity,shortPrefix.SampleTick,phase,fullFuture),"Neither short nor long requests may borrow a Balanced result as a complete-world miss.");
                    int preferred=(int)Call(attack,"CandidatePreferred",published.Identity);
                    Require((int)Call(attack,"CandidateProgressRequired",published.Identity,progressKey,false)==preferred && progressAttempts[progressKey]==priorProgressAttempt,"Finite representative progress uses its current preferred range without rewriting the fair attempt stamp.");
                    progressSignificant[published.Identity.Slot]=true;
                    Require((int)Call(attack,"CandidateProgressRequired",published.Identity,progressKey,false)==preferred && progressSignificant[published.Identity.Slot] && progressAttempts[progressKey]==priorProgressAttempt,"A significant unresolved queue ticket survives independently of the retired full-miss range rule.");
                    // The live fact-change detector clears the range once;
                    // its queue's significant priority is a separate lifetime.
                    progressRequests[progressKey]=0;progressSignificant[published.Identity.Slot]=true;
                    Require((int)Call(attack,"CandidateProgressRequired",published.Identity,progressKey,false)==(int)Call(attack,"CandidatePreferred",published.Identity) && progressAttempts[progressKey]==priorProgressAttempt,"A significant new receive premise restarts only its work range, never its fair attempt stamp.");
                    progressSignificant[published.Identity.Slot]=false;
                    // Only this component clock advances; no native consumer
                    // or live Source demand is manufactured for the window.
                    NativeQuickItemChecks.BeginWorldStep();NpcTrajectory agedWindow;
                    Require(published.TryWindow(Main.GameUpdateCount,fullFuture-1,published.Version,out agedWindow) && agedWindow.SampleTick!=agedWindow.CaptureTick,"The display window retains its older capture and has a shorter remaining future.");
                    Call(retryPlan,"Clear");Call(attack,"PreparePlan",retryPlan,agedWindow,phase);var aged=NativeCombatAimChecks.ReadPlanSolution(attack,retryPlan);Require((bool)Get(retryPlan,"Prepared") && aged.Timeline!=null && aged.Timeline.SampleTick==agedWindow.SampleTick && aged.Contact==null && aged.Reachability!="Reachable","The aged conditional window retains only its own finite blocked/unknown result.");
                    Require(!(bool)Call(attack,"CandidateMissCurrent",retryPlan,agedWindow.Identity,agedWindow.SampleTick,phase,32) && !(bool)Call(attack,"CandidateMissCurrent",retryPlan,agedWindow.Identity,agedWindow.SampleTick,phase,fullFuture),"An aged display miss never authorizes a fresh candidate Source request.");
                }
                Console.WriteLine("PASS incomplete plan budget signal propagates, preserves Source, retries same sample/phase; finite Balanced results keep actual coverage/leases and fair range ownership; no full-world miss is fabricated.");
            }
            finally{progressPlans.SetValue(priorProgressPlan,progressKey);progressRequests[progressKey]=priorProgressRequest;progressUnknown[progressKey]=priorProgressUnknown;progressSignificant[published.Identity.Slot]=priorProgressSignificant;foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);retryPlan=null;interruptRetry=false;NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static object stressAttack,stressPrediction;
        private static int stressFar,stressBirths,stressEnhanced,stressProjectile,stressLateContacts,stressFrame,stressBlockers,stressHandStarts,stressNearEnhanced;
        private static Player stressPlayer;private static uint stressInputTick,stressProposedTick;
        private static bool stressPath,stressMoving;
        private static NativeCombatAimChecks.SolutionFacts stressOpeningSolution,stressProposedSolution;
        private static object stressOfferedWeapon;private static string stressOfferedPhase;
        private static void StressSolutionOffered(object __instance,bool __result,object __3)
        {
            if(!ReferenceEquals(stressAttack,__instance) || !__result || Get(__3,"Kind").ToString()!="Enhanced")return;
            stressProposedSolution=NativeCombatAimChecks.ReadSolution(__instance);stressProposedTick=Main.GameUpdateCount;
            stressOfferedWeapon=Get(__3,"Weapon");stressOfferedPhase=Get(__3,"Phase").ToString();
        }
        private static readonly List<string> stressConsumers=new List<string>();
        private static void StressEvaluated(object __instance,bool __2,int __4,int __5,bool __result)
        {
            if(!ReferenceEquals(__instance,stressAttack) || stressFrame<140 || __4/2!=2)return;
            var plans=(Array)Get(stressAttack,"candidatePlans");var plan=plans.GetValue(__4);var path=plan==null?null:GetOptional(plan,"Timeline") as NpcTrajectory;
            Console.WriteLine("CANDIDATE-ADMISSION frame="+stressFrame+" beforeNpc="+__2+" required="+__5+" result="+__result+" prepared="+(plan==null?false:Get(plan,"Prepared"))+" count="+(path?.Count??0)+" paused="+(path?.BudgetPaused??false)+" stop="+path?.Stop+" request="+((int[])Get(stressAttack,"candidateRequests"))[__4]);
        }
        private static void StressHandStarted(Player __instance)
        {if(stressAttack!=null && ReferenceEquals(__instance,stressPlayer))stressHandStarts++;}
        private static void StressRecorded(object __instance)
        {
            if(stressAttack==null || !ReferenceEquals(__instance,stressAttack))return;
            var offer=stressProposedSolution;var input=Get(stressAttack,"consumedInput");
            bool match=stressProposedTick==Main.GameUpdateCount && (uint)Get(stressAttack,"prepared")==Main.GameUpdateCount && offer.HasPoint && offer.Timeline!=null && Get(input,"Kind").ToString()=="Enhanced" && ((NpcIdentity)Get(input,"Target")).Equals(offer.Timeline.Identity) && (Vector2)Get(input,"Point")==offer.Point && Get(input,"Phase").ToString()==stressOfferedPhase && offer.Phase==stressOfferedPhase && ReferenceEquals(Get(input,"Weapon"),stressOfferedWeapon) && ReferenceEquals(stressOfferedWeapon,stressPlayer.HeldItem) && GetOptional(input,"Source")==null;
            stressOpeningSolution=match?offer:default(NativeCombatAimChecks.SolutionFacts);stressProposedSolution=default(NativeCombatAimChecks.SolutionFacts);stressInputTick=Main.GameUpdateCount;
        }
        private static void StressOpening(out int __state)
        {__state=stressInputTick==Main.GameUpdateCount && stressOpeningSolution.HasPoint?stressOpeningSolution.NativeTick:-1;}
        private static void StressBorn(int __result,int __state)
        {
            if(stressAttack==null || __result<0 || __result>=Main.maxProjectiles || Main.projectile[__result].owner!=Main.myPlayer || Main.projectile[__result].type!=stressProjectile)return;
            var input=Get(stressAttack,"consumedInput");var target=(NpcIdentity)Get(input,"Target");var value=stressOpeningSolution;
            bool receiptCurrent=stressInputTick==Main.GameUpdateCount && (bool)Get(stressAttack,"hasConsumedInput") && (uint)Get(stressAttack,"consumedInputStep")==Main.GameUpdateCount;
            bool solutionMatch=value.HasPoint && value.Timeline!=null && stressProposedTick==Main.GameUpdateCount && value.Timeline.Identity.Equals(target) && (Vector2)Get(input,"Point")==value.Point && Get(input,"Phase").ToString()==value.Phase && ReferenceEquals(Get(input,"Weapon"),stressPlayer.HeldItem) && GetOptional(input,"Source")==null && __state>0 && __state==value.NativeTick;
            bool enhanced=receiptCurrent && solutionMatch && Get(input,"Kind").ToString()=="Enhanced";
            // Every natural birth remains in the denominator. Only the actual
            // same-epoch offer/receipt pair can credit this Enhanced consumer.
            stressConsumers.Add(string.Join(",",stressBlockers,stressPath,stressFrame,Main.GameUpdateCount,target.Slot,Get(input,"Kind"),Get(input,"Phase"),__state,value.Timeline?.SampleTick??-1,value.Timeline?.Count??0,Get(Get(stressPrediction,"rolling"),"resultPlayerAdvanced"),stressMoving,receiptCurrent,solutionMatch,enhanced,stressInputTick,stressProposedTick));
            if(stressFrame>=140 && target.Slot==2 && enhanced)stressNearEnhanced++;
            if(target.Slot!=stressFar || !receiptCurrent)return;
            stressBirths++;if(enhanced){stressEnhanced++;if(__state>32)stressLateContacts++;}
        }
        // Real native input runs through the normal Host selector. Private
        // progress arrays are read only AFTER each complete native update;
        // they measure fairness rather than supplying a Contact or demand.
        internal static void RunStress(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            var audit=new Harmony("JueMingR.Tests.CandidateProgressNative");var rows=new List<string>{"scenario,frame,tick,selected,actionPass,worldPass,farActionAttempt,farWorldAttempt,farActionDepth,farWorldDepth,lateNearActionDepth,farBirths,farEnhanced,budgetMs,lateContacts,path,weapon,movingCandidates"};
            stressConsumers.Clear();stressConsumers.Add("blockers,path,frame,tick,target,kind,inputPhase,openingSolutionTick,sourceSampleTick,sourceCount,firstPlayerStepCompleted,movingCandidates,receiptCurrent,solutionMatch,countedEnhanced,recordedTick,proposedTick");
            try
            {
                audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13),prefix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(StressOpening)),postfix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(StressBorn)));
                audit.Patch(attack.GetType().GetMethod("RecordInput",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(StressRecorded)));
                audit.Patch(attack.GetType().GetMethod("TryOrdinaryInput",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(StressSolutionOffered)));
                audit.Patch(typeof(Player).GetMethod("ItemCheck_StartActualUse",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(StressHandStarted)));
                if(Environment.GetEnvironmentVariable("JMR_CANDIDATE_PROGRESS_DIAGNOSTIC")=="1")audit.Patch(attack.GetType().GetMethod("EvaluateCandidate",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(StressEvaluated)));
                foreach(bool movingCandidates in new[]{false,true})
                foreach(int blockers in new[]{2,24})
                foreach(bool path in new[]{true,false})
                {
                    if(movingCandidates && (blockers!=24 || path))continue;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(350,400);
                    for(int i=0;i<blockers;i++){var wallCandidate=Scene(2+i,new Vector2(825+i*2,667));if(movingCandidates)wallCandidate.velocity=new Vector2(.04f,0);}
                    float initialLastX=Main.npc[1+blockers].Center.X;
                    int farSlot=2+blockers;Scene(farSlot,new Vector2(190,667));
                    for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(47,y,1);
                    var physical=p.Center+new Vector2(220,-60);
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,path,false,false,false,50,true,true));
                    int idleFuture;
                    using(NativeCombatUsageChecks.ObserveDrawSteps())
                    {int before=NativeCombatUsageChecks.ObservedNpcSteps;for(int warm=0;warm<3;warm++)NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);idleFuture=NativeCombatUsageChecks.ObservedNpcSteps-before;}
                    var initial=(NpcIdentity)Get(Get(host,"Selection"),"Target");
                    bool ownsA=(bool)Call(Get(host,"Prediction"),"OwnsTarget",initial);var initialCache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");
                    Require(initial.Slot==2 && (path?ownsA:!ownsA && initialCache.Read(0)==null && initialCache.Read(1)==null && idleFuture==0),"Both owns initial A; Aim-only idle selects A with no Source, publication or future steps.");
                    Console.WriteLine("CANDIDATE-INITIAL blockers="+blockers+" path="+path+" ownsA="+ownsA+" actualIdleFutureSteps="+idleFuture);
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,path,true,false,false,50,true,true));
                    stressAttack=attack;stressPlayer=p;stressPrediction=Get(host,"Prediction");stressBlockers=blockers;stressPath=path;stressMoving=movingCandidates;stressFar=farSlot;stressProjectile=14;stressBirths=stressEnhanced=stressLateContacts=stressHandStarts=stressNearEnhanced=0;stressOpeningSolution=stressProposedSolution=default(NativeCombatAimChecks.SolutionFacts);stressOfferedWeapon=null;stressOfferedPhase=null;stressInputTick=stressProposedTick=0;int selectedFar=-1,firstEnhanced=-1,nearRecovered=-1;long firstAction=(long)Get(attack,"candidateActionPass"),firstWorld=(long)Get(attack,"candidateWorldPass"),priorAction=firstAction,priorWorld=firstWorld,maxActionWait=0,maxWorldWait=0;
                    for(int frame=0;frame<180;frame++)
                    {
                        // A newly reachable near receiver must replace an old
                        // successful farther target at the same configured rank.
                        if(frame==140){Main.npc[2].Center=new Vector2(280,667);Main.npc[2].velocity=Vector2.Zero;}
                        stressFrame=frame;
                        NativeCombatSelectionPreviewChecks.Step(context,physical,frame%40<30);
                        int selected=((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot;
                        var attempts=(long[])Get(attack,"candidateAttempt");var depths=(int[])Get(attack,"candidateDepths");long action=(long)Get(attack,"candidateActionPass"),world=(long)Get(attack,"candidateWorldPass");
                        long actionAttempt=attempts[farSlot*2+1],worldAttempt=attempts[farSlot*2];
                        if(actionAttempt!=priorAction){maxActionWait=Math.Max(maxActionWait,actionAttempt-priorAction);priorAction=actionAttempt;}
                        if(worldAttempt!=priorWorld){maxWorldWait=Math.Max(maxWorldWait,worldAttempt-priorWorld);priorWorld=worldAttempt;}
                        if(frame<140 && selected==farSlot && selectedFar<0)selectedFar=frame;
                        if(stressEnhanced>0 && firstEnhanced<0)firstEnhanced=frame;
                        if(frame>=140 && selected==2 && nearRecovered<0)nearRecovered=frame-140;
                        rows.Add(string.Join(",",blockers,frame,Main.GameUpdateCount,selected,action,world,actionAttempt,worldAttempt,depths[farSlot*2+1],depths[farSlot*2],depths[5],stressBirths,stressEnhanced,Get(Get(host,"PredictionWork"),"UsedMilliseconds"),stressLateContacts,path,95,movingCandidates));
                        if(movingCandidates && (frame==0 || frame==140 || frame==179))Console.WriteLine("CANDIDATE-MOVING frame="+frame+" near2="+Main.npc[2].Center+"/v="+Main.npc[2].velocity+" last25="+Main.npc[25].Center+"/v="+Main.npc[25].velocity+" far26="+Main.npc[26].Center+"/v="+Main.npc[26].velocity);
                    }
                    Require(selectedFar>=0 && stressBirths>0 && stressEnhanced>0,"A non-incumbent late-hit far target is eventually chosen and actually consumes Enhanced: blockers="+blockers);
                    if(nearRecovered<0)Console.WriteLine("CANDIDATE-LATE-FAIL near="+Main.npc[2].Center+"/v="+Main.npc[2].velocity+" active="+Main.npc[2].active+" life="+Main.npc[2].life+" player="+p.Center+" timers="+p.itemTime+"/"+p.itemAnimation+" actionAttempt="+((long[])Get(attack,"candidateAttempt"))[5]+" worldAttempt="+((long[])Get(attack,"candidateAttempt"))[4]+" request="+((int[])Get(attack,"candidateRequests"))[5]+"/"+((int[])Get(attack,"candidateRequests"))[4]+" depth="+((int[])Get(attack,"candidateDepths"))[5]+"/"+((int[])Get(attack,"candidateDepths"))[4]);
                    Require(nearRecovered>=0,"A new nearer reachable late-hit target is reevaluated instead of permanently keeping the far positive.");
                    Require(stressNearEnhanced>0,"After the nearer target becomes reachable, a real native action opportunity consumes its Enhanced input within the original 180-update window.");
                    if(movingCandidates)Require(Main.npc[25].Center.X>initialLastX+2 && Main.npc[25].position.X>48*16,"The moving counterexample executes original NPC movement and stays on the blocked side of the wall.");
                    Console.WriteLine("PASS candidate native progress blockers="+blockers+" path="+path+" movingCandidates="+movingCandidates+" farFirstSelectedFrame="+selectedFar+" firstFarEnhancedFrame="+firstEnhanced+" realActionOpportunities="+stressHandStarts+" farActualBirths="+stressBirths+" farEnhancedBirths="+stressEnhanced+" actualLateContacts="+stressLateContacts+" lateNearRecoveryFrames="+nearRecovered+" actualNearEnhancedBirths="+stressNearEnhanced+" maximumReservedActionPassWait="+maxActionWait+" maximumReservedWorldPassWait="+maxWorldWait+" farDepth="+((int[])Get(attack,"candidateDepths"))[farSlot*2+1]);
                    stressAttack=null;
                }
            }
            finally
            {
                foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);
                stressAttack=stressPrediction=stressOfferedWeapon=null;stressPlayer=null;stressOfferedPhase=null;stressOpeningSolution=stressProposedSolution=default(NativeCombatAimChecks.SolutionFacts);stressInputTick=stressProposedTick=0;NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                string root=Path.Combine(Terraria.Program.SavePath,"candidate-progress");Directory.CreateDirectory(root);File.WriteAllLines(Path.Combine(root,"frames.csv"),rows);File.WriteAllLines(Path.Combine(root,"native-consumers.csv"),stressConsumers);Console.WriteLine("CANDIDATE-PROGRESS raw="+root);
            }
        }
        private static void SameObstacle(object context,object combat,object host,object input)
        {
            foreach(int weapon in new[]{95,39,3570,3278})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.yoyoGlove=p.magicString=false;p.counterWeight=0;p.statMana=p.statManaMax2=10000;Main.screenPosition=new Vector2(600,400);
                p.inventory[54].SetDefaults(weapon==95?97:40);p.inventory[54].stack=999;var blocked=Scene(2,new Vector2(825,667));var open=Scene(3,new Vector2(650,667));
                for(int y=40;y<=42;y++){Main.tile[48,y].active(true);Main.tile[48,y].type=1;}
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,false,25,true,true));NativeToolExecutionChecks.Sample(context,input,blocked.Center,true);Call(combat,"Sample");Call(host,"SampleMouse");Call(Get(host,"Selection"),"Update",((ObservationSettings)Get(host,"Settings")).Value,Get(host,"Session"),true,null);
                var identity=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target");Console.WriteLine("WALL CANDIDATE weapon="+weapon+" selected="+identity.Slot+" solidColumn=48:y40..42 player="+p.Center+" A="+blocked.Center+" B="+open.Center);
                Require(identity.Slot==2,"nearest mode remains strictly physical distance across straight/arc/sky/navigation attack mechanisms");
                if(weapon==95)
                {
                    // Preference never deletes the only blocked receiver; its
                    // current zero velocity is not a future immobility proof.
                    open.active=false;blocked.velocity=new Vector2(0,-1);Call(Get(host,"Selection"),"Update",((ObservationSettings)Get(host,"Settings")).Value,Get(host,"Session"),true,null);
                    Require(((JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot==2,"moving or sole blocked receiver remains a candidate, not a permanent unreachable exclusion");
                    open.active=true;blocked.velocity=Vector2.Zero;Main.tile[48,42].active(false);Call(Get(host,"Selection"),"Update",((ObservationSettings)Get(host,"Settings")).Value,Get(host,"Session"),true,null);
                    Require(((JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot==2,"current receiver edge through incomplete wall cone is unknown, centre blockage alone cannot demote it");
                    Main.tile[48,42].active(true);Call(Get(host,"Selection"),"Update",((ObservationSettings)Get(host,"Settings")).Value,Get(host,"Session"),true,null);
                }
                var target=Main.npc[identity.Slot];var attack=Get(combat,"Attack");NativeCombatAimChecks.Prepare(host,attack,target,0,true);
                if(weapon==3278)
                {
                    // This manually published trajectory tests a prepared
                    // navigation consumer. A cold budget pause is not a proof
                    // of a ready opening, nor a natural first-click result.
                    // Reevaluate only on real new update clocks, with the same
                    // normal allowance and a finite six-update preparation cap.
                    var control=Get(attack,"Control");var work=Get(host,"PredictionWork");int warms=0;
                    for(;;)
                    {
                        bool ready=(bool)Get(control,"openingUsable") && (bool)Get(control,"HasPreparedInput");
                        Console.WriteLine("WALL PREPARED weapon="+weapon+" warm="+warms+" tick="+Main.GameUpdateCount+" workTick="+Get(work,"tick")+" budgetMs="+Get(work,"UsedMilliseconds")+" exceeded="+Get(work,"Exceeded")+" openingUsable="+Get(control,"openingUsable")+" hasPreparedInput="+Get(control,"HasPreparedInput"));
                        if(ready || warms==6)break;
                        NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,blocked.Center,true);Call(combat,"Sample");blocked.UpdateNPC(2);open.UpdateNPC(3);NativeCombatAimChecks.Prepare(host,attack,target,0,true);warms++;
                    }
                    Require((bool)Get(control,"openingUsable") && (bool)Get(control,"HasPreparedInput"),"The bounded normal-budget navigation component prepares a legal opening before its manual Shoot consumer.");
                }
                var solution=NativeCombatAimChecks.ReadSolution(attack);
                var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");Call(Get(Get(host,"World"),"Marker"),"Capture");
                Require(cache.Read(1).Identity.Equals(identity) && Get(Get(Get(host,"World"),"Marker"),"identity").Equals(identity),"path/attack future and marker capture share the final selected identity");
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});int first=-1;
                for(int tick=1;tick<=120 && first<0;tick++)
                {NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,blocked.Center,true);Call(combat,"Sample");blocked.UpdateNPC(2);open.UpdateNPC(3);if(weapon==3278)NativeCombatAimChecks.Prepare(host,attack,target,0,true);int life=target.life;for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;Main.projectile[slot].Update(slot);}Main.ProjectileUpdateLoopIndex=-1;if(target.life<life)first=tick;}
                Console.WriteLine("WALL RESULT weapon="+weapon+" selected="+identity.Slot+" openingSolution="+solution.HasPoint+" reachability="+solution.Reachability+" firstDamage="+first+" lifeA="+blocked.life+" lifeB="+open.life);
                Require(weapon!=95 || solution.Reachability!="Reachable" && solution.Contact==null && first<0,"nearest blocked straight target remains selected without fabricating a contact or redirecting to the farther receiver");
                Require(weapon==95 || (weapon==39?solution.Reachability!="Reachable" && solution.Contact==null && first<0:first>0),"same obstacle preserves finite unknown arc and actual useful sky/navigation outcomes without claiming every mechanism reachable");
            }
            Console.WriteLine("PASS same-obstacle shared identity matrix; current passage preference is conditional, finite no-Contact is not a physical-impossibility claim.");
        }
        private static void ShortDemand(object context,object combat,object host,object input)
        {
            using(var futureSteps=NativeCombatUsageChecks.ObserveDrawSteps())
            {
            var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");var audit=new Harmony("JueMingR.Tests.AttackDemand");audit.Patch(typeof(Player).GetMethod("ItemCheck",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks).GetMethod("Before",Flags)){priority=Priority.Last});
            try
            {
                foreach(bool path in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.releaseUseItem=true;p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;var n=Scene(2,new Vector2(870,667));
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,path,false,false,false,25,false,true));NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");Call(host,"SampleMouse");NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");
                    NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");Call(host,"SampleMouse");watchCache=cache;NativeQuickItemChecks.BeginWorldStep();p.Update(0);watchCache=null;
                    Projectile shot=null;foreach(var q in Main.projectile)if(q.active && q.type==14)shot=q;
                    var attack=Get(combat,"Attack");var consumed=Get(attack,"consumedInput");var openingTarget=(JueMingR.Platform.Combat.NpcIdentity)Get(Get(host,"Selection"),"Target");
                    Console.WriteLine("DEMAND before path="+path+" required="+window+" samples="+count+" shot="+(shot==null?"none":shot.velocity.ToString())+" consumed="+Get(consumed,"Kind"));
                    Require(shot!=null && shot.velocity.X>0 && window>=16 && window<40 && count>=window+1 && (bool)Get(attack,"hasConsumedInput") && (uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount && Get(consumed,"Kind").ToString()=="Enhanced","real first shot consumes Enhanced from the bounded input prefix with Path OFF or ON");
                    // The same native world step now completes NPC movement
                    // and projectile damage before the optional path producer.
                    // Long display work is no longer a same-update input gate.
                    foreach(var npc in Main.npc)if(npc.active)npc.UpdateNPC(npc.whoAmI);
                    for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;Main.projectile[slot].Update(slot);}Main.ProjectileUpdateLoopIndex=-1;
                    Call(context,"UpdateRuntime");var displayed=GetOptional(host,"DisplayPath") as JueMingR.Platform.Combat.NpcTrajectory;
                    Require(path?cache.Required==120:cache.Read(0)==null,"Path keeps its long-range intent; Path OFF does not acquire reader0.");
                    bool observedDisplay=CheckShortDisplay(host,n,openingTarget,displayed,0);
                    int longest=displayed?.Count??0;
                    for(int update=0;path && update<6;update++)
                    {NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),false,true);displayed=GetOptional(host,"DisplayPath") as JueMingR.Platform.Combat.NpcTrajectory;observedDisplay=CheckShortDisplay(host,n,openingTarget,displayed,update+1)||observedDisplay;longest=Math.Max(longest,displayed?.Count??0);}
                    // The shared optional allowance promises a true usable
                    // prefix, not 120 completed actions within six updates.
                    // Exact extension/half-step rollback/immutable storage are
                    // proved deterministically by RollingNpcPredictionChecks'
                    // IncrementalPrefix, BudgetPrefix and MultiSlotPrefix;
                    // NpcTrajectoryWindowChecks keeps reader minima separate.
                    Require(!path || observedDisplay,"The original observation window acquires a current, content-checked future without delaying the actual Enhanced input.");
                    Console.WriteLine("DEMAND display path="+path+" observed="+observedDisplay+" longest="+longest+" fullTailIsNotASuccessPredicate=True");
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions(marker:true));NativeToolExecutionChecks.Sample(context,input,new Vector2(870,667),false);Call(combat,"Sample");Call(host,"SampleMouse");Call(context,"UpdateRuntime");int steps=NativeCombatUsageChecks.ObservedNpcSteps;
                for(int i=0;i<20;i++){Call(context,"UpdateRuntime");Call(context,"UpdateShell");}Require(cache.Required==0 && NativeCombatUsageChecks.ObservedNpcSteps==steps && !(bool)Get(Get(host,"Selection"),"HasTarget") && !(bool)Get(host,"Marker") && ((ObservationOptions)Get(host,"Options")).Marker,"saved Marker with Aim/Path OFF retires selection and keeps zero future work or residual attack demand");
                Console.WriteLine("PASS real Enhanced short input prefix with Path OFF/ON, current honest optional display prefix, saved Marker OFF zero future steps");
            }
            finally{watchCache=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
            }
        }
        private static bool CheckShortDisplay(object host,NPC n,NpcIdentity target,NpcTrajectory path,int update)
        {
            long now=(long)Main.GameUpdateCount;
            Console.WriteLine("DEMAND prefix update="+update+" tick="+now+" sample="+(path==null?-1:path.SampleTick)+" capture="+(path==null?-1:path.CaptureTick)+" count="+(path==null?0:path.Count)+" paused="+(path!=null && path.BudgetPaused)+" lease="+Get(host,"displayLeaseTick")+" required="+((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Required+" layer="+Get(host,"LayerStatus")+" canDraw="+Get(host,"CanDraw"));
            if(path==null)return false;
            long age=now-path.SampleTick;
            int maximumAge=(int)host.GetType().GetField("DisplayMaximumAge",Flags).GetRawConstantValue();
            Require(path.Identity.Equals(target) && ((NpcIdentity)Get(Get(host,"Selection"),"Target")).Equals(target) && ReferenceEquals(path.Identity.Token,n),"Every displayed prefix belongs to the actual selected native instance, session and generation.");
            Require(path.CaptureTick<=path.SampleTick && age>=0 && age<=maximumAge && age<path.Count-1 && path.Count<=NpcPredictionCache.Horizon+1 && (long)Get(host,"displayLeaseTick")==now,"The display has its original capture, finite current window and an owner-granted lease on this actual update.");
            // This existing fixture has no AI, velocity or gravity. Its native
            // world actions independently establish every future point, so a
            // plausible count or a newly stamped stale path cannot pass.
            Require(n.aiStyle==-1 && n.noGravity && n.velocity==Vector2.Zero,"The native static receiver still satisfies the independently known display-content premise.");
            for(int i=0;i<path.Count;i++)
            {
                var point=path[i];var box=point.Bounds;
                Require(point.TickOffset==i && box.X==n.position.X && box.Y==n.position.Y && box.Width==n.width && box.Height==n.height && point.Vx==0 && point.Vy==0 && point.Phase==n.ai[0] && point.Wet==n.wet && point.NoTileCollide==n.noTileCollide,"The entire published prefix contains completed native-static receiver facts at their honest offsets: update="+update+" point="+i);
            }
            // This CPU entry consumes the Host's current DisplayPath lease.
            // World-layer readiness is established by the native Draw setup,
            // which this fixture does not execute. Actual rendering belongs
            // to the existing World/Main consumers, not a fabricated Ready.
            Require((bool)Get(host,"Path"),"The real Path owner remains enabled while exposing its checked current future.");
            return true;
        }
        private static NPC Scene(int slot,Vector2 center)
        {
            var n=Main.npc[slot];Vector2 before=n.velocity;n.SetDefaults(3);
            if(before!=Vector2.Zero || n.velocity!=Vector2.Zero)Console.WriteLine("CANDIDATE SCENE INITIAL slot="+slot+" velocityBeforeDefaults="+before+" velocityAfterDefaults="+n.velocity+" initializedVelocity="+Vector2.Zero);
            // Vanilla SetDefaults retains velocity on this reused live object;
            // resetting active alone must not carry the prior case's knockback
            // into a static manually published trajectory. Only initialization
            // clears it; moving fixtures and all subsequent native motion own it.
            n.velocity=Vector2.Zero;n.whoAmI=slot;n.active=true;n.Center=center;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=100000;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
    }
}
