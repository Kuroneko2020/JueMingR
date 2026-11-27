using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using JueMingR.Features.ItemBrowser;
using JueMingR.Platform.ItemCatalog;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal static class NativeKnowledgeQuery
    {
        internal static bool IsPurpose(string purpose) => purpose == null || new[] { "heal_life", "heal_mana", "buff", "light",
            "bait", "fishing_rod", "mine", "chop", "hammer", "flight", "grapple", "weapon", "armor", "place",
            "teleport", "coast_teleport", "home_teleport", "world_spawn_teleport" }.Contains(purpose, StringComparer.Ordinal);

        internal static bool Matches(Item item, string purpose, int power, string teleport)
        {
            switch (purpose)
            {
                case null: return true;
                case "heal_life": return item.healLife > 0;
                case "heal_mana": return item.healMana > 0;
                case "buff": return item.buffType > 0 && item.buffTime > 0;
                case "light": return item.createTile == TileID.Torches || item.type == ItemID.Glowstick || item.type == ItemID.StickyGlowstick;
                case "bait": return item.bait >= Math.Max(1, power);
                case "fishing_rod": return item.fishingPole >= Math.Max(1, power);
                case "mine": return item.pick >= Math.Max(1, power);
                case "chop": return item.axe > 0 && item.axe * 5 >= power;
                case "hammer": return item.hammer >= Math.Max(1, power);
                case "flight": return item.wingSlot > 0;
                case "grapple": return item.shoot > 0 && Main.projHook[item.shoot];
                case "weapon": return item.damage > 0;
                case "armor": return item.headSlot >= 0 || item.bodySlot >= 0 || item.legSlot >= 0;
                case "place": return item.createTile >= 0 || item.createWall > 0;
                case "teleport": return teleport != null || item.type == ItemID.WormholePotion;
                case "coast_teleport": return item.type == ItemID.MagicConch || item.type == ItemID.ShellphoneOcean;
                case "home_teleport": return item.type == ItemID.MagicMirror || item.type == ItemID.IceMirror ||
                    item.type == ItemID.CellPhone || item.type == ItemID.Shellphone || item.type == ItemID.RecallPotion ||
                    item.type == ItemID.PotionOfReturn;
                case "world_spawn_teleport": return item.type == ItemID.ShellphoneSpawn;
                default: return false;
            }
        }

        internal static bool NameMatches(Item item, string filter)
        {
            if (String.IsNullOrWhiteSpace(filter)) return true;
            int id = BrowserCatalog.ClassifyQuery(filter);
            if (id != 0) return id == item.type;
            return item.Name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                StableNameMatches(ItemID.Search.GetName(item.type), filter) ||
                NativeItemAliases.Matches(Lang.GetItemName(item.type).Key, filter);
        }

        private static bool StableNameMatches(string stable, string filter)
        {
            if (String.IsNullOrEmpty(stable)) return false;
            int j = 0;
            for (int i = 0; i < stable.Length; i++)
            {
                if (Char.ToUpperInvariant(stable[i]) != Char.ToUpperInvariant(filter[j])) continue;
                do { j++; } while (j < filter.Length && (Char.IsWhiteSpace(filter[j]) || filter[j] == '-' || filter[j] == '_'));
                if (j == filter.Length) return true;
                // Names can match a contiguous substring of the stable
                // name with spaces removed, never arbitrary subsequences.
                int k = i + 1, next = j;
                while (k < stable.Length && next < filter.Length &&
                    Char.ToUpperInvariant(stable[k]) == Char.ToUpperInvariant(filter[next]))
                {
                    k++;
                    do { next++; } while (next < filter.Length &&
                        (Char.IsWhiteSpace(filter[next]) || filter[next] == '-' || filter[next] == '_'));
                }
                if (next == filter.Length) return true;
                j = 0;
            }
            return false;
        }

        private static string Matter(BrowserCatalog catalog, int type)
        {
            return (catalog.Find(type)?.Name ?? Lang.GetItemNameValue(type)) + " (#" + type + ")";
        }

        internal static string Items(HostItemKnowledge host, string filter, string purpose, int power, int offset,
            int revision, Func<int, string> teleport)
        {
            bool aliasPending = NativeItemAliases.IsPending(filter);
            host.Native.ObserveContext();
            if (!host.Native.CatalogCurrent)
            {
                if (!host.Native.CaptureUnavailable) host.RequestChat(HostItemKnowledge.ChatDemand.Items);
                return (host.Native.CaptureUnavailable ? "failed" : "pending") + " · " + host.Native.Status;
            }
            if (offset > 0 && revision >= 0 && revision != host.Native.Revision)
                return "stale · 目录更新，使用 offset=0 重新查询。";
            // Category searches use the shared frozen identities. Native
            // attributes are consulted once for a filter, and pages reuse
            // that result until the catalog or alias readiness changes.
            var matches = host.MatchChatItems(filter, purpose, power, aliasPending, item =>
                ContentSamples.ItemsByType.TryGetValue(item.Type, out var sample) &&
                (NameMatches(sample, filter) || item.Name.IndexOf(filter ?? "", StringComparison.CurrentCultureIgnoreCase) >= 0) &&
                Matches(sample, purpose, power, teleport(item.Type)));
            const int count = 6;
            return (aliasPending ? "partial · 正在读取内置中英文名称。匹配 " : "ready · 匹配 ") + matches.Count +
                " 项，目录修订 revision=" + host.Native.Revision + ".\n" +
                String.Join("\n", matches.Skip(offset).Take(count).Select(item =>
                {
                    string destination = teleport(item.Type);
                    string firstLine = item.Description.Split('\n').FirstOrDefault() ?? "";
                    return item.Name + " (#" + item.Type + ", " + item.InternalName + ") " +
                        (destination == null ? firstLine : "传送目的地：" + destination);
                })) + NextPage(offset, count, matches.Count);
        }

        internal static string Sources(HostItemKnowledge host, int type, bool uses, string kind, int offset, int revision)
        {
            host.Native.ObserveContext();
            if (!host.Native.CatalogCurrent)
            {
                if (!host.Native.CaptureUnavailable) host.RequestChat(HostItemKnowledge.ChatDemand.Recipes);
                return (host.Native.CaptureUnavailable ? "failed" : "pending") + " · " + host.Native.Status;
            }
            var catalog = host.Native.Catalog;
            if (offset > 0 && revision >= 0 && revision != host.Revision)
                return "stale · 来源目录更新，使用 offset=0 重新查询。";
            if (catalog.Find(type) == null) return "ready · 当前目录没有该物品类型。";
            bool recipe = kind == null || kind == "recipe", drop = kind == null || kind == "drop";
            bool shop = kind == null || kind == "shop", shimmer = kind == null || kind == "shimmer";
            bool isReady = host.Native.Ready;
            bool currentSources = host.Sources.IsCurrent(host.Native);
            bool recipeReady = !recipe || isReady;
            bool dropReady = !drop || currentSources && host.Sources.Drops != null;
            bool shimmerReady = !shimmer || currentSources && host.Sources.Shimmer != null;
            bool shopReady = !shop || host.Shops.ChatCurrent;
            if (!recipeReady) host.RequestChat(HostItemKnowledge.ChatDemand.Recipes);
            if ((!dropReady || !shimmerReady) && (!currentSources || !host.Sources.Done)) host.RequestChat(HostItemKnowledge.ChatDemand.Sources);
            if (!shopReady) host.RequestChat(HostItemKnowledge.ChatDemand.Shops);
            var rows = new List<ItemRelation>();
            if (recipe && recipeReady) rows.AddRange(host.Native.Recipes.Find(type, uses));
            if (drop && dropReady) rows.AddRange(host.Sources.Drops.Find(type, uses, (int)RelationKind.NpcDrop));
            if (shimmer && shimmerReady) rows.AddRange(host.Sources.Shimmer.Find(type, uses));
            if (shop && shopReady) rows.AddRange(host.Shops.Index.Find(type, uses));
            if (kind == null || kind == "other") rows.AddRange(host.Sources.Curated.Find(type, uses));
            if (kind == "container") rows.AddRange(host.Sources.Curated.Find(type, uses, (int)RelationKind.Container));
            if (kind == "fishing") rows.AddRange(host.Sources.Curated.Find(type, uses, (int)RelationKind.Fishing));
            if (kind == "world") rows.AddRange(host.Sources.Curated.Find(type, uses, (int)RelationKind.World));
            bool incomplete = (drop || shimmer) && currentSources && host.Sources.Done && (!dropReady || !shimmerReady) ||
                shop && shopReady && host.Shops.Partial;
            bool ready = recipeReady && dropReady && shimmerReady && shopReady && !incomplete;
            var statuses = new List<string>();
            if (!recipeReady) statuses.Add(host.Native.Status);
            if (!dropReady) statuses.Add(host.Sources.DropStatus);
            if (!shimmerReady) statuses.Add(host.Sources.ShimmerStatus);
            if (!shopReady) statuses.Add(host.Shops.Status);
            if (shop && shopReady) statuses.Add("商店为当前本机玩家条件下的分批样本，队友实际购买资格需另查。");
            const int count = 5;
            return (ready ? "ready" : rows.Count > 0 || incomplete ? "partial" : "pending") + " · " + Matter(catalog, type) +
                " · " + rows.Count + " 条已读关系，目录修订 revision=" + host.Revision + ". " + String.Join("；", statuses) + "\n" +
                String.Join("\n", rows.Skip(offset).Take(count).Select(row => FormatRelation(catalog, row))) + NextPage(offset, count, rows.Count);
        }

        private static string FormatRelation(BrowserCatalog catalog, ItemRelation row)
        {
            var text = new StringBuilder(row.Id).Append(" · ").Append(row.Title).Append(" · ").Append(Matter(catalog, row.Output))
                .Append(" ×").Append(row.Minimum);
            if (row.Maximum != row.Minimum) text.Append("–").Append(row.Maximum);
            if (row.StationName.Length > 0) text.Append(" · 站点 ").Append(row.StationName);
            if (row.Station > 0) text.Append(" (#").Append(row.Station).Append(')');
            foreach (var ingredient in row.Ingredients)
            {
                text.Append(" · 原料 ×").Append(ingredient.Count).Append(' ');
                if (ingredient.Label.Length > 0) text.Append(ingredient.Label).Append(' ');
                text.Append(String.Join("/", ingredient.Types.Take(8).Select(type => Matter(catalog, type))));
                if (ingredient.Types.Count > 8) text.Append("（同组共 ").Append(ingredient.Types.Count).Append(" 种）");
            }
            if (row.Conditions.Length != 0) text.Append(" · ").Append(row.Conditions);
            return text.ToString();
        }

        private static string NextPage(int offset, int count, int total) =>
            offset + count < total ? "\n下一页 offset=" + (offset + count).ToString(CultureInfo.InvariantCulture) : "";
    }
}
