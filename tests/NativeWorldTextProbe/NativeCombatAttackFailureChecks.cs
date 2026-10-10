using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCombatAttackFailureChecks
    {
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static bool optionalFault,nativeFault,achievementFault;private static int attempts;
        private static void BornFault(){if(optionalFault){attempts++;throw new InvalidOperationException("controlled optional birth preparation fault");}}
        private static void NativeFault(){if(nativeFault)throw new InvalidOperationException("controlled original Shoot exception");}
        private static void FaultTrace(object sender,System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {if(e.Exception is NullReferenceException || e.Exception.Message=="controlled optional birth preparation fault"){achievementFault|=e.Exception is NullReferenceException && (e.Exception.StackTrace??"").Contains("AchievementsHelper.HandleSpecialEvent");Console.WriteLine("BIRTH first-chance: "+e.Exception);}}
        internal static void Run(object context)
        {
            var combat=Get(context,"Combat");var host=Get(context,"CombatObservation");var attack=Get(combat,"Attack");var input=Get(context,"Input");var control=Get(attack,"Control");var harmony=new Harmony("JueMingR.Tests.AttackBirthFailure");
            var shoot=typeof(Player).GetMethod("ItemCheck_Shoot",Flags);var prepare=attack.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.HostYoyoNavigation",true).GetMethod("Prepare",Flags);
            harmony.Patch(prepare,prefix:new HarmonyMethod(typeof(NativeCombatAttackFailureChecks).GetMethod("BornFault",Flags)));
            AppDomain.CurrentDomain.FirstChanceException+=FaultTrace;
            try
            {
                NativeCombatObservationChecks.Save(host,new ObservationOptions());var p=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),input,3278,0,0);p.position=new Vector2(700,646);p.ResetEffects();p.channel=p.controlUseItem=true;Main.screenPosition=new Vector2(600,400);
                var n=Main.npc[2];n.SetDefaults(3);n.whoAmI=2;n.active=true;n.position=new Vector2(800,646);n.velocity=n.netOffset=Vector2.Zero;n.aiStyle=-1;n.noGravity=true;n.life=n.lifeMax=10000;n.shimmerTransparency=0;Array.Clear(n.immune,0,n.immune.Length);Array.Clear(n.buffType,0,n.buffType.Length);Array.Clear(n.buffTime,0,n.buffTime.Length);
                NativeToolExecutionChecks.Sample(context,input,new Vector2(650,450),true);Call(combat,"Sample");NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,false,25,false,true));NativeCombatAimChecks.Prepare(host,attack,n,0,true);
                var timeline=Get(Get(host,"Prediction"),"Cache");var healthy=Call(timeline,"Read",0);int x=Main.mouseX,y=Main.mouseY;attempts=0;achievementFault=false;bool nativeFixtureFault=false;
                try{shoot.Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});}catch(TargetInvocationException e){if(!(e.InnerException is NullReferenceException))throw;nativeFixtureFault=true;}
                Require((!nativeFixtureFault || achievementFault) && attempts==0 && GetOptional(control,"owner")==null,"uninjected original caller either succeeds or exposes only the identified CPU achievement outlet, with outer ownership returned");
                if(nativeFixtureFault)
                {
                    harmony.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("HandleSpecialEvent",Flags),prefix:new HarmonyMethod(typeof(NativeCombatCadenceChecks).GetMethod("SkipAchievement",Flags)));
                    Console.WriteLine("BIRTH CPU isolation: original HandleSpecialEvent only; no Main/gameplay achievements host, natural Shoot/birth retained; removed in finally with this probe Harmony ID.");
                }
                foreach(var q in Main.projectile)q.active=false;Call(attack,"Reset");NativeCombatAimChecks.Prepare(host,attack,n,0,true);healthy=Call(timeline,"Read",0);optionalFault=true;
                shoot.Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});var ball=Main.projectile.Single(q=>q.active && q.type==541);optionalFault=false;
                Require(attempts==1 && (bool)Get(attack,"Failed") && ball.active && Main.mouseX==x && Main.mouseY==y && GetOptional(control,"owner")==null,"real Shoot survives optional born preparation failure and returns all cursor/source ownership");
                Require(ReferenceEquals(healthy,Call(timeline,"Read",0)) && (bool)Get(host,"Path") && !(bool)Get(Get(host,"World"),"Failed"),"local born failure preserves shared prediction/path/world health");
                for(int i=0;i<3;i++)Call(attack,"PrepareProjectiles");Require(attempts==1 && GetOptional(attack,"ExpectedImpact")==null,"failed optional born preparation is latched rather than retried every callback");
                Call(host,"Set",6,true);NativeCombatAimChecks.Prepare(host,attack,n,0,true);shoot.Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});Require(!(bool)Get(attack,"Failed") && ball.active && GetOptional(attack,"ExpectedImpact")!=null,"explicit feature retry recovers a new real source attack, preserving the former native ball");
                // An exception from the actual native caller remains visible;
                // optional recovery does not turn Shoot into a success stub.
                harmony.Patch(shoot,prefix:new HarmonyMethod(typeof(NativeCombatAttackFailureChecks).GetMethod("NativeFault",Flags)){priority=Priority.Last});nativeFault=true;bool propagated=false;
                try{shoot.Invoke(p,new object[]{0,p.HeldItem,p.GetWeaponDamage(p.HeldItem),true});}catch(TargetInvocationException e){propagated=e.InnerException is InvalidOperationException && e.InnerException.Message=="controlled original Shoot exception";}
                nativeFault=false;Require(propagated && Main.mouseX==x && Main.mouseY==y && GetOptional(control,"owner")==null,"native Shoot error propagates after the real outer receipt finalizer returns input");
                var receipt=Call(control,"BeginShot",p,p.HeldItem,null);Call(control,"EndShot",receipt);var successor=Call(control,"BeginShot",p,p.HeldItem,null);Call(control,"EndShot",receipt);Require(ReferenceEquals(Get(control,"owner"),successor),"late duplicate retired receipt cannot revoke the successor source");Call(control,"EndShot",successor);Require(GetOptional(control,"owner")==null,"successor outer receipt returns once");
                Console.WriteLine("PASS real Shoot birth preparation local latch/recovery, original exception and late duplicate source receipt");
            }
            finally{AppDomain.CurrentDomain.FirstChanceException-=FaultTrace;optionalFault=nativeFault=false;harmony.UnpatchAll(harmony.Id);Call(attack,"Reset");NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
    }
}
