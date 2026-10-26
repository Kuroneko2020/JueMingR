using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class BlueprintMaterialsRenderer : IDisposable
    {
        private readonly ItemsRenderer surface = new ItemsRenderer();
        private readonly PreparedItemIcons icons = new PreparedItemIcons();
        private readonly F5HintLayout hint = new F5HintLayout();
        private readonly List<F5Element> hintLines = new List<F5Element>();
        private string hintValue;
        private object hintFont;
        private F5Rect hintPanel;
        private int preparedProjection = -1;
        internal int Generation { get { return surface.Generation; } }
        internal bool ResourcesCurrent { get { return surface.ResourcesCurrent; } }
#if DEBUG
        internal long IconLoads { get { return icons.IconLoads; } }
        internal long VisibleSetBuilds { get { return icons.VisibleSetBuilds; } }
#endif
        internal bool Refresh() { return surface.Refresh(); }
        internal void Prepare(BlueprintMaterialsLayout layout, int projection)
        {
            if (projection != preparedProjection)
            {
                icons.BeginVisibleSet(); foreach (var row in layout.Visible) icons.AddVisible(row.Cell.ItemType); icons.EndVisibleSet();
                preparedProjection = projection;
            }
            icons.Prepare();
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
                    if (!layout.Dynamic) surface.ItemButton(row.Rect, true, false);
                    else surface.Divider(new F5Rect(row.Rect.X, row.Rect.Bottom, row.Rect.Width, 1));
                    surface.PreparedItem(row.Cell.ItemType, icons.Get(row.Cell.ItemType), row.Icon);
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
        public void Dispose() { icons.Clear(); hint.Clear(); hintLines.Clear(); hintValue = null; hintFont = null; preparedProjection = -1; surface.Dispose(); }
    }
}
