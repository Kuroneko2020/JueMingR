using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Announcements;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // A display body and its received contents are distinct facts. Missing or
    // incomplete native entities never become an empty-display assertion.
    internal sealed class NativeDisplayContents
    {
        private static readonly FieldInfo dollEquip = Field(typeof(TEDisplayDoll), "_equip"), dollDyes = Field(typeof(TEDisplayDoll), "_dyes"), dollMisc = Field(typeof(TEDisplayDoll), "_misc");
        private static readonly FieldInfo hats = Field(typeof(TEHatRack), "_items"), hatDyes = Field(typeof(TEHatRack), "_dyes");
        private readonly int x, y, type, width, height;
        private TileEntity entity;
        private int[] facts;
        internal bool Known { get; private set; }
        internal int ItemType { get; private set; }
        internal int Quantity { get; private set; }
        internal string FallbackName { get; private set; }
        private NativeDisplayContents(int x, int y, int type, int width, int height, string name)
        { this.x = x; this.y = y; this.type = type; this.width = width; this.height = height; FallbackName = name; }
        private static FieldInfo Field(Type type, string name) { return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); }
        internal static NativeDisplayContents Capture(Tile tile, int x, int y)
        {
            int width, height; string name;
            switch (tile.type)
            {
                case 395: width = height = 2; name = "展示框"; break;
                case 471: width = height = 3; name = "武器架"; break;
                case 698: width = 1; height = 2; name = "物品瓶"; break;
                case 520: width = height = 1; name = "餐盘"; break;
                case 470: width = 2; height = 3; name = "人体模型"; break;
                case 475: width = 3; height = 4; name = "帽架"; break;
                default: return null;
            }
            if (tile.frameX >= 0 && tile.frameY >= 0) { x -= tile.frameX / 18 % width; y -= tile.frameY / 18 % height; }
            var result = new NativeDisplayContents(x, y, tile.type, width, height, name);
            if (tile.frameX < 0 || tile.frameY < 0 || !result.CompleteBody()) return result;
            TileEntity found;
            if (!TileEntity.ByPosition.TryGetValue(new Point16(x, y), out found) || found.Position.X != x || found.Position.Y != y) return result;
            result.entity = found; result.facts = result.ReadFacts(); result.Known = result.facts != null;
            if (result.Known) for (int i = 0; i < result.facts.Length; i += 2)
                if (result.facts[i] > 0 && result.facts[i + 1] > 0) { result.ItemType = result.facts[i]; result.Quantity = result.facts[i + 1]; break; }
            return result;
        }
        private bool CompleteBody()
        {
            for (int dx = 0; dx < width; dx++) for (int dy = 0; dy < height; dy++)
            {
                int tx = x + dx, ty = y + dy;
                if (!Loaded(tx, ty)) return false;
                var tile = Main.tile[tx, ty];
                if (tile == null || !tile.active() || tile.type != type || tile.frameX < 0 || tile.frameY < 0 || tile.frameX / 18 % width != dx || tile.frameY / 18 % height != dy) return false;
            }
            return true;
        }
        internal static bool Loaded(int x, int y)
        { return Main.tile != null && x >= 0 && y >= 0 && x < Main.maxTilesX && y < Main.maxTilesY && (Main.netMode != 1 || Main.sectionManager != null && Main.sectionManager.TileLoaded(x, y)); }
        internal static bool JarGlows(Tile tile, int x, int y)
        {
            if (tile.type != 698 || tile.frameX < 0 || tile.frameY < 0) return false;
            int top = y - tile.frameY / 18 % 2;
            for (int dy = 0; dy < 2; dy++)
            {
                if (!Loaded(x, top + dy)) return false;
                Tile part = Main.tile[x, top + dy];
                if (part == null || !part.active() || part.type != 698 || part.frameY != dy * 18 || part.frameX != tile.frameX) return false;
            }
            TileEntity entity;
            if (!TileEntity.ByPosition.TryGetValue(new Point16(x, top), out entity) || entity.Position.X != x || entity.Position.Y != top) return false;
            var jar = entity as TEDeadCellsDisplayJar;
            // Native DrawMultiTileVinesInWind draws this entity's glow and
            // blends its item with white independently of ambient lighting.
            // Existence alone does not bypass the caller's separate echo gate.
            return jar != null && jar.item != null;
        }
        private int[] ReadFacts()
        {
            var values = new List<int>();
            var frame = entity as TEItemFrame; var rack = entity as TEWeaponsRack; var jar = entity as TEDeadCellsDisplayJar; var platter = entity as TEFoodPlatter;
            bool valid;
            if (type == 395 && frame != null) valid = Add(values, frame.item);
            else if (type == 471 && rack != null) valid = Add(values, rack.item);
            else if (type == 698 && jar != null) valid = Add(values, jar.item);
            else if (type == 520 && platter != null) valid = Add(values, platter.item);
            else if (type == 470 && entity is TEDisplayDoll) valid = Add(values, dollEquip, 9) && Add(values, dollDyes, 9) && Add(values, dollMisc, 1);
            else if (type == 475 && entity is TEHatRack) valid = Add(values, hats, 2) && Add(values, hatDyes, 2);
            else valid = false;
            return valid ? values.ToArray() : null;
        }
        private bool Add(List<int> values, FieldInfo field, int count)
        {
            var items = field?.GetValue(entity) as Item[];
            if (items == null || items.Length != count) return false;
            foreach (var item in items) if (!Add(values, item)) return false;
            return true;
        }
        private static bool Add(List<int> values, Item item)
        {
            if (item == null || item.type < 0 || item.type >= Terraria.ID.ItemID.Count || item.stack < 0) return false;
            values.Add(item.type); values.Add(item.stack); return true;
        }
        internal bool StillMatches()
        {
            if (!CompleteBody()) return false;
            TileEntity current; TileEntity.ByPosition.TryGetValue(new Point16(x, y), out current);
            if (!ReferenceEquals(current, entity)) return false;
            var now = entity == null ? null : ReadFacts();
            if (now == null || facts == null) return now == null && facts == null;
            if (now.Length != facts.Length) return false;
            for (int i = 0; i < now.Length; i++) if (now[i] != facts[i]) return false;
            return true;
        }
        internal string Describe(string body)
        {
            if (!Known) return body + "（内容尚未确认）";
            if (ItemType == 0) return body;
            var entries = new List<string>(); bool omitted = false;
            for (int i = 0; i < facts.Length; i += 2)
            {
                if (facts[i] <= 0 || facts[i + 1] <= 0) continue;
                string next = facts[i + 1] + " 个 " + NativeTargetObservation.ItemName(facts[i]);
                // Whole slot facts remain intact inside a bounded carrier entry;
                // final chat framing and other world layers have their own budget.
                if (SafeChatText.EncodedEntryByteCount("放在" + body + "的" + string.Join("和", entries) + "和" + next) > 760) { omitted = true; break; }
                entries.Add(next);
            }
            return "放在" + body + "的" + string.Join("和", entries) + (omitted ? "（部分内容已省略）" : "");
        }
    }
}
