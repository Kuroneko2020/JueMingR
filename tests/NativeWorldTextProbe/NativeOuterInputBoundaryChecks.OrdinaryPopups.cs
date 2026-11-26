using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.UI;

namespace NativeWorldTextProbe
{
    internal static partial class NativeOuterInputBoundaryChecks
    {
        private static void OrdinaryPopupInput(Type worker)
        {
            var state = Get(shell, "State");
            var layout = Get(state, "Layout");
            var renderer = Get(shell, "renderer");
            var hotkeys = Get(input, "Hotkeys");
            var death = Get(context, "DeathRecords");
            var popup = Get(shell, "DeathPopup");
            var bindings = PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus;
            var originalJump = bindings["Jump"];
            var originalMain = Main.instance;
            var originalFont = Terraria.GameContent.FontAssets.MouseText;
            int originalX = px, originalY = py;
            var resources = new Harmony("JueMingR.Tests.OrdinaryPopupCpuFont");

            bool Visible(object value) => (bool)Get(value, "Visible");
            bool Suppressed(Keys key) => (bool)Call(hotkeys, "IsSuppressed", (int)key);
            int Count() => (int)Get(Get(death, "Settings"), "Count");
            void ExpectUnclaimedW() => Require(!Suppressed(Keys.W),
                "ordinary popup input cannot claim an unrelated, continuously held W");
            void Step(Vector2 point, bool down, params Keys[] keys)
            {
                px = (int)point.X; py = (int)point.Y;
                Frame(down, keys);
                Call(context, "UpdateRuntime");
                Call(context, "UpdateShell");
                Require(!(bool)Get(shell, "Failed"), "actual ordinary popup shell stays available");
            }
            void Click(Vector2 point, params Keys[] keys)
            {
                Step(point, true, keys);
                Step(point, false, keys);
            }
            Vector2 Navigation(int page) => PopupTestCenter(Call(layout, "Navigation", page),
                (float)Get(state, "X"), (float)Get(state, "Y"));
            Vector2 Entry(string id)
            {
                var item = ((IEnumerable)Get(layout, "Elements")).Cast<object>().Single(e => Get(e, "Command").ToString() == id);
                var viewport = Get(layout, "Viewport");
                return PopupTestCenter(Get(item, "Rect"), (float)Get(state, "X") + (float)Get(viewport, "X"),
                    (float)Get(state, "Y") + (float)Get(viewport, "Y") - (float)Get(state, "Scroll"));
            }
            Vector2 ButtonPoint(object window, int command)
            {
                int index = ((IList)Get(window, "Commands")).IndexOf(command);
                Require(index >= 0 && (bool)((IList)Get(window, "Enabled"))[index], "actual ordinary popup button available " + command);
                var panel = Get(window, "Panel");
                return PopupTestCenter(Get(((IList)Get(window, "Buttons"))[index], "Rect"), (float)Get(panel, "X"), (float)Get(panel, "Y"));
            }
            void Open(string command, object window)
            {
                var entry = Entry(command);
                Step(entry, true, Keys.W);
                Require(!Visible(window), command + " only opens on the original mouse release");
                Step(entry, false, Keys.W);
                Require(Visible(window), "actual F5 click opens and prepares " + command);
                ExpectUnclaimedW();
            }

            var outside = new Vector2(150, 150); // Existing native UserInterface button outside the F5 panel.
            var neutral = new Vector2(20, 20);
            try
            {
                Main.instance = main;
                // Only the absent GPU texture is bypassed in this existing CPU
                // leaf. F5 still establishes readiness and builds its layout.
                resources.Patch(renderer.GetType().GetMethod("RefreshResources", F), postfix:
                    new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks), nameof(OrdinaryPopupCpuFont)));
                var anchors = new List<GameInterfaceLayer>();
                foreach (string name in new[] { "Achievement Complete Popups", "Cursor", "Mouse Over", "Interface Logic 4" })
                    anchors.Add(new LegacyGameInterfaceLayer("Vanilla: " + name, () => true, InterfaceScaleType.UI));
                worker.GetMethod("EnsureF5Layers", F).Invoke(null, new object[] { anchors });
                NativeQuickItemChecks.Until(() =>
                {
                    Call(context, "UpdateRuntime"); Call(context, "UpdateShell");
                    return (bool)Get(state, "Ready") && (bool)Get(death, "ControlsEnabled");
                });
                Step(neutral, false, Keys.W); Step(neutral, false, Keys.W);
                if (!Visible(state)) Step(neutral, false, Keys.F5, Keys.W);
                Step(neutral, false, Keys.W);
                Click(Navigation(2), Keys.W);
                Require((int)Get(state, "Page") == 2, "original Mouse1 opens the map tab while a real movement key is held");

