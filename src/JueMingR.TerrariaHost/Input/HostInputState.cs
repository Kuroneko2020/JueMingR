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
        private bool observed, rearming, quarantine, mapped, finalized, nativePermission, nativeUiOwnsFrame;
        internal enum ExternalModalPhase { None, ModalOwned, AwaitFocusedNeutral }
        internal enum InventoryMouseClick { Native, Interface, CloseMenu }
        private Guid externalModalRequest;
        internal ExternalModalPhase ExternalModalState { get; private set; }
        internal bool ExternalModalBusy { get { return ExternalModalState != ExternalModalPhase.None; } }
        internal IntPtr CaptureGameWindow() { return gameWindow(); }
        internal bool TryAcquireExternalModal(Guid requestId)
        {
            if (requestId == Guid.Empty || ExternalModalBusy) return false;
            externalModalRequest = requestId; ExternalModalState = ExternalModalPhase.ModalOwned;
            // The accepted button is consumed before another native consumer can
            // see it. Physical ownership stays until trusted samples retire it.
            Hotkeys.SuppressHeld(); quarantine = true; ConsumeMappedInput();
            return true;
        }
        internal void ReturnExternalModal(Guid requestId)
        {
            if (externalModalRequest != requestId || ExternalModalState != ExternalModalPhase.ModalOwned) return;
            // Explicitly rearm even when Windows never produced an observable
            // unfocused epoch. Returning a path is not physical release proof.
            ExternalModalState = ExternalModalPhase.AwaitFocusedNeutral; rearming = quarantine = true;
            Hotkeys.SuppressHeld(); ConsumeMappedInput();
        }
        private uint worldAtBegin;
        private Dictionary<string, bool> mappedKeys;
        private string[] keys = new string[0];
        private readonly bool[] physical = new bool[HotkeyChord.KeyCount];
        private bool consumeInventoryOnNextMapping, inventoryHasUnownedMouseMapping;
        private int inventoryMouseTail;
        private static readonly Dictionary<string, Keys> keyboardCodes = BuildKeyboardCodes();
        private static Dictionary<string, Keys> BuildKeyboardCodes()
        {
            var result = new Dictionary<string, Keys>(StringComparer.Ordinal);
            foreach (Keys key in Enum.GetValues(typeof(Keys))) result[key.ToString()] = key;
            return result;
        }
        internal readonly HotkeyInput Hotkeys = new HotkeyInput();
        internal readonly HotkeyUseGesture UseGesture = new HotkeyUseGesture();
        internal void ClaimUseGesture(HotkeyChord chord)
        { if (CanStartActions) UseGesture.Claim(chord, Hotkeys, physical); }
        internal KeyboardState KeyboardSample { get; private set; }
        // A private early sample admits only read-only mouse ownership. Vanilla
        // keeps its cached keyboard mapping and its later final refresh intact.
        internal Func<KeyboardState> ReadOnlyKeyboardSource { get; set; }
        internal HotkeyModifiers ReadOnlyModifiers { get; private set; }
        internal bool ReadOnlySystemModifier { get; private set; }
        internal long MouseSample { get; private set; }
        internal long FocusGeneration { get; private set; }
        internal int MapMouseX { get; private set; }
        internal int MapMouseY { get; private set; }
        internal bool HotkeyCapture { get; set; }
        internal Func<bool> ClaimsHotkeyPointer { get; set; }
        internal Func<bool> ClaimsMapPointer { get; set; }
        internal Func<int, int, int> ClaimsReadOnlyGesture { get; set; }
        internal Func<bool> ReadOnlyGestureDemand { get; set; }
        internal int ReadOnlyMouseMask { get; private set; }
        private int readOnlyTail;
        internal int MapWheel { get; private set; }
        internal int PhysicalMapX { get; private set; }
        internal int PhysicalMapY { get; private set; }
        internal bool RawMouseLeft { get; private set; }
        internal bool RawMouseRight { get; private set; }
        internal bool NativeUseItemRequest { get; private set; }
        internal bool NativeUseTileRequest { get; private set; }
        internal bool NativeUiTransitionRequest { get; private set; }
        internal Action BlueprintMapping;
        internal int OwnedMouseMask { get; private set; }
        internal bool MapPointerOwned { get { return OwnedMouseMask != 0 || mapWheelOwned; } }
        private int mouseTail, previousMouse;
        private bool mapWheelOwned;
        internal void AfterNativeMouse(List<string> mouseKeys)
        {
            RefreshFocus(); MouseState sample = PlayerInput.MouseInfo;
            MouseSample++;
            RawMouseLeft = sample.LeftButton == ButtonState.Pressed;
            RawMouseRight = sample.RightButton == ButtonState.Pressed;
            PhysicalMapX = (int)(sample.X * PlayerInput.RawMouseScale.X); PhysicalMapY = (int)(sample.Y * PlayerInput.RawMouseScale.Y);
            int down = (sample.LeftButton == ButtonState.Pressed ? 1 : 0) | (sample.RightButton == ButtonState.Pressed ? 2 : 0) |
                (sample.MiddleButton == ButtonState.Pressed ? 4 : 0) | (sample.XButton1 == ButtonState.Pressed ? 8 : 0) | (sample.XButton2 == ButtonState.Pressed ? 16 : 0);
            bool reliable = IsFocused && FocusHelper.IsSelectedApplication && !rearming && !ExternalModalBusy;
            bool claims = reliable && ClaimsMapPointer != null && ClaimsMapPointer();
            if (claims) mouseTail |= down & ~previousMouse;
            if (reliable && !claims && ClaimsReadOnlyGesture != null && (ReadOnlyGestureDemand == null || ReadOnlyGestureDemand()))
            {
                if ((down & ~previousMouse) != 0)
                {
                    KeyboardState early = ReadOnlyKeyboardSource();
                    ReadOnlyModifiers = Modifiers(early);
                    ReadOnlySystemModifier = early.IsKeyDown(Keys.LeftWindows) || early.IsKeyDown(Keys.RightWindows);
                }
                readOnlyTail |= ClaimsReadOnlyGesture(down, down & ~previousMouse) & down & ~previousMouse & 31;
            }
            OwnedMouseMask = mouseTail; mapWheelOwned = claims;
            ReadOnlyMouseMask = readOnlyTail;
            // MouseInput only staged these tokens; keyboard and gamepad mappings
            // have not merged yet. Filtering here preserves shared keyboard binds.
            int excludedMouse = OwnedMouseMask | ReadOnlyMouseMask | inventoryMouseTail;
            for (int i = mouseKeys.Count - 1; i >= 0; i--)
            {
                string key = mouseKeys[i];
                if (key.Length == 6 && key.StartsWith("Mouse", StringComparison.Ordinal) && key[5] >= '1' && key[5] <= '5' && (excludedMouse & 1 << (key[5] - '1')) != 0) mouseKeys.RemoveAt(i);
            }
            // The delayed keyboard press must leave an independent live
            // mouse source for the same action available to vanilla.
            if (consumeInventoryOnNextMapping && GetInventoryBindings() is List<string> bindings)
                foreach (string token in mouseKeys) inventoryHasUnownedMouseMapping |= bindings.Contains(token);
            if (reliable) { mouseTail &= down; readOnlyTail &= down; inventoryMouseTail &= down; previousMouse = down; }
        }
        internal void ConsumeOwnedMouseEdges()
        { for (int i = 0; i < 5; i++) if ((OwnedMouseMask & 1 << i) != 0) Hotkeys.ConsumePress(256 + i); }
        private bool hotkeyTailSample;
        internal bool HotkeyPointerOwned { get { return HotkeyCapture || Hotkeys.HasSuppressedKeys || hotkeyTailSample; } }
        internal bool IsFocused { get; private set; }
        internal bool SampleFocused { get { return mapped && IsFocused && nativePermission; } }
        internal bool CanUseInput { get { return SampleFocused && finalized && !quarantine && !ExternalModalBusy; } }
        internal bool CanStartActions { get { return CanUseInput && !nativeUiOwnsFrame; } }
        // UpdateViewZoomKeys runs after mapping but before keyboard finalization.
        // Its only source is the already-filtered native triggers; this grants
        // no permission to later action consumers or to reread physical keys.
        internal bool CanUseMappedViewInput { get { return SampleFocused && !quarantine && !rearming && !ExternalModalBusy && !HotkeyPointerOwned && !HotkeyCapture; } }
        // Automation observes game state, not an OS gesture. A background
        // simulation may continue, but it never proves a physical release or
        // rearms hotkeys. An empty outer Update cannot start work: the native
        // world stage must arrive in this epoch even without physical input.
        // Input runs before native pause/step checks, so sampling alone is not
        // proof. This gates new consumers, never receipts or retained ownership.
        // Equality also handles the native uint counter wrapping naturally.
        internal bool CanRunAutomaticActions { get { return !ExternalModalBusy && !nativeUiOwnsFrame && CanRunOwnedContinuation; } }
        // Only an existing exact owner may use this outlet for finite cleanup.
        // It preserves the original gameplay-step gate while a native modal
        // blocks all NEW targets. It is not a general automation permission.
        internal bool CanRunOwnedContinuation { get { return mapped && finalized && Main.CanUpdateGameplay && !Main.gamePaused && Main.GameUpdateCount != worldAtBegin && (ExternalModalBusy || !IsFocused || CanStartActions); } }
        // Native single-player auto-pause still runs its inventory UI tail.
        // This epoch proof belongs exclusively to an already-held inventory
        // endpoint path; it proves neither a world step nor physical release.
        internal bool CanContinueModalInventory { get { return ExternalModalBusy && mapped && finalized && Main.CanUpdateGameplay; } }
        internal bool CanRetainAutomaticIntent { get { return IsFocused ? CanRetainIntent : Main.CanUpdateGameplay && !Main.gamePaused; } }
        // Main.Update can finish without DoUpdate sampling input (frame skip
        // off). That revokes execution for this epoch, not an existing intent.
        // Ordinary focus loss/reactivation still revokes both permissions. An
        // explicit external modal instead retains the accepted owner while its
        // separate new-action barrier and trusted-neutral tail remain closed.
        internal bool CanRetainIntent { get { return IsFocused && (ExternalModalBusy || !rearming && !quarantine); } }
        internal long Frame { get; private set; }
        internal long Mapping { get; private set; }
