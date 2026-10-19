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
        private Player player;
        private MovementInputFrame frame;
        private long tick, intentVersion;
        private bool failed, stopping, permitted;
        private string failure;
        private PreferenceStatus? reported;
        internal Exception SetupError { get; private set; }
        internal Func<bool> CanMovementInput, CanTeleportPointer;
        internal readonly MovementJumpHost JumpHost = new MovementJumpHost();
        internal readonly ContinuousDashHost DashHost;
        public event Action DashModeChanged;
        internal MovementOptions Value { get { return preferences.Snapshot.Value; } }
        internal MovementSession(string directory, SingleFeatureRuntime runtime, HostInputState input)
        {
            this.runtime = runtime; this.input = input;
            preferences = new PreferenceDocument<MovementOptions>(new AtomicFileDocument(Path.Combine(directory, "JueMingRData", "config", "features", "movement.json"), 65536, true), new MovementPreferenceCodec(), new MovementOptions());
            DashHost = new ContinuousDashHost(this);
            try { MovementHooks.Install(this); }
            catch (Exception error) { SetupError = error; failed = true; failure = "移动辅助暂不可用，原版操作不受影响。"; }
            if (!failed)
            {
                try { DashHost.Install(); } catch (Exception error) { SetupError = error; failure = "连续冲刺暂不可用，原版操作不受影响。"; }
                TeleportHooks.TryInstall(CanCorrectTeleport, reason => failure = "传送修正暂不可用，原版操作不受影响。");
            }
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        public bool Enabled { get { return true; } }
        public bool ControlsEnabled { get { return !failed && !stopping && runtime.IsSessionActive && preferences.Snapshot.IsLoaded; } }
        public bool IsEnabled(int feature) { return Value.Enabled(feature); }
        public bool Available(int feature) { return ControlsEnabled && (feature != 1 || DashHost.Available) && (feature != 2 || TeleportHooks.Available); }
        public ContinuousDashMode DashMode { get { return Value.DashMode; } }
        public bool SetEnabled(int feature, bool value)
        {
            if (!Available(feature) || !preferences.Set(Value.WithEnabled(feature, value))) return false;
            if (feature == 0) { intentVersion++; JumpHost.Cancel(); }
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
        private void ResetIntent() { intentVersion++; DashHost.Reset(); JumpHost.Cancel(); }
        internal bool BeginUpdate(Player value)
        {
            var options = Value;
            if (!options.SimulatedJumpEnabled && !options.ContinuousDashEnabled && !options.TeleportCorrectionEnabled) return false;
            if (player != null || !HasWorld(value)) return false;
            player = value; frame = default(MovementInputFrame); tick++;
            if (options.SimulatedJumpEnabled) JumpHost.BeginUpdate(value, runtime.Generation, tick, input.Mapping);
            return true;
        }
        internal void CaptureInput(Player value)
        {
            if (!ReferenceEquals(player, value)) return;
            frame = new MovementInputFrame { Captured = true, Frame = input.Frame, Mapping = input.Mapping, Generation = runtime.Generation, Tick = tick,
                JumpHeld = value.controlJump, DownHeld = value.controlDown, LeftHeld = value.controlLeft, RightHeld = value.controlRight };
            bool allowed = CanAct(value);
            if (permitted != allowed) { permitted = allowed; intentVersion++; }
            if (!allowed) { DashHost.Reset(); JumpHost.Cancel(); }
            else DashHost.Sample(value, frame);
        }
        private bool HasWorld(Player value)
        {
            return !failed && !stopping && runtime.IsSessionActive && Thread.CurrentThread.ManagedThreadId == thread && !Main.dedServ && Main.netMode != 2 && !Main.gameMenu &&
                Main.myPlayer >= 0 && Main.myPlayer < Main.maxPlayers && ReferenceEquals(value, Main.player[Main.myPlayer]) && value != null && value.active && !value.dead && !value.ghost;
        }
        internal bool CanAct(Player value)
        {
            var capture = CaptureManager.Instance;
            return ReferenceEquals(player, value) && HasWorld(value) && frame.Captured && frame.Frame == input.Frame && frame.Mapping == input.Mapping && frame.Generation == runtime.Generation &&
                input.CanStartActions && !input.HotkeyPointerOwned && !input.HotkeyCapture && !Main.blockInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest &&
                !PlayerInput.WritingText && Main.CurrentInputTextTakerOverride == null && !Main.mapFullscreen && !Main.inFancyUI && !Main.onlyDrawFancyUI && !Main.ingameOptionsWindow &&
                (capture == null || !capture.Active) && !value.CCed && !value.isControlledByFilm && value.spectating < 0 && !value.isOperatingAnotherEntity && (CanMovementInput == null || CanMovementInput());
        }
        internal bool CanCorrectTeleport(Player value)
        { return Value.TeleportCorrectionEnabled && CanAct(value) && !input.MapPointerOwned && (CanTeleportPointer == null || CanTeleportPointer()); }
        internal void ObserveInput()
        {
            // Revoke an old forecast premise before input-stage consumers can
            // reuse it. This may only cancel: true intent is published later
            // by the real player's post-takeover JumpMovement boundary.
            var capture = CaptureManager.Instance;
            bool allowed = input.CanStartActions && !input.HotkeyPointerOwned && !input.HotkeyCapture && !Main.blockInput && !Main.drawingPlayerChat && !Main.editSign && !Main.editChest &&
                !PlayerInput.WritingText && Main.CurrentInputTextTakerOverride == null && !Main.mapFullscreen && !Main.inFancyUI && !Main.onlyDrawFancyUI && !Main.ingameOptionsWindow &&
                (capture == null || !capture.Active) && (CanMovementInput == null || CanMovementInput());
            if (!allowed) { DashHost.Reset(); JumpHost.Cancel(); permitted = false; }
            else if (!Value.SimulatedJumpEnabled || !PlayerInput.Triggers.Current.Jump || PlayerInput.Triggers.Current.Down) JumpHost.Cancel();
        }
        internal void BeforeJump(Player value)
        {
            if (!ReferenceEquals(player, value)) return;
            bool allowed = CanAct(value);
            if (!allowed || !Value.SimulatedJumpEnabled) JumpHost.Cancel();
            JumpHost.BeforeJump(value, Value.SimulatedJumpEnabled, allowed, frame.JumpHeld, frame.DownHeld, intentVersion);
        }
        internal void AfterJump(Player value) { if (ReferenceEquals(player, value)) JumpHost.AfterJump(value, CanAct(value), frame.JumpHeld, frame.DownHeld); }
        internal void EndUpdate(Player value)
        {
            if (!ReferenceEquals(player, value)) return;
            try
            {
                DashHost.EndUpdate(value); JumpHost.EndUpdate(value);
                // Text and other native takeovers can skip the capture block
                // entirely. Absence of a trusted sample retires old intent;
                // it is never interpreted as a neutral release to replay later.
                if (!frame.Captured || !CanAct(value)) { DashHost.Reset(); JumpHost.Cancel(); permitted = false; }
            }
            finally { player = null; frame = default(MovementInputFrame); }
        }
        public void OnSessionStarted() { ResetIntent(); }
        public void OnSessionEnded() { if (player != null) EndUpdate(player); ResetIntent(); JumpHost.ClearSession(); }
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
            OnSessionEnded(); TeleportHooks.Uninstall(); DashHost.Uninstall(); MovementHooks.Uninstall(this); preferences.Stop(750);
        }
    }
}
