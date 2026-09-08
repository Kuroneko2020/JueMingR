using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JueMingR.Infrastructure.Storage;
using JueMingR.Features.Notes;
using JueMingR.Platform.Persistence;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class NotesRevisionChecks
    {
        internal static void Check(IList<string> failures)
        {
            NotesDomainChecks.Run(failures, "non-file retained source protects the old document before replace", () =>
            {
                string root = Path.Combine(Path.GetTempPath(), "JueMingR-schema-path-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
                string path = Path.Combine(root, "notes.json"), archive = path + ".schema1-original";
                byte[] raw = Encoding.UTF8.GetBytes("{\"schema\":1,\"notes\":[]}");
                File.WriteAllBytes(path, raw); Directory.CreateDirectory(archive);
                try
                {
                    using (var storage = new AtomicFileDocument(path, Notebook.MaximumBytes, true, ".schema1-original"))
                    {
                        var loaded = storage.Read(); storage.RetainLoadedSource();
                        var saved = storage.Write(loaded.Identity, new NotebookCodec().Encode(Notebook.Empty));
                        NotesDomainChecks.Require(saved.Status == PreferenceWriteStatus.Conflict && !saved.CommitUnconfirmed,
                            "an existing non-file archive is a protected conflict, not a retryable save failure");
                        NotesDomainChecks.Require(Convert.ToBase64String(File.ReadAllBytes(path)) == Convert.ToBase64String(raw) &&
                            Directory.Exists(archive) && !File.Exists(path + ".bak"), "invalid archive cannot replace the old source");
                    }
                }
                finally { Directory.Delete(archive); Directory.Delete(root, true); }
            });
            NotesDomainChecks.Run(failures, "full-size schema 1 upgrades through worker without modifying source archive", () =>
            {
                string root = Path.Combine(Path.GetTempPath(), "JueMingR-schema-boundary-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
                string path = Path.Combine(root, "notes.json"); var codec = new NotebookCodec();
                var notes = new List<Note>(); for (int i = 0; i < Notebook.MaximumNotes; i++) notes.Add(Note.Create().WithText(false, "原文\r\n[i:2]"));
                string encoded = Encoding.UTF8.GetString(codec.Encode(new Notebook(notes))).Replace("\"schema\":2", "\"schema\":1").Replace(",\"readingWidth\":280,\"fontPercent\":120", "");
                byte[] prefix = Encoding.UTF8.GetBytes(encoded), original = new byte[Notebook.LegacyMaximumBytes];
                Array.Copy(prefix, original, prefix.Length); for (int i = prefix.Length; i < original.Length; i++) original[i] = 32;
                File.WriteAllBytes(path, original);
                try
                {
                    var storage = new AtomicFileDocument(path, Notebook.MaximumBytes, true, ".schema1-original");
                    using (var worker = new DocumentWorker<Notebook>(storage, bytes =>
                    { Notebook book = codec.Decode(bytes); if (book.SourceSchema == 1) storage.RetainLoadedSource(); return book; }, codec.Encode, Notebook.Empty))
                    {
                        var loaded = NotesStorageChecks.Take(worker); NotesDomainChecks.Require(loaded.Success && loaded.Value.Notes.Count == Notebook.MaximumNotes, "old full limit remains readable");
                        NotesDomainChecks.Require(!File.Exists(path + ".schema1-original"), "loading alone never upgrades or archives");
                        worker.TrySubmit(1, loaded.Value); var saved = NotesStorageChecks.Take(worker);
                        NotesDomainChecks.Require(saved.Success, "metadata headroom permits all existing notes");
                        Notebook actual = codec.Decode(File.ReadAllBytes(path));
                        NotesDomainChecks.Require(actual.SourceSchema == 2 && actual.Notes[1023].Body == "原文\r\n[i:2]", "actual file preserves content and upgrades schema");
                        NotesDomainChecks.Require(File.ReadAllBytes(path + ".schema1-original").Length == original.Length &&
                            Convert.ToBase64String(File.ReadAllBytes(path + ".schema1-original")) == Convert.ToBase64String(original), "archive is exact original bytes including whitespace");
                    }
                }
                finally { Directory.Delete(root, true); }
            });
            NotesDomainChecks.Run(failures, "conflicting retained source protects both files before replace", () =>
            {
                string root = Path.Combine(Path.GetTempPath(), "JueMingR-schema-conflict-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
                string path = Path.Combine(root, "notes.json"); byte[] raw = Encoding.UTF8.GetBytes("{\"schema\":1,\"notes\":[]}");
                File.WriteAllBytes(path, raw); File.WriteAllText(path + ".schema1-original", "different source");
                try
                {
                    using (var storage = new AtomicFileDocument(path, Notebook.MaximumBytes, true, ".schema1-original"))
                    {
                        var loaded = storage.Read(); storage.RetainLoadedSource();
                        var saved = storage.Write(loaded.Identity, new NotebookCodec().Encode(Notebook.Empty));
                        NotesDomainChecks.Require(saved.Status == PreferenceWriteStatus.Conflict && !saved.CommitUnconfirmed,
                            "archive conflict rejects before replacement");
                        NotesDomainChecks.Require(Encoding.UTF8.GetString(File.ReadAllBytes(path)) == Encoding.UTF8.GetString(raw) &&
                            File.ReadAllText(path + ".schema1-original") == "different source", "neither source is overwritten");
                    }
                }
                finally { Directory.Delete(root, true); }
            });
            NotesDomainChecks.Run(failures, "upgrade retains exact original beyond ordinary backup rotation", () =>
            {
                string root = Path.Combine(Path.GetTempPath(), "JueMingR-reading-upgrade-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root); string path = Path.Combine(root, "notes.json");
                byte[] original = Encoding.UTF8.GetBytes("{ \"schema\" : 1, \"notes\" : [] }"); File.WriteAllBytes(path, original);
                try
                {
                    var ctor = typeof(AtomicFileDocument).GetConstructor(new[] { typeof(string), typeof(int), typeof(bool), typeof(string) });
                    NotesDomainChecks.Require(ctor != null, "mechanical storage must preserve an explicit nonrotating upgrade source");
                    using (var storage = (AtomicFileDocument)ctor.Invoke(new object[] { path, Notebook.MaximumBytes, true, ".schema1-original" }))
                    {
                        var read = storage.Read();
                        typeof(AtomicFileDocument).GetMethod("RetainLoadedSource").Invoke(storage, null);
                        byte[] upgraded = new NotebookCodec().Encode(new NotebookCodec().Decode(original));
                        var written = storage.Write(read.Identity, upgraded);
                        NotesDomainChecks.Require(written.Status == JueMingR.Platform.Settings.PreferenceWriteStatus.Saved, "upgrade saved");
                        storage.Write(written.Identity, new NotebookCodec().Encode(Notebook.Empty.Add(Note.Create())));
                        NotesDomainChecks.Require(Convert.ToBase64String(File.ReadAllBytes(path + ".schema1-original")) == Convert.ToBase64String(original), "raw original survives later backup rotation");
                    }
                    File.Delete(path); File.Delete(path + ".bak");
                    using (var storage = (AtomicFileDocument)ctor.Invoke(new object[] { path, Notebook.MaximumBytes, true, ".schema1-original" }))
                        NotesDomainChecks.Require(storage.Read().Status != JueMingR.Platform.Settings.PreferenceReadStatus.Missing, "archive alone is recovery, not first use");
                }
                finally { Directory.Delete(root, true); }
            });
            NotesDomainChecks.Run(failures, "schema 2 reading metadata and schema 1 content compatibility", () =>
            {
                var codec = new NotebookCodec();
                string old = "{\"schema\":1,\"notes\":[{\"id\":\"12345678901234567890123456789012\",\"title\":\" old \",\"body\":\"原文\\r\\n[i:2]\",\"pinned\":true,\"x\":101,\"y\":202,\"opacity\":35}]}";
                Notebook legacy = codec.Decode(System.Text.Encoding.UTF8.GetBytes(old));
                string current = old.Replace("\"schema\":1", "\"schema\":2").Replace("\"opacity\":35", "\"opacity\":35,\"readingWidth\":400,\"fontPercent\":150");
                Notebook restored = codec.Decode(System.Text.Encoding.UTF8.GetBytes(current));
                string encoded = System.Text.Encoding.UTF8.GetString(codec.Encode(restored));
                NotesDomainChecks.Require(encoded.Contains("\"readingWidth\":400") && encoded.Contains("\"fontPercent\":150"), "new preferences round trip");
                NotesDomainChecks.Require(restored.Notes[0].Body == legacy.Notes[0].Body && restored.Notes[0].Title == " old " && restored.Notes[0].X == 101, "all old content stays literal");
                string upgraded = System.Text.Encoding.UTF8.GetString(codec.Encode(legacy));
                NotesDomainChecks.Require(upgraded.Contains("\"schema\":2") && upgraded.Contains("\"readingWidth\":280") && upgraded.Contains("\"fontPercent\":120"), "old version maps explicit defaults");
                foreach (string bad in new[] { current.Replace("400", "239"), current.Replace("150", "181"), current.Replace("400", "400.5"),
                    current.Replace(",\"fontPercent\":150", ""), current.Replace("\"readingWidth\":400", "\"readingWidth\":400,\"readingWidth\":400"),
                    current.Replace("\"fontPercent\":150", "\"fontPercent\":150,\"future\":1") })
                {
                    bool rejected = false; try { codec.Decode(Encoding.UTF8.GetBytes(bad)); } catch (Exception) { rejected = true; }
                    NotesDomainChecks.Require(rejected, "invalid reading metadata cannot be defaulted or silently rewritten");
                }
            });
            NotesDomainChecks.Run(failures, "selection replacement preserves complete characters and original on rejection", () =>
            {
                var editor = new NoteEditor(false, "A👩🏽‍💻中\nend");
                editor.MoveTo(1);
                var move = typeof(NoteEditor).GetMethod("MoveTo", new[] { typeof(int), typeof(bool) });
                NotesDomainChecks.Require(move != null, "editor must own a text selection, not renderer rectangles");
                move.Invoke(editor, new object[] { 1 + "👩🏽‍💻".Length, true });
                NotesDomainChecks.Require(editor.Insert("新") && editor.Text == "A新中\nend", "typing replaces whole selected emoji once");
                editor.MoveTo(1); move.Invoke(editor, new object[] { 2, true });
                NotesDomainChecks.Require(!editor.Insert("\ud800") && editor.Text == "A新中\nend", "invalid replacement preserves original");
                editor.Delete();
                NotesDomainChecks.Require(editor.Text == "A中\nend", "rejected input preserves selection for deletion");
                var title = new NoteEditor(true, new string('中', 80)); title.MoveTo(0);
                move.Invoke(title, new object[] { 1, true });
                NotesDomainChecks.Require(title.Insert("新") && title.Text.Length == 80, "selected title capacity is replaced, not added");
                title.MoveTo(0); move.Invoke(title, new object[] { 1, true });
                NotesDomainChecks.Require(!title.Insert("太长") && title.Text.StartsWith("新"), "oversize replacement atomic");
                title.Backspace(); NotesDomainChecks.Require(title.Text.Length == 79, "rejected title retains same selection");
            });
            NotesDomainChecks.Run(failures, "reverse selection collapses and copies original line breaks", () =>
            {
                var editor = new NoteEditor(false, "abc\ndef"); editor.MoveTo(6);
                var move = typeof(NoteEditor).GetMethod("MoveTo", new[] { typeof(int), typeof(bool) });
                NotesDomainChecks.Require(move != null, "selection navigation required");
                move.Invoke(editor, new object[] { 1, true });
                var selected = typeof(NoteEditor).GetProperty("SelectedText");
                NotesDomainChecks.Require(selected != null && (string)selected.GetValue(editor) == "bc\nde", "reverse selection is source text");
                editor.Move(1); NotesDomainChecks.Require(editor.Caret == 6, "right collapses to selection end");
                editor.Backspace(); NotesDomainChecks.Require(editor.Text == "abc\ndf", "ordinary deletion resumes at collapsed caret");
            });
        }
    }
}
