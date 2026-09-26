using System;
using System.IO;
using JueMingR.Platform.Hotkeys;
using JueMingR.Infrastructure.Storage;
using JueMingR.Features.Items;
using JueMingR.TerrariaHost.Items;
using JueMingR.TerrariaHost.Settings;
using Terraria;

namespace JueMingR.TerrariaHost.Hotkeys
{
    internal sealed class HostHotkeys
    {
        internal readonly HotkeyRegistry Registry = new HotkeyRegistry();
        internal readonly HotkeyBindings Bindings;
        internal readonly HotkeyStateFeedback Feedback;
        internal HostHotkeys(string gameDirectory, Phase0TBiomeRuntime biome, HostPreferences preferences, HostItems items, EntityLabels.HostEntityLabels labels = null, WorldTargets.HostWorldTargets targets = null, WorldObjectText.HostWorldObjectText worldObjects = null,
            Information.HostInformation information = null, Func<bool> canAdjustInformation = null, Action adjustInformation = null, Guidance.HostGuidance guidance = null, F5.IDeathControls deaths = null, F5.IMapControls maps = null, F5.IFootprintControls footprints = null, Func<bool> canAnnounce = null, Action announce = null, Func<bool> canQuery = null, Action query = null, F5.IAnnouncementControls announcements = null, QuickItems.HostQuickItems quickItems = null, CoinDeposit.HostCoinDeposit coinDeposit = null, Recovery.HostRecovery recovery = null, Processing.HostProcessing processing = null, Feedback.LocalShortFeedback display = null)
        {
            if (display != null) Feedback = new HotkeyStateFeedback(display);
            RegisterSwitch(HotkeyActionIds.Biome, "群系显示",
                () => preferences.BiomeLoaded && !biome.FeatureFailed && biome.CanObserveLocalPlayer,
                () => preferences.SetBiomeEnabled(!preferences.BiomeEnabled), () => biome.FeatureEnabled ? 1 : 0,
                () => biome.SetFeatureEnabled(preferences.BiomeLoaded && preferences.BiomeEnabled));
            if (items != null)
                for (int i = 0; i < HotkeyActionIds.Items.Length; i++)
                {
                    var action = (ItemActionKind)i;
                    RegisterSwitch(HotkeyActionIds.Items[i], ItemsPresentation.Name(action),
                        () => items.ControlsEnabled,
                        () => items.Change(items.Preferences.Value.WithEnabled(action, !items.Preferences.Value.Enabled(action))),
                        () => items.Preferences.Value.Enabled(action) ? 1 : 0, items.PollPreferences);
                }
            if (labels != null)
                for (int i = 0; i < HotkeyActionIds.EntityLabels.Length; i++)
                {
                    var kind = (Features.EntityLabels.EntityLabelKind)i;
                    RegisterSwitch(HotkeyActionIds.EntityLabels[i], EntityLabels.StylePopupLayout.Name(kind),
                        () => labels.ControlsEnabled, () => labels.Toggle(kind),
                        () => kind == Features.EntityLabels.EntityLabelKind.Npc ? (int)labels.Preferences.Value.NpcMode : labels.Preferences.Value.Enabled(kind) ? 1 : 0,
                        labels.PollPreferences, kind == Features.EntityLabels.EntityLabelKind.Npc ? (Func<int, string>)(value => value == 2 ? "类型" : "名字") : null);
                }
            if (targets != null)
                foreach (Platform.WorldTargets.WorldTargetKind target in Enum.GetValues(typeof(Platform.WorldTargets.WorldTargetKind)))
                {
                    var kind = target;
                    RegisterSwitch(HotkeyActionIds.WorldTarget(kind), F5.WorldTargetControls.Name(kind),
                        () => targets.ControlsEnabled, () => targets.Toggle(kind), () => targets.Preferences.Value.Enabled(kind) ? 1 : 0);
                }
            if (worldObjects != null)
                for (int i = 0; i < 3; i++)
                {
                    var kind = (Platform.WorldObjectText.WorldObjectKind)i;
                    RegisterSwitch(HotkeyActionIds.WorldObject(kind), F5.WorldObjectControls.Name(kind),
                        () => worldObjects.ControlsEnabled, () => worldObjects.Toggle(kind), () => (int)worldObjects.Preferences.Value.Style(kind).Mode,
                        mode: value => value == 1 ? "始终" : value == 2 ? "开过" : value == 3 ? "全部" : value == 4 ? "前几行" : "前几字");
                }
            if (information != null)
            {
                for (int i = 1; i < 4; i++)
                {
                    var kind = (Platform.Information.InformationKind)i;
                    RegisterSwitch(HotkeyActionIds.Information(kind), Information.InformationControls.Name(kind),
                        () => information.CanConfigure, () => information.Toggle(kind), () => information.Enabled(kind) ? 1 : 0);
                }
                Registry.Register(new HotkeyAction(HotkeyActionIds.AdjustInformation, "调整信息窗位置", HotkeyContext.Gameplay,
                    canAdjustInformation ?? (() => false), adjustInformation ?? (() => { })));
            }
            if (guidance != null)
                for (int i = 0; i < HotkeyActionIds.Guidance.Length; i++)
                {
                    var kind = (Features.Guidance.GuidanceKind)i;
                    RegisterSwitch(HotkeyActionIds.Guidance[i], F5.GuidanceControls.Name(kind),
                        () => guidance.ControlsEnabled, () => guidance.Toggle(kind), () => guidance.IsEnabled(kind) ? 1 : 0);
                }
            if (deaths != null) RegisterSwitch(F5.DeathControls.ActionId, "死亡点常驻",
                () => deaths.ControlsEnabled, () => deaths.SetEnabled(!deaths.Settings.Enabled), () => deaths.Settings.Enabled ? 1 : 0);
            if (maps != null) RegisterSwitch(F5.MapControls.ActionId, "地图标记", () => maps.ControlsEnabled, () => maps.SetMarkers(!maps.MarkersEnabled), () => maps.MarkersEnabled ? 1 : 0);
            if (footprints != null) RegisterSwitch(F5.FootprintControls.ActionId, "足迹", () => footprints.ControlsEnabled, () => footprints.SetDisplay(!footprints.Display), () => footprints.Display ? 1 : 0);
            if (canAnnounce != null && announce != null) Registry.Register(new HotkeyAction(F5.AnnouncementControls.SendActionId, "宣告指向内容", HotkeyContext.Gameplay, canAnnounce, announce));
            // Preserve existing send bindings. The switch is a distinct shared
            // action and must remain available while announcements are disabled.
            if (announcements != null) RegisterSwitch(F5.AnnouncementControls.ToggleActionId, "快捷宣告", () => announcements.CanConfigure, () => announcements.SetEnabled(!announcements.Enabled), () => announcements.Enabled ? 1 : 0, registryName: "快捷宣告开关");
            if (canQuery != null && query != null) Registry.Register(new HotkeyAction("item-browser.query", "查询悬停物品", HotkeyContext.Gameplay, canQuery, query));
            quickItems?.Register(Registry, Feedback);
            coinDeposit?.Register(Registry, Feedback);
            recovery?.Register(Registry, Feedback);
            processing?.Register(Registry, Feedback);
            Bindings = new HotkeyBindings(Registry, new AtomicFileDocument(Path.Combine(gameDirectory, "JueMingRData", "config", "hotkeys.json"), 65536, true));
            quickItems?.Attach(this);
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        private void RegisterSwitch(string id, string name, Func<bool> available, Action command, Func<int> state, Action apply = null, Func<int, string> mode = null, string registryName = null)
        { Registry.Register(new HotkeyAction(id, registryName ?? name, HotkeyContext.Gameplay, available, Feedback == null ? command : Feedback.Immediate(id, name, command, state, available, apply, mode))); }
        private void OnExit(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= OnExit; Bindings.Stop(750); }
    }
}
