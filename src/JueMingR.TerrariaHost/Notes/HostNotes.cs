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
            worker = new DocumentWorker<Notebook>(new AtomicFileDocument(Path.Combine(verifiedGameDirectory,
                "JueMingRData", "notes", "notes.json"), Notebook.MaximumBytes, true), codec.Decode, codec.Encode, Notebook.Empty);
            Workspace = new NotesWorkspace(new NotesFeature(worker));
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        internal void Update() { Workspace.Poll(); }
        private void OnExit(object sender, EventArgs args)
        {
            AppDomain.CurrentDomain.ProcessExit -= OnExit;
            // Stop drains within the budget; timeout cancels before file commit,
            // while any native I/O already entered retains ownership until it ends.
            worker.Stop(750);
        }
    }
}
