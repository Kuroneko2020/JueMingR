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
    internal static class NativeCombatSuperStarChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile parent,receiving;private static int step,first,kind,depth,rngCalls;private static readonly List<Projectile> children=new List<Projectile>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void Birth(IEntitySource __0,int __result){if((__0 as EntitySource_Parent)?.Entity==parent && __result>=0 && __result<1000 && Main.projectile[__result].type==729)children.Add(Main.projectile[__result]);}
        private static void Random(){if(depth>0)rngCalls++;}
        private static void Damage(Projectile __instance){receiving=__instance;}private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==2 && receiving!=null && (receiving.type==728 || receiving.type==729) && first==0){first=step;kind=receiving.type;}}
        private static Player Reset(object context)
        {NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),4060,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.itemAnimationMax=p.itemAnimation=p.HeldItem.useAnimation;p.inventory[54].SetDefaults(75);p.inventory[54].stack=999;Main.windPhysics=false;foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();for(int x=35;x<115;x++)NativeToolsChecks.Tile(x,43,1);children.Clear();first=step=kind=depth=rngCalls=0;return p;}
        private static NPC Target(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Root(Player p,Projectile source,NpcTrajectory path)
        {return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,source,path,0,true,false},null);}
        private static AttackContact Known(Player p,Projectile[] sources,NPC n,out object root)
        {var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);root=Root(p,sources[0],path);depth++;try{return (AttackContact)Static(model,"KnownFamily",p,sources,path,clock,Terrain(),root);}finally{depth--;}}
        private static void Advance(int maximum)
        {for(step=1;step<=maximum && first==0;step++){NativeQuickItemChecks.BeginWorldStep();foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostSuperStarAttack",true);var audit=new Harmony("JueMingR.Tests.SuperStar");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatSuperStarChecks),nameof(Birth)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSuperStarChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatSuperStarChecks),nameof(DamageDone)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatSuperStarChecks),nameof(Strike)));foreach(var method in typeof(Terraria.Utilities.UnifiedRandom).GetMethods(Flags).Where(m=>m.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && (m.Name=="Next" || m.Name=="NextDouble")))audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatSuperStarChecks),nameof(Random)));
            try{Motor(context);Births(context);Receive(context);Hand(context);Console.WriteLine("PASS SUPER STAR native728 hand/OnHit729, sealed slash receive, no forecast RNG and wall birth.");}finally{Main.windPhysics=wind;Main.ProjectileUpdateLoopIndex=-1;depth=0;audit.UnpatchAll(audit.Id);}
        }
        private static void Motor(object context)
        {
            foreach(int type in new[]{728,729})foreach(bool wall in new[]{false,true})
            {var p=Reset(context);var original=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900,350),new Vector2(8,1),type,50,0,p.whoAmI,type==729?.25f:0,500)];if(wall)for(int y=10;y<55;y++)NativeToolsChecks.Tile(65,y,1);Projectile q;depth++;try{q=(Projectile)Static(model,"Copy",original);}finally{depth--;}var terrain=Terrain();for(int outer=0;outer<45 && original.active;outer++){for(int sub=0;sub<=q.extraUpdates && q.active;sub++){depth++;try{Require((bool)Static(model,"Step",q,terrain),"native star motor terrain is fully captured");}finally{depth--;}if(q.active && --q.timeLeft<=0)q.active=false;}original.Update(original.whoAmI);Require(original.active==q.active && Vector2.Distance(original.position,q.position)<.002f && Vector2.Distance(original.velocity,q.velocity)<.002f && original.ai[0]==q.ai[0],"native728/729 complete extra-update motor/lifetime/tile semantics agree type="+type+" wall="+wall);}Require(rngCalls==0,"observed729 copy must not call its RNG-consuming SetDefaults");}
            Console.WriteLine("PASS SUPER STAR original4 full motor branches and RNG-free observed729 copies");
        }
        private static void Births(object context)
        {
            var p=Reset(context);var n=Target(1,new Vector2(1100,350));parent=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),n.Center,Vector2.UnitX*20,728,80,0,p.whoAmI)];parent.Damage();Require(children.Count==1 && n.life<10000 && parent.localNPCImmunity[1]==-1,"one genuine728 received OnHit produces its own finite729 with permanent parent-local immunity");var child=children[0];Require(child.damage==60 && child.knockBack==0 && child.ai[0]==0 && child.ai[1]==n.Center.Y && child.velocity.Y>0 && Math.Abs(child.velocity.Length()-6)<.001f && Vector2.Distance(child.Center,n.Center-child.velocity*20)<.001f && child.scale>=1 && child.scale<=1.29f,"actual EntitySource child keeps original75%damage/downward edge support/120px offset/discrete scale");depth++;try{var representative=(Projectile)Static(model,"Birth",parent,n.Center);Require(representative.scale==1.14f && representative.damage==child.damage,"private descendant uses an explicit legal discrete scale and no RNG");}finally{depth--;}Require(rngCalls==0,"finite729 representative birth never consumes live RNG");
        }
        private static void Receive(object context)
        {
            foreach(bool higher in new[]{true,false})
            {var p=Reset(context);if(!higher){var reserve=Main.projectile[0];reserve.SetDefaults(1);reserve.active=true;reserve.whoAmI=0;}var victim=Target(1,new Vector2(1100,350));var n=Target(2,new Vector2(1100,390));parent=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),victim.Center,Vector2.UnitX*20,728,80,0,p.whoAmI)];if(!higher)Main.projectile[0].active=false;parent.Damage();Require(children.Count==1 && n.life==10000,"parent receives on another NPC before its own slash can reach selected");var child=children[0];parent.active=false;n.Center=child.Center+child.velocity*24;object root;var expected=Known(p,new[]{child},n,out root);Require(expected!=null,"observed native random729 retains useful exact descent receive");Advance(20);Require(first==expected.Tick && kind==729 && rngCalls==0,"actual own child first Strike matches sealed model with native static history and no prediction RNG");Console.WriteLine("SUPER STAR child highSlot="+higher+" predicted="+expected.Tick+" native="+first);}
        }
        private static void Hand(object context)
        {
            var p=Reset(context);if(Environment.GetEnvironmentVariable("JMR_SUPERSTAR_ROTATED_HAND")=="1")p.fullRotation=.4f;var n=Target(2,new Vector2(1250,667));var path=Timeline(n);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var prototype=Terraria.ID.ContentSamples.ProjectilesByType[728];var origin=p.RotatedRelativePoint(p.MountedCenter);AttackContact expected;depth++;try{expected=(AttackContact)Static(model,"Solve",p,ammo,path,origin,0,true,Terrain(),Root(p,prototype,path),0);}finally{depth--;}Require(expected!=null && rngCalls==0,"actual fallen-star ammo damage yields a complete useful RNG-free family solution");Main.mouseX=(int)(expected.AimX-Main.screenPosition.X);Main.mouseY=(int)(expected.AimY-Main.screenPosition.Y);typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});parent=Main.projectile.Single(q=>q.active && q.type==728);Require(parent.damage==(int)Get(ammo,"Damage"),"actual4060 native ammo has the captured pre-hand damage");Advance(40);Require(first==expected.Tick && kind==728,"ordinary native4060 Shoot/AI151/first Damage matches complete family solve");Console.WriteLine("SUPER STAR hand predicted="+expected.Tick+" native="+first);
            p=Reset(context);for(int y=10;y<65;y++)NativeToolsChecks.Tile(56,y,1);Main.mouseX=1300-(int)Main.screenPosition.X;Main.mouseY=(int)(p.MountedCenter.Y-Main.screenPosition.Y);
            // Original Shoot computes velocity from the rotated origin and physical cursor before testing the muzzle line. Preserve an inherited legal fullRotation.
            var launch=p.RotatedRelativePoint(p.MountedCenter);var velocity=Main.MouseWorld-launch;float normalize=20/(float)Math.Sqrt(velocity.X*velocity.X+velocity.Y*velocity.Y);velocity.X*=normalize;velocity.Y*=normalize;object[] args={p,launch,velocity,Terrain(),null};Require((bool)Static(model,"Origin",args),"near muzzle native line is known");var birth=(Vector2)args[4];typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});parent=Main.projectile.Single(q=>q.active && q.type==728);Console.WriteLine("SUPER STAR BLOCKED PREMISE rotation="+p.fullRotation+" origin="+p.RotatedRelativePoint(p.MountedCenter)+" center="+p.Center+" physicalMouse="+Main.MouseWorld+" assumedV="+velocity+" nativeV="+parent.velocity+" predictedBirth="+birth+" nativeBirth="+parent.Center+" nativeFormula="+(p.Center-parent.velocity*.25f));Require(Vector2.Distance(parent.Center,birth)<.01f && birth==p.Center-velocity*.25f && parent.Center==p.Center-parent.velocity*.25f,"blocked native muzzle line uses true center-minus-quarter-velocity birth");
        }
    }
}
