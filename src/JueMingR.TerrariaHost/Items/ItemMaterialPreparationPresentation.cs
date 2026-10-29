using System;
using Terraria;
using Terraria.GameInput;
using Microsoft.Xna.Framework.Input;

namespace JueMingR.TerrariaHost.Items
{
    internal sealed partial class ItemMaterialPreparation
    {
        internal sealed class PresentationScope
        {
            internal ItemMaterialPreparation Owner;
            internal long Epoch, Intent;
            internal bool Inventory;
        }
        private PresentationScope presentation;
        private long presentationIntent;
        private int presentationSlot = -1;
        private Item presentationItem;
        private Item[] hotbarView;
        private static readonly Item hiddenCursor = new Item();

        private bool PresentationCurrent(bool ignoreInventoryIntent = false)
        {
            if (!OnGameThread || (phase != Phase.Preparing && phase != Phase.Returning && phase != Phase.Ready && phase != Phase.Using) ||
                forwardCount == 0 || !SameSession(player) || tools.SelectionIntent != selection || !OrdinaryInventoryUi(player) ||
                player.dead || !tools.Runtime.IsSessionActive || tools.ManualLeft || tools.ManualRight ||
                PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed || PlayerInput.MouseInfo.RightButton == ButtonState.Pressed ||
                PlayerInput.Triggers.Current.SmartSelect || !ignoreInventoryIntent &&
                (PlayerInput.Triggers.Current.Inventory || Main.keyState.IsKeyDown(Keys.Escape)) ||
                !PreparationItemsNotPending() || !ExpectedCurrent(phase == Phase.Using, false)) return false;
            return presentation != null ? presentation.Inventory == ownsInventory : Main.playerInventory == ownsInventory;
        }
        private bool HiddenInventoryCurrent(bool ignoreInventoryIntent = false)
        { return ownsInventory && (phase == Phase.Preparing || phase == Phase.Returning) && PresentationCurrent(ignoreInventoryIntent); }

        internal static void BeginHiddenPresentation(out PresentationScope scope)
        {
            scope = null; var owner = Current;
            if (owner == null || owner.presentation != null || !owner.HiddenInventoryCurrent()) return;
            scope = new PresentationScope { Owner = owner, Epoch = owner.receiptEpoch, Intent = owner.presentationIntent, Inventory = Main.playerInventory };
            owner.presentation = scope;
            // DrawInterface mixes layout and ordinary UI logic. Only an
            // internally opened plain inventory may borrow the closed view;
            // Update, real swaps and DoDraw's release tail see the logical UI.
            Main.playerInventory = false;
        }
        internal static void EndHiddenPresentation(PresentationScope scope)
        { scope?.Owner.RestoreHiddenPresentation(scope); }
        private void RestoreHiddenPresentation(PresentationScope scope = null)
        {
            if (scope != null && !ReferenceEquals(scope, presentation) || presentation == null) return;
            var saved = presentation; presentation = null;
            // A native Inventory hook restores before applying the new intent.
            // A different UI opened inside Draw revokes this stale restoration.
            if (saved.Epoch == receiptEpoch && saved.Intent == presentationIntent && !Main.playerInventory && OrdinaryInventoryUi(player))
                Main.playerInventory = saved.Inventory;
        }
        internal static Item PresentMouseItem(Item actual)
        {
            var owner = Current;
            return owner?.presentation != null && owner.PresentationCurrent() &&
                ReferenceEquals(actual, Main.mouseItem) && ReferenceEquals(actual, owner.expectedCursor) ? hiddenCursor : actual;
        }
        internal static Item[] PresentHotbarInventory(Item[] actual)
        {
            var owner = Current;
            if (owner == null || !ReferenceEquals(actual, owner.inventory) || !owner.PresentationCurrent()) return actual;
            if (owner.hotbarView == null || owner.hotbarView.Length != actual.Length) owner.hotbarView = new Item[actual.Length];
            Array.Copy(actual, owner.hotbarView, actual.Length);
            // Only object references move. Native consumption updates those
            // very Items, so displayed stacks remain real, including depletion.
            for (int i = 0; i < owner.endpointCount; i++)
                owner.hotbarView[owner.endpoints[i].Slot] = owner.endpoints[i].Original;
            return owner.hotbarView;
        }
        private bool PresentCursorSelection
        { get { return (phase == Phase.Preparing || phase == Phase.Returning) && player?.selectedItem == 58 &&
            presentationSlot >= 0 && presentationSlot < 50 && presentationItem != null && PresentationCurrent(); } }
        private int PresentUsingSelection(int actual)
        {
            if (phase != Phase.Using || actual < 0 || actual >= 50 || player?.selectedItem != actual || !PresentationCurrent()) return actual;
            var use = tools.Use;
            if (!use.Building || use.Operation != token || use.Intent?.Slot != actual || !use.OwnsBuildMaterial(inventory[actual])) return actual;
            // Flexible can move the winning tool out of slot0. The view puts
            // Items back at their original endpoints, so its selected index
            // must follow THIS use's actual tool by reference as well. Keep
            // vanilla's override flag: its extra temporary slot draws this
            // same mapped Item; the pre-preparation selection is not pinned.
            for (int i = 0; i < endpointCount; i++)
                if (ReferenceEquals(endpoints[i].Original, inventory[actual])) return endpoints[i].Slot;
            return actual;
        }
        internal static int PresentHotbarSelection(int actual)
        { var owner = Current; return owner == null ? actual : owner.PresentCursorSelection ? owner.presentationSlot : owner.PresentUsingSelection(actual); }
        internal static bool PresentHotbarOverride(bool actual)
        { var owner = Current; return owner != null && owner.PresentCursorSelection ? false : actual; }
        internal static Item PresentHotbarHeldItem(Item actual)
        { var owner = Current; return owner != null && owner.PresentCursorSelection ? owner.presentationItem : actual; }
    }
}
