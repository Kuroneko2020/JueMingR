using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Terraria;
using Terraria.ID;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private static readonly Action<Item[], int, int, Player> OpenTaskContainerNative =
            (Action<Item[], int, int, Player>)Delegate.CreateDelegate(typeof(Action<Item[], int, int, Player>),
                AccessTools.Method(typeof(ItemSlot), "TryOpenContainer"));
        private sealed class TaskOpening
        {
            internal int Opened;
            internal readonly Dictionary<int, int> Received = new Dictionary<int, int>();
        }

        private sealed class TaskExtraction
        {
            internal TargetTile Target;
            internal WorldUse Work;
            internal CompanionTaskOutcome Last;
            internal Dictionary<int, int> Before;
            internal int Processed;
            internal uint ObservationsUntil;
        }

        partial void RegisterTaskProcessing()
        {
            RegisterTaskOperation("open_container", "沿原版打开队友背包中的袋、匣或钓鱼箱，按实际数量停止。金锁盒消耗钥匙须明确 allow_key。",
                OpenTaskContainer, null, new TaskField("item_type", "integer", true, 1, ItemID.Count - 1),
                new TaskField("amount", "integer", true, 1, 99999), new TaskField("allow_key", "boolean"));
            RegisterTaskOperation("extract_item", "走到 observe_tiles 返回的原版提炼机，用指定材料提炼，并报告实际已入包的产物。",
                ExtractTaskItems, CancelWorldUse, new TaskField("target", required: true, min: 1, max: 64),
                new TaskField("item_type", "integer", true, 1, ItemID.Count - 1), new TaskField("amount", "integer", true, 1, 99999));
            RegisterTaskShimmer();
        }

        private CompanionTaskOutcome ExtractTaskItems(TaskExecution execution)
        {
            var actor = execution.Actor;
            int type = execution.Number("item_type"), amount = execution.Number("amount");
            if (!(execution.State is TaskExtraction work))
            {
                if (ItemID.Sets.ExtractinatorMode[type] < 0 || actor.TaskTiles == null ||
                    !actor.TaskTiles.TryGetValue(execution.Text("target"), out var target) || !target.Fact.Active ||
                    target.Fact.Type != TileID.Extractinator && target.Fact.Type != TileID.ChlorophyteExtractinator)
                    return TaskOutcome("failed", "指定材料或已观察到的提炼机不符合原版条件。");
                if (actor.Fishing != null) EndFishingActivity(actor);
                execution.State = work = new TaskExtraction { Target = target, Before = TaskBagAmounts(actor.Body) };
            }
            if (work.Last != null)
            {
                var result = work.Last;
                work.Last = null;
                work.Work = null;
                if (result.State != "succeeded") return TaskOutcome(work.Processed > 0 && result.State == "failed" ? "partial" : result.State,
                    result.Detail, work.Processed);
                work.Processed += result.Completed;
                if (work.Processed >= amount) work.ObservationsUntil = Main.GameUpdateCount + 45;
            }
            if (work.Processed >= amount)
            {
                if (Main.GameUpdateCount < work.ObservationsUntil) return TaskOutcome("waiting", "已处理指定数量，正在观察实际拾取。", work.Processed);
                var products = TaskBagAmounts(actor.Body).Where(entry => entry.Key != type &&
                    (!work.Before.TryGetValue(entry.Key, out int before) || entry.Value > before)).Take(12)
                    .Select(entry => { work.Before.TryGetValue(entry.Key, out int prior); return Lang.GetItemNameValue(entry.Key) + "×" + (entry.Value - prior); }).ToArray();
                return TaskOutcome("succeeded", "原版实际处理 " + work.Processed + " 份材料。" +
                    (products.Length == 0 ? "观察期没有确认入包产物。" : "观察到入包 " + String.Join("、", products) + "。"), work.Processed);
            }
            if (work.Work == null) work.Work = new WorldUse { Point = work.Target.Point, Before = work.Target.Fact, Operation = "extract" };
            var step = AdvanceWorldUse(execution, work.Work, type, 0);
            if (work.Work.Hand != null) work.Work.Hand.Completed = result =>
            {
                if (!ReferenceEquals(execution.Job.Current, execution))
                    execution.Settle(TaskOutcome("partial", "停止前实际处理 " + (work.Processed + result.Completed) + " 份材料。",
                        work.Processed + result.Completed));
                else work.Last = result;
            };
            return work.Processed > 0 && step.State == "failed" ? TaskOutcome("partial", step.Detail, work.Processed) : step;
        }

        private CompanionTaskOutcome OpenTaskContainer(TaskExecution execution)
        {
            int type = execution.Number("item_type"), amount = execution.Number("amount");
            var actor = execution.Actor;
            var body = actor.Body;
            if (!ItemID.Sets.OpenableBag[type]) return TaskOutcome("failed", "该物品没有原版右键打开操作。");
            if (!(execution.State is TaskOpening work)) execution.State = work = new TaskOpening();
            if (!FishingHandFree(body) || body.IsStackingItems()) return TaskOutcome("waiting", "等队友结束手中动作后打开。", work.Opened);
            int slot = Enumerable.Range(0, 50).Where(index => body.inventory[index].type == type && body.inventory[index].stack > 0 &&
                !body.inventory[index].favorited && !TaskResourceSlotBlocked(actor, false, CompanionInventoryArea.Inventory, index)).DefaultIfEmpty(-1).First();
            if (slot < 0) return EndTaskOpening(work, "没有更多可打开的指定物品。", work.Opened > 0 ? "partial" : "failed");
            if (type == ItemID.LockBox && (!execution.Flag("allow_key") || !CanSpendTaskContainerKey(actor)))
                return EndTaskOpening(work, "金锁盒需要明确允许且可使用的金钥匙。", work.Opened > 0 ? "partial" : "failed");
            var before = TaskBagAmounts(body);
            int originals = before.TryGetValue(type, out int count) ? count : 0;
            int split = Main.stackSplit;
            bool rightRelease = Main.mouseRightRelease;
            execution.MarkSubmitted();
            try
            {
                using (CompanionHooks.Enter(actor, actor.Aim, inventory: true))
                {
                    Main.mouseItem = new Item();
                    OpenTaskContainerNative(body.inventory, ItemSlot.Context.InventoryItem, slot, body);
                }
            }
            finally { Main.stackSplit = split; Main.mouseRightRelease = rightRelease; actor.Dirty = true; }
            InvalidateItemSources(actor);
            var after = TaskBagAmounts(body);
            after.TryGetValue(type, out int remaining);
            if (remaining != originals - 1)
                return EndTaskOpening(work, "原版未确认消耗一个容器，已停止。", work.Opened > 0 ? "partial" : "failed");
            work.Opened++;
            foreach (var increase in after)
            {
                before.TryGetValue(increase.Key, out int old);
                if (increase.Value <= old) continue;
                work.Received.TryGetValue(increase.Key, out int prior);
                work.Received[increase.Key] = prior + increase.Value - old;
            }
            return work.Opened >= amount ? EndTaskOpening(work, "实际打开指定容器。", "succeeded") :
                TaskOutcome("running", "已打开 " + work.Opened + " 个容器。", work.Opened);
        }

        private bool CanSpendTaskContainerKey(Actor actor)
        {
            var body = actor.Body;
            foreach (var area in new[] { CompanionInventoryArea.Inventory, CompanionInventoryArea.VoidVault })
            {
                if (area == CompanionInventoryArea.VoidVault && !body.useVoidBag()) break;
                var slots = CompanionResources.Items(body, area);
                int count = area == CompanionInventoryArea.Inventory ? 58 : slots.Length;
                for (int slot = 0; slot < count; slot++)
                    if (slots[slot].type == ItemID.GoldenKey && !slots[slot].IsAir)
                        return !slots[slot].favorited && !TaskResourceSlotBlocked(actor, false, area, slot);
            }
            return false;
        }

        private static Dictionary<int, int> TaskBagAmounts(Player body)
        {
            var items = new Dictionary<int, int>();
            for (int i = 0; i < 58; i++)
                if (!body.inventory[i].IsAir)
                {
                    var item = body.inventory[i];
                    items.TryGetValue(item.type, out int count);
                    items[item.type] = count + item.stack;
                }
            return items;
        }

        private static CompanionTaskOutcome EndTaskOpening(TaskOpening work, string reason, string status)
        {
            var results = work.Received.Take(12).Select(entry => Lang.GetItemNameValue(entry.Key) + "×" + entry.Value);
            string text = reason + " 共打开 " + work.Opened + " 个。";
            if (work.Received.Count > 0) text += " 背包已取得 " + String.Join("、", results) + "。";
            return TaskOutcome(status, text, work.Opened);
        }
    }
}
