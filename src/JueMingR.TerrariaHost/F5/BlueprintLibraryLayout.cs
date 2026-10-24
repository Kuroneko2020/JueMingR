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
        internal static F5Rect CaptureHeader(float width, float header = HeaderHeight) { return new F5Rect(0, 0, width, header); }
        internal static F5Rect CaptureBack(float width, float buttonWidth, float header = HeaderHeight, float buttonHeight = 28)
        { return new F5Rect(width - 10 - buttonWidth, (header - buttonHeight) / 2, buttonWidth, buttonHeight); }
        internal static F5Rect CaptureSave(float width, float saveWidth, float backWidth, float header = HeaderHeight, float buttonHeight = 28)
        { return new F5Rect(CaptureBack(width, backWidth, header, buttonHeight).X - 6 - saveWidth, (header - buttonHeight) / 2, saveWidth, buttonHeight); }
        internal static F5Rect CaptureTitle(float width, float saveWidth, float backWidth, float header = HeaderHeight)
        { return new F5Rect(10, 0, Math.Max(1, CaptureSave(width, saveWidth, backWidth, header).X - 20), header); }
        internal static F5Rect CaptureNameRow(float width, float header = HeaderHeight) { return new F5Rect(0, header + HeaderGap, width, 44); }
        internal static F5Rect CaptureName(float width, float header = HeaderHeight)
        { var row = CaptureNameRow(width, header); float left = Math.Min(104, Math.Max(76, width * .25f)); return new F5Rect(left, row.Y + 6, Math.Max(1, width - left - 8), 32); }
        internal static F5Rect CaptureNameLabel(float width, float header = HeaderHeight)
        { var row = CaptureNameRow(width, header); var field = CaptureName(width, header); return new F5Rect(10, row.Y, Math.Max(1, field.X - 18), row.Height); }
        internal static F5Rect CapturePreview(float width, float height, float header = HeaderHeight)
        {
            // Header, standard name row and preview share the geometry used by
            // editing, IME, pixel-budget identity and the shell's single scroll.
            float top = CaptureNameRow(width, header).Bottom + 8;
            return new F5Rect(8, top, Math.Max(1, width - 16), Math.Max(48, Math.Min(350, height - top - 30)));
        }
        internal static Point CapturePreviewSize(float width, float height, Matrix matrix)
        { return CapturePreviewSizeForHeader(width, height, matrix, HeaderHeight); }
        internal static Point CapturePreviewSizeForHeader(float width, float height, Matrix matrix, float header)
        {
            var rect = CapturePreview(width, height, header);
            return new Point(Math.Max(1, Math.Min(BlueprintPreviewBudget.DraftWidth, (int)Math.Floor((rect.Width - 10) * Math.Abs(matrix.M11)))),
                Math.Max(1, Math.Min(BlueprintPreviewBudget.DraftHeight, (int)Math.Floor((rect.Height - 10) * Math.Abs(matrix.M22)))));
        }
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
