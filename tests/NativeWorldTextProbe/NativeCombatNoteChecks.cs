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
    internal static class NativeCombatNoteChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static Assembly assembly;private static Type model;private static NPC target;private static Projectile receiving;
        private sealed class BirthFact {internal Vector2 Cursor,Center;internal AttackContact Known;internal long Sample;internal bool Updated,Hit;}
        private static readonly Dictionary<ProjectileKey,BirthFact> born=new Dictionary<ProjectileKey,BirthFact>();
        private static readonly List<int> hitSlots=new List<int>();private static bool natural;private static long first;private static int solves,steps,positive;private static Vector2 raw;
        private static object Static(Type type,string name,params object[] args){return type.GetMethod(name,Flags).Invoke(null,args);}
        private static void SolveWork(){solves++;}private static void StepWork(){steps++;}
        private static void BeforeDamage(Projectile __instance){receiving=__instance;}private static void AfterDamage(){receiving=null;}
        private static void Strike(NPC __instance,int __result)
        {
            if(receiving==null || receiving.type<76 || receiving.type>78 || __result<=0)return;hitSlots.Add(__instance.whoAmI);
            if(!ReferenceEquals(__instance,target))return;if(first<0)first=Main.GameUpdateCount;positive++;
            BirthFact fact;if(natural && born.TryGetValue(receiving.key,out fact) && !fact.Hit){fact.Hit=true;Require(fact.Updated && fact.Known!=null && Main.GameUpdateCount-fact.Sample==fact.Known.Tick,"each actual note's first positive Strike matches its captured original type, pitch and deceleration");}
        }
        private static Player Reset(object context)
        {
            natural=false;target=null;receiving=null;first=-1;positive=0;born.Clear();hitSlots.Clear();
            NativeCombatObservationChecks.Save(Get(context,"CombatObservation"),new ObservationOptions());NativeCombatCadenceChecks.Save(Get(context,"Combat"),new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),494,0,0);p.statMana=p.statManaMax=p.statManaMax2=200;Main.windPhysics=false;for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();if(Main.GameUpdateCount==0)NativeQuickItemChecks.BeginWorldStep();return p;
        }
        private static NPC Target(int slot,Vector2 center)
        {
            typeof(NPC).GetMethod("NewNPCInstanceInSlot",Flags).Invoke(null,new object[]{slot,(byte)0});var n=Main.npc[slot];n.SetDefaults(3);n.whoAmI=slot;n.active=true;n.width=18;n.height=40;n.Center=center;n.life=n.lifeMax=10000;n.defense=0;n.aiStyle=-1;n.noGravity=n.noTileCollide=true;n.velocity=n.netOffset=Vector2.Zero;n.knockBackResist=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);return n;
        }
        private static NpcTrajectory Path(NPC n)
        {
            var key=(NpcIdentity)Static(assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true),"Identity",n,1L);var state=new NpcMotionState{Identity=key,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=!n.dontTakeDamage,NoTileCollide=n.noTileCollide,ReflectsProjectiles=n.reflectsProjectiles};var points=Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray();return new NpcTrajectory(key,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static object Terrain(){var t=Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true),true);Call(t,"Reset");return t;}
        private static object Clock(NpcTrajectory path,bool current=false){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true),Flags,null,new object[]{path,Enum.Parse(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true),current?"Projectiles":"BeforeNpc"),0},null);}
        private static object Obstacles(Player p,Projectile q,NpcTrajectory path,bool current=false){return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackObstacles",true),Flags,null,new object[]{p,q,path,0,!current,false},null);}
        private static void Born(int __result)
        {if(!natural || __result<0 || __result>=Main.maxProjectiles)return;var q=Main.projectile[__result];if(q.type>=76 && q.type<=78)born[q.key]=new BirthFact{Cursor=Main.MouseWorld,Center=Main.LocalPlayer.Center};}
        private static void BeforeUpdate(Projectile __instance)
        {
            BirthFact fact;if(!natural || !born.TryGetValue(__instance.key,out fact) || fact.Updated)return;fact.Updated=true;var p=Main.LocalPlayer;var q=__instance;
            float duration=q.velocity.Length()/q.ai[2];var jitter=q.velocity*(duration/2)-(fact.Cursor-fact.Center);Console.WriteLine("NOTE birth type="+q.type+" v="+q.velocity+" pitch="+q.ai[0]+" decel="+q.ai[2]+" duration="+duration+" jitter="+jitter+" cursor="+fact.Cursor+" raw="+raw);
            Require(duration>=19.999f && duration<=60.001f && jitter.Length()<=40.01f && q.ai[0]>=-1 && q.ai[0]<=1 && q.ai[1]==1 && Vector2.Dot(q.velocity,raw-fact.Center)<0,"real harp birth consumes the assisted cursor and its legal native random disk/distance coefficients");
            var path=Path(target);fact.Sample=path.SampleTick;fact.Known=(AttackContact)Static(model,"Known",p,q,path,Clock(path,true),Terrain(),Obstacles(p,q,path,true));
        }
        internal static void Run(object context)
        {
            assembly=Get(context,"Combat").GetType().Assembly;model=assembly.GetType("JueMingR.TerrariaHost.Combat.HostNoteAttack",true);var audit=new Harmony("JueMingR.Tests.Notes");bool wind=Main.windPhysics;
            foreach(string name in new[]{"HandleSpecialEvent","HandleRunning","HandleMining"})audit.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks),"SkipAchievement"));
            audit.Patch(model.GetMethod("Solve",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNoteChecks),nameof(SolveWork)));audit.Patch(model.GetMethod("Step",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNoteChecks),nameof(StepWork)));
            audit.Patch(typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float)),postfix:new HarmonyMethod(typeof(NativeCombatNoteChecks),nameof(Born)));
            audit.Patch(typeof(Projectile).GetMethod("Update",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNoteChecks),nameof(BeforeUpdate)));audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatNoteChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatNoteChecks),nameof(AfterDamage)));audit.Patch(typeof(NPC).GetMethod("StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatNoteChecks),nameof(Strike)));
            try{string leaf=Environment.GetEnvironmentVariable("JMR_NOTE_LEAF");Require(string.IsNullOrEmpty(leaf) || leaf=="motor" || leaf=="receive" || leaf=="natural","known note leaf");if(string.IsNullOrEmpty(leaf) || leaf=="motor")Motors(context);if(string.IsNullOrEmpty(leaf) || leaf=="receive")Receives(context);if(string.IsNullOrEmpty(leaf) || leaf=="natural")Natural(context);}
            finally{natural=false;audit.UnpatchAll(audit.Id);Main.windPhysics=wind;}
        }
        private static void Motors(object context)
        {
            int actions=0;for(int scene=0;scene<12;scene++)
            {
                var p=Reset(context);int type=76+scene%3;if(scene>=3 && scene<=5)for(int y=10;y<60;y++)NativeToolsChecks.Tile(60,y,1);
                if(scene>=6 && scene<=8)for(int x=40;x<100;x++)for(int y=10;y<60;y++){Main.tile[x,y].liquid=255;Main.tile[x,y].liquidType(scene==6?0:scene==7?2:3);}
                if(scene==9){Main.tile[57,21].liquid=150;Main.tile[57,21].liquidType(3);}
                var center=scene==11?new Vector2(8,350):new Vector2(920,350);var velocity=new Vector2(scene==11?-6:8,scene==9?1:0);int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,velocity,type,0,0,p.whoAmI,scene%2==0?-1:1,1,.15f);var q=Main.projectile[slot];if(scene==10)q.timeLeft=1;var copy=(Projectile)Static(model,"Copy",q);var terrain=Terrain();
                for(int action=0;action<120 && q.active;action++){Require((bool)Static(model,"Step",copy,terrain),"note copied terrain is available");Static(model,"Finish",copy);q.Update(slot);actions++;Console.WriteLine("NOTE motor scene="+scene+" action="+action+" native="+q.position+"/"+q.velocity+" predicted="+copy.position+"/"+copy.velocity+" live="+q.active+"/"+copy.active);Require(q.active==copy.active && q.timeLeft==copy.timeLeft && q.penetrate==copy.penetrate && (!q.active || Vector2.DistanceSquared(q.position,copy.position)<.002f && Vector2.DistanceSquared(q.velocity,copy.velocity)<.00001f && q.shimmerWet==copy.shimmerWet),"actual AI21 deceleration, wall reflection, liquid branch and lifetime agree");}
            }
            Console.WriteLine("PASS NOTE motor=12 actions="+actions);
        }
        private static void Receives(object context)
        {
            for(int mode=0;mode<10;mode++)
            {
                var p=Reset(context);var center=new Vector2(940,350);target=Target(6,center+new Vector2(mode==0?22:mode==1?24:0,0));if(mode==2)target.immune[p.whoAmI]=200;if(mode==4)target.reflectsProjectiles=true;if(mode==8)target.dontTakeDamage=true;
                if(mode==5 || mode==6)for(int slot=0;slot<(mode==5?4:5);slot++)Target(slot,center);
                int at=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),center,Vector2.Zero,76,mode==9?0:50,0,p.whoAmI,0,1);var q=Main.projectile[at];if(mode==3)q.localNPCImmunity[6]=-1;if(mode==7)q.timeLeft=1;
                NativeQuickItemChecks.BeginWorldStep();var path=Path(target);var known=(AttackContact)Static(model,"Known",p,q,path,Clock(path),Terrain(),Obstacles(p,q,path));
                for(int action=0;action<40 && q.active && first<0;action++){if(action>0)NativeQuickItemChecks.BeginWorldStep();for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i].active)Main.npc[i].UpdateNPC(i);q.Update(at);}
                int actual=first<0?0:(int)(first-path.SampleTick);bool negative=mode==1 || mode==2 || mode==3 || mode==4 || mode==6 || mode==8 || mode==9;Console.WriteLine("NOTE receive mode="+mode+" actual="+actual+" known="+known?.Tick+" hits="+string.Join(",",hitSlots));Require((actual==0)==negative && (known==null)==negative && (known==null || known.Tick==actual),"native enlarged note collider, strict edge, local/owner/reflection, ordered penetration and damage expiry agree");
            }
            Console.WriteLine("PASS NOTE receive=10");
        }
        private static void Natural(object context)
        {
            var p=Reset(context);p.position=new Vector2(650,646);p.releaseUseItem=true;Main.screenPosition=new Vector2(500,400);for(int x=5;x<115;x++)NativeToolsChecks.Tile(x,43,1);target=NativeCombatControlPreviewChecks.Dummy(2,48,40);raw=target.Center-new Vector2(490,0);var host=Get(context,"CombatObservation");var attack=Get(Get(context,"Combat"),"Attack");solves=steps=0;
            NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,false));for(int i=0;i<3;i++){NativeCombatSelectionPreviewChecks.Step(context,raw,false,true);Require(GetOptional(attack,"ExpectedPreview")!=null && !Main.projectile.Any(q=>q.active),"idle harp preview is independent of attack authority");}
            natural=true;NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,true,false,true,25,true,true));for(int i=0;i<36;i++)
            {
                NativeCombatSelectionPreviewChecks.Step(context,raw,true);var plan=(AttackContact)GetOptional(attack,"ExpectedPreview");Require(plan!=null,"feasible sustained harp cadence retains a useful next-use preview");int a=solves,b=steps;var marker=Get(Get(host,"World"),"Impact");var zoom=Main.GameViewMatrix.ZoomMatrix;for(int j=0;j<20;j++){Call(marker,"Capture");Call(marker,"Project",zoom,Matrix.Invert(zoom));Require((bool)Call(attack,"PreviewCurrent",plan),"CPU harp presentation remains tied to current preparation");}Require(a==solves && b==steps,"same-update CPU presentation performs no note Solve or Step");
            }
            Require(born.Count>=3 && positive>0 && born.Values.Any(b=>b.Hit),"real native harp uses emit original random note types and deal positive damage at their captured known times");
            foreach(var fact in born.Values)if(fact.Known!=null && Main.GameUpdateCount-fact.Sample>=fact.Known.Tick)Require(fact.Hit,"every captured note whose predicted first-contact time elapsed produced its real positive Strike");
            Console.WriteLine("PASS NOTE natural births="+born.Count+" positive="+positive+" knownHits="+born.Values.Count(b=>b.Hit)+" preview=36/36 mana="+p.statMana+"/"+p.statManaMax2+" solves="+solves+" steps="+steps);natural=false;NativeCombatSelectionPreviewChecks.Step(context,raw,false);
        }
    }
}
