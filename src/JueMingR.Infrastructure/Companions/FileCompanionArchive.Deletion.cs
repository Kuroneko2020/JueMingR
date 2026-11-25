using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Settings;
using Microsoft.Win32.SafeHandles;

namespace JueMingR.Infrastructure.Companions
{
    public sealed partial class FileCompanionArchive
    {
        private const string DeletionFormat = "JueMingR.CompanionDeletion.v1";
        private const int MaximumDeletionBytes = 1024;
        private static readonly string[] DeletedSuffixes = { "", ".bak", ".tmp" };
        // This worker keeps the earliest unresolved expected primary identity
        // even when the atomic writer releases its cached document and lease.
        private readonly Dictionary<string, byte[]> deletionSourceConflicts = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        private sealed class DeletionRecord
        {
            internal CompanionDeletion Deletion;
            internal byte[][] Files;
            internal byte[] Identity;
        }

        public void DeleteFiles(CompanionDeletion request, long maximumRevision)
        {
            string path = DeletionPath(request);
            using (var temporary = new AtomicFileDocument(path, MaximumBytes, protectRecovery: true))
            {
                // Use the same stable lease as Save, including a cached live
                // document. Its lifetime covers both intent creation and purge.
                documents.TryGetValue(path, out var current);
                var storage = current?.Storage ?? temporary;
                storage.Read();
                if (!storage.HasWriteLease) throw new IOException("队友档案正被占用，无法删除。");
                string marker = path + ".delete";
                DeletionRecord record;
                if (HasOrdinaryFile(marker)) record = ReadDeletion(marker, request.OwnerId, request.ProfileId);
                else
                {
                    if (maximumRevision <= 0) throw new IOException("找不到已提交的删除记录，未清理文件。");
                    record = PlanDeletion(request, maximumRevision, path, current);
                    CommitDeletion(marker, record);
                    // Use exactly the durable record for both first and resumed purge.
                    record = ReadDeletion(marker, request.OwnerId, request.ProfileId);
                }

                // Verify every present member before changing any of them. A
                // stale backup or new unknown candidate is preserved for retry.
                for (int i = 0; i < DeletedSuffixes.Length; i++)
                    CheckDeletionIdentity(path + DeletedSuffixes[i], record.Files[i]);
                for (int i = 0; i < DeletedSuffixes.Length; i++)
                    DeleteVerified(path + DeletedSuffixes[i], record.Files[i]);
                if (current != null) Release(current);
                deletionSourceConflicts.Remove(path);
            }
        }

        private static void CommitDeletion(string marker, DeletionRecord record)
        {
            byte[] candidate = EncodeDeletion(record);
            string staged = marker + ".tmp";
            if (HasOrdinaryFile(staged))
            {
                using (var prior = new AtomicFileDocument(marker, MaximumDeletionBytes, protectRecovery: true))
                {
                    var read = prior.Read();
                    if (!prior.HasWriteLease || read.Error != "missing-document-with-recovery-material" || HasOrdinaryFile(marker + ".bak"))
                        throw new IOException("存在未能确认的删除恢复文件，相关文件已保留。");
                    // The old candidate never committed deletion. A later valid
                    // save may change its file hashes. Under both leases, parse
                    // the candidate on the exact opened handle before retiring it.
                    var deletion = record.Deletion;
                    DeleteVerified(staged, stream => DecodeDeletion(ReadExact(stream, MaximumDeletionBytes), deletion.OwnerId, deletion.ProfileId));
                }
            }
            using (var journal = new AtomicFileDocument(marker, MaximumDeletionBytes, protectRecovery: true))
            {
                var read = journal.Read();
                if (read.Status != PreferenceReadStatus.Missing || journal.Write(read.Identity, candidate).Status != PreferenceWriteStatus.Saved)
                    throw new IOException("删除记录未能可靠保存，档案文件已保留。");
            }
        }

