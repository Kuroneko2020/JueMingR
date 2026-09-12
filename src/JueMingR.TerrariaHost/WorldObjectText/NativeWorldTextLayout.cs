using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.UI.Chat;

namespace JueMingR.TerrariaHost.WorldObjectText
{
    // Parse, wrap and prepare on the game Update thread. Draw consumes only the
    // resulting native PositionedSnippets and never revisits source text.
    internal sealed class NativeWorldTextLayout
    {
        private readonly DynamicSpriteFont font;
        private readonly WorldTextCursor cursor;
        private readonly float scale, width, lineHeight;
        private readonly int lineLimit, characterLimit;
        private readonly List<PositionedSnippet> snippets = new List<PositionedSnippet>();
        private readonly List<bool> inheritedColor = new List<bool>();
        private readonly List<Unit> units = new List<Unit>();
        private readonly float[] inkLeft = new float[10], inkRight = new float[10];
        private int line, color = -1, visible;
        private float x;
        internal NativeWorldTextLayout(DynamicSpriteFont font, string text, WorldObjectStyle style, float width)
        {
            this.font = font ?? throw new ArgumentNullException(nameof(font)); scale = style.Size / 100f; this.width = Math.Max(32, width);
            lineHeight = Math.Max(font.LineSpacing, 24) * scale;
            lineLimit = style.Mode == WorldObjectMode.Lines ? style.Lines : 10;
            characterLimit = style.Mode == WorldObjectMode.Characters ? style.Characters : 8192;
            cursor = new WorldTextCursor(text, ValidItem);
            for (int i = 0; i < inkLeft.Length; i++) inkLeft[i] = Single.PositiveInfinity;
        }
        internal bool Ready { get; private set; }
        internal bool Truncated { get; private set; }
        internal int SourceWork { get; private set; }
        internal int VisibleUnits { get { return visible; } }
        internal int Lines { get { return line + 1; } }
        internal float Width { get; private set; }
        internal float Height { get { return (line + 1) * lineHeight; } }
        internal bool HasInk { get; private set; }
        internal IReadOnlyList<PositionedSnippet> Snippets { get { return snippets; } }
        internal int Step(int budget)
        {
            int consumed = 0;
            while (!Ready && budget - consumed >= 4)
            {
                WorldTextStep step = cursor.MoveNext(Math.Min(4096, budget - consumed));
                consumed += cursor.WorkUsed; SourceWork += cursor.WorkUsed;
                if (step == WorldTextStep.Pending) break;
                if (step == WorldTextStep.End) { Ready = true; break; }
                var element = cursor.Current;
                if (SourceWork > 65536) { FinishTruncated(); break; }
                if (element.IsNewLine)
                { if (line + 1 >= lineLimit) { FinishTruncated(); break; } line++; x = 0; continue; }
                if (visible == characterLimit) { FinishTruncated(); break; }
                var parts = Prepare(element); float unitWidth = 0;
                foreach (var part in parts) unitWidth += part.Width;
                if (x > 0 && x + unitWidth > width) { line++; x = 0; }
                if (line >= lineLimit) { line = lineLimit - 1; RestoreLineEnd(); FinishTruncated(); break; }
                if (unitWidth > width) { FinishTruncated(); break; }
                bool ink = element.ItemTag != null || !String.IsNullOrWhiteSpace(element.Text);
                var unit = new Unit { First = snippets.Count, X = x, Width = unitWidth, Line = line, PriorInkLeft = inkLeft[line], PriorInkRight = inkRight[line] };
                foreach (var part in parts)
                {
                    part.Snippet.CheckForHover = false; part.Snippet.UseRawColor = true;
                    snippets.Add(new PositionedSnippet(part.Snippet, snippets.Count, line, new Vector2(x, line * lineHeight), new Vector2(part.Width, lineHeight)));
                    inheritedColor.Add(part.Inherit); x += part.Width;
                }
                units.Add(unit); visible++; Width = Math.Max(Width, x);
                // No H/L-sized whitespace pre-pass. Ink is accumulated from the
                // already bounded visible unit, never the untouched source tail.
                if (ink) { inkLeft[line] = Math.Min(inkLeft[line], unit.X); inkRight[line] = x; HasInk = true; }
                if (cursor.ResourceLimited) { Truncated = true; Ready = true; }
            }
            return consumed;
        }
        private List<Part> Prepare(WorldTextElement element)
        {
            var result = new List<Part>();
            if (element.ItemTag != null)
            {
                var parsed = ChatManager.ParseMessage(element.ItemTag, Color.White);
                if (parsed.Count != 1 || !parsed[0].DeleteWhole) throw new InvalidOperationException("native-item-parser-unavailable");
                float previous = Main.inventoryScale;
                try
                {
                    Vector2 size;
                    if (!parsed[0].UniqueDraw(true, out size, Main.spriteBatch, Vector2.Zero, Color.White, scale)) throw new InvalidOperationException("native-item-measurement-unavailable");
                    result.Add(new Part { Snippet = parsed[0], Width = size.X, Inherit = false });
                }
                finally { Main.inventoryScale = previous; }
                return result;
            }
            foreach (var part in element.Parts)
            {
                TextSnippet snippet;
                if (part.Rgb < 0) snippet = new TextSnippet(part.Text, Color.White);
                else
                {
                    // Native regex has no safe trailing-backslash escape. Parse
                    // the verified color with a neutral payload, then use its
                    // native CopyMorph to attach this already-lexed literal unit.
                    string tag = "[c/" + part.Rgb.ToString("X6", CultureInfo.InvariantCulture) + ":x]";
                    var parsed = ChatManager.ParseMessage(tag, Color.White);
                    if (parsed.Count != 1 || parsed[0].Text != "x") throw new InvalidOperationException("native-color-parser-unavailable");
                    snippet = parsed[0].CopyMorph(part.Text);
                }
                result.Add(new Part { Snippet = snippet, Width = font.MeasureString(part.Text).X * scale, Inherit = part.Rgb < 0 });
            }
            return result;
        }
        private void RestoreLineEnd()
        { x = 0; for (int i = units.Count - 1; i >= 0; i--) if (units[i].Line == line) { x = units[i].X + units[i].Width; break; } }
        private void FinishTruncated()
        {
            if (!HasInk) { Truncated = true; Ready = true; return; }
            float ellipsis = font.MeasureString("…").X * scale;
            while (x + ellipsis > width && units.Count != 0 && units[units.Count - 1].Line == line)
            {
                var last = units[units.Count - 1]; x = last.X;
                snippets.RemoveRange(last.First, snippets.Count - last.First); inheritedColor.RemoveRange(last.First, inheritedColor.Count - last.First); units.RemoveAt(units.Count - 1);
                inkLeft[line] = last.PriorInkLeft; inkRight[line] = last.PriorInkRight; visible--;
            }
            if (ellipsis <= width)
            {
                var snippet = new TextSnippet("…", Color.White) { CheckForHover = false, UseRawColor = true };
                snippets.Add(new PositionedSnippet(snippet, snippets.Count, line, new Vector2(x, line * lineHeight), new Vector2(ellipsis, lineHeight)));
                inheritedColor.Add(true); Width = Math.Max(Width, x + ellipsis); HasInk = true;
                inkLeft[line] = Math.Min(inkLeft[line], x); inkRight[line] = x + ellipsis;
            }
            Truncated = true; Ready = true;
        }
        internal void ApplyColor(int rgb)
        {
            if (color == rgb) return; color = rgb; Color value = new Color((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            for (int i = 0; i < snippets.Count; i++) if (inheritedColor[i]) snippets[i].Snippet.Color = value;
        }
        internal void Draw(SpriteBatch batch, Vector2 position)
        {
            float previous = Main.inventoryScale;
            try
            {
                ChatManager.DrawColorCodedStringShadow(batch, font, snippets, position, Color.Black, 0, Vector2.Zero, new Vector2(scale), 1.5f);
                int hover; ChatManager.DrawColorCodedString(batch, font, snippets, position, 0, Vector2.Zero, new Vector2(scale), out hover, null);
            }
            finally { Main.inventoryScale = previous; }
        }
        internal bool HasVisibleInk(Vector2 position, Matrix zoom, int screenWidth, int screenHeight)
        {
            // Blank lines affect anchoring/wrapping but cannot spend a display
            // slot. At most ten prepared line bounds, with the native shadow.
            for (int i = 0; i <= line; i++)
            {
                if (Single.IsPositiveInfinity(inkLeft[i])) continue;
                var first = Vector2.Transform(position + new Vector2(inkLeft[i] - 2, i * lineHeight - 2), zoom);
                var last = Vector2.Transform(position + new Vector2(inkRight[i] + 2, (i + 1) * lineHeight + 2), zoom);
                if (last.X >= 0 && last.Y >= 0 && first.X <= screenWidth && first.Y <= screenHeight) return true;
            }
            return false;
        }
        private static bool ValidItem(int id) { return id > 0 && id < Terraria.ID.ItemID.Count || id < 0 && id >= -48; }
        private struct Part { internal TextSnippet Snippet; internal float Width; internal bool Inherit; }
        private struct Unit { internal int First, Line; internal float X, Width, PriorInkLeft, PriorInkRight; }
    }
}
