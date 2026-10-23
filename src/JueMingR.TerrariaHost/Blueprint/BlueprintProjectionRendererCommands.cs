using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal enum BlueprintProjectionTexture { Tile, Wall, Extra, Pixel }

    internal struct BlueprintProjectionCommand
    {
        internal BlueprintProjectionTexture Texture;
        internal int Type, Paint;
        internal Rectangle Source;
        internal Vector2 Position;
        internal Color Color;
        internal SpriteEffects Effects;
        // A special building body uses a frame grid whose pixel size belongs
        // to the borrowed texture. Ordinary tile slices always use raw pixels.
        internal int Columns, Rows;
        internal bool Centered;
    }

    // Interprets an immutable, bounded source window only. No Tile instances,
    // destination ecology, world RNG, placement preview or entity are involved.
    // Commands stay in local pixels; moving a projection never rebuilds them.
    internal static class BlueprintProjectionRendererCommands
    {
        internal static List<BlueprintProjectionCommand> Build(CaptureSummary summary, IReadOnlyList<CaptureCell> cells,
            IReadOnlyList<CaptureObject> objects, int left, int top, int right, int bottom)
        {
            var commands = new List<BlueprintProjectionCommand>();
            var lookup = new Dictionary<long, CaptureCell>(cells.Count);
            var groups = new Dictionary<int, CaptureObject>(objects.Count);
            var samples = new Dictionary<int, CaptureCell>();
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (Has(cell, CaptureLayer.Tile) && (cell.TileType >= TileID.Count || cell.TilePaint > 31) ||
                    Has(cell, CaptureLayer.Wall) && (cell.WallType >= WallID.Count || cell.WallPaint > 31))
                    throw new System.IO.InvalidDataException("blueprint-projection-native-type-or-paint-unavailable");
                lookup[Key(cells[i].X, cells[i].Y)] = cells[i];
                if (cells[i].ObjectId != 0 && !samples.ContainsKey(cells[i].ObjectId)) samples.Add(cells[i].ObjectId, cells[i]);
            }
            for (int i = 0; i < objects.Count; i++) groups[objects[i].Id] = objects[i];
            // The outer ring is lookup-only. Missing records inside a completed
            // window mean air; outside it they mean unknown, so never derive a
            // permanent edge from that outer ring. Summary edges are known air.
            int l = left <= 0 ? 0 : left + 1, t = top <= 0 ? 0 : top + 1;
            int r = right >= summary.Width ? summary.Width : right - 1;
            int b = bottom >= summary.Height ? summary.Height : bottom - 1;
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < cells.Count; i++)
                {
                    var cell = cells[i];
                    if (cell.X < l || cell.Y < t || cell.X >= r || cell.Y >= b) continue;
                    if (pass == 0 && Has(cell, CaptureLayer.Wall)) Wall(commands, lookup, cell);
                    if (pass == 1 && Has(cell, CaptureLayer.Liquid)) Liquid(commands, cell);
                    if (pass == 2 && Has(cell, CaptureLayer.Tile))
                    {
                        CaptureObject obj; groups.TryGetValue(cell.ObjectId, out obj);
                        Tile(commands, lookup, cell, obj, summary.Left, summary.Top);
                    }
                }
            // Pylons have a separate native crystal sprite above the raw tile
            // base. One body per referenced object, even when its origin is off
            // screen; member tiles are still emitted exactly once above.
            for (int i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];
                if (!obj.Reliable || obj.Type != 597) continue;
                CaptureCell sample; if (!samples.TryGetValue(obj.Id, out sample)) continue;
                commands.Add(new BlueprintProjectionCommand { Texture = BlueprintProjectionTexture.Extra, Type = 181,
                    Source = new Rectangle(3 + Math.Max(0, Math.Min(10, obj.Style)), 0, 1, 1), Columns = 14, Rows = 8,
                    Position = new Vector2(obj.X * 16 + 24, obj.Y * 16 + 24), Centered = true, Color = Ink(sample, false) * .9f });
            }
            // Circuit layers deliberately remain visible without the player's
            // wiring tool. They never alter the real-world wiring visibility.
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i]; if (c.X < l || c.Y < t || c.X >= r || c.Y >= b) continue;
                if (Has(c, CaptureLayer.Wire)) Wire(commands, lookup, c);
                if (Has(c, CaptureLayer.Actuator))
                {
                    Pixel(commands, c.X * 16 + 5, c.Y * 16 + 5, 6, 1, Color.Orange * .8f);
                    Pixel(commands, c.X * 16 + 5, c.Y * 16 + 10, 6, 1, Color.Orange * .8f);
                    Pixel(commands, c.X * 16 + 5, c.Y * 16 + 6, 1, 4, Color.Orange * .8f);
                    Pixel(commands, c.X * 16 + 10, c.Y * 16 + 6, 1, 4, Color.Orange * .8f);
                }
            }
            return commands;
        }

        internal static void Shape(List<BlueprintProjectionCommand> output, BlueprintProjectionCommand normal, byte slope, bool half,
            bool platform, bool dontSlope, bool slopeFrames)
        {
            if (slope > 0 && slope <= 4 && !dontSlope)
            {
                if (platform || slopeFrames) { output.Add(normal); return; }
                // Locked .8 geometry: eight two-pixel strips plus a two-pixel
                // cap. Upper/lower slopes use different source Y, not a scaled
                // triangle. The final zero-height strip needs no GPU command.
                for (int i = 0; i < 8; i++)
                {
                    int x = slope == 1 || slope == 3 ? i * 2 : 14 - i * 2;
                    int sy = slope <= 2 ? 0 : i * 2;
                    int height = slope <= 2 ? 14 - i * 2 : 16 - i * 2;
                    if (height <= 0) continue;
                    Slice(output, normal, x, sy, 2, height, x, slope <= 2 ? i * 2 : 0);
                }
                int edge = slope <= 2 ? 14 : 0;
                Slice(output, normal, 0, edge, 16, 2, 0, edge); return;
            }
            if (half)
            {
                // Native normalTileRect keeps the original top source rows and
                // shortens height; target moves down eight. Never squash 16px.
                Slice(output, normal, 0, 0, normal.Source.Width, Math.Max(0, normal.Source.Height - 8), 0, 8); return;
            }
            output.Add(normal);
        }

        internal static bool Clip(ref Vector2 worldPosition, ref Rectangle source, ref Vector2 scale, SpriteEffects effects, bool pixel, float worldWidth, float worldHeight)
        {
            float width = source.Width * scale.X, height = source.Height * scale.Y;
            if (worldPosition.X >= worldWidth || worldPosition.Y >= worldHeight || worldPosition.X + width <= 0 || worldPosition.Y + height <= 0) return false;
            if (pixel)
            {
                float l = Math.Max(0, -worldPosition.X), t = Math.Max(0, -worldPosition.Y);
                float r = Math.Max(0, worldPosition.X + width - worldWidth), b = Math.Max(0, worldPosition.Y + height - worldHeight);
                worldPosition += new Vector2(l, t); scale = new Vector2(width - l - r, height - t - b); return scale.X > 0 && scale.Y > 0;
            }
            // Texture commands are sampled 1:1. Crop source and destination by
            // the same pixel count; flipping chooses the opposite source edge.
            // This clips an out-of-bounds preview without silently moving its
            // origin or introducing another SpriteBatch/scissor transform.
            int left = Math.Max(0, (int)Math.Ceiling(-worldPosition.X)), top = Math.Max(0, (int)Math.Ceiling(-worldPosition.Y));
            int right = Math.Max(0, (int)Math.Ceiling(worldPosition.X + width - worldWidth));
            int bottom = Math.Max(0, (int)Math.Ceiling(worldPosition.Y + height - worldHeight));
            source.X += (effects & SpriteEffects.FlipHorizontally) != 0 ? right : left;
            source.Y += (effects & SpriteEffects.FlipVertically) != 0 ? bottom : top;
            source.Width -= left + right; source.Height -= top + bottom;
            worldPosition += new Vector2(left, top); return source.Width > 0 && source.Height > 0;
        }

        private static void Tile(List<BlueprintProjectionCommand> result, Dictionary<long, CaptureCell> cells, CaptureCell c,
            CaptureObject obj, int sourceLeft, int sourceTop)
        {
            int width = 16, height = 16, dx = 0, dy = 0, fx = c.FrameX, fy = c.FrameY;
            SpriteEffects effects = SpriteEffects.None;
            if (obj.Id != 0 && obj.Reliable)
            {
                var data = TileObjectData.GetTileData(obj.Type, obj.Style, obj.Alternate);
                int row = c.Y - obj.Y, col = c.X - obj.X;
                if (data != null && row >= 0 && row < data.Height && col >= 0 && col < data.Width)
                {
                    width = data.CoordinateWidth; height = data.CoordinateHeights[row];
                    dx = -data.DrawXOffset; dy = data.DrawYOffset;
                    if (data.DrawStyleOffset != 0)
                    {
                        // Native visual pages can live in another atlas bank
                        // (e.g. furniture raw Y >= 2016). Shift only the style
                        // bank, retaining each member's original frame offset;
                        // DrawFrameOffsets below then chooses its visible bank.
                        Point stored = FrameBase(data, obj.PlacementStyle, false);
                        Point visible = FrameBase(data, checked(obj.PlacementStyle + data.DrawStyleOffset), true);
                        fx = checked(fx + visible.X - stored.X); fy = checked(fy + visible.Y - stored.Y);
                    }
                    if (!TileID.Sets.DoNotAdjustDrawPositionBasedOnTileWidth[c.TileType]) dx -= (width - 16) / 2;
                    if (data.DrawFlipHorizontal && ((c.X + sourceLeft) & 1) == 0) effects |= SpriteEffects.FlipHorizontally;
                    if (data.DrawFlipVertical && ((c.Y + sourceTop) & 1) == 0) effects |= SpriteEffects.FlipVertically;
                    if (data.DrawFrameOffsets != null)
                    {
                        var offset = data.DrawFrameOffsets[col, row];
                        fx += offset.X; fy += offset.Y; width += offset.Width; height += offset.Height;
                    }
                    if (data.DrawStepDown != 0 && Solid(Get(cells, obj.X + col, obj.Y - 1))) dy += data.DrawStepDown;
                }
            }
            // Natural snapshots (ObjectId zero is legal) and registered grass
            // both use native visible extents. Flip is tied to saved source X.
            switch (c.TileType)
            {
                case 3: case 24: case 61: case 71: case 110: case 201: case 637: case 703:
                    height = 20; effects = SourceFlip(c.X + sourceLeft); break;
                case 82: case 83: case 84:
                    height = 20; dy = -2; effects = SourceFlip(c.X + sourceLeft); break;
                case 73: case 74: case 113:
                    height = 32; dy = -12; effects = SourceFlip(c.X + sourceLeft); break;
                case 324:
                    width = height = 20; dx = -2; dy = -2; effects = SourceFlip(c.X + sourceLeft); break;
                case 5: width = height = 20; dx = -2; break;
                // v1 has no palm biome. Style zero is a deterministic same-type
                // representative, never inferred from destination terrain.
                case 323: width = height = 20; dx = -2; fy = 0; break;
            }
            var normal = new BlueprintProjectionCommand { Texture = BlueprintProjectionTexture.Tile, Type = c.TileType, Paint = c.TilePaint,
                Source = new Rectangle(fx, fy, width, height), Position = new Vector2(c.X * 16 + dx, c.Y * 16 + dy), Color = Ink(c, false), Effects = effects };
            bool platform = TileID.Sets.Platforms[c.TileType], noSlope = TileID.Sets.DontDrawTileSlopes[c.TileType];
            if (!noSlope && platform && c.Slope > 0)
            {
                result.Add(normal);
                var below = Get(cells, c.X, c.Y + 1);
                var side = Get(cells, c.X + (c.Slope == 1 ? 1 : -1), c.Y + 1);
                bool blocks = Solid(below) && (below.Slope == 0 || below.Slope == (c.Slope == 1 ? 3 : 4)) && TileID.Sets.BlocksStairs[below.TileType];
                if ((c.Slope == 1 || c.Slope == 2) && Solid(side) && !side.HalfBrick && side.Slope != (c.Slope == 1 ? 2 : 1) && !blocks)
                {
                    var join = normal; join.Position.Y += 16;
                    join.Source = new Rectangle(TileID.Sets.Platforms[side.TileType] && side.Slope == 0 ? (c.Slope == 1 ? 324 : 306) : (c.Slope == 1 ? 198 : 162), fy, 16, 16);
                    result.Add(join);
                }
                return;
            }
            if (c.Slope == 0 && !c.HalfBrick && !platform && !noSlope && Main.tileSolid[c.TileType] &&
                !TileID.Sets.NotReallySolid[c.TileType] && !TileID.Sets.IgnoresNearbyHalfbricksWhenDrawn[c.TileType])
            {
                var west = Get(cells, c.X - 1, c.Y); var east = Get(cells, c.X + 1, c.Y);
                if (west.HalfBrick || east.HalfBrick) { HalfJoin(result, normal, west.HalfBrick, east.HalfBrick, Get(cells, c.X, c.Y - 1), c.TileType); return; }
            }
            if (c.Slope == 0 && c.HalfBrick && !platform)
            {
                var below = Get(cells, c.X, c.Y + 1);
                if (!Has(below, CaptureLayer.Tile) || !Main.tileSolid[below.TileType] || below.HalfBrick)
                {
                    // The exposed native half-brick bottom has its own four
                    // pixel edge. Both pieces retain 1:1 source sampling.
                    Slice(result, normal, 0, 0, width, Math.Max(0, height - 12), 0, 8);
                    var edge = normal; edge.Source = new Rectangle(144, 66, width, 4); edge.Position.Y += 12; result.Add(edge); return;
                }
            }
            Shape(result, normal, c.Slope, c.HalfBrick, platform, noSlope, TileID.Sets.HasSlopeFrames[c.TileType]);
        }

        private static void HalfJoin(List<BlueprintProjectionCommand> output, BlueprintProjectionCommand n, bool west, bool east, CaptureCell above, ushort type)
        {
            Slice(output, n, 0, 8, n.Source.Width, 8, 0, 8);
            if (west && east)
            {
                var edge = n; edge.Source = new Rectangle(Has(above, CaptureLayer.Tile) && above.TileType == type && above.Slope != 3 && above.Slope != 4 ? 90 : 126, 0, 16, 8); output.Add(edge); return;
            }
            int inset = TileID.Sets.AllBlocksWithSmoothBordersToResolveHalfBlockIssue[type] ? 2 : 4;
            Slice(output, n, west ? inset : 0, 0, n.Source.Width - inset, n.Source.Height, west ? inset : 0, 0);
            var cap = n; cap.Position.X += west ? 0 : 16 - inset; cap.Source = new Rectangle(west ? 144 : 160 - inset, 0, inset, 8); output.Add(cap);
            if (inset == 2) { var corner = cap; corner.Source = new Rectangle(west ? 148 : 156, 0, 2, 2); output.Add(corner); }
        }
        private static Point FrameBase(TileObjectData data, int placement, bool visual)
        {
            if (placement < 0) throw new System.IO.InvalidDataException("blueprint-projection-style-bank-invalid");
            int wrap = visual ? data.StyleWrapLimitVisualOverride ?? data.StyleWrapLimit : data.StyleWrapLimit;
            int skip = visual ? data.styleLineSkipVisualOverride ?? data.StyleLineSkip : data.StyleLineSkip;
            int line = wrap > 0 ? checked(placement / wrap * skip) : 0, item = wrap > 0 ? placement % wrap : placement;
            return data.StyleHorizontal ? new Point(checked(item * data.CoordinateFullWidth), checked(line * data.CoordinateFullHeight)) :
                new Point(checked(line * data.CoordinateFullWidth), checked(item * data.CoordinateFullHeight));
        }

        private static readonly Point[] WallFrames = { new Point(9,3), new Point(6,3), new Point(12,0), new Point(1,4),
            new Point(9,0), new Point(0,4), new Point(6,4), new Point(1,2), new Point(6,0), new Point(5,0), new Point(1,3),
            new Point(4,0), new Point(0,3), new Point(0,0), new Point(1,0), new Point(1,1) };
        private static void Wall(List<BlueprintProjectionCommand> result, Dictionary<long, CaptureCell> cells, CaptureCell c)
        {
            // v1 did not save random wall frames. Native mask order is N/W/E/S;
            // use one fixed original variant for a stable 32x32 adjacency frame.
            int mask = (WallNeighbor(Get(cells, c.X, c.Y - 1)) ? 1 : 0) | (WallNeighbor(Get(cells, c.X - 1, c.Y)) ? 2 : 0) |
                (WallNeighbor(Get(cells, c.X + 1, c.Y)) ? 4 : 0) | (WallNeighbor(Get(cells, c.X, c.Y + 1)) ? 8 : 0);
            var frame = WallFrames[mask];
            result.Add(new BlueprintProjectionCommand { Texture = BlueprintProjectionTexture.Wall, Type = c.WallType, Paint = c.WallPaint,
                Source = new Rectangle(frame.X * 36, frame.Y * 36, 32, 32), Position = new Vector2(c.X * 16 - 8, c.Y * 16 - 8), Color = Ink(c, true) });
        }
        private static bool WallNeighbor(CaptureCell c) { return Has(c, CaptureLayer.Wall) || Has(c, CaptureLayer.Tile) && TileID.Sets.TruncatesWalls[c.TileType]; }
        private static void Liquid(List<BlueprintProjectionCommand> result, CaptureCell c)
        {
            int height = Math.Max(1, (c.LiquidAmount * 16 + 254) / 255);
            // A static volume layer: source liquid kind/amount only, no native
            // liquid simulation, destination surface or biome water selection.
            Color ink = c.LiquidKind == 1 ? new Color(255, 105, 35) : c.LiquidKind == 2 ? new Color(240, 174, 35) :
                c.LiquidKind == 3 ? new Color(197, 125, 244) : new Color(60, 135, 245);
            Pixel(result, c.X * 16, c.Y * 16 + 16 - height, 16, height, ink * .42f);
        }
        private static readonly Color[] WireColors = { Color.Red, Color.Blue, Color.LimeGreen, Color.Yellow };
        private static void Wire(List<BlueprintProjectionCommand> result, Dictionary<long, CaptureCell> cells, CaptureCell c)
        {
            for (int bit = 0; bit < 4; bit++)
            {
                int mask = 1 << bit; if ((c.Wires & mask) == 0) continue;
                int center = 5 + bit * 2, x = c.X * 16, y = c.Y * 16; var color = WireColors[bit] * .85f;
                bool w = (Get(cells, c.X - 1, c.Y).Wires & mask) != 0, e = (Get(cells, c.X + 1, c.Y).Wires & mask) != 0;
                bool n = (Get(cells, c.X, c.Y - 1).Wires & mask) != 0, s = (Get(cells, c.X, c.Y + 1).Wires & mask) != 0;
                Pixel(result, x + (w ? 0 : center - 1), y + center, (e ? 16 : center + 2) - (w ? 0 : center - 1), 1, color);
                Pixel(result, x + center, y + (n ? 0 : center - 1), 1, (s ? 16 : center + 2) - (n ? 0 : center - 1), color);
            }
        }
        private static SpriteEffects SourceFlip(int x) { return (x & 1) == 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None; }
        private static Color Ink(CaptureCell c, bool wall)
        {
            float opacity = .58f;
            if (wall ? c.InvisibleWall : c.InvisibleTile) opacity *= .5f;
            if (!wall && c.Inactive) opacity *= .65f;
            // Overlay brightness is deliberately independent of destination
            // light; fullbright and invisible stored content never disappears.
            return Color.White * opacity;
        }
        private static void Slice(List<BlueprintProjectionCommand> result, BlueprintProjectionCommand n, int sx, int sy, int width, int height, int dx, int dy)
        { if (width <= 0 || height <= 0) return; n.Source = new Rectangle(n.Source.X + sx, n.Source.Y + sy, width, height); n.Position += new Vector2(dx, dy); result.Add(n); }
        private static void Pixel(List<BlueprintProjectionCommand> result, int x, int y, int width, int height, Color color)
        { result.Add(new BlueprintProjectionCommand { Texture = BlueprintProjectionTexture.Pixel, Source = new Rectangle(0, 0, width, height), Position = new Vector2(x, y), Color = color }); }
        private static bool Has(CaptureCell c, CaptureLayer layer) { return (c.Layers & layer) != 0; }
        private static bool Solid(CaptureCell c) { return Has(c, CaptureLayer.Tile) && !c.Inactive && Main.tileSolid[c.TileType]; }
        private static long Key(int x, int y) { return ((long)x << 32) | (uint)y; }
        private static CaptureCell Get(Dictionary<long, CaptureCell> cells, int x, int y) { CaptureCell c; return cells.TryGetValue(Key(x, y), out c) ? c : default(CaptureCell); }
    }
}
