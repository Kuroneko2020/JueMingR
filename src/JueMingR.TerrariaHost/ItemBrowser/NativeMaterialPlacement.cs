using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.ItemBrowser
{
    internal sealed partial class NativeItemCatalog
    {
        private readonly List<BlueprintPlacementRule> materialRules = new List<BlueprintPlacementRule>();
        private readonly Dictionary<int, BlueprintPlacementDescriptor> materialWalls = new Dictionary<int, BlueprintPlacementDescriptor>();
        private readonly Dictionary<int, int> materialPaints = new Dictionary<int, int>(), materialCoatings = new Dictionary<int, int>();
        private readonly HashSet<int> materialCaptured = new HashSet<int>();
        private static readonly FieldInfo materialBuckets = typeof(FlexibleTileWand).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Type materialBucketType = typeof(FlexibleTileWand).GetNestedType("OptionBucket", BindingFlags.NonPublic);
        private static readonly FieldInfo materialOptions = materialBucketType?.GetField("Options"), materialConsume = materialBucketType?.GetField("ItemTypeToConsume");
        private BlueprintPlacementCatalog materialCatalog;
        private long placementRevision;
        private ulong materialPlacementTick;
        private bool materialPlacementTickSet;
        internal long PlacementRevision { get { PublishMaterialPlacement(); return placementRevision; } }
        internal BlueprintPlacementCatalog MaterialPlacement { get { PublishMaterialPlacement(); return PlacementReady ? materialCatalog : null; } }
        private void ClearMaterialPlacement()
        {
            materialRules.Clear(); materialWalls.Clear(); materialPaints.Clear(); materialCoatings.Clear(); materialCaptured.Clear();
            materialCatalog = null; placementRevision++; materialPlacementTickSet = false;
        }
        private void PublishMaterialPlacement()
        {
            if (!PlacementReady || materialCatalog != null) return;
            materialCatalog = new BlueprintPlacementCatalog(++placementRevision, materialRules, materialWalls, materialPaints, materialCoatings, ItemID.Wire, ItemID.Actuator);
        }
        private static void AddMaterialIdentity(Dictionary<int, int> table, int key, int item)
        { int old; table[key] = table.TryGetValue(key, out old) && old != item ? -1 : item; }
        private void CaptureMaterialPlacement(Item sample)
        {
            try { CaptureMaterialPlacementCore(sample); }
            catch (ArgumentException) { }
            catch (IndexOutOfRangeException) { }
            catch (OverflowException) { }
            catch (TargetInvocationException) { }
        }
        private void CaptureMaterialPlacementCore(Item sample)
        {
            if (sample.type <= 0 || !materialCaptured.Add(sample.type)) return;
            if (sample.paint > 0) AddMaterialIdentity(materialPaints, sample.paint, sample.type);
            if (sample.paintCoating > 0) AddMaterialIdentity(materialCoatings, sample.paintCoating, sample.type);
            if (sample.createWall > 0)
            {
                var value = new BlueprintPlacementDescriptor(BlueprintPlacementKind.Wall, sample.type, sample.consumable ? sample.type : 0,
                    sample.consumable ? 1 : 0, -1, sample.placeStyle, sample.createWall);
                BlueprintPlacementDescriptor old;
                if (!materialWalls.TryGetValue(sample.createWall, out old)) materialWalls.Add(sample.createWall, value);
                else if (old == null || !old.SameConsumption(value)) materialWalls[sample.createWall] = null;
            }
            var wand = sample.GetFlexibleTileWand();
            if (wand != null && materialBuckets != null && materialOptions != null && materialConsume != null)
            {
                var table = materialBuckets.GetValue(wand) as IDictionary;
                if (table != null) foreach (DictionaryEntry pair in table)
                {
                    int consumed = (int)materialConsume.GetValue(pair.Value);
                    var options = materialOptions.GetValue(pair.Value) as IEnumerable;
                    if (options == null) continue;
                    foreach (FlexibleTileWand.PlacementOption option in options)
                        CompileMaterialRules(sample, option.TileIdToPlace, option.TileStyleToPlace,
                            wand.ConsumesAmmoItem ? consumed : sample.consumable ? sample.type : 0,
                            wand.ConsumesAmmoItem || sample.consumable ? 1 : 0, true);
                }
                // The real consumer first selects a flexible declaration; its
                // createTile fallback is not a second direct placement option.
                return;
            }
            if (wand != null) return; // Missing locked declaration ABI is unknown, never a free-tool fallback.
            if (sample.createTile >= 0) CompileMaterialRules(sample, sample.createTile, sample.placeStyle,
                sample.tileWand > 0 ? sample.tileWand : sample.consumable ? sample.type : 0,
                sample.tileWand > 0 || sample.consumable ? 1 : 0, wand != null || sample.tileWand > 0);
        }
        private void CompileMaterialRules(Item sample, int tile, int style, int consumed, long units, bool flexible)
        {
            if (tile < 0 || tile >= TileID.Count || style < 0) return;
            if (tile == TileID.ClosedDoor)
            {
                materialRules.Add(new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, sample.type, consumed, units,
                    tile, style, flexible: flexible, width: 1, height: 3, originX: 0, originY: 0, requiresObject: true), false, door: true)); return;
            }
            if (tile < TileID.Sets.Platforms.Length && TileID.Sets.Platforms[tile])
            { materialRules.Add(new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, sample.type, consumed, units, tile, style, flexible: flexible, platform: true), false)); return; }
            if (!Main.tileFrameImportant[tile])
            {
                // These original PlaceThing_Tiles profiles transform an
                // existing substrate; createTile alone is not a complete
                // from-air body recipe. Keep the declared consumable known,
                // with the unimplemented substrate step explicitly unresolved.
                bool substrate = tile == 2 || tile == 23 || tile == 199 || tile == 60 || tile == 70 || tile == 109 || tile == 633 || tile == 661 || tile == 662 ||
                    tile < Main.tileMoss.Length && Main.tileMoss[tile];
                if (style == 0) materialRules.Add(new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, sample.type, consumed, units, tile, style,
                    flexible: flexible, requiresSubstrate: substrate), true));
                return;
            }
            var root = TileObjectData.GetTileData(tile, style);
            if (root == null) return;
            for (int alternate = 0; alternate <= root.AlternatesCount; alternate++)
            {
                var data = TileObjectData.GetTileData(tile, style, alternate);
                if (data == null) continue;
                if (data.GetStyleOverride != null)
                {
                    // These are the three current original selectors already
                    // supported by capture. Compile their declared regions;
                    // no selector delegate survives in the published catalog.
                    var basis = TileObjectData.GetTileData(tile, int.MaxValue);
                    if (basis?.GetStyleOverride == TileObjectData.GetStyle_Detritus) { CompileSelector(sample, tile, style, consumed, units, flexible, basis, 0); CompileSelector(sample, tile, style, consumed, units, flexible, basis, 36); }
                    else if (basis?.GetStyleOverride == TileObjectData.GetStyle_SmallPiles) { CompileSelector(sample, tile, style, consumed, units, flexible, basis, 0); CompileSelector(sample, tile, style, consumed, units, flexible, basis, 18); }
                    else if (basis?.GetStyleOverride == TileObjectData.GetStyle_Stalactite) foreach (int y in new[] { 0, 36, 72, 90 }) CompileSelector(sample, tile, style, consumed, units, flexible, basis, y);
                    continue;
                }
                if (data.SpecificRandomStyles != null)
                    foreach (int random in data.SpecificRandomStyles) CompileGrid(sample, tile, style, consumed, units, flexible, data, alternate, random - style);
                else for (int random = 0; random < Math.Max(1, data.RandomStyleRange); random++) CompileGrid(sample, tile, style, consumed, units, flexible, data, alternate, random);
            }
        }
        private void CompileSelector(Item sample, int tile, int style, int consumed, long units, bool flexible, TileObjectData basis, int regionY)
        {
            var probe = new Tile { type = (ushort)tile, frameY = (short)regionY }; probe.active(true);
            var selected = basis.GetStyleOverride(probe, basis);
            if (selected == null) return;
            AddMaterialRule(sample, tile, style, consumed, units, flexible, selected, 0, checked(style * selected.CoordinateFullWidth), regionY);
        }
        private void CompileGrid(Item sample, int tile, int style, int consumed, long units, bool flexible, TileObjectData data, int alternate, int random)
        {
            if (data.StyleMultiplier <= 0 || data.CoordinateFullWidth <= 0 || data.CoordinateFullHeight <= 0) return;
            int encoded = data.CalculatePlacementStyle(style, alternate, random), cross = 0;
            if (encoded < 0) return;
            if (data.StyleWrapLimit > 0) { cross = encoded / data.StyleWrapLimit * data.StyleLineSkip; encoded %= data.StyleWrapLimit; }
            AddMaterialRule(sample, tile, style, consumed, units, flexible, data, alternate,
                checked((data.StyleHorizontal ? encoded : cross) * data.CoordinateFullWidth), checked((data.StyleHorizontal ? cross : encoded) * data.CoordinateFullHeight));
        }
        private void AddMaterialRule(Item sample, int tile, int style, int consumed, long units, bool flexible, TileObjectData data, int alternate, int baseX, int baseY)
        {
            if (data.Width <= 0 || data.Height <= 0 || data.CoordinateHeights == null || data.CoordinateHeights.Length != data.Height || baseX > short.MaxValue || baseY > short.MaxValue) return;
            var rows = new int[data.Height]; int at = 0;
            for (int i = 0; i < rows.Length; i++) { rows[i] = at; at = checked(at + data.CoordinateHeights[i] + data.CoordinatePadding); }
            int dx = 0, dy = 0;
            switch (tile)
            {
                case 4: dx = 66; break;
                case 33: case 49: case 174: case 372: case 646: case 42: case 93: case 92: case 593: dx = 18; break;
                case 95: case 100: case 126: case 173: case 564: case 35: case 139: case 565: case 594: dx = 36; break;
                case 34: case 149: case 405: case 244: dx = 54; break;
                case 215: dy = 36; break;
                case 136: case 144: dy = 18; break;
                case 207: dy = 72; break;
                case 410: dy = 56; break;
                case 480: case 509: case 657: case 720: case 721: case 725: case 733: case 658: dy = 54; break;
            }
            materialRules.Add(new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, sample.type, consumed, units,
                tile, style, flexible: flexible, width: data.Width, height: data.Height, originX: data.Origin.X, originY: data.Origin.Y,
                alternate: alternate, direction: (int)data.Direction, requiresObject: true), false, baseX, baseY, data.CoordinateWidth + data.CoordinatePadding, rows, dx, dy, operatingY2: tile == 658 ? 108 : 0));
        }
    }
}
