using System;

namespace JueMingR.Features.Notes
{
    public enum NotesActionKind { Save, FinishEdit, BeginEdit, Create, ConfirmDelete, Delete, Pin, Unpin, Opacity, Position, Leave }
    public sealed class NotesAction
    {
        public NotesAction(NotesActionKind kind, string id = null, bool title = false, int x = 0, int y = 0)
        { Kind = kind; Id = id; Title = title; X = x; Y = y; }
        public NotesActionKind Kind { get; }
        public string Id { get; }
        public bool Title { get; }
        public int X { get; }
        public int Y { get; }
    }

    // Game-thread presentation state. A persisted completion and its dependent UI
    // action have different lifetimes: hidden UI still accepts the former, while
    // epoch/revision checks prevent the latter from reviving after interruption.
    public sealed class NotesWorkspace
    {
        private long epoch, pendingEpoch, pendingRevision, pendingId;
        private NoteEditor submittedEditor;
        private string submittedText;
        private string pendingDeleteId;
        private NotesAction afterSave;
        private NotesAction navigation;
        private bool pendingUncommittedInput;
        public NotesWorkspace(NotesFeature feature) { Feature = feature; }
        public NotesFeature Feature { get; }
        public NoteEditor Editor { get; private set; }
        public string EditingId { get; private set; }
        public string DeleteConfirmation { get; private set; }
        public string Error { get; private set; }
        public void Poll()
        {
            var result = Feature.Poll();
            if (result == null || result.CommandId == 0) { Feature.FlushReading(); return; }
            if (result.CommandId != pendingId) throw new InvalidOperationException("Unexpected notes completion identity.");
            if (!result.Success)
            {
                Error = result.CommitUnconfirmed ? "磁盘提交结果未确认，已停止写入。当前显示最后可信内容；请退出并保留 notes.json、.bak、.tmp 检查恢复。"
                    : "保存失败，草稿保留；后续动作未执行。";
                ClearPending(); return;
            }
            Error = null;
            // The completion belongs to this command and target, not to whichever
            // confirmation happens to be visible now. A deleted target cannot
            // retain a legal confirmation; a different target remains untouched.
            if (pendingDeleteId != null && DeleteConfirmation == pendingDeleteId && Feature.Saved.Find(pendingDeleteId) == null)
                DeleteConfirmation = null;
            NotesAction action = afterSave;
            bool current = pendingEpoch == epoch && (submittedEditor == null || ReferenceEquals(Editor, submittedEditor) && Editor.Revision == pendingRevision);
            if (ReferenceEquals(Editor, submittedEditor) && Editor != null)
            {
                Editor.AcceptBaseline(submittedText);
                // Title normalization belongs to the acknowledged revision only;
                // a newer draft or live IME replacement range must not be replaced
                // by the saved fallback/trim, even before its text revision changes.
                if (Editor.Revision == pendingRevision && !pendingUncommittedInput) Editor.AcceptCanonicalTitle(Feature.Saved.Find(EditingId));
            }
            ClearPending();
            if (current && action != null)
            {
                if (action.Kind != NotesActionKind.Save) EndEdit();
                Execute(action);
            }
            // Dependent user actions retain first refusal on the single worker.
            Feature.FlushReading();
        }
        public bool Request(NotesAction action)
        {
            if (!Feature.Readable) { Error = "笔记未可靠读取，原件保持保护。"; return false; }
            if (Feature.Busy) { Error = "上一项保存尚未完成，本次动作未执行。"; return false; }
            Error = null;
            if (Editor != null && Editor.Dirty)
            {
                Note note = Feature.Saved.Find(EditingId);
                if (note == null) { Error = "编辑对象已不存在，草稿保留。"; return false; }
                if (!Submit(Feature.Saved.Replace(note.WithEditor(Editor)))) return false;
                submittedEditor = Editor; submittedText = Editor.Text; pendingRevision = Editor.Revision; afterSave = action;
                return true;
            }
            if (action.Kind != NotesActionKind.Save) EndEdit();
            return Execute(action);
        }
        public void RequestDelete(string id)
        { Request(new NotesAction(DeleteConfirmation == id ? NotesActionKind.Delete : NotesActionKind.ConfirmDelete, id)); }
        public void CancelDelete() { DeleteConfirmation = null; }
        public void PreserveUncommittedInput(NoteEditor editor)
        {
            // IME previews and split surrogate pairs are not text revisions, but
            // still belong to this editor. Invalidate only the dependent UI action;
            // the accepted write and its acknowledged baseline must complete normally.
            if (editor != null && ReferenceEquals(Editor, editor) && ReferenceEquals(submittedEditor, editor))
            { afterSave = null; pendingUncommittedInput = true; }
        }
        public void CancelEdit() { epoch++; EndEdit(); }
        public void Suspend()
        { epoch++; DeleteConfirmation = null; navigation = null; }
        public NotesAction TakeNavigation()
        { NotesAction value = navigation; navigation = null; return value; }
        private void EndEdit() { Editor = null; EditingId = null; }
        private bool Execute(NotesAction action)
        {
            Note note = action.Id == null ? null : Feature.Saved.Find(action.Id);
            switch (action.Kind)
            {
                case NotesActionKind.Save:
                case NotesActionKind.FinishEdit: return true;
                case NotesActionKind.BeginEdit:
                    if (note == null) return false;
                    EditingId = note.Id; Editor = new NoteEditor(action.Title, note); Editor.MoveTo(action.X); return true;
                case NotesActionKind.Leave:
                    DeleteConfirmation = null; epoch++; navigation = action; return true;
                case NotesActionKind.ConfirmDelete:
                    if (note == null) return false;
                    DeleteConfirmation = note.Id; return true;
                case NotesActionKind.Create:
                    if (Feature.Saved.Notes.Count >= Notebook.MaximumNotes) { Error = "已达到 1,024 篇上限；现有内容保留。"; return false; }
                    DeleteConfirmation = null; return Submit(Feature.Saved.Add(Note.Create()));
                case NotesActionKind.Delete:
                    if (note == null || DeleteConfirmation != note.Id) return false;
                    if (!Submit(Feature.Saved.Remove(note.Id))) return false;
                    pendingDeleteId = note.Id; return true;
            }
            if (note == null) return false;
            Note next;
            switch (action.Kind)
            {
                case NotesActionKind.Pin: if (note.Pinned) return true; next = note.Pin(action.X, action.Y); break;
                case NotesActionKind.Unpin: if (!note.Pinned) return true; next = note.Unpin(); break;
                case NotesActionKind.Opacity:
                    int opacity = Math.Max(0, Math.Min(100, action.X)); if (note.Opacity == opacity) return true;
                    next = note.WithOpacity(opacity); break;
                case NotesActionKind.Position: if (note.X == action.X && note.Y == action.Y) return true; next = note.WithPosition(action.X, action.Y); break;
                default: return false;
            }
            return Submit(Feature.Saved.Replace(next));
        }
        private bool Submit(Notebook next)
        {
            if (!Feature.TrySubmit(next, out pendingId)) { Error = "笔记暂不能写入，草稿保留。"; return false; }
            pendingEpoch = epoch; return true;
        }
        private void ClearPending() { pendingId = 0; submittedEditor = null; submittedText = null; afterSave = null; pendingDeleteId = null; pendingUncommittedInput = false; }
    }
}
