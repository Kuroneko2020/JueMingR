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
        private static readonly PropertyInfo alternatesProperty = typeof(TileObjectData).GetProperty("Alternates", BindingFlags.Instance | BindingFlags.NonPublic);
        internal BlueprintCaptureSource(Func<bool> session)
        { this.session = session; tiles = Main.tile; sections = Main.sectionManager; }
        public bool Read(int x, int y, out CaptureCell value)
        {
            value = default(CaptureCell);
            if (!session() || !ReferenceEquals(tiles, Main.tile) || !ReferenceEquals(sections, Main.sectionManager) ||
                Main.tile == null || x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY ||
                Main.netMode == 1 && (Main.sectionManager == null || !Main.sectionManager.TileLoaded(x, y))) return false;
            Tile tile = Main.tile[x, y];
            if (tile == null) return false;
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
            return true;
        }
        public BlueprintObjectShape Describe(CaptureCell cell)
        {
            if ((cell.Layers & CaptureLayer.Tile) == 0 || cell.TileType >= Main.tileFrameImportant.Length || !Main.tileFrameImportant[cell.TileType]) return null;
            try { return Resolve(cell); }
            catch (ArgumentException) { return Unknown(cell, "对象元数据无法确认"); }
            catch (IndexOutOfRangeException) { return Unknown(cell, "对象帧或范围无法确认"); }
            catch (OverflowException) { return Unknown(cell, "对象帧或范围超出可确认范围"); }
        }
        private static BlueprintObjectShape Resolve(CaptureCell cell)
        {
            TileObjectData basis = TileObjectData.GetTileData(cell.TileType, 0);
            if (basis == null || basis.CoordinateFullWidth <= 0 || basis.CoordinateFullHeight <= 0 || basis.StyleMultiplier <= 0 || cell.FrameX < 0 || cell.FrameY < 0)
                return Unknown(cell, "此对象缺少可确认的完整外观范围");
            if (basis.GetStyleOverride != null) return Unknown(cell, "此对象使用专用样式规则，尚不能确认整件");
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
            TileObjectData main = TileObjectData.GetTileData(cell.TileType, style), data = main;
            int alternate = 0;
            if (main == null) return Unknown(cell, "对象样式元数据缺失");
            // Alternates is private in the locked 1.4.5.8 ABI. The reflected
            // member is resolved once; unavailable metadata rejects this object
            // instead of guessing a direction or bypassing the world read gate.
            if (alternatesProperty == null || alternatesProperty.PropertyType != typeof(List<TileObjectData>)) return Unknown(cell, "对象方向元数据不可用");
            var alternates = (List<TileObjectData>)alternatesProperty.GetValue(main, null);
            if (alternates != null)
                for (int i = 0; i < alternates.Count; i++)
                {
                    TileObjectData candidate = alternates[i];
                    if (candidate != null && variant >= candidate.Style && variant <= (long)candidate.Style + candidate.RandomStyleRange)
                    { data = candidate; alternate = i + 1; break; }
                }
            if (data.Width <= 0 || data.Height <= 0 || data.CoordinateWidth <= 0 || data.CoordinatePadding < 0 ||
                data.CoordinateFullWidth <= 0 || data.CoordinateFullHeight <= 0 || data.CoordinateHeights == null || data.CoordinateHeights.Length != data.Height)
                return Unknown(cell, "对象尺寸元数据不完整");
            if ((long)data.Width * data.Height >= CaptureChunk.MaximumRecords) return Unknown(cell, "此对象超出单组外观证明范围");
            int step = checked(data.CoordinateWidth + data.CoordinatePadding);
            int baseX = cell.FrameX / data.CoordinateFullWidth * data.CoordinateFullWidth;
            int baseY = cell.FrameY / data.CoordinateFullHeight * data.CoordinateFullHeight;
            int localX = cell.FrameX - baseX, localY = cell.FrameY - baseY;
            if (localX % step != 0 || localX / step >= data.Width) return Unknown(cell, "对象横向帧不对齐");
            var offsets = new int[data.Height]; int row = -1, cumulative = 0;
            for (int i = 0; i < data.Height; i++)
            {
                offsets[i] = cumulative; if (localY == cumulative) row = i;
                if (data.CoordinateHeights[i] <= 0) return Unknown(cell, "对象纵向帧不完整");
                cumulative = checked(cumulative + data.CoordinateHeights[i] + data.CoordinatePadding);
            }
            if (row < 0 || cumulative != data.CoordinateFullHeight) return Unknown(cell, "对象纵向帧不对齐");
            return new BlueprintObjectShape { BaseFrameX = baseX, BaseFrameY = baseY, ColumnStep = step, RowOffsets = offsets,
                Object = new CaptureObject { X = cell.X - localX / step, Y = cell.Y - row, Width = data.Width, Height = data.Height,
                    Type = cell.TileType, Style = style, Alternate = alternate, Direction = (int)data.Direction,
                    OriginX = data.Origin.X, OriginY = data.Origin.Y, PlacementStyle = placement, RandomVariant = variant,
                    Reliable = true, MissingPayload = Missing(cell.TileType) } };
        }
        private static BlueprintObjectShape Unknown(CaptureCell cell, string reason)
        { return new BlueprintObjectShape { Object = new CaptureObject { X = cell.X, Y = cell.Y, Width = 1, Height = 1, Type = cell.TileType, Reliable = false, Reason = reason } }; }
        private static string Missing(ushort type)
        {
            if (type < TileID.Sets.BasicChest.Length && TileID.Sets.BasicChest[type] || type < TileID.Sets.BasicDresser.Length && TileID.Sets.BasicDresser[type]) return "箱内物品未采集";
            if (type == TileID.Signs || type == TileID.Tombstones) return "牌子或墓碑文字未采集";
            if (type == TileID.DisplayDoll || type == TileID.WeaponsRack || type == TileID.ItemFrame || type == TileID.FoodPlatter || type == TileID.HatRack) return "展示物品未采集";
            // The locked .8 TileEntitiesManager registers these remaining
            // entity families. Record the missing category without reading a
            // payload or depending on the client receiving its entity first.
            if (type == 378) return "训练假人关联状态未采集";
            if (type == 423) return "逻辑感应设置未采集";
            if (type == 597) return "晶塔实体状态未采集";
            if (type == 698) return "展示罐内容未采集";
            if (type == 723) return "风筝物品与放飞状态未采集";
            if (type == 724) return "生物放置数据未采集";
            return null;
        }
    }
}
