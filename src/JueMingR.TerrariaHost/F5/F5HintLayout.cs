using System;
using System.Collections.Generic;

namespace JueMingR.TerrariaHost.F5
{
    // One current layout, including dynamic reasons. It is deliberately not a
    // dictionary: visiting errors must not grow the fixed page text cache.
    internal sealed class F5HintLayout
    {
        internal const float Scale = .65f;
        private readonly List<F5Element> lines = new List<F5Element>();
        private string text;
        private object font;
        private float width, height;
        internal IList<F5Element> Lines { get { return lines; } }
        internal F5Rect Panel { get; private set; }
        internal bool Visible { get; private set; }
#if DEBUG
        internal int BuildCount { get; private set; }
#endif
        internal void Hide() { Visible = false; }
        internal void Clear() { Hide(); text = null; font = null; lines.Clear(); }
        internal void Prepare(string value, F5Rect target, F5Rect bounds, object fontIdentity, Func<string, float, F5Size> measure)
        {
            Hide();
            if (string.IsNullOrEmpty(value) || bounds.Width <= 16 || bounds.Height <= 16) return;
            if (text != value || !ReferenceEquals(font, fontIdentity) || width != bounds.Width || height != bounds.Height)
            {
                lines.Clear();
                Wrap(value, Math.Min(380, bounds.Width - 16), measure);
                if (ContentHeight + 16 > bounds.Height)
                { lines.Clear(); Wrap(value, bounds.Width - 16, measure); }
                if (ContentHeight + 16 > bounds.Height)
                    throw new InvalidOperationException("F5 hint cannot fit its readable viewport.");
                text = value; font = fontIdentity; width = bounds.Width; height = bounds.Height;
#if DEBUG
                BuildCount++;
#endif
            }
            float w = 0; foreach (var line in lines) w = Math.Max(w, line.Rect.Width);
            Panel = Place(target, bounds, w + 16, ContentHeight + 16);
            Visible = true;
        }
        private float ContentHeight { get { return lines.Count == 0 ? 0 : lines[lines.Count - 1].Rect.Bottom; } }
        private void Wrap(string value, float available, Func<string, float, F5Size> measure)
        {
            float y = 0;
            foreach (string paragraph in value.Replace("\r", "").Split('\n'))
            {
                int start = 0;
                while (start < paragraph.Length)
                {
                    int count = paragraph.Length - start;
                    string line = paragraph.Substring(start, count); F5Size size = measure(line, Scale);
                    while (size.Width > available && count > 1)
                    {
                        count--;
                        if (char.IsHighSurrogate(paragraph[start + count - 1]) && count > 1) count--;
                        line = paragraph.Substring(start, count); size = measure(line, Scale);
                    }
                    if (size.Width > available) throw new InvalidOperationException("F5 hint glyph exceeds readable width.");
                    lines.Add(new F5Element(F5ElementKind.Text, new F5Rect(0, y, size.Width, size.Height), line, size, Scale, F5Command.None));
                    y += size.Height + 2; start += count;
                }
            }
        }
        internal static F5Rect Place(F5Rect target, F5Rect bounds, float width, float height)
        {
            float x = Math.Max(bounds.X, Math.Min(bounds.Right - width, target.X));
            if (target.Y - height - 6 >= bounds.Y) return new F5Rect(x, target.Y - height - 6, width, height);
            if (target.Bottom + height + 6 <= bounds.Bottom) return new F5Rect(x, target.Bottom + 6, width, height);
            float y = Math.Max(bounds.Y, Math.Min(bounds.Bottom - height, target.Y));
            if (target.Right + width + 6 <= bounds.Right) return new F5Rect(target.Right + 6, y, width, height);
            if (target.X - width - 6 >= bounds.X) return new F5Rect(target.X - width - 6, y, width, height);
            return new F5Rect(x, Math.Max(bounds.Y, Math.Min(bounds.Bottom - height, target.Bottom + 6)), width, height);
        }
        internal static F5Rect Intersect(F5Rect a, F5Rect b)
        {
            float x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y);
            return new F5Rect(x, y, Math.Max(0, Math.Min(a.Right, b.Right) - x), Math.Max(0, Math.Min(a.Bottom, b.Bottom) - y));
        }
        internal static F5Element HitName(IList<F5Element> elements, F5Rect view, float dx, float dy, float x, float y, out F5Rect target)
        {
            target = default(F5Rect);
            if (!view.Contains(x, y)) return null;
            foreach (var element in elements)
            {
                if (element.Description == null) continue;
                // Padding eases pointing only while some actual name text is
                // still drawn. It cannot resurrect a completely cropped row.
                var text = Intersect(element.Rect.Offset(dx, dy), view);
                if (text.Width <= 0 || text.Height <= 0) continue;
                var visible = Intersect(element.HintRect.Offset(dx, dy), view);
                if (!visible.Contains(x, y)) continue;
                target = visible; return element;
            }
            return null;
        }
    }
}
