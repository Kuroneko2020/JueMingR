using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace JueMingR.TerrariaHost.View
{
    internal static class ViewOptions
    {
        // The original bar already writes only for its actual mouse/lock
        // interaction. Clamp its presentation input only; opening/hovering
        // options must not write the retained target back to ordinary range.
        internal static IEnumerable<CodeInstruction> Display(IEnumerable<CodeInstruction> source)
        {
            var code = new List<CodeInstruction>(source);
            var target = AccessTools.Field(typeof(Main), "GameZoomTarget");
            int count = 0;
            for (int i = 0; i + 2 < code.Count; i++)
                if (code[i].LoadsField(target) && code[i + 1].opcode == OpCodes.Ldc_R4 && Equals(code[i + 1].operand, 1f) && code[i + 2].opcode == OpCodes.Sub)
                { code[i].opcode = OpCodes.Call; code[i].operand = AccessTools.Method(typeof(ViewOptions), "DisplayTarget"); count++; }
            if (count != 1) throw new InvalidOperationException(".8 original zoom bar shape=" + count);
            return code;
        }
        private static float DisplayTarget() { return Microsoft.Xna.Framework.MathHelper.Clamp(Main.GameZoomTarget, 1f, 2f); }
    }
}
