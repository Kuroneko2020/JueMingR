using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal static class BlueprintWorldObjectSemantics
    {
        internal static bool TryCreate(CaptureCell cell, BlueprintObjectShape rawShape, out BlueprintWorldObject result)
        {
            result = null;
            if ((cell.Layers & CaptureLayer.Tile) == 0 || rawShape == null) return true;
            if (!rawShape.Object.Reliable) return false;
            try
            {
                CaptureObject raw = rawShape.Object;
                if (cell.TileType == TileID.ClosedDoor || cell.TileType == TileID.OpenDoor)
                {
                    // .8 WorldGen.OpenDoor/CloseDoor preserve style and hinge
                    // while transforming 1x3 <-> 2x3. The left wing's top-left
                    // is one column left of the canonical hinge. Closed X is
                    // independently randomized on every row, not a direction.
                    bool open = cell.TileType == TileID.OpenDoor;
                    int hinge = open && cell.FrameX % 72 >= 36 ? raw.X + 1 : raw.X;
                    result = new BlueprintWorldObject(raw.X, raw.Y, raw.Width, raw.Height, hinge, raw.Y, TileID.ClosedDoor,
                        raw.Style, 0, BlueprintWorldObjectFamily.Door, Array.Empty<int>(), FrameGroup(rawShape), rawShape);
                    return true;
                }
                CaptureCell semantic = NormalizeOperatingFrame(cell);
                BlueprintObjectShape shape = semantic.FrameX == cell.FrameX && semantic.FrameY == cell.FrameY ? rawShape :
                    BlueprintCaptureSource.DescribeRead(semantic, TileValue(semantic));
                if (shape == null || !shape.Object.Reliable || shape.Object.X != raw.X || shape.Object.Y != raw.Y || shape.Object.Width != raw.Width || shape.Object.Height != raw.Height) return false;
                CaptureObject value = shape.Object;
                TileObjectData data = TileObjectData.GetTileData(TileValue(semantic));
                TileObjectData basis = TileObjectData.GetTileData(value.Type, int.MaxValue);
                if (data == null || basis == null) return false;
                int variant = value.RandomVariant, style = value.Style;
                // Only the original declared random placement range is folded.
                // Other variant slots remain semantic: chair directions and
                // selector attachment forms cannot disappear with the name.
                if (data.RandomStyleRange > 0 && variant >= data.Style && variant < (long)data.Style + data.RandomStyleRange) variant = data.Style;
                // Original natural placements also declare random ranges with
                // multiplier=1 (larva, sunflower, sapling). Their random slots
                // spill into capture's style quotient. Consecutive full ranges
                // retain the building form (e.g. sapling biome alternates),
                // while slots inside one range are texture decoration. A
                // selected SubTile is excluded: an art/style selector is not
                // this registered base grid. No extra private ABI is needed.
                if (ReferenceEquals(data, basis) && basis.StyleMultiplier == 1 && data.RandomStyleRange > 1 && data.Style == 0)
                    style = value.PlacementStyle / data.RandomStyleRange;
                var form = new List<int> { value.Width, value.Height, data.CoordinateWidth, data.CoordinatePadding,
                    data.CoordinatePaddingFix.X, data.CoordinatePaddingFix.Y, data.DrawXOffset, data.DrawYOffset, data.DrawStyleOffset,
                    variant, data.Style, SelectorRegion(basis, semantic) };
                foreach (int row in data.CoordinateHeights) form.Add(row);
                AddAnchor(form, data.AnchorTop); AddAnchor(form, data.AnchorBottom); AddAnchor(form, data.AnchorLeft); AddAnchor(form, data.AnchorRight);
                form.Add(data.AnchorWall ? 1 : 0);
                result = new BlueprintWorldObject(raw.X, raw.Y, raw.Width, raw.Height, raw.X, raw.Y, value.Type, style, value.Direction,
                    BlueprintWorldObjectFamily.Metadata, form.ToArray(), FrameGroup(rawShape), rawShape);
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (IndexOutOfRangeException) { return false; }
            catch (OverflowException) { return false; }
        }

        private static int SelectorRegion(TileObjectData basis, CaptureCell cell)
        {
            if (basis.GetStyleOverride == null) return 0;
            if (basis.GetStyleOverride == TileObjectData.GetStyle_Detritus) return cell.FrameY >= 36 ? 36 : 0;
            if (basis.GetStyleOverride == TileObjectData.GetStyle_SmallPiles) return cell.FrameY >= 18 ? 18 : 0;
            if (basis.GetStyleOverride == TileObjectData.GetStyle_Stalactite) return cell.FrameY >= 90 ? 90 : cell.FrameY >= 72 ? 72 : cell.FrameY >= 36 ? 36 : 0;
            return -1;
        }
        // Canonical equivalence may ignore an operating or random frame slot,
        // but all members of one current rectangle must still share a legal
        // raw group. Closed-door independent rows already share decoded bases.
        private static long FrameGroup(BlueprintObjectShape shape)
        { return ((long)(uint)shape.BaseFrameX << 32) | (uint)shape.BaseFrameY; }
        private static void AddAnchor(List<int> values, Terraria.DataStructures.AnchorData anchor)
        { values.Add((int)anchor.type); values.Add(anchor.tileCount); values.Add(anchor.checkStart); }

        // These finite displacements cover the locked operating-frame rules
        // below, not a list of permitted furniture. Cheap normalization first
        // rejects other frames; original metadata must then prove the same
        // placement and fixed layout before a phase enters the value profile.
        private static readonly int[] operatingDisplacements = { -108, -72, -66, -56, -54, -36, -18, 18, 36, 54, 56, 66, 72, 108 };
        internal static bool TryCreateFrameWitness(CaptureCell first, BlueprintWorldObject observed, out BlueprintWorldObjectFrameWitness witness)
        {
            witness = null;
            // Door type/presence/rectangle changes need the ordinary complete
            // object path. They cannot borrow a fixed-layout frame certificate.
            if (observed == null || observed.Family != BlueprintWorldObjectFamily.Metadata) return false;
            CaptureCell canonical = NormalizeOperatingFrame(first);
            var candidates = new List<BlueprintWorldObject> { observed };
            for (int axis = 0; axis < 2; axis++) foreach (int delta in operatingDisplacements)
            {
                int value = (axis == 0 ? first.FrameX : first.FrameY) + delta;
                if (value < 0 || value > short.MaxValue) continue;
                CaptureCell probe = first;
                if (axis == 0) probe.FrameX = (short)value; else probe.FrameY = (short)value;
                CaptureCell normalized = NormalizeOperatingFrame(probe);
                if (normalized.FrameX != canonical.FrameX || normalized.FrameY != canonical.FrameY) continue;
                BlueprintObjectShape shape = BlueprintCaptureSource.DescribeRead(probe, TileValue(probe));
                BlueprintWorldObject candidate;
                if (shape != null && TryCreate(probe, shape, out candidate) && candidate != null && observed.SamePlacement(candidate) &&
                    candidate.WorldX == observed.WorldX && candidate.WorldY == observed.WorldY && candidate.Width == observed.Width && candidate.Height == observed.Height)
                    candidates.Add(candidate);
            }
            return observed.TryCreateFrameWitness(candidates, out witness);
        }

        // These are finite original operating-frame mechanisms, established by
        // Wiring.Toggle* in locked .8, not a furniture support allowlist. Shape,
        // direction, material style and every saved member coating remain live.
        private static CaptureCell NormalizeOperatingFrame(CaptureCell cell)
        {
            int x = cell.FrameX, y = cell.FrameY;
            switch (cell.TileType)
            {
                case 33: case 49: case 174: case 372: case 646: // ToggleCandle
                case 42: case 93: case 92: case 593: // lantern, lamp, lamp post, switch
                    if (x >= 18 && x < 36) x -= 18;
                    break;
                case 95: case 100: case 126: case 173: case 564: // Toggle2x2Light
                case 35: case 139: case 565: case 594: // music box and 2x2 switches
                    if (x >= 36 && x < 72) x -= 36;
                    break;
                case 34: // ToggleChandelier: six X columns per material style
                    if (x % 108 >= 54) x -= 54;
                    break;
                case 4: // Torch: the first 66px retains attachment direction
                    if (x >= 66 && x < 132) x -= 66;
                    break;
                case 149: // Holiday light: color remains in the first 54px
                case 405: case 244: // fireplace and lever
                    if (x >= 54 && x < 108) x -= 54;
                    break;
                case 136: case 144: // switch/timer state in Y, timer style in X
                    if (y >= 18 && y < 36) y -= 18;
                    break;
                case 207:
                    if (y >= 72 && y < 144) y -= 72;
                    break;
                case 410:
                    if (y >= 56 && y < 112) y -= 56;
                    break;
                case 480: case 509: case 657: case 720: case 721: case 725: case 733:
                    if (y >= 54 && y < 108) y -= 54;
                    break;
                case 658:
                    if (y >= 54 && y < 162) y %= 54;
                    break;
            }
            if (cell.TileType < TileID.Sets.Campfires.Length && TileID.Sets.Campfires[cell.TileType] && y >= 36 && y < 72) y -= 36;
            cell.FrameX = checked((short)x); cell.FrameY = checked((short)y); return cell;
        }
        internal static Tile TileValue(CaptureCell cell)
        {
            var tile = new Tile { type = cell.TileType, frameX = cell.FrameX, frameY = cell.FrameY };
            tile.active((cell.Layers & CaptureLayer.Tile) != 0); return tile;
        }
    }
}
