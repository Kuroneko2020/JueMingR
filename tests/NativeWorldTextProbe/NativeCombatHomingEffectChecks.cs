using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatHomingEffectChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static Projectile receiving,watched;private static int strikes;
        private static int readProbeDepth,playerReads;
        private static void PlayerRead(){if(readProbeDepth>0)playerReads++;}
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static void Damage(Projectile __instance){receiving=__instance;}private static void DamageDone(){receiving=null;}
        private static void Strike(NPC __instance,int __result){if(__result>0 && __instance.whoAmI==2 && ReferenceEquals(receiving,watched))strikes++;}
        private static NPC Target(int slot,Vector2 center)
        {var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;n.friendly=n.immortal=n.dontTakeDamage=false;Array.Clear(n.immune,0,n.immune.Length);return n;}
        private static NpcTrajectory Timeline(NPC n)
        {
            var identity=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);
            var state=new NpcMotionState{Identity=identity,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=n.noTileCollide};
            var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var terrain=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(terrain,"Reset");return terrain;}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static Player Reset(object context,int weapon=4952)
        {
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);
            p.position=new Vector2(850,328);p.velocity=Vector2.Zero;p.ResetEffects();p.statMana=p.statManaMax=p.statManaMax2=200;Main.windPhysics=false;
            foreach(var n in Main.npc)n.active=false;for(int x=45;x<115;x++)for(int y=10;y<50;y++)Main.tile[x,y].ClearEverything();Main.tileSolid[1]=true;return p;
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostHomingEffectAttack",true);var audit=new Harmony("JueMingR.Tests.HomingEffect");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHomingEffectChecks),nameof(Damage)),postfix:new HarmonyMethod(typeof(NativeCombatHomingEffectChecks),nameof(DamageDone)));
            audit.Patch(typeof(NPC).GetMethod("StrikeNPC",Flags,null,new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)},null),postfix:new HarmonyMethod(typeof(NativeCombatHomingEffectChecks),nameof(Strike)));
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.NpcPredictionSource",true).GetMethod("ReadPlayer",Flags),prefix:new HarmonyMethod(typeof(NativeCombatHomingEffectChecks),nameof(PlayerRead)));
            try{Births(context);Motors(context);Bolts(context);Receives(context);Bounces(context);DrawInputs(context);Console.WriteLine("PASS HOMING EFFECT original hand births, AI171/181/196, seeded ArcSurge geometry and actual first Damage/Strike boundaries.");}
            finally{Main.windPhysics=wind;audit.UnpatchAll(audit.Id);}
        }
        private static void DrawInputs(object context)
        {
            var p=Reset(context);var n=Target(2,new Vector2(1200,350));var q=new Projectile();q.SetDefaults(931);q.owner=p.whoAmI;var root=Obstacles(p,q,Timeline(n));Call(root,"CaptureHomingInputs",p,false,true);playerReads=0;readProbeDepth++;
            try{for(int i=0;i<200;i++)Require((bool)Get(root,"BirthInputsCurrent"),"captured scalar input remains presentation-current");}finally{readProbeDepth--;}
            Require(playerReads==0,"200 Draw input checks never re-enter the terrain-reading player capture");readProbeDepth++;try{Require((bool)Get(root,"Unchanged"),"complete initial input dependencies remain current");}finally{readProbeDepth--;}
            Require(playerReads>0,"real preparation/consume still checks the complete player motion premise");p.controlRight=!p.controlRight;Require(!(bool)Get(root,"BirthInputsCurrent") && !(bool)Get(root,"Unchanged"),"actual moving-player input edit revokes both presentation and consume");p.controlRight=!p.controlRight;float acceleration=p.runAcceleration;p.runAcceleration+=.01f;Require(!(bool)Get(root,"Unchanged"),"the full consume check retains nonvisual acceleration fields");p.runAcceleration=acceleration;
            Console.WriteLine("PASS HOMING light200 Draw checks0 player reads; full input capture retained and moving/control/acceleration edits revoke");
        }
        private static void Births(object context)
        {
            var ammoType=assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true);var shoot=typeof(Player).GetMethod("ItemCheck_Shoot",Flags);
            foreach(int weapon in new[]{4952,6154,6173})
            {
                var p=Reset(context,weapon);p.itemAnimationMax=p.HeldItem.useAnimation;p.itemAnimation=p.itemAnimationMax;
                Main.mouseX=1050-(int)Main.screenPosition.X;Main.mouseY=350-(int)Main.screenPosition.Y;
                var ammo=Static(ammoType,"Capture",p,p.HeldItem);var origin=p.RotatedRelativePoint(p.MountedCenter);var direction=(Main.MouseWorld-origin).SafeNormalize(Vector2.UnitX)*(float)Get(ammo,"Speed");
                shoot.Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});int type=p.HeldItem.shoot;var shots=Main.projectile.Where(q=>q.active && q.type==type).ToArray();
                Require(shots.Length==(weapon==6154?3:1),"original magic hand creates the actual parent count, with no fictitious later mouse stream: "+weapon);
                foreach(var q in shots)
                {
                    Require(q.damage==(int)Get(ammo,"Damage") && q.ai[0]==-1,"ordinary captured damage and native initial target clock agree: "+type);
                    if(type==1122){var arcExpected=Main.MouseWorld;p.LimitPointToPlayerReachableArea(ref arcExpected);float distance=Vector2.Distance(arcExpected,origin),minimum=(float)Get(ammo,"Speed")*20;if(distance<minimum)arcExpected+=direction.SafeNormalize(Vector2.UnitX)*(minimum-distance);Console.WriteLine("ARC BIRTH actual="+q.Center+" expected="+arcExpected+" v="+q.velocity+" life="+q.timeLeft+" origin="+origin+" speed="+Get(ammo,"Speed"));Require(Vector2.Distance(q.Center,arcExpected)<=20.001f && q.velocity==Vector2.Zero && q.timeLeft==1,"actual ArcSurge has cursor-circle/minimum-range birth and first-action lifetime, not a240-action body");continue;}
                    object[] args={p,p.direction,Terrain(),false};var expected=(Vector2)Static(model,"StaffBirth",args);Require((bool)args[3] && Vector2.Distance(q.Center,expected)<.001f,"actual staff native mount offset/fallback matches the prepared birth");
                    var random=type==1116?(q.velocity-direction*2)/2:q.velocity;
                    Require(Math.Abs(random.X)<=4.001f && random.Y<=.001f && random.Y>=-(type==1116?3.001f:4.001f),"actual random hand impulse is inside its native circle-plus-edge and upward-fold support");
                }
            }
            int rows=0;
            foreach(int ammunition in new[]{40,41,47,51,265,516,545,988,1235,1334,1341,3003,3103,3568,5348})
            {
                var p=Reset(context,4953);p.inventory[54].SetDefaults(ammunition);p.inventory[54].stack=999;
                foreach(int stage in Enumerable.Range(0,5))
                {
                    foreach(var shot in Main.projectile)shot.active=false;p.inventory[54].stack=999;p.itemAnimationMax=p.HeldItem.useAnimation;p.itemAnimation=p.itemAnimationMax-stage*2;
                    Main.mouseX=1050-(int)Main.screenPosition.X;Main.mouseY=350-(int)Main.screenPosition.Y;var cursor=Main.MouseWorld;var origin=p.RotatedRelativePoint(p.MountedCenter);var ammo=Static(ammoType,"Capture",p,p.HeldItem);
                    shoot.Invoke(p,new object[]{p.whoAmI,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var q=Main.projectile.Single(s=>s.active);object[] args={p,origin,cursor,(float)Get(ammo,"Speed"),stage,Terrain(),Vector2.Zero,Vector2.Zero};Require((bool)Static(model,"EventideBirth",args),"the native Eventide birth read set is available");
                    int type=stage==2?932:(int)Get(ammo,"Projectile");var expected=(Vector2)args[7];if(type==932)expected=expected.SafeNormalize(Vector2.Zero)*(float)Get(ammo,"Speed")*2;
                    else{object[] motionArgs={ammo,default(AttackMotion)};Require((bool)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackModels",true),"TryRead",motionArgs),"every actual Eventide non932 ammo has its declared native motor");var motion=(AttackMotion)motionArgs[1];motion.Launch(ref expected.X,ref expected.Y);}
                    Require(q.type==type && q.damage==(int)Get(ammo,"Damage")*(stage==2?2:1) && Vector2.Distance(q.Center,(Vector2)args[6])<.001f && Vector2.Distance(q.velocity,expected)<.001f,"each original Eventide hand ordinal preserves its actual ammo, displaced birth, native speed and central double damage: "+ammunition+"/"+stage);rows++;
                }
            }
            Console.WriteLine("PASS HOMING BIRTH magic3 and Eventide actual ammunition15 x handOrdinal5="+rows);
        }
        private static void Motors(object context)
        {
            foreach(int type in new[]{931,932,1116})
            {
                var p=Reset(context);var n=Target(2,new Vector2(1300,350));var path=Timeline(n);var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(1000,350),new Vector2(2,-1),type,50,0,p.whoAmI,-1)];
                var shadow=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",q);var terrain=Terrain();var obstacles=Obstacles(p,q,path);var targets=Call(obstacles,"CaptureChaseTargets",q);
                int count=type==931?222:75;
                for(int step=0;step<count;step++)
                {
                    Require((bool)Static(model,"Ai",shadow,targets,path,1,terrain),"homing model obtains every required target/terrain premise");q.AI();
                    Require(Vector2.Distance(q.velocity,shadow.velocity)<.0001f && q.ai[0]==shadow.ai[0] && q.timeLeft==shadow.timeLeft,"original autonomous AI velocity/target/lifetime equals private motor: "+type+"/"+step);
                    q.position+=q.velocity;shadow.position+=shadow.velocity;q.timeLeft--;shadow.timeLeft--;
                }
                Console.WriteLine("PASS HOMING MOTOR type="+type+" nativeActions="+count+" endpoint="+q.Center+" v="+q.velocity);
            }
        }
        private static void Bolts(object context)
        {
            Reset(context,6173);var start=new Vector2(850,350);var end=new Vector2(1050,350);
            foreach(bool blocked in new[]{false,true})
            {
                if(blocked)for(int y=15;y<30;y++)NativeToolsChecks.Tile(60,y,1);
                for(uint seed=0;seed<4;seed++)
                {
                    var terrain=Terrain();object[] args={seed,start,end,terrain,null};Require((bool)Static(model,"ArcPoints",args),"leased seeded lightning geometry is available");var actual=LightningGenerator.GetArcSurgeWeaponGenerator(start,end).GenerateMainBoltPath(seed,end,end-start);
                    Require(((Vector2[])args[4]).SequenceEqual(actual.positions),"private LCG/native seed gives every identical original point including first solid stop: "+blocked+"/"+seed);
                }
            }
            Console.WriteLine("PASS HOMING BOLT seeds=4 open/solid leased native multipoint prefix");
        }
        private static void Receives(object context)
        {
            foreach(int type in new[]{931,932,1116,1122,1039})foreach(int mode in new[]{0,1,2})
            {
                var p=Reset(context,type==1122?6173:4952);var n=Target(2,new Vector2(1000,350));if(mode==2)Target(1,n.Center);
                var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),n.Center,Vector2.Zero,type,50,0,p.whoAmI,-1,0,0)];
                if(q.usesIDStaticNPCImmunity && mode!=1)Projectile.perIDStaticNPCImmunity[q.immunityIdentity,2]=0;
                if(mode==1){if(q.usesLocalNPCImmunity)q.localNPCImmunity[2]=200;else Projectile.perIDStaticNPCImmunity[q.immunityIdentity,2]=Main.GameUpdateCount+200;}
                NativeQuickItemChecks.BeginWorldStep();var path=Timeline(n);var known=(AttackContact)Static(model,"Known",p,q,path,Terrain(),Obstacles(p,q,path));strikes=0;watched=q;q.Update(q.whoAmI);watched=null;
                Console.WriteLine("HOMING RECEIVE type="+type+" mode="+mode+" predicted="+known?.Tick+" actualStrikes="+strikes+" nativePen="+q.penetrate);
                Require((known!=null)==(strikes>0) && (known==null || known.Tick==1),"known free family actual first Damage/Strike and immunity/slot order agree: "+type+"/"+mode);
                if(mode==0)Require(strikes>0,"original family delivers its real damaging stage");
            }
        }
        private static void Bounces(object context)
        {
            foreach(int type in new[]{931,1039})
            {
                var p=Reset(context);var n=Target(2,new Vector2(1750,600));var path=Timeline(n);for(int y=10;y<45;y++)NativeToolsChecks.Tile(62,y,1);
                var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950,350),new Vector2(8,0),type,50,0,p.whoAmI,-1)];var shadow=(Projectile)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostNebulaBlazeAttack",true),"Copy",q);var terrain=Terrain();var targets=Call(Obstacles(p,q,path),"CaptureChaseTargets",q);bool reflected=false;
                for(int outer=0;outer<16;outer++)
                {
                    for(int sub=0;sub<=shadow.extraUpdates;sub++){Require((bool)Static(model,"Step",shadow,targets,path,1,terrain),"streak finite tile action has complete terrain premises");shadow.timeLeft--;}
                    q.Update(q.whoAmI);reflected|=q.velocity.X<0;
                    Require(q.active && shadow.active && Vector2.Distance(q.Center,shadow.Center)<.001f && Vector2.Distance(q.velocity,shadow.velocity)<.0001f && q.timeLeft==shadow.timeLeft,"original931/1039 tile response reflects rather than retiring the autonomous body: "+type+"/"+outer);
                }
                Require(reflected,"original streak actually crossed its native wall-bounce branch");Console.WriteLine("PASS HOMING BOUNCE type="+type+" originalOuter=16 endpoint="+q.Center);
            }
        }
    }
}
