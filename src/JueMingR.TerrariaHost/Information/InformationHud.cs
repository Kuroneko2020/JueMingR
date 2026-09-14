using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Platform.Information;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;

namespace JueMingR.TerrariaHost.Information
{
    // Four independent text layouts, one UI anchor. Draw consumes this prepared
    // projection only: it never samples the world or advances an input gesture.
    internal sealed class InformationHud
    {
        private readonly HostInformation host;
        private readonly UiTextMetrics metrics = new UiTextMetrics();
        private readonly Block[] blocks = { new Block(), new Block(), new Block(), new Block() };
        private readonly Block empty = new Block();
        private DynamicSpriteFont font;
        private float viewportWidth, viewportHeight;
        private bool placeholder;
        internal F5Rect Bounds { get; private set; }
        internal bool Visible { get; private set; }
        internal long GeometryVersion { get; private set; }
#if DEBUG
        internal int Measurements { get; private set; }
        internal int LayoutBuilds { get; private set; }
        internal int Draws { get; private set; }
#endif
        internal InformationHud(HostInformation host) { this.host = host; }
        internal void Clear() { Visible = false; Bounds = default(F5Rect); GeometryVersion++; }
        internal void Prepare(DynamicSpriteFont currentFont, float width, float height, bool adjusting, WindowPosition draft = null)
        {
            if (!adjusting && !host.Enabled(InformationKind.Biome) && !host.Preferences.Value.AnySummaryEnabled)
            { if (Visible) Clear(); return; }
            if (currentFont == null || width < 24 || height < 24 || Single.IsNaN(width) || Single.IsNaN(height) || Single.IsInfinity(width) || Single.IsInfinity(height))
            { Clear(); return; }
            float maximumWidth = Math.Max(12, Math.Min(720, width - 24));
            bool geometryChanged = !ReferenceEquals(font, currentFont) || width != viewportWidth || height != viewportHeight;
            font = currentFont; viewportWidth = width; viewportHeight = height;
            float primaryHeight = 0; int active = 0;
            for (int i = 0; i < 4; i++)
            {
                Block block = blocks[i]; string text = host.Text((InformationKind)i);
                var style = host.Preferences.Value.Style((InformationKind)i); block.Rgb = style.Rgb;
                if (block.Prepare(text, style.Size / 100f, font, maximumWidth, Measure))
                {
                    geometryChanged = true;
#if DEBUG
                    LayoutBuilds++;
#endif
                }
                if (block.Lines.Count > 0) { primaryHeight += block.LineHeight + 4; active++; }
            }
            placeholder = active == 0 && adjusting;
            if (placeholder)
            {
                empty.Rgb = 0xFAFAD2;
                if (empty.Prepare("信息窗", 0.82f, font, maximumWidth, Measure)) geometryChanged = true;
                primaryHeight = empty.LineHeight + 4; active = 1;
            }
            if (geometryChanged) GeometryVersion++;
            if (active == 0) { Visible = false; Bounds = default(F5Rect); return; }
            float remaining = Math.Max(0, height - 16 - primaryHeight), y = 4, usedWidth = 0;
            for (int i = 0; i < (placeholder ? 1 : 4); i++)
            {
                Block block = placeholder ? empty : blocks[i]; block.OffsetY = y;
                block.VisibleLines = block.Lines.Count == 0 ? 0 : Math.Min(block.Lines.Count, 1 + (int)(remaining / block.LineHeight));
                remaining -= Math.Max(0, block.VisibleLines - 1) * block.LineHeight;
                if (block.VisibleLines > 0) y += block.VisibleLines * block.LineHeight + 4;
                for (int j = 0; j < block.VisibleLines; j++) usedWidth = Math.Max(usedWidth, block.Lines[j].Size.Width * block.Scale);
            }
            float hudWidth = Math.Min(width, usedWidth + 8), hudHeight = Math.Min(height, y);
            WindowPosition intent = draft ?? host.Position.Value;
            float x = intent == null ? 20 : intent.X, top = intent == null ? height * 0.45f : intent.Y;
            Bounds = new F5Rect(Math.Max(0, Math.Min(width - hudWidth, x)), Math.Max(0, Math.Min(height - hudHeight, top)), hudWidth, hudHeight);
            Visible = true;
        }
        private F5Size Measure(string text)
        {
#if DEBUG
            Measurements++;
#endif
            return metrics.Measure(font, text);
        }
        internal void Project(WindowPosition draft)
        {
            if (!Visible) return;
            float x = draft == null ? 20 : draft.X, y = draft == null ? viewportHeight * 0.45f : draft.Y;
            Bounds = new F5Rect(Math.Max(0, Math.Min(viewportWidth - Bounds.Width, x)), Math.Max(0, Math.Min(viewportHeight - Bounds.Height, y)), Bounds.Width, Bounds.Height);
        }
        internal void Draw(SpriteBatch batch, bool adjusting)
        {
            if (!Visible || batch == null || font == null) return;
#if DEBUG
            Draws++;
#endif
            for (int i = 0; i < (placeholder ? 1 : 4); i++)
            {
                Block block = placeholder ? empty : blocks[i]; var rgb = block.Rgb;
                var color = new Color((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                for (int j = 0; j < block.VisibleLines; j++)
                {
                    Line line = block.Lines[j];
                    if (j > 0 && j == block.VisibleLines - 1 && block.VisibleLines < block.Lines.Count) line = block.Ellipsis;
                    var point = new Vector2(Bounds.X + 4 - line.Size.OffsetX * block.Scale, Bounds.Y + block.OffsetY + j * block.LineHeight - line.Size.OffsetY * block.Scale);
                    Terraria.Utils.DrawBorderStringFourWay(batch, font, line.Text, point.X, point.Y, color, Color.Black, Vector2.Zero, block.Scale);
                }
            }
            if (adjusting)
            {
                var pixel = Terraria.GameContent.TextureAssets.MagicPixel?.Value;
                if (pixel != null && !pixel.IsDisposed)
                {
                    var c = Color.Gold * 0.8f;
                    batch.Draw(pixel, new Rectangle((int)Bounds.X, (int)Bounds.Y, (int)Bounds.Width, 1), c);
                    batch.Draw(pixel, new Rectangle((int)Bounds.X, (int)Bounds.Bottom - 1, (int)Bounds.Width, 1), c);
                    batch.Draw(pixel, new Rectangle((int)Bounds.X, (int)Bounds.Y, 1, (int)Bounds.Height), c);
                    batch.Draw(pixel, new Rectangle((int)Bounds.Right - 1, (int)Bounds.Y, 1, (int)Bounds.Height), c);
                }
            }
        }
        private struct Line
        {
            internal string Text; internal F5Size Size;
            internal Line(string text, F5Size size) { Text = text; Size = size; }
        }
        private sealed class Block
        {
            internal readonly List<Line> Lines = new List<Line>();
            internal float Scale, LineHeight, OffsetY;
            internal int Rgb, VisibleLines;
            internal Line Ellipsis;
            private string text;
            private object font;
            private float width;
            internal bool Prepare(string value, float scale, DynamicSpriteFont currentFont, float maximumWidth, Func<string, F5Size> measure)
            {
                if (String.Equals(text, value, StringComparison.Ordinal) && scale == Scale && ReferenceEquals(font, currentFont) && width == maximumWidth) return false;
                text = value; Scale = scale; font = currentFont; width = maximumWidth; Lines.Clear();
                if (String.IsNullOrEmpty(value)) return true;
                Ellipsis = new Line("…", measure("…"));
                LineHeight = Math.Max(1, currentFont.LineSpacing * scale + 2);
                // Binary-search complete text-element boundaries using actual
                // glyph geometry. No UTF-16 count is used as a visual width.
                string bounded = value.Length > 8192 ? value.Substring(0, 8192) + "…" : value;
                foreach (string paragraph in bounded.Split('\n'))
                {
                    int[] boundaries = StringInfo.ParseCombiningCharacters(paragraph); int start = 0;
                    if (boundaries.Length == 0) { Lines.Add(new Line("", measure(""))); continue; }
                    while (start < boundaries.Length && Lines.Count < 48)
                    {
                        int lo = start + 1, hi = boundaries.Length, best = start;
                        F5Size bestSize = default(F5Size);
                        while (lo <= hi)
                        {
                            int mid = (lo + hi) / 2, end = mid == boundaries.Length ? paragraph.Length : boundaries[mid];
                            F5Size size = measure(paragraph.Substring(boundaries[start], end - boundaries[start]));
                            if (size.Width * scale <= maximumWidth) { best = mid; bestSize = size; lo = mid + 1; } else hi = mid - 1;
                        }
                        if (best == start) { Lines.Add(Ellipsis); start++; continue; }
                        int finish = best == boundaries.Length ? paragraph.Length : boundaries[best];
                        Lines.Add(new Line(paragraph.Substring(boundaries[start], finish - boundaries[start]), bestSize));
                        LineHeight = Math.Max(LineHeight, bestSize.Height * scale + 2); start = best;
                    }
                    if (Lines.Count >= 48) { Lines[Lines.Count - 1] = Ellipsis; break; }
                }
            return true;
            }
        }
    }
}
