using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Platform.Companions;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private CompanionTaskOutcome ReforgeTaskItem(TaskExecution execution)
        {
            var actor = execution.Actor;
            var body = actor.Body;
            int type = execution.Number("item_type"), maximum = execution.Number("attempts", 1), prefix = execution.Number("stop_prefix");
            if (!ShopBuyerReady(actor)) return TaskOutcome("failed", "当前连接不具备队友自己的原版钱包结算入口。");
            var result = ReachService(execution, npc => npc.type == NPCID.GoblinTinkerer, out var visit);
            if (result != null) return result;
            if (visit.ItemSlot < 0)
            {
                visit.ItemSlot = FindServiceItem(actor, type, execution.Number("slot", -1), 1, true);
                if (visit.ItemSlot < 0) return TaskOutcome("failed", "队友主背包没有可重铸的指定非收藏物品。");
                visit.Item = body.inventory[visit.ItemSlot];
            }
            var item = visit.Item;
            if (!ReferenceEquals(body.inventory[visit.ItemSlot], item) || item.IsAir || item.type != type || item.favorited)
                return ServiceVisitResult(visit, "failed", "原重铸物品已改变。");
            if (prefix > 0 && item.prefix == prefix)
                return TaskOutcome("succeeded", "装备已具备指定词缀；实际支出 " + visit.Spent + " 铜币。", visit.Count, item.prefix.ToString());
            if (visit.Count >= maximum)
                return TaskOutcome(prefix == 0 ? "succeeded" : "partial", "已到指定重铸次数，实际支出 " + visit.Spent +
                    " 铜币，当前词缀 " + item.prefix + "。", visit.Count, item.prefix.ToString());
            if (unchecked((int)(Main.GameUpdateCount - visit.Next)) < 0 || !FishingHandFree(body) ||
                TaskResourceSlotBlocked(actor, false, CompanionInventoryArea.Inventory, visit.ItemSlot))
                return TaskOutcome("waiting", "正在等原版重铸间隔和物品可用。", visit.Count);
            if (!RefreshActorEnvironment(actor) || !NurseHousingKnown(visit.Npc))
                return TaskOutcome("waiting", "正在等当前群系及工匠住房资料。", visit.Count);
            var previous = body.currentShoppingSettings;
            using (CompanionHooks.Enter(actor, actor.Aim, inventory: true))
            try
            {
                body.currentShoppingSettings = NurseShoppingSettings(actor, visit.Npc);
                // Same cost order as the original reforge button, followed
                // by its actual Prefix(-2) item mutation without UI effects.
                long cost = (long)item.value * item.stack;
                if (body.discountAvailable) cost = (long)(cost * 0.8);
                cost = (long)(cost * body.currentShoppingSettings.PriceAdjustment) / 3;
                if (cost < 0 || visit.Spent + cost > execution.Number("budget_copper"))
                    return ServiceVisitResult(visit, "failed", "当前原版报价超出剩余预算，实际已支出 " + visit.Spent + " 铜币。");
                var wallet = PrepareServiceDebit(actor, cost);
                if (wallet == null || !wallet.Current())
                    return ServiceVisitResult(visit, "failed", "队友钱包未就绪，本次未付款。");
                execution.MarkSubmitted();
                if (!body.BuyItem(cost)) return ServiceVisitResult(visit, "failed", "原版未接受队友钱包付款。");
                PublishServiceWallet(actor, wallet);
                if (!wallet.Debited()) return TaskOutcome("unconfirmed", "重铸扣款结果未确认，未再次付款。", visit.Count);
                visit.Spent += cost;
                item.ResetPrefix();
                bool completedRoll = item.Prefix(-2, out bool best);
                PublishServiceInventory(actor, visit.ItemSlot);
                if (!completedRoll)
                    return TaskOutcome("unconfirmed", "已扣款，但原版未确认词缀重铸，未再次付款。", visit.Count);
                visit.Count++;
                visit.Next = Main.GameUpdateCount + (best ? 60u : 1u);
                if (prefix > 0 && item.prefix == prefix || prefix == 0 && visit.Count == maximum)
                    return TaskOutcome("succeeded", "实际重铸 " + visit.Count + " 次，支出 " + visit.Spent +
                        " 铜币，当前词缀 " + item.prefix + "。", visit.Count, item.prefix.ToString());
                return TaskOutcome("running", "已完成 " + visit.Count + " 次原版重铸。", visit.Count);
            }
            finally { body.currentShoppingSettings = previous; }
        }

        private CompanionTaskOutcome HealTaskNurse(TaskExecution execution)
        {
            var actor = execution.Actor;
            var body = actor.Body;
            if (!actor.Preferences.AllowNurse) return TaskOutcome("failed", "此队友的护士补给没有启用。");
            if (!NurseNeedsService(body)) return TaskOutcome("succeeded", "队友当前没有需要护士处理的伤势或异常。");
            var result = ReachService(execution, npc => npc.type == NPCID.Nurse, out var visit);
            if (result != null) return result;
            if (!RefreshActorEnvironment(actor) || !NurseHousingKnown(visit.Npc) ||
                Main.GameUpdateCount < actor.Supplies.NextNurseAttempt)
                return TaskOutcome("waiting", "正在等该护士与现行补给付款入口就绪。");
            using (CompanionHooks.Enter(actor, actor.Aim))
            {
                if (!ReferenceEquals(FindNurse(actor), visit.Npc))
                    return TaskOutcome("failed", "当前原版治疗入口对不上指定护士，没有扣款。");
                return PayTaskNurse(execution, visit);
            }
        }

        private CompanionTaskOutcome PayTaskNurse(TaskExecution execution, ServiceVisit visit)
        {
            var actor = execution.Actor;
            var body = actor.Body;
            var previous = body.currentShoppingSettings;
            int quote;
            try
            {
                body.currentShoppingSettings = NurseShoppingSettings(actor, visit.Npc);
                quote = Main.GetNurseHealCost();
            }
            finally { body.currentShoppingSettings = previous; }
            if (quote < 1 || quote > execution.Number("budget_copper"))
                return TaskOutcome("failed", "护士当场需要 " + quote + " 铜币，未在本次预算内付款。");
            execution.MarkSubmitted();
            var outcome = TryCompanionNurse(actor);
            if (outcome == CompanionNurseOutcome.Treated)
                return TaskOutcome("succeeded", "护士已实际治疗队友，本次报价 " + quote + " 铜币。", 1);
            if (outcome == CompanionNurseOutcome.Unconfirmed)
                return TaskOutcome("unconfirmed", "护士已经扣款，治疗结果未确认，未再次付款。");
            return TaskOutcome("failed", "护士未成交，请检查资格和现行护士钱包规则。");
        }

        private sealed class AnglerServiceClaim
        {
            internal TaskExecution Execution;
            internal int Quest, Fish;
            internal string Name;
            internal long DataEpoch;
            internal bool Acknowledged;
            internal string LocalReceipt;
        }

        private readonly Dictionary<Guid, AnglerServiceClaim> anglerServiceClaims = new Dictionary<Guid, AnglerServiceClaim>();
        private long anglerServiceSession = -1;

        private void RefreshAnglerServiceClaims()
        {
            if (anglerServiceSession == session) return;
            anglerServiceSession = session;
            anglerServiceClaims.Clear();
        }

        partial void ObserveTaskAnglerReceipt(Actor actor)
        {
            RefreshAnglerServiceClaims();
            if (!anglerServiceClaims.TryGetValue(actor.Profile.Id, out var claim)) return;
            var line = actor.FishingLine;
            if (actor.Profile.DataEpoch != claim.DataEpoch || actor.Body.name != claim.Name ||
                line.Quest != claim.Quest || claim.Acknowledged && !line.QuestFinished)
            {
                anglerServiceClaims.Remove(actor.Profile.Id);
                return;
            }
            if (!line.QuestFinished || claim.Acknowledged) return;
            claim.Acknowledged = true;
            var pending = claim.Execution;
            claim.Execution = null;
            if (claim.LocalReceipt != null)
                pending?.Settle(TaskOutcome("succeeded", claim.LocalReceipt + " 服务器已同步当天完成资格。", 1, claim.Fish.ToString()));
        }

        private CompanionTaskOutcome TurnInTaskAngler(TaskExecution execution)
        {
            var actor = execution.Actor;
            var body = actor.Body;
            bool client = Main.netMode == 1;
            RefreshAnglerServiceClaims();
            if (!ShopBuyerReady(actor)) return TaskOutcome("failed", "当前连接尚不能结算该队友自己的原版渔夫交付。");
            if (client && anglerServiceClaims.TryGetValue(actor.Profile.Id, out var oldClaim))
            {
                if (oldClaim.Quest == actor.FishingLine.Quest && oldClaim.Name == body.name && oldClaim.DataEpoch == actor.Profile.DataEpoch)
                    return TaskOutcome("failed", oldClaim.Acknowledged ? "该队友今天已经提交。" : "该队友本次已提交且服务端尚未确认，不能重复扣鱼。");
                anglerServiceClaims.Remove(actor.Profile.Id);
            }
            if (String.IsNullOrWhiteSpace(body.name) || owner != null && body.name == owner.name ||
                Enumerable.Range(0, Main.maxPlayers).Any(index => index != body.whoAmI &&
                    Main.player[index]?.active == true && Main.player[index].name == body.name))
                return TaskOutcome("failed", "原版用名字记录当日交付，此队友与另一位玩家重名。");
            int type = ActorQuestType(actor);
            if (type < 1) return TaskOutcome("failed", "当前任务鱼或队友自己的当日资格尚不可用，或今天已经交付。");
            var result = ReachService(execution, npc => npc.type == NPCID.Angler, out var visit);
            if (visit != null)
            {
                if (visit.QuestType == 0) visit.QuestType = type;
                else if (visit.QuestType != type) return TaskOutcome("failed", "途中任务鱼已变化，未扣鱼，请重新核对当天任务。");
            }
            if (result != null) return result;
            if (ActorQuestType(actor) != type) return TaskOutcome("failed", "当天任务已变化，未扣鱼。");
            int itemSlot = FindServiceItem(actor, type, -1, 1);
            if (itemSlot < 0) return TaskOutcome("failed", "队友自己的主背包没有当前任务要求的可交付鱼。");
            if (!FishingHandFree(body) || Enumerable.Range(0, body.inventory.Length).Any(slot => itemDraft?.DependsOn(body.inventory, slot) == true))
                return TaskOutcome("waiting", "等待队友背包编辑完成后接收原版奖励。");

            int beforeCounter = body.anglerQuestsFinished;
            var oldItems = body.inventory.Select(item => item.Clone()).ToArray();
            var previousRewards = CountRewardItems(oldItems);
            AnglerServiceClaim claim = null;
            if (client)
            {
                claim = new AnglerServiceClaim { Execution = execution, Quest = actor.FishingLine.Quest, Fish = type,
                    Name = body.name, DataEpoch = actor.Profile.DataEpoch };
                anglerServiceClaims.Add(actor.Profile.Id, claim);
                execution.MarkSubmitted();
                try
                {
                    if (!actor.Connection.TrySend(CompanionConnection.Packet(75)))
                    {
                        anglerServiceClaims.Remove(actor.Profile.Id);
                        return TaskOutcome("failed", "队友连接未接纳这次交付，未扣鱼。");
                    }
                }
                catch (CompanionConnection.SendQueueFullException)
                {
                    anglerServiceClaims.Remove(actor.Profile.Id);
                    return TaskOutcome("failed", "队友连接队列未接纳交付，未扣鱼。");
                }
                // No immediate success reply is defined for 75. The same
                // actor's next authoritative 74 may update the retained receipt.
                actor.FishingLine.QuestFinished = true;
            }
            else
            {
                execution.MarkSubmitted();
                Main.anglerWhoFinishedToday.Add(body.name);
            }

            // This is the transactional part of NPCChatText_DoAnglerQuest.
            // The dialogue widget owns chat, portrait animation and achievements;
            // the native reward implementation owns all reward selection.
            using (CompanionHooks.EnterWithPersonalItems(actor))
            {
                if (--body.inventory[itemSlot].stack <= 0) body.inventory[itemSlot] = new Item();
                body.anglerQuestsFinished++;
                body.GetAnglerReward(visit.Npc, type);
            }
            for (int slot = 0; slot < oldItems.Length; slot++)
            {
                var before = oldItems[slot]; var after = body.inventory[slot];
                if (before.type != after.type || before.stack != after.stack || before.prefix != after.prefix)
                    PublishServiceInventory(actor, slot);
            }
            actor.Dirty = true;
            if (client)
            {
                var connection = actor.Connection;
                connection.TrySend(CompanionConnection.Packet(76, writer =>
                {
                    writer.Write((byte)body.whoAmI);
                    writer.Write(body.anglerQuestsFinished);
                    writer.Write(body.golferScoreAccumulated);
                }));
            }
            if (body.anglerQuestsFinished != beforeCounter + 1)
                return TaskOutcome("unconfirmed", "已调用原版队友交付，累计结果未确认。");
            var reward = CountRewardItems(body.inventory).Where(pair => pair.Key != type &&
                pair.Value > (previousRewards.TryGetValue(pair.Key, out int old) ? old : 0))
                .Take(12).Select(pair => Lang.GetItemNameValue(pair.Key) + "×" +
                    (pair.Value - (previousRewards.TryGetValue(pair.Key, out int old) ? old : 0)));
            string receipt = "队友已交付 " + Lang.GetItemNameValue(type) + "，累计 " + body.anglerQuestsFinished +
                "；可核对入包奖励：" + String.Join("、", reward) + "。";
            if (claim == null) return TaskOutcome("succeeded", receipt, 1, type.ToString());
            claim.LocalReceipt = receipt;
            return TaskOutcome("unconfirmed", receipt + " 尚未收到该队友的服务器当日资格回执。", 1, type.ToString());
        }

        private static Dictionary<int, int> CountRewardItems(Item[] inventory) => inventory.Where(item => !item.IsAir)
            .GroupBy(item => item.type).ToDictionary(group => group.Key, group => group.Sum(item => item.stack));
    }
}
