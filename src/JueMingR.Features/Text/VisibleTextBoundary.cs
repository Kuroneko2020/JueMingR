using System;
using System.Globalization;

namespace JueMingR.Features.Text
{
    // Shared, deliberately finite rules already used by Notes on net472. This is
    // not a claim of complete current UAX #29 support. Renderers may lack glyphs;
    // that never permits cutting surrogate pairs, marks or joined emoji in half.
    public static class VisibleTextBoundary
    {
        public const int MaximumElementUnits = 1024;
        public static bool RequiresIndex(char c, UnicodeCategory category)
        { return Char.IsSurrogate(c) || Char.IsControl(c) || Extend(c, category) || c == 0x200d || Prepend(c) || Hangul(c) != 0; }
        public static bool Joins(int previous, int current, UnicodeCategory category, int regionalRun)
        {
            if (previous < 0 || previous == 13 && current == 10) return true;
            return !Control(previous) && !Control(current) && (Extend(current, category) || current == 0x200d || previous == 0x200d ||
                Prepend(previous) || HangulJoin(previous, current) || current >= 0x1f1e6 && current <= 0x1f1ff && regionalRun % 2 == 1 || Virama(previous));
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
