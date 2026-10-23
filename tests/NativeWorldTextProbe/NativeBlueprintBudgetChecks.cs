using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Literal A/B/C sequences from the approved repair contract. These drive
    // actual Host admission, the sole files worker and retained renderer data;
    // they make no GPU, OS-input, FPS or multiplayer claim.
    internal static class NativeBlueprintBudgetChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var library = (LibraryState)Get(host, "Library");
            Call(Get(context, "Shell"), "CloseAndSubmitPosition"); Call(host, "ClearProjection"); Call(host, "CancelCaptureOrDraft");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            ((BlueprintCaptureState)Get(host, "State")).Exit(); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            try
            {
                CompactSemantics();
                NarrowStrips(host, input, library);
                EmptyCompletion(host, input, library);
                Pressure(host, input, library, false);
                Pressure(host, input, library, true);
                MovingCommandPressure(host, input, library);
                OperationOwnership(host, input, library);
                Console.WriteLine("PASS BlueprintBudgetCpu actual Host/worker/renderer A40 compact strips, independent raw32/commands64 stable denial/release/candidate/point, immutable halo/empty/rollback; GPU/OS/FPS/multiplayer NOT_RUN.");
            }
            finally { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "ClearProjection"); }
        }
        private static object[] Scene(object host) { return ((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().ToArray(); }
        private static Dictionary<BlueprintProjectionBounds, BlueprintProjectionWindow> Windows(object item)
        { return (Dictionary<BlueprintProjectionBounds, BlueprintProjectionWindow>)Get(item, "Windows"); }
        private static string Mode(object host) { return Get(host, "ProjectionMode").ToString(); }
        private static void Pointer(object host, bool left, int x, int y)
        { Call(host, "AcceptWorldPointer", true, left, false, x, y, 16384, 4096); }
        private static void Place(object host, object input, LibraryEntry entry, int x, int y, bool commit = true)
        {
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            Require((bool)Call(host, "Place", entry), "budget sequence accepts genuine library entry");
            Require(!(bool)Get(host, "CanOpenLibrary"), "pending Place rejects manual library entry");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return Mode(host) == "Following"; });
            Pointer(host, false, x, y);
            Require(!(bool)Get(host, "CanOpenLibrary"), "Following rejects manual library entry");
            if (!commit) return;
            Pointer(host, true, x, y);
            Require(Mode(host) == "Committing" && GetOptional(host, "projectionFeedback") == null, "accepted normal commit is quiet and remains a real transaction");
            Require(!(bool)Get(host, "CanOpenLibrary"), "Place Committing rejects manual library entry");
            WaitFixed(host);
            Require((bool)Get(host, "CanOpenLibrary"), "Fixed passive projections permit manual library entry");
        }
        private static void WaitFixed(object host)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew(); int report = 2;
            var library = (LibraryState)Get(host, "Library"); var initial = library.DebugProjectionStatistics;
            var worker = Get(library, "worker"); var gate = Get(worker, "gate");
            Action print = () =>
            {
                var stats = library.DebugProjectionStatistics;
                lock (gate) Console.WriteLine("BUDGET commit elapsedMs=" + clock.ElapsedMilliseconds + " mode=" + Mode(host) + " scene=" + Scene(host).Length + " feedback=" + GetOptional(host, "projectionFeedback") + " queued=" + (GetOptional(worker, "projectionCommit") != null) + " active=" + (GetOptional(worker, "projectionActiveCommit") != null) + " iterator=" + (GetOptional(worker, "projectionCommitting") != null) + " receipt=" + (GetOptional(worker, "projectionCommitReceipt") != null) + " reads/decoded/slices=" + (stats.BlockReads - initial.BlockReads) + "/" + (stats.DecodedRecords - initial.DecodedRecords) + "/" + (stats.Slices - initial.Slices));
            };
            while (true)
            {
                Call(host, "Poll"); if (Mode(host) == "Fixed") return;
                if (Mode(host) != "Committing") { print(); throw new Exception("Budget commit left transaction before Fixed"); }
                if (clock.Elapsed.TotalSeconds >= report) { print(); report += 2; }
                if (clock.Elapsed.TotalSeconds > 8) { print(); throw new Exception("Budget commit fixture 8-second timeout; raw progress retained above"); }
                System.Threading.Thread.Sleep(2);
            }
        }
        private static void Tick(object host, WorldTargetView view) { Call(host, "UpdateProjectionWindow", view); Call(host, "Poll"); }
        private static bool Settled(object host)
        {
            return Scene(host).All(p => !(bool)Get(p, "WindowPending") &&
                (Windows(p).Count != 0 || (int)Get(Get(p, "Denied"), "Count") != 0));
        }
        private static long[] Work(object host, LibraryState library)
        {
            var values = Scene(host).ToList(); var candidate = GetOptional(host, "ProjectionCandidate"); if (candidate != null) values.Add(candidate);
            var stats = library.DebugProjectionStatistics;
            return new[] { stats.BlockReads, stats.DecodedRecords, values.Sum(p => (long)Get(Get(p, "Renderer"), "BuildCount")), values.Sum(p => (long)Get(Get(p, "Renderer"), "SegmentBuildCount")) };
        }
        private static void Stable(object host, LibraryState library, WorldTargetView view, string label)
        {
            var before = Work(host, library);
            for (int i = 0; i < 100; i++) Tick(host, view);
            Require(before.SequenceEqual(Work(host, library)), label + " 100 unchanged Update/Pump cause zero reads/decodes/builds/segment builds");
            Require((long)Get(host, "ProjectionWindowBytes") < 32L * 1024 * 1024 && (long)Get(host, "ProjectionCommandBytes") < 64L * 1024 * 1024, label + " retained DTO/commands stay within original budgets");
            Console.WriteLine(label + " reads/decoded/builds/segments=" + String.Join("/", before) + " retained=" + Get(host, "ProjectionWindowBytes") + "/" + Get(host, "ProjectionCommandBytes"));
        }
        private static void NarrowStrips(object host, object input, LibraryState library)
        {
            var view = new WorldTargetView(0, 0, 167, 128, 0);
            var cells = Dense(128, 128, false);
            for (int i = 0; i < 40; i++)
            {
                // Different TemplateKeys exceed shared8 MiB and cannot hide an
                // eviction loop behind hot raw blocks. Every old core is loaded
                // before the next commit permanently erases 127 columns.
                var entry = Save(library, cells, 128, 128);
                Console.WriteLine("A commit=" + (i + 1) + " priorScene=" + Scene(host).Length + " asset=" + entry.AssetId.ToString("N"));
                Place(host, input, entry, 63 + i, 63);
                NativeQuickItemChecks.Until(() => { Tick(host, view); return Scene(host).All(p => Windows(p).Count == 1); });
            }
            var scene = Scene(host);
            Require(scene.Length == 40 && scene.Select(p => ((BlueprintProjectionIdentity)Get(p, "Identity")).TemplateKey).Distinct().Count() == 40, "A uses forty distinct actual templates/instances");
            Require(scene.Sum(p => Windows(p).Values.Sum(w => w.Cells.Count)) == 21376, "A retains 39 literal 128-cell strips plus final16384 cells");
            Require(scene.All(p => (int)Get(Get(p, "Renderer"), "CommandCount") > 0), "all forty effective strips retain render commands");
            Require(scene.Take(39).All(p => Windows(p).Values.Single().Cells.Count == 128 && (int)Get(Get(p, "Renderer"), "CommandCount") == 128) && (int)Get(Get(scene[39], "Renderer"), "CommandCount") == 16384, "A each literal full-Tile strip and final full core reaches every retained render command");
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "RemoveProjection");
            // Starting selection owns its neutral edge before any hover sample.
            Pointer(host, false, 0, 10);
            for (int i = 0; i < 40; i++)
            {
                Pointer(host, false, i, 10);
                var actual = GetOptional(host, "ProjectionHover");
                Require(ReferenceEquals(actual, scene[i]), "A every effective visible strip has its own actual Host hit; index=" + i + " expected=" + ((BlueprintProjectionIdentity)Get(scene[i], "Identity")).ProjectionGeneration + " actual=" + (actual == null ? "null" : ((BlueprintProjectionIdentity)Get(actual, "Identity")).ProjectionGeneration.ToString()));
            }
            Call(host, "RemoveProjection"); Require(Mode(host) == "Fixed", "same remove command cancels selection");
            Stable(host, library, view, "A40");

            // A moving copy borrows immutable DTOs. Cancel through the owning
            // command must recover exact old origin/order/mask/window references.
            object last = scene[39]; var old = Get(last, "Window"); var oldMask = Get(last, "Mask"); long order = (long)Get(last, "CommitOrder"); long rawBytes = (long)Get(host, "ProjectionWindowBytes");
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "MoveProjection"); Pointer(host, false, 100, 10); Pointer(host, true, 100, 10);
            Require(Mode(host) == "Moving", "C actual loaded hit starts moving copy"); Pointer(host, false, 102, 10);
            Require((long)Get(host, "ProjectionWindowBytes") == rawBytes, "C shared moving/rollback immutable snapshot is charged once");
            Require(!(bool)Get(host, "CanOpenLibrary"), "Moving rejects manual library entry");
            Call(host, "MoveProjection");
            Require(Mode(host) == "Fixed" && (int)Get(last, "OriginX") == 39 && (long)Get(last, "CommitOrder") == order && ReferenceEquals(oldMask, Get(last, "Mask")) && ReferenceEquals(old, Get(last, "Window")), "C same move action restores exact retained origin/order/mask/immutable DTO");
            Call(host, "ClearProjection");
        }
        private static void Pressure(object host, object input, LibraryState library, bool commands)
        {
            int count = commands ? 10 : 35;
            var entry = Save(library, Dense(128, 128, commands), 128, 128);
            for (int i = 0; i < count; i++) Place(host, input, entry, 63 + i * 130, 63);
            var view = new WorldTargetView(0, 0, count * 130, 128, 0);
            NativeQuickItemChecks.Until(() => { Tick(host, view); return Settled(host); });
            var residents = Scene(host).Where(p => Windows(p).Count != 0).ToArray();
            var denied = Scene(host).Where(p => Windows(p).Count == 0).ToArray();
            Require(residents.Length > 0 && denied.Length > 0, "B actual " + (commands ? "commands64" : "raw32") + " pressure shows useful resident prefix and denied remainder");
            if (commands) Require((long)Get(host, "ProjectionWindowBytes") < 16L * 1024 * 1024, "B command budget independently binds while DTO is far below raw32");
            else Require((long)Get(host, "ProjectionCommandBytes") < 64L * 1024 * 1024, "B raw budget binds without command overflow");
            Stable(host, library, view, commands ? "Bcommands" : "Braw");
            // Explicit click on NotLoaded is a 16core request. Hover alone
            // cannot request it, and arrival cannot select/delete automatically.
            object target = denied[0]; int x = (int)Get(target, "OriginX");
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "MoveProjection"); Pointer(host, false, x, 0);
            long serial = (long)Get(target, "WindowGeneration");
            for (int i = 0; i < 5; i++) Pointer(host, false, x, 0);
            Require((long)Get(target, "WindowGeneration") == serial && GetOptional(host, "ProjectionHover") == null, "B hover NotLoaded stays unknown without priority IO/fallthrough");
            Pointer(host, true, x, 0);
            NativeQuickItemChecks.Until(() => { Tick(host, view); return Windows(target).Keys.Any(k => k.Width == 16); });
            Require(Mode(host) == "SelectingMove" && Scene(host).Contains(target) && Windows(target).Keys.Any(k => k.Width == 16 && k.Height == 16), "B point16 priority loads actual worker receipt without auto selection");
            var pointWindow = Windows(target).Values.Single();
            Pointer(host, false, x, 0); Pointer(host, true, x, 0);
            Require(Mode(host) == "Moving", "B second independent click selects point-loaded target");
            Call(host, "MoveProjection");
            Require(Mode(host) == "Fixed" && Windows(target).Values.Any(w => ReferenceEquals(w, pointWindow)), "B point-selected Move cancellation preserves exact completed rollback snapshot");
            Call(host, "RemoveProjection"); Pointer(host, false, x, 0); Pointer(host, true, x, 0);
            Require(!Scene(host).Contains(target), "B subsequent new remove gesture can remove point-loaded target");

            // Candidate may preempt ordinary cores; stable ordinary updates
            // cannot evict it back. Cancel is an actual capacity release.
            Place(host, input, entry, 63, 63, false); object candidate = Get(host, "ProjectionCandidate");
            NativeQuickItemChecks.Until(() => { Tick(host, view); return Windows(candidate).Count != 0; });
            NativeQuickItemChecks.Until(() => { Tick(host, view); return Scene(host).All(p => !(bool)Get(p, "WindowPending")); });
            Stable(host, library, view, commands ? "BcommandsCandidate" : "BrawCandidate");
            Call(host, "Place", new object[] { null }); Require(Mode(host) == "Fixed", "same Place command cancels its own Following candidate");
            int missing = Scene(host).Count(p => Windows(p).Count == 0);
            object retire = Scene(host).First(p => Windows(p).Count != 0);
            Call(host, "RetireProjection", retire, true);
            NativeQuickItemChecks.Until(() => { Tick(host, view); return Scene(host).Count(p => Windows(p).Count == 0) < missing; });
            NativeQuickItemChecks.Until(() => { Tick(host, view); return Scene(host).All(p => !(bool)Get(p, "WindowPending")); });
            Stable(host, library, view, commands ? "BcommandsRelease" : "BrawRelease");
            object panTarget = Scene(host).First(p => Windows(p).Count == 0);
            var pan = new WorldTargetView((int)Get(panTarget, "OriginX"), 0, 128, 128, 0);
            NativeQuickItemChecks.Until(() => { Tick(host, pan); return Windows(panTarget).Count != 0; });
            NativeQuickItemChecks.Until(() => { Tick(host, pan); return Scene(host).All(p => !(bool)Get(p, "WindowPending")); });
            Stable(host, library, pan, commands ? "BcommandsPan" : "BrawPan");
            Call(host, "ClearProjection");
        }
        private static void EmptyCompletion(object host, object input, LibraryState library)
        {
            var lower = Save(library, new[] { new CaptureCell { Layers = CaptureLayer.Wall, WallType = 7 } }, 1, 1);
            var upper = Save(library, new[] { new CaptureCell { Layers = CaptureLayer.Tile, TileType = 1 }, new CaptureCell { X = 256, Layers = CaptureLayer.Wire, Wires = 1 } }, 257, 1);
            var cover = Save(library, new[] { new CaptureCell { Layers = CaptureLayer.Tile, TileType = 1 } }, 1, 1);
            Place(host, input, lower, 0, 0); object bottom = Scene(host).Single();
            Place(host, input, upper, 128, 0); object empty = Scene(host).Last();
            var view = new WorldTargetView(0, 0, 80, 32, 0);
            NativeQuickItemChecks.Until(() => { Tick(host, view); return Scene(host).All(p => Windows(p).Count == 1); });
            object raw = Get(empty, "Window");
            var rawCache = ((IEnumerable)Get(Get(library, "worker"), "projectionCache")).Cast<object>().First(block => ((BlueprintProjectionIdentity)Get(Get(block, "Owner"), "Identity")).AssetId == upper.AssetId);
            var rawChunk = (CaptureChunk)Get(rawCache, "Chunk"); var cachedCells = (CaptureCell[])rawChunk.Cells.Clone();
            Place(host, input, cover, 0, 0);
            NativeQuickItemChecks.Until(() => { Tick(host, view); return Scene(host).All(p => Windows(p).Count == 1); });
            Require(Windows(empty).Values.Single().Cells.Count == 0 && ((BlueprintProjectionWindow)raw).Cells.Count == 1 && (int)Get(Get(empty, "Renderer"), "CommandCount") == 0, "C real mask commit compacts loaded raw to completed empty while old snapshot stays intact");
            Require(cachedCells.SequenceEqual(rawChunk.Cells), "C actual shared worker raw cache chunk remains unchanged after Host compact");
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "RemoveProjection"); Pointer(host, false, 0, 0); Pointer(host, true, 0, 0);
            Call(host, "RemoveProjection"); Pointer(host, false, 0, 0); Pointer(host, false, 0, 0);
            Require(ReferenceEquals(GetOptional(host, "ProjectionHover"), bottom), "C only completed empty higher core can fall through to lower live wall");
            Call(host, "RemoveProjection"); Stable(host, library, view, "Cempty"); Call(host, "ClearProjection");
        }
        private static CaptureCell[] Dense(int width, int height, bool commands)
        {
            var cells = new CaptureCell[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) cells[y * width + x] = new CaptureCell { X = x, Y = y, Layers = commands ? CaptureLayer.Tile | CaptureLayer.Wire | CaptureLayer.Wall | CaptureLayer.Actuator | CaptureLayer.Liquid : CaptureLayer.Tile, TileType = 1, WallType = commands ? (ushort)7 : (ushort)0, Slope = commands ? (byte)1 : (byte)0, Wires = commands ? (byte)15 : (byte)0, Actuator = commands, LiquidAmount = commands ? (byte)80 : (byte)0 };
            return cells;
        }
        private static void MovingCommandPressure(object host, object input, LibraryState library)
        {
            var entry = Save(library, Dense(512, 128, true), 512, 128);
            Place(host, input, entry, 255, 63);
            var view = new WorldTargetView(0, 0, 512, 128, 0); object source = Scene(host).Single();
            NativeQuickItemChecks.Until(() => { Tick(host, view); return !(bool)Get(source, "WindowPending") && Windows(source).Count + (int)Get(Get(source, "Denied"), "Count") == 4; });
            long commands = (long)Get(host, "ProjectionCommandBytes");
            Require(commands > 48L * 1024 * 1024 && commands < 64L * 1024 * 1024, "B single multi-core instance genuinely fills most commands64 budget");
            Stable(host, library, view, "BsingleMoveBefore");
            var originalWindows = Windows(source).ToDictionary(p => p.Key, p => p.Value); object alias = Get(source, "Window");
            var stats = library.DebugProjectionStatistics;
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "MoveProjection"); Pointer(host, false, 0, 0); Pointer(host, true, 0, 0);
            Require(Mode(host) == "Moving" && (int)Get(Get(source, "Renderer"), "CommandCount") == 0 && (long)Get(host, "ProjectionCommandBytes") == commands && (int)Get(Get(Get(host, "ProjectionCandidate"), "Renderer"), "CommandCount") * 80L == commands, "B Moving borrows released hidden-source command capacity without freezing its own visible prefix");
            Call(host, "MoveProjection");
            Require(Mode(host) == "Fixed" && (long)Get(host, "ProjectionCommandBytes") == commands && ReferenceEquals(Get(source, "Window"), alias) && originalWindows.All(p => Windows(source).TryGetValue(p.Key, out var value) && ReferenceEquals(value, p.Value)), "B near64 Moving cancel restores exact original immutable windows/alias and full old command prefix");
            Require(stats.BlockReads == library.DebugProjectionStatistics.BlockReads && stats.DecodedRecords == library.DebugProjectionStatistics.DecodedRecords, "B Moving and cancel rebuild from retained source snapshots without extra file IO or decode");
            NativeQuickItemChecks.Until(() => { Tick(host, view); return !(bool)Get(source, "WindowPending"); });
            Stable(host, library, view, "BsingleMoveAfter"); Call(host, "ClearProjection");
        }
        private static void OperationOwnership(object host, object input, LibraryState library)
        {
            var entry = NativeBlueprintPlacementChecks.Seed(library, 2, 2, "操作来源样本");
            Action drain = () => { for (int i = 0; i < 25; i++) { Call(host, "Poll"); System.Threading.Thread.Sleep(2); } };
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            Require((bool)Call(host, "Place", entry) && (bool)Get(host, "OwnPlaceCancellation"), "pending Open belongs to Place cancellation");
            Require(!(bool)Get(host, "CanInvokeMove") && !(bool)Get(host, "CanInvokeRemove"), "pending Place cannot be stolen by move/remove");
            Require((bool)Call(host, "Place", new object[] { null }), "same Place command retires pending Open"); drain();
            Require(Scene(host).Length == 0 && GetOptional(host, "ProjectionCandidate") == null && GetOptional(host, "pendingProjection") == null, "late Open after same-action cancellation cannot revive candidate");
            Place(host, input, entry, 20, 20, false); Pointer(host, true, 20, 20);
            Require(Mode(host) == "Committing" && (bool)Get(host, "OwnPlaceCancellation") && !(bool)Get(host, "OwnMoveCancellation"), "new Place committing retains exact operation source");
            Call(host, "Place", new object[] { null }); drain(); Require(Scene(host).Length == 0, "Place committing cancel rejects late transaction receipt");

            Place(host, input, entry, 20, 20); Place(host, input, entry, 400, 20);
            var view = new WorldTargetView(0, 0, 430, 128, 0);
            NativeQuickItemChecks.Until(() => { Tick(host, view); return Scene(host).All(p => Windows(p).Count != 0); });
            object original = Scene(host)[0]; var window = Get(original, "Window"); var mask = Get(original, "Mask"); long order = (long)Get(original, "CommitOrder");
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "MoveProjection");
            Require((bool)Get(host, "OwnMoveCancellation") && (bool)Get(host, "CanInvokeMove") && !(bool)Get(host, "CanInvokeRemove") && !(bool)Get(host, "CanInvokePlace"), "SelectingMove exposes only its own cancel");
            Require(!(bool)Get(host, "CanOpenLibrary"), "SelectingMove rejects manual library entry");
            Call(host, "RemoveProjection"); Require(Mode(host) == "SelectingMove", "cross remove cannot cancel or switch move selection");
            Call(host, "MoveProjection"); Require(Mode(host) == "Fixed", "same Move command exits selecting state");
            Call(host, "MoveProjection"); Pointer(host, false, 20, 20); Pointer(host, true, 20, 20); Pointer(host, false, 21, 20); Pointer(host, true, 21, 20);
            Require(Mode(host) == "Committing" && (bool)Get(host, "OwnMoveCancellation") && !(bool)Get(host, "OwnPlaceCancellation"), "Move committing preserves rollback-based operation source");
            Require(!(bool)Get(host, "CanOpenLibrary"), "Move Committing rejects manual library entry");
            Call(host, "MoveProjection"); drain();
            Require(Scene(host).Length == 2 && (int)Get(original, "OriginX") == 20 && (long)Get(original, "CommitOrder") == order && ReferenceEquals(window, Get(original, "Window")) && ReferenceEquals(mask, Get(original, "Mask")), "Move committing cancellation preserves exact original scene and rejects late result");
            Call(host, "RemoveAllProjections"); Require(Scene(host).Length == 2, "RemoveAll is rejected outside SelectingRemove");
            Call(host, "RemoveProjection"); Call(host, "MoveProjection"); Require(Mode(host) == "SelectingRemove" && (bool)Get(host, "CanInvokeRemove"), "cross move cannot steal remove selection");
            Require(!(bool)Get(host, "CanOpenLibrary"), "SelectingRemove rejects manual library entry");
            Call(host, "RemoveProjection"); Require(Mode(host) == "Fixed", "same Remove command cancels remove selection");
            Call(host, "RemoveProjection"); Call(host, "RemoveAllProjections"); drain();
            Require(Mode(host) == "None" && Scene(host).Length == 0 && GetOptional(host, "ProjectionCandidate") == null && !(bool)Get(host, "CanRemoveAllProjections"), "RemoveAll clears same-template visible/offscreen scene and exits mode without late revival");
            Require(library.TryList(91003, 0, entry.AssetId), "RemoveAll leaves library template queryable"); Require(Complete(library).Page.Entries.Any(e => e.AssetId == entry.AssetId), "RemoveAll never deletes library asset");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return library.DebugProjectionStatistics.ActiveReferences == 0 && library.DebugProjectionStatistics.ActiveTemplates == 0; });
            Console.WriteLine("PASS operation ownership pending/Following/PlaceCommitting/SelectingMove/MoveCommitting/SelectingRemove, cross-action rejection, late receipts and RemoveAll last-reference/library preservation.");
        }
        private static LibraryEntry Save(LibraryState library, CaptureCell[] cells, int width, int height)
        {
            var token = new CaptureToken(Guid.NewGuid(), 1, 1, 1); var handle = new ContentHandle(token.OperationId);
            Require(library.TryBeginStage(token), "budget real stage accepted"); Complete(library);
            for (int i = 0; i < cells.Length; i += CaptureChunk.MaximumRecords)
            {
                var chunk = cells.Skip(i).Take(CaptureChunk.MaximumRecords).ToArray();
                Require(library.TryAppendChunk(handle, new CaptureChunk(chunk, chunk.Length)), "budget bounded real chunk accepted"); Complete(library);
            }
            Require(library.TrySealDraft(handle, new CaptureSummary { Width = width, Height = height, ContentCount = cells.Length }), "budget draft sealed"); Complete(library);
            Require(library.TryCreate(handle, "预算机制样本", token), "budget asset create accepted"); var saved = Complete(library);
            Require(saved.Status == LibraryStatus.Saved, "budget asset reliably saved");
            Require(library.TryList(91002, 0, saved.AssetId), "budget list targets exact newly saved asset");
            return Complete(library).Page.Entries.Single(e => e.AssetId == saved.AssetId);
        }
        private static LibraryReceipt Complete(LibraryState library)
        {
            LibraryReceipt result = null; NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out result); });
            Require(result.Status != LibraryStatus.Failed && result.Status != LibraryStatus.CommitUnconfirmed, "budget real files operation: " + result.Error); return result;
        }
        private static void CompactSemantics()
        {
            var ctor = typeof(BlueprintProjectionMask).GetConstructors(Flags).Single();
            var removed = new Dictionary<long, ushort> { [((long)5 << 32) | 5] = 1, [((long)5 << 32) | 6] = 1 };
            var mask = (BlueprintProjectionMask)ctor.Invoke(new object[] { removed, new HashSet<int>(), 2L });
            var id = new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "halo", 1, 1);
            var cells = new[] {
                new CaptureCell { X = 5, Y = 5, Layers = CaptureLayer.Tile | CaptureLayer.Wall | CaptureLayer.Wire, TileType = 1, WallType = 7, Wires = 2, ObjectId = 1 },
                new CaptureCell { X = 6, Y = 5, Layers = CaptureLayer.Tile, TileType = 1, ObjectId = 2 },
                new CaptureCell { X = 7, Y = 5, Layers = CaptureLayer.Tile, TileType = 1, ObjectId = 3 } };
            var objects = new[] { new CaptureObject { Id = 1, X = 0, Y = 0 }, new CaptureObject { Id = 2, X = 0, Y = 0 }, new CaptureObject { Id = 3, X = 0, Y = 0, Width = 12, Height = 6 } };
            var raw = new BlueprintProjectionWindow(id, 7, new CaptureSummary { Width = 16, Height = 16 }, new BlueprintProjectionBounds(4, 4, 8, 8), cells, objects);
            var effective = mask.Apply(raw);
            Require(effective.Bounds.Equals(raw.Bounds) && ReferenceEquals(effective.Identity, id) && effective.WindowGeneration == 7 && ReferenceEquals(effective.AppliedMask, mask), "C effective preserves halo bounds/instance/generation/applied mask identity");
            Require(effective.Cells.Count == 2 && effective.Cells[0].Layers == (CaptureLayer.Wall | CaptureLayer.Wire) && effective.Cells[0].Wires == 2 && effective.Objects.Count == 1 && effective.Objects[0].Id == 3 && effective.Objects[0].X == 0, "C Tile-only erase preserves other layers and only referenced full external-origin metadata");
            Require(raw.Cells.Count == 3 && raw.Objects.Count == 3 && raw.Cells[0].ObjectId == 1 && effective.EstimatedBytes < raw.EstimatedBytes && ReferenceEquals(mask.Apply(effective), effective), "C raw/cache snapshot unmodified; repeated applied identity reuses compact snapshot");
            var emptyMask = (BlueprintProjectionMask)ctor.Invoke(new object[] { new Dictionary<long, ushort> { [((long)5 << 32) | 7] = 1 }, new HashSet<int>(), 1L });
            var empty = emptyMask.Apply(new BlueprintProjectionWindow(id, 8, raw.Summary, raw.Bounds, new[] { cells[2] }, objects));
            Require(empty.Cells.Count == 0 && empty.Objects.Count == 0 && empty.Bounds.Equals(raw.Bounds), "C completed empty DTO retains coverage and no object ghost");
        }
    }
}
