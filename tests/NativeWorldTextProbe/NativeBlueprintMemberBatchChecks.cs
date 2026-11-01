using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual native capture/profile and the production file/worker owners are
    // the consumers. The wrapper counts real semantic calls; it supplies no
    // successful stand-in layouts or member answers.
    internal static class NativeBlueprintMemberBatchChecks
    {
        private const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const int Width = 2050, Platforms = 48, Hinge = 2047, Left = 37, Top = 30;
        private readonly struct MemberWork
        {
            internal MemberWork(long records, long blocks) { RecordVisits = records; BlockScans = blocks; }
            internal long RecordVisits { get; }
            internal long BlockScans { get; }
        }

        private sealed class CountedProfile : IBlueprintHorizontalMirrorProfile
        {
            private readonly IBlueprintHorizontalMirrorProfile original;
            internal readonly Dictionary<int, int> Calls = new Dictionary<int, int>();
            internal CountedProfile(IBlueprintHorizontalMirrorProfile original) { this.original = original; }
            public long Revision { get { return original.Revision; } }
            public bool Ready { get { return original.Ready; } }
            internal void Reset() { Calls.Clear(); }
            private void Count(int id) { int count; Calls.TryGetValue(id, out count); Calls[id] = count + 1; }
            public bool TryMirrorCell(CaptureCell source, int width, out CaptureCell target, out string reason)
            { return original.TryMirrorCell(source, width, out target, out reason); }
            public bool TryNormalizeObject(CaptureObject source, IReadOnlyList<CaptureCell> members, out CaptureObject target, out string reason)
            { Count(source.Id); return original.TryNormalizeObject(source, members, out target, out reason); }
            public bool TryMirrorObject(CaptureObject source, IReadOnlyList<CaptureCell> members, int width,
                out CaptureObject target, out CaptureCell[] targets, out string reason)
            { Count(source.Id); return original.TryMirrorObject(source, members, width, out target, out targets, out reason); }
        }

        internal static void Run(object context)
        {
            NativeBlueprintWorldMatchChecks.InitializeMetadata();
            Assembly host = Get(context, "Blueprint").GetType().Assembly;
            var resolver = host.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintHorizontalMirrorResolver", true);
            var profile = new CountedProfile((IBlueprintHorizontalMirrorProfile)resolver.GetMethod("Create", F).Invoke(null, null));
            Tile[,] oldTiles = Main.tile; int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY, oldMode = Main.netMode;
            var oldSections = Main.sectionManager;
            string path = Path.Combine(Terraria.Program.SavePath, "members-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Main.netMode = 0; Main.maxTilesX = 2200; Main.maxTilesY = 100; Main.tile = new Tile[2200, 100];
                for (int x = 0; x < 2200; x++) for (int y = 0; y < 100; y++) Main.tile[x, y] = new Tile();
                for (int x = 0; x < Width; x++) { var tile = Main.tile[Left + x, Top]; tile.active(true); tile.type = 1; }
                for (int n = 0; n < Platforms; n++)
                {
                    var tile = Main.tile[Left + n * 2, Top]; tile.type = 19; tile.frameX = 18; tile.frameY = 36;
                    tile.wall = 7; tile.wallColor(4); tile.wire(true);
                    if (n == 0) { tile.wire3(true); tile.actuator(true); tile.liquid = 71; tile.liquidType(2); tile.slope(1); }
                }
                for (int y = 0; y < 3; y++)
                {
                    var tile = Main.tile[Left + Hinge, Top + y]; tile.active(true); tile.type = 10;
                    tile.frameX = (short)(y * 18); tile.frameY = (short)(y * 18); tile.color((byte)(y + 1));
                }
                Main.tile[Left + Hinge - 1, Top].wall = 7; Main.tile[Left + Hinge - 1, Top].wallColor(9);
                var sourceType = host.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
                var source = (IBlueprintCaptureSource)Activator.CreateInstance(sourceType, F, null, new object[] { (Func<bool>)(() => true) }, null);
                using (var library = new LibraryState(new FileBlueprintLibrary(path)))
                {
                    var entry = Capture(library, source);
                    var identity = new BlueprintProjectionIdentity(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash, 31, 1);
                    Require(library.RequestProjectionOpen(identity), "member probe authenticates a real saved template");
                    var opened = Projection(library, BlueprintProjectionReceiptKind.Opened);
                    Require(library.RequestProjectionWindow(identity, 1, new BlueprintProjectionBounds(0, 0, Width, 3)), "member probe requests its finite real source window");
                    var raw = Projection(library, BlueprintProjectionReceiptKind.Window).Window;
                    Require(raw.Cells.Count == Width + 2 && raw.Objects.Count == Platforms + 1 && raw.Objects.All(o => o.Reliable), "native capture has 48 positive one-cell platforms and a complete cross-block door");
                    Require(raw.Objects.Count(o => o.Type == 19 && o.Width == 1 && o.Height == 1 && o.Id > 0) == Platforms &&
                        raw.Summary.Left == Left && raw.Summary.Top == Top, "source identity uses actual platform metadata and nonzero raw origin");
                    var platforms = raw.Objects.Where(o => o.Type == 19).OrderBy(o => o.X).ToArray();
                    int door = raw.Objects.Single(o => o.Type == 10).Id;
                    foreach (bool mirrored in new[] { false, true })
                    {
#if DEBUG
                        var small = SourceCase(library, opened, raw, profile, platforms, door, mirrored, 1, 100 + (mirrored ? 10 : 0));
                        var many = SourceCase(library, opened, raw, profile, platforms, door, mirrored, Platforms, 101 + (mirrored ? 10 : 0));
                        Require(small.RecordVisits == many.RecordVisits && small.BlockScans == many.BlockScans,
                            "one versus 48 selected IDs in the same finite batch visits the same two physical member blocks");
#else
                        SourceCase(library, opened, raw, profile, platforms, door, mirrored, 1, 100 + (mirrored ? 10 : 0));
                        SourceCase(library, opened, raw, profile, platforms, door, mirrored, Platforms, 101 + (mirrored ? 10 : 0));
#endif
                        Qualification(library, raw, profile, mirrored);
                    }
                    ExplicitMirrorAndTrim(library, raw, profile);
#if DEBUG
                    CancelCollector(library, opened, raw, profile);
#endif
                    Require(library.RequestProjectionWindow(identity, 2, new BlueprintProjectionBounds(0, 0, Width, 3)), "original raw template remains readable after every consumer");
                    var unchanged = Projection(library, BlueprintProjectionReceiptKind.Window).Window;
                    Require(unchanged.Cells.Single(c => c.X == 0 && c.Y == 0).FrameX == 18 &&
                        unchanged.Cells.Single(c => c.X == 0 && c.Y == 0).Slope == 1 && unchanged.Cells.Count == Width + 2,
                        "temporary member interpretation never rewrites the saved source or its raw frames");
                    library.ReleaseProjection(identity);
#if DEBUG
                    NativeQuickItemChecks.Until(() => library.DebugProjectionStatistics.SourceIndexBytes == 0 &&
                        library.DebugProjectionMemberBatchStatistics.WorkingBytes == 0 && library.DebugProjectionSourceStatistics.PendingRequests == 0);
#endif
                }
                Console.WriteLine("PASS Blueprint member batches: native capture/save/open, ordinary and mirrored nonempty View, 1 versus 48 positive platforms with walls/wires, shared full-object/9-cell door contexts, cross-block membership, qualification, explicit mirror, incremental trim/freeze, raw ordinal and collector cancellation. CPU/file/worker evidence; FPS, GPU, ordinary game load and multiplayer NOT_RUN.");
            }
            finally { Main.tile = oldTiles; Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight; Main.netMode = oldMode; Main.sectionManager = oldSections; }
        }

        private static LibraryEntry Capture(LibraryState library, IBlueprintCaptureSource source)
        {
            var selection = new BlueprintSelection(); selection.ToggleRectangle(Left, Top, Left + Width - 1, Top, 2200, 100);
            selection.ToggleRectangle(Left + Hinge, Top + 1, Left + Hinge, Top + 2, 2200, 100);
            var token = new CaptureToken(Guid.NewGuid(), 31, 1, 1); var handle = new ContentHandle(token.OperationId);
            Require(Finish(library, library.TryBeginStage(token)).Status == LibraryStatus.Completed, "member capture begins detached stage");
            var capture = new CaptureJob(source, selection.Freeze(), 0);
            for (int n = 0; n < 20000 && !capture.Complete && capture.Error == null; n++)
            {
                capture.Advance(64, 20);
                if (capture.PendingChunk == null) continue;
                Require(Finish(library, library.TryAppendChunk(handle, capture.PendingChunk)).Status == LibraryStatus.Completed, "member raw chunk reaches the real library worker");
                capture.AcceptChunk();
            }
            Require(capture.Complete && capture.Error == null, "member native capture completes: " + capture.Error);
            var summary = capture.Summary(10);
            Require(summary.Width == Width && summary.Height == 3 && summary.ContentCount == Width + 2 && summary.UnreliableObjectCount == 0, "finite native capture retains all independent expected members");
            Finish(library, library.TrySealDraft(handle, summary));
            var saved = Finish(library, library.TryCreate(handle, "成员批次原版样本", token)); Require(saved.Status == LibraryStatus.Saved, "member fixture has reliable Saved identity");
            return Finish(library, library.TryList(1, 0, saved.AssetId)).Page.Entries.Single(e => e.AssetId == saved.AssetId);
        }

        private static MemberWork SourceCase(LibraryState library, BlueprintProjectionReceipt opened, BlueprintProjectionWindow raw, CountedProfile profile,
            CaptureObject[] platforms, int door, bool mirrored, int selected, long generation)
        {
            var excluded = new HashSet<int>(platforms.Skip(selected).Select(o => o.Id));
            var mask = (BlueprintProjectionMask)typeof(BlueprintProjectionMask).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single()
                .Invoke(new object[] { new Dictionary<long, ushort>(), excluded, (long)excluded.Count, (long)excluded.Count });
            var view = new BlueprintProjectionView(raw.Summary, mirrored, mask, profile);
            int ordinary = Enumerable.Range(0, opened.MatchRanges.Count).Single(i => opened.MatchRanges[i].ObjectId == 0 && opened.MatchRanges[i].Contains(0, 0));
            var wanted = new HashSet<int>(platforms.Take(selected).Select(o => o.Id)) { door };
            var demands = new List<BlueprintProjectionSourceDemand> { new BlueprintProjectionSourceDemand(ordinary) };
            demands.AddRange(Enumerable.Range(0, opened.MatchRanges.Count).Where(i => wanted.Contains(opened.MatchRanges[i].ObjectId)).Select(i => new BlueprintProjectionSourceDemand(i)));
            profile.Reset();
#if DEBUG
            var before = library.DebugProjectionMemberBatchStatistics;
#endif
            Require(library.RequestProjectionSources(raw.Identity, generation, demands, view: view), "nonempty formal View source request accepted");
            var receipt = Source(library); Require(receipt.Parts.Count == selected + 2 && ReferenceEquals(receipt.View, view), "ordinary, selected objects and door share the same accepted request");
            var ordinaryPart = receipt.Parts[0];
            Require(ordinaryPart.Complete && ordinaryPart.Offset == 0 && ordinaryPart.NextOffset == CaptureChunk.MaximumRecords &&
                ordinaryPart.Window.CellsInSourceOrder && ordinaryPart.Window.SourceWindow.Cells.Count == 2047 && ordinaryPart.Window.Objects.Count == 0,
                "ordinary output keeps the accepted physical raw prefix/NextOffset, natural Tile targets and non-Tile object context");
            Require(ordinaryPart.Window.SourceWindow.Cells.Select(c=>c.X).SequenceEqual(Enumerable.Range(0,2047)) &&
                ordinaryPart.Window.SourceWindow.Cells.All(c=>c.Y==0) && ordinaryPart.Window.SourceWindow.Cells.Count(c=>c.ObjectId==0 && (c.Layers&CaptureLayer.Tile)!=0)==1999,
                "the literal saved first block keeps coordinates zero through 2046 and all 1999 natural Stone Tiles; only the pure-Tile door member is stripped");
            var first = ordinaryPart.Window.Cells.Single(c => c.X == (mirrored ? Width - 1 : 0) && c.Y == 0);
            Require(first.ObjectId == platforms[0].Id && (first.Layers & CaptureLayer.Tile) == 0 && first.WallType == 7 && first.WallPaint == 4 &&
                first.Wires == 5 && first.Actuator && first.LiquidAmount == 71 && first.LiquidKind == 2 && first.FrameX == (mirrored ? 36 : 18) && first.Slope == (mirrored ? 2 : 1),
                "ordinary platform context retains canonical appearance and every same-cell selected layer");
            var doorPart = receipt.Parts.Single(p => p.LogicalObjectId == door); var context = doorPart.DoorContexts.Single();
            Require(doorPart.Window.Cells.Count == 3 && !doorPart.Window.CellsInSourceOrder && doorPart.Window.Objects.Single().X == (mirrored ? 2 : Hinge) &&
                doorPart.Window.Cells.Select(c => c.FrameX).SequenceEqual(new short[] { 0, 18, 36 }), "cross-block door is complete display row-major with independent row decorations");
            Require(context.Cells.Count == 9 && context.Cells.Count(c => !c.HasContent) == 4 && context.Cells.Single(c => c.X == (mirrored ? 3 : Hinge - 1) && c.Y == 0).WallPaint == 9,
                "all known empty door neighbours and exact wall context survive shared interpretation");
            Require(ordinaryPart.DoorContexts.Count == 1 && ordinaryPart.DoorContexts[0].Cells.Count == 9 &&
                profile.Calls.Count == selected + 1 && profile.Calls.All(pair => pair.Value == 1), "ordinary, full objects and repeated door contexts interpret each ID exactly once");
#if DEBUG
            var after = library.DebugProjectionMemberBatchStatistics;
            Require(after.Batches - before.Batches == 1 && after.WorkingBytes == 0 && after.WorkingPeakBytes > 0,
                "one finite batch is observable and releases its temporary body before receipt publication");
            Console.WriteLine("Blueprint members pose=" + mirrored + " ids=" + (selected + 1) + " records=" + (after.RecordVisits - before.RecordVisits) +
                " blocks=" + (after.BlockScans - before.BlockScans) + " interpretations=" + (after.Interpretations - before.Interpretations) + " receiptBytes=" + receipt.EstimatedBytes);
            return new MemberWork(after.RecordVisits - before.RecordVisits, after.BlockScans - before.BlockScans);
#else
            return default;
#endif
        }

        private static void Qualification(LibraryState library, BlueprintProjectionWindow raw, CountedProfile profile, bool mirrored)
        {
            // A deliberately incomplete producer catalog is a valid input: the
            // real classifier must preserve Unknown, not fabricate producers.
            var catalog = new BlueprintPlacementCatalog(mirrored ? 702 : 701, Array.Empty<BlueprintPlacementRule>(),
                new Dictionary<int, BlueprintPlacementDescriptor>(), new Dictionary<int, int>(), new Dictionary<int, int>(), 0, 0);
            var view = new BlueprintProjectionView(raw.Summary, mirrored, BlueprintProjectionMask.Empty, profile); profile.Reset();
#if DEBUG
            var before = library.DebugProjectionMemberBatchStatistics;
#endif
            Require(library.RequestProjectionQualification(raw.Identity, catalog, BlueprintProjectionMask.Empty, view), "qualification real owner accepts the cold catalog/profile/pose");
            BlueprintProjectionQualificationReceipt receipt = null;
            NativeQuickItemChecks.Until(() => { AssertNoFailure(library); return library.TryTakeProjectionQualification(out receipt); });
            Require(receipt.TotalRequiredLayerCount == 2151 && receipt.EffectiveRequiredLayerCount == 2151 && receipt.EffectiveUnknownLayerCount == 2101,
                "native source keeps independent Tile/wall/wire/actuator denominators and honest incomplete-producer Unknown");
            Require(profile.Calls.Count == Platforms + 1 && profile.Calls.All(pair => pair.Value == 1), "qualification caches only the small same-catalog/profile/pose result across later member blocks");
#if DEBUG
            var after = library.DebugProjectionMemberBatchStatistics;
            Require(after.Batches - before.Batches == 1 && after.WorkingBytes == 0 && after.BlockScans - before.BlockScans <= 4,
                "first qualification discovers and collects the cross-block member union in fixed stages");
#endif
        }

        private static void ExplicitMirrorAndTrim(LibraryState library, BlueprintProjectionWindow raw, CountedProfile profile)
        {
            var mirrored = new BlueprintProjectionPlacement(raw.Identity, raw.Summary, 0, 0, 1, mirroredHorizontally: true, mirrorProfile: profile); profile.Reset();
#if DEBUG
            long batches = library.DebugProjectionMemberBatchStatistics.Batches;
#endif
            var mirror = Commit(library, new BlueprintProjectionCommitRequest(31, 200, 1, mirrored, Array.Empty<BlueprintProjectionPlacement>(), BlueprintProjectionEditKind.Mirror));
            Require(mirror.Succeeded && profile.Calls.Count == Platforms + 1 && profile.Calls.All(pair => pair.Value == 1), "explicit mirror validates each complete native object exactly once through its real commit lane");
#if DEBUG
            Require(library.DebugProjectionMemberBatchStatistics.Batches - batches == 1 && library.DebugProjectionMemberBatchStatistics.WorkingBytes == 0, "explicit mirror releases its one finite body batch");
#endif
            var placement = new BlueprintProjectionPlacement(raw.Identity, raw.Summary, 0, 0, 1, mirrorProfile: profile);
#if DEBUG
            batches = library.DebugProjectionMemberBatchStatistics.Batches;
#endif
            var accumulated = Commit(library, new BlueprintProjectionCommitRequest(31, 201, 1, placement, Array.Empty<BlueprintProjectionPlacement>(),
                BlueprintProjectionEditKind.Trim, new BlueprintProjectionBounds(0, 0, Width, 1)));
            Require(accumulated.Succeeded && accumulated.TrimPlan.Mask == null && accumulated.TrimPlan.RemovedLayerCount == 2152 &&
                accumulated.TrimPlan.RemovedConstructionLayerCount == 2151 && accumulated.TrimPlan.ObjectBounds.Count == Platforms + 1,
                "trim expands only complete Tile members after exact all-layer user-row processing");
#if DEBUG
            Require(library.DebugProjectionMemberBatchStatistics.Batches - batches == 1 && library.DebugProjectionMemberBatchStatistics.WorkingBytes == 0, "trim reuses the same finite member collector");
#endif
            var frozen = Commit(library, new BlueprintProjectionCommitRequest(31, 201, 1, placement, Array.Empty<BlueprintProjectionPlacement>(),
                BlueprintProjectionEditKind.Trim, trimPhase: BlueprintProjectionTrimPhase.Freeze, expectedTrimVersion: 1, throughTrimVersion: 1, confirmedTrimRemovedCount: 2152));
            Require(frozen.Succeeded && frozen.TrimPlan.Frozen && raw.Cells.All(c => !frozen.TrimPlan.Mask.Apply(c).HasContent) &&
                ReferenceEquals(frozen.TrimPlan.OriginalMask, BlueprintProjectionMask.Empty), "one freeze combines every exact source layer with the original baseline");
            library.CancelProjectionCommit(31, 201);
            Require(frozen.TrimPlan.Mask.RemovedLayerCount == 2152, "operation cleanup cannot mutate the published final mask");
        }

#if DEBUG
        private static void CancelCollector(LibraryState library, BlueprintProjectionReceipt opened, BlueprintProjectionWindow raw, CountedProfile profile)
        {
            var worker = Get(library, "worker"); var hook = worker.GetType().GetField("projectionObjectMemberCheckpoint", F); int canceled = 0;
            var view = new BlueprintProjectionView(raw.Summary, false, BlueprintProjectionMask.Empty, profile);
            int ordinary = Enumerable.Range(0, opened.MatchRanges.Count).Single(i => opened.MatchRanges[i].ObjectId == 0 && opened.MatchRanges[i].Contains(0, 0));
            long published = library.DebugProjectionSourceStatistics.Receipts;
            try
            {
                hook.SetValue(worker, (Action)(() => { if (Interlocked.Exchange(ref canceled, 1) == 0) library.ClearProjectionSource(raw.Identity); }));
                Require(library.RequestProjectionSource(raw.Identity, 900, ordinary, view: view), "cancellation probe accepts an actual source request");
                NativeQuickItemChecks.Until(() => Volatile.Read(ref canceled) != 0 && library.DebugProjectionMemberBatchStatistics.WorkingBytes == 0 &&
                    library.DebugProjectionSourceStatistics.PendingRequests == 0 && library.DebugProjectionSourceStatistics.WorkingBytes == 0);
                BlueprintProjectionSourceReceipt late;
                Require(!library.TryTakeProjectionSource(out late) && library.DebugProjectionSourceStatistics.Receipts == published, "cancellation at a real member checkpoint publishes no stale receipt and releases temporary bytes");
            }
            finally { hook.SetValue(worker, null); }
            Require(library.RequestProjectionSource(raw.Identity, 901, ordinary, view: view), "a legal successor works after canceled collection");
            Require(Source(library).Generation == 901 && library.DebugProjectionMemberBatchStatistics.WorkingBytes == 0, "canceled batch cannot erase or contaminate its successor");
        }
#endif
        private static BlueprintProjectionCommitReceipt Commit(LibraryState library, BlueprintProjectionCommitRequest request)
        {
            NativeQuickItemChecks.Until(() => library.RequestProjectionCommit(request)); BlueprintProjectionCommitReceipt receipt = null;
            NativeQuickItemChecks.Until(() => { AssertNoFailure(library); return library.TryTakeProjectionCommit(out receipt); }); return receipt;
        }
        private static BlueprintProjectionSourceReceipt Source(LibraryState library)
        {
            BlueprintProjectionSourceReceipt receipt = null;
            NativeQuickItemChecks.Until(() => { AssertNoFailure(library); return library.TryTakeProjectionSource(out receipt); }); return receipt;
        }
        private static void AssertNoFailure(LibraryState library)
        {
            BlueprintProjectionReceipt receipt;
            while (library.TryTakeProjectionReceipt(out receipt)) Require(receipt.Kind != BlueprintProjectionReceiptKind.Failed, "member asset must not fail: " + receipt.FailureCode + " " + receipt.Error);
        }
        private static BlueprintProjectionReceipt Projection(LibraryState library, BlueprintProjectionReceiptKind kind)
        {
            BlueprintProjectionReceipt receipt = null;
            NativeQuickItemChecks.Until(() => library.TryTakeProjectionReceipt(out receipt));
            Require(receipt.Kind == kind, "member source receipt " + kind + ": " + receipt.Error); return receipt;
        }
        private static LibraryReceipt Finish(LibraryState library, bool accepted)
        {
            Require(accepted, "member fixture file request accepted"); LibraryReceipt receipt = null;
            NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out receipt); });
            Require(receipt.Status != LibraryStatus.Failed && receipt.Status != LibraryStatus.CommitUnconfirmed && receipt.Status != LibraryStatus.Protected, "member fixture file receipt: " + receipt.Error);
            return receipt;
        }
    }
}
