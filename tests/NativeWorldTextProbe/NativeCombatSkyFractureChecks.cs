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
    internal static class NativeCombatSkyFractureChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,watched;
        private static bool recordBirth;private static object ammo;private static Vector2 raw;private static long first;
        private static int solves,knownCalls,depth,steps,scans,rngCalls,kills;
        private sealed class Birth
        {internal Projectile Shot;internal int Key,Slot,Damage,Animation,Time,Reuse;internal long Tick,First=-1;internal Vector2 Center,Velocity,Origin,Cursor;internal AttackContact Known;}
        private static readonly List<Birth> born=new List<Birth>();private static readonly List<int> hitSlots=new List<int>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;depth++;}private static void KnownWork(){knownCalls++;depth++;}private static void Done(){depth--;}
        private static void StepWork(){steps++;}private static void ScanWork(){scans++;}private static void RngWork(){if(depth>0)rngCalls++;}
        private static void BeforeKill(Projectile __instance){if(__instance.type==660)kills++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(receiving==null || receiving.type!=660 || receiving.owner!=Main.myPlayer || receiving.damage<=0 || __result<=0)return;hitSlots.Add(__instance.whoAmI);
            if(!ReferenceEquals(__instance,target) || watched!=null && !ReferenceEquals(watched,receiving))return;if(first<0)first=Main.GameUpdateCount;
            foreach(var b in born)if(ReferenceEquals(b.Shot,receiving) && b.Key==(int)receiving.key && b.First<0)b.First=Main.GameUpdateCount;
        }
        private static Player Reset(object context)
        {
            recordBirth=false;receiving=watched=null;target=null;first=-1;kills=0;born.Clear();hitSlots.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),3787,0,0);p.statMana=p.statManaMax=p.statManaMax2=300;p.releaseUseItem=true;Main.windPhysics=false;
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
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Receivers(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path,string phase="BeforeNpc"){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),0},null);}
        private static Projectile Shot(Player p,Vector2 center,Vector2 velocity,int damage=38){return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,660,damage,0,p.whoAmI)];}
        private static AttackContact Known(Player p,Projectile q,NpcTrajectory path){return (AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Receivers(p,q,path));}
        private static void BirthArgs(Vector2 origin,Vector2 cursor,float speed,Vector2 offset,out Vector2 center,out Vector2 velocity)
        {object[] args={origin,cursor,speed,offset,Vector2.Zero,Vector2.Zero};Static(model,"Birth",args);center=(Vector2)args[4];velocity=(Vector2)args[5];}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!recordBirth || __result<0)return;var q=Main.projectile[__result];if(q.type!=660)return;var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=Main.MouseWorld;var offset=q.Center-origin;Vector2 center,velocity;BirthArgs(origin,cursor,(float)Get(ammo,"Speed"),offset,out center,out velocity);var path=Path(target);
            var row=new Birth{Shot=q,Key=(int)q.key,Slot=q.whoAmI,Tick=Main.GameUpdateCount,Damage=q.damage,Animation=p.itemAnimation,Time=p.itemTime,Reuse=p.reuseDelay,Center=q.Center,Velocity=q.velocity,Origin=origin,Cursor=cursor,Known=Known(p,q,path)};born.Add(row);
            Console.WriteLine("SKY FRACTURE born tick="+row.Tick+" slot/key="+row.Slot+"/"+row.Key+" center="+row.Center+" origin="+origin+" radius="+offset.Length()+" V="+row.Velocity+" damage="+q.damage+" anim/time/reuse="+row.Animation+","+row.Time+","+row.Reuse+" cursor="+cursor+" raw="+raw+" known="+row.Known?.Tick);
            // NewProjectile stores the requested float center as top-left.
            // +/-1/8192 bounds only that two-operation coordinate rounding,
            // not a larger legal random ring or trajectory tolerance.
            Require(!(__0 is EntitySource_Parent source && source.Entity is Projectile) && offset.Length()>=20f-1f/8192 && offset.Length()<=60f+1f/8192 && q.magic && q.ignoreWater && q.width==10 && q.height==10 && q.extraUpdates==1 && q.timeLeft==600 && q.penetrate==1 && q.damage==(int)Get(ammo,"Damage") && Vector2.DistanceSquared(q.velocity,velocity)<.000001f,"actual single hand birth has legal20..60 ring and native two-direction Lerp, rather than a sky origin or instant three-member spawn");
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostSkyFractureAttack",true);var audit=new Harmony("JueMingR.Tests.SkyFracture");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(SolveWork)),postfix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(Done)));audit.Patch(model.GetMethod("KnownFamily",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(KnownWork)),postfix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(Done)));audit.Patch(model.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(StepWork)));audit.Patch(model.GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(ScanWork)));
            foreach(var method in Main.rand.GetType().GetMethods(Flags).Where(m=>m.Name=="Next" || m.Name=="NextDouble").Distinct())audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(RngWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(Born)));audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatSkyFractureChecks),nameof(Strike)));
            try
            {
                string leaf=Environment.GetEnvironmentVariable("JMR_SKY_FRACTURE_ORACLE_ONLY");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="birth" || leaf=="receive" || leaf=="natural","SkyFracture leaf selector");
                if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);if(string.IsNullOrEmpty(leaf) || leaf=="birth")Births(context);if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);if(string.IsNullOrEmpty(leaf) || leaf=="natural")Natural(context);
            }
            finally{recordBirth=false;receiving=watched=null;target=null;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Motors(object context)
        {
            int actions=0;for(int scene=0;scene<14;scene++)
            {
                var p=Reset(context);target=Target(2,new Vector2(1750,450));if(scene>=1 && scene<=3)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}if(scene==4)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                if(scene>=5 && scene<=8)for(int x=60;x<65;x++){NativeToolsChecks.Tile(x,22,1);Main.tile[x,22].slope((byte)(scene-4));}
                if(scene==10 || scene==11)for(int x=40;x<115;x++){Main.tile[x,21].liquid=255;Main.tile[x,21].liquidType(3);}
                var q=Shot(p,scene==13?new Vector2(10,350):new Vector2(950,scene>=5 && scene<=8?340:350),scene==11?new Vector2(6,-2):new Vector2(scene==4?24:6,scene>=5 && scene<=8 || scene==10?2:0));if(scene==9){q.velocity=new Vector2(6,22);q.alpha=0;q.frame=11;}if(scene==12)q.timeLeft=1;var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();
                for(int outer=1;outer<=20 && q.active;outer++)
                {
                    int updates=q.extraUpdates+1;for(int sub=0;sub<updates;sub++){Require((bool)Static(model,"Step",copy,terrain),"SkyFracture motor dependencies are present");actions++;if(!copy.active)break;Static(model,"Finish",copy);if(!copy.active){if(sub+1<updates)Require((bool)Static(model,"RetiredStep",copy,terrain),"inactive tail movement has no repeated AI/Damage");break;}}
                    q.Update(q.whoAmI);Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.penetrate==copy.penetrate && q.maxPenetrate==copy.maxPenetrate && q.alpha==copy.alpha && q.scale==copy.scale && q.ai.SequenceEqual(copy.ai) && q.rotation==copy.rotation && q.direction==copy.direction && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.shimmerWet==copy.shimmerWet && Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.0001f,"native SkyFracture finite motor "+scene+"/"+outer+" actual="+q.position+"/"+q.velocity+"/left="+q.timeLeft+"/active="+q.active+"/alpha="+q.alpha+"/scale="+q.scale+"/shimmer="+q.shimmerWet+" model="+copy.position+"/"+copy.velocity+"/left="+copy.timeLeft+"/active="+copy.active+"/alpha="+copy.alpha+"/scale="+copy.scale+"/shimmer="+copy.shimmerWet);
                }
                if(scene==9)Require(q.frame==11 && copy.frame==11 && q.velocity.Y==16,"already chosen visual frame stays fixed while AI001 applies its real fall-speed cap");if(scene==13)Require(kills==0 && q.timeLeft>0,"world edge retires without native Kill");
            }
            Console.WriteLine("PASS SKY FRACTURE motor14 actions="+actions+" visual random frame excluded from Damage state");
        }
        private static void Births(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+180;raw=target.Center;ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=true;
            for(int frame=0;frame<11;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,frame==0);Console.WriteLine("SKY FRACTURE first released action count="+born.Count+" ticks="+string.Join(",",born.Select(b=>b.Tick)));
            Require(born.Count==3 && born[1].Tick-born[0].Tick==4 && born[2].Tick-born[1].Tick==4 && born.All(b=>b.Reuse==18),"one real press then release still finishes three native4-separated hand births with reuse18, never instant three-shot synthesis");
            recordBirth=false;
        }
        private static void Receives(object context)
        {
            int cases=0;for(int mode=0;mode<6;mode++)
            {
                var p=Reset(context);target=Target(2,new Vector2(mode==5?980:995,350));if(mode==1)target.immune[p.whoAmI]=200;if(mode==4)target.reflectsProjectiles=true;NPC front=mode==2?Target(1,new Vector2(970,350)):null;watched=Shot(p,new Vector2(950,350),new Vector2(8.75f,0));if(mode==3)watched.damage=0;
                NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var contact=Known(p,watched,path);long sample=path.SampleTick;Require(Static(model,"KnownFamily",p,new[]{watched},path,Clock(path,"Projectiles"),Terrain(),Receivers(p,watched,path))==null,"whole-family replay declines an unknown partial-array slot epoch");
                for(int outer=1;outer<=30 && watched.active;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(1);target.UpdateNPC(2);watched.Update(watched.whoAmI);}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("SKY FRACTURE receive mode="+mode+" native="+actual+" known="+contact?.Tick+" slotHits="+string.Join(",",hitSlots)+" owner="+target.immune[p.whoAmI]);bool negative=mode==2 || mode==3 || mode==4;
                Require((contact==null)==negative && (actual==0)==negative && (contact==null || contact.Tick==actual),"native single-penetration body/other receiver/owner bypass/zero damage/reflection agrees with known source");if(mode==2)Require(hitSlots.Contains(1) && !hitSlots.Contains(2),"actual lower receiver consumes penetration before selected body");cases++;
            }
            LegalRandomControls(context);Console.WriteLine("PASS SKY FRACTURE receives="+cases+" plus2 legal ring branches");
        }
        private static void LegalRandomControls(object context)
        {
            foreach(bool misses in new[]{false,true})
            {
                var p=Reset(context);target=Target(2,new Vector2(1140,350));var origin=new Vector2(950,350);var cursor=origin+Vector2.UnitX*10000;Vector2 center,velocity;BirthArgs(origin,cursor,8.75f,misses?new Vector2(0,59):new Vector2(40,0),out center,out velocity);watched=Shot(p,center,velocity);NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var contact=Known(p,watched,path);long sample=path.SampleTick;
                for(int outer=1;outer<=120 && watched.active;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);watched.Update(watched.whoAmI);}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("SKY FRACTURE legal ring misses="+misses+" center="+center+" V="+velocity+" native="+actual+" known="+contact?.Tick);
                Require((actual==0)==misses && (contact==null)==misses && (contact==null || contact.Tick==actual),"same lawful cursor can yield useful or missed random ring branch, with each actual-born replay equal to native; representative cannot promise every random birth hits");
            }
        }
        private static void Draws(object host,object attack,AttackContact plan)
        {
            int a=solves,b=steps,c=scans;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;
            for(int check=0;check<20;check++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",plan),"stable SkyFracture preview lease remains current");}
            Require(a==solves && b==steps && c==scans,"twenty presentations perform zero SkyFracture Solve/motor/pool scans");
        }
        private static void Natural(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+180;raw=target.Center-new Vector2(380,0);solves=knownCalls=depth=steps=scans=rngCalls=0;var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int frame=0;frame<3;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);var plan=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(plan!=null && plan.Confidence==AttackConfidence.Representative && !Main.projectile.Any(q=>q.active),"idle random-ring next hand preview is read-only and does not fire");Draws(host,attack,plan);
            ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));int previews=0;
            for(int frame=0;frame<45;frame++){NativeCombatSelectionPreviewChecks.Step(context,raw,true);var next=(AttackContact)GetOptional(attack,"ExpectedPreview");if(frame>=2){Require(next!=null,"continuous three-hand action and reuse18 retain a legal next-hand preview");previews++;}if(frame==6 && next!=null)Draws(host,attack,next);}
            NativeCombatSelectionPreviewChecks.Step(context,raw,false);recordBirth=false;for(int frame=0;frame<120 && born.Any(b=>b.First<0 && b.Key==(int)b.Shot.key && b.Shot.active);frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
            int useful=0,missed=0;foreach(var b in born)
            {
                int actual=b.First<0?0:(int)(b.First-b.Tick+1);Console.WriteLine("SKY FRACTURE natural slot/key="+b.Slot+"/"+b.Key+" born="+b.Center+" V="+b.Velocity+" cursor="+b.Cursor+" first="+actual+" known="+b.Known?.Tick);
                Require((b.Known==null)==(actual==0) && (b.Known==null || b.Known.Confidence==AttackConfidence.Conditional && b.Known.Tick==actual) && Vector2.Dot(b.Velocity,raw-b.Origin)<0,"every captured random birth keeps its own native useful/missed outcome and known first damage, using assisted cursor opposite raw");if(actual>0)useful++;else missed++;
            }
            Require(useful>0 && born.Count>=6 && previews>0 && rngCalls==0 && scans==solves+knownCalls,"actual player use has positive immortal488 Strike, finite capture work and no model RNG; random misses are recorded rather than universally rejected");Console.WriteLine("PASS SKY FRACTURE natural births="+born.Count+" useful="+useful+" randomMiss="+missed+" previewFrames="+previews+" solves="+solves+" known="+knownCalls+" poolScans="+scans+" motor="+steps+" modelRNG="+rngCalls);
        }
    }
}
