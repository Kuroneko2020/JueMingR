using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    // Only block ranges are retained, never an entry per cell/object. A wide
    // row is queried by its X interval, not by taking every block touching Y.
    internal sealed class ProjectionDirectory
    {
        internal readonly struct CellBlock
        {
            internal CellBlock(long number, long first, long last) { Number = number; First = first; Last = last; }
            internal long Number { get; }
            internal long First { get; }
            internal long Last { get; }
        }
        internal readonly struct ObjectBlock
        {
            internal ObjectBlock(long number, int first, int last) { Number = number; First = first; Last = last; }
            internal long Number { get; }
            internal int First { get; }
            internal int Last { get; }
        }
        private readonly List<CellBlock> cells = new List<CellBlock>();
        private readonly List<ObjectBlock> objects = new List<ObjectBlock>();
        internal long EstimatedBytes { get { return 128L + cells.Capacity * 24L + objects.Capacity * 16L; } }
        internal int CellBlockCount { get { return cells.Count; } }
        internal int ObjectBlockCount { get { return objects.Count; } }
        internal void Add(long number, CaptureChunk chunk)
        {
            if (chunk.Count != 0) cells.Add(new CellBlock(number, Key(chunk.Cells[0].X, chunk.Cells[0].Y), Key(chunk.Cells[chunk.Count - 1].X, chunk.Cells[chunk.Count - 1].Y)));
            if (chunk.ObjectCount != 0) objects.Add(new ObjectBlock(number, chunk.Objects[0].Id, chunk.Objects[chunk.ObjectCount - 1].Id));
        }
        internal IEnumerable<long> Query(int left, int top, int right, int bottom, Action comparison = null)
        {
            if (left >= right || top >= bottom) yield break;
            var selected = new HashSet<long>(); int y = top;
            while (y < bottom)
            {
                long start = Key(left, y), end = Key(right - 1, y); int low = 0, high = cells.Count;
                while (low < high) { if (comparison != null) comparison(); int mid = low + (high - low) / 2; if (cells[mid].Last < start) low = mid + 1; else high = mid; }
                if (low == cells.Count) yield break;
                int firstY = (int)(cells[low].First >> 32);
                // A sparse overlap/object footprint may span millions of empty
                // rows. Binary search proves the gap before the next block empty.
                if (firstY > y) { y = firstY; continue; }
                int nextY = y + 1;
                for (int i = low; i < cells.Count && cells[i].First <= end; i++)
                {
                    if (selected.Add(cells[i].Number)) yield return cells[i].Number;
                    // All records before this selected block's last row are
                    // already covered by that whole block. Revisit its last row:
                    // additional X blocks can start there and must not be skipped.
                    int lastY = (int)(cells[i].Last >> 32); if (lastY > nextY) nextY = lastY;
                }
                y = nextY;
            }
        }
        internal long FindObject(int id, Action comparison = null)
        {
            int low = 0, high = objects.Count - 1;
            while (low <= high)
            {
                if (comparison != null) comparison(); int mid = low + (high - low) / 2; ObjectBlock block = objects[mid];
                if (block.Last < id) low = mid + 1; else if (block.First > id) high = mid - 1; else return block.Number;
            }
            throw new System.IO.InvalidDataException("blueprint-missing-object-reference");
        }
        internal static long Key(int x, int y) { return ((long)y << 32) | (uint)x; }
    }
}
