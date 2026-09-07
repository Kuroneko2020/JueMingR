using System;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingR.Features.Notes
{
    public static class TextElements
    {
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
                bool join = i == 0 || previous == 13 && current == 10;
                if (!Control(previous) && !Control(current))
                    join |= Extend(current, category) || current == 0x200d || previous == 0x200d || Prepend(previous) ||
                        HangulJoin(previous, current) || regional && regionalRun % 2 == 1 || Virama(previous);
                if (!join) starts.Add(i);
                regionalRun = regional ? regionalRun + 1 : 0;
                previous = current; i += current > 0xffff ? 2 : 1;
            }
            if (text.Length != 0) starts.Add(text.Length);
            return starts.ToArray();
        }
        private static bool Control(int c) { return c >= 0 && (c < 32 || c == 127); }
        private static bool Extend(int c, UnicodeCategory category)
        {
            return category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark ||
                category == UnicodeCategory.EnclosingMark || c == 0x200c || c >= 0x1f3fb && c <= 0x1f3ff ||
                c >= 0xe0020 && c <= 0xe007f || c >= 0xe0100 && c <= 0xe01ef;
        }
        private static bool Prepend(int c)
        { return c >= 0x600 && c <= 0x605 || c == 0x6dd || c == 0x70f || c == 0x890 || c == 0x891 || c == 0x8e2 || c == 0x110bd || c == 0x110cd; }
        private static bool Virama(int c)
        { return c == 0x94d || c == 0x9cd || c == 0xa4d || c == 0xacd || c == 0xb4d || c == 0xbcd || c == 0xc4d || c == 0xccd || c == 0xd4d || c == 0xdca; }
        private static int Hangul(int c)
        {
            if (c >= 0x1100 && c <= 0x115f || c >= 0xa960 && c <= 0xa97c) return 1;
            if (c >= 0x1160 && c <= 0x11a7 || c >= 0xd7b0 && c <= 0xd7c6) return 2;
            if (c >= 0x11a8 && c <= 0x11ff || c >= 0xd7cb && c <= 0xd7fb) return 3;
            if (c >= 0xac00 && c <= 0xd7a3) return (c - 0xac00) % 28 == 0 ? 4 : 5;
            return 0;
        }
        private static bool HangulJoin(int previous, int current)
        {
            int p = Hangul(previous), c = Hangul(current);
            return p == 1 && (c == 1 || c == 2 || c == 4 || c == 5) || (p == 2 || p == 4) && (c == 2 || c == 3) || (p == 3 || p == 5) && c == 3;
        }
    }
}
