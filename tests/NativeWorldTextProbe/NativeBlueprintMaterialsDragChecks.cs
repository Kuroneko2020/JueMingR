using System;
using System.Collections;
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
            var title = Objects(Get(popup, "labels")).First(e => (string)Get(e, "Text") == "施工材料"); var rect = Get(title, "Rect");
            float x = X(rect) + W(rect) / 2, y = Y(rect) + H(rect) / 2;
            NativeBlueprintMaterialsUiChecks.Frame(shell, input, x, y, false);
            NativeBlueprintMaterialsUiChecks.Frame(shell, input, x, y, true);
            NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
            var panel = Get(popup, "Panel"); var body = Get(popup, "Body");
            int material = Main.LocalPlayer.inventory[0].type; Main.LocalPlayer.inventory[0].stack = 12;
            tick(); NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
            Require((bool)Get(popup, "Dragging") && ((JueMingR.Features.Blueprint.BlueprintMaterialSnapshot)Get(popup, "snapshot")).Status == JueMingR.Features.Blueprint.BlueprintMaterialStatus.Pending,
                "a real inventory publication continues while a title drag holds the same opening");
            Require(Same(panel, Get(popup, "Panel")) && Same(body, Get(popup, "Body")), "Pending freezes the grabbed frame and all body-local geometry");
            NativeQuickItemChecks.Until(() =>
            {
                tick(); NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
                return ((JueMingR.Features.Blueprint.BlueprintMaterialSnapshot)Get(popup, "snapshot")).Status == JueMingR.Features.Blueprint.BlueprintMaterialStatus.Exact;
            });
            var value = (JueMingR.Features.Blueprint.BlueprintMaterialSnapshot)Get(popup, "snapshot");
            Require((bool)Get(popup, "Dragging") && value.Rows.Single(r => r.ItemType == material).Held == 12 && Same(panel, Get(popup, "Panel")) && Same(body, Get(popup, "Body")),
                "the new exact quantity reaches the grabbed window without freezing business or shifting local frame geometry");
            finishOneTarget();
            NativeQuickItemChecks.Until(() =>
            {
                tick(); NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
                value = (JueMingR.Features.Blueprint.BlueprintMaterialSnapshot)Get(popup, "snapshot");
                return value.Status == JueMingR.Features.Blueprint.BlueprintMaterialStatus.Exact && value.Rows.Count == 69;
            });
            Require((bool)Get(popup, "Dragging") && (int)Get(Get(popup, "Content"), "Count") == 69 && Same(panel, Get(popup, "Panel")) && Same(body, Get(popup, "Body")),
                "a real completed material leaves the live set during dragging while the frame and local body stay frozen");
            NativeBlueprintMaterialsUiChecks.Frame(shell, input, x, y, false); PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
            NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
            Require(!(bool)Get(popup, "Dragging") && !Main.LocalPlayer.controlUseItem, "publication drag release remains consumed before original world input");
        }
        internal static void Run(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var popup = Get(shell, "BlueprintMaterialsUi");
            var input = Get(context, "Input"); var state = Get(shell, "State"); var player = Main.LocalPlayer;
            var foreground = (Func<IntPtr>)Get(input, "foregroundWindow"); float scale = Main.UIScale;
            float shellX = (float)Get(state, "X"), shellY = (float)Get(state, "Y");
            Action prepare = () => NativeBlueprintMaterialsUiChecks.Prepare(shell, graphics);
            Action<float, float, bool> frame = (x, y, left) => NativeBlueprintMaterialsUiChecks.Frame(shell, input, x, y, left);
            Action open = () => { frame(1, 1, false); frame(1, 1, false); Call(host, "OpenMaterials"); prepare(); };
            try
            {
                Call(shell, "CloseAndSubmitPosition"); open();
                var initial = Get(popup, "Panel");
                Require(H(initial) < 360 && W(initial) <= 600, "an accurate empty material window contracts to its measured content");
                if (output != null) graphics.Image(Path.Combine(output, "blueprint-world-materials-empty.png"), () => Call(popup, "Draw"), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                var title = Objects(Get(popup, "labels")).First(e => (string)Get(e, "Text") == "施工材料"); var titleRect = Get(title, "Rect");
                float x = X(titleRect) + W(titleRect) / 2, y = Y(titleRect) + H(titleRect) / 2;
                frame(x, y, false); frame(x, y, true); prepare();
                Require((bool)Get(popup, "Dragging"), "the actual title text belongs to the drag handle");
                var before = Get(popup, "Panel"); var body = Get(popup, "Body"); float bodyY = Y(body) - Y(before);
                long builds = (long)Get(popup, "LayoutBuilds"), sets = (long)Get(popup, "VisibleSetBuilds");
                float outsideX = Main.screenWidth / Main.UIScale - 1, outsideY = Main.screenHeight / Main.UIScale - 1;
                frame(outsideX, outsideY, true); PlayerInput.Triggers.Current.CopyInto(player); prepare();
                var moved = Get(popup, "Panel");
                Require((bool)Get(popup, "Dragging") && X(moved) > X(before) && Y(moved) != Y(before) &&
                    X(moved) + W(moved) <= Main.screenWidth / Main.UIScale - 11.9f && Y(moved) + H(moved) <= Main.screenHeight / Main.UIScale - 11.9f,
                    "captured title movement continues outside the old window and clamps to the current logical viewport");
                Require(!player.controlUseItem && (long)Get(popup, "LayoutBuilds") == builds && (long)Get(popup, "VisibleSetBuilds") == sets &&
                    H(moved) == H(before) && Y(Get(popup, "Body")) - Y(moved) == bodyY,
                    "pure dragging projects the same local geometry, prepares no new layout/icons and is consumed by original CopyInto");
                object nextAnchor = Activator.CreateInstance(moved.GetType(), F, null, new object[] { 20f, 20f, 40f, 30f }, null);
                Call(popup, "MoveAnchor", nextAnchor); prepare();
                Require(X(Get(popup, "Panel")) == X(moved) && Y(Get(popup, "Panel")) == Y(moved), "refreshing the actual source anchor cannot absorb a user-positioned window");
                frame(outsideX, outsideY, false); PlayerInput.Triggers.Current.CopyInto(player); prepare();
                Require(!(bool)Get(popup, "Dragging") && !player.controlUseItem && (bool)Get(popup, "Visible"), "the trusted release ends only the drag and remains consumed");
                Require((float)Get(state, "X") == shellX && (float)Get(state, "Y") == shellY, "material dragging never moves the F5 main window");
                if (output != null) graphics.Image(Path.Combine(output, "blueprint-world-materials-dragged.png"), () => Call(popup, "Draw"), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                ClickClose(popup, frame, prepare);
                Require(!(bool)Get(popup, "Visible"), "the visible relocated Close button executes at its current position");
                open(); Require(X(Get(popup, "Panel")) == X(initial) && Y(Get(popup, "Panel")) == Y(initial), "a new opening starts from its entry anchor instead of persisting the old drag");
                var panel = Get(popup, "Panel"); var titleBar = Get(popup, "titleBar");
                x = X(panel) + W(panel) / 2; y = Y(titleBar) + H(titleBar) - 3;
                frame(x, y, false); frame(x, y, true); prepare();
                Require((bool)Get(popup, "Dragging"), "the remaining title-bar area also starts a drag");
                Main.UIScale = scale * 1.2f; PlayerInput.CacheOriginalScreenDimensions();
                frame(x, y, true); PlayerInput.Triggers.Current.CopyInto(player); prepare();
                Require(!(bool)Get(popup, "Dragging") && !player.controlUseItem && (bool)Get(popup, "Visible"), "a changed UI matrix safely ends the old grab while retaining its physical tail");
                frame(1, 1, false); PlayerInput.Triggers.Current.CopyInto(player); Require(!player.controlUseItem, "scale invalidation cannot emit the owned release into world use");
                Main.UIScale = scale; PlayerInput.CacheOriginalScreenDimensions(); prepare();
                Call(popup, "Close"); open();
                var selector = Get(popup, "selector"); x = X(selector) + 8; y = Y(selector) + 8;
                frame(x, y, false); frame(x, y, true); Require(!(bool)Get(popup, "Dragging"), "the range selector cannot become a title drag");
                frame(x, y, false); prepare(); Require((bool)Get(popup, "RangeExpanded"), "the original selector action remains usable after drag recovery");
                Call(popup, "Close"); open(); panel = Get(popup, "Panel"); x = X(panel) + 20; y = Y(panel) + 16;
                frame(x, y, false); frame(x, y, true); prepare();
                Require((bool)Get(popup, "Dragging"), "a fresh drag can start after the old scale gesture retired");
                Set(input, "foregroundWindow", (Func<IntPtr>)(() => IntPtr.Zero)); frame(x, y, true); PlayerInput.Triggers.Current.CopyInto(player);
                Require(!(bool)Get(popup, "Visible") && !(bool)Get(popup, "Dragging") && !player.controlUseItem, "actual focus loss cancels a drag, closes viewing and consumes the held world-use tail");
                Set(input, "foregroundWindow", foreground); frame(x, y, true); PlayerInput.Triggers.Current.CopyInto(player);
                Require(!(bool)Get(input, "CanStartActions") && !player.controlUseItem, "held reactivation does not manufacture a new drag or world use");
                Console.WriteLine("PASS materials title drag: actual text/blank title handles, outside/clamp, one projection/no remeasure, anchor refresh, relocated Close, temporary position, selector exclusion, scale/focus tails and original CopyInto. OS input NOT_EXECUTED.");
            }
            finally
            {
                Set(input, "foregroundWindow", foreground); Main.UIScale = scale; PlayerInput.CacheOriginalScreenDimensions();
                Call(popup, "Close"); frame(1, 1, false); frame(1, 1, false);
            }
        }
        private static void ClickClose(object popup, Action<float, float, bool> frame, Action prepare)
        {
            var close = Get(Objects(Get(popup, "buttons"))[0], "Rect"); float x = X(close) + 8, y = Y(close) + 8;
            frame(x, y, false); frame(x, y, true); Require(!(bool)Get(popup, "Dragging"), "Close owns its button rather than the drag handle");
            frame(x, y, false); prepare();
        }
        private static object[] Objects(object value) { return ((IEnumerable)value).Cast<object>().ToArray(); }
        private static float X(object value) { return (float)Get(value, "X"); }
        private static float Y(object value) { return (float)Get(value, "Y"); }
        private static float W(object value) { return (float)Get(value, "Width"); }
        private static float H(object value) { return (float)Get(value, "Height"); }
        private static bool Same(object a, object b) { return X(a) == X(b) && Y(a) == Y(b) && W(a) == W(b) && H(a) == H(b); }
    }
}
