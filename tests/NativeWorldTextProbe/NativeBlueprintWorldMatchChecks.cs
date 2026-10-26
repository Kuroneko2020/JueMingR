using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Test setup owns a finite disposable Tile world. Only original mutations
    // trigger invalidation; no match/completion bit is injected into the Host.
    internal static partial class NativeBlueprintWorldMatchChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run(object context, ProbeGraphics graphics = null, string output = null)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var library = (LibraryState)Get(host, "Library");
            var oldTiles = Main.tile; var oldSections = Main.sectionManager; var oldMap = Main.Map; int width = Main.maxTilesX, height = Main.maxTilesY, netMode = Main.netMode;
            var camera = Main.screenPosition; int screenWidth = Main.screenWidth, screenHeight = Main.screenHeight; var zoom = Main.GameViewMatrix.Zoom; float ui = Main.UIScale, gravity = Main.LocalPlayer.gravDir;
            var absentPlayers = Enumerable.Range(0, Main.player.Length).Where(i => Main.player[i] == null).ToArray();
            Microsoft.Xna.Framework.Color[] ordinaryPixels = null, hiddenPixels = null;
            Call(Get(context, "Shell"), "CloseAndSubmitPosition"); Call(host, "CancelCaptureOrDraft"); Call(host, "ClearProjection");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            ((BlueprintCaptureState)Get(host, "State")).Exit();
            var originalLibrary = library; var libraryField = host.GetType().GetField("<Library>k__BackingField", Flags);
            var ownLibrary = new LibraryState(new JueMingR.Infrastructure.Blueprint.FileBlueprintLibrary(System.IO.Path.Combine(Terraria.Program.SavePath, "world-match")));
            libraryField.SetValue(host, ownLibrary); library = ownLibrary;
            try
            {
                Main.netMode = 0; Main.maxTilesX = 1024; Main.maxTilesY = 160; Main.tile = new Tile[1024, 160];
                for (int x = 0; x < 1024; x++) for (int y = 0; y < 160; y++) Main.tile[x, y] = new Tile();
                Main.Map = new Terraria.Map.WorldMap(1024, 160);
                InitializeMetadata(); Framing.Initialize(); Main.SetupTileMerge();
                // SlopeTile's real collision adjustment visits every player
                // slot, as normal Main initialization guarantees non-null.
                foreach (int i in absentPlayers) Main.player[i] = new Player();
                if (graphics != null)
                {
                    graphics.LoadTexture("Tile", "Images/Tiles_1", 1); graphics.LoadTexture("Wall", "Images/Wall_7", 7);
                    Main.screenWidth = 960; Main.screenHeight = 640; Main.screenPosition = new Microsoft.Xna.Framework.Vector2(520, 520);
                    Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 960, 640)); Main.GameViewMatrix.Zoom = Microsoft.Xna.Framework.Vector2.One; Main.UIScale = 1; Main.LocalPlayer.gravDir = 1;
                }
                var summary = new CaptureSummary { Width = 260, Height = 1, ContentCount = 2 };
                var target = new CaptureCell { X = 0, Y = 0, Layers = CaptureLayer.Tile | CaptureLayer.Wall | CaptureLayer.Wire | CaptureLayer.Actuator, TileType = 1, WallType = 7, Wires = 1, Actuator = true };
                var asset = new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "match-fixture", 1, 1), 1, summary,
                    new BlueprintProjectionBounds(0, 0, 260, 1), new[] { target, new CaptureCell { X = 259, Y = 0, Layers = CaptureLayer.Tile, TileType = 1 } }, Array.Empty<CaptureObject>());
                var entry = NativeBlueprintProjectionVisualChecks.Save(library, asset);
                // This scope executes selected original entries rather than
                // Main.DoUpdate. Advance its real entry counter once per step;
                // Poll and Draw deliberately do not provide another allowance.
                int maxStepReads = 0;
                Action step = () => { long before = (long)Get(host, "ProjectionMatchCellReads"); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); int used = checked((int)((long)Get(host, "ProjectionMatchCellReads") - before)); maxStepReads = Math.Max(maxStepReads, used); Require(used <= 256, "one original update entry has at most 256 actual match reads"); };
                LayerChurn(host, input, library, step);
                OperatingCompletion(host, input, library, step);
                OperatingCompletion(host, input, library, step, true);
                OperatingCompletion(host, input, library, step, true, 32);
                SparseConvergence(host, input, library, step);
                Action visible = () => { Call(host, "UpdateProjectionWindow", new WorldTargetView(20, 20, 32, 16, 0)); step(); };
                long initialReads = (long)Get(host, "ProjectionMatchCellReads"), initialBlocks = library.DebugProjectionStatistics.BlockReads;
                var first = Place(host, input, entry, 40, 40, step); var second = Place(host, input, entry, 400, 40, step);
                AwaitReady(first, step); AwaitReady(second, step);
                NativeQuickItemChecks.Until(() => { visible(); return GetOptional(first, "Window") != null; });
                AwaitSettled(host, visible);
                long coverageReads = (long)Get(host, "ProjectionMatchCellReads") - initialReads, coverageBlocks = library.DebugProjectionStatistics.BlockReads - initialBlocks;
                Require(Matches(first) == 0 && Scene(host).Length == 2, "empty layers remain ordinary; full sparse source coverage is independent of viewport");
                if (graphics != null) ordinaryPixels = Pixels(context, host, graphics, output, "blueprint-match-ordinary");
                long idleReads = (long)Get(host, "ProjectionMatchCellReads"), idleBlocks = library.DebugProjectionStatistics.BlockReads;
                for (int i = 0; i < 12; i++) { visible(); Call(host, "Poll"); }
                Require((long)Get(host, "ProjectionMatchCellReads") == idleReads && library.DebugProjectionStatistics.BlockReads == idleBlocks,
                    "stable scene and Poll plus Update do not restart world/body scanning");

                long localReads = (long)Get(host, "ProjectionMatchCellReads"), localBlocks = library.DebugProjectionStatistics.BlockReads, segmentBuilds = (long)Get(Get(first, "Renderer"), "SegmentBuildCount");
                Require(WorldGen.PlaceTile(40, 40, 1, true, true), "original solid placement succeeds in disposable world");
                WorldGen.PlaceWire2(40, 40); // An extra non-target layer is legal.
                NativeQuickItemChecks.Until(() => { visible(); return Matches(first) == 1; });
                localReads = (long)Get(host, "ProjectionMatchCellReads") - localReads; localBlocks = library.DebugProjectionStatistics.BlockReads - localBlocks;
                Require((long)Get(Get(first, "Renderer"), "SegmentBuildCount") == segmentBuilds, "world match publication changes hide/tint without rebuilding source geometry");
                Require(Result(first, 0, 0).MatchBits == 1 && Result(first, 0, 0).MissingBits == (2 | 8 | 16), "same-cell tile Match hides only Tile; wall/red wire/actuator remain ordinary and extra blue wire is transparent");
                AssertCommand(first, 1, false, false); AssertCommand(first, 2, true, false);
                if (graphics != null) { hiddenPixels = Pixels(context, host, graphics, output, "blueprint-match-layer-hidden"); Require(!ordinaryPixels.SequenceEqual(hiddenPixels) && hiddenPixels.Any(c => c.A != 0), "real Draw hides matched Tile while independent layers remain pixels"); }
                Require(WorldGen.SlopeTile(40, 40, 1, true), "actual shape mutation admitted");
                Require(Main.tile[40, 40].slope() == 1, "actual original shape remains sloped after native framing");
                NativeQuickItemChecks.Until(() => { visible(); return (Result(first, 0, 0).ConflictBits & 1) != 0; });
                AssertCommand(first, 1, true, true);
                if (graphics != null) { var redPixels = Pixels(context, host, graphics, output, "blueprint-match-conflict-red"); Require(!hiddenPixels.SequenceEqual(redPixels) && redPixels.Count(c => c.R > c.G * 2 && c.R > 100) > hiddenPixels.Count(c => c.R > c.G * 2 && c.R > 100), "actual conflict tint adds red Tile pixels through production WorldLayer.Draw"); }
                Require(WorldGen.SlopeTile(40, 40, 0, true), "shape restored through original consumer");
                NativeQuickItemChecks.Until(() => { visible(); return (Result(first, 0, 0).MatchBits & 1) != 0; });
                WorldGen.KillTile(40, 40, false, false, true);
                NativeQuickItemChecks.Until(() => { visible(); return Matches(first) == 0 && Result(first, 0, 0).ConflictBits == 0; });
                AssertCommand(first, 1, true, false);
                if (graphics != null) Require(ordinaryPixels.SequenceEqual(Pixels(context, host, graphics, output, "blueprint-match-restored")), "demolition restores exact ordinary projection pixels before complete retirement");

                long offscreenReads = (long)Get(host, "ProjectionMatchCellReads");
                Require(WorldGen.PlaceTile(299, 40, 1, true, true), "offscreen sentinel placement");
                NativeQuickItemChecks.Until(() => { step(); return Matches(first) == 1; });
                offscreenReads = (long)Get(host, "ProjectionMatchCellReads") - offscreenReads;
                long queryReads = (long)Get(host, "ProjectionMatchCellReads");
                Require(CallResult(host, "ProjectionMoveEligibility", first).ToString() == "Matched", "offscreen Match locks the correct instance");
                Require(CallResult(host, "ProjectionMoveEligibility", second).ToString() == "Movable", "same template at another origin has independent zero-Match qualification");
                Require((long)Get(host, "ProjectionMatchCellReads") == queryReads, "current movement qualification consumes the same ledger without another scan");
                StartSelection(host, input, false); Pointer(host, false, 40, 40); Pointer(host, true, 40, 40);
                Require(Mode(host) == "SelectingMove" && GetOptional(host, "ProjectionCandidate") == null, "trusted move click cannot seize a locked instance or select through it");
                Call(host, "CancelProjection");
                NativeQuickItemChecks.Until(() => { Call(host, "UpdateProjectionWindow", new WorldTargetView(390, 30, 48, 24, 0)); step(); return GetOptional(second, "Window") != null; });
                StartSelection(host, input, false); Pointer(host, false, 400, 40); Pointer(host, true, 400, 40);
                Require(Mode(host) == "Moving", "another fully checked instance remains movable");
                Pointer(host, false, 420, 40);
                Require(WorldGen.PlaceTile(400, 40, 1, true, true), "original restore location gains Match while dragging");
                NativeQuickItemChecks.Until(() => { step(); return Matches(second) == 1; });
                Pointer(host, true, 420, 40);
                Require(Mode(host) == "Fixed" && Scene(host).Any(p => ReferenceEquals(p, second)) && (int)Get(second, "OriginX") == 400,
                    "final move rechecks original location and cancels to exact restore, independent of candidate destination");

                NativeQuickItemChecks.Until(() => { visible(); return GetOptional(first, "Window") != null; });
                StartSelection(host, input, true); Pointer(host, false, 40, 40); Pointer(host, true, 40, 40);
                Require(Scene(host).Length == 1 && ReferenceEquals(Scene(host)[0], second), "Remove stays selectable for a partially hidden/locked instance and retires only that reference");

                // Fresh same-template instance starts a fresh proof. Partial
                // Match is reversible until the last target becomes current.
                first = Place(host, input, entry, 40, 40, step); AwaitReady(first, step);
                Require(Scene(host).Length == 2, "Following and confirmed incomplete instances retain independent life");
                long completionReads = (long)Get(host, "ProjectionMatchCellReads");
                Require(WorldGen.PlaceTile(40, 40, 1, true, true), "last local solid setup");
                WorldGen.PlaceWall(40, 40, 7, true); WorldGen.PlaceWire(40, 40); WorldGen.PlaceActuator(40, 40);
                string worldBefore = Snapshot(40, 40, 1, 1) + Snapshot(299, 40, 1, 1); int inventory = Main.LocalPlayer.inventory.Sum(i => i == null ? 0 : i.stack);
                NativeQuickItemChecks.Until(() => { visible(); return !Scene(host).Any(p => ReferenceEquals(p, first)); });
                completionReads = (long)Get(host, "ProjectionMatchCellReads") - completionReads;
                Require(Scene(host).Length == 1 && ReferenceEquals(Scene(host)[0], second), "full current proof retires exactly one instance and keeps the other; remaining="+Scene(host).Length);
                RequireListedAsset(library,entry.AssetId,"automatic retirement keeps its actual library template");
                Require((int)Get(Get(first, "Renderer"), "CommandCount") == 0 && GetOptional(first, "Match") == null && GetOptional(first, "SourceRanges") == null && GetOptional(first,"MatchRanges")==null && GetOptional(first, "Window") == null && ((IDictionary)Get(first, "Windows")).Count == 0,
                    "completed instance actually releases commands/matcher/watch, DTOs and shared-index references even while this test retains its old identity object");
                Require(worldBefore == Snapshot(40, 40, 1, 1) + Snapshot(299, 40, 1, 1) && Main.LocalPlayer.inventory.Sum(i => i == null ? 0 : i.stack) == inventory,
                    "product observation/retirement leaves finite world and inventory/materials unchanged");
                WorldGen.KillTile(40, 40, false, false, true); WorldGen.KillTile(299, 40, false, false, true);
                for (int i = 0; i < 12; i++) step();
                Require(!Scene(host).Any(p => ((BlueprintProjectionIdentity)Get(p, "Identity")).ProjectionGeneration == ((BlueprintProjectionIdentity)Get(first, "Identity")).ProjectionGeneration), "later demolition cannot resurrect automatically retired identity");
                Call(host, "ClearProjection");
                long stopped = (long)Get(host, "ProjectionMatchCellReads"); for (int i = 0; i < 8; i++) step();
                Require((int)Get(host, "ProjectionMatchWatchCount") == 0 && !(bool)Get(host, "ProjectionMatchReading") && stopped == (long)Get(host, "ProjectionMatchCellReads"), "last scene retirement leaves no matcher scans, watches or source request");
                ObjectProof(context, host, input, library, step);
                ObjectInFlight(context, host, input, library, step);
                DoorSeedInFlight(host,input,library,step);
                OpenDoorDependencies(host,input,library,step);
                AnimatedCompletion(host, input, library, step);
                UnknownAndCandidate(context, host, input, library, step);
                SlicedProofAndCommit(context, host, input, library, step);
                OperatingCompletion(host, input, library, step, true, 8, true);
                OperatingCompletion(host, input, library, step, true, 8, true, true);
                CompletionProgress(context, host, input, library, step);
                Console.WriteLine("BlueprintWorldMatch costs targets=5 sourceRecords=2 bboxWidth=260 instances=2 firstCoverageWorldReads=" + coverageReads + " firstCoverageBodyBlocks=" + coverageBlocks + " stable12UpdatesReads=0 localWorldReads=" + localReads + " localBodyBlocks=" + localBlocks + " localSegmentBuilds=0 offscreenWorldReads=" + offscreenReads + " currentMoveQueryReads=0 completionWorldReads=" + completionReads + " maxActualReadsPerUpdate=" + maxStepReads + "; bounded physical-source/complete-object workset; not FPS evidence");
                Console.WriteLine("PASS BlueprintWorldMatch real Host/source/Tile consumers: per-layer ordinary/hide/red/recovery, stable zero scans, offscreen locking/remove, same-template independence, moving restore recheck, complete retirement/no resurrection, world/inventory read-only and final cleanup. Ordinary game/FPS/multiplayer NOT_RUN.");
            }
            finally
            {
                Call(host, "ClearProjection"); Main.tile = oldTiles; Main.sectionManager = oldSections; Main.Map = oldMap; Main.maxTilesX = width; Main.maxTilesY = height; Main.netMode = netMode;
                libraryField.SetValue(host, originalLibrary); ownLibrary.Dispose();
                foreach (int i in absentPlayers) Main.player[i] = null;
                if (graphics != null) { Main.screenPosition = camera; Main.screenWidth = screenWidth; Main.screenHeight = screenHeight; Main.GameViewMatrix.Zoom = zoom; Main.UIScale = ui; Main.LocalPlayer.gravDir = gravity; }
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            }
        }
        internal static void InitializeMetadata()
        {
            // Original Initialize locks its table at the end; later Blueprint
            // consumers reuse that same startup owner instead of unlocking or
            // replaying initialization. This getter only dereferences _data.
            bool notStarted = false; Terraria.ObjectData.TileObjectData door = null;
            try { door = Terraria.ObjectData.TileObjectData.GetTileData(10, 0); }
            catch (NullReferenceException) { notStarted = true; }
            if (notStarted)
            {
                typeof(Main).GetMethod("Initialize_TileAndNPCData1", Flags).Invoke(null, null);
                typeof(Main).GetMethod("Initialize_TileAndNPCData2", Flags).Invoke(null, null);
                Terraria.ObjectData.TileObjectData.Initialize(); door = Terraria.ObjectData.TileObjectData.GetTileData(10, 0);
            }
            Require(door != null && door.Width == 1 && door.Height == 3 && Main.tileFrameImportant[10] && Main.tileFrameImportant[93] && Main.tileFrameImportant[658],
                "Blueprint fixture reuses complete original startup metadata and both tile-property initializers");
        }
        internal static void RunOriginal(object context, Action<int, int, bool> nativeSample, Action draw,
            Func<Microsoft.Xna.Framework.Graphics.RenderTarget2D> canvas, string output)
        {
            var host = Get(context, "Blueprint"); var library = (LibraryState)Get(host, "Library");
            const int x = 410, y = 210, otherX = 434, left = 408, top = 208, width = 42, height = 7;
            var saved = new Tile[width, height]; Call(host, "ClearProjection");
            Action<int, int, bool> point = (wx, wy, down) =>
            {
                var screen = Microsoft.Xna.Framework.Vector2.Transform(Main.ReverseGravitySupport(new Microsoft.Xna.Framework.Vector2(wx * 16 + 8, wy * 16 + 8) - Main.screenPosition), Main.GameViewMatrix.ZoomMatrix);
                nativeSample((int)screen.X, (int)screen.Y, down);
            };
            Action step = () => point(x + 6, y, false);
            Action<string> image = name => { draw(); using (var stream = System.IO.File.Create(System.IO.Path.Combine(output, name + ".png"))) canvas().SaveAsPng(stream, Main.screenWidth, Main.screenHeight); };
            try
            {
                for (int dy = 0; dy < height; dy++) for (int dx = 0; dx < width; dx++)
                { saved[dx, dy] = Main.tile[left + dx, top + dy]; Main.tile[left + dx, top + dy] = new Tile(); }
                var entry = Small(library, new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Wall, TileType = 1, TilePaint = Terraria.ID.PaintID.DeepGreenPaint, WallType = 7 }, new CaptureCell { X = 12, Layers = CaptureLayer.Wire, Wires = 8 });
                Func<int, object> place = origin =>
                {
                    point(origin + 6, y, false); point(origin + 6, y, false);
                    Require((bool)Call(host, "Place", entry), "initialized native Place uses the mapped current input");
                    NativeQuickItemChecks.Until(() => { point(origin + 6, y, false); return Mode(host) == "Following"; });
                    var candidate = Get(host, "ProjectionCandidate"); point(origin + 6, y, false); point(origin + 6, y, false); point(origin + 6, y, true); point(origin + 6, y, true);
                    NativeQuickItemChecks.Until(() => { step(); return Mode(host) == "Fixed"; });
                    Require(Scene(host).Contains(candidate) && (int)Get(candidate, "OriginX") == origin && (int)Get(candidate, "OriginY") == y,
                        "actual native certificate confirms the exact finite-world origin"); return candidate;
                };
                var first = place(x); var second = place(otherX); AwaitReady(first, step); AwaitReady(second, step);
                NativeQuickItemChecks.Until(() => { step(); return Result(first, 0, 0).TargetBits == 3; });
                image("blueprint-original-match-missing");
                Require(WorldGen.PlaceTile(x, y, 1, true, true), "initialized original finite Tile construction");
                WorldGen.paintTile(x, y, Terraria.ID.PaintID.DeepGreenPaint, false, false);
                NativeQuickItemChecks.Until(() => { step(); return Result(first, 0, 0).MatchBits == 1; });
                AssertCommand(first, 1, false, false); AssertCommand(first, 2, true, false); image("blueprint-original-match-partial");
                step(); Call(host, "MoveProjection"); point(x, y, false); point(x, y, false); point(x, y, true); point(x, y, true);
                Require(Mode(host) == "SelectingMove" && GetOptional(host, "ProjectionCandidate") == null && ReferenceEquals(GetOptional(host, "ProjectionHover"), first) && Get(host, "ProjectionHoverMoveEligibility").ToString() == "Matched",
                    "real native Move click shares the visible matched qualification and cannot seize the built instance");
                Call(host, "CancelProjection"); step(); Call(host, "RemoveProjection"); point(x, y, false); point(x, y, false); point(x, y, true); point(x, y, true);
                Require(!Scene(host).Contains(first) && Scene(host).Contains(second), "real native Remove click still selects the partially hidden matched instance");
                first = place(x); AwaitReady(first, step); NativeQuickItemChecks.Until(() => { step(); return Result(first, 0, 0).MatchBits == 1; });
                WorldGen.KillTile(x, y, false, false, true); NativeQuickItemChecks.Until(() => { step(); return Result(first, 0, 0).MatchBits == 0; });
                AssertCommand(first, 1, true, false); image("blueprint-original-match-restored");
                Require(WorldGen.PlaceTile(x, y, 1, true, true), "initialized original rebuild"); WorldGen.paintTile(x, y, Terraria.ID.PaintID.DeepGreenPaint, false, false); WorldGen.SlopeTile(x, y, 1);
                NativeQuickItemChecks.Until(() => { step(); return Result(first, 0, 0).ConflictBits == 1; });
                NativeQuickItemChecks.Until(() => { step(); return Main.instance.TilePaintSystem.TryGetTileAndRequestIfNotReady(1,0,Terraria.ID.PaintID.DeepGreenPaint) != null; });
                AssertCommand(first, 1, true, true); image("blueprint-original-match-conflict");
                var layerPixels = OriginalLayerPixels(context, host);
                var corner = Microsoft.Xna.Framework.Vector2.Transform(new Microsoft.Xna.Framework.Vector2(x * 16, y * 16) - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
                int red = 0;
                for (int py = (int)corner.Y; py < (int)corner.Y + 16; py++) for (int px = (int)corner.X; px < (int)corner.X + 16; px++)
                { var color = layerPixels[py * Main.screenWidth + px]; if (color.R > color.G * 2 && color.R > color.B * 2 && color.R > 40) red++; }
                Console.WriteLine("BlueprintWorldMatch initialized deep-green target conflict actual red pixels=" + red);
                Require(red >= 8, "conflicting deeply painted Tile still produces visible red pixels through the real original paint texture and WorldLayer.Draw");
                WorldGen.SlopeTile(x, y, 0); WorldGen.PlaceWall(x, y, 7, true);
                NativeQuickItemChecks.Until(() => { step(); return Result(first, 0, 0).MatchBits == 3; });
                WorldGen.PlaceWire4(x + 12, y); string before = Snapshot(left, top, width, height); int materials = Main.LocalPlayer.inventory.Sum(i => i == null ? 0 : i.stack);
                NativeQuickItemChecks.Until(() => { step(); return !Scene(host).Contains(first); });
                Require(Scene(host).Length == 1 && ReferenceEquals(Scene(host)[0], second) && before == Snapshot(left, top, width, height) && materials == Main.LocalPlayer.inventory.Sum(i => i == null ? 0 : i.stack),
                    "initialized Main/input/Host/Draw completion retires only the current instance without changing world or materials");
                image("blueprint-original-match-completed"); WorldGen.KillTile(x, y, false, false, true); for (int n = 0; n < 5; n++) step();
                Require(!Scene(host).Contains(first), "initialized native demolition cannot resurrect a retired instance");
                RequireListedAsset(library,entry.AssetId,"initialized native retirement keeps its actual library template");
                Call(host, "ClearProjection");
                Require((int)Get(host, "ProjectionMatchWatchCount") == 0, "initialized original last-instance cleanup releases its matcher subscriptions");
                Console.WriteLine("PASS BlueprintWorldMatch initialized original Main/XNB/input/UI/Host/Draw: native confirmation, missing/partial-layer hide/restore/conflict, matched Move denial with independent Remove, current full retirement/no resurrection, template/twin/materials preserved. Controlled specialist with simplified Update; ordinary load/OS/multiplayer/FPS NOT_RUN.");
            }
            finally
            {
                Call(host, "ClearProjection");
                for (int dy = 0; dy < height; dy++) for (int dx = 0; dx < width; dx++) if (saved[dx, dy] != null) Main.tile[left + dx, top + dy] = saved[dx, dy];
            }
        }
        private static Microsoft.Xna.Framework.Color[] OriginalLayerPixels(object context, object host)
        {
            var device = Main.instance.GraphicsDevice; var previous = device.GetRenderTargets();
            using (var target = new Microsoft.Xna.Framework.Graphics.RenderTarget2D(device, Main.screenWidth, Main.screenHeight))
            {
                try
                {
                    device.SetRenderTarget(target); device.Clear(Microsoft.Xna.Framework.Color.Transparent); Call(Get(context,"worldTiles"),"BeginTick");
                    Main.spriteBatch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred, Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend, Microsoft.Xna.Framework.Graphics.SamplerState.PointClamp,
                        Microsoft.Xna.Framework.Graphics.DepthStencilState.None, Microsoft.Xna.Framework.Graphics.RasterizerState.CullNone, null, Main.GameViewMatrix.ZoomMatrix);
                    try { Call(Get(host,"World"),"Draw"); } finally { Main.spriteBatch.End(); }
                    device.SetRenderTarget(null); var pixels = new Microsoft.Xna.Framework.Color[target.Width * target.Height]; target.GetData(pixels); return pixels;
                }
                finally { device.SetRenderTargets(previous); }
            }
        }
        private static void LayerChurn(object host, object input, LibraryState library, Action step)
        {
            const int count = 4097, sourceWidth = 1000, oy = 110;
            var cells = Enumerable.Range(0, count).Select(i => new CaptureCell { X = i % sourceWidth, Y = i / sourceWidth, Layers = CaptureLayer.Wall, WallType = 7 }).ToArray();
            foreach (var cell in cells) Main.tile[cell.X, oy + cell.Y].wall = 7;
            cells[900].Layers |= CaptureLayer.Tile; cells[900].TileType = 1; Main.tile[900, oy].active(true); Main.tile[900, oy].type = 1;
            cells[901].Layers |= CaptureLayer.Wire; cells[901].Wires = 1; Main.tile[901, oy].wire(true);
            cells[902].Layers |= CaptureLayer.Liquid; cells[902].LiquidAmount = 10; Main.tile[902, oy].liquid = 10;
            Main.tile[96, oy + 4].wall = 0; // Keep the old instance incomplete while its mask is committed.
            var entry = NativeBlueprintBudgetChecks.Save(library, cells, sourceWidth, 5);
            var placed = Place(host, input, entry, 0, oy, step);
            AwaitReady(placed, step);
            var cover = Small(library, new CaptureCell { Layers = CaptureLayer.Tile, TileType = 1 });
            Place(host, input, cover, 900, oy, step);
            NativeQuickItemChecks.Until(() => { step(); return Scene(host).Length == 1 && ReferenceEquals(Scene(host)[0], placed); });
            Require(((BlueprintProjectionMask)Get(placed, "Mask")).RemovedLayerCount == 1, "real cover removes old Tile target while keeping its wall");
            AwaitReady(placed, step); WorldGen.PlaceWall(96, oy + 4, 7, true); Require(Main.tile[96, oy + 4].wall == 7, "last required wall changed through native placement");
            long invalidations = (long)Get(host, "ProjectionMatchInvalidations"), reads = (long)Get(host, "ProjectionMatchCellReads");
            int changes = 0;
            for (; changes < 200 && Scene(host).Contains(placed); changes++)
            {
                if ((changes & 1) == 0) { WorldGen.PlaceWire(500, oy + 1); WorldGen.PlaceTile(500, oy + 1, 1, true, true); WorldGen.PlaceTile(900, oy, 1, true, true); }
                else { WorldGen.KillWire(500, oy + 1); WorldGen.KillTile(500, oy + 1, false, false, true); WorldGen.KillTile(900, oy, false, false, true); }
                using (var bytes = new System.IO.MemoryStream())
                {
                    using (var writer = new System.IO.BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) { writer.Write((ushort)1); writer.Write((500 << 16) | (oy + 1)); writer.Write((byte)(40 + (changes & 1))); writer.Write((byte)0); }
                    bytes.Position = 0; using (var reader = new System.IO.BinaryReader(bytes)) new Terraria.GameContent.NetModules.NetLiquidModule().Deserialize(reader, 256);
                }
                step(); System.Threading.Thread.Sleep(2);
            }
            Require(!Scene(host).Contains(placed), "4097 mixed targets must finish bounded multi-tick verification while extra Tile/wire/liquid at a wall-only point and permanently erased Tile change every update; changes=" + changes + " invalidations=" + ((long)Get(host, "ProjectionMatchInvalidations") - invalidations) + " worldReads=" + ((long)Get(host, "ProjectionMatchCellReads") - reads));
            Require((long)Get(host, "ProjectionMatchInvalidations") - invalidations == 1, "only the final required wall revokes its own range; non-target/masked layer writes never revoke proof");
            Call(host, "ClearProjection");
            Console.WriteLine("PASS BlueprintWorldMatch layer churn records=4097 ranges=3 physicalSourceWorksets=3 mixed-range=true permanent-mask=true extra-Tile/wire/liquid-mutations=" + changes + " requiredWallInvalidations=1 completeRetirement=true; genuine native writers and liquid receiver, not Main.tileFrame-only animation.");
        }
        private static void ObjectProof(object context, object host, object input, LibraryState library, Action step)
        {
            // OpenDoor synchronously frames its result. CheckDoorOpen and
            // CheckDoorClosed require solid hinge supports at both ends;
            // without them the original returns true then destroys the door.
            Require(WorldGen.PlaceTile(20, 19, 1, true, true) && WorldGen.PlaceTile(20, 23, 1, true, true), "original door has both required solid hinge supports");
            for (int y = 0; y < 3; y++) { Main.tile[20, 20 + y].active(true); Main.tile[20, 20 + y].type = 10; Main.tile[20, 20 + y].frameX = (short)(y * 18); Main.tile[20, 20 + y].frameY = (short)(y * 18); }
            var sourceType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
            var source = (IBlueprintCaptureSource)Activator.CreateInstance(sourceType, Flags, null, new object[] { (Func<bool>)(() => true) }, null);
            CaptureCell initial; Require(source.Read(20, 20, out initial), "literal original door source readable"); var shape = source.Describe(initial); Require(shape != null && shape.Object.Reliable, "native capture explains complete door metadata");
            var obj = shape.Object; obj.X = obj.Y = 0; obj.Id = 1; var members = new List<CaptureCell>();
            for (int y = 0; y < 3; y++) { CaptureCell cell; Require(source.Read(20, 20 + y, out cell), "door source member"); cell.X = 0; cell.Y = y; cell.ObjectId = 1; members.Add(cell); }
            members.Add(new CaptureCell { X = 20, Y = 0, Layers = CaptureLayer.Wire, Wires = 8 });
            var window = new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "door-match", 1, 1), 1,
                new CaptureSummary { Width = 21, Height = 3, ContentCount = 4 }, new BlueprintProjectionBounds(0, 0, 21, 3), members.ToArray(), new[] { obj });
            var entry = NativeBlueprintProjectionVisualChecks.Save(library, window);
            var placed = Place(host, input, entry, 20, 20, step); AwaitReady(placed, step);
            Require(Matches(placed) == 3 && Scene(host).Length == 1, "whole furniture reads all members, ignores independent closed-door random rows, and cannot complete while sentinel missing");
            Require(WorldGen.OpenDoor(20, 20, -1), "actual native door opens left beyond the source footprint");
            try { NativeQuickItemChecks.Until(() => { step(); return Matches(placed) == 3; }); }
            catch
            {
                var proof = GetOptional(placed, "Match");
                Console.WriteLine("DOOR actual open-left proof matches=" + Matches(placed) + " targets=" + GetOptional(proof, "Targets") + " unknown=" + GetOptional(proof, "Unknown") + " current=" + GetOptional(proof, "Current"));
                for (int y = 19; y <= 23; y++) for (int x = 18; x <= 21; x++)
                {
                    CaptureCell actual; source.Read(x, y, out actual); var described = source.Describe(actual); var tile = Main.tile[x, y];
                    Console.WriteLine("DOOR world=" + x + "," + y + " active=" + tile.active() + " type=" + tile.type + " frame=" + tile.frameX + "," + tile.frameY + " paint=" + tile.color() + " object=" + (described == null ? "none" : described.Object.Type + ":" + described.Object.X + "," + described.Object.Y + "/" + described.Object.Width + "x" + described.Object.Height + "/style=" + described.Object.Style + "/reliable=" + described.Object.Reliable));
                }
                throw;
            }
            long beforeWing = (long)Get(host, "ProjectionMatchInvalidations"); WorldGen.PlaceWire4(19, 21); step();
            Require((long)Get(host, "ProjectionMatchInvalidations") == beforeWing && Matches(placed) == 3, "door wing depends on Tile identity/appearance, not an extra wire color targeted elsewhere in the same source range");
            WorldGen.paintTile(19, 21, 11, false, false); Require(Main.tile[19, 21].color() == 11 && Main.tile[20, 21].color() == 0, "only the extra world wing member was painted");
            NativeQuickItemChecks.Until(() => { step(); return Matches(placed) == 0; });
            WorldGen.paintTile(19, 21, 0, false, false); NativeQuickItemChecks.Until(() => { step(); return Matches(placed) == 3; });
            Require(WorldGen.CloseDoor(20, 20, true), "original door closes after extra-wing dependency proof");
            NativeQuickItemChecks.Until(() => { step(); return Matches(placed) == 3; });
            WorldGen.KillTile(20, 21, false, false, true);
            NativeQuickItemChecks.Until(() => { step(); return Matches(placed) == 0; });
            Require(Scene(host).Length == 1, "partial object destruction retracts the entire object proof");
            Call(host, "ClearProjection");
        }
        private static void SparseConvergence(object host, object input, LibraryState library, Action step)
        {
            var entry = Small(library, new CaptureCell { Layers = CaptureLayer.Tile, TileType = 1 }, new CaptureCell { X = 900, Layers = CaptureLayer.Tile, TileType = 1 });
            Require(WorldGen.PlaceTile(40, 50, 1, true, true) && WorldGen.PlaceTile(940, 50, 1, true, true) && WorldGen.PlaceTile(500, 50, 1, true, true), "finite endpoints and unrelated bbox hole prepared");
            var placed = Place(host, input, entry, 40, 50, step);
            Require(library.DebugProjectionStatistics.SourceIndexBytes == 144, "two source positions retain a shared 144-byte charged index despite wide bounds");
            var hooks = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldChangeHooks", true);
            long mapped = (long)hooks.GetField("DebugMappedReads", Flags).GetValue(null), invalidations = (long)Get(host, "ProjectionMatchInvalidations"); int changes = 0;
            for (; changes < 80 && Scene(host).Contains(placed); changes++) { WorldGen.paintTile(500, 50, (byte)(1 + (changes & 1)), false, false); step(); System.Threading.Thread.Sleep(2); }
            Require(!Scene(host).Contains(placed) && (long)Get(host, "ProjectionMatchInvalidations") == invalidations && (long)hooks.GetField("DebugMappedReads", Flags).GetValue(null) == mapped,
                "continuous genuine writes in a sparse bbox hole neither track Tile references nor revoke current proof and cannot prevent retirement");
            Require((int)Get(host, "ProjectionMatchWatchCount") == 0 && (int)hooks.GetProperty("DebugScopeBuffers", Flags).GetValue(null, null) == 0, "last sparse instance releases watch and native association buffers");
            Console.WriteLine("PASS BlueprintWorldMatch sparse width=901 positions=2 indexBytes=144 hole-native-writes=" + changes + " deltaTrackedTiles=0 deltaInvalidations=0 naturalRetirement=true");
        }
        private static void OperatingCompletion(object host, object input, LibraryState library, Action step, bool multiple = false,int synchronized = 0,bool doors=false,bool blockedLeft=false)
        {
            // 2047 cell records plus the one object record fill the existing
            // 2048-record physical chunk without changing its format/budget.
            const int ox = 40, oy = 110, width = 900;
            int[] lamps=synchronized>0?Enumerable.Range(0,synchronized).Select(i=>i*6).ToArray():multiple?new[]{0,200}:new[]{0};
            int records=2048-lamps.Length;
            for (int i = 0; i < records; i++) Main.tile[ox + i % width, oy + i / width].wall = 7;
            foreach(int x in lamps)
            {
                Require(WorldGen.PlaceTile(ox+x, oy + 3, 1, true, true), "operating lamp has original solid support");
                if(doors)Require(WorldGen.PlaceTile(ox+x,oy-1,1,true,true),"operating door has both original solid anchors");
                if(blockedLeft)for(int row=0;row<3;row++)Require(WorldGen.PlaceTile(ox+x-1,oy+row,1,true,true),"only one legal opening wing is needed; the unused wing contains actual solid Tile");
                for (int row = 0; row < 3; row++) { var tile = Main.tile[ox+x, oy + row]; tile.active(true); tile.type = (ushort)(doors?10:93); tile.frameX = 0; tile.frameY = (short)(row * 18); }
            }
            if(multiple)Require(WorldGen.PlaceTile(ox+100,oy,1,true,true),"stable ordinary Tile lies between the two changing same-layer objects");
            var sourceType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
            var source = (IBlueprintCaptureSource)Activator.CreateInstance(sourceType, Flags, null, new object[] { (Func<bool>)(() => true) }, null);
            CaptureCell member;var objects=new List<CaptureObject>();var cells = new List<CaptureCell>();
            for(int i=0;i<lamps.Length;i++)
            {
                Require(source.Read(ox+lamps[i], oy, out member), "real operating lamp source"); var shape = source.Describe(member);
                Require(shape != null && shape.Object.Reliable, "operating lamp has complete original metadata");
                var obj=shape.Object;obj.X=lamps[i];obj.Y=0;obj.Id=i+1;objects.Add(obj);
            }
            for (int i = 0; i < records; i++)
            {
                int x = i % width, y = i / width;
                int lamp=Array.IndexOf(lamps,x);
                if (lamp>=0) { Require(source.Read(ox+x, oy + y, out member), "operating lamp complete source member"); member.X = x; member.Y = y; member.ObjectId = lamp+1; cells.Add(member); }
                else cells.Add(new CaptureCell { X = x, Y = y, Layers = CaptureLayer.Wall | (multiple&&x==100&&y==0?CaptureLayer.Tile:CaptureLayer.None), TileType=(ushort)(multiple&&x==100&&y==0?1:0),WallType = 7 });
            }
            var entry = NativeBlueprintProjectionVisualChecks.Save(library, new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "operating-completion", 1, 1), 1,
                new CaptureSummary { Width = width, Height = 3, ContentCount = records }, new BlueprintProjectionBounds(0, 0, width, 3), cells.ToArray(),objects.ToArray()));
            int updates = 0, changes = 0; long invalidations = (long)Get(host,"ProjectionMatchInvalidations"), reads = (long)Get(host,"ProjectionMatchCellReads");
            Action operating = () =>
            {
                if(++updates>0)
                {
                    foreach(int x in lamps)
                    {
                        if(doors)Require(Main.tile[ox+x,oy].type==10?WorldGen.OpenDoor(ox+x,oy,blockedLeft?1:-1):WorldGen.CloseDoor(ox+x,oy,true),"real synchronous door operating transition has legal anchors and succeeds");
                        else Wiring.ToggleLamp(ox+x,oy,Main.tile[ox+x,oy],null,false);
                    }
                    changes++;
                }
                step();
            };
            var placed = Place(host, input, entry, ox, oy, operating);
            var initial=Get(placed,"Match");var ordinary=((IEnumerable)Get(initial,"Ranges")).Cast<object>().Single(r=>((BlueprintProjectionSourceRange)Get(r,"Source")).TargetLayerCount>3);
            long indexBytes=library.DebugProjectionStatistics.SourceIndexBytes;
            Require((int)Get(host,"ProjectionMatchWatchCount")==1 && ((Array)Get(initial,"Ranges")).Length==lamps.Length+1,"native watch count remains one physical block while only declared object Tile proofs are separated");
            try
            {
                for (int i = 0; i < 320 && Scene(host).Contains(placed); i++) { operating(); System.Threading.Thread.Sleep(2); }
                var state = GetOptional(placed,"Match");
                Console.WriteLine("OPERATING completion kind="+(doors?"door":"lamp")+" blockedUnusedWing="+blockedLeft+" records="+records+" objects="+lamps.Length+" ordinaryTileBetween="+multiple+" nativeWatches=1 logicalProofs="+(lamps.Length+1)+" sharedIndexBytes="+indexBytes+" updates="+updates+" realToggle="+changes+" invalidations="+((long)Get(host,"ProjectionMatchInvalidations")-invalidations)+" ordinaryInvalidations="+Get(ordinary,"Version")+" worldReads="+((long)Get(host,"ProjectionMatchCellReads")-reads)+" current="+(state==null?"retired":Get(state,"Current"))+" ranges="+(state==null?0:((Array)Get(state,"Ranges")).Length)+" retired="+!Scene(host).Contains(placed));
                Require(changes >= 2 && (long)Get(ordinary,"Version")==0 && !Scene(host).Contains(placed), "complete canonical operating objects and same-block stable layers converge under genuine every-update transitions without a union-bounds ordinary Tile invalidation");
            }
            finally { Call(host,"ClearProjection"); }
        }
        private static void AnimatedCompletion(object host, object input, LibraryState library, Action step)
        {
            const int x = 250, y = 70;
            for (int row = 0; row < 3; row++) for (int column = 0; column < 2; column++)
            { var tile = Main.tile[x + column, y + row]; tile.active(true); tile.type = 658; tile.frameX = (short)(column * 18); tile.frameY = (short)(row * 18); }
            var sourceType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
            var source = (IBlueprintCaptureSource)Activator.CreateInstance(sourceType, Flags, null, new object[] { (Func<bool>)(() => true) }, null);
            CaptureCell member; Require(source.Read(x, y, out member), "animated monolith readable"); var shape = source.Describe(member); Require(shape != null && shape.Object.Reliable, "animated monolith has reliable native metadata");
            var obj = shape.Object; obj.X = obj.Y = 0; obj.Id = 1; var cells = new List<CaptureCell>();
            for (int row = 0; row < 3; row++) for (int column = 0; column < 2; column++) { Require(source.Read(x + column, y + row, out member), "complete animated monolith member"); member.X = column; member.Y = row; member.ObjectId = 1; cells.Add(member); }
            var entry = NativeBlueprintProjectionVisualChecks.Save(library, new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "animated", 1, 1), 1,
                new CaptureSummary { Width = 2, Height = 3, ContentCount = 6 }, new BlueprintProjectionBounds(0, 0, 2, 3), cells.ToArray(), new[] { obj }));
            int frame = Main.tileFrame[658], frames = 0;
            try
            {
                Action animated = () => { Main.tileFrame[658] = ++frames % 6; step(); };
                var placed = Place(host, input, entry, x, y, animated);
                NativeQuickItemChecks.Until(() => { animated(); return !Scene(host).Contains(placed); });
                Require(frames > 0 && Main.tile[x, y].active() && Main.tile[x, y].type == 658, "stable complete native object retires naturally while its original draw animation array changes every update, including immediate safe retirement");
                Console.WriteLine("PASS BlueprintWorldMatch animated complete monolith members=6 animation-updates=" + frames + " actual fixed retirement=true");
            }
            finally { Main.tileFrame[658] = frame; Call(host, "ClearProjection"); }
        }
        private static void ObjectInFlight(object context, object host, object input, LibraryState library, Action step)
        {
            const int ox = 300, oy = 20, sentinel = 20;
            var captureType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
            var source = (IBlueprintCaptureSource)Activator.CreateInstance(captureType, Flags, null, new object[] { (Func<bool>)(() => true) }, null);
            var cells = new List<CaptureCell>();
            for (int row = 0; row < 3; row++) { var tile = Main.tile[ox, oy + row]; tile.active(true); tile.type = 10; tile.frameX = tile.frameY = (short)(row * 18); }
            CaptureCell member; Require(source.Read(ox, oy, out member), "finite door source"); var shape = source.Describe(member); Require(shape != null && shape.Object.Reliable, "original metadata explains complete door");
            var obj = shape.Object; obj.X = obj.Y = 0; obj.Id = 1;
            for (int row = 0; row < 3; row++) { Require(source.Read(ox, oy + row, out member), "finite complete door member"); member.X = 0; member.Y = row; member.ObjectId = 1; cells.Add(member); }
            cells.Add(new CaptureCell { X = sentinel, Y = 0, Layers = CaptureLayer.Wire, Wires = 8 });
            var window = new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "object-in-flight", 1, 1), 1,
                new CaptureSummary { Width = sentinel + 1, Height = 3, ContentCount = cells.Count }, new BlueprintProjectionBounds(0, 0, sentinel + 1, 3), cells.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray(), new[] { obj });
            var timing = new HarmonyLib.Harmony("JueMingR.Tests.Blueprint.ObjectReadTiming");
            int mutations = 0;
            try
            {
                // Schedule a true native mutation immediately after the real
                // second member sample. No success, dirty or match value is
                // injected; the adopted native store hooks must revoke proof.
                objectSample = (x, y) => { if (x != ox || y != oy + 1) return; objectSample = null; mutations++; WorldGen.KillTile(ox, oy + 1, false, false, true); WorldGen.PlaceWire4(ox + sentinel, oy); };
                var matchSource = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource", true);
                timing.Patch(matchSource.GetMethod("TryRead", Flags), postfix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks), nameof(AfterObjectSample)));
                var placed = Place(host, input, NativeBlueprintProjectionVisualChecks.Save(library, window), ox, oy, step);
                AwaitReady(placed, step);
                Require(mutations == 1 && Scene(host).Contains(placed) && Matches(placed) == 1,
                    "a real door mutation between member samples retracts the entire in-flight object proof; a newly built sentinel cannot complete with historical object members");
            }
            finally { objectSample = null; timing.UnpatchAll(timing.Id); Call(host, "ClearProjection"); }
            Console.WriteLine("PASS BlueprintWorldMatch object-in-flight: actual second member read then real middle-member demolition/new sentinel, adopted writer invalidation, no stitched whole-object completion.");
        }
        private static Action<int, int> objectSample;
        private static void AfterObjectSample(int x, int y) { objectSample?.Invoke(x, y); }
        private static void DoorSeedInFlight(object host,object input,LibraryState library,Action step)
        {
            const int ox=300,oy=50;
            Require(WorldGen.PlaceTile(ox,oy-1,1,true,true) && WorldGen.PlaceTile(ox,oy+3,1,true,true) && WorldGen.PlaceTile(ox-1,oy,1,true,true),"seed fixture has real door anchors and an ordinary unused-wing Tile");
            for(int row=0;row<3;row++){var tile=Main.tile[ox,oy+row];tile.active(true);tile.type=10;tile.frameX=0;tile.frameY=(short)(row*18);}
            Main.tile[ox-1,oy].frameX=Main.tile[ox-1,oy].frameY=-1;
            var sourceType=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource",true);
            var source=(IBlueprintCaptureSource)Activator.CreateInstance(sourceType,Flags,null,new object[]{(Func<bool>)(()=>true)},null);
            CaptureCell member;Require(source.Read(ox,oy,out member),"seed target source is gated");var shape=source.Describe(member);Require(shape!=null && shape.Object.Reliable,"seed target is an original door");
            var obj=shape.Object;obj.X=obj.Y=0;obj.Id=1;var cells=new List<CaptureCell>();
            for(int row=0;row<3;row++){Require(source.Read(ox,oy+row,out member),"seed target complete member");member.X=0;member.Y=row;member.ObjectId=1;cells.Add(member);}
            cells.Add(new CaptureCell{X=20,Layers=CaptureLayer.Wire,Wires=8});
            var entry=NativeBlueprintProjectionVisualChecks.Save(library,new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"seed-in-flight",1,1),1,new CaptureSummary{Width=21,Height=3,ContentCount=4},new BlueprintProjectionBounds(0,0,21,3),cells.ToArray(),new[]{obj}));
            var timing=new HarmonyLib.Harmony("JueMingR.Tests.Blueprint.DoorSeedTiming");int changes=0;
            try
            {
                objectSample=(x,y)=>
                {
                    if(x!=ox-1 || y!=oy)return;objectSample=null;
                    var read=Get(host,"projectionMatchRead");Require(GetOptional(read,"WriteSeed")!=null,"first unproven seed sample owns its live raw-interest lease");
                    var range=Get(read,"Range");var group=Get(range,"Group");
                    Framing.SelfFrame8Way(x,y,Main.tile[x,y],false);changes++;
                    Require(Main.tile[x,y].frameX!=-1 || Main.tile[x,y].frameY!=-1,"real ordinary framing changed the previously sampled raw seed");
                    Require(!(bool)CallResult(Get(host,"projectionChanges"),"IsCurrent",Get(range,"Watch"),Get(group,"NativeStamp")),"ordinary raw frame revokes the unproven door seed before a witness can be published");
                };
                var matchSource=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource",true);
                timing.Patch(matchSource.GetMethod("TryRead",Flags),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks),nameof(AfterObjectSample)));
                var placed=Place(host,input,entry,ox,oy,step);AwaitReady(placed,step);
                Require(changes==1 && Scene(host).Contains(placed) && Matches(placed)==3,"stale seed is retried through the real source and cannot complete with the missing sentinel");
                long invalidations=(long)Get(host,"ProjectionMatchInvalidations");Require(WorldGen.OpenDoor(ox,oy,1),"fresh certified door still opens on its unblocked wing");step();
                Require(Matches(placed)==3 && (long)Get(host,"ProjectionMatchInvalidations")==invalidations,"fresh seed and complete real occupancy retain only an already current canonical proof; matches="+Matches(placed)+" hardInvalidations="+((long)Get(host,"ProjectionMatchInvalidations")-invalidations));
            }
            finally{objectSample=null;timing.UnpatchAll(timing.Id);Call(host,"ClearProjection");}
            Console.WriteLine("PASS BlueprintWorldMatch door seed in-flight: actual gated wing sample then original ordinary SelfFrame8Way; old native stamp revoked before first witness, fresh source retry and certified right-open, no success injection.");
        }
        private static void OpenDoorDependencies(object host,object input,LibraryState library,Action step)
        {
            for(int direction=-1;direction<=1;direction+=2)
            {
                int hinge=direction<0?450:550,y=85,outside=hinge+direction*2;
                Require(WorldGen.PlaceTile(hinge,y-1,1,true,true) && WorldGen.PlaceTile(hinge,y+3,1,true,true) && WorldGen.PlaceTile(outside,y,1,true,true),"open-target fixture has original anchors and a foreign Tile beyond every legal wing");
                for(int row=0;row<3;row++){var tile=Main.tile[hinge,y+row];tile.active(true);tile.type=10;tile.frameX=0;tile.frameY=(short)(row*18);}
                Require(WorldGen.OpenDoor(hinge,y,direction),"original source opens in the requested direction");
                int origin=direction<0?hinge-1:hinge;
                var sourceType=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource",true);
                var source=(IBlueprintCaptureSource)Activator.CreateInstance(sourceType,Flags,null,new object[]{(Func<bool>)(()=>true)},null);
                CaptureCell member;Require(source.Read(origin,y,out member),"original open target source readable");var shape=source.Describe(member);
                Require(shape!=null && shape.Object.Reliable && shape.Object.Width==2 && shape.Object.Direction==(direction<0?1:2) && shape.Object.OriginX==(direction<0?1:0),"actual original direction/origin identifies the open target's hinge");
                var obj=shape.Object;obj.X=obj.Y=0;obj.Id=1;var cells=new List<CaptureCell>();
                for(int row=0;row<3;row++)for(int column=0;column<2;column++){Require(source.Read(origin+column,y+row,out member),"open source includes both actual columns");member.X=column;member.Y=row;member.ObjectId=1;cells.Add(member);}
                cells.Add(new CaptureCell{X=20,Layers=CaptureLayer.Wire,Wires=8});
                var entry=NativeBlueprintProjectionVisualChecks.Save(library,new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"open-dependency",1,1),1,new CaptureSummary{Width=21,Height=3,ContentCount=7},new BlueprintProjectionBounds(0,0,21,3),cells.ToArray(),new[]{obj}));
                try
                {
                    var placed=Place(host,input,entry,origin,y,step);AwaitReady(placed,step);Require(Matches(placed)==6,"the actual two-column open target has complete Match while its sentinel is missing");
                    long before=(long)Get(host,"ProjectionMatchInvalidations"),reads=(long)Get(host,"ProjectionMatchCellReads");
                    for(int n=0;n<40;n++){WorldGen.paintTile(outside,y,(byte)(1+(n&1)),false,false);step();}
                    Require(Matches(placed)==6 && (long)Get(host,"ProjectionMatchInvalidations")==before && (long)Get(host,"ProjectionMatchCellReads")==reads,"real continuous Tile changes outside the exact three-column union do not revoke or rescan an open target");
                    WorldGen.PlaceWire4(origin+20,y);int updates=0;
                    for(;updates<80 && Scene(host).Contains(placed);updates++){WorldGen.paintTile(outside,y,(byte)(1+(updates&1)),false,false);step();System.Threading.Thread.Sleep(2);}
                    Require(!Scene(host).Contains(placed),"the complete open target retires naturally while unrelated outer-column native changes continue");
                    Console.WriteLine("PASS BlueprintWorldMatch open source direction="+direction+" originalColumns=2 requiredUnionColumns=3 foreignOuterChanges="+(40+updates)+" stableWorldReads=0 currentMatch=6 naturalRetirement=true");
                }
                finally{Call(host,"ClearProjection");}
            }
        }
        private static LibraryEntry Small(LibraryState library, params CaptureCell[] cells)
        {
            int width = cells.Max(c => c.X) + 1, height = cells.Max(c => c.Y) + 1;
            return NativeBlueprintProjectionVisualChecks.Save(library, new BlueprintProjectionWindow(
                new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "qualification", 1, 1), 1,
                new CaptureSummary { Width = width, Height = height, ContentCount = cells.Length }, new BlueprintProjectionBounds(0, 0, width, height), cells, Array.Empty<CaptureObject>()));
        }
        private static void UnknownAndCandidate(object context, object host, object input, LibraryState library, Action step)
        {
            // A literal invalid metadata frame is readable Tile data, while its
            // furniture identity is Unknown. The independent wall stays known.
            var unknown = Main.tile[80, 70]; unknown.active(true); unknown.type = 658; unknown.frameX = unknown.frameY = Int16.MaxValue;
            var bottom = Place(host, input, Small(library, new CaptureCell { Layers = CaptureLayer.Wall, WallType = 7 }), 80, 70, step);
            var top = Place(host, input, Small(library, new CaptureCell { Layers = CaptureLayer.Tile, TileType = 1 }), 80, 70, step);
            AwaitReady(bottom, step);
            NativeQuickItemChecks.Until(() => { step(); var state = GetOptional(top, "Match"); return state != null && (int)Get(state, "Current") == ((Array)Get(state, "Ranges")).Length && (long)Get(state, "Unknown") == 1; });
            NativeQuickItemChecks.Until(() => { Call(host, "UpdateProjectionWindow", new WorldTargetView(70, 60, 32, 24, 0)); step(); return Scene(host).All(p => GetOptional(p, "Window") != null); });
            Require(CallResult(host, "ProjectionMoveEligibility", bottom).ToString() == "Movable", "independent missing wall is known zero-Match despite unreadable furniture meaning");
            StartSelection(host, input, false); Pointer(host, false, 80, 70); Pointer(host, true, 80, 70);
            Require(Mode(host) == "SelectingMove" && GetOptional(host, "ProjectionCandidate") == null && ReferenceEquals(Get(host, "ProjectionHover"), top) && Get(host, "ProjectionHoverMoveEligibility").ToString() == "Checking",
                "top Unknown target has the same hover/click qualification and cannot select through to a known movable wall instance");
            Call(host, "CancelProjection"); StartSelection(host, input, true); Pointer(host, false, 80, 70); Pointer(host, true, 80, 70);
            Require(Scene(host).Length == 1 && ReferenceEquals(Scene(host)[0], bottom), "Remove remains independent of Unknown move qualification"); Call(host, "ClearProjection");

            var entry = Small(library, new CaptureCell { Layers = CaptureLayer.Tile, TileType = 1 }, new CaptureCell { X = 3, Layers = CaptureLayer.Wire, Wires = 1 });
            var original = Place(host, input, entry, 150, 70, step); AwaitReady(original, step);
            Require(WorldGen.PlaceTile(180, 70, 1, true, true), "already built candidate destination");
            NativeQuickItemChecks.Until(() => { Call(host, "UpdateProjectionWindow", new WorldTargetView(140, 60, 60, 24, 0)); step(); return GetOptional(original, "Window") != null; });
            StartSelection(host, input, false); Pointer(host, false, 150, 70); Pointer(host, true, 150, 70); Require(Mode(host) == "Moving", "known empty original is selectable");
            Pointer(host, false, 180, 70); for (int i = 0; i < 4; i++) step();
            Require(Mode(host) == "Moving" && Matches(original) == 0 && GetOptional(Get(host, "ProjectionCandidate"), "Match") == null,
                "Moving candidate at built destination stays alive and cannot borrow original-location qualification");
            Pointer(host, true, 180, 70); NativeQuickItemChecks.Until(() => { step(); return Mode(host) == "Fixed"; });
            var moved = Scene(host).Single(); AwaitReady(moved, step);
            Require((int)Get(moved, "OriginX") == 180 && Matches(moved) == 1, "destination Match does not reject a legal move from independently empty restore location"); Call(host, "ClearProjection");

            Require(WorldGen.PlaceTile(200, 70, 1, true, true), "fully built Following destination");
            var completed = Small(library, new CaptureCell { Layers = CaptureLayer.Tile, TileType = 1 });
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Require((bool)Call(host, "Place", completed), "Following complete-world preview admitted");
            NativeQuickItemChecks.Until(() => { step(); return Mode(host) == "Following"; }); var preview = Get(host, "ProjectionCandidate"); Pointer(host, false, 200, 70);
            for (int i = 0; i < 4; i++) step(); Require(Mode(host) == "Following" && ReferenceEquals(preview, Get(host, "ProjectionCandidate")), "unconfirmed Following never disappears over an already complete building");
            Pointer(host, true, 200, 70); NativeQuickItemChecks.Until(() => { step(); return Scene(host).Contains(preview); });
            NativeQuickItemChecks.Until(() => { step(); return !Scene(host).Contains(preview); });
            Require(Main.tile[200, 70].active(), "confirming an already complete new instance retires only after fixed proof and leaves the world building");

            Require(WorldGen.PlaceTile(220, 70, 1, true, true), "permanent cover built world");
            var layered = Place(host, input, Small(library, new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Wall, TileType = 1, WallType = 7 }, new CaptureCell { X = 3, Layers = CaptureLayer.Wire, Wires = 2 }), 220, 70, step); AwaitReady(layered, step);
            Require(Matches(layered) == 1, "old Tile is hidden by world Match before overlap");
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Require((bool)Call(host, "Place", completed), "cover preview admitted");
            NativeQuickItemChecks.Until(() => { step(); return Mode(host) == "Following"; }); Pointer(host, false, 220, 70);
            Require(((BlueprintProjectionMask)Get(layered, "Mask")).RemovedLayerCount == 0, "preview world Match does not write the permanent mask");
            Pointer(host, true, 220, 70); NativeQuickItemChecks.Until(() => { step(); return ((BlueprintProjectionMask)Get(layered, "Mask")).RemovedLayerCount == 1; });
            var mask = (BlueprintProjectionMask)Get(layered, "Mask"); NativeQuickItemChecks.Until(() => { step(); return Scene(host).Length == 1 && ReferenceEquals(Scene(host)[0], layered); });
            WorldGen.KillTile(220, 70, false, false, true); AwaitReady(layered, step);
            Require(ReferenceEquals(mask, Get(layered, "Mask")) && (mask.Apply(new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Wall, TileType = 1, WallType = 7 }).Layers & CaptureLayer.Tile) == 0,
                "effective overlap consumes a world-hidden Tile and permanent erasure survives automatic cover retirement and later world demolition");
            Call(host, "ClearProjection");
            Console.WriteLine("PASS BlueprintWorldMatch Unknown top selection/no-through/independent Remove, destination versus restore qualification, complete Following preview, and world-hidden overlap with permanent mask after auto-retirement.");
        }
        private static void SlicedProofAndCommit(object context, object host, object input, LibraryState library, Action step)
        {
            var cells = Enumerable.Range(0, 300).Select(x => new CaptureCell { X = x, Y = 0, Layers = CaptureLayer.Tile, TileType = 1 }).ToArray();
            var window = new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "sliced-match", 1, 1), 1,
                new CaptureSummary { Width = 300, Height = 1, ContentCount = 300 }, new BlueprintProjectionBounds(0, 0, 300, 1), cells, Array.Empty<CaptureObject>());
            var entry = NativeBlueprintProjectionVisualChecks.Save(library, window);
            for (int x = 500; x < 799; x++) Require(WorldGen.PlaceTile(x, 100, 1, true, true), "finite pre-existing source row");
            var instance = Place(host, input, entry, 500, 100, step);
            NativeQuickItemChecks.Until(() => { step(); var read = GetOptional(host, "projectionMatchRead"); return read != null && ReferenceEquals(Get(read,"Owner"),instance) && (int)Get(read,"CellIndex") >= 256; });
            // UpdateRuntime owns and increments its token on every callback;
            // calling it twice is two different Update tokens. Repeat the exact
            // already consumed Host token to test the real budget contract.
            ulong consumedTick = (ulong)Get(host,"projectionMatchTick");
            long sameTickReads = (long)Get(host, "ProjectionMatchCellReads");
            Call(host, "Poll"); Call(host, "Update", consumedTick); Call(host, "Poll");
            Require((long)Get(host, "ProjectionMatchCellReads") == sameTickReads, "Poll plus repeated Host callback with the same consumed updateTick cannot provide a second read allowance");
            WorldGen.KillTile(500, 100, false, false, true); Require(WorldGen.PlaceTile(799, 100, 1, true, true), "tail becomes correct only after previously read head was demolished");
            AwaitReady(instance, step);
            Require(Scene(host).Contains(instance) && Matches(instance) == 299, "cross-tick traversal cannot add historical head Match to a later tail and retire an incomplete row");

            var other = NativeBlueprintProjectionVisualChecks.Save(library, new BlueprintProjectionWindow(window.Identity, 1,
                new CaptureSummary { Width = 1, Height = 1, ContentCount = 1 }, new BlueprintProjectionBounds(0, 0, 1, 1),
                new[] { new CaptureCell { X = 0, Y = 0, Layers = CaptureLayer.Tile, TileType = 2 } }, Array.Empty<CaptureObject>()));
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Require((bool)Call(host, "Place", other), "unrelated real placement starts");
            NativeQuickItemChecks.Until(() => { step(); return Mode(host) == "Following"; });
            Pointer(host, false, 80, 100); Pointer(host, true, 80, 100); Require(Mode(host) == "Committing", "unrelated transaction is actually accepted");
            // Hold the actual unmodified worker delivery, never a fabricated
            // success/match bit. This deterministically represents a delayed
            // receipt while the normal Host update continues its match work.
            BlueprintProjectionCommitReceipt delivery = null;
            NativeQuickItemChecks.Until(() => library.TryTakeProjectionCommit(out delivery));
            Require(delivery.Succeeded, "held receipt came from the real overlap computation");
            Require(WorldGen.PlaceTile(500, 100, 1, true, true), "last missing row member built through original mutation");
            NativeQuickItemChecks.Until(() => { step(); var state = GetOptional(instance, "Match"); return state != null && (int)Get(state, "Current") == ((Array)Get(state, "Ranges")).Length && (long)Get(state,"Unknown")==0 && (long)Get(state,"Matches")==300; });
            Require(Mode(host) == "Committing" && Scene(host).Contains(instance) && GetOptional(host, "ProjectionCandidate") != null,
                "current complete proof waits without cancelling an unrelated real Committing transaction");
            var worker = Get(library, "worker"); var gate = Get(worker, "gate");
            lock (gate) { Require(GetOptional(worker, "projectionCommitReceipt") == null, "delivery hold did not replace another receipt"); worker.GetType().GetField("projectionCommitReceipt", Flags).SetValue(worker, delivery); }
            NativeQuickItemChecks.Until(() => { step(); return Mode(host) == "Fixed" && !Scene(host).Contains(instance); });
            Require(Scene(host).Length == 1 && ((BlueprintProjectionIdentity)Get(Scene(host)[0], "Identity")).AssetId == other.AssetId,
                "safe exit applies the unrelated commit then retires only the completed row");
            Call(host, "ClearProjection");
            Console.WriteLine("PASS BlueprintWorldMatch cross-tick 300-target/physical-source-workset history revocation and delayed real commit delivery: no stale completion, no unrelated cancellation, safe deferred single retirement.");
        }
        private static object Place(object host, object input, LibraryEntry entry, int x, int y, Action step)
        {
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Require((bool)Call(host, "Place", entry), "real Place admission");
            NativeQuickItemChecks.Until(() => { step(); return Mode(host) == "Following"; });
            var candidate = Get(host, "ProjectionCandidate"); var summary = (CaptureSummary)Get(candidate, "Summary"); int px = x + (summary.Width - 1) / 2, py = y + (summary.Height - 1) / 2;
            Pointer(host, false, px, py); for (int i = 0; i < 3; i++) step();
            Require(Mode(host) == "Following", "an unconfirmed preview never auto-retires");
            long retired = (long)Get(host,"ProjectionMatchRetirements");
            Pointer(host, true, px, py); NativeQuickItemChecks.Until(() => { step(); return Mode(host) == "Fixed"; });
            Require(Scene(host).Contains(candidate) || (long)Get(host,"ProjectionMatchRetirements") > retired && GetOptional(candidate,"Match")==null,
                "confirmed Fixed is either still in the scene or already safely retired by complete current proof");
            return candidate;
        }
        private static void AwaitReady(object item, Action step)
        { NativeQuickItemChecks.Until(() => { step(); var state = GetOptional(item, "Match"); return state != null && (int)Get(state, "Current") == ((Array)Get(state, "Ranges")).Length && (long)Get(state, "Unknown") == 0; }); }
        private static void RequireListedAsset(LibraryState library,Guid asset,string reason)
        {
            // The real library has six rows per page. Other fixtures may save
            // more assets; absence from its last selected page is not deletion.
            // Ask the actual reader to select this ID and still require it.
            int previousPage=library.Snapshot.Page?.PageIndex??-1;
            bool wasVisible=library.Snapshot.Page!=null && library.Snapshot.Page.Entries.Any(e=>e.AssetId==asset);
            Require(library.TryList(77001,0,asset),"retained template list request admitted");
            LibraryReceipt receipt=null;
            NativeQuickItemChecks.Until(()=>{library.Pump();return library.TryTakeReceipt(out receipt);});
            Require(receipt.Kind==LibraryCommandKind.List && receipt.Generation==77001 && receipt.Page!=null && receipt.Page.Entries.Any(e=>e.AssetId==asset),reason+"; actualPage="+receipt.Page?.PageIndex+" total="+receipt.Page?.TotalCount);
            if(!wasVisible)Console.WriteLine("PASS retained template actual list: priorPage="+previousPage+" selectedPage="+receipt.Page.PageIndex+" totalAssets="+receipt.Page.TotalCount+" sixRowsPerPage=true; prior-page absence was not deletion");
        }
        internal static void AwaitMovable(object context, object host, object item, int denseRecords = 0, int denseBlocks = 0, int denseEffective = 0, int cooperatingRecords = 0, int cooperatingBlocks = 0)
        {
            // Legacy window/budget fixtures used Poll alone. Their move must
            // now earn the same complete empty-world proof as a real click;
            // no matcher summary or success bit is supplied by the fixture.
            // EmptyWorld swaps the actual array/section identity. Let the
            // real Update retire that old binding before the explicit query;
            // otherwise this first Update correctly clears the stale priority.
            NativeQuickItemChecks.BeginWorldStep(); Call(context,"UpdateRuntime");
            Call(host, "CheckProjectionMove", item);
            var before = ((LibraryState)Get(host,"Library")).DebugProjectionStatistics;
            Func<bool> advance = () =>
            {
                NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime");
                return CallResult(host, "ProjectionMoveEligibility", item).ToString() == "Movable";
            };
            try
            {
                if (denseRecords == 0) NativeQuickItemChecks.Until(advance);
                else
                {
                    // Three literal fixtures need bounded work rather than an
                    // eight-second wall clock: 512x260 recovery with one mask,
                    // forty 128x128 A40 sources, or one 512x128 eight-layer
                    // command-pressure source. Keep the same stagnation
                    // failure and a total facility guard; wakes/slice counts
                    // do not renew either instance's semantic progress.
                    bool recovery=denseRecords==512*260 && denseBlocks==65 && denseEffective==denseRecords-1 && cooperatingRecords==0 && cooperatingBlocks==0;
                    bool strips=denseRecords==128*128 && denseBlocks==8 && (denseEffective==128 || denseEffective==128*128) && cooperatingRecords==40*128*128 && cooperatingBlocks==40*8;
                    bool commands=denseRecords==512*128 && denseBlocks==32 && denseEffective==denseRecords && cooperatingRecords==0 && cooperatingBlocks==0;
                    Require(recovery || strips || commands,"dense Move proof dimensions, cooperative source bound and effective denominator come from the three literal fixtures");
                    var clock = System.Diagnostics.Stopwatch.StartNew(); long progressAt = 0, world = -1, decoded = -1, blocks = -1;
                    while (true)
                    {
                        bool ready = advance(); var state = GetOptional(item,"Match");
                        var stats = ((LibraryState)Get(host,"Library")).DebugProjectionStatistics;
                        long currentWorld = state == null ? 0 : (long)Get(state,"WorldReads");
                        long currentBlocks = stats.BlockReads-before.BlockReads, currentDecoded = stats.DecodedRecords-before.DecodedRecords;
                        long sourceRecords=cooperatingRecords>0?cooperatingRecords:denseRecords;
                        int sourceBlocks=cooperatingBlocks>0?cooperatingBlocks:denseBlocks;
                        Require(currentWorld <= denseRecords && currentBlocks <= sourceBlocks * 2 && currentDecoded <= sourceRecords * 2L,
                            "dense Move proof stays within one world pass and at most source plus viewport block passes");
                        if (currentWorld != world || currentDecoded != decoded || currentBlocks != blocks)
                        { progressAt = clock.ElapsedMilliseconds; world = currentWorld; decoded = currentDecoded; blocks = currentBlocks; }
                        if (ready)
                        {
                            Require(currentWorld == denseEffective, "dense Move qualification observed every effective empty-world target and skipped permanently covered cells");
                            if(commands)Require((long)Get(state,"Targets")==8L*denseEffective && ((Array)Get(state,"Ranges")).Length==32,"literal command-pressure proof covers all eight layers of 65536 cells in its 32 source blocks");
                            Console.WriteLine("PASS dense Move proof records="+denseRecords+" blocks="+denseBlocks+" cooperatingRecords="+sourceRecords+" cooperatingBlocks="+sourceBlocks+" worldReads="+world+" bodyReads="+blocks+" decoded="+decoded+" elapsedMs="+clock.ElapsedMilliseconds+"; finite work/stagnation guard, not a product latency or FPS gate"); break;
                        }
                        if (clock.ElapsedMilliseconds-progressAt > 8000 || clock.ElapsedMilliseconds > 60000) throw new Exception("Dense Move proof stalled or exceeded facility timeout");
                        System.Threading.Thread.Sleep(2);
                    }
                }
            }
            catch
            {
                var state = GetOptional(item,"Match"); var stats = ((LibraryState)Get(host,"Library")).DebugProjectionStatistics;
                Console.WriteLine("MOVE proof timeout summaryRecords=" + ((CaptureSummary)Get(item,"Summary")).ContentCount + " current=" + GetOptional(state,"Current") + "/" + ((Array)Get(state,"Ranges")).Length + " targets=" + GetOptional(state,"Targets") + " matches=" + GetOptional(state,"Matches") + " unknown=" + GetOptional(state,"Unknown") + " worldReads=" + GetOptional(state,"WorldReads") + " bodyReads=" + (stats.BlockReads-before.BlockReads) + " decoded=" + (stats.DecodedRecords-before.DecodedRecords) + " sourceBatches=" + Get(host,"ProjectionMatchSourceBatches") + " pendingRead=" + Get(host,"ProjectionMatchReading"));
                Console.WriteLine("MOVE range offsets=" + String.Join(",", ((IEnumerable)Get(state,"Ranges")).Cast<object>().Select(r => Get(r,"Offset").ToString()))); throw;
            }
        }
        internal static IDisposable EmptyWorld(object host, int width, int height) { return new EmptyWorldScope(host, width, height); }
        private sealed class EmptyWorldScope : IDisposable
        {
            private readonly object host;
            private readonly Tile[,] tiles = Main.tile;
            private readonly WorldSections sections = Main.sectionManager;
            private readonly Terraria.Map.WorldMap map = Main.Map;
            private readonly int width = Main.maxTilesX, height = Main.maxTilesY, mode = Main.netMode;
            internal EmptyWorldScope(object host, int width, int height)
            {
                this.host = host; Call(host, "ClearProjection"); Main.netMode = 0; Main.maxTilesX = width; Main.maxTilesY = height;
                Main.tile = new Tile[width, height];
                for (int x = 0; x < width; x++) for (int y = 0; y < height; y++) Main.tile[x, y] = new Tile();
                Main.Map = new Terraria.Map.WorldMap(width, height); Main.sectionManager = new WorldSections((width + 199) / 200, (height + 149) / 150); Main.sectionManager.SetAllSectionsLoaded();
            }
            public void Dispose()
            { Call(host, "ClearProjection"); Main.tile = tiles; Main.sectionManager = sections; Main.Map = map; Main.maxTilesX = width; Main.maxTilesY = height; Main.netMode = mode; }
        }
        private static void AwaitSettled(object host, Action step)
        { NativeQuickItemChecks.Until(() => { step(); return !(bool)Get(host, "ProjectionMatchReading") && Scene(host).All(item => { var state = GetOptional(item, "Match"); return state != null && ((IEnumerable)Get(state, "Ranges")).Cast<object>().All(range => !(bool)Get(range, "Demand") && !(bool)Get(range, "Reading")); }); }); }
        private static long Matches(object item) { var state = GetOptional(item, "Match"); return state == null ? 0 : (long)Get(state, "Matches"); }
        private static object[] Scene(object host) { return ((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().ToArray(); }
        private static string Mode(object host) { return Get(host, "ProjectionMode").ToString(); }
        private static void Pointer(object host, bool left, int x, int y) { Call(host, "AcceptWorldPointer", true, left, false, x, y, Main.maxTilesX, Main.maxTilesY); }
        private static void StartSelection(object host, object input, bool remove)
        { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, remove ? "RemoveProjection" : "MoveProjection"); }
        private static object CallResult(object target, string name, params object[] values) { return target.GetType().GetMethod(name, Flags).Invoke(target, values); }
        private static BlueprintWorldMatchResult Result(object item, int x, int y)
        {
            var renderer = Get(item, "Renderer"); object[] args = { x, y, default(BlueprintWorldMatchResult) };
            if (!(bool)renderer.GetType().GetMethod("TryGetMatch", Flags).Invoke(renderer, args)) return default(BlueprintWorldMatchResult);
            return (BlueprintWorldMatchResult)args[2];
        }
        private static void AssertCommand(object item, ushort bit, bool visible, bool red)
        {
            var renderer = Get(item, "Renderer"); var command = ((IEnumerable)Get(renderer, "commands")).Cast<object>().First(c => (int)Get(c, "CellX") == 0 && (int)Get(c, "CellY") == 0 && (ushort)Get(c, "SemanticBits") == bit);
            object[] args = { command, default(BlueprintWorldMatchResult) }; bool actual = (bool)renderer.GetType().GetMethod("ResolveMatch", Flags).Invoke(renderer, args);
            var color = (Microsoft.Xna.Framework.Color)Get(args[0], "Color"); Require(actual == visible && (!red || color.R == 255 && color.G == 48), "actual renderer semantic-layer hide/red consumer");
        }
        private static string Snapshot(int x, int y, int width, int height)
        { return NativeBlueprintProjectionVisualChecks.Snapshot(x, y, width, height); }
        private static Microsoft.Xna.Framework.Color[] Pixels(object context, object host, ProbeGraphics graphics, string output, string name)
        {
            long reads = (long)Get(host, "ProjectionMatchCellReads");
            Call(Get(context, "worldTiles"), "BeginTick"); Action draw = () => Call(Get(host, "World"), "Draw");
            var pixels = graphics.Pixels(draw, Main.GameViewMatrix.ZoomMatrix);
            graphics.Image(System.IO.Path.Combine(output, name + ".png"), draw, Main.GameViewMatrix.ZoomMatrix);
            Require((long)Get(host, "ProjectionMatchCellReads") == reads, "repeated actual GPU Draw never advances world matching"); return pixels;
        }
    }
}