#if JMR_AIM_LIGHT || JMR_INPUT_DIAGNOSTIC
        // The existing input diagnostic's cached boundary bits. Consumers may
        // read them without refreshing focus, devices or mapped ownership.
        internal int DiagnosticBoundaryState {get{return (IsFocused?1:0)|(nativePermission?2:0)|(mapped?4:0)|(finalized?8:0)|(rearming?16:0)|(quarantine?32:0)|
            (HotkeyCapture?64:0)|(Hotkeys.HasSuppressedKeys?128:0)|(hotkeyTailSample?256:0)|(Main.blockInput?512:0)|
            (PlayerInput.WritingText?1024:0)|(CanUseInput?2048:0)|(Main.gameMenu?4096:0)|(Main.CurrentInputTextTakerOverride!=null?8192:0)|(MapPointerOwned?16384:0);}}
#endif
#if JMR_BLUEPRINT_DIAGNOSTIC
        // Only the already-sampled scalar epoch; no focus refresh or permission
        // evaluation is allowed through this diagnostic outlet.
        internal long BlueprintDiagnosticState { get { return (IsFocused?1:0)|(observed?2:0)|(mapped?4:0)|(finalized?8:0)|(nativePermission?16:0)|
            (quarantine?32:0)|(rearming?64:0)|(RawMouseLeft?128:0)|(RawMouseRight?256:0)|(HotkeyCapture?512:0)|(OwnedMouseMask!=0?1024:0); } }
        internal uint BlueprintDiagnosticWorldAtBegin { get { return worldAtBegin; } }
