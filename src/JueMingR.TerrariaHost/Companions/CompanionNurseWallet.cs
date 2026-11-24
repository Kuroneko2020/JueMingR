using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.CoinDeposit;
using JueMingR.TerrariaHost.Recovery;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    // A plan owns only shadow arrays until both real wallets are admitted.
    // All native denomination choices, including impossible change, come from
    // the same TryPurchasing leaf that BuyItem calls.
    internal sealed class CompanionNurseWallet
    {
        private delegate bool Purchase(long price, List<Item[]> inventory, List<Point> coins, List<Point> packed,
            List<Point> bank1, List<Point> bank2, List<Point> bank3, List<Point> bank4);
        private static Purchase purchase;

        internal static void Bind()
        {
            if (purchase != null) return;
            var method = typeof(Player).GetMethod("TryPurchasing", BindingFlags.Static | BindingFlags.NonPublic, null,
                new[] { typeof(long), typeof(List<Item[]>), typeof(List<Point>), typeof(List<Point>),
                    typeof(List<Point>), typeof(List<Point>), typeof(List<Point>), typeof(List<Point>) }, null);
            if (method == null) throw new MissingMethodException(typeof(Player).FullName, "TryPurchasing");
            purchase = (Purchase)Delegate.CreateDelegate(typeof(Purchase), method);
        }

        internal struct Write
        {
            internal int Account, Slot;
            internal Item[] Items;
            internal Item Before, After;
        }

        private readonly Player player;
        private readonly Item[][] arrays = new Item[5][];
        private readonly Item[][] originals = new Item[5][];
        private readonly Item[][] snapshots = new Item[5][];
        internal readonly List<Write> Writes = new List<Write>();
        internal readonly ulong[] Slots = new ulong[5];
        internal readonly long Before, Share;

        private CompanionNurseWallet(Player player, long before, long share)
        {
            this.player = player;
            Before = before;
            Share = share;
        }

        private static int SlotsInAccount(int account) { return account == 0 ? 54 : 40; }

        internal static bool TryBalance(Player player, out long balance)
        {
            balance = 0;
            for (int account = 0; account < 5; account++)
            {
                var items = RecoverySource.AccountItems(player, account);
                if (items == null || items.Length != (account == 0 ? 59 : 40)) return false;
                for (int slot = 0; slot < SlotsInAccount(account); slot++)
                {
                    var item = items[slot];
                    if (item == null || item.stack < 0) return false;
                    balance += Value(item);
                }
            }
            return true;
        }

        private static long Value(Item item)
        {
            // Match the native CoinsCount denominations and widened stack
            // product for an exact debit receipt.
            return CoinRules.TryValue(item.type, item.stack, out long value) ? value : 0;
        }

        internal static CompanionNurseWallet Prepare(Player player, long before, long share)
        {
            var plan = new CompanionNurseWallet(player, before, share);
            if (share == 0) return plan;
            if (share < 0 || share > before) return null;
            return plan.Prepare() ? plan : null;
        }

        private bool Prepare()
        {
            var shadow = new List<Item[]>(5);
            var coins = new List<Point>();
            var empty = new List<Point>[5];
            for (int account = 0; account < 5; account++)
            {
                var items = arrays[account] = RecoverySource.AccountItems(player, account);
                var refs = originals[account] = (Item[])items.Clone();
                var values = snapshots[account] = new Item[refs.Length];
                var copies = new Item[refs.Length];
                for (int slot = 0; slot < refs.Length; slot++)
                {
                    values[slot] = refs[slot].Clone();
                    copies[slot] = refs[slot].Clone();
                    if (slot < SlotsInAccount(account) && copies[slot].IsACoin)
                        coins.Add(new Point(account, slot));
                }
                empty[account] = new List<Point>();
                for (int slot = SlotsInAccount(account) - 1; slot >= 0; slot--)
                    if (copies[slot].type == 0 || copies[slot].stack == 0)
                        empty[account].Add(new Point(account, slot));
                shadow.Add(copies);
            }

            // Unlike BuyItem, TryPurchasing returns true on failure.
            if (purchase(Share, shadow, coins, empty[0], empty[1], empty[2], empty[3], empty[4])) return false;
            long after = 0;
            for (int account = 0; account < 5; account++)
            {
                for (int slot = 0; slot < arrays[account].Length; slot++)
                {
                    Item original = snapshots[account][slot], replacement = shadow[account][slot];
                    bool included = slot < SlotsInAccount(account);
                    if (included) after += Value(replacement);
                    if (!replacement.IsNetStateDifferent(original)) continue;
                    if (!included || !original.IsACoin && !original.IsAir ||
                        !replacement.IsAir && (!replacement.IsACoin || replacement.stack <= 0 || replacement.stack > replacement.maxStack))
                        return false;
                    Slots[account] |= 1UL << slot;
                    Writes.Add(new Write
                    {
                        Account = account, Slot = slot, Items = arrays[account],
                        Before = originals[account][slot], After = replacement
                    });
                }
            }
            return Writes.Count != 0 && Before - after == Share;
        }

        internal bool Current()
        {
            if (Share == 0) return TryBalance(player, out long balance) && balance == Before;
            // Recheck all native inputs, including unchosen coin and empty
            // slots. A stale plan can otherwise silently change payer shares.
            for (int account = 0; account < 5; account++)
            {
                var items = RecoverySource.AccountItems(player, account);
                if (!ReferenceEquals(items, arrays[account])) return false;
                for (int slot = 0; slot < SlotsInAccount(account); slot++)
                    if (!ReferenceEquals(items[slot], originals[account][slot]) ||
                        items[slot].IsNetStateDifferent(snapshots[account][slot])) return false;
            }
            return true;
        }

        internal void Apply()
        {
            foreach (var write in Writes) write.Items[write.Slot] = write.After;
        }

        internal bool Debited()
        {
            return TryBalance(player, out long balance) && Before - balance == Share;
        }
    }
}
