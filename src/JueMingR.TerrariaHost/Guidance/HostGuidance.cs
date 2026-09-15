using System;
using System.Diagnostics;
using System.IO;
using JueMingR.Features.Guidance;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.Npcs;
using Terraria;

namespace JueMingR.TerrariaHost.Guidance
{
    internal sealed class HostGuidance : IRuntimeFeature, F5.IGuidanceControls
    {
        private readonly SingleFeatureRuntime runtime;
        private readonly NativeNpcObservation npcs;
        private readonly GuidanceObservationReader source;
        private readonly Func<bool> inputAllowed;
        private readonly Func<bool> presentationAllowed;
        private readonly PreferenceDocument<GuidancePreferences> preferences;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private bool stopping;
        private int failures;
        private int reportedFailures;
        private string reportedPreference;
        private Platform.Guidance.MerchantTestReceipt reportedResult;
        internal readonly RareCreatureDirection Rare;
        internal readonly TravellingMerchantDirection Merchant;
        internal readonly MerchantLocation Location = new MerchantLocation();
        internal readonly EquipmentWarning Equipment = new EquipmentWarning();
        internal readonly MerchantTestFeature MerchantTest;
        internal readonly GuidanceWorldLayer World;
        internal Rendering.WorldLayerStatus LayerStatus { get; set; }
        internal HostGuidance(string gameDirectory, SingleFeatureRuntime runtime, NativeNpcObservation npcs, Func<bool> inputAllowed, Func<bool> merchantInputAllowed, Func<bool> presentationAllowed)
        {
            this.runtime = runtime; this.npcs = npcs; this.inputAllowed = inputAllowed; this.presentationAllowed = presentationAllowed;
            source = new GuidanceObservationReader(npcs); Rare = new RareCreatureDirection(npcs); Merchant = new TravellingMerchantDirection(npcs);
            MerchantTest = new MerchantTestFeature(new MerchantTestOperations(() => Session, merchantInputAllowed));
            var file = new AtomicFileDocument(Path.Combine(gameDirectory, "JueMingRData", "config", "features", "guidance.json"), 65536, true, ".schema1-original");
            preferences = new PreferenceDocument<GuidancePreferences>(file, new RetainingCodec(file), GuidancePreferences.Default);
            World = new GuidanceWorldLayer(this); AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        internal long Session { get { return runtime.IsSessionActive ? runtime.Generation : -1; } }
        internal PreferenceSnapshot<GuidancePreferences> Preferences { get { return preferences.Snapshot; } }
        internal bool ControlsEnabled { get { return !stopping && Preferences.IsLoaded && runtime.IsSessionActive; } }
        bool F5.IGuidanceControls.ControlsEnabled { get { return ControlsEnabled; } }
        bool F5.IGuidanceControls.IsEnabled(GuidanceKind kind) { return IsEnabled(kind); }
        bool F5.IGuidanceControls.SetEnabled(GuidanceKind kind, bool enabled) { return SetEnabled(kind, enabled); }
        string F5.IGuidanceControls.SummonReason { get { return SummonReason; } }
        void F5.IGuidanceControls.RequestMerchant() { RequestMerchant(); }
        GuidancePreferences F5.IGuidanceControls.Settings { get { return Preferences.Value; } }
        string F5.IGuidanceControls.PreferenceMessage { get { return PreferenceMessage; } }
        bool F5.IGuidanceControls.SetColor(GuidanceKind kind, int rgb) { return SetColor(kind, rgb); }
        bool F5.IGuidanceControls.StepSize(GuidanceKind kind, int direction) { return StepSize(kind, direction); }
        void F5.IGuidanceControls.ResetStyle(GuidanceKind kind) { ResetStyle(kind); }
        internal bool SetColor(GuidanceKind kind, int rgb) { return ControlsEnabled && preferences.Set(Preferences.Value.WithStyle(kind, Preferences.Value.Style(kind).WithColor(rgb))); }
        internal bool StepSize(GuidanceKind kind, int direction) { return ControlsEnabled && preferences.Set(Preferences.Value.WithStyle(kind, Preferences.Value.Style(kind).Step(direction))); }
        internal bool ResetStyle(GuidanceKind kind) { return ControlsEnabled && preferences.Set(Preferences.Value.ResetStyle(kind)); }
        internal string PreferenceMessage
        {
            get
            {
                var snapshot = Preferences;
                return !snapshot.IsLoaded ? "正在加载指引设置" : snapshot.CommitUnconfirmed ? "无法确认指引设置是否保存成功；本次仍可使用，文件已保护。" :
                    snapshot.Status == PreferenceStatus.Missing || snapshot.Status == PreferenceStatus.Pending || snapshot.Status == PreferenceStatus.Saved ? null :
                    "指引设置加载或保存失败；当前修改仅本次有效，原文件已保留。";
            }
        }
        // An unsampled outer Update revokes action permission, but can still
        // be followed by Draw (native FrameSkip.Off). Presentation uses focus
        // quarantine independently; never retain the old action permission.
        internal bool CanDraw { get { return runtime.IsSessionActive && LayerStatus == Rendering.WorldLayerStatus.Ready && presentationAllowed() && GuidanceObservationReader.ValidPlayer; } }
        internal bool IsEnabled(GuidanceKind kind) { return Preferences.Value.Enabled(kind); }
        internal bool SetEnabled(GuidanceKind kind, bool enabled)
        {
            if (!ControlsEnabled) return false;
            if (enabled) { failures &= ~(1 << (int)kind); reportedFailures &= ~(1 << (int)kind); World.Recover(kind); }
            bool changed = preferences.Set(Preferences.Value.WithEnabled(kind, enabled));
            if (!enabled) { if (kind == GuidanceKind.Rare) Rare.Clear(); else if (kind == GuidanceKind.Merchant) { Merchant.Clear(); Location.Clear(); } else Equipment.Clear(); }
            return changed;
        }
        internal void Toggle(GuidanceKind kind) { SetEnabled(kind, !IsEnabled(kind)); }
        internal string SummonReason { get { return !ControlsEnabled ? "当前暂不可用。" : MerchantTest.Pending ? "正在尝试召唤。" : MerchantTestOperations.UnavailableReason; } }
        internal void RequestMerchant() { if (SummonReason == null && inputAllowed()) MerchantTest.Request(Session); }
        // Kept enabled to retire pending intents and disabled feature content;
        // each closed display exits before any NPC/equipment/pylon observation.
        public bool Enabled { get { return true; } }
        internal void PollPreferences() { if (!Preferences.IsLoaded && clock.ElapsedMilliseconds >= 2000) preferences.AbandonSlowLoad(); }
        public void OnSessionStarted() { failures = reportedFailures = 0; Clear(); }
        public void OnSessionEnded() { Clear(); }
        private void Clear() { Rare.Clear(); Merchant.Clear(); Location.Clear(); Equipment.Clear(); MerchantTest.Cancel(); World.Clear(); }
        public void FailClosed() { failures = 7; Clear(); }
        public void Update(ulong tick)
        {
            bool valid = GuidanceObservationReader.ValidPlayer;
            bool pending = MerchantTest.Pending;
            MerchantTest.Update(Session, valid && inputAllowed());
            if (pending) { npcs.BeginTick(); Merchant.InvalidateDiscovery(); }
            if (!valid) { Rare.Clear(); Merchant.Clear(); Location.Clear(); Equipment.Clear(); return; }
            var player = Main.LocalPlayer;
            try { Rare.Update((failures & 1) == 0 && IsEnabled(GuidanceKind.Rare) && GuidanceObservationReader.RareQualified, player.Center.X, player.Center.Y); }
            catch { failures |= 1; Rare.Clear(); }
            try
            {
                Merchant.Update((failures & 2) == 0 && IsEnabled(GuidanceKind.Merchant));
                if (Merchant.Visible) Location.Update(Merchant.Target, npcs, source); else Location.Clear();
            }
            catch { failures |= 2; Merchant.Clear(); Location.Clear(); }
            try
            {
                if ((failures & 4) != 0 || !IsEnabled(GuidanceKind.Equipment)) { Equipment.Clear(); return; }
                int events; bool known = source.ReadDanger(out events);
                bool danger = events != 0 || source.BossCount > 0;
                if (known && danger) known = source.Equipment.Read(player);
                Equipment.Update(known, true, events, source.Bosses, source.BossCount, source.Equipment.Types, danger ? source.Equipment.Count : 0, clock.Elapsed.TotalSeconds);
            }
            catch { failures |= 4; Equipment.Clear(); }
        }
        internal void TakeFeedback(Action<string> display)
        {
            int fresh = (failures | World.Failures) & ~reportedFailures;
            if (fresh != 0)
            {
                reportedFailures |= fresh;
                for (int i = 0; i < 3; i++) if ((fresh & 1 << i) != 0) display(F5.GuidanceControls.Name((GuidanceKind)i) + "暂不可用，设置已保留；可重新开启尝试恢复。");
            }
            var result = MerchantTest.Result;
            if (result != null && !ReferenceEquals(result, reportedResult)) { reportedResult = result; display(result.Message); }
            string text = Preferences.IsLoaded ? PreferenceMessage : null;
            if (text != null && text != reportedPreference) { reportedPreference = text; display(text); }
        }
        private void OnExit(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= OnExit; stopping = true; Clear(); World.Stop(); preferences.Stop(750); }
        private sealed class RetainingCodec : IPreferenceCodec<GuidancePreferences>
        {
            private readonly AtomicFileDocument file;
            private readonly GuidancePreferenceCodec codec = new GuidancePreferenceCodec();
            internal RetainingCodec(AtomicFileDocument file) { this.file = file; }
            public GuidancePreferences Decode(byte[] contents)
            {
                int version; var value = codec.Decode(contents, out version);
                // Load is read-only. The first explicit save archives validated
                // schema1 bytes before replacing them; unknown input never migrates.
                if (version == 1) file.RetainLoadedSource(); return value;
            }
            public byte[] Encode(GuidancePreferences value) { return codec.Encode(value); }
        }
    }
}
