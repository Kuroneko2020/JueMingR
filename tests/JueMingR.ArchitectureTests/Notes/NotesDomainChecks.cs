using System;
using System.Collections.Generic;
using System.Text;
using JueMingR.Features.Notes;

namespace JueMingR.ArchitectureTests
{
    internal static class NotesDomainChecks
    {
        internal static void Check(IList<string> failures)
        {
            Run(failures, "notes identities, order and complete document", () =>
            {
                Notebook book = Notebook.Empty;
                Note first = Note.Create(), second = Note.Create();
                book = book.Add(first).Add(second);
                Require(first.Id != second.Id && first.Title == "新笔记" && first.Body == "", "independent defaults");
                book = book.Replace(first.WithText(false, "中文 [i:1] e\u0301 👩🏽‍💻"));
                book = book.Replace(book.Find(first.Id).WithText(true, second.Title));
                Require(book.Notes[0].Id == first.Id && book.Notes[1].Id == second.Id, "rename preserves order");
                var codec = new NotebookCodec();
                Notebook restored = codec.Decode(codec.Encode(book));
                Require(restored.Notes[0].Body == "中文 [i:1] e\u0301 👩🏽‍💻", "literal complete body roundtrip");
                book = book.Replace(book.Find(first.Id).Pin(70, 80).WithOpacity(35));
                Require(book.Find(first.Id).Unpin().Pin(90, 100).Opacity == 0, "unpin then repin resets background");
                book = book.Remove(first.Id);
                Require(book.Notes.Count == 1 && book.Notes[0].Id == second.Id, "delete one identity only");
            });
            Run(failures, "notes strict format rejects partial and future documents", () =>
            {
                var codec = new NotebookCodec();
                Require(codec.Decode(Encoding.UTF8.GetBytes("{\"schema\":1,\"notes\":[]}")).Notes.Count == 0, "valid empty");
                string note = "{\"id\":\"e6cf277c1fd24aa381a6803d13c579bd\",\"title\":\"a\",\"body\":\"keep\",\"pinned\":false,\"x\":0,\"y\":0,\"opacity\":0}";
                foreach (string invalid in new[] { "", "{}", "{\"schema\":2,\"notes\":[]}", "{\"schema\":1,\"notes\":[],\"future\":1}",
                    "{\"__type\":\"FutureNotebook\",\"schema\":1,\"notes\":[]}",
                    "{\"schema\":1,\"notes\":[" + note.Replace("{", "{\"__type\":\"FutureNote\",") + "]}",
                    "{\"schema\":1,\"schema\":1,\"notes\":[]}", "{\"schema\":1,\"notes\":[" + note.Replace("\"body\":\"keep\",", "") + "]}",
                    "{\"schema\":1,\"notes\":[" + note + "," + note + "]}" })
                {
                    bool rejected = false;
                    try { codec.Decode(Encoding.UTF8.GetBytes(invalid)); } catch (Exception) { rejected = true; }
                    Require(rejected, "unreliable source must not become empty: " + invalid);
                }
            });
        }
        internal static void Run(IList<string> failures, string name, Action test)
        { try { test(); } catch (Exception e) { failures.Add(name + ": " + e.Message); } }
        internal static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
