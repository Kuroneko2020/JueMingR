using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using System.Collections;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatBallisticCloudChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NPC currentTarget;private static int positive;
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,currentTarget) && __result>0)positive++;}
        internal static void Run(object context)
        {
            var host=Get(context,"Combat").GetType().Assembly;var model=host.GetType("JueMingR.TerrariaHost.Combat.HostCloudAttack",true);int actions=0,cases=0;
            foreach(int type in new[]{237,238,239,243,244,245})foreach(bool wet in new[]{false,true})
            {
                var player=NativeCombatBallisticCandidateChecks.Scene(context,1244,75);foreach(var npc in Main.npc)npc.active=false;Main.windPhysics=false;
                if(wet)for(int x=20;x<110;x++)for(int y=10;y<40;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(0);}
                int slot=Projectile.NewProjectile(player.GetProjectileSource_Item(player.HeldItem),new Vector2(700,350),new Vector2(2,1),type,0,0,player.whoAmI);
                var native=Main.projectile[slot];native.ai[0]=type==238 || type==244?-100:900;native.ai[1]=900;
                var copy=host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("Copy",Flags);var shadow=(Projectile)copy.Invoke(null,new object[]{native});
                var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
                var member=Activator.CreateInstance(model.GetNestedType("Member",Flags),true);Set(member,"Shot",shadow);
                var rows=(IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(member.GetType()));rows.Add(member);
                var slots=host.GetType("JueMingR.TerrariaHost.Combat.HostProjectileBirthSlots",true).GetMethod("Capture",Flags).Invoke(null,null);
                for(int step=0;step<25 && native.active;step++)
                {
                    for(int sub=0;sub<=shadow.extraUpdates && shadow.active;sub++)
                    {Require((bool)model.GetMethod("Step",Flags).Invoke(null,new object[]{member,rows,slots,terrain,null}),"AI045 concrete observed continuation");if(shadow.active && --shadow.timeLeft<=0)shadow.active=false;actions++;}
                    NativeQuickItemChecks.BeginWorldStep();native.Update(slot);
                    Require(native.active==shadow.active && (!native.active || Vector2.DistanceSquared(native.position,shadow.position)<.0001f && native.timeLeft==shadow.timeLeft),"AI045 observed body/clock/liquid native="+type+" wet="+wet+" step="+step+" actual="+native.position+" model="+shadow.position);
                }
                cases++;
            }
            var audit=new Harmony("JueMingR.Tests.BallisticCloudDamage");
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBallisticCloudChecks),nameof(Strike)));
            try
            {
            foreach(int weapon in new[]{1244,1256})
            {
                var player=NativeCombatBallisticCandidateChecks.Scene(context,weapon,54);var target=Main.npc[2];currentTarget=target;positive=0;
                var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
                NativeCombatObservationChecks.Save(observation,new JueMingR.Features.Combat.ObservationOptions(false,true,false,false,true,50,true,true));
                NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,target,0);
                var contact=GetOptional(attack,"ExpectedImpact");Require(contact!=null,"AI045 true derived rain hand preparation "+weapon);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(player,new object[]{0,player.HeldItem,player.GetWeaponDamage(player.HeldItem),false});
                Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","AI045 native raw command consumer "+weapon);
                bool cloud=false,rain=false;
                for(int tick=0;tick<120 && positive==0;tick++)
                {
                    NativeQuickItemChecks.BeginWorldStep();
                    for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active)
                    {Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}
                    cloud|=Main.projectile.Any(p=>p.active && (p.type==238 || p.type==244));rain|=Main.projectile.Any(p=>p.active && (p.type==239 || p.type==245));
                }
                Console.WriteLine("BALLISTIC-CLOUD actual item="+weapon+" cloud="+cloud+" rain="+rain+" positiveStrikes="+positive);
                Require(cloud && rain && positive>0,"AI045 actual parent/cloud/rain/native positive Damage "+weapon);
            }
            }
            finally{audit.UnpatchAll(audit.Id);currentTarget=null;}
            Console.WriteLine("BALLISTIC-CLOUD nativeMembers=6 cases="+cases+" actions="+actions+" trueDerivedHands=2");
        }
    }
}
