using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using JueMingR.Platform.Hotkeys;

namespace JueMingR.TerrariaHost.Input
{
    // One game-thread input epoch. Window activation is not a Session boundary:
    // native receipts keep their original generation while new actions wait.
    internal sealed class HostInputState
    {
        private readonly Func<IntPtr> gameWindow, foregroundWindow;
        private bool observed, rearming, quarantine, mapped, finalized, nativePermission;
        private Dictionary<string, bool> mappedKeys;
        private string[] keys = new string[0];
        private readonly bool[] physical = new bool[HotkeyChord.KeyCount];
        internal readonly HotkeyInput Hotkeys = new HotkeyInput();
        internal KeyboardState KeyboardSample { get; private set; }
        internal int MapMouseX { get; private set; }
        internal int MapMouseY { get; private set; }
        internal bool HotkeyCapture { get; set; }
        internal Func<bool> ClaimsHotkeyPointer { get; set; }
        internal Func<bool> ClaimsMapPointer { get; set; }
        internal Func<int, int, int> ClaimsReadOnlyGesture { get; set; }
        internal int ReadOnlyMouseMask { get; private set; }
        private int readOnlyTail;
        internal int MapWheel { get; private set; }
        internal int PhysicalMapX { get; private set; }
        internal int PhysicalMapY { get; private set; }
        internal int OwnedMouseMask { get; private set; }
        internal bool MapPointerOwned { get { return OwnedMouseMask != 0 || mapWheelOwned; } }
        private int mouseTail, previousMouse;
        private bool mapWheelOwned;
        internal void AfterNativeMouse(List<string> mouseKeys)
        {
            RefreshFocus(); MouseState sample = PlayerInput.MouseInfo;
            PhysicalMapX = (int)(sample.X * PlayerInput.RawMouseScale.X); PhysicalMapY = (int)(sample.Y * PlayerInput.RawMouseScale.Y);
            int down = (sample.LeftButton == ButtonState.Pressed ? 1 : 0) | (sample.RightButton == ButtonState.Pressed ? 2 : 0) |
                (sample.MiddleButton == ButtonState.Pressed ? 4 : 0) | (sample.XButton1 == ButtonState.Pressed ? 8 : 0) | (sample.XButton2 == ButtonState.Pressed ? 16 : 0);
            bool reliable = IsFocused && FocusHelper.IsSelectedApplication && !rearming;
            bool claims = reliable && ClaimsMapPointer != null && ClaimsMapPointer();
            if (claims) mouseTail |= down & ~previousMouse;
            if (reliable && !claims && ClaimsReadOnlyGesture != null) readOnlyTail |= ClaimsReadOnlyGesture(down, down & ~previousMouse) & down & ~previousMouse & 31;
            OwnedMouseMask = mouseTail; mapWheelOwned = claims;
            ReadOnlyMouseMask = readOnlyTail;
            // MouseInput only staged these tokens; keyboard and gamepad mappings
            // have not merged yet. Filtering here preserves shared keyboard binds.
            for (int i = mouseKeys.Count - 1; i >= 0; i--)
            {
                string key = mouseKeys[i];
                if (key.Length == 6 && key.StartsWith("Mouse", StringComparison.Ordinal) && key[5] >= '1' && key[5] <= '5' && ((OwnedMouseMask | ReadOnlyMouseMask) & 1 << (key[5] - '1')) != 0) mouseKeys.RemoveAt(i);
            }
            if (reliable) { mouseTail &= down; readOnlyTail &= down; previousMouse = down; }
        }
        internal void ConsumeOwnedMouseEdges()
        { for (int i = 0; i < 5; i++) if ((OwnedMouseMask & 1 << i) != 0) Hotkeys.ConsumePress(256 + i); }
        private bool hotkeyTailSample;
        internal bool HotkeyPointerOwned { get { return HotkeyCapture || Hotkeys.HasSuppressedKeys || hotkeyTailSample; } }
        internal bool IsFocused { get; private set; }
        internal bool SampleFocused { get { return mapped && IsFocused && nativePermission; } }
        internal bool CanUseInput { get { return SampleFocused && finalized && !quarantine; } }
        internal bool CanStartActions { get { return CanUseInput; } }
        internal long Frame { get; private set; }
        internal bool CanPrepareText { get { return IsFocused && !rearming && !quarantine; } }
        internal HostInputState() : this(() => Main.instance == null ? IntPtr.Zero : Main.instance.Window.Handle, GetForegroundWindow) { }
        internal HostInputState(Func<IntPtr> gameWindow, Func<IntPtr> foregroundWindow)
        { this.gameWindow = gameWindow; this.foregroundWindow = foregroundWindow; }

