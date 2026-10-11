using System;
using System.Collections;
using System.Collections.Generic;
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
    internal static class NativeCombatCelebrationChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        private static NPC watched;private static int hits,damageDepth,stageFilter=-1;private static Projectile damaging;private static bool above,below;private static long firstHit;
        private static readonly HashSet<int> bornStages=new HashSet<int>();
        private static int helixDepth,helixSeeds,helixSteps;
        private static void BeforeHelix(){helixDepth++;helixSeeds++;}
        private static void AfterHelix(){helixDepth--;}
        private static void MotorWork(){if(helixDepth>0)helixSteps++;}
        private static void Strike(NPC __instance,int __result){if(ReferenceEquals(__instance,watched) && __result>0 && (stageFilter<0 || damageDepth>0 && damaging!=null && damaging.ai[0]==stageFilter)){hits++;if(firstHit<0)firstHit=Main.GameUpdateCount;}}
        private static void BeforeDamage(Projectile __instance)
        {
            if(__instance.aiStyle!=147)return;damageDepth++;damaging=__instance;
            if(watched==null || __instance.ai[0]!=4 || __instance.width!=14)return;var b=__instance.Hitbox;var n=watched.Hitbox;
            if(b.Right<=n.Left || b.Left>=n.Right)return;
            above|=b.Bottom<=n.Top;below|=b.Top>=n.Bottom;
        }
        private static void AfterDamage(Projectile __instance){if(__instance.aiStyle==147)damageDepth--;}
        private static void Born(int __result)
        {if(__result>=0 && __result<Main.maxProjectiles){var q=Main.projectile[__result];if(q.owner==0 && q.aiStyle==147)bornStages.Add((int)q.ai[0]);}}
        internal static void Run(object context)
        {
            string leaf=Environment.GetEnvironmentVariable("JMR_CELEBRATION_ORACLE_ONLY");
            if(leaf=="helix"){Helix(context);return;}
            Motor(context);Cycle(context);Helix(context);
        }
        private static NpcTrajectory Path(Assembly a,NPC n)
        {
            var id=(NpcIdentity)a.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",Flags).Invoke(null,new object[]{n,1L});
            var state=new NpcMotionState{Identity=id,X=n.position.X,Y=n.position.Y,Width=n.width,Height=n.height,CanReceive=true,NoTileCollide=n.noTileCollide};
            var points=Enumerable.Range(0,121).Select(step=>new NpcTrajectoryPoint(step,state)).ToArray();return new NpcTrajectory(id,(long)Main.GameUpdateCount,1,PredictionAssumption.NoNewHits,PredictionStop.None,points,points.Length,PredictionStrategy.RollingConditional);
        }
        private static void Helix(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var a=combat.GetType().Assembly;var model=a.GetType("JueMingR.TerrariaHost.Combat.HostCelebrationAttack",true);
            var clockType=a.GetType("JueMingR.TerrariaHost.Combat.HostAttackClock",true);var phaseType=a.GetType("JueMingR.TerrariaHost.Combat.HostAttackPhase",true);var terrainType=a.GetType("JueMingR.TerrariaHost.Combat.PredictionTerrain",true);
            var audit=new Harmony("JueMingR.Tests.CelebrationHelix");
            // Observe work in either build configuration; production does not
            // expose test counters or acquire a release-only probe dependency.
            audit.Patch(model.GetMethod("HelixDirection",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCelebrationChecks),nameof(BeforeHelix)),postfix:new HarmonyMethod(typeof(NativeCombatCelebrationChecks),nameof(AfterHelix)));
            audit.Patch(model.GetMethod("Advance",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCelebrationChecks),nameof(MotorWork)));
            audit.Patch(typeof(Projectile).GetMethod("Damage",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCelebrationChecks),nameof(BeforeDamage)),postfix:new HarmonyMethod(typeof(NativeCombatCelebrationChecks),nameof(AfterDamage)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatCelebrationChecks),nameof(Strike)));
            bool wind=Main.windPhysics;int positive=0,negative=0;
            try
            {
                foreach(bool assisted in new[]{false,true})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
                    var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3930,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.inventory[54].SetDefaults(771);p.inventory[54].stack=999;Main.windPhysics=false;
                    watched=NativeCombatControlPreviewChecks.Dummy(2,52,40);watched.immune[p.whoAmI]=7;
                    // Keep the player grounded, but remove only the forward
                    // floor from this shape oracle. Both native siblings can
                    // then cross the target's X interval above/below its real
                    // body without a ground Kill obscuring that motor fact.
                    for(int x=45;x<115;x++)Main.tile[x,43].ClearEverything();
                    Vector2 origin=p.RotatedRelativePoint(p.MountedCenter);
                    int parentSlot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),origin,Vector2.UnitX*17,714,40,0,p.whoAmI,29,8);var parent=Main.projectile[parentSlot];
                    var path=Path(a,watched);var terrain=Activator.CreateInstance(terrainType,true);Call(terrain,"Reset");var clock=Activator.CreateInstance(clockType,Flags,null,new object[]{path,Enum.Parse(phaseType,"CompletedWorld")},null);
                    helixDepth=helixSeeds=helixSteps=0;object[] args={p,parent,path,clock,terrain,Vector2.Zero,null,null};var contact=(AttackContact)model.GetMethod("Solve",Flags).Invoke(null,args);var aim=(Vector2)args[5];var ammo=args[6];float speed=(float)Get(ammo,"Speed");
                    Require(contact!=null && contact.Confidence==AttackConfidence.Representative,"captured parent29/8 has a feasible native stage4 contact; random controller spread remains representative");
                    int seeds=helixSeeds,steps=helixSteps;
                    Require(seeds>0 && seeds<=6 && steps<=seeds*3*360,"stage4 direction inverse has bounded native-motor work and still validates full candidates");
                    Vector2 delta=watched.Center-origin;int travel=(int)Math.Ceiling(delta.Length()/speed);float drift=0,vy=0;
                    for(int step=1;step<=travel;step++){if(step>=10)vy+=.04f;drift+=vy;}
                    Vector2 legacy=new Vector2(delta.X,delta.Y-drift).SafeNormalize(Vector2.UnitX),direction=assisted?(aim-origin).SafeNormalize(Vector2.UnitX):legacy;
                    Require(Math.Abs(MathHelper.WrapAngle((aim-origin).ToRotation()-legacy.ToRotation()))>.08f,"the useful seed changes the native helix direction rather than repeating gravity-only aim");
                    hits=damageDepth=0;firstHit=-1;above=below=false;stageFilter=4;long start=Main.GameUpdateCount;
                    for(int outer=1;outer<=20 && hits==0;outer++)
                    {
                        NativeQuickItemChecks.BeginWorldStep();if(watched.immune[p.whoAmI]>0)watched.immune[p.whoAmI]--;
                        // The captured controller's next native firing window
                        // is outer8. These are zero-spread native phase4 births,
                        // not claims that future random shots are exact.
                        if(outer==8)for(int sibling=0;sibling<2;sibling++)Projectile.NewProjectile(parent.GetProjectileSource_FromThis(),origin,direction*speed,(int)Get(ammo,"Projectile"),40,0,p.whoAmI,4,sibling);
                        for(int slot=0;slot<Main.maxProjectiles;slot++){var shot=Main.projectile[slot];if(shot.active && shot.aiStyle==147){Main.ProjectileUpdateLoopIndex=slot;shot.Update(slot);}}Main.ProjectileUpdateLoopIndex=-1;
                    }
                    if(assisted){Require(hits>0 && firstHit==path.SampleTick+contact.Tick,"inverted helix receives actual native Damage at the validated zero-spread contact: predicted="+(path.SampleTick+contact.Tick)+" native="+firstHit);positive++;}
                    else{Require(hits==0 && above && below,"the former gravity-only ray really misses above and below the NPC body in native stage4 Update/Damage");negative++;}
                    Console.WriteLine("CELEBRATION helix assisted="+assisted+" nativeFirst="+(firstHit<0?-1:firstHit-start)+" plan="+contact.Tick+" above="+above+" below="+below+" seeds="+seeds+" seedSteps="+steps+" aim="+aim);
                }
                Require(positive==1 && negative==1,"native stage4 helix positive and original-direction negative both execute");Console.WriteLine("PASS Celebration stage4 native helix contact/above-below miss and finite inverse workload");
            }
            finally{watched=null;damaging=null;stageFilter=-1;damageDepth=helixDepth=0;Main.windPhysics=wind;Main.ProjectileUpdateLoopIndex=-1;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());}
        }
        private static void Motor(object context)
        {
            var combat=Get(context,"Combat");var input=Get(context,"Input");var model=combat.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostCelebrationAttack",true);
            var advance=model.GetMethod("Advance",Flags);var targets=Activator.CreateInstance(typeof(List<>).MakeGenericType(model.GetNestedType("Target",Flags)));int cases=0,steps=0;
            foreach(int type in new[]{715,717})foreach(int stage in Enumerable.Range(0,7))foreach(int direction in new[]{-1,1})foreach(int sibling in stage==4?new[]{0,1}:new[]{0})
            {
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3930,0,0);p.position=new Vector2(700,646);p.ResetEffects();
                for(int x=5;x<115;x++)Main.tile[x,43].ClearEverything();foreach(var n in Main.npc)n.active=false;
                int slot=Projectile.NewProjectile(p.GetProjectileSource_Item(p.HeldItem),new Vector2(900,500),new Vector2(direction*8,0),type,20,0,0,stage,sibling);var original=Main.projectile[slot];
                var shadow=new Projectile();shadow.SetDefaults(type);shadow.owner=0;shadow.Center=original.Center;shadow.velocity=original.velocity;shadow.ai=(float[])original.ai.Clone();shadow.localAI=(float[])original.localAI.Clone();
                for(int tick=0;tick<26;tick++)
                {
                    for(int sub=0;sub<shadow.MaxUpdates;sub++){advance.Invoke(null,new object[]{shadow,null,0,null,targets});shadow.position+=shadow.velocity;steps++;}
                    original.Update(slot);
                    Require(original.active && Vector2.DistanceSquared(original.Center,shadow.Center)<.001f && Vector2.DistanceSquared(original.velocity,shadow.velocity)<.001f && original.maxPenetrate==shadow.maxPenetrate && original.localAI[0]==shadow.localAI[0] && Math.Abs(original.scale-shadow.scale)<.0001f,"all seven native Celebration motors, gravity boundaries and twin helix agree: "+type+"/"+stage+"/"+direction+"/"+sibling+"/"+tick+" original="+original.Center+" shadow="+shadow.Center);
                }
                cases++;
            }
            Console.WriteLine("PASS Celebration native full projectile-update motors: cases="+cases+" subupdates="+steps+"; no autonomous targets in this scalar motor oracle");
        }
        private static void Cycle(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());NativeCombatCadenceChecks.Save(combat,new CombatOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3930,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.releaseUseItem=true;p.inventory[54].SetDefaults(771);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,400);
            watched=NativeCombatControlPreviewChecks.Dummy(2,55,40);hits=0;bornStages.Clear();
            var audit=new Harmony("JueMingR.Tests.CelebrationCycle");var birth=typeof(Projectile).GetMethods(Flags).Single(m=>m.Name=="NewProjectile" && m.GetParameters().Length==13 && m.GetParameters()[1].ParameterType==typeof(float));
            audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatCelebrationChecks).GetMethod(nameof(Born),Flags)));
            audit.Patch(AccessTools.Method(typeof(NPC),"StrikeNPC",new[]{typeof(int),typeof(float),typeof(int),typeof(bool),typeof(bool),typeof(int)}),postfix:new HarmonyMethod(typeof(NativeCombatCelebrationChecks).GetMethod(nameof(Strike),Flags)));
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,25,true,true));int visible=0;
                for(int tick=0;tick<80;tick++){NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true);if(GetOptional(attack,"ExpectedPreview")!=null)visible++;}
                Require(bornStages.SetEquals(Enumerable.Range(0,7)) && hits>0 && visible>0,"actual held ItemCheck/controller cycle creates every native stage, hits the real dummy and keeps useful preview");
                Console.WriteLine("PASS Celebration natural complete cycle: stages="+string.Join(",",bornStages.OrderBy(v=>v))+" actualDamage="+hits+" visible="+visible+"/80; native random spread remains Representative");
            }
            finally{watched=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
    }
}
