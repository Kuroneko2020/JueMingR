using System;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCombatAttackCostChecks
    {
        private static readonly System.Collections.Generic.Dictionary<int,int> movingNpcSteps=new System.Collections.Generic.Dictionary<int,int>();
        private static void MovingNpcStep(System.Reflection.MethodBase __originalMethod,NpcMotionState n)
        {Count(__originalMethod);int value;movingNpcSteps.TryGetValue(n.Identity.Slot,out value);movingNpcSteps[n.Identity.Slot]=value+1;}
        private static float MovingDistance(NPC n,Vector2 center)
        {var area=n.getRect();return Vector2.DistanceSquared(center,Vector2.Clamp(center,new Vector2(area.Left,area.Top),new Vector2(area.Right,area.Bottom)));}
        private static void MovingCandidates(object context,object combat,object host,object attack,object input)
        {
            int movingType=Environment.GetEnvironmentVariable("JMR_MOVING_CANDIDATE_TYPE")=="49"?49:2;
            bool use=Environment.GetEnvironmentVariable("JMR_MOVING_CANDIDATE_USE")=="1";int frames=use?24:12;
            var outlet=new Harmony("JueMingR.Tests.MovingCandidateCosts");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})outlet.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            foreach(var method in typeof(NpcMotion).GetMethods(Flags).Where(m=>m.Name=="Step" && m.GetParameters().Length==8))outlet.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks).GetMethod(nameof(MovingNpcStep),Flags)));
            if(Environment.GetEnvironmentVariable("JMR_CANDIDATE_STATE_DIFF")=="1")CandidateStateAudit(outlet);
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,95,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.ResetFallDamage();p.releaseUseItem=true;p.inventory[54].SetDefaults(3104);Main.dayTime=false;Main.screenPosition=new Vector2(480,400);
                for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(48,y,1);
                var moving=new NPC[6];var initial=new Vector2[moving.Length];
                for(int i=0;i<moving.Length;i++)
                {
                    var n=Main.npc[i+2];n.SetDefaults(movingType);n.whoAmI=i+2;n.active=true;n.target=0;n.Center=new Vector2(810+i*7,626+i*2);n.velocity=new Vector2(-1,-.2f);n.life=n.lifeMax=100000;
                    Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.shimmerTransparency=0;moving[i]=n;initial[i]=n.position;
                }
                var available=NativeCombatControlPreviewChecks.Dummy(12,33,40);
                if(use)
                {
                    solarReceiver=available;solarOtherReceivers=null;
                    outlet.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks).GetMethod(nameof(SolarDamage),Flags)));
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,use));
                for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,430),use,true);
                var source=Get(host,"Prediction");int initialContinuations=(int)Get(source,"CandidateContinuations");
                calls.Clear();movingNpcSteps.Clear();solarDamage=0;int visible=0;var selectedSlots=new System.Collections.Generic.SortedSet<int>();var watch=System.Diagnostics.Stopwatch.StartNew();
                for(int frame=0;frame<frames;frame++)
                {
                    NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,430),use,true);
                    var selected=(NpcIdentity)Get(Get(host,"Selection"),"Target");selectedSlots.Add(selected.Slot);if(GetOptional(attack,"ExpectedPreview")!=null)visible++;
                    Require(selected.Slot==available.whoAmI,"six genuinely moving blocked candidates do not hide the farther current feasible target");
                }
                watch.Stop();Require(moving.Select((n,i)=>n.active && n.position!=initial[i]).All(v=>v) && visible==frames,"measured candidate loop contains six real moving actors and a useful preview on every update");
                if(use)Require(solarDamage>0,"moving candidate work during actual item use retains native positive strikes on the feasible dummy");
                int continuations=(int)Get(source,"CandidateContinuations")-initialContinuations;
                Report("moving-six-blocked");Console.WriteLine("MOVING CANDIDATES frames="+frames+" actualMoving=6 type="+movingType+" use="+use+" nativeDamage="+solarDamage+" visible="+visible+" selected="+string.Join(",",selectedSlots)+" npcSteps="+Total("NpcMotion.Step")+" continuations="+continuations+" attackSolves="+Total("AttackIntercept.Solve")+" elapsedMs="+watch.Elapsed.TotalMilliseconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                Console.WriteLine("MOVING CANDIDATE stepsBySlot="+string.Join(";",movingNpcSteps.OrderBy(pairs=>pairs.Key)));
                int movingSteps=moving.Sum(n=>movingNpcSteps[n.whoAmI]);
                // Actual use invokes both BeforeNpc and CompletedWorld. The
                // selected dummy has its own normal producer; measure these
                // six blocked candidates directly rather than guessing how
                // often another owner's full prediction is requested.
                Require(continuations>=frames*6 && movingSteps<=frames*6*(use?4:2),"moving misses continue only endpoint actions per capture phase, retaining useful selection without full 120-action replay");
                CheckCandidateSuffixes(source,moving);
                p.position.X-=16;int priorContinuations=(int)Get(source,"CandidateContinuations");
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,430),false,true);
                Require((int)Get(source,"CandidateContinuations")==priorContinuations && ((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot==available.whoAmI && GetOptional(attack,"ExpectedPreview")!=null,"a changed real player position rejects old candidate suffixes while retaining the correct useful farther target");
                // The old farther target remains usable. A real changed local
                // tile lease must immediately let the nearer actor win again.
                for(int y=36;y<=45;y++)Main.tile[48,y].active(false);
                NativeCombatSelectionPreviewChecks.Step(context,new Vector2(650,430),false,true);
                var openedTarget=(NpcIdentity)Get(Get(host,"Selection"),"Target");var openedContact=(AttackContact)GetOptional(attack,"ExpectedPreview");
                Console.WriteLine("MOVING WALL OPEN type="+movingType+" selected="+openedTarget.Slot+" preview="+(openedContact==null?"none":openedContact.Timeline.Identity.Slot.ToString())+" player="+p.Center+" candidates="+string.Join(";",moving.Concat(new[]{available}).Select(n=>n.whoAmI+":"+n.Center+":receiveDistance2="+MovingDistance(n,p.Center).ToString("R",System.Globalization.CultureInfo.InvariantCulture))));
                // Collision turns change the actors' distance order during
                // these real updates. Slot order describes only their spawn.
                int nearest=moving.OrderBy(n=>MovingDistance(n,p.Center)).ThenBy(n=>n.whoAmI).First().whoAmI;
                Require(openedTarget.Slot==nearest && openedContact!=null && openedContact.Timeline.Identity.Equals(openedTarget) && available.active,"opening the wall immediately chooses the currently nearest feasible actor with a matching preview");
            }
            finally{solarReceiver=null;outlet.UnpatchAll(outlet.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static void CheckCandidateSuffixes(object source,NPC[] moving)
        {
            var candidate=Get(source,"candidateSource");var entries=(Array)Get(candidate,"candidateInputs");var terrain=(IPredictionTerrain)Get(candidate,"Terrain");int points=0;
            foreach(var npc in moving)
            {
                var entry=entries.GetValue(npc.whoAmI*2);var proof=Get(entry,"Continuation");var path=(NpcTrajectory)Get(proof,"path");
                var fresh=new RollingNpcPrediction().Prepare((NpcMotionState[])Get(entry,"States"),1,0,path.CaptureTick,120,path.RelationVersion,(PredictionEnvironment)Get(entry,"Environment"),(PredictionPlayerMotion)Get(entry,"Player"),terrain,(bool[])Get(entry,"Roles"),(bool)Get(entry,"PlayerAdvanced"));
                Require(fresh!=null && fresh.Count==121 && path.Count==121 && fresh.CaptureTick==path.CaptureTick,"continued and fully recomputed candidates cover the same fresh complete horizon");
                for(int i=0;i<121;i++){Require(path[i].Equals(fresh[i]),"continued candidate exactly matches fresh complete prediction slot="+npc.whoAmI+" point="+i);points++;}
            }
            Console.WriteLine("PASS moving candidate suffixes equal fresh full predictions points="+points+"; validation replay excluded from workload interval");
        }
        private static NPC solarReceiver;private static int solarDamage;private static NPC[] solarOtherReceivers;
        private static void SolarDamage(NPC __instance,int __result){if(__result>0 && (ReferenceEquals(__instance,solarReceiver) || solarOtherReceivers!=null && solarOtherReceivers.Contains(__instance)))solarDamage++;}
        private static void SolarComparable(object context,object combat,object host,object attack,object input)
        {
            var outlet=new Harmony("JueMingR.Tests.SolarComparable");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})outlet.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            outlet.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks).GetMethod(nameof(SolarDamage),Flags)));
            try
            {
                foreach(string scene in new[]{"idle","continuous","unreachable","multiple"})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3473,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.ResetFallDamage();p.releaseUseItem=true;Main.screenPosition=new Vector2(480,400);Main.rand=new Terraria.Utilities.UnifiedRandom(7193);
                    bool use=scene=="continuous" || scene=="multiple";int duration=use?45:12;
                    solarOtherReceivers=null;solarReceiver=NativeCombatControlPreviewChecks.Dummy(2,scene=="unreachable"?90:55,40);
                    if(scene=="multiple")
                    {
                        solarReceiver.active=false;solarReceiver=NativeCombatControlPreviewChecks.Dummy(2,52,40);solarOtherReceivers=new[]{NativeCombatControlPreviewChecks.Dummy(3,54,40),NativeCombatControlPreviewChecks.Dummy(4,57,40)};
                    }
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,50,true,true));
                    for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(560,580),false,true);
                    calls.Clear();solarDamage=0;int preview=0,live=0;var selectedSlots=new System.Collections.Generic.SortedSet<int>();var watch=System.Diagnostics.Stopwatch.StartNew();
                    for(int i=0;i<duration;i++)
                    {
                        NativeCombatSelectionPreviewChecks.Step(context,new Vector2(560,580),use);
                        // Historical selection keeps an old reachable member
                        // after another becomes nearer. Count actual useful
                        // damage across this same three-receiver scene and
                        // expose selected slots; correctness has its own leaf.
                        selectedSlots.Add(((NpcIdentity)Get(Get(host,"Selection"),"Target")).Slot);
                        if(GetOptional(attack,"ExpectedPreview")!=null)preview++;
                        if(Main.projectile.Any(q=>q.active && q.type==611 && q.owner==0))live++;
                    }
                    watch.Stop();Report("solar-comparable-"+scene);
                    if(use)Require(solarDamage>0 && live>0 && preview>0,"same-scene Solar workload retains the real useful attack: "+scene);
                    else Require(solarDamage==0 && live==0 && (scene=="idle"?preview==duration:preview==0),"finite Solar idle/miss work preserves actual outcome: "+scene);
                    Console.WriteLine("SOLAR COMPARE scene="+scene+" frames="+duration+" nativeDamage="+solarDamage+" nativeLive="+live+" preview="+preview+" selectedSlots="+string.Join(",",selectedSlots)+" modelCalls="+(Total("HostMeleeAttack.SolvePrepared")+Total("HostMeleeAttack.KnownSolar"))+" poses="+Total("HostMeleeAttack.SolarPose")+" elapsedMs="+watch.Elapsed.TotalMilliseconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"; original Player/NPC/projectile/preparation component, not FPS");
                }
            }
            finally{solarReceiver=null;solarOtherReceivers=null;outlet.UnpatchAll(outlet.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static void NaturalCosts(object context,object combat,object host,object attack,object input)
        {
            var outlet=new Harmony("JueMingR.Tests.NaturalAttackCosts");
            foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})outlet.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            outlet.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatAttackCostChecks).GetMethod(nameof(SolarDamage),Flags)));
            try
            {
                foreach(int type in new[]{3475,2797,3541})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,type,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.statMana=p.statManaMax=p.statManaMax2=200;p.inventory[54].SetDefaults(3104);p.inventory[54].stack=1;Main.screenPosition=new Vector2(600,400);
                    var target=NativeCombatControlPreviewChecks.Dummy(2,52,40);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
                    for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                    calls.Clear();int visible=0;
                    for(int i=0;i<8;i++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);if(GetOptional(attack,"ExpectedPreview")!=null)visible++;}
                    Report("natural-idle-controller-"+type);int solves=Total("HostHeldAttack.SolvePrepared")+Total("HostAttackControl.BubblePreview");
                    Require(visible==8 && solves<=2 && !Main.projectile.Any(q=>q.active),"equivalent natural idle updates reuse a useful private opening, not native sources: "+type+" solves="+solves);
                    calls.Clear();target.active=false;NativeCombatControlPreviewChecks.Dummy(3,54,40);NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                    Require(Total("HostHeldAttack.SolvePrepared")+Total("HostAttackControl.BubblePreview")>0,"different real target identity invalidates the idle opening: "+type);
                    var result=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(result!=null && result.Timeline.Identity.Slot==3,"idle result rebind never retains the former selected target: "+type);
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3473,0,0);player.position=new Vector2(700,646);player.ResetEffects();player.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);
                solarReceiver=NativeCombatControlPreviewChecks.Dummy(2,55,40);solarDamage=0;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));
                for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                calls.Clear();int previews=0,born=0;
                for(int i=0;i<45;i++)
                {NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);if(GetOptional(attack,"ExpectedPreview")!=null)previews++;if(Main.projectile.Any(q=>q.active && q.type==611))born++;}
                Report("solar-natural-player-npc-projectile45");int poses=Total("HostMeleeAttack.SolarPose"),solvesSolar=Total("HostMeleeAttack.SolvePrepared")+Total("HostMeleeAttack.KnownSolar");
                Require(born>0 && solarDamage>0 && previews>0 && poses>0 && poses<=29*solvesSolar,"whole natural Solar cycle retains native births, Damage and preview with deduplicated finite poses");
                Console.WriteLine("SOLAR natural actions=45 live="+born+" actualDamage="+solarDamage+" preview="+previews+" poses="+poses+" modelCalls="+solvesSolar+"; component workload, not FPS");
                player.inventory[0].SetDefaults(3509);calls.Clear();for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false);
                Require(Total("HostMeleeAttack.SolvePrepared")==0 && Total("HostMeleeAttack.KnownSolar")==0 && Total("HostMeleeAttack.SolarPose")==0 && GetOptional(attack,"ExpectedPreview")==null,"natural switch-away ends Solar solving and preview even while an old native arc finishes");
                HarpyCosts(context,combat,host,attack,input);
            }
            finally{solarReceiver=null;outlet.UnpatchAll(outlet.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static void HarpyCosts(object context,object combat,object host,object attack,object input)
        {
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,39,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.armor[3].SetDefaults(6166);p.inventory[54].SetDefaults(1334);p.inventory[54].stack=999;p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);
            solarReceiver=NativeCombatControlPreviewChecks.Dummy(2,55,40);solarDamage=0;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,false));
            try
            {
                for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                Require(p.accHarpyCharm,"native equipped charm is active through Player.Update");calls.Clear();int previews=0;
                for(int i=0;i<8;i++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);if(GetOptional(attack,"ExpectedPreview")!=null)previews++;}
                Report("harpy-natural-idle8");Require(previews==8 && Total("HostHarpyAttack.Solve")<=2 && !Main.projectile.Any(q=>q.active),"equivalent complete updates retain useful Harpy preview without repeating finite replay");
                var contact=(AttackContact)GetOptional(attack,"ExpectedPreview");Call(attack,"BindPreview",contact);calls.Clear();for(int i=0;i<20;i++)Require((bool)CallResult(attack,"PreviewCurrent",contact),"same-update Harpy presentation lease remains visible");
                Report("harpy-presentation20");Require(Total("HostHarpyAttack.Solve")==0 && Total("State.get_Current")==0 && Total("PredictionTerrain.Read")==0 && Total("HostAttackObstacles.Eligible")==0,"Harpy presentation cannot replay, scan scene or read terrain");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));calls.Clear();previews=0;
                for(int i=0;i<40;i++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);if(GetOptional(attack,"ExpectedPreview")!=null)previews++;}
                Report("harpy-natural-first-use40");Require(solarDamage>0 && previews>0 && Total("HostHarpyAttack.Solve")>0 && Total("HostHarpyAttack.Solve")<=40*6,"Harpy complete preparation keeps natural damage and preview within finite phase/candidate work");
                Console.WriteLine("HARPY natural updates=40 actualDamage="+solarDamage+" preview="+previews+" finiteSolves="+Total("HostHarpyAttack.Solve")+"; component work, not FPS");
                p.inventory[0].SetDefaults(3509);calls.Clear();for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false);Require(Total("HostHarpyAttack.Solve")==0 && GetOptional(attack,"ExpectedPreview")==null,"switch-away ends Harpy attack demand despite equipped charm");
            }
            // Later component-only scenarios do not run Player.Update's
            // equipment reset. Remove this fixture's scalar charm effects as
            // well as its item, so their original no-accessory premise holds.
            finally{p.armor[3].TurnToAir();p.ResetEffects();solarReceiver=null;}
        }
        private static object CallResult(object instance,string name,params object[] args){return instance.GetType().GetMethod(name,Flags).Invoke(instance,args);}
    }
}
