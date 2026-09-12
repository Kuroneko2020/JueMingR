using System;
using JueMingR.Platform.WorldTargets;
using JueMingR.Features.WorldTargets;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.WorldTargets
{
    internal sealed class WorldTargetHostObservation : IWorldTargetSource
    {
        private readonly Func<bool> sessionActive;
        private object tiles, sections;
        private long revision;
        private long geometryRevision;
        private int screenWidth, screenHeight;
        private Matrix lastZoom;
        internal WorldTargetHostObservation(Func<bool> sessionActive) { this.sessionActive = sessionActive; }
        internal void EndSession() { tiles = sections = null; revision++; }
        public bool TryBegin(out WorldTargetView view)
        {
            view = default(WorldTargetView);
            Player player = Main.LocalPlayer;
            // Main.Update postfix runs after native ResetEffects/UpdateEquips
            // (including native team sharing). Never refresh/simulate accessories.
            if (!sessionActive() || Main.gameMenu || Main.dedServ || Main.netMode != 0 && Main.netMode != 1 ||
                player == null || !player.active || !player.accOreFinder || Main.tile == null ||
                Main.netMode == 1 && Main.sectionManager == null) return false;
            if (!ReferenceEquals(tiles, Main.tile) || !ReferenceEquals(sections, Main.sectionManager))
            { tiles = Main.tile; sections = Main.sectionManager; revision++; }
            Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
            if (!Finite(zoom.M11) || !Finite(zoom.M22) || zoom.M11 <= 0 || zoom.M22 <= 0 || Main.screenWidth <= 0 || Main.screenHeight <= 0) return false;
            // Camera sub-tile motion alternates floor/ceil widths by one cell.
            // Only real screen/zoom changes restart discovery, not that rounding.
            if (screenWidth != Main.screenWidth || screenHeight != Main.screenHeight || lastZoom != zoom)
            { screenWidth = Main.screenWidth; screenHeight = Main.screenHeight; lastZoom = zoom; geometryRevision++; }
            Matrix inverse = Matrix.Invert(zoom);
            Vector2 first = Vector2.Transform(Vector2.Zero, inverse), last = Vector2.Transform(new Vector2(Main.screenWidth, Main.screenHeight), inverse);
            if (!Finite(first.X) || !Finite(first.Y) || !Finite(last.X) || !Finite(last.Y) || !Finite(Main.screenPosition.X) || !Finite(Main.screenPosition.Y)) return false;
            if (player.gravDir == -1) { float previous = first.Y; first.Y = Main.screenHeight - last.Y; last.Y = Main.screenHeight - previous; }
            // Observe origins whose arrows may still intersect the screen. This
            // presentation margin is separate from the resolver's four-cell
            // completeness reads (which may reach outside the discovery area).
            int padding = WorldTargetArrows.ObservationPadding;
            int x = Math.Max(0, (int)Math.Floor((first.X + Main.screenPosition.X - padding) / 16));
            int y = Math.Max(0, (int)Math.Floor((first.Y + Main.screenPosition.Y - padding) / 16));
            int right = Math.Min(Main.maxTilesX, (int)Math.Ceiling((last.X + Main.screenPosition.X + padding) / 16));
            int bottom = Math.Min(Main.maxTilesY, (int)Math.Ceiling((last.Y + Main.screenPosition.Y + padding) / 16));
            view = new WorldTargetView(x, y, right - x, bottom - y, revision, geometryRevision);
            return view.Width > 0 && view.Height > 0;
        }
        public WorldTargetTile Read(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY || Main.tile == null ||
                Main.netMode == 1 && (Main.sectionManager == null || !Main.sectionManager.TileLoaded(x, y))) return default(WorldTargetTile);
            // GetTileSafely would allocate into the world on a null cell. A null
            // here is unreadable, and object-check methods have gameplay effects.
            Tile tile = Main.tile[x, y];
            if (tile == null) return default(WorldTargetTile);
            return new WorldTargetTile { Readable = true, Active = tile.active(), Inactive = tile.inActive(),
                Type = tile.type, FrameX = tile.frameX, FrameY = tile.frameY };
        }
        private static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
    }
}
