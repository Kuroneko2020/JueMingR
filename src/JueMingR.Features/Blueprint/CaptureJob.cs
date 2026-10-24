using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace JueMingR.Features.Blueprint
{
    // Only Advance may call the source, on its creating game thread. Sealed
    // chunks are detached from this builder and can safely pass to the worker.
    public sealed class CaptureJob
    {
        private readonly IBlueprintCaptureSource source;
        private readonly BlueprintSelectionSnapshot selection;
        private readonly int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private readonly Dictionary<long, int> covered = new Dictionary<long, int>();
        private readonly Dictionary<long, CaptureObject> groups = new Dictionary<long, CaptureObject>();
        private readonly List<CaptureObject> activeGroups = new List<CaptureObject>();
        private readonly HashSet<string> missing = new HashSet<string>();
        private readonly List<string> reasons = new List<string>();
        private CaptureCell[] cells = new CaptureCell[CaptureChunk.MaximumRecords];
        private CaptureObject[] objects = new CaptureObject[CaptureChunk.MaximumRecords];
        private int cellCount, objectCount, spanIndex, x, currentY = -1, nextObjectId;
        private int pruneY = -1, pruneIndex, pruneMember;
        private int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        private long contentCount, unreliable;
        private BlueprintObjectShape pendingShape;
        private CaptureCell[] objectCells;
        private CaptureCell firstCell;
        private int objectRead, objectVerify, objectEmit;
        private bool objectVerified, descriptorEmitted;
        public CaptureJob(IBlueprintCaptureSource source, BlueprintSelectionSnapshot selection, ulong startTick)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            StartTick = startTick;
            if (selection.SpanCount > 0) x = selection.GetSpan(0).Left;
        }
        public long Progress { get; private set; }
        public long Total { get { return selection.Count; } }
        public ulong StartTick { get; }
        public int LastReads { get; private set; }
        public long TotalReads { get; private set; }
        public bool Complete { get; private set; }
        public bool Cancelled { get; private set; }
        public string Error { get; private set; }
        public CaptureChunk PendingChunk { get; private set; }
        public void AcceptChunk() { PendingChunk = null; }
        public void Cancel() { Cancelled = true; pendingShape = null; objectCells = null; PendingChunk = null; covered.Clear(); groups.Clear(); activeGroups.Clear(); }

        public void Advance(int maximumReads, double milliseconds)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != threadId) throw new InvalidOperationException("Capture source belongs to the game thread.");
            LastReads = 0;
            if (Cancelled || Error != null || Complete || PendingChunk != null || maximumReads <= 0) return;
            if (hasDeferredCell) { hasDeferredCell = false; Add(deferredCell); }
            var timer = Stopwatch.StartNew();
            while (LastReads < maximumReads && timer.Elapsed.TotalMilliseconds < milliseconds)
            {
                if (PendingChunk != null) return;
                if (cellCount + objectCount >= CaptureChunk.MaximumRecords) { SealChunk(); return; }
                if (pendingShape != null) { AdvanceObject(); continue; }
                if (spanIndex >= selection.SpanCount)
                {
                    if (cellCount + objectCount != 0) SealChunk();
                    else Complete = true;
                    return;
                }
                BlueprintSpan span = selection.GetSpan(spanIndex);
                if (currentY != span.Y) { if (!PruneStep(span.Y)) continue; currentY = span.Y; }
                if (x >= span.RightExclusive)
                { spanIndex++; if (spanIndex < selection.SpanCount) x = selection.GetSpan(spanIndex).Left; continue; }
                long key = Key(x, span.Y);
                if (covered.ContainsKey(key)) { x++; Progress++; continue; }
                CaptureCell cell;
                if (!Read(x, span.Y, out cell)) { Error = "区域尚未收到或无法读取，请移动到附近后重试。"; return; }
                x++; Progress++;
                if ((cell.Layers & CaptureLayer.Tile) == 0) { Add(cell); continue; }
                BlueprintObjectShape shape = source.Describe(cell);
                if (shape == null) { Add(cell); continue; }
                long originKey = Key(shape.Object.X, shape.Object.Y);
                CaptureObject known;
                if (groups.TryGetValue(originKey, out known))
                {
                    if (known.Reliable) { Add(cell.WithoutTile()); continue; }
                    cell.ObjectId = known.Id; cell.UnreliableObject = true; Add(cell); continue;
                }
                shape.Object.Id = ++nextObjectId;
                long area = (long)shape.Object.Width * shape.Object.Height;
                if (!shape.Object.Reliable || area <= 0 || area >= CaptureChunk.MaximumRecords ||
                    shape.RowOffsets.Length != shape.Object.Height || shape.ColumnStep <= 0)
                {
                    shape.Object.Reliable = false;
                    if (string.IsNullOrEmpty(shape.Object.Reason)) shape.Object.Reason = "对象范围或外观无法可靠确认";
                    AddUnreliable(shape.Object, cell); continue;
                }
                pendingShape = shape; firstCell = cell;
                objectCells = new CaptureCell[(int)area]; objectRead = objectVerify = objectEmit = 0;
                objectVerified = descriptorEmitted = false;
            }
        }

        private bool Read(int readX, int readY, out CaptureCell cell)
        { LastReads++; TotalReads++; return source.Read(readX, readY, out cell); }
        private void AdvanceObject()
        {
            var shape = pendingShape;
            CaptureObject obj = shape.Object;
            if (objectRead < objectCells.Length)
            {
                int index = objectRead, px = obj.X + index % obj.Width, py = obj.Y + index / obj.Width;
                CaptureCell value;
                if (selection.Contains(px, py) && (py < firstCell.Y || py == firstCell.Y && px < firstCell.X))
                { RejectObject("对象在选区读取后出现，外观无法保持一致"); return; }
                if (!Read(px, py, out value) || covered.ContainsKey(Key(px, py)) || !Matches(shape, value, index))
                { RejectObject("对象成员尚未收到、残缺或外观不一致"); return; }
                objectCells[objectRead++] = value; return;
            }
            if (objectVerify < objectCells.Length)
            {
                CaptureCell again; CaptureCell before = objectCells[objectVerify];
                if (!Read(before.X, before.Y, out again) || !SameTile(before, again))
                { RejectObject("对象在采集期间发生变化，请排除后保存其余内容"); return; }
                objectVerify++; return;
            }
            if (!objectVerified)
            {
                objectVerified = true; groups.Add(Key(obj.X, obj.Y), obj); activeGroups.Add(obj);
                if (!string.IsNullOrEmpty(obj.MissingPayload)) missing.Add(obj.MissingPayload);
            }
            if (!descriptorEmitted) { objects[objectCount++] = obj; descriptorEmitted = true; return; }
            if (objectEmit < objectCells.Length)
            {
                CaptureCell value = objectCells[objectEmit++];
                value.ObjectId = obj.Id;
                if (!selection.Contains(value.X, value.Y))
                {
                    value.Layers = CaptureLayer.Tile; value.WallType = 0; value.WallPaint = 0;
                    value.Wires = value.LiquidAmount = value.LiquidKind = 0;
                    value.Actuator = value.InvisibleWall = value.FullbrightWall = false;
                }
                Add(value); covered.Add(Key(value.X, value.Y), obj.Id); return;
            }
            pendingShape = null; objectCells = null;
        }

        private bool Matches(BlueprintObjectShape shape, CaptureCell value, int index)
        {
            if ((value.Layers & CaptureLayer.Tile) == 0 || value.TileType != shape.Object.Type ||
                !shape.IndependentMemberFrames && value.FrameX != shape.BaseFrameX + index % shape.Object.Width * shape.ColumnStep ||
                value.FrameY != shape.BaseFrameY + shape.RowOffsets[index / shape.Object.Width]) return false;
            var member = source.Describe(value);
            return member != null && member.Object.Reliable && member.Object.X == shape.Object.X && member.Object.Y == shape.Object.Y &&
                member.Object.Width == shape.Object.Width && member.Object.Height == shape.Object.Height &&
                member.Object.Style == shape.Object.Style && member.Object.Alternate == shape.Object.Alternate && member.Object.Direction == shape.Object.Direction;
        }
        private void RejectObject(string reason)
        {
            var obj = pendingShape.Object; obj.Reliable = false; obj.Reason = reason;
            AddUnreliable(obj, firstCell); pendingShape = null; objectCells = null;
        }
        private void AddUnreliable(CaptureObject obj, CaptureCell cell)
        {
            groups[Key(obj.X, obj.Y)] = obj; activeGroups.Add(obj); unreliable++;
            if (reasons.Count < 6) reasons.Add("(" + obj.X + ", " + obj.Y + ") " + obj.Reason);
            objects[objectCount++] = obj;
            // A descriptor and its first cell can straddle chunks. Keep the
            // first cell pending rather than exceeding the closed-chunk bound.
            cell.ObjectId = obj.Id; cell.UnreliableObject = true;
            if (cellCount + objectCount == CaptureChunk.MaximumRecords)
            { SealChunk(); deferredCell = cell; hasDeferredCell = true; }
            else Add(cell);
        }
        private CaptureCell deferredCell;
        private bool hasDeferredCell;
        private void Add(CaptureCell value)
        {
            if (!value.HasContent) return;
            cells[cellCount++] = value; contentCount++;
            left = Math.Min(left, value.X); top = Math.Min(top, value.Y);
            right = Math.Max(right, checked(value.X + 1)); bottom = Math.Max(bottom, checked(value.Y + 1));
        }
        private void SealChunk()
        {
            PendingChunk = new CaptureChunk(cells, cellCount, objects, objectCount);
            cells = new CaptureCell[CaptureChunk.MaximumRecords]; objects = new CaptureObject[CaptureChunk.MaximumRecords]; cellCount = objectCount = 0;
        }
        private bool PruneStep(int y)
        {
            if (pruneY != y) { pruneY = y; pruneIndex = activeGroups.Count - 1; pruneMember = 0; }
            if (pruneIndex < 0) return true;
            CaptureObject obj = activeGroups[pruneIndex];
            if ((long)obj.Y + Math.Max(1, obj.Height) > y) { pruneIndex--; return false; }
            // Retiring a wide object or a crowded row is part of the same time
            // budget, not an unbounded pre-loop sweep hidden outside Advance.
            if (obj.Reliable && pruneMember < obj.Width * obj.Height)
            { covered.Remove(Key(obj.X + pruneMember % obj.Width, obj.Y + pruneMember / obj.Width)); pruneMember++; return false; }
            groups.Remove(Key(obj.X, obj.Y));
            activeGroups[pruneIndex] = activeGroups[activeGroups.Count - 1]; activeGroups.RemoveAt(activeGroups.Count - 1);
            pruneIndex--; pruneMember = 0; return false;
        }
        private static long Key(int keyX, int keyY) { return ((long)keyY << 32) | (uint)keyX; }
        private static bool SameTile(CaptureCell a, CaptureCell b)
        {
            return (b.Layers & CaptureLayer.Tile) != 0 && a.TileType == b.TileType && a.FrameX == b.FrameX && a.FrameY == b.FrameY &&
                a.TilePaint == b.TilePaint && a.Slope == b.Slope && a.HalfBrick == b.HalfBrick && a.Inactive == b.Inactive &&
                a.InvisibleTile == b.InvisibleTile && a.FullbrightTile == b.FullbrightTile;
        }
        public CaptureSummary Summary(ulong endTick)
        {
            if (!Complete || Error != null || Cancelled) throw new InvalidOperationException("Capture is not complete.");
            return new CaptureSummary { Left = contentCount == 0 ? 0 : left, Top = contentCount == 0 ? 0 : top,
                Width = contentCount == 0 ? 0 : right - left, Height = contentCount == 0 ? 0 : bottom - top,
                ContentCount = contentCount, UnreliableObjectCount = unreliable, StartTick = StartTick, EndTick = endTick,
                MissingPayloads = new List<string>(missing).ToArray(), UnreliableReasons = reasons.ToArray() };
        }
    }
}
