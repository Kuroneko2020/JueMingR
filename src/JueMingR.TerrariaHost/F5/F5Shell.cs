using System;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.Settings;
using JueMingR.TerrariaHost.Notes;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.Hotkeys;

namespace JueMingR.TerrariaHost.F5
{
    // The shell combines its current UI owners. Leases remember values changed
    // by that decision; they are not a separate source of pointer ownership.
    internal sealed class F5Shell
    {
        internal readonly F5Interaction State = new F5Interaction();
        private readonly F5Renderer renderer = new F5Renderer();
        private readonly Phase0TBiomeRuntime biome;
        private readonly HostPreferences preferences;
        private readonly NotesPresentation notes;
        private readonly ItemsPresentation items;
        private readonly HostItems hostItems;
        private readonly Input.HostInputState inputState;
        private readonly HostHotkeys hotkeys;
        internal readonly HotkeyPopup HotkeyPopup;
        private readonly System.Diagnostics.Stopwatch clickClock = System.Diagnostics.Stopwatch.StartNew();
        private readonly Action<F5Rect> drawKeyboard;
        private static readonly Action<string> displayPreferenceFeedback = message => Main.NewText(message, 255, 180, 90);
        private Player leasedPlayer;
        private bool priorMouseInterface, hoverLease, priorMouseText;
        private bool failed, failureNotified, positionRestored;
        private Matrix matrix;
        internal bool LayersReady { get; set; }
        internal bool Failed { get { return failed; } }

        internal F5Shell(Phase0TBiomeRuntime biome, HostPreferences preferences, HostNotes hostNotes)
            : this(biome, preferences, hostNotes, null) { }
        internal F5Shell(Phase0TBiomeRuntime biome, HostPreferences preferences, HostNotes hostNotes, HostItems hostItems, Input.HostInputState inputState = null, HostHotkeys hotkeys = null)
        { this.biome = biome; this.preferences = preferences; this.hostItems = hostItems; notes = new NotesPresentation(hostNotes.Workspace); notes.Attach(State);
            this.inputState = inputState ?? new Input.HostInputState();
            this.hotkeys = hotkeys;
            if (hotkeys != null)
            {
                HotkeyPopup = new HotkeyPopup(hotkeys.Bindings, hotkeys.Registry, this.inputState);
                this.inputState.ClaimsHotkeyPointer = ClaimsPopupPointer;
            }
            drawKeyboard = rect => renderer.Keyboard(Main.spriteBatch, rect);
            if (hostItems != null) { items = new ItemsPresentation(hostItems, State); items.HotkeyClicked = OpenHotkey; }
            Func<int, bool> prior = State.BeforeLeave;
            State.BeforeLeave = page => { if (prior != null && !prior(page)) return false; HotkeyPopup?.Close(); return true; };
        }
        private void OpenHotkey(string id, F5Rect rect) { HotkeyPopup?.Click(id, rect, State.Layout.Generation, State.Page, clickClock.ElapsedMilliseconds); }
        private bool ClaimsPopupPointer()
        {
            // MouseInfo is already this tick's native sample at AfterMapping.
            // Claim visible popup buttons/body before native zoom/navigation;
            // do not sample or advance physical edges here. The later popup
            // handles the same geometry and retains the complete gesture tail.
            if (failed || HotkeyPopup == null || !HotkeyPopup.Visible || !State.Visible || !CanPresentNow) return false;
            MouseState mouse = PlayerInput.MouseInfo;
            if (mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released &&
                mouse.MiddleButton == ButtonState.Released && mouse.XButton1 == ButtonState.Released && mouse.XButton2 == ButtonState.Released) return false;
            Vector2 raw = new Vector2(mouse.X * PlayerInput.RawMouseScale.X, mouse.Y * PlayerInput.RawMouseScale.Y);
            Vector2 point = Vector2.Transform(raw, Matrix.Invert(Main.UIScaleMatrix));
            return HotkeyPopup.Layout.Panel.Contains(point.X, point.Y);
        }
        internal bool OwnsPointer { get { return !failed && CanPresentNow && (inputState.HotkeyPointerOwned || State.OwnsPointer || notes.OwnsPointer || items != null && items.OwnsPointer || HotkeyPopup != null && HotkeyPopup.OwnsPointer); } }

