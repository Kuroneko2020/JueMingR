using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using JueMingR.Features.HiddenFeatures;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real Shell/Bindings/Stop with controlled trusted physical samples. The
    // retained mouseInterface value represents vanilla's preceding Draw hover;
    // this CPU check does not certify real pixels, physical IME or a server.
    internal static class NativeJourneyCloseAdmissionChecks
    {
        internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            Prepare(context);
            object journey = Get(context, "Journey"), shell = Get(context, "Shell");
            var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
            foreach (int mode in new[] { 0, 1 })
            {
                Main.netMode = mode; if (mode == 1) Netplay.Connection.State = 10;
                Call(context, "UpdateRuntime"); Frame(context); Frame(context);
                Bind(bindings, "F13");
                Require((bool)Call(journey, "SetEnabled", true), "real Journey opens in supported mode=" + mode + ": " + GetOptional(journey, "LastFailure"));
                var god = CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>();
                bool originalGod = god.IsEnabledForPlayer(Main.myPlayer); god.SetEnabledState(Main.myPlayer, !originalGod);
                Main.LocalPlayer.mouseInterface = Main.LocalPlayer.lastMouseInterface = true;
                Frame(context, new[] { Keys.F13 });
                Require(!(bool)Get(journey, "IsActive"), "non-text native-menu hover admits the real bound close in mode=" + mode);
                Require(god.IsEnabledForPlayer(Main.myPlayer) == originalGod, "close returns the original personal ability through Journey.Stop");
                Frame(context, new[] { Keys.F13 });
                Require(!(bool)Get(journey, "IsActive"), "held close tail cannot reopen Journey");
                Main.LocalPlayer.mouseInterface = Main.LocalPlayer.lastMouseInterface = false; Frame(context); Frame(context);
                Bind(bindings, "Mouse4");
                Require((bool)Call(journey, "SetEnabled", true), "mouse-binding scene opens");
                Main.LocalPlayer.mouseInterface = Main.LocalPlayer.lastMouseInterface = true;
                Frame(context, null, true);
                Require(!(bool)Get(journey, "IsActive"), "mouse binding reaches the same native-menu Stop in mode=" + mode);
                Frame(context, null, true);
                Require(!(bool)Get(journey, "IsActive"), "held mouse close tail cannot reopen Journey");
                Main.LocalPlayer.mouseInterface = Main.LocalPlayer.lastMouseInterface = false;
                Frame(context); Frame(context);
                Bind(bindings, "F13"); Require((bool)Call(journey, "SetEnabled", true), "non-hover scene opens");
                Frame(context, new[] { Keys.F13 });
                Require(!(bool)Get(journey, "IsActive"), "ordinary non-hover close remains available in mode=" + mode);
                Frame(context); Frame(context);
            }
            Bind(bindings, "F13"); Main.netMode = 1; Call(context, "UpdateRuntime");
            Require((bool)Call(journey, "SetEnabled", true), "text-protection scene opens");
            Main.LocalPlayer.mouseInterface = Main.LocalPlayer.lastMouseInterface = true;
            foreach (int owner in new[] { 0, 1, 2, 3, 4 })
            {
                Main.CurrentInputTextTakerOverride = owner == 0 ? new object() : null;
                PlayerInput.WritingText = owner == 1; Main.drawingPlayerChat = owner == 2;
                Main.editSign = owner == 3; Main.editChest = owner == 4;
                Frame(context); Frame(context, new[] { Keys.F13 });
                Require((bool)Get(journey, "IsActive"), "actual text/IME/chat owner protects the close key: " + owner);
                Main.CurrentInputTextTakerOverride = null; PlayerInput.WritingText = Main.drawingPlayerChat = Main.editSign = Main.editChest = false;
                Frame(context); Frame(context);
            }
            object input = Get(context, "Input");
            Set(input, "foregroundWindow", (Func<IntPtr>)(() => IntPtr.Zero));
            Frame(context, new[] { Keys.F13 });
            Require(!(bool)Get(input, "SampleFocused") && (bool)Get(journey, "IsActive"), "unfocused real input sample cannot close Journey");
            Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1)));
            Frame(context, new[] { Keys.F13 });
            Require((bool)Get(journey, "IsActive"), "focus return with the old held key cannot close Journey");
            Frame(context); Frame(context);
            Frame(context, new[] { Keys.F13 });
            Require(!(bool)Get(journey, "IsActive"), "fresh close works after all text owners release");
            Frame(context); Frame(context);
            CaptureAndButton(context);
            Console.WriteLine("PASS Journey real Shell/Bindings keyboard/mouse close, modes 0/1, native hover/non-hover, personal return, text/sign/chest/focus/held-tail, actual Record capture and F5 button; renderer-only CPU substitute, physical IME/Draw/network remain unverified.");
        }

        private static void CaptureAndButton(object context)
        {
            var originalPixel = Terraria.GameContent.TextureAssets.MagicPixel;
            var originalBack = Terraria.GameContent.TextureAssets.InventoryBack;
            try
            {
            // The CPU fixture never draws. Reuse the existing metadata-only
            // XNA asset seam so the real renderer can publish availability;
            // do not force State.Ready or any popup/business result.
            Terraria.GameContent.TextureAssets.MagicPixel = CpuTexture(1, 1);
            Terraria.GameContent.TextureAssets.InventoryBack = CpuTexture(52, 52);
            object shell = Get(context, "Shell"), state = Get(shell, "State"), journey = Get(context, "Journey"), input = Get(context, "Input");
            Require((bool)Call(journey, "SetEnabled", true) && (bool)Call(journey, "CloseMenu"), "capture scene retains active Journey after real native window close");
            Main.LocalPlayer.mouseInterface = Main.LocalPlayer.lastMouseInterface = false;
            FiniteCostChecks.SetCpuFont(10); Call(context, "UpdateShell");
            Require((bool)Get(state, "Ready"), "renderer-only CPU resources publish actual Shell readiness");
            NativeToolsUiChecks.Prepare(context, 1, 1280, 900, 1);
            object popup = Get(shell, "HotkeyPopup");
            Type rectType = shell.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect", true);
            object anchor = Activator.CreateInstance(rectType, Flags, null, new object[] { 100f, 100f, 40f, 20f }, null);
            Call(shell, "OpenHotkey", "journey.toggle", anchor); Call(shell, "OpenHotkey", "journey.toggle", anchor);
            Require((bool)Get(popup, "Visible"), "actual configurable Journey popup opened");
            // Let the real outer update publish the popup's current geometry;
            // the established UI fixture also supplies native mapped mouse bits
            // and coordinates consistently before its trusted final sample.
            NativeToolsUiChecks.UiFrame(context, Microsoft.Xna.Framework.Vector2.Zero, false);
            object layout = Get(popup, "Layout"), panel = Get(layout, "Panel"); var commands = (IList)Get(layout, "Commands");
            int record = Enumerable.Range(0, commands.Count).Single(i => commands[i].ToString() == "Record");
            object button = Get(((IList)Get(layout, "Buttons"))[record], "Rect");
            int x = (int)((float)Get(panel, "X") + (float)Get(button, "X") + 4), y = (int)((float)Get(panel, "Y") + (float)Get(button, "Y") + 4);
            NativeToolsUiChecks.Click(context, new Microsoft.Xna.Framework.Vector2(x, y));
            Require((bool)Get(popup, "Capturing") && (bool)Get(input, "HotkeyCapture"), "real Record release owns public hotkey capture; visible=" + Get(popup, "Visible") + " hovered=" + Get(popup, "Hovered") + " pressed=" + Get(popup, "Pressed") + " ready=" + Get(state, "Ready") + " shell-visible=" + Get(state, "Visible") + " focused=" + Get(input, "SampleFocused"));
            Frame(context, new[] { Keys.F13 });
            Require((bool)Get(journey, "IsActive"), "actual recording consumes the bound close without stopping Journey");
            Call(popup, "Close"); Call(state, "Close");
            Frame(context, new[] { Keys.F13 });
            Require((bool)Get(journey, "IsActive"), "recording exit retains the captured key's release tail");
            Frame(context); Frame(context);

            // Reuse the existing real F5 press/release route, with CPU font
            // geometry. Its command still reaches the same Journey.Stop.
            NativeToolsUiChecks.Prepare(context, 1, 1280, 900, 1);
            layout = Get(state, "Layout");
            object element = ((IEnumerable)Get(layout, "Elements")).Cast<object>().Single(e => Get(e, "Command").ToString() == "DisableJourney");
            object rect = Get(element, "Rect"), view = Get(layout, "Viewport");
            Call(state, "ScrollTo", Math.Max(0, (float)Get(rect, "Bottom") - (float)Get(view, "Height") + 4));
            var point = new Microsoft.Xna.Framework.Vector2((float)Get(state, "X") + (float)Get(view, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2,
                (float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(rect, "Y") - (float)Get(state, "Scroll") + (float)Get(rect, "Height") / 2);
            var god = CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>(); bool baseline = god.IsEnabledForPlayer(Main.myPlayer);
            god.SetEnabledState(Main.myPlayer, !baseline);
            NativeToolsUiChecks.Click(context, point);
            Require(!(bool)Get(journey, "IsActive") && god.IsEnabledForPlayer(Main.myPlayer) == baseline, "real F5 Disable button still reaches Stop and returns personal powers");
            }
            finally
            {
                Terraria.GameContent.TextureAssets.MagicPixel = originalPixel;
                Terraria.GameContent.TextureAssets.InventoryBack = originalBack;
            }
        }
        private static Asset<Texture2D> CpuTexture(int width, int height)
        { return (Asset<Texture2D>)typeof(NativeTargetOwnerChecks).GetMethod("Texture", Flags).Invoke(null, new object[] { width, height }); }

        internal static void Prepare(object context)
        {
            typeof(NativeJourneyChecks).GetMethod("Prepare", Flags).Invoke(null, new[] { context });
            // This is the non-drawing CPU fixture: no content manager exists to
            // construct CreativeUI's textures/UI tree. Keep its real menu,
            // ToggleMenu/slot-return and Journey admission/Stop untouched; only
            // retire that renderer-only initialization prerequisite. The native
            // navigator and an already-open inventory supply the ordinary game
            // prerequisites without writing Enabled or Journey.Active.
            Set(Main.CreativeMenu, "_initialized", true);
            if (Terraria.UI.Gamepad.UILinkPointNavigator.Points.Count == 0)
                Terraria.Initializers.UILinksInitializer.Load();
            Main.playerInventory = true;
            var access = (HiddenFeatureAccess)Get(context, "hiddenAccess");
            for (int i = 0; i < 5; i++) access.TryCompleteTitleClick(100 + i * 100, i + 1);
            object input = Get(context, "Input"), shell = Get(context, "Shell");
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1)));
            Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1)));
            Set(input, "ReadOnlyKeyboardSource", (Func<KeyboardState>)(() => Main.keyState));
            Set(shell, "LayersReady", true); Set(Get(shell, "State"), "Ready", true);
            Call(Get(shell, "hotkeys"), "PollHiddenRegistration");
            Frame(context); Frame(context);
        }
        internal static void Bind(HotkeyBindings bindings, string text)
        {
            NativeQuickItemChecks.Until(() => { bindings.Poll(); return bindings.Loaded && !bindings.Busy; });
            HotkeyChord chord; string reason; long command;
            Require(HotkeyChord.TryParse(text, out chord, out reason), "valid fixture chord " + text);
            Require(bindings.TrySet("journey.toggle", chord, null, out command, out reason), "same binding owner accepts fixture chord: " + reason);
            NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
        }
        internal static void Frame(object context, Keys[] keys = null, bool mouse4 = false, bool left = false, int x = 200, int y = 200)
        {
            object input = Get(context, "Input"), shell = Get(context, "Shell");
            Call(input, "BeginUpdate");
            if (!(bool)Get(input, "IsFocused")) Call(shell, "CancelForFocusLoss");
            Call(shell, "BeforeInput");
            PlayerInput.MouseInfo = new MouseState(x, y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released,
                mouse4 ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released);
            Main.mouseLeft = left; Main.mouseRight = false; Main.mouseX = x; Main.mouseY = y;
            PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 0;
            PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Update();
            Call(input, "AfterMapping"); Main.keyState = new KeyboardState(keys ?? new Keys[0]); Call(input, "AfterKeyboardRefresh");
            Call(shell, "ProcessInput"); Call(context, "UpdateRuntime"); Call(context, "UpdateShell");
        }
        internal static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, Flags);
            if (field != null) field.SetValue(target, value); else target.GetType().GetProperty(name, Flags).SetValue(target, value, null);
        }
        internal static void Require(bool condition, string message)
        { if (!condition) throw new JueMingR.Testing.BusinessAssertionException(message); }
    }
}
