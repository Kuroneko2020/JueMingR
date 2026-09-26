using System;
using JueMingR.Features.CoinDeposit;
using Terraria;
using Terraria.UI;
using Microsoft.Xna.Framework;
using static NativeWorldTextProbe.NativeInformationChecks;
using static NativeWorldTextProbe.NativeCoinChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCoinMatrix
    {
        internal static void Run(object context, object host)
        {
            PopupText.popupText = new PopupText[20];
            for (int i = 0; i < PopupText.popupText.Length; i++) PopupText.popupText[i] = new PopupText();
            Player p = Main.LocalPlayer;
            var intent = (CoinIntent)Get(host, "Intent");
            var range = Get(host, "Range"); var transfer = Get(host, "Transfer");
            // Five real container arrays, each actual native entrance. No fake
            // successful MoveCoins / GetItem implementation is used here.
            for (int source = 0; source < 5; source++)
                foreach (string mode in new[] { "loot", "left", "right", "shift" })
                {
                    Reset(p, host); int index = source == 0 ? 0 : -1 - source;
                    Chest bank = source == 0 ? Chest.CreateWorldChest(0, 40, 40) : Bank(p, source - 1);
                    bank.item[0] = Coin(72, 10); p.chest = index; Main.playerInventory = true;
                    int slotContext = source == 0 ? 3 : source == 4 ? 32 : 4;
                    long before = Total(bank.item, 40) + Total(p.inventory, 58);
                    if (mode == "loot") ChestUI.LootAll();
                    else if (mode == "left") { Main.mouseLeft = Main.mouseLeftRelease = true; ItemSlot.LeftClick(bank.item, slotContext, 0); }
                    else if (mode == "shift") { Main.cursorOverride = source == 0 ? 8 : 7; typeof(ItemSlot).GetMethod("OverrideLeftClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, new object[] { bank.item, slotContext, 0 }); }
                    else
                    {
                        Main.mouseRight = true; Main.stackSplit = 0;
                        ItemSlot.RightClick(bank.item, slotContext, 0);
                    }
                    Require(intent.Protected && !intent.Pending, "real withdrawal " + source + "/" + mode + " confirms actual source and arrival");
                    Require(Total(bank.item, 40) + Total(p.inventory, 58) + Value(Main.mouseItem) == before, "withdrawal two ends " + source + "/" + mode);
                    long generation = intent.Generation;
                    Main.mouseRight = true; Main.stackSplit = 0; ItemSlot.RightClick(bank.item, slotContext, 0);
                    Require(intent.Generation == generation, "protected continuous split does not accumulate history " + source);
                }
            Reset(p, host);
            // A real left-click exchanges denomination objects even when the
            // account's net value stays equal or rises. Both are withdrawals.
            foreach (int silver in new[] { 100, 1 })
            {
                Reset(p, host); Main.tile[40, 40].type = 29;
                p.bank.item[0] = Coin(72, silver); Main.mouseItem = Coin(73, 1);
                Item withdrawn = p.bank.item[0], deposited = Main.mouseItem;
                long sum = Total(p.bank.item, 40) + Value(Main.mouseItem);
                p.chest = -2; Main.mouseLeft = Main.mouseLeftRelease = true;
                ItemSlot.LeftClick(p.bank.item, 4, 0);
                Require(ReferenceEquals(Main.mouseItem, withdrawn) && ReferenceEquals(p.bank.item[0], deposited), "true native left-click denomination exchange");
                Require(Total(p.bank.item, 40) + Value(Main.mouseItem) == sum, "exchange preserves both ends");
                Require(intent.Protected && !intent.Pending, "manual denomination exchange protects gross withdrawal despite net deposit: " + silver);
                p.chest = -1; p.inventory[50] = Main.mouseItem; Main.mouseItem = new Item();
                Main.mouseLeft = false; Tick(host, 0, 180);
                Require(intent.Protected && p.inventory[50].stack == silver, "closing exchanged account cannot return withdrawn coins");
            }
            foreach (int silver in new[] { 10, 80 })
            {
                Reset(p, host); p.bank.item[0] = Coin(72, silver); Main.mouseItem = Coin(72, 40);
                p.chest = -2; Main.mouseLeft = Main.mouseLeftRelease = true;
                ItemSlot.LeftClick(p.bank.item, 4, 0);
                Require(!intent.Protected && !intent.Pending, "same-denomination merge is ordinary deposit, not gross withdrawal: " + silver);
                Require(Total(p.bank.item, 40) + Value(Main.mouseItem) == (silver + 40) * 100L, "native merging conserves both ends");
            }
            Reset(p, host);
            // Two no-progress targets expose reciprocal native Item rebuilding;
            // a single full-bank test would miss this infinite retry bug.
            Main.tile[40, 40].type = 29; Main.tile[41, 40].type = 97;
            for (int b = 0; b < 2; b++) for (int i = 0; i < 40; i++) Bank(p, b).item[i] = Coin(74, 9999);
            p.inventory[50] = Coin(73, 99);
            long calls = (long)Get(transfer, "NativeCalls");
            Tick(host, 0, 1000);
            long settled = (long)Get(transfer, "NativeCalls");
            Tick(host, 1000, 2000);
            Require(settled - calls <= 2 && (long)Get(transfer, "NativeCalls") == settled && p.inventory[50].stack == 99,
                "two full native banks do not wake each other through their own zero-return normalization");
            p.bank.item[39] = new Item(); Tick(host, 2000, 2100);
            Require(Total(p.inventory, 58) == 0, "real released bank space resumes after no progress");

            Reset(p, host); Main.tile[40, 40].type = 29; p.bank.item[0] = Coin(71, 1); p.inventory[0] = Coin(73, 1);
            long blockedQueries = (long)Get(range, "CompleteQueries"), blockedTiles = (long)Get(range, "TileReads");
            p.selectedItemState.Select(0); Tick(host, 0, 150);
            Require(p.inventory[0].stack == 1, "selected money is not stolen by native bulk deposit");
            Require((long)Get(range, "CompleteQueries") == blockedQueries && (long)Get(range, "TileReads") == blockedTiles,
                "selected-only money never starts an impossible range query");
            p.selectedItemState.Select(1); p.selectedItemState.Update(); Tick(host, 150, 240);
            Require(Total(p.inventory, 58) == 0, "a harmless selection rejection recovers without a wallet mutation");

            Reset(p, host); Main.tile[40, 40].type = 29; p.bank.item[0] = Coin(71, 1); p.inventory[50] = Coin(73, 1);
            object items = Get(host, "Items"), world = Get(items, "World"), feature = Get(items, "Feature");
            Call(feature, "Configure", JueMingR.Features.Items.ItemAutomationSettings.Default.WithEnabled(JueMingR.Features.Items.ItemActionKind.Stack, true));
            ItemSlot.Handle(p.inventory,0,50);Require((int)Get(world,"ManualSlot")==-1,"released hover cannot invent a manual coin gesture");
            Terraria.GameInput.PlayerInput.MouseInfo=new Microsoft.Xna.Framework.Input.MouseState(0,0,0,Microsoft.Xna.Framework.Input.ButtonState.Pressed,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released,Microsoft.Xna.Framework.Input.ButtonState.Released);
            ItemSlot.LeftClick(p.inventory, 0, 50); // Frozen physical press remains visible after vanilla consumes its mutable mouse flags.
            Require((int)Get(world, "ManualSlot") == 50, "real shared inventory hook records manual coin slot");
            Terraria.GameInput.PlayerInput.MouseInfo=new Microsoft.Xna.Framework.Input.MouseState();
            Tick(host, 0, 180);
            Require(Total(p.inventory, 58) == 0 && (int)Get(world, "ManualSlot") == -1,
                "physical release retires shared manual facts without a noncoin acquisition");
            Call(feature, "Configure", JueMingR.Features.Items.ItemAutomationSettings.Default);

            Reset(p, host); Main.tile[40, 40].type = 29; p.bank.item[0] = Coin(71, 1); p.inventory[50] = Coin(73, 1);
            for (ulong tick = 0; tick < 100; tick++) { p.position.X += 2; Call(host, "Update", tick); }
            Require(Total(p.inventory, 58) == 0, "ordinary movement cannot repeatedly restart and starve discovery");

            Reset(p, host); Main.tile[40, 40].type = 29; Main.tile[41, 40].type = 491;
            p.inventory[0].SetDefaults(5325); p.inventory[0].stack = 1;
            intent.Withdrawal((long)Get(host,"Session"), intent.Generation, 100, 100);
            Tick(host, 0, 20); p.inventory[0] = new Item(); Main.tile[40, 40].type = 0;
            Tick(host, 20, 40);
            Require(intent.Protected, "newly opened void qualification prevents stale-zero protection release");
            Main.tile[41, 40].type = 0; Tick(host, 40, 60);
            Require(!intent.Protected, "actual zero range retires protection with an empty wallet");

            Reset(p, host);
            long scans = (long)Get(range, "TileReads"), snapshots = (long)Get(transfer, "Snapshots");
            Tick(host, 0, 1000);
            Require((long)Get(range, "TileReads") == scans && (long)Get(transfer, "Snapshots") == snapshots, "enabled empty wallet has no bank query or transaction snapshots");
            Console.WriteLine("PASS: five native withdrawal sources/four entrances; full-bank suppression, space/selection recovery, moving discovery, void qualification and empty-wallet work.");
        }
        internal static void Tick(object host, ulong start, ulong end) { for (ulong tick = start; tick < end; tick++) Call(host, "Update", tick); }
        internal static void Reset(Player p, object host)
        {
            for (int i = 0; i < 59; i++) p.inventory[i] = new Item();
            for (int b = 0; b < 4; b++) for (int i = 0; i < 40; i++) Bank(p, b).item[i] = new Item();
            for (int x = 0; x < Main.maxTilesX; x++) for (int y = 0; y < Main.maxTilesY; y++) Main.tile[x, y].type = 0;
            for (int i = 0; i < 1000; i++) Main.projectile[i].active = false;
            p.chest = -1; p.position = new Vector2(640, 640); p.dead = false; p.itemAnimation = p.itemTime = 0; p.channel = false;
            p.selectedItemState.Select(0); p.selectedItemState.Update(); Main.mouseItem = new Item(); Main.mouseLeft = Main.mouseRight = false;
            Main.cursorOverride = 0; Main.stackSplit = 0; Main.gamePaused = false;
            ((CoinIntent)Get(host, "Intent")).ObserveRange(true, false); Call(Get(host, "Range"), "Clear"); Call(host, "ResetAttempts");
            Set(host, "nextWallet", 0UL); Set(host, "nextCommit", 0UL); Set(host, "hasCoins", false); Set(host, "voidClosed", false);
        }
        private static long Value(Item item) { long value; return CoinRules.TryValue(item.type, item.stack, out value) ? value : 0; }
    }
}
