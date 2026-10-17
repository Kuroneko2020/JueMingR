using System;
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
    // Focused ownership/timing oracle. Entry timers below are observed after
    // real ItemCheck starts; the public plan must only advance its private pose.
    internal static class NativeCombatBalancedPhaseChecks
    {
        private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        private static bool watching;
        private static Player original;
        private static int calls,animation,time,stage;
        private static void Spear(Player __0){if(watching){Require(!ReferenceEquals(original,__0),"future spear plan owns a private player");calls++;animation=__0.itemAnimation;time=__0.itemTime;}}
        private static void Arrow(Player __0){if(watching){Require(!ReferenceEquals(original,__0),"future bow plan owns a private player");calls++;animation=__0.itemAnimation;time=__0.itemTime;}}
        private static void Lane(int __4){if(watching && stage<0)stage=__4;}
        internal static void Run(object context)
        {
            var host=Get(context,"CombatObservation");var combat=Get(context,"Combat");var attack=Get(combat,"Attack");var assembly=attack.GetType().Assembly;
            var audit=new Harmony("JueMingR.Tests.BalancedPhase");
            audit.Patch(assembly.GetType("JueMingR.TerrariaHost.Combat.HostRepresentativeAttack",true).GetMethod("PrepareSpear",F),prefix:new HarmonyMethod(typeof(NativeCombatBalancedPhaseChecks),nameof(Spear)));
            var arrow=assembly.GetType("JueMingR.TerrariaHost.Combat.HostNativeArrowBirthAttack",true);
            audit.Patch(arrow.GetMethod("Prepare",F),prefix:new HarmonyMethod(typeof(NativeCombatBalancedPhaseChecks),nameof(Arrow)));
            audit.Patch(arrow.GetMethod("Birth",F),prefix:new HarmonyMethod(typeof(NativeCombatBalancedPhaseChecks),nameof(Lane)));
            try
            {
                foreach(int weapon in new[]{1947,2331,4953})
                {
                    NativeCombatObservationChecks.Save(host,new ObservationOptions());
                    original=NativeToolExecutionChecks.Reset(context,Get(context,"Tools"),Get(context,"Input"),weapon,0,0);
                    foreach(var armor in original.armor)armor.TurnToAir();original.ResetEffects();original.position=new Vector2(700,646);original.releaseUseItem=true;
                    original.inventory[54].SetDefaults(40);original.inventory[54].stack=999;Main.screenPosition=new Vector2(480,400);
                    var target=NativeCombatControlPreviewChecks.Dummy(2,75,40);var cursor=new Vector2(500,430);
                    NativeCombatObservationChecks.Save(host,new ObservationOptions(false,true,false,false,true,50,false,true));
                    int first=-1,delay=-1;bool found=false;
                    var window=assembly.GetType("JueMingR.TerrariaHost.Combat.HostAttackWindow",true).GetMethod("PreviewDelay",F);
                    for(int frame=0;frame<45;frame++)
                    {
                        NativeCombatBalancedAimChecks.StepAt(context,cursor,frame>=3);
                        if(first<0 && original.itemAnimation>0)first=frame;
                        delay=(int)window.Invoke(null,new object[]{original,original.HeldItem});
                        if(weapon==4953?delay>0 && original.itemAnimation>delay+1:first>=0 && frame==first+6 && delay>0){found=true;break;}
                    }
                    Require(found,"native current-hand entry supplies a finite next-launch delay");
                    var identity=(NpcIdentity)assembly.GetType("JueMingR.TerrariaHost.Combat.CombatSelection",true).GetMethod("Identity",F).Invoke(null,new object[]{target,1L});
                    var state=new NpcMotionState{Identity=identity,X=target.position.X,Y=target.position.Y,Width=target.width,Height=target.height,Active=true,CanReceive=true};
                    var path=new NpcTrajectory(identity,(long)Main.GameUpdateCount-1,1,PredictionAssumption.NoNewHits,PredictionStop.None,Enumerable.Range(0,121).Select(t=>new NpcTrajectoryPoint(t,state)).ToArray(),121,PredictionStrategy.RollingConditional);
                    var prepare=attack.GetType().GetMethod("PreparePlan",F);var plan=Activator.CreateInstance(prepare.GetParameters()[0].ParameterType,true);
                    var phase=Enum.Parse(prepare.GetParameters()[2].ParameterType,"CompletedWorld");
                    int beforeAnimation=original.itemAnimation,beforeTime=original.itemTime,beforeReuse=original.reuseDelay,maximum=original.itemAnimationMax;
                    calls=0;animation=time=stage=-1;watching=true;
                    try{prepare.Invoke(attack,new[]{plan,(object)path,phase});}finally{watching=false;}
                    Require(calls>0,"public future plan actually enters its representative birth adapter");
                    Require(original.itemAnimation==beforeAnimation && original.itemTime==beforeTime && original.reuseDelay==beforeReuse,"future planning never rewrites native current timers");
                    if(weapon==4953)
                    {
                        Require(animation==beforeAnimation,"same-animation Eventide keeps the original timer for exactly one delay/phase subtraction");
                        int expected=(maximum-(beforeAnimation-delay-1))/2;
                        Require(stage==expected,"Eventide representative ordinal matches the native remaining-animation clock");
                    }
                    else Require(animation==0 && time==0,"next fresh spear hand resets only its private old timers before deriving emission");
                    Console.WriteLine("BALANCED-PHASE item="+weapon+" nativeAnimation="+beforeAnimation+" nativeTime="+beforeTime+" nativeMaximum="+maximum+" delay="+delay+" privateAnimation="+animation+" privateTime="+time+" ordinal="+stage);
                }
            }
            finally{watching=false;original=null;audit.UnpatchAll(audit.Id);NativeCombatObservationChecks.Save(host,new ObservationOptions());}
        }
    }
}
