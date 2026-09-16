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
        private readonly EntityLabels.StylePopupRenderer styleRenderer = new EntityLabels.StylePopupRenderer();
        internal readonly F5HintLayout HintLayout = new F5HintLayout();
        private readonly Func<string, float, F5Size> hintMeasure;
        internal void DrawStylePopup(EntityLabels.StylePopup popup)
        { styleRenderer.Draw(popup, Main.spriteBatch, font, background, pixel); }
        internal int SkinGeneration { get; private set; }
        internal object FontIdentity { get { return font; } }
        internal EntityLabelControls EntityControls { get; set; }
        internal WorldTargetControls WorldControls { get; set; }
        internal WorldObjectControls ObjectControls { get; set; }
        internal Information.InformationControls InformationControls { get; set; }
        internal GuidanceControls GuidanceControls { get; set; }
        internal DeathControls DeathControls { get; set; }
        internal MapControls MapControls { get; set; }
        private readonly Map.MarkerIcons markerIcons = new Map.MarkerIcons();
        private string mapValue;
        private object mapValueFont;
        private F5Size mapValueSize;
        internal void PrepareMapValue()
        {
            string value = MapControls?.Value ?? "暂不可用";
            if (value == mapValue && ReferenceEquals(mapValueFont, font)) return;
            mapValue = value; mapValueFont = font; mapValueSize = PopupMeasure(value, .70f);
        }
        internal void DrawMapPopup(MapManagementPopup popup)
        {
            if (!popup.Visible) return; var batch = Main.spriteBatch;
            Panel(batch, popup.Panel, background, Color.White, true);
            foreach (var section in popup.Sections) F5ControlRenderer.Panel(batch, pixel, row, section.Offset(popup.Panel.X, popup.Panel.Y));
            foreach (var line in popup.Text) Text(batch, line.Text, new Vector2(popup.Panel.X + line.Rect.X, popup.Panel.Y + line.Rect.Y), line.TextScale, Color.White, line.TextSize);
            foreach (var icon in popup.Icons) { F5Rect bounds; var rect = icon.Item2.Offset(popup.Panel.X, popup.Panel.Y); markerIcons.Draw(batch, icon.Item1, new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), 24, 1, out bounds); }
            for (int i = 0; i < popup.Buttons.Count; i++) F5ControlRenderer.Button(batch, pixel, button, font, popup.Buttons[i], popup.Hovered == i, popup.Enabled[i], popup.Selected(popup.Commands[i]) ? (Color?)Color.LightGreen : null, popup.Panel.X, popup.Panel.Y, true, popup.Pressed == i && popup.Hovered == i);
            if (popup.Hint.Visible)
            {
                var hint = popup.Hint.Panel; Panel(batch, hint, background, Color.White, true);
                foreach (var line in popup.Hint.Lines) Text(batch, line.Text, new Vector2(hint.X + 8 + line.Rect.X, hint.Y + 8 + line.Rect.Y), line.TextScale, Color.White, line.TextSize);
            }
            if (popup.Editor != null && popup.EditRect.Width > 0)
            {
                var r = popup.EditRect; var editor = popup.Editor; var view = popup.EditView;
                float caret = view.Caret;
                if (editor.HasSelection)
                {
                    float left = view.SelectionLeft, right = view.SelectionRight;
                    batch.Draw(pixel, new Vector2(r.X + 4 + left, r.Y + 3), new Rectangle(0, 0, 1, 1), Color.CornflowerBlue * .45f, 0, Vector2.Zero, new Vector2(Math.Max(1, right - left), r.Height - 6), SpriteEffects.None, 0);
                }
                Text(batch, view.Text, new Vector2(r.X + 4, r.Y + 3), .70f, Color.White, view.Size);
                if ((Environment.TickCount & 1023) < 512) batch.Draw(pixel, new Vector2(r.X + 4 + caret, r.Y + 3), new Rectangle(0, 0, 1, 1), Color.White, 0, Vector2.Zero, new Vector2(1, r.Height - 6), SpriteEffects.None, 0);
                Main.instance.SetIMEPanelAnchor(new Vector2(r.X + 4 + caret, r.Bottom + 32), 0);
            }
        }
        internal void DrawDeathPopup(DeathHistoryPopup popup)
        {
            if (!popup.Visible) return;
            var batch = Main.spriteBatch;
            Panel(batch, popup.Panel, background, Color.White, true);
            foreach (var line in popup.Text) Text(batch, line.Text, new Vector2(popup.Panel.X + line.Rect.X, popup.Panel.Y + line.Rect.Y), line.TextScale, Color.White, line.TextSize);
            for (int i = 0; i < popup.Buttons.Count; i++)
            {
                int command = popup.Commands[i];
                if (command >= 10 && command < 16)
                {
                    // The reason cell itself is the read-only entry, without
                    // another large per-row button or a repeated action label.
                    var cell = popup.Buttons[i]; Text(batch, cell.Text, new Vector2(popup.Panel.X + cell.Rect.X + 4, popup.Panel.Y + cell.Rect.Y), cell.TextScale, popup.Hovered == command ? Color.LightGoldenrodYellow : Color.White, cell.TextSize);
                    continue;
                }
                F5ControlRenderer.Button(batch, pixel, button, font, popup.Buttons[i], popup.Hovered == command, popup.Enabled[i], popup.IsSelected(command) ? (Color?)Color.LightGreen : null,
                    popup.Panel.X, popup.Panel.Y, true, popup.Pressed == command && popup.Hovered == command);
            }
        }
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
            // Only complete measured lines enter this bounded viewport. Scrolling
            // selects cached lines without touching the caller's SpriteBatch or scissor.
            for (int rowIndex = 0; rowIndex < layout.DetailVisibleLines; rowIndex++)
            {
                var line = layout.DetailText[popup.DetailOffset + rowIndex];
                Text(batch, line.Text, new Vector2(layout.Panel.X + layout.DetailViewport.X,
                    layout.Panel.Y + layout.DetailViewport.Y + rowIndex * layout.DetailLineHeight), line.TextScale, Color.LightGoldenrodYellow, line.TextSize);
            }
            PopupRule(batch, layout.Panel.X + 12, layout.Panel.Y + layout.HeaderBottom, layout.Panel.Width - 24);
            PopupRule(batch, layout.Panel.X + 12, layout.Panel.Y + layout.FooterTop, layout.Panel.Width - 24);
            for (int i = 0; i < layout.Buttons.Count; i++)
            {
                var command = layout.Commands[i];
                bool enabled = layout.IsEnabled(i, popup.DetailOffset);
                F5ControlRenderer.Button(batch, pixel, button, font, layout.Buttons[i], popup.Hovered == command, enabled,
                    command == Hotkeys.HotkeyPopupCommand.Record && enabled ? (Color?)Color.LightGray : null,
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

        internal F5Renderer() { measure = Measure; hintMeasure = MeasureHint; }
        private F5Size MeasureHint(string text, float scale)
        { var size = PopupMeasure(text, scale); return new F5Size(size.Width + 4, size.Height + 4, size.OffsetX - 2, size.OffsetY - 2); }

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
                device.ScissorRectangle = F5ControlRenderer.ContentClip(view, matrix, oldScissor);
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
                    else if (element.Command == F5Command.ExplorationValue)
                    { Text(batch, mapValue ?? "", new Vector2(rect.X, rect.Y + (rect.Height - mapValueSize.Height) / 2), element.TextScale, Color.White, mapValueSize); }
                    else if (element.Kind == F5ElementKind.Text)
                        Text(batch, element.Text, new Vector2(rect.X, rect.Y), element.TextScale,
                            Color.White, element.TextSize);
                    else if (element.Kind == F5ElementKind.Divider) Decoration(batch, rect, Color.White * 0.35f);
                    else if (element.Kind == F5ElementKind.Hotkey) Keyboard(batch, rect);
                    else
                    {
                        bool entity = EntityLabelControls.Target(element.Command).HasValue;
                        bool world = WorldTargetControls.Target(element.Command).HasValue;
                        bool objects = WorldObjectControls.Target(element.Command).HasValue;
                        bool information = Information.InformationControls.Target(element.Command).HasValue || element.Command == F5Command.AdjustInformation;
                        bool legacyBiome = element.Command == F5Command.EnableBiome || element.Command == F5Command.DisableBiome;
                        bool guidance = F5.GuidanceControls.Owns(element.Command);
                        bool enabled = entity ? EntityControls != null && EntityControls.Available(element.Command) : world ? WorldControls != null && WorldControls.Available(element.Command) : objects ? ObjectControls != null && ObjectControls.Available(element.Command) :
                            information ? legacyBiome ? !biomeFailed : InformationControls != null && InformationControls.Available(element.Command) : guidance ? GuidanceControls != null && GuidanceControls.Available(element.Command) : F5.MapControls.Owns(element.Command) ? MapControls != null && MapControls.Available(element.Command) : DeathControls != null && DeathControls.Available(element.Command);
                        bool hovered = !state.PointerBlocked && rect.Contains(state.PointerX, state.PointerY) && view.Contains(state.PointerX, state.PointerY);
                        F5ControlRenderer.Button(batch, pixel, button, font, element, hovered, enabled,
                            entity ? EntityControls?.Selected(element.Command) : world ? WorldControls?.Selected(element.Command) : objects ? ObjectControls?.Selected(element.Command) :
                            information && !legacyBiome ? InformationControls?.Selected(element.Command) : guidance ? GuidanceControls?.Selected(element.Command) : F5.MapControls.Owns(element.Command) ? MapControls?.Selected(element.Command) : F5.DeathControls.Owns(element.Command) ? DeathControls?.Selected(element.Command) : F5Layout.IsSelected(element, biomeEnabled, biomeFailed) ? (Color?)(biomeEnabled ? Color.LightGreen : Color.IndianRed) : null,
                            view.X, view.Y - state.Scroll);
                    }
                }
                batch.End();
                contentBatch = false;
                device.ScissorRectangle = oldScissor;
                batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, matrix);
                contentBatch = true;
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

        internal string ResolveHint(F5Interaction state, Items.ItemsPresentation items, bool blocked, bool biomeFailed, out F5Rect target, F5Rect? contentClip = null)
        {
            target = default(F5Rect);
            if (!state.CanShowHint || blocked) return null;
            if (state.Page == 0) return items == null ? null : items.Hint(state.PointerX, state.PointerY, out target, contentClip);
            var view = state.Layout.Viewport.Offset(state.X, state.Y);
            var visible = contentClip.HasValue ? F5HintLayout.Intersect(view, contentClip.Value) : view;
            if (!visible.Contains(state.PointerX, state.PointerY)) return null;
            var name = F5HintLayout.HitName(state.Layout.Elements, visible, view.X, view.Y - state.Scroll,
                state.PointerX, state.PointerY, out target);
            if (name != null) return name.Description.Text;
            F5Element hover = state.HitButton(state.PointerX - state.X, state.PointerY - state.Y);
            if (hover == null) return null;
            target = F5HintLayout.Intersect(hover.Rect.Offset(view.X, view.Y - state.Scroll), visible);
            if (hover.Kind == F5ElementKind.Hotkey && hover.HotkeyTarget != null)
                return hover.HotkeyTarget == Hotkeys.HotkeyActionIds.AdjustInformation ? "双击设置调整信息窗位置的快捷键" : "双击设置功能开关快捷键";
            return ButtonHint(hover, biomeFailed);
        }
        internal string ButtonHint(F5Element hover, bool biomeFailed)
        {
            if (biomeFailed && (hover.Command == F5Command.EnableBiome || hover.Command == F5Command.DisableBiome)) return "群系显示暂不可用";
            return EntityControls?.Hint(hover.Command) ?? WorldControls?.Hint(hover.Command) ?? ObjectControls?.Hint(hover.Command) ?? InformationControls?.Hint(hover.Command) ?? GuidanceControls?.Hint(hover.Command) ?? DeathControls?.Hint(hover.Command) ?? MapControls?.Hint(hover.Command);
        }
        // The shell owns one current hint for every ordinary page. Adapters
        // supply only content and final name regions; preparation is shared with
        // the CPU consumer checks and never rebuilds a page or reads business.
        internal void PrepareHints(F5Interaction state, Items.ItemsPresentation items, bool blocked, bool biomeFailed,
            F5Rect bounds, F5Rect contentClip, object fontIdentity, Func<string, float, F5Size> measureText)
        {
            F5Rect target;
            string hint = ResolveHint(state, items, blocked, biomeFailed, out target, contentClip);
            if (hint == null) { HintLayout.Hide(); return; }
            HintLayout.Prepare(hint, target, bounds, fontIdentity, measureText);
        }
        internal void DrawHints(F5Interaction state, Matrix matrix, Items.ItemsPresentation items, bool blocked, bool biomeFailed)
        {
            // No graphics or text preparation for a hidden window or higher owner.
            if (!state.CanShowHint || blocked) { HintLayout.Hide(); return; }
            // Blank content and ordinary buttons need no graphics state at all.
            // A real target is checked again against the final pixel clip below.
            F5Rect preliminaryTarget;
            if (ResolveHint(state, items, blocked, biomeFailed, out preliminaryTarget) == null)
            { HintLayout.Hide(); return; }
            var batch = Main.spriteBatch; var device = batch.GraphicsDevice;
            var screen = Terraria.GameInput.PlayerInput.OriginalScreenSize;
            var bounds = new F5Rect(8, 8, screen.X / matrix.M11 - 16, screen.Y / matrix.M11 - 16);
            Rectangle oldScissor = device.ScissorRectangle;
            bounds = F5HintLayout.Intersect(bounds, F5ControlRenderer.LogicalClip(oldScissor, matrix));
            var contentClip = F5ControlRenderer.LogicalClip(F5ControlRenderer.ContentClip(
                state.Layout.Viewport.Offset(state.X, state.Y), matrix, oldScissor), matrix);
            PrepareHints(state, items, blocked, biomeFailed, bounds, contentClip, font, hintMeasure);
            if (!HintLayout.Visible) return;
            RasterizerState oldRasterizer = device.RasterizerState; BlendState oldBlend = device.BlendState;
            DepthStencilState oldDepth = device.DepthStencilState; SamplerState oldSampler = device.SamplerStates[0];
            if (clipped == null) clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            bool began = false; batch.End();
            try
            {
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, clipped, null, matrix);
                began = true;
                var panel = HintLayout.Panel; Panel(batch, panel, background, Color.White);
                foreach (var line in HintLayout.Lines)
                    Text(batch, line.Text, new Vector2(panel.X + 8, panel.Y + 8 + line.Rect.Y), line.TextScale, Color.White, line.TextSize);
            }
            finally
            {
                try { if (began) batch.End(); }
                finally
                {
                    device.ScissorRectangle = oldScissor; device.RasterizerState = oldRasterizer; device.BlendState = oldBlend;
                    device.DepthStencilState = oldDepth; device.SamplerStates[0] = oldSampler;
                    batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
                }
            }
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
                bool hovered = !state.PointerBlocked && nav.Contains(state.PointerX, state.PointerY);
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
            HintLayout.Clear();
            // Dispose only our GPU objects; font and skin textures belong to Terraria assets.
            if (clipped != null) { clipped.Dispose(); clipped = null; }
            if (roundCap != null) { roundCap.Dispose(); roundCap = null; }
            icons.Dispose();
        }
    }
}
