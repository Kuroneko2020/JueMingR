using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintMaterialsDragChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void DuringPublication(object context, ProbeGraphics graphics, Action tick, Action finishOneTarget)
        {
            var shell = Get(context, "Shell"); var input = Get(context, "Input"); var popup = Get(shell, "BlueprintMaterialsUi");
            Action<bool, string> check = (valid, reason) => Check(valid, reason, popup, input);
            var title = Objects(Get(popup, "labels")).First(e => (string)Get(e, "Text") == "施工材料"); var rect = Get(title, "Rect");
            float x = X(rect) + W(rect) / 2, y = Y(rect) + H(rect) / 2;
            NativeBlueprintMaterialsUiChecks.Frame(shell, input, x, y, false);
            NativeBlueprintMaterialsUiChecks.Frame(shell, input, x, y, true);
            NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
            var panel = Get(popup, "Panel"); var body = Get(popup, "Body");
            Console.WriteLine("MATERIAL DRAG publication-start " + PublicationState(popup, input));
            int material = Main.LocalPlayer.inventory[0].type; Main.LocalPlayer.inventory[0].stack = 12;
            tick(); NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
            check((bool)Get(popup, "Dragging") && ((JueMingR.Features.Blueprint.BlueprintMaterialSnapshot)Get(popup, "snapshot")).Status == JueMingR.Features.Blueprint.BlueprintMaterialStatus.Pending,
                "a real inventory publication continues while a title drag holds the same opening");
            check(Same(panel, Get(popup, "Panel")) && Same(body, Get(popup, "Body")), "Pending freezes the grabbed frame and all body-local geometry");
            NativeQuickItemChecks.Until(() =>
            {
                tick(); NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
                return ((JueMingR.Features.Blueprint.BlueprintMaterialSnapshot)Get(popup, "snapshot")).Status == JueMingR.Features.Blueprint.BlueprintMaterialStatus.Exact;
            });
            var value = (JueMingR.Features.Blueprint.BlueprintMaterialSnapshot)Get(popup, "snapshot");
            check((bool)Get(popup, "Dragging") && value.Rows.Single(r => r.ItemType == material).Held == 12 && Same(panel, Get(popup, "Panel")) && Same(body, Get(popup, "Body")),
                "the new exact quantity reaches the grabbed window without freezing business or shifting local frame geometry");
            finishOneTarget();
            string priorState = null; int stateTraces = 0;
            NativeQuickItemChecks.Until(() =>
            {
                tick(); NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
                value = (JueMingR.Features.Blueprint.BlueprintMaterialSnapshot)Get(popup, "snapshot");
                string state = PublicationState(popup, input);
                if (state != priorState && stateTraces < 16) { Console.WriteLine("MATERIAL DRAG world-step " + state); stateTraces++; }
                priorState = state;
                return value.Status == JueMingR.Features.Blueprint.BlueprintMaterialStatus.Exact && value.Rows.Count == 69;
            });
            check((bool)Get(popup, "Dragging") && (int)Get(Get(popup, "Content"), "Count") == 69 && Same(panel, Get(popup, "Panel")) && Same(body, Get(popup, "Body")),
                "a real completed material leaves the live set during dragging while the frame and local body stay frozen");
            NativeBlueprintMaterialsUiChecks.Frame(shell, input, x, y, false); PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
            NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
            check(!(bool)Get(popup, "Dragging") && !Main.LocalPlayer.controlUseItem, "publication drag release remains consumed before original world input");
        }
        internal static void Run(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var popup = Get(shell, "BlueprintMaterialsUi");
            var input = Get(context, "Input"); var state = Get(shell, "State"); var player = Main.LocalPlayer;
            var foreground = (Func<IntPtr>)Get(input, "foregroundWindow"); float scale = Main.UIScaleWanted;
            int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight;
            float shellX = (float)Get(state, "X"), shellY = (float)Get(state, "Y");
            Action<bool, string> check = (valid, reason) => Check(valid, reason, popup, input);
            Action prepare = () => NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
            Action<float, float, bool> frame = (x, y, left) => NativeBlueprintMaterialsUiChecks.Frame(shell, input, x, y, left);
            int openings = 0;
            Action open = () =>
            {
                frame(1, 1, false); frame(1, 1, false); Call(host, "OpenMaterials"); prepare();
                Console.WriteLine("MATERIAL DRAG opening=" + (++openings) + " fresh " + PublicationState(popup, input));
                NativeQuickItemChecks.Until(() =>
                {
                    NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); prepare();
                    var value = (JueMingR.Features.Blueprint.BlueprintMaterialSnapshot)Get(popup, "snapshot");
                    return value.Status == JueMingR.Features.Blueprint.BlueprintMaterialStatus.Exact && value.InventoryReady;
                });
                check(((JueMingR.Features.Blueprint.BlueprintMaterialSnapshot)Get(popup, "snapshot")).Rows.Count == 0 &&
                    Objects(Get(popup, "labels")).Any(e => (string)Get(e, "Text") == "没有需要的材料"),
                    "the accurate empty window has completed the real Host material/inventory publication and draws its explicit empty result");
                Console.WriteLine("MATERIAL DRAG opening=" + openings + " exact " + PublicationState(popup, input));
            };
            try
            {
                NativeBlueprintMaterialsUiChecks.EnsureNativeScaleRoom(); Call(shell, "CloseAndSubmitPosition"); open();
                var initial = Get(popup, "Panel");
                check(H(initial) < 360 && W(initial) <= 600, "an accurate empty material window contracts to its measured content");
                if (output != null) graphics.Image(Path.Combine(output, "blueprint-world-materials-empty.png"), () => Call(popup, "Draw"), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                var title = Objects(Get(popup, "labels")).First(e => (string)Get(e, "Text") == "施工材料"); var titleRect = Get(title, "Rect");
                float x = X(titleRect) + W(titleRect) / 2, y = Y(titleRect) + H(titleRect) / 2;
                frame(x, y, false); frame(x, y, true); prepare();
                check((bool)Get(popup, "Dragging"), "the actual title text belongs to the drag handle");
                var before = Get(popup, "Panel"); var body = Get(popup, "Body"); float bodyY = Y(body) - Y(before);
                float cachedBodyTop = (float)Get(popup, "bodyTop");
                long builds = (long)Get(popup, "LayoutBuilds"), sets = (long)Get(popup, "VisibleSetBuilds");
                float outsideX = Main.screenWidth / Main.UIScale - 1, outsideY = Main.screenHeight / Main.UIScale - 1;
                frame(outsideX, outsideY, true); PlayerInput.Triggers.Current.CopyInto(player); prepare();
                var moved = Get(popup, "Panel");
                check((bool)Get(popup, "Dragging") && X(moved) > X(before) && Y(moved) != Y(before) &&
                    X(moved) + W(moved) <= Main.screenWidth / Main.UIScale - 11.9f && Y(moved) + H(moved) <= Main.screenHeight / Main.UIScale - 11.9f,
                    "captured title movement continues outside the old window and clamps to the current logical viewport");
                Console.WriteLine("MATERIAL DRAG pure beforePanel=" + RectState(before) + " beforeBody=" + RectState(body) + " after=" + PublicationState(popup, input) +
                    " localBodyY=" + Number(bodyY) + "/" + Number(Y(Get(popup, "Body")) - Y(moved)) +
                    " cachedBodyTop=" + Number(cachedBodyTop) + "/" + Number((float)Get(popup, "bodyTop")) +
                    " layoutBuilds=" + builds + "/" + Get(popup, "LayoutBuilds") + " visibleSets=" + sets + "/" + Get(popup, "VisibleSetBuilds") + " nativeUse=" + player.controlUseItem);
                Console.WriteLine("MATERIAL DRAG translation x=" + TranslationState(X(body), X(Get(popup, "Body")), X(moved) - X(before)) +
                    " y=" + TranslationState(Y(body), Y(Get(popup, "Body")), Y(moved) - Y(before)) +
                    " width=" + Number(W(body)) + "/" + Number(W(Get(popup, "Body"))) + " height=" + Number(H(body)) + "/" + Number(H(Get(popup, "Body"))));
                check(!player.controlUseItem && (long)Get(popup, "LayoutBuilds") == builds && (long)Get(popup, "VisibleSetBuilds") == sets &&
                    W(moved) == W(before) && H(moved) == H(before) && (float)Get(popup, "bodyTop") == cachedBodyTop &&
                    Translated(body, Get(popup, "Body"), X(moved) - X(before), Y(moved) - Y(before)),
                    "pure dragging projects the same local geometry, prepares no new layout/icons and is consumed by original CopyInto");
                object nextAnchor = Activator.CreateInstance(moved.GetType(), F, null, new object[] { 20f, 20f, 40f, 30f }, null);
                Call(popup, "MoveAnchor", nextAnchor); prepare();
                check(X(Get(popup, "Panel")) == X(moved) && Y(Get(popup, "Panel")) == Y(moved), "refreshing the actual source anchor cannot absorb a user-positioned window");
                frame(outsideX, outsideY, false); PlayerInput.Triggers.Current.CopyInto(player); prepare();
                check(!(bool)Get(popup, "Dragging") && !player.controlUseItem && (bool)Get(popup, "Visible"), "the trusted release ends only the drag and remains consumed");
                check((float)Get(state, "X") == shellX && (float)Get(state, "Y") == shellY, "material dragging never moves the F5 main window");
                if (output != null) graphics.Image(Path.Combine(output, "blueprint-world-materials-dragged.png"), () => Call(popup, "Draw"), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                ClickClose(popup, input, frame, prepare);
                check(!(bool)Get(popup, "Visible"), "the visible relocated Close button executes at its current position");
                open(); check(Same(initial, Get(popup, "Panel")) && !(bool)Get(popup, "userPositioned"), "a new exact opening starts from its entry anchor instead of persisting the old drag");
                var panel = Get(popup, "Panel"); var titleBar = Get(popup, "titleBar");
                x = X(panel) + W(panel) / 2; y = Y(titleBar) + H(titleBar) - 3;
                frame(x, y, false); frame(x, y, true); prepare();
                check((bool)Get(popup, "Dragging"), "the remaining title-bar area also starts a drag");
                NativeBlueprintMaterialsUiChecks.ChangeNativeScale("material-title-drag");
                frame(x, y, true); PlayerInput.Triggers.Current.CopyInto(player); prepare();
                check(!(bool)Get(popup, "Dragging") && !player.controlUseItem && (bool)Get(popup, "Visible"), "a changed UI matrix safely ends the old grab while retaining its physical tail");
                frame(1, 1, false); PlayerInput.Triggers.Current.CopyInto(player); check(!player.controlUseItem, "scale invalidation cannot emit the owned release into world use");
                Main.UIScale = scale; PlayerInput.CacheOriginalScreenDimensions(); prepare();
                Call(popup, "Close"); open();
                var selector = Get(popup, "selector"); x = X(selector) + 8; y = Y(selector) + 8;
                frame(x, y, false); frame(x, y, true); check(!(bool)Get(popup, "Dragging"), "the range selector cannot become a title drag");
                frame(x, y, false); prepare(); check((bool)Get(popup, "RangeExpanded"), "the original selector action remains usable after drag recovery");
                Call(popup, "Close"); open(); panel = Get(popup, "Panel"); x = X(panel) + 20; y = Y(panel) + 16;
                frame(x, y, false); frame(x, y, true); prepare();
                check((bool)Get(popup, "Dragging"), "a fresh drag can start after the old scale gesture retired");
                Set(input, "foregroundWindow", (Func<IntPtr>)(() => IntPtr.Zero)); frame(x, y, true); PlayerInput.Triggers.Current.CopyInto(player);
                check(!(bool)Get(popup, "Visible") && !(bool)Get(popup, "Dragging") && !player.controlUseItem, "actual focus loss cancels a drag, closes viewing and consumes the held world-use tail");
                Set(input, "foregroundWindow", foreground); frame(x, y, true); PlayerInput.Triggers.Current.CopyInto(player);
                check(!(bool)Get(input, "CanStartActions") && !player.controlUseItem, "held reactivation does not manufacture a new drag or world use");
                Console.WriteLine("PASS materials title drag: actual text/blank title handles, outside/clamp, one projection/no remeasure, anchor refresh, relocated Close, temporary position, selector exclusion, scale/focus tails and original CopyInto. OS input NOT_EXECUTED.");
            }
            finally
            {
                Set(input, "foregroundWindow", foreground); Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = scale;
                Call(popup, "Close"); frame(1, 1, false); frame(1, 1, false);
            }
        }
        private static void ClickClose(object popup, object input, Action<float, float, bool> frame, Action prepare)
        {
            var close = Get(Objects(Get(popup, "buttons"))[0], "Rect"); float x = X(close) + 8, y = Y(close) + 8;
            frame(x, y, false); frame(x, y, true); Check(!(bool)Get(popup, "Dragging"), "Close owns its button rather than the drag handle", popup, input);
            frame(x, y, false); prepare();
        }
        private static object[] Objects(object value) { return ((IEnumerable)value).Cast<object>().ToArray(); }
        private static void Check(bool value, string reason, object popup, object input)
        {
            if (!value)
                Console.Error.WriteLine("MATERIAL DRAG failed " + reason + " " + PublicationState(popup, input) +
                    " ready=" + Get(popup, "ready") + " userPositioned=" + Get(popup, "userPositioned") + " requested=" + Get(popup, "requestedPosition") +
                    " anchor=" + RectState(Get(popup, "anchor")) + " preparedAnchor=" + RectState(Get(popup, "preparedAnchor")) +
                    " titleBar=" + RectState(Get(popup, "titleBar")) + " selectorRect=" + RectState(Get(popup, "selector")) +
                    " source=" + Get(popup, "Source") + " selected=" + Get(popup, "Selected") + " dropdown=" + Get(popup, "RangeExpanded") +
                    " tails=" + Get(popup, "leftTail") + "/" + Get(popup, "rightTail") + " previousLeft=" + Get(popup, "previousLeft") +
                    " start=" + Get(input, "CanStartActions") + " focused=" + Get(input, "SampleFocused") + " nativeUse=" + Main.LocalPlayer.controlUseItem +
                    " scale=" + Main.UIScale + " matrix=" + Main.UIScaleMatrix + " viewport=" + PlayerInput.OriginalScreenSize +
                    " controls=" + string.Join(";", Objects(Get(popup, "buttons")).Select(b => (string)Get(b, "Text") + ":" + RectState(Get(b, "Rect")))));
            Require(value, reason);
        }
        private static string PublicationState(object popup, object input)
        {
            var value = (JueMingR.Features.Blueprint.BlueprintMaterialSnapshot)Get(popup, "snapshot");
            return "dragging=" + Get(popup, "Dragging") + " rawLeft=" + Get(input, "RawMouseLeft") + " panel=" + RectState(Get(popup, "Panel")) +
                " body=" + RectState(Get(popup, "Body")) + " status=" + (value?.Status.ToString() ?? "none") + " rows=" + (value?.Rows.Count ?? 0) + " content=" + Get(Get(popup, "Content"), "Count") +
                " summary=" + Number((float)Get(popup, "summaryHeight")) + "/" + Number((float)Get(popup, "frozenSummaryHeight")) +
                " title=" + Number((float)Get(popup, "titleHeight")) + "/" + Number((float)Get(popup, "frozenTitleHeight")) +
                " selector=" + Number((float)Get(popup, "selectorHeight")) + "/" + Number((float)Get(popup, "frozenSelectorHeight"));
        }
        private static string Number(float value) { return value.ToString("R", CultureInfo.InvariantCulture); }
        private static string RectState(object value) { return Number(X(value)) + "," + Number(Y(value)) + "," + Number(W(value)) + "," + Number(H(value)); }
        private static float X(object value) { return (float)Get(value, "X"); }
        private static float Y(object value) { return (float)Get(value, "Y"); }
        private static float W(object value) { return (float)Get(value, "Width"); }
        private static float H(object value) { return (float)Get(value, "Height"); }
        // Coordinates are stored as floats; x86 can keep their sum at greater
        // temporary precision. Bound only the two coordinate operations' float
        // rounding by their magnitude. Dimensions/bodyTop stay exact, and the
        // stationary publication test above still requires identical rectangles.
        private static bool Translated(object before, object after, float dx, float dy)
        { return AxisTranslated(X(before), X(after), dx) && AxisTranslated(Y(before), Y(after), dy) && W(after) == W(before) && H(after) == H(before); }
        private static bool AxisTranslated(float before, float after, float delta)
        { return Math.Abs((double)after - ((double)before + delta)) <= TranslationRounding(before, after); }
        private static double TranslationRounding(float before, float after)
        { return 2 * 1.1920928955078125e-7 * Math.Max(1, Math.Max(Math.Abs((double)before), Math.Abs((double)after))); }
        private static string TranslationState(float before, float after, float delta)
        {
            double expected = (double)before + delta;
            // GetBytes writes an actual four-byte single, unlike an x86 cast
            // which may still leave the temporary value in an extended register.
            float stored = BitConverter.ToSingle(BitConverter.GetBytes((float)expected), 0);
            return "actual:" + ((double)after).ToString("R", CultureInfo.InvariantCulture) + ",expected:" + expected.ToString("R", CultureInfo.InvariantCulture) +
                ",singleStored:" + ((double)stored).ToString("R", CultureInfo.InvariantCulture) + ",diff:" + ((double)after - expected).ToString("R", CultureInfo.InvariantCulture) +
                ",roundingBound:" + TranslationRounding(before, after).ToString("R", CultureInfo.InvariantCulture);
        }
        private static bool Same(object a, object b) { return X(a) == X(b) && Y(a) == Y(b) && W(a) == W(b) && H(a) == H(b); }
    }
}
