using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed class CompanionItemChange
    {
        internal bool Owner;
        internal CompanionInventoryArea Area;
        internal int Slot;
        internal int StoredLoadout = -1;
        internal Item[] Array;
        internal Item Before, After;
        internal Player TrashOwner;
        internal bool OwnerTrash { get { return TrashOwner != null; } }
        internal void Apply()
        { if (OwnerTrash) TrashOwner.trashItem = After; else Array[Slot] = After; }
    }

    // This is a bounded projection, not an inventory owner. Until Cursor is Air
    // every real Item remains in its original authority. No abort writes Items.
    internal sealed class CompanionItemDraft
    {
        private sealed class Cell
        {
            internal bool Owner;
            internal CompanionInventoryArea Area;
            internal int Slot;
            internal int StoredLoadout = -1;
            internal Item[] Array;
            internal Item Original, Baseline, Preview, InPlaceModeItem;
            internal bool Written;
            internal bool OwnerTrash;
        }

        internal readonly Player Owner, Companion;
        internal readonly Guid Profile;
        internal readonly long Generation, Session;
        internal readonly object World;
        private readonly List<Cell> cells = new List<Cell>();
        internal Item Cursor { get; private set; } = new Item();
        internal int DependencyCount { get { return cells.Count; } }
        internal bool DependsOn(Item[] array, int slot)
        {
            foreach (var cell in cells)
                if (ReferenceEquals(cell.Array, array) && cell.Slot == slot) return true;
            return false;
        }
        internal bool UsesCompanion { get; private set; }
        internal bool InvalidBank { get; private set; }
        internal readonly Func<bool, CompanionInventoryArea, int, bool> Blocked;
        private readonly Func<bool> discardBlocked;
        private readonly CompanionInventoryArea transferTarget;
        private readonly Func<CompanionInventoryArea, bool> bankCurrent;

        internal CompanionItemDraft(Player owner, Player companion, Guid profile, long generation, long session,
            object world, Func<bool, CompanionInventoryArea, int, bool> blocked, Func<bool> discardBlocked = null,
            CompanionInventoryArea transferTarget = CompanionInventoryArea.Inventory, Func<CompanionInventoryArea, bool> bankCurrent = null)
        {
            Owner = owner; Companion = companion; Profile = profile; Generation = generation; Session = session; World = world;
            Blocked = blocked; this.discardBlocked = discardBlocked; this.transferTarget = transferTarget; this.bankCurrent = bankCurrent;
        }

        private bool BankAvailable(bool owner, CompanionInventoryArea area)
        { return !CompanionResources.IsBank(area) || !owner && bankCurrent?.Invoke(area) == true; }

        private Player Body(bool owner) { return owner ? Owner : Companion; }
        private Cell Find(bool owner, CompanionInventoryArea area, int slot)
        {
            foreach (var cell in cells) if (!cell.OwnerTrash && cell.StoredLoadout < 0 && cell.Owner == owner && cell.Area == area && cell.Slot == slot) return cell;
            return null;
        }
        private Cell Touch(bool owner, CompanionInventoryArea area, int slot)
        {
            var cell = Find(owner, area, slot);
            if (cell != null) return cell;
            if (!CompanionItemRules.ValidSlot(Body(owner), area, slot)) return null;
            var array = CompanionResources.Items(Body(owner), area);
            var original = array[slot];
            cell = new Cell { Owner = owner, Area = area, Slot = slot, Array = array, Original = original,
                Baseline = original?.Clone() ?? new Item(), Preview = original?.Clone() ?? new Item() };
            cells.Add(cell); UsesCompanion |= !owner;
            return cell;
        }
        internal Item Preview(bool owner, CompanionInventoryArea area, int slot)
        {
            var cell = Find(owner, area, slot);
            return cell != null ? cell.Preview : CompanionItemRules.ValidSlot(Body(owner), area, slot) ? CompanionResources.Items(Body(owner), area)[slot] : null;
        }

        // Native networking persists type/stack/prefix/favorite. Classification
        // fields are also dependencies because slot legality and capacity read
        // them. Object AND array identity rejects same-type Clone/receiver writes.
        private static bool Same(Item current, Item baseline)
        {
            if (current == null) return baseline.IsAir;
            return !current.IsNetStateDifferent(baseline) && current.maxStack == baseline.maxStack &&
                current.accessory == baseline.accessory && current.vanity == baseline.vanity && current.headSlot == baseline.headSlot &&
                current.bodySlot == baseline.bodySlot && current.legSlot == baseline.legSlot && current.mountType == baseline.mountType &&
                current.buffType == baseline.buffType && current.shoot == baseline.shoot && current.ammo == baseline.ammo &&
                current.dye == baseline.dye && current.bait == baseline.bait && current.paint == baseline.paint &&
                current.useAmmo == baseline.useAmmo && current.tileWand == baseline.tileWand;
        }
        internal bool Validate()
        {
            InvalidBank = false;
            CompanionInventoryArea checkedBank = CompanionInventoryArea.Inventory;
            foreach (var cell in cells)
            {
                if (cell.StoredLoadout >= 0)
                {
                    var loadout = Body(cell.Owner).Loadouts[cell.StoredLoadout];
                    if (!ReferenceEquals(cell.Array, cell.Area == CompanionInventoryArea.Armor ? loadout.Armor : loadout.Dye) ||
                        !ReferenceEquals(cell.Array[cell.Slot], cell.Original) || !Same(cell.Original, cell.Baseline)) return false;
                    continue;
                }
                if (cell.OwnerTrash)
                {
                    if (discardBlocked?.Invoke() == true || !ReferenceEquals(Owner.trashItem, cell.Original) || !Same(Owner.trashItem, cell.Baseline)) return false;
                    continue;
                }
                if (CompanionResources.IsBank(cell.Area) && (cell.Owner || cell.Area != checkedBank))
                {
                    if (!BankAvailable(cell.Owner, cell.Area)) { InvalidBank = true; return false; }
                    checkedBank = cell.Area;
                }
                if (!CompanionItemRules.ValidSlot(Body(cell.Owner), cell.Area, cell.Slot) ||
                    !ReferenceEquals(CompanionResources.Items(Body(cell.Owner), cell.Area), cell.Array) ||
                    !ReferenceEquals(cell.Array[cell.Slot], cell.Original) || !Same(cell.Array[cell.Slot], cell.Baseline)) return false;
                if (!cell.Written) continue;
                if (Blocked(cell.Owner, cell.Area, cell.Slot)) return false;
                if (!CompanionItemRules.Available(Body(cell.Owner), cell.Area, cell.Slot) &&
                    (!cell.Preview.IsAir || cell.Baseline.IsAir)) return false;
            }
            return true;
        }

        // Trash is a real scalar endpoint, never an array wrapping its old value.
        // Both guards and both reference/value baselines survive through prepare.
        internal bool QuickTrash(bool fromOwner, CompanionInventoryArea area, int slot)
        {
            if (cells.Count != 0 || !Cursor.IsAir || area != CompanionInventoryArea.Inventory && !CompanionResources.IsBank(area) ||
                !BankAvailable(fromOwner, area) ||
                discardBlocked?.Invoke() == true || !CompanionItemRules.Available(Body(fromOwner), area, slot) || Blocked(fromOwner, area, slot)) return false;
            var source = Touch(fromOwner, area, slot);
            if (source.Preview.IsAir || source.Preview.favorited) return false;
            var original = Owner.trashItem;
            cells.Add(new Cell { Owner = true, OwnerTrash = true, Original = original,
                Baseline = original?.Clone() ?? new Item(), Preview = source.Preview.Clone(), Written = true });
            source.Preview = new Item(); source.Written = true;
            return true;
        }

        private void LegalityDependencies(bool owner, CompanionInventoryArea area, int slot)
        {
            if (area != CompanionInventoryArea.Armor) return;
            // Only the opposite armor group or the two accessory groups affect
            // this placement; an unrelated backpack update cannot cancel it.
            if (slot % 10 < 3)
            {
                int first = slot < 10 ? 10 : 0;
                for (int i = first; i < first + 3; i++) Touch(owner, area, i);
            }
            else for (int i = 3; i < 10; i++) { Touch(owner, area, i); Touch(owner, area, i + 10); }
        }
        private bool Accepts(bool owner, CompanionInventoryArea area, int slot, Item item)
        {
            if (item != null && !item.IsAir) LegalityDependencies(owner, area, slot);
            return CompanionItemRules.Accepts(Body(owner), area, slot, item, (a, s) => Preview(owner, a, s));
        }

        internal bool Gesture(bool owner, CompanionInventoryArea area, int slot, CompanionItemGesture gesture, int take, out string message)
        {
            int count = cells.Count;
            bool accepted = GestureCore(owner, area, slot, gesture, take, out message);
            if (!accepted && cells.Count > count) cells.RemoveRange(count, cells.Count - count);
            UsesCompanion = false; foreach (var cell in cells) UsesCompanion |= !cell.Owner;
            return accepted;
        }

        private bool GestureCore(bool owner, CompanionInventoryArea area, int slot, CompanionItemGesture gesture, int take, out string message)
        {
            message = null;
            if (!BankAvailable(owner, area) || !CompanionItemRules.ValidSlot(Body(owner), area, slot)) { message = "该槽位当前不可用。"; return false; }
            bool locked = !CompanionItemRules.Available(Body(owner), area, slot);
            // Difficulty can make an occupied slot unusable. Its original
            // equipment remains recoverable, but the slot cannot take a write.
            if (locked && (!Cursor.IsAir || Preview(owner, area, slot)?.IsAir != false))
            { message = "该槽位当前只可取回已有装备。"; return false; }
            if (Blocked(owner, area, slot)) { message = "该物品正在使用，请先结束相关操作。"; return false; }
            var cell = Touch(owner, area, slot);
            if (gesture == CompanionItemGesture.Shift) return QuickTransfer(owner, cell, locked, out message);
            if (gesture == CompanionItemGesture.Mark)
            {
                if (locked || cell.Preview.IsAir || !CompanionItemRules.CanMark(area)) return false;
                cell.Preview.favorited = !cell.Preview.favorited;
                cell.Written = true;
                if (cell.Preview.favorited && (area == CompanionInventoryArea.Armor || area == CompanionInventoryArea.Dye))
                    ClearOtherLoadoutMarks(cell);
                return true;
            }
            if (gesture == CompanionItemGesture.Right || gesture == CompanionItemGesture.RightRepeat)
            {
                var action = CompanionSlotRightClick.Classify(area, slot, cell.Preview, Cursor);
                if (action == CompanionSlotRightClick.Kind.Mode && !locked)
                {
                    if (gesture == CompanionItemGesture.RightRepeat) return false;
                    if (!cell.Preview.IsNotTheSameAs(cell.Baseline)) cell.InPlaceModeItem = cell.Preview;
                    CompanionSlotRightClick.ChangeMode(cell.Preview);
                    cell.Written = true;
                    return true;
                }
                if (action != CompanionSlotRightClick.Kind.Pickup && !locked)
                {
                    if (gesture == CompanionItemGesture.Right) message = "请先放下手中的物品。";
                    return false;
                }
                // ItemSlot.RightClick always takes from the pointed stack. A
                // new press, another compatible stack or an empty cell never
                // reverses direction. Left-click places the projected cursor.
                if (cell.Preview.IsAir || !Cursor.IsAir && !Item.CanStack(Cursor, cell.Preview)) return false;
                int capacity = Cursor.IsAir ? cell.Preview.maxStack : Cursor.maxStack - Cursor.stack;
                int amount = Math.Min(cell.Preview.stack, Math.Min(capacity, locked ? cell.Preview.stack : take));
                if (amount <= 0) return false;
                if (Cursor.IsAir)
                {
                    Cursor = cell.Preview.Clone();
                    Cursor.stack = 0;
                    if (cell.Preview.stack != 1 || CompanionItemRules.EquipmentContext(area, slot) >= 0) Cursor.favorited = false;
                }
                Cursor.stack += amount;
                cell.Preview = Remainder(cell.Preview, amount);
                cell.Written = true;
                return true;
            }
            if (Cursor.IsAir)
            {
                if (cell.Preview.IsAir) return false;
                Cursor = cell.Preview;
                if (CompanionItemRules.EquipmentContext(area, slot) >= 0) Cursor.favorited = false;
                cell.Preview = new Item(); cell.Written = true;
                return true;
            }
            if (cell.Preview.IsAir || Item.CanStack(Cursor, cell.Preview))
            {
                int capacity = Math.Max(0, CompanionItemRules.Capacity(area, Cursor) - (cell.Preview.IsAir ? 0 : cell.Preview.stack));
                int count = Math.Min(Cursor.stack, capacity);
                if (count == 0) return false;
                var next = cell.Preview.IsAir ? Cursor.Clone() : cell.Preview.Clone();
                next.stack = (cell.Preview.IsAir ? 0 : cell.Preview.stack) + count;
                if (!Accepts(owner, area, slot, next)) { message = "此物品不能放入该槽位。"; return false; }
                if (CompanionItemRules.EquipmentContext(area, slot) >= 0) next.favorited = false;
                cell.Preview = next; Cursor = Remainder(Cursor, count); cell.Written = true;
                return true;
            }
            if (!Accepts(owner, area, slot, Cursor)) { message = "此物品不能放入该槽位。"; return false; }
            // Swaps can leave an unrelated item on this window's cursor. Do not
            // push it back into the first source (which could be a helmet slot).
            var previous = cell.Preview; cell.Preview = Cursor; Cursor = previous; cell.Written = true;
            // Vanilla clears both ends of an equipment swap. On armor and
            // dye, that bit means shared loadout ownership, not a backpack mark.
            if (CompanionItemRules.EquipmentContext(area, slot) >= 0) cell.Preview.favorited = Cursor.favorited = false;
            return true;
        }

        // Vanilla uses the same bit for a backpack favorite and loadout
        // sharing. Only enabling an equipped shared slot clears this person's
        // stored counterpart in every loadout. Bind the actual arrays/items
        // until the cursor closes; a cancelled draft changes none of them.
        private void ClearOtherLoadoutMarks(Cell equipped)
        {
            var loadouts = Body(equipped.Owner).Loadouts;
            for (int i = 0; i < loadouts.Length; i++)
            {
                var array = equipped.Area == CompanionInventoryArea.Armor ? loadouts[i].Armor : loadouts[i].Dye;
                if (DependsOn(array, equipped.Slot)) continue;
                var original = array[equipped.Slot];
                var cell = new Cell { Owner = equipped.Owner, Area = equipped.Area, Slot = equipped.Slot, StoredLoadout = i,
                    Array = array, Original = original, Preview = original?.Clone() ?? new Item(), Baseline = original?.Clone() ?? new Item(),
                    Written = original?.favorited == true };
                cell.Preview.favorited = false;
                cells.Add(cell);
            }
        }

        private bool QuickTransfer(bool fromOwner, Cell source, bool locked, out string message)
        {
            message = null;
            if (!Cursor.IsAir) { message = "请先放下手中的物品。"; return false; }
            if (source.Preview.IsAir) return false;
            if (Body(!fromOwner) == null) { message = "队友当前不能交接物品。"; return false; }
            var targetArea = fromOwner ? transferTarget : CompanionInventoryArea.Inventory;
            if (!BankAvailable(!fromOwner, targetArea)) { message = "当前储存入口已失效。"; return false; }
            int countSlots = targetArea == CompanionInventoryArea.Inventory ? 50 : CompanionResources.Items(Body(!fromOwner), targetArea).Length;
            int requested = source.Preview.stack, remaining = requested;
            var placements = new List<Tuple<int, Item>>();
            for (int pass = 0; pass < 2 && remaining > 0; pass++)
                for (int slot = 0; slot < countSlots && remaining > 0; slot++)
                {
                    if (Blocked(!fromOwner, targetArea, slot)) continue;
                    Item target = Preview(!fromOwner, targetArea, slot);
                    if (pass == 0 ? target.IsAir || !Item.CanStack(source.Preview, target) : !target.IsAir) continue;
                    int count = Math.Min(remaining, Math.Max(0, source.Preview.maxStack - (target.IsAir ? 0 : target.stack)));
                    if (count == 0) continue;
                    Item replacement = target.IsAir ? source.Preview.Clone() : target.Clone(); replacement.stack = (target.IsAir ? 0 : target.stack) + count;
                    if (target.IsAir && CompanionItemRules.EquipmentContext(source.Area, source.Slot) >= 0) replacement.favorited = false;
                    placements.Add(Tuple.Create(slot, replacement)); remaining -= count;
                }
            if (remaining == requested) { message = "接收方背包没有可用空间。"; return false; }
            if (locked && remaining != 0) { message = "请腾出空间以取回这组装备。"; return false; }
            foreach (var placement in placements)
            {
                var cell = Touch(!fromOwner, targetArea, placement.Item1);
                cell.Preview = placement.Item2; cell.Written = true;
            }
            source.Preview = Remainder(source.Preview, requested - remaining); source.Written = true;
            message = remaining == 0 ? "已转移。" : "背包空间有限，已转移部分物品。";
            return true;
        }

        private static Item Remainder(Item item, int removed)
        { if (removed >= item.stack) return new Item(); var copy = item.Clone(); copy.stack -= removed; return copy; }

        // Bulk operations calculate over one detached projection per endpoint.
        // Capturing each consulted slot makes capacity and ordering real
        // dependencies even when a particular slot ends up byte-for-byte equal.
        internal Item[] Project(bool owner, CompanionInventoryArea area, int count)
        {
            var projection = new Item[count];
            for (int slot = 0; slot < count; slot++) projection[slot] = Touch(owner, area, slot).Preview;
            return projection;
        }

        internal void Stage(bool owner, CompanionInventoryArea area, Item[] projection)
        {
            for (int slot = 0; slot < projection.Length; slot++)
            {
                var cell = Find(owner, area, slot);
                if (Same(projection[slot], cell.Baseline)) continue;
                cell.Preview = projection[slot];
                cell.Written = true;
            }
        }

        internal bool TryPrepare(out List<CompanionItemChange> changes)
        {
            changes = new List<CompanionItemChange>();
            if (!Cursor.IsAir || !Validate()) return false;
            for (int index = 0; index < cells.Count; index++)
            {
                var cell = cells[index];
                if (!cell.Written || !cell.OwnerTrash && Same(cell.Preview, cell.Baseline)) continue;
                // An in-place native mode change (e.g. a previously favorited
                // bag in its own vault) has no new placement destination.
                if (!cell.OwnerTrash && cell.StoredLoadout < 0 && !ReferenceEquals(cell.Preview, cell.InPlaceModeItem) && cell.Preview.IsNotTheSameAs(cell.Baseline) &&
                    !Accepts(cell.Owner, cell.Area, cell.Slot, cell.Preview)) return false;
                changes.Add(new CompanionItemChange { Owner = cell.Owner, Area = cell.Area, Slot = cell.Slot, Array = cell.Array,
                    StoredLoadout = cell.StoredLoadout, Before = cell.Original, After = cell.Preview.Clone(), TrashOwner = cell.OwnerTrash ? Owner : null });
            }
            // Accepts can add read dependencies. Recheck those before exposing
            // any replacement; no disk/network/native callback belongs here.
            return Validate();
        }
    }
}
