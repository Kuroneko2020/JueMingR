using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.Companions;

namespace JueMingR.TerrariaHost.Hotkeys
{
    internal enum CompanionSetting { Follow, Attack, Life, Mana, Buff, Growth, Respawn }

    internal sealed class CompanionHotkeys
    {
        private const string Prefix = "companions.profile.";
        private static readonly string[] Keys = { "follow", "attack", "life", "mana", "buff", "growth", "respawn" };
        private static readonly string[] Names = { "跟随主人", "允许攻击", "主动回血", "主动回蓝", "使用增益物品", "使用成长物品", "自动复活" };
        private readonly DynamicHotkeyOwner owner;
        private IReadOnlyList<CompanionProfile> observedProfiles;
        private IReadOnlyList<KeyValuePair<string, string>> observedEntries;
        private HostCompanions observedHost;
        private Guid selected;
        private Dictionary<string, HotkeyAction> actions = new Dictionary<string, HotkeyAction>(StringComparer.Ordinal);

        internal CompanionHotkeys(HotkeyRegistry registry)
        {
            // The profile roster is unbounded. Only persisted binds and the seven
            // controls currently offered by the window need registry entries.
            owner = registry.CreateDynamicOwner(Prefix, HotkeyDocument.MaximumEntries + Keys.Length);
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
                            Guid ownerId = profile.OwnerId, profileId = profile.Id;
                            action = new HotkeyAction(id, name, HotkeyContext.Gameplay,
                                () => host.CompanionSettingCurrent(ownerId, profileId),
                                () => host.ToggleCompanionSetting(ownerId, profileId, setting));
                        }
                        next.Add(id, action);
                        ordered.Add(action);
                    }
            if (!owner.TryReplace(ordered, out _)) return;
            actions = next;
            observedHost = host; observedProfiles = profiles; observedEntries = entries; selected = selectedProfile;
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
