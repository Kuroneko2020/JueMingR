using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.ItemBrowser;
using JueMingR.Platform.ItemCatalog;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // Main-thread demand-driven capture. Draw only consumes published frozen data.
    // A language event includes same-culture resource hot reloads.
    internal sealed class NativeItemCatalog : IDisposable
    {
        private readonly List<CatalogItem> items = new List<CatalogItem>();
        private readonly List<ItemRelation> recipes = new List<ItemRelation>();
        private readonly Dictionary<long, int> stations = new Dictionary<long, int>();
        private readonly Dictionary<int, int> walls = new Dictionary<int, int>();
        private readonly NativePlacementNames placementNames = new NativePlacementNames();
        private int itemCursor = 1, placementCursor = 1, recipeCursor, failures;
        internal int ExcludedIdentities { get; private set; }
        private bool dirty;
        private object world;
        private bool crimson, remix, worthy, zenith, drunk, skyblock, mechdusa;
        internal BrowserCatalog Catalog { get; private set; }
        internal RelationIndex Recipes { get; private set; }
        internal IReadOnlyList<ItemRelation> RecipeValues { get { return recipes.AsReadOnly(); } }
        internal long Revision { get; private set; }
        internal string Status { get; private set; } = "打开查询页后准备资料";
        internal bool Ready { get { return Catalog != null && Recipes != null && !dirty; } }
        internal bool PlacementReady { get { return !dirty && (placementCursor >= ItemID.Count || itemCursor >= ItemID.Count); } }
#if DEBUG
        internal long ItemReads, RecipeReads, PlacementReads;
        internal Exception CaptureFailure;
#endif
        internal NativeItemCatalog() { LanguageManager.Instance.OnLanguageChanged += LanguageChanged; }
        private void LanguageChanged(LanguageManager manager) { dirty = true; }
        internal void ObserveContext()
        {
            if (!ReferenceEquals(world, Main.ActiveWorldFileData) || crimson != WorldGen.crimson || remix != Main.remixWorld ||
                worthy != Main.getGoodWorld || zenith != Main.zenithWorld || drunk != Main.drunkWorld || skyblock != Main.skyblockWorld || mechdusa != SpecialSeedFeatures.Mechdusa)
            {
                world = Main.ActiveWorldFileData; crimson = WorldGen.crimson; remix = Main.remixWorld;
                worthy = Main.getGoodWorld; zenith = Main.zenithWorld; drunk = Main.drunkWorld; dirty = true;
                skyblock = Main.skyblockWorld; mechdusa = SpecialSeedFeatures.Mechdusa;
            }
        }
        internal void Step(bool requested)
        {
            ObserveContext(); if (!requested) return;
            if (dirty) Reset();
            if (Ready || failures >= 3) return;
            // These tables finish after the initial sample dictionary exists.
            if (ContentSamples.ItemsByType.Count < ItemID.Count || Recipe.numRecipes <= 0 || Main.recipe == null ||
                Main.recipe[0] == null || Main.recipe[0].requiredItemQuickLookup[0].stack <= 0)
            { Status = "等待原版物品与配方资料就绪"; return; }
            try
            {
                if (itemCursor < ItemID.Count)
                {
                    int end = Math.Min(ItemID.Count, itemCursor + 64);
                    for (; itemCursor < end; itemCursor++)
                    {
                        Item sample = ContentSamples.ItemsByType[itemCursor];
                        // Native deprecated IDs become air or a different type
                        // (e.g. 226 -> 227); show each current canonical type once.
                        if (sample.type <= 0 || sample.type != itemCursor) { ExcludedIdentities++; continue; }
                        sample = sample.Clone(); sample.Refresh(); // Own display sample; never mutate ContentSamples.
                        items.Add(CaptureItem(sample));
                        CapturePlacement(sample);
#if DEBUG
                        ItemReads++;
#endif
                    }
                    Status = "准备物品 " + (itemCursor - 1) + "/" + (ItemID.Count - 1);
                    if (itemCursor == ItemID.Count) { Catalog = new BrowserCatalog(items.ToArray()); Revision++; }
                    return;
                }
                int last = Math.Min(Recipe.numRecipes, recipeCursor + 32);
                for (; recipeCursor < last; recipeCursor++)
                {
                    ItemRelation relation = CaptureRecipe(recipeCursor);
                    if (relation != null) recipes.Add(relation);
#if DEBUG
                    RecipeReads++;
#endif
                }
                Status = "准备配方 " + recipeCursor + "/" + Recipe.numRecipes;
                if (recipeCursor == Recipe.numRecipes) { Recipes = new RelationIndex(recipes); Revision++; Status = "目录与普通配方已就绪"; }
            }
            catch (Exception error)
            {
#if DEBUG
                CaptureFailure = error;
#else
                _ = error;
#endif
                failures++; Catalog = null; Recipes = null; items.Clear(); recipes.Clear(); stations.Clear(); walls.Clear(); placementNames.Clear();
                itemCursor = placementCursor = 1; recipeCursor = ExcludedIdentities = 0;
                Status = "原版资料读取未完成（" + failures + "/3）";
            }
        }
        private void Reset()
        { dirty = false; itemCursor = placementCursor = 1; recipeCursor = failures = ExcludedIdentities = 0; items.Clear(); recipes.Clear(); stations.Clear(); walls.Clear(); placementNames.Clear(); Catalog = null; Recipes = null; Revision++; }
        // A cold world gesture needs only canonical placement metadata. It does
        // not materialize descriptions, recipes, drops, shops, or the directory.
        // The gesture owns the demand and cancellation; no background warm-up.
        internal void StepPlacement()
        {
            ObserveContext(); if (dirty) Reset();
            if (PlacementReady || ContentSamples.ItemsByType.Count < ItemID.Count) return;
            int end = Math.Min(ItemID.Count, placementCursor + 64);
            for (; placementCursor < end; placementCursor++)
            {
                Item sample = ContentSamples.ItemsByType[placementCursor];
                if (sample.type == placementCursor) CapturePlacement(sample);
#if DEBUG
                PlacementReads++;
#endif
            }
        }
        private void CapturePlacement(Item sample)
        {
            placementNames.Capture(sample);
            if (sample.createTile >= 0)
            {
                long key = ((long)sample.createTile << 32) | (uint)sample.placeStyle;
                int old; stations[key] = stations.TryGetValue(key, out old) && old != sample.type ? -1 : sample.type;
            }
            if (sample.createWall > 0) { int old; walls[sample.createWall] = walls.TryGetValue(sample.createWall, out old) && old != sample.type ? -1 : sample.type; }
        }
        internal int WallItem(int wall) { int type; return PlacementReady && walls.TryGetValue(wall, out type) && type > 0 ? type : 0; }
        internal int PlacedItem(Tile tile)
        {
            return PlacementReady ? placementNames.Find(tile) : 0;
        }
        internal static CatalogItem CaptureItem(Item item)
        {
            int flags = 0;
            if (item.damage > 0) flags |= (int)ItemCategory.Weapon;
            if (item.pick > 0 || item.axe > 0 || item.hammer > 0) flags |= (int)ItemCategory.Tool;
            if (item.accessory || item.headSlot >= 0 || item.bodySlot >= 0 || item.legSlot >= 0) flags |= (int)ItemCategory.Equipment;
            if (item.consumable) flags |= (int)ItemCategory.Consumable;
            if (item.material) flags |= (int)ItemCategory.Material;
            if (item.createTile >= 0 || item.createWall > 0) flags |= (int)ItemCategory.Placeable;
            var text = new List<string>();
            if (item.damage > 0) text.Add("基础伤害 " + item.damage);
            if (item.defense > 0) text.Add("基础防御 " + item.defense);
            if (item.pick > 0) text.Add("镐力 " + item.pick + "%");
            if (item.axe > 0) text.Add("斧力 " + item.axe * 5 + "%");
            if (item.hammer > 0) text.Add("锤力 " + item.hammer + "%");
            if (item.healLife > 0) text.Add("恢复 " + item.healLife + " 生命");
            if (item.healMana > 0) text.Add("恢复 " + item.healMana + " 魔力");
            if (item.mana > 0) text.Add("基础魔力消耗 " + item.mana);
            if (item.buffTime > 0) text.Add("基础效果时间 " + (item.buffTime / 60f).ToString("0.##") + " 秒");
            if (item.damage > 0 && item.crit > 0) text.Add("基础暴击率 " + item.crit + "%（未计装备）");
            if (item.damage > 0 && item.useTime > 0) text.Add("基础使用间隔 " + item.useTime + " 个游戏更新（未计装备）");
            if (item.maxStack > 1) text.Add("最大堆叠 " + item.maxStack);
            if (item.ToolTip != null) for (int line = 0; line < item.ToolTip.Lines; line++) text.Add(item.ToolTip.GetLine(line));
            return new CatalogItem(item.type, Lang.GetItemNameValue(item.type), ItemID.Search.GetName(item.type), flags, item.value, string.Join("\n", text));
        }
        internal ItemRelation CaptureRecipe(int id)
        {
            Recipe recipe = Main.recipe[id];
            if (recipe == null || recipe.createItem == null || recipe.createItem.type <= 0 || recipe.createItem.stack <= 0) return null;
            var ingredients = new List<RelationIngredient>();
            foreach (var entry in recipe.requiredItemQuickLookup)
            {
                if (entry.stack <= 0) break;
                ingredients.Add(entry.IsRecipeGroup ? new RelationIngredient(entry.RecipeGroup.ValidItems.OrderBy(i => i).ToArray(), entry.stack, entry.RecipeGroup.GetText()) :
                    new RelationIngredient(new[] { entry.itemIdOrRecipeGroup }, entry.stack));
            }
            int station = 0; string stationName = "徒手";
            if (recipe.requiredTile >= 0)
            {
                stationName = Recipe.GetRequiredTileName(recipe.requiredTile);
                long key = ((long)recipe.requiredTile << 32) | (uint)Recipe.GetRequiredTileStyle(recipe.requiredTile);
                int mapped; if (stations.TryGetValue(key, out mapped) && mapped > 0) station = mapped;
            }
            var conditions = new List<string>();
            if (recipe.needWater) conditions.Add("靠近水"); if (recipe.needHoney) conditions.Add("靠近蜂蜜"); if (recipe.needLava) conditions.Add("靠近熔岩");
            if (recipe.needSnowBiome) conditions.Add("雪原"); if (recipe.needGraveyardBiome) conditions.Add("墓地");
            if (recipe.needMechdusa) conditions.Add("特殊世界的机械美杜莎配方"); if (recipe.needTorchGodsFavor) conditions.Add("火把神的恩赐");
            if (recipe.alchemy) conditions.Add("炼药台可能节省材料；此处为原始需求量");
            return new ItemRelation("recipe:" + id, RelationKind.Recipe, recipe.createItem.type, recipe.createItem.stack, recipe.createItem.stack,
                "制作", string.Join("；", conditions), ingredients.ToArray(), station, stationName);
        }
        public void Dispose() { LanguageManager.Instance.OnLanguageChanged -= LanguageChanged; }
    }
}
