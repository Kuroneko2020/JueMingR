using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.QuickItems
{
    internal static class QuickItemHooks
    {
        private static HostQuickItems host;
        private static readonly Harmony harmony=new Harmony("JueMingR.QuickItems");
        internal static void Install(HostQuickItems value)
        {
            host=value;
            Patch("PickItemSelectionOverride",new[]{typeof(int).MakeByRefType()},null,nameof(Pick));
            Patch("TrySyncingInput",Type.EmptyTypes,nameof(Sync));
            Patch("ItemCheck",Type.EmptyTypes,nameof(Before),nameof(After),nameof(Final));
            Patch("ItemCheck_StartActualUse",new[]{typeof(Item)},null,nameof(Started));
            Patch("DropItems",new[]{typeof(bool)},nameof(Boundary));
        }
        private static void Patch(string name,Type[] args,string prefix=null,string postfix=null,string finalizer=null)
        {
            var method=typeof(Player).GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,args,null);
            if(method==null || method.GetMethodBody()==null)throw new MissingMethodException("quick-items-abi:"+name);
            harmony.Patch(method,Hook(prefix),Hook(postfix),null,Hook(finalizer));
        }
        private static HarmonyMethod Hook(string name) {return name==null?null:new HarmonyMethod(typeof(QuickItemHooks).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic));}
        internal static void Uninstall() {foreach(var method in harmony.GetPatchedMethods().ToArray())harmony.Unpatch(method,HarmonyPatchType.All,harmony.Id);host=null;}
        private static void Pick(Player __instance,ref int __0,ref bool __result) {host?.Use.Pick(__instance,ref __0,ref __result);}
        private static void Sync(Player __instance) {host?.Use.BeforeSync(__instance);}
        private static void Started(Player __instance) {host?.Use.Started(__instance);}
        private static void Boundary(Player __instance) {if(ReferenceEquals(__instance,host?.Player))host.Use.Retire();}
        private static void Before(Player __instance,out Lease __state)
        {bool mouse=false;__state=new Lease{Operation=host?.Use.Operation??0,Entered=host!=null && host.Use.BeginItemCheck(__instance,out mouse),Mouse=mouse};}
        private static void After(Player __instance,Lease __state) {host?.Use.EndItemCheck(__instance,__state.Operation,__state.Entered,__state.Mouse,null);}
        private static Exception Final(Player __instance,Lease __state,Exception __exception)
        {if(__exception!=null)host?.Use.EndItemCheck(__instance,__state.Operation,__state.Entered,__state.Mouse,__exception);return __exception;}
        private struct Lease {internal long Operation;internal bool Entered,Mouse;}
    }
}
