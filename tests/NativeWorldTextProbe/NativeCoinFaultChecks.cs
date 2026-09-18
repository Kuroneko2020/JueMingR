using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.CoinDeposit;
using Terraria;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinFaultChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static int fault;
        internal static void Run(object context, object host)
        {
            var p = Main.LocalPlayer; var intent = (CoinIntent)Get(host, "Intent"); var transfer = Get(host, "Transfer");
            NativeCoinMatrix.Reset(p, host);
            Main.tile[40, 40].type = 29; p.inventory[50] = Coin(73, 1); p.bank.item[0] = Coin(71, 1);
            // A real shared member alias must be rejected before entering the
            // native function, not discovered after it can duplicate or clear it.
            Item other = p.bank2.item[0]; p.bank2.item[0] = p.inventory[50];
            long calls = (long)Get(transfer, "NativeCalls"); NativeCoinMatrix.Tick(host, 0, 130);
            Require((long)Get(transfer, "NativeCalls") == calls && !intent.Faulted, "alias refused before native write without inventing a transaction fault");
            p.bank2.item[0] = other; NativeCoinMatrix.Tick(host, 130, 250);
            Require(Total(p.inventory, 58) == 0, "safe prewrite refusal recovers on actual corrected identity");

            var audit = new Harmony("JueMingR.Tests.CoinNativeFaults");
            MethodInfo native = typeof(ChestUI).GetMethod("MoveCoins", Flags, null, new[] { typeof(Item[]), typeof(Item[]), typeof(int) }, null);
            audit.Patch(native, postfix: new HarmonyMethod(typeof(NativeCoinFaultChecks).GetMethod(nameof(AfterNative), Flags)));
            try
            {
                NativeCoinMatrix.Reset(p, host); Main.tile[40, 40].type = 29; p.inventory[50] = Coin(73, 1); p.bank.item[0] = Coin(71, 1);
                long sum = Total(p.inventory, 58) + Total(p.bank.item, 40); fault = 1;
                NativeCoinMatrix.Tick(host, 0, 130);
                Require(intent.Faulted && Get(transfer, "Outcome").ToString() == "Unconfirmed", "exception after real native write retains unknown boundary");
                Require(Total(p.inventory, 58) + Total(p.bank.item, 40) == sum && Total(p.inventory, 58) == 0, "catch never refills pre-transfer wallet or duplicates deposited money");
                calls = (long)Get(transfer, "NativeCalls");
                var settings = (CoinSettings)Get(host, "Settings");
                Require(settings.Set(false), "faulted preference can turn off"); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !settings.Busy; });
                object shell=Get(context,"Shell"),state=Get(shell,"State"),page=Get(shell,"items"),renderer=Get(shell,"renderer");
                Set(state,"Ready",true);Call(state,"Navigate",0);Call(state,"RestoreVisible");Call(renderer,"RefreshResources");
                Call(renderer,"Prepare",state,960f,760f,1f);Call(page,"PrepareLayout",Microsoft.Xna.Framework.Matrix.Identity,new Microsoft.Xna.Framework.Vector2(960,760));
                Require(((IEnumerable)Get(Get(page,"CoinPanel"),"rows")).Cast<object>().Any(row=>(string)GetOptional(row,"Text")=="结果未确认"),
                    "real unknown transaction remains directly visible after turning the feature off");
                Call(state,"Close");
                Require(settings.Set(true), "faulted preference can turn on without settlement"); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !settings.Busy; });
                p.inventory[51] = Coin(72, 5); NativeCoinMatrix.Tick(host, 130, 500);
                Require(intent.Faulted && (long)Get(transfer, "NativeCalls") == calls && p.inventory[51].stack == 5, "off/on and new coins cannot replay an unknown transaction");
                var ownership = (JueMingR.Platform.Items.ItemOperationOwnership)Get(Get(host, "Items"), "Ownership");
                ulong held = ownership.ProtectedSlots;
                Require(held == 1UL << 50, "unknown transaction owns only its actual original source");
                var world = Main.ActiveWorldFileData;
                Main.ActiveWorldFileData = null; Call(context, "UpdateRuntime");
                Main.ActiveWorldFileData = world; Call(context, "UpdateRuntime");
                Require(intent.Faulted && ownership.ProtectedSlots == held && ownership.SaleBlocked && !ownership.TryBeginSale(ownership.Session),
                    "same native session recovery retains unknown transaction resource protection");
                Require((long)Get(transfer, "NativeCalls") == calls, "resource retention never restores an old transaction permit");
                Main.gamePaused = true;
                try
                {
                    Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(System.IO.Path.Combine(Terraria.Program.SavePath, "coin-new-world.wld"), false);
                    Call(context, "UpdateRuntime");
                    Require(!intent.Faulted && ownership.ProtectedSlots == 0 && (ulong)Get(transfer, "UnconfirmedSources") == 0 &&
                        (long)Get(transfer, "NativeCalls") == calls, "positively new world retires old conflict facts without a replay");
                }
                finally { Main.ActiveWorldFileData = world; Call(context, "UpdateRuntime"); Main.gamePaused = false; }
            }
            finally { fault = 0; audit.Unpatch(native, HarmonyPatchType.All, audit.Id); }
            Console.WriteLine("PASS: true native alias admission and post-write exception; no rollback, replay or false success.");
        }
        private static void AfterNative() { if (fault == 1) { fault = 0; throw new InvalidOperationException("isolated-after-real-money-write"); } }
    }
}
