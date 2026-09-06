using System;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using JueMingR.Platform.Settings;

namespace JueMingR.Infrastructure.Settings
{
    public sealed class FilePreferenceStorage : IPreferenceStorage
    {
        private const int MaximumBytes = 65536;
        private const string MissingIdentity = "missing";
        private readonly string path;
        private FileStream writerLease;
        private PreferenceReadResult initialRead;
        private string acceptedIdentity;
        private PreferenceWriteStatus? protection;
        private string protectionError;
        private bool disposed;

        public FilePreferenceStorage(string fullDocumentPath)
        {
            if (String.IsNullOrWhiteSpace(fullDocumentPath) || !Path.IsPathRooted(fullDocumentPath))
                throw new ArgumentException("An explicit absolute document path is required.", nameof(fullDocumentPath));
            path = Path.GetFullPath(fullDocumentPath);
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
                acceptedIdentity = MissingIdentity;
                initialRead = new PreferenceReadResult(PreferenceReadStatus.Missing, null, acceptedIdentity, null);
            }
            catch (IOException) { return RememberIoFailure("document-read-io-failure"); }
            catch (UnauthorizedAccessException) { return RememberIoFailure("document-read-access-denied"); }
            catch (SecurityException) { return RememberIoFailure("document-read-access-denied"); }
            return initialRead;
        }

        public PreferenceWriteResult Write(string expectedIdentity, byte[] contents)
        {
            if (disposed) return Failure(PreferenceWriteStatus.IoFailure, "storage-disposed");
            if (protection.HasValue) return Failure(protection.Value, protectionError);
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
                            return Protect(PreferenceWriteStatus.Conflict, "replace-race-backup-preserved");
                    }
                }

                using (var committed = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] actual = ReadBounded(committed);
                    if (actual == null || !String.Equals(Identity(actual), candidateIdentity, StringComparison.Ordinal))
                        return Protect(PreferenceWriteStatus.Conflict, "committed-document-changed");
                }
                acceptedIdentity = candidateIdentity;
                return new PreferenceWriteResult(PreferenceWriteStatus.Saved, candidateIdentity, null);
            }
            catch (FileNotFoundException)
            {
                return Protect(PreferenceWriteStatus.Conflict, "document-disappeared");
            }
            catch (IOException) { return Protect(PreferenceWriteStatus.IoFailure, "document-write-io-failure"); }
            catch (UnauthorizedAccessException) { return Protect(PreferenceWriteStatus.IoFailure, "document-write-access-denied"); }
            catch (SecurityException) { return Protect(PreferenceWriteStatus.IoFailure, "document-write-access-denied"); }
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

        private PreferenceWriteResult Protect(PreferenceWriteStatus status, string error)
        {
            // A conflict or ambiguous I/O failure is terminal for this instance. In
            // particular, another save must not rotate away a conflict recovery backup.
            protection = status;
            protectionError = error;
            return Failure(status, error);
        }

        private static PreferenceReadResult ReadFailure(PreferenceReadStatus status, string error)
        {
            return new PreferenceReadResult(status, null, null, error);
        }

        private static PreferenceWriteResult Failure(PreferenceWriteStatus status, string error)
        {
            return new PreferenceWriteResult(status, null, error);
        }

        private static byte[] ReadBounded(FileStream stream)
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
