using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingR.Platform.Persistence;

namespace JueMingR.Features.Notes
{
    public sealed class NotesFeature
    {
        private readonly DocumentWorker<Notebook> worker;
        private long sequence;
        private readonly object gate = new object();
        private readonly Dictionary<string, ReadingIntent> reading = new Dictionary<string, ReadingIntent>();
        private ReadingIntent[] submittedReading;
        private long readingCommand;
        private bool readingFailed, stopping;
        public NotesFeature(DocumentWorker<Notebook> worker) { this.worker = worker; }
        public Notebook Saved { get; private set; } = Notebook.Empty;
        public bool Loaded { get; private set; }
        public bool Readable { get; private set; }
        public bool Busy { get; private set; }
        public string Error { get; private set; }
        public bool NeedsRecovery { get; private set; }
        public long Revision { get; private set; }
        public long ReadingRevision { get; private set; }
        public string ReadingError { get; private set; }
        public bool HasPendingReading { get { lock (gate) return reading.Count != 0; } }
        public DocumentResult<Notebook> Poll()
        {
            lock (gate)
            {
                if (stopping) return null;
                DocumentResult<Notebook> result;
                if (!worker.TryTake(out result)) return null;
                if (result.CommandId == 0) { Loaded = true; Readable = result.Success; }
                else Busy = false;
                if (result.Success) { Saved = result.Value; Revision++; Error = null; }
                else { Error = result.Error ?? "notes-operation-failed"; NeedsRecovery |= result.CommitUnconfirmed; }
                if (result.CommandId != 0 && result.CommandId == readingCommand)
                {
                    if (result.Success)
                    {
                        foreach (ReadingIntent intent in submittedReading)
                            if (reading.TryGetValue(intent.Id, out ReadingIntent current) && ReferenceEquals(current, intent)) reading.Remove(intent.Id);
                        ReadingError = null;
                    }
                    else
                    { readingFailed = true; ReadingError = "阅读调整尚未可靠保存；当前预览可能无法在重启后恢复。请在 F5 笔记页查看原因。"; }
                    readingCommand = 0; submittedReading = null; ReadingRevision++;
                    PruneReading();
                    // Workspace owns ordinary command acknowledgments. This completion
                    // only acknowledges the exact immutable reading batch captured here.
                    return null;
                }
                if (result.Success) PruneReading();
                return result;
            }
        }
        public bool TrySubmit(Notebook next, out long commandId)
        {
            lock (gate)
            {
                commandId = 0;
                if (stopping || !Readable || Busy || NeedsRecovery) return false;
                long id = ++sequence;
                if (!worker.TrySubmit(id, next)) return false;
                Busy = true; commandId = id; return true;
            }
        }

        public NoteReading ReadingFor(string id)
        { lock (gate) return ReadingForNote(Saved.Find(id)); }

        // Presentation already traverses the current immutable Saved notebook.
        // Reuse that note so every pin does not scan the entire notebook again.
        public NoteReading ReadingFor(Note note)
        { lock (gate) return ReadingForNote(note); }
        private NoteReading ReadingForNote(Note note)
        {
                return note != null && reading.TryGetValue(note.Id, out ReadingIntent intent) && ReferenceEquals(intent.Lifetime, note.PinIdentity)
                    ? intent.Value : note == null ? NoteReading.Default : note.Reading;
        }
        public bool AdjustReading(string id, bool area, int steps)
        {
            lock (gate)
            {
                Note note = Saved.Find(id);
                if (stopping || !Readable || NeedsRecovery || note == null || !note.Pinned || steps == 0) return false;
                NoteReading current = ReadingForNote(note), next = current.Adjust(area, steps);
                if (next.Same(current)) return false;
                long now = Now(); reading.TryGetValue(id, out ReadingIntent prior);
                // At most one final intent per existing note. No notebook cloning,
                // encoding or file access occurs for individual wheel notches.
                reading[id] = new ReadingIntent(id, note.PinIdentity, next,
                    prior == null || prior.Submitted || readingFailed ? now : prior.First, now);
                readingFailed = false; ReadingRevision++; return true;
            }
        }
        public void FlushReading()
        {
            lock (gate)
            {
                if (stopping || Busy || !Readable || NeedsRecovery || readingFailed || reading.Count == 0) return;
                long now = Now(); bool due = false;
                foreach (ReadingIntent intent in reading.Values) due |= now - intent.Last >= 250 || now - intent.First >= 1000;
                if (!due) return;
                ReadingIntent[] batch = SnapshotReading(); Notebook next = ApplyReading(Saved, batch);
                if (ReferenceEquals(next, Saved)) { reading.Clear(); ReadingError = null; ReadingRevision++; return; }
                if (TrySubmit(next, out long id))
                {
                    readingCommand = id; submittedReading = batch;
                    // A later wheel event starts a new bounded batch. Carrying the
                    // old First past this boundary would turn continuous scrolling
                    // into one whole-document write per completion after one second.
                    foreach (ReadingIntent intent in batch) intent.Submitted = true;
                }
                else { readingFailed = true; ReadingError = "阅读调整尚未可靠保存；存储受保护，请退出后检查恢复材料。"; }
            }
        }
        public bool Stop(int milliseconds)
        {
            ReadingIntent[] final;
            lock (gate) { stopping = true; final = SnapshotReading(); }
            // Freeze only accepted reading intentions, never Editor/Workspace or
            // game objects. The worker applies them to its LAST successful content
            // after any in-flight command, within the same total exit budget.
            return worker.Stop(milliseconds, book => ApplyReading(book, final));
        }
        private ReadingIntent[] SnapshotReading()
        { var values = new ReadingIntent[reading.Count]; reading.Values.CopyTo(values, 0); return values; }
        private void PruneReading()
        {
            if (reading.Count == 0) return;
            var obsolete = new List<string>();
            foreach (ReadingIntent intent in reading.Values)
            {
                Note note = Saved.Find(intent.Id);
                if (note == null || !note.Pinned || !ReferenceEquals(note.PinIdentity, intent.Lifetime)) obsolete.Add(intent.Id);
            }
            foreach (string id in obsolete) reading.Remove(id);
            if (reading.Count == 0) { ReadingError = null; readingFailed = false; }
            if (obsolete.Count != 0) ReadingRevision++;
        }
        private static Notebook ApplyReading(Notebook source, ReadingIntent[] batch)
        {
            if (batch.Length == 0) return source;
            var byId = new Dictionary<string, ReadingIntent>(); foreach (ReadingIntent intent in batch) byId.Add(intent.Id, intent);
            var notes = new List<Note>(source.Notes.Count); bool changed = false;
            foreach (Note note in source.Notes)
            {
                Note next = note;
                if (note.Pinned && byId.TryGetValue(note.Id, out ReadingIntent intent) && ReferenceEquals(note.PinIdentity, intent.Lifetime) && !note.Reading.Same(intent.Value))
                { next = note.WithReading(intent.Value); changed = true; }
                notes.Add(next);
            }
            return changed ? new Notebook(notes) : source;
        }
        private static long Now() { return Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000); }
        private sealed class ReadingIntent
        {
            internal readonly string Id;
            internal readonly object Lifetime;
            internal readonly NoteReading Value;
            internal readonly long First, Last;
            internal bool Submitted; // Scheduling marker; guarded by gate, payload stays immutable.
            internal ReadingIntent(string id, object lifetime, NoteReading value, long first, long last)
            { Id = id; Lifetime = lifetime; Value = value; First = first; Last = last; }
        }
    }
}
