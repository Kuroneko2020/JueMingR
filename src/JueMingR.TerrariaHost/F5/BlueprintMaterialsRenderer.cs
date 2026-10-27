using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class BlueprintMaterialsRenderer : IDisposable
    {
        private sealed class CaptureIcon
        { internal BlueprintMaterialDrawing Row; internal Texture2D Texture; internal Rectangle Frame; internal Vector2 Center; internal float Scale; }
        private readonly ItemsRenderer surface = new ItemsRenderer();
        private readonly PreparedItemIcons icons = new PreparedItemIcons();
        private readonly F5HintLayout hint = new F5HintLayout();
        private readonly List<F5Element> hintLines = new List<F5Element>();
        private string hintValue;
        private object hintFont;
        private F5Rect hintPanel;
        private int preparedProjection = -1;
        private readonly List<int> preparedItems = new List<int>();
        private readonly Dictionary<int, CaptureIcon> captureIcons = new Dictionary<int, CaptureIcon>();
        internal int Generation { get { return surface.Generation; } }
        internal bool ResourcesCurrent { get { return surface.ResourcesCurrent; } }
#if DEBUG
        internal long IconLoads { get { return icons.IconLoads; } }
        internal long VisibleSetBuilds { get { return icons.VisibleSetBuilds; } }
        internal long CaptureIconProjects { get; private set; }
#endif
        internal bool Refresh() { return surface.Refresh(); }
        internal void Prepare(BlueprintMaterialsLayout layout, int projection)
        {
            if (projection != preparedProjection)
            {
                bool changed = preparedItems.Count != layout.Visible.Count;
                if (!changed) for (int i = 0; i < preparedItems.Count; i++)
                    if (preparedItems[i] != layout.Visible[i].Cell.ItemType) { changed = true; break; }
                if (changed)
                {
                    captureIcons.Clear();
                    preparedItems.Clear(); icons.BeginVisibleSet();
                    foreach (var row in layout.Visible) { preparedItems.Add(row.Cell.ItemType); icons.AddVisible(row.Cell.ItemType); }
                    icons.EndVisibleSet();
                }
                preparedProjection = projection;
            }
            icons.Prepare();
            if (!layout.Dynamic && !layout.ShowNames)
                foreach (var row in layout.Visible) PrepareCaptureIcon(row);
            else captureIcons.Clear();
        }
        private void PrepareCaptureIcon(BlueprintMaterialDrawing row)
        {
            var texture = icons.Get(row.Cell.ItemType);
            if (texture == null || texture.IsDisposed) { captureIcons.Remove(row.Cell.ItemType); return; }
            Rectangle frame = Main.itemAnimations[row.Cell.ItemType] == null ? texture.Bounds : Main.itemAnimations[row.Cell.ItemType].GetFrame(texture);
            CaptureIcon prior;
            if (captureIcons.TryGetValue(row.Cell.ItemType, out prior) && ReferenceEquals(prior.Row, row) && ReferenceEquals(prior.Texture, texture) && prior.Frame == frame) return;
            var box = row.Cell.Icon.Offset(row.Rect.X - row.Cell.Rect.X, row.Rect.Y - row.Cell.Rect.Y);
            float scale = Math.Min(1, Math.Min((box.Width - 4) / frame.Width, (box.Height - 4) / frame.Height));
            var center = new Vector2(box.X + box.Width / 2, box.Y + box.Height / 2);
            // Match the native inventory's current texture frame and 32-unit
            // size limit, not a count anchored to the old whole-cell footer.
            // Frame and pose are prepared together; Draw neither loads textures
            // nor rereads a different animation frame halfway through this UI.
            row.Icon = new F5Rect(center.X - frame.Width * scale / 2, center.Y - frame.Height * scale / 2, frame.Width * scale, frame.Height * scale);
            var lines = row.Cell.RequiredLines;
            float width = 0; foreach (var line in lines) width = Math.Max(width, line.TextSize.Width);
            bool below = lines.Count > 1;
            float right = below ? center.X + width / 2 : Math.Max(row.Rect.X + 4 + width, Math.Min(row.Rect.Right - 4, row.Icon.Right + 2));
            float top = below ? row.Icon.Bottom + 3 : row.Icon.Bottom + 2 - BlueprintMaterialsLayout.BlockHeight(lines, 2);
            row.Glyphs.Clear();
            foreach (var line in lines)
                row.Glyphs.Add(new BlueprintMaterialGlyph { Element = new F5Element(F5ElementKind.Text,
                    new F5Rect(right - line.TextSize.Width, top + line.Rect.Y, line.TextSize.Width, line.TextSize.Height), line.Text, line.TextSize, line.TextScale, line.Command), Ink = Color.White });
            captureIcons[row.Cell.ItemType] = new CaptureIcon { Row = row, Texture = texture, Frame = frame, Center = center, Scale = scale };
#if DEBUG
            CaptureIconProjects++;
#endif
        }
        internal void PrepareHint(string value, F5Rect target, F5Rect bounds, object font, Func<string, float, F5Size> measure)
        {
            if (value == null) { hint.Hide(); return; }
            hint.Prepare(value, target, bounds, font, measure);
            if (hintValue == value && ReferenceEquals(hintFont, font) && F5PopupPlacement.SameAnchor(hintPanel, hint.Panel)) return;
            hintValue = value; hintFont = font; hintPanel = hint.Panel; hintLines.Clear();
            foreach (var line in hint.Lines) hintLines.Add(new F5Element(line.Kind, line.Rect.Offset(hint.Panel.X + 8, hint.Panel.Y + 8), line.Text, line.TextSize, line.TextScale, line.Command));
        }
        internal void DrawContent(BlueprintMaterialsLayout layout, Matrix matrix, F5Rect clip)
        {
            if (clip.Width <= 0 || clip.Height <= 0) return;
            surface.Pass(matrix, clip, () =>
            {
                foreach (var row in layout.Visible)
                {
                    bool capture = !layout.Dynamic && !layout.ShowNames;
                    if (!layout.Dynamic && !capture) surface.ItemButton(row.Rect, true, false);
                    else if (layout.Dynamic) surface.Divider(new F5Rect(row.Rect.X, row.Rect.Bottom, row.Rect.Width, 1));
                    CaptureIcon icon;
                    if (capture && captureIcons.TryGetValue(row.Cell.ItemType, out icon) && ReferenceEquals(icon.Row, row) && !icon.Texture.IsDisposed)
                        Main.spriteBatch.Draw(icon.Texture, icon.Center, icon.Frame, Color.White, 0, new Vector2(icon.Frame.Width / 2f, icon.Frame.Height / 2f), icon.Scale, SpriteEffects.None, 0);
                    else surface.PreparedItem(row.Cell.ItemType, icons.Get(row.Cell.ItemType), row.Icon);
                    foreach (var glyph in row.Glyphs) surface.Label(glyph.Element, glyph.Ink);
                }
            });
        }
        internal void DrawPanel(Matrix matrix, F5Rect panel, IList<F5Element> labels, IList<F5Element> buttons,
            F5Element hovered, F5Element armed)
        {
            surface.Pass(matrix, panel, () =>
            {
                surface.Panel(panel);
                foreach (var button in buttons) surface.Button(button, false, true, false, ReferenceEquals(button, hovered), ReferenceEquals(button, armed));
                foreach (var label in labels) surface.Label(label);
            });
        }
        internal void DrawScope(Matrix matrix, F5Rect panel, IList<F5Element> rows, F5Element hovered)
        {
            surface.Pass(matrix, panel, () =>
            { surface.Panel(panel); foreach (var row in rows) surface.Button(row, false, true, false, ReferenceEquals(row, hovered)); });
        }
        internal void DrawHint(Matrix matrix, F5Rect bounds)
        {
            if (!hint.Visible) return;
            surface.Pass(matrix, bounds, () =>
            {
                surface.Panel(hint.Panel);
                foreach (var line in hintLines) surface.Label(line);
            });
        }
        public void Dispose() { icons.Clear(); preparedItems.Clear(); captureIcons.Clear(); hint.Clear(); hintLines.Clear(); hintValue = null; hintFont = null; preparedProjection = -1; surface.Dispose(); }
    }
}
