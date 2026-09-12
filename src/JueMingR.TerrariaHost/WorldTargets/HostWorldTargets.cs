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
        internal bool LayersReady { get; set; }
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
                if (snapshot.CommitUnconfirmed) return "保存结果未确认，目标配置已保护；当前选择仅能确认在本次内存生效。";
                switch (snapshot.Status)
                {
                    case PreferenceStatus.Loading: return "正在读取附近目标设置";
                    case PreferenceStatus.Missing: case PreferenceStatus.Pending: case PreferenceStatus.Saved: return null;
                    case PreferenceStatus.UnsupportedVersion: case PreferenceStatus.UnknownFields: return "附近目标设置仅本次有效：版本或字段不受支持，原文件已保留。";
                    case PreferenceStatus.Invalid: return "附近目标配置格式有误，原文件已保留；本次选择只在内存生效。";
                    case PreferenceStatus.Conflict: return "附近目标配置发生外部变化，已停止覆盖；本次选择只在内存生效。";
                    case PreferenceStatus.Busy: return "另一进程占用附近目标配置；本次选择只在内存生效。";
                    default: return "附近目标设置未能可靠加载或保存；退出后检查 world-targets.json 及恢复材料。";
                }
            }
        }
        internal void TakeFeedback(Action<string> display)
        {
            string message = PreferenceMessage;
            if (Preferences.IsLoaded && message != null && message != reportedPreference) { display(message); reportedPreference = message; }
            string failure = Feature.HasFailed || World.Failure != null ? "附近目标本次已停止：" + (World.Failure ?? "观察不可用") :
                Enabled && !LayersReady ? "附近目标绘制层不可用，选择已保留。" : null;
            if (failure != null && failure != reportedFailure) { display(failure); reportedFailure = failure; }
        }
        private void OnExit(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= OnExit; stopping = true; preferences.Stop(750); }
    }
}
