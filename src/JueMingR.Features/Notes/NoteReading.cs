using System;

namespace JueMingR.Features.Notes
{
    // Persisted preference, independent of viewport projection and F5 scale.
    // One current value per pin lifetime, never a global default or history.
    public sealed class NoteReading
    {
        public const int MinimumWidth = 240, MaximumWidth = 560, WidthStep = 40;
        public const int MinimumFont = 80, MaximumFont = 180, FontStep = 10;
        public static readonly NoteReading Default = new NoteReading(280, 120);
        public NoteReading(int width, int fontPercent)
        {
            if (width < MinimumWidth || width > MaximumWidth || fontPercent < MinimumFont || fontPercent > MaximumFont)
                throw new ArgumentException("invalid-note-reading");
            Width = width; FontPercent = fontPercent;
        }
        public int Width { get; }
        public int Height { get { return (Width * 304 + 140) / 280; } }
        public int FontPercent { get; }
        public bool Same(NoteReading other) { return other != null && Width == other.Width && FontPercent == other.FontPercent; }
        public NoteReading Adjust(bool area, int steps)
        {
            return area ? new NoteReading((int)Math.Max(MinimumWidth, Math.Min(MaximumWidth, (long)Width + (long)steps * WidthStep)), FontPercent)
                : new NoteReading(Width, (int)Math.Max(MinimumFont, Math.Min(MaximumFont, (long)FontPercent + (long)steps * FontStep)));
        }
    }
}
