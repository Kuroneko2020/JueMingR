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
            Run(failures, "sparse overlap spans skip empty rows", () => SparseSpan(Path.Combine(area, "sparse-span")));
            Run(failures, "65 blocks retain intersection", () => CacheIntersection(Path.Combine(area, "cache-intersection")));
            Run(failures, "delete handle and commit boundaries", () => Deletion(Path.Combine(area, "deletion")));
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
                Require(rename.Status == LibraryStatus.Saved && counted.Reads == blocks && counted.IndexReadBytes >= indexes, "rename does not reparse content or retire window");
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
                    Require(after.DirectoryBlocks == width / 2048 && after.DirectoryBytes <= 128L + after.DirectoryBlocks * 48L, "directory stores O(blocks) ranges rather than per-cell entries");
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
                Require(failed.Kind == BlueprintProjectionReceiptKind.Failed && failed.Window == null, "read failure is explicit and never a completed empty/air window");
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
                    if (mode == 0) Require(deleted.Status == LibraryStatus.Deleted, "successful delete retires content and handle");
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
        // Literal v1 bytes are built independently of LibraryCodec. Expectations
        // above use supplied source coordinates/layers, never renderer output.
        private static Fixture Write(string path, int width, int height, long count, Func<long, CaptureCell> cell, CaptureObject[] objects = null)
        {
            var fixture = new Fixture { Id = Guid.NewGuid(), Revision = Guid.NewGuid() }; objects = objects ?? Array.Empty<CaptureObject>();
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
                writer.Write(0x42524D4A); writer.Write(1); writer.Write(fixture.Revision.ToByteArray()); writer.Write(number); writer.Write(Digest(index)); writer.Write(37); writer.Write(91); writer.Write(width); writer.Write(height); writer.Write(count); writer.Write(0L); writer.Write(1UL); writer.Write(2UL); Text(writer, "1.4.5.8"); writer.Write(0); writer.Flush(); root = stream.ToArray();
            }
            File.WriteAllBytes(Path.Combine(content, "root.bin"), root); fixture.Hash = Convert.ToBase64String(Digest(root));
            File.WriteAllText(Path.Combine(asset, "metadata.json"), "{\"format\":\"JueMingR.BlueprintAsset\",\"version\":1,\"assetId\":\"" + fixture.Id.ToString("N") + "\",\"name\":\"投影测试\",\"contentRevision\":\"" + fixture.Revision.ToString("N") + "\",\"contentHash\":\"" + fixture.Hash + "\",\"width\":" + width + ",\"height\":" + height + ",\"contentCount\":" + count + ",\"blockCount\":" + number + ",\"missingSummary\":\"\",\"createdTicks\":1,\"updatedTicks\":2,\"deleted\":false}\n", new UTF8Encoding(false));
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
                if (number == FailBlock) throw new IOException("injected-projection-block-failure"); return inner.ReadBlock(id, stage, number);
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
            public void Dispose() { if (disposed) return; disposed = true; LayoutRelease.Set(); BlockRelease.Set(); inner.Dispose(); LayoutEntered.Dispose(); LayoutRelease.Dispose(); BlockEntered.Dispose(); BlockRelease.Dispose(); }
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
                protected override void Dispose(bool disposing) { if (disposing) { inner.Dispose(); Interlocked.Decrement(ref owner.ActiveIndexes); } base.Dispose(disposing); }
            }
        }
    }
}
