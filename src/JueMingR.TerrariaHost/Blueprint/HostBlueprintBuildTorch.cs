using System;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        // The original assorted gate may legally flatten one torch anchor
        // before PlaceIt. Its own BuildSupport invalidation must not require a
        // second swing. This receipt exists only after that exact native gate
        // returned true in this flight/ItemCheck, and never publishes Match.
        private Action<bool> BeginNativeBuildPreparation()
        {
            var flight = activeBuild; var use = buildTools?.Use;
            if (flight == null || use == null || !use.InNativeUse || flight.Checked || !flight.Entered ||
                flight.Token != use.Operation || flight.Target.Placement.TileType != TileID.Torches ||
                Player.tileTargetX != flight.Target.WorldX || Player.tileTargetY != flight.Target.WorldY) return null;
            CaptureCell[,] expected; bool flattened;
            if (!ReadBuildTorchCells(flight, true, out expected, out flattened) || !flattened) return null;
            long native = use.NativeGeneration;
            return allowed =>
            {
                if (!ReferenceEquals(activeBuild, flight) || use.NativeGeneration != native || !use.InNativeUse || flight.Token != use.Operation) return;
                flight.TorchPreparation = null;
                if (!allowed || flight.Checked || !flight.Entered || !IsBuildTargetIdentityCurrent(flight.Target) || !TorchCellsCurrent(flight, expected)) return;
                flight.TorchPreparation = expected; flight.TorchPreparationGeneration = native;
            };
        }
        private bool NativeTorchPreparationCurrent(BuildFlight flight)
        {
            var use = buildTools?.Use;
            return flight.TorchPreparation != null && ReferenceEquals(activeBuild, flight) && use?.InNativeUse == true &&
                !flight.Checked && flight.Entered && flight.Token == use.Operation && flight.TorchPreparationGeneration == use.NativeGeneration &&
                IsBuildTargetIdentityCurrent(flight.Target) && TorchCellsCurrent(flight, flight.TorchPreparation);
        }
        private bool TorchCellsCurrent(BuildFlight flight, CaptureCell[,] expected)
        {
            for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
            {
                BlueprintWorldMatchSample sample;
                if (!ReadBuildCell(flight.Target.WorldX + x - 1, flight.Target.WorldY + y - 1, out sample) ||
                    !TorchCellMeaning(sample.Cell).Equals(TorchCellMeaning(expected[x, y]))) return false;
            }
            return true;
        }
        private static CaptureCell TorchCellMeaning(CaptureCell cell)
        {
            // SquareTileFrame changes ordinary terrain's decoration during
            // SlopeTile. Those frames have no placement identity; frame-
            // important neighbors, shape, paint, liquid and every layer remain
            // exact. External support removal/change still fails this proof.
            if ((cell.Layers & CaptureLayer.Tile) != 0 && cell.TileType < Main.tileFrameImportant.Length && !Main.tileFrameImportant[cell.TileType])
                cell.FrameX = cell.FrameY = 0;
            return cell;
        }
        private bool ReadBuildTorchCells(BuildFlight flight, bool predictFlattening, out CaptureCell[,] cells, out bool flattened)
        {
            cells = new CaptureCell[3, 3]; flattened = false;
            for (int x = 0; x < 3; x++) for (int y = 0; y < 3; y++)
            {
                BlueprintWorldMatchSample sample;
#if DEBUG
                BuildEligibilityReads++;
#endif
                if (projectionMatchSource == null || !projectionMatchSource.TryRead(flight.Target.WorldX + x - 1, flight.Target.WorldY + y - 1, out sample) || !sample.Available) return false;
                cells[x, y] = sample.Cell;
            }
            if (predictFlattening && (cells[1, 1].Layers & CaptureLayer.Wall) == 0 &&
                !WorldGen.SolidTileNoAttach(flight.Target.WorldX, flight.Target.WorldY + 1) &&
                !WorldGen.SolidTileNoAttach(flight.Target.WorldX - 1, flight.Target.WorldY) &&
                !WorldGen.SolidTileNoAttach(flight.Target.WorldX + 1, flight.Target.WorldY))
            {
                int tx = flight.Target.WorldX, ty = flight.Target.WorldY, sx = -1, sy = -1;
                if (!WorldGen.SolidTileNoAttach(tx, ty + 1) && (cells[1, 2].HalfBrick || cells[1, 2].Slope != 0)) { sx = 1; sy = 2; }
                else if (!WorldGen.SolidTileNoAttach(tx, ty + 1) && !WorldGen.SolidTileNoAttach(tx - 1, ty) && (cells[0, 1].HalfBrick || cells[0, 1].Slope != 0)) { sx = 0; sy = 1; }
                else if (!WorldGen.SolidTileNoAttach(tx, ty + 1) && !WorldGen.SolidTileNoAttach(tx + 1, ty) && (cells[2, 1].HalfBrick || cells[2, 1].Slope != 0)) { sx = 2; sy = 1; }
                if (sx >= 0 && !TileID.Sets.Platforms[cells[sx, sy].TileType])
                {
                    int x = tx + sx - 1, y = ty + sy - 1; BlueprintWorldMatchSample below;
                    if (!ReadBuildCell(x, y + 1, out below)) return false;
                    if (cells[sx, sy].TileType == 235)
                    {
                        // CanPoundTile delegates this one type's breakability
                        // to all three top members, whose left edge is frame
                        // derived and may exceed the torch's own 3x3 halo.
                        int first = x - cells[sx, sy].FrameX % 54 / 18;
                        for (int i = 0; i < 3; i++) if (!ReadBuildCell(first + i, y - 1, out below)) return false;
                    }
                    if (WorldGen.CanPoundTile(x, y)) { var flat = cells[sx, sy]; flat.HalfBrick = false; flat.Slope = 0; cells[sx, sy] = flat; flattened = true; }
                }
            }
            return true;
        }
        private bool MatchBuildTorchCells(BuildFlight flight, CaptureCell[,] cells)
        {
            var down = cells[1, 2]; var left = cells[0, 1]; var right = cells[2, 1];
            int d = BuildActive(down) && (TileID.Sets.Platforms[down.TileType] && !down.HalfBrick && !down.Inactive &&
                (down.Slope != 1 && down.Slope != 2 || WorldGen.PlatformProperTopFrame(down.FrameX)) || !down.HalfBrick && down.Slope != 1 && down.Slope != 2) ? down.TileType : -1;
            int l = BuildActive(left) && (left.Slope == 0 || left.Slope % 2 != 1) ? left.TileType : -1;
            int r = BuildActive(right) && (right.Slope == 0 || right.Slope % 2 != 0) ? right.TileType : -1;
            int frame;
            if (d >= 0 && Main.tileSolid[d] && (!Main.tileNoAttach[d] || TileID.Sets.Platforms[d])) frame = 0;
            else if (l >= 0 && (Main.tileSolid[l] && !Main.tileNoAttach[l] || TileID.Sets.IsBeam[l]) || BuildTree(l, cells[0, 0], cells[0, 2])) frame = 22;
            else if (r >= 0 && (Main.tileSolid[r] && !Main.tileNoAttach[r] || TileID.Sets.IsBeam[r]) || BuildTree(r, cells[2, 0], cells[2, 2])) frame = 44;
            else if ((cells[1, 1].Layers & CaptureLayer.Wall) != 0) frame = 0;
            else return false;
            var cell = new CaptureCell { X = flight.Target.WorldX, Y = flight.Target.WorldY, Layers = CaptureLayer.Tile, TileType = TileID.Torches,
                FrameX = (short)frame, FrameY = checked((short)(flight.Target.Placement.Style * 22)) };
            return SameLegacyBuildBody(flight, cell);
        }
    }
}
