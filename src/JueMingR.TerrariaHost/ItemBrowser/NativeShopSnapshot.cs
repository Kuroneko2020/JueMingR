using System;
using System.Collections.Generic;
using JueMingR.Features.ItemBrowser;
using JueMingR.Platform.ItemCatalog;
using Terraria;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // Explicit request, one temporary shop per update. No purchase, no
    // SetupTravelShop, no claim that a multi-frame capture is one atomic moment.
    internal sealed class NativeShopSnapshot
    {
        private readonly List<ItemRelation> values = new List<ItemRelation>();
        private int next;
        private int failures;
        private string captureTime;
        private object player, world;
        internal RelationIndex Index { get; private set; }
        internal string Status { get; private set; } = "商店尚未读取；点击刷新获取本次条件下的商品";
        internal long Revision { get; private set; }
        internal bool Busy { get { return next > 0; } }
#if DEBUG
        internal long ShopReads;
#endif
        internal void Request()
        {
            player = Main.LocalPlayer; world = Main.ActiveWorldFileData; next = 1; failures = 0; captureTime = DateTime.Now.ToString("HH:mm:ss"); values.Clear(); Index = null; Revision++;
            Status = "正在分批读取商店；结果为各店读取时的条件快照";
        }
        internal void Clear()
        { next = 0; values.Clear(); Index = null; Revision++; Status = "世界或角色已变化，请重新读取商店"; }
        internal void Step(bool requested)
        {
            if ((Busy || Index != null) && (!ReferenceEquals(player, Main.LocalPlayer) || !ReferenceEquals(world, Main.ActiveWorldFileData))) Clear();
            if (!requested || !Busy) return;
            try
            {
                var shop = Chest.CreateShop(); shop.SetupShop(next);
#if DEBUG
                ShopReads++;
#endif
                for (int i = 0; i < shop.maxItems && i < shop.item.Length; i++)
                {
                    Item value = shop.item[i]; if (value == null || value.IsAir) continue;
                    values.Add(new ItemRelation("shop:" + next + ":" + i, RelationKind.Shop, value.type, 1, 1,
                        ShopName(next), "本次条件快照（" + captureTime + " 起分批读取）；库存与价格可能变化；不表示 NPC 当前在场", null));
                }
            }
            catch { failures++; }
            if (++next > 25) { next = 0; Index = new RelationIndex(values); Revision++; Status = captureTime + " 起的 25 个商店页分批快照" + (failures == 0 ? "已读取" : "；其中 " + failures + " 页读取失败") + "；条件变化请刷新"; }
        }
        private static string ShopName(int shop)
        {
            string[] names = { "", "商人", "军火商", "树妖", "爆破专家", "服装商", "哥布林工匠", "巫师", "机械师", "圣诞老人", "松露人", "蒸汽朋克人", "染料商", "派对女孩", "机器侠", "画家", "巫医", "海盗", "发型师", "旅商", "骷髅商人", "酒馆老板", "高尔夫球手", "动物学家", "公主", "画家（装饰）" };
            return names[shop];
        }
    }
}
