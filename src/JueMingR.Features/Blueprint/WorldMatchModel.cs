using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    public enum BlueprintWorldMatchState { Unknown, Missing, Conflict, Match }
    public enum BlueprintWorldObjectFamily { Metadata, Door }
    public enum BlueprintWorldFrameKind { Literal, TreeTrunk22, Palm22, Mushroom18, Platform18 }

    // Construction observations use the permanent mask's layer units without
    // its display-only Liquid bit. A Match never mutates the permanent mask.
    public readonly struct BlueprintWorldMatchResult
    {
        public BlueprintWorldMatchResult(ushort targetBits, ushort matchBits, ushort conflictBits, ushort unknownBits)
        {
            TargetBits = BlueprintConstructionTargets.FilterBits(targetBits); MatchBits = (ushort)(matchBits & TargetBits);
            ConflictBits = (ushort)(conflictBits & TargetBits & ~MatchBits);
            UnknownBits = (ushort)(unknownBits & TargetBits & ~MatchBits & ~ConflictBits);
        }
        public ushort TargetBits { get; }
        public ushort MatchBits { get; }
        public ushort ConflictBits { get; }
        public ushort UnknownBits { get; }
        public ushort MissingBits { get { return (ushort)(TargetBits & ~(MatchBits | ConflictBits | UnknownBits)); } }
        public int TargetCount { get { return Count(TargetBits); } }
        public int MatchCount { get { return Count(MatchBits); } }
        private static int Count(ushort value) { int count = 0; while (value != 0) { count += value & 1; value >>= 1; } return count; }
    }

    // Canonical building identity and the currently occupied rectangle are
    // separate. An open door occupies two columns; its building anchor is the
    // closed hinge column. Placement click origins are not building identity.
    public sealed class BlueprintWorldObject
    {
        private readonly int[] form;
        private readonly int[] rowOffsets;
        private readonly int baseFrameX, baseFrameY, columnStep;
        private readonly bool independentFrames;
        public BlueprintWorldObject(int worldX, int worldY, int width, int height, int anchorX, int anchorY,
            ushort kind, int style, int direction, BlueprintWorldObjectFamily family, int[] form, long frameGroup = 0, BlueprintObjectShape memberLayout = null)
        {
            if (width <= 0 || height <= 0 || (long)width * height >= CaptureChunk.MaximumRecords) throw new ArgumentException("Invalid object footprint.");
            WorldX = worldX; WorldY = worldY; Width = width; Height = height; AnchorX = anchorX; AnchorY = anchorY;
            Kind = kind; Style = style; Direction = direction; Family = family; FrameGroup = frameGroup;
            this.form = form == null ? Array.Empty<int>() : (int[])form.Clone();
            if (memberLayout != null)
            {
                baseFrameX = memberLayout.BaseFrameX; baseFrameY = memberLayout.BaseFrameY; columnStep = memberLayout.ColumnStep;
                rowOffsets = (int[])memberLayout.RowOffsets.Clone(); independentFrames = memberLayout.IndependentMemberFrames;
            }
        }
        public int WorldX { get; }
        public int WorldY { get; }
        public int Width { get; }
        public int Height { get; }
        public int AnchorX { get; }
        public int AnchorY { get; }
        public ushort Kind { get; }
        public int Style { get; }
        public int Direction { get; }
        public BlueprintWorldObjectFamily Family { get; }
        public long FrameGroup { get; }
        public int MemberCount { get { return Width * Height; } }
        // Conservative retained-value accounting for a suspended Host sample;
        // this object owns both private arrays, never source/world Tile data.
        public long EstimatedBytes { get { return 128L + form.LongLength * 4L + (rowOffsets?.LongLength ?? 0) * 4L; } }
        public bool Contains(int x, int y) { return x >= WorldX && y >= WorldY && (long)x < (long)WorldX + Width && (long)y < (long)WorldY + Height; }
        public bool SameBuilding(BlueprintWorldObject other)
        {
            if (other == null || Kind != other.Kind || Style != other.Style || Direction != other.Direction || Family != other.Family || form.Length != other.form.Length) return false;
            for (int i = 0; i < form.Length; i++) if (form[i] != other.form[i]) return false;
            return true;
        }
        public bool SamePlacement(BlueprintWorldObject other)
        { return SameBuilding(other) && AnchorX == other.AnchorX && AnchorY == other.AnchorY; }
        public bool SameObservedObject(BlueprintWorldObject other)
        { return SamePlacement(other) && WorldX == other.WorldX && WorldY == other.WorldY && Width == other.Width && Height == other.Height && FrameGroup == other.FrameGroup; }
        public bool MatchesMemberFrame(CaptureCell cell, int index)
        {
            if (rowOffsets == null) return true;
            if (index < 0 || index >= MemberCount || rowOffsets.Length != Height || columnStep <= 0) return false;
            if (cell.FrameY != baseFrameY + rowOffsets[index / Width]) return false;
            if (!independentFrames) return cell.FrameX == baseFrameX + index % Width * columnStep;
            // Locked closed-door rows choose exactly three decoration slots.
            // Independent does not grant arbitrary or cross-style raw frames.
            return cell.FrameX >= baseFrameX && cell.FrameX < baseFrameX + columnStep * 3 && (cell.FrameX - baseFrameX) % columnStep == 0;
        }

        // Candidates are decoded on the game thread from the original metadata
        // and must prove this same placement and fixed raw member layout. The
        // resulting profile contains values only; it cannot certify a group by
        // itself, nor describe type/active/footprint changes such as opening a door.
        public bool TryCreateFrameWitness(IReadOnlyList<BlueprintWorldObject> candidates, out BlueprintWorldObjectFrameWitness witness)
        {
            witness = null;
            if (Family != BlueprintWorldObjectFamily.Metadata || rowOffsets == null || rowOffsets.Length != Height ||
                independentFrames || columnStep <= 0 || candidates == null) return false;
            for (int i = 0; i < rowOffsets.Length; i++)
                if (rowOffsets[i] < 0 || i == 0 && rowOffsets[i] != 0 || i > 0 && rowOffsets[i] <= rowOffsets[i - 1]) return false;
            var phases = new List<long>();
            foreach (BlueprintWorldObject candidate in candidates)
            {
                if (!SamePlacement(candidate) || candidate.WorldX != WorldX || candidate.WorldY != WorldY || candidate.Width != Width || candidate.Height != Height ||
                    candidate.independentFrames || candidate.columnStep != columnStep || candidate.rowOffsets == null || candidate.rowOffsets.Length != rowOffsets.Length ||
                    candidate.baseFrameX < 0 || candidate.baseFrameY < 0 ||
                    (long)candidate.baseFrameX + (long)(Width - 1) * columnStep > short.MaxValue ||
                    (long)candidate.baseFrameY + rowOffsets[Height - 1] > short.MaxValue ||
                    candidate.FrameGroup != ((long)(uint)candidate.baseFrameX << 32 | (uint)candidate.baseFrameY)) return false;
                for (int i = 0; i < rowOffsets.Length; i++) if (candidate.rowOffsets[i] != rowOffsets[i]) return false;
                if (!phases.Contains(candidate.FrameGroup)) phases.Add(candidate.FrameGroup);
            }
            if (phases.Count < 2 || !phases.Contains(FrameGroup)) return false;
            witness = new BlueprintWorldObjectFrameWitness(WorldX, WorldY, Width, Height, Kind, FrameGroup, columnStep, rowOffsets, phases.ToArray());
            return true;
        }
    }

    // A small immutable frame/layout certificate, never a retained world DTO.
    // Its owner may install it only after a current complete Match. Each actual
    // store still changes the raw-read token. Preserving a completed canonical
    // proof additionally requires transaction coverage/coherence, no exception,
    // no overlapping writer and no other semantic field/identity change.
    public interface IBlueprintWorldObjectWriteWitness
    {
        int WorldX { get; }
        int WorldY { get; }
        int Width { get; }
        int Height { get; }
        int MemberCount { get; }
        ushort Kind { get; }
    }

    public sealed class BlueprintWorldObjectFrameWitness : IBlueprintWorldObjectWriteWitness
    {
        private readonly int columnStep;
        private readonly int[] rowOffsets;
        private readonly long[] phases;
        internal BlueprintWorldObjectFrameWitness(int x, int y, int width, int height, ushort kind, long initialPhase,
            int columnStep, int[] rowOffsets, long[] phases)
        {
            WorldX = x; WorldY = y; Width = width; Height = height; Kind = kind; InitialPhase = initialPhase;
            this.columnStep = columnStep; this.rowOffsets = (int[])rowOffsets.Clone(); this.phases = (long[])phases.Clone();
        }
        public int WorldX { get; }
        public int WorldY { get; }
        public int Width { get; }
        public int Height { get; }
        public ushort Kind { get; }
        public int MemberCount { get { return Width * Height; } }
        public long InitialPhase { get; }
        public long EstimatedBytes { get { return 128L + rowOffsets.LongLength * 4L + phases.LongLength * 8L; } }

        // The other axis comes from the previously certified member phase, not
        // a native Tile read in a write hook. Checking the exact old field value
        // rejects missing/reordered writes; the caller tracks member phases and
        // must not confuse one legal member transition with a complete object.
        public bool TryTransition(int x, int y, bool horizontal, int oldValue, int newValue, long currentMemberPhase,
            out int memberIndex, out long afterPhase)
        {
            memberIndex = -1; afterPhase = 0;
            long column = (long)x - WorldX, row = (long)y - WorldY;
            if (column < 0 || column >= Width || row < 0 || row >= Height || Array.IndexOf(phases, currentMemberPhase) < 0 ||
                newValue < 0 || newValue > short.MaxValue) return false;
            int dx = checked((int)column * columnStep), dy = rowOffsets[(int)row];
            int frameX = checked((int)(currentMemberPhase >> 32) + dx), frameY = checked((int)(currentMemberPhase & uint.MaxValue) + dy);
            if (oldValue != (horizontal ? frameX : frameY)) return false;
            int baseX = horizontal ? newValue - dx : frameX - dx, baseY = horizontal ? frameY - dy : newValue - dy;
            if (baseX < 0 || baseY < 0) return false;
            long candidate = (long)(uint)baseX << 32 | (uint)baseY;
            if (Array.IndexOf(phases, candidate) < 0) return false;
            memberIndex = checked((int)row * Width + (int)column); afterPhase = candidate; return true;
        }
    }

    // Only Tile comparison fields are retained, including inactive slots' real
    // residual values. Wire/actuator/wall/liquid bits remain independent. This
    // small value comes from the same gated read as CaptureCell, never a second
    // native lookup in a writer or an assumption that inactive raw fields are 0.
    public readonly struct BlueprintWorldTileWriteState
    {
        public BlueprintWorldTileWriteState(ushort type, short frameX, short frameY, ushort header, byte coatings)
        { Known = true; Type = type; FrameX = frameX; FrameY = frameY; Header = (ushort)(header & 0x747f); Coatings = (byte)(coatings & 0xa0); }
        public bool Known { get; }
        public ushort Type { get; }
        public short FrameX { get; }
        public short FrameY { get; }
        public ushort Header { get; }
        public byte Coatings { get; }
        public bool Active { get { return (Header & 0x20) != 0; } }
        internal uint Appearance { get { return ((uint)Header & ~0x20u) << 8 | Coatings; } }
        internal static uint CellAppearance(CaptureCell cell)
        { return ((uint)cell.TilePaint | (cell.Inactive ? 0x40u : 0) | (cell.HalfBrick ? 0x400u : 0) | (uint)cell.Slope << 12) << 8 |
                (cell.InvisibleTile ? 0x20u : 0) | (cell.FullbrightTile ? 0x80u : 0); }
        public bool MatchesCell(CaptureCell cell)
        {
            return Known && Active == ((cell.Layers & CaptureLayer.Tile) != 0) && (!Active ||
                Type == cell.TileType && FrameX == cell.FrameX && FrameY == cell.FrameY && Appearance == CellAppearance(cell));
        }
        internal bool TryApply(int fieldKind, int oldValue, int newValue, out BlueprintWorldTileWriteState after)
        {
            after = default(BlueprintWorldTileWriteState);
            if (!Known) return false;
            ushort type = Type, header = Header; short x = FrameX, y = FrameY; byte coatings = Coatings;
            switch (fieldKind)
            {
                case 0:
                    if (oldValue != Type || newValue < 0 || newValue > ushort.MaxValue) return false;
                    type = (ushort)newValue; break;
                case 3:
                    if (oldValue < 0 || oldValue > ushort.MaxValue || newValue < 0 || newValue > ushort.MaxValue || (oldValue & 0x747f) != Header) return false;
                    header = (ushort)newValue; break;
                case 5:
                    if (oldValue < 0 || oldValue > byte.MaxValue || newValue < 0 || newValue > byte.MaxValue || (oldValue & 0xa0) != Coatings) return false;
                    coatings = (byte)newValue; break;
                case 6:
                case 7:
                    if (oldValue != (fieldKind == 6 ? FrameX : FrameY) || newValue < short.MinValue || newValue > short.MaxValue) return false;
                    if (fieldKind == 6) x = (short)newValue; else y = (short)newValue; break;
                default: return false;
            }
            after = new BlueprintWorldTileWriteState(type, x, y, header, coatings); return true;
        }
    }

    // A door has three finite occupancy layouts in the hinge's 3x3 union.
    // Seeds retain exact small raw slot values, not CaptureCell/template DTOs.
    // Root transaction ownership, exceptional exit, overlapping writers, raw
    // read tokens and other proofs' independent invalidation remain external.
    public sealed class BlueprintWorldDoorWitness : IBlueprintWorldObjectWriteWitness
    {
        private readonly BlueprintWorldTileWriteState[] initial;
        private readonly uint[] appearance;
        private readonly int closedX, openX, baseY;
        private BlueprintWorldDoorWitness(int x, int y, int closedX, int openX, int baseY, uint[] appearance, BlueprintWorldTileWriteState[] states)
        {
            WorldX = x; WorldY = y; this.closedX = closedX; this.openX = openX; this.baseY = baseY;
            this.appearance = appearance; initial = (BlueprintWorldTileWriteState[])states.Clone();
        }
        public int WorldX { get; }
        public int WorldY { get; }
        public int Width { get { return 3; } }
        public int Height { get { return 3; } }
        public int MemberCount { get { return 9; } }
        public ushort Kind { get { return 10; } }
        public BlueprintWorldTileWriteState InitialState(int memberIndex)
        { if (memberIndex < 0 || memberIndex >= 9) throw new ArgumentOutOfRangeException(nameof(memberIndex)); return initial[memberIndex]; }

        // The game-thread adapter must first prove current complete Match.
        // Every union slot, including blocked/unused wings, needs a real gated
        // raw seed. A source open door with two different row appearances may
        // match an open layout but cannot preserve that target across closing.
        public static bool TryCreate(BlueprintWorldObject target, IReadOnlyList<CaptureCell> targets, BlueprintWorldObject observed,
            IReadOnlyList<BlueprintWorldMatchSample> union, out BlueprintWorldDoorWitness witness)
        {
            witness = null;
            if (target == null || observed == null || !target.SamePlacement(observed) || target.Family != BlueprintWorldObjectFamily.Door ||
                target.Height != 3 || target.Width < 1 || target.Width > 2 || observed.Height != 3 || observed.Width < 1 || observed.Width > 2 ||
                observed.WorldY != observed.AnchorY || observed.AnchorX < observed.WorldX || (long)observed.AnchorX >= (long)observed.WorldX + observed.Width ||
                targets == null || targets.Count != target.MemberCount || union == null || union.Count != 9 || observed.Style < 0 ||
                observed.AnchorX == int.MinValue || observed.AnchorY > int.MaxValue - 2) return false;
            long closed = (long)(observed.Style / 36) * 54, open = (long)(observed.Style / 36) * 72, yBase = (long)(observed.Style % 36) * 54;
            if (closed + 36 > short.MaxValue || open + 54 > short.MaxValue || yBase + 36 > short.MaxValue) return false;
            var rows = new uint[3]; var states = new BlueprintWorldTileWriteState[9];
            var occupied = new List<BlueprintWorldMatchSample>(observed.MemberCount);
            for (int row = 0; row < 3; row++)
            {
                CaptureCell expected = targets[row * target.Width];
                if ((expected.Layers & CaptureLayer.Tile) == 0 || expected.TilePaint > 31 || expected.Slope > 4 || expected.HalfBrick && expected.Slope != 0) return false;
                rows[row] = BlueprintWorldTileWriteState.CellAppearance(expected);
                for (int column = 1; column < target.Width; column++)
                    if (BlueprintWorldTileWriteState.CellAppearance(targets[row * target.Width + column]) != rows[row]) return false;
            }
            for (int i = 0; i < 9; i++)
            {
                var sample = union[i]; int x = observed.AnchorX - 1 + i % 3, y = observed.AnchorY + i / 3;
                if (!sample.Available || sample.Cell.X != x || sample.Cell.Y != y || !sample.WriteState.HasValue || !sample.WriteState.Value.MatchesCell(sample.Cell)) return false;
                states[i] = sample.WriteState.Value;
                if (observed.Contains(x, y)) occupied.Add(sample);
            }
            if (BlueprintWorldMatchComparer.CompareObject(target, targets, occupied, true) != BlueprintWorldMatchState.Match) return false;
            var candidate = new BlueprintWorldDoorWitness(observed.AnchorX - 1, observed.AnchorY, (int)closed, (int)open, (int)yBase, rows, states);
            if (!candidate.Complete(states)) return false;
            witness = candidate; return true;
        }
        public bool TryTransition(int x, int y, int fieldKind, int oldValue, int newValue, BlueprintWorldTileWriteState current,
            out int memberIndex, out BlueprintWorldTileWriteState after)
        {
            memberIndex = -1; after = default(BlueprintWorldTileWriteState);
            long column = (long)x - WorldX, row = (long)y - WorldY;
            if (column < 0 || column >= 3 || row < 0 || row >= 3 || !current.TryApply(fieldKind, oldValue, newValue, out after)) return false;
            memberIndex = (int)(row * 3 + column); return true;
        }
        public bool TryComplete(IReadOnlyList<BlueprintWorldTileWriteState> states, out BlueprintWorldDoorWitness next)
        {
            next = null;
            if (!Complete(states)) return false;
            var copy = new BlueprintWorldTileWriteState[9]; for (int i = 0; i < 9; i++) copy[i] = states[i];
            next = new BlueprintWorldDoorWitness(WorldX, WorldY, closedX, openX, baseY, appearance, copy); return true;
        }
        private bool Complete(IReadOnlyList<BlueprintWorldTileWriteState> states)
        {
            if (states == null || states.Count != 9) return false;
            for (int i = 0; i < 9; i++) if (!states[i].Known) return false;
            for (int pose = 0; pose < 3; pose++)
            {
                bool valid = true;
                for (int i = 0; i < 9 && valid; i++)
                {
                    int row = i / 3, column = i % 3; var state = states[i];
                    bool member = column == 1 || pose == 1 && column == 0 || pose == 2 && column == 2;
                    if (!member)
                    {
                        // Extra foreign Tile is transparent. An active open-door
                        // fragment claiming this hinge/top is still this object's
                        // damaged wing and cannot be hidden as unrelated content.
                        if (state.Active && state.Type == 11)
                        {
                            int slot = state.FrameX < 0 ? -1 : state.FrameX % 72;
                            if (state.FrameY < 0 || state.FrameY % 18 != 0 || slot != 0 && slot != 18 && slot != 36 && slot != 54) valid = false;
                            else if (WorldX + column + (slot == 36 ? 1 : slot == 18 ? -1 : 0) == WorldX + 1 &&
                                row - state.FrameY % 54 / 18 == 0) valid = false;
                        }
                        continue;
                    }
                    if (!state.Active || state.Appearance != appearance[row] || state.FrameY != baseY + row * 18) { valid = false; continue; }
                    if (pose == 0) valid = state.Type == 10 && state.FrameX >= closedX && state.FrameX < closedX + 54 && (state.FrameX - closedX) % 18 == 0;
                    else valid = state.Type == 11 && state.FrameX == openX + (pose == 1 ? 36 + column * 18 : (column - 1) * 18);
                }
                if (valid) return true;
            }
            return false;
        }
    }

    // Available=false is an unreadable sample, never an empty cell. Object=null
    // with ObjectUnknown=false means a legitimate per-cell snapshot, including
    // natural frame-important content without rectangular object metadata.
    public readonly struct BlueprintWorldMatchSample
    {
        public BlueprintWorldMatchSample(bool available, CaptureCell cell, BlueprintWorldObject obj = null,
            bool compareFrames = false, short semanticFrameX = 0, short semanticFrameY = 0, bool objectUnknown = false,
            BlueprintWorldFrameKind frameKind = BlueprintWorldFrameKind.Literal, BlueprintWorldTileWriteState? writeState = null)
        { Available = available; Cell = cell; Object = obj; CompareFrames = compareFrames; SemanticFrameX = semanticFrameX; SemanticFrameY = semanticFrameY; ObjectUnknown = objectUnknown; FrameKind = frameKind; WriteState = writeState; }
        public bool Available { get; }
        public CaptureCell Cell { get; }
        public BlueprintWorldObject Object { get; }
        public bool CompareFrames { get; }
        public short SemanticFrameX { get; }
        public short SemanticFrameY { get; }
        public bool ObjectUnknown { get; }
        public BlueprintWorldFrameKind FrameKind { get; }
        public BlueprintWorldTileWriteState? WriteState { get; }
    }
}
