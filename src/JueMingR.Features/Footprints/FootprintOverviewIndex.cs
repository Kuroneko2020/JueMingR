using System;
using System.Collections.Generic;
using System.IO;
using JueMingR.Platform.Footprints;

namespace JueMingR.Features.Footprints
{
    // The archive worker is the sole owner. No game-thread arrays or mutable
    // recorder lists enter this index; published snapshots own frozen arrays.
    public sealed class FootprintOverviewIndex
    {
        private const float CellSize = 128f;
        private sealed class Page
        {
            internal FootprintOverviewGeometry Geometry;
            internal LinkedListNode<long> Lease;
        }
        private sealed class Selection
        {
            internal long Start, End;
            internal FootprintOverviewEdge[] Edges;
            internal long Bytes { get { return 128L + (Edges == null ? 0 : Edges.Length * 72L); } }
        }
        private struct EdgeKey : IEquatable<EdgeKey>
        {
            internal long X1, Y1, X2, Y2;
            public bool Equals(EdgeKey other) { return X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2; }
            public override bool Equals(object value) { return value is EdgeKey && Equals((EdgeKey)value); }
            public override int GetHashCode() { unchecked { return (((X1.GetHashCode() * 397 ^ Y1.GetHashCode()) * 397 ^ X2.GetHashCode()) * 397) ^ Y2.GetHashCode(); } }
        }
        private readonly FootprintArchive archive;
        private readonly string generation;
        private readonly long cacheBudget, metadataBudget;
        private Dictionary<long, Page> pages = new Dictionary<long, Page>();
        private readonly LinkedList<long> lru = new LinkedList<long>();
        private readonly Dictionary<long, List<long>> spatial = new Dictionary<long, List<long>>();
        private Dictionary<long, Selection> selected = new Dictionary<long, Selection>();
        private readonly List<FootprintOverviewEdge> edges = new List<FootprintOverviewEdge>();
        private readonly HashSet<EdgeKey> distinct = new HashSet<EdgeKey>();
        private readonly List<long> candidates = new List<long>();
        private FootprintOverviewRequest request, selectionView;
        private FootprintSample[] tail = new FootprintSample[0];
        private long builtBlocks, spatialBlocks, builtEnd, metadataBytes, selectedBytes;
        private int nextCandidate;
        private float epsilon;
        private bool queryPending, spatialFull;
        public FootprintOverviewIndex(FootprintArchive archive, long cacheBudgetBytes = 8 * 1024 * 1024, long metadataBudgetBytes = 16 * 1024 * 1024)
        {
            this.archive = archive ?? throw new ArgumentNullException(nameof(archive)); generation = archive.Generation;
            if (cacheBudgetBytes < 65536 || metadataBudgetBytes < 1024) throw new ArgumentOutOfRangeException(nameof(cacheBudgetBytes));
            cacheBudget = cacheBudgetBytes; metadataBudget = metadataBudgetBytes;
        }
        public long BuiltBlocks { get { return builtBlocks; } }
        public bool CaughtUp { get { return builtBlocks == archive.BlockCount; } }
        public bool QueryPending { get { return queryPending; } }
        public FootprintOverviewSnapshot Result { get; private set; }
        public long CacheBytes { get; private set; }
        public long MetadataBytes { get { return metadataBytes; } }
        public long SelectionBytes { get { return selectedBytes; } }
        public long SourcePageReads { get; private set; }
        public long CachePageReads { get; private set; }
        public long GeometryBuilds { get; private set; }
        public long SelectionBuilds { get; private set; }
        public long SpatialCellVisits { get; private set; }

