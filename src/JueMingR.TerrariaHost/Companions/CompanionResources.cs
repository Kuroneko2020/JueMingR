using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.ID;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed class CompanionResourceResult
    {
        internal CompanionResourceResult(int moved, string message) { Moved = moved; Message = message; }
        internal int Moved { get; }
        internal string Message { get; }
        internal bool Changed { get { return Moved > 0; } }
    }

    // All entry points run on the game thread after this frame's native use has
    // settled. UI sends a slot, never its stale item/quantity snapshot. The caller
    // validates owner/world/activation and synchronizes both native identities.
    internal static class CompanionResources
    {
        internal static Item[] Items(Player body, CompanionInventoryArea area)
        {
            switch (area)
            {
                case CompanionInventoryArea.Inventory: return body.inventory;
                case CompanionInventoryArea.Armor: return body.armor;
                case CompanionInventoryArea.Dye: return body.dye;
                case CompanionInventoryArea.Misc: return body.miscEquips;
                case CompanionInventoryArea.MiscDye: return body.miscDyes;
                default: throw new ArgumentOutOfRangeException(nameof(area));
            }
        }
        internal static CompanionResourceResult Transfer(Player owner, Player companion, bool fromOwner,
            CompanionInventoryArea area, int slot, bool wholeStack, ItemOperationOwnership ownerOperations,
            Func<Player, Item[], int, bool> supplyBlocked = null)
        {
            if (!Usable(owner, companion)) return Failed("队友当前不能交接物品。");
            Player source = fromOwner ? owner : companion, destination = fromOwner ? companion : owner;
            if (fromOwner && area != CompanionInventoryArea.Inventory) return Failed("请从自己的背包选择物品。");
            Item[] inventory = Items(source, area);
            if (!ValidSlot(area, inventory, slot)) return Failed("物品槽位已失效。");
            Item item = inventory[slot];
            if (item == null || item.IsAir || item.stack <= 0) return Failed("该槽位没有物品。");
            if (Blocked(source, area, slot, fromOwner ? ownerOperations : null, supplyBlocked)) return Failed("该物品正在使用，请先结束相关操作。");
            int requested = wholeStack ? item.stack : 1;
            bool locked = !CompanionItemRules.Available(source, area, slot);
            if (locked && requested != item.stack) return Failed("该槽位当前只可取回整组装备。");
            int moved = MoveToInventory(item, destination, requested, fromOwner ? null : ownerOperations, supplyBlocked,
                clearFavorite: area != CompanionInventoryArea.Inventory, requireFullStack: locked);
            if (moved == 0) return Failed("接收方背包没有可用空间，物品未移动。");
            return new CompanionResourceResult(moved, moved == requested ? "已转移 " + moved + " 个。" : "背包空间有限，已转移 " + moved + " 个。");
        }
        internal static CompanionResourceResult Equip(Player owner, Player companion, bool fromOwner,
            int inventorySlot, ItemOperationOwnership ownerOperations, Func<Player, Item[], int, bool> supplyBlocked = null)
        {
            if (!Usable(owner, companion)) return Failed("队友当前不能调整装备。");
            Player source = fromOwner ? owner : companion;
            if (inventorySlot < 0 || inventorySlot >= 50) return Failed("请选择背包中的装备。");
            var item = source.inventory[inventorySlot];
            if (item == null || item.IsAir) return Failed("该槽位没有物品。");
            if (Blocked(source, CompanionInventoryArea.Inventory, inventorySlot, fromOwner ? ownerOperations : null, supplyBlocked)) return Failed("该物品正在使用，请先结束相关操作。");
            CompanionInventoryArea area; int target = EquipmentSlot(companion, item, out area);
            if (target < 0) return Failed("此物品当前没有可用装备槽位。");
            if (!CompanionItemRules.Available(companion, area, target) || Blocked(companion, area, target, null, supplyBlocked))
                return Failed("当前装备槽不可用，请稍后再试。");
            var equipment = Items(companion, area);
            Item previous = equipment[target];
            if (!previous.IsAir && item.stack > 1) return Failed("请先卸下该槽位的装备，再放入这一组物品。");
            Item equipped = item.Clone(); equipped.stack = 1; equipped.favorited = false;
            if (item.stack == 1)
            {
                source.inventory[inventorySlot] = previous.Clone();
                source.inventory[inventorySlot].favorited = false;
            }
            else { item.stack--; }
            equipment[target] = equipped;
            // Do not call UpdateEquips in isolation: it accumulates derived stats.
            // The normal once-per-tick Player.Update resets and applies equipment.
            return new CompanionResourceResult(1, "已装备。");
        }
        internal static CompanionResourceResult Unequip(Player owner, Player companion, int armorSlot,
            ItemOperationOwnership ownerOperations)
        { return Unequip(owner, companion, CompanionInventoryArea.Armor, armorSlot, ownerOperations); }
        internal static CompanionResourceResult Unequip(Player owner, Player companion, CompanionInventoryArea area, int slot,
            ItemOperationOwnership ownerOperations, Func<Player, Item[], int, bool> supplyBlocked = null)
        {
            if (!Usable(owner, companion) || (area != CompanionInventoryArea.Armor && area != CompanionInventoryArea.Dye &&
                area != CompanionInventoryArea.Misc && area != CompanionInventoryArea.MiscDye)) return Failed("装备槽位不可用。");
            // Preserve the actual selected native array: a dye or pet slot with
            // index 0 must never be interpreted as armor[0]. All accepted equip
            // areas return through the same capacity/conservation operation.
            var equipment = Items(companion, area);
            if (!ValidSlot(area, equipment, slot)) return Failed("装备槽位不可用。");
            var item = equipment[slot];
            if (item == null || item.IsAir) return Failed("该槽位没有装备。");
            if (Blocked(companion, area, slot, null, supplyBlocked)) return Failed("当前装备参与正在进行的用物，请稍后再试。");
            int moved = MoveToInventory(item, companion, item.stack, null, supplyBlocked, clearFavorite: true,
                requireFullStack: !CompanionItemRules.Available(companion, area, slot));
            return moved == 0 ? Failed("队友背包没有可用空间，装备未卸下。") : new CompanionResourceResult(moved, "已卸下到队友背包。");
        }
        private static bool Usable(Player owner, Player companion)
        { return owner != null && companion != null && !ReferenceEquals(owner, companion) && owner.active && companion.active && !owner.dead && !companion.dead; }
        private static bool ValidSlot(CompanionInventoryArea area, Item[] inventory, int slot)
        { return slot >= 0 && slot < inventory.Length && (area != CompanionInventoryArea.Inventory || slot < 58); }
        private static bool Blocked(Player body, CompanionInventoryArea area, int slot, ItemOperationOwnership operations,
            Func<Player, Item[], int, bool> supplyBlocked)
        {
            return CompanionItemRules.Blocked(body, area, slot, operations) || supplyBlocked?.Invoke(body, Items(body, area), slot) == true;
        }
        private sealed class Placement
        { internal int Slot; internal Item NewItem; }
        private static int MoveToInventory(Item source, Player destination, int requested, ItemOperationOwnership operations,
            Func<Player, Item[], int, bool> supplyBlocked, bool clearFavorite = false, bool requireFullStack = false)
        {
            int remaining = Math.Min(source.stack, requested);
            var placements = new List<Placement>();
            // Same-stack receipt first. Empty dedicated ammo/coin slots are
            // eligible only for their actual native item category.
            for (int pass = 0; pass < 2 && remaining > 0; pass++)
                for (int slot = 0; slot < 58 && remaining > 0; slot++)
                {
                    Item target = destination.inventory[slot];
                    if (ReferenceEquals(source, target) || Blocked(destination, CompanionInventoryArea.Inventory, slot, operations, supplyBlocked)) continue;
                    if (slot >= 50 && slot < 54 && !source.IsACoin || slot >= 54 && !source.FitsAmmoSlot()) continue;
                    if (pass == 0 && (target.IsAir || !Item.CanStack(source, target))) continue;
                    if (pass == 1 && !target.IsAir) continue;
                    int capacity = Math.Max(0, source.maxStack - (target.IsAir ? 0 : target.stack));
                    int count = Math.Min(remaining, capacity);
                    if (count == 0) continue;
                    Item replacement = target.IsAir ? source.Clone() : target.Clone();
                    if (target.IsAir && clearFavorite) replacement.favorited = false;
                    replacement.stack = (target.IsAir ? 0 : target.stack) + count;
                    placements.Add(new Placement { Slot = slot, NewItem = replacement }); remaining -= count;
                }
            int moved = Math.Min(source.stack, requested) - remaining;
            if (moved == 0 || requireFullStack && remaining != 0) return 0;
            // Allocate and validate before either side changes. No native use,
            // disk I/O or network callback runs between these two local commits.
            foreach (var placement in placements) destination.inventory[placement.Slot] = placement.NewItem;
            source.stack -= moved; if (source.stack <= 0) source.TurnToAir(); return moved;
        }
        private static int EquipmentSlot(Player body, Item item, out CompanionInventoryArea area)
        {
            area = CompanionInventoryArea.Armor;
            int slot = item.headSlot >= 0 ? 0 : item.bodySlot >= 0 ? 1 : item.legSlot >= 0 ? 2 : -1;
            if (slot >= 0)
            {
                // Native CanEquipInArmorSlot's armor branch uses Main.LocalPlayer
                // for vanity duplicate checks. Supply this body's real arrays.
                if (!ItemID.Sets.DualEquipArmor[item.type] && ItemSlot.HasSameItemInSlot(item, item.vanity ? ItemSlot.ArmorSlots(body) : ItemSlot.VanityArmorSlots(body))) return -1;
                return slot + (item.vanity ? 10 : 0);
            }
            if (item.accessory)
            {
                if (ItemSlot.HasSameItemInSlot(item, item.vanity ? ItemSlot.AccSlots(body) : ItemSlot.VanityAccSlots(body))) return -1;
                int replace = -1;
                for (int i = item.vanity ? 13 : 3; i < (item.vanity ? 20 : 10); i++)
                    if (body.IsItemSlotUnlockedAndUsable(i) && ItemSlot.CanEquipAccessoryInSlot(item, item.vanity, item.vanity ? ItemSlot.VanityAccSlots(body) : ItemSlot.AccSlots(body), i))
                    { if (body.armor[i].IsAir) return i; if (replace < 0) replace = i; }
                return replace;
            }
            if (item.dye > 0)
            {
                area = CompanionInventoryArea.Dye;
                for (int i = 0; i < body.dye.Length; i++) if (body.IsItemSlotUnlockedAndUsable(i) && body.dye[i].IsAir) return i;
                return 0;
            }
            area = CompanionInventoryArea.Misc;
            if (item.mountType >= 0) return MountID.Sets.Cart[item.mountType] ? 2 : 3;
            if (item.shoot > 0 && Main.projHook[item.shoot]) return 4;
            if (item.buffType > 0 && Main.lightPet[item.buffType]) return 1;
            if (item.buffType > 0 && Main.vanityPet[item.buffType]) return 0;
            return -1;
        }
        private static CompanionResourceResult Failed(string message) { return new CompanionResourceResult(0, message); }
    }
}
