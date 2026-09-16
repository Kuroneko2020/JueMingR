using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using JueMingR.Features.Footprints;
using JueMingR.Infrastructure.Footprints;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Footprints;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Map;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Footprints
{
    internal sealed class HostFootprints : IRuntimeFeature, IFootprintControls
    {
        private readonly SingleFeatureRuntime runtime;
        private readonly Items.ItemSessionProbe probe;
        private readonly string directory;
        private readonly PreferenceDocument<FootprintPreferences> preferences;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly FootprintSourceHooks hooks;
        private readonly List<Retired> retired = new List<Retired>();
        private object token, stepToken;
        private string pair, recorderGeneration;
        private int width, height;
        private bool failed, stopping, discontinuity = true, previousValid, step;
        private bool? recordingOverride;
        private bool archiveRequested;
        private string reportedFailure;
        private Vector2 previousPosition;
        private FootprintStore store;
        internal FootprintRecorder Recorder { get; private set; }
        internal readonly FootprintMapLayer Layer;
        internal HostFootprints(string gameDirectory, SingleFeatureRuntime runtime, Items.ItemSessionProbe probe, HostInputState input)
        {
            this.runtime = runtime; this.probe = probe; directory = Path.Combine(gameDirectory, "JueMingRData", "footprints");
            preferences = new PreferenceDocument<FootprintPreferences>(new AtomicFileDocument(Path.Combine(gameDirectory, "JueMingRData", "config", "features", "footprints.json"), 65536, true), new FootprintPreferenceCodec(), new FootprintPreferences(true, false));
            hooks = new FootprintSourceHooks(this); hooks.Install(); Layer = new FootprintMapLayer(this, input); AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        public bool Enabled { get { return true; } }
        public long Session { get { return !failed && !stopping && runtime.IsSessionActive && ReferenceEquals(token, probe.SessionIdentity) ? runtime.Generation : -1; } }
        public bool ControlsEnabled { get { return Session >= 0 && preferences.Snapshot.IsLoaded; } }
        public bool Display { get { return preferences.Snapshot.Value.Display; } }
        public bool Recording { get { return recordingOverride ?? (!preferences.Snapshot.IsProtected && preferences.Snapshot.Value.Recording); } }
        public string Generation { get { return store?.Snapshot.Generation; } }
        public bool Clearing { get { return RelevantStore?.Snapshot.Clearing == true || clearAccepted; } }
        private bool clearAccepted;
        public bool CanClear { get { var state = store?.Snapshot; return Session >= 0 && state != null && state.Loaded && !state.Stopped && !state.Protected && !Clearing; } }
        public bool CanRetryClear { get { var value = RelevantStore?.Snapshot; return value != null && value.Clearing && value.Error != null && !value.Protected && !value.Stopped; } }
        private FootprintStore RelevantStore { get { if (store != null) return store; foreach (var old in retired) if (old.Pair == pair) return old.Store; return null; } }
        public bool CanRetrySave { get { var value = RelevantStore?.Snapshot; return value != null && value.RetryableSave && !value.Clearing && !value.Protected && !value.Stopped; } }
        public void RetrySave() { if (CanRetrySave) RelevantStore.RetrySave(); }
        private bool PreferenceFailure { get { var p = preferences.Snapshot; return p.IsLoaded && p.Status != PreferenceStatus.Missing && p.Status != PreferenceStatus.Saved && p.Status != PreferenceStatus.Pending; } }
        internal bool HasFailure { get { return !hooks.Ready || RelevantStore?.Snapshot.Error != null || Recorder != null && Recorder.Blocked || PreferenceFailure || retired.Count >= 8; } }
        public bool HasIssue { get { return HasFailure; } }
        public void TakeFeedback(Action<string> show)
        {
            if (Session < 0 || show == null) return;
            string error = HasFailure ? StatusMessage : null;
            if (error != null && error != reportedFailure) show("足迹：" + error);
            reportedFailure = error;
        }
        internal long End { get { return Recorder?.End ?? store?.Snapshot.End ?? 0; } }
        internal FootprintQuery Query { get { return store?.QueryResult; } }
        internal long CommittedCount { get { return store?.Snapshot.Count ?? 0; } }
        internal double UiSeconds { get { return clock.ElapsedTicks / (double)Stopwatch.Frequency; } }
        public string StatusMessage
        {
            get
            {
                if (!hooks.Ready) return "录制观察暂不可用。";
                if (PreferenceFailure) return Recording ? "设置未保存，本次选择可能丢失。" : "设置读取失败，录制暂未开启。";
                if (pair == null) return "等待角色与世界身份。";
                var value = RelevantStore?.Snapshot;
                if (value == null && retired.Count >= 8) return "旧记录尚未保存，已暂停新录制。";
                if (value == null) return !Recording && !Display && !archiveRequested ? "录制与显示均已关闭。" : "正在读取足迹…";
                if (!value.Loaded && value.Error == null) return "正在读取足迹…";
                if (value.Error != null) return value.Clearing ? "清除未完成，请保留现场并重试。" : "读取或保存失败，已有文件保留。";
                if (Clearing) return "正在清除当前角色与世界的足迹…";
                if (Recorder != null && Recorder.Blocked) return "保存繁忙，已暂停接纳新足迹。";
                if (preferences.Snapshot.IsProtected) return Recording ? "设置已保护，本次开启不能保存。" : "设置读取失败，录制暂未开启。";
                return Recording ? "正在录制；关闭显示仍会记录。" : "录制已关闭；已有足迹仍可回放。";
            }
        }
        public bool SetDisplay(bool value) { bool changed = ControlsEnabled && preferences.Set(new FootprintPreferences(preferences.Snapshot.Value.Recording, value)); if (changed) Layer.Invalidate(); return changed; }
        public bool SetRecording(bool value)
        {
            if (!ControlsEnabled || Clearing || value == Recording) return false;
            // An unreadable preference never implicitly opts the player in.
            // An explicit current-session choice is still usable without changing
            // protected bytes, including a choice equal to the codec default.
            recordingOverride = value; preferences.Set(new FootprintPreferences(value, Display));
            Recorder?.Flush(); BreakContinuity(); return true;
        }
        public bool Clear(string generation)
        {
            if (!CanClear || !store.Clear(generation, Guid.NewGuid().ToString("N"))) return false;
            clearAccepted = true; recorderGeneration = generation; Recorder = null; previousValid = false; Layer.Invalidate(); return true;
        }
        public void PrepareConfiguration() { archiveRequested = true; }
        public void RetryClear() { if (CanRetryClear) RelevantStore.RetryClear(); }
        public void OnSessionStarted() { token = probe.SessionIdentity; width = Main.maxTilesX; height = Main.maxTilesY; pair = null; previousValid = false; discontinuity = true; }
        public void OnSessionEnded()
        {
            if (store != null) { retired.Add(new Retired(pair, store, Recorder)); store = null; Recorder = null; }
            token = stepToken = null; pair = recorderGeneration = null; clearAccepted = archiveRequested = false; step = previousValid = false; Layer.Invalidate(); PumpRetired();
        }
        public void FailClosed() { failed = true; OnSessionEnded(); }
        internal void PollPreferences()
        {
            if (clock.ElapsedMilliseconds > 2000 && !preferences.Snapshot.IsLoaded) preferences.AbandonSlowLoad();
            PumpRetired(); Recorder?.FlushDue();
        }
        public void Update(ulong tick)
        {
            if (Session < 0) return;
            if (width != Main.maxTilesX || height != Main.maxTilesY) { OnSessionEnded(); OnSessionStarted(); }
            if (pair == null) pair = LocalWorldIdentity.Observe();
            if (store == null && pair != null)
            {
                foreach (var old in retired) if (old.Pair == pair)
                {
                    // A real re-entry grants one finite retry round to the same
                    // worker/lease, never a fresh archive that loses its FIFO.
                    if (!ReferenceEquals(old.RetrySession, token) && old.Store.RetrySave()) old.RetrySession = token;
                    return;
                }
                if (retired.Count >= 8) return;
                if (!preferences.Snapshot.IsLoaded || !Recording && !Display && !archiveRequested) return;
                string captured = pair; store = new FootprintStore(() => new FileFootprintArchive(directory, captured), captured, width, height);
            }
            var value = store?.Snapshot;
            if (value != null && value.Loaded && value.Error == null && !value.Clearing && (!clearAccepted || value.Generation != recorderGeneration) && (Recorder == null || value.Generation != recorderGeneration))
            {
                recorderGeneration = value.Generation; clearAccepted = false;
                Recorder = new FootprintRecorder(value.Count, value.End, value.Segment, store.Offer, () => clock.ElapsedMilliseconds); BreakContinuity();
            }
            Layer.Update();
        }
        internal bool RequestQuery(long cursor) { if (store == null) return false; store.RequestQuery(cursor); return true; }
        internal void CancelQuery() { store?.CancelQuery(); }
        internal void BreakContinuity() { discontinuity = true; Recorder?.Break(); }
        internal bool BeforeSimulation()
        {
            step = false;
            if (Session < 0 || !preferences.Snapshot.IsLoaded || !Recording || Clearing || Recorder == null || store.Snapshot.Error != null) { BreakContinuity(); return false; }
            var player = Main.LocalPlayer; if (player == null || !player.active) { BreakContinuity(); return false; }
            var position = player.Center;
            if (previousValid && position != previousPosition) BreakContinuity();
            stepToken = token; step = true; return true;
        }
        internal void AfterSimulation(bool completed)
        {
            if (!step) return; step = false;
            if (!completed || Session < 0 || !ReferenceEquals(stepToken, token) || !Recording || Clearing || Recorder == null) { previousValid = false; BreakContinuity(); return; }
            var player = Main.LocalPlayer; if (player == null) { previousValid = false; BreakContinuity(); return; }
            Vector2 position = player.Center; float x = position.X / 16f, y = position.Y / 16f;
            bool valid = FootprintSample.Finite(x) && FootprintSample.Finite(y) && x >= 0 && y >= 0 && x < width && y < height;
            var kind = player.dead ? FootprintPosition.Dead : player.ghost ? FootprintPosition.Ghost : !valid ? FootprintPosition.Unavailable : FootprintPosition.Valid;
            // One completed native world step is one unit. No Draw, UTC, dayRate,
            // outer Update count, or skipped paused interval can enter this clock.
            bool accepted = Recorder.Observe(valid ? x : 0, valid ? y : 0, kind, discontinuity);
            previousPosition = position; previousValid = valid && accepted; discontinuity = !accepted;
        }
        private void PumpRetired()
        {
            for (int i = retired.Count - 1; i >= 0; i--)
            {
                var old = retired[i]; var state = old.Store.Snapshot;
                bool acceptedTail = old.Recorder == null || !old.Recorder.Dirty || old.Recorder.Flush();
                if (acceptedTail && !old.Store.HasUnsavedFacts && !state.Clearing) old.Store.Dispose();
                if (old.Store.Finished && acceptedTail && !old.Store.HasUnsavedFacts && !state.Clearing) retired.RemoveAt(i);
            }
        }
        private void OnExit(object sender, EventArgs args)
        {
            stopping = true; OnSessionEnded(); var deadline = Stopwatch.StartNew();
            while (retired.Count > 0 && deadline.ElapsedMilliseconds < 1000) { PumpRetired(); Thread.Sleep(5); }
            preferences.Stop(Math.Max(0, 1500 - (int)deadline.ElapsedMilliseconds)); hooks.Dispose(); Layer.Dispose(); AppDomain.CurrentDomain.ProcessExit -= OnExit;
        }
        private sealed class Retired
        {
            internal readonly string Pair; internal readonly FootprintStore Store; internal readonly FootprintRecorder Recorder;
            internal object RetrySession;
            internal Retired(string pair, FootprintStore store, FootprintRecorder recorder) { Pair = pair; Store = store; Recorder = recorder; }
        }
    }
}
