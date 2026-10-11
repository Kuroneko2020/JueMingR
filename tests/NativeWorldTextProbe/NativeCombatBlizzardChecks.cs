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
    internal static class NativeCombatBlizzardChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,watched;private static object combat;
        private static bool recordBirth;private static object ammo;private static Vector2 raw;private static long first;
        private static int solves,knownCalls,depth,steps,scans,rngCalls,kills,advances;
        private sealed class Birth
        {internal Projectile Shot;internal int Key,Slot,Damage,Animation,Time,Reuse;internal long Tick,First=-1;internal Vector2 Center,Velocity,Origin,Cursor;internal AttackContact Known;internal bool Pressed,Permission;}
        private static readonly List<Birth> born=new List<Birth>();private static readonly List<int> hitSlots=new List<int>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;depth++;}private static void KnownWork(){knownCalls++;depth++;}private static void Done(){depth--;}
        private static void StepWork(){steps++;}private static void ScanWork(){scans++;}private static void RngWork(){if(depth>0)rngCalls++;}
        private static void AdvanceWork(){if(depth>0)advances++;}
        private static void BeforeKill(Projectile __instance){if(__instance.type==337)kills++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(receiving==null || receiving.type!=337 || receiving.owner!=Main.myPlayer || receiving.damage<=0 || __result<=0)return;hitSlots.Add(__instance.whoAmI);
            if(!ReferenceEquals(__instance,target) || watched!=null && !ReferenceEquals(watched,receiving))return;if(first<0)first=Main.GameUpdateCount;
            foreach(var b in born)if(ReferenceEquals(b.Shot,receiving) && b.Key==(int)receiving.key && b.First<0)b.First=Main.GameUpdateCount;
        }
        private static Player Reset(object context)
        {
            recordBirth=false;receiving=watched=null;target=null;first=-1;kills=0;born.Clear();hitSlots.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),1931,0,0);p.statMana=p.statManaMax=p.statManaMax2=300;p.releaseUseItem=true;Main.windPhysics=false;
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
        private static object Frame(Player p,NpcTrajectory path){return Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPlayerFrame",true),"Capture",p,path);}
        private static Projectile Shot(Player p,Vector2 center,Vector2 velocity,int damage=38){return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,337,damage,0,p.whoAmI)];}
        private static AttackContact Known(Player p,Projectile q,NpcTrajectory path){return (AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Receivers(p,q,path),Frame(p,path));}
        private static void BirthArgs(Player p,Vector2 cursor,float speed,int member,int offset,int jitter,Vector2 spread,out Vector2 center,out Vector2 velocity)
        {object[] args={p,cursor,speed,member,offset,jitter,spread,Vector2.Zero,Vector2.Zero};Static(model,"Birth",args);center=(Vector2)args[7];velocity=(Vector2)args[8];}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!recordBirth || __result<0)return;var q=Main.projectile[__result];if(q.type!=337)return;var p=Main.LocalPlayer;var origin=p.MountedCenter;var cursor=Main.MouseWorld;int member=(int)Math.Round((origin.Y-600-q.Center.Y)/100);float middle=(cursor.X+p.Center.X+p.width*.5f)*.5f;float relative=q.Center.X-middle;float low=p.direction==1?-300:-200,high=p.direction==1?200:300;
            var direction=cursor-q.Center;direction.Y=Math.Max(20,Math.Abs(direction.Y));var baseVelocity=direction.SafeNormalize(Vector2.UnitY)*(float)Get(ammo,"Speed");var spread=q.velocity-baseVelocity;var path=Path(target);
            var row=new Birth{Shot=q,Key=(int)q.key,Slot=q.whoAmI,Tick=Main.GameUpdateCount,Damage=q.damage,Animation=p.itemAnimation,Time=p.itemTime,Reuse=p.reuseDelay,Center=q.Center,Velocity=q.velocity,Origin=origin,Cursor=cursor,Pressed=(bool)Get(combat,"Left"),Permission=(bool)Get(Get(combat,"Attack"),"Permission"),Known=Known(p,q,path)};born.Add(row);
            Console.WriteLine("BLIZZARD born tick="+row.Tick+" slot/key="+row.Slot+"/"+row.Key+" member="+member+" center="+row.Center+" ownerMounted="+origin+" relativeX="+relative+" V="+row.Velocity+" spread="+spread+" damage="+q.damage+" frameAi="+q.ai[1]+" anim/time/reuse="+row.Animation+","+row.Time+","+row.Reuse+" pressed/permission="+row.Pressed+","+row.Permission+" cursor="+cursor+" raw="+raw+" known="+row.Known?.Tick);
            // Derive the two-member height and lawful X/spread intervals from
            // captured native birth arguments; never replace its RNG decisions.
            Require(!(__0 is EntitySource_Parent source && source.Entity is Projectile) && (member==0 || member==1) && Math.Abs(q.Center.Y-(origin.Y-600-100*member))<1f/8192 && relative>=low-1f/8192 && relative<=high+1f/8192 && Math.Abs(spread.X)<=.80001f && Math.Abs(spread.Y)<=.80001f && q.ai[1]>=0 && q.ai[1]<=4 && q.ai[1]==(int)q.ai[1] && q.magic && !q.tileCollide && !q.ignoreWater && q.width==10 && q.height==10 && q.extraUpdates==1 && q.penetrate==1 && q.damage==(int)Get(ammo,"Damage"),"actual native pair has two owner-relative heights, lawful X/spread/visual frame and genuine magic damage, not straight hand-origin birth");
        }
        internal static void Run(object context)
        {
            combat=Get(context,"Combat");assembly=combat.GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostBlizzardAttack",true);var audit=new Harmony("JueMingR.Tests.Blizzard");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(SolveWork)),postfix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(Done)));audit.Patch(model.GetMethod("KnownFamily",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(KnownWork)),postfix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(Done)));audit.Patch(model.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(StepWork)));audit.Patch(model.GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(ScanWork)));
            audit.Patch(typeof(PlayerMotionContinuation).GetMethod("Advance",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(AdvanceWork)));
            foreach(var method in Main.rand.GetType().GetMethods(Flags).Where(m=>m.Name=="Next" || m.Name=="NextDouble").Distinct())audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(RngWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(Born)));audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatBlizzardChecks),nameof(Strike)));
            try
            {
                string leaf=Environment.GetEnvironmentVariable("JMR_BLIZZARD_ORACLE_ONLY");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="birth" || leaf=="receive" || leaf=="natural","Blizzard leaf selector");
                if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);if(string.IsNullOrEmpty(leaf) || leaf=="birth")Births(context);if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);if(string.IsNullOrEmpty(leaf) || leaf=="natural")Natural(context);
            }
            finally{recordBirth=false;receiving=watched=null;target=null;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Motors(object context)
        {
            string part=Environment.GetEnvironmentVariable("JMR_BLIZZARD_MOTOR_PART");Require(string.IsNullOrEmpty(part) || part=="shimmer","Blizzard motor part selector");
            int actions=0;for(int scene=0;scene<12;scene++)
            {
                if(part=="shimmer" && scene!=8 && scene!=9)continue;Console.WriteLine("BLIZZARD motor actualScene="+scene+" part="+(part??"all"));
                var p=Reset(context);target=Target(2,new Vector2(1750,450));Main.worldSurface=10;
                p.position.Y=scene==1?649:scene==5 || scene==8 || scene==9?900:scene==6 || scene==7?340:650;
                if(scene==2)Main.worldSurface=35;if(scene==3)Main.worldSurface=21.875;if(scene==5)p.gravDir=-1;
                if(scene==1 || scene==2 || scene==4)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                if(scene==6 || scene==7)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==6?0:2);}
                if(scene==8 || scene==9)for(int x=40;x<115;x++){Main.tile[x,21].liquid=255;Main.tile[x,21].liquidType(3);if(scene==9){Main.tile[x,20].liquid=255;Main.tile[x,20].liquidType(3);}}
                // Native WetCollision uses a centre strip only five pixels
                // high. At center355 its top353 lies below row21's end352.
                // Move only the two shimmer bodies into actual liquid overlap.
                var q=Shot(p,new Vector2(scene==11?3:950,scene==8 || scene==9?350:355),new Vector2(6,scene==8?2:0));q.ai[0]=20;q.ai[1]=3;
                if(scene==6 || scene==7)q.velocity.Y=22;if(scene==10)q.timeLeft=1;var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();
                for(int outer=1;outer<=20 && q.active;outer++)
                {
                    if(scene==4 && outer==3)p.position.Y=600;
                    int updates=q.extraUpdates+1;for(int sub=0;sub<updates;sub++){Require((bool)Static(model,"Step",copy,p.position.Y,Main.worldSurface,terrain),"Blizzard owner-relative motor dependencies are present");actions++;if(!copy.active)break;Static(model,"Finish",copy);if(!copy.active){if(sub+1<updates)Require((bool)Static(model,"RetiredStep",copy,p.position.Y,Main.worldSurface,terrain),"inactive tail movement has no repeated AI/Damage");break;}}
                    q.Update(q.whoAmI);
                    Console.WriteLine("BLIZZARD motor "+scene+"/"+outer+" actual="+q.position+"/"+q.velocity+" left/active/tile/frame/ai="+q.timeLeft+","+q.active+","+q.tileCollide+","+q.frame+","+q.ai[0]+" model="+copy.position+"/"+copy.velocity+" left/active/tile/frame/ai="+copy.timeLeft+","+copy.active+","+copy.tileCollide+","+copy.frame+","+copy.ai[0]);
                    if(outer==1 && scene==8)Require(q.active && q.velocity.Y<0 && q.timeLeft<540,"actual partial Shimmer reverses downward movement and reduces native AI1 lifetime");if(outer==1 && scene==9)Require(!q.active && q.timeLeft==0 && kills>0,"actual full Shimmer calls Kill before useful Damage");
                    Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.penetrate==copy.penetrate && q.maxPenetrate==copy.maxPenetrate && q.tileCollide==copy.tileCollide && q.frame==copy.frame && q.ai.SequenceEqual(copy.ai) && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.shimmerWet==copy.shimmerWet && Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.0001f,"native Blizzard finite owner/surface/liquid/collision/retirement motor "+scene+"/"+outer);
                }
                if(scene==0 || scene==3 || scene==5)Require(!q.tileCollide && q.velocity.Y==0,"strict owner/worldSurface boundary and AI337 branch skip ordinary gravity after ai0 twenty");if(scene==11)Require(kills==0 && q.timeLeft>0,"world edge retires without native Kill");
            }
            Console.WriteLine("PASS BLIZZARD motor cases="+(part=="shimmer"?2:12)+" actions="+actions+" actual owner threshold, strict surface boundary, wet displacement, shimmer and retirement");
        }
        private static void Births(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+180;raw=target.Center;ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=true;
            for(int frame=0;frame<11;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,frame==0);recordBirth=false;Console.WriteLine("BLIZZARD first released animation count="+born.Count+" ticks="+string.Join(",",born.Select(b=>b.Tick)));
            Require(born.Count==4 && born[0].Tick==born[1].Tick && born[2].Tick==born[3].Tick && born[2].Tick-born[0].Tick==5 && born[0].Center.Y-born[1].Center.Y==100 && born[2].Center.Y-born[3].Center.Y==100 && born.All(b=>b.Reuse==0),"one real press then release finishes two separate five-update hand actions, each a simultaneous pair with hundred-pixel vertical separation");
            PreparedClock(context);
        }
        private static void PreparedClock(object context)
        {
            var p=Reset(context);target=Target(2,new Vector2(660,85),300,40);var path=Path(target);var snapshot=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var sample=new Projectile();sample.SetDefaults(337);sample.owner=p.whoAmI;sample.damage=(int)Get(snapshot,"Damage");
            var frame=Frame(p,path);int count=advances;var prepared=(AttackContact)Static(model,"Solve",p,snapshot,path,p.MountedCenter,0,false,Terrain(),Receivers(p,sample,path),0,frame,true);
            Require(prepared!=null && prepared.Tick==1 && advances==count,"already advanced Prepared birth consumes its supplied owner frame without a second first-player update");
            count=advances;var rawFrame=(AttackContact)Static(model,"Solve",p,snapshot,path,p.MountedCenter,0,false,Terrain(),Receivers(p,sample,path),0,frame,false);
            Require(rawFrame!=null && rawFrame.Tick==1 && advances==count+1,"unprepared CompletedWorld advances the first owner update exactly once");
            Console.WriteLine("PASS BLIZZARD prepared owner clock supplied0/unprepared1 first-update advances");
        }
        private static void Receives(object context)
        {
            int cases=0;for(int mode=0;mode<6;mode++)
            {
                var p=Reset(context);target=Target(2,new Vector2(mode==5?980:995,350));if(mode==1)target.immune[p.whoAmI]=200;if(mode==4)target.reflectsProjectiles=true;NPC front=mode==2?Target(1,new Vector2(970,350)):null;watched=Shot(p,new Vector2(950,350),new Vector2(8,0));if(mode==3)watched.damage=0;
                NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var contact=Known(p,watched,path);long sample=path.SampleTick;Require(Static(model,"KnownFamily",p,new[]{watched},path,Clock(path,"Projectiles"),Terrain(),Receivers(p,watched,path),Frame(p,path))==null,"whole-family replay declines an unknown partial-array slot epoch");
                for(int outer=1;outer<=30 && watched.active;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(1);target.UpdateNPC(2);watched.Update(watched.whoAmI);}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("BLIZZARD receive mode="+mode+" native="+actual+" known="+contact?.Tick+" slotHits="+string.Join(",",hitSlots)+" owner="+target.immune[p.whoAmI]);bool negative=mode==2 || mode==3 || mode==4;
                Require((contact==null)==negative && (actual==0)==negative && (contact==null || contact.Tick==actual),"native single-penetration body/other receiver/owner bypass/zero damage/reflection agrees with known source");if(mode==2)Require(hitSlots.Contains(1) && !hitSlots.Contains(2),"actual lower receiver consumes penetration before selected body");cases++;
            }
            LegalRandomControls(context);Console.WriteLine("PASS BLIZZARD receives="+cases+" plus2 legal spread branches");
        }
        private static void LegalRandomControls(object context)
        {
            foreach(bool misses in new[]{false,true})
            {
                var p=Reset(context);p.position=new Vector2(940,979);Main.worldSurface=10;target=Target(2,new Vector2(1040,1000));Vector2 center,velocity;BirthArgs(p,target.Center,10,0,100,0,misses?new Vector2(-.8f,0):Vector2.Zero,out center,out velocity);watched=Shot(p,center,velocity);NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var contact=Known(p,watched,path);long sample=path.SampleTick;
                for(int outer=1;outer<=120 && watched.active;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);watched.Update(watched.whoAmI);}
                int actual=first<0?0:(int)(first-sample);Console.WriteLine("BLIZZARD legal spread misses="+misses+" center="+center+" V="+velocity+" native="+actual+" known="+contact?.Tick);
                Require((actual==0)==misses && (contact==null)==misses && (contact==null || contact.Tick==actual),"same lawful cursor yields useful or missed spread branch, with each actual-born replay equal to native; Representative cannot promise every random birth hits");
            }
        }
        private static void Draws(object host,object attack,AttackContact plan)
        {
            int a=solves,b=steps,c=scans;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;
            for(int check=0;check<20;check++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",plan),"stable Blizzard preview lease remains current");}
            Require(a==solves && b==steps && c==scans,"twenty presentations perform zero Blizzard Solve/motor/pool scans");
        }
        private static void Natural(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+180;raw=target.Center-new Vector2(380,0);solves=knownCalls=depth=steps=scans=rngCalls=0;var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int frame=0;frame<3;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);var plan=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(plan!=null && plan.Confidence==AttackConfidence.Representative && !Main.projectile.Any(q=>q.active),"idle next native two-member hand preview is read-only and does not fire");Draws(host,attack,plan);
            ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));int previews=0;
            for(int frame=0;frame<45;frame++){NativeCombatSelectionPreviewChecks.Step(context,raw,true);var next=(AttackContact)GetOptional(attack,"ExpectedPreview");if(frame>=2){Require(next!=null,"continuous five-update hand actions retain a legal next-hand preview");previews++;}if(frame==6 && next!=null)Draws(host,attack,next);}
            NativeCombatSelectionPreviewChecks.Step(context,raw,false);recordBirth=false;for(int frame=0;frame<120 && born.Any(b=>b.First<0 && b.Key==(int)b.Shot.key && b.Shot.active);frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
            int useful=0,missed=0,held=0,released=0;foreach(var b in born)
            {
                int actual=b.First<0?0:(int)(b.First-b.Tick+1);Console.WriteLine("BLIZZARD natural slot/key="+b.Slot+"/"+b.Key+" born="+b.Center+" V="+b.Velocity+" cursor="+b.Cursor+" first="+actual+" known="+b.Known?.Tick);
                Require((b.Known==null)==(actual==0) && (b.Known==null || b.Known.Confidence==AttackConfidence.Conditional && b.Known.Tick==actual),"every captured random birth, including released native animation suffix, preserves its own actual useful/missed outcome and known first damage");
                if(b.Pressed){Require(b.Permission && Vector2.Dot(b.Cursor-b.Origin,raw-b.Origin)<0,"each genuinely held hand consumes an assisted cursor opposite physical raw input");held++;}
                else{Require(!b.Permission && Vector2.DistanceSquared(b.Cursor,raw)<=2,"released native animation suffix preserves physical cursor and cannot borrow an auxiliary input capability");released++;}
                if(actual>0)useful++;else missed++;
            }
            Require(useful>0 && held>=6 && released==2 && previews>0 && rngCalls==0 && scans==solves+knownCalls,"actual player use has positive immortal488 Strike, finite capture work and no model RNG; exactly two released suffix births retain physical input and legal random misses");Console.WriteLine("PASS BLIZZARD natural births="+born.Count+" held/released="+held+","+released+" useful="+useful+" randomMiss="+missed+" previewFrames="+previews+" solves="+solves+" known="+knownCalls+" poolScans="+scans+" motor="+steps+" modelRNG="+rngCalls);
        }
    }
}
