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
    internal static class NativeCombatBoomerangVariantChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal static void Run(object context)
        {
            var assembly=Get(context,"Combat").GetType().Assembly;var variants=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBoomerangVariants",true);var melee=assembly.GetType("JueMingR.TerrariaHost.Combat.HostMeleeAttack",true);
            var terrainType=assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);var targetsType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostProjectileTargets",true);var obstaclesType=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true);var ammoType=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true);
            bool wind=Main.windPhysics;int cases=0,steps=0,shocks=0;
            var audit=new Harmony("JueMingR.Tests.BoomerangVariantPlayer");
            try
            {
                foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
                foreach(int type in new[]{182,383,301,866})foreach(int scene in Enumerable.Range(0,9))foreach(float speed in new[]{1f,.65f})
                {
                    var p=Reset(context,type);p.meleeSpeed=speed;p.position=new Vector2(790,350);
                    var target=Target(2,new Vector2(1150,250),32,40);if(scene==0)target.dontTakeDamage=true;
                    if(scene>=1 && scene<=4)for(int x=40;x<=80;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:scene==3?1:3);}
                    if(scene>=5){Main.tile[60,25].active(true);Main.tile[60,25].type=1;Main.tile[60,25].slope((byte)(scene-4));}
                    for(int y=15;y<45;y++){Main.tile[80,y].active(true);Main.tile[80,y].type=1;}
                    for(int x=40;x<=80;x++){Main.tile[x,45].active(true);Main.tile[x,45].type=1;}
                    int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),scene>=5?new Vector2(936,390):new Vector2(920,350),scene>=5?new Vector2(24,8):new Vector2(8,-.5f),type,0,0,p.whoAmI);var q=Main.projectile[slot];
                    var shadow=(Projectile)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(q,null);shadow.ai=(float[])q.ai.Clone();shadow.localAI=(float[])q.localAI.Clone();shadow.localNPCImmunity=(int[])q.localNPCImmunity.Clone();
                    var timeline=Timeline(assembly,target);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var targets=targetsType.GetMethod("Capture",Flags).Invoke(null,new object[]{shadow,1L});
                    bool alive=true;
                    for(int outer=1;outer<=260 && q.active;outer++)
                    {
                        for(int sub=0;sub<shadow.extraUpdates+1 && alive;sub++)
                        {
                            var args=new object[]{shadow,p,terrain,targets,timeline,outer,alive,null,outer};Require((bool)variants.GetMethod("Advance",Flags).Invoke(null,args),"AI003 special full motor retains actual terrain/chase dependencies: "+type+"/"+scene+"/"+outer);alive=(bool)args[6];if(!alive)break;
                            if(shadow.localAI[0]>0)shocks++;variants.GetMethod("DamageBox",Flags).Invoke(null,new object[]{shadow});steps++;
                        }
                        q.Update(slot);
                        Require(q.active==alive && (!q.active || Vector2.DistanceSquared(q.Center,shadow.Center)<.002f && Vector2.DistanceSquared(q.velocity,shadow.velocity)<.0001f && q.ai[0]==shadow.ai[0] && q.ai[1]==shadow.ai[1] && q.localAI[0]==shadow.localAI[0] && q.tileCollide==shadow.tileCollide),
                            "native182/383/301 target/anchor/fall/tile/shock/vector-return agrees: type="+type+" scene="+scene+" speed="+speed+" outer="+outer+" native="+q.Center+"/"+q.velocity+"/"+q.ai[0]+","+q.ai[1]+","+q.localAI[0]+" model="+shadow.Center+"/"+shadow.velocity+"/"+shadow.ai[0]+","+shadow.ai[1]+","+shadow.localAI[0]);
                    }
                    Require(!q.active,"native special boomerang eventually returns and catches its real owner");cases++;
                }
                Contacts(context,assembly,melee,terrainType,obstaclesType,ammoType);
                HammerShapes(context,assembly,variants,terrainType);Retargets(context,assembly,variants,terrainType,obstaclesType,targetsType);Require(shocks>0,"native383/301 audit includes real tile shock stages");Console.WriteLine("PASS AI003182/383/301/866 motors="+cases+" substeps="+steps+" shockCalls="+shocks+";phaseblade motor/input is validated by its separate scope");
            }
            finally{audit.UnpatchAll(audit.Id);Main.windPhysics=wind;}
        }
        private static void Contacts(object context,Assembly assembly,Type melee,Type terrainType,Type obstaclesType,Type ammoType)
        {
            int useful=0,negatives=0;
            foreach(int type in new[]{182,383,301,866})foreach(bool front in new[]{false,true})
            {
                var p=Reset(context,type);var origin=p.RotatedRelativePoint(p.MountedCenter);var target=Target(2,new Vector2(origin.X+190,origin.Y-20),32,40);
                NPC earlier=null;if(front)earlier=Target(1,new Vector2(origin.X+80,origin.Y-150),30,300);
                var timeline=Timeline(assembly,target);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var ammo=ammoType.GetMethod("Capture",Flags).Invoke(null,new object[]{p,p.HeldItem});
                Require((int)Get(ammo,"Projectile")==type,"actual hand weapon resolves admitted special boomerang type");
                var policy=new Projectile();policy.SetDefaults(type);policy.owner=p.whoAmI;var obstacles=Activator.CreateInstance(obstaclesType,Flags,null,new object[]{p,policy,timeline,0,true,false},null);
                var contact=(AttackContact)melee.GetMethod("Solve",Flags).Invoke(null,new object[]{p,p.HeldItem,ammo,timeline,0,true,terrain,0,obstacles});
                bool impossible=type==182 && front;
                Require(impossible?contact==null:contact!=null,"182 actual prior hit starts return;383/301 own OnHit keeps outbound: "+type+"/"+front+" contact="+contact?.Tick);
                if(contact==null)
                {
                    earlier.active=false;Call(terrain,"Reset");obstacles=Activator.CreateInstance(obstaclesType,Flags,null,new object[]{p,policy,timeline,0,true,false},null);
                    contact=(AttackContact)melee.GetMethod("Solve",Flags).Invoke(null,new object[]{p,p.HeldItem,ammo,timeline,0,true,terrain,0,obstacles});earlier.active=true;
                    Require(contact!=null,"special boomerang prior-hit negative has independently reachable real control");
                }
                var velocity=(new Vector2(contact.AimX,contact.AimY)-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed");int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,velocity,type,50,0,p.whoAmI);var q=Main.projectile[slot];int first=0,other=0;
                for(int outer=1;outer<=120 && q.active;outer++)
                {
                    NativeQuickItemChecks.BeginWorldStep();
                    // This is a BeforeNpc plan. The first owner state is
                    // already observed; later 301 anchors use real subsequent
                    // Player.Update, including the clear arena's falling.
                    if(type==301 && outer>1){NativeToolExecutionChecks.Sample(context,Get(context,"Input"),target.Center,false);p.controlUseItem=false;p.Update(p.whoAmI);}
                    if(earlier!=null)earlier.UpdateNPC(1);target.UpdateNPC(2);int before=earlier?.life??0;q.Update(slot);if(earlier!=null && earlier.life<before){other++;if(type==182)Require(q.ai[0]==1,"actual182 prior Damage triggers native common OnHit return");}
                    if(target.life<10000){first=outer;break;}
                }
                Require(impossible?first==0 && other>0:first==contact.Tick,"native special boomerang first useful Damage and prior receiver agrees: "+type+"/"+front+" model="+contact.Tick+" native="+first+" other="+other);
                if(first>0)useful++;else negatives++;
            }
            Require(useful==7 && negatives==1,"special boomerang contacts include useful Damage and a causal negative");
        }
        private static void HammerShapes(object context,Assembly assembly,Type variants,Type terrainType)
        {
            int positives=0,negatives=0;
            foreach(bool burst in new[]{false,true})foreach(bool outside in new[]{false,true})
            {
                var p=Reset(context,301);var center=new Vector2(920,350);int offset=burst?(outside?123:110):(outside?42:30);
                var target=Target(2,new Vector2(center.X+offset,center.Y-5),10,10);
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,Vector2.Zero,301,50,0,p.whoAmI);var q=Main.projectile[slot];q.localAI[0]=burst?200:0;
                var shadow=(Projectile)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(q,null);shadow.localAI=(float[])q.localAI.Clone();
                var box=(Rectangle)variants.GetMethod("DamageBox",Flags).Invoke(null,new object[]{shadow});
                Require(box.Width==(burst?244:82) && box.Height==box.Width && box.Intersects(target.Hitbox)==!outside,"301 effective collision includes native22 inflation after normal/200 impulse");
                q.Damage();Require((target.life<10000)==!outside,"301 native useful Damage agrees at normal/impulse shape edge: "+burst+"/"+outside);
                if(burst)
                {
                    Require(q.localAI[0]==-1 && shadow.localAI[0]==-1,"301 impulse is consumed by the actual Damage call even when it missed");
                    target.life=10000;Array.Clear(q.localNPCImmunity,0,q.localNPCImmunity.Length);Array.Clear(target.immune,0,target.immune.Length);
                    q.Damage();Require(target.life==10000,"301 second Damage cannot reuse a consumed200 impulse, with immunity independently clear");
                }
                if(outside)negatives++;else positives++;
            }
            Require(positives==2 && negatives==2,"301 normal and shock contact edges include actual damage and causal negatives");
        }
        private static void Retargets(object context,Assembly assembly,Type variants,Type terrainType,Type obstaclesType,Type targetsType)
        {
            int clockCases=0,receiveCases=0,returnSteps=0;
            foreach(int clock in new[]{9,10,360,361})foreach(bool localBlocked in new[]{false,true})
            {
                var p=Reset(context,866);p.position=new Vector2(790,350);var target=Target(2,new Vector2(1150,250),32,40);target.immune[p.whoAmI]=200;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(920,350),new Vector2(6,0),866,50,0,p.whoAmI);var q=Main.projectile[slot];q.extraUpdates=0;q.localAI[0]=clock;if(localBlocked)q.localNPCImmunity[2]=-1;
                var shadow=Copy(q);var timeline=Timeline(assembly,target);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var targets=targetsType.GetMethod("Capture",Flags).Invoke(null,new object[]{shadow,1L});var receivers=Activator.CreateInstance(obstaclesType,Flags,null,new object[]{p,shadow,timeline,0,true,false},null);
                var args=new object[]{shadow,p,terrain,targets,timeline,1,true,receivers,1};Require((bool)variants.GetMethod("Advance",Flags).Invoke(null,args),"866 bounded chase clock retains complete scene dependencies");q.Update(slot);
                Require(q.ai[0]==shadow.ai[0] && q.ai[1]==shadow.ai[1] && q.localAI[0]==shadow.localAI[0] && Vector2.DistanceSquared(q.Center,shadow.Center)<.002f && Vector2.DistanceSquared(q.velocity,shadow.velocity)<.0001f,"866 exact9/10/360/361 query boundary and local immunity agrees: "+clock+"/"+localBlocked);
                if((clock==10 || clock==360) && !localBlocked)Require(q.ai[1]==2,"866 can chase an owner-immune NPC when its local immunity is clear");clockCases++;
            }
            foreach(bool ownerBlocked in new[]{false,true})
            {
                var p=Reset(context,866);p.position=new Vector2(790,350);var center=new Vector2(920,350);var target=Target(10,new Vector2(1200,250),32,40);
                for(int i=1;i<=5;i++)Target(i,center+new Vector2(6,i*2-4),8,8);if(ownerBlocked)Main.npc[1].immune[p.whoAmI]=200;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,new Vector2(6,0),866,50,0,p.whoAmI);var q=Main.projectile[slot];var shadow=Copy(q);
                var timeline=Timeline(assembly,target);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var targets=targetsType.GetMethod("Capture",Flags).Invoke(null,new object[]{shadow,1L});var receivers=Activator.CreateInstance(obstaclesType,Flags,null,new object[]{p,shadow,timeline,0,true,false},null);Call(receivers,"BeginCandidate");
                Func<int,int,bool> hit=(npc,penetration)=>(bool)variants.GetMethod("OnRetargetHit",Flags).Invoke(null,new object[]{shadow,npc,penetration,terrain,targets,timeline,1,receivers,1});
                var body=shadow.Hitbox;bool passes=(bool)obstaclesType.GetMethod("PassShapeOuter",Flags).Invoke(receivers,new object[]{2,1,1,body.X+body.Width*.5f,body.Y+body.Height*.5f,body.Width,body.Height,false,0,hit});q.Damage();
                int expectedHits=ownerBlocked?4:5;
                Require((int)Get(receivers,"OtherHits")==expectedHits && Main.npc.Skip(1).Take(5).Count(n=>n.life<10000)==expectedHits,"866 retarget events come only from actual qualified slot-ordered NPC receives");
                Console.WriteLine("BOOMERANG866 receive gate ownerBlocked="+ownerBlocked+" passes="+passes+" remaining="+Get(receivers,"Remaining")+" native="+q.active+"/"+q.damage+"/"+q.penetrate+"/"+q.ai[1]+"/"+q.velocity+" model="+shadow.active+"/"+shadow.damage+"/"+shadow.penetrate+"/"+shadow.ai[1]+"/"+shadow.velocity+" locals="+string.Join(",",q.localNPCImmunity.Skip(1).Take(5))+" owners="+string.Join(",",Main.npc.Skip(1).Take(5).Select(n=>n.immune[p.whoAmI])));
                Require(passes==ownerBlocked && q.active && q.damage==shadow.damage && q.penetrate==shadow.penetrate && q.ai[1]==shadow.ai[1] && Vector2.DistanceSquared(q.velocity,shadow.velocity)<.0001f,"866 every OnHit retargets before the next NPC slot; fifth hit is zero-damage survival, not Kill");
                for(int i=ownerBlocked?2:1;i<=5;i++)Require(q.localNPCImmunity[i]==-1 && !(bool)obstaclesType.GetMethod("LocalTargetAllowed",Flags).Invoke(receivers,new object[]{i,1}),"866 actual received NPC remains excluded from chase by private local immunity");
                if(ownerBlocked)Require(Main.npc[1].life==10000 && q.ai[1]==1 && q.damage>0,"866 can retarget immune front without inventing its OnHit or spending fifth penetration");
                else
                {
                    Require(q.damage==0 && q.penetrate==1 && q.ai[1]==-1,"866 final native receive retains a visual return body with zero useful damage");bool alive=true;
                    for(int outer=1;outer<=120 && q.active;outer++)
                    {
                        for(int sub=0;sub<shadow.extraUpdates+1 && alive;sub++){var args=new object[]{shadow,p,terrain,targets,timeline,outer,alive,receivers,outer};Require((bool)variants.GetMethod("Advance",Flags).Invoke(null,args),"866 zero-damage return retains its scene");alive=(bool)args[6];returnSteps++;}
                        q.Update(slot);Require(q.active==alive && (!alive || Vector2.DistanceSquared(q.Center,shadow.Center)<.002f && Vector2.DistanceSquared(q.velocity,shadow.velocity)<.0001f && q.ai[0]==shadow.ai[0]),"866 actual zero-damage body completes its finite native return/catch");
                    }
                    Require(!q.active,"866 zero-damage fifth receive does not strand a body or invent a useful contact");
                }
                receiveCases++;
            }
            Console.WriteLine("BOOMERANG866 clocks="+clockCases+" receives="+receiveCases+" zeroDamageReturnSubsteps="+returnSteps);
        }
        private static Projectile Copy(Projectile q)
        {var copy=(Projectile)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(q,null);copy.ai=(float[])q.ai.Clone();copy.localAI=(float[])q.localAI.Clone();copy.localNPCImmunity=(int[])q.localNPCImmunity.Clone();return copy;}
        private static Player Reset(object context,int type)
        {
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),type==182?1122:type==301?1513:type==866?4760:2424,0,0);Main.windPhysics=false;
            foreach(var n in Main.npc)n.active=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();return p;
        }
        private static NPC Target(int slot,Vector2 position,int width,int height)
        {
            var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.position=position;n.width=width;n.height=height;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=true;n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);return n;
        }
        private static NpcTrajectory Timeline(Assembly assembly,NPC n)
        {
            var selection=assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true);var key=(NpcIdentity)selection.GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=true};
            var points=Enumerable.Range(0,361).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,0,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
    }
}
