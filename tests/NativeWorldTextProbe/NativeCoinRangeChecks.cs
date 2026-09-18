using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinRangeChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(object host)
        {
            Player p = Main.LocalPlayer; object range = Get(host, "Range");
            foreach (Vector2 position in new[] { new Vector2(640, 640), Vector2.Zero, new Vector2(1850, 1850) })
            {
                NativeCoinMatrix.Reset(p, host); p.position = position;
                Main.tile[0, 0].type = 29; Main.tile[39, 39].type = 97; Main.tile[40, 40].type = 463; Main.tile[119, 119].type = 491;
                Main.projectile[0].active = true; Main.projectile[0].type = 734; Main.projectile[0].position = p.position; Main.projectile[0].width = Main.projectile[0].height = 24;
                Main.projectile[500].active = true; Main.projectile[500].type = 525; Main.projectile[500].position = p.position; Main.projectile[500].width = Main.projectile[500].height = 24;
                // Copy native shared scratch immediately; the later world-chest
                // lookup intentionally reuses it and must not alter R's result.
                Chest[] expected = NearbyChests.GetBanksInRangeOf(p).Select(row => row.chest).ToArray();
                Query(range, p, false); NearbyChests.GetChestsInRangeOf(p.Center);
                Require(Banks(range).SequenceEqual(expected), "actual native projectile/tile order, account dedup and world edges: " + position);
                p.inventory[0].SetDefaults(5325); p.inventory[0].stack = 1;
                expected = NearbyChests.GetBanksInRangeOf(p).Select(row => row.chest).ToArray(); Query(range, p, true);
                Require(Banks(range).SequenceEqual(expected) && !expected.Contains(p.bank4), "closed void qualification matches actual native discovery");
            }
            NativeCoinMatrix.Reset(p, host); p.inventory[50] = Coin(73, 1); p.bank.item[0] = Coin(71, 1);
            NativeCoinMatrix.Tick(host, 0, 150); Require(Total(p.inventory, 58) == 10000, "no target does not manufacture a deposit");
            Main.tile[40, 40].type = 29; NativeCoinMatrix.Tick(host, 150, 260);
            Require(Total(p.inventory, 58) == 0, "stationary new furniture invalidates a negative discovery result");

            NativeCoinMatrix.Reset(p, host); Main.tile[40, 40].type = 29; p.inventory[50] = Coin(74, 1);
            p.bank.item[0] = Coin(74, 9999); for (int i = 1; i < 40; i++) { p.bank.item[i].SetDefaults(8); p.bank.item[i].stack = 7; }
            NativeCoinMatrix.Tick(host, 0, 150); Item last = p.bank.item[39]; p.bank.item[0] = Coin(74, 9998); p.bank.item[39] = null;
            NativeCoinMatrix.Tick(host, 150, 190); p.bank.item[39] = last; NativeCoinMatrix.Tick(host, 190, 320);
            Require(Total(p.inventory, 58) == 0, "late unreadable bank member cannot swallow earlier capacity change");

            NativeCoinMatrix.Reset(p, host); Main.tile[40, 40].type = 29; p.inventory[50] = Coin(73, 1); p.bank.item[0] = Coin(71, 1); Query(range, p, false);
            object entrance = ((Array)Get(range, "Banks")).GetValue(0);
            Chest old = p.bank; Set(p, "bank", new Player().bank); // Isolated identity-fault injection into the native readonly field.
            Require(Call(Get(host, "Transfer"), "Execute", entrance, Get(Get(host, "Intent"), "Generation")).ToString() == "Rejected" && p.inventory[50].stack == 1,
                "equal amount replacement account does not inherit a prepared entrance");
            Set(p, "bank", old);
            Type snapshot = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.CoinDeposit.CoinSnapshot");
            object prepared = snapshot.GetMethod("Capture", Flags).Invoke(null, new object[] { p, p.bank, 1UL << 50 });
            p.inventory[50] = Coin(73, 1);
            Require(!(bool)Call(prepared, "Unchanged"), "equal amount new wallet Item cannot inherit physical write permission");
            NativeCoinMatrix.Reset(p, host);
            Console.WriteLine("PASS: native range differential, edges/projectiles/void, scratch isolation, new furniture, unreadable capacity and replacement identities.");
        }
        private static void Query(object range, Player p, bool closed)
        { object[] args = { p, closed, false }; Require((bool)range.GetType().GetMethod("TryComplete", Flags).Invoke(range, args), "complete readable native range"); }
        private static Chest[] Banks(object range)
        { return ((Array)Get(range, "Banks")).Cast<object>().Take((int)Get(range, "Count")).Select(entry => (Chest)Get(entry, "Bank")).ToArray(); }
    }
}
