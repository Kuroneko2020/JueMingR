using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintWorldMatchChecks
    {
        // Called inside the existing real Host/world-match fixture. Original
        // writers below establish independent world facts, not an autobuild
        // success claim; actual construction has its own native-use checks.
        private static void MaterialProgress(object context, object host, object input, LibraryState library, Action step)
        {
            var p = Main.LocalPlayer; var inventory = p.inventory;
            bool auto = (bool)Get(host, "AutoBuildEnabled");
            object knowledge = Get(host, "materialKnowledge");
            using (EmptyWorld(host, 1024, 160)) try
            {
                Call(host, "SetAutoBuild", false);
                p.inventory = new Item[inventory.Length]; for (int i = 0; i < p.inventory.Length; i++) p.inventory[i] = new Item();
                p.inventory[0].SetDefaults(ItemID.StoneBlock); p.inventory[0].stack = 30;
                p.inventory[49].SetDefaults(ItemID.StoneBlock); p.inventory[49].stack = 50;
                p.inventory[50].SetDefaults(ItemID.StoneBlock); p.inventory[50].stack = 999;
                p.inventory[57].SetDefaults(ItemID.StoneBlock); p.inventory[57].stack = 999;
                Call(host, "SetMaterialViewDemand", true);
                NativeQuickItemChecks.Until(() => { step(); return GetOptional(host, "MaterialCatalog") != null; });
                var catalog = (BlueprintPlacementCatalog)Get(host, "MaterialCatalog");
                var profiles = MaterialNativeCatalog(host, catalog);
                MaterialInitialPending(host, input, library, step);

                var cells = Enumerable.Range(0, 50).Select(x => new CaptureCell { X = x, Layers = CaptureLayer.Tile, TileType = TileID.Stone }).ToArray();
                var entry = Small(library, cells);
                var stat = MaterialAwait(host, "MaterialAsset", entry, step);
                Require(stat.Status == BlueprintMaterialStatus.Exact && stat.Rows.Single().ItemType == ItemID.StoneBlock && stat.Total == 50,
                    "native direct declaration resolves TileID.Stone to ItemID.StoneBlock and exact fifty consumables");
                var first = Place(host, input, entry, 40, 70, step); var second = Place(host, input, entry, 400, 70, step);
                long firstId = ((BlueprintProjectionIdentity)Get(first, "Identity")).ProjectionGeneration;
                long secondId = ((BlueprintProjectionIdentity)Get(second, "Identity")).ProjectionGeneration;
                var all = MaterialAwait(host, "MaterialWorld", 0L, step);
                var row = all.Rows.Single();
                Require(all.Status == BlueprintMaterialStatus.Exact && row.Required == 100 && row.Held == 80 && row.Missing == 20,
                    "two real fixed instances aggregate 100/80/20; slots 50 and 57 are excluded");
                var one = MaterialAwait(host, "MaterialWorld", firstId, step).Rows.Single();
                Require(one.Required == 50 && one.Held == 80 && one.Missing == 0, "single real instance independently sees the same inventory without reservation");
                MaterialCatalogLifecycle(host, entry, catalog, profiles, step);
                MaterialAwait(host, "MaterialWorld", firstId, step);
                var identities = ((System.Collections.Generic.IReadOnlyList<BlueprintMaterialInstance>)Get(host, "MaterialInstances")).ToArray();
                long secondSequence = identities.Single(i => i.ProjectionGeneration == secondId).Sequence;
                Require(identities.Select(i => i.Sequence).Distinct().Count() == 2, "same template instances have distinct stable session sequence identities");
                NativeBlueprintMaterialsUiChecks.BeginInstanceScope(context, firstId, secondId);

                Require(WorldGen.PlaceTile(40, 70, TileID.Stone, true, true), "independent original world placement establishes one matched body");
                p.inventory[0].stack--;
                var changed = MaterialAwait(host, "MaterialWorld", 0L, step).Rows.Single();
                NativeQuickItemChecks.Until(() => { step(); changed = ((BlueprintMaterialSnapshot)Call(host, "MaterialWorld", 0L)).Rows.SingleOrDefault(); return changed != null && changed.Required == 99 && changed.Held == 79; });
                Require(changed.Missing == 20, "world proof and actual inventory observation refresh independently without double subtraction");
                for (int x = 1; x < 50; x++) Require(WorldGen.PlaceTile(40 + x, 70, TileID.Stone, true, true), "complete first instance with original isolated world writer");
                NativeQuickItemChecks.Until(() => { step(); return !Scene(host).Contains(first); });
                var remaining = MaterialAwait(host, "MaterialWorld", firstId, step).Rows.Single();
                Require(Scene(host).Contains(second) && remaining.Required == 50 && remaining.Held == 79,
                    "retired selected instance falls back to all while same-template sibling remains independent");
                Require(((System.Collections.Generic.IReadOnlyList<BlueprintMaterialInstance>)Get(host, "MaterialInstances")).Single().Sequence == secondSequence,
                    "another instance's retirement does not renumber the surviving instance");
                NativeBlueprintMaterialsUiChecks.AssertRetiredScope(context, secondId);
                AwaitSettled(host, step);
                Call(host, "SetMaterialViewDemand", false);
                long reads = (long)Get(host, "MaterialInventoryReads"), merges = (long)Get(host, "MaterialContributionSteps"), publishes = (long)Get(host, "MaterialPublicationSteps");
                for (int i = 0; i < 120; i++) step();
                Require((long)Get(host, "MaterialInventoryReads") == reads && (long)Get(host, "MaterialContributionSteps") == merges && (long)Get(host, "MaterialPublicationSteps") == publishes,
                    "closed material view and disabled build have no inventory observation, contribution merging or publication work");

                Call(host, "ClearProjection");
                host.GetType().GetField("materialKnowledge", Flags).SetValue(host, null);
                var complete = Small(library, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
                Place(host, input, complete, 40, 70, step);
                NativeQuickItemChecks.Until(() => { step(); return Scene(host).Length == 0; });
                Require(Scene(host).Length == 0, "unavailable material provider never delays existing complete-match natural retirement");
                host.GetType().GetField("materialKnowledge", Flags).SetValue(host, knowledge);
                MaterialFurnitureWallSeams(context, host, input, library, step);
                MaterialProjectionRetirement(context, host, input, library, step);
                Console.WriteLine("PASS: native Blueprint materials declaration identity; real worker static summary; two fixed instances 100/80/20 and single 50/80/0; slots 0/49 versus 50/57; world/inventory refresh, retirement fallback, stable numbering; 120 closed updates; matching retires without a material provider.");
            }
            finally
            {
                host.GetType().GetField("materialKnowledge", Flags).SetValue(host, knowledge);
                p.inventory = inventory; Call(host, "SetMaterialViewDemand", false); Call(host, "SetAutoBuild", auto);
            }
        }
        private static BlueprintMaterialSnapshot MaterialAwait(object host, string method, object argument, Action step)
        {
            BlueprintMaterialSnapshot result = null;
            NativeQuickItemChecks.Until(() => { step(); result = (BlueprintMaterialSnapshot)Call(host, method, argument); return result.Status != BlueprintMaterialStatus.Pending; });
            Require(result.Status == BlueprintMaterialStatus.Exact, "native requested material scope becomes exact: " + result.Status + " " + result.Error);
            return result;
        }
        private sealed class MaterialProfile
        { internal CaptureCell Cell; internal CaptureObject? Object; internal BlueprintPlacementDescriptor Placement; }
        private static List<MaterialProfile> MaterialNativeCatalog(object host, BlueprintPlacementCatalog catalog)
        {
            long randomBefore; int checks = 0, failures = 0; string firstFailure = null;
            var profiles = new List<MaterialProfile>();
            Action<string> fail = reason =>
            {
                failures++; if (firstFailure == null) firstFailure = reason;
                Console.WriteLine("BlueprintMaterials PROFILE FAIL " + reason);
            };
            var originalRandom = Main.rand;
            var expectedRandom = new Terraria.Utilities.UnifiedRandom(619); randomBefore = expectedRandom.Next();
            try
            {
                var sourceType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
                var source = (IBlueprintCaptureSource)Activator.CreateInstance(sourceType, Flags, null, new object[] { (Func<bool>)(() => true) }, null);
                foreach (int itemType in new[] { ItemID.WoodenTable, ItemID.WoodenChair, ItemID.GlassChair, 48, 306, 3270, 2699, 5472, 3977, 498, 1989, 4326, 275, 2625, 2626, 4072, 4073, 4071, 149, 1813, 4420, 4609, 966 })
                {
                    Item item = ContentSamples.ItemsByType[itemType]; var root = TileObjectData.GetTileData(item.createTile, item.placeStyle);
                    if (root == null) { fail("native original metadata missing item=" + itemType + " tile=" + item.createTile + " style=" + item.placeStyle); continue; }
                    for (int alternate = 0; alternate <= root.AlternatesCount; alternate++)
                    {
                        var data = TileObjectData.GetTileData(item.createTile, item.placeStyle, alternate);
                        if (data == null) { fail("native alternate metadata missing item=" + itemType + " alternate=" + alternate); continue; }
                        for (int variant = 0; variant < Math.Max(1, data.RandomStyleRange); variant++)
                        {
                            for (int x = 18; x < 38; x++) for (int y = 18; y < 38; y++) Main.tile[x, y] = new Tile();
                            if (!TileObject.Place(new TileObject { xCoord = 20, yCoord = 20, type = item.createTile, style = item.placeStyle, alternate = alternate, random = variant }))
                            { fail("native original frame writer rejected item=" + itemType + " alternate=" + alternate + " random=" + variant + " root=" + MaterialNativeData(root) + " requested=" + MaterialNativeData(data)); continue; }
                            for (int x = 0; x < data.Width; x++) for (int y = 0; y < data.Height; y++)
                            {
                                checks++;
                                string key = itemType + "/" + alternate + "/" + variant + "/" + x + "/" + y;
                                CaptureCell cell;
                                if (!source.Read(20 + x, 20 + y, out cell))
                                { fail("native original gated capture rejected " + key + " root=" + MaterialNativeData(root) + " requested=" + MaterialNativeData(data)); continue; }
                                var shape = source.Describe(cell); var descriptor = catalog.Tile(cell, shape?.Object);
                                bool correct = descriptor != null && descriptor.ConsumeItemType == itemType && descriptor.Units == 1;
                                string replay = "no unique material descriptor";
                                if (correct) correct = MaterialNativeReplay(source, descriptor, cell, variant, x, y, out replay);
                                if (!correct || x == 0 && y == 0)
                                {
                                    string detail = key + " itemTile=" + item.createTile + " itemStyle=" + item.placeStyle +
                                        " root=" + MaterialNativeData(root) + " requested=" + MaterialNativeData(data) +
                                        " reverse=" + MaterialNativeData(TileObjectData.GetTileData(Main.tile[20 + x, 20 + y])) +
                                        " replay=" + replay + " " + MaterialNativeProfile(catalog, cell, shape?.Object, descriptor);
                                    if (!correct) fail(detail); else Console.WriteLine("BlueprintMaterials PROFILE PASS " + detail);
                                }
                                if (correct && x == 0 && y == 0) profiles.Add(new MaterialProfile { Cell = cell, Object = shape?.Object, Placement = descriptor });
                            }
                        }
                    }
                }
                Require(failures == 0, "native material all 23 groups/all alternates/random/member failures=" + failures + " checked=" + checks + "; first=" + firstFailure);
                MaterialFlexibleConsumption(source, catalog);
                // The original writer above owns its own possible side effects.
                // Only frozen material resolution is inside this RNG probe.
                Main.rand = new Terraria.Utilities.UnifiedRandom(619);
                var probe = new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone };
                for (int i = 0; i < 120; i++) catalog.Tile(probe);
                Require(Main.rand.Next() == randomBefore, "pure material resolution and native forced frame oracle do not consume gameplay RNG");
                Require(catalog.Paint(1) > 0 && catalog.Coating(1) > 0 && catalog.Coating(2) == ItemID.EchoCoating,
                    "paint and independent coating IDs come from actual item declarations");
                var seeded = new CaptureCell { Layers = CaptureLayer.Tile, TileType = 23 };
                var seed = catalog.Tile(seeded); var seededMaterials = new BlueprintMaterialAccumulator();
                BlueprintMaterialSteps.StaticCell(seeded, catalog, seededMaterials);
                Require(seed != null && seed.RequiresSubstrate && seed.ConsumeItemType == 59 &&
                    seededMaterials.Status == BlueprintMaterialStatus.Unresolved && seededMaterials.Snapshot(1).Rows.Any(r => r.ItemType == 59),
                    "original Corrupt Seeds createTile declaration remains known without claiming its missing substrate as a free block");
                Console.WriteLine("BlueprintMaterials native declared furniture member variants=" + checks + " catalogRevision=" + catalog.Revision);
                return profiles;
            }
            finally { Main.rand = originalRandom; }
        }
        private static bool MaterialNativeReplay(IBlueprintCaptureSource source, BlueprintPlacementDescriptor descriptor,
            CaptureCell original, int random, int memberX, int memberY, out string result)
        {
            // Consumption lookup cannot overwrite native operation fields
            // with reverse canonical metadata. Replay the returned descriptor
            // through the independent original frame writer, including true
            // alternate aliases. This is a descriptor oracle, not ItemCheck
            // execution or permission to skip the real CanPlace consumer.
            var data = TileObjectData.GetTileData(descriptor.TileType, descriptor.Style, descriptor.Alternate);
            if (data == null || data.Width != descriptor.Width || data.Height != descriptor.Height ||
                data.Origin.X != descriptor.OriginX || data.Origin.Y != descriptor.OriginY || (int)data.Direction != descriptor.Direction ||
                memberX >= descriptor.Width || memberY >= descriptor.Height)
            { result = "descriptor disagrees with original operation metadata " + MaterialNativeData(data); return false; }
            for (int x = 48; x < 68; x++) for (int y = 18; y < 38; y++) Main.tile[x, y] = new Tile();
            if (!TileObject.Place(new TileObject { xCoord = 50, yCoord = 20, type = descriptor.TileType,
                style = descriptor.Style, alternate = descriptor.Alternate, random = random }))
            { result = "original descriptor frame replay rejected"; return false; }
            CaptureCell replayed;
            if (!source.Read(50 + memberX, 20 + memberY, out replayed))
            { result = "original descriptor replay member unavailable"; return false; }
            bool same = replayed.TileType == original.TileType && replayed.FrameX == original.FrameX && replayed.FrameY == original.FrameY;
            result = "type=" + replayed.TileType + ",frame=" + replayed.FrameX + "," + replayed.FrameY + ",matching=" + same;
            return same;
        }
        private static string MaterialNativeData(TileObjectData data)
        {
            if (data == null) return "null";
            return "{size=" + data.Width + "x" + data.Height + ",origin=" + data.Origin.X + "," + data.Origin.Y +
                ",direction=" + (int)data.Direction + ",style=" + data.Style + ",multiplier=" + data.StyleMultiplier +
                ",horizontal=" + data.StyleHorizontal + ",wrap=" + data.StyleWrapLimit + ",lineSkip=" + data.StyleLineSkip +
                ",random=" + data.RandomStyleRange + ",specific=[" + (data.SpecificRandomStyles == null ? "" : string.Join(",", data.SpecificRandomStyles)) +
                "],coordinate=" + data.CoordinateWidth + "+" + data.CoordinatePadding + ",full=" + data.CoordinateFullWidth + "x" + data.CoordinateFullHeight +
                ",paddingFix=" + data.CoordinatePaddingFix.X + "," + data.CoordinatePaddingFix.Y + ",rows=[" +
                (data.CoordinateHeights == null ? "" : string.Join(",", data.CoordinateHeights)) + "]}";
        }
        private static string MaterialNativeDescriptor(BlueprintPlacementDescriptor value)
        {
            if (value == null) return "null";
            return "{kind=" + value.Kind + ",use=" + value.UseItemType + ",consume=" + value.ConsumeItemType + ",units=" + value.Units +
                ",tile=" + value.TileType + ",wall=" + value.WallType + ",style=" + value.Style + ",size=" + value.Width + "x" + value.Height +
                ",origin=" + value.OriginX + "," + value.OriginY + ",alternate=" + value.Alternate + ",direction=" + value.Direction +
                ",flexible=" + value.Flexible + ",object=" + value.RequiresObject + ",platform=" + value.Platform + ",substrate=" + value.RequiresSubstrate + "}";
        }
        private static string MaterialNativeProfile(BlueprintPlacementCatalog catalog, CaptureCell cell, CaptureObject? obj, BlueprintPlacementDescriptor result)
        {
            string canonical = "null";
            if (obj.HasValue)
            {
                var value = obj.Value;
                canonical = "{id=" + value.Id + ",xy=" + value.X + "," + value.Y + ",type=" + value.Type + ",size=" + value.Width + "x" + value.Height +
                    ",origin=" + value.OriginX + "," + value.OriginY + ",style=" + value.Style + ",alternate=" + value.Alternate + ",direction=" + value.Direction +
                    ",placement=" + value.PlacementStyle + ",random=" + value.RandomVariant + ",reliable=" + value.Reliable + ",reason=" + value.Reason + "}";
            }
            var table = (Dictionary<int, BlueprintPlacementRule[]>)Get(catalog, "tiles"); BlueprintPlacementRule[] rules;
            var matching = new List<string>();
            if (table.TryGetValue(cell.TileType, out rules)) foreach (var rule in rules) if (rule.Matches(cell))
                matching.Add("{base=" + rule.BaseX + "," + rule.BaseY + ",step=" + rule.ColumnStep + ",operating=" + rule.OperatingX + "," + rule.OperatingY + "," + rule.OperatingY2 +
                    ",rows=[" + string.Join(",", (int[])Get(rule, "rows")) + "],simple=" + rule.Simple + ",door=" + rule.Door + ",descriptor=" + MaterialNativeDescriptor(rule.Descriptor) + "}");
            return "cell={xy=" + cell.X + "," + cell.Y + ",type=" + cell.TileType + ",frame=" + cell.FrameX + "," + cell.FrameY + ",layers=" + cell.Layers +
                "} canonical=" + canonical + " result=" + MaterialNativeDescriptor(result) + " matchingRules=[" + string.Join(";", matching) + "]";
        }
        private static void MaterialInitialPending(object host, object input, LibraryState library, Action step)
        {
            var cells = Enumerable.Range(0, 300).Select(x => new CaptureCell { X = x, Layers = CaptureLayer.Tile, TileType = TileID.Stone }).ToArray();
            var entry = Small(library, cells); bool pending = false;
            Action watched = () =>
            {
                step(); if (Scene(host).Length == 0) return;
                var value = (BlueprintMaterialSnapshot)Call(host, "MaterialWorld", 0L);
                pending |= value.Status == BlueprintMaterialStatus.Pending;
                Require(value.Status != BlueprintMaterialStatus.Exact || value.Total == 300,
                    "a new fixed scene without its first complete material proof cannot publish exact zero");
            };
            Place(host, input, entry, 600, 70, watched);
            Require(MaterialAwait(host, "MaterialWorld", 0L, watched).Total == 300 && pending,
                "the real 300-target scene progresses from Pending to its complete exact bill across the existing read budget");
            Call(host, "ClearProjection"); Call(host, "SetMaterialViewDemand", true);
        }
        private static void MaterialCatalogLifecycle(object host, LibraryEntry entry, BlueprintPlacementCatalog original,
            List<MaterialProfile> profiles, Action step)
        {
            var native = Get(Get(host, "materialKnowledge"), "Native");
            long reads = (long)Get(native, "PlacementReads"), demands = (long)Get(host, "MaterialSourceDemands");
            MaterialAwait(host, "MaterialWorld", 0L, step);
            for (int i = 0; i < 120; i++) step();
            Require(ReferenceEquals(original, Get(host, "MaterialCatalog")) && (long)Get(native, "PlacementReads") == reads &&
                (long)Get(host, "MaterialSourceDemands") == demands, "warm material viewing keeps the frozen catalog and does not rescan its declarations or stable world sources");
            string language = Language.ActiveCulture.Name;
            var previous = original;
            try
            {
                foreach (string next in new[] { language == "zh-Hans" ? "en-US" : "zh-Hans", language })
                {
                    string beforeName = (string)Call(host, "MaterialName", ItemID.StoneBlock); long nameRevision = (long)Get(host, "MaterialNameRevision");
                    LanguageManager.Instance.SetLanguage(next); Lang.InitializeLegacyLocalization(); step();
                    Require(GetOptional(host, "MaterialCatalog") == null && ((BlueprintMaterialSnapshot)Call(host, "MaterialWorld", 0L)).Status == BlueprintMaterialStatus.Pending,
                        "a real language invalidation withdraws the old exact bill while the shared placement catalog rebuilds");
                    BlueprintPlacementCatalog current = null;
                    NativeQuickItemChecks.Until(() =>
                    {
                        long before = (long)Get(native, "PlacementReads"); step(); long after = (long)Get(native, "PlacementReads");
                        Call(native, "StepPlacement");
                        Require(after - before <= 64 && (long)Get(native, "PlacementReads") == after,
                            "material and another same-update placement consumer share one bounded declaration pass");
                        current = (BlueprintPlacementCatalog)GetOptional(host, "MaterialCatalog"); return current != null;
                    });
                    Require(current.Revision > previous.Revision && !ReferenceEquals(current, previous), "real language event publishes a new placement identity");
                    foreach (var profile in profiles)
                    {
                        var value = current.Tile(profile.Cell, profile.Object); var old = profile.Placement;
                        Require(value != null && value.UseItemType == old.UseItemType && value.ConsumeItemType == old.ConsumeItemType && value.Units == old.Units &&
                            value.Style == old.Style && value.Width == old.Width && value.Height == old.Height && value.OriginX == old.OriginX && value.OriginY == old.OriginY &&
                            value.Alternate == old.Alternate && value.Direction == old.Direction,
                            "actual captured item/style/alternate/random profiles retain their identities after a real language rebuild");
                    }
                    string name = (string)Call(host, "MaterialName", ItemID.StoneBlock);
                    Require(name == Lang.GetItemNameValue(ItemID.StoneBlock) && name != beforeName && (long)Get(host, "MaterialNameRevision") > nameRevision,
                        "material names and their layout revision follow the actual current native language");
                    Require(MaterialAwait(host, "MaterialWorld", 0L, step).Rows.Single().Required == 100 && MaterialAwait(host, "MaterialAsset", entry, step).Total == 50,
                        "existing dynamic and fixed v1 asset demand rederive under the new catalog identity without false zero");
                    previous = current;
                }
                Console.WriteLine("PASS BlueprintMaterials shared cold/warm catalog, same-update 64 declaration budget, real language invalidation, item/style/alternate identity and current-language names.");
            }
            finally { if (Language.ActiveCulture.Name != language) { LanguageManager.Instance.SetLanguage(language); Lang.InitializeLegacyLocalization(); } }
        }
        private static void MaterialFlexibleConsumption(IBlueprintCaptureSource source, BlueprintPlacementCatalog catalog)
        {
            var p = new Player();
            for (int i = 0; i < p.inventory.Length; i++) p.inventory[i] = new Item();
            p.inventory[0].SetDefaults(5324); p.selectedItemState.Select(0); p.selectedItemState.Update();
            p.inventory[1].SetDefaults(ItemID.StoneBlock); p.inventory[1].stack = 4;
            int seed = Player.FlexibleWandRandomSeed, cycle = Player.FlexibleWandCycleOffset;
            try
            {
                Player.FlexibleWandRandomSeed = 619; Player.FlexibleWandCycleOffset = 0;
                var wand = p.inventory[0].GetFlexibleTileWand();
                FlexibleTileWand.PlacementOption option; Item consumed;
                Require(wand != null && wand.ConsumesAmmoItem && wand.TryGetPlacementOption(p, 619, 0, out option, out consumed),
                    "original flexible-wand selector finds the isolated real stone stack");
                Require(wand.TryGetPlacementOption(p, 619, 0, out option, out consumed) && ReferenceEquals(consumed, p.inventory[1]),
                    "original flexible selection owns the consumable stack separately from the tool");
                var data = TileObjectData.GetTileData(option.TileIdToPlace, option.TileStyleToPlace);
                Require(data != null, "original flexible placement has a declared frame profile");
                for (int x = 18; x < 38; x++) for (int y = 18; y < 38; y++) Main.tile[x, y] = new Tile();
                Require(TileObject.Place(new TileObject { xCoord = 20, yCoord = 20, type = option.TileIdToPlace, style = option.TileStyleToPlace }),
                    "original flexible frame writer establishes the selected variant independently");
                CaptureCell cell; Require(source.Read(20, 20, out cell), "flexible result passes the original capture reader");
                var descriptor = catalog.Tile(cell, source.Describe(cell)?.Object);
                Require(descriptor != null && descriptor.Flexible && descriptor.UseItemType == 5324 &&
                    descriptor.ConsumeItemType == ItemID.StoneBlock && descriptor.Units == 1,
                    "shared material declaration names the actual flexible consumable, not the wand");
                p.PlaceThing_Tiles_PlaceIt_ConsumeFlexibleWandMaterial();
                Require(p.inventory[1].stack == 3 && p.inventory[0].type == 5324 && p.inventory[0].stack == 1,
                    "actual original flexible consume method removes one declared stone and preserves its tool");
            }
            finally { Player.FlexibleWandRandomSeed = seed; Player.FlexibleWandCycleOffset = cycle; }
        }
    }
}
