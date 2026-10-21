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
    internal static class NativeCombatAmmoImpactChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile receiving;private static int hitTick,hitType,currentTick,statusCalls;
        private sealed class Stage{internal int Type,Width,Height,Cooldown;internal bool Active;internal Vector2 Center;}
        private static readonly List<Stage> stages=new List<Stage>();private static readonly Dictionary<string,string> parents=new Dictionary<string,string>();
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static void Damage(Projectile __instance){receiving=__instance;stages.Add(new Stage{Type=__instance.type,Width=__instance.width,Height=__instance.height,Cooldown=__instance.idStaticNPCHitCooldown,Active=__instance.active,Center=__instance.Center});}
        private static void DamageDone(){receiving=null;}
        private static void Status(Projectile __instance){if(__instance.type==283 && !__instance.active)statusCalls++;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==2 && receiving!=null && hitTick==0){hitTick=currentTick;hitType=receiving.type;}}
        private static void Born(IEntitySource __0,int __result){var source=__0 as EntitySource_Parent;if(source?.Entity is Projectile p && __result>=0 && __result<1000)parents[Main.projectile[__result].key.ToString()]=p.key.ToString();}
        private static Player Reset(object context)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),39,0,0);p.position=new Vector2(850,328);p.velocity=Vector2.Zero;p.ResetEffects();Main.windPhysics=false;Main.ProjectileUpdateLoopIndex=-1;
            foreach(var n in Main.npc)n.active=false;foreach(var q in Main.projectile)q.active=false;for(int x=35;x<Main.maxTilesX-5;x++)for(int y=5;y<Main.maxTilesY-5;y++)Main.tile[x,y].ClearEverything();Main.tileSolid[1]=true;parents.Clear();stages.Clear();hitTick=hitType=currentTick=statusCalls=0;Array.Clear(Projectile.perIDStaticNPCImmunity,0,Projectile.perIDStaticNPCImmunity.Length);return p;
        }
        private static NPC Target(Vector2 center,int slot=2,int side=8)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=n.height=side;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static Projectile Spawn(Player p,int type,Vector2 center,Vector2 velocity,int damage=60)
        {return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,type,damage,0,p.whoAmI)];}
        private static NpcTrajectory Timeline(NPC n)
        {var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);}
        private static AttackContact Known(Player p,Projectile[] source,NPC n,out object root)
        {
            var path=Timeline(n);var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new[]{(object)path,phase},null);
            var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,source[0],path,0,true,false},null);return (AttackContact)Static(model,"KnownFamily",p,source,path,clock,terrain,root);
        }
        private static void Advance(NPC n,int limit=90,bool sameFirstEpoch=false)
        {for(currentTick=1;currentTick<=limit && hitTick==0;currentTick++){if(!sameFirstEpoch || currentTick>1)NativeQuickItemChecks.BeginWorldStep();n.UpdateNPC(n.whoAmI);for(int slot=0;slot<1000;slot++)if(Main.projectile[slot].active){Main.ProjectileUpdateLoopIndex=slot;try{Main.projectile[slot].Update(slot);}finally{Main.ProjectileUpdateLoopIndex=-1;}}}}
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAmmoImpactAttack",true);var audit=new Harmony("JueMingR.Tests.AmmoImpact");bool wind=Main.windPhysics;float windSpeed=Main.windSpeedCurrent,windStrength=Main.windPhysicsStrength;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAmmoImpactChecks),nameof(Damage)),finalizer:new HarmonyMethod(typeof(NativeCombatAmmoImpactChecks),nameof(DamageDone)));
            audit.Patch(typeof(Projectile).GetMethod("StatusNPC",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAmmoImpactChecks),nameof(Status)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatAmmoImpactChecks),nameof(Strike)));
            var birth=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float));audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatAmmoImpactChecks),nameof(Born)));
            try
            {
                // Each group establishes its own original world via Reset.
                // Keep all failed originals while exposing independent later
                // obligations; any failed group still fails the entire leaf.
                var failures=new List<Exception>();Action<string,Action> run=(name,body)=>{try{body();}catch(Exception error){failures.Add(error);Console.WriteLine("FAIL AMMO GROUP "+name+": "+error);}};
                run("motor",()=>Motors(context));run("environment",()=>Environment(context));run("kill",()=>Kills(context));run("receive",()=>Receives(context));run("static",()=>StaticReset(context));run("crystal",()=>Crystal(context));run("quiver",()=>Quiver(context));run("hands",()=>Hands(context));run("eventide",()=>Eventide(context));run("public-consumer",()=>Consumer(context));
                if(failures.Count>0)throw new AggregateException("AmmoImpact original groups failed: "+failures.Count,failures);
                Console.WriteLine("PASS AMMO IMPACT native41/89/283/286 finite Kill stages, sealed90 motor/receive, slots and static history, ordinary true hands.");
            }
            finally{Main.ProjectileUpdateLoopIndex=-1;Main.windPhysics=wind;Main.windSpeedCurrent=windSpeed;Main.windPhysicsStrength=windStrength;audit.UnpatchAll(audit.Id);}
        }
        private static void Motors(object context)
        {
            int actions=0;foreach(int type in new[]{41,89,90,283,286})foreach(var velocity in new[]{new Vector2(6,-1),new Vector2(-2,20),Vector2.Zero})foreach(int clock in new[]{0,14,15})
            {
                var p=Reset(context);var q=Spawn(p,type,new Vector2(1000,350),velocity);q.ai[0]=clock;var copy=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",q);
                for(int step=0;step<60 && q.active;step++)
                {
                    bool alive=(bool)Static(model,"Ai",copy);q.AI();actions++;
                    Require(alive==q.active && Vector2.Distance(q.velocity,copy.velocity)<.0001f && q.ai.SequenceEqual(copy.ai) && q.localAI.SequenceEqual(copy.localAI) && Math.Abs(q.scale-copy.scale)<.00001f && q.alpha==copy.alpha && Math.Abs(q.rotation-copy.rotation)<.0001f,"native ammo AI clocks/scale/velocity: "+type+"/"+step);
                    q.position+=q.velocity;copy.position+=copy.velocity;q.timeLeft--;copy.timeLeft--;
                }
            }
            Console.WriteLine("PASS AMMO MOTOR 5types x3 velocities x3 initial clocks0/14/15 native complete crystal lifetime, actions="+actions);
        }
        private static void Kills(object context)
        {
            foreach(int type in new[]{41,89,283,286})
            {
                var p=Reset(context);var q=Spawn(p,type,new Vector2(1100,400),new Vector2(8,-2));q.oldVelocity=new Vector2(3,4);var center=q.position+new Vector2(q.width/2,q.height/2);string key=q.key.ToString();var copy=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",q);copy.oldVelocity=q.oldVelocity;stages.Clear();q.Kill();
                if(type==41 || type==286){Require(stages.Count==1 && stages[0].Width==(type==41?64:80) && stages[0].Height==stages[0].Width && stages[0].Center==center && stages[0].Active,"native Kill keeps exact samebody center and expanded Damage: "+type);Require((bool)Static(model,"PrepareKillBurst",copy) && copy.width==stages[0].Width && copy.Center==stages[0].Center && (type!=286 || stages[0].Cooldown==5),"private Kill policy equals actual expanded body");}
                if(type==283)
                {
                    var areas=(Rectangle[])Static(model,"VenomStatusAreas",copy);Require(stages.Count==3 && stages.All(s=>s.Width==80 && !s.Active),"Venom original three inactive Damage passes are status stages");for(int i=0;i<3;i++)Require(new Rectangle((int)(stages[i].Center.X-40),(int)(stages[i].Center.Y-40),80,80)==areas[i],"Venom original oldVelocity-based three translated integer areas");
                    p=Reset(context);q=Spawn(p,283,new Vector2(1100,400),Vector2.Zero);q.oldVelocity=new Vector2(3,4);var n=Target(q.Center+new Vector2(36,48));q.Kill();Require(statusCalls>0 && hitTick==0 && n.life==10000 && n.buffType.Contains(70),"Venom Kill really applies Venom without any Strike or life damage");
                }
                if(type==89)
                {
                    var children=Main.projectile.Where(s=>s.active && s.type==90).ToArray();Require(children.Length==2,"native Crystal Kill emits exactly two children");foreach(var child in children){Require(parents[child.key.ToString()]==key && child.damage==30 && child.knockBack==0 && child.Center==q.position+child.velocity,"native90 parent source/half damage/top-left displaced center");float rx=child.velocity.X+q.velocity.X*.55f,ry=child.velocity.Y+q.velocity.Y*.55f;Require(Math.Abs(rx)<=9.3f && Math.Abs(ry)<=8.4f,"native crystal velocity lies within actual discrete support");}
                    var representatives=(Projectile[])Static(model,"KillChildren",copy);Require(representatives.Length==2 && representatives.All(c=>c.damage==30 && c.Center==copy.position+c.velocity),"private crystal births are legal representatives of the full original formula");
                }
            }
            Console.WriteLine("PASS AMMO KILL original 64/80 samebody,89->2x90 EntitySource and283 three status-only Damage/buff");
        }
        private static void Environment(object context)
        {
            int count=0;foreach(int type in new[]{41,89,90,283,286})foreach(int liquid in new[]{-1,0,2,3})
            {
                var owner=Reset(context);if(liquid>=0)for(int x=60;x<72;x++)for(int y=18;y<29;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(liquid);}
                Main.windPhysics=true;Main.windSpeedCurrent=.15f;Main.windPhysicsStrength=.2f;
                var q=Spawn(owner,type,new Vector2(1000,350),new Vector2(4,-1));q.extraUpdates=0;q.damage=0;
                var copy=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",q);var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");
                for(int action=0;action<3 && q.active;action++)
                {
                    object[] args={copy,terrain,false,false,null};Require((bool)Static(model,"Step",args),"ammo shares native environment phase");bool killed=(bool)args[2];stages.Clear();q.Update(q.whoAmI);
                    if(killed){Require(!q.active,"native full shimmer column kills generic bullet before Damage");break;}
                    copy.timeLeft--;Require(q.active && Vector2.Distance(q.position,copy.position)<.001f && Vector2.Distance(q.velocity,copy.velocity)<.001f && q.timeLeft==copy.timeLeft && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.shimmerWet==copy.shimmerWet,"native water/honey/shimmer/wind Update movement: "+type+"/"+liquid+"/"+action);count++;
                }
            }
            foreach(int type in new[]{41,286})
            {
                var p=Reset(context);for(int y=17;y<30;y++)NativeToolsChecks.Tile(70,y,1);var q=Spawn(p,type,new Vector2(1102,350),new Vector2(20,0));q.extraUpdates=0;
                var copy=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",q);var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");object[] args={copy,terrain,false,false,null};Require((bool)Static(model,"Step",args) && (bool)args[2],"native collision is a finite Kill trigger");Static(model,"PrepareKillBurst",copy);stages.Clear();q.Update(q.whoAmI);var burst=stages.First(s=>s.Width==(type==41?64:80));Console.WriteLine("AMMO TILE type="+type+" private="+copy.Center+" nativeBurst="+burst.Center+" nativeFinal="+q.Center+" velocity="+q.velocity);Require(Vector2.Distance(burst.Center,copy.Center)<.001f,"tile Kill Damage uses the clipped pre-burst center, before dead-body later movement");
            }
            Console.WriteLine("PASS AMMO ENV original dry/water/honey/shimmer+wind actions="+count+" and two native tile Kill centers");
        }
        private static void Receives(object context)
        {
            foreach(int type in new[]{41,286})foreach(int mode in new[]{0,1,2})
            {
                var p=Reset(context);var q=Spawn(p,type,new Vector2(1100,400),Vector2.Zero);q.timeLeft=1;var n=Target(q.Center+new Vector2(26,0));if(mode==1)Target(n.Center,1);if(mode==2)n.immune[p.whoAmI]=200;
                object root;var expected=Known(p,new[]{q},n,out root);Advance(n,2,true);bool useful=mode==0 || mode==1 && type==41;
                Console.WriteLine("AMMO BURST type="+type+" mode="+mode+" expected="+(expected==null?"null":expected.Tick.ToString())+" native="+hitTick);
                Require((expected!=null)==useful && (hitTick>0)==useful && (!useful || hitTick==expected.Tick && hitType==type),"samebody Kill receive preserves NPC slot consumption/owner qualification: "+type+"/"+mode);
            }
            Console.WriteLine("PASS AMMO BURST six native positive/slot/owner receive cases");
        }
        private static void StaticReset(object context)
        {
            foreach(int offset in new[]{60,61})
            {
                var p=Reset(context);var first=Spawn(p,286,new Vector2(1100,400),Vector2.Zero);var second=Spawn(p,286,first.Center,Vector2.Zero);first.timeLeft=second.timeLeft=1;var n=Target(first.Center+new Vector2(28,0));Projectile.perIDStaticNPCImmunity[second.immunityIdentity,2]=(uint)(Main.GameUpdateCount+offset);
                object root;var expected=Known(p,new[]{second},n,out root);Advance(n,2,true);Require((expected!=null)==(offset==60) && (hitTick>0)==(offset==60),"native ResetStatic clears only exact old60 expiry, not61; candidate siblings share the reset");
                if(offset==60)Require(hitTick==expected.Tick && Projectile.perIDStaticNPCImmunity[second.immunityIdentity,2]==(uint)(Main.GameUpdateCount+5),"new burst five-tick assignment survives old-expiry reset");
            }
            Console.WriteLine("PASS AMMO STATIC old60 match versus61, sameowner prior slot reset and new5 retained");
        }
        private static void Crystal(object context)
        {
            foreach(bool highSlot in new[]{true,false})
            {
                var p=Reset(context);if(!highSlot){Main.projectile[0].SetDefaults(1);Main.projectile[0].active=true;Main.projectile[0].whoAmI=0;}
                var parent=Spawn(p,89,new Vector2(1100,400),new Vector2(8,0));if(!highSlot)Main.projectile[0].active=false;parent.Kill();var children=Main.projectile.Where(q=>q.active && q.type==90).ToArray();var child=children[0];var n=Target(child.Center+child.velocity*5,2,28);object root;var expected=Known(p,children,n,out root);float scale=child.scale;child.scale+=.1f;Require(!(bool)Get(root,"Unchanged"),"sameGUC crystal scale/lifetime change revokes root");child.scale=scale;Advance(n,30);Require(expected!=null && hitType==90 && hitTick==expected.Tick,"sealed original90 children have useful originalAI024 first Strike: "+highSlot);
            }
            Console.WriteLine("PASS AMMO CRYSTAL sealed native two-child motor/receive and lower/higher source slots");
        }
        private static void Hands(object context)
        {
            foreach(var row in new[]{new[]{39,265,41},new[]{96,515,89},new[]{96,1342,283},new[]{96,1351,286}})
            {
                var p=Reset(context);p.inventory[p.selectedItem].SetDefaults(row[0]);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;p.itemAnimationMax=p.HeldItem.useAnimation;p.itemAnimation=p.itemAnimationMax;
                var n=Target(new Vector2(1250,350),2,40);var path=Timeline(n);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var origin=p.RotatedRelativePoint(p.MountedCenter);var sample=new Projectile();sample.SetDefaults(row[2]);sample.owner=p.whoAmI;
                var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sample,path,0,true,false},null);
                var expected=(AttackContact)Static(model,"Solve",p,ammo,path,origin,0,true,terrain,root,0);Require(expected!=null && (int)Get(ammo,"Damage")>0,"resolved ammo has full finite family preparation: "+row[2]);Main.mouseX=(int)(expected.AimX-Main.screenPosition.X);Main.mouseY=(int)(expected.AimY-Main.screenPosition.Y);
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active);Require(q.type==row[2] && q.damage==(int)Get(ammo,"Damage"),"actual ordinary ammo conversion and damage");Advance(n,90);Console.WriteLine("AMMO HAND type="+row[2]+" predicted="+expected.Tick+" native="+hitTick);Require(hitType==row[2] && hitTick==expected.Tick,"native Shoot→AI→Damage first useful ammo receive");
            }
        }
        private static void Quiver(object context)
        {
            var p=Reset(context);p.inventory[54].SetDefaults(265);p.inventory[54].stack=999;p.magicQuiver=true;var q=Spawn(p,41,new Vector2(1100,400),new Vector2(8,0));Require(q.extraUpdates==0,"native born Hellfire has not yet performed the post-movement Quiver gate");var n=Target(q.Center+q.velocity*2.3f,2,1);object root;var sealedContact=Known(p,new[]{q},n,out root);
            q.active=false;var path=Timeline(n);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);
            var explicitContact=(AttackContact)Static(model,"Explicit",p,ammo,path,n.Center,q.Center,q.velocity,0,terrain,root,0,null);q.active=true;
            Require(sealedContact!=null && explicitContact!=null && sealedContact.Tick==2 && explicitContact.Tick==2,"sealed and explicit Hellfire retain first outer1 action, subsequent outer2 actions");Advance(n,3,true);Require(hitTick==2 && hitTick==explicitContact.Tick,"original Quiver first-action epoch matches the explicit family hand");Console.WriteLine("PASS AMMO QUIVER native first outer1 action/later2, explicit/sealed/native firstStrike2");
        }
        private static void Eventide(object context)
        {
            int cases=0;
            foreach(int stage in new[]{0,1,3,4})foreach(bool quiver in new[]{false,true})
            {
                var p=Reset(context);p.inventory[p.selectedItem].SetDefaults(4953);p.inventory[54].SetDefaults(265);p.inventory[54].stack=999;p.magicQuiver=quiver;p.itemAnimationMax=p.HeldItem.useAnimation;p.itemAnimation=p.itemAnimationMax-stage*2;
                var n=Target(new Vector2(1250,350),2,40);var path=Timeline(n);var ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var origin=p.RotatedRelativePoint(p.MountedCenter);var sample=new Projectile();sample.SetDefaults(41);sample.owner=p.whoAmI;var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");var root=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sample,path,0,true,false},null);
                // The historical Homing.Solve alias now returns Prepare.Contact,
                // which is optional for this unborn representative. The actual
                // born41 family below still owns its complete receive proof.
                var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"BeforeNpc");var clock=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,phase},null);
                var solution=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNativeArrowBirthAttack",true),"Prepare",p,ammo,path,origin,clock,terrain,root,false);var point=(Vector2)Get(solution,"Point");int receive=(int)Get(solution,"ReceiveTick"),firstTick=(int)Get(clock,"FirstTick");
                Require((bool)Get(solution,"HasPoint") && ReferenceEquals(Get(solution,"Timeline"),path) && Get(solution,"Reachability").ToString()=="Reachable" && receive>=firstTick && receive<path.Count && !float.IsNaN(point.X) && !float.IsNaN(point.Y) && !float.IsInfinity(point.X) && !float.IsInfinity(point.Y),"actual Eventide ordinal has a useful current representative point: "+stage+"/"+quiver);
                Main.mouseX=(int)(point.X-Main.screenPosition.X);Main.mouseY=(int)(point.Y-Main.screenPosition.Y);Require((p.itemAnimationMax-p.itemAnimation)/2==stage,"real Eventide Shoot retains this noncentral native ordinal");typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var born=Main.projectile.Where(s=>s.active).ToArray();Require(born.Length==1 && born[0].type==41 && born[0].owner==p.whoAmI && born[0].damage==(int)Get(ammo,"Damage"),"noncentral Eventide hand really births one selected41 with original owner/damage, retaining central932's separate stage");
                object bornRoot;var expected=Known(p,born,n,out bornRoot);Require(expected!=null && (bool)Get(bornRoot,"Unchanged"),"sealed actual Eventide41 retains a complete current family receive root");Advance(n,90);Require(hitType==41 && hitTick>0 && hitTick==expected.Tick && hitTick==receive-firstTick+1,"original Eventide displaced birth/Quiver→AI→Kill first Damage matches born-family replay and the representative clock: "+stage+"/"+quiver);cases++;
            }
            Require(cases==8,"all four noncentral Eventide41 ordinals and both real Quiver states execute");
            Console.WriteLine("PASS AMMO EVENTIDE four noncentral41 ordinals xQuiver2 native Shoot→complete receive");
        }
        private static void Consumer(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");
            foreach(var row in new[]{new[]{39,265,41},new[]{96,515,89},new[]{96,1342,283},new[]{96,1351,286}})for(int mode=0;mode<2;mode++)
            {
                var p=Reset(context);p.inventory[p.selectedItem].SetDefaults(row[0]);p.inventory[54].SetDefaults(row[1]);p.inventory[54].stack=999;p.position=new Vector2(650,646);p.itemAnimation=p.itemTime=0;p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);var n=Target(p.Center+new Vector2(190,0),2,80);var ambient=Spawn(p,row[2],new Vector2(1100,350),Vector2.Zero);ambient.damage=0;
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));NativeToolExecutionChecks.Sample(context,Get(context,"Input"),n.Center-new Vector2(450,0),true);Call(combat,"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,phaseName:"BeforeNpc");var plan=NativeCombatAimChecks.RequireSolution(attack,p,"real ordinary ammo preparation supplies a current representative input: "+row[2]);
                // Ambient family lifetime belongs to the dedicated KnownFamily
                // root above, not to this unborn representative hand.
                ambient.timeLeft--;NativeCombatAimChecks.RequireSolution(attack,p,"ambient ammo clock edit does not revoke a representative hand");
                var origin=(Vector2)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true),"LaunchOrigin",p,p.HeldItem);var cell=Main.tile[(int)origin.X/16,(int)origin.Y/16];byte liquid=cell.liquid;cell.liquid=(byte)(liquid==0?1:0);try{Require(NativeCombatAimChecks.QueryInput(attack,p).Kind!="Enhanced","changed actual muzzle liquid cell withdraws the captured representative terrain premise");}finally{cell.liquid=liquid;}
                var velocity=p.velocity;p.velocity+=Vector2.UnitX;try{Require(NativeCombatAimChecks.QueryInput(attack,p).Kind!="Enhanced","changed owner motion withdraws old ordinary ammo enhancement");}finally{p.velocity=velocity;}
                Main.npc[n.whoAmI]=new NPC();try{Require(!NativeCombatAimChecks.QueryInput(attack,p).Allowed,"a different object in the selected slot cannot inherit the prepared target identity");}finally{Main.npc[n.whoAmI]=n;}
                NativeCombatAimChecks.RequireSolution(attack,p,"unchanged selected identity and owner motion restore the same ordinary ammo solution");
                if(mode==1){p.inventory[54].stack--;Require(NativeCombatAimChecks.QueryInput(attack,p).Kind!="Enhanced","selected ammo member edit withdraws this hand's enhancement");}
                int mouse=Main.mouseX,mouseY=Main.mouseY;Require(!(bool)Get(attack,"hasConsumedInput"),"querying an ammo solution never fabricates native consumption");typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Require(Main.mouseX==mouse && Main.mouseY==mouseY && Main.projectile.Any(q=>q.active && q.type==row[2] && q.damage>0),"native ammo hand/birth restores physical cursor");
                bool enhanced=(bool)Get(attack,"hasConsumedInput") && Convert.ToString(Get(Get(attack,"consumedInput"),"Kind"))=="Enhanced";Require(enhanced==(mode==0),"only current selected-ammo preparation is actually consumed as Enhanced");if(mode==0){NativeCombatAimChecks.RequireConsumedInput(attack,p,plan,"ordinary ammo native hand consumes its actual prepared point");Advance(n,60);Require(hitTick>0 && hitType==row[2],"public enhanced ordinary ammo hand reaches native Damage");}
            }
            Console.WriteLine("PASS AMMO public representative→native Shoot Enhanced+Strike; ambient independent, selected identity/motion/ammo invalidation (1000ms mechanism component allowance)");
        }
    }
}