        private bool CanPresentNow
        {
            get
            {
                Player player = Main.LocalPlayer;
                var capture = Terraria.Graphics.Capture.CaptureManager.Instance;
                return !Main.gameMenu && !Main.dedServ && (Main.netMode == 0 || Main.netMode == 1) && player != null && player.active &&
                    inputState.IsFocused && !PlayerInput.UsingGamepad && !PlayerInput.ShouldFastUseItem &&
                    !Main.mapFullscreen && !Main.hideUI && !Main.onlyDrawFancyUI && !Main.ingameOptionsWindow &&
                    !Main.inFancyUI && capture != null && !capture.Active &&
                    (!notes.OtherTextOwner || items != null && items.OwnsTextToken && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest);
            }
        }

        internal void BeforeInput()
        { try { notes.BeforeInput(!failed && CanPresentNow && inputState.CanPrepareText); items?.BeforeInput(!failed && CanPresentNow && inputState.CanPrepareText); } catch { FailClosed(); } }

        internal void ProcessInput()
        {
            try
            {
                RestoreLeases();
                RestorePositionWhenLoaded();
                matrix = Main.UIScaleMatrix;
                Vector2 screen = PlayerInput.OriginalScreenSize;
                Vector2 raw = new Vector2(PlayerInput.MouseInfo.X * PlayerInput.RawMouseScale.X,
                    PlayerInput.MouseInfo.Y * PlayerInput.RawMouseScale.Y);
                KeyboardState keySample = inputState.KeyboardSample;
                bool f5 = keySample.IsKeyDown(Keys.F5) && !inputState.Hotkeys.IsSuppressed((int)Keys.F5) && !(HotkeyPopup != null && HotkeyPopup.Capturing);
                // Map/camera requests precede their modal flags and draw layers.
                // The shell and Notes must yield the same newly sampled input.
                bool inputActive = !failed && CanPresentNow && inputState.CanUseInput && !PlayerInput.Triggers.Current.MapFull && !PlayerInput.Triggers.Current.ToggleCameraMode;
                Vector2 pointer = State.Visible || f5 ? Vector2.Transform(raw, Matrix.Invert(matrix)) : raw;
                HotkeyPopup?.Process(inputActive && State.Visible, State.Page, pointer.X, pointer.Y,
                    HotkeyPopup.Layout.Matches(screen.X / matrix.M11, screen.Y / matrix.M11, renderer.FontIdentity));
                bool popupPointer = HotkeyPopup != null && HotkeyPopup.BlockPointer || inputState.HotkeyPointerOwned;
                State.Update(new F5Input
                {
                    Width = screen.X, Height = screen.Y, Scale = matrix.M11, X = pointer.X, Y = pointer.Y,
                    Active = inputActive,
                    Focused = inputState.SampleFocused,
                    F5 = f5,
                    Left = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed,
                    Right = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed,
                    BlockPointer = popupPointer,
                    Wheel = PlayerInput.ScrollWheelDeltaForUI,
                    PageWheelHandled = inputActive && notes.Wheel(pointer.X, pointer.Y, PlayerInput.ScrollWheelDeltaForUI)
                });
                notes.ProcessInput(inputActive, matrix, screen, raw, inputState.SampleFocused, popupPointer, keySample);
                items?.ProcessInput(inputActive, keySample, pointer, State.Layout.Matches(screen.X, screen.Y, matrix.M11, State.Page), inputState.SampleFocused, popupPointer);
                if (State.ClickedHotkey != null)
                    OpenHotkey(State.ClickedHotkey.HotkeyTarget, State.ClickedHotkey.Rect.Offset(State.X + State.Layout.Viewport.X, State.Y + State.Layout.Viewport.Y - State.Scroll));
                if (OwnsPointer) LeaseMouseInterface();
                ConsumeSample();
                SubmitPosition();
                // Settings owns intent; the Feature still owns actual state and
                // its failure latch. Loading or a failure cannot become a click.
                if (State.Command != F5Command.None && Main.netMode == 0 && preferences.BiomeLoaded && !biome.FeatureFailed)
                    preferences.SetBiomeEnabled(State.Command == F5Command.EnableBiome);
                bool gameplay = inputActive && !Main.blockInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest &&
                    Main.CurrentInputTextTakerOverride == null && !PlayerInput.WritingText && !OwnsPointer &&
                    !(HotkeyPopup != null && HotkeyPopup.Visible) && !(items != null && items.Selecting) &&
                    !(Main.LocalPlayer != null && Main.LocalPlayer.mouseInterface);
                hotkeys?.Bindings.Dispatch(inputState.Hotkeys, Main.netMode == 0 ? HotkeyContext.SinglePlayer : HotkeyContext.Multiplayer, gameplay);
            }
            catch { FailClosed(); ConsumeSample(); }
        }

