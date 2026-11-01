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
        private readonly List<BlueprintPlacementDeclaration> materialDeclarations = new List<BlueprintPlacementDeclaration>();
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
        private bool materialTileSourceComplete = true, materialWallSourceComplete = true, materialAbiValid = true;
        internal long PlacementRevision { get { PublishMaterialPlacement(); return placementRevision; } }
        internal BlueprintPlacementCatalog MaterialPlacement { get { PublishMaterialPlacement(); return PlacementReady ? materialCatalog : null; } }
        private void ClearMaterialPlacement()
        {
            materialRules.Clear(); materialDeclarations.Clear(); materialWalls.Clear(); materialPaints.Clear(); materialCoatings.Clear(); materialCaptured.Clear();
            ClearBlueprintSubstitution();
            materialTileSourceComplete = materialWallSourceComplete = materialAbiValid = true;
            materialCatalog = null; placementRevision++; materialPlacementTickSet = false;
        }
        private void PublishMaterialPlacement()
        {
            if (!PlacementReady || materialCatalog != null && (materialCatalog.Substitution.Ready || !BlueprintSubstitutionMetadataReady)) return;
            // Negative results depend on the locked producer sources, including
            // flexible outputs. Conversion/projectile domains not compiled here
            // stay unknown instead of being mislabeled as natural decorations.
            var tileDomains = MaterialSpecialDomains(typeof(TileID.Sets.Conversion));
            var wallDomains = MaterialSpecialDomains(typeof(WallID.Sets.Conversion));
            tileDomains.Add(127); // Ice Rod projectile, not createTile.
            tileDomains.Add(51);  // Original projectile 1054 and other web writers.
            bool version = typeof(Main).Assembly.GetName().Version == new Version(1, 4, 5, 8);
            var coverage = new BlueprintProducerCoverage(materialTileSourceComplete && version, materialWallSourceComplete && version,
                materialAbiValid && version, tileDomains, wallDomains, "Terraria 1.4.5.8; canonical createTile/createWall + tileWand + FlexibleTileWand options; special conversion/projectile domains retained unknown",
                TileID.Count, WallID.Count);
            long revision = ++placementRevision;
            materialCatalog = new BlueprintPlacementCatalog(revision, materialRules, materialWalls, materialPaints, materialCoatings, ItemID.Wire, ItemID.Actuator,
                declarations: materialDeclarations, coverage: coverage, substitution: CompileBlueprintSubstitution(revision, version));
        }
        private HashSet<int> MaterialSpecialDomains(Type source)
        {
            var values = new HashSet<int>();
            try
            {
                var fields = source.GetFields(BindingFlags.Public | BindingFlags.Static);
                bool found = false;
                foreach (var field in fields) if (field.FieldType == typeof(bool[]))
                {
                    found = true; var domain = field.GetValue(null) as bool[];
                    if (domain == null) { materialAbiValid = false; continue; }
                    for (int i = 0; i < domain.Length; i++) if (domain[i]) values.Add(i);
                }
                if (!found) materialAbiValid = false;
            }
            catch (MemberAccessException) { materialAbiValid = false; }
            catch (TargetInvocationException) { materialAbiValid = false; }
            return values;
        }
        private static void AddMaterialIdentity(Dictionary<int, int> table, int key, int item)
        { int old; table[key] = table.TryGetValue(key, out old) && old != item ? -1 : item; }
        private void CaptureMaterialPlacement(Item sample)
        {
            try { CaptureMaterialPlacementCore(sample); }
            catch (ArgumentException) { materialTileSourceComplete = false; }
            catch (IndexOutOfRangeException) { materialTileSourceComplete = false; }
            catch (OverflowException) { materialTileSourceComplete = false; }
            catch (TargetInvocationException) { materialTileSourceComplete = false; materialAbiValid = false; }
            catch (InvalidCastException) { materialTileSourceComplete = false; materialAbiValid = false; }
            catch (MemberAccessException) { materialTileSourceComplete = false; materialAbiValid = false; }
        }
        private void CaptureMaterialPlacementCore(Item sample)
        {
            if (sample.type <= 0 || !materialCaptured.Add(sample.type)) return;
            CaptureBlueprintSubstitutionExclusion(sample);
            if (sample.paint > 0) AddMaterialIdentity(materialPaints, sample.paint, sample.type);
            if (sample.paintCoating > 0) AddMaterialIdentity(materialCoatings, sample.paintCoating, sample.type);
            if (sample.createWall > 0)
            {
                materialDeclarations.Add(new BlueprintPlacementDeclaration(BlueprintPlacementKind.Wall, sample.type, -1, sample.placeStyle,
                    sample.createWall, BlueprintProducerSource.Direct, true));
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
                if (table == null) { materialTileSourceComplete = false; materialAbiValid = false; return; }
                foreach (DictionaryEntry pair in table)
                {
                    if (pair.Value == null) { materialTileSourceComplete = false; materialAbiValid = false; continue; }
                    int consumed = (int)materialConsume.GetValue(pair.Value);
                    var options = materialOptions.GetValue(pair.Value) as IList;
                    if (options == null) { materialTileSourceComplete = false; materialAbiValid = false; continue; }
                    for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
                    {
                        var option = options[optionIndex] as FlexibleTileWand.PlacementOption;
                        if (option == null) { materialTileSourceComplete = false; materialAbiValid = false; continue; }
                        CompileMaterialRules(sample, option.TileIdToPlace, option.TileStyleToPlace,
                            wand.ConsumesAmmoItem ? consumed : sample.consumable ? sample.type : 0,
                            wand.ConsumesAmmoItem || sample.consumable ? 1 : 0, true, BlueprintProducerSource.Flexible,
                            new BlueprintSpecialProducer(BlueprintSpecialProducerKind.Flexible, consumed, wand.ConsumesAmmoItem,
                                sample.consumable, optionIndex, options.Count));
                    }
                }
                // The real consumer first selects a flexible declaration; its
                // createTile fallback is not a second direct placement option.
                return;
            }
            if (wand != null) { materialTileSourceComplete = false; materialAbiValid = false; return; }
            // The original FigureOutWhatToPlace rewrites evil grass seeds on
            // mud. Preserve these distinct final outputs instead of treating
            // their missing direct createTile declarations as scenery.
            if (sample.createTile == 23 || sample.createTile == 199)
                CompileMaterialRules(sample, sample.createTile == 23 ? 661 : 662, sample.placeStyle,
                    sample.consumable ? sample.type : 0, sample.consumable ? 1 : 0, false, BlueprintProducerSource.Direct);
            if (sample.createTile >= 0) CompileMaterialRules(sample, sample.createTile, sample.placeStyle,
                sample.tileWand > 0 ? sample.tileWand : sample.consumable ? sample.type : 0,
                sample.tileWand > 0 || sample.consumable ? 1 : 0, sample.tileWand > 0,
                sample.tileWand > 0 ? BlueprintProducerSource.TileWand : BlueprintProducerSource.Direct,
                sample.tileWand > 0 ? new BlueprintSpecialProducer(BlueprintSpecialProducerKind.TileWand, sample.tileWand, true, sample.consumable) : null);
        }
        private void CompileMaterialRules(Item sample, int tile, int style, int consumed, long units, bool flexible, BlueprintProducerSource source,
            BlueprintSpecialProducer producer = null)
        {
            int declaration = materialDeclarations.Count;
            int firstRule = materialRules.Count;
            materialDeclarations.Add(new BlueprintPlacementDeclaration(BlueprintPlacementKind.Tile, sample.type, tile, style, 0, source, false));
            try
            {
                bool complete = CompileMaterialRulesCore(sample, tile, style, consumed, units, flexible, producer);
                materialDeclarations[declaration] = new BlueprintPlacementDeclaration(BlueprintPlacementKind.Tile, sample.type, tile, style, 0, source, complete);
            }
            catch (ArgumentException) { }
            catch (IndexOutOfRangeException) { }
            catch (OverflowException) { }
            catch (TargetInvocationException) { }
            for (int i = firstRule; i < materialRules.Count; i++) CaptureBlueprintSubstitutionRule(sample, materialRules[i]);
            // The retained uncompiled declaration scopes these failures. It is
            // not evidence that the corresponding player output is impossible.
        }
        private bool CompileMaterialRulesCore(Item sample, int tile, int style, int consumed, long units, bool flexible, BlueprintSpecialProducer producer)
        {
            if (tile < 0 || tile >= TileID.Count || style < 0) return false;
            if (tile == TileID.ClosedDoor)
            {
                materialRules.Add(new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, sample.type, consumed, units,
                    tile, style, flexible: flexible, width: 1, height: 3, originX: 0, originY: 0, requiresObject: true, producer: producer), false, door: true)); return true;
            }
            if (tile == TileID.PlanterBox)
            {
                // Locked .8 has no TileObjectData entry for this legacy cell.
                // PlaceTile writes Y=18*style; TileFrame writes only four X
                // connection slots. It is neither a platform nor Smart shape.
                if ((long)style * 18 > short.MaxValue) return false;
                materialRules.Add(new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, sample.type, consumed, units,
                    tile, style, flexible: flexible, producer: producer, planterBox: true), false));
                return true;
            }
            if (tile < TileID.Sets.Platforms.Length && TileID.Sets.Platforms[tile])
            { materialRules.Add(new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, sample.type, consumed, units, tile, style, flexible: flexible, platform: true, producer: producer), false)); return true; }
            if (!Main.tileFrameImportant[tile])
            {
                // These original PlaceThing_Tiles profiles transform an
                // existing substrate; createTile alone is not a complete
                // from-air body recipe. Keep the declared consumable known;
                // the finite nine recipes explain their base/conversion steps,
                // while other substrate profiles remain explicitly unresolved.
                bool substrate = tile == 2 || tile == 23 || tile == 199 || tile == 60 || tile == 70 || tile == 109 || tile == 633 || tile == 661 || tile == 662 ||
                    tile < Main.tileMoss.Length && Main.tileMoss[tile];
                if (style == 0) materialRules.Add(new BlueprintPlacementRule(new BlueprintPlacementDescriptor(BlueprintPlacementKind.Tile, sample.type, consumed, units, tile, style,
                    flexible: flexible, requiresSubstrate: substrate, producer: producer), true));
                return style == 0;
            }
            var root = TileObjectData.GetTileData(tile, style);
            if (root == null) return false;
            bool complete = true;
            for (int alternate = 0; alternate <= root.AlternatesCount; alternate++)
            {
                var data = TileObjectData.GetTileData(tile, style, alternate);
                if (data == null) { complete = false; continue; }
                if (data.GetStyleOverride != null)
                {
                    // These are the three current original selectors already
                    // supported by capture. Compile their declared regions;
                    // no selector delegate survives in the published catalog.
                    var basis = TileObjectData.GetTileData(tile, int.MaxValue);
                    if (basis?.GetStyleOverride == TileObjectData.GetStyle_Detritus) { complete &= CompileSelector(sample, tile, style, consumed, units, flexible, basis, 0, producer); complete &= CompileSelector(sample, tile, style, consumed, units, flexible, basis, 36, producer); }
                    else if (basis?.GetStyleOverride == TileObjectData.GetStyle_SmallPiles) { complete &= CompileSelector(sample, tile, style, consumed, units, flexible, basis, 0, producer); complete &= CompileSelector(sample, tile, style, consumed, units, flexible, basis, 18, producer); }
                    else if (basis?.GetStyleOverride == TileObjectData.GetStyle_Stalactite) foreach (int y in new[] { 0, 36, 72, 90 }) complete &= CompileSelector(sample, tile, style, consumed, units, flexible, basis, y, producer);
                    else complete = false;
                    continue;
                }
                // CanPlace reads its random period from the root profile,
                // then encodes using the chosen alternate's geometry.
                var randomData = producer?.Kind == BlueprintSpecialProducerKind.Flexible ? root : data;
                if (randomData.SpecificRandomStyles != null)
                    for (int index = 0; index < randomData.SpecificRandomStyles.Length; index++)
                        complete &= CompileGrid(sample, tile, style, consumed, units, flexible, data, alternate,
                            randomData.SpecificRandomStyles[index] - style,
                            producer?.WithRandom(randomData.SpecificRandomStyles.Length, index, randomData.SpecificRandomStyles[index] - style));
                else for (int random = 0; random < Math.Max(1, randomData.RandomStyleRange); random++)
                    complete &= CompileGrid(sample, tile, style, consumed, units, flexible, data, alternate, random,
                        producer?.WithRandom(Math.Max(1, randomData.RandomStyleRange), random, random));
            }
            return complete;
        }
        private bool CompileSelector(Item sample, int tile, int style, int consumed, long units, bool flexible, TileObjectData basis, int regionY, BlueprintSpecialProducer producer)
        {
            var probe = new Tile { type = (ushort)tile, frameY = (short)regionY }; probe.active(true);
            var selected = basis.GetStyleOverride(probe, basis);
            if (selected == null) return false;
            return AddMaterialRule(sample, tile, style, consumed, units, flexible, selected, 0, checked(style * selected.CoordinateFullWidth), regionY, producer);
        }
        private bool CompileGrid(Item sample, int tile, int style, int consumed, long units, bool flexible, TileObjectData data, int alternate, int random, BlueprintSpecialProducer producer)
        {
            if (data.StyleMultiplier <= 0 || data.CoordinateFullWidth <= 0 || data.CoordinateFullHeight <= 0) return false;
            int encoded = data.CalculatePlacementStyle(style, alternate, random), cross = 0;
            if (encoded < 0) return false;
            if (data.StyleWrapLimit > 0) { cross = encoded / data.StyleWrapLimit * data.StyleLineSkip; encoded %= data.StyleWrapLimit; }
            return AddMaterialRule(sample, tile, style, consumed, units, flexible, data, alternate,
                checked((data.StyleHorizontal ? encoded : cross) * data.CoordinateFullWidth), checked((data.StyleHorizontal ? cross : encoded) * data.CoordinateFullHeight), producer);
        }
        private bool AddMaterialRule(Item sample, int tile, int style, int consumed, long units, bool flexible, TileObjectData data, int alternate, int baseX, int baseY, BlueprintSpecialProducer producer)
        {
            if (data.Width <= 0 || data.Height <= 0 || data.CoordinateHeights == null || data.CoordinateHeights.Length != data.Height || baseX < 0 || baseY < 0 || baseX > short.MaxValue || baseY > short.MaxValue) return false;
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
                alternate: alternate, direction: (int)data.Direction, requiresObject: true, producer: producer), false, baseX, baseY, data.CoordinateWidth + data.CoordinatePadding, rows, dx, dy, operatingY2: tile == 658 ? 108 : 0));
            return true;
        }
    }
}
