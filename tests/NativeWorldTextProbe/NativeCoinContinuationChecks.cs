using System;
using System.Collections.Generic;
using JueMingR.Features.CoinDeposit;
using JueMingR.Platform.Items;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    // These cases enter Host.Update: calling Transfer twice would bypass the
    // no-progress cache and priority starvation that this regression protects.
    internal static class NativeCoinContinuationChecks
    {
        internal static void Run(object context, object host)
        {
            var failures = new List<Exception>();
            Check("four-first-deposits", () => FirstDeposits(host), failures);
            Check("partial-static-remainder", () => Partial(host), failures);
            Check("nonpiggy-partial-native-payment", () => PartialPayment(host), failures);
            Check("full-first-continuous-income", () => Fallback(host), failures);
            Check("target-local-conflict", () => TargetConflict(host), failures);
            Check("source-and-intent-stop", () => SourceAndIntent(host), failures);
            NativeCoinMatrix.Reset(Main.LocalPlayer, host);
            if (failures.Count != 0) throw new AggregateException("Coin continuation regressions failed", failures);
            Console.WriteLine("PASS: four real first deposits, static Partial continuation, bounded continuous-income fallback and exact conflict scope through Host.Update.");
        }
        private static void Check(string name, Action action, List<Exception> failures)
        {
            try { action(); Console.WriteLine("PASS: coin continuation " + name); }
            catch (JueMingR.Testing.BusinessAssertionException error) { Console.WriteLine("FAIL: coin continuation " + name + ": " + error.Message); failures.Add(error); }
        }
        private static void FirstDeposits(object host)
        {
            Player p = Main.LocalPlayer; int[] tiles = {29, 97, 463, 491};
            for (int b = 0; b < 4; b++)
            {
                NativeCoinMatrix.Reset(p, host); Main.tile[40, 40].type = (ushort)tiles[b];
                p.inventory[50] = Coin(73, 2); Item source = p.inventory[50];
                Bank(p, b).item[0].SetDefaults(8); Bank(p, b).item[0].stack = 7;
                Item noncoin = Bank(p, b).item[0];
                Item[][] untouched = new Item[4][];
                for (int a = 0; a < 4; a++) untouched[a] = (Item[])Bank(p, a).item.Clone();
                NativeCoinMatrix.Tick(host, 0, 180);
                Require(p.inventory[50].IsAir && ReferenceEquals(Bank(p, b).item[1], source) && Total(Bank(p, b).item, 40) == 20000,
                    "Host.Update first deposit reaches real account " + b);
                Require(ReferenceEquals(Bank(p, b).item[0], noncoin) && noncoin.stack == 7, "first deposit preserves target noncoin " + b);
                for (int a = 0; a < 4; a++) if (a != b) for (int i = 0; i < 40; i++)
                    Require(ReferenceEquals(untouched[a][i], Bank(p, a).item[i]), "first deposit leaves other account identity unchanged " + b + "/" + a);
            }
        }
        private static void Partial(object host)
        {
            Player p = Main.LocalPlayer; NativeCoinMatrix.Reset(p, host);
            Main.tile[40, 40].type = 29; Main.tile[41, 40].type = 97;
            p.inventory[50] = Coin(73, 2); p.inventory[51] = Coin(72, 3);
            object transfer = Get(host, "Transfer"), range = Get(host, "Range");
            long calls = (long)Get(transfer, "NativeCalls"), queries = (long)Get(range, "CompleteQueries");
            ulong tick = 0;
            for (; tick < 180 && (long)Get(transfer, "NativeCalls") == calls; tick++) NativeCoinMatrix.Tick(host, tick, tick + 1);
            Require(Get(transfer, "Outcome").ToString() == "Partial" && Total(p.bank.item, 40) == 20000 && Total(p.inventory, 58) == 300,
                "first bounded Host.Update operation seeds only the highest denomination");
            NativeCoinMatrix.Tick(host, tick, tick + 59);
            Require((long)Get(transfer, "NativeCalls") == calls + 1, "Partial does not bypass the existing 60-update commit cadence");
            NativeCoinMatrix.Tick(host, tick + 59, tick + 180);
            Require(Total(p.inventory, 58) == 0 && Total(p.bank.item, 40) == 20300 && Total(p.bank2.item, 40) == 0,
                "unchanged remainder continues natively into the same priority account without new income");
            Require((long)Get(transfer, "NativeCalls") == calls + 2 && (long)Get(range, "CompleteQueries") == queries + 2,
                "Partial continuation uses exactly two bounded ordered rounds");
            NativeCoinMatrix.Tick(host, tick + 180, tick + 600);
            Require((long)Get(transfer, "NativeCalls") == calls + 2, "completed continuation stops native work");
        }
        private static void Fallback(object host)
        {
            Player p = Main.LocalPlayer; NativeCoinMatrix.Reset(p, host);
            Main.tile[40, 40].type = 29; Main.tile[41, 40].type = 97;
            for (int i = 0; i < 40; i++) p.bank.item[i] = Coin(74, 9999);
            p.bank2.item[0] = Coin(71, 1);
            long full = Total(p.bank.item, 40), income = 0;
            object transfer = Get(host, "Transfer"), range = Get(host, "Range");
            long calls = (long)Get(transfer, "NativeCalls"), queries = (long)Get(range, "CompleteQueries");
            for (ulong tick = 0; tick < 600; tick++)
            {
                if (p.inventory[50].IsAir) p.inventory[50] = Coin(74, 1); else p.inventory[50].stack++;
                income += 1000000;
                NativeCoinMatrix.Tick(host, tick, tick + 1);
            }
            long deposited = Total(p.bank2.item, 40) - 1;
            Require(deposited > 0 && deposited + Total(p.inventory, 58) == income && Total(p.bank.item, 40) == full,
                "continuous actual wallet changes cannot starve the later bank after proven zero transfer");
            Require((long)Get(transfer, "NativeCalls") - calls <= 40 && (long)Get(range, "CompleteQueries") - queries <= 10,
                "600 updates remain at most ten complete queries and four native calls per round");
        }
        private static void PartialPayment(object host)
        {
            Player p = Main.LocalPlayer; NativeCoinMatrix.Reset(p, host);
            Main.tile[40, 40].type = 97;
            p.inventory[50] = Coin(73, 2); p.inventory[51] = Coin(72, 3);
            object transfer = Get(host, "Transfer"); long calls = (long)Get(transfer, "NativeCalls");
            ulong tick = 0;
            for (; tick < 180 && (long)Get(transfer, "NativeCalls") == calls; tick++) NativeCoinMatrix.Tick(host, tick, tick + 1);
            Require(Get(transfer, "Outcome").ToString() == "Partial" && Total(p.bank2.item, 40) == 20000 && Total(p.inventory, 58) == 300,
                "nonpiggy seed leaves the actual lower-denomination remainder for the next round");
            Require(p.BuyItem(150) && Total(p.inventory, 58) + Total(p.bank2.item, 40) == 20150 && Total(p.inventory, 58) > 0,
                "real native payment consumes value between Partial and continuation");
            NativeCoinMatrix.Tick(host, tick, tick + 180);
            Require(Total(p.inventory, 58) == 0 && Total(p.bank2.item, 40) == 20150 && (long)Get(transfer, "NativeCalls") == calls + 2,
                "Partial continuation admits current post-payment objects and balance without restoring spent coins");
        }
        private static void TargetConflict(object host)
        {
            Player p = Main.LocalPlayer; var own = (ItemOperationOwnership)Get(Get(host, "Items"), "Ownership");
            foreach (bool protectedCoin in new[] { false, true })
            {
                NativeCoinMatrix.Reset(p, host);
                Main.tile[40, 40].type = 491; Main.tile[41, 40].type = 97;
                if (protectedCoin) p.bank4.item[0] = Coin(71, 1);
                p.inventory[50] = Coin(73, 2); p.bank2.item[0] = Coin(71, 1);
                Item protectedItem = p.bank4.item[0]; ulong[] slots = { 0, 0, 0, 0, 1 };
                Require(own.TryBeginRecovery(own.Session, slots, 7001), "exact bank4 coin/Air protection fixture");
                try
                {
                    NativeCoinMatrix.Tick(host, 0, 180);
                    Require(Total(p.inventory, 58) == 0 && Total(p.bank2.item, 40) == 20001 && ReferenceEquals(p.bank4.item[0], protectedItem) && own.IsProtected(4, 0),
                        "only the protected target write set is skipped; later account deposits without releasing its owner");
                }
                finally { own.EndRecovery(own.Session, slots, 7001, false); }
            }
            NativeCoinMatrix.Reset(p, host); Main.tile[40, 40].type = 491;
            p.inventory[50] = Coin(73, 2); p.bank4.item[0].SetDefaults(8); p.bank4.item[0].stack = 3;
            Item noncoin = p.bank4.item[0]; ulong[] noncoinSlots = { 0, 0, 0, 0, 1 };
            Require(own.TryBeginRecovery(own.Session, noncoinSlots, 7002), "bank4 noncoin protection fixture");
            try
            {
                NativeCoinMatrix.Tick(host, 0, 180);
                Require(Total(p.inventory, 58) == 0 && Total(p.bank4.item, 40) == 20000 && ReferenceEquals(p.bank4.item[0], noncoin) && noncoin.stack == 3,
                    "protected noncoin outside MoveCoins write set does not block a valid first deposit");
            }
            finally { own.EndRecovery(own.Session, noncoinSlots, 7002, false); }
        }
        private static void SourceAndIntent(object host)
        {
            Player p = Main.LocalPlayer; NativeCoinMatrix.Reset(p, host);
            Main.tile[40, 40].type = 491; Main.tile[41, 40].type = 97;
            p.inventory[50] = Coin(73, 2); p.bank4.item[0] = Coin(71, 1); p.bank2.item[0] = Coin(71, 1);
            var own = (ItemOperationOwnership)Get(Get(host, "Items"), "Ownership");
            ulong[] slots = { 1UL << 50, 0, 0, 0, 1 };
            Require(own.TryBeginRecovery(own.Session, slots, 7003), "shared source plus local target protection fixture");
            long calls = (long)Get(Get(host, "Transfer"), "NativeCalls");
            try
            {
                NativeCoinMatrix.Tick(host, 0, 180);
                Require((long)Get(Get(host, "Transfer"), "NativeCalls") == calls && p.inventory[50].stack == 2,
                    "source conflict stops the whole round instead of falling through to another bank");
            }
            finally { own.EndRecovery(own.Session, slots, 7003, false); }
            var intent = (CoinIntent)Get(host, "Intent"); intent.Withdrawal((long)Get(host, "Session"), intent.Generation, 100, 100);
            NativeCoinMatrix.Tick(host, 180, 360);
            Require(intent.Protected && (long)Get(Get(host, "Transfer"), "NativeCalls") == calls && p.inventory[50].stack == 2,
                "confirmed withdrawal intent prevents all target fallback");
        }
    }
}
