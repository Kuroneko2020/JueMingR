using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;

namespace JueMingR.Infrastructure.Blueprint
{
    // The stable library lease spans every asset operation, including the final
    // removal of an asset lock. Unknown entries and links are never cleanup work.
    public sealed class FileBlueprintLibrary : IBlueprintLibraryFiles
    {
        public const int MaximumBlockBytes = 1048576;
        private readonly string directory;
        private FileStream lease;
        private AtomicFileDocument metadata;
        private readonly byte[] stageKey = NewStageKey();
        private FileStream stageIndex;
        private Guid stageWriter;
        private Guid metadataAsset;
        private string preparedCleanupIdentity;
        private bool disposed;
        public FileBlueprintLibrary(string fullTemplatesDirectory)
        {
            if (String.IsNullOrWhiteSpace(fullTemplatesDirectory) || !Path.IsPathRooted(fullTemplatesDirectory)) throw new ArgumentException("blueprint-explicit-root");
            directory = Path.GetFullPath(fullTemplatesDirectory);
            if (!String.Equals(Path.GetPathRoot(directory), Path.GetPathRoot(fullTemplatesDirectory), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("blueprint-absolute-root");
        }
        public void Open()
        {
            Alive(); if (lease != null) return;
            Guard(directory); Directory.CreateDirectory(directory); Guard(directory);
            string path = Path.Combine(directory, ".library.lock"); Guard(path);
            lease = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        public IEnumerable<Guid> EnumerateAssets()
        {
            Ready(); Guard(directory);
            foreach (string entry in Directory.EnumerateDirectories(directory))
            {
                Guid id; string name = Path.GetFileName(entry);
                if (!Guid.TryParseExact(name, "N", out id) || id == Guid.Empty || name != id.ToString("N")) continue;
                // A valid-looking linked asset must be reported as a bad item by
                // the caller, rather than silently traversed during enumeration.
                yield return id;
            }
        }
        public PreferenceReadResult OpenMetadata(Guid assetId, bool ownedUnpublishedContent = false)
        {
            Ready(); CloseMetadata(); string asset = Asset(assetId); Guard(asset);
            string path = Path.Combine(asset, "metadata.json"); GuardDocument(path);
            if (!File.Exists(path) && Directory.Exists(asset) && !ownedUnpublishedContent)
                foreach (string entry in Directory.EnumerateFileSystemEntries(asset))
                    if (Path.GetFileName(entry) != "metadata.json.lock") throw new InvalidDataException("blueprint-missing-metadata-with-material");
            metadataAsset = assetId; metadata = new AtomicFileDocument(path, 65536, true); return metadata.Read();
        }
        public PreferenceWriteResult WriteMetadata(string expectedIdentity, byte[] contents)
        {
            Ready(); if (metadata == null) throw new InvalidOperationException("blueprint-metadata-not-open");
            string intentPath = Path.Combine(Asset(metadataAsset), ".delete.bin"); Guard(intentPath);
            if (File.Exists(intentPath) || Directory.Exists(intentPath))
            {
                BlueprintCleanupProof intent = ReadCleanupIntent(metadataAsset);
                string candidate = Convert.ToBase64String(Hash(contents));
                if (candidate != preparedCleanupIdentity || candidate != intent.MetadataIdentity || expectedIdentity != intent.SourceMetadataIdentity) throw new InvalidDataException("blueprint-cleanup-recovery-protected");
            }
            return metadata.Write(expectedIdentity, contents);
        }
        public void CloseMetadata() { if (metadata != null) metadata.Dispose(); metadata = null; metadataAsset = Guid.Empty; preparedCleanupIdentity = null; }
        public void BeginStage(Guid id)
        {
            Ready(); string path = Content(id, true); Guard(path);
            if (Directory.Exists(path) || File.Exists(path) || Directory.Exists(Asset(id)) || File.Exists(Asset(id))) throw new InvalidDataException("blueprint-identity-already-exists");
            Directory.CreateDirectory(path); Guard(path);
            CloseStageWriter(); stageWriter = id;
            stageIndex = new FileStream(Path.Combine(path, "index.bin"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read); stageIndex.Flush(true);
            WriteOwner(id, new StageOwner { Id = id, Chain = new byte[32] }, true);
        }
        public void WriteStageBlock(Guid id, long number, byte[] contents, byte[] hash)
        {
            Ready(); if (contents == null || contents.Length < 1 || contents.Length > MaximumBlockBytes || hash == null || hash.Length != 32 || number < 0) throw new ArgumentException("blueprint-block-size");
            StageOwner owner = ReadOwner(id);
            if (owner.Root != null || owner.Blocks != number || stageWriter != id || stageIndex == null || stageIndex.Length != checked(number * 32L) || File.Exists(Path.Combine(Content(id, true), "root.bin"))) throw new InvalidDataException("blueprint-stage-writer-identity");
            string path = Block(id, true, number); Guard(path); Directory.CreateDirectory(Path.GetDirectoryName(path)); Guard(path);
            // A failed partial CreateNew is recovery material. Neither retry nor
            // cancellation may overwrite it and claim it was the original block.
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Write(contents, 0, contents.Length); stream.Flush(true); stream.Position = 0;
                if (!Equal(Hash(stream), hash)) throw new InvalidDataException("blueprint-block-readback");
            }
            stageIndex.Position = stageIndex.Length; stageIndex.Write(hash, 0, 32); stageIndex.Flush(true);
            owner.Blocks++; owner.Chain = Chain(owner.Chain, hash); WriteOwner(id, owner, false);
        }
        public byte[] ReadBlock(Guid id, bool stage, long number) { Ready(); return ReadBounded(Block(id, stage, number), MaximumBlockBytes); }
        public Stream OpenIndex(Guid id, bool stage)
        { Ready(); string path = Path.Combine(Content(id, stage), "index.bin"); Guard(path); return new FileStream(path, FileMode.Open, FileAccess.Read, stage && stageWriter == id && stageIndex != null ? FileShare.ReadWrite : FileShare.Read); }
        public void SealStage(Guid id, byte[] root)
        {
            Ready(); if (root == null || root.Length < 1 || root.Length > 65536) throw new ArgumentException("blueprint-root-size");
            StageOwner owner = ReadOwner(id); if (owner.Root != null) throw new InvalidDataException("blueprint-stage-already-sealed");
            using (Stream index = OpenIndex(id, true)) VerifyStageIndex(index, owner);
            string path = Path.Combine(Content(id, true), "root.bin"); Guard(path);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(root, 0, root.Length); stream.Flush(true); }
            if (!Equal(ReadBounded(path, 65536), root)) throw new InvalidDataException("blueprint-root-readback");
            owner.Root = Hash(root); WriteOwner(id, owner, false); if (stageWriter == id) CloseStageWriter();
        }
        public byte[] ReadRoot(Guid id, bool stage) { Ready(); return ReadBounded(Path.Combine(Content(id, stage), "root.bin"), 65536); }
        public void ValidateLayout(Guid id, bool stage, long blockCount)
        {
            Ready(); if (blockCount < 0 || blockCount > Int64.MaxValue / 32L) throw new InvalidDataException("blueprint-block-count");
            string content = Content(id, stage); Guard(content); int direct = 0;
            foreach (string entry in Directory.EnumerateFileSystemEntries(content))
            {
                Guard(entry); string name = Path.GetFileName(entry);
                if (++direct > (stage ? 4 : 3) || !(name == "root.bin" && File.Exists(entry) || name == "index.bin" && File.Exists(entry) || name == "chunks" && Directory.Exists(entry) || stage && name == ".owner.bin" && File.Exists(entry))) throw new InvalidDataException("blueprint-unknown-content-entry");
            }
            using (Stream index = OpenIndex(id, stage)) if (index.Length != blockCount * 32L) throw new InvalidDataException("blueprint-index-length");
            string chunks = Path.Combine(content, "chunks"); Guard(chunks); long found = 0;
            if (Directory.Exists(chunks)) foreach (string entry in Directory.EnumerateFileSystemEntries(chunks))
            {
                Guard(entry); long number;
                if (Directory.Exists(entry) || !BlockNumber(Path.GetFileName(entry), out number) || number >= blockCount) throw new InvalidDataException("blueprint-unknown-chunk");
                if (new FileInfo(entry).Length < 1 || new FileInfo(entry).Length > MaximumBlockBytes) throw new InvalidDataException("blueprint-chunk-length");
                found++;
            }
            if (found != blockCount) throw new InvalidDataException("blueprint-missing-chunk");
        }
        public void PromoteStage(Guid stageId, Guid assetId)
        {
            Ready(); string source = Content(stageId, true), asset = Asset(assetId); Guard(source); Guard(asset);
            StageOwner owner = ReadOwner(stageId); if (owner.Root == null) throw new InvalidDataException("blueprint-stage-not-sealed");
            ValidateCleanupNames(stageId, true);
            VerifyFile(Path.Combine(source, "root.bin"), owner.Root, false);
            using (Stream index = OpenIndex(stageId, true)) VerifyStageIndex(index, owner);
            if (Directory.Exists(asset) || File.Exists(asset)) throw new InvalidDataException("blueprint-asset-conflict");
            DeleteVerified(Path.Combine(source, ".owner.bin"), owner.OwnerIdentity, false);
            Directory.CreateDirectory(asset); Guard(asset);
            Directory.Move(source, Path.Combine(asset, "content"));
        }
        public void PrepareCleanup(Guid assetId, BlueprintCleanupProof proof)
        {
            Ready(); byte[] bytes = CleanupIntentBytes(assetId, proof); string path = Path.Combine(Asset(assetId), ".delete.bin"); Guard(path);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); stream.Position = 0; if (!Equal(Hash(stream), Hash(bytes))) throw new InvalidDataException("blueprint-cleanup-intent-readback"); }
            preparedCleanupIdentity = proof.MetadataIdentity;
        }
        public bool CleanupStep(Guid id, bool stage, long blockCount, int budget, BlueprintCleanupProof proof = null)
        {
            Ready(); if (budget < 1 || budget > 64 || blockCount < 0) throw new ArgumentOutOfRangeException(nameof(budget));
            string content = Content(id, stage), asset = Asset(id); Guard(content);
            if (stageWriter == id) CloseStageWriter();
            StageOwner owner = stage ? ReadOwner(id) : null;
            if (stage && owner.Blocks != blockCount || !stage && proof == null) throw new InvalidDataException("blueprint-cleanup-proof-required");
            byte[] intentIdentity = null;
            if (!stage)
            {
                BlueprintCleanupProof intent = ReadCleanupIntent(id);
                if (!SameProof(intent, proof)) throw new InvalidDataException("blueprint-cleanup-intent-changed");
                intentIdentity = Hash(CleanupIntentBytes(id, intent));
            }
            ValidateCleanupNames(id, stage);
            byte[] rootHash = stage ? owner.Root : IdentityBytes(proof.RootIdentity);
            string rootPath = Path.Combine(content, "root.bin"), indexPath = Path.Combine(content, "index.bin");
            VerifyFile(rootPath, rootHash, true);
            if (!stage)
            {
                VerifyFile(Path.Combine(asset, "metadata.json"), IdentityBytes(proof.MetadataIdentity), false);
                VerifyFile(Path.Combine(asset, "metadata.json.bak"), IdentityBytes(proof.SourceMetadataIdentity), true);
            }
            if (File.Exists(indexPath)) using (Stream index = OpenIndex(id, stage))
            { if (stage) VerifyStageIndex(index, owner); else if (index.Length != checked(blockCount * 32L) || !Equal(Hash(index), IdentityBytes(proof.IndexIdentity))) throw new InvalidDataException("blueprint-cleanup-index-identity"); }
            // Only a tombstone-bound index (or authenticated private-stage
            // owner) authorizes chunk deletion. Current bytes cannot self-authorize.
            string chunks = Path.Combine(content, "chunks"); Guard(chunks); int deleted = 0;
            if (Directory.Exists(chunks)) using (Stream index = OpenIndex(id, stage))
            {
                if (index.Length != checked(blockCount * 32L)) throw new InvalidDataException("blueprint-cleanup-index");
                if (stage) VerifyStageIndex(index, owner);
                else if (!Equal(Hash(index), IdentityBytes(proof.IndexIdentity))) throw new InvalidDataException("blueprint-cleanup-index-identity");
                foreach (string entry in Directory.EnumerateFileSystemEntries(chunks))
                {
                    Guard(entry); long number;
                    if (Directory.Exists(entry) || !BlockNumber(Path.GetFileName(entry), out number) || number >= blockCount) throw new InvalidDataException("blueprint-cleanup-unknown-chunk");
                    index.Position = number * 32L; byte[] expected = new byte[32]; ReadExactly(index, expected);
                    using (var stream = new FileStream(entry, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                    {
                        if (stream.Length > MaximumBlockBytes || !Equal(Hash(stream), expected)) throw new InvalidDataException("blueprint-cleanup-external-chunk");
                        File.Delete(entry);
                    }
                    if (++deleted >= budget) return false;
                }
                Directory.Delete(chunks, false);
            }
            if (Directory.Exists(content))
            {
                // Missing known files can be the tail of an interrupted cleanup;
                // any surviving replacement still has to match its trusted hash.
                DeleteVerified(rootPath, rootHash, true);
                if (File.Exists(indexPath))
                {
                    if (stage) { using (var index = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) { VerifyStageIndex(index, owner); File.Delete(indexPath); } }
                    else DeleteVerified(indexPath, IdentityBytes(proof.IndexIdentity), true, Int64.MaxValue);
                }
                if (stage) DeleteVerified(Path.Combine(content, ".owner.bin"), owner.OwnerIdentity, false);
                Directory.Delete(content, false);
            }
            if (stage) return true;
            CloseMetadata(); Guard(asset);
            ValidateCleanupNames(id, false);
            DeleteVerified(Path.Combine(asset, "metadata.json.bak"), IdentityBytes(proof.SourceMetadataIdentity), true);
            DeleteVerified(Path.Combine(asset, "metadata.json"), IdentityBytes(proof.MetadataIdentity), false);
            string lockPath = Path.Combine(asset, "metadata.json.lock"); Guard(lockPath);
            if (File.Exists(lockPath)) { using (var stream = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) { if (stream.Length != 0) throw new InvalidDataException("blueprint-cleanup-external-lock"); File.Delete(lockPath); } }
            DeleteVerified(Path.Combine(asset, ".delete.bin"), intentIdentity, false);
            Directory.Delete(asset, false); return true;
        }
        public void Dispose() { if (disposed) return; CloseStageWriter(); CloseMetadata(); if (lease != null) lease.Dispose(); lease = null; Array.Clear(stageKey, 0, stageKey.Length); disposed = true; }
        private sealed class StageOwner
        {
            internal Guid Id;
            internal long Blocks;
            internal byte[] Root, Chain, OwnerIdentity;
        }
        // Private stages can be numerous external-sort runs. A fixed signed
        // record authenticates each exact owner's hashes without retaining an
        // unbounded dictionary. A new process cannot adopt old private stages.
        private StageOwner ReadOwner(Guid id)
        {
            byte[] bytes = ReadBounded(Path.Combine(Content(id, true), ".owner.bin"), 256);
            if (bytes.Length != 129) throw new InvalidDataException("blueprint-stage-owner-format");
            var body = new byte[97]; var signature = new byte[32]; Array.Copy(bytes, body, body.Length); Array.Copy(bytes, body.Length, signature, 0, signature.Length);
            using (var mac = new HMACSHA256(stageKey)) if (!Equal(mac.ComputeHash(body), signature)) throw new InvalidDataException("blueprint-stage-owner-identity");
            using (var stream = new MemoryStream(body, false)) using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != 0x4F424D4A || reader.ReadInt32() != 1) throw new InvalidDataException("blueprint-stage-owner-version");
                var result = new StageOwner { Id = new Guid(reader.ReadBytes(16)), Blocks = reader.ReadInt64() };
                byte hasRoot = reader.ReadByte(); byte[] root = reader.ReadBytes(32); result.Chain = reader.ReadBytes(32);
                if (result.Id != id || result.Blocks < 0 || result.Blocks > Int64.MaxValue / 32L || hasRoot > 1 || hasRoot == 0 && !Equal(root, new byte[32])) throw new InvalidDataException("blueprint-stage-owner-fields");
                result.Root = hasRoot == 0 ? null : root; result.OwnerIdentity = Hash(bytes); return result;
            }
        }
        private void WriteOwner(Guid id, StageOwner owner, bool create)
        {
            byte[] bytes;
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(0x4F424D4A); writer.Write(1); writer.Write(id.ToByteArray()); writer.Write(owner.Blocks); writer.Write((byte)(owner.Root == null ? 0 : 1)); writer.Write(owner.Root ?? new byte[32]); writer.Write(owner.Chain); writer.Flush();
                byte[] body = stream.ToArray(); using (var mac = new HMACSHA256(stageKey)) { writer.Write(mac.ComputeHash(body)); writer.Flush(); bytes = stream.ToArray(); }
            }
            string path = Path.Combine(Content(id, true), ".owner.bin"); Guard(path);
            using (var target = new FileStream(path, create ? FileMode.CreateNew : FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                if (!create)
                {
                    if (target.Length != 129) throw new InvalidDataException("blueprint-stage-owner-changed");
                    var current = new byte[129]; ReadExactly(target, current); if (!Equal(Hash(current), owner.OwnerIdentity)) throw new InvalidDataException("blueprint-stage-owner-changed");
                }
                target.Position = 0; target.Write(bytes, 0, bytes.Length); target.SetLength(bytes.Length); target.Flush(true);
            }
            owner.OwnerIdentity = Hash(bytes);
        }
        private void VerifyStageIndex(Stream index, StageOwner owner)
        {
            if (index.Length != checked(owner.Blocks * 32L)) throw new InvalidDataException("blueprint-stage-index-length");
            index.Position = 0; byte[] chain = new byte[32], hash = new byte[32];
            for (long number = 0; number < owner.Blocks; number++) { ReadExactly(index, hash); chain = Chain(chain, hash); }
            if (!Equal(chain, owner.Chain)) throw new InvalidDataException("blueprint-stage-index-identity");
        }
        private void ValidateCleanupNames(Guid id, bool stage)
        {
            string content = Content(id, stage); Guard(content);
            if (!stage)
            {
                string asset = Asset(id); Guard(asset);
                if (Directory.Exists(asset)) foreach (string entry in Directory.EnumerateFileSystemEntries(asset))
                {
                    Guard(entry); string name = Path.GetFileName(entry);
                    if (name == "content" && Directory.Exists(entry)) continue;
                    if (Directory.Exists(entry) || name != "metadata.json" && name != "metadata.json.bak" && name != "metadata.json.lock" && name != ".delete.bin") throw new InvalidDataException("blueprint-cleanup-unknown-asset");
                }
            }
            if (Directory.Exists(content)) foreach (string entry in Directory.EnumerateFileSystemEntries(content))
            {
                Guard(entry); string name = Path.GetFileName(entry);
                if (name == "chunks" && Directory.Exists(entry)) continue;
                if (Directory.Exists(entry) || name != "root.bin" && name != "index.bin" && !(stage && name == ".owner.bin")) throw new InvalidDataException("blueprint-cleanup-unknown-content");
            }
        }
        private static void VerifyFile(string path, byte[] expected, bool allowMissing)
        {
            Guard(path); if (Directory.Exists(path)) throw new InvalidDataException("blueprint-cleanup-file-is-directory");
            if (!File.Exists(path)) { if (allowMissing) return; throw new InvalidDataException("blueprint-cleanup-missing-file"); }
            if (expected == null) throw new InvalidDataException("blueprint-cleanup-unowned-file");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                if (stream.Length > 65536 || !Equal(Hash(stream), expected)) throw new InvalidDataException("blueprint-cleanup-external-file");
        }
        private static void DeleteVerified(string path, byte[] expected, bool allowMissing, long maximum = 65536)
        {
            Guard(path); if (Directory.Exists(path)) throw new InvalidDataException("blueprint-cleanup-file-is-directory");
            if (!File.Exists(path)) { if (allowMissing) return; throw new InvalidDataException("blueprint-cleanup-missing-file"); }
            if (expected == null) throw new InvalidDataException("blueprint-cleanup-unowned-file");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            { if (stream.Length > maximum || !Equal(Hash(stream), expected)) throw new InvalidDataException("blueprint-cleanup-external-file"); File.Delete(path); }
        }
        private static byte[] IdentityBytes(string identity)
        { try { byte[] hash = Convert.FromBase64String(identity ?? ""); if (hash.Length != 32) throw new InvalidDataException("blueprint-cleanup-invalid-identity"); return hash; } catch (FormatException) { throw new InvalidDataException("blueprint-cleanup-invalid-identity"); } }
        private static byte[] NewStageKey() { var key = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(key); return key; }
        private static byte[] CleanupIntentBytes(Guid id, BlueprintCleanupProof proof)
        {
            if (proof == null) throw new InvalidDataException("blueprint-cleanup-proof-required");
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            { writer.Write(0x44424D4A); writer.Write(1); writer.Write(id.ToByteArray()); writer.Write(IdentityBytes(proof.MetadataIdentity)); writer.Write(IdentityBytes(proof.SourceMetadataIdentity)); writer.Write(IdentityBytes(proof.RootIdentity)); writer.Write(IdentityBytes(proof.IndexIdentity)); writer.Flush(); return stream.ToArray(); }
        }
        private BlueprintCleanupProof ReadCleanupIntent(Guid id)
        {
            byte[] bytes = ReadBounded(Path.Combine(Asset(id), ".delete.bin"), 256);
            if (bytes.Length != 152) throw new InvalidDataException("blueprint-cleanup-intent-format");
            using (var stream = new MemoryStream(bytes, false)) using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != 0x44424D4A || reader.ReadInt32() != 1 || new Guid(reader.ReadBytes(16)) != id) throw new InvalidDataException("blueprint-cleanup-intent-version");
                return new BlueprintCleanupProof(Convert.ToBase64String(reader.ReadBytes(32)), Convert.ToBase64String(reader.ReadBytes(32)), Convert.ToBase64String(reader.ReadBytes(32)), Convert.ToBase64String(reader.ReadBytes(32)));
            }
        }
        private static bool SameProof(BlueprintCleanupProof a, BlueprintCleanupProof b)
        { return a.MetadataIdentity == b.MetadataIdentity && a.SourceMetadataIdentity == b.SourceMetadataIdentity && a.RootIdentity == b.RootIdentity && a.IndexIdentity == b.IndexIdentity; }
        private static byte[] Chain(byte[] previous, byte[] hash) { var bytes = new byte[64]; Array.Copy(previous, bytes, 32); Array.Copy(hash, 0, bytes, 32, 32); return Hash(bytes); }
        private static byte[] Hash(byte[] bytes) { using (var hash = SHA256.Create()) return hash.ComputeHash(bytes); }
        private void CloseStageWriter() { if (stageIndex != null) stageIndex.Dispose(); stageIndex = null; stageWriter = Guid.Empty; }
        private string Asset(Guid id) { if (id == Guid.Empty) throw new ArgumentException("blueprint-empty-id"); return Path.Combine(directory, id.ToString("N")); }
        private string Content(Guid id, bool stage) { if (id == Guid.Empty) throw new ArgumentException("blueprint-empty-id"); return stage ? Path.Combine(directory, ".staging", id.ToString("N")) : Path.Combine(Asset(id), "content"); }
        private string Block(Guid id, bool stage, long number) { if (number < 0) throw new ArgumentOutOfRangeException(nameof(number)); return Path.Combine(Content(id, stage), "chunks", number.ToString("D20", CultureInfo.InvariantCulture) + ".bin"); }
        private static bool BlockNumber(string name, out long number) { number = -1; return name.Length == 24 && name.EndsWith(".bin", StringComparison.Ordinal) && Int64.TryParse(name.Substring(0, 20), NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 0 && name == number.ToString("D20", CultureInfo.InvariantCulture) + ".bin"; }
        private static byte[] ReadBounded(string path, int maximum) { Guard(path); using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) { if (stream.Length < 1 || stream.Length > maximum) throw new InvalidDataException("blueprint-file-size"); byte[] bytes = new byte[(int)stream.Length]; ReadExactly(stream, bytes); if (stream.ReadByte() != -1) throw new InvalidDataException("blueprint-file-grew"); return bytes; } }
        private static void ReadExactly(Stream stream, byte[] bytes) { int at = 0; while (at != bytes.Length) { int n = stream.Read(bytes, at, bytes.Length - at); if (n == 0) throw new EndOfStreamException(); at += n; } }
        private static byte[] Hash(Stream stream) { using (var hash = SHA256.Create()) return hash.ComputeHash(stream); }
        private static bool Equal(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static void DeleteFile(string path) { Guard(path); if (Directory.Exists(path)) throw new InvalidDataException("blueprint-cleanup-directory"); File.Delete(path); }
        private static void GuardDocument(string path) { Guard(path); Guard(path + ".lock"); Guard(path + ".tmp"); Guard(path + ".bak"); }
        private static void Guard(string path)
        {
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                try { var attributes = File.GetAttributes(current); if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0) throw new InvalidDataException("blueprint-linked-path-protected"); }
                catch (FileNotFoundException) { } catch (DirectoryNotFoundException) { }
                string parent = Path.GetDirectoryName(current); if (parent == current) break; current = parent;
            }
        }
        private void Alive() { if (disposed) throw new ObjectDisposedException(nameof(FileBlueprintLibrary)); }
        private void Ready() { Alive(); if (lease == null) throw new InvalidOperationException("blueprint-library-not-open"); }
    }
}
