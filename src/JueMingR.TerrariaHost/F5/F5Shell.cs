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
using JueMingR.TerrariaHost.EntityLabels;

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
        internal IBrowserPage Browser { get; private set; }
        internal Func<bool> TargetGestureBusy { get; set; }
        private readonly HostItems hostItems;
        private readonly Input.HostInputState inputState;
        private readonly HostHotkeys hotkeys;
        internal QuickItems.HostQuickItems QuickItems { get; set; }
        private CoinDeposit.HostCoinDeposit coinDeposit;
        private Recovery.HostRecovery recovery;
        internal Recovery.RecoveryPresentation RecoveryUi {get;private set;}
        internal void AttachRecovery(Recovery.HostRecovery owner)
        {
            recovery=owner;RecoveryUi=new Recovery.RecoveryPresentation(owner,State){HotkeyClicked=OpenHotkey};renderer.RecoveryUi=RecoveryUi;
            RecoveryUi.PotionPopup.Opened=()=>{HotkeyPopup?.Close();StylePopup?.Close();DeathPopup?.Close();FootprintPopup?.Close();};
        }
        internal Onboarding.HostOnboarding Onboarding { get; private set; }
        internal void AttachOnboarding(Onboarding.HostOnboarding owner)
        {
            Onboarding = owner;
            owner.CanPresent = () => CanTargetInput && !notes.OwnsPointer && !Main.LocalPlayer.mouseInterface;
            owner.Overlaps = onboardingOverlap;
        }
        private readonly Func<F5Rect, bool> onboardingOverlap;
        private bool OverlapsOnboarding(F5Rect rect)
        {
            var hud = information?.Hud;
            var b = hud == null ? default(F5Rect) : hud.Bounds;
            return hud != null && hud.Visible && b.X < rect.Right && b.Right > rect.X && b.Y < rect.Bottom && b.Bottom > rect.Y || notes.Overlaps(rect);
        }
        internal void AttachQuickItems(QuickItems.HostQuickItems owner) { QuickItems = owner; items?.AttachQuick(owner); }
        internal void AttachCoinDeposit(CoinDeposit.HostCoinDeposit owner) { coinDeposit = owner; items?.AttachCoins(owner); }
        internal readonly HotkeyPopup HotkeyPopup;
        internal readonly StylePopup StylePopup;
        internal readonly DeathHistoryPopup DeathPopup;
        private readonly IDeathControls deaths;
        private readonly IMapControls maps;
        internal readonly MapManagementPopup MapPopup;
        internal readonly FootprintPopup FootprintPopup;
        private readonly IFootprintControls footprints;
        private readonly HostEntityLabels labels;
        private readonly WorldTargets.HostWorldTargets worldTargets;
        private readonly WorldObjectText.HostWorldObjectText worldObjects;
        private readonly Information.HostInformation information;
        private readonly Guidance.HostGuidance guidance;
        private long labelSession = -1;
        private string reportedStyleFailure;
        private readonly System.Diagnostics.Stopwatch clickClock = System.Diagnostics.Stopwatch.StartNew();
        private readonly Action<F5Rect> drawKeyboard;
        private static readonly Action<string> displayPreferenceFeedback = message => Main.NewText(message, 255, 180, 90);
        private Player leasedPlayer;
        private bool priorMouseInterface, hoverLease, priorMouseText;
        private bool failed, failureNotified, positionRestored;
        private bool adjustmentPending;
        private long adjustmentRequest;
        private Matrix matrix;
        internal bool LayersReady { get; set; }
        internal bool Failed { get { return failed; } }

        internal F5Shell(Phase0TBiomeRuntime biome, HostPreferences preferences, HostNotes hostNotes)
            : this(biome, preferences, hostNotes, null) { }
        internal F5Shell(Phase0TBiomeRuntime biome, HostPreferences preferences, HostNotes hostNotes, HostItems hostItems, Input.HostInputState inputState = null, HostHotkeys hotkeys = null, HostEntityLabels labels = null, WorldTargets.HostWorldTargets worldTargets = null, WorldObjectText.HostWorldObjectText worldObjects = null, Information.HostInformation information = null, Guidance.HostGuidance guidance = null, IDeathControls deaths = null, IMapControls maps = null, IFootprintControls footprints = null, IAnnouncementControls announcements = null)
        { this.biome = biome; this.preferences = preferences; this.hostItems = hostItems; notes = new NotesPresentation(hostNotes.Workspace); notes.Attach(State);
            this.inputState = inputState ?? new Input.HostInputState();
            onboardingOverlap = OverlapsOnboarding;
            State.Layout.About.Attach(new NotesClipboard(() => Main.instance.Window.Handle).TryCopy);
            this.hotkeys = hotkeys;
            this.labels = labels;
            this.worldTargets = worldTargets;
            this.worldObjects = worldObjects;
            this.information = information;
            this.guidance = guidance;
            this.deaths = deaths; this.maps = maps;
            this.footprints = footprints;
            if (footprints != null) { renderer.FootprintControls = new FootprintControls(footprints); FootprintPopup = new FootprintPopup(footprints, this.inputState); }
            if (maps != null) { renderer.MapControls = new MapControls(maps); MapPopup = new MapManagementPopup(maps, this.inputState); }
            if (deaths != null) { renderer.DeathControls = new DeathControls(deaths); DeathPopup = new DeathHistoryPopup(deaths, this.inputState); }
            if (guidance != null) renderer.GuidanceControls = new GuidanceControls(guidance);
            if (labels != null || worldTargets != null || worldObjects != null || information != null || guidance != null) StylePopup = new StylePopup(labels, this.inputState, worldTargets: worldTargets, worldObjects: worldObjects, information: information, guidance: guidance);
            if (information != null) renderer.InformationControls = new Information.InformationControls(information);
            if (labels != null) renderer.EntityControls = new EntityLabelControls(labels);
            if (worldTargets != null) renderer.WorldControls = new WorldTargetControls(worldTargets);
            if (worldObjects != null) renderer.ObjectControls = new WorldObjectControls(worldObjects);
            if (hotkeys != null)
            {
                HotkeyPopup = new HotkeyPopup(hotkeys.Bindings, hotkeys.Registry, this.inputState);
            }
            if (hotkeys != null || labels != null || worldTargets != null || worldObjects != null || information != null || deaths != null || maps != null || footprints != null) this.inputState.ClaimsHotkeyPointer = ClaimsPopupPointer;
            drawKeyboard = rect => renderer.Keyboard(Main.spriteBatch, rect);
            if (hostItems != null) { items = new ItemsPresentation(hostItems, State); items.HotkeyClicked = OpenHotkey; }
            if (announcements != null) renderer.AnnouncementControls = new AnnouncementControls(announcements, hotkeys?.Bindings);
            Func<int, bool> prior = State.BeforeLeave;
            State.BeforeLeave = page => { if (MapPopup != null && !MapPopup.TryLeave(() => { if (page < 0) State.Close(); else State.Navigate(page); })) return false; if (prior != null && !prior(page)) return false; if (Browser != null && !Browser.RequestFinish()) return false; Browser?.Suspend(); HotkeyPopup?.Close(); StylePopup?.Close(); DeathPopup?.Close(); FootprintPopup?.Close(); State.Layout.About.Leave(); return true; };
        }
        internal void AttachBrowser(IBrowserPage page) { if (Browser != null) throw new InvalidOperationException("Browser already attached."); Browser = page; }
        internal bool CanTargetInput { get { return CanTargetActions(true); } }
        // Background buffs share every UI safety condition. Focus is the only
        // presentation exception; no keyboard or pointer permission is granted.
        internal bool CanBackgroundBuff { get { return !inputState.IsFocused && CanTargetActions(false); } }
        private bool CanTargetActions(bool requireFocus)
        { return !failed && LayersReady && biome.SharedRuntime.IsSessionActive && CanPresent(false,requireFocus) && !State.Visible && !Main.blockInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest && Main.CurrentInputTextTakerOverride == null && !PlayerInput.WritingText && !inputState.HotkeyCapture && Main.LocalPlayer != null && Main.LocalPlayer.talkNPC < 0 && Main.LocalPlayer.sign < 0 && Main.npcShop == 0 && string.IsNullOrEmpty(Main.npcChatText) && !Main.clothesWindow && !Main.hairWindow && !(information != null && information.Adjustment.Active) && !adjustmentPending; }
        internal bool BlocksMapInput { get { return failed || State.Visible || notes.OwnsPointer || HotkeyPopup != null && HotkeyPopup.Visible || StylePopup != null && StylePopup.Visible || MapPopup != null && MapPopup.Visible; } }
        internal void CloseForMapLocate() { MapPopup?.Suspend(); State.Close(); notes.Suspend(); items?.Suspend(); RecoveryUi?.Suspend(); Browser?.Suspend(); }
        internal void OpenHotkey(string id, F5Rect rect)
        { HotkeyPopup?.Click(id, rect, State.Layout.Generation, State.Page, clickClock.ElapsedMilliseconds); if (HotkeyPopup != null && HotkeyPopup.Visible) { RecoveryUi?.PotionPopup.Close(); StylePopup?.Close(); DeathPopup?.Close(); FootprintPopup?.Close(); } }
        private bool ClaimsPopupPointer()
        {
            // MouseInfo is already this tick's native sample at AfterMapping.
            // Claim visible popup buttons/body before native zoom/navigation;
            // do not sample or advance physical edges here. The later popup
            // handles the same geometry and retains the complete gesture tail.
            if (failed || !State.Visible || !CanPresentNow) return false;
            MouseState mouse = PlayerInput.MouseInfo;
            Vector2 raw = new Vector2(mouse.X * PlayerInput.RawMouseScale.X, mouse.Y * PlayerInput.RawMouseScale.Y);
            Vector2 point = Vector2.Transform(raw, Matrix.Invert(Main.UIScaleMatrix));
            return RecoveryUi != null && (RecoveryUi.PotionPopup.Captured || RecoveryUi.PotionPopup.Contains(point.X,point.Y)) || StylePopup != null && (StylePopup.HasCapture || StylePopup.ContainsPointer(point.X, point.Y)) || HotkeyPopup != null && HotkeyPopup.ContainsPointer(point.X, point.Y) || DeathPopup != null && (DeathPopup.Pressed >= 0 || DeathPopup.ContainsPointer(point.X, point.Y)) || MapPopup != null && (MapPopup.Pressed >= 0 || MapPopup.ContainsPointer(point.X, point.Y)) || FootprintPopup != null && (FootprintPopup.Pressed >= 0 || FootprintPopup.ContainsPointer(point.X, point.Y));
        }
        internal bool OwnsPointer { get { return !failed && CanPresentNow && (information != null && information.Adjustment.Dragging || inputState.HotkeyPointerOwned || State.OwnsPointer || Browser != null && Browser.OwnsPointer || notes.OwnsPointer || RecoveryUi != null && RecoveryUi.OwnsPointer || items != null && items.OwnsPointer || HotkeyPopup != null && HotkeyPopup.OwnsPointer || StylePopup != null && StylePopup.OwnsPointer || DeathPopup != null && DeathPopup.OwnsPointer || MapPopup != null && MapPopup.OwnsPointer || FootprintPopup != null && FootprintPopup.OwnsPointer); } }

        internal bool CanAdjustInformation { get { return information != null && information.PositionReady && biome.SharedRuntime.IsSessionActive && !failed && CanPresentNow && inputState.CanUseInput; } }
        internal void RequestInformationAdjustment()
        {
            if (!CanAdjustInformation || information.Adjustment.Active || adjustmentPending ||
                HotkeyPopup != null && HotkeyPopup.Capturing || StylePopup != null && StylePopup.HasCapture || DeathPopup != null && DeathPopup.Visible || MapPopup != null && MapPopup.Visible || FootprintPopup != null && FootprintPopup.Visible || items != null && items.Selecting) return;
            long request = ++adjustmentRequest, session = information.Session;
            bool returnToF5 = State.Visible;
            Action<bool> completed = success =>
            {
                if (request != adjustmentRequest) return;
                adjustmentPending = false;
                if (!success || information.Session != session || !information.PositionReady || !CanPresentNow || failed) return;
                HotkeyPopup?.Close(); StylePopup?.Close(); State.Close();
                information.Adjustment.Begin(information.Position.Value, returnToF5, session, information.NativeEpoch, inputState.SampleFocused && NeutralAdjustmentInput());
                information.Pointer.Invalidate(); information.PrepareHud();
                if (!information.Hud.Visible) { information.Adjustment.Cancel(); if (returnToF5) State.RestoreVisible(); return; }
                // Entry is one action. Its whole physical chord must end before
                // the HUD can acquire a new press; no binding is replayed.
                inputState.Hotkeys.SuppressHeld(); inputState.ConsumeHotkeyActions();
            };
            if (State.Visible && State.Page == 4)
            {
                adjustmentPending = true;
                if (!notes.RequestSafeLeave(completed)) adjustmentPending = false;
            }
            else completed(State.BeforeLeave == null || !State.Visible || State.BeforeLeave(-1));
        }
        private void CancelInformationAdjustment(bool restore)
        {
            adjustmentRequest++; adjustmentPending = false;
            if (information == null) return;
            bool returnToF5 = information.Adjustment.Active && information.Adjustment.ReturnToF5;
            information.Adjustment.Cancel(); information.Pointer.Invalidate();
            information.Hud.Project(information.Position.Value);
            if (restore && returnToF5 && CanPresentNow && !failed) State.RestoreVisible();
        }
        private bool NeutralAdjustmentInput()
        {
            for (int key = 0; key < HotkeyChord.KeyCount; key++) if (inputState.Hotkeys.IsDown(key)) return false;
            return true;
        }

        private bool CanPresentNow
        {
            get { return CanPresent(false); }
        }
        private bool CanPresent(bool allowMap)
        {return CanPresent(allowMap,true);}
        private bool CanPresent(bool allowMap,bool requireFocus)
            {
                Player player = Main.LocalPlayer;
                var capture = Terraria.Graphics.Capture.CaptureManager.Instance;
                return !Main.gameMenu && !Main.dedServ && (Main.netMode == 0 || Main.netMode == 1) && player != null && player.active &&
                    (!requireFocus || inputState.IsFocused) && !PlayerInput.UsingGamepad && !PlayerInput.ShouldFastUseItem &&
                    (!Main.mapFullscreen || allowMap) && !Main.hideUI && !Main.onlyDrawFancyUI && !Main.ingameOptionsWindow &&
                    !Main.inFancyUI && capture != null && !capture.Active &&
                    (!notes.OtherTextOwner || (RecoveryUi != null && RecoveryUi.OwnsTextToken || items != null && items.OwnsTextToken || Browser != null && Browser.OwnsTextToken || StylePopup != null && StylePopup.TextInput.OwnsTextToken || MapPopup != null && MapPopup.TextInput.OwnsTextToken) && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest);
            }

        // Input may be taken over by vanilla after this frame's button release.
        // Recheck at the operation outlet, while retaining our own mouse lease.
        internal bool CanExecuteMerchantInput
        {
            get { return !failed && State.Ready && State.Visible && CanPresentNow && inputState.CanStartActions &&
                !PlayerInput.Triggers.Current.MapFull && !PlayerInput.Triggers.Current.ToggleCameraMode &&
                !Main.blockInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest &&
                Main.CurrentInputTextTakerOverride == null && !PlayerInput.WritingText &&
                !(HotkeyPopup != null && HotkeyPopup.Visible) && !(StylePopup != null && StylePopup.Visible) && !(DeathPopup != null && DeathPopup.Visible) && !(MapPopup != null && MapPopup.Visible) && !(FootprintPopup != null && FootprintPopup.Visible) &&
                !(RecoveryUi != null && RecoveryUi.Selecting) && !(items != null && items.Selecting) && !(information != null && information.Adjustment.Active) && !adjustmentPending; }
        }

        internal void BeforeInput()
        { try { CheckLabelSession(); MapPopup?.BeforeInput(!failed && CanPresentNow && inputState.CanPrepareText); StylePopup?.BeforeInput(!failed && CanPresentNow && inputState.CanPrepareText); notes.BeforeInput(!failed && CanPresentNow && inputState.CanPrepareText); RecoveryUi?.BeforeInput(!failed && CanPresentNow && inputState.CanPrepareText); items?.BeforeInput(!failed && CanPresentNow && inputState.CanPrepareText); Browser?.BeforeInput(!failed && CanPresentNow && inputState.CanPrepareText); } catch { FailClosed(); } }

        internal void ProcessInput()
        {
            try
            {
                RestoreLeases();
                RestorePositionWhenLoaded();
                if (worldObjects != null) State.Layout.SetWorldObjectSettings(worldObjects.Preferences.Value);
                if (deaths != null) State.Layout.SetDeathInformation(deaths.CountText, deaths.DaysText);
                matrix = Main.UIScaleMatrix;
                Vector2 screen = PlayerInput.OriginalScreenSize;
                Vector2 raw = new Vector2(PlayerInput.MouseInfo.X * PlayerInput.RawMouseScale.X,
                    PlayerInput.MouseInfo.Y * PlayerInput.RawMouseScale.Y);
                KeyboardState keySample = inputState.KeyboardSample;
                bool f5 = !(TargetGestureBusy?.Invoke() ?? false) && keySample.IsKeyDown(Keys.F5) && !inputState.Hotkeys.IsSuppressed((int)Keys.F5) && !(HotkeyPopup != null && HotkeyPopup.Capturing);
                // Map/camera requests precede their modal flags and draw layers.
                // The shell and Notes must yield the same newly sampled input.
                bool inputActive = !failed && CanPresentNow && inputState.CanUseInput && !PlayerInput.Triggers.Current.MapFull && !PlayerInput.Triggers.Current.ToggleCameraMode;
                Vector2 pointer = Vector2.Transform(raw, Matrix.Invert(matrix));
                bool adjusting = information != null && information.Adjustment.Active;
                // Native menu entry is the ordinary world-exit edge. Preserve
                // the last valid cached foreground sample before UI teardown;
                // focus loss has already cancelled it in UpdatePrefix.
                if (adjusting && Main.gameMenu && inputState.SampleFocused)
                { information.FinishNormalAdjustment(); adjusting = false; }
                if (adjusting && (!inputActive || !information.InputGeometryCurrent || inputState.Hotkeys.IsNew((int)Keys.Escape) || inputState.Hotkeys.IsNew(257) || f5))
                {
                    CancelInformationAdjustment(inputActive);
                    inputState.Hotkeys.SuppressHeld(); inputState.ConsumeHotkeyActions(); f5 = false;
                }
                if (!inputActive && adjustmentPending) CancelInformationAdjustment(false);
                if (StylePopup != null && StylePopup.Visible || DeathPopup != null && DeathPopup.Visible || MapPopup != null && MapPopup.Visible || FootprintPopup != null && FootprintPopup.Visible) renderer.RefreshResources();
                if (StylePopup != null && StylePopup.Visible)
                {
                    // Resource/viewport changes invalidate an in-flight gesture
                    // before its release can submit against stale geometry.
                    if (inputState.Hotkeys.IsNew(256) && !StylePopup.ContainsPointer(pointer.X, pointer.Y)) StylePopup.YieldTextToEntry();
                    StylePopup.Process(inputActive && State.Visible, State.Page, pointer.X, pointer.Y,
                        StylePopup.Layout.Matches(screen.X / matrix.M11, screen.Y / matrix.M11, matrix.M11, renderer.FontIdentity, renderer.SkinGeneration, StyleAnchor()), PlayerInput.ScrollWheelDeltaForUI);
                }
                HotkeyPopup?.Process(inputActive && State.Visible, State.Page, pointer.X, pointer.Y,
                    HotkeyPopup.Layout.Matches(screen.X / matrix.M11, screen.Y / matrix.M11, renderer.FontIdentity, renderer.SkinGeneration), PlayerInput.ScrollWheelDeltaForUI);
                DeathPopup?.Process(inputActive && State.Visible, State.Page, pointer.X, pointer.Y,
                    DeathPopup.Matches(screen.X / matrix.M11, screen.Y / matrix.M11, renderer.FontIdentity, renderer.SkinGeneration), PlayerInput.ScrollWheelDeltaForUI);
                MapPopup?.Process(inputActive && State.Visible, pointer.X, pointer.Y, MapPopup.Matches(screen.X / matrix.M11, screen.Y / matrix.M11, renderer.FontIdentity, renderer.SkinGeneration), PlayerInput.ScrollWheelDeltaForUI);
                FootprintPopup?.Process(inputActive && State.Visible, State.Page, pointer.X, pointer.Y, FootprintPopup.Matches(screen.X / matrix.M11, screen.Y / matrix.M11, renderer.FontIdentity, renderer.SkinGeneration));
                RecoveryUi?.PotionPopup.Process(inputActive && State.Visible && State.Page==10,keySample,pointer,matrix,screen,inputState.SampleFocused,PlayerInput.ScrollWheelDeltaForUI);
                bool popupPointer = RecoveryUi != null && RecoveryUi.PotionPopup.OwnsPointer || HotkeyPopup != null && HotkeyPopup.BlockPointer || StylePopup != null && StylePopup.BlockPointer || DeathPopup != null && DeathPopup.BlockPointer || MapPopup != null && MapPopup.BlockPointer || FootprintPopup != null && FootprintPopup.BlockPointer || inputState.HotkeyPointerOwned;
                // About has long dynamically measured content. Refresh before
                // release dispatch so a replaced font cannot fire stale geometry.
                if (State.Visible && State.Page == 5 && renderer.RefreshResources()) renderer.Prepare(State, screen.X, screen.Y, matrix.M11);
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
                    PageWheelHandled = RecoveryUi != null && RecoveryUi.ConsumeWheel || HotkeyPopup != null && HotkeyPopup.ConsumeWheel || StylePopup != null && StylePopup.ConsumeWheel || DeathPopup != null && DeathPopup.Visible || MapPopup != null && MapPopup.Visible || FootprintPopup != null && FootprintPopup.Visible || inputActive && (notes.Wheel(pointer.X, pointer.Y, PlayerInput.ScrollWheelDeltaForUI) || Browser != null && Browser.Wheel(pointer.X, pointer.Y, PlayerInput.ScrollWheelDeltaForUI))
                });
                if (!State.Visible) { HotkeyPopup?.Close(); StylePopup?.Close(); DeathPopup?.Close(); MapPopup?.Suspend(); FootprintPopup?.Close(); }
                notes.ProcessInput(inputActive, matrix, screen, raw, inputState.SampleFocused, popupPointer, keySample);
                items?.ProcessInput(inputActive, keySample, pointer, State.Layout.Matches(screen.X, screen.Y, matrix.M11, State.Page), inputState.SampleFocused, popupPointer);
                RecoveryUi?.ProcessInput(inputActive,keySample,pointer,State.Layout.Matches(screen.X,screen.Y,matrix.M11,State.Page),inputState.SampleFocused,popupPointer);
                Browser?.Process(inputActive, pointer, popupPointer, State.Layout.Matches(screen.X, screen.Y, matrix.M11, State.Page));
                if (information != null)
                {
                    var adjustment = information.Adjustment; bool wasActive = adjustment.Active;
                    var mouse = PlayerInput.MouseInfo;
                    WindowPosition submitted = adjustment.Step(new Information.InformationPointerSample
                    {
                        Active = inputActive, Focused = inputState.SampleFocused,
                        Neutral = wasActive && !adjustment.Dragging && NeutralAdjustmentInput(),
                        NewLeft = inputState.Hotkeys.IsNew(256), Left = mouse.LeftButton == ButtonState.Pressed,
                        X = pointer.X, Y = pointer.Y, Geometry = information.Hud.GeometryVersion, Session = information.Session,
                        NativeEpoch = wasActive ? information.NativeEpoch : 0,
                        CanGrab = wasActive && information.Pointer.CanGrab(information.Tick, information.Session),
                        HigherOwner = State.Visible || notes.OwnsPointer || RecoveryUi != null && RecoveryUi.OwnsPointer || items != null && items.OwnsPointer ||
                            HotkeyPopup != null && HotkeyPopup.BlockPointer || StylePopup != null && StylePopup.BlockPointer ||
                            wasActive && information.Pointer.NativeOwnsPointer(information.Tick, information.Session)
                    }, information.Hud.Bounds);
                    if (submitted != null) information.SetPosition(submitted);
                    if (wasActive)
                    {
                        information.Hud.Project(adjustment.Draft ?? information.Position.Value);
                        if (!adjustment.Active && inputActive && adjustment.ReturnToF5) State.RestoreVisible();
                    }
                }
                if (State.ClickedHotkey != null)
                    OpenHotkey(State.ClickedHotkey.HotkeyTarget, State.ClickedHotkey.Rect.Offset(State.X + State.Layout.Viewport.X, State.Y + State.Layout.Viewport.Y - State.Scroll));
                if (State.ClickedControl != null && (State.Command == F5Command.MarkerManage || State.Command == F5Command.ExplorationDetails) && renderer.MapControls != null && renderer.MapControls.Available(State.Command))
                { HotkeyPopup?.Close(); StylePopup?.Close(); DeathPopup?.Close(); FootprintPopup?.Close(); MapPopup.Open(State.Command == F5Command.ExplorationDetails); }
                if (State.ClickedControl != null && (State.Command == F5Command.DeathDetails || State.Command == F5Command.DeathConfigure) && renderer.DeathControls != null && renderer.DeathControls.Available(State.Command))
                { HotkeyPopup?.Close(); StylePopup?.Close(); FootprintPopup?.Close(); DeathPopup.Open(State.Command == F5Command.DeathConfigure, State.Page); }
                if (State.ClickedControl != null && State.Command == F5Command.FootprintConfigure && renderer.FootprintControls != null && renderer.FootprintControls.Available(State.Command))
                { HotkeyPopup?.Close(); StylePopup?.Close(); DeathPopup?.Close(); MapPopup?.Suspend(); FootprintPopup.Open(State.Page); }
                if (State.ClickedControl != null && EntityLabelControls.IsStyle(State.Command) && renderer.EntityControls != null && renderer.EntityControls.Available(State.Command))
                {
                    HotkeyPopup?.Close();
                    StylePopup.Click(EntityLabelControls.Target(State.Command).Value, ControlRect(State.ClickedControl), State.Page);
                }
                if (OwnsPointer) LeaseMouseInterface();
                ConsumeSample();
                SubmitPosition();
                // Settings owns intent; the Feature still owns actual state and
                // its failure latch. Loading or a failure cannot become a click.
                if ((State.Command == F5Command.EnableBiome || State.Command == F5Command.DisableBiome) && biome.CanObserveLocalPlayer && preferences.BiomeLoaded && !biome.FeatureFailed)
                    preferences.SetBiomeEnabled(State.Command == F5Command.EnableBiome);
                else if (State.ClickedControl != null && WorldTargetControls.IsStyle(State.Command) && renderer.WorldControls != null && renderer.WorldControls.Available(State.Command))
                {
                    HotkeyPopup?.Close();
                    StylePopup.Click(WorldTargetControls.Target(State.Command).Value, ControlRect(State.ClickedControl), State.Page);
                }
                else if (State.ClickedControl != null && WorldObjectControls.IsStyle(State.Command) && renderer.ObjectControls != null && renderer.ObjectControls.Available(State.Command))
                {
                    HotkeyPopup?.Close();
                    StylePopup.Click(WorldObjectControls.Target(State.Command).Value, ControlRect(State.ClickedControl), State.Page);
                }
                else if (State.ClickedControl != null && Information.InformationControls.IsStyle(State.Command) && renderer.InformationControls != null && renderer.InformationControls.Available(State.Command))
                {
                    HotkeyPopup?.Close();
                    StylePopup.Click(Information.InformationControls.Target(State.Command).Value, ControlRect(State.ClickedControl), State.Page);
                }
                else if (State.Command == F5Command.AdjustInformation) RequestInformationAdjustment();
                else if (State.ClickedControl != null && GuidanceControls.IsStyle(State.Command) && renderer.GuidanceControls != null && renderer.GuidanceControls.Available(State.Command))
                {
                    HotkeyPopup?.Close();
                    StylePopup.Click(GuidanceControls.Target(State.Command).Value, ControlRect(State.ClickedControl), State.Page);
                }
                else { renderer.EntityControls?.Execute(State.Command); renderer.WorldControls?.Execute(State.Command); renderer.ObjectControls?.Execute(State.Command);
                    renderer.GuidanceControls?.Execute(State.Command);
                    State.Layout.About.Execute(State.Command);
                    renderer.DeathControls?.Execute(State.Command); renderer.MapControls?.Execute(State.Command); renderer.FootprintControls?.Execute(State.Command); renderer.AnnouncementControls?.Execute(State.Command);
                    if (State.Command != F5Command.EnableBiome && State.Command != F5Command.DisableBiome) renderer.InformationControls?.Execute(State.Command); }
                bool gameplay = !(TargetGestureBusy?.Invoke() ?? false) && inputActive && !(information != null && information.Adjustment.Active) && !adjustmentPending && !Main.blockInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest &&
                    Main.CurrentInputTextTakerOverride == null && !PlayerInput.WritingText && !OwnsPointer &&
                    !(HotkeyPopup != null && HotkeyPopup.Visible) && !(StylePopup != null && StylePopup.Visible) && !(DeathPopup != null && DeathPopup.Visible) && !(MapPopup != null && MapPopup.Visible) && !(FootprintPopup != null && FootprintPopup.Visible) && !(RecoveryUi != null && RecoveryUi.Selecting) && !(items != null && items.Selecting) &&
                    !(Main.LocalPlayer != null && Main.LocalPlayer.mouseInterface);
                hotkeys?.Bindings.Dispatch(inputState.Hotkeys, Main.netMode == 0 ? HotkeyContext.SinglePlayer : HotkeyContext.Multiplayer, gameplay);
                QuickItems?.AfterDispatch(gameplay);
                // Older development profiles have no target owner or actions;
                // do not evaluate the new native observation gate for them.
                if (Browser != null)
                {
                    hotkeys?.Bindings.Dispatch(inputState.Hotkeys, Main.netMode == 0 ? HotkeyContext.SinglePlayer : HotkeyContext.Multiplayer, CanTargetInput, AnnouncementControls.SendActionId);
                    hotkeys?.Bindings.Dispatch(inputState.Hotkeys, Main.netMode == 0 ? HotkeyContext.SinglePlayer : HotkeyContext.Multiplayer, CanTargetInput, "item-browser.query");
                }
                // Fullscreen map owns native mouseInterface. Admit only its
                // registered master switch, through the same binding engine.
                bool mapHotkey = !failed && maps != null && Main.mapFullscreen && CanPresent(true) && inputState.CanUseInput &&
                    !PlayerInput.Triggers.Current.MapFull && !PlayerInput.Triggers.Current.ToggleCameraMode && !Main.blockInput &&
                    Main.CurrentInputTextTakerOverride == null && !PlayerInput.WritingText && !inputState.HotkeyCapture;
                hotkeys?.Bindings.Dispatch(inputState.Hotkeys, Main.netMode == 0 ? HotkeyContext.SinglePlayer : HotkeyContext.Multiplayer, mapHotkey, MapControls.ActionId);
                hotkeys?.Bindings.Dispatch(inputState.Hotkeys, Main.netMode == 0 ? HotkeyContext.SinglePlayer : HotkeyContext.Multiplayer, mapHotkey && footprints != null, FootprintControls.ActionId);
            }
            catch { FailClosed(); ConsumeSample(); }
        }

        private void ConsumeSample()
        {
            if (State.ConsumeLeft || Browser != null && Browser.ConsumeLeft || notes.ConsumeLeft || RecoveryUi != null && RecoveryUi.ConsumeLeft || items != null && items.ConsumeLeft || information != null && information.Adjustment.ConsumeLeft)
            {
                PlayerInput.Triggers.Current.MouseLeft = false;
                PlayerInput.Triggers.JustPressed.MouseLeft = false;
                PlayerInput.Triggers.JustReleased.MouseLeft = false;
                Main.mouseLeft = false;
            }
            if (State.ConsumeRight || Browser != null && Browser.ConsumeRight || notes.ConsumeRight || RecoveryUi != null && RecoveryUi.ConsumeRight || items != null && items.ConsumeRight)
            {
                PlayerInput.Triggers.Current.MouseRight = false;
                PlayerInput.Triggers.JustPressed.MouseRight = false;
                PlayerInput.Triggers.JustReleased.MouseRight = false;
                Main.mouseRight = false;
            }
            if (State.ConsumeWheel || Browser != null && Browser.ConsumeWheel || notes.ConsumeWheel || RecoveryUi != null && RecoveryUi.ConsumeWheel || items != null && items.ConsumeWheel || HotkeyPopup != null && HotkeyPopup.ConsumeWheel || StylePopup != null && StylePopup.ConsumeWheel || DeathPopup != null && DeathPopup.ConsumeWheel || MapPopup != null && MapPopup.ConsumeWheel || FootprintPopup != null && FootprintPopup.OwnsPointer)
            { PlayerInput.ScrollWheelDelta = 0; PlayerInput.ScrollWheelDeltaForUI = 0; }
            // Absolute wheel and physical MouseInfo are never changed. Consumed
            // button transitions/deltas are never restored or replayed later.
        }

        internal void AfterUpdate()
        {
            try
            {
                RestoreLeases();
                if (!State.Visible || State.Page != 5) State.Layout.About.Leave();
                RestorePositionWhenLoaded();
                CheckLabelSession();
                DeathPopup?.CheckSession(); MapPopup?.CheckSession(); FootprintPopup?.CheckSession();
                if (deaths != null) State.Layout.SetDeathInformation(deaths.CountText, deaths.DaysText);
                if (worldObjects != null) State.Layout.SetWorldObjectSettings(worldObjects.Preferences.Value);
                hotkeys?.Bindings.Poll();
                SubmitPosition();
                // Do not consume a required alert while normal game text is hidden.
                if (!Main.gameMenu && !Main.hideUI)
                {
                    preferences.TakeFeedback(displayPreferenceFeedback); hostItems?.TakeFeedback(displayPreferenceFeedback); labels?.TakeFeedback(displayPreferenceFeedback); worldTargets?.TakeFeedback(displayPreferenceFeedback); worldObjects?.TakeFeedback(displayPreferenceFeedback); information?.TakeFeedback(displayPreferenceFeedback);
                    guidance?.TakeFeedback(displayPreferenceFeedback);
                    deaths?.TakeFeedback(displayPreferenceFeedback); maps?.TakeFeedback(displayPreferenceFeedback); footprints?.TakeFeedback(displayPreferenceFeedback);
                    QuickItems?.TakeFeedback(displayPreferenceFeedback); coinDeposit?.TakeFeedback(displayPreferenceFeedback); recovery?.TakeFeedback(displayPreferenceFeedback);
                    Onboarding?.State.TakeFeedback(displayPreferenceFeedback);
                    if (StylePopup?.Failure != null && StylePopup.FailureKey != reportedStyleFailure)
                    { reportedStyleFailure = StylePopup.FailureKey; displayPreferenceFeedback(StylePopup.Failure); }
                }
                if (failed)
                {
                    if (!failureNotified && !Main.gameMenu)
                    {
                        failureNotified = true;
                        Main.NewText("F5 界面出现问题，已关闭；设置已保留。", 255, 180, 90);
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
                    if (State.Page == 2) renderer.PrepareMapValue();
                    HotkeyPopup?.Prepare(screen.X / matrix.M11, screen.Y / matrix.M11, renderer.FontIdentity, renderer.PopupMeasure, renderer.SkinGeneration);
                    StylePopup?.Prepare(screen.X / matrix.M11, screen.Y / matrix.M11, matrix.M11, renderer.FontIdentity, renderer.PopupMeasure, renderer.SkinGeneration, StyleAnchor());
                    DeathPopup?.Prepare(screen.X / matrix.M11, screen.Y / matrix.M11, renderer.FontIdentity, renderer.PopupMeasure, renderer.SkinGeneration);
                    MapPopup?.Prepare(screen.X / matrix.M11, screen.Y / matrix.M11, renderer.FontIdentity, renderer.PopupMeasure, renderer.SkinGeneration);
                    FootprintPopup?.Prepare(screen.X / matrix.M11, screen.Y / matrix.M11, renderer.FontIdentity, renderer.PopupMeasure, renderer.SkinGeneration);
                    // Emote Bubbles runs before the modal early-return layers.
                    // Clear only the old pointer-triggered NPC bubble at update end.
                    if (OwnsPointer) Main.instance.currentNPCShowingChatBubble = -1;
                }
                notes.Prepare(CanPresentNow && LayersReady, matrix, PlayerInput.OriginalScreenSize);
                items?.Prepare(CanPresentNow && LayersReady, matrix, PlayerInput.OriginalScreenSize);
                RecoveryUi?.Prepare(CanPresentNow && LayersReady,matrix,PlayerInput.OriginalScreenSize);
                Browser?.Prepare(CanPresentNow && LayersReady, matrix);
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
            Onboarding?.ConfirmNativeDraw();
            try
            {
                if (!CanPresentNow) { CloseAndSubmitPosition(); RestoreLeases(); }
                else if (!failed)
                {
                    notes.DrawPins();
                    if (State.Visible && State.Ready)
                    {
                        renderer.Draw(State, matrix, biome.FeatureEnabled, !biome.CanObserveLocalPlayer || biome.FeatureFailed || !preferences.BiomeLoaded);
                        notes.DrawCards();
                        // FrameSkip.Off can draw after an outer Update with no
                        // HandleInput call. That revokes actions, not a focused
                        // hover's read-only text; focus quarantine still applies.
                        bool hintsBlocked = !inputState.CanPrepareText || HotkeyPopup != null && HotkeyPopup.Visible || StylePopup != null && StylePopup.Visible || DeathPopup != null && DeathPopup.Visible || MapPopup != null && MapPopup.Visible || FootprintPopup != null && FootprintPopup.Visible;
                        items?.Draw(drawKeyboard, !hintsBlocked && State.CanShowHint);
                        RecoveryUi?.Draw(drawKeyboard,!hintsBlocked && State.CanShowHint);
                        Browser?.Draw();
                        RecoveryUi?.PotionPopup.Draw();
                        renderer.DrawHints(State, matrix, items, hintsBlocked,
                            !biome.CanObserveLocalPlayer || biome.FeatureFailed || !preferences.BiomeLoaded);
                        if (HotkeyPopup != null) renderer.DrawPopup(HotkeyPopup);
                        if (StylePopup != null) renderer.DrawStylePopup(StylePopup);
                        if (DeathPopup != null) renderer.DrawDeathPopup(DeathPopup);
                        if (MapPopup != null) renderer.DrawMapPopup(MapPopup);
                        if (FootprintPopup != null) renderer.DrawFootprintPopup(FootprintPopup);
                    }
                }
            }
            catch { FailClosed(); }
            return true;
        }

        internal bool EndPointerLayer() { RestoreLeases(); if (information != null && information.Adjustment.Active) information.Pointer.End(); return true; }

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

        private F5Rect ControlRect(F5Element element)
        { return element.Rect.Offset(State.X + State.Layout.Viewport.X, State.Y + State.Layout.Viewport.Y - State.Scroll); }
        private F5Rect StyleAnchor()
        {
            if (StylePopup != null && StylePopup.Visible)
                foreach (var element in State.Layout.Elements)
                    if (EntityLabelControls.IsStyle(element.Command) && EntityLabelControls.Target(element.Command) == StylePopup.Target ||
                        WorldTargetControls.IsStyle(element.Command) && WorldTargetControls.Target(element.Command) == StylePopup.WorldTarget ||
                        WorldObjectControls.IsStyle(element.Command) && WorldObjectControls.Target(element.Command) == StylePopup.WorldObject ||
                        Information.InformationControls.IsStyle(element.Command) && Information.InformationControls.Target(element.Command) == StylePopup.InformationTarget ||
                        GuidanceControls.IsStyle(element.Command) && GuidanceControls.Target(element.Command) == StylePopup.GuidanceTarget) return ControlRect(element);
            return new F5Rect(State.X, State.Y, 0, 0);
        }
        private void CheckLabelSession()
        {
            long generation = labels?.SessionGeneration ?? worldTargets?.SessionGeneration ?? worldObjects?.SessionGeneration ?? -1;
            if (labelSession == generation) return;
            labelSession = generation; StylePopup?.Close();
        }

        internal void CloseAndSubmitPosition() { CancelInformationAdjustment(false); HotkeyPopup?.Close(); StylePopup?.Close(); DeathPopup?.Close(); MapPopup?.Suspend(); FootprintPopup?.Close(); State.Close(); notes.Suspend(); items?.Suspend(); RecoveryUi?.Suspend(); Browser?.Suspend(); SubmitPosition(); }

        internal void CancelForFocusLoss()
        { CancelInformationAdjustment(false); HotkeyPopup?.Close(); StylePopup?.Close(); DeathPopup?.Close(); MapPopup?.Suspend(); FootprintPopup?.Close(); State.CancelForFocusLoss(); notes.Suspend(true); items?.Suspend(); RecoveryUi?.Suspend(); Browser?.Suspend(); RestoreLeases(); }

        internal void FailClosed()
        { failed = true; State.Ready = false; CloseAndSubmitPosition(); RestoreLeases(); notes.FailClosed(); renderer.Dispose(); }
    }
}
