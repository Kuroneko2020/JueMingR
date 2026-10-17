using System;
using System.Collections.Generic;
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
    internal static class NativeCombatScourgeChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static int parentSlot;private static Vector2 birthPosition;private static int kills;
        private static void BeforeKill(Projectile __instance)
        {if(__instance.active && __instance.type==306 && __instance.whoAmI==parentSlot){birthPosition=__instance.position;kills++;}}
        internal static void Run(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;
            var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostScourgeAttack",true);
            var stateType=model.GetNestedType("State",BindingFlags.NonPublic);
            var targetsType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true);
            var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            var obstaclesType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true);
            var clockType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);
            var phaseType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);
            var random=Main.rand;bool wind=Main.windPhysics;var audit=new Harmony("JueMingR.Tests.ScourgeStages");
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatScourgeChecks),"BeforeKill"));
            try
            {
                Motors(context,model,stateType,targetsType,terrainType,assembly);
                WorldEdges(context,model,stateType,targetsType,terrainType,assembly);
                Targets(context,model,stateType,targetsType,terrainType,assembly);
                ReceiveAndTail(context,model,terrainType,obstaclesType,clockType,phaseType,assembly);
                BirthAndDamage(context,model,terrainType,obstaclesType,clockType,phaseType,assembly);
            }
            finally{audit.UnpatchAll(audit.Id);Main.rand=random;Main.windPhysics=wind;}
        }
        private static Player Reset(object context)
        {
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),1571,0,0);
            Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
            // A BeforeNpc timeline carries the preceding completed sample.
            // Dedicated scope can start at GUC zero; create that real epoch.
            if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();
            foreach(var n in Main.npc)n.active=false;return p;
        }
        private static void WorldEdges(object context,Type model,Type stateType,Type targetsType,Type terrainType,Assembly assembly)
        {
            int cases=0;
            foreach(int type in new[]{306,307})foreach(int edge in Enumerable.Range(0,4))
            {
                var p=Reset(context);var target=Target(2,new Vector2(900,500),32,40);target.dontTakeDamage=true;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,0,0,type,0,0,p.whoAmI);var q=Main.projectile[slot];
                q.position=edge==0?new Vector2(Main.leftWorld+1,350):edge==1?new Vector2(Main.rightWorld-q.width-1,350):edge==2?new Vector2(950,Main.topWorld+1):new Vector2(950,Main.bottomWorld-q.height-1);
                q.velocity=edge==0?new Vector2(-7,0):edge==1?new Vector2(7,0):edge==2?new Vector2(0,-7):new Vector2(0,7);
                var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);var targets=Capture(targetsType,q);var terrain=Terrain(terrainType);var timeline=Timeline(assembly,target);
                for(int outer=1;outer<=4 && q.active;outer++)
                {
                    int updates=(int)Get(state,"Extra")+1;
                    for(int sub=0;sub<updates;sub++)
                    {var args=new object[]{q,targets,timeline,outer,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"native world edge retains its legal clipped interval before retirement");state=args[5];var tail=new object[]{q,state};model.GetMethod("Tail",Flags).Invoke(null,tail);state=tail[1];if((bool)Get(state,"StoppedAfterMovement"))break;}
                    q.Update(slot);
                    Require(q.active==(bool)Get(state,"Alive") && q.timeLeft==(int)Get(state,"Left") && q.position==(Vector2)Get(state,"Position") && q.velocity==(Vector2)Get(state,"Velocity"),"pre-AI actual edge retirement preserves body and Left: "+type+"/"+edge);
                }
                Require(!q.active && !(bool)Get(state,"Killed") && q.timeLeft>0,"world edge is native active retirement, never Kill or fabricated child spawn");
                Require(!Main.projectile.Any(child=>child.active && child.type==307),"306 world retirement creates no native 307 family");cases++;
            }
            Console.WriteLine("PASS SCOURGE world retirement/clipped last interval cases="+cases);
        }
        private static object Capture(Type type,Projectile shot)
        {return type.GetMethod("Capture",Flags).Invoke(null,new object[]{shot,1L});}
        private static object Terrain(Type type)
        {var t=Activator.CreateInstance(type,true);Call(t,"Reset");return t;}
        private static void Motors(object context,Type model,Type stateType,Type targetsType,Type terrainType,Assembly assembly)
        {
            int cases=0,actions=0,bounces=0,deaths=0;
            // Native 307 tolerates water/lava, unlike the other AI036 types.
            // Shimmer covers upward near-surface movement, deep Kill and sticky
            // simultaneous honey+shimmer flags. Slopes include speed>body cap.
            foreach(int scene in Enumerable.Range(0,12))
            {
                var p=Reset(context);Vector2 origin=new Vector2(950,350),velocity=new Vector2(5,-.3f);
                // Keep every complete native path within known query cells.
                // This closed arena also exercises eventual sixth collision.
                for(int x=40;x<=80;x++)foreach(int y in new[]{15,45}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                for(int y=15;y<=45;y++)foreach(int x in new[]{40,80}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                if(scene>=1 && scene<=3)for(int x=40;x<110;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:1);}
                if(scene==4 || scene==5 || scene==6)
                {
                    for(int x=40;x<110;x++)for(int y=22;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(3);}
                    origin=scene==5?new Vector2(950,390):new Vector2(950,355);velocity=new Vector2(5,scene==6?2:-.3f);
                }
                if(scene>=7 && scene<=10)
                {
                    for(int x=40;x<=80;x++)foreach(int y in new[]{15,45}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                    for(int y=15;y<=45;y++)foreach(int x in new[]{40,80}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                    Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-6));origin=new Vector2(936,390);velocity=new Vector2(24,8);
                }
                if(scene==11)
                {
                    for(int x=55;x<=65;x++)foreach(int y in new[]{20,30}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                    for(int y=20;y<=30;y++)foreach(int x in new[]{55,65}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}
                    origin=new Vector2(960,390);velocity=new Vector2(7,5);
                }
                var target=Target(2,new Vector2(1100,250),32,40);target.dontTakeDamage=true;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,307,0,0,p.whoAmI);var q=Main.projectile[slot];
                if(scene==4)q.honeyWet=true; // Native shimmer wins this sticky mix.
                var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);var targets=Capture(targetsType,q);var timeline=Timeline(assembly,target);var terrain=Terrain(terrainType);float priorBounce=q.ai[1];
                for(int outer=1;outer<=120 && q.active;outer++)
                {
                    int updates=(int)Get(state,"Extra")+1;
                    for(int sub=0;sub<updates;sub++)
                    {
                        var args=new object[]{q,targets,timeline,outer,terrain,state};
                        Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"307 finite full motor retains actual scene dependencies: "+scene+"/"+outer+"/"+sub);
                        state=args[5];var tail=new object[]{q,state};model.GetMethod("Tail",Flags).Invoke(null,tail);state=tail[1];actions++;
                        if((bool)Get(state,"StoppedAfterMovement"))break;
                    }
                    q.Update(slot);
                    Require(q.active==(bool)Get(state,"Alive") && Vector2.DistanceSquared(q.Center,(Vector2)Get(state,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f && q.ai[0]==(float)Get(state,"Counter") && q.ai[1]==(float)Get(state,"Bounces") && q.alpha==(int)Get(state,"Alpha") && q.extraUpdates==(int)Get(state,"Extra") && q.friendly==(bool)Get(state,"Friendly") && q.timeLeft==(int)Get(state,"Left"),
                        "native complete 307 motor/alpha/clock/friendly/collision/lifetime: scene="+scene+" outer="+outer+" native="+q.Center+"/"+q.velocity+"/"+q.ai[0]+","+q.ai[1]+"/"+q.timeLeft+" model="+Get(state,"Center")+"/"+Get(state,"Velocity")+"/"+Get(state,"Counter")+","+Get(state,"Bounces")+"/"+Get(state,"Left"));
                    Require(!(bool)Get(state,"Friendly"),"unacquired native 307 never becomes damaging merely because alpha reached zero");
                    if(q.ai[1]>priorBounce)bounces++;priorBounce=q.ai[1];
                }
                if(scene==5 || scene==11){Require(!q.active,"native deep shimmer/sixth tile collision really retires 307");deaths++;}
                if(scene==11)Require(q.ai[1]==5,"exactly five live tile responses precede the default sixth-collision Kill");
                cases++;
            }
            Require(bounces>=5 && deaths==2,"complete 307 motor includes five actual bounces and two causal exit types");
            Console.WriteLine("SCOURGE MOTOR cases="+cases+" actions="+actions+" bounces="+bounces+" exits="+deaths);
        }
        private static void Targets(object context,Type model,Type stateType,Type targetsType,Type terrainType,Assembly assembly)
        {
            int cases=0;
            foreach(int scene in Enumerable.Range(0,5))
            {
                var p=Reset(context);var origin=new Vector2(950,350);
                var selected=Target(2,new Vector2(scene==0?1734:1084,330),32,40);
                NPC earlier=null;if(scene==1)earlier=Target(1,selected.position,32,40);
                if(scene==2)for(int y=10;y<40;y++){Main.tile[64,y].active(true);Main.tile[64,y].type=1;}
                if(scene==3)selected.dontTakeDamage=true;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,Vector2.Zero,307,0,0,p.whoAmI,30);var q=Main.projectile[slot];q.extraUpdates=0;q.alpha=0;
                var timeline=Timeline(assembly,selected);var terrain=Terrain(terrainType);var targets=Capture(targetsType,q);var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q},null);
                var args=new object[]{q,targets,timeline,1,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"307 chase uses original full-body CanHit and finite target snapshot");state=args[5];var tail=new object[]{q,state};model.GetMethod("Tail",Flags).Invoke(null,tail);state=tail[1];q.Update(slot);
                int expected=scene==1?1:scene==4?2:-1;
                Require((int)Get(state,"TargetSlot")==expected && q.friendly==(expected>=0) && q.friendly==(bool)Get(state,"Friendly") && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f,
                    "native 307 strict Manhattan800/slot tie/LOS/invulnerability/acquire: "+scene+" target="+Get(state,"TargetSlot")+" friendly="+q.friendly);
                Require((bool)Get(targets,"Current"),"unchanged chase permission lease is valid");
                if(scene==4)
                {
                    selected.active=false;Require(!(bool)Get(targets,"Current"),"chase target qualification removal retires prepared receipt");targets=Capture(targetsType,q);
                    args=new object[]{q,targets,timeline,2,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"captured lost-target continuation retains actual fallback motor");state=args[5];q.Update(slot);
                    Require(q.friendly && (bool)Get(state,"Friendly") && (int)Get(state,"TargetSlot")==-1,"native acquired 307 remains friendly after losing all chase targets");
                    selected.active=true;Require(!(bool)Get(targets,"Current"),"newly qualifying chase target also retires receipt");
                    targets=Capture(targetsType,q);selected.netOffset.X=1;Require(!(bool)Get(targets,"Current"),"chase snapshot preserves presentation/body identity dependency too");
                }
                cases++;
            }
            Console.WriteLine("SCOURGE CHASE actual eligibility/LOS/Manhattan/tie/sticky tests="+cases);
        }
        private static void BirthAndDamage(object context,Type model,Type terrainType,Type obstaclesType,Type clockType,Type phaseType,Assembly assembly)
        {
            int cases=0,positive=0,negative=0,lower=0,higher=0;
            foreach(bool lowerSlots in new[]{false,true})foreach(int seed in Enumerable.Range(0,8))
            {
                var p=Reset(context);Main.rand=new Terraria.Utilities.UnifiedRandom(14060+seed);
                var target=Target(2,new Vector2(1060,310),40,70);bool rejected=seed==7;if(rejected)target.dontTakeDamage=true;
                if(lowerSlots)
                {
                    for(int i=0;i<7;i++)
                    {int fill=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),600,350,0,0,14,0,0,p.whoAmI);var f=Main.projectile[fill];f.friendly=false;f.tileCollide=false;}
                }
                Main.tile[60,22].active(true);Main.tile[60,22].type=1;
                parentSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),936,355,8,0,306,50,0,p.whoAmI);
                if(lowerSlots){Main.projectile[1].active=false;Main.projectile[2].active=false;}
                kills=0;
                for(int outer=0;outer<8 && Main.projectile[parentSlot].active;outer++)
                {
                    NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);
                    for(int i=0;i<1000;i++)if(Main.projectile[i].active)Main.projectile[i].Update(i);
                }
                var children=Main.projectile.Where(q=>q.active && q.type==307 && q.owner==p.whoAmI).ToArray();
                Require(kills==1 && children.Length>=2 && children.Length<=15,"actual 306 collision creates its complete distinct native child family");
                foreach(var child in children)
                {
                    if(child.whoAmI<parentSlot){Require(child.ai[0]==0 && Vector2.DistanceSquared(child.Center,birthPosition)<.002f,"native lower-slot child is born at Kill entry and waits this outer");lower++;}
                    else{Require(child.ai[0]==4 && child.timeLeft==596,"native higher-slot child executes all four newborn substeps in the parent's outer");higher++;}
                }
                Require(target.life==10000,"initial nonfriendly children have not caused target Damage before acquisition");
                var timeline=Timeline(assembly,target);var terrain=Terrain(terrainType);var policy=new Projectile();policy.SetDefaults(307);policy.owner=p.whoAmI;policy.friendly=true;
                var obstacles=Activator.CreateInstance(obstaclesType,Flags,null,new object[]{p,policy,timeline,0,true,false},null);
                var phase=Enum.Parse(phaseType,"BeforeNpc");var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,phase},null);
                var contact=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,children,timeline,clock,terrain,obstacles});
                if(contact==null && model.GetField("FailedSlot",Flags)==null)Console.WriteLine("SCOURGE FAMILY STOP lower="+lowerSlots+" seed="+seed+" diagnosticFields=NOT_AVAILABLE_RELEASE");
                else if(contact==null)Console.WriteLine("SCOURGE FAMILY STOP lower="+lowerSlots+" seed="+seed+" slot="+model.GetField("FailedSlot",Flags).GetValue(null)+" tick="+model.GetField("FailedTick",Flags).GetValue(null)+" outer="+model.GetField("FailedOuter",Flags).GetValue(null)+" position="+model.GetField("FailedPosition",Flags).GetValue(null)+" stage="+model.GetField("FailedStage",Flags).GetValue(null)+" children="+string.Join(";",children.Select(q=>q.whoAmI+":"+q.position+"/"+q.velocity+"/"+q.ai[0]+"/"+q.friendly)));
                int first=0;
                for(int outer=1;outer<=120 && children.Any(q=>q.active);outer++)
                {
                    NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);
                    for(int i=0;i<1000;i++)if(Main.projectile[i].active)Main.projectile[i].Update(i);
                    if(target.life<10000){first=outer;break;}
                }
                Require(rejected?contact==null && first==0:contact==null?first==0:first==contact.Tick && contact.Confidence==AttackConfidence.Conditional,
                    "captured actual child family predicts useful first Damage or genuine negative: lower="+lowerSlots+" seed="+seed+" native="+first+" plan="+contact?.Tick);
                if(first>0)positive++;else negative++;cases++;
                Console.WriteLine("SCOURGE ACTUAL CHILD lower="+lowerSlots+" seed="+seed+" count="+children.Length+" first="+first+" contact="+contact?.Tick);
            }
            Require(positive>0 && negative>=2 && lower>0 && higher>0,"307 actual primary->child validation includes useful Damage, rejected receivers and both native slot epochs");
            Console.WriteLine("PASS SCOURGE actual family cases="+cases+" Damage="+positive+" negative="+negative+" lowerChildren="+lower+" higherChildren="+higher+"; unborn child RNG remains representative");
        }
        private static void ReceiveAndTail(object context,Type model,Type terrainType,Type obstaclesType,Type clockType,Type phaseType,Assembly assembly)
        {
            foreach(bool front in new[]{false,true})
            {
                var p=Reset(context);var target=Target(2,new Vector2(934,330),32,40);target.chaseable=false;target.immune[p.whoAmI]=100;
                NPC other=null;if(front){other=Target(1,target.position,32,40);other.chaseable=false;}
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,0,0,307,50,0,p.whoAmI);var q=Main.projectile[slot];
                q.alpha=0;q.extraUpdates=0;q.friendly=true;q.timeLeft=1;
                var timeline=Timeline(assembly,target);var terrain=Terrain(terrainType);var policy=new Projectile();policy.SetDefaults(307);policy.owner=p.whoAmI;policy.friendly=true;
                var obstacles=Activator.CreateInstance(obstaclesType,Flags,null,new object[]{p,policy,timeline,0,true,false},null);
                var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{timeline,Enum.Parse(phaseType,"BeforeNpc")},null);
                var contact=(AttackContact)model.GetMethod("KnownFamily",Flags).Invoke(null,new object[]{p,new[]{q},timeline,clock,terrain,obstacles});
                NativeQuickItemChecks.BeginWorldStep();if(other!=null)other.UpdateNPC(1);target.UpdateNPC(2);q.Update(slot);
                Require(front?contact==null && target.life==10000 && other.life<10000:contact!=null && contact.Tick==1 && target.life<10000,
                    "native 307 Damage precedes final lifetime decrement; penetrate1 ignores prior owner immunity and actual earlier NPC consumes it: "+front+" contact="+contact?.Tick+" targetLife="+target.life+" earlierLife="+other?.life);
                Require(!q.active,"native one-penetration/timeLeft1 attack really retires after its permitted Damage");
            }
        }
        private static NPC Target(int slot,Vector2 position,int width,int height)
        {
            var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.position=position;n.width=width;n.height=height;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=true;n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);return n;
        }
        private static NpcTrajectory Timeline(Assembly assembly,NPC n)
        {
            var selection=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true);var key=(NpcIdentity)selection.GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});
            var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=true};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
    }
}
