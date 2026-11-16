using System;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinEnvelopeChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(object host)
        {
            Player p = Main.LocalPlayer;
            Type snapshot = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.CoinDeposit.CoinSnapshot");
            foreach (string mutation in new[] { "other-bank-alias", "other-bank-fields", "coin-fields", "normal" })
            {
                NativeCoinMatrix.Reset(p, host);
                p.inventory[50] = Coin(74, 2); p.bank.item[0] = Coin(71, 1);
                p.bank2.item[0].SetDefaults(8); p.bank2.item[0].stack = 7;
                object before = snapshot.GetMethod("Capture", Flags).Invoke(null, new object[] { p, p.bank, 1UL << 50 });
                ChestUI.MoveCoins(p.inventory, p.bank);
                Item deposited = p.bank.item.First(item => item.type == 74 && item.stack > 0);
                if (mutation == "other-bank-alias") p.bank2.item[0] = deposited;
                if (mutation == "other-bank-fields") p.bank2.item[0].damage++;
                if (mutation == "coin-fields") deposited.favorited = true;
                bool accepted;
                try { accepted = (bool)Call(before, "NativeEnvelope"); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException && error.InnerException.Message == "coin-invalid-member")
                { accepted = false; } // The real operation boundary maps invalid post-write members to Unconfirmed.
                Require(accepted == (mutation == "normal"), "real native result envelope detects " + mutation);
            }
            NativeCoinMatrix.Reset(p, host); Main.tile[40, 40].type = 29;
            p.inventory[50] = Coin(74, 2); p.bank.item[0] = Coin(74, 9998);
            for (int i = 1; i < 40; i++) { p.bank.item[i].SetDefaults(8); p.bank.item[i].stack = 7; }
            Item[] noncoins = p.bank.item.Skip(1).ToArray();
            long sum = Total(p.inventory, 58) + Total(p.bank.item, 40);
            NativeCoinMatrix.Tick(host, 0, 90);
            Require(p.inventory[50].stack == 1 && p.bank.item[0].stack == 9999 &&
                Get(Get(host, "Transfer"), "Outcome").ToString() == "Partial", "true native partial capacity records exact remaining platinum");
            Require(Total(p.inventory, 58) + Total(p.bank.item, 40) == sum && noncoins.Select((item, i) => ReferenceEquals(item, p.bank.item[i + 1]) && item.type == 8 && item.stack == 7).All(value => value), "partial transfer preserves full total and noncoin physical members");
            object transfer = Get(host, "Transfer");
            long partialCalls = (long)Get(transfer, "NativeCalls");
            // Partial is not a no-progress fact: C3 requires the next normal
            // round to inspect this real remainder once. Establish the actual
            // None receipt before testing its stable cache/entrance identity.
            ulong settled = 90;
            for (; settled < 210 && Get(transfer, "Outcome").ToString() != "None"; settled++)
                NativeCoinMatrix.Tick(host, settled, settled + 1);
            Require(Get(transfer, "Outcome").ToString() == "None" && (long)Get(transfer, "NativeCalls") == partialCalls + 1 &&
                p.inventory[50].stack == 1 && p.bank.item[0].stack == 9999 && Total(p.inventory, 58) + Total(p.bank.item, 40) == sum,
                "partial capacity is re-evaluated exactly once before a real zero-transfer no-progress receipt");
            long calls = (long)Get(transfer, "NativeCalls");
            for (ulong tick = settled; tick < settled + 400; tick++)
            {
                Main.tile[40, 40].type = tick < settled + 200 ? (ushort)29 : (ushort)0;
                Main.tile[41, 40].type = 29;
                NativeCoinMatrix.Tick(host, tick, tick + 1);
            }
            Require((long)Get(Get(host, "Transfer"), "NativeCalls") == calls, "same full account changing entrance cannot reopen a no-progress native attempt");
            Main.tile[42, 40].type = 97; p.bank2.item[0] = Coin(71, 1);
            NativeCoinMatrix.Tick(host, settled + 400, settled + 560);
            Require(Total(p.inventory, 58) == 0 && Total(p.bank2.item, 40) == 1000001, "next discovered funded account receives actual partial remainder");
            Console.WriteLine("PASS: real native all-account and attribute envelopes; partial capacity, same-account entrance changes, next-account remainder.");
        }
    }
}
