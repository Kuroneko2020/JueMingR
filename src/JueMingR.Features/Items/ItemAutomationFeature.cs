using System;
using System.Collections.Generic;
using JueMingR.Platform.Items;
using JueMingR.Platform.Runtime;

namespace JueMingR.Features.Items
{
    // This owner knows business priority and finite acquisition intentions. The
    // operation port owns actual conflicts/receipts, including its wider sale range.
    public sealed class ItemAutomationFeature : IRuntimeFeature
    {
        private readonly IItemObservationSource source;
        private readonly IItemOperationPort operations;
        private readonly Dictionary<ItemIdentity, ulong> acquisitions = new Dictionary<ItemIdentity, ulong>();
        private readonly HashSet<int> sellTypes = new HashSet<int>(), discardTypes = new HashSet<int>();
        private readonly long[] attemptedRevision = new long[58];
        private ItemAutomationSettings settings = ItemAutomationSettings.Default;
        private bool active, immediate = true, hasTick;
        private ulong lastTick;
        private long session;
        public bool HasFailed { get; private set; }
        public bool Enabled { get { return !HasFailed && (settings.StackEnabled || settings.SellEnabled || settings.DiscardEnabled); } }
        public ItemAutomationFeature(IItemObservationSource source, IItemOperationPort operations)
        { this.source = source ?? throw new ArgumentNullException(nameof(source)); this.operations = operations ?? throw new ArgumentNullException(nameof(operations)); }
        public ItemOperationResult LastResult(ItemActionKind action)
        {
            ItemAutomationSettings.Default.Enabled(action);
            return action == ItemActionKind.Sell ? operations.Ownership.SaleResult :
                action == ItemActionKind.Discard ? operations.Ownership.DiscardResult : operations.Ownership.StoreResult;
        }
        public void Configure(ItemAutomationSettings value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (settings.Equals(value)) return;
            settings = value;
            sellTypes.Clear(); discardTypes.Clear();
            foreach (int type in value.SellTypes) sellTypes.Add(type);
            foreach (int type in value.DiscardTypes) discardTypes.Add(type);
            if (!value.StackEnabled) acquisitions.Clear();
            ResetAttempts(); immediate = true;
        }
        public void OnSessionStarted()
        { session = source.SessionGeneration; active = true; acquisitions.Clear(); ResetAttempts(); hasTick = false; immediate = true; }
        public void OnSessionEnded()
        { active = false; acquisitions.Clear(); ResetAttempts(); }
        public void FailClosed() { HasFailed = true; active = false; acquisitions.Clear(); }

        // Only the Host's completed causal scope calls this. Positive inventory
        // polling, settings changes and ordinary GetItem calls never call it.
        public void RegisterAcquisition(ItemIdentity identity, long generation, ulong tick)
        {
            if (!active || HasFailed || !settings.StackEnabled || generation != session || identity.Type <= 0 || ItemAutomationSettings.IsCoin(identity.Type)) return;
            if (!acquisitions.ContainsKey(identity))
            {
                // A stalled UI cannot turn high-rate acquisition into an unbounded
                // journal. Overflow is a visible failure, not a fabricated origin.
                if (acquisitions.Count == 128) { FailClosed(); return; }
                acquisitions.Add(identity, tick);
            }
            // Repeated acquisition coalesces evaluation, never extends the first
            // unresolved intention's lifetime into a permanent type permission.
            immediate = true;
        }
        public void Update(ulong tick)
        {
            if (!active || !Enabled || source.SessionGeneration != session) return;
            if (!immediate && hasTick && unchecked(tick - lastTick) < 6) return;
            immediate = false; hasTick = true; lastTick = tick;
            ItemInventoryObservation inventory;
            if (!source.TryObserve(out inventory) || inventory == null || inventory.Session != session) return;
            PruneAcquisitions(inventory, tick);
            foreach (ItemSlotObservation slot in inventory.Slots)
            {
                if (!slot.IsCandidate || operations.Ownership.IsProtected(slot.Slot) || attemptedRevision[slot.Slot] == inventory.Revision) continue;
                ItemOperationResult result = null;
                // Re-evaluate current shop and flags at the same decision point.
                // A disabled/closed sale never creates a reservation against trash.
                if (settings.SellEnabled && inventory.ShopAvailable && sellTypes.Contains(slot.Identity.Type))
                {
                    if (operations.Ownership.SaleBlocked) continue;
                    result = operations.Execute(new SellItemRequest(session, inventory.ShopIdentity, slot));
                }
                // Only a proved not-started result permits lower priority. Apply
                // this rule to every step, including a shop closing at admission.
                if ((result == null || result.State == ItemOperationState.NotApplicable) &&
                    settings.DiscardEnabled && discardTypes.Contains(slot.Identity.Type))
                {
                    if (operations.Ownership.DiscardBlocked) continue;
                    result = operations.Execute(new DiscardItemRequest(session, slot));
                }
                if ((result == null || result.State == ItemOperationState.NotApplicable) &&
                    settings.StackEnabled && slot.MaximumStack > 1 && acquisitions.ContainsKey(slot.Identity))
                {
                    if (operations.Ownership.StoreBlocked) continue;
                    var group = new List<ItemSlotObservation>();
                    foreach (ItemSlotObservation candidate in inventory.Slots)
                        if (candidate.IsCandidate && !operations.Ownership.IsProtected(candidate.Slot) &&
                            candidate.MaximumStack > 1 && candidate.Identity.Equals(slot.Identity)) group.Add(candidate);
                    result = operations.Execute(new StoreItemsRequest(session, group));
                    // Rejected admission has not consumed a source opportunity.
                    // Executing or a terminal/no-target result does; actual late
                    // receipts remain the operation owner's responsibility.
                    if (result.State != ItemOperationState.Rejected) acquisitions.Remove(slot.Identity);
                }
                if (result == null) continue;
                attemptedRevision[slot.Slot] = inventory.Revision;
                // A single sale/trash only handles that slot. Other compatible
                // stacks retain the finite source until the group is absent,
                // stored, cancelled or expired; PruneAcquisitions checks absence.
                return; // One admitted attempt per observation; no stale batch walks.
            }
        }
        private void PruneAcquisitions(ItemInventoryObservation inventory, ulong tick)
        {
            if (acquisitions.Count == 0) return;
            var expired = new List<ItemIdentity>();
            foreach (KeyValuePair<ItemIdentity, ulong> entry in acquisitions)
            {
                bool present = false;
                foreach (ItemSlotObservation slot in inventory.Slots)
                    if (slot.Stack > 0 && slot.Identity.Equals(entry.Key)) { present = true; break; }
                if (!present || unchecked(tick - entry.Value) >= 600) expired.Add(entry.Key);
            }
            foreach (ItemIdentity key in expired) acquisitions.Remove(key);
        }
        private void ResetAttempts() { for (int i = 0; i < attemptedRevision.Length; i++) attemptedRevision[i] = Int64.MinValue; }
    }
}
