using System;
using System.Collections.Generic;
using JueMingR.Features.CoinDeposit;
using JueMingR.Platform.Items;

namespace JueMingR.ArchitectureTests
{
    internal static class CoinDepositChecks
    {
        internal static void Check(IList<string> failures)
        {
            var intent = new CoinIntent();
            intent.BeginSession(1); intent.Configure(true, 1);
            long before = intent.Generation;
            intent.Withdrawal(1, before, 100, 100);
            if (!intent.Protected || intent.Allows(1, before)) failures.Add("Coins: real withdrawal failed to invalidate an already prepared request.");
            intent.ObserveRange(false, false);
            if (!intent.Protected) failures.Add("Coins: unknown/partial empty range released protection.");
            for (int i = 0; i < 100000; i++) intent.ObserveRange(true, true);
            if (!intent.Protected) failures.Add("Coins: continuing bank presence consumed protection.");
            intent.ObserveRange(true, false);
            if (intent.Protected) failures.Add("Coins: reliable empty range did not release protection without a wallet.");
            intent.Withdrawal(1, intent.Generation, 0, 100);
            if (intent.Protected || intent.Pending) failures.Add("Coins: wallet increase without source decrease became withdrawal.");
            intent.Withdrawal(1, intent.Generation, 100, 0);
            if (!intent.Pending || intent.Allows(1, intent.Generation)) failures.Add("Coins: known source loss allowed an automatic write before arrival.");
            intent.ObserveRange(false, false);
            if (!intent.Pending) failures.Add("Coins: unreadable range discarded known manual source loss.");
            intent.ObserveRange(true, false);
            if (intent.Pending) failures.Add("Coins: retired manual transit kept intent forever after complete range exit.");
            intent.Withdrawal(1, intent.Generation, 100, 0);
            intent.Configure(false, 2); intent.Configure(true, 3);
            if (intent.Pending || intent.Protected) failures.Add("Coins: explicit off/on retained old user intent.");
            intent.Unconfirmed(); intent.Configure(false, 4); intent.Configure(true, 5);
            if (!intent.Faulted || intent.Allows(1, intent.Generation)) failures.Add("Coins: toggle silently cleared an unknown transaction.");
            intent.BeginSession(2);
            if (intent.Faulted || intent.Protected || intent.Pending) failures.Add("Coins: new session inherited ended operation state.");
            intent.Withdrawal(1, before, 100, 100);
            if (intent.Protected) failures.Add("Coins: ended session withdrawal was replayed.");
            long value;
            if (!CoinRules.TryValue(74, 9999, out value) || value != 9999000000L) failures.Add("Coins: platinum exceeds 32-bit wallet value.");
            if (CoinRules.TryValue(75, 1, out value) || CoinRules.TryValue(71, -1, out value)) failures.Add("Coins: non-currency or invalid stack admitted.");
            if (CoinRules.Conserved(100, 50, 200, 250) != 50 || CoinRules.Conserved(100, 50, 200, 249) != -1)
                failures.Add("Coins: source loss alone was treated as confirmed arrival.");
            var owner = new ItemOperationOwnership(); owner.SetSession(1);
            owner.TryBeginStore(1, 1UL << 10);
            if (!owner.TryBeginCoins(1, 1UL << 50, 7)) failures.Add("Coins: unrelated noncoin store freezes currency.");
            if (owner.TryBeginSale(1) || owner.TryBeginUse(1, 10, 3)) failures.Add("Coins: shared operation ranges overlapped.");
            owner.EndCoins(1, 6, false);
            if (!owner.IsProtected(50)) failures.Add("Coins: wrong operation token released currency ownership.");
            owner.EndCoins(1, 7, true);
            if (!owner.IsProtected(50)) failures.Add("Coins: unknown transaction dropped its write range.");
            owner.SetSession(2);
            if (owner.ProtectedSlots != 0) failures.Add("Coins: session retained old physical ownership.");
            var codec = new CoinPreferenceCodec();
            if (!codec.Decode(codec.Encode(true)) || codec.Decode(codec.Encode(false))) failures.Add("Coins: independent toggle codec changed value.");
            CoinSettingsChecks.Check(failures);
        }
    }
}