        internal void BeginUpdate()
        {
            Frame++;
            mapped = finalized = nativePermission = false;
            RefreshFocus();
            quarantine = !IsFocused || rearming;
            // .8's F7-F11/Alt+Enter precede its current keyboard refresh. A stale
            // cached sample must not operate the game on loss or reactivation.
            if (quarantine || HotkeyCapture || Hotkeys.HasSuppressedKeys)
            {
                Main.keyState = default(KeyboardState);
                ClearTextActions();
            }
        }
        private void RefreshFocus()
        {
            bool focused;
            try { IntPtr window = gameWindow(); focused = window != IntPtr.Zero && foregroundWindow() == window; }
            catch { focused = false; }
            if (!focused || observed && !IsFocused && focused) rearming = true;
            IsFocused = focused; observed = true;
        }
        internal bool RestrictNativePermission(bool permitted)
        { return permitted && IsFocused; }

        internal void AfterMapping()
        {
            // This seam precedes UpdateViewZoomKeys and UILinkPointNavigator.
            // Keep MouseInfo and the absolute wheel owned by Terraria; they are
            // observations, not values to restore after consuming an action.
            RefreshFocus(); mapped = true;
            MapMouseX = PlayerInput.MouseX; MapMouseY = PlayerInput.MouseY;
            MapWheel = PlayerInput.ScrollWheelDeltaForUI;
            if (MapPointerOwned) PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 0;
            // Do not depend on interception of the tiny native getter: a caller
            // may have inlined it before patching. Mapping is independently
            // guarded below; this native field only distinguishes its synthetic
            // mouse sample from a focused physical sample for release proof.
            nativePermission = FocusHelper.IsSelectedApplication;
            quarantine |= !IsFocused || rearming;
            if (quarantine) { ConsumeMappedInput(); ClearTextActions(); }
            else if (HotkeyCapture || Hotkeys.HasSuppressedKeys || ClaimsHotkeyPointer != null && ClaimsHotkeyPointer()) ConsumeHotkeyActions();
        }
        internal void AfterKeyboardRefresh()
        {
            finalized = mapped;
            KeyboardSample = Main.keyState;
            for (int key = 0; key < 256; key++) physical[key] = KeyboardSample.IsKeyDown((Keys)key);
            MouseState physicalMouse = PlayerInput.MouseInfo;
            physical[256] = physicalMouse.LeftButton == ButtonState.Pressed; physical[257] = physicalMouse.RightButton == ButtonState.Pressed;
            physical[258] = physicalMouse.MiddleButton == ButtonState.Pressed; physical[259] = physicalMouse.XButton1 == ButtonState.Pressed; physical[260] = physicalMouse.XButton2 == ButtonState.Pressed;
            // Keep the final release frame owned even though Update retires its
            // held bit. Independent UI consumers also read physical MouseInfo.
            hotkeyTailSample = Hotkeys.HasSuppressedKeys;
            Hotkeys.Update(physical, SampleFocused);
            if (HotkeyCapture || Hotkeys.HasSuppressedKeys) ConsumeHotkeyActions();
            if (!quarantine) return;
            KeyboardState sample = KeyboardSample;
            Main.keyState = default(KeyboardState);
            // A native synthesized release is never neutral proof. The complete
            // activating chord must genuinely end; this is not a timed cooldown.
            MouseState mouse = PlayerInput.MouseInfo;
            if (SampleFocused && mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released &&
                mouse.MiddleButton == ButtonState.Released && mouse.XButton1 == ButtonState.Released && mouse.XButton2 == ButtonState.Released &&
                sample.GetPressedKeys().Length == 0) rearming = false;
            // The neutral/activation sample itself remains consumed. Next fresh
            // input can act, without replaying a key, wheel delta or old target.
        }
        private void ConsumeMappedInput()
        {
            ConsumeHotkeyActions();
            PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 0;
        }
        internal void ConsumeHotkeyActions()
        {
            var pack = PlayerInput.Triggers;
            var currentKeys = pack.Current.KeyStatus;
            if (!ReferenceEquals(mappedKeys, currentKeys) || keys.Length != currentKeys.Count)
            { mappedKeys = currentKeys; keys = new string[currentKeys.Count]; currentKeys.Keys.CopyTo(keys, 0); }
            Clear(pack.Current); Clear(pack.Old); Clear(pack.JustPressed); Clear(pack.JustReleased);
            Main.mouseLeft = Main.mouseRight = false;
            Main.keyState = default(KeyboardState);
            ClearTextActions();
        }
        private void Clear(TriggersSet set)
        { foreach (string key in keys) if (set.KeyStatus.ContainsKey(key)) set.KeyStatus[key] = false; }
        internal static void ClearTextActions()
        {
            // Chat reads Escape before GetInputText. These are per-call action
            // outputs, not chat content, text queues, IME or ownership leases.
            Main.inputTextEnter = Main.inputTextEscape = false;
        }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    }
}
