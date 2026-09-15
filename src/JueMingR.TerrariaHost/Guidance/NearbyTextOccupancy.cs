using System;
using Microsoft.Xna.Framework;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Guidance
{
    // Bounded native pools are read only while our warning is visible. A cache
    // entry owns geometry only, never native active/TTL/text or another source.
    internal sealed class NearbyTextOccupancy
    {
        private readonly string[] texts = new string[120];
        private readonly DynamicSpriteFont[] fonts = new DynamicSpriteFont[120];
        private readonly Vector2[] sizes = new Vector2[120];
        private readonly Rectangle[] occupied = new Rectangle[120];
        private int count, lane;
#if DEBUG
        internal int PoolPasses { get; private set; }
        internal int Measurements { get; private set; }
#endif
        internal void Clear() { lane = count = 0; }
        internal Vector2 Place(Vector2 anchor, float width, float height, float viewportWidth, float viewportHeight, Matrix zoom)
        {
            count = 0;
#if DEBUG
            PoolPasses++;
#endif
            var popups = PopupText.popupText;
            if (popups != null)
                for (int i = 0; i < Math.Min(20, popups.Length); i++)
                { var p = popups[i]; if (p != null && p.active) Add(i, p.displayText ?? p.name, FontAssets.MouseText?.Value, p.position, p.scale, p.rotation, anchor, zoom); }
            var combat = Main.combatText;
            if (combat != null)
                for (int i = 0; i < Math.Min(100, combat.Length); i++)
                { var p = combat[i]; if (p != null && p.active) Add(20 + i, p.text, FontAssets.CombatText[p.crit ? 1 : 0]?.Value, p.position, p.scale, p.rotation, anchor, zoom); }
            // Keep the chosen band until it collides; do not bounce back toward
            // the head as each short native text expires. Six finite candidates.
            Vector2 chosen = Candidate(anchor, width, height, viewportWidth, viewportHeight, lane);
            if (!Blocked(chosen, width, height)) return chosen;
            for (int i = 0; i < 6; i++)
            {
                Vector2 candidate = Candidate(anchor, width, height, viewportWidth, viewportHeight, i);
                if (!Blocked(candidate, width, height)) { lane = i; return candidate; }
            }
            return chosen;
        }
        private void Add(int slot, string text, DynamicSpriteFont font, Vector2 world, float scale, float rotation, Vector2 anchor, Matrix zoom)
        {
            if (font == null || string.IsNullOrEmpty(text) || scale <= 0) return;
            Vector2 position = GuidanceWorldLayer.Project(world, zoom);
            if (Math.Abs(position.X - anchor.X) > 500 || Math.Abs(position.Y - anchor.Y) > 320) return;
            if (texts[slot] != text || !ReferenceEquals(fonts[slot], font))
            {
                Vector2 measured = font.MeasureString(text); texts[slot] = text; fonts[slot] = font; sizes[slot] = measured;
#if DEBUG
                Measurements++;
#endif
            }
            // Native .8 draws at position + unscaled half-size, rotating and
            // scaling around that half-size. Its stored position is not center.
            position += sizes[slot] * new Vector2(zoom.M11, zoom.M22) * .5f;
            float cos = Math.Abs((float)Math.Cos(rotation)), sin = Math.Abs((float)Math.Sin(rotation));
            Vector2 size = new Vector2(sizes[slot].X * cos + sizes[slot].Y * sin, sizes[slot].X * sin + sizes[slot].Y * cos) * scale;
            size *= new Vector2(Math.Abs(zoom.M11), Math.Abs(zoom.M22));
            occupied[count++] = new Rectangle((int)(position.X - size.X / 2 - 6), (int)(position.Y - size.Y / 2 - 6), (int)Math.Ceiling(size.X + 12), (int)Math.Ceiling(size.Y + 12));
        }
        private bool Blocked(Vector2 p, float width, float height)
        { var box = new Rectangle((int)p.X, (int)p.Y, (int)Math.Ceiling(width), (int)Math.Ceiling(height)); for (int i = 0; i < count; i++) if (box.Intersects(occupied[i])) return true; return false; }
        private static Vector2 Candidate(Vector2 a, float w, float h, float vw, float vh, int band)
        {
            float offset = band < 4 ? -band * (h + 8) : (band - 3) * (h + 8);
            return new Vector2(Math.Max(8, Math.Min(vw - w - 8, a.X - w / 2)), Math.Max(8, Math.Min(vh - h - 8, a.Y + offset - h)));
        }
    }
}
