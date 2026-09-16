using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using JueMingR.Features.Exploration;
using JueMingR.Features.MapMarkers;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using Terraria;
using Terraria.Map;

namespace JueMingR.TerrariaHost.Map
{
    internal sealed class HostMapFeatures : IRuntimeFeature, IMapControls
    {
        private readonly SingleFeatureRuntime runtime;
        private readonly Items.ItemSessionProbe probe;
        private readonly PreferenceDocument<bool> markerPreference, dynamicPreference;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private Func<long> milliseconds;
        private readonly ExplorationMapHooks observation = new ExplorationMapHooks();
        private readonly ExplorationHistory history;
        internal readonly MapMarkerLayer Layer;
        private object token;
        private WorldMap map;
        private string pair, feedback;
        private int width, height;
        private bool failed, stopping, paused;
        private long nextPublish, offeredRevision = -1, offeredCalibration = -1;
        internal ExplorationCounter Counter { get; private set; }
        internal long MapGeneration { get; private set; }
        internal string Pair { get { return pair; } }
        internal int Width { get { return width; } }
        internal int Height { get { return height; } }
        internal bool OwnershipCurrent { get { return Session >= 0 && ReferenceEquals(token, probe.SessionIdentity); } }
        internal HostMapFeatures(string directory, SingleFeatureRuntime runtime, Items.ItemSessionProbe probe, HostInputState input)
        {
            this.runtime = runtime; this.probe = probe;
            milliseconds = () => clock.ElapsedMilliseconds;
            string data = Path.Combine(directory, "JueMingRData");
            Markers = new MarkerLibrary(key => new AtomicFileDocument(Path.Combine(data, "map-markers", key + ".json"), MarkerCodec.MaximumBytes, true));
            Workspace = new MarkerWorkspace(Markers);
            history = new ExplorationHistory(key => new AtomicFileDocument(Path.Combine(data, "records", "exploration", key + ".json"), 65536, true), () => milliseconds());
            markerPreference = new PreferenceDocument<bool>(new AtomicFileDocument(Path.Combine(data, "config", "features", "map-markers.json"), 65536, true), new MarkerPreferenceCodec(), false);
            dynamicPreference = new PreferenceDocument<bool>(new AtomicFileDocument(Path.Combine(data, "config", "features", "exploration-dynamic.json"), 65536, true), new ExplorationPreferenceCodec(), false);
            observation.Install(); Layer = new MapMarkerLayer(this, input); AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        public MarkerLibrary Markers { get; }
        public MarkerWorkspace Workspace { get; }
        public long Session { get { return runtime.IsSessionActive && !failed && !stopping ? runtime.Generation : -1; } }
        public bool ControlsEnabled { get { return Session >= 0 && markerPreference.Snapshot.IsLoaded && dynamicPreference.Snapshot.IsLoaded; } }
        public bool MarkersEnabled { get { return markerPreference.Snapshot.Value; } }
        public bool DynamicEnabled { get { return dynamicPreference.Snapshot.Value; } }
        public bool FastScan { get; set; }
        public bool ScanPaused { get { return paused; } }
        public string ExplorationText { get; private set; } = "统计中…";
        public string ScanText { get; private set; } = "等待地图就绪";
        public string StatusMessage
        {
            get
            {
                if (Markers.CommitUnconfirmed || history.CommitUnconfirmed) return "保存结果未确认，已停止相关文件写入。";
                if (Markers.Error != null || history.Error != null) return "读取或保存失败；现有文件已保护。";
                if (!observation.Ready) return "动态观察不可用，请保留现场并反馈。";
                if (pair == null) return "角色或世界身份未识别，暂不保存。";
                if (markerPreference.Snapshot.IsProtected || dynamicPreference.Snapshot.IsProtected) return "设置文件已保护；本次设置可能无法保存。";
                return Workspace.Error;
            }
        }
        public bool Enabled { get { return true; } }
        public bool SetMarkers(bool value) { return ControlsEnabled && markerPreference.Set(value); }
        public bool SetDynamic(bool value)
        {
            if (!ControlsEnabled || value && !observation.Ready) return false;
            bool changed = dynamicPreference.Set(value); if (changed && Counter != null) Counter.SetDynamic(value); return changed;
        }
        public void PauseScan(bool value) { paused = value; if (Counter != null) Counter.Paused = value; nextPublish = 0; }
        public void Recount() { if (ControlsEnabled && Counter != null) { Counter.Restart(); nextPublish = 0; } }
        public void OnSessionStarted()
        {
            token = probe.SessionIdentity; pair = null; map = null; FastScan = paused = false; width = Main.maxTilesX; height = Main.maxTilesY;
            Markers.BeginSession(runtime.Generation, width, height); history.BeginSession(width, height); offeredRevision = -1; nextPublish = 0; MapGeneration++;
        }
        public void OnSessionEnded()
        { observation.Bind(null, null); Counter = null; map = null; token = null; pair = null; Markers.EndSession(); history.EndSession(); Workspace.Poll(); Layer.Invalidate(); MapGeneration++; }
        public void FailClosed() { failed = true; OnSessionEnded(); feedback = "地图功能暂不可用；已有文件保留。"; }
        internal void PollPreferences()
        { if (clock.ElapsedMilliseconds > 2000) { if (!markerPreference.Snapshot.IsLoaded) markerPreference.AbandonSlowLoad(); if (!dynamicPreference.Snapshot.IsLoaded) dynamicPreference.AbandonSlowLoad(); } }
        public void Update(ulong tick)
        {
            if (Session < 0 || !ReferenceEquals(token, probe.SessionIdentity)) return;
            if (pair == null) pair = LocalWorldIdentity.Observe();
            if (pair != null) { Markers.UsePair(pair); history.UsePair(pair); }
            Markers.Poll(); Workspace.Poll(); history.Poll(); Layer.Update();
            var current = Main.Map;
            if (current == null || width <= 0 || height <= 0 || Main.maxTilesX != width || Main.maxTilesY != height || width > current.MaxWidth || height > current.MaxHeight)
            { observation.Bind(null, null); Counter = null; map = null; ExplorationText = "地图范围暂不可用"; return; }
            if (ExplorationMapHooks.Loading) { ScanText = "等待地图载入"; return; }
            if (!ReferenceEquals(current, map))
            {
                // Wait for a known pair's initial document result before deciding
                // whether off+historical needs an automatic first scan.
                if (pair != null && !history.Loaded) return;
                map = current; MapGeneration++;
                var captured = map;
                Counter = new ExplorationCounter(width, height, (x, y) => captured.IsRevealed(x, y), DynamicEnabled || history.Historical == null) { Paused = paused };
                Counter.SetDynamic(DynamicEnabled && observation.Ready); observation.Bind(map, Counter); Layer.Invalidate(); offeredRevision = offeredCalibration = -1;
                if (ExplorationMapHooks.Loading) return;
            }
            if (Counter.Dynamic != (DynamicEnabled && observation.Ready)) Counter.SetDynamic(DynamicEnabled && observation.Ready);
            if (!Counter.Scanning || FastScan || tick % 4 == 0) Counter.Advance(FastScan && Counter.Scanning ? 32768 : 4096, 64);
            if (Counter.HasResult && Counter.Complete && Counter.Revision != offeredRevision)
            { history.Offer(Counter.Count, DateTime.UtcNow.Ticks, Counter.CalibrationRevision != offeredCalibration); offeredRevision = Counter.Revision; offeredCalibration = Counter.CalibrationRevision; }
            if (milliseconds() >= nextPublish) { Publish(); nextPublish = milliseconds() + 333; }
        }
        private void Publish()
        {
            var value = Counter; if (value == null) return;
            long? count = value.HasResult ? (long?)value.Count : history.Historical?.Count;
            string status = value.Current ? "当前揭示" : value.Dynamic && value.Complete && value.Pending ? "更新中" : "上次统计";
            ExplorationText = count.HasValue ? status + " " + ((double)count.Value * 100 / value.Total).ToString("0.00", CultureInfo.InvariantCulture) + "%" : "统计中…";
            ScanText = value.Scanning ? (paused ? "已暂停 " : "完整扫描 ") + (100d * value.CompletedBlocks / value.BlockCount).ToString("0.0", CultureInfo.InvariantCulture) + "%" : value.Pending ? "正在更新变化区域" : "完整扫描已结束";
        }
        public bool Locate(string id)
        {
            var record = Workspace.Find(id); if (!ControlsEnabled || record == null || !Layer.CanLocate) return false;
            return Layer.Locate(record);
        }
        internal void Feedback(string value) { feedback = value; }
        public void TakeFeedback(Action<string> display) { if (feedback == null) return; string value = feedback; feedback = null; display(value); }
        private void OnExit(object sender, EventArgs args)
        {
            if (stopping) return; stopping = true; AppDomain.CurrentDomain.ProcessExit -= OnExit;
            Layer.Dispose(); observation.Dispose(); var elapsed = Stopwatch.StartNew();
            Markers.Stop(750); history.Stop(Math.Max(0, 750 - (int)elapsed.ElapsedMilliseconds));
            markerPreference.Stop(Math.Max(0, 750 - (int)elapsed.ElapsedMilliseconds)); dynamicPreference.Stop(Math.Max(0, 750 - (int)elapsed.ElapsedMilliseconds));
        }
    }
}
