using System;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatMagnetSphereChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static NPC target;private static Projectile damaging;private static long firstHit;private static int orbBirths,boltBirths;private static object currentContext;private static bool enhanced;
        private static void Damage(Projectile __instance){damaging=__instance;}
        private static void DamageEnd(){damaging=null;}
        private static void Strike(NPC __instance,int __result)
        {if(ReferenceEquals(__instance,target) && damaging!=null && __result>0){Require(damaging.type==255,"the orb itself never supplies damage");if(firstHit<0)firstHit=Main.GameUpdateCount;}}
        private static void Birth(IEntitySource __0,int __result)
        {
            if(__result<0 || __result>=Main.maxProjectiles)return;var p=Main.projectile[__result];
            if(p.type==255){Require(__0 is EntitySource_Parent parent && parent.Entity is Projectile source && source.type==254,"a real 255 has the actual orb parent source");boltBirths++;}
            if(p.type==254){orbBirths++;var attack=Get(Get(currentContext,"Combat"),"Attack");enhanced=(bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced";}
        }
        private static Player Scene(object context)
        {
            var host=Get(context,"CombatObservation");NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),1266,0,0);p.position=new Vector2(650,646);p.ResetEffects();p.statMana=p.statManaMax=p.statManaMax2=1000;p.releaseUseItem=true;
            foreach(var n in Main.npc)n.active=false;Main.windPhysics=false;Main.tileSolid[1]=true;Main.screenPosition=new Vector2(500,400);
            for(int x=5;x<115;x++)for(int y=5;y<78;y++)Main.tile[x,y].ClearEverything();
            return p;
        }
        internal static void Run(object context)
        {
            currentContext=context;var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var assembly=combat.GetType().Assembly;
            var type=assembly.GetType("JueMingR.TerrariaHost.Combat.HostMagnetSphereAttack",true);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            var copy=assembly.GetType("JueMingR.TerrariaHost.Combat.HostRaySpellAttack",true).GetMethod("Copy",Flags);
            int cases=0,actions=0;
            foreach(var velocity in new[]{new Vector2(5,0),new Vector2(-4,-2),new Vector2(0,3)})foreach(int liquid in new[]{-1,0,2,3})
            {
                var p=Scene(context);if(liquid>=0)for(int x=20;x<115;x++)for(int y=5;y<78;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                if(liquid<0)for(int y=5;y<78;y++)NativeToolsChecks.Tile(50,y,1);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(730,450),velocity,254,0,0,p.whoAmI);var native=Main.projectile[slot];var model=(Projectile)copy.Invoke(null,new object[]{native});
                var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                for(int t=0;t<50 && native.active;t++)
                {
                    Require((bool)type.GetMethod("StepParent",Flags).Invoke(null,new object[]{model,terrain}),"parent AI is modeled in the observed environment");
                    if(model.active)Require((bool)type.GetMethod("MoveParent",Flags).Invoke(null,new object[]{model,terrain}),"parent native collision/liquid move is modeled");
                    type.GetMethod("Finish",Flags).Invoke(null,new object[]{model});NativeQuickItemChecks.BeginWorldStep();native.Update(slot);actions++;
                    Require(native.active==model.active && (!native.active || Vector2.DistanceSquared(native.position,model.position)<.002f && Vector2.DistanceSquared(native.velocity,model.velocity)<.0001f && native.ai[0]==model.ai[0] && native.ai[1]==model.ai[1] && native.frame==model.frame && native.frameCounter==model.frameCounter && native.timeLeft==model.timeLeft),
                        "original orb motor/bounce/water/honey/shimmer: "+liquid+"/"+velocity+"/"+t+" native="+native.position+"/"+native.velocity+" model="+model.position+"/"+model.velocity);
                }
                cases++;
            }
            var audit=new Harmony("JueMingR.Tests.MagnetSphere");
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatMagnetSphereChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatMagnetSphereChecks),nameof(DamageEnd)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatMagnetSphereChecks),nameof(Strike)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatMagnetSphereChecks),nameof(Birth)));
            try
            {
                foreach(bool olderOrb in new[]{false,true})foreach(bool wall in new[]{false,true})
                {
                    var p=Scene(context);target=Main.npc[2];target.SetDefaults(3);target.whoAmI=2;target.active=true;target.Center=new Vector2(1060,660);target.aiStyle=-1;target.noGravity=target.noTileCollide=true;target.life=target.lifeMax=10000;target.immortal=target.dontTakeDamage=target.friendly=false;Array.Clear(target.immune,0,target.immune.Length);
                    if(wall)for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(55,y,1);
                    if(olderOrb){var old=Main.projectile[0];old.SetDefaults(254);old.active=true;old.whoAmI=0;old.owner=p.whoAmI;old.Center=new Vector2(700,450);old.velocity=Vector2.Zero;old.timeLeft=1;old.damage=0;}
                    firstHit=-1;orbBirths=boltBirths=0;enhanced=false;
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,50,false,true));
                    var input=Get(context,"Input");NativeToolExecutionChecks.Sample(context,input,target.Center-new Vector2(600,0),true);Call(combat,"Sample");
                    NativeCombatAimChecks.PrepareMechanismFixture(host,Get(combat,"Attack"),target,0);var contact=(AttackContact)GetOptional(Get(combat,"Attack"),"ExpectedImpact");
                    Require(wall?contact==null:contact!=null,"magnet child reachability, not orb proximity, controls the actual plan");
                    long start=Main.GameUpdateCount;int mx=Main.mouseX,my=Main.mouseY;
                    typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});
                    Require(orbBirths==1 && Main.mouseX==mx && Main.mouseY==my,"one native orb birth restores the physical cursor");if(!wall)Require(enhanced,"actual original Shoot consumes the orb launch plan");
                    if(!wall)
                    {
                        var identity=(NpcIdentity)assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{target,1L});
                        var state=new NpcMotionState{Identity=identity,X=target.position.X,Y=target.position.Y,Width=target.width,Height=target.height,CanReceive=true,NoTileCollide=true};
                        var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
                        var path=new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
                        var phaseType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);
                        var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(phaseType,"BeforeNpc"),0},null);
                        var sources=Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && q.type==254 && q.damage>0).ToArray();
                        var receivers=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sources[0],path,0,true,false},null);
                        var reader=Activator.CreateInstance(terrainType,true);Call(reader,"Reset");
                        var known=(AttackContact)type.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,sources,path,clock,reader,receivers});
                        Require(known!=null && known.Tick==contact.Tick,"sealed observed orb predicts its child's contact without a new hand");
                        Require((bool)Get(receivers,"Unchanged"),"known-family lease is valid before native movement");
                        target.friendly=true;Require(!(bool)Get(receivers,"Unchanged"),"a newly unchaseable receiver revokes the known-family lease");target.friendly=false;
                    }
                    for(int t=1;t<=120 && firstHit<0;t++)
                    {
                        NativeQuickItemChecks.BeginWorldStep();
                        for(int slot=0;slot<Main.maxProjectiles;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;Main.projectile[slot].Update(slot);}Main.ProjectileUpdateLoopIndex=-1;
                    }
                    Console.WriteLine("MAGNET hand older="+olderOrb+" wall="+wall+" orbs="+orbBirths+" bolts="+boltBirths+" enhanced="+enhanced+" native="+(firstHit<0?-1:firstHit-start)+" contact="+contact?.Tick);
                    Require(wall?firstHit<0:firstHit-start==contact.Tick && boltBirths>0,"original child first Damage and earlier/later birth-slot phase agree");
                }
            }
            finally{audit.UnpatchAll(audit.Id);target=null;currentContext=null;damaging=null;}
            Console.WriteLine("PASS MAGNET native motors="+cases+" actions="+actions+" actual-parent/child-input=4");
        }
    }
}
