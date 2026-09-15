using System;
using HarmonyLib;
using JueMingR.Features.Guidance;
using JueMingR.Platform.Operations;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeGuidanceCadenceChecks
    {
        private static bool hintBlocked;
        private static int hintCalls;
        private static bool SkipPanelDraw() { return false; }
        private static bool ObserveHint(bool blocked) { hintBlocked = blocked; hintCalls++; return false; }

        internal static void NameHint(object shell, object input)
        {
            // Invoke the real shell consumer, replacing only GPU entry points.
            // CPU runs cannot create a graphics device; visual runs cover pixels.
            var renderer = Get(shell, "renderer"); var type = renderer.GetType();
            var patch = new Harmony("JueMingR.Guidance.CadenceHints");
            var draw = type.GetMethod("Draw", NativeGuidanceChecks.Flags);
            var hints = type.GetMethod("DrawHints", NativeGuidanceChecks.Flags);
            patch.Patch(draw, prefix: new HarmonyMethod(typeof(NativeGuidanceCadenceChecks).GetMethod(nameof(SkipPanelDraw), NativeGuidanceChecks.Flags)));
            patch.Patch(hints, prefix: new HarmonyMethod(typeof(NativeGuidanceCadenceChecks).GetMethod(nameof(ObserveHint), NativeGuidanceChecks.Flags)));
            try
            {
                Require((bool)Get(Get(shell, "State"), "CanShowHint"), "name hover begins on a real active pointer sample");
                hintCalls = 0; Call(shell, "DrawLayer");
                Require(hintCalls == 1 && !hintBlocked, "sampled update delivers the F5 name hint");
                Call(input, "BeginUpdate"); // Native DoUpdate may return here without HandleInput.
                Require(!(bool)Get(input, "CanStartActions"), "unsampled update cannot authorize an action");
                Call(shell, "DrawLayer");
                Require(hintCalls == 2 && !hintBlocked, "F5 name hint survives an outer Update without input sampling");
            }
            finally
            {
                patch.Unpatch(draw, HarmonyPatchType.All, patch.Id); patch.Unpatch(hints, HarmonyPatchType.All, patch.Id);
                Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh");
            }
        }

        internal static void Displays(object context, object host)
        {
            object input = Get(context, "Input"), world = Get(host, "World");
            var pending = (MerchantTestFeature)Get(host, "MerchantTest");
            var clock = (System.Diagnostics.Stopwatch)Get(host, "clock"); clock.Stop();
            ((EquipmentWarning)Get(host, "Equipment")).Clear();
            try
            {
                for (int i = 0; i < 90; i++)
                {
                    Call(input, "BeginUpdate");
                    if (i % 3 == 0) { Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh"); }
                    else
                    {
                        pending.Request((long)Get(host, "Session"));
                        Require(!(bool)Get(input, "CanStartActions"), "no fresh native sample remains action-ineligible");
                    }
                    Call(context, "UpdateRuntime"); Call(world, "Prepare");
                    foreach (string field in new[] { "rareVisible", "merchantVisible", "equipmentVisible" })
                        Require((bool)Get(world, field), field + " remains visible across sampled and unsampled outer updates");
                    if (i % 3 != 0) Require(!pending.Pending && pending.Result.Outcome == GameOperationOutcome.Cancelled, "unsampled update cancels summon rather than replaying input");
                }
                Set(input, "foregroundWindow", (Func<IntPtr>)(() => IntPtr.Zero)); Call(input, "BeginUpdate"); Call(world, "Prepare");
                Require(!(bool)Get(host, "CanDraw") && !(bool)Get(world, "rareVisible") && !(bool)Get(world, "equipmentVisible"), "actual loss of focus still hides hints immediately");
                Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1))); Call(input, "BeginUpdate");
                Require(!(bool)Get(host, "CanDraw") && !(bool)Get(input, "CanStartActions"), "reactivation still waits for a neutral native sample");
                Main.keyState = default(Microsoft.Xna.Framework.Input.KeyboardState); PlayerInput.MouseInfo = default(Microsoft.Xna.Framework.Input.MouseState);
                Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh");
                Require(!(bool)Get(host, "CanDraw"), "neutral reactivation sample itself remains quarantined");
                Call(input, "BeginUpdate"); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh"); Call(world, "Prepare");
                Require((bool)Get(host, "CanDraw"), "next clean frame restores presentation");
                Main.mapFullscreen = true; Call(world, "Prepare"); Require(!(bool)Get(world, "rareVisible"), "map still hides prepared hints");
                Main.mapFullscreen = false; Main.hideUI = true; Call(world, "Prepare"); Require(!(bool)Get(world, "merchantVisible"), "hidden UI still hides prepared hints");
                Console.WriteLine("PASS: 90 mixed sampled/unsampled updates preserve three displays; absent input cannot summon; focus/quarantine/map/UI gates remain.");
            }
            finally { clock.Start(); Main.mapFullscreen = Main.hideUI = false; Call(input, "BeginUpdate"); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh"); }
        }
    }
}
