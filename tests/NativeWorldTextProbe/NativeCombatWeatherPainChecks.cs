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
    internal static class NativeCombatWeatherPainChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile receiving;private static int step,first;private static long firstGuc;
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void Damage(Projectile __instance){receiving=__instance;}private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==2 && receiving?.type==969 && first==0){first=step;firstGuc=Main.GameUpdateCount;}}
        private static void RequireClock(AttackContact contact)
        {Require(firstGuc>=0 && firstGuc-contact.Timeline.SampleTick==contact.Tick && firstGuc-contact.Timeline.SampleTick==contact.ReceiveTick,"Weather actual Strike GUC agrees with the dedicated timeline receive index");Console.WriteLine("WEATHER absolute sample="+contact.Timeline.SampleTick+" strike="+firstGuc+" receive="+contact.ReceiveTick);}
        private static Player Reset(object context)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),5118,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;Main.windPhysics=false;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();first=step=0;firstGuc=-1;return p;
        }
        private static NPC Target(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Terrain(){var value=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(value,"Reset");return value;}
        private static object Root(Player p,Projectile source,NpcTrajectory path)
        {return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,source,path,0,true,false},null);}
        private static AttackContact Known(Player p,Projectile[] sources,NPC n,out object root)
        {var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);root=Root(p,sources[0],path);return (AttackContact)Static(model,"KnownFamily",p,sources,path,clock,Terrain(),root);}
        private static Projectile Spawn(Player p,Vector2 center,Vector2 velocity)
        {return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,969,30,0,p.whoAmI)];}
        private static void Advance(int limit)
        {
            // This BeforeNpc root includes the current GUC; only later worlds advance it.
            for(step=1;step<=limit && first==0;step++){if(step>1)NativeQuickItemChecks.BeginWorldStep();foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostWeatherPainAttack",true);var audit=new Harmony("JueMingR.Tests.WeatherPain");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatWeatherPainChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatWeatherPainChecks),nameof(DamageDone)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatWeatherPainChecks),nameof(Strike)));
            try{Motor(context);Retirement(context);Receive(context);Hand(context);Console.WriteLine("PASS WEATHER PAIN native AI177/400LOS, slot retirement, ID-static receive and actual Shoot.");}finally{Main.windPhysics=wind;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);}
        }
        private static void Motor(object context)
        {
            foreach(int mode in new[]{0,1,2,3})
            {
                var p=Reset(context);var n=Target(2,new Vector2(1170,350));var original=Spawn(p,new Vector2(900,350),new Vector2(3,0));if(mode==1){for(int y=10;y<50;y++)NativeToolsChecks.Tile(65,y,1);}if(mode==2)original.localNPCImmunity[2]=60;if(mode==3){n.active=false;original.ai[1]=552;}
                var copy=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",original);var path=Timeline(n);var terrain=Terrain();var targets=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true),"Capture",copy,1L);
                for(int action=0;action<35 && original.active;action++)
                {
                    Require((bool)Static(model,"Step",copy,new[]{copy},targets,path,0,terrain),"all WeatherPain reads have exact terrain closure");
                    // Update performs local-immunity aging before its AI. The
                    // blocked target fixture stays blocked for the whole run.
                    if(mode==2)original.localNPCImmunity[2]=copy.localNPCImmunity[2];
                    original.Update(original.whoAmI);if(original.active!=copy.active || Vector2.Distance(original.position,copy.position)>=.002f || Vector2.Distance(original.velocity,copy.velocity)>=.002f || original.ai[0]!=copy.ai[0] || original.ai[1]!=copy.ai[1])Console.WriteLine("WEATHER difference mode="+mode+" action="+action+" active="+original.active+"/"+copy.active+" pos="+original.position+"/"+copy.position+" vel="+original.velocity+"/"+copy.velocity+" ai="+string.Join("/",original.ai)+" vs "+string.Join("/",copy.ai)+" left="+original.timeLeft+"/"+copy.timeLeft);Require(original.active==copy.active && Vector2.Distance(original.position,copy.position)<.002f && Vector2.Distance(original.velocity,copy.velocity)<.002f && original.ai[0]==copy.ai[0] && original.ai[1]==copy.ai[1],"original969 full action agrees for target/LOS/local exclusion/retirement mode="+mode+" action="+action);
                    if(copy.active)copy.timeLeft--;
                }
            }
            Console.WriteLine("PASS WEATHER native4 motor branches: acquire/blockedLOS/local exclusion/570 retire");
        }
        private static void Retirement(object context)
        {
            foreach(bool newHigher in new[]{true,false})
            {
                var p=Reset(context);var n=Target(2,new Vector2(1700,350));var a=Spawn(p,new Vector2(900,350),Vector2.UnitX);var b=Spawn(p,new Vector2(940,350),Vector2.UnitX);var newer=newHigher?b:a;var older=newHigher?a:b;newer.timeLeft=3600;older.timeLeft=3500;var qa=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",a);var qb=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",b);var scene=new[]{qa,qb};var targets=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true),"Capture",qa,1L);var path=Timeline(n);var terrain=Terrain();
                foreach(var q in scene){Require((bool)Static(model,"Step",q,scene,targets,path,0,terrain),"complete same-owner scene is readable");q.timeLeft--;}
                a.Update(a.whoAmI);b.Update(b.whoAmI);Require(a.ai[1]==qa.ai[1] && b.ai[1]==qb.ai[1] && older.ai[1]==(newHigher?555:556),"newer969 retires only same-owner older storm in real lower/higher slot order");
                object lease;Known(p,new[]{newer},n,out lease);older.ai[1]++;Require(!(bool)Get(lease,"Unchanged"),"same-GUC fading sibling clock revokes the full family lease");
            }
            Console.WriteLine("PASS WEATHER two native slot orders /555 retirement / sibling clock lease");
        }
        private static void Receive(object context)
        {
            foreach(int cooldown in new[]{0,45})
            {
                var p=Reset(context);var n=Target(2,new Vector2(1100,350));var q=Spawn(p,new Vector2(900,350),new Vector2(1,0));Projectile.perIDStaticNPCImmunity[969,2]=(uint)Main.GameUpdateCount+(uint)cooldown;object lease;var expected=Known(p,new[]{q},n,out lease);Require(expected!=null,"native autonomous storm has a real conditional future receiver");Advance(90);RequireClock(expected);Require(first==expected.Tick,"native ID-static expiry and AI177 endpoints preserve exact first Strike");Console.WriteLine("WEATHER receive cooldown="+cooldown+" expected="+expected.Tick+" native="+first);
            }
        }
        private static void Hand(object context)
        {
            var p=Reset(context);var n=Target(2,new Vector2(1150,667));var path=Timeline(n);var sample=new Projectile();sample.SetDefaults(969);sample.owner=p.whoAmI;var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var origin=p.RotatedRelativePoint(p.MountedCenter);var expected=(AttackContact)Static(model,"Solve",p,ammo,path,origin,0,true,Terrain(),Root(p,sample,path),0);Require(expected!=null,"5118 captures real magic damage and solves a complete finite storm family");Main.mouseX=(int)(expected.AimX-Main.screenPosition.X);Main.mouseY=(int)(expected.AimY-Main.screenPosition.Y);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var born=Main.projectile.Single(q=>q.active && q.type==969);Require(born.damage==(int)Get(ammo,"Damage"),"real native5118 Shoot has the captured damage");Advance(90);RequireClock(expected);Require(first==expected.Tick,"actual5118 Shoot->AI177->Damage matches the prepared first useful receive");Console.WriteLine("WEATHER hand expected="+expected.Tick+" native="+first);
        }
    }
}


