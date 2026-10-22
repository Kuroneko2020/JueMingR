using System;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed class BlueprintWorldLayer
    {
        private readonly HostBlueprint host;
        private readonly World.WorldTileObservation geometry;
        internal BlueprintWorldLayer(HostBlueprint host, World.WorldTileObservation geometry) { this.host = host; this.geometry = geometry; }
        internal void Draw()
        {
            if (!host.State.Active || !host.HasSession || !Rendering.WorldPresentation.CanDraw || Main.spriteBatch == null) return;
            Platform.WorldTargets.WorldTargetView view;
            if (!geometry.TryView(0, out view)) return;
            int right = view.X + view.Width, bottom = view.Y + view.Height;
            // Enumerate only visible rows/spans. A solid world-sized selection
            // takes one fill per visible row, never one object/draw per tile.
            foreach (var span in host.State.Selection.EnumerateVisible(view.X, view.Y, right, bottom))
                Fill(span.Left, span.Y, span.RightExclusive, span.Y + 1, Color.FromNonPremultiplied(35, 125, 255, 70));
            var preview = host.State.Preview;
            if (host.State.Dragging && !preview.IsEmpty)
            {
                int l = Math.Max(view.X, preview.Left), t = Math.Max(view.Y, preview.Top), r = Math.Min(right, preview.RightExclusive), b = Math.Min(bottom, preview.BottomExclusive);
                if (l < r && t < b) Fill(l, t, r, b, Color.FromNonPremultiplied(65, 155, 255, 95));
            }
            // A legal empty outer Update revokes new action permission, while
            // Draw may continue. Present the retained point without replaying
            // input; actual focus/UI/session rejection still hides stale points.
            if (host.CanRetainEdit && host.Pointer.HasPoint)
            {
                int x = host.Pointer.CellX, y = host.Pointer.CellY;
                if (x >= 0 && y >= 0 && x < Main.maxTilesX && y < Main.maxTilesY) {
                    Fill(x, y, x + 1, y + 1, Color.FromNonPremultiplied(80, 180, 255, 150));
                }
            }
        }
        private static void Fill(int left, int top, int right, int bottom, Color color)
        {
            Vector2 a = Main.ReverseGravitySupport(new Vector2(left * 16f, top * 16f) - Main.screenPosition);
            Vector2 b = Main.ReverseGravitySupport(new Vector2(right * 16f, bottom * 16f) - Main.screenPosition);
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)Math.Min(a.X, b.X), (int)Math.Min(a.Y, b.Y),
                (int)Math.Abs(b.X - a.X), (int)Math.Abs(b.Y - a.Y)), color);
        }
    }
}
