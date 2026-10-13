using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCombatAttackCostChecks
    {
        private static object budgetAttack,budgetHost,budgetContext,abortedPlan;
        private static object abortedPhase;private static long abortedSample;private static int abortedRequired;
        private static bool forceBudget;
        private static uint forcedTick;private static ulong forcedRuntimeTick;
        private static Player incidentPlayer;
        private static NPC incidentReceiver;
        private static Vector2 incidentMouse;
        private static int incidentBirths,incidentHits;
        private static bool incidentClearLine;private static int incidentFrame,incidentFarBirths,incidentFirstFar;
        private static void IncidentBirth(int __result)
        {
            if(incidentPlayer==null || __result<0 || __result>=Main.maxProjectiles)return;
            var shot=Main.projectile[__result];
            if(shot.owner!=incidentPlayer.whoAmI || shot.type!=1 && shot.type!=14)return;
            incidentBirths++;
            var attack=Get(Get(incidentContext,"Combat"),"Attack");var input=Get(attack,"consumedInput");
            var expected=(NpcIdentity)Get(Get(Get(incidentContext,"CombatObservation"),"Selection"),"Target");
            var recorded=(NpcIdentity)Get(input,"Target");var cursor=Main.screenPosition+new Vector2(Main.mouseX,Main.mouseY);
            var plans=(Array)Get(attack,"candidatePlans");var farPlan=plans.GetValue(incidentReceiver.whoAmI*2+1);var farPath=farPlan==null?null:GetOptional(farPlan,"Timeline") as NpcTrajectory;var farContact=farPlan==null?null:GetOptional(farPlan,"Contact") as AttackContact;
            bool farPositive=farContact!=null && (uint)Get(farPlan,"PreparedTick")==Main.GameUpdateCount && Convert.ToString(Get(farPlan,"Phase"))=="BeforeNpc" && farPath!=null && farPath.SampleTick==(long)Main.GameUpdateCount-1;
            bool farUnknown=((bool[])Get(attack,"candidateUnknown"))[incidentReceiver.whoAmI*2+1];
            var work=Get(Get(incidentContext,"CombatObservation"),"PredictionWork");
            Console.WriteLine("INCIDENT far before-birth frame="+incidentFrame+" prepared="+(farPlan!=null && (bool)Get(farPlan,"Prepared"))+" preparedTick="+(farPlan==null?-1:Convert.ToInt64(Get(farPlan,"PreparedTick")))+" phase="+(farPlan==null?"none":Convert.ToString(Get(farPlan,"Phase")))+" timeline="+(farPath==null?"none":farPath.SampleTick+"/"+farPath.CaptureTick+"/"+farPath.Count+"/paused"+farPath.BudgetPaused+"/"+farPath.Stop)+" contact="+(farContact==null?-1:farContact.Tick)+" unknown="+farUnknown+" positive="+farPositive+" used="+Get(work,"UsedMilliseconds")+" exceeded="+Get(work,"Exceeded")+" fault="+Get(attack,"Failed"));
            Console.WriteLine("INCIDENT birth boundary tick="+Main.GameUpdateCount+" has="+Get(attack,"hasConsumedInput")+" receiptTick="+Get(attack,"consumedInputStep")+" kind="+Get(input,"Kind")+" recorded="+IdentityFact(recorded)+" selected="+IdentityFact(expected)+" receiver="+incidentReceiver.whoAmI+"/"+incidentReceiver.generation+"/"+incidentReceiver.type+"/object"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(incidentReceiver)+" cursor="+cursor+" physical="+incidentMouse+" velocity="+shot.velocity+" dot="+Vector2.Dot(shot.velocity.SafeNormalize(Vector2.Zero),(incidentReceiver.Center-shot.Center).SafeNormalize(Vector2.Zero)));
            Require((bool)Get(attack,"hasConsumedInput") && (uint)Get(attack,"consumedInputStep")==Main.GameUpdateCount && recorded.Equals(expected) && expected.Slot>=0 && ReferenceEquals(expected.Token,Main.npc[expected.Slot]),"real incident birth has a same-GUC full current receiver receipt");
            bool far=ReferenceEquals(expected.Token,incidentReceiver);
            if(!far)Require(incidentClearLine && Convert.ToString(Get(input,"Kind"))=="Basic" && farUnknown && !farPositive && !(bool)Get(attack,"Failed"),"an unresolved cold opportunity may consume only its current legal Basic target, never ignore an already proven far contact");
            else if(incidentClearLine){Require(farPositive,"a chosen reachable clear-line receiver has a fresh actual-phase positive proof");if(Convert.ToString(Get(input,"Kind"))=="Enhanced"){incidentFarBirths++;if(incidentFirstFar<0)incidentFirstFar=incidentFrame;}}
            Console.WriteLine("INCIDENT actual birth tick="+Main.GameUpdateCount+" projectile="+shot.type+" kind="+Get(input,"Kind")+" target="+expected.Slot+"/"+expected.Generation+"/"+expected.Type+" point="+Get(input,"Point"));
            Require(Vector2.DistanceSquared(cursor,incidentMouse)>4 && Vector2.Dot(shot.velocity.SafeNormalize(Vector2.Zero),(Main.npc[expected.Slot].Center-shot.Center).SafeNormalize(Vector2.Zero))>.9f,"real native birth consumes its full current legal shared target with the declared Basic/Enhanced receipt");
        }
        private static string IdentityFact(NpcIdentity id)
        {return id.Session+"/"+id.Slot+"/"+id.Generation+"/"+id.Type+"/"+id.NetId+"/object"+(id.Token==null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(id.Token));}
        private static void IncidentHit(NPC __instance,int __result)
        {if(ReferenceEquals(__instance,incidentReceiver) && __result>0)incidentHits++;}
        private static void AbortPreparedPlan()
        {
            if(!forceBudget)return;
            var plans=(Array)Get(budgetAttack,"candidatePlans");
            var preview=Get(budgetAttack,"preview");
            abortedPlan=plans.Cast<object>().Concat(new[]{preview}).FirstOrDefault(p=>p!=null && (bool)Get(p,"Prepared") && (uint)Get(p,"PreparedTick")==Main.GameUpdateCount && GetOptional(p,"Contact")==null);
            if(abortedPlan==null)return;
            forceBudget=false;forcedTick=Main.GameUpdateCount;forcedRuntimeTick=(ulong)Get(budgetContext,"updateTick");
            abortedPhase=Get(abortedPlan,"Phase");var abortedTimeline=(NpcTrajectory)Get(abortedPlan,"Timeline");abortedSample=abortedTimeline.SampleTick;abortedRequired=Math.Max(1,abortedTimeline.Count-1);
            var work=Get(budgetHost,"PredictionWork");
            // Deterministically exhaust the REAL owner's current allowance
            // after preparation has published partial scratch. No sleeps,
            // changed input gates or fake Contact are part of this injection.
            Set(work,"spent",Get(work,"allowance"));
            try{work.GetType().GetMethod("Check",Flags).Invoke(null,null);}
            catch(System.Reflection.TargetInvocationException error)
            {System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException??error).Throw();throw;}
            throw new InvalidOperationException("forced budget must be inside the production owner scope");
        }
        private static object incidentContext;
        private static void BudgetAndPathOffSelection(object context,object combat,object host,object attack,object input,bool checkWorldClock=false,bool budgetComponent=true,bool recoverCold=false)
        {
            incidentContext=context;incidentClearLine=false;if(budgetComponent)GeometryAllowanceIntervals(host);
            var audit=new Harmony("JueMingR.Tests.IncidentBudgetSelection");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13),postfix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks),nameof(IncidentBirth)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks),nameof(IncidentHit)));
            var solve=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileEnvironment",true).GetMethod("Solve",Flags);
            audit.Patch(solve,prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks),nameof(AbortPreparedPlan)));
            try
            {
                if(!budgetComponent){if(recoverCold)PathOffSelection(context,combat,host,attack,input,true,true);else foreach(bool clearLine in new[]{false,true})PathOffSelection(context,combat,host,attack,input,clearLine);return;}
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                incidentPlayer=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,39,0,0);incidentPlayer.position=new Vector2(700,646);incidentPlayer.inventory[54].SetDefaults(40);incidentPlayer.inventory[54].stack=999;incidentPlayer.releaseUseItem=true;
                Main.screenPosition=new Vector2(480,400);incidentReceiver=NativeCombatControlPreviewChecks.Dummy(2,60,40);incidentMouse=new Vector2(550,430);
                // This display-budget component needs Path's declared demand.
                // Separate ordinary Aim-only idle/cost cases keep Path OFF.
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,false,true));
                if(checkWorldClock)
                {
                    // A real unsampled outer update is a supported native
                    // lifecycle, and advances runtime without a world step.
                    // Do not manufacture clock values to hide this boundary.
                    uint worldBefore=Main.GameUpdateCount;ulong runtimeBefore=(ulong)Get(context,"updateTick");
                    NativeToolExecutionChecks.Outer(context,input,1);
                    Require(Main.GameUpdateCount==worldBefore && (ulong)Get(context,"updateTick")==runtimeBefore+1,"one real unsampled runtime entry establishes the independent clock without changing the native world counter");
                    Console.WriteLine("BUDGET real-unsampled-outer callbacks=1 GUC="+worldBefore+" runtimeTick="+runtimeBefore+"->"+Get(context,"updateTick"));
                }
                // Fixed natural preparation interval, never a search for a
                // convenient successful Contact. These are functional-leaf
                // warmups; cold product/presentation windows stay separate.
                var warmPreview=new char[60];var warmBudget=new char[60];
                for(int i=0;i<60;i++)
                {
                    NativeCombatSelectionPreviewChecks.Step(context,incidentMouse,false);
                    warmPreview[i]=GetOptional(attack,"ExpectedPreview")!=null?'P':'M';
                    warmBudget[i]=(bool)Get(Get(host,"PredictionWork"),"Exceeded")?'E':'_';
                }
                Console.WriteLine("BUDGET fixed-natural-warmup updates=60 preview(P=present/M=missing)="+new string(warmPreview)+" completed-budget(E=exceeded/_=clear)="+new string(warmBudget)+" endGUC="+Main.GameUpdateCount+" runtimeTick="+Get(context,"updateTick"));
                Require(GetOptional(attack,"ExpectedPreview")!=null,"budget scene has an independently useful original arc before injection");
                incidentPlayer.inventory[0]=incidentPlayer.HeldItem.Clone();budgetAttack=attack;budgetHost=host;budgetContext=context;abortedPlan=null;forceBudget=true;incidentBirths=incidentHits=0;
                NativeCombatSelectionPreviewChecks.Step(context,incidentMouse,true);
                Require(!forceBudget && abortedPlan!=null && forcedTick==Main.GameUpdateCount && incidentBirths==1,"forced allowance exhaustion happens after a real plan starts, and the same update still emits one native Basic attack");
                Require(!(bool)Get(abortedPlan,"Prepared") && GetOptional(abortedPlan,"Contact")==null && !(bool)Get(attack,"Failed") && !(bool)Get(host,"pathFailed"),"aborted scratch is retired without recording a persistent attack/path fault");
                var selection=(NpcIdentity)Get(Get(host,"Selection"),"Target");
                Require(!(bool)Call(attack,"CandidateMissCurrent",abortedPlan,selection,abortedSample,abortedPhase,abortedRequired),"an incomplete budget-aborted plan is not an independently proved candidate miss at its exact captured phase/sample/consumer range");
                if(checkWorldClock)
                {
                    // Step has already called the real CompletedWorld runtime
                    // entry. Its naturally independent tick must not reset the
                    // allowance consumed by BeforeNPC in this same world step.
                    Require(forcedRuntimeTick!=(ulong)forcedTick && (ulong)Get(context,"updateTick")==forcedRuntimeTick+1,"the real runtime counter is naturally offset and its CompletedWorld entry actually ran once");
                    Require((bool)Get(Get(host,"PredictionWork"),"Exceeded") && (bool)Get(attack,"BasicPermission") && incidentBirths==1,"CompletedWorld preserves this real GUC's exhausted allowance and legal native Basic consumer despite the different runtime counter");
                }
                NativeCombatSelectionPreviewChecks.Step(context,incidentMouse,false);
                Require(Main.GameUpdateCount==forcedTick+1 && !(bool)Get(Get(host,"PredictionWork"),"Exceeded") && GetOptional(attack,"ExpectedPreview")!=null && !(bool)Get(attack,"Failed"),"next real native update resets the allowance and recovers enhancement without a retry/reset command");
                Console.WriteLine("PASS forced budget partial-plan/no-miss/same-update native Basic/next-native-tick recovery");
                if(checkWorldClock)Console.WriteLine("PASS budget world-clock same-GUC CompletedWorld preserves exhaustion runtimeTick="+forcedRuntimeTick+" realGUC="+forcedTick+"; next real GUC alone restores enhancement");
                budgetAttack=budgetHost=budgetContext=null;
                foreach(bool clearLine in new[]{false,true})PathOffSelection(context,combat,host,attack,input,clearLine);
            }
            finally{forceBudget=incidentClearLine=false;budgetAttack=budgetHost=budgetContext=abortedPlan=abortedPhase=incidentContext=null;incidentPlayer=null;incidentReceiver=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static void GeometryAllowanceIntervals(object host)
        {
            // Exercise the actual Release owner implementation at the real
            // world key. No world counter or clock delay is fabricated.
            var type=Get(host,"PredictionWork").GetType();var enter=type.GetMethod("Enter",Flags);
            var geometry=(Action)Delegate.CreateDelegate(typeof(Action),type.GetMethod("CheckGeometry",Flags));
            var spent=type.GetField("spent",Flags);var allowance=type.GetField("allowance",Flags);
            Func<object> create=()=>Activator.CreateInstance(type,new object[]{6.0});
            Func<object,IDisposable> scope=owner=>(IDisposable)enter.Invoke(owner,new object[]{(ulong)Main.GameUpdateCount});
            Func<bool> exhausted=()=>{try{geometry();return false;}catch(Exception error){if(error.GetType().Name=="CombatPredictionBudgetExceededException")return true;throw;}};
            var first=create();
            using(scope(first)){spent.SetValue(first,allowance.GetValue(first));Require(exhausted(),"the first geometry access immediately checks an exhausted real owner allowance");}
            var shared=create();
            using(scope(shared)){geometry();spent.SetValue(shared,allowance.GetValue(shared));}
            int calls=0;bool stopped=false;
            while(!stopped && calls<32)
            {
                // Repeated short scopes and one nested scope use this SAME
                // GUC/owner. Neither is allowed to restart the cell interval.
                using(scope(shared))using(scope(shared)){calls++;stopped=exhausted();}
            }
            Require(stopped && calls<=32,"geometry checks cannot indefinitely defer exhaustion through same-GUC short or nested scopes");
            using(scope(shared))Require(exhausted(),"an already-exhausted owner rejects the very next geometry call");
            Console.WriteLine("PASS geometry allowance first access / shared short+nested scope interval calls="+calls+" (at most32), immediate exceeded signal; actual GUC="+Main.GameUpdateCount);
        }
        private static void PathOffSelection(object context,object combat,object host,object attack,object input,bool clearLine,bool recoverCold=false)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            incidentPlayer=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);incidentPlayer.position=new Vector2(700,646);incidentPlayer.inventory[54].SetDefaults(3104);incidentPlayer.inventory[54].stack=1;incidentPlayer.releaseUseItem=true;
            Main.screenPosition=new Vector2(480,400);for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(48,y,1);
            var blocked=NativeCombatControlPreviewChecks.Dummy(2,52,40);var feasible=NativeCombatControlPreviewChecks.Dummy(3,33,40);
            Require(MovingDistance(blocked,incidentPlayer.Center)<MovingDistance(feasible,incidentPlayer.Center),"nearest and clear-line modes have independently different distance/reachability winners");
            incidentReceiver=clearLine?feasible:blocked;incidentMouse=clearLine?new Vector2(900,430):new Vector2(550,430);incidentBirths=incidentHits=incidentFarBirths=0;incidentFirstFar=-1;incidentClearLine=clearLine;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,clearLine,false,true,25,false,true));
            for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,incidentMouse,false);
            int duration=recoverCold?160:30,opportunities=0;var observed=new List<int>();for(int i=0;i<duration;i++)
            {
                incidentFrame=i;bool pressed=i==0 || recoverCold && incidentFarBirths==0 && incidentPlayer.itemAnimation==0 && incidentPlayer.itemTime==0 && incidentPlayer.reuseDelay<=0;if(pressed)opportunities++;
                NativeCombatSelectionPreviewChecks.Step(context,incidentMouse,pressed);
                int selected=((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot;observed.Add(selected);
                Console.WriteLine("PATHOFF observation frame="+i+" tick="+Main.GameUpdateCount+" pressed="+pressed+" urgent="+Get(attack,"UrgentPreparation")+" selected="+selected);
                if(i==0 && !clearLine)Require(selected==incidentReceiver.whoAmI,"Path OFF nearest input opportunity selects the current blocked receiver");
                Require(((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(0)==null,"Path OFF does not acquire a hidden path-display consumer");
            }
            Require(incidentBirths==opportunities && (!clearLine?incidentHits==0:!recoverCold || incidentFarBirths>0 && incidentHits>0),"the unchanged first click and each legal natural repress are real consumers; a recovery window must serve and damage the reachable far receiver");
            Console.WriteLine((clearLine && incidentFarBirths==0?"OBSERVED cold single input":"PASS Path OFF")+" Aim ON clearLine="+clearLine+" recovery="+recoverCold+" actualOpportunities="+opportunities+" firstFar="+incidentFirstFar+" farEnhanced="+incidentFarBirths+" observations="+observed.Count+"/"+duration+" slots="+string.Join(",",observed)+" nativeBirths="+incidentBirths+" nativeHits="+incidentHits);
        }
        private static void TracePathOffFailure(object host,object attack,NPC blocked,NPC feasible,bool clearLine,int frame)
        {
            var selected=(NpcIdentity)Get(Get(host,"Selection"),"Target");var cache=(NpcPredictionCache)Get(Get(host,"Prediction"),"Cache");var work=Get(host,"PredictionWork");
            Console.WriteLine("PATHOFF FIRST FAILURE tick="+Main.GameUpdateCount+" frame="+frame+" clearLine="+clearLine+" selected="+selected.Slot+"/generation="+selected.Generation+"/type="+selected.Type+" expected="+incidentReceiver.whoAmI+"/generation="+incidentReceiver.generation+" player="+incidentPlayer.Center+" direction="+incidentPlayer.direction+" animation="+incidentPlayer.itemAnimation+" time="+incidentPlayer.itemTime+" budgetExceeded="+Get(work,"Exceeded")+" budgetMs="+Get(work,"UsedMilliseconds")+" attackFailed="+Get(attack,"Failed")+" pathFailed="+Get(host,"pathFailed")+" selectionFailed="+Get(host,"selectionFailed")+" targetPredictionFailed="+Get(host,"TargetPredictionFailed")+" read0="+(cache.Read(0)==null?"none":cache.Read(0).Identity.Slot.ToString())+" read1="+(cache.Read(1)==null?"none":cache.Read(1).Identity.Slot.ToString()));
            foreach(var npc in new[]{blocked,feasible})
            {
                Console.WriteLine("PATHOFF NPC slot="+npc.whoAmI+" generation="+npc.generation+" active="+npc.active+" life="+npc.life+" immune="+npc.immune[incidentPlayer.whoAmI]+" position="+npc.position+" velocity="+npc.velocity+" friendly="+npc.friendly+" dontTakeDamage="+npc.dontTakeDamage);
                var plans=(Array)Get(attack,"candidatePlans");
                foreach(int key in new[]{npc.whoAmI*2,npc.whoAmI*2+1})
                {
                    var plan=plans.GetValue(key);if(plan==null){Console.WriteLine("PATHOFF plan="+key+" none");continue;}
                    var timeline=(NpcTrajectory)GetOptional(plan,"Timeline");var contact=(AttackContact)GetOptional(plan,"Contact");
                    bool miss=timeline!=null && (bool)Call(attack,"CandidateMissCurrent",plan,timeline.Identity,timeline.SampleTick,Get(plan,"Phase"),Math.Max(1,timeline.Count-1));
                    Console.WriteLine("PATHOFF plan="+key+" prepared="+Get(plan,"Prepared")+" phase="+Get(plan,"Phase")+" age="+Get(plan,"Age")+" delay="+Get(plan,"Delay")+" timeline="+(timeline==null?"none":timeline.Identity.Slot+"/"+timeline.SampleTick+"/"+timeline.Count)+" contact="+(contact==null?"none":contact.Timeline.Identity.Slot+"/"+contact.Tick+"/aim="+contact.AimX+","+contact.AimY)+" candidateMissCurrent="+miss);
                }
            }
        }
        private static void MovingNativeStep(object context,Vector2 mouse,bool use,bool right)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");NativeToolExecutionChecks.Sample(context,Get(context,"Input"),mouse,use);
            PlayerInput.Triggers.Current.Right=right;PlayerInput.Triggers.Current.Left=!right;Main.oldKeyState=Main.keyState;Main.keyState=new KeyboardState(right?Keys.D:Keys.A);
            Call(combat,"Sample");Call(host,"SampleMouse");int x=Main.mouseX,y=Main.mouseY;
            NativeQuickItemChecks.BeginWorldStep();Main.LocalPlayer.Update(0);foreach(var npc in Main.npc)if(npc.active)npc.UpdateNPC(npc.whoAmI);
            for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}Main.ProjectileUpdateLoopIndex=-1;
            Call(context,"UpdateRuntime");Call(context,"UpdateShell");Require(Main.mouseX==x && Main.mouseY==y,"moving native use returns the physical cursor");
        }
        private static void MovingPlayerCandidates(object context,object combat,object host,object attack,object input)
        {
            var outlet=new Harmony("JueMingR.Tests.MovingPlayerCandidates");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})outlet.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            outlet.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks),nameof(SolarDamage)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.inventory[54].SetDefaults(3104);p.inventory[54].stack=1;p.releaseUseItem=true;Main.dayTime=false;Main.screenPosition=new Vector2(480,400);
                for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(48,y,1);
                for(int i=0;i<6;i++){var n=Main.npc[i+2];n.SetDefaults(2);n.whoAmI=i+2;n.active=true;n.target=0;n.Center=new Vector2(810+i*7,626+i*2);n.velocity=new Vector2(-1,-.2f);n.life=n.lifeMax=100000;Array.Clear(n.immune,0,n.immune.Length);}
                solarReceiver=NativeCombatControlPreviewChecks.Dummy(12,33,40);solarOtherReceivers=null;solarDamage=0;
                var initialNpcPositions=Main.npc.Where(n=>n.active && (n.whoAmI>=2 && n.whoAmI<=7 || ReferenceEquals(n,solarReceiver))).ToDictionary(n=>n.whoAmI,n=>n.position);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,true,false,true,25,false,true));
                for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(900,430),false);
                calls.Clear();float min=p.position.X,max=min;int moved=0,previews=0;const int frames=180;
                for(int i=0;i<frames;i++)
                {
                    float prior=p.position.X;int beforeSteps=Total("NpcMotion.Step");MovingNativeStep(context,new Vector2(900,430),i%45==0,i%24<12);
                    if(p.position.X!=prior)moved++;min=Math.Min(min,p.position.X);max=Math.Max(max,p.position.X);
                    if(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot!=solarReceiver.whoAmI)TraceMovingPlayerFailure(host,attack,input,p,i,moved,min,max,Total("NpcMotion.Step")-beforeSteps,initialNpcPositions);
                    Require(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot==solarReceiver.whoAmI,"continuous original player motion keeps the farther feasible target ahead of six blocked actors");
                    if(GetOptional(attack,"ExpectedPreview")!=null)previews++;
                    Require(Total("NpcMotion.Step")-beforeSteps<=2*7*120,"moving world-update future work is bounded by seven receivers and two actual phases, independently of repeated producer calls");
                }
                Require(moved>=frames*.8 && max-min>20 && solarDamage>0 && previews==frames,"the actual native player moves continuously while shared selection, preview and real damage remain useful");
                Report("native-moving-player-and-six-npcs");Console.WriteLine("PASS MOVING PLAYER nativeUpdates="+frames+" moved="+moved+" xRange="+min+".."+max+" npcSteps="+Total("NpcMotion.Step")+" nativeHits="+solarDamage+" previews="+previews+"; counted workload, not optimized timing or FPS");
            }
            finally{solarReceiver=null;Main.keyState=new KeyboardState();PlayerInput.Triggers.Current.Left=PlayerInput.Triggers.Current.Right=false;outlet.UnpatchAll(outlet.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static void TraceMovingPlayerFailure(object host,object attack,object input,Player p,int frame,int moved,float min,float max,int steps,IDictionary<int,Vector2> initialNpcPositions)
        {
            var selected=(NpcIdentity)Get(Get(host,"Selection"),"Target");var work=Get(host,"PredictionWork");
            Console.WriteLine("MOVING PLAYER FIRST FAILURE tick="+Main.GameUpdateCount+" frame="+frame+" inputFrame="+Get(input,"Frame")+" moved="+moved+" xRange="+min+".."+max+" player="+p.position+" velocity="+p.velocity+" controls="+p.controlLeft+","+p.controlRight+" film="+p.isControlledByFilm+" selected="+selected.Slot+"/"+selected.Generation+" expected="+solarReceiver.whoAmI+"/"+solarReceiver.generation+" futureSteps="+steps+" budgetExceeded="+Get(work,"Exceeded")+" budgetMs="+Get(work,"UsedMilliseconds")+" attackFailed="+Get(attack,"Failed")+" pathFailed="+Get(host,"pathFailed")+" selectionFailed="+Get(host,"selectionFailed"));
            foreach(var npc in Main.npc.Where(n=>n.active && n.whoAmI>=2 && n.whoAmI<=7).Concat(new[]{solarReceiver}))
            {
                Console.WriteLine("MOVING PLAYER NPC slot="+npc.whoAmI+" generation="+npc.generation+" active="+npc.active+" life="+npc.life+" immune="+npc.immune[p.whoAmI]+" position="+npc.position+" displacement="+(npc.position-initialNpcPositions[npc.whoAmI])+" velocity="+npc.velocity+" receiveDistance2="+MovingDistance(npc,p.Center));
                if(npc.whoAmI!=selected.Slot && npc.whoAmI!=solarReceiver.whoAmI)continue;
                var plans=(Array)Get(attack,"candidatePlans");
                foreach(int key in new[]{npc.whoAmI*2,npc.whoAmI*2+1})
                {
                    var plan=plans.GetValue(key);if(plan==null){Console.WriteLine("MOVING PLAYER plan="+key+" none");continue;}
                    var path=(NpcTrajectory)GetOptional(plan,"Timeline");var contact=(AttackContact)GetOptional(plan,"Contact");
                    Console.WriteLine("MOVING PLAYER plan="+key+" prepared="+Get(plan,"Prepared")+" phase="+Get(plan,"Phase")+" timeline="+(path==null?"none":path.Identity.Slot+"/"+path.SampleTick)+" contact="+(contact==null?"none":contact.Timeline.Identity.Slot+"/"+contact.Tick)+" candidateMissCurrent="+(path!=null && (bool)Call(attack,"CandidateMissCurrent",plan,path.Identity,path.SampleTick,Get(plan,"Phase"),Math.Max(1,path.Count-1))));
                }
            }
        }
    }
}
