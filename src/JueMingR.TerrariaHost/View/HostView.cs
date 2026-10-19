using System;
using System.IO;
using System.Diagnostics;
using JueMingR.Features.View;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.Graphics.Capture;

namespace JueMingR.TerrariaHost.View
{
    // Permission, native target, and actual camera coverage are independent.
    // In particular disabling permission must leave a selected wide view intact.
    internal sealed class HostView : IRuntimeFeature, IViewControls
    {
        internal const float MinimumZoom = ViewZoom.Minimum;
        private readonly SingleFeatureRuntime runtime;
        private readonly HostInputState input;
        private readonly PreferenceDocument<bool> preference;
        private bool ready, failed, stopping;
        private readonly Stopwatch startup = Stopwatch.StartNew();
        private PreferenceStatus? seenPreference;
        private readonly ViewSectionRequests sections;
        internal readonly ViewSceneCapture Scene = new ViewSceneCapture();
        internal readonly ViewWaterResources Water = new ViewWaterResources();
        internal readonly ViewTileCache Cache = new ViewTileCache();
        internal readonly ViewNativeCoverage Native = new ViewNativeCoverage();
        private long consumedMapping = -1;
        internal Exception SetupError { get; private set; }
        internal string Failure { get; private set; }
        internal HostView(string directory, SingleFeatureRuntime runtime, HostInputState input)
        {
            this.runtime = runtime; this.input = input;
            sections = new ViewSectionRequests(message => Failure = message);
            preference = new PreferenceDocument<bool>(new AtomicFileDocument(Path.Combine(directory, "JueMingRData", "config", "features", "view-unlock.json"), 65536, true), new ViewPreferenceCodec(), false);
            try { ViewHooks.Install(this); ready = true; }
            catch (Exception error) { SetupError = error; Failure = "视距解锁暂不可用，原版操作不受影响。"; }
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        public bool Enabled { get { return true; } }
        public bool ControlsEnabled { get { return ready && !failed && !stopping && runtime.IsSessionActive && preference.Snapshot.IsLoaded; } }
        public bool UnlockEnabled { get { return preference.Snapshot.Value; } }
        public bool SetUnlockEnabled(bool value) { return ControlsEnabled && preference.Set(value); }
        internal bool HasWorld { get { return ready && !failed && !stopping && runtime.IsSessionActive && !Main.gameMenu && !Main.dedServ && Main.netMode != 2; } }
        internal bool HasExtendedView { get { return HasWorld && ViewZoom.IsExpanded(Main.GameViewMatrix.RenderZoom.X,Main.ForcedMinimumZoom); } }
        // Section visibility follows the final semantic view. Renderer repair
        // additionally accounts for the native group's real capped capacity;
        // neither fact is the permission switch or a temporary cache leaf.
        internal bool NeedsExpandedRendering { get { return HasWorld && !Cache.Rendering && !Native.Rendering && ViewZoom.RequiresRenderer(Main.GameViewMatrix.RenderZoom.X,Main.Camera.ScaledSize,ViewZoom.NativeCapacity(Main.screenWidth,Main.screenHeight),Lighting.NotRetro); } }
        internal bool RetainsTarget { get { return HasWorld && (Main.GameZoomTarget < 1f || Main.GameZoomTarget > 2f); } }
        internal ViewRenderPlan Plan { get; set; }
        internal float MinimumTarget
        {
            get
            {
                return ViewZoom.MinimumTarget(Main.screenWidth, Main.screenHeight, Main.ForcedMinimumZoom);
            }
        }
        internal bool CanZoom
        {
            get
            {
                var capture = CaptureManager.Instance;
                return HasWorld && input.CanUseMappedViewInput && !Main.inFancyUI && !Main.onlyDrawFancyUI &&
                    !Main.ingameOptionsWindow && !Main.mapFullscreen && !Main.blockInput && !Main.drawingPlayerChat &&
                    !PlayerInput.WritingText && Main.CurrentInputTextTakerOverride == null && !Main.editSign && !Main.editChest &&
                    (capture == null || !capture.Active);
            }
        }
        internal void ConsumeZoom()
        {
            if (!CanZoom || consumedMapping == input.Mapping) return;
            consumedMapping = input.Mapping;
            int direction = (PlayerInput.Triggers.Current.ViewZoomIn ? 1 : 0) - (PlayerInput.Triggers.Current.ViewZoomOut ? 1 : 0);
            if (direction == 0) return;
            float current = Main.GameZoomTarget;
            if (float.IsNaN(current) || float.IsInfinity(current) || current <= 0f) return;
            // ON and retained inward travel use a reciprocal relative step;
            // ordinary OFF keeps the native absolute step. This consumes only
            // the current original Mapping, without a new key source or clock.
            Main.GameZoomTarget = ViewZoom.Step(current, direction, UnlockEnabled, MinimumTarget);
        }
        public void OnSessionStarted() { consumedMapping = -1; sections.Clear(); }
        public void OnSessionEnded() { consumedMapping = -1; sections.Clear(); Native.Dispose(); Cache.Dispose(); Scene.Dispose(); Water.Dispose(); }
        public void Update(ulong tick)
        {
            if (!preference.Snapshot.IsLoaded && startup.ElapsedMilliseconds >= 2000) preference.AbandonSlowLoad();
            sections.Update(tick, HasExtendedView);
        }
        public void FailClosed() { failed = true; }
        internal void RenderingFailed()
        {
            if (failed) return;
            // A resource failure is an exceptional recovery, distinct from a
            // normal OFF click. Stop further wider preparation for this owner;
            // intact native lighting/resources can draw the ordinary view.
            failed = true; sections.Clear(); Native.Dispose(); Cache.Dispose(); Scene.Dispose(); Water.Dispose(); Main.GameZoomTarget = Math.Max(1f, Main.GameZoomTarget); Main.renderNow = true;
            Failure = "视距绘制暂不可用，已恢复普通视野；本次已停用，请重启游戏后再试。";
        }
        public void TakeFeedback(Action<string> display)
        {
            if (Failure != null) { string value = Failure; display(value); Failure = null; return; }
            var current = preference.Snapshot;
            if (!current.IsLoaded || current.Status == seenPreference) return;
            string message;
            if (current.CommitUnconfirmed) message = "无法确认视距解锁的设置是否保存成功；本次仍可使用，文件已保护。";
            else switch (current.Status)
            {
                case PreferenceStatus.Missing: case PreferenceStatus.Saved: case PreferenceStatus.Pending: return;
                case PreferenceStatus.UnsupportedVersion: case PreferenceStatus.UnknownFields:
                    message = "视距解锁的设置含当前版本不支持的内容；当前修改仅本次有效，原文件已保留。"; break;
                case PreferenceStatus.Invalid: message = "视距解锁的设置格式有误；当前修改仅本次有效，原文件已保留。"; break;
                case PreferenceStatus.Conflict: message = "视距解锁的设置文件已有变化，已停止保存；当前修改仅本次有效。"; break;
                default: message = "视距解锁的设置加载或保存失败；当前修改仅本次有效，原文件已保留。"; break;
            }
            display(message); seenPreference = current.Status;
        }
        private void OnExit(object sender, EventArgs args)
        {
            if (stopping) return; stopping = true;
            AppDomain.CurrentDomain.ProcessExit -= OnExit;
            ViewHooks.Uninstall(this); Native.Dispose(); Cache.Dispose(); Scene.Dispose(); Water.Dispose(); preference.Stop(750);
        }
    }
}
