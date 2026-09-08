using System;

namespace JueMingR.Platform.Items
{
    // Game-thread owner for these three real operations, not a scheduler. Native
    // receipt data belongs to the Host; only this owner releases conflict ranges.
    public sealed class ItemOperationOwnership
    {
        private const ulong AllInventorySlots = (1UL << 58) - 1;
        private ulong saleSlots, discardSlots, storeSlots;
        public long Session { get; private set; }
        public ItemOperationResult SaleResult { get; private set; }
        public ItemOperationResult DiscardResult { get; private set; }
        public ItemOperationResult StoreResult { get; private set; }
        public ulong ProtectedSlots { get { return saleSlots | discardSlots | storeSlots; } }
        public bool SaleBlocked { get { return ProtectedSlots != 0; } }
        public bool DiscardBlocked { get { return discardSlots != 0; } }
        public bool StoreBlocked { get { return storeSlots != 0; } }

        public void SetSession(long generation)
        {
            if (generation == Session) return;
            // A new native session ends interpretation, not the vanilla transfer.
            // No inventory/lock is restored or cleared by this neutral owner.
            if (saleSlots != 0) SaleResult = Unknown();
            if (discardSlots != 0) DiscardResult = Unknown();
            if (storeSlots != 0) StoreResult = Unknown();
            saleSlots = discardSlots = storeSlots = 0;
            Session = generation;
        }
        public bool IsProtected(int slot)
        { return slot >= 0 && slot < 58 && (ProtectedSlots & (1UL << slot)) != 0; }
        public bool TryBeginSale(long generation)
        {
            if (generation != Session || generation <= 0 || SaleBlocked) return false;
            saleSlots = AllInventorySlots;
            SaleResult = new ItemOperationResult(ItemOperationState.Executing); return true;
        }
        public bool TryBeginDiscard(long generation, int slot)
        {
            ulong mask = SlotMask(slot);
            if (generation != Session || generation <= 0 || DiscardBlocked || (ProtectedSlots & mask) != 0) return false;
            discardSlots = mask;
            DiscardResult = new ItemOperationResult(ItemOperationState.Executing); return true;
        }
        public bool TryBeginStore(long generation, ulong slots)
        {
            if (slots == 0 || (slots & ~AllInventorySlots) != 0) throw new ArgumentOutOfRangeException(nameof(slots));
            if (generation != Session || generation <= 0 || StoreBlocked || (ProtectedSlots & slots) != 0) return false;
            storeSlots = slots;
            StoreResult = new ItemOperationResult(ItemOperationState.Executing); return true;
        }
        public void FinishSale(long generation, ItemOperationResult result)
        { if (generation == Session && saleSlots != 0) { SaleResult = Validate(result); if (!RetainsOwnership(result.State)) saleSlots = 0; } }
        public void FinishDiscard(long generation, ItemOperationResult result)
        { if (generation == Session && discardSlots != 0) { DiscardResult = Validate(result); if (!RetainsOwnership(result.State)) discardSlots = 0; } }
        public void FinishStore(long generation, ItemOperationResult result)
        { if (generation == Session && storeSlots != 0) { StoreResult = Validate(result); if (!RetainsOwnership(result.State)) storeSlots = 0; } }
        private static bool RetainsOwnership(ItemOperationState state)
        {
            return state == ItemOperationState.Executing || state == ItemOperationState.Unconfirmed ||
                state == ItemOperationState.TimedOut || state == ItemOperationState.Failed;
        }
        private static ItemOperationResult Validate(ItemOperationResult result)
        { return result ?? throw new ArgumentNullException(nameof(result)); }
        private static ItemOperationResult Unknown()
        { return new ItemOperationResult(ItemOperationState.Unconfirmed, reason: "session-ended-before-confirmation"); }
        private static ulong SlotMask(int slot)
        { if (slot < 0 || slot >= 58) throw new ArgumentOutOfRangeException(nameof(slot)); return 1UL << slot; }
    }
}
