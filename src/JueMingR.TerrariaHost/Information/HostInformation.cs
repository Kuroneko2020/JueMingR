using System;
using System.Diagnostics;
using System.IO;
using JueMingR.Features.Information;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Information;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.Settings;

namespace JueMingR.TerrariaHost.Information
{
    // Preference intent is process-scoped. Native facts and each content owner
    // have their own session lifetime; no world state is saved in these files.
    internal sealed class HostInformation : IInformationControls, Platform.Runtime.IRuntimeFeature
    {
        private readonly PreferenceDocument<InformationPreferences> preferences;
        private readonly PreferenceDocument<WindowPosition> position;
        private readonly HostPreferences biomePreferences;
        private readonly Phase0TBiomeRuntime biome;
        private readonly Action<Exception> biomeFailure;
        private readonly Stopwatch startup = Stopwatch.StartNew();
        private bool stopping;
        private bool hudFailed;
        private string reportedSettings, reportedPosition;
        private int displayFailures, reportedDisplayFailures;
        private Microsoft.Xna.Framework.Matrix hudMatrix;
        private readonly InformationObservationReader source;
        internal readonly InfectionSummary Infection = new InfectionSummary();
        internal readonly LuckSummary Luck = new LuckSummary();
        internal readonly AnglerSummary Angler = new AnglerSummary();
        internal readonly InformationHud Hud;
        internal readonly InformationAdjustment Adjustment = new InformationAdjustment();
        internal readonly InformationPointerObservation Pointer = new InformationPointerObservation();
        internal long Tick { get; private set; }
        internal long Session { get { return biome.SharedRuntime.Generation; } }
        internal long NativeEpoch { get { return source.NativeEpoch; } }
        internal HostInformation(string gameDirectory, Phase0TBiomeRuntime biome, HostPreferences biomePreferences, InformationReadiness readiness, Action<Exception> biomeFailure = null, Npcs.NativeNpcObservation nativeNpcs = null)
        {
            this.biome = biome; this.biomePreferences = biomePreferences; this.biomeFailure = biomeFailure;
            source = new InformationObservationReader(readiness, nativeNpcs);
            string config = Path.Combine(gameDirectory, "JueMingRData", "config");
            preferences = new PreferenceDocument<InformationPreferences>(new AtomicFileDocument(Path.Combine(config, "features", "information-display.json"), 65536, true),
                new InformationPreferenceCodec(), InformationPreferences.Default);
            position = new PreferenceDocument<WindowPosition>(new AtomicFileDocument(Path.Combine(config, "information-window.json"), 65536, true), new UiPreferenceCodec(), null);
            Hud = new InformationHud(this);
            source.Attach();
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        internal PreferenceSnapshot<InformationPreferences> Preferences { get { return preferences.Snapshot; } }
        InformationPreferences IInformationControls.Settings { get { return Preferences.Value; } }
        bool IInformationControls.CanConfigure { get { return CanConfigure; } }
        bool IInformationControls.PositionReady { get { return PositionReady; } }
        string IInformationControls.PreferenceMessage { get { return PreferenceMessage; } }
        string IInformationControls.PositionMessage { get { return PositionMessage; } }
        bool IInformationControls.Enabled(InformationKind kind) { return Enabled(kind); }
        bool IInformationControls.SetEnabled(InformationKind kind, bool enabled) { return SetEnabled(kind, enabled); }
        bool IInformationControls.SetColor(InformationKind kind, int rgb) { return SetColor(kind, rgb); }
        bool IInformationControls.StepSize(InformationKind kind, int direction) { return StepSize(kind, direction); }
        void IInformationControls.ResetStyle(InformationKind kind) { ResetStyle(kind); }
        internal PreferenceSnapshot<WindowPosition> Position { get { return position.Snapshot; } }
        internal bool CanConfigure { get { return !stopping && Preferences.IsLoaded; } }
        internal bool PositionReady { get { return !stopping && Position.IsLoaded; } }
        internal bool Enabled(InformationKind kind) { return kind == InformationKind.Biome ? biomePreferences.BiomeEnabled : Preferences.Value.Enabled(kind); }
        internal bool SetEnabled(InformationKind kind, bool enabled)
        {
            if (!CanConfigure) return false;
            if (kind == InformationKind.Biome)
            {
                if (!biomePreferences.BiomeLoaded) return false;
                biomePreferences.SetBiomeEnabled(enabled); return true;
            }
            return preferences.Set(Preferences.Value.WithEnabled(kind, enabled));
        }
        internal bool Toggle(InformationKind kind) { return SetEnabled(kind, !Enabled(kind)); }
        internal bool SetColor(InformationKind kind, int rgb) { return CanConfigure && preferences.Set(Preferences.Value.WithStyle(kind, Preferences.Value.Style(kind).WithColor(rgb))); }
        internal bool StepSize(InformationKind kind, int direction) { return CanConfigure && preferences.Set(Preferences.Value.WithStyle(kind, Preferences.Value.Style(kind).Step(direction))); }
        internal void ResetStyle(InformationKind kind) { if (CanConfigure) preferences.Set(Preferences.Value.ResetStyle(kind)); }
        internal bool SetPosition(WindowPosition value) { return PositionReady && position.Set(value); }
        internal string Text(InformationKind kind)
        {
            if (!Enabled(kind)) return null;
            switch (kind)
            {
                case InformationKind.Biome: var model = biome.CurrentViewModel; return biome.FeatureEnabled && model != null && model.Visible ? model.Text : null;
                case InformationKind.Infection: return Infection.Content.Text;
                case InformationKind.Luck: return Luck.Content.Text;
                default: return Angler.Content.Text;
            }
        }
        internal void PollPreferences()
        {
            if (startup.ElapsedMilliseconds >= 2000)
            { if (!Preferences.IsLoaded) preferences.AbandonSlowLoad(); if (!Position.IsLoaded) position.AbandonSlowLoad(); }
        }
        internal string PreferenceMessage { get { return Message(Preferences, "信息显示设置"); } }
        internal string PositionMessage { get { return Message(Position, "信息窗位置"); } }
        private static string Message<T>(PreferenceSnapshot<T> snapshot, string label)
        {
            if (snapshot.CommitUnconfirmed) return label + "保存结果未确认，原件已保护；当前选择仅本次内存生效。";
            switch (snapshot.Status)
            {
                case PreferenceStatus.Loading: return "正在读取" + label;
                case PreferenceStatus.Missing: case PreferenceStatus.Pending: case PreferenceStatus.Saved: return null;
                case PreferenceStatus.UnknownFields: case PreferenceStatus.UnsupportedVersion: return label + "含未知版本或字段，原件已保留；当前选择仅本次有效。";
                case PreferenceStatus.Conflict: return label + "发生外部变化，已停止覆盖；当前选择仅本次有效。";
                default: return label + "未能可靠读取或保存，原件已保留；当前选择仅本次有效。";
            }
        }
        internal void TakeFeedback(Action<string> display)
        {
            int fresh = displayFailures & ~reportedDisplayFailures;
            if (fresh != 0)
            {
                reportedDisplayFailures |= fresh;
                for (int i = 0; i < 4; i++) if ((fresh & (1 << i)) != 0) display(InformationControls.Name((InformationKind)i) + "绘制不可用，设置已保留。");
            }
            string message = PreferenceMessage;
            if (Preferences.IsLoaded && message != null && message != reportedSettings) { display(message); reportedSettings = message; }
            message = PositionMessage;
            if (Position.IsLoaded && message != null && message != reportedPosition) { display(message); reportedPosition = message; }
        }
        internal void ClearContent() { Infection.Clear(); Luck.Clear(); Angler.Clear(); Hud.Clear(); }
        internal bool InputGeometryCurrent
        {
            get
            {
                var matrix = Terraria.Main.UIScaleMatrix; var screen = ScreenSize();
                return matrix == hudMatrix && matrix.M11 > 0 && matrix.M22 > 0 &&
                    Hud.MatchesInput(Terraria.GameContent.FontAssets.MouseText?.Value, screen.X / matrix.M11, screen.Y / matrix.M22);
            }
        }
        private static Microsoft.Xna.Framework.Vector2 ScreenSize()
        {
            var screen = Terraria.GameInput.PlayerInput.OriginalScreenSize;
            return screen.X <= 0 || screen.Y <= 0 ? new Microsoft.Xna.Framework.Vector2(Terraria.Main.screenWidth, Terraria.Main.screenHeight) : screen;
        }
        internal void PrepareHud()
        {
            if (hudFailed) return;
            // Presentation is optional. An unexpected resource/geometry error
            // must never escape UpdateShell into the shared Runtime failure gate.
            try { PrepareHudCore(); }
            catch (Exception error) { DisplayFailed(error); }
        }
        private void PrepareHudCore()
        {
            if (!biome.SharedRuntime.IsSessionActive || Terraria.Main.hideUI || Terraria.Main.mapFullscreen || Terraria.Main.dedServ)
            { if (Hud.Visible) Hud.Clear(); return; }
            var matrix = Terraria.Main.UIScaleMatrix; var screen = ScreenSize();
            if (matrix.M11 <= 0 || matrix.M22 <= 0) { Hud.Clear(); return; }
            Hud.Prepare(Terraria.GameContent.FontAssets.MouseText?.Value, screen.X / matrix.M11, screen.Y / matrix.M22, Adjustment.Active, Adjustment.Draft);
            hudMatrix = matrix;
            Adjustment.BindGeometry(Hud.GeometryVersion);
        }
        bool Platform.Runtime.IRuntimeFeature.Enabled { get { return Preferences.Value.AnySummaryEnabled; } }
        public void OnSessionStarted() { hudFailed = false; Adjustment.Cancel(); Pointer.Invalidate(); source.Clear(); ClearContent(); }
        public void OnSessionEnded()
        {
            // No native input is read during shutdown. Only a still-trusted
            // cached foreground drag can produce this final intent.
            FinishNormalAdjustment();
            Pointer.Invalidate(); source.Clear(); ClearContent();
        }
        public void Update(ulong tick) { Tick++; source.Update(this); }
        public void FailClosed() { Adjustment.Cancel(); Pointer.Invalidate(); source.Clear(); ClearContent(); }
        internal void DisplayFailed(Exception error)
        { hudFailed = true; Adjustment.Cancel(); Pointer.Invalidate(); Hud.Clear(); displayFailures |= 15; }
        internal void DisplayFailed(InformationKind kind, Exception error)
        {
            displayFailures |= 1 << (int)kind;
            // Retain the existing biome fault latch and diagnostic identity;
            // its shared layer remains available to the independent summaries.
            if (kind == InformationKind.Biome) { biome.FailClosed(); biomeFailure?.Invoke(error); }
        }
        internal void FinishNormalAdjustment()
        { WindowPosition intent = Adjustment.FinishNormal(); if (intent != null) SetPosition(intent); Pointer.Invalidate(); }
        private void OnExit(object sender, EventArgs args)
        {
            AppDomain.CurrentDomain.ProcessExit -= OnExit;
            source.Detach();
            FinishNormalAdjustment();
            stopping = true;
            var budget = Stopwatch.StartNew(); preferences.Stop(750); position.Stop(Math.Max(0, 750 - (int)budget.ElapsedMilliseconds));
        }
    }
}
