using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatDartChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private sealed class Birth
        {internal Projectile Shot,Parent;internal int Type,Pen,Max,Damage,Left,Extra;internal float Second;internal Vector2 Center,Velocity;}
        private static readonly List<Birth> births=new List<Birth>();
        private static Projectile parent,receiving;private static NPC target;private static bool watching,natural;
        private static long first;private static int solves,captures,pools,motors;private static AttackContact prepared;private static Vector2 raw;
        private static object ammo;private static Assembly assembly;private static Type model;private static int launcher;
        private static void SolveWork(){solves++;}
        private static void CaptureWork(){captures++;}
        private static void PoolWork(){pools++;}
        private static void MotorWork(){motors++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}
        private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {if(watching && ReferenceEquals(__instance,target) && receiving!=null && receiving.owner==Main.myPlayer && receiving.damage>0 && receiving.type>=267 && (receiving.type==267 || receiving.type>=477 && receiving.type<=480) && __result>0 && first<0)first=Main.GameUpdateCount;}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!watching || __result<0)return;var q=Main.projectile[__result];if(q.type!=267 && (q.type<477 || q.type>480))return;
            var source=__0 as EntitySource_Parent;var actualParent=source?.Entity as Projectile;
            if(!natural && !ReferenceEquals(actualParent,parent))return;
            var b=new Birth{Shot=q,Parent=actualParent,Type=q.type,Pen=q.penetrate,Max=q.maxPenetrate,Damage=q.damage,Left=q.timeLeft,Extra=q.extraUpdates,Second=q.ai[1],Center=q.Center,Velocity=q.velocity};births.Add(b);
            if(natural && actualParent==null)
            {
                var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);
                if(launcher==281 || launcher==986){origin.X+=6*p.direction;origin.Y-=6*p.gravDir;}if(launcher==3007){origin.X-=4*p.direction;origin.Y-=2*p.gravDir;}
                // Read the actual borrowed native consumer, not a previous
                // preview's future pose. Facing-dependent offsets can change
                // between idle and this real ItemCheck receipt.
                var cursor=new Vector2(Main.mouseX,Main.mouseY)+Main.screenPosition;var expected=(cursor-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed");
                while(expected.X>=16 || expected.X<=-16 || expected.Y>=16 || expected.Y< -16)expected*=.97f;
                int damage=(int)Get(ammo,"Damage");if(q.type==267){var selected=(Item)Get(ammo,"Ammo");damage=p.GetWeaponDamage(p.HeldItem)+(int)(selected.damage*p.GetWeaponDamageMultiplier(selected));}
                Console.WriteLine("DART actual consumer item="+launcher+" cursor="+cursor+" raw="+raw+" idleAim="+prepared.AimX+","+prepared.AimY+" direction="+p.direction+" origin="+origin);
                Require(q.owner==p.whoAmI && q.ranged && !q.magic && q.type==(int)Get(ammo,"Projectile") && q.damage==damage && Vector2.DistanceSquared(q.Center,origin)<.0001f && Vector2.DistanceSquared(q.velocity,expected)<.0001f && Vector2.Dot(q.velocity,raw-origin)<0,"actual dart hand birth uses assisted cursor, native offset/component limit and selected ammo damage without touching RNG");
            }
            Console.WriteLine("DART actual birth type="+b.Type+" slot="+q.whoAmI+" parent="+actualParent?.whoAmI+" center="+b.Center+" V="+b.Velocity+" pen/max="+b.Pen+"/"+b.Max+" damage="+b.Damage+" ai1="+b.Second+" left="+b.Left+" extra="+b.Extra);
        }
        private static Player Reset(object context,int item=3007)
        {
            watching=natural=false;parent=receiving=null;target=null;first=-1;births.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),item,0,0);Main.windPhysics=false;
            for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=32,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Timeline(NPC n)
        {
            var key=(NpcIdentity)assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});
            var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory timeline)
        {return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,timeline,0,true,false},null);}
        private static object Clock(NpcTrajectory timeline)
        {return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{timeline,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),0},null);}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostDartAttack",true);bool wind=Main.windPhysics;var random=Main.rand;var audit=new Harmony("JueMingR.Tests.Darts");
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDartChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDartChecks),nameof(MotorWork)));
            var obstacles=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true);audit.Patch(obstacles.GetMethod("CaptureChaseTargets",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDartChecks),nameof(CaptureWork)));audit.Patch(obstacles.GetMethod("CaptureBirthSlots",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDartChecks),nameof(PoolWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatDartChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatDartChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatDartChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatDartChecks),nameof(Strike)));
            try
            {
                Require((bool)CallStatic(model,"Handles",477) && (bool)CallStatic(model,"Handles",478) && (bool)CallStatic(model,"Handles",479) && !(bool)CallStatic(model,"Handles",480),"three dart hand openers preserve child480's separate observed role");
                string only=Environment.GetEnvironmentVariable("JMR_DART_ORACLE_ONLY");if(string.IsNullOrEmpty(only) || only=="motor")Motors(context);if(string.IsNullOrEmpty(only) || only=="birth")Births(context);if(string.IsNullOrEmpty(only) || only=="receive")Receives(context);if(string.IsNullOrEmpty(only) || only=="natural")Natural(context);
                Require(string.IsNullOrEmpty(only) || only=="motor" || only=="birth" || only=="receive" || only=="natural","known Dart leaf selector");
            }
            finally{watching=natural=false;parent=receiving=null;target=null;births.Clear();prepared=null;ammo=null;Main.windPhysics=wind;Main.rand=random;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static object CallStatic(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static void Compare(Projectile q,object s,string label)
        {
            Require(q.active==(bool)Get(s,"Alive") && q.timeLeft==(int)Get(s,"Left") && q.penetrate==(int)Get(s,"Penetration") && q.maxPenetrate==(int)Get(s,"MaximumPenetration") && q.damage==(int)Get(s,"Damage") && q.alpha==(int)Get(s,"Alpha") && q.extraUpdates==(int)Get(s,"Extra") && q.ai[0]==(float)Get(s,"Counter") && q.ai[1]==(float)Get(s,"Second") && q.localAI[0]==(float)Get(s,"Visual") && q.wet==(bool)Get(s,"Wet") && q.honeyWet==(bool)Get(s,"Honey") && q.shimmerWet==(bool)Get(s,"Shimmer") && Vector2.DistanceSquared(q.position,(Vector2)Get(s,"Position"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(s,"Velocity"))<.0001f,"native dart full motor/flags/clock agrees: "+label+" native="+q.position+"/"+q.velocity+" ai="+q.ai[0]+","+q.ai[1]+" pen/max="+q.penetrate+"/"+q.maxPenetrate+" left="+q.timeLeft+" alpha="+q.alpha+" model="+Get(s,"Position")+"/"+Get(s,"Velocity")+" ai="+Get(s,"Counter")+","+Get(s,"Second")+" pen/max="+Get(s,"Penetration")+"/"+Get(s,"MaximumPenetration")+" left="+Get(s,"Left")+" alpha="+Get(s,"Alpha"));
        }
        private static void Motors(object context)
        {
            int cases=0,actions=0;var foreign=Main.player[254];bool active=foreign.active;var stateType=model.GetNestedType("State",BindingFlags.NonPublic);
            try
            {
                foreach(int type in new[]{477,478,479,480})foreach(int scene in Enumerable.Range(0,10))
                {
                    if(scene>=8 && type!=477 && type!=480)continue;
                    var p=Reset(context);foreign.active=true;var n=Target(2,new Vector2(1100,350));n.dontTakeDamage=true;var timeline=Timeline(n);
                    if(scene>=1 && scene<=3)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                    if(scene>=8)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=128;Main.tile[x,y].liquidType(3);}
                    if(scene==4)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);if(scene==5){NativeToolsChecks.Tile(60,25,1);Main.tile[60,25].slope(1);}
                    var origin=scene==5?new Vector2(936,390):scene==6?new Vector2(10,350):new Vector2(950,350);var v=scene==4?new Vector2(24,0):scene==5?new Vector2(24,8):scene==6?new Vector2(-7,0):scene>=8?new Vector2(6,scene==8?2:-2):new Vector2(6,-.3f);
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,v,type,0,0,type==478?254:p.whoAmI,0,type==479?-1000:0);var q=Main.projectile[slot];if(scene==7)q.timeLeft=1;var s=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);var terrain=Terrain();var targets=assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true).GetMethod("Capture",Flags).Invoke(null,new object[]{q,1L});
                    for(int outer=1;outer<=35 && q.active;outer++)
                    {
                        int updates=q.extraUpdates+1;for(int sub=0;sub<updates;sub++){Require((bool)CallStatic(model,"Step",q,s,targets,timeline,Math.Min(outer,120),terrain,5),"finite dart scene dependencies remain available");actions++;if((bool)Get(s,"Stopped"))break;CallStatic(model,"Tail",q,s);}q.Update(slot);Compare(q,s,type+"/"+scene+"/"+outer);
                        if(type==480 && scene==4 && outer==1)Require(q.velocity==new Vector2(4,0) && q.position==new Vector2(948,344),"native Cursed child retains the wall-clipped positive velocity instead of AI014's ordinary negative half-speed bounce");
                    }
                    if(scene==3 && type!=480)Require(!q.active && q.timeLeft==0,"AI001 full shimmer column truly calls Kill");if(scene==6)Require(!q.active && q.timeLeft>0,"dart world retirement is distinct from Kill");cases++;
                }
            }
            finally{foreign.active=active;}
            Console.WriteLine("PASS DART motor="+cases+" actualActions="+actions+"; Cursed254 isolates motor without local random births, Ichor is actual already-split child");
        }
        private static void Births(object context)
        {
            var stateType=model.GetNestedType("State",BindingFlags.NonPublic);int events=0;
            foreach(int type in new[]{478,479})
            {
                var p=Reset(context);var n=Target(2,new Vector2(1100,350));n.dontTakeDamage=true;var timeline=Timeline(n);var terrain=Terrain();int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,6,0,type,50,4,p.whoAmI);parent=Main.projectile[slot];watching=true;
                var s=Activator.CreateInstance(stateType,Flags,null,new object[]{parent},null);var targets=assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true).GetMethod("Capture",Flags).Invoke(null,new object[]{parent,1L});
                // The prefix has no legal random motor event. These are actual
                // updates, not a favorable seeded future event schedule.
                for(int i=1;i<=5;i++){Require((bool)CallStatic(model,"Step",parent,s,targets,timeline,i,terrain,5),"dart deterministic pre-event prefix");CallStatic(model,"Tail",parent,s);parent.Update(slot);Compare(parent,s,"local prefix "+type+"/"+i);Require(births.Count==0,"no AI birth precedes native minimum sixth action");}
                int before=0,action=5;var beforeCenter=parent.Center;
                while(births.Count==0 && action<30){before=parent.timeLeft;beforeCenter=parent.Center;parent.Update(slot);action++;}
                Require(births.Count>0 && action>=6 && action<=(type==478?20:30),"native unseeded first AI birth lies in its complete legal interval");
                // Locked x86 evaluates damage * .9f before the integer cast:
                // 50 * the binary float .9f is below45 without an intervening
                // single-precision store. The actual child therefore has44.
                int childDamage=type==478?40:44;
                Require(births.All(b=>ReferenceEquals(b.Parent,parent) && b.Type==(type==478?480:479) && b.Pen==1 && b.Max==1 && b.Damage==childDamage && b.Center==beforeCenter && (type==478?b.Velocity==Vector2.Zero:b.Second==-1000)),"actual AI child retains causal parent, pre-movement origin, damage and birth modifier");
                Require((bool)CallStatic(model,"Step",parent,s,targets,timeline,6,terrain,5) && (int)Get(Get(s,"AiBirth"),"Damage")==childDamage,"legal minimum-threshold model birth uses the actual native child damage, independently of the observed random birth action");
                if(type==478)Require(births.Count==1 && parent.timeLeft==before-21 && parent.ai[1]==0,"Cursed actual event subtracts twenty before the ordinary lifetime tail");
                else{Require(births.Count>=2 && births.Count<=4 && parent.penetrate==-1 && parent.maxPenetrate==-1 && parent.ai[1]==-1000,"Ichor split keeps parent unlimited for the split Damage stage while every newborn child is one-hit");parent.Update(slot);Require(parent.penetrate==1 && parent.maxPenetrate==1,"only the next AI action transitions the Ichor parent to one hit");}
                Console.WriteLine("DART actual AI event type="+type+" action="+action+" children="+births.Count+" parentPen="+parent.penetrate);events++;
            }
            foreach(bool edge in new[]{false,true})
            {
                var p=Reset(context);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,0,0,478,50,4,p.whoAmI);parent=Main.projectile[slot];watching=true;
                if(edge){parent.position.X=Main.leftWorld;parent.Update(slot);Require(!parent.active && parent.timeLeft==300 && births.Count==0,"world retirement never invents the Cursed Kill child");}
                else{var center=parent.Center;parent.Kill();Require(births.Count==1 && ReferenceEquals(births[0].Parent,parent) && births[0].Type==480 && births[0].Center==center && births[0].Damage==40 && births[0].Velocity==Vector2.Zero,"actual Cursed Kill creates its distinct zero-velocity child at Kill-entry center");}events++;
            }
            watching=false;Console.WriteLine("PASS DART actual birth/death roles="+events+"; random choices observed without prediction or RNG modification");
        }
        private static void Receives(object context)
        {
            int cases=0;
            foreach(bool lower in new[]{false,true})
            {
                var p=Reset(context);target=Target(2,new Vector2(950,350),50,60);target.immune[p.whoAmI]=100;
                int filler=-1;if(lower)filler=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),1200,400,0,0,480,0,0,p.whoAmI);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,0,0,478,50,0,p.whoAmI);parent=Main.projectile[slot];parent.timeLeft=1;if(lower)Main.projectile[filler].active=false;
                NativeQuickItemChecks.BeginWorldStep();var timeline=Timeline(target);var terrain=Terrain();var obstacles=Obstacles(p,parent,timeline);var contact=(AttackContact)CallStatic(model,"KnownFamily",p,new[]{parent},timeline,Clock(timeline),terrain,obstacles);long sample=timeline.SampleTick;watching=true;
                for(int outer=1;outer<=3 && first<0;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);for(int i=0;i<Main.maxProjectiles;i++)if(Main.projectile[i].active){Main.ProjectileUpdateLoopIndex=i;Main.projectile[i].Update(i);}Main.ProjectileUpdateLoopIndex=-1;}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("DART Cursed Kill family lower="+lower+" native="+actual+" known="+contact?.Tick+" childSlots="+string.Join(",",births.Select(b=>b.Shot.whoAmI)));
                Require(births.Count==1 && ReferenceEquals(births[0].Parent,parent) && actual==(lower?2:1) && actual==contact?.Tick && target.immune[p.whoAmI]>90,"causal Cursed child bypasses owner immunity with its native one-hit policy and correct higher/lower birth-slot outer");cases++;
            }
            // One actual eligible alternative makes OnHit retarget deterministic;
            // the overlapping victim is itself a chaseable 50-distance body.
            {
                var p=Reset(context);target=Target(2,new Vector2(1080,440));var victim=Target(1,new Vector2(1000,350),220,40);var timeline=Timeline(target);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,9,0,477,50,0,p.whoAmI);var q=Main.projectile[slot];var s=Activator.CreateInstance(model.GetNestedType("State",BindingFlags.NonPublic),Flags,null,new object[]{q},null);var targets=assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true).GetMethod("Capture",Flags).Invoke(null,new object[]{q,1L});
                Require((bool)CallStatic(model,"OnHit",q,s,targets,timeline,1,Terrain(),victim.whoAmI,5),"Crystal dart actual victim exclusion has complete line/target dependencies");q.Damage();Require(victim.life<10000 && q.penetrate==4 && Vector2.DistanceSquared(q.velocity,(Vector2)Get(s,"Velocity"))<.0001f && q.damage==50,"Crystal native received OnHit excludes its victim and changes direction without the tile damage penalty");cases++;
            }
            foreach(int boundary in new[]{20,700,-1})
            {
                var p=Reset(context);target=Target(2,new Vector2(950+(boundary<0?150:boundary),350),1,1);var victim=Target(1,new Vector2(1000,350),220,40);if(boundary<0)for(int y=10;y<60;y++)NativeToolsChecks.Tile(64,y,1);
                var timeline=Timeline(target);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,9,0,477,50,0,p.whoAmI);var q=Main.projectile[slot];var s=Activator.CreateInstance(model.GetNestedType("State",BindingFlags.NonPublic),Flags,null,new object[]{q},null);var targets=assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true).GetMethod("Capture",Flags).Invoke(null,new object[]{q,1L});
                Require((bool)CallStatic(model,"OnHit",q,s,targets,timeline,1,Terrain(),1,5),"strict retarget distance/occlusion negative has complete dependencies");q.Damage();Require(victim.life<10000 && q.velocity==new Vector2(9,0) && (Vector2)Get(s,"Velocity")==q.velocity,"Crystal retarget rejects exact20, exact700, and a native blocked center-line: "+boundary);cases++;
            }
            {
                var p=Reset(context);var victim=Target(1,new Vector2(1000,350),220,40);var eligible=new List<Vector2>();for(int i=2;i<=10;i++)eligible.Add(Target(i,new Vector2(1100,320+i*10),18,18).Center);target=Target(12,new Vector2(1050,350),18,18);
                var timeline=Timeline(target);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,9,0,477,50,0,p.whoAmI);var q=Main.projectile[slot];var s=Activator.CreateInstance(model.GetNestedType("State",BindingFlags.NonPublic),Flags,null,new object[]{q},null);var targets=assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true).GetMethod("Capture",Flags).Invoke(null,new object[]{q,1L});
                Require((bool)CallStatic(model,"OnHit",q,s,targets,timeline,1,Terrain(),1,5),"first-nine random target prefix has complete dependencies");q.Damage();var native=q.velocity;var representative=(Vector2)Get(s,"Velocity");
                Require(victim.life<10000 && eligible.Any(point=>Vector2.DistanceSquared((point-q.Center).SafeNormalize(Vector2.Zero)*9,native)<.0001f) && eligible.Any(point=>Vector2.DistanceSquared((point-q.Center).SafeNormalize(Vector2.Zero)*9,representative)<.0001f) && (bool)Get(s,"Random"),"native and legal representative retarget stay inside the actual first-nine slot prefix; closer tenth selected body does not enter it");
                Require((bool)Get(targets,"Current"),"retarget lease does not confuse owner immunity with chase qualification");target.dontTakeDamage=true;Require(!(bool)Get(targets,"Current"),"changed future target qualification invalidates the captured retarget lease");cases++;
            }
            watching=false;Console.WriteLine("PASS DART ordered actual receivers="+cases);
        }
        private static void Natural(object context)
        {
            int cases=0;string only=Environment.GetEnvironmentVariable("JMR_DART_NATURAL_CASE");var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");
            foreach(int item in new[]{281,986,3007,3008})foreach(int ammoType in new[]{3009,3010,3011,1310})
            {
                if(ammoType==1310 && item!=3007)continue;string name=item+"/"+ammoType;if(!string.IsNullOrEmpty(only) && only!=name)continue;
                var p=Reset(context,item);launcher=item;p.position=new Vector2(650,646);p.inventory[54].SetDefaults(ammoType);p.inventory[54].stack=999;p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,43,40);raw=target.Center-new Vector2(390,0);solves=captures=pools=motors=0;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);
                prepared=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(prepared!=null && !Main.projectile.Any(q=>q.active),"idle dart preview retains a real reachable hand stage without firing");ammo=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true).GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});Require((int)Get(ammo,"Projectile")== (ammoType==1310?267:477+ammoType-3009),"each actual launcher/ammo resolves its native Dart type");
                int s=solves,c=captures,b=pools,m=motors;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int draw=0;draw<20;draw++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",prepared),"same-update Dart presentation uses the preparation lease");}
                Require(s==solves && c==captures && b==pools && m==motors && (ammoType==1310 || c==s && b==s && s>0),"Dart captures targets/slot lease once per solve and Draw performs zero solver/motor/capture work");
                long start=Main.GameUpdateCount;watching=natural=true;first=-1;births.Clear();NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));NativeCombatSelectionPreviewChecks.Step(context,raw,true);for(int i=0;i<10 && first<0;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
                Console.WriteLine("DART natural case="+name+" primaryBirths="+births.Count(born=>born.Parent==null)+" first="+(first<0?0:first-start)+" representative="+prepared.Tick+" solves="+solves+" captures="+captures+" pools="+pools+" motors="+motors);
                Require(births.Count(born=>born.Parent==null)==1 && first>=start && (prepared.Confidence!=AttackConfidence.Conditional || first-start==prepared.Tick),"actual near Dart hand birth and one-press/release produces positive Strike at its deterministic contact on the genuine immortal dummy");watching=natural=false;cases++;
            }
            Require(cases==(string.IsNullOrEmpty(only)?13:1),"all twelve resolved Dart pairs and the adjacent3007/267 offset case execute, or the explicit narrow case");Console.WriteLine("PASS DART natural="+cases+" actual birth/assisted consumer/Strike/presentation");
        }
    }
}
