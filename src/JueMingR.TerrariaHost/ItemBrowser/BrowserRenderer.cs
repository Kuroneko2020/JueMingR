using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using JueMingR.TerrariaHost.Input;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal sealed class BrowserRenderer : IDisposable
    {
        private readonly ItemsRenderer surface = new ItemsRenderer();
        private readonly PreparedItemIcons icons = new PreparedItemIcons();
#if DEBUG
        internal long IconLoads { get { return icons.IconLoads; } }
        internal long VisibleSetBuilds { get { return icons.VisibleSetBuilds; } }
#endif
        internal int Generation { get { return surface.Generation; } }
        internal bool Refresh() { return surface.Refresh(); }
        internal void PrepareIcons(List<BrowserPart> parts, bool changed)
        {
            if (changed)
            {
                icons.BeginVisibleSet(); foreach (var part in parts) icons.AddVisible(part.Type); icons.EndVisibleSet();
            }
            // Stable frames only validate borrowed visible resources; no set,
            // array or texture load is recreated until its identity changes.
            icons.Prepare();
        }
        internal void Draw(BrowserPresentation page, Matrix matrix)
        {
            surface.Pass(matrix, page.View, () =>
            {
                surface.Divider(page.LocatorDivider);
                foreach (var part in page.Parts)
                {
                    if (part.Command == 19) { surface.ItemButton(part.Element.Rect, part.Enabled, part == page.Hovered); if (part.Selected) surface.Selection(part.Element.Rect); }
                    else if (part.Command != 0) surface.Button(part.Element, part.Selected, part.Enabled, false, part == page.Hovered);
                    else surface.Label(part.Element);
                    if (part.Type > 0)
                    {
                        var rect = part.Element.Rect; if (part.Command == 27) rect = new F5Rect(rect.X + 2, rect.Y + 2, rect.Height - 4, rect.Height - 4);
                        surface.PreparedItem(part.Type, icons.Get(part.Type), rect);
                    }
                }
                if (page.Editor != null)
                {
                    var view = page.EditView; var rect = page.EditRect;
                    var pixel = TextureAssets.MagicPixel.Value;
                    if (view.SelectionRight > view.SelectionLeft) Main.spriteBatch.Draw(pixel,
                        new Rectangle((int)(rect.X + 4 + view.SelectionLeft), (int)(rect.Y + 4), (int)(view.SelectionRight - view.SelectionLeft), (int)(rect.Height - 8)), new Rectangle(0, 0, 1, 1), new Color(70, 100, 145));
                    surface.Text(view.Text, new F5Rect(rect.X + 2, rect.Y, rect.Width - 4, rect.Height), Color.White);
                    Main.spriteBatch.Draw(pixel, new Rectangle((int)(rect.X + 4 + view.Caret), (int)rect.Y + 5, 1, (int)rect.Height - 10), new Rectangle(0, 0, 1, 1), Color.White);
                    Main.instance.SetIMEPanelAnchor(new Vector2(rect.X + 4 + view.Caret, rect.Bottom + 32), 0);
                }
                else if (page.HintLines.Count > 0)
                { surface.Panel(page.HintPanel); foreach (var line in page.HintLines) surface.Label(line); }
            });
        }
        public void Dispose() { icons.Clear(); surface.Dispose(); }
    }
}