        public void FinishDeletion(CompanionDeletion request)
        {
            string path = DeletionPath(request);
            string marker = path + ".delete";
            using (var storage = new AtomicFileDocument(path, MaximumBytes, protectRecovery: true))
            {
                storage.Read();
                if (!storage.HasWriteLease) throw new IOException("队友档案正被占用，无法结束删除。");
                foreach (string suffix in DeletedSuffixes)
                    if (HasOrdinaryFile(path + suffix)) throw new IOException("队友档案仍有文件，删除尚未完成。");
                if (HasOrdinaryFile(marker))
                {
                    var record = ReadDeletion(marker, request.OwnerId, request.ProfileId);
                    DeleteVerified(marker, record.Identity);
                }
            }
        }

        private string DeletionPath(CompanionDeletion request)
        {
            if (request == null || request.OwnerId == Guid.Empty || request.ProfileId == Guid.Empty)
                throw new ArgumentException("未能确认待删除的队友身份。", nameof(request));
            string characters = Path.Combine(root, "characters");
            string directory = Path.Combine(characters, request.OwnerId.ToString("N"));
            foreach (string part in new[] { Path.GetDirectoryName(root), root, characters, directory })
            {
                Directory.CreateDirectory(part);
                if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("队友档案目录包含链接，未删除任何文件。");
            }
            return Path.Combine(directory, request.ProfileId.ToString("N") + ".bin");
        }

        private static bool ArchiveError(Exception error) => error is IOException || error is InvalidDataException ||
            error is UnauthorizedAccessException || error is ArgumentException;

