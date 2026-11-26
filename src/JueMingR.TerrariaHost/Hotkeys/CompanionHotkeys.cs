using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.Companions;

namespace JueMingR.TerrariaHost.Hotkeys
{
    internal sealed class CompanionHotkeys
    {
        private const string Prefix = "companions.profile.";
        private const string RecallKey = "wormhole";
        internal const string RecallName = "传送AI到玩家";
        private static readonly string[] Keys = { "follow", "attack", "life", "mana", "buff", "growth", "respawn", "nurse", "furniture",
            "fish-equipment", "fish-storage", "fish-cut", "fish-teleport", "fish-loadout", "fishing" };
        private static readonly string[] Names = { "跟随主人", "允许攻击", "主动回血", "主动回蓝", "使用增益物品", "使用成长物品", "自动复活", "护士治疗", "家具增益",
            "自动配装", "自动存放鱼", "切杆跳过", "传送后停止钓鱼", "自动换装", "钓鱼模式" };
        private readonly DynamicHotkeyOwner owner;
        private readonly HotkeyStateFeedback feedback;
        private IReadOnlyList<CompanionProfile> observedProfiles;
        private IReadOnlyList<KeyValuePair<string, string>> observedEntries;
        private HostCompanions observedHost;
        private Guid selected;
        private CompanionDeletion removing;
        private long removalCommand;
        private Dictionary<string, HotkeyAction> actions = new Dictionary<string, HotkeyAction>(StringComparer.Ordinal);

        internal CompanionHotkeys(HotkeyRegistry registry, HotkeyStateFeedback feedback)
        {
            // The profile roster is unbounded. Only persisted binds and the
            // controls currently offered by the window need registry entries.
            owner = registry.CreateDynamicOwner(Prefix, HotkeyDocument.MaximumEntries + Keys.Length + 1);
            this.feedback = feedback;
        }

        internal static string Id(CompanionProfile profile, CompanionSetting setting) =>
            Id(profile.OwnerId, profile.Id, (int)setting);

        private static string Id(Guid ownerId, Guid profileId, int setting) =>
            Id(ownerId, profileId, Keys[setting]);

        private static string Id(Guid ownerId, Guid profileId, string key) =>
            Prefix + ownerId.ToString("N") + "." + profileId.ToString("N") + "." + key;

        internal static string RecallId(CompanionProfile profile) => Id(profile.OwnerId, profile.Id, RecallKey);

        internal static string Name(CompanionSetting setting) => Names[(int)setting];

        internal void PollDeletions(HostCompanions host, HotkeyBindings bindings, Guid selectedProfile)
        {
            if (host == null) return;
            var pending = host.PollDeletionBindings();
            if (removing != null && bindings.CompletionId == removalCommand)
            {
                if (!bindings.CompletionSucceeded) host.FailDeletionBindings(removing);
                removing = null;
            }
            if (pending.Count == 0 || removing != null || !bindings.Loaded || bindings.Busy) return;

            // Retire dynamic actions before removing their rows. A write that
            // was already pending keeps its own receipt and is polled first.
            bool refreshed = false;
            foreach (var deletion in host.Deletions)
            {
                if (!deletion.FilesRemoved || deletion.Error != null || deletion.Finishing) continue;
                if (!refreshed) { Refresh(host, bindings, selectedProfile); refreshed = true; }
                if (bindings.Protected) { host.FailDeletionBindings(deletion); continue; }
                string row = null;
                for (int i = 0; row == null && i <= Keys.Length; i++)
                {
                    string id = Id(deletion.OwnerId, deletion.ProfileId, i == Keys.Length ? RecallKey : Keys[i]);
                    foreach (var entry in bindings.StoredEntries)
                        if (entry.Key == id) { row = id; break; }
                }
                if (row == null) { host.FinishDeletionBindings(deletion); continue; }
                if (!bindings.TryRemoveRetired(owner, row, out removalCommand, out _))
                { host.FailDeletionBindings(deletion); continue; }
                removing = deletion;
                return;
            }
        }

