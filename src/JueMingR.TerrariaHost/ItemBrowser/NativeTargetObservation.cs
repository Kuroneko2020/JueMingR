using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Announcements;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Map;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal sealed class TargetValue
    {
        internal int ItemType, Quantity;
        internal bool EmptyAir, UiSlot, Literal, JoinItems;
        internal NativeDisplayContents Display;
        internal Tile Placement;
        internal bool WallPlacement;
        internal int PlacementX, PlacementY, PlacementEntry;
        internal readonly List<string> Entries = new List<string>();
    }
    internal static class NativeTargetObservation
    {
        private static readonly int[] herbItems = { 313, 314, 315, 316, 317, 318, 2358 };
        private static readonly string[] airPhrases = {
            "这里毛都没有", "这里只有空气", "这里空空如也", "这里什么都没有", "这里啥也没有", "这里只有寂寞", "这里空得很彻底",
            "这里一片安静", "这里没有目标", "这里看起来很干净", "这里什么也没发现", "这里只有风经过", "这里空无一物", "这里没有东西",
            "这里连影子都没有", "这里值得路过", "这里确认是空气", "？！滚木！？", "一位慈祥的老奶奶"
        };
        private static readonly MethodInfo dangerous = typeof(Terraria.GameContent.Drawing.TileDrawing).GetMethod("IsTileDangerous", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(Player), typeof(int), typeof(int), typeof(Tile), typeof(ushort) }, null);
        internal static string Name(string value) { return SafeChatText.CleanName(value, 80); }
        internal static string ItemName(int type)
        {
            if (type <= 0 || type >= ItemID.Count) return "";
            // Only native IDs create markup; localized/external names still pass
            // the same sanitizer. Quantity stays in text, not a clamped tag stack.
            return Name(Lang.GetItemNameValue(type)) + " [i:" + type.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
        }
        internal static TargetValue UiItem(int type, int quantity)
        {
            var value = new TargetValue { UiSlot = true, ItemType = type > 0 && type < ItemID.Count && quantity > 0 ? type : 0, Quantity = Math.Max(0, quantity) };
            if (value.ItemType > 0) value.Entries.Add(quantity + " 个 " + ItemName(type));
            return value;
        }
        internal static TargetValue World(Vector2 point, NativeItemCatalog catalog, bool announcement)
        {
            catalog.ObserveContext();
            var value = new TargetValue();
            if (Main.mapFullscreen) { value.Literal = true; value.Entries.Add("大地图已打开，无法确定世界目标"); return value; }
            Point position = point.ToPoint();
            for (int i = 0; announcement && i < Main.player.Length && value.Entries.Count < 12; i++)
            {
                Player player = Main.player[i]; if (player == null || !player.active || player.dead || player.ghost || !player.Hitbox.Contains(position)) continue;
                value.Entries.Add(Name(player.name) + " 生命 " + player.statLife + "/" + player.statLifeMax2 +
                    (i == Main.myPlayer ? "，魔力 " + player.statMana + "/" + player.statManaMax2 : ""));
            }
            for (int i = 0; announcement && i < Main.npc.Length && value.Entries.Count < 12; i++)
            {
                NPC npc = Main.npc[i]; if (npc == null || !npc.active || npc.hide || npc.life <= 0 || !npc.Hitbox.Contains(position)) continue;
                value.Entries.Add(Name(npc.FullName) + " 生命 " + npc.life + "/" + npc.lifeMax);
            }
            if (value.Entries.Count != 0) { if (value.Entries.Count >= 12) value.Entries.Add("目标较多，仅列出前 12 个"); return value; }
            if (DroppedItems(value, position, announcement)) return value;
            int x = (int)Math.Floor(point.X / 16), y = (int)Math.Floor(point.Y / 16);
            if (Main.tile == null || x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY || Main.netMode == 1 && (Main.sectionManager == null || !Main.sectionManager.TileLoaded(x, y)))
            { value.Literal = true; value.Entries.Add("这里的世界资料尚未接收"); return value; }
            Tile tile = Main.tile[x, y]; if (tile == null) { value.Literal = true; value.Entries.Add("这里暂时无法确认"); return value; }
            // Wires are an explicit product exception, never proof that another
            // dark/echo layer is visible. Keep their result independent below.
            var wires = new List<string>();
            if (tile.wire()) wires.Add("红线"); if (tile.wire2()) wires.Add("蓝线"); if (tile.wire3()) wires.Add("绿线"); if (tile.wire4()) wires.Add("黄线");
            bool lit = false; try { Color color = Lighting.GetColor(x, y); lit = color.R > 0 || color.G > 0 || color.B > 0; } catch { }
            bool echo = Main.ShouldShowInvisibleBlocksAndWalls();
            Tile targetTile = tile; int targetX = x, targetY = y; bool ambiguousJar = false;
            if (tile.active() && tile.type == 698)
            {
                // Native jar drawing uses only its upper tile. Coating/lighting
                // on the lower occupied cell does not control that sprite; freeze
                // the draw anchor too so coating it during cold capture cancels.
                targetY -= tile.frameY / 18 % 2;
                targetTile = NativeDisplayContents.Loaded(x, targetY) ? Main.tile[x, targetY] : null;
                if (targetTile == null || targetTile.type != 698 || targetTile.frameY != 0 || targetTile.frameX != tile.frameX) targetTile = null;
            }
            bool visibleTile = Visible(targetTile, targetX, targetY, echo);
            // The jar's native sprite is 36x44 over a 1x2 tile body. Resolve its
            // static visible footprint only when no foreground was hit; keep
            // liquid/wires/wall observations tied to the original cursor cell.
            if (!visibleTile && !tile.active()) visibleTile = TryJar(point, x, y, echo, out targetTile, out targetX, out targetY, out ambiguousJar);
            if (visibleTile)
            {
                int herb = Herb(targetTile);
                value.Display = NativeDisplayContents.Capture(targetTile, targetX, targetY);
                int item = herb > 0 ? herb : catalog.PlacedItem(targetTile);
                value.ItemType = value.Display != null && value.Display.ItemType > 0 ? value.Display.ItemType : item;
                value.Quantity = value.Display != null && value.Display.ItemType > 0 ? value.Display.Quantity : item > 0 ? 1 : 0;
                string body = item > 0 ? ItemName(item) : value.Display?.FallbackName ?? MapName(targetTile, targetX, targetY) ?? "可见物件（暂无可靠物品对应）";
                value.Entries.Add(value.Display == null ? body : value.Display.Describe(body));
                if (herb == 0) FreezePlacement(value, targetTile, targetX, targetY, false, catalog);
            }
            else if (ambiguousJar) value.Entries.Add("可见物件（重叠，无法确定目标）");
            if (lit && tile.liquid > 0 && (!tile.active() || !Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type] || tile.inActive()))
                value.Entries.Add(tile.liquidType() == 1 ? "熔岩" : tile.liquidType() == 2 ? "蜂蜜" : tile.liquidType() == 3 ? "微光" : "水");
            if (wires.Count > 0) value.Entries.Add(string.Join("、", wires));
            if (tile.actuator()) value.Entries.Add("执行器");
            // WallDrawing.FullTile explicitly does not occlude walls behind an
            // echo-painted foreground when echo vision is off. Keep the wall's
            // own echo gate independent, including fullbright hidden walls.
            bool wallExposed = !tile.active() || tile.invisibleBlock() && !echo;
            if (value.Entries.Count == 0 && tile.wall > 0 && (!tile.invisibleWall() && tile.wall != 318 || echo) && (lit || tile.fullbrightWall() || tile.wall == 318 && echo) && wallExposed)
            {
                int item = catalog.WallItem(tile.wall); value.ItemType = item; value.Quantity = item > 0 ? 1 : 0;
                value.Entries.Add(item > 0 ? ItemName(item) : "背景墙");
                FreezePlacement(value, tile, x, y, true, catalog);
            }
            if (value.Entries.Count == 0)
            {
                value.EmptyAir = lit && !tile.active() && tile.wall == 0 && tile.liquid == 0;
                value.Literal = true;
                // Vary Legacy's air phrases without consuming gameplay RNG.
                uint choice = unchecked((uint)x * 73856093u ^ (uint)y * 19349663u ^ (uint)Main.GameUpdateCount);
                value.Entries.Add(value.EmptyAir ? airPhrases[choice % airPhrases.Length] : "这里看不见东西");
            }
            return value;
        }
        private static bool TryJar(Vector2 point, int x, int y, bool echo, out Tile tile, out int tx, out int ty, out bool ambiguous)
        {
            tile = null; tx = ty = 0; ambiguous = false;
            // At most twelve anchors, only on an explicit world gesture. Do
            // not inspect unreceived sections or call native placement helpers.
            for (int ax = x - 1; ax <= x + 1; ax++) for (int ay = y - 2; ay <= y + 1; ay++)
            {
                if (!NativeDisplayContents.Loaded(ax, ay)) continue;
                Tile candidate = Main.tile[ax, ay];
                if (candidate == null || !candidate.active() || candidate.type != 698 || candidate.frameY != 0 || candidate.frameX < 0 || candidate.invisibleBlock() && !echo) continue;
                int top = ay * 16 - 2;
                if (!NativeDisplayContents.Loaded(ax, ay - 1)) continue;
                Tile above = Main.tile[ax, ay - 1];
                if (above != null && above.active() && TileID.Sets.Platforms[above.type] && !above.halfBrick() && above.slope() == 0) top -= 8;
                if (point.X < ax * 16 - 10 || point.X >= ax * 16 + 26 || point.Y < top || point.Y >= top + 44) continue;
                if (!Visible(candidate, ax, ay, echo)) continue;
                // Multiple overlapping silhouettes are ambiguous. Leave the
                // target unknown rather than picking hidden neighbour contents.
                if (tile != null) { tile = null; ambiguous = true; return false; }
                tile = candidate; tx = ax; ty = ay;
            }
            return tile != null;
        }
        private static bool Visible(Tile tile, int x, int y, bool echo)
        {
            if (tile == null || !tile.active()) return false;
            bool hidden = tile.invisibleBlock() || tile.type == 541 || tile.type == 631 || tile.type == 19 && tile.frameY / 18 == 48;
            if (hidden && !echo) return false;
            // Native light-independent drawing still has its separate echo gate.
            if (tile.fullbrightBlock() || TileID.Sets.IgnoreDrawLightConditions[tile.type] || Main.tileGlowMask[tile.type] != -1 || Main.tileFlame[tile.type] ||
                tile.wall > 0 && (tile.wall == 318 || tile.fullbrightWall()) || NativeDisplayContents.JarGlows(tile, x, y)) return true;
            try { Color light = Lighting.GetColor(x, y); if (light.R > 0 || light.G > 0 || light.B > 0) return true; } catch { }
            return VisionReveals(tile, x, y);
        }
        private static int Herb(Tile tile)
        {
            // .8 Lang.BuildMapAtlas identifies all three growth stages by this
            // style. This names the plant; it does not predict harvest drops.
            if (tile.type >= 82 && tile.type <= 84)
            { int style = tile.frameX / 18; return style >= 0 && style < herbItems.Length ? herbItems[style] : 0; }
            return 0;
        }
        private static bool DroppedItems(TargetValue value, Point position, bool announcement)
        {
            WorldItem first = null;
            foreach (var drop in Main.item)
                if (drop != null && drop.active && drop.inner != null && !drop.inner.IsAir && drop.Hitbox.Contains(position)) { first = drop; break; }
            if (first == null) return false;
            int tx = (int)Math.Floor(first.Center.X / 16), ty = (int)Math.Floor(first.Center.Y / 16);
            var counts = new Dictionary<int, long>(); var order = new List<int>();
            // Keep Legacy's fixed neighbourhood, not a transitive flood fill.
            // One additional pass aggregates every type, instead of rescanning
            // the world-item array once for each co-located item.
            foreach (var drop in Main.item)
            {
                if (drop == null || !drop.active || drop.inner == null || drop.inner.IsAir || !announcement && drop.inner.type != first.inner.type ||
                    Math.Abs((int)Math.Floor(drop.Center.X / 16) - tx) > 1 || Math.Abs((int)Math.Floor(drop.Center.Y / 16) - ty) > 1) continue;
                long count; if (!counts.TryGetValue(drop.inner.type, out count)) order.Add(drop.inner.type);
                counts[drop.inner.type] = count + drop.inner.stack;
            }
            value.ItemType = first.inner.type; value.Quantity = (int)Math.Min(int.MaxValue, counts[first.inner.type]); value.JoinItems = announcement;
            foreach (int type in order) value.Entries.Add(counts[type] + " 个 " + ItemName(type));
            return true;
        }
        private static string MapName(Tile tile, int x, int y)
        {
            var counts = MapHelper.tileOptionCounts; var lookups = MapHelper.tileLookup; var legend = Lang._mapLegendCache;
            int type = tile.type;
            if (counts == null || lookups == null || legend == null || type >= counts.Length || type >= lookups.Length || counts[type] <= 0) return null;
            int option = 0;
            // These .8 options inspect neighbours or SceneMetrics. Use their
            // generic map label rather than reading unreceived neighbouring tiles.
            if (type != 80 && type != 529 && type != 530 && type != 461) MapHelper.GetTileBaseOption(x, y, type, tile, ref option);
            if (option < 0 || option >= counts[type]) return null;
            int index = lookups[type] + option;
            if (index < 0 || index >= legend.Length || legend[index] == null) return null;
            string text = Name(legend[index].Value); return text.Length == 0 ? null : text;
        }
        private static void FreezePlacement(TargetValue value, Tile tile, int x, int y, bool wall, NativeItemCatalog catalog)
        {
            if (catalog.PlacementReady) return;
            value.Placement = new Tile(); value.Placement.CopyFrom(tile);
            value.PlacementX = x; value.PlacementY = y; value.WallPlacement = wall; value.PlacementEntry = value.Entries.Count - 1;
        }
        internal static bool ResolvePlacement(TargetValue value, NativeItemCatalog catalog)
        {
            Tile observed = value.Placement, current = Main.tile[value.PlacementX, value.PlacementY];
            // Resolve only the frozen target. A changed tile cancels instead of
            // silently declaring a replacement after the bounded cold capture.
            if (current == null || current.type != observed.type || current.wall != observed.wall || current.frameX != observed.frameX || current.frameY != observed.frameY ||
                current.active() != observed.active() || current.invisibleBlock() != observed.invisibleBlock() || current.invisibleWall() != observed.invisibleWall()) return false;
            if (value.Display != null && !value.Display.StillMatches()) return false;
            int item = value.WallPlacement ? catalog.WallItem(observed.wall) : catalog.PlacedItem(observed);
            value.ItemType = value.Display != null && value.Display.ItemType > 0 ? value.Display.ItemType : item;
            value.Quantity = value.Display != null && value.Display.ItemType > 0 ? value.Display.Quantity : item > 0 ? 1 : 0;
            if (item > 0) { string name = ItemName(item); value.Entries[value.PlacementEntry] = value.Display == null ? name : value.Display.Describe(name); }
            value.Placement = null; return true;
        }
        private static bool VisionReveals(Tile tile, int x, int y)
        {
            Player player = Main.SceneMetrics?.PerspectivePlayer; if (player == null) return false;
            if (player.findTreasure && Main.IsTileSpelunkable(tile)) return true;
            Color color = Color.White;
            if (player.biomeSight && Main.IsTileBiomeSightable(tile.type, tile.frameX, tile.frameY, ref color)) return true;
            return player.dangerSense && dangerous != null && (bool)dangerous.Invoke(null, new object[] { player, x, y, tile, tile.type });
        }
    }
}
