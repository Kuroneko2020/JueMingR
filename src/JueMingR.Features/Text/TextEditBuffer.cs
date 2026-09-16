using System;
using System.Collections.Generic;

namespace JueMingR.Features.Text
{
    public class TextEditBuffer
    {
        private int[] boundaries;
        private readonly int maximumElements, maximumUnits;
        private readonly string elementLimitMessage;
        public TextEditBuffer(string text, bool singleLine, int maximumElements, int maximumUnits, string elementLimitMessage)
            : this(text, singleLine, TextElements.Boundaries(text), maximumElements, maximumUnits, elementLimitMessage) { }
        protected TextEditBuffer(string text, bool singleLine, IReadOnlyList<int> index, int maximumElements, int maximumUnits, string elementLimitMessage)
        {
            SingleLine = singleLine; Text = Baseline = text; this.maximumElements = maximumElements; this.maximumUnits = maximumUnits; this.elementLimitMessage = elementLimitMessage;
            boundaries = new int[index.Count]; for (int i = 0; i < index.Count; i++) boundaries[i] = index[i];
            Boundaries = Array.AsReadOnly(boundaries); Anchor = Caret = text.Length;
        }
        internal int[] RawBoundaries { get { return boundaries; } }
        public IReadOnlyList<int> Boundaries { get; private set; }
        public int LastChangeStart { get; private set; }
        public bool SingleLine { get; }
        public string Text { get; private set; }
        public string Baseline { get; private set; }
        public int Caret { get; private set; }
        public int Anchor { get; private set; }
        public int SelectionStart { get { return Math.Min(Anchor, Caret); } }
        public int SelectionEnd { get { return Math.Max(Anchor, Caret); } }
        public bool HasSelection { get { return Anchor != Caret; } }
        public string SelectedText { get { return Text.Substring(SelectionStart, SelectionEnd - SelectionStart); } }
        public long Revision { get; private set; }
        public long CaretRevision { get; private set; }
        public bool Dirty { get; private set; }
        public string Error { get; private set; }
        public bool Insert(string text)
        {
            if (String.IsNullOrEmpty(text)) return true;
            text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            if (!TextElements.IsValid(text)) { Error = "部分字符不完整，未添加到草稿。"; return false; }
            if (SingleLine && text.IndexOf('\n') >= 0) { Error = "此字段不能换行。"; return false; }
            int from = SelectionStart, to = SelectionEnd;
            if ((long)Text.Length - (to - from) + text.Length > maximumUnits) { Error = "内容过长，未添加到草稿。"; return false; }
            // The original selection remains intact until the entire candidate is
            // validated. IME previews never call this committed-text transaction.
            string next = Text.Remove(from, to - from).Insert(from, text);
            int[] nextBoundaries;
            try { nextBoundaries = Rebuild(next, from, to, text.Length - (to - from)); }
            catch (ArgumentException) { Error = "字符组合过长，未添加到草稿。"; return false; }
            if (SingleLine && nextBoundaries.Length - 1 > maximumElements)
            { Error = elementLimitMessage; return false; }
            Change(next, nextBoundaries, from + text.Length, from); return true;
        }
        public void MoveTo(int offset) { MoveTo(offset, false); }
        public void MoveTo(int offset, bool extend)
        {
            int index = Array.BinarySearch(boundaries, Math.Max(0, Math.Min(Text.Length, offset)));
            int next = boundaries[index < 0 ? Math.Max(0, ~index - 1) : index];
            int anchor = extend ? Anchor : next;
            if (next != Caret || anchor != Anchor) { Anchor = anchor; Caret = next; CaretRevision++; }
        }
        public void Move(int direction) { Move(direction, false); }
        public void Move(int direction, bool extend)
        {
            if (!extend && HasSelection) { MoveTo(direction < 0 ? SelectionStart : SelectionEnd); return; }
            int index = Array.BinarySearch(boundaries, Caret);
            MoveTo(boundaries[Math.Max(0, Math.Min(boundaries.Length - 1, index + direction))], extend);
        }
        public void SelectAll() { MoveTo(0); MoveTo(Text.Length, true); }
        public void DeleteSelection() { if (HasSelection) Remove(SelectionStart, SelectionEnd); }
        public void Backspace()
        {
            if (HasSelection) { DeleteSelection(); return; }
            int index = Array.BinarySearch(boundaries, Caret); if (index <= 0) return;
            Remove(boundaries[index - 1], Caret);
        }
        public void Delete()
        {
            if (HasSelection) { DeleteSelection(); return; }
            int index = Array.BinarySearch(boundaries, Caret); if (index == boundaries.Length - 1) return;
            Remove(Caret, boundaries[index + 1]);
        }
        public void AcceptBaseline(string submittedText) { Baseline = submittedText; Dirty = Text != Baseline; }
        protected void AcceptCanonical(string canonical, IReadOnlyList<int> index)
        {
            if (!SingleLine || Text == canonical) return;
            int leading = 0; while (leading < Text.Length && Char.IsWhiteSpace(Text[leading])) leading++;
            Text = Baseline = canonical; Dirty = false; LastChangeStart = 0; Revision++;
            boundaries = new int[index.Count]; for (int i = 0; i < index.Count; i++) boundaries[i] = index[i];
            Boundaries = Array.AsReadOnly(boundaries); MoveTo(Math.Max(0, Caret - leading)); CaretRevision++;
        }
        private void Remove(int from, int to)
        {
            string next = Text.Remove(from, to - from);
            try { Change(next, Rebuild(next, from, to, from - to), from, from); }
            catch (ArgumentException) { Error = "删除后会形成过长的字符组合，原文未改动。"; }
        }
        private int[] Rebuild(string next, int from, int to, int delta)
        {
            int first = Math.Max(0, Array.BinarySearch(boundaries, from) - 1);
            int last = Math.Min(boundaries.Length - 1, Array.BinarySearch(boundaries, to) + 2);
            // A regional-indicator insertion can change pairing through the entire
            // adjacent run. Extend to its end instead of assuming a fixed window.
            while (last < boundaries.Length - 1)
            {
                int scalar = Char.ConvertToUtf32(Text, boundaries[last]);
                if (scalar < 0x1f1e6 || scalar > 0x1f1ff) break;
                last++;
            }
            int start = boundaries[first], end = boundaries[last] + delta;
            int[] local = TextElements.Boundaries(next.Substring(start, end - start));
            var result = new int[first + local.Length + boundaries.Length - last - 1];
            Array.Copy(boundaries, result, first);
            for (int i = 0; i < local.Length; i++) result[first + i] = start + local[i];
            for (int i = last + 1; i < boundaries.Length; i++) result[first + local.Length + i - last - 1] = boundaries[i] + delta;
            return result;
        }
        private void Change(string text, int[] next, int caret, int changedStart)
        {
            Text = text; boundaries = next; Boundaries = Array.AsReadOnly(next); Revision++; Error = null; Dirty = Text != Baseline; LastChangeStart = changedStart;
            // Inserting a mark/ZWJ can merge across the insertion boundary. Place
            // the caret at the next complete boundary, never inside that cluster.
            int index = Array.BinarySearch(next, caret); Anchor = Caret = next[index < 0 ? ~index : index]; CaretRevision++;
        }
    }
}
