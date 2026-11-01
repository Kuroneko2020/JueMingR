using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    public static class BlueprintWorldMatchComparer
    {
        public static BlueprintWorldMatchResult CompareCell(CaptureCell target, BlueprintWorldMatchSample world,
            BlueprintConstructionSelection selection, BlueprintWorldMatchState objectTileState = BlueprintWorldMatchState.Unknown,
            BlueprintPlacementCatalog catalog = null, BlueprintSubstitutionPolicy policy = default(BlueprintSubstitutionPolicy))
        {
            var result = CompareCell(selection.Apply(target), world, objectTileState, catalog, policy);
            ushort known = (ushort)~selection.UnknownBits;
            return new BlueprintWorldMatchResult(result.TargetBits, (ushort)(result.MatchBits & known),
                (ushort)(result.ConflictBits & known), (ushort)(result.UnknownBits | selection.UnknownBits));
        }
        public static BlueprintWorldMatchResult CompareCell(CaptureCell target, BlueprintWorldMatchSample world,
            BlueprintWorldMatchState objectTileState = BlueprintWorldMatchState.Unknown,
            BlueprintPlacementCatalog catalog = null, BlueprintSubstitutionPolicy policy = default(BlueprintSubstitutionPolicy))
        {
            ushort bits = BlueprintConstructionTargets.Layers(target), matches = 0, conflicts = 0, unknown = 0;
            if (!world.Available) return new BlueprintWorldMatchResult(bits, 0, 0, bits);
            CaptureCell actual = world.Cell;
            if ((bits & 1) != 0)
            {
                BlueprintWorldMatchState state;
                short targetFrameX, targetFrameY;
                NormalizeFrame(target, world.FrameKind, out targetFrameX, out targetFrameY);
                if (target.UnreliableObject || target.Slope > 4 || target.HalfBrick && target.Slope != 0) state = BlueprintWorldMatchState.Unknown;
                else if (target.ObjectId > 0) state = objectTileState;
                else if ((actual.Layers & CaptureLayer.Tile) == 0) state = BlueprintWorldMatchState.Missing;
                else if (world.ObjectUnknown || !FrameProfileCurrent(world) || actual.Slope > 4 || actual.HalfBrick && actual.Slope != 0)
                    state = BlueprintWorldMatchState.Unknown;
                else if (!SameAppearance(target, actual) ||
                    !(target.TileType == actual.TileType && (!world.CompareFrames || targetFrameX == world.SemanticFrameX && targetFrameY == world.SemanticFrameY) ||
                        catalog?.Substitution.AllowsTile(target, actual, policy) == true &&
                        SingleCellFrameProfile(catalog.TileShape(target), world))) state = BlueprintWorldMatchState.Conflict;
                else state = BlueprintWorldMatchState.Match;
                Put(1, state, ref matches, ref conflicts, ref unknown);
            }
            if ((bits & 2) != 0)
                Put(2, (actual.Layers & CaptureLayer.Wall) == 0 ? BlueprintWorldMatchState.Missing :
                    (target.WallType == actual.WallType || catalog?.Substitution.AllowsWall(target.WallType, actual.WallType, policy) == true) &&
                    target.WallPaint == actual.WallPaint && target.InvisibleWall == actual.InvisibleWall && target.FullbrightWall == actual.FullbrightWall ? BlueprintWorldMatchState.Match : BlueprintWorldMatchState.Conflict,
                    ref matches, ref conflicts, ref unknown);
            if ((bits & 8) != 0 && actual.Actuator) matches |= 8;
            // Wires and actuators are presence requirements. Additional colors
            // and layers are transparent to a target that never asked for them.
            matches |= (ushort)(bits & ((actual.Wires & 15) << 4));
            return new BlueprintWorldMatchResult(bits, matches, conflicts, unknown);
        }

        // The Host owns completeness/currentness and supplies one finite group.
        // Every member must prove the same observed object; type-compatible
        // fragments, duplicate positions and partial groups cannot earn Match.
        public static BlueprintWorldMatchState CompareObject(BlueprintWorldObject target, IReadOnlyList<CaptureCell> targetMembers,
            IReadOnlyList<BlueprintWorldMatchSample> worldMembers, bool currentAndComplete,
            BlueprintPlacementCatalog catalog = null, BlueprintSubstitutionPolicy policy = default(BlueprintSubstitutionPolicy))
        {
            if (target == null || targetMembers == null || worldMembers == null || !currentAndComplete || targetMembers.Count != target.MemberCount) return BlueprintWorldMatchState.Unknown;
            if (!ValidTargetMembers(target, targetMembers)) return BlueprintWorldMatchState.Unknown;
            bool missing = false, unknown = false, conflict = false;
            BlueprintWorldObject observed = null;
            foreach (BlueprintWorldMatchSample sample in worldMembers)
            {
                if (!sample.Available) { unknown = true; continue; }
                if ((sample.Cell.Layers & CaptureLayer.Tile) == 0) { missing = true; continue; }
                if (sample.ObjectUnknown) { unknown = true; continue; }
                if (sample.Object == null || !(target.SamePlacement(sample.Object) || catalog?.Substitution.AllowsObject(target, sample.Object, policy) == true) ||
                    (sample.Object.Family == BlueprintWorldObjectFamily.Door ? sample.Cell.TileType != 10 && sample.Cell.TileType != 11 : sample.Cell.TileType != sample.Object.Kind)) { conflict = true; continue; }
                if (observed == null) observed = sample.Object;
                else if (!observed.SameObservedObject(sample.Object)) conflict = true;
                if (sample.Cell.Slope > 4 || sample.Cell.HalfBrick && sample.Cell.Slope != 0) unknown = true;
                else if (!MatchesMemberAppearance(target, targetMembers, sample.Object, sample.Cell, catalog, policy)) conflict = true;
            }
            if (conflict) return BlueprintWorldMatchState.Conflict;
            if (unknown) return BlueprintWorldMatchState.Unknown;
            if (observed == null) return worldMembers.Count == target.MemberCount && CompleteRectangle(target, worldMembers) ? BlueprintWorldMatchState.Missing : BlueprintWorldMatchState.Unknown;
            if (missing) return CompleteRectangle(target, worldMembers) || CompleteRectangle(observed, worldMembers) ? BlueprintWorldMatchState.Missing : BlueprintWorldMatchState.Unknown;
            if (worldMembers.Count != observed.MemberCount || !CompleteRectangle(observed, worldMembers)) return BlueprintWorldMatchState.Unknown;
            return BlueprintWorldMatchState.Match;
        }

        private static bool MatchesMemberAppearance(BlueprintWorldObject target, IReadOnlyList<CaptureCell> expected, BlueprintWorldObject observed, CaptureCell actual,
            BlueprintPlacementCatalog catalog, BlueprintSubstitutionPolicy policy)
        {
            int row = actual.Y - observed.WorldY, column = actual.X - observed.WorldX;
            if (row < 0 || row >= target.Height || column < 0 || column >= observed.Width) return false;
            if (target.Family != BlueprintWorldObjectFamily.Door) return column < target.Width && SameAppearance(expected[row * target.Width + column], actual);
            CaptureCell requirement;
            return BlueprintDoorMembers.TryRequirement(target, expected, observed, actual.X, actual.Y, out requirement, catalog, policy) &&
                SameAppearance(requirement, actual);
        }

        // The Host grants these value profiles from native metadata/rules.
        // Tree structure and palm lean remain semantic. A proved one-cell
        // platform has 27 neighbor/shape slots in X and material style in Y;
        // malformed raw target frames do not inherit that X equivalence.
        public static void NormalizeFrame(CaptureCell cell, BlueprintWorldFrameKind kind, out short x, out short y)
        {
            x = cell.FrameX; y = cell.FrameY;
            if (kind == BlueprintWorldFrameKind.TreeTrunk22 && x >= 0 && x <= 110 && x % 22 == 0 && y >= 0 && y < 264 && y % 22 == 0) y = (short)(y / 66 * 66);
            else if (kind == BlueprintWorldFrameKind.Palm22 && x % 22 == 0)
            { if (x >= 0 && x <= 44) x = 0; else if (x >= 88 && x <= 132) x = 88; }
            else if (kind == BlueprintWorldFrameKind.Mushroom18 && (x == 0 || x == 36) && y >= 0 && y <= 36 && y % 18 == 0) y = 0;
            else if (kind == BlueprintWorldFrameKind.Platform18 && x >= 0 && x < 27 * 18 && x % 18 == 0 && y >= 0 && y % 18 == 0) x = 0;
            else if (kind == BlueprintWorldFrameKind.PlanterBox18 && x >= 0 && x <= 54 && x % 18 == 0 && y >= 0 && y % 18 == 0) x = 0;
        }
        // The platform tag cannot hide malformed raw observations or a style
        // from another sample. Host creates both fields from the same gated
        // read; pure consumers retain that relationship before granting Same.
        internal static bool FrameProfileCurrent(BlueprintWorldMatchSample sample)
        {
            CaptureCell cell = sample.Cell;
            if (sample.FrameKind == BlueprintWorldFrameKind.Platform18) return sample.CompareFrames &&
                cell.FrameX >= 0 && cell.FrameX < 27 * 18 && cell.FrameX % 18 == 0 && cell.FrameY >= 0 && cell.FrameY % 18 == 0 &&
                sample.SemanticFrameX == 0 && sample.SemanticFrameY == cell.FrameY;
            // Planter boxes have four legacy horizontal connection frames,
            // not the platform's 27 shape slots or a metadata object grid.
            if (sample.FrameKind == BlueprintWorldFrameKind.PlanterBox18) return sample.CompareFrames &&
                cell.FrameX >= 0 && cell.FrameX <= 54 && cell.FrameX % 18 == 0 && cell.FrameY >= 0 && cell.FrameY % 18 == 0 &&
                !cell.HalfBrick && cell.Slope == 0 && sample.SemanticFrameX == 0 && sample.SemanticFrameY == cell.FrameY;
            return true;
        }
        internal static bool SingleCellFrameProfile(BlueprintPlacementDescriptor descriptor, BlueprintWorldMatchSample sample)
        { return descriptor == null || !descriptor.Platform && !descriptor.PlanterBox || sample.CompareFrames &&
            (descriptor.PlanterBox ? sample.FrameKind == BlueprintWorldFrameKind.PlanterBox18 : sample.FrameKind == BlueprintWorldFrameKind.Platform18); }

        private static bool ValidTargetMembers(BlueprintWorldObject target, IReadOnlyList<CaptureCell> members)
        {
            // Source batches are complete row-major rectangles. Local template
            // positions remain local here; world origin was applied once when
            // describing the canonical target, never Summary.Left/Top twice.
            CaptureCell first = members[0];
            for (int i = 0; i < members.Count; i++)
            {
                CaptureCell cell = members[i];
                if ((cell.Layers & CaptureLayer.Tile) == 0 || cell.ObjectId <= 0 || cell.ObjectId != first.ObjectId || cell.TileType != first.TileType || cell.UnreliableObject || cell.Slope > 4 || cell.HalfBrick && cell.Slope != 0 ||
                    cell.X != first.X + i % target.Width || cell.Y != first.Y + i / target.Width || !target.MatchesMemberFrame(cell, i)) return false;
            }
            return true;
        }
        private static bool CompleteRectangle(BlueprintWorldObject obj, IReadOnlyList<BlueprintWorldMatchSample> samples)
        {
            if (samples.Count != obj.MemberCount) return false;
            for (int i = 0; i < samples.Count; i++) if (samples[i].Cell.X != obj.WorldX + i % obj.Width || samples[i].Cell.Y != obj.WorldY + i / obj.Width) return false;
            return true;
        }
        private static bool SameAppearance(CaptureCell expected, CaptureCell actual)
        { return expected.HalfBrick == actual.HalfBrick && expected.Slope == actual.Slope && expected.Inactive == actual.Inactive && expected.TilePaint == actual.TilePaint && expected.InvisibleTile == actual.InvisibleTile && expected.FullbrightTile == actual.FullbrightTile; }
        private static void Put(ushort bit, BlueprintWorldMatchState state, ref ushort matches, ref ushort conflicts, ref ushort unknown)
        { if (state == BlueprintWorldMatchState.Match) matches |= bit; else if (state == BlueprintWorldMatchState.Conflict) conflicts |= bit; else if (state == BlueprintWorldMatchState.Unknown) unknown |= bit; }
    }
}
