using System;

namespace JueMingR.Features.Notes
{
    public sealed class NoteEditor
    {
        private int[] boundaries;
        public NoteEditor(bool title, string text)
        { IsTitle = title; Text = Baseline = text; boundaries = TextElements.Boundaries(text); Caret = text.Length; }
        public bool IsTitle { get; }
        public string Text { get; private set; }
        public string Baseline { get; private set; }
        public int Caret { get; private set; }
        public long Revision { get; private set; }
        public long CaretRevision { get; private set; }
        public bool Dirty { get { return Text != Baseline; } }
        public string Error { get; private set; }
        public bool Insert(string text)
        {
            if (String.IsNullOrEmpty(text)) return true;
            text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            if (!TextElements.IsValid(text)) { Error = "输入含不完整字符，未更改草稿。"; return false; }
            if (IsTitle && text.IndexOf('\n') >= 0) { Error = "标题不能包含换行，未更改草稿。"; return false; }
            if ((long)Text.Length + text.Length > Note.MaximumBodyUnits) { Error = "正文达到 1,048,576 UTF-16 单位上限；本次输入未加入。"; return false; }
            string next = Text.Insert(Caret, text);
            int[] nextBoundaries = TextElements.Boundaries(next);
            if (IsTitle && nextBoundaries.Length - 1 > Note.MaximumTitleElements)
            { Error = "标题最多 80 个完整字符；本次输入未加入，原文保留。"; return false; }
            Change(next, nextBoundaries, Caret + text.Length); return true;
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
        public void ClearAfterCopy() { if (Text.Length != 0) Change("", new[] { 0 }, 0); }
        public void AcceptBaseline(string submittedText) { Baseline = submittedText; }
        private void Remove(int from, int to)
        { string next = Text.Remove(from, to - from); Change(next, TextElements.Boundaries(next), from); }
        private void Change(string text, int[] next, int caret)
        {
            Text = text; boundaries = next; Revision++; Error = null;
            // Inserting a mark/ZWJ can merge across the insertion boundary. Place
            // the caret at the next complete boundary, never inside that cluster.
            int index = Array.BinarySearch(next, caret); Caret = next[index < 0 ? ~index : index]; CaretRevision++;
        }
    }
}
