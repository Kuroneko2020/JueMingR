using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.DataStructures;

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
            int needed = 0;
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
                needed |= 1 << slot;
            }
            if (needed == 0) return false;
            // The complete live body determines all outstanding slots. A
            // missing first choice must not hide the other legal refills.
            operation = new BlueprintBuildOperation { Kind = BlueprintBuildOperationKind.ChristmasTreeOrnament,
                Consumer = ToolBuildConsumer.ChristmasTreeOrnament, OrnamentSlots = needed, HeldMaximum = 1, PrimaryMaximum = 0 };
            // Vanilla can decorate from any member. Keep the saved ordinal
            // at the anchor, but aim at the closest member of the same body
            // so placing a tall tree from below does not strand its ornaments.
            var body = target.Body;
            target.TargetX = System.Math.Max(body.WorldX, System.Math.Min(body.WorldX + body.Width - 1, (int)(player.Center.X / 16f)));
            target.TargetY = System.Math.Max(body.WorldY, System.Math.Min(body.WorldY + body.Height - 1, (int)(player.Center.Y / 16f)));
            return true;
        }

        private static int NeededOrnamentType(CaptureCell cell, int slots, int slot) =>
            (slots & (1 << slot)) != 0 ? BlueprintChristmasTreeMembers.OrnamentItem(cell.FrameY, slot) : 0;

        private int PlanChristmasTreeSource(BlueprintBuildTarget target, Player p, BuildSource preferred,
            ref BlueprintBuildOperation operation, out BlueprintBuildWait wait)
        {
            wait = BlueprintBuildWait.Material | BlueprintBuildWait.Capability;
            int tool = -1, chosen = -1;
            bool prefer = BuildHintCurrent(p, preferred);
            for (int slot = 0; slot < BlueprintChristmasTreeMembers.OrnamentSlots; slot++)
            {
                int type = NeededOrnamentType(target.Cell, operation.OrnamentSlots, slot);
                if (type == 0) continue;
                bool same = prefer && preferred.Type == type;
                if (tool >= 0 && !same) continue;
                int candidate = same ? preferred.Slot : ConstructionItemSlot(p, type);
                if (candidate < 0) continue;
                if (!p.IsInTileInteractionRange(target.WorldX, target.WorldY, TileReachCheckSettings.Simple, p.inventory[candidate].tileBoost + p.blockRange))
                { wait = BlueprintBuildWait.Player | BlueprintBuildWait.Capability; continue; }
                tool = candidate;
                chosen = slot;
                if (same || !prefer) break;
            }
            if (tool < 0) return -1;
            wait = BlueprintBuildWait.None;
            operation = CopyBuildOperation(operation);
            operation.Mode = p.inventory[tool].type;
            operation.Cycle = chosen;
            return tool;
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
