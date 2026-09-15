using System;
using System.IO;
using System.Security.Cryptography;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.DeathHistory;
using JueMingR.Platform.Settings;

namespace JueMingR.Infrastructure.DeathHistory
{
    public sealed class FileDeathArchive : IDeathArchiveFiles
    {
        private const int MaximumPageBytes = 1048576;
        private readonly string pages;
        private readonly AtomicFileDocument root;
        private bool ready;
        private PreferenceReadResult initial;
        public FileDeathArchive(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory) || Path.GetFullPath(directory) != directory.TrimEnd(Path.DirectorySeparatorChar)) throw new ArgumentException("absolute-death-directory-required");
            pages = Path.Combine(directory, "pages"); root = new AtomicFileDocument(Path.Combine(directory, "root.json"), 65536, true);
        }
        public PreferenceReadResult ReadRoot()
        {
            if (initial != null) return initial;
            initial = root.Read();
            if (initial.Status == PreferenceReadStatus.Missing && Directory.Exists(pages))
            {
                // Missing root with remaining immutable data may be an
                // interrupted commit. It is never a reliable new empty pair.
                using (var entries = Directory.EnumerateFileSystemEntries(pages).GetEnumerator())
                    if (entries.MoveNext()) initial = new PreferenceReadResult(PreferenceReadStatus.IoFailure, null, null, "death-recovery-without-root");
            }
            ready = initial.Status == PreferenceReadStatus.Loaded || initial.Status == PreferenceReadStatus.Missing; return initial;
        }
        public PreferenceWriteResult WriteRoot(string expectedIdentity, byte[] bytes)
        { if (!ready) throw new InvalidOperationException("death-archive-not-leased"); return root.Write(expectedIdentity, bytes); }
        public byte[] ReadPage(string id)
        {
            if (!ready) throw new InvalidOperationException("death-archive-not-leased"); string path = PagePath(id);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long length = stream.Length; if (length < 1 || length > MaximumPageBytes) throw new InvalidDataException("death-page-size");
                var bytes = new byte[(int)length]; int read = 0;
                while (read < bytes.Length) { int n = stream.Read(bytes, read, bytes.Length - read); if (n == 0) throw new EndOfStreamException(); read += n; }
                if (Hash(bytes) != id) throw new InvalidDataException("death-page-hash"); return bytes;
            }
        }
        public string CreatePage(byte[] bytes)
        {
            if (!ready || bytes == null || bytes.Length < 1 || bytes.Length > MaximumPageBytes) throw new ArgumentException("invalid-death-page");
            string id = Hash(bytes), path = PagePath(id);
            // Content addresses make a known precommit retry reuse complete
            // bytes. Existing bytes are verified, never overwritten or adopted
            // merely because a filename happens to match.
            if (File.Exists(path)) { ReadPage(id); return id; }
            string directory = Path.GetDirectoryName(path); Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            // A failure retains our incomplete/complete temporary material.
            // The root has not been entered, so the domain may retry finitely.
            File.Move(temporary, path); ReadPage(id); return id;
        }
        private string PagePath(string id)
        {
            if (id == null || id.Length != 64) throw new ArgumentException("invalid-death-page-id");
            foreach (char c in id) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) throw new ArgumentException("invalid-death-page-id");
            // Keep all 256 hash bits but avoid spelling them twice as long hex
            // paths. The same-directory temporary name is independently short;
            // the native .NET host may still apply Windows MAX_PATH rules.
            var bytes = new byte[32]; for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(id.Substring(i * 2, 2), 16);
            string name = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return Path.Combine(pages, id.Substring(0, 2), name + ".bin");
        }
        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        public void Dispose() { ready = false; root.Dispose(); }
    }
}
