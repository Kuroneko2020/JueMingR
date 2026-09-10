using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class F5Renderer : IDisposable
    {
        private DynamicSpriteFont font;
        private Texture2D background, row, button, pixel;
        private readonly Func<string, F5Size> measure;
        private readonly UiTextMetrics textMetrics = new UiTextMetrics();
        private RasterizerState clipped;
        private Texture2D roundCap;
        private readonly F5IconAtlas icons = new F5IconAtlas();
        internal int SkinGeneration { get; private set; }
        internal object FontIdentity { get { return font; } }
        internal F5Size PopupMeasure(string text, float scale)
        { F5Size size = textMetrics.Measure(font, text); return new F5Size(size.Width * scale, size.Height * scale, size.OffsetX * scale, size.OffsetY * scale); }
        internal void DrawPopup(Hotkeys.HotkeyPopup popup)
        {
            if (!popup.Visible) return;
            var layout = popup.Layout; SpriteBatch batch = Main.spriteBatch;
            Panel(batch, layout.Panel, background, Color.White, true);
            for (int i = 0; i < layout.Text.Count; i++)
            {
                var line = layout.Text[i]; var role = layout.Roles[i];
                Color color = role == Hotkeys.HotkeyTextRole.Success ? Color.LightGreen : role == Hotkeys.HotkeyTextRole.Warning ? Color.LightGoldenrodYellow :
                    role == Hotkeys.HotkeyTextRole.Error ? Color.LightCoral : role == Hotkeys.HotkeyTextRole.Muted ? Color.LightGray : Color.White;
                Text(batch, line.Text, new Vector2(layout.Panel.X + line.Rect.X, layout.Panel.Y + line.Rect.Y), line.TextScale, color, line.TextSize);
            }
            foreach (var cap in layout.Keycaps)
            {
                var rect = cap.Rect.Offset(layout.Panel.X, layout.Panel.Y);
                // Flat labels reuse R's surface; no button bevel, hover or action.
                Panel(batch, rect, row, new Color(210, 210, 210), true);
                var label = F5Layout.ButtonLabel(cap).Offset(layout.Panel.X, layout.Panel.Y);
                Text(batch, cap.Text, new Vector2(label.X, label.Y), cap.TextScale, Color.White, cap.TextSize);
            }
            PopupRule(batch, layout.Panel.X + 12, layout.Panel.Y + layout.HeaderBottom, layout.Panel.Width - 24);
            PopupRule(batch, layout.Panel.X + 12, layout.Panel.Y + layout.FooterTop, layout.Panel.Width - 24);
            for (int i = 0; i < layout.Buttons.Count; i++)
            {
                var command = layout.Commands[i];
                F5ControlRenderer.Button(batch, pixel, button, font, layout.Buttons[i], popup.Hovered == command, layout.Enabled[i],
                    command == Hotkeys.HotkeyPopupCommand.Record && layout.Enabled[i] ? (Color?)Color.LightGray : null,
                    layout.Panel.X, layout.Panel.Y, true, popup.Pressed == command && popup.Hovered == command);
            }
            if (popup.HelpVisible)
            {
                Panel(batch, layout.HelpPanel, background, Color.White, true);
                foreach (var line in layout.HelpText)
                    Text(batch, line.Text, new Vector2(layout.HelpPanel.X + line.Rect.X, layout.HelpPanel.Y + line.Rect.Y), line.TextScale, Color.White, line.TextSize);
            }
        }
        private void PopupRule(SpriteBatch batch, float x, float y, float width)
        { batch.Draw(pixel, new Vector2(x, y), new Rectangle(0, 0, 1, 1), Color.White * .25f, 0, Vector2.Zero, new Vector2(width, 1), SpriteEffects.None, 0); }

        internal F5Renderer() { measure = Measure; }

        internal bool RefreshResources()
        {
            font = FontAssets.MouseText == null ? null : FontAssets.MouseText.Value;
            Texture2D nextButton = TextureAssets.InventoryBack == null ? null : TextureAssets.InventoryBack.Value;
            // InventoryBack already carries the current skin's color. SettingsPanel
            // is a horizontal strip; InventoryBack13 requires a vanilla blue tint.
            Texture2D nextBackground = nextButton, nextRow = nextButton;
            Texture2D nextPixel = TextureAssets.MagicPixel == null ? null : TextureAssets.MagicPixel.Value;
            if (!ReferenceEquals(background, nextBackground) || !ReferenceEquals(row, nextRow) ||
                !ReferenceEquals(button, nextButton) || !ReferenceEquals(pixel, nextPixel)) SkinGeneration++;
            background = nextBackground; row = nextRow; button = nextButton; pixel = nextPixel;
            return font != null && pixel != null && !pixel.IsDisposed;
        }

        internal void Prepare(F5Interaction state, float width, float height, float scale)
        {
            state.Layout.Ensure(width, height, scale, state.Page, font, measure);
            // Dynamic pages clamp after committing their real content height.
            // Ensure's temporary empty height must not reset their offset.
            if (state.Page != 0 && state.Page != 4) state.ClampScroll();
        }

        private F5Size Measure(string text)
        { return textMetrics.Measure(font, text); }

        internal void Draw(F5Interaction state, Matrix matrix, bool biomeEnabled, bool biomeFailed)
        {
            SpriteBatch batch = Main.spriteBatch;
            if (batch == null) throw new InvalidOperationException("F5 SpriteBatch is unavailable.");
            GraphicsDevice device = batch.GraphicsDevice;
            icons.Ensure(device);
            EnsureRoundCap(device);
            F5Layout layout = state.Layout;
            Matrix layerMatrix = Main.UIScaleMatrix;
            // GameInterfaceLayer has begun this batch. Restore both its batch
            // contract and the actual graphics state even if a content draw fails.
            Rectangle oldScissor = device.ScissorRectangle;
            RasterizerState oldRasterizer = device.RasterizerState;
            BlendState oldBlend = device.BlendState;
            DepthStencilState oldDepth = device.DepthStencilState;
            SamplerState oldSampler = device.SamplerStates[0];
            if (clipped == null) clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            bool contentBatch = false;
            batch.End();
            try
            {
                batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, matrix);
                contentBatch = true;
                DrawChrome(batch, state);
                batch.End();
                contentBatch = false;
                F5Rect view = layout.Viewport.Offset(state.X, state.Y);
                Vector2 top = Vector2.Transform(new Vector2(view.X, view.Y), matrix);
                Vector2 bottom = Vector2.Transform(new Vector2(view.Right, view.Bottom), matrix);
                Rectangle clip = new Rectangle((int)Math.Ceiling(top.X), (int)Math.Ceiling(top.Y),
                    Math.Max(0, (int)Math.Floor(bottom.X) - (int)Math.Ceiling(top.X)),
                    Math.Max(0, (int)Math.Floor(bottom.Y) - (int)Math.Ceiling(top.Y)));
                device.ScissorRectangle = Rectangle.Intersect(oldScissor, clip);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, clipped, null, matrix);
                contentBatch = true;
                for (int i = 0; i < layout.Elements.Count; i++)
                {
                    F5Element element = layout.Elements[i];
                    F5Rect rect = element.Rect.Offset(view.X, view.Y - state.Scroll);
                    if (rect.Bottom <= view.Y || rect.Y >= view.Bottom) continue;
                    if (element.Kind == F5ElementKind.Panel) F5ControlRenderer.Panel(batch, pixel, row, rect);
                    else if (element.Kind == F5ElementKind.Field) Panel(batch, rect, row, new Color(180, 180, 180));
                    else if (element.Kind == F5ElementKind.Text)
                        Text(batch, element.Text, new Vector2(rect.X, rect.Y), element.TextScale,
                            Color.White, element.TextSize);
                    else if (element.Kind == F5ElementKind.Divider) Decoration(batch, rect, Color.White * 0.35f);
                    else if (element.Kind == F5ElementKind.Hotkey) Keyboard(batch, rect);
                    else
                    {
                        bool enabled = element.Command != F5Command.None && !biomeFailed;
                        bool hovered = rect.Contains(state.PointerX, state.PointerY) && view.Contains(state.PointerX, state.PointerY);
                        F5ControlRenderer.Button(batch, pixel, button, font, element, hovered, enabled,
                            F5Layout.IsSelected(element, biomeEnabled, biomeFailed) ? (Color?)(biomeEnabled ? Color.LightGreen : Color.IndianRed) : null,
                            view.X, view.Y - state.Scroll);
                    }
                }
                batch.End();
                contentBatch = false;
                device.ScissorRectangle = oldScissor;
                batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, matrix);
                contentBatch = true;
                DrawHint(batch, state, biomeFailed);
            }
            finally
            {
                try { if (contentBatch) batch.End(); }
                finally
                {
                    device.ScissorRectangle = oldScissor;
                    device.RasterizerState = oldRasterizer;
                    device.BlendState = oldBlend;
                    device.DepthStencilState = oldDepth;
                    device.SamplerStates[0] = oldSampler;
                    batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, layerMatrix);
                }
            }
        }

        private void DrawHint(SpriteBatch batch, F5Interaction state, bool biomeFailed)
        {
            if (!state.OwnsPointer) return;
            F5Element hover = state.HitButton(state.PointerX - state.X, state.PointerY - state.Y);
            if (hover == null) return;
            int index = F5Layout.HintIndex(hover, biomeFailed);
            if (index < 0) return;
            F5Size size = state.Layout.HintSize(index);
            float x = Math.Max(state.X + 8, Math.Min(state.X + state.Layout.Window.Width - size.Width - 24, state.PointerX + 14));
            float y = Math.Max(state.Y + 8, Math.Min(state.Y + state.Layout.Window.Height - size.Height - 24, state.PointerY + 18));
            Panel(batch, new F5Rect(x, y, size.Width + 16, size.Height + 16), background, Color.White);
            Text(batch, F5Layout.HintText(index), new Vector2(x + 8, y + 8), 0.65f, Color.White, size);
        }

        private void DrawChrome(SpriteBatch batch, F5Interaction state)
        {
            F5Layout layout = state.Layout;
            Panel(batch, layout.Window.Offset(state.X, state.Y), background, new Color(235, 235, 235), true);
            Text(batch, F5Layout.DisplayTitle, new Vector2(state.X + 14,
                state.Y + layout.Title.Y + (layout.Title.Height - layout.TitleSize.Height) / 2),
                0.75f, Color.White, layout.TitleSize);
            Decoration(batch, layout.TitleDivider.Offset(state.X, state.Y), Color.White * 0.35f);
            for (int i = 0; i < F5Layout.Pages.Length; i++)
            {
                F5Rect nav = layout.Navigation(i).Offset(state.X, state.Y);
                bool selected = state.Page == i;
                bool hovered = nav.Contains(state.PointerX, state.PointerY);
                Panel(batch, nav, button, selected || hovered ? Color.White : new Color(220, 220, 220));
                F5Size size = layout.NavigationSize(i);
                Color foreground = selected ? Color.Gold : Color.White;
                icons.Draw(batch, i, layout.NavigationIcon(i).Offset(state.X, state.Y), foreground);
                F5Rect label = layout.NavigationLabel(i).Offset(state.X, state.Y);
                Text(batch, F5Layout.Pages[i], new Vector2(label.X, label.Y), 0.75f, foreground, size);
                if (selected) Decoration(batch, layout.NavigationUnderline(i).Offset(state.X, state.Y), Color.Gold);
            }
            Panel(batch, layout.ContentPanel.Offset(state.X, state.Y), row, new Color(205, 205, 205));
            RoundBar(batch, layout.ScrollTrackVisual.Offset(state.X, state.Y), Color.Black * 0.4f);
            bool hover = layout.ScrollTrack.Offset(state.X, state.Y).Contains(state.PointerX, state.PointerY);
            RoundBar(batch, layout.ScrollThumbVisual(state.Scroll).Offset(state.X, state.Y),
                Color.White * (layout.MaxScroll <= 0 ? 0.25f : hover || state.DraggingScroll ? 0.85f : 0.6f));
        }

        internal void Keyboard(SpriteBatch batch, F5Rect slot)
        {
            // One shell-owned atlas serves real entries and inert placeholders.
            icons.Draw(batch, F5IconAtlas.KeyboardIndex, new F5Rect(slot.X + (slot.Width - 14) / 2,
                slot.Y + (slot.Height - 14) / 2, 14, 14), Color.White);
        }

        private void Decoration(SpriteBatch batch, F5Rect rect, Color color)
        { batch.Draw(pixel, new Vector2(rect.X, rect.Y), new Rectangle(0, 0, 1, 1), color,
            0, Vector2.Zero, new Vector2(rect.Width, rect.Height), SpriteEffects.None, 0); }

        private void EnsureRoundCap(GraphicsDevice device)
        {
            if (roundCap != null && !roundCap.IsDisposed && ReferenceEquals(roundCap.GraphicsDevice, device)) return;
            if (roundCap != null) roundCap.Dispose();
            var data = new Color[64];
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                float distance = (float)Math.Sqrt((x - 3.5f) * (x - 3.5f) + (y - 3.5f) * (y - 3.5f));
                data[y * 8 + x] = Color.White * Math.Max(0, Math.Min(1, 4 - distance));
            }
            var candidate = new Texture2D(device, 8, 8);
            try { candidate.SetData(data); roundCap = candidate; }
            catch { candidate.Dispose(); roundCap = null; throw; }
        }

        private void RoundBar(SpriteBatch batch, F5Rect rect, Color color)
        {
            float radius = rect.Width / 2;
            batch.Draw(roundCap, new Vector2(rect.X, rect.Y), new Rectangle(0, 0, 8, 4), color,
                0, Vector2.Zero, rect.Width / 8, SpriteEffects.None, 0);
            batch.Draw(pixel, new Vector2(rect.X, rect.Y + radius), new Rectangle(0, 0, 1, 1), color,
                0, Vector2.Zero, new Vector2(rect.Width, Math.Max(0, rect.Height - 2 * radius)), SpriteEffects.None, 0);
            batch.Draw(roundCap, new Vector2(rect.X, rect.Bottom - radius), new Rectangle(0, 4, 8, 4), color,
                0, Vector2.Zero, rect.Width / 8, SpriteEffects.None, 0);
        }

        private void Panel(SpriteBatch batch, F5Rect rect, Texture2D texture, Color tint,
            bool roundedOuter = false, bool fractionalSurface = false)
        { UiSurface.Panel(batch, pixel, rect, texture, tint, roundedOuter, fractionalSurface); }

        private void Fill(SpriteBatch batch, F5Rect rect, Color color)
        { batch.Draw(pixel, new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), color); }
        private void Text(SpriteBatch batch, string text, Vector2 position, float scale, Color color, F5Size size)
        { Utils.DrawBorderStringFourWay(batch, font, text, position.X - size.OffsetX, position.Y - size.OffsetY,
            color, Color.Black, Vector2.Zero, scale); }

        public void Dispose()
        {
            // Dispose only our GPU objects; font and skin textures belong to Terraria assets.
            if (clipped != null) { clipped.Dispose(); clipped = null; }
            if (roundCap != null) { roundCap.Dispose(); roundCap = null; }
            icons.Dispose();
        }
    }
}
