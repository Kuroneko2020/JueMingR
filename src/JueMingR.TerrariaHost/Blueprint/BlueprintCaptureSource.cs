using System;
using System.Collections.Generic;
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
            // The native resolver uses registered layouts, including sheet
            // alternates that GetTileData(Tile) cannot recover by modulo alone.
            // An identified but invalid layout stays Unknown, never natural air.
            BlueprintObjectShape shape;
            return BlueprintHorizontalMirrorResolver.TryDescribeNative(cell, out shape) ? shape : null;
        }
        private static BlueprintObjectShape Unknown(CaptureCell cell, string reason)
        { return new BlueprintObjectShape { Object = new CaptureObject { X = cell.X, Y = cell.Y, Width = 1, Height = 1, Type = cell.TileType, Reliable = false, Reason = reason } }; }
    }
}
