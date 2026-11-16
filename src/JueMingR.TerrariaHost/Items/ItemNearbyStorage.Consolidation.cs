using System;
using System.Collections.Generic;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Items
{
    internal sealed partial class ItemNearbyStorage
    {
        // These records grant no lease. They live only as long as one native
        // network batch and are retired by any foreign or manual mutation.
        internal Action<long, ItemSlotObservation, ItemSlotObservation, ItemSlotObservation, ItemSlotObservation, ulong> Consolidated;
        internal Func<bool> CanConsolidate;
        internal bool HasPendingReception { get { return pending != null && !pending.Invalid && CanConsolidate?.Invoke() != false; } }
        internal sealed class Fragment
        {
            internal Item Item;
            internal int Quantity, Type, Maximum;
            internal byte Prefix;
            internal object Origin;
        }
        internal sealed class Reception
        {
            internal Pending Batch;
            internal Item Input;
            internal object Origin;
            internal bool Eligible;
            internal List<ReceivedSlot> Slots;
        }
        internal sealed class ReceivedSlot
        {
            internal int Slot, Added;
            internal Item Item;
            internal Fragment Previous;
        }
        internal Reception BeginReception(Player player, Item input, object origin)
        {
            Pending batch = pending;
            if (batch == null || batch.Invalid || CanConsolidate?.Invoke() == false || !ReferenceEquals(player, batch.Player) || batch.Request.Session != world.SessionGeneration ||
                !ReferenceEquals(world.SessionPlayer, player) || input == null) return null;
            bool eligible = false;
            if (origin != null && input.maxStack > 1 && !input.IsAir)
                foreach (ItemSlotObservation source in batch.Request.Sources)
                {
                    Item old = player.inventory[source.Slot];
                    if (player.inventoryChestStack[source.Slot] && old != null && !old.IsAir && old.stack < old.maxStack && Item.CanStack(old, input))
                    { eligible = true; break; }
                }
            // A foreign GetItem still observes touched tracked slots so a
            // withdrawal or automatic return cannot inherit pickup evidence.
            if (!eligible && batch.Fragments == null) return null;
            return new Reception { Batch = batch, Input = input, Origin = origin, Eligible = eligible };
        }
        internal void Received(Reception scope, Item[] array, int slot, int added)
        {
            if (scope == null || !ReferenceEquals(scope.Batch, pending) || !ReferenceEquals(array, scope.Batch.Player.inventory) ||
                !LegalSlot(slot) || added <= 0) return;
            Pending batch = scope.Batch;
            Fragment previous = batch.Fragments == null ? null : batch.Fragments[slot];
            if (!scope.Eligible)
            { if (previous != null) batch.Fragments[slot] = null; return; }
            Item actual = array[slot];
            if (previous == null && !ReferenceEquals(actual, scope.Input)) return;
            if (scope.Slots == null) scope.Slots = new List<ReceivedSlot>();
            foreach (ReceivedSlot entry in scope.Slots)
                if (entry.Slot == slot) { entry.Added = checked(entry.Added + added); return; }
            scope.Slots.Add(new ReceivedSlot { Slot = slot, Item = actual, Previous = previous, Added = added });
        }
        internal void EndReception(Reception scope, bool returned)
        {
            if (scope == null || !ReferenceEquals(scope.Batch, pending) || scope.Slots == null) return;
            Pending batch = scope.Batch;
            foreach (ReceivedSlot received in scope.Slots)
            {
                Item actual = batch.Player.inventory[received.Slot];
                Fragment old = received.Previous;
                bool valid = returned && ReferenceEquals(actual, received.Item) && actual != null && !actual.IsAir && actual.maxStack > 1 &&
                    actual.type == scope.Input.type && actual.prefix == scope.Input.prefix && actual.stack == checked((old == null ? 0 : old.Quantity) + received.Added) &&
                    (old == null || ReferenceEquals(old.Item, actual) && old.Type == actual.type && old.Prefix == actual.prefix && old.Maximum == actual.maxStack);
                if (batch.Fragments == null) { if (!valid) continue; batch.Fragments = new Fragment[58]; }
                batch.Fragments[received.Slot] = valid ? new Fragment { Item = actual, Quantity = actual.stack, Type = actual.type,
                    Prefix = actual.prefix, Maximum = actual.maxStack, Origin = scope.Origin } : null;
            }
        }
        internal void ConfirmReception(object origin, bool completed)
        {
            Pending batch = pending;
            if (batch?.Fragments == null) return;
            for (int i = 0; i < batch.Fragments.Length; i++)
            {
                Fragment fragment = batch.Fragments[i];
                if (fragment == null || !ReferenceEquals(fragment.Origin, origin)) continue;
                if (completed) fragment.Origin = null; else batch.Fragments[i] = null;
            }
        }
        internal void RetireReception(Item[] array, int slot)
        {
            Pending batch = pending;
            if (batch?.Fragments != null && ReferenceEquals(array, batch.Player.inventory) && LegalSlot(slot)) batch.Fragments[slot] = null;
        }
        internal void DiscardReceptions() { if (pending != null) pending.Fragments = null; }
        private static bool LegalSlot(int slot) { return slot >= 0 && slot < 50 || slot >= 54 && slot < 58; }
        private void Consolidate(Pending batch)
        {
            if (batch.Fragments == null || CanConsolidate?.Invoke() == false || !ReferenceEquals(world.Player, batch.Player) || !world.CanStartActions || world.Busy ||
                batch.Request.Session != world.SessionGeneration) return;
            Player player = batch.Player;
            int[] expected = (int[])batch.Remaining.Clone();
            ulong changed = 0;
            try
            {
                for (int slot = 0; slot < batch.Fragments.Length; slot++)
                {
                    Fragment fragment = batch.Fragments[slot];
                    if (fragment == null || fragment.Origin != null) continue;
                    Item item = player.inventory[slot];
                    if (!ReferenceEquals(item, fragment.Item) || item.type != fragment.Type || item.prefix != fragment.Prefix ||
                        item.stack != fragment.Quantity || item.maxStack != fragment.Maximum || world.IsProtected(player, item, slot)) continue;
                    for (int i = 0; i < batch.Request.Sources.Count && !item.IsAir; i++)
                    {
                        int targetSlot = batch.Request.Sources[i].Slot;
                        Item target = player.inventory[targetSlot];
                        if (targetSlot == slot || !ReferenceEquals(target, batch.RepliedItems[i]) || target == null || target.IsAir ||
                            target.stack != expected[i] || target.stack <= 0 || target.maxStack <= 1 || target.stack >= target.maxStack ||
                            item.stack <= 0 || item.stack > item.maxStack || ReferenceEquals(item, target) || !Item.CanStack(target, item) ||
                            world.IsProtected(player, target, targetSlot) || world.IsProtected(player, item, slot)) continue;
                        ItemSlotObservation beforeSource = world.ObserveSlot(slot), beforeTarget = world.ObserveSlot(targetSlot);
                        int moved = Math.Min(target.maxStack - target.stack, item.stack);
                        target.stack += moved; item.stack -= moved;
                        if (item.stack == 0) item.TurnToAir();
                        expected[i] = target.stack;
                        changed |= (1UL << slot) | (1UL << targetSlot);
                        world.InvalidateObservation();
                        Consolidated?.Invoke(batch.Request.Session, beforeSource, beforeTarget, world.ObserveSlot(slot), world.ObserveSlot(targetSlot), Tick);
                    }
                }
            }
            finally
            {
                // Changed quantities are facts even if mapping/sending fails.
                // Never restore a stale snapshot or replay this closed batch.
                for (int slot = 0; slot < 58; slot++)
                    if ((changed & (1UL << slot)) != 0 && Main.netMode == 1)
                        NetMessage.SendData(5, -1, -1, null, player.whoAmI, PlayerItemSlotID.Inventory0 + slot);
            }
        }
    }
}
