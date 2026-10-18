using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.View
{
    // DrawNPCs owns one coarse draw rectangle, independent of NPC update and
    // synchronization. Expand only that local rectangle after the native final
    // camera is published; OFF-Retained has exactly the same rendering duty.
    internal static class ViewNpcCulling
    {
        internal static IEnumerable<CodeInstruction> Expand(IEnumerable<CodeInstruction> source)
        {
            var code = new List<CodeInstruction>(source);
            var constructor = typeof(Rectangle).GetConstructor(new[] { typeof(int), typeof(int), typeof(int), typeof(int) });
            var width = AccessTools.Field(typeof(Main), "screenWidth");
            var height = AccessTools.Field(typeof(Main), "screenHeight");
            int matches = 0;
            for (int i = 6; i < code.Count; i++)
            {
                // Locked .8 IL004A constructs the initial culling rectangle by
                // address. Do not rewrite the later per-NPC hitbox constructor.
                if (code[i].opcode != OpCodes.Call || !Equals(code[i].operand, constructor) ||
                    !code[i - 6].LoadsField(width) || !code[i - 5].LoadsConstant(1600) || code[i - 4].opcode != OpCodes.Add ||
                    !code[i - 3].LoadsField(height) || !code[i - 2].LoadsConstant(1600) || code[i - 1].opcode != OpCodes.Add) continue;
                code[i].operand = AccessTools.Method(typeof(ViewNpcCulling), nameof(Initialize));
                matches++;
            }
            if (matches != 1) throw new InvalidOperationException(".8 DrawNPCs culling rectangle shape mismatch: " + matches);
            return code;
        }

        private static void Initialize(ref Rectangle rectangle, int x, int y, int width, int height)
        {
            rectangle = new Rectangle(x, y, width, height);
            if (!ViewHooks.Expanded) return;
            Vector2 position = Main.Camera.ScaledPosition, size = Main.Camera.ScaledSize;
            if (!Finite(position.X) || !Finite(position.Y) || !Finite(size.X) || !Finite(size.Y) || size.X <= 0f || size.Y <= 0f) return;
            // Retain the native 800-world-pixel sprite allowance around the
            // actual visible camera, and retain every native draw candidate.
            // These are local values: physical dimensions, NPC flags and the
            // server's simulation/activation rectangles remain untouched.
            double left = Math.Min(x, Math.Floor(position.X) - 800d);
            double top = Math.Min(y, Math.Floor(position.Y) - 800d);
            double right = Math.Max((double)x + width, Math.Ceiling((double)position.X + size.X) + 800d);
            double bottom = Math.Max((double)y + height, Math.Ceiling((double)position.Y + size.Y) + 800d);
            if (left < int.MinValue || top < int.MinValue || right > int.MaxValue || bottom > int.MaxValue ||
                right - left > int.MaxValue || bottom - top > int.MaxValue) return;
            rectangle = new Rectangle((int)left, (int)top, (int)(right - left), (int)(bottom - top));
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
