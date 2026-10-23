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
            metadata = new AtomicFileDocument(path, 65536, true); return metadata.Read();
        }
        public PreferenceWriteResult WriteMetadata(string expectedIdentity, byte[] contents)
        { Ready(); if (metadata == null) throw new InvalidOperationException("blueprint-metadata-not-open"); return metadata.Write(expectedIdentity, contents); }
        public void CloseMetadata() { if (metadata != null) metadata.Dispose(); metadata = null; }
        public void BeginStage(Guid id)
        {
            Ready(); string path = Content(id, true); Guard(path);
            if (Directory.Exists(path) || File.Exists(path) || Directory.Exists(Asset(id)) || File.Exists(Asset(id))) throw new InvalidDataException("blueprint-identity-already-exists");
            Directory.CreateDirectory(path); Guard(path);
            using (var stream = new FileStream(Path.Combine(path, "index.bin"), FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.Flush(true);
        }
        public void WriteStageBlock(Guid id, long number, byte[] contents, byte[] hash)
        {
            Ready(); if (contents == null || contents.Length < 1 || contents.Length > MaximumBlockBytes || hash == null || hash.Length != 32 || number < 0) throw new ArgumentException("blueprint-block-size");
            string path = Block(id, true, number); Guard(path); Directory.CreateDirectory(Path.GetDirectoryName(path)); Guard(path);
            // A failed partial CreateNew is recovery material. Neither retry nor
            // cancellation may overwrite it and claim it was the original block.
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                stream.Write(contents, 0, contents.Length); stream.Flush(true); stream.Position = 0;
                if (!Equal(Hash(stream), hash)) throw new InvalidDataException("blueprint-block-readback");
            }
            string index = Path.Combine(Content(id, true), "index.bin"); Guard(index);
            using (var stream = new FileStream(index, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                if (stream.Length != checked(number * 32L)) throw new InvalidDataException("blueprint-index-position");
                stream.Position = stream.Length; stream.Write(hash, 0, 32); stream.Flush(true);
            }
        }
        public byte[] ReadBlock(Guid id, bool stage, long number) { Ready(); return ReadBounded(Block(id, stage, number), MaximumBlockBytes); }
        public Stream OpenIndex(Guid id, bool stage)
        { Ready(); string path = Path.Combine(Content(id, stage), "index.bin"); Guard(path); return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); }
        public void SealStage(Guid id, byte[] root)
        {
            Ready(); if (root == null || root.Length < 1 || root.Length > 65536) throw new ArgumentException("blueprint-root-size");
            string path = Path.Combine(Content(id, true), "root.bin"); Guard(path);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(root, 0, root.Length); stream.Flush(true); }
            if (!Equal(ReadBounded(path, 65536), root)) throw new InvalidDataException("blueprint-root-readback");
        }
        public byte[] ReadRoot(Guid id, bool stage) { Ready(); return ReadBounded(Path.Combine(Content(id, stage), "root.bin"), 65536); }
        public void ValidateLayout(Guid id, bool stage, long blockCount)
        {
            Ready(); if (blockCount < 0 || blockCount > Int64.MaxValue / 32L) throw new InvalidDataException("blueprint-block-count");
            string content = Content(id, stage); Guard(content); int direct = 0;
            foreach (string entry in Directory.EnumerateFileSystemEntries(content))
            {
                Guard(entry); string name = Path.GetFileName(entry);
                if (++direct > 3 || !(name == "root.bin" && File.Exists(entry) || name == "index.bin" && File.Exists(entry) || name == "chunks" && Directory.Exists(entry))) throw new InvalidDataException("blueprint-unknown-content-entry");
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
            if (Directory.Exists(asset) || File.Exists(asset)) throw new InvalidDataException("blueprint-asset-conflict");
            Directory.CreateDirectory(asset); Guard(asset);
            Directory.Move(source, Path.Combine(asset, "content"));
        }
        public bool CleanupStep(Guid id, bool stage, long blockCount, int budget)
        {
            Ready(); if (budget < 1 || budget > 64 || blockCount < 0) throw new ArgumentOutOfRangeException(nameof(budget));
            string content = Content(id, stage), asset = Asset(id); Guard(content);
            // The retained index is the exact owned-file/hash list. Deleting a
            // block first checks these bytes, so an external replacement is kept.
            string chunks = Path.Combine(content, "chunks"); Guard(chunks); int deleted = 0;
            if (Directory.Exists(chunks)) using (Stream index = OpenIndex(id, stage))
            {
                if (index.Length != checked(blockCount * 32L)) throw new InvalidDataException("blueprint-cleanup-index");
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
                foreach (string entry in Directory.EnumerateFileSystemEntries(content))
                { Guard(entry); string name = Path.GetFileName(entry); if (Directory.Exists(entry) || name != "root.bin" && name != "index.bin") throw new InvalidDataException("blueprint-cleanup-unknown-content"); }
                // root.bin may be absent for a cancelled, not-yet-sealed stage.
                DeleteFile(Path.Combine(content, "root.bin")); DeleteFile(Path.Combine(content, "index.bin")); Directory.Delete(content, false);
            }
            if (stage) return true;
            CloseMetadata(); Guard(asset);
            foreach (string entry in Directory.EnumerateFileSystemEntries(asset))
            {
                Guard(entry); string name = Path.GetFileName(entry);
                if (Directory.Exists(entry) || name != "metadata.json" && name != "metadata.json.bak" && name != "metadata.json.lock") throw new InvalidDataException("blueprint-cleanup-unknown-asset");
            }
            DeleteFile(Path.Combine(asset, "metadata.json.bak")); DeleteFile(Path.Combine(asset, "metadata.json")); DeleteFile(Path.Combine(asset, "metadata.json.lock")); Directory.Delete(asset, false); return true;
        }
        public void Dispose() { if (disposed) return; CloseMetadata(); if (lease != null) lease.Dispose(); lease = null; disposed = true; }
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
