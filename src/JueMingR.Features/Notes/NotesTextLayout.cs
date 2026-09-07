using System;
using System.Collections.Generic;

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
    // Each grapheme is measured and drawn separately with the same host metric.
    // This avoids prefix remeasurement and divergent hit/caret wrapping rules.
    public sealed class NotesTextLayout
    {
        private readonly int[] boundaries;
        private readonly float[] advances;
        private readonly List<NotesTextLine> lines = new List<NotesTextLine>();
        public NotesTextLayout(string text, float width, Func<string, float> measure)
        {
            if (width <= 0 || Single.IsNaN(width) || Single.IsInfinity(width)) throw new ArgumentException("invalid-text-width");
            Text = text; boundaries = TextElements.Boundaries(text); advances = new float[boundaries.Length - 1];
            int first = 0; float x = 0;
            for (int i = 0; i < advances.Length; i++)
            {
                string element = Element(i);
                if (element.IndexOf('\n') >= 0 || element.IndexOf('\r') >= 0)
                { lines.Add(new NotesTextLine(first, i, boundaries[first], boundaries[i])); first = i + 1; x = 0; continue; }
                float advance = measure(element);
                if (advance < 0 || Single.IsNaN(advance) || Single.IsInfinity(advance)) throw new ArgumentException("invalid-text-metric");
                advances[i] = advance;
                if (x + advance > width && i > first)
                { lines.Add(new NotesTextLine(first, i, boundaries[first], boundaries[i])); first = i; x = 0; }
                x += advance;
            }
            lines.Add(new NotesTextLine(first, advances.Length, boundaries[first], text.Length));
        }
        public string Text { get; }
        public IReadOnlyList<NotesTextLine> Lines { get { return lines; } }
        public string Element(int index) { return Text.Substring(boundaries[index], boundaries[index + 1] - boundaries[index]); }
        public float Advance(int index) { return advances[index]; }
        public int LineOf(int offset)
        {
            int low = 0, high = lines.Count - 1;
            while (low < high) { int mid = (low + high + 1) / 2; if (lines[mid].Start <= offset) low = mid; else high = mid - 1; }
            return low;
        }
        public float CaretX(int offset)
        {
            NotesTextLine line = lines[LineOf(offset)]; float x = 0;
            for (int i = line.First; i < line.Last && boundaries[i] < offset; i++) x += advances[i];
            return x;
        }
        public int Hit(float x, int lineIndex)
        {
            NotesTextLine line = lines[Math.Max(0, Math.Min(lines.Count - 1, lineIndex))]; float current = 0;
            for (int i = line.First; i < line.Last; i++)
            { if (x < current + advances[i] / 2) return boundaries[i]; current += advances[i]; }
            return line.End;
        }
        public int LineHome(int offset) { return lines[LineOf(offset)].Start; }
        public int LineEnd(int offset) { return lines[LineOf(offset)].End; }
        public int Vertical(int offset, int direction) { return Hit(CaretX(offset), LineOf(offset) + direction); }
    }
}
