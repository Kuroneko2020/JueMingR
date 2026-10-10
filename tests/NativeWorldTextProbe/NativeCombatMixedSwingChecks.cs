using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The first native Damage decides whether the hand or its launched body
    // wins. An independently observed real first-use rectangle supplies the
    // close positive; the product does not supply this fixture's geometry.
    internal static class NativeCombatMixedSwingChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC target;private static int projectile,firstType,weapon;private static long first;
        private static bool calibrating,recordBirth;private static int births,birthType;private static Rectangle firstBox;
        private static void Born(int __result)
        {if(recordBirth && __result>=0 && __result<Main.maxProjectiles){var shot=Main.projectile[__result];if(shot.owner==Main.myPlayer && shot.type==birthType)births++;}}
        private static void BeforeDamage(Projectile __instance){projectile=__instance.type;}
        private static void AfterDamage(){projectile=0;}
        private static void Strike(NPC __instance,int __result)
        {if(ReferenceEquals(target,__instance) && __result>0 && first<0){first=Main.GameUpdateCount;firstType=projectile;}}
        private static void Shape(Player __instance,Item __0,Rectangle __1)
        {if(calibrating && __instance.whoAmI==Main.myPlayer && __0.type==weapon && firstBox==Rectangle.Empty)firstBox=__1;}
        internal static void Run(object context)
        {
            var audit=new Harmony("JueMingR.Tests.MixedHandShot");
            // This leaf also runs directly, without FiniteShot's parent
            // setup. Achievement UI is outside the isolated native receiver.
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(Strike)));
            audit.Patch(typeof(Player).GetMethod("ItemCheck_MeleeHitNPCs",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(Shape)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatMixedSwingChecks),nameof(Born)));
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");int cases=0,handHits=0,shotHits=0;
            try
            {
                foreach(int item in new[]{723,989})
                {
                    weapon=item;var p=Reset(context,item);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);
                    for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,target.Center,false,true);
                    firstBox=Rectangle.Empty;calibrating=true;NativeCombatSelectionPreviewChecks.Step(context,target.Center,true);calibrating=false;
                    Require(firstBox.Width>0 && first<0,"real mixed first-use shape is observed away from the receiver");
                    float closeX=p.position.X+target.Hitbox.Left-firstBox.Right+1;
                    foreach(bool near in new[]{false,true})
                    {
                        p=Reset(context,item);if(near)p.position.X=closeX;target=NativeCombatControlPreviewChecks.Dummy(2,52,40);
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,false));AttackContact preview=null;
                        for(int i=0;i<3;i++)
                        {NativeCombatSelectionPreviewChecks.Step(context,target.Center,false,true);preview=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(!Main.projectile.Any(q=>q.active) && p.itemAnimation==0,"idle mixed preview neither swings nor fires");}
                        Require(preview!=null,"both close hand and distant native shot have a held preview: "+item+"/"+near);
                        long start=Main.GameUpdateCount;int shotType=p.HeldItem.shoot;birthType=shotType;births=0;recordBirth=true;
                        NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,25,true,true));
                        for(int i=1;i<=90 && first<0;i++)
                        {NativeCombatSelectionPreviewChecks.Step(context,target.Center,i==1);}
                        recordBirth=false;
                        Require(first>=0 && first-start==preview.Tick,"mixed earliest hand/shot contact matches actual native first Damage: "+item+"/"+near+" first="+(first-start)+" type="+firstType+" plan="+preview.Tick+" player="+p.position+" nativeFirstBox="+firstBox);
                        Require(near?firstType==0:firstType==shotType,"real first receiver distinguishes close hand and distant projectile: "+item+"/"+near+" type="+firstType);
                        // A near one-penetration shot can be born and killed
                        // by native Damage before the outer update returns.
                        // Observe its actual birth instead of requiring it to
                        // remain active after that valid receive.
                        Require(births>0,"native mixed use preserves its independently launched projectile");
                        if(near)handHits++;else shotHits++;cases++;
                        Console.WriteLine("MIXED HAND item="+item+" near="+near+" first="+(first-start)+" damageType="+firstType+" preview="+preview.Tick+" actualBirths="+births);
                    }
                }
                Require(cases==4 && handHits==2 && shotHits==2,"mixed stage oracle has both real hand and shot winners");
                Console.WriteLine("PASS native mixed sword preview: close hand and distant projectile, actual first Damage, idle Aim-off readonly");
            }
            finally{calibrating=recordBirth=false;target=null;projectile=0;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
        private static Player Reset(object context,int item)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);p.position=new Vector2(650,646);p.ResetEffects();p.ResetFallDamage();p.ResetMeleeHitCooldowns();p.attackCD=0;p.releaseUseItem=true;
            Main.windPhysics=false;Main.screenPosition=new Vector2(500,400);first=-1;firstType=-1;return p;
        }
    }
}
