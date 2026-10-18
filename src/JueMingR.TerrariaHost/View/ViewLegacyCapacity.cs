using System;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Light;

namespace JueMingR.TerrariaHost.View
{
    internal static class ViewLegacyCapacity
    {
        private static readonly FieldInfo states = AccessTools.Field(typeof(LegacyLighting), "_states");
        private static readonly FieldInfo[] prepared = {
            states, AccessTools.Field(typeof(LegacyLighting), "_axisFlipStates"),
            AccessTools.Field(typeof(LegacyLighting), "_swipe"), AccessTools.Field(typeof(LegacyLighting), "_threadSwipes"),
            AccessTools.Field(typeof(LegacyLighting), "_lightMap") };
        internal static void Ensure(LegacyLighting current)
        {
            if (!ViewHooks.Expanded) return;
            var value = states.GetValue(current) as Array;
            Vector2 need = Main.Camera.ScaledSize;
            if (value != null && value.Length >= (int)need.X / 16 + 100 && ((Array)value.GetValue(0)).Length >= (int)need.Y / 16 + 100) return;
            try
            {
                // Native Rebuild publishes each jagged array before filling it.
                // Perform VIEW's extra growth on a separate original engine;
                // never expose a half-built array or mutate the old LightMap.
                var candidate = new LegacyLighting(Main.Camera);
                candidate.Rebuild();
                var complete = new object[prepared.Length];
                for (int i = 0; i < prepared.Length; i++) complete[i] = prepared[i].GetValue(candidate);
                // Same game-thread boundary as native Rebuild. These are exact
                // reference fields, with no callbacks between publication. Keep
                // current queued temp lights; growth must not discard sources.
                for (int i = 0; i < prepared.Length; i++) prepared[i].SetValue(current, complete[i]);
            }
            catch (OutOfMemoryException error) { throw new ViewRenderFailure(error); }
        }
    }
}
