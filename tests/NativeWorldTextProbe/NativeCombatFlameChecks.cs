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
    internal static class NativeCombatFlameChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static bool actualHand;private static NPC target;private static long firstHit;private static int damageDepth;
        private static void BeforeDamage(Projectile __instance){if(__instance.type==85)damageDepth++;}
        private static void AfterDamage(Projectile __instance){if(__instance.type==85)damageDepth--;}
        private static void Strike(NPC __instance,int __result){if(damageDepth>0 && ReferenceEquals(__instance,target) && __result>0 && firstHit<0)firstHit=Main.GameUpdateCount;}
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var a=combat.GetType().Assembly;var audit=new Harmony("JueMingR.Tests.Flame");bool wind=Main.windPhysics;Exception primaryFailure=null;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFlameChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatFlameChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatFlameChecks),nameof(Strike)));
            var componentHand=new NativeCombatHandFixture(context,()=>actualHand,()=>target);
            try{if(Environment.GetEnvironmentVariable("JMR_FLAME_ONLY")!="natural"){Births(context,a);Motor(context,a);Shapes(context,a);DamageWindow(context);}Natural(context,host,combat);}
            catch(Exception error){primaryFailure=error;Console.WriteLine("FLAME PRIMARY FAILURE "+error);throw;}
            finally
            {
                componentHand.Dispose();actualHand=false;target=null;damageDepth=0;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);
                try{NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
                catch(Exception error){if(primaryFailure==null)throw;Console.WriteLine("FLAME CLEANUP FAILURE (primary retained) "+error);}
            }
        }
        private static Player Reset(object context,int weapon)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);p.position=new Vector2(750,646);p.ResetEffects();p.ResetFallDamage();p.releaseUseItem=true;p.inventory[54].SetDefaults(23);p.inventory[54].stack=999;Main.windPhysics=false;Main.screenPosition=new Vector2(500,400);Main.rand=new Terraria.Utilities.UnifiedRandom(7417);return p;
        }
        private static object Terrain(Assembly a){var t=Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static void Births(object context,Assembly a)
        {
            var model=a.GetType("JueMingR.TerrariaHost.Combat.HostFlameAttack",true);int cases=0,extended=0,blocked=0;
            foreach(int weapon in new[]{506,1910})foreach(int direction in new[]{-1,1})foreach(float gravity in new[]{-1f,1f})foreach(bool wall in new[]{false,true})
            {
                var p=Reset(context,weapon);p.gravDir=gravity;p.direction=direction;p.position=new Vector2(950,350);if(wall)for(int y=5;y<85;y++)NativeToolsChecks.Tile(direction<0?58:62,y,1);
                var origin=p.RotatedRelativePoint(p.MountedCenter);var aim=origin+new Vector2(direction*400,70);NativeToolExecutionChecks.Sample(context,Get(context,"Input"),aim,true);Call(Get(context,"Combat"),"Sample");
                // Use the very mouse vector the original Shoot will read;
                // the input fixture also maps reversed gravity to pixels.
                var unit=(Main.MouseWorld-origin).SafeNormalize(Vector2.UnitX);var ammo=a.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true).GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});float speed=(float)Get(ammo,"Speed");var terrain=Terrain(a);
                var expected=(Vector2)model.GetMethod("BirthOrigin",Flags).Invoke(null,new object[]{p,origin,unit,speed,terrain});
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var q=Main.projectile.Single(s=>s.active && s.owner==p.whoAmI && s.type==85);
                Require(Vector2.DistanceSquared(q.Center,expected)<.001f && Vector2.DistanceSquared(q.velocity,unit*speed)<.0001f && q.ai[0]==(weapon==1910?1:0),"native flame gun muzzle/terrain/side/gravity and variant birth "+weapon+"/"+direction+"/"+gravity+"/"+wall+" native="+q.Center+" model="+expected);
                if(Vector2.Distance(origin,q.Center)>speed*3)extended++;else blocked++;cases++;
            }
            Require(cases==16 && extended>0 && blocked>0,"both usable flamethrowers exercise obstructed and advanced muzzles");Console.WriteLine("PASS native flame births="+cases+" forwardMuzzles="+extended+" obstructedMuzzles="+blocked);
        }
        private static void Motor(object context,Assembly a)
        {
            var model=a.GetType("JueMingR.TerrariaHost.Combat.HostFlameAttack",true);var st=model.GetNestedType("State",BindingFlags.NonPublic);int cases=0,actions=0,waterDeaths=0,wallSurvivals=0,damageOff=0;
            foreach(int weapon in new[]{506,1910})foreach(int direction in new[]{-1,1})foreach(int scene in Enumerable.Range(0,10))
            {
                var p=Reset(context,weapon);foreach(var n in Main.npc)n.active=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                if(scene>=1 && scene<=4)for(int x=25;x<115;x++)for(int y=10;y<70;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene-1);}
                if(scene==5 || scene==6)for(int y=10;y<70;y++)NativeToolsChecks.Tile(direction<0?54:64,y,1);
                if(scene==6)Main.tile[direction<0?54:64,22].slope((byte)(direction<0?1:2));
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950,350),new Vector2(direction*(scene==9?20:5.5f),.3f),85,0,0,p.whoAmI,weapon==1910?1:0);var q=Main.projectile[slot];
                if(scene==7)q.timeLeft=2;if(scene==8)q.position.X=Main.leftWorld;
                q.wet=Collision.WetCollision(q.position,q.width,q.height);q.honeyWet=q.wet && Collision.honey;q.shimmerWet=q.wet && Collision.shimmer;
                var state=Activator.CreateInstance(st,Flags,null,new object[]{q},null);var terrain=Terrain(a);
                for(int outer=1;outer<=30 && q.active;outer++)
                {
                    for(int sub=0;sub<=q.extraUpdates && (bool)Get(state,"Alive");sub++){Require((bool)model.GetMethod("Step",Flags).Invoke(null,new[]{(object)q,state,terrain}),"flame motor geometry available");model.GetMethod("Tail",Flags).Invoke(null,new[]{state});actions++;}
                    q.Update(slot);bool alive=(bool)Get(state,"Alive");
                    Require(alive==q.active && (!alive || Vector2.DistanceSquared(q.position,(Vector2)Get(state,"Position"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f && q.localAI[0]==(float)Get(state,"Clock") && q.timeLeft==(int)Get(state,"Left") && q.wet==(bool)Get(state,"Wet") && q.honeyWet==(bool)Get(state,"Honey") && q.lavaWet==(bool)Get(state,"Lava") && q.shimmerWet==(bool)Get(state,"Shimmer")),"full native flame motor "+weapon+"/"+direction+"/"+scene+"/"+outer+" native="+q.active+"/"+q.position+"/"+q.velocity+"/"+q.localAI[0]+"/"+q.timeLeft+" model="+alive+"/"+Get(state,"Position")+"/"+Get(state,"Velocity")+"/"+Get(state,"Clock")+"/"+Get(state,"Left"));
                    if(alive && q.localAI[0]>=54)damageOff++;if(alive && (scene==5 || scene==6) && q.velocity.Length()<3)wallSurvivals++;
                }
                if(scene==1 || scene==3 || scene==4){Require(!q.active,"original preceding wet flag retires water/honey/shimmer flame");waterDeaths++;}cases++;
            }
            Require(cases==40 && waterDeaths==12 && wallSurvivals>0 && damageOff>0,"finite flame motors keep liquid retirement, wall settling and harmless visual tail");Console.WriteLine("PASS native flame motors="+cases+" subupdates="+actions+" waterDeaths="+waterDeaths+" wallSurvivals="+wallSurvivals+" harmlessTail="+damageOff);
        }
        private static void Shapes(object context,Assembly a)
        {
            var model=a.GetType("JueMingR.TerrariaHost.Combat.HostFlameAttack",true);var st=model.GetNestedType("State",BindingFlags.NonPublic);int cases=0,expanded=0,wallRejected=0;
            foreach(int clock in new[]{0,1,20,53,54,71})foreach(int offset in new[]{-40,-20,0,20,40})foreach(bool wall in new[]{false,true})
            {
                var p=Reset(context,506);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950.25f,350.5f),Vector2.UnitX*7,85,0,0,p.whoAmI);var q=Main.projectile[slot];q.localAI[0]=clock;if(wall)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                var state=Activator.CreateInstance(st,Flags,null,new object[]{q},null);var terrain=Terrain(a);var other=new Rectangle(945+offset,345,10,10);var box=(Rectangle)model.GetMethod("DamageBox",Flags).Invoke(null,new[]{(object)q,state});Require(box==q.Damage_GetHitbox(),"native expanding flame integer damage rectangle");
                object[] args={q,state,terrain,new MotionRect(other.X,other.Y,other.Width,other.Height),false};Require((bool)model.GetMethod("Collision",Flags).Invoke(null,args),"flame collision geometry available");bool native=q.Colliding(q.Damage_GetHitbox(),other);Require((bool)args[4]==native,"flame custom collider preserves point CanHit through enlarged body "+clock+"/"+offset+"/"+wall);
                if(native && !q.Hitbox.Intersects(other))expanded++;if(!native && box.Intersects(other))wallRejected++;cases++;
            }
            Require(cases==60 && expanded>0 && wallRejected>0,"enlarged positive and wall negative reject body-only or wall-blind substitutes");Console.WriteLine("PASS native flame colliders="+cases+" expandedPositives="+expanded+" wallRejections="+wallRejected);
        }
        private static void Natural(object context,object host,object combat)
        {
            int cases=0,hits=0,misses=0;var attack=Get(combat,"Attack");
            foreach(int weapon in new[]{506,1910})foreach(int direction in new[]{-1,1})foreach(int distance in new[]{8,20})foreach(bool wall in new[]{false,true})
            {
                var p=Reset(context,weapon);target=NativeCombatControlPreviewChecks.Dummy(2,47+direction*distance,40);firstHit=-1;damageDepth=0;
                if(wall)for(int y=0;y<Main.maxTilesY;y++)NativeToolsChecks.Tile(47+direction*4,y,1);
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,30,true,false));AttackContact preview=null;
                for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,target.Center,false,true);preview=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(preview==null && !Main.projectile.Any(q=>q.active),"withdrawn idle flame preview creates no native shot or contact");}
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,30,true,true));
                NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center,true);Call(combat,"Sample");
                NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);preview=(AttackContact)GetOptional(attack,"ExpectedImpact");
                Require(wall?preview==null:preview!=null,"pressed flame component retains its exact open/wall preparation "+weapon+"/"+direction+"/"+distance);
                if(weapon==506 && direction==1 && distance==20 && !wall)Workload(combat.GetType().Assembly,p,preview);
                if(preview!=null)Call(attack,"BindPresentation",preview);
                if(preview!=null)
                {
                    var impact=Get(Get(host,"World"),"Impact");Call(impact,"Capture");var zoom=Main.GameViewMatrix.ZoomMatrix;Call(impact,"Project",zoom,Matrix.Invert(zoom));var b=preview.ReceiveBounds;
                    var center=new Vector2(b.CenterX,b.CenterY);var projected=Vector2.Transform(center-Main.screenPosition,zoom);
                    var cornerA=Vector2.Transform(new Vector2(b.X,b.Y)-Main.screenPosition,zoom);var cornerB=Vector2.Transform(new Vector2(b.X+b.Width,b.Y+b.Height)-Main.screenPosition,zoom);
                    bool inView=Math.Max(cornerA.X,cornerB.X)>=-4 && Math.Max(cornerA.Y,cornerB.Y)>=-4 && Math.Min(cornerA.X,cornerB.X)<=Main.screenWidth+4 && Math.Min(cornerA.Y,cornerB.Y)<=Main.screenHeight+4;
                    Require((bool)Get(impact,"Visible")==inView && Vector2.DistanceSquared((Vector2)Get(impact,"Position"),projected)<.001f,"flame receive-time rectangle centre is projected exactly and intersecting edges remain visible");
                    // The original left/far case projected to (-185,255). Its
                    // invisible marker was correct. Pan only the camera, then
                    // project the very same captured contact; do not re-solve.
                    if(!inView)
                    {
                        Main.screenPosition=center-new Vector2(Main.screenWidth/2,Main.screenHeight/2);Call(impact,"Project",zoom,Matrix.Invert(zoom));
                        Require((bool)Get(impact,"Visible") && Vector2.DistanceSquared((Vector2)Get(impact,"Position"),Vector2.Transform(center-Main.screenPosition,zoom))<.001f,"same flame contact returns on screen without a new preparation");
                    }
                }
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,true,true,30,true,true));long start=Main.GameUpdateCount;bool born=false;actualHand=true;
                for(int frame=1;frame<=60 && firstHit<0;frame++){NativeCombatSelectionPreviewChecks.Step(context,target.Center,frame==1);born|=Main.projectile.Any(q=>q.active && q.type==85 && q.owner==p.whoAmI);}
                Require(born && (wall?firstHit<0:firstHit==preview.Timeline.SampleTick+preview.Tick),"actual original flame hand agrees with its prepared exact contact and real wall "+weapon+"/"+direction+"/"+distance+"/"+wall+" actual="+(firstHit<0?-1:firstHit-start)+" preview="+preview?.Tick);
                actualHand=false;if(!wall)Require((bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","actual native flame consumes its prepared direction");
                if(firstHit<0)misses++;else hits++;cases++;
            }
            Require(cases==16 && hits==8 && misses==8,"both actual gel weapons keep left/right near/far hand use and real walls");Console.WriteLine("PASS natural flame cases="+cases+" actualHits="+hits+" blocked="+misses);
        }
        private static void DamageWindow(object context)
        {
            foreach(int clock in new[]{53,54})
            {
                var p=Reset(context,506);foreach(var n in Main.npc)n.active=false;var n2=Main.npc[2]=new NPC{whoAmI=2};n2.SetDefaults(3);n2.active=true;n2.Center=new Vector2(950,350);n2.life=n2.lifeMax=10000;n2.defense=0;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),n2.Center,Vector2.UnitX*7,85,50,0,p.whoAmI);var q=Main.projectile[slot];q.localAI[0]=clock;q.Damage();
                Require((n2.life<10000)==(clock==53) && q.active,"original flame receives at subupdate53 but its live visual body cannot receive at54");
            }
            Console.WriteLine("PASS native flame last damaging subupdate53 / harmless54");
        }
        private static void Workload(Assembly a,Player p,AttackContact preview)
        {
            var model=a.GetType("JueMingR.TerrariaHost.Combat.HostFlameAttack",true);
            if(model.GetMethod("ResetCounters",Flags)==null){Console.WriteLine("NOT_EXECUTED_REQUIRES_SAME_SOURCE_DEBUG flame workload counters; Release functional groups are separate");return;}
            CallStatic(model,"ResetCounters");var terrain=Terrain(a);
            var ammo=a.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true).GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});var sample=Terraria.ID.ContentSamples.ProjectilesByType[85];
            var obstacles=Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sample,preview.Timeline,0,false,true},null);
            var result=(AttackContact)model.GetMethod("Solve",Flags).Invoke(null,new object[]{p,ammo,preview.Timeline,p.RotatedRelativePoint(p.MountedCenter),(int)((long)Main.GameUpdateCount-preview.Timeline.SampleTick),false,terrain,obstacles,0});
            int candidates=(int)model.GetField("Candidates",Flags).GetValue(null),steps=(int)model.GetField("Steps",Flags).GetValue(null);
            Require(result!=null && candidates==1 && steps>0 && steps<=54,"a useful stationary flame solve deduplicates all eighteen receive seeds and exits inside its true damage lifetime");Console.WriteLine("PASS flame solve work candidates="+candidates+" subupdates="+steps+" usefulTick="+result.Tick);
        }
        private static void CallStatic(Type type,string name){type.GetMethod(name,Flags).Invoke(null,null);}
    }
}
