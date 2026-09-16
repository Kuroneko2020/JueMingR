using System;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Footprints;
using JueMingR.Platform.Settings;

namespace JueMingR.Infrastructure.Footprints
{
    // Mechanical files only. The pair lease stays held across generation changes.
    // No recursive delete, parent deletion, recovery promotion, or arbitrary paths.
    public sealed class FileFootprintArchive : IFootprintArchiveFiles
    {
        private const int MaximumBytes = 16384;
        private readonly string pairDirectory;
        private AtomicFileDocument catalog, root;
        private string generationDirectory;
        private bool disposed;
        private IEnumerator<string> blockNames;
        private long namesFound;
        private bool namesStarted, namesComplete;
        public FileFootprintArchive(string fullFootprintsDirectory, string pair)
        {
            if (String.IsNullOrWhiteSpace(fullFootprintsDirectory) || !Path.IsPathRooted(fullFootprintsDirectory) || pair == null || pair.Length != 64) throw new ArgumentException("footprint-path-identity");
            foreach (char c in pair) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) throw new ArgumentException("footprint-pair");
            string absolute = Path.GetFullPath(fullFootprintsDirectory);
            if (!String.Equals(Path.GetPathRoot(absolute), Path.GetPathRoot(fullFootprintsDirectory), StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("footprint-absolute-root");
            pairDirectory = Path.Combine(absolute, pair);
        }
        public long BlockReads { get; private set; }
        public long BlockBytesWritten { get; private set; }
        public long DeletedFiles { get; private set; }
        public PreferenceReadResult ReadCatalog()
        {
            Alive(); if (catalog != null) return catalog.Read(); Guard(pairDirectory);
            string path = Path.Combine(pairDirectory, "catalog.json");
            if (!File.Exists(path) && Directory.Exists(pairDirectory))
                foreach (string entry in Directory.EnumerateFileSystemEntries(pairDirectory))
                    if (Path.GetFileName(entry) != "catalog.json.lock") throw new InvalidDataException("footprint-missing-catalog-with-material");
            GuardDocument(path); catalog = new AtomicFileDocument(path, MaximumBytes, true); return catalog.Read();
        }
        public PreferenceWriteResult WriteCatalog(string identity, byte[] bytes)
        { Alive(); GuardDocument(Path.Combine(pairDirectory, "catalog.json")); return catalog.Write(identity, bytes); }
        public PreferenceReadResult OpenRoot(string generation)
        {
            Alive(); if (root != null) root.Dispose(); root = null;
            generationDirectory = GenerationPath(generation); Guard(generationDirectory);
            string path = Path.Combine(generationDirectory, "root.bin"); GuardDocument(path);
            root = new AtomicFileDocument(path, MaximumBytes, true); return root.Read();
        }
        public PreferenceWriteResult WriteRoot(string identity, byte[] bytes)
        { Alive(); GuardDocument(Path.Combine(generationDirectory, "root.bin")); return root.Write(identity, bytes); }
        public byte[] ReadBlock(long number)
        {
            Alive(); string path = BlockPath(number); Guard(path); BlockReads++;
            return ReadBounded(path);
        }
        public void CreateBlock(long number, byte[] bytes)
        {
            Alive(); if (bytes == null || bytes.Length > MaximumBytes) throw new InvalidDataException("footprint-block-size");
            string path = BlockPath(number); Guard(path); Directory.CreateDirectory(Path.GetDirectoryName(path)); Guard(path);
            if (File.Exists(path))
            {
                byte[] existing = ReadBounded(path); if (existing.Length != bytes.Length) throw new InvalidDataException("footprint-block-conflict");
                for (int i = 0; i < bytes.Length; i++) if (existing[i] != bytes[i]) throw new InvalidDataException("footprint-block-conflict");
                return; // Exact idempotence after a known pre-root-commit retry only.
            }
            // A partial CreateNew remains visible as protected evidence. Never
            // overwrite it on retry or treat it as a committed historical block.
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            BlockBytesWritten += bytes.Length;
        }
        public bool ValidateBlockNamesStep(long blockCount, int budget)
        {
            Alive(); if (budget < 1 || budget > 256) throw new ArgumentOutOfRangeException(nameof(budget));
            if (namesComplete) return true;
            Guard(generationDirectory); string directory = Path.Combine(generationDirectory, "blocks");
            if (!namesStarted)
            {
                // Only five known entries can occur directly in a generation.
                int direct = 0;
                foreach (string entry in Directory.EnumerateFileSystemEntries(generationDirectory))
                {
                    Guard(entry); string name = Path.GetFileName(entry); if (++direct > 5) throw new InvalidDataException("footprint-unknown-generation-entry");
                    if (name == "blocks" && Directory.Exists(entry)) continue;
                    if (!RootName(name) || Directory.Exists(entry)) throw new InvalidDataException("footprint-unknown-generation-entry");
                }
                namesStarted = true;
                if (Directory.Exists(directory)) blockNames = Directory.EnumerateFileSystemEntries(directory).GetEnumerator();
            }
            if (blockNames != null)
                for (int i = 0; i < budget; i++)
                {
                    if (!blockNames.MoveNext()) { blockNames.Dispose(); blockNames = null; break; }
                    string entry = blockNames.Current;
                    Guard(entry); long number;
                    if (Directory.Exists(entry) || !BlockNumber(Path.GetFileName(entry), out number) || number >= blockCount) throw new InvalidDataException("footprint-uncommitted-or-unknown-block");
                    namesFound++;
                }
            if (blockNames != null) return false;
            if (namesFound != blockCount) throw new InvalidDataException("footprint-missing-block");
            namesComplete = true; return true;
        }
        public bool DeleteGenerationStep(string generation, int budget)
        {
            Alive(); if (budget < 1 || budget > 256) throw new ArgumentOutOfRangeException(nameof(budget));
            string target = GenerationPath(generation); Guard(target);
            if (root != null) { root.Dispose(); root = null; }
            if (!Directory.Exists(target)) { if (File.Exists(target)) throw new InvalidDataException("footprint-generation-is-file"); return true; }
            int deleted = 0; string blocksPath = Path.Combine(target, "blocks"); Guard(blocksPath);
            if (File.Exists(blocksPath)) throw new InvalidDataException("footprint-blocks-is-file");
            if (Directory.Exists(blocksPath))
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(blocksPath))
                {
                    Guard(entry); long number;
                    if (Directory.Exists(entry) || !BlockNumber(Path.GetFileName(entry), out number)) throw new InvalidDataException("footprint-clear-unknown-block");
                    File.Delete(entry); DeletedFiles++; if (++deleted >= budget) return false;
                }
                Directory.Delete(blocksPath, false);
            }
            foreach (string entry in Directory.EnumerateFileSystemEntries(target))
            {
                Guard(entry); if (Directory.Exists(entry) || !RootName(Path.GetFileName(entry))) throw new InvalidDataException("footprint-clear-unknown-entry");
                File.Delete(entry); DeletedFiles++; if (++deleted >= budget) return false;
            }
            Directory.Delete(target, false); return true;
        }
        public void Dispose() { if (disposed) return; disposed = true; if (blockNames != null) blockNames.Dispose(); if (root != null) root.Dispose(); if (catalog != null) catalog.Dispose(); }
        private string GenerationPath(string generation)
        {
            Guid value; if (generation == null || generation.Length != 32 || !Guid.TryParseExact(generation, "N", out value) || value.ToString("N") != generation) throw new ArgumentException("footprint-generation");
            return Path.Combine(pairDirectory, generation);
        }
        private string BlockPath(long number) { if (generationDirectory == null || number < 0) throw new InvalidOperationException("footprint-block-target"); return Path.Combine(generationDirectory, "blocks", number.ToString("D20", CultureInfo.InvariantCulture) + ".bin"); }
        private static bool BlockNumber(string name, out long number)
        { number = -1; return name.Length == 24 && name.EndsWith(".bin", StringComparison.Ordinal) && Int64.TryParse(name.Substring(0, 20), NumberStyles.None, CultureInfo.InvariantCulture, out number) && number >= 0 && name == number.ToString("D20", CultureInfo.InvariantCulture) + ".bin"; }
        private static bool RootName(string name) { return name == "root.bin" || name == "root.bin.bak" || name == "root.bin.tmp" || name == "root.bin.lock"; }
        private static byte[] ReadBounded(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < 1 || stream.Length > MaximumBytes) throw new InvalidDataException("footprint-file-size");
                var bytes = new byte[(int)stream.Length]; int at = 0; while (at < bytes.Length) { int got = stream.Read(bytes, at, bytes.Length - at); if (got == 0) throw new EndOfStreamException(); at += got; } return bytes;
            }
        }
        private static void GuardDocument(string path) { Guard(path); Guard(path + ".bak"); Guard(path + ".tmp"); Guard(path + ".lock"); }
        private static void Guard(string path)
        {
            // Check every existing ancestor without following reparse points. The
            // install data-root contract excludes cooperating concurrent path swaps.
            string current = Path.GetFullPath(path);
            while (!String.IsNullOrEmpty(current))
            {
                try { var attributes = File.GetAttributes(current); if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0) throw new InvalidDataException("footprint-linked-path-protected"); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                string parent = Path.GetDirectoryName(current); if (parent == current) break; current = parent;
            }
        }
        private void Alive() { if (disposed) throw new ObjectDisposedException(nameof(FileFootprintArchive)); }
    }
}
