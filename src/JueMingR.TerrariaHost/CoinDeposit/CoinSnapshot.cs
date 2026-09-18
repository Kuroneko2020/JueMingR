using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using JueMingR.Features.CoinDeposit;
using Terraria;

namespace JueMingR.TerrariaHost.CoinDeposit
{
    // Full fields are compared by a delegate compiled once at binding, not by
    // per-frame reflection. Clones exist only during a real bounded operation.
    internal sealed class CoinSnapshot
    {
        internal static readonly Func<Item, Item, bool> SameFields = CompileComparer();
        internal readonly Player Player;
        internal readonly Chest Bank;
        internal readonly Item[] Wallet, Target;
        internal readonly Item[] WalletRefs, TargetRefs, WalletValues, TargetValues;
        internal readonly Item Mouse, MouseValue;
        internal readonly long WalletTotal, TargetTotal;
        internal readonly ulong SourceMask;
        private readonly Chest[] accounts = new Chest[4];
        private readonly Item[][] accountArrays = new Item[4][], accountRefs = new Item[4][], accountValues = new Item[4][];
        private readonly Item[] nativeCoins = new Item[4];
        private readonly Item nativeAir = new Item();
        private CoinSnapshot(Player player, Chest bank, Item[] walletValues, Item[] targetValues, ulong mask)
        {
            Player = player; Bank = bank; Wallet = player.inventory; Target = bank.item;
            WalletRefs = (Item[])Wallet.Clone(); TargetRefs = (Item[])Target.Clone();
            WalletValues = walletValues; TargetValues = targetValues; Mouse = Main.mouseItem; MouseValue = Mouse.Clone();
            WalletTotal = Total(Wallet, 58); TargetTotal = Total(Target, Target.Length); SourceMask = mask;
            for (int b = 0; b < 4; b++)
            {
                accounts[b] = CoinRange.Account(player, b); accountArrays[b] = accounts[b].item;
                accountRefs[b] = (Item[])accountArrays[b].Clone(); accountValues[b] = new Item[40];
                for (int i = 0; i < 40; i++) accountValues[b][i] = accountArrays[b][i].Clone();
                // SetDefaults uses current native localization/shader tables.
                // Keep templates within this operation rather than caching stale
                // reference fields across language/resource lifetime changes.
                nativeCoins[b] = new Item(); nativeCoins[b].SetDefaults(71 + b);
            }
        }
        internal static CoinSnapshot Capture(Player player, Chest bank, ulong mask)
        {
            if (player == null || bank == null || player.inventory == null || player.inventory.Length != 59 ||
                bank.item == null || bank.item.Length != bank.maxItems || bank.maxItems != 40 || Main.mouseItem == null) throw new InvalidOperationException("coin-shape");
            var seen = new HashSet<Item>();
            for (int i = 0; i < 59; i++) Validate(player.inventory[i], seen);
            // Account aliases are rejected rather than counted twice or allowing
            // a currency write to become an unrelated account's hidden write.
            var arrays = new HashSet<Item[]> { player.inventory };
            for (int b = 0; b < 4; b++)
            {
                Chest account = CoinRange.Account(player, b);
                if (account?.item == null || account.item.Length != account.maxItems || account.maxItems != 40 || !arrays.Add(account.item)) throw new InvalidOperationException("coin-account-alias");
                for (int i = 0; i < account.item.Length; i++) Validate(account.item[i], seen);
            }
            // Slot 58 may be the native mouse mirror. No writable member may be
            // that same object, even when its displayed amount is identical.
            if (!Main.mouseItem.IsAir && seen.Contains(Main.mouseItem) && !ReferenceEquals(player.inventory[58], Main.mouseItem)) throw new InvalidOperationException("coin-mouse-alias");
            var wallet = new Item[59]; var target = new Item[bank.item.Length];
            for (int i = 0; i < wallet.Length; i++) wallet[i] = player.inventory[i].Clone();
            for (int i = 0; i < target.Length; i++) target[i] = bank.item[i].Clone();
            return new CoinSnapshot(player, bank, wallet, target, mask);
        }
        private static void Validate(Item item, HashSet<Item> seen)
        {
            if (item == null || !seen.Add(item) || item.stack < 0 || CoinRules.IsCoin(item.type) &&
                (item.stack > item.maxStack || item.maxStack != (item.type == 74 ? 9999 : 100))) throw new InvalidOperationException("coin-invalid-member");
        }
        internal static long Total(Item[] array, int count)
        {
            long result = 0;
            for (int i = 0; i < count; i++)
            {
                if (array[i] == null || array[i].stack < 0) throw new InvalidOperationException("coin-unreadable");
                long value;
                if (CoinRules.TryValue(array[i].type, array[i].stack, out value)) result = checked(result + value);
            }
            return result;
        }
        internal bool Identities()
        {
            if (!ReferenceEquals(Player, Main.LocalPlayer) || !ReferenceEquals(Wallet, Player.inventory) || !ReferenceEquals(Target, Bank.item) || !ReferenceEquals(Mouse, Main.mouseItem) || !SameFields(MouseValue, Mouse)) return false;
            for (int b = 0; b < 4; b++)
                if (!ReferenceEquals(accounts[b], CoinRange.Account(Player, b)) || !ReferenceEquals(accountArrays[b], accounts[b].item)) return false;
            return true;
        }
        internal bool Unchanged()
        {
            if (!Identities()) return false;
            for (int i = 0; i < 59; i++) if (!ReferenceEquals(WalletRefs[i], Wallet[i]) || !SameFields(WalletValues[i], Wallet[i])) return false;
            for (int b = 0; b < 4; b++)
                for (int i = 0; i < 40; i++) if (!ReferenceEquals(accountRefs[b][i], accountArrays[b][i]) || !SameFields(accountValues[b][i], accountArrays[b][i])) return false;
            return true;
        }
        internal bool NativeEnvelope()
        {
            if (!Identities()) return false;
            var seen = new HashSet<Item>();
            for (int i = 0; i < 59; i++)
            {
                Validate(Wallet[i], seen);
                if (i == 58 || (SourceMask & (1UL << i)) == 0 || TargetTotal == 0)
                { if (!ReferenceEquals(WalletRefs[i], Wallet[i]) || !SameFields(WalletValues[i], Wallet[i])) return false; }
                else if (ReferenceEquals(WalletRefs[i], Wallet[i]) || !NativeValue(Wallet[i])) return false;
            }
            for (int b = 0; b < 4; b++)
            for (int i = 0; i < 40; i++)
            {
                Item current = accountArrays[b][i], before = accountValues[b][i];
                Validate(current, seen);
                if (!ReferenceEquals(accounts[b], Bank) || !before.IsAir && !CoinRules.IsCoin(before.type))
                { if (!ReferenceEquals(accountRefs[b][i], current) || !SameFields(before, current)) return false; }
                else if (ReferenceEquals(accountRefs[b][i], current) || !NativeValue(current)) return false;
            }
            return true;
        }
        private bool NativeValue(Item value)
        {
            // The fixed native path reconstructs writable members as plain Air,
            // or SetDefaults(coin) followed only by stack. TurnToAir/SetDefaults(0)
            // are different native states and cannot substitute for plain Air.
            if (value.IsAir) return SameFields(nativeAir, value);
            if (!CoinRules.IsCoin(value.type)) return false;
            Item template = nativeCoins[value.type - 71]; template.stack = value.stack;
            return SameFields(template, value);
        }
        private static Func<Item, Item, bool> CompileComparer()
        {
            var a = Expression.Parameter(typeof(Item), "a"); var b = Expression.Parameter(typeof(Item), "b");
            Expression equal = Expression.Constant(true);
            for (Type type = typeof(Item); type != null && type != typeof(object); type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var x = Expression.Field(a, field); var y = Expression.Field(b, field);
                    Expression same;
                    if (!field.FieldType.IsValueType) same = Expression.ReferenceEqual(x, y);
                    else
                    {
                        Type comparer = typeof(EqualityComparer<>).MakeGenericType(field.FieldType);
                        same = Expression.Call(Expression.Property(null, comparer.GetProperty("Default")), comparer.GetMethod("Equals", new[] { field.FieldType, field.FieldType }), x, y);
                    }
                    equal = Expression.AndAlso(equal, same);
                }
            return Expression.Lambda<Func<Item, Item, bool>>(equal, a, b).Compile();
        }
    }
}
