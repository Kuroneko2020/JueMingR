using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Items;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Items
{
    // One ephemeral page draft. Settings remain owned by HostItems; acceptance
    // here means the memory command was accepted, not that async disk I/O passed.
    internal sealed class ItemSelection
    {
        private readonly HostItems host;
        private readonly HashSet<int> selected = new HashSet<int>();
        private int[] candidates = new int[0];
        internal ItemListKind? List { get; private set; }
        internal int Target { get; private set; }
        internal long Session { get; private set; }
        internal int Generation { get; private set; }
        internal bool HasInventoryTypes { get; private set; }
        internal string Message { get; private set; }
        internal bool Active { get { return List.HasValue; } }
        internal int Count { get { return selected.Count; } }
        internal IReadOnlyList<int> Candidates { get { return candidates; } }
        internal ItemSelection(HostItems host) { this.host = host; }
        internal static bool ValidType(int type) { return type > 0 && type < ItemID.Count && !ItemAutomationSettings.IsCoin(type); }
        internal static IReadOnlyList<int> Types(ItemAutomationSettings value, ItemListKind list)
        { return list == ItemListKind.Sell ? value.SellTypes : value.DiscardTypes; }
        internal bool IsSelected(int type) { return selected.Contains(type); }
        internal bool Open(ItemListKind list, int target)
        {
            if (Active && List == list && Target == target && IsCurrent) return false;
            Cancel();
            if (!host.Runtime.IsSessionActive || host.World.Player == null || target != 0 && !Types(host.Preferences.Value, list).Contains(target)) return false;
            bool available; candidates = host.PickerTypes(list, out available); HasInventoryTypes = available;
            List = list; Target = target; Session = host.Runtime.Generation; Generation++;
            return true;
        }
        internal bool IsCurrent { get { return host.Runtime.IsSessionActive && Session == host.Runtime.Generation && host.World.Player != null; } }
        internal bool ValidateSession()
        { if (!Active || IsCurrent) return true; Cancel(); return false; }
        internal void Cancel()
        { List = null; Target = 0; candidates = new int[0]; selected.Clear(); Message = null; Generation++; }
        internal bool CanSelect(int type)
        { return Active && IsCurrent && ValidType(type) && System.Array.IndexOf(candidates, type) >= 0 && !Types(host.Preferences.Value, List.Value).Contains(type); }
        internal void Select(int type)
        {
            if (!ValidateSession() || !Active) return;
            Message = null;
            if (Target != 0 && !Types(host.Preferences.Value, List.Value).Contains(Target))
            { Cancel(); Message = "原名单物品已变化，请重新选择要替换的图标。"; return; }
            if (Target == 0 && selected.Remove(type)) return;
            if (!CanSelect(type)) { Message = "该物品已在当前名单中，请选择其它物品。"; return; }
            if (Target == 0) { selected.Add(type); return; }
            var value = host.Preferences.Value;
            Commit(value.WithTypes(List.Value, Types(value, List.Value).Where(t => t != Target).Concat(new[] { type })));
        }
        internal void Confirm()
        {
            if (!ValidateSession() || !Active || Target != 0 || selected.Count == 0) return;
            // Inventory changes do not revoke a type choice. Merge with the
            // latest list and switches, never a captured preferences revision.
            var value = host.Preferences.Value;
            int[] additions = selected.Where(CanSelect).ToArray();
            if (additions.Length == 0) { Message = "已选物品均已在名单中，请调整选择或关闭。"; return; }
            Commit(value.WithTypes(List.Value, Types(value, List.Value).Concat(additions)));
        }
        private void Commit(ItemAutomationSettings value)
        { if (host.Change(value)) Cancel(); else Message = "本次名单修改未被接受，选择已保留，请重试。"; }
    }
}
