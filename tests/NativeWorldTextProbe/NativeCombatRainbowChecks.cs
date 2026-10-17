using System;
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
    internal static class NativeCombatRainbowChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NPC target;private static int hits;
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,target) && __result>0)hits++;}
        internal static void Run(object context)
        {
            var host=Get(context,"Combat").GetType().Assembly;var model=host.GetType("JueMingR.TerrariaHost.Combat.HostRainbowAttack",true);int cases=0,actions=0,births=0;
            foreach(int type in new[]{250,251})foreach(float angle in new[]{-1.2f,0,.8f})foreach(int clock in new[]{0,4})
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,1260,50);foreach(var npc in Main.npc)npc.active=false;Main.windPhysics=false;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,700),angle.ToRotationVector2()*10,type,0,0,p.whoAmI);var actual=Main.projectile[slot];actual.localAI[0]=clock;
                var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("Copy",Flags).Invoke(null,new object[]{actual});var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
                for(int action=0;action<40;action++)
                {
                    int before=Main.projectile.Count(q=>q.active && q.type==251);var args=new object[]{shadow,terrain,false};var center=actual.Center;var velocity=actual.velocity;
                    Require((bool)model.GetMethod("Ai",Flags).Invoke(null,args),"AI046 concrete captured continuation");typeof(Projectile).GetMethod("AI",Flags).Invoke(actual,null);
                    var children=Main.projectile.Where(q=>q.active && q.type==251 && q.whoAmI!=slot).ToArray();bool born=Main.projectile.Count(q=>q.active && q.type==251)>before;
                    Require((bool)args[2]==born,"AI046 exact native emitter clock");if(born){var child=children.Last();Require(child.Center==center && child.velocity==velocity*.001f,"AI046 trail birth precedes front acceleration/movement");births++;}
                    Require(actual.active==shadow.active && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.00001f && actual.ai[0]==shadow.ai[0] && actual.localAI[0]==shadow.localAI[0] && actual.timeLeft==shadow.timeLeft,"AI046 exact direction-dependent clock, scale cap and lifetime type="+type+" angle="+angle+" action="+action+" actual="+actual.velocity+"/"+actual.ai[0]+"/"+actual.localAI[0]+"/"+actual.timeLeft+" center="+actual.Center+" active="+actual.active+" world="+Main.maxTilesX+","+Main.maxTilesY+" model="+shadow.velocity+"/"+shadow.ai[0]+"/"+shadow.localAI[0]+"/"+shadow.timeLeft);
                    if(!actual.active)break;actual.position+=actual.velocity;shadow.position+=shadow.velocity;actual.timeLeft--;shadow.timeLeft--;actions++;
                }
                cases++;
            }
            foreach(bool shimmer in new[]{false,true})
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,1260,50);foreach(var npc in Main.npc)npc.active=false;var center=shimmer?new Vector2(700,700):new Vector2(700,16);if(shimmer){Main.tile[43,43].liquid=255;Main.tile[43,43].liquidType(3);}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,new Vector2(6,-2),250,0,0,p.whoAmI);var actual=Main.projectile[slot];actual.localAI[0]=shimmer?0:4;var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("Copy",Flags).Invoke(null,new object[]{actual});var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var args=new object[]{shadow,terrain,false};Require((bool)model.GetMethod("Ai",Flags).Invoke(null,args),"Rainbow concrete native Kill boundary");actual.AI();Require(!actual.active && !shadow.active && actual.timeLeft==shadow.timeLeft && actual.ai[0]==shadow.ai[0] && actual.localAI[0]==shadow.localAI[0],"world-edge Kill returns, shimmer Kill continues the same native emitter AI");
            }
            var audit=new Harmony("JueMingR.Tests.RainbowInput");audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatRainbowChecks),nameof(Strike)));
            try
            {
                foreach(int weapon in new[]{1260})
                {
                    var p=NativeCombatBallisticCandidateChecks.Scene(context,weapon,50);target=Main.npc[2];hits=0;var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
                    NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,50,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,target,0);var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(contact!=null,"Rainbow front's true trail receives a useful hand contact");
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","Rainbow original Shoot consumes the assisted source direction");
                    bool trail=false;int first=0;for(int tick=1;tick<=120 && hits==0;tick++){NativeQuickItemChecks.BeginWorldStep();for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active){Main.ProjectileUpdateLoopIndex=index;try{Main.projectile[index].Update(index);}finally{Main.ProjectileUpdateLoopIndex=-1;}}trail|=Main.projectile.Any(q=>q.active && q.type==251);if(hits>0)first=tick;}
                    Require(trail && hits>0 && first==contact.Tick,"Rainbow original parent/trail, static receive and first Damage retain exact clocks");Console.WriteLine("RAINBOW actual item="+weapon+" first="+first+" trail="+trail+" strikes="+hits);
                }
            }
            finally{audit.UnpatchAll(audit.Id);target=null;}
            Console.WriteLine("RAINBOW aiCases="+cases+" aiActions="+actions+" exactBirths="+births+" trueDerivedHand=1");
        }
    }
}
