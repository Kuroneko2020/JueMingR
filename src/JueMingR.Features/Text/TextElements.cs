using System;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;

namespace JueMingR.Features.Text
{
    public static class TextElements
    {
        // Resource protection, separate from the 80-element title contract. This
        // permits normal emoji/marks by a wide margin while preventing one element
        // from turning a bounded layout/draw step into a million-character call.
        public const int MaximumElementUnits = VisibleTextBoundary.MaximumElementUnits;
        public static bool IsValid(string text)
        {
            if (text == null) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (Char.IsHighSurrogate(c)) { if (++i == text.Length || !Char.IsLowSurrogate(text[i])) return false; }
                else if (Char.IsLowSurrogate(c) || (Char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')) return false;
            }
            return true;
        }
        public static int[] Boundaries(string text)
        {
            if (!IsValid(text)) throw new ArgumentException("invalid-text-encoding-or-controls");
            var starts = new List<int>(Math.Min(text.Length + 1, 4096)) { 0 };
            int previous = -1, regionalRun = 0;
            for (int i = 0; i < text.Length;)
            {
                int current = Char.ConvertToUtf32(text, i);
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(text, i);
                bool regional = current >= 0x1f1e6 && current <= 0x1f1ff;
                // net472's StringInfo predates extended emoji clusters. Keep marks,
                // joiners and their following scalar together conservatively, plus
                // RI pairs/Hangul. Drawing support never changes stored boundaries.
                bool join = VisibleTextBoundary.Joins(previous, current, category, regionalRun);
                if (!join) starts.Add(i);
                regionalRun = regional ? regionalRun + 1 : 0;
                previous = current; i += current > 0xffff ? 2 : 1;
                if (i - starts[starts.Count - 1] > MaximumElementUnits) throw new ArgumentException("text-element-exceeds-1024-UTF16-units");
            }
            if (text.Length != 0) starts.Add(text.Length);
            return starts.ToArray();
        }
    }
}
