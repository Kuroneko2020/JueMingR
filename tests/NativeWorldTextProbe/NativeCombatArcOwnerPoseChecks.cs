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
    internal static class NativeCombatArcOwnerPoseChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly host;private static Type arc,pumpkin;private static NPC selected;private static int strikes;private static readonly List<Projectile> pumpkinBirths=new List<Projectile>();
        private static void Born(IEntitySource __0,int __result){if(__result>=0 && __result<Main.maxProjectiles && Main.projectile[__result].type==321){var q=Main.projectile[__result];Require((__0 as EntitySource_ItemUse)?.Entity==Main.player[q.owner],"actual Horseman pumpkin source is the original player item-use event");pumpkinBirths.Add((Projectile)Static(arc,"Copy",q));}}
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(selected,__instance) && __result>0)strikes++;}
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static Player Scene(object context,int weapon=1826)
        {var p=NativeCombatBallisticCandidateChecks.Scene(context,weapon,50);foreach(var n in Main.npc)n.active=false;for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);p.velocity=Vector2.Zero;p.direction=1;p.gravDir=1;p.dead=false;p.ResetMeleeHitCooldowns();p.itemAnimation=p.itemAnimationMax=p.HeldItem.useAnimation;Main.windPhysics=false;strikes=0;pumpkinBirths.Clear();return p;}
        private static NPC Target(int slot,Vector2 center,bool blocked=false)
        {var n=Main.npc[slot];n.SetDefaults(3);n.active=true;n.whoAmI=slot;n.width=n.height=40;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.dontTakeDamage=blocked;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Path(NPC n)
        {var key=new NpcIdentity(1,n,n.whoAmI,n.generation,n.type,n.netID);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Vx=n.velocity.X,Vy=n.velocity.Y,CanReceive=!n.dontTakeDamage,Active=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.None,PredictionStop.None,points,points.Length);}
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static AttackContact Known(Player p,Projectile[] sources,NPC n,out object root)
        {var path=Path(n);var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld")},null);root=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sources[0],path,0,false,true},null);var frame=Static(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true),"Capture",p,path);using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)Static(arc,"KnownFamily",p,sources,path,clock,Terrain(),root,frame);}
        private static void UpdateAll(){for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;arc=host.GetType("JueMingR.TerrariaHost.Combat.HostArcSwingAttack",true);pumpkin=host.GetType("JueMingR.TerrariaHost.Combat.HostPumpkinEffectAttack",true);int motors=0,actions=0,actualUseful=0,actualMiss=0;var originalRandom=Main.rand;
            var audit=new Harmony("JueMingR.Tests.ArcOwnerPose");audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatArcOwnerPoseChecks),nameof(Strike)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatArcOwnerPoseChecks),nameof(Born)));
            try
            {
                foreach(int phase in new[]{1,2,-1})foreach(bool wall in new[]{false,true})
                {
                    var p=Scene(context);var victim=Target(1,new Vector2(1000,450));var n=Target(2,new Vector2(1080,450));if(phase==2)victim.dontTakeDamage=true;if(wall)for(int y=10;y<65;y++)NativeToolsChecks.Tile(65,y,1);var path=Path(n);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(800,450),new Vector2(7,1),321,0,0,p.whoAmI,phase<0?-1:1);var actual=Main.projectile[slot];var shadow=(Projectile)Static(arc,"Copy",actual);var targets=Static(host.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true),"Capture",actual,1L);var terrain=Terrain();
                    for(int tick=1;tick<=48 && actual.active;tick++){Require((bool)Static(pumpkin,"Step",shadow,targets,path,tick,terrain),"AI055 sealed native homing/LOS dependencies are captured");if(shadow.active && --shadow.timeLeft<=0){shadow.active=false;shadow.timeLeft=0;}NativeQuickItemChecks.BeginWorldStep();actual.Update(slot);Require(actual.active==shadow.active && (!actual.active || Vector2.DistanceSquared(actual.position,shadow.position)<.002f && Vector2.DistanceSquared(actual.velocity,shadow.velocity)<.0001f && actual.ai[0]==shadow.ai[0]),"native pumpkin valid lock/dead lock reacquire/invalid index and wall motor phase="+phase+" wall="+wall+" tick="+tick);actions++;}motors++;
                }
                foreach(int weapon in new[]{368,1826})foreach(int gravity in new[]{-1,1})
                {
                    var p=Scene(context,weapon);p.itemAnimation=p.itemAnimationMax=p.itemTime=0;p.gravDir=gravity;selected=Target(2,p.MountedCenter+new Vector2(70,0));var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,50,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),selected.Center-new Vector2(500,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,selected,0);var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");var root=GetOptional(attack,"obstacles");Require(plan!=null && root!=null,"zero-speed native owner arc has actual complete prepared source/root weapon="+weapon);var ammo=Get(attack,"ammo");Require((float)Get(ammo,"Speed")==0 && (bool)Call(ammo,"MembersMatch",p,p.HeldItem),"only the actual owner-pose descriptor retains zero speed and its symmetric damage lease");p.velocity=Vector2.UnitX;Require(!(bool)Get(root,"Unchanged"),"same-GUC owner movement revokes actual arc preparation");p.velocity=Vector2.Zero;
                    typeof(Player).GetMethod("ItemCheck_StartActualUse",Flags).Invoke(p,new object[]{p.HeldItem});p.itemAnimation--;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(x=>x.active && (x.type==982 || x.type==997));Require(q.velocity==new Vector2(p.direction,0) && q.ai[0]==p.direction*p.gravDir && q.ai[1]==p.itemAnimationMax && q.ai[2]==p.GetAdjustedItemScale(p.HeldItem) && (bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","native owner-facing arc consumes the actual prepared facing and births UnitX rather than a made-up speed");strikes=0;for(int tick=1;tick<=60 && strikes==0;tick++){NativeQuickItemChecks.BeginWorldStep();p.itemAnimation=Math.Max(0,p.itemAnimation-1);UpdateAll();}Require(strikes>0,"actual zero-speed hand's true native cone receives useful Damage weapon="+weapon+" gravity="+gravity);
                }
                // Prove the finite OnHit dependency through a DIFFERENT early
                // receiver. A distant selected body cannot be reached by997.
                for(int seed=0;seed<16;seed++)
                {
                    var p=Scene(context);var front=Target(1,p.MountedCenter+new Vector2(60,0));var corner=p.position+new Vector2(Main.MaxWorldViewSize.X/2,Main.MaxWorldViewSize.Y/2);var inward=(front.Center-corner).SafeNormalize(Vector2.UnitX);selected=Target(2,corner+inward*65);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.MountedCenter,Vector2.UnitX,997,50,0,p.whoAmI,1,p.itemAnimationMax,1);var parent=Main.projectile[slot];object root;var expected=Known(p,new[]{parent},selected,out root);Require(expected!=null && expected.Confidence==AttackConfidence.Representative,"997 via another receiver has a complete lawful finite321 opportunity outside arc range");Require((bool)Get(root,"Unchanged"),"complete 997 family and pumpkin proc dependencies start current");front.SpawnedFromStatue=true;Require(!(bool)Get(root,"Unchanged") && !(bool)Get(root,"BirthInputsCurrent"),"same-GUC statue proc suppression revokes the actual finite family");front.SpawnedFromStatue=false;
                    Main.rand=new Terraria.Utilities.UnifiedRandom(seed);strikes=0;bool born=false;for(int tick=1;tick<=120 && strikes==0;tick++){NativeQuickItemChecks.BeginWorldStep();p.itemAnimation=Math.Max(0,p.itemAnimation-1);UpdateAll();foreach(var child in Main.projectile.Where(q=>q.active && q.type==321 && !born)){Require(child.damage==parent.damage && child.ai[0]==1 && child.owner==p.whoAmI,"actual Horseman OnHit emits its own item-source finite pumpkin with victim and damage");born=true;}}
                    Require(front.life<100000 && pumpkinBirths.Count>0,"every recorded native lawful corner outcome retains actual parent receive and pumpkin birth seed="+seed+" frontLife="+front.life+" aliveBorn="+born+" snapshots="+pumpkinBirths.Count);foreach(var b in pumpkinBirths){Require(b.damage==parent.damage && b.ai[0]==1 && b.owner==p.whoAmI,"native pumpkin birth, including same-update world retirement, retains actual victim/owner/damage");var offset=b.Center-p.position;Require(Math.Abs(offset.X)==Main.MaxWorldViewSize.X/2 && Math.Abs(offset.Y)==Main.MaxWorldViewSize.Y/2 && Math.Abs(b.velocity.Length()-8)<.001f,"actual random cancellation produces only the four exact viewport corners and native8-speed");}if(strikes>0)actualUseful++;else actualMiss++;
                }
                Require(actualUseful>0,"recorded native random corner outcomes independently prove useful321 receive beyond997 range");
                {
                    var p=Scene(context);var front=Target(1,p.MountedCenter+new Vector2(60,0));front.SpawnedFromStatue=true;selected=Target(2,new Vector2(1500,1000));int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),p.MountedCenter,Vector2.UnitX,997,50,0,p.whoAmI,1,p.itemAnimationMax,1);object root;Require(Known(p,new[]{Main.projectile[slot]},selected,out root)==null,"statue victim retains primary arc but cannot supply a fictitious pumpkin opportunity");for(int tick=1;tick<=60;tick++){NativeQuickItemChecks.BeginWorldStep();p.itemAnimation=Math.Max(0,p.itemAnimation-1);UpdateAll();}Require(front.life<100000 && pumpkinBirths.Count==0,"real statue-sourced receiver still takes primary Damage with no native finite pumpkin proc");
                }
                foreach(bool blocked in new[]{false,true})
                {
                    var p=Scene(context);selected=Target(2,new Vector2(900,450),blocked);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(800,450),Vector2.UnitX*8,321,40,0,p.whoAmI,2);var q=Main.projectile[slot];object root;var expected=Known(p,new[]{q},selected,out root);strikes=0;int first=0;for(int tick=1;tick<=60 && q.active;tick++){NativeQuickItemChecks.BeginWorldStep();q.Update(slot);if(strikes>0){first=tick;break;}}Require(blocked?expected==null && first==0:expected!=null && expected.Tick==first && first>0,"observed321 exact native first Receive with invulnerable negative");
                }
            }
            finally{selected=null;Main.rand=originalRandom;audit.UnpatchAll(audit.Id);}Console.WriteLine("ARC-OWNER-POSE pumpkinMotors="+motors+" actions="+actions+" realHands=4 random16="+actualUseful+"/"+actualMiss+" exactObserved321=2");
        }
    }
}
