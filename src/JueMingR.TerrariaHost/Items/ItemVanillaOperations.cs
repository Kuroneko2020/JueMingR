using System;
using System.Reflection;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Items
{
    internal sealed class ItemVanillaOperations : IItemOperationPort
    {
        private delegate bool SlotOverride(Item[] items, int context, int slot);
        private readonly ItemHostObservation world;
        private SlotOverride slotOverride;
        internal Func<StoreItemsRequest, ItemOperationResult> Store { get; set; }
        public ItemOperationOwnership Ownership { get; }
        internal ItemVanillaOperations(ItemHostObservation world, ItemOperationOwnership ownership)
        {
            this.world = world; Ownership = ownership;
        }
        internal void BindNativeOperation()
        {
            MethodInfo method = typeof(ItemSlot).GetMethod("OverrideLeftClick", BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(Item[]), typeof(int), typeof(int) }, null);
            if (method == null || method.ReturnType != typeof(bool)) throw new MissingMethodException("item-slot-operation-abi");
            slotOverride = (SlotOverride)Delegate.CreateDelegate(typeof(SlotOverride), method);
        }
        public ItemOperationResult Execute(SellItemRequest request)
        {
            if (request.Session != world.SessionGeneration || !world.MatchesShop(request.ShopIdentity)) return Result(ItemOperationState.NotApplicable, "shop-closed-or-changed");
            Player player = world.Player;
            if (player == null || player.HasLockedInventory() || world.HasManualOperation || player.itemAnimation > 0 || player.itemTime > 0 ||
                player.tileEntityAnchor.IsInValidUseTileEntity() || !world.Matches(request.Source)) return Result(ItemOperationState.Rejected, "sale-source-or-range-busy");
            Chest shop; NPC npc;
            if (!world.TryShop(out shop, out npc)) return Result(ItemOperationState.NotApplicable, "shop-closed");
            Item original = player.inventory[request.Source.Slot].Clone();
            long selling, buying;
            player.GetItemExpectedPrice(original, out selling, out buying);
            int boughtBack = Math.Min(Main.shopSellbackHelper.GetAmount(original), original.stack);
            long unit = Math.Max(1, selling / 5);
            long expectedMoney = selling <= 0 ? 0 : checked(unit * original.stack + (buying - unit) * boughtBack);
            long beforeMoney = Copper(player);
            int beforeBuyback = BuybackQuantity(shop, original);
            int expectedBuyback = BuybackCapacity(shop, original, Math.Max(0, original.stack - boughtBack));
            if (!Ownership.TryBeginSale(request.Session)) return Result(ItemOperationState.Rejected, "sale-range-busy");
            ItemOperationResult result;
            try
            {
                Invoke(player, request.Source.Slot, 10);
                long money = Copper(player) - beforeMoney;
                int buyback = BuybackQuantity(shop, original) - beforeBuyback;
                Item after = player.inventory[request.Source.Slot];
                if (after.IsAir && money == expectedMoney && buyback == expectedBuyback)
                    result = new ItemOperationResult(ItemOperationState.Completed, original.stack, money, money == 0 ? "accepted-without-income" : null);
                else if (!after.IsNetStateDifferent(original) && money == 0 && buyback == 0)
                    result = Result(ItemOperationState.Rejected, "vanilla-sale-not-accepted");
                else result = Result(ItemOperationState.Unconfirmed, "sale-side-effects-not-confirmed");
            }
            catch { result = Result(ItemOperationState.Unconfirmed, "sale-interrupted-after-admission"); }
            Ownership.FinishSale(request.Session, result); return result;
        }
        public ItemOperationResult Execute(DiscardItemRequest request)
        {
            Player player = world.Player;
            if (request.Session != world.SessionGeneration || player == null || player.tileEntityAnchor.IsInValidUseTileEntity() || !world.Matches(request.Source))
                return Result(ItemOperationState.Rejected, "discard-source-busy");
            Item original = player.inventory[request.Source.Slot].Clone();
            if (!Ownership.TryBeginDiscard(request.Session, request.Source.Slot)) return Result(ItemOperationState.Rejected, "trash-or-source-busy");
            ItemOperationResult result;
            try
            {
                // This original path may research the old trash first in Journey.
                // Its replacement is intentional; never emulate it with world drops.
                Invoke(player, request.Source.Slot, 6);
                result = player.inventory[request.Source.Slot].IsAir && player.trashItem != null && !player.trashItem.IsNetStateDifferent(original) ?
                    new ItemOperationResult(ItemOperationState.Completed, original.stack) : Result(ItemOperationState.Unconfirmed, "trash-replacement-not-confirmed");
            }
            catch { result = Result(ItemOperationState.Unconfirmed, "trash-interrupted-after-admission"); }
            Ownership.FinishDiscard(request.Session, result); return result;
        }
        public ItemOperationResult Execute(StoreItemsRequest request)
        { return Store == null ? Result(ItemOperationState.Rejected, "storage-capability-unavailable") : Store(request); }
        private void Invoke(Player player, int slot, int cursor)
        {
            int previousCursor = Main.cursorOverride;
            world.AutomaticOperation = true;
            try
            {
                // A scoped branch selector for the verified original operation,
                // not an input event: no mouse button, release or shop is fabricated.
                Main.cursorOverride = cursor;
                if (!slotOverride(player.inventory, slot >= 54 ? 2 : 0, slot)) throw new InvalidOperationException("vanilla-operation-not-entered");
            }
            finally { Main.cursorOverride = previousCursor; world.AutomaticOperation = false; }
        }
        private static long Copper(Player player)
        {
            long total = 0;
            for (int i = 0; i < 54; i++)
            {
                Item item = player.inventory[i];
                if (item == null || item.stack <= 0) continue;
                long unit = item.type == 71 ? 1 : item.type == 72 ? 100 : item.type == 73 ? 10000 : item.type == 74 ? 1000000 : 0;
                total = checked(total + unit * item.stack);
            }
            return total;
        }
        private static int BuybackQuantity(Chest shop, Item original)
        {
            int count = 0;
            for (int i = 0; i < 39; i++) if (shop.item[i] != null && shop.item[i].buyOnce && Item.CanStack(shop.item[i], original)) count = checked(count + shop.item[i].stack);
            return count;
        }
        private static int BuybackCapacity(Chest shop, Item original, int quantity)
        {
            int remaining = quantity; bool empty = false;
            for (int i = 0; i < 39; i++)
            {
                Item item = shop.item[i];
                if (item == null || item.IsAir) empty = true;
                else if (item.buyOnce && Item.CanStack(item, original)) remaining -= Math.Min(remaining, item.maxStack - item.stack);
            }
            return empty ? quantity : quantity - remaining;
        }
        private static ItemOperationResult Result(ItemOperationState state, string reason)
        { return new ItemOperationResult(state, reason: reason); }
    }
}
