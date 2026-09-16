using System;
using JueMingR.Features.Text;

namespace JueMingR.Features.MapMarkers
{
    // Only acknowledged writes may release an edit and execute its dependent
    // navigation. A later composition can cancel navigation without undoing I/O.
    public sealed class MarkerWorkspace
    {
        private readonly MarkerLibrary library;
        private Action afterSave;
        private TextEditBuffer submitted;
        private string submittedText;
        private long operation, revision, generation;
        private bool uncommitted;
        public MarkerWorkspace(MarkerLibrary library) { this.library = library; }
        public TextEditBuffer Editor { get; private set; }
        public string EditingId { get; private set; }
        public string DeleteConfirmation { get; private set; }
        public string Error { get; private set; }
        public bool BeginEdit(string id)
        {
            if (!library.CanEdit || Editor != null) return false;
            var record = Find(id); if (record == null) return false;
            generation = library.Generation; EditingId = id;
            Editor = new TextEditBuffer(record.Name, true, MarkerName.MaximumElements, MarkerName.MaximumElements * VisibleTextBoundary.MaximumElementUnits, MarkerName.LimitMessage, MarkerName.IsValid);
            Editor.SelectAll(); DeleteConfirmation = null; Error = null; return true;
        }
        public MarkerRecord Find(string id)
        { if (library.Saved != null) foreach (var record in library.Saved.Records) if (record.Id == id) return record; return null; }
        public bool Request(Action navigation)
        {
            if (operation != 0 || library.Busy) { Error = "正在保存，请稍候。"; return false; }
            if (Editor != null && Editor.Dirty)
            {
                string name;
                try { name = MarkerName.Normalize(Editor.Text, DateTime.Now); } catch (ArgumentException) { Error = MarkerName.LimitMessage; return false; }
                var record = Find(EditingId); if (record == null) { Error = "标记已不可用，草稿已保留。"; return false; }
                if (name != record.Name)
                {
                    operation = library.Rename(EditingId, name);
                    if (operation == 0) { Error = "暂时无法保存，草稿已保留。"; return false; }
                    submitted = Editor; submittedText = name; revision = Editor.Revision; afterSave = navigation; uncommitted = false; return true;
                }
            }
            CancelEdit(); navigation?.Invoke(); return true;
        }
        public void PreserveUncommittedInput(TextEditBuffer editor)
        { if (editor != null && ReferenceEquals(editor, submitted)) { uncommitted = true; afterSave = null; } }
        public void Suspend() { afterSave = null; DeleteConfirmation = null; }
        public void CancelEdit() { afterSave = null; Editor = null; EditingId = null; Error = null; }
        public void ConfirmDelete(string id)
        { if (Find(id) != null) DeleteConfirmation = id; }
        public bool Delete(string id)
        {
            if (DeleteConfirmation != id || Editor != null || operation != 0 || library.Busy) return false;
            long accepted = library.Delete(id); if (accepted == 0) return false; operation = accepted; generation = library.Generation; DeleteConfirmation = null; return true;
        }
        public void CancelDelete() { DeleteConfirmation = null; }
        public void Poll()
        {
            if (generation != library.Generation) { Suspend(); CancelEdit(); operation = 0; submitted = null; generation = library.Generation; }
            if (operation == 0 || library.LastOperation != operation) return;
            operation = 0;
            if (!library.LastSuccess) { Error = library.CommitUnconfirmed ? "保存结果未确认，已停止保存；草稿保留。" : "保存失败，草稿已保留。"; afterSave = null; submitted = null; return; }
            bool unchanged = !uncommitted && ReferenceEquals(Editor, submitted) && (Editor == null || Editor.Revision == revision);
            if (Editor != null && ReferenceEquals(Editor, submitted)) Editor.AcceptBaseline(submittedText);
            var next = afterSave; afterSave = null; submitted = null; Error = null;
            if (unchanged) { CancelEdit(); next?.Invoke(); }
        }
    }
}
