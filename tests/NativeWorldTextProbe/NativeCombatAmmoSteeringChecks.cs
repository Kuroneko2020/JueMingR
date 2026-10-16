using System;
using System.Collections.Generic;
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
    internal static class NativeCombatAmmoSteeringChecks
    {
        private const BindingFlags Flags=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private static Assembly host;private static Type model;private static int depth,rng,first,currentTick;private static Projectile receiving;
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void Random(){if(depth>0)rng++;}
        private static void Damage(Projectile __instance){receiving=__instance;}
        private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__instance.whoAmI==2 && __result>0 && receiving!=null && first==0)first=currentTick;}
        private static Player Scene(object context,int weapon=96,int type=207)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);p.position=new Vector2(850,328);p.velocity=Vector2.Zero;p.ResetEffects();p.ResetMeleeHitCooldowns();Main.windPhysics=false;Main.ProjectileUpdateLoopIndex=-1;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;
            for(int x=5;x<Main.maxTilesX-5;x++)for(int y=5;y<Main.maxTilesY-5;y++)Main.tile[x,y].ClearEverything();Main.tileSolid[1]=true;
            var ammo=ContentSamples.ItemsByType.Values.First(i=>i.ammo>0 && i.shoot==type);p.inventory[54].SetDefaults(ammo.type);p.inventory[54].stack=999;first=currentTick=0;return p;
        }
        private static NPC Target(Vector2 center,int slot=2,int side=30,bool blocked=false)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.active=true;n.whoAmI=slot;n.width=n.height=side;n.Center=center;n.life=n.lifeMax=100000;n.defense=0;n.knockBackResist=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.dontTakeDamage=blocked;Array.Clear(n.immune,0,n.immune.Length);return n;
        }
        private static Projectile Spawn(Player p,int type,Vector2 center,Vector2 velocity,int damage=60)
        {return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,type,damage,0,p.whoAmI)];}
        private static NpcTrajectory Path(NPC n)
        {
            var key=(NpcIdentity)Static(host.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,Active=true,CanReceive=!n.dontTakeDamage,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length);
        }
        private static object Terrain(){var t=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Root(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,false,true},null);}
        private static object Snapshot(Player p,object root,int type){return Static(model,"CaptureForSolve",p,root,type);}
        private static Projectile Copy(Projectile q){return (Projectile)Static(host.GetType("JueMingR.TerrariaHost.Combat.HostNoteAttack",true),"Copy",q);}
        private static AttackContact Known(Player p,Projectile q,NPC n,out object root)
        {
            var path=Path(n);root=Root(p,q,path);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);
            depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return (AttackContact)Static(model,"KnownFamily",p,new[]{q},path,clock,Terrain(),root);}finally{depth--;}
        }
        private static void Advance(int limit=90)
        {
            for(currentTick=1;currentTick<=limit && first==0;currentTick++)
            {
                NativeQuickItemChecks.BeginWorldStep();foreach(var n in Main.npc)if(n.active)n.UpdateNPC(n.whoAmI);
                for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}
            }
        }
        internal static void Run(object context)
        {
            host=Get(context,"Combat").GetType().Assembly;model=host.GetType("JueMingR.TerrariaHost.Combat.HostAmmoSteeringAttack",true);var audit=new Harmony("JueMingR.Tests.AmmoSteering");rng=depth=0;
            foreach(var method in typeof(Terraria.Utilities.UnifiedRandom).GetMethods(Flags).Where(m=>m.DeclaringType==typeof(Terraria.Utilities.UnifiedRandom) && (m.Name=="Next" || m.Name=="NextDouble")))audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatAmmoSteeringChecks),nameof(Random)));
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAmmoSteeringChecks),nameof(Damage)),finalizer:new HarmonyMethod(typeof(NativeCombatAmmoSteeringChecks),nameof(DamageDone)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatAmmoSteeringChecks),nameof(Strike)));
                        try
            {
                string selector=Environment.GetEnvironmentVariable("JMR_AMMO_STEERING_LEAF");var failures=new List<Exception>();
                Action<string,Action> run=(name,body)=>{if(!string.IsNullOrEmpty(selector) && selector!=name)return;try{body();}catch(Exception error){failures.Add(error);Console.WriteLine("FAIL STEERING GROUP "+name+": "+error);}};
                Require(string.IsNullOrEmpty(selector) || new[]{"ai","motor","tile","receive","hands","harpy","consumer","harpy-flight","harpy-adapter","confidence"}.Contains(selector),"known finite steering test selector");
                run("ai",()=>Ai(context));run("motor",()=>Motors(context));run("tile",()=>Tiles(context));run("receive",()=>Receives(context));run("hands",()=>Hands(context));run("harpy",()=>Harpy(context));run("consumer",()=>Consumers(context));run("harpy-flight",()=>HarpyFlight(context));
                run("harpy-adapter",()=>HarpyAdapter(context));
                run("confidence",()=>Confidence(context));
                if(failures.Count>0)throw new AggregateException("original steering groups failed",failures);
                Require(rng==0,"all private ammo steering preparation/motors consume zero live RNG");Console.WriteLine("PASS AMMO-STEERING scope="+(selector??"all")+" RNG0");
            }
            finally{depth=0;receiving=null;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);}
        }
        private static void Ai(object context)
        {
            int cases=0,actions=0;
            foreach(int type in new[]{36,207,225,285})foreach(int age in new[]{0,14,15})foreach(var v in new[]{new Vector2(6,-1),new Vector2(-2,20),Vector2.Zero})
            {
                var p=Scene(context,type==225?39:96,type);var n=Target(new Vector2(1150,360),side:31);var q=Spawn(p,type,new Vector2(1000,350),v);q.ai[0]=age;var shadow=Copy(q);var path=Path(n);var root=Root(p,q,path);var capture=Snapshot(p,root,type);var terrain=Terrain();
                for(int action=0;action<45;action++)
                {
                    depth++;try{Require((int)Static(model,"Ai",shadow,Get(capture,"Targets"),path,1,terrain)==0,"native finite steering AI is supported");}finally{depth--;}q.AI();
                    Require(Vector2.DistanceSquared(q.velocity,shadow.velocity)<.00001f && q.ai.SequenceEqual(shadow.ai) && q.localAI.SequenceEqual(shadow.localAI) && q.alpha==shadow.alpha && Math.Abs(q.rotation-shadow.rotation)<.00001f,"original AI001 clock/locked speed/alpha/rotation exact type="+type+" age="+age+" action="+action+" native="+q.velocity+" model="+shadow.velocity);
                    q.position+=q.velocity;shadow.position+=shadow.velocity;actions++;
                }
                cases++;
            }
            foreach(int kind in new[]{0,1,2,3,4})
            {
                var p=Scene(context);var n=Target(new Vector2(kind==2?2050:1150,350),blocked:kind==3);var q=Spawn(p,207,new Vector2(1000,350),new Vector2(10,0));q.ai[1]=kind==0?0:kind==4?4:3;if(kind==1)for(int y=5;y<75;y++)NativeToolsChecks.Tile(68,y,1);
                var shadow=Copy(q);var path=Path(n);var capture=Snapshot(p,Root(p,q,path),207);depth++;try{Static(model,"Ai",shadow,Get(capture,"Targets"),path,1,Terrain());}finally{depth--;}q.AI();Require(q.ai.SequenceEqual(shadow.ai) && q.velocity==shadow.velocity,"207 keeps locked target without LOS, retains distant lock, clears invalid/dontTakeDamage exactly kind="+kind);cases++;
            }
            Console.WriteLine("STEERING AI cases="+cases+" actions="+actions+" plus5lockEdges");
        }
        private static void Motors(object context)
        {
            int cases=0,actions=0;
            foreach(int type in new[]{36,207,225,285})foreach(int kind in new[]{0,1,2,3,4,5,6,7})
            {
                var p=Scene(context,type==225?39:96,type);var n=Target(new Vector2(1250,420));
                if(kind>=1 && kind<=3)for(int x=55;x<100;x++)for(int y=15;y<65;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(kind==1?0:kind==2?2:3);}
                if(kind==4 || kind==6 || kind==7)for(int y=5;y<75;y++)NativeToolsChecks.Tile(70,y,1);if(kind==5)for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,28,1);
                if(kind>=6)n.Center=new Vector2(1050,450);var q=Spawn(p,type,new Vector2(kind>=6?1115:1000,350),new Vector2(8,3),0);if(kind==7)q.localNPCImmunity[2]=-1;var shadow=Copy(q);var path=Path(n);var capture=Snapshot(p,Root(p,q,path),type);var terrain=Terrain();
                for(int action=0;action<35 && q.active;action++)
                {
                    int updates=shadow.extraUpdates+1;for(int sub=0;sub<updates && shadow.active;sub++)
                    {var args=new object[]{shadow,Get(capture,"Targets"),path,Math.Min(action+1,120),terrain,false,false,null};depth++;bool advanced;try{advanced=(bool)Static(model,"Step",args);}finally{depth--;}Require(advanced,"steering original environment and tile result are known");if((bool)args[5]){shadow.active=false;shadow.timeLeft=0;}else if(shadow.active && --shadow.timeLeft<=0){shadow.active=false;shadow.timeLeft=0;}}
                    NativeQuickItemChecks.BeginWorldStep();q.Update(q.whoAmI);actions++;
                    Require(q.active==shadow.active && (!q.active || Vector2.DistanceSquared(q.position,shadow.position)<.002f && Vector2.DistanceSquared(q.velocity,shadow.velocity)<.0001f && q.ai.SequenceEqual(shadow.ai) && q.penetrate==shadow.penetrate && q.damage==shadow.damage && q.timeLeft==shadow.timeLeft),"native complete dry/water/honey/shimmer/wall/floor motor type="+type+" kind="+kind+" action="+action+" actual="+q.position+"/"+q.velocity+" model="+shadow.position+"/"+shadow.velocity);
                }
                cases++;
            }
            Console.WriteLine("STEERING whole motors="+cases+" actions="+actions);
        }
        private static void Tiles(object context)
        {
            int cases=0;
            foreach(int type in new[]{225,285,36})foreach(bool target in new[]{false,true})foreach(bool local in new[]{false,true})
            {
                var p=Scene(context,type==225?39:96,type);var n=Target(new Vector2(1150,450));if(!target)n.dontTakeDamage=true;var q=Spawn(p,type,new Vector2(1000,350),new Vector2(8,3),61);if(local)q.localNPCImmunity[2]=-1;var path=Path(n);var capture=Snapshot(p,Root(p,q,path),type);var shadow=Copy(q);
                var before=new Vector2(8,3);q.velocity=shadow.velocity=new Vector2(0,3);depth++;int result;try{result=(int)Static(model,"Tile",shadow,before,Get(capture,"Targets"),path,1,Terrain());}finally{depth--;}
                if(type==36)Require(result==0 && shadow.penetrate==1 && shadow.velocity==new Vector2(-8,3),"Meteor spends penetration only once and reflects changed axes");
                else
                {Require(result==0 && shadow.ai[1]==1 && shadow.damage==(int)(61*.66f),"first tile alone applies exact native .66 damage and bounce clock");if(target && !local){float height=type==225?MathHelper.Lerp(n.height*.1f,n.height*.5f,Utils.GetLerpValue(0,300,Vector2.Distance(q.Center,n.Center))):0;var aim=(n.Center-new Vector2(0,height)-q.Center).SafeNormalize(-Vector2.UnitY);Require(Vector2.DistanceSquared(shadow.velocity,aim*before.Length())<.00001f,"native nearest LOS retarget includes225 vertical offset and exact original speed");}else Require(shadow.velocity==(type==225?-before:new Vector2(-8,3)),"no eligible/local-immune target retains native full/axis reversal");}
                shadow.ai[1]=type==225?3:1;shadow.penetrate=1;depth++;try{result=(int)Static(model,"Tile",shadow,before,Get(capture,"Targets"),path,1,Terrain());}finally{depth--;}Require(result==(type==36?2:1),"exact terminal bounce count retains native Kill/movement boundary");cases++;
            }
            Console.WriteLine("STEERING tile qualifications/terminal cases="+cases);
        }
        private static void Receives(object context)
        {
            int cases=0;
            foreach(int type in new[]{36,207,225,285})foreach(bool blocked in new[]{false,true})
            {
                var p=Scene(context,type==225?39:96,type);var n=Target(new Vector2(1250,350),blocked:blocked);if(type==36)Target(new Vector2(1120,350),1,20);var q=Spawn(p,type,new Vector2(1000,350),new Vector2(8,0));object root;var plan=Known(p,q,n,out root);Require((bool)Get(root,"Unchanged"),"finite steering root is initially current");int style=q.aiStyle;q.aiStyle=0;Require(!(bool)Get(root,"Unchanged"),"sameGUC actual motor-style mutation revokes steering family");q.aiStyle=style;Advance();Require(blocked?plan==null && first==0:plan!=null && first==plan.Tick,"sealed original steering body has exact first native receive and invulnerable negative type="+type+" actual="+first+" plan="+(plan?.Tick??-1));cases++;
            }
            Console.WriteLine("STEERING receives="+cases);
        }
        private static void Hands(object context)
        {
            foreach(int type in new[]{36,207,225,285})
            {
                var p=Scene(context,type==225?39:96,type);if(Environment.GetEnvironmentVariable("JMR_AMMO_STEERING_ROTATED_HAND")=="1")p.fullRotation=.4f;var n=Target(p.Center+new Vector2(240,0),side:40);var path=Path(n);var ammo=Static(host.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);// Match original ItemCheck_Shoot: a legitimate prior fullRotation remains part of this hand, not a reason to reset its pose.
                var origin=p.RotatedRelativePoint(p.MountedCenter);var sample=ContentSamples.ProjectilesByType[type];var root=Root(p,sample,path);AttackContact plan;depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))plan=(AttackContact)Static(model,"Solve",p,ammo,path,origin,0,true,Terrain(),root,0);}finally{depth--;}
                Require(plan!=null && (int)Get(ammo,"Damage")>0,"ordinary resolved steering ammo has complete source preparation type="+type);Main.mouseX=(int)(plan.AimX-Main.screenPosition.X);Main.mouseY=(int)(plan.AimY-Main.screenPosition.Y);p.itemAnimation=p.itemAnimationMax=p.HeldItem.useAnimation;
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active);Require(q.Center==origin && q.type==type && q.damage==(int)Get(ammo,"Damage"),"real ordinary Shoot preserves resolved steering ammo and selected ammo damage");object bornRoot;var actualBornPlan=Known(p,q,n,out bornRoot);var proposedVelocity=(Vector2)Static(host.GetType("JueMingR.TerrariaHost.Combat.HostThornAttack",true),"BirthVelocity",p,origin,new Vector2(plan.AimX,plan.AimY),(float)Get(ammo,"Speed"));Console.WriteLine("STEERING HAND PREMISE type="+type+" nativeOrigin="+q.Center+" modelOrigin="+origin+" rotatedOrigin="+p.RotatedRelativePoint(p.MountedCenter)+" nativeV="+q.velocity+" proposedV="+proposedVelocity+" speed="+Get(ammo,"Speed")+" extra="+q.extraUpdates+" maxPen="+q.maxPenetrate+" bodyFrame="+p.bodyFrame+" fullRotation="+p.fullRotation+" plan="+plan.Tick+" actualBornKnown="+actualBornPlan?.Tick+" target="+n.position+" netOffset="+n.netOffset+" screen="+Main.screenPosition+"/"+Main.screenWidth+"x"+Main.screenHeight);Advance();Require(first==plan.Tick,"ordinary steering Shoot→AI→first original Strike matches full model type="+type+" native="+first+" plan="+plan.Tick);Console.WriteLine("STEERING hand type="+type+" first="+first);
            }
        }
        private static void Harpy(object context)
        {
            int cases=0;var harpy=host.GetType("JueMingR.TerrariaHost.Combat.HostHarpyAttack",true);
            foreach(bool wall in new[]{false,true})foreach(bool blocked in new[]{false,true})
            {
                var p=Scene(context,39,225);p.accHarpyCharm=true;p.accHarpyCharmCooldown=0;var n=Target(new Vector2(1250,450),blocked:blocked);
                if(wall)for(int y=5;y<75;y++)NativeToolsChecks.Tile(72,y,1);
                var q=Spawn(p,225,new Vector2(1100,450),new Vector2(8,0));var f=new Projectile.AI_001_Features(q.ai[2]);f.HarpyCharmed=true;q.ai[2]=f.Bits;var path=Path(n);var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var clock=Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);AttackContact plan;
                depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))plan=(AttackContact)Static(harpy,"KnownFamily",p,new[]{q},path,clock,Terrain());}finally{depth--;}
                Advance(120);Require(first==0?plan==null:plan!=null && plan.Tick==first,"already charmed225 retains original AI→charm→environment→tile→receive ordering wall="+wall+" blocked="+blocked+" actual="+first+" plan="+(plan?.Tick??-1));if(!wall && !blocked)Require(first>0,"sealed charmed225 really reaches native Damage");cases++;
            }
            Console.WriteLine("STEERING charmed225 native/known cases="+cases);
        }        private static void HarpyFlight(object context)
        {
            var p=Scene(context,39,225);Call(Get(Get(context,"Combat"),"Attack"),"Reset");p.accHarpyCharm=true;p.accHarpyCharmCooldown=0;var n=Target(new Vector2(1250,450));
            var q=Spawn(p,225,new Vector2(1100,450),new Vector2(8,0));var f=new Projectile.AI_001_Features(q.ai[2]);f.HarpyCharmed=true;q.ai[2]=f.Bits;p.inventory[54].TurnToAir();
            var path=Path(n);var attack=Get(Get(context,"Combat"),"Attack");var phase=Enum.Parse(host.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var args=new object[]{p,path,phase,false};object flight;
            depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))flight=Call(attack,"ReadFlight",args);}finally{depth--;}
            var plan=flight==null?null:Get(flight,"Contact") as AttackContact;Require((bool)args[3] && plan!=null,"actual public ReadFlight observes already charmed225 with no future ammo and its complete returned dependencies");
            for(int i=0;i<20;i++)Require((bool)Get(flight,"Current") && (bool)Get(flight,"DependenciesCurrent"),"public sealed Harpy flight has a current scalar lease and complete captured receive dependencies");
            q.arrow=false;Require(!(bool)Get(flight,"Current") && !(bool)Get(flight,"DependenciesCurrent"),"sameGUC actual arrow qualification revokes both public Harpy leases");q.arrow=true;
            q.aiStyle=0;Require(!(bool)Get(flight,"Current") && !(bool)Get(flight,"DependenciesCurrent"),"sameGUC actual AI dispatch revokes both public Harpy leases");q.aiStyle=1;
            p.accHarpyCharmCooldown=1;Require(!(bool)Get(flight,"Current") && !(bool)Get(flight,"DependenciesCurrent"),"sameGUC real owner charm state revokes the public Harpy flight");p.accHarpyCharmCooldown=0;
            n.chaseable=false;Require(!(bool)Get(flight,"DependenciesCurrent"),"sameGUC target chase qualification revokes real Harpy flight dependencies");n.chaseable=true;Require((bool)Get(flight,"Current") && (bool)Get(flight,"DependenciesCurrent"),"restoring exact captured owner/target premises restores the same sealed flight");
            Advance(40);Require(first>0 && first==plan.Tick && !(bool)Get(attack,"hasConsumedInput"),"public observed charmed225 first contact equals native Damage without any hand permission or input consumption");Console.WriteLine("STEERING public Harpy225 noAmmo=true first="+first+" returnedLease=true owner/targetRevoke=true inputConsumed=false");
        }
        private static void HarpyAdapter(object context)
        {
            var harpy=host.GetType("JueMingR.TerrariaHost.Combat.HostHarpyAttack",true);int cases=0;
            foreach(int type in new[]{1,225,41})foreach(int gear in new[]{0,1,2,3})foreach(int initialCooldown in new[]{0,1})
            {
                var p=Scene(context,39,225);p.accHarpyCharm=gear!=3;p.accSeraphNecklace=gear==1;p.hasPhoenixQuiver=gear==2;p.accHarpyCharmCooldown=initialCooldown;
                var n=Target(p.Center+new Vector2(650,50));var a=Spawn(p,type,p.Center+new Vector2(500,0),new Vector2(3,-2));var b=Spawn(p,type,a.Center,new Vector2(3,-2));
                var path=Path(n);var root=Root(p,a,path);object state;depth++;try{state=Static(harpy,"CaptureBatchState",p,path,root);}finally{depth--;}
                var privateA=Copy(a);var privateB=Copy(b);var args=new object[]{new[]{privateB,privateA},state,path,0,Terrain(),p.Center,initialCooldown};
                depth++;try{Require((bool)Static(harpy,"BeforePlayerCharm",args),"shared batch charm consumes only captured target/terrain dependencies");}finally{depth--;}
                typeof(Player).GetMethod("Update_HarpyCharm",Flags).Invoke(p,null);
                Require((int)args[6]==p.accHarpyCharmCooldown && privateA.velocity==a.velocity && privateB.velocity==b.velocity && privateA.ai[2]==a.ai[2] && privateB.ai[2]==b.ai[2] && privateA.timeLeft==a.timeLeft && privateB.timeLeft==b.timeLeft,"actual Player Harpy stage equals batch adapter including cooldown, accessory flags and lower-slot tie on a reversed private list");
                // A native AI with only Harpy disabled supplies the motor input
                // to this isolated AFTER-AI callback test. The whole ordered
                // AI/movement/Damage batch is independently tested by Held.
                var motor=Copy(a);var bits=new Projectile.AI_001_Features(motor.ai[2]);bits.HarpyCharmed=false;motor.ai[2]=bits.Bits;motor.AI();motor.ai[2]=a.ai[2];a.AI();
                depth++;try{Require((bool)Static(harpy,"AfterAi",motor,state,path,1,Terrain()),"shared after-AI charm resolves exact captured target");}finally{depth--;}
                Require(motor.velocity==a.velocity && motor.ai[2]==a.ai[2],"original AI001 Harpy turn and speed floor/boost match shared callback type="+type+" gear="+gear+" cooldown="+initialCooldown);cases++;
            }
            Console.WriteLine("STEERING shared Harpy adapters native Player/AI cases="+cases+" reversedSlotTie=true");
        }
        private static void Confidence(object context)
        {
            foreach(int type in new[]{207,285})
            {
                var p=Scene(context,534,type);var n=Target(p.Center+new Vector2(240,0),side:100);var path=Path(n);var ammo=Static(host.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var root=Root(p,ContentSamples.ProjectilesByType[type],path);AttackContact contact;
                depth++;try{using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))contact=(AttackContact)Static(model,"Solve",p,ammo,path,p.RotatedRelativePoint(p.MountedCenter),0,true,Terrain(),root,0);}finally{depth--;}
                Require(contact!=null && contact.Confidence==AttackConfidence.Representative,"local homing proposal and resolved ordinary motor both retain the existing random weapon launch confidence");
                var shot=Spawn(p,type,p.MountedCenter,Vector2.UnitX*8);object knownRoot;var known=Known(p,shot,n,out knownRoot);Require(known!=null && known.Confidence==AttackConfidence.Conditional,"sealed actually observed velocity needs no future launch RNG representative");
            }
            Console.WriteLine("STEERING spread-source versus sealed confidence cases=4");
        }
        private static void Consumers(object context)
        {
            var observation=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
            foreach(int type in new[]{36,207,225,285})
            {
                var p=Scene(context,type==225?39:96,type);p.position=new Vector2(650,646);p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);var n=Target(p.Center+new Vector2(190,0),side:80);var ambient=Spawn(p,type,new Vector2(1100,350),Vector2.Zero,0);
                NativeCombatObservationChecks.Save(observation,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),n.Center-new Vector2(450,0),true);Call(combat,"Sample");depth++;try{NativeCombatAimChecks.PrepareMechanismFixture(observation,attack,n,0);}finally{depth--;}var plan=(AttackContact)GetOptional(attack,"ExpectedImpact");var root=GetOptional(attack,"obstacles");Require(plan!=null && root!=null,"actual ordinary steering public preparation returns its complete root type="+type);int oldStyle=ambient.aiStyle;ambient.aiStyle=0;Require(!(bool)Get(root,"Unchanged"),"actual public root detects sameGUC steering family style edit");ambient.aiStyle=oldStyle;
                int physicalX=Main.mouseX;typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require(Main.mouseX==physicalX && (bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced","real ordinary Shoot consumes enhanced steering preparation and restores physical cursor");Advance();Require(first>0,"real enhanced steering hand actually strikes after native AI");
            }
            Console.WriteLine("STEERING public preparedRoot→Enhanced→nativeStrike=4 (explicit component scope; not ordinary cost)");
        }
    }
}
