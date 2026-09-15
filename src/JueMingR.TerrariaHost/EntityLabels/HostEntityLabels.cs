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
        private const string LayerUnavailableMessage = "显名暂时无法显示，设置已保留。";
        private Rendering.WorldLayerStatus layerStatus;
        internal Rendering.WorldLayerStatus LayerStatus
        {
            get { return layerStatus; }
            set
            {
                // Feedback may be hidden through recovery. End only the layer
                // failure's deduplication here, not an unrelated feature error.
                if (layerStatus == Rendering.WorldLayerStatus.Unavailable && value == Rendering.WorldLayerStatus.Ready && reportedCapability == LayerUnavailableMessage) reportedCapability = null;
                layerStatus = value;
            }
        }
        internal bool LayersReady { get { return LayerStatus == Rendering.WorldLayerStatus.Ready; } }
        internal long SessionGeneration { get { return runtime.IsSessionActive ? runtime.Generation : -1; } }
        internal HostEntityLabels(string gameDirectory, SingleFeatureRuntime runtime, Npcs.NativeNpcObservation nativeNpcs = null)
        {
            this.runtime = runtime;
            source = new EntityHostObservation(() => runtime.IsSessionActive, nativeNpcs);
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
                if (snapshot.CommitUnconfirmed) return "无法确认显名设置是否保存成功；本次仍可使用，文件已保护。";
                switch (snapshot.Status)
                {
                    case PreferenceStatus.Loading: return "正在加载显名设置";
                    case PreferenceStatus.Missing: case PreferenceStatus.Pending: case PreferenceStatus.Saved: return null;
                    case PreferenceStatus.UnsupportedVersion: case PreferenceStatus.UnknownFields:
                        return "显名设置含当前版本不支持的内容；当前修改仅本次有效，原文件已保留。";
                    case PreferenceStatus.Invalid: return "显名设置格式有误；当前修改仅本次有效，原文件已保留。";
                    case PreferenceStatus.Conflict: return "显名设置文件已有变化，已停止保存；当前修改仅本次有效。";
                    case PreferenceStatus.Busy: return "显名设置正被其他程序使用；当前修改仅本次有效。";
                    default: return "显名设置加载或保存失败；当前修改仅本次有效，原文件已保留。";
                }
            }
        }
        internal void TakeFeedback(Action<string> display)
        {
            string message = PreferenceMessage;
            if (Preferences.IsLoaded && message != null && message != reportedPreference) { display(message); reportedPreference = message; }
            string capability = !Enabled && !Feature.HasFailed ? null : LayerStatus == Rendering.WorldLayerStatus.Unavailable ? LayerUnavailableMessage :
                World.Failure != null || Feature.HasFailed ? "显名已停止，设置已保留。" :
                Feature.UnavailableReason != null ? "暂时无法读取显名信息，设置已保留。" :
                source.FailedObjects > 0 || Feature.UnresolvedGroups > 0 ? "部分对象暂时无法显名，其余正常显示。" :
                World.FontUnavailable ? "显名字体暂不可用，设置已保留。" : null;
            // Keep the diagnostic cause in deduplication, not in player copy.
            // Different failures still notify once each; layer recovery retains its key.
            string key = capability == "显名已停止，设置已保留。" ? "stopped:" + (World.Failure ?? Feature.UnavailableReason) : capability;
            if (capability != null && key != reportedCapability) { display(capability); reportedCapability = key; }
            if (capability == null) reportedCapability = null;
        }
        private void OnExit(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= OnExit; stopping = true; preferences.Stop(750); }
    }
}
