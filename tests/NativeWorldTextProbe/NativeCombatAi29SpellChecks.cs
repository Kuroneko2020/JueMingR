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
    internal static class NativeCombatAi29SpellChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving,watched,parentKill;
        private static bool births,damageOnlyOverlap,rawReflectionOverlap;private static object ammo;private static Vector2 raw;private static long first;
        private static int solves,knownCalls,depth,steps,scans,rngCalls,kills;
        private sealed class Birth
        {
            internal Projectile Shot,Snapshot;internal int Key,Slot,Type,ParentSlot=-1;internal long Tick,First=-1;
            internal Vector2 Center,Velocity,Origin,Cursor;internal NpcTrajectory Path;internal object Clock;
            internal readonly List<bool> Skips=new List<bool>();
        }
        private static readonly List<Birth> born=new List<Birth>();private static readonly List<bool> aiSkips=new List<bool>();private static readonly List<int> hits=new List<int>();
        private static object Static(Type t,string name,params object[] args){return t.GetMethod(name,Flags).Invoke(null,args);}
        // Native NewProjectile stores knockBack in a Single field. On x86 a
        // multiply kept in x87 can compare unequal to that field even while
        // both print the same Single. Compare exact stored bits, not a tolerance.
        private static bool SameStoredFloat(float actual,float expected){return BitConverter.ToInt32(BitConverter.GetBytes(actual),0)==BitConverter.ToInt32(BitConverter.GetBytes(expected),0);}
        private static void SolveWork(){solves++;depth++;}private static void KnownWork(){knownCalls++;depth++;}private static void Done(){depth--;}
        private static void StepWork(){steps++;}private static void ScanWork(){scans++;}private static void RngWork(){if(depth>0)rngCalls++;}
        private static void BeforeAi(Projectile __instance,out int __state){__state=(int)__instance.ai[0];}
        private static void AfterAi(Projectile __instance,int __state)
        {
            if(__instance.type!=731)return;int next=__state+1;bool skip=next%2!=0 && ((int)__instance.ai[0]==next+1 || next+1==20 && __instance.ai[0]==0);aiSkips.Add(skip);
            foreach(var b in born)if(ReferenceEquals(b.Shot,__instance) && b.Key==(int)__instance.key)b.Skips.Add(skip);
        }
        private static void BeforeKill(Projectile __instance,out Projectile __state){__state=parentKill;if(__instance.type==521){parentKill=__instance;kills++;}}
        private static void AfterKill(Projectile __state){parentKill=__state;}
        private static void BeforeDamage(Projectile __instance)
        {
            receiving=__instance;
            if(ReferenceEquals(watched,__instance) && target!=null && target.reflectsProjectiles && __instance.Damage_GetHitbox().Intersects(target.Hitbox))
            {if(__instance.Hitbox.Intersects(target.Hitbox))rawReflectionOverlap=true;else damageOnlyOverlap=true;}
        }
        private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(receiving==null || (receiving.type!=521 && receiving.type!=522 && receiving.type!=731) || receiving.owner!=Main.myPlayer || receiving.damage<=0 || __result<=0)return;hits.Add(__instance.whoAmI);
            if(!ReferenceEquals(__instance,target) || watched!=null && !ReferenceEquals(watched,receiving))return;if(first<0)first=Main.GameUpdateCount;
            foreach(var b in born)if(ReferenceEquals(b.Shot,receiving) && b.Key==(int)receiving.key && b.First<0)b.First=Main.GameUpdateCount;
        }
        private static Player Reset(object context,int weapon=3209)
        {
            births=damageOnlyOverlap=rawReflectionOverlap=false;receiving=watched=parentKill=null;target=null;first=-1;kills=0;born.Clear();hits.Clear();aiSkips.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);p.statMana=p.statManaMax=p.statManaMax2=400;p.releaseUseItem=true;Main.windPhysics=false;
            for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center,int width=18,int height=40)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=width;n.height=height;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;n.realLife=-1;
            Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.lifeRegen=n.lifeRegenCount=0;return n;
        }
        private static NpcTrajectory Path(NPC n,int offset=0)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();
            return new NpcTrajectory(key,(long)Main.GameUpdateCount-1-offset,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Receivers(Player p,Projectile q,NpcTrajectory path){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,true,false},null);}
        private static object Clock(NpcTrajectory path,string phase="BeforeNpc"){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),phase),0},null);}
        private static Projectile Shot(Player p,int type,Vector2 center,Vector2 velocity,int damage=40){return Main.projectile[Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,type,damage,0,p.whoAmI)];}
        private static AttackContact Known(Player p,Projectile q,NpcTrajectory path){return (AttackContact)Static(model,"KnownFamily",p,new[]{q},path,Clock(path),Terrain(),Receivers(p,q,path));}
        private static void Born(IEntitySource __0,int __result)
        {
            if(!births || __result<0)return;var q=Main.projectile[__result];if(q.type!=521 && q.type!=522 && q.type!=731)return;var p=Main.LocalPlayer;var origin=p.RotatedRelativePoint(p.MountedCenter);var cursor=Main.MouseWorld;
            var row=new Birth{Shot=q,Snapshot=(Projectile)Static(model,"Copy",q),Key=(int)q.key,Slot=q.whoAmI,Type=q.type,Tick=Main.GameUpdateCount,Center=q.Center,Velocity=q.velocity,Origin=origin,Cursor=cursor};
            if(__0 is EntitySource_Parent source && source.Entity is Projectile parent)
            {
                Require(q.type==522 && parent.type==521 && ReferenceEquals(parent,parentKill),"only real local Serpent Kill contributes its splinter family");row.ParentSlot=parent.whoAmI;row.Path=Path(target,row.Slot<parent.whoAmI?1:0);
                var expected=parent.oldPosition+new Vector2(parent.width/2,parent.height/2);float speed=q.velocity.Length();
                Console.WriteLine("AI29 child parent="+parent.whoAmI+" slot/key="+row.Slot+"/"+row.Key+" center="+q.Center+" oldBirth="+expected+" V="+q.velocity+" damage="+q.damage+" sameOuter="+(row.Slot>parent.whoAmI));
                Console.WriteLine("AI29 child exact center="+q.Center.X.ToString("R")+","+q.Center.Y.ToString("R")+" expected="+expected.X.ToString("R")+","+expected.Y.ToString("R")+" equal="+(q.Center==expected)+" speed="+speed.ToString("R")+" legal="+(speed>=7-.00001f && speed<=10+.00001f)+" damageExpected="+(int)((double)parent.damage*.8)+" kb="+q.knockBack.ToString("R")+" parentKb="+parent.knockBack.ToString("R")+" kbExpected="+(parent.knockBack*.8f).ToString("R")+" kbEqual="+(q.knockBack==parent.knockBack*.8f)+" extra="+q.extraUpdates);
                Console.WriteLine("AI29 child origin inputs parentOld="+parent.oldPosition.X.ToString("R")+","+parent.oldPosition.Y.ToString("R")+" parentSize="+parent.width+","+parent.height+" integerHalf="+(parent.width/2)+","+(parent.height/2)+" childPosition="+q.position.X.ToString("R")+","+q.position.Y.ToString("R")+" childSize="+q.width+","+q.height);
                Console.WriteLine("AI29 child stored kb actualBits="+BitConverter.ToInt32(BitConverter.GetBytes(q.knockBack),0).ToString("X8")+" expectedBits="+BitConverter.ToInt32(BitConverter.GetBytes(parent.knockBack*.8f),0).ToString("X8")+" exactStoredEqual="+SameStoredFloat(q.knockBack,parent.knockBack*.8f));
                Require(q.Center==expected && speed>=7-.00001f && speed<=10+.00001f && q.damage==(int)((double)parent.damage*.8) && SameStoredFloat(q.knockBack,parent.knockBack*.8f) && q.extraUpdates==0,"native parent oldPosition,7..10 velocity,double damage truncation and no inherited extra update");
            }
            else
            {
                row.Path=Path(target);var desired=(cursor-origin).SafeNormalize(-Vector2.UnitX)*(float)Get(ammo,"Speed");
                Console.WriteLine("AI29 hand weapon="+p.HeldItem.type+" type="+q.type+" tick="+row.Tick+" slot="+row.Slot+" center="+q.Center+" V="+q.velocity+" cursor="+cursor+" raw="+raw+" damage="+q.damage);
                Require(q.Center==origin && Vector2.DistanceSquared(q.velocity,desired)<.000001f && q.damage==(int)Get(ammo,"Damage") && q.extraUpdates==1,"actual single hand birth has native origin/speed/damage, no RNG copied into preparation");
            }
            row.Clock=Clock(row.Path);born.Add(row);
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAi29SpellAttack",true);var audit=new Harmony("JueMingR.Tests.Ai29Spells");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            // Budget cancellation must close the observation scope too; a
            // skipped postfix would mislabel later native RNG as model work.
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(SolveWork)),finalizer:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(Done)));
            foreach(string name in new[]{"KnownFamily","KnownClock"})audit.Patch(model.GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(KnownWork)),finalizer:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(Done)));
            audit.Patch(model.GetMethod("Motor",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(StepWork)));audit.Patch(model.GetMethod("Capture",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(ScanWork)));
            foreach(var method in Main.rand.GetType().GetMethods(Flags).Where(m=>m.Name=="Next" || m.Name=="NextDouble").Distinct())audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(RngWork)));
            audit.Patch(typeof(Projectile).GetMethod("AI_029_GemStaffs",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(BeforeAi)),postfix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(AfterAi)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Kill",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(BeforeKill)),postfix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(AfterKill)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatAi29SpellChecks),nameof(Strike)));
            try
            {
                string leaf=Environment.GetEnvironmentVariable("JMR_AI29_SPELL_ORACLE_ONLY");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="birth" || leaf=="receive" || leaf=="natural","AI29 spell leaf selector");
                if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);if(string.IsNullOrEmpty(leaf) || leaf=="birth")Births(context);if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);if(string.IsNullOrEmpty(leaf) || leaf=="natural")Natural(context);
            }
            finally{births=false;receiving=watched=parentKill=null;target=null;Main.windPhysics=wind;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());}
        }
        private static void Motors(object context)
        {
            int actions=0,cases=0;foreach(int type in new[]{521,522,731})for(int scene=0;scene<8;scene++)
            {
                var p=Reset(context,type==731?4062:3209);target=Target(2,new Vector2(1750,450));
                if(scene==1 || scene==2 || scene==4)for(int x=40;x<115;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==1?0:scene==2?2:3);}
                if(scene==3)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                if(scene==6 || scene==7)for(int x=40;x<115;x++){Main.tile[x,21].liquid=255;Main.tile[x,21].liquidType(3);}
                var q=Shot(p,type,new Vector2(950,350),scene==3?new Vector2(20,2):new Vector2(6,scene==6?2:scene==7?-2:0));if(scene==5){if(type==522)q.ai[1]=40;else if(type==731)q.ai[0]=8;else q.timeLeft=1;}
                var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();
                for(int outer=1;outer<=20 && q.active;outer++)
                {
                    aiSkips.Clear();q.Update(q.whoAmI);int updates=copy.extraUpdates+1,index=0;
                    for(int sub=0;sub<updates;sub++)
                    {
                        bool skip=type==731 && index<aiSkips.Count && aiSkips[index++];Require((bool)Static(model,"Step",copy,terrain,skip),"AI29 motor retains all scene dependencies");actions++;if(!copy.active)break;Static(model,"Finish",copy);if(!copy.active){if(sub+1<updates)Require((bool)Static(model,"RetiredStep",copy,terrain),"post-tail inactive motion never repeats AI/Damage");break;}
                    }
                    Console.WriteLine("AI29 motor type/scene/outer="+type+"/"+scene+"/"+outer+" clock="+string.Join(",",aiSkips)+" native="+q.position+"/"+q.velocity+"/"+q.ai[0]+","+q.ai[1]+" left="+q.timeLeft+" model="+copy.position+"/"+copy.velocity+"/"+copy.ai[0]+","+copy.ai[1]+" left="+copy.timeLeft);
                    Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.alpha==copy.alpha && q.penetrate==copy.penetrate && q.ai.SequenceEqual(copy.ai) && q.frame==copy.frame && q.frameCounter==copy.frameCounter && q.wet==copy.wet && q.honeyWet==copy.honeyWet && q.shimmerWet==copy.shimmerWet && Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.0001f,"native role-specific motor agrees including actual Thunder random clock and zero-width splinter collision");
                }
                if(scene==6 || scene==7)Require(q.timeLeft==copy.timeLeft,"AI29 surface reflection does not borrow AI1 minus60 lifetime");cases++;
            }
            Console.WriteLine("PASS AI29 SPELL motors="+cases+" actions="+actions+" native RNG observed but never replaced");
        }
        private static void Births(object context)
        {
            foreach(int weapon in new[]{3209,4062})
            {
                var p=Reset(context,weapon);p.position=new Vector2(650,646);Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+35;raw=target.Center;
                ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);births=true;for(int frame=0;frame<35;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,frame==0);births=false;
                Require(born.Count(b=>b.ParentSlot<0)==1 && first>=0,"one actual press produces one hand parent and a real positive Strike");var children=born.Where(b=>b.Type==522).ToArray();
                if(weapon==3209)Require(children.Length>=3 && children.Length<=5 && children.All(b=>b.ParentSlot==born.Single(v=>v.ParentSlot<0).Slot),"real parent Update/Damage/Kill produces exactly its native3..5 children, no visual-only classification");else Require(children.Length==0,"Thunder Kill has no damage burst or child family");
                CompareBorn(p);Console.WriteLine("PASS AI29 birth weapon="+weapon+" hand1 children="+children.Length+" actual first="+first);
            }
            // A parent may occupy a high slot while a lower free slot was
            // already visited. The child receives its FIRST update next GUC.
            var owner=Reset(context);target=Target(2,new Vector2(1100,350),32,160);for(int i=0;i<3;i++)Shot(owner,522,new Vector2(1700,500),Vector2.Zero);var parent=Shot(owner,521,new Vector2(950,350),new Vector2(8.5f,0));Main.projectile[0].active=false;parent.timeLeft=1;parent.damage=41;births=true;
            for(int outer=0;outer<45;outer++){NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);for(int slot=0;slot<Main.projectile.Length;slot++)if(Main.projectile[slot].active)Main.projectile[slot].Update(slot);}births=false;
            Require(born.Count>=3 && born.Count<=5 && born.Any(b=>b.Slot<parent.whoAmI) && born.All(b=>b.Snapshot.damage==32),"real tail Kill retains parent occupied slot, lower/higher birth order and conv.i4 double32 damage");CompareBorn(owner);Console.WriteLine("PASS AI29 lower/higher native parent-child slot order");
            PooledBirth(context);
        }
        private static void PooledBirth(object context)
        {
            var p=Reset(context);p.position=new Vector2(940,329);p.gravDir=1;Main.screenPosition=Vector2.Zero;var origin=p.RotatedRelativePoint(p.MountedCenter);
            target=Target(2,new Vector2(1040,350),200,200);var retained=new Vector2(1013,343);var body=Main.projectile[0];Require(!body.active,"pooled birth uses a genuinely free native first slot");body.oldPosition=retained;
            int x=(int)(origin.X/16),y=(int)((origin.Y-7)/16);for(int row=y-1;row<=y;row++){Main.tile[x,row].liquid=255;Main.tile[x,row].liquidType(3);}
            ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);var sample=new Projectile();sample.SetDefaults(521);sample.owner=p.whoAmI;
            var path=Path(target);var receive=Receivers(p,sample,path);var pool=Call(receive,"CaptureBirthSlots");object[] args={0,Vector2.Zero};Require((bool)Call(pool,"TryRetainedPosition",args),"actual free slot has a retained birth-origin result");bool pooled=(bool)Get(pool,"PoolsInstances");var saved=(Vector2)args[1];
            Require(saved==(pooled?retained:Vector2.Zero) && (bool)Get(pool,"Current"),"native NoPooling decides retained oldPosition, with a current root lease");
            body.oldPosition+=Vector2.UnitX;Require((bool)Get(pool,"Current")==!pooled,"only a pooled consumed oldPosition change invalidates preparation");body.oldPosition=retained;Require((bool)Get(pool,"Current"),"exact oldPosition restoration restores the unchanged lease");
            bool noPooling=Main.NoPooling;try{Main.NoPooling=!noPooling;Require(!(bool)Get(pool,"Current"),"changing the actual pooling mode invalidates the consumed slot lease");}finally{Main.NoPooling=noPooling;}Require((bool)Get(pool,"Current"),"restored pooling mode restores the same preparation");
            var plan=(AttackContact)Static(model,"Solve",p,ammo,path,origin,0,true,Terrain(),receive,0);
            raw=plan==null?target.Center:new Vector2(plan.AimX,plan.AimY);Main.mouseX=(int)raw.X;Main.mouseY=(int)raw.Y;var velocity=(Main.MouseWorld-origin).SafeNormalize(-Vector2.UnitX)*(float)Get(ammo,"Speed");births=true;var parent=Shot(p,521,origin,velocity,(int)Get(ammo,"Damage"));
            Console.WriteLine("AI29 first-Shimmer native birth center="+parent.Center+" old="+parent.oldPosition+" captured="+saved+" shimmer="+parent.shimmerWet+" pooling="+pooled+" plan="+(plan==null?"null":plan.Tick.ToString()));
            Require(parent.whoAmI==0 && parent.oldPosition==saved,"real NewProjectile preserved exactly the captured slot origin before first Update");NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);for(int slot=0;slot<Main.projectile.Length;slot++)if(Main.projectile[slot].active)Main.projectile[slot].Update(slot);births=false;
            var children=born.Where(b=>b.Type==522).ToArray();Console.WriteLine("AI29 first-Shimmer pooling="+pooled+" parentSlot="+parent.whoAmI+" retained="+saved+" born="+children.Length+" nativeFirst="+first+" known="+(plan==null?"null":plan.Tick.ToString()));
            Require(!parent.active && parent.timeLeft==0 && kills==1 && children.Length>=3 && children.Length<=5 && children.All(b=>b.ParentSlot==0 && b.Slot>0 && b.Center==saved+new Vector2(7,7)),"first full-Shimmer Kill births native3..5 from pre-store oldPosition in same-outer higher slots");
            Require(pooled?plan!=null && plan.Tick==1 && plan.Confidence==AttackConfidence.Representative:plan==null,"first-Shimmer child geometry uses retained pooled origin or new-instance zero, never parent center");
            Require(pooled?first==(long)Main.GameUpdateCount && plan.Tick==1:first<0 && plan==null,"native useful child Damage and representative contact agree across the actual pooling mode");
            Console.WriteLine("PASS AI29 first-Shimmer pooled birth and consumed oldPosition lease negative/restore controls");
        }
        private static void CompareBorn(Player p)
        {
            // Each recorded body is replayed in an isolated otherwise empty
            // family after the actual run. This is a born-body oracle, not a
            // promise to rewind a live partial projectile array. MaxPen1 and
            // no local/static cooldown make that selected receive independent
            // of the other recorded splinters; their actual birth slot decides
            // whether the first update was this GUC or the following one.
            foreach(var b in born)
            {
                foreach(var live in Main.projectile)live.active=false;var q=(Projectile)Static(model,"Copy",b.Snapshot);Main.projectile[b.Slot]=q;Array.Clear(target.immune,0,target.immune.Length);
                AttackContact known=b.Type==731?(AttackContact)Static(model,"KnownClock",p,q,b.Path,b.Clock,Terrain(),Receivers(p,q,b.Path),b.Skips.ToArray()):(AttackContact)Static(model,"KnownFamily",p,new[]{q},b.Path,b.Clock,Terrain(),Receivers(p,q,b.Path));
                int actual=b.First<0?0:(int)(b.First-b.Tick+1);Console.WriteLine("AI29 recorded body type/slot="+b.Type+"/"+b.Slot+" parent="+b.ParentSlot+" first="+actual+" known="+known?.Tick+" clocks="+b.Skips.Count+" origin="+b.Center+" V="+b.Velocity);
                Require((known==null)==(actual==0) && (known==null || known.Tick==actual),"each actual born body keeps its own native positive/missed contact under the recorded clock/slot timing");
            }
        }
        private static void Receives(object context)
        {
            int cases=0;foreach(int type in new[]{521,522,731})foreach(int mode in new[]{0,1,2,3})
            {
                var p=Reset(context,type==731?4062:3209);target=Target(2,new Vector2(980,350));if(mode==1)target.immune[p.whoAmI]=200;if(mode==2)target.reflectsProjectiles=true;watched=Shot(p,type,new Vector2(950,350),new Vector2(8,0));if(mode==3)watched.damage=0;
                NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var known=Known(p,watched,path);long sample=path.SampleTick;
                for(int outer=1;outer<=45 && watched.active;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);watched.Update(watched.whoAmI);}int actual=first<0?0:(int)(first-sample);Console.WriteLine("AI29 receive type/mode="+type+"/"+mode+" actual="+actual+" known="+known?.Tick+" immune="+target.immune[p.whoAmI]);
                // Keep the original 522/731 at980: their expanded damage body
                // reaches the NPC before the raw body can reflect. That is a
                // positive native Strike, not a failed reflection negative.
                if(mode==2)Console.WriteLine("AI29 reflection witness type="+type+" expandedOnly="+damageOnlyOverlap+" rawOverlap="+rawReflectionOverlap+" reflected="+watched.reflected+" finalBody="+watched.Hitbox+" target="+target.Hitbox);
                bool negative=mode==3 || mode==2 && type==521;Require((actual==0)==negative && (known==null)==negative && (known==null || known.Tick==actual),"real inflated AI29 damage body/pen1 owner bypass/reflection/zeroDamage agrees");if(type!=521 && mode==2)Require(damageOnlyOverlap && !rawReflectionOverlap && !watched.reflected,"original splinter/Thunder scene has real expanded-only Damage before any raw reflection overlap");if(type==521 && mode==2)Require(rawReflectionOverlap && watched.reflected && !watched.friendly && watched.hostile,"original parent reflection negative has a native raw overlap and actual reflection witness");if(type==731 && known!=null)Require(known.Confidence==AttackConfidence.Representative,"captured Thunder cannot make its unknown future random clock Conditional");cases++;
            }
            foreach(int type in new[]{522,731})
            {
                var reflectOwner=Reset(context,type==731?4062:3209);target=Target(2,new Vector2(type==522?974:971,350));target.reflectsProjectiles=true;watched=Shot(reflectOwner,type,new Vector2(950,350),new Vector2(8,0));NativeQuickItemChecks.BeginWorldStep();var reflectPath=Path(target);var reflectedPlan=Known(reflectOwner,watched,reflectPath);
                for(int outer=1;outer<=3 && watched.active;outer++){if(outer>1)NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);watched.Update(watched.whoAmI);}
                Console.WriteLine("AI29 strict raw reflection type="+type+" target="+target.Hitbox+" nativeFirst="+first+" known="+reflectedPlan?.Tick+" expandedOnly="+damageOnlyOverlap+" rawOverlap="+rawReflectionOverlap+" reflected="+watched.reflected);
                Require(first<0 && reflectedPlan==null && rawReflectionOverlap && watched.reflected && !watched.friendly && watched.hostile,"strict raw role overlap reflects before useful Strike, separately from expanded-only positive");
            }
            var owner=Reset(context);target=Target(2,new Vector2(950,362),18,4);watched=Shot(owner,521,new Vector2(950,350),Vector2.Zero);Require(!watched.Hitbox.Intersects(target.Hitbox) && watched.Damage_GetHitbox().Intersects(target.Hitbox),"raw body misses but native AI29 Inflate4 has a strict real overlap");NativeQuickItemChecks.BeginWorldStep();var geometryPath=Path(target);var expanded=Known(owner,watched,geometryPath);target.UpdateNPC(2);watched.Update(watched.whoAmI);Require(first>=0 && expanded!=null && expanded.Tick==1,"strict enlarged native shape is a positive receive, never raw-body substitution");
            var leaseOwner=Reset(context);target=Target(2,new Vector2(980,350));var leased=Shot(leaseOwner,521,new Vector2(950,350),new Vector2(8.5f,0));NativeQuickItemChecks.BeginWorldStep();var leasePath=Path(target);var lease=Receivers(leaseOwner,leased,leasePath);Static(model,"KnownFamily",leaseOwner,new[]{leased},leasePath,Clock(leasePath),Terrain(),lease);var old=leased.oldPosition;
            Require((bool)Get(lease,"Unchanged"),"Serpent's original death-origin lease is current");leased.oldPosition+=Vector2.UnitX;Require(!(bool)Get(lease,"Unchanged"),"only oldPosition changes actual Kill children birth and must invalidate the prepared family");leased.oldPosition=old;Require((bool)Get(lease,"Unchanged"),"restoring exact native oldPosition restores the unchanged death-origin lease");
            Console.WriteLine("PASS AI29 receives="+cases+" plus native inflated-body edge");
            PublicFlights(context);
        }
        private static void PublicFlights(object context)
        {
            foreach(int type in new[]{521,522,731})
            {
                var p=Reset(context,type==731?4062:3209);target=Target(2,new Vector2(950,350));watched=Shot(p,type,target.Center,new Vector2(8,0));
                var attack=Get(Get(context,"Combat"),"Attack");long sample=Main.GameUpdateCount;var flight=Observe(attack,p);
                var contact=flight==null?null:(AttackContact)Get(flight,"Contact");
                Require(contact!=null && (bool)Get(flight,"Current") && (bool)Get(flight,"DependenciesCurrent") && !p.controlUseItem && GetOptional(attack,"ExpectedImpact")==null,"public AI29 observes the real parent/splinter/Thunder source with a complete lease and no hand authority type="+type);
                int damage=watched.damage;watched.damage++;Require(!(bool)Get(flight,"Current"),"same-GUC actual AI29 family damage change revokes public Flight");watched.damage=damage;Require((bool)Get(flight,"Current"),"exact AI29 family restoration restores the same public lease");
                NativeQuickItemChecks.BeginWorldStep();target.UpdateNPC(2);watched.Update(watched.whoAmI);
                Require(first>=0 && first-sample==contact.Tick && contact.Tick==1,"public AI29 first contact equals an actual native positive Strike before any unknown later random clock type="+type);
            }
            Console.WriteLine("PASS AI29 public Flight original3 firstStrike/complete-lease/sameGUC-damage/no-input");
        }
        private static void Draws(object host,object attack,AttackContact plan,object flight=null)
        {
            if(flight==null)Call(attack,"BindPresentation",plan);
            int a=solves,b=steps,c=scans;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;
            for(int check=0;check<20;check++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require(flight==null?(bool)Call(attack,"PresentationCurrent",plan):(bool)Call(flight,"PresentationCurrent",0),"stable actual spell hand/family preparation lease remains current");}
            Require(a==solves && b==steps && c==scans,"twenty CPU presentations perform zero spell Solve/motor/family capture");
        }
        private static AttackContact PrepareNatural(object context,object host,object attack)
        {
            // This is a pressed hand at the completed-world boundary. The next
            // native Player update consumes it; idle and released hands do not
            // borrow this finite1000ms mechanism component allowance.
            NativeToolExecutionChecks.Sample(context,Get(context,"Input"),raw,true);Call(Get(context,"Combat"),"Sample");
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,target,0);
            return (AttackContact)GetOptional(attack,"ExpectedImpact");
        }
        private static object Observe(object attack,Player player)
        {
            var args=new object[]{player,Path(target,-1),Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),"CompletedWorld"),false};
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))return Call(attack,"ReadFlight",args);
        }
        private static void Natural(object context)
        {
            foreach(int weapon in new[]{3209,4062})
            {
                var p=Reset(context,weapon);p.position=new Vector2(650,646);Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,52,40);target.position.X=p.Center.X+35;raw=target.Center-new Vector2(300,0);solves=knownCalls=depth=steps=scans=rngCalls=0;
                var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int frame=0;frame<3;frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);Require(GetOptional(attack,"ExpectedPreview")==null && !Main.projectile.Any(q=>q.active) && solves==0,"withdrawn idle spell preview neither solves nor fires");
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));var plan=PrepareNatural(context,host,attack);Require(plan!=null,"pressed spell hand has an actual preparation under1000ms component allowance");Draws(host,attack,plan);
                ammo=Static(assembly.GetType("JueMingR.TerrariaHost.Combat.AttackAmmoSnapshot",true),"Capture",p,p.HeldItem);births=true;int previews=0,contacts=0;
                for(int frame=0;frame<65;frame++)
                {
                    var next=frame==0?plan:PrepareNatural(context,host,attack);var flight=next==null?Observe(attack,p):null;var observed=next??(flight==null?null:(AttackContact)Get(flight,"Contact"));
                    if(frame>=2){Require(GetOptional(attack,"ExpectedPreview")==null,"all63 stable native spell frames keep the retired hypothetical preview absent");previews++;if(observed!=null)contacts++;}
                    if(frame==6 && observed!=null)Draws(host,attack,observed,flight);NativeCombatSelectionPreviewChecks.Step(context,raw,true);
                }
                NativeCombatSelectionPreviewChecks.Step(context,raw,false);for(int frame=0;frame<100 && Main.projectile.Any(q=>q.active && (q.type==521 || q.type==522 || q.type==731));frame++)NativeCombatSelectionPreviewChecks.Step(context,raw,false);births=false;
                int hands=born.Count(b=>b.ParentSlot<0),useful=born.Count(b=>b.First>=0);Console.WriteLine("AI29 natural before expectations weapon="+weapon+" hands="+hands+" bodies="+born.Count+" useful="+useful+" previews="+previews+" solves="+solves+" known="+knownCalls+" captures="+scans+" motor="+steps+" modelRNG="+rngCalls);
                Require(hands>=3 && useful>0 && born.Where(b=>b.ParentSlot<0).All(b=>Vector2.Dot(b.Velocity,raw-b.Origin)<0) && previews==63 && contacts>0 && rngCalls==0,"all63 original observations preserve real hand/family contacts, opposite assisted cursor and positive immortal488 Strike without model RNG");
                CompareBorn(p);Require(rngCalls==0 && scans==solves+knownCalls,"each natural preparation/recorded replay captures its finite family once and does not draw native RNG");Console.WriteLine("PASS AI29 natural weapon="+weapon+" random misses="+born.Count(b=>b.First<0)+" (actual clock oracle; no live flight control registered)");
            }
        }
    }
}
