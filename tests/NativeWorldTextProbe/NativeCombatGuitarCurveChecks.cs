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
    internal static class NativeCombatGuitarCurveChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile receiving;private static int first,step;
        private static bool traceFirst;private static Projectile traced,tracedBefore;private static Vector2 tracedOwner,tracedCenter;
        private static void CurveBefore(Projectile __0,Vector2 __1){if(traceFirst && __0.localAI[0]==0){traceFirst=false;traced=__0;tracedOwner=__1;tracedBefore=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostSuperStarAttack",true),"Copy",__0);}}
        private static void CurveAfter(Projectile __0){if(ReferenceEquals(traced,__0) && __0.localAI[0]==1)tracedCenter=__0.Center;}
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void Damage(Projectile __instance){receiving=__instance;}private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==2 && receiving?.type==856 && first==0)first=step;}
        private static Player Reset(object context)
        {NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),4715,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;Main.windPhysics=false;foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();for(int x=35;x<115;x++)NativeToolsChecks.Tile(x,43,1);first=step=0;return p;}
        private static NPC Target(Vector2 center)
        {var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Vx=n.velocity.X,Vy=n.velocity.Y,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static AttackContact Known(Player p,Projectile q,NPC n,out object root)
        {var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);return (AttackContact)Static(model,"KnownFamily",p,new[]{q},path,clock,Terrain(),root);}
        private static Projectile Shoot(Player p,Vector2 cursor)
        {Main.mouseX=(int)(cursor.X-Main.screenPosition.X);Main.mouseY=(int)(cursor.Y-Main.screenPosition.Y);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});return Main.projectile.Single(q=>q.active && q.type==856);}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostGuitarCurveAttack",true);var audit=new Harmony("JueMingR.Tests.GuitarCurve");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatGuitarCurveChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatGuitarCurveChecks),nameof(DamageDone)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatGuitarCurveChecks),nameof(Strike)));
            audit.Patch(model.GetMethod("Ai",Flags),prefix:new HarmonyMethod(typeof(NativeCombatGuitarCurveChecks),nameof(CurveBefore)),postfix:new HarmonyMethod(typeof(NativeCombatGuitarCurveChecks),nameof(CurveAfter)));
            try{Motor(context);Births(context);Receive(context);Terminal(context);LaunchFrame(context);Console.WriteLine("PASS GUITAR CURVE true4715 birth supports, AI167 owner curve and exact original receive.");}finally{traceFirst=false;traced=tracedBefore=null;Main.windPhysics=wind;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);}
        }
        private static void Motor(object context)
        {
            foreach(int moving in new[]{0,1,2})
            {
                var p=Reset(context);var original=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.Center,new Vector2(230,-420),856,50,0,p.whoAmI,1300,550)];var copy=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",original);
                for(int action=0;action<89;action++)
                {
                    if(moving==1)p.position.X+=3;if(moving==2)p.position.Y+=(float)Math.Sin(action*.3f)*2;Main.mouseX=0;Main.mouseY=0;
                    Static(model,"Ai",copy,p.Center);original.AI();if(original.active!=copy.active || original.localAI[0]!=copy.localAI[0] || Vector2.Distance(original.position,copy.position)>=.0001f || original.velocity!=copy.velocity || original.rotation!=copy.rotation)Console.WriteLine("GUITAR diff moving="+moving+" action="+action+" active="+original.active+"/"+copy.active+" pos="+original.position+"/"+copy.position+" velocity="+original.velocity+"/"+copy.velocity+" rotation="+original.rotation+"/"+copy.rotation+" ai="+original.localAI[0]+"/"+copy.localAI[0]);Require(original.active==copy.active && original.localAI[0]==copy.localAI[0] && Vector2.Distance(original.position,copy.position)<.0001f && original.velocity==copy.velocity && original.rotation==copy.rotation,"original85690-clock CatmullRom uses real moving owner center and sealed original velocity/endpoint each action");
                }
                Require(!original.active && original.localAI[0]==89,"original89th AI action kills before its next Damage");
            }
            Console.WriteLine("PASS GUITAR original3 owner-motion curves x89 actions / opposite mouse / exact retirement");
        }
        private static void Births(object context)
        {
            foreach(bool automatic in new[]{false,true})for(int draw=0;draw<8;draw++)
            {
                var p=Reset(context);var n=Target(new Vector2(1200,600));n.velocity=new Vector2(2,-1);if(!automatic)n.active=false;var cursor=new Vector2(1750,667);var q=Shoot(p,cursor);var end=new Vector2(q.ai[0],q.ai[1]);var target=automatic?n.Center+n.velocity*20:p.Center+(cursor-p.Center).SafeNormalize(Vector2.UnitX)*700;
                Console.WriteLine("GUITAR BIRTH automatic="+automatic+" draw="+draw+" end="+end+" expected="+target+" center="+p.Center+" velocity="+q.velocity+" speed="+q.velocity.Length()+" distance="+(target-p.Center).Length()+" yRatio="+Math.Abs(q.velocity.Y)/q.velocity.Length()+" rotation="+p.fullRotation);
                // Player clamps edge.Y before scaling, without normalizing again.
                // Its native floor is distance on the Y component, not .49 of
                // the resulting (possibly longer) vector's length.
                float distance=(target-p.Center).Length();
                Require(Vector2.Distance(end,target)<.002f && q.velocity.Y<=0 && Math.Abs(q.velocity.Y)>=distance-.01f && Math.Abs(q.velocity.Y)<=distance*2+.01f && Math.Abs(q.velocity.X)<=distance*2+.01f && q.velocity.Length()>=distance*2-.01f && q.velocity.Length()<=distance*2*(float)Math.Sqrt(2)+.01f,"actual4715 one-shot random target/700-clamp and unnormalized upper edge-circle component bounds stay inside the native birth support");
            }
            Console.WriteLine("PASS GUITAR original16 actual Shoot births: current1000x800 target or physical700 clamp");
        }
        private static void Receive(object context)
        {
            var p=Reset(context);var n=Target(new Vector2(1200,667));var q=Shoot(p,new Vector2(500,667));object root;var expected=Known(p,q,n,out root);Require(expected!=null,"already born genuine random856 has a useful sealed native receive");Main.mouseX=0;Main.mouseY=0;
            for(step=1;step<=40 && first==0;step++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);q.Update(q.whoAmI);}Require(first==expected.Tick,"original3subupdates/89-action kill/local history match actual first Damage after physical cursor reversal");Console.WriteLine("GUITAR actual born predicted="+expected.Tick+" nativeFirst="+first);
            p=Reset(context);n=Target(new Vector2(1200,667));q=Shoot(p,new Vector2(500,667));Known(p,q,n,out root);q.ai[0]++;Require(!(bool)Get(root,"Unchanged"),"same-GUC sealed endpoint edit revokes source dependency");
        }
        private static void Terminal(object context)
        {
            var p=Reset(context);var n=Target(new Vector2(1165,350));var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1100,350),Vector2.Zero,856,50,0,p.whoAmI,1300,350)];q.localAI[0]=88;object root;var expected=Known(p,q,n,out root);Require(expected!=null && expected.Tick==1,"native89th-action Kill has a useful128px burst even when the old32px body cannot reach");step=1;NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);q.Update(q.whoAmI);Require(first==1 && q.width==128 && !q.active && n.life<10000,"original Kill synchronously resizes the same856 and performs real Damage before retirement");Console.WriteLine("PASS GUITAR original terminal128px Damage predicted1=native1");
        }
        private static void LaunchFrame(object context)
        {
            foreach(int delay in new[]{0,5})
            {
                var p=Reset(context);p.velocity=new Vector2(3,0);p.controlRight=true;var future=(Player)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);future.position.X+=3*(delay+1);var n=Target(future.Center+new Vector2(300,0));var path=Timeline(n);var frame=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true),"Capture",future,path);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{future,Terraria.ID.ContentSamples.ProjectilesByType[856],path,delay,false,true},null);traced=tracedBefore=null;traceFirst=true;AttackContact contact;
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)Static(model,"Solve",future,ammo,path,future.RotatedRelativePoint(future.MountedCenter),delay+1,false,Terrain(),root,delay,frame,true);
                Require(contact!=null && tracedBefore!=null && tracedOwner==future.Center,"a supplied future moving-owner launch frame is consumed exactly once, including CompletedWorld and cooldown: "+delay);
                var position=p.position;p.position=future.position;var native=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),tracedBefore.Center,tracedBefore.velocity,856,tracedBefore.damage,0,p.whoAmI,tracedBefore.ai[0],tracedBefore.ai[1])];native.AI();Require(Vector2.Distance(native.Center,tracedCenter)<.001f,"the actual original AI167 at that future owner center matches the prepared first curve action, without a second delay/first-world advance");p.position=position;
            }
            Console.WriteLine("PASS GUITAR moving supplied launch frame delay0/5 +original first AI167; no double owner advancement");
        }
    }
}


