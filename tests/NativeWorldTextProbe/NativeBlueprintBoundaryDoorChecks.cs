using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintBoundaryDoorChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint");
            var input = Get(context, "Input");
            var oldLibrary = (LibraryState)Get(host, "Library");
            var oldHistory = (BlueprintSceneHistory)Get(host, "projectionHistory");
            var oldWorld = Main.ActiveWorldFileData;
            bool oldMenu = Main.gameMenu, oldAuto = (bool)Get(host, "AutoBuildEnabled");
            string root = Path.Combine(Terraria.Program.SavePath, "blueprint-boundary-door-" + Guid.NewGuid().ToString("N"));
            var library = new LibraryState(new FileBlueprintLibrary(Path.Combine(root, "templates")));
            var history = new BlueprintSceneHistory(scope => new AtomicFileDocument(Path.Combine(root, "instances", scope, "scene.bin"), int.MaxValue, true));
            Action step = () =>
            {
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                NativeQuickItemChecks.BeginWorldStep();
                Call(context, "UpdateRuntime");
                Call(host, "Poll");
            };
            Main.gameMenu = true;
            Call(context, "UpdateRuntime");
            var world = NativeBlueprintWorldMatchChecks.EmptyWorld(host, 512, 160);
            try
            {
                Set(host, "<Library>k__BackingField", library);
                Set(host, "projectionHistory", history);
                Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "world.wld"), false) { UniqueId = Guid.NewGuid() };
                Main.gameMenu = false;
                NativeQuickItemChecks.Until(() => { step(); return (bool)Get(host, "ProjectionPersistenceReady"); });
                Framing.Initialize();
                Main.SetupTileMerge();
                Call(host, "SetAutoBuild", false);
                Call(Get(context, "Shell"), "CloseAndSubmitPosition");
                var entry = CaptureDoor(host, library);
                var independent = NativeBlueprintBudgetChecks.Save(library, new[] { new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone } }, 1, 1);
                var first = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 100, 60, step);
                var mirror = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 130, 60, step);
                var other = NativeBlueprintWorldMatchChecks.Place(host, input, independent, 160, 60, step);
                var a = (BlueprintProjectionIdentity)Get(first, "Identity");
                var b = (BlueprintProjectionIdentity)Get(mirror, "Identity");
                var c = (BlueprintProjectionIdentity)Get(other, "Identity");
                NativeQuickItemChecks.Until(() => { step(); return MatchComplete(first) && MatchComplete(mirror) && MatchComplete(other) && history.Saved; });
                Call(host, "UpdateProjectionWindow", new WorldTargetView(130, 60, 16, 16, 0));
                NativeQuickItemChecks.Until(() => { step(); return GetOptional(mirror, "Window") != null; });
                Call(host, "MirrorProjection");
                Call(host, "AcceptWorldPointer", true, false, false, 130, 60, Main.maxTilesX, Main.maxTilesY);
                Call(host, "AcceptWorldPointer", true, true, false, 130, 60, Main.maxTilesX, Main.maxTilesY);
                NativeQuickItemChecks.Until(() =>
                {
                    step();
                    var current = Find(host, b);
                    return current == null || !(bool)Get(host, "ProjectionEditing") && (bool)Get(current, "MirroredHorizontally") && MatchComplete(current);
                });
                bool keepsA = Find(host, a) != null, keepsB = Find(host, b) != null, keepsC = Find(host, c) != null;
                Console.WriteLine("BOUNDARY_DOOR_HOST raw=40,40 size=1x3 expectedContext=-1..1 ordinaryA=" + keepsA + " mirrorB=" + keepsB +
                    " otherC=" + keepsC + " retainedA=" + library.RetainsProjection(a) + " retainedB=" + library.RetainsProjection(b) +
                    " retainedC=" + library.RetainsProjection(c) + " protected=" + history.Protected + " feedback=" + GetOptional(host, "projectionFeedback"));
                Require(keepsA && keepsB && keepsC, "a native edge door remains matchable after mirroring without revoking either healthy sibling");
                NativeQuickItemChecks.Until(() => { step(); return history.Saved; });
                var placed = Find(host, b);
                long order = (long)Get(placed, "CommitOrder");
                Main.gameMenu = true;
                Call(context, "UpdateRuntime");
                Main.gameMenu = false;
                NativeQuickItemChecks.Until(() =>
                {
                    step();
                    var restored = Scene(host).SingleOrDefault(item => (long)Get(item, "CommitOrder") == order);
                    return restored != null && (bool)Get(restored, "MirroredHorizontally") && MatchComplete(restored);
                });
                Console.WriteLine("PASS native boundary door original placement/capture, ordinary match, mirror, healthy aliases, reliable save and same-scope matched reentry.");
                VerifyHostSourceFailure(host, library, history, order, step);
                VerifyProducerIndependentProof(host, input, library, step);
                VerifyOriginalWorldRecheck(host, input, library, step);
                VerifyConstructionWitness(host, step);
                using (var isolated = NativeBlueprintHelperImportChecks.State(Path.Combine(root, "source"), Path.Combine(root, "source-counter.json")))
                {
                    var profile = (IBlueprintHorizontalMirrorProfile)host.GetType().Assembly
                        .GetType("JueMingR.TerrariaHost.Blueprint.BlueprintHorizontalMirrorResolver", true).GetMethod("Create", F).Invoke(null, null);
                    VerifySourceContexts(host, isolated, profile);
                    VerifyNormalizedImport(isolated, profile, root);
                    VerifySourceFailures(host, isolated, profile, Path.Combine(root, "source"));
                }
            }
            finally
            {
                Call(host, "SetAutoBuild", oldAuto);
                Main.gameMenu = true;
                Call(context, "UpdateRuntime");
                history.Stop(3000);
                library.Stop(3000);
                Set(host, "<Library>k__BackingField", oldLibrary);
                Set(host, "projectionHistory", oldHistory);
                world.Dispose();
                Main.ActiveWorldFileData = oldWorld;
                Main.gameMenu = oldMenu;
                if (!oldMenu) step();
            }
        }

        private static LibraryEntry CaptureDoor(object host, LibraryState library, int hinge = 40, int direction = 0, int left = 40, int width = 1)
        {
            for (int x = left - 1; x <= left + width; x++)
                for (int y = 39; y <= 43; y++) Main.tile[x, y] = new Tile();
            SetAnchor(hinge, 39);
            SetAnchor(hinge, 43);
            Require(WorldGen.PlaceDoor(hinge, 41, TileID.ClosedDoor), "original PlaceDoor creates a complete anchored three-row door");
            if (direction != 0) Require(WorldGen.OpenDoor(hinge, 41, direction), "original OpenDoor supplies the requested occupied wing");
            Main.tile[hinge, 40].wall = WallID.Stone;
            if (hinge != left) Main.tile[left, 40].wall = WallID.Wood;
            if (hinge != left + width - 1) Main.tile[left + width - 1, 40].wall = WallID.Wood;
            var source = (IBlueprintCaptureSource)Activator.CreateInstance(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true), F,
                null, new object[] { (Func<bool>)(() => true) }, null);
            var selection = new BlueprintSelection();
            selection.ToggleRectangle(left, 40, left + width - 1, 42, Main.maxTilesX, Main.maxTilesY);
            var job = new CaptureJob(source, selection.Freeze(), 0);
            var token = new CaptureToken(Guid.NewGuid(), 7, 1, 1);
            var handle = new ContentHandle(token.OperationId);
            Finish(library, library.TryBeginStage(token), LibraryStatus.Completed);
            for (int n = 0; n < 100 && !job.Complete && job.Error == null; n++)
            {
                job.Advance(64, 20);
                if (job.PendingChunk == null) continue;
                Finish(library, library.TryAppendChunk(handle, job.PendingChunk), LibraryStatus.Completed);
                job.AcceptChunk();
            }
            Require(job.Complete && job.Error == null, "native edge capture completes");
            var summary = job.Summary(10);
            Require(summary.Left == left && summary.Top == 40 && summary.Width == width && summary.Height == 3 && summary.UnreliableObjectCount == 0,
                "nonzero source origin and literal edge footprint survive capture");
            Finish(library, library.TrySealDraft(handle, summary), LibraryStatus.Completed);
            var saved = Finish(library, library.TryCreate(handle, "边界门" + hinge + "/" + direction, token), LibraryStatus.Saved);
            Require(library.TryList(80171, 0, saved.AssetId), "saved edge template is listed");
            return NativeBlueprintLibraryWait.AcceptedList(library, "native edge template").Page.Entries.Single(e => e.AssetId == saved.AssetId);
        }

        private static void VerifySourceContexts(object host, LibraryState library, IBlueprintHorizontalMirrorProfile profile)
        {
            // Source hinge 0/2 is on an actual declared edge. The middle
            // comparison has both physical wings inside the same width.
            foreach (var row in new[] { new[] { 0, 0 }, new[] { 2, 0 }, new[] { 0, 1 }, new[] { 2, -1 }, new[] { 1, 1 } })
            {
                int hinge = row[0], direction = row[1];
                var entry = CaptureDoor(host, library, 50 + hinge, direction, 50, 3);
                var opened = Open(library, entry, 1);
                int wall = Enumerable.Range(0, opened.MatchRanges.Count).First(i => opened.MatchRanges[i].ObjectId == 0);
                int door = opened.MatchRanges.Single(r => r.ObjectId > 0).ObjectId;
                var raw = Source(library, opened, 11, wall, null).Parts.Single().DoorContexts.Single();
                Require(raw.Cells.Count == 9 && raw.Bounds.Left == hinge - 1, "standalone wall source independently proves the actual hinge union");
                if (hinge == 0)
                {
                    var unproven = new BlueprintProjectionWindow(raw.Identity, raw.WindowGeneration, raw.Summary, raw.Bounds, raw.Cells.ToArray(), raw.Objects.ToArray());
                    BlueprintProjectionWindow invalid; string reason;
                    Require(!new BlueprintProjectionView(opened.Summary, true, BlueprintProjectionMask.Empty, profile).TryApply(unproven, true, out invalid, out reason, keepEmpty: true),
                        "the same outside coordinates without a validated worker door query cannot be asserted as context air");
                }
                foreach (bool masked in new[] { false, true })
                {
                    long members = direction == 0 ? 3 : 6;
                    var mask = masked ? (BlueprintProjectionMask)typeof(BlueprintProjectionMask).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
                        .Invoke(new object[] { new Dictionary<long, ushort>(), new HashSet<int> { door }, members, members }) : BlueprintProjectionMask.Empty;
                    var view = new BlueprintProjectionView(opened.Summary, true, mask, profile);
                    var receipt = Source(library, opened, masked ? 13 : 12, wall, view);
                    var part = receipt.Parts.Single();
                    var context = part.DoorContexts.Single();
                    int reflectedHinge = 2 - hinge;
                    Require(part.LogicalObjectId == 0 && context.Bounds.Left == reflectedHinge - 1 && context.Cells.Count == 9 &&
                        context.Cells.Where((cell, i) => cell.X == reflectedHinge - 1 + i % 3 && cell.Y == i / 3).Count() == 9,
                        "reflection keeps all nine exact row-major cells on a wall-only demand");
                    Require(context.Cells.Where(c => c.X < 0 || c.X >= 3).All(c => !c.HasContent) &&
                        context.Cells.Single(c => c.X == reflectedHinge && c.Y == 0).WallType == WallID.Stone,
                        "only attested out-of-template air reflects outside the declared width; the hinge wall remains");
                    Require(context.Cells.Count(c => (c.Layers & CaptureLayer.Tile) != 0) == (masked ? 0 : members),
                        "removing the whole source door does not resurrect Tile or erase its independent wall");
                    if (!masked)
                    {
                        var obj = context.Objects.Single();
                        Require(obj.Type == (direction == 0 ? 10 : 11) && obj.X == reflectedHinge - (direction > 0 ? 1 : 0) &&
                            (direction == 0 || obj.Direction == (direction > 0 ? 1 : 2)), "effective mirror preserves exact object kind and reverses only the occupied wing");
                    }
                    else Require(context.Objects.Count == 0, "masked door has no effective object metadata");
                    BlueprintProjectionWindow shared; string reason;
                    Require(view.TryApply(raw, true, out shared, out reason, keepEmpty: true) &&
                        shared.Cells.Select(c => (c.X, c.Y, c.Layers)).SequenceEqual(context.Cells.Select(c => (c.X, c.Y, c.Layers))),
                        "shared keepEmpty transform obeys the same coordinate/air/mask rule: " + reason);
                }
                library.ReleaseProjection(opened.Identity);
            }
            Console.WriteLine("PASS real wall-only source has all nine row-major cells for closed left/right edges, open left/right wings, interior hinge, and a whole-door source mask.");
        }

        private static void VerifyNormalizedImport(LibraryState library, IBlueprintHorizontalMirrorProfile profile, string root)
        {
            SetAnchor(70, 39);
            SetAnchor(70, 43);
            Require(WorldGen.PlaceDoor(70, 41, TileID.ClosedDoor), "original writer supplies the external template frames");
            string cells = String.Join(",", Enumerable.Range(0, 3).Select(row => String.Format(
                "{{\"x\":0,\"y\":{0},\"tileType\":10,\"frameX\":{1},\"frameY\":{2}}}", row, Main.tile[70, 40 + row].frameX, Main.tile[70, 40 + row].frameY)));
            string path = Path.Combine(root, "normalized-door.json");
            File.WriteAllText(path, "{\"name\":\"归一坐标门\",\"width\":1,\"height\":3,\"tiles\":[" + cells + "],\"walls\":[{\"x\":0,\"y\":0,\"wallType\":1}]}");
            var imported = NativeBlueprintHelperImportChecks.Import(library, (IBlueprintImportProfile)profile, path);
            Require(imported.Status == BlueprintExchangeStatus.Completed, "original door frames routed through the real helper import: " + imported.Message);
            Require(library.TryList(80172, 0, imported.AssetId), "real normalized asset available in library");
            var entry = NativeBlueprintLibraryWait.AcceptedList(library, "normalized helper door").Page.Entries.Single(e => e.AssetId == imported.AssetId);
            var opened = Open(library, entry, 4);
            int wall = Enumerable.Range(0, opened.MatchRanges.Count).Single(i => opened.MatchRanges[i].ObjectId == 0);
            var context = Source(library, opened, 14, wall, new BlueprintProjectionView(opened.Summary, true, BlueprintProjectionMask.Empty, profile))
                .Parts.Single().DoorContexts.Single();
            Require(opened.Summary.Left == 0 && opened.Summary.Top == 0 && opened.Summary.Width == 1 &&
                context.Cells.Count == 9 && context.Cells.Where(c => c.X != 0).All(c => !c.HasContent) &&
                context.Cells.Single(c => c.X == 0 && c.Y == 0).WallType == WallID.Stone, "normalized Helper coordinates reach identical exact wall context");
            library.ReleaseProjection(opened.Identity);
            Console.WriteLine("PASS normalized Helper import generated from native door frames reaches standalone mirrored wall context.");
        }

        private static void VerifyProducerIndependentProof(object host, object input, LibraryState library, Action step)
        {
            // The native catalog deliberately cannot prove every conversion
            // and projectile producer for Magical Ice. Its ordinary world Tile
            // shape is nevertheless known, and the destination is empty.
            var entry = NativeBlueprintBudgetChecks.Save(library, new[] { new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.MagicalIceBlock } }, 1, 1);
            var target = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 210, 60, step);
            var id = (BlueprintProjectionIdentity)Get(target, "Identity");
            NativeQuickItemChecks.Until(() =>
            {
                Call(host, "UpdateProjectionWindow", new WorldTargetView(210, 60, 16, 16, 0));
                step();
                var state = GetOptional(target, "Match");
                return GetOptional(target, "Window") != null && state != null && (int)Get(state, "Current") == ((Array)Get(state, "Ranges")).Length;
            });
            var qualification = (BlueprintProjectionQualificationReceipt)Get(target, "Qualification");
            Console.WriteLine("PRODUCER_INDEPENDENCE targets=" + qualification.EffectiveRequiredLayerCount + " producerUnknown=" + qualification.EffectiveUnknownLayerCount +
                " matchUnknown=" + Get(Get(target, "Match"), "Unknown") + " editProof=" + Call(host, "ProjectionMoveEligibility", target));
            Require(qualification.EffectiveUnknownLayerCount == 1 && (long)Get(Get(target, "Match"), "Unknown") == 1 &&
                Call(host, "ProjectionMoveEligibility", target).ToString() == "Movable", "stable missing producer leaves honest construction Unknown and independently proves the original world is empty");
            Call(host, "MirrorProjection");
            Call(host, "AcceptWorldPointer", true, false, false, 210, 60, Main.maxTilesX, Main.maxTilesY);
            Call(host, "AcceptWorldPointer", true, true, false, 210, 60, Main.maxTilesX, Main.maxTilesY);
            NativeQuickItemChecks.Until(() =>
            {
                step();
                var mirrored = Find(host, id);
                var proof = mirrored != null ? GetOptional(mirrored, "Qualification") as BlueprintProjectionQualificationReceipt : null;
                return mirrored != null && (bool)Get(mirrored, "MirroredHorizontally") && proof?.EffectiveUnknownLayerCount == 1;
            });
            Console.WriteLine("PASS original Magical Ice producer remains unresolved while its independently proven empty instance can mirror.");
        }

        private static void VerifyHostSourceFailure(object host, LibraryState library, BlueprintSceneHistory history, long order, Action step)
        {
            var target = Scene(host).Single(item => (long)Get(item, "CommitOrder") == order);
            var profile = (IBlueprintHorizontalMirrorProfile)Get(target, "MirrorProfile");
            var identity = (BlueprintProjectionIdentity)Get(target, "Identity");
            var worker = Get(library, "worker");
            var fault = new FaultProfile(profile) { Reject = () =>
            {
                var active = GetOptional(worker, "projectionSourceActive");
                return active != null && identity.Equals(Get(active, "Identity"));
            } };
            Set(target, "MirrorProfile", fault);
            NativeQuickItemChecks.Until(() => { step(); return GetOptional(target, "SourceFailureView") != null; });
            Require(Scene(host).Length == 3 && Scene(host).Contains(target) && !history.Protected &&
                library.RetainsProjection(identity) && GetOptional(target, "Match") == null,
                "Host consumes an exact derived source failure, releases Match and keeps the complete archive plus healthy aliases");
            var accepted = fault.Rejections;
            for (int i = 0; i < 32; i++) step();
            Require(fault.Rejections == accepted, "stable failed view cannot repeatedly occupy a source slot");
            Set(target, "MirrorProfile", profile);
            NativeQuickItemChecks.Until(() => { step(); return MatchComplete(target); });
            Console.WriteLine("PASS Host scopes away current derived source only, settles stable ticks, and accepts retry after profile replacement.");
        }

        private static void VerifyOriginalWorldRecheck(object host, object input, LibraryState library, Action step)
        {
            var entry = NativeBlueprintBudgetChecks.Save(library, new[]
            {
                new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone },
                new CaptureCell { X = 1, Layers = CaptureLayer.Wire, Wires = 1 }
            }, 2, 1);
            var target = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 240, 60, step);
            NativeQuickItemChecks.Until(() =>
            {
                Call(host, "UpdateProjectionWindow", new WorldTargetView(240, 60, 16, 16, 0));
                step();
                return MatchComplete(target) && GetOptional(target, "Window") != null;
            });
            long originalScene = (long)Get(host, "projectionSceneRevision"), originalOrder = (long)Get(target, "CommitOrder");
            var mask = Get(target, "Mask");
            var worker = Get(library, "worker");
            Call(host, "MirrorProjection");
            Call(host, "AcceptWorldPointer", true, false, false, 240, 60, Main.maxTilesX, Main.maxTilesY);
            Call(host, "AcceptWorldPointer", true, true, false, 240, 60, Main.maxTilesX, Main.maxTilesY);
            Require(Get(host, "ProjectionMode").ToString() == "Committing", "exact original empty proof admitted the opposite pose");
            NativeQuickItemChecks.Until(() => { lock (Get(worker, "gate")) return GetOptional(worker, "projectionCommitReceipt") != null; });
            Require(WorldGen.PlaceTile(240, 60, TileID.Stone, true, true) && !Main.tile[241, 60].active(),
                "native write creates an original-pose match while the mirrored Tile location remains empty");
            NativeQuickItemChecks.Until(() => { step(); return !(bool)Get(host, "ProjectionEditing"); });
            Require(Scene(host).Contains(target) && !(bool)Get(target, "MirroredHorizontally") &&
                (long)Get(target, "CommitOrder") == originalOrder && (long)Get(host, "projectionSceneRevision") == originalScene &&
                ReferenceEquals(mask, Get(target, "Mask")), "unconsumed candidate is cancelled at original world stamp change with exact scene, pose, order and mask");
            Console.WriteLine("PASS candidate receipt cannot borrow the empty opposite coordinates after a native write to the original.");
        }

        private static void VerifyConstructionWitness(object host, Action step)
        {
            var original = Scene(host).Single(item => (int)Get(item, "OriginX") == 100);
            SetAnchor(100, 59);
            SetAnchor(100, 63);
            Require(WorldGen.PlaceDoor(100, 61, TileID.ClosedDoor) && Main.tile[100, 60].wall == 0,
                "original writer places its complete native door while the independently required wall stays missing");
            NativeQuickItemChecks.Until(() => { step(); return DoorWitnesses(host, original) == 1; });
            Require(MatchComplete(original) && (long)Get(Get(original, "Match"), "Matches") == 3,
                "qualified complete door establishes the three Tile matches and their original write witness");

            var native = Get(Get(host, "materialKnowledge"), "Native");
            var current = (BlueprintPlacementCatalog)Get(host, "MaterialCatalog");
            var unresolved = new BlueprintPlacementCatalog(current.Revision + 1, Array.Empty<BlueprintPlacementRule>(),
                new Dictionary<int, BlueprintPlacementDescriptor>(), new Dictionary<int, int>(), new Dictionary<int, int>(),
                0, 0, targetVersion: current.TargetVersion, substitution: current.Substitution);
            try
            {
                Set(native, "materialCatalog", unresolved);
                NativeQuickItemChecks.Until(() =>
                {
                    step();
                    var q = GetOptional(original, "Qualification") as BlueprintProjectionQualificationReceipt;
                    var match = GetOptional(original, "Match");
                    return ReferenceEquals(q?.Catalog, unresolved) && match != null &&
                        (int)Get(match, "Current") == ((Array)Get(match, "Ranges")).Length;
                });
                Require(DoorWitnesses(host, original) == 0 && Call(host, "ProjectionMoveEligibility", original).ToString() == "Matched" &&
                    (long)Get(Get(original, "Match"), "Unknown") == 4,
                    "the same actual door still blocks movement but publishes neither a construction match nor a write witness for an unresolved producer");
            }
            finally { Set(native, "materialCatalog", current); }
            Console.WriteLine("PASS qualified original closed door publishes its write witness; same world under unresolved producer proves built content only.");
        }

        private static int DoorWitnesses(object host, object placement)
        {
            var state = GetOptional(placement, "Match");
            if (state == null) return 0;
            var objectPart = ((Array)Get(state, "Ranges")).Cast<object>().First(range => ((BlueprintProjectionSourceRange)Get(range, "Source")).ObjectId > 0);
            var observer = Get(host, "projectionChanges");
            var watches = (IDictionary)Get(observer, "watches");
            lock (Get(observer, "gate"))
            {
                var watch = watches[Get(objectPart, "Watch")];
                return watch == null ? 0 : (int)Get(watch, "DoorCount");
            }
        }

        private static void VerifySourceFailures(object host, LibraryState library, IBlueprintHorizontalMirrorProfile profile, string templatePath)
        {
            var door = CaptureDoor(host, library, 64, 0, 64);
            var other = NativeBlueprintBudgetChecks.Save(library, new[] { new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone } }, 1, 1);
            var a = Open(library, door, 1);
            var b = Open(library, door, 2);
            var c = Open(library, other, 3);
            int range = Enumerable.Range(0, b.MatchRanges.Count).Single(i => b.MatchRanges[i].ObjectId > 0);
            var fault = new FaultProfile(profile);
            var view = new BlueprintProjectionView(b.Summary, true, BlueprintProjectionMask.Empty, fault);
            var rejected = Source(library, b, 21, range, view);
            Require(rejected.FailureScope == BlueprintProjectionFailureScope.DerivedView && rejected.FailureStage == BlueprintProjectionFailureStage.SourceTransform &&
                rejected.FailureDetail.Contains("probe-source-transform") && rejected.Parts.Count == 0 &&
                library.RetainsProjection(a.Identity) && library.RetainsProjection(b.Identity) && library.RetainsProjection(c.Identity),
                "a derived B error ends only that request and cannot publish fake empty coverage or revoke A/C");
            fault.Budget = true;
            var budget = Source(library, b, 22, range, view);
            Require(budget.FailureScope == BlueprintProjectionFailureScope.Demand && budget.Parts.Count == 0 &&
                library.RetainsProjection(a.Identity) && library.RetainsProjection(b.Identity), "typed source budget failure is demand-level");

            using (var entered = new ManualResetEventSlim())
            using (var continueOld = new ManualResetEventSlim())
            {
                fault.Entered = entered; fault.Continue = continueOld;
                Require(library.RequestProjectionSource(b.Identity, 23, range, view: view), "the old source is accepted");
                try
                {
                    Require(entered.Wait(5000), "old request entered transform after its last exact worker checkpoint");
                    library.ClearProjectionSource(b.Identity);
                    Require(library.RequestProjectionSource(b.Identity, 24, range, view: new BlueprintProjectionView(b.Summary, true, BlueprintProjectionMask.Empty, profile)),
                        "replacement view accepted before the old transform throws");
                }
                finally { continueOld.Set(); }
                BlueprintProjectionSourceReceipt receipt = null;
                NativeQuickItemChecks.Until(() => library.TryTakeProjectionSource(out receipt));
                Require(receipt.Generation == 24 && receipt.FailureScope == BlueprintProjectionFailureScope.None && receipt.Parts.Count == 1,
                    "stale exception neither published failure nor erased the exact successor");
            }

            // Remove only the verified test worker's decoded block cache.
            // The checked on-disk index stays unchanged when the copied chunk
            // byte is toggled, making chunk hash mismatch the independent cause.
            var worker = Get(library, "worker");
            lock (Get(worker, "gate")) { ((IList)Get(worker, "projectionCache")).Clear(); Set(worker, "projectionCacheSize", 0L); }
            string chunk = Directory.EnumerateFiles(Path.Combine(templatePath, door.AssetId.ToString("N"), "content", "chunks")).First();
            byte[] bytes = File.ReadAllBytes(chunk);
            bytes[bytes.Length - 1] ^= 1;
            File.WriteAllBytes(chunk, bytes);
            Require(library.RequestProjectionSource(b.Identity, 25, range), "worker accepts a demand before discovering the actual changed hash");
            for (int i = 0; i < 2; i++)
            {
                BlueprintProjectionReceipt receipt = null;
                NativeQuickItemChecks.Until(() => library.TryTakeProjectionReceipt(out receipt));
                Require(receipt.Identity.TemplateKey.Equals(b.Identity.TemplateKey) && receipt.IdentityRetired &&
                    receipt.FailureScope == BlueprintProjectionFailureScope.SharedAsset && receipt.FailureCode == "blueprint-chunk-hash",
                    "actual chunk hash failure retires only references bound to the same immutable asset body");
            }
            Require(!library.RetainsProjection(a.Identity) && !library.RetainsProjection(b.Identity) && library.RetainsProjection(c.Identity) &&
                Source(library, c, 26, 0, null).Parts.Count == 1, "independent template keeps its normal source after shared hash failure");
            Console.WriteLine("PASS derived/Demand scope, cancellation after the last pre-failure gate and replacement publication, actual raw hash failure only for the shared A/B template.");
        }

        private static BlueprintProjectionReceipt Open(LibraryState library, LibraryEntry entry, long instance)
        {
            var id = new BlueprintProjectionIdentity(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash, 23, instance);
            Require(library.RequestProjectionOpen(id), "isolated worker accepts projection open");
            BlueprintProjectionReceipt receipt = null;
            NativeQuickItemChecks.Until(() => library.TryTakeProjectionReceipt(out receipt));
            Require(receipt.Kind == BlueprintProjectionReceiptKind.Opened && id.Equals(receipt.Identity), "source fixture opens exact immutable template: " + receipt.FailureCode);
            return receipt;
        }

        private static BlueprintProjectionSourceReceipt Source(LibraryState library, BlueprintProjectionReceipt opened, long token, int range, BlueprintProjectionView view)
        {
            Require(library.RequestProjectionSource(opened.Identity, token, range, view: view), "isolated worker accepts exact source demand");
            BlueprintProjectionSourceReceipt receipt = null;
            NativeQuickItemChecks.Until(() => library.TryTakeProjectionSource(out receipt));
            Require(receipt.Generation == token && ReferenceEquals(receipt.View, view), "source receipt preserves exact demand generation and view");
            return receipt;
        }

        private sealed class FaultProfile : IBlueprintHorizontalMirrorProfile
        {
            private readonly IBlueprintHorizontalMirrorProfile profile;
            internal bool Budget;
            internal Func<bool> Reject;
            internal int Rejections;
            internal ManualResetEventSlim Entered, Continue;
            internal FaultProfile(IBlueprintHorizontalMirrorProfile profile) { this.profile = profile; }
            public long Revision { get { return profile.Revision; } }
            public bool Ready { get { return profile.Ready; } }
            public bool TryMirrorCell(CaptureCell source, int width, out CaptureCell target, out string reason)
            { return profile.TryMirrorCell(source, width, out target, out reason); }
            public bool TryNormalizeObject(CaptureObject source, IReadOnlyList<CaptureCell> members, out CaptureObject target, out string reason)
            { return profile.TryNormalizeObject(source, members, out target, out reason); }
            public bool TryMirrorObject(CaptureObject source, IReadOnlyList<CaptureCell> members, int width, out CaptureObject target,
                out CaptureCell[] targets, out string reason)
            {
                if (Reject != null && !Reject()) return profile.TryMirrorObject(source, members, width, out target, out targets, out reason);
                Interlocked.Increment(ref Rejections);
                Entered?.Set();
                if (Continue != null && !Continue.Wait(6000)) throw new TimeoutException("projection race barrier");
                if (Budget)
                {
                    var failure = typeof(LibraryState).Assembly.GetType("JueMingR.Features.Blueprint.LibraryWorker+ProjectionBudgetException", true);
                    throw (Exception)Activator.CreateInstance(failure, true);
                }
                throw new InvalidDataException("probe-source-transform");
            }
        }

        private static LibraryReceipt Finish(LibraryState library, bool accepted, LibraryStatus expected)
        {
            Require(accepted, "real library accepts edge template stage");
            LibraryReceipt receipt = null;
            NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out receipt); });
            Require(receipt.Status == expected, "real edge template stage reaches " + expected + " got " + receipt.Status);
            return receipt;
        }
        private static object[] Scene(object host) { return ((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().ToArray(); }
        private static void SetAnchor(int x, int y) { Main.tile[x, y].active(true); Main.tile[x, y].type = TileID.Stone; }
        private static object Find(object host, BlueprintProjectionIdentity identity) { return Scene(host).SingleOrDefault(item => identity.Equals(Get(item, "Identity"))); }
        private static bool MatchComplete(object item)
        {
            var match = GetOptional(item, "Match");
            return match != null && (int)Get(match, "Current") == ((Array)Get(match, "Ranges")).Length && (long)Get(match, "Unknown") == 0;
        }
        private static void Set(object target, string field, object value) { target.GetType().GetField(field, F).SetValue(target, value); }
    }
}
