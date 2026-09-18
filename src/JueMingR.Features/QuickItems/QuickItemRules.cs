using System;
using System.Collections.Generic;

namespace JueMingR.Features.QuickItems
{
    public enum QuickItemMode { Use, SetState, SetStateAndUse }
    public sealed class QuickItemEntry
    {
        public string Id { get; }
        public int Target { get; }
        public QuickItemMode Mode { get; }
        public bool Compatible { get; }
        public bool Enabled { get; }
        public QuickItemEntry(string id, int target, QuickItemMode mode, bool compatible, bool enabled)
        {
            Guid parsed;
            if (!Guid.TryParseExact(id, "N", out parsed) || parsed == Guid.Empty || id != parsed.ToString("N") ||
                target <= 0 || target >= 65536 || target == 5437 || mode < QuickItemMode.Use || mode > QuickItemMode.SetStateAndUse ||
                mode != QuickItemMode.Use && !QuickItemRules.HasState(target)) throw new ArgumentException("Invalid quick item entry.");
            Id = id; Target = target; Mode = mode; Compatible = compatible; Enabled = enabled;
        }
        public string ActionId { get { return "items.quick-use." + Id; } }
        public QuickItemEntry With(int target, QuickItemMode mode, bool compatible, bool enabled)
        { return new QuickItemEntry(Id, target, mode, compatible, enabled); }
    }
    public struct QuickItemCandidate
    {
        public int Slot, Type, Stack;
        public bool Usable, Protected;
        public QuickItemCandidate(int slot, int type, int stack, bool usable, bool protectedSlot = false)
        { Slot = slot; Type = type; Stack = stack; Usable = usable; Protected = protectedSlot; }
    }
    public struct QuickItemChoice
    {
        public int Slot, OriginalType, TargetType;
        public bool Use;
        public bool Found { get { return Slot >= 0; } }
        public static QuickItemChoice Missing { get { return new QuickItemChoice { Slot = -1 }; } }
    }
    public static class QuickItemRules
    {
        // Directed vanilla 1.4.5.8 TryItemSwap edges. A family describes the
        // same physical object, never an authorization to convert providers.
        private static readonly int[][] states = {
            new[] {2611,5526}, new[] {4131,5325}, new[] {4346,5391}, new[] {4767,5453},
            new[] {5059,5060}, new[] {5309,5454}, new[] {5323,5455}, new[] {5324,5329,5330},
            new[] {5358,5360,5361,5359}, new[] {6168,6169,6193,6194}, new[] {6190,6195}
        };
        private static readonly int[][] purposes = { new[] {50,3199,3124,5358}, new[] {4263,5360}, new[] {4819,5361}, new[] {5359} };
        public static bool HasState(int type) { return Family(type) >= 0; }
        public static int NextState(int type)
        {
            if (type == 5437) return 5358; // Internal creation state; never a bindable target.
            foreach (var family in states) for (int i = 0; i < family.Length; i++) if (family[i] == type) return family[(i + 1) % family.Length];
            return 0;
        }
        public static bool SamePhysicalFamily(int a, int b) { int family = Family(a==5437?5358:a); return a == b || family >= 0 && family == Family(b==5437?5358:b); }
        public static QuickItemMode DefaultMode(int type)
        { return Family(type) == 8 ? QuickItemMode.SetStateAndUse : HasState(type) ? QuickItemMode.SetState : QuickItemMode.Use; }
        private static int Family(int type)
        { for (int i = 0; i < states.Length; i++) foreach (int value in states[i]) if (value == type) return i; return -1; }
        private static int[] Purpose(int target)
        { foreach (var group in purposes) foreach (int value in group) if (value == target) return group; return null; }
        public static QuickItemChoice Choose(QuickItemEntry entry, IReadOnlyList<QuickItemCandidate> candidates, int selected)
        {
            var result = QuickItemChoice.Missing; long best = Int64.MaxValue;
            if (entry == null || !entry.Enabled) return result;
            int[] group = entry.Compatible && entry.Mode != QuickItemMode.SetState ? Purpose(entry.Target) : null;
            foreach (var item in candidates)
            {
                if (item.Slot < 0 || item.Slot >= 50 || item.Stack <= 0 || item.Type <= 0 || item.Protected ||
                    entry.Mode != QuickItemMode.SetState && !item.Usable && item.Type!=5437) continue;
                int target = entry.Target, provider = 0;
                bool preferred = SamePhysicalFamily(item.Type, entry.Target);
                if (!preferred)
                {
                    if (group == null) continue;
                    bool match = false;
                    for (int i = 0; i < group.Length; i++) if (SamePhysicalFamily(item.Type, group[i]))
                    { target = group[i]; provider = i; match = true; break; }
                    if (!match) continue;
                }
                // Use-only targets also retain the requested real state. State
                // changes require the vanilla singleton condition, not a stack edit.
                if (item.Type != target && (!SamePhysicalFamily(item.Type, target) || item.Stack != 1)) continue;
                int slotRank = item.Slot == selected ? 0 : item.Slot + 1;
                long rank = (preferred ? 0L : 1000000L) + (item.Type == target ? 0L : 100000L) + provider * 1000L + slotRank;
                if (rank >= best) continue;
                best = rank; result = new QuickItemChoice { Slot = item.Slot, OriginalType = item.Type, TargetType = target, Use = entry.Mode != QuickItemMode.SetState };
            }
            return result;
        }
    }
}
