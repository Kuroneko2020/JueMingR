using System;
using System.Collections.Generic;
using System.Threading;
using JueMingR.Features.Notes;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class NotesConcurrencyChecks
    {
        internal static void Check(IList<string> failures)
        {
            NotesDomainChecks.Run(failures, "notes late save, failure and interrupted UI actions", () =>
            {
                Note original = Note.Create().WithText(false, "original");
                var storage = new GateStorage(Notebook.Empty.Add(original)); var codec = new NotebookCodec();
                using (var worker = new DocumentWorker<Notebook>(storage, codec.Decode, codec.Encode, Notebook.Empty))
                {
                    var workspace = new NotesWorkspace(new NotesFeature(worker));
                    Until(() => { workspace.Poll(); return workspace.Feature.Loaded; });
                    workspace.Request(new NotesAction(NotesActionKind.BeginEdit, original.Id, false, 8));
                    workspace.Editor.Insert(" one");
                    workspace.Request(new NotesAction(NotesActionKind.Leave, x: -1));
                    Until(() => storage.Entered.IsSet);
                    NotesDomainChecks.Require(workspace.Feature.Saved.Find(original.Id).Body == "original", "accepted is not committed");
                    workspace.Editor.Insert(" two"); workspace.Suspend(); storage.Release.Set();
                    Until(() => { workspace.Poll(); return !workspace.Feature.Busy; });
                    NotesDomainChecks.Require(workspace.Feature.Saved.Find(original.Id).Body == "original one", "hidden UI still takes real completion");
                    NotesDomainChecks.Require(workspace.Editor.Text == "original one two" && workspace.Editor.Dirty, "late result does not overwrite newer draft");
                    NotesDomainChecks.Require(workspace.TakeNavigation() == null, "old close action never revives");
                    storage.Fail = true;
                    workspace.Request(new NotesAction(NotesActionKind.Create));
                    Until(() => { workspace.Poll(); return !workspace.Feature.Busy; });
                    NotesDomainChecks.Require(workspace.Editor.Dirty && workspace.Feature.Saved.Notes.Count == 1, "failure leaves draft, blocks dependent create");
                    storage.Fail = false;
                    workspace.Request(new NotesAction(NotesActionKind.Leave, x: 9));
                    Until(() => { workspace.Poll(); return !workspace.Feature.Busy; });
                    NotesDomainChecks.Require(workspace.Editor == null && workspace.TakeNavigation().X == 9, "successful current save releases navigation");
                    workspace.RequestDelete(original.Id); storage.Fail = true; workspace.RequestDelete(original.Id);
                    Until(() => { workspace.Poll(); return !workspace.Feature.Busy; });
                    NotesDomainChecks.Require(workspace.Feature.Saved.Find(original.Id) != null && workspace.DeleteConfirmation == original.Id, "failed deletion keeps note and confirmation");
                    workspace.Suspend(); NotesDomainChecks.Require(workspace.DeleteConfirmation == null, "hidden confirmation expires");
                }
                NotesDomainChecks.Require(storage.Disposed, "worker actually releases ownership");
                storage.Entered.Dispose(); storage.Release.Dispose();
            });
        }
        private static void Until(Func<bool> predicate)
        { if (!SpinWait.SpinUntil(predicate, 5000)) throw new InvalidOperationException("notes bounded result wait failed"); }
        private sealed class GateStorage : IPreferenceStorage
        {
            private readonly byte[] source;
            internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim Release = new ManualResetEventSlim();
            internal bool Fail, Disposed;
            internal GateStorage(Notebook book) { source = new NotebookCodec().Encode(book); }
            public PreferenceReadResult Read() { return new PreferenceReadResult(PreferenceReadStatus.Loaded, source, "initial", null); }
            public PreferenceWriteResult Write(string identity, byte[] contents)
            {
                Entered.Set(); if (!Release.Wait(5000)) throw new TimeoutException("test release not signalled");
                return new PreferenceWriteResult(Fail ? PreferenceWriteStatus.IoFailure : PreferenceWriteStatus.Saved, "next", Fail ? "injected" : null);
            }
            public void Dispose() { Disposed = true; }
        }
    }
}