        public bool BuildStep(int maxBlocks = 8)
        {
            RequireGeneration(); if (maxBlocks < 1) throw new ArgumentOutOfRangeException(nameof(maxBlocks));
            for (int i = 0; i < maxBlocks && builtBlocks < archive.BlockCount; i++)
            {
                long number = builtBlocks; var geometry = GetPage(number, true); builtEnd = geometry.End;
                if (!spatialFull)
                {
                    var cells = new HashSet<long>();
                    // Register the actual selectable chords, not a diagonal's
                    // enclosing rectangle. A coarse chord can cross different
                    // cells from its original leaves, so both are indexed.
                    foreach (var node in geometry.Nodes) WalkCells(node.X1, node.Y1, node.X2, node.Y2, (x, y) => cells.Add(Cell(x, y)));
                    long added = cells.Count * 16L;
                    foreach (long cell in cells) if (!spatial.ContainsKey(cell)) added += 96;
                    if (metadataBytes + added > metadataBudget) spatialFull = true;
                    else
                    {
                        foreach (long cell in cells)
                        {
                            List<long> values; if (!spatial.TryGetValue(cell, out values)) { values = new List<long>(); spatial.Add(cell, values); }
                            values.Add(number);
                        }
                        metadataBytes += added; spatialBlocks++;
                    }
                }
                builtBlocks++;
                if (request != null) { if (Intersects(geometry)) candidates.Add(number); queryPending = true; }
            }
            return CaughtUp;
        }

        public void BeginQuery(FootprintOverviewRequest value, FootprintSample[] committedTail)
        {
            RequireGeneration();
            if (value == null || value.Generation != generation || !FootprintSample.Finite(value.Zoom) || value.Zoom <= 0 ||
                !FootprintSample.Finite(value.MinX) || !FootprintSample.Finite(value.MinY) || !FootprintSample.Finite(value.MaxX) || !FootprintSample.Finite(value.MaxY) ||
                value.MinX > value.MaxX || value.MinY > value.MaxY || value.Until < 0) throw new ArgumentException("footprint-overview-request");
            if (committedTail != null && committedTail.Length > 256) throw new ArgumentException("footprint-overview-tail");
            if (request != null && request.Serial == value.Serial && request.Until == value.Until && SameView(request, value))
            {
                tail = committedTail == null ? new FootprintSample[0] : (FootprintSample[])committedTail.Clone();
                queryPending = true; return;
            }
            if (!SameView(selectionView, value)) { selected.Clear(); selectedBytes = 0; selectionView = value; }
            request = value; tail = committedTail == null ? new FootprintSample[0] : (FootprintSample[])committedTail.Clone();
            epsilon = .375f / value.Zoom; edges.Clear(); distinct.Clear(); candidates.Clear(); nextCandidate = 0;
            var found = new HashSet<long>();
            int minX = Grid(value.MinX - 2f / value.Zoom), minY = Grid(value.MinY - 2f / value.Zoom);
            int maxX = Grid(value.MaxX + 2f / value.Zoom), maxY = Grid(value.MaxY + 2f / value.Zoom);
            // Empty or adversarial wide views do not enumerate billions of
            // absent cells. Enumerating the bounded existing table is equivalent.
            long area = ((long)maxX - minX + 1) * ((long)maxY - minY + 1);
            if (area > spatial.Count * 2L)
            {
                foreach (var entry in spatial)
                {
                    int x = (int)(entry.Key >> 32), y = unchecked((int)entry.Key); SpatialCellVisits++;
                    if (x < minX || x > maxX || y < minY || y > maxY) continue;
                    foreach (long number in entry.Value) found.Add(number);
                }
            }
            else for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
            {
                List<long> values; SpatialCellVisits++; if (spatial.TryGetValue(Cell(x, y), out values)) foreach (long number in values) found.Add(number);
            }
            candidates.AddRange(found); candidates.Sort();
            // Beyond the metadata ceiling, retain full coverage by streaming
            // numbered derived pages. Recording never stops or deletes history.
            for (long number = spatialBlocks; number < builtBlocks; number++) candidates.Add(number);
            queryPending = true;
        }

