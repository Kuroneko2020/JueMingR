using System;
using System.Diagnostics;
using System.IO;
using JueMingR.Features.WorldTargets;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.TerrariaHost.WorldTargets
{
    internal sealed class HostWorldTargets : IRuntimeFeature
    {
        private readonly PreferenceDocument<WorldTargetSettings> preferences;
        private readonly SingleFeatureRuntime runtime;
        private readonly WorldTargetHostObservation source;
        private readonly Stopwatch startup = Stopwatch.StartNew();
        private long appliedRevision = -1;
        private bool stopping;
        private string reportedPreference, reportedFailure;
        internal readonly WorldTargetFeature Feature;
        internal readonly WorldTargetWorldLayer World;
        private const string LayerUnavailableMessage = "附近目标暂时无法显示，设置已保留。";
        private Rendering.WorldLayerStatus layerStatus;
        internal Rendering.WorldLayerStatus LayerStatus
        {
            get { return layerStatus; }
            set
            {
                // Native setup can recover while UI feedback is suppressed.
                // Reset only a previous layer alert, once at that transition.
                if (layerStatus == Rendering.WorldLayerStatus.Unavailable && value == Rendering.WorldLayerStatus.Ready && reportedFailure == LayerUnavailableMessage) reportedFailure = null;
                layerStatus = value;
            }
        }
        internal bool LayersReady { get { return LayerStatus == Rendering.WorldLayerStatus.Ready; } }
        internal long SessionGeneration { get { return runtime.IsSessionActive ? runtime.Generation : -1; } }
        internal HostWorldTargets(string gameDirectory, SingleFeatureRuntime runtime, World.WorldTileObservation world = null)
        {
            this.runtime = runtime; source = new WorldTargetHostObservation(() => runtime.IsSessionActive, world);
            Feature = new WorldTargetFeature(source);
            preferences = new PreferenceDocument<WorldTargetSettings>(new AtomicFileDocument(Path.Combine(gameDirectory,
                "JueMingRData", "config", "features", "world-targets.json"), 65536, true), new WorldTargetCodec(), WorldTargetSettings.Default);
            World = new WorldTargetWorldLayer(Feature.Targets, () => runtime.IsSessionActive && LayersReady, () => Preferences.Value);
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        internal PreferenceSnapshot<WorldTargetSettings> Preferences { get { return preferences.Snapshot; } }
        internal bool CanConfigure { get { return !stopping && Preferences.IsLoaded; } }
        // No accOreFinder gate here: desired state/bindings remain configurable.
        internal bool ControlsEnabled { get { return CanConfigure && runtime.IsSessionActive && LayersReady && !Feature.HasFailed; } }
        public bool Enabled { get { return Feature.Enabled; } }
        internal void PollPreferences()
        {
            if (!Preferences.IsLoaded && startup.ElapsedMilliseconds >= 2000) preferences.AbandonSlowLoad();
            var snapshot = Preferences;
            if (snapshot.IsLoaded && snapshot.Revision != appliedRevision)
            { appliedRevision = snapshot.Revision; Feature.Configure(snapshot.Value); if (!snapshot.Value.AnyEnabled) World.Clear(); }
        }
        private bool Change(WorldTargetSettings value)
        { if (stopping || !preferences.Set(value)) return false; PollPreferences(); return true; }
        internal bool SetEnabled(WorldTargetKind kind, bool enabled) { return Change(Preferences.Value.WithEnabled(kind, enabled)); }
        internal bool Toggle(WorldTargetKind kind) { return Change(Preferences.Value.Toggle(kind)); }
        internal bool SetColor(WorldTargetKind kind, int rgb) { return Change(Preferences.Value.WithColor(kind, rgb)); }
        internal bool ResetColor(WorldTargetKind kind) { return Change(Preferences.Value.ResetColor(kind)); }
        public void OnSessionStarted() { source.EndSession(); World.Clear(); Feature.OnSessionStarted(); }
        public void OnSessionEnded() { Feature.OnSessionEnded(); source.EndSession(); World.Clear(); }
        public void Update(ulong tick) { if (World.Failure != null) Feature.FailClosed(); Feature.Update(tick); }
        public void FailClosed() { Feature.FailClosed(); source.EndSession(); World.Clear(); }
        internal string PreferenceMessage
        {
            get
            {
                var snapshot = Preferences;
                if (snapshot.CommitUnconfirmed) return "无法确认附近目标设置是否保存成功；本次仍可使用，文件已保护。";
                switch (snapshot.Status)
                {
                    case PreferenceStatus.Loading: return "正在加载附近目标设置";
                    case PreferenceStatus.Missing: case PreferenceStatus.Pending: case PreferenceStatus.Saved: return null;
                    case PreferenceStatus.UnsupportedVersion: case PreferenceStatus.UnknownFields: return "附近目标设置含当前版本不支持的内容；当前修改仅本次有效，原文件已保留。";
                    case PreferenceStatus.Invalid: return "附近目标设置格式有误；当前修改仅本次有效，原文件已保留。";
                    case PreferenceStatus.Conflict: return "附近目标设置文件已有变化，已停止保存；当前修改仅本次有效。";
                    case PreferenceStatus.Busy: return "附近目标设置正被其他程序使用；当前修改仅本次有效。";
                    default: return "附近目标设置加载或保存失败；当前修改仅本次有效，原文件已保留。";
                }
            }
        }
        internal void TakeFeedback(Action<string> display)
        {
            string message = PreferenceMessage;
            if (Preferences.IsLoaded && message != null && message != reportedPreference) { display(message); reportedPreference = message; }
            string failure = Feature.HasFailed || World.Failure != null ? "附近目标显示已停止，设置已保留。" :
                Enabled && LayerStatus == Rendering.WorldLayerStatus.Unavailable ? LayerUnavailableMessage : null;
            // Preserve per-cause feedback and layer recovery without exposing reason IDs.
            string key = Feature.HasFailed || World.Failure != null ? "stopped:" + (World.Failure ?? "observation-unavailable") : failure;
            if (failure != null && key != reportedFailure) { display(failure); reportedFailure = key; }
            if (failure == null) reportedFailure = null;
        }
        private void OnExit(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= OnExit; stopping = true; preferences.Stop(750); }
    }
}
