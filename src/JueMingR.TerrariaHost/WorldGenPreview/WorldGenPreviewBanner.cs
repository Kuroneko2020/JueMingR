using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.States;
using Terraria.UI;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.WorldGenPreview
{
    // A non-interactive child of one native load page. Fonts and MagicPixel are
    // borrowed; this node owns only its small CPU geometry cache.
    internal sealed class WorldGenPreviewBanner : UIElement
    {
        internal const string Text = "按 F5 查看世界生成预览";
        private readonly UIWorldLoad page;
        private readonly UIElement anchor;
        private readonly HostWorldGenPreview owner;
        private readonly System.Action<System.Exception> fail;
        private readonly NativeProgressFootprint footprint;
        private readonly UiTextMetrics metrics = new UiTextMetrics();
        private DynamicSpriteFont font;
        private F5Size measured;
        private Rectangle bounds;
        private Vector2 position;
        private bool visible;
        private CalculatedStyle lastProgress;
        private float lastWidth, lastHeight;
        private float lastScale;
        private bool layoutReady;
        internal int Measurements { get; private set; }
        internal int Layouts { get; private set; }
        internal Rectangle Bounds { get { return bounds; } }
        internal Rectangle ProgressBounds { get; private set; }
        internal WorldGenPreviewBanner(UIWorldLoad page, UIElement anchor, HostWorldGenPreview owner, System.Action<System.Exception> fail, Asset<Texture2D>[] textures)
        {
            this.page = page; this.anchor = anchor; this.owner = owner; this.fail = fail;
            footprint = new NativeProgressFootprint(textures);
            Width.Set(0, 1); Height.Set(0, 1); IgnoresMouseInteraction = true;
        }
        internal void Prepare()
        {
            visible = owner.CanShow(page) && FontAssets.MouseText != null && FontAssets.MouseText.IsLoaded;
            if (!visible) return;
            Rectangle native; bool changedFootprint;
            if (!footprint.TryRead(out native, out changedFootprint)) { visible = false; return; }
            var next = FontAssets.MouseText.Value;
            bool changedFont = !ReferenceEquals(font, next);
            if (changedFont) { font = next; measured = metrics.Measure(font, Text); Measurements++; }
            CalculatedStyle progress = anchor.GetDimensions();
            float scale = Main.UIScaleMatrix.M11;
            // Native MenuUI dimensions are already logical UI coordinates.
            // Main.screenWidth/Height can be temporarily zoom-scaled by native
            // callers; dividing those again would apply UI scale twice.
            CalculatedStyle viewport = Main.MenuUI.GetDimensions();
            if (layoutReady && !changedFont && !changedFootprint && lastProgress.X == progress.X && lastProgress.Y == progress.Y &&
                lastProgress.Width == progress.Width && lastProgress.Height == progress.Height && lastScale == scale &&
                lastWidth == viewport.Width && lastHeight == viewport.Height) return;
            lastProgress = progress; lastScale = scale; lastWidth = viewport.Width; lastHeight = viewport.Height;
            layoutReady = true; Layouts++;
            float width = measured.Width + 28, height = measured.Height + 16;
            float viewWidth = viewport.Width, viewHeight = viewport.Height;
            ProgressBounds = new Rectangle((int)progress.X + native.X, (int)progress.Y + native.Y, native.Width, native.Height);
            float left = MathHelper.Clamp(progress.X + progress.Width / 2 - width / 2, 6, System.Math.Max(6, viewWidth - width - 6));
            float top = ProgressBounds.Bottom + 14;
            // Never clamp the banner back over the native texture ornament.
            // Current supported native viewports have room below this footprint.
            if (top + height > viewHeight - 6) top = ProgressBounds.Bottom + 4;
            bounds = new Rectangle((int)left, (int)top, (int)System.Math.Ceiling(width), (int)System.Math.Ceiling(height));
            position = new Vector2(left + 14 - measured.OffsetX, top + 8 - measured.OffsetY);
        }
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            if (!visible || font == null || !owner.CanShow(page) || !TextureAssets.MagicPixel.IsLoaded) return;
            try
            {
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, bounds, new Color(22, 19, 13, 190));
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(139, 112, 58, 180));
                spriteBatch.DrawString(font, Text, position + Vector2.One, Color.Black * 0.8f);
                spriteBatch.DrawString(font, Text, position, new Color(222, 193, 131));
            }
            catch (System.Exception e) { visible = false; fail(e); }
        }
    }

    // UIGenProgressBar's 70px logical height omits its lower ornament. Mirror
    // the locked native DrawSelf rectangles, conservatively including all three
    // possible outer textures without consuming its drunk-world RNG choice.
    // These assets are borrowed and their current texture identities invalidate
    // only this finite footprint cache; no texture readback or ownership transfer.
    internal sealed class NativeProgressFootprint
    {
        private readonly Asset<Texture2D>[] assets;
        private readonly Texture2D[] identities = new Texture2D[4];
        private Rectangle cached;
        internal NativeProgressFootprint(Asset<Texture2D>[] assets) { this.assets = assets; }
        internal bool TryRead(out Rectangle footprint, out bool changed)
        {
            footprint = cached; changed = false;
            for (int i = 0; i < 4; i++) if (assets[i] == null || !assets[i].IsLoaded) return false;
            for (int i = 0; i < 4; i++)
            {
                var texture = assets[i].Value;
                if (!ReferenceEquals(identities[i], texture)) { identities[i] = texture; changed = true; }
            }
            if (changed)
            {
                int right = 590, bottom = 70;
                for (int i = 0; i < 3; i++)
                { right = System.Math.Max(right, -8 + identities[i].Width); bottom = System.Math.Max(bottom, identities[i].Height); }
                right = System.Math.Max(right, 36 + identities[3].Width);
                bottom = System.Math.Max(bottom, 60 + identities[3].Height);
                cached = new Rectangle(-8, 0, right + 8, bottom);
            }
            footprint = cached; return true;
        }
    }
}
