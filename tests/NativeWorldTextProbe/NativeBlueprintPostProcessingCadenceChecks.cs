using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private sealed class PostProcessingCadenceResult
        {
            internal PostProcessingEvent[] Events;
            internal PostProcessingClock[] Clocks;
            internal int Neutrals;
        }

        internal static void PostProcessingNativeCadence(object context, object host, object tools, object input)
        {
            foreach (bool hammer in new[] { true, false })
            {
                var automatic = PostProcessingCadenceRun(context, host, tools, input, hammer, null);
                // Replaying only the observed coordinate order keeps both arms
                // on the same actual shape cycle. Timing is generated afresh by
                // the unmodified original tool and legal held/neutral input;
                // automatic clocks, tokens and discovery state are never used
                // to decide when the baseline is allowed to act.
                var original = PostProcessingCadenceRun(context, host, tools, input, hammer, automatic.Events);
                var actualGaps = automatic.Events.Skip(1).Select((entry, index) => unchecked(entry.World - automatic.Events[index].World)).ToArray();
                var expectedGaps = original.Events.Skip(1).Select((entry, index) => unchecked(entry.World - original.Events[index].World)).ToArray();
                Console.WriteLine("BlueprintPostProcessingCadence tool=" + (hammer ? "WoodenHammer" : "Paintbrush") +
                    " originalGaps=" + string.Join(",", expectedGaps) + " automaticGaps=" + string.Join(",", actualGaps) +
                    " originalNeutralTransitions=" + original.Neutrals + " automaticNeutralTransitions=" + automatic.Neutrals);
                PostProcessingNoMissedOpportunity(original, hammer, "original");
                PostProcessingNoMissedOpportunity(automatic, hammer, "automatic");
                Require(original.Events.Length == automatic.Events.Length && expectedGaps.SequenceEqual(actualGaps),
                    "same-version/same-tool/same-start native post-processing intervals match every legal service opportunity without delay tolerance");
                Require(original.Events.Zip(automatic.Events, (expected, actual) => expected.X == actual.X && expected.Y == actual.Y &&
                    expected.Before == actual.Before && expected.After == actual.After && expected.ToolType == actual.ToolType &&
                    expected.SupplyType == actual.SupplyType && expected.SupplyBefore == actual.SupplyBefore && expected.SupplyAfter == actual.SupplyAfter).All(same => same),
                    "paired native input reproduces the same ordered real effects and exact zero/material costs from identical initial world and inventory");
                Require(!hammer || original.Neutrals > 0 && automatic.Neutrals > 0,
                    "both hammer arms contain real neutral input that naturally restores poundRelease");
            }
        }

        private static void PostProcessingNoMissedOpportunity(PostProcessingCadenceResult result, bool hammer, string arm)
        {
            uint first = result.Events.First().World, last = result.Events.Last().World;
            var opportunities = result.Clocks.Where(clock => clock.Hammer == hammer && clock.Potential && clock.World >= first && clock.World <= last)
                .GroupBy(clock => clock.World).Select(group => group.First()).ToArray();
            var missed = opportunities.Where(clock => !result.Events.Any(entry => entry.World == clock.World)).ToArray();
            Console.WriteLine("BlueprintPostProcessingOpportunities arm=" + arm + " hammer=" + hammer + " serviceable=" + opportunities.Length +
                " effects=" + result.Events.Length + " missed=" + missed.Length + " unknown-state=" + string.Join(";", missed.Take(12).Select(clock =>
                    clock.World + ":time/tool/animation=" + clock.Time + "/" + clock.ToolTime + "/" + clock.Animation +
                    ":pound/held=" + clock.PoundRelease + "/" + clock.Held)));
            Require(opportunities.Length >= result.Events.Length && missed.Length == 0,
                "the real consumer clock plus independent live goal/reach/stock gives a nonempty denominator with no unserved stable opportunity: " + arm);
        }

        private static PostProcessingCadenceResult PostProcessingCadenceRun(object context, object host, object tools, object input, bool hammer,
            PostProcessingEvent[] nativeOrder)
        {
            bool automatic = nativeOrder == null;
            using (var probe = new PostProcessingProbe(context, host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                p.inventory[12].SetDefaults(hammer ? ItemID.WoodenHammer : ItemID.Paintbrush); var tool = p.inventory[12];
                p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 8; var paint = p.inventory[20];
                var goals = hammer ? new[] {
                    PostProcessingTarget("Hammer", 40, 42, 1), PostProcessingTarget("Hammer", 44, 42, 3),
                    PostProcessingTarget("Hammer", 45, 42, 8), PostProcessingTarget("Hammer", 46, 42, 4) } :
                    new[] { 40, 41, 44, 45, 46 }.Select(x => PostProcessingTarget("TilePaint", x, 42, paint.paint, ItemID.RedPaint)).ToArray();
                int[] initialShapes = { 0, 8, 1, 2 };
                for (int index = 0; index < goals.Length; index++)
                    PostProcessingSeedShape(goals[index].X, goals[index].Y, hammer ? initialShapes[index] : 0);
                probe.Goals = goals; probe.ToolItems = new[] { tool }; probe.Supplies = new[] { paint };
                Require(goals.All(probe.Runnable), "paired cadence starts with the same legal existing bodies, natural reach and genuine tool/supply");
                PostProcessingPlace(context, host, input, goals);
                if (!automatic) p.selectedItemState.Select(12);
                probe.Advance(); probe.Begin(automatic);
                if (automatic) SetAutomatic(host, input, true);
                for (int frame = 0; frame < 2200 && !goals.All(goal => goal.Complete); frame++)
                {
                    if (automatic) probe.Advance();
                    else
                    {
                        Require(probe.Events.Count < nativeOrder.Length, "the original legal input produces the required effects without an extra action");
                        var next = nativeOrder[probe.Events.Count];
                        // Releasing for one real input/update lets vanilla
                        // restore poundRelease. toolTime remains untouched and
                        // the next held input waits on its own native clock.
                        probe.Advance(new Vector2(next.X * 16 + 8, next.Y * 16 + 8), !hammer || p.poundRelease);
                    }
                }
                probe.Report("cadence automatic=" + automatic + " hammer=" + hammer);
                Require(goals.All(goal => goal.Complete) && probe.Events.Count >= (hammer ? 5 : goals.Length) &&
                    ReferenceEquals(p.inventory[12], tool) && tool.stack == 1 && (hammer ? paint.stack == 8 : paint.stack == 3),
                    "paired native cadence reaches every exact target shape/color and preserves the real tool and material accounting");
                return new PostProcessingCadenceResult { Events = probe.Events.ToArray(), Clocks = probe.Clocks.ToArray(), Neutrals = probe.NativeNeutrals };
            }
        }
    }
}
