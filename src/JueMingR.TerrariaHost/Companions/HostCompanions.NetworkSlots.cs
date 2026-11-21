using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using JueMingR.Platform.Companions;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal static int NetworkSlot(CompanionInventoryArea area, int slot)
        {
            // The same native mapping serves either identity. In particular,
            // owner equipment/misc is not an inventory packet with its index.
            switch (area)
            {
                case CompanionInventoryArea.Inventory: return PlayerItemSlotID.Inventory0 + slot;
                case CompanionInventoryArea.Armor: return PlayerItemSlotID.Armor0 + slot;
                case CompanionInventoryArea.Dye: return PlayerItemSlotID.Dye0 + slot;
                case CompanionInventoryArea.Misc: return PlayerItemSlotID.Misc0 + slot;
                case CompanionInventoryArea.MiscDye: return PlayerItemSlotID.MiscDye0 + slot;
                case CompanionInventoryArea.PiggyBank: return PlayerItemSlotID.Bank1_0 + slot;
                case CompanionInventoryArea.Safe: return PlayerItemSlotID.Bank2_0 + slot;
                case CompanionInventoryArea.DefenderForge: return PlayerItemSlotID.Bank3_0 + slot;
                case CompanionInventoryArea.VoidVault: return PlayerItemSlotID.Bank4_0 + slot;
                default: throw new ArgumentOutOfRangeException(nameof(area));
            }
        }

        private static int StoredLoadoutNetworkSlot(CompanionInventoryArea area, int loadout, int slot)
        {
            bool armor = area == CompanionInventoryArea.Armor;
            if (loadout == 0) return slot + (armor ? PlayerItemSlotID.Loadout1_Armor_0 : PlayerItemSlotID.Loadout1_Dye_0);
            if (loadout == 1) return slot + (armor ? PlayerItemSlotID.Loadout2_Armor_0 : PlayerItemSlotID.Loadout2_Dye_0);
            return slot + (armor ? PlayerItemSlotID.Loadout3_Armor_0 : PlayerItemSlotID.Loadout3_Dye_0);
        }
        private static int[] NetworkSlots(Player body)
        {
            // 1.4.5.8 reserves 200 protocol IDs for each bank, while native
            // Chest.CreateBank currently allocates 40 actual items. Native
            // TrySyncingItemArray likewise walks the actual array at its
            // protocol offset. Reserved holes are not inventory entries.
            var slots=new List<int>();
            AddSlots(slots,PlayerItemSlotID.Inventory0,body.inventory.Length);
            AddSlots(slots,PlayerItemSlotID.Armor0,body.armor.Length);
            AddSlots(slots,PlayerItemSlotID.Dye0,body.dye.Length);
            AddSlots(slots,PlayerItemSlotID.Misc0,body.miscEquips.Length);
            AddSlots(slots,PlayerItemSlotID.MiscDye0,body.miscDyes.Length);
            AddSlots(slots,PlayerItemSlotID.Bank1_0,body.bank.item.Length);
            AddSlots(slots,PlayerItemSlotID.Bank2_0,body.bank2.item.Length);
            slots.Add(PlayerItemSlotID.TrashItem);
            AddSlots(slots,PlayerItemSlotID.Bank3_0,body.bank3.item.Length);
            AddSlots(slots,PlayerItemSlotID.Bank4_0,body.bank4.item.Length);
            if(body.Loadouts.Length!=3)throw new InvalidOperationException("原版装备栏布局与队友接口不一致。");
            AddSlots(slots,PlayerItemSlotID.Loadout1_Armor_0,body.Loadouts[0].Armor.Length);AddSlots(slots,PlayerItemSlotID.Loadout1_Dye_0,body.Loadouts[0].Dye.Length);
            AddSlots(slots,PlayerItemSlotID.Loadout2_Armor_0,body.Loadouts[1].Armor.Length);AddSlots(slots,PlayerItemSlotID.Loadout2_Dye_0,body.Loadouts[1].Dye.Length);
            AddSlots(slots,PlayerItemSlotID.Loadout3_Armor_0,body.Loadouts[2].Armor.Length);AddSlots(slots,PlayerItemSlotID.Loadout3_Dye_0,body.Loadouts[2].Dye.Length);
            return slots.ToArray();
        }
        private static void AddSlots(List<int> slots,int first,int count){for(int i=0;i<count;i++)slots.Add(first+i);}
    }
}
