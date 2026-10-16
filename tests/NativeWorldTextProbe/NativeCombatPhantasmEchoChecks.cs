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
    internal static class NativeCombatPhantasmEchoChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Assembly assembly;private static Type model;private static Projectile parent,receiving;private static int selectedSlot=2,first,step;private static readonly List<Projectile> children=new List<Projectile>();
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static void Birth(IEntitySource __0,int __result){if((__0 as EntitySource_Parent)?.Entity==parent && __result>=0 && __result<1000 && Main.projectile[__result].type==631)children.Add(Main.projectile[__result]);}
        private static void Damage(Projectile __instance){receiving=__instance;}private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==selectedSlot && receiving?.type==631){Require(receiving.localAI[1]==1,"original631 can Strike only in its post-Kill AI burst");if(first==0)first=step;}}
        private static Player Reset(object context)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3854,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.phantasmTime=20;Main.windPhysics=false;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();children.Clear();first=step=0;selectedSlot=2;return p;
        }
        private static NPC Target(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {
            var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static AttackContact Known(Player p,Projectile[] sources,NPC n)
        {
            var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sources[0],path,0,true,false},null);return (AttackContact)Static(model,"KnownFamily",p,sources,path,clock,terrain,root);
        }
        private static void Advance(int limit)
        {for(step=1;step<=limit && first==0;step++){NativeQuickItemChecks.BeginWorldStep();foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostPhantasmEchoAttack",true);var audit=new Harmony("JueMingR.Tests.PhantasmEcho");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            var birth=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float));audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(Birth)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(DamageDone)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatPhantasmEchoChecks),nameof(Strike)));
            try{Births(context);Receive(context);Boundaries(context);Console.WriteLine("PASS PHANTASM ECHO real arrow OnHit3x631, sealed victim/waits, no flight Damage and actual terminal side receive.");}finally{Main.windPhysics=wind;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);}
        }
        private static void Births(object context)
        {
            foreach(int type in new[]{1,91,117,639})
            {
                var p=Reset(context);var n=Target(2,new Vector2(1100,667));parent=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),n.Center,Vector2.UnitX*8,type,60,0,p.whoAmI)];parent.Damage();
                Require(children.Count==3 && children.Select(q=>q.ai[1]).SequenceEqual(new[]{0f,15f,30f}),"every qualifying actual arrow OnHit produces exactly3 native delayed echoes");
                foreach(var child in children)Require(child.ai[0]==n.whoAmI && child.damage==18 && p.Hitbox.Contains(child.Center.ToPoint()) && Math.Abs(child.velocity.Length()-6)<.001f,"native echo inherits real parent cause,30%pre-final-OnHit damage, legal owner-body birth and locked victim");
                Require(n.life<10000,"echo birth follows a real primary Strike, never a hypothetical first shot");
            }
            Console.WriteLine("PASS PHANTASM native4 arrow OnHit families, original3 source children /0,15,30 delays");
        }
        private static void Receive(object context)
        {
            var p=Reset(context);var n=Target(2,new Vector2(1100,667));parent=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),n.Center,Vector2.UnitX*8,1,60,0,p.whoAmI)];parent.Damage();parent.Kill();p.phantasmTime=0;Array.Clear(n.immune,0,n.immune.Length);var source=children.ToArray();var expected=Known(p,source,n);Require(expected!=null,"actual already-born631 retains its sealed later receive after owner phantom timer expires");Advance(90);Require(first==expected.Tick,"original AI122 wait/movement/pre-move burst yields identical first echo Strike");Console.WriteLine("PHANTASM actual631 predicted="+expected.Tick+" nativeFirst="+first);
        }
        private static void Boundaries(object context)
        {
            var p=Reset(context);p.phantasmTime=0;var victim=Target(1,new Vector2(1300,667));var selected=Target(2,new Vector2(1050,667));var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900,667),Vector2.UnitX*6,631,30,0,p.whoAmI,1,15)];Require(Known(p,new[]{q},selected)==null,"passing through a different receiver does not grant631 in-flight damage or retarget");Advance(90);Require(first==0 && selected.life==10000,"actual631 passes selected body without Strike before its locked endpoint");
            p=Reset(context);p.phantasmTime=0;victim=Target(1,new Vector2(1100,667));victim.dontTakeDamage=true;selected=Target(2,victim.Center);q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),victim.Center,Vector2.Zero,631,30,0,p.whoAmI,1,30)];var expected=Known(p,new[]{q},selected);Require(expected!=null && expected.Tick==1,"active invulnerable locked target still triggers native terminal burst before its wait, allowing an adjacent real receiver");Advance(1);Require(first==1 && selected.life<10000 && victim.life==10000,"original active-only lock and post-Kill Damage preserve the real side-receive counterexample");Console.WriteLine("PASS PHANTASM no flight damage / active invulnerable lock / terminal adjacent receive");
        }
    }
}
