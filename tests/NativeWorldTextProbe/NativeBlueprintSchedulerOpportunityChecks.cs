using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        internal static void SchedulerNativeHandoffCadence(object context, object host, object tools, object input)
        {
            foreach (bool different in new[] { false, true })
            {
                var automatic = SchedulerHandoffRun(context, host, tools, input, different, null);
                var original = SchedulerHandoffRun(context, host, tools, input, different, automatic);
                var actual = automatic.Skip(1).Select((entry, index) => unchecked(entry.World - automatic[index].World)).ToArray();
                var expected = original.Skip(1).Select((entry, index) => unchecked(entry.World - original[index].World)).ToArray();
                Console.WriteLine("BlueprintSchedulerHandoff differentMaterial=" + different + " originalGaps=" + string.Join(",", expected) +
                    " automaticGaps=" + string.Join(",", actual) + " boundaryGap=" + actual[1] + "/" + expected[1]);
                Require(automatic.Length == original.Length && expected.SequenceEqual(actual),
                    "all native intervals, including exhausted exact Item and different-material selection, match original same-frame Select plus legal held input");
                Require(automatic.Zip(original, (a, b) => a.Type == b.Type && a.Slot == b.Slot && a.X == b.X && a.Y == b.Y &&
                    a.Count == b.Count && a.ChangedGoals.SequenceEqual(b.ChangedGoals)).All(same => same),
                    "paired stack/material handoff preserves each ordered real target, exact source slot and debit");
            }
        }

        private static BusinessEvent[] SchedulerHandoffRun(object context, object host, object tools, object input, bool different, BusinessEvent[] replay)
        {
            bool automatic = replay == null;
            using (var scope = new BusinessScope(context, host, tools, input))
            using (var timeline = new SchedulerTimeline(host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
                var first = BusinessStock(p, 12, ItemID.DirtBlock, 2);
                var next = BusinessStock(p, 13, different ? ItemID.StoneBlock : ItemID.DirtBlock, 4);
                scope.Sources = new[] { first, next };
                scope.Goals = Enumerable.Range(0, 4).Select(index => BusinessTileGoal(44 + index, 42, index < 2 ? first : next)).ToArray();
                Require(scope.Goals.All(scope.Runnable), "four handoff goals have independent actual floor, reach, empty body and genuine inventory");
                var cells = scope.Goals.Select(goal => new CaptureCell { X = goal.X - 44, Layers = CaptureLayer.Tile, TileType = (ushort)goal.TileType }).ToArray();
                Place(context, host, input, Asset(host, cells), 44, 42);
                scope.Step(); scope.Begin(automatic);
                if (automatic) SetAutomatic(host, input, true);
                for (int frame = 0; frame < 1600 && !scope.Goals.All(goal => goal.Complete); frame++)
                {
                    if (automatic) scope.Step();
                    else
                    {
                        Require(scope.Events.Count < replay.Length, "native stack handoff has no extra target effect");
                        var target = replay[scope.Events.Count];
                        var source = scope.Sources.Single(value => value.Slot == target.Slot);
                        if (p.selectedItem != source.Slot)
                        {
                            if (!p.selectedItemState.HasBufferedChange) p.selectedItemState.Select(source.Slot);
                            if (!p.selectedItemState.CanChangeSelectedItemImmediately) { scope.Step(); continue; }
                        }
                        // Selection.Update precedes ItemCheck in this same
                        // genuine Player.Update. Never insert a neutral frame
                        // merely because Select buffered a change of source.
                        bool held = p.itemAnimation > 0 || p.releaseUseItem || source.Item.autoReuse;
                        scope.Step(new Vector2(target.X * 16 + 8, target.Y * 16 + 8), held);
                    }
                }
                scope.Report("scheduler handoff automatic=" + automatic + " differentMaterial=" + different);
                timeline.Report("stack-handoff automatic=" + automatic + " differentMaterial=" + different);
                Require(scope.Goals.All(goal => goal.Complete) && scope.Events.Count == 4 && first.Item.IsAir && next.Item.stack == 2 &&
                    scope.Events.Take(2).All(entry => ReferenceEquals(entry.Item, first.Item) && entry.Slot == 12) &&
                    scope.Events.Skip(2).All(entry => ReferenceEquals(entry.Item, next.Item) && entry.Slot == 13),
                    "real exhausted source is replaced by the other exact Item with four complete bodies and no extra debit");
                Require(!automatic || scope.Events[0].Token == scope.Events[1].Token && scope.Events[2].Token != scope.Events[1].Token &&
                    scope.Events[2].Token == scope.Events[3].Token,
                    "source exhaustion creates a fresh native lease while stable successors retain their exact use");
                return scope.Events.ToArray();
            }
        }

        internal static void SchedulerPostProcessingCadence(object context, object host, object tools, object input)
        {
            foreach (string scenario in new[] { "hammer-paint", "paint-layers", "coating-effects", "scrape-paint" })
            {
                var automatic = SchedulerPostProcessingRun(context, host, tools, input, scenario, null);
                var original = SchedulerPostProcessingRun(context, host, tools, input, scenario, automatic);
                var actual = automatic.Skip(1).Select((entry, index) => unchecked(entry.World - automatic[index].World)).ToArray();
                var expected = original.Skip(1).Select((entry, index) => unchecked(entry.World - original[index].World)).ToArray();
                Console.WriteLine("BlueprintSchedulerPostProcessing scenario=" + scenario + " originalGaps=" + string.Join(",", expected) +
                    " automaticGaps=" + string.Join(",", actual) + " transitions=" + string.Join(";", automatic.Skip(1).Select((entry, index) =>
                        automatic[index].Group + "->" + entry.Group + ":" + actual[index] + "/" + expected[index])));
                Require(expected.SequenceEqual(actual), "every same- and cross-operation gap matches that original consumer's own natural clock, neutral and selection sequence: " + scenario);
                Require(original.Zip(automatic, (a, b) => a.Kind == b.Kind && a.X == b.X && a.Y == b.Y && a.Before == b.Before &&
                    a.After == b.After && a.ToolType == b.ToolType && a.SupplyType == b.SupplyType && a.SupplyBefore == b.SupplyBefore && a.SupplyAfter == b.SupplyAfter).All(same => same),
                    "paired operations reproduce every requested real shape/layer/effect and exact material or zero-material charge");
                Require(automatic.Zip(automatic.Skip(1), (a, b) => a.Group != b.Group).Any(switched => switched),
                    "the representative includes a genuine cross-operation boundary, not only stable same-type actions");
            }
        }

        private static PostProcessingEvent[] SchedulerPostProcessingRun(object context, object host, object tools, object input,
            string scenario, PostProcessingEvent[] replay)
        {
            bool automatic = replay == null;
            using (var probe = new PostProcessingProbe(context, host, tools, input))
            using (var timeline = new SchedulerTimeline(host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                p.inventory[12].SetDefaults(scenario == "hammer-paint" ? ItemID.WoodenHammer : scenario == "scrape-paint" ? ItemID.PaintScraper : ItemID.Paintbrush);
                p.inventory[13].SetDefaults(scenario == "hammer-paint" || scenario == "scrape-paint" ? ItemID.Paintbrush : ItemID.PaintRoller);
                var firstTool = p.inventory[12]; var secondTool = p.inventory[13];
                p.inventory[20].SetDefaults(scenario == "coating-effects" ? ItemID.GlowPaint : ItemID.RedPaint);
                p.inventory[20].stack = scenario == "paint-layers" ? 4 : scenario == "coating-effects" ? 2 : 1;
                var firstSupply = p.inventory[20];
                Item secondSupply = null;
                if (scenario == "coating-effects") { p.inventory[21].SetDefaults(ItemID.EchoCoating); p.inventory[21].stack = 2; secondSupply = p.inventory[21]; }
                var goals = scenario == "hammer-paint" ? new[] {
                    PostProcessingTarget("Hammer", 40, 42, 8), PostProcessingTarget("Hammer", 44, 42, 1),
                    PostProcessingTarget("TilePaint", 46, 42, firstSupply.paint, ItemID.RedPaint) } : scenario == "paint-layers" ? new[] {
                    PostProcessingTarget("TilePaint", 40, 42, firstSupply.paint, ItemID.RedPaint), PostProcessingTarget("TilePaint", 44, 42, firstSupply.paint, ItemID.RedPaint),
                    PostProcessingTarget("WallPaint", 41, 42, firstSupply.paint, ItemID.RedPaint), PostProcessingTarget("WallPaint", 45, 42, firstSupply.paint, ItemID.RedPaint) } :
                    scenario == "coating-effects" ? new[] {
                    PostProcessingTarget("TileCoating", 40, 42, 1, ItemID.GlowPaint), PostProcessingTarget("TileCoating", 44, 42, 1, ItemID.GlowPaint),
                    PostProcessingTarget("WallCoating", 41, 42, 2, ItemID.EchoCoating), PostProcessingTarget("WallCoating", 45, 42, 2, ItemID.EchoCoating) } : new[] {
                    PostProcessingTarget("TilePaint", 40, 42, 0), PostProcessingTarget("TilePaint", 44, 42, 0),
                    PostProcessingTarget("TilePaint", 46, 42, firstSupply.paint, ItemID.RedPaint) };
                var blue = new Item(); blue.SetDefaults(ItemID.BluePaint);
                foreach (var goal in goals)
                {
                    if (goal.Kind.StartsWith("Wall", StringComparison.Ordinal)) Main.tile[goal.X, goal.Y].wall = WallID.Stone;
                    else NativeToolsChecks.Tile(goal.X, goal.Y, TileID.Stone);
                    if (scenario == "scrape-paint" && goal.SupplyType == 0)
                        Require(WorldGen.paintTile(goal.X, goal.Y, blue.paint, false, false), "scrape setup has a genuine different painted foreground");
                }
                probe.Goals = goals; probe.ToolItems = new[] { firstTool, secondTool };
                probe.Supplies = secondSupply == null ? new[] { firstSupply } : new[] { firstSupply, secondSupply };
                Require(goals.All(goal => goal.SupplyType == 0 && goal.Kind != "Hammer" ?
                    p.IsInTileInteractionRange(goal.X, goal.Y, Terraria.DataStructures.TileReachCheckSettings.Simple, firstTool.tileBoost + p.blockRange) && !goal.Complete : probe.Runnable(goal)),
                    "mixed opportunity fixture starts with actual bodies, exact effects, genuine tools/supply and native reach");
                PostProcessingPlace(context, host, input, goals); probe.Advance(); probe.Begin(automatic);
                var patch = new Harmony("JueMingR.Tests.SchedulerScrapeCadence");
                try
                {
                    if (scenario == "scrape-paint")
                    {
                        patch.Patch(typeof(Player).GetMethod("PlaceThing_PaintScraper_TryScraping", F),
                            prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerScrapeBefore)),
                            postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerScrapeAfter)));
                        patch.Patch(typeof(Player).GetMethod("ApplyPaint", F),
                            prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerScrapeGroupBeforePaint)));
                    }
                    if (automatic) SetAutomatic(host, input, true);
                    for (int frame = 0; frame < 1800 && !goals.All(goal => goal.Complete); frame++)
                    {
                        if (automatic) probe.Advance();
                        else
                        {
                            Require(probe.Events.Count < replay.Length, "mixed native replay needs no extra world effect");
                            var target = replay[probe.Events.Count];
                            int slot = target.ToolType == firstTool.type ? 12 : 13;
                            if (p.selectedItem != slot)
                            {
                                if (!p.selectedItemState.HasBufferedChange) p.selectedItemState.Select(slot);
                                if (!p.selectedItemState.CanChangeSelectedItemImmediately) { probe.Advance(); continue; }
                            }
                            if (target.SupplyType != 0)
                                Require(p.FindPaintOrCoating() != null && p.FindPaintOrCoating().type == target.SupplyType,
                                    "paired exhausted-supply fixture naturally exposes the same next paint/coating, without hidden inventory edits");
                            // Only the hammer needs its real neutral recovery;
                            // paint, roller and scraper keep their own vanilla
                            // clocks. Selection never creates a default gap.
                            bool held = target.Kind == "Hammer" ? p.poundRelease : p.itemAnimation > 0 || p.releaseUseItem || p.inventory[slot].autoReuse;
                            probe.Advance(new Vector2(target.X * 16 + 8, target.Y * 16 + 8), held);
                        }
                    }
                    probe.Report("scheduler scenario=" + scenario + " automatic=" + automatic);
                    timeline.Report("postprocessing " + scenario + " automatic=" + automatic);
                    Require(goals.All(goal => goal.Complete) && probe.Events.Count >= goals.Length && firstSupply.IsAir &&
                        (secondSupply == null || secondSupply.IsAir) && firstTool.stack == 1 && secondTool.stack == 1 && Main.mouseItem.IsAir,
                        "mixed native effects reach all goals with exactly exhausted planned supply and both reusable exact tools");
                    if (scenario == "hammer-paint") Require(probe.NativeNeutrals > 0, "mixed hammer arm restores poundRelease only by actual neutral ItemCheck");
                    return probe.Events.ToArray();
                }
                finally { foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id); }
            }
        }

        private static void SchedulerScrapeBefore(Player __instance, int __0, int __1, out PostProcessingEvent __state)
        {
            __state = null; var probe = postProcessingProbe;
            if (probe == null || !probe.Watching || !ReferenceEquals(__instance, Main.LocalPlayer) ||
                !probe.Goals.Any(goal => goal.X == __0 && goal.Y == __1 && goal.SupplyType == 0 && goal.Kind == "TilePaint")) return;
            var p = __instance; var source = probe.Automatic ? GetOptional(probe.Host, "activeBuild") : null;
            var exact = source == null ? null : Get(source, "Source");
            __state = new PostProcessingEvent { Kind = "Scrape", Group = "Scrape", X = __0, Y = __1, Before = Main.tile[__0, __1].color(),
                World = Main.GameUpdateCount, Input = (long)Get(probe.Input, "Frame"), Host = checked((long)(ulong)Get(probe.Host, "projectionMatchTick")),
                Tool = p.HeldItem, ToolType = p.HeldItem.type, ToolSlot = p.selectedItem, Time = p.itemTime, ToolTime = p.toolTime, Animation = p.itemAnimation,
                Held = p.controlUseItem, Token = probe.Automatic ? (long)Get(Get(probe.Tools, "Use"), "Operation") : 0,
                Native = probe.Automatic ? (long)Get(Get(probe.Tools, "Use"), "NativeGeneration") : 0, Flight = source == null ? 0 : (long)Get(source, "Generation"),
                ExactTool = exact == null ? !probe.Automatic : ReferenceEquals(Get(exact, "Item"), p.HeldItem) && (int)Get(exact, "Slot") == p.selectedItem,
                PreviousRunnable = probe.CurrentGroup != null && probe.CurrentGroup != "Scrape" &&
                    probe.Goals.Any(goal => goal.Group == probe.CurrentGroup && probe.Runnable(goal)) };
        }
        private static void SchedulerScrapeAfter(bool __runOriginal, PostProcessingEvent __state)
        {
            var probe = postProcessingProbe;
            if (__state == null || !__runOriginal) return;
            __state.After = Main.tile[__state.X, __state.Y].color();
            if (__state.Before == __state.After) return;
            Require(__state.Before != 0 && __state.After == 0 && __state.Tool.type == ItemID.PaintScraper && __state.Tool.stack == 1 &&
                __state.Time == 0 && __state.Animation > 0 && __state.Held &&
                (!probe.Automatic || __state.Token > 0 && __state.Native > 0 && __state.Flight > 0 && __state.ExactTool && !__state.PreviousRunnable),
                "scraping is an actual zero-material original consumer effect at its own item clock and exact native permission");
            __state.Consumer = "PlaceThing_PaintScraper_TryScraping";
            probe.Events.Add(__state); probe.CurrentGroup = "Scrape";
        }
        private static void SchedulerScrapeGroupBeforePaint(Player __instance)
        {
            var probe = postProcessingProbe;
            if (probe == null || !probe.Watching || !probe.Automatic || probe.CurrentGroup != "Scrape" || !ReferenceEquals(__instance, Main.LocalPlayer)) return;
            var scraper = probe.ToolItems.Single(item => item.type == ItemID.PaintScraper);
            Require(!probe.Goals.Any(goal => goal.SupplyType == 0 && goal.Kind == "TilePaint" && !goal.Complete &&
                __instance.IsInTileInteractionRange(goal.X, goal.Y, Terraria.DataStructures.TileReachCheckSettings.Simple, scraper.tileBoost + __instance.blockRange)),
                "a supplied competing paint consumer cannot interrupt independently known remaining legal scrape goals");
        }
    }
}
