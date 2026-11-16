using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.AfkMode
{
    // Native outlets retain separate responsibilities: an after-timer receipt,
    // the focus-pause reason, and one late Draw-owned mouse delay write. None
    // runs an extra ItemCheck or changes native focus/configuration/locks.
    internal static class AfkNativeHooks
    {
        private static HostAfkMode host;
        private static readonly Harmony harmony=new Harmony("JueMingR.AfkMode");
        internal static void Install(HostAfkMode value)
        {
            var method=AccessTools.Method(typeof(Player),"AnimatePlayerAndGetItemFrame",new[]{typeof(float),typeof(Item)});
            if(method?.GetMethodBody()==null)throw new MissingMethodException("afk-abi:AnimatePlayerAndGetItemFrame");
            host=value;harmony.Patch(method,prefix:new HarmonyMethod(typeof(AfkNativeHooks),nameof(AfterTimers)));
            var focus=AccessTools.Method(typeof(FocusHelper),"UpdateFocus",new[]{typeof(bool).MakeByRefType()});
            if(focus?.GetMethodBody()==null)throw new MissingMethodException("afk-abi:UpdateFocus");
            harmony.Patch(focus,postfix:new HarmonyMethod(typeof(AfkNativeHooks),nameof(Focus)));
            harmony.Patch(AccessTools.Method(typeof(Player),"Update",new[]{typeof(int)}),transpiler:new HarmonyMethod(typeof(AfkNativeHooks),nameof(DrawMouseDelay)));
        }
        private static IEnumerable<CodeInstruction> DrawMouseDelay(IEnumerable<CodeInstruction> source)
        {
            var code=source.ToList();int count=0;
            var mouse=AccessTools.Field(typeof(Player),"mouseInterface");var delay=AccessTools.Field(typeof(Player),"delayUseItem");
            for(int i=0;i+4<code.Count;i++)
                if(code[i].LoadsField(mouse) && (code[i+1].opcode==OpCodes.Brfalse || code[i+1].opcode==OpCodes.Brfalse_S) &&
                    code[i+2].opcode==OpCodes.Ldarg_0 && code[i+3].opcode==OpCodes.Ldc_I4_1 && code[i+4].StoresField(delay))
                {code[i].opcode=OpCodes.Call;code[i].operand=AccessTools.Method(typeof(AfkNativeHooks),nameof(DrawMouseBlocks));count++;}
            if(count!=1)throw new InvalidOperationException("afk-abi:background-draw-delay-count:"+count);
            return code;
        }
        private static bool DrawMouseBlocks(Player player)
        {return player.mouseInterface && !(host?.IgnoreBackgroundDrawMouse(player)??false);}
        private static void Focus(ref bool wantsToPause)
        {
            // Remove only the unfocused single-player auto-pause reason.
            // Focus, settings, audio and later native pause checks stay real.
            if(wantsToPause && host!=null && Main.netMode==0 && !Main.gameMenu &&
                (host.WantsBackground || host.Tools.FishingBackgroundDemand?.Invoke()==true))wantsToPause=false;
        }
        private static void AfterTimers(Player __instance){host?.AfterTimers(__instance);}
    }
}
