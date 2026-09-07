using System;
using System.Collections.Generic;

namespace JueMingR.Features.Notes
{
    public sealed class NoteEditor
    {
        private int[] boundaries;
        public NoteEditor(bool title, string text)
        { IsTitle = title; Text = Baseline = text; boundaries = TextElements.Boundaries(text); Boundaries = Array.AsReadOnly(boundaries); Caret = text.Length; }
        internal NoteEditor(bool title, Note note)
        {
            IsTitle = title; Text = Baseline = title ? note.Title : note.Body;
            var index = note.Index(title); boundaries = new int[index.Count]; for (int i = 0; i < index.Count; i++) boundaries[i] = index[i];
            Boundaries = Array.AsReadOnly(boundaries); Caret = Text.Length;
        }
        internal int[] RawBoundaries { get { return boundaries; } }
        public IReadOnlyList<int> Boundaries { get; private set; }
        public int LastChangeStart { get; private set; }
        public bool IsTitle { get; }
        public string Text { get; private set; }
        public string Baseline { get; private set; }
        public int Caret { get; private set; }
        public long Revision { get; private set; }
        public long CaretRevision { get; private set; }
        public bool Dirty { get; private set; }
        public string Error { get; private set; }
        public bool Insert(string text)
        {
            if (String.IsNullOrEmpty(text)) return true;
            text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            if (!TextElements.IsValid(text)) { Error = "输入含不完整字符，未更改草稿。"; return false; }
            if (IsTitle && text.IndexOf('\n') >= 0) { Error = "标题不能包含换行，未更改草稿。"; return false; }
            if ((long)Text.Length + text.Length > Note.MaximumBodyUnits) { Error = "正文达到 1,048,576 UTF-16 单位上限；本次输入未加入。"; return false; }
            string next = Text.Insert(Caret, text);
            int[] nextBoundaries;
            try { nextBoundaries = Rebuild(next, Caret, Caret, text.Length); }
            catch (ArgumentException) { Error = "一个组合字符超过 1,024 UTF-16 单位；本次输入未加入，原文保留。"; return false; }
            if (IsTitle && nextBoundaries.Length - 1 > Note.MaximumTitleElements)
            { Error = "标题最多 80 个完整字符；本次输入未加入，原文保留。"; return false; }
            Change(next, nextBoundaries, Caret + text.Length, Caret); return true;
        }
        public void MoveTo(int offset)
        {
            int index = Array.BinarySearch(boundaries, Math.Max(0, Math.Min(Text.Length, offset)));
            int next = boundaries[index < 0 ? Math.Max(0, ~index - 1) : index];
            if (next != Caret) { Caret = next; CaretRevision++; }
        }
        public void Move(int direction)
        { int index = Array.BinarySearch(boundaries, Caret); MoveTo(boundaries[Math.Max(0, Math.Min(boundaries.Length - 1, index + direction))]); }
        public void Backspace()
        {
            int index = Array.BinarySearch(boundaries, Caret); if (index <= 0) return;
            Remove(boundaries[index - 1], Caret);
        }
        public void Delete()
        {
            int index = Array.BinarySearch(boundaries, Caret); if (index == boundaries.Length - 1) return;
            Remove(Caret, boundaries[index + 1]);
        }
        public void ClearAfterCopy() { if (Text.Length != 0) Change("", new[] { 0 }, 0, 0); }
        public void AcceptBaseline(string submittedText) { Baseline = submittedText; Dirty = Text != Baseline; }
        internal void AcceptCanonicalTitle(Note note)
        {
            if (!IsTitle || Text == note.Title) return;
            int leading = 0; while (leading < Text.Length && Char.IsWhiteSpace(Text[leading])) leading++;
            Text = Baseline = note.Title; Dirty = false; LastChangeStart = 0; Revision++;
            var index = note.TitleBoundaries; boundaries = new int[index.Count]; for (int i = 0; i < index.Count; i++) boundaries[i] = index[i];
            Boundaries = Array.AsReadOnly(boundaries); MoveTo(Math.Max(0, Caret - leading)); CaretRevision++;
        }
        private void Remove(int from, int to)
        {
            string next = Text.Remove(from, to - from);
            try { Change(next, Rebuild(next, from, to, from - to), from, from); }
            catch (ArgumentException) { Error = "删除会合并出超限组合字符，原文保留。"; }
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
            int index = Array.BinarySearch(next, caret); Caret = next[index < 0 ? ~index : index]; CaretRevision++;
        }
    }
}
