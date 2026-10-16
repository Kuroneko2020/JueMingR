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
    internal static class NativeCombatSonicBoomChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,watched;private static object ammo;
        private static bool recordBirth;private static object naturalAttack;private static Vector2 raw;private static long first;private static int firstSub,solves,knownCalls,depth,steps,scans,rngCalls,kills;
        private sealed class Birth{internal Projectile Shot;internal int Key,Slot,FirstSub=-1;internal long Tick,First=-1;internal Vector2 Origin,Center,Velocity,Cursor;internal AttackContact Known;}
        private static readonly List<Birth> born=new List<Birth>();private static readonly List<int> hitSlots=new List<int>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static int cancelledBeforeCapture;
        private static void SolveWork(out int __state){__state=scans;solves++;depth++;}private static void KnownWork(out int __state){__state=scans;knownCalls++;depth++;}
        // A normal product budget cancellation bypasses postfixes. Preserve
        // its exception, close only this observer depth, and count an attempt
        // cancelled before capture separately from a completed shared scene.
        private static Exception Done(int __state,Exception __exception){depth--;if(__exception is CombatPredictionBudgetExceededException && scans==__state)cancelledBeforeCapture++;return __exception;}
        private static void StepWork(){steps++;}private static void ScanWork(){scans++;}private static void RngWork(){if(depth>0)rngCalls++;}
        private static void BeforeKill(Projectile __instance){if(__instance.active && __instance.type==684)kills++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            // Actual positive projectile Strike is separate from same-use
            // hand melee and from immortal488's unchanged life.
            if(receiving==null || receiving.type!=684 || receiving.owner!=Main.myPlayer || __result<=0)return;hitSlots.Add(__instance.whoAmI);
            if(!ReferenceEquals(__instance,target) || watched!=null && !ReferenceEquals(watched,receiving))return;
            int sub=receiving.extraUpdates-receiving.numUpdates-1;if(first<0){first=Main.GameUpdateCount;firstSub=sub;}
            foreach(var b in born)if(ReferenceEquals(b.Shot,receiving) && b.Key==(int)receiving.key && b.First<0){b.First=Main.GameUpdateCount;b.FirstSub=sub;}
        }
        private static Player Reset(object context)
        {
            recordBirth=false;naturalAttack=null;receiving=watched=null;target=null;first=-1;firstSub=-1;kills=0;born.Clear();hitSlots.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3827,0,0);p.releaseUseItem=true;Main.windPhysics=false;
            for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Path(NPC n)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var value=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(value,"Reset");return value;}
        private static object Receivers(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path,string phase="BeforeNpc"){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),0},null);}
        private static Projectile Shot(Player p,Vector2 center,Vector2 velocity,int damage=38){return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,684,damage,0,p.whoAmI)];}
        private static AttackContact Known(Player p,Projectile q,NpcTrajectory path){return (AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Receivers(p,q,path));}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!recordBirth || __result<0)return;var q=Main.projectile[__result];if(q.type!=684)return;if(naturalAttack!=null)Require((bool)Get(naturalAttack,"hasConsumedInput") && Convert.ToString(Get(Get(naturalAttack,"consumedInput"),"Kind"))=="Enhanced","every actual Sonic hand consumes its ready enhanced cursor");var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=Main.MouseWorld;
            var center=(Vector2)Static(model,"BirthOrigin",origin,cursor-origin,p.direction);var beforeClamp=(Vector2)Static(model,"BirthVelocity",center,cursor,(float)Get(ammo,"Speed"),p.direction);var velocity=(Vector2)Static(model,"LimitLaunch",beforeClamp);
            var row=new Birth{Shot=q,Key=(int)q.key,Slot=q.whoAmI,Tick=Main.GameUpdateCount,Origin=origin,Center=q.Center,Velocity=q.velocity,Cursor=cursor,Known=Known(p,q,Path(target))};born.Add(row);
            Console.WriteLine("SONIC born tick="+row.Tick+" slot/key="+row.Slot+"/"+row.Key+" facing="+p.direction+" origin/center="+origin+"/"+q.Center+" rawV/actualV="+beforeClamp+"/"+q.velocity+" damage="+q.damage+" cursor/raw="+cursor+"/"+raw+" known="+row.Known?.Tick+"/"+row.Known?.Subupdate);
            Require(!(__0 is EntitySource_Parent parent && parent.Entity is Projectile) && p.direction==(int)Static(model,"ShotDirection",p,cursor-origin) && q.melee && q.width==16 && q.height==16 && q.penetrate==5 && q.maxPenetrate==5 && q.timeLeft==60 && q.extraUpdates==0 && !q.tileCollide && q.damage==(int)Get(ammo,"Damage") && Vector2.DistanceSquared(center,q.Center)<.000001f && Vector2.DistanceSquared(velocity,q.velocity)<.000001f,"native Sonic hand changes facing, applies24px offset, re-aims from that origin, then limits AI001 components and sets born60 lifetime");
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostSonicBoomAttack",true);var audit=new Harmony("JueMingR.Tests.SonicBoom");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(SolveWork)),finalizer:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(Done)));audit.Patch(model.GetMethod("KnownFamily",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(KnownWork)),finalizer:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(Done)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(StepWork)));audit.Patch(model.GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(ScanWork)));
            foreach(var method in Main.rand.GetType().GetMethods(Flags).Where(m=>m.Name=="Next" || m.Name=="NextDouble").Distinct())audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(RngWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(Born)));audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatSonicBoomChecks),nameof(Strike)));
            var componentHand=new NativeCombatHandFixture(context,()=>recordBirth && naturalAttack!=null,()=>target);
            try{string leaf=Environment.GetEnvironmentVariable("JMR_SONIC_BOOM_ORACLE_ONLY");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="birth" || leaf=="receive" || leaf=="natural","Sonic leaf selector");if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);if(string.IsNullOrEmpty(leaf) || leaf=="birth")Births(context);if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);if(string.IsNullOrEmpty(leaf) || leaf=="natural")Natural(context);}
            finally{componentHand.Dispose();recordBirth=false;naturalAttack=null;receiving=watched=null;target=null;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Motors(object context)
        {
            int actions=0;for(int scene=0;scene<10;scene++)
            {
                var p=Reset(context);target=Target(2,new Vector2(1750,500));
                if(scene>=1 && scene<=3)for(int x=5;x<115;x++)for(int y=10;y<40;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                if(scene==4)for(int y=10;y<40;y++)NativeToolsChecks.Tile(60,y,1);
                if(scene==8 || scene==9)for(int x=5;x<115;x++){Main.tile[x,21].liquid=128;Main.tile[x,21].liquidType(3);}
                var q=Shot(p,scene==7?new Vector2(7,350):new Vector2(950,350),new Vector2(8,scene==8?2:scene==9?-2:0));if(scene==5)q.timeLeft=1;if(scene==6)q.penetrate=0;var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();
                for(int outer=1;outer<=60 && q.active;outer++)
                {
                    Require((bool)Static(model,"Step",copy,terrain),"Sonic native motor has finite scene dependencies");actions++;if(copy.active)Static(model,"Finish",copy);q.Update(q.whoAmI);
                    Console.WriteLine("SONIC motor "+scene+"/"+outer+" native="+q.position+"/"+q.velocity+" ai/local="+q.ai[0]+","+q.localAI[0]+" alpha/left/active="+q.alpha+","+q.timeLeft+","+q.active+" model="+copy.position+"/"+copy.velocity+" ai/local="+copy.ai[0]+","+copy.localAI[0]+" alpha/left/active="+copy.alpha+","+copy.timeLeft+","+copy.active);
                    Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.alpha==copy.alpha && q.ai.SequenceEqual(copy.ai) && q.localAI.SequenceEqual(copy.localAI) && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.shimmerWet==copy.shimmerWet && q.direction==copy.direction && Vector2.DistanceSquared(q.position,copy.position)<.0001f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.000001f,"native684 AI001 no-gravity, liquid tail and actual Kill/retirement motor agree");
                }
                if(scene==3 || scene==8)Require(kills==1 && !q.active && q.timeLeft==0,"full Shimmer Kill or downward partial reversal exhausting born60 invokes real Kill");
                if(scene==7)Require(kills==0 && !q.active && q.timeLeft==60,"body boundary retires without native Kill");
                if(scene==9)Require(q.localAI[0]>1 && q.ai[0]==0,"upward partial Shimmer retains native no-gravity motor");
            }
            Console.WriteLine("PASS SONIC motors10 actions="+actions);
        }
        private static void Births(object context)
        {
            foreach(int direction in new[]{1,-1})
            {
                var p=Reset(context);p.position=new Vector2(650,646);p.direction=direction;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+direction*180;raw=target.Center;
                ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=true;for(int frame=0;frame<24;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,frame==0);recordBirth=false;Require(born.Count==1,"one physical press/release emits exactly one actual Sonic shot");
            }
            Console.WriteLine("PASS SONIC native hand births2");
        }
        private static void Receives(object context)
        {
            int cases=0;for(int mode=0;mode<8;mode++)
            {
                var p=Reset(context);bool edge=mode==0 || mode==5 || mode==6;target=Target(2,new Vector2(965,mode==6?400:edge?380:350),edge?2:18,edge?2:40);if(mode==1)target.immune[p.whoAmI]=200;if(mode==4 || mode==5)target.reflectsProjectiles=true;
                watched=Shot(p,new Vector2(950,350),new Vector2(15,0),mode==3?0:38);NPC front=null;if(mode==2)front=Target(1,new Vector2(965,350));if(mode==7)for(int y=10;y<40;y++)NativeToolsChecks.Tile(60,y,1);
                var expected=(Projectile)Static(model,"Copy",watched);expected.position+=expected.velocity;
                if(edge)Require(!expected.Hitbox.Intersects(target.Hitbox) && expected.Colliding(expected.Hitbox,target.Hitbox)==(mode!=6) && (bool)Static(model,"Collides",expected,new MotionRect(target.position.X,target.position.Y,target.width,target.height))==(mode!=6),"native transverse line has a real off-body positive and beyond-line negative; raw reflection body remains separate");
                NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var contact=Known(p,watched,path);long sample=path.SampleTick;
                Require(Static(model,"Known",p,watched,path,Clock(path,"Projectiles"),Terrain(),Receivers(p,watched,path))==null,"Sonic existing body oracle declines partial projectile-array epochs");
                for(int outer=1;outer<=10 && watched.active && watched.friendly;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(1);target.UpdateNPC(2);watched.Update(watched.whoAmI);}
                int actual=first<0?0:(int)(first-sample);bool negative=mode==1 || mode==3 || mode==4 || mode==6;
                Console.WriteLine("SONIC receive mode="+mode+" actual="+actual+" known="+contact?.Tick+"/"+contact?.Subupdate+" slots="+string.Join(",",hitSlots)+" friendly/hostile="+watched.friendly+","+watched.hostile+" owner="+target.immune[p.whoAmI]);
                Require((actual==0)==negative && (contact==null)==negative && (contact==null || contact.Tick==actual && contact.Subupdate==firstSub),"native transverse Damage, ordered owner history, finite budget and raw-body reflection agree");
                if(mode==2)Require(hitSlots.Contains(1) && hitSlots.Contains(2),"pen5 truly receives lower-front then selected in the same Damage call");if(mode==4)Require(watched.hostile && !watched.friendly,"raw NPC-body reflection really converts the shot before useful Strike");cases++;
            }
            ScaleLease(context);Console.WriteLine("PASS SONIC receivers="+cases+" plus actual scale-only family lease");
        }
        private static void ScaleLease(object context)
        {
            var p=Reset(context);target=Target(2,new Vector2(965,400),2,2);var q=Shot(p,new Vector2(950,350),new Vector2(15,0));var at=(Projectile)Static(model,"Copy",q);at.position+=at.velocity;
            Require(!at.Colliding(at.Hitbox,target.Hitbox),"scale1 transverse endpoint is strictly outside selected");at.scale=2;Require(at.Colliding(at.Hitbox,target.Hitbox) && (bool)Static(model,"Collides",at,new MotionRect(target.position.X,target.position.Y,target.width,target.height)),"native scale2 alone expands the actual line into selected without altering raw body");
            NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var receiver=Receivers(p,q,path);Static(model,"Known",p,q,path,Clock(path),Terrain(),receiver);Require((bool)Get(receiver,"Unchanged"),"real captured Sonic family starts current");
            float original=q.scale;q.scale=original*2;Require(!(bool)Get(receiver,"Unchanged"),"changing only a captured684 scale must reject its collision/history lease");q.scale=original;Require((bool)Get(receiver,"Unchanged"),"restoring only scale restores the exact captured collision premise");
        }
        private static void Draws(object host,object attack,AttackContact plan)
        {
            int a=solves,b=steps,c=scans;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int check=0;check<20;check++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PresentationCurrent",plan),"stable Sonic preparation remains current");}Require(a==solves && b==steps && c==scans,"twenty CPU presentations have zero Sonic solve/motor/family scans");
        }
        private static void ExistingFlight(Player player,object attack)
        {
            // The actual first-born shot is already independently checked
            // against native Strike. Compare its remaining clock in this
            // completed-world phase with the production displayed plan.
            var shot=born[0].Shot;Require(shot.active && (int)shot.key==born[0].Key,"first real Sonic birth still owns its observed flight");
            var path=Path(target);
            var receivers=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{player,shot,path,0,false,true},null);
            var live=(AttackContact)Static(model,"Known",player,shot,path,Clock(path,"CompletedWorld"),Terrain(),receivers);
            var phase=Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld");var args=new object[]{player,path,phase,false};object flight;
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))flight=Call(attack,"ReadFlight",args);
            var observed=flight==null?null:Get(flight,"Contact") as AttackContact;
            Require((bool)args[3] && live!=null && observed!=null && observed.Tick==live.Tick && observed.Subupdate==live.Subupdate && (bool)Get(flight,"Current") && (bool)Get(flight,"DependenciesCurrent"),"public Flight retains the actual Sonic family's earliest sealed receive independently of any later hand");
            Console.WriteLine("SONIC actual live sample="+path.SampleTick+" observed="+observed.Tick+"/"+observed.Subupdate+" sealed="+live.Tick+"/"+live.Subupdate);

        }
        private static void Natural(object context)
        {
            foreach(int direction in new[]{1,-1})
            {
                var p=Reset(context);p.position=new Vector2(650,646);p.direction=direction;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+direction*180;raw=target.Center-new Vector2(direction*380,0);
                solves=knownCalls=depth=steps=scans=rngCalls=cancelledBeforeCapture=0;var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int frame=0;frame<3;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);
                Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active) && solves==0 && scans==0,"withdrawn idle Sonic preview never solves, captures or fires");
                ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=true;naturalAttack=attack;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));int previews=0;
                for(int frame=0;frame<45;frame++)
                {
                    NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);var next=(AttackContact)GetOptional(attack,"ExpectedImpact");
                    if(next!=null){previews++;Call(attack,"BindPresentation",next);Draws(host,attack,next);}
                    NativeCombatSelectionPreviewChecks.Step(context,raw,true);Require(GetOptional(attack,"ExpectedPreview")==null,"ongoing Sonic native cadence never restores an idle or cooldown preview");
                    if(frame==6)ExistingFlight(p,attack);
                }
                NativeCombatSelectionPreviewChecks.Step(context,raw,false);recordBirth=false;for(int frame=0;frame<60 && born.Any(b=>b.First<0 && b.Key==(int)b.Shot.key && b.Shot.active);frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
                foreach(var b in born){int actual=b.First<0?0:(int)(b.First-b.Tick+1);Console.WriteLine("SONIC natural direction="+direction+" slot/key="+b.Slot+"/"+b.Key+" born="+b.Tick+" center/V="+b.Center+"/"+b.Velocity+" cursor/raw="+b.Cursor+"/"+raw+" actual="+actual+" known="+b.Known?.Tick+"/"+b.Known?.Subupdate);Require(actual>0 && b.Known!=null && b.Known.Tick==actual && b.Known.Subupdate==b.FirstSub && Vector2.Dot(b.Velocity,raw-b.Origin)<0,"actual assisted Sonic projectile has positive immortal488 Strike matching its Known first clock, independent of hand melee");}
                Require(born.Count>=3 && previews>=3 && rngCalls==0 && depth==0 && scans+cancelledBeforeCapture==solves+knownCalls,"first/continuous real Sonic uses and finite source preparations without model RNG");Console.WriteLine("PASS SONIC natural direction="+direction+" births="+born.Count+" realUpdates=45 readyPreparations="+previews+" solve/known/capture/motor="+solves+","+knownCalls+","+scans+","+steps+" RNG="+rngCalls+" depth="+depth+" cancelledBeforeCapture="+cancelledBeforeCapture);
            }
        }
    }
}
