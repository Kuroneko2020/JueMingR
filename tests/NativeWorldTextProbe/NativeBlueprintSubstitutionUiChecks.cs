using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real Host preferences, command registration, shared F5 geometry and trusted
    // CPU edges. Visual is a separate original-XNB/XNA consumer, never OS evidence.
    internal static class NativeBlueprintSubstitutionUiChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string ActionId = "blueprint.substitution";
        private static readonly string[] Names = { "工作台", "桌子", "椅子", "门", "床", "种植盆", "铁砧", "秘银砧", "箱子", "火把", "平台", "墙" };

        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var renderer = Get(shell, "renderer");
            var controls = Get(renderer, "BlueprintControls"); var state = Get(shell, "State"); var input = Get(context, "Input");
            var preferences = (PreferenceDocument<BlueprintPreferences>)Get(host, "preference");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return preferences.Snapshot.IsLoaded; });
            var original = preferences.Snapshot.Value;
            var oldFont = FontAssets.MouseText; float oldScale = Main.UIScale;
            int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight;
            var oldMouse = PlayerInput.MouseInfo; var oldRawScale = PlayerInput.RawMouseScale;
            var oldKeyboard = Main.keyState; int oldMouseX = Main.mouseX, oldMouseY = Main.mouseY, oldInputX = PlayerInput.MouseX, oldInputY = PlayerInput.MouseY;
            int oldWheel = PlayerInput.ScrollWheelDelta, oldUiWheel = PlayerInput.ScrollWheelDeltaForUI;
            bool oldLeft = Main.mouseLeft, oldRight = Main.mouseRight, oldInterface = Main.LocalPlayer.mouseInterface, oldMouseText = Main.mouseText;
            var triggerSets = new[] { PlayerInput.Triggers.Current, PlayerInput.Triggers.Old, PlayerInput.Triggers.JustPressed, PlayerInput.Triggers.JustReleased };
            var oldTriggers = triggerSets.Select(t => t.KeyStatus.ToDictionary(p => p.Key, p => p.Value)).ToArray();
            try
            {
                Call(shell, "CloseAndSubmitPosition");
                Call(host, "SetAutoBuild", false); Call(host, "SetSameTypeReplacement", false); Call(host, "SetReplacementCategories", 4095);
                long revision = preferences.Snapshot.Revision, policy = (long)Get(host, "substitutionRevision");
                for (int i = 0; i < 20; i++) { Call(host, "SetSameTypeReplacement", false); Call(host, "SetReplacementCategories", 4095); Call(host, "Poll"); }
                Require(preferences.Snapshot.Revision == revision && (long)Get(host, "substitutionRevision") == policy,
                    "same-value Host commands and stable Poll publish no new preference/policy revision");
                Call(host, "SetReplacementCategories", 0x805); Call(host, "SetToolbar", !original.GelToolbar); Call(host, "SetAutoBuild", true); Call(host, "SetAutoBuild", false);
                Require(preferences.Snapshot.Value.ReplacementCategories == 0x805 && !preferences.Snapshot.Value.SameTypeReplacement &&
                    (long)Get(host, "substitutionRevision") == policy, "old toolbar/build setters retain off-state categories and the exact effective relation");
                Call(host, "SetReplacementCategories", -1); Call(host, "SetReplacementCategories", 4096);
                Require(preferences.Snapshot.Value.ReplacementCategories == 0x805, "Host rejects invalid category commands without truncation");

                FiniteCostChecks.SetCpuFont(10); Call(renderer, "RefreshResources");
                LayoutAndTails(context, host, shell, renderer, controls, preferences, input);
                CommandsAndBinding(context, host, shell, renderer, controls, preferences, input);
                Console.WriteLine("PASS Blueprint substitution UI CPU: exact retained main row and independent Recovery-style four-by-three popup/12 categories/three hints; real main toggle/drag/configure, complete short/tall row scroll, stable metrics, snapshot/geometry release invalidation and retained tails, real Host preferences/common binding/Immediate feedback. GPU/OS/IME/owner acceptance NOT_EXECUTED by this CPU leaf.");
            }
            finally
            {
                Call(shell, "CloseAndSubmitPosition"); preferences.Set(original); Call(host, "Poll");
                FontAssets.MouseText = oldFont; Call(renderer, "RefreshResources"); NativeBlueprintUiChecks.SetScale(oldScale);
                Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; PlayerInput.CacheOriginalScreenDimensions();
                PlayerInput.MouseInfo = oldMouse; PlayerInput.RawMouseScale = oldRawScale;
                Main.keyState = oldKeyboard; Main.mouseX = oldMouseX; Main.mouseY = oldMouseY; PlayerInput.MouseX = oldInputX; PlayerInput.MouseY = oldInputY;
                PlayerInput.ScrollWheelDelta = oldWheel; PlayerInput.ScrollWheelDeltaForUI = oldUiWheel; Main.mouseLeft = oldLeft; Main.mouseRight = oldRight;
                Main.LocalPlayer.mouseInterface = oldInterface; Main.mouseText = oldMouseText;
                for (int i = 0; i < triggerSets.Length; i++) foreach (var pair in oldTriggers[i]) triggerSets[i].KeyStatus[pair.Key] = pair.Value;
            }
        }

        private static void LayoutAndTails(object context, object host, object shell, object renderer, object controls,
            PreferenceDocument<BlueprintPreferences> preferences, object input)
        {
            var assembly = host.GetType().Assembly; var state = Get(shell, "State"); var layout = Get(state, "Layout");
            var popup = Get(shell, "BlueprintReplacementPopup");
            Main.screenWidth = 1280; Main.screenHeight = 720; NativeBlueprintUiChecks.SetScale(1.5f); PlayerInput.CacheOriginalScreenDimensions();
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            Set(state, "Ready", true); Call(state, "Navigate", 6); Call(state, "RestoreVisible");
            Call(renderer, "Prepare", state, 1280f, 720f, 1.5f); Sample(state, null, false);
            var elements = Objects(Get(layout, "Elements"));
            var name = elements.Single(e => (string)GetOptional(e, "Text") == "同类替换");
            Require((string)Get(Get(name, "Description"), "Text") == "符合标准的内容允许使用同类型物品自动放置", "main name keeps the exact description");
            Require(elements.Count(e => (string)GetOptional(e, "HotkeyTarget") == ActionId) == 1 &&
                elements.Where(e => Get(e, "Command").ToString().StartsWith("BlueprintSubstitution", StringComparison.Ordinal))
                .Select(e => Get(e, "Command").ToString()).SequenceEqual(new[] { "BlueprintSubstitutionConfigure", "BlueprintSubstitutionOn", "BlueprintSubstitutionOff" }),
                "the real home row retains configure/on/off and its one common keyboard target");
            int mainGeneration = (int)Get(layout, "Generation"), mainMeasurements = (int)Get(layout, "MeasurementCount");
            var on = elements.Single(e => Get(e, "Command").ToString() == "BlueprintSubstitutionOn");
            Reveal(state, on); Sample(state, on, true); float mainScroll = (float)Get(state, "Scroll");
            Call(shell, "OpenBlueprintReplacementConfiguration"); Sample(state, on, false);
            Require(Get(state, "Command").ToString() == "None" && (bool)Get(state, "ConsumeLeft") && (bool)Get(state, "Visible") &&
                (float)Get(state, "Scroll") == mainScroll && (int)Get(layout, "Generation") == mainGeneration,
                "popup handoff retires an old main control without changing the visible home, its scroll or the original physical release tail");
            var cells = PopupCells(popup);
            Require((bool)Get(popup, "Visible") && cells.Length == 12 && (int)Get(popup, "Columns") == 4 && (int)Get(popup, "VisibleRows") == 3 &&
                cells.Select(e => (string)Get(e, "Text")).SequenceEqual(Names) && Objects(Get(layout, "Elements")).SequenceEqual(elements),
                "independent popup shows all twelve ordered names in a compact four-by-three grid while the home elements remain intact");
            Require(cells.Select(e => PopupHint(popup, e)).Where(h => h != null).SequenceEqual(new[] { "王朝桌除外", "云/微光平台除外", "仅安全墙" }),
                "actual popup hit targets supply exactly the three approved hints");
            AssertPopupBounds(popup, new Vector2(1280, 720), Matrix.CreateScale(1.5f, 1.5f, 1f));
            int popupGeneration = (int)Get(popup, "Generation"), popupMeasurements = (int)Get(popup, "MeasurementCount");
            Require(popupMeasurements == 13 && (int)Get(layout, "MeasurementCount") - mainMeasurements <= 13,
                "cold popup preparation measures its title and twelve labels once through the shared fixed-text cache");
            mainMeasurements = (int)Get(layout, "MeasurementCount");
            for (int i = 0; i < 30; i++) Call(shell, "PrepareBlueprintReplacement", Matrix.CreateScale(1.5f, 1.5f, 1f), new Vector2(1280, 720), false);
            Require((int)Get(popup, "Generation") == popupGeneration && (int)Get(popup, "MeasurementCount") == popupMeasurements &&
                (int)Get(layout, "Generation") == mainGeneration && (int)Get(layout, "MeasurementCount") == mainMeasurements,
                "stable overlay preparations remeasure neither its thirteen fixed labels nor the unchanged home page");
            Call(shell, "CloseBlueprintReplacementConfiguration");

            var popupType = popup.GetType(); var constructor = popupType.GetConstructors(F).Single();
            var metricType = constructor.GetParameters()[1].ParameterType;
            var tall = Activator.CreateInstance(popupType, F, null, new object[] { host, TallMeasure(assembly, metricType) }, null);
            object anchor = Rect(assembly, 500, 80, 48, 32); var matrix = Matrix.CreateScale(1.5f, 1.5f, 1f);
            var screen = new Vector2(1280, 360); // 240 logical: within the real F5 minimum.
            Action prepare = () => PreparePopup(tall, matrix, screen, anchor, renderer);
            Call(tall, "Open", anchor); prepare();
            Require((bool)Get(tall, "Visible") && (int)Get(tall, "MaxFirstRow") > 0, "legal short viewport and tall category-only glyphs use the popup's own bounded row scrolling");
            foreach (var cell in PopupCells(tall))
            {
                var rect = Get(cell, "Rect"); var size = Get(cell, "TextSize");
                Require((float)Get(size, "Height") >= 41.99f && H(rect) >= (float)Get(size, "Height") + 16 && W(rect) >= (float)Get(size, "Width") + 34,
                    "tall category glyphs keep readable label padding and a separate checkmark corner without shrinking navigation or text");
            }
            var point = Center(Get(PopupCells(tall)[0], "Rect"));
            PopupFrame(tall, input, point, false, matrix, screen);
            for (int i = 0; i < 20; i++) PopupFrame(tall, input, point, false, matrix, screen, wheel: -120);
            AssertPopupBounds(tall, screen, matrix);
            var last = PopupCells(tall).Last();
            Require((int)Get(tall, "FirstRow") == (int)Get(tall, "MaxFirstRow") && (string)Get(last, "Text") == "墙" &&
                Y(Get(last, "Rect")) + H(Get(last, "Rect")) <= Y(Get(tall, "Body")) + H(Get(tall, "Body")),
                "scroll clamps at the complete final wall cell, not a partial button or a main-page offset");
            int beforeLast = preferences.Snapshot.Value.ReplacementCategories;
            point = Center(Get(last, "Rect")); PopupFrame(tall, input, point, false, matrix, screen); PopupFrame(tall, input, point, true, matrix, screen); PopupFrame(tall, input, point, false, matrix, screen);
            Require(preferences.Snapshot.Value.ReplacementCategories == (beforeLast ^ 2048) && (bool)Get(tall, "ConsumeLeft"), "the last complete tall-font category executes a real Host preference toggle on matching release");
            Call(host, "SetReplacementCategories", beforeLast); Call(tall, "Close");
            Call(tall, "Open", anchor); prepare(); point = Center(Get(PopupCells(tall)[0], "Rect"));
            int beforeWheelRelease = preferences.Snapshot.Value.ReplacementCategories;
            PopupFrame(tall, input, point, false, matrix, screen); PopupFrame(tall, input, point, true, matrix, screen);
            PopupFrame(tall, input, point, false, matrix, screen, wheel: -120);
            Require(preferences.Snapshot.Value.ReplacementCategories == beforeWheelRelease && (bool)Get(tall, "ConsumeLeft") && (bool)Get(tall, "ConsumeWheel"),
                "a real wheel and release in the same popup sample scrolls without executing the old pressed category");
            for (int i = 0; i < 20; i++) PopupFrame(tall, input, point, false, matrix, screen, wheel: 120);
            Require((int)Get(tall, "FirstRow") == 0 && (string)Get(PopupCells(tall)[0], "Text") == "工作台", "reverse wheel clamps to the first complete popup row");
            PopupFrame(tall, input, point, false, matrix, screen, right: true); Call(tall, "Close");
            PopupFrame(tall, input, point, false, matrix, screen);
            Require((bool)Get(tall, "ConsumeRight") && preferences.Snapshot.Value.ReplacementCategories == beforeWheelRelease, "closed popup retains its right-button release without treating it as a category edit");
            foreach (string change in new[] { "screen", "scale", "font", "skin", "anchor", "source-generation", "scroll", "revision", "close", "focus" })
            {
                Call(tall, "Open", anchor); prepare();
                point = Center(Get(PopupCells(tall)[0], "Rect"));
                PopupFrame(tall, input, point, false, matrix, screen); PopupFrame(tall, input, point, true, matrix, screen);
                int before = preferences.Snapshot.Value.ReplacementCategories;
                var releaseScreen = screen; var releaseMatrix = matrix;
                if (change == "screen") releaseScreen = new Vector2(1280, 390);
                else if (change == "scale") releaseMatrix = Matrix.Identity;
                else if (change == "font") PreparePopup(tall, matrix, screen, anchor, renderer, font: new object());
                else if (change == "skin") PreparePopup(tall, matrix, screen, anchor, renderer, skinDelta: 1);
                else if (change == "anchor") PreparePopup(tall, matrix, screen, Call(anchor, "Offset", -16f, 4f), renderer);
                else if (change == "source-generation") PreparePopup(tall, matrix, screen, anchor, renderer, generation: 1);
                else if (change == "scroll") PopupFrame(tall, input, point, true, matrix, screen, wheel: -120);
                else if (change == "revision") Call(host, "SetToolbar", !preferences.Snapshot.Value.GelToolbar);
                else if (change == "close") Call(tall, "Close");
                else PopupFrame(tall, input, point, false, matrix, screen, focused: false);
                PopupFrame(tall, input, point, false, releaseMatrix, releaseScreen);
                Require(preferences.Snapshot.Value.ReplacementCategories == before && (bool)Get(tall, "ConsumeLeft"),
                    "popup invalidated " + change + " cannot apply the held category or surrender its trusted release tail");
                PopupFrame(tall, input, point, false, matrix, screen);
                Require(!(bool)Get(tall, "Captured"), "neutral sample ends only the popup's completed physical tail: " + change);
                Call(tall, "Close");
            }
        }
        private static void CommandsAndBinding(object context, object host, object shell, object renderer, object controls,
            PreferenceDocument<BlueprintPreferences> preferences, object input)
        {
            var state = Get(shell, "State"); var layout = Get(state, "Layout"); var display = Get(context, "ShortFeedback");
            Main.screenWidth = 1280; Main.screenHeight = 720; NativeBlueprintUiChecks.SetScale(1.5f); PlayerInput.CacheOriginalScreenDimensions();
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            Set(state, "Ready", true); Call(state, "Navigate", 6); Call(state, "RestoreVisible");
            var popup = Get(shell, "BlueprintReplacementPopup");
            PhysicalFrame(input, shell, Vector2.Zero, false);
            var configure = Objects(Get(layout, "Elements")).Single(e => Get(e, "Command").ToString() == "BlueprintSubstitutionConfigure");
            Reveal(state, configure); var configurePoint = MainPoint(state, configure);
            PhysicalFrame(input, shell, configurePoint, false); PhysicalFrame(input, shell, configurePoint, true); PhysicalFrame(input, shell, configurePoint, false);
            Require((bool)Get(popup, "Visible") && (bool)Get(state, "Visible") && (int)Get(state, "Page") == 6 && PopupCells(popup).Length == 12,
                "actual main-row physical configure release opens the separate twelve-category popup without replacing the home page");
            int before = preferences.Snapshot.Value.ReplacementCategories;
            var point = Center(Get(PopupCells(popup)[1], "Rect"));
            Call(display, "Clear");
            PhysicalFrame(input, shell, point, false); PhysicalFrame(input, shell, point, true); PhysicalFrame(input, shell, point, false);
            Require(preferences.Snapshot.Value.ReplacementCategories == (before ^ 2) && !preferences.Snapshot.Value.SameTypeReplacement &&
                (int)Get(display, "Count") == 0 && (bool)Get(popup, "ConsumeLeft") && !Main.mouseLeft,
                "actual popup physical category release edits the unique owner while total is off, silently, and consumes its native tail");
            int generation = (int)Get(layout, "Generation"), measured = (int)Get(layout, "MeasurementCount");
            int popupGeneration = (int)Get(popup, "Generation"), popupMeasured = (int)Get(popup, "MeasurementCount");
            Call(shell, "PrepareBlueprintReplacement", Matrix.CreateScale(1.5f, 1.5f, 1f), new Vector2(1280, 720), false);
            Require(generation == (int)Get(layout, "Generation") && measured == (int)Get(layout, "MeasurementCount") &&
                popupGeneration == (int)Get(popup, "Generation") && popupMeasured == (int)Get(popup, "MeasurementCount"),
                "preference selection changes project checkmarks without rebuilding popup or home geometry");
            foreach (bool enabled in new[] { true, false })
            {
                var button = Objects(Get(layout, "Elements")).Single(e => Get(e, "Command").ToString() == (enabled ? "BlueprintSubstitutionOn" : "BlueprintSubstitutionOff"));
                point = MainPoint(state, button);
                Require(!(bool)Call(popup, "Contains", point.X, point.Y), "anchored popup leaves the main on/off group reachable");
                PhysicalFrame(input, shell, point, false); PhysicalFrame(input, shell, point, true); PhysicalFrame(input, shell, point, false);
                Require(preferences.Snapshot.Value.SameTypeReplacement == enabled && preferences.Snapshot.Value.ReplacementCategories == (before ^ 2) &&
                    (bool)Get(popup, "Visible") && (int)Get(display, "Count") == 0,
                    "real main on/off controls remain independent, preserve categories and leave the separate popup open");
            }
            point = MainPoint(state, configure);
            PhysicalFrame(input, shell, point, false); PhysicalFrame(input, shell, point, true); PhysicalFrame(input, shell, point, false);
            Require(!(bool)Get(popup, "Visible") && (bool)Get(state, "Visible"), "a second real configure click closes only the existing popup");
            PhysicalFrame(input, shell, point, false); PhysicalFrame(input, shell, point, true); PhysicalFrame(input, shell, point, false);
            Require((bool)Get(popup, "Visible"), "a fresh configure gesture reopens the same sole popup owner");
            var titlePoint = new Vector2((float)Get(state, "X") + 20, (float)Get(state, "Y") + 12);
            int beforeDrag = (int)Get(popup, "Generation");
            PhysicalFrame(input, shell, titlePoint, false); PhysicalFrame(input, shell, titlePoint, true);
            PhysicalFrame(input, shell, titlePoint + new Vector2(16, 0), true); PhysicalFrame(input, shell, titlePoint + new Vector2(16, 0), false);
            Call(shell, "PrepareBlueprintReplacement", Matrix.CreateScale(1.5f, 1.5f, 1f), new Vector2(1280, 720), false);
            Require((bool)Get(popup, "Visible") && (int)Get(popup, "Generation") > beforeDrag, "real main-window drag reprojects its anchored popup and retires old cell geometry");
            Call(shell, "CloseBlueprintReplacementConfiguration"); Call(renderer, "Prepare", state, 1280f, 720f, 1.5f);
            var key = Objects(Get(layout, "Elements")).Single(e => (string)GetOptional(e, "HotkeyTarget") == ActionId);
            Call(shell, "OpenHotkey", ActionId, Get(key, "Rect")); Call(shell, "OpenHotkey", ActionId, Get(key, "Rect"));
            Require((bool)Get(Get(shell, "HotkeyPopup"), "Visible") && (string)Get(Get(shell, "HotkeyPopup"), "Target") == ActionId &&
                !(bool)Get(popup, "Visible"), "the actual common double-click binding popup owns this action and excludes the category popup");
            Call(Get(shell, "HotkeyPopup"), "Close");
            var registry = (HotkeyRegistry)Get(Get(shell, "hotkeys"), "Registry");
            Require(registry.Find(ActionId) != null && registry.Find(ActionId).CanConfigure, "the row's stable identity is truly registered/configurable");
            var storage = new BindingStorage();
            using (var first = new HotkeyBindings(registry, storage))
            {
                Wait(first, () => first.Loaded); Require(first.Get(ActionId) == null, "new action is initially unbound");
                HotkeyChord chord; string reason; long command;
                Require(HotkeyChord.TryParse("F19", out chord, out reason) && first.TrySet(ActionId, chord, null, out command, out reason), "common binder accepts the new action/chord");
                Wait(first, () => !first.Busy); Require(first.CompletionSucceeded, "binding becomes active only after reliable acknowledgement");
            }
            using (var bindings = new HotkeyBindings(registry, new BindingStorage(storage.Snapshot)))
            {
                Wait(bindings, () => bindings.Loaded);
                Require(bindings.Get(ActionId)?.Text == "F19", "a fresh common binding owner reopens the acknowledged exact action/chord");
                Call(shell, "CloseAndSubmitPosition");
                int mask = preferences.Snapshot.Value.ReplacementCategories;
                Call(display, "Clear"); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, new[] { Keys.F19 });
                var physical = (HotkeyInput)Get(input, "Hotkeys"); var vetoed = preferences.Snapshot.Value;
                Require(physical.IsNew((int)Keys.F19), "explicit dispatch-veto counterexample has a real new saved primary");
                bindings.Dispatch(physical, HotkeyContext.SinglePlayer, false);
                Require(preferences.Snapshot.Value.Equals(vetoed) && (int)Get(display, "Count") == 0,
                    "the real coarse dispatch veto admits neither the saved toggle nor a success acknowledgement");
                foreach (bool expected in new[] { true, false })
                {
                    Call(display, "Clear"); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, new[] { Keys.F19 });
                    physical = (HotkeyInput)Get(input, "Hotkeys");
                    bool permitted = (bool)Get(input, "CanStartActions"), canPresent = (bool)Get(input, "CanPrepareText");
                    Require(permitted && canPresent && physical.IsNew((int)Keys.F19) && !physical.IsSuppressed((int)Keys.F19) &&
                        !(bool)Get(state, "Visible") && !(bool)Get(shell, "OwnsPointer"),
                        "saved-binding input context: CanStartActions=" + permitted + " CanPrepareText=" + canPresent +
                        " Reliable=" + physical.Reliable + " NewF19=" + physical.IsNew((int)Keys.F19) + " SuppressedF19=" + physical.IsSuppressed((int)Keys.F19));
                    // The third argument is an execution permission. Passing
                    // false without an admit delegate vetoes every action.
                    bindings.Dispatch(physical, HotkeyContext.SinglePlayer, permitted);
                    Require(preferences.Snapshot.Value.SameTypeReplacement == expected,
                        "saved physical binding applies the real Host toggle: expected=" + expected + " actual=" + preferences.Snapshot.Value.SameTypeReplacement);
                    Require(preferences.Snapshot.Value.ReplacementCategories == mask,
                        "saved physical toggle retains categories: expected=" + mask + " actual=" + preferences.Snapshot.Value.ReplacementCategories);
                    string message = "同类替换 " + (expected ? "已开启" : "已关闭");
                    Require(NativeShortFeedbackChecks.Has(display, message), "real Immediate feedback: expected=" + message + " actual=" +
                        string.Join(" | ", NativeShortFeedbackChecks.Entries(display).Select(e => (string)Get(e, "Text"))));
                }
                Call(display, "Clear"); Call(host, "SetSameTypeReplacement", true);
                Require((int)Get(display, "Count") == 0, "ordinary F5/domain setting does not manufacture a hotkey acknowledgement");
                Call(host, "SetSameTypeReplacement", false);
            }
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(display, "Clear");
            Set(state, "Ready", true); Call(state, "Navigate", 6); Call(state, "RestoreVisible"); Call(shell, "OpenBlueprintReplacementConfiguration");
            Call(host, "OpenLibrary");
            Require(!(bool)Get(popup, "Visible") && (bool)Get(Get(shell, "BlueprintUi"), "Submenu"), "real library command closes the category popup before taking its presentation owner");
            Require((bool)Call(Get(shell, "BlueprintUi"), "RequestLeave"), "ordinary library view can return without an edit transaction");
            Call(shell, "OpenBlueprintReplacementConfiguration");
            Call(host, "OpenMaterials"); Require(!(bool)Get(popup, "Visible") && (bool)Get(Get(shell, "BlueprintMaterialsUi"), "Visible"),
                "actual materials command closes the category popup without a second pointer owner");
            Call(Get(shell, "BlueprintMaterialsUi"), "Close"); Call(shell, "OpenBlueprintReplacementConfiguration");
            point = Center(Get(popup, "CloseRect")); int beforeClose = preferences.Snapshot.Value.ReplacementCategories;
            PhysicalFrame(input, shell, point, false); PhysicalFrame(input, shell, point, true); PhysicalFrame(input, shell, point, false);
            Require(!(bool)Get(popup, "Visible") && (bool)Get(state, "Visible") && (bool)Get(popup, "ConsumeLeft") && preferences.Snapshot.Value.ReplacementCategories == beforeClose,
                "real Recovery-style close cross closes only its popup and consumes the matching release");
            PhysicalFrame(input, shell, Vector2.Zero, false); Call(shell, "OpenBlueprintReplacementConfiguration");
            PhysicalFrame(input, shell, Vector2.Zero, false); PhysicalFrame(input, shell, Vector2.Zero, false, new[] { Keys.Escape });
            Require(!(bool)Get(popup, "Visible") && (bool)Get(state, "Visible"), "an independent real Escape closes only the category popup and keeps the blueprint home visible");
            PhysicalFrame(input, shell, Vector2.Zero, false); Call(shell, "OpenBlueprintReplacementConfiguration");
            point = Center(Get(PopupCells(popup)[0], "Rect")); beforeClose = preferences.Snapshot.Value.ReplacementCategories;
            PhysicalFrame(input, shell, point, false); PhysicalFrame(input, shell, point, true);
            Call(shell, "CloseAndSubmitPosition");
            PhysicalFrame(input, shell, point, true); PhysicalFrame(input, shell, point, false);
            Require(!(bool)Get(popup, "Visible") && (bool)Get(popup, "ConsumeLeft") && !Main.mouseLeft && preferences.Snapshot.Value.ReplacementCategories == beforeClose,
                "normal close while a category is held preserves its real release owner and never writes the category or leaks native Mouse1");
            PhysicalFrame(input, shell, point, false);
            Require(!(bool)Get(popup, "Captured") && !(bool)Get(shell, "OwnsPointer"), "trusted neutral after closed-popup release relinquishes the complete tail");
            Set(state, "Ready", true); Call(state, "Navigate", 6); Call(state, "RestoreVisible"); Call(shell, "OpenBlueprintReplacementConfiguration");
            Call(state, "Navigate", 9); PhysicalFrame(input, shell, Vector2.Zero, false);
            Require(!(bool)Get(popup, "Visible"), "page departure closes the independent popup instead of leaving a second live overlay");
            Call(shell, "CloseAndSubmitPosition");
        }

        internal static void Visual(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State"); var renderer = Get(shell, "renderer"); var layout = Get(state, "Layout");
            var popup = Get(shell, "BlueprintReplacementPopup"); var input = Get(context, "Input");
            var preference = (PreferenceDocument<BlueprintPreferences>)Get(host, "preference");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return preference.Snapshot.IsLoaded; });
            var original = preference.Snapshot.Value; float oldScale = Main.UIScale; int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight;
            try
            {
                Call(shell, "CloseAndSubmitPosition"); Call(host, "SetReplacementCategories", 4095);
                foreach (float scale in new[] { 1f, 1.5f })
                {
                    Main.screenWidth = 1280; Main.screenHeight = scale == 1f ? 900 : 720; PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = scale;
                    float nativeEffective = Main.UIScale, nativeMatrixX = Main.UIScaleMatrix.M11, nativeMatrixY = Main.UIScaleMatrix.M22;
                    bool presentationFixture = scale == 1.5f;
                    // The native setter clamps this short viewport. Preserve
                    // its effective sample, then supply the existing explicit
                    // presentation fixture for actual requested-scale Draw.
                    if (presentationFixture) NativeBlueprintUiChecks.SetScale(scale);
                    var expectedMatrix = Matrix.CreateScale(scale, scale, 1f);
                    string scaleSource = presentationFixture ? "presentation-fixture" : "native-setter";
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "BLUEPRINT SUBSTITUTION SCALE source={0} viewport=1280x{1} requested={2:R} native-effective={3:R} native-matrix={4:R},{5:R} presentation-effective={6:R} presentation-matrix={7:R},{8:R}",
                        scaleSource, Main.screenHeight, scale, nativeEffective, nativeMatrixX, nativeMatrixY, Main.UIScale, Main.UIScaleMatrix.M11, Main.UIScaleMatrix.M22));
                    Require(Main.UIScale == scale && Main.UIScaleMatrix == expectedMatrix,
                        "actual substitution Draw uses the requested Main.UIScale and full UI matrix: " + scaleSource);
                    Set(state, "Ready", true); Call(state, "Navigate", 6); Call(state, "RestoreVisible");
                    Call(shell, "RefreshBlueprintPageState");
                    Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, (float)Main.screenHeight, Main.UIScaleMatrix.M11);
                    Sample(state, null, false, width: 1280, height: Main.screenHeight, scale: Main.UIScaleMatrix.M11);
                    var main = Objects(Get(layout, "Elements")).Single(e => Get(e, "Command").ToString() == "BlueprintSubstitutionConfigure");
                    Reveal(state, main); Sample(state, main, false, width: 1280, height: Main.screenHeight, scale: Main.UIScaleMatrix.M11);
                    Action draw = () => { Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false); Call(popup, "Draw"); Call(renderer, "DrawHints", state, Main.UIScaleMatrix, null, false, false); };
                    string suffix = presentationFixture ? "short-presentation-fixture-150" : "normal-native-setter-100";
                    Directory.CreateDirectory(output); graphics.Image(Path.Combine(output, "blueprint-substitution-main-" + suffix + ".png"), draw, Main.UIScaleMatrix, 1280, Main.screenHeight);
                    Call(shell, "OpenBlueprintReplacementConfiguration");
                    Call(shell, "PrepareBlueprintReplacement", Main.UIScaleMatrix, new Vector2(1280, Main.screenHeight), true);
                    Require((bool)Get(popup, "Visible") && PopupCells(popup).Select(e => (string)Get(e, "Text")).SequenceEqual(Names) &&
                        (int)Get(popup, "Columns") == 4 && (int)Get(popup, "VisibleRows") == 3 && (int)Get(popup, "MaxFirstRow") == 0 &&
                        Objects(Get(layout, "Elements")).Any(e => (string)GetOptional(e, "Text") == "自动建造"),
                        "actual main remains visible beneath the separate compact four-by-three configuration popup");
                    AssertPopupBounds(popup, new Vector2(1280, Main.screenHeight), Main.UIScaleMatrix);
                    int generation = (int)Get(popup, "Generation"), count = (int)Get(popup, "MeasurementCount");
                    int homeGeneration = (int)Get(layout, "Generation"), homeCount = (int)Get(layout, "MeasurementCount");
                    PhysicalFrame(input, shell, Vector2.Zero, false);
                    var pixels = graphics.Pixels(draw, Main.UIScaleMatrix, 1280, Main.screenHeight);
                    foreach (var cell in PopupCells(popup))
                        Require(SelectedPixels(pixels, Get(cell, "Rect"), Main.UIScaleMatrix, 1280, Main.screenHeight), "each actual category card renders the Recovery-style light-green checkmark on the original XNA device: " + Get(cell, "Text"));
                    graphics.Image(Path.Combine(output, "blueprint-substitution-popup-" + suffix + ".png"), draw, Main.UIScaleMatrix, 1280, Main.screenHeight);
                    foreach (int category in new[] { 1, 10, 11 })
                    {
                        var cell = PopupCells(popup)[category]; var point = Center(Get(cell, "Rect"));
                        PhysicalFrame(input, shell, point, false);
                        graphics.Image(Path.Combine(output, "blueprint-substitution-popup-" + suffix + "-hint-" + category + ".png"), draw, Main.UIScaleMatrix, 1280, Main.screenHeight);
                        string shown = string.Concat(Objects(Get(Get(renderer, "HintLayout"), "Lines")).Select(e => (string)Get(e, "Text")));
                        string expected = category == 1 ? "王朝桌除外" : category == 10 ? "云/微光平台除外" : "仅安全墙";
                        Require((bool)Get(Get(renderer, "HintLayout"), "Visible") && shown == expected, "actual independent popup hover draws the exact category hint: " + expected);
                    }
                    Require(generation == (int)Get(popup, "Generation") && count == (int)Get(popup, "MeasurementCount") &&
                        homeGeneration == (int)Get(layout, "Generation") && homeCount == (int)Get(layout, "MeasurementCount") && !(bool)Get(shell, "Failed"),
                        "real popup/checkmark/hint Draws retain both fixed layout caches and the healthy shell");
                    Require(Main.UIScale == scale && Main.UIScaleMatrix == expectedMatrix, "actual main/popup Draws retain the requested presentation scale and full UI matrix");
                    Call(shell, "CloseAndSubmitPosition");
                }
                Console.WriteLine("PASS Blueprint substitution F5 GPU: actual retained home and independent Recovery-style four-by-three popup Draw/checkmarks/three hints at 1280x900/100% native setter and 1280x720/150% explicit presentation fixture, requested/native-effective/presentation-effective scale samples, complete twelve-category popup snapshots, stable page metrics. Hidden isolated XNA; OS/IME/FPS/multiplayer/owner acceptance NOT_EXECUTED.");
            }
            finally
            {
                Call(shell, "CloseAndSubmitPosition"); preference.Set(original); Call(host, "Poll");
                Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = oldScale;
                NativeBlueprintUiChecks.SetScale(oldScale);
            }
        }

        private static object[] Objects(object value) { return ((IEnumerable)value).Cast<object>().ToArray(); }
        private static object[] PopupCells(object popup) { return Objects(Get(popup, "Cells")); }
        private static Vector2 Center(object rect) { return new Vector2(X(rect) + W(rect) / 2, Y(rect) + H(rect) / 2); }
        private static Vector2 MainPoint(object state, object element)
        {
            var rect = Get(element, "Rect"); var view = Get(Get(state, "Layout"), "Viewport");
            return Center(rect) + new Vector2((float)Get(state, "X") + X(view), (float)Get(state, "Y") + Y(view) - (float)Get(state, "Scroll"));
        }
        private static object Rect(Assembly assembly, float x, float y, float width, float height)
        { return Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect", true), F, null, new object[] { x, y, width, height }, null); }
        private static string PopupHint(object popup, object cell)
        {
            var rect = Get(cell, "Rect"); var point = Center(rect);
            return Call(popup, "Hint", point.X, point.Y, Activator.CreateInstance(rect.GetType())) as string;
        }
        private static void PreparePopup(object popup, Matrix matrix, Vector2 screen, object anchor, object renderer, int generation = 0, object font = null, int skinDelta = 0)
        { Call(popup, "Prepare", matrix, screen, anchor, font ?? Get(renderer, "FontIdentity"), (int)Get(renderer, "SkinGeneration") + skinDelta, generation, false); }
        private static void PopupFrame(object popup, object input, Vector2 point, bool left, Matrix matrix, Vector2 screen, bool focused = true, int wheel = 0, bool right = false)
        {
            NativeQuickGestureChecks.Sample(input, (left ? 1 : 0) | (right ? 2 : 0));
            Call(popup, "Process", true, new KeyboardState(), point, matrix, screen, focused, wheel);
        }
        private static void AssertPopupBounds(object popup, Vector2 screen, Matrix matrix)
        {
            var panel = Get(popup, "Panel"); var body = Get(popup, "Body"); var close = Get(popup, "CloseRect");
            Require(X(panel) >= 12 && Y(panel) >= 12 && X(panel) + W(panel) <= screen.X / matrix.M11 - 11.99f &&
                Y(panel) + H(panel) <= screen.Y / matrix.M22 - 11.99f && W(close) == 30 && H(close) == 30,
                "Recovery-style independent panel and full close hitbox fit the actual logical viewport");
            foreach (var cell in PopupCells(popup))
            {
                var rect = Get(cell, "Rect");
                Require(X(rect) >= X(body) && X(rect) + W(rect) <= X(body) + W(body) && Y(rect) >= Y(body) && Y(rect) + H(rect) <= Y(body) + H(body),
                    "each displayed category keeps its complete drawable and clickable surface inside the popup body");
            }
        }
        private static bool SelectedPixels(Color[] pixels, object rect, Matrix matrix, int width, int height)
        {
            int left = Math.Max(0, (int)Math.Floor((X(rect) + W(rect) - 17) * matrix.M11));
            int right = Math.Min(width, (int)Math.Ceiling((X(rect) + W(rect) - 1) * matrix.M11));
            int top = Math.Max(0, (int)Math.Floor((Y(rect) + 1) * matrix.M22));
            int bottom = Math.Min(height, (int)Math.Ceiling((Y(rect) + 13) * matrix.M22));
            for (int y = top; y < bottom; y++) for (int x = left; x < right; x++) if (pixels[y * width + x] == Color.LightGreen) return true;
            return false;
        }
        private static void Reveal(object state, object element) { Call(state, "ScrollTo", Math.Max(0f, Y(Get(element, "Rect")) - 8)); }
        private static void Sample(object state, object element, bool left, int wheel = 0, float width = 1280, float height = 720, float scale = 1.5f)
        {
            object sample = Activator.CreateInstance(state.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Input", true));
            Set(sample, "Width", width); Set(sample, "Height", height); Set(sample, "Scale", scale); Set(sample, "Active", true); Set(sample, "Focused", true); Set(sample, "Left", left); Set(sample, "Wheel", wheel);
            if (element != null)
            {
                var view = Get(Get(state, "Layout"), "Viewport"); var rect = Get(element, "Rect");
                Set(sample, "X", (float)Get(state, "X") + X(view) + X(rect) + 3);
                Set(sample, "Y", (float)Get(state, "Y") + Y(view) + Y(rect) - (float)Get(state, "Scroll") + 3);
            }
            Call(state, "Update", sample);
        }
        private static Delegate TallMeasure(Assembly assembly, Type type)
        {
            var size = assembly.GetType("JueMingR.TerrariaHost.F5.F5Size", true);
            // Only category labels are tall. Navigation retains its legal
            // metrics; the popup must grow cells and scroll complete rows.
            Func<string, float, object> values = (text, scale) => Activator.CreateInstance(size, F, null,
                new object[] { text.Length * 10f * scale, (Names.Contains(text, StringComparer.Ordinal) ? 60f : 16f) * scale, 0f, 0f }, null);
            var textParameter = Expression.Parameter(typeof(string)); var scaleParameter = Expression.Parameter(typeof(float));
            return Expression.Lambda(type, Expression.Convert(Expression.Invoke(Expression.Constant(values), textParameter, scaleParameter), size), textParameter, scaleParameter).Compile();
        }
        private static void Wait(HotkeyBindings bindings, Func<bool> complete) { NativeQuickItemChecks.Until(() => { bindings.Poll(); return complete(); }); }
        private static void PhysicalFrame(object input, object shell, Vector2 point, bool left, Keys[] keys = null)
        {
            int x = (int)Math.Round(point.X * Main.UIScaleMatrix.M11), y = (int)Math.Round(point.Y * Main.UIScaleMatrix.M22);
            Main.LocalPlayer.mouseInterface = Main.mouseText = false; PlayerInput.RawMouseScale = Vector2.One;
            PlayerInput.MouseInfo = new MouseState(x, y, 0, left ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Main.mouseX = PlayerInput.MouseX = x; Main.mouseY = PlayerInput.MouseY = y; Main.mouseLeft = left; Main.mouseRight = false;
            Call(input, "BeginUpdate"); Call(shell, "BeforeInput");
            Call(input, "AfterNativeMouse", new List<string>(left ? new[] { "Mouse1" } : Array.Empty<string>()));
            PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Update();
            PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 0;
            Call(input, "AfterMapping"); Main.keyState = new KeyboardState(keys ?? Array.Empty<Keys>()); Call(input, "AfterKeyboardRefresh"); Call(shell, "ProcessInput");
            Require(!(bool)Get(shell, "Failed"), "actual substitution Shell.ProcessInput remains healthy");
        }
        private sealed class BindingStorage : IPreferenceStorage
        {
            private byte[] bytes; private int revision;
            internal BindingStorage(byte[] initial = null) { bytes = initial == null ? null : (byte[])initial.Clone(); }
            internal byte[] Snapshot { get { return bytes == null ? null : (byte[])bytes.Clone(); } }
            public PreferenceReadResult Read() { return new PreferenceReadResult(bytes == null ? PreferenceReadStatus.Missing : PreferenceReadStatus.Loaded, bytes, revision.ToString(), null); }
            public PreferenceWriteResult Write(string expectedIdentity, byte[] contents)
            {
                if (expectedIdentity != revision.ToString()) return new PreferenceWriteResult(PreferenceWriteStatus.Conflict, null, "isolated binding changed", false, true);
                bytes = (byte[])contents.Clone(); return new PreferenceWriteResult(PreferenceWriteStatus.Saved, (++revision).ToString(), null);
            }
            public void Dispose() { }
        }
        private static float X(object rect) { return (float)Get(rect, "X"); }
        private static float Y(object rect) { return (float)Get(rect, "Y"); }
        private static float W(object rect) { return (float)Get(rect, "Width"); }
        private static float H(object rect) { return (float)Get(rect, "Height"); }
    }
}
