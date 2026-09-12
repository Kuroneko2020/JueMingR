using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.WorldObjectText;

namespace JueMingR.ArchitectureTests
{
    internal static class ObjectTextChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                // Reflection only bridges the test-first absent API. These are
                // behavioral literals, not a source/type-existence acceptance.
                Type type = typeof(WorldObjectResolver).Assembly.GetType("JueMingR.Features.WorldObjectText.WorldTextCursor");
                Require(type != null, "bounded rich-text cursor is not implemented");
                foreach (string value in new[] { "中", "😀", "e\u0301", "👩🏽‍💻", "👨‍👩‍👧‍👦", "🇨🇳", "1️⃣", "\u1100\u1161\u11a8" })
                    Require(Read(type, "A" + value + "B").Select(e => e.Text).SequenceEqual(new[] { "A", value, "B" }), "complete visible unit: " + value);
                Require(Read(type, "A\r\n\nB").Select(e => e.Text).SequenceEqual(new[] { "A", "\n", "\n", "B" }), "normalize CRLF and retain internal blank line");
                var color = Read(type, "e[c/FF0000:\u0301]B");
                Require(color.Count == 2 && color[0].Text == "e\u0301", "color boundary cannot split combining cluster");
                Require(Read(type, "[c/00FF00:中😀]").Count == 2, "color markup counts zero");
                var icon = Read(type, "A[i:1]B");
                Require(icon.Count == 3 && icon[1].Item == "[i:1]", "valid native item is one atomic visible unit");
                foreach (string bad in new[] { "[unknown:abc]", "[c/GG0000:abc]", "[c/FF0000:abc", "[i:nope]", "[i:0]" })
                    Require(String.Concat(Read(type, bad).Select(e => e.Text)) == bad, "bad/unknown tag retains literal source: " + bad);
                var cursor = Activator.CreateInstance(type, new object[] { new string('x', 1000000), (Func<int, bool>)(id => id > 0 && id < 6000) });
                MethodInfo move = type.GetMethod("MoveNext");
                for (int i = 0; i < 10; i++) Require(move.Invoke(cursor, new object[] { 16 }).ToString() == "Element", "long ordinary source makes bounded prefix progress");
                Require((int)type.GetProperty("SourceOffset").GetValue(cursor) <= 32, "prefix work does not enumerate million-unit tail");
            }
            catch (Exception e) { failures.Add("World object text: " + (e.InnerException ?? e).Message); }
        }
        private sealed class Element { internal string Text, Item; }
        private static List<Element> Read(Type type, string source)
        {
            var cursor = Activator.CreateInstance(type, new object[] { source, (Func<int, bool>)(id => id > 0 && id < 6000) });
            var result = new List<Element>(); var move = type.GetMethod("MoveNext");
            for (int step = 0; step < source.Length * 8 + 20; step++)
            {
                string state = move.Invoke(cursor, new object[] { 16 }).ToString();
                if (state == "End") return result;
                if (state == "Pending") continue;
                object current = type.GetProperty("Current").GetValue(cursor);
                Type entry = current.GetType();
                result.Add(new Element { Text = (string)entry.GetProperty("Text").GetValue(current), Item = (string)entry.GetProperty("ItemTag").GetValue(current) });
            }
            throw new InvalidOperationException("cursor did not make finite progress");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
