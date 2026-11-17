using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Settings;

namespace JueMingR.Infrastructure.Companions
{
    public sealed class FileCompanionArchive : ICompanionArchive
    {
        // Full native character data has its own bound; the preference-only
        // 64 KiB limit does not apply to banks, loadouts and Journey research.
        private const int MaximumBytes = 16 * 1024 * 1024;
        private readonly string root;
        private readonly Dictionary<string, Document> documents = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase);
        private sealed class Document
        { internal AtomicFileDocument Storage; internal string Path, Identity; internal byte[] Contents; }

        public FileCompanionArchive(string gameDirectory)
        {
            if (String.IsNullOrWhiteSpace(gameDirectory) || !Path.IsPathRooted(gameDirectory))
                throw new ArgumentException("A host-resolved game directory is required.", nameof(gameDirectory));
            root = Path.Combine(Path.GetFullPath(gameDirectory), "JueMingRData", "companions");
        }

        public CompanionOwnerArchive LoadOwner(string characterKey)
        {
            if (String.IsNullOrWhiteSpace(characterKey)) throw new ArgumentException("Character file identity is required.");
            var ownerDocument = Open(OwnerPath(characterKey));
            Guid owner;
            if (ownerDocument.Contents == null)
            {
                owner = Guid.NewGuid();
                Write(ownerDocument, owner.ToByteArray());
            }
            else
            {
                if (ownerDocument.Contents.Length != 16) throw new InvalidDataException("创建者关联记录损坏，未创建替代人物。");
                owner = new Guid(ownerDocument.Contents);
                if (owner == Guid.Empty) throw new InvalidDataException("创建者关联记录无效。");
            }
            string directory = Path.Combine(root, "characters", owner.ToString("N"));
            var profiles = new List<CompanionProfile>();
            var errors = new List<string>();
            if (Directory.Exists(directory))
            {
                var paths = new HashSet<string>(Directory.GetFiles(directory, "*.bin"), StringComparer.OrdinalIgnoreCase);
                foreach (string material in Directory.GetFiles(directory, "*.bin.*"))
                    if (material.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) || material.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) paths.Add(material.Substring(0, material.Length - 4));
                foreach (string path in paths)
                {
                    try
                    {
                        var document = Open(path);
                        var profile = Decode(document.Contents);
                        if (profile.OwnerId != owner || !String.Equals(Path.GetFileNameWithoutExtension(path), profile.Id.ToString("N"), StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("队友档案身份与目录不符。");
                        profiles.Add(profile);
                    }
                    // Semantic corruption is local to this character just like
                    // an unreadable file. Retain its bytes and report the actual
                    // failure while making the owner's other profiles available.
                    catch (Exception exception) when (exception is InvalidDataException || exception is IOException || exception is ArgumentException || exception is UnauthorizedAccessException)
                    { errors.Add(Path.GetFileName(path) + "：" + exception.Message); }
                }
            }
            profiles.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            return new CompanionOwnerArchive(owner, profiles.AsReadOnly(), errors.Count == 0 ? null : String.Join("；", errors));
        }

        public void LinkOwner(string characterKey, Guid ownerId)
        {
            if (ownerId == Guid.Empty) throw new ArgumentException("Owner is required.");
            var document = Open(OwnerPath(characterKey));
            if (document.Contents != null)
            {
                if (document.Contents.Length != 16 || new Guid(document.Contents) != ownerId)
                    throw new InvalidDataException("迁移目标已有不同归属，未覆盖人物关联。");
                return;
            }
            Write(document, ownerId.ToByteArray());
        }
        private string OwnerPath(string characterKey)
        {
            if (String.IsNullOrWhiteSpace(characterKey)) throw new ArgumentException("Character file identity is required.");
            string key;
            using (var sha = SHA256.Create()) key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(characterKey))).Replace("-", "").ToLowerInvariant();
            return Path.Combine(root, "owners", key + ".bin");
        }

        public void Save(CompanionProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string path = Path.Combine(root, "characters", profile.OwnerId.ToString("N"), profile.Id.ToString("N") + ".bin");
            var document = Open(path);
            if (document.Contents != null)
            {
                var previous = Decode(document.Contents);
                if (previous.Id != profile.Id || previous.OwnerId != profile.OwnerId || previous.Revision > profile.Revision || previous.DataEpoch > profile.DataEpoch)
                    throw new InvalidDataException("旧修订不能覆盖当前队友档案。");
                if (previous.Revision == profile.Revision)
                {
                    byte[] candidate = Encode(profile);
                    if (Convert.ToBase64String(candidate) == Convert.ToBase64String(document.Contents)) return;
                    throw new InvalidDataException("同一队友修订包含不同资源。");
                }
            }
            Write(document, Encode(profile));
        }

        private Document Open(string path)
        {
            Document document;
            if (documents.TryGetValue(path, out document)) return document;
            var storage = new AtomicFileDocument(path, MaximumBytes, protectRecovery: true);
            var result = storage.Read();
            if (result.Status != PreferenceReadStatus.Loaded && result.Status != PreferenceReadStatus.Missing)
            { storage.Dispose(); throw new IOException("队友档案读取失败：" + result.Error); }
            document = new Document { Storage = storage, Path = path, Identity = result.Identity, Contents = result.Contents };
            documents.Add(path, document); return document;
        }
        private void Write(Document document, byte[] bytes)
        {
            var result = document.Storage.Write(document.Identity, bytes);
            if (result.Status != PreferenceWriteStatus.Saved)
            {
                document.Storage.Dispose(); documents.Remove(document.Path);
                throw new IOException("队友档案保存失败：" + result.Error + (result.CommitUnconfirmed ? "（提交结果未确认）" : ""));
            }
            document.Identity = result.Identity; document.Contents = bytes;
        }

        private static byte[] Encode(CompanionProfile profile)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
            {
                writer.Write("JueMingR.Companion"); writer.Write(3);
                writer.Write(profile.Id.ToByteArray()); writer.Write(profile.OwnerId.ToByteArray());
                writer.Write(profile.Name); writer.Write(profile.Revision); writer.Write(profile.DataEpoch);
                var behavior = profile.Behavior;
                writer.Write(behavior.Follow); writer.Write(behavior.AttackEnabled); writer.Write(behavior.SelfDefense); writer.Write(behavior.PreferOwner);
                writer.Write(profile.PermanentlyDead);
                writer.Write(behavior.AutoRespawn); writer.Write(behavior.ManualWeaponSlot); writer.Write(behavior.ManualWeaponType); writer.Write((byte)behavior.ManualWeaponPrefix);
                byte[] data = profile.NativeData; writer.Write(data.Length); writer.Write(data); writer.Flush();
                using (var sha = SHA256.Create()) writer.Write(sha.ComputeHash(stream.ToArray()));
                writer.Flush();
                return stream.ToArray();
            }
        }
        private static CompanionProfile Decode(byte[] bytes)
        {
            if (bytes == null) throw new InvalidDataException("队友档案缺失，未自动补发初始物品。");
            if (bytes.Length < 32) throw new InvalidDataException("队友档案内容不完整。");
            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes, 0, bytes.Length - 32);
                for (int i = 0; i < digest.Length; i++) if (digest[i] != bytes[bytes.Length - 32 + i])
                    throw new InvalidDataException("队友档案完整性校验失败，未自动重建人物。");
            }
            using (var stream = new MemoryStream(bytes, 0, bytes.Length - 32, false))
            using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
            {
                if (reader.ReadString() != "JueMingR.Companion") throw new InvalidDataException("不支持的队友档案格式。");
                int version = reader.ReadInt32();
                if (version != 1 && version != 2 && version != 3) throw new InvalidDataException("不支持的队友档案格式。");
                Guid id = new Guid(reader.ReadBytes(16)), owner = new Guid(reader.ReadBytes(16));
                string name = reader.ReadString(); long revision = reader.ReadInt64(), epoch = reader.ReadInt64();
                var behavior = new CompanionBehavior { Follow = ReadBoolean(reader), AttackEnabled = ReadBoolean(reader), SelfDefense = ReadBoolean(reader), PreferOwner = ReadBoolean(reader) };
                // v1 had no terminal lifecycle marker. Its existing characters remain
                // live; v2 keeps native bytes and terminal identity in one commit.
                bool permanentlyDead = version >= 2 && ReadBoolean(reader);
                // v1/v2 keep native bytes/ownership and default to automatic
                // respawn with no manual weapon. One v3 revision stores both
                // preferences atomically with the native character and terminal flag.
                if (version >= 3) { behavior.AutoRespawn = ReadBoolean(reader); behavior.ManualWeaponSlot = reader.ReadInt32(); behavior.ManualWeaponType = reader.ReadInt32(); behavior.ManualWeaponPrefix = reader.ReadByte(); }
                int length = reader.ReadInt32();
                if (length <= 0 || length > MaximumBytes || length != stream.Length - stream.Position)
                    throw new InvalidDataException("队友人物数据长度不符。");
                return new CompanionProfile(id, owner, name, revision, epoch, behavior, reader.ReadBytes(length), permanentlyDead);
            }
        }
        private static bool ReadBoolean(BinaryReader reader)
        { byte value = reader.ReadByte(); if (value > 1) throw new InvalidDataException("无效的队友设置。"); return value == 1; }
        public void Dispose()
        { foreach (var document in documents.Values) document.Storage.Dispose(); documents.Clear(); }
    }
}
