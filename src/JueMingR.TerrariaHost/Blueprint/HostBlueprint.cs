using System;
using System.Diagnostics;
using System.IO;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed class HostBlueprint : IRuntimeFeature
    {
        internal readonly BlueprintCaptureState State = new BlueprintCaptureState();
        internal readonly HostInputState Input;
        internal readonly BlueprintPointerProof Pointer;
        internal readonly BlueprintWorldLayer World;
        internal readonly BlueprintToolbar Toolbar;
        private readonly SingleFeatureRuntime runtime;
        private readonly PreferenceDocument<bool> preference;
        private readonly Stopwatch loading = Stopwatch.StartNew();
        private Player player;
        private object world, connection;
        private long generation;
        private bool ready, failed;
        private PreferenceStatus? reported;
        internal F5Shell Shell;
        internal Exception SetupError { get; private set; }
        internal HostBlueprint(string directory, SingleFeatureRuntime runtime, HostInputState input, World.WorldTileObservation geometry)
        {
            this.runtime = runtime; Input = input;
            preference = new PreferenceDocument<bool>(new AtomicFileDocument(Path.Combine(directory, "JueMingRData", "config", "features", "blueprint.json"), 65536, true), new BlueprintPreferenceCodec(), false);
            World = new BlueprintWorldLayer(this, geometry); Toolbar = new BlueprintToolbar(this);
            Pointer = new BlueprintPointerProof(this);
            try { BlueprintHooks.Install(this); Pointer.Install(); ready = true; }
            catch (Exception error) { SetupError = error; BlueprintHooks.Uninstall(); Pointer.Dispose(); }
            Input.BlueprintMapping = ObserveMapping;
            AppDomain.CurrentDomain.ProcessExit += ExitProcess;
        }
        public bool Enabled { get { return true; } }
        internal bool HasSession { get { return ready && !failed && runtime.IsSessionActive && generation == runtime.Generation &&
            !Main.gameMenu && ReferenceEquals(player, Main.LocalPlayer) && ReferenceEquals(world, Main.ActiveWorldFileData) && ReferenceEquals(connection, Netplay.Connection); } }
        internal bool ControlsEnabled { get { return HasSession && preference.Snapshot.IsLoaded; } }
        internal bool ToolbarEnabled { get { return preference.Snapshot.Value; } }
        internal bool Local(Player candidate) { return candidate != null && ReferenceEquals(candidate, Main.LocalPlayer); }
        internal int BlockedActions { get { return State.BlockedActions; } }
        internal bool WorldActionsBlocked { get { return BlockedActions != 0; } }
        internal bool NativeUiBusy { get { return Main.playerInventory || Main.mapFullscreen || Main.blockInput || Main.drawingPlayerChat ||
            Main.editSign || Main.editChest || !string.IsNullOrEmpty(Main.npcChatText) || Main.inFancyUI || Main.onlyDrawFancyUI || Main.ingameOptionsWindow ||
            PlayerInput.WritingText || Main.CurrentInputTextTakerOverride != null || Terraria.Graphics.Capture.CaptureManager.Instance?.Active == true; } }
        internal bool CanEdit { get { return Input.CanStartActions && CanRetainEdit; } }
        // An outer Update can contain no native input stage. It revokes new
        // execution, but does not invalidate a previously frozen focused sample.
        internal bool CanRetainEdit { get { return HasSession && Input.CanRetainIntent && !NativeUiBusy && !Main.gamePaused && !player.dead && !player.ghost &&
            !(Shell?.State.Visible ?? false) && !(Shell?.OwnsPointer ?? false) && !(Shell?.TargetGestureBusy?.Invoke() ?? false) && !PlayerInput.Triggers.Current.MapFull && !PlayerInput.Triggers.Current.ToggleCameraMode; } }
        internal bool ShowToolbar { get { return ControlsEnabled && ToolbarEnabled && Input.CanPrepareText && !NativeUiBusy && !Main.hideUI &&
            !PlayerInput.Triggers.Current.MapFull && !PlayerInput.Triggers.Current.ToggleCameraMode &&
            !(Shell?.State.Visible ?? false) && !(Shell?.OwnsPointer ?? false) && !(Shell?.TargetGestureBusy?.Invoke() ?? false) && !player.dead && !player.ghost && player.HeldItem != null && player.HeldItem.type == Terraria.ID.ItemID.Gel && player.HeldItem.stack > 0; } }
        internal void SetToolbar(bool value) { if (ControlsEnabled) preference.Set(value); }
        internal void Toggle()
        {
            if (!ControlsEnabled || !Input.CanStartActions) return;
            Pointer.Cancel(); Toolbar.Cancel();
            if (State.Active) State.Exit();
            else { State.Enter(); Shell?.CloseAndSubmitPosition(); }
        }
        internal bool Save() { return false; }
        internal void Register(HotkeyRegistry registry, Hotkeys.HotkeyStateFeedback feedback = null)
        {
            registry.Register(new HotkeyAction(BlueprintControls.CaptureAction, "采集蓝图", HotkeyContext.Gameplay, () => ControlsEnabled, Toggle));
            // Save remains configurable through the common binder but has no
            // executable success path, including direct owner callers.
            registry.Register(new HotkeyAction(BlueprintControls.SaveAction, "保存采集", HotkeyContext.Gameplay, () => false, () => Save()));
            // F5 and the independent display binding share the preference owner;
            // hiding the toolbar never exits capture or releases its world gate.
            Action toolbar = () => SetToolbar(!ToolbarEnabled);
            if (feedback != null) toolbar = feedback.Immediate(BlueprintControls.ToolbarAction, "手持凝胶显示快捷栏", toolbar, () => ToolbarEnabled ? 1 : 0, () => ControlsEnabled);
            registry.Register(new HotkeyAction(BlueprintControls.ToolbarAction, "手持凝胶显示快捷栏", HotkeyContext.Gameplay, () => ControlsEnabled, toolbar));
        }
        private void ObserveMapping()
        {
            State.Observe(Input.SampleFocused, Input.RawMouseLeft, Input.RawMouseRight, Input.NativeUseItemRequest, Input.NativeUseTileRequest);
            if (!Input.SampleFocused) { Pointer.Cancel(); Toolbar.Cancel(); }
        }
        internal void ProcessInput()
        {
            // Consume at most the immediately preceding mapping. Draw gaps
            // retain a held draft, never queue an uncertified click/release.
            Pointer.Consume(CanEdit);
            Toolbar.Process();
            if (!CanEdit) { State.Interrupt(); Pointer.Cancel(); return; }
            Pointer.Capture();
        }
        public void OnSessionStarted()
        { State.Exit(); Pointer.Cancel(); Toolbar.Cancel(); generation = runtime.Generation; player = Main.LocalPlayer; world = Main.ActiveWorldFileData; connection = Netplay.Connection; }
        public void OnSessionEnded()
        { State.Exit(); Pointer.Cancel(); Toolbar.Cancel(); player = null; world = connection = null; }
        public void Update(ulong updateTick)
        {
            if (!preference.Snapshot.IsLoaded && loading.ElapsedMilliseconds >= 2000) preference.AbandonSlowLoad();
            if (State.Active && (!HasSession || !Input.CanRetainIntent || NativeUiBusy || player.dead || player.ghost || Shell?.State.Visible == true))
            { State.Interrupt(); Pointer.Cancel(); }
        }
        internal void Poll() { if (!preference.Snapshot.IsLoaded && loading.ElapsedMilliseconds >= 2000) preference.AbandonSlowLoad(); }
        public void FailClosed() { failed = true; State.Exit(); Pointer.Cancel(); Toolbar.Cancel(); }
        internal void TakeFeedback(Action<string> display)
        {
            var snapshot = preference.Snapshot;
            if (!snapshot.IsLoaded || reported == snapshot.Status) return;
            if (snapshot.Status == PreferenceStatus.Missing || snapshot.Status == PreferenceStatus.Saved || snapshot.Status == PreferenceStatus.Pending) return;
            display(snapshot.CommitUnconfirmed ? "无法确认凝胶快捷栏设置是否保存成功；本次仍可使用，原文件已保护。" : "凝胶快捷栏设置未能可靠读取或保存；当前修改仅本次有效，原文件已保留。");
            reported = snapshot.Status;
        }
        private void ExitProcess(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= ExitProcess; preference.Stop(750); Toolbar.Dispose(); Pointer.Dispose(); BlueprintHooks.Uninstall(); }
    }
}