        public FootprintOverviewSnapshot QueryStep(int maxBlocks = 8)
        {
            RequireGeneration(); if (maxBlocks < 1) throw new ArgumentOutOfRangeException(nameof(maxBlocks));
            if (!queryPending) return Result;
            for (int i = 0; i < maxBlocks && nextCandidate < candidates.Count; i++, nextCandidate++)
            {
                long number = candidates[nextCandidate]; Selection selection;
                if (selected.TryGetValue(number, out selection))
                {
                    if (selection.Start > request.Until) continue;
                    if (selection.Edges != null && selection.End <= request.Until) { foreach (var edge in selection.Edges) Add(edge); continue; }
                }
                var geometry = GetPage(number);
                if (geometry.Nodes.Length == 0 || !Intersects(geometry)) { Remember(number, 0, geometry.End, new FootprintOverviewEdge[0]); continue; }
                long start = geometry.Nodes[0].Start;
                if (start > request.Until) { Remember(number, start, geometry.End, null); continue; }
                var output = new List<FootprintOverviewEdge>(); SelectionBuilds++;
                geometry.Select(request.Until, epsilon, edge => { if (Visible(edge)) output.Add(edge); });
                foreach (var edge in output) Add(edge);
                Remember(number, start, geometry.End, geometry.End <= request.Until ? output.ToArray() : null);
            }
            bool pagesDone = nextCandidate == candidates.Count;
            var published = new List<FootprintOverviewEdge>(edges);
            // An unindexed immutable prefix is never relabeled as covered by a
            // much later root tail. The replaceable finite root remains separate
            // from reusable sealed output, including after it becomes sealed.
            if (pagesDone && CaughtUp) FootprintOverviewGeometry.Build(tail).Select(request.Until, epsilon, edge => { if (Visible(edge)) published.Add(edge); });
            long sequence = pagesDone ? builtBlocks * 256 : 0;
            long end = pagesDone ? builtEnd : 0;
            if (pagesDone && CaughtUp && tail.Length != 0) { sequence = tail[tail.Length - 1].Sequence; end = tail[tail.Length - 1].End; }
            Result = new FootprintOverviewSnapshot(generation, request.Serial, request.Until, sequence, end,
                pagesDone && CaughtUp, published.ToArray());
            queryPending = !pagesDone; return Result;
        }

        public FootprintOverviewSnapshot Query(FootprintOverviewRequest value, FootprintSample[] committedTail)
        { BeginQuery(value, committedTail); while (QueryPending) QueryStep(); return Result; }

        public void CancelQuery()
        {
            queryPending = false; request = selectionView = null; tail = new FootprintSample[0]; Result = null;
            candidates.Clear(); candidates.TrimExcess(); edges.Clear(); edges.TrimExcess(); distinct.Clear(); distinct.TrimExcess();
            selected = new Dictionary<long, Selection>(); selectedBytes = 0;
            // Persisted derived pages survive closing the map; resident geometry
            // and per-viewport output have no reason to retain their memory.
            pages = new Dictionary<long, Page>(); lru.Clear(); CacheBytes = 0;
        }

        private FootprintOverviewGeometry GetPage(long number, bool verifySource = false)
        {
            // Each source block is verified once when this worker first indexes
            // it, including when a derived cache exists. Subsequent reads reuse
            // only those checked immutable identities; a sidecar is no authority.
            FootprintSample[] source = null;
            if (verifySource) { source = archive.ReadOverviewBlock(number); SourcePageReads++; }
            Page page;
            if (pages.TryGetValue(number, out page))
            {
                if (source == null || page.Geometry.MatchesSource(source)) { lru.Remove(page.Lease); lru.AddLast(page.Lease); return page.Geometry; }
                pages.Remove(number); lru.Remove(page.Lease); CacheBytes -= page.Geometry.Bytes;
            }
            FootprintOverviewGeometry geometry = null;
            try
            {
                byte[] cached = archive.ReadOverviewCache(number); CachePageReads++;
                if (cached != null) geometry = FootprintOverviewGeometry.Decode(cached, generation, number);
                if (geometry != null && source != null && !geometry.MatchesSource(source)) geometry = null;
            }
            catch (Exception e) when (CacheFailure(e)) { }
            if (geometry == null)
            {
                // Source errors propagate to the Store's protected read path;
                // only reconstructible derived-cache errors are disposable.
                if (source == null) { source = archive.ReadOverviewBlock(number); SourcePageReads++; }
                geometry = FootprintOverviewGeometry.Build(source); GeometryBuilds++;
                try { archive.WriteOverviewCache(number, geometry.Encode(generation, number)); }
                catch (Exception e) when (CacheFailure(e)) { }
            }
            while (CacheBytes + geometry.Bytes > cacheBudget && lru.Count != 0)
            {
                long victim = lru.First.Value; CacheBytes -= pages[victim].Geometry.Bytes; pages.Remove(victim); lru.RemoveFirst();
            }
            var lease = lru.AddLast(number); pages.Add(number, new Page { Geometry = geometry, Lease = lease }); CacheBytes += geometry.Bytes; return geometry;
        }

