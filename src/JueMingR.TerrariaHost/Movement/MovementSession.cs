using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using JueMingR.Features.Movement;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.Graphics.Capture;

namespace JueMingR.TerrariaHost.Movement
{
    // One preference owner and one finite real-player input scope. F5 opening
    // is not a movement barrier; actual text/modal/capture owners are.
    internal sealed class MovementSession : IRuntimeFeature, IMovementControls
    {
        private readonly SingleFeatureRuntime runtime;
        private readonly HostInputState input;
        private readonly PreferenceDocument<MovementOptions> preferences;
        private readonly Stopwatch startup = Stopwatch.StartNew();
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private Player player, intentPlayer;
        private MovementInputFrame frame;
        private long tick, intentVersion, intentGeneration, observedMapping = -1;
        private bool failed, stopping;
        private string failure;
        private PreferenceStatus? reported;
        internal Exception SetupError { get; private set; }
        internal Func<bool> CanMovementInput, CanTeleportPointer;
        internal readonly MovementJumpHost JumpHost = new MovementJumpHost();
        internal readonly ContinuousDashHost DashHost;
        internal readonly SmartFallHost FallHost;
        public event Action DashModeChanged;
        internal MovementOptions Value { get { return preferences.Snapshot.Value; } }
        internal MovementSession(string directory, SingleFeatureRuntime runtime, HostInputState input, Items.HostItems items = null, Fishing.FishingEquipment fishing = null)
        {
            this.runtime = runtime; this.input = input;
            preferences = new PreferenceDocument<MovementOptions>(new AtomicFileDocument(Path.Combine(directory, "JueMingRData", "config", "features", "movement.json"), 65536, true), new MovementPreferenceCodec(), new MovementOptions());
            DashHost = new ContinuousDashHost(this);
            FallHost = new SmartFallHost(this, items, () => runtime.Generation, fishing);
            try { MovementHooks.Install(this); }
            catch (Exception error) { SetupError = error; failed = true; failure = "移动辅助暂不可用，原版操作不受影响。"; }
            if (!failed)
            {
                try { DashHost.Install(); } catch (Exception error) { SetupError = error; failure = "连续冲刺暂不可用，原版操作不受影响。"; }
                TeleportHooks.TryInstall(CanCorrectTeleport, reason => failure = "传送修正暂不可用，原版操作不受影响。");
                TeleportHooks.RescueTarget = (p, item, point) => FallHost.TryTeleportTarget(p, item, ref point) ? (Microsoft.Xna.Framework.Vector2?)point : null;
            }
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        public bool Enabled { get { return true; } }
        public bool ControlsEnabled { get { return !failed && !stopping && runtime.IsSessionActive && preferences.Snapshot.IsLoaded; } }
        public bool IsEnabled(int feature) { return Value.Enabled(feature); }
        public bool Available(int feature) { return ControlsEnabled && (feature != 1 || DashHost.Available) && (feature != 2 || TeleportHooks.Available); }
        public ContinuousDashMode DashMode { get { return Value.DashMode; } }
        public SmartFallProtectionOptions SmartFallProtection { get { return Value.SmartFallProtection; } }
        public bool SetSmartFallPermission(int capability, bool allowed)
        { return ControlsEnabled && preferences.Set(Value.WithSmartFallProtection(Value.SmartFallProtection.WithPermission(capability, allowed))); }
        public bool SetEnabled(int feature, bool value)
        {
            if (!Available(feature) || !preferences.Set(Value.WithEnabled(feature, value))) return false;
            if (feature == 0) CancelJumpIntent();
            else if (feature == 1) DashHost.Reset();
            return true;
        }
        public bool SetDashMode(ContinuousDashMode value)
        {
            var before = Value.DashMode;
            if (!ControlsEnabled || !preferences.Set(Value.WithMode(value))) return false;
            DashHost.Reset();
            // OFF is still a separately selected mode. Retire an earlier
            // hotkey result without changing its binary enabled semantics.
            if (before != value) DashModeChanged?.Invoke();
            return true;
        }
        private void CancelJumpIntent() { if (JumpHost.Cancel()) intentVersion++; }
        private void ResetIntent() { DashHost.Reset(); CancelJumpIntent(); }
        internal bool BeginUpdate(Player value)
        {
            // An unowned call is not invalidation of the local context. Reject
            // remote players/clones before touching retained intent or scope.
            int local = Main.myPlayer;
            if (value == null || local < 0 || local >= Main.maxPlayers || Main.player == null ||
                local >= Main.player.Length || !ReferenceEquals(value, Main.player[local])) return false;
            var options = Value;
            if (!options.SimulatedJumpEnabled && !options.ContinuousDashEnabled && !options.TeleportCorrectionEnabled && !options.SmartFallProtection.Enabled && !FallHost.HasPending) return false;
            if (player != null) return false;
            if (!RetainContext(value))
            {
                ResetIntent();
                // Losing input permission forbids new rescue, not returning a
                // loan to this exact still-live local object. Remote calls were
                // rejected above and cannot advance or cancel that cleanup.
                if (!FallHost.HasPending || !CanCleanUp(value)) return false;
            }
            player = value; frame = default(MovementInputFrame); tick++;
            if (options.SimulatedJumpEnabled || options.SmartFallProtection.Enabled || FallHost.HasPending) JumpHost.BeginUpdate(value, runtime.Generation, tick, input.Mapping);
            FallHost.BeginUpdate(value, runtime.Generation, tick);
            return true;
        }
        internal void CaptureInput(Player value)
        {
            if (!ReferenceEquals(player, value)) return;
            if (!RetainContext(value)) { ResetIntent(); FallHost.Capture(value, false); return; }
            // A missing mapped/finalized sample is not a release and grants no
            // native action. Only an actual Capture can lend this epoch's edge.
            if (!input.CanStartActions) { FallHost.Capture(value, false); return; }
            frame = new MovementInputFrame { Captured = true, Frame = input.Frame, Mapping = input.Mapping, Generation = runtime.Generation, Tick = tick,
                JumpHeld = value.controlJump, DownHeld = value.controlDown, LeftHeld = value.controlLeft, RightHeld = value.controlRight };
            bool allowed = CanAct(value);
            if (!allowed) ResetIntent();
            else DashHost.Sample(value, frame);
            FallHost.Capture(value, allowed && Value.SmartFallProtection.Enabled);
        }
        private bool HasWorld(Player value)
        {
            return !failed && !stopping && runtime.IsSessionActive && Thread.CurrentThread.ManagedThreadId == thread && !Main.dedServ && Main.netMode != 2 && !Main.gameMenu &&
                Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers && ReferenceEquals(value, Main.player[Main.myPlayer]) && value != null && value.active && !value.dead && !value.ghost;
        }
        private bool CanCleanUp(Player value)
        {
            // Death withdraws action permission but does not turn the same
            // real player's native containers into a successor's inventory.
            return !stopping && runtime.IsSessionActive && Thread.CurrentThread.ManagedThreadId == thread && !Main.dedServ && Main.netMode != 2 && !Main.gameMenu &&
                value != null && value.active && Main.player != null && Main.myPlayer >= 0 && Main.myPlayer < Main.player.Length && ReferenceEquals(value, Main.player[Main.myPlayer]);
        }
        private bool CanRetainMovementContext(Player value)
        {
            var capture = CaptureManager.Instance;
            return HasWorld(value) && input.CanRetainIntent && !input.HotkeyPointerOwned && !input.HotkeyCapture && !Main.blockInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest &&
                !PlayerInput.WritingText && Main.CurrentInputTextTakerOverride == null && !Main.mapFullscreen && !Main.inFancyUI && !Main.onlyDrawFancyUI && !Main.ingameOptionsWindow &&
                (capture == null || !capture.Active) && !value.CCed && !value.isControlledByFilm && value.spectating < 0 && !value.isOperatingAnotherEntity && (CanMovementInput == null || CanMovementInput());
        }
        private bool RetainContext(Player value)
        {
            if (!CanRetainMovementContext(value)) return false;
            // Bind retained gestures to the existing Runtime identity; this is
            // no new session detector. A replacement player cannot inherit them
            // before the outer Runtime callback has observed its new identity.
            if (!ReferenceEquals(intentPlayer, value) || intentGeneration != runtime.Generation)
            { ResetIntent(); intentPlayer = value; intentGeneration = runtime.Generation; }
            return true;
        }
        internal bool CanAct(Player value)
        {
            return ReferenceEquals(player, value) && CanRetainMovementContext(value) && frame.Captured && frame.Frame == input.Frame &&
                frame.Mapping == input.Mapping && frame.Generation == runtime.Generation && input.CanStartActions;
        }
        internal bool CanCorrectTeleport(Player value)
        { return Value.TeleportCorrectionEnabled && CanAct(value) && !input.MapPointerOwned && (CanTeleportPointer == null || CanTeleportPointer()); }
        internal void ObserveInput()
        {
            // Context revocation is valid even on an empty outer Update. Input
            // edges are meaningful only once per fresh trusted Mapping; absence
            // of mapping neither releases a key nor advances a gesture clock.
            Player local = Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers ? Main.player[Main.myPlayer] : null;
            if (!RetainContext(local)) { ResetIntent(); return; }
            if (!Value.SimulatedJumpEnabled) CancelJumpIntent();
            if (!input.CanStartActions || observedMapping == input.Mapping) return;
            observedMapping = input.Mapping;
            if (!PlayerInput.Triggers.Current.Jump || PlayerInput.Triggers.Current.Down) CancelJumpIntent();
        }
        internal void BeforeJump(Player value)
        {
            if (!ReferenceEquals(player, value)) return;
            bool allowed = CanAct(value);
            if (!allowed)
            {
                if (!CanRetainMovementContext(value)) ResetIntent();
                return;
            }
            if (!Value.SimulatedJumpEnabled) CancelJumpIntent();
            FallHost.BeforeJump(value);
            JumpHost.BeforeJump(value, Value.SimulatedJumpEnabled, allowed, frame.JumpHeld, frame.DownHeld, intentVersion);
        }
        internal void AfterJump(Player value) { if (ReferenceEquals(player, value)) { JumpHost.AfterJump(value, CanAct(value), frame.JumpHeld, frame.DownHeld); FallHost.AfterJump(value); } }
        internal void AfterFlight(Player value)
        {
            // Native flight already ran. Retiring our span is observation, not
            // permission to start an action; even an unsampled epoch must not
            // let a later coast mask take over that existing native flight.
            if (ReferenceEquals(player, value) && Thread.CurrentThread.ManagedThreadId == thread) { JumpHost.AfterFlight(value); FallHost.AfterFlight(value); }
        }
        internal bool EffectiveJump(Player value, bool nativeJump)
        {
            // Native release and a disabled feature need no movement gate at
            // the eleven fixed reads; neither can request an assisted mask.
            if (!nativeJump || !Value.SimulatedJumpEnabled) return nativeJump;
            return CanAct(value) ? JumpHost.EffectiveJump(value, true) : nativeJump;
        }
        internal void EndUpdate(Player value)
        {
            if (!ReferenceEquals(player, value)) return;
            try
            {
                try { FallHost.EndUpdate(value); }
                finally { DashHost.EndUpdate(value); JumpHost.EndUpdate(value); }
                // Returning borrowed inputs is unconditional. Retaining the
                // historical gesture depends on real context, not on whether
                // this scope happened to reach the mapped Capture boundary.
                if (!RetainContext(value)) ResetIntent();
            }
            finally { player = null; frame = default(MovementInputFrame); }
        }
        public void OnSessionStarted() { ResetIntent(); intentPlayer = null; observedMapping = -1; }
        public void OnSessionEnded() { if (player != null) EndUpdate(player); FallHost.ClearSession(); ResetIntent(); intentPlayer = null; observedMapping = -1; JumpHost.ClearSession(); }
        public void Update(ulong updateTick)
        {
            if (!preferences.Snapshot.IsLoaded && startup.ElapsedMilliseconds >= 2000) preferences.AbandonSlowLoad();
            ObserveInput();
        }
        public void FailClosed() { failed = true; OnSessionEnded(); }
        public void TakeFeedback(Action<string> display)
        {
            if (failure != null) { display(failure); failure = null; }
            var snapshot = preferences.Snapshot;
            if (!snapshot.IsLoaded || reported == snapshot.Status || snapshot.Status == PreferenceStatus.Missing || snapshot.Status == PreferenceStatus.Pending || snapshot.Status == PreferenceStatus.Saved) return;
            display(snapshot.CommitUnconfirmed ? "移动设置是否保存成功无法确认；当前修改仅本次有效，原文件已保留。" : "移动设置无法读取或保存；当前修改仅本次有效，原文件已保留。"); reported = snapshot.Status;
        }
        private void OnExit(object sender, EventArgs args)
        {
            if (stopping) return; stopping = true; AppDomain.CurrentDomain.ProcessExit -= OnExit;
            OnSessionEnded(); FallHost.Uninstall(); TeleportHooks.Uninstall(); DashHost.Uninstall(); MovementHooks.Uninstall(this); preferences.Stop(750);
        }
    }
}
