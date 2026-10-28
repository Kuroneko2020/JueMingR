using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintWorldMatchChecks
    {
        internal static void Check(IList<string> failures)
        {
            try { Layers(); Objects(); FrameWitness(); DoorAppearance(); DoorWitness(); }
            catch (Exception error) { failures.Add("Blueprint world matching pure values: " + error); }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static CaptureCell Mixed()
        { return new CaptureCell { Layers = (CaptureLayer)31, TileType = 1, WallType = 7, Wires = 15, Actuator = true, LiquidKind = 2, LiquidAmount = 64, TilePaint = 3, WallPaint = 4, Inactive = true, InvisibleTile = true, FullbrightWall = true }; }
        private static BlueprintWorldMatchResult Compare(CaptureCell target, CaptureCell actual)
        { return BlueprintWorldMatchComparer.CompareCell(target, new BlueprintWorldMatchSample(true, actual)); }
        private static void Layers()
        {
            CaptureCell target = Mixed(), actual = target;
            var result = Compare(target, actual);
            Require(result.TargetBits == 251 && result.MatchBits == 251 && result.TargetCount == 7 && result.MatchCount == 7, "each non-liquid layer and wire color contributes exactly one construction target");
            actual.Layers &= ~CaptureLayer.Tile; actual.WallPaint++; actual.Wires = 5; actual.LiquidAmount = 0;
            result = Compare(target, actual);
            Require(result.MatchBits == (8 | 16 | 64) && result.ConflictBits == 2 && result.MissingBits == (1 | 32 | 128), "same-cell wall conflict does not hide missing Tile or wire colors; liquid contributes no target");
            target = new CaptureCell { Layers = CaptureLayer.Wall, WallType = 7 };
            actual = Mixed(); actual.WallPaint = 0; actual.FullbrightWall = false;
            Require(Compare(target, actual).MatchBits == 2, "unrequested foreground, wires, actuator and liquid are not erase requirements");
            target = Mixed();
            foreach (Action<CaptureCellBox> change in new Action<CaptureCellBox>[] {
                b => b.Value.TileType++, b => b.Value.HalfBrick = true, b => b.Value.Slope = 1, b => b.Value.Inactive = false,
                b => b.Value.TilePaint++, b => b.Value.InvisibleTile = false, b => b.Value.FullbrightTile = true })
            { var box = new CaptureCellBox { Value = target }; change(box); Require((Compare(target, box.Value).ConflictBits & 1) != 0, "saved foreground structure and coating each blocks completion"); }
            actual = target; actual.Slope = 5; result = Compare(target, actual);
            Require(result.UnknownBits == 1 && result.MatchBits == 250, "unrecognized slope bits are unknown, never clamped to a completed block");
            actual = target; actual.FrameX = 104; actual.FrameY = 333;
            Require(Compare(target, actual).MatchBits == 251, "ordinary block adjacency frames do not define another building");
            result = BlueprintWorldMatchComparer.CompareCell(target, new BlueprintWorldMatchSample(false, actual));
            Require(result.UnknownBits == 251 && result.MatchCount == 0 && result.MissingBits == 0, "unreadable non-liquid tile/section cannot claim empty or zero-match movement eligibility");
            var liquid = new CaptureCell { Layers = CaptureLayer.Liquid, LiquidAmount = 64, LiquidKind = 2 };
            var displayOnly = BlueprintWorldMatchComparer.CompareCell(liquid, default(BlueprintWorldMatchSample));
            Require(displayOnly.TargetCount == 0 && displayOnly.UnknownBits == 0 && displayOnly.MatchBits == 0,
                "display-only liquid needs no world sample and never acquires a hidden Match layer");
            Require(BlueprintConstructionTargets.Filter(target).Wires == 15 && (BlueprintConstructionTargets.Filter(target).Layers & CaptureLayer.Wire) != 0,
                "CaptureLayer Liquid=16 filtering preserves Wire=4 and all four wire colors");
            target.ObjectId = 4;
            result = BlueprintWorldMatchComparer.CompareCell(target, new BlueprintWorldMatchSample(true, target), BlueprintWorldMatchState.Unknown);
            Require(result.UnknownBits == 1 && result.MatchBits == 250, "whole object proof affects only its Tile bit");
            result = BlueprintWorldMatchComparer.CompareCell(target, new BlueprintWorldMatchSample(true, target), BlueprintWorldMatchState.Match);
            Require(result.MatchBits == 251, "complete object grants each effective member Tile once");
            var raw = Mixed(); var unchanged = raw;
            Compare(raw, actual);
            Require(raw.Layers == unchanged.Layers && raw.Wires == unchanged.Wires && BlueprintProjectionMask.Empty.Apply(raw).Layers == raw.Layers, "matching never mutates raw targets or permanent mask");
            var natural = new CaptureCell { Layers = CaptureLayer.Tile, TileType = 5, FrameX = 22, FrameY = 198 };
            Require(BlueprintWorldMatchComparer.CompareCell(natural, new BlueprintWorldMatchSample(true, natural, null, true, 22, 198)).MatchBits == 1, "reliable ObjectId=0 natural snapshot remains supported");
            Require(BlueprintWorldMatchComparer.CompareCell(natural, new BlueprintWorldMatchSample(true, natural, null, true, 44, 198)).ConflictBits == 1, "natural content with meaningful structural frame is not blindly equal by type");
        }
        private sealed class CaptureCellBox { internal CaptureCell Value; }
        private static BlueprintWorldObject Object(int x = 100, int style = 2, int direction = 1, int form = 9)
        { return new BlueprintWorldObject(x, 200, 2, 2, x, 200, 79, style, direction, BlueprintWorldObjectFamily.Metadata, new[] { form }); }
        private static void Objects()
        {
            BlueprintWorldObject target = Object();
            var cells = new CaptureCell[4]; var actual = new BlueprintWorldMatchSample[4];
            for (int i = 0; i < 4; i++)
            {
                cells[i] = new CaptureCell { X = i % 2, Y = i / 2, ObjectId = 17, Layers = CaptureLayer.Tile, TileType = 79, FrameX = (short)(i * 18) };
                CaptureCell cell = cells[i]; cell.X += 100; cell.Y += 200; cell.FrameX += 72;
                actual[i] = new BlueprintWorldMatchSample(true, cell, Object());
            }
            Func<BlueprintWorldMatchState> compare = () => BlueprintWorldMatchComparer.CompareObject(target, cells, actual, true);
            Require(compare() == BlueprintWorldMatchState.Match, "complete same canonical object matches independently of raw decoration frames and instance-local IDs");
            var good = actual[3]; var empty = good.Cell; empty.Layers = CaptureLayer.None;
            actual[3] = new BlueprintWorldMatchSample(true, empty); Require(compare() == BlueprintWorldMatchState.Missing, "compatible partial object stays ordinary and grants no partial Tile matches");
            var first = actual[0]; var wrongPaint = first.Cell; wrongPaint.TilePaint = 4;
            actual[0] = new BlueprintWorldMatchSample(true, wrongPaint, Object());
            Require(compare() == BlueprintWorldMatchState.Conflict, "known wrong member appearance remains a conflict even when another member is missing");
            actual[0] = first;
            actual[3] = new BlueprintWorldMatchSample(false, empty); Require(compare() == BlueprintWorldMatchState.Unknown, "one unreadable member keeps entire object unknown");
            actual[3] = new BlueprintWorldMatchSample(true, good.Cell, Object(101)); Require(compare() == BlueprintWorldMatchState.Conflict, "same-type members with another object origin cannot be assembled into proof");
            actual[3] = new BlueprintWorldMatchSample(true, good.Cell, Object(style: 3)); Require(compare() == BlueprintWorldMatchState.Conflict, "material building style matters");
            actual[3] = new BlueprintWorldMatchSample(true, good.Cell, Object(direction: 2)); Require(compare() == BlueprintWorldMatchState.Conflict, "directional alternate matters");
            actual[3] = new BlueprintWorldMatchSample(true, good.Cell, Object(form: 10)); Require(compare() == BlueprintWorldMatchState.Conflict, "selector attachment/shape is retained");
            actual[3] = new BlueprintWorldMatchSample(true, good.Cell, new BlueprintWorldObject(100, 200, 2, 2, 100, 200, 79, 2, 1, BlueprintWorldObjectFamily.Metadata, new[] { 9 }, 1));
            Require(compare() == BlueprintWorldMatchState.Conflict, "individually equivalent operating members from different raw phases cannot form one complete proof");
            actual[3] = actual[2]; Require(compare() == BlueprintWorldMatchState.Unknown, "duplicate members cannot prove complete rectangle");
            actual[3] = good;
            Require(BlueprintWorldMatchComparer.CompareObject(target, cells, actual, false) == BlueprintWorldMatchState.Unknown, "stale epoch and unfinished coverage cannot complete an object");
            for (int i = 0; i < 4; i++) { var cell = actual[i].Cell; cell.Layers = CaptureLayer.None; actual[i] = new BlueprintWorldMatchSample(true, cell); }
            Require(compare() == BlueprintWorldMatchState.Missing, "full observed empty footprint is missing, not a malformed furniture conflict");
        }
        private static BlueprintWorldObject Framed(int baseX, int baseY, int x = 100, int style = 2, int direction = 1,
            bool independent = false, int row = 20, BlueprintWorldObjectFamily family = BlueprintWorldObjectFamily.Metadata)
        {
            var shape = new BlueprintObjectShape { BaseFrameX = baseX, BaseFrameY = baseY, ColumnStep = 18,
                RowOffsets = new[] { 0, row }, IndependentMemberFrames = independent };
            return new BlueprintWorldObject(x, 200, 2, 2, x, 200, 79, style, direction, family, new[] { 9 }, (long)(uint)baseX << 32 | (uint)baseY, shape);
        }
        private static void FrameWitness()
        {
            var first = Framed(108, 0); var second = Framed(162, 0); BlueprintWorldObjectFrameWitness witness;
            Require(first.TryCreateFrameWitness(new[] { first, second }, out witness) && witness.MemberCount == 4 && witness.InitialPhase == first.FrameGroup,
                "same-placement fixed layouts compile an immutable value-only phase profile");
            int member; long phase;
            Require(witness.TryTransition(101, 201, true, 126, 180, first.FrameGroup, out member, out phase) && member == 3 && phase == second.FrameGroup,
                "actual field old/new plus certified member phase prove the member and infer the other axis without native reads");
            Require(witness.TryTransition(101, 201, true, 180, 126, phase, out member, out phase) && phase == first.FrameGroup,
                "later transactions can use the newly committed phase instead of a stale initial phase");
            Require(!witness.TryTransition(101, 201, true, 127, 180, first.FrameGroup, out member, out phase), "missing or reordered old-value chain fails closed");
            Require(!witness.TryTransition(101, 201, true, 126, 234, first.FrameGroup, out member, out phase), "next material page is not another allowed operating phase");
            Require(!witness.TryTransition(101, 201, false, 20, 74, first.FrameGroup, out member, out phase), "an unproved other-axis change cannot borrow an X-phase certificate");
            Require(!witness.TryTransition(102, 201, true, 144, 198, first.FrameGroup, out member, out phase) &&
                !witness.TryTransition(101, 201, true, 126, 180, 99, out member, out phase) &&
                !witness.TryTransition(101, 201, true, 126, -1, first.FrameGroup, out member, out phase), "outside member, unknown phase and invalid frame are rejected");
            foreach (var invalid in new[] { Framed(162, 0, x: 101), Framed(162, 0, style: 3), Framed(162, 0, direction: 2),
                Framed(162, 0, row: 22), Framed(162, 0, independent: true) })
                Require(!first.TryCreateFrameWitness(new[] { first, invalid }, out witness), "position, material, direction and exact raw member layout remain necessary");
            Require(!first.TryCreateFrameWitness(new[] { first }, out witness), "one unchanged frame is not a useful operating equivalence certificate");
            var door = Framed(0, 0, family: BlueprintWorldObjectFamily.Door);
            Require(!door.TryCreateFrameWitness(new[] { door, Framed(18, 0, family: BlueprintWorldObjectFamily.Door) }, out witness),
                "type/presence/footprint-changing doors cannot be certified by fixed-layout frame stores");
            var initial = Framed(0, 0); var middle = Framed(0, 54); var last = Framed(0, 108);
            Require(initial.TryCreateFrameWitness(new[] { initial, middle, last }, out witness) &&
                witness.TryTransition(100, 201, false, 74, 128, middle.FrameGroup, out member, out phase) && phase == last.FrameGroup &&
                witness.TryTransition(100, 201, false, 128, 20, phase, out member, out phase) && phase == initial.FrameGroup,
                "three-state vertical cycles retain row offsets and can wrap to the first certified phase");
        }
        private static BlueprintWorldObject DoorObject(int width = 1, bool left = false)
        { return new BlueprintWorldObject(left ? 99 : 100, 200, width, 3, 100, 200, 10, 0, 0, BlueprintWorldObjectFamily.Door, Array.Empty<int>()); }
        private static CaptureCell[] DoorTargets(int width = 1, bool different = false)
        {
            var cells = new CaptureCell[width * 3];
            for (int i = 0; i < cells.Length; i++) cells[i] = new CaptureCell { X = i % width, Y = i / width, ObjectId = 1,
                Layers = CaptureLayer.Tile, TileType = (ushort)(width == 1 ? 10 : 11), TilePaint = (byte)(different && i % width == 0 ? 3 : 4) };
            return cells;
        }
        private static BlueprintWorldMatchSample[] DoorUnion(BlueprintWorldObject observed, bool different = false, bool blockedLeft = false)
        {
            var samples = new BlueprintWorldMatchSample[9];
            for (int i = 0; i < 9; i++)
            {
                int x = 99 + i % 3, y = 200 + i / 3; bool member = observed.Contains(x, y);
                bool foreign = !member && blockedLeft && i % 3 == 0;
                byte paint = (byte)(member && different && x != observed.AnchorX ? 3 : 4);
                ushort type = (ushort)(member ? observed.Width == 1 ? 10 : 11 : foreign ? 1 : 222);
                short frameX = (short)(member ? observed.Width == 1 ? i / 3 * 18 : observed.WorldX == 99 ? 36 + (x - 99) * 18 : (x - 100) * 18 : 72);
                short frameY = (short)(member ? i / 3 * 18 : 14);
                ushort header = (ushort)((member || foreign ? 32 : 0) | paint);
                var raw = new BlueprintWorldTileWriteState(type, frameX, frameY, header, 0);
                var cell = new CaptureCell { X = x, Y = y, Layers = member || foreign ? CaptureLayer.Tile : CaptureLayer.None,
                    TileType = member || foreign ? type : (ushort)0, FrameX = member || foreign ? frameX : (short)0,
                    FrameY = member || foreign ? frameY : (short)0, TilePaint = member || foreign ? paint : (byte)0 };
                samples[i] = new BlueprintWorldMatchSample(true, cell, member ? observed : null, writeState: raw);
            }
            return samples;
        }
        private static BlueprintWorldMatchSample[] Occupied(BlueprintWorldObject observed, BlueprintWorldMatchSample[] union)
        { var cells = new List<BlueprintWorldMatchSample>(); foreach (var cell in union) if (observed.Contains(cell.Cell.X, cell.Cell.Y)) cells.Add(cell); return cells.ToArray(); }
        private static void DoorAppearance()
        {
            var open = DoorObject(2, true); var targets = DoorTargets(2, true); var union = DoorUnion(open, true);
            Require(BlueprintWorldMatchComparer.CompareObject(open, targets, Occupied(open, union), true) == BlueprintWorldMatchState.Match,
                "a saved open door with different hinge and wing paint matches its own complete two-column appearance");
            var opposite = DoorObject(2); var right = DoorUnion(opposite, true);
            Require(BlueprintWorldMatchComparer.CompareObject(open, targets, Occupied(opposite, right), true) == BlueprintWorldMatchState.Match,
                "open door member appearance follows hinge/wing roles across direction");
            var closed = DoorObject(); var closedUnion = DoorUnion(closed);
            Require(BlueprintWorldMatchComparer.CompareObject(open, targets, Occupied(closed, closedUnion), true) == BlueprintWorldMatchState.Conflict,
                "one closed member cannot satisfy two different saved open member paints");
            BlueprintWorldDoorWitness witness;
            Require(!BlueprintWorldDoorWitness.TryCreate(open, targets, open, union, out witness), "heterogeneous open target cannot get a cross-footprint equivalence certificate");
        }
        private static BlueprintWorldDoorWitness DoorChanges(BlueprintWorldDoorWitness witness, BlueprintWorldMatchSample[] target)
        {
            var states = new BlueprintWorldTileWriteState[9];
            for (int i = 0; i < 9; i++)
            {
                states[i] = witness.InitialState(i); var to = target[i].WriteState.Value;
                int[] kinds = { 0, 3, 5, 6, 7 }, oldValues = { states[i].Type, states[i].Header, states[i].Coatings, states[i].FrameX, states[i].FrameY },
                    newValues = { to.Type, to.Header, to.Coatings, to.FrameX, to.FrameY };
                for (int n = 0; n < kinds.Length; n++) if (oldValues[n] != newValues[n])
                {
                    int member; BlueprintWorldTileWriteState after;
                    Require(witness.TryTransition(99 + i % 3, 200 + i / 3, kinds[n], oldValues[n], newValues[n], states[i], out member, out after) && member == i,
                        "known raw residuals and actual delta values preserve the exact per-slot old-value chain");
                    states[i] = after;
                }
            }
            BlueprintWorldDoorWitness next; Require(witness.TryComplete(states, out next), "only complete allowed occupancy and all row appearances can commit a door certificate"); return next;
        }
        private static void DoorWitness()
        {
            var closed = DoorObject(); var union = DoorUnion(closed); BlueprintWorldDoorWitness witness;
            Require(BlueprintWorldDoorWitness.TryCreate(closed, DoorTargets(), closed, union, out witness), "complete current closed door plus all nine real raw slots can seed occupancy proof");
            Require(witness.InitialState(0).Type == 222 && witness.InitialState(0).FrameX == 72 && !witness.InitialState(0).Active,
                "inactive wing residual fields are retained rather than guessed as zero");
            var left = DoorObject(2, true); var opened = DoorUnion(left); witness = DoorChanges(witness, opened);
            witness = DoorChanges(witness, union); witness = DoorChanges(witness, DoorUnion(DoorObject(2))); witness = DoorChanges(witness, union);
            int member; BlueprintWorldTileWriteState state;
            Require(!witness.TryTransition(99, 200, 6, 0, 36, witness.InitialState(0), out member, out state), "wrong inactive residue old value cannot be silently accepted");
            Require(!witness.TryTransition(99, 200, 6, 72, 36, default(BlueprintWorldTileWriteState), out member, out state), "unknown member write state cannot gain certainty from a frame event");
            var states = new BlueprintWorldTileWriteState[9]; for (int i = 0; i < 9; i++) states[i] = witness.InitialState(i);
            var good = states[4]; states[4] = new BlueprintWorldTileWriteState(good.Type, good.FrameX, good.FrameY, (ushort)(good.Header ^ 1), good.Coatings);
            BlueprintWorldDoorWitness next; Require(!witness.TryComplete(states, out next), "a single row paint mismatch defeats occupancy equivalence"); states[4] = good;
            states[0] = opened[0].WriteState.Value; Require(!witness.TryComplete(states, out next), "a surviving active wing claiming this hinge cannot hide as extra content after closing");
            var blocked = DoorUnion(closed, blockedLeft: true);
            Require(BlueprintWorldDoorWitness.TryCreate(closed, DoorTargets(), closed, blocked, out witness), "an unused blocked wing does not forbid a valid opposite opening");
            witness = DoorChanges(witness, DoorUnion(DoorObject(2), blockedLeft: true));
            witness = DoorChanges(witness, blocked);
        }
    }
}
