using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JueMingR.Platform.Items
{
    public readonly struct ItemIdentity : IEquatable<ItemIdentity>
    {
        public int Type { get; }
        public byte Prefix { get; }
        public ItemIdentity(int type, byte prefix) { Type = type; Prefix = prefix; }
        public bool Equals(ItemIdentity other) { return Type == other.Type && Prefix == other.Prefix; }
        public override bool Equals(object obj) { return obj is ItemIdentity && Equals((ItemIdentity)obj); }
        public override int GetHashCode() { return unchecked(Type * 397 ^ Prefix); }
    }

    public readonly struct ItemSlotObservation
    {
        public int Slot { get; }
        public ItemIdentity Identity { get; }
        public int Stack { get; }
        public int MaximumStack { get; }
        public long Instance { get; }
        public bool Protected { get; }
        public ItemSlotObservation(int slot, ItemIdentity identity, int stack, int maximumStack, long instance, bool isProtected)
        { Slot = slot; Identity = identity; Stack = stack; MaximumStack = maximumStack; Instance = instance; Protected = isProtected; }
        public bool IsCandidate
        { get { return (Slot >= 0 && Slot < 50 || Slot >= 54 && Slot < 58) && Stack > 0 && Identity.Type > 0 &&
                    (Identity.Type < 71 || Identity.Type > 74) && !Protected; } }
    }

    public sealed class ItemInventoryObservation
    {
        public long Session { get; }
        public long Revision { get; }
        public bool ShopAvailable { get; }
        public long ShopIdentity { get; }
        public ReadOnlyCollection<ItemSlotObservation> Slots { get; }
        public ItemInventoryObservation(long session, long revision, bool shopAvailable, long shopIdentity, IEnumerable<ItemSlotObservation> slots)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            var copy = new List<ItemSlotObservation>();
            foreach (ItemSlotObservation slot in slots) { if (copy.Count == 58) throw new ArgumentException("inventory-observation-too-large"); copy.Add(slot); }
            Session = session; Revision = revision; ShopAvailable = shopAvailable; ShopIdentity = shopIdentity;
            Slots = copy.AsReadOnly();
        }
    }

    public interface IItemObservationSource
    {
        long SessionGeneration { get; }
        bool TryObserve(out ItemInventoryObservation observation);
    }
}
