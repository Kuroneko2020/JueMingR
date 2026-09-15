using System;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Guidance
{
    internal sealed class EffectiveEquipmentReader
    {
        internal readonly int[] Types = new int[15];
        internal int Count { get; private set; }
#if DEBUG
        internal int EffectiveSlotReads { get; private set; }
#endif
        internal bool Read(Player player)
        {
            Count = 0;
            if (player.armor == null || player.armor.Length < 10 || player.miscEquips == null || player.miscEquips.Length < 5) return false;
            // These .8 read-only methods reproduce unlocked slots and favorite
            // sharing/conflicts. Never call UpdateEquips or mutate returned Item.
            for (int slot = 0; slot < 10; slot++)
            {
                if (!player.IsItemSlotUnlockedAndUsable(slot)) continue;
                Item item = player.GetEffectiveArmor(slot);
#if DEBUG
                EffectiveSlotReads++;
#endif
                if (Valid(item) && (!item.expertOnly || Main.expertMode) &&
                    (slot == 0 ? item.headSlot >= 0 : slot == 1 ? item.bodySlot >= 0 : slot == 2 ? item.legSlot >= 0 : item.accessory)) Types[Count++] = item.type;
            }
            for (int slot = 0; slot < 5; slot++)
            {
                Item item = player.miscEquips[slot]; if (!Valid(item) || item.expertOnly && !Main.expertMode) continue;
                bool effective;
                if (slot < 2)
                {
                    int buff = item.buffType;
                    effective = !player.hideMisc[slot] && buff > 0 &&
                        (slot == 0 ? buff < Main.vanityPet.Length && Main.vanityPet[buff] : buff < Main.lightPet.Length && Main.lightPet[buff]) && (item.type != 603 || Main.runningCollectorsEdition);
                }
                else if (slot < 4)
                { int mount = item.mountType; effective = mount >= 0 && mount < MountID.Sets.Cart.Length && MountID.Sets.Cart[mount] == (slot == 2); }
                else effective = item.shoot >= 0 && item.shoot < Main.projHook.Length && Main.projHook[item.shoot];
                if (effective) Types[Count++] = item.type;
            }
            return true;
        }
        private static bool Valid(Item item) { return item != null && !item.IsAir && item.stack > 0 && item.type > 0 && item.type < ItemID.Count; }
    }
}
