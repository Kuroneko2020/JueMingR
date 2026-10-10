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
    internal static class NativeCombatPoisonCloudChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static bool watchKill;private static int kills;private static Vector2 deathCenter;
        private static void BeforeKill(Projectile __instance)
        {if(watchKill && __instance.active && __instance.type==510){kills++;deathCenter=__instance.Center;}}
        private static Player Reset(object context)
        {var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3105,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();foreach(var n in Main.npc)n.active=false;for(int i=0;i<Main.maxNPCs;i++)Projectile.perIDStaticNPCImmunity[511,i]=0;return p;}
        private static NPC Target(int slot,Vector2 position,int width=32,int height=40)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.position=position;n.width=width;n.height=height;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(Assembly a,NPC n)
        {var key=(NpcIdentity)a.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static object Terrain(Assembly a){var t=Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Obstacles(Assembly a,Player p,Projectile q,NpcTrajectory t)
        {return Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,t,0,true,false},null);}
        private static object Clock(Assembly a,NpcTrajectory t)
        {return Activator.CreateInstance(a.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{t,Enum.Parse(a.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc"),0},null);}
        internal static void Run(object context)
        {
            var a=Get(context,"Combat").GetType().Assembly;var model=a.GetType("JueMingR.TerrariaHost.Combat.HostPoisonCloudAttack",true);bool wind=Main.windPhysics;var random=Main.rand;bool secondActive=Main.player[1].active;var audit=new Harmony("JueMingR.Tests.PoisonCloud");audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatPoisonCloudChecks).GetMethod("BeforeKill",Flags)));
            try{Points(context,a);Motors(context,a,model);ParentMotors(context,a,model);Receives(context,a,model);Births(context,a,model);Workload(context,a,model);}
            finally{watchKill=false;Main.windPhysics=wind;Main.rand=random;Main.player[1].active=secondActive;audit.UnpatchAll(audit.Id);}
        }
        private static void Points(object context,Assembly a)
        {
            Reset(context);int cases=0;
            foreach(int shape in Enumerable.Range(0,10))
            {
                var tile=Main.tile[60,25];tile.ClearEverything();tile.active(shape!=8);tile.type=(ushort)(shape==6?19:shape==7?380:1);if(shape==1)tile.halfBrick(true);if(shape>=2 && shape<=5)tile.slope((byte)(shape-1));if(shape==9)tile.inActive(true);var terrain=Terrain(a);
                foreach(float x in new[]{0f,.01f,4f,8f,12f,15.99f})foreach(float y in new[]{0f,.01f,4f,8f,12f,15.99f})
                {var point=new Vector2(960+x,400+y);var args=new object[]{point,false};Require((bool)terrain.GetType().GetMethod("ProjectileWorldPointSolid",Flags).Invoke(terrain,args),"point-solid exact tile dependency exists");Require((bool)args[1]==Collision.IsWorldPointSolid(point,true),"native point-solid full/half/four-slope/platform/inactive agrees: "+shape+"/"+x+"/"+y);cases++;}
            }
            foreach(var point in new[]{new Vector2(-1,400),new Vector2(0,400),new Vector2(15.99f,400),new Vector2(Main.maxTilesX*16-1,400)})
            {var args=new object[]{point,false};var terrain=Terrain(a);Require((bool)terrain.GetType().GetMethod("ProjectileWorldPointSolid",Flags).Invoke(terrain,args) && (bool)args[1]==Collision.IsWorldPointSolid(point,true),"native WorldGen padding-one point edge is a known nonsolid result");cases++;}
            Console.WriteLine("PASS POISON point-solid native matrix="+cases);
        }
        private static void Motors(object context,Assembly a,Type model)
        {
            int cases=0,actions=0;
            foreach(int type in new[]{511,512,513})foreach(int scene in Enumerable.Range(0,16))
            {
                var p=Reset(context);Main.player[1].active=true;
                if(scene>=1 && scene<=4)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:scene==3?1:3);}
                if(scene>=6 && scene<=10){Main.tile[60,25].active(true);Main.tile[60,25].type=1;if(scene>6)Main.tile[60,25].slope((byte)(scene-6));}
                var origin=scene>=6 && scene<=10?new Vector2(936,408):new Vector2(950,350);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,new Vector2(2,0),type,0,0,p.whoAmI);var q=Main.projectile[slot];if(scene==11)q.timeLeft=1;if(scene==12)q.ai[0]=259;if(scene==13){q.owner=1;Main.player[1].active=false;}if(scene==14){q.position.X=Main.leftWorld+1;q.velocity.X=-2;}if(scene==15)q.ai[1]=1;
                int otherSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),scene==5?origin:origin+new Vector2(5,3),new Vector2(-.7f,.2f),type==513?511:type+1,0,0,1);var other=Main.projectile[otherSlot];if(scene==13)other.owner=p.whoAmI;
                var real=new[]{q,other}.OrderBy(body=>body.whoAmI).ToArray();var shadow=real.Select(body=>(Projectile)model.GetMethod("Copy",Flags).Invoke(null,new object[]{body})).ToArray();var terrain=Terrain(a);
                for(int outer=1;outer<=100 && real.Any(body=>body.active);outer++)
                {
                    for(int index=0;index<shadow.Length;index++)if(shadow[index].active)
                    {int updates=shadow[index].extraUpdates+1;for(int sub=0;sub<updates;sub++){var args=new object[]{shadow,index,terrain,false};Require((bool)model.GetMethod("Advance",Flags).Invoke(null,args),"all-owner cloud finite scene dependencies remain known");model.GetMethod("Tail",Flags).Invoke(null,new object[]{shadow[index],args[3]});actions++;if((bool)args[3])break;}}
                    foreach(var body in real)if(body.active)body.Update(body.whoAmI);
                    for(int index=0;index<real.Length;index++)
                    {var actual=real[index];var copy=shadow[index];Require(actual.active==copy.active && actual.timeLeft==copy.timeLeft && actual.ai[0]==copy.ai[0] && actual.ai[1]==copy.ai[1] && actual.alpha==copy.alpha && actual.wet==copy.wet && actual.honeyWet==copy.honeyWet && actual.shimmerWet==copy.shimmerWet && Vector2.DistanceSquared(actual.position,copy.position)<.002f && Vector2.DistanceSquared(actual.velocity,copy.velocity)<.0001f,"native cloud full all-owner slot competition/motor agrees: "+type+"/"+scene+"/"+outer+"/"+index+" native="+actual.position+"/"+actual.velocity+"/"+actual.ai[0]+"/"+actual.ai[1]+"/"+actual.timeLeft+"/"+actual.active+" model="+copy.position+"/"+copy.velocity+"/"+copy.ai[0]+"/"+copy.ai[1]+"/"+copy.timeLeft+"/"+copy.active);}
                }
                cases++;
            }
            Console.WriteLine("PASS POISON cloud motors="+cases+" actions="+actions+" all-owner scenes");
        }
        private static void ParentMotors(object context,Assembly a,Type model)
        {
            int cases=0,actions=0;
            foreach(int scene in Enumerable.Range(0,10))
            {
                var p=Reset(context);if(scene>=1 && scene<=4)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:scene==3?1:3);}if(scene>=5 && scene<=8){Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-4));}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),scene>=5 && scene<=8?new Vector2(936,390):new Vector2(950,350),scene>=5 && scene<=8?new Vector2(24,8):new Vector2(6,0),510,0,0,p.whoAmI);var q=Main.projectile[slot];if(scene==9)q.timeLeft=3;var copy=(Projectile)model.GetMethod("Copy",Flags).Invoke(null,new object[]{q});var terrain=Terrain(a);
                for(int outer=1;outer<=45 && q.active;outer++)
                {var args=new object[]{copy,terrain,false,false,Vector2.Zero};Require((bool)model.GetMethod("AdvanceParent",Flags).Invoke(null,args),"poison bottle full movement dependencies remain known");model.GetMethod("Tail",Flags).Invoke(null,new object[]{copy,args[2]});q.Update(slot);Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.ai[0]==copy.ai[0] && Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.0001f,"native poison bottle slope/liquid/tail motor agrees: "+scene+"/"+outer+" native="+q.position+"/"+q.velocity+"/"+q.timeLeft+" model="+copy.position+"/"+copy.velocity+"/"+copy.timeLeft);actions++;}cases++;
            }
            Console.WriteLine("PASS POISON bottle motors="+cases+" actions="+actions);
        }
        private static void Receives(object context,Assembly a,Type model)
        {
            foreach(bool ambientOwn in new[]{false,true})foreach(bool ambientHits in new[]{false,true})
            {
                var p=Reset(context);Main.player[1].active=true;var target=Target(2,new Vector2(940,330));int ambientSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,0,0,511,50,0,ambientOwn?p.whoAmI:1);var ambient=Main.projectile[ambientSlot];if(!ambientHits)ambient.ai[1]=1;int sourceSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,0,0,512,50,0,p.whoAmI);var source=Main.projectile[sourceSlot];var timeline=Timeline(a,target);var receivers=Obstacles(a,p,source,timeline);var terrain=Terrain(a);
                var plan=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{source},timeline,Clock(a,timeline),terrain,receivers});bool ambientConsumes=ambientOwn && ambientHits;
                int before=target.life;ambient.Update(ambientSlot);int afterAmbient=target.life;source.Update(sourceSlot);Require((afterAmbient<before)==ambientConsumes && (target.life<afterAmbient)==!ambientConsumes,"native local ambient pass precedes source shared511 receive");
                Require(source.ai[1]==(ambientConsumes?0:1),"source's actual OnHit advances only after a qualified Damage event");
                // The ambient's actual first OnHit disables its NEXT Damage
                // call. Shared immunity blocks the source now, but expires;
                // it cannot justify a null contact for the entire horizon.
                int first=target.life<afterAmbient?1:0;
                for(int outer=2;outer<=120 && first==0 && source.active;outer++)
                {NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);ambient.Update(ambientSlot);int life=target.life;source.Update(sourceSlot);if(target.life<life)first=outer;}
                Require(first>0 && first==(plan?.Tick??0) && (ambientConsumes?first==11:first==1),"ambient shared511 permits only the real source's later eligible first Damage: own="+ambientOwn+" ambientHit="+ambientHits+" native="+first+" model="+plan?.Tick+" ambientClock="+ambient.ai[1]+" sourceClock="+source.ai[1]);
            }
            // One native Damage pass continues through higher slots even after
            // OnHit changes ai1. The next whole Damage call is then disabled.
            {
                var p=Reset(context);var lower=Target(1,new Vector2(940,330));var selected=Target(2,new Vector2(940,330));var higher=Target(3,new Vector2(940,330));int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,0,0,511,50,0,p.whoAmI);var q=Main.projectile[slot];var timeline=Timeline(a,selected);var receivers=Obstacles(a,p,q,timeline);int callbacks=0;
                Call(receivers,"BeginCandidate");Func<int,int,bool> hit=(npc,remaining)=>{callbacks++;return true;};Require((bool)Call(receivers,"PassCustomOuter",2,1,1,q.Center.X,q.Center.Y,q.width,q.height,null,false,0,true,hit),"ambient selected consume completes the current native NPC-slot pass");q.Damage();Require(lower.life<10000 && selected.life<10000 && higher.life<10000 && q.ai[1]==3 && callbacks==3 && (int)Get(receivers,"SelectedHits")==1 && (int)Get(receivers,"OtherHits")==2,"native OnHit order includes selected and later slots within the same Damage call");int life=higher.life;q.Damage();Require(higher.life==life && q.ai[1]==3,"ai1 disables the next whole Damage call, not later slots in the prior call");
            }
            Console.WriteLine("PASS POISON native ambient/source/static and same-Damage higher-slot receivers=5");
        }
        private static void Births(object context,Assembly a,Type model)
        {
            int families=0,useful=0;
            foreach(int seed in Enumerable.Range(0,8))
            {
                var p=Reset(context);Main.rand=new Terraria.Utilities.UnifiedRandom(8200+seed);var target=Target(2,new Vector2(990,325),32,50);int parentSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,0,0,510,90,0,p.whoAmI);var parent=Main.projectile[parentSlot];parent.timeLeft=1;kills=0;watchKill=true;try{parent.Update(parentSlot);}finally{watchKill=false;}
                var children=Main.projectile.Where(body=>body.active && body.owner==p.whoAmI && body.type>=511 && body.type<=513).ToArray();Require(kills==1 && children.Length>=20 && children.Length<=30 && children.All(body=>Vector2.DistanceSquared(body.Center,deathCenter)<.0001f) && target.life==10000,"real poison bottle tail Kill births native20..30 causal clouds at its center before selected damage");
                var timeline=Timeline(a,target);var terrain=Terrain(a);var receivers=Obstacles(a,p,children[0],timeline);var plan=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,children,timeline,Clock(a,timeline),terrain,receivers});int first=0;
                for(int outer=1;outer<=120;outer++){NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active)Main.projectile[slot].Update(slot);if(target.life<10000){first=outer;break;}}
                Require(first==(plan?.Tick??0),"real poison bottle causal family first useful Damage equals known finite scene: seed="+seed+" native="+first+" model="+plan?.Tick);if(first>0)useful++;families++;Console.WriteLine("POISON FAMILY seed="+seed+" children="+children.Length+" first="+first+" model="+plan?.Tick);
            }
            Require(useful>0,"native causal cloud families contain actual useful Damage, beyond scalar motor agreement");Console.WriteLine("PASS POISON actual parent deaths="+families+" useful="+useful);
        }
        private static void Workload(object context,Assembly a,Type model)
        {
            var p=Reset(context);var target=Target(2,new Vector2(1040,330));target.dontTakeDamage=true;int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,2,0,511,50,0,p.whoAmI);var q=Main.projectile[slot];var timeline=Timeline(a,target);var receivers=Obstacles(a,p,q,timeline);model.GetMethod("ResetCounters",Flags).Invoke(null,null);var plan=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{q},timeline,Clock(a,timeline),Terrain(a),receivers});
            Require(plan==null && (int)model.GetField("Captures",Flags).GetValue(null)==1 && (int)model.GetField("MotorSteps",Flags).GetValue(null)<=120 && (bool)Get(receivers,"Unchanged"),"one actual scene capture serves the full finite hard-miss family");
            // Active-pool identity alone does not detect TransformType into an
            // additional competitor. Preserve the same live slot/key boundary.
            int added=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),900,300,0,0,14,0,0,p.whoAmI);var live=Main.projectile[added];var leaseType=a.GetType("JueMingR.TerrariaHost.Combat.HostProjectileFamilyLease",true);var lease=Activator.CreateInstance(leaseType,Flags,null,new object[]{new[]{q},511,513},null);Require((bool)Get(lease,"Current"),"new non-cloud role does not change cloud qualification");live.type=512;Require(!(bool)Get(lease,"Current"),"in-place competitor qualification invalidates the scene lease");live.type=14;Main.player[p.whoAmI].active=false;Require(!(bool)Get(lease,"Current"),"owner-active retirement premise invalidates scene lease");Main.player[p.whoAmI].active=true;
            Console.WriteLine("PASS POISON workload captures=1 bounded120; transform and owner retirement lease negatives");
        }
    }
}
