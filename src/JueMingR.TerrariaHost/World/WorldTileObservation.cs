using System;
using System.Collections.Generic;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.World
{
    // Composition starts one cache epoch before Runtime Update. Shared fields
    // carry no consumer's accessory, enablement or freshness policy.
    internal sealed class WorldTileObservation
    {
        private readonly Func<bool> sessionActive;
        private readonly Dictionary<long, WorldTargetTile> cache = new Dictionary<long, WorldTargetTile>();
        private object tiles, sections;
        private long revision, geometryRevision;
        private int screenWidth, screenHeight;
        private Matrix lastZoom;
        private bool prepared, available;
        private Vector2 first, last;
        internal WorldTileObservation(Func<bool> sessionActive) { this.sessionActive = sessionActive; }
        internal void BeginTick() { cache.Clear(); prepared = available = false; CheckIdentity(); }
        internal void EndSession() { cache.Clear(); tiles = sections = null; prepared = available = false; revision++; }
        private void CheckIdentity()
        {
            if (ReferenceEquals(tiles, Main.tile) && ReferenceEquals(sections, Main.sectionManager)) return;
            tiles = Main.tile; sections = Main.sectionManager; cache.Clear(); prepared = available = false; revision++;
        }
        internal bool TryView(int padding, out WorldTargetView view)
        {
            view = default(WorldTargetView); CheckIdentity();
            if (!prepared) { prepared = true; available = Prepare(); }
            if (!available) return false;
            int x = Math.Max(0, (int)Math.Floor((first.X + Main.screenPosition.X - padding) / 16));
            int y = Math.Max(0, (int)Math.Floor((first.Y + Main.screenPosition.Y - padding) / 16));
            int right = Math.Min(Main.maxTilesX, (int)Math.Ceiling((last.X + Main.screenPosition.X + padding) / 16));
            int bottom = Math.Min(Main.maxTilesY, (int)Math.Ceiling((last.Y + Main.screenPosition.Y + padding) / 16));
            view = new WorldTargetView(x, y, right - x, bottom - y, revision, geometryRevision);
            return view.Width > 0 && view.Height > 0;
        }
        private bool Prepare()
        {
            Player player = Main.LocalPlayer;
            if (!sessionActive() || Main.gameMenu || Main.dedServ || Main.netMode != 0 && Main.netMode != 1 ||
                player == null || !player.active || Main.tile == null || Main.netMode == 1 && Main.sectionManager == null) return false;
            Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
            if (!Finite(zoom.M11) || !Finite(zoom.M22) || zoom.M11 <= 0 || zoom.M22 <= 0 || Main.screenWidth <= 0 || Main.screenHeight <= 0) return false;
            if (screenWidth != Main.screenWidth || screenHeight != Main.screenHeight || lastZoom != zoom)
            { screenWidth = Main.screenWidth; screenHeight = Main.screenHeight; lastZoom = zoom; geometryRevision++; }
            Matrix inverse = Matrix.Invert(zoom);
            first = Vector2.Transform(Vector2.Zero, inverse); last = Vector2.Transform(new Vector2(Main.screenWidth, Main.screenHeight), inverse);
            if (!Finite(first.X) || !Finite(first.Y) || !Finite(last.X) || !Finite(last.Y) || !Finite(Main.screenPosition.X) || !Finite(Main.screenPosition.Y)) return false;
            if (player.gravDir == -1) { float previous = first.Y; first.Y = Main.screenHeight - last.Y; last.Y = Main.screenHeight - previous; }
            return true;
        }
        internal WorldTargetTile Read(int x, int y)
        {
            CheckIdentity(); long key = ((long)x << 32) | (uint)y; WorldTargetTile value;
            if (cache.TryGetValue(key, out value)) return value;
            value = ReadCurrent(x, y);
            // Both bounded discovery consumers fit below this retained limit.
            // Overflow remains correct without growing the retained cache.
            if (cache.Count < 65536) cache.Add(key, value);
            return value;
        }
        private static WorldTargetTile ReadCurrent(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY || Main.tile == null ||
                Main.netMode == 1 && (Main.sectionManager == null || !Main.sectionManager.TileLoaded(x, y))) return default(WorldTargetTile);
            Tile tile = Main.tile[x, y];
            if (tile == null) return default(WorldTargetTile);
            return new WorldTargetTile { Readable = true, Active = tile.active(), Inactive = tile.inActive(), Type = tile.type, FrameX = tile.frameX, FrameY = tile.frameY };
        }
        private static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
    }
}
