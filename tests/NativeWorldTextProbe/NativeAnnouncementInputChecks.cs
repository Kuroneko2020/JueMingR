using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeAnnouncementInputChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(object context)
        {
            object shell = Get(context, "Shell"), input = Get(context, "Input"), state = Get(shell, "State"), renderer = Get(shell, "renderer"), popup = Get(shell, "HotkeyPopup");
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1)));
            typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, 960); typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, 640);
            typeof(PlayerInput).GetField("RawMouseScale", Flags).SetValue(null, Vector2.One); PlayerInput.Triggers.Initialize();
            Main.blockInput = false; Main.LocalPlayer.mouseInterface = Main.mouseText = false; FocusHelper.IsSelectedApplication = true;
            Action<int, int, bool> frame = (x, y, down) =>
            {
                Main.keyState = new KeyboardState(); PlayerInput.MouseInfo = new MouseState(x, y, 0, down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = down; PlayerInput.Triggers.Update(); Main.mouseLeft = down;
                Call(input, "BeginUpdate"); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh"); Call(shell, "ProcessInput");
                Require(!(bool)Get(shell, "Failed"), "announcement double-click consumer remains available");
            };
            frame(0, 0, false); frame(0, 0, false); Set(state, "Ready", true); Call(state, "Navigate", 2); Call(state, "RestoreVisible");
            Call(renderer, "Prepare", state, 960f, 640f, 1f);
            frame(0, 0, false);
            // Hold only the click interval clock for this valid <=500ms gesture;
            // neither rendering nor machine/JIT speed is a double-click oracle.
            var clock = (System.Diagnostics.Stopwatch)Get(shell, "clickClock"); clock.Stop();
            try
            {
                foreach (string id in new[] { "announcement.send", "announcement.toggle" })
                {
                    var layout = Get(state, "Layout"); var view = Get(layout, "Viewport");
                    var element = ((IEnumerable)Get(layout, "Elements")).Cast<object>().Single(e => (string)GetOptional(e, "HotkeyTarget") == id);
                    var rect = Get(element, "Rect");
                    int x = (int)((float)Get(state, "X") + (float)Get(view, "X") + (float)Get(rect, "X") + 4);
                    int y = (int)((float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(rect, "Y") - (float)Get(state, "Scroll") + 4);
                    frame(x, y, false); frame(x, y, true); frame(x, y, false);
                    Require(!(bool)Get(popup, "Visible"), "one click does not open either binding entry");
                    frame(x, y, true); frame(x, y, false);
                    Require((bool)Get(popup, "Visible") && (string)Get(popup, "Target") == id, "actual F5 double-click " + id + " at " + x + "," + y + "; visible=" + Get(state, "Visible") + "; pointer=" + Get(state, "PointerX") + "," + Get(state, "PointerY") + "; last=" + GetOptional(popup, "lastClick") + "; input=" + Get(input, "CanUseInput"));
                    Call(popup, "Close"); frame(0, 0, false);
                }
            }
            finally { clock.Start(); Call(popup, "Close"); Call(state, "Close"); }
            Console.WriteLine("PASS: actual F5 physical press/release double-click opens send field and switch icon in the shared binding popup; no graphics device required.");
        }
    }
}
