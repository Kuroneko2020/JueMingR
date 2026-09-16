namespace JueMingR.Features.Notes
{
    // Notes keeps its accepted title/body contract; the editor mechanics now
    // serve a second real consumer without making that consumer a fake note.
    public sealed class NoteEditor : Text.TextEditBuffer
    {
        public NoteEditor(bool title, string text)
            : base(text, title, Note.MaximumTitleElements, Note.MaximumBodyUnits, "标题最多 80 字。") { }
        internal NoteEditor(bool title, Note note)
            : base(title ? note.Title : note.Body, title, note.Index(title), Note.MaximumTitleElements, Note.MaximumBodyUnits, "标题最多 80 字。") { }
        public bool IsTitle { get { return SingleLine; } }
        internal void AcceptCanonicalTitle(Note note) { AcceptCanonical(note.Title, note.TitleBoundaries); }
    }
}