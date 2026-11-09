using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.GameContent;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    // Item placeStyle is not a texture-frame number. Keep the native declared
    // inputs, then match only this tile family's legal placement appearances.
    internal sealed class NativePlacementNames
    {
        private struct Candidate { internal int Item, Style; }
        private readonly Dictionary<int, List<Candidate>> candidates = new Dictionary<int, List<Candidate>>();
        private readonly HashSet<long> captured = new HashSet<long>();
        private static readonly FieldInfo buckets = typeof(FlexibleTileWand).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Type bucketType = typeof(FlexibleTileWand).GetNestedType("OptionBucket", BindingFlags.NonPublic);
        private static readonly FieldInfo options = bucketType?.GetField("Options"), material = bucketType?.GetField("ItemTypeToConsume");
        internal void Clear() { candidates.Clear(); captured.Clear(); }
        internal void Capture(Item sample)
        {
            if (sample.createTile >= 0) Add(sample.createTile, sample.placeStyle, sample.type);
            // Opening a door changes its world type and frame bank, not its
            // canonical item. Keep both poses in the same candidate aggregator.
            if (sample.createTile == Terraria.ID.TileID.ClosedDoor) Add(Terraria.ID.TileID.OpenDoor, sample.placeStyle, sample.type);
            if (sample.createTile == Terraria.ID.TileID.TrapdoorClosed) Add(Terraria.ID.TileID.TrapdoorOpen, sample.placeStyle, sample.type);
            if (sample.createTile == Terraria.ID.TileID.TallGateClosed) Add(Terraria.ID.TileID.TallGateOpen, sample.placeStyle, sample.type);
            // Only declarations for an item's own non-consuming variations.
            // Never call the wand's inventory/cycling/RNG placement operation,
            // or misidentify rubble/sandcastle materials as the placing tool.
            var wand = sample.GetFlexibleTileWand();
            if (wand == null || wand.ConsumesAmmoItem || options == null || material == null) return;
            var table = buckets?.GetValue(wand) as IDictionary;
            object bucket = table != null && table.Contains(sample.type) ? table[sample.type] : null;
            if (bucket == null || (int)material.GetValue(bucket) != sample.type) return;
            var values = options.GetValue(bucket) as IEnumerable;
            if (values != null) foreach (FlexibleTileWand.PlacementOption value in values) Add(value.TileIdToPlace, value.TileStyleToPlace, sample.type);
        }
        private void Add(int tile, int style, int item)
        {
            if (tile < 0 || tile >= Terraria.ID.TileID.Count || style < 0 || style > ushort.MaxValue || item <= 0) return;
            long key = ((long)tile << 32) | ((long)style << 16) | (uint)item;
            if (!captured.Add(key)) return;
            List<Candidate> values;
            if (!candidates.TryGetValue(tile, out values)) candidates[tile] = values = new List<Candidate>();
            values.Add(new Candidate { Item = item, Style = style });
        }
        internal int Find(Tile tile)
        {
            if (tile == null || !tile.active() || tile.frameX < 0 || (tile.frameY < 0 && tile.type != Terraria.ID.TileID.MinecartTrack)) return 0;
            List<Candidate> values; if (!candidates.TryGetValue(tile.type, out values)) return 0;
            int result = 0;
            foreach (var value in values)
            {
                bool platform = Terraria.ID.TileID.Sets.Platforms[tile.type];
                bool legacy = platform || tile.type == Terraria.ID.TileID.PlanterBox || tile.type == Terraria.ID.TileID.ClosedDoor || tile.type == Terraria.ID.TileID.OpenDoor || tile.type == Terraria.ID.TileID.TrapdoorOpen || tile.type == Terraria.ID.TileID.MinecartTrack;
                bool matches = platform ? PlatformMatches(tile, value) : legacy ? LegacyMatches(tile, value) : !Main.tileFrameImportant[tile.type] && value.Style == 0;
                if (legacy && !matches) continue;
                if (!Main.tileFrameImportant[tile.type] && !matches) continue;
                if (!matches)
                {
                    var root = TileObjectData.GetTileData(tile.type, value.Style);
                    if (root == null) continue;
                    for (int alternate = 0; alternate <= root.AlternatesCount && !matches; alternate++)
                    {
                        var data = TileObjectData.GetTileData(tile.type, value.Style, alternate);
                        if (data == null || data.GetStyleOverride != null) continue;
                        var specific = data.SpecificRandomStyles;
                        if (specific != null)
                        {
                            foreach (int style in specific) if (Matches(tile, data, value.Style, alternate, style - value.Style)) { matches = true; break; }
                        }
                        else for (int random = 0; random < Math.Max(1, data.RandomStyleRange) && !matches; random++)
                            matches = Matches(tile, data, value.Style, alternate, random);
                    }
                }
                if (!matches) continue;
                if (result != 0 && result != value.Item) return 0;
                result = value.Item;
            }
            return result;
        }
        private static bool PlatformMatches(Tile tile, Candidate value)
        {
            // Native .8 platform framing owns 27 connection slots, not random
            // placement variants. Its registered root has no alternates to
            // compensate for those slots. Preserve the material's Y style.
            var data = TileObjectData.GetTileData(tile.type, value.Style);
            return data != null && data.Width == 1 && data.Height == 1 && data.CoordinateWidth == 16 && data.CoordinatePadding == 2 &&
                data.CoordinateFullWidth == 18 && data.CoordinateFullHeight == 18 && data.StyleHorizontal && data.StyleMultiplier == 27 &&
                data.StyleWrapLimit == 27 && data.StyleLineSkip == 1 && data.Style == 0 && data.RandomStyleRange == 0 &&
                data.GetStyleOverride == null && data.AlternatesCount == 0 && data.SpecificRandomStyles == null &&
                tile.frameX < 27 * 18 && tile.frameX % 18 == 0 && (long)tile.frameY == 18L * value.Style &&
                tile.slope() <= 4 && (!tile.halfBrick() || tile.slope() == 0);
        }
        private static bool LegacyMatches(Tile tile, Candidate value)
        {
            // These .8 legacy encodings do not share ordinary TOD coordinates.
            // Validate only the finite family domain; never repair world frames
            // or derive an item ID from a style. Candidates still own identity.
            if (tile.halfBrick() || tile.slope() != 0) return false;
            if (tile.type == Terraria.ID.TileID.PlanterBox)
                return tile.frameX <= 54 && tile.frameX % 18 == 0 && (long)tile.frameY == 18L * value.Style;
            // CloseDoor chooses decoration X independently for each of the
            // three rows. X54 is a style page, not another decoration slot.
            if (tile.type == Terraria.ID.TileID.ClosedDoor)
                return tile.frameX % 18 == 0 && tile.frameY % 18 == 0 &&
                    tile.frameX / 54 == value.Style / 36 && tile.frameY / 54 == value.Style % 36;
            if (tile.type == Terraria.ID.TileID.OpenDoor)
                return tile.frameX % 18 == 0 && tile.frameY % 18 == 0 &&
                    (long)tile.frameX / 72 == value.Style / 36 && tile.frameY / 54 == value.Style % 36;
            // ShiftTrapdoor produces two 2x2 poses from the sole native closed
            // declaration (style0). Their X banks encode opening side, not item.
            if (tile.type == Terraria.ID.TileID.TrapdoorOpen)
                return value.Style == 0 && tile.frameX <= 54 && tile.frameX % 18 == 0 && tile.frameY <= 18 && tile.frameY % 18 == 0;
            // FrameTrack stores indices, with -1 meaning no secondary track.
            // GetTrackItem is a read-only native classification, not a drop or
            // framing operation. Bound both fields before calling its array ABI.
            return tile.frameX < 36 && tile.frameY >= -1 && tile.frameY < 36 && Minecart.GetTrackItem(tile) == value.Item;
        }
        private static bool Matches(Tile tile, TileObjectData data, int style, int alternate, int random)
        {
            if (data.Width <= 0 || data.Height <= 0 || data.CoordinateFullWidth <= 0 || data.CoordinateFullHeight <= 0 || data.StyleMultiplier <= 0) return false;
            int encoded = data.CalculatePlacementStyle(style, alternate, random), cross = 0;
            if (encoded < 0) return false;
            if (data.StyleWrapLimit > 0) { cross = encoded / data.StyleWrapLimit * data.StyleLineSkip; encoded %= data.StyleWrapLimit; }
            int fx = tile.frameX - (data.StyleHorizontal ? encoded : cross) * data.CoordinateFullWidth;
            int fy = tile.frameY - (data.StyleHorizontal ? cross : encoded) * data.CoordinateFullHeight;
            if (Cell(data, fx, fy)) return true;
            // Wiring changes state frames after placement. Admit only the .8
            // Toggle* offsets for these families, preserving item style/colour;
            // unused multiplier/line-skip slots are not arbitrary variants.
            switch (tile.type)
            {
                case 4: fx -= 66; break;
                case 33: case 49: case 174: case 372: case 646: case 42: case 93: case 92: case 593: fx -= 18; break;
                case 95: case 100: case 126: case 173: case 564: case 35: case 139: case 565: case 594: fx -= 36; break;
                case 34: case 149: case 405: case 244: fx -= 54; break;
                case 215: fy -= 36; break;
                case 136: case 144: fy -= 18; break;
                case 207: fy -= 72; break;
                case 410: fy -= 56; break;
                case 480: case 509: case 657: case 720: case 721: case 725: case 733: fy -= 54; break;
                case 658: return Cell(data, fx, fy - 54) || Cell(data, fx, fy - 108);
                default: return false;
            }
            return Cell(data, fx, fy);
        }
        private static bool Cell(TileObjectData data, int fx, int fy)
        {
            int stride = data.CoordinateWidth + data.CoordinatePadding;
            if (stride <= 0 || fx < 0 || fx >= data.CoordinateFullWidth || fx % stride != 0 || fx / stride >= data.Width || fy < 0) return false;
            int[] heights = data.CoordinateHeights;
            if (heights == null || heights.Length < data.Height) return false;
            for (int row = 0, offset = 0; row < data.Height; offset += heights[row++] + data.CoordinatePadding)
                if (fy == offset) return true;
            return false;
        }
    }
}
