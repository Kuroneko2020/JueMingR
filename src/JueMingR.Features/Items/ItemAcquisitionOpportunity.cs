using System.Collections.Generic;
using JueMingR.Platform.Items;

namespace JueMingR.Features.Items
{
    // Values owned only by ItemAutomationFeature. A causal acquisition can admit
    // current whole stacks; ordinary observations only remove members. One
    // proven local two-slot transfer may map an existing member, never recapture.
    // No unit history, slot reservation or permanent type permission lives here.
    internal sealed class ItemAcquisitionOpportunity
    {
        internal ulong Started { get; }
        private readonly List<ItemSlotObservation> members = new List<ItemSlotObservation>();
        internal int Count { get { return members.Count; } }
        internal ItemAcquisitionOpportunity(ulong tick) { Started = tick; }
        internal void Capture(ItemIdentity identity, ItemInventoryObservation inventory)
        {
            members.Clear();
            foreach (ItemSlotObservation slot in inventory.Slots)
                if (slot.IsCandidate && slot.Identity.Equals(identity)) members.Add(slot);
        }
        internal void Reconcile(ItemInventoryObservation inventory)
        {
            for (int i = members.Count - 1; i >= 0; i--)
            {
                bool present = false;
                foreach (ItemSlotObservation current in inventory.Slots)
                    if (Matches(members[i], current)) { present = true; break; }
                if (!present) members.RemoveAt(i);
            }
        }
        internal bool Contains(ItemSlotObservation slot)
        {
            foreach (ItemSlotObservation member in members) if (Matches(member, slot)) return true;
            return false;
        }
        internal void Retire(ItemSlotObservation slot)
        {
            for (int i = members.Count - 1; i >= 0; i--)
                if (members[i].Slot == slot.Slot && members[i].Instance == slot.Instance) members.RemoveAt(i);
        }
        internal bool MapTransfer(ItemSlotObservation source, ItemSlotObservation target, ItemSlotObservation remaining, ItemSlotObservation combined)
        {
            if (!Contains(source)) return false;
            Retire(source); Retire(target);
            if (combined.IsCandidate) members.Add(combined);
            if (remaining.IsCandidate) members.Add(remaining);
            return true;
        }
        private static bool Matches(ItemSlotObservation member, ItemSlotObservation current)
        {
            return current.IsCandidate && member.Slot == current.Slot && member.Instance == current.Instance &&
                member.Identity.Equals(current.Identity) && member.Stack == current.Stack && member.MaximumStack == current.MaximumStack;
        }
    }
}
