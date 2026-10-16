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
    internal static class NativeCombatRaySpellChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,watched;
        private static bool recordBirth;private static object ammo;private static Vector2 raw;private static long first;private static int firstSub;
        private static int solves,knownCalls,depth,steps,scans,rngCalls,kills;
        private sealed class Birth
        {internal Projectile Shot;internal int Key,Slot,Damage,Animation,Time,Reuse,FirstSub=-1;internal long Tick,First=-1;internal Vector2 Center,Velocity,Origin,Cursor;internal AttackContact Known;}
        private static readonly List<Birth> born=new List<Birth>();private static readonly List<int> hitSlots=new List<int>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;depth++;}private static void KnownWork(){knownCalls++;depth++;}private static void Done(){depth--;}
        private static void StepWork(){steps++;}private static void ScanWork(){scans++;}private static void RngWork(){if(depth>0)rngCalls++;}
        private static void BeforeKill(Projectile __instance){if(__instance.active && (__instance.type==260 || __instance.type==294))kills++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            //294 may already have lowered its source damage to0 before
            // StrikeNPC. The actual positive result is the witness, not the
            // now-mutated damage field or immortal488's unchanged life.
            if(receiving==null || (receiving.type!=260 && receiving.type!=294) || receiving.owner!=Main.myPlayer || __result<=0)return;hitSlots.Add(__instance.whoAmI);
            if(!ReferenceEquals(__instance,target) || watched!=null && !ReferenceEquals(watched,receiving))return;
            int sub=receiving.extraUpdates-receiving.numUpdates-1;if(first<0){first=Main.GameUpdateCount;firstSub=sub;}
            foreach(var b in born)if(ReferenceEquals(b.Shot,receiving) && b.Key==(int)receiving.key && b.First<0){b.First=Main.GameUpdateCount;b.FirstSub=sub;}
        }
        private static Player Reset(object context,int weapon=1295)
        {
            recordBirth=false;receiving=watched=null;target=null;first=-1;firstSub=-1;kills=0;born.Clear();hitSlots.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);p.statMana=p.statManaMax=p.statManaMax2=300;p.releaseUseItem=true;Main.windPhysics=false;
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
        private static Projectile Shot(Player p,Vector2 center,Vector2 velocity,int damage=38){return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,p.HeldItem.shoot,damage,0,p.whoAmI)];}
        private static AttackContact Known(Player p,Projectile q,NpcTrajectory path){return (AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Receivers(p,q,path));}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!recordBirth || __result<0)return;var q=Main.projectile[__result];if(q.type!=260 && q.type!=294)return;
            var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=Main.MouseWorld;
            var v=(Vector2)Static(model,"BirthVelocity",origin,cursor,(float)Get(ammo,"Speed"),p.direction);var path=Path(target);
            var row=new Birth{Shot=q,Key=(int)q.key,Slot=q.whoAmI,Tick=Main.GameUpdateCount,Damage=q.damage,Animation=p.itemAnimation,Time=p.itemTime,Reuse=p.reuseDelay,Center=q.Center,Velocity=q.velocity,Origin=origin,Cursor=cursor,Known=Known(p,q,path)};born.Add(row);
            Console.WriteLine("RAY SPELL born type="+q.type+" tick="+row.Tick+" slot/key="+row.Slot+"/"+row.Key+" center="+q.Center+" origin="+origin+" V="+q.velocity+" damage="+q.damage+" extra/left/pen="+q.extraUpdates+","+q.timeLeft+","+q.penetrate+" cursor="+cursor+" raw="+raw+" known="+row.Known?.Tick+"/"+row.Known?.Subupdate);
            Require(!(__0 is EntitySource_Parent source && source.Entity is Projectile) && q.magic && q.extraUpdates==100 && q.timeLeft==(q.type==260?200:300) && q.width==(q.type==260?8:4) && q.height==q.width && q.penetrate==(q.type==260?1:-1) && q.damage==(int)Get(ammo,"Damage") && Vector2.DistanceSquared(q.Center,origin)<.000001f && Vector2.DistanceSquared(q.velocity,v)<.000001f,"actual AI048 hand birth uses mounted origin and native unclamped velocity, not an AI001 pulse or random member");
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostRaySpellAttack",true);var audit=new Harmony("JueMingR.Tests.RaySpells");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(SolveWork)),finalizer:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(Done)));audit.Patch(model.GetMethod("KnownFamily",Flags),prefix:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(KnownWork)),finalizer:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(Done)));audit.Patch(model.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(StepWork)));audit.Patch(model.GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(ScanWork)));
            foreach(var method in Main.rand.GetType().GetMethods(Flags).Where(m=>m.Name=="Next" || m.Name=="NextDouble").Distinct())audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(RngWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(Born)));audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatRaySpellChecks),nameof(Strike)));
            try
            {
                string leaf=Environment.GetEnvironmentVariable("JMR_RAY_SPELL_ORACLE_ONLY");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="birth" || leaf=="receive" || leaf=="natural","RaySpells leaf selector");
                if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);if(string.IsNullOrEmpty(leaf) || leaf=="birth")Births(context);if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);if(string.IsNullOrEmpty(leaf) || leaf=="natural")Natural(context);
            }
            finally{recordBirth=false;receiving=watched=null;target=null;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Motors(object context)
        {
            int actions=0,reflections=0;foreach(int weapon in new[]{1295,1444})for(int scene=0;scene<9;scene++)
            {
                var p=Reset(context,weapon);target=Target(2,new Vector2(1750,500));
                if(scene>=1 && scene<=3)for(int x=5;x<115;x++)for(int y=10;y<40;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                if(scene==4)for(int y=10;y<40;y++)NativeToolsChecks.Tile(60,y,1);
                if(scene==5 || scene==6)for(int x=60;x<68;x++){NativeToolsChecks.Tile(x,22,1);Main.tile[x,22].slope((byte)(scene==5?1:3));}
                Vector2 center=scene==8?new Vector2(70,350):new Vector2(950,scene==5 || scene==6?340:350);
                var q=Shot(p,center,new Vector2(scene==4?15:1,scene==5 || scene==6?2:0));if(scene==7)q.timeLeft=1;
                var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();
                for(int outer=1;outer<=3 && q.active;outer++)
                {
                    for(int sub=0;sub<=copy.extraUpdates;sub++)
                    {
                        bool edge=(bool)Static(model,"WorldRetired",copy);var old=copy.velocity;
                        Require((bool)Static(model,"Step",copy,terrain),"AI048 finite motor has all terrain cells");actions++;if(edge)break;
                        if(copy.velocity.X*old.X<0 || copy.velocity.Y*old.Y<0)reflections++;
                        // Update returns immediately after HandleMovement
                        // when inactive (17131). A Tail Kill permits just
                        // one next-sub movement, never100 inactive Damage or
                        // lifetime calls. A motor Kill skips Tail altogether.
                        if(!copy.active)break;
                        Static(model,"Finish",copy);
                    }
                    q.Update(q.whoAmI);
                    Console.WriteLine("RAY SPELL motor "+q.type+"/"+scene+"/"+outer+" native="+q.position+"/"+q.velocity+" local="+string.Join(",",q.localAI)+" alpha/left/active="+q.alpha+","+q.timeLeft+","+q.active+" model="+copy.position+"/"+copy.velocity+" local="+string.Join(",",copy.localAI)+" alpha/left/active="+copy.alpha+","+copy.timeLeft+","+copy.active);
                    Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.penetrate==copy.penetrate && q.alpha==copy.alpha && q.localAI.SequenceEqual(copy.localAI) && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.shimmerWet==copy.shimmerWet && q.direction==copy.direction && Vector2.DistanceSquared(q.position,copy.position)<.0001f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.000001f,"native AI048 finite subcycle including Kill's inactive suffix, segmented tile response and liquid displacement");
                    if(scene==3)Require(q.shimmerWet && q.localAI[0]>0,"AI048 in Shimmer retains its own motor rather than AI001 liquid Kill");
                }
                if(scene==8)Require(kills==1 && !q.active,"AI048 padding5 edge invokes exactly one real Kill, without descendants");
            }
            Require(reflections>0,"Shadowbeam has at least one real model tile reflection");Console.WriteLine("PASS RAY SPELL motors18 actions="+actions+" reflections="+reflections);
        }
        private static void Births(object context)
        {
            foreach(int weapon in new[]{1295,1444})
            {
                var p=Reset(context,weapon);p.position=new Vector2(650,646);Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+180;raw=target.Center;
                ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=true;
                int duration=p.HeldItem.useAnimation;for(int frame=0;frame<duration+3;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,frame==0);
                Require(born.Count==1,"one press finishes exactly one real ray hand action, without synthetic101 siblings");recordBirth=false;
                var v=(Vector2)Static(model,"BirthVelocity",Vector2.Zero,Vector2.UnitX,24f,1);Require(v.X==24 && v.Y==0,"AI048 birth preserves a24 component instead of applying AI001's .97 clamp");
                var fast=Shot(p,new Vector2(950,350),v);Require(fast.velocity==v,"actual native NewProjectile AI048 also stores the24 component unchanged");fast.active=false;
            }
            Console.WriteLine("PASS RAY SPELL actual hand births2");
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(int weapon in new[]{1295,1444})for(int mode=0;mode<8;mode++)
            {
                var p=Reset(context,weapon);target=Target(2,mode==7?new Vector2(928,350):mode==5?new Vector2(995,361):new Vector2(mode==6?1050:995,350),mode==7?10:18,mode==5 || mode==7?2:40);
                if(mode==1)target.immune[p.whoAmI]=200;if(mode==4)target.reflectsProjectiles=true;
                NPC front=mode==2?Target(1,new Vector2(970,350)):null;if(mode>=6)for(int y=10;y<40;y++)NativeToolsChecks.Tile(60,y,1);
                watched=Shot(p,new Vector2(950,350),new Vector2(weapon==1295?15:6,0));if(mode==3)watched.damage=0;
                NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var contact=Known(p,watched,path);long sample=path.SampleTick;
                Require(Static(model,"KnownFamily",p,new[]{watched},path,Clock(path,"Projectiles"),Terrain(),Receivers(p,watched,path))==null,"ray whole-array oracle declines an unknown partial projectile epoch");
                for(int outer=1;outer<=3 && watched.active;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();if(front!=null)front.UpdateNPC(1);target.UpdateNPC(2);watched.Update(watched.whoAmI);}
                int actual=first<0?0:(int)(first-sample);bool negative=mode==3 || mode==6 || weapon==1295 && (mode==2 || mode==5 || mode==7) || weapon==1444 && mode==1;
                Console.WriteLine("RAY SPELL receive "+watched.type+"/"+mode+" actual="+actual+" known="+contact?.Tick+"/"+contact?.Subupdate+" hits="+string.Join(",",hitSlots)+" damage="+watched.damage+" owner="+target.immune[p.whoAmI]);
                Require((actual==0)==negative && (contact==null)==negative && (contact==null || contact.Tick==actual && contact.Subupdate==firstSub),"native AI048 expanded shape, owner once per outer, penetration, tile bounce and noAI48 reflection agree");
                if(mode==4)Require(actual>0 && watched.friendly && !watched.hostile,"a reflectsProjectiles NPC really receives AI048: native reflection list excludes48");
                if(mode==2 && weapon==1444)Require(hitSlots.Contains(1) && hitSlots.Contains(2) && watched.damage==(int)((double)(int)(38*.9)*.9),"two real ordered NPC hits reduce Shadowbeam damage once each");
                cases++;
            }
            ZeroDamageTail(context);Console.WriteLine("PASS RAY SPELL receivers="+cases+" plus same-call damage0 tail");PublicFlights(context);
        }
        private static void PublicFlights(object context)
        {
            foreach(int weapon in new[]{1295,1444})
            {
                var p=Reset(context,weapon);target=Target(2,new Vector2(995,350));watched=Shot(p,new Vector2(950,350),new Vector2(weapon==1295?15:6,0));
                var attack=Get(Get(context,"Combat"),"Attack");var rawPath=Path(target);var state=new NpcMotionState{Identity=rawPath.Identity,X=target.position.X,Y=target.position.Y,Width=target.width,Height=target.height,CanReceive=true,Active=true,NoTileCollide=true};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();var path=new NpcTrajectory(rawPath.Identity,Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length);
                var args=new object[]{p,path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld"),false};object flight;using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))flight=Call(attack,"ReadFlight",args);var contact=flight==null?null:(AttackContact)Get(flight,"Contact");
                Require((bool)args[3] && contact!=null && (bool)Get(flight,"Current") && (bool)Get(flight,"DependenciesCurrent") && !p.controlUseItem && GetOptional(attack,"ExpectedImpact")==null,"public ray observes its real101-subaction source with complete leases and no hand authority weapon="+weapon);
                int damage=watched.damage;watched.damage++;Require(!(bool)Get(flight,"Current"),"same-GUC actual ray family damage change revokes public Flight");watched.damage=damage;Require((bool)Get(flight,"Current"),"exact ray family restoration restores the same public lease");
                NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);watched.Update(watched.whoAmI);
                Require(first>=0 && first-path.SampleTick==contact.Tick && firstSub==contact.Subupdate,"public ray first contact/subaction equals actual native positive Strike weapon="+weapon);
            }
            Console.WriteLine("PASS RAY SPELL public Flight original2 firstStrike/substep/complete-lease/sameGUC-damage/no-input");
        }
        private static void ZeroDamageTail(object context)
        {
            var p=Reset(context,1444);target=Target(2,new Vector2(956,350),2,2);var front=Target(1,target.Center,2,2);var higher=Target(3,target.Center,2,2);watched=Shot(p,new Vector2(950,350),new Vector2(6,0),1);
            NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var contact=Known(p,watched,path);long sample=path.SampleTick;front.UpdateNPC(1);target.UpdateNPC(2);higher.UpdateNPC(3);watched.Update(watched.whoAmI);
            Console.WriteLine("RAY SPELL zero tail actual="+first+" known="+contact?.Tick+" slots="+string.Join(",",hitSlots)+" finalDamage="+watched.damage);
            Require(contact!=null && first-sample==contact.Tick && hitSlots.Contains(1) && hitSlots.Contains(2) && hitSlots.Contains(3) && watched.damage==0,"OnHit1->0 still completes the native current Damage NPC loop; only later substeps reject damage0");
        }
        private static void Draws(object host,object attack,AttackContact plan)
        {
            Call(attack,"BindPresentation",plan);
            int a=solves,b=steps,c=scans;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;
            for(int check=0;check<20;check++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PresentationCurrent",plan),"stable actual ray preparation lease remains current");}
            Require(a==solves && b==steps && c==scans,"twenty CPU presentations do zero ray Solve/motor/family scans");
        }
        private static AttackContact PrepareNatural(object context,object host,object attack)
        {
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);
            return (AttackContact)GetOptional(attack,"ExpectedImpact");
        }
        private static void Natural(object context)
        {
            foreach(int weapon in new[]{1295,1444})
            {
                var p=Reset(context,weapon);p.position=new Vector2(650,646);Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+180;raw=target.Center-new Vector2(380,0);solves=knownCalls=depth=steps=scans=rngCalls=0;
                var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));
                for(int frame=0;frame<3;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);
                Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active) && solves==0,"withdrawn idle ray preview neither solves nor fires");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));var plan=PrepareNatural(context,host,attack);Require(plan!=null && plan.Confidence==AttackConfidence.Conditional,"actual pressed ray hand prepares under1000ms mechanism allowance");Draws(host,attack,plan);
                ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=true;int previews=0,prepared=0;bool repeatedDraw=false;
                for(int frame=0;frame<45;frame++)
                {
                    var next=frame==0?plan:PrepareNatural(context,host,attack);
                    if(frame>=2){Require(GetOptional(attack,"ExpectedPreview")==null,"all43 stable native ray frames keep the retired hypothetical preview absent");previews++;if(next!=null)prepared++;}
                    // AI048 may finish its101 subactions in the first world
                    // update. A later real opening, not an empty cooldown,
                    // supplies the second twenty-read presentation component.
                    if(frame>=6 && next!=null && !repeatedDraw){Draws(host,attack,next);repeatedDraw=true;}NativeCombatSelectionPreviewChecks.Step(context,raw,true);
                }
                NativeCombatSelectionPreviewChecks.Step(context,raw,false);recordBirth=false;for(int frame=0;frame<4 && born.Any(b=>b.First<0 && b.Key==(int)b.Shot.key && b.Shot.active);frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);
                int useful=0;foreach(var b in born)
                {
                    int actual=b.First<0?0:(int)(b.First-b.Tick+1);Console.WriteLine("RAY SPELL natural weapon="+weapon+" slot/key="+b.Slot+"/"+b.Key+" tick="+b.Tick+" center="+b.Center+" V="+b.Velocity+" cursor/raw="+b.Cursor+"/"+raw+" actual="+actual+" known="+b.Known?.Tick+"/"+b.Known?.Subupdate);
                    Require(actual>0 && b.Known!=null && b.Known.Tick==actual && b.Known.Subupdate==b.FirstSub && Vector2.Dot(b.Velocity,raw-b.Origin)<0,"every deterministic actual assisted ray has a positive immortal488 Strike at its known first receive/substep, opposite physical cursor");useful++;
                }
                Require(useful>=3 && previews==43 && prepared>0 && repeatedDraw && rngCalls==0 && scans==solves+knownCalls,"all43 native ray observations retain genuine repeated prepared hands, two twenty-read presentations and finite work without model RNG");
                Console.WriteLine("PASS RAY SPELL natural "+weapon+" births/useful="+born.Count+"/"+useful+" preview="+previews+" solve/known/capture/motor="+solves+","+knownCalls+","+scans+","+steps+" RNG="+rngCalls);
            }
        }
    }
}
