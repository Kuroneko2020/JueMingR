using System;
using System.Reflection;
using HarmonyLib;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Explicit one-run test instrumentation. No production flag/logging and
    // no altered input/AI/Damage result. The actual Source entry runs after preparation
    // scopes, so console time is not charged to the shared allowance.
    internal sealed class NativeCombatWeaponPreparationTrace:IDisposable
    {
        private static object context;private static int weapon,remaining;
        private readonly Harmony audit;
        private const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        internal NativeCombatWeaponPreparationTrace(object value,int type)
        {
            if(Environment.GetEnvironmentVariable("JMR_B_WEAPON_DIAGNOSTIC")!="1")return;
            context=value;weapon=type;remaining=18;audit=new Harmony("JueMingR.Tests.WeaponPreparationTrace");var attack=Get(Get(value,"Combat"),"Attack").GetType();
            audit.Patch(attack.GetMethod("BeginShot",Flags),prefix:new HarmonyMethod(typeof(NativeCombatWeaponPreparationTrace),nameof(Source)));
        }
        private static string Contact(object attack,string name)
        {var contact=GetOptional(attack,name);return contact==null?"none":Get(contact,"Tick")+"@"+Get(Get(contact,"Timeline"),"SampleTick");}
        private static void Snapshot(object attack,string stage)
        {
            var p=Main.LocalPlayer;if(context==null || p.HeldItem.type!=weapon || remaining<=0)return;
            remaining--;var host=Get(context,"CombatObservation");var cache=Get(Get(host,"Prediction"),"Cache");var path=Call(cache,"Read",1);
            var ammo=GetOptional(attack,"ammo");bool matches=ammo!=null && (bool)Call(ammo,"Matches",p,p.HeldItem);
            var current=GetOptional(attack,"current");bool timeline=current!=null && (bool)Call(attack,"TimelineCurrent",Get(current,"Timeline"));
            Console.WriteLine("WEAPON-PREP item="+weapon+" stage="+stage+" guc="+Main.GameUpdateCount+" timer="+p.itemAnimation+"/"+p.itemTime+" dir="+p.direction+" pos="+p.position+" left="+Get(Get(context,"Combat"),"Left")+" permission="+Get(attack,"Permission")+" prepared="+Get(attack,"prepared")+" phase="+Get(attack,"preparedOrdinaryPhase")+" current="+Contact(attack,"current")+" next="+Contact(attack,"next")+" valid="+Call(attack,"Valid",true)+" ammo="+matches+" timeline="+timeline+" target="+Get(Get(host,"Selection"),"Target")+" npcPath="+(path==null?"none":Get(path,"SampleTick")+"/"+Get(path,"Count"))+" required="+Get(cache,"Required")+" exceeded="+Get(Get(host,"PredictionWork"),"Exceeded")+" failed="+Get(attack,"Failed"));
        }
        private static void Source(object __instance){Snapshot(__instance,"actual-source");}
        public void Dispose(){if(audit!=null){audit.UnpatchAll(audit.Id);context=null;}}
    }
}
