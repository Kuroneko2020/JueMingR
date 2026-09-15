using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JueMingR.Features.DeathHistory;
using JueMingR.Features.WorldTime;
using JueMingR.Infrastructure.DeathHistory;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.DeathHistory;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using Terraria;
using DeathLedger = JueMingR.Features.DeathHistory.DeathHistory;

namespace JueMingR.TerrariaHost.DeathHistory
{
    // Composition and native identity only. Death facts, cumulative time and
    // display preferences keep their respective single domain owners.
    internal sealed class HostDeathRecords : IRuntimeFeature, F5.IDeathControls
    {
        private readonly SingleFeatureRuntime runtime;
        private readonly Items.ItemSessionProbe probe;
        private readonly PreferenceDocument<DeathDisplayPreferences> preferences;
        private readonly DeathSourceHooks deathHooks;
        private readonly WorldTime.WorldTimeSourceHooks timeHooks;
        internal readonly DeathMapHooks Map;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private object activeToken, pendingToken;
        private readonly List<Tuple<DeathFact, DateTime>> pendingDeaths = new List<Tuple<DeathFact, DateTime>>();
        private double pendingTime;
        private bool stopping, failed;
        private object missedToken;
        private readonly HashSet<string> missedPairs = new HashSet<string>(StringComparer.Ordinal);
        private bool MissedHere { get { return pair != null && missedPairs.Contains(pair) || missedToken != null && ReferenceEquals(missedToken, CurrentToken()); } }
        private string pair, localError, reportedError;
        private long pageOffset = -1;
        private string selected;
        private long shownCount = -1, shownDays = -1;
        private string countText, daysText;
        internal readonly DeathLedger History;
        internal readonly WorldTimeHistory Time;
        internal HostDeathRecords(string directory, SingleFeatureRuntime runtime, Items.ItemSessionProbe probe)
        {
            this.runtime = runtime; this.probe = probe;
            string data = Path.Combine(directory, "JueMingRData");
            History = new DeathLedger(key => new FileDeathArchive(Path.GetFullPath(Path.Combine(data, "records", "deaths", key))));
            Time = new WorldTimeHistory(key => new AtomicFileDocument(Path.Combine(data, "records", "playtime", key + ".json"), 65536, true), () => clock.ElapsedMilliseconds);
            preferences = new PreferenceDocument<DeathDisplayPreferences>(new AtomicFileDocument(Path.Combine(data, "config", "features", "death-markers.json"), 65536, true), new DeathDisplayCodec(), DeathDisplayPreferences.Default);
            deathHooks = new DeathSourceHooks(DeathToken, ObservedDeath); timeHooks = new WorldTime.WorldTimeSourceHooks(ObservedTime);
            deathHooks.Install(); timeHooks.Install(); AppDomain.CurrentDomain.ProcessExit += OnExit;
            Map = new DeathMapHooks(this); Map.Install();
        }
        public long Session { get { return runtime.IsSessionActive ? runtime.Generation : -1; } }
        public DeathHistorySnapshot Snapshot { get { return History.Snapshot; } }
        internal PreferenceSnapshot<DeathDisplayPreferences> Preferences { get { return preferences.Snapshot; } }
        public DeathDisplayPreferences Settings { get { return Preferences.Value; } }
        public bool ControlsEnabled { get { return !stopping && !failed && runtime.IsSessionActive && Preferences.IsLoaded; } }
        public bool SetEnabled(bool value) { return ControlsEnabled && preferences.Set(new DeathDisplayPreferences(value, Settings.Count)); }
        public bool SetCount(int value) { return ControlsEnabled && preferences.Set(new DeathDisplayPreferences(Settings.Enabled, value)); }
        internal string NativeEventId { get; private set; }
        internal DateTime NativeStamp { get; private set; }
        internal float NativeX { get; private set; }
        internal float NativeY { get; private set; }
        internal bool SourceReady { get { return deathHooks.Ready && !failed; } }
        internal bool TimeReady { get { return timeHooks.Ready && !failed; } }
        private object CurrentToken()
        {
            if (failed || stopping || !probe.IsSessionActive || Main.netMode == 1 && Netplay.Connection == null) return null;
            return probe.SessionIdentity;
        }
        private object DeathToken(Player player)
        { return ReferenceEquals(player, Main.LocalPlayer) ? CurrentToken() : null; }
        private void Pending(object token)
        {
            if (ReferenceEquals(pendingToken, token)) return;
            if (pendingDeaths.Count != 0 || pendingTime != 0) localError = "unadmitted-session-observations";
            pendingToken = token; pendingDeaths.Clear(); pendingTime = 0;
        }
        private void ObservedTime(double value)
        {
            object token = CurrentToken(); if (token == null) return;
            if (ReferenceEquals(token, activeToken) && runtime.IsSessionActive) { Time.Advance(value); return; }
            Pending(token); pendingTime += value;
        }
        private void ObservedDeath(object token, DeathFact fact, DateTime nativeStamp)
        {
            if (ReferenceEquals(token, activeToken) && runtime.IsSessionActive) { Admit(fact, nativeStamp); return; }
            // Native callbacks precede the Runtime Update postfix. Only the
            // same probe token may cross that short, bounded handoff window.
            if (!ReferenceEquals(token, CurrentToken())) { localError = "death-session-changed-during-capture"; return; }
            Pending(token);
            if (pendingDeaths.Count == 32) { MarkMissed(); localError = "death-handoff-full"; return; }
            pendingDeaths.Add(Tuple.Create(fact, nativeStamp));
        }
        private void Admit(DeathFact fact, DateTime nativeStamp)
        {
            if (!History.Accept(fact)) { MarkMissed(); localError = History.Error ?? "death-admission-unavailable"; return; }
            NativeEventId = fact.EventId; NativeStamp = nativeStamp; NativeX = fact.X; NativeY = fact.Y;
        }
        public bool Enabled { get { return true; } }
        private void MarkMissed()
        {
            missedToken = CurrentToken(); if (pair == null || missedPairs.Contains(pair)) return;
            // A failed pair's completeness warning cannot leak into another
            // character/world. Keep this exceptional process state bounded.
            if (missedPairs.Count == 32) { FailClosed(); return; }
            missedPairs.Add(pair);
        }
        public void OnSessionStarted()
        {
            activeToken = CurrentToken(); pair = null; pageOffset = -1; selected = null;
            if (!ReferenceEquals(activeToken, missedToken)) localError = null;
            History.BeginSession(runtime.Generation); Time.BeginSession(runtime.Generation);
            if (ReferenceEquals(activeToken, pendingToken))
            { Time.Advance(pendingTime); foreach (var death in pendingDeaths) Admit(death.Item1, death.Item2); }
            pendingToken = null; pendingTime = 0; pendingDeaths.Clear();
        }
        public void OnSessionEnded()
        { History.EndSession(); Time.EndSession(); activeToken = null; pair = null; pageOffset = -1; selected = null; NativeEventId = null; }
        public void FailClosed() { failed = true; localError = "death-records-unavailable"; OnSessionEnded(); }
        public void Update(ulong tick)
        {
            if (stopping || failed || !ReferenceEquals(activeToken, CurrentToken())) return;
            if (pair == null) ObserveIdentity();
            if (pair != null) { if (!History.HasPair || History.HasPendingHandoff) History.UsePair(pair); if (!Time.HasPair) Time.UsePair(pair); }
            Time.Poll(); UpdateDemand(); Map.Update();
        }
        private void ObserveIdentity()
        {
            var player = Main.LocalPlayer; var file = Main.ActivePlayerFileData; var world = Main.ActiveWorldFileData;
            if (file == null || !ReferenceEquals(file.Player, player) || file.ServerSideCharacter || Main.ServerSideCharacter || String.IsNullOrEmpty(file.Path) || file.Path.Length > 32768 || world == null || world.UniqueId == Guid.Empty ||
                Main.netMode == 1 && (Netplay.Connection == null || Netplay.Connection.State != 10)) return;
            // Separate domain from opened-v1; death and time share only this
            // admitted reliable pair, never opened-position set semantics.
            using (var sha = SHA256.Create())
                pair = BitConverter.ToString(sha.ComputeHash(new UTF8Encoding(false, true).GetBytes("world-records-v1\0" + (file.IsCloudSave ? "cloud:" : "local:") + file.Path + "\0" + world.UniqueId.ToString("N")))).Replace("-", "").ToLowerInvariant();
            if (missedToken != null && ReferenceEquals(missedToken, activeToken)) MarkMissed();
        }
        public void RequestDetails(long offset) { pageOffset = offset; UpdateDemand(); }
        public void RequestSelection(string eventId) { selected = eventId; UpdateDemand(); }
        internal void RequestHover(string eventId) { selected = eventId; UpdateDemand(); }
        private void UpdateDemand()
        {
            bool map = Map != null && Map.Active;
            History.Request(pageOffset, map ? Settings.Count : 0, map || pageOffset >= 0 ? selected : null);
        }
        public bool QueryReady { get { return History.Snapshot.Request == History.RequestId; } }
        internal void PollPreferences()
        { if (!Preferences.IsLoaded && clock.ElapsedMilliseconds >= 2000) preferences.AbandonSlowLoad(); Time.Poll(); }
        public string PreferenceMessage
        {
            get
            {
                var value = Preferences;
                if (!value.IsLoaded) return "正在读取设置…";
                if (value.CommitUnconfirmed) return "无法确认设置是否保存成功；本次仍可使用，原文件已保护。";
                return value.Status == PreferenceStatus.Missing || value.Status == PreferenceStatus.Pending || value.Status == PreferenceStatus.Saved ? null : "设置读取或保存失败；当前选择仅本次有效，原文件已保留。";
            }
        }
        public void TakeFeedback(Action<string> display)
        {
            string error = localError ?? History.Error ?? History.Snapshot.Error ?? History.BackgroundError ?? Time.Error ?? Time.BackgroundError ?? deathHooks.Failure ?? timeHooks.Failure ?? Map.Failure;
            if (error == null && Preferences.IsLoaded) error = PreferenceMessage;
            if (error == null || error == reportedError) return;
            display(MissedHere ? "有死亡未能记录；次数和详情可能不完整，已有记录已保留。" : "死亡记录或世界天数遇到读取、记录或保存问题；现有文件已保留，请查看对应信息。"); reportedError = error;
        }
        public string CountText
        { get { var value = History.Snapshot; if (!SourceReady) return "暂不可用"; if (!value.Known) return value.Error != null || History.Error != null ? "读取失败" : "正在读取…";
            if (shownCount != value.Count) { shownCount = value.Count; countText = shownCount.ToString(CultureInfo.InvariantCulture); } return MissedHere ? "记录可能不完整" : countText; } }
        public string DaysText
        { get { if (!TimeReady) return "暂不可用"; if (!Time.Known) return Time.Error != null ? "读取失败" : "正在读取…";
            if (shownDays != Time.Days) { shownDays = Time.Days; daysText = shownDays.ToString(CultureInfo.InvariantCulture); } return daysText; } }
        private void OnExit(object sender, EventArgs args)
        {
            if (stopping) return; stopping = true; AppDomain.CurrentDomain.ProcessExit -= OnExit;
            deathHooks.Dispose(); timeHooks.Dispose(); Map.Dispose(); var elapsed = Stopwatch.StartNew();
            History.Stop(750); Time.Stop(Math.Max(0, 750 - (int)elapsed.ElapsedMilliseconds)); preferences.Stop(Math.Max(0, 750 - (int)elapsed.ElapsedMilliseconds));
        }
    }
}