                Open("DeathConfigure", popup);
                var change = ButtonPoint(popup, 128);
                int baselineCount = Count(), beforeClicks = clicks;
                bindings["Jump"] = new List<string> { nameof(Keys.Space), "Mouse1" };
                Step(change, false, Keys.W, Keys.Space); Step(change, false, Keys.W, Keys.Space);
                int reads = keyboardSamples;
                long started = Stopwatch.GetTimestamp();
                for (int frame = 0; frame < 12; frame++) { Step(change, false, Keys.W, Keys.Space); ExpectUnclaimedW(); }
                double milliseconds = (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
                Require(keyboardSamples - reads == 12, "passive ordinary popup ownership needs no additional keyboard read");
                Console.WriteLine($"WORKLOAD ordinary popup hover: frames=12 nativeKeyboardSamples={keyboardSamples - reads} elapsedMS={milliseconds:F3}");
                Step(change, true, Keys.W, Keys.Space);
                Require((int)Get(popup, "Pressed") == 128 && !Main.LocalPlayer.controlUseItem,
                    "actual modal press owns its Mouse1 instead of passing a use edge to vanilla");
                Step(outside, true, Keys.W);
                ExpectUnclaimedW();
                Require(!Main.LocalPlayer.controlUseItem && !Main.mouseLeft, "dragging the captured press outside cannot begin a vanilla click");
                Step(outside, false, Keys.W);
                Require(Count() == baselineCount && clicks == beforeClicks, "release outside neither selects a quantity nor clicks underlying native UI");
                bindings["Jump"] = originalJump;

                Step(change, true, Keys.W);
                Require((int)Get(popup, "Pressed") == 128, "fresh physical press arms the current geometry");
                FiniteCostChecks.SetCpuFont(8);
                Step(change, false, Keys.W);
                Require(Count() == baselineCount, "visual resource replacement retires the old native release target");
                Click(ButtonPoint(popup,128), Keys.W);
                Require(Count() == 128, "current quantity changes during the same uninterrupted W hold");
                Click(ButtonPoint(popup,0), Keys.W);
                Require(!Visible(popup), "native close button works during the same held W");
                Step(neutral, false, Keys.W);
                Click(Navigation(1), Keys.W);
                Require((int)Get(state, "Page") == 1, "after mouse close, ordinary F5 navigation still works without releasing W");
                Click(Navigation(2), Keys.W);
                Open("DeathConfigure", popup);
                Step(neutral, false, Keys.W, Keys.Escape);
                Require(!Visible(popup) && !Suppressed(Keys.W) && Suppressed(Keys.Escape), "fresh Esc claims its own tail and closes the popup");
                Step(neutral, false, Keys.W); Step(neutral, false, Keys.W);
                Require(Main.LocalPlayer.controlUp && !Suppressed(Keys.W), "original movement resumes after only Esc releases");

                Open("DeathConfigure", popup);
                change = ButtonPoint(popup, 256);
                Step(change, true, Keys.W);
                Call(shell, "CloseAndSubmitPosition");
                beforeClicks = clicks;
                bindings["Jump"] = new List<string> { nameof(Keys.Space), "Mouse1" };
                Step(outside, true, Keys.W);
                Require(!Main.LocalPlayer.controlUseItem && !Main.LocalPlayer.controlJump,
                    "the retained Mouse1 alone cannot activate either native action after programmatic close");
                Step(outside, true, Keys.W, Keys.Space); Step(outside, true, Keys.W, Keys.Space);
                Require(!Main.LocalPlayer.controlUseItem && !Suppressed(Keys.W) && Main.LocalPlayer.controlUp && Main.LocalPlayer.controlJump,
                    "programmatic close retains only the Mouse1 source, preserving independent W and Space even with shared Jump binding");
                Step(outside, false, Keys.W, Keys.Space); Step(outside, false, Keys.W, Keys.Space);
                Require(Main.LocalPlayer.controlUp && Main.LocalPlayer.controlJump,
                    "real Mouse1 release restores independent held W and keyboard Space with a shared Jump binding");
                bindings["Jump"] = originalJump;
                Require(Count() == 128 && clicks == beforeClicks, "old release after programmatic close does not replay into either surface");
                var vanilla = new[] { Keys.W, Keys.D, Keys.Space, Keys.Escape };
                Step(outside, true, vanilla); Step(outside, true, vanilla);
                var player = Main.LocalPlayer;
                Require(player.controlUp && player.controlRight && player.controlJump && player.controlInv && player.controlUseItem,
                    "native CopyInto receives continuous W and fresh movement, jump, inventory and mouse after popup close");
                Step(outside, false, Keys.W);
                Require(clicks == beforeClicks + 1, "fresh native UserInterface click is delivered once with W still held");

                Step(neutral, false, Keys.F5, Keys.W); Step(neutral, false, Keys.W);
                Open("DeathConfigure", popup);
                change = ButtonPoint(popup, 256);
                Step(change, true, Keys.W);
                foreground = false;
                Step(change, true, Keys.W); Step(change, false);
                Require(!(bool)Get(input, "CanUseInput") && !Suppressed(Keys.W) && Count() == 128,
                    "background false button state does not deliver the old release or create a W tail");
                foreground = true;
                Step(outside, true, Keys.W);
                Require(!Main.LocalPlayer.controlUseItem && !(bool)Get(input, "CanUseInput"),
                    "a held mouse on activation cannot bypass the modal's original physical tail");
                beforeClicks = clicks;
                Step(outside, false);
                Step(outside, true);
                Require((bool)Get(input, "CanUseInput") && Main.LocalPlayer.controlUseItem && Count() == 128,
                    "one credible neutral sample admits the immediately following fresh Mouse1");
                Step(outside, false);
                Require(clicks == beforeClicks + 1, "trusted recovery delivers the new native click exactly once");

                Step(neutral, false, Keys.F5, Keys.W); Step(neutral, false, Keys.W);
                foreach (var entry in new[] { new[] { "MarkerManage", "MapPopup" }, new[] { "FootprintConfigure", "FootprintPopup" } })
                {
                    var window = Get(shell, entry[1]);
                    Open(entry[0], window);
                    Click(ButtonPoint(window, 0), Keys.W);
                    Require(!Visible(window), entry[1] + " ordinary mouse close works during held W");
                    ExpectUnclaimedW();
                    Step(neutral, false, Keys.W);
                }
                Step(neutral, false, Keys.F5); Step(neutral, false);
                Console.WriteLine("PASS real native ordinary popup clicks: Death, Map and Footprint preserve unrelated W, protect source-specific press/release, and recover correctly after focus loss.");
            }
            finally
            {
                bindings["Jump"] = originalJump;
                foreground = nativeSelected = true;
                resources.UnpatchAll(resources.Id);
                Main.instance = originalMain;
                Terraria.GameContent.FontAssets.MouseText = originalFont;
                px = originalX; py = originalY;
            }
        }

        private static Vector2 PopupTestCenter(object rect, float x, float y) =>
            new Vector2(x + (float)Get(rect, "X") + (float)Get(rect, "Width") * .5f,
                y + (float)Get(rect, "Y") + (float)Get(rect, "Height") * .5f);

        private static void OrdinaryPopupCpuFont(object __instance, ref bool __result)
        {
            if (Get(__instance, "FontIdentity") != null) __result = true;
        }
    }
}
