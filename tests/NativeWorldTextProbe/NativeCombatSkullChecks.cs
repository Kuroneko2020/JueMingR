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
    internal static class NativeCombatSkullChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,watched;private static bool record;
        private static Vector2 raw;private static long first;private static int firstSub,solves,knownCalls,depth,steps,scans,rng,kills;
        private sealed class Birth{internal Projectile Shot;internal int Key,Slot,Damage,Sub=-1;internal long Tick,First=-1;internal Vector2 Center,Velocity;internal AttackContact Known;}
        private static readonly List<Birth> born=new List<Birth>();private static readonly List<int> hitSlots=new List<int>();
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static bool Role(Projectile q){return q!=null && q.type==837;}
        private static void SolveWork(){solves++;depth++;}private static void KnownWork(){knownCalls++;depth++;}private static void Done(){depth--;}
        private static void StepWork(){steps++;}private static void ScanWork(){scans++;}private static void RngWork(){if(depth>0)rng++;}
        private static void BeforeKill(Projectile __instance){if(Role(__instance) && __instance.active)kills++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(!Role(receiving) || receiving.owner!=Main.myPlayer || __result<=0)return;hitSlots.Add(__instance.whoAmI);
            if(!ReferenceEquals(__instance,target) || watched!=null && !ReferenceEquals(watched,receiving))return;int sub=receiving.extraUpdates-receiving.numUpdates-1;
            if(first<0){first=Main.GameUpdateCount;firstSub=sub;}foreach(var b in born)if(ReferenceEquals(b.Shot,receiving) && b.Key==(int)receiving.key && b.First<0){b.First=Main.GameUpdateCount;b.Sub=sub;}
        }
        private static Player Reset(object context)
        {
            record=false;receiving=watched=null;target=null;first=-1;firstSub=-1;kills=0;born.Clear();hitSlots.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),1313,0,0);p.statMana=p.statManaMax=p.statManaMax2=300;p.releaseUseItem=true;Main.windPhysics=false;
            for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Path(NPC n,bool completed=false)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-(completed?0:1),1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var value=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(value,"Reset");return value;}
        private static object Receivers(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path,string phase="BeforeNpc"){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),0},null);}
        private static AttackContact Known(Player p,Projectile q,NpcTrajectory path){return (AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Receivers(p,q,path));}
        private static Projectile Shot(Player p,Vector2 center,Vector2 velocity,int damage=38){return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,837,damage,0,p.whoAmI)];}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!record || __result<0)return;var q=Main.projectile[__result];if(!Role(q))return;var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=Main.MouseWorld;
            var velocity=(Vector2)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.HostSonicBoomAttack",true),"LimitLaunch",(cursor-origin).SafeNormalize(new Vector2(p.direction,0))*p.HeldItem.shootSpeed);
            var row=new Birth{Shot=q,Slot=q.whoAmI,Key=(int)q.key,Damage=q.damage,Tick=Main.GameUpdateCount,Center=q.Center,Velocity=q.velocity,Known=Known(p,q,Path(target))};born.Add(row);
            Console.WriteLine("SKULL born tick="+row.Tick+" slot/key="+row.Slot+"/"+row.Key+" center/V="+row.Center+"/"+row.Velocity+" cursor/raw="+cursor+"/"+raw+" damage="+q.damage+" pen/left/tile="+q.penetrate+"/"+q.timeLeft+"/"+q.tileCollide+" known="+row.Known?.Tick+"/"+row.Known?.Subupdate);
            Require(!(__0 is EntitySource_Parent parent && parent.Entity is Projectile) && q.magic && q.width==26 && q.height==26 && q.penetrate==3 && q.maxPenetrate==3 && q.timeLeft==480 && q.extraUpdates==0 && !q.tileCollide && q.damage==p.GetWeaponDamage(p.HeldItem) && q.ai.SequenceEqual(new float[3]) && Vector2.DistanceSquared(origin,q.Center)<.000001f && Vector2.DistanceSquared(velocity,q.velocity)<.000001f,"native1313 directly births one837 with the real hand cursor, pure damage and AI001 launch limit");
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostSkullAttack",true);var audit=new Harmony("JueMingR.Tests.Skull");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(SolveWork)),finalizer:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(Done)));audit.Patch(model.GetMethod("KnownFamily",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(KnownWork)),finalizer:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(Done)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(StepWork)));audit.Patch(model.GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(ScanWork)));
            foreach(var method in Main.rand.GetType().GetMethods(Flags).Where(m=>m.Name=="Next" || m.Name=="NextDouble").Distinct())audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(RngWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(Born)));audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatSkullChecks),nameof(Strike)));
            try{string leaf=Environment.GetEnvironmentVariable("JMR_SKULL_ORACLE_ONLY");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="birth" || leaf=="receive" || leaf=="natural","Skull leaf selector");if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);if(string.IsNullOrEmpty(leaf) || leaf=="birth")Births(context);if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);if(string.IsNullOrEmpty(leaf) || leaf=="natural")Natural(context);}
            finally{record=false;receiving=watched=null;target=null;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Motors(object context)
        {
            int actions=0;for(int scene=0;scene<16;scene++)
            {
                var p=Reset(context);target=Target(2,new Vector2(1750,500));
                if(scene>=1 && scene<=3)for(int x=5;x<115;x++)for(int y=10;y<40;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                if(scene>=4 && scene<=7)for(int x=50;x<90;x++)for(int y=10;y<40;y++){NativeToolsChecks.Tile(x,y,scene==7?19:1);if(scene==5)Main.tile[x,y].halfBrick(true);if(scene==6)Main.tile[x,y].slope(1);}
                if(scene==8){target.Center=new Vector2(1080,365);Target(1,new Vector2(1080,335));}
                if(scene==9)target.Center=new Vector2(1750,650);
                if(scene==10){target.Center=new Vector2(1080,365);target.dontTakeDamage=true;}
                if(scene==11){target.Center=new Vector2(1100,350);for(int y=10;y<40;y++)NativeToolsChecks.Tile(64,y,1);}
                if(scene==12 || scene==13)for(int x=5;x<115;x++){Main.tile[x,21].liquid=128;Main.tile[x,21].liquidType(3);}
                var q=Shot(p,scene==15?new Vector2(12,350):new Vector2(950,350),new Vector2(4.5f,scene==12?2:scene==13?-2:0));if(scene==9 || scene==10)q.ai[1]=3;if(scene==14)q.timeLeft=1;
                var path=Path(target);var receivers=Receivers(p,q,path);var targets=Call(receivers,"CaptureChaseTargets",q);var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();int initial=q.timeLeft;
                for(int outer=1;outer<=40 && q.active;outer++)
                {
                    Require((bool)Static(model,"Step",copy,targets,path,outer,terrain),"Skull physical motor has the required finite target/tile/liquid dependencies");actions++;if(copy.active)Static(model,"Finish",copy);q.Update(q.whoAmI);
                    Console.WriteLine("SKULL motor "+scene+"/"+outer+" native="+q.position+"/"+q.velocity+" ai/local="+q.ai[0]+","+q.ai[1]+"/"+q.localAI[0]+" alpha/frame/left/active="+q.alpha+"/"+q.frame+"/"+q.timeLeft+"/"+q.active+" model="+copy.position+"/"+copy.velocity+" ai/local="+copy.ai[0]+","+copy.ai[1]+"/"+copy.localAI[0]+" alpha/frame/left/active="+copy.alpha+"/"+copy.frame+"/"+copy.timeLeft+"/"+copy.active);
                    Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.alpha==copy.alpha && q.frame==copy.frame && q.ai.SequenceEqual(copy.ai) && q.localAI.SequenceEqual(copy.localAI) && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.shimmerWet==copy.shimmerWet && Vector2.DistanceSquared(q.position,copy.position)<.0001f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.000001f,"native837 target acquisition/slot persistence, solid-center tax and generic liquid motor agree");
                    if(outer==1){if(scene==4)Require(q.timeLeft==initial-34,"a full solid center taxes33 before the ordinary lifetime decrement");if(scene>=5 && scene<=7)Require(q.timeLeft==initial-1,"half/sloped/platform centers are excluded by WorldGen.SolidTile");if(scene==8)Require(q.ai[1]==2 && q.velocity.Y<0,"equal-distance acquisition preserves the first NPC slot");if(scene==9)Require(q.ai[1]==3 && q.velocity.Y==0,"a chaseable target beyond1000 retains its slot without steering");if(scene==10 || scene==11)Require(q.ai[1]==0,"invalid locked target or acquisition blocked by native CanHit does not steer");}
                }
                if(scene==3 || scene==14)Require(kills==1 && !q.active && q.timeLeft==0,"full Shimmer or exhausted lifetime really invokes visual-only Kill");if(scene==15)Require(kills==0 && !q.active && q.timeLeft==480,"world edge retires before AI without Kill");
            }
            TargetLease(context);Console.WriteLine("PASS SKULL motor16 actions="+actions+" plus chase eligibility/geometry lease");
        }
        private static void TargetLease(object context)
        {
            var p=Reset(context);target=Target(2,new Vector2(1080,365));var q=Shot(p,new Vector2(950,350),new Vector2(4.5f,0));var lease=Call(Receivers(p,q,Path(target)),"CaptureChaseTargets",q);Require((bool)Get(lease,"Current"),"captured actual chase target is current");
            target.dontTakeDamage=true;Require(!(bool)Get(lease,"Current"),"chase qualification disappearing invalidates the acquired slot");target.dontTakeDamage=false;Require((bool)Get(lease,"Current"),"restored native chase qualification restores the lease");Vector2 center=target.Center;target.position.X+=1;Require(!(bool)Get(lease,"Current"),"only target position changing invalidates pursuit geometry");target.Center=center;Require((bool)Get(lease,"Current"),"restored pursuit geometry is current");
        }
        private static Player Scene(object context,int direction)
        {var p=Reset(context);p.position=new Vector2(650,646);p.direction=direction;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+direction*180;raw=target.Center;return p;}
        private static void Births(object context)
        {
            foreach(int direction in new[]{1,-1}){Scene(context,direction);record=true;for(int frame=0;frame<40;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,frame==0);record=false;Require(born.Count==1,"one actual press/release births one BookOfSkulls shot");}
            Console.WriteLine("PASS SKULL native hand births2");
        }
        private static void Receives(object context)
        {
            for(int mode=0;mode<7;mode++)
            {
                var p=Reset(context);target=Target(2,new Vector2(975,350));if(mode==1)target.immune[p.whoAmI]=200;if(mode==2)target.reflectsProjectiles=true;
                NPC front=mode==3?Target(1,target.Center):null;watched=Shot(p,new Vector2(950,350),new Vector2(4.5f,0),mode==4?0:38);if(mode==5)watched.penetrate=watched.maxPenetrate=1;if(mode==6)watched.penetrate=1;if(mode>=5)target.immune[p.whoAmI]=200;
                NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var plan=Known(p,watched,path);long sample=path.SampleTick;Require(Static(model,"Known",p,watched,path,Clock(path,"Projectiles"),Terrain(),Receivers(p,watched,path))==null,"Skull family observation declines partial array epochs");
                for(int outer=1;outer<=12 && watched.active && watched.friendly;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(1);target.UpdateNPC(2);watched.Update(watched.whoAmI);}int actual=first<0?0:(int)(first-sample);bool negative=mode==1 || mode==2 || mode==4 || mode==6;
                Console.WriteLine("SKULL receive mode="+mode+" actual/known="+actual+"/"+plan?.Tick+" sub="+firstSub+"/"+plan?.Subupdate+" slots="+string.Join(",",hitSlots)+" owner="+target.immune[p.whoAmI]+" pen="+watched.penetrate);
                Require((actual==0)==negative && (plan==null)==negative && (plan==null || actual==plan.Tick && firstSub==plan.Subupdate),"actual skull owner gate, finite penetration, raw reflection and useful Strike agree");if(mode==2)Require(watched.hostile && !watched.friendly,"native raw overlapping body really reflects before useful Strike");if(mode==3)Require(hitSlots.Contains(1) && hitSlots.Contains(2),"pen3 consumes lower and selected in the same native NPC loop");
            }
            Console.WriteLine("PASS SKULL receiving7");
        }
        private static void Draws(object host,object attack,AttackContact plan,object flight=null)
        {if(flight==null)Call(attack,"BindPresentation",plan);int a=solves,b=steps,c=scans;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int check=0;check<20;check++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require(flight==null?(bool)Call(attack,"PresentationCurrent",plan):(bool)Call(flight,"PresentationCurrent",0),"stable actual Skull hand/family preparation remains current");}Require(a==solves && b==steps && c==scans,"twenty CPU presentations have zero Skull solve/motor/family scans");}
        private static object ExistingFlight(object attack,Player p)
        {
            var sources=born.Where(b=>b.Shot.active && b.Shot.friendly && b.Key==(int)b.Shot.key).Select(b=>b.Shot).ToArray();Require(sources.Length>0,"real skulls own the observed flight interval");var path=Path(target,true);
            var receivers=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,sources[0],path,0,false,true},null);var live=(AttackContact)Static(model,"KnownFamily",p,sources,path,Clock(path,"CompletedWorld"),Terrain(),receivers);
            var args=new object[]{p,path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld"),false};object flight;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))flight=Call(attack,"ReadFlight",args);var observed=flight==null?null:(AttackContact)Get(flight,"Contact");
            Console.WriteLine("SKULL existing GUC/sample="+Main.GameUpdateCount+"/"+path.SampleTick+" public="+observed?.Tick+"/"+observed?.Subupdate+" known="+live?.Tick+"/"+live?.Subupdate);
            Require((bool)args[3] && live!=null && observed!=null && observed.Tick==live.Tick && observed.Subupdate==live.Subupdate,"public Skull Flight consumes the independently known complete existing family without granting input");
            return flight;
        }
        private static AttackContact PrepareNatural(object context,object host,object attack)
        {
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);return (AttackContact)GetOptional(attack,"ExpectedImpact");
        }
        private static void Natural(object context)
        {
            foreach(int direction in new[]{1,-1})
            {
                var p=Scene(context,direction);solves=knownCalls=depth=steps=scans=rng=0;var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);
                Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active) && solves==0,"withdrawn idle BookOfSkulls preview neither solves nor fires");
                raw=target.Center-new Vector2(direction*380,0);NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));var plan=PrepareNatural(context,host,attack);Require(plan!=null,"actual pressed Skull hand prepares under1000ms mechanism allowance");Draws(host,attack,plan);record=true;int previews=0;
                for(int frame=0;frame<80;frame++)
                {var next=frame==0?plan:PrepareNatural(context,host,attack);if(frame>=2){Console.WriteLine("SKULL natural direction/frame="+direction+"/"+frame+" timers="+p.itemAnimation+"/"+p.itemTime+" preparation="+next?.Tick+"/"+next?.Subupdate);Require(GetOptional(attack,"ExpectedPreview")==null,"all78 stable native Skull frames keep the retired hypothetical preview absent");previews++;}NativeCombatSelectionPreviewChecks.Step(context,raw,true);if(frame==6){var flight=ExistingFlight(attack,p);Draws(host,attack,(AttackContact)Get(flight,"Contact"),flight);}}
                NativeCombatSelectionPreviewChecks.Step(context,raw,false);record=false;for(int frame=0;frame<60 && born.Any(b=>b.First<0 && b.Shot.active && b.Key==(int)b.Shot.key);frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
                foreach(var b in born){int actual=b.First<0?0:(int)(b.First-b.Tick+1);Console.WriteLine("SKULL actual direction="+direction+" born="+b.Tick+" slot/key="+b.Slot+"/"+b.Key+" center/V="+b.Center+"/"+b.Velocity+" first/known="+actual+"/"+b.Known?.Tick+" sub="+b.Sub+"/"+b.Known?.Subupdate);Require(actual>0 && b.Known!=null && actual==b.Known.Tick && b.Sub==b.Known.Subupdate && Vector2.Dot(b.Velocity,raw-b.Center)<0,"actual assisted skull has positive immortal488 Strike matching its own Known first clock and an opposite physical mouse");}
                Require(born.Count>=3 && previews==78 && rng==0 && scans==solves+knownCalls,"actual Skull first/continuous uses cover all78 stable native observations, real hand/family consumers and finite work without model RNG");Console.WriteLine("PASS SKULL natural direction="+direction+" births="+born.Count+" observations="+previews+" solve/known/capture/motor="+solves+"/"+knownCalls+"/"+scans+"/"+steps);
            }
        }
    }
}
