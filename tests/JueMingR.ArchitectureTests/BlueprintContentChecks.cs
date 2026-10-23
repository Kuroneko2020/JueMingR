using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Blueprint;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintContentChecks
    {
        internal static void Check(IList<string> failures)
        {
            try { Sparse(); Objects(); BudgetAndScale(); Lifetimes(); }
            catch (Exception error) { failures.Add("BlueprintContent: " + error.Message); }
        }
        private static void Sparse()
        {
            var selected = new BlueprintSelection(); selected.ToggleRectangle(0, 0, 6, 6, 10, 10);
            selected.ToggleRectangle(3, 3, 3, 3, 10, 10);
            var source = new Grid();
            source.Values[Key(2, 2)] = new CaptureCell { X = 2, Y = 2, Layers = CaptureLayer.Wall | CaptureLayer.Wire | CaptureLayer.Actuator | CaptureLayer.Liquid,
                WallType = 7, WallPaint = 4, Wires = 15, Actuator = true, LiquidAmount = 209, LiquidKind = 3, FullbrightWall = true };
            source.Values[Key(3, 3)] = new CaptureCell { X = 3, Y = 3, Layers = CaptureLayer.Tile, TileType = 999 };
            source.Values[Key(5, 5)] = new CaptureCell { X = 5, Y = 5, Layers = CaptureLayer.Tile, TileType = 1, Inactive = true, HalfBrick = true, TilePaint = 12, InvisibleTile = true };
            var snapshot = selected.Freeze(); selected.Clear();
            var job = new CaptureJob(source, snapshot, 12); var records = Drain(job, 3);
            CaptureSummary summary = job.Summary(91);
            Require(records.Count == 2 && summary.Left == 2 && summary.Top == 2 && summary.Width == 4 && summary.Height == 4, "air border trims; interior XOR hole has no cell");
            var wall = records.Single(c => c.X == 2); var tile = records.Single(c => c.X == 5);
            Require((wall.Layers & CaptureLayer.Tile) == 0 && wall.Wires == 15 && wall.LiquidKind == 3 && wall.LiquidAmount == 209 && wall.Actuator && wall.FullbrightWall, "all meaningful non-tile layers retain independent values");
            Require(tile.Inactive && tile.TilePaint == 12 && tile.HalfBrick && tile.InvisibleTile, "inactive foreground remains content");
            source.Values.Clear(); Require(records[0].WallType == 7 && summary.StartTick == 12 && summary.EndTick == 91, "closed draft remains fixed after source change");
            var empty = new CaptureJob(new Grid(), snapshot, 0); Require(Drain(empty, 2).Count == 0 && empty.Summary(1).ContentCount == 0, "pure air never creates content");
        }
        private static void Objects()
        {
            var source = new Grid();
            for (int y = 4; y < 7; y++) for (int x = 4; x < 6; x++)
                source.Values[Key(x, y)] = new CaptureCell { X = x, Y = y, Layers = CaptureLayer.Tile | CaptureLayer.Wall | CaptureLayer.Wire | CaptureLayer.Liquid | CaptureLayer.Actuator,
                    TileType = 21, FrameX = (short)((x - 4) * 18), FrameY = (short)((y - 4) * 18), WallType = 3, Wires = 15, LiquidAmount = 90, LiquidKind = 2, Actuator = true };
            source.Shape = cell => cell.TileType != 21 ? null : new BlueprintObjectShape { BaseFrameX = 0, BaseFrameY = 0, ColumnStep = 18, RowOffsets = new[] { 0, 18, 36 },
                Object = new CaptureObject { X = 4, Y = 4, Width = 2, Height = 3, Type = 21, Style = 3, Alternate = 1, Direction = 1, OriginX = 0, OriginY = 2, Reliable = true, MissingPayload = "箱内物品未采集" } };
            var selected = new BlueprintSelection(); selected.ToggleRectangle(5, 6, 5, 6, 20, 20);
            var job = new CaptureJob(source, selected.Freeze(), 0); var cells = Drain(job, 1);
            Require(cells.Count == 6 && job.Summary(5).Width == 2 && job.Summary(5).Height == 3, "partial object completes full reliable footprint across read budgets");
            Require(cells.Count(c => c.Layers != CaptureLayer.Tile) == 1 && cells.Single(c => c.X == 5 && c.Y == 6).WallType == 3, "outside selection only object tile expands; selected layers remain");
            Require(cells.All(c => c.ObjectId == 1 && !c.UnreliableObject) && job.Summary(5).MissingPayloads.Single() == "箱内物品未采集", "one object identity and concrete missing payload");
            source.Values.Remove(Key(4, 4));
            var broken = new CaptureJob(source, selected.Freeze(), 0); var brokenCells = Drain(broken, 1);
            Require(brokenCells.Count == 1 && brokenCells[0].UnreliableObject && brokenCells[0].WithoutTile().WallType == 3 && broken.Summary(4).UnreliableObjectCount == 1, "broken object is explicit; excluding foreground preserves confirmed wall/wires/liquid");
            source.Unreadable = Key(5, 6); var unknown = new CaptureJob(source, selected.Freeze(), 0); unknown.Advance(10, 100);
            Require(unknown.Error != null && !unknown.Complete && unknown.PendingChunk == null, "unreceived content is failure, never air");
        }
        private static void BudgetAndScale()
        {
            var source = new Grid { DefaultContent = true };
            var snapshot = new BlueprintSelectionSnapshot(new[] { new BlueprintSpan(0, 0, 10000), new BlueprintSpan(0, 1000000, 1010000) }, 2);
            var job = new CaptureJob(source, snapshot, 0); long count = 0;
            for (int steps = 0; !job.Complete && steps < 50000; steps++)
            {
                job.Advance(1, 100); Require(job.LastReads <= 1, "each advance obeys source read budget");
                if (job.PendingChunk != null)
                {
                    count += job.PendingChunk.Count; long before = job.TotalReads; job.Advance(100, 100);
                    Require(job.TotalReads == before, "pending chunk applies backpressure"); job.AcceptChunk();
                }
            }
            Require(job.Complete && count == 20000 && job.Summary(10).Width == 1010000, "large sparse selection exceeds 16384 without bbox allocation or truncation");
            var boundary = new Grid { DefaultContent = true, Shape = cell => cell.X == 2047 ? new BlueprintObjectShape { Object = new CaptureObject { X = 2047, Width = 1, Height = 1, Type = 1, Reason = "未确认" } } : null };
            var chunkJob = new CaptureJob(boundary, new BlueprintSelectionSnapshot(new[] { new BlueprintSpan(0, 0, 2050) }, 1), 0);
            var boundaryCells = Drain(chunkJob, 1024);
            Require(boundaryCells.Count == 2050 && boundaryCells.Count(c => c.UnreliableObject) == 1, "descriptor at full chunk boundary never drops or duplicates its first cell");
            var cancel = new CaptureJob(source, snapshot, 0); cancel.Advance(2, 100); long read = cancel.TotalReads; cancel.Cancel(); cancel.Advance(999, 100);
            Require(cancel.TotalReads == read && cancel.Cancelled, "cancellation stops further live reads");
        }
        private static void Lifetimes()
        {
            var state = new BlueprintCaptureState(); state.Enter(); long mode = state.ModeGeneration;
            state.Selection.ToggleRectangle(1, 1, 2, 2, 100, 100); long revision = state.Selection.Revision;
            var frozen = state.Selection.Freeze(); state.Selection.ToggleRectangle(3, 3, 3, 3, 100, 100);
            Require(state.Selection.Revision != revision && frozen.Count == 4 && !frozen.Contains(3, 3), "selection snapshots and revisions remain independent");
            state.Exit(); state.Enter(); Require(state.ModeGeneration != mode, "exit/reenter cannot collide with old capture mode");
            state.Exit(); state.Observe(true, true, true, true, true); state.SetInteractionBlocked(true);
            Require(state.BlockedActions == 3 && !state.Active, "library blocks world actions with capture mode off");
            state.SetInteractionBlocked(false); state.Observe(false, false, false, false, false); Require(state.BlockedActions == 3, "UI close/focus synthetic release cannot retire held world tails");
            state.Observe(true, false, false, false, false); state.Observe(true, false, false, false, false); Require(state.BlockedActions == 0, "trusted physical neutral retires UI tails");
        }
        private static List<CaptureCell> Drain(CaptureJob job, int reads)
        {
            var result = new List<CaptureCell>();
            for (int i = 0; !job.Complete && job.Error == null && i < 100000; i++)
            {
                job.Advance(reads, 100);
                if (job.PendingChunk != null) { result.AddRange(job.PendingChunk.Cells.Take(job.PendingChunk.Count)); job.AcceptChunk(); }
            }
            Require(job.Complete && job.Error == null, "capture completes without an unexplained failure: " + job.Error); return result;
        }
        private static long Key(int x, int y) { return ((long)y << 32) | (uint)x; }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private sealed class Grid : IBlueprintCaptureSource
        {
            internal readonly Dictionary<long, CaptureCell> Values = new Dictionary<long, CaptureCell>();
            internal Func<CaptureCell, BlueprintObjectShape> Shape;
            internal long Unreadable = -1;
            internal bool DefaultContent;
            public bool Read(int x, int y, out CaptureCell cell)
            {
                if (!Values.TryGetValue(Key(x, y), out cell)) cell = new CaptureCell { X = x, Y = y, Layers = DefaultContent ? CaptureLayer.Tile : CaptureLayer.None, TileType = DefaultContent ? (ushort)1 : (ushort)0 };
                return Key(x, y) != Unreadable;
            }
            public BlueprintObjectShape Describe(CaptureCell cell) { return Shape?.Invoke(cell); }
        }
    }
}
