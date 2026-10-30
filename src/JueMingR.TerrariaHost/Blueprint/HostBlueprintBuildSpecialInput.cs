using System;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private sealed class SpecialSelection
        {
            internal Player Player;
            internal Item Tool;
            internal int Slot, OriginalCycle, OwnedCycle, GoalCycle, PreparedCycle;
            internal long Token, Session, Selection;
            internal bool NeutralRequired, Invalid, DownPending, Prepared;
            internal uint LastEpoch;
            internal bool EpochSet;
        }
        private SpecialSelection specialSelection;

        private ToolReadiness SpecialBuildReadiness(BuildFlight flight)
        {
            if (flight?.Operation?.NativePlacement?.Producer?.Kind != BlueprintSpecialProducerKind.Flexible) return ToolReadiness.Ready;
            var receipt = specialSelection;
            if (receipt != null && (receipt.Token != flight.Token || receipt.Session != generation || receipt.Invalid ||
                Player.FlexibleWandCycleOffset != receipt.OwnedCycle))
            { specialSelection = null; receipt = null; }
            if (receipt == null && !flight.Checked)
            {
                receipt = specialSelection = new SpecialSelection { Player = flight.Player, Tool = flight.Source.Item, Slot = flight.Source.Slot,
                    OriginalCycle = Player.FlexibleWandCycleOffset, OwnedCycle = Player.FlexibleWandCycleOffset, Token = flight.Token,
                    Session = generation, Selection = buildTools.SelectionIntent, GoalCycle = flight.Operation.Cycle,
                    Prepared = Player.FlexibleWandCycleOffset == flight.Operation.Cycle, PreparedCycle = flight.Operation.Cycle };
            }
            if (receipt == null) { flight.InputGesture = ToolInputGesture.HeldPress; return flight.Checked ? ToolReadiness.Yield : ToolReadiness.Ready; }
            if (flight.Player.controlUp || flight.Player.controlDown || buildTools.ManualLeft || buildTools.ManualRight ||
                buildTools.SelectionIntent != receipt.Selection)
            { receipt.Invalid = true; specialSelection = null; return ToolReadiness.Yield; }
            if (flight.Checked)
            { ReturnSpecialSelection(receipt); specialSelection = null; return ToolReadiness.Yield; }
            int goal = flight.Operation.Cycle;
            if (goal != receipt.GoalCycle) { receipt.GoalCycle = goal; receipt.Prepared = false; }
            if (receipt.Prepared && receipt.PreparedCycle == goal)
            { flight.InputGesture = ToolInputGesture.HeldPress; return ToolReadiness.Ready; }
            flight.InputGesture = receipt.NeutralRequired || receipt.DownPending ? ToolInputGesture.NeutralRelease :
                Player.FlexibleWandCycleOffset == goal ? ToolInputGesture.HeldPress :
                Player.FlexibleWandCycleOffset < goal ? ToolInputGesture.SelectDown : ToolInputGesture.SelectUp;
            if (Player.FlexibleWandCycleOffset == goal && !receipt.NeutralRequired && !receipt.DownPending)
            { receipt.Prepared = true; receipt.PreparedCycle = goal; }
            return ToolReadiness.Ready;
        }

        private Action<Exception> BeginSpecialControlHolds(Player p)
        {
            var flight = activeBuild; var receipt = specialSelection;
            if (receipt == null && flight != null) { SpecialBuildReadiness(flight); receipt = specialSelection; }
            if (receipt == null || receipt.Invalid || !ReferenceEquals(p, receipt.Player) || receipt.Session != generation ||
                !ReferenceEquals(p.HeldItem, receipt.Tool) || !ReferenceEquals(p.inventory[receipt.Slot], receipt.Tool) ||
                buildTools.SelectionIntent != receipt.Selection || p.controlUp || p.controlDown || buildTools.ManualLeft || buildTools.ManualRight)
                return null;
            if (receipt.EpochSet && receipt.LastEpoch == Main.GameUpdateCount) return null;
            receipt.EpochSet = true; receipt.LastEpoch = Main.GameUpdateCount;
            if (Player.FlexibleWandCycleOffset != receipt.OwnedCycle) { receipt.Invalid = true; return null; }
            bool oldUp = p.controlUp, oldDown = p.controlDown;
            if (receipt.Prepared) return null;
            int goal = receipt.GoalCycle;
            bool neutral = receipt.NeutralRequired;
            bool up = !neutral && receipt.OwnedCycle > goal && p.releaseUp;
            bool down = !neutral && receipt.OwnedCycle < goal && p.releaseDown;
            if (!neutral && !up && !down) return null;
            // Only the original variant selector sees this key. Movement has
            // already consumed the player's own controls. Release flags and
            // the global cycle are written exclusively by original consumers.
            p.controlUp = up; p.controlDown = down;
            bool ended = false;
            return error => {
                if (ended) return; ended = true;
                if (p.controlUp == up) p.controlUp = oldUp;
                if (p.controlDown == down) p.controlDown = oldDown;
                int expected = receipt.OwnedCycle + (down ? 1 : up ? -1 : 0);
                if (error != null || Player.FlexibleWandCycleOffset != expected) { receipt.Invalid = true; return; }
                receipt.OwnedCycle = expected;
                if (neutral)
                {
                    // Up is rearmed here. Down is rearmed only later in the
                    // real Player.Update and is checked by EndControlEpoch.
                    receipt.NeutralRequired = true;
                    receipt.DownPending = true;
                }
                else { receipt.NeutralRequired = true; receipt.DownPending = false; }
            };
        }
        private void EndSpecialControlEpoch(Player p)
        {
            var receipt = specialSelection;
            if (receipt == null || !ReferenceEquals(p, receipt.Player) || !receipt.DownPending) return;
            receipt.DownPending = false;
            receipt.NeutralRequired = !p.releaseUp || !p.releaseDown;
            if (!receipt.NeutralRequired && receipt.OwnedCycle == receipt.GoalCycle)
            { receipt.Prepared = true; receipt.PreparedCycle = receipt.GoalCycle; }
        }
        private void EndSpecialSelection(BuildFlight flight)
        {
            var receipt = specialSelection;
            if (receipt == null || receipt.Token != flight.Token) return;
            if (buildTools.SelectionIntent != receipt.Selection || receipt.Invalid || Player.FlexibleWandCycleOffset != receipt.OwnedCycle)
            { specialSelection = null; return; }
            ReturnSpecialSelection(receipt); specialSelection = null;
        }
        private void ReturnSpecialSelection(SpecialSelection receipt)
        {
            // Restoring the borrowed user's mode is not selecting an unearned
            // variant. Every automatic change was accepted by the real native
            // +/-1 selector; a later player selection cancels this return right.
            if (!receipt.Invalid && receipt.Session == generation && buildTools.SelectionIntent == receipt.Selection &&
                Player.FlexibleWandCycleOffset == receipt.OwnedCycle) Player.FlexibleWandCycleOffset = receipt.OriginalCycle;
        }
        private void StopSpecialSelection(bool sessionEnding)
        {
            if (sessionEnding || specialSelection != null && (buildTools?.ManualLeft == true || buildTools?.ManualRight == true ||
                buildTools?.SelectionIntent != specialSelection.Selection)) specialSelection = null;
            else if (specialSelection != null) { ReturnSpecialSelection(specialSelection); specialSelection = null; }
        }
    }
}
