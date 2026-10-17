using System;
using System.Threading;
using JueMingR.Features.HiddenFeatures;
using JueMingR.Features.Journey;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.F5;
using Terraria;

namespace JueMingR.TerrariaHost.Journey
{
    internal sealed class JourneySession : IJourneyControls, IRuntimeFeature
    {
        private readonly SingleFeatureRuntime runtime;
        private readonly HiddenFeatureAccess access;
        private readonly Func<bool> nativeInput;
        private readonly JourneyActivation state = new JourneyActivation();
        private long generation;
        private JourneyPowers baseline;
        private JourneyResearch research;
        private bool ready;
        internal Exception SetupError { get; private set; }
        public string LastFailure { get; private set; }
        public bool Enabled { get { return state.Active; } }
        public bool IsActive { get { return state.Active && MatchesCurrent(); } }
        public bool IsMenuOpen { get { return Main.LocalPlayer != null && Main.CreativeMenu.Enabled && Main.playerInventory && !Main.CreativeMenu.Blocked; } }
        public bool CanEnable { get { return UnavailableReason == null; } }
        public string UnavailableReason
        {
            get
            {
                if (!ready || state.Failed) return "旅行菜单暂不可用。";
                if (!access.IsUnlocked) return "旅行菜单尚未解锁。";
                if (!runtime.IsSessionActive || Main.gameMenu || WorldGen.isGeneratingOrLoadingWorld || Main.netMode != 0 || Main.ServerSideCharacter)
                    return "旅行菜单仅限进入世界后的单人游戏。";
                var player = Main.LocalPlayer;
                if (player == null || !player.active || Main.ActivePlayerFileData == null || Main.ActiveWorldFileData == null ||
                    !ReferenceEquals(Main.ActivePlayerFileData.Player, player)) return "角色或世界尚未就绪。";
                // Cloud providers are left untouched. This candidate admits
                // local files only until a real provider's output is verified.
                if (Main.ActivePlayerFileData.IsCloudSave || Main.ActiveWorldFileData.IsCloudSave) return "旅行菜单暂不支持云存档。";
                if (player.dead || Main.blockInput || Main.CreativeMenu.Blocked) return "请先结束当前交互。";
                return null;
            }
        }

        internal JourneySession(SingleFeatureRuntime runtime, HiddenFeatureAccess access, Func<bool> nativeInput = null)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.access = access ?? throw new ArgumentNullException(nameof(access));
            this.nativeInput = nativeInput ?? (() => false);
            try { JourneyHooks.Install(this); ready = true; }
            catch (Exception error) { SetupError = error; LastFailure = "旅行菜单暂不可用，原版操作不受影响。"; state.Fail(); }
        }

        public bool SetEnabled(bool enabled)
        {
            if (!enabled) return Stop(true);
            if (IsActive) return Open();
            string unavailable = UnavailableReason;
            if (unavailable != null) { LastFailure = unavailable; return false; }
            try
            {
                if (research == null) research = new JourneyResearch();
                var captured = new JourneyPowers(Main.LocalPlayer, Main.ActivePlayerFileData, Main.ActiveWorldFileData);
                generation = runtime.Generation;
                Volatile.Write(ref baseline, captured);
                state.Activate();
                captured.InitializeNeutral();
                JourneyResearch.InvalidateMenu();
                if (!Open())
                {
                    string openingFailure = LastFailure;
                    if (Stop(true)) LastFailure = openingFailure;
                    return false;
                }
                LastFailure = null; return true;
            }
            catch (Exception)
            {
                Stop(true); LastFailure = "旅行菜单未能开启。"; return false;
            }
        }
        public bool Toggle() { return SetEnabled(!state.Active); }
        public bool Open()
        {
            if (!IsActive) { LastFailure = "请先开启旅行菜单。"; return false; }
            try
            {
                JourneyResearch.EnsureMenuReady();
                if (!Main.CreativeMenu.Enabled || !Main.playerInventory || Main.CreativeMenu.Blocked) Main.LocalPlayer.ToggleCreativeMenu();
                if (!IsMenuOpen) { LastFailure = "原版旅行菜单未能打开。"; return false; }
                LastFailure = null; return true;
            }
            catch (Exception) { LastFailure = "原版旅行菜单未能打开。"; return false; }
        }
        public bool CloseMenu()
        {
            try
            {
                // Native ToggleMenu returns the research slot to its player.
                // CloseMenu only hides it and would strand that real item.
                if (Main.CreativeMenu.Enabled) Main.CreativeMenu.ToggleMenu();
                return !Main.CreativeMenu.Enabled;
            }
            catch (Exception) { LastFailure = "旅行菜单未能收起。"; return false; }
        }
        private bool Stop(bool restore)
        {
            var saved = Volatile.Read(ref baseline);
            if (saved == null) { state.End(); return true; }
            // Revoke read-side admission first. Existing save contexts already
            // own immutable substreams and survive this reference retirement.
            state.End();
            bool same = ReferenceEquals(Main.ActiveWorldFileData, saved.WorldFile) && ReferenceEquals(Main.LocalPlayer, saved.Player);
            bool closed = !same || CloseMenu();
            try
            {
                if (restore && same) saved.Restore();
                JourneyResearch.InvalidateMenu();
                if (!closed)
                {
                    state.Fail(); LastFailure = "旅行菜单未能收起，已停止继续开启。"; return false;
                }
                Volatile.Write(ref baseline, null);
                LastFailure = null; return true;
            }
            catch (Exception)
            {
                // Keep the baseline if restoration failed. Saving remains
                // protected, but new enable attempts must stay unavailable.
                state.Fail(); LastFailure = "旅行能力未能完全恢复，已停止继续开启。"; return false;
            }
        }
        private bool MatchesCurrent()
        {
            var saved = Volatile.Read(ref baseline);
            return saved != null && runtime.IsSessionActive && generation == runtime.Generation && !Main.gameMenu && Main.netMode == 0 &&
                ReferenceEquals(Main.LocalPlayer, saved.Player) && ReferenceEquals(Main.ActiveWorldFileData, saved.WorldFile);
        }
        internal bool OwnsPlayer(Player player) { return IsActive && ReferenceEquals(player, baseline.Player); }
        internal bool OwnsTracker(Terraria.GameContent.Creative.ItemsSacrificedUnlocksTracker tracker)
        { return IsActive && ReferenceEquals(tracker, baseline.Player.creativeTracker.ItemSacrifices); }
        internal JourneyResearch Research { get { return research; } }
        internal bool CanUseNativeInput { get { return IsActive && nativeInput(); } }
        internal JourneyPowers Persistence { get { return Volatile.Read(ref baseline); } }
        internal void BeforeNativeReset() { Stop(false); research = null; }
        public void OnSessionStarted() { }
        public void OnSessionEnded() { Stop(true); research = null; }
        public void Update(ulong updateTick) { if (state.Active && !MatchesCurrent()) Stop(true); }
        public void FailClosed() { Stop(true); state.Fail(); }
    }
}
