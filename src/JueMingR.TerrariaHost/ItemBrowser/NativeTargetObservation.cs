using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Announcements;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Map;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal sealed class TargetValue
    {
        internal int ItemType, Quantity;
        internal bool EmptyAir, UiSlot;
        internal readonly List<string> Entries = new List<string>();
    }
    internal static class NativeTargetObservation
    {
        private static readonly MethodInfo dangerous = typeof(Terraria.GameContent.Drawing.TileDrawing).GetMethod("IsTileDangerous", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(Player), typeof(int), typeof(int), typeof(Tile), typeof(ushort) }, null);
        internal static string Name(string value) { return SafeChatText.CleanName(value, 80); }
        internal static TargetValue UiItem(int type, int quantity)
        {
            var value = new TargetValue { UiSlot = true, ItemType = type > 0 && quantity > 0 ? type : 0, Quantity = Math.Max(0, quantity) };
            if (value.ItemType > 0) value.Entries.Add(Name(Lang.GetItemNameValue(type)) + " ×" + quantity);
            return value;
        }
        internal static TargetValue World(Vector2 point, NativeItemCatalog catalog, bool announcement)
        {
            var value = new TargetValue();
            if (Main.mapFullscreen) { value.Entries.Add("大地图已打开，无法确定世界目标"); return value; }
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
            if (value.Entries.Count != 0) return value;
            for (int i = 0; i < Main.item.Length; i++)
            {
                WorldItem drop = Main.item[i]; if (drop == null || !drop.active || drop.inner == null || drop.inner.IsAir || !drop.Hitbox.Contains(position)) continue;
                long count = 0; int tx = (int)(drop.Center.X / 16), ty = (int)(drop.Center.Y / 16);
                for (int j = 0; j < Main.item.Length; j++)
                {
                    WorldItem other = Main.item[j]; if (other == null || !other.active || other.inner == null || other.inner.type != drop.inner.type || other.inner.stack <= 0) continue;
                    if (Math.Abs((int)(other.Center.X / 16) - tx) <= 1 && Math.Abs((int)(other.Center.Y / 16) - ty) <= 1) count += other.inner.stack;
                }
                value.ItemType = drop.inner.type; value.Quantity = (int)Math.Min(int.MaxValue, count); value.Entries.Add(Name(Lang.GetItemNameValue(value.ItemType)) + " ×" + count); return value;
            }
            int x = (int)Math.Floor(point.X / 16), y = (int)Math.Floor(point.Y / 16);
            if (Main.tile == null || x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY || Main.netMode == 1 && (Main.sectionManager == null || !Main.sectionManager.TileLoaded(x, y)))
            { value.Entries.Add("这里的世界资料尚未接收"); return value; }
            Tile tile = Main.tile[x, y]; if (tile == null) { value.Entries.Add("这里暂时无法确认"); return value; }
            // Wires are an explicit product exception, never proof that another
            // dark/echo layer is visible. Keep their result independent below.
            var wires = new List<string>();
            if (tile.wire()) wires.Add("红线"); if (tile.wire2()) wires.Add("蓝线"); if (tile.wire3()) wires.Add("绿线"); if (tile.wire4()) wires.Add("黄线"); if (tile.actuator()) wires.Add("制动器");
            bool lit = false; try { Color color = Lighting.GetColor(x, y); lit = color.R > 0 || color.G > 0 || color.B > 0; } catch { }
            bool echo = Main.ShouldShowInvisibleBlocksAndWalls();
            bool tileHidden = tile.invisibleBlock() || tile.type == 541 || tile.type == 631 || tile.type == 19 && tile.frameY / 18 == 48;
            bool visibleTile = tile.active() && (!tileHidden || echo) && (lit || tile.fullbrightBlock() || VisionReveals(tile, x, y));
            if (visibleTile)
            {
                int item = catalog.PlacedItem(tile);
                value.ItemType = item; value.Quantity = item > 0 ? 1 : 0;
                string name = item > 0 ? Lang.GetItemNameValue(item) : Lang.GetMapObjectName(MapHelper.TileToLookup(tile.type, 0));
                value.Entries.Add(Name(string.IsNullOrEmpty(name) ? "可见物件" : name));
            }
            if (lit && tile.liquid > 0 && (!tile.active() || !Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type] || tile.inActive()))
                value.Entries.Add(tile.liquidType() == 1 ? "熔岩" : tile.liquidType() == 2 ? "蜂蜜" : tile.liquidType() == 3 ? "微光" : "水");
            value.Entries.AddRange(wires);
            if (value.Entries.Count == 0 && tile.wall > 0 && (!tile.invisibleWall() && tile.wall != 318 || echo) && (lit || tile.fullbrightWall()) && !tile.active())
            {
                int item = catalog.WallItem(tile.wall); value.ItemType = item; value.Quantity = item > 0 ? 1 : 0;
                value.Entries.Add(Name(item > 0 ? Lang.GetItemNameValue(item) : "背景墙"));
            }
            if (value.Entries.Count == 0)
            {
                value.EmptyAir = lit && !tile.active() && tile.wall == 0 && tile.liquid == 0;
                value.Entries.Add(value.EmptyAir ? "这里是空气" : "这里看不清，无法确认目标");
            }
            return value;
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
