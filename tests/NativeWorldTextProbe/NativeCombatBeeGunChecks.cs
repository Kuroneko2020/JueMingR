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
    internal static class NativeCombatBeeGunChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static bool watching;private static NPC target;private static Projectile receiving;
        private static int births,baseDamage,ordinary,giant,solves,captures,motors;private static long first;
        private static Vector2 birthVelocity,birthCenter;
        private static void SolveWork(){solves++;}
        private static void CaptureWork(){captures++;}
        private static void MotorWork(){motors++;}
        private static void Born(int __result)
        {
            if(!watching || __result<0 || __result>=Main.maxProjectiles)return;var q=Main.projectile[__result];if(q.owner!=Main.myPlayer || q.type!=181 && q.type!=566)return;
            // The Shoot branch sets magic immediately after NewProjectile;
            // inspect that flag after the complete natural outer update.
            int delta=q.damage-baseDamage;Require(q.type==181?delta>=0 && delta<=1:delta>=1 && delta<=3,"Bee Gun native damage uses its actual ordinary/strong random branch");
            if(births++==0){birthVelocity=q.velocity;birthCenter=q.Center;}if(q.type==181)ordinary++;else giant++;
        }
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}
        private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(watching && ReferenceEquals(__instance,target) && receiving!=null && receiving.owner==Main.myPlayer && receiving.magic && receiving.damage>0 && (receiving.type==181 || receiving.type==566) && __result>0 && first<0)first=Main.GameUpdateCount;}
        internal static void Run(object context)
        {
            var a=Get(context,"Combat").GetType().Assembly;var model=a.GetType("JueMingR.TerrariaHost.Combat.HostBeeGunAttack",true);var bee=a.GetType("JueMingR.TerrariaHost.Combat.HostBeeAttack",true);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");bool wind=Main.windPhysics;var random=Main.rand;var audit=new Harmony("JueMingR.Tests.BeeGun");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBeeGunChecks),nameof(SolveWork)));
            audit.Patch(bee.GetMethod("CaptureTargets",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBeeGunChecks),nameof(CaptureWork)));
            audit.Patch(bee.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBeeGunChecks),nameof(MotorWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatBeeGunChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBeeGunChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatBeeGunChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBeeGunChecks),nameof(Strike)));
            try
            {
                Require((bool)model.GetMethod("Handles",Flags).Invoke(null,new object[]{181}) && (bool)model.GetMethod("Handles",Flags).Invoke(null,new object[]{566}) && !(bool)model.GetMethod("Handles",Flags).Invoke(null,new object[]{189}) && !(bool)model.GetMethod("Handles",Flags).Invoke(null,new object[]{316}),"Wasp and Bat are distinct native AI036 roles");
                foreach(bool strong in new[]{false,true})
                {
                    watching=false;first=-1;births=ordinary=giant=solves=captures=motors=0;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),1121,0,0);p.position=new Vector2(650,646);p.statMana=p.statManaMax2=200;p.releaseUseItem=true;Main.windPhysics=false;Main.screenPosition=new Vector2(500,400);
                    for(int i=3;i<10;i++)p.armor[i].TurnToAir();if(strong)p.armor[3].SetDefaults(3333);
                    target=NativeCombatControlPreviewChecks.Dummy(2,52,40);Vector2 raw=target.Center-new Vector2(390,0);
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));
                    for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);
                    Require(p.strongBees==strong,"actual accessory update establishes the captured Hive Pack premise");
                    var preview=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(preview!=null && preview.Confidence==AttackConfidence.Representative && !Main.projectile.Any(q=>q.active),"idle Bee Gun has a read-only representative of a legal random newborn use");
                    int s=solves,c=captures,m=motors;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;
                    for(int draw=0;draw<20;draw++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",preview),"same-update Bee Gun presentation keeps the prepared lease");}
                    Require(solves==s && captures==c && motors==m,"twenty presentations perform no Bee Gun Solve, target capture or motor replay");
                    Require(captures==solves && captures>0,"each held solve captures one chase snapshot shared by its finite candidates");
                    baseDamage=p.GetWeaponDamage(p.HeldItem);long start=Main.GameUpdateCount;watching=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));
                    NativeCombatSelectionPreviewChecks.Step(context,raw,true);
                    Require(births>=1 && births<=(strong?6:5) && (strong || giant==0),"one actual Bee Gun action preserves the native legal random count/type");
                    Require(Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && (q.type==181 || q.type==566)).All(q=>q.magic),"native Shoot gives born bees their magic damage role");
                    var actual=birthVelocity.SafeNormalize(Vector2.Zero);var aim=(new Vector2(preview.AimX,preview.AimY)-birthCenter).SafeNormalize(Vector2.Zero);var physical=(raw-birthCenter).SafeNormalize(Vector2.Zero);
                    Require(Vector2.Dot(actual,aim)>.98f && Vector2.Dot(actual,physical)<0,"real Bee Gun uses the assisted hand direction while preserving the opposite physical mouse");
                    for(int i=1;i<120 && first<0;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
                    // A real training dummy is immortal. The successful
                    // StrikeNPC result from the local positive-damage magic
                    // bee is its damage witness, rather than a life delta.
                    Require(first>=start,"actual random bees deliver useful native damage after a single real press/release");watching=false;
                    Console.WriteLine("BEE GUN strong="+strong+" births="+births+" ordinary="+ordinary+" giant="+giant+" first="+(first-start)+" representative="+preview.Tick+" captures="+captures+" solves="+solves+" motor="+motors);
                }
                Console.WriteLine("PASS Bee Gun natural handheld direction/birth/damage, actual Hive Pack and readonly presentation; Wasp/Bat remain separate");
            }
            finally{watching=false;target=null;receiving=null;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;Main.rand=random;NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
    }
}
