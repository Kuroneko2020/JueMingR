using JueMingR.Platform.Persistence;

namespace JueMingR.Features.Notes
{
    public sealed class NotesFeature
    {
        private readonly DocumentWorker<Notebook> worker;
        private long sequence;
        public NotesFeature(DocumentWorker<Notebook> worker) { this.worker = worker; }
        public Notebook Saved { get; private set; } = Notebook.Empty;
        public bool Loaded { get; private set; }
        public bool Readable { get; private set; }
        public bool Busy { get; private set; }
        public string Error { get; private set; }
        public bool NeedsRecovery { get; private set; }
        public long Revision { get; private set; }
        public DocumentResult<Notebook> Poll()
        {
            DocumentResult<Notebook> result;
            if (!worker.TryTake(out result)) return null;
            if (result.CommandId == 0) { Loaded = true; Readable = result.Success; }
            else Busy = false;
            if (result.Success) { Saved = result.Value; Revision++; Error = null; }
            else { Error = result.Error ?? "notes-operation-failed"; NeedsRecovery |= result.CommitUnconfirmed; }
            return result;
        }
        public bool TrySubmit(Notebook next, out long commandId)
        {
            commandId = 0;
            if (!Readable || Busy || NeedsRecovery) return false;
            long id = ++sequence;
            if (!worker.TrySubmit(id, next)) return false;
            Busy = true; commandId = id; return true;
        }
    }
}
