using System;
using System.Collections;
using System.IO;
using System.Linq;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintWorldMatchChecks
    {
        // All assets below enter the real capture/seal/save/list flow. Native
        // liquid packets change world facts; no Match/material value is injected.
        private static void LiquidDisplayProgress(object context, object host, object input, LibraryState library,
            Action step, ProbeGraphics graphics, string output)
        {
            bool auto = (bool)Get(host, "AutoBuildEnabled"); var camera = Main.screenPosition;
            using (EmptyWorld(host, 1024, 160)) try
            {
                Call(host, "SetAutoBuild", false); Call(host, "SetMaterialViewDemand", true);
                Require(WorldGen.PlaceTile(60, 50, TileID.Stone, true, true), "captured liquid mixed source Stone");
                WorldGen.PlaceWall(60, 50, WallID.Wood, true);
                WorldGen.PlaceWire(60, 50); WorldGen.PlaceWire2(60, 50); WorldGen.PlaceWire3(60, 50); WorldGen.PlaceWire4(60, 50); WorldGen.PlaceActuator(60, 50);
                NativeLiquidStore(60, 50, 64, 0); NativeLiquidStore(61, 50, 80, 2);
                var mixed = CaptureCompletionAsset(context, host, input, library, step, 60, 50, 2, 1, "液体混合展示");
                var stat = MaterialAwait(host, "MaterialAsset", mixed, step);
                Require(stat.Total == 7 && stat.KindCount == 4 && stat.Rows.Single(r => r.ItemType == ItemID.Wire).Required == 4,
                    "real captured mixed asset has Stone, wall, four wires and actuator only, with exact summaries");

                NativeLiquidStore(200, 70, 64, 0); NativeLiquidStore(201, 70, 80, 2);
                var item = Place(host, input, mixed, 200, 70, step); AwaitReady(item, step);
                Require((long)Get(item, "TotalLayerCount") == 9 && (long)Get(item, "TotalConstructionLayerCount") == 7 && Matches(item) == 0 &&
                    CallResult(host, "ProjectionMoveEligibility", item).ToString() == "Movable",
                    "only liquid matches: all display layers remain, seven construction targets are missing, and movement stays allowed");
                var world = MaterialAwait(host, "MaterialWorld", 0L, step);
                Require(world.Total == 7 && world.KindCount == 4 && world.Rows.All(r => r.InventoryReady),
                    "real all-instance material summary and missing columns remain exact despite target liquid");

                Require(WorldGen.PlaceTile(200, 70, TileID.Stone, true, true), "partial non-liquid building Stone");
                WorldGen.PlaceWall(200, 70, WallID.Wood, true);
                WorldGen.PlaceWire(200, 70); WorldGen.PlaceWire2(200, 70); WorldGen.PlaceWire3(200, 70); WorldGen.PlaceActuator(200, 70);
                NativeQuickItemChecks.Until(() => { step(); return Matches(item) == 6; });
                Require(Scene(host).Contains(item) && CallResult(host, "ProjectionMoveEligibility", item).ToString() == "Matched",
                    "one missing wire still blocks retirement and any non-liquid Match still locks movement");
                // Only the unsupported fourth wire remains missing, so demand
                // can exercise real placement-support observation without an
                // eligible body action changing the independent fixture.
                Call(host, "SetAutoBuild", true); step();
                var range = ((IEnumerable)Get(Get(item, "Match"), "Ranges")).Cast<object>().Single();
                long version = (long)Get(range, "Version"), support = LiquidSupportStamp(host, range);
                for (int i = 0; i < 16; i++) { NativeLiquidStore(200, 70, (byte)(100 + i), (byte)(i % 3)); NativeLiquidStore(201, 70, (byte)(150 + i), (byte)((i + 1) % 3)); step(); }
                Require((long)Get(range, "Version") == version && Matches(item) == 6 && LiquidSupportStamp(host, range) > support,
                    "target liquid churn preserves the non-liquid proof while independent BuildSupport still observes real liquid qualification changes");
                Call(host, "SetAutoBuild", false);
                WorldGen.PlaceWire4(200, 70);
                NativeQuickItemChecks.Until(() => { step(); return !Scene(host).Contains(item); });
                Require(Main.tile[200, 70].liquid != 64 && Main.tile[201, 70].liquid != 80,
                    "last non-liquid target naturally retires the mixed projection while world liquid still differs");
                var initiallyComplete = Place(host, input, mixed, 200, 70, step);
                NativeQuickItemChecks.Until(() => { step(); return !Scene(host).Contains(initiallyComplete); });
                RequireListedAsset(library, mixed.AssetId, "completion retains the captured liquid template for a new independent instance");

                NativeLiquidStore(65, 50, 112, 1);
                var pure = CaptureCompletionAsset(context, host, input, library, step, 65, 50, 1, 1, "纯液体展示");
                var pureStatic = MaterialAwait(host, "MaterialAsset", pure, step);
                Require(pureStatic.Total == 0 && pureStatic.Rows.Count == 0, "real pure-liquid capture saves and derives exact static zero");
                var display = Place(host, input, pure, 400, 70, step); AwaitReady(display, step);
                for (int i = 0; i < 12; i++) step();
                long displayId = ((BlueprintProjectionIdentity)Get(display, "Identity")).ProjectionGeneration;
                var zero = MaterialAwait(host, "MaterialWorld", displayId, step);
                Require(Scene(host).Contains(display) && (long)Get(display, "TotalConstructionLayerCount") == 0 && zero.Total == 0 && zero.Rows.Count == 0 &&
                    CallResult(host, "ProjectionMoveEligibility", display).ToString() == "Movable",
                    "zero construction targets remain a persistent movable display with exact single-instance materials");
                NativeQuickItemChecks.Until(() => { Call(host, "UpdateProjectionWindow", new JueMingR.Platform.WorldTargets.WorldTargetView(390, 60, 64, 24, 0)); step(); return GetOptional(display, "Window") != null && (int)Get(Get(display, "Renderer"), "CommandCount") > 0; });
                AssertCommand(display, 4, true, false);
                if (graphics != null)
                {
                    Main.screenPosition = new Vector2(400 * 16 - 480, 70 * 16 - 320);
                    Require(Pixels(context, host, graphics, output, "blueprint-liquid-display-only").Any(c => c.A != 0),
                        "actual WorldLayer.Draw retains nonempty liquid pixels after its zero-target Match proof");
                }
                StartSelection(host, input, false); Pointer(host, false, 400, 70); Pointer(host, true, 400, 70);
                Require(Mode(host) == "Moving", "display-only liquid remains selectable through the real move input");
                Pointer(host, false, 420, 70); for (int i = 0; i < 3; i++) step(); Pointer(host, true, 420, 70);
                NativeQuickItemChecks.Until(() => { step(); return Mode(host) == "Fixed"; });
                display = Scene(host).Single(); AwaitReady(display, step);
                Require((int)Get(display, "OriginX") == 420 && Matches(display) == 0, "display-only move retains the reliable zero-target restore/commit identity");
                NativeQuickItemChecks.Until(() => { Call(host, "UpdateProjectionWindow", new JueMingR.Platform.WorldTargets.WorldTargetView(390, 60, 64, 24, 0)); step(); return GetOptional(display, "Window") != null; });
                StartSelection(host, input, true); Pointer(host, false, 420, 70); Pointer(host, true, 420, 70);
                Require(Scene(host).Length == 0, "display-only liquid still supports real manual removal");

                Require(WorldGen.PlaceTile(68, 50, TileID.Stone, true, true), "captured Tile plus liquid source"); NativeLiquidStore(68, 50, 76, 2);
                var tileLiquid = CaptureCompletionAsset(context, host, input, library, step, 68, 50, 1, 1, "最后施工层");
                var remaining = Place(host, input, tileLiquid, 500, 70, step); AwaitReady(remaining, step);
                var cover = Place(host, input, Small(library, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 500, 70, step);
                NativeQuickItemChecks.Until(() => { step(); return ((BlueprintProjectionMask)Get(remaining, "Mask")).RemovedLayerCount == 1; });
                var mask = (BlueprintProjectionMask)Get(remaining, "Mask");
                Require(mask.RemovedConstructionLayerCount == 1 && Scene(host).Contains(remaining), "permanent Tile overlap leaves a zero-construction liquid display");
                Require(WorldGen.PlaceTile(500, 70, TileID.Stone, true, true), "cover completes independently");
                NativeQuickItemChecks.Until(() => { step(); return !Scene(host).Contains(cover); }); AwaitReady(remaining, step);
                Require(CallResult(host, "ProjectionMoveEligibility", remaining).ToString() == "Movable" && MaterialAwait(host, "MaterialWorld", 0L, step).Total == 0,
                    "mask and world completion share the same remaining non-liquid denominator");
                long retirements = (long)Get(host, "ProjectionMatchRetirements");
                Place(host, input, pure, 500, 70, step);
                NativeQuickItemChecks.Until(() => { step(); return !Scene(host).Contains(remaining); });
                Require((long)Get(host, "ProjectionMatchRetirements") == retirements && Scene(host).Length == 1,
                    "masking the final liquid display uses empty-instance retirement and retains the new pure-liquid instance");
                Call(host, "ClearProjection");

                Main.netMode = 1; Main.sectionManager = new WorldSections(6, 2);
                long reads = (long)Get(host, "ProjectionMatchCellReads");
                var unseenLiquid = Place(host, input, pure, 700, 70, step); AwaitReady(unseenLiquid, step);
                Require((long)Get(host, "ProjectionMatchCellReads") == reads && Scene(host).Contains(unseenLiquid) &&
                    CallResult(host, "ProjectionMoveEligibility", unseenLiquid).ToString() == "Movable" && MaterialAwait(host, "MaterialWorld", 0L, step).Total == 0,
                    "pure-liquid targets in an unreceived native section need no world reads and cannot manufacture Unknown");
                var unread = Place(host, input, tileLiquid, 800, 70, step);
                NativeQuickItemChecks.Until(() => { step(); return GetOptional(unread, "Match") != null && (long)Get(Get(unread, "Match"), "Unknown") == 1; });
                Require(Scene(host).Contains(unread) && CallResult(host, "ProjectionMoveEligibility", unread).ToString() == "Checking" &&
                    ((BlueprintMaterialSnapshot)Call(host, "MaterialWorld", 0L)).Status != BlueprintMaterialStatus.Exact,
                    "unreceived non-liquid targets retain Checking and unresolved materials rather than borrowing the liquid-only zero");
                Console.WriteLine("PASS Blueprint liquid: actual capture/save/static/world materials; seven non-liquid targets; partial wire and move lock; native target-liquid churn preserves Match and updates BuildSupport; initial/last-target retirement; pure-liquid Draw/move/remove; last-target mask and empty retirement; unreceived-section zero versus non-liquid Unknown.");
            }
            finally { Main.screenPosition = camera; Call(host, "SetMaterialViewDemand", false); Call(host, "SetAutoBuild", auto); }
        }

        private static long LiquidSupportStamp(object host, object range)
        {
            var changes = Get(host, "projectionChanges"); object[] values = { (long)Get(range, "Watch"), (int)Get(range, "PartIndex"), 0L };
            Require((bool)changes.GetType().GetMethod("TryGetBuildStamp", Flags).Invoke(changes, values), "real liquid BuildSupport stamp is current");
            return (long)values[2];
        }
        private static void NativeLiquidStore(int x, int y, byte amount, byte kind)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) { writer.Write((ushort)1); writer.Write((x << 16) | y); writer.Write(amount); writer.Write(kind); }
                bytes.Position = 0; using (var reader = new BinaryReader(bytes)) new Terraria.GameContent.NetModules.NetLiquidModule().Deserialize(reader, 256);
            }
        }
    }
}
