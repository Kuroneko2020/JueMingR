using System;
using System.IO;
using JueMingR.Features.Notes;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Persistence;

namespace JueMingR.TerrariaHost.Notes
{
    // Same verified installation root as preferences, separate consistency unit
    // and lifetime from both settings and the current world's biome runtime.
    internal sealed class HostNotes
    {
        private readonly DocumentWorker<Notebook> worker;
        internal readonly NotesWorkspace Workspace;
        internal HostNotes(string verifiedGameDirectory)
        {
            var codec = new NotebookCodec();
            var storage = new AtomicFileDocument(Path.Combine(verifiedGameDirectory,
                "JueMingRData", "notes", "notes.json"), Notebook.MaximumBytes, true, ".schema1-original");
            worker = new DocumentWorker<Notebook>(storage, bytes =>
            {
                Notebook book = codec.Decode(bytes);
                if (book.SourceSchema == 1) storage.RetainLoadedSource();
                return book;
            }, codec.Encode, Notebook.Empty);
            Workspace = new NotesWorkspace(new NotesFeature(worker));
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        internal void Update() { Workspace.Poll(); }
        private void OnExit(object sender, EventArgs args)
        {
            AppDomain.CurrentDomain.ProcessExit -= OnExit;
            // Stop drains within the budget; timeout cancels before file commit,
            // while any native I/O already entered retains ownership until it ends.
            Workspace.Feature.Stop(750);
        }
    }
}
