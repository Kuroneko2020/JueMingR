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
    internal static class NativeCombatBubbleFlailFamilyChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile parent,receiving;private static int step,first;private static readonly List<Projectile> births=new List<Projectile>();
        private static bool traceLaunch;private static Vector2 tracedOwner,tracedAfter;private static Projectile launchBefore,launchBody;
        private static void ParentBefore(Projectile __0,Player __1){if(traceLaunch && __0.type==404 && __0.ai[1]==0){traceLaunch=false;tracedOwner=__1.Center;launchBody=__0;launchBefore=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostSuperStarAttack",true),"Copy",__0);}}
        private static void ParentAfter(Projectile __0){if(ReferenceEquals(launchBody,__0) && __0.ai[1]==1)tracedAfter=__0.Center;}
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void Birth(IEntitySource __0,int __result){if((__0 as EntitySource_Parent)?.Entity==parent && __result>=0 && __result<1000 && Main.projectile[__result].type==405)births.Add(Main.projectile[__result]);}
        private static void Damage(Projectile __instance){receiving=__instance;}private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==2 && receiving!=null && (receiving.type==404 || receiving.type==405 || receiving.type==1058) && first==0)first=step;}
        private static Player Reset(object context,int type=404)
        {NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),type==404?2611:5526,0,0);p.position=new Vector2(850,646);p.velocity=Vector2.Zero;p.ResetEffects();p.channel=p.controlUseItem=true;p.itemAnimation=p.itemAnimationMax=30;Main.windPhysics=false;Main.mapFullscreen=false;foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<115;x++)for(int y=10;y<65;y++)Main.tile[x,y].ClearEverything();for(int x=35;x<115;x++)NativeToolsChecks.Tile(x,43,1);births.Clear();first=step=0;parent=receiving=null;return p;}
        private static NPC Target(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.width=30;n.height=40;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static Projectile Shot(Player p,int type,Vector2 center,Vector2 v,float ai0=0,float ai1=0)
        {return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,v,type,60,4,p.whoAmI,ai0,ai1)];}
        private static object Inputs(Player p,params Projectile[] scene)
        {return Activator.CreateInstance(model.GetNestedType("Inputs",BindingFlags.NonPublic),Flags,null,new object[]{p,scene},null);}
        private static object Targets(Projectile q){return Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true),"Capture",q,1L);}
        private static AttackContact Known(Player p,Projectile source,NPC n,out object root)
        {var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,source,path,0,true,false},null);return (AttackContact)Static(model,"KnownFamily",p,new[]{source},path,clock,Terrain(),root);}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBubbleFlailFamilyAttack",true);var audit=new Harmony("JueMingR.Tests.BubbleFlailFamily");bool wind=Main.windPhysics,map=Main.mapFullscreen;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatBubbleFlailFamilyChecks),nameof(Birth)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBubbleFlailFamilyChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatBubbleFlailFamilyChecks),nameof(DamageDone)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatBubbleFlailFamilyChecks),nameof(Strike)));
            audit.Patch(model.GetMethod("Parent",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBubbleFlailFamilyChecks),nameof(ParentBefore)),postfix:new HarmonyMethod(typeof(NativeCombatBubbleFlailFamilyChecks),nameof(ParentAfter)));
            try{Parents(context);Bubbles(context);Births(context);Receive(context);Hands(context);LaunchFrame(context);Console.WriteLine("PASS BUBBLE FLAIL native404/1058 complete owner phases, causal405 births, locked/nearest chase and receive.");}finally{traceLaunch=false;launchBody=launchBefore=null;Main.windPhysics=wind;Main.mapFullscreen=map;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);}
        }
        private static void Parents(object context)
        {
            int count=0;foreach(int type in new[]{404,1058})foreach(int state in type==404?new[]{0,1,2}:Enumerable.Range(0,7))foreach(bool held in new[]{false,true})
            {
                var p=Reset(context,type);p.channel=p.controlUseItem=held;parent=Shot(p,type,new Vector2(1040,420),new Vector2(8,-1),state);var q=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostSuperStarAttack",true),"Copy",parent);var body=(Player)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);var terrain=Terrain();Main.mouseX=1250-(int)Main.screenPosition.X;Main.mouseY=400-(int)Main.screenPosition.Y;
                for(int action=0;action<35 && parent.active;action++)
                {object[] args={q,body,Main.MouseWorld,false,0f};Require((bool)Static(model,"Parent",args),"complete finite owner phase is available");Require((bool)Static(model,"Move",q,terrain),"native flail movement terrain available");if(q.active && --q.timeLeft<=0)q.active=false;parent.Update(parent.whoAmI);Require(parent.active==q.active && Vector2.Distance(parent.position,q.position)<.004f && Vector2.Distance(parent.velocity,q.velocity)<.002f && parent.ai[0]==q.ai[0] && parent.ai[1]==q.ai[1] && parent.alpha==q.alpha && parent.ownerHitCheck==q.ownerHitCheck,"native flail phase/movement/retirement matches "+type+"/"+state+"/"+held+"/"+action+" native="+parent.position+"/"+parent.velocity+" model="+q.position+"/"+q.velocity);}
                count++;
            }
            Console.WriteLine("PASS BUBBLE FLAIL original parent branches="+count+" full0..6 modern +0..2 legacy, held/release and ground collision");
        }
        private static void Bubbles(object context)
        {
            int count=0;foreach(int mode in new[]{0,1,2,3})
            {
                var p=Reset(context);var n=Target(2,new Vector2(1300,350));var q=Shot(p,405,new Vector2(950,350),new Vector2(8,0),mode==0?-40:3,1.17f);if(mode==2)n.active=false;if(mode==3)for(int y=10;y<55;y++)NativeToolsChecks.Tile(62,y,1);var path=Timeline(n);var inputs=Inputs(p,q);var targets=Targets(q);var shadow=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostSuperStarAttack",true),"Copy",q);var terrain=Terrain();
                for(int action=0;action<25 && q.active;action++){object[] args={shadow,targets,inputs,path,1,terrain,false};Require((bool)Static(model,"Bubble",args) && (bool)Static(model,"Move",shadow,terrain),"bubble active/inactive locked target remains finite and captured");if(shadow.active && --shadow.timeLeft<=0)shadow.active=false;q.Update(q.whoAmI);Require(q.active==shadow.active && q.ai[0]==shadow.ai[0] && q.ai[1]==shadow.ai[1] && Vector2.Distance(q.Center,shadow.Center)<.003f && Vector2.Distance(q.velocity,shadow.velocity)<.001f && q.alpha==shadow.alpha && q.localAI[0]==shadow.localAI[0],"native405 motor/two-bounce/lock ignores target active exactly "+mode+"/"+action);}
                count++;
            }
            var owner=Reset(context);var near=Target(1,new Vector2(1100,350));var far=Target(2,new Vector2(1300,350));var actual=Shot(owner,405,new Vector2(950,350),Vector2.UnitX*5);actual.AI();Require(actual.ai[0]==2 && actual.ai[1]>=.8f && actual.ai[1]<=1.2f,"actual650 nearest chase chooses eligible nearer body and legal native scale");var sealedCopy=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostSuperStarAttack",true),"Copy",actual);object[] stepArgs={sealedCopy,Targets(actual),Inputs(owner,actual),Timeline(far),1,Terrain(),false};Require((bool)Static(model,"Bubble",stepArgs),"observed sealed random action is deterministic on continuation");actual.AI();Require(Vector2.Distance(actual.velocity,sealedCopy.velocity)<.001f,"sealed original random405 birth follows exact later locked motor");Console.WriteLine("PASS BUBBLE native405 branches="+count+" +actual nearest random lock/scale and sealed continuation");
        }
        private static void Births(object context)
        {
            foreach(int type in new[]{404,1058})
            {var p=Reset(context,type);parent=Shot(p,type,new Vector2(1100,350),Vector2.UnitX*8,type==404?0:1);if(type==1058)p.controlUseItem=false;for(int action=1;action<=4;action++){var center=parent.Center;parent.Update(parent.whoAmI);if(action<4)Require(births.Count==0,"actual fixed outbound phase has no bubble before fourth action");else{Require(births.Count==1,"actual fixed outbound fourth action creates one causal bubble");var q=births[0];Require(q.Center==center && q.damage==parent.damage && q.knockBack==parent.knockBack && q.ai[0]==-10 && q.timeLeft==120 && q.velocity.Length()>=4.5f && q.velocity.Length()<=6.401f,"real parent source births405 at pre-move center with complete damage/KB/clock/random support");}}}
        }
        private static void Receive(object context)
        {
            foreach(bool blocked in new[]{false,true})
            {var p=Reset(context);var n=Target(2,new Vector2(1100,350));n.immune[p.whoAmI]=200;if(blocked)n.dontTakeDamage=true;var q=Shot(p,405,n.Center,Vector2.Zero,-10,1.1f);object root;var expected=Known(p,q,n,out root);NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);step=1;q.Update(q.whoAmI);Require(blocked?first==0 && expected==null:first==1 && expected!=null && expected.Tick==1,"actual one-hit405 bypasses owner immunity but retains damage qualification");}
            var owner=Reset(context,1058);var target=Target(2,owner.Center+new Vector2(30,0));parent=Shot(owner,1058,owner.Center,Vector2.Zero);parent.localAI[1]=13;object lease;Require(Known(owner,parent,target,out lease)!=null,"actual held1058 native ellipse retains useful damage beyond displayed ball");owner.channel=false;Require(!(bool)Get(lease,"Unchanged"),"same-GUC channel edit retires the actual complete family root");Console.WriteLine("PASS BUBBLE observed405 receive/owner200 and live1058 ellipse/channel guard");
        }
        private static void Hands(object context)
        {
            foreach(int type in new[]{404,1058})
            {
                var p=Reset(context,type);var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),new Vector2(1250,667),true);Call(combat,"Sample");Main.mouseX=1250-(int)Main.screenPosition.X;Main.mouseY=667-(int)Main.screenPosition.Y;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});parent=Main.projectile.Single(q=>q.active && q.type==type);Require(parent.damage>0 && parent.owner==p.whoAmI,"both original items retain real native404/1058 birth, no current-array adoption");
                if(type==1058)for(int action=0;action<13;action++)parent.Update(parent.whoAmI);var n=Target(2,type==404?parent.Center+parent.velocity*1.5f:p.Center+new Vector2(30,0));object root;var expected=Known(p,parent,n,out root);Require(expected!=null,"actual native hand source retains a useful full-family receive");
                if(type==1058)
                {var control=Get(attack,"Control");var entries=(System.Collections.IDictionary)Get(control,"shots");Require(entries.Contains((int)parent.key),"actual modern flail hand has a registered source");// The public controller borrows the current published calculation, never this leaf's direct-kernel path with synthetic session1.
                NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,phaseName:"BeforeNpc");var entry=entries[(int)parent.key];Console.WriteLine("BUBBLE public1058 realSession="+Get(host,"Session")+" actualPublished="+(((NpcPredictionCache)Get(Get(host,"Prediction"),"Cache")).Read(1)?.Identity.ToString())+" nativeParent="+parent.key);Require(GetOptional(entry,"Obstacles")!=null,"real HostHeld1058 preparation carries the complete family root through the public controller route");}
                for(step=1;step<=40 && first==0 && parent.active;step++){NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;Main.projectile[slot].Update(slot);}Main.ProjectileUpdateLoopIndex=-1;}Require(first>0 && (expected.Confidence!=AttackConfidence.Conditional || first==expected.Tick),"original actual hand has real native Strike, with exact sealed contact or explicitly random future-birth representative");Console.WriteLine("BUBBLE native hand type="+type+" predicted="+expected.Tick+" confidence="+expected.Confidence+" native="+first);
            }
        }
        private static void LaunchFrame(object context)
        {
            foreach(int delay in new[]{0,5})
            {
                var p=Reset(context);p.velocity=new Vector2(3,0);p.controlRight=true;var future=(Player)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(p,null);future.position.X+=3*(delay+1);var n=Target(2,future.Center+new Vector2(150,0));var path=Timeline(n);var frame=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true),"Capture",future,path);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{future,Terraria.ID.ContentSamples.ProjectilesByType[404],path,delay,false,true},null);traceLaunch=true;launchBefore=launchBody=null;AttackContact contact;
                using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)Static(model,"Solve",future,ammo,path,future.RotatedRelativePoint(future.MountedCenter),delay+1,false,Terrain(),root,delay,frame,true);
                Require(contact!=null && launchBefore!=null && tracedOwner==future.Center,"404 uses the supplied future moving owner launch frame exactly once in CompletedWorld/delay: "+delay);var position=p.position;p.position=future.position;var native=Shot(p,404,launchBefore.Center,launchBefore.velocity,launchBefore.ai[0],launchBefore.ai[1]);native.AI();Require(native.Center==tracedAfter,"actual original AI069 at the supplied future owner agrees with the private first parent action");p.position=position;
            }
            Console.WriteLine("PASS BUBBLE future moving launch frame delay0/5 +original first AI069 without duplicate advancement");
        }
    }
}
