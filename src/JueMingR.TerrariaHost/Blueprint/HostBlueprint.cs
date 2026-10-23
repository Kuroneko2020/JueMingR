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
    internal sealed partial class HostBlueprint : IRuntimeFeature
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
        internal HostBlueprint(string directory, SingleFeatureRuntime runtime, HostInputState input, World.WorldTileObservation geometry, LibraryState library = null)
        {
            this.runtime = runtime; Input = input;
            Library = library;
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
        internal int BlockedActions { get { State.SetInteractionBlocked(ProjectionEditing || Shell?.BlueprintUi?.WorldInteractionBlocked == true); return State.BlockedActions; } }
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
        internal void OpenLibrary()
        {
            if (!ControlsEnabled || !Input.CanStartActions) return;
            Pointer.Cancel(); Toolbar.Cancel(); State.Interrupt();
            Shell?.BlueprintUi?.OpenLibrary();
        }
        internal void Toggle()
        {
            if (!ControlsEnabled || !Input.CanStartActions) return;
            if (ProjectionEditing || pendingProjection != null) { projectionFeedback = "请先完成或取消当前摆放"; return; }
            Pointer.Cancel(); Toolbar.Cancel();
            if (State.Active) State.Exit();
            else { State.Enter(); Shell?.CloseAndSubmitPosition(); }
        }
        internal void Register(HotkeyRegistry registry, Hotkeys.HotkeyStateFeedback feedback = null)
        {
            registry.Register(new HotkeyAction(BlueprintControls.CaptureAction, "采集蓝图", HotkeyContext.Gameplay, () => ControlsEnabled, Toggle));
            registry.Register(new HotkeyAction(BlueprintControls.SaveAction, "保存采集", HotkeyContext.Gameplay, () => CanSave, () => Save()));
            registry.Register(new HotkeyAction(BlueprintControls.LibraryAction, "打开蓝图库", HotkeyContext.Gameplay, () => ControlsEnabled, OpenLibrary));
            registry.Register(new HotkeyAction(BlueprintControls.MoveAction, "移动蓝图", HotkeyContext.Gameplay, () => CanMoveProjection, MoveProjection));
            registry.Register(new HotkeyAction(BlueprintControls.RemoveAction, "移除蓝图", HotkeyContext.Gameplay, () => CanRemoveProjection, RemoveProjection));
            // F5 and the independent display binding share the preference owner;
            // hiding the toolbar never exits capture or releases its world gate.
            Action toolbar = () => SetToolbar(!ToolbarEnabled);
            if (feedback != null) toolbar = feedback.Immediate(BlueprintControls.ToolbarAction, "手持凝胶显示快捷栏", toolbar, () => ToolbarEnabled ? 1 : 0, () => ControlsEnabled);
            registry.Register(new HotkeyAction(BlueprintControls.ToolbarAction, "手持凝胶显示快捷栏", HotkeyContext.Gameplay, () => ControlsEnabled, toolbar));
        }
        private void ObserveMapping()
        {
            State.SetInteractionBlocked(ProjectionEditing || Shell?.BlueprintUi?.WorldInteractionBlocked == true);
            State.Observe(Input.SampleFocused, Input.RawMouseLeft, Input.RawMouseRight, Input.NativeUseItemRequest, Input.NativeUseTileRequest);
            if (!Input.SampleFocused) { Pointer.Cancel(); Toolbar.Cancel(); }
        }
        internal void ProcessInput()
        {
            // Consume at most the immediately preceding mapping. Draw gaps
            // retain a held draft, never queue an uncertified click/release.
            Pointer.Consume(CanEdit);
            Toolbar.Process();
            if (!CanEdit) {
                State.Interrupt(); if (ProjectionEditing) InterruptProjection(); Pointer.Cancel(); return; }
            if (ProjectionEditing && Input.Hotkeys.IsNew((int)Microsoft.Xna.Framework.Input.Keys.Escape))
            { Input.Hotkeys.SuppressKey((int)Microsoft.Xna.Framework.Input.Keys.Escape); CancelProjection(); return; }
            Pointer.Capture();
        }
        public void OnSessionStarted()
        {
            CancelOldCapture();
            ClearProjection();
            State.Exit(); Pointer.Cancel(); Toolbar.Cancel(); generation = runtime.Generation; player = Main.LocalPlayer; world = Main.ActiveWorldFileData; connection = Netplay.Connection; }
        public void OnSessionEnded()
        {
            CancelOldCapture();
            ClearProjection();
            State.Exit(); Pointer.Cancel(); Toolbar.Cancel(); player = null; world = connection = null; }
        public void Update(ulong updateTick)
        {
            AdvanceCapture(updateTick);
            PumpProjection();
            if (!preference.Snapshot.IsLoaded && loading.ElapsedMilliseconds >= 2000) preference.AbandonSlowLoad();
            if (State.Active && (!HasSession || !Input.CanRetainIntent || NativeUiBusy || player.dead || player.ghost || Shell?.State.Visible == true))
            {
                State.Interrupt(); Pointer.Cancel(); }
        }
        internal void Poll() { PumpLibrary(); PumpProjection(); if (!preference.Snapshot.IsLoaded && loading.ElapsedMilliseconds >= 2000) preference.AbandonSlowLoad(); }
        public void FailClosed() {
            failed = true; CancelOldCapture(); ClearProjection(); State.Exit(); Pointer.Cancel(); Toolbar.Cancel(); }
        internal void TakeFeedback(Action<string> display)
        {
            if (projectionFeedback != null) { string message = projectionFeedback; projectionFeedback = null; display(message); }
            var snapshot = preference.Snapshot;
            if (!snapshot.IsLoaded || reported == snapshot.Status) return;
            if (snapshot.Status == PreferenceStatus.Missing || snapshot.Status == PreferenceStatus.Saved || snapshot.Status == PreferenceStatus.Pending) return;
            display(snapshot.CommitUnconfirmed ? "无法确认凝胶快捷栏设置是否保存成功；本次仍可使用，原文件已保护。" : "凝胶快捷栏设置未能可靠读取或保存；当前修改仅本次有效，原文件已保留。");
            reported = snapshot.Status;
        }
        private void ExitProcess(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= ExitProcess; CancelOldCapture(); ClearProjection(); ProjectionRenderer.Dispose(); Library?.Stop(1500); preference.Stop(750); Toolbar.Dispose(); Pointer.Dispose(); BlueprintHooks.Uninstall(); }
    }
}
