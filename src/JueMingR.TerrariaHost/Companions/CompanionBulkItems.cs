using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Platform.Companions;
using Terraria;
using Terraria.ID;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum CompanionBulkOperation { LootAll, DepositAll, QuickStack, Restock, Sort }

    internal static class CompanionBulkItems
    {
        private static readonly int[] BasicInventory = Enumerable.Range(0, 50).ToArray();
        private static readonly int[] CoinInventory = Enumerable.Range(50, 4).ToArray();
        private static readonly int[] AmmoInventory = Enumerable.Range(54, 4).ToArray();
        private static readonly int[] DepositInventory = Enumerable.Range(10, 40).Reverse().ToArray();
        private static readonly int[] RestockInventory = AmmoInventory.Reverse().Concat(BasicInventory.Reverse()).ToArray();
        private static readonly int[] CoinThenInventory = CoinInventory.Concat(BasicInventory).ToArray();
        private static readonly int[] ReverseCoinInventory = CoinThenInventory.Take(4).Reverse().Concat(BasicInventory.Reverse()).ToArray();
        private static readonly int[] Hotbar = Enumerable.Range(0, 10).ToArray();
        private static readonly long[] CoinValues = { 1, 100, 10000, 1000000 };

        private static class NativeSort
        {
            internal static readonly Action<bool, Item[], int[]> Run = (Action<bool, Item[], int[]>)Delegate.CreateDelegate(
                typeof(Action<bool, Item[], int[]>), typeof(ItemSorting).GetMethod("Sort", BindingFlags.Static | BindingFlags.NonPublic,
                    null, new[] { typeof(bool), typeof(Item[]), typeof(int[]) }, null));
        }

        // This plan never touches live bodies. The caller subsequently
        // prepares and commits a single write set against both real arrays.
        internal static void Plan(CompanionBulkOperation operation, CompanionInventoryArea area, Player owner,
            Item[] backpack, Item[] container, bool[] backpackBlocked, bool[] containerBlocked)
        {
            bool main = area == CompanionInventoryArea.Inventory;
            int[] general = main ? BasicInventory : Enumerable.Range(0, container.Length).ToArray();
            switch (operation)
            {
                case CompanionBulkOperation.LootAll:
                    for (int i = 0; i < container.Length; i++)
                    {
                        if (containerBlocked[i] || container[i].IsAir ||
                            area == CompanionInventoryArea.VoidVault && container[i].favorited) continue;
                        Loot(owner, ref container[i], backpack, backpackBlocked);
                    }
                    break;
                case CompanionBulkOperation.DepositAll:
                case CompanionBulkOperation.QuickStack:
                    Deposit(operation == CompanionBulkOperation.QuickStack, main, backpack, backpackBlocked,
                        container, containerBlocked, general);
                    break;
                case CompanionBulkOperation.Restock:
                    Restock(backpack, backpackBlocked, container, containerBlocked);
                    break;
                case CompanionBulkOperation.Sort:
                    Sort(container, containerBlocked, main);
                    break;
            }
        }

        private static void Loot(Player owner, ref Item source, Item[] inventory, bool[] blocked)
        {
            if (source.uniqueStack)
            {
                int type = source.type;
                if (inventory.Any(item => !item.IsAir && item.type == type)) return;
            }
            if (source.IsACoin)
            {
                MoveCoins(ref source, inventory, blocked, CoinThenInventory);
                if (source.IsAir) return;
            }
            if (source.FitsAmmoSlot())
            {
                Move(ref source, inventory, blocked, AmmoInventory, false, owner);
                if (!source.IsAir && source.CanFillEmptyAmmoSlot()) Move(ref source, inventory, blocked, AmmoInventory, true);
                if (source.IsAir) return;
            }
            Move(ref source, inventory, blocked, source.IsACoin ? CoinThenInventory : BasicInventory, false, owner);
            if (source.IsAir) return;
            if (!source.IsACoin && source.useStyle != 0) Move(ref source, inventory, blocked, Hotbar, true);
            if (source.IsAir) return;
            Move(ref source, inventory, blocked, source.IsACoin ?
                (source.favorited ? BasicInventory.Concat(CoinInventory) : ReverseCoinInventory) :
                (source.favorited ? BasicInventory : DepositInventory.Concat(Hotbar.Reverse())), true);
        }

        private static void Deposit(bool quick, bool main, Item[] source, bool[] sourceBlocked,
            Item[] target, bool[] targetBlocked, int[] general)
        {
            int[] coins = main ? CoinThenInventory : general;
            // ChestUI.MoveCoins returns before scanning the owner if the
            // destination has no coins. DepositAll still handles ordinary
            // backpack coins in its later 49-to-10 pass.
            if (coins.Any(slot => target[slot].IsACoin))
            {
                for (int i = 53; i >= 0; i--)
                    if (!sourceBlocked[i] && source[i].IsACoin && !source[i].favorited)
                        MoveCoins(ref source[i], target, targetBlocked, coins);
            }
            var types = new HashSet<int>();
            if (quick)
                foreach (int slot in general)
                    if (!target[slot].IsAir && !target[slot].IsACoin) types.Add(target[slot].type);
            var candidates = quick ? BasicInventory.Skip(10) : DepositInventory;
            foreach (int i in candidates)
            {
                if (sourceBlocked[i] || source[i].IsAir || source[i].favorited ||
                    quick && (source[i].IsACoin || !types.Contains(source[i].type))) continue;
                Move(ref source[i], target, targetBlocked, general, false);
                if (!source[i].IsAir) Move(ref source[i], target, targetBlocked, general, true);
            }
        }

        private static void Restock(Item[] inventory, bool[] blocked, Item[] source, bool[] sourceBlocked)
        {
            var matchingTypes = new HashSet<int>();
            foreach (int slot in RestockInventory)
            {
                var item = inventory[slot];
                if (!item.IsAir && !item.IsACoin && item.maxStack > 1 && (!item.favorited || !item.OnlyNeedOneInInventory()))
                    matchingTypes.Add(item.type);
            }
            for (int i = 0; i < source.Length; i++)
            {
                var item = source[i];
                if (sourceBlocked[i] || item.IsAir || item.prefix != 0 || !matchingTypes.Contains(item.type)) continue;
                foreach (int slot in RestockInventory)
                {
                    var target = inventory[slot];
                    if (blocked[slot] || target.IsAir || target.favorited && target.OnlyNeedOneInInventory()) continue;
                    if (slot >= 54 && !item.FitsAmmoSlot()) continue;
                    MoveSlot(ref source[i], inventory, slot, false);
                    if (source[i].IsAir) break;
                }
                if (!source[i].IsAir && item.ammo != 0)
                {
                    if (item.FitsAmmoSlot()) Move(ref source[i], inventory, blocked, AmmoInventory.Reverse(), true);
                    if (!source[i].IsAir) Move(ref source[i], inventory, blocked, BasicInventory.Reverse(), true);
                }
            }
        }

        private static void Move(ref Item source, Item[] target, bool[] blocked, IEnumerable<int> slots, bool empty, Player pickup = null)
        {
            foreach (int slot in slots)
            {
                if (blocked[slot] || pickup != null && !pickup.CanItemSlotAcceptPickup(target[slot], source)) continue;
                MoveSlot(ref source, target, slot, empty);
                if (source.IsAir) return;
            }
        }

        private static void MoveSlot(ref Item source, Item[] target, int slot, bool empty)
        {
            var current = target[slot];
            if (current.IsAir != empty || !empty && !Item.CanStack(source, current)) return;
            int count = Math.Min(source.stack, empty ? source.maxStack : current.maxStack - current.stack);
            if (count <= 0) return;
            if (empty)
            {
                target[slot] = source.Clone();
                target[slot].stack = count;
            }
            else current.stack += count;
            source.stack -= count;
            if (source.stack == 0) source = new Item();
        }

        private static void MoveCoins(ref Item source, Item[] target, bool[] blocked, int[] slots)
        {
            int denomination = source.type - ItemID.CopperCoin;
            var available = new List<int>();
            long total = source.stack * CoinValues[denomination];
            foreach (int slot in slots)
            {
                var item = target[slot];
                if (blocked[slot] || item.favorited || !item.IsAir && !item.IsACoin) continue;
                available.Add(slot);
                if (item.IsACoin) total += item.stack * CoinValues[item.type - ItemID.CopperCoin];
            }
            // Try native denomination conversion first. If the new value
            // needs more slots, a same-coin partial move is still legitimate.
            if (WriteCoinValue(target, available, total))
            { source = new Item(); return; }
            Move(ref source, target, blocked, available, false);
            if (!source.IsAir) Move(ref source, target, blocked, available, true);
        }

        private static bool WriteCoinValue(Item[] target, List<int> slots, long value)
        {
            var coins = new List<Item>();
            for (int denomination = 3; denomination >= 0; denomination--)
            {
                long unit = CoinValues[denomination];
                while (value >= unit)
                {
                    if (coins.Count == slots.Count) return false;
                    var coin = new Item();
                    coin.SetDefaults(ItemID.CopperCoin + denomination);
                    coin.stack = (int)Math.Min(coin.maxStack, value / unit);
                    value -= coin.stack * unit;
                    coins.Add(coin);
                }
            }
            for (int i = 0; i < slots.Count; i++)
                target[slots[i]] = i < coins.Count ? coins[i] : new Item();
            return true;
        }

        private static void Sort(Item[] items, bool[] blocked, bool main)
        {
            if (main)
            {
                var coinSlots = new List<int>();
                long value = 0;
                foreach (int slot in CoinThenInventory)
                {
                    var item = items[slot];
                    if (blocked[slot] || item.favorited || !item.IsACoin && (slot < 50 || !item.IsAir)) continue;
                    coinSlots.Add(slot);
                    if (item.IsACoin) value += item.stack * CoinValues[item.type - ItemID.CopperCoin];
                }
                WriteCoinValue(items, coinSlots, value);
                SortAmmo(items, blocked);
            }
            var ignored = new List<int>();
            for (int i = 0; i < items.Length; i++)
                if (blocked[i] || main && i >= 50) ignored.Add(i);
            NativeSort.Run(false, items, ignored.ToArray());
        }

        private static void SortAmmo(Item[] items, bool[] blocked)
        {
            // ClearAmmoSlotSpaces consolidates and slides the dedicated
            // region before FillAmmoFromInventory chooses what shoots first.
            foreach (int slot in AmmoInventory)
            {
                var item = items[slot];
                if (!blocked[slot] && !item.IsAir && item.ammo != AmmoID.None)
                    RefillAmmo(items, blocked, slot, item.favorited ? 54 : slot + 1, 58);
            }
            foreach (int slot in AmmoInventory)
            {
                if (blocked[slot] || items[slot].IsAir || items[slot].favorited) continue;
                for (int earlier = 54; earlier < slot; earlier++)
                {
                    if (blocked[earlier] || !items[earlier].IsAir) continue;
                    Utils.Swap(ref items[slot], ref items[earlier]);
                    break;
                }
            }
            var existingTypes = new HashSet<int>();
            var empty = new Queue<int>();
            foreach (int slot in AmmoInventory)
            {
                var item = items[slot];
                if (item.IsAir)
                {
                    if (!blocked[slot]) empty.Enqueue(slot);
                }
                else if (item.ammo != AmmoID.None)
                {
                    existingTypes.Add(item.type);
                    if (!blocked[slot]) RefillAmmo(items, blocked, slot, 0, 50);
                }
            }
            for (int pass = 0; pass < 2 && empty.Count > 0; pass++)
                for (int slot = 0; slot < 50 && empty.Count > 0; slot++)
                {
                    var item = items[slot];
                    if (blocked[slot] || item.IsAir || item.favorited || !item.CanFillEmptyAmmoSlot() ||
                        !(pass == 0 ? existingTypes.Contains(item.type) : item.FitsAmmoSlot())) continue;
                    int target = empty.Dequeue();
                    Utils.Swap(ref items[slot], ref items[target]);
                    RefillAmmo(items, blocked, target, 0, 50);
                }
        }

        private static void RefillAmmo(Item[] items, bool[] blocked, int target, int first, int end)
        {
            for (int source = first; source < end && items[target].stack < items[target].maxStack; source++)
            {
                if (source == target || blocked[source] || items[source].IsAir || items[source].favorited) continue;
                MoveSlot(ref items[source], items, target, false);
            }
        }
    }
}
