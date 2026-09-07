using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using JueMingR.Features.Notes;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class NotesStorageChecks
    {
        internal static DocumentWorker<Notebook> Open(string path)
        {
            var codec = new NotebookCodec();
            return new DocumentWorker<Notebook>(new AtomicFileDocument(path, Notebook.MaximumBytes, true), codec.Decode, codec.Encode, Notebook.Empty);
        }
        internal static DocumentResult<Notebook> Take(DocumentWorker<Notebook> worker)
        {
            DocumentResult<Notebook> result = null;
            if (!SpinWait.SpinUntil(() => worker.TryTake(out result), 5000)) throw new InvalidOperationException("worker result deadline");
            return result;
        }
        internal static void Check(IList<string> failures)
        {
            NotesDomainChecks.Run(failures, "notes real files commit, backup and reload", () => WithRoot(root =>
            {
                string path = Path.Combine(root, "notes.json"); Note note = Note.Create().WithText(false, new string('中', 50000));
                using (var worker = Open(path))
                {
                    NotesDomainChecks.Require(Take(worker).Success && !File.Exists(path), "missing doesn't write defaults");
                    NotesDomainChecks.Require(worker.TrySubmit(1, Notebook.Empty.Add(note)), "accept create");
                    NotesDomainChecks.Require(!worker.TrySubmit(2, Notebook.Empty), "no command overwrite queue");
                    NotesDomainChecks.Require(Take(worker).Success, "full content committed");
                    NotesDomainChecks.Require(worker.TrySubmit(2, Notebook.Empty.Add(note.WithText(true, "重命名"))), "accept rename");
                    NotesDomainChecks.Require(Take(worker).Success && File.Exists(path + ".bak"), "backup actual previous document");
                }
                using (var worker = Open(path))
                {
                    var read = Take(worker);
                    NotesDomainChecks.Require(read.Success && read.Value.Notes[0].Body == note.Body && read.Value.Notes[0].Title == "重命名", "new owner readback");
                    byte[] external = Encoding.UTF8.GetBytes("{\"schema\":1,\"notes\":[]}"); File.WriteAllBytes(path, external);
                    worker.TrySubmit(3, Notebook.Empty.Add(note));
                    NotesDomainChecks.Require(!Take(worker).Success && File.ReadAllText(path) == Encoding.UTF8.GetString(external), "external change retained");
                }
            }));
            NotesDomainChecks.Run(failures, "notes missing with recovery and unknown source protection", () => WithRoot(root =>
            {
                string path = Path.Combine(root, "notes.json"); File.WriteAllText(path + ".bak", "only surviving user bytes");
                using (var worker = Open(path))
                { NotesDomainChecks.Require(!Take(worker).Success && !worker.TrySubmit(1, Notebook.Empty), "missing plus recovery refuses empty overwrite"); }
                NotesDomainChecks.Require(!File.Exists(path) && File.ReadAllText(path + ".bak") == "only surviving user bytes", "recovery original untouched");
                File.WriteAllText(path, "{\"schema\":7,\"notes\":[]}");
                using (var worker = Open(path))
                { NotesDomainChecks.Require(!Take(worker).Success && !worker.TrySubmit(1, Notebook.Empty), "future schema protected"); }
                NotesDomainChecks.Require(File.ReadAllText(path) == "{\"schema\":7,\"notes\":[]}", "unknown version not downgraded");
            }));
        }
        internal static void WithRoot(Action<string> action)
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-notes-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            // Every using above waits for actual worker termination before this cleanup.
            // A stop timeout throws before cleanup, preserving the root still owned by I/O.
            action(root); Directory.Delete(root, true);
        }
    }
}
