using System;
using System.Collections.Generic;
using JueMingR.Features.Notes;

namespace JueMingR.ArchitectureTests
{
    internal static class NotesEditingChecks
    {
        internal static void Check(IList<string> failures)
        {
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
