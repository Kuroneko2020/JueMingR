using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintSubstitutionChecks
    {
        private static readonly BlueprintSubstitutionPolicy On = new BlueprintSubstitutionPolicy(true, 4095, 8);
        internal static void Check(IList<string> failures)
        {
            try { Policy(); CategoryPairs(); Walls(); PlatformsAndAppearance(); PlanterBoxes(); Objects(); Doors(); SharedStock(); EffectiveOutput(); }
            catch (Exception error) { failures.Add("Blueprint substitution pure values: " + error); }
        }
        private static void Require(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
        // Synthetic transport values prove the relation/accounting contract.
        // They do not certify Terraria's native membership or placement gates.
        private static BlueprintSubstitutionMember Member(BlueprintSubstitutionCategory category, int item, int tile, int style,
            int width = 1, int height = 1, int direction = 0, int physical = 1, int alternate = 0)
        {
            bool door = category == BlueprintSubstitutionCategory.Door, platform = category == BlueprintSubstitutionCategory.Platform,
                planter = category == BlueprintSubstitutionCategory.PlanterBox;
            var p = new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, item, item, 1, tile, style,
                width: width, height: height, alternate: alternate, direction: direction, requiresObject: !platform && !planter, platform: platform, planterBox: planter);
            var rows = new int[height]; for (int i = 0; i < rows.Length; i++) rows[i] = i * 18;
            var rule = new BlueprintPlacementRule(p, false, platform || door || planter ? 0 : style * width * 18, 0, rowOffsets: rows, door: door);
            var obj = new CaptureObject { Id = 1, Type = (ushort)tile, Style = style, Width = width, Height = height,
                PlacementStyle = platform ? style * 27 : style, Alternate = alternate, Direction = direction, Reliable = true };
            return new BlueprintSubstitutionMember(category, rule, new[] { width, height, direction, physical }, obj);
        }
        private static BlueprintSubstitutionMember Wall(int item, int wall, bool safe)
        { return new BlueprintSubstitutionMember(BlueprintSubstitutionCategory.Wall,
            new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Wall, item, item, 1, -1, 0, wall), true), new[] { 1 }, safeWall: safe); }
        private static BlueprintPlacementCatalog Catalog(params BlueprintSubstitutionMember[] members)
        {
            var rules = new List<BlueprintPlacementRule>(); var walls = new Dictionary<int, BlueprintPlacementDescriptor>();
            foreach (var m in members) if (m.Placement.Kind == BlueprintPlacementKind.Wall) walls[m.Placement.WallType] = m.Placement; else rules.Add(m.Rule);
            return new BlueprintPlacementCatalog(31, rules, walls, new Dictionary<int, int> { { 1, 9001 } },
                new Dictionary<int, int> { { 1, 9002 }, { 2, 9003 } }, 9004, 9005,
                substitution: new BlueprintSubstitutionCatalog(31, members));
        }
        private static void Policy()
        {
            Require((int)BlueprintSubstitutionCategory.Workbench == 0 && (int)BlueprintSubstitutionCategory.Wall == 11,
                "persisted categories retain the approved twelve-bit order");
            Require(!default(BlueprintSubstitutionPolicy).Allows(BlueprintSubstitutionCategory.Table), "default exact mode is off");
            var p = new BlueprintSubstitutionPolicy(true, 1 << 10, 9);
            Require(p.Allows(BlueprintSubstitutionCategory.Platform) && !p.Allows(BlueprintSubstitutionCategory.Wall) && !p.Equals(On), "category and policy revision are explicit");
            bool threw = false; try { new BlueprintSubstitutionPolicy(true, 4096); } catch (ArgumentOutOfRangeException) { threw = true; }
            Require(threw, "unknown category bits are rejected rather than truncated");
        }
        private static void CategoryPairs()
        {
            // Independent twelve-category matrix: same group permits an
            // additional body; every other approved category remains distinct.
            var entries = new List<BlueprintSubstitutionMember>();
            for (int i = 0; i < 11; i++)
            {
                var category = (BlueprintSubstitutionCategory)i;
                entries.Add(Member(category, 100 + i * 2, 1000 + i, 0));
                entries.Add(Member(category, 101 + i * 2, 1000 + i, 1));
            }
            entries.Add(Wall(190, 80, true)); entries.Add(Wall(191, 81, true));
            var catalog = Catalog(entries.ToArray());
            for (int i = 0; i < 12; i++)
            {
                var a = entries[i * 2]; var b = entries[i * 2 + 1];
                Require(catalog.Substitution.CandidatesForItem(a.Placement, b.Placement.UseItemType, On).Count > 0, "approved category positive pair " + i);
                Require(catalog.Substitution.CandidatesForItem(a.Placement, b.Placement.UseItemType,
                    new BlueprintSubstitutionPolicy(true, 4095 ^ 1 << i)).Count == 0, "category off preserves exact-only relation " + i);
                Require(catalog.Substitution.CandidatesForItem(a.Placement, entries[((i + 1) % 12) * 2].Placement.UseItemType, On).Count == 0,
                    "distinct categories do not merge " + i);
            }
        }
        private static void Walls()
        {
            var catalog = Catalog(Wall(701, 70, true), Wall(702, 71, true), Wall(703, 72, false), Wall(704, 73, false));
            var safe = new CaptureCell { Layers = CaptureLayer.Wall, WallType = 70 };
            var other = safe; other.WallType = 71;
            Require(BlueprintWorldMatchComparer.CompareCell(safe, new BlueprintWorldMatchSample(true, other), catalog: catalog, policy: On).MatchBits == 2,
                "ordinary safe walls can replace each other");
            foreach (var pair in new[] { new[] { 70, 72 }, new[] { 72, 70 }, new[] { 72, 73 } })
            {
                var target = safe; target.WallType = (ushort)pair[0]; other.WallType = (ushort)pair[1];
                Require(!catalog.Substitution.AllowsWall(pair[0], pair[1], On) &&
                    BlueprintMaterialSteps.SameBody(target, new BlueprintWorldMatchSample(true, other), catalog, true, On) == BlueprintBodyState.Conflict &&
                    BlueprintWorldMatchComparer.CompareCell(target, new BlueprintWorldMatchSample(true, other), catalog: catalog, policy: On).ConflictBits == 2,
                    "safe-to-unsafe, unsafe-to-safe and unsafe-to-different-unsafe are all forbidden");
            }
            safe.WallType = 72;
            Require(catalog.QualifyWall(72).Disposition == BlueprintTargetDisposition.Construction &&
                BlueprintWorldMatchComparer.CompareCell(safe, new BlueprintWorldMatchSample(true, safe), catalog: catalog, policy: On).MatchBits == 2 &&
                catalog.Substitution.MaterialCandidates(703, On).Count == 0,
                "unsafe exact production and Match survive exclusion from the replacement relation");
            CaptureCell execution; CaptureObject? obj;
            Require(catalog.Substitution.TryCreateExecution(safe, null, catalog.Wall(72), On, out execution, out obj) && execution.WallType == 72,
                "unsafe exact execution view never depends on replacement membership");
            other = safe; other.WallType = 70; other.WallPaint = 1;
            safe.WallType = 71;
            Require(BlueprintWorldMatchComparer.CompareCell(safe, new BlueprintWorldMatchSample(true, other), catalog: catalog, policy: On).ConflictBits == 2,
                "replacement walls retain paint and coating completion requirements");
        }
        private static void PlatformsAndAppearance()
        {
            var a = Member(BlueprintSubstitutionCategory.Platform, 801, 19, 0);
            var b = Member(BlueprintSubstitutionCategory.Platform, 802, 19, 1);
            var catalog = Catalog(a, b);
            var target = new CaptureCell { Layers = CaptureLayer.Tile, TileType = 19, FrameY = 0, Slope = 1, TilePaint = 1 };
            var actual = target; actual.FrameX = 36; actual.FrameY = 18; actual.TilePaint = 0;
            var sample = new BlueprintWorldMatchSample(true, actual, compareFrames: true, semanticFrameX: 0, semanticFrameY: 18, frameKind: BlueprintWorldFrameKind.Platform18);
            Require(BlueprintMaterialSteps.SameBody(target, sample, catalog, policy: On) == BlueprintBodyState.Same &&
                BlueprintWorldMatchComparer.CompareCell(target, sample, catalog: catalog, policy: On).ConflictBits == 1,
                "replacement body is independent of the remaining paint step");
            var bill = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.RequiredSteps(target, sample, catalog, bill, policy: On);
            var snapshot = bill.Snapshot(1);
            Require(snapshot.Total == 1 && snapshot.Rows[0].ItemType == 9001, "accepted platform body does not charge another platform; paint remains");
            actual.TilePaint = 1; sample = new BlueprintWorldMatchSample(true, actual, compareFrames: true, semanticFrameX: 0, semanticFrameY: 18, frameKind: BlueprintWorldFrameKind.Platform18);
            Require(BlueprintWorldMatchComparer.CompareCell(target, sample, catalog: catalog, policy: On).MatchBits == 1 && target.FrameY == 0,
                "legal neighbor frames and replacement style match without rewriting the template");
            actual.FrameX = 487;
            Require(!catalog.Substitution.AllowsTile(target, actual, On), "malformed platform frames cannot borrow replacement equivalence");
            actual = target; actual.FrameY = 49 * 18;
            Require(!catalog.Substitution.AllowsTile(target, actual, On) && !catalog.Substitution.AllowsTile(actual, target, On),
                "an absent/excluded product cannot be used as either endpoint");
            Require(BlueprintWorldMatchComparer.CompareCell(actual, new BlueprintWorldMatchSample(true, actual, compareFrames: true,
                semanticFrameX: 0, semanticFrameY: actual.FrameY, frameKind: BlueprintWorldFrameKind.Platform18), catalog: catalog, policy: On).MatchBits == 1,
                "an excluded platform's exact frame still matches");
            Require(BlueprintMaterialSteps.SameBody(target, default(BlueprintWorldMatchSample), catalog, policy: On) == BlueprintBodyState.Unknown,
                "missing observation never becomes replacement success");
        }
        private static BlueprintWorldObject Body(BlueprintSubstitutionMember m, int x, int y, int art)
        {
            short frameX, frameY; m.Rule.TryMemberFrame(0, 0, out frameX, out frameY);
            return new BlueprintWorldObject(x, y, m.Prototype.Width, m.Prototype.Height, x, y, (ushort)m.Placement.TileType,
                m.Prototype.Style, m.Prototype.Direction, BlueprintWorldObjectFamily.Metadata, new[] { art }, (long)(ushort)frameX << 32 | (ushort)frameY);
        }
        private static void PlanterBoxes()
        {
            // The independent .8 source declares eight direct item styles and
            // a four-X connection framer, with no TileObjectData object. Native
            // checks certify the real catalog; these values test its consumers.
            var entries = new BlueprintSubstitutionMember[10];
            for (int i = 0; i < 8; i++) entries[i] = Member(BlueprintSubstitutionCategory.PlanterBox, 3215 + i, 380, i);
            entries[8] = Member(BlueprintSubstitutionCategory.Platform, 94, 19, 0);
            entries[9] = Member(BlueprintSubstitutionCategory.Table, 222, 78, 0);
            var catalog = Catalog(entries);
            var target = new CaptureCell { X = 5, Y = 7, Layers = CaptureLayer.Tile, TileType = 380, FrameX = 18 };
            for (int style = 0; style < 8; style++)
            {
                var source = target; source.FrameY = (short)(style * 18);
                var qualification = catalog.QualifyTile(source);
                Require(qualification.Disposition == BlueprintTargetDisposition.Construction && qualification.Resolution == BlueprintPlacementResolutionKind.Exact &&
                    qualification.Placement.ConsumeItemType == 3215 + style && qualification.Placement.PlanterBox &&
                    !qualification.Placement.Platform && !qualification.Placement.RequiresObject,
                    "each planter style retains exact one-cell production without a platform or metadata-object claim");
                Require(catalog.Substitution.MaterialCandidates(3215 + style, On).Count == 8, "all eight planter items share one planter material pool");
                for (int slot = 0; slot < 4; slot++)
                {
                    var actual = source; actual.FrameX = (short)(slot * 18);
                    var sample = new BlueprintWorldMatchSample(true, actual, compareFrames: true,
                        semanticFrameX: 0, semanticFrameY: actual.FrameY, frameKind: BlueprintWorldFrameKind.PlanterBox18);
                    Require(BlueprintWorldMatchComparer.CompareCell(source, sample, catalog: catalog).MatchBits == 1 &&
                        BlueprintMaterialSteps.SameBody(source, sample, catalog) == BlueprintBodyState.Same,
                        "four legal connection frames preserve exact planter material when replacement is off");
                    Require(BlueprintWorldMatchComparer.CompareCell(target, sample, catalog: catalog, policy: On).MatchBits == 1,
                        "all admitted planter styles and connection frames can satisfy the original planter body");
                }
            }
            CaptureCell execution; CaptureObject? executionObject;
            Require(catalog.Substitution.TryCreateExecution(target, null, entries[1].Placement, On, out execution, out executionObject) &&
                execution.TileType == 380 && execution.FrameY == 18 && !executionObject.HasValue && target.FrameY == 0,
                "planter execution changes a private style value and keeps Object=null");
            var replacement = target; replacement.FrameX = 54; replacement.FrameY = 18; replacement.TilePaint = 1;
            var replacementSample = new BlueprintWorldMatchSample(true, replacement, compareFrames: true,
                semanticFrameX: 0, semanticFrameY: 18, frameKind: BlueprintWorldFrameKind.PlanterBox18);
            var bill = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.RequiredSteps(target, replacementSample, catalog, bill, policy: On);
            Require(BlueprintMaterialSteps.SameBody(target, replacementSample, catalog, policy: On) == BlueprintBodyState.Same &&
                !bill.Counts.ContainsKey(3215) && BlueprintWorldMatchComparer.CompareCell(target, replacementSample, catalog: catalog, policy: On).ConflictBits == 1,
                "accepted planter body does not consume another planter while paint remains incomplete");
            foreach (int invalid in new[] { -18, 9, 72 })
            {
                var malformed = replacement; malformed.FrameX = (short)invalid;
                var sample = new BlueprintWorldMatchSample(true, malformed, compareFrames: true,
                    semanticFrameX: 0, semanticFrameY: malformed.FrameY, frameKind: BlueprintWorldFrameKind.PlanterBox18);
                Require(BlueprintWorldMatchComparer.CompareCell(target, sample, catalog: catalog, policy: On).UnknownBits == 1 &&
                    BlueprintMaterialSteps.SameBody(target, sample, catalog, policy: On) == BlueprintBodyState.Unknown,
                    "planter framing cannot borrow the platform's extra X slots or malformed raw values");
            }
            var platform = new CaptureCell { Layers = CaptureLayer.Tile, TileType = 19 };
            var pot = new CaptureCell { Layers = CaptureLayer.Tile, TileType = 78 };
            Require(!catalog.Substitution.AllowsTile(target, platform, On) && !catalog.Substitution.AllowsTile(platform, target, On) &&
                !catalog.Substitution.AllowsTile(target, pot, On), "planter boxes never merge with platforms or clay pots");
        }
        private static void Objects()
        {
            var a = Member(BlueprintSubstitutionCategory.Chair, 901, 15, 0, 1, 2, 1);
            var b = Member(BlueprintSubstitutionCategory.Chair, 902, 15, 1, 1, 2, 1);
            var wrong = Member(BlueprintSubstitutionCategory.Chair, 903, 15, 2, 1, 2, 2);
            var shape = Member(BlueprintSubstitutionCategory.Chair, 904, 15, 3, 1, 2, 1, 2);
            var catalog = Catalog(a, b, wrong, shape);
            var source = Body(a, 5, 7, 100); var actual = Body(b, 5, 7, 999);
            var targets = new CaptureCell[2]; var world = new BlueprintWorldMatchSample[2];
            for (int i = 0; i < 2; i++)
            {
                targets[i] = new CaptureCell { X = 5, Y = 7 + i, ObjectId = 1, Layers = CaptureLayer.Tile, TileType = 15, FrameY = (short)(i * 18) };
                var cell = targets[i]; cell.FrameX = 18; world[i] = new BlueprintWorldMatchSample(true, cell, actual);
            }
            Require(!source.SamePlacement(actual) && catalog.Substitution.AllowsObject(source, actual, On) &&
                BlueprintWorldMatchComparer.CompareObject(source, targets, world, true, catalog, On) == BlueprintWorldMatchState.Match &&
                BlueprintMaterialSteps.SameBody(source, targets, world, true, catalog, On) == BlueprintBodyState.Same,
                "explicit physical relation ignores art identity while strict SamePlacement remains strict");
            Require(!catalog.Substitution.AllowsObject(source, Body(wrong, 5, 7, 100), On) &&
                !catalog.Substitution.AllowsObject(source, Body(shape, 5, 7, 100), On), "wrong direction and attachment structure remain incompatible");
            var original = a.Prototype; original.X = 5; original.Y = 7;
            CaptureCell execution; CaptureObject? executionObject;
            Require(catalog.Substitution.TryCreateExecution(targets[0], original, b.Placement, On, out execution, out executionObject) &&
                execution.FrameX == 18 && executionObject.Value.Style == 1 && targets[0].FrameX == 0 && original.Style == 0,
                "concrete execution uses candidate raw grid and prototype; source values remain unchanged");
            world[1] = world[0];
            Require(BlueprintWorldMatchComparer.CompareObject(source, targets, world, true, catalog, On) != BlueprintWorldMatchState.Match,
                "duplicate members cannot assemble a whole replacement object");
            var fragment = targets[1]; fragment.FrameX = 18;
            world[1] = new BlueprintWorldMatchSample(true, fragment, Body(b, 5, 8, 999));
            Require(BlueprintMaterialSteps.SameBody(source, targets, world, true, catalog, On) != BlueprintBodyState.Same,
                "different actual object fragments cannot share one body proof");
        }
        private static void Doors()
        {
            var a = Member(BlueprintSubstitutionCategory.Door, 1001, 10, 0, 1, 3);
            var b = Member(BlueprintSubstitutionCategory.Door, 1002, 10, 1, 1, 3);
            var catalog = Catalog(a, b);
            var source = new BlueprintWorldObject(5, 7, 1, 3, 5, 7, 10, 0, 0, BlueprintWorldObjectFamily.Door, null);
            var observed = new BlueprintWorldObject(4, 7, 2, 3, 5, 7, 10, 1, 0, BlueprintWorldObjectFamily.Door, null);
            var target = new CaptureCell[3]; var world = new List<BlueprintWorldMatchSample>(); var union = new BlueprintWorldMatchSample[9];
            for (int row = 0; row < 3; row++)
            {
                target[row] = new CaptureCell { X = 5, Y = 7 + row, Layers = CaptureLayer.Tile, TileType = 10, FrameY = (short)(row * 18), ObjectId = 1, TilePaint = 2 };
                for (int col = 0; col < 3; col++)
                {
                    bool active = col < 2;
                    var cell = new CaptureCell { X = 4 + col, Y = 7 + row, Layers = active ? CaptureLayer.Tile : CaptureLayer.None,
                        TileType = 11, FrameX = (short)(36 + col * 18), FrameY = (short)(54 + row * 18), TilePaint = 2 };
                    var raw = new BlueprintWorldTileWriteState(cell.TileType, cell.FrameX, cell.FrameY, (ushort)(active ? 34 : 0), 0);
                    var sample = new BlueprintWorldMatchSample(true, cell, active ? observed : null, writeState: raw);
                    union[row * 3 + col] = sample; if (active) world.Add(sample);
                }
            }
            CaptureCell requirement;
            Require(BlueprintDoorMembers.TryRequirement(source, target, observed, 4, 8, out requirement, catalog, On) && requirement.TilePaint == 2 &&
                BlueprintWorldMatchComparer.CompareObject(source, target, world, true, catalog, On) == BlueprintWorldMatchState.Match &&
                BlueprintMaterialSteps.SameBody(source, target, world, true, catalog, On) == BlueprintBodyState.Same,
                "replacement doors retain canonical hinge, open wings, body proof and per-row appearance");
            BlueprintWorldDoorWitness witness;
            Require(BlueprintWorldDoorWitness.TryCreate(source, target, observed, union, out witness, catalog, On) &&
                !BlueprintWorldDoorWitness.TryCreate(source, target, observed, union, out witness),
                "witness explicitly consumes current relation and remains bound to actual door style");
            var obj = a.Prototype; obj.Id = 1; obj.X = 4; obj.Y = 7; obj.Width = 2; obj.Type = 11;
            var open = target[0]; open.X = 4; open.TileType = 11; open.FrameX = 36;
            CaptureCell execution; CaptureObject? next;
            Require(catalog.Substitution.TryCreateExecution(open, obj, b.Placement, On, out execution, out next) &&
                execution.X == 5 && execution.FrameY == 54 && next.Value.X == 5 && next.Value.Width == 1,
                "open source execution is a concrete closed door at the same hinge");
            var sourceLeft = new BlueprintWorldObject(4, 7, 2, 3, 5, 7, 10, 0, 0, BlueprintWorldObjectFamily.Door, null);
            var sourceRight = new BlueprintWorldObject(5, 7, 2, 3, 5, 7, 10, 0, 0, BlueprintWorldObjectFamily.Door, null);
            var closed = new BlueprintWorldObject(5, 7, 1, 3, 5, 7, 10, 1, 0, BlueprintWorldObjectFamily.Door, null);
            var right = new BlueprintWorldObject(5, 7, 2, 3, 5, 7, 10, 1, 0, BlueprintWorldObjectFamily.Door, null);
            foreach (var sourcePose in new[] { source, sourceLeft, sourceRight })
                foreach (var actualPose in new[] { closed, observed, right })
                {
                    var sourceSamples = DoorSamples(sourcePose); var members = new CaptureCell[sourceSamples.Length];
                    for (int i = 0; i < members.Length; i++) { members[i] = sourceSamples[i].Cell; members[i].ObjectId = 1; }
                    var actualSamples = DoorSamples(actualPose);
                    Require(BlueprintWorldMatchComparer.CompareObject(sourcePose, members, actualSamples, true, catalog, On) == BlueprintWorldMatchState.Match &&
                        BlueprintMaterialSteps.SameBody(sourcePose, members, actualSamples, true, catalog, On) == BlueprintBodyState.Same,
                        "all nine canonical closed/left-open/right-open source and replacement poses retain the same hinge contract");
                }
            Require(!catalog.Substitution.AllowsObject(source, new BlueprintWorldObject(6, 7, 1, 3, 6, 7, 10, 1, 0, BlueprintWorldObjectFamily.Door, null), On),
                "a different hinge never borrows door substitution identity");
        }
        private static BlueprintWorldMatchSample[] DoorSamples(BlueprintWorldObject body)
        {
            var result = new BlueprintWorldMatchSample[body.MemberCount];
            int yBase = body.Style % 36 * 54;
            for (int row = 0; row < 3; row++) for (int col = 0; col < body.Width; col++)
            {
                int xBase = body.Width == 1 ? body.Style / 36 * 54 : body.Style / 36 * 72 + (body.AnchorX > body.WorldX ? 36 : 0);
                var cell = new CaptureCell { X = body.WorldX + col, Y = body.WorldY + row, Layers = CaptureLayer.Tile,
                    TileType = (ushort)(body.Width == 1 ? 10 : 11), FrameX = (short)(xBase + col * 18), FrameY = (short)(yBase + row * 18), TilePaint = 2 };
                result[row * body.Width + col] = new BlueprintWorldMatchSample(true, cell, body);
            }
            return result;
        }
        private static BlueprintMaterialInventory Inventory(params long[] pairs)
        {
            var slots = new BlueprintMaterialInventorySlot[pairs.Length / 2];
            for (int i = 0; i < slots.Length; i++) slots[i] = new BlueprintMaterialInventorySlot(i, (int)pairs[i * 2], pairs[i * 2 + 1], true);
            return new BlueprintMaterialInventory(1, 2, 3, true, slots);
        }
        private static void SharedStock()
        {
            var a = Member(BlueprintSubstitutionCategory.Workbench, 101, 18, 0);
            var b = Member(BlueprintSubstitutionCategory.Workbench, 102, 18, 1);
            var c = Member(BlueprintSubstitutionCategory.Workbench, 103, 18, 2);
            var catalog = Catalog(a, b, c); var needs = new Dictionary<int, long> { { 101, 10 }, { 102, 10 } };
            var allocation = new BlueprintMaterialAllocation(needs, Inventory(103, 12), catalog, On);
            Require(allocation.Row(101).Missing + allocation.Row(102).Missing == 8 && allocation.Row(101).Held == 0 && allocation.Row(102).Held == 0 &&
                allocation.Row(101).SubstitutionAvailable + allocation.Row(102).SubstitutionAvailable == 12,
                "two original rows share one stack exactly once and preserve raw Held meaning");
            allocation = new BlueprintMaterialAllocation(needs, Inventory(101, 10, 103, 10), catalog, On);
            Require(allocation.Row(101).AllocatedExact == 10 && allocation.Row(102).SubstitutionAvailable == 10 && allocation.Row(102).Missing == 0,
                "available originals satisfy their original rows before alternatives");
            allocation = new BlueprintMaterialAllocation(new Dictionary<int, long> { { 101, 100 } }, Inventory(101, 10, 102, 35, 103, 55), catalog, On);
            Require(allocation.Row(101).Missing == 0 && allocation.Row(101).Held == 10 && allocation.Row(101).SubstitutionAvailable == 90,
                "one demand can mix original and two individually insufficient replacement stacks");
            var ax = Member(BlueprintSubstitutionCategory.Workbench, 201, 18, 0, physical: 1);
            var ay = Member(BlueprintSubstitutionCategory.Workbench, 201, 18, 1, physical: 2, alternate: 1);
            var bx = Member(BlueprintSubstitutionCategory.Workbench, 202, 18, 2, physical: 1);
            var cy = Member(BlueprintSubstitutionCategory.Workbench, 203, 18, 3, physical: 2);
            allocation = new BlueprintMaterialAllocation(new Dictionary<int, long> { { 201, 10 }, { 202, 10 } },
                Inventory(201, 10, 203, 10), Catalog(ax, ay, bx, cy), On);
            Require(allocation.Row(201).Missing == 0 && allocation.Row(202).Missing == 0 && allocation.Row(201).Held == 10,
                "overlapping pools reassign a flexible row instead of producing a greedy false shortage");
            allocation = new BlueprintMaterialAllocation(needs, Inventory(103, 12), catalog, default(BlueprintSubstitutionPolicy));
            Require(allocation.Row(101).Missing + allocation.Row(102).Missing == 20, "disabled policy never spends replacement supply");
        }
        private static void EffectiveOutput()
        {
            var a = Member(BlueprintSubstitutionCategory.Torch, 1101, 4, 0);
            var b = Member(BlueprintSubstitutionCategory.Torch, 1102, 4, 1);
            var catalog = Catalog(a, b); BlueprintPlacementDescriptor effective;
            Require(catalog.Substitution.TryEffectiveTile(a.Placement, 4, 1, On, out effective) && effective.UseItemType == 1101 &&
                effective.ConsumeItemType == 1101 && effective.Style == 1, "effective native output retains its precise held-item consumption owner");
            var original = new CaptureCell { Layers = CaptureLayer.Tile, TileType = 4, ObjectId = 1 };
            CaptureCell execution; CaptureObject? obj;
            Require(catalog.Substitution.TryCreateExecution(original, a.Prototype, effective, On, out execution, out obj) && execution.FrameX == 18 && obj.Value.Style == 1,
                "effective output uses the admitted output grid, rather than held item's nominal style");
            Require(!catalog.Substitution.TryEffectiveTile(a.Placement, 4, 1, default(BlueprintSubstitutionPolicy), out effective),
                "exact-only mode does not silently permit native style conversion");
        }
    }
}
