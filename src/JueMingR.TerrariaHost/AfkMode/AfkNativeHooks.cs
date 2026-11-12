using System;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.AfkMode
{
    // ItemCheck calls this after its real animation/itemTime decrements and
    // before OwnerOnlyCode can spend the next manual action's itemTime. It is
    // a read-only receipt, never another ItemCheck or a speculative use probe.
    internal static class AfkNativeHooks
    {
        private static HostAfkMode host;
        private static readonly Harmony harmony=new Harmony("JueMingR.AfkMode");
        internal static void Install(HostAfkMode value)
        {
            var method=AccessTools.Method(typeof(Player),"AnimatePlayerAndGetItemFrame",new[]{typeof(float),typeof(Item)});
            if(method?.GetMethodBody()==null)throw new MissingMethodException("afk-abi:AnimatePlayerAndGetItemFrame");
            host=value;harmony.Patch(method,prefix:new HarmonyMethod(typeof(AfkNativeHooks),nameof(AfterTimers)));
        }
        private static void AfterTimers(Player __instance){host?.AfterTimers(__instance);}
    }
}
