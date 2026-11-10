using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Microsoft.Xna.Framework.Input;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintImportedMaterialsChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run(object context, ProbeGraphics graphics = null, string output = null)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var library = (LibraryState)Get(host, "Library");
            NativeBlueprintWorldMatchChecks.InitializeMetadata();
            typeof(NativeLegacyPlacementIdentityChecks).GetMethod("PrepareTrackMetadata", F).Invoke(null, null);
            var profile = (IBlueprintImportProfile)host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintHorizontalMirrorResolver").GetMethod("Create", F).Invoke(null, null);
            var receipt = (BlueprintExchangeReceipt)typeof(NativeBlueprintHelperImportChecks).GetMethod("Import", F).Invoke(null, new object[] { library, profile,
                Path.Combine(Program.Repository, "references/JMR_Blueprint_Hoik_Zoom_20261110_v1/attachments/岛牌刷怪场4.0.json") });
            Require(receipt.Status == BlueprintExchangeStatus.Completed, "actual supplied sample imports into the standard library");
            var page = (LibraryPage)typeof(NativeBlueprintHelperImportChecks).GetMethod("List", F).Invoke(null, new object[] { library });
            var entry = page.Entries.Single(e => e.AssetId == receipt.AssetId);
            Action tick = () => { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); Call(host, "Poll"); };
            Call(host, "SetMaterialViewDemand", true);
            NativeQuickItemChecks.Until(() => { tick(); return GetOptional(host, "MaterialCatalog") != null; });
            var catalog = (BlueprintPlacementCatalog)Get(host, "MaterialCatalog");
            BlueprintMaterialSnapshot value = null;
            using (NativeBlueprintLibraryWait.ObserveHost(library))
                NativeBlueprintLibraryWait.HostCondition(library, "real imported native material bill", () => { tick(); value = (BlueprintMaterialSnapshot)Call(host, "MaterialAsset", entry); return value.Status != BlueprintMaterialStatus.Pending; });
            Console.WriteLine("IMPORTED MATERIALS status=" + value.Status + " error=" + value.Error + " rows=" + value.Rows.Count + " kinds=" + value.KindCount + " total=" + value.Total + " catalogRevision=" + catalog.Revision);
            Console.WriteLine("IMPORTED ASSET identity=" + entry.ContentHash + " revision=" + entry.ContentRevision);
            Console.WriteLine("IMPORTED MATERIAL ROWS " + String.Join(";", value.Rows.Select(r => r.ItemType + ":" + r.Required)));
            var window = (BlueprintProjectionWindow)typeof(NativeBlueprintBuildChecks).GetMethod("OpenCapturedAppearance", F).Invoke(null, new object[] { library, entry });
            var objects = window.Objects.ToDictionary(o => o.Id);
            int failures = 0;
            var unknown = new Dictionary<string, int>();
            var unknownFirst = new Dictionary<string, string>();
            foreach (CaptureCell cell in window.Cells)
            {
                CaptureObject obj; CaptureObject? description = cell.ObjectId > 0 && objects.TryGetValue(cell.ObjectId, out obj) ? obj : (CaptureObject?)null;
                var bill = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.StaticCell(cell, catalog, bill, description);
                if (bill.Status == BlueprintMaterialStatus.Unresolved)
                {
                    var qualification = catalog.QualifyTile(cell, description);
                    var tileOnly = cell; tileOnly.Layers &= CaptureLayer.Tile;
                    var tileBill = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.StaticCell(tileOnly, catalog, tileBill, description);
                    string key = tileBill.Status == BlueprintMaterialStatus.Unresolved ? "tile " + cell.TileType + " " + qualification.Reason : "wall " + cell.WallType + " " + catalog.QualifyWall(cell.WallType).Reason;
                    int count; unknown.TryGetValue(key, out count); unknown[key] = count + 1;
                    if (!unknownFirst.ContainsKey(key)) unknownFirst[key] = cell.X + "," + cell.Y + " frame=" + cell.FrameX + "," + cell.FrameY + " object=" + cell.ObjectId;
                    if (failures < 12) Console.WriteLine("IMPORTED UNKNOWN cell=" + cell.X + "," + cell.Y + " tile=" + cell.TileType + " wall=" + cell.WallType + " frame=" + cell.FrameX + "," + cell.FrameY + " object=" + cell.ObjectId + " reason=" + bill.Error + " tileReason=" + qualification.MaterialReason + " disposition=" + qualification.Disposition);
                    failures++;
                }
            }
            foreach (CaptureObject obj in window.Objects)
            {
                CaptureCell first = window.Cells.First(c => c.ObjectId == obj.Id); var bill = new BlueprintMaterialAccumulator(); BlueprintMaterialSteps.StaticObject(obj, first, catalog, bill);
                if (bill.Status == BlueprintMaterialStatus.Unresolved)
                {
                    string key = "object " + obj.Type + " " + catalog.QualifyTile(first, obj).Reason; int count; unknown.TryGetValue(key, out count); unknown[key] = count + 1;
                    if (!unknownFirst.ContainsKey(key)) unknownFirst[key] = obj.X + "," + obj.Y + " frame=" + first.FrameX + "," + first.FrameY + " object=" + obj.Id;
                    if (failures < 12) Console.WriteLine("IMPORTED UNKNOWN object=" + obj.Id + " xy=" + obj.X + "," + obj.Y + " type=" + obj.Type + " style=" + obj.Style + " placement=" + obj.PlacementStyle + " reason=" + bill.Error + " qualification=" + catalog.QualifyTile(first, obj).MaterialReason); failures++;
                }
            }
            Console.WriteLine("IMPORTED MATERIAL unknown cell/object entries=" + failures);
            foreach (var pair in unknown) Console.WriteLine("IMPORTED UNKNOWN GROUP " + pair.Key + " count=" + pair.Value + " first=" + unknownFirst[pair.Key]);
            Require(value.Status == BlueprintMaterialStatus.Unresolved && value.KindCount == 42 && value.Total == 18818 && value.Error == "目标形态的放置解释尚未确认",
                "real catalog retains unknown original frames while restoring eleven legal legacy material bodies");
            Require(value.Rows.Single(r => r.ItemType == ItemID.DartTrap).Required == 1 && value.Rows.Single(r => r.ItemType == 5135).Required == 8 && value.Rows.Single(r => r.ItemType == ItemID.MinecartTrack).Required == 2,
                "real declared trap styles and signed-index rails use their actual consumed item identities");
            NativeLegacyProfiles(host, catalog);
            Reload(context, host, library, entry, value, tick);
            Presentation(context, entry, value, tick, graphics, output);
            Console.WriteLine("PASS imported materials: real full catalog/MaterialAsset, partial42/18818 with exact unknown reason, native legacy profiles, immutable asset reopened without reimport, complete ordinary bill; card/detail Draw=" + (graphics != null ? "executed" : "separate visual scope") + "; full sample construction/multiplayer/FPS NOT_RUN.");
        }
        private static void NativeLegacyProfiles(object host, BlueprintPlacementCatalog catalog)
        {
            using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 120, 120))
            {
                // These original writers establish the expected frame/identity;
                // material qualification never writes these fields itself.
                Require(WorldGen.PlaceTile(60, 61, TileID.Stone, true, true), "native profile support");
                Require(WorldGen.PlaceTile(60, 60, TileID.PressurePlates, true, true, 0, 0), "native pressure-plate producer");
                Console.WriteLine("IMPORTED UNEXPLAINED plate sourceFrame=16,0 nativeStyle0Frame=" + Main.tile[60, 60].frameX + "," + Main.tile[60, 60].frameY + "; source raw frame is retained unresolved");
                Main.tile[60, 60].ClearEverything();
                Require(WorldGen.PlaceTile(60, 60, TileID.Traps, true, true, 0, 5), "native legacy trap producer");
                var trap = new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Traps, FrameX = Main.tile[60, 60].frameX, FrameY = Main.tile[60, 60].frameY };
                Require(catalog.Tile(trap).UseItemType == 5135, "native trap style5 writes the actual compiled row");
                trap.FrameX = 54; Require(catalog.Tile(trap).UseItemType == 5135, "original hammer direction bank keeps trap material");
                trap.FrameX = 108; Require(catalog.QualifyTile(trap).Resolution != BlueprintPlacementResolutionKind.Exact && BlueprintConstructionTargets.Select(trap, catalog).UnknownBits != 0,
                    "outside original trap direction domain stays unknown in the direct construction selector");
                Require(!(bool)host.GetType().GetMethod("OrdinaryHammerShapeType", F).Invoke(null, new object[] { (int)TileID.Traps }) &&
                    !(bool)host.GetType().GetMethod("OrdinaryHammerShapeType", F).Invoke(null, new object[] { (int)TileID.MinecartTrack }),
                    "material identity does not grant the ordinary hammer executor special trap-direction or rail-topology reconstruction");
                for (short slot = 0; slot < 36; slot++)
                {
                    var native = new Tile { frameX = slot, frameY = -1 }; int item = Minecart.GetTrackItem(native);
                    var track = new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.MinecartTrack, FrameX = slot, FrameY = -1 };
                    Require(catalog.Tile(track)?.UseItemType == item, "copied rail slot agrees with independent original GetTrackItem: " + slot);
                }
                var invalid = new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.MinecartTrack, FrameX = 36, FrameY = -1 };
                Require(catalog.QualifyTile(invalid).Resolution != BlueprintPlacementResolutionKind.Exact && BlueprintConstructionTargets.Select(invalid, catalog).UnknownBits != 0,
                    "rail decoration/invalid frame cannot earn an exact material or a construction permission");
                Main.tile[60, 60].ClearEverything(); Require(WorldGen.PlaceTile(60, 60, TileID.MinecartTrack, true, true, 0, 0), "original rail placement");
                WorldGen.SquareTileFrame(60, 60);
                Require(Main.tile[60, 60].frameY == -1 && catalog.Tile(new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.MinecartTrack, FrameX = Main.tile[60, 60].frameX, FrameY = -1 }).UseItemType == ItemID.MinecartTrack,
                    "native rail consumer produces legal signed back-rail index interpreted by materials");
            }
        }
        private static void Reload(object context, object host, LibraryState original, LibraryEntry entry, BlueprintMaterialSnapshot expected, Action tick)
        {
            string source = (string)Get(Get(Get(original, "worker"), "files"), "directory");
            string target = Path.Combine(Program.OutputDirectory, "reopened-existing-asset");
            string asset = entry.AssetId.ToString("N"), oldAsset = Path.Combine(source, asset), newAsset = Path.Combine(target, asset);
            foreach (string file in Directory.EnumerateFiles(oldAsset, "*", SearchOption.AllDirectories))
            { string copy = Path.Combine(newAsset, file.Substring(oldAsset.Length + 1)); Directory.CreateDirectory(Path.GetDirectoryName(copy)); File.Copy(file, copy); }
            using (var reopened = new LibraryState(new FileBlueprintLibrary(Path.GetFullPath(target))))
            {
                var field = host.GetType().GetField("<Library>k__BackingField", F); field.SetValue(host, reopened);
                try
                {
                    var page = (LibraryPage)typeof(NativeBlueprintHelperImportChecks).GetMethod("List", F).Invoke(null, new object[] { reopened });
                    var existing = page.Entries.Single(); Require(existing.AssetId == entry.AssetId && existing.ContentHash == entry.ContentHash, "reopened exact existing asset retains identity without another import");
                    BlueprintMaterialSnapshot value = null;
                    NativeQuickItemChecks.Until(() => { tick(); value = (BlueprintMaterialSnapshot)Call(host, "MaterialAsset", existing); return value.Status != BlueprintMaterialStatus.Pending; });
                    Require(value.Status == expected.Status && value.Error == expected.Error && value.Total == expected.Total && value.KindCount == expected.KindCount && value.Rows.Select(r => r.ItemType + ":" + r.Required).SequenceEqual(expected.Rows.Select(r => r.ItemType + ":" + r.Required)), "fresh library owner rebuilds the entire real material bill from the unchanged persisted body");
                    Console.WriteLine("IMPORTED REOPEN status=" + value.Status + " error=" + value.Error + " kinds=" + value.KindCount + " total=" + value.Total + " asset=" + existing.AssetId);
                }
                finally { field.SetValue(host, original); }
            }
        }
        private static void Presentation(object context, LibraryEntry entry, BlueprintMaterialSnapshot expected, Action tick, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State"); var ui = Get(shell, "BlueprintUi"); var popup = Get(shell, "BlueprintMaterialsUi");
            var renderer = Get(shell, "renderer"); var layout = Get(state, "Layout"); var input = Get(context, "Input");
            var oldFont = FontAssets.MouseText;
            try
            {
                if (graphics == null) FiniteCostChecks.SetCpuFont(10);
                else graphics.LoadItemTextures(expected.Rows.Select(r => r.ItemType));
                var summary = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintMaterialsLayout").GetMethod("Summary", F);
                string partial = (string)summary.Invoke(null, new object[] { expected, false });
                Require(partial == "材料已确认 42 种 · 数量 18818", "partial summary explicitly exposes only current confirmed contribution");
                var pending = new BlueprintMaterialSnapshot(999, BlueprintMaterialStatus.Pending, expected.Rows.ToArray());
                Require((string)summary.Invoke(null, new object[] { pending, false }) == "材料正在计算", "Pending with stale rows never presents their previous digits");
                var library = (LibraryState)Get(host, "Library");
                var ordinary = NativeBlueprintBudgetChecks.Save(library, new[] { new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone } }, 1, 1);
                BlueprintMaterialSnapshot exact = null;
                using (NativeBlueprintLibraryWait.ObserveHost(library)) NativeBlueprintLibraryWait.HostCondition(library, "ordinary real exact material asset", () =>
                { tick(); exact = (BlueprintMaterialSnapshot)Call(host, "MaterialAsset", ordinary); return exact.Status != BlueprintMaterialStatus.Pending; });
                Require(exact.Status == BlueprintMaterialStatus.Exact && (string)summary.Invoke(null, new object[] { exact, false }) == "材料 1 种 · 总计 1", "ordinary persisted complete material bill retains exact totals");
                if (graphics == null)
                {
                    var rect = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect");
                    object position = Activator.CreateInstance(rect, F, null, new object[] { 400f, 200f, 60f, 24f }, null);
                    Call(popup, "OpenAsset", entry, position);
                    var actual = (BlueprintMaterialSnapshot)Call(popup, "ReadSnapshot");
                    Require(actual.Status == expected.Status && actual.Total == expected.Total && actual.Error == expected.Error, "real detail snapshot consumes the current asset rather than a guessed UI bill");
                    Console.WriteLine("IMPORTED SUMMARY/DETAIL-SNAPSHOT " + partial + "; reason=" + expected.Error + "; ordinary=材料 1 种 · 总计 1; pending=材料正在计算; actual Draw in explicit visual scope");
                    return;
                }
                Main.screenWidth = 1280; Main.screenHeight = 900; Main.UIScale = 1; PlayerInput.CacheOriginalScreenDimensions();
                Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, 900f, 1f);
                Call(ui, "OpenLibrary"); Call(layout, "SetBlueprintLibraryVisible", true); Call(ui, "ShowSaved", entry.AssetId);
                using (NativeBlueprintLibraryWait.ObserveHost((LibraryState)Get(host, "Library"))) NativeBlueprintLibraryWait.HostCondition((LibraryState)Get(host, "Library"), "imported actual card summary", () =>
                {
                    tick(); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900));
                    var values = (IDictionary)Get(ui, "pageMaterials"); return values.Contains(entry.AssetId) && ((BlueprintMaterialSnapshot)values[entry.AssetId]).Status != BlueprintMaterialStatus.Pending;
                });
                string card = String.Concat(((IEnumerable)Get(ui, "parts")).Cast<object>().Select(p => (string)Get(Get(p, "Element"), "Text")));
                Require(card.Contains(partial), "actual library card renders partial confirmed totals");
                if (graphics != null) graphics.Image(Path.Combine(output, "imported-materials-card.png"), () => Call(ui, "Draw"), Main.UIScaleMatrix, 1280, 900);
                var rectType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect");
                object anchor = Activator.CreateInstance(rectType, F, null, new object[] { 400f, 200f, 60f, 24f }, null);
                Call(popup, "OpenAsset", entry, anchor); Call(popup, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900));
                Require((bool)Get(popup, "Visible") && (string)Get(popup, "summaryText") == partial && ((BlueprintMaterialSnapshot)Call(popup, "ReadSnapshot")).Error == expected.Error,
                    "actual same-asset detail renders confirmed totals and retains the original unknown reason");
                if (graphics != null) graphics.Image(Path.Combine(output, "imported-materials-detail.png"), () => Call(popup, "Draw"), Main.UIScaleMatrix, 1280, 900);
                graphics.LoadItemTextures(new[] { (int)ItemID.StoneBlock });
                Call(popup, "OpenAsset", ordinary, anchor); Call(popup, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900));
                Require((string)Get(popup, "summaryText") == "材料 1 种 · 总计 1", "actual ordinary saved-asset detail preserves exact totals");
                graphics.Image(Path.Combine(output, "ordinary-materials-detail.png"), () => Call(popup, "Draw"), Main.UIScaleMatrix, 1280, 900);
                Console.WriteLine("IMPORTED CARD/DETAIL " + partial + "; reason=" + expected.Error + "; ordinary=材料 1 种 · 总计 1; pending=材料正在计算");
            }
            finally { Call(popup, "Close"); Call(shell, "CloseAndSubmitPosition"); FontAssets.MouseText = oldFont; }
        }
    }
}
