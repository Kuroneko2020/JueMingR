using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Announcements;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Map;
using Terraria.ObjectData;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal sealed class TargetValue
    {
        internal int ItemType, Quantity;
        internal bool EmptyAir, UiSlot, Literal;
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
        internal static TargetValue UiItem(int type, int quantity)
        {
            var value = new TargetValue { UiSlot = true, ItemType = type > 0 && quantity > 0 ? type : 0, Quantity = Math.Max(0, quantity) };
            if (value.ItemType > 0) value.Entries.Add(quantity + " 个 " + Name(Lang.GetItemNameValue(type)));
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
            for (int i = 0; i < Main.item.Length; i++)
            {
                WorldItem drop = Main.item[i]; if (drop == null || !drop.active || drop.inner == null || drop.inner.IsAir || !drop.Hitbox.Contains(position)) continue;
                long count = 0; int tx = (int)(drop.Center.X / 16), ty = (int)(drop.Center.Y / 16);
                for (int j = 0; j < Main.item.Length; j++)
                {
                    WorldItem other = Main.item[j]; if (other == null || !other.active || other.inner == null || other.inner.type != drop.inner.type || other.inner.stack <= 0) continue;
                    if (Math.Abs((int)(other.Center.X / 16) - tx) <= 1 && Math.Abs((int)(other.Center.Y / 16) - ty) <= 1) count += other.inner.stack;
                }
                value.ItemType = drop.inner.type; value.Quantity = (int)Math.Min(int.MaxValue, count); value.Entries.Add(count + " 个 " + Name(Lang.GetItemNameValue(value.ItemType))); return value;
            }
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
            bool tileHidden = tile.invisibleBlock() || tile.type == 541 || tile.type == 631 || tile.type == 19 && tile.frameY / 18 == 48;
            // Native DrawSingleTile admits these declared light-independent
            // layers before its separate echo gate. Light is not visibility.
            bool drawWithoutLight = tile.fullbrightBlock() || TileID.Sets.IgnoreDrawLightConditions[tile.type] ||
                Main.tileGlowMask[tile.type] != -1 || Main.tileFlame[tile.type] ||
                tile.wall > 0 && (tile.wall == 318 || tile.fullbrightWall());
            bool visibleTile = tile.active() && (!tileHidden || echo) && (lit || drawWithoutLight || VisionReveals(tile, x, y));
            if (visibleTile)
            {
                int item = VisibleContent(tile, x, y);
                bool resolvedContent = item > 0;
                if (!resolvedContent) item = catalog.PlacedItem(tile);
                value.ItemType = item; value.Quantity = item > 0 ? 1 : 0;
                value.Entries.Add(item > 0 ? Name(Lang.GetItemNameValue(item)) : MapName(tile, x, y) ?? "可见物件（暂无可靠物品对应）");
                if (!resolvedContent) FreezePlacement(value, tile, x, y, false, catalog);
            }
            if (lit && tile.liquid > 0 && (!tile.active() || !Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type] || tile.inActive()))
                value.Entries.Add(tile.liquidType() == 1 ? "熔岩" : tile.liquidType() == 2 ? "蜂蜜" : tile.liquidType() == 3 ? "微光" : "水");
            if (wires.Count > 0) value.Entries.Add(string.Join("、", wires));
            if (tile.actuator()) value.Entries.Add("制动器");
            // WallDrawing.FullTile explicitly does not occlude walls behind an
            // echo-painted foreground when echo vision is off. Keep the wall's
            // own echo gate independent, including fullbright hidden walls.
            bool wallExposed = !tile.active() || tile.invisibleBlock() && !echo;
            if (value.Entries.Count == 0 && tile.wall > 0 && (!tile.invisibleWall() && tile.wall != 318 || echo) && (lit || tile.fullbrightWall() || tile.wall == 318 && echo) && wallExposed)
            {
                int item = catalog.WallItem(tile.wall); value.ItemType = item; value.Quantity = item > 0 ? 1 : 0;
                value.Entries.Add(Name(item > 0 ? Lang.GetItemNameValue(item) : "背景墙"));
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
        private static int VisibleContent(Tile tile, int x, int y)
        {
            // .8 Lang.BuildMapAtlas identifies all three growth stages by this
            // style. This names the plant; it does not predict harvest drops.
            if (tile.type >= 82 && tile.type <= 84)
            { int style = tile.frameX / 18; return style >= 0 && style < herbItems.Length ? herbItems[style] : 0; }
            if (tile.type != 395) return 0;
            // Match TEItemFrame's 2x2 anchor. Ordinary clients may not have its
            // entity: read only what was received, without requesting/opening it.
            if (tile.frameX % 36 != 0) x--; if (tile.frameY % 36 != 0) y--;
            TEItemFrame frame;
            return TileEntity.TryGetAt(x, y, out frame) && frame.item != null && !frame.item.IsAir ? frame.item.type : 0;
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
            int item = value.WallPlacement ? catalog.WallItem(observed.wall) : catalog.PlacedItem(observed);
            value.ItemType = item; value.Quantity = item > 0 ? 1 : 0;
            if (item > 0) value.Entries[value.PlacementEntry] = Name(Lang.GetItemNameValue(item));
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
