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
    internal static class NativeCombatArrowFamilyChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile receiving;private static int hitType,hitTick,currentTick,killEntryType;private static Vector2 killEntry,killVelocity;private static string parentKey;private static readonly Dictionary<string,string> parents=new Dictionary<string,string>();
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static void Damage(Projectile __instance){receiving=__instance;}private static void DamageDone(){receiving=null;}
        private static void KillEntry(Projectile __instance){killEntryType=__instance.type;killEntry=__instance.position;killVelocity=__instance.velocity;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==2 && receiving!=null && hitTick==0){hitTick=currentTick;hitType=receiving.type;}}
        private static void Born(IEntitySource __0,int __result)
        {var source=__0 as EntitySource_Parent;if(source?.Entity is Projectile p && __result>=0 && __result<1000)parents[Main.projectile[__result].key.ToString()]=p.key.ToString();}
        private static Player Reset(object context)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),39,0,0);p.position=new Vector2(850,328);p.velocity=Vector2.Zero;p.ResetEffects();Main.windPhysics=false;Main.ProjectileUpdateLoopIndex=-1;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<Main.maxTilesX-5;x++)for(int y=5;y<Main.maxTilesY-5;y++)Main.tile[x,y].ClearEverything();Main.tileSolid[1]=true;parents.Clear();hitType=hitTick=currentTick=0;return p;
        }
        private static NPC Target(Vector2 center)
        {var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static Projectile Spawn(Player p,int type,Vector2 center,Vector2 velocity,int damage=60)
        {return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,type,damage,0,p.whoAmI)];}
        private static NpcTrajectory Timeline(NPC n)
        {
            var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static AttackContact Known(Player p,Projectile[] source,NPC n,out object obstacles)
        {
            var path=Timeline(n);var clockType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(clockType,Flags,null,new[]{(object)path,phase},null);
            var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");obstacles=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,source[0],path,0,true,false},null);
            return (AttackContact)Static(model,"KnownFamily",p,source,path,clock,terrain,obstacles);
        }
        private static void Advance(NPC n,int limit=120)
        {
            for(currentTick=1;currentTick<=limit && hitTick==0;currentTick++)
            {
                NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);
                for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}
            }
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostArrowFamilyAttack",true);var audit=new Harmony("JueMingR.Tests.ArrowFamily");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatArrowFamilyChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatArrowFamilyChecks),nameof(DamageDone)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatArrowFamilyChecks),nameof(KillEntry)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatArrowFamilyChecks),nameof(Strike)));
            var birth=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float));audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatArrowFamilyChecks),nameof(Born)));
            try{Motors(context);TileKill(context);Births(context);Night(context);NightPenetration(context);Children(context);Hands(context);Console.WriteLine("PASS ARROW FAMILY full AI clocks, original Kill sources and original descendant first Damage/Strike; no later cursor authority.");}
            finally{Main.ProjectileUpdateLoopIndex=-1;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);}
        }
        private static void Motors(object context)
        {
            foreach(int type in new[]{91,117,639,640,1124})
            {
                var p=Reset(context);var q=Spawn(p,type,new Vector2(1000,350),new Vector2(6,-1));var shadow=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",q);shadow.oldPos=(Vector2[])q.oldPos.Clone();
                for(int step=0;step<60;step++)
                {
                    Require((bool)Static(model,"Ai",shadow),"moving native arrow obtains its complete AI clock");q.AI();
                    Require(Vector2.Distance(q.velocity,shadow.velocity)<.0001f && q.ai.SequenceEqual(shadow.ai) && q.localAI.SequenceEqual(shadow.localAI),"native original AI motion and sealed child birth clocks equal: "+type+"/"+step);
                    q.position+=q.velocity;shadow.position+=shadow.velocity;q.timeLeft--;shadow.timeLeft--;
                }
            }
            Console.WriteLine("PASS ARROW MOTOR 5 explicit types x60 original AI actions");
        }
        private static void Births(object context)
        {
            foreach(int type in new[]{91,117,639})
            {
                var p=Reset(context);var parent=Spawn(p,type,new Vector2(1100,1050),new Vector2(8,-1));parentKey=parent.key.ToString();if(type==639){parent.localAI[0]=900;parent.localAI[1]=1050;parent.ai[0]=8;parent.ai[1]=-1;}int left=parent.timeLeft;parent.Kill();
                int childType=type==91?92:type==117?1124:640;var children=Main.projectile.Where(q=>q.active && q.type==childType).ToArray();Require(children.Length==(type==117?2:1),"original Kill has the complete native descendant count: "+type);
                foreach(var child in children)
                {
                    Require(parents.TryGetValue(child.key.ToString(),out string cause) && cause==parentKey,"original descendant's EntitySource_Parent retains the real arrow source");
                    if(type==91)Require(child.damage==parent.damage/3 && child.ai[0]==1 && child.ai[1]==parent.position.Y && child.Center.Y<=parent.position.Y-600 && child.Center.Y>parent.position.Y-900 && Math.Abs(child.velocity.Length()-22)<.001f,"Holy first star preserves native random support, divided damage and second-generation clock");
                    if(type==117)Require(child.damage==(int)(parent.damage*.25f) && child.ai[0]==0 && child.velocity.Length()>=1 && child.velocity.Length()<3,"Bone children preserve quarter damage and immature collision phase");
                    if(type==639)Require(child.Center==new Vector2(900,1050) && child.velocity==new Vector2(8,-1) && child.ai[1]==left+1,"Night trail has sealed birth origin/velocity and remaining-action delay outside Main slot loop");
                }
            }
            Console.WriteLine("PASS ARROW KILL native91->92,117->2x1124,639->640 EntitySource_Parent and exact damage/clocks");
        }
        private static void TileKill(object context)
        {
            foreach(int type in new[]{91,117,639})
            {
                var p=Reset(context);for(int y=5;y<75;y++)NativeToolsChecks.Tile(70,y,1);var q=Spawn(p,type,new Vector2(1115,350),new Vector2(8,2),0);var shadow=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",q);shadow.oldPos=(Vector2[])q.oldPos.Clone();
                var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");object[] args={shadow,terrain,false,false,null};Require((bool)Static(model,"Step",args) && (bool)args[2],"native arrow wall impact reaches Kill entry");NativeQuickItemChecks.BeginWorldStep();Main.ProjectileUpdateLoopIndex=q.whoAmI;try{q.Update(q.whoAmI);}finally{Main.ProjectileUpdateLoopIndex=-1;}
                Require(!q.active && killEntryType==type && killEntry==shadow.position && killVelocity==shadow.velocity,"native generic tile Kill first adds its clipped velocity before descendant birth type="+type+" native="+killEntry+" model="+shadow.position);
            }
            Console.WriteLine("PASS ARROW native tile Kill entry centers=3");
        }
        private static void Night(object context)
        {
            foreach(bool highSlot in new[]{true,false})
            {
                var p=Reset(context);if(!highSlot){var reserved=Main.projectile[0];reserved.SetDefaults(1);reserved.active=true;reserved.whoAmI=0;}
                var parent=Spawn(p,639,new Vector2(1300,350),new Vector2(8,0));if(!highSlot)Main.projectile[0].active=false;parent.timeLeft=1;parent.localAI[0]=1000;parent.localAI[1]=350;parent.ai[0]=8;parent.ai[1]=0;parentKey=parent.key.ToString();var n=Target(new Vector2(1070,350));
                object lease;var expected=Known(p,new[]{parent},n,out lease);Require(expected!=null,"Night parent expiry yields a useful delayed child-only receive");Advance(n,30);
                Console.WriteLine("ARROW NIGHT slot="+parent.whoAmI+" higherChild="+highSlot+" predicted="+expected.Tick+" actual="+hitTick+" type="+hitType);
                Require(hitType==640 && hitTick==expected.Tick,"original higher/lower birth slot and skipped subupdates yield the same first child Strike");
                var child=Main.projectile.Single(q=>q.active && q.type==640);Require(parents[child.key.ToString()]==parentKey,"actual delayed trail comes from the expiring source");
                Known(p,new[]{child},n,out lease);child.oldPos[0]+=Vector2.One;Require(!(bool)Get(lease,"Unchanged"),"same-GUC oldPos edit revokes the actual640 lifetime read set");
            }
        }
        private static void Children(object context)
        {
            foreach(int type in new[]{91,117})
            {
                var p=Reset(context);var parent=Spawn(p,type,new Vector2(1100,1050),new Vector2(8,0));parentKey=parent.key.ToString();parent.Kill();int childType=type==91?92:1124;var children=Main.projectile.Where(q=>q.active && q.type==childType).ToArray();
                var first=children[0];var n=Target(first.Center+first.velocity*(type==91?8:20)+new Vector2(0,type==117?20:0));
                if(type==117){n.width=n.height=64;n.Center=first.Center+first.velocity*20+new Vector2(0,20);}
                object lease;var expected=Known(p,children,n,out lease);Require(expected!=null,"observed original Kill children retain an actual future receiving stage: "+childType);Advance(n,90);
                Console.WriteLine("ARROW CHILD type="+childType+" predicted="+expected.Tick+" actual="+hitTick+" receiveType="+hitType);
                Require(hitType==childType && hitTick==expected.Tick,"observed native descendant's first original Damage/Strike matches the complete family replay: "+childType);
                if(type==117)Require(hitTick>=7,"Bone's two extra actions retain the native15-action no-damage gate");
            }
        }
        private static void NightPenetration(object context)
        {
            foreach(bool highSlot in new[]{true,false})
            {
                var p=Reset(context);if(!highSlot){Main.projectile[0].SetDefaults(1);Main.projectile[0].active=true;Main.projectile[0].whoAmI=0;}
                var parent=Spawn(p,639,new Vector2(1300,350),new Vector2(8,0));if(!highSlot)Main.projectile[0].active=false;parent.timeLeft=5;parent.localAI[0]=1000;parent.localAI[1]=350;parent.ai[0]=8;parent.ai[1]=0;var n=Target(new Vector2(1070,350));
                foreach(int slot in new[]{0,1,3,4}){typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var other=Main.npc[slot];other.SetDefaults(3);other.whoAmI=slot;other.active=true;other.width=other.height=50;other.Center=new Vector2(1308,350);other.life=other.lifeMax=10000;other.defense=0;other.aiStyle=-1;other.noGravity=other.noTileCollide=true;other.knockBackResist=0;Array.Clear(other.immune,0,other.immune.Length);}
                object oldRoot;var oldPlan=Known(p,new[]{parent},n,out oldRoot);var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,parent,path,0,true,false},null);AttackContact plan;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))plan=(AttackContact)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostHeldAmmoFamilyAttack",true),"KnownFamily",p,new[]{parent},path,clock,terrain,root);Advance(n,30);
                Require(hitType==640 && hitTick>0 && oldPlan!=null && plan!=null && oldPlan.Tick==hitTick && plan.Tick==hitTick,"four real prior NPC slots exhaust Night inside Damage before lifetime tail; both consumers keep original lower/higher child delay high="+highSlot+" native="+hitTick+" old="+(oldPlan?.Tick??-1)+" batch="+(plan?.Tick??-1));Console.WriteLine("PASS ARROW Night pen0 immediate native Kill highChild="+highSlot+" old="+oldPlan.Tick+" batch="+plan.Tick+" native="+hitTick);
            }
        }
        private static void Hands(object context)
        {
            foreach(var row in new[]{new[]{39,516,91},new[]{39,3568,639},new[]{682,40,117}})
            {
                var p=Reset(context);p.inventory[p.selectedItem].SetDefaults(row[0]);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;p.itemAnimationMax=p.HeldItem.useAnimation;p.itemAnimation=p.itemAnimationMax;
                var n=Target(new Vector2(1250,350));var path=Timeline(n);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var origin=p.RotatedRelativePoint(p.MountedCenter);
                var sample=new Projectile();sample.SetDefaults(row[2]);sample.owner=p.whoAmI;
                var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var obstacles=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sample,path,0,true,false},null);
                var expected=(AttackContact)Static(model,"Solve",p,ammo,path,origin,0,true,terrain,obstacles,0);Require(expected!=null && (int)Get(ammo,"Damage")>0,"ordinary resolved arrow has real captured damage and a useful full-family solution");
                Main.mouseX=(int)(expected.AimX-Main.screenPosition.X);Main.mouseY=(int)(expected.AimY-Main.screenPosition.Y);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var shot=Main.projectile.Single(q=>q.active);
                Require(shot.type==row[2] && shot.damage==(int)Get(ammo,"Damage"),"original ordinary Shoot preserves actual ammo conversion and damage: "+row[2]);Advance(n,90);
                Console.WriteLine("ARROW HAND type="+row[2]+" predicted="+expected.Tick+" actual="+hitTick+" receiveType="+hitType);
                Require(hitTick>0 && hitTick==expected.Tick && hitType==row[2],"ordinary native Shoot->AI->Damage first useful receive matches full-family preparation");
            }
        }
    }
}
