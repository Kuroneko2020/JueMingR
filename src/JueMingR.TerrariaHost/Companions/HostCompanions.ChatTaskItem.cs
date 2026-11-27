using System;
using System.Globalization;
using JueMingR.Platform.Companions;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        partial void RegisterItemUseTaskOperations()
        {
            RegisterTaskOperation("use_item", "Use one matching main-inventory item through its native hand action. " +
                "The task resolves the item again for each distinct step. Named teleport forms use their actual destination.",
                UseTaskItem, CancelTaskItem, new TaskField("item_type", "integer", true, 1, ItemID.Count - 1),
                new TaskField("prefix", "integer", false, 0, Byte.MaxValue));
        }

        private CompanionTaskOutcome UseTaskItem(TaskExecution execution)
        {
            if (execution.State is ItemOnce)
                return TaskOutcome("running", "等原版用物动作和结果。");
            var actor = execution.Actor;
            if (ManualHandBusy(actor))
                return TaskOutcome("waiting", "等队友当前手部动作结束。");
            int type = execution.Number("item_type"), prefix = execution.Number("prefix", -1);
            if (actor.Supplies.ChatTeleportPending != null && ItemTeleportStyle(ItemTeleportKind(type)) >= 0)
                return TaskOutcome("waiting", "等该队友先前的原版传送完成同步。");
            string reason = null;
            for (int slot = 0; slot < CompanionBehavior.MainInventorySlots; slot++)
            {
                Item item = actor.Body.inventory[slot];
                if (item.type != type || item.IsAir || prefix >= 0 && item.prefix != prefix) continue;
                if (!CanUseItemOnce(actor.Profile.Id, CompanionInventoryArea.Inventory, slot, out reason) ||
                    !TryQueueItemOnce(actor, slot, false, out reason)) continue;
                var once = actor.Supplies.Once;
                execution.State = once;
                execution.MarkSubmitted();
                once.ChatActivation = execution.Job.Identity.Activation;
                once.ChatDataEpoch = execution.Job.Identity.DataEpoch;
                string name = item.Name;
                once.ChatComplete = (state, message) => execution.Settle(
                    TaskOutcome(state, name + " · " + message, state == "succeeded" ? 1 : 0,
                        type.ToString(CultureInfo.InvariantCulture)));
                // The native scheduler owns the source snapshot after this
                // point. Model completion cannot resubmit this attempt.
                return TaskOutcome("running", "原版手部已受理 " + name + "。");
            }
            return TaskOutcome("failed", reason ?? "队友实际主背包中没有该物品或前缀。");
        }

        private void CancelTaskItem(TaskExecution execution)
        {
            if (execution.State is ItemOnce command && ReferenceEquals(execution.Actor.Supplies.Once, command))
                CancelChatItemOnce(execution.Actor, "已停止任务并撤回尚未开始的用物。");
        }
    }
}
