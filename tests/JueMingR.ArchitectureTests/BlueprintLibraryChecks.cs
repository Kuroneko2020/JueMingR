using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintLibraryChecks
    {
        internal static void Check(IList<string> failures)
        {
            string area = Path.Combine(Path.GetTempPath(), "JueMingR-blueprint-library-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(area);
            int before = failures.Count;
            Run(failures, "names", () => Names());
            Run(failures, "real lifecycle", () => Lifecycle(Path.Combine(area, "lifecycle")));
            Run(failures, "layer exclusions", () => Exclusions(Path.Combine(area, "exclusions")));
            Run(failures, "corrupt and future isolation", () => Isolation(Path.Combine(area, "isolation")));
            Run(failures, "external edit and recovery", () => External(Path.Combine(area, "external")));
            Run(failures, "writer lock and precommit failure", () => Locks(Path.Combine(area, "locks")));
            Run(failures, "uncertain real commit", () => Unconfirmed(Path.Combine(area, "unconfirmed")));
            Run(failures, "logical delete cleanup", () => CleanupPending(Path.Combine(area, "cleanup")));
            Run(failures, "bounded normalization and duplicate", () => Normalization(Path.Combine(area, "normalization")));
            Run(failures, "accepted stop", () => AcceptedStop(Path.Combine(area, "stop")));
            // Failure originals remain in this printed, exclusively generated
            // root. Success cleanup cannot target installation/user data.
            if (failures.Count == before)
            {
                string resolved = Path.GetFullPath(area), parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!resolved.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("JueMingR-blueprint-library-", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe test cleanup root.");
                Directory.Delete(resolved, true);
            }
            else failures.Add("BlueprintLibrary failure material retained: " + area);
        }
        private static void Run(IList<string> failures, string name, Action test) { try { test(); } catch (Exception e) { failures.Add("BlueprintLibrary " + name + ": " + e); } }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        private static LibraryReceipt Await(LibraryState state)
        {
            var clock = Stopwatch.StartNew(); LibraryReceipt receipt;
            while (clock.ElapsedMilliseconds < 30000) { state.Pump(); if (state.TryTakeReceipt(out receipt)) return receipt; Thread.Sleep(1); }
            throw new TimeoutException("Blueprint command did not complete.");
        }
        private static LibraryReceipt Command(LibraryState state, bool accepted) { Require(accepted, "command accepted in empty slot"); return Await(state); }
        private static CaptureCell Cell(int x, int y) { return new CaptureCell { X = x, Y = y, Layers = CaptureLayer.Tile, TileType = 1 }; }
        private static CaptureToken Token(Guid id) { return new CaptureToken(id, 11, 12, 13); }
        private static ContentHandle Draft(LibraryState state, Guid id, CaptureCell[] cells, CaptureObject[] objects = null)
        {
            Require(Command(state, state.TryBeginStage(Token(id))).Status == LibraryStatus.Completed, "stage begins");
            Require(Command(state, state.TryAppendChunk(new ContentHandle(id), new CaptureChunk(cells, cells.Length, objects, objects == null ? 0 : objects.Length))).Status == LibraryStatus.Completed, "sealed records accepted");
            int left = cells.Min(c => c.X), top = cells.Min(c => c.Y), right = cells.Max(c => c.X), bottom = cells.Max(c => c.Y);
            var summary = new CaptureSummary { Left = left, Top = top, Width = right - left + 1, Height = bottom - top + 1, ContentCount = cells.Length, UnreliableObjectCount = objects == null ? 0 : objects.Count(o => !o.Reliable), StartTick = 8, EndTick = 10 };
            Require(Command(state, state.TrySealDraft(new ContentHandle(id), summary)).Status == LibraryStatus.Completed, "draft normalizes and seals"); return new ContentHandle(id);
        }
        private static Guid SaveOne(LibraryState state, string name = "蓝图")
        {
            Guid id = Guid.NewGuid(); ContentHandle handle = Draft(state, id, new[] { Cell(4, 5), Cell(400, 500) });
            LibraryReceipt receipt = Command(state, state.TryCreate(handle, name, Token(id))); Require(receipt.Status == LibraryStatus.Saved && receipt.AssetId == id && receipt.Token.SelectionRevision == 13, "only reliable create produces Saved with original correlation"); return id;
        }
        private static LibraryPage List(LibraryState state, int page = 0, Guid? select = null)
        { LibraryReceipt receipt = Command(state, state.TryList(200 + page, page, select)); Require(receipt.Status == LibraryStatus.Completed && receipt.Page != null, "real list completes"); return receipt.Page; }
        private static string Metadata(string path, Guid id) { return Path.Combine(path, id.ToString("N"), "metadata.json"); }
        private static Dictionary<string, byte[]> Body(string path, Guid id)
        { string root = Path.Combine(path, id.ToString("N"), "content"); return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToDictionary(p => p.Substring(root.Length), File.ReadAllBytes); }
        private static void SameBody(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
        { Require(expected.Count == actual.Count && expected.All(pair => actual.ContainsKey(pair.Key) && pair.Value.SequenceEqual(actual[pair.Key])), "rename never changes any body/index/root bytes"); }
        private static void Names()
        {
            string error; Require(LibraryName.TryValidate(new string('中', 48), out error), "48 Chinese text elements legal");
            Require(!LibraryName.TryValidate(new string('中', 49), out error), "49 Chinese text elements rejected");
            string joined = String.Concat(Enumerable.Repeat("👩‍💻", 48)); Require(LibraryName.TryValidate(joined, out error), "joined emoji is one existing-project text element");
            foreach (string bad in new[] { "", "  ", " 名", "名 ", "名\n字", "名\t字", "名\u2028字", "\ud800" }) Require(!LibraryName.TryValidate(bad, out error), "illegal name rejected without truncation");
            Require(LibraryName.TryValidate("A/\\:*?<>| 中文", out error), "display names are independent of filenames");
        }
        private static void Lifecycle(string path)
        {
            Guid id; Dictionary<string, byte[]> bytes;
            using (var state = new LibraryState(new FileBlueprintLibrary(path))) { id = SaveOne(state, "中文 👩‍💻"); Require(List(state).Entries.Single().Name == "中文 👩‍💻", "create visible after durable receipt"); bytes = Body(path, id); }
            using (var counted = new CountedFiles(path)) using (var state = new LibraryState(counted))
            {
                LibraryEntry entry = List(state).Entries.Single(); Require(entry.AssetId == id && entry.Width == 397 && entry.Height == 496 && entry.ContentCount == 2, "restart reads independent literal bounds/count");
                long reads = counted.BlockReads, writes = counted.BlockWrites;
                for (int i = 0; i < 100; i++) state.Pump(); Require(counted.BlockReads == reads && counted.BlockWrites == writes, "idle owner does not scan/write");
                Require(Command(state, state.TryRename(id, entry.MetadataIdentity, "重启改名")).Status == LibraryStatus.Saved, "real rename committed");
                Require(counted.BlockReads == reads && counted.BlockWrites == writes, "rename only touches small metadata"); SameBody(bytes, Body(path, id));
            }
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            { LibraryEntry entry = List(state).Entries.Single(); Require(entry.Name == "重启改名", "rename survives restart"); Require(Command(state, state.TryDelete(id, entry.MetadataIdentity)).Status == LibraryStatus.Deleted, "real delete reliably tombstones then cleans"); }
            using (var state = new LibraryState(new FileBlueprintLibrary(path))) Require(List(state).TotalCount == 0, "delete survives restart");
            Require(!Directory.Exists(Path.Combine(path, id.ToString("N"))), "all exact known asset files cleaned");
        }
        private static void Exclusions(string path)
        {
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            {
                Guid id = Guid.NewGuid(); var bad = Cell(10, 20); bad.ObjectId = 7; bad.UnreliableObject = true; bad.Layers |= CaptureLayer.Wall | CaptureLayer.Wire | CaptureLayer.Liquid; bad.WallType = 2; bad.Wires = 15; bad.LiquidAmount = 100; bad.LiquidKind = 3;
                var objectInfo = new CaptureObject { Id = 7, Type = 1, X = 10, Y = 20, Width = 1, Height = 1, Reliable = false, Reason = "(10,20) 成员尚未收到" };
                ContentHandle handle = Draft(state, id, new[] { bad, Cell(12, 20) }, new[] { objectInfo });
                Require(Command(state, state.TryCreate(handle, "错误代次", new CaptureToken(id, 999, 12, 13), true)).Status == LibraryStatus.Failed, "same operation ID cannot spoof original session correlation");
                Require(Command(state, state.TryCreate(handle, "有不可靠对象", Token(id))).Status == LibraryStatus.Failed, "unexcluded object prevents create");
                var objects = Command(state, state.TryObjects(333, handle, 0)).ObjectPage; Require(objects.Items.Single().Id == 7 && objects.Items.Single().Reason.Contains("10,20"), "specific stable object/reason readable from stage");
                var preview = Command(state, state.TryPreview(334, handle, 256, 58, false, new[] { 7 })); Require(preview.Preview.ProcessedCells == 2 && preview.Summary.UnreliableObjectCount == 1, "excluded tile preserves co-located other layers and original unresolved denominator");
                Require(Command(state, state.TryCreate(handle, "已排除", Token(id), false, new[] { 7 })).Status == LibraryStatus.Saved, "explicit per-object exclusion saves confirmed layers");
                LibraryEntry entry = List(state).Entries.Single(); Require(entry.ContentCount == 2 && entry.Width == 3, "saved bounds/count retain hole and wall/wire/liquid cell");
                LibraryReceipt savedPreview = Command(state, state.TryPreview(335, id, entry.ContentHash, 256, 58)); Require(savedPreview.Preview.ProcessedCells == 2 && savedPreview.Summary.UnreliableObjectCount == 0, "asset body has no unresolved tile state");
            }
        }
        private static void Isolation(string path)
        {
            Guid bad, healthy; using (var state = new LibraryState(new FileBlueprintLibrary(path))) { bad = SaveOne(state, "same"); healthy = SaveOne(state, "same"); Require(bad != healthy, "duplicate names retain distinct IDs"); }
            string file = Metadata(path, bad); string original = File.ReadAllText(file, Encoding.UTF8);
            foreach (string corrupt in new[] { original.Replace("\"version\":1", "\"version\":9"), original.TrimEnd().TrimEnd('}') + ",\"future\":1}", original.Substring(0, original.Length / 2), original.Replace("\"width\":397", "\"width\":2147483648") })
            {
                File.WriteAllText(file, corrupt, new UTF8Encoding(false)); byte[] before = File.ReadAllBytes(file);
                using (var state = new LibraryState(new FileBlueprintLibrary(path))) { LibraryPage page = List(state); Require(page.TotalCount == 1 && page.Entries[0].AssetId == healthy && page.IsolatedCount == 1, "bad/future asset isolated, healthy asset remains usable"); }
                Require(before.SequenceEqual(File.ReadAllBytes(file)), "bad/unknown original remains byte-for-byte");
            }
            File.WriteAllText(file, original, new UTF8Encoding(false)); string chunk = Directory.EnumerateFiles(Path.Combine(path, bad.ToString("N"), "content", "chunks")).First(); byte[] body = File.ReadAllBytes(chunk); File.WriteAllBytes(chunk, body.Take(8).ToArray());
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            { LibraryEntry entry = List(state).Entries.First(e => e.AssetId == bad); Require(Command(state, state.TryPreview(500, bad, entry.ContentHash, 256, 58)).Status != LibraryStatus.Completed, "truncated chunk cannot generate preview or masquerade as air"); }
        }
        private static void External(string path)
        {
            Guid id; using (var state = new LibraryState(new FileBlueprintLibrary(path))) id = SaveOne(state);
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            {
                LibraryEntry entry = List(state).Entries.Single(); File.AppendAllText(Metadata(path, id), "\n"); byte[] changed = File.ReadAllBytes(Metadata(path, id));
                Require(Command(state, state.TryRename(id, entry.MetadataIdentity, "不应覆盖")).Status == LibraryStatus.Protected, "expected identity catches real external edit");
                Require(state.Snapshot.Page.Entries.Single().IsProtected && List(state).Entries.Single().IsProtected, "protection is published by domain for UI and remains across list refresh");
                Require(changed.SequenceEqual(File.ReadAllBytes(Metadata(path, id))), "external original kept");
                Require(Command(state, state.TryDelete(id, entry.MetadataIdentity)).Status == LibraryStatus.Protected, "protection latched across commands");
            }
            string primary = Metadata(path, id); File.Move(primary, primary + ".bak"); byte[] recovery = File.ReadAllBytes(primary + ".bak");
            using (var state = new LibraryState(new FileBlueprintLibrary(path))) { var page = List(state); Require(page.TotalCount == 0 && page.IsolatedCount == 1, "missing metadata with recovery material is not empty/default asset"); }
            Require(!File.Exists(primary) && recovery.SequenceEqual(File.ReadAllBytes(primary + ".bak")), "recovery bytes are not restored/overwritten");
        }
        private static void Locks(string path)
        {
            Directory.CreateDirectory(path);
            using (var occupied = new FileStream(Path.Combine(path, ".library.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            { LibraryReceipt result = Command(state, state.TryList(1, 0)); Require(result.Status == LibraryStatus.Protected && result.Page == null, "real writer lease failure is distinct from empty library"); }
            string failing = Path.Combine(path, "fail-write");
            using (var files = new CountedFiles(failing) { FailBlock = true }) using (var state = new LibraryState(files))
            {
                Guid id = Guid.NewGuid(); Require(Command(state, state.TryBeginStage(Token(id))).Status == LibraryStatus.Completed, "stage created before actual disk failure");
                Require(Command(state, state.TryAppendChunk(new ContentHandle(id), new CaptureChunk(new[] { Cell(1, 1) }, 1))).Status == LibraryStatus.Failed, "occupied chunk path is actual filesystem failure");
                Require(!File.Exists(Metadata(failing, id)), "failed body write cannot publish metadata");
            }
        }
        private static void Unconfirmed(string path)
        {
            Guid id = Guid.NewGuid();
            using (var files = new CountedFiles(path) { UnknownMetadata = true }) using (var state = new LibraryState(files))
            {
                ContentHandle handle = Draft(state, id, new[] { Cell(1, 2) }); LibraryReceipt result = Command(state, state.TryCreate(handle, "未知提交", Token(id)));
                Require(result.Status == LibraryStatus.CommitUnconfirmed && result.IsProtected && File.Exists(Metadata(path, id)), "actual committed file plus lost acknowledgement reports unknown"); byte[] bytes = File.ReadAllBytes(Metadata(path, id));
                Require(Command(state, state.TryCreate(handle, "重试不能复制", Token(id))).Status == LibraryStatus.Protected, "unknown command stays protected instead of returning to retryable draft");
                Require(bytes.SequenceEqual(File.ReadAllBytes(Metadata(path, id))) && Directory.EnumerateDirectories(path).Count(p => Guid.TryParseExact(Path.GetFileName(p), "N", out _)) == 1, "one stable published identity and unchanged recovery bytes");
            }
            using (var state = new LibraryState(new FileBlueprintLibrary(path))) Require(List(state).Entries.Single().AssetId == id, "restart may discover actual commit without retroactively claiming prior certainty");
        }
        private static void CleanupPending(string path)
        {
            Guid id; using (var state = new LibraryState(new FileBlueprintLibrary(path))) id = SaveOne(state);
            string unknown = Path.Combine(path, id.ToString("N"), "future.keep"); File.WriteAllText(unknown, "owner bytes");
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            { var entry = List(state).Entries.Single(); LibraryReceipt result = Command(state, state.TryDelete(id, entry.MetadataIdentity)); Require(result.Status == LibraryStatus.Deleted && result.CleanupPending, "durable logical deletion independent of later cleanup failure"); }
            using (var state = new LibraryState(new FileBlueprintLibrary(path))) Require(List(state).TotalCount == 0, "tombstone prevents restart resurrection");
            Require(File.ReadAllText(unknown) == "owner bytes" && File.Exists(Metadata(path, id)), "unknown file and necessary tombstone preserved");
        }
        private static void Normalization(string path)
        {
            using (var state = new LibraryState(new FileBlueprintLibrary(Path.Combine(path, "sort"))))
            {
                Guid id = Guid.NewGuid(); var handle = new ContentHandle(id); Command(state, state.TryBeginStage(Token(id)));
                for (int y = 32; y >= 0; y--)
                { var cells = new CaptureCell[1024]; for (int x = 0; x < cells.Length; x++) cells[x] = Cell(1000000 + x * 1000, y); Require(Command(state, state.TryAppendChunk(handle, new CaptureChunk(cells, cells.Length))).Status == LibraryStatus.Completed, "bounded chunk append"); }
                var summary = new CaptureSummary { Left = 1000000, Top = 0, Width = 1023001, Height = 33, ContentCount = 33792, StartTick = 1, EndTick = 2 };
                Require(Command(state, state.TrySealDraft(handle, summary)).Status == LibraryStatus.Completed, "more than sixteen runs normalize without all-content memory");
                var preview = Command(state, state.TryPreview(44, handle, 10, 128)); Require(preview.Preview.ProcessedCells == 33792 && preview.Preview.GetPixel(0, preview.Preview.Height - 1) != 0, "all 33792 sparse records participate in bounded preview without bbox allocation");
                Require(Command(state, state.TryCreate(handle, "无bbox密集分配", Token(id))).Status == LibraryStatus.Saved, "large coordinates do not imply dense allocation");
            }
            using (var state = new LibraryState(new FileBlueprintLibrary(Path.Combine(path, "duplicates"))))
            {
                Guid id = Guid.NewGuid(); var handle = new ContentHandle(id); Command(state, state.TryBeginStage(Token(id)));
                Command(state, state.TryAppendChunk(handle, new CaptureChunk(new[] { Cell(1, 1) }, 1))); Command(state, state.TryAppendChunk(handle, new CaptureChunk(new[] { Cell(1, 1) }, 1)));
                var result = Command(state, state.TrySealDraft(handle, new CaptureSummary { Left = 1, Top = 1, Width = 2, Height = 1, ContentCount = 2 })); Require(result.Status != LibraryStatus.Completed, "cross-chunk duplicate is rejected by external merge");
                Require(!File.Exists(Metadata(Path.Combine(path, "duplicates"), id)), "invalid stage never published");
            }
            using (var state = new LibraryState(new FileBlueprintLibrary(Path.Combine(path, "incomplete-object"))))
            {
                Guid id = Guid.NewGuid(); var handle = new ContentHandle(id); Command(state, state.TryBeginStage(Token(id))); var cell = Cell(1, 1); cell.ObjectId = 1;
                var obj = new CaptureObject { Id = 1, Type = 1, X = 1, Y = 1, Width = 2, Height = 1, Reliable = true };
                Command(state, state.TryAppendChunk(handle, new CaptureChunk(new[] { cell }, 1, new[] { obj }, 1)));
                LibraryReceipt result = Command(state, state.TrySealDraft(handle, new CaptureSummary { Left = 1, Top = 1, Width = 1, Height = 1, ContentCount = 1 }));
                Require(result.Status != LibraryStatus.Completed, "declared reliable object cannot hide a missing member even in producer DTO");
            }
        }
        private static void AcceptedStop(string path)
        {
            Guid id = Guid.NewGuid(); var state = new LibraryState(new FileBlueprintLibrary(path));
            ContentHandle handle = Draft(state, id, new[] { Cell(1, 2) }); Require(state.TryCreate(handle, "退出期间保存", Token(id)), "create accepted before runtime retirement");
            Require(state.Stop(30000), "accepted disk owner completes before releasing lease"); state.Dispose();
            using (var reopened = new LibraryState(new FileBlueprintLibrary(path))) Require(List(reopened).Entries.Single().AssetId == id, "accepted create survives owner stop even without UI collecting receipt");
        }
        private sealed class CountedFiles : IBlueprintLibraryFiles
        {
            private readonly FileBlueprintLibrary inner;
            private readonly string path;
            internal long BlockReads, BlockWrites;
            internal bool UnknownMetadata, FailBlock;
            internal CountedFiles(string path) { this.path = path; inner = new FileBlueprintLibrary(path); }
            public void Open() { inner.Open(); }
            public IEnumerable<Guid> EnumerateAssets() { return inner.EnumerateAssets(); }
            public PreferenceReadResult OpenMetadata(Guid id, bool ownedUnpublishedContent = false) { return inner.OpenMetadata(id, ownedUnpublishedContent); }
            public PreferenceWriteResult WriteMetadata(string expectedIdentity, byte[] contents) { var result = inner.WriteMetadata(expectedIdentity, contents); return UnknownMetadata && result.Status == PreferenceWriteStatus.Saved ? new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "lost-readback-after-real-commit", true, true) : result; }
            public void CloseMetadata() { inner.CloseMetadata(); }
            public void BeginStage(Guid id) { inner.BeginStage(id); }
            public void WriteStageBlock(Guid id, long number, byte[] bytes, byte[] hash)
            {
                BlockWrites++;
                if (FailBlock)
                {
                    string name = Path.Combine(path, ".staging", id.ToString("N"), "chunks", number.ToString("D20") + ".bin"); Directory.CreateDirectory(Path.GetDirectoryName(name));
                    using (var occupied = new FileStream(name, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) inner.WriteStageBlock(id, number, bytes, hash);
                }
                else inner.WriteStageBlock(id, number, bytes, hash);
            }
            public byte[] ReadBlock(Guid id, bool stage, long number) { BlockReads++; return inner.ReadBlock(id, stage, number); }
            public Stream OpenIndex(Guid id, bool stage) { return inner.OpenIndex(id, stage); }
            public void SealStage(Guid id, byte[] root) { inner.SealStage(id, root); }
            public byte[] ReadRoot(Guid id, bool stage) { return inner.ReadRoot(id, stage); }
            public void ValidateLayout(Guid id, bool stage, long count) { inner.ValidateLayout(id, stage, count); }
            public void PromoteStage(Guid stageId, Guid assetId) { inner.PromoteStage(stageId, assetId); }
            public bool CleanupStep(Guid id, bool stage, long count, int budget) { return inner.CleanupStep(id, stage, count, budget); }
            public void Dispose() { inner.Dispose(); }
        }
    }
}
