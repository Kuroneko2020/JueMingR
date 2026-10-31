using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // A finite actual-Capture/Host/atomic-scene lifecycle. Native world writers
    // prepare manual buildings only; this leaf never claims automatic use or
    // inventory consumption, which have separate original-consumer checks.
    internal static class NativeBlueprintSubstitutionLifecycleChecks
    {
        private const int Width = 9, Height = 2, Y = 40;

        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var shell = Get(context, "Shell");
            var originalLibrary = (LibraryState)Get(host, "Library"); var originalHistory = (BlueprintSceneHistory)Get(host, "projectionHistory");
            var preference = (PreferenceDocument<BlueprintPreferences>)Get(host, "preference");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return preference.Snapshot.IsLoaded; });
            var originalPreference = preference.Snapshot.Value;
            var originalWorld = Main.ActiveWorldFileData; bool originalMenu = Main.gameMenu, originalInventory = Main.playerInventory;
            bool originalDemand = (bool)Get(host, "materialViewDemand");
            string root = Path.Combine(Terraria.Program.SavePath, "blueprint-substitution-lifecycle-" + Guid.NewGuid().ToString("N"));
            var library = new LibraryState(new FileBlueprintLibrary(Path.Combine(root, "templates")));
            BlueprintSceneHistory history = NewHistory(root); IDisposable finite = null;
            Action step = () => { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); Call(host, "Poll"); };
            Action menu = () => { Main.gameMenu = true; Call(context, "UpdateRuntime"); };
            Action enter = () => { Main.gameMenu = false; NativeQuickItemChecks.Until(() => { step(); return (bool)Get(host, "ControlsEnabled") && (bool)Get(host, "ProjectionPersistenceReady"); }); };
            Exception primary = null;
            try
            {
                Call(shell, "CloseAndSubmitPosition"); Call(host, "SetAutoBuild", false); menu();
                finite = NativeBlueprintWorldMatchChecks.EmptyWorld(host, 512, 160);
                Set(host, "<Library>k__BackingField", library); Set(host, "projectionHistory", history);
                Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "world.wld"), false) { UniqueId = Guid.NewGuid() };
                Main.playerInventory = false; enter();
                NativeBlueprintWorldMatchChecks.InitializeMetadata(); Framing.Initialize(); Main.SetupTileMerge();
                Call(host, "SetAutoBuild", false); Call(host, "SetReplacementCategories", 4095); Call(host, "SetSameTypeReplacement", true);
                // Catalog publication requires its real consumer demand. Merely
                // polling a demand-free Host cannot qualify native metadata.
                Call(host, "SetMaterialViewDemand", true);
                NativeQuickItemChecks.Until(() => { step(); return GetOptional(host, "MaterialCatalog") is BlueprintPlacementCatalog c && c.Substitution.Ready; });
                Require(!(bool)Get(host, "AutoBuildEnabled") && (bool)Get(host, "SameTypeReplacementEnabled"), "manual lifecycle starts with substitution ON and automatic construction OFF");
                long automaticUses = (long)Get(host, "BuildUsesStarted");

                SeedSource();
                var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library, step, 20, Y, Width, Height, "同类替换生命周期小图");
                var raw = Open(library, entry);
                Require(raw.Summary.Width == Width && raw.Summary.Height == Height && raw.Cells.Count == 5 && raw.Objects.Count == 2,
                    "actual Capture/Save/Open preserves the independent nine-by-two sparse source extent");
                var chair = raw.Objects.Single(o => o.Type == TileID.Chairs);
                var platform = raw.Objects.Single(o => o.Type == TileID.Platforms);
                Require(chair.Reliable && chair.Id > 0 && chair.X == 0 && chair.Y == 0 && chair.Width == 1 && chair.Height == 2 && chair.Direction == 2 &&
                    platform.Reliable && platform.Id > 0 && platform.X == 4 && platform.Y == 1 && raw.Cells.Single(c => c.X == 4 && c.Y == 1).Slope == 1,
                    "source chair is a complete native right object and the native sloped platform has its own positive Capture identity");
                var layered = raw.Cells.Single(c => c.X == 0 && c.Y == 1);
                Require((layered.Layers & (CaptureLayer.Tile | CaptureLayer.Wall)) == (CaptureLayer.Tile | CaptureLayer.Wall) &&
                    raw.Cells.Single(c => c.X == 6 && c.Y == 0).Layers == CaptureLayer.Wall && raw.Cells.Single(c => c.X == 8 && c.Y == 1).TileType == TileID.Stone,
                    "independent source oracle includes chair-foot wall, separated wall and unbuilt exact stone sentinel");
                string assetRoot = Path.Combine(root, "templates", entry.AssetId.ToString("N")); var sourceBytes = HashFiles(assetRoot);

                foreach (int x in new[] { 100, 130, 160 }) Floor(x);
                object ordinary = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 100, Y, step);
                object mirrored = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 130, Y, step);
                object retiring = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 160, Y, step);
                mirrored = Mirror(context, host, input, mirrored, step);
                Require(Scene(host).Contains(ordinary) && Scene(host).Contains(retiring) &&
                    !(bool)Get(ordinary, "MirroredHorizontally") && (bool)Get(mirrored, "MirroredHorizontally") && !(bool)Get(retiring, "MirroredHorizontally"),
                    "actual Mirror transaction changes only its chosen empty same-template sibling");
                Bbox(ordinary, 100, false); Bbox(mirrored, 130, true);

                Manual(100, false, false); Manual(130, true, false); Manual(160, false, true);
                AwaitMatch(host, ordinary, 6, 5, step); AwaitMatch(host, mirrored, 6, 5, step);
                long retiredOrder = (long)Get(retiring, "CommitOrder");
                NativeQuickItemChecks.Until(() => { step(); return !Scene(host).Contains(retiring); });
                Require(Scene(host).Length == 2 && !(bool)Get(host, "AutoBuildEnabled"), "manually completed replacement sibling retires while two incomplete siblings remain without automatic construction");
                string ordinaryWorld = NativeBlueprintProjectionVisualChecks.Snapshot(100, Y, Width, Height);
                string mirroredWorld = NativeBlueprintProjectionVisualChecks.Snapshot(130, Y, Width, Height);
                string retiredWorld = NativeBlueprintProjectionVisualChecks.Snapshot(160, Y, Width, Height);
                var siblingMask = (BlueprintProjectionMask)Get(ordinary, "Mask");
                var baseline = (BlueprintProjectionMask)Get(mirrored, "Mask"); long beforeRevision = (long)Get(host, "projectionSceneRevision");
                long mirrorOrder = (long)Get(mirrored, "CommitOrder"), ordinaryOrder = (long)Get(ordinary, "CommitOrder");

                BeginTrim(host, input, mirrored, 138, Y, step);
                Seal(host, 138, Y); Ready(host, step);
                var preview = (BlueprintProjectionTrimPlan)Get(host, "TrimPlan");
                Require(preview.RemovedLayerCount == 2 && preview.ObjectBounds.Count == 1 && preview.ObjectBounds[0].Width == 1 && preview.ObjectBounds[0].Height == 2 &&
                    ReferenceEquals(Get(mirrored, "Mask"), baseline) && (long)Get(host, "projectionSceneRevision") == beforeRevision,
                    "first manual top-member selection expands both chair Tile members only and remains a nonpermanent preview");
                // A second disjoint completed drag adds the independent wall.
                // It must not replace the first drag or silently add the wall
                // under the chair's automatically extended bottom Tile member.
                Seal(host, 132, Y); Ready(host, step);
                preview = (BlueprintProjectionTrimPlan)Get(host, "TrimPlan");
                var objectGeometry = (BlueprintTrimGeometry)Get(host, "TrimObjectGeometry");
                var manualGeometry = (BlueprintTrimGeometry)Get(host, "TrimManualGeometry");
                Require(preview.RemovedLayerCount == 3 && objectGeometry.Contains(8, 0) && objectGeometry.Contains(8, 1) &&
                    manualGeometry.Contains(8, 0) && manualGeometry.Contains(2, 0) && !manualGeometry.Contains(8, 1) && ReferenceEquals(Get(mirrored, "Mask"), baseline),
                    "cumulative union is exactly two chair Tiles plus the independently selected wall, retaining the chair-foot wall");
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "ConfirmTrimProjection"); Call(host, "ProcessInput");
                Require((bool)Get(host, "trimFinalizing") && ReferenceEquals(Get(mirrored, "Mask"), baseline), "real confirm accepts Freeze without synchronously publishing a mask");
                NativeQuickItemChecks.Until(() => { step(); Call(host, "ProcessInput"); return !(bool)Get(host, "ProjectionEditing"); });
                var mask = (BlueprintProjectionMask)Get(mirrored, "Mask");
                Require(Scene(host).Contains(mirrored) && mask.RemovedLayerCount == 3 && mask.ObjectCount == 1 && (long)Get(host, "projectionSceneRevision") == beforeRevision + 1 &&
                    (mask.Apply(layered).Layers & CaptureLayer.Tile) == 0 && (mask.Apply(layered).Layers & CaptureLayer.Wall) != 0 &&
                    !mask.Apply(raw.Cells.Single(c => c.X == 6 && c.Y == 0)).HasContent &&
                    mask.Apply(raw.Cells.Single(c => c.X == 4 && c.Y == 1)).Slope == 1 && mask.Apply(raw.Cells.Single(c => c.X == 8 && c.Y == 1)).HasContent,
                    "one real trim publication persists SOURCE-domain whole-chair removal and the selected wall while keeping same-cell wall, platform shape and sentinel");
                Require(ReferenceEquals(Get(ordinary, "Mask"), siblingMask) && siblingMask.RemovedLayerCount == 0 && (long)Get(mirrored, "CommitOrder") == mirrorOrder,
                    "trim does not alter its normal sibling or acquire a new mirror order");
                Bbox(ordinary, 100, false); Bbox(mirrored, 130, true);
                AwaitMatch(host, ordinary, 6, 5, step); AwaitMatch(host, mirrored, 3, 2, step);
                WorldUnchanged(ordinaryWorld, mirroredWorld, retiredWorld);
                SameFiles(assetRoot, sourceBytes);

                NativeQuickItemChecks.Until(() => { step(); return history.Saved; });
                string scope = (string)Get(history, "requestedScope"), path = Path.Combine(root, "instances", scope, "scene.bin");
                byte[] saved = File.ReadAllBytes(path); var document = BlueprintSceneCodec.Decode(saved, scope, 512, 160);
                var key = new BlueprintTemplateKey(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash);
                var expected = new BlueprintSceneDocument(scope, 512, 160, document.Revision, new[] {
                    new BlueprintSceneRecord(key, 100, Y, ordinaryOrder, BlueprintProjectionMask.Empty),
                    new BlueprintSceneRecord(key, 130, Y, mirrorOrder, mask, true)
                }.OrderBy(r => r.Order).ToArray());
                Require(document.Records.Count == 2 && document.Records.All(r => r.Order != retiredOrder) && BlueprintSceneCodec.Encode(expected).SequenceEqual(saved),
                    "saved v2 bytes contain exactly approved template/order/origin/pose/source-mask facts, with no retired instance or concrete replacement plan");
                var oldOrdinaryMatch = Get(ordinary, "Match"); var oldMirrorMatch = Get(mirrored, "Match");
                long oldGeneration = ((BlueprintProjectionIdentity)Get(ordinary, "Identity")).SessionGeneration;
                // Change the global intention before leaving the live session;
                // initial restore must use that current exact policy, never the
                // old saved same-type proof or a remembered selected material.
                Call(host, "SetSameTypeReplacement", false);
                Require(GetOptional(ordinary, "Match") == null && GetOptional(mirrored, "Match") == null, "effective off command immediately revokes both surviving Match owners");
                menu(); Require(history.Stop(3000), "old scene writer ends before the new same-scope reader is constructed");
                history = NewHistory(root); Set(host, "projectionHistory", history); enter();
                NativeQuickItemChecks.Until(() => { step(); return Scene(host).Length == 2 && Scene(host).All(item => (bool)Get(item, "Opened")); });
                ordinary = Scene(host).Single(item => (long)Get(item, "CommitOrder") == ordinaryOrder);
                mirrored = Scene(host).Single(item => (long)Get(item, "CommitOrder") == mirrorOrder);
                AwaitMatch(host, ordinary, 6, 0, step); AwaitMatch(host, mirrored, 3, 0, step);
                Require(((BlueprintProjectionIdentity)Get(ordinary, "Identity")).SessionGeneration != oldGeneration &&
                    !ReferenceEquals(Get(ordinary, "Match"), oldOrdinaryMatch) && !ReferenceEquals(Get(mirrored, "Match"), oldMirrorMatch) &&
                    !(bool)Get(host, "SameTypeReplacementEnabled") && !(bool)Get(host, "AutoBuildEnabled") &&
                    ((BlueprintProjectionMask)Get(mirrored, "Mask")).RemovedLayerCount == 3 && ((BlueprintProjectionMask)Get(ordinary, "Mask")).RemovedLayerCount == 0,
                    "new real session/history owner restores only committed pose/masks and creates fresh exact-policy proof from the unchanged world");
                Bbox(ordinary, 100, false); Bbox(mirrored, 130, true);
                Call(host, "SetSameTypeReplacement", true);
                Require(GetOptional(ordinary, "Match") == null && GetOptional(mirrored, "Match") == null, "on command also retires the just-restored exact proof before rechecking");
                AwaitMatch(host, ordinary, 6, 5, step); AwaitMatch(host, mirrored, 3, 2, step);
                Require(Scene(host).Length == 2 && Scene(host).All(item => (long)Get(item, "CommitOrder") != retiredOrder) &&
                    GetOptional(host, "preparedBuild") == null && GetOptional(host, "activeBuild") == null && (long)Get(host, "BuildUsesStarted") == automaticUses,
                    "current global policy recognizes existing manual replacements with no restored concrete action or retired-sibling resurrection");
                WorldUnchanged(ordinaryWorld, mirroredWorld, retiredWorld); SameFiles(assetRoot, sourceBytes);
                Console.WriteLine("PASS Blueprint substitution lifecycle: actual 9x2 Capture with complete directional chair/sloped platform/two walls/sentinel; normal+mirrored manual replacement proofs; separate same-template manual retirement; cumulative real trim expands only chair Tile members and keeps independent same-cell wall/source bbox/sibling/source bytes; two-record atomic scene/new owner real menu reentry rebuilds off/on current Match without concrete selection or retired-instance resurrection. Manual native writers only; automatic consumption/GPU/OS/multiplayer/FPS/owner acceptance are separate evidence.");
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                Exception cleanupError = null;
                Action<Action> restore = action => {
                    try { action(); }
                    catch (Exception error) { if (cleanupError == null) cleanupError = error; Console.Error.WriteLine("SUBSTITUTION LIFECYCLE cleanup: " + error); }
                };
                restore(() => { Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelProjection"); Call(host, "SetAutoBuild", false); menu(); });
                restore(() => Require(history.Stop(3000), "isolated lifecycle scene worker stopped before its root can be released"));
                restore(() => Require(library.Stop(3000), "isolated lifecycle library worker stopped before its root can be released"));
                restore(() => { Set(host, "projectionHistory", originalHistory); Set(host, "<Library>k__BackingField", originalLibrary); });
                restore(() => finite?.Dispose());
                restore(() => { Main.ActiveWorldFileData = originalWorld; Main.playerInventory = originalInventory; Main.gameMenu = false;
                    NativeQuickItemChecks.Until(() => { step(); return (bool)Get(host, "ControlsEnabled") && (bool)Get(host, "ProjectionPersistenceReady"); }); });
                restore(() => { Call(host, "SetReplacementCategories", originalPreference.ReplacementCategories); Call(host, "SetSameTypeReplacement", originalPreference.SameTypeReplacement);
                    Call(host, "SetAutoBuild", originalPreference.AutoBuild); Call(host, "SetMaterialViewDemand", originalDemand); });
                restore(() => { if (originalMenu) menu(); });
                if (primary == null && cleanupError != null) throw new InvalidOperationException("substitution lifecycle fixture restoration failed", cleanupError);
            }
        }

        private static BlueprintSceneHistory NewHistory(string root)
        { return new BlueprintSceneHistory(scope => new AtomicFileDocument(Path.Combine(root, "instances", scope, "scene.bin"), Int32.MaxValue, true, ".schema1-original")); }
        private static Item Item(int type) { var item = new Item(); item.SetDefaults(type); return item; }
        private static void Floor(int x)
        { for (int dx = 0; dx < Width; dx++) Require(WorldGen.PlaceTile(x + dx, Y + 2, TileID.Stone, mute: true, forced: true, plr: 0), "manual source/destination has actual floor outside capture bounds"); }
        private static void SeedSource()
        {
            Floor(20);
            Require(WorldGen.PlaceObject(20, Y + 1, TileID.Chairs, mute: true, style: 0, direction: 1), "source complete right wooden chair is placed by the original writer");
            Platform(Item(ItemID.WoodPlatform), 24, Y + 1, 1);
            var wall = Item(ItemID.WoodWall); WorldGen.PlaceWall(20, Y + 1, wall.createWall, mute: true); WorldGen.PlaceWall(26, Y, wall.createWall, mute: true);
            Require(Main.tile[20, Y + 1].wall == wall.createWall && Main.tile[26, Y].wall == wall.createWall &&
                WorldGen.PlaceTile(28, Y + 1, TileID.Stone, mute: true, forced: true, plr: 0), "source contains actual walls and an independent exact sentinel");
        }
        private static void Platform(Item item, int x, int y, int slope)
        {
            Require(WorldGen.PlaceTile(x, y, item.createTile, mute: true, forced: true, plr: 0, style: item.placeStyle) && WorldGen.SlopeTile(x, y, slope),
                "manual sloped platform uses native placement and native shape writer");
            Require(Main.tile[x, y].type == item.createTile && Main.tile[x, y].frameY == item.placeStyle * 18 && Main.tile[x, y].slope() == slope,
                "native manual platform has the requested actual material and slope");
        }
        private static void Manual(int x, bool mirrored, bool sentinel)
        {
            var chair = Item(ItemID.GlassChair); int chairX = x + (mirrored ? 8 : 0);
            Require(WorldGen.PlaceObject(chairX, Y + 1, chair.createTile, mute: true, style: chair.placeStyle, direction: mirrored ? -1 : 1),
                "manual complete replacement chair uses the real direction-specific native writer");
            Platform(Item(ItemID.ObsidianPlatform), x + 4, Y + 1, mirrored ? 2 : 1);
            var wall = Item(ItemID.StoneWall); WorldGen.PlaceWall(chairX, Y + 1, wall.createWall, mute: true); WorldGen.PlaceWall(x + (mirrored ? 2 : 6), Y, wall.createWall, mute: true);
            Require(Main.tile[chairX, Y + 1].wall == wall.createWall && Main.tile[x + (mirrored ? 2 : 6), Y].wall == wall.createWall,
                "manual replacement walls are the actual safe Stone output");
            if (sentinel) Require(WorldGen.PlaceTile(x + (mirrored ? 0 : 8), Y + 1, TileID.Stone, mute: true, forced: true, plr: 0), "only the retiring sibling receives its last exact sentinel");
        }
        private static void Bbox(object item, int x, bool mirrored)
        {
            var summary = (CaptureSummary)Get(item, "Summary");
            Require((int)Get(item, "OriginX") == x && (int)Get(item, "OriginY") == Y && summary.Width == Width && summary.Height == Height && (bool)Get(item, "MirroredHorizontally") == mirrored,
                "source bbox and fixed origin remain literal across mirror/trim/restore");
        }
        private static object Mirror(object context, object host, object input, object item, Action step)
        {
            NativeBlueprintWorldMatchChecks.AwaitMovable(context, host, item); Window(host, item, step);
            var identity = (BlueprintProjectionIdentity)Get(item, "Identity");
            long order = (long)Get(item, "CommitOrder"), revision = (long)Get(host, "projectionSceneRevision");
            int count = Scene(host).Length;
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            Require(Scene(host).Contains(item) && !(bool)Get(item, "MirroredHorizontally") && !(bool)Get(host, "ProjectionEditing") && (bool)Get(input, "CanStartActions"),
                "mirror starts from its current movable normal scene owner with trusted neutral input: " + MirrorState(host));
            Call(host, "MirrorProjection");
            Require(Get(host, "ProjectionMode").ToString() == "SelectingMirror", "real mirror request enters selection: " + MirrorState(host));
            Pointer(host, false, 130, Y); Pointer(host, true, 130, Y);
            NativeQuickItemChecks.Until(() => { step(); return !(bool)Get(host, "ProjectionEditing"); });
            // Mirror atomically removes/disposes the original placement and
            // publishes a Copy with the same identity and a new cover order.
            // Following the old reference would read its intentionally old pose.
            var scene = Scene(host); var current = scene.Where(value => identity.Equals((BlueprintProjectionIdentity)Get(value, "Identity"))).ToArray();
            Require(current.Length == 1, "mirror retains exactly one current owner of its complete identity: " + MirrorState(host));
            var result = current[0];
            Require(!scene.Contains(item) && !ReferenceEquals(result, item) && !(bool)Get(item, "MirroredHorizontally") &&
                (bool)Get(result, "MirroredHorizontally") && (long)Get(result, "CommitOrder") > order &&
                scene.Length == count && (long)Get(host, "projectionSceneRevision") == revision + 1,
                "one real mirror commit replaces only its old owner and publishes one scene revision; oldOrder=" + order + " oldRevision=" + revision + ": " + MirrorState(host));
            return result;
        }
        private static string MirrorState(object host)
        { return "mode=" + Get(host, "ProjectionMode") + " feedback=" + (GetOptional(host, "projectionFeedback") ?? "<none>") + " revision=" + Get(host, "projectionSceneRevision") +
            " scene=[" + string.Join(";", Scene(host).Select(item => ((BlueprintProjectionIdentity)Get(item, "Identity")).ProjectionGeneration +
                "@" + Get(item, "OriginX") + "," + Get(item, "OriginY") + " mirrored=" + Get(item, "MirroredHorizontally") + " order=" + Get(item, "CommitOrder"))) + "]"; }
        private static void BeginTrim(object host, object input, object item, int x, int y, Action step)
        {
            Window(host, item, step); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "TrimProjection"); Pointer(host, false, x, y); Pointer(host, true, x, y);
            NativeQuickItemChecks.Until(() => { step(); return Get(host, "ProjectionMode").ToString() == "TrimReady"; }); Pointer(host, false, x, y);
        }
        private static void Seal(object host, int x, int y) { Pointer(host, true, x, y); Pointer(host, false, x, y); }
        private static void Ready(object host, Action step) { NativeQuickItemChecks.Until(() => { step(); return (bool)Get(host, "CanInvokeConfirmTrim"); }); }
        private static void Pointer(object host, bool down, int x, int y) { Call(host, "AcceptWorldPointer", true, down, false, x, y, Main.maxTilesX, Main.maxTilesY); }
        private static void Window(object host, object item, Action step)
        { NativeQuickItemChecks.Until(() => { Call(host, "UpdateProjectionWindow", new WorldTargetView((int)Get(item, "OriginX"), Y, Width, Height, 0)); step(); return GetOptional(item, "Window") != null; }); }
        private static void AwaitMatch(object host, object item, long targets, long matches, Action step)
        {
            try
            {
                NativeQuickItemChecks.Until(() => {
                    step(); var value = GetOptional(item, "Match");
                    return Scene(host).Contains(item) && (bool)Call(host, "ProjectionQualificationCurrent", item) && value != null &&
                        (int)Get(value, "Current") == ((Array)Get(value, "Ranges")).Length && (long)Get(value, "Unknown") == 0 &&
                        (long)Get(value, "Targets") == targets && (long)Get(value, "Matches") == matches;
                });
            }
            catch
            {
                var value = GetOptional(item, "Match"); Console.Error.WriteLine("SUBSTITUTION LIFECYCLE match origin=" + Get(item, "OriginX") +
                    " expected=" + matches + "/" + targets + " actual=" + (value == null ? "none" : Get(value, "Matches") + "/" + Get(value, "Targets") +
                    " unknown=" + Get(value, "Unknown") + " current=" + Get(value, "Current") + "/" + ((Array)Get(value, "Ranges")).Length)); throw;
            }
        }
        private static object[] Scene(object host) { return ((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().ToArray(); }
        private static BlueprintProjectionWindow Open(LibraryState library, LibraryEntry entry)
        {
            var identity = new BlueprintProjectionIdentity(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash, 91009, 1);
            Require(library.RequestProjectionOpen(identity), "actual lifecycle asset enters the projection reader");
            try
            {
                Receipt(library, identity, BlueprintProjectionReceiptKind.Opened);
                Require(library.RequestProjectionWindow(identity, 1, new BlueprintProjectionBounds(0, 0, Width, Height)), "actual lifecycle asset requests its bounded raw source");
                return Receipt(library, identity, BlueprintProjectionReceiptKind.Window).Window;
            }
            finally { library.ReleaseProjection(identity); }
        }
        private static BlueprintProjectionReceipt Receipt(LibraryState library, BlueprintProjectionIdentity identity, BlueprintProjectionReceiptKind kind)
        {
            BlueprintProjectionReceipt result = null;
            NativeQuickItemChecks.Until(() => {
                BlueprintProjectionReceipt value; if (!library.TryTakeProjectionReceipt(out value) || !identity.Equals(value.Identity)) return false;
                Require(value.Kind != BlueprintProjectionReceiptKind.Failed, "actual lifecycle source stays reliable: " + value.FailureCode);
                if (value.Kind != kind) return false; result = value; return true;
            });
            Require(result != null && (kind != BlueprintProjectionReceiptKind.Window || result.Window != null), "only a complete actual lifecycle receipt is used"); return result;
        }
        private static Dictionary<string, string> HashFiles(string asset)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(asset, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".lock", StringComparison.Ordinal)))
                using (var sha = SHA256.Create()) result.Add(path.Substring(asset.Length), Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path))));
            Require(result.Count > 0, "the isolated real asset has retained source files"); return result;
        }
        private static void SameFiles(string asset, Dictionary<string, string> expected)
        { var actual = HashFiles(asset); Require(actual.Count == expected.Count && expected.All(p => actual.TryGetValue(p.Key, out string hash) && hash == p.Value), "mirror/substitution/trim/restore never mutate actual saved metadata/content bytes"); }
        private static void WorldUnchanged(string normal, string mirrored, string retired)
        {
            Require(NativeBlueprintProjectionVisualChecks.Snapshot(100, Y, Width, Height) == normal && NativeBlueprintProjectionVisualChecks.Snapshot(130, Y, Width, Height) == mirrored &&
                NativeBlueprintProjectionVisualChecks.Snapshot(160, Y, Width, Height) == retired, "trim, policy changes and scene reentry preserve all actual manual buildings, including the retired sibling");
        }
    }
}
