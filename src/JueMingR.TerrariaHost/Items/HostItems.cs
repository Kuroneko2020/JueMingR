using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Items;
using JueMingR.Infrastructure.Settings;
using JueMingR.Platform.Items;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Items
{
    // Composition and lifetime only. The Feature owns lists/priority; concrete
    // adapters own native calls and causal scopes. One real config document.
    internal sealed class HostItems : IRuntimeFeature
    {
        private readonly PreferenceDocument<ItemAutomationSettings> preferences;
        private readonly Stopwatch startup = Stopwatch.StartNew();
        private long appliedRevision = -1;
        private bool stopping;
        private PreferenceStatus? reported;
        private string reportedCapability, reportedSource;
        internal readonly SingleFeatureRuntime Runtime;
        internal readonly ItemOperationOwnership Ownership = new ItemOperationOwnership();
        internal readonly ItemHostObservation World;
        internal readonly ItemAutomationFeature Feature;
        internal readonly ItemVanillaOperations Operations;
        internal readonly ItemNearbyStorage Storage;
        internal readonly int ThreadId = Thread.CurrentThread.ManagedThreadId;
        internal string CapabilityError { get; private set; }
        internal string SourceMessage { get; set; }
        internal Exception SetupError { get; private set; }
        internal bool Available { get; private set; }
        internal ulong Tick { get; private set; }
        internal PreferenceSnapshot<ItemAutomationSettings> Preferences { get { return preferences.Snapshot; } }
        public bool Enabled { get { return Feature.Enabled; } }
        internal HostItems(string verifiedGameDirectory, SingleFeatureRuntime runtime)
        {
            Runtime = runtime;
            World = new ItemHostObservation(() => Runtime.Generation, Ownership);
            Operations = new ItemVanillaOperations(World, Ownership);
            Storage = new ItemNearbyStorage(World, Ownership);
            Operations.Store = Storage.Execute;
            Feature = new ItemAutomationFeature(World, Operations);
            preferences = new PreferenceDocument<ItemAutomationSettings>(new FilePreferenceStorage(Path.Combine(verifiedGameDirectory,
                "JueMingRData", "config", "features", "item-automation.json")), new ItemAutomationCodec(ItemID.Count), ItemAutomationSettings.Default);
            var harmony = new Harmony("JueMingR.Items");
            try
            {
                // Resolve version-specific entry points inside this capability
                // boundary, so failure cannot take away Notes or the F5 shell.
                Operations.BindNativeOperation(); Storage.BindNativeOperation();
                ItemSourceHooks.Install(this, harmony);
                ItemPendingGuards.Install(this, harmony);
                World.AdditionalProtection = ItemPendingGuards.ProtectActiveMaterial;
                Storage.GuardsReady = true; Available = true;
            }
            catch (Exception e)
            {
                SetupError = e;
                try { foreach (var method in harmony.GetPatchedMethods().ToArray()) harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); } catch { }
                CapabilityError = "物品处理接入不可用：" + e.GetType().Name + "；本次未启用自动操作。";
                Feature.FailClosed();
            }
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        internal void PollPreferences()
        {
            if (!Preferences.IsLoaded && startup.ElapsedMilliseconds >= 2000) preferences.AbandonSlowLoad();
            PreferenceSnapshot<ItemAutomationSettings> snapshot = Preferences;
            if (snapshot.IsLoaded && snapshot.Revision != appliedRevision)
            { appliedRevision = snapshot.Revision; Feature.Configure(snapshot.Value); }
        }
        internal bool Change(ItemAutomationSettings value) { return !stopping && preferences.Set(value); }
        internal int[] PickerTypes(ItemListKind list)
        {
            var result = new SortedSet<int>(); Player player = World.Player;
            if (player == null) return result.ToArray();
            var existing = list == ItemListKind.Sell ? Preferences.Value.SellTypes : Preferences.Value.DiscardTypes;
            for (int i = 0; i < 58; i++)
            {
                if (i >= 50 && i < 54) continue;
                Item item = player.inventory[i];
                if (item != null && item.stack > 0 && item.type > 0 && item.type < ItemID.Count && !ItemAutomationSettings.IsCoin(item.type) && !existing.Contains(item.type)) result.Add(item.type);
            }
            return result.ToArray();
        }
        internal bool CanCapture
        { get { return CanObserve && Preferences.Value.StackEnabled; } }
        internal bool CanObserve
        { get { return !stopping && Available && Feature.Enabled && Preferences.IsLoaded &&
                    Runtime.IsSessionActive && Thread.CurrentThread.ManagedThreadId == ThreadId && World.Player != null && !World.AutomaticOperation; } }
        public void OnSessionStarted()
        { SourceMessage = null; Ownership.SetSession(Runtime.Generation); World.BeginSession(); Feature.OnSessionStarted(); }
        public void OnSessionEnded()
        { Feature.OnSessionEnded(); Ownership.SetSession(0); Storage.EndSession(); World.EndSession(); ItemSourceHooks.EndSession(); }
        public void Update(ulong tick)
        { Tick = tick; Storage.Tick = tick; Storage.Update(); Feature.Update(tick);
            if (Feature.HasFailed && CapabilityError == null) CapabilityError = "物品处理已停止：本次会话观察未能可靠完成；未确认操作不会重试。"; }
        public void FailClosed()
        {
            Feature.FailClosed();
            var unknown = new ItemOperationResult(ItemOperationState.Unconfirmed, reason: "item-host-failed");
            Ownership.FinishSale(Runtime.Generation, unknown); Ownership.FinishDiscard(Runtime.Generation, unknown); Ownership.FinishStore(Runtime.Generation, unknown);
            CapabilityError = "物品处理已停止：宿主状态异常；未确认操作不会自动重试。";
        }
        internal string PreferenceMessage
        {
            get
            {
                switch (Preferences.Status)
                {
                    case PreferenceStatus.Loading: return "正在读取物品设置";
                    case PreferenceStatus.Missing:
                    case PreferenceStatus.Pending:
                    case PreferenceStatus.Saved: return null;
                    default: return "物品设置未能可靠保存；本次选择仍可使用，原配置已保留。请退出游戏后检查 config/features/item-automation.json。";
                }
            }
        }
        internal void TakeFeedback(Action<string> display)
        {
            string message = PreferenceMessage;
            if (Preferences.IsLoaded && message != null && reported != Preferences.Status) { display(message); reported = Preferences.Status; }
            if (CapabilityError != null && CapabilityError != reportedCapability) { display(CapabilityError); reportedCapability = CapabilityError; }
            if (SourceMessage != null && SourceMessage != reportedSource) { display(SourceMessage); reportedSource = SourceMessage; }
        }
        private void OnExit(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= OnExit; stopping = true; preferences.Stop(750); }
    }
}
