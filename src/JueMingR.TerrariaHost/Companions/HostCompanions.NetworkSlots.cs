using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
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
