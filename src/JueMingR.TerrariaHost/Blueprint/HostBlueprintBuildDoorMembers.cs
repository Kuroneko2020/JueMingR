using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    // At most six immutable, already selected source members. Retaining this
    // tiny source proof and coordinate-wall knowledge (including proven empty)
    // prevents transport eviction during a native use from turning a real debit
    // into an unknown result. Target identity/mask and actual form still revoke.
    internal sealed class BlueprintDoorBuildProof
    {
        internal BlueprintDoorBuildProof(BlueprintWorldObject source, CaptureCell[] members, CaptureCell? wall = null, int wallX = 0, int wallY = 0, bool wallKnown = false)
        { Source = source; Members = members; Wall = wall; WallX = wallX; WallY = wallY; WallKnown = wallKnown; }
        internal readonly BlueprintWorldObject Source;
        internal readonly CaptureCell[] Members;
        internal readonly CaptureCell? Wall;
        internal readonly int WallX, WallY;
        internal readonly bool WallKnown;
    }
    internal sealed partial class HostBlueprint
    {
        private BlueprintProjectionWindow DoorSourceWindow(BlueprintProjectionMatchRange range)
        {
            var retained = FindBuildSource(range); if (retained != null) return retained.Window;
            BlueprintProjectionMatchWork work;
            if (projectionMatchWork.TryGetValue(range.Owner.Identity, out work) && ReferenceEquals(work.Owner, range.Owner))
                foreach (var part in work.Parts) if (part?.Range == range.Index) return part.Window;
            return null;
        }
        // The source ordinal remains a saved member/lane. A closed source can
        // service both actual columns through that same ordinal after each
        // native write; neither the source Cell nor its other layers move.
        private bool TryBindConstructionTarget(BlueprintBuildTarget target, BlueprintBuildLane lane, out BlueprintWorldMatchSample actual)
        {
            actual = default(BlueprintWorldMatchSample);
            if (lane != BlueprintBuildLane.TileBody)
            {
                target.TargetX = target.OriginX + target.Cell.X; target.TargetY = target.OriginY + target.Cell.Y;
                target.Bounds = new BlueprintProjectionBounds(target.TargetX, target.TargetY, 1, 1);
            }
            if (lane != BlueprintBuildLane.Appearance || target.Body?.Family != BlueprintWorldObjectFamily.Door)
                return ReadBuildCell(target.WorldX, target.WorldY, out actual);
            CaptureCell[] members; BlueprintWorldObject observed;
            if (!TryDoorSourceMembers(target.Range.Owner, DoorSourceWindow(target.Range), target.Body, out members) ||
                !TryReadCompleteDoor(target.Body, out observed)) return false;
            int row = target.OriginY + target.Cell.Y - target.Body.WorldY;
            if (row < 0 || row >= observed.Height) return false;
            for (int column = 0; column < observed.Width; column++)
            {
                int x = observed.WorldX + column, y = observed.WorldY + row;
                CaptureCell requirement;
                if (!BlueprintDoorMembers.TryRequirement(target.Body, members, observed, x, y, out requirement)) return false;
                if (target.Body.Width == 2 && observed.Width == 2 && requirement.X != target.Cell.X) continue;
                // Equal source columns collapse to one actual step. Only the
                // first source column owns it, avoiding a duplicate candidate.
                if (observed.Width == 1 && target.Body.Width == 2 && target.Cell.X != members[row * 2].X) return false;
                target.TargetX = x; target.TargetY = y; target.Bounds = new BlueprintProjectionBounds(x, y, 1, 1);
                BlueprintBuildOperation operation;
                if (ReadBuildCell(x, y, out actual) && TryPlanAppearanceBuild(target, actual, lane, out operation)) return true;
            }
            return false;
        }

        private bool TryDoorSourceMembers(BlueprintProjectionPlacement owner, BlueprintProjectionWindow window,
            BlueprintWorldObject body, out CaptureCell[] members)
        {
            members = null;
            if (owner == null || window == null || body?.Family != BlueprintWorldObjectFamily.Door ||
                !window.Identity.Equals(owner.Identity) || window.Objects.Count != 1 ||
                window.Cells.Count != body.MemberCount && !CompleteDoorContext(window) ||
                !ProjectionQualificationCurrent(owner)) return false;
            var obj = window.Objects[0];
            if (!obj.Reliable || obj.Id <= 0) return false;
            var values = new CaptureCell[body.MemberCount];
            for (int index = 0; index < values.Length; index++)
            {
                CaptureCell raw;
                if (!TryWindowSourceCell(window, body.WorldX - owner.OriginX + index % body.Width,
                    body.WorldY - owner.OriginY + index / body.Width, out raw)) return false;
                var selected = owner.Mask.Apply(raw);
                var selection = SelectProjectionTarget(owner, selected, obj);
                if (raw.ObjectId != obj.Id || raw.UnreliableObject || (raw.Layers & CaptureLayer.Tile) == 0 ||
                    raw.X + owner.OriginX != body.WorldX + index % body.Width || raw.Y + owner.OriginY != body.WorldY + index / body.Width ||
                    !body.MatchesMemberFrame(raw, index) || (selection.RequiredBits & 1) == 0 || (selection.UnknownBits & 1) != 0 ||
                    (selected.Layers & CaptureLayer.Tile) == 0) return false;
                values[index] = selected;
            }
            members = values; return true;
        }

        private bool TryReadCompleteDoor(BlueprintWorldObject source, out BlueprintWorldObject observed)
        {
            observed = null; BlueprintWorldMatchSample hinge;
            if (source?.Family != BlueprintWorldObjectFamily.Door || !ReadBuildCell(source.AnchorX, source.AnchorY, out hinge) ||
                hinge.ObjectUnknown || hinge.Object == null || !source.SamePlacement(hinge.Object)) return false;
            var form = hinge.Object;
            if (form.Height != 3 || form.Width < 1 || form.Width > 2) return false;
            var proof = new BlueprintObjectBodyProof(source);
            for (int row = 0; row < form.Height; row++)
            for (int column = 0; column < form.Width; column++)
            {
                BlueprintWorldMatchSample sample;
                if (!ReadBuildCell(form.WorldX + column, form.WorldY + row, out sample) || sample.Object == null ||
                    !form.SameObservedObject(sample.Object)) return false;
                proof.Observe(sample);
            }
            if (proof.Complete() != BlueprintBodyState.Same) return false;
            observed = form; return true;
        }

        private bool TryDoorRequirementFromWindow(BlueprintProjectionPlacement owner, BlueprintProjectionWindow window,
            BlueprintWorldMatchSample actual, out CaptureCell requirement, out BlueprintWorldObject source)
        {
            requirement = default(CaptureCell); source = null;
            if (window == null || window.Objects.Count != 1 || window.Cells.Count == 0 || actual.Object?.Family != BlueprintWorldObjectFamily.Door) return false;
            var obj = window.Objects[0]; CaptureCell first;
            if (!TryWindowSourceCell(window, obj.X, obj.Y, out first) ||
                !projectionMatchSource.TryDescribeTarget(obj, first, owner.OriginX, owner.OriginY, out source) ||
                !source.SamePlacement(actual.Object)) return false;
            CaptureCell[] members;
            return TryDoorSourceMembers(owner, window, source, out members) &&
                BlueprintDoorMembers.TryRequirement(source, members, actual.Object, actual.Cell.X, actual.Cell.Y, out requirement);
        }

        private bool TrySelectedDoorRequirement(BlueprintProjectionPlacement owner, BlueprintWorldMatchSample actual,
            out CaptureCell requirement, out BlueprintWorldObject source, out BlueprintProjectionWindow window)
        {
            requirement = default(CaptureCell); source = null; window = null;
            if (actual.Object?.Family != BlueprintWorldObjectFamily.Door) return false;
            // A candidate's own retained source part carries this context.
            // There is no dependency on another live work part or UI Draw.
            foreach (var retained in buildSources)
                if (ReferenceEquals(retained.Owner, owner) && ReferenceEquals(retained.Mask, owner.Mask) &&
                    retained.X == owner.OriginX && retained.Y == owner.OriginY && retained.Catalog == MaterialCatalog.Revision &&
                    TrySelectedDoorRequirement(owner, retained.Part, actual, out requirement, out source, out window)) return true;
            BlueprintProjectionMatchWork work;
            if (projectionMatchWork.TryGetValue(owner.Identity, out work) && ReferenceEquals(work.Owner, owner))
                foreach (var part in work.Parts)
                    if (part != null && TrySelectedDoorRequirement(owner, part, actual, out requirement, out source, out window)) return true;
            return false;
        }
        private bool TrySelectedDoorRequirement(BlueprintProjectionPlacement owner, BlueprintProjectionSourcePart part,
            BlueprintWorldMatchSample actual, out CaptureCell requirement, out BlueprintWorldObject source, out BlueprintProjectionWindow window)
        {
            requirement = default(CaptureCell); source = null; window = null;
            if (part == null || actual.Object?.Family != BlueprintWorldObjectFamily.Door) return false;
            window = FindDoorContext(part, actual.Object.AnchorX - owner.OriginX - 1, actual.Object.WorldY - owner.OriginY);
            if (window != null) return TryDoorRequirementFromWindow(owner, window, actual, out requirement, out source);
            window = part.Window;
            return TryDoorRequirementFromWindow(owner, window, actual, out requirement, out source);
        }

        private static bool CompleteDoorContext(BlueprintProjectionWindow window)
        {
            if (window == null || window.Bounds.Width != 3 || window.Bounds.Height != 3 || window.Cells.Count != 9) return false;
            for (int i = 0; i < 9; i++)
                if (window.Cells[i].X != window.Bounds.Left + i % 3 || window.Cells[i].Y != window.Bounds.Top + i / 3) return false;
            return true;
        }
        private static BlueprintProjectionWindow FindDoorContext(BlueprintProjectionSourcePart part, int left, int top)
        {
            var contexts = part.DoorContexts; int low = 0, high = contexts.Count;
            while (low < high)
            {
                int at = low + (high - low) / 2; var bounds = contexts[at].Bounds;
                if (bounds.Top < top || bounds.Top == top && bounds.Left < left) low = at + 1; else high = at;
            }
            return low < contexts.Count && contexts[low].Bounds.Left == left && contexts[low].Bounds.Top == top &&
                CompleteDoorContext(contexts[low]) ? contexts[low] : null;
        }

        private static bool TryWindowSourceCell(BlueprintProjectionWindow window, int x, int y, out CaptureCell cell)
        {
            cell = default(CaptureCell);
            if (window == null) return false;
            int index = BuildCellLowerBound(window.Cells, x, y);
            if (index >= window.Cells.Count || window.Cells[index].X != x || window.Cells[index].Y != y) return false;
            cell = window.Cells[index]; return true;
        }

        private bool TryCoordinateWall(BlueprintProjectionPlacement owner, int worldX, int worldY, out CaptureCell wall, out bool known)
        {
            wall = default(CaptureCell); known = false;
            foreach (var retained in buildSources)
            {
                if (!ReferenceEquals(retained.Owner, owner) || !ReferenceEquals(retained.Mask, owner.Mask) ||
                    retained.X != owner.OriginX || retained.Y != owner.OriginY || retained.Catalog != MaterialCatalog.Revision) continue;
                if (TryCoordinateWall(owner, retained.Part, worldX, worldY, out wall, out known)) return true;
                if (known) return false;
            }
            BlueprintProjectionMatchWork work;
            if (projectionMatchWork.TryGetValue(owner.Identity, out work) && ReferenceEquals(work.Owner, owner))
                foreach (var part in work.Parts)
                {
                    if (part == null) continue;
                    if (TryCoordinateWall(owner, part, worldX, worldY, out wall, out known)) return true;
                    if (known) return false;
                }
            return false;
        }
        private bool TryCoordinateWall(BlueprintProjectionPlacement owner, BlueprintProjectionSourcePart part, int worldX, int worldY,
            out CaptureCell wall, out bool known)
        {
            wall = default(CaptureCell); known = false;
            if (part?.Window == null || !part.Window.Identity.Equals(owner.Identity)) return false;
            int x = worldX - owner.OriginX, y = worldY - owner.OriginY; CaptureCell raw;
            // At most nine exact area-key lookups, each a binary search of the
            // current part. An explicit empty context cell proves absence; a
            // missing context must block rather than grant Exact or an action.
            for (int top = y - 2; top <= y; top++)
            for (int left = x - 2; left <= x; left++)
            {
                var context = FindDoorContext(part, left, top);
                if (context != null && TryWindowSourceCell(context, x, y, out raw))
                { known = true; return TrySelectedCoordinateWall(owner, raw, out wall); }
            }
            if (!TryWindowSourceCell(part.Window, x, y, out raw)) return false;
            known = true; return TrySelectedCoordinateWall(owner, raw, out wall);
        }
        private bool TrySelectedCoordinateWall(BlueprintProjectionPlacement owner, CaptureCell raw, out CaptureCell wall)
        {
            wall = owner.Mask.Apply(raw);
            var selection = SelectProjectionTarget(owner, wall);
            return (wall.Layers & CaptureLayer.Wall) != 0 && (selection.RequiredBits & 2) != 0 && (selection.UnknownBits & 2) == 0;
        }
        private static CaptureCell WithCoordinateWall(CaptureCell tile, CaptureCell wall)
        {
            tile.Layers |= CaptureLayer.Wall; tile.WallType = wall.WallType; tile.WallPaint = wall.WallPaint;
            tile.InvisibleWall = wall.InvisibleWall; tile.FullbrightWall = wall.FullbrightWall; return tile;
        }

        private bool TryAppearanceTarget(BlueprintBuildTarget target, BlueprintWorldMatchSample actual,
            out CaptureCell expected, out bool tileSame, out BlueprintWorldObject doorForm)
        {
            BlueprintDoorBuildProof proof;
            return TryAppearanceTarget(target, actual, out expected, out tileSame, out doorForm, out proof);
        }
        private bool TryAppearanceTarget(BlueprintBuildTarget target, BlueprintWorldMatchSample actual,
            out CaptureCell expected, out bool tileSame, out BlueprintWorldObject doorForm, out BlueprintDoorBuildProof proof)
        {
            expected = target.Cell; doorForm = null; proof = target.Operation?.DoorProof;
            tileSame = (expected.Layers & CaptureLayer.Tile) != 0 && BuildOperationBodySame(target, actual);
            if (target.Body?.Family == BlueprintWorldObjectFamily.Door)
            {
                CaptureCell[] members; BlueprintWorldObject observed;
                if (proof != null) members = proof.Members;
                else if (!TryDoorSourceMembers(target.Range.Owner, DoorSourceWindow(target.Range), target.Body, out members)) return false;
                if (proof != null && !target.Body.SameObservedObject(proof.Source) ||
                    !TryReadCompleteDoor(target.Body, out observed) || actual.Object == null || !observed.SameObservedObject(actual.Object) ||
                    !BlueprintDoorMembers.TryRequirement(target.Body, members, observed, actual.Cell.X, actual.Cell.Y, out expected)) return false;
                if (proof == null) proof = new BlueprintDoorBuildProof(target.Body, members);
                doorForm = observed; tileSame = true;
                // Only Tile requirements follow the operating pose. The wall
                // context is recovered at the actual coordinate, independently.
                expected.Layers = CaptureLayer.Tile; expected.Actuator = false; expected.Wires = 0;
                CaptureCell wall;
                if (proof.WallKnown && proof.WallX == actual.Cell.X && proof.WallY == actual.Cell.Y)
                {
                    if (proof.Wall.HasValue) expected = WithCoordinateWall(expected, proof.Wall.Value);
                }
                else
                {
                    bool known; bool selected = TryCoordinateWall(target.Range.Owner, actual.Cell.X, actual.Cell.Y, out wall, out known);
                    if (!known) return false;
                    if (selected) expected = WithCoordinateWall(expected, wall);
                    proof = new BlueprintDoorBuildProof(proof.Source, proof.Members, selected ? wall : (CaptureCell?)null, actual.Cell.X, actual.Cell.Y, true);
                }
            }
            else if ((expected.Layers & CaptureLayer.Wall) != 0 && actual.Object?.Family == BlueprintWorldObjectFamily.Door)
            {
                CaptureCell mapped; BlueprintWorldObject source, observed; BlueprintProjectionWindow sourceWindow;
                // A wall source can lie under a runtime wing outside the saved
                // door bbox. Only the same complete selected door grants Tile
                // restoration; a nearby/unselected foreground grants nothing.
                if (proof != null)
                {
                    source = proof.Source;
                    if (!BlueprintDoorMembers.TryRequirement(source, proof.Members, actual.Object, actual.Cell.X, actual.Cell.Y, out mapped) ||
                        !TryReadCompleteDoor(source, out observed) || !observed.SameObservedObject(actual.Object)) return false;
                    expected = WithCoordinateWall(mapped, expected); tileSame = true; doorForm = observed;
                }
                else if (TrySelectedDoorRequirement(target.Range.Owner, actual, out mapped, out source, out sourceWindow) &&
                    TryReadCompleteDoor(source, out observed) && observed.SameObservedObject(actual.Object))
                {
                    CaptureCell[] members;
                    if (!TryDoorSourceMembers(target.Range.Owner, sourceWindow, source, out members)) return false;
                    proof = new BlueprintDoorBuildProof(source, members, expected, actual.Cell.X, actual.Cell.Y, true);
                    expected = WithCoordinateWall(mapped, expected); tileSame = true; doorForm = observed;
                }
            }
            if (target.Operation?.DoorForm != null && (doorForm == null || !target.Operation.DoorForm.SameObservedObject(doorForm))) return false;
            return true;
        }

        private bool AppearanceDoorFormCurrent(BlueprintBuildTarget target)
        {
            if (target.Operation?.DoorForm == null) return true;
            BlueprintWorldObject observed;
            return TryReadCompleteDoor(target.Operation.DoorForm, out observed) && target.Operation.DoorForm.SameObservedObject(observed);
        }
    }
}
