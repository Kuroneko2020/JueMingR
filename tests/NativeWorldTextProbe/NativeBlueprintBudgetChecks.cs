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
            using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 4700, 160))
            try
            {
                AdmissionFailureAtomicity(host, library);
                CompactSemantics();
                NarrowStrips(context, host, input, library);
                EmptyCompletion(host, input, library);
                Pressure(context, host, input, library, false);
                Pressure(context, host, input, library, true);
                MovingCommandPressure(context, host, input, library);
                OperationOwnership(context, host, input, library);
                Console.WriteLine("PASS BlueprintBudgetCpu actual Host/worker/renderer A40 compact strips, independent raw32/commands64 stable denial/release/candidate/point, immutable halo/empty/rollback; GPU/OS/FPS/multiplayer NOT_RUN.");
            }
            finally { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "ClearProjection"); }
        }
        // Independent literal payloads exercise actual Host admission and the
        // retained renderer seam. Repeated commands replace only the pressure
        // fixture's retained payload; no GPU drawing correctness is claimed.
        private static object RepeatCommand<T>(object command, int count)
        { return Enumerable.Repeat((T)command, count).ToList(); }
        private static BlueprintProjectionWindow AdmissionWindow(object item, BlueprintProjectionBounds core, long bytes = 0, bool slope = false)
        {
            var cells = slope ? Dense(128, 128, false) : new[] { new CaptureCell { X = core.Left, Y = core.Top, Layers = CaptureLayer.Tile, TileType = 1 } };
            if (slope) for (int i = 0; i < cells.Length; i++) { var cell = cells[i]; cell.X += core.Left; cell.Y += core.Top; cell.Slope = 1; cells[i] = cell; }
            var objects = bytes == 0 ? Array.Empty<CaptureObject>() : new[] { new CaptureObject { Id = 1, Reason = new string('x', checked((int)((bytes - 448) / 2))) } };
            return new BlueprintProjectionWindow((BlueprintProjectionIdentity)Get(item, "Identity"), 1, (CaptureSummary)Get(item, "Summary"),
                new BlueprintProjectionBounds(0, 0, 256, 128), cells, objects);
        }
        private static object AdmissionOwner(object host, int origin = 0)
        {
            var type = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintProjectionPlacement", true);
            var item = Activator.CreateInstance(type, true);
            Set(item, "Identity", new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "admission", 1, 1));
            Set(item, "Summary", new CaptureSummary { Width = 256, Height = 128, ContentCount = 32768 });
            Set(item, "Coverage", new BlueprintProjectionBounds(0, 0, 256, 128));
            Set(item, "OriginX", origin); Set(item, "Opened", true); Set(item, "Positioned", true);
            return item;
        }
        private static void RetainAdmissionCore(object item, BlueprintProjectionBounds core, BlueprintProjectionWindow window, int commands = 0)
        {
            Windows(item).Add(core, window); Set(item, "Window", window); Set(item, "HasRequestedWindow", true);
            var renderer = Get(item, "Renderer"); Call(renderer, "UpdateWindow", window, BlueprintProjectionMask.Empty, core, Get(item, "Coverage"));
            if (commands == 0) return;
            var segment = ((IDictionary)Get(renderer, "segments")).Values.Cast<object>().Single();
            var first = ((IList)Get(segment, "Commands"))[0];
            var payload = typeof(NativeBlueprintBudgetChecks).GetMethod("RepeatCommand", BindingFlags.NonPublic | BindingFlags.Static)
                .MakeGenericMethod(first.GetType()).Invoke(null, new object[] { first, commands });
            Set(segment, "Commands", payload); Set(renderer, "commands", payload);
        }
        private static void AddAdmissionScene(object host, object item)
        { ((IList)Get(host, "projectionScene")).Add(item); }
        private static void AdmissionUnchanged(object host, object item, BlueprintProjectionBounds core, BlueprintProjectionWindow window,
            object commands, int count, long capacity, string label)
        {
            Require(Windows(item).Count == 1 && ReferenceEquals(Windows(item)[core], window) && ReferenceEquals(Get(item, "Window"), window) && (bool)Get(item, "HasRequestedWindow"), label + " exact old resident/alias/request state survives");
            Require(ReferenceEquals(Get(Get(item, "Renderer"), "commands"), commands) && (int)Get(Get(item, "Renderer"), "CommandCount") == count && (int)Get(Get(item, "Denied"), "Count") == 0, label + " old commands and denial survive");
            Require((long)Get(host, "projectionCapacityRevision") == capacity, label + " refusal creates no capacity revision");
        }
        private static void AdmissionFailureAtomicity(object host, LibraryState library)
        {
            var oldCore = new BlueprintProjectionBounds(0, 0, 128, 128); var incomingCore = new BlueprintProjectionBounds(128, 0, 128, 128);
            try
            {
                ExtraRetention(host);
                // 60MiB + 2MiB are compatible; even removing the whole 2MiB
                // resident cannot fit the independent 8-command-per-cell slope.
                var candidate = AdmissionOwner(host); var ordinary = AdmissionOwner(host, 512);
                Set(host, "candidateProjection", candidate); AddAdmissionScene(host, ordinary);
                var old = AdmissionWindow(ordinary, oldCore); var prior = AdmissionWindow(candidate, oldCore);
                RetainAdmissionCore(candidate, oldCore, prior, 786432); RetainAdmissionCore(ordinary, oldCore, old, 26214);
                var incoming = AdmissionWindow(candidate, incomingCore, slope: true);
                var payload = Get(Get(ordinary, "Renderer"), "commands"); long capacity = (long)Get(host, "projectionCapacityRevision");
                Require(ReferenceEquals(Call(host, "HitProjection", 512, 0, .5f, .5f, false), ordinary), "unsatisfiable commands fixture has an actual old Host hit");
                Require(!(bool)Call(host, "AdmitProjectionWindow", candidate, incomingCore, incoming), "unsatisfiable commands priority actually refuses incoming");
                AdmissionUnchanged(host, ordinary, oldCore, old, payload, 26214, capacity, "commands failure");
                Require(Windows(candidate).Count == 1 && ReferenceEquals(Windows(candidate)[oldCore], prior) && (int)Get(Get(candidate, "Renderer"), "CommandCount") == 786432 && !Windows(candidate).ContainsKey(incomingCore), "commands refusal leaves candidate prefix intact and never publishes incoming");
                Require((long)Get(host, "ProjectionCommandBytes") == (786432L + 26214L) * 80 && ReferenceEquals(Call(host, "HitProjection", 512, 0, .5f, .5f, false), ordinary), "commands refusal preserves exact charge and actual Host hit");
                var stats = library.DebugProjectionStatistics; long builds = (long)Get(Get(candidate, "Renderer"), "BuildCount"), segments = (long)Get(Get(candidate, "Renderer"), "SegmentBuildCount");
                for (int i = 0; i < 100; i++) Require((bool)Call(host, "ProjectionDenied", candidate, incomingCore), "stable same-capacity incoming denial remains bounded");
                Require(stats.BlockReads == library.DebugProjectionStatistics.BlockReads && stats.DecodedRecords == library.DebugProjectionStatistics.DecodedRecords && builds == (long)Get(Get(candidate, "Renderer"), "BuildCount") && segments == (long)Get(Get(candidate, "Renderer"), "SegmentBuildCount"), "stable refused demand does no read/decode/rebuild");
                Call(host, "RemoveProjectionCore", candidate, oldCore);
                Require(!(bool)Call(host, "ProjectionDenied", candidate, incomingCore) && (bool)Call(host, "AdmitProjectionWindow", candidate, incomingCore, incoming), "real command release makes original incoming denial eligible and admission succeeds");
                Call(host, "ClearProjection");

                // Literal shared raw payload: 31.8MiB + .1MiB resident fits;
                // even without ordinary, source/candidate + .4MiB exceeds 32.
                var source = AdmissionOwner(host); candidate = AdmissionOwner(host); ordinary = AdmissionOwner(host, 512);
                AddAdmissionScene(host, source); AddAdmissionScene(host, ordinary); Set(host, "restoreProjection", source); Set(host, "candidateProjection", candidate);
                var shared = AdmissionWindow(source, oldCore, 33344716); old = AdmissionWindow(ordinary, oldCore, 104858);
                RetainAdmissionCore(source, oldCore, shared); Call(Get(source, "Renderer"), "Clear");
                RetainAdmissionCore(candidate, oldCore, shared); RetainAdmissionCore(ordinary, oldCore, old);
                incoming = AdmissionWindow(candidate, incomingCore, 419430); payload = Get(Get(ordinary, "Renderer"), "commands"); capacity = (long)Get(host, "projectionCapacityRevision");
                Require((long)Get(host, "ProjectionWindowBytes") == 33449574L, "literal raw source/candidate alias is charged once before admission");
                Require(!(bool)Call(host, "AdmitProjectionWindow", candidate, incomingCore, incoming), "shared raw priority actually refuses impossible 32.2MiB payload");
                AdmissionUnchanged(host, ordinary, oldCore, old, payload, 1, capacity, "shared raw failure");
                Require((long)Get(host, "ProjectionWindowBytes") == 33449574L && ReferenceEquals(Windows(source)[oldCore], shared) && ReferenceEquals(Windows(candidate)[oldCore], shared) && !Windows(candidate).ContainsKey(incomingCore), "shared raw refusal preserves source/candidate aliases and original exact charge");
                Require(ReferenceEquals(Call(host, "HitProjection", 512, 0, .5f, .5f, false), ordinary), "shared raw refusal keeps actual old hit");
                var denial = ((IDictionary)Get(candidate, "Denied"))[incomingCore];
                Require((long)Get(denial, "WindowBytes") == 419430L && (long)Get(denial, "CapacityRevision") == capacity, "incoming denial uses original net raw cost and real capacity revision");
                Call(host, "RemoveProjectionCore", source, oldCore);
                Require((long)Get(host, "ProjectionWindowBytes") == 33449574L && (bool)Call(host, "ProjectionDenied", candidate, incomingCore), "removing only rollback alias creates no raw room");
                Call(host, "RemoveProjectionCore", candidate, oldCore);
                Require(!(bool)Call(host, "ProjectionDenied", candidate, incomingCore) && (bool)Call(host, "AdmitProjectionWindow", candidate, incomingCore, incoming), "real shared DTO release recovers denied raw admission");
                Call(host, "ClearProjection");

                candidate = AdmissionOwner(host); ordinary = AdmissionOwner(host, 512); Set(host, "candidateProjection", candidate); AddAdmissionScene(host, ordinary);
                RetainAdmissionCore(candidate, oldCore, AdmissionWindow(candidate, oldCore), 681574); RetainAdmissionCore(ordinary, oldCore, AdmissionWindow(ordinary, oldCore), 104857);
                incoming = AdmissionWindow(candidate, incomingCore, slope: true);
                Require((bool)Call(host, "AdmitProjectionWindow", candidate, incomingCore, incoming), "satisfiable priority really evicts legal ordinary core");
                Require(Windows(ordinary).Count == 0 && (int)Get(Get(ordinary, "Renderer"), "CommandCount") == 0 && Windows(candidate).Count == 2 && (int)Get(Get(candidate, "Renderer"), "CommandCount") == 681574 + 131072, "successful plan publishes exact literal 52MiB-prefix plus 131072 slope commands");
                Require((long)Get(host, "ProjectionWindowBytes") < 32L * 1024 * 1024 && (long)Get(host, "ProjectionCommandBytes") < 64L * 1024 * 1024 && (long)Get(((IDictionary)Get(ordinary, "Denied"))[oldCore], "CapacityRevision") == (long)Get(host, "projectionCapacityRevision"), "successful finite eviction stays in both original budgets and stamps final capacity");
                Call(host, "ClearProjection");

                var pointOwner = AdmissionOwner(host); AddAdmissionScene(host, pointOwner);
                RetainAdmissionCore(pointOwner, oldCore, AdmissionWindow(pointOwner, oldCore), 838860);
                var point = new BlueprintProjectionBounds(128, 0, 16, 16);
                Set(host, "ProjectionMode", Enum.Parse(Get(host, "ProjectionMode").GetType(), "SelectingMove")); Set(host, "projectionPointTarget", pointOwner); Set(host, "projectionPointCore", point);
                incoming = AdmissionWindow(pointOwner, point);
                Require((bool)Call(host, "AdmitProjectionWindow", pointOwner, point, incoming), "same-owner point admits by finite non-point eviction");
                Require(Windows(pointOwner).Count == 1 && Windows(pointOwner).ContainsKey(point) && !Windows(pointOwner).ContainsKey(oldCore) && (int)Get(Get(pointOwner, "Renderer"), "CommandCount") == 1, "published same-owner stage cannot bring its evicted 838860-command core back");
                Require(Call(host, "HitProjection", 0, 0, .5f, .5f, false) == null && ReferenceEquals(Call(host, "HitProjection", 128, 0, .5f, .5f, false), pointOwner), "same-owner victim loses hit while admitted point remains hittable");
                Console.WriteLine("PASS Blueprint admission atomic failure commands60+2/new10, sharedRaw31.8+.1/new.4, literal finite eviction and same-owner point; retained Extra-position counting; GPU NOT_RUN.");
            }
            finally { Call(host, "ClearProjection"); }
        }
        private static void ExtraRetention(object host)
        {
            var item = AdmissionOwner(host); var renderer = Get(item, "Renderer");
            var window = new BlueprintProjectionWindow((BlueprintProjectionIdentity)Get(item, "Identity"), 1, new CaptureSummary { Width = 32, Height = 16 }, new BlueprintProjectionBounds(0, 0, 32, 16),
                new[] { new CaptureCell { X = 1, Layers = CaptureLayer.Tile, TileType = 1, ObjectId = 1 }, new CaptureCell { X = 17, Layers = CaptureLayer.Tile, TileType = 1, ObjectId = 1 } },
                new[] { new CaptureObject { Id = 1, Type = 597, Reliable = true, Width = 20, Height = 1 } });
            var first = new BlueprintProjectionBounds(0, 0, 16, 16); var second = new BlueprintProjectionBounds(16, 0, 16, 16); var coverage = window.Bounds;
            Call(renderer, "UpdateWindow", window, BlueprintProjectionMask.Empty, first, coverage); Call(renderer, "UpdateWindow", window, BlueprintProjectionMask.Empty, second, coverage);
            Require((int)Get(renderer, "CommandCount") == 3, "two segments share exactly one Extra body plus two tile commands");
            var plan = Call(renderer, "PlanCommands", new object[] { null }); Call(plan, "RemoveCore", first);
            Require((int)Get(plan, "CommandCount") == 2 && (int)Get(renderer, "CommandCount") == 3, "read-only command plan retains duplicate Extra body while other segment owns it");
            Call(renderer, "RemoveCore", first); Require((int)Get(renderer, "CommandCount") == 2, "actual Compose agrees with independent two-command expectation");
            Call(plan, "RemoveCore", second); Call(renderer, "RemoveCore", second);
            Require((int)Get(plan, "CommandCount") == 0 && (int)Get(renderer, "CommandCount") == 0, "last Extra reference releases exactly once");
        }
        private static object[] Scene(object host) { return ((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().ToArray(); }
        private static Dictionary<BlueprintProjectionBounds, BlueprintProjectionWindow> Windows(object item)
        { return (Dictionary<BlueprintProjectionBounds, BlueprintProjectionWindow>)Get(item, "Windows"); }
        private static string Mode(object host) { return Get(host, "ProjectionMode").ToString(); }
        private static void Pointer(object host, bool left, int x, int y)
        { Call(host, "AcceptWorldPointer", true, left, false, x, y, 16384, 4096); }
        private static void Place(object host, object input, LibraryEntry entry, int x, int y, bool commit = true, int densePriorCount = -1)
        {
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            Require((bool)Call(host, "Place", entry), "budget sequence accepts genuine library entry");
            Require(!(bool)Get(host, "CanOpenLibrary"), "pending Place rejects manual library entry");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return Mode(host) == "Following"; });
            Pointer(host, false, x, y);
            Require(!(bool)Get(host, "CanOpenLibrary"), "Following rejects manual library entry");
            if (!commit) return;
            // Snapshot before acceptance: the worker may start the commit as
            // soon as Pointer submits it, before WaitFixed gets its first turn.
            BlueprintProjectionStatistics? commitStart = densePriorCount >= 0 ? ((LibraryState)Get(host, "Library")).DebugProjectionStatistics : (BlueprintProjectionStatistics?)null;
            Pointer(host, true, x, y);
            Require(Mode(host) == "Committing" && GetOptional(host, "projectionFeedback") == null, "accepted normal commit is quiet and remains a real transaction");
            Require(!(bool)Get(host, "CanOpenLibrary"), "Place Committing rejects manual library entry");
            WaitFixed(host, densePriorCount, commitStart);
            Require((bool)Get(host, "CanOpenLibrary"), "Fixed passive projections permit manual library entry");
        }
        private static void WaitFixed(object host, int densePriorCount, BlueprintProjectionStatistics? commitStart)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew(); int report = 2;
            var library = (LibraryState)Get(host, "Library"); var initial = commitStart ?? library.DebugProjectionStatistics;
            var worker = Get(library, "worker"); var gate = Get(worker, "gate");
            long lastReads = 0, lastDecoded = 0, lastComparisons = 0, lastProgressMs = 0;
            // Only the literal A40 sequence supplies this count. Each aligned
            // 128x128 template has eight 2048-cell blocks and no objects. Per
            // old opponent: at most eight incoming plus eight target reads;
            // directory searches cost <= (128 + 16384) * 4 comparisons. No new
            // window/library requests are made here. These independent work
            // ceilings must not grow with observed production counters.
            long readLimit = 16L * densePriorCount, decodedLimit = 32768L * densePriorCount, comparisonLimit = 66048L * densePriorCount;
            Action print = () =>
            {
                var stats = library.DebugProjectionStatistics;
                lock (gate) Console.WriteLine("BUDGET commit elapsedMs=" + clock.ElapsedMilliseconds + " mode=" + Mode(host) + " scene=" + Scene(host).Length + " feedback=" + GetOptional(host, "projectionFeedback") + " queued=" + (GetOptional(worker, "projectionCommit") != null) + " active=" + (GetOptional(worker, "projectionActiveCommit") != null) + " iterator=" + (GetOptional(worker, "projectionCommitting") != null) + " receipt=" + (GetOptional(worker, "projectionCommitReceipt") != null) + " reads/decoded/slices=" + (stats.BlockReads - initial.BlockReads) + "/" + (stats.DecodedRecords - initial.DecodedRecords) + "/" + (stats.Slices - initial.Slices) + " directoryComparisons=" + (stats.DirectoryComparisons - initial.DirectoryComparisons));
            };
            while (true)
            {
                Call(host, "Poll"); string mode = Mode(host);
                if (mode != "Fixed" && mode != "Committing") { print(); throw new Exception("Budget commit left transaction before Fixed"); }
                if (densePriorCount >= 0)
                {
                    var stats = library.DebugProjectionStatistics;
                    long reads = stats.BlockReads - initial.BlockReads, decoded = stats.DecodedRecords - initial.DecodedRecords, comparisons = stats.DirectoryComparisons - initial.DirectoryComparisons;
                    if (reads > readLimit || decoded > decodedLimit || comparisons > comparisonLimit)
                    { print(); throw new Exception("A40 commit exceeded literal finite work limits: " + readLimit + "/" + decodedLimit + "/" + comparisonLimit); }
                    // A cooperative lane can spin without semantic work. Only
                    // bounded read/decode/directory progress renews this timer;
                    // Slices never does. The total watchdog is fixture safety,
                    // not a product completion-latency promise.
                    if (reads > lastReads || decoded > lastDecoded || comparisons > lastComparisons)
                    { lastReads = reads; lastDecoded = decoded; lastComparisons = comparisons; lastProgressMs = clock.ElapsedMilliseconds; }
                    if (clock.ElapsedMilliseconds - lastProgressMs > 8000)
                    { print(); throw new Exception("A40 commit made no bounded read/decode/directory progress for 8 seconds"); }
                    if (clock.Elapsed.TotalSeconds > 60)
                    { print(); throw new Exception("A40 commit fixture 60-second total safety watchdog"); }
                }
                if (mode == "Fixed") return;
                if (clock.Elapsed.TotalSeconds >= report) { print(); report += 2; }
                if (densePriorCount < 0 && clock.Elapsed.TotalSeconds > 8) { print(); throw new Exception("Budget commit fixture 8-second timeout; raw progress retained above"); }
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
        private static void NarrowStrips(object context, object host, object input, LibraryState library)
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
                Place(host, input, entry, 63 + i, 63, densePriorCount: i);
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
            // All forty sources may fairly advance. This literal fixture has
            // 40*8 blocks/40*16384 source records, while each selected instance
            // still earns exactly its own effective empty-world observation.
            NativeBlueprintWorldMatchChecks.AwaitMovable(context, host, scene[39],16384,8,16384,40*16384,40*8);
            NativeBlueprintWorldMatchChecks.AwaitMovable(context, host, scene[0],16384,8,128,40*16384,40*8);
            Require((long)Get(Get(scene[0], "Match"), "WorldReads") == 128, "A first 16384-record source observes only its 128 effective strip cells; 16256 permanently erased cells spend zero world reads");
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
        private static void Pressure(object context, object host, object input, LibraryState library, bool commands)
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
            NativeBlueprintWorldMatchChecks.AwaitMovable(context, host, target);
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
        private static void MovingCommandPressure(object context, object host, object input, LibraryState library)
        {
            var entry = Save(library, Dense(512, 128, true), 512, 128);
            Place(host, input, entry, 255, 63);
            var view = new WorldTargetView(0, 0, 512, 128, 0); object source = Scene(host).Single();
            NativeQuickItemChecks.Until(() => { Tick(host, view); return !(bool)Get(source, "WindowPending") && Windows(source).Count + (int)Get(Get(source, "Denied"), "Count") == 4; });
            long commands = (long)Get(host, "ProjectionCommandBytes");
            Require(commands > 48L * 1024 * 1024 && commands < 64L * 1024 * 1024, "B single multi-core instance genuinely fills most commands64 budget");
            NativeBlueprintWorldMatchChecks.AwaitMovable(context, host, source,512*128,32,512*128);
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
        private static void OperationOwnership(object context, object host, object input, LibraryState library)
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
            object original = Scene(host)[0]; NativeBlueprintWorldMatchChecks.AwaitMovable(context, host, original);
            var window = Get(original, "Window"); var mask = Get(original, "Mask"); long order = (long)Get(original, "CommitOrder");
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
        internal static LibraryEntry Save(LibraryState library, CaptureCell[] cells, int width, int height)
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
