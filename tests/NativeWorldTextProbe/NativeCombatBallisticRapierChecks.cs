using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBallisticRapierChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NPC currentTarget;private static int positive;
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,currentTarget) && __result>0)positive++;}
        internal static void Run(object context)
        {
            var model=Get(context,"Combat").GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true);
            int cases=0,actions=0;
            foreach(int type in new[]{802,842,938,939,940,941,942,943,944,945,1127})
            foreach(float ratio in type==1127?new[]{1f/3,.75f,1.25f}:new[]{1f})
            {
                var player=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),4463,0,0);
                foreach(var npc in Main.npc)npc.active=false;player.position=new Vector2(700,350);player.direction=-1;
                int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),player.MountedCenter,new Vector2(3.2f,-1.4f),type,0,0,player.whoAmI,0,ratio);
                var shot=Main.projectile[slot];var shadow=new Projectile();shadow.SetDefaults(type);shadow.velocity=shot.velocity;shadow.ai[1]=ratio;
                int limit=type==1127?(int)(16*ratio)+5:16;
                bool active=true;int action=0;
                while(action<limit && shot.active)
                {
                    for(int sub=0;sub<=shot.extraUpdates && active;sub++)
                    {action++;active=(bool)model.GetMethod("RapierPose",Flags).Invoke(null,new object[]{shadow,player,action});actions++;}
                    NativeQuickItemChecks.BeginWorldStep();shot.Update(slot);
                    Require(shot.active==active && (!active || Vector2.DistanceSquared(shot.Center,shadow.Center)<.0001f),"AI161 exact native pose/lifetime type="+type+" ratio="+ratio+" action="+action+" native="+shot.Center+" model="+shadow.Center);
                }
                cases++;
            }
            // Born spread is intentionally representative. Verify all newly
            // admitted hand members through the real replacement consumer and
            // native Damage; do not pretend their unobserved random angle is exact.
            var audit=new Harmony("JueMingR.Tests.BallisticRapierDamage");
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBallisticRapierChecks),nameof(Strike)));
            try
            {
            foreach(int weapon in new[]{4463,486,1227})
            {
                var player=NativeCombatBallisticCandidateChecks.Scene(context,weapon,42);var target=Main.npc[2];currentTarget=target;positive=0;
                player.itemAnimationMax=player.itemAnimation=player.HeldItem.useAnimation;
                var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
                NativeCombatObservationChecks.Save(host,new JueMingR.Features.Combat.ObservationOptions(false,true,false,false,true,50,true,true));
                NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");
                NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);
                Require(GetOptional(attack,"ExpectedImpact")!=null,"AI161 same-mechanism hand preparation "+weapon);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(player,new object[]{0,player.HeldItem,player.GetWeaponDamage(player.HeldItem),false});
                Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","AI161 real native input consumer "+weapon);
                for(int tick=0;tick<35 && positive==0;tick++)
                {
                    NativeQuickItemChecks.BeginWorldStep();if(tick>0)player.itemAnimation--;
                    for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}
                }
                Console.WriteLine("BALLISTIC-RAPIER actual item="+weapon+" positiveStrikes="+positive);
                Require(positive>0,"AI161 representative real native Damage "+weapon);
            }
            }
            finally{audit.UnpatchAll(audit.Id);currentTarget=null;}
            Console.WriteLine("BALLISTIC-RAPIER nativeMembers=11 poseCases="+cases+" actions="+actions+" actualHands=3");
        }
    }
}
