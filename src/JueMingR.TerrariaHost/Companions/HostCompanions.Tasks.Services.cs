using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Recovery;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private sealed class ServiceVisit
        {
            internal NPC Npc;
            internal int Slot, Generation;
            internal ArrivalSearch Arrival;
            internal Item Item;
            internal int ItemSlot, Count, QuestType;
            internal long Spent;
            internal uint Next;
        }

        private static readonly AccessTools.FieldRef<NPCInteractions.Actions.OpenShop, int> ServiceShopNpc =
            AccessTools.FieldRefAccess<NPCInteractions.Actions.OpenShop, int>("_npcType");
        private static readonly AccessTools.FieldRef<NPCInteractions.Actions.OpenShop, int> ServiceShopIndex =
            AccessTools.FieldRefAccess<NPCInteractions.Actions.OpenShop, int>("_shopIndex");

        partial void RegisterServiceTaskOperations()
        {
            var npc = new TaskField("npc", min: 1, max: 64);
            var item = new TaskField("item_type", "integer", true, 1, ItemID.Count - 1);
            var slot = new TaskField("slot", "integer", min: 0, max: CompanionBehavior.MainInventorySlots - 1);
            var budget = new TaskField("budget_copper", "integer", true, 0, Int32.MaxValue);
            var amount = new TaskField("amount", "integer", true, 1, 9999);
            RegisterTaskObservation("shop_quote", "核对已看到的商店 NPC 是否在队友原版交互范围、是否卖指定物品和队友当前价格。不消费。", ShopTaskQuote, item, npc);
            RegisterTaskOperation("buy_item", "前往场上的商人，按指定数量和总铜币预算从队友的钱包购买当场在售物品。",
                BuyTaskItems, CancelTravelTask, item, amount, budget, npc);
            RegisterTaskOperation("sell_item", "前往场上的商人，出售队友自己主背包的指定物品和数量，可指定槽。不会出售收藏物品。",
                SellTaskItems, CancelTravelTask, item, amount, npc, slot);
            RegisterTaskOperation("reforge_item", "到哥布林工匠处给指定装备重铸，使用队友的铜币预算和次数上限；可在达到指定词缀时停止。",
                ReforgeTaskItem, CancelTravelTask, item, budget, npc, slot,
                new TaskField("attempts", "integer", min: 1, max: 32), new TaskField("stop_prefix", "integer", min: 1, max: PrefixID.Count - 1));
            RegisterTaskOperation("nurse_heal", "前往护士。在队友已允许护士补给时执行一次正常治疗，按现行钱包规则和本次最高预算结算。",
                HealTaskNurse, CancelTravelTask, budget, npc);
            RegisterTaskOperation("turn_in_angler", "在渔夫处提交该队友当天实际拥有的任务鱼，核对该队友累计次数、奖励和当天资格。",
                TurnInTaskAngler, CancelTravelTask, npc);
            RegisterOtherServiceTasks();
        }

        partial void RegisterOtherServiceTasks();

        private static IEnumerable<int> ServiceShopPages(NPC npc)
        {
            foreach (var action in NPCInteractions.All)
                if (action is NPCInteractions.Actions.OpenShop shop && ServiceShopNpc(shop) == npc.type)
                    yield return ServiceShopIndex(shop);
        }

        private static bool ServiceNpcUsable(NPC npc) => npc?.active == true && npc.townNPC && !npc.CurrentlyShimmerTransparent();
        private static bool IsServiceVendor(NPC npc) => ServiceShopPages(npc).Any();

        private bool AtServiceNpc(Actor actor, NPC npc) => ActorTileReachReady(actor.Body) &&
            NearbyServiceTarget.Eligible(actor.Body, npc, npc.type, ActorTileReach(actor.Body), allowMouseInterface: true);

        private CompanionTaskOutcome ReachService(TaskExecution execution, Func<NPC, bool> accepts, out ServiceVisit visit)
        {
            var actor = execution.Actor;
            visit = execution.State as ServiceVisit;
            if (visit == null)
            {
                var table = TaskNpcTable;
                if (table == null) return TaskOutcome("waiting", "正在等当前世界的 NPC 资料。");
                string reference = execution.Text("npc");
                NPC best = null;
                if (reference != null)
                {
                    best = ReadTaskNpc(reference);
                    if (!ServiceNpcUsable(best) || !accepts(best))
                        return TaskOutcome("failed", "指定的 NPC 已离开或不提供此服务。");
                }
                else
                {
                    float distance = float.MaxValue;
                    foreach (var npc in table)
                    {
                        if (!ServiceNpcUsable(npc) || !TaskNpcObserved(npc) || !accepts(npc)) continue;
                        float candidate = Vector2.DistanceSquared(actor.Body.Center, npc.Center);
                        if (candidate >= distance) continue;
                        best = npc; distance = candidate;
                    }
                    if (best == null) return TaskOutcome("failed", "当前已观测的世界中未找到提供此服务的 NPC。");
                }
                execution.State = visit = new ServiceVisit { Npc = best, Slot = best.whoAmI, Generation = best.generation, ItemSlot = -1 };
            }

            var target = visit.Npc;
            if (!ServiceNpcUsable(target) || target.generation != visit.Generation ||
                TaskNpcTable == null || !ReferenceEquals(TaskNpcTable[visit.Slot], target))
                return ServiceVisitResult(visit, "failed", "原服务 NPC 已离开，未向替换对象交易。");
            if (!TaskNpcObserved(target)) return TaskOutcome("waiting", "正在等待该 NPC 的最新资料。", visit.Count);
            if (actor.Body.stinky || actor.Body.ownedProjectileCounts[ProjectileID.WireKite] > 0)
                return ServiceVisitResult(visit, "failed", "队友当前状态不允许与城镇 NPC 交互。");
            if (AtServiceNpc(actor, target)) return null;

            Point tile = target.Bottom.ToTileCoordinates();
            var area = visit.Arrival;
            if (area == null || Math.Abs(tile.X - area.X) > 2 || Math.Abs(tile.Y - area.Y) > 2)
            {
                area = visit.Arrival = new ArrivalSearch { X = tile.X, Y = tile.Y, HorizontalRadius = 5,
                    VerticalRadius = 7, Name = target.FullName };
                if (actor.Fishing != null) EndFishingActivity(actor);
                SetTaskTravel(actor, null);
            }
            if (actor.TaskTravel == null)
            {
                if (!FindTaskFooting(actor, area, out var feet, out var exhausted))
                    return exhausted ? ServiceVisitResult(visit, "failed", "NPC 附近没有找到可站立的原版交互位置。") :
                        TaskOutcome("waiting", "正在等待 NPC 附近的已知地形与落脚点。", visit.Count);
                SetTaskTravel(actor, new TaskTravel { Name = area.Name, Feet = feet });
            }
            if (actor.TaskTravel.Arrived && !actor.TaskTravel.Temporary && ActorTileReachReady(actor.Body))
                return ServiceVisitResult(visit, "failed", "已到附近，NPC 仍不在队友的原版交互范围。");
            return TaskOutcome("running", "正在前往" + target.FullName + "。", visit.Count);
        }

        private static CompanionTaskOutcome ServiceVisitResult(ServiceVisit visit, string state, string detail) =>
            TaskOutcome(visit.Count > 0 && state == "failed" ? "partial" : state, detail, visit.Count);

        private Item CurrentShopOffer(Actor actor, NPC npc, int type)
        {
            // Read the native stock page under this actor. The player's open
            // shop, cursor item, and sellback session keep their ownership.
            var stock = Chest.CreateShop();
            foreach (int page in ServiceShopPages(npc))
            {
                stock.SetupShop(page);
                foreach (var item in stock.item)
                    if (!item.IsAir && item.type == type && !item.buyOnce) return item;
            }
            return null;
        }

        private CompanionTaskOutcome ShopTaskQuote(TaskExecution execution)
        {
            var actor = execution.Actor;
            int type = execution.Number("item_type");
            var npc = ReadTaskNpc(execution.Text("npc"));
            if (!ServiceNpcUsable(npc) || !TaskNpcObserved(npc) || !IsServiceVendor(npc))
                return TaskOutcome("failed", "请先通过 NPC 观察指定在场的商人。");
            if (!AtServiceNpc(actor, npc)) return TaskOutcome("blocked", "队友还未到这家商店的原版交互范围。");
            if (!RefreshActorEnvironment(actor) || !NurseHousingKnown(npc))
                return TaskOutcome("waiting", "正在等待队友环境及 NPC 住房资料，以取得当前报价。");
            var previous = actor.Body.currentShoppingSettings;
            using (CompanionHooks.Enter(actor, actor.Aim))
            try
            {
                actor.Body.currentShoppingSettings = NurseShoppingSettings(actor, npc);
                var offer = CurrentShopOffer(actor, npc, type);
                if (offer == null) return TaskOutcome("failed", "指定物品当前未在这家店的原版库存中。");
                actor.Body.GetItemExpectedPrice(offer, out _, out long price);
                if (offer.shopSpecialCurrency != -1) return TaskOutcome("blocked", "当前物品使用商店专用货币，不能按铜币预算报价。");
                return TaskOutcome("succeeded", npc.FullName + "当场单价 " + price + " 铜币。", 1, price.ToString());
            }
            finally { actor.Body.currentShoppingSettings = previous; }
        }

        private bool ShopBuyerReady(Actor actor) => Main.netMode != 1 || !Main.ServerSideCharacter &&
            actor.Connection != null && !actor.Connection.Finished && actor.Ready;

        private CompanionTaskOutcome BuyTaskItems(TaskExecution execution)
        {
            int type = execution.Number("item_type"), amount = execution.Number("amount");
            var actor = execution.Actor;
            var body = actor.Body;
            if (!ShopBuyerReady(actor)) return TaskOutcome("failed", "当前连接不具备队友本人钱包与背包的有效结算入口。");
            var result = ReachService(execution, npc => IsServiceVendor(npc) &&
                (execution.Text("npc") != null || VendorMayStock(actor, npc, type)), out var visit);
            if (result != null) return result;
            var vendor = visit.Npc;
            if (!FishingHandFree(body) || !RefreshActorEnvironment(actor) || !NurseHousingKnown(vendor))
                return TaskOutcome("waiting", "等待手部空闲与商店报价资料。");
            var previous = body.currentShoppingSettings;
            using (CompanionHooks.Enter(actor, actor.Aim, inventory: true))
            try
            {
                body.currentShoppingSettings = NurseShoppingSettings(actor, vendor);
                var offer = CurrentShopOffer(actor, vendor, type);
                if (offer == null) return TaskOutcome("failed", "指定物品当前不在该商店的原版库存中。");
                if (offer.shopSpecialCurrency != -1) return TaskOutcome("failed", "该商品需要专用货币，当前铜币预算不能购买。");
                body.GetItemExpectedPrice(offer, out _, out long unit);
                long cost = unit * amount;
                if (unit < 0 || cost > execution.Number("budget_copper")) return TaskOutcome("failed", "当场报价超出指定总预算。");
                var wallet = PrepareServiceDebit(actor, cost);
                if (wallet == null) return TaskOutcome("failed", "队友的钱币或零钱槽不足，或正由手动操作使用。");
                var delivered = new Item(); delivered.SetDefaults(type);
                if (offer.prefix != 0) delivered.Prefix(offer.prefix);
                if (!PlanBoughtItems(actor, delivered, amount, wallet, out var writes))
                    return TaskOutcome("failed", "队友可使用的主背包空间不足，本次未付款。");
                if (!wallet.Current()) return TaskOutcome("failed", "队友钱包已变化，本次未付款。");
                execution.MarkSubmitted();
                if (!body.BuyItem(cost)) return TaskOutcome("failed", "原版未接受队友钱包的付款。");
                PublishServiceWallet(actor, wallet);
                if (!wallet.Debited()) return TaskOutcome("unconfirmed", "队友钱包的实际扣款与报价不符，未再次付款。");
                foreach (var write in writes)
                {
                    body.inventory[write.Key] = write.Value;
                    PublishServiceInventory(actor, write.Key);
                }
                visit.Count = amount;
                return TaskOutcome("succeeded", "已从" + vendor.FullName + "购买 " + amount + " 个" + delivered.Name +
                    "，队友实际支出 " + cost + " 铜币。", amount, type.ToString());
            }
            finally { body.currentShoppingSettings = previous; }
        }

        private bool VendorMayStock(Actor actor, NPC npc, int type)
        {
            using (CompanionHooks.Enter(actor, actor.Aim))
                return CurrentShopOffer(actor, npc, type) != null;
        }

        private CompanionNurseWallet PrepareServiceDebit(Actor actor, long cost)
        {
            if (!CompanionNurseWallet.TryBalance(actor.Body, out long balance)) return null;
            var wallet = CompanionNurseWallet.Prepare(actor.Body, balance, cost);
            if (wallet == null || wallet.Writes.Any(write => write.Before.favorited ||
                itemDraft?.DependsOn(write.Items, write.Slot) == true ||
                !SupplyAdmitted(actor, write.Items, write.Account, write.Slot))) return null;
            return wallet;
        }

        private void PublishServiceWallet(Actor actor, CompanionNurseWallet wallet)
        {
            actor.Dirty = true; InvalidateItemSources(actor);
            if (Main.netMode != 1) return;
            foreach (var write in wallet.Writes)
                QueueItemSync(actor.Body, actor, SupplyNetworkBases[write.Account] + write.Slot);
        }

        private void PublishServiceInventory(Actor actor, int slot)
        {
            actor.Dirty = true; InvalidateItemSources(actor);
            if (Main.netMode == 1) QueueItemSync(actor.Body, actor, PlayerItemSlotID.Inventory0 + slot);
        }

        private bool PlanBoughtItems(Actor actor, Item sample, int amount, CompanionNurseWallet wallet, out Dictionary<int, Item> writes)
        {
            writes = new Dictionary<int, Item>();
            for (int phase = 0; phase < 2 && amount > 0; phase++)
                for (int slot = 0; slot < CompanionBehavior.MainInventorySlots && amount > 0; slot++)
                {
                    var item = actor.Body.inventory[slot];
                    if (wallet.Writes.Any(write => write.Account == 0 && write.Slot == slot) ||
                        TaskResourceSlotBlocked(actor, false, CompanionInventoryArea.Inventory, slot) || item.favorited ||
                        (phase == 0 ? item.IsAir || !Item.CanStack(item, sample) : !item.IsAir)) continue;
                    int moved = Math.Min(amount, item.IsAir ? sample.maxStack : item.maxStack - item.stack);
                    if (moved <= 0) continue;
                    var after = item.IsAir ? sample.Clone() : item.Clone();
                    after.stack = (item.IsAir ? 0 : item.stack) + moved;
                    writes.Add(slot, after); amount -= moved;
                }
            return amount == 0;
        }

        private int FindServiceItem(Actor actor, int type, int preferred, int quantity, bool reforge = false)
        {
            for (int i = 0; i < CompanionBehavior.MainInventorySlots; i++)
            {
                if (preferred >= 0 && preferred != i) continue;
                Item item = actor.Body.inventory[i];
                if (item?.type == type && !item.IsAir && item.stack >= quantity && !item.favorited &&
                    (!reforge || item.CanHavePrefixes()) && !TaskResourceSlotBlocked(actor, false, CompanionInventoryArea.Inventory, i))
                    return i;
            }
            return -1;
        }

        private CompanionTaskOutcome SellTaskItems(TaskExecution execution)
        {
            var actor = execution.Actor;
            var body = actor.Body;
            int type = execution.Number("item_type"), amount = execution.Number("amount");
            if (!ShopBuyerReady(actor)) return TaskOutcome("failed", "当前连接不具备队友本人背包的有效结算入口。");
            var result = ReachService(execution, IsServiceVendor, out var visit);
            if (result != null) return result;
            if (!FishingHandFree(body) || !RefreshActorEnvironment(actor) || !NurseHousingKnown(visit.Npc))
                return TaskOutcome("waiting", "等待手部空闲和商店报价资料。");
            var original = body.inventory;
            int preferred = execution.Number("slot", -1), outstanding = amount;
            var portions = new List<KeyValuePair<int, int>>();
            for (int slot = 0; slot < CompanionBehavior.MainInventorySlots && outstanding > 0; slot++)
            {
                var candidate = original[slot];
                if (preferred >= 0 && preferred != slot || candidate.type != type || candidate.IsAir || candidate.IsACoin ||
                    candidate.favorited || TaskResourceSlotBlocked(actor, false, CompanionInventoryArea.Inventory, slot)) continue;
                int selling = Math.Min(outstanding, candidate.stack);
                portions.Add(new KeyValuePair<int, int>(slot, selling));
                outstanding -= selling;
            }
            if (outstanding != 0) return TaskOutcome("failed", "队友没有可出售的指定物品和数量。");
            var projected = original.Select(item => item.Clone()).ToArray();
            var previous = body.currentShoppingSettings;
            var sellback = Main.shopSellbackHelper;
            using (CompanionHooks.Enter(actor, actor.Aim, inventory: true))
            try
            {
                // Run original payout against a private array before committing its exact changed slots.
                // SellItem does not remove the sold stack; the native UI owns that step.
                body.currentShoppingSettings = NurseShoppingSettings(actor, visit.Npc);
                Main.shopSellbackHelper = new ItemShopSellbackHelper();
                body.inventory = projected;
                foreach (var portion in portions)
                {
                    Item sale = projected[portion.Key].Clone();
                    sale.stack = portion.Value;
                    if (!body.SellItem(sale, portion.Value)) return TaskOutcome("failed", "原版拒绝出售，或没有容纳实际零钱的空间。");
                    projected[portion.Key].stack -= portion.Value;
                    if (projected[portion.Key].stack == 0) projected[portion.Key] = new Item();
                }
            }
            finally { body.inventory = original; body.currentShoppingSettings = previous; Main.shopSellbackHelper = sellback; }
            var changed = new List<int>();
            long copper = 0;
            for (int i = 0; i < 54; i++)
            {
                Item before = original[i], after = projected[i];
                if (before.type == after.type && before.stack == after.stack && before.prefix == after.prefix) continue;
                if (before.favorited || itemDraft?.DependsOn(original, i) == true || !SupplyAdmitted(actor, original, 0, i))
                    return TaskOutcome("failed", "找零会碰到收藏物品或手动操作槽，本次未出售。");
                copper += ServiceCopper(after) - ServiceCopper(before);
                changed.Add(i);
            }
            if (copper <= 0) return TaskOutcome("failed", "未得到实际原版出售收入。");
            execution.MarkSubmitted();
            foreach (int i in changed)
            {
                original[i] = projected[i];
                PublishServiceInventory(actor, i);
            }
            return TaskOutcome("succeeded", "已出售 " + amount + " 个" + Lang.GetItemNameValue(type) + "，队友获得 " + copper + " 铜币。", amount, type.ToString());
        }

        private static long ServiceCopper(Item item)
        {
            switch (item.type)
            {
                case ItemID.CopperCoin: return item.stack;
                case ItemID.SilverCoin: return (long)item.stack * 100;
                case ItemID.GoldCoin: return (long)item.stack * 10000;
                case ItemID.PlatinumCoin: return (long)item.stack * 1000000;
                default: return 0;
            }
        }
    }
}
