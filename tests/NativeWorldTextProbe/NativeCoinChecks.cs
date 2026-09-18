using System;
using System.Reflection;
using JueMingR.Features.CoinDeposit;
using Terraria;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinChecks
    {
        internal static void Run(object context, Action<object> visual = null)
        {
            object host = Get(context, "CoinDeposit");
            Require((bool)Get(host, "Available"), "G06 exact native hooks available: " + GetOptional(host, "SetupError"));
            var settings = (CoinSettings)Get(host, "Settings");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return settings.Loaded; });
            Require(!settings.Enabled, "G06 default off");
            Require(settings.Set(true), "G06 independent enable accepted");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !settings.Busy; });
            Player p = Main.LocalPlayer;
            for (int i = 0; i < 59; i++) p.inventory[i] = new Item();
            for (int b = 0; b < 4; b++) for (int i = 0; i < 40; i++) Bank(p, b).item[i] = new Item();
            p.selectedItemState.Select(0); p.chest = -1; Main.mouseItem = new Item();
            p.inventory[50] = Coin(73, 99); p.inventory[51] = Coin(72, 100);
            p.inventory[52] = Coin(74, 2); p.inventory[52].favorited = true;
            p.inventory[58] = Coin(74, 3); Main.mouseItem = new Item();
            p.bank.item[0] = Coin(71, 1); p.bank.item[1].SetDefaults(8); p.bank.item[1].stack = 7;
            Main.tile[40, 40].type = 29;
            long before = Total(p.inventory, 58) + Total(p.bank.item, 40);
            for (ulong tick = 0; tick < 180; tick++) Call(host, "Update", tick);
            Require(Total(p.inventory, 58) == 2000000 && Total(p.bank.item, 40) == 1000001, "real MoveCoins carry and favored source exclusion; wallet=" + Total(p.inventory,58) + "; bank=" + Total(p.bank.item,40) + "; status=" + Get(host,"Status") + "; calls=" + Get(Get(host,"Transfer"),"NativeCalls") + "; banks=" + Get(Get(host,"Range"),"Count") + "; input=" + Get(Get(host,"Input"),"CanStartActions"));
            Require(Total(p.inventory, 58) + Total(p.bank.item, 40) == before && p.inventory[58].stack == 3 && p.bank.item[1].stack == 7, "full native transfer conserves both ends and excluded/noncoin slots");
            p.chest = -2; Main.mouseItem = new Item();
            ItemSlot.PickupItemIntoMouse(p.bank.item, 4, 0, p);
            var intent = (CoinIntent)Get(host, "Intent");
            Require(intent.Protected, "real partial mouse withdrawal immediately protects");
            p.chest = -1; Main.mouseItem = new Item(); p.inventory[50] = Coin(74, 1);
            for (ulong tick = 180; tick < 400; tick++) Call(host, "Update", tick);
            Require(p.inventory[50].stack == 1 && intent.Protected, "closing bank and receiving new coins does not consume intent");
            Main.tile[40, 40].type = 0;
            for (ulong tick = 400; tick < 420; tick++) Call(host, "Update", tick);
            Require(!intent.Protected, "complete current zero-bank observation releases protection");
            NativeCoinMatrix.Run(context, host);
            NativeCoinEnvelopeChecks.Run(host);
            NativeCoinRangeChecks.Run(host);
            NativeCoinLifecycleChecks.Run(context, host);
            NativeCoinUseChecks.Run(context, host);
            NativeCoinNetworkChecks.Run(context, host);
            NativeCoinSaveChecks.Run(host);
            NativeCoinWorkloadChecks.Run(host);
            NativeCoinUiChecks.Run(context);
            visual?.Invoke(context);
            NativeCoinFaultChecks.Run(context, host);
            Console.WriteLine("PASS: G06 real native carry, two-end conservation, mouse withdrawal and range protection.");
        }
        internal static Item Coin(int type, int amount) { var item = new Item(); item.SetDefaults(type); item.stack = amount; return item; }
        internal static Chest Bank(Player p, int i) { return i == 0 ? p.bank : i == 1 ? p.bank2 : i == 2 ? p.bank3 : p.bank4; }
        internal static long Total(Item[] items, int count)
        { long total = 0; for (int i = 0; i < count; i++) { long value; if (CoinRules.TryValue(items[i].type, items[i].stack, out value)) total = checked(total + value); } return total; }
    }
}
