using System;
using System.Diagnostics;
using System.IO;
using JueMingR.Features.EntityLabels;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;

namespace JueMingR.TerrariaHost.EntityLabels
{
    // Composition only: the settings document is the sole desired-state owner,
    // the Feature owns label decisions, and the Host borrows current game facts.
    internal sealed class HostEntityLabels : IRuntimeFeature
    {
        private readonly PreferenceDocument<EntityLabelSettings> preferences;
        private readonly SingleFeatureRuntime runtime;
        private readonly Stopwatch startup = Stopwatch.StartNew();
        private readonly EntityHostObservation source;
        private long appliedRevision = -1;
        private bool stopping;
        private string reportedPreference, reportedCapability;
        internal readonly EntityLabelFeature Feature;
        internal readonly EntityWorldLayer World;
        internal bool LayersReady { get; set; }
        internal long SessionGeneration { get { return runtime.IsSessionActive ? runtime.Generation : -1; } }
        internal HostEntityLabels(string gameDirectory, SingleFeatureRuntime runtime)
        {
            this.runtime = runtime;
            source = new EntityHostObservation(() => runtime.IsSessionActive);
            Feature = new EntityLabelFeature(source);
            World = new EntityWorldLayer(Feature.Labels, () => runtime.IsSessionActive && LayersReady);
            preferences = new PreferenceDocument<EntityLabelSettings>(new AtomicFileDocument(Path.Combine(gameDirectory,
                "JueMingRData", "config", "features", "entity-labels.json"), 65536, true), new EntityLabelCodec(), EntityLabelSettings.Default);
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        internal PreferenceSnapshot<EntityLabelSettings> Preferences { get { return preferences.Snapshot; } }
        internal bool CanConfigure { get { return !stopping && Preferences.IsLoaded; } }
        internal bool ControlsEnabled { get { return CanConfigure && runtime.IsSessionActive && LayersReady && !Feature.HasFailed; } }
        public bool Enabled { get { return Feature.Enabled; } }
        internal void PollPreferences()
        {
            if (!Preferences.IsLoaded && startup.ElapsedMilliseconds >= 2000) preferences.AbandonSlowLoad();
            var snapshot = Preferences;
            if (snapshot.IsLoaded && snapshot.Revision != appliedRevision)
            { appliedRevision = snapshot.Revision; Feature.Configure(snapshot.Value); if (!snapshot.Value.AnyEnabled) World.Clear(); }
        }
        private bool Change(EntityLabelSettings value) { return !stopping && preferences.Set(value); }
        internal bool SetEnabled(EntityLabelKind kind, bool enabled) { return Change(Preferences.Value.WithEnabled(kind, enabled)); }
        internal bool SetNpcMode(NpcLabelMode mode) { return Change(Preferences.Value.WithNpcMode(mode)); }
        internal bool Toggle(EntityLabelKind kind) { return Change(Preferences.Value.Toggle(kind)); }
        internal bool SetColor(EntityLabelKind kind, int rgb) { return Change(Preferences.Value.WithStyle(kind, Preferences.Value.Style(kind).WithColor(rgb))); }
        internal bool StepSize(EntityLabelKind kind, int direction) { return Change(Preferences.Value.WithStyle(kind, Preferences.Value.Style(kind).StepSize(direction))); }
        internal bool ResetStyle(EntityLabelKind kind) { return Change(Preferences.Value.ResetStyle(kind)); }
        public void OnSessionStarted() { source.EndSession(); World.Clear(); Feature.OnSessionStarted(); }
        public void OnSessionEnded() { Feature.OnSessionEnded(); source.EndSession(); World.Clear(); }
        public void Update(ulong tick) { if (World.Failure != null) Feature.FailClosed(); Feature.Update(tick); }
        public void FailClosed() { Feature.FailClosed(); source.EndSession(); World.Clear(); }
        internal string PreferenceMessage
        {
            get
            {
                var snapshot = Preferences;
                if (snapshot.CommitUnconfirmed) return "保存结果未确认，文件已保护；当前颜色仅能确认在本次内存生效。退出后保留配置及恢复材料核对。";
                switch (snapshot.Status)
                {
                    case PreferenceStatus.Loading: return "正在读取显名设置";
                    case PreferenceStatus.Missing: case PreferenceStatus.Pending: case PreferenceStatus.Saved: return null;
                    case PreferenceStatus.UnsupportedVersion: case PreferenceStatus.UnknownFields:
                        return "显名设置仅本次有效：版本或字段不受支持，原文件已保留。";
                    case PreferenceStatus.Invalid: return "显名配置格式有误，原文件已保留；本次选择只在内存生效。";
                    case PreferenceStatus.Conflict: return "显名配置发生外部变化，已停止覆盖；本次选择只在内存生效。";
                    case PreferenceStatus.Busy: return "另一进程占用显名配置；本次选择只在内存生效。";
                    default: return "显名设置未能可靠加载或保存；退出后检查 entity-labels.json 及恢复材料。";
                }
            }
        }
        internal void TakeFeedback(Action<string> display)
        {
            string message = PreferenceMessage;
            if (Preferences.IsLoaded && message != null && message != reportedPreference) { display(message); reportedPreference = message; }
            string capability = !Enabled && !Feature.HasFailed ? null : !LayersReady ? "显名绘制层不可用，选择已保留。" :
                World.Failure != null || Feature.HasFailed ? "显名本次已停止：" + (World.Failure ?? Feature.UnavailableReason) :
                Feature.UnavailableReason != null ? "显名观察暂不可用，选择已保留。" :
                source.FailedObjects > 0 || Feature.UnresolvedGroups > 0 ? "部分显名对象或生命关系尚未可靠取得，已跳过；其它对象继续显示。" :
                World.FontUnavailable ? "显名字体暂不可用，选择已保留。" : null;
            if (capability != null && capability != reportedCapability) { display(capability); reportedCapability = capability; }
        }
        private void OnExit(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= OnExit; stopping = true; preferences.Stop(750); }
    }
}
