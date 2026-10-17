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
    internal static class NativeCombatNorthPoleChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static Type model;private static NPC target;private static int hits;
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,target) && __result>0)hits++;}
        private static Player Scene(object context)
        {
            var p=NativeCombatBallisticCandidateChecks.Scene(context,1947,50);foreach(var npc in Main.npc)npc.active=false;Main.windPhysics=false;
            for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();for(int x=39;x<45;x++)NativeToolsChecks.Tile(x,43,1);
            p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.direction=1;p.velocity=Vector2.Zero;return p;
        }
        private static NpcTrajectory Path(NPC n)
        {
            var identity=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,Active=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);
        }
        private static NPC Target(Vector2 center,bool blocked=false)
        {var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.width=80;n.height=80;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.noTileCollide=n.noGravity=true;n.aiStyle=-1;n.dontTakeDamage=blocked;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static AttackContact Known(Player p,Projectile shot,NPC n)
        {
            var path=Path(n);var terrain=Terrain();var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,shot,path,0,false,true},null);
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{shot},path,clock,terrain,root});
        }
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;model=host.GetType("JueMingR.TerrariaHost.Combat.HostNorthPoleAttack",true);int cases=0,actions=0;
            foreach(int type in new[]{343,344})foreach(int environment in new[]{0,1,2,3})foreach(float vy in new[]{-2f,3f})
            {
                var p=Scene(context);
                if(environment==1 || environment==2)for(int x=25;x<110;x++)for(int y=10;y<75;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(environment==1?0:2);}
                if(environment==3)for(int x=20;x<110;x++)NativeToolsChecks.Tile(x,35,1);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,480),new Vector2(3,vy),type,0,0,p.whoAmI);var native=Main.projectile[slot];native.localAI[1]=type==343?-1:0;native.ai[1]=type==344?2:0;
                var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack").GetMethod("Copy",Flags).Invoke(null,new object[]{native});var terrain=Terrain();var scene=new System.Collections.Generic.List<Projectile>{shadow};
                for(int outer=0;outer<60 && native.active;outer++)
                {
                    object[] args={shadow,scene,terrain,false,false};Require((bool)model.GetMethod("ChildStep",Flags).Invoke(null,args),"North Pole concrete motor terrain");if(shadow.active && --shadow.timeLeft<=0)shadow.active=false;actions++;
                    int before=Main.projectile.Count(q=>q.active && q.type==344);NativeQuickItemChecks.BeginWorldStep();native.Update(slot);int after=Main.projectile.Count(q=>q.active && q.type==344);
                    Require(native.active==shadow.active && (!native.active || Vector2.DistanceSquared(native.position,shadow.position)<.0001f && Vector2.DistanceSquared(native.velocity,shadow.velocity)<.000001f && native.ai[0]==shadow.ai[0] && native.localAI[1]==shadow.localAI[1] && native.timeLeft==shadow.timeLeft),"North Pole exact AI057/AI001 type="+type+" environment="+environment+" outer="+outer+" native="+native.position+"/"+native.velocity+" ai="+native.ai[0]+" local="+native.localAI[1]+" shadow="+shadow.position+"/"+shadow.velocity+" ai="+shadow.ai[0]+" local="+shadow.localAI[1]);
                    if(type==343)Require(after-before==((bool)args[3]?1:0),"sealed -1 clock emits real344 at each eight actions");
                }
                cases++;
            }
            // Repulsion includes a foreign owner and the +/-60px boundary.
            foreach(float dx in new[]{-60f,-30f,0f,30f,60f,61f})
            {
                var p=Scene(context);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,480),new Vector2(0,1),344,0,0,p.whoAmI);var native=Main.projectile[slot];native.localAI[1]=24;
                int otherSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),native.Center+new Vector2(dx,0),Vector2.Zero,344,0,0,1);var other=Main.projectile[otherSlot];
                var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack").GetMethod("Copy",Flags).Invoke(null,new object[]{native});var scene=new System.Collections.Generic.List<Projectile>{shadow,other};object[] args={shadow,scene,Terrain(),false,false};Require((bool)model.GetMethod("ChildStep",Flags).Invoke(null,args),"repulsion step");NativeQuickItemChecks.BeginWorldStep();native.Update(slot);
                Require(native.position==shadow.position && native.velocity==shadow.velocity,"foreign owner repulsion exact dx="+dx);
            }
            {
                var p=Scene(context);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,480),Vector2.Zero,344,0,0,1);var native=Main.projectile[slot];Main.player[1].active=false;
                var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack").GetMethod("Copy",Flags).Invoke(null,new object[]{native});object[] args={shadow,new System.Collections.Generic.List<Projectile>{shadow},Terrain(),false,false};Require((bool)model.GetMethod("ChildStep",Flags).Invoke(null,args),"inactive foreign owner motor");NativeQuickItemChecks.BeginWorldStep();native.Update(slot);Require(!native.active && !shadow.active,"native owner retirement removes a foreign snowflake before later-slot repulsion");
            }
            foreach(int type in new[]{343,344})foreach(bool blocked in new[]{false,true})
            {
                var p=Scene(context);var n=Target(type==343?new Vector2(900,400):new Vector2(700,520),blocked);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),type==343?new Vector2(700,400):new Vector2(700,400),type==343?new Vector2(8,0):Vector2.Zero,type,40,0,p.whoAmI);var q=Main.projectile[slot];q.localAI[1]=type==343?-1:0;var contact=Known(p,q,n);int first=0;
                for(int tick=1;tick<=120 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();if(n.immune[p.whoAmI]>0)n.immune[p.whoAmI]--;q.Update(slot);if(n.life<100000){first=tick;break;}}
                Require(blocked?contact==null && first==0:contact!=null && contact.Tick==first && first>0,"observed343/344 first actual Strike type="+type+" expected="+contact?.Tick+" native="+first);
            }
            foreach(bool lowSlot in new[]{false,true})
            {
                var p=Scene(context);var n=Target(new Vector2(742,570));n.width=n.height=44;n.Center=new Vector2(742,570);
                if(lowSlot)for(int i=0;i<10;i++){int filler=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1500,300),Vector2.Zero,1,0,0,p.whoAmI);Main.projectile[filler].timeLeft=1000;}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,400),new Vector2(6,0),343,40,0,p.whoAmI);var q=Main.projectile[slot];q.localAI[1]=-1;if(lowSlot)Main.projectile[0].active=false;
                var path=Path(n);var terrain=Terrain();var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,true},null);AttackContact contact;
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{q},path,clock,terrain,root});
                Require(contact!=null && (bool)Get(root,"Unchanged"),"captured343 predicts finite emitted344 receiving beyond its own flight");q.localAI[0]++;Require(!(bool)Get(root,"Unchanged"),"same-GUC emitter local clock retires actual lease");q.localAI[0]--;
                int first=0;for(int tick=1;tick<=120 && first==0;tick++){NativeQuickItemChecks.BeginWorldStep();if(n.immune[p.whoAmI]>0)n.immune[p.whoAmI]--;for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;try{Main.projectile[i].Update(i);}finally{Main.ProjectileUpdateLoopIndex=-1;}}if(n.life<100000)first=tick;}
                Require(first==contact.Tick,"real343 periodic snowflake first Strike preserves high/lower birth slot phase expected="+contact.Tick+" native="+first+" low="+lowSlot);Console.WriteLine("NORTH-POLE snowflake lowSlot="+lowSlot+" first="+first);
            }
            var audit=new Harmony("JueMingR.Tests.NorthPole");audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatNorthPoleChecks),nameof(Strike)));
            try
            {
                var p=Scene(context);target=Target(new Vector2(980,660));hits=0;var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,50,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,target,0);
                Require(GetOptional(attack,"ExpectedImpact")!=null,"North Pole actual finite hand preparation");var root=GetOptional(attack,"obstacles");Require(root!=null && (bool)Get(root,"Unchanged"),"North Pole public returned root current");
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","North Pole native hand consumes Enhanced");
                bool born=false;for(int tick=1;tick<=120 && hits==0;tick++)
                {
                    NativeQuickItemChecks.BeginWorldStep();if(tick>1)p.itemAnimation=Math.Max(0,p.itemAnimation-1);for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active){Main.ProjectileUpdateLoopIndex=index;try{Main.projectile[index].Update(index);}finally{Main.ProjectileUpdateLoopIndex=-1;}}
                    if(Main.projectile.Any(q=>q.active && q.type==343))born=true;
                }
                Require(born && hits>0,"late actual spear343 family gives useful original Damage beyond342 body");Console.WriteLine("NORTH-POLE actualHand strikes="+hits);
            }
            finally{audit.UnpatchAll(audit.Id);target=null;}
            Console.WriteLine("NORTH-POLE motorCases="+cases+" actions="+actions+" foreignRepulsion=6 observedReceive=4 actualHand=1");
        }
    }
}
