using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    public readonly struct BlueprintRectangle
    {
        public BlueprintRectangle(int left, int top, int rightExclusive, int bottomExclusive)
        { Left = left; Top = top; RightExclusive = rightExclusive; BottomExclusive = bottomExclusive; }
        public int Left { get; }
        public int Top { get; }
        public int RightExclusive { get; }
        public int BottomExclusive { get; }
        public bool IsEmpty { get { return Left >= RightExclusive || Top >= BottomExclusive; } }

        // Normalize inclusive tile endpoints before intersecting the world. A
        // wholly outside drag is empty; clamping each endpoint would invent a tile.
        public static BlueprintRectangle FromEndpoints(int startX, int startY, int endX, int endY, int worldWidth, int worldHeight)
        {
            if (worldWidth <= 0 || worldHeight <= 0) return default(BlueprintRectangle);
            int left = (int)Math.Max(0L, Math.Min((long)worldWidth, Math.Min(startX, endX)));
            int top = (int)Math.Max(0L, Math.Min((long)worldHeight, Math.Min(startY, endY)));
            int right = (int)Math.Max(0L, Math.Min((long)worldWidth, (long)Math.Max(startX, endX) + 1));
            int bottom = (int)Math.Max(0L, Math.Min((long)worldHeight, (long)Math.Max(startY, endY) + 1));
            return new BlueprintRectangle(left, top, right, bottom);
        }
    }

    public readonly struct BlueprintSpan
    {
        public BlueprintSpan(int y, int left, int rightExclusive)
        { Y = y; Left = left; RightExclusive = rightExclusive; }
        public int Y { get; }
        public int Left { get; }
        public int RightExclusive { get; }
    }

    // Coordinate storage only. The session Host owns when to commit/clear;
    // this class neither observes tiles nor owns mode, input or persistence.
    public sealed class BlueprintSelection
    {
        private readonly Dictionary<int, List<BlueprintSpan>> rows = new Dictionary<int, List<BlueprintSpan>>();
        public long Revision { get; private set; }
        public bool HasSelection { get { return rows.Count != 0; } }
#if DEBUG
        // Finite workload regression probes, absent from normal Release.
        public int DebugChangedRows { get; private set; }
        public int DebugVisibleRows { get; private set; }
        public int DebugVisibleSpans { get; private set; }
        public int DebugVisibleSearchComparisons { get; private set; }
#endif
        public void Clear() { if (rows.Count != 0) { rows.Clear(); Revision++; } }

        public BlueprintSelectionSnapshot Freeze()
        {
            var ordered = new List<BlueprintSpan>();
            foreach (var row in rows.Values) ordered.AddRange(row);
            ordered.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.Left.CompareTo(b.Left));
            return new BlueprintSelectionSnapshot(ordered.ToArray(), Revision);
        }

        public void ToggleRectangle(int startX, int startY, int endX, int endY, int worldWidth, int worldHeight)
        {
#if DEBUG
            DebugChangedRows = 0;
#endif
            BlueprintRectangle rectangle = BlueprintRectangle.FromEndpoints(startX, startY, endX, endY, worldWidth, worldHeight);
            if (rectangle.IsEmpty) return;
            Revision++;
            for (int y = rectangle.Top; y < rectangle.BottomExclusive; y++)
            {
                List<BlueprintSpan> row;
                if (!rows.TryGetValue(y, out row)) { row = new List<BlueprintSpan>(); rows.Add(y, row); }
                ToggleRow(row, y, rectangle.Left, rectangle.RightExclusive);
                if (row.Count == 0) rows.Remove(y);
#if DEBUG
                DebugChangedRows++;
#endif
            }
        }

        // Iterate viewport Y directly rather than walking the selected rows.
        // Each row binary-searches the first overlap, then yields clipped spans;
        // no expansion into individual tiles or offscreen selection scan occurs.
        public IEnumerable<BlueprintSpan> EnumerateVisible(int left, int top, int rightExclusive, int bottomExclusive)
        {
#if DEBUG
            DebugVisibleRows = DebugVisibleSpans = DebugVisibleSearchComparisons = 0;
#endif
            if (left >= rightExclusive || top >= bottomExclusive || rows.Count == 0) yield break;
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

        private static void ToggleRow(List<BlueprintSpan> row, int y, int left, int right)
        {
            int low = 0, high = row.Count;
            // Include adjacency so the replacement remains maximally merged.
            while (low < high)
            { int middle = low + (high - low) / 2; if (row[middle].RightExclusive < left) low = middle + 1; else high = middle; }
            int first = low, after = first, cursor = left;
            var replacement = new List<BlueprintSpan>();
            while (after < row.Count && row[after].Left <= right)
            {
                BlueprintSpan span = row[after++];
                if (span.Left < left) AddMerged(replacement, y, span.Left, left);
                if (cursor < span.Left) AddMerged(replacement, y, cursor, Math.Min(span.Left, right));
                cursor = Math.Max(cursor, span.RightExclusive);
                if (span.RightExclusive > right) AddMerged(replacement, y, right, span.RightExclusive);
            }
            if (cursor < right) AddMerged(replacement, y, cursor, right);
            // Only intersecting/adjacent spans change. List movement may shift
            // the same row's suffix, but other rows and their lists are untouched.
            row.RemoveRange(first, after - first);
            row.InsertRange(first, replacement);
        }

        private static void AddMerged(List<BlueprintSpan> output, int y, int left, int right)
        {
            if (left >= right) return;
            int last = output.Count - 1;
            if (last >= 0 && output[last].RightExclusive == left)
                output[last] = new BlueprintSpan(y, output[last].Left, right);
            else output.Add(new BlueprintSpan(y, left, right));
        }
    }
}
