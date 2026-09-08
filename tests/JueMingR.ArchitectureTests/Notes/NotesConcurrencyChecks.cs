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
            NotesDomainChecks.Run(failures, "reading batch restarts its quiet window and keeps late intent", () =>
            {
                Note note = Note.Create().Pin(0, 0); var codec = new NotebookCodec();
                var storage = new GateStorage(Notebook.Empty.Add(note));
                using (var worker = new DocumentWorker<Notebook>(storage, codec.Decode, codec.Encode, Notebook.Empty))
                {
                    var feature = new NotesFeature(worker); var workspace = new NotesWorkspace(feature);
                    Until(() => { workspace.Poll(); return feature.Loaded; });
                    feature.AdjustReading(note.Id, true, 1);
                    Thread.Sleep(1050); // Deliberately cross the maximum batch age before submitting.
                    feature.AdjustReading(note.Id, true, 1); feature.FlushReading(); Until(() => storage.Entered.IsSet);
                    feature.AdjustReading(note.Id, false, 1); storage.Release.Set();
                    Until(() => { feature.Poll(); return !feature.Busy; });
                    feature.FlushReading();
                    NotesDomainChecks.Require(!feature.Busy && feature.HasPendingReading && storage.Writes == 1,
                        "new batch must not inherit an expired one-second deadline");
                    NotesDomainChecks.Require(feature.Stop(5000), "exit drains remaining reading intent");
                    Note actual = codec.Decode(storage.Latest).Find(note.Id);
                    NotesDomainChecks.Require(actual.Reading.Width == 360 && actual.Reading.FontPercent == 130 && storage.Writes == 2,
                        "late reading intent remains once after old completion");
                }
                storage.Entered.Dispose(); storage.Release.Dispose();
            });
            foreach (string action in new[] { "save", "failed-save", "unpin", "delete" })
                NotesDomainChecks.Run(failures, "exit applies reading to last successful content: " + action, () =>
                {
                    Note original = Note.Create().Pin(0, 0).WithText(false, "before"); var codec = new NotebookCodec();
                    var storage = new GateStorage(Notebook.Empty.Add(original)) { Fail = action == "failed-save" };
                    using (var worker = new DocumentWorker<Notebook>(storage, codec.Decode, codec.Encode, Notebook.Empty))
                    {
                        var feature = new NotesFeature(worker); var workspace = new NotesWorkspace(feature);
                        Until(() => { workspace.Poll(); return feature.Loaded; });
                        feature.AdjustReading(original.Id, true, 2);
                        if (action.EndsWith("save", StringComparison.Ordinal))
                        {
                            workspace.Request(new NotesAction(NotesActionKind.BeginEdit, original.Id, false, 6)); workspace.Editor.Insert(" latest");
                            workspace.Request(new NotesAction(NotesActionKind.Save));
                        }
                        else if (action == "unpin") workspace.Request(new NotesAction(NotesActionKind.Unpin, original.Id));
                        else { workspace.RequestDelete(original.Id); workspace.RequestDelete(original.Id); }
                        Until(() => storage.Entered.IsSet); storage.Release.Set();
                        NotesDomainChecks.Require(feature.Stop(5000), "finite exit finishes");
                        Note actual = codec.Decode(storage.Latest).Find(original.Id);
                        if (action == "delete") NotesDomainChecks.Require(actual == null, "exit never recreates deleted note");
                        else if (action == "unpin") NotesDomainChecks.Require(!actual.Pinned && actual.Reading.Same(NoteReading.Default), "old lifetime never repins");
                        else if (action == "failed-save") NotesDomainChecks.Require(actual.Body == "before" && actual.Reading.Same(NoteReading.Default) && storage.Writes == 1, "failure does not trigger a stale final overwrite");
                        else NotesDomainChecks.Require(actual.Body == "before latest" && actual.Reading.Width == 360 && storage.Writes == 2, "exit starts from unclaimed successful body");
                    }
                    storage.Entered.Dispose(); storage.Release.Dispose();
                });
            NotesDomainChecks.Run(failures, "failed reading stays visible without retry spam and ends with its pin", () =>
            {
                Note note = Note.Create().Pin(0, 0); var codec = new NotebookCodec();
                var storage = new GateStorage(Notebook.Empty.Add(note)) { Fail = true }; storage.Release.Set();
                using (var worker = new DocumentWorker<Notebook>(storage, codec.Decode, codec.Encode, Notebook.Empty))
                {
                    var feature = new NotesFeature(worker); var workspace = new NotesWorkspace(feature);
                    Until(() => { workspace.Poll(); return feature.Loaded; }); feature.AdjustReading(note.Id, false, 2);
                    Until(() => { workspace.Poll(); return feature.ReadingError != null; });
                    for (int i = 0; i < 100; i++) workspace.Poll();
                    NotesDomainChecks.Require(storage.Writes == 1 && feature.ReadingFor(note.Id).FontPercent == 140 && feature.Saved.Find(note.Id).Reading.FontPercent == 120,
                        "failed preview is retained separately, without automatic writes");
                    storage.Fail = false; workspace.Request(new NotesAction(NotesActionKind.Unpin, note.Id)); Until(() => { workspace.Poll(); return !feature.Busy; });
                    NotesDomainChecks.Require(!feature.HasPendingReading && feature.ReadingError == null, "cancelled lifetime ends only its reading error");
                }
                storage.Entered.Dispose(); storage.Release.Dispose();
            });
            NotesDomainChecks.Run(failures, "reading intents coalesce while preserving other drafts and current saved text", () =>
            {
                Note original = Note.Create().Pin(80, 90).WithText(false, "original");
                var storage = new GateStorage(Notebook.Empty.Add(original)); storage.Release.Set(); var codec = new NotebookCodec();
                using (var worker = new DocumentWorker<Notebook>(storage, codec.Decode, codec.Encode, Notebook.Empty))
                {
                    var feature = new NotesFeature(worker); var workspace = new NotesWorkspace(feature);
                    Until(() => { workspace.Poll(); return feature.Loaded; });
                    workspace.Request(new NotesAction(NotesActionKind.BeginEdit, original.Id, false, 8)); workspace.Editor.Insert(" latest");
                    var adjust = typeof(NotesFeature).GetMethod("AdjustReading");
                    NotesDomainChecks.Require(adjust != null, "reading preferences need a separate bounded intent path");
                    for (int i = 0; i < 100; i++) adjust.Invoke(feature, new object[] { original.Id, true, i % 2 == 0 ? 1 : -1 });
                    adjust.Invoke(feature, new object[] { original.Id, true, 2 });
                    NotesDomainChecks.Require(workspace.Editor.Dirty && !feature.Busy && !storage.Entered.IsSet, "wheel preview never submits draft or each tick");
                    workspace.Request(new NotesAction(NotesActionKind.Save));
                    Until(() => { workspace.Poll(); return !feature.Busy; });
                    Until(() => { workspace.Poll(); return !feature.Busy && feature.Saved.Find(original.Id).Reading.Width == 360; });
                    NotesDomainChecks.Require(feature.Saved.Find(original.Id).Body == "original latest", "reading save uses current content");
                    NotesDomainChecks.Require(storage.Writes == 2, "one text write and one combined reading write");
                    workspace.Request(new NotesAction(NotesActionKind.Unpin, original.Id)); Until(() => { workspace.Poll(); return !feature.Busy; });
                    workspace.Request(new NotesAction(NotesActionKind.Pin, original.Id)); Until(() => { workspace.Poll(); return !feature.Busy; });
                    NotesDomainChecks.Require(feature.Saved.Find(original.Id).Reading.Same(NoteReading.Default), "new pin lifetime restores defaults");
                }
                storage.Entered.Dispose(); storage.Release.Dispose();
            });
            NotesDomainChecks.Run(failures, "already pinned action keeps position and opacity without writing", () =>
            {
                Note original = Note.Create().Pin(80, 90).WithOpacity(35);
                var storage = new GateStorage(Notebook.Empty.Add(original)); storage.Release.Set(); var codec = new NotebookCodec();
                using (var worker = new DocumentWorker<Notebook>(storage, codec.Decode, codec.Encode, Notebook.Empty))
                {
                    var workspace = new NotesWorkspace(new NotesFeature(worker));
                    Until(() => { workspace.Poll(); return workspace.Feature.Loaded; });
                    NotesDomainChecks.Require(workspace.Request(new NotesAction(NotesActionKind.Pin, original.Id, x: 200, y: 300)), "repeated pin accepted");
                    NotesDomainChecks.Require(!workspace.Feature.Busy && !storage.Entered.IsSet, "already pinned does not write");
                    Note saved = workspace.Feature.Saved.Find(original.Id);
                    NotesDomainChecks.Require(saved.X == 80 && saved.Y == 90 && saved.Opacity == 35, "position and opacity unchanged");
                }
                storage.Entered.Dispose(); storage.Release.Dispose();
            });
            NotesDomainChecks.Run(failures, "notes unconfirmed commit is terminal, never ordinary failure", () =>
            {
                var storage = new GateStorage(Notebook.Empty) { Unconfirmed = true }; storage.Release.Set(); var codec = new NotebookCodec();
                using (var worker = new DocumentWorker<Notebook>(storage, codec.Decode, codec.Encode, Notebook.Empty))
                {
                    NotesStorageChecks.Take(worker); worker.TrySubmit(1, Notebook.Empty.Add(Note.Create()));
                    var result = NotesStorageChecks.Take(worker);
                    NotesDomainChecks.Require(result.CommitUnconfirmed && !worker.TrySubmit(2, Notebook.Empty), "ambiguous commit locks writing");
                }
                storage.Entered.Dispose(); storage.Release.Dispose();
            });
            NotesDomainChecks.Run(failures, "notes stop timeout cancels before entering file commit", () =>
            {
                var storage = new GateStorage(Notebook.Empty); storage.Release.Set(); var codec = new NotebookCodec();
                using (var encodeEntered = new ManualResetEventSlim())
                using (var encodeRelease = new ManualResetEventSlim())
                using (var worker = new DocumentWorker<Notebook>(storage, codec.Decode, book =>
                { encodeEntered.Set(); if (!encodeRelease.Wait(5000)) throw new TimeoutException(); return codec.Encode(book); }, Notebook.Empty))
                {
                    NotesStorageChecks.Take(worker); worker.TrySubmit(1, Notebook.Empty.Add(Note.Create()));
                    Until(() => encodeEntered.IsSet); NotesDomainChecks.Require(!worker.Stop(0), "encoder still owns worker");
                    encodeRelease.Set(); NotesDomainChecks.Require(worker.Stop(5000), "worker actually stopped");
                    NotesDomainChecks.Require(!storage.Entered.IsSet && storage.Disposed, "no write starts after timed out stop");
                }
                storage.Entered.Dispose(); storage.Release.Dispose();
            });
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
                    string failure = workspace.Error;
                    workspace.CancelEdit();
                    NotesDomainChecks.Require(workspace.Error == failure, "cancel edit must not erase an unresolved storage failure");
                    workspace.Request(new NotesAction(NotesActionKind.BeginEdit, original.Id, false, 12));
                    storage.Fail = false;
                    workspace.Request(new NotesAction(NotesActionKind.Leave, x: 9));
                    Until(() => { workspace.Poll(); return !workspace.Feature.Busy; });
                    NotesDomainChecks.Require(workspace.Editor == null && workspace.TakeNavigation().X == 9, "successful current save releases navigation");
                    workspace.RequestDelete(original.Id); storage.Fail = true; workspace.RequestDelete(original.Id);
                    Until(() => { workspace.Poll(); return !workspace.Feature.Busy; });
                    NotesDomainChecks.Require(workspace.Feature.Saved.Find(original.Id) != null && workspace.DeleteConfirmation == original.Id, "failed deletion keeps note and confirmation");
                    workspace.Suspend(); NotesDomainChecks.Require(workspace.DeleteConfirmation == null, "hidden confirmation expires");
                    storage.Fail = false;
                    workspace.Request(new NotesAction(NotesActionKind.BeginEdit, original.Id, true, 0)); workspace.Editor.SelectAll(); workspace.Editor.DeleteSelection();
                    workspace.Request(new NotesAction(NotesActionKind.Save)); Until(() => { workspace.Poll(); return !workspace.Feature.Busy; });
                    NotesDomainChecks.Require(workspace.Editor.Text == workspace.Feature.Saved.Find(original.Id).Title && workspace.Editor.Text == "新笔记" && !workspace.Editor.Dirty,
                        "explicit save shows canonical blank-title fallback in retained editor");
                    workspace.RequestDelete(original.Id); workspace.RequestDelete(original.Id);
                    Until(() => { workspace.Poll(); return !workspace.Feature.Busy; });
                    NotesDomainChecks.Require(workspace.Feature.Saved.Find(original.Id) == null && workspace.DeleteConfirmation == null,
                        "successful deletion ends the matching confirmation");
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
            internal bool Fail, Disposed, Unconfirmed;
            internal int Writes;
            internal byte[] Latest;
            internal GateStorage(Notebook book) { source = new NotebookCodec().Encode(book); Latest = source; }
            public PreferenceReadResult Read() { return new PreferenceReadResult(PreferenceReadStatus.Loaded, source, "initial", null); }
            public PreferenceWriteResult Write(string identity, byte[] contents)
            {
                Interlocked.Increment(ref Writes);
                Entered.Set(); if (!Release.Wait(5000)) throw new TimeoutException("test release not signalled");
                if (Unconfirmed) return new PreferenceWriteResult(PreferenceWriteStatus.Conflict, null, "post-replace-failed", true, true);
                if (!Fail) Latest = (byte[])contents.Clone();
                return new PreferenceWriteResult(Fail ? PreferenceWriteStatus.IoFailure : PreferenceWriteStatus.Saved, "next", Fail ? "injected" : null);
            }
            public void Dispose() { Disposed = true; }
        }
    }
}
