using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JueMingR.Features.Notes
{
    // This immutable snapshot is the sole committed content shared by cards and pins.
    // Edits share unchanged strings; no periodic UI-to-UI synchronization exists.
    public sealed class Notebook
    {
        public const int MaximumNotes = 1024;
        public const int LegacyMaximumBytes = 16 * 1024 * 1024;
        // Enough explicit metadata headroom to upgrade every legal schema 1
        // notebook without trimming content at its previous byte limit.
        public const int MaximumBytes = LegacyMaximumBytes + 64 * 1024;
        public static readonly Notebook Empty = new Notebook(new Note[0]);
        private readonly ReadOnlyCollection<Note> notes;
        public Notebook(IEnumerable<Note> source) : this(source, 2) { }
        internal Notebook(IEnumerable<Note> source, int sourceSchema)
        {
            var values = new List<Note>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Note note in source)
            {
                if (note == null || !ids.Add(note.Id) || values.Count == MaximumNotes) throw new ArgumentException("invalid-note-list");
                values.Add(note);
            }
            notes = values.AsReadOnly(); SourceSchema = sourceSchema;
        }
        public int SourceSchema { get; }
        public IReadOnlyList<Note> Notes { get { return notes; } }
        public Note Find(string id) { foreach (Note note in notes) if (note.Id == id) return note; return null; }
        public Notebook Add(Note note)
        { var next = new List<Note>(notes); next.Add(note); return new Notebook(next); }
        public Notebook Replace(Note note)
        {
            var next = new List<Note>(notes);
            for (int i = 0; i < next.Count; i++) if (next[i].Id == note.Id) { next[i] = note; return new Notebook(next); }
            throw new ArgumentException("note-no-longer-exists");
        }
        public Notebook Remove(string id)
        {
            var next = new List<Note>(notes);
            if (next.RemoveAll(n => n.Id == id) != 1) throw new ArgumentException("note-no-longer-exists");
            return new Notebook(next);
        }
    }
}
