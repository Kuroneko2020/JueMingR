using System;
using System.Reflection;
using Microsoft.Xna.Framework.Input;
using JueMingR.TerrariaHost.Input;
using Terraria.GameInput;

namespace Terraria
{
    internal static class HostInputChecks
    {
        internal static bool Foreground = true;
        internal static void ConfigureLoadedHost()
        {
            Assembly host = Array.Find(AppDomain.CurrentDomain.GetAssemblies(), x => x.GetName().Name == "JueMingR.TerrariaHost");
            Type worker = host.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true);
            object context = worker.GetField("postfixContext", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            object input = context.GetType().GetField("Input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(context);
            // This assembly has no native game window; substitute only the OS
            // observation boundary in the loaded production object, not logic.
            input.GetType().GetField("gameWindow", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(input, new Func<IntPtr>(() => new IntPtr(1)));
            input.GetType().GetField("foregroundWindow", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(input,
                new Func<IntPtr>(() => Foreground && FocusHelper.IsSelectedApplication ? new IntPtr(1) : new IntPtr(2)));
            Foreground = true;
        }
        internal static void Run()
        {
            bool foreground = true;
            var input = new HostInputState(() => new IntPtr(1), () => foreground ? new IntPtr(1) : new IntPtr(2));
            Frame(input, false);
            Check(input.CanStartActions, "normal neutral foreground admits new actions");
            Main.keyState = new KeyboardState(Keys.Z); foreground = false;
            input.BeginUpdate();
            Check(Main.keyState.IsKeyUp(Keys.Z) && !input.CanStartActions, "loss prevents earlier cached keyboard consumers");
            Check(!input.RestrictNativePermission(true), "stale native application selection cannot allow a different foreground window");
            // Deliberately emulate a too-permissive native mapping. The earlier
            // mapping seam must still stop actual consumers and all edge packs.
            PlayerInput.Triggers.Current.KeyStatus["ViewZoomIn"] = true;
            PlayerInput.Triggers.JustReleased.MouseLeft = true;
            PlayerInput.ScrollWheelValue = 720; PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 240;
            input.AfterMapping(); Main.keyState = new KeyboardState(Keys.F5); input.AfterKeyboardRefresh();
            Check(!PlayerInput.Triggers.Current.KeyStatus["ViewZoomIn"] && !PlayerInput.Triggers.JustReleased.MouseLeft && Main.keyState.IsKeyUp(Keys.F5), "background mapping and fresh raw keys cannot escape");
            Check(PlayerInput.ScrollWheelValue == 720 && PlayerInput.ScrollWheelDelta == 0 && PlayerInput.ScrollWheelDeltaForUI == 0, "consume wheel delta without rewinding native absolute baseline");
            foreground = true;
            Frame(input, true, Keys.F5);
            Check(!input.CanStartActions && !PlayerInput.Triggers.Current.MouseLeft && Main.keyState.IsKeyUp(Keys.F5), "activating click/chord cannot operate a prior target");
            Frame(input, true);
            Check(!input.CanStartActions, "held activating pointer remains denied without a fixed timeout");
            Frame(input, false);
            Check(!input.CanStartActions, "neutral activation tail is itself consumed");
            Frame(input, true, Keys.F5);
            Check(input.CanStartActions && PlayerInput.Triggers.Current.MouseLeft && Main.keyState.IsKeyDown(Keys.F5), "next independent click and F5 work");
            input.BeginUpdate();
            Check(!input.CanStartActions, "a skipped native input stage cannot reuse last frame permission");
            Check(!input.RestrictNativePermission(false), "host gate never broadens a native denial");
            var unavailable = new HostInputState(() => IntPtr.Zero, () => IntPtr.Zero);
            unavailable.BeginUpdate(); Check(!unavailable.IsFocused, "null foreground/window is not a match");
            Main.keyState = default(KeyboardState); Main.SampleLeft = Main.SampleF5 = false;
            PlayerInput.Triggers = new TriggersPack(); PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 0;
            Console.WriteLine("PASS: foreground, current input epoch, activation chord and wheel guards.");
        }
        private static void Frame(HostInputState input, bool left, params Keys[] keys)
        {
            input.BeginUpdate(); input.RestrictNativePermission(true);
            PlayerInput.MouseInfo = new MouseState(20, 20, PlayerInput.ScrollWheelValue, left ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            PlayerInput.Triggers.Current.MouseLeft = left; Main.mouseLeft = left;
            input.AfterMapping(); Main.keyState = new KeyboardState(keys); input.AfterKeyboardRefresh();
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
