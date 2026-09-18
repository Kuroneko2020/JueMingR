using System;
using System.Collections.Generic;

namespace JueMingR.Platform.Hotkeys
{
    [Flags]
    public enum HotkeyContext { None = 0, SinglePlayer = 1, Multiplayer = 2, Gameplay = 3 }
    public sealed class HotkeyAction
    {
        public string Id { get; }
        public string Name { get; }
        public HotkeyContext Context { get; }
        private readonly Func<bool> available;
        private readonly Action command;
        private readonly Action<HotkeyChord> gestureCommand;
        private readonly Func<bool> configurable;
        public bool CanConfigure {get{return configurable==null || configurable();}}
        internal Func<bool> RegistrationIsCurrent;
        public HotkeyAction(string id, string name, HotkeyContext context, Func<bool> available, Action command, Func<bool> configurable = null)
        {
            if (!ValidId(id) || String.IsNullOrWhiteSpace(name) || name.Length > 96 || context == HotkeyContext.None || ((int)context & ~3) != 0)
                throw new ArgumentException("Invalid hotkey action identity/context.");
            Id = id; Name = name; Context = context; this.available = available ?? throw new ArgumentNullException(nameof(available));
            this.command = command ?? throw new ArgumentNullException(nameof(command));
            this.configurable=configurable;
        }
        public HotkeyAction(string id, string name, HotkeyContext context, Func<bool> available, Action<HotkeyChord> command, Func<bool> configurable = null)
            : this(id, name, context, available, () => { }, configurable)
        { gestureCommand = command ?? throw new ArgumentNullException(nameof(command)); }
        // Pass the immutable matched binding, never a later lookup of settings.
        // Gesture consumers cannot be invoked without a real dispatch identity.
        public bool Invoke(HotkeyContext context, HotkeyChord gesture = null)
        {
            if ((Context & context) == 0 || RegistrationIsCurrent != null && !RegistrationIsCurrent() || !available() || gestureCommand != null && gesture == null) return false;
            if (gestureCommand != null) gestureCommand(gesture); else command();
            return true;
        }
        public static bool ValidId(string id)
        { if (String.IsNullOrEmpty(id) || id.Length > 96) return false; foreach (char c in id) if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '.' || c == '-')) return false; return true; }
    }
    public sealed class HotkeyRegistry
    {
        private Dictionary<string, HotkeyAction> actions = new Dictionary<string, HotkeyAction>(StringComparer.Ordinal);
        private readonly List<HotkeyAction> core = new List<HotkeyAction>();
        private readonly List<DynamicHotkeyOwner> owners = new List<DynamicHotkeyOwner>();
        private bool frozen;
        public IReadOnlyList<HotkeyAction> Actions { get; private set; } = new HotkeyAction[0];
        public long Revision { get; private set; }
        public void Register(HotkeyAction action)
        {
            if (frozen || action == null || actions.ContainsKey(action.Id) || actions.Count >= 256) throw new InvalidOperationException("Duplicate, late or excessive hotkey registration.");
            foreach (var owner in owners) if (owner.Owns(action.Id)) throw new InvalidOperationException("Reserved dynamic action namespace.");
            core.Add(action); Publish(null, null);
        }
        // Created only at composition time. The capability reserves a namespace;
        // it never permits reopening, replacing or deleting the frozen core.
        public DynamicHotkeyOwner CreateDynamicOwner(string prefix)
        {
            if (frozen || !HotkeyAction.ValidId(prefix) || !prefix.EndsWith(".", StringComparison.Ordinal) || prefix.Length > 64)
                throw new InvalidOperationException("Invalid or late dynamic owner.");
            foreach (var owner in owners)
                if (owner.Prefix.StartsWith(prefix, StringComparison.Ordinal) || prefix.StartsWith(owner.Prefix, StringComparison.Ordinal))
                    throw new InvalidOperationException("Overlapping dynamic owners.");
            foreach (var action in core) if (action.Id.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Core namespace already used.");
            var result = new DynamicHotkeyOwner(this, prefix); owners.Add(result); return result;
        }
        internal bool Replace(DynamicHotkeyOwner owner, IEnumerable<HotkeyAction> values, out string reason)
        {
            reason = null; var copy = new List<HotkeyAction>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            int otherCount = core.Count; foreach (var other in owners) if (other != owner) otherCount += other.Current.Count;
            foreach (var action in values)
            {
                if (action == null || !owner.Owns(action.Id) || !ids.Add(action.Id) || copy.Count + otherCount >= 256)
                { reason = "快捷动作无效、重复或已达到数量上限。"; return false; }
                copy.Add(action);
            }
            bool same = copy.Count == owner.Current.Count;
            for (int i = 0; same && i < copy.Count; i++) same = ReferenceEquals(copy[i], owner.Current[i]);
            if (!same) Publish(owner, copy.AsReadOnly());
            return true;
        }
        private void Publish(DynamicHotkeyOwner changed, IReadOnlyList<HotkeyAction> replacement)
        {
            var next = new Dictionary<string, HotkeyAction>(StringComparer.Ordinal); var order = new List<HotkeyAction>(core);
            foreach (var owner in owners) order.AddRange(owner == changed ? replacement : owner.Current);
            foreach (var action in order) next.Add(action.Id, action);
            if (changed != null) changed.Current = replacement;
            actions = next; Actions = order.AsReadOnly(); Revision++;
            foreach (var action in order)
                if (action.RegistrationIsCurrent == null) action.RegistrationIsCurrent = () => ReferenceEquals(Find(action.Id), action);
        }
        public void Freeze() { frozen = true; }
        public HotkeyAction Find(string id) { HotkeyAction result; return id != null && actions.TryGetValue(id, out result) ? result : null; }
    }
    public sealed class DynamicHotkeyOwner
    {
        internal readonly HotkeyRegistry Registry;
        internal readonly string Prefix;
        internal IReadOnlyList<HotkeyAction> Current = new HotkeyAction[0];
        internal DynamicHotkeyOwner(HotkeyRegistry registry, string prefix) { Registry = registry; Prefix = prefix; }
        public bool Owns(string id) { return id != null && id.Length > Prefix.Length && id.StartsWith(Prefix, StringComparison.Ordinal); }
        public bool TryReplace(IEnumerable<HotkeyAction> actions, out string reason)
        { if (actions == null) throw new ArgumentNullException(nameof(actions)); return Registry.Replace(this, actions, out reason); }
    }
}
