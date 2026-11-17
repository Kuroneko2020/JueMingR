using System;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.ID;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum CompanionItemGesture { Left, Right, Shift }

    // Independent panel operations must name the target body. ItemSlot's
    // convenient movement entry points use LocalPlayer and mouseItem internally.
    internal static class CompanionItemRules
    {
        internal static bool ValidSlot(Player body, CompanionInventoryArea area, int slot)
        {
            if (body == null || slot < 0) return false;
            var items = CompanionResources.Items(body, area);
            return items != null && slot < items.Length && (area != CompanionInventoryArea.Inventory || slot < 58);
        }

        internal static bool Available(Player body, CompanionInventoryArea area, int slot)
        {
            if (!ValidSlot(body, area, slot)) return false;
            return (area != CompanionInventoryArea.Armor && area != CompanionInventoryArea.Dye) || body.IsItemSlotUnlockedAndUsable(slot);
        }

        internal static int Capacity(CompanionInventoryArea area, Item item)
        { return area == CompanionInventoryArea.Inventory ? item.maxStack : 1; }

        internal static bool Accepts(Player body, CompanionInventoryArea area, int slot, Item item,
            Func<CompanionInventoryArea, int, Item> read)
        {
            if (!Available(body, area, slot)) return false;
            if (item == null || item.IsAir) return true;
            if (item.stack <= 0 || item.stack > Capacity(area, item)) return false;
            switch (area)
            {
                case CompanionInventoryArea.Inventory:
                    return slot < 50 || slot < 54 ? slot < 50 || item.IsACoin : item.FitsAmmoSlot();
                case CompanionInventoryArea.Dye:
                case CompanionInventoryArea.MiscDye:
                    return item.dye > 0;
                case CompanionInventoryArea.Misc:
                    switch (slot)
                    {
                        case 0: return item.buffType > 0 && Main.vanityPet[item.buffType] && !Main.lightPet[item.buffType];
                        case 1: return item.buffType > 0 && Main.lightPet[item.buffType];
                        case 2: return item.mountType >= 0 && MountID.Sets.Cart[item.mountType];
                        case 3: return item.mountType >= 0 && !MountID.Sets.Cart[item.mountType];
                        case 4: return item.shoot > 0 && Main.projHook[item.shoot];
                        default: return false;
                    }
                case CompanionInventoryArea.Armor:
                    int position = slot % 10;
                    if (position < 3)
                    {
                        if (position == 0 ? item.headSlot < 0 : position == 1 ? item.bodySlot < 0 : item.legSlot < 0) return false;
                        if (ItemID.Sets.DualEquipArmor[item.type]) return true;
                        int first = slot < 10 ? 10 : 0;
                        for (int i = first; i < first + 3; i++)
                            if (SameOrIncompatible(item, read(CompanionInventoryArea.Armor, i))) return false;
                        return true;
                    }
                    if (!item.accessory) return false;
                    int ownFirst = slot < 10 ? 3 : 13, otherFirst = slot < 10 ? 13 : 3;
                    for (int i = ownFirst; i < ownFirst + 7; i++)
                        if (i != slot && !ItemSlot.CanEquipBothAccessories(item, read(CompanionInventoryArea.Armor, i), slot >= 10)) return false;
                    for (int i = otherFirst; i < otherFirst + 7; i++)
                        if (SameOrIncompatible(item, read(CompanionInventoryArea.Armor, i))) return false;
                    return true;
                default: return false;
            }
        }

        private static bool SameOrIncompatible(Item first, Item second)
        {
            if (second == null || second.IsAir) return false;
            int conflict = ItemID.Sets.AccessoryIncompatibilityType[first.type];
            return first.type == second.type || conflict != -1 && conflict == ItemID.Sets.AccessoryIncompatibilityType[second.type];
        }

        internal static bool Blocked(Player body, CompanionInventoryArea area, int slot, ItemOperationOwnership operations)
        {
            if (!Available(body, area, slot)) return true;
            // HasLockedInventory is not a pure per-body predicate: it scans
            // and clears native Air locks, then reads LocalPlayer's pending
            // crafting cursor. Only this exact native slot is our dependency.
            if (area == CompanionInventoryArea.Inventory && body.inventoryChestStack[slot]) return true;
            if (operations != null && (area == CompanionInventoryArea.Inventory && operations.IsProtected(slot) ||
                area == CompanionInventoryArea.Armor && operations.IsProtected(5, slot) ||
                area == CompanionInventoryArea.Misc && operations.IsProtected(6, slot))) return true;
            if (!body.UsingOrReusingItem && !body.channel) return false;
            if (area != CompanionInventoryArea.Inventory) return false;
            if (slot == body.selectedItem) return true;
            if (body.selectedItem < 0 || body.selectedItem >= body.inventory.Length) return false;
            Item held = body.inventory[body.selectedItem], candidate = body.inventory[slot];
            // Current use can choose another ammo stack at its next native
            // debit. Also respect the native wand/material dependencies; the
            // owner lease remains the authority for borrowed automation work.
            return held != null && candidate != null && !candidate.IsAir &&
                (held.useAmmo > 0 && candidate.ammo == held.useAmmo || held.tileWand > 0 && candidate.type == held.tileWand ||
                body.autoPaint && candidate.PaintOrCoating || body.autoActuator && candidate.type == ItemID.Actuator ||
                WireTool(held) && (candidate.type == ItemID.Wire || candidate.type == ItemID.Actuator) || FlexibleMaterial(body, held, candidate));
        }

        private static bool WireTool(Item item)
        { return item.type == 509 || item.type == 850 || item.type == 851 || item.type == 3612 || item.type == 3625 || item.shoot == 651; }

        private static bool FlexibleMaterial(Player body, Item held, Item candidate)
        {
            var wand = held.GetFlexibleTileWand();
            return wand != null && wand.TryGetPlacementOption(body, Player.FlexibleWandRandomSeed, Player.FlexibleWandCycleOffset,
                out var option, out Item material) && ReferenceEquals(material, candidate);
        }
    }
}
