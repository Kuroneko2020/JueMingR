using System;
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
    // Original feedback is an input-consumption claim. Observe the actual
    // native birth while ItemCheck owns its cursor scope, before homing,
    // collision or a lucky eventual hit can hide an unassisted launch.
    internal static class NativeCombatOriginalRangedChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static Player player;private static NPC receiver;private static Vector2 physical;
        private static int expectedProjectile,item,phase,births,aimed,raw,hits;
        private static bool enabled;
        private static readonly int[] phaseBirths=new int[3];
        private static readonly List<string> observations=new List<string>();
        private static void Born(Player __instance,Projectile __0)
        {
            if(!ReferenceEquals(__instance,player) || __0.owner!=player.whoAmI)return;
            Require(__0.type==expectedProjectile,"original configured ammunition reaches its actual native source: item="+item+" projectile="+__0.type+" expected="+expectedProjectile);
            births++;phaseBirths[phase]++;
            var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;
            var ray=__0.velocity.SafeNormalize(Vector2.UnitX);
            var target=(receiver.Center-__0.Center).SafeNormalize(Vector2.UnitX);
            var manual=(physical-__0.Center).SafeNormalize(-Vector2.UnitX);
            float targetDot=Vector2.Dot(ray,target),rawDot=Vector2.Dot(ray,manual);
            bool assisted=cursor.X>player.Center.X && targetDot>.65f && rawDot<0;
            bool unassisted=Vector2.DistanceSquared(cursor,physical)<=2 && rawDot>.65f && targetDot<0;
            if(assisted)aimed++;if(unassisted)raw++;
            observations.Add("phase="+phase+" tick="+Main.GameUpdateCount+" source="+__0.type+" cursor="+cursor+" velocity="+__0.velocity+" targetDot="+targetDot+" rawDot="+rawDot);
            // Native scatter remains native: broad opposing half-planes
            // distinguish its source direction without requiring every
            // random arrow/knife to hit a narrow stationary dummy.
            Require(enabled?assisted:unassisted,"native birth consumes assisted/physical input respectively: item="+item+" phase="+phase+" enabled="+enabled+" cursor="+cursor+" velocity="+__0.velocity);
        }
        private static void Hit(NPC __instance,int __result)
        {if(ReferenceEquals(__instance,receiver) && __result>0)hits++;}
        internal static void Run(object context,ProbeGraphics graphics=null,string output=null)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            var audit=new Harmony("JueMingR.Tests.OriginalRangedInputConsumers");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Player).GetMethod("TryUpdateChannel",Flags),postfix:new HarmonyMethod(typeof(NativeCombatOriginalRangedChecks).GetMethod(nameof(Born),Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatOriginalRangedChecks).GetMethod(nameof(Hit),Flags)));
            var failures=new List<string>();
            try
            {
                foreach(var row in new[]{new[]{2624,1334,278},new[]{3019,1334,278},new[]{518,0,94},new[]{1569,0,304},new[]{1446,0,297},new[]{197,75,955}})
                foreach(bool assist in new[]{false,true})
                {
                    try
                    {
                        NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                        player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,row[0],0,0);player.position=new Vector2(700,646);player.ResetEffects();player.ResetFallDamage();player.releaseUseItem=true;player.statMana=player.statManaMax=player.statManaMax2=400;
                        if(row[1]!=0){player.inventory[54].SetDefaults(row[1]);player.inventory[54].stack=999;}
                        Main.screenPosition=new Vector2(480,400);Main.GameViewMatrix.Zoom=Vector2.One;Main.rand=new Terraria.Utilities.UnifiedRandom(7193);Main.windPhysics=false;
                        receiver=NativeCombatControlPreviewChecks.Dummy(2,55,40);physical=new Vector2(550,430);item=row[0];expectedProjectile=row[2];enabled=assist;phase=0;births=aimed=raw=hits=0;Array.Clear(phaseBirths,0,phaseBirths.Length);observations.Clear();
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,assist));
                        int idleShown=0,useShown=0,useSamples=0,missing=0,longestMissing=0;
                        for(int i=0;i<4;i++){NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);if(GetOptional(attack,"ExpectedPreview")!=null)idleShown++;}
                        Require(births==0 && (bool)Get(Get(host,"Selection"),"HasTarget"),"idle original-feedback scene selects its placed dummy without attacking: "+item);
                        for(phase=0;phase<3;phase++)
                        {
                            if(phase>0)
                            {
                                for(int i=0;i<8;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                                if(phase==2)
                                {
                                    player.inventory[0].SetDefaults(3509);NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                                    player.inventory[0].SetDefaults(item);
                                    for(int i=0;i<4;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false,true);
                                }
                            }
                            // Enough real updates for multiple original uses,
                            // with no extra Prepare or supplied solution.
                            int before=births;
                            for(int frame=0;frame<45;frame++)
                            {
                                NativeCombatSelectionPreviewChecks.Step(context,physical,true);useSamples++;
                                if(GetOptional(attack,"ExpectedPreview")!=null){useShown++;missing=0;}else longestMissing=Math.Max(longestMissing,++missing);
                            }
                            Require(births-before>=2,"first/held/repress/switch-back native uses must all produce sources: "+item+" phase="+phase+" births="+(births-before));
                        }
                        for(int i=0;i<8;i++)NativeCombatSelectionPreviewChecks.Step(context,physical,false);
                        // The independent impact preview was withdrawn. Keep
                        // every one of the original 3x45 native-use actions,
                        // fresh/repeated/switch-back source births and actual
                        // input/Strike oracles below. Their validity does not
                        // depend on reviving a source-free red-box producer.
                        Require(idleShown==0 && useShown==0 && useSamples==135,"retired preview stays absent throughout all original idle and native-use samples: "+item);
                        Require(assist?aimed==births && raw==0:raw==births && aimed==0,"all actual births use the correct source direction: "+item+" enabled="+assist);
                        if(item==2624)Require(phaseBirths.All(n=>n>=5 && n%5==0),"Tsunami emits its actual five-arrow groups with Ichor ammunition");
                        if(assist)Require(hits>0,"assisted finite original attack reaches real StrikeNPC: "+item);
                        Console.WriteLine("PASS ORIGINAL ranged item="+item+" ammo="+row[1]+" projectile="+expectedProjectile+" assist="+assist+" phases="+string.Join(",",phaseBirths)+" aimed="+aimed+" raw="+raw+" actualDamage="+hits+" idlePreview="+idleShown+"/4 usePreview="+useShown+"/"+useSamples+" longestMissing="+longestMissing+" observations=["+string.Join(";",observations)+"]");
                    }
                    catch(Exception error){failures.Add(row[0]+"/"+assist+": "+error.Message);Console.WriteLine("FAIL ORIGINAL ranged item="+row[0]+" assist="+assist+": "+error);}
                }
                Require(failures.Count==0,"original ranged feedback failures: "+string.Join(" | ",failures));
            }
            finally{player=null;receiver=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
    }
}
