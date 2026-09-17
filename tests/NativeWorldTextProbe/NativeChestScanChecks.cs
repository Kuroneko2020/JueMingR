using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeChestScanChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(Assembly assembly, object native)
        {
            object host = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.HostItemKnowledge"), true);
            ((IDisposable)Get(host, "Native")).Dispose(); Set(host, "Native", native);
            object world = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.World.WorldTileObservation"), Flags, null, new object[] { (Func<bool>)(() => true) }, null);
            object locator = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ChestLocator.HostChestLocator"), Flags, null, new[] { world, host }, null);
            long tick = 0;
            Action update = () => { Call(world, "BeginTick"); Call(locator, "Update", ++tick); };
            try
            {
                Main.gameMenu = false; Main.netMode = 0; Main.LocalPlayer.chest = -1; Main.LocalPlayer.active = true;
                Main.LocalPlayer.position = new Vector2(300, 300); Main.LocalPlayer.gravDir = 1;
                Main.screenPosition = Vector2.Zero; Main.screenWidth = 960; Main.screenHeight = 640;
                Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null); Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640));
                Reset(); update();
                long reads = (long)Get(locator, "CellReads"), slots = (long)Get(locator, "SlotReads");
                Call(locator, "Submit", "");
                Require((long)Get(locator, "CellReads") == reads && (long)Get(locator, "SlotReads") == slots && !(bool)Get(locator, "scanning"), "25th query type rejected before any chest/tile read");
                int[] tileTypes = { 21, 88, 441, 467, 468 };
                for (int i = 0; i < tileTypes.Length; i++) Place(i, 6 + i * 4, 8, tileTypes[i], i + 1);
                // A fake chest tile without a real content record must not count.
                Place(50, 30, 8, 441, 100); Main.chest[50] = null;
                Call(locator, "Submit", "#9"); RunScan(locator, update);
                var results = (IList)Get(locator, "Results");
                Require(results.Count == 5 && results.Cast<object>().Select(r => (string)Get(r, "Name")).Distinct().Count() == 5, "all real container families, dresser dedup, no fabricated fake-chest stock");
                Require(results.Cast<object>().Select(r => (int)Get(Get(r, "Chest"), "index")).SequenceEqual(new[] { 0, 1, 2, 3, 4 }), "stable X-outer Y-inner container order");
                Require(results.Cast<object>().Select(r => (string)Get(r, "WorldLabel")).SequenceEqual(new[] { "2个", "3个", "4个", "5个", "6个" }), "world labels contain only the matched total across slots");
                reads = (long)Get(locator, "CellReads"); slots = (long)Get(locator, "SlotReads"); for (int i = 0; i < 120; i++) update();
                Require((long)Get(locator, "CellReads") == reads && (long)Get(locator, "SlotReads") == slots, "stable highlight performs no content or discovery scan");
                InvalidateThroughSelectiveStorage(); update();
                Require(results.Count == 0, "actual selective quick-stack overload invalidates earlier content evidence even when transfer is uncertain");
                Call(locator, "Submit", "#9"); RunScan(locator, update);
                Main.LocalPlayer.chest = 7999; update(); Require(results.Count == 0, "opening any ordinary chest removes old results"); Main.LocalPlayer.chest = -1;
                Reset(); update();
                for (int i = 0; i < 25; i++) Place(i, 4 + i % 10 * 4, 4 + i / 10 * 4, 21, 2);
                Call(locator, "Submit", "#9"); RunScan(locator, update);
                Require(results.Count == 24 && ((string)Get(locator, "Status")).Contains("未完整"), "25th actual hit proves result truncation");
                Reset(); update();
                for (int i = 0; i < 65; i++) Place(i, 2 + i % 13 * 4, 2 + i / 13 * 4, 21, 0);
                slots = (long)Get(locator, "SlotReads"); Call(locator, "Submit", "#9"); RunScan(locator, update);
                Require(results.Count == 0 && (long)Get(locator, "SlotReads") - slots == 64 * 3 && ((string)Get(locator, "Status")).Contains("未完整"), "content candidate65 is reported unexamined and never scanned");
                Reset(); update(); Place(0, 20, 20, 21, 5); Call(locator, "Submit", "#9"); RunScan(locator, update);
                Call(locator, "Update", tick + 3601); Require(results.Count == 0, "3600-game-update snapshot expiry does not rescan");
                update(); Call(locator, "Submit", "#9"); RunScan(locator, update); Call(locator, "Update", tick - 1); Require(results.Count == 0, "clock rollback invalidates locator results");
                Reset(); update(); Place(0, 64, 8, 21, 5); Place(1, 68, 8, 21, 5);
                Call(locator, "Submit", "#9"); RunScan(locator, update);
                Require(results.Count == 1 && (int)Get(Get(results[0], "Chest"), "index") == 0, "view extends by 96 pixels and excludes the next outside container");
                Reset(); Main.screenWidth = 2000; Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 2000, 640)); Main.LocalPlayer.position = new Vector2(100, 300); update();
                Place(0, 110, 20, 21, 5); Place(1, 115, 20, 21, 5); Call(locator, "Submit", "#9"); RunScan(locator, update);
                Require(results.Count == 1 && (int)Get(Get(results[0], "Chest"), "index") == 0, "1696 pixel player radius intersects the visible search rectangle");
                Main.screenWidth = 960; Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640)); Main.LocalPlayer.position = new Vector2(300, 300);
                // Real receive coverage already has a separate GetData check;
                // this checks that its absence gates the real scanner as unknown.
                Reset(); Main.netMode = 1; Main.sectionManager = new WorldSections(2, 2); Main.sectionManager.SetAllSectionsLoaded(); Netplay.Connection = new RemoteServer(); update();
                Place(0, 20, 20, 21, 99); slots = (long)Get(locator, "SlotReads"); Call(locator, "Submit", "#9"); RunScan(locator, update);
                Require(results.Count == 0 && (long)Get(locator, "SlotReads") == slots && ((string)Get(locator, "Status")).Contains("未知"), "allocated positive client cache is unreadable without capacity and complete slot receipts");
                var buffer = new MessageBuffer { whoAmI = 256 };
                NativeChestLocatorChecks.Receive(buffer, 155, writer => { writer.Write((short)0); writer.Write((short)3); });
                for (int i = 0; i < 3; i++)
                {
                    int slot = i;
                    NativeChestLocatorChecks.Receive(buffer, 32, writer => { writer.Write((short)0); writer.Write((byte)slot); writer.Write((short)(slot == 0 ? 99 : 0)); writer.Write((byte)0); writer.Write((short)(slot == 0 ? 9 : 0)); });
                }
                update(); slots = (long)Get(locator, "SlotReads"); Call(locator, "Submit", "#9"); RunScan(locator, update);
                Require(results.Count == 1 && (long)Get(locator, "SlotReads") - slots == 3 && ((string)Get(locator, "Status")).Contains("最近接收"), "actual native 155 plus every 32 slot unlock the normal client scanner without opening a chest");
                ReceiveRecovery(locator, update, buffer, results);
                Console.WriteLine("PASS: actual spatial scanner families/order/64 candidates/24 hits/TTL/open-chest/client unknown, with zero stable slot rescans.");
            }
            finally { ((IDisposable)locator).Dispose(); Main.netMode = 0; }
        }
        private static void ReceiveRecovery(object locator, Action update, MessageBuffer buffer, IList results)
        {
            object receiver = Get(locator, "Receiver");
            Action header = () => NativeChestLocatorChecks.Receive(buffer, 155, writer => { writer.Write((short)0); writer.Write((short)3); });
            Action<int> slot = index => NativeChestLocatorChecks.Receive(buffer, 32, writer =>
            { writer.Write((short)0); writer.Write((byte)index); writer.Write((short)(index == 0 ? 99 : 0)); writer.Write((byte)0); writer.Write((short)(index == 0 ? 9 : 0)); });
            long cells = (long)Get(locator, "CellReads"), slots = (long)Get(locator, "SlotReads");
            slot(0); Call(locator, "Submit", "#9");
            Require(!(bool)Get(locator, "scanning") && (long)Get(locator, "CellReads") == cells && (long)Get(locator, "SlotReads") == slots &&
                ((string)Get(locator, "Status")).Contains("同步正在处理"), "pending native receipt blocks a real query before world or inventory reads");
            update(); Call(locator, "Submit", "#9"); RunScan(locator, update);
            Require(results.Count == 1, "consuming a normal revision permits explicit query again");

            // 4097 drops the full queue and the overflowing receipt. Pending must
            // still guard the old complete knowledge even with an empty queue.
            for (int i = 0; i < 4097; i++) slot(0);
            Require(((ICollection)Get(receiver, "receipts")).Count == 0 && (bool)Get(receiver, "Pending"), "queue-loss boundary remains pending without queued receipts");
            Call(locator, "Submit", "#9"); Require(((string)Get(locator, "Status")).Contains("同步正在处理"), "unconsumed loss is not a reconnect failure");
            update(); slots = (long)Get(locator, "SlotReads");
            Call(locator, "Submit", "#9"); RunScan(locator, update);
            Require(results.Count == 0 && (long)Get(locator, "SlotReads") == slots && ((string)Get(locator, "Status")).Contains("需收到完整同步"), "loss retires old query evidence and describes the actual complete-sync recovery gate");
            slot(0); slot(1); slot(2); update(); Call(locator, "Submit", "#9"); RunScan(locator, update);
            Require(results.Count == 0 && (long)Get(locator, "SlotReads") == slots, "all slots without a new capacity cannot reuse pre-loss completeness");
            header(); slot(0); slot(0); slot(2); update(); Call(locator, "Submit", "#9"); RunScan(locator, update);
            Require(results.Count == 0 && (long)Get(locator, "SlotReads") == slots, "duplicate slots cannot replace the missing slot at the real scanner");
            slot(1); update(); Call(locator, "Submit", "#9"); RunScan(locator, update);
            Require(results.Count == 1 && (long)Get(locator, "SlotReads") - slots == 3 && (string)Get(results[0], "WorldLabel") == "99个", "fresh complete coverage recovers the actual query on the same connection after overflow");
            cells = (long)Get(locator, "CellReads"); slots = (long)Get(locator, "SlotReads"); string status = (string)Get(locator, "Status");
            for (int i = 0; i < 120; i++) update();
            Require((long)Get(locator, "CellReads") == cells && (long)Get(locator, "SlotReads") == slots && ReferenceEquals(status, Get(locator, "Status")), "recovered stable query neither rescans nor rebuilds status text");

            // Invoke the existing provenance seam; do not deliver real socket data.
            Call(receiver, "ReadBefore", new RemoteServer()); Call(locator, "Submit", "#9");
            Require(((string)Get(locator, "Status")).Contains("请重新连接"), "unconsumed old-source contamination requires reconnect rather than normal waiting");
            update(); header(); slot(0); slot(1); slot(2); update(); Call(locator, "Submit", "#9");
            Require(results.Count == 0 && !(bool)Get(locator, "scanning") && ((string)Get(locator, "Status")).Contains("请重新连接"), "fresh coverage cannot rehabilitate a contaminated connection");
            ((IDisposable)receiver).Dispose(); Call(locator, "Submit", "#9");
            Require(((string)Get(locator, "Status")).Contains("接收功能不可用"), "unavailable hooks do not promise recovery through synchronization");
            Console.WriteLine("PASS: real scanner overflow recovery, incomplete coverage, pending/source/unavailable feedback, and stable recovered workload.");
        }
        private static void RunScan(object locator, Action update)
        { for (int i = 0; i < 100 && (bool)Get(locator, "scanning"); i++) update(); Require(!(bool)Get(locator, "scanning"), "bounded scanner finishes"); }
        private static void InvalidateThroughSelectiveStorage()
        {
            for (int i = 0; i < Main.player.Length; i++) if (Main.player[i] == null) Main.player[i] = new Player();
            Type owner = typeof(Terraria.GameContent.QuickStacking), source = owner.GetNestedType("SourceInventory", BindingFlags.NonPublic);
            object inventory = Activator.CreateInstance(source);
            source.GetField("items").SetValue(inventory, new Item[0]); source.GetField("numItems").SetValue(inventory, 0);
            source.GetField("slots").SetValue(inventory, Array.CreateInstance(source.GetField("slots").FieldType.GetElementType(), 0));
            source.GetField("transferBlocked").SetValue(inventory, new bool[0]); source.GetField("position").SetValue(inventory, Main.LocalPlayer.Center);
            // Real native selective path, isolated tiles and an empty source: no
            // inventory transfer, packet, server or real-world save is involved.
            Vector2 old = Main.LocalPlayer.position;
            try
            {
                Main.LocalPlayer.position = new Vector2(1000000, 1000000); // Empty nearby set; native item sorting needs no fixture cache.
                owner.GetMethod("QuickStackToNearbyChests", Flags, null, new[] { typeof(Player), source, typeof(bool) }, null)
                    .Invoke(null, new[] { (object)Main.LocalPlayer, inventory, false });
            }
            finally { Main.LocalPlayer.position = old; }
        }
        private static void Reset()
        {
            Main.maxTilesX = 256; Main.maxTilesY = 128; Main.tile = new Tile[256, 128]; Main.chest = new Chest[8000];
            for (int x = 0; x < 256; x++) for (int y = 0; y < 128; y++) Main.tile[x, y] = new Tile();
        }
        private static void Place(int index, int x, int y, int type, int quantity)
        {
            int width = type == 88 ? 3 : 2;
            for (int dx = 0; dx < width; dx++) for (int dy = 0; dy < 2; dy++)
            { var tile = Main.tile[x + dx, y + dy]; tile.active(true); tile.type = (ushort)type; tile.frameX = (short)(dx * 18); tile.frameY = (short)(dy * 18); }
            var chest = Chest.CreateWorldChest(index, x, y); chest.name = "箱 " + index; chest.Resize(3);
            if (quantity > 0) { chest.item[0].SetDefaults(9); chest.item[0].stack = quantity; chest.item[1].SetDefaults(9); chest.item[1].stack = 1; }
        }
    }
}
