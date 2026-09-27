using System;

namespace JueMingR.Platform.Items
{
    // Game-thread owner for real overlapping operations, not a scheduler. Native
    // receipt data belongs to the Host; only this owner releases conflict ranges.
    public sealed class ItemOperationOwnership
    {
        private const ulong AllInventorySlots = (1UL << 58) - 1;
        private ulong saleSlots, discardSlots, storeSlots, interruptedSourceSlots, useSlots, coinSlots;
        private long useToken, coinToken, nextUseToken;
        // Shared by QuickItems, extraction and tools. A delayed native finalizer
        // cannot match a successor from another owner's local counter.
        public long NewUseToken(){if(nextUseToken==long.MaxValue)throw new InvalidOperationException("Use lease exhausted.");return ++nextUseToken;}
        private readonly ulong[] recoverySlots = new ulong[5];
        private long recoveryToken;
        private readonly ulong[] processingSlots = new ulong[5];
        private long processingToken;
        public long Session { get; private set; }
        public ItemOperationResult SaleResult { get; private set; }
        public ItemOperationResult DiscardResult { get; private set; }
        public ItemOperationResult StoreResult { get; private set; }
        public ulong ProtectedSlots { get { return saleSlots | discardSlots | storeSlots | interruptedSourceSlots | useSlots | coinSlots | recoverySlots[0] | processingSlots[0]; } }
        public bool AnyProtected {get{return (ProtectedSlots|recoverySlots[1]|recoverySlots[2]|recoverySlots[3]|recoverySlots[4]|processingSlots[1]|processingSlots[2]|processingSlots[3]|processingSlots[4])!=0;}}
        public bool IsProtected(int account,int slot)
        { return account>=0 && account<5 && slot>=0 && slot<(account==0?58:40) && (account==0?IsProtected(slot):((recoverySlots[account]|processingSlots[account])&(1UL<<slot))!=0); }
        // A separate owner from recovery: same finite account masks, but no
        // shared payment bypass, token, cancellation or unknown-result release.
        public bool TryBeginProcessing(long generation,ulong[] slots,long token)
        {
            if(generation<=0 || generation!=Session || token<=0 || processingToken!=0 || slots==null || slots.Length!=5)return false;
            if((slots[0]|slots[1]|slots[2]|slots[3]|slots[4])==0)return false;
            for(int a=0;a<5;a++)if((slots[a]&~((1UL<<(a==0?58:40))-1))!=0 || (slots[a]&(a==0?ProtectedSlots:recoverySlots[a]|processingSlots[a]))!=0)return false;
            for(int a=0;a<5;a++)processingSlots[a]|=slots[a];processingToken=token;return true;
        }
        public void EndProcessing(long generation,ulong[] slots,long token,bool unknown)
        {
            if(generation!=Session || token!=processingToken)return;
            if(!unknown)for(int a=0;a<5;a++)processingSlots[a]&=~slots[a];processingToken=0;
        }
        public void HoldProcessing(long generation,ulong[] slots)
        {if(generation==Session && generation>0)for(int a=0;a<5;a++)processingSlots[a]|=slots[a];}
        public bool TryBeginRecovery(long generation,ulong[] slots,long token)
        {
            if(generation<=0 || generation!=Session || token<=0 || recoveryToken!=0 || slots==null || slots.Length!=5)return false;
            if((slots[0]|slots[1]|slots[2]|slots[3]|slots[4])==0)return false;
            for(int a=0;a<5;a++)if((slots[a]&~((1UL<<(a==0?58:40))-1))!=0 || (slots[a]&(a==0?ProtectedSlots:recoverySlots[a]|processingSlots[a]))!=0)return false;
            for(int a=0;a<5;a++)recoverySlots[a]|=slots[a];recoveryToken=token;return true;
        }
        public void EndRecovery(long generation,ulong[] slots,long token,bool unknown)
        {
            if(generation!=Session || token!=recoveryToken)return;
            if(!unknown)for(int a=0;a<5;a++)recoverySlots[a]&=~slots[a];
            recoveryToken=0;
        }
        public void HoldRecovery(long generation,ulong[] slots)
        { if(generation==Session && generation>0)for(int a=0;a<5;a++)recoverySlots[a]|=slots[a]; }
        public bool TryBeginCoins(long generation, ulong slots, long token)
        {
            if (slots == 0 || (slots & ~AllInventorySlots) != 0 || token <= 0 || generation <= 0 ||
                generation != Session || coinSlots != 0 || (ProtectedSlots & slots) != 0) return false;
            coinSlots = slots; coinToken = token; return true;
        }
        public void EndCoins(long generation, long token, bool unconfirmed)
        {
            // Unknown native writes retain only this operation's real source
            // range. Toggling its preference is not a settlement or unlock.
            if (generation == Session && token == coinToken && !unconfirmed) { coinSlots = 0; coinToken = 0; }
        }
        public bool OwnsCoins(long generation, ulong slots, long token)
        {
            return generation > 0 && generation == Session && token == coinToken && slots != 0 && coinSlots == slots &&
                ((saleSlots | discardSlots | storeSlots | interruptedSourceSlots | useSlots) & slots) == 0;
        }
        public bool IsUseSlot(int slot) { return slot >= 0 && slot < 50 && (useSlots & (1UL << slot)) != 0; }
        public bool TryBeginUse(long generation, int slot, long token)
        {
            if (slot < 0 || slot >= 50 || token <= 0 || generation <= 0 || generation != Session || useSlots != 0 || IsProtected(slot)) return false;
            useSlots = 1UL << slot; useToken = token; return true;
        }
        public void EndUse(long generation, long token)
        { if (generation == Session && token == useToken) { useSlots = 0; useToken = 0; } }
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
            saleSlots = discardSlots = storeSlots = interruptedSourceSlots = useSlots = coinSlots = 0; useToken = coinToken = 0;
            Session = generation;
            Array.Clear(recoverySlots,0,recoverySlots.Length);recoveryToken=0;
            Array.Clear(processingSlots,0,processingSlots.Length);processingToken=0;
            // Old unknown results describe the ended session. A fresh session
            // has no current protected range and must not display them as live.
            if (generation > 0) SaleResult = DiscardResult = StoreResult = null;
        }
        public bool IsProtected(int slot)
        { return slot >= 0 && slot < 58 && (ProtectedSlots & (1UL << slot)) != 0; }
        public void HoldInterruptedSource(long generation, ulong slots)
        {
            if ((slots & ~AllInventorySlots) != 0) throw new ArgumentOutOfRangeException(nameof(slots));
            if (generation == Session && generation > 0) interruptedSourceSlots |= slots;
        }
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
