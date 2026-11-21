using JueMingR.TerrariaHost.Rendering;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal void DrawCompanionNames()
        {
            if (Main.netMode != 0 || bodies.Count == 0 || !ArchiveIdentityCurrent() || !WorldPresentation.CanDraw) return;
            var font = FontAssets.MouseText.Value;
            float width = Main.screenWidth / Main.UIScale, height = Main.screenHeight / Main.UIScale;
            const float scale = .87f;
            foreach (var actor in actors.Values)
            {
                var body = actor.Body;
                if (!actor.Ready || actor.Closing || body.dead || body.ghost || !body.active ||
                    !ReferenceEquals(Owned(body), actor) || string.IsNullOrEmpty(body.name)) continue;

                // Continue the native overhead batch: the anchor already
                // applies camera zoom, gravity, screen offset and UI scale.
                Vector2 anchor = Main.GetChatDrawPosition(body);
                if (body.chatOverhead.timeLeft > 0) anchor.Y -= body.chatOverhead.messageSize.Y;
                Vector2 size = font.MeasureString(body.name) * scale;
                Vector2 position = (anchor - new Vector2(size.X * .5f, size.Y)).Floor();
                if (position.X + size.X < 0 || position.Y + size.Y < 0 || position.X > width || position.Y > height) continue;

                // Names are literal strings, including any bracketed text.
                F5.F5ControlRenderer.DrawScaledOutline(Main.spriteBatch, font, body.name, position, Color.White, scale);
            }
        }
    }
}
