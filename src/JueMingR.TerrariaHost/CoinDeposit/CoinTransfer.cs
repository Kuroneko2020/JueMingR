using System;
using System.Runtime.CompilerServices;
using JueMingR.Features.CoinDeposit;
using Terraria;
using Terraria.UI;

namespace JueMingR.TerrariaHost.CoinDeposit
{
    internal sealed class CoinTransfer
    {
        private readonly HostCoinDeposit host;
        private long token;
        private ulong currentSources;
        internal ulong UnconfirmedSources { get; private set; }
        internal void BeginSession() { UnconfirmedSources = 0; }
        internal bool Executing { get; private set; }
        internal CoinOutcome Outcome { get; private set; }
        internal long Amount { get; private set; }
#if DEBUG
        internal long NativeCalls, Snapshots, SeedCalls;
#endif
        internal CoinTransfer(HostCoinDeposit host) { this.host = host; }
        internal CoinOutcome Execute(CoinRange.Entrance entrance, long generation)
        {
            Amount = 0;
            Player player = host.Player;
            ulong sources;
            if (!host.Admit(entrance, generation, out sources)) return Outcome = CoinOutcome.Rejected;
            CoinSnapshot before;
            try
            {
                before = CoinSnapshot.Capture(player, entrance.Bank, sources);
#if DEBUG
                Snapshots++;
#endif
            }
            catch { return Outcome = CoinOutcome.Rejected; }
            if (!before.Unchanged() || !host.Admit(entrance, generation, out sources) || sources != before.SourceMask ||
                !host.Items.Ownership.TryBeginCoins(host.Runtime.Generation, sources, ++token)) return Outcome = CoinOutcome.Cancelled;
            currentSources = sources; Executing = true; Outcome = CoinOutcome.Executing;
            host.Items.World.AutomaticOperation = true;
            CoinSnapshot seedBasis = null;
            long nativeRuntime = host.Runtime.Generation, nativeSession = host.Session, nativeLease = token;
            try
            {
                // Call the real inventory, not a sliced array or temporarily
                // favored mask. This path has no bank RPC / fictional ACK.
#if DEBUG
                NativeCalls++;
#endif
                long nativeResult = ChestUI.MoveCoins(player.inventory, entrance.Bank);
                if (host.Runtime.Generation != nativeRuntime || host.Session != nativeSession || !ReferenceEquals(host.Player, player) || !before.NativeEnvelope()) return Unknown();
                long afterWallet = CoinSnapshot.Total(player.inventory, 58), afterBank = CoinSnapshot.Total(entrance.Bank.item, entrance.Bank.maxItems);
                Amount = CoinRules.Conserved(before.WalletTotal, afterWallet, before.TargetTotal, afterBank);
                if (Amount < 0) return Unknown();
                if (Amount > 0) return Outcome = host.HasMovableCoins(player) ? CoinOutcome.Partial : CoinOutcome.Completed;
                if (entrance.Kind == 0 && nativeResult == 0 && before.EmptyNormalization())
                {
                    // Capture only the actual post-normalization objects. Keep
                    // this evidence across release so an intervening equivalent
                    // replacement cannot silently acquire a new write permit.
                    seedBasis = CoinSnapshot.Capture(player, entrance.Bank, sources);
#if DEBUG
                    Snapshots++;
#endif
                    if (!before.EmptyNormalization() || !seedBasis.Unchanged()) return Unknown();
                }
                Outcome = CoinOutcome.None;
            }
            catch { return Unknown(); }
            finally
            {
                host.Items.World.AutomaticOperation = false; Executing = false;
                host.Items.Ownership.EndCoins(nativeRuntime, nativeLease, Outcome == CoinOutcome.Unconfirmed);
                currentSources = 0;
                host.Items.World.InvalidateObservation();
            }
            // The old lease grants no seed permission. A new admission/lease
            // must prove the post-native snapshot still describes reality.
            return seedBasis != null ? Seed(entrance, generation, seedBasis) : Outcome;
        }
        private CoinOutcome Seed(CoinRange.Entrance entrance, long generation, CoinSnapshot fresh)
        {
            ulong sources; Player player = host.Player;
            if (entrance.Kind != 0 || !host.Admit(entrance, generation, out sources)) return Outcome = CoinOutcome.Cancelled;
            Item empty;
            int source = -1, target = -1;
            try
            {
                if (fresh.TargetTotal != 0 || !fresh.Unchanged() || sources != fresh.SourceMask) return Outcome = CoinOutcome.Cancelled;
                for (int i = 0; i < 58; i++)
                    if ((sources & (1UL << i)) != 0 && (source < 0 || fresh.WalletRefs[i].type > fresh.WalletRefs[source].type)) source = i;
                for (int i = 0; i < 40; i++) if (fresh.TargetRefs[i].IsAir) { target = i; break; }
                if (source < 0 || target < 0) return Outcome = CoinOutcome.None;
                empty = new Item(); // Prepared before changing either endpoint.
            }
            catch { return Outcome = CoinOutcome.Rejected; }
            if (!fresh.Unchanged() || !host.Admit(entrance, generation, out sources) || sources != fresh.SourceMask)
                return Outcome = CoinOutcome.Cancelled;
            long runtime = host.Runtime.Generation, session = host.Session;
            ulong selected = 1UL << source; long lease = ++token;
            if (!host.Items.Ownership.TryBeginCoins(runtime, selected, lease)) return Outcome = CoinOutcome.Cancelled;
            currentSources = selected; Executing = true; Outcome = CoinOutcome.Executing; host.Items.World.AutomaticOperation = true;
            try
            {
#if DEBUG
                SeedCalls++;
#endif
                WriteSeed(fresh, source, target, empty);
                if (host.Runtime.Generation != runtime || host.Session != session || !ReferenceEquals(host.Player, player) ||
                    !host.Intent.Allows(session, generation) || !host.Items.Ownership.OwnsCoins(runtime, selected, lease) || !fresh.SeedEnvelope(source, target, empty, true)) return Unknown();
                Amount = CoinRules.Conserved(fresh.WalletTotal, CoinSnapshot.Total(fresh.Wallet, 58), 0, CoinSnapshot.Total(fresh.Target, 40));
                long expected;
                if (!CoinRules.TryValue(fresh.WalletValues[source].type, fresh.WalletValues[source].stack, out expected) || Amount != expected || Amount <= 0) return Unknown();
                return Outcome = host.HasMovableCoins(player) ? CoinOutcome.Partial : CoinOutcome.Completed;
            }
            catch
            {
                // Recovery is limited to our exact two writes while the same
                // native identity/intent and exclusive lease still hold. Any
                // outside mutation fails this proof; never refill a wallet.
                try
                {
                    if (host.Runtime.Generation == runtime && host.Session == session && ReferenceEquals(host.Player, player) &&
                        host.Intent.Allows(session, generation) && host.Items.Ownership.OwnsCoins(runtime, selected, lease))
                    {
                        if (fresh.Unchanged()) return Outcome = CoinOutcome.Failed;
                        if (fresh.SeedEnvelope(source, target, empty, false) || fresh.SeedEnvelope(source, target, empty, true))
                        {
                            RestoreSeed(fresh, source, target);
                            if (fresh.Unchanged()) { Amount = 0; return Outcome = CoinOutcome.Failed; }
                        }
                    }
                }
                catch { }
                return Unknown();
            }
            finally
            {
                host.Items.World.AutomaticOperation = false; Executing = false;
                host.Items.Ownership.EndCoins(runtime, lease, Outcome == CoinOutcome.Unconfirmed);
                currentSources = 0; host.Items.World.InvalidateObservation();
            }
        }
        // Two-phase exact reference move: an interrupted first write does not
        // create two live copies. These bounded mutation/recovery boundaries
        // keep exceptional completion independently verifiable.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WriteSeed(CoinSnapshot fresh, int source, int target, Item empty)
        { fresh.Wallet[source] = empty; fresh.Target[target] = fresh.WalletRefs[source]; }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RestoreSeed(CoinSnapshot fresh, int source, int target)
        { fresh.Target[target] = fresh.TargetRefs[target]; fresh.Wallet[source] = fresh.WalletRefs[source]; }
        private CoinOutcome Unknown()
        { UnconfirmedSources |= currentSources; host.Intent.Unconfirmed(); return Outcome = CoinOutcome.Unconfirmed; }
    }
}
