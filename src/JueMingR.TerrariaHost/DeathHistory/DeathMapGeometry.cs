using Microsoft.Xna.Framework;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.DeathHistory
{
    internal struct DeathMapGeometry
    {
        internal Vector2 Center;
        internal F5Rect Icon, Hit;
        internal static DeathMapGeometry Project(float x, float y, Vector2 mapPosition, Vector2 mapOffset, float mapScale, float drawScale, int width, int height)
        {
            // MapIconOverlay already receives the native -10*mapScale offset.
            // Only death-marker's vertical anchor correction remains. Pixel to
            // tile conversion occurs here; icon size uses drawScale, not zoom.
            var center = (new Vector2(x / 16f, y / 16f) - mapPosition) * mapScale + mapOffset;
            center.Y -= 2 - mapScale / 5 * 2;
            return new DeathMapGeometry { Center = center,
                Icon = new F5Rect(center.X - width * drawScale / 2, center.Y - height * drawScale / 2, width * drawScale, height * drawScale),
                Hit = new F5Rect(center.X + 4 - 14 * drawScale, center.Y + 2 - 14 * drawScale, 28 * drawScale, 28 * drawScale) };
        }
        internal bool Contains(float x, float y) { return x >= Hit.X && x <= Hit.Right && y >= Hit.Y && y <= Hit.Bottom; }
        internal bool Visible(F5Rect view) { return Icon.Right >= view.X && Icon.X <= view.Right && Icon.Bottom >= view.Y && Icon.Y <= view.Bottom; }
    }
}
