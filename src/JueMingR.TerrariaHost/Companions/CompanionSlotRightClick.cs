using System;
using System.Reflection;
using JueMingR.Platform.Companions;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionSlotRightClick
    {
        internal enum Kind { Pickup, Open, Mode, Vanity, Equip }
        private const BindingFlags Native = BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly bool[] modeContexts = (bool[])typeof(ItemSlot).GetField("canItemSwapAt", Native).GetValue(null);
        private static readonly Action<Item> swapMode = (Action<Item>)Delegate.CreateDelegate(typeof(Action<Item>),
            typeof(ItemSlot).GetMethod("TryItemSwap", Native));
        internal static readonly Action<Item[], int, int, Player> Open = NativeAction("TryOpenContainer");
        internal static readonly Action<Item[], int, int, Player> SwapVanity = NativeAction("SwapVanityEquip");
        internal static readonly FieldInfo DyeCounter = typeof(ItemSlot).GetField("dyeSwapCounter", Native);

        private static Action<Item[], int, int, Player> NativeAction(string name) =>
            (Action<Item[], int, int, Player>)Delegate.CreateDelegate(typeof(Action<Item[], int, int, Player>),
                typeof(ItemSlot).GetMethod(name, Native));

        // This matches .8 ItemSlot.RightClick before its cursor split. Calling
        // that entire method on a remote bag would write the human mouseItem.
        internal static Kind Classify(CompanionInventoryArea area, int slot, Item item, Item cursor)
        {
            if (item == null || !cursor.IsAir && Item.CanStack(item, cursor)) return Kind.Pickup;
            int context = CompanionItemRules.SlotContext(area, slot);
            if (!PlayerInput.UsingGamepadUI && context == ItemSlot.Context.InventoryItem && ItemID.Sets.OpenableBag[item.type])
                return Kind.Open;
            if (!PlayerInput.UsingGamepadUI && modeContexts[context] && item.stack == 1 && ItemID.Sets.HasItemSwap[item.type])
                return Kind.Mode;
            if (context == ItemSlot.Context.EquipArmorVanity || context == ItemSlot.Context.EquipAccessoryVanity) return Kind.Vanity;
            if (!PlayerInput.UsingGamepadUI && item.stack == 1 &&
                (context == ItemSlot.Context.InventoryItem || context == ItemSlot.Context.BankItem || context == ItemSlot.Context.VoidItem) &&
                (item.CanBeEquipped() || item.dye > 0)) return Kind.Equip;
            return Kind.Pickup;
        }

        internal static void ChangeMode(Item projected)
        {
            // The native item table owns both the cycle and prefix/favorite
            // preservation. Its cooldown belongs to the independent window.
            int split = Main.stackSplit;
            bool released = Main.mouseRightRelease;
            try { swapMode(projected); }
            finally { Main.stackSplit = split; Main.mouseRightRelease = released; }
        }
    }
}
