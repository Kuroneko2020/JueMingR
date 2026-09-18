using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.CoinDeposit;
using Terraria;
using Terraria.UI;

namespace JueMingR.TerrariaHost.CoinDeposit
{
    // Only a currently open real source creates a causal scope; nested native
    // calls are interpreted once, after the outermost transfer has finished.
    internal static class CoinWithdrawalHooks
    {
        private static readonly Harmony harmony = new Harmony("JueMingR.CoinWithdrawal");
        private static HostCoinDeposit host;
        private static Scope active;
#if DEBUG
        internal static long Scopes;
#endif
        private sealed class Scope
        {
            internal Player Player;
            internal Chest Container;
            internal Item[] Source, Wallet;
            internal long Session, Generation, SourceValue, WalletValue, MouseValue, SelectedValue;
            internal Item Mouse, Selected;
            internal Stamp MouseStamp, SelectedStamp;
            internal bool Finished;
            internal int ChestIndex, Slot, Context;
        }
        private struct Stamp
        {
            internal int Type, Stack;
            internal byte Prefix;
            internal Stamp(Item item) { Type = item.type; Stack = item.stack; Prefix = item.prefix; }
            internal bool Matches(Item item) { return item != null && item.type == Type && item.stack == Stack && item.prefix == Prefix; }
        }
        internal static void Install(HostCoinDeposit value)
        {
            host = value;
            if (typeof(Main).Assembly.ManifestModule.ModuleVersionId != new Guid("2c29f6c3-4bd9-4add-9c58-da159804e083")) throw new InvalidOperationException("coin-native-identity");
            var slot = new[] { typeof(Item[]), typeof(int), typeof(int) };
            Patch(typeof(ChestUI), "LootAll", Type.EmptyTypes, nameof(BeforeLoot));
            Patch(typeof(ItemSlot), "LeftClick", slot, nameof(BeforeClick));
            Patch(typeof(ItemSlot), "OverrideLeftClick", slot, nameof(BeforeClick));
            Patch(typeof(ItemSlot), "RightClick", slot, nameof(BeforeSlot));
            Patch(typeof(ItemSlot), "PickupItemIntoMouse", new[] { typeof(Item[]), typeof(int), typeof(int), typeof(Player) }, nameof(BeforeSlot));
        }
        private static void Patch(Type type, string name, Type[] args, string before)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, args, null);
            if (method == null || method.GetMethodBody() == null) throw new MissingMethodException("coin-withdrawal:" + name);
            harmony.Patch(method, Hook(before), Hook(nameof(After)), null, Hook(nameof(Final)));
        }
        private static HarmonyMethod Hook(string name) { return new HarmonyMethod(typeof(CoinWithdrawalHooks).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)); }
        internal static void Uninstall()
        { foreach (var method in harmony.GetPatchedMethods().ToArray()) harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); host = null; Retire(); }
        internal static void Retire() { active = null; }
        private static void BeforeLoot(out Scope __state) { __state = Begin(null, -1, -1); }
        private static void BeforeClick(Item[] __0, int __1, int __2, out Scope __state)
        { __state = Main.cursorOverride == 6 ? null : Begin(__0, __2, __1); }
        private static void BeforeSlot(Item[] __0, int __1, int __2, out Scope __state) { __state = Begin(__0, __2, __1); }
        private static Scope Begin(Item[] source, int slot, int context)
        {
            // Demand gate precedes all reads and allocations when disabled.
            if (host == null || !host.Observing || active != null || host.Intent.Protected || host.Intent.Pending) return null;
            Player player = host.Player;
            if (player.chest == -1 || player.chest < -5 || player.chest >= Main.chest.Length) return null;
            int expected = player.chest >= 0 ? 3 : player.chest == -5 ? 32 : 4;
            if (context != -1 && context != expected) return null;
            Chest container = player.GetCurrentContainer();
            if (container?.item == null || source != null && !ReferenceEquals(source, container.item)) return null;
            source = container.item;
            if (slot >= 0 && (slot >= source.Length || source[slot] == null || !CoinRules.IsCoin(source[slot].type) || source[slot].stack <= 0)) return null;
            try
            {
                long value = CoinSnapshot.Total(source, source.Length);
                if (value <= 0) return null;
                active = new Scope { Player = player, Container = container, Source = source, Wallet = player.inventory,
                    Session = host.Session, Generation = host.Intent.Generation, SourceValue = value, ChestIndex = player.chest, Slot = slot, Context = context,
                    WalletValue = CoinSnapshot.Total(player.inventory, 58), MouseValue = MouseValue(), Mouse = Main.mouseItem,
                    Selected = slot < 0 ? null : source[slot], SelectedValue = slot < 0 ? 0 : Value(source[slot]),
                    SelectedStamp = slot < 0 ? default(Stamp) : new Stamp(source[slot]), MouseStamp = new Stamp(Main.mouseItem) };
#if DEBUG
                Scopes++;
#endif
                return active;
            }
            catch { return null; }
        }
        private static void After(Scope __state) { Finish(__state); }
        private static Exception Final(Exception __exception, Scope __state) { Finish(__state); return __exception; }
        private static void Finish(Scope scope)
        {
            if (scope == null || scope.Finished) return;
            scope.Finished = true; if (ReferenceEquals(active, scope)) active = null;
            if (host == null || !host.CanRetainWithdrawal(scope.Player, scope.Session) || host.Intent.Generation != scope.Generation ||
                !ReferenceEquals(scope.Source, scope.Container.item)) return;
            Chest current = scope.ChestIndex >= 0 ? Main.chest[scope.ChestIndex] : CoinRange.Account(scope.Player, -2 - scope.ChestIndex);
            if (!ReferenceEquals(scope.Container, current)) return;
            long loss = 0;
            try
            {
                // A denomination swap withdraws the original bank object even
                // if more valuable mouse coins enter the bank simultaneously.
                // Follow this exact native exchange, never an aggregate gain.
                bool exchange = scope.Slot >= 0 && scope.SelectedValue > 0 && !ReferenceEquals(scope.Selected, scope.Mouse) &&
                    ReferenceEquals(scope.Source[scope.Slot], scope.Mouse) && ReferenceEquals(Main.mouseItem, scope.Selected) &&
                    scope.SelectedStamp.Matches(Main.mouseItem) && scope.MouseStamp.Matches(scope.Source[scope.Slot]);
                if (exchange) loss = scope.SelectedValue;
                long sourceAfter = CoinSnapshot.Total(scope.Source, scope.Source.Length);
                if (!exchange) loss = checked(scope.SourceValue - sourceAfter);
                if (loss <= 0) return;
                if (!host.Observing || !ReferenceEquals(scope.Wallet, scope.Player.inventory))
                { host.Intent.Withdrawal(scope.Session, scope.Generation, loss, 0); return; }
                long walletAfter = CoinSnapshot.Total(scope.Wallet, 58), mouseAfter = MouseValue();
                long arrival = exchange ? ReferenceEquals(Main.mouseItem, scope.Selected) && mouseAfter == loss &&
                    checked(sourceAfter + walletAfter + mouseAfter) == checked(scope.SourceValue + scope.WalletValue + scope.MouseValue) ? loss : 0 :
                    checked(walletAfter + mouseAfter - scope.WalletValue - scope.MouseValue);
                host.Intent.Withdrawal(scope.Session, scope.Generation, loss, arrival);
            }
            catch
            {
                if (loss > 0) host.Intent.Withdrawal(scope.Session, scope.Generation, loss, 0);
            }
            // No cross-frame aggregate matching: a later pickup is not this
            // withdrawal's arrival. Pending keeps only finite user-intent facts,
            // with no Item references, until complete range exit/off-on/session.
        }
        private static long MouseValue()
        { if (Main.mouseItem == null) throw new InvalidOperationException("coin-mouse-unreadable"); return Value(Main.mouseItem); }
        private static long Value(Item item)
        { long value; return CoinRules.TryValue(item.type, item.stack, out value) ? value : 0; }
    }
}
