using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.CoinDeposit;
using Terraria;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinLifecycleChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static int fault;
        private static Item held;
        internal static void Run(object context, object host)
        {
            Player p = Main.LocalPlayer; var intent = (CoinIntent)Get(host, "Intent");
            object range = Get(host, "Range"), transfer = Get(host, "Transfer");
            NativeCoinMatrix.Reset(p, host); Main.tile[40, 40].type = 29; p.bank.item[0] = Coin(73, 2);
            object[] query = { p, false, false }; range.GetType().GetMethod("TryComplete", Flags).Invoke(range, query);
            object entrance = ((Array)Get(range, "Banks")).GetValue(0); long oldGeneration = intent.Generation;
            p.chest = -2; ItemSlot.PickupItemIntoMouse(p.bank.item, 4, 0, p); p.chest = -1;
            p.inventory[50] = Main.mouseItem; Main.mouseItem = new Item();
            long before = Total(p.inventory, 58) + Total(p.bank.item, 40), calls = (long)Get(transfer, "NativeCalls");
            Require(Call(transfer, "Execute", entrance, oldGeneration).ToString() == "Rejected" && (long)Get(transfer, "NativeCalls") == calls &&
                Total(p.inventory, 58) + Total(p.bank.item, 40) == before, "same-update real withdrawal rejects previously prepared generation before native entry");

            var originalWorld = Main.ActiveWorldFileData;
            Main.ActiveWorldFileData = null; Call(context, "UpdateRuntime");
            Main.ActiveWorldFileData = originalWorld; Call(context, "UpdateRuntime");
            Require(intent.Protected, "temporary world identity failure and same-object recovery preserves intent");

            var audit = new Harmony("JueMingR.Tests.CoinWithdrawalCompletion");
            MethodInfo pickup = typeof(ItemSlot).GetMethod("PickupItemIntoMouse", Flags, null, new[] { typeof(Item[]), typeof(int), typeof(int), typeof(Player) }, null);
            audit.Patch(pickup, postfix: new HarmonyMethod(typeof(NativeCoinLifecycleChecks).GetMethod(nameof(AfterPickup), Flags)) { before = new[] { "JueMingR.CoinWithdrawal" } });
            try
            {
                foreach (int mode in new[] { 1, 2 })
                {
                    NativeCoinMatrix.Reset(p, host); Main.tile[40, 40].type = 29; p.bank.item[0] = Coin(72, 5); p.chest = -2;
                    fault = mode; ItemSlot.PickupItemIntoMouse(p.bank.item, 4, 0, p);
                    Main.ActiveWorldFileData = originalWorld;
                    if (held != null) { Main.mouseItem = held; held = null; }
                    Require(intent.Pending && !intent.Faulted, "real source loss with unavailable completion preserves finite pending intent: " + mode);
                    p.chest = -1; p.inventory[50] = Main.mouseItem; Main.mouseItem = new Item(); p.inventory[51] = Coin(73, 1);
                    NativeCoinMatrix.Tick(host, 0, 120);
                    Require(intent.Pending && p.inventory[51].stack == 1, "closing source and later coins cannot settle old unconfirmed arrival");
                    Main.tile[40, 40].type = 0; Tile saved = Main.tile[41, 40]; Main.tile[41, 40] = null;
                    NativeCoinMatrix.Tick(host, 120, 140);
                    Require(intent.Pending, "partly unreadable zero-bank query retains pending intent");
                    Main.tile[41, 40] = saved; NativeCoinMatrix.Tick(host, 140, 220);
                    Require(!intent.Pending && !intent.Protected, "complete zero range retires only the old manual intent");
                }
            }
            finally { fault = 0; Main.ActiveWorldFileData = originalWorld; if (held != null) Main.mouseItem = held; held = null; audit.Unpatch(pickup, HarmonyPatchType.All, audit.Id); }
            NativeCoinMatrix.Reset(p, host); Main.tile[40, 40].type = 29;
            Main.netMode = 1; Netplay.Connection = new RemoteServer { PendingTermination = true }; Call(context, "UpdateRuntime");
            intent.Withdrawal((long)Get(host, "Session"), intent.Generation, 100, 100);
            var socket = Netplay.Connection.Socket;
            Netplay.Connection.Socket = null; Call(context, "UpdateRuntime");
            Netplay.Connection.Socket = socket; Call(context, "UpdateRuntime");
            Require(intent.Protected, "temporary null socket does not become a new confirmed connection");
            Netplay.Connection = new RemoteServer { PendingTermination = true }; Call(context, "UpdateRuntime");
            Require(!intent.Protected && !intent.Pending, "positive new connection retires previous intent without replay");
            Main.netMode = 0; Call(context, "UpdateRuntime"); NativeCoinMatrix.Reset(p, host);
            Console.WriteLine("PASS: same-update stale permit, temporary world/socket recovery, native pending loss and unreadable/complete range exit.");
        }
        private static void AfterPickup()
        {
            if (fault == 1) Main.ActiveWorldFileData = null;
            if (fault == 2) { held = Main.mouseItem; Main.mouseItem = null; }
            fault = 0;
        }
    }
}
