using System;
using JueMingR.Features.Items;
using JueMingR.Platform.Items;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Terraria.UI;

namespace Terraria
{
    // Independent ABI sink: checks the production adapter's payload and failure
    // boundary, not native rendering. The target .8 methods were inspected separately.
    public struct AdvancedPopupRequest
    {
        public string Text;
        public Color Color;
        public int DurationInFrames;
        public Vector2 Velocity;
    }
    public static class PopupText
    {
        public static int Calls;
        public static bool Throw, Reject;
        public static AdvancedPopupRequest Last;
        public static Vector2 Position;
        public static Action OnShow;
        public static int NewText(AdvancedPopupRequest request, Vector2 position)
        {
            Calls++; Last = request; Position = position;
            if (OnShow != null) OnShow();
            if (Throw) throw new InvalidOperationException("fixture-popup-unavailable");
            return Reject ? -1 : 0;
        }
        internal static void Reset() { Calls = 0; Throw = Reject = false; Last = default(AdvancedPopupRequest); OnShow = null; }
    }
    public partial class Main { public static bool showItemText = true; }

    internal static class ItemDiscardFeedbackChecks
    {
        internal static void Run(HostItems host, Action newSession)
        {
            newSession(); PopupText.Reset(); Item.AffixReads = 0;
            host.Change(ItemAutomationSettings.Default); host.PollPreferences();
            Player player = Main.LocalPlayer;
            player.Center = new Vector2(320, 480);
            player.inventory[10] = new Item { type = 8, stack = 23, prefix = 3, DisplayName = "木头" };
            player.trashItem = new Item { type = 9, stack = 999 };
            Item.OnAffixName = () => Check(player.inventory[10].stack == 23, "name must be captured before source clearing");
            bool completedBeforeDisplay = false;
            PopupText.OnShow = () => completedBeforeDisplay = host.Ownership.DiscardResult != null &&
                host.Ownership.DiscardResult.State == ItemOperationState.Completed && !host.Ownership.DiscardBlocked;
            ItemOperationResult result;
            try { result = Discard(host, 10); }
            finally { Item.OnAffixName = null; PopupText.OnShow = null; }
            Check(result.State == ItemOperationState.Completed && result.ConfirmedQuantity == 23 && player.inventory[10].IsAir && player.trashItem.stack == 23,
                "trusted original discard result keeps actual stack and replacement semantics");
            Check(PopupText.Calls == 1 && PopupText.Last.Text == "自动丢弃了23个锋利的木头" && completedBeforeDisplay,
                "one completed discard must display captured localized affix name and actual 23, excluding old trash");
            Check(PopupText.Position == player.Center && PopupText.Last.DurationInFrames == 60 && PopupText.Last.Velocity == new Vector2(0, -7),
                "original regular-reforge position and motion feed the native popup sink");
            for (int i = 0; i < 20; i++) host.Feature.LastResult(ItemActionKind.Discard);
            Check(PopupText.Calls == 1, "reading last result cannot replay successful feedback");
            Check(Discard(host, 10).State == ItemOperationState.Rejected && PopupText.Calls == 1, "stale cleared request cannot display twice");

            host.Change(host.Preferences.Value.WithDiscardFeedbackEnabled(false)); host.PollPreferences();
            player.inventory[10] = new Item { type = 8, stack = 7, DisplayName = "Wood" };
            int reads = Item.AffixReads;
            Item.OnAffixName = () => { throw new InvalidOperationException("disabled name lookup"); };
            try { result = Discard(host, 10); } finally { Item.OnAffixName = null; }
            Check(result.State == ItemOperationState.Completed && result.ConfirmedQuantity == 7 && Item.AffixReads == reads && PopupText.Calls == 1,
                "feedback off still discards without name access or popup formatting");
            host.Change(host.Preferences.Value.WithDiscardFeedbackEnabled(true)); host.PollPreferences();
            Main.showItemText = false;
            player.inventory[10] = new Item { type = 8, stack = 1 };
            try { result = Discard(host, 10); } finally { Main.showItemText = true; }
            Check(result.State == ItemOperationState.Completed && Item.AffixReads == reads && PopupText.Calls == 1,
                "native show-item-text opt-out also skips name lookup without changing discard");
            foreach (string failure in new[] { "name", "throw", "reject" })
            {
                PopupText.Reset(); Item.AffixReads = 0;
                player.inventory[10] = new Item { type = 8, stack = 2, DisplayName = "Wood" };
                if (failure == "name") Item.OnAffixName = () => { throw new InvalidOperationException("fixture-language-failure"); };
                PopupText.Throw = failure == "throw"; PopupText.Reject = failure == "reject";
                try { result = Discard(host, 10); } finally { Item.OnAffixName = null; }
                Check(result.State == ItemOperationState.Completed && result.ConfirmedQuantity == 2 && !host.Ownership.DiscardBlocked,
                    "presentation failure cannot change completed deletion or resource ownership: " + failure);
                int expectedCalls = failure == "name" ? 0 : 1;
                Check(PopupText.Calls == expectedCalls, "failed presentation attempted at most once: " + failure);
                PopupText.Throw = PopupText.Reject = false;
                Discard(host, 10);
                Check(PopupText.Calls == expectedCalls, "later availability cannot resend old completed feedback");
            }
            PopupText.Reset();
            player.inventory[10] = new Item { type = 8, stack = 4, DisplayName = "Wood" };
            result = Discard(host, 10);
            Check(PopupText.Calls == 1 && PopupText.Last.Text == "自动丢弃了4个Wood", "next success reads current item-language display name");

            foreach (int failure in new[] { 1, 2, 3 })
            {
                newSession(); PopupText.Reset();
                Main.LocalPlayer.inventory[10] = new Item { type = 8, stack = 5 };
                ItemSlot.TrashFailure = failure;
                try { result = Discard(host, 10); } finally { ItemSlot.TrashFailure = 0; }
                Check(result.State == ItemOperationState.Unconfirmed && result.ConfirmedQuantity == 0 && PopupText.Calls == 0 && host.Ownership.DiscardBlocked,
                    "interrupted, no-op or mismatching trash must not report completed feedback: " + failure);
            }
            newSession(); PopupText.Reset();
            Main.LocalPlayer.inventory[10] = new Item { type = 8, stack = 6 };
            Main.cursorOverride = 6;
            try { ItemSlot.LeftClick(Main.LocalPlayer.inventory, 0, 10); } finally { Main.cursorOverride = 0; }
            Check(Main.LocalPlayer.inventory[10].IsAir && PopupText.Calls == 0, "manual native trash operation never emits automation feedback");
            Console.WriteLine("PASS: discard feedback exact localized quantities, native ABI sink, opt-out zero lookup, once-only failure isolation, unknown and manual suppression.");
            PopupText.Reset(); Item.OnAffixName = null;
        }
        private static ItemOperationResult Discard(HostItems host, int slot)
        {
            ItemInventoryObservation inventory;
            Check(host.World.TryObserve(out inventory), "discard observation available");
            return host.Operations.Execute(new DiscardItemRequest(host.Runtime.Generation, inventory.Slots[slot]));
        }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("DISCARD FEEDBACK CHECK FAILED: " + message); }
    }
}
