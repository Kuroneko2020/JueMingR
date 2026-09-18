using System;
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
        internal long NativeCalls, Snapshots;
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
            try
            {
                // Call the real inventory, not a sliced array or temporarily
                // favored mask. This path has no bank RPC / fictional ACK.
#if DEBUG
                NativeCalls++;
#endif
                ChestUI.MoveCoins(player.inventory, entrance.Bank);
                if (!before.NativeEnvelope()) return Unknown();
                long afterWallet = CoinSnapshot.Total(player.inventory, 58), afterBank = CoinSnapshot.Total(entrance.Bank.item, entrance.Bank.maxItems);
                Amount = CoinRules.Conserved(before.WalletTotal, afterWallet, before.TargetTotal, afterBank);
                if (Amount < 0) return Unknown();
                if (Amount > 0) return Outcome = host.HasMovableCoins(player) ? CoinOutcome.Partial : CoinOutcome.Completed;
                // No seed follows raw-zero. Its separate admission must prove
                // the exact approved empty-slot normalization before a new permit.
                return Outcome = CoinOutcome.None;
            }
            catch { return Unknown(); }
            finally
            {
                host.Items.World.AutomaticOperation = false; Executing = false;
                host.Items.Ownership.EndCoins(host.Runtime.Generation, token, Outcome == CoinOutcome.Unconfirmed);
                currentSources = 0;
                host.Items.World.InvalidateObservation();
            }
        }
        private CoinOutcome Unknown()
        { UnconfirmedSources |= currentSources; host.Intent.Unconfirmed(); return Outcome = CoinOutcome.Unconfirmed; }
    }
}