#endif
        internal bool CanPrepareText { get { return IsFocused && !rearming && !quarantine && !ExternalModalBusy; } }
        internal HostInputState() : this(() => Main.instance == null ? IntPtr.Zero : Main.instance.Window.Handle, GetForegroundWindow)
        { ReadOnlyKeyboardSource = Keyboard.GetState; }
        internal HostInputState(Func<IntPtr> gameWindow, Func<IntPtr> foregroundWindow)
        {
            this.gameWindow = gameWindow; this.foregroundWindow = foregroundWindow;
            // Controlled component fixtures supply their keyboard through this
            // seam; they never call the machine's OS input provider.
            ReadOnlyKeyboardSource = () => Main.keyState;
        }

        internal void BeginUpdate()
        {
            Frame++;
            worldAtBegin = Main.GameUpdateCount;
            UseGesture.BeginUpdate();
            mapped = finalized = nativePermission = false;
            nativeUiOwnsFrame = false;
            NativeUiTransitionRequest = false;
            inventoryHasUnownedMouseMapping = false;
            RefreshFocus();
            quarantine = !IsFocused || rearming || ExternalModalBusy;
            // .8's F7-F11/Alt+Enter precede its current keyboard refresh. A stale
            // cached sample must not operate the game on loss or reactivation.
            if (quarantine || HotkeyCapture || Hotkeys.HasSuppressedKeys)
            {
                Main.keyState = default(KeyboardState);
                ClearTextActions();
            }
#if JMR_INPUT_DIAGNOSTIC
            ObserveDiagnostics(0);
#endif
        }
        private void RefreshFocus()
        {
            bool focused;
            try { IntPtr window = gameWindow(); focused = window != IntPtr.Zero && foregroundWindow() == window; }
            catch { focused = false; }
            if (observed && focused != IsFocused) FocusGeneration++;
            if (!focused || observed && !IsFocused && focused) rearming = true;
            IsFocused = focused; observed = true;
        }
        internal static HotkeyModifiers Modifiers(KeyboardState sample)
        {
            HotkeyModifiers value = HotkeyModifiers.None;
            for (int i = 0; i < 6; i++) if (sample.IsKeyDown((Keys)HotkeyChord.ModifierCode(i))) value |= (HotkeyModifiers)(1 << i);
            return value;
        }
        internal bool RestrictNativePermission(bool permitted)
        { return permitted && IsFocused; }

        internal bool ConsumeInventoryKeyPress(bool f5Only, InventoryMouseClick mouseClick)
        {
            if (!CanStartActions || HotkeyPointerOwned || !string.IsNullOrEmpty(PlayerInput.ListeningTrigger)) return false;
            var bindings = GetInventoryBindings();
            if (bindings == null) return false;
            bool keyboardPress = false;
            foreach (string token in bindings)
            {
                if (!keyboardCodes.TryGetValue(token, out Keys key) || key == Keys.None || (int)key >= 256 ||
                    (f5Only && key != Keys.F5) || !Hotkeys.IsNew((int)key) || token == Main.blockKey) continue;
                // Terraria reserves Alt+Tab and Steam's Shift+Tab before binding lookup.
                if (key == Keys.Tab && (KeyboardSample.IsKeyDown(Keys.LeftAlt) || KeyboardSample.IsKeyDown(Keys.RightAlt) ||
                    Terraria.Social.SocialAPI.Mode == Terraria.Social.SocialMode.Steam && KeyboardSample.PressingShift())) continue;
                Hotkeys.ConsumePress((int)key);
                keyboardPress = true;
            }
            if (keyboardPress) consumeInventoryOnNextMapping = true;
            if (f5Only || !MappedKeyboardInventory) return keyboardPress;

            int mousePresses = 0;
            bool mouseClosesMenu = false;
            for (int button = mouseClick == InventoryMouseClick.Native ? 2 : 0; button < 5; button++)
            {
                int key = 256 + button;
                int bit = 1 << button;
                if (((OwnedMouseMask | ReadOnlyMouseMask | inventoryMouseTail) & bit) != 0 ||
                    !Hotkeys.IsNew(key) || !bindings.Contains(HotkeyChord.KeyName(key))) continue;
                if (button >= 2 || mouseClick == InventoryMouseClick.CloseMenu)
                {
                    // The same physical button can also be bound to use. Preserve
                    // independent use bindings through the existing gesture owner.
                    if (HotkeyChord.TryCreate(key, HotkeyModifiers.None, out var chord, out _)) ClaimUseGesture(chord);
                    Hotkeys.ConsumePress(key);
                    mouseClosesMenu = true;
                }
                mousePresses |= bit;
            }
            if (mousePresses == 0) return keyboardPress;

            // Mouse tokens have already mapped in this frame. Own their held
            // buttons at the existing pre-mapping filter from the next sample.
            inventoryMouseTail |= mousePresses;
            ConsumeMappedInventory();
            return keyboardPress || mouseClosesMenu;
        }

        private static bool MappedKeyboardInventory => PlayerInput.Triggers.Current.Inventory &&
            PlayerInput.Triggers.Current.LatestInputMode["Inventory"] == InputMode.Keyboard;

        private static List<string> GetInventoryBindings()
        {
            var profile = PlayerInput.CurrentProfile;
            return profile != null && profile.InputModes.TryGetValue(InputMode.Keyboard, out var keyboard) &&
                keyboard.KeyStatus.TryGetValue("Inventory", out var bindings) ? bindings : null;
        }

        private static void ConsumeMappedInventory()
        { PlayerInput.Triggers.Current.Inventory = PlayerInput.Triggers.JustPressed.Inventory = false; }

        internal void AfterMapping()
        {
            Mapping++;
            // This seam precedes UpdateViewZoomKeys and UILinkPointNavigator.
            // Keep MouseInfo and the absolute wheel owned by Terraria; they are
            // observations, not values to restore after consuming an action.
            RefreshFocus(); mapped = true;
            UseGesture.AfterMapping();
            MapMouseX = PlayerInput.MouseX; MapMouseY = PlayerInput.MouseY;
            MapWheel = PlayerInput.ScrollWheelDeltaForUI;
            if (MapPointerOwned) PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 0;
            // Do not depend on interception of the tiny native getter: a caller
            // may have inlined it before patching. Mapping is independently
            // guarded below; this native field only distinguishes its synthetic
            // mouse sample from a focused physical sample for release proof.
            nativePermission = FocusHelper.IsSelectedApplication;
            if (consumeInventoryOnNextMapping)
            {
                consumeInventoryOnNextMapping = false;
                // Terraria maps the previous keyboard sample, then copies the
                // mapped value for held keys. Clear its first Inventory mapping
                // before navigation and CopyInto, leaving other actions intact.
                if (MappedKeyboardInventory && !inventoryHasUnownedMouseMapping) ConsumeMappedInventory();
            }
            // Blueprint consumes semantic world requests only. Freeze them
            // before general UI/focus suppression; retain Mouse1/2 and all
            // native triggers so remapped Jump and inventory clicks still work.
            NativeUseItemRequest = PlayerInput.Triggers.Current.MouseLeft;
            NativeUseTileRequest = PlayerInput.Triggers.Current.MouseRight;
            // Map/camera intent must cancel stale UI controls even when the
            // same owned pointer or hotkey tail consumes its native trigger.
            // This is one mapping's observation, never permission to restore
            // input or open the map; BeginUpdate retires it before any sample.
            NativeUiTransitionRequest = PlayerInput.Triggers.Current.MapFull || PlayerInput.Triggers.Current.ToggleCameraMode;
            BlueprintMapping?.Invoke();
            quarantine |= !IsFocused || rearming || ExternalModalBusy;
            if (quarantine) { ConsumeMappedInput(); ClearTextActions(); }
            else if (HotkeyCapture || Hotkeys.HasSuppressedKeys || ClaimsHotkeyPointer != null && ClaimsHotkeyPointer()) ConsumeHotkeyActions();
#if JMR_INPUT_DIAGNOSTIC
            ObserveDiagnostics(1);
#endif
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
            if (UseGesture.HasTail) UseGesture.AfterSample(physical, SampleFocused,
                CanUseInput && !HotkeyPointerOwned && (ClaimsHotkeyPointer == null || !ClaimsHotkeyPointer()));
            if (HotkeyCapture || Hotkeys.HasSuppressedKeys) ConsumeHotkeyActions();
            if (!quarantine)
            {
#if JMR_INPUT_DIAGNOSTIC
                ObserveDiagnostics(2);
#endif
                return;
            }
            KeyboardState sample = KeyboardSample;
            Main.keyState = default(KeyboardState);
            // A native synthesized release is never neutral proof. The complete
            // activating chord must genuinely end; this is not a timed cooldown.
            MouseState mouse = PlayerInput.MouseInfo;
            if (SampleFocused && mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released &&
                mouse.MiddleButton == ButtonState.Released && mouse.XButton1 == ButtonState.Released && mouse.XButton2 == ButtonState.Released &&
                sample.GetPressedKeys().Length == 0 && ExternalModalState != ExternalModalPhase.ModalOwned)
            {
                rearming = false;
                if (ExternalModalState == ExternalModalPhase.AwaitFocusedNeutral)
                { ExternalModalState = ExternalModalPhase.None; externalModalRequest = Guid.Empty; }
            }
            // The neutral/activation sample itself remains consumed. Next fresh
            // input can act, without replaying a key, wheel delta or old target.
#if JMR_INPUT_DIAGNOSTIC
            ObserveDiagnostics(2);
#endif
        }
