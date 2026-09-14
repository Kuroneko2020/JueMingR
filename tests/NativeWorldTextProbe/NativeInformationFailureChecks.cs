using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Information;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeInformationFailureChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static object metrics;
        private static int faults;
        private static void FailLuckMeasurement(object __instance, string text)
        { if (ReferenceEquals(metrics, __instance) && text.Contains("幸运")) { faults++; throw new InvalidOperationException("injected information glyph failure"); } }
        private static void FailHudPrepare() { faults++; throw new InvalidOperationException("injected information preparation failure"); }
        internal static void Run(object context, object host)
        {
            var hud = Get(host, "Hud"); var shell = Get(context, "Shell");
            Main.gameMenu = false; Main.hideUI = Main.mapFullscreen = false; Main.netMode = 1;
            foreach (InformationKind kind in new[] { InformationKind.Infection, InformationKind.Luck, InformationKind.Angler }) Call(host, "SetEnabled", kind, true);
            Call(context, "UpdateRuntime");
            object[] neighbors = { Get(Get(context, "items"), "Feature"), Get(Get(context, "Labels"), "Feature"), Get(Get(context, "WorldTargets"), "Feature") };
            var before = Array.ConvertAll(neighbors, n => (bool)Get(n, "HasFailed"));
            bool objectsFailed = (bool)Get(Get(context, "WorldObjects"), "failed");
            var preferences = Get(host, "Preferences"); object expected = Get(preferences, "Value");
            var assembly = host.GetType().Assembly;
            var measure = assembly.GetType("JueMingR.TerrariaHost.F5.UiTextMetrics", true).GetMethod("Measure", Flags);
            var prepare = hud.GetType().GetMethod("Prepare", Flags);
            var guard = new Harmony("JueMingR.NativeInformation.FailureGuard"); metrics = Get(hud, "metrics"); faults = 0;
            try
            {
                guard.Patch(measure, new HarmonyMethod(typeof(NativeInformationFailureChecks).GetMethod(nameof(FailLuckMeasurement), Flags)));
                Call(context, "UpdateShell");
                var blocks = (Array)Get(hud, "blocks");
                Require(faults == 1 && (bool)Get(blocks.GetValue(2), "Failed") && (bool)Get(hud, "Visible") &&
                    (int)Get(blocks.GetValue(1), "VisibleLines") > 0 && (int)Get(blocks.GetValue(3), "VisibleLines") > 0,
                    "real UpdateShell isolates one failed glyph block while other summaries stay visible");
                for (int i = 0; i < 50; i++) { Main.LocalPlayer.luck += .01f; Call(context, "UpdateRuntime"); Call(context, "UpdateShell"); }
                Require(faults == 1 && !((bool)Get(Get(context, "Runtime"), "FeatureFailed")) && !(bool)Get(shell, "failed") && expected.Equals(Get(Get(host, "Preferences"), "Value")),
                    "failed block does not retry changing text on the same font or disable existing Runtime/F5/settings");
                guard.Unpatch(measure, HarmonyPatchType.All, guard.Id);
                faults = 0; guard.Patch(prepare, new HarmonyMethod(typeof(NativeInformationFailureChecks).GetMethod(nameof(FailHudPrepare), Flags)));
                for (int i = 0; i < 20; i++) Call(context, "UpdateShell");
                Require(faults == 1 && !(bool)Get(hud, "Visible") && !(bool)Get(Get(context, "Runtime"), "FeatureFailed") && !(bool)Get(shell, "failed"),
                    "unexpected HUD preparation failure is bounded to information presentation without escaping UpdateShell");
                for (int i = 0; i < neighbors.Length; i++) Require((bool)Get(neighbors[i], "HasFailed") == before[i], "existing automation and world label failure states remain unchanged");
                Require((bool)Get(Get(context, "WorldObjects"), "failed") == objectsFailed, "existing world-object presentation failure state remains unchanged");
                Console.WriteLine("PASS: actual glyph failure isolates its block; module preparation failure never escapes to existing Runtime/F5, with bounded retry work.");
            }
            finally { guard.Unpatch(measure, HarmonyPatchType.All, guard.Id); guard.Unpatch(prepare, HarmonyPatchType.All, guard.Id); metrics = null; }
        }
    }
}