        internal void Refresh(HostCompanions host, HotkeyBindings bindings, Guid selectedProfile)
        {
            var profiles = host?.Profiles;
            var entries = bindings.StoredEntries;
            if (ReferenceEquals(host, observedHost) && ReferenceEquals(entries, observedEntries) &&
                selectedProfile == selected && SameRoster(profiles, observedProfiles))
            {
                // Body captures and ordinary preference saves replace profile
                // snapshots without changing any action identity or caption.
                observedProfiles = profiles;
                return;
            }

            var bound = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
                if (entry.Value.Length != 0 && owner.Owns(entry.Key)) bound.Add(entry.Key);

            var next = new Dictionary<string, HotkeyAction>(StringComparer.Ordinal);
            var ordered = new List<HotkeyAction>();
            if (profiles != null && (bound.Count > 0 || selectedProfile != Guid.Empty))
                foreach (var profile in profiles)
                {
                    for (int index = 0; index < Keys.Length; index++)
                    {
                        var setting = (CompanionSetting)index;
                        string id = Id(profile, setting);
                        if (profile.Id != selectedProfile && !bound.Contains(id)) continue;
                        string name = profile.Name + " · " + Name(setting);
                        if (!ReferenceEquals(host, observedHost) || !actions.TryGetValue(id, out var action) || action.Name != name)
                        {
                            action = CreateAction(host, profile.OwnerId, profile.Id, profile.Name, setting, id, name);
                        }
                        next.Add(id, action);
                        ordered.Add(action);
                    }
                    string recallId = RecallId(profile);
                    if (profile.Id != selectedProfile && !bound.Contains(recallId)) continue;
                    string recallName = profile.Name + " · " + RecallName;
                    if (!ReferenceEquals(host, observedHost) || !actions.TryGetValue(recallId, out var recall) || recall.Name != recallName)
                        recall = new HotkeyAction(recallId, recallName, HotkeyContext.Gameplay,
                            () => host.SettingProfile(profile.OwnerId, profile.Id) != null, () => host.TryManualRecall(profile.Id));
                    next.Add(recallId, recall);
                    ordered.Add(recall);
                }
            if (!owner.TryReplace(ordered, out _)) return;
            actions = next;
            observedHost = host; observedProfiles = profiles; observedEntries = entries; selected = selectedProfile;
        }

        private HotkeyAction CreateAction(HostCompanions host, Guid ownerId, Guid profileId, string profileName,
            CompanionSetting setting, string id, string name)
        {
            Func<CompanionProfile> current = () => host.SettingProfile(ownerId, profileId);
            Func<bool> currentIdentity = () => current()?.Name == profileName;
            Func<bool> available = () => currentIdentity() &&
                (setting != CompanionSetting.Follow || !host.IsFishing(ownerId, profileId)) &&
                (setting != CompanionSetting.FishingMode || host.CanStartFishing(ownerId, profileId) || host.IsFishing(ownerId, profileId));
            Action command = () => host.ToggleCompanionSetting(ownerId, profileId, setting);
            if (setting == CompanionSetting.FishingMode)
            {
                command = feedback?.Immediate(id, name, command, () => host.IsFishing(ownerId, profileId) ? 1 : 0, currentIdentity) ?? command;
                return new HotkeyAction(id, name, HotkeyContext.Gameplay, available, command);
            }
            if (feedback != null)
            {
                // The writer may coalesce this character's inventory and other
                // settings. Only the first persisted outcome for this switch
                // change can acknowledge it, regardless of the selected panel.
                command = feedback.Committed(id, name, command, () => current()?.SettingValue(setting) ?? 0, available,
                    () => host.SettingRevision(profileId, setting), () => host.CompletedSettingSave(profileId, setting, out _),
                    () => { host.CompletedSettingSave(profileId, setting, out bool succeeded); return succeeded; },
                    setting == CompanionSetting.Life ? (Func<int, string>)(mode => mode == (int)CompanionHealthRecoveryMode.Smart ? "智能" : "快速") :
                    setting == CompanionSetting.FishingStorage ? (Func<int, string>)(mode => mode == 2 ? "任务鱼" : "所有") : null);
            }
            return new HotkeyAction(id, name, HotkeyContext.Gameplay, available, command);
        }

        private static bool SameRoster(IReadOnlyList<CompanionProfile> left, IReadOnlyList<CompanionProfile> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (left[i].Id != right[i].Id || left[i].OwnerId != right[i].OwnerId || left[i].Name != right[i].Name)
                    return false;
            return true;
        }
    }
}
