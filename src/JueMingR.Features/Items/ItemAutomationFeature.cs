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
        private readonly Dictionary<ItemIdentity, ItemAcquisitionOpportunity> acquisitions = new Dictionary<ItemIdentity, ItemAcquisitionOpportunity>();
        private readonly Func<bool> canStartActions;
        private readonly HashSet<int> sellTypes = new HashSet<int>(), discardTypes = new HashSet<int>();
        private readonly long[] attemptedRevision = new long[58];
        private ItemAutomationSettings settings = ItemAutomationSettings.Default;
        private bool active, immediate = true, hasTick;
        private ulong lastTick;
        private long session;
        public bool HasFailed { get; private set; }
        public bool Enabled { get { return !HasFailed && (settings.StackEnabled || settings.SellEnabled || settings.DiscardEnabled); } }
        public ItemAutomationFeature(IItemObservationSource source, IItemOperationPort operations, Func<bool> canStartActions = null)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.canStartActions = canStartActions ?? (() => true);
        }
        public ItemOperationResult LastResult(ItemActionKind action)
        {
            ItemAutomationSettings.Default.Enabled(action);
            return action == ItemActionKind.Sell ? operations.Ownership.SaleResult :
                action == ItemActionKind.Discard ? operations.Ownership.DiscardResult : operations.Ownership.StoreResult;
        }
        public void Configure(ItemAutomationSettings value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            bool changed = !settings.HasSameAutomationRules(value);
            settings = value;
            // Presentation-only preferences share the document, not the business
            // retry epoch. They must not revive attempts or cancel acquisitions.
            if (!changed) return;
            sellTypes.Clear(); discardTypes.Clear();
            foreach (int type in value.SellTypes) sellTypes.Add(type);
            foreach (int type in value.DiscardTypes) discardTypes.Add(type);
            acquisitions.Clear();
            ResetAttempts(); immediate = true;
        }
        public void OnSessionStarted()
        { session = source.SessionGeneration; active = true; acquisitions.Clear(); ResetAttempts(); hasTick = false; immediate = true; }
        public void OnSessionEnded()
        { active = false; acquisitions.Clear(); ResetAttempts(); }
        public void FailClosed() { HasFailed = true; active = false; acquisitions.Clear(); }

        // Only the Host's completed causal scope calls this. Positive inventory
        // polling, settings changes and ordinary GetItem calls never call it.
        public void RegisterAcquisitions(IEnumerable<ItemIdentity> identities, ItemInventoryObservation inventory, ulong tick)
        {
            if (identities == null) throw new ArgumentNullException(nameof(identities));
            if (!active || !Enabled || inventory == null || inventory.Session != session) return;
            PruneAcquisitions(null, tick);
            // Capture the whole causal batch before reconciling old members: two
            // products changing together must not reset each other's first age.
            foreach (ItemIdentity identity in identities)
            {
                if (identity.Type <= 0 || ItemAutomationSettings.IsCoin(identity.Type)) continue;
                ItemAcquisitionOpportunity opportunity;
                if (!acquisitions.TryGetValue(identity, out opportunity))
                {
                    // A stalled UI cannot turn high-rate acquisition into an unbounded
                    // journal. Overflow is a visible failure, not a fabricated origin.
                    if (acquisitions.Count == 128) { FailClosed(); return; }
                    opportunity = new ItemAcquisitionOpportunity(tick);
                    acquisitions.Add(identity, opportunity);
                }
                // Repeated acquisition coalesces evaluation, never extends the first
                // unresolved intention's lifetime into a permanent type permission.
                opportunity.Capture(identity, inventory);
                if (opportunity.Count == 0) acquisitions.Remove(identity);
                foreach (ItemSlotObservation slot in inventory.Slots)
                    if (slot.Identity.Equals(identity) && slot.Slot >= 0 && slot.Slot < 58) attemptedRevision[slot.Slot] = Int64.MinValue;
            }
            PruneAcquisitions(inventory, tick);
            immediate = true;
        }
        public void Update(ulong tick)
        {
            if (!active || !Enabled || source.SessionGeneration != session) return;
            PruneAcquisitions(null, tick);
            if (acquisitions.Count == 0 || !canStartActions()) return;
            if (!immediate && hasTick && unchecked(tick - lastTick) < 6) return;
            immediate = false; hasTick = true; lastTick = tick;
            ItemInventoryObservation inventory;
            if (!source.TryObserve(out inventory) || inventory == null || inventory.Session != session) return;
            PruneAcquisitions(inventory, tick);
            foreach (ItemSlotObservation slot in inventory.Slots)
            {
                ItemAcquisitionOpportunity opportunity;
                if (!acquisitions.TryGetValue(slot.Identity, out opportunity) || !opportunity.Contains(slot)) continue;
                if (!slot.IsCandidate || operations.Ownership.IsProtected(slot.Slot) || attemptedRevision[slot.Slot] == inventory.Revision) continue;
                // Requests execute synchronously on the game thread. Recheck this
                // owner's current source/rules here; the Host then rechecks the
                // same input permission and live resource identity at admission.
                if (!canStartActions()) return;
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
                    settings.StackEnabled && slot.MaximumStack > 1)
                {
                    if (operations.Ownership.StoreBlocked) continue;
                    var group = new List<ItemSlotObservation>();
                    foreach (ItemSlotObservation candidate in inventory.Slots)
                        if (candidate.IsCandidate && !operations.Ownership.IsProtected(candidate.Slot) &&
                            candidate.MaximumStack > 1 && opportunity.Contains(candidate)) group.Add(candidate);
                    result = operations.Execute(new StoreItemsRequest(session, group));
                    // Rejected admission has not consumed a source opportunity.
                    // Executing or a terminal/no-target result does; actual late
                    // receipts remain the operation owner's responsibility.
                    if (result.State != ItemOperationState.Rejected)
                        foreach (ItemSlotObservation submitted in group) opportunity.Retire(submitted);
                }
                // A submitted/finished stack cannot be replaced by a subsequent
                // withdrawal before the next six-tick observation. Unknown native
                // results retain their separate resource ownership, not permission
                // to submit that member again. A favorite-only group ends too.
                if (result == null || result.State != ItemOperationState.Rejected) opportunity.Retire(slot);
                if (opportunity.Count == 0) acquisitions.Remove(slot.Identity);
                if (result == null) continue;
                attemptedRevision[slot.Slot] = inventory.Revision;
                return; // One admitted attempt per observation; no stale batch walks.
            }
        }
        private void PruneAcquisitions(ItemInventoryObservation inventory, ulong tick)
        {
            if (acquisitions.Count == 0) return;
            var expired = new List<ItemIdentity>();
            foreach (KeyValuePair<ItemIdentity, ItemAcquisitionOpportunity> entry in acquisitions)
            {
                if (inventory != null) entry.Value.Reconcile(inventory);
                if (entry.Value.Count == 0 || unchecked(tick - entry.Value.Started) >= 600) expired.Add(entry.Key);
            }
            foreach (ItemIdentity key in expired) acquisitions.Remove(key);
        }
        private void ResetAttempts() { for (int i = 0; i < attemptedRevision.Length; i++) attemptedRevision[i] = Int64.MinValue; }
    }
}
