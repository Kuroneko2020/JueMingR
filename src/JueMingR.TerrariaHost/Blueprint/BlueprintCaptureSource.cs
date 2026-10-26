using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.Blueprint
{
    // Never hands Tile or metadata references to the worker. Every footprint
    // member passes the same section gate before semantic values are copied.
    internal sealed class BlueprintCaptureSource : IBlueprintCaptureSource
    {
        private readonly Func<bool> session;
        private readonly object tiles, sections;
        private CaptureCell describedCell;
        private BlueprintObjectShape describedShape;
        private bool hasDescription;
        private BlueprintWorldTileWriteState writeState;
        internal BlueprintWorldTileWriteState WriteState { get { return writeState; } }
        private static readonly PropertyInfo alternatesProperty = typeof(TileObjectData).GetProperty("Alternates", BindingFlags.Instance | BindingFlags.NonPublic);
        internal BlueprintCaptureSource(Func<bool> session)
        { this.session = session; tiles = Main.tile; sections = Main.sectionManager; }
        public bool Read(int x, int y, out CaptureCell value)
        {
            value = default(CaptureCell);
            hasDescription = false; describedShape = null; writeState = default(BlueprintWorldTileWriteState);
            if (!session() || !ReferenceEquals(tiles, Main.tile) || !ReferenceEquals(sections, Main.sectionManager) ||
                Main.tile == null || x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY ||
                Main.netMode == 1 && (Main.sectionManager == null || !Main.sectionManager.TileLoaded(x, y))) return false;
            Tile tile = Main.tile[x, y];
            if (tile == null) return false;
            // Preserve inactive slots' actual residual fields for finite door
            // write certificates. CaptureCell still omits absent Tile content;
            // this value uses this same gated Tile, never a second grid lookup.
            writeState = new BlueprintWorldTileWriteState(tile.type, tile.frameX, tile.frameY, tile.sTileHeader, tile.bTileHeader3);
            value.X = x; value.Y = y;
            if (tile.active())
            {
                value.Layers |= CaptureLayer.Tile; value.TileType = tile.type; value.FrameX = tile.frameX; value.FrameY = tile.frameY;
                value.HalfBrick = tile.halfBrick(); value.Slope = tile.slope(); value.Inactive = tile.inActive();
                value.TilePaint = tile.color(); value.InvisibleTile = tile.invisibleBlock(); value.FullbrightTile = tile.fullbrightBlock();
            }
            if (tile.wall != 0)
            {
                value.Layers |= CaptureLayer.Wall; value.WallType = tile.wall; value.WallPaint = tile.wallColor();
                value.InvisibleWall = tile.invisibleWall(); value.FullbrightWall = tile.fullbrightWall();
            }
            value.Wires = (byte)((tile.wire() ? 1 : 0) | (tile.wire2() ? 2 : 0) | (tile.wire3() ? 4 : 0) | (tile.wire4() ? 8 : 0));
            if (value.Wires != 0) value.Layers |= CaptureLayer.Wire;
            value.Actuator = tile.actuator(); if (value.Actuator) value.Layers |= CaptureLayer.Actuator;
            if (tile.liquid != 0) { value.Layers |= CaptureLayer.Liquid; value.LiquidAmount = tile.liquid; value.LiquidKind = tile.liquidType(); }
            // Resolve against this exact gated Tile before returning its copy.
            // Describe never rereads the world or hides reads outside the budget.
            describedCell = value; describedShape = DescribeRead(value, tile); hasDescription = true;
            return true;
        }
        public BlueprintObjectShape Describe(CaptureCell cell)
        {
            if (!hasDescription || cell.X != describedCell.X || cell.Y != describedCell.Y || cell.TileType != describedCell.TileType ||
                cell.FrameX != describedCell.FrameX || cell.FrameY != describedCell.FrameY || cell.Layers != describedCell.Layers)
                return Unknown(cell, "对象读取样本已失效");
            return describedShape;
        }
        // World matching reuses this exact metadata decoder on copied values.
        // Its canonical equivalence rules live separately and never change the
        // capture's raw DTO, stable-group checks or output format.
        internal static BlueprintObjectShape DescribeRead(CaptureCell cell, Tile tile)
        {
            if ((cell.Layers & CaptureLayer.Tile) == 0 || cell.TileType >= Main.tileFrameImportant.Length || !Main.tileFrameImportant[cell.TileType]) return null;
            try { return Resolve(cell, tile); }
            catch (ArgumentException) { return Unknown(cell, "对象元数据无法确认"); }
            catch (IndexOutOfRangeException) { return Unknown(cell, "对象帧或范围无法确认"); }
            catch (OverflowException) { return Unknown(cell, "对象帧或范围超出可确认范围"); }
        }
        private static BlueprintObjectShape Resolve(CaptureCell cell, Tile tile)
        {
            // In the locked original API, style 0 may select SubTiles[0]
            // (detritus/piles change dimensions there). An out-of-table style
            // returns the registered base; decode the real style only after
            // obtaining that grid. This never supplies a placement request.
            TileObjectData basis = TileObjectData.GetTileData(cell.TileType, int.MaxValue);
            // Frame-important is a rendering flag, not a rectangular-object
            // contract. Trees and other unregistered content are selected-cell
            // snapshots, with raw frames and no invented placement proof.
            if (basis == null) return null;
            if (basis.CoordinateFullWidth <= 0 || basis.CoordinateFullHeight <= 0 || basis.StyleMultiplier <= 0 || cell.FrameX < 0 || cell.FrameY < 0)
                return Unknown(cell, "此对象缺少可确认的完整外观范围");
            int frameColumn = cell.FrameX / basis.CoordinateFullWidth, frameRow = cell.FrameY / basis.CoordinateFullHeight;
            int wrap = Math.Max(1, basis.StyleWrapLimit);
            int placement = basis.StyleHorizontal ? checked(frameRow * wrap + frameColumn) : checked(frameColumn * wrap + frameRow);
            int style = placement / basis.StyleMultiplier, variant = placement % basis.StyleMultiplier;
            if (basis.StyleLineSkip > 1)
            {
                int line = basis.StyleHorizontal ? frameRow : frameColumn;
                int primary = basis.StyleHorizontal ? frameColumn : frameRow;
                style = checked(line / basis.StyleLineSkip * wrap + primary); variant = line % basis.StyleLineSkip;
            }
            TileObjectData main = TileObjectData.GetTileData(cell.TileType, style), data = TileObjectData.GetTileData(tile);
            int alternate = 0;
            if (main == null || data == null) return Unknown(cell, "对象样式元数据缺失");
            // Alternates is private in the locked 1.4.5.8 ABI. The reflected
            // member is resolved once; unavailable metadata rejects this object
            // instead of guessing a direction or bypassing the world read gate.
            if (alternatesProperty == null || alternatesProperty.PropertyType != typeof(List<TileObjectData>)) return Unknown(cell, "对象方向元数据不可用");
            var alternates = (List<TileObjectData>)alternatesProperty.GetValue(main, null);
            if (alternates != null)
                for (int i = 0; i < alternates.Count; i++)
                {
                    TileObjectData candidate = alternates[i];
                    if (ReferenceEquals(candidate, data)) { alternate = i + 1; break; }
                }
            // PlaceDoor/CloseDoor choose X independently for all three rows.
            // Those slots are decoration, not placement alternates (whose only
            // difference here is the clicked placement origin). Keep one stable
            // building style while retaining every member's original raw X.
            bool independent = cell.TileType == TileID.ClosedDoor;
            int overrideBaseY = 0;
            if (independent)
            {
                style = checked(cell.FrameX / 54 * 36 + cell.FrameY / 54);
                main = TileObjectData.GetTileData(cell.TileType, style); data = main;
                alternate = 0; placement = style; variant = 0;
            }
            else if (basis.GetStyleOverride != null)
            {
                // These are the locked original selector mechanisms, not tile
                // type allowlists. Their Y regions encode shape/attachment,
                // rather than a generic vertical style grid (notably Y=90).
                if (basis.GetStyleOverride == TileObjectData.GetStyle_Detritus) overrideBaseY = cell.FrameY >= 36 ? 36 : 0;
                else if (basis.GetStyleOverride == TileObjectData.GetStyle_SmallPiles) overrideBaseY = cell.FrameY >= 18 ? 18 : 0;
                else if (basis.GetStyleOverride == TileObjectData.GetStyle_Stalactite)
                    overrideBaseY = cell.FrameY >= 90 ? 90 : cell.FrameY >= 72 ? 72 : cell.FrameY >= 36 ? 36 : 0;
                else return Unknown(cell, "对象形态的帧归属规则尚无法确认");
                style = cell.FrameX / data.CoordinateFullWidth;
                placement = style; variant = 0; alternate = 0;
            }
            if (data == null) return Unknown(cell, "对象样式元数据缺失");
            if (data.Width <= 0 || data.Height <= 0 || data.CoordinateWidth <= 0 || data.CoordinatePadding < 0 ||
                data.CoordinateFullWidth <= 0 || data.CoordinateFullHeight <= 0 || data.CoordinateHeights == null || data.CoordinateHeights.Length != data.Height)
                return Unknown(cell, "对象尺寸元数据不完整");
            if ((long)data.Width * data.Height >= CaptureChunk.MaximumRecords) return Unknown(cell, "此对象超出单组外观证明范围");
            int step = checked(data.CoordinateWidth + data.CoordinatePadding);
            int baseX = independent ? cell.FrameX / 54 * 54 : cell.FrameX / data.CoordinateFullWidth * data.CoordinateFullWidth;
            int baseY = basis.GetStyleOverride != null ? overrideBaseY : cell.FrameY / data.CoordinateFullHeight * data.CoordinateFullHeight;
            int localX = cell.FrameX - baseX, localY = cell.FrameY - baseY;
            if (localX % step != 0 || (independent ? localX >= 54 : localX / step >= data.Width) ||
                checked(step * data.Width + data.CoordinatePaddingFix.X) != data.CoordinateFullWidth) return Unknown(cell, "对象横向帧不对齐");
            var offsets = new int[data.Height]; int row = -1, cumulative = 0;
            for (int i = 0; i < data.Height; i++)
            {
                offsets[i] = cumulative; if (localY == cumulative) row = i;
                if (data.CoordinateHeights[i] <= 0) return Unknown(cell, "对象纵向帧不完整");
                cumulative = checked(cumulative + data.CoordinateHeights[i] + data.CoordinatePadding);
            }
            // Full style stride includes PaddingFix; member starts do not.
            // Beds have -2 and chairs/toilets +2, so summing row steps alone
            // cannot validate their complete style span.
            if (row < 0 || checked(cumulative + data.CoordinatePaddingFix.Y) != data.CoordinateFullHeight) return Unknown(cell, "对象纵向帧不对齐");
            return new BlueprintObjectShape { BaseFrameX = baseX, BaseFrameY = baseY, ColumnStep = step, RowOffsets = offsets, IndependentMemberFrames = independent,
                Object = new CaptureObject { X = cell.X - (independent ? 0 : localX / step), Y = cell.Y - row, Width = data.Width, Height = data.Height,
                    Type = cell.TileType, Style = style, Alternate = alternate, Direction = (int)data.Direction,
                    OriginX = data.Origin.X, OriginY = data.Origin.Y, PlacementStyle = placement, RandomVariant = variant,
                    Reliable = true } };
        }
        private static BlueprintObjectShape Unknown(CaptureCell cell, string reason)
        { return new BlueprintObjectShape { Object = new CaptureObject { X = cell.X, Y = cell.Y, Width = 1, Height = 1, Type = cell.TileType, Reliable = false, Reason = reason } }; }
    }
}
