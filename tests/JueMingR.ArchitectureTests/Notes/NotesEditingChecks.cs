using System;
using System.Collections.Generic;
using JueMingR.Features.Notes;

namespace JueMingR.ArchitectureTests
{
    internal static class NotesEditingChecks
    {
        internal static void Check(IList<string> failures)
        {
            NotesDomainChecks.Run(failures, "incremental layout budget and unchanged prefix", () =>
            {
                string text = new string('中', 20000); var editor = new NoteEditor(false, text); int measured = 0;
                var layout = new NotesTextLayout(text, 40, s => { measured += s.Length; return 1; }, editor.Boundaries);
                NotesDomainChecks.Require(measured == 0 && !layout.Complete && !layout.CanLocate(text.Length), "construction has no hidden whole-body work");
                int step = layout.Continue(1024);
                NotesDomainChecks.Require(step == 1024 && measured == 1024 && !layout.Complete, "work budget counts UTF16 units");
                while (!layout.Complete) layout.Continue(1024);
                editor.Insert("尾"); measured = 0;
                var changed = new NotesTextLayout(editor.Text, 40, s => { measured += s.Length; return 1; }, editor.Boundaries, layout, editor.LastChangeStart);
                while (!changed.Complete) changed.Continue(1024);
                NotesDomainChecks.Require(measured <= 81 && changed.LineEnd(editor.Caret) == editor.Text.Length, "tail edit reuses unchanged visual prefix");
                var dense = new NoteEditor(false, "keep"); dense.MoveTo(4);
                NotesDomainChecks.Require(!dense.Insert("A" + new string('\u0301', 1024)) && dense.Text == "keep" && !dense.Dirty, "pathological element rejected without trimming draft");
                NotesDomainChecks.Require(dense.Insert("A" + new string('\u0301', 1023)), "exact element resource boundary accepted");
                var zeroWidth = new NotesTextLayout(new string('x', 20000), 40, s => 0);
                NotesDomainChecks.Require(zeroWidth.Lines.Count == 20 && zeroWidth.Lines[19].End == 20000, "dense zero-width text wraps visually without truncation");
            });
            NotesDomainChecks.Run(failures, "local edit index matches full segmentation across joins", () =>
            {
                var random = new Random(4281); string[] values = { "中", "a", "\u0301", "\u200d", "🇨", "🇳", "👩", "🏽", "\u094d", "\u1100", "\u1161" };
                var editor = new NoteEditor(false, "A🇨🇳🇨🇳🇨🇳B");
                for (int i = 0; i < 800; i++)
                {
                    editor.MoveTo(random.Next(editor.Text.Length + 1));
                    if (random.Next(3) == 0) editor.Delete(); else editor.Insert(values[random.Next(values.Length)]);
                    int[] expected = TextElements.Boundaries(editor.Text);
                    NotesDomainChecks.Require(expected.Length == editor.Boundaries.Count, "segmentation count at edit " + i);
                    for (int j = 0; j < expected.Length; j++) NotesDomainChecks.Require(expected[j] == editor.Boundaries[j], "segmentation boundary at edit " + i);
                }
            });
            NotesDomainChecks.Run(failures, "dense newline tail layout shares line prefix", () =>
            {
                Note note = Note.Create().WithText(false, new string('\n', Note.MaximumBodyUnits - 1));
                var layout = new NotesTextLayout(note.Body, 40, s => 1, note.BodyBoundaries);
                while (!layout.Complete) layout.Continue(1024);
                var editor = new NoteEditor(false, note.Body); editor.Insert("尾");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var next = new NotesTextLayout(editor.Text, 40, s => 1, editor.Boundaries, layout, editor.LastChangeStart);
                while (!next.Complete) next.Continue(1024); clock.Stop();
                NotesDomainChecks.Require(next.Lines.Count == Note.MaximumBodyUnits && next.LineEnd(editor.Caret) == editor.Text.Length, "all blank lines and tail retained");
                NotesDomainChecks.Require(ReferenceEquals(layout.Lines[100000], next.Lines[100000]), "completed immutable line shared");
                Console.WriteLine("Notes dense-newline evidence: 1 Mi visual-line prefix tail layout {0:F3} ms (neutral metric, no FPS claim).", clock.ElapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            });
            NotesDomainChecks.Run(failures, "notes character boundaries and title preservation", () =>
            {
                string[] characters = { "中", "😀", "e\u0301", "👩🏽‍💻", "👨‍👩‍👧‍👦", "🇨🇳", "1️⃣", "\u1100\u1161\u11a8" };
                foreach (string value in characters)
                {
                    NotesDomainChecks.Require(TextElements.Boundaries(value).Length == 2, "one element: " + value);
                    var edit = new NoteEditor(false, "A" + value + "B");
                    edit.MoveTo(1 + value.Length); edit.Backspace();
                    NotesDomainChecks.Require(edit.Text == "AB" && edit.Caret == 1, "backspace whole element");
                }
                var title = new NoteEditor(true, new string('中', 80));
                title.MoveTo(0);
                NotesDomainChecks.Require(!title.Insert("新") && title.Text == new string('中', 80), "reject addition without truncating old tail");
                title.Delete();
                NotesDomainChecks.Require(title.Insert("👩🏽‍💻") && TextElements.Boundaries(title.Text).Length == 81, "emoji fits one freed slot");
                var body = new NoteEditor(false, "[i:123]abc");
                body.MoveTo(7); body.Backspace();
                NotesDomainChecks.Require(body.Text == "[i:123abc", "tag is literal, no whole snippet deletion");
            });
            NotesDomainChecks.Run(failures, "notes shared visual lines and caret", () =>
            {
                var layout = new NotesTextLayout("abcdef\n中😀e\u0301", 3, s => 1);
                NotesDomainChecks.Require(layout.Lines.Count == 3, "wrap and explicit newline");
                NotesDomainChecks.Require(layout.Hit(2, 1) == 5, "click second visual line");
                NotesDomainChecks.Require(layout.LineHome(4) == 3 && layout.LineEnd(4) == 6, "home/end visual line");
                NotesDomainChecks.Require(layout.Vertical(1, 1) == 4, "down keeps visual column");
            });
        }
    }
}
