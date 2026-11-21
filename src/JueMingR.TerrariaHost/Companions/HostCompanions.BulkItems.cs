using System;
using JueMingR.Platform.Companions;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal bool BulkItems(Guid id, CompanionInventoryArea area, CompanionBulkOperation operation)
        {
            if (!ItemOwnerCurrent() || !actors.TryGetValue(id, out var actor) || !ItemActorCurrent(id, actor))
                return Result(false, "队友当前不能整理物品。");
            if (itemDraft != null || Main.mouseItem?.IsAir == false)
                return Result(false, "请先放回手中的物品。");
            if (!StorageAvailable(id, area, out string reason)) return Result(false, reason);
            var access = bankAccess;
            var draft = new CompanionItemDraft(owner, actor.Body, id, actor.Generation, session, world,
                (isOwner, a, s) => ItemSlotBlocked(isOwner, isOwner ? owner : actor.Body, a, s),
                bankCurrent: bank => BankCurrent(access, bank));
            var storage = draft.Project(false, area, area == CompanionInventoryArea.Inventory ? 58 : CompanionResources.Items(actor.Body, area).Length);
            var storageBlocked = BulkBlocked(draft, false, area, storage.Length);
            Item[] backpack = null;
            bool[] backpackBlocked = null;
            if (operation != CompanionBulkOperation.Sort)
            {
                backpack = draft.Project(true, CompanionInventoryArea.Inventory, 58);
                backpackBlocked = BulkBlocked(draft, true, CompanionInventoryArea.Inventory, backpack.Length);
            }
            if (operation == CompanionBulkOperation.Sort)
            {
                try
                {
                    // ItemSorting's private pure-sort leaf reads this identity
                    // for priorities. Feed it detached items and disable glow;
                    // avoid the world/chest wrapper and its packet/UI effects.
                    using (CompanionHooks.Enter(actor, actor.Aim))
                        CompanionBulkItems.Plan(operation, area, owner, backpack, storage, backpackBlocked, storageBlocked);
                }
                catch (Exception) { return Result(false, "原版排列失败，物品未改变。"); }
            }
            else CompanionBulkItems.Plan(operation, area, owner, backpack, storage, backpackBlocked, storageBlocked);
            draft.Stage(false, area, storage);
            if (backpack != null) draft.Stage(true, CompanionInventoryArea.Inventory, backpack);
            if (!ItemOwnerCurrent() || !ItemActorCurrent(id, actor) || draft.Generation != actor.Generation ||
                !ReferenceEquals(draft.Owner, owner) || !ReferenceEquals(draft.Companion, actor.Body) ||
                draft.Session != session || !ReferenceEquals(draft.World, world) || !draft.TryPrepare(out var changes))
                return Result(false, draft.InvalidBank ? "队友储存入口已失效，物品未改变。" : "物品已变化，请重试。");
            if (changes.Count == 0) return Result(false, "没有可移动或需要排列的物品。");
            ItemGestureRevision++;
            foreach (var change in changes) change.Apply();
            return FinishItemCommit(actor, changes, BulkFeedback(operation));
        }

        private static bool[] BulkBlocked(CompanionItemDraft draft, bool owner, CompanionInventoryArea area, int count)
        {
            var blocked = new bool[count];
            for (int slot = 0; slot < count; slot++) blocked[slot] = draft.Blocked(owner, area, slot);
            return blocked;
        }

        private static string BulkFeedback(CompanionBulkOperation operation)
        {
            switch (operation)
            {
                case CompanionBulkOperation.LootAll: return "已取出能装进主人背包的物品。";
                case CompanionBulkOperation.DepositAll: return "已存入可以转移的物品。";
                case CompanionBulkOperation.QuickStack: return "已快速堆叠匹配的物品。";
                case CompanionBulkOperation.Restock: return "已补充主人背包中的现有物品。";
                default: return "已排列当前储存中的物品。";
            }
        }
    }
}
