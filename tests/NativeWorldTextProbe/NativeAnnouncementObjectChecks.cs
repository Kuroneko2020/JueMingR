using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeAnnouncementObjectChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(Assembly assembly, object catalog)
        {
            var observe = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeTargetObservation").GetMethod("World", Flags);
            var failures = new List<string>();
            Action<bool, string> check = (ok, text) => { if (!ok) failures.Add(text); };
            Func<int, int, object> at = (x, y) => observe.Invoke(null, new object[] { new Vector2(x * 16 + 8, y * 16 + 8), catalog, true });
            Func<object, string> textOf = value => string.Join("|", (List<string>)Get(value, "Entries"));
            int[] types = { 27, 395, 471, 698, 520, 470, 475 };
            bool[] oldFlags = types.Select(t => Main.tileFrameImportant[t]).ToArray();
            var savedTiles = new Tile[8, 8];
            for (int x = 0; x < 8; x++) for (int y = 0; y < 8; y++) { savedTiles[x, y] = Main.tile[18 + x, 18 + y]; Main.tile[18 + x, 18 + y] = new Tile(); }
            var origin = new Point16(20, 20); TileEntity oldEntity; bool hadEntity = TileEntity.ByPosition.TryGetValue(origin, out oldEntity);
            WorldItem[] drops = (WorldItem[])Main.item.Clone();
            try
            {
                foreach (int type in types) Main.tileFrameImportant[type] = true;
                // Native PlaceSunflower allows each lower cell to choose a
                // different one of these three appearance frames.
                for (int variant = 0; variant < 3; variant++) for (int x = 0; x < 2; x++) for (int y = 0; y < 4; y++)
                {
                    Tile tile = Make(27, variant * 36 + x * 18, y * 18);
                    check((int)Call(catalog, "PlacedItem", tile) == 63, "sunflower appearance/cell " + variant + "/" + x + "/" + y);
                }
                // Independent .8 identities and entity dimensions; actual native
                // dictionaries/Items are read by production, not a fake resolver.
                var cases = new[] {
                    Tuple.Create(395, 3270, 2, 2, (TileEntity)new TEItemFrame()),
                    Tuple.Create(471, 2699, 3, 3, (TileEntity)new TEWeaponsRack()),
                    Tuple.Create(698, 5472, 1, 2, (TileEntity)new TEDeadCellsDisplayJar()),
                    Tuple.Create(520, 4326, 1, 1, (TileEntity)new TEFoodPlatter())
                };
                foreach (var c in cases)
                {
                    for (int x = 0; x < 4; x++) for (int y = 0; y < 5; y++) Main.tile[20 + x, 20 + y] = new Tile();
                    for (int x = 0; x < c.Item3; x++) for (int y = 0; y < c.Item4; y++) Main.tile[20 + x, 20 + y] = Make(c.Item1, x * 18, y * 18);
                    c.Item5.Position = origin; TileEntity.ByPosition[origin] = c.Item5;
                    var item = new Item(); Set(c.Item5, "item", item);
                    string furniture = Lang.GetItemNameValue(c.Item2);
                    for (int x = 0; x < c.Item3; x++) for (int y = 0; y < c.Item4; y++)
                        check(textOf(at(20 + x, 20 + y)).Contains(furniture), "empty support identifies its body at every cell " + c.Item1);
                    item.SetDefaults(4); item.stack = 1;
                    if (c.Item1 == 698)
                    {
                        foreach (var p in new[] { new Vector2(312, 328), new Vector2(344, 328), new Vector2(328, 358) })
                            check(textOf(observe.Invoke(null, new object[] { p, catalog, true })).Contains("放在" + furniture + "的"), "jar visible sprite extends beyond its 1x2 body");
                        Main.tile[20, 20].invisibleBlock(true);
                        check(!textOf(observe.Invoke(null, new object[] { new Vector2(312, 328), catalog, true })).Contains(Lang.GetItemNameValue(4)), "jar exterior does not reveal an echo-hidden carrier");
                        Main.tile[20, 20].invisibleBlock(false);
                        Main.tile[20, 19] = Make(19, 0, 0);
                        check(textOf(observe.Invoke(null, new object[] { new Vector2(312, 312), catalog, true })).Contains("放在" + furniture + "的"), "platform-hung jar uses native vertical draw offset");
                        Main.tile[20, 19] = new Tile();
                    }
                    var frozen = at(20, 20);
                    for (int x = 0; x < c.Item3; x++) for (int y = 0; y < c.Item4; y++)
                    {
                        string text = textOf(at(20 + x, 20 + y));
                        check(text.Contains("放在" + furniture + "的") && text.Contains(Lang.GetItemNameValue(4)), "support and actual item remain together " + c.Item1);
                    }
                    var sections = Main.sectionManager; int mode = Main.netMode;
                    try
                    {
                        Main.netMode = 1; Main.sectionManager = new Terraria.WorldSections(1, 1);
                        check(textOf(at(20, 20)) == "这里的世界资料尚未接收", "unreceived client body cannot reveal stale entity contents " + c.Item1);
                        Main.sectionManager.SetAllSectionsLoaded();
                        check(textOf(at(20, 20)).Contains("放在" + furniture + "的"), "ordinary client resolves only existing received entity " + c.Item1);
                    }
                    finally { Main.netMode = mode; Main.sectionManager = sections; }
                    item.SetDefaults(8); item.stack = 1;
                    check(!(bool)Call(Get(frozen, "Display"), "StillMatches"), "changed received content cancels a cold pending display " + c.Item1);
                    TileEntity.ByPosition[origin] = new TEFoodPlatter { Position = origin, item = new Item() };
                    if (c.Item1 != 520) check(textOf(at(20, 20)).Contains("内容尚未确认"), "wrong entity kind never confirms an empty support " + c.Item1);
                    TileEntity.ByPosition.Remove(origin);
                    string unknown = textOf(at(20, 20));
                    check(unknown.Contains(furniture) && unknown.Contains("内容尚未确认") && !unknown.Contains(Lang.GetItemNameValue(4)), "missing entity is named support with unknown contents " + c.Item1);
                }
                foreach (var c in new[] { Tuple.Create(470, 2, 3, (TileEntity)new TEDisplayDoll(), "_equip", "_misc"), Tuple.Create(475, 3, 4, (TileEntity)new TEHatRack(), "_items", "_dyes") })
                {
                    for (int x = 0; x < 4; x++) for (int y = 0; y < 5; y++) Main.tile[20 + x, 20 + y] = new Tile();
                    for (int x = 0; x < c.Item2; x++) for (int y = 0; y < c.Item3; y++) Main.tile[20 + x, 20 + y] = Make(c.Item1, x * 18, y * 18);
                    c.Item4.Position = origin; TileEntity.ByPosition[origin] = c.Item4;
                    ((Item[])Get(c.Item4, c.Item5))[0].SetDefaults(4); ((Item[])Get(c.Item4, c.Item6))[0].SetDefaults(8);
                    for (int x = 0; x < c.Item2; x++) for (int y = 0; y < c.Item3; y++)
                    {
                        string text = textOf(at(20 + x, 20 + y));
                        check(text.Contains("放在") && text.Contains(Lang.GetItemNameValue(4)) && text.Contains(Lang.GetItemNameValue(8)), "multiple received display slots and every body cell " + c.Item1);
                    }
                    Main.tile[20, 20] = new Tile();
                    check(textOf(at(21, 21)).Contains("内容尚未确认"), "incomplete body cannot authorize stale tile-entity contents " + c.Item1);
                    TileEntity.ByPosition.Remove(origin);
                }
                for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new WorldItem();
                Main.item[0] = Drop(9, 3, 20, 20); Main.item[1] = Drop(8, 7, 20, 20); Main.item[2] = Drop(9, 5, 21, 20);
                Main.item[3] = Drop(9, 99, 23, 20); Main.item[4] = Drop(8, 4, 20, 21);
                string all = textOf(at(20, 20));
                check(all.Contains("8 个 " + Lang.GetItemNameValue(9)) && all.Contains("11 个 " + Lang.GetItemNameValue(8)) && !all.Contains("107 个"), "all co-located drop types aggregate once within the fixed local neighbourhood");
                object dropsValue = at(20, 20); check((bool)Get(dropsValue, "JoinItems"), "mixed dropped groups use the item conjunction at final delivery");
                object query = observe.Invoke(null, new object[] { new Vector2(328, 328), catalog, false });
                check((int)Get(query, "ItemType") == 9 && (int)Get(query, "Quantity") == 8 && ((List<string>)Get(query, "Entries")).Count == 1, "single-item query preserves deterministic primary target");
                for (int i = 0; i < Main.item.Length; i++) Main.item[i] = new WorldItem();
                var layered = Make(27, 0, 0); layered.liquid = 60; layered.wall = 4; layered.wire(true); layered.wire2(true); layered.actuator(true); Main.tile[20, 20] = layered;
                check(textOf(at(20, 20)) == Lang.GetItemNameValue(63) + "|水|红线、蓝线|执行器", "same-cell visible object/liquid/wires/actuator retain Legacy ordering without adding occluded wall");
                layered.invisibleBlock(true);
                check(!textOf(at(20, 20)).Contains(Lang.GetItemNameValue(63)) && textOf(at(20, 20)).Contains("红线、蓝线"), "wires do not reveal hidden object identity");
                // Native jar glow is drawn from a received entity even when
                // Lighting.GetColor is black; the ordinary glow-mask array is
                // explicitly absent here, as in a real initialized game.
                short glow = Main.tileGlowMask[698]; bool flame = Main.tileFlame[698];
                var otherOrigin = new Point16(21, 20); TileEntity otherOld; bool hadOther = TileEntity.ByPosition.TryGetValue(otherOrigin, out otherOld);
                try
                {
                    Main.tileGlowMask[698] = -1; Main.tileFlame[698] = false; NativeAnnouncementTargetChecks.TestLight = Color.Black;
                    for (int x = 18; x < 26; x++) for (int y = 18; y < 26; y++) Main.tile[x, y] = new Tile();
                    Main.tile[20, 20] = Make(698, 0, 0); Main.tile[20, 21] = Make(698, 0, 18);
                    var jar = new TEDeadCellsDisplayJar { Position = origin, item = new Item() }; TileEntity.ByPosition[origin] = jar;
                    foreach (bool occupied in new[] { false, true })
                    {
                        if (occupied) jar.item.SetDefaults(4);
                        foreach (var point in new[] { new Vector2(328, 328), new Vector2(328, 344), new Vector2(312, 328) })
                            check((int)Get(observe.Invoke(null, new object[] { point, catalog, true }), "ItemType") == (occupied ? 4 : 5472), "native dark jar glow reveals empty/occupied body and exterior");
                    }
                    Main.tile[20, 20].invisibleBlock(true); Main.tile[20, 21].invisibleBlock(true);
                    check((int)Get(at(20, 20), "ItemType") == 0 && (int)Get(observe.Invoke(null, new object[] { new Vector2(312, 328), catalog, true }), "ItemType") == 0, "jar glow never bypasses echo hiding");
                    Main.tile[20, 21].invisibleBlock(false);
                    foreach (var light in new[] { Color.Black, Color.White })
                    {
                        NativeAnnouncementTargetChecks.TestLight = light;
                        foreach (var point in new[] { new Vector2(328, 328), new Vector2(328, 344), new Vector2(312, 328) })
                            check((int)Get(observe.Invoke(null, new object[] { point, catalog, true }), "ItemType") == 0, "only jar draw-anchor echo coating hides the entire visible object");
                    }
                    NativeAnnouncementTargetChecks.TestLight = Color.Black;
                    Main.tile[20, 20].invisibleBlock(false); Main.tile[20, 21].invisibleBlock(true);
                    check((int)Get(at(20, 21), "ItemType") == 4, "coating the non-drawing jar lower cell does not hide the native sprite");
                    Main.tile[20, 20].invisibleBlock(false); Main.tile[20, 21].invisibleBlock(false);
                    TileEntity.ByPosition.Remove(origin);
                    check((int)Get(at(20, 20), "ItemType") == 0, "missing entity cannot manufacture native dark jar glow");
                    TileEntity.ByPosition[origin] = jar; NativeAnnouncementTargetChecks.TestLight = Color.White;
                    Main.tile[21, 20] = Make(698, 18, 0); Main.tile[21, 21] = Make(698, 18, 18);
                    TileEntity.ByPosition[otherOrigin] = new TEDeadCellsDisplayJar { Position = otherOrigin, item = new Item() };
                    object overlap = observe.Invoke(null, new object[] { new Vector2(328, 358), catalog, true });
                    check(!(bool)Get(overlap, "EmptyAir") && (int)Get(overlap, "ItemType") == 0 && textOf(overlap).Contains("重叠"), "overlapping jar outlines are an ambiguous object, never air or an arbitrary content");
                }
                finally { Main.tileGlowMask[698] = glow; Main.tileFlame[698] = flame; NativeAnnouncementTargetChecks.TestLight = Color.White; if (hadOther) TileEntity.ByPosition[otherOrigin] = otherOld; else TileEntity.ByPosition.Remove(otherOrigin); }
                Require(failures.Count == 0, "object-family regressions: " + string.Join("; ", failures));
                Console.WriteLine("PASS: all sunflower random-frame cells, empty/occupied/unknown supports, and mixed dropped stacks.");
            }
            finally
            {
                for (int i = 0; i < types.Length; i++) Main.tileFrameImportant[types[i]] = oldFlags[i];
                for (int x = 0; x < 8; x++) for (int y = 0; y < 8; y++) Main.tile[18 + x, 18 + y] = savedTiles[x, y];
                for (int i = 0; i < drops.Length; i++) Main.item[i] = drops[i];
                if (hadEntity) TileEntity.ByPosition[origin] = oldEntity; else TileEntity.ByPosition.Remove(origin);
            }
        }
        private static Tile Make(int type, int frameX, int frameY)
        { var tile = new Tile { type = (ushort)type, frameX = (short)frameX, frameY = (short)frameY }; tile.active(true); return tile; }
        private static WorldItem Drop(int type, int stack, int x, int y)
        { var item = new Item(); item.SetDefaults(type); item.stack = stack; return new WorldItem(item) { position = new Vector2(x * 16, y * 16), width = 16, height = 16 }; }
    }
}
