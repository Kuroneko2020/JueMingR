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
        private readonly PreferenceDocument<BlueprintPreferences> preference;
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
            preference = new PreferenceDocument<BlueprintPreferences>(new AtomicFileDocument(Path.Combine(directory, "JueMingRData", "config", "features", "blueprint.json"), 65536, true), new BlueprintPreferenceCodec(), new BlueprintPreferences(false, false));
            projectionHistory = new BlueprintSceneHistory(scope => new AtomicFileDocument(Path.Combine(directory, "JueMingRData", "blueprints", "instances", scope, "scene.bin"), Int32.MaxValue, true, ".schema1-original"),
                (storage, value) => { if (value.SourceVersion == 1) ((AtomicFileDocument)storage).RetainLoadedSource(); });
            World = new BlueprintWorldLayer(this, geometry); Toolbar = new BlueprintToolbar(this);
            Pointer = new BlueprintPointerProof(this);
            try { BlueprintHooks.Install(this); Pointer.Install(); projectionChanges.Install(); ready = true; }
            catch (Exception error) { SetupError = error; BlueprintHooks.Uninstall(); Pointer.Dispose(); projectionChanges.Dispose(); }
            Input.BlueprintMapping = ObserveMapping;
            AppDomain.CurrentDomain.ProcessExit += ExitProcess;
        }
        public bool Enabled { get { return true; } }
        internal bool HasSession { get { return ready && !failed && runtime.IsSessionActive && generation == runtime.Generation &&
            !Main.gameMenu && ReferenceEquals(player, Main.LocalPlayer) && ReferenceEquals(world, Main.ActiveWorldFileData) && ReferenceEquals(connection, Netplay.Connection); } }
        internal bool ControlsEnabled { get { return HasSession && preference.Snapshot.IsLoaded; } }
        internal bool ToolbarEnabled { get { return preference.Snapshot.Value.GelToolbar; } }
        internal bool Local(Player candidate) { return candidate != null && ReferenceEquals(candidate, Main.LocalPlayer); }
        // This is the world editor's gate. Pure material viewing owns its
        // pointer and physical tails in F5Shell; treating visibility as an
        // editor barrier also erases unrelated automatic held-item presses.
        internal int BlockedActions { get { State.SetInteractionBlocked(ProjectionEditing || Shell?.BlueprintUi?.WorldInteractionBlocked == true); return State.BlockedActions; } }
        internal bool WorldActionsBlocked { get { return BlockedActions != 0; } }
        internal bool NativeUiBusy { get { return Main.playerInventory || Main.mapFullscreen || Main.blockInput || Main.drawingPlayerChat ||
            Main.editSign || Main.editChest || !string.IsNullOrEmpty(Main.npcChatText) || Main.inFancyUI || Main.onlyDrawFancyUI || Main.ingameOptionsWindow ||
            PlayerInput.WritingText || Main.CurrentInputTextTakerOverride != null || Terraria.Graphics.Capture.CaptureManager.Instance?.Active == true; } }
        internal bool CanEdit { get { return Input.CanStartActions && CanRetainEdit; } }
        // An outer Update can contain no native input stage. It revokes new
        // execution, but does not invalidate a previously frozen focused sample.
        private bool CanRetainWorldInput { get { return HasSession && Input.CanRetainIntent && !NativeUiBusy && !Main.gamePaused && !player.dead && !player.ghost &&
            !(Shell?.BlueprintMaterialsUi?.Visible ?? false) && !(Shell?.TargetGestureBusy?.Invoke() ?? false) && !PlayerInput.Triggers.Current.MapFull && !PlayerInput.Triggers.Current.ToggleCameraMode; } }
        internal bool CanRetainEdit { get { return CanRetainWorldInput && !(Shell?.State.Visible ?? false) && !(Shell?.OwnsPointer ?? false); } }
        internal bool ShowToolbar { get { return ControlsEnabled && ToolbarEnabled && Input.CanPrepareText && !NativeUiBusy && !Main.hideUI &&
            !PlayerInput.Triggers.Current.MapFull && !PlayerInput.Triggers.Current.ToggleCameraMode &&
            !(Shell?.State.Visible ?? false) && !(Shell?.BlueprintMaterialsUi?.Visible ?? false) && !(Shell?.OwnsPointer ?? false) && !(Shell?.TargetGestureBusy?.Invoke() ?? false) && !player.dead && !player.ghost && player.HeldItem != null && player.HeldItem.type == Terraria.ID.ItemID.Gel && player.HeldItem.stack > 0; } }
        internal void SetToolbar(bool value) { if (ControlsEnabled) preference.Set(new BlueprintPreferences(value, AutoBuildEnabled)); }
        // Display and cached UI use stable business eligibility. Every action
        // entry independently checks this Update's completed input permission.
        internal bool CanOpenLibrary { get { return ManualLibraryDisabledReason == null; } }
        internal string ManualLibraryDisabledReason
        {
            get {
                if (!ControlsEnabled) return "蓝图当前不可用";
                if (Library == null) return "蓝图库暂不可用";
                if (HasActiveCaptureWork) return "请先完成或退出当前采集";
                if (ProjectionEditing || pendingProjection != null) return "请先完成或取消当前蓝图操作";
                if (Library.Snapshot.Busy) return "蓝图库正在处理，请稍候";
                return null;
            }
        }
        internal void OpenLibrary()
        {
            // Reject before touching pointer/capture/scene ownership. Internal
            // Saved completion uses ShowSaved and does not pass this manual gate.
            if (!CanOpenLibrary || !Input.CanStartActions) return;
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
            registry.Register(new HotkeyAction(BlueprintControls.LibraryAction, "打开蓝图库", HotkeyContext.Gameplay, () => CanOpenLibrary, OpenLibrary));
            registry.Register(new HotkeyAction(BlueprintControls.MoveAction, "移动蓝图", HotkeyContext.Gameplay, () => CanInvokeMove, MoveProjection));
            registry.Register(new HotkeyAction(BlueprintControls.RemoveAction, "移除蓝图", HotkeyContext.Gameplay, () => CanInvokeRemove, RemoveProjection));
            registry.Register(new HotkeyAction(BlueprintControls.MirrorAction, "水平镜像", HotkeyContext.Gameplay, () => CanInvokeMirror, MirrorProjection));
            registry.Register(new HotkeyAction(BlueprintControls.TrimAction, "局部修剪", HotkeyContext.Gameplay, () => CanInvokeTrim, TrimProjection));
            registry.Register(new HotkeyAction(BlueprintControls.ConfirmTrimAction, "确定修剪", HotkeyContext.Gameplay, () => CanInvokeConfirmTrim, RequestTrimConfirm));
            // F5 and the independent display binding share the preference owner;
            // hiding the toolbar never exits capture or releases its world gate.
            Action toolbar = () => SetToolbar(!ToolbarEnabled);
            if (feedback != null) toolbar = feedback.Immediate(BlueprintControls.ToolbarAction, "手持凝胶显示快捷栏", toolbar, () => ToolbarEnabled ? 1 : 0, () => ControlsEnabled);
            registry.Register(new HotkeyAction(BlueprintControls.ToolbarAction, "手持凝胶显示快捷栏", HotkeyContext.Gameplay, () => ControlsEnabled, toolbar));
            Action automatic = () => SetAutoBuild(!AutoBuildEnabled);
            if (feedback != null) automatic = feedback.Immediate(BlueprintControls.AutoBuildAction, "自动建造", automatic, () => AutoBuildEnabled ? 1 : 0, () => ControlsEnabled);
            registry.Register(new HotkeyAction(BlueprintControls.AutoBuildAction, "自动建造", HotkeyContext.Gameplay, () => ControlsEnabled, automatic));
            registry.Register(new HotkeyAction(BlueprintControls.MaterialsAction, "施工材料", HotkeyContext.Gameplay, () => CanOpenMaterials, OpenMaterials));
        }
        private void ObserveMapping()
        {
            State.SetInteractionBlocked(ProjectionEditing || Shell?.BlueprintUi?.WorldInteractionBlocked == true);
            State.Observe(Input.SampleFocused, Input.RawMouseLeft, Input.RawMouseRight, Input.NativeUseItemRequest, Input.NativeUseTileRequest);
            if (!Input.SampleFocused) { Pointer.Cancel(); Toolbar.Cancel(); }
        }
        internal void ProcessInput()
        {
            // A current higher-owner cancellation wins before a certified old
            // click can publish. Native right-click still comes only from its
            // own certificate, never from raw polling of another UI's input.
            if (CanEdit && ProjectionEditing && Input.Hotkeys.IsNew((int)Microsoft.Xna.Framework.Input.Keys.Escape))
            { Input.Hotkeys.SuppressKey((int)Microsoft.Xna.Framework.Input.Keys.Escape); CancelProjection(); return; }
            Toolbar.Process();
            // Consume at most the immediately preceding mapping. Draw gaps
            // retain a held draft, never queue an uncertified click/release.
            Pointer.Consume(CanEdit);
            if (!CanEdit) {
                // Ordinary F5 visibility still suspends every world gesture.
                // Only a public Mouse1 confirm already accepted in this exact
                // mapping may retain its native UI proof; it cannot drag/select.
                bool confirm = CanRetainTrimConfirmation(Input.Mapping);
                State.Interrupt(); if (ProjectionEditing) InterruptProjectionGesture(confirm);
                ArbitrateCurrentTrim();
                if (confirm) Pointer.CaptureTrimConfirmation(); else Pointer.Cancel();
                return; }
            ArbitrateCurrentTrim();
            Pointer.Capture();
        }
        public void OnSessionStarted()
        {
            CancelOldCapture();
            ClearProjection(); ClearBuildSession();
            StartBuildUnknownSession();
            State.Exit(); Pointer.Cancel(); Toolbar.Cancel(); generation = runtime.Generation; projectionChanges.SetSession(generation); player = Main.LocalPlayer; world = Main.ActiveWorldFileData; connection = Netplay.Connection; BeginProjectionSession(); }
        public void OnSessionEnded()
        {
            CancelOldCapture();
            EndProjectionSession();
            ClearProjection(); ClearBuildSession();
            State.Exit(); Pointer.Cancel(); Toolbar.Cancel(); player = null; world = connection = null; }
        public void Update(ulong updateTick)
        {
#if JMR_BLUEPRINT_DIAGNOSTIC
            BlueprintBuildTrace.Hit(BuildTraceStage.BlueprintUpdate);
#endif
            AdvanceCapture(updateTick);
            PumpProjectionPersistence();
            PumpProjection();
            PumpProjectionEditor();
            UpdateMaterials(updateTick);
            AdvanceProjectionMatch(updateTick);
            AdvanceAutomaticBuild();
            AcceptProjectionSceneSnapshot();
            if (!preference.Snapshot.IsLoaded && loading.ElapsedMilliseconds >= 2000) preference.AbandonSlowLoad();
            if (State.Active && (!HasSession || !Input.CanRetainIntent || NativeUiBusy || player.dead || player.ghost || Shell?.State.Visible == true))
            {
                State.Interrupt(); Pointer.Cancel(); }
        }
        internal void Poll() { PumpLibrary(); PumpProjectionPersistence(); PumpProjection(); AcceptProjectionSceneSnapshot(); if (!preference.Snapshot.IsLoaded && loading.ElapsedMilliseconds >= 2000) preference.AbandonSlowLoad(); }
        public void FailClosed() {
#if JMR_BLUEPRINT_DIAGNOSTIC
            BlueprintBuildTrace.Hit(BuildTraceStage.FailClosed);
            BlueprintBuildTrace.Semantic(BuildTraceStage.FailClosed,BuildTraceReason.BlueprintFailed);
#endif
            failed = true; CancelOldCapture(); EndProjectionSession(); ClearProjection(); State.Exit(); Pointer.Cancel(); Toolbar.Cancel(); }
        internal void TakeFeedback(Action<string> display)
        {
            TakeProjectionPersistenceFeedback(display);
            if (projectionFeedback != null) { string message = projectionFeedback; projectionFeedback = null; display(message); }
            var snapshot = preference.Snapshot;
            if (!snapshot.IsLoaded || reported == snapshot.Status) return;
            if (snapshot.Status == PreferenceStatus.Missing || snapshot.Status == PreferenceStatus.Saved || snapshot.Status == PreferenceStatus.Pending) return;
            display(snapshot.CommitUnconfirmed ? "无法确认蓝图设置是否保存成功；本次仍可使用，原文件已保护。" : "蓝图设置未能可靠读取或保存；当前修改仅本次有效，原文件已保留。");
            reported = snapshot.Status;
        }
        private void ExitProcess(object sender, EventArgs args)
        {
            AppDomain.CurrentDomain.ProcessExit -= ExitProcess;
            // ProcessExit never samples live scene/edit state for persistence.
            // It only drains immutable values accepted on the game thread.
            var elapsed = Stopwatch.StartNew(); projectionHistory.Stop(1500);
            CancelOldCapture(); ClearProjection(); projectionChanges.Dispose(); ProjectionRenderer.Dispose();
            Library?.Stop(Math.Max(0, 2250 - (int)elapsed.ElapsedMilliseconds)); preference.Stop(Math.Max(0, 2250 - (int)elapsed.ElapsedMilliseconds));
            Toolbar.Dispose(); Pointer.Dispose(); BlueprintHooks.Uninstall();
        }
    }
}
