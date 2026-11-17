using System;
using System.Collections.Generic;
using System.Threading;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Items;
using JueMingR.TerrariaHost.Items;
using Terraria;
using Terraria.ID;
using Terraria.UI;
using Terraria.GameInput;
using System.Reflection;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal ItemOperationOwnership OwnerOperations { get; set; }
        internal HostItems OwnerItems { get; set; }
        private readonly int itemThread = Thread.CurrentThread.ManagedThreadId;
        private CompanionItemDraft itemDraft;
        private readonly Item emptyItemCursor = new Item();
        internal long ItemGestureRevision { get; private set; }
        internal bool HasItemGesture { get { return itemDraft != null; } }
        internal Item CursorItem { get { return itemDraft?.Cursor ?? emptyItemCursor; } }
        private readonly HashSet<Guid> pendingItemCaptures = new HashSet<Guid>();
        private readonly List<ItemSync> pendingItemSync = new List<ItemSync>();
        private uint nextItemSync;
        private Player itemMaterialBody;
        private uint itemMaterialStep;
        private readonly HashSet<int> itemMaterialTypes = new HashSet<int>();

        private sealed class ItemSync
        {
            internal Player Body;
            internal Actor Actor;
            internal long Session, Generation;
            internal object World;
            internal object MainConnection, MainSocket, ActorConnection;
            internal int Slot;
        }

        internal void CancelItemGesture()
        { if (itemDraft == null) return; itemDraft = null; itemMaterialBody = null; itemMaterialTypes.Clear(); ItemGestureRevision++; }
        internal void CancelItemGestureForActor(Actor actor)
        {
            if (actor != null && itemDraft != null && itemDraft.UsesCompanion && itemDraft.Profile == actor.Profile.Id &&
                itemDraft.Generation == actor.Generation && ReferenceEquals(itemDraft.Companion, actor.Body)) CancelItemGesture();
        }
        internal void EndItemRepeat() { itemDraft?.EndRepeat(); }

        internal static bool QuickTrashRequested(bool control, bool shift)
        {
            bool ctrlTrash = ItemSlot.Options.DisableLeftShiftTrashCan && !PlayerInput.UsingGamepad;
            return !ItemSlot.Options.DisableQuickTrash && (ctrlTrash ? control : shift || ItemSlot.ShiftForcedOn);
        }

        private delegate bool ResearchItem(ref Item item, bool onlyIfComplete);
        private static readonly ResearchItem researchTrash = (ResearchItem)Delegate.CreateDelegate(typeof(ResearchItem),
            typeof(ItemSlot).GetMethod("TryResearchingItem", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Item).MakeByRefType(), typeof(bool) }, null));

        internal bool QuickTrashGesture(Guid id, bool fromOwner, CompanionInventoryArea area, int slot)
        {
            if (!ItemOwnerCurrent() || Main.mouseItem != null && !Main.mouseItem.IsAir) return Result(false, "当前不能整理物品。");
            if (itemDraft != null) return Result(false, "请先放下手中的物品。");
            Actor actor; actors.TryGetValue(id, out actor);
            if (!fromOwner && !ItemActorCurrent(id, actor)) return Result(false, "队友尚未就绪，当前不能交接物品。");
            if (OwnerOperations?.DiscardBlocked == true) return Result(false, "垃圾桶正在使用，请稍后再试。");
            var draft = new CompanionItemDraft(owner, actor?.Body, id, actor?.Generation ?? 0, session, world,
                (isOwner, a, s) => ItemSlotBlocked(isOwner, isOwner ? owner : actor?.Body, a, s), () => OwnerOperations?.DiscardBlocked == true);
            if (!draft.QuickTrash(fromOwner, area, slot)) return Result(false, "该物品当前不能丢弃。");
            List<CompanionItemChange> changes;
            if (!draft.TryPrepare(out changes)) return Result(false, "物品已变化，请重试。");
            Item sourceResearch = changes[0].Before.DeepClone(), oldTrash = owner.trashItem;
            if (!ItemOwnerCurrent() || draft.Session != session || !ReferenceEquals(draft.World, world) ||
                !ReferenceEquals(draft.Owner, owner) || !fromOwner && (!ItemActorCurrent(id, actor) || actor.Generation != draft.Generation || !ReferenceEquals(actor.Body, draft.Companion)) ||
                OwnerOperations?.DiscardBlocked == true || !draft.Validate()) return Result(false, "物品已变化，请重试。");
            // All allocations precede this short same-thread two-endpoint write.
            // A retry may publish/save the new slots, never replay this debit.
            ItemGestureRevision++;
            foreach (var change in changes) change.Apply();
            bool result = FinishItemCommit(actor, changes, "已丢入垃圾桶。");
            try
            {
                // The native quick-trash adjacency consumes detached copies with
                // the REAL owner identity once, only after an accepted commit.
                researchTrash(ref sourceResearch, true); researchTrash(ref oldTrash, false);
                ItemSlot.AnnounceTransfer(new ItemSlot.ItemTransferInfo(owner.trashItem, 0, 6));
            }
            catch (Exception) { Feedback = "已丢入垃圾桶，原版通知未完成。"; }
            return result;
        }

        private bool ItemOwnerCurrent()
        {
            return Thread.CurrentThread.ManagedThreadId == itemThread && !Main.gameMenu && owner != null && owner.active &&
                !owner.dead && !owner.ghost && !Main.LocalPlayerHasPendingInventoryActions() && ReferenceEquals(owner, Main.LocalPlayer) &&
                ReferenceEquals(world, Main.ActiveWorldFileData) && session > 0;
        }

        private bool ItemActorCurrent(Guid id, Actor actor)
        { return actor != null && CanTransfer(id) && !actor.Faulted && !actor.Body.ghost && profiles.OwnsActivation(id, actor.Generation); }

        internal bool ValidateItemGesture()
        {
            if (Thread.CurrentThread.ManagedThreadId != itemThread) return false;
            FlushItemSync();
            var draft = itemDraft;
            if (draft == null) return true;
            Actor actor;
            bool current = ItemOwnerCurrent() && ReferenceEquals(draft.Owner, owner) && draft.Session == session &&
                ReferenceEquals(draft.World, world) && (Main.mouseItem == null || Main.mouseItem.IsAir);
            if (current && draft.UsesCompanion)
                current = actors.TryGetValue(draft.Profile, out actor) && ItemActorCurrent(draft.Profile, actor) &&
                    actor.Generation == draft.Generation && ReferenceEquals(actor.Body, draft.Companion);
            if (current && draft.Validate()) return true;
            CancelItemGesture(); Feedback = "物品已变化，请重新拿取。"; return false;
        }

        internal Item ItemPreview(Guid id, bool fromOwner, CompanionInventoryArea area, int slot)
        {
            if (itemDraft != null && itemDraft.Profile == id)
            { var preview = itemDraft.Preview(fromOwner, area, slot); if (preview != null) return preview; }
            var body = fromOwner ? owner : GetBody(id);
            return CompanionItemRules.ValidSlot(body, area, slot) ? CompanionResources.Items(body, area)[slot] : null;
        }
        internal Item ItemPreview(bool fromOwner, CompanionInventoryArea area, int slot)
        { return itemDraft == null ? null : itemDraft.Preview(fromOwner, area, slot); }

        internal bool ItemGesture(Guid id, bool fromOwner, CompanionInventoryArea area, int slot, CompanionItemGesture gesture)
        {
            if (!ItemOwnerCurrent()) { CancelItemGesture(); return Result(false, "当前不能整理物品。"); }
            if (Main.mouseItem != null && !Main.mouseItem.IsAir) { CancelItemGesture(); return Result(false, "请先放回原版鼠标上的物品。"); }
            if (itemDraft != null && itemDraft.Profile != id) CancelItemGesture();
            if (!ValidateItemGesture()) return false;
            Actor actor; actors.TryGetValue(id, out actor);
            bool usesCompanion = !fromOwner || gesture == CompanionItemGesture.Shift;
            if (usesCompanion && !ItemActorCurrent(id, actor)) return Result(false, "队友尚未就绪，当前不能交接物品。");
            if (gesture == CompanionItemGesture.Shift && itemDraft != null) return Result(false, "请先放下手中的物品。");
            if (itemDraft == null)
                itemDraft = new CompanionItemDraft(owner, actor?.Body, id, actor?.Generation ?? 0, session, world,
                    (isOwner, a, s) => ItemSlotBlocked(isOwner, isOwner ? owner : actor?.Body, a, s));
            // Owner-only organization is allowed with an offline AI. A newly
            // joined AI cannot silently become the target of that older chain.
            if (usesCompanion && (itemDraft.Generation != actor.Generation || !ReferenceEquals(itemDraft.Companion, actor.Body)))
            { CancelItemGesture(); return Result(false, "队友已变化，请重新拿取。"); }
            string message;
            if (!itemDraft.Gesture(fromOwner, area, slot, gesture, out message))
            {
                if (itemDraft.Cursor.IsAir) CancelItemGesture();
                if (message != null) Feedback = message;
                return false;
            }
            ItemGestureRevision++;
            if (!itemDraft.Cursor.IsAir) { Feedback = ""; return true; }
            List<CompanionItemChange> changes;
            if (!itemDraft.TryPrepare(out changes) || !ValidateItemGesture())
            { CancelItemGesture(); return Result(false, "物品已变化，请重新拿取。"); }
            CancelItemGesture();
            // Every replacement is allocated and all identities/finite reads
            // are validated. No callback/I/O runs between these slot writes.
            foreach (var change in changes) change.Apply();
            return FinishItemCommit(actor, changes, message);
        }

        private bool ItemSlotBlocked(bool isOwner, Player body, CompanionInventoryArea area, int slot)
        {
            if (body == null || CompanionItemRules.Blocked(body, area, slot, isOwner ? OwnerOperations : null)) return true;
            if (!isOwner && area == CompanionInventoryArea.Inventory && CompanionProjectileMaterial(body, body.inventory[slot])) return true;
            return isOwner && OwnerItems != null && OwnerItems.CompanionSlotProtected(body, CompanionResources.Items(body, area), slot);
        }

        private bool CompanionProjectileMaterial(Player body, Item item)
        {
            if (item == null || item.IsAir || item.bait <= 0 && item.type != 530 && item.type != 849) return false;
            // Only an active draft requests this finite dependency scan, once
            // per body/world step even when Shift inspects several bait stacks.
            if (!ReferenceEquals(itemMaterialBody, body) || itemMaterialStep != Main.GameUpdateCount)
            {
                itemMaterialBody = body; itemMaterialStep = Main.GameUpdateCount; itemMaterialTypes.Clear();
                foreach (var projectile in Main.projectile)
                {
                    if (projectile == null || !projectile.active || projectile.owner != body.whoAmI) continue;
                    if (projectile.bobber && projectile.localAI[2] > 0) itemMaterialTypes.Add((int)projectile.localAI[2]);
                    if (projectile.type == 651) { itemMaterialTypes.Add(530); itemMaterialTypes.Add(849); }
                }
            }
            return itemMaterialTypes.Contains(item.type);
        }

        private bool FinishItemCommit(Actor actor, List<CompanionItemChange> changes, string message)
        {
            bool companionChanged = false; string failure = null;
            foreach (var change in changes)
            {
                companionChanged |= !change.Owner;
                if (change.Owner && !change.OwnerTrash)
                {
                    try { OwnerItems?.NotifyManualItemReplacement(owner, change.Array, change.Slot, change.Before, change.After); }
                    catch (Exception) { failure = "物品已移动，物品状态通知失败。"; }
                }
                if (Main.netMode == 1) QueueItemSync(change.Owner ? owner : actor.Body, change.Owner ? null : actor,
                    change.OwnerTrash ? PlayerItemSlotID.TrashItem : NetworkSlot(change.Area, change.Slot));
            }
            if (companionChanged && actor != null)
            {
                // Capture failure retains this exact latest body. Retry only
                // captures/sends; it never executes the accepted transfer twice.
                actor.Dirty = true;
                pendingItemCaptures.Add(actor.Profile.Id);
                try { ValidateManualWeapon(actor.Profile.Id); actor.Profile = profiles.CaptureAndSave(actor.Profile.Id, actor.Body, actor.Generation); pendingItemCaptures.Remove(actor.Profile.Id); }
                catch (Exception) { failure = "物品已移动，人物保存失败，请重试保存。"; }
            }
            FlushItemSync(true);
            if (pendingItemSync.Count != 0) failure = "物品已移动，同步未完成。";
            Feedback = failure ?? message ?? "";
            return true;
        }

        internal bool HasPendingItemCapture(Guid id) { return pendingItemCaptures.Contains(id); }
        internal void ItemCaptureSucceeded(Guid id) { pendingItemCaptures.Remove(id); }
        internal bool RetryItemCapture(Guid id)
        {
            Actor actor;
            if (!pendingItemCaptures.Contains(id) || !actors.TryGetValue(id, out actor) || !profiles.OwnsActivation(id, actor.Generation)) return false;
            try { ValidateManualWeapon(id); actor.Profile = profiles.CaptureAndSave(id, actor.Body, actor.Generation); pendingItemCaptures.Remove(id); return Result(true, "正在重试保存。"); }
            catch (Exception) { return Result(false, "人物保存失败，请重试保存。"); }
        }

        private void QueueItemSync(Player body, Actor actor, int slot)
        {
            for (int i = pendingItemSync.Count - 1; i >= 0; i--)
                if (ReferenceEquals(pendingItemSync[i].Body, body) && pendingItemSync[i].Slot == slot) pendingItemSync.RemoveAt(i);
            pendingItemSync.Add(new ItemSync { Body = body, Actor = actor, Session = session, Generation = actor?.Generation ?? 0, World = world, Slot = slot,
                MainConnection = Netplay.Connection, MainSocket = Netplay.Connection?.Socket, ActorConnection = actor?.Connection });
        }
        private void FlushItemSync(bool immediate = false)
        {
            if (pendingItemSync.Count == 0 || !immediate && Main.GameUpdateCount < nextItemSync) return;
            nextItemSync = Main.GameUpdateCount + 60;
            for (int i = pendingItemSync.Count - 1; i >= 0; i--)
            {
                var pending = pendingItemSync[i];
                bool current = Main.netMode == 1 && pending.Session == session && ReferenceEquals(pending.World, world) &&
                    ReferenceEquals(world, Main.ActiveWorldFileData) && ReferenceEquals(owner, Main.LocalPlayer) &&
                    ReferenceEquals(pending.MainConnection, Netplay.Connection) && ReferenceEquals(pending.MainSocket, Netplay.Connection?.Socket);
                if (pending.Actor != null) current &= pending.Actor.Ready && !pending.Actor.Closing && pending.Actor.Generation == pending.Generation &&
                    ReferenceEquals(pending.Actor.Body, pending.Body) && ReferenceEquals(pending.ActorConnection, pending.Actor.Connection) &&
                    profiles.OwnsActivation(pending.Actor.Profile.Id, pending.Generation);
                else current &= ReferenceEquals(pending.Body, owner);
                if (!current) { pendingItemSync.RemoveAt(i); continue; }
                // SendData silently returns without sending when the primary
                // socket is down; CompanionConnection likewise drops terminal
                // sends. Neither return is a successful publication or ACK.
                if (Netplay.Connection == null || !Netplay.Connection.IsConnected() ||
                    pending.Actor != null && (pending.Actor.Connection == null || pending.Actor.Connection.Finished)) continue;
                try
                {
                    if (pending.Actor == null) NetMessage.SendData(5, -1, -1, null, pending.Body.whoAmI, pending.Slot);
                    else using (CompanionHooks.Enter(pending.Actor, pending.Actor.Aim)) NetMessage.SendData(5, -1, -1, null, pending.Body.whoAmI, pending.Slot);
                    pendingItemSync.RemoveAt(i);
                }
                catch (Exception) { /* Latest real slots remain authoritative for the bounded retry. */ }
            }
        }

        // Preserve existing internal command consumers; the actual window now
        // owns only the gesture chain. Legacy calls also notify/publish changes.
        internal bool Transfer(Guid id, bool fromOwner, CompanionInventoryArea area, int slot, bool wholeStack)
        { return Resource(id, a => CompanionResources.Transfer(owner, a.Body, fromOwner, area, slot, wholeStack, OwnerOperations)); }
        internal bool Equip(Guid id, bool fromOwner, int inventorySlot)
        { return Resource(id, a => CompanionResources.Equip(owner, a.Body, fromOwner, inventorySlot, OwnerOperations)); }
        internal bool Unequip(Guid id, int armorSlot) { return Unequip(id, CompanionInventoryArea.Armor, armorSlot); }
        internal bool Unequip(Guid id, CompanionInventoryArea area, int slot)
        { return Resource(id, a => CompanionResources.Unequip(owner, a.Body, area, slot, OwnerOperations)); }
        private bool Resource(Guid id, Func<Actor, CompanionResourceResult> command)
        {
            Actor actor;
            if (!ItemOwnerCurrent() || !actors.TryGetValue(id, out actor) || !ItemActorCurrent(id, actor)) return Result(false, "队友尚未就绪，当前不能交接物品。");
            if (Main.mouseItem != null && !Main.mouseItem.IsAir) return Result(false, "请先放回原版鼠标上的物品。");
            CancelItemGesture();
            var snapshots = new List<CompanionItemChange>(); var values = new List<Item>();
            for (int side = 0; side < 2; side++) foreach (CompanionInventoryArea area in Enum.GetValues(typeof(CompanionInventoryArea)))
            {
                var array = CompanionResources.Items(side == 0 ? owner : actor.Body, area);
                for (int slot = 0; slot < array.Length && (area != CompanionInventoryArea.Inventory || slot < 58); slot++)
                { snapshots.Add(new CompanionItemChange { Owner = side == 0, Area = area, Slot = slot, Array = array, Before = array[slot] }); values.Add(array[slot].Clone()); }
            }
            try
            {
                var result = command(actor); if (!result.Changed) return Result(false, result.Message);
                var changes = new List<CompanionItemChange>();
                for (int i = 0; i < snapshots.Count; i++)
                    if (snapshots[i].Array[snapshots[i].Slot].IsNetStateDifferent(values[i]))
                    { snapshots[i].After = snapshots[i].Array[snapshots[i].Slot]; changes.Add(snapshots[i]); }
                return FinishItemCommit(actor, changes, result.Message);
            }
            catch (Exception e) { return Result(false, e.Message); }
        }
    }
}
