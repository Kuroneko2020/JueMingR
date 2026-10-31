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
            if ((cell.Layers & CaptureLayer.Tile) == 0) return true;
            // A platform Set member needs its exact registered grid even when
            // metadata is absent; it cannot fall back to an ordinary cell.
            if (rawShape == null) return !IsPlatform(cell.TileType);
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
                TileObjectData data = BlueprintHorizontalMirrorResolver.NativeData(semantic);
                TileObjectData basis = TileObjectData.GetTileData(value.Type, int.MaxValue);
                if (data == null || basis == null) return false;
                int variant = value.RandomVariant, style = value.Style;
                if (IsPlatform(cell.TileType))
                {
                    // Locked .8 PlatformFrame chooses X from the 27 neighbor/
                    // shape slots. Y alone selects material style. Prove that
                    // exact grid before folding X in building identity; raw
                    // capture, member layout and observation group stay intact.
                    if (!PlatformProfile(cell, rawShape, data, basis)) return false;
                    variant = 0;
                }
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
        private static bool IsPlatform(ushort type)
        { return type < TileID.Sets.Platforms.Length && TileID.Sets.Platforms[type]; }
        private static bool PlatformProfile(CaptureCell cell, BlueprintObjectShape shape, TileObjectData data, TileObjectData basis)
        {
            if (!PlatformGrid(basis) || !PlatformGrid(data) || cell.FrameX < 0 || cell.FrameY < 0 ||
                cell.FrameX >= 27 * 18 || cell.FrameX % 18 != 0 || cell.FrameY % 18 != 0) return false;
            CaptureObject value = shape.Object;
            int style = cell.FrameY / 18, slot = cell.FrameX / 18;
            return value.Type == cell.TileType && value.X == cell.X && value.Y == cell.Y && value.Width == 1 && value.Height == 1 &&
                value.OriginX == 0 && value.OriginY == 0 && value.Direction == 0 && value.Alternate == 0 &&
                value.Style == style && value.PlacementStyle == style * 27 + slot && value.RandomVariant == slot &&
                !shape.IndependentMemberFrames && shape.BaseFrameX == cell.FrameX && shape.BaseFrameY == cell.FrameY &&
                shape.ColumnStep == 18 && shape.RowOffsets != null && shape.RowOffsets.Length == 1 && shape.RowOffsets[0] == 0;
        }
        private static bool PlatformGrid(TileObjectData data)
        {
            return data.Width == 1 && data.Height == 1 && data.Origin.X == 0 && data.Origin.Y == 0 && (int)data.Direction == 0 &&
                data.CoordinateWidth == 16 && data.CoordinatePadding == 2 && data.CoordinateFullWidth == 18 && data.CoordinateFullHeight == 18 &&
                data.CoordinatePaddingFix.X == 0 && data.CoordinatePaddingFix.Y == 0 && data.CoordinateHeights != null &&
                data.CoordinateHeights.Length == 1 && data.CoordinateHeights[0] == 16 && data.StyleHorizontal &&
                data.StyleMultiplier == 27 && data.StyleWrapLimit == 27 && data.StyleLineSkip == 1 && data.Style == 0 &&
                data.RandomStyleRange == 0 && data.GetStyleOverride == null && !data.UsesCustomCanPlace && !data.AnchorWall &&
                NoAnchor(data.AnchorTop) && NoAnchor(data.AnchorBottom) && NoAnchor(data.AnchorLeft) && NoAnchor(data.AnchorRight);
        }
        private static bool NoAnchor(Terraria.DataStructures.AnchorData anchor)
        { return (int)anchor.type == 0 && anchor.tileCount == 0 && anchor.checkStart == 0; }
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
            // Platform body equivalence proves neither raw neighbor-frame
            // writes nor their coverage. Keep ordinary one-cell invalidation
            // and bounded rereads instead of inventing an operating phase set.
            if (IsPlatform(first.TileType) || IsPlatform(observed.Kind)) return false;
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
        { return BlueprintHorizontalMirrorResolver.NormalizeOperatingFrame(cell); }
        internal static Tile TileValue(CaptureCell cell)
        {
            var tile = new Tile { type = cell.TileType, frameX = cell.FrameX, frameY = cell.FrameY };
            tile.active((cell.Layers & CaptureLayer.Tile) != 0); return tile;
        }
    }
}
