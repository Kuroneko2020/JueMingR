using System;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.QuickItems;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinUseChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static int shots;
        internal static void Run(object context, object host)
        {
            Player p = Main.LocalPlayer;
            object input = Get(context, "Input"), shell = Get(context, "Shell"), quick = Get(context, "QuickItems"), use = Get(quick, "Use");
            // Reproduce the shared installation catch: no material guard exists.
            // Turning off ordinary item automation is NOT this failure state.
            object items = Get(host, "Items"), world = Get(items, "World");
            object materialGuard = Get(world, "AdditionalProtection");
            var availability = items.GetType().GetField("<Available>k__BackingField", Flags);
            try
            {
                Prepare(p, host); p.selectedItemState.Select(17); p.selectedItemState.Update(); p.itemAnimation = 10;
                Main.tile[40, 40].type = 29; p.bank.item[0] = Coin(71, 1);
                availability.SetValue(items, false); Set(world, "AdditionalProtection", null);
                long calls = (long)Get(Get(host, "Transfer"), "NativeCalls");
                NativeCoinMatrix.Tick(host, 0, 180); Call(host, "Poll");
                Require((long)Get(Get(host, "Transfer"), "NativeCalls") == calls && p.inventory[50].stack == 30,
                    "missing shared protection capability cannot enter native deposit or take CoinGun ammunition");
                Require(!(bool)Get(host, "Available") && !(bool)Get(host, "ControlsEnabled") && !(bool)Get(host, "Observing") && Get(host, "Status").ToString() == "暂不可用",
                    "missing dependency is accurately unavailable in observation and controls");
            }
            finally { availability.SetValue(items, true); Set(world, "AdditionalProtection", materialGuard); NativeCoinMatrix.Reset(p, host); }
            var settings = (QuickItemSettings)Get(quick, "Settings");
            var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
            var entry = new QuickItemEntry("cccccccccccccccccccccccccccccccc", ItemID.CoinGun, QuickItemMode.Use, false, true);
            string reason;
            Require(settings.TryChange(settings.Current.Toggles(settings.KeepFavorited, true).Change(entry), entry.Id, out reason), "coin gun quick item commit: " + reason);
            NativeQuickItemChecks.Until(() => { Call(quick, "Poll"); return !settings.Busy; });
            NativeQuickGestureChecks.Bind(bindings, entry.ActionId, "J");
            var audit = new Harmony("JueMingR.Tests.CoinGunUse");
            MethodInfo shoot = typeof(Player).GetMethod("ItemCheck_Shoot", Flags);
            audit.Patch(shoot, postfix: new HarmonyMethod(typeof(NativeCoinUseChecks).GetMethod(nameof(Shot), Flags)));
            try
            {
                Prepare(p, host); p.selectedItemState.Select(17); p.selectedItemState.Update(); shots = 0;
                for (int i = 0; i < 100; i++)
                { NativeQuickItemChecks.Sample(input, new Keys[0]); PlayerInput.Triggers.Current.MouseLeft = i == 0; NativeQuickItemChecks.NativeFrame(p); }
                int expectedShots = shots, spent = 30 - p.inventory[50].stack;
                Require(expectedShots > 0 && spent > 0, "ordinary true CoinGun spends real copper ammunition");
                Prepare(p, host); Main.tile[40, 40].type = 29; p.bank.item[0] = Coin(71, 1); shots = 0;
                // Populate actual discovery while native use is idle, then start
                // the shared binding. Admission must protect ammunition, beyond
                // G05's narrow selected-provider ownership.
                object range = Get(host, "Range"); object[] query = { p, false, false };
                range.GetType().GetMethod("TryComplete", Flags).Invoke(range, query);
                NativeQuickUseMatrix.Press(input, shell, Keys.J);
                Require((bool)Get(use, "Active"), "public quick binding accepts real CoinGun provider");
                long calls = (long)Get(Get(host, "Transfer"), "NativeCalls");
                for (ulong tick = 0; tick < 160; tick++)
                {
                    NativeQuickItemChecks.NativeFrame(p);
                    NativeQuickItemChecks.Sample(input, new Keys[0]); Call(shell, "ProcessInput");
                    bool usingAmmo = p.itemAnimation > 0 || p.itemTime > 0 || p.channel;
                    Call(host, "Update", tick);
                    if (usingAmmo) Require((long)Get(Get(host, "Transfer"), "NativeCalls") == calls, "deposit waits for actual CoinGun ammunition use, including final shot");
                }
                Require(shots == expectedShots && Total(p.inventory, 58) == 0 && Total(p.bank.item, 40) == 31 - spent,
                    "G05 CoinGun shot/ammunition matches ordinary native use and only remainder deposits");
                NativeQuickUseMatrix.Returned(p, use, 2, "G06 coexists with CoinGun natural selection return");
                Require(!((JueMingR.Features.CoinDeposit.CoinIntent)Get(host, "Intent")).Protected, "ordinary ammunition spending does not manufacture withdrawal intent");
            }
            finally
            {
                audit.Unpatch(shoot, HarmonyPatchType.All, audit.Id);
                Require((bool)Call(quick, "Delete", entry.Id), "remove isolated coin gun entry");
                NativeQuickItemChecks.Until(() => { Call(quick, "Poll"); bindings.Poll(); return !settings.Busy && !bindings.Busy; });
                NativeCoinMatrix.Reset(p, host);
            }
            Console.WriteLine("PASS: true CoinGun ordinary/quick differential, actual ammunition protection, final shot and natural selection return.");
        }
        private static void Prepare(Player p, object host)
        {
            NativeCoinMatrix.Reset(p, host); NativeQuickUseMatrix.Reset(p);
            p.inventory[17].SetDefaults(ItemID.CoinGun); p.inventory[50] = Coin(71, 30);
        }
        private static void Shot(Item sItem) { if (sItem.type == ItemID.CoinGun) shots++; }
    }
}
