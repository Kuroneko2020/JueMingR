using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatPhaseChecks
    {
        private static Projectile born;
        private static bool captureBirth;
        internal static void Run(object context)
        {
            // Same CPU achievement outlet as the existing real Player.Update
            // checks. No player/NPC/projectile phase is replaced.
            var audit=new Harmony("JueMingR.Tests.AttackPhases");var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
            var methods=new[]{"HandleSpecialEvent","HandleRunning","HandleMining"}.Select(name=>typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,flags)).ToArray();
            foreach(var method in methods)audit.Patch(method,prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",flags)));
            var birth=typeof(Player).GetMethod("TryUpdateChannel",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);audit.Patch(birth,postfix:new HarmonyMethod(typeof(NativeCombatPhaseChecks).GetMethod(nameof(Birth),flags)));
            var randomField=typeof(Main).GetField("_rngs",flags);object previousRandom=randomField.GetValue(null);
            // The original phase owns this named RNG stream after Main's
            // normal initialization; preserve the calling probe's old stream.
            randomField.SetValue(null,new System.Collections.Generic.Dictionary<string,Terraria.Utilities.UnifiedRandom>{{"UpdateProjectiles",new Terraria.Utilities.UnifiedRandom(702)}});
            try{RunCore(context);Bubble(context,0,true,true);Bubble(context,0);Bubble(context,5);Bubble(context,0,true);UnknownSlot(context);NativeCombatSwingChecks.Run(context);NativeCombatWhipChecks.Run(context);}finally{randomField.SetValue(null,previousRandom);captureBirth=false;audit.Unpatch(birth,HarmonyPatchType.All,audit.Id);foreach(var method in methods)audit.Unpatch(method,HarmonyPatchType.All,audit.Id);}
        }
        private static void RunCore(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,113,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,500);
            Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(810,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);n.UpdateNPC(2);
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));Call(context,"UpdateRuntime");
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});Call(context,"UpdateRuntime");
            var q=Main.projectile.Single(s=>s.active && s.type==16);var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");Require(contact!=null,"world-completed guided preparation has a genuine finite contact");long expected=contact.Timeline.SampleTick+contact.Tick,first=-1;int life=n.life;
            for(int k=0;k<20 && first<0;k++)
            {
                NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");p.Update(0);n.UpdateNPC(2);
                Main.ProjectileUpdateLoopIndex=q.whoAmI;try{q.Update(q.whoAmI);}finally{Main.ProjectileUpdateLoopIndex=-1;}
                if(n.life<life)first=Main.GameUpdateCount;Call(context,"UpdateRuntime");
            }
            Console.WriteLine("PHASE completed guided: sample="+contact.Timeline.SampleTick+" contact="+contact.Tick+" expected="+expected+" actual="+first+" target="+n.position);
            Require(first==expected,"world-completed future and actual first Damage share the absolute NPC/Projectile tick");
            Console.WriteLine("PASS natural component Player/NPC/Projectile order and completed-world contact");
        }
        private static void Birth(Projectile __0){if(captureBirth && __0.type==14)born=__0;}
        private static void Bubble(object context,int slot,bool stationary=false,bool shortFuture=false)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,2797,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.inventory[54].SetDefaults(97);p.inventory[54].stack=999;Main.screenPosition=new Vector2(600,500);Main.rand=new Terraria.Utilities.UnifiedRandom(7123);
            for(int i=0;i<slot;i++)Require(Projectile.NewProjectile(new EntitySource_DebugCommand(),new Vector2(300,300),Vector2.Zero,1,0,0,0)==i,"native filler preserves default sequential slots");
            var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(1100,646);n.life=n.lifeMax=10000;n.target=0;Array.Clear(n.immune,0,n.immune.Length);
            // SetDefaults preserves buff timers and shimmer transparency on a
            // reused NPC. Independent dry scenes must not inherit the earlier
            // real liquid scene; the short-future row deliberately retains a
            // known native transformation boundary instead.
            Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);n.shimmerTransparency=0;n.lifeRegen=n.lifeRegenCount=0;
            if(shortFuture){n.buffType[0]=353;n.buffTime[0]=18;n.shimmerTransparency=.82f;}
            n.UpdateNPC(2);
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));Call(context,"UpdateRuntime");
            typeof(Player).GetMethod("ItemCheck_Shoot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});var bubble=Main.projectile[slot];Require(bubble.type==444 && bubble.active,"native Xeno parent in requested slot");for(int i=0;i<slot;i++)Main.projectile[i].Kill();bubble.timeLeft=2;
            Vector2 initial=bubble.Center,velocity=bubble.velocity;double difference=bubble.ai[0].ToRotationVector2().ToRotation()-velocity.ToRotation();if(difference>Math.PI)difference-=Math.PI*2;if(difference<-Math.PI)difference+=Math.PI*2;Require(Math.Abs(difference)>.001 && velocity!=Vector2.Zero,"actual Shot retains nonzero random bubble angle for steering counterexample");
            if(stationary)bubble.velocity=Vector2.Zero;
            NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");p.channel=p.controlUseItem=false;Call(context,"UpdateRuntime");var contact=(AttackContact)GetOptional(attack,"ExpectedImpact");
            if(shortFuture)
            {
                var timeline=(JueMingR.Platform.Combat.NpcTrajectory)Get(Get(attack,"Control"),"preparedTimeline");
                Console.WriteLine("PHASE short native future: samples="+timeline.Count+" stop="+timeline.Stop+" shimmer="+n.shimmerTransparency+" buff="+n.buffTime[0]+" contact="+contact?.Tick);
                Require(timeline.Stop==JueMingR.Platform.Combat.PredictionStop.PhaseBoundary && timeline.Count>1 && timeline.Count<12 && contact==null,"real shared future stops before native shimmer transformation and cannot invent a later child contact");
                return;
            }
            Require(contact!=null,"natural expiry has a finite prepared child contact");long expected=contact.Timeline.SampleTick+contact.Tick,first=-1;int life=n.life;born=null;captureBirth=true;
            for(int k=0;k<40 && first<0;k++)
            {
                NativeQuickItemChecks.BeginWorldStep();NativeToolExecutionChecks.Sample(context,input,new Vector2(650,550),false);Call(combat,"Sample");p.Update(0);n.UpdateNPC(2);ProjectilePhase();
                if(k==0)
                {Require(born!=null && born.active && born.velocity.X>0,"natural expiry Kill borrows future after actual bubble steering: slot="+slot+" initial="+initial+" velocity="+velocity+" now="+bubble.Center+" child="+born?.velocity);Require(slot==0?born.whoAmI>slot && born.timeLeft<600:born.whoAmI<slot && born.timeLeft==600,"later child runs in birth phase; earlier child waits for next phase");Console.WriteLine("PHASE bubble birth: parentSlot="+slot+" childSlot="+born.whoAmI+" childKey="+born.key+" birthPhase="+Main.GameUpdateCount+" timeLeft="+born.timeLeft+" childVelocity="+born.velocity);}
                if(n.life<life)first=Main.GameUpdateCount;Call(context,"UpdateRuntime");
            }
            captureBirth=false;Console.WriteLine("PHASE bubble contact: slot="+slot+" stationary="+stationary+" sample="+contact.Timeline.SampleTick+" tick="+contact.Tick+" expected="+expected+" actual="+first);Require(first==expected,"same shared timeline predicts real high/low child first Damage tick");
        }
        private static void UnknownSlot(object context)
        {
            var control=Get(Get(Get(context,"Combat"),"Attack"),"Control");var query=control.GetType().GetMethod("TryChildLaterSlot",BindingFlags.Static|BindingFlags.NonPublic);
            bool random=Terraria.Testing.DebugOptions.Shared_RandomizeProjectileSlots;var active=Main.projectile.Select(q=>q.active).ToArray();
            try
            {
                foreach(var q in Main.projectile)q.active=true;object[] args={Main.projectile[0],false};Require(!(bool)query.Invoke(null,args),"full pool replacement is unknown, not an early child receipt");
                Main.projectile[4].active=false;Terraria.Testing.DebugOptions.Shared_RandomizeProjectileSlots=true;Require(!(bool)query.Invoke(null,args),"random slot mode cannot promise a birth phase");
                Terraria.Testing.DebugOptions.Shared_RandomizeProjectileSlots=false;Require((bool)query.Invoke(null,args) && (bool)args[1],"known late free slot remains supported");
            }
            finally{Terraria.Testing.DebugOptions.Shared_RandomizeProjectileSlots=random;for(int i=0;i<active.Length;i++)Main.projectile[i].active=active[i];}
            Console.WriteLine("PASS full/random slot unknown and known deterministic phase boundary");
        }
        private static void ProjectilePhase()
        {
            var receiver=Main.instance;var oldSpelunker=receiver.SpelunkerProjectileHelper;var oldChum=receiver.ChumBucketProjectileHelper;
            receiver.SpelunkerProjectileHelper=new Terraria.GameContent.SpelunkerProjectileHelper();receiver.ChumBucketProjectileHelper=new Terraria.GameContent.ChumBucketProjectileHelper();
            try{using(Main.SwapRandom("UpdateProjectiles"))typeof(Main).GetMethod("UpdateWorld_Projectiles",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(receiver,null);}
            finally{receiver.SpelunkerProjectileHelper=oldSpelunker;receiver.ChumBucketProjectileHelper=oldChum;}
        }
    }
}
