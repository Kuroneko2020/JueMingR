using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JueMingR.Features.ItemBrowser;
using JueMingR.Platform.ItemCatalog;
using Terraria;
using Terraria.ID;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // Independent declaration families: one failed family cannot erase recipes
    // or another source. No Report method here attempts a drop or tests RNG.
    internal sealed class NativeRelationSources
    {
        private readonly List<ItemRelation> drops = new List<ItemRelation>(), shimmer = new List<ItemRelation>();
        private int npc = -66, item = 1;
        private long catalogRevision = -1;
        private bool dropsDone, shimmerDone;
        internal RelationIndex Drops { get; private set; }
        internal RelationIndex Shimmer { get; private set; }
        internal readonly RelationIndex Curated = new RelationIndex(CuratedItemSources.All());
        internal string DropStatus { get; private set; } = "NPC 来源尚未准备";
        internal string ShimmerStatus { get; private set; } = "微光来源尚未准备";
        internal long Revision { get; private set; }
#if DEBUG
        internal long NpcReads, GlobalReads, ShimmerReads;
#endif
        internal void Step(bool requested, NativeItemCatalog catalog)
        {
            if (!requested || !catalog.Ready) return;
            if (catalogRevision != catalog.Revision)
            {
                catalogRevision = catalog.Revision; npc = -66; item = 1; dropsDone = shimmerDone = false;
                drops.Clear(); shimmer.Clear(); Drops = Shimmer = null; Revision++;
            }
            if (!dropsDone)
            {
                try
                {
                    if (Main.ItemDropsDB == null) { DropStatus = "等待原版掉落声明就绪"; return; }
                    bool global = npc == -66;
                    // int.MinValue is outside the fixed native NPC identity domain;
                    // its query contains only the global declaration list.
                    List<IItemDropRule> rules = Main.ItemDropsDB.GetRulesForNPCID(global ? int.MinValue : npc, global);
                    CaptureDrops(npc, global ? "全局掉落" : Lang.GetNPCNameValue(npc), rules);
#if DEBUG
                    if (global) GlobalReads++; else NpcReads++;
#endif
                    npc++; DropStatus = "准备 NPC 掉落 " + (npc + 65) + "/" + (NPCID.Count + 65);
                    if (npc >= NPCID.Count) { Drops = new RelationIndex(drops); dropsDone = true; DropStatus = "NPC / 全局声明已就绪；概率为基础报告值"; Revision++; }
                }
                catch { dropsDone = true; DropStatus = "NPC 掉落资料读取失败；配方与其他来源仍可使用"; Revision++; }
                return;
            }
            if (shimmerDone) return;
            try
            {
                int end = Math.Min(ItemID.Count, item + 32);
                for (; item < end; item++)
                {
                    Item sample = ContentSamples.ItemsByType[item]; if (sample.type != item || sample.IsAir) continue;
                    CaptureShimmer(sample);
#if DEBUG
                    ShimmerReads++;
#endif
                }
                ShimmerStatus = "准备微光关系 " + item + "/" + ItemID.Count;
                if (item == ItemID.Count) { Shimmer = new RelationIndex(shimmer); shimmerDone = true; ShimmerStatus = "直接转换、别名与当前世界解构；特殊非物品效果未穷尽"; Revision++; }
            }
            catch { shimmerDone = true; ShimmerStatus = "微光资料读取失败；其他来源仍可使用"; Revision++; }
        }
        private void CaptureDrops(int source, string name, List<IItemDropRule> rules)
        {
            for (int r = 0; r < rules.Count; r++)
            {
                var reports = new List<DropRateInfo>(); rules[r].ReportDroprates(reports, new DropRateInfoChainFeed(1));
                for (int i = 0; i < reports.Count; i++)
                {
                    DropRateInfo report = reports[i]; if (report.itemId <= 0 || report.itemId >= ItemID.Count) continue;
                    var conditions = new List<string> { "基础概率 " + report.dropRate.ToString("P2", CultureInfo.CurrentCulture) + "（幸运与当前状态未计算）" };
                    if (report.conditions != null) foreach (var condition in report.conditions)
                    { string text = condition.GetConditionDescription(); conditions.Add(string.IsNullOrWhiteSpace(text) ? "另有原版条件，未提供文字说明" : text); }
                    drops.Add(new ItemRelation("npc:" + source + ":" + r + ":" + i, RelationKind.NpcDrop, report.itemId, report.stackMin, report.stackMax,
                        name, string.Join("；", conditions), null));
                }
            }
        }
        private void CaptureShimmer(Item sample)
        {
            int equivalent = sample.GetShimmerEquivalentType();
            // Native money/luck, town-slime and NPC release branches precede
            // decrafting. A registered crafting recipe is not sufficient.
            if (ItemID.Sets.CommonCoin[equivalent]) return;
            string locked = ItemID.Sets.ShimmerPostMoonlord[equivalent] ? "需击败月亮领主；" : "";
            if (equivalent == 3461)
            {
                int[] targets = { 5408, 5401, 5403, 5402, 5406, 5407, 5405, 5404 };
                string[] phases = { "满月", "亏凸月", "下弦月", "残月", "新月", "蛾眉月", "上弦月", "盈凸月" };
                for (int i = 0; i < targets.Length; i++) AddShimmer(sample.type, targets[i], 1, 1, 1, "月相：" + phases[i]);
                return;
            }
            int target = ShimmerTransforms.GetTransformToItem(equivalent);
            if (target > 0) { AddShimmer(sample.type, target, 1, 1, 1, locked + "投入微光"); return; }
            if (sample.type == 4986 || sample.type == 560 || sample.makeNPC > 0) return;
            int index = ShimmerTransforms.GetDecraftingRecipeIndex(sample.GetShimmerEquivalentType(true));
            if (index < 0 || index >= Recipe.numRecipes) return;
            Recipe recipe = Main.recipe[index]; if (recipe.notDecraftable) return;
            if (ShimmerTransforms.RecipeSets.PostSkeletron[index]) locked += "需击败骷髅王；";
            if (ShimmerTransforms.RecipeSets.PostGolem[index]) locked += "需击败石巨人；";
            locked += "原版在当前世界选定的解构配方";
            if (recipe.alchemy) locked += "；炼药减耗随机，显示可能范围";
            if (recipe.customShimmerResults != null)
            {
                foreach (Item result in recipe.customShimmerResults)
                    if (!result.IsAir) AddShimmer(sample.type, result.type, recipe.createItem.stack, recipe.alchemy ? 0 : result.stack, result.stack, locked);
            }
            else foreach (var entry in recipe.requiredItemQuickLookup)
            {
                if (entry.stack <= 0) break;
                AddShimmer(sample.type, entry.IsRecipeGroup ? entry.RecipeGroup.DecraftItemId : entry.itemIdOrRecipeGroup, recipe.createItem.stack,
                    recipe.alchemy ? 0 : entry.stack, entry.stack, locked);
            }
        }
        private void AddShimmer(int from, int to, int inputCount, int minimum, int maximum, string condition)
        { shimmer.Add(new ItemRelation("shimmer:" + from + ":" + shimmer.Count, RelationKind.Shimmer, to, minimum, maximum, "微光", condition, new[] { new RelationIngredient(new[] { from }, inputCount) })); }
        internal IReadOnlyList<ItemRelation> Find(int type, bool uses, int kind, NativeItemCatalog catalog, NativeShopSnapshot shops)
        {
            var result = new List<ItemRelation>();
            foreach (RelationIndex index in new[] { catalog.Recipes, Drops, Shimmer, Curated, shops.Index })
                if (index != null) result.AddRange(index.Find(type, uses, kind));
            return result.AsReadOnly();
        }
    }
}
