using System.Linq;
using System;
using JueMingR.Features.Items;
using JueMingR.Platform.Items;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.Items;
using Terraria.GameContent;
using Terraria.UI;

namespace Terraria
{
    internal sealed class ItemDeathBoundaryChecks
    {
        private readonly HostItems host;
        private readonly SingleFeatureRuntime runtime;
        private readonly Action<int> newSession, step;
        private readonly Action<bool> allowActions;
        internal ItemDeathBoundaryChecks(HostItems host, SingleFeatureRuntime runtime, Action<int> newSession, Action<int> step, Action<bool> allowActions)
        { this.host = host; this.runtime = runtime; this.newSession = newSession; this.step = step; this.allowActions = allowActions; }
        internal void Run()
        {
            NewSession(0); Configure(true, true, true); Main.playerInventory = true; Main.npcShop = 1;
            Player p = Main.LocalPlayer;
            p.inventory[10] = Make(100, 3); p.inventory[11] = Make(101, 3); p.inventory[12] = Make(8, 3);
            var sale = Sale(10); var discard = new DiscardItemRequest(runtime.Generation, Observe(11));
            var store = new StoreItemsRequest(runtime.Generation, new[] { Observe(12) });
            int sales = p.SellCalls, calls = QuickStacking.Calls; long generation = runtime.Generation;
            p.dead = true;
            host.Operations.Execute(sale); host.Operations.Execute(discard); host.Operations.Execute(store); Step();
            Check(p.SellCalls == sales && QuickStacking.Calls == calls && p.inventory[10].stack == 3 && p.inventory[11].stack == 3 && p.inventory[12].stack == 3,
                "death never relaxes sell/discard/store admission");
            Check(runtime.IsSessionActive && runtime.Generation == generation, "death preserves the one valid observation Session");
            p.dead = false; Step(); Check(runtime.Generation == generation, "revival does not create a fake second Session");
            p.inventory[50] = Make(71, 5); p.DropCoins();
            Check(runtime.IsSessionActive && p.inventory[50].IsAir, "native coin death boundary runs without invalidating world observation");
            p.DropItems(false); Check(runtime.IsSessionActive && p.inventory[10].IsAir, "native death drops remain native");

            NewSession(1); Configure(true, false, false); p = Main.LocalPlayer; generation = runtime.Generation;
            p.inventory[10] = Make(8, 20); p.inventory[54] = Make(8, 3);
            host.Operations.Execute(new StoreItemsRequest(generation, new[] { Observe(10), Observe(54) })); calls = QuickStacking.Calls;
            p.dead = true; Step();
            int clicks = ItemSlot.ManualClicks, sorted = ItemSorting.Calls; sales = p.SellCalls;
            ItemSlot.LeftClick(p.inventory, 0, 10); ItemSorting.SortInventory(); QuickStacking.QuickStackToNearbyChests(p, false); p.SellItem(Make(100, 1));
            Check(ItemSlot.ManualClicks == clicks && ItemSorting.Calls == sorted && QuickStacking.Calls == calls && p.SellCalls == sales,
                "dead pending source still blocks real native conflicting consumers");
            Reply(10, 8, 6); Step(); Check(host.Ownership.StoreBlocked, "dead first receipt cannot release sibling source");
            Reply(54, 0, 0); Step();
            Check(runtime.Generation == generation && !host.Ownership.StoreBlocked && host.Ownership.StoreResult.ConfirmedQuantity == 17 && QuickStacking.Calls == calls,
                "real receipts finish same batch while dead, without resending");
            p.dead = false; p.inventory[11] = Make(9, 3);
            host.Operations.Execute(new StoreItemsRequest(generation, new[] { Observe(11) })); calls = QuickStacking.Calls;
            p.dead = true; p.inventoryChestStack[11] = false; Step(610);
            Check(host.Ownership.StoreResult.State == ItemOperationState.TimedOut && host.Ownership.IsProtected(11) && QuickStacking.Calls == calls,
                "dead bare unlock is not a receipt or a retry permission");
            clicks = ItemSlot.ManualClicks; ItemSlot.LeftClick(p.inventory, 0, 11);
            Check(ItemSlot.ManualClicks == clicks, "R source guard survives death and native unlock");
            NewSession(1); Reply(11, 0, 0); Step();
            Check(runtime.Generation != generation && !host.Ownership.StoreBlocked, "true world/player/connection change retires old receipt ownership");

            foreach (bool nativeDrop in new[] { false, true })
            {
                NewSession(0); Configure(false, false, true, trash: new[] { 8 }); p = Main.LocalPlayer;
                p.inventory[10] = Make(8, 20); allowActions(false);
                p.Pickup(new WorldItem { inner = Make(8, 3) }); Step();
                Check(p.inventory[10].stack == 23, "unfocused acquisition waits before death");
                if (nativeDrop) { p.inventory[50] = Make(71, 1); p.DropCoins(); }
                p.dead = true; Step(); p.dead = false; allowActions(true); Step();
                Check(p.inventory[10].stack == 23, "revival cannot replay a pre-death acquisition");
                p.Pickup(new WorldItem { inner = Make(8, 1) }); Step();
                Check(p.inventory[10].IsAir, "new post-revival acquisition still works");
            }
            System.Console.WriteLine("PASS: death keeps shared observation, rejects new actions, retains real receipt guards and retires old acquisitions.");
        }
        private void NewSession(int mode) { newSession(mode); }
        private void Step(int count = 8) { step(count); }
        private void Configure(bool stack, bool sell, bool discard, int[] trash = null)
        { host.Change(new ItemAutomationSettings(stack, sell, discard, new[] { 100 }, trash ?? new[] { 101 })); host.PollPreferences(); }
        private static Item Make(int type, int amount) { return new Item { type = type, stack = amount, maxStack = 9999 }; }
        private ItemSlotObservation Observe(int slot)
        { ItemInventoryObservation value; Check(host.World.TryObserve(out value), "observe active inventory"); return value.Slots[slot]; }
        private SellItemRequest Sale(int slot)
        { ItemInventoryObservation value; Check(host.World.TryObserve(out value), "observe sale"); return new SellItemRequest(runtime.Generation, value.ShopIdentity, value.Slots[slot]); }
        private static void Reply(int slot, int type, int amount)
        {
            var message = new MessageBuffer(); byte[] data = message.readBuffer; data[0] = 5; data[1] = (byte)Main.LocalPlayer.whoAmI;
            BitConverter.GetBytes((short)slot).CopyTo(data, 2); BitConverter.GetBytes((short)amount).CopyTo(data, 4); BitConverter.GetBytes((short)type).CopyTo(data, 7);
            int ignored; message.GetData(0, 10, out ignored);
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Death boundary: " + message); }
    }
}
