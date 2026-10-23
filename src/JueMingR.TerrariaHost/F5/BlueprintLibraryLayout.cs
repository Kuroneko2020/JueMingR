using System;
using Microsoft.Xna.Framework;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.F5
{
    internal enum BlueprintLibraryCommand
    { None, Card, Name, Rename, Delete, Previous, Next, Back, ConfirmDraft, Cancel, Exclude, PreviousObjects, NextObjects, ExcludeAll, Place }

    internal sealed class BlueprintLibraryPart
    {
        internal F5Element Element;
        internal BlueprintLibraryCommand Command;
        internal Guid AssetId;
        internal int ObjectId;
        internal bool Enabled, Selected, Card, Editing;
        internal string PreviewKey;
        internal string Hint;
        internal F5Size HintSize;
        internal Color Ink = Color.White;
    }

    // All values are F5 UI logical units. The caller offsets and clips these
    // exact rectangles for both rendering and pointer dispatch.
    internal static class BlueprintLibraryLayout
    {
        internal const int PageCapacity = 6;
        internal const float HeaderHeight = 42, HeaderGap = 6, CardHeight = 166, Gap = 8, PreviewHeight = 68;
        internal static float Height(int count)
        {
            int rows = (Math.Max(0, Math.Min(PageCapacity, count)) + 1) / 2;
            return HeaderHeight + HeaderGap + (rows == 0 ? 58 : rows * CardHeight + (rows - 1) * Gap) + 6;
        }
        internal static F5Rect Card(float width, int index)
        {
            if (index < 0 || index >= PageCapacity) throw new ArgumentOutOfRangeException(nameof(index));
            float cardWidth = Math.Max(1, (width - Gap) / 2);
            return new F5Rect(index % 2 * (cardWidth + Gap), HeaderHeight + HeaderGap + index / 2 * (CardHeight + Gap), cardWidth, CardHeight);
        }
        internal static F5Rect HeaderButton(float width, int index)
        {
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));
            float right = width - 10;
            if (index == 3) return new F5Rect(right - 54, 8, 54, 24);
            right -= 60;
            if (index == 2) return new F5Rect(right - 52, 8, 52, 24);
            right -= 58;
            if (index == 1) return new F5Rect(right - 52, 8, 52, 24);
            right -= 58;
            return new F5Rect(right - 64, 8, 64, 24);
        }
        internal static F5Rect Name(F5Rect card)
        {
            var action = Action(card, 0);
            return new F5Rect(card.X + 8, card.Y + 8, Math.Max(44, action.X - 4 - card.X - 8), 22);
        }
        // 0=place/save, 1=export, 2=delete, matching Z's right-to-left group.
        internal static F5Rect Action(F5Rect card, int index)
        {
            float width = Math.Min(38, Math.Max(28, (card.Width - 16 - 12) / 4));
            return new F5Rect(card.Right - 8 - width - (2 - index) * (width + 4), card.Y + 8, width, 22);
        }
        internal static F5Rect Preview(F5Rect card) { return new F5Rect(card.X + 8, card.Y + 36, Math.Max(1, card.Width - 16), PreviewHeight); }
        // Generate against the final physical drawing budget. Position/scroll do
        // not affect this identity; the renderer snaps a budget of this exact
        // size inside the container and never shrinks an already aggregated image.
        internal static Point PreviewSize(float width, Matrix matrix)
        {
            var rect = Preview(Card(width, 0));
            return new Point(Math.Max(1, Math.Min(BlueprintPreviewBuilder.MaximumWidth, (int)Math.Floor((rect.Width - 10) * Math.Abs(matrix.M11)))),
                Math.Max(1, Math.Min(BlueprintPreviewBuilder.MaximumHeight, (int)Math.Floor((rect.Height - 10) * Math.Abs(matrix.M22)))));
        }
        internal static F5Rect Materials(F5Rect card)
        {
            var p = Preview(card); float width = Math.Min(58, p.Width);
            return new F5Rect(p.Right - width, p.Bottom + 4, width, 22);
        }
        internal static F5Rect Intersect(F5Rect a, F5Rect b) { return F5HintLayout.Intersect(a, b); }
    }
}
