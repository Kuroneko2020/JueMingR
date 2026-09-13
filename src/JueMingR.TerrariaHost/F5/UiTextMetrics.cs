using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;

namespace JueMingR.TerrariaHost.F5
{
    internal struct F5Size
    {
        internal readonly float Width;
        internal readonly float Height;
        internal readonly float OffsetX, OffsetY;
        internal F5Size(float width, float height, float offsetX = 0, float offsetY = 0)
        { Width = width; Height = height; OffsetX = offsetX; OffsetY = offsetY; }
    }

    // Shared CPU glyph geometry; no texture ownership, readback or Draw-time work.
    internal sealed class UiTextMetrics
    {
        private readonly DynamicSpriteFont.DrawCharacter collect;
        private float left, top, right, bottom;
        private string measuredText;
        private int characterIndex;
        private bool supportedSpace;
        internal UiTextMetrics() { collect = Collect; }
        internal F5Size Measure(DynamicSpriteFont font, string text)
        {
            left = top = float.PositiveInfinity; right = bottom = float.NegativeInfinity;
            measuredText = text; characterIndex = 0;
            try
            {
                supportedSpace = font.IsCharacterSupported(' ');
                font.DrawCustomFast(collect, text, Vector2.Zero, Vector2.One);
                if (float.IsPositiveInfinity(left)) return new F5Size(0, font.LineSpacing);
                return float.IsPositiveInfinity(top) ? new F5Size(right - left, font.LineSpacing, left, 0) :
                    new F5Size(right - left, bottom - top, left, top);
            }
            finally { measuredText = null; characterIndex = 0; supportedSpace = false; }
        }
        private void Collect(Texture2D texture, Vector2 position, Rectangle source, Vector2 scale)
        {
            // DrawCustomFast skips CR/LF, then invokes once per remaining UTF-16 unit,
            // including empty and fallback glyphs. Keep this cursor aligned before
            // rejecting rectangles; native drawing still owns spacing and newlines.
            while (characterIndex < measuredText.Length && (measuredText[characterIndex] == '\r' || measuredText[characterIndex] == '\n')) characterIndex++;
            bool space = supportedSpace && measuredText[characterIndex] == ' ';
            characterIndex++;
            if (source.Width <= 0 || source.Height <= 0) return;
            left = Math.Min(left, position.X);
            right = Math.Max(right, position.X + source.Width * scale.X);
            // Mouse_Text's supported space has a transparent 1x1 glyph at Y=40.
            // It must not inflate visible height; preserve horizontal bounds and
            // all other glyphs, including visible 1x1 and unsupported-space fallback.
            if (space) return;
            top = Math.Min(top, position.Y);
            bottom = Math.Max(bottom, position.Y + source.Height * scale.Y);
        }
    }
}
