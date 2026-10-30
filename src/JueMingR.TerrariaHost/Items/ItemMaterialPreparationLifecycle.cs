using System;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Items
{
    internal sealed partial class ItemMaterialPreparation
    {
        internal struct CursorSyncScope
        {
            internal ItemMaterialPreparation Owner;
            internal Player Player;
            internal Item Cursor;
            internal long Epoch, Session;
        }
        internal struct DeathScope
        {
            internal ItemMaterialPreparation Owner;
            internal Player Player;
        }
        internal struct DeathReturnScope
        {
            internal ItemMaterialPreparation Owner;
            internal Player Player;
            internal Item Before, Incoming;
            internal int Slot, Type, BeforeStack;
        }
        private sealed class CursorHandoff
        {
            internal Player Player;
            internal Item Cursor, Clone;
            internal long Session;
        }
        private CursorHandoff cursorHandoff;
        private Player cursorPlayer;
        private Item cursorOriginal, cursorClone;
        private int cursorSyncCalls, lifecycleCalls;
        private bool CursorCommandInput
        {get{return tools.ManualLeft || tools.ManualRight || PlayerInput.MouseInfo.LeftButton==ButtonState.Pressed ||
            PlayerInput.MouseInfo.RightButton==ButtonState.Pressed || PlayerInput.Triggers.Current.Throw;}}

        // This is one consumed gesture on the exact previously hidden cursor,
        // not an inventory lock. Real release or a real new cursor ends it.
        // Keeping it outside ResetReceipt prevents the later ItemCheck in the
        // SAME Update from consuming slot58 after drop has handed the UI back.
        private void RefreshCursorHandoff()
        {
            var handoff = cursorHandoff;
            if (handoff != null && (!OnGameThread || handoff.Session != tools.Runtime.Generation ||
                !ReferenceEquals(handoff.Player, tools.Items.World.SessionPlayer) || !CursorCommandInput ||
                !ReferenceEquals(handoff.Cursor, Main.mouseItem))) cursorHandoff = null;
        }
        private bool OwnsCursor(Player p)
        {
            return (phase == Phase.Preparing || phase == Phase.Returning) && SameSession(p) && ownsInventory &&
                Main.mouseItem != null && !Main.mouseItem.IsAir && Matches(Main.mouseItem, expectedCursor, cursorType, cursorStack);
        }
        private void HandoffCursor(Player p)
        {
            if (!OwnsCursor(p)) return;
            cursorHandoff = new CursorHandoff { Player = p, Cursor = Main.mouseItem, Session = session,
                Clone = ReferenceEquals(cursorOriginal, Main.mouseItem) ? cursorClone : null };
            LoseIdentity("material-preparation-player-cursor", true);
        }
        internal static CursorSyncScope BeforeNativeCursorSync(Player p)
        {
            var owner = Current;
            if (owner == null || !owner.OnGameThread || !ReferenceEquals(p, owner.tools.Items.World.SessionPlayer)) return default(CursorSyncScope);
            owner.RefreshCursorHandoff();
            bool ownCursor = owner.OwnsCursor(p);
            if (ownCursor && owner.CursorCommandInput) owner.HandoffCursor(p);
            // Observe only our finite cursor lineage. Ordinary player cursor
            // sync, other characters and an idle preparation allocate nothing.
            if (!ownCursor && owner.cursorHandoff == null && !owner.IsDisplaced(Main.mouseItem)) return default(CursorSyncScope);
            owner.cursorSyncCalls++;
            owner.cursorPlayer = p; owner.cursorOriginal = Main.mouseItem; owner.cursorClone = null;
            return new CursorSyncScope { Owner = owner, Player = p, Cursor = Main.mouseItem, Epoch = owner.receiptEpoch, Session = owner.tools.Runtime.Generation };
        }
        internal static void AfterNativeCursorSync(CursorSyncScope scope, Exception error)
        {
            var owner = scope.Owner;
            if (owner == null) return;
            owner.cursorSyncCalls--;
            if (error != null || scope.Epoch != owner.receiptEpoch || scope.Session != owner.tools.Runtime.Generation ||
                !ReferenceEquals(scope.Player, owner.tools.Items.World.SessionPlayer) ||
                !ReferenceEquals(scope.Cursor, Main.mouseItem)) return;
            // The original dropItemCheck has performed its unconditional Clone
            // into 58. This exact call is provenance; type equality is not.
            owner.cursorPlayer = scope.Player; owner.cursorOriginal = scope.Cursor; owner.cursorClone = scope.Player.inventory[58];
            if (owner.cursorHandoff != null && ReferenceEquals(owner.cursorHandoff.Cursor, scope.Cursor))
                owner.cursorHandoff.Clone = owner.cursorClone;
        }
        private bool CursorConsumerBlocked(Player p, Item item)
        {
            RefreshCursorHandoff();
            if (cursorHandoff == null && CursorCommandInput && OwnsCursor(p)) HandoffCursor(p);
            var handoff = cursorHandoff;
            if (handoff == null || !ReferenceEquals(p, handoff.Player) || !ReferenceEquals(Main.mouseItem, handoff.Cursor)) return false;
            return ReferenceEquals(item, handoff.Cursor) || ReferenceEquals(item, handoff.Clone) ||
                cursorSyncCalls != 0 && ReferenceEquals(cursorPlayer, p) && ReferenceEquals(cursorOriginal, handoff.Cursor) &&
                ReferenceEquals(item, p.inventory[58]);
        }
        internal static bool BlocksNativeCursorDrop(Player p, int slot)
        { return slot == 58 && Current?.CursorConsumerBlocked(p, p.inventory[58]) == true; }
        internal static bool BlocksNativeCursorUse(Player p, Item item)
        { return Current?.CursorConsumerBlocked(p, item) == true; }
        internal void HandUseToManual(long useToken)
        {
            // The cause belongs to this token at the actual handoff, not to
            // whichever buttons happen to be down at delayed Completed.
            if (OnGameThread && phase == Phase.Using && token == useToken && SameSessionIdentity(player))
                LoseIdentity("material-preparation-player-use", true);
        }

        internal static DeathScope EnterDeathCall(Player p)
        {
            var owner = Current;
            if (owner == null || !owner.OnGameThread || !ReferenceEquals(p, owner.tools.Items.World.SessionPlayer) ||
                !owner.HasWork && owner.abandonedDisplaced.Count == 0 && owner.cursorHandoff == null) return default(DeathScope);
            owner.lifecycleCalls++;
            return new DeathScope { Owner = owner, Player = p };
        }
        internal static void BeforeDeathInventory(Player p)
        {
            var owner = Current;
            if (owner == null || !owner.OnGameThread || !owner.HasWork || owner.phase == Phase.Unknown || owner.phase == Phase.Abandoning ||
                !owner.SameSessionIdentity(p)) return;
            if (!owner.ExpectedCurrent(owner.phase == Phase.Using))
            { owner.LoseIdentity("material-preparation-before-death-unknown", false); return; }
            // DropItems is the accepted native death boundary (KillMe can
            // return without dying). Give up only THIS known return promise
            // before inventory changes. Do not replay swaps or clear Unknown.
            owner.LoseIdentity("material-preparation-death", true);
        }
        internal static void LeaveDeathCall(DeathScope scope)
        {
            var owner = scope.Owner;
            if (owner == null) return;
            owner.lifecycleCalls--;
            owner.FinishAbandonment();
            if (owner.lifecycleCalls == 0) owner.ObserveInventory(scope.Player);
        }
        internal static DeathReturnScope BeforeDeathReturn(Player p, Item incoming, int slot)
        {
            var owner = Current;
            if (owner == null || owner.lifecycleCalls == 0 || !owner.OnGameThread || slot < 0 || slot >= 58 ||
                !ReferenceEquals(p, owner.tools.Items.World.SessionPlayer) ||
                !(owner.IsDisplaced(incoming) || ReferenceEquals(p, owner.cursorPlayer) && ReferenceEquals(incoming, owner.cursorClone) &&
                    owner.IsDisplaced(owner.cursorOriginal))) return default(DeathReturnScope);
            return new DeathReturnScope { Owner = owner, Player = p, Slot = slot, Before = p.inventory[slot],
                Incoming = incoming, Type = incoming.type, BeforeStack = p.inventory[slot].stack };
        }
        internal static void AfterDeathReturn(DeathReturnScope scope, Exception error)
        {
            var owner = scope.Owner;
            if (owner == null || error != null || !ReferenceEquals(scope.Player, owner.tools.Items.World.SessionPlayer)) return;
            var delivered = scope.Player.inventory[scope.Slot];
            if (delivered == null || delivered.IsAir || delivered.type != scope.Type ||
                ReferenceEquals(delivered, scope.Before) && delivered.stack <= scope.BeforeStack) return;
            // KillMe returns its exact native cursor clone via GetItem. Carry
            // the existing exclusion through only that actual fill/merge.
            // A full occupied-slot merge leaves incoming.stack unchanged;
            // the recipient's real increase, not the input remainder, proves it.
            // death is not a player's pickup/sort granting new supply.
            owner.guardPlayer = scope.Player; owner.guardInventory = scope.Player.inventory; owner.guardSession = owner.tools.Runtime.Generation;
            if (!owner.abandonedDisplaced.Contains(delivered)) owner.abandonedDisplaced.Add(delivered);
        }
    }
}
