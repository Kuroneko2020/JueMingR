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
    internal static class NativeCombatInfluxChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static Type model;private static NPC target;private static int hits;
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,target) && __result>0)hits++;}
        private static Player Scene(object context)
        {var p=NativeCombatBallisticCandidateChecks.Scene(context,2880,50);foreach(var n in Main.npc)n.active=false;Main.windPhysics=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();return p;}
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static NpcTrajectory Path(NPC n)
        {var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var s=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,Active=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,s)).ToArray();return new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);}
        private static NPC Target(bool blocked)
        {var n=Main.npc[2];n.SetDefaults(3);n.active=true;n.whoAmI=2;n.width=n.height=80;n.Center=new Vector2(950,450);n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.noGravity=n.noTileCollide=true;n.aiStyle=-1;n.dontTakeDamage=blocked;Array.Clear(n.immune,0,n.immune.Length);return n;}
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;model=host.GetType("JueMingR.TerrariaHost.Combat.HostInfluxAttack",true);var targetType=host.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true);int cases=0,actions=0;
            foreach(int phase in new[]{-1,0,1,3,6})foreach(int scene in new[]{0,1,2,3})
            {
                var p=Scene(context);var n=Target(true);n.active=false;if(scene==1 || scene==2)for(int x=25;x<105;x++)for(int y=10;y<75;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:2);}if(scene==3)for(int y=10;y<65;y++)NativeToolsChecks.Tile(50,y,1);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(700,450),new Vector2(11,-1),451,0,0,p.whoAmI);var actual=Main.projectile[slot];actual.ai[0]=phase;actual.alpha=phase==6?0:75;var shadow=(Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostBetsyFlameAttack",true).GetMethod("Copy",Flags).Invoke(null,new object[]{actual});var targets=targetType.GetMethod("CaptureMovingChase",Flags).Invoke(null,new object[]{actual,1L});var path=Path(n);var terrain=Terrain();
                for(int action=0;action<40 && actual.active;action++)
                {
                    var args=new object[]{shadow,targets,path,Math.Min(action+1,120),terrain,false};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"AI081 exact concrete captured motor");if(shadow.active && --shadow.timeLeft<=0)shadow.active=false;NativeQuickItemChecks.BeginWorldStep();actual.Update(slot);
                    Require(actual.active==shadow.active && (!actual.active || Vector2.DistanceSquared(actual.position,shadow.position)<.001f && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.0001f && actual.ai[0]==shadow.ai[0] && actual.ai[1]==shadow.ai[1] && actual.localAI[0]==shadow.localAI[0] && actual.localAI[1]==shadow.localAI[1] && actual.scale==shadow.scale && actual.alpha==shadow.alpha && actual.timeLeft==shadow.timeLeft),"AI081 native scale/fade/11speed/tile state phase="+phase+" scene="+scene+" action="+action+" actual="+actual.position+"/"+actual.velocity+"/"+actual.scale+" model="+shadow.position+"/"+shadow.velocity+"/"+shadow.scale);actions++;
                }cases++;
            }
            foreach(bool blocked in new[]{false,true})
            {
                var p=Scene(context);var n=Target(blocked);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900,450),new Vector2(4,0),451,50,0,p.whoAmI);var q=Main.projectile[slot];q.ai[0]=3;q.alpha=250;var path=Path(n);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);var terrain=Terrain();var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,true},null);AttackContact contact;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{q},path,clock,terrain,root});int first=0;
                for(int tick=1;tick<=40 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();q.Update(slot);if(n.life<100000){first=tick;break;}}
                Require(blocked?contact==null && first==0:contact!=null && contact.Confidence==AttackConfidence.Representative && first>0,"observed native teleport phase has real future Receive and an invulnerable negative");Require(q.penetrate==3 && n.immune[p.whoAmI]==0,"AI081 OnHit retains penetrate3 and bypasses ordinary immunity tail");
            }
            var audit=new Harmony("JueMingR.Tests.InfluxInput");audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatInfluxChecks),nameof(Strike)));
            try
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,2880,50);target=Main.npc[2];hits=0;var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,50,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,target,0);Require(GetOptional(attack,"ExpectedImpact")!=null,"Influx original -1 launch has a useful family preparation");typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","Influx original sword projectile consumes prepared input");for(int tick=1;tick<=120 && hits==0;tick++){NativeQuickItemChecks.BeginWorldStep();for(int index=0;index<Main.maxProjectiles;index++)if(Main.projectile[index].active)Main.projectile[index].Update(index);}Require(hits>0,"Influx actual native sword launch retains useful Damage");
            }
            finally{audit.UnpatchAll(audit.Id);target=null;}
            Console.WriteLine("INFLUX motorCases="+cases+" actions="+actions+" teleportReceive=2 actualHand=1");
        }
    }
}
