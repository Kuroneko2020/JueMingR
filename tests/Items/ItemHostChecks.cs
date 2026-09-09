using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Infrastructure.Settings;
using JueMingR.Platform.Settings;
using JueMingR.Features.Items;
using JueMingR.Platform.Items;
using JueMingR.Platform.Runtime;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace Terraria
{
    internal static class ItemHostChecks
    {
        private static HostItems host;
        private static SingleFeatureRuntime runtime;
        private static ulong tick;
        private static string root;
        private static readonly List<PreferenceDocument<ItemAutomationSettings>> documents = new List<PreferenceDocument<ItemAutomationSettings>>();
        internal static void Run(string content = null, string output = null, bool graphics = true)
        {
            root = Path.Combine(Path.GetTempPath(), "JueMingR-Items-Fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            bool passed = false;
            try { RunChecks(content, output, graphics); passed = true; }
            finally
            {
                // Stop every worker before releasing its exact isolated root,
                // including documents whose assertion failed before normal Stop.
                if (host != null) typeof(HostItems).GetMethod("OnExit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(host, new object[] { null, EventArgs.Empty });
                bool stopped = true;
                foreach (var document in documents) stopped &= document.Stop(750);
                if (passed && stopped)
                {
                    string fullRoot = Path.GetFullPath(root);
                    Check(string.Equals(Path.GetDirectoryName(fullRoot), Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                        && Path.GetFileName(fullRoot).StartsWith("JueMingR-Items-Fixture-", StringComparison.Ordinal), "cleanup stays within this fixture TEMP root");
                    Directory.Delete(fullRoot, true);
                    Check(!Directory.Exists(fullRoot), "fixture root removed after all workers stopped");
                    Console.WriteLine("PASS: item fixture workers stopped and exact TEMP root removed.");
                }
                else Console.WriteLine("Item fixture evidence retained at {0}; all workers stopped={1}.", root, stopped);
                Check(stopped, "item fixture worker still owns retained TEMP root: " + root);
            }
        }
        private static void RunChecks(string content, string output, bool graphics)
        {
            new Main(); Main.gameMenu = false; Main.LocalPlayer = new Player { active = true };
            runtime = new SingleFeatureRuntime(new ItemSessionProbe(), new Idle());
            host = new HostItems(root, runtime);
            documents.Add((PreferenceDocument<ItemAutomationSettings>)typeof(HostItems).GetField("preferences", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host));
            Check(host.Available, "all native hooks admitted: " + host.SetupError);
            runtime.AddFeature(host); runtime.Update(tick++);
            for (int i = 0; i < 200 && !host.Preferences.IsLoaded; i++) { Thread.Sleep(5); host.PollPreferences(); }
            Check(host.Preferences.IsLoaded, "isolated preferences load");
            PreferenceIsolation(); Transactions(); SelectionAndSources(); SourceEdges(); CapacityBackoff(); StorageBoundaries(); NetworkOwnership(); Guards(); UiKeys();
            ItemUiChecks.RunLayout(host);
            if (graphics) ItemUiChecks.Run(host, content, output);
            // Partial original side effects protect their finite affected range.
            NewSession(0); Configure(true, false, false); Main.LocalPlayer.inventory[10] = Make(1000, 2);
            Main.LocalPlayer.inventory[11] = Make(327, 2); Main.LocalPlayer.ThrowGrant = true; ID.ItemID.Sets.OpenableBag[1000] = true;
            bool threw = false;
            try { ItemSlot.RightClick(Main.LocalPlayer.inventory, 0, 10); } catch (InvalidOperationException) { threw = true; }
            Check(threw && !host.Feature.HasFailed && host.World.CausalDepth == 0 && Main.LocalPlayer.inventory.Any(i => i.type == 8) && host.Ownership.IsProtected(10) && !host.Ownership.IsProtected(20), "partial native grant preserved with only affected source range protected");
            Console.WriteLine("PASS: item host fixture transactions, causal sources, selective requests, receipts, conflict guards and key focus. Not real-game/server acceptance.");
        }
        private static void NewSession(int mode)
        {
            runtime.InvalidateSession(); Main.netMode = mode; Main.gameMenu = false; Main.playerInventory = false; Main.npcShop = 0;
            Main.LocalPlayer = new Player { active = true }; Main.ActiveWorldFileData = new IO.WorldFileData(); Netplay.Connection.Socket = new Net.Sockets.FixtureSocket();
            Main.instance.shop[1] = new Chest(); Main.PendingInventory = false; Main.NativeVoidPending = false; Main.ServerSideCharacter = false;
            Main.shopSellbackHelper.Count = 0; Main.projectile = new Projectile[1000]; NearbyChests.Targets.Clear();
            PlayerInput.MouseInfo = new MouseState(); Main.mouseLeft = Main.mouseRight = false; Main.cursorOverride = 0;
            FocusHelper.IsSelectedApplication = true; runtime.Update(tick++);
        }
        private static void PreferenceIsolation()
        {
            RetiredConfiguration();
            var codec = new ItemAutomationCodec(ID.ItemID.Count);
            string path = Path.Combine(root, "isolated-preferences", "items.json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            var document = new PreferenceDocument<ItemAutomationSettings>(new FilePreferenceStorage(path), codec, ItemAutomationSettings.Default);
            documents.Add(document);
            Wait(() => document.Snapshot.IsLoaded, "missing item config load");
            Check(!File.Exists(path), "missing item config is not auto-created");
            ItemAutomationSettings changed = ItemAutomationSettings.Default.WithEnabled(ItemActionKind.Stack, true).WithTypes(ItemListKind.Discard, new[] { 101, 102 });
            document.Set(changed); Wait(() => document.Snapshot.Status == PreferenceStatus.Saved, "item config saved"); document.Stop(750);
            var restored = new PreferenceDocument<ItemAutomationSettings>(new FilePreferenceStorage(path), codec, ItemAutomationSettings.Default);
            documents.Add(restored);
            Wait(() => restored.Snapshot.IsLoaded, "saved item config reload");
            Check(restored.Snapshot.Value.Equals(changed), "three controls/lists survive isolated restart"); restored.Stop(750);
            foreach (string invalid in new[] { "{", Encoding.UTF8.GetString(codec.Encode(changed)).Replace("\"version\":2", "\"version\":99") })
            {
                File.WriteAllText(path, invalid, new UTF8Encoding(false)); byte[] original = File.ReadAllBytes(path);
                var protectedDocument = new PreferenceDocument<ItemAutomationSettings>(new FilePreferenceStorage(path), codec, ItemAutomationSettings.Default);
                documents.Add(protectedDocument);
                Wait(() => protectedDocument.Snapshot.IsLoaded, "protected item config load");
                Check(!protectedDocument.Snapshot.Value.StackEnabled && protectedDocument.Snapshot.Status != PreferenceStatus.Saved, "invalid/future document defaults all actions off");
                protectedDocument.Set(changed); protectedDocument.Stop(750);
                Check(original.SequenceEqual(File.ReadAllBytes(path)), "explicit temporary setting preserves invalid/future original bytes");
            }
        }
        private static PreferenceDocument<ItemAutomationSettings> OpenRetiring(string path)
        {
            var file = new JueMingR.Infrastructure.Storage.AtomicFileDocument(path, 65536, true, ".schema1-original");
            var codec = (IPreferenceCodec<ItemAutomationSettings>)Activator.CreateInstance(
                typeof(HostItems).GetNestedType("RetiringItemCodec", BindingFlags.NonPublic),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { file }, null);
            var document = new PreferenceDocument<ItemAutomationSettings>(file, codec, ItemAutomationSettings.Default);
            documents.Add(document); Wait(() => document.Snapshot.IsLoaded, "production retiring codec load"); return document;
        }
        private static void RetiredConfiguration()
        {
            string directory = Path.Combine(root, "retirement"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "items.json");
            const string legacy = "{\"format\":\"JueMingR.ItemAutomation\",\"version\":1,\"stackEnabled\":true,\"sellEnabled\":false,\"discardEnabled\":true,\"sellTypes\":[8,100],\"discardTypes\":[101],\"stackBinding\":112,\"sellBinding\":113,\"discardBinding\":114}";
            File.WriteAllText(path, legacy, new UTF8Encoding(false));
            var document = OpenRetiring(path);
            var original = document.Snapshot.Value;
            Check(original.StackEnabled && !original.SellEnabled && original.DiscardEnabled && original.SellTypes.SequenceEqual(new[] { 8, 100 }) && original.DiscardTypes.SequenceEqual(new[] { 101 }), "legacy booleans and lists preserved");
            Check(File.ReadAllText(path) == legacy && !File.Exists(path + ".schema1-original"), "load does not migrate or archive user file");
            Check(!document.Set(original), "repeated explicit set is a no-op");
            document.Set(original.WithEnabled(ItemActionKind.Sell, true)); Wait(() => document.Snapshot.Status == PreferenceStatus.Saved, "first v2 save");
            string first = File.ReadAllText(path);
            Check(first.Contains("\"version\":2") && !first.Contains("Binding") && File.ReadAllText(path + ".schema1-original") == legacy, "first real write retires fields and retains exact original");
            document.Set(original.WithEnabled(ItemActionKind.Stack, false)); Wait(() => document.Snapshot.Status == PreferenceStatus.Saved, "second v2 save");
            Check(File.ReadAllText(path + ".schema1-original") == legacy && File.ReadAllText(path + ".bak") == first, "original archive never rotates, backup does");
            document.Stop(750);
            var reopened = OpenRetiring(path); Check(!reopened.Snapshot.Value.StackEnabled && reopened.Snapshot.Value.DiscardEnabled, "new version reload"); reopened.Stop(750);
            foreach (string suffix in new[] { ".bak", ".tmp", ".schema1-original" })
            {
                string missing = Path.Combine(directory, "missing" + suffix + ".json"); File.WriteAllText(missing + suffix, legacy);
                var protectedFile = OpenRetiring(missing); protectedFile.Set(original); protectedFile.Stop(750);
                Check(!File.Exists(missing) && File.ReadAllText(missing + suffix) == legacy, "recovery materials prevent default replacement: " + suffix);
            }
            foreach (string mode in new[] { "conflict", "archive", "temporary", "invalid", "future" })
            {
                string guarded = Path.Combine(directory, mode + ".json");
                string bytes = mode == "invalid" ? legacy.Replace("\"stackBinding\":112", "\"stackBinding\":\"F1\"") : mode == "future" ? legacy.Replace("\"version\":1", "\"version\":99") : legacy;
                File.WriteAllText(guarded, bytes, new UTF8Encoding(false));
                if (mode == "archive") File.WriteAllText(guarded + ".schema1-original", "different original");
                if (mode == "temporary") File.WriteAllText(guarded + ".tmp", "interrupted");
                var guardedFile = OpenRetiring(guarded);
                if (mode == "conflict") { bytes = "external change"; File.WriteAllText(guarded, bytes); }
                guardedFile.Set(original.WithEnabled(ItemActionKind.Sell, true)); guardedFile.Stop(750);
                Check(File.ReadAllText(guarded) == bytes, "unsafe retirement preserves source: " + mode);
                Check(!File.Exists(guarded + ".schema1-original") || mode == "archive", "invalid/conflicted file not archived: " + mode);
            }
        }
        private static void Wait(Func<bool> ready, string message)
        { for (int i = 0; i < 500 && !ready(); i++) Thread.Sleep(5); Check(ready(), message); }
        private static void Configure(bool stack, bool sell, bool discard, int[] sale = null, int[] trash = null)
        {
            host.Change(new ItemAutomationSettings(stack, sell, discard, sale ?? new[] { 100 }, trash ?? new[] { 101 })); host.PollPreferences();
        }
        private static Item Make(int type, int amount, int maximum = 9999) { return new Item { type = type, stack = amount, maxStack = maximum }; }
        private static ItemSlotObservation Observe(int slot)
        { ItemInventoryObservation value; Check(host.World.TryObserve(out value), "observe active inventory"); return value.Slots[slot]; }
        private static SellItemRequest Sale(int slot)
        { ItemInventoryObservation value; Check(host.World.TryObserve(out value), "observe sale"); return new SellItemRequest(runtime.Generation, value.ShopIdentity, value.Slots[slot]); }
        private static void Step(int count = 8) { for (int i = 0; i < count; i++) runtime.Update(tick++); }
        private static void Transactions()
        {
            NewSession(0); Main.playerInventory = true; Main.npcShop = 1;
            Player player = Main.LocalPlayer; player.inventory[10] = Make(100, 3); player.inventory[50] = Make(71, 10);
            ItemOperationResult sold = host.Operations.Execute(Sale(10));
            Check(sold.State == ItemOperationState.Completed && sold.ConfirmedQuantity == 3 && sold.ConfirmedCopper == 15 && player.inventory[50].stack == 25 && player.inventory[10].IsAir && Main.instance.shop[1].item[0].stack == 3, "sell source/coins/buyback conservation");
            player.inventory[10] = Make(100, 1, 1); Main.shopSellbackHelper.Count = 1;
            sold = host.Operations.Execute(Sale(10));
            Check(sold.State == ItemOperationState.Completed && sold.ConfirmedCopper == 125, "original bought-back price on nonstackable source");
            Main.shopSellbackHelper.Count = 0; player.inventory[10] = Make(100, 1); player.inventory[10].value = 0;
            sold = host.Operations.Execute(Sale(10)); Check(sold.State == ItemOperationState.Completed && sold.ConfirmedCopper == 0, "legal zero-income sale");
            player.inventory[10] = Make(100, 5); player.RejectSale = true;
            ItemInventoryObservation before; host.World.TryObserve(out before);
            sold = host.Operations.Execute(Sale(10)); ItemInventoryObservation after; host.World.TryObserve(out after);
            Check(sold.State == ItemOperationState.Rejected && after.Revision == before.Revision && after.Slots[10].Instance != before.Slots[10].Instance, "equivalent 58-slot cloning refreshes identities without retry revision");
            host.World.ManualSlot = 11; host.World.ManualItem = player.inventory[11];
            int calls = player.SellCalls; sold = host.Operations.Execute(new SellItemRequest(runtime.Generation, after.ShopIdentity, after.Slots[10]));
            Check(sold.State == ItemOperationState.Rejected && player.SellCalls == calls, "sale honors entire manual inventory range");
            host.World.ManualSlot = -1; player.RejectSale = false; player.trashItem = Make(74, 20);
            var discarded = host.Operations.Execute(new DiscardItemRequest(runtime.Generation, Observe(10)));
            Check(discarded.State == ItemOperationState.Completed && player.trashItem.type == 100 && player.inventory[10].IsAir && ItemSlot.TrashResearch > 0, "trash replaces arbitrary old content through native effect");
            player.inventory[11] = Make(101, 1, 1); host.Operations.Execute(new DiscardItemRequest(runtime.Generation, Observe(11)));
            Check(player.trashItem.type == 101 && player.trashItem.stack == 1, "only final replacement recoverable");
            player.inventory[10] = Make(100, 1); player.ThrowSaleAfterCredit = true;
            sold = host.Operations.Execute(Sale(10));
            Check(sold.State == ItemOperationState.Unconfirmed && host.Ownership.ProtectedSlots == (1UL << 58) - 1, "interrupted credited sale owns all 58 slots without rollback");
            NewSession(0); Configure(false, true, false); player = Main.LocalPlayer;
            Main.playerInventory = true; Main.npcShop = 1; player.inventory[10] = Make(100, 1); player.itemAnimation = 10;
            Step(); Check(player.SellCalls == 0, "temporary item-use range prevents sale");
            player.itemAnimation = 0; Step();
            Check(player.SellCalls == 1 && player.inventory[10].IsAir, "ending temporary use re-evaluates unchanged sale candidate");
        }
        private static void SelectionAndSources()
        {
            NewSession(0); Configure(true, false, false);
            Player p = Main.LocalPlayer; p.inventory[10] = Make(8, 20); p.inventory[11] = Make(8, 9); p.inventory[11].favorited = true;
            var chest = new Chest(); chest.item[0] = Make(8, 1, 100); NearbyChests.Targets.Add(new PositionedChest { chest = chest });
            Step(); Check(p.inventory[10].stack == 20, "enable does not authorize existing inventory");
            p.GetItem(Make(8, 1), new GetItemSettings()); Step(); Check(p.inventory[10].stack == 21, "generic GetItem is not source");
            p.Pickup(new WorldItem { inner = Make(8, 3) }); Step();
            Check(p.inventory[10].IsAir && p.inventory[11].stack == 9 && chest.item[0].stack == 25 && QuickStacking.LastSlots.SequenceEqual(new[] { 10 }), "real pickup coalesces entire eligible current stack, keeps favorite");
            p.inventory[10] = Make(8, 1); p.inventory[12] = Make(1000, 2); p.inventory[13] = Make(327, 2); ID.ItemID.Sets.OpenableBag[1000] = true;
            ItemSlot.RightClick(p.inventory, 0, 12); Step(); Check(chest.item[0].stack == 29 && p.inventory[12].stack == 1 && p.inventory[13].stack == 1, "normal completed opening grants storage after consumption");
            Configure(false, true, true, new[] { 1000, 327 }, new[] { 1000, 327 });
            PlayerInput.MouseInfo = new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Pressed, ButtonState.Released, ButtonState.Released);
            p.inventory[12].stack = 3; p.inventory[13].stack = 3; ItemSlot.RightClick(p.inventory, 0, 12);
            Check(host.World.ManualMaterials.Contains(p.inventory[13]) && host.World.ManualSlot == 12, "opening source and actual key remain protected with storage disabled");
            Step(); Check(p.inventory[12].stack == 2 && p.inventory[13].stack == 2, "held opening cannot sell or discard source/key");
        }
        private static void NetworkOwnership()
        {
            NewSession(1); Configure(true, false, false); Player p = Main.LocalPlayer;
            p.inventory[10] = Make(8, 20); p.inventory[54] = Make(8, 3); int calls = QuickStacking.Calls;
            var result = host.Operations.Execute(new StoreItemsRequest(runtime.Generation, new[] { Observe(10), Observe(54) }));
            Check(result.State == ItemOperationState.Executing && QuickStacking.LastSlots.SequenceEqual(new[] { 10, 54 }), "client submits only selected normal/ammo slots");
            QuickStacking.QuickStackToNearbyChests(p, false); Step(20);
            Check(QuickStacking.Calls == calls + 1 && host.Ownership.StoreBlocked, "single shared native batch, no blind resend");
            int sales = p.SellCalls; bool manualSale = p.SellItem(Make(100, 1));
            Check(!manualSale && p.SellCalls == sales, "mouse-held shop sale shares full 58-slot conflict boundary");
            Check(!p.BuyItem(1, 0), "custom currency full-inventory restore conflicts with any pending source");
            Check(p.BuyItem(1), "ordinary coin purchase does not touch occupied noncoin pending slots");
            Reply(10, 8, 6); Step(); Check(host.Ownership.StoreBlocked, "partial source replies do not release");
            Reply(54, 0, 0); Step();
            Check(!host.Ownership.StoreBlocked && host.Ownership.StoreResult.State == ItemOperationState.PartiallyCompleted && host.Ownership.StoreResult.ConfirmedQuantity == 17, "aggregate source receipts confirm partial amount");
            p.inventory[11] = Make(9, 3); host.Operations.Execute(new StoreItemsRequest(runtime.Generation, new[] { Observe(11) }));
            p.inventoryChestStack[11] = false; Step(610);
            Check(host.Ownership.StoreResult.State == ItemOperationState.TimedOut && host.Ownership.IsProtected(11), "bare native unlock is not reply or permission to retry");
            int sorted = ItemSorting.Calls; ItemSorting.SortInventory(); Check(ItemSorting.Calls == sorted, "R unknown ownership survives native unlock during sort");
            long generation = runtime.Generation; NewSession(1); Reply(11, 9, 0); Step();
            Check(runtime.Generation != generation && !host.Ownership.StoreBlocked, "old receipt does not drive replacement session");
        }
        private static void SourceEdges()
        {
            NewSession(0); Configure(true, false, false); Player p = Main.LocalPlayer;
            p.inventory[10] = Make(8, 20); p.inventory[12] = Make(1000, 2); ID.ItemID.Sets.OpenableBag[1000] = true;
            var chest = new Chest(); chest.item[0] = Make(8, 1); NearbyChests.Targets.Add(new PositionedChest { chest = chest });
            int calls = QuickStacking.Calls; ItemSlot.RightClick(p.inventory, 0, 12); Step();
            Check(p.inventory[12].stack == 2 && p.inventory[10].stack == 20 && QuickStacking.Calls == calls, "missing key does not authorize old inventory");
            for (int i = 0; i < 50; i++) p.inventory[i] = Make(9, 5, 5);
            p.inventory[10] = Make(8, 20, 22); p.inventory[12] = Make(1000, 2); p.inventory[13] = Make(327, 2);
            ItemSlot.RightClick(p.inventory, 0, 12); Step();
            Check(p.inventory[10].IsAir && chest.item[0].stack == 23 && p.FixtureGrantRemainder.stack == 1 && QuickStacking.Calls == calls + 1,
                "partial direct receipt authorizes only its current whole group once, leaves grounded remainder");
            p.Pickup(new WorldItem { inner = p.FixtureGrantRemainder }); Step();
            Check(chest.item[0].stack == 24 && QuickStacking.Calls == calls + 2, "actual later pickup of remainder is an independent single source");
            NewSession(0); Configure(true, false, false); p = Main.LocalPlayer;
            p.inventory[10] = Make(8, 20); p.inventory[12] = Make(1001, 2); ID.ItemID.Sets.OpenableBag[1001] = true;
            p.FixtureVoidReceive = true; calls = QuickStacking.Calls; ItemSlot.RightClick(p.inventory, 0, 12); Step();
            Check(p.FixtureVoidQuantity == 3 && p.inventory[10].stack == 20 && p.inventory[12].stack == 1 && QuickStacking.Calls == calls,
                "successful alternate openable received only by void creates no main-inventory storage source");
        }
        private static void CapacityBackoff()
        {
            NewSession(0); Configure(true, false, false); Player p = Main.LocalPlayer;
            p.inventory[10] = Make(8, 20); int calls = QuickStacking.Calls;
            Step(12); Check(QuickStacking.Calls == calls, "no source never queries a container");
            p.Pickup(new WorldItem { inner = Make(8, 1) }); Step();
            Check(QuickStacking.Calls == calls + 1, "one actual no-capacity result");
            for (int i = 0; i < 10; i++) { p.Pickup(new WorldItem { inner = Make(8, 1) }); Step(); }
            Check(QuickStacking.Calls == calls + 1, "repeated pickup within same position backoff does not repeat native chest scans");
            p.position = new Vector2(10, 0); p.Pickup(new WorldItem { inner = Make(8, 1) }); Step();
            Check(QuickStacking.Calls == calls + 2, "movement permits a fresh actual capacity attempt");
            var chest = new Chest(); chest.item[0] = Make(8, 1); NearbyChests.Targets.Add(new PositionedChest { chest = chest });
            p.Pickup(new WorldItem { inner = Make(8, 1) }); Step();
            Check(!p.inventory[10].IsAir, "no blind immediate retry while short capacity wait remains");
            host.Storage.InvalidateCapacity(); Step();
            Check(p.inventory[10].IsAir && chest.item[0].stack == 34, "real capacity invalidation re-evaluates current full stack");
            NewSession(0); Configure(true, false, false); p = Main.LocalPlayer; p.inventory[10] = Make(8, 20);
            chest = new Chest(); chest.item[0] = Make(8, 1); NearbyChests.Targets.Add(new PositionedChest { chest = chest });
            Main.NativeVoidPending = true; p.itemAnimation = 10;
            p.Pickup(new WorldItem { inner = Make(8, 1) }); Step(); Check(!p.inventory[10].IsAir, "native void request temporarily blocks storage");
            Main.NativeVoidPending = false; Step();
            Check(p.inventory[10].IsAir && chest.item[0].stack == 22, "one admission gate release is visible while unrelated use gate stays busy");
        }
        private static void StorageBoundaries()
        {
            NewSession(0); Configure(true, false, false); Player p = Main.LocalPlayer;
            p.inventory[10] = Make(8, 4, 5); var chest = new Chest(); chest.item[0] = Make(8, 4, 5);
            NearbyChests.Targets.Add(new PositionedChest { chest = chest });
            var result = host.Operations.Execute(new StoreItemsRequest(runtime.Generation, new[] { Observe(10) }));
            Check(result.State == ItemOperationState.Completed && chest.item[0].stack == 5 && chest.item[1].type == 8 && chest.item[1].stack == 3,
                "compatible target fills then creates allowed same-type new stack");
            NewSession(0); p = Main.LocalPlayer; p.inventory[10] = Make(8, 4, 5); chest = new Chest();
            for (int i = 0; i < 40; i++) chest.item[i] = Make(9, 5, 5); chest.item[0] = Make(8, 4, 5);
            NearbyChests.Targets.Add(new PositionedChest { chest = chest });
            result = host.Operations.Execute(new StoreItemsRequest(runtime.Generation, new[] { Observe(10) }));
            Check(result.State == ItemOperationState.PartiallyCompleted && result.ConfirmedQuantity == 1 && p.inventory[10].stack == 3,
                "limited local capacity verifies moved amount and actual remaining source");
            foreach (bool occupied in new[] { false, true })
            {
                NewSession(0); p = Main.LocalPlayer; p.inventory[10] = Make(8, 4);
                chest = new Chest { FixtureLocked = !occupied, FixtureOccupied = occupied }; chest.item[0] = Make(8, 1);
                NearbyChests.Targets.Add(new PositionedChest { chest = chest });
                result = host.Operations.Execute(new StoreItemsRequest(runtime.Generation, new[] { Observe(10) }));
                Check(result.State == ItemOperationState.NotApplicable && p.inventory[10].stack == 4 && chest.item[0].stack == 1, "native lock/occupancy prevents movement");
            }
        }
        private static void Guards()
        {
            NewSession(1); Player p = Main.LocalPlayer;
            p.inventory[54] = Make(10, 5); p.inventory[54].ammo = 1; p.inventory[55] = Make(10, 2); p.inventory[55].ammo = 1;
            host.Operations.Execute(new StoreItemsRequest(runtime.Generation, new[] { Observe(54) }));
            ItemSlot.LeftClick(p.inventory, 2, 54); Check(p.inventory[54].stack == 5, "ammo manual click cannot touch pending");
            p.FillAmmo(new Item { type = 10, stack = 1, ammo = 1 }, new GetItemSettings());
            Check(p.inventory[54].stack == 5 && p.inventory[55].stack == 3, "ammo receiver skips occupied pending without blocking another slot");
            p.inventory[54].TurnToAir(); p.FillAmmo(new Item { type = 12, stack = 1, ammo = 1 }, new GetItemSettings());
            Check(p.inventory[54].IsAir && p.inventory[56].type == 12, "ammo receiver does not mistake protected air for available empty slot");
            NewSession(1); p = Main.LocalPlayer; p.inventory[10] = Make(530, 3); p.inventory[11] = Make(530, 4);
            host.Operations.Execute(new StoreItemsRequest(runtime.Generation, new[] { Observe(11) }));
            Check(!p.Use(new Item { type = 3611, shoot = 651 }), "mass wire checks later protected stack despite earlier available material");
            Check(p.Use(new Item { type = 509 }), "single first-match wire source remains available");
            Check(p.ConsumeItem(530) && p.inventory[10].stack == 2 && p.inventory[11].stack == 4, "consume uses unprotected source");
            NewSession(1); p = Main.LocalPlayer; p.inventory[10] = Make(849, 3); p.inventory[11] = Make(20, 3); p.inventory[11].PaintOrCoating = true;
            host.Operations.Execute(new StoreItemsRequest(runtime.Generation, new[] { Observe(10) })); p.autoPaint = p.autoActuator = true; p.Decorate();
            Check(p.Paints == 1 && p.Actuators == 0 && p.inventory[10].stack == 3, "pending actuator does not block unrelated paint");
            p.inventory[0] = new Item { type = 999, stack = 1, Flexible = new FlexibleTileWand() }; p.Place();
            Check(p.Places == 0, "flexible wand retains original selected option and waits for its material");
        }
        private static void UiKeys()
        {
            var shell = new JueMingR.TerrariaHost.F5.F5Interaction { Ready = true };
            shell.Update(new JueMingR.TerrariaHost.F5.F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1, F5 = true });
            shell.Layout.Ensure(1920, 1080, 1, 9, new object(), t => new JueMingR.TerrariaHost.F5.F5Size(t.Length * 18, 24));
            var nav = shell.Layout.Navigation(0).Offset(shell.X, shell.Y);
            shell.Update(new JueMingR.TerrariaHost.F5.F5Input { Active = true, Focused = true, Width = 1920, Height = 1080, Scale = 1,
                X = nav.X + 2, Y = nav.Y + 2, Left = true });
            Check(shell.Page == 0 && shell.ConsumeLeft, "inline page allows navigation and consumes its press");
            NewSession(0); Configure(false, false, false);
            var input = new ItemPickerInput();
            var before = host.Preferences.Value;
            Main.keyState = new KeyboardState(Keys.F1, Keys.F2, Keys.F6);
            input.Sample(Main.keyState, true);
            Check(host.Preferences.Value.Equals(before) && Main.keyState.IsKeyDown(Keys.F6), "no picker means no item key commands or native key consumption");
            Main.blockInput = false; input.BeforeInput(true);
            Check(Main.blockInput && input.OwnsTextToken && PlayerInput.WritingText, "picker acquires minimal text/block lease");
            input.Sample(new KeyboardState(Keys.Escape), true); input.Release();
            PlayerInput.WritingText = false; input.BeforeInput(false);
            Check(PlayerInput.WritingText && !Main.blockInput, "released picker retains held key tail, restores block lease");
            input.Sample(new KeyboardState(), true); PlayerInput.WritingText = false; input.BeforeInput(false);
            Check(!PlayerInput.WritingText, "real release ends keyboard tail");
            input.BeforeInput(true); var foreign = new object(); Main.CurrentInputTextTakerOverride = foreign;
            input.Sample(new KeyboardState(Keys.Escape), false);
            Check(!Main.blockInput && ReferenceEquals(Main.CurrentInputTextTakerOverride, foreign), "focus interruption preserves foreign token and restores block lease");
            Main.keyState = new KeyboardState(Keys.A); input.Sample(Main.keyState, true);
            Check(Main.keyState.IsKeyDown(Keys.A), "picker tail never consumes a foreign text owner sample");
            Main.CurrentInputTextTakerOverride = null; input.Sample(new KeyboardState(), true);
            input.BeforeInput(true); input.Sample(new KeyboardState(), true); input.BeforeInput(false);
            input.Sample(new KeyboardState(), false); PlayerInput.WritingText = false; input.BeforeInput(false);
            Check(PlayerInput.WritingText, "prefix-before-sample focus loss retains release tail");
            Main.CurrentInputTextTakerOverride = null; input.Sample(new KeyboardState(), true); PlayerInput.WritingText = false;
        }
        private static void Reply(int slot, int type, int amount)
        {
            var message = new MessageBuffer(); byte[] data = message.readBuffer; data[0] = 5; data[1] = (byte)Main.LocalPlayer.whoAmI;
            BitConverter.GetBytes((short)slot).CopyTo(data, 2); BitConverter.GetBytes((short)amount).CopyTo(data, 4); BitConverter.GetBytes((short)type).CopyTo(data, 7);
            int ignored; message.GetData(0, 10, out ignored);
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException("ITEM CHECK FAILED: " + message); }
        private sealed class Idle : IRuntimeFeature
        { public bool Enabled { get { return false; } } public void OnSessionStarted() { } public void OnSessionEnded() { } public void Update(ulong tick) { } public void FailClosed() { } }
    }
}
