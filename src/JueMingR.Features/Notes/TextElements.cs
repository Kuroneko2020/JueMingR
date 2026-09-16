using System;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;

namespace JueMingR.Features.Notes
{
    public static class TextElements
    {
        // Resource protection, separate from the 80-element title contract. This
        // permits normal emoji/marks by a wide margin while preventing one element
        // from turning a bounded layout/draw step into a million-character call.
        public const int MaximumElementUnits = Text.VisibleTextBoundary.MaximumElementUnits;
        internal static IReadOnlyList<int> Index(string text)
        {
            // Common BMP text has boundary i == offset i. Keep that identity map
            // implicit, so a 15 MiB ASCII document does not retain 60 MiB of ints
            // in every immutable snapshot. Complex text uses the same full rules.
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i]; if (c >= 32 && c <= 126 || c == '\n' || c == '\t') continue;
                if (Text.VisibleTextBoundary.RequiresIndex(c, CharUnicodeInfo.GetUnicodeCategory(text, i)))
                    return Array.AsReadOnly(Boundaries(text));
            }
            return new UnitIndex(text.Length);
        }
        internal static IReadOnlyList<int> Compact(int[] index, int length)
        { return index.Length == length + 1 ? (IReadOnlyList<int>)new UnitIndex(length) : Array.AsReadOnly(index); }
        private sealed class UnitIndex : IReadOnlyList<int>
        {
            private readonly int length;
            internal UnitIndex(int length) { this.length = length; }
            public int Count { get { return length + 1; } }
            public int this[int index] { get { if (index < 0 || index > length) throw new ArgumentOutOfRangeException(nameof(index)); return index; } }
            public IEnumerator<int> GetEnumerator() { for (int i = 0; i <= length; i++) yield return i; }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        }
        public static bool IsValid(string text) { return Text.TextElements.IsValid(text); }
        public static int[] Boundaries(string text) { return Text.TextElements.Boundaries(text); }
    }
}
