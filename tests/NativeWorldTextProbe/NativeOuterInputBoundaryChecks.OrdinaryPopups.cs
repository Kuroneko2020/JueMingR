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
                AutomaticTeleportPopupInput(resources, Step, originalJump, outside);
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

        private static void AutomaticTeleportPopupInput(Harmony resources, Action<Vector2, bool, Keys[]> step,
            List<string> originalJump, Vector2 outside)
        {
            var window = Get(shell, "CompanionUi");
            var host = Get(context, "Companions");
            var layout = Get(Get(shell, "State"), "Layout");
            var pane = Get(window, "autoTeleportPopup");
            var hotkeys = Get(input, "Hotkeys");
            var bindings = PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus;
            var archive = Get(host, "profiles");
            // The companion window at this viewport reaches the old test
            // coordinate (20,20). Use the outer margin, where native controls
            // are eligible even while the companion manager remains open.
            Vector2 worldPoint = new Vector2(4, 4);
            Vector2 neutral = worldPoint * Main.UIScale;
            Keys[] held = { Keys.W };
            resources.Patch(Get(window, "renderer").GetType().GetMethod("Refresh", F),
                postfix: new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks), nameof(CompanionPopupCpuAssets)));
            var text = Get(pane, "textInput");
            var ime = text.GetType().GetNestedType("NativeIme", F);
            resources.Patch(ime.GetProperty("Composition", F).GetGetMethod(true),
                prefix: new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks), nameof(ImeComposition)));
            resources.Patch(ime.GetProperty("Candidates", F).GetGetMethod(true),
                prefix: new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks), nameof(NoGamepad)));
            resources.Patch(ime.GetMethod("Toggle", F),
                prefix: new HarmonyMethod(typeof(NativeOuterInputBoundaryChecks), nameof(ImeToggleSink)));
            NativeQuickItemChecks.Until(() =>
            {
                step(neutral, false, held);
                return (bool)Get(host, "CanCreate");
            });
            Require((bool)Call(host, "Create", "自动传送输入验证"), "outer popup fixture creates an actual owned archive profile");
            Guid id = (Guid)Get(host, "LastCreatedId");
            NativeQuickItemChecks.Until(() =>
            {
                step(neutral, false, held);
                return (bool)Call(archive, "IsSaved", id);
            });
            bool Visible() => (bool)Get(pane, "Visible");
            void UnclaimedW() => Require(!(bool)Call(hotkeys, "IsSuppressed", (int)Keys.W), "ordinary teleport popup preserves held W");
            Vector2 ControlPoint(string prefix)
            {
                var control = ((IEnumerable)Get(window, "controls")).Cast<object>().Single(c =>
                    ((string)Get(c, "Key")).StartsWith(prefix, StringComparison.Ordinal));
                return PopupTestCenter(Get(control, "Rect"), 0, 0) * Main.UIScale;
            }
            void Click(Vector2 point)
            {
                step(point, true, held); step(point, false, held); UnclaimedW();
            }
            void Open()
            {
                Call(shell, "OpenCompanions", Get(layout, "Viewport"));
                step(neutral, false, held);
                Require((bool)Get(window, "Visible"), "the actual CompanionWindow prepared without a GPU draw");
                if ((int)Get(window, "page") != 1) Click(ControlPoint("configuration/"));
                Click(ControlPoint("config-AutoTeleportEnabled/配置/"));
                Require(Visible(), "native input opens the actual automatic teleport popup");
            }
            void Queue(string typed)
            {
                Main.keyCount = typed.Length;
                for (int i = 0; i < typed.Length; i++)
                { Main.keyInt[i] = typed[i]; Main.keyString[i] = typed[i].ToString(); }
            }

            Open();
            object blocked = Get(pane, "blocked");
            var check = PopupTestCenter(Get(blocked, "Check"), 0, 0) * Main.UIScale;
            Click(check);
            Require(!NativeCompanionConfigurationChecks.Profile(host, id).Behavior.AutoTeleportBlockedEnabled,
                "the real checkbox commits its setting during uninterrupted W");
            Click(PopupTestCenter(Get(pane, "close"), 0, 0) * Main.UIScale);
            Require(!Visible(), "native close button dismisses the automatic teleport popup");
            Click(ControlPoint("config-AutoTeleportEnabled/配置/"));
            step(neutral, false, new[] { Keys.W, Keys.Escape });
            Require(!Visible() && (bool)Call(hotkeys, "IsSuppressed", (int)Keys.Escape), "ordinary Escape closes only the popup");
            UnclaimedW();
            Require((bool)Get(window, "Visible") && !(bool)Call(shell, "ClaimsPopupPoint", worldPoint),
                "Escape leaves the companion manager open and the cursor outside the remaining F5 surfaces");
            step(neutral, false, held); step(neutral, false, held);
            UnclaimedW();

            Click(ControlPoint("config-AutoTeleportEnabled/配置/"));
            Require(Visible(), "the same held W allows ordinary clicks after Escape releases");
            bindings["Jump"] = new List<string> { nameof(Keys.Space), "Mouse1" };
            check = PopupTestCenter(Get(blocked, "Check"), 0, 0) * Main.UIScale;
            step(check, true, held);
            Require(Get(pane, "pressed").ToString() == "BlockedCheck" && ((int)Get(input, "uiMouseTail") & 1) != 0 &&
                !Main.LocalPlayer.controlUseItem && !Main.LocalPlayer.controlJump,
                "the actual checkbox owns the initial Mouse1 source before any use item or jump");
            Call(shell, "CloseAndSubmitPosition");
            int before = clicks;
            step(outside, true, held); step(outside, true, held);
            Require(Main.LocalPlayer.controlUp && !Main.LocalPlayer.controlUseItem && !Main.LocalPlayer.controlJump,
                "native movement resumes while the retired Mouse1 item use and jump source stays owned");
            step(outside, true, new[] { Keys.W, Keys.Space }); step(outside, true, new[] { Keys.W, Keys.Space });
            Require(!Main.LocalPlayer.controlUseItem && Main.LocalPlayer.controlUp && Main.LocalPlayer.controlJump,
                "the retired checkbox Mouse1 cannot suppress independent W or Space even with a shared Jump binding");
            UnclaimedW();
            step(outside, false, held);
            Require(clicks == before && !NativeCompanionConfigurationChecks.Profile(host, id).Behavior.AutoTeleportBlockedEnabled,
                "the original release reaches neither the closed checkbox nor the native interface below it");
            bindings["Jump"] = originalJump;
            step(outside, true, held); step(outside, false, held);
            Require(clicks == before + 1, "a truly new native mouse gesture is delivered once with W still down");
            step(neutral, false, Array.Empty<Keys>());

            Open();
            var field = PopupTestCenter(Get(blocked, "Field"), 0, 0) * Main.UIScale;
            Click(field);
            expectTextLease = true;
            try { step(field, false, Array.Empty<Keys>()); }
            finally { expectTextLease = false; }
            Require((bool)Get(text, "OwnsTextToken") && !(bool)Get(Get(window, "textInput"), "OwnsTextToken") &&
                observedTextWriting && Main.blockInput, "only the numeric editor holds the actual outer input lease");
            Queue("13\r"); step(field, false, new[] { Keys.Enter });
            Require(NativeCompanionConfigurationChecks.Profile(host, id).Behavior.AutoTeleportBlockedSeconds == 13 &&
                !(bool)Get(text, "OwnsTextToken") && !Main.blockInput,
                "the original Enter character queue commits only the numeric field and returns the block lease");
            step(field, false, Array.Empty<Keys>());
            Click(field); step(field, false, Array.Empty<Keys>());
            Queue("29"); step(field, false, Array.Empty<Keys>());
            foreground = false;
            step(field, true, Array.Empty<Keys>());
            Require(!Visible() && !Main.blockInput && !(bool)Get(text, "OwnsTextToken") &&
                NativeCompanionConfigurationChecks.Profile(host, id).Behavior.AutoTeleportBlockedSeconds == 13,
                "real focus loss retires the draft and the original Main block without saving its partial input");
            foreground = true;
            step(neutral, false, Array.Empty<Keys>()); step(neutral, false, held); step(neutral, false, held);
            Require(Main.LocalPlayer.controlUp, "native movement resumes after focus returns and a trusted neutral sample");
            step(neutral, false, Array.Empty<Keys>());
            Console.WriteLine("PASS actual automatic teleport popup through original input: ordinary W, retained mouse source, shared Jump, native numeric lease and Enter, and focus retirement.");
        }

        private static void CompanionPopupCpuAssets(ref bool __result)
        {
            // Only the GPU asset readiness gate is bypassed in a CPU leaf.
            __result = true;
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
