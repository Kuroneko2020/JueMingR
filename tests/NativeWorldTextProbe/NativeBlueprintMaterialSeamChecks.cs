using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintWorldMatchChecks
    {
        private static void MaterialFurnitureAt(int x, int y, int item, byte wallPaint)
        {
            for (int dx = 0; dx < 4; dx++) for (int dy = 0; dy < 3; dy++) Main.tile[x + dx, y + dy] = new Tile();
            Require(WorldGen.PlaceTile(x, y + 2, TileID.Stone, true, true), "original chair support");
            if (item != 0)
            {
                var sample = ContentSamples.ItemsByType[item];
                Require(WorldGen.PlaceObject(x, y + 1, sample.createTile, true, sample.placeStyle, direction: -1), "original complete chair body");
            }
            for (int dy = 0; dy < 2; dy++)
            {
                WorldGen.PlaceWall(x, y + dy, WallID.Wood, true);
                if (item != 0)
                {
                    WorldGen.paintTile(x, y + dy, PaintID.RedPaint, false, false);
                    WorldGen.paintCoatTile(x, y + dy, 1, false, false);
                    WorldGen.paintCoatTile(x, y + dy, 2, false, false);
                }
            }
            if (wallPaint != 0) WorldGen.paintWall(x, y, wallPaint, false, false);
        }
        private static LibraryEntry MaterialFurnitureAsset(object context, object host, object input, LibraryState library, Action step)
        {
            MaterialFurnitureAt(60, 41, ItemID.WoodenChair, 0); WorldGen.PlaceWire(62, 41);
            NativeLiquidStore(60, 41, 64, 0);
            return CaptureCompletionAsset(context, host, input, library, step, 60, 41, 3, 2, "家具墙面材料接缝");
        }
        private static BlueprintMaterialSnapshot MaterialSeamResult(object host, long scope, Action step)
        {
            BlueprintMaterialSnapshot result = null;
            NativeQuickItemChecks.Until(() =>
            {
                step(); result = (BlueprintMaterialSnapshot)Call(host, "MaterialWorld", scope);
                return result.Status != BlueprintMaterialStatus.Pending;
            });
            return result;
        }
        private static void MaterialSeamRows(BlueprintMaterialSnapshot result, params int[] itemCounts)
        {
            var expected = new Dictionary<int, long>();
            for (int i = 0; i < itemCounts.Length; i += 2) expected.Add(itemCounts[i], itemCounts[i + 1]);
            Require(MaterialSeamRowsEqual(result, expected),
                "real split furniture/wall material bill: " + result.Status + " " + result.Error + " " +
                string.Join(",", result.Rows.Select(row => row.ItemType + ":" + row.Required)));
        }
        private static bool MaterialSeamRowsEqual(BlueprintMaterialSnapshot result, Dictionary<int, long> expected)
        { return result.Status == BlueprintMaterialStatus.Exact && result.Rows.Count == expected.Count && result.Rows.All(row => expected.TryGetValue(row.ItemType, out var count) && row.Required == count); }
        private static void MaterialSeamAwait(object host, long scope, Action step, params int[] itemCounts)
        {
            var expected = new Dictionary<int, long>();
            for (int i = 0; i < itemCounts.Length; i += 2) expected.Add(itemCounts[i], itemCounts[i + 1]);
            BlueprintMaterialSnapshot result = null;
            try
            {
                NativeQuickItemChecks.Until(() => { step(); result = (BlueprintMaterialSnapshot)Call(host, "MaterialWorld", scope); return MaterialSeamRowsEqual(result, expected); });
            }
            catch
            {
                if (result != null) Console.WriteLine("material seam awaiting " + string.Join(",", expected.Select(p => p.Key + ":" + p.Value)) +
                    " actual " + result.Status + " " + result.Error + " " + string.Join(",", result.Rows.Select(r => r.ItemType + ":" + r.Required)));
                throw;
            }
        }
        private static void MaterialNativeScraper(object input)
        {
            var player = Main.LocalPlayer; var savedItem = player.inventory[12]; int selected = player.selectedItem;
            var position = player.position; var camera = Main.screenPosition; bool mouseInterface = player.mouseInterface;
            int mouseX = Main.mouseX, mouseY = Main.mouseY, tileX = Player.tileTargetX, tileY = Player.tileTargetY;
            try
            {
                MaterialFurnitureAt(44, 41, ItemID.WoodenChair, PaintID.BluePaint);
                WorldGen.paintCoatWall(44, 41, 1, false, false); WorldGen.paintCoatWall(44, 41, 2, false, false);
                // This direct ItemCheck fixture starts a world-only input
                // frame. Normal Main.Draw clears the preceding UI hover;
                // earlier offscreen visual leaves cannot supply that reset.
                player.mouseInterface = false;
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                NativeQuickItemChecks.Until(() => { NativeQuickItemChecks.NativeFrame(player); return player.itemAnimation == 0 && player.itemTime == 0; });
                player.inventory[12] = new Item(); player.inventory[12].SetDefaults(ItemID.PaintScraper); player.selectedItemState.Select(12);
                player.position = new Microsoft.Xna.Framework.Vector2(42 * 16, 42 * 16 - player.height); Main.screenPosition = Microsoft.Xna.Framework.Vector2.Zero;
                Main.mouseX = 44 * 16 + 8; Main.mouseY = 41 * 16 + 8; Player.tileTargetX = 44; Player.tileTargetY = 41;
                Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true; Main.mouseLeft = true;
                int phase = 0;
                try { NativeQuickItemChecks.Until(() =>
                {
                    NativeQuickItemChecks.NativeFrame(player); var tile = Main.tile[44, 41];
                    int next = tile.color() != 0 ? 0 : tile.invisibleBlock() || tile.fullbrightBlock() ? 1 : tile.wallColor() != 0 ? 2 : tile.invisibleWall() || tile.fullbrightWall() ? 3 : 4;
                    if (next == phase) return false;
                    Require(next == phase + 1, "native scraper advances exactly Tile paint, both Tile coatings, Wall paint, both Wall coatings");
                    if (next <= 2) Require(tile.wallColor() == PaintID.BluePaint && tile.invisibleWall() && tile.fullbrightWall(), "foreground scraper passes preserve the wall until reached");
                    if (next == 1) Require(tile.invisibleBlock() && tile.fullbrightBlock(), "first scrape removes Tile paint before either Tile coating");
                    if (next == 3) Require(tile.invisibleWall() && tile.fullbrightWall(), "wall paint is removed before its coatings");
                    phase = next; return phase == 4;
                }); }
                catch
                {
                    Console.Error.WriteLine("SCRAPER native state phase=" + phase + " selected=" + player.selectedItem + " held=" + player.HeldItem.type +
                        " anim/time=" + player.itemAnimation + "/" + player.itemTime + " use/release=" + player.controlUseItem + "/" + player.releaseUseItem +
                        " mouseInterface=" + player.mouseInterface + " target=" + Player.tileTargetX + "," + Player.tileTargetY +
                        " position=" + player.position + " mouse=" + Main.mouseX + "," + Main.mouseY + " camera=" + Main.screenPosition +
                        " trigger=" + Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft + " raw=" + Main.mouseLeft);
                    throw;
                }
                Require(player.inventory[12].type == ItemID.PaintScraper && player.inventory[12].stack == 1, "scraper itself is a reusable original tool, not a material charge");
                Console.WriteLine("PASS original .8 scraper ItemCheck: Tile paint / both Tile coatings / Wall paint / both Wall coatings; reusable source unchanged.");
            }
            finally
            {
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                NativeQuickItemChecks.Until(() => { NativeQuickItemChecks.NativeFrame(player); return player.itemAnimation == 0 && player.itemTime == 0; });
                player.inventory[12] = savedItem; player.selectedItemState.Select(selected); player.selectedItemState.Update();
                player.mouseInterface = mouseInterface;
                player.position = position; Main.screenPosition = camera; Main.mouseX = mouseX; Main.mouseY = mouseY; Player.tileTargetX = tileX; Player.tileTargetY = tileY;
            }
        }
        private static void MaterialFurnitureWallSeams(object context, object host, object input, LibraryState library, Action step)
        {
            using (EmptyWorld(host, 1024, 160))
            try
            {
                MaterialNativeScraper(input);
                var entry = MaterialFurnitureAsset(context, host, input, library, step);
                var catalog = (BlueprintPlacementCatalog)Get(host, "MaterialCatalog");
                int paint = catalog.Paint(PaintID.RedPaint), light = catalog.Coating(1), echo = catalog.Coating(2), wire = catalog.WireItemType;
                MaterialFurnitureAt(44, 41, ItemID.WoodenChair, PaintID.BluePaint);
                Call(host, "SetMaterialViewDemand", true);
                var placed = Place(host, input, entry, 44, 41, step);
                long id = ((BlueprintProjectionIdentity)Get(placed, "Identity")).ProjectionGeneration;
                MaterialSeamAwait(host, id, step, wire, 1, paint, 1, light, 1, echo, 1);
                var objectRange = ((IEnumerable)Get(Get(placed, "Match"), "Ranges")).Cast<object>()
                    .Single(range => ((BlueprintProjectionSourceRange)Get(range, "Source")).ObjectId > 0);
                long objectVersion = (long)Get(objectRange, "Version");
                WorldGen.paintWall(44, 41, 0, false, false);
                NativeQuickItemChecks.Until(() =>
                {
                    var result = MaterialSeamResult(host, id, step);
                    return result.Status == BlueprintMaterialStatus.Exact && result.Rows.Count == 1 && result.Rows[0].ItemType == wire;
                });
                Require((bool)Get(objectRange, "Current") && (long)Get(objectRange, "Version") == objectVersion,
                    "wall-only changes revoke the coupled material contribution without revoking final furniture Tile proof");
                WorldGen.paintCoatWall(44, 41, 1, false, false);
                MaterialSeamAwait(host, id, step, wire, 1, paint, 1, light, 1, echo, 1);
                WorldGen.paintCoatWall(44, 41, 0, false, false);
                WorldGen.paintTile(44, 41, PaintID.BluePaint, false, false);
                MaterialSeamAwait(host, id, step, wire, 1, paint, 1);
                WorldGen.paintTile(44, 41, PaintID.RedPaint, false, false);
                MaterialSeamAwait(host, id, step, wire, 1);

                // A real same-layer overlay removes only the old wall target.
                WorldGen.paintWall(44, 41, PaintID.BluePaint, false, false);
                var wallCover = Small(library, new CaptureCell { Layers = CaptureLayer.Wall, WallType = WallID.Wood });
                Place(host, input, wallCover, 44, 41, step);
                MaterialSeamAwait(host, id, step, wire, 1);
                Call(host, "ClearProjection");
                placed = Place(host, input, entry, 44, 41, step); id = ((BlueprintProjectionIdentity)Get(placed, "Identity")).ProjectionGeneration;
                var tileCover = Small(library, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
                Place(host, input, tileCover, 44, 41, step);
                var masked = MaterialSeamResult(host, id, step);
                Require(masked.Status == BlueprintMaterialStatus.Unresolved && masked.Rows.All(row => row.ItemType != paint && row.ItemType != light && row.ItemType != echo),
                    "permanently masked furniture is actually unselected foreground, not an invented restoration target");
                WorldGen.paintTile(44, 41, 0, false, false); WorldGen.paintCoatTile(44, 41, 0, false, false);
                MaterialSeamAwait(host, id, step, wire, 1);
                Call(host, "SetMaterialViewDemand", false); WorldGen.paintTile(44, 41, PaintID.RedPaint, false, false);
                for (int i = 0; i < 8; i++) step();
                Call(host, "SetMaterialViewDemand", true);
                NativeQuickItemChecks.Until(() => { step(); return ((BlueprintMaterialSnapshot)Call(host, "MaterialWorld", id)).Status == BlueprintMaterialStatus.Unresolved; });
                WorldGen.paintTile(44, 41, 0, false, false);
                MaterialSeamAwait(host, id, step, wire, 1);
                foreach (int body in new[] { 0, ItemID.GlassChair })
                {
                    Call(host, "ClearProjection"); MaterialFurnitureAt(44, 41, body, PaintID.BluePaint);
                    placed = Place(host, input, entry, 44, 41, step); id = ((BlueprintProjectionIdentity)Get(placed, "Identity")).ProjectionGeneration;
                    MaterialSeamAwait(host, id, step, ItemID.WoodenChair, 1, wire, 1, paint, 2, light, 2, echo, 2);
                }
                Console.WriteLine("PASS material furniture/wall real capture and split Host source: paint/coating restore once; wall/Tile changes, masks, missing/conflicting bodies; final Match ownership retained.");
            }
            finally { Call(host, "ClearProjection"); Call(host, "SetMaterialViewDemand", false); }
        }

        private static object materialPauseWorker;
        private static Guid materialPauseAsset;
        private static ManualResetEventSlim materialIndexEntered, materialIndexContinue;
        private static MaterialIndexStream materialIndexStream;
        private static long materialFallbackReads;
        private static void MaterialIndexOpened(Guid __0, bool __1, ref Stream __result)
        {
            if (__1 || __0 != materialPauseAsset || materialPauseWorker == null || GetOptional(materialPauseWorker, "materialRequest") == null) return;
            materialIndexStream = new MaterialIndexStream(__result, materialIndexEntered, materialIndexContinue); __result = materialIndexStream;
        }
        private static void MaterialFallbackRead(Guid __0, bool __1)
        { if (!__1 && __0 == materialPauseAsset && materialPauseWorker != null && GetOptional(materialPauseWorker, "materialRequest") != null) Interlocked.Increment(ref materialFallbackReads); }
        private static void MaterialProjectionRetirement(object context, object host, object input, LibraryState library, Action step)
        {
            using (EmptyWorld(host, 1024, 160))
            foreach (int remaining in new[] { 0, 1, -1 })
            {
                Call(host, "ClearProjection");
                var entry = MaterialFurnitureAsset(context, host, input, library, step);
                MaterialFurnitureAt(44, 41, ItemID.WoodenChair, 0); Call(host, "SetMaterialViewDemand", true);
                var first = Place(host, input, entry, 44, 41, step);
                if (remaining == 1) Place(host, input, entry, 400, 70, step);
                MaterialSeamResult(host, 0, step);
                NativeQuickItemChecks.Until(() => { step(); return !(bool)Get(host, "ProjectionMatchReading"); });
                WorldGen.PlaceWire(46, 41); step();
                Require(Scene(host).Contains(first), "cold static read starts while the last changed target awaits its real source receipt");
                var worker = Get(library, "worker");
                NativeQuickItemChecks.Until(() => ((ICollection)Get(worker, "projectionSourceReceipts")).Count != 0);
                var catalog = (BlueprintPlacementCatalog)Get(host, "MaterialCatalog");
                var key = new BlueprintMaterialKey(entry.AssetId, false, Guid.Parse(entry.ContentRevision), entry.ContentHash, false, null, catalog.Revision);
                var audit = new Harmony("JueMingR.Tests.MaterialProjectionRetirement");
                materialIndexEntered = new ManualResetEventSlim(); materialIndexContinue = new ManualResetEventSlim();
                materialPauseWorker = worker; materialPauseAsset = entry.AssetId; materialFallbackReads = 0; materialIndexStream = null;
                audit.Patch(AccessTools.Method(Get(worker, "files").GetType(), "OpenIndex"), postfix: new HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks), nameof(MaterialIndexOpened)));
                audit.Patch(AccessTools.Method(worker.GetType(), "ReadChunk"), prefix: new HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks), nameof(MaterialFallbackRead)));
                try
                {
                    Require(library.RequestMaterials(key, catalog), "real cold asset material request admitted");
                    NativeQuickItemChecks.Until(() => materialIndexEntered.IsSet);
                    // The actual source receipt was already queued. World Match
                    // can consume it and retire naturally while this independent
                    // static index read is paused; no Match fields are supplied.
                    step();
                    Require(!Scene(host).Contains(first) && Scene(host).Length == Math.Max(0, remaining),
                        "natural Match retirement does not await an in-progress static material reader");
                    if (remaining < 0) library.CancelMaterials();
                    materialIndexContinue.Set();
                    if (remaining < 0)
                    {
                        NativeQuickItemChecks.Until(() => GetOptional(worker, "materialReading") == null && GetOptional(worker, "materialRequest") == null);
                        Require(library.Materials(key).Status == BlueprintMaterialStatus.Pending, "cancelled static view publishes no late failure or result");
                    }
                    else
                    {
                        BlueprintMaterialSnapshot result = null;
                        NativeQuickItemChecks.Until(() => { result = library.Materials(key); return result.Status != BlueprintMaterialStatus.Pending; });
                        MaterialSeamRows(result, ItemID.WoodenChair, 1, catalog.Wall(WallID.Wood).ConsumeItemType, 2, catalog.WireItemType, 1,
                            catalog.Paint(PaintID.RedPaint), 2, catalog.Coating(1), 2, catalog.Coating(2), 2);
                        Require(remaining == 1 ? materialFallbackReads == 0 : materialFallbackReads > 0 && materialFallbackReads <= 4,
                            "live same-template owner reuses its raw cache; final retirement continues through a bounded independently verified read");
                    }
                    NativeQuickItemChecks.Until(() => materialIndexStream != null && materialIndexStream.Disposed);
                    Console.WriteLine("PASS static material projection-lifetime seam remaining=" + remaining + " fallbackReads=" + materialFallbackReads + " naturalRetired=true indexDisposed=true");
                }
                finally
                {
                    library.CancelMaterials(); materialIndexContinue.Set();
                    NativeQuickItemChecks.Until(() => materialIndexStream == null || materialIndexStream.Disposed);
                    foreach (var method in audit.GetPatchedMethods().ToArray()) audit.Unpatch(method, HarmonyPatchType.All, audit.Id);
                    materialPauseWorker = null; materialPauseAsset = Guid.Empty;
                    materialIndexEntered.Dispose(); materialIndexContinue.Dispose();
                    Call(host, "ClearProjection"); Call(host, "SetMaterialViewDemand", false);
                }
            }
        }
        private sealed class MaterialIndexStream : Stream
        {
            private readonly Stream inner;
            private readonly ManualResetEventSlim entered, proceed;
            private bool paused;
            internal volatile bool Disposed;
            internal MaterialIndexStream(Stream inner, ManualResetEventSlim entered, ManualResetEventSlim proceed)
            { this.inner = inner; this.entered = entered; this.proceed = proceed; }
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (!paused) { paused = true; entered.Set(); if (!proceed.Wait(10000)) throw new TimeoutException("Controlled material index pause was not released."); }
                return inner.Read(buffer, offset, count);
            }
            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => inner.CanSeek;
            public override bool CanWrite => false;
            public override long Length => inner.Length;
            public override long Position { get => inner.Position; set => inner.Position = value; }
            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
            public override void Flush() => inner.Flush();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { try { if (disposing) inner.Dispose(); } finally { Disposed = true; base.Dispose(disposing); } }
        }
    }
}
