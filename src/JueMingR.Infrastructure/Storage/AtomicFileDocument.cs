using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using JueMingR.Platform.Settings;

namespace JueMingR.Infrastructure.Storage
{
    public sealed class AtomicFileDocument : IPreferenceStorage
    {
        private readonly int MaximumBytes;
        private readonly bool protectRecovery;
        private const string MissingIdentity = "missing";
        private readonly string path;
        private FileStream writerLease;
        private PreferenceReadResult initialRead;
        private string acceptedIdentity;
        private PreferenceWriteStatus? protection;
        private string protectionError;
        private bool unconfirmed;
        private bool disposed;
        private readonly string retainedSourcePath;
        private bool retainSource;

        public AtomicFileDocument(string fullDocumentPath, int maximumBytes, bool protectRecovery = false)
            : this(fullDocumentPath, maximumBytes, protectRecovery, null) { }
        public AtomicFileDocument(string fullDocumentPath, int maximumBytes, bool protectRecovery, string retainedSourceSuffix)
        {
            if (String.IsNullOrWhiteSpace(fullDocumentPath) || !Path.IsPathRooted(fullDocumentPath))
                throw new ArgumentException("An explicit absolute document path is required.", nameof(fullDocumentPath));
            if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            MaximumBytes = maximumBytes; this.protectRecovery = protectRecovery;
            path = Path.GetFullPath(fullDocumentPath);
            if (retainedSourceSuffix != null)
            {
                if (!protectRecovery || retainedSourceSuffix.Length < 2 || retainedSourceSuffix[0] != '.' ||
                    retainedSourceSuffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    String.Equals(retainedSourceSuffix, ".bak", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(retainedSourceSuffix, ".tmp", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(retainedSourceSuffix, ".lock", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("invalid-retained-source-suffix");
                retainedSourcePath = path + retainedSourceSuffix;
            }
            // IsPathRooted also accepts C:file and \file on Windows; those silently
            // depend on a working directory/drive and are not host-resolved data roots.
            if (!String.Equals(Path.GetPathRoot(fullDocumentPath), Path.GetPathRoot(path), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A fully qualified document path is required.", nameof(fullDocumentPath));
        }

        public PreferenceReadResult Read()
        {
            if (disposed) return ReadFailure(PreferenceReadStatus.IoFailure, "storage-disposed");
            if (initialRead != null) return initialRead;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // The stable lock file is never deleted: deleting it after releasing the
                // handle would race another process acquiring the same document lease.
                writerLease = new FileStream(path + ".lock", FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception)
            {
                bool busy = IsSharingViolation(exception);
                return RememberFailure(busy ? PreferenceReadStatus.Busy : PreferenceReadStatus.IoFailure,
                    busy ? PreferenceWriteStatus.Busy : PreferenceWriteStatus.IoFailure,
                    busy ? "another-writer" : "data-root-io-failure");
            }
            catch (UnauthorizedAccessException) { return RememberIoFailure("data-root-access-denied"); }
            catch (SecurityException) { return RememberIoFailure("data-root-access-denied"); }

            try
            {
                using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] bytes = ReadBounded(source);
                    if (bytes == null)
                        return RememberFailure(PreferenceReadStatus.TooLarge, PreferenceWriteStatus.IoFailure,
                            "document-too-large");
                    acceptedIdentity = Identity(bytes);
                    initialRead = new PreferenceReadResult(PreferenceReadStatus.Loaded, bytes, acceptedIdentity, null);
                }
            }
            catch (FileNotFoundException)
            {
                if (protectRecovery && (File.Exists(path + ".bak") || File.Exists(path + ".tmp") || Directory.Exists(path + ".bak") || Directory.Exists(path + ".tmp") ||
                    retainedSourcePath != null && (File.Exists(retainedSourcePath) || Directory.Exists(retainedSourcePath))))
                    return RememberIoFailure("missing-document-with-recovery-material");
                acceptedIdentity = MissingIdentity;
                initialRead = new PreferenceReadResult(PreferenceReadStatus.Missing, null, acceptedIdentity, null);
            }
            catch (IOException) { return RememberIoFailure("document-read-io-failure"); }
            catch (UnauthorizedAccessException) { return RememberIoFailure("document-read-access-denied"); }
            catch (SecurityException) { return RememberIoFailure("document-read-access-denied"); }
            return initialRead;
        }

        // Called on the document worker only after the semantic decoder has
        // validated the entire known older format. The adapter never parses JSON.
        public void RetainLoadedSource()
        {
            if (retainedSourcePath == null || initialRead == null || initialRead.Status != PreferenceReadStatus.Loaded)
                throw new InvalidOperationException("retained-source-requires-validated-load");
            retainSource = true;
        }

        public PreferenceWriteResult Write(string expectedIdentity, byte[] contents)
        {
            if (disposed) return Failure(PreferenceWriteStatus.IoFailure, "storage-disposed");
            if (protection.HasValue) return new PreferenceWriteResult(protection.Value, null, protectionError, unconfirmed, true);
            if (initialRead == null || !String.Equals(expectedIdentity, acceptedIdentity, StringComparison.Ordinal))
                return Protect(PreferenceWriteStatus.Conflict, "unexpected-source-identity");
            if (contents == null || contents.Length > MaximumBytes)
                return Protect(PreferenceWriteStatus.IoFailure, "invalid-candidate-size");

            // This adapter has no JSON knowledge. Only a successful semantic load (or
            // Missing) may reach here; corrupt/future/unknown-field documents stay read-only.
            byte[] candidate = (byte[])contents.Clone();
            string candidateIdentity = Identity(candidate);
            string temporaryPath = path + ".tmp";
            bool ownsTemporary = false;
            bool commitAttempted = false;
            try
            {
                using (var temporary = new FileStream(temporaryPath, FileMode.CreateNew,
                    FileAccess.ReadWrite, FileShare.None))
                {
                    ownsTemporary = true;
                    temporary.Write(candidate, 0, candidate.Length);
                    temporary.Flush(true);
                    temporary.Position = 0;
                    if (!String.Equals(Identity(ReadBounded(temporary)), candidateIdentity, StringComparison.Ordinal))
                        return Protect(PreferenceWriteStatus.IoFailure, "candidate-verification-failed");
                }

                if (acceptedIdentity == MissingIdentity)
                {
                    // Move is the first-create commit point and never overwrites an existing
                    // destination, including a file appearing after the initial Missing read.
                    commitAttempted = true;
                    try { File.Move(temporaryPath, path); }
                    catch (IOException)
                    {
                        if (File.Exists(path) || Directory.Exists(path))
                            return Protect(PreferenceWriteStatus.Conflict, "first-create-conflict");
                        throw;
                    }
                }
                else
                {
                    using (var source = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.Read | FileShare.Delete))
                    {
                        byte[] current = ReadBounded(source);
                        if (current == null || !String.Equals(Identity(current), expectedIdentity, StringComparison.Ordinal))
                            return Protect(PreferenceWriteStatus.Conflict, "external-document-change");
                        if (retainSource && !RetainOriginal(current, expectedIdentity))
                            return Protect(PreferenceWriteStatus.Conflict, "retained-source-conflict");

                        // This handle forbids ordinary in-place writers through Replace.
                        // Delete sharing is required for our replacement; it also means an
                        // uncooperative external atomic rename is outside this exclusion.
                        // Replace preserves the actual displaced bytes, then we verify them.
                        commitAttempted = true;
                        File.Replace(temporaryPath, path, path + ".bak");
                    }
                    using (var backup = new FileStream(path + ".bak", FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        byte[] previous = ReadBounded(backup);
                        if (previous == null || !String.Equals(Identity(previous), expectedIdentity, StringComparison.Ordinal))
                            return Protect(PreferenceWriteStatus.Conflict, "replace-race-backup-preserved", true);
                    }
                }

                using (var committed = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] actual = ReadBounded(committed);
                    if (actual == null || !String.Equals(Identity(actual), candidateIdentity, StringComparison.Ordinal))
                        return Protect(PreferenceWriteStatus.Conflict, "committed-document-changed", true);
                }
                acceptedIdentity = candidateIdentity;
                retainSource = false;
                return new PreferenceWriteResult(PreferenceWriteStatus.Saved, candidateIdentity, null);
            }
            catch (FileNotFoundException)
            {
                return Protect(PreferenceWriteStatus.Conflict, "document-disappeared", commitAttempted);
            }
            catch (IOException) { return WriteIoFailure(commitAttempted, ownsTemporary, "document-write-io-failure"); }
            catch (UnauthorizedAccessException) { return WriteIoFailure(commitAttempted, ownsTemporary, "document-write-access-denied"); }
            catch (SecurityException) { return WriteIoFailure(commitAttempted, ownsTemporary, "document-write-access-denied"); }
            finally
            {
                // Never delete a preexisting temp. After an ambiguous failed OS commit,
                // leave our one bounded candidate for recovery instead of guessing state.
                if (ownsTemporary && !commitAttempted)
                {
                    try { File.Delete(temporaryPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (SecurityException) { }
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (writerLease != null) writerLease.Dispose();
        }

        private bool RetainOriginal(byte[] current, string expectedIdentity)
        {
            // This exact archive is never rotated, overwritten or automatically
            // restored. A failed partial archive remains recovery material.
            bool exists;
            try
            {
                FileAttributes attributes = File.GetAttributes(retainedSourcePath);
                // Matching bytes through a link to the primary would cease to be
                // the old format as soon as Replace changes the primary's target.
                // Only an ordinary file at this exact archive path is acceptable.
                if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0) return false;
                exists = true;
            }
            catch (FileNotFoundException) { exists = false; }
            if (!exists)
            {
                using (var archive = new FileStream(retainedSourcePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    archive.Write(current, 0, current.Length); archive.Flush(true); archive.Position = 0;
                    return String.Equals(Identity(ReadBounded(archive)), expectedIdentity, StringComparison.Ordinal);
                }
            }
            using (var archive = new FileStream(retainedSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                return String.Equals(Identity(ReadBounded(archive)), expectedIdentity, StringComparison.Ordinal);
        }

        private PreferenceWriteResult WriteIoFailure(bool commitAttempted, bool ownsTemporary, string error)
        {
            // Notes can retry a known precommit failure; all ambiguous commits and
            // preexisting recovery material remain latched. Configuration keeps its
            // original terminal-failure behavior. Failed temp cleanup will make the
            // next CreateNew fail safely, never overwrite that surviving candidate.
            return protectRecovery && ownsTemporary && !commitAttempted
                ? Failure(PreferenceWriteStatus.IoFailure, error) : Protect(PreferenceWriteStatus.IoFailure, error, commitAttempted);
        }

        private PreferenceReadResult RememberIoFailure(string error)
        {
            return RememberFailure(PreferenceReadStatus.IoFailure, PreferenceWriteStatus.IoFailure, error);
        }

        private PreferenceReadResult RememberFailure(PreferenceReadStatus readStatus, PreferenceWriteStatus writeStatus, string error)
        {
            protection = writeStatus;
            protectionError = error;
            initialRead = ReadFailure(readStatus, error);
            return initialRead;
        }

        private PreferenceWriteResult Protect(PreferenceWriteStatus status, string error, bool commitUnconfirmed = false)
        {
            // A conflict or ambiguous I/O failure is terminal for this instance. In
            // particular, another save must not rotate away a conflict recovery backup.
            protection = status;
            protectionError = error;
            unconfirmed = commitUnconfirmed;
            return new PreferenceWriteResult(status, null, error, commitUnconfirmed, true);
        }

        private static PreferenceReadResult ReadFailure(PreferenceReadStatus status, string error)
        {
            return new PreferenceReadResult(status, null, null, error);
        }

        private static PreferenceWriteResult Failure(PreferenceWriteStatus status, string error)
        {
            return new PreferenceWriteResult(status, null, error);
        }

        private byte[] ReadBounded(FileStream stream)
        {
            if (stream.Length > MaximumBytes) return null;
            byte[] result = new byte[(int)stream.Length];
            int offset = 0;
            while (offset < result.Length)
            {
                int count = stream.Read(result, offset, result.Length - offset);
                if (count == 0) throw new IOException("Document changed while reading.");
                offset += count;
            }
            if (stream.ReadByte() != -1) throw new IOException("Document grew while reading.");
            return result;
        }

        private static string Identity(byte[] bytes)
        {
            if (bytes == null) return null;
            using (SHA256 hash = SHA256.Create())
                return Convert.ToBase64String(hash.ComputeHash(bytes));
        }

        private static bool IsSharingViolation(IOException exception)
        {
            int code = exception.HResult & 0xffff;
            return code == 32 || code == 33;
        }
    }
}
