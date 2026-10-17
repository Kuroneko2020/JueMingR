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
    internal static class NativeCombatSeedlerChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NPC target;private static int hits;
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,target) && __result>0)hits++;}
        private static NpcTrajectory Path(NPC n)
        {
            var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,Active=true,NoTileCollide=n.noTileCollide};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        internal static void Run(object context)
        {
            var host=Get(context,"Combat").GetType().Assembly;var model=host.GetType("JueMingR.TerrariaHost.Combat.HostSeedlerAttack",true);int cases=0,actions=0;
            foreach(int type in new[]{483,484})foreach(int start in new[]{0,5,14})foreach(int liquid in new[]{-1,0,2})
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,3018,50);foreach(var npc in Main.npc)npc.active=false;Main.windPhysics=false;
                for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                if(liquid>=0)for(int x=20;x<110;x++)for(int y=10;y<40;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,350),new Vector2(7,-1),type,0,0,p.whoAmI);var actual=Main.projectile[slot];actual.ai[0]=start;
                var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("Copy",Flags).Invoke(null,new object[]{actual});
                var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
                int limit=type==483?20-start:50;
                for(int outer=0;outer<limit && actual.active;outer++)
                {
                    for(int sub=0;sub<=shadow.extraUpdates && shadow.active;sub++)
                    {var args=new object[]{shadow,terrain,130,false};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args) && !(bool)args[3],"Seedler deterministic prefix keeps its native random-death boundary");if(shadow.active && --shadow.timeLeft<=0)shadow.active=false;actions++;}
                    NativeQuickItemChecks.BeginWorldStep();actual.Update(slot);
                    Require(actual.active==shadow.active && Vector2.DistanceSquared(actual.position,shadow.position)<.001f && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.0001f && actual.ai[0]==shadow.ai[0] && actual.timeLeft==shadow.timeLeft,"Seedler native parent/child motor liquid="+liquid+" type="+type+" start="+start+" outer="+outer+" actual="+actual.position+"/"+actual.velocity+" model="+shadow.position+"/"+shadow.velocity);
                }
                cases++;
            }
            // An observed child's actual random speed and existing local
            // history are deterministic inputs. Compare exact receive time,
            // then expire an observed parent to exercise its real Kill family.
            foreach(bool expiry in new[]{false,true})foreach(int separation in new[]{70,180})
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,3018,50);foreach(var npc in Main.npc)npc.active=false;target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.life=target.lifeMax=10000;target.defense=0;target.knockBackResist=0;target.Center=new Vector2(850,350);var path=Path(target);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),target.Center-new Vector2(separation,0),new Vector2(8,0),expiry?483:484,50,0,p.whoAmI);var shot=Main.projectile[slot];if(expiry)shot.timeLeft=1;
                var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,shot,path,0,false,true},null);
                var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);AttackContact contact;
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{shot},path,clock,terrain,root});Require(contact!=null,"observed Seedler family retains true primary/derived receive");
                int first=0;bool child=false;for(int tick=1;tick<=120;tick++){NativeQuickItemChecks.BeginWorldStep();if(target.immune[p.whoAmI]>0)target.immune[p.whoAmI]--;for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active){Main.ProjectileUpdateLoopIndex=index;try{Main.projectile[index].Update(index);}finally{Main.ProjectileUpdateLoopIndex=-1;}}child|=Main.projectile.Any(q=>q.active && q.type==484);if(target.life<10000){first=tick;break;}}
                Require(first>0 && (expiry?child && contact.Confidence==AttackConfidence.Representative:first==contact.Tick),"original Seedler Kill seeds receive; observed actual child agrees with exact first Damage clock");
                Console.WriteLine("SEEDLER family expiry="+expiry+" separation="+separation+" planned="+contact.Tick+" actual="+first);
            }
            var audit=new Harmony("JueMingR.Tests.SeedlerInput");audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatSeedlerChecks),nameof(Strike)));
            try
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,3018,50);target=Main.npc[2];hits=0;var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
                NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,50,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,target,0);Require(GetOptional(attack,"ExpectedImpact")!=null,"Seedler finite hand has a true family contact");
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","Seedler actual native hand consumes its assisted direction");
                for(int tick=0;tick<120 && hits==0;tick++){NativeQuickItemChecks.BeginWorldStep();for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active){Main.ProjectileUpdateLoopIndex=index;try{Main.projectile[index].Update(index);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}
                Require(hits>0,"Seedler real Shoot/parent/seed native positive Damage");
            }
            finally{audit.UnpatchAll(audit.Id);target=null;}
            Console.WriteLine("SEEDLER native-motor-cases="+cases+" actions="+actions+" observed-families=4 actual-hand=1");
        }
    }
}
