using System;
using System.Collections.Generic;
using System.Collections;

namespace JueMingR.Features.Notes
{
    public sealed class NotesTextLine
    {
        internal NotesTextLine(int first, int last, int start, int end) { First = first; Last = last; Start = start; End = end; }
        public int First { get; }
        public int Last { get; }
        public int Start { get; }
        public int End { get; }
    }
    // A layout shares the immutable, already validated text index. Construction
    // does no full-text scan. Host advances all visible layouts under one budget;
    // only finished lines can answer caret/navigation queries while it catches up.
    public sealed class NotesTextLayout
    {
        private const int BlockSize = 1024;
        private readonly IReadOnlyList<int> boundaries;
        private readonly List<float[]> advances = new List<float[]>();
        private readonly LineBuffer lines = new LineBuffer();
        private readonly Func<string, float> measure;
        private readonly float width;
        private int processed, first;
        private float x;
        public NotesTextLayout(string text, float width, Func<string, float> measure)
            : this(text, width, measure, Array.AsReadOnly(TextElements.Boundaries(text)))
        { while (!Complete) Continue(8192); }
        public NotesTextLayout(string text, float width, Func<string, float> measure, IReadOnlyList<int> index,
            NotesTextLayout previous = null, int changedStart = 0)
        {
            if (width <= 0 || Single.IsNaN(width) || Single.IsInfinity(width)) throw new ArgumentException("invalid-text-width");
            Text = text; this.width = width; this.measure = measure; boundaries = index;
            if (previous != null && previous.width == width && changedStart > 0)
            {
                // Restart before the changed visual line. Prefix metric blocks are
                // immutable and shared; only its final block is copied for writes.
                int line = Math.Max(0, previous.LineOf(changedStart) - 1);
                lines.SharePrefix(previous.lines, line);
                processed = first = previous.lines[line].First;
                for (int i = 0; i < processed / BlockSize; i++) advances.Add(previous.advances[i]);
                if (processed % BlockSize != 0) advances.Add((float[])previous.advances[processed / BlockSize].Clone());
            }
            lines.Add(new NotesTextLine(first, processed, boundaries[first], boundaries[processed]));
        }
        public string Text { get; }
        public bool Complete { get { return processed == boundaries.Count - 1; } }
        public IReadOnlyList<NotesTextLine> Lines { get { return lines; } }
        public bool CanLocate(int offset) { return Complete || offset < lines[lines.Count - 1].Start; }
        public int Continue(int unitBudget)
        {
            if (Complete || unitBudget <= 0) return 0;
            int used = 0; lines.RemoveLast();
            while (processed < boundaries.Count - 1)
            {
                int units = boundaries[processed + 1] - boundaries[processed];
                if (units > unitBudget - used) break;
                used += units; string element = Element(processed);
                while (advances.Count <= processed / BlockSize) advances.Add(new float[BlockSize]);
                if (element.IndexOf('\n') >= 0 || element.IndexOf('\r') >= 0)
                { lines.Add(new NotesTextLine(first, processed, boundaries[first], boundaries[processed])); first = ++processed; x = 0; continue; }
                float advance = measure(element);
                if (advance < 0 || Single.IsNaN(advance) || Single.IsInfinity(advance)) throw new ArgumentException("invalid-text-metric");
                advances[processed / BlockSize][processed % BlockSize] = advance;
                // Also wrap dense/zero-advance text at a complete element boundary.
                // Width clipping alone cannot bound a line containing many invisible
                // glyphs. This is a visual break only; stored text is unchanged.
                if ((x + advance > width || boundaries[processed + 1] - boundaries[first] > 1024) && processed > first)
                { lines.Add(new NotesTextLine(first, processed, boundaries[first], boundaries[processed])); first = processed; x = 0; }
                x += advance; processed++;
            }
            lines.Add(new NotesTextLine(first, processed, boundaries[first], boundaries[processed]));
            return used;
        }
        public string Element(int index) { return Text.Substring(boundaries[index], boundaries[index + 1] - boundaries[index]); }
        public float Advance(int index) { return advances[index / BlockSize][index % BlockSize]; }
        public int LineOf(int offset)
        {
            int low = 0, high = lines.Count - 1;
            while (low < high) { int mid = (low + high + 1) / 2; if (lines[mid].Start <= offset) low = mid; else high = mid - 1; }
            return low;
        }
        public float CaretX(int offset)
        {
            NotesTextLine line = lines[LineOf(offset)]; float result = 0;
            for (int i = line.First; i < line.Last && boundaries[i] < offset; i++) result += Advance(i);
            return result;
        }
        public int Hit(float targetX, int lineIndex)
        {
            if (lineIndex >= lines.Count - 1 && !Complete) return -1;
            NotesTextLine line = lines[Math.Max(0, Math.Min(lines.Count - 1, lineIndex))]; float current = 0;
            for (int i = line.First; i < line.Last; i++)
            { if (targetX < current + Advance(i) / 2) return boundaries[i]; current += Advance(i); }
            return line.End;
        }
        public int LineHome(int offset) { return lines[LineOf(offset)].Start; }
        public int LineEnd(int offset) { return lines[LineOf(offset)].End; }
        public int Vertical(int offset, int direction) { return Hit(CaretX(offset), LineOf(offset) + direction); }
        private sealed class LineBuffer : IReadOnlyList<NotesTextLine>
        {
            private readonly List<NotesTextLine[]> blocks = new List<NotesTextLine[]>();
            public int Count { get; private set; }
            public NotesTextLine this[int index] { get { return blocks[index / BlockSize][index % BlockSize]; } }
            internal void Add(NotesTextLine line)
            {
                if (blocks.Count <= Count / BlockSize) blocks.Add(new NotesTextLine[BlockSize]);
                blocks[Count / BlockSize][Count % BlockSize] = line; Count++;
            }
            internal void RemoveLast() { Count--; blocks[Count / BlockSize][Count % BlockSize] = null; }
            internal void SharePrefix(LineBuffer source, int count)
            {
                // A newline-dense body may have a million visual lines. Share its
                // completed blocks rather than copying a million references for
                // every tail keystroke outside the incremental layout budget.
                for (int i = 0; i < count / BlockSize; i++) blocks.Add(source.blocks[i]);
                if (count % BlockSize != 0) blocks.Add((NotesTextLine[])source.blocks[count / BlockSize].Clone());
                Count = count;
            }
            public IEnumerator<NotesTextLine> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        }
    }
}
