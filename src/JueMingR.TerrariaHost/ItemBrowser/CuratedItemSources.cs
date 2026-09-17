using System.Collections.Generic;
using JueMingR.Platform.ItemCatalog;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // Versioned 1.4.5.8 declarations independently checked against Player,
    // ExtractinatorHelper and WorldGen. See the design's coverage/evidence table.
    // This finite supplement deliberately makes no complete loot-table claim.
    internal static class CuratedItemSources
    {
        internal static IEnumerable<ItemRelation> All()
        {
            yield return From(3318, 3090, "史莱姆王宝藏袋", "必出");
            yield return From(3318, 998, "史莱姆王宝藏袋", "必出");
            yield return From(3318, 1309, "史莱姆王宝藏袋", "1/30");
            yield return From(3319, 3097, "克苏鲁之眼宝藏袋", "必出");
            yield return From(3320, 3224, "世界吞噬怪宝藏袋", "必出");
            yield return From(3324, 3335, "血肉墙宝藏袋", "尚未永久增加饰品槽时必出");
            yield return From(2336, 989, "金匣", "独立概率 1/30");
            yield return From(3981, 989, "钛金匣", "独立概率 1/15");
            yield return From(3205, 3085, "地牢匣", "必出；先开匣获得锁盒");
            yield return From(3984, 3085, "围栏匣", "必出；先开匣获得锁盒");
            yield return From(3085, 155, "锁盒", "1/7；消耗一把金钥匙");
            yield return From(3085, 156, "锁盒", "1/7；消耗一把金钥匙");
            yield return From(4879, 274, "黑曜石锁盒", "1/5；持有暗影钥匙，不消耗");
            yield return From(3206, 4978, "天空匣", "独立概率 1/40");
            yield return From(3985, 4978, "天蓝匣", "独立概率 1/40");
            yield return From(3347, 3380, "提炼", "沙漠化石；提炼机或叶绿提炼机；1/10", RelationKind.World, 1, 7);
            yield return From(424, 999, "提炼", "泥沙；提炼机或叶绿提炼机；约 0.96%", RelationKind.World, 1, 16);
            yield return From(1103, 999, "提炼", "雪泥；提炼机或叶绿提炼机；约 0.96%", RelationKind.World, 1, 16);
            yield return From(0, 3347, "世界采集", "成功采掘沙漠化石块；不表示当前世界已发现", RelationKind.World);
            yield return From(0, 1103, "世界采集", "成功采掘雪泥块；不表示当前世界已发现", RelationKind.World);
            yield return From(0, 424, "世界采集", "成功采掘泥沙块；不表示当前世界已发现", RelationKind.World);
            int[] milestones = { 5, 10, 15, 20, 25, 30 }, rewards = { 2428, 2367, 2368, 2369, 3031, 2294 };
            for (int i = 0; i < milestones.Length; i++) yield return From(0, rewards[i], "渔夫奖励", "完成第 " + milestones[i] + " 次任务的固定奖励", RelationKind.Fishing);
        }
        private static ItemRelation From(int source, int output, string title, string condition, RelationKind kind = RelationKind.Container, int minimum = 1, int maximum = 1)
        { return new ItemRelation("curated:" + source + ":" + output, kind, output, minimum, maximum, title, condition + "；1.4.5.8 精选资料", source == 0 ? null : new[] { new RelationIngredient(new[] { source }, 1) }); }
    }
}
