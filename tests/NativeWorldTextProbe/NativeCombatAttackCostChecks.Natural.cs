using System;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCombatAttackCostChecks
    {
        private static NPC solarReceiver;private static int solarDamage;
        private static void SolarDamage(NPC __instance,int __result){if(ReferenceEquals(__instance,solarReceiver) && __result>0)solarDamage++;}
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
                    Report("natural-idle-controller-"+type);int solves=Total("HostHeldAttack.Solve")+Total("HostAttackControl.BubblePreview");
                    Require(visible==8 && solves<=2 && !Main.projectile.Any(q=>q.active),"equivalent natural idle updates reuse a useful private opening, not native sources: "+type+" solves="+solves);
                    calls.Clear();target.active=false;NativeCombatControlPreviewChecks.Dummy(3,54,40);NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                    Require(Total("HostHeldAttack.Solve")+Total("HostAttackControl.BubblePreview")>0,"different real target identity invalidates the idle opening: "+type);
                    var result=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(result!=null && result.Timeline.Identity.Slot==3,"idle result rebind never retains the former selected target: "+type);
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3473,0,0);player.position=new Vector2(700,646);player.ResetEffects();player.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);
                solarReceiver=NativeCombatControlPreviewChecks.Dummy(2,55,40);solarDamage=0;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));
                for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false,true);
                calls.Clear();int previews=0,born=0;
                for(int i=0;i<45;i++)
                {NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);if(GetOptional(attack,"ExpectedPreview")!=null)previews++;if(Main.projectile.Any(q=>q.active && q.type==611))born++;}
                Report("solar-natural-player-npc-projectile45");int poses=Total("HostMeleeAttack.SolarPose"),solvesSolar=Total("HostMeleeAttack.Solve")+Total("HostMeleeAttack.KnownSolar");
                Require(born>0 && solarDamage>0 && previews>0 && poses>0 && poses<=29*solvesSolar,"whole natural Solar cycle retains native births, Damage and preview with deduplicated finite poses");
                Console.WriteLine("SOLAR natural actions=45 live="+born+" actualDamage="+solarDamage+" preview="+previews+" poses="+poses+" modelCalls="+solvesSolar+"; component workload, not FPS");
                player.inventory[0].SetDefaults(3509);calls.Clear();for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),false);
                Require(Total("HostMeleeAttack.Solve")==0 && Total("HostMeleeAttack.KnownSolar")==0 && Total("HostMeleeAttack.SolarPose")==0 && GetOptional(attack,"ExpectedPreview")==null,"natural switch-away ends Solar solving and preview even while an old native arc finishes");
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
