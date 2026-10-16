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
    internal static class NativeCombatImpactBurstChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NPC target;private static int hits;
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,target) && __result>0)hits++;}
        private static NpcTrajectory Path(NPC n,int count)
        {
            var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,Active=true,NoTileCollide=n.noTileCollide};
            var points=Enumerable.Range(0,count).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        internal static void Run(object context)
        {
            var host=Get(context,"Combat").GetType().Assembly;var model=host.GetType("JueMingR.TerrariaHost.Combat.HostImpactBurstAttack",true);int cases=0,actions=0;
            foreach(int type in new[]{246,249,312})foreach(int start in new[]{0,18,59})foreach(int liquid in new[]{-1,0,2})
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,1258,50);foreach(var npc in Main.npc)npc.active=false;Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                if(liquid>=0)for(int x=20;x<110;x++)for(int y=10;y<78;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,350),new Vector2(7,-1),type,0,0,p.whoAmI);var native=Main.projectile[slot];native.ai[0]=start;
                var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("Copy",Flags).Invoke(null,new object[]{native});var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
                for(int outer=0;outer<35 && native.active;outer++)
                {
                    for(int sub=0;sub<=shadow.extraUpdates && shadow.active;sub++){var args=new object[]{shadow,terrain,false};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args) && !(bool)args[2],"finite impact motor stays in concrete no-collision prefix");if(shadow.active && --shadow.timeLeft<=0)shadow.active=false;actions++;}
                    NativeQuickItemChecks.BeginWorldStep();native.Update(slot);Require(native.active==shadow.active && Vector2.DistanceSquared(native.position,shadow.position)<.001f && Vector2.DistanceSquared(native.velocity,shadow.velocity)<.0001f && native.ai[0]==shadow.ai[0] && native.timeLeft==shadow.timeLeft,"impact native motor type="+type+" start="+start+" liquid="+liquid+" outer="+outer+" actual="+native.position+"/"+native.velocity+"/"+native.ai[0]+" model="+shadow.position+"/"+shadow.velocity+"/"+shadow.ai[0]);
                }
                cases++;
            }
            foreach(int type in new[]{246,249,312})foreach(bool inside in new[]{true,false})
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,1258,50);foreach(var npc in Main.npc)npc.active=false;var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=new Vector2(850,350);n.life=n.lifeMax=10000;n.defense=0;n.knockBackResist=0;var path=Path(n,2);
                int separation=type==246?(inside?70:100):type==249?(inside?52:80):(inside?65:90);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),n.Center-new Vector2(separation,0),Vector2.Zero,type,50,0,p.whoAmI);var shot=Main.projectile[slot];shot.timeLeft=1;
                var terrain=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,shot,path,0,false,true},null);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);
                AttackContact contact;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{shot},path,clock,terrain,root});
                NativeQuickItemChecks.BeginWorldStep();for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active){Main.ProjectileUpdateLoopIndex=index;try{Main.projectile[index].Update(index);}finally{Main.ProjectileUpdateLoopIndex=-1;}}
                Require((contact!=null)==inside && (n.life<10000)==inside,"native exact Kill burst has an outside counterexample independent of later fragments type="+type+" inside="+inside);if(type==246)Require(Main.projectile.Count(q=>q.active && q.type==249)>=2,"native Stynger Kill emits its real finite fragment family");
            }
            var audit=new Harmony("JueMingR.Tests.ImpactBurstInput");audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatImpactBurstChecks),nameof(Strike)));
            try
            {
                foreach(var row in new[]{new[]{1258,1261},new[]{1784,1785}})
                {
                    var p=NativeCombatBallisticCandidateChecks.Scene(context,row[0],50);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;target=Main.npc[2];hits=0;var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
                    NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,50,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,target,0);Require(GetOptional(attack,"ExpectedImpact")!=null,"impact family supplies a useful source contact");
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","impact native ammo source consumes its enhanced input");
                    for(int tick=0;tick<120 && hits==0;tick++){NativeQuickItemChecks.BeginWorldStep();for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active){Main.ProjectileUpdateLoopIndex=index;try{Main.projectile[index].Update(index);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}Require(hits>0,"impact actual Shoot/Update/Damage remains useful item="+row[0]);
                }
            }
            finally{audit.UnpatchAll(audit.Id);target=null;}
            Console.WriteLine("IMPACT-BURST motorCases="+cases+" actions="+actions+" exactBurstEdges=6 trueHands=2");
        }
    }
}
