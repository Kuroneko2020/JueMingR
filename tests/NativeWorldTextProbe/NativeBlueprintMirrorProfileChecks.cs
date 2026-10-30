using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintMirrorProfileChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            NativeBlueprintWorldMatchChecks.InitializeMetadata();
            Assembly host = Get(context, "Blueprint").GetType().Assembly;
            Type resolver = host.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintHorizontalMirrorResolver", true);
            var profile = (IBlueprintHorizontalMirrorProfile)resolver.GetMethod("Create", F).Invoke(null, null);
            Type capture = host.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
            Func<CaptureCell, BlueprintObjectShape> describe = cell =>
            {
                var tile = new Tile { type = cell.TileType, frameX = cell.FrameX, frameY = cell.FrameY }; tile.active(true);
                return (BlueprintObjectShape)capture.GetMethod("DescribeRead", F).Invoke(null, new object[] { cell, tile });
            };
            RunProfile(profile, describe);
            CaptureRoundTrip(context, profile, capture);
        }

        // Expectations below are fixed .8 registration / WorldGen literals.
        // The resolver constructs neither expected counterparts nor answers.
        internal static void RunProfile(IBlueprintHorizontalMirrorProfile profile, Func<CaptureCell, BlueprintObjectShape> describe)
        {
            Require(profile.Ready && profile.Revision != 0, "locked original mirror snapshot is ready");
            foreach (string name in new[] { "Alternates", "SubTiles" })
                Require(typeof(TileObjectData).GetProperty(name, F)?.PropertyType == typeof(List<TileObjectData>), "original private ABI: " + name);
            Require(typeof(TileObjectData).GetProperty("LinkedAlternates", F)?.PropertyType == typeof(bool), "original LinkedAlternates ABI");
            Require(TileObjectData.GetTileData(349, 3, 1).Style == 7 && TileObjectData.GetTileData(105, 4, 1).Style == 165 &&
                TileObjectData.GetTileData(545, 1, 1).Style == 2, "actual alternate sheet offsets are not alternate indices");

            Check(profile, describe, 15, 1, new[] { 40, 58 }, 0, 7, 18, 40, 1, 2, "odd middle-column chair changes direction");
            Check(profile, describe, 79, 4, new[] { 108, 126 }, 0, 10, 72, 108, 3, 2, "bed keeps native negative padding and member coatings");
            Check(profile, describe, 14, 3, new[] { 0, 18 }, 0, 9, 0, 0, 0, 0, "nondirectional furniture keeps legal internal frames");
            Check(profile, describe, 454, 4, new[] { 0, 18, 36 }, 0, 10, 0, 0, 0, 0, "undirected body retains asymmetric native support metadata");
            Check(profile, describe, 349, 2, new[] { 0, 18, 36 }, 108, 9, 108, 54, 3, 2, "same-style seven-offset direction sheet");
            Check(profile, describe, 105, 2, new[] { 0, 18, 36 }, 144, 9, 144, 162, 4, 2, "same-style 165-offset direction sheet");
            Check(profile, describe, 545, 2, new[] { 0, 18, 36 }, 36, 9, 36, 54, 1, 2, "two-offset sheet preserves the other identity slot");
            var doll = Check(profile, describe, 470, 2, new[] { 0, 18, 36 }, 72, 9, 108, 0, 2, 2, "female doll keeps gender while turning");
            Require(doll.OriginX == 0 && doll.OriginY == 2, "doll counterpart retains actual native origin instead of reflecting it");
            Check(profile, describe, 233, 3, new[] { 0, 18 }, 108, 9, 108, 0, 2, 0, "detritus base selector uses its three-column grid");
            Check(profile, describe, 233, 2, new[] { 36, 54 }, 108, 9, 108, 36, 3, 0, "detritus selected region uses its two-column grid");
            Check(profile, describe, 185, 1, new[] { 0 }, 90, 9, 90, 0, 5, 0, "small-pile base selector remains one cell");
            Check(profile, describe, 185, 2, new[] { 18 }, 108, 9, 108, 18, 3, 0, "small-pile selected region remains two cells");
            Check(profile, describe, 165, 1, new[] { 0, 18 }, 54, 7, 54, 0, 3, 0, "stalactite hanging two-cell region");
            Check(profile, describe, 165, 1, new[] { 36, 54 }, 54, 7, 54, 36, 3, 0, "stalactite standing two-cell region");
            Check(profile, describe, 165, 1, new[] { 72 }, 54, 7, 54, 72, 3, 0, "stalactite hanging one-cell region");
            Check(profile, describe, 165, 1, new[] { 90 }, 54, 7, 54, 90, 3, 0, "selector Y=90 remains a one-cell attachment");

            CaptureCell[] door = Pattern(10, 1, new[] { 0, 18, 36 }, 0);
            door[1].FrameX = 18; door[2].FrameX = 36;
            CaptureObject doorObject = Object(describe, door);
            CaptureObject mirrored; CaptureCell[] members; string reason;
            Require(profile.TryMirrorObject(doorObject, door, 7, out mirrored, out members, out reason) && members[0].FrameX == 0 && members[1].FrameX == 18 && members[2].FrameX == 36,
                "closed-door independent row decoration is retained");
            Check(profile, describe, 11, 2, new[] { 0, 18, 36 }, 0, 9, 36, 0, 0, 1, "open-door hinge and wing use opposite legal bank");

            CaptureCell[] operating = Pattern(720, 2, new[] { 54, 72, 90 }, 0);
            CaptureObject operatingObject = Object(describe, operating);
            Require(profile.TryMirrorObject(operatingObject, operating, 9, out mirrored, out members, out reason) && members[0].FrameX == 36 && members[0].FrameY == 54 &&
                mirrored.Style == 0 && mirrored.Direction == 2, "finite operating phase survives the counterpart grid without becoming another style");
            Check(profile, describe, 658, 2, new[] { 108, 126, 144 }, 0, 9, 0, 108, 0, 0, "three-state operating Y phase remains saved");

            CaptureCell[] alias = Pattern(349, 2, new[] { 54, 72, 90 }, 108);
            CaptureObject canonical = Object(describe, alias), old = canonical;
            old.Style = 10; old.Alternate = 0; old.Direction = 1;
            old.PlacementStyle = 10; old.RandomVariant = 0;
            Require(profile.TryNormalizeObject(old, alias, out mirrored, out reason) && mirrored.Style == 3 && mirrored.Direction == 2 && mirrored.Alternate == 1,
                "exact legacy sheet alias is corrected from complete raw evidence");
            old.Style = 99;
            Require(!profile.TryNormalizeObject(old, alias, out mirrored, out reason), "arbitrary metadata mismatch cannot borrow alias permission");
            Require(!profile.TryNormalizeObject(canonical, new[] { alias[0] }, out mirrored, out reason), "partial object cannot normalize or authorize mirror");
            alias[1].FrameY += 18;
            Require(!profile.TryNormalizeObject(canonical, alias, out mirrored, out reason), "malformed member group cannot normalize or authorize mirror");

            Platform(profile, describe);
            CaptureCell ordinary = new CaptureCell { X = 1, Y = 2, Layers = CaptureLayer.Tile | CaptureLayer.Wall | CaptureLayer.Wire | CaptureLayer.Actuator | CaptureLayer.Liquid,
                TileType = 1, WallType = 7, Slope = 3, TilePaint = 4, WallPaint = 5, Wires = 15, Actuator = true, LiquidAmount = 71, LiquidKind = 2, InvisibleTile = true, FullbrightWall = true };
            CaptureCell transformed;
            Require(profile.TryMirrorCell(ordinary, 4, out transformed, out reason) && transformed.X == 2 && transformed.Slope == 4 && transformed.Wires == 15 &&
                transformed.LiquidAmount == 71 && transformed.LiquidKind == 2 && transformed.InvisibleTile && transformed.FullbrightWall && transformed.TilePaint == 4 && transformed.WallPaint == 5,
                "ordinary layers and appearance survive even-width reflection");
            ordinary.HalfBrick = true;
            Require(!profile.TryMirrorCell(ordinary, 4, out transformed, out reason), "halfbrick plus slope is unknown, never clamped");
            var tree = new CaptureCell { X = 0, Layers = CaptureLayer.Tile, TileType = 5, FrameX = 44, FrameY = 220 };
            Require(describe(tree) == null && profile.TryMirrorCell(tree, 1, out transformed, out reason) && transformed.FrameX == 66 && transformed.FrameY == 220,
                "real selected tree fragment reflects structural branch and saved random row");
            var palm = new CaptureCell { X = 0, Layers = CaptureLayer.Tile, TileType = 323, FrameX = 22, FrameY = -6 };
            Require(describe(palm) == null && profile.TryMirrorCell(palm, 1, out transformed, out reason) && transformed.FrameY == 6,
                "selected palm preserves signed source lean with opposite direction");
            Console.WriteLine("PASS Blueprint mirror profile: locked private ABI, native direction sheets and exact legacy aliases, gender/native origins, complete member proof, operating phases, selector Y90, doors, positive-object 27-slot platforms, shapes/layers and selected natural fragments. CPU/original metadata only; no GPU, real game, multiplayer or owner acceptance.");
        }

        private static CaptureObject Check(IBlueprintHorizontalMirrorProfile profile, Func<CaptureCell, BlueprintObjectShape> describe, ushort type, int width,
            int[] rows, int frameX, int templateWidth, int expectedX, int expectedY, int expectedStyle, int expectedDirection, string label)
        {
            CaptureCell[] original = Pattern(type, width, rows, frameX);
            if (type == 15) for (int i = 0; i < original.Length; i++) original[i].X = templateWidth / 2;
            CaptureObject source = Object(describe, original), target, twice;
            CaptureCell[] members, restored; string reason;
            Require(profile.TryMirrorObject(source, original, templateWidth, out target, out members, out reason), label + ": " + reason);
            Require(target.X == templateWidth - source.X - width && target.Y == source.Y && target.Width == source.Width && target.Height == source.Height &&
                target.Style == expectedStyle && target.Direction == expectedDirection && members[0].FrameX == expectedX && members[0].FrameY == expectedY,
                label + " has independently specified native counterpart");
            Require(members[0].TilePaint == original[width - 1].TilePaint && members[0].InvisibleTile == original[width - 1].InvisibleTile,
                label + " mirrors each member coating instead of only moving the object");
            Require(profile.TryMirrorObject(target, members, templateWidth, out twice, out restored, out reason) && twice.X == source.X && twice.Y == source.Y,
                label + " second reflection restores the source geometry");
            for (int i = 0; i < original.Length; i++)
                Require(restored[i].X == original[i].X && restored[i].Y == original[i].Y && restored[i].FrameX == original[i].FrameX && restored[i].FrameY == original[i].FrameY &&
                    restored[i].TilePaint == original[i].TilePaint && restored[i].InvisibleTile == original[i].InvisibleTile,
                    label + " second reflection restores raw frame and member appearance");
            return target;
        }
        private static CaptureCell[] Pattern(ushort type, int width, int[] rows, int frameX)
        {
            var result = new CaptureCell[width * rows.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new CaptureCell { X = 1 + i % width, Y = i / width, ObjectId = 17, Layers = CaptureLayer.Tile,
                TileType = type, FrameX = checked((short)(frameX + i % width * 18)), FrameY = checked((short)rows[i / width]), TilePaint = (byte)(i + 1), InvisibleTile = (i & 1) != 0 };
            return result;
        }
        private static CaptureObject Object(Func<CaptureCell, BlueprintObjectShape> describe, CaptureCell[] members)
        {
            BlueprintObjectShape shape = describe(members[0]);
            Require(shape != null && shape.Object.Reliable && shape.Object.Width * shape.Object.Height == members.Length,
                "actual capture decoder proves the complete native fixture");
            CaptureObject value = shape.Object; value.Id = members[0].ObjectId; return value;
        }
        private static void Platform(IBlueprintHorizontalMirrorProfile profile, Func<CaptureCell, BlueprintObjectShape> describe)
        {
            // Both semantic frames and renderer join-only slots are paired by
            // the locked TileFrame branch and the client's join selections.
            int[,] pairs = { { 18, 36 }, { 54, 72 }, { 108, 126 }, { 144, 180 }, { 162, 198 }, { 216, 234 }, { 270, 288 },
                { 306, 324 }, { 342, 360 }, { 378, 396 }, { 414, 432 }, { 450, 468 } };
            for (int i = 0; i < pairs.GetLength(0); i++)
            {
                CaptureCell[] source = Pattern(19, 1, new[] { 36 }, pairs[i, 0]); source[0].Slope = 1;
                CaptureObject obj = Object(describe, source), target; CaptureCell[] members; string reason;
                Require(profile.TryMirrorObject(obj, source, 4, out target, out members, out reason) && members[0].FrameX == pairs[i, 1] && members[0].FrameY == 36 && members[0].Slope == 2 && target.Id > 0,
                    "positive captured platform mirrors exact native slot " + pairs[i, 0]);
            }
        }

        private static void CaptureRoundTrip(object context, IBlueprintHorizontalMirrorProfile profile, Type captureType)
        {
            Tile[,] previous = Main.tile; int width = Main.maxTilesX, height = Main.maxTilesY, mode = Main.netMode;
            var sections = Main.sectionManager;
            string directory = Path.Combine(Terraria.Program.SavePath, "mirror-capture-" + Guid.NewGuid().ToString("N"));
            try
            {
                Main.netMode = 0; Main.maxTilesX = Main.maxTilesY = 120; Main.tile = new Tile[120, 120];
                for (int x = 0; x < 120; x++) for (int y = 0; y < 120; y++) Main.tile[x, y] = new Tile();
                // Actual right-facing .8 sheet: type349 style3, direction sheet
                // offset7. Selecting only its last member must capture all six.
                for (int row = 0; row < 3; row++) for (int col = 0; col < 2; col++)
                {
                    Tile tile = Main.tile[30 + col, 30 + row]; tile.active(true); tile.type = 349;
                    tile.frameX = (short)(108 + col * 18); tile.frameY = (short)(54 + row * 18);
                    tile.color((byte)(1 + row * 2 + col)); tile.invisibleBlock(col != 0);
                }
                Tile platform = Main.tile[34, 30]; platform.active(true); platform.type = 19; platform.frameX = 18; platform.frameY = 36;
                platform.slope(1); platform.wall = 7; platform.wallColor(4); platform.wire(true); platform.wire3(true); platform.actuator(true); platform.liquid = 71; platform.liquidType(2);
                Tile slope = Main.tile[37, 30]; slope.active(true); slope.type = 1; slope.slope(3);
                Tile tree = Main.tile[38, 32]; tree.active(true); tree.type = 5; tree.frameX = 44; tree.frameY = 220;
                var source = (IBlueprintCaptureSource)Activator.CreateInstance(captureType, F, null, new object[] { (Func<bool>)(() => true) }, null);
                var selection = new BlueprintSelection();
                foreach (var point in new[] { new[] { 31, 32 }, new[] { 34, 30 }, new[] { 37, 30 }, new[] { 38, 32 } })
                    selection.ToggleRectangle(point[0], point[1], point[0], point[1], 120, 120);
                var token = new CaptureToken(Guid.NewGuid(), 1, 1, 1); var handle = new ContentHandle(token.OperationId);
                using (var library = new LibraryState(new FileBlueprintLibrary(directory)))
                {
                    Require(Finish(library, library.TryBeginStage(token)).Status == LibraryStatus.Completed, "real mirror capture stage starts");
                    var capture = new CaptureJob(source, selection.Freeze(), 0);
                    for (int step = 0; step < 2000 && !capture.Complete && capture.Error == null; step++)
                    {
                        capture.Advance(16, 20);
                        if (capture.PendingChunk == null) continue;
                        Require(Finish(library, library.TryAppendChunk(handle, capture.PendingChunk)).Status == LibraryStatus.Completed, "real detached capture chunk enters original library worker");
                        capture.AcceptChunk();
                    }
                    Require(capture.Complete && capture.Error == null, "native source capture finishes: " + capture.Error);
                    CaptureSummary summary = capture.Summary(10);
                    Require(summary.Left == 30 && summary.Top == 30 && summary.Width == 9 && summary.Height == 3 && summary.ContentCount == 9 && summary.UnreliableObjectCount == 0,
                        "real asymmetric source keeps nonzero raw origin, complete furniture and all selected single-cell targets");
                    Require(Finish(library, library.TrySealDraft(handle, summary)).Status == LibraryStatus.Completed, "real mirror capture draft seals");
                    LibraryReceipt saved = Finish(library, library.TryCreate(handle, "镜像原版采集样本", token));
                    Require(saved.Status == LibraryStatus.Saved, "actual capture reaches reliable Saved");
                    LibraryEntry entry = Finish(library, library.TryList(1, 0, saved.AssetId)).Page.Entries.Single(e => e.AssetId == saved.AssetId);
                    BlueprintProjectionWindow raw = Open(library, entry, 1);
                    Require(raw.Cells.Count == 9 && raw.Objects.Count == 2 && raw.Objects.All(o => o.Reliable && o.Id > 0),
                        "saved asset reopens with real positive furniture and platform ObjectIds");
                    var view = new BlueprintProjectionView(raw.Summary, true, BlueprintProjectionMask.Empty, profile);
                    BlueprintProjectionWindow effective; string reason;
                    Require(view.TryApply(raw, true, out effective, out reason), "reopened asset uses production effective view: " + reason);
                    CaptureObject furniture = effective.Objects.Single(o => o.Type == 349);
                    CaptureCell[] furnitureMembers = effective.Cells.Where(c => c.ObjectId == furniture.Id).OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
                    Require(furniture.Style == 3 && furniture.Direction == 1 && furniture.X == 7 && furnitureMembers[0].FrameX == 108 && furnitureMembers[0].FrameY == 0 && furnitureMembers[0].TilePaint == 2,
                        "real saved right-facing sheet becomes legal left-facing same-style target with reflected member appearance");
                    CaptureCell platformTarget = effective.Cells.Single(c => c.TileType == 19);
                    Require(platformTarget.ObjectId > 0 && platformTarget.X == 4 && platformTarget.Slope == 2 && platformTarget.FrameX == 36 && platformTarget.FrameY == 36 &&
                        platformTarget.WallType == 7 && platformTarget.WallPaint == 4 && platformTarget.Wires == 5 && platformTarget.Actuator && platformTarget.LiquidAmount == 71 && platformTarget.LiquidKind == 2,
                        "real middle-column platform changes shape and connection while retaining every other saved layer");
                    Require(effective.Cells.Single(c => c.TileType == 5).ObjectId == 0 && effective.Cells.Single(c => c.TileType == 5).X == 0 && effective.Cells.Single(c => c.TileType == 5).FrameX == 66 &&
                        effective.Cells.Single(c => c.TileType == 1).X == 1 && effective.Cells.Single(c => c.TileType == 1).Slope == 4,
                        "reopened selected natural fragment and ordinary slope use the same view");
                    Canonical(context, furniture, furnitureMembers);
                    library.ReleaseProjection(raw.Identity);
                    BlueprintProjectionWindow reopened = Open(library, entry, 2);
                    Require(reopened.Cells.Single(c => c.TileType == 5).FrameX == 44 && reopened.Cells.Single(c => c.TileType == 19).FrameX == 18 &&
                        reopened.Cells.Where(c => c.TileType == 349).Min(c => c.FrameY) == 54,
                        "profile and effective consumer leave original saved template bytes and raw frames intact");
                }
                Console.WriteLine("PASS Blueprint mirror native capture -> detached stage -> reliable save -> authenticated open -> effective view -> canonical/member Match; asymmetric sheet, positive platform, natural fragment, ordinary slope and nonzero source origin. Isolated files/tiles only.");
            }
            finally { Main.tile = previous; Main.maxTilesX = width; Main.maxTilesY = height; Main.netMode = mode; Main.sectionManager = sections; }
        }
        private static LibraryReceipt Finish(LibraryState library, bool accepted)
        {
            Require(accepted, "mirror isolated library request accepted"); LibraryReceipt result = null;
            NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out result); });
            Require(result.Status != LibraryStatus.Failed && result.Status != LibraryStatus.CommitUnconfirmed && result.Status != LibraryStatus.Protected,
                "mirror isolated library receipt: " + result.Error);
            return result;
        }
        private static BlueprintProjectionWindow Open(LibraryState library, LibraryEntry entry, long generation)
        {
            var identity = new BlueprintProjectionIdentity(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash, 1, generation);
            Require(library.RequestProjectionOpen(identity), "mirror authenticated source open accepted");
            BlueprintProjectionReceipt result = null;
            NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeProjectionReceipt(out result); });
            Require(result.Kind == BlueprintProjectionReceiptKind.Opened, "mirror authenticated source opens: " + result.Error);
            Require(library.RequestProjectionWindow(identity, 1, new BlueprintProjectionBounds(0, 0, entry.Width, entry.Height)), "mirror full finite representative window accepted");
            NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeProjectionReceipt(out result); });
            Require(result.Kind == BlueprintProjectionReceiptKind.Window && result.Window != null, "mirror representative window arrives: " + result.Error);
            return result.Window;
        }
        private static void Canonical(object context, CaptureObject target, CaptureCell[] members)
        {
            Type matchType = Get(context, "Blueprint").GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource", true);
            object source = Activator.CreateInstance(matchType, F, null, new object[] { (Func<bool>)(() => true) }, null);
            object[] describe = { target, members[0], 60, 40, null };
            Require((bool)matchType.GetMethod("TryDescribeTarget", F).Invoke(source, describe), "mirrored saved target passes actual canonical decoder");
            var canonical = (BlueprintWorldObject)describe[4];
            var samples = new BlueprintWorldMatchSample[members.Length];
            for (int i = 0; i < members.Length; i++)
            {
                CaptureCell value = members[i]; Tile tile = Main.tile[60 + value.X, 40 + value.Y]; tile.active(true); tile.type = value.TileType;
                tile.frameX = value.FrameX; tile.frameY = value.FrameY; tile.color(value.TilePaint); tile.slope(value.Slope); tile.halfBrick(value.HalfBrick);
                tile.inActive(value.Inactive); tile.invisibleBlock(value.InvisibleTile); tile.fullbrightBlock(value.FullbrightTile);
                object[] read = { 60 + value.X, 40 + value.Y, null };
                Require((bool)matchType.GetMethod("TryRead", F).Invoke(source, read), "mirrored representative world member is read through production gate");
                samples[i] = (BlueprintWorldMatchSample)read[2];
            }
            Require(BlueprintWorldMatchComparer.CompareObject(canonical, members, samples, true) == BlueprintWorldMatchState.Match,
                "real canonical target and complete row-major members agree after save/open/mirror");
            CaptureCell[] malformed = (CaptureCell[])members.Clone(); malformed[0].FrameX += 18;
            Require(BlueprintWorldMatchComparer.CompareObject(canonical, malformed, samples, true) == BlueprintWorldMatchState.Unknown,
                "canonical member consumer still rejects an illegal raw layout");
        }
    }
}
