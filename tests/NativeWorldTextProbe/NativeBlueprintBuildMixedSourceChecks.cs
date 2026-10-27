using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        // Anonymous geometry reproduces the owner's large-template mechanism,
        // without copying private cells: one physical member block owns a long
        // ordinary proof and more object proofs than one 64-range discovery turn.
        // Every source body is made by original writers and saved by real capture.
        private static void MixedSourceContinuation(object context, object host, object tools, object input)
        {
            const int width = 1000, chairs = 96, sx = 20, sy = 20, ox = 44, oy = 80, stoneIndex = 998;
            const int records = width + chairs * 3, targets = width * 2 + chairs * 3;
            var camera = Main.screenPosition;
            using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 1280, 200))
            try
            {
                var p = Reset(context, host, tools, input);
                foreach (var item in p.inventory) item.TurnToAir();
                for (int i = 0; i < width; i++)
                {
                    Require(WorldGen.PlaceTile(sx + i, sy, i == stoneIndex ? TileID.Stone : TileID.Dirt, true, true),
                        "native anonymous ordinary source placement succeeds");
                    WorldGen.PlaceWall(sx + i, sy, WallID.Wood, true);
                    Require(Main.tile[sx + i, sy].wall == WallID.Wood, "ordinary source really has its independent Wood wall layer");
                }
                for (int i = 0; i < chairs; i++)
                {
                    int x = sx + i * 3;
                    Require(WorldGen.PlaceTile(x, sy + 6, TileID.Stone, true, true) &&
                        WorldGen.PlaceObject(x, sy + 5, TileID.Chairs, true, 0, direction: -1),
                        "original chair writer accepts each independent real Stone anchor");
                    Require(Main.tile[x, sy + 4].active() && Main.tile[x, sy + 5].active() &&
                        Main.tile[x, sy + 4].type == TileID.Chairs && Main.tile[x, sy + 5].type == TileID.Chairs,
                        "source chair contributes both real members, not a synthetic object declaration");
                }
                var library = (LibraryState)Get(host, "Library");
                var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library,
                    () => Step(context, input), sx, sy, width, 10, "匿名混合源续行");
                Require(entry.ContentCount == records && records + chairs < CaptureChunk.MaximumRecords,
                    "known native geometry produces the full 1288-cell source below one capture chunk's combined record ceiling");
                var instance = Place(context, host, input, entry, ox, oy);
                Until(context, input, () => GetOptional(instance, "Match") != null &&
                    (int)Get(Get(instance, "Match"), "Current") == EligibilityRanges(instance).Length);
                var match = Get(instance, "Match"); var summary = (CaptureSummary)Get(instance, "Summary");
                var physical = ((IEnumerable)Get(instance, "SourceRanges")).Cast<object>().ToArray();
                var logical = ((IEnumerable)Get(instance, "MatchRanges")).Cast<object>().ToArray();
                // Capture trims the three selected empty tail rows: native
                // content spans sy through sy+6, independently seven rows.
                Console.WriteLine("BlueprintBuild mixed source metadata dimensions=" + summary.Width + "x" + summary.Height +
                    " records=" + entry.ContentCount + " physicalRanges=" + physical.Length + " logicalRanges=" + logical.Length +
                    " unreliable=" + summary.UnreliableObjectCount);
                Require(summary.Width == width && summary.Height == 7 && summary.UnreliableObjectCount == 0 &&
                    physical.Length == 1 && logical.Length == chairs + 1 && logical.Length > 64 &&
                    logical.Count(r => (int)Get(r, "ObjectId") == 0) == 1 &&
                    logical.Where(r => (int)Get(r, "ObjectId") > 0).Select(r => (int)Get(r, "ObjectId")).Distinct().Count() == chairs &&
                    logical.All(r => (long)Get(r, "Block") == (long)Get(physical[0], "Block") &&
                        (int)Get(r, "ObservationGroup") == (int)Get(physical[0], "ObservationGroup")),
                    "actual saved source metadata proves one physical member chunk with one ordinary and 96 reliable independent object ranges");
                Require((long)Get(instance, "TotalConstructionLayerCount") == targets && (long)Get(match, "Targets") == targets &&
                    (long)Get(match, "Matches") == 0 && (long)Get(match, "Unknown") == 0,
                    "blank received destination has exactly the independently counted 2288 Tile/Wall targets");

                // Warm the real material view before enabling construction. A
                // status label alone is insufficient: all range contributions
                // and the publisher must refer to the same completed revision.
                Call(host, "SetMaterialViewDemand", true);
                BlueprintMaterialSnapshot stat = null, world = null;
                Until(context, input, () =>
                {
                    stat = (BlueprintMaterialSnapshot)Call(host, "MaterialAsset", entry);
                    return stat.Status != BlueprintMaterialStatus.Pending;
                });
                var totals = Get(match, "Materials");
                long projection = ((BlueprintProjectionIdentity)Get(instance, "Identity")).ProjectionGeneration;
                Until(context, input, () =>
                {
                    world = (BlueprintMaterialSnapshot)Call(host, "MaterialWorld", projection);
                    return (int)Get(totals, "Current") == (int)Get(totals, "Expected") && (int)Get(totals, "Updating") == 0 &&
                        GetOptional(host, "materialPublishing") == null && ReferenceEquals(GetOptional(host, "materialPublishingTotals"), totals) &&
                        (long)Get(host, "materialPublishingRevision") == (long)Get(totals, "Revision") && world.InventoryReady &&
                        world.Status != BlueprintMaterialStatus.Pending && EligibilityRanges(instance).All(r =>
                            (bool)Get(r, "Current") && !(bool)Get(r, "Reading") && !(bool)Get(r, "Demand"));
                });
                Require(stat.Status == BlueprintMaterialStatus.Exact && world.Status == BlueprintMaterialStatus.Exact &&
                    stat.KindCount == 4 && world.KindCount == 4 && stat.Total == width * 2 + chairs * 2 && world.Total == stat.Total,
                    "complete real static/world material views are Exact before enabling, with furniture units counted once per complete chair");
                foreach (var bill in new[] { stat, world })
                {
                    MixedSourceBill(bill, ItemID.DirtBlock, width - 1);
                    MixedSourceBill(bill, ItemID.StoneBlock, chairs + 1);
                    MixedSourceBill(bill, ItemID.WoodWall, width);
                    MixedSourceBill(bill, ItemID.WoodenChair, chairs);
                }
                Require(world.Rows.All(r => r.Held == 0 && r.Missing == r.Required), "initial supply is genuinely absent for all four independently known materials");

                int targetX = ox + stoneIndex;
                // Pre-existing fixture support is explicitly outside the source
                // targets at this row. R must neither invent it nor consume it.
                for (int x = targetX - 4; x <= targetX + 2; x++)
                    Require(WorldGen.PlaceTile(x, oy + 1, TileID.WoodBlock, true, true), "original writer establishes the player's floor and the late Stone target's legal support");
                p.position = new Vector2((targetX - 3) * 16, (oy + 1) * 16 - p.height); p.velocity = Vector2.Zero;
                Main.screenPosition = new Vector2((targetX - 10) * 16, (oy - 10) * 16);
                p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 1;
                Require(p.inventory.Count(i => !i.IsAir) == 1 && !Main.tile[targetX, oy].active() &&
                    Collision.EmptyTile(targetX, oy) && Main.tile[targetX, oy + 1].type == TileID.WoodBlock,
                    "only one original Stone is available; its sole nearby source target is empty and independently supported");
                long source = (long)Get(host, "ProjectionMatchSourceSteps"), requests = (long)Get(host, "ProjectionMatchSourceRequests"),
                    checks = (long)Get(host, "BuildCandidateChecks"), started = (long)Get(host, "BuildUsesStarted"), unknown = (long)Get(host, "BuildUnknownResults");
                var sourceBefore = library.DebugProjectionSourceStatistics;
                int updates = 0, readyUpdates = 0, maxPins = 0;
                int readyUpdateLimit = 8 * ((records + 191) / 192 + (width * 2 + chairs * 2 + 63) / 64 + (logical.Length + 63) / 64);
                bool partial = false, mixedBatch = false, declinedWhileFull = false;
                SetAutomatic(host, input, true);
                Until(context, input, () =>
                {
                    updates++;
                    MixedSourceObserve(host, instance, ref readyUpdates, ref maxPins, ref partial, ref mixedBatch, ref declinedWhileFull);
                    // Eight complete real-cell traversals include bounded world
                    // resampling and Tile/Wall candidate walks. It excludes the
                    // tens of thousands of repeated source steps in the stall;
                    // no cache, per-update budget or wall-clock promise changes.
                    Require((long)Get(host, "ProjectionMatchSourceSteps") - source <= 8L * records,
                        "mixed source continuation must finish within eight full real-cell traversal equivalents: steps=" +
                        ((long)Get(host, "ProjectionMatchSourceSteps") - source) + " limit=" + (8L * records));
                    // Charge only updates with a retained source or an active
                    // original read, not asynchronous file-receipt waiting.
                    // Eight bounded passes allow original world invalidations;
                    // rediscovering 97 ranges for each Tile/Wall ordinal does
                    // not fit. This is a work bound, never a speed/FPS claim.
                    Require(readyUpdates <= readyUpdateLimit,
                        "four active source walks must progress without rediscovering every exhausted object for each candidate: readyUpdates=" +
                        readyUpdates + " limit=" + readyUpdateLimit);
                    return p.inventory[12].IsAir && ProgressDormant(instance) && !(bool)Get(Get(tools, "Use"), "Active");
                });
                Console.WriteLine("BlueprintBuild mixed source observed partial=" + partial + " mixedBatch=" + mixedBatch + " maxPins=" + maxPins +
                    " declinedWhileFull=" + declinedWhileFull + " updates=" + updates + " readyUpdates=" + readyUpdates +
                    " sourceSteps=" + ((long)Get(host, "ProjectionMatchSourceSteps") - source) + " actualStone=" + Main.tile[targetX, oy].active() + "/" + Main.tile[targetX, oy].type +
                    " stock=" + p.inventory[12].stack);
                // Four slots are a capacity ceiling, not a required occupancy:
                // fair consumption may release sources before this outer probe.
                // The separate full-cache case proves all four live pins; this
                // case proves competing long/object DTOs and bounded progress.
                Require(partial && mixedBatch && maxPins > 0 && maxPins <= 4,
                    "actual immutable source DTOs crossed Updates and shared a batch with object DTOs while retained sources stayed within four slots");
                Require(Main.tile[targetX, oy].active() && Main.tile[targetX, oy].type == TileID.Stone &&
                    Enumerable.Range(ox, width).Count(x => Main.tile[x, oy].active()) == 1 &&
                    Enumerable.Range(ox, width).All(x => Main.tile[x, oy].wall == 0) &&
                    Enumerable.Range(0, chairs).All(i => !Main.tile[ox + i * 3, oy + 4].active() && !Main.tile[ox + i * 3, oy + 5].active() &&
                        !Main.tile[ox + i * 3, oy + 6].active()) &&
                    Enumerable.Range(targetX - 4, 7).All(x => Main.tile[x, oy + 1].active() && Main.tile[x, oy + 1].type == TileID.WoodBlock) &&
                    Scene(host).Contains(instance) && (long)Get(match, "Matches") == 1 && (long)Get(match, "Unknown") == 0 &&
                    (long)Get(host, "BuildUsesStarted") == started + 1 && (long)Get(host, "BuildUnknownResults") == unknown,
                    "real Player.Update consumes exactly one Stone on the late original target; missing Dirt/walls/chairs/support targets remain, fixture Wood remains, and no unknown or false retirement appears");
                var sourceAfter = library.DebugProjectionSourceStatistics;
                Console.WriteLine("PASS BlueprintBuild mixed-source continuation records=" + records + " reliableObjects=" + chairs + " physicalRanges=" + physical.Length +
                    " logicalRanges=" + logical.Length + " materialWarm=true targets=" + targets + " actualStone=1 unknownDelta=0 updates=" + updates +
                    " readyUpdates=" + readyUpdates + " readyUpdateLimit=" + readyUpdateLimit + " partialRead=" + partial + " mixedSourceDTOBatch=" + mixedBatch +
                    " maxPins=" + maxPins + " fullPinsDeclinedLongSource=" + declinedWhileFull +
                    " sourceSteps=" + ((long)Get(host, "ProjectionMatchSourceSteps") - source) + " sourceStepLimit=" + (8L * records) +
                    " sourceRequests=" + ((long)Get(host, "ProjectionMatchSourceRequests") - requests) + " candidateChecks=" + ((long)Get(host, "BuildCandidateChecks") - checks) +
                    " memberBlocks=" + (sourceAfter.MemberBlocks - sourceBefore.MemberBlocks) + " memberRecords=" + (sourceAfter.MemberRecords - sourceBefore.MemberRecords));
            }
            finally
            {
                SetAutomatic(host, input, false); Call(host, "SetMaterialViewDemand", false); Call(host, "CancelStaticMaterialViews");
                Call(host, "ClearProjection"); Main.screenPosition = camera;
            }
        }

        private static void MixedSourceBill(BlueprintMaterialSnapshot bill, int item, long required)
        { Require(bill.Rows.Single(r => r.ItemType == item).Required == required, "material row agrees with independently constructed native source geometry: item=" + item + " required=" + required); }

        private static void MixedSourceObserve(object host, object instance, ref int readyUpdates, ref int maxPins, ref bool partial, ref bool mixedBatch, ref bool declinedWhileFull)
        {
            var pins = ((IEnumerable)Get(host, "buildSources")).Cast<object>().ToArray();
            maxPins = Math.Max(maxPins, pins.Length);
            Require(pins.Length <= 4, "mixed source keeps the existing four-part retained-source limit");
            bool ready = pins.Length != 0;
            foreach (var work in ((IDictionary)Get(host, "projectionMatchWork")).Values.Cast<object>())
            {
                if (!ReferenceEquals(Get(work, "Owner"), instance)) continue;
                // Completed receipt slots stay null in this finite work list;
                // they are no longer source DTOs and must not enter the probe.
                var parts = ((IEnumerable)Get(work, "Parts")).Cast<BlueprintProjectionSourcePart>().Where(p => p != null).ToArray();
                mixedBatch |= parts.Any(p => p.LogicalObjectId == 0) && parts.Any(p => p.LogicalObjectId > 0);
                var read = GetOptional(work, "Read"); if (read == null || GetOptional(read, "Batch") == null) continue;
                ready = true;
                var part = (BlueprintProjectionSourcePart)Get(read, "Batch");
                int index = (int)Get(read, "CellIndex");
                if (part.LogicalObjectId != 0 || index <= 0 || index >= part.Window.Cells.Count) continue;
                partial = true;
                declinedWhileFull |= pins.Length == 4 && !pins.Any(p => ReferenceEquals(Get(p, "Range"), Get(read, "Range")));
            }
            if (ready) readyUpdates++;
        }
    }
}
