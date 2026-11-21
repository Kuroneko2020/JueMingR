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
        private static readonly string[] Keys = { "follow", "attack", "life", "mana", "buff", "growth", "respawn", "nurse", "furniture" };
        private static readonly string[] Names = { "跟随主人", "允许攻击", "主动回血", "主动回蓝", "使用增益物品", "使用成长物品", "自动复活", "护士治疗", "家具增益" };
        private readonly DynamicHotkeyOwner owner;
        private readonly HotkeyStateFeedback feedback;
        private IReadOnlyList<CompanionProfile> observedProfiles;
        private IReadOnlyList<KeyValuePair<string, string>> observedEntries;
        private HostCompanions observedHost;
        private Guid selected;
        private Dictionary<string, HotkeyAction> actions = new Dictionary<string, HotkeyAction>(StringComparer.Ordinal);

        internal CompanionHotkeys(HotkeyRegistry registry, HotkeyStateFeedback feedback)
        {
            // The profile roster is unbounded. Only persisted binds and the
            // controls currently offered by the window need registry entries.
            owner = registry.CreateDynamicOwner(Prefix, HotkeyDocument.MaximumEntries + Keys.Length);
            this.feedback = feedback;
        }

        internal static string Id(CompanionProfile profile, CompanionSetting setting) =>
            Prefix + profile.OwnerId.ToString("N") + "." + profile.Id.ToString("N") + "." + Keys[(int)setting];

        internal static string Name(CompanionSetting setting) => Names[(int)setting];

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
            if (!owner.TryReplace(ordered, out _)) return;
            actions = next;
            observedHost = host; observedProfiles = profiles; observedEntries = entries; selected = selectedProfile;
        }

        private HotkeyAction CreateAction(HostCompanions host, Guid ownerId, Guid profileId, string profileName,
            CompanionSetting setting, string id, string name)
        {
            Func<CompanionProfile> current = () => host.SettingProfile(ownerId, profileId);
            Func<bool> available = () => current()?.Name == profileName;
            Action command = () => host.ToggleCompanionSetting(ownerId, profileId, setting);
            if (feedback != null)
            {
                // The writer may coalesce this character's inventory and other
                // settings. Only the first persisted outcome for this switch
                // change can acknowledge it, regardless of the selected panel.
                command = feedback.Committed(id, name, command, () => current()?.SettingValue(setting) ?? 0, available,
                    () => host.SettingRevision(profileId, setting), () => host.CompletedSettingSave(profileId, setting, out _),
                    () => { host.CompletedSettingSave(profileId, setting, out bool succeeded); return succeeded; },
                    setting == CompanionSetting.Life ? (Func<int, string>)(mode => mode == (int)CompanionHealthRecoveryMode.Smart ? "智能" : "快速") : null);
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
