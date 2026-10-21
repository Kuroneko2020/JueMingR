using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Same initialized .8 Main, real native mapping, production Shell/layout/
    // renderer and preference owner. Only physical input is supplied by this
    // owned fixture; no business Execute call stands in for a click.
    internal static class NativeMovementUiChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Action<Player> onJump;
        private static int limiterCalls;
        internal static void Run(Main game, object context, Action<Keys[]> input, Action draw, string output, Action<int, int, bool> pointer = null)
        {
            Require(pointer != null, "Movement UI needs an actual native mouse sampling callback");
            var h = new Frames(context, input, pointer);
            var movement = Get(context, "Movement"); var shell = Get(context, "Shell"); var state = Get(shell, "State");
            var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
            var registry = (HotkeyRegistry)Get(Get(shell, "hotkeys"), "Registry");
            var popup = Get(shell, "HotkeyPopup");
            string[] ids = { "movement.simulated-jump", "movement.continuous-dash", "movement.teleport-correction", "movement.smart-fall-protection" };
            string[] names = { "模拟连跳", "连续冲刺", "传送修正", "智能防摔" };
            var observer = new Harmony("JueMingR.Tests.Movement.UiOwners");
            observer.Patch(typeof(Player).GetMethod("JumpMovement", F), postfix: new HarmonyMethod(typeof(NativeMovementUiChecks), nameof(InWorldJump)));
            observer.Patch(typeof(Player).GetMethod("LimitPointToPlayerReachableArea", F), prefix: new HarmonyMethod(typeof(NativeMovementUiChecks), nameof(Limited)));
            float scale = Main.UIScale;
            try
            {
                NativeQuickItemChecks.Until(() => { h.Frame(); return (bool)Get(movement, "ControlsEnabled") && bindings.Loaded; });
                for (int i = 0; i < ids.Length; i++) if ((bool)Call(movement, "IsEnabled", i)) Require((bool)Call(movement, "SetEnabled", i, false), "prepare isolated OFF state");
                Type mode = movement.GetType().Assembly.GetType("JueMingR.Features.Movement.ContinuousDashMode", false) ?? Get(movement, "DashMode").GetType();
                Call(movement, "SetDashMode", Enum.Parse(mode, "DoubleTap"));
                h.Open(); h.Navigate(11);
                var rows = h.Elements().Where(e => names.Contains((string)GetOptional(e, "Text"))).ToArray();
                Require(rows.Select(e => (string)Get(e, "Text")).SequenceEqual(names), "real movement page has four ordered names");
                Require(h.Elements().Count(e => ids.Contains((string)GetOptional(e, "HotkeyTarget"))) == ids.Length, "four standard row-end binding slots");
                for (int i = 0; i < ids.Length; i++) Require(registry.Find(ids[i]) != null, "real shared registry owns action " + ids[i]);
                Require(Get(movement, "DashMode").ToString() == "DoubleTap", "OFF presentation begins with the specified mode");
                h.ClickCommand("MovementDashMode");
                Require(!(bool)Call(movement, "IsEnabled", 1) && Get(movement, "DashMode").ToString() == "Hold", "actual mode click changes preference while main switch remains OFF");
                h.ClickCommand("MovementDashOn"); h.ClickCommand("MovementDashOff");
                Require(!(bool)Call(movement, "IsEnabled", 1) && Get(movement, "DashMode").ToString() == "Hold", "real ON/OFF commands retain independent mode");
                h.Hover(h.Command("MovementDashMode")); Render(game, shell, Path.Combine(output, "movement-mode-off-hold.png"));
                var hint = Get(Get(shell, "renderer"), "HintLayout");
                Require((bool)Get(hint, "Visible") && (string)Get(hint, "text") == "按住方向键冲刺", "actual mode-button hint reflects OFF owner's current mode");
                h.ClickCommand("MovementDashMode"); h.Hover(h.Command("MovementDashMode")); Render(game, shell, null);
                Require((string)Get(hint, "text") == "双击按住方向键冲刺", "mode-button hint refreshes after actual switch");
                foreach (float ui in new[] { 1f, 1.5f })
                {
                    Main.UIScale = ui; PlayerInput.CacheOriginalScreenDimensions(); h.Frame(); h.Frame(); h.Navigate(11);
                    Columns(h);
                    h.ClickCommand("MovementDashMode"); Columns(h);
                    Require(!(bool)Call(movement, "IsEnabled", 1) && Get(movement, "DashMode").ToString() == "Hold", "scaled left-of-ON mode button remains usable while OFF");
                    h.ClickCommand("MovementDashMode"); Columns(h);
                    foreach (string command in new[] { "MovementJumpOn", "MovementJumpOff", "MovementTeleportOn", "MovementTeleportOff", "MovementFallOn", "MovementFallOff" }) h.ClickCommand(command);
                    h.Hover(h.Elements().Single(e => (string)GetOptional(e, "Text") == "模拟连跳")); Render(game, shell, Path.Combine(output, "movement-page-ui-" + (int)(ui * 100) + ".png"));
                    Require((bool)Get(hint, "Visible") && (string)Get(hint, "text") == "按住跳跃键连跳，类似蛙腿效果", "name hover uses the actual final approved description at UI scale=" + Main.UIScale);
                    Require(!(bool)Call(movement, "IsEnabled", 0) && !(bool)Call(movement, "IsEnabled", 2), "scaled clicks reach exact owning settings");
                }
                Main.UIScale = 1; h.Frame(); h.Frame();
                for (int feature = 0; feature < ids.Length; feature++)
                {
                    string id = ids[feature]; object icon = h.Elements().Single(e => (string)GetOptional(e, "HotkeyTarget") == id);
                    h.Click(h.Point(icon)); h.Click(h.Point(icon));
                    Require((bool)Get(popup, "Visible") && (string)Get(popup, "Target") == id, "real double-click opens shared recorder for " + id);
                    h.ClickPopup("Record"); Require((bool)Get(popup, "Capturing"), "actual Record button owns capture");
                    if (feature == 0)
                    {
                        Require(!(bool)Get(shell, "CanMovementInput"), "actual recorder is a movement owner");
                        Require((bool)Call(movement, "SetEnabled", 2, true), "prepare gated teleport preference");
                        Gate(game, context, false, "actual-recording-owner");
                    }
                    Keys key = feature == 0 ? Keys.F20 : feature == 1 ? Keys.F21 : feature == 2 ? Keys.F22 : Keys.F23;
                    h.Frame(Keys.LeftControl); h.Frame(Keys.LeftControl, key); h.Frame(Keys.LeftControl, key); h.Frame(); h.Frame();
                    NativeQuickItemChecks.Until(() => { h.Frame(); return !bindings.Busy; });
                    Require(bindings.CompletionSucceeded && bindings.Get(id) != null && bindings.Get(id).MainKey == (int)key, "actual shared recorder saved " + id);
                    h.ClickPopup("Close"); h.Frame();
                    Call(shell, "CloseAndSubmitPosition"); h.Frame();
                    bool before = (bool)Call(movement, "IsEnabled", feature);
                    h.Frame(Keys.LeftControl, key); h.Frame(Keys.LeftControl, key); h.Frame(); h.Frame();
                    Require((bool)Call(movement, "IsEnabled", feature) != before, "real mapped saved hotkey toggles exact feature " + feature);
                    var feedback = Get(context, "ShortFeedback");
                    Require(NativeShortFeedbackChecks.Has(feedback, names[feature] + (feature == 1 ? "（双击）" : "") + ((bool)Call(movement, "IsEnabled", feature) ? " 已开启" : " 已关闭")), "real toggle has standard state and current-mode feedback");
                    if (feature == 1) ModeReceipts(h, movement, feedback, key);
                    h.Open(); h.Navigate(11);
                }
                if (!(bool)Call(movement, "IsEnabled", 2)) h.ClickCommand("MovementTeleportOn");
                h.Pointer(20, 20, false); h.Frame(); h.Frame();
                Require((bool)Get(state, "Visible") && !(bool)Get(shell, "OwnsPointer") && (bool)Get(shell, "CanMovementInput"), "ordinary visible F5 with pointer outside is not a movement barrier");
                Gate(game, context, true, "F5-visible-pointer-outside");
                h.Pointer(20, 20, false); h.Frame(); h.Frame(); Main.playerInventory = true;
                try { Gate(game, context, true, "ordinary-backpack-visible"); } finally { Main.playerInventory = false; }
                var buttonPoint = h.Point(h.Command("MovementDashMode")); h.Pointer((int)buttonPoint.X, (int)buttonPoint.Y, true); h.Frame();
                Require((bool)Get(shell, "OwnsPointer"), "actual F5 button press owns pointer");
                Gate(game, context, false, "F5-consumed-press");
                h.Pointer((int)buttonPoint.X, (int)buttonPoint.Y, false); h.Frame(); h.Frame();
                h.Pointer(20, 20, false); h.Frame(); h.Frame();
                Main.drawingPlayerChat = true;
                try { Gate(game, context, false, "native-chat-owner"); } finally { Main.drawingPlayerChat = false; }
                PlayerInput.WritingText = true;
                try { Gate(game, context, false, "native-writing-owner"); } finally { PlayerInput.WritingText = false; }
                // A real popup remains a modal even after recording stopped
                // and the pointer moved outside its panel.
                object teleportIcon = h.Elements().Single(e => (string)GetOptional(e, "HotkeyTarget") == ids[2]);
                h.Click(h.Point(teleportIcon)); h.Click(h.Point(teleportIcon)); h.Pointer(20, 20, false); h.Frame();
                Require((bool)Get(popup, "Visible") && !(bool)Get(popup, "Capturing"), "real non-recording modal remains open");
                Gate(game, context, false, "actual-binding-modal-outside");
                h.ClickPopup("Close"); h.Frame(); h.Frame();
                Console.WriteLine("PASS MOVEMENT UI actual native input/F5 navigation/four rows,OFF mode/owning settings,100/150% renderer+hitboxes,four shared record/save/toggle feedback,real world Session and F5/backpack/text/record/modal pointer gates; physical focus/IME/owner-experience=NOT_EVALUATED");
            }
            finally
            {
                onJump = null; observer.UnpatchAll(observer.Id); Main.UIScale = scale; Main.drawingPlayerChat = false; PlayerInput.WritingText = false;
                Call(popup, "Close"); Call(shell, "CloseAndSubmitPosition"); h.Pointer(1400, 540, false); h.Frame(); h.Frame();
            }
        }

        private static void Gate(Main game, object context, bool allowed, string label)
        {
            object movement = Get(context, "Movement"); bool observed = false; int calls = 0;
            var p = Main.LocalPlayer; Vector2 before = p.position;
            onJump = player =>
            {
                if (!ReferenceEquals(player, p) || observed) return;
                observed = true; Require((bool)Call(movement, "CanCorrectTeleport", p) == allowed, "real Update Session permission " + label);
                Vector2 target = new Vector2(Main.mouseX + Main.screenPosition.X - p.width / 2,
                    p.gravDir == 1 ? Main.mouseY + Main.screenPosition.Y - p.height : Main.screenPosition.Y + Main.screenHeight - Main.mouseY);
                p.LimitPointToPlayerReachableArea(ref target);
                int x = (int)((target.X + p.width / 2) / 16), y = (int)((target.Y + p.height / 2) / 16);
                Tile tile = Main.tile[x, y]; bool active = tile.active(), solid = Main.tileSolid[1]; ushort type = tile.type;
                try
                {
                    tile.active(true); tile.type = 1; Main.tileSolid[1] = true;
                    p.itemAnimation = 20; p.itemTime = 0; limiterCalls = 0;
                    typeof(Player).GetMethod("ItemCheck_UseTeleportRod", F).Invoke(p, new object[] { new Item { type = 5335, useTime = 20 } });
                    calls = limiterCalls;
                    Require(p.itemTime == 20 && (allowed ? calls > 1 : calls == 1), "real native rod consumer " + label + " limiter-count=" + calls);
                }
                finally { tile.active(active); tile.type = type; Main.tileSolid[1] = solid; }
            };
            try { typeof(Main).GetMethod("DoUpdateInWorld_Inner", F).Invoke(game, null); }
            finally { onJump = null; p.position = before; p.velocity = Vector2.Zero; p.itemAnimation = p.itemTime = 0; }
            Require(observed && !(bool)Call(movement, "CanCorrectTeleport", p), "actual world update closes permission after native consumer " + label);
            Console.WriteLine("MOVEMENT UI OWNER " + label + " allowed=" + allowed + " native-limit-calls=" + calls);
        }
        private static void InWorldJump(Player __instance) { onJump?.Invoke(__instance); }
        private static void ModeReceipts(Frames frames, object movement, object feedback, Keys key)
        {
            foreach (string mode in new[] { "Hold", "DoubleTap" })
            {
                frames.Open(); frames.Navigate(11); Call(feedback, "Clear");
                if (Get(movement, "DashMode").ToString() != mode) frames.ClickCommand("MovementDashMode");
                frames.ClickCommand("MovementDashOn"); frames.ClickCommand("MovementDashOff");
                Require((int)Get(feedback, "Count") == 0, "ordinary F5 mode/ON/OFF clicks remain silent");
                Call(Get(frames, "shell"), "CloseAndSubmitPosition"); frames.Frame(); frames.Frame();
                string detail = mode == "Hold" ? "按住" : "双击";
                foreach (bool on in new[] { true, false })
                {
                    Call(feedback, "Clear");
                    frames.Frame(Keys.LeftControl, key); frames.Frame(Keys.LeftControl, key); frames.Frame(); frames.Frame();
                    Require((bool)Call(movement, "IsEnabled", 1) == on && Get(movement, "DashMode").ToString() == mode,
                        "real shared dash key toggles enabled and retains selected mode " + mode);
                    Require(NativeShortFeedbackChecks.Has(feedback, "连续冲刺（" + detail + "）" + (on ? " 已开启" : " 已关闭")),
                        "real enabled/disabled receipt uses effective/prior mode " + mode);
                }
                // Do not clear or replace the OFF result: the actual mode
                // command must retire it through the existing display owner.
                frames.Open(); frames.Navigate(11); frames.ClickCommand("MovementDashMode");
                Require(!(bool)Call(movement, "IsEnabled", 1) && (int)Get(feedback, "Count") == 0,
                    "Changing mode while OFF retires the old mode receipt without publishing an F5 result");
            }
        }
        private static void Columns(Frames frames)
        {
            object mode = Get(frames.Command("MovementDashMode"), "Rect"), on = Get(frames.Command("MovementDashOn"), "Rect"), off = Get(frames.Command("MovementDashOff"), "Rect");
            float onX = (float)Get(on, "X"), offX = (float)Get(off, "X");
            Require((float)Get(mode, "X") + (float)Get(mode, "Width") < onX && onX + (float)Get(on, "Width") < offX, "final dash order is mode, ON, OFF");
            foreach (string feature in new[] { "Jump", "Teleport", "Fall" })
            {
                Require(Math.Abs((float)Get(Get(frames.Command("Movement" + feature + "On"), "Rect"), "X") - onX) < .01f &&
                    Math.Abs((float)Get(Get(frames.Command("Movement" + feature + "Off"), "Rect"), "X") - offX) < .01f,
                    "all four rows share ON/OFF columns at actual UI scale=" + Main.UIScale);
            }
        }
        private static void Limited() { limiterCalls++; }
        private static void Render(Main game, object shell, string path)
        {
            var device = game.GraphicsDevice; var old = device.GetRenderTargets();
            using (var target = new RenderTarget2D(device, Main.screenWidth, Main.screenHeight, false, SurfaceFormat.Color, DepthFormat.None))
            {
                device.SetRenderTarget(target); device.Clear(Color.CornflowerBlue);
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
                try { Call(shell, "DrawLayer"); }
                finally { Main.spriteBatch.End(); device.SetRenderTargets(old); }
                Require(!(bool)Get(shell, "Failed"), "production movement renderer stays healthy");
                if (path != null) using (var file = File.Create(path)) target.SaveAsPng(file, target.Width, target.Height);
            }
        }
        private sealed class Frames
        {
            private readonly object context, shell, state;
            private readonly Action<Keys[]> input;
            private readonly Action<int, int, bool> pointer;
            internal Frames(object context, Action<Keys[]> input, Action<int, int, bool> pointer)
            { this.context = context; shell = Get(context, "Shell"); state = Get(shell, "State"); this.input = input; this.pointer = pointer; }
            internal void Pointer(int x, int y, bool down) { pointer(x, y, down); }
            internal void Frame(params Keys[] keys)
            {
                Call(shell, "BeforeInput"); input(keys); Call(shell, "ProcessInput"); Call(context, "UpdateShell");
                Require(!(bool)Get(shell, "Failed"), "actual native mapping,Shell and layout remain healthy");
            }
            internal void Open()
            { if ((bool)Get(state, "Visible")) return; Pointer(20, 20, false); Frame(); Frame(); Frame(Keys.F5); Frame(Keys.F5); Frame(); Frame(); Require((bool)Get(state, "Visible"), "real F5 opens movement shell"); }
            internal object[] Elements() => ((IEnumerable)Get(Get(state, "Layout"), "Elements")).Cast<object>().ToArray();
            internal object Command(string command) => Elements().Single(e => Get(e, "Command").ToString() == command);
            internal Vector2 Point(object element)
            {
                var rect = Get(element, "Rect"); var view = Get(Get(state, "Layout"), "Viewport");
                return (new Vector2((float)Get(state, "X") + (float)Get(view, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2,
                    (float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(rect, "Y") - (float)Get(state, "Scroll") + (float)Get(rect, "Height") / 2)) * Main.UIScale;
            }
            internal void Click(Vector2 point)
            { Pointer((int)point.X, (int)point.Y, false); Frame(); Pointer((int)point.X, (int)point.Y, true); Frame(); Pointer((int)point.X, (int)point.Y, false); Frame(); Frame(); }
            internal void ClickCommand(string command) { Click(Point(Command(command))); }
            internal void Hover(object element) { var point = Point(element); Pointer((int)point.X, (int)point.Y, false); Frame(); }
            internal void Navigate(int page)
            {
                var rect = Call(Get(state, "Layout"), "Navigation", page);
                Click((new Vector2((float)Get(state, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2,
                    (float)Get(state, "Y") + (float)Get(rect, "Y") + (float)Get(rect, "Height") / 2)) * Main.UIScale);
                Require((int)Get(state, "Page") == page, "actual navigation selects movement page");
            }
            internal void ClickPopup(string command)
            {
                var layout = Get(Get(shell, "HotkeyPopup"), "Layout"); var commands = ((IEnumerable)Get(layout, "Commands")).Cast<object>().ToArray();
                var buttons = ((IEnumerable)Get(layout, "Buttons")).Cast<object>().ToArray(); int index = Array.FindIndex(commands, c => c.ToString() == command);
                Require(index >= 0, "shared popup command " + command); var panel = Get(layout, "Panel"); var rect = Get(buttons[index], "Rect");
                Click((new Vector2((float)Get(panel, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2,
                    (float)Get(panel, "Y") + (float)Get(rect, "Y") + (float)Get(rect, "Height") / 2)) * Main.UIScale);
            }
        }
    }
}
