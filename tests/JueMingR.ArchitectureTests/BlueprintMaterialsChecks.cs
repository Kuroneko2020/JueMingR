using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintMaterialsChecks
    {
        internal static void Check(IList<string> failures)
        {
            try { LayersAndBodies(); PlacementCanonicalMetadata(); UnknownAndArithmetic(); }
            catch (Exception error) { failures.Add("Blueprint materials values: " + error); }
            BlueprintLibraryChecks.MaterialLane(failures);
        }
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
        internal static BlueprintPlacementCatalog Catalog()
        {
            // Deliberately different tile and item identities. These values
            // describe a pure transport fixture, not a Terraria catalog oracle.
            return new BlueprintPlacementCatalog(71, new[] {
                new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, 1001, 1001, 1, 1, 0), true),
                new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, 1002, 1002, 1, 14, 0, width:2,height:2), false, rowOffsets:new[]{0,18}),
                new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, 1009, 1009, 1, 23, 0, requiresSubstrate:true), true) },
                new Dictionary<int, BlueprintPlacementDescriptor> { { 7, new BlueprintPlacementDescriptor(BlueprintPlacementKind.Wall, 1003, 1003, 1, -1, 0, 7) } },
                new Dictionary<int, int> { { 1, 1004 } }, new Dictionary<int, int> { { 1, 1005 }, { 2, 1006 } }, 1007, 1008);
        }
        private static CaptureCell Tile(int x = 0, int y = 0)
        { return new CaptureCell { X = x, Y = y, Layers = CaptureLayer.Tile, TileType = 1 }; }
        private static void LayersAndBodies()
        {
            var catalog = Catalog(); var cell = Tile(); cell.Layers |= CaptureLayer.Wall | CaptureLayer.Wire | CaptureLayer.Actuator;
            cell.WallType = 7; cell.Wires = 15; cell.Actuator = true; cell.TilePaint = cell.WallPaint = 1; cell.FullbrightTile = cell.InvisibleTile = true;
            var all = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.StaticCell(cell, catalog, all);
            var result = all.Snapshot(1);
            Require(result.Status == BlueprintMaterialStatus.Exact && result.Total == 11 && result.KindCount == 7, "independent tile/wall/four-wire/actuator/two-coating and two-paint steps");
            Require(result.Rows.Single(r => r.ItemType == 1007).Required == 4 && !result.Rows.Any(r => r.ItemType == cell.TileType), "four colors consume four real wire identities, never TileID as ItemID");

            var target = Tile(); target.TilePaint = 1; target.HalfBrick = true;
            var current = Tile(); var paint = new BlueprintMaterialAccumulator();
            BlueprintMaterialSteps.RequiredSteps(target, new BlueprintWorldMatchSample(true, current), catalog, paint);
            var paintResult = paint.Snapshot(2);
            Require(paintResult.Total == 1 && paintResult.Rows.Single().ItemType == 1004 && BlueprintMaterialSteps.SameBody(target, new BlueprintWorldMatchSample(true, current)) == BlueprintBodyState.Same,
                "correct body with wrong paint/shape needs paint only");
            Require(BlueprintWorldMatchComparer.CompareCell(target, new BlueprintWorldMatchSample(true, current)).ConflictBits == 1,
                "zero body or tool-only material cannot weaken final world Match");
            current.TileType = 2; var conflict = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.RequiredSteps(target, new BlueprintWorldMatchSample(true, current), catalog, conflict);
            Require(conflict.Snapshot(3).Rows.Single(r => r.ItemType == 1001).Required == 1, "foreign body retains intended body material without granting replacement");

            var coated = Tile(); coated.TilePaint = 1; coated.FullbrightTile = true;
            var excess = coated; excess.InvisibleTile = true;
            var scrape = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.RequiredSteps(coated, new BlueprintWorldMatchSample(true, excess), catalog, scrape);
            Require(scrape.Snapshot(4).Rows.Single(r => r.ItemType == 1004).Required == 1 && scrape.Snapshot(4).Rows.Single(r => r.ItemType == 1005).Required == 1,
                "native coating removal first clears paint, then both coatings; retained paint and coating each need one reapplication");
            coated.Layers |= CaptureLayer.Wall; coated.WallType = 7; coated.WallPaint = 1; coated.FullbrightWall = true;
            excess = coated; excess.InvisibleWall = true;
            var behind = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.RequiredSteps(coated, new BlueprintWorldMatchSample(true, excess), catalog, behind);
            Require(behind.Snapshot(5).Rows.Single(r => r.ItemType == 1004).Required == 2 && behind.Snapshot(5).Rows.Single(r => r.ItemType == 1005).Required == 2,
                "wall coating scraping first clears the foreground; restore both saved layers after the wall operation");
            var wallOnly = coated.WithoutTile(); var blocked = new BlueprintMaterialAccumulator();
            BlueprintMaterialSteps.RequiredSteps(wallOnly, new BlueprintWorldMatchSample(true, excess), catalog, blocked);
            Require(blocked.Snapshot(6).Status == BlueprintMaterialStatus.Unresolved, "unselected painted foreground is not silently assigned a restoration target");

            var obj = new CaptureObject { Id = 1, X = 2, Y = 3, Width = 2, Height = 2, Type = 14, Reliable = true };
            var body = new BlueprintWorldObject(2, 3, 2, 2, 2, 3, 14, 0, 0, BlueprintWorldObjectFamily.Metadata, new[] { 2, 2 });
            var members = new CaptureCell[4]; var samples = new BlueprintWorldMatchSample[4]; var furniture = new BlueprintMaterialAccumulator();
            for (int i = 0; i < 4; i++)
            {
                members[i] = new CaptureCell { X = 2 + i % 2, Y = 3 + i / 2, Layers = CaptureLayer.Tile, TileType = 14, ObjectId = 1, FrameX = (short)(i % 2 * 18), FrameY = (short)(i / 2 * 18), TilePaint = 1 };
                BlueprintMaterialSteps.StaticCell(members[i], catalog, furniture);
                var actual = members[i]; actual.TilePaint = 0; samples[i] = new BlueprintWorldMatchSample(true, actual, body);
            }
            BlueprintMaterialSteps.StaticObject(obj, members[0], catalog, furniture);
            Require(furniture.Snapshot(4).Rows.Single(r => r.ItemType == 1002).Required == 1 && furniture.Snapshot(4).Rows.Single(r => r.ItemType == 1004).Required == 4,
                "one complete multi-cell furniture item, member paints separately");
            Require(BlueprintMaterialSteps.SameBody(body, members, samples, true) == BlueprintBodyState.Same, "same complete structural body survives member appearance differences");
            var fragment = samples[3].Cell; fragment.Layers = CaptureLayer.None; samples[3] = new BlueprintWorldMatchSample(true, fragment);
            Require(BlueprintMaterialSteps.SameBody(body, members, samples, true) == BlueprintBodyState.Conflict && !BlueprintMaterialSteps.EmptyObjectFootprint(body, samples),
                "same-type partial furniture is not a complete body or an empty placement footprint");
            samples[3] = default(BlueprintWorldMatchSample);
            Require(BlueprintMaterialSteps.SameBody(body, members, samples, true) == BlueprintBodyState.Unknown, "unread member remains unknown");
        }
        private static void PlacementCanonicalMetadata()
        {
            // A pure model of the original anchor-alternate mechanism: the
            // frame writer's origin/direction differs from reverse capture's
            // canonical metadata, while the complete body/material is unique.
            var actual = new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, 1002, 1002, 1, 14, 0,
                width: 2, height: 2, originY: 0, alternate: 1, direction: 2, requiresObject: true);
            var rule = new BlueprintPlacementRule(actual, false, baseX: 36, rowOffsets: new[] { 0, 18 });
            var catalog = new BlueprintPlacementCatalog(72, new[] { rule }, new Dictionary<int, BlueprintPlacementDescriptor>(),
                new Dictionary<int, int>(), new Dictionary<int, int>(), 1007, 1008);
            var cell = new CaptureCell { Layers = CaptureLayer.Tile, TileType = 14, FrameX = 36, ObjectId = 1 };
            var canonical = new CaptureObject { Id = 1, Type = 14, Width = 2, Height = 2, OriginY = 1, Style = 1, Direction = 1, Reliable = true };
            var resolved = catalog.Tile(cell, canonical);
            Require(ReferenceEquals(resolved, actual) && resolved.OriginY == 0 && resolved.Direction == 2 && resolved.Style == 0 &&
                resolved.Alternate == 1 && resolved.ConsumeItemType == 1002,
                "canonical capture metadata cannot discard a unique material or overwrite the actual direction/style/alternate/origin");
            var wrong = canonical; wrong.Width = 3;
            Require(catalog.Tile(cell, wrong) == null, "different complete footprint still rejects this material profile");
            var wrongFrame = cell; wrongFrame.FrameX = 0;
            Require(catalog.Tile(wrongFrame, canonical) == null, "canonical metadata cannot authorize a frame absent from the actual placement declaration");
            var other = new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, 1003, 1003, 1, 14, 0,
                width: 2, height: 2, originY: 1, direction: 1, requiresObject: true);
            var ambiguous = new BlueprintPlacementCatalog(73, new[] { rule, new BlueprintPlacementRule(other, false, baseX: 36, rowOffsets: new[] { 0, 18 }) },
                new Dictionary<int, BlueprintPlacementDescriptor>(), new Dictionary<int, int>(), new Dictionary<int, int>(), 1007, 1008);
            Require(ambiguous.Tile(cell, canonical) == null, "canonical origin/direction cannot choose between conflicting consumable identities");
        }
        private static void UnknownAndArithmetic()
        {
            var natural = Tile(); natural.TileType = 5;
            var unknown = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.StaticCell(natural, Catalog(), unknown);
            Require(unknown.Snapshot(1).Status == BlueprintMaterialStatus.Unresolved && unknown.Snapshot(1).Total == 0, "unmapped natural content is not an exact zero");
            var seeded = Tile(); seeded.TileType = 23;
            var conversion = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.StaticCell(seeded, Catalog(), conversion);
            Require(conversion.Snapshot(2).Status == BlueprintMaterialStatus.Unresolved && conversion.Snapshot(2).Rows.Single().ItemType == 1009,
                "a known seed does not claim a complete from-air body recipe when the substrate step is unimplemented");
            var present = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.RequiredSteps(seeded, new BlueprintWorldMatchSample(true, seeded), Catalog(), present);
            Require(present.Snapshot(3).Status == BlueprintMaterialStatus.Exact && present.Snapshot(3).Total == 0,
                "an already matching seeded body does not need another seed or an unresolved construction step");
            var toolsOnly = Tile(); toolsOnly.HalfBrick = true;
            var tools = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.RequiredSteps(toolsOnly, new BlueprintWorldMatchSample(true, Tile()), Catalog(), tools);
            Require(tools.Snapshot(1).Status == BlueprintMaterialStatus.Exact && tools.Snapshot(1).Total == 0, "proven hammer-only change is an explained zero");
            var liquid = new CaptureCell { Layers = CaptureLayer.Liquid, LiquidAmount = 128, LiquidKind = 0 };
            BlueprintMaterialSteps.StaticCell(liquid, Catalog(), tools);
            Require(tools.Snapshot(2).Status == BlueprintMaterialStatus.Unresolved, "liquid amount is not bucket count");

            var large = new BlueprintMaterialAccumulator(); large.Add(1001, (long)int.MaxValue + 1); large.Add(1001, 17);
            Require(large.Snapshot(1).Total == (long)int.MaxValue + 18, "direct counts remain exact beyond int");
            large.Add(1001, long.MaxValue);
            Require(large.Snapshot(2).Status == BlueprintMaterialStatus.Unresolved && large.Snapshot(2).Rows.All(r => r.Required >= 0), "checked overflow cannot wrap or prove enough");

            var inventory = new BlueprintMaterialInventory(1, 2, 3, true, new[] {
                new BlueprintMaterialInventorySlot(0,1001,30,true), new BlueprintMaterialInventorySlot(49,1001,50,true),
                new BlueprintMaterialInventorySlot(1,1001,999,false) });
            var one = new BlueprintMaterialAccumulator(); one.Add(1001, 50);
            var both = new BlueprintMaterialAccumulator(); both.Add(1001, 50); both.Add(1001, 50);
            Require(one.Snapshot(1, inventory).Rows.Single().Missing == 0 && both.Snapshot(2, inventory).Rows.Single().Required == 100 &&
                both.Snapshot(2, inventory).Rows.Single().Held == 80 && both.Snapshot(2, inventory).Rows.Single().Missing == 20,
                "each independent scope sees one shared inventory; all merges need before subtracting");
            Require(!one.Snapshot(3, new BlueprintMaterialInventory(1, 3, 4, false, Array.Empty<BlueprintMaterialInventorySlot>())).Rows.Single().InventoryReady,
                "inventory failure is distinct from a successful zero count");
        }
    }
}
