using JueMingR.Features.Notes;
using JueMingR.Features.Text;
using JueMingR.TerrariaHost.Input;
using Terraria;

namespace JueMingR.TerrariaHost.Notes
{
    internal interface INotesIme
    {
        string Composition { get; }
        bool Candidates { get; }
        void Toggle(bool enabled);
    }
    internal sealed class NotesInput : TextEditInput
    {
        internal NotesInput(NotesWorkspace workspace, INotesClipboard clipboard, INotesIme ime = null)
            : base(new NotesEditSession(workspace), clipboard, ime) { }
        internal void AfterSample(bool active, NotesTextLayout layout)
        { AfterSample(active, layout, Main.keyState, true); }
        private sealed class NotesEditSession : ITextEditSession
        {
            private readonly NotesWorkspace workspace;
            internal NotesEditSession(NotesWorkspace workspace) { this.workspace = workspace; }
            public TextEditBuffer Editor { get { return workspace.Editor; } }
            public bool RequestFinish() { return workspace.Request(new NotesAction(NotesActionKind.FinishEdit)); }
            public void CancelEdit() { workspace.CancelEdit(); }
            public void PreserveUncommittedInput(TextEditBuffer editor) { workspace.PreserveUncommittedInput(editor as NoteEditor); }
        }
    }
}