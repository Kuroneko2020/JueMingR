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
        private int shownPercent = -1, shownStatus = -1, scanProgress, scanState, formattedProgress = -1, formattedState = -1;
        private long assetGeneration;
        private string scanText = "等待地图就绪";
#if DEBUG
        internal long PublishChecks, FormattedValues, FormattedDetails, SummaryOffers;
#endif
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
        public bool ScanActive { get { return Counter != null && Counter.Scanning; } }
        public string ExplorationText { get; private set; } = "统计中…";
        public string ScanText
        {
            get
            {
                if (scanState != formattedState || scanProgress != formattedProgress)
                {
                    formattedState = scanState; formattedProgress = scanProgress;
                    scanText = scanState == 0 ? "等待地图就绪" : scanState == 1 ? "等待地图载入" : scanState == 2 || scanState == 3 ? "已扫描：" + (scanProgress / 100d).ToString("0.00", CultureInfo.InvariantCulture) + "%" : scanState == 4 ? "更新中…" : scanState == 5 ? "上次结果" : "";
#if DEBUG
                    FormattedDetails++;
#endif
                }
                return scanText;
            }
        }
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
            bool changed = dynamicPreference.Set(value); if (changed && Counter != null) Counter.SetDynamic(value); if (changed) nextPublish = 0; return changed;
        }
        public void PauseScan(bool value) { paused = value; if (Counter != null) Counter.Paused = value; nextPublish = 0; }
        public void Recount() { if (ControlsEnabled && Counter != null) { Counter.Restart(); nextPublish = 0; } }
        public void OnSessionStarted()
        {
            token = probe.SessionIdentity; pair = null; map = null; FastScan = paused = false; width = Main.maxTilesX; height = Main.maxTilesY;
            Markers.BeginSession(++assetGeneration, width, height); history.BeginSession(width, height); offeredRevision = -1; nextPublish = 0; MapGeneration++; shownPercent = shownStatus = -1; scanState = 0; ExplorationText = "统计中…";
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
            if (current == null || Main.maxTilesX <= 0 || Main.maxTilesY <= 0 || Main.maxTilesX > current.MaxWidth || Main.maxTilesY > current.MaxHeight)
            { observation.Bind(null, null); Counter = null; map = null; ExplorationText = "地图范围暂不可用"; return; }
            if (Main.maxTilesX != width || Main.maxTilesY != height)
            {
                // The current world object can replace its logical extent.
                // Rebind documents with the new dimensions; incompatible old
                // assets stay protected, while this map can compute in memory.
                observation.Bind(null, null); Counter = null; map = null; width = Main.maxTilesX; height = Main.maxTilesY;
                Markers.BeginSession(++assetGeneration, width, height); history.BeginSession(width, height); Workspace.Poll(); Layer.Invalidate(); MapGeneration++;
                if (pair != null) { Markers.UsePair(pair); history.UsePair(pair); } nextPublish = 0;
            }
            if (ExplorationMapHooks.Loading) { scanState = 1; return; }
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
            bool advanceScan = FastScan || tick % 4 == 0;
            Counter.Advance(FastScan && Counter.Scanning && !Counter.Paused ? 32768 : 4096, 64, advanceScan);
            if (Counter.HasResult && Counter.Complete && Counter.Revision != offeredRevision)
            {
                bool calibrated = Counter.CalibrationRevision != offeredCalibration;
                history.Offer(Counter.Count, DateTime.UtcNow.Ticks, calibrated); offeredRevision = Counter.Revision; offeredCalibration = Counter.CalibrationRevision; if (calibrated) nextPublish = 0;
#if DEBUG
                SummaryOffers++;
#endif
            }
            if (milliseconds() >= nextPublish) { Publish(); nextPublish = milliseconds() + 333; }
        }
        private void Publish()
        {
            var value = Counter; if (value == null) return;
#if DEBUG
            PublishChecks++;
#endif
            long? count = value.HasResult ? (long?)value.Count : history.Historical?.Count;
            int status = !count.HasValue ? 3 : value.Current ? 0 : value.Dynamic && value.Complete && value.Pending ? 1 : 2;
            int percent = count.HasValue ? (int)Math.Round(count.Value * 10000d / value.Total) : -1;
            if (status != shownStatus || percent != shownPercent)
            {
                shownStatus = status; shownPercent = percent;
                ExplorationText = status == 3 ? "统计中…" : "已揭示 " + (percent / 100d).ToString("0.00", CultureInfo.InvariantCulture) + "%";
#if DEBUG
                FormattedValues++;
#endif
            }
            scanState = value.Scanning ? (paused ? 3 : 2) : value.Pending ? 4 : value.Current ? 6 : 5;
            scanProgress = (int)Math.Round(10000d * value.CompletedBlocks / value.BlockCount);
        }
        public bool Locate(string id)
        {
            var record = Workspace.Find(id); if (!ControlsEnabled || record == null || !Layer.CanLocate) return false;
            return Layer.Locate(record);
        }
        internal void Feedback(string value) { feedback = value; }
        public void TakeFeedback(Action<string> display)
        {
            if (feedback != null) { string value = feedback; feedback = null; display(value); return; }
            // A retired world's failure must reach the player once without
            // becoming the current world's file status or being erased by it.
            if (Markers.TakeBackgroundError() != null) { display("先前世界的标记保存未完成；原文件已保护，请保留现场并反馈。"); return; }
            if (history.TakeBackgroundError() != null) display("先前世界的统计保存未完成；原文件已保护，请保留现场并反馈。");
        }
        private void OnExit(object sender, EventArgs args)
        {
            if (stopping) return; stopping = true; AppDomain.CurrentDomain.ProcessExit -= OnExit;
            Layer.Dispose(); observation.Dispose(); var elapsed = Stopwatch.StartNew();
            Markers.Stop(750); history.Stop(Math.Max(0, 750 - (int)elapsed.ElapsedMilliseconds));
            markerPreference.Stop(Math.Max(0, 750 - (int)elapsed.ElapsedMilliseconds)); dynamicPreference.Stop(Math.Max(0, 750 - (int)elapsed.ElapsedMilliseconds));
        }
    }
}
