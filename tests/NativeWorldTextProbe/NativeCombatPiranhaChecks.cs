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
    internal static class NativeCombatPiranhaChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static Type model;private static bool launchTrace;private static Vector2 firstOwner;private static Projectile firstBefore,firstAfter;private static NPC handTarget;private static int handStrikes;
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(handTarget,__instance) && __result>0)handStrikes++;}
        private static void BeforeAi(Projectile p,Player owner){if(launchTrace && firstBefore==null){firstOwner=owner.Center;firstBefore=Copy(p);}}
        private static void AfterAi(Projectile p){if(launchTrace && firstAfter==null)firstAfter=Copy(p);}
        private static Player Scene(object context)
        {var p=NativeCombatBallisticCandidateChecks.Scene(context,1156,50);foreach(var n in Main.npc)n.active=false;Main.windPhysics=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();for(int x=39;x<45;x++)NativeToolsChecks.Tile(x,43,1);p.channel=true;p.dead=false;p.velocity=Vector2.Zero;return p;}
        private static NPC Target(int slot,Vector2 center,bool blocked=false)
        {var n=Main.npc[slot];n.SetDefaults(3);n.active=true;n.whoAmI=slot;n.width=n.height=40;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.dontTakeDamage=blocked;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Path(NPC n)
        {var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Vx=n.velocity.X,Vy=n.velocity.Y,CanReceive=!n.dontTakeDamage,Active=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);}
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Capture(string method,Projectile p){return host.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true).GetMethod(method,Flags).Invoke(null,new object[]{p,1L});}
        private static Projectile Copy(Projectile p){return (Projectile)host.GetType("JueMingR.TerrariaHost.Combat.HostNoteAttack").GetMethod("Copy",Flags).Invoke(null,new object[]{p});}
        private static AttackContact Known(Player p,Projectile[] q,NPC n,out object root)
        {var path=Path(n);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q[0],path,0,false,true},null);using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,q,path,clock,Terrain(),root});}
        private static bool Move(Projectile p,object terrain,Vector2 beforeAi){var method=model.GetMethod("Move",Flags);return (bool)method.Invoke(null,method.GetParameters().Length==2?new object[]{p,terrain}:new object[]{p,terrain,beforeAi});}
        private static void UpdateAll(){for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;model=host.GetType("JueMingR.TerrariaHost.Combat.HostPiranhaAttack",true);int cases=0,actions=0;
            foreach(int phase in new[]{0,1,2,3,4})foreach(bool held in new[]{false,true})foreach(int scene in new[]{0,1,2,3}.Where(v=>string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JMR_PIRANHA_MOTOR_SCENE")) || v.ToString()==Environment.GetEnvironmentVariable("JMR_PIRANHA_MOTOR_SCENE")))
            {
                var p=Scene(context);p.channel=held;var attached=Target(1,new Vector2(900,450));attached.velocity=new Vector2(1,0);if(phase==3)attached.life=0;if(phase==4)attached.active=false;var n=Target(2,new Vector2(1050,450));var path=Path(n);
                if(scene==1 || scene==2)for(int x=25;x<105;x++)for(int y=10;y<75;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:2);}if(scene==3)for(int y=10;y<65;y++)NativeToolsChecks.Tile(53,y,1);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(800,450),new Vector2(7,1),190,0,0,p.whoAmI,phase==1?1:0,phase>=2?2:phase==1?-1:0);var actual=Main.projectile[slot];var shadow=Copy(actual);var bodies=Capture("CapturePiranhaBodies",actual);var chase=Capture("CaptureMovingChase",actual);var terrain=Terrain();
                for(int tick=1;tick<=24 && actual.active;tick++)
                {
                    var beforeAi=shadow.velocity;Require((bool)model.GetMethod("Ai",Flags).Invoke(null,new object[]{shadow,p,new[]{shadow},bodies,chase,path,Math.Min(tick,120),terrain}) && Move(shadow,terrain,beforeAi),"AI039 full concrete motor and LOS query");if(shadow.active && --shadow.timeLeft<=0){shadow.active=false;shadow.timeLeft=0;}NativeQuickItemChecks.BeginWorldStep();actual.Update(slot);
                    Require(actual.active==shadow.active && (!actual.active || Vector2.DistanceSquared(actual.position,shadow.position)<.002f && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.0001f && actual.ai[0]==shadow.ai[0] && actual.ai[1]==shadow.ai[1] && actual.tileCollide==shadow.tileCollide),"native190 flight/attachment/zero-life reacquire/channel return/wet/wall phase="+phase+" held="+held+" scene="+scene+" tick="+tick+" actual="+actual.position+"/"+actual.velocity+"/"+actual.ai[0]+","+actual.ai[1]+" model="+shadow.position+"/"+shadow.velocity+"/"+shadow.ai[0]+","+shadow.ai[1]);actions++;
                }
                cases++;
            }
            // Native separation reads other slots in the pool's current order,
            // including an earlier member's position already updated this pass.
            {
                var p=Scene(context);var n=Target(2,new Vector2(900,450));n.velocity=Vector2.UnitX;var path=Path(n);var real=new Projectile[3];for(int i=0;i<3;i++)real[i]=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(899,450),Vector2.Zero,190,0,0,p.whoAmI,1,3)];var shadow=Array.ConvertAll(real,Copy);var bodies=Capture("CapturePiranhaBodies",real[0]);var chase=Capture("CaptureMovingChase",real[0]);var terrain=Terrain();
                for(int tick=1;tick<=12;tick++)for(int i=0;i<3;i++){var beforeAi=shadow[i].velocity;Require((bool)model.GetMethod("Ai",Flags).Invoke(null,new object[]{shadow[i],p,shadow,bodies,chase,path,tick,terrain}) && Move(shadow[i],terrain,beforeAi),"complete same-owner190 ordered repulsion");real[i].Update(real[i].whoAmI);Require(Vector2.DistanceSquared(real[i].position,shadow[i].position)<.002f && Vector2.DistanceSquared(real[i].velocity,shadow[i].velocity)<.0001f,"native three-body repulsion retains ascending pool order and target velocity");}
            }
            foreach(bool blocked in new[]{false,true})
            {
                var p=Scene(context);var n=Target(2,new Vector2(900,450),blocked);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(880,450),new Vector2(7,0),190,50,0,p.whoAmI,1,3);var q=Main.projectile[slot];object root;var contact=Known(p,new[]{q},n,out root);Require((bool)Get(root,"Unchanged"),"captured piranha family/channel root starts current");p.channel=false;Require(!(bool)Get(root,"Unchanged") && !(bool)Get(root,"BirthInputsCurrent"),"same-GUC release revokes the real observed channel continuation");p.channel=true;n.life=0;Require(!(bool)Get(root,"Unchanged"),"same-GUC victim life==0 revokes native positive-life attachment");n.life=100000;int first=0;
                for(int tick=1;tick<=40 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();q.Update(slot);if(n.life<100000 && first==0)first=tick;}Require(blocked?contact==null && first==0:contact!=null && contact.Tick==first && first>0,"attached actual190 local14 receive and invulnerable negative");
            }
            foreach(int count in new[]{0,1,3})
            {
                var p=Scene(context);for(int i=0;i<count;i++)Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1100+i*50,350),Vector2.Zero,190,0,0,p.whoAmI);var old=Main.projectile.Where(q=>q.active).ToArray();Main.mouseX=(int)(p.MountedCenter.X+400-Main.screenPosition.X);Main.mouseY=(int)(p.MountedCenter.Y-Main.screenPosition.Y);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var born=Main.projectile.Where(q=>q.active && !old.Contains(q)).ToArray();Require(born.Length==3-count && born.All(q=>q.type==190 && Math.Abs(q.velocity.X-p.HeldItem.shootSpeed)<=2.001f && Math.Abs(q.velocity.Y)<=2.001f),"native1156 fills exactly3 owner bodies with each legal discrete +/-2 birth component");
            }
            {
                var p=NativeCombatBallisticCandidateChecks.Scene(context,1156,50);p.channel=true;var n=Main.npc[2];var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,50,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),n.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0);Require(GetOptional(attack,"ExpectedImpact")!=null && GetOptional(attack,"obstacles")!=null,"real Piranha preparation has complete family root and useful source");typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced" && Main.projectile.Count(q=>q.active && q.type==190)==3,"native1156 consumes actual prepared launch and creates all3 random bodies");
                // A real training dummy reports positive Strike damage while
                // retaining its health. Inspect that native result directly.
                var audit=new Harmony("JueMingR.Tests.PiranhaHand");handTarget=n;handStrikes=0;audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatPiranhaChecks),nameof(Strike)));
                try{for(int tick=1;tick<=120 && handStrikes==0;tick++){NativeQuickItemChecks.BeginWorldStep();UpdateAll();}Require(handStrikes>0,"actual assisted1156 random birth/attach family receives positive native Damage");}finally{handTarget=null;audit.UnpatchAll(audit.Id);}
            }
            LaunchFrame(context);Console.WriteLine("PIRANHA motorCases="+cases+" actions="+actions+" ordered3x12=36 receiveEdges=2 actualBirthCounts=3 trueEnhancedHand=1 launchFrames=2");
        }
        private static void LaunchFrame(object context)
        {
            var audit=new Harmony("JueMingR.Tests.PiranhaLaunchFrame");audit.Patch(model.GetMethod("Ai",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPiranhaChecks),nameof(BeforeAi)),postfix:new HarmonyMethod(typeof(NativeCombatPiranhaChecks),nameof(AfterAi)));
            try
            {
                foreach(int delay in new[]{0,5})
                {
                    var live=Scene(context);var n=Target(2,new Vector2(950,666));var path=Path(n);var future=(Player)typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(live,null);future.position.X+=3*(delay+1);future.velocity=new Vector2(3,0);future.controlRight=true;
                    var origin=future.RotatedRelativePoint(future.MountedCenter);var terrain=Terrain();var frame=host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame").GetMethod("Capture",Flags).Invoke(null,new object[]{future,path});var ammo=host.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot").GetMethod("Capture",Flags).Invoke(null,new object[]{future,future.HeldItem});var root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{future,Terraria.ID.ContentSamples.ProjectilesByType[190],path,delay,false,true},null);
                    firstBefore=firstAfter=null;launchTrace=true;try{var method=model.GetMethod("Solve",Flags);var args=method.GetParameters().Length==9?new object[]{future,ammo,path,origin,delay,false,terrain,root,delay}:new object[]{future,ammo,path,origin,delay,false,terrain,root,delay,frame,true};using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))method.Invoke(null,args);}finally{launchTrace=false;}
                    Require(firstBefore!=null && firstAfter!=null && firstOwner==future.Center,"Piranha receives the already advanced launch owner exactly once, including delayed CompletedWorld delay="+delay+" expected="+future.Center+" actual="+firstOwner);
                    var position=live.position;var velocity=live.velocity;bool channel=live.channel;live.position=future.position;live.velocity=future.velocity;live.channel=true;
                    try{var actual=Main.projectile[Projectile.NewProjectile(live.GetProjectileSource_Item(live.HeldItem),firstBefore.Center,firstBefore.velocity,190,firstBefore.damage,0,live.whoAmI,firstBefore.ai[0],firstBefore.ai[1])];actual.alpha=firstBefore.alpha;actual.AI();Require(actual.position==firstAfter.position && actual.velocity==firstAfter.velocity && actual.ai[0]==firstAfter.ai[0] && actual.ai[1]==firstAfter.ai[1],"the same sealed launch body at that exact future owner matches original AI039 first action");}
                    finally{live.position=position;live.velocity=velocity;live.channel=channel;}
                }
            }
            finally{launchTrace=false;audit.UnpatchAll(audit.Id);}
        }
    }
}