        private void ConsumeSample()
        {
            if (State.ConsumeLeft || notes.ConsumeLeft || items != null && items.ConsumeLeft)
            {
                PlayerInput.Triggers.Current.MouseLeft = false;
                PlayerInput.Triggers.JustPressed.MouseLeft = false;
                PlayerInput.Triggers.JustReleased.MouseLeft = false;
                Main.mouseLeft = false;
            }
            if (State.ConsumeRight || notes.ConsumeRight || items != null && items.ConsumeRight)
            {
                PlayerInput.Triggers.Current.MouseRight = false;
                PlayerInput.Triggers.JustPressed.MouseRight = false;
                PlayerInput.Triggers.JustReleased.MouseRight = false;
                Main.mouseRight = false;
            }
            if (State.ConsumeWheel || notes.ConsumeWheel || items != null && items.ConsumeWheel)
            { PlayerInput.ScrollWheelDelta = 0; PlayerInput.ScrollWheelDeltaForUI = 0; }
            // Absolute wheel and physical MouseInfo are never changed. Consumed
            // button transitions/deltas are never restored or replayed later.
        }

        internal void AfterUpdate()
        {
            try
            {
                RestoreLeases();
                RestorePositionWhenLoaded();
                hotkeys?.Bindings.Poll();
                SubmitPosition();
                // Do not consume a required alert while normal game text is hidden.
                if (!Main.gameMenu && !Main.hideUI) { preferences.TakeFeedback(displayPreferenceFeedback); hostItems?.TakeFeedback(displayPreferenceFeedback); }
                if (failed)
                {
                    if (!failureNotified && !Main.gameMenu)
                    {
                        failureNotified = true;
                        Main.NewText("F5 界面已安全关闭：资源、布局或绘制不可用。群系设置保留。", 255, 180, 90);
                    }
                    return;
                }
                if (!CanPresentNow)
                { CloseAndSubmitPosition(); notes.Prepare(false, matrix, PlayerInput.OriginalScreenSize); renderer.Dispose(); return; }
                // Documents finish independently; a protected/failed read still
                // finishes loading and permits in-memory use of that document.
                if (!preferences.UiLoaded) { State.Ready = false; notes.Prepare(CanPresentNow && LayersReady, matrix, PlayerInput.OriginalScreenSize); return; }
                if (State.Visible || !State.Ready) State.Ready = LayersReady && renderer.RefreshResources();
                if (!State.Ready) { CloseAndSubmitPosition(); notes.Prepare(false, matrix, PlayerInput.OriginalScreenSize); return; }
                if (State.Visible)
                {
                    Vector2 screen = PlayerInput.OriginalScreenSize;
                    renderer.Prepare(State, screen.X, screen.Y, matrix.M11);
                    HotkeyPopup?.Prepare(screen.X / matrix.M11, screen.Y / matrix.M11, renderer.FontIdentity, renderer.PopupMeasure);
                    // Emote Bubbles runs before the modal early-return layers.
                    // Clear only the old pointer-triggered NPC bubble at update end.
                    if (OwnsPointer) Main.instance.currentNPCShowingChatBubble = -1;
                }
                notes.Prepare(CanPresentNow && LayersReady, matrix, PlayerInput.OriginalScreenSize);
                items?.Prepare(CanPresentNow && LayersReady, matrix, PlayerInput.OriginalScreenSize);
            }
            catch { FailClosed(); }
        }

