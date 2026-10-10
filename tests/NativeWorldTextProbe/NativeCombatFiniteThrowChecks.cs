using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatFiniteThrowChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Vector2 scourgeKillPosition;private static int scourgeKills;
        private static void BeforeScourgeKill(Projectile __instance)
        {if(__instance.active && __instance.type==306 && __instance.owner==Main.myPlayer){scourgeKillPosition=__instance.position;scourgeKills++;}}
        internal static void Run(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;var model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostFiniteThrowAttack",true);var stateType=model.GetNestedType("State",BindingFlags.NonPublic);var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);var obstacleType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true);var snapshots=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true);bool wind=Main.windPhysics;int motors=0,steps=0,contacts=0;
            try
            {
                foreach(int type in new[]{954,979})foreach(int scene in new[]{-1,0,2,1,4,5,6,7,8})
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),type==954?3069:5147,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                    if(scene>=0 && scene<4)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene);}if(scene==4)for(int y=10;y<60;y++){Main.tile[70,y].active(true);Main.tile[70,y].type=1;}
                    if(scene>=5){for(int x=40;x<=80;x++)foreach(int y in new[]{15,45}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}for(int y=15;y<=45;y++)foreach(int x in new[]{40,80}){Main.tile[x,y].active(true);Main.tile[x,y].type=1;}Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-4));}
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),scene>=5?new Vector2(936,390):new Vector2(950,350),scene>=5?new Vector2(24,8):new Vector2(5.5f,0),type,0,0,p.whoAmI);var q=Main.projectile[slot];var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q,q.Center,q.velocity},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");int compared=0;
                    for(int outer=1;outer<=80 && q.active;outer++)
                    {var args=new object[]{q,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"finite throw complete scene dependencies");state=args[2];q.Update(slot);Require(q.active==(bool)Get(state,"Alive") && (!q.active || Vector2.DistanceSquared(q.Center,(Vector2)Get(state,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f && q.ai[0]==(float)Get(state,"Counter") && q.timeLeft==(int)Get(state,"Left")),"native finite AI002 motor/clock/death agrees: type="+type+" scene="+scene+" outer="+outer+" native="+q.Center+"/"+q.velocity+"/"+q.ai[0]+" model="+Get(state,"Center")+"/"+Get(state,"Velocity")+"/"+Get(state,"Counter"));compared++;steps++;}
                    Require(!q.active && compared>=(scene>=5?1:30) && compared<=60,"finite throw reaches real wall/clock exit without inventing lifetime");motors++;
                }
                foreach(int type in new[]{954,979})foreach(int distance in new[]{180,260})foreach(bool front in new[]{false,true})
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),type==954?3069:5147,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();var origin=new Vector2(950,350);var target=Target(2,new Vector2(origin.X+distance-16,330),32,40);NPC earlier=null;if(front)earlier=Target(1,new Vector2(1040,100),30,500);
                    var timeline=Timeline(assembly,target);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var ammo=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});Require((int)Get(ammo,"Projectile")==type,"actual Wand flame/frost pair resolves its own native type");var obstacle=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,ContentSamples.ProjectilesByType[type],timeline,0,true,false},null);var plan=(AttackContact)model.GetMethod("Solve",Flags).Invoke(null,new object[]{p,ammo,timeline,origin,0,true,terrain,obstacle,0});
                    bool impossible=front && distance==260;Require(impossible?plan==null:plan!=null,"qualified earlier NPC accelerates finite clock, with a reachable no-front control: type="+type+" distance="+distance+" front="+front+" plan="+plan?.Tick);
                    // A negative reuses the corresponding no-front launch ray;
                    // that ray really reaches the same target when front=false.
                    if(plan==null){earlier.active=false;Call(terrain,"Reset");var clean=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,ContentSamples.ProjectilesByType[type],timeline,0,true,false},null);plan=(AttackContact)model.GetMethod("Solve",Flags).Invoke(null,new object[]{p,ammo,timeline,origin,0,true,terrain,clean,0});earlier.active=true;Require(plan!=null,"clock-retirement negative has an actually reachable independent baseline");}
                    var velocity=(new Vector2(plan.AimX,plan.AimY)-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed");int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,type,50,0,p.whoAmI);var q=Main.projectile[slot];int first=0,other=0;
                    for(int outer=1;outer<=80 && q.active;outer++)
                    {NativeQuickItemChecks.BeginWorldStep();if(earlier!=null)earlier.UpdateNPC(earlier.whoAmI);target.UpdateNPC(target.whoAmI);int life=earlier?.life??0;float counter=q.ai[0];q.Update(slot);if(earlier!=null && earlier.life<life && other==0){other=outer;float motorCounter=counter+1;Require(Math.Abs(q.ai[0]-(motorCounter+(60-motorCounter)/2))<.0001f,"native prior Damage advances ai0 only after that motor step");}if(target.life<10000){first=outer;break;}}
                    Require(impossible?first==0 && other>0 && !q.active:first==plan.Tick,"finite throw native first Damage/earlier-hit clock agrees: type="+type+" distance="+distance+" front="+front+" expected="+plan.Tick+" native="+first+" earlier="+other+" ai0="+q.ai[0]);Console.WriteLine("FINITE THROW type="+type+" distance="+distance+" front="+front+" primary="+first+" prior="+other+" clock="+q.ai[0]);contacts++;
                }
                DaggerAndScourge(context,assembly,model,stateType,terrainType,obstacleType,snapshots);
                Snowballs(context,assembly,model,stateType,terrainType,obstacleType,snapshots);
            }
            finally{Main.windPhysics=wind;}
            Console.WriteLine("PASS finite AI002 flame/frost motor="+motors+" cases/"+steps+" outer; actual earlier-hit/primary clock="+contacts+" cases");
        }
        private static void Snowballs(object context,Assembly assembly,Type model,Type stateType,Type terrainType,Type obstacleType,Type snapshots)
        {
            int motors=0,actions=0,contacts=0;float windSpeed=Main.windSpeedCurrent;int net=Main.netMode;
            try
            {
                Main.netMode=0;
                foreach(bool hand in new[]{false,true})foreach(int scene in Enumerable.Range(0,13))
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),hand?949:1319,0,0);Main.windPhysics=scene==9;Main.windSpeedCurrent=.7f;
                    for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();
                    if(scene>=1 && scene<=4)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:scene==3?1:3);}
                    if(scene>=5 && scene<=8){Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-4));}
                    Vector2 origin=scene>=5 && scene<=8?new Vector2(936,390):scene==11?new Vector2(10,350):new Vector2(950,350);
                    if(scene==10){var town=Target(1,origin-new Vector2(20,20),40,40);town.friendly=town.townNPC=true;}
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,scene>=5 && scene<=8?new Vector2(24,8):scene==11?new Vector2(-7,0):new Vector2(6,-.3f),166,0,0,p.whoAmI,18,hand?1:0);var q=Main.projectile[slot];if(scene==12)q.timeLeft=3;
                    var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q,q.Center,q.velocity},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    object towns=hand?assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true).GetMethod("CaptureTownBodies",Flags).Invoke(null,new object[]{q,1L}):null;
                    for(int outer=1;outer<=80 && q.active;outer++)
                    {
                        var args=new object[]{q,towns,terrain,state};Require((bool)model.GetMethod("StepSnowball",Flags).Invoke(null,args),"snowball native liquid/slope/town dependencies remain available");state=args[3];var tail=new object[]{state};model.GetMethod("Tail",Flags).Invoke(null,tail);state=tail[0];q.Update(slot);actions++;
                        Require(q.active==(bool)Get(state,"Alive") && (!q.active || Vector2.DistanceSquared(q.Center,(Vector2)Get(state,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f && q.ai[0]==(float)Get(state,"Counter") && q.timeLeft==(int)Get(state,"Left") && q.wet==(bool)Get(state,"Wet") && q.shimmerWet==(bool)Get(state,"Shimmer")),"native snowball motor agrees with actual hand/cannon ai1: "+hand+"/"+scene+"/"+outer+" native="+q.Center+"/"+q.velocity+"/"+q.ai[0]+"/"+q.timeLeft+"/active="+q.active+"/wet="+q.wet+"/shimmer="+q.shimmerWet+" model="+Get(state,"Center")+"/"+Get(state,"Velocity")+"/"+Get(state,"Counter")+"/"+Get(state,"Left")+"/active="+Get(state,"Alive")+"/wet="+Get(state,"Wet")+"/shimmer="+Get(state,"Shimmer"));
                    }
                    if(scene==10)Require(hand?!q.active && q.timeLeft==0:q.active,"only the actual hand ai1=1 stage is killed by an overlapping friendly town body");
                    if(scene==11)Require(!q.active && q.timeLeft>0,"world-edge retirement does not call snowball Kill");
                    if(towns!=null){Require((bool)Get(towns,"Current"),"unchanged observed town body lease remains current");var added=Target(4,new Vector2(800,200),18,40);added.friendly=added.townNPC=true;Require(!(bool)Get(towns,"Current"),"new town-body eligibility invalidates the preparation lease");}
                    motors++;
                }
                // Native's final timeLeft step still runs Damage before Kill.
                // Keep that positive alongside the three-step expiry negative;
                // marking Alive=false inside Step would make this contact vanish.
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),1319,0,0);Main.windPhysics=false;
                    var target=Target(2,new Vector2(944,334),32,40);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,6,0,166,50,0,p.whoAmI);var q=Main.projectile[slot];q.timeLeft=1;
                    var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q,q.Center,q.velocity},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    var args=new object[]{q,null,terrain,state};Require((bool)model.GetMethod("StepSnowball",Flags).Invoke(null,args),"snowball final movement retains terrain");state=args[3];
                    Require((bool)Get(state,"Alive") && (int)Get(state,"Left")==0,"finite lifetime tail leaves the final native Damage stage available");
                    var center=(Vector2)Get(state,"Center");Require(new Rectangle((int)(center.X-q.width*.5f),(int)(center.Y-q.height*.5f),q.width,q.height).Intersects(target.Hitbox),"last-step control really overlaps the receiver");
                    q.Update(slot);Require(target.life<10000 && !q.active,"native final movement deals useful Damage before its lifetime Kill");
                    var tail=new object[]{state};model.GetMethod("Tail",Flags).Invoke(null,tail);state=tail[0];Require(!(bool)Get(state,"Alive") && (int)Get(state,"Left")==0,"no future receive stage survives native lifetime tail");
                }
                foreach(bool hand in new[]{false,true})foreach(bool townAtOrigin in new[]{false,true})
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),hand?949:1319,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(!hand){p.inventory[54].SetDefaults(949);p.inventory[54].stack=999;}
                    var origin=new Vector2(950,350);var target=Target(2,new Vector2(1060,330),32,40);if(townAtOrigin){var town=Target(1,origin-new Vector2(20,20),40,40);town.friendly=town.townNPC=true;}
                    var timeline=Timeline(assembly,target);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var ammo=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});Require(ammo!=null && (int)Get(ammo,"Projectile")==166,"actual Snowball/SnowballCannon resolves 166");
                    var obstacle=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,ContentSamples.ProjectilesByType[166],timeline,0,true,false},null);var plan=(AttackContact)model.GetMethod("Solve",Flags).Invoke(null,new object[]{p,ammo,timeline,origin,0,true,terrain,obstacle,0});
                    Require(hand && townAtOrigin?plan==null:plan!=null,"same 166 projectile retains the real hand-specific town pre-AI kill boundary");
                    Vector2 direction=plan==null?Vector2.UnitX:(new Vector2(plan.AimX,plan.AimY)-origin).SafeNormalize(Vector2.UnitX);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,direction*(float)Get(ammo,"Speed"),166,50,0,p.whoAmI,0,hand?1:0);var q=Main.projectile[slot];int first=0;
                    for(int outer=1;outer<=80 && q.active;outer++){NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);q.Update(slot);if(target.life<10000){first=outer;break;}}
                    Require(first==(plan?.Tick??0),"actual snowball primary Damage equals contact or genuine pre-AI kill: hand="+hand+" town="+townAtOrigin+" native="+first+" model="+plan?.Tick);contacts++;
                }
                Console.WriteLine("PASS SNOWBALL 166 motor="+motors+" actions="+actions+" actual hand/cannon/preceding-town contacts="+contacts+"; allied-player multiplayer stage not yet modeled");
            }
            finally{Main.windSpeedCurrent=windSpeed;Main.netMode=net;}
        }
        private static void DaggerAndScourge(object context,Assembly assembly,Type model,Type stateType,Type terrainType,Type obstacleType,Type snapshots)
        {
            var random=Main.rand;var starts=new System.Collections.Generic.HashSet<int>();int actions=0;var audit=new Harmony("JueMingR.Tests.FiniteScourgeBirth");audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatFiniteThrowChecks),"BeforeScourgeKill"));
            try
            {
                // The future random onset is an oracle input ONLY after native
                // has executed it. Replaying each recorded realized path does
                // not call RNG or pretend an unborn representative is exact.
                for(int seed=0;seed<32;seed++)
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),517,0,0);Main.windPhysics=false;Main.rand=new Terraria.Utilities.UnifiedRandom(9400+seed);for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var n in Main.npc)n.active=false;
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,6,-.3f,93,0,0,p.whoAmI);var q=Main.projectile[slot];var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q,q.Center,q.velocity},null);var centers=new Vector2[40];var velocities=new Vector2[40];var counters=new float[40];int onset=0;
                    for(int k=0;k<40;k++){q.Update(slot);Require(q.active,"native dagger remains alive in clear finite oracle");centers[k]=q.Center;velocities[k]=q.velocity;counters[k]=q.ai[0];if(onset==0 && q.ai[0]==40)onset=k+1;}
                    Require(onset>=28 && onset<=33,"native MagicDagger first gravity lies in its complete six-step random window");starts.Add(onset);Set(state,"GravityThreshold",onset);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    for(int k=0;k<40;k++){var args=new object[]{q,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"dagger realized gravity path retains finite terrain");state=args[2];Require(Vector2.DistanceSquared(centers[k],(Vector2)Get(state,"Center"))<.002f && Vector2.DistanceSquared(velocities[k],(Vector2)Get(state,"Velocity"))<.0001f && counters[k]==(float)Get(state,"Counter"),"native realized dagger random onset and fixed40 tail agree: seed="+seed+" onset="+onset+" step="+(k+1));Require((bool)Get(state,"Representative")==((k+1)>=28),"dagger confidence marks entry into its future random window, never its deterministic prefix");actions++;}
                    Console.WriteLine("FINITE DAGGER realized seed="+seed+" gravityStart="+onset);
                }
                foreach(float counter in new[]{0f,40f})
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),517,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var n in Main.npc)n.active=false;int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),950,350,6,-.3f,93,0,0,p.whoAmI,counter);var q=Main.projectile[slot];var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q,q.Center,q.velocity},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    for(int k=0;k<20;k++){var args=new object[]{q,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"known dagger prefix/tail has complete terrain");state=args[2];q.Update(slot);Require(!(bool)Get(state,"Representative") && q.active && q.ai[0]==(float)Get(state,"Counter") && Vector2.DistanceSquared(q.Center,(Vector2)Get(state,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f,"captured dagger deterministic prefix or post-random40 tail is exact: "+counter+"/"+k);actions++;}
                }
                foreach(int scene in Enumerable.Range(0,8))
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),1571,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var n in Main.npc)n.active=false;
                    if(scene>=1 && scene<=3)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:1);}if(scene>=4){Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-3));}
                    Vector2 origin=scene>=4?new Vector2(936,390):new Vector2(950,350),velocity=scene>=4?new Vector2(24,8):new Vector2(6,-.3f);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,306,50,0,p.whoAmI,21);var q=Main.projectile[slot];var state=Activator.CreateInstance(stateType,Flags,null,new object[]{q,q.Center,q.velocity},null);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");
                    scourgeKills=0;for(int outer=1;outer<=35 && q.active;outer++){int updates=q.extraUpdates+1;for(int sub=0;sub<updates && (bool)Get(state,"Alive");sub++){var args=new object[]{q,terrain,state};Require((bool)model.GetMethod("Step",Flags).Invoke(null,args),"Scourge primary full tile/liquid stage retains terrain");state=args[2];actions++;}q.Update(slot);Require(q.active==(bool)Get(state,"Alive") && (!q.active || q.ai[0]==21 && q.ai[0]==(float)Get(state,"Counter") && Vector2.DistanceSquared(q.Center,(Vector2)Get(state,"Center"))<.002f && Vector2.DistanceSquared(q.velocity,(Vector2)Get(state,"Velocity"))<.0001f),"native Scourge straight primary has no twenty-step gravity or ai0 advancement: "+scene+"/"+outer);}
                    // This parent ignores water and is dry. Native Kill births
                    // children at its entry body top-left, BEFORE the remaining
                    // HandleMovement UpdatePosition moves the dead parent again.
                    if(!q.active){var children=Main.projectile.Where(a=>a.active && a.owner==p.whoAmI && a.type==307).ToArray();Require(q.ignoreWater && !q.wet && scourgeKills==1 && children.Length>=2 && children.Length<=15 && children.All(a=>a.damage==37 && a.aiStyle==36 && !a.friendly && Vector2.DistanceSquared(a.Center,scourgeKillPosition)<.002f),"native Scourge death really creates its random separate initially-nonfriendly AI36 role at actual Kill-entry top-left");Console.WriteLine("FINITE SCOURGE death scene="+scene+" children="+children.Length+" childStage=BIRTH_ONLY_IN_THIS_SCOPE");}
                }
                foreach(int weapon in new[]{517,1571})foreach(int distance in new[]{180,240})
                {
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var n in Main.npc)n.active=false;var origin=new Vector2(950,350);var target=Target(2,new Vector2(origin.X+distance,330),32,40);var timeline=Timeline(assembly,target);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var ammo=snapshots.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});int type=(int)Get(ammo,"Projectile");Require(type==(weapon==517?93:306),"native finite hand stage resolves correct primary type");var obstacle=Activator.CreateInstance(obstacleType,Flags,null,new object[]{p,ContentSamples.ProjectilesByType[type],timeline,0,true,false},null);var contact=(AttackContact)model.GetMethod("Solve",Flags).Invoke(null,new object[]{p,ammo,timeline,origin,0,true,terrain,obstacle,0});Require(contact!=null && contact.Confidence==AttackConfidence.Conditional,"near deterministic dagger/Scourge primary retains useful finite contact");var v=(new Vector2(contact.AimX,contact.AimY)-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed");int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,v,type,50,0,p.whoAmI);var q=Main.projectile[slot];int first=0;for(int outer=1;outer<=60 && q.active;outer++){NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);q.Update(slot);if(target.life<10000){first=outer;break;}}Require(first>0 && first==contact.Tick,"actual finite primary first Damage agrees before random gravity or child stage: "+weapon+"/"+distance+" native="+first+" model="+contact.Tick);Console.WriteLine("FINITE HAND weapon="+weapon+" distance="+distance+" first="+first);
                }
                Require(starts.Count>=2,"native random dagger denominator includes distinct realized onset paths");Console.WriteLine("PASS finite dagger/Scourge primary native actions="+actions+" randomStarts="+string.Join(",",starts.OrderBy(x=>x))+"; child motor/family Damage is validated by the separate Scourge scope");
            }
            finally{audit.UnpatchAll(audit.Id);Main.rand=random;}
        }
        private static NPC Target(int slot,Vector2 position,int width,int height)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.position=position;n.width=width;n.height=height;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=true;n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;}
        private static NpcTrajectory Timeline(Assembly assembly,NPC n)
        {var selection=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true);var key=(NpcIdentity)selection.GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,0,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
    }
}
