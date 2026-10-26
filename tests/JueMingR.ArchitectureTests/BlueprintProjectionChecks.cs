using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintProjectionChecks
    {
        internal static void Check(IList<string> failures)
        {
            string area = Path.Combine(Path.GetTempPath(), "JueMingR-blueprint-projection-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(area);
            int before = failures.Count;
            Run(failures, "local coordinates and all layers", () => Mixed(Path.Combine(area, "mixed")));
            Run(failures, "X bounded wide rows and stationary reuse", () => Shapes(Path.Combine(area, "shapes")));
            Run(failures, "open fairness latest retirement and failure", () => Scheduling(Path.Combine(area, "scheduling")));
            Run(failures, "shared instances and atomic layer/object delta", () => Scene(Path.Combine(area, "scene")));
            Run(failures, "overlap reader retirement rejects late delta", () => CommitRetirement(Path.Combine(area, "commit-retirement")));
            Run(failures, "shared request saturation and eventual progress", () => Saturation(Path.Combine(area, "saturation")));
            Run(failures, "last reference retirement during reader disposal", () => LastReferenceWake(Path.Combine(area, "last-reference-wake")));
            Run(failures, "sparse overlap spans skip empty rows", () => SparseSpan(Path.Combine(area, "sparse-span")));
            Run(failures, "65 blocks retain intersection", () => CacheIntersection(Path.Combine(area, "cache-intersection")));
            Run(failures, "delete handle and commit boundaries", () => Deletion(Path.Combine(area, "deletion")));
            Run(failures, "sparse source slices remain independent of viewport latest", () => MatchSources(Path.Combine(area, "match-sources")));
            Run(failures, "source batches preserve members layers prefix and lifetime", () => SourceBatches(Path.Combine(area, "source-batches")));
            if (before == failures.Count)
            {
                string resolved = Path.GetFullPath(area), parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!resolved.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("JueMingR-blueprint-projection-", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe projection test cleanup.");
                Directory.Delete(resolved, true);
            }
            else failures.Add("BlueprintProjection failure material retained: " + area);
        }
        private static void Run(IList<string> failures, string name, Action check) { try { check(); } catch (Exception e) { failures.Add("BlueprintProjection " + name + ": " + e); } }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Until(Func<bool> condition)
        { var clock = Stopwatch.StartNew(); while (!condition()) { if (clock.ElapsedMilliseconds > 30000) throw new TimeoutException("Projection work did not reach its bounded test boundary."); Thread.Sleep(1); } }
        private static BlueprintProjectionReceipt Await(LibraryState state, BlueprintProjectionReceiptKind kind, long generation = -1)
        {
            BlueprintProjectionReceipt found = null;
            Until(() => { BlueprintProjectionReceipt value; if (!state.TryTakeProjectionReceipt(out value)) return false; if (value.Kind == BlueprintProjectionReceiptKind.Failed) { found = value; return true; } if (value.Kind != kind || generation >= 0 && value.WindowGeneration != generation) return false; found = value; return true; });
            return found;
        }
        private static LibraryReceipt Library(LibraryState state, bool accepted)
        {
            Require(accepted, "ordinary library command remains independently admissible"); LibraryReceipt found = null;
            Until(() => { state.Pump(); return state.TryTakeReceipt(out found); }); return found;
        }
        private static BlueprintProjectionIdentity Open(LibraryState state, Fixture fixture, long selection = 1, long session = 11)
        {
            var identity = new BlueprintProjectionIdentity(fixture.Id, fixture.Revision, fixture.Hash, session, selection);
            Require(state.RequestProjectionOpen(identity), "open accepted"); BlueprintProjectionReceipt receipt = Await(state, BlueprintProjectionReceiptKind.Opened);
            Require(receipt.Kind == BlueprintProjectionReceiptKind.Opened && receipt.Identity.Equals(identity), "full original v1 proof opens exact identity: " + receipt.FailureCode); return identity;
        }
        private static BlueprintProjectionWindow Window(LibraryState state, BlueprintProjectionIdentity identity, long generation, BlueprintProjectionBounds bounds)
        {
            Require(state.RequestProjectionWindow(identity, generation, bounds), "bounded latest window accepted"); BlueprintProjectionReceipt receipt = Await(state, BlueprintProjectionReceiptKind.Window, generation);
            Require(receipt.Kind == BlueprintProjectionReceiptKind.Window && receipt.Window != null, "only complete halo publishes: " + receipt.FailureCode); return receipt.Window;
        }
        private static CaptureCell Tile(int x, int y) { return new CaptureCell { X = x, Y = y, Layers = CaptureLayer.Tile, TileType = 1, FrameX = 54, FrameY = 72 }; }
        private static void Mixed(string path)
        {
            CaptureCell[] cells = { Tile(37, 91), new CaptureCell { X = 38, Y = 91, Layers = CaptureLayer.Wall, WallType = 2 }, new CaptureCell { X = 39, Y = 91, Layers = CaptureLayer.Wire, Wires = 5 }, new CaptureCell { X = 40, Y = 91, Layers = CaptureLayer.Actuator, Actuator = true }, new CaptureCell { X = 41, Y = 91, Layers = CaptureLayer.Liquid, LiquidAmount = 64, LiquidKind = 2 }, Tile(42, 91), Tile(43, 91), Tile(42, 92), Tile(43, 92) };
            for (int i = 5; i < cells.Length; i++) { cells[i].ObjectId = 17; cells[i].TileType = 10; cells[i].FrameX = (short)(100 + (cells[i].X - 42) * 18); cells[i].FrameY = (short)(200 + (cells[i].Y - 91) * 20); }
            var obj = new CaptureObject { Id = 17, X = 42, Y = 91, Width = 2, Height = 2, Type = 10, Reliable = true, Direction = 2, RandomVariant = 3 };
            Fixture fixture = Write(path, 7, 2, cells.LongLength, n => cells[n], new[] { obj });
            using (var counted = new CountedFiles(path)) using (var state = new LibraryState(counted))
            {
                BlueprintProjectionIdentity identity = Open(state, fixture); var bounds = new BlueprintProjectionBounds(-5, -5, 17, 12);
                BlueprintProjectionWindow window = Window(state, identity, 1, bounds);
                Require(window.Summary.Left == 37 && window.Summary.Top == 91 && window.Summary.Width == 7 && window.Summary.Height == 2, "saved origin retained separately from normalized cells");
                Require(window.Cells.Count == 9 && window.Cells[0].X == 0 && window.Cells[0].Y == 0 && window.Cells[0].FrameX == 54 && window.Cells[0].FrameY == 72, "local positions preserve literal raw frames");
                Require(window.EstimatedBytes >= window.Cells.Count * 64L + window.Objects.Count * 128L, "window payload is accounted independently of block cache");
                Require(window.Cells[1].Layers == CaptureLayer.Wall && window.Cells[2].Layers == CaptureLayer.Wire && window.Cells[3].Layers == CaptureLayer.Actuator && window.Cells[4].Layers == CaptureLayer.Liquid, "tile-free saved layers all survive");
                Require(window.Objects.Count == 1 && window.Objects[0].X == 5 && window.Objects[0].Y == 0 && window.Objects[0].Direction == 2 && window.Objects[0].RandomVariant == 3, "object metadata normalized without style/frame rewriting");
                BlueprintProjectionWindow edge = Window(state, identity, 2, new BlueprintProjectionBounds(6, 0, 1, 2));
                Require(edge.Cells.Count == 2 && edge.Objects.Count == 1 && edge.Objects[0].X == 5, "visible member resolves object whose origin is outside query");
                long blocks = counted.Reads, indexes = counted.IndexReadBytes;
                LibraryReceipt rename = Library(state, state.TryRename(fixture.Id, Library(state, state.TryList(1, 0)).Page.Entries[0].MetadataIdentity, "改名"));
                Require(rename.Status == LibraryStatus.Saved && counted.Reads == blocks && counted.IndexReadBytes >= indexes, "rename does not reparse content or retire window; status=" + rename.Status + " error=" + rename.Error + " failure=" + rename.FailureCode + " blockReads=" + blocks + "->" + counted.Reads + " indexBytes=" + indexes + "->" + counted.IndexReadBytes);
                state.CloseProjection(); Until(() => Volatile.Read(ref counted.ActiveIndexes) == 0);
            }
            string brokenPath = Path.Combine(path, "incomplete-object"); Fixture broken = Write(brokenPath, 7, 2, 8, n => cells[n], new[] { obj });
            using (var state = new LibraryState(new FileBlueprintLibrary(brokenPath)))
            {
                Require(state.RequestProjectionOpen(new BlueprintProjectionIdentity(broken.Id, broken.Revision, broken.Hash, 1, 1)), "incomplete literal accepted only for validation");
                BlueprintProjectionReceipt rejected = Await(state, BlueprintProjectionReceiptKind.Failed);
                Require(rejected.Kind == BlueprintProjectionReceiptKind.Failed && rejected.FailureCode == "blueprint-missing-object-member", "projection open retains full old v1 rectangular-member validation");
            }
        }
        private static void Shapes(string path)
        {
            foreach (int width in new[] { 8192, 262144 })
            {
                string part = Path.Combine(path, "wide-" + width); Fixture fixture = Write(part, width, 1, width, n => Tile(37 + (int)n, 91));
                using (var counted = new CountedFiles(part)) using (var state = new LibraryState(counted))
                {
                    BlueprintProjectionIdentity identity = Open(state, fixture); long reads = counted.Reads, bytes = counted.IndexReadBytes;
#if DEBUG
                    BlueprintProjectionStatistics prior = state.DebugProjectionStatistics;
#endif
                    BlueprintProjectionWindow window = Window(state, identity, 1, new BlueprintProjectionBounds(1000, 0, 30, 1));
                    Require(window.Cells.Count == 30 && window.Cells[0].X == 1000 && counted.Reads - reads <= 1, "same narrow X range reads at most one cell block despite offscreen row growth");
                    Require(counted.IndexReadBytes - bytes <= 32, "window reads only selected block hash, never rehashes complete index");
#if DEBUG
                    BlueprintProjectionStatistics after = state.DebugProjectionStatistics;
                    Require(after.WindowBlocks - prior.WindowBlocks == 1 && after.DecodedRecords - prior.DecodedRecords <= 2048 && after.DirectoryComparisons - prior.DirectoryComparisons <= 9, "one block and logarithmic directory work for fixed visible width");
                    Require(after.CachePeakBytes <= 8L * 1024 * 1024, "decoded cache budget is measured in charged bytes");
                    Require(after.DirectoryBlocks == width / 2048 && after.DirectoryBytes <= 256L + after.DirectoryBlocks * 256L, "query directory plus two bounded source/proof range lists store O(blocks) metadata rather than per-cell entries");
                    Console.WriteLine("BlueprintProjection wide=" + width + " blocks=" + after.DirectoryBlocks + " directoryBytes=" + after.DirectoryBytes + " openActualBlockReads=" + reads + " windowCells=" + window.Cells.Count + " windowObjects=" + window.Objects.Count + " windowEstimatedBytes=" + window.EstimatedBytes + " windowBlockHits=" + (after.WindowBlocks - prior.WindowBlocks) + " windowActualBlockReads=" + (counted.Reads - reads) + " decodedRecords=" + (after.DecodedRecords - prior.DecodedRecords) + " comparisons=" + (after.DirectoryComparisons - prior.DirectoryComparisons) + " cachePeakBytes=" + after.CachePeakBytes);
#endif
                    long steady = counted.Reads; Thread.Sleep(15); Require(counted.Reads == steady, "stationary display has no periodic file read");
                    Window(state, identity, 2, new BlueprintProjectionBounds(width - 50, 0, 20, 1));
                    long returning = counted.Reads; Window(state, identity, 3, new BlueprintProjectionBounds(1000, 0, 30, 1)); Require(counted.Reads == returning, "moving origin/camera back reuses immutable block cache");
                }
            }
            foreach (bool sparse in new[] { false, true })
            {
                string part = Path.Combine(path, sparse ? "sparse" : "tall"); int height = sparse ? 1000000 : 131072;
                Fixture fixture = Write(part, sparse ? 1000000 : 1, height, 131072, n => Tile(37 + (sparse ? (int)(n % 2) * 999999 : 0), 91 + (sparse ? (int)(n * 7) : (int)n)));
                using (var counted = new CountedFiles(part)) using (var state = new LibraryState(counted))
                {
                    BlueprintProjectionIdentity identity = Open(state, fixture); long reads = counted.Reads;
#if DEBUG
                    BlueprintProjectionStatistics prior = state.DebugProjectionStatistics;
#endif
                    BlueprintProjectionWindow window = Window(state, identity, 1, new BlueprintProjectionBounds(0, 10, 8, 10));
                    Require(window.Cells.Count == (sparse ? 1 : 10) && counted.Reads - reads <= 2, "tall/sparse query decodes bounded intersecting blocks");
#if DEBUG
                    BlueprintProjectionStatistics after = state.DebugProjectionStatistics;
                    Console.WriteLine("BlueprintProjection " + (sparse ? "sparse" : "tall") + " height=" + height + " cells=" + window.Cells.Count + " objects=" + window.Objects.Count + " windowEstimatedBytes=" + window.EstimatedBytes + " directoryBytes=" + after.DirectoryBytes + " windowBlockHits=" + (after.WindowBlocks - prior.WindowBlocks) + " windowActualBlockReads=" + (counted.Reads - reads) + " decodedRecords=" + (after.DecodedRecords - prior.DecodedRecords) + " comparisons=" + (after.DirectoryComparisons - prior.DirectoryComparisons) + " cachePeakBytes=" + after.CachePeakBytes);
#endif
                }
            }
        }
        private static void Scheduling(string path)
        {
            Fixture fixture = Write(path, 262144, 1, 262144, n => Tile(37 + (int)n, 91));
            using (var counted = new CountedFiles(path)) using (var state = new LibraryState(counted))
            {
                Guid stage = Guid.NewGuid(); var token = new CaptureToken(stage, 70, 71, 72); var handle = new ContentHandle(stage);
                Require(Library(state, state.TryBeginStage(token)).Status == LibraryStatus.Completed, "unrelated capture stage starts");
                Require(Library(state, state.TryAppendChunk(handle, new CaptureChunk(new[] { Tile(1, 1) }, 1))).Status == LibraryStatus.Completed, "unrelated capture chunk accepted");
                Require(Library(state, state.TrySealDraft(handle, new CaptureSummary { Left = 1, Top = 1, Width = 1, Height = 1, ContentCount = 1, StartTick = 1, EndTick = 2 })).Status == LibraryStatus.Completed, "unrelated draft is ready before long projection open");
                counted.HoldLayoutBatches = 3; var identity = new BlueprintProjectionIdentity(fixture.Id, fixture.Revision, fixture.Hash, 1, 1);
                Require(state.RequestProjectionOpen(identity), "open starts independently"); Require(counted.LayoutEntered.Wait(5000), "long open reached a layout batch");
                counted.LayoutEntered.Reset();
                Require(state.RequestProjectionWindow(identity, 0, new BlueprintProjectionBounds(262100, 0, 10, 1)), "window may be prequeued while open is still validating");
                Require(state.TryList(3, 0), "List admitted during long open"); counted.LayoutRelease.Set();
                Require(Library(state, true).Status == LibraryStatus.Completed && counted.MetadataWhileLayout > 0, "List served between open batches before the long layout finished");
                Require(counted.LayoutEntered.Wait(5000), "long open yields again before Create"); counted.LayoutEntered.Reset();
                Require(state.TryCreate(handle, "公平性保存", token), "accepted Create is not blocked by projection open"); counted.LayoutRelease.Set();
                Require(Library(state, true).Status == LibraryStatus.Saved, "accepted Create completes while projection open is paused");
                Require(counted.LayoutEntered.Wait(5000), "long open yields again before Delete"); counted.LayoutEntered.Reset();
                Require(state.TryDelete(stage, counted.LastWrittenIdentity), "accepted Delete is not blocked by projection open"); counted.LayoutRelease.Set();
                Require(Library(state, true).Status == LibraryStatus.Deleted && counted.WritesWhileLayout >= 2, "Create and Delete commits receive service before the long layout finishes");
                Require(Await(state, BlueprintProjectionReceiptKind.Opened).Kind == BlueprintProjectionReceiptKind.Opened, "open resumes after ordinary command");
                Require(Await(state, BlueprintProjectionReceiptKind.Window, 0).Kind == BlueprintProjectionReceiptKind.Window, "initial open receipt is consumed before prequeued window can replace it");
                counted.HoldBlock = 0;
                Require(state.RequestProjectionWindow(identity, 1, new BlueprintProjectionBounds(1, 0, 10, 1)), "first uncached window starts");
                Require(counted.BlockEntered.Wait(5000), "window reaches held block");
                for (int i = 2; i <= 12; i++) Require(state.RequestProjectionWindow(identity, i, new BlueprintProjectionBounds(i * 10, 0, 10, 1)), "new window replaces prior pending request");
                counted.BlockRelease.Set(); BlueprintProjectionReceipt latest = Await(state, BlueprintProjectionReceiptKind.Window, 12);
                Require(latest.Kind == BlueprintProjectionReceiptKind.Window && latest.WindowGeneration == 12 && latest.Window.Cells[0].X == 120, "only latest window publishes after delayed older request");
                state.CloseProjection(); Until(() => Volatile.Read(ref counted.ActiveIndexes) == 0);
                identity = Open(state, fixture, 2, 2); counted.FailBlock = 0;
                Require(state.RequestProjectionWindow(identity, 20, new BlueprintProjectionBounds(0, 0, 10, 1)), "failing halo requested");
                BlueprintProjectionReceipt failed = Await(state, BlueprintProjectionReceiptKind.Failed);
                Require(failed.Kind == BlueprintProjectionReceiptKind.Failed && failed.Window == null && failed.IdentityRetired, "read failure is explicit, revokes identity and never becomes a completed empty/air window");
                Require(!state.RequestProjectionSource(identity, 201, 0), "a fatal viewport read no longer owns a source reference");
                counted.FailBlock = -1; identity = Open(state, fixture, 21, 2); counted.FailBlock = 0; counted.FailCode = "blueprint-projection-window-budget";
                Require(state.RequestProjectionSource(identity, 202, 0), "source budget-named reader exception reaches the real source lane");
                failed = Await(state, BlueprintProjectionReceiptKind.Failed);
                Require(failed.IdentityRetired && failed.WindowGeneration == 0 && failed.FailureCode == counted.FailCode && !state.RequestProjectionSource(identity, 203, 0),
                    "source failure broadcasts actual identity retirement even when its code names a recoverable viewport budget refusal");
                counted.FailBlock = -1; identity = Open(state, fixture, 22, 2); counted.FailBlock = 0;
                Require(state.RequestProjectionWindow(identity, 204, new BlueprintProjectionBounds(0, 0, 10, 1)), "viewport budget refusal reaches its own request path");
                failed = Await(state, BlueprintProjectionReceiptKind.Failed);
                Require(!failed.IdentityRetired && failed.WindowGeneration == 204 && failed.FailureCode == counted.FailCode,
                    "viewport budget refusal retains identity and binds the actual viewport generation");
                counted.FailBlock = -1; counted.FailCode = null;
                Require(Window(state, identity, 205, new BlueprintProjectionBounds(0, 0, 10, 1)).Cells.Count == 10, "budget refusal keeps its live reference available for a smaller retry");
                counted.FailBlock = -1; identity = Open(state, fixture, 3, 2);
                Require(Window(state, identity, 21, new BlueprintProjectionBounds(0, 0, 10, 1)).Cells.Count == 10, "retry replaces failure with actual records");
                Require(!state.RequestProjectionWindow(new BlueprintProjectionIdentity(fixture.Id, fixture.Revision, fixture.Hash, 1, 1), 99, new BlueprintProjectionBounds(0, 0, 10, 1)), "old session/selection cannot enqueue or revive a window");
                state.CloseProjection(); Until(() => Volatile.Read(ref counted.ActiveIndexes) == 0);
                Require(!state.RequestProjectionWindow(identity, 22, new BlueprintProjectionBounds(0, 0, 10, 1)), "removed projection rejects delayed request");
                identity = Open(state, fixture, 4, 3); counted.BlockEntered.Reset(); counted.BlockRelease.Reset(); counted.HoldBlock = 0;
                Require(state.RequestProjectionWindow(identity, 30, new BlueprintProjectionBounds(0, 0, 10, 1)), "new in-flight retirement request accepted");
                Require(counted.BlockEntered.Wait(5000), "in-flight request held before removal/session replacement");
                state.CloseProjection(); var next = new BlueprintProjectionIdentity(fixture.Id, fixture.Revision, fixture.Hash, 4, 5);
                Require(state.RequestProjectionOpen(next), "new session opens while old window is still in flight"); counted.BlockRelease.Set();
                BlueprintProjectionReceipt reopened = Await(state, BlueprintProjectionReceiptKind.Opened);
                Require(reopened.Kind == BlueprintProjectionReceiptKind.Opened && reopened.Identity.Equals(next), "old delayed content cannot replace new session open");
                Require(Window(state, next, 31, new BlueprintProjectionBounds(20, 0, 10, 1)).Identity.Equals(next), "new session publishes only its own window");
                var wrongRevision = new BlueprintProjectionIdentity(fixture.Id, Guid.NewGuid(), fixture.Hash, 4, 6);
                Require(state.RequestProjectionOpen(wrongRevision), "changed revision is checked on worker");
                Require(Await(state, BlueprintProjectionReceiptKind.Failed).FailureCode == "blueprint-projection-identity", "old metadata cannot validate a changed revision");
            }
        }
        private static void Deletion(string path)
        {
            foreach (int mode in new[] { 0, 1, 2 })
            {
                string part = Path.Combine(path, mode.ToString()); Fixture fixture = Write(part, 32, 1, 32, n => Tile(37 + (int)n, 91));
                using (var counted = new CountedFiles(part)) using (var state = new LibraryState(counted))
                {
                    BlueprintProjectionIdentity identity = Open(state, fixture); Window(state, identity, 1, new BlueprintProjectionBounds(0, 0, 32, 1));
                    LibraryEntry entry = Library(state, state.TryList(3, 0)).Page.Entries[0]; counted.DeleteMode = mode;
                    LibraryReceipt deleted = Library(state, state.TryDelete(entry.AssetId, entry.MetadataIdentity));
                    Require(counted.IndexesAtMetadataWrite == 0 && Volatile.Read(ref counted.ActiveIndexes) == 0, "current index released before delete metadata commit/cleanup");
                    if (mode == 0) Require(deleted.Status == LibraryStatus.Deleted, "successful delete retires content and handle; status=" + deleted.Status + " error=" + deleted.Error + " failure=" + deleted.FailureCode);
                    else if (mode == 1)
                    {
                        Require(deleted.Status == LibraryStatus.Protected && !deleted.Error.Contains("无法确认"), "known uncommitted delete stays distinguishable");
                        Open(state, fixture, 2); Require(Window(state, new BlueprintProjectionIdentity(fixture.Id, fixture.Revision, fixture.Hash, 11, 2), 2, new BlueprintProjectionBounds(0, 0, 2, 1)).Cells.Count == 2, "known precommit failure may reopen verified old content");
                    }
                    else
                    {
                        Require(deleted.Status == LibraryStatus.CommitUnconfirmed, "unknown real commit is preserved as unknown");
                        Require(state.RequestProjectionOpen(new BlueprintProjectionIdentity(fixture.Id, fixture.Revision, fixture.Hash, 11, 2)), "unknown open reaches worker rejection");
                        BlueprintProjectionReceipt refused = Await(state, BlueprintProjectionReceiptKind.Failed);
                        Require(refused.FailureCode == "blueprint-projection-delete-unconfirmed", "unknown delete blocks further reads of that ID");
                    }
                }
            }
        }

        private static BlueprintProjectionCommitReceipt Commit(LibraryState state, BlueprintProjectionCommitRequest request)
        {
            Require(state.RequestProjectionCommit(request), "pure scene computation admitted independently of ordinary library slot");
            BlueprintProjectionCommitReceipt receipt = null; Until(() => state.TryTakeProjectionCommit(out receipt));
            Require(receipt.Succeeded, "complete overlap succeeded: " + receipt.Error); return receipt;
        }
        private static void Scene(string path)
        {
            CaptureCell all = Tile(37, 91); all.Layers |= CaptureLayer.Wall | CaptureLayer.Wire | CaptureLayer.Liquid | CaptureLayer.Actuator; all.WallType = 2; all.Wires = 15; all.LiquidAmount = 40; all.Actuator = true; all.ObjectId = 7;
            CaptureCell member = Tile(38, 91); member.ObjectId = 7; member.Layers |= CaptureLayer.Wall; member.WallType = 2;
            Fixture old = Write(path, 2, 1, 2, n => n == 0 ? all : member, new[] { new CaptureObject { Id = 7, X = 37, Y = 91, Width = 2, Height = 1, Type = 1, Reliable = true } });
            CaptureCell top = Tile(37, 91); top.Layers |= CaptureLayer.Wire; top.Wires = 5;
            Fixture incoming = Write(path, 1, 1, 1, n => top);
            using (var counted = new CountedFiles(path)) using (var state = new LibraryState(counted))
            {
                BlueprintProjectionIdentity a = Open(state, old, 1), b = Open(state, old, 2), c = Open(state, incoming, 3);
                Require(counted.ActiveIndexes == 2, "same-template instances share a single retained index; different template has its own immutable reader");
                BlueprintProjectionWindow aw = Window(state, a, 1, new BlueprintProjectionBounds(0, 0, 1, 1));
                long reads = counted.Reads;
                BlueprintProjectionWindow bw = Window(state, b, 2, new BlueprintProjectionBounds(0, 0, 1, 1));
                Require(aw.Cells.Count == 1 && bw.Cells.Count == 1, "published windows exclude the second object member; complete-object removal must query beyond them");
                Require(counted.Reads == reads && aw.Identity.ProjectionGeneration != bw.Identity.ProjectionGeneration, "same-template block cache shared without sharing instance identity");
                CaptureSummary summary = aw.Summary, single = new CaptureSummary { Left = 37, Top = 91, Width = 1, Height = 1, ContentCount = 1 };
                BlueprintProjectionPlacement pa = new BlueprintProjectionPlacement(a, summary, 100, 100, 1), pb = new BlueprintProjectionPlacement(b, summary, 200, 100, 2);
                var pc = new BlueprintProjectionPlacement(c, single, 100, 100, 3);
                BlueprintProjectionCommitReceipt first = Commit(state, new BlueprintProjectionCommitRequest(11, 70, 9, pc, new[] { pa, pb }));
                Require(first.Request.SceneRevision == 9 && first.Request.TransactionId == 70 && first.Masks.Count == 1 && first.Masks.ContainsKey(1) && !first.Masks.ContainsKey(2), "one immutable qualified delta leaves same object ID in other instance untouched");
                BlueprintProjectionMask mask = first.Masks[1]; CaptureCell zero = all; zero.X = 0; zero.Y = 0; CaptureCell one = member; one.X = 1; one.Y = 0;
                CaptureCell left = mask.Apply(zero), right = mask.Apply(one);
                Require((left.Layers & CaptureLayer.Tile) == 0 && (right.Layers & CaptureLayer.Tile) == 0 && right.Layers == CaptureLayer.Wall, "one-member overlap removes entire off-overlap object Tile, preserving member Wall");
                Require(left.Wires == 10 && (left.Layers & (CaptureLayer.Wall | CaptureLayer.Liquid | CaptureLayer.Actuator)) == (CaptureLayer.Wall | CaptureLayer.Liquid | CaptureLayer.Actuator), "only same wire colors and Tile removed; missing Wall/Liquid/Actuator transparent");
                Require(mask.RemovedLayerCount == 4 && pa.Mask.RemovedLayerCount == 0 && pb.Mask.RemovedLayerCount == 0, "source masks remain byte-semantic unchanged until Host chooses atomic commit");
                var moved = new BlueprintProjectionPlacement(a, summary, 101, 100, 4, mask);
                BlueprintProjectionCommitReceipt self = Commit(state, new BlueprintProjectionCommitRequest(11, 71, 10, moved, new[] { pa }));
                Require(self.Masks.Count == 0 && moved.Mask.Apply(one).Layers == CaptureLayer.Wall, "moving self intersection excluded, inherited permanent mask follows origin");
                var maskedSource = new BlueprintProjectionPlacement(a, summary, 200, 100, 5, mask);
                BlueprintProjectionCommitReceipt second = Commit(state, new BlueprintProjectionCommitRequest(11, 72, 11, maskedSource, new[] { pb }));
                CaptureCell remains = second.Masks[2].Apply(zero);
                Require((remains.Layers & CaptureLayer.Tile) != 0 && remains.Wires == 5 && (remains.Layers & CaptureLayer.Wall) == 0, "already masked Tile/wires are no longer covering sources; visible Wall remains a covering source");
                Require(mask.Apply(zero).Wires == 10, "moving/removing covering instance cannot mutate or resurrect old mask");
                var extra = new List<BlueprintProjectionIdentity>();
                for (int i = 0; i < 40; i++) extra.Add(Open(state, old, 100 + i));
#if DEBUG
                BlueprintProjectionStatistics shared = state.DebugProjectionStatistics;
                Require(shared.ActiveReferences == 43 && shared.ActiveTemplates == 2 && shared.CacheBytes <= 8L * 1024 * 1024 && shared.PendingRequests <= 32 && shared.ReceiptBytes <= 32L * 1024 * 1024, "32 request budget is backpressure rather than an instance cap; more than 32 instances share total bounded worker storage");
                Console.WriteLine("BlueprintProjection sharedReferences=" + shared.ActiveReferences + " templates=" + shared.ActiveTemplates + " totalCacheBytes=" + shared.CacheBytes + " pendingRequests=" + shared.PendingRequests + " receiptBytes=" + shared.ReceiptBytes + " windowWorkingBytes=" + shared.WindowWorkingBytes);
#endif
                foreach (BlueprintProjectionIdentity added in extra) state.ReleaseProjection(added);
                var duplicate = Open(state, incoming, 4);
                BlueprintProjectionCommitReceipt full = Commit(state, new BlueprintProjectionCommitRequest(11, 73, 12, new BlueprintProjectionPlacement(duplicate, single, 100, 100, 6), new[] { pc }));
                Require(full.RetiredInstanceIds.Count == 1 && full.RetiredInstanceIds[0] == 3, "all remaining layer units consumed retires exactly the empty instance");
                state.ReleaseProjection(duplicate);
                state.ReleaseProjection(a); Window(state, b, 3, new BlueprintProjectionBounds(0, 0, 2, 1)); Require(counted.ActiveIndexes == 2, "one world removal keeps same-template remaining reference and unrelated template alive");
                state.ReleaseProjection(b); Until(() => Volatile.Read(ref counted.ActiveIndexes) == 1);
                state.ReleaseProjection(c); Until(() => Volatile.Read(ref counted.ActiveIndexes) == 0);
            }
        }

        private static void CommitRetirement(string path)
        {
            Fixture wide = Write(path, 262144, 1, 262144, n => Tile(37 + (int)n, 91));
            Fixture tiny = Write(path, 1, 1, 1, n => Tile(37, 91));
            using (var counted = new CountedFiles(path)) using (var state = new LibraryState(counted))
            {
                BlueprintProjectionIdentity previous = Open(state, wide, 1), candidate = Open(state, tiny, 2);
                var old = new BlueprintProjectionPlacement(previous, new CaptureSummary { Left = 37, Top = 91, Width = 262144, Height = 1, ContentCount = 262144 }, 0, 0, 1);
                var next = new BlueprintProjectionPlacement(candidate, new CaptureSummary { Left = 37, Top = 91, Width = 1, Height = 1, ContentCount = 1 }, 60 * 2048, 0, 2);
                counted.HoldBlock = 60;
                Require(state.RequestProjectionCommit(new BlueprintProjectionCommitRequest(11, 800, 80, next, new[] { old })), "overlap starts against a qualified immutable scene");
                Require(counted.BlockEntered.Wait(5000), "overlap actually reaches old template reader beyond cached tail");
                state.ReleaseProjection(previous); counted.BlockRelease.Set();
                Until(() => Volatile.Read(ref counted.ActiveIndexes) == 1);
                var fresh = new BlueprintProjectionCommitRequest(11, 801, 81, next, Array.Empty<BlueprintProjectionPlacement>());
                Until(() => state.RequestProjectionCommit(fresh));
                BlueprintProjectionCommitReceipt receipt = null; Until(() => state.TryTakeProjectionCommit(out receipt));
                Require(receipt.Succeeded && receipt.Request.TransactionId == 801 && receipt.Request.SceneRevision == 81 && receipt.Masks.Count == 0, "reader retirement drops old delta, releases its compute slot and preserves the next transaction identity");
                Require(old.Mask.RemovedLayerCount == 0, "retired computation never mutates Host's old immutable mask");
                state.ReleaseProjection(candidate); Until(() => Volatile.Read(ref counted.ActiveIndexes) == 0);
            }
        }

        private static void Saturation(string path)
        {
            Fixture fixture = Write(path, 262144, 1, 262144, n => Tile(37 + (int)n, 91));
            using (var counted = new CountedFiles(path)) using (var state = new LibraryState(counted))
            {
                var identities = new List<BlueprintProjectionIdentity>();
                for (int i = 0; i < 40; i++) identities.Add(Open(state, fixture, 300 + i));
                Require(counted.ActiveIndexes == 1, "forty references share one immutable template reader");
                counted.HoldBlock = 0;
                Require(state.RequestProjectionWindow(identities[0], 1, new BlueprintProjectionBounds(0, 0, 1, 1)), "first viewport request admitted");
                Require(counted.BlockEntered.Wait(5000), "single worker held at uncached read before mailbox saturation");
                var admitted = new List<BlueprintProjectionIdentity> { identities[0] }; var refused = new List<BlueprintProjectionIdentity>();
                for (int i = 1; i < identities.Count; i++)
                {
                    if (state.RequestProjectionWindow(identities[i], 1, new BlueprintProjectionBounds(i * 2048, 0, 1, 1))) admitted.Add(identities[i]);
                    else refused.Add(identities[i]);
                }
                Require(admitted.Count == 32 && refused.Count == 8, "active plus pending viewport requests are bounded; saturation returns backpressure without losing instances");
#if DEBUG
                Require(state.DebugProjectionStatistics.PendingRequests == 32 && state.DebugProjectionStatistics.ActiveReferences == 40, "real owner counters distinguish request budget from reference count");
#endif
                Require(state.TryList(900, 0), "ordinary library request accepted while viewport queue is saturated");
                counted.BlockRelease.Set(); Require(Library(state, true).Status == LibraryStatus.Completed, "accepted List gets priority at the next cooperative viewport boundary");
                var received = new HashSet<long>();
                Until(() =>
                {
                    BlueprintProjectionReceipt receipt;
                    if (state.TryTakeProjectionReceipt(out receipt))
                    {
                        Require(receipt.Kind == BlueprintProjectionReceiptKind.Window && receipt.Window.Cells.Count == 1, "only complete bounded windows reach consumer"); received.Add(receipt.Identity.ProjectionGeneration);
                    }
                    return received.Count == admitted.Count;
                });
                foreach (BlueprintProjectionIdentity identity in refused)
                {
                    int at = identities.IndexOf(identity);
                    Require(state.RequestProjectionWindow(identity, 2, new BlueprintProjectionBounds(at * 2048, 0, 1, 1)), "previously refused reference admitted after progress frees capacity");
                }
                Until(() =>
                {
                    BlueprintProjectionReceipt receipt;
                    if (state.TryTakeProjectionReceipt(out receipt)) { Require(receipt.Kind == BlueprintProjectionReceiptKind.Window, "retried demand remains readable"); received.Add(receipt.Identity.ProjectionGeneration); }
                    return received.Count == identities.Count;
                });
#if DEBUG
                BlueprintProjectionStatistics stats = state.DebugProjectionStatistics;
                Require(stats.CachePeakBytes <= 8L * 1024 * 1024 && stats.ReceiptBytes <= 32L * 1024 * 1024, "all references consume one global decoded and receipt byte budget");
                Console.WriteLine("BlueprintProjection saturation references=40 admitted=32 deferred=8 consumed=" + received.Count + " cachePeakBytes=" + stats.CachePeakBytes + " receiptBytes=" + stats.ReceiptBytes);
#endif
                long reads = counted.Reads; Thread.Sleep(10); Require(counted.Reads == reads, "no-demand steady state does no new I/O after all saturated requests are consumed");
                foreach (BlueprintProjectionIdentity identity in identities) state.ReleaseProjection(identity);
                Until(() => Volatile.Read(ref counted.ActiveIndexes) == 0);
            }
        }

        private static void LastReferenceWake(string path)
        {
            Fixture first=Write(path,1,1,1,n=>Tile(37,91)), second=Write(path,1,1,1,n=>Tile(37,91));
            using(var counted=new CountedFiles(path)) using(var state=new LibraryState(counted))
            {
                var a=Open(state,first,601);var b=Open(state,second,602);
                counted.HoldDispose=true;state.ReleaseProjection(a);
                Require(counted.DisposeEntered.Wait(5000),"first retired reader reaches actual disposal boundary");
                // Retire the second template after the worker has selected its
                // current disposal batch, before it consumes the wake flag.
                state.ReleaseProjection(b);counted.DisposeRelease.Set();
                Until(()=>Volatile.Read(ref counted.ActiveIndexes)==0);
#if DEBUG
                Require(state.DebugProjectionStatistics.ActiveReferences==0,"both last refs retire with no new request or receipt consumer wake");
#endif
                Console.WriteLine("BlueprintProjection lastRefDuringDispose activeReaders=0 references=0 no follow-up work");
            }
        }

        private static void SparseSpan(string path)
        {
            for (int variant = 0; variant < 3; variant++)
            {
                int kind = variant;
                long count = kind == 0 ? 2 : kind == 1 ? 4097 : 4098;
                Fixture fixture = Write(Path.Combine(path, kind.ToString()), kind == 0 ? 1 : 4096, 1000000, count, n =>
                {
                    if (kind == 0) return Tile(37, 91 + (n == 0 ? 0 : 999999));
                    if (kind == 1) return n == 0 ? Tile(37, 91) : Tile(37 + (int)n - 1, 1000090);
                    if (n < 2048) return Tile(37 + (int)n, 91);
                    if (n < 4096) return Tile(37 + (int)n - 2048, 500091);
                    return Tile(n == 4096 ? 37 : 4132, 1000090);
                });
                using (var counted = new CountedFiles(Path.Combine(path, kind.ToString()))) using (var state = new LibraryState(counted))
                {
                    BlueprintProjectionIdentity identity = Open(state, fixture);
#if DEBUG
                    BlueprintProjectionStatistics before = state.DebugProjectionStatistics;
#endif
                    var bounds = new BlueprintProjectionBounds(kind == 0 ? 0 : kind == 1 ? 3000 : 2046, 0, kind == 0 ? 1 : 2, 1000000);
                    BlueprintProjectionWindow window = Window(state, identity, 1, bounds);
                    if (kind == 0) Require(window.Cells.Count == 2 && window.Cells[0].Y == 0 && window.Cells[1].Y == 999999, "two actual records survive a million-row sparse overlap query");
                    else if (kind == 1) Require(window.Cells.Count == 2 && window.Cells[0].X == 3000 && window.Cells[1].X == 3001 && window.Cells[0].Y == 999999 && window.Cells[1].Y == 999999, "jumping a spanning block must still query later blocks on its last row");
                    else Require(window.Cells.Count == 4 && window.Cells[0].Y == 0 && window.Cells[1].Y == 0 && window.Cells[2].Y == 500000 && window.Cells[3].Y == 500000, "empty gaps between disjoint block ranges are skipped without missing selected X endpoints");
#if DEBUG
                    BlueprintProjectionStatistics after = state.DebugProjectionStatistics; long comparisons = after.DirectoryComparisons - before.DirectoryComparisons;
                    Console.WriteLine("BlueprintProjection sparseSpan variant=" + kind + " height=1000000 cells=" + window.Cells.Count + " comparisons=" + comparisons);
                    Require(comparisons <= 32, "complete sparse overlap must locate actual block spans rather than loop a million empty rows; comparisons=" + comparisons);
#endif
                }
            }
        }

        private static void CacheIntersection(string path)
        {
            Fixture fixture = Write(path, 512, 264, 512L * 264, n => Tile(37 + (int)(n % 512), 91 + (int)(n / 512)));
            using (var counted = new CountedFiles(path)) using (var state = new LibraryState(counted))
            {
                BlueprintProjectionIdentity identity = Open(state, fixture);
                Window(state, identity, 1, new BlueprintProjectionBounds(0, 0, 512, 260));
                long before = counted.Reads;
                Window(state, identity, 2, new BlueprintProjectionBounds(0, 4, 512, 260));
                long added = counted.Reads - before;
                Console.WriteLine("BlueprintProjection cache65 capacity=8388608 shiftedBlockReads=" + added);
                Require(added <= 3, "64 overlapping block demands must survive the 65-block/63-resident LRU cycle; actual reads=" + added);
            }
        }

        private sealed class Fixture { internal Guid Id, Revision; internal string Hash; }
        private static void MatchSources(string path)
        {
            const int count = 4097;
            Fixture fixture = Write(path, 4096, 1000000, count, n => n < 4096 ? Tile(37 + (int)n, 91) : Tile(37, 1000090));
            using (var counted = new CountedFiles(path)) using (var state = new LibraryState(counted))
            {
                var identity = new BlueprintProjectionIdentity(fixture.Id, fixture.Revision, fixture.Hash, 55, 77);
                Require(state.RequestProjectionOpen(identity), "source open admitted"); var opened = Await(state, BlueprintProjectionReceiptKind.Opened);
                Require(opened.SourceRanges != null && opened.SourceRanges.Count == 3 && opened.TotalLayerCount == count, "full validated sparse source directory has one range per cell block and exact layer denominator");
                long sourceIndexBytes = opened.SourceRanges.Sum(r => r.SourceIndexBytes);
                Require(sourceIndexBytes <= 512,
                    "dense runs compress the shared position index; charged index is separate from decoded payload cache");
                // Release keeps the public index/lifetime assertions; only diagnostic counter agreement is DEBUG-only.
#if DEBUG
                Require(sourceIndexBytes == state.DebugProjectionStatistics.SourceIndexBytes, "charged source index bytes match the public directory");
#endif
                int actual = 0, worksets = 0; long token = 100; bool viewport = false;
                for (int range = 0; range < opened.SourceRanges.Count; range++)
                {
                    int offset = 0;
                    while (true)
                    {
                        Require(state.RequestProjectionSource(identity, ++token, range, offset: offset), "independent finite source lane admitted");
                        Require(state.RequestProjectionWindow(identity, token, new BlueprintProjectionBounds(0, 999999, 1, 1)), "viewport Latest remains independent of complete traversal");
                        BlueprintProjectionSourceReceipt source = null;
                        Until(() => { BlueprintProjectionReceipt display; if (state.TryTakeProjectionReceipt(out display)) { Require(display.Kind != BlueprintProjectionReceiptKind.Failed, "viewport still reliable"); if (display.Kind == BlueprintProjectionReceiptKind.Window) viewport = true; } return state.TryTakeProjectionSource(out source); });
                        worksets++;
                        Require(source.Generation == token && source.Range == range && source.Offset == offset && source.Complete && source.Window.Cells.Count > 0 && source.Window.Cells.Count <= 2048 && source.NextOffset > offset,
                            "receipt binds range/raw offset/generation and returns one finite physical block within the DTO byte gate");
                        Require(source.Window.EstimatedBytes == 256L + source.Window.Cells.Count * 64L && source.Window.EstimatedBytes <= 32L * 1024 * 1024, "ordinary workset cells and array/header ownership are charged to the existing DTO limit");
                        foreach (var cell in source.Window.Cells)
                        { Require(cell.X == (actual < 4096 ? actual : 0) && cell.Y == (actual < 4096 ? 0 : 999999), "sparse normalized source order never scans or duplicates bbox air"); actual++; }
                        offset = source.NextOffset; if (source.Complete) break;
                    }
                }
                Require(actual == count && worksets == 3 && viewport, "three physical blocks cover actual sparse records once while viewport Latest independently makes progress");
                Require(state.RequestProjectionSource(identity, ++token, 0), "retirement fixture source demand accepted"); state.ReleaseProjection(identity);
                BlueprintProjectionSourceReceipt late; Require(!state.TryTakeProjectionSource(out late) && !state.RequestProjectionSource(identity, ++token, 0), "release clears late source payload and rejects identity resurrection");
                Until(() => Volatile.Read(ref counted.ActiveIndexes) == 0);
                long stopped = counted.Reads; Thread.Sleep(10); Require(counted.Reads == stopped, "last-reference source lane does no residual body IO");
                Console.WriteLine("BlueprintProjection source ranges=3 targetCells=4097 bboxHeight=1000000 worksets=3 physicalRecords<=2048 dtoBytesCharged=true viewportIndependent=true retiredReaders=0");
            }
            string gapPath = Path.Combine(path, "exact-gap"); Fixture gap = Write(gapPath, 1, 1000000, 2, n => Tile(37, n == 0 ? 91 : 1000090));
            using (var counted = new CountedFiles(gapPath)) using (var state = new LibraryState(counted))
            {
                var identities = new List<BlueprintProjectionIdentity>(); BlueprintProjectionSourceRange range = default(BlueprintProjectionSourceRange);
                for (int i = 0; i < 16; i++)
                {
                    var id = new BlueprintProjectionIdentity(gap.Id, gap.Revision, gap.Hash, 90, i + 1); identities.Add(id); Require(state.RequestProjectionOpen(id), "shared sparse index open");
                    var opened = Await(state, BlueprintProjectionReceiptKind.Opened); range = opened.SourceRanges.Single();
                    Require(range.Contains(0, 0) && range.Contains(0, 999999) && !range.Contains(0, 500000) && !range.Intersects(new BlueprintProjectionBounds(0, 1, 1, 999998)), "exact index excludes every absent bbox row without scanning it");
                    Require(range.SourceIndexBytes == 144 && counted.ActiveIndexes == 1, "16 instances share the same two-position 144-byte index and reader");
#if DEBUG
                    Require(state.DebugProjectionStatistics.SourceIndexBytes == 144, "shared source index is charged once across 16 instances");
#endif
                }
                foreach (var identity in identities) state.ReleaseProjection(identity); Until(() => counted.ActiveIndexes == 0);
#if DEBUG
                Until(() => state.DebugProjectionStatistics.SourceIndexBytes == 0);
                Require(state.DebugProjectionStatistics.SourceIndexBytes == 0, "last reference releases the shared immutable source index");
#endif
                Console.WriteLine("BlueprintProjection source exactGapHeight=1000000 targetPositions=2 sharedInstances=16 chargedSourceIndexBytes=144 retiredIndexBytes=0");
            }
            string objectPath = Path.Combine(path, "cross-block-object");
            Fixture split = Write(objectPath, 4096, 2, 4100, n =>
            {
                var cell = Tile(37 + (int)(n < 4096 ? n : 4092 + n - 4096), 91 + (n < 4096 ? 0 : 1));
                if (n >= 4094 && n < 4096 || n>=4098) { cell.ObjectId = 1; cell.TileType = 21; } return cell;
            }, new[] { new CaptureObject { Id = 1, Type = 21, X = 4131, Y = 91, Width = 2, Height = 2, Reliable = true } });
            using (var state = new LibraryState(new FileBlueprintLibrary(objectPath)))
            {
                var id = new BlueprintProjectionIdentity(split.Id, split.Revision, split.Hash, 91, 1); Require(state.RequestProjectionOpen(id), "cross-block group source open"); var opened = Await(state, BlueprintProjectionReceiptKind.Opened);
                Require(opened.Kind == BlueprintProjectionReceiptKind.Opened && opened.SourceRanges.Count == 3 && opened.SourceRanges[1].Contains(4094, 1) && opened.SourceRanges[2].Contains(4094, 0) && !opened.SourceRanges[2].Contains(2000, 0),
                    "each split group range includes all other-block object members while excluding unrelated source positions");
                var maskedObject = SourceMask(new Dictionary<long, ushort>(), new HashSet<int> { 1 }, 4);
                Require(!opened.SourceRanges[1].Intersects(new BlueprintProjectionBounds(4094, 1, 1, 1), 1, maskedObject) && !opened.SourceRanges[2].Intersects(new BlueprintProjectionBounds(4094, 0, 2, 2), 1, maskedObject),
                    "whole-object permanent removal retracts other-block and own-member Tile dependencies by object ID");
                Require(opened.MatchRanges.Count==4 && opened.MatchRanges[1].TargetLayerCount==2046 && opened.MatchRanges[2].TargetLayerCount==4 && opened.MatchRanges[2].ObservationGroup==1,
                    "split object has one complete four-Tile logical proof while both physical-block ordinary proofs keep separate denominators");
                var routes=new HashSet<int>();opened.SourceRanges[1].RouteProofs(new BlueprintProjectionBounds(4094,0,1,1),1,BlueprintProjectionMask.Empty,i=>routes.Add(i));opened.SourceRanges[1].RouteProofs(new BlueprintProjectionBounds(4095,1,1,1),1,BlueprintProjectionMask.Empty,i=>routes.Add(i));
                Require(routes.SetEquals(new[]{1}),"two actual object member changes route only the object, without their union retracting the ordinary Tile proof");
                routes.Clear();opened.SourceRanges[1].RouteProofs(new BlueprintProjectionBounds(2048,0,1,1),1,BlueprintProjectionMask.Empty,i=>routes.Add(i));Require(routes.SetEquals(new[]{0}),"real ordinary Tile change routes only its ordinary proof");
                routes.Clear();opened.SourceRanges[1].RouteProofs(new BlueprintProjectionBounds(4094,1,1,1),1,maskedObject,i=>routes.Add(i));Require(routes.Count==0,"permanently erased object has no routed Tile proof dependency");
                Require(!opened.SourceRanges[2].IntersectsProofs(new BlueprintProjectionBounds(4094,1,1,1),1,BlueprintProjectionMask.Empty) && opened.SourceRanges[1].IntersectsProofs(new BlueprintProjectionBounds(4094,1,1,1),1,BlueprintProjectionMask.Empty) && opened.SourceRanges[2].IntersectsProofs(new BlueprintProjectionBounds(4092,1,1,1),1,BlueprintProjectionMask.Empty),
                    "mixed tail block rejects the earlier-owned object's true Tile write without losing its own ordinary Tile or the owning group's full member dependency");
                Require(state.RequestProjectionSource(id,2001,2),"independent object proof primary slice admitted");BlueprintProjectionSourceReceipt objectSource=null;Until(()=>state.TryTakeProjectionSource(out objectSource));
                Require(opened.MatchRanges[2].ObjectId==1 && objectSource.ObjectId==0 && objectSource.Complete && objectSource.Window.Cells.Count==4 && objectSource.Window.Objects.Count==1 && objectSource.Window.Cells.All(c=>c.ObjectId==1 && c.Layers==CaptureLayer.Tile),
                    "logical object primary workset directly returns all cross-block Tile members and metadata without an anchor round trip or duplicate same-cell layers");
                Require(state.RequestProjectionSource(id,2002,2,1),"one complete object target workset admitted");Until(()=>state.TryTakeProjectionSource(out objectSource));
                Require(objectSource.ObjectId==1 && objectSource.Window.Cells.Count==4 && objectSource.Window.Objects.Count==1,"complete target workset returns all four cross-block members once");
            }
            string layersPath = Path.Combine(path, "exact-layers");
            Fixture layered = Write(layersPath, 4096, 2, 2050, n => n < 2048 ? new CaptureCell { X = 37 + (int)n * 2, Y = 91, Layers = CaptureLayer.Wall, WallType = 7 } : n == 2048 ?
                new CaptureCell { X = 37, Y = 92, Layers = CaptureLayer.Tile | CaptureLayer.Wall, TileType = 1, WallType = 7 } :
                new CaptureCell { X = 4132, Y = 92, Layers = CaptureLayer.Wire | CaptureLayer.Actuator, Wires = 5, Actuator = true });
            using (var state = new LibraryState(new FileBlueprintLibrary(layersPath)))
            {
                var id = new BlueprintProjectionIdentity(layered.Id, layered.Revision, layered.Hash, 92, 1); Require(state.RequestProjectionOpen(id), "layer position index opens"); var opened = Await(state, BlueprintProjectionReceiptKind.Opened); var range = opened.SourceRanges[1];
                Require(range.TargetBits == (1 | 2 | 8 | 16 | 64) && !range.Intersects(new BlueprintProjectionBounds(0, 1, 1, 1), 16, BlueprintProjectionMask.Empty) && !range.Intersects(new BlueprintProjectionBounds(4095, 1, 1, 1), 32, BlueprintProjectionMask.Empty),
                    "another coordinate/color in the same range cannot turn an extra layer into a target dependency");
                var mask = SourceMask(new Dictionary<long, ushort> { [((long)1 << 32)] = 1, [((long)1 << 32) | 4095] = 16 }, new HashSet<int>(), 2);
                Require(!range.Intersects(new BlueprintProjectionBounds(0, 1, 1, 1), 1, mask) && range.Intersects(new BlueprintProjectionBounds(0, 1, 1, 1), 2, mask) && !range.Intersects(new BlueprintProjectionBounds(4095, 1, 1, 1), 16, mask) && range.Intersects(new BlueprintProjectionBounds(4095, 1, 1, 1), 64, mask),
                    "permanent masks remove only the exact coordinate/layer/color from immutable source observation dependencies");
                Console.WriteLine("BlueprintProjection source mixed-layer position masks=true crossBlockObjectMasks=true; no per-instance DTO/world index");
            }
            SourceObjectRoutes(Path.Combine(path,"unique-object-routes"));
        }
        private static void SourceObjectRoutes(string path)
        {
            // Small sparse blocks retain points while the same object's full
            // extent is also indexed. Count callbacks, not just set membership:
            // a repeated ordinal would apply one native old/new delta twice.
            var cells=new List<CaptureCell>();
            for(int row=0;row<3;row++)
            {
                for(int column=0;column<2;column++)cells.Add(new CaptureCell{X=37+column*2,Y=91+row,ObjectId=column+1,Layers=CaptureLayer.Tile|(row==0&&column==0?CaptureLayer.Wall:0),TileType=10,WallType=(ushort)(row==0&&column==0?7:0),FrameY=(short)(row*18)});
                if(row==0)cells.Add(new CaptureCell{X=57,Y=91,Layers=CaptureLayer.Wire,Wires=8});
            }
            var fixture=Write(path,21,3,cells.Count,n=>cells[(int)n],new[]{new CaptureObject{Id=1,Type=10,X=37,Y=91,Width=1,Height=3,Reliable=true},new CaptureObject{Id=2,Type=10,X=39,Y=91,Width=1,Height=3,Reliable=true}});
            using(var state=new LibraryState(new FileBlueprintLibrary(path)))
            {
                var id=new BlueprintProjectionIdentity(fixture.Id,fixture.Revision,fixture.Hash,94,1);Require(state.RequestProjectionOpen(id),"sparse object route source open");var opened=Await(state,BlueprintProjectionReceiptKind.Opened);
                Require(opened.SourceRanges.Count==1 && opened.MatchRanges.Count==3,"one physical watch owns an ordinary proof and two complete doors");
                var range=opened.SourceRanges[0];var routes=new List<int>();
                range.RouteProofs(new BlueprintProjectionBounds(0,0,1,1),1,BlueprintProjectionMask.Empty,routes.Add);
                Console.WriteLine("BlueprintProjection sparse native-store routes="+String.Join(",",routes));
                Require(routes.SequenceEqual(new[]{1}),"one actual native store reaches the same sparse object proof exactly once despite point/extent overlap; actual="+String.Join(",",routes));
                routes.Clear();range.RouteProofs(new BlueprintProjectionBounds(0,0,1,1),1,BlueprintProjectionMask.Empty,routes.Add);Require(routes.SequenceEqual(new[]{1}),"a later store in the same root must independently reach that proof once again");
                routes.Clear();range.RouteProofs(new BlueprintProjectionBounds(1,0,1,1),1,BlueprintProjectionMask.Empty,routes.Add);Require(routes.Count==2 && routes.OrderBy(i=>i).SequenceEqual(new[]{1,2}),"the shared wing coordinate reaches both distinct object proofs once");
                routes.Clear();range.RouteProofs(new BlueprintProjectionBounds(0,0,3,3),3,BlueprintProjectionMask.Empty,routes.Add);Require(routes.Count==3 && routes.OrderBy(i=>i).SequenceEqual(new[]{0,1,2}),"multi-member extent reaches each object and the independent wall proof once");
                var mask=SourceMask(new Dictionary<long,ushort>(),new HashSet<int>{1},3);
                routes.Clear();range.RouteProofs(new BlueprintProjectionBounds(1,0,1,1),1,mask,routes.Add);Require(routes.SequenceEqual(new[]{2}),"permanent object mask removes only its own proof from the shared wing");
            }
            SourceOpenDoorBounds(Path.Combine(path,"open-door"));
        }
        private static void SourceOpenDoorBounds(string path)
        {
            for(int direction=1;direction<=2;direction++)
            {
                var cells=new List<CaptureCell>();
                for(int row=0;row<3;row++)
                {
                    for(int column=0;column<2;column++)cells.Add(new CaptureCell{X=37+column,Y=91+row,ObjectId=1,Layers=CaptureLayer.Tile,TileType=11,FrameX=(short)((direction==1?36:0)+column*18),FrameY=(short)(row*18)});
                    if(row==0)cells.Add(new CaptureCell{X=57,Y=91,Layers=CaptureLayer.Wire,Wires=8});
                }
                string folder=Path.Combine(path,direction.ToString());var fixture=Write(folder,21,3,7,n=>cells[(int)n],new[]{new CaptureObject{Id=1,Type=11,X=37,Y=91,Width=2,Height=3,Direction=(byte)direction,OriginX=direction==1?1:0,Reliable=true}});
                using(var state=new LibraryState(new FileBlueprintLibrary(folder)))
                {
                    var id=new BlueprintProjectionIdentity(fixture.Id,fixture.Revision,fixture.Hash,95,direction);Require(state.RequestProjectionOpen(id),"original open-door source direction opens");var opened=Await(state,BlueprintProjectionReceiptKind.Opened);var physical=opened.SourceRanges.Single();var proof=opened.MatchRanges.Single(r=>r.TargetLayerCount==6);
                    int left=direction==1?0:-1,outside=direction==1?-1:2;var change=new BlueprintProjectionBounds(outside,0,1,1);var routes=new List<int>();physical.RouteProofs(change,1,BlueprintProjectionMask.Empty,routes.Add);
                    Console.WriteLine("BlueprintProjection open-door direction="+direction+" sourceProof="+proof.Bounds.Left+","+proof.Bounds.Width+" irrelevantOuterRoutes="+String.Join(",",routes));
                    Require(physical.Bounds.Contains(outside,0) && !physical.IntersectsProofs(change,1,BlueprintProjectionMask.Empty) && routes.Count==0,"outer fourth column is only a coarse bbox hole, never a same-hinge member/seed dependency; direction="+direction);
                    Require(proof.Bounds.Left==left && proof.Bounds.Width==3 && proof.Bounds.Height==3,"reliable original open direction defines the exact hinge-minus-one three-column union");
                    routes.Clear();physical.RouteProofs(new BlueprintProjectionBounds(direction==1?2:-1,0,1,1),1,BlueprintProjectionMask.Empty,routes.Add);Require(routes.SequenceEqual(new[]{1}),"the opposite legal wing outside the source footprint remains a required object dependency");
                }
            }
        }
        private static BlueprintProjectionMask SourceMask(Dictionary<long, ushort> cells, HashSet<int> objects, long count)
        { return (BlueprintProjectionMask)typeof(BlueprintProjectionMask).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single().Invoke(new object[] { cells, objects, count }); }
        private static BlueprintProjectionSourceReceipt Source(LibraryState state)
        {
            BlueprintProjectionSourceReceipt found = null;
            Until(() =>
            {
                BlueprintProjectionReceipt other;
                if (state.TryTakeProjectionReceipt(out other)) Require(other.Kind != BlueprintProjectionReceiptKind.Failed, "source batch must not retire a valid asset: " + other.FailureCode);
                return state.TryTakeProjectionSource(out found);
            });
            return found;
        }
        private static void SourceBatches(string path)
        {
            string mixedPath = Path.Combine(path, "mixed-small-objects"); var cells = new List<CaptureCell>(); var objects = new List<CaptureObject>();
            for (int row = 0; row < 3; row++)
            {
                for (int item = 0; item < 64; item++) cells.Add(new CaptureCell { X = 37 + item * 2, Y = 91 + row, ObjectId = item + 1, Layers = CaptureLayer.Tile |
                    (row == 0 && item == 0 ? CaptureLayer.Wall : 0) | (row == 0 && item == 1 ? CaptureLayer.Wire : 0), TileType = 10,
                    WallType = (ushort)(row == 0 && item == 0 ? 7 : 0), Wires = (byte)(row == 0 && item == 1 ? 1 : 0), FrameY = (short)(row * 18) });
                if (row == 0) cells.Add(Tile(165, 91));
            }
            for (int item = 0; item < 64; item++) objects.Add(new CaptureObject { Id = item + 1, Type = 10, X = 37 + item * 2, Y = 91, Width = 1, Height = 3, Reliable = true, Reason = item == 0 ? "retained metadata" : "" });
            var missing = Enumerable.Range(0, 16).Select(i => "PayloadCategory" + i).ToArray();
            var fixture = Write(mixedPath, 129, 3, cells.Count, n => cells[(int)n], objects.ToArray(), missing);
            using (var counted = new CountedFiles(mixedPath)) using (var state = new LibraryState(counted))
            {
                var id = new BlueprintProjectionIdentity(fixture.Id, fixture.Revision, fixture.Hash, 96, 1);
                Require(state.RequestProjectionOpen(id), "mixed batch source open"); var opened = Await(state, BlueprintProjectionReceiptKind.Opened);
                Require(opened.MatchRanges.Count == 65 && opened.TotalLayerCount == 195, "64 independent three-member objects keep a separate ordinary Tile/wall/wire proof");
                var demands = Enumerable.Range(0, 64).Select(r => new BlueprintProjectionSourceDemand(r)).ToArray();
#if DEBUG
                var before = state.DebugProjectionSourceStatistics;
#endif
                Require(state.RequestProjectionSources(id, 3001, demands), "finite source batch accepted");
                demands[0] = new BlueprintProjectionSourceDemand(Int32.MaxValue);
                var batch = Source(state);
                Require(batch.Generation == 3001 && batch.Parts.Count == 64 && batch.Range == 0 && batch.Window == batch.Parts[0].Window && batch.ObjectId == 0,
                    "request owns its copied finite demand list and old accessors describe its first part");
                var ordinary = batch.Parts[0];
                Require(ordinary.LogicalObjectId == 0 && ordinary.NextOffset == cells.Count && ordinary.Window.Cells.Count == 3 && ordinary.Window.Objects.Count == 0 &&
                    ordinary.Window.Cells.Count(c => c.Layers == CaptureLayer.Wall) == 1 && ordinary.Window.Cells.Count(c => c.Layers == CaptureLayer.Wire) == 1 && ordinary.Window.Cells.Count(c => c.Layers == CaptureLayer.Tile) == 1,
                    "ordinary lane preserves same-cell wall/wire once and raw next offset counts all physical records");
                Require(ordinary.Window.Cells.Single(c => c.Layers == CaptureLayer.Wall).ObjectId == 1 &&
                    ordinary.Window.Cells.Single(c => c.Layers == CaptureLayer.Wire).ObjectId == 2,
                    "ordinary non-Tile targets retain captured furniture identity as context, without acquiring its Tile ownership");
                for (int i = 1; i < batch.Parts.Count; i++)
                {
                    var part = batch.Parts[i];
                    int expectedObject = opened.MatchRanges[i].ObjectId;
                    Require(part.Range == i && part.ObjectId == 0 && part.LogicalObjectId == expectedObject && part.Offset == 0 && part.NextOffset == 0 && part.Complete &&
                        part.Window.Cells.Count == 3 && part.Window.Objects.Count == 1 && part.Window.Objects[0].Id == expectedObject &&
                        part.Window.Cells.All(c => c.ObjectId == expectedObject && (c.Layers & CaptureLayer.Tile) != 0 && (c.Layers & ~(CaptureLayer.Tile | CaptureLayer.Wall)) == 0) &&
                        part.Window.Cells.Count(c => (c.Layers & CaptureLayer.Wall) != 0) == (expectedObject == 1 ? 1 : 0),
                        "each logical object keeps one complete Tile owner and only its original same-cell wall context; wire stays ordinary");
                }
                long uniqueWindows = batch.Parts.Select(p => p.Window).Distinct().Sum(w => w.EstimatedBytes);
                Require(batch.Parts.All(p => p.WindowEstimatedBytes == p.Window.EstimatedBytes + missing.Length * 8L && p.Window.Summary.MissingPayloads.SequenceEqual(missing) && p.Window.Summary.UnreliableReasons.Length == 0) &&
                    !ReferenceEquals(batch.Parts[0].Window.Summary.MissingPayloads, batch.Parts[1].Window.Summary.MissingPayloads) &&
                    ReferenceEquals(batch.Parts[0].Window.Summary.MissingPayloads[0], batch.Parts[1].Window.Summary.MissingPayloads[0]),
                    "actual ReadRoot summary arrays are cloned per window and charged per slot while their immutable strings remain shared");
                Require(batch.EstimatedBytes == uniqueWindows + 128L + batch.Parts.Count * (128L + missing.Length * 8L) && batch.EstimatedBytes <= BlueprintProjectionSourceReceipt.TargetBytes,
                    "retained batch estimate includes unique immutable windows plus real directory/descriptor ownership");
#if DEBUG
                var after = state.DebugProjectionSourceStatistics;
                Require(after.MemberBlocks - before.MemberBlocks == 1 && after.MemberRecords - before.MemberRecords == cells.Count,
                    "64 logical parts scan the shared physical member block once, not once per object");
                Require(after.Receipts - before.Receipts == 1 && after.Parts - before.Parts == 64 && after.ReceiptBytes == 0 && after.WorkingPeakBytes >= batch.EstimatedBytes,
                    "actual transport/part counts and staging/receipt ownership are observable and released on Take");
                Console.WriteLine("BlueprintProjection batch smallObjects=63 ordinaryRecords=3 physicalRecords=" + cells.Count + " memberScans=" + (after.MemberBlocks - before.MemberBlocks) +
                    " scannedRecords=" + (after.MemberRecords - before.MemberRecords) + " parts=" + batch.Parts.Count + " retainedBytes=" + batch.EstimatedBytes + " workingPeakBytes=" + after.WorkingPeakBytes);
#endif
                Require(state.RequestProjectionSources(id, 3003, new[] { new BlueprintProjectionSourceDemand(1), new BlueprintProjectionSourceDemand(1) }), "repeated immutable demand shares one build");
                var duplicate = Source(state);
                Require(duplicate.Parts.Count == 2 && ReferenceEquals(duplicate.Parts[0].Window, duplicate.Parts[1].Window) &&
                    duplicate.EstimatedBytes == duplicate.Window.EstimatedBytes + missing.Length * 8L + 384L,
                    "two part references retain one window payload, while both descriptors remain charged");
                int objectOne = Enumerable.Range(0, opened.MatchRanges.Count).Single(r => opened.MatchRanges[r].ObjectId == 1);
                Require(state.RequestProjectionSource(id, 3004, objectOne, 1), "legacy explicit positive object request remains accepted");
                var explicitObject = Source(state);
                Require(explicitObject.ObjectId == 1 && explicitObject.Parts[0].LogicalObjectId == 1 && explicitObject.Window.Cells.Count == 3 &&
                    explicitObject.Window.Cells.Any(c => c.Layers == (CaptureLayer.Tile | CaptureLayer.Wall)), "explicit all-layer selection keeps its original request identity and captured layers");
                int objectTwo = Enumerable.Range(0, opened.MatchRanges.Count).Single(r => opened.MatchRanges[r].ObjectId == 2);
                Require(state.RequestProjectionSource(id, 3007, objectTwo, 2), "legacy explicit same-cell wire request remains accepted");
                var explicitWire = Source(state);
                Require(explicitWire.ObjectId == 2 && explicitWire.Window.Cells.Any(c => c.Layers == (CaptureLayer.Tile | CaptureLayer.Wire)),
                    "explicit positive object API keeps wire, whereas the logical Tile-owner transport carries only its required wall context");
                Require(!state.RequestProjectionSources(id, 3005, new BlueprintProjectionSourceDemand[65]) && !state.RequestProjectionSources(id, 3005, Array.Empty<BlueprintProjectionSourceDemand>()),
                    "request envelope backpressure rejects empty/over-64 lists without retiring valid source identity");
                Require(state.RequestProjectionSource(id, 3006, 64), "a still-live final logical object remains independently serviceable");
                Require(Source(state).Window.Objects[0].Id == opened.MatchRanges[64].ObjectId, "64-part envelope never labels its unrequested suffix complete");
                state.ReleaseProjection(id); Until(() => counted.ActiveIndexes == 0);
            }
            SourceBatchPrefix(Path.Combine(path, "prefix"));
            SourceBatchLifetime(Path.Combine(path, "lifetime"));
        }
        private static void SourceBatchPrefix(string path)
        {
            string densePath = Path.Combine(path, "ordinary"); var dense = Write(densePath, 6144, 1, 6144, n => Tile(37 + (int)n, 91));
            using (var state = new LibraryState(new FileBlueprintLibrary(densePath)))
            {
                var id = Open(state, dense);
                Require(state.RequestProjectionSources(id, 3101, new[] { new BlueprintProjectionSourceDemand(0), new BlueprintProjectionSourceDemand(1), new BlueprintProjectionSourceDemand(2) }), "three valid ordinary demands accepted");
                var first = Source(state);
                Require(first.Parts.Count == 1 && first.Range == 0 && first.Window.Cells.Count == 2048 && first.Complete && first.EstimatedBytes <= BlueprintProjectionSourceReceipt.TargetBytes,
                    "two full physical windows plus their directories exceed the soft batch target, yielding a legal one-item prefix");
                Require(state.RequestProjectionSources(id, 3102, new[] { new BlueprintProjectionSourceDemand(1), new BlueprintProjectionSourceDemand(2) }), "unaccepted suffix can be requested normally");
                Require(Source(state).Range == 1, "batch-full does not retire identity or skip the first unaccepted range");
                Require(state.RequestProjectionSource(id, 3103, 2, offset: 10), "old raw offset API remains independent of batch filtering");
                var tail = Source(state); Require(tail.Offset == 10 && tail.NextOffset == 2048 && tail.Window.Cells.Count == 2038 && tail.Window.Cells[0].X == 4106, "raw physical offset survives transport batching");
                Require(Library(state, state.TryList(3104, 0)).Status == LibraryStatus.Completed, "ordinary library lane remains live after repeated batch capacity refusals");
            }
            string largePath = Path.Combine(path, "reliable-cross-block-object"); const int memberCount = 2047;
            var large = Write(largePath, 2050, 1, 2050, n => n >= 2 && n < 2049 ?
                new CaptureCell { X = 37 + (int)n, Y = 91, ObjectId = 1, Layers = CaptureLayer.Tile, TileType = 21 } : Tile(37 + (int)n, 91),
                new[] { new CaptureObject { Id = 1, Type = 21, X = 39, Y = 91, Width = memberCount, Height = 1, Reliable = true } });
            using (var state = new LibraryState(new FileBlueprintLibrary(largePath)))
            {
                var id = new BlueprintProjectionIdentity(large.Id, large.Revision, large.Hash, 97, 1); Require(state.RequestProjectionOpen(id), "legal reliable cross-block source opens");
                var opened = Await(state, BlueprintProjectionReceiptKind.Opened);
                Require(opened.Kind == BlueprintProjectionReceiptKind.Opened && opened.MatchRanges.Count == 3 && opened.MatchRanges[1].ObjectId == 1,
                    "one reliable cross-block object has its own proof between independent head and tail ordinary ranges");
#if DEBUG
                var before = state.DebugProjectionSourceStatistics;
#endif
                Require(state.RequestProjectionSources(id, 3201, new[] { new BlueprintProjectionSourceDemand(1), new BlueprintProjectionSourceDemand(2) }), "reliable cross-block object and ordinary tail admitted together");
                var batch = Source(state);
                Require(batch.Parts.Count == 2 && batch.Window.Cells.Count == memberCount && batch.Window.Objects.Count == 1 && batch.Complete &&
                    batch.Window.Cells.All(c => c.ObjectId == 1 && c.Layers == CaptureLayer.Tile) && batch.Parts[1].Range == 2 && batch.Parts[1].Window.Cells.Count == 1 && batch.Parts[1].NextOffset == 2,
                    "reliable whole-object members and its same-physical-block ordinary sibling keep separate target ownership in one transport");
#if DEBUG
                var after = state.DebugProjectionSourceStatistics;
                Require(after.MemberBlocks - before.MemberBlocks == 2 && after.MemberRecords - before.MemberRecords == 2050,
                    "cross-block object plus tail ordinary demand scan their two shared physical blocks only once");
#endif
                Require(state.RequestProjectionSource(id, 3202, 1, 1), "legacy explicit positive ID assembles the same legal complete cross-block source");
                Require(Source(state).Window.Cells.Count == memberCount, "explicit positive request preserves all 2047 reliable members without extending the original proof-size limit");
            }
            // Saved assets must contain zero unresolved objects. A format-
            // readable root with an accurate nonzero uncertain count is rejected
            // before source opens; it cannot prove a large legal source path.
            string uncertainPath = Path.Combine(path, "uncertain-root-rejected");
            var uncertain = Write(uncertainPath, 1, 1, 1,
                n => new CaptureCell { X = 37, Y = 91, ObjectId = 1, Layers = CaptureLayer.Tile, TileType = 21, UnreliableObject = true },
                new[] { new CaptureObject { Id = 1, Type = 21, X = 37, Y = 91, Width = 1, Height = 1, Reliable = false, Reason = "uncertain saved source fixture" } });
            using (var state = new LibraryState(new FileBlueprintLibrary(uncertainPath)))
            {
                var id = new BlueprintProjectionIdentity(uncertain.Id, uncertain.Revision, uncertain.Hash, 97, 2); Require(state.RequestProjectionOpen(id), "uncertain saved identity is admitted only for validation");
                var failed = Await(state, BlueprintProjectionReceiptKind.Failed);
                Require(failed.Kind == BlueprintProjectionReceiptKind.Failed && failed.IdentityRetired && failed.FailureCode == "blueprint-metadata-content-mismatch" &&
                    !state.RequestProjectionSource(id, 3203, 0, 1), "unresolved saved root remains rejected before any logical or explicit source request");
            }
        }
        private static void SourceBatchLifetime(string path)
        {
            var fixture = Write(path, 140000, 1, 140000, n => Tile(37 + (int)n, 91));
            using (var counted = new CountedFiles(path)) using (var state = new LibraryState(counted))
            {
                var a = Open(state, fixture, 1, 98); var b = Open(state, fixture, 2, 98);
                counted.HoldBlock = 0;
                Require(state.RequestProjectionSources(a, 3301, new[] { new BlueprintProjectionSourceDemand(0), new BlueprintProjectionSourceDemand(1) }), "old batch starts at uncached block");
                Require(counted.BlockEntered.Wait(5000), "old transport held in real reader before cancellation");
                Require(!state.RequestProjectionSources(a, 3300, new[] { new BlueprintProjectionSourceDemand(2) }), "one identity keeps one unfinished transport while the real reader is held");
                state.ClearProjectionSource(a);
                Require(state.RequestProjectionSources(a, 3302, new[] { new BlueprintProjectionSourceDemand(2), new BlueprintProjectionSourceDemand(3) }), "new serial can replace canceled transport while old read returns");
                Require(state.TryList(3303, 0), "ordinary library command accepted while source is in flight");
                counted.BlockRelease.Set(); Require(Library(state, true).Status == LibraryStatus.Completed, "single worker services accepted List at its next cooperative source boundary");
                var current = Source(state);
                Require(current.Identity.Equals(a) && current.Generation == 3302 && current.Range == 2, "late old batch cannot publish over or remove the replacement serial");
                Require(state.RequestProjectionSources(a, 3304, new[] { new BlueprintProjectionSourceDemand(3), new BlueprintProjectionSourceDemand(4) }), "retirement can hold a queued batch");
#if DEBUG
                Until(() => state.DebugProjectionSourceStatistics.ReceiptBytes > 0);
#endif
                state.ReleaseProjection(a);
                Require(!state.RequestProjectionSource(a, 3305, 0), "released identity cannot resurrect through source request");
                BlueprintProjectionSourceReceipt stale; Require(!state.TryTakeProjectionSource(out stale), "retirement removes queued source parts before consumer sees them");
                Require(state.RequestProjectionSources(b, 3306, new[] { new BlueprintProjectionSourceDemand(4), new BlueprintProjectionSourceDemand(5) }), "same-template sibling retains its own live source reference");
                Require(Source(state).Identity.Equals(b) && counted.ActiveIndexes == 1, "retiring one identity does not dispose its shared template owner");
                Require(state.RequestProjectionSource(b, 3307, 0), "last reference can retire with pending source"); state.ReleaseProjection(b);
                Until(() => counted.ActiveIndexes == 0);
#if DEBUG
                Until(() => state.DebugProjectionSourceStatistics.PendingRequests == 0 && state.DebugProjectionSourceStatistics.WorkingBytes == 0);
                Require(state.DebugProjectionSourceStatistics.ReceiptBytes == 0 && state.DebugProjectionStatistics.ReceiptBytes == 0 && state.DebugProjectionStatistics.SourceIndexBytes == 0,
                    "last reference returns source queue/staging/shared index ownership without leaking charged payload");
                Console.WriteLine("BlueprintProjection batch cancellationSerial=true queuedRetirement=true sharedSibling=true lastReferenceBytes=0 workingPeakBytes=" + state.DebugProjectionSourceStatistics.WorkingPeakBytes);
#endif
                Require(!state.TryTakeProjectionSource(out stale), "late batch after last retirement cannot revive source identity");
            }
        }
        // Literal v1 bytes are built independently of LibraryCodec. Expectations
        // above use supplied source coordinates/layers, never renderer output.
        private static Fixture Write(string path, int width, int height, long count, Func<long, CaptureCell> cell, CaptureObject[] objects = null, string[] missing = null)
        {
            var fixture = new Fixture { Id = Guid.NewGuid(), Revision = Guid.NewGuid() }; objects = objects ?? Array.Empty<CaptureObject>(); missing = missing ?? Array.Empty<string>();
            string asset = Path.Combine(path, fixture.Id.ToString("N")), content = Path.Combine(asset, "content"), chunks = Path.Combine(content, "chunks"); Directory.CreateDirectory(chunks);
            long number = 0; byte[] index;
            using (var hashes = new MemoryStream())
            {
                for (long start = 0; start < count; start += 2048)
                {
                    int length = (int)Math.Min(2048, count - start); byte[] bytes;
                    using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(0x43424D4A); writer.Write(1); writer.Write(length); writer.Write(0);
                        for (int i = 0; i < length; i++) WriteCell(writer, cell(start + i)); writer.Flush(); bytes = stream.ToArray();
                    }
                    File.WriteAllBytes(Path.Combine(chunks, (number++).ToString("D20") + ".bin"), bytes); byte[] hash = Digest(bytes); hashes.Write(hash, 0, hash.Length);
                }
                if (objects.Length != 0)
                {
                    byte[] bytes; using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(0x43424D4A); writer.Write(1); writer.Write(0); writer.Write(objects.Length);
                        foreach (CaptureObject obj in objects)
                        {
                            writer.Write(obj.Id); writer.Write(obj.Type); writer.Write(obj.X); writer.Write(obj.Y); writer.Write(obj.Width); writer.Write(obj.Height); writer.Write(obj.Style); writer.Write(obj.Alternate); writer.Write(obj.PlacementStyle); writer.Write(obj.RandomVariant); writer.Write(obj.Direction); writer.Write(obj.OriginX); writer.Write(obj.OriginY); writer.Write(obj.Reliable); Text(writer, obj.Reason ?? ""); Text(writer, obj.MissingPayload ?? "");
                        }
                        writer.Flush(); bytes = stream.ToArray();
                    }
                    File.WriteAllBytes(Path.Combine(chunks, (number++).ToString("D20") + ".bin"), bytes); byte[] hash = Digest(bytes); hashes.Write(hash, 0, hash.Length);
                }
                index = hashes.ToArray();
            }
            File.WriteAllBytes(Path.Combine(content, "index.bin"), index); byte[] root;
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(0x42524D4A); writer.Write(1); writer.Write(fixture.Revision.ToByteArray()); writer.Write(number); writer.Write(Digest(index)); writer.Write(37); writer.Write(91); writer.Write(width); writer.Write(height); writer.Write(count); writer.Write(objects.LongCount(o => !o.Reliable)); writer.Write(1UL); writer.Write(2UL); Text(writer, "1.4.5.8"); writer.Write(missing.Length); foreach (string category in missing) Text(writer, category); writer.Flush(); root = stream.ToArray();
            }
            File.WriteAllBytes(Path.Combine(content, "root.bin"), root); fixture.Hash = Convert.ToBase64String(Digest(root));
            File.WriteAllText(Path.Combine(asset, "metadata.json"), "{\"format\":\"JueMingR.BlueprintAsset\",\"version\":1,\"assetId\":\"" + fixture.Id.ToString("N") + "\",\"name\":\"投影测试\",\"contentRevision\":\"" + fixture.Revision.ToString("N") + "\",\"contentHash\":\"" + fixture.Hash + "\",\"width\":" + width + ",\"height\":" + height + ",\"contentCount\":" + count + ",\"blockCount\":" + number + ",\"missingSummary\":\"" + String.Join("；", missing) + "\",\"createdTicks\":1,\"updatedTicks\":2,\"deleted\":false}\n", new UTF8Encoding(false));
            return fixture;
        }
        private static void WriteCell(BinaryWriter writer, CaptureCell c)
        {
            writer.Write(c.X); writer.Write(c.Y); writer.Write(c.ObjectId); writer.Write((byte)c.Layers); writer.Write(c.TileType); writer.Write(c.WallType); writer.Write(c.FrameX); writer.Write(c.FrameY); writer.Write(c.TilePaint); writer.Write(c.WallPaint); writer.Write(c.Slope); writer.Write(c.Wires); writer.Write(c.LiquidAmount); writer.Write(c.LiquidKind);
            writer.Write((byte)((c.HalfBrick ? 1 : 0) | (c.Inactive ? 2 : 0) | (c.Actuator ? 4 : 0) | (c.InvisibleTile ? 8 : 0) | (c.FullbrightTile ? 16 : 0) | (c.InvisibleWall ? 32 : 0) | (c.FullbrightWall ? 64 : 0) | (c.UnreliableObject ? 128 : 0)));
        }
        private static void Text(BinaryWriter writer, string text) { byte[] bytes = Encoding.UTF8.GetBytes(text); writer.Write(bytes.Length); writer.Write(bytes); }
        private static byte[] Digest(byte[] bytes) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }

        private sealed class CountedFiles : IBlueprintLibraryFiles, IBlueprintLibraryLayoutSteps
        {
            private readonly FileBlueprintLibrary inner;
            private bool disposed;
            internal long Reads, IndexReadBytes; internal int ActiveIndexes, MetadataWhileLayout, IndexesAtMetadataWrite, WritesWhileLayout;
            internal string LastWrittenIdentity;
            internal string FailCode;
            internal bool HoldDispose;
            internal readonly ManualResetEventSlim DisposeEntered=new ManualResetEventSlim(), DisposeRelease=new ManualResetEventSlim();
            internal int DeleteMode, HoldLayoutBatches; internal long HoldBlock = -1, FailBlock = -1; internal bool LayoutStarted, LayoutCompleted;
            internal readonly ManualResetEventSlim LayoutEntered = new ManualResetEventSlim(), LayoutRelease = new ManualResetEventSlim(), BlockEntered = new ManualResetEventSlim(), BlockRelease = new ManualResetEventSlim();
            internal CountedFiles(string path) { inner = new FileBlueprintLibrary(path); }
            public void Open() { inner.Open(); }
            public IEnumerable<Guid> EnumerateAssets() { return inner.EnumerateAssets(); }
            public PreferenceReadResult OpenMetadata(Guid id, bool ownedUnpublishedContent = false) { if (LayoutStarted && !LayoutCompleted) MetadataWhileLayout++; return inner.OpenMetadata(id, ownedUnpublishedContent); }
            public PreferenceWriteResult WriteMetadata(string expectedIdentity, byte[] contents)
            {
                IndexesAtMetadataWrite = ActiveIndexes;
                if (LayoutStarted && !LayoutCompleted) WritesWhileLayout++;
                if (DeleteMode == 1) return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "injected-before-commit", false, false);
                PreferenceWriteResult value = inner.WriteMetadata(expectedIdentity, contents);
                LastWrittenIdentity = value.Identity;
                return DeleteMode == 2 && value.Status == PreferenceWriteStatus.Saved ? new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "lost-readback-after-real-commit", true, true) : value;
            }
            public void CloseMetadata() { inner.CloseMetadata(); }
            public void BeginStage(Guid id) { inner.BeginStage(id); }
            public void CloseStageWriter() { inner.CloseStageWriter(); }
            public void WriteStageBlock(Guid id, long number, byte[] contents, byte[] hash) { inner.WriteStageBlock(id, number, contents, hash); }
            public byte[] ReadBlock(Guid id, bool stage, long number)
            {
                Interlocked.Increment(ref Reads);
                if (number == HoldBlock) { HoldBlock = -1; BlockEntered.Set(); if (!BlockRelease.Wait(5000)) throw new TimeoutException("Held read test boundary timed out."); }
                if (number == FailBlock) { if (FailCode != null) throw new InvalidDataException(FailCode); throw new IOException("injected-projection-block-failure"); } return inner.ReadBlock(id, stage, number);
            }
            public Stream OpenIndex(Guid id, bool stage) { return new CountedIndex(inner.OpenIndex(id, stage), this); }
            public void SealStage(Guid id, byte[] root) { inner.SealStage(id, root); }
            public byte[] ReadRoot(Guid id, bool stage) { return inner.ReadRoot(id, stage); }
            public void ValidateLayout(Guid id, bool stage, long blockCount) { inner.ValidateLayout(id, stage, blockCount); }
            public IEnumerable<int> ValidateLayoutSteps(Guid id, bool stage, long blockCount, int budget)
            {
                LayoutStarted = true; LayoutCompleted = false;
                foreach (int step in inner.ValidateLayoutSteps(id, stage, blockCount, budget))
                { if (HoldLayoutBatches > 0) { HoldLayoutBatches--; LayoutEntered.Set(); if (!LayoutRelease.Wait(5000)) throw new TimeoutException("Held layout test boundary timed out."); LayoutRelease.Reset(); } yield return step; }
                LayoutCompleted = true;
            }
            public void PromoteStage(Guid id, Guid assetId) { inner.PromoteStage(id, assetId); }
            public void PrepareCleanup(Guid id, BlueprintCleanupProof proof) { inner.PrepareCleanup(id, proof); }
            public bool CleanupStep(Guid id, bool stage, long blockCount, int budget, BlueprintCleanupProof proof = null) { Require(ActiveIndexes == 0, "cleanup cannot run while projection owns index"); return inner.CleanupStep(id, stage, blockCount, budget, proof); }
            public void CloseCleanup() { inner.CloseCleanup(); }
            public void Dispose() { if (disposed) return; disposed = true; LayoutRelease.Set(); BlockRelease.Set(); DisposeRelease.Set(); inner.Dispose(); LayoutEntered.Dispose(); LayoutRelease.Dispose(); BlockEntered.Dispose(); BlockRelease.Dispose(); DisposeEntered.Dispose(); DisposeRelease.Dispose(); }
            private sealed class CountedIndex : Stream
            {
                private readonly Stream inner; private readonly CountedFiles owner;
                internal CountedIndex(Stream inner, CountedFiles owner) { this.inner = inner; this.owner = owner; Interlocked.Increment(ref owner.ActiveIndexes); }
                public override bool CanRead { get { return inner.CanRead; } }
                public override bool CanSeek { get { return inner.CanSeek; } }
                public override bool CanWrite { get { return false; } }
                public override long Length { get { return inner.Length; } }
                public override long Position { get { return inner.Position; } set { inner.Position = value; } }
                public override int Read(byte[] bytes, int offset, int count) { int read = inner.Read(bytes, offset, count); Interlocked.Add(ref owner.IndexReadBytes, read); return read; }
                public override long Seek(long offset, SeekOrigin origin) { return inner.Seek(offset, origin); }
                public override void Flush() { inner.Flush(); }
                public override void SetLength(long value) { throw new NotSupportedException(); }
                public override void Write(byte[] bytes, int offset, int count) { throw new NotSupportedException(); }
                protected override void Dispose(bool disposing) { if (disposing) { if(owner.HoldDispose){owner.HoldDispose=false;owner.DisposeEntered.Set();if(!owner.DisposeRelease.Wait(5000))throw new TimeoutException("Held reader disposal boundary timed out.");} inner.Dispose(); Interlocked.Decrement(ref owner.ActiveIndexes); } base.Dispose(disposing); }
            }
        }
    }
}
