using System;
using System.Collections;
using System.Reflection;
using JueMingR.Features.DeathHistory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeDeathInputChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(object context, object host)
        {
            object shell = Get(context, "Shell"), input = Get(context, "Input"), state = Get(shell, "State"), popup = Get(shell, "DeathPopup"), renderer = Get(shell, "renderer");
            bool focused = true, mapRequest = false;
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => focused ? new IntPtr(1) : IntPtr.Zero));
            typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity);
            typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, 960); typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, 640);
            typeof(PlayerInput).GetField("RawMouseScale", Flags).SetValue(null, Vector2.One);
            Main.dedServ = true; try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Terraria.Graphics.Capture.CaptureManager).TypeHandle); } finally { Main.dedServ = false; }
            PlayerInput.Triggers.Initialize(); Main.blockInput = false; Main.LocalPlayer.mouseInterface = Main.mouseText = false;
            FiniteCostChecks.SetCpuFont(8); Call(renderer, "RefreshResources"); Set(state, "Ready", true);
            var prepare = popup.GetType().GetMethod("Prepare", Flags);
            var measure = Delegate.CreateDelegate(prepare.GetParameters()[3].ParameterType, renderer, renderer.GetType().GetMethod("PopupMeasure", Flags));
            Action layout = () => prepare.Invoke(popup, new object[] { 960f, 640f, Get(renderer, "FontIdentity"), measure, Get(renderer, "SkinGeneration") });
            Action<int, int, bool> frame = (x, y, down) =>
            {
                Main.keyState = new KeyboardState(); PlayerInput.MouseInfo = new MouseState(x, y, 0, down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = down; PlayerInput.Triggers.Update(); Main.mouseLeft = down; FocusHelper.IsSelectedApplication = focused;
                // Held modal pointer input intentionally suppresses ordinary
                // map key actions. Exercise a forced native map transition.
                Main.mapFullscreen = mapRequest;
                Call(input, "BeginUpdate"); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh"); Call(shell, "ProcessInput");
                Require(!(bool)Get(shell, "Failed"), "actual death input shell must stay available");
            };
            Action open = () => { frame(0, 0, false); frame(0, 0, false); Set(state, "Ready", true); Call(state, "Navigate", 2); Call(state, "RestoreVisible"); Call(popup, "Open", true, 2); Call(renderer, "RefreshResources"); layout(); };
            Func<int, Vector2> button = command =>
            {
                var commands = (IList)Get(popup, "Commands"); var buttons = (IList)Get(popup, "Buttons"); var rect = Get(buttons[commands.IndexOf(command)], "Rect"); var panel = Get(popup, "Panel");
                return new Vector2((float)Get(rect, "X") + (float)Get(panel, "X") + 4, (float)Get(rect, "Y") + (float)Get(panel, "Y") + 4);
            };
            Func<Vector2> option = () => button(128);
            open(); var point = option(); frame((int)point.X, (int)point.Y, true);
            Require((int)Get(popup, "Pressed") == 128, "actual shell forwards physical press into death popup");
            FiniteCostChecks.SetCpuFont(8); // Same dimensions; do not pre-refresh or Prepare before release.
            frame((int)point.X, (int)point.Y, false);
            Require(((DeathDisplayPreferences)Get(host, "Settings")).Count == 512, "shell detects replaced font before stale release can select 128");
            foreach (bool loseFocus in new[] { true, false })
            {
                open(); point = option(); frame((int)point.X, (int)point.Y, true); focused = !loseFocus; mapRequest = !loseFocus;
                frame((int)point.X, (int)point.Y, false);
                Require(!(bool)Get(popup, "Visible") && ((DeathDisplayPreferences)Get(host, "Settings")).Count == 512, "focus loss or fullscreen transition closes without delivering old release");
                focused = true; mapRequest = false;
            }
            open(); point = option(); frame((int)point.X, (int)point.Y, true); frame((int)point.X, (int)point.Y, false);
            Require(((DeathDisplayPreferences)Get(host, "Settings")).Count == 128, "ordinary actual-shell click still performs exactly the intended quantity change");
            Call(popup, "Open", false, 2); Wait(() => (bool)Get(host, "QueryReady")); layout(); point = button(10);
            frame((int)point.X, (int)point.Y, true); frame((int)point.X, (int)point.Y, false);
            Require((int)Get(popup, "Mode") == 3, "actual shell cause click opens full original");
            Wait(() => (bool)Get(host, "QueryReady")); layout(); point = button(1);
            frame((int)point.X, (int)point.Y, true); frame((int)point.X, (int)point.Y, false);
            Require((int)Get(popup, "Mode") == 2 && (long)Get(popup, "Offset") == 0, "actual shell full-text back keeps page");
            Call(popup, "Close"); Call(host, "SetCount", 512);
            Console.WriteLine("PASS: actual F5 shell death-popup press/release, font identity before release, focus loss and fullscreen transition cancellation.");
        }
        private static void Wait(Func<bool> ready)
        { var timer = System.Diagnostics.Stopwatch.StartNew(); while (!ready()) { if (timer.ElapsedMilliseconds > 5000) throw new TimeoutException("death input query unavailable"); System.Threading.Thread.Sleep(5); } }
    }
}
