using System;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Blueprint
{
    // Presentation owns only two button rectangles and their UI gesture. Mode,
    // selection, world-action tails and preference all remain on HostBlueprint.
    internal sealed class BlueprintToolbar : IDisposable
    {
        internal static readonly Color HintColor = new Color(184, 134, 11);
        private readonly HostBlueprint host;
        internal readonly BlueprintToolbarLayout Layout = new BlueprintToolbarLayout();
        private readonly UiTextMetrics metrics = new UiTextMetrics();
        private readonly F5HintLayout hint = new F5HintLayout();
        private readonly Func<string, float, F5Size> measure;
        private DynamicSpriteFont font;
        private Texture2D skin, pixel;
        private RasterizerState clipped;
        private Matrix drawnMatrix;
        private F5Rect visibleClip;
        private bool presented, previousLeft = true, needsNeutral = true;
        private bool leftTail, rightTail;
        private long processedMapping = -1;
        private int armed = -1, armedGeneration;
#if DEBUG
        internal int MeasurementCount { get; private set; }
#endif
        internal BlueprintToolbar(HostBlueprint host) { this.host = host; measure = Measure; }

        // This is a visible UI hit, never the capture mode or an aggregate tail.
        // HostInputState retains each already-owned physical mouse tail itself;
        // reporting either tail here would capture the other key outside the bar.
        internal bool HitCurrent()
        {
            if (!GeometryCurrent()) return false;
            Vector2 point = Pointer();
            return Layout.Panel.Contains(point.X, point.Y) && visibleClip.Contains(point.X, point.Y);
        }

        private bool GeometryCurrent()
        {
            return presented && host.ShowToolbar && drawnMatrix == Main.UIScaleMatrix &&
                Layout.Matches(PlayerInput.OriginalScreenSize.X, PlayerInput.OriginalScreenSize.Y,
                    Main.UIScaleMatrix.M11, FontAssets.MouseText?.Value, host.State.Active) &&
                ReferenceEquals(skin, TextureAssets.InventoryBack?.Value) && ReferenceEquals(pixel, TextureAssets.MagicPixel?.Value);
        }
        private Vector2 Pointer()
        {
            // Physical UI pixels use the UI inverse only. The selection's frozen
            // world tile coordinate is owned by the independent pointer proof.
            return Vector2.Transform(new Vector2(PlayerInput.MouseInfo.X * PlayerInput.RawMouseScale.X,
                PlayerInput.MouseInfo.Y * PlayerInput.RawMouseScale.Y), Matrix.Invert(Main.UIScaleMatrix));
        }

        internal void Process()
        {
            if (processedMapping == host.Input.Mapping) return;
            processedMapping = host.Input.Mapping;
            if (!host.Input.SampleFocused)
            {
                Cancel(); previousLeft = needsNeutral = true;
                return; // A synthetic unfocused release cannot retire UI tails.
            }
            bool left = host.Input.RawMouseLeft, right = host.Input.RawMouseRight;
            bool current = host.Input.CanStartActions && GeometryCurrent();
            Vector2 point = current ? Pointer() : Vector2.Zero;
            bool over = current && Layout.Panel.Contains(point.X, point.Y) && visibleClip.Contains(point.X, point.Y);
            int hit = over ? Layout.Hit(point.X, point.Y) : -1;
            if (!current) Cancel();
            if (over)
            {
                // Disabled buttons and the bar's small gaps still own UI input.
                // Dragging into the bar cancels world editing, but never arms it.
                if (left) leftTail = true;
                if (right) rightTail = true;
                if (!needsNeutral && left && !previousLeft)
                { armed = hit >= 0 && Layout.Available(hit) ? hit : -1; armedGeneration = Layout.Generation; }
            }
            int execute = -1;
            if (armed >= 0 && !left && previousLeft)
            {
                if (current && armedGeneration == Layout.Generation && armed == hit && Layout.Available(armed)) execute = armed;
                armed = -1;
            }
            if (right) armed = -1;
            // Consume before retiring each trusted release. Hiding or reflowing
            // cancels activation, while its physical tail remains UI-owned.
            Consume(leftTail, rightTail);
            if (!left) leftTail = false;
            if (!right) rightTail = false;
            if (!left && !right) needsNeutral = false;
            previousLeft = left;
            if (execute >= 0) host.Toggle(); // Both live buttons share one owner action.
        }

        private void Consume(bool left, bool right)
        {
            if (left)
            {
                host.Input.Hotkeys.ConsumePress(256);
                PlayerInput.Triggers.Current.MouseLeft = PlayerInput.Triggers.JustPressed.MouseLeft = PlayerInput.Triggers.JustReleased.MouseLeft = false;
                Main.mouseLeft = false;
            }
            if (right)
            {
                host.Input.Hotkeys.ConsumePress(257);
                PlayerInput.Triggers.Current.MouseRight = PlayerInput.Triggers.JustPressed.MouseRight = PlayerInput.Triggers.JustReleased.MouseRight = false;
                Main.mouseRight = false;
            }
        }

        internal void Cancel()
        {
            armed = -1; presented = false; hint.Hide();
            // Do not clear leftTail/rightTail here: visibility, session and
            // focus changes are not evidence that a physical key was released.
        }

        internal void Draw()
        {
            if (!host.ShowToolbar) { Cancel(); return; }
            var nextFont = FontAssets.MouseText?.Value;
            var nextSkin = TextureAssets.InventoryBack?.Value;
            var nextPixel = TextureAssets.MagicPixel?.Value;
            if (nextFont == null || nextPixel == null || nextPixel.IsDisposed || Main.spriteBatch == null) { Cancel(); return; }
            Matrix matrix = Main.UIScaleMatrix;
            Vector2 screen = PlayerInput.OriginalScreenSize;
            if (!ReferenceEquals(skin, nextSkin) || !ReferenceEquals(pixel, nextPixel) || matrix != drawnMatrix) Cancel();
            font = nextFont; skin = nextSkin; pixel = nextPixel;
            int oldGeneration = Layout.Generation;
            if (!Layout.Ensure(screen.X, screen.Y, matrix.M11, font, measure, host.State.Active)) { Cancel(); return; }
            if (oldGeneration != Layout.Generation) Cancel();
            drawnMatrix = matrix;
            var batch = Main.spriteBatch; var device = batch.GraphicsDevice;
            Rectangle oldScissor = device.ScissorRectangle;
            var oldRasterizer = device.RasterizerState; var oldBlend = device.BlendState;
            var oldDepth = device.DepthStencilState; var oldSampler = device.SamplerStates[0];
            if (clipped == null) clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            var bounds = new F5Rect(8, 8, screen.X / matrix.M11 - 16, screen.Y / matrix.M11 - 16);
            Rectangle clip = F5ControlRenderer.ContentClip(bounds, matrix, oldScissor);
            bool began = false;
            batch.End();
            try
            {
                device.ScissorRectangle = clip;
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, clipped, null, matrix);
                began = true;
                visibleClip = F5ControlRenderer.LogicalClip(clip, matrix);
                Vector2 point = Pointer();
                bool over = Layout.Panel.Contains(point.X, point.Y) && visibleClip.Contains(point.X, point.Y);
                int hovered = over ? Layout.Hit(point.X, point.Y) : -1;
                UiSurface.Panel(batch, pixel, Layout.Panel, skin, new Color(232, 232, 232), roundedOuter: true);
                for (int i = 0; i < Layout.Buttons.Length; i++)
                    F5ControlRenderer.Button(batch, pixel, skin, font, Layout.Buttons[i], hovered == i,
                        Layout.Available(i), null, Layout.Panel.X, Layout.Panel.Y, true, armed == i && hovered == i);
                DrawHint(batch, bounds, hovered);
                if (over) Main.LocalPlayer.mouseInterface = true;
                presented = true;
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

        private void DrawHint(SpriteBatch batch, F5Rect bounds, int hovered)
        {
            string value = Layout.Hint(hovered);
            if (value == null || leftTail || rightTail || !host.Input.CanPrepareText) { hint.Hide(); return; }
            // Reuse the common bounded wrapping and glyph metrics, but place
            // the measured result above the entire bar, explicitly centered.
            var above = new F5Rect(bounds.X, bounds.Y, bounds.Width, Layout.Panel.Y - 2 - bounds.Y);
            hint.Prepare(value, Layout.Panel, above, font, measure);
            if (!hint.Visible) return;
            // Lines already include their outline and native glyph offsets.
            // Discard the common hint panel padding: only text is presented,
            // each measured line centered above the bar. The native font's
            // transparent glyph rows already leave the visible outline gap;
            // adding another panel gap would move the actual text too far up.
            float contentHeight = hint.Lines.Count == 0 ? 0 : hint.Lines[hint.Lines.Count - 1].Rect.Bottom;
            float textTop = Layout.Panel.Y - contentHeight;
            foreach (var line in hint.Lines)
                Utils.DrawBorderStringFourWay(batch, font, line.Text,
                    Layout.Panel.X + (Layout.Panel.Width - line.Rect.Width) / 2 - line.TextSize.OffsetX,
                    textTop + line.Rect.Y - line.TextSize.OffsetY,
                    HintColor, Color.Black, Vector2.Zero, line.TextScale);
        }

        private F5Size Measure(string text, float scale)
        {
#if DEBUG
            MeasurementCount++;
#endif
            var size = metrics.Measure(font, text);
            return new F5Size(size.Width * scale + 4, size.Height * scale + 4, size.OffsetX * scale - 2, size.OffsetY * scale - 2);
        }

        public void Dispose()
        {
            Cancel(); hint.Clear();
            clipped?.Dispose(); clipped = null;
            // Font and skin textures belong to Terraria, never to this bar.
            font = null; skin = pixel = null;
        }
    }

    // CPU geometry is separate from the native adapter so the actual toolbar
    // placement, mode replacement and stable-layout cost can be checked directly.
    internal sealed class BlueprintToolbarLayout
    {
        internal readonly F5Element[] Buttons = new F5Element[2];
        internal F5Rect Panel { get; private set; }
        internal int Generation { get; private set; }
        private float width, height, scale;
        private object font;
        private bool active;
        internal bool Matches(float w, float h, float s, object f, bool mode)
        { return Generation > 0 && w == width && h == height && s == scale && ReferenceEquals(f, font) && mode == active; }
        internal bool Ensure(float w, float h, float s, object f, Func<string, float, F5Size> measure, bool mode)
        {
            if (Matches(w, h, s, f, mode)) return true;
            if (f == null || measure == null || !Finite(w) || !Finite(h) || !Finite(s) || s <= 0 || w <= 0 || h <= 0) return false;
            string first = mode ? "保存蓝图" : "采集蓝图", second = mode ? "退出采集" : "打开蓝图库";
            var a = measure(first, .70f); var b = measure(second, .70f);
            float aw = Math.Max(30, a.Width + 16), bw = Math.Max(30, b.Width + 16), bh = Math.Max(30, Math.Max(a.Height, b.Height) + 8);
            float pw = aw + bw + 20, ph = bh + 16;
            if (!Finite(pw) || !Finite(ph) || pw > w / s - 16 || ph > h / s - 32) return false;
            Panel = new F5Rect((w / s - pw) / 2, h / s - ph - 24, pw, ph);
            Buttons[0] = new F5Element(F5ElementKind.Button, new F5Rect(8, 8, aw, bh), first, a, .70f, F5Command.None);
            Buttons[1] = new F5Element(F5ElementKind.Button, new F5Rect(12 + aw, 8, bw, bh), second, b, .70f, F5Command.None);
            width = w; height = h; scale = s; font = f; active = mode; Generation++;
            return true;
        }
        internal int Hit(float x, float y)
        { for (int i = 0; i < Buttons.Length; i++) if (Buttons[i].Rect.Offset(Panel.X, Panel.Y).Contains(x, y)) return i; return -1; }
        internal bool Available(int index) { return active ? index == 1 : index == 0; }
        internal string Hint(int index)
        { return index < 0 || index > 1 ? null : active ? index == 0 ? BlueprintControls.SaveHint : BlueprintControls.ExitHint : index == 0 ? BlueprintControls.StartHint : null; }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
