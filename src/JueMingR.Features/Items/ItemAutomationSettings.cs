using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace JueMingR.Features.Items
{
    public enum ItemActionKind { Stack, Sell, Discard }
    public enum ItemListKind { Sell, Discard }

    // The preference worker compares revisions by value and verifies codec roundtrips.
    // Never expose mutable arrays: a picker draft must not change a running revision.
    public sealed class ItemAutomationSettings : IEquatable<ItemAutomationSettings>
    {
        public static readonly ItemAutomationSettings Default = new ItemAutomationSettings(false, false, false,
            new[] { 2337, 2338, 2339 }, new int[0]);
        public bool StackEnabled { get; }
        public bool SellEnabled { get; }
        public bool DiscardEnabled { get; }
        public ReadOnlyCollection<int> SellTypes { get; }
        public ReadOnlyCollection<int> DiscardTypes { get; }
        public ItemAutomationSettings(bool stack, bool sell, bool discard, IEnumerable<int> sellTypes,
            IEnumerable<int> discardTypes)
        {
            StackEnabled = stack; SellEnabled = sell; DiscardEnabled = discard;
            SellTypes = Normalize(sellTypes); DiscardTypes = Normalize(discardTypes);
        }
        public bool Enabled(ItemActionKind action)
        { ValidateAction(action); return action == ItemActionKind.Stack ? StackEnabled : action == ItemActionKind.Sell ? SellEnabled : DiscardEnabled; }
        public ItemAutomationSettings WithEnabled(ItemActionKind action, bool enabled)
        {
            ValidateAction(action);
            return new ItemAutomationSettings(action == ItemActionKind.Stack ? enabled : StackEnabled,
                action == ItemActionKind.Sell ? enabled : SellEnabled, action == ItemActionKind.Discard ? enabled : DiscardEnabled,
                SellTypes, DiscardTypes);
        }
        public ItemAutomationSettings WithTypes(ItemListKind list, IEnumerable<int> types)
        {
            if (list != ItemListKind.Sell && list != ItemListKind.Discard) throw new ArgumentOutOfRangeException(nameof(list));
            return new ItemAutomationSettings(StackEnabled, SellEnabled, DiscardEnabled,
                list == ItemListKind.Sell ? types : SellTypes, list == ItemListKind.Discard ? types : DiscardTypes);
        }
        public static bool IsCoin(int type) { return type >= 71 && type <= 74; }
        private static ReadOnlyCollection<int> Normalize(IEnumerable<int> types)
        {
            if (types == null) throw new ArgumentNullException(nameof(types));
            var unique = new SortedSet<int>();
            foreach (int type in types)
            {
                if (type <= 0 || type > 65535) throw new ArgumentOutOfRangeException(nameof(types));
                if (!IsCoin(type)) unique.Add(type);
            }
            return Array.AsReadOnly(unique.ToArray());
        }
        private static void ValidateAction(ItemActionKind action)
        { if (action < ItemActionKind.Stack || action > ItemActionKind.Discard) throw new ArgumentOutOfRangeException(nameof(action)); }
        public bool Equals(ItemAutomationSettings other)
        {
            return other != null && StackEnabled == other.StackEnabled && SellEnabled == other.SellEnabled &&
                DiscardEnabled == other.DiscardEnabled && SellTypes.SequenceEqual(other.SellTypes) && DiscardTypes.SequenceEqual(other.DiscardTypes);
        }
        public override bool Equals(object obj) { return Equals(obj as ItemAutomationSettings); }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (StackEnabled ? 1 : 0) | (SellEnabled ? 2 : 0) | (DiscardEnabled ? 4 : 0);
                foreach (int value in SellTypes) hash = hash * 31 + value;
                hash = hash * 31 + SellTypes.Count;
                foreach (int value in DiscardTypes) hash = hash * 31 + value;
                return hash * 31 + DiscardTypes.Count;
            }
        }
    }
}
