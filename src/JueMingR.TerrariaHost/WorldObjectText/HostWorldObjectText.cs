using System;
using System.Diagnostics;
using System.IO;
using JueMingR.Features.WorldObjectText;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;
using JueMingR.TerrariaHost.World;

namespace JueMingR.TerrariaHost.WorldObjectText
{
    internal sealed class HostWorldObjectText : IRuntimeFeature, F5.IWorldObjectControls
    {
        private readonly SingleFeatureRuntime runtime;
        private readonly PreferenceDocument<WorldObjectSettings> preferences;
        private readonly WorldObjectHostObservation source;
        private readonly OpenedContainerObserver observer;
        private readonly Stopwatch startup = Stopwatch.StartNew();
        private bool stopping, failed;
        private string feedback, observerFailure;
        internal readonly OpenedPositionHistory History;
        internal readonly WorldObjectDiscovery Discovery;
        internal readonly WorldObjectTextWorldLayer World;
        internal HostWorldObjectText(string directory, SingleFeatureRuntime runtime, WorldTileObservation world, Func<bool> automatic)
        {
            this.runtime = runtime; source = new WorldObjectHostObservation(world); Discovery = new WorldObjectDiscovery(source);
            string data = Path.Combine(directory, "JueMingRData");
            preferences = new PreferenceDocument<WorldObjectSettings>(new AtomicFileDocument(Path.Combine(data, "config", "features", "world-object-text.json"), 65536, true), new WorldObjectCodec(), WorldObjectSettings.Default);
            History = new OpenedPositionHistory(pair => new AtomicFileDocument(Path.Combine(data, "records", "opened-containers", pair + ".json"), OpenedPositionCodec.MaximumBytes, true));
            observer = new OpenedContainerObserver(runtime, History, world, automatic);
            World = new WorldObjectTextWorldLayer(Discovery, () => runtime.IsSessionActive && LayersReady);
            Discovery.SetPresentationGate(World.MayPresent, World.IsPrepared);
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        internal bool LayersReady { get; set; }
        internal long SessionGeneration { get { return runtime.IsSessionActive ? runtime.Generation : -1; } }
        internal PreferenceSnapshot<WorldObjectSettings> Preferences { get { return preferences.Snapshot; } }
        internal bool CanConfigure { get { return !stopping && Preferences.IsLoaded; } }
        WorldObjectSettings F5.IWorldObjectControls.Settings { get { return Preferences.Value; } }
        bool F5.IWorldObjectControls.CanConfigure { get { return CanConfigure; } }
        bool F5.IWorldObjectControls.ControlsEnabled { get { return ControlsEnabled; } }
        string F5.IWorldObjectControls.PreferenceMessage { get { return PreferenceMessage; } }
        bool F5.IWorldObjectControls.SetMode(WorldObjectKind kind, WorldObjectMode mode) { return SetMode(kind, mode); }
        bool F5.IWorldObjectControls.SetColor(WorldObjectKind kind, int rgb) { return SetColor(kind, rgb); }
        bool F5.IWorldObjectControls.ResetColor(WorldObjectKind kind) { return ResetColor(kind); }
        bool F5.IWorldObjectControls.SetSize(WorldObjectKind kind, int size) { return SetSize(kind, size); }
        bool F5.IWorldObjectControls.SetLimits(WorldObjectKind kind, int lines, int characters) { return SetLimits(kind, lines, characters); }
        internal bool ControlsEnabled { get { return !stopping && !failed && Preferences.IsLoaded && runtime.IsSessionActive && LayersReady; } }
        public bool Enabled { get { return Preferences.Value.AnyEnabled; } }
        internal void PollPreferences() { if (!Preferences.IsLoaded && startup.ElapsedMilliseconds >= 2000) preferences.AbandonSlowLoad(); }
        private bool Change(WorldObjectSettings value) { return !stopping && preferences.Set(value); }
        internal bool SetMode(WorldObjectKind kind, WorldObjectMode mode) { return Change(Preferences.Value.WithMode(kind, mode)); }
        internal bool Toggle(WorldObjectKind kind) { return Change(Preferences.Value.Toggle(kind)); }
        internal bool SetColor(WorldObjectKind kind, int rgb) { return Change(Preferences.Value.With(Preferences.Value.Style(kind).WithColor(rgb))); }
        internal bool ResetColor(WorldObjectKind kind) { return SetColor(kind, WorldObjectSettings.Default.Style(kind).Rgb); }
        internal bool SetSize(WorldObjectKind kind, int size) { return Change(Preferences.Value.With(Preferences.Value.Style(kind).WithSize(size))); }
        internal bool SetLimits(WorldObjectKind kind, int lines, int characters) { return Change(Preferences.Value.With(Preferences.Value.Style(kind).WithLimits(lines, characters))); }
        public void OnSessionStarted() { source.EndSession(); Discovery.Clear(); World.Clear(); observer.Start(); feedback = null; }
        public void OnSessionEnded() { observer.End(); source.EndSession(); Discovery.Clear(); World.Clear(); }
        public void Update(ulong tick)
        {
            if (stopping) return;
            // Record observation is independent of preferences and all display
            // modes. A text/layout failure must not silently disable that owner.
            if (observerFailure == null) try { observer.Update(Preferences.IsLoaded && Preferences.Value.Style(WorldObjectKind.Chest).Mode == WorldObjectMode.Opened); } catch (Exception e) { observerFailure = "opened-observer-" + e.GetType().Name; }
            if (failed || World.Failure != null) { Discovery.Clear(); return; }
            Discovery.Update(Preferences.IsLoaded ? Preferences.Value : WorldObjectSettings.Default, History);
            World.Prepare(Preferences.Value);
        }
        public void FailClosed() { failed = true; Discovery.Clear(); World.Clear(); }
        internal string PreferenceMessage
        {
            get
            {
                var snapshot = Preferences;
                if (snapshot.CommitUnconfirmed) return "世界文字设置保存结果未确认，原文件与恢复材料已保护；当前选择仅能确认在本次生效。";
                switch (snapshot.Status)
                {
                    case PreferenceStatus.Loading: return "正在读取世界文字设置";
                    case PreferenceStatus.Missing: case PreferenceStatus.Saved: case PreferenceStatus.Pending: return null;
                    default: return "世界文字设置未能可靠加载或保存；本次选择仅在内存生效，原文件已保留。";
                }
            }
        }
        internal void TakeFeedback(Action<string> display)
        {
            string background = History.TakeBackgroundFailure();
            if (background != null) display("开过记录有未能保存的内容，原文件与恢复材料已保留：" + background);
            string message = observerFailure != null ? "本次成功开箱观察已停止：" + observerFailure :
                World.Failure != null ? "世界文字显示本次已停止：" + World.Failure :
                History.CommitUnconfirmed ? "开过记录保存结果未确认，原文件与恢复材料已保护。" :
                History.Error != null ? "开过记录本次仅能确认在内存生效：" + History.Error :
                History.HasAny && !History.HasPair ? "未取得可靠的角色与世界身份，开过记录仅本次有效。" : PreferenceMessage;
            if (message != null && message != feedback) { display(message); feedback = message; }
        }
        private void OnExit(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= OnExit; stopping = true; preferences.Stop(750); History.Stop(1500); }
    }
}
