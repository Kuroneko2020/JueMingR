using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    public enum BlueprintTrimEdgeSide { Top, Bottom, Left, Right }

    public readonly struct BlueprintTrimEdge
    {
        public BlueprintTrimEdge(BlueprintTrimEdgeSide side, int fixedCoordinate, int start, int endExclusive)
        { Side = side; Fixed = fixedCoordinate; Start = start; EndExclusive = endExclusive; }
        public BlueprintTrimEdgeSide Side { get; }
        public int Fixed { get; }
        public int Start { get; }
        public int EndExclusive { get; }
    }

    // Numeric geometry for one trim operation, never a source of layer facts.
    // Host owns a live instance. A successfully admitted pending instance may
    // instead be transferred to the worker; the previous owner must then stop
    // mutating it (including Clear). No row/list is shared between instances.
    public sealed class BlueprintTrimGeometry
    {
        private readonly bool preserveRectangleEdges;
        private readonly Dictionary<int, List<BlueprintSpan>> rows = new Dictionary<int, List<BlueprintSpan>>();
        private readonly Dictionary<int, List<BlueprintSpan>> topEdges = new Dictionary<int, List<BlueprintSpan>>();
        private readonly Dictionary<int, List<BlueprintSpan>> bottomEdges = new Dictionary<int, List<BlueprintSpan>>();
        private readonly Dictionary<int, List<int>> leftEdges = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, List<int>> rightEdges = new Dictionary<int, List<int>>();

        // The manual layer keeps the union silhouette. The object Tile layer
        // preserves each verified object's real outline, including internal
        // outlines inside another selected area, while deduplicating each edge.
        public BlueprintTrimGeometry(bool preserveRectangleEdges = false)
        { this.preserveRectangleEdges = preserveRectangleEdges; }
        public bool HasContent { get { return rows.Count != 0; } }
#if DEBUG
        public int DebugChangedRows { get; private set; }
        public int DebugVisibleRows { get; private set; }
        public int DebugVisibleSpans { get; private set; }
        public int DebugVisibleSearchComparisons { get; private set; }
        public int DebugVisibleEdgeRows { get; private set; }
        public int DebugVisibleEdges { get; private set; }
        public int DebugVisibleEdgeSearchComparisons { get; private set; }
        public int DebugStoredRows { get { return rows.Count; } }
#endif

        // Only newly covered all-layer geometry is returned. In particular,
        // callers must not subtract the separate object Tile extension layer.
        // The returned delta has its own list; later insertions cannot alter it.
        public IReadOnlyList<BlueprintSpan> AddRectangle(BlueprintProjectionBounds rectangle)
        {
#if DEBUG
            DebugChangedRows = 0;
#endif
            if (rectangle.Width == 0 || rectangle.Height == 0) return Array.Empty<BlueprintSpan>();
            var added = new List<BlueprintSpan>();
            for (int y = rectangle.Top; y < rectangle.Bottom; y++)
                AddRow(y, rectangle.Left, rectangle.Right, added);
            if (preserveRectangleEdges)
            {
                AddInterval(topEdges, rectangle.Top, rectangle.Left, rectangle.Right);
                AddInterval(bottomEdges, rectangle.Bottom, rectangle.Left, rectangle.Right);
                for (int y = rectangle.Top; y < rectangle.Bottom; y++)
                { AddPoint(leftEdges, y, rectangle.Left); AddPoint(rightEdges, y, rectangle.Right); }
            }
            return added.Count == 0 ? (IReadOnlyList<BlueprintSpan>)Array.Empty<BlueprintSpan>() : added.AsReadOnly();
        }

        public void AddSpans(IEnumerable<BlueprintSpan> spans)
        {
            if (spans == null) throw new ArgumentNullException(nameof(spans));
#if DEBUG
            DebugChangedRows = 0;
#endif
            var added = new List<BlueprintSpan>();
            foreach (BlueprintSpan span in spans)
            {
                if (span.Left >= span.RightExclusive) continue;
                if (span.Y == int.MaxValue) throw new ArgumentOutOfRangeException(nameof(spans));
                added.Clear();
                AddRow(span.Y, span.Left, span.RightExclusive, added);
                if (preserveRectangleEdges)
                {
                    AddInterval(topEdges, span.Y, span.Left, span.RightExclusive);
                    AddInterval(bottomEdges, span.Y + 1, span.Left, span.RightExclusive);
                    AddPoint(leftEdges, span.Y, span.Left); AddPoint(rightEdges, span.Y, span.RightExclusive);
                }
            }
        }

        public bool Contains(int x, int y)
        {
            List<BlueprintSpan> row;
            if (!rows.TryGetValue(y, out row)) return false;
            int index = FirstOverlap(row, x);
            return index < row.Count && row[index].Left <= x;
        }

        // Full enumeration belongs to a transferred pending batch, not Draw.
        // Row order is immaterial; each row is maximally merged and ordered.
        public IEnumerable<BlueprintSpan> EnumerateSpans()
        { foreach (List<BlueprintSpan> row in rows.Values) foreach (BlueprintSpan span in row) yield return span; }

        public void Clear()
        { rows.Clear(); topEdges.Clear(); bottomEdges.Clear(); leftEdges.Clear(); rightEdges.Clear(); }

        // Lookup follows viewport height and binary searches each visible row;
        // neither offscreen rows nor the historical rectangle/object list exists
        // on this path. Clipping changes fills, never creates an outline edge.
        public IEnumerable<BlueprintSpan> EnumerateVisible(int left, int top, int rightExclusive, int bottomExclusive)
        {
#if DEBUG
            DebugVisibleRows = DebugVisibleSpans = DebugVisibleSearchComparisons = 0;
#endif
            if (left >= rightExclusive || top >= bottomExclusive || !HasContent) yield break;
            for (int y = top; y < bottomExclusive; y++)
            {
#if DEBUG
                DebugVisibleRows++;
#endif
                List<BlueprintSpan> row;
                if (!rows.TryGetValue(y, out row)) continue;
                int low = 0, high = row.Count;
                while (low < high)
                {
                    int middle = low + (high - low) / 2;
#if DEBUG
                    DebugVisibleSearchComparisons++;
#endif
                    if (row[middle].RightExclusive <= left) low = middle + 1; else high = middle;
                }
                for (int i = low; i < row.Count && row[i].Left < rightExclusive; i++)
                {
#if DEBUG
                    DebugVisibleSpans++;
#endif
                    yield return new BlueprintSpan(y, Math.Max(left, row[i].Left), Math.Min(rightExclusive, row[i].RightExclusive));
                }
            }
        }

        public IEnumerable<BlueprintTrimEdge> EnumerateVisibleEdges(int left, int top, int rightExclusive, int bottomExclusive)
        {
#if DEBUG
            DebugVisibleEdgeRows = DebugVisibleEdges = DebugVisibleEdgeSearchComparisons = 0;
#endif
            if (left >= rightExclusive || top >= bottomExclusive || !HasContent) yield break;
            for (int y = top; ; y++)
            {
#if DEBUG
                DebugVisibleEdgeRows++;
#endif
                for (int side = 0; side < 2; side++)
                {
                    if (side == 0 ? y == bottomExclusive : y == top) continue;
                    List<BlueprintSpan> row;
                    if (!(side == 0 ? topEdges : bottomEdges).TryGetValue(y, out row)) continue;
                    for (int i = FirstVisibleEdgeSpan(row, left); i < row.Count && row[i].Left < rightExclusive; i++)
                    {
#if DEBUG
                        DebugVisibleEdges++;
#endif
                        yield return new BlueprintTrimEdge(side == 0 ? BlueprintTrimEdgeSide.Top : BlueprintTrimEdgeSide.Bottom,
                            y, Math.Max(left, row[i].Left), Math.Min(rightExclusive, row[i].RightExclusive));
                    }
                }
                if (y == bottomExclusive) break;
            }
            for (int y = top; y < bottomExclusive; y++)
            {
#if DEBUG
                DebugVisibleEdgeRows++;
#endif
                for (int side = 0; side < 2; side++)
                {
                    List<int> row;
                    if (!(side == 0 ? leftEdges : rightEdges).TryGetValue(y, out row)) continue;
                    // Bottom/right ink is inside its real boundary, matching Outline.
                    for (int i = FirstVisibleEdgePoint(row, left, side == 1); i < row.Count && (side == 1 ? row[i] <= rightExclusive : row[i] < rightExclusive); i++)
                    {
#if DEBUG
                        DebugVisibleEdges++;
#endif
                        yield return new BlueprintTrimEdge(side == 0 ? BlueprintTrimEdgeSide.Left : BlueprintTrimEdgeSide.Right, row[i], y, y + 1);
                    }
                }
            }
        }

        private void AddRow(int y, int left, int right, List<BlueprintSpan> added)
        {
            List<BlueprintSpan> row;
            if (!rows.TryGetValue(y, out row)) { row = new List<BlueprintSpan>(); rows.Add(y, row); }
            int first = FirstAdjacent(row, left), after = first, cursor = left, mergedLeft = left, mergedRight = right, addedStart = added.Count;
            while (after < row.Count && row[after].Left <= right)
            {
                BlueprintSpan span = row[after++];
                if (cursor < span.Left) added.Add(new BlueprintSpan(y, cursor, Math.Min(span.Left, right)));
                cursor = Math.Max(cursor, span.RightExclusive);
                mergedLeft = Math.Min(mergedLeft, span.Left); mergedRight = Math.Max(mergedRight, span.RightExclusive);
            }
            if (cursor < right) added.Add(new BlueprintSpan(y, cursor, right));
            if (addedStart == added.Count) return;
            if (!preserveRectangleEdges)
                for (int i = first; i < after; i++)
                { RemovePoint(leftEdges, y, row[i].Left); RemovePoint(rightEdges, y, row[i].RightExclusive); }
            row.RemoveRange(first, after - first); row.Insert(first, new BlueprintSpan(y, mergedLeft, mergedRight));
            if (!preserveRectangleEdges)
            {
                AddPoint(leftEdges, y, mergedLeft); AddPoint(rightEdges, y, mergedRight);
                for (int i = addedStart; i < added.Count; i++)
                {
                    BlueprintSpan delta = added[i];
                    // Only the new strip changes horizontal boundaries. Its
                    // selected neighbours lose their former inside-facing edge.
                    RemoveInterval(bottomEdges, y, delta.Left, delta.RightExclusive);
                    AddExposed(topEdges, y, y == int.MinValue ? (int?)null : y - 1, delta.Left, delta.RightExclusive);
                    RemoveInterval(topEdges, y + 1, delta.Left, delta.RightExclusive);
                    AddExposed(bottomEdges, y + 1, y + 1, delta.Left, delta.RightExclusive);
                }
            }
#if DEBUG
            DebugChangedRows++;
#endif
        }

        private void AddExposed(Dictionary<int, List<BlueprintSpan>> edges, int boundary, int? neighbourY, int left, int right)
        {
            List<BlueprintSpan> neighbour;
            if (!neighbourY.HasValue || !rows.TryGetValue(neighbourY.Value, out neighbour)) { AddInterval(edges, boundary, left, right); return; }
            int cursor = left;
            for (int i = FirstOverlap(neighbour, left); i < neighbour.Count && neighbour[i].Left < right; i++)
            {
                BlueprintSpan span = neighbour[i];
                if (cursor < span.Left) AddInterval(edges, boundary, cursor, Math.Min(right, span.Left));
                cursor = Math.Max(cursor, span.RightExclusive);
                if (cursor >= right) return;
            }
            if (cursor < right) AddInterval(edges, boundary, cursor, right);
        }

        private static int FirstOverlap(List<BlueprintSpan> row, int left)
        {
            int low = 0, high = row.Count;
            while (low < high) { int middle = low + (high - low) / 2; if (row[middle].RightExclusive <= left) low = middle + 1; else high = middle; }
            return low;
        }
        private static int FirstAdjacent(List<BlueprintSpan> row, int left)
        {
            int low = 0, high = row.Count;
            while (low < high) { int middle = low + (high - low) / 2; if (row[middle].RightExclusive < left) low = middle + 1; else high = middle; }
            return low;
        }
        private static void AddInterval(Dictionary<int, List<BlueprintSpan>> index, int y, int left, int right)
        {
            if (left >= right) return;
            List<BlueprintSpan> row;
            if (!index.TryGetValue(y, out row)) { row = new List<BlueprintSpan>(); index.Add(y, row); }
            int first = FirstAdjacent(row, left), after = first;
            while (after < row.Count && row[after].Left <= right)
            { left = Math.Min(left, row[after].Left); right = Math.Max(right, row[after].RightExclusive); after++; }
            row.RemoveRange(first, after - first); row.Insert(first, new BlueprintSpan(y, left, right));
        }
        private static void RemoveInterval(Dictionary<int, List<BlueprintSpan>> index, int y, int left, int right)
        {
            List<BlueprintSpan> row;
            if (!index.TryGetValue(y, out row)) return;
            int first = FirstOverlap(row, left), after = first;
            while (after < row.Count && row[after].Left < right) after++;
            if (first == after) return;
            int oldLeft = row[first].Left, oldRight = row[after - 1].RightExclusive;
            row.RemoveRange(first, after - first);
            if (oldRight > right) row.Insert(first, new BlueprintSpan(y, right, oldRight));
            if (oldLeft < left) row.Insert(first, new BlueprintSpan(y, oldLeft, left));
            if (row.Count == 0) index.Remove(y);
        }
        private static void AddPoint(Dictionary<int, List<int>> index, int y, int x)
        {
            List<int> row;
            if (!index.TryGetValue(y, out row)) { row = new List<int>(); index.Add(y, row); }
            int found = row.BinarySearch(x); if (found < 0) row.Insert(~found, x);
        }
        private static void RemovePoint(Dictionary<int, List<int>> index, int y, int x)
        {
            List<int> row;
            if (!index.TryGetValue(y, out row)) return;
            int found = row.BinarySearch(x); if (found >= 0) row.RemoveAt(found);
            if (row.Count == 0) index.Remove(y);
        }

        private int FirstVisibleEdgeSpan(List<BlueprintSpan> row, int left)
        {
            int low = 0, high = row.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
#if DEBUG
                DebugVisibleEdgeSearchComparisons++;
#endif
                if (row[middle].RightExclusive <= left) low = middle + 1; else high = middle;
            }
            return low;
        }
        private int FirstVisibleEdgePoint(List<int> row, int left, bool insideLeft)
        {
            int low = 0, high = row.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
#if DEBUG
                DebugVisibleEdgeSearchComparisons++;
#endif
                if (insideLeft ? row[middle] <= left : row[middle] < left) low = middle + 1; else high = middle;
            }
            return low;
        }
    }
}
