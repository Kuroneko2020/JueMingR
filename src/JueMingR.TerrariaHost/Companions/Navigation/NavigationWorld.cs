using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum NavigationCellKind : byte { Empty, Solid, Half, Slope, Platform, Door, Trapdoor, Rope, Track }

    internal readonly struct NavigationCell : IEquatable<NavigationCell>
    {
        internal readonly NavigationCellKind Kind;
        internal readonly byte Slope, Liquid, Amount;
        internal readonly ushort Type;
        internal readonly short FrameX, FrameY;
        internal readonly bool Known, RopeOverlap, Active, Actuated;

        internal NavigationCell(NavigationCellKind kind, byte slope, byte liquid, byte amount,
            ushort type, short frameX, short frameY, bool ropeOverlap = false, bool active = false, bool actuated = false)
        {
            Kind = kind;
            Slope = slope;
            Liquid = liquid;
            Amount = amount;
            Type = type;
            FrameX = frameX;
            FrameY = frameY;
            Known = true;
            RopeOverlap = ropeOverlap;
            Active = active;
            Actuated = actuated;
        }

        internal bool Support => Kind == NavigationCellKind.Solid || Kind == NavigationCellKind.Half ||
            Kind == NavigationCellKind.Slope || Kind == NavigationCellKind.Platform ||
            Kind == NavigationCellKind.Trapdoor;

        // Only full blocks split the coarse air span. Traversal checks the
        // occupied part of a shaped tile against the actual body envelope.
        internal bool FullBarrier => Kind == NavigationCellKind.Solid || Kind == NavigationCellKind.Door;
        internal bool Hazard => Amount != 0 && (Liquid == LiquidID.Lava || Liquid == LiquidID.Shimmer);

        // Extents of the occupied vertical band over the overlapped X span.
        // Terraria slopes 1/2 fill below the diagonal and 3/4 fill above it.
        internal void SolidBand(float left, float right, out float top, out float bottom)
        {
            top = 0f;
            bottom = 16f;
            if (Kind == NavigationCellKind.Half) top = 8f;
            else if (Kind == NavigationCellKind.Slope || Kind == NavigationCellKind.Platform)
            {
                switch (Slope)
                {
                    case 1: top = left; break;
                    case 2: top = 16f - right; break;
                    case 3: bottom = 16f - left; break;
                    case 4: bottom = right; break;
                }
            }
        }

        public bool Equals(NavigationCell other) => Known == other.Known && Kind == other.Kind &&
            Slope == other.Slope && Liquid == other.Liquid && Amount == other.Amount && Type == other.Type &&
            FrameX == other.FrameX && FrameY == other.FrameY && RopeOverlap == other.RopeOverlap &&
            Active == other.Active && Actuated == other.Actuated;
        public override bool Equals(object obj) => obj is NavigationCell other && Equals(other);
        public override int GetHashCode() => ((int)Kind << 16) ^ Type ^ (Slope << 8) ^ Amount;
    }

    internal readonly struct NavigationSpan
    {
        internal readonly int Y, Left, Right;
        internal readonly NavigationCellKind Kind;
        internal NavigationSpan(int y, int left, int right, NavigationCellKind kind)
        {
            Y = y;
            Left = left;
            Right = right;
            Kind = kind;
        }
    }

    // Chunk data is shared by all bodies, with no target, stance, failed edge,
    // or acceptance state. A support span describes the tile contour; either
    // gravity and the current body clearance are applied during traversal.
    internal sealed class NavigationChunk
    {
        internal readonly Point Origin;
        internal readonly NavigationCell[] Cells = new NavigationCell[NavigationWorld.ChunkSize * NavigationWorld.ChunkSize];
        internal readonly uint[] VerifiedAt = new uint[NavigationWorld.ChunkSize * NavigationWorld.ChunkSize];
        internal readonly List<NavigationSpan> Surfaces = new List<NavigationSpan>();
        internal readonly List<NavigationSpan> AirSpans = new List<NavigationSpan>();
        private readonly int[] surfaceRows = new int[NavigationWorld.ChunkSize + 1];
        private readonly int[] airRows = new int[NavigationWorld.ChunkSize + 1];
        internal int ReadCount, LastUse, Version, EditVersion, UnknownCells;
        internal bool Complete => ReadCount == Cells.Length;

        internal NavigationChunk(Point origin, int version)
        {
            Origin = origin;
            Version = version;
        }

        internal void IndexRow(int row)
        {
            surfaceRows[row] = Surfaces.Count;
            airRows[row] = AirSpans.Count;
            int supportStart = -1, airStart = -1;
            NavigationCell previous = default;
            for (int x = 0; x <= NavigationWorld.ChunkSize; x++)
            {
                var cell = x < NavigationWorld.ChunkSize ? Cells[row * NavigationWorld.ChunkSize + x] : default;
                bool support = cell.Known && cell.Support;
                bool air = cell.Known && !cell.FullBarrier;
                if (supportStart >= 0 && (!support || previous.Kind != cell.Kind || previous.Slope != cell.Slope))
                {
                    Surfaces.Add(new NavigationSpan(Origin.Y + row, Origin.X + supportStart, Origin.X + x, previous.Kind));
                    supportStart = -1;
                }
                if (airStart >= 0 && !air)
                {
                    AirSpans.Add(new NavigationSpan(Origin.Y + row, Origin.X + airStart, Origin.X + x, NavigationCellKind.Empty));
                    airStart = -1;
                }
                if (support && supportStart < 0) supportStart = x;
                if (air && airStart < 0) airStart = x;
                previous = cell;
            }
            surfaceRows[row + 1] = Surfaces.Count;
            airRows[row + 1] = AirSpans.Count;
        }

        internal bool FindSpan(Point point, bool air, out NavigationSpan result)
        {
            var spans = air ? AirSpans : Surfaces;
            var rows = air ? airRows : surfaceRows;
            int row = point.Y - Origin.Y;
            for (int i = rows[row]; i < rows[row + 1]; i++)
            {
                var span = spans[i];
                if (span.Left > point.X) break;
                if (span.Right <= point.X) continue;
                result = span;
                return true;
            }
            result = default;
            return false;
        }

        internal void Reindex()
        {
            Surfaces.Clear();
            AirSpans.Clear();
            for (int row = 0; row < ReadCount / NavigationWorld.ChunkSize; row++) IndexRow(row);
        }
    }

    internal sealed class NavigationWorld
    {
        internal const int ChunkSize = 8;
        private const int Capacity = 192;
        private const uint VerifyInterval = 90;
        private readonly Dictionary<Point, NavigationChunk> chunks = new Dictionary<Point, NavigationChunk>();
        private WorldTileSource source;
        private int clock;

        internal int Epoch { get; private set; }
        internal int Revision { get; private set; }
        internal int ChunkCount => chunks.Count;

        internal void Synchronize()
        {
            if (source.Current) return;
            chunks.Clear();
            source = WorldTileSource.Capture();
            Epoch++;
            Revision++;
        }

        internal void Reset()
        {
            source = default;
            chunks.Clear();
            clock = 0;
            Epoch++;
            Revision++;
        }

        internal NavigationChunk Discover(Point cell, NavigationBudget budget)
        {
            Synchronize();
            return Ensure(cell, budget);
        }

        internal bool Read(Point point, NavigationBudget budget, out NavigationCell cell)
        {
            cell = default;
            if (!InWorld(point))
            {
                cell = new NavigationCell(NavigationCellKind.Solid, 0, 0, 0, 0, 0, 0);
                return true;
            }
            var chunk = Ensure(point, budget);
            int index = (point.Y - chunk.Origin.Y) * ChunkSize + point.X - chunk.Origin.X;
            if (index >= chunk.ReadCount) return false;
            cell = chunk.Cells[index];
            uint age = unchecked(Main.GameUpdateCount - chunk.VerifiedAt[index]);
            if ((age >= VerifyInterval || !cell.Known && age >= 4 && WorldTileKnowledge.CellKnown(point.X, point.Y)) &&
                budget.TilesRemaining > 0 &&
                TryVerify(point, budget, out bool changed) && changed) cell = chunk.Cells[index];
            return true;
        }

        internal bool TrySpan(Point point, bool air, NavigationBudget budget, out NavigationSpan span, out bool pending)
        {
            span = default;
            pending = false;
            if (!InWorld(point)) return false;
            var chunk = Ensure(point, budget);
            if (chunk.ReadCount < (point.Y - chunk.Origin.Y + 1) * ChunkSize)
            {
                pending = true;
                return false;
            }
            return chunk.FindSpan(point, air, out span);
        }

        internal bool TryVerify(Point point, NavigationBudget budget, out bool changed)
        {
            changed = false;
            if (!InWorld(point)) return true;
            if (!budget.TryTiles()) return false;
            if (!chunks.TryGetValue(OriginOf(point), out var chunk)) return true;
            int index = (point.Y - chunk.Origin.Y) * ChunkSize + point.X - chunk.Origin.X;
            if (index >= chunk.ReadCount) return true;
            var previous = chunk.Cells[index];
            var current = ReadNative(point);
            chunk.VerifiedAt[index] = Main.GameUpdateCount;
            if (previous.Equals(current)) return true;
            chunk.Cells[index] = current;
            chunk.UnknownCells += (current.Known ? 0 : 1) - (previous.Known ? 0 : 1);
            chunk.Version = ++Revision;
            chunk.EditVersion = chunk.Version;
            chunk.Reindex();
            changed = true;
            return true;
        }

        internal static bool InWorld(Point point) =>
            point.X >= 0 && point.Y >= 0 && point.X < Main.maxTilesX && point.Y < Main.maxTilesY;

        internal bool TryVersion(Point point, out Point origin, out int version)
        {
            origin = OriginOf(point);
            version = 0;
            if (!chunks.TryGetValue(origin, out var chunk)) return false;
            version = chunk.Version;
            return true;
        }

        // First discovery of neighboring terrain does not undo a physical
        // rejection already observed against that terrain.
        internal int EditVersion(Point point) => chunks.TryGetValue(OriginOf(point), out var chunk) ? chunk.EditVersion : 0;

        private NavigationChunk Ensure(Point point, NavigationBudget budget)
        {
            var origin = OriginOf(point);
            if (!chunks.TryGetValue(origin, out var chunk))
            {
                if (chunks.Count == Capacity) Evict();
                chunk = new NavigationChunk(origin, ++Revision);
                chunks.Add(origin, chunk);
            }
            chunk.LastUse = ++clock;
            while (!chunk.Complete && budget.TryTiles())
            {
                int i = chunk.ReadCount++;
                var cell = ReadNative(new Point(origin.X + i % ChunkSize, origin.Y + i / ChunkSize));
                chunk.Cells[i] = cell;
                chunk.VerifiedAt[i] = Main.GameUpdateCount;
                if (!cell.Known) chunk.UnknownCells++;
                if (i % ChunkSize == ChunkSize - 1) chunk.IndexRow(i / ChunkSize);
            }
            return chunk;
        }

        private void Evict()
        {
            Point oldest = default;
            int time = int.MaxValue;
            foreach (var item in chunks)
            {
                if (item.Value.LastUse >= time) continue;
                oldest = item.Key;
                time = item.Value.LastUse;
            }
            chunks.Remove(oldest);
        }

        private static Point OriginOf(Point cell) => new Point(
            (int)Math.Floor(cell.X / (float)ChunkSize) * ChunkSize,
            (int)Math.Floor(cell.Y / (float)ChunkSize) * ChunkSize);

        private static NavigationCell ReadNative(Point point)
        {
            if (!InWorld(point)) return new NavigationCell(NavigationCellKind.Solid, 0, 0, 0, 0, 0, 0);
            if (!WorldTileKnowledge.CellKnown(point.X, point.Y)) return default;
            var tile = Main.tile[point.X, point.Y];
            if (tile == null) return default;
            var kind = NavigationCellKind.Empty;
            ushort type = tile.type;
            if (tile.nactive())
            {
                if (type == TileID.ClosedDoor || type == TileID.TallGateClosed) kind = NavigationCellKind.Door;
                else if (type == TileID.TrapdoorClosed) kind = NavigationCellKind.Trapdoor;
                else if (type == TileID.MinecartTrack) kind = NavigationCellKind.Track;
                else if (Main.tileRope[type]) kind = NavigationCellKind.Rope;
                else if (Main.tileSolid[type])
                {
                    if (Main.tileSolidTop[type]) kind = NavigationCellKind.Platform;
                    else if (tile.halfBrick()) kind = NavigationCellKind.Half;
                    else if (tile.slope() != 0) kind = NavigationCellKind.Slope;
                    else kind = NavigationCellKind.Solid;
                }
            }
            bool framed = kind == NavigationCellKind.Door || kind == NavigationCellKind.Trapdoor || kind == NavigationCellKind.Track;
            bool ropeOverlap = tile.nactive() &&
                (type == TileID.MinecartTrack || type == 380 || TileID.Sets.Platforms[type]);
            return new NavigationCell(kind, tile.slope(), tile.liquidType(), tile.liquid,
                tile.active() ? type : (ushort)0, framed ? tile.frameX : (short)0, framed ? tile.frameY : (short)0,
                ropeOverlap, tile.active(), tile.inActive());
        }
    }
}
