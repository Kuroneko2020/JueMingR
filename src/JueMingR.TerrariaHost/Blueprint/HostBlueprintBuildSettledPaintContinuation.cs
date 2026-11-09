using System;
using JueMingR.TerrariaHost.Tools;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private bool SettledPaintContinuationAllowed(BuildFlight current, BuildFlight next)
        {
            // Only the positively consumed, genuinely empty receipt grants
            // this narrower continuation. Nonempty/unknown/manual receipts
            // retain their inverse program and ordinary selection boundary.
            if (current == null || next == null || !current.EmptySupplySettled || current.SettledSupplyPreparationPending || !current.Checked || current.Rejected ||
                !current.SettledConsumption || next.Token != 0 || buildMaterialPreparation == null || buildMaterialPreparation.HasWork ||
                !ReferenceEquals(current.Player, next.Player) || !ReferenceEquals(current.Player, player) ||
                !buildTools.Use.Building || buildTools.Use.Operation != current.Token || !buildTools.Use.ActionValid ||
                buildTools.ManualLeft || buildTools.ManualRight || Main.playerInventory || Main.mouseItem == null || !Main.mouseItem.IsAir ||
                Main.LocalPlayerHasPendingInventoryActions() || player.selectedItemState.HasBufferedChange) return false;
            var a = current.Operation; var b = next.Operation;
            if (a == null || b == null || a.Kind != b.Kind ||
                a.Kind != BlueprintBuildOperationKind.PaintTile && a.Kind != BlueprintBuildOperationKind.PaintWall ||
                (a.Mode & AppearanceCoatingMode) != 0 || (b.Mode & AppearanceCoatingMode) != 0 || b.SupplyDestination != 54 ||
                BuildConsumer(a, current.Target) != BuildConsumer(b, next.Target) ||
                next.Source.Slot != current.Source.Slot || next.Source.Type != current.Source.Type ||
                !ReferenceEquals(next.Source.Item, current.Source.Item) || next.CursorMode != current.CursorMode ||
                next.SmartDirection != current.SmartDirection || next.SmartDirectionY != current.SmartDirectionY) return false;
            // The canonical native paint scan, not a color/ItemID whitelist,
            // must already choose the successor's exact live bag material.
            // This path performs no preparation clicks or inventory writes.
            var first = FirstBuildPaint(player);
            int slot = first == null ? -1 : Array.IndexOf(player.inventory, first);
            return slot >= 0 && first.paint > 0 && first.type == b.SupplyType && first.paint == b.Mode &&
                buildTools.BuildInventoryEligible(slot, first) && PreparedSupplyMatches(next, first, slot);
        }
        private bool DeferSettledPaintPreparation(BlueprintBuildTarget target)
        {
            var current = activeBuild;
            if (current == null || !current.EmptySupplySettled || !current.Checked || current.Rejected || !current.SettledConsumption ||
                buildMaterialPreparation == null || buildMaterialPreparation.HasWork || !ReferenceEquals(current.Player, player) ||
                !buildTools.Use.Building || current.Token != buildTools.Use.Operation ||
                !buildTools.Items.Ownership.OwnsUse(buildTools.Runtime.Generation, current.Token)) return false;
            var operation = current.Operation;
            if (operation == null || target.Operation == null || operation.Kind != target.Operation.Kind ||
                operation.Consumer != target.Operation.Consumer ||
                operation.Kind != BlueprintBuildOperationKind.PaintTile && operation.Kind != BlueprintBuildOperationKind.PaintWall ||
                (operation.Mode & AppearanceCoatingMode) != 0 || (target.Operation.Mode & AppearanceCoatingMode) != 0) return false;
            current.SettledSupplyPreparationPending = true;
            target.Readiness = BlueprintBuildReadiness.Deferred;
            return true;
        }
        private bool ContinueSettledPaintLease(BuildFlight current, BuildFlight next, ToolIntent intent)
        {
            // Refresh already rechecked current A before admitting another
            // group. Re-read material/input after that possibly native proof,
            // then exchange the old exact mask without End/Begin or a token
            // change. Vanilla alone advances animation and useTime.
            if (!SettledPaintContinuationAllowed(current, next) ||
                !buildTools.Items.Ownership.TryContinueUse(buildTools.Runtime.Generation, current.Source.Slot, current.Token,
                    intent.MaterialSlots, next.DependencySlots)) return false;
            intent.MaterialSlots = next.DependencySlots;
            next.EmptySupplySettled = true;
            return true;
        }
    }
}
