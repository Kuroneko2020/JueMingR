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
            SameObstacle(context,combat,host,input);ShortDemand(context,combat,host,input);BudgetPlanRetry(context);RunStress(context);
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
                audit.Patch(attack.GetType().GetMethod("SolveOnTerrain",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(BeforeRetrySolve)));
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))
                {
                    Require(Call(attack,"PreparePlan",retryPlan,published,phase) is AttackContact,"The same ordinary scene completes a normal contact before injecting interruption.");
                    var realContact=(AttackContact)Get(retryPlan,"Contact");NpcTrajectory contactPrefix;
                    Require(published.TryWindow(published.SampleTick,realContact.ReceiveTick+1,published.Version,out contactPrefix),"A real solved contact has a complete immutable prefix inside its native publication.");
                    Call(retryPlan,"Clear");Require(Call(attack,"PreparePlan",retryPlan,contactPrefix,phase) is AttackContact,"The same ordinary contact completes on its actual shorter prefix.");
                    var borrow=attack.GetType().GetMethod("TryCandidatePresentationPrefix",Flags);var borrowArgs=new object[]{retryPlan,published,null};
                    Require((bool)borrow.Invoke(null,borrowArgs) && AttackIntercept.Rebind((AttackContact)Get(retryPlan,"Contact"),(NpcTrajectory)borrowArgs[2])!=null,"A completed fresh candidate borrows the entire equal shorter prefix from a longer selected publication without another solve.");
                    NpcTrajectory insufficient;Require(published.TryWindow(published.SampleTick,0,published.Version,out insufficient),"The selected publication also supplies an independently too-short view.");
                    borrowArgs=new object[]{retryPlan,insufficient,null};Require(!(bool)borrow.Invoke(null,borrowArgs),"A shorter selected publication cannot borrow a longer candidate plan or its contact.");
                    NpcTrajectory otherSample;Require(published.TryWindow(published.SampleTick+1,realContact.ReceiveTick,published.Version,out otherSample),"The original immutable publication can expose the next sample without a new capture.");
                    borrowArgs=new object[]{retryPlan,otherSample,null};Require(!(bool)borrow.Invoke(null,borrowArgs),"A different sample never borrows the old candidate presentation even when its geometry stays equal.");
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
                        Require(ReferenceEquals(GetOptional(host,"DisplayPath"),displayArgs[1]) && retrySolves==borrowedSolves && ReferenceEquals(cache.Read(0),published) && GetOptional(attack,"ExpectedImpact")==null && GetOptional(attack,"ExpectedPreview")==null && !p.controlUseItem,"Publishing the completed NPC path performs zero hand solves, preserves the selected Source publication and grants neither input nor retired preview.");
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
                    Require(Call(attack,"PreparePlan",retryPlan,published,phase) is AttackContact && retrySolves>1,"The identical sample/phase/plan retries the real solve and recovers its contact.");
                    int before=retrySolves;Require(Call(attack,"PreparePlan",retryPlan,published,phase) is AttackContact && retrySolves==before,"A completed contact still reuses the same legal solve.");
                    Call(retryPlan,"Clear");for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(48,y,1);
                    Require(Call(attack,"PreparePlan",retryPlan,published,phase)==null && (bool)Get(retryPlan,"Prepared"),"The real complete wall produces a completed ordinary miss.");
                    before=retrySolves;Require(Call(attack,"PreparePlan",retryPlan,published,phase)==null && retrySolves==before,"A completed null still reuses its legal miss instead of replaying forever.");
                    int fullFuture=published.Count-1;
                    Require(fullFuture>32 && (bool)Call(attack,"CandidateMissCurrent",retryPlan,published.Identity,published.SampleTick,phase,fullFuture),"A genuine complete null can authorize the exact completed requested prefix.");
                    NpcTrajectory shortPrefix;Require(published.TryWindow(published.SampleTick,32,published.Version,out shortPrefix),"The naturally published trajectory supplies a real shorter prefix.");
                    Call(retryPlan,"Clear");Require(Call(attack,"PreparePlan",retryPlan,shortPrefix,phase)==null && (bool)Get(retryPlan,"Prepared"),"The shorter prefix also completes its real wall miss.");
                    Require((bool)Call(attack,"CandidateMissCurrent",retryPlan,shortPrefix.Identity,shortPrefix.SampleTick,phase,32) && !(bool)Call(attack,"CandidateMissCurrent",retryPlan,shortPrefix.Identity,shortPrefix.SampleTick,phase,fullFuture),"A completed short miss cannot authorize a longer requested future.");
                    Require((int)Call(attack,"CandidateProgressRequired",published.Identity,progressKey,false)==64,"A complete nonpaused requested-prefix miss may fairly extend its actual future.");
                    progressSignificant[published.Identity.Slot]=true;
                    Require((int)Call(attack,"CandidateProgressRequired",published.Identity,progressKey,false)==(int)Call(attack,"CandidatePreferred",published.Identity) && progressAttempts[progressKey]==priorProgressAttempt,"A significant new receive premise restarts only its work range, never its fair attempt stamp.");
                    progressSignificant[published.Identity.Slot]=false;
                    // Only this component clock advances; no native consumer
                    // or live Source demand is manufactured for the window.
                    NativeQuickItemChecks.BeginWorldStep();NpcTrajectory agedWindow;
                    Require(published.TryWindow(Main.GameUpdateCount,fullFuture-1,published.Version,out agedWindow) && agedWindow.SampleTick!=agedWindow.CaptureTick,"The display window retains its older capture and has a shorter remaining future.");
                    Call(retryPlan,"Clear");Require(Call(attack,"PreparePlan",retryPlan,agedWindow,phase)==null && (bool)Get(retryPlan,"Prepared"),"The aged conditional window completes a genuine display miss.");
                    Require(!(bool)Call(attack,"CandidateMissCurrent",retryPlan,agedWindow.Identity,agedWindow.SampleTick,phase,32) && !(bool)Call(attack,"CandidateMissCurrent",retryPlan,agedWindow.Identity,agedWindow.SampleTick,phase,fullFuture),"An aged display miss never authorizes a fresh candidate Source request.");
                }
                Console.WriteLine("PASS incomplete plan budget signal propagates, preserves Source, retries same sample/phase; complete null memoizes only its completed range, while short/aged display misses cannot authorize a fresh longer Source request.");
            }
            finally{progressPlans.SetValue(priorProgressPlan,progressKey);progressRequests[progressKey]=priorProgressRequest;progressUnknown[progressKey]=priorProgressUnknown;progressSignificant[published.Identity.Slot]=priorProgressSignificant;foreach(var method in audit.GetPatchedMethods().ToArray())audit.Unpatch(method,HarmonyPatchType.All,audit.Id);retryPlan=null;interruptRetry=false;NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static object stressAttack,stressPrediction;
        private static int stressFar,stressBirths,stressEnhanced,stressProjectile,stressLateContacts,stressFrame,stressBlockers,stressHandStarts,stressNearEnhanced;
        private static Player stressPlayer;private static uint stressInputTick,stressProposedTick;
        private static bool stressPath,stressMoving;private static AttackContact stressOpeningContact,stressProposedContact;
        private static void StressContactProposed(object __instance,AttackContact __0)
        {if(ReferenceEquals(stressAttack,__instance)){stressProposedContact=__0;stressProposedTick=Main.GameUpdateCount;}}
        private static readonly List<string> stressConsumers=new List<string>();
        private static void StressHandStarted(Player __instance)
        {if(stressAttack!=null && ReferenceEquals(__instance,stressPlayer))stressHandStarts++;}
        private static void StressRecorded(object __instance)
        {
            if(stressAttack==null || !ReferenceEquals(__instance,stressAttack))return;
            stressOpeningContact=stressProposedTick==Main.GameUpdateCount?stressProposedContact:null;stressProposedContact=null;stressInputTick=Main.GameUpdateCount;
            var input=Get(stressAttack,"consumedInput");var point=(Vector2)Get(input,"Point");if(Get(input,"Kind").ToString()!="Enhanced" || stressOpeningContact!=null && (!stressOpeningContact.Timeline.Identity.Equals((NpcIdentity)Get(input,"Target")) || point.X!=stressOpeningContact.AimX || point.Y!=stressOpeningContact.AimY))stressOpeningContact=null;
        }
        private static void StressOpening(out int __state)
        {
            var contact=stressInputTick==Main.GameUpdateCount?stressOpeningContact:null;
            __state=contact?.Tick??-1;
        }
        private static void StressBorn(int __result,int __state)
        {
            if(stressAttack==null || __result<0 || __result>=Main.maxProjectiles || Main.projectile[__result].owner!=Main.myPlayer || Main.projectile[__result].type!=stressProjectile)return;
            var input=Get(stressAttack,"consumedInput");var target=(NpcIdentity)Get(input,"Target");
            var point=(Vector2)Get(input,"Point");bool receiptCurrent=stressInputTick==Main.GameUpdateCount && (bool)Get(stressAttack,"hasConsumedInput") && (uint)Get(stressAttack,"consumedInputStep")==Main.GameUpdateCount;
            bool contactMatch=stressOpeningContact!=null && stressProposedTick==Main.GameUpdateCount && stressOpeningContact.Timeline.Identity.Equals(target) && point.X==stressOpeningContact.AimX && point.Y==stressOpeningContact.AimY && __state>=0 && __state==stressOpeningContact.Tick;
            bool enhanced=receiptCurrent && contactMatch && Get(input,"Kind").ToString()=="Enhanced";
            // Retain invalid observer rows, but a prior receipt or unmatched
            // proposal never counts as this native birth's enhanced consumer.
            stressConsumers.Add(string.Join(",",stressBlockers,stressPath,stressFrame,Main.GameUpdateCount,target.Slot,Get(input,"Kind"),Get(input,"Phase"),__state,stressOpeningContact?.Timeline.SampleTick??-1,stressOpeningContact?.Timeline.Count??0,Get(Get(stressPrediction,"rolling"),"resultPlayerAdvanced"),stressMoving,receiptCurrent,contactMatch,enhanced,stressInputTick,stressProposedTick));
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
            stressConsumers.Clear();stressConsumers.Add("blockers,path,frame,tick,target,kind,inputPhase,openingContactTick,sourceSampleTick,sourceCount,firstPlayerStepCompleted,movingCandidates,receiptCurrent,contactMatch,countedEnhanced,recordedTick,proposedTick");
            try
            {
                audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13),prefix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(StressOpening)),postfix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(StressBorn)));
                audit.Patch(attack.GetType().GetMethod("RecordInput",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(StressRecorded)));
                audit.Patch(attack.GetType().GetMethod("ContactInput",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(StressContactProposed)));
                audit.Patch(typeof(Player).GetMethod("ItemCheck_StartActualUse",Flags),postfix:new HarmonyMethod(typeof(NativeCombatCandidateDemandChecks),nameof(StressHandStarted)));
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
                    stressAttack=attack;stressPlayer=p;stressPrediction=Get(host,"Prediction");stressBlockers=blockers;stressPath=path;stressMoving=movingCandidates;stressFar=farSlot;stressProjectile=14;stressBirths=stressEnhanced=stressLateContacts=stressHandStarts=stressNearEnhanced=0;stressOpeningContact=stressProposedContact=null;stressInputTick=stressProposedTick=0;int selectedFar=-1,firstEnhanced=-1,nearRecovered=-1;long firstAction=(long)Get(attack,"candidateActionPass"),firstWorld=(long)Get(attack,"candidateWorldPass"),priorAction=firstAction,priorWorld=firstWorld,maxActionWait=0,maxWorldWait=0;
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
                stressAttack=null;NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
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
                var plan=GetOptional(attack,"ExpectedImpact");
                var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");Call(Get(Get(host,"World"),"Marker"),"Capture");
                Require(cache.Read(1).Identity.Equals(identity) && Get(Get(Get(host,"World"),"Marker"),"identity").Equals(identity),"path/attack future and marker capture share the final selected identity");
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});int first=-1;
                for(int tick=1;tick<=120 && first<0;tick++)
                {NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,blocked.Center,true);Call(combat,"Sample");blocked.UpdateNPC(2);open.UpdateNPC(3);if(weapon==3278)NativeCombatAimChecks.Prepare(host,attack,target,0,true);int life=target.life;for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;Main.projectile[slot].Update(slot);}Main.ProjectileUpdateLoopIndex=-1;if(target.life<life)first=tick;}
                Console.WriteLine("WALL RESULT weapon="+weapon+" selected="+identity.Slot+" openingContact="+(plan!=null)+" firstDamage="+first+" lifeA="+blocked.life+" lifeB="+open.life);
                Require(weapon!=95 || plan==null && first<0,"nearest blocked straight target remains selected without fabricating a contact or redirecting to the farther receiver");
                Require(weapon==95 || (weapon==39?plan==null && first<0:first>0),"same obstacle preserves finite unknown arc and actual useful sky/navigation outcomes without claiming every mechanism reachable");
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
                    if(displayed!=null)Require(displayed.Count>=2 && displayed.Count<=121 && displayed.SampleTick<=(long)Main.GameUpdateCount && displayed.Identity.Equals(openingTarget),"Any displayed prefix has honest length/time and the same final target.");
                    int longest=displayed?.Count??0;
                    for(int update=0;path && update<6;update++)
                    {NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,550),false,true);displayed=GetOptional(host,"DisplayPath") as JueMingR.Platform.Combat.NpcTrajectory;longest=Math.Max(longest,displayed?.Count??0);}
                    Require(!path || longest==121,"The ordinary unloaded path still reaches its full two-second range without delaying the first actual input.");
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions(marker:true));NativeToolExecutionChecks.Sample(context,input,new Vector2(870,667),false);Call(combat,"Sample");Call(host,"SampleMouse");Call(context,"UpdateRuntime");int steps=NativeCombatUsageChecks.ObservedNpcSteps;
                for(int i=0;i<20;i++){Call(context,"UpdateRuntime");Call(context,"UpdateShell");}Require(cache.Required==0 && NativeCombatUsageChecks.ObservedNpcSteps==steps && !(bool)Get(Get(host,"Selection"),"HasTarget") && !(bool)Get(host,"Marker") && ((ObservationOptions)Get(host,"Options")).Marker,"saved Marker with Aim/Path OFF retires selection and keeps zero future work or residual attack demand");
                Console.WriteLine("PASS real Enhanced short input prefix with Path OFF/ON, optional full-range display progress, saved Marker OFF zero future steps");
            }
            finally{watchCache=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
            }
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