#if JMR_INPUT_DIAGNOSTIC
        internal bool DiagnosticGameplayActive {get;set;}
        internal void ObserveDiagnostics(int point)
        {
            try
            {
                if(!InputDiagnosticTrace.ShouldObserve(point,DiagnosticGameplayActive,IsFocused))return;
                int state=DiagnosticBoundaryState;
                int held=-1,mouse=-1;
                // Only Final/Consumers contain a current, trusted sample.
                // Background/native-synthetic zeros are unknown, never release.
                if(point>=2 && SampleFocused && FocusHelper.IsSelectedApplication)
                {held=0;mouse=0;for(int k=0;k<256;k++)if(physical[k]){held=1;break;}for(int k=0;k<5;k++)if(physical[256+k])mouse|=1<<k;}
                int actions=point<2?-1:(Main.mouseLeft?1:0)|(Main.mouseRight?2:0)|(Main.mouseLeftRelease?4:0)|(Main.mouseRightRelease?8:0);
                InputDiagnosticTrace.Observe(point,Frame,state,held,mouse,actions);
            }
            catch { InputDiagnosticTrace.Fail(); }
        }
#endif
        private void ConsumeMappedInput()
        {
            ConsumeHotkeyActions();
            PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 0;
        }
        // The owned native creator is dispatched after this epoch's final
        // sample. Its text/IME Draw still needs the trusted keyboard, but no
        // trigger from that dispatch may reach world CopyInto or another action.
        // This also owns a callback's closing frame after its UI state is gone.
        internal void ConsumeNativeUiActions()
        {
            nativeUiOwnsFrame = true;
            var pack = PlayerInput.Triggers;
            var currentKeys = pack.Current.KeyStatus;
            if (!ReferenceEquals(mappedKeys, currentKeys) || keys.Length != currentKeys.Count)
            { mappedKeys = currentKeys; keys = new string[currentKeys.Count]; currentKeys.Keys.CopyTo(keys, 0); }
            Clear(pack.Current); Clear(pack.Old); Clear(pack.JustPressed); Clear(pack.JustReleased);
            Main.mouseLeft = Main.mouseRight = false;
            PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 0;
            for (int key = 1; key < HotkeyChord.KeyCount; key++) Hotkeys.ConsumePress(key);
        }
        internal void RetainNativeUiTail()
        {
            // Only the normal game-thread modal retirement uses this outlet.
            // Held keyboard/mouse keys stay suppressed through physical release;
            // CLR shutdown must never inspect native UI or physical input.
            Hotkeys.SuppressHeld();
            ConsumeNativeUiActions();
        }
        internal void ConsumeCancellation(int key)
        {
            // An accepted child cancellation owns this frame across every
            // dispatch admission, including target/journey/blueprint shortcuts.
            // Retain only its actual key tail: consuming frozen edges must not
            // suppress held movement until those unrelated keys are released.
            Hotkeys.SuppressKey(key);
            for (int i = 1; i < HotkeyChord.KeyCount; i++) Hotkeys.ConsumePress(i);
            ConsumeHotkeyActions();
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
