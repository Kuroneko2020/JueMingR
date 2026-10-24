using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static partial class BlueprintLibraryChecks
    {
        private static void RecoveryRetirement(string area)
        {
            foreach (bool seal in new[] { false, true })
            {
                string path = Path.Combine(area, seal ? "seal" : "append");
                using (var files = new CountedFiles(path)) using (var state = new LibraryState(files))
                {
                    Guid old = Guid.NewGuid(); var oldHandle = new ContentHandle(old);
                    Command(state, state.TryBeginStage(Token(old)));
                    LibraryReceipt failed;
                    if (seal)
                    {
                        Command(state, state.TryAppendChunk(oldHandle, new CaptureChunk(new[] { Cell(1, 1) }, 1))); files.FailSeal = true;
                        failed = Command(state, state.TrySealDraft(oldHandle, new CaptureSummary { Left = 1, Top = 1, Width = 1, Height = 1, ContentCount = 1 }));
                    }
                    else { files.FailBlock = true; failed = Command(state, state.TryAppendChunk(oldHandle, new CaptureChunk(new[] { Cell(1, 1) }, 1))); }
                    Require(failed.Status == LibraryStatus.Failed && failed.IsProtected && failed.OperationRetired && failed.Token.OperationId == old && failed.Token.SelectionRevision == 13, "real I/O failure retires exact old token without claiming commit uncertainty");
                    files.FailBlock = files.FailSeal = false;
                    string retained = Path.Combine(path, ".staging", old.ToString("N")); var before = SnapshotFiles(retained);
                    Guid healthy = Guid.NewGuid(); var current = new ContentHandle(healthy); Command(state, state.TryBeginStage(Token(healthy)));
                    Require(Command(state, state.TryAppendChunk(oldHandle, new CaptureChunk(new[] { Cell(2, 2) }, 1))).Status != LibraryStatus.Completed, "old append cannot acquire later stage");
                    Require(Command(state, state.TrySealDraft(oldHandle, new CaptureSummary())).Status != LibraryStatus.Completed, "old seal cannot acquire later stage");
                    Require(Command(state, state.TryDiscardDraft(oldHandle)).Status != LibraryStatus.Completed, "old discard cannot clear later stage");
                    Require(Command(state, state.TryCreate(oldHandle, "旧命令", Token(old))).Status != LibraryStatus.Saved, "old create cannot publish later stage");
                    SameFiles(before, SnapshotFiles(retained), "retirement retains failed files byte for byte");
                    Require(Command(state, state.TryAppendChunk(current, new CaptureChunk(new[] { Cell(3, 4) }, 1))).Status == LibraryStatus.Completed, "stale commands cannot close the new append writer");
                    Require(Command(state, state.TrySealDraft(current, new CaptureSummary { Left = 3, Top = 4, Width = 1, Height = 1, ContentCount = 1 })).Status == LibraryStatus.Completed, "healthy operation seals after old-handle attempts");
                    var preview = Command(state, state.TryPreview(410, current, 32, 16));
                    Require(preview.Status == LibraryStatus.Completed && preview.Preview.ProcessedCells == 1, "healthy operation previews after temporary failure without restart");
                    Require(Command(state, state.TryCreate(current, "故障后新采集", Token(healthy))).Status == LibraryStatus.Saved, "healthy operation saves after temporary failure without restart");
                    SameFiles(before, SnapshotFiles(retained), "new success never cleans old recovery material");
                }
            }
        }
        private static void PromotedPrecommit(string path)
        {
#if DEBUG
            using (var files = new CountedFiles(path)) using (var state = new LibraryState(files))
            {
                Guid id = Guid.NewGuid(); ContentHandle handle = Draft(state, id, new[] { Cell(1, 2) }); files.PrecommitMetadata = true;
                LibraryReceipt receipt = Command(state, state.TryCreate(handle, "明确提交前失败", Token(id)));
                Require(files.LastMetadataResult != null && files.LastMetadataResult.Status == PreferenceWriteStatus.IoFailure && !files.LastMetadataResult.IsProtected && !files.LastMetadataResult.CommitUnconfirmed, "mechanical owned-temp failure remains known precommit and unprotected");
                Require(receipt.Status == LibraryStatus.Failed && receipt.IsProtected && receipt.OperationRetired && !receipt.Error.Contains("无法确认"), "domain termination is independent from known uncommitted metadata");
                Require(!File.Exists(Metadata(path, id)) && !File.Exists(Metadata(path, id) + ".tmp"), "owned partial temp is cleaned before the never-attempted commit");
                var original = Body(path, id);
                LibraryReceipt retry = Command(state, state.TryCreate(handle, "不得伪装重试", Token(id)));
                Require(retry.Status == LibraryStatus.Protected && retry.OperationRetired, "terminated promoted handle is not offered as retryable draft");
                SameBody(original, Body(path, id)); files.PrecommitMetadata = false;
                Guid healthy = SaveOne(state, "用户另发起新采集");
                Require(healthy != id && List(state).Entries.Single().AssetId == healthy, "explicit healthy new operation succeeds while retired unpublished payload stays isolated");
                SameBody(original, Body(path, id));
            }
#else
            Require(typeof(AtomicFileDocument).GetProperty("DebugCandidateWriteFailure", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) == null, "release omits mechanical failure injection");
#endif
        }
        private static void CleanupWork(string area)
        {
            long small = CleanupScale(Path.Combine(area, "B"), 64, 32), large = CleanupScale(Path.Combine(area, "2B"), 128, 8);
#if DEBUG
            Require(large == 2 * small, "more cleanup batches do not multiply full-index work");
#endif
            string changed = Path.Combine(area, "closed-owner");
            using (var files = new FileBlueprintLibrary(changed))
            {
                files.Open(); Guid id = Guid.NewGuid(); RawStage(files, id, 65);
                Require(!files.CleanupStep(id, true, 65, 32), "proof verification yields before any deletion"); files.CloseCleanup();
                string folder = Path.Combine(changed, ".staging", id.ToString("N")), index = Path.Combine(folder, "index.bin");
                byte[] bytes = File.ReadAllBytes(index); bytes[0] ^= 1; File.WriteAllBytes(index, bytes); var before = SnapshotFiles(folder); bool rejected = false;
                try { while (!files.CleanupStep(id, true, 65, 32)) { } } catch (InvalidDataException) { rejected = true; }
                Require(rejected, "closed handles cannot reuse old path trust after an external index change"); SameFiles(before, SnapshotFiles(folder), "untrusted reopened index never authorizes deletion");
            }
            string bodyChanged = Path.Combine(area, "chunk-after-proof");
            using (var files = new FileBlueprintLibrary(bodyChanged))
            {
                files.Open(); Guid id = Guid.NewGuid(); RawStage(files, id, 2);
                Require(!files.CleanupStep(id, true, 2, 2) && !files.CleanupStep(id, true, 2, 2) && !files.CleanupStep(id, true, 2, 2), "index and known-name proof complete before deletion batch");
                string folder = Path.Combine(bodyChanged, ".staging", id.ToString("N"));
                string first = Path.Combine(folder, "chunks", 0L.ToString("D20") + ".bin"); File.WriteAllBytes(first, new byte[] { 4, 3, 2, 1 });
                var before = SnapshotFiles(folder); bool rejected = false;
                try { files.CleanupStep(id, true, 2, 2); } catch (InvalidDataException) { rejected = true; }
                Require(rejected, "authenticated index reuse still checks actual block bytes immediately before deletion"); SameFiles(before, SnapshotFiles(folder), "changed first chunk and untouched siblings survive failed deletion");
            }
        }
        private static long CleanupScale(string path, int blocks, int budget)
        {
            using (var files = new FileBlueprintLibrary(path))
            {
                files.Open(); Guid id = Guid.NewGuid(); RawStage(files, id, blocks);
                Require(!files.CleanupStep(id, true, blocks, budget), "first bounded proof batch yields");
                string folder = Path.Combine(path, ".staging", id.ToString("N"));
                foreach (string name in new[] { "index.bin", "root.bin", ".owner.bin" })
                {
                    string file = Path.Combine(folder, name); bool writeBlocked = false, renameBlocked = false;
                    try { using (var writer = new FileStream(file, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete)) { } } catch (IOException) { writeBlocked = true; }
                    try { File.Move(file, file + ".moved"); } catch (IOException) { renameBlocked = true; }
                    Require(writeBlocked && renameBlocked, "in-flight proof pins deny writes and ordinary path substitution: " + name);
                }
                int calls = 1; while (!files.CleanupStep(id, true, blocks, budget)) { calls++; Require(calls < blocks * 5, "bounded cleanup cursor makes progress"); }
                Require(!Directory.Exists(folder), "exact private stage fully cleaned");
#if DEBUG
                Require(files.DebugCleanupIndexScans == 2, "one authenticated index pass plus one final identity check");
                Require(files.DebugCleanupIndexBytes == blocks * 96L && files.DebugCleanupChainRecords == blocks, "index proof, per-block hash lookup and final digest each linear; authenticated chain once");
                Require(files.DebugCleanupNameVisits == blocks && files.DebugCleanupBlockVisits == blocks, "names and deletion cursor each visit a block once");
                Console.WriteLine("BlueprintLibrary cleanup B=" + blocks + " budget=" + budget + " indexScans=" + files.DebugCleanupIndexScans + " bytes=" + files.DebugCleanupIndexBytes + " chain=" + files.DebugCleanupChainRecords + " names=" + files.DebugCleanupNameVisits + " blocks=" + files.DebugCleanupBlockVisits);
                return files.DebugCleanupIndexBytes;
#else
                return 0;
#endif
            }
        }
        private static void RawStage(FileBlueprintLibrary files, Guid id, int blocks)
        {
            files.BeginStage(id);
            for (int number = 0; number < blocks; number++) { byte[] bytes = BitConverter.GetBytes(number); files.WriteStageBlock(id, number, bytes, Digest(bytes)); }
            files.SealStage(id, new byte[] { 1, 2, 3 });
        }
        private static void NormalizeWork(string area)
        {
            long small = NormalizeScale(Path.Combine(area, "B"), 33), large = NormalizeScale(Path.Combine(area, "2B"), 66);
            Require(large == 2 * small, "first sealed-source index work doubles with B rather than quadrupling");
        }
        private static long NormalizeScale(string path, int blocks)
        {
            using (var files = new CountedFiles(path)) using (var state = new LibraryState(files))
            {
                Guid id = Guid.NewGuid(); var handle = new ContentHandle(id); Command(state, state.TryBeginStage(Token(id)));
                for (int y = blocks - 1; y >= 0; y--) Command(state, state.TryAppendChunk(handle, new CaptureChunk(new[] { Cell(1, y) }, 1)));
                Require(Command(state, state.TrySealDraft(handle, new CaptureSummary { Left = 1, Width = 1, Height = blocks, ContentCount = blocks })).Status == LibraryStatus.Completed, "counted multi-group source seals");
                Require(files.IndexOpens[id] == 2 && files.IndexBytes[id] == blocks * 128L, "original source opens for root digest and one pinned merge validation, with two per-block cursor passes");
                Require(Command(state, state.TryPreview(811, handle, 16, 16)).Preview.ProcessedCells == blocks, "all normalized cells survive index reuse");
                Console.WriteLine("BlueprintLibrary normalize B=" + blocks + " sourceIndexOpens=" + files.IndexOpens[id] + " bytes=" + files.IndexBytes[id]);
                return files.IndexBytes[id];
            }
        }
        private static void CleanupCancellation(string area)
        {
            string path = Path.Combine(area, "list"); Guid id = Guid.NewGuid(); WriteLiteralAsset(path, id, 65);
            string hold = Path.Combine(path, id.ToString("N"), "hold.keep"); File.WriteAllText(hold, "delay only cleanup");
            using (var files = new CountedFiles(path)) using (var state = new LibraryState(files))
            {
                LibraryEntry entry = List(state).Entries.Single();
                Require(Command(state, state.TryDelete(id, entry.MetadataIdentity)).CleanupPending, "reliable tombstone retained before cancellation fixture"); File.Delete(hold);
                string asset = Path.Combine(path, id.ToString("N")); var before = SnapshotFiles(asset); int batches = 0;
                files.AfterCleanupStep = () => { batches++; state.CancelRead(901); };
                Require(Command(state, state.TryList(901, 0)).Status == LibraryStatus.Cancelled && batches == 1, "List read cancellation stops at first safe index proof batch");
                SameFiles(before, SnapshotFiles(asset), "cancelled verification does not delete or alter protected material");
                using (var checkReleased = new FileStream(Path.Combine(asset, "content", "index.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                files.AfterCleanupStep = null; Require(List(state).TotalCount == 0 && !Directory.Exists(asset), "next read revalidates and completes exact tombstone cleanup");
            }
            string stopping = Path.Combine(area, "stop"); Guid stopped = Guid.NewGuid(); WriteLiteralAsset(stopping, stopped, 65);
            using (var files = new CountedFiles(stopping)) using (var state = new LibraryState(files))
            {
                LibraryEntry entry = List(state).Entries.Single(); files.AfterCleanupStep = () => state.BeginStop();
                Require(state.TryDelete(stopped, entry.MetadataIdentity), "delete accepted before Stop"); Require(state.Stop(30000), "Stop releases at a safe cleanup batch");
                LibraryReceipt receipt = Await(state);
                Require(receipt.Status == LibraryStatus.Deleted && receipt.CleanupPending, "Stop cannot revoke reliable deletion or claim all cleanup finished");
            }
            using (var state = new LibraryState(new FileBlueprintLibrary(stopping))) Require(List(state).TotalCount == 0, "restart resumes trusted tombstone proof without resurrection");
            Require(!Directory.Exists(Path.Combine(stopping, stopped.ToString("N"))), "restart completes safely interrupted accepted deletion");
        }
        private static void LegacyLiteralAsset(string path)
        {
            Guid id = new Guid("6d161e71-33ed-4c27-9715-0c4597a13c83"); WriteLiteralAsset(path, id, 2);
            string asset = Path.Combine(path, id.ToString("N")); var before = SnapshotFiles(asset);
            using (var state = new LibraryState(new FileBlueprintLibrary(path)))
            {
                LibraryEntry entry = List(state).Entries.Single();
                Require(Command(state, state.TryPreview(301, id, entry.ContentHash, 32, 16)).Preview.ProcessedCells == 2, "old v1 raw frame snapshots preview without fixed group IDs");
                var afterRead = SnapshotFiles(asset); foreach (var file in before) Require(afterRead[file.Key].SequenceEqual(file.Value), "legacy loading/preview never rewrites original bytes");
                var body = Body(path, id); Require(Command(state, state.TryRename(id, entry.MetadataIdentity, "旧资产改名")).Status == LibraryStatus.Saved, "old v1 asset renames"); SameBody(body, Body(path, id));
                entry = List(state).Entries.Single(); Require(Command(state, state.TryDelete(id, entry.MetadataIdentity)).Status == LibraryStatus.Deleted, "old v1 asset deletes by reliable proof");
            }
            Require(!Directory.Exists(asset), "old v1 asset cleanly removed");
        }
        private static void WriteLiteralAsset(string path, Guid id, int blocks)
        {
            // Literal 75cd4ea v1 wire fields, independent of current encoders.
            // ObjectId=0 already meant a raw cell snapshot, including its frame.
            Guid revision = new Guid("ba3529cd-80de-4f2a-9ab3-1083e29fc995");
            string asset = Path.Combine(path, id.ToString("N")), content = Path.Combine(asset, "content"), chunks = Path.Combine(content, "chunks"); Directory.CreateDirectory(chunks);
            byte[] index;
            using (var indexes = new MemoryStream())
            {
                for (int number = 0; number < blocks; number++)
                {
                    byte[] bytes;
                    using (var stream = new MemoryStream()) using (var w = new BinaryWriter(stream))
                    {
                        w.Write(0x43424D4A); w.Write(1); w.Write(1); w.Write(0);
                        w.Write(0); w.Write(number); w.Write(0); w.Write((byte)1); w.Write((ushort)5); w.Write((ushort)0); w.Write((short)54); w.Write((short)72); for (int i = 0; i < 7; i++) w.Write((byte)0);
                        w.Flush(); bytes = stream.ToArray();
                    }
                    File.WriteAllBytes(Path.Combine(chunks, number.ToString("D20") + ".bin"), bytes); byte[] hash = Digest(bytes); indexes.Write(hash, 0, hash.Length);
                }
                index = indexes.ToArray();
            }
            File.WriteAllBytes(Path.Combine(content, "index.bin"), index); byte[] root;
            using (var stream = new MemoryStream()) using (var w = new BinaryWriter(stream))
            {
                w.Write(0x42524D4A); w.Write(1); w.Write(revision.ToByteArray()); w.Write((long)blocks); w.Write(Digest(index));
                w.Write(0); w.Write(0); w.Write(1); w.Write(blocks); w.Write((long)blocks); w.Write(0L); w.Write(1UL); w.Write(2UL);
                byte[] version = Encoding.UTF8.GetBytes("1.4.5.8"); w.Write(version.Length); w.Write(version); w.Write(0); w.Flush(); root = stream.ToArray();
            }
            File.WriteAllBytes(Path.Combine(content, "root.bin"), root);
            File.WriteAllText(Path.Combine(asset, "metadata.json"), "{\"format\":\"JueMingR.BlueprintAsset\",\"version\":1,\"assetId\":\"" + id.ToString("N") + "\",\"name\":\"旧资产\",\"contentRevision\":\"" + revision.ToString("N") + "\",\"contentHash\":\"" + Convert.ToBase64String(Digest(root)) + "\",\"width\":1,\"height\":" + blocks + ",\"contentCount\":" + blocks + ",\"blockCount\":" + blocks + ",\"missingSummary\":\"\",\"createdTicks\":1,\"updatedTicks\":2,\"deleted\":false}\n", new UTF8Encoding(false));
        }
        private sealed class CountedIndex : Stream
        {
            private readonly Stream inner; private readonly CountedFiles owner; private readonly Guid id;
            internal CountedIndex(Stream inner, CountedFiles owner, Guid id)
            { this.inner = inner; this.owner = owner; this.id = id; long count; owner.IndexOpens.TryGetValue(id, out count); owner.IndexOpens[id] = count + 1; if (!owner.IndexBytes.ContainsKey(id)) owner.IndexBytes[id] = 0; }
            public override bool CanRead { get { return inner.CanRead; } }
            public override bool CanSeek { get { return inner.CanSeek; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { return inner.Length; } }
            public override long Position { get { return inner.Position; } set { inner.Position = value; } }
            public override int Read(byte[] buffer, int offset, int count) { int read = inner.Read(buffer, offset, count); owner.IndexBytes[id] += read; return read; }
            public override long Seek(long offset, SeekOrigin origin) { return inner.Seek(offset, origin); }
            public override void Flush() { inner.Flush(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        }
    }
}
