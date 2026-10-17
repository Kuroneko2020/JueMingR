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
    internal static class NativeCombatInfernoChecks
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
        private static void BeforeKill(Projectile __instance){if(__instance.active && (__instance.type==295 || __instance.type==296))kills++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            // Only actual positive Strike proves useful direct damage.
            // Neither immortal488 life nor NPC323 damage-over-time counts
            // as an owned parent/child projectile receive witness.
            if(receiving==null || (receiving.type!=295 && receiving.type!=296) || receiving.owner!=Main.myPlayer || __result<=0)return;hitSlots.Add(__instance.whoAmI);
            if(!ReferenceEquals(__instance,target) || watched!=null && !ReferenceEquals(watched,receiving))return;
            int sub=receiving.extraUpdates-receiving.numUpdates-1;if(first<0){first=Main.GameUpdateCount;firstSub=sub;}
            foreach(var b in born)if(ReferenceEquals(b.Shot,receiving) && b.Key==(int)receiving.key && b.First<0){b.First=Main.GameUpdateCount;b.FirstSub=sub;}
            foreach(var child in children)if(ReferenceEquals(child.Shot,receiving) && child.Key==(int)receiving.key && child.First<0){child.First=Main.GameUpdateCount;if(child.Root!=null && child.Root.First<0){child.Root.First=Main.GameUpdateCount;child.Root.FirstSub=sub;}}
        }
        private static Player Reset(object context,int weapon=1445)
        {
            recordBirth=recordChildren=false;receiving=watched=null;target=null;first=-1;firstSub=-1;kills=0;born.Clear();children.Clear();hitSlots.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);p.statMana=p.statManaMax=p.statManaMax2=300;p.releaseUseItem=true;Main.windPhysics=false;
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
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Receivers(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path,string phase="BeforeNpc"){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),0},null);}
        private static Projectile Shot(Player p,Vector2 center,Vector2 velocity,int damage=38){return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,p.HeldItem.shoot,damage,0,p.whoAmI)];}
        private static AttackContact Known(Player p,Projectile q,NpcTrajectory path){return (AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Receivers(p,q,path));}
        private sealed class Child
        {internal Projectile Shot;internal int Key,Slot,ParentSlot,ParentKey,Damage,ParentDamage;internal long Tick,First=-1;internal Vector2 Center,ParentCenter;internal bool Occupied;internal Birth Root;}
        private static readonly List<Child> children=new List<Child>();private static bool recordChildren;
        private static void Born(IEntitySource __0,int __result)
        {
            if(__result<0)return;var q=Main.projectile[__result];if(q.type==296 && recordChildren)
            {
                var parent=(__0 as EntitySource_Parent)?.Entity as Projectile;Require(parent!=null && parent.type==295,"296 is born only from its actual295 Kill source");
                var row=new Child{Shot=q,Key=(int)q.key,Slot=q.whoAmI,ParentSlot=parent.whoAmI,ParentKey=(int)parent.key,Damage=q.damage,ParentDamage=parent.damage,Tick=Main.GameUpdateCount,Center=q.Center,ParentCenter=parent.Center,Occupied=parent.active,Root=born.FirstOrDefault(b=>ReferenceEquals(b.Shot,parent) && b.Key==(int)parent.key)};children.Add(row);
                var expected=(Projectile)Static(model,"ChildBirth",parent,q.whoAmI);
                Console.WriteLine("INFERNO child parent/key="+row.ParentSlot+"/"+row.ParentKey+" active/left/pen="+parent.active+","+parent.timeLeft+","+parent.penetrate+" child/key="+q.whoAmI+"/"+q.key+" tick="+row.Tick+" parentCenter="+parent.Center+" center="+q.Center+" damage="+q.damage+" parentDamage="+parent.damage);
                Require(parent.active && parent.timeLeft==0 && q.whoAmI!=parent.whoAmI && q.owner==parent.owner && q.position==expected.position && q.velocity==Vector2.Zero && q.damage==expected.damage && q.knockBack==parent.knockBack && q.width==150 && q.height==150 && !q.tileCollide && q.penetrate==-1 && q.timeLeft==3600 && q.extraUpdates==0,"native Kill retains its occupied parent and births exactly one150x150 child at the actual death center with double .75 damage, before any child AI");return;
            }
            if(!recordBirth || q.type!=295)return;var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=Main.MouseWorld;var v=(Vector2)Static(model,"BirthVelocity",origin,cursor,(float)Get(ammo,"Speed"),p.direction);var path=Path(target);
            var rowParent=new Birth{Shot=q,Key=(int)q.key,Slot=q.whoAmI,Tick=Main.GameUpdateCount,Damage=q.damage,Animation=p.itemAnimation,Time=p.itemTime,Reuse=p.reuseDelay,Center=q.Center,Velocity=q.velocity,Origin=origin,Cursor=cursor,Known=Known(p,q,path)};born.Add(rowParent);
            Console.WriteLine("INFERNO hand tick="+rowParent.Tick+" slot/key="+q.whoAmI+"/"+q.key+" center="+q.Center+" V="+q.velocity+" damage="+q.damage+" cursor/raw="+cursor+"/"+raw+" known="+rowParent.Known?.Tick);
            Require(!(__0 is EntitySource_Parent) && q.magic && q.extraUpdates==0 && q.timeLeft==3600 && q.width==12 && q.height==12 && q.penetrate==1 && q.damage==(int)Get(ammo,"Damage") && Vector2.DistanceSquared(q.Center,origin)<.000001f && Vector2.DistanceSquared(q.velocity,v)<.000001f,"real1445 hand birth uses mounted point and unclamped deterministic AI050 parent");
        }        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostInfernoAttack",true);var audit=new Harmony("JueMingR.Tests.Inferno");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(SolveWork)),finalizer:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(Done)));audit.Patch(model.GetMethod("KnownFamily",Flags),prefix:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(KnownWork)),finalizer:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(Done)));audit.Patch(model.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(StepWork)));audit.Patch(model.GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(ScanWork)));
            foreach(var method in Main.rand.GetType().GetMethods(Flags).Where(m=>m.Name=="Next" || m.Name=="NextDouble").Distinct())audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(RngWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(Born)));audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(BeforeKill)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(AfterDamage)));audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatInfernoChecks),nameof(Strike)));
            try
            {
                string leaf=Environment.GetEnvironmentVariable("JMR_INFERNO_ORACLE_ONLY");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="birth" || leaf=="receive" || leaf=="natural","Inferno leaf selector");
                if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);if(string.IsNullOrEmpty(leaf) || leaf=="birth")Births(context);if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);if(string.IsNullOrEmpty(leaf) || leaf=="natural")Natural(context);
            }
            finally{recordBirth=recordChildren=false;receiving=watched=null;target=null;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Motors(object context)
        {
            int actions=0;foreach(int type in new[]{295,296})for(int scene=0;scene<8;scene++)
            {
                var p=Reset(context);target=Target(2,new Vector2(1750,500));
                if(scene>=1 && scene<=3)for(int x=5;x<115;x++)for(int y=10;y<50;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                if(scene==4)for(int y=10;y<50;y++)NativeToolsChecks.Tile(60,y,1);
                var q=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),scene==7?new Vector2(5,350):new Vector2(950,350),new Vector2(scene==4?8:1,0),type,38,0,p.whoAmI)];
                if(scene==5)q.timeLeft=1;if(scene==6 && type==296)q.ai[0]=536;if(scene==6 && type==295)q.penetrate=0;
                var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();recordChildren=true;
                for(int outer=1;outer<=136 && q.active;outer++)
                {
                    Require((bool)Static(model,"Step",copy,terrain),"Inferno pure motor has necessary cells");if(copy.active)Static(model,"Finish",copy);actions++;q.Update(q.whoAmI);
                    Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.alpha==copy.alpha && q.ai.SequenceEqual(copy.ai) && q.localAI.SequenceEqual(copy.localAI) && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.shimmerWet==copy.shimmerWet && Vector2.DistanceSquared(q.position,copy.position)<.0001f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.000001f,"native Inferno motor "+type+"/"+scene+"/"+outer+" actual="+q.position+"/"+q.velocity+" ai/left="+q.ai[0]+","+q.timeLeft+" model="+copy.position+"/"+copy.velocity+" ai/left="+copy.ai[0]+","+copy.timeLeft);
                }
                if(type==296 && scene==0)Require(!q.active && q.ai[0]==544 && kills==1,"296 ends before Damage on actual136th AI, rather than one instantaneous explosion");
                if(scene==7)Require(kills==0 && children.Count==0 && q.timeLeft==3600,"body-world active=false retires without invoking Kill or creating296");
                if(type==295 && (scene==4 || scene==5 || scene==6))Require(children.Count==1,"wall/expiry/penetration death each has exactly one native child");
                recordChildren=false;
            }
            Console.WriteLine("PASS INFERNO motors16 actions="+actions);
        }
        private static void Births(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+180;raw=target.Center;ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=recordChildren=true;
            for(int frame=0;frame<45;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,frame==0);
            Require(born.Count==1 && children.Count==1 && children[0].Root==born[0] && children[0].Occupied && children[0].Damage==(int)((double)born[0].Damage*.75),"real press/release finishes one source followed by one actual occupied-parent Kill birth");recordBirth=recordChildren=false;
            Console.WriteLine("PASS INFERNO actual hand1 -> child1");
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(bool lower in new[]{false,true})for(int gate=0;gate<5;gate++)
            {
                var p=Reset(context);target=Target(2,new Vector2(1010,400),10,10);var front=Target(1,new Vector2(970,350));if(gate==1)target.immune[p.whoAmI]=200;if(gate==2)target.reflectsProjectiles=true;if(gate==4)front.immune[p.whoAmI]=200;
                Projectile filler=null;if(lower)filler=Shot(p,new Vector2(800,500),Vector2.Zero,0);watched=Shot(p,new Vector2(950,350),new Vector2(8,0),gate==3?1:70);if(filler!=null)filler.active=false;
                // Parent raw body cannot touch selected Y395..405. Only its
                // real Kill child can be the positive useful source.
                Require(watched.position.Y+watched.height<target.position.Y,"selected is geometrically outside every horizontal parent body");
                var initialBox=watched.Hitbox;var firstBox=initialBox;firstBox.Offset(8,0);
                var childBox=new Rectangle(883,275,150,150);
                Require(!initialBox.Intersects(front.Hitbox) && firstBox.Intersects(front.Hitbox) && childBox.Intersects(target.Hitbox),"front receives after exactly one native parent movement; its center958 child already overlaps selected");
                NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var contact=Known(p,watched,path);long sample=path.SampleTick;watched=null;recordChildren=true;
                Require(Static(model,"KnownFamily",p,Main.projectile.Where(q=>q.active && q.type==295).ToArray(),path,Clock(path,"Projectiles"),Terrain(),Receivers(p,Main.projectile.First(q=>q.active && q.type==295),path))==null,"Inferno whole family declines partial projectile-array replay");
                for(int outer=1;outer<=20;outer++)
                {
                    if(outer>1)NativeQuickItemChecks.BeginWorldStep();front.UpdateNPC(1);target.UpdateNPC(2);
                    for(int slot=0;slot<Main.maxProjectiles;slot++){var q=Main.projectile[slot];if(q.active && (q.type==295 || q.type==296))q.Update(slot);}
                }
                int actual=first<0?0:(int)(first-sample);bool negative=gate==1 || gate==3;
                Console.WriteLine("INFERNO causal receive lower/gate="+lower+"/"+gate+" actual="+actual+" known="+contact?.Tick+" child="+string.Join(",",children.Select(c=>c.Slot+"@"+c.Tick+" damage"+c.Damage+" first"+c.First))+" owner="+target.immune[p.whoAmI]);
                Require(children.Count==1 && (actual==0)==negative && (contact==null)==negative && (contact==null || contact.Tick==actual),"actual original-array occupied parent/child, owner200 and zero-derived damage agree with causal Known");
                if(!negative)Require(children[0].Tick==sample+1 && children[0].Center==new Vector2(958,350) && actual==(lower?2:1) && first==children[0].Tick+(lower?1:0) && children[0].First==first,"first-outer Kill creates higher child acting the same outer; lower child starts exactly the next outer");
                if(gate==4)Require(hitSlots.Contains(1),"parent pen1 really bypasses front owner200 before its child shares ordinary owner history");cases++;recordChildren=false;
            }
            ChildWindow(context);Console.WriteLine("PASS INFERNO causal receives="+cases+" plus child lifetime/owner window");
        }
        private static void ChildWindow(object context)
        {
            var p=Reset(context);target=Target(2,new Vector2(980,350),10,10);watched=Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(950,350),Vector2.Zero,296,52,0,p.whoAmI)];target.immune[p.whoAmI]=134;
            NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var contact=Known(p,watched,path);
            Require(contact==null,"120-point path cannot claim a first receive beyond its finite prediction window");
            for(int outer=1;outer<=136;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);watched.Update(watched.whoAmI);}
            Require(first>=0 && watched.ai[0]==544 && !watched.active && watched.timeLeft==0,"actual child remains damaging through135, with owner once per world; firstexpiry540 crossing kills before136 Damage");
            Console.WriteLine("INFERNO child late actual="+first+" (outside120-point oracle), retiredAI="+watched.ai[0]);
        }
        private static void Draws(object host,object attack,AttackContact plan,object flight=null)
        {
            if(flight==null)Call(attack,"BindPresentation",plan);
            int a=solves,b=steps,c=scans;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;
            for(int check=0;check<20;check++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require(flight==null?(bool)Call(attack,"PresentationCurrent",plan):(bool)Call(flight,"PresentationCurrent",0),"stable actual Inferno hand/family preparation remains current");}
            Require(a==solves && b==steps && c==scans,"twenty CPU presentations have zero Inferno Solve/motor/family scans");
        }
        private static AttackContact PrepareNatural(object context,object host,object attack)
        {
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);
            return (AttackContact)GetOptional(attack,"ExpectedImpact");
        }
        private static object Observe(object attack,Player player)
        {
            var args=new object[]{player,Path(target,true),Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld"),false};
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return Call(attack,"ReadFlight",args);
        }
        private static void Natural(object context)
        {
            string only=Environment.GetEnvironmentVariable("JMR_INFERNO_NATURAL_CASE");Require(string.IsNullOrEmpty(only) || only=="False" || only=="True","Inferno natural case selector");
            foreach(bool causal in new[]{false,true})
            {
                if(!string.IsNullOrEmpty(only) && only!=causal.ToString())continue;Console.WriteLine("INFERNO natural executing causal="+causal);
                var p=Reset(context);p.position=new Vector2(650,646);Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+180;raw=target.Center-new Vector2(380,0);
                if(causal)
                {
                    // A directed receiver premise, separate from selection:
                    // immortal excludes NPC3 from CombatSelection, but the
                    // original player-owned295 still strikes it (npcProj=false).
                    // Preserve the actual488 dummy contract unchanged.
                    Target(1,new Vector2(p.Center.X+120,target.Center.Y),18,60).immortal=true;
                }
                solves=knownCalls=depth=steps=scans=rngCalls=0;var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));
                for(int frame=0;frame<3;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active) && solves==0 && (int)Static(typeof(NativeCombatSelectionPreviewChecks),"Selected",host)==target.whoAmI,"withdrawn idle Inferno preview selects the actual dummy without solving or shooting");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));var plan=PrepareNatural(context,host,attack);Require(plan!=null,"actual pressed Inferno parent/child hand prepares under1000ms mechanism allowance");Draws(host,attack,plan);
                ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);recordBirth=recordChildren=true;int previews=0;
                int frames=causal?30:93,observations=0;
                for(int frame=0;frame<frames;frame++)
                {
                    //1445 has no native autoReuse. Use real release edges,
                    // not a invented held-controller/automatic input right.
                    bool pressed=frame%31!=30;var next=pressed?(frame==0?plan:PrepareNatural(context,host,attack)):null;
                    var flight=next==null?Observe(attack,p):null;var observed=next??(flight==null?null:(AttackContact)Get(flight,"Contact"));
                    if(frame>=2){observations++;if(observed!=null)previews++;}if(frame==6 && observed!=null)Draws(host,attack,observed,flight);
                    NativeCombatSelectionPreviewChecks.Step(context,raw,pressed);
                    if(!pressed)Require(GetOptional(attack,"ExpectedPreview")==null,"real Inferno release edge cannot retain a hypothetical next-use preview");
                }
                NativeCombatSelectionPreviewChecks.Step(context,raw,false);recordBirth=false;
                for(int frame=0;frame<40 && born.Any(b=>b.First<0);frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);recordChildren=false;
                Require(observations==frames-2 && previews>0,"all original Inferno stable observations retain real pressed-hand/owned-family evidence without inventing cooldown or released hands");
                foreach(var b in born)
                {
                    int actual=b.First<0?0:(int)(b.First-b.Tick+1);Console.WriteLine("INFERNO natural causal="+causal+" born="+b.Tick+" key="+b.Key+" cursor/raw="+b.Cursor+"/"+raw+" native="+actual+" known="+b.Known?.Tick);
                    Require(actual>0 && b.Known!=null && b.Known.Tick==actual && Vector2.Dot(b.Velocity,raw-b.Origin)<0,"real assisted input and causal family positive Strike equal Known first receive; immortal488 life is not the damage witness");
                }
                Require(born.Count==(causal?1:3) && children.Count==born.Count && children.All(c=>c.Root!=null) && rngCalls==0 && scans==solves+knownCalls,"each real press completes one source and one owned Kill child, with finite preparation/no model RNG");
                if(causal)Require(children[0].First==born[0].First,"selected native first positive comes from the child after a front NPC really consumes the parent");
                Console.WriteLine("PASS INFERNO natural causal="+causal+" hand/children="+born.Count+"/"+children.Count+" preview="+previews+" solve/known/capture/motor="+solves+","+knownCalls+","+scans+","+steps+" RNG="+rngCalls);
            }
        }
        private static void MissingPreview(Player p,object host,object attack,bool causal,int frame,AttackContact preview)
        {
            var path=(NpcTrajectory)Call(Get(Get(host,"Prediction"),"Cache"),"Read",1);var impact=(AttackContact)GetOptional(attack,"ExpectedImpact");
            var source=Main.projectile.Where(q=>q.active && q.owner==p.whoAmI && (q.type==295 || q.type==296)).ToArray();AttackContact known=null;
            if(path!=null && source.Length>0){var receiver=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,source[0],path,0,false,true},null);known=(AttackContact)Static(model,"KnownFamily",p,source,path,Clock(path,"CompletedWorld"),Terrain(),receiver);}
            Console.WriteLine("INFERNO missing preview causal/frame="+causal+"/"+frame+" GUC="+Main.GameUpdateCount+" animation/time/release="+p.itemAnimation+","+p.itemTime+","+p.releaseUseItem+" selected="+Static(typeof(NativeCombatSelectionPreviewChecks),"Selected",host)+" selectedOwner="+target.immune[p.whoAmI]+" frontOwner="+Main.npc[1].immune[p.whoAmI]+" pathSample/count="+path?.SampleTick+","+path?.Count+" impact/preview/familyKnown="+impact?.Tick+","+preview?.Tick+","+known?.Tick+" family="+string.Join(";",source.Select(q=>q.whoAmI+"/"+q.key+" type/owner="+q.type+","+q.owner+" center"+q.Center+" ai/left/pen="+q.ai[0]+","+q.timeLeft+","+q.penetrate)));
        }
    }
}
