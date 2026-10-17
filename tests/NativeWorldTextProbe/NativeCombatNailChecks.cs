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
    internal static class NativeCombatNailChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static Type model;private static NPC target;private static int hits;
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,target) && __result>0)hits++;}
        private static Player Scene(object context)
        {var p=NativeCombatBallisticCandidateChecks.Scene(context,3107,50);p.inventory[54].SetDefaults(3108);p.inventory[54].stack=999;foreach(var n in Main.npc)n.active=false;Main.windPhysics=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();return p;}
        private static NPC Target(int slot,Vector2 center,bool blocked=false)
        {var n=Main.npc[slot];n.SetDefaults(3);n.active=true;n.whoAmI=slot;n.width=n.height=40;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.dontTakeDamage=blocked;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static AttackContact Known(Player p,Projectile q,NPC n,out object root)
        {
            var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,Active=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var path=new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);var terrain=Terrain();root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,true},null);using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{q},path,clock,terrain,root});
        }
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;model=host.GetType("JueMingR.TerrariaHost.Combat.HostNailAttack",true);int cases=0,actions=0;
            foreach(int phase in new[]{0,1,-2})foreach(int scene in new[]{0,1,2,3})
            {
                var p=Scene(context);var attached=Target(1,new Vector2(850,450),true);if(scene==1 || scene==2)for(int x=25;x<105;x++)for(int y=10;y<75;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:2);}if(scene==3)for(int y=10;y<65;y++)NativeToolsChecks.Tile(50,y,1);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,450),new Vector2(7,-1),514,0,0,p.whoAmI);var actual=Main.projectile[slot];actual.ai[0]=phase;actual.ai[1]=phase==0?18:80;var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("Copy",Flags).Invoke(null,new object[]{actual});var terrain=Terrain();
                for(int action=0;action<30 && actual.active;action++)
                {
                    MotionRect? body=phase<0?new MotionRect(attached.position.X,attached.position.Y,attached.width,attached.height):(MotionRect?)null;var args=new object[]{shadow,terrain,body,false,Vector2.Zero,false};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"AI093 concrete motor/attachment");if((bool)args[3]){shadow.active=false;shadow.timeLeft=0;}else if(shadow.active && --shadow.timeLeft<=0)shadow.active=false;
                    NativeQuickItemChecks.BeginWorldStep();actual.Update(slot);Require(actual.active==shadow.active && (!actual.active || Vector2.DistanceSquared(actual.position,shadow.position)<.001f && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.0001f && actual.ai[0]==shadow.ai[0] && actual.ai[1]==shadow.ai[1] && actual.timeLeft==shadow.timeLeft),"AI093 native pregravity/4px tile-stick/water/attachment clock phase="+phase+" scene="+scene+" action="+action+" actual="+actual.position+"/"+actual.velocity+"/"+actual.ai[0]+","+actual.ai[1]+" model="+shadow.position+"/"+shadow.velocity+"/"+shadow.ai[0]+","+shadow.ai[1]);actions++;
                }
                cases++;
            }
            foreach(int phase in new[]{0,1,-2})foreach(bool inside in new[]{true,false})
            {
                var p=Scene(context);var n=Target(2,new Vector2(inside?920:1000,450));var attached=Target(1,new Vector2(870,450),true);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(850,450),Vector2.Zero,514,100,0,p.whoAmI);var q=Main.projectile[slot];q.ai[0]=phase;q.ai[1]=phase==0?0:88;if(phase==0)q.timeLeft=1;object root;var contact=Known(p,q,n,out root);if(phase<0){Require((bool)Get(root,"Unchanged"),"captured nail lifecycle premise starts current");attached.life=-1;Require(!(bool)Get(root,"Unchanged"),"same-GUC attached victim life threshold invalidates nail lifecycle lease");attached.life=100000;}int first=0;
                for(int tick=1;tick<=5 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();q.Update(slot);if(n.life<100000){first=tick;break;}}
                Require(inside?contact!=null && contact.Tick==first && first>0:contact==null && first==0,"all AI093 Kill paths retain exact112px native burst with an outside negative phase="+phase+" first="+first+" plan="+contact?.Tick);

            }
            var audit=new Harmony("JueMingR.Tests.NailInput");audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatNailChecks),nameof(Strike)));
            try
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,3107,50);p.inventory[54].SetDefaults(3108);p.inventory[54].stack=999;target=Main.npc[2];hits=0;var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,50,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,target,0);Require(GetOptional(attack,"ExpectedImpact")!=null,"nail family prepares the real entry/attached burst source");typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","original nail gun Shoot consumes the prepared source");
                for(int tick=1;tick<=120 && hits==0;tick++){NativeQuickItemChecks.BeginWorldStep();for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active)Main.projectile[index].Update(index);}Require(hits>0,"original ammo birth and AI093 receive useful Damage");
            }
            finally{audit.UnpatchAll(audit.Id);target=null;}
            Console.WriteLine("NAIL motorCases="+cases+" actions="+actions+" burstEdges=6 trueHand=1");
        }
    }
}
