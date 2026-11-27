using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private static bool OrnamentOperation(BlueprintBuildOperation operation) =>
            operation?.Kind == BlueprintBuildOperationKind.ChristmasTreeOrnament;

        private bool TryPlanChristmasTreeOrnament(BlueprintBuildTarget target, BlueprintWorldMatchSample actual, out BlueprintBuildOperation operation)
        {
            operation = null;
            if (!BlueprintChristmasTreeMembers.IsAnchor(target.Cell) || !BlueprintChristmasTreeMembers.IsAnchor(actual.Cell) ||
                target.Cell.FrameY == actual.Cell.FrameY || !BuildOperationBodySame(target, actual)) return false;
            for (int slot = 0; slot < BlueprintChristmasTreeMembers.OrnamentSlots; slot++)
            {
                int wanted = BlueprintChristmasTreeMembers.OrnamentItem(target.Cell.FrameY, slot);
                if (wanted == BlueprintChristmasTreeMembers.OrnamentItem(actual.Cell.FrameY, slot)) continue;
                if (wanted == 0)
                {
                    // Vanilla item use can add or swap one ornament. An empty
                    // target slot has no corresponding removal interaction.
                    projectionFeedback = "圣诞树上有多余的装饰，需要手动处理。";
                    continue;
                }
                var next = new BlueprintBuildOperation { Kind = BlueprintBuildOperationKind.ChristmasTreeOrnament,
                    Consumer = ToolBuildConsumer.ChristmasTreeOrnament, Mode = wanted, Cycle = slot, HeldMaximum = 1, PrimaryMaximum = 0 };
                if (operation == null) operation = next;
                if (ConstructionItemSlot(player, wanted) >= 0) { operation = next; break; }
            }
            if (operation == null) return false;
            // Vanilla can decorate from any member. Keep the saved ordinal
            // at the anchor, but aim at the closest member of the same body
            // so placing a tall tree from below does not strand its ornaments.
            var body = target.Body;
            target.TargetX = System.Math.Max(body.WorldX, System.Math.Min(body.WorldX + body.Width - 1, (int)(player.Center.X / 16f)));
            target.TargetY = System.Math.Max(body.WorldY, System.Math.Min(body.WorldY + body.Height - 1, (int)(player.Center.Y / 16f)));
            return true;
        }

        private bool ChristmasTreeOrnamentCurrent(BuildFlight flight)
        {
            var target = flight.Target;
            return flight.Source.Type == flight.Operation.Mode &&
                ReadBuildCell(target.Body.AnchorX, target.Body.AnchorY, out var actual) &&
                BlueprintChristmasTreeMembers.IsAnchor(actual.Cell) && BuildOperationBodySame(target, actual) &&
                ReadBuildCell(target.WorldX, target.WorldY, out var aimed) && target.Body.SamePlacement(aimed.Object) &&
                BlueprintChristmasTreeMembers.OrnamentItem(actual.Cell.FrameY, flight.Operation.Cycle) != flight.Operation.Mode;
        }

        private bool ChristmasTreeOrnamentApplied(BuildFlight flight)
        {
            // The actual Player use owns consumption, old drop and network
            // sync. A receipt requires the intended slot to have changed.
            var target = flight.Target;
            return flight.Entered && ReadBuildCell(target.Body.AnchorX, target.Body.AnchorY, out var actual) &&
                actual.Object != null && target.Body.SamePlacement(actual.Object) && BlueprintChristmasTreeMembers.IsAnchor(actual.Cell) &&
                BlueprintChristmasTreeMembers.OrnamentItem(actual.Cell.FrameY, flight.Operation.Cycle) == flight.Operation.Mode;
        }
    }
}
