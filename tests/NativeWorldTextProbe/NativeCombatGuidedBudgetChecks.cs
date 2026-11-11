using System;
using System.Collections;
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
    internal static class NativeCombatGuidedBudgetChecks
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        private static object watched;
        private static bool watchOpening;
        private static int interruptions;
        private static Projectile openingSample;
        private static void Opened(Projectile __result){if(watchOpening)openingSample=__result;}
        private static void Exhaust(object __instance)
        {
            if(!watchOpening && !ReferenceEquals(watched,__instance))return;interruptions++;
            // Deterministic budget fault at an actual completed-work boundary,
            // not a wall-clock sleep or a larger production allowance.
            var work=typeof(CombatPredictionWork).GetField("active",Flags).GetValue(null);
            Require(work!=null,"fault injection has the real enclosing cooperative owner");
            typeof(CombatPredictionWork).GetField("exceeded",Flags).SetValue(work,true);CombatPredictionWork.Check();
        }
        internal static void Run(object context)
        {
            Opening(context);
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,113,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.releaseUseItem=true;Main.screenPosition=new Vector2(600,400);
            var n=NativeCombatControlPreviewChecks.Dummy(2,56,40);n.immune[0]=200;
            NativeToolExecutionChecks.Sample(context,input,new Vector2(600,500),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,true,50,false,true));
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,true);
            typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});
            var shot=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==16);
            NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,true);
            var control=Get(attack,"Control");var entry=((IDictionary)Get(control,"shots"))[(int)shot.key];var nav=Get(entry,"Guided");
            var timeline=(NpcTrajectory)Get(control,"preparedTimeline");var phase=Get(control,"preparedPhase");
            foreach(bool prefix in new[]{true,false})
            {
                watched=nav;interruptions=0;var audit=new Harmony("JueMingR.Tests.GuidedBudget."+prefix);
                var method=nav.GetType().GetMethod(prefix?"CapturePrefix":"Replay",Flags);var hook=new HarmonyMethod(typeof(NativeCombatGuidedBudgetChecks).GetMethod(nameof(Exhaust),Flags));
                if(prefix)audit.Patch(method,postfix:hook);else audit.Patch(method,prefix:hook);
                var owner=new CombatPredictionWork(1000);
                try{using(owner.Enter(Main.GameUpdateCount))Call(control,"Prepare",timeline,phase);}
                finally{watched=null;audit.UnpatchAll(audit.Id);}
                Require(interruptions==1 && owner.Exceeded && (bool)Get(nav,"BudgetPaused"),"deterministic guided budget interruption occurred at the requested boundary");
                Require((bool)Get(entry,"HasPoint")==prefix && GetOptional(entry,"Contact")==null,"only a completed prefix survives budget exhaustion, never a Contact");
                Require((uint)Get(entry,"Prepared")==Main.GameUpdateCount && ((NpcIdentity)Get(entry,"Target")).Equals(timeline.Identity),"normal controller publication stamps the recovered prefix's current frame and target");
                int searches=(int)Get(nav,"Searches"),replays=(int)Get(nav,"Replays");
                using(owner.Enter(Main.GameUpdateCount))for(int i=0;i<4;i++)
                {
                    var scope=Call(control,"BeginAI",shot);if(scope!=null)Call(scope,"End");
                    var receipt=Get(attack,"consumedInput");
                    Require(Convert.ToString(Get(receipt,"Kind"))==(prefix?"Enhanced":"Basic"),"actual AI uses the completed prefix or current Basic fallback under the same exhausted owner");
                }
                Require((int)Get(nav,"Searches")==searches && (int)Get(nav,"Replays")==replays,"AI consumes the recovered prefix without new future or route work");
                Console.WriteLine("PASS guided deterministic tail budget prefix="+prefix+" searches="+searches+" replays="+replays);
            }
            // Independent preview state must perform its own preparation and
            // retain immutable dependencies without editing the real route.
            n.immune[0]=0;NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,true);
            timeline=(NpcTrajectory)Get(control,"preparedTimeline");phase=Get(control,"preparedPhase");
            var clone=(NpcTrajectory)typeof(object).GetMethod("MemberwiseClone",Flags).Invoke(timeline,null);
            int actualSearches=(int)Get(nav,"Searches"),actualReplays=(int)Get(nav,"Replays");var actualPoint=(Vector2)Get(entry,"Point");var actualContact=GetOptional(entry,"Contact");
            using(new CombatPredictionWork(1000).Enter(Main.GameUpdateCount))Call(control,"Preview",clone,phase,0,true);
            var preview=Get(entry,"PreviewGuided");
            Require(preview!=null && !ReferenceEquals(preview,nav) && (int)Get(preview,"Replays")>0,"guided preview uses its independent navigator");
            Require((int)Get(nav,"Searches")==actualSearches && (int)Get(nav,"Replays")==actualReplays && (Vector2)Get(entry,"Point")==actualPoint && ReferenceEquals(actualContact,GetOptional(entry,"Contact")),"candidate preview leaves the real prepared point, Contact and navigator work unchanged");
            NativeCombatObservationChecks.Save(host,new ObservationOptions());
            int offSearches=(int)Get(nav,"Searches"),offReplays=(int)Get(nav,"Replays");
            for(int i=0;i<3;i++)NativeCombatSelectionPreviewChecks.Step(context,new Vector2(600,500),true,true);
            Require((int)Get(nav,"Searches")==offSearches && (int)Get(nav,"Replays")==offReplays,"Aim/Path off creates no new guided route or future work");
        }
        private static void Opening(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var input=Get(context,"Input");var attack=Get(combat,"Attack");var assembly=attack.GetType().Assembly;
            var navigator=assembly.GetType("JueMingR.TerrariaHost.Combat.HostGuidedNavigation",true);
            foreach(int weapon in new[]{113,3030})foreach(bool prefix in new[]{true,false})
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());
                var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,weapon,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;p.releaseUseItem=true;p.statMana=p.statManaMax=p.statManaMax2=1000;Main.screenPosition=new Vector2(600,400);
                var n=NativeCombatControlPreviewChecks.Dummy(2,56,40);n.immune[0]=200;
                NativeToolExecutionChecks.Sample(context,input,n.Center,true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,false,false,false,true,50,false,true));
                NativeCombatAimChecks.PrepareMechanismFixture(host,attack,n,0,phaseName:"BeforeNpc");
                var control=Get(attack,"Control");var timeline=(NpcTrajectory)Get(control,"preparedTimeline");var phase=Get(control,"preparedPhase");
                var audit=new Harmony("JueMingR.Tests.GuidedOpeningBudget."+weapon+"."+prefix);var hook=new HarmonyMethod(typeof(NativeCombatGuidedBudgetChecks).GetMethod(nameof(Exhaust),Flags));
                var method=navigator.GetMethod(prefix?"CapturePrefix":"Replay",Flags);if(prefix)audit.Patch(method,postfix:hook);else audit.Patch(method,prefix:hook);
                audit.Patch(navigator.GetMethod("OpeningShot",Flags),postfix:new HarmonyMethod(typeof(NativeCombatGuidedBudgetChecks).GetMethod(nameof(Opened),Flags)));
                watchOpening=true;interruptions=0;openingSample=null;var owner=new CombatPredictionWork(1000);
                try{using(owner.Enter(Main.GameUpdateCount))Call(control,"Prepare",timeline,phase);}
                finally{watchOpening=false;audit.UnpatchAll(audit.Id);}
                var nav=Get(control,"openingGuided");var sample=openingSample;
                Require(sample!=null && interruptions==1 && owner.Exceeded && (bool)Get(control,"openingUsable")==prefix && (bool)Get(nav,"BudgetPaused"),"one opening solve retains only an actually verified birth-compatible prefix: weapon="+weapon+" prefix="+prefix);
                int replays=(int)Get(nav,"Replays");Require(replays==(prefix?1:0),"opening does not discard its result through a second Prepare after budget exhaustion");
                using(owner.Enter(Main.GameUpdateCount))
                {
                    var scope=Call(control,"BeginOpening",p,p.HeldItem);Require(scope!=null,"an exhausted opening keeps either enhanced input or original Basic");
                    Call(scope,"End");var receipt=Get(attack,"consumedInput");
                    Require(Convert.ToString(Get(receipt,"Kind"))==(prefix?"Enhanced":"Basic"),"real opening consumer retains the verified prefix under its exhausted work owner");
                }
                // The ordinary native Shoot remains the independent birth
                // oracle. No projectile motion is written to match the model.
                typeof(Player).GetMethod("ItemCheck_Shoot",Flags).Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),false});
                var actual=Main.projectile.Single(q=>q.active && q.owner==0 && q.type==p.HeldItem.shoot);
                if(prefix)Require(Vector2.DistanceSquared(actual.Center,sample.Center)<.0001f && Vector2.DistanceSquared(actual.velocity,sample.velocity)<.0001f,"surviving prefix was replayed from the actual original Shoot position and velocity: weapon="+weapon);
                Require(actual.active,"missing opening prefix does not inhibit original native emission");
                Console.WriteLine("PASS guided opening budget weapon="+weapon+" prefix="+prefix+" replays="+replays+" nativeBirth="+actual.Center+"/"+actual.velocity);
            }
        }
    }
}