        internal bool BeginPointerLayer()
        {
            try
            {
                RestoreLeases();
                if (!CanPresentNow) { CloseAndSubmitPosition(); return true; }
                if (OwnsPointer)
                {
                    // AfterUpdate already cleared the old bubble before Emote.
                    // Keep this pointer's later NPC hover state clear as well.
                    Main.instance.currentNPCShowingChatBubble = -1;
                    // Claim this pointer's pending text before lower mouse UI can
                    // generate it. null is Terraria's supported no-text value;
                    // DrawPendingMouseText still draws its normal cursor.
                    Main.ClearHoverItem();
                    Main.instance.MouseTextNoOverride(null);
                }
            }
            catch { FailClosed(); }
            return true;
        }

        internal bool HoverGateLayer()
        {
            if (!OwnsPointer) return true;
            try
            {
                // Immediately after Mouse Item / NPC Head: its reset must have
                // already run. Preserve until Interact Item Icon has consumed it.
                LeaseMouseInterface();
                priorMouseText = Main.mouseText;
                hoverLease = true;
                Main.mouseText = true;
            }
            catch { FailClosed(); }
            return true;
        }

        internal bool AllowNpcHover()
        {
            if (!OwnsPointer) return true;
            // DrawMouseOver already resets HoveringOverAnNPC immediately before
            // this call. The skipped body never acquires noThrow or frees the
            // elder slime; do not undo unrelated existing noThrow/interaction.
            Main.instance.currentNPCShowingChatBubble = -1;
            return false;
        }

        internal bool DrawLayer()
        {
            try
            {
                if (!CanPresentNow) { CloseAndSubmitPosition(); RestoreLeases(); }
                else if (!failed)
                {
                    notes.DrawPins();
                    if (State.Visible && State.Ready)
                    {
                        renderer.Draw(State, matrix, biome.FeatureEnabled, Main.netMode != 0 || biome.FeatureFailed || !preferences.BiomeLoaded);
                        notes.DrawCards();
                        items?.Draw(drawKeyboard);
                        if (HotkeyPopup != null) renderer.DrawPopup(HotkeyPopup);
                    }
                }
            }
            catch { FailClosed(); }
            return true;
        }

        internal bool EndPointerLayer() { RestoreLeases(); return true; }

        private void LeaseMouseInterface()
        {
            Player player = Main.LocalPlayer;
            if (player == null) return;
            if (leasedPlayer == null) { leasedPlayer = player; priorMouseInterface = player.mouseInterface; }
            player.mouseInterface = true;
        }

        private void RestoreLeases()
        {
            // Restore the captured player, not a possibly different current LocalPlayer.
            if (leasedPlayer != null)
            {
                if (leasedPlayer.mouseInterface) leasedPlayer.mouseInterface = priorMouseInterface;
                leasedPlayer = null;
            }
            if (hoverLease)
            {
                if (Main.mouseText) Main.mouseText = priorMouseText;
                hoverLease = false;
            }
        }

        private void RestorePositionWhenLoaded()
        {
            if (positionRestored || !preferences.UiLoaded) return;
            State.RestorePosition(preferences.Position);
            positionRestored = true;
        }

        private void SubmitPosition()
        {
            WindowPosition position = State.TakePositionToSave();
            if (position != null) preferences.SetPosition(position);
        }

        internal void CloseAndSubmitPosition() { HotkeyPopup?.Close(); State.Close(); notes.Suspend(); items?.Suspend(); SubmitPosition(); }

        internal void CancelForFocusLoss()
        { HotkeyPopup?.Close(); State.CancelForFocusLoss(); notes.Suspend(true); items?.Suspend(); RestoreLeases(); }

        internal void FailClosed()
        { failed = true; State.Ready = false; CloseAndSubmitPosition(); RestoreLeases(); notes.FailClosed(); renderer.Dispose(); }
    }
}
