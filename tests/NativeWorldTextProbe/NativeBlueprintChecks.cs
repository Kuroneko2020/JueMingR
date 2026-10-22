using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using JueMingR.Platform.Settings;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Visual(object context, ProbeGraphics graphics, string output)
        {
            Directory.CreateDirectory(output);
            var host = Get(context, "Blueprint"); var editor = (BlueprintCaptureState)Get(host, "State");
            var shell = Get(context, "Shell"); var state = Get(shell, "State"); var renderer = Get(shell, "renderer");
            var p = Main.LocalPlayer; Item item = p.inventory[0]; int selected = p.selectedItem;
            Call(host, "SetToolbar", true); p.inventory[0] = new Item(); p.inventory[0].SetDefaults(Terraria.ID.ItemID.Gel); p.selectedItemState.Select(0); p.selectedItemState.Update();
            Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
            try
            {
                foreach (float ui in new[] { 1f, 1.5f })
                foreach (bool active in new[] { false, true })
                {
                    Main.screenWidth = 1280; Main.screenHeight = 900; Main.UIScale = ui; PlayerInput.CacheOriginalScreenDimensions();
                    if (active) editor.Enter(); else editor.Exit();
                    Call(state, "Navigate", 6); Call(state, "RestoreVisible"); Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, 900f, ui);
                    graphics.Image(Path.Combine(output, "f5-" + ui.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + active + ".png"),
                        () => Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false), Main.UIScaleMatrix, 1280, 900);
                    Call(shell, "CloseAndSubmitPosition");
                    PlayerInput.MouseInfo = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                    graphics.Image(Path.Combine(output, "toolbar-" + ui.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + active + ".png"), () => Call(Get(host, "Toolbar"), "Draw"), Main.UIScaleMatrix, 1280, 900);
                    Require((bool)Get(Get(host, "Toolbar"), "presented"), "actual toolbar draws borrowed native resources");
                    var toolbar = Get(host, "Toolbar"); var layout = Get(toolbar, "Layout"); var panel = Get(layout, "Panel");
                    var button = ((System.Collections.IEnumerable)Get(layout, "Buttons")).Cast<object>().First(); var rect = Get(button, "Rect");
                    int px = (int)(((float)Get(panel, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2) * ui);
                    int py = (int)(((float)Get(panel, "Y") + (float)Get(rect, "Y") + (float)Get(rect, "Height") / 2) * ui);
                    PlayerInput.MouseInfo = new MouseState(px, py, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                    graphics.Image(Path.Combine(output, "toolbar-hint-" + ui.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + active + ".png"), () => Call(toolbar, "Draw"), Main.UIScaleMatrix, 1280, 900);
                    Require((bool)Get(Get(toolbar, "hint"), "Visible"), "disabled save keeps an actual gold hover hint");
                    Call(shell, "HoverGateLayer");
                    Require(p.mouseInterface && Main.mouseText && !(bool)Call(shell, "AllowNpcHover"), "visible toolbar hit owns only the existing world-hover gate");
                    Call(shell, "EndPointerLayer");
                }
                editor.Enter(); editor.Selection.ToggleRectangle(28, 18, 34, 22, Main.maxTilesX, Main.maxTilesY);
                Main.screenWidth = 960; Main.screenHeight = 640; Main.screenPosition = Microsoft.Xna.Framework.Vector2.Zero;
                Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 960, 640));
                foreach (float zoom in new[] { .3f, 1f, 10f })
                foreach (float gravity in new[] { 1f, -1f })
                {
                    Main.GameViewMatrix.Zoom = new Microsoft.Xna.Framework.Vector2(zoom); p.gravDir = gravity;
                    Call(Get(context, "worldTiles"), "BeginTick");
                    Action draw = () => Call(Get(host, "World"), "Draw");
                    var pixels = graphics.Pixels(draw, Main.GameViewMatrix.ZoomMatrix);
                    Require(pixels.Count(c => c.A > 0 && c.B > c.R && c.B > c.G) > 0, "visible selected spans reach actual blue pixels at zoom/gravity " + zoom + "/" + gravity);
                    graphics.Image(Path.Combine(output, "selection-" + zoom.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + gravity + ".png"), draw, Main.GameViewMatrix.ZoomMatrix);
                }
                Console.WriteLine("PASS Blueprint isolated actual F5/toolbar native-resource draws and blue selection pixels at 30/100/1000% and both gravity directions; not full Main, OS input or owner experience.");
            }
            finally { editor.Exit(); p.inventory[0] = item; p.selectedItemState.Select(selected); p.selectedItemState.Update(); p.gravDir = 1; }
        }
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var p = Main.LocalPlayer;
            Require(host != null && GetOptional(host, "SetupError") == null, "blueprint exact native hooks install in full composition: " + GetOptional(host, "SetupError"));
            NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled"); });
            var state = (BlueprintCaptureState)Get(host, "State");
            Require(!(bool)Get(host, "ToolbarEnabled") && !state.Active, "toolbar defaults off; mode never restored from settings");
            var keys = Get(Get(context, "Shell"), "hotkeys"); var registry = (HotkeyRegistry)Get(keys, "Registry");
            Require(registry.Find("blueprint.capture") != null && registry.Find("blueprint.save").CanConfigure, "both stable actions registered before binding freeze");
            Call(host, "Toggle"); Require(state.Active, "shared action enters current local session");
            state.Selection.ToggleRectangle(1, 2, 3, 4, Main.maxTilesX, Main.maxTilesY);
            Require(!registry.Find("blueprint.save").Invoke(HotkeyContext.SinglePlayer) && !(bool)Call(host, "Save") && state.Selection.HasSelection, "save has no success path and cannot clear selection");
            PointerFlow(host, input);

            bool inventory = Main.playerInventory; var mouseItem = Main.mouseItem; Item slot = p.inventory[58]; bool mouseLeft = Main.mouseLeft, mouseRight = Main.mouseRight;
            try
            {
                Main.playerInventory = false;
                Sample(input, true, true, true, true);
                Require((bool)Get(input, "RawMouseLeft") && (bool)Get(input, "RawMouseRight") && Main.mouseLeft && Main.mouseRight, "native raw/UI mouse survives world capture");
                PlayerInput.Triggers.Current.Jump = true; PlayerInput.Triggers.Current.Up = true;
                PlayerInput.Triggers.Current.CopyInto(p);
                Require(!p.controlUseItem && !p.controlUseTile && p.controlJump && p.controlUp, "real CopyInto gates only world actions; Mouse1 Jump and W survive");
                var remote = new Player(); PlayerInput.Triggers.Current.CopyInto(remote);
                Require(remote.controlUseItem && remote.controlUseTile && state.Active, "remote CopyInto is not local editor ownership");
                var hooks = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintHooks");
                var lease = hooks.GetMethod("BeginHeld", Flags).Invoke(null, new object[] { p });
                try
                {
                    Require(!Main.mouseLeft && !Main.mouseRight, "held ItemCheck scope masks raw only while world consumer runs");
                    bool result = (bool)typeof(Player).GetMethod("ItemCheck_TryStartUse", Flags).Invoke(p, new object[] { new Item(), false });
                    Require(!result, "fast-use cannot restart held action behind world gate");
                }
                finally { Call(lease, "End"); Call(lease, "End"); }
                Require(Main.mouseLeft && Main.mouseRight, "idempotent held scope returns its raw values");
                Main.playerInventory = true; Main.mouseItem = new Item(); Main.mouseItem.SetDefaults(9); Main.mouseItem.stack = 3;
                p.mouseInterface = false; p.controlThrow = false; p.noThrow = 2;
                p.dropItemCheck(); Require(p.noThrow == 1 && Main.mouseItem.stack == 3 && Main.mouseRight, "real drop upkeep runs; carried UI item is not thrown via raw right click");
                Main.mouseRightRelease = true; p.noThrow = 0; p.dropItemCheck();
                Require(Main.mouseItem.stack == 3 && Main.mouseRightRelease, "world drop branch denied without consuming UI release flag");
                foreach (string method in new[] { "HoverOverNPCs", "TryInteractingWithVoidLens", "TryInteractingWithMoneyTrough", "TryDespawningProjectile" })
                {
                    var native = typeof(Main).GetMethod(method, Flags); var patches = Harmony.GetPatchInfo(native);
                    Require(patches != null && patches.Owners.Contains("JueMingR.Blueprint.WorldActions") && patches.Finalizers.Count > 0, "exact world hover consumer has exception-safe gate: " + method);
                }
                Call(host, "Toggle"); Require(!state.Active && !state.Selection.HasSelection && state.BlockedActions == 3, "same action exits and retains independent physical tails");
                Sample(input, false, true, false, true); Sample(input, false, true, false, true);
                Require(state.BlockedActions == 2 && Main.mouseRight, "left release does not retire right or block UI raw");
                Sample(input, false, false, false, false); Sample(input, false, false, false, false);
                Require(state.BlockedActions == 0, "trusted physical release retires tail without editable-mode permission");
                Sample(input, false, false, true, false); Call(host, "Toggle"); Call(host, "Toggle");
                Sample(input, false, false, true, false); Require(state.BlockedActions == 1, "keyboard use cannot replay after explicit exit");
                Sample(input, false, false, false, false); Sample(input, false, false, false, false);
                Require(state.BlockedActions == 0, "keyboard request neutral is observed independently");
            }
            finally
            {
                Main.playerInventory = inventory; Main.mouseItem = mouseItem; p.inventory[58] = slot; Main.mouseLeft = mouseLeft; Main.mouseRight = mouseRight;
                if (state.Active) state.Exit();
            }
            NativeBlueprintUiChecks.Run(context);
            PreferencesAndLifecycle(context, host, input, keys);
            Console.WriteLine("PASS Blueprint raw/world semantic gate, exact native scopes, remote filter, keyboard use and independent release tails; owner physical UI/gameplay remains NOT_EXECUTED.");
        }
        private static void PreferencesAndLifecycle(object context, object host, object input, object keys)
        {
            var preference = (PreferenceDocument<bool>)Get(host, "preference");
            var state = (BlueprintCaptureState)Get(host, "State"); var p = Main.LocalPlayer;
            var bindings = (HotkeyBindings)Get(keys, "Bindings");
            HotkeyChord capture = null, save = null; string reason; long command;
            Require(HotkeyChord.TryParse("K", out capture, out reason) && HotkeyChord.TryParse("L", out save, out reason), "test bindings parse");
            Require(bindings.TrySet("blueprint.capture", capture, null, out command, out reason), "capture binding enters common save pipeline");
            NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
            Require(!bindings.TrySet("blueprint.save", capture, null, out command, out reason) && bindings.Get("blueprint.capture").Equals(capture), "same-context conflict cannot replace the effective capture binding");
            Require(bindings.TrySet("blueprint.save", save, null, out command, out reason), "disabled save action remains editable");
            NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
            Require(bindings.Get("blueprint.save").Equals(save), "save binding publishes only after actual reliable persistence");
            Call(host, "SetToolbar", true);
            NativeQuickItemChecks.Until(() => preference.Snapshot.Status == PreferenceStatus.Saved);
            var path = Path.Combine((string)Get(context, "gameDirectory"), "JueMingRData", "config", "features", "blueprint.json");
            Require(new BlueprintPreferenceCodec().Decode(File.ReadAllBytes(path)), "real preference worker persists only the toolbar bool");
            Item item = p.inventory[0]; int selected = p.selectedItem; bool dead = p.dead, menu = Main.gameMenu;
            bool map = PlayerInput.Triggers.Current.MapFull, camera = PlayerInput.Triggers.Current.ToggleCameraMode;
            try
            {
                p.inventory[0] = new Item(); p.inventory[0].SetDefaults(Terraria.ID.ItemID.Gel); p.selectedItemState.Select(0); p.selectedItemState.Update();
                Sample(input, false, false, false, false);
                Require((bool)Get(host, "ShowToolbar"), "actual held gel and explicit preference display the bar");
                PlayerInput.Triggers.Current.MapFull = true; Require(!(bool)Get(host, "ShowToolbar"), "map request hides toolbar before native map flag changes");
                PlayerInput.Triggers.Current.MapFull = false; PlayerInput.Triggers.Current.ToggleCameraMode = true;
                Require(!(bool)Get(host, "ShowToolbar"), "capture request hides toolbar before native capture flag changes");
                PlayerInput.Triggers.Current.ToggleCameraMode = false;
                state.Enter(); state.Selection.ToggleRectangle(1, 1, 2, 2, Main.maxTilesX, Main.maxTilesY);
                p.inventory[0] = new Item(); p.inventory[0].SetDefaults(9);
                Require(!(bool)Get(host, "ShowToolbar") && state.Active && state.Selection.HasSelection && (bool)Get(host, "WorldActionsBlocked"), "non-gel only hides toolbar and retains world editor");
                Call(host, "SetToolbar", false);
                Require(state.Active && state.Selection.HasSelection, "closing preference is not capture exit");
                p.dead = true; Call(host, "Update", 1UL);
                Require(state.Active && state.Selection.HasSelection && state.NeedsNeutral, "death cancels only unsubmitted pointer state");
                p.dead = false; Sample(input, true, false, true, false);
                Main.gameMenu = true; Call(context, "UpdateRuntime");
                Require(!state.Active && !state.Selection.HasSelection && state.BlockedActions == 1, "real Runtime session end clears editor and preserves held world tail");
            }
            finally
            {
                p.dead = dead; Main.gameMenu = menu; p.inventory[0] = item; p.selectedItemState.Select(selected); p.selectedItemState.Update();
                PlayerInput.Triggers.Current.MapFull = map; PlayerInput.Triggers.Current.ToggleCameraMode = camera;
                Call(context, "UpdateRuntime"); Sample(input, false, false, false, false); Sample(input, false, false, false, false);
            }
        }
        private static void PointerFlow(object host, object input)
        {
            var state = (BlueprintCaptureState)Get(host, "State"); var pointer = Get(host, "Pointer");
            var type = pointer.GetType(); var p = Main.LocalPlayer;
            bool mouseInterface = p.mouseInterface, mouseText = Main.mouseText, inventory = Main.playerInventory;
            var camera = Main.screenPosition; var zoom = Main.GameViewMatrix.Zoom; float ui = Main.UIScale;
            int mouseX = Main.mouseX, mouseY = Main.mouseY;
            var cacheNames = new[] { "_originalMouseX", "_originalMouseY", "_originalLastMouseX", "_originalLastMouseY", "_originalScreenWidth", "_originalScreenHeight" };
            var cache = cacheNames.Select(name => typeof(PlayerInput).GetField(name, Flags).GetValue(null)).ToArray();
            Action<bool> publish = blocked =>
            {
                // Drive the same bounded hook endpoints without a GPU. This
                // proves certificate state/order, not native Draw or pixels.
                p.mouseInterface = blocked; Main.mouseText = false;
                type.GetMethod("Begin", Flags).Invoke(null, null);
                type.GetMethod("Boundary", Flags).Invoke(null, null);
                type.GetMethod("Resources", Flags).Invoke(null, new object[] { true });
                type.GetMethod("End", Flags).Invoke(null, new object[] { true });
                p.mouseInterface = false;
            };
            Action<bool, bool> sample = (left, blocked) =>
            {
                Sample(input, left, false, left, false); Call(pointer, "Capture"); publish(blocked);
                Sample(input, left, false, left, false); Call(pointer, "Consume", true);
            };
            try
            {
                Main.playerInventory = false; p.mouseInterface = Main.mouseText = false; state.Selection.Clear(); state.Interrupt();
                sample(false, false); sample(true, false);
                Require(state.Dragging && !state.Selection.HasSelection, "certified press starts one uncommitted draft");
                sample(false, false); Require(state.Selection.HasSelection && !state.Dragging, "certified release commits once in Update consumer");
                Call(pointer, "Consume", true); Require(state.Selection.HasSelection, "repeated Draw/empty consumption cannot XOR again");
                sample(true, true); Require(!state.Dragging && state.Selection.HasSelection && state.NeedsNeutral, "current native UI ownership cancels draft without touching committed set");
                sample(true, false); Require(!state.Dragging, "UI drag leaving panel cannot turn held input into a world press");
                sample(false, false);
                Sample(input, true, false, true, false); Call(pointer, "Capture");
                Main.screenPosition += new Microsoft.Xna.Framework.Vector2(16, 8); publish(false);
                Sample(input, true, false, true, false); Call(pointer, "Consume", true);
                Require(state.Dragging, "camera travel does not permanently invalidate current UI proof");
                state.Interrupt();
                foreach (float factor in new[] { .3f, 1f, 10f })
                {
                    Main.GameViewMatrix.Zoom = new Microsoft.Xna.Framework.Vector2(factor); NativeBlueprintUiChecks.SetScale(1.5f);
                    Sample(input, false, false, false, false); Call(pointer, "Capture");
                    Main.mouseX = 200; Main.mouseY = 200; PlayerInput.CacheZoomableValues(); PlayerInput.SetZoom_World();
                    var point = Main.MouseWorld;
                    Require((int)Get(pointer, "CellX") == (int)Math.Floor(point.X / 16) && (int)Get(pointer, "CellY") == (int)Math.Floor(point.Y / 16), "world inverse zoom ignores UI matrix at " + factor);
                }
                Call(pointer, "Cancel"); state.Interrupt();
            }
            finally
            {
                Main.GameViewMatrix.Zoom = zoom; NativeBlueprintUiChecks.SetScale(ui); Main.screenPosition = camera;
                Main.mouseX = mouseX; Main.mouseY = mouseY;
                for (int i = 0; i < cacheNames.Length; i++) typeof(PlayerInput).GetField(cacheNames[i], Flags).SetValue(null, cache[i]);
                p.mouseInterface = mouseInterface; Main.mouseText = mouseText; Main.playerInventory = inventory; Call(pointer, "Cancel"); state.Interrupt();
            }
        }
        private static void Sample(object input, bool left, bool right, bool item, bool tile)
        {
            Call(input, "BeginUpdate"); FocusHelper.IsSelectedApplication = true;
            PlayerInput.MouseInfo = new MouseState(200, 200, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
                right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Main.keyState = new KeyboardState(); Main.mouseLeft = left; Main.mouseRight = right;
            var tokens = new List<string>(); if (left) tokens.Add("Mouse1"); if (right) tokens.Add("Mouse2");
            Call(input, "AfterNativeMouse", tokens); Require(tokens.Count == (left ? 1 : 0) + (right ? 1 : 0), "capture mode does not delete native Mouse1/2 tokens");
            PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = item; PlayerInput.Triggers.Current.MouseRight = tile;
            Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh");
        }
    }
}
