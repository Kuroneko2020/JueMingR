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
    internal static partial class BlueprintLibraryChecks
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
            Run(failures, "precise read cancellation", () => ReadCancellation(Path.Combine(area, "read-cancel")));
            Run(failures, "literal direction codec", () => DirectionFiles(Path.Combine(area, "direction")));
            Run(failures, "default names", () => DefaultNames(Path.Combine(area, "default-names")));
            Run(failures, "tombstone foreign replacements", () => CleanupReplacements(Path.Combine(area, "cleanup-replacements")));
            Run(failures, "private stage ownership", () => StageOwnership(Path.Combine(area, "stage-ownership")));
            Run(failures, "safe cleanup restart", () => InterruptedCleanup(Path.Combine(area, "cleanup-restart")));
            Run(failures, "failed stages retire without taking new owner", () => RecoveryRetirement(Path.Combine(area, "retirement")));
            Run(failures, "promoted precommit certainty", () => PromotedPrecommit(Path.Combine(area, "precommit")));
            Run(failures, "pinned cleanup and bounded work", () => CleanupWork(Path.Combine(area, "cleanup-work")));
            Run(failures, "normalize index source reuse", () => NormalizeWork(Path.Combine(area, "normalize-work")));
            Run(failures, "safe list cancellation and stop", () => CleanupCancellation(Path.Combine(area, "cleanup-cancel")));
            Run(failures, "legacy v1 literal asset", () => LegacyLiteralAsset(Path.Combine(area, "legacy-v1")));
            Run(failures, "historical missing compatibility", () => HistoricalMissing(Path.Combine(area, "historical-missing")));
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
        private static void HistoricalMissing(string path)
        {
            Guid id = Guid.NewGuid();
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            {
                var cell = Cell(4, 5); cell.ObjectId = 1;
                var obj = new CaptureObject { Id = 1, X = 4, Y = 5, Width = 1, Height = 1, Type = 1, Reliable = true, MissingPayload = "箱内物品未采集" };
                var handle = Draft(state, id, new[] { cell }, new[] { obj });
                Require(Command(state, state.TryCreate(handle, "旧含Missing图", Token(id))).Status == LibraryStatus.Saved, "legacy semantic field survives reliable v1 creation");
                var entry = List(state).Entries.Single(); var before = SnapshotFiles(Path.Combine(path, id.ToString("N")));
                Require(entry.MissingSummary == obj.MissingPayload && Command(state, state.TryPreview(77, id, entry.ContentHash, 16, 16)).Summary.MissingPayloads.Single() == obj.MissingPayload, "v1 root/metadata/object historical Missing remains readable");
                var after = SnapshotFiles(Path.Combine(path, id.ToString("N"))); foreach (var file in before) Require(after[file.Key].SequenceEqual(file.Value), "quiet UI policy never rewrites old content");
            }
            string metadata = Path.Combine(path, id.ToString("N"), "metadata.json");
            File.WriteAllText(metadata, File.ReadAllText(metadata).Replace("箱内物品未采集", "展示物品未采集"), new UTF8Encoding(false));
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            {
                var page = List(state);
                Require(page.TotalCount == 0 && page.IsolatedCount == 1, "real metadata/root Missing mismatch still isolates the bad asset");
            }
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
                Require(!Directory.EnumerateFiles(Path.Combine(path, id.ToString("N"), "content"), ".owner.bin", SearchOption.AllDirectories).Any(), "published body has no private stage ownership marker");
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
        private static void DirectionFiles(string path)
        {
            var decode = typeof(LibraryState).Assembly.GetType("JueMingR.Features.Blueprint.LibraryCodec", true).GetMethod("DecodeChunk", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            using (var files = new FileBlueprintLibrary(path))
            {
                files.Open();
                foreach (int direction in new[] { 0, 1, 2, -1, 3, Int32.MaxValue })
                {
                    Guid id = Guid.NewGuid(); files.BeginStage(id); byte[] literal = DirectionLiteral(direction); files.WriteStageBlock(id, 0, literal, Digest(literal)); byte[] disk = files.ReadBlock(id, true, 0);
                    bool rejected = false;
                    try { var chunk = (CaptureChunk)decode.Invoke(null, new object[] { disk }); Require(chunk.Objects[0].Direction == direction, "literal 0/1/2 direction decoded exactly"); }
                    catch (System.Reflection.TargetInvocationException e) when (e.InnerException is InvalidDataException) { rejected = true; }
                    Require(rejected == (direction < 0 || direction > 2), "Terraria enum accepts only None0/PlaceLeft1/PlaceRight2"); Require(literal.SequenceEqual(files.ReadBlock(id, true, 0)), "invalid literal file bytes remain unchanged");
                }
            }
        }
        private static byte[] DirectionLiteral(int direction)
        {
            // Independent format fixture, not a production encoder round trip.
            using (var stream = new MemoryStream()) using (var w = new BinaryWriter(stream))
            {
                w.Write(0x43424D4A); w.Write(1); w.Write(1); w.Write(1);
                w.Write(1); w.Write(1); w.Write(1); w.Write((byte)1); w.Write((ushort)1); w.Write((ushort)0); w.Write((short)0); w.Write((short)0); for (int i = 0; i < 7; i++) w.Write((byte)0);
                w.Write(1); w.Write((ushort)1); w.Write(1); w.Write(1); w.Write(1); w.Write(1); w.Write(0); w.Write(0); w.Write(0); w.Write(0); w.Write(direction); w.Write(0); w.Write(0); w.Write(true); w.Write(0); w.Write(0);
                w.Flush(); return stream.ToArray();
            }
        }
        private static void DefaultNames(string path)
        {
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            {
                Guid empty = Guid.NewGuid(); Require(Command(state, state.TryBeginStage(Token(empty))).SuggestedName == "新蓝图", "empty library suggests original default"); Command(state, state.TryDiscardDraft(new ContentHandle(empty)));
                for (int i = 0; i < 8; i++) SaveOne(state, "手动 " + i);
                SaveOne(state, "新蓝图"); SaveOne(state, "新蓝图 9"); SaveOne(state, "新蓝图 009");
                Guid id = Guid.NewGuid(); LibraryReceipt begin = Command(state, state.TryBeginStage(Token(id))); Require(begin.SuggestedName == "新蓝图 10", "default naming scans all assets beyond six visible items and ignores noncanonical suffix");
                Command(state, state.TryDiscardDraft(new ContentHandle(id)));
                SaveOne(state, "新蓝图"); Require(List(state).TotalCount == 12, "manual duplicate name remains allowed");
            }
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            {
                Guid id = Guid.NewGuid(); Require(Command(state, state.TryBeginStage(Token(id))).SuggestedName == "新蓝图 10", "default suggestion survives restart without a cached page"); Command(state, state.TryDiscardDraft(new ContentHandle(id)));
            }
        }
        private static void CleanupReplacements(string area)
        {
            foreach (string kind in new[] { "root", "index", "backup", "metadata-temp", "content-recovery", "future-tombstone", "primary-bytes", "intent" })
            {
                string path = Path.Combine(area, kind); Guid id;
                using (var state = new LibraryState(new FileBlueprintLibrary(path))) id = SaveOne(state);
                string asset = Path.Combine(path, id.ToString("N")), hold = Path.Combine(asset, "hold.keep"); File.WriteAllText(hold, "prevent first cleanup");
                using (var state = new LibraryState(new FileBlueprintLibrary(path))) { LibraryEntry entry = List(state).Entries.Single(); LibraryReceipt deleted = Command(state, state.TryDelete(id, entry.MetadataIdentity)); Require(deleted.Status == LibraryStatus.Deleted && deleted.CleanupPending, "durable tombstone precedes deliberately paused cleanup"); }
                File.Delete(hold);
                if (kind == "root") File.WriteAllText(Path.Combine(asset, "content", "root.bin"), "future root replacement");
                else if (kind == "index") { string chunk = Directory.EnumerateFiles(Path.Combine(asset, "content", "chunks")).First(); byte[] bytes = File.ReadAllBytes(chunk); bytes[0] ^= 1; File.WriteAllBytes(chunk, bytes); File.WriteAllBytes(Path.Combine(asset, "content", "index.bin"), Digest(bytes)); }
                else if (kind == "backup") File.WriteAllText(Path.Combine(asset, "metadata.json.bak"), "{\"format\":\"future owner backup\"}");
                else if (kind == "metadata-temp") File.WriteAllText(Path.Combine(asset, "metadata.json.tmp"), "recovery candidate");
                else if (kind == "content-recovery") File.WriteAllText(Path.Combine(asset, "content", "root.bin.bak"), "private recovery bytes");
                else if (kind == "primary-bytes") File.AppendAllText(Metadata(path, id), "\n");
                else if (kind == "intent") File.WriteAllText(Path.Combine(asset, ".delete.bin"), "future cleanup format");
                else File.WriteAllText(Metadata(path, id), File.ReadAllText(Metadata(path, id)).Replace("\"version\":1", "\"version\":9"), new UTF8Encoding(false));
                Dictionary<string, byte[]> before = SnapshotFiles(asset);
                using (var state = new LibraryState(new FileBlueprintLibrary(path))) Require(List(state).TotalCount == 0, "logical deletion never resurrects when cleanup proof is invalid");
                SameFiles(before, SnapshotFiles(asset), "restart preserves all foreign root/index/backup/recovery bytes");
            }
        }
        private static void StageOwnership(string area)
        {
            foreach (string kind in new[] { "root", "index", "owner", "recovery" })
            {
                string path = Path.Combine(area, kind);
                using (var state = new LibraryState(new FileBlueprintLibrary(path)))
                {
                    Guid id = Guid.NewGuid(); ContentHandle handle = Draft(state, id, new[] { Cell(1, 2) }); string folder = Directory.EnumerateDirectories(Path.Combine(path, ".staging")).Single();
                    if (kind == "root") File.WriteAllText(Path.Combine(folder, "root.bin"), "future stage root");
                    else if (kind == "index") File.WriteAllBytes(Path.Combine(folder, "index.bin"), new byte[32]);
                    else if (kind == "owner") { string file = Path.Combine(folder, ".owner.bin"); byte[] bytes = File.ReadAllBytes(file); bytes[0] ^= 1; File.WriteAllBytes(file, bytes); }
                    else File.WriteAllText(Path.Combine(folder, "root.bin.tmp"), "recovery bytes");
                    Dictionary<string, byte[]> before = SnapshotFiles(folder); Require(Command(state, state.TryDiscardDraft(handle)).Status != LibraryStatus.Completed, "cancel cannot delete changed private stage"); SameFiles(before, SnapshotFiles(folder), "private stage foreign bytes preserved on cancel");
                }
            }
            string oldPath = Path.Combine(area, "old-process"); Guid physical;
            using (var state = new LibraryState(new FileBlueprintLibrary(oldPath)))
            {
                Guid id = Guid.NewGuid(); Draft(state, id, new[] { Cell(1, 2) }); string folder = Directory.EnumerateDirectories(Path.Combine(oldPath, ".staging")).Single(); physical = Guid.ParseExact(Path.GetFileName(folder), "N");
                // Simulate a protected exit, so disposal cannot clean this stage.
                File.WriteAllText(Path.Combine(folder, "root.bin.tmp"), "protected recovery");
            }
            string oldFolder = Path.Combine(oldPath, ".staging", physical.ToString("N")); File.Delete(Path.Combine(oldFolder, "root.bin.tmp")); var oldBytes = SnapshotFiles(oldFolder); bool rejected = false;
            using (var files = new FileBlueprintLibrary(oldPath)) { files.Open(); try { files.CleanupStep(physical, true, 1, 32); } catch (InvalidDataException) { rejected = true; } }
            Require(rejected, "a restarted worker cannot adopt an old stage owner proof"); SameFiles(oldBytes, SnapshotFiles(oldFolder), "old private stage retained byte for byte");
        }
        private static void InterruptedCleanup(string path)
        {
            Guid id = Guid.NewGuid();
            using (var files = new CountedFiles(path)) using (var state = new LibraryState(files))
            {
                var handle = new ContentHandle(id); Command(state, state.TryBeginStage(Token(id)));
                for (int y = 0; y < 65; y++) { var cells = new CaptureCell[1024]; for (int x = 0; x < cells.Length; x++) cells[x] = Cell(x, y); Command(state, state.TryAppendChunk(handle, new CaptureChunk(cells, cells.Length))); }
                Require(Command(state, state.TrySealDraft(handle, new CaptureSummary { Left = 0, Top = 0, Width = 1024, Height = 65, ContentCount = 66560 })).Status == LibraryStatus.Completed, "large cleanup fixture seals");
                Require(Command(state, state.TryCreate(handle, "中断清理", Token(id))).Status == LibraryStatus.Saved, "large cleanup fixture saves"); LibraryEntry entry = List(state).Entries.Single(); files.InterruptCleanup = true;
                string chunks = Path.Combine(path, id.ToString("N"), "content", "chunks"); int before = Directory.EnumerateFiles(chunks).Count();
                LibraryReceipt deleted = Command(state, state.TryDelete(id, entry.MetadataIdentity)); Require(deleted.Status == LibraryStatus.Deleted && deleted.CleanupPending, "interruption after a real bounded cleanup batch keeps logical delete");
                int remaining = Directory.EnumerateFiles(chunks).Count(); Require(remaining > 0 && remaining < before, "interruption follows actual deletion and leaves some exact owned blocks for restart");
            }
            using (var state = new LibraryState(new FileBlueprintLibrary(path))) Require(List(state).TotalCount == 0, "restart finishes remaining trusted cleanup without resurrection");
            Require(!Directory.Exists(Path.Combine(path, id.ToString("N"))), "safe interrupted cleanup removes remaining exact known files");
        }
        private static Dictionary<string, byte[]> SnapshotFiles(string root) { return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToDictionary(name => name.Substring(root.Length), File.ReadAllBytes); }
        private static void SameFiles(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual, string message) { Require(expected.Count == actual.Count && expected.All(pair => actual.ContainsKey(pair.Key) && pair.Value.SequenceEqual(actual[pair.Key])), message); }
        private static byte[] Digest(byte[] bytes) { using (var hash = System.Security.Cryptography.SHA256.Create()) return hash.ComputeHash(bytes); }
        private static void ReadCancellation(string path)
        {
            using (var files = new CountedFiles(path)) using (var state = new LibraryState(files))
            {
                Guid id = Guid.NewGuid(); var handle = new ContentHandle(id); Command(state, state.TryBeginStage(Token(id)));
                for (int y = 0; y < 4; y++) { var cells = Enumerable.Range(0, 2048).Select(x => Cell(x, y)).ToArray(); Command(state, state.TryAppendChunk(handle, new CaptureChunk(cells, cells.Length))); }
                Require(Command(state, state.TrySealDraft(handle, new CaptureSummary { Width = 2048, Height = 4, ContentCount = 8192 })).Status == LibraryStatus.Completed, "real multi-block draft for cancellation");
                foreach (long generation in new long[] { 71, 72 })
                {
                    files.BlockReads = 0; files.BeforeBlockRead = () => state.CancelRead(generation);
                    LibraryReceipt receipt = Command(state, generation == 71 ? state.TryPreview(generation, handle, 32, 16) : state.TryObjects(generation, handle, 0));
                    Require(receipt.Status == LibraryStatus.Cancelled && files.BlockReads == 1, "obsolete preview/object read stops at its next bounded chunk, without protecting or discarding draft");
                }
                files.BeforeBlockRead = () => state.CancelRead(0);
                Require(Command(state, state.TryCreate(handle, "受理写入", Token(id))).Status == LibraryStatus.Saved, "a read cancellation with a write's numeric generation cannot cancel accepted Create");
                files.BeforeBlockRead = null; files.Enumerated = 0; files.BeforeEnumerate = () => state.CancelRead(73);
                Require(Command(state, state.TryList(73, 0)).Status == LibraryStatus.Cancelled && files.Enumerated == 1, "obsolete list stops before reading the first yielded asset metadata");
                files.BeforeEnumerate = null;
                Require(List(state).TotalCount == 1, "a new read generation proceeds and accepted write remains durable");
            }
        }
        private sealed class CountedFiles : IBlueprintLibraryFiles
        {
            private readonly FileBlueprintLibrary inner;
            private readonly string path;
            internal long BlockReads, BlockWrites;
            internal bool UnknownMetadata, FailBlock, FailSeal, InterruptCleanup;
            internal bool PrecommitMetadata = false;
            internal Action BeforeBlockRead, BeforeEnumerate, AfterCleanupStep;
            internal readonly Dictionary<Guid, long> IndexBytes = new Dictionary<Guid, long>(), IndexOpens = new Dictionary<Guid, long>();
            internal PreferenceWriteResult LastMetadataResult;
            internal int Enumerated;
            internal CountedFiles(string path) { this.path = path; inner = new FileBlueprintLibrary(path); }
            public void Open() { inner.Open(); }
            public IEnumerable<Guid> EnumerateAssets() { foreach (Guid id in inner.EnumerateAssets()) { Enumerated++; BeforeEnumerate?.Invoke(); yield return id; } }
            public PreferenceReadResult OpenMetadata(Guid id, bool ownedUnpublishedContent = false) { return inner.OpenMetadata(id, ownedUnpublishedContent); }
            public PreferenceWriteResult WriteMetadata(string expectedIdentity, byte[] contents)
            {
                // Debug-only mechanical injection writes a partial owned .tmp
                // then takes the original I/O catch and cleanup path. It does
                // not fabricate a result or change the release write protocol.
                if (PrecommitMetadata)
                {
                    object document = typeof(FileBlueprintLibrary).GetField("metadata", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(inner);
                    var failure = document.GetType().GetProperty("DebugCandidateWriteFailure", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (failure == null) throw new InvalidOperationException("Debug candidate write injection unavailable");
                    failure.SetValue(document, true, null);
                }
                var result = inner.WriteMetadata(expectedIdentity, contents); LastMetadataResult = result;
                return UnknownMetadata && result.Status == PreferenceWriteStatus.Saved ? new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "lost-readback-after-real-commit", true, true) : result;
            }
            public void CloseMetadata() { inner.CloseMetadata(); }
            public void BeginStage(Guid id) { inner.BeginStage(id); }
            public void CloseStageWriter() { inner.CloseStageWriter(); }
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
            public byte[] ReadBlock(Guid id, bool stage, long number) { BlockReads++; BeforeBlockRead?.Invoke(); return inner.ReadBlock(id, stage, number); }
            public Stream OpenIndex(Guid id, bool stage) { return new CountedIndex(inner.OpenIndex(id, stage), this, id); }
            public void SealStage(Guid id, byte[] root)
            {
                if (FailSeal)
                {
                    string name = Path.Combine(path, ".staging", id.ToString("N"), "root.bin");
                    using (var occupied = new FileStream(name, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) inner.SealStage(id, root);
                }
                else inner.SealStage(id, root);
            }
            public byte[] ReadRoot(Guid id, bool stage) { return inner.ReadRoot(id, stage); }
            public void ValidateLayout(Guid id, bool stage, long count) { inner.ValidateLayout(id, stage, count); }
            public void PromoteStage(Guid stageId, Guid assetId) { inner.PromoteStage(stageId, assetId); }
            public void PrepareCleanup(Guid id, BlueprintCleanupProof proof) { inner.PrepareCleanup(id, proof); }
            public bool CleanupStep(Guid id, bool stage, long count, int budget, BlueprintCleanupProof proof = null)
            {
                string chunks = Path.Combine(path, id.ToString("N"), "content", "chunks");
                int before = !stage && InterruptCleanup ? Directory.EnumerateFiles(chunks).Count() : 0;
                bool completed = inner.CleanupStep(id, stage, count, budget, proof); AfterCleanupStep?.Invoke();
                if (!stage && InterruptCleanup && !completed && Directory.EnumerateFiles(chunks).Count() < before) throw new IOException("test interruption after actual safe deletion batch");
                return completed;
            }
            public void CloseCleanup() { inner.CloseCleanup(); }
            public void Dispose() { inner.Dispose(); }
        }
    }
}
