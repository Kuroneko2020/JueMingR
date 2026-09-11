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
        public HotkeyAction(string id, string name, HotkeyContext context, Func<bool> available, Action command)
        {
            if (!ValidId(id) || String.IsNullOrWhiteSpace(name) || name.Length > 96 || context == HotkeyContext.None || ((int)context & ~3) != 0)
                throw new ArgumentException("Invalid hotkey action identity/context.");
            Id = id; Name = name; Context = context; this.available = available ?? throw new ArgumentNullException(nameof(available));
            this.command = command ?? throw new ArgumentNullException(nameof(command));
        }
        public bool Invoke(HotkeyContext context) { if ((Context & context) == 0 || !available()) return false; command(); return true; }
        public static bool ValidId(string id)
        { if (String.IsNullOrEmpty(id) || id.Length > 96) return false; foreach (char c in id) if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '.' || c == '-')) return false; return true; }
    }
    public sealed class HotkeyRegistry
    {
        private readonly Dictionary<string, HotkeyAction> actions = new Dictionary<string, HotkeyAction>(StringComparer.Ordinal);
        private readonly List<HotkeyAction> ordered = new List<HotkeyAction>();
        private bool frozen;
        public IReadOnlyList<HotkeyAction> Actions { get; }
        public HotkeyRegistry() { Actions = ordered.AsReadOnly(); }
        public void Register(HotkeyAction action)
        {
            if (frozen || action == null || actions.ContainsKey(action.Id) || ordered.Count >= 256) throw new InvalidOperationException("Duplicate, late or excessive hotkey registration.");
            actions.Add(action.Id, action); ordered.Add(action);
        }
        public void Freeze() { frozen = true; }
        public HotkeyAction Find(string id) { HotkeyAction result; return id != null && actions.TryGetValue(id, out result) ? result : null; }
    }
}