        private void Remember(long number, long start, long end, FootprintOverviewEdge[] output)
        {
            var value = new Selection { Start = start, End = end, Edges = output }; Selection previous;
            long oldBytes = selected.TryGetValue(number, out previous) ? previous.Bytes : 0;
            if (selectedBytes - oldBytes + value.Bytes > cacheBudget) return;
            selected[number] = value; selectedBytes += value.Bytes - oldBytes;
        }

        private void Add(FootprintOverviewEdge edge)
        {
            if (!Visible(edge)) return;
            double unit = .25 / request.Zoom;
            var key = new EdgeKey { X1 = Round(edge.X1 / unit), Y1 = Round(edge.Y1 / unit), X2 = Round(edge.X2 / unit), Y2 = Round(edge.Y2 / unit) };
            if (key.X1 > key.X2 || (key.X1 == key.X2 && key.Y1 > key.Y2))
            { long x = key.X1, y = key.Y1; key.X1 = key.X2; key.Y1 = key.Y2; key.X2 = x; key.Y2 = y; }
            if (distinct.Add(key)) edges.Add(edge);
        }
        private bool Intersects(FootprintOverviewGeometry geometry)
        { float margin = 2f / request.Zoom; return geometry.MaxX >= request.MinX - margin && geometry.MinX <= request.MaxX + margin && geometry.MaxY >= request.MinY - margin && geometry.MinY <= request.MaxY + margin; }
        private bool Visible(FootprintOverviewEdge edge)
        {
            double from = 0, to = 1, dx = edge.X2 - edge.X1, dy = edge.Y2 - edge.Y1; float margin = 2f / request.Zoom;
            return Clip(-dx, edge.X1 - request.MinX + margin, ref from, ref to) && Clip(dx, request.MaxX + margin - edge.X1, ref from, ref to) &&
                Clip(-dy, edge.Y1 - request.MinY + margin, ref from, ref to) && Clip(dy, request.MaxY + margin - edge.Y1, ref from, ref to);
        }
        private static bool Clip(double p, double q, ref double from, ref double to)
        { if (p == 0) return q >= 0; double r = q / p; if (p < 0) { if (r > to) return false; if (r > from) from = r; } else { if (r < from) return false; if (r < to) to = r; } return true; }
        private void RequireGeneration() { if (archive.Generation != generation) throw new InvalidOperationException("footprint-overview-generation"); }
        private static bool SameView(FootprintOverviewRequest a, FootprintOverviewRequest b)
        { return a != null && a.Generation == b.Generation && a.MinX == b.MinX && a.MinY == b.MinY && a.MaxX == b.MaxX && a.MaxY == b.MaxY && a.Zoom == b.Zoom; }
        private static bool CacheFailure(Exception e)
        { return e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is ArgumentException || e is FormatException || e is OverflowException; }
        private static long Round(double value) { return checked((long)Math.Round(value)); }
        private static int Grid(float value) { return checked((int)Math.Floor(value / CellSize)); }
        private static long Cell(int x, int y) { return ((long)x << 32) | (uint)y; }
        private static void WalkCells(float x1, float y1, float x2, float y2, Action<int, int> visit)
        {
            int x = Grid(x1), y = Grid(y1), endX = Grid(x2), endY = Grid(y2); visit(x, y);
            double dx = x2 - x1, dy = y2 - y1; int sx = Math.Sign(dx), sy = Math.Sign(dy);
            double tx = sx == 0 ? Double.PositiveInfinity : ((sx > 0 ? (x + 1) * CellSize : x * CellSize) - x1) / dx;
            double ty = sy == 0 ? Double.PositiveInfinity : ((sy > 0 ? (y + 1) * CellSize : y * CellSize) - y1) / dy;
            double stepX = sx == 0 ? Double.PositiveInfinity : CellSize / Math.Abs(dx), stepY = sy == 0 ? Double.PositiveInfinity : CellSize / Math.Abs(dy);
            while (x != endX || y != endY)
            {
                if (tx < ty) { x += sx; tx += stepX; }
                else if (ty < tx) { y += sy; ty += stepY; }
                else { x += sx; y += sy; tx += stepX; ty += stepY; }
                visit(x, y);
            }
        }
    }
}