        private static bool HasOrdinaryFile(string path)
        {
            try
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                    throw new IOException("档案路径包含目录、链接或设备，相关文件已保留。");
                return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        private DeletionRecord PlanDeletion(CompanionDeletion deletion, long maximumRevision, string path, Document current)
        {
            var hashes = new byte[DeletedSuffixes.Length][];
            bool unresolvedConflict = deletionSourceConflicts.TryGetValue(path, out var expectedPrimary);
            for (int i = 0; i < hashes.Length; i++)
            {
                string member = path + DeletedSuffixes[i];
                if (!HasOrdinaryFile(member))
                {
                    if (i == 0 && (unresolvedConflict ? expectedPrimary != null : current?.Contents != null))
                        throw new IOException("主档案已被外部移走，相关文件已保留。");
                    continue;
                }
                using (var stream = new FileStream(member, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bytes = ReadExact(stream, MaximumBytes);
                    var profile = Decode(bytes);
                    if (profile.Id != deletion.ProfileId || profile.OwnerId != deletion.OwnerId || profile.Revision > maximumRevision)
                        throw new IOException("档案文件身份或内容已变化，相关文件已保留。");
                    hashes[i] = Hash(bytes);
                    if (i == 0 && unresolvedConflict && !MatchKnownHash(expectedPrimary, hashes[i]))
                        throw new IOException("主档案仍与保存前确认的内容不符，相关文件已保留。");
                    if (i == 0 && !unresolvedConflict && current != null && !EqualBytes(current.Contents ?? Array.Empty<byte>(), bytes))
                        throw new IOException("档案文件身份或内容已变化，相关文件已保留。");
                }
            }
            return new DeletionRecord { Deletion = deletion, Files = hashes };
        }

        private static bool MatchKnownHash(byte[] expected, byte[] actual) =>
            expected == null ? actual == null : actual != null && EqualBytes(expected, actual);

        private static byte[] EncodeDeletion(DeletionRecord record)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
            {
                writer.Write(DeletionFormat);
                writer.Write(record.Deletion.OwnerId.ToByteArray());
                writer.Write(record.Deletion.ProfileId.ToByteArray());
                writer.Write(record.Deletion.Name);
                foreach (byte[] hash in record.Files)
                {
                    writer.Write(hash != null);
                    if (hash != null) writer.Write(hash);
                }
                writer.Flush();
                writer.Write(Hash(stream.ToArray()));
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static DeletionRecord ReadDeletion(string path, Guid owner, Guid profile)
        {
            if (!HasOrdinaryFile(path)) throw new FileNotFoundException("找不到删除记录。");
            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                bytes = ReadExact(stream, MaximumDeletionBytes);
            return DecodeDeletion(bytes, owner, profile);
        }

        private static DeletionRecord DecodeDeletion(byte[] bytes, Guid owner, Guid profile)
        {
            int payloadLength = bytes.Length - 32;
            if (payloadLength <= 0 || !EqualBytes(Hash(bytes, payloadLength), Slice(bytes, payloadLength, 32)))
                throw new InvalidDataException("队友删除记录校验失败，相关文件已保留。");
            using (var stream = new MemoryStream(bytes, 0, payloadLength, false))
            using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
            {
                if (reader.ReadString() != DeletionFormat || new Guid(reader.ReadBytes(16)) != owner || new Guid(reader.ReadBytes(16)) != profile)
                    throw new InvalidDataException("队友删除记录身份或版本无效。");
                string name = reader.ReadString();
                if (String.IsNullOrWhiteSpace(name) || name.Length > 64) throw new InvalidDataException("队友删除记录中的姓名无效。");
                var hashes = new byte[DeletedSuffixes.Length][];
                for (int i = 0; i < hashes.Length; i++)
                    if (ReadBoolean(reader))
                    {
                        hashes[i] = reader.ReadBytes(32);
                        if (hashes[i].Length != 32) throw new InvalidDataException("队友删除记录不完整。");
                    }
                if (stream.Position != stream.Length) throw new InvalidDataException("队友删除记录包含多余内容。");
                return new DeletionRecord { Deletion = new CompanionDeletion(owner, profile, name), Files = hashes, Identity = Hash(bytes) };
            }
        }

        private static byte[] ReadExact(Stream stream, int maximum)
        {
            if (stream.Length > maximum) throw new InvalidDataException("队友文件超过可验证大小。");
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                var bytes = reader.ReadBytes((int)stream.Length);
                if (bytes.Length != stream.Length) throw new IOException("读取时队友文件已变化。");
                return bytes;
            }
        }

        private static byte[] Slice(byte[] bytes, int offset, int count)
        { var part = new byte[count]; Buffer.BlockCopy(bytes, offset, part, 0, count); return part; }

        private static byte[] Hash(byte[] bytes, int count = -1)
        { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes, 0, count < 0 ? bytes.Length : count); }

        private static void CheckDeletionIdentity(string path, byte[] expected)
        {
            if (!HasOrdinaryFile(path)) return;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                VerifyDeletionIdentity(stream, expected);
        }

        private static void VerifyDeletionIdentity(Stream stream, byte[] expected)
        {
            if (expected == null || !EqualBytes(Hash(ReadExact(stream, MaximumBytes)), expected))
                throw new IOException("队友文件与已确认的删除记录不符，已保留该文件。");
        }

        private static void DeleteVerified(string path, byte[] expected) =>
            DeleteVerified(path, stream => VerifyDeletionIdentity(stream, expected));

        private static void DeleteVerified(string path, Action<Stream> verify)
        {
            if (!HasOrdinaryFile(path)) return;
            // Mark the exact opened Windows file for deletion. Sharing only
            // read prevents a pathname rename/replacement between hash and removal.
            const uint readAndDelete = 0x80000000 | 0x00010000, shareRead = 1, existing = 3, openReparsePoint = 0x00200000;
            using (var handle = OpenDeletionHandle(path, readAndDelete, shareRead, IntPtr.Zero, existing, openReparsePoint, IntPtr.Zero))
            {
                if (handle.IsInvalid) throw new IOException("无法取得队友文件的删除权限。", new Win32Exception(Marshal.GetLastWin32Error()));
                using (var stream = new FileStream(handle, FileAccess.Read))
                {
                    verify(stream);
                    byte delete = 1;
                    if (!SetFileInformationByHandle(handle, 4, ref delete, 1))
                        throw new IOException("队友文件清理失败，可重试。", new Win32Exception(Marshal.GetLastWin32Error()));
                }
            }
            if (HasOrdinaryFile(path)) throw new IOException("队友文件删除尚未确认，可重试。");
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle OpenDeletionHandle(string path, uint access, uint share, IntPtr security,
            uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, ref byte info, int size);
    }
}
