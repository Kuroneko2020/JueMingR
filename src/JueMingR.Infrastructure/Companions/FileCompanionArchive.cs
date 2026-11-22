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
    public sealed partial class FileCompanionArchive : ICompanionArchive
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
                owner = ownerDocument.Contents.Length == 16 ? new Guid(ownerDocument.Contents) : Guid.Empty;
                if (owner == Guid.Empty)
                {
                    Release(ownerDocument);
                    throw new InvalidDataException("创建者关联记录无效，未创建替代人物。");
                }
            }
            string directory = Path.Combine(root, "characters", owner.ToString("N"));
            var profiles = new List<CompanionProfile>();
            var deletions = new List<CompanionDeletion>();
            var retiring = new HashSet<Guid>();
            var errors = new List<string>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                paths.UnionWith(Directory.GetFiles(directory, "*.bin"));
                foreach (string material in Directory.GetFiles(directory, "*.bin.*"))
                    if (material.EndsWith(".bin.bak", StringComparison.OrdinalIgnoreCase) || material.EndsWith(".bin.tmp", StringComparison.OrdinalIgnoreCase))
                        paths.Add(material.Substring(0, material.Length - 4));
                foreach (string marker in Directory.GetFiles(directory, "*.bin.delete"))
                {
                    string file = Path.GetFileName(marker);
                    if (file.Length != 32 + ".bin.delete".Length || !file.EndsWith(".bin.delete", StringComparison.OrdinalIgnoreCase) ||
                        !Guid.TryParseExact(file.Substring(0, 32), "N", out var id)) continue;
                    retiring.Add(id);
                    try { deletions.Add(ReadDeletion(marker, owner, id).Deletion); }
                    catch (Exception error) when (ArchiveError(error)) { errors.Add(file + "：" + error.Message); }
                }
            }
            catch (DirectoryNotFoundException) when (paths.Count == 0) { }
            foreach (string path in paths)
            {
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out var id) && retiring.Contains(id)) continue;
                Document document = null;
                try
                {
                    document = Open(path);
                    var profile = Decode(document.Contents);
                    if (profile.OwnerId != owner || !String.Equals(Path.GetFileNameWithoutExtension(path), profile.Id.ToString("N"), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("队友档案身份与目录不符。");
                    profiles.Add(profile);
                }
                catch (Exception exception) when (exception is InvalidDataException || exception is IOException || exception is ArgumentException || exception is UnauthorizedAccessException)
                {
                    // Release only the failed document. An explicit reload can
                    // re-read it after repair without disturbing healthy leases.
                    if (document != null) Release(document);
                    errors.Add(Path.GetFileName(path) + "：" + exception.Message);
                }
            }
            profiles.Sort((a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            return new CompanionOwnerArchive(owner, profiles.AsReadOnly(), errors.Count == 0 ? null : String.Join("；", errors), deletions.AsReadOnly());
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
            if (HasOrdinaryFile(path + ".delete")) { Release(document); throw new InvalidOperationException("该人物正在删除，未写回旧档案。"); }
            if (document.Contents != null)
            {
                var previous = Decode(document.Contents);
                if (previous.Id != profile.Id || previous.OwnerId != profile.OwnerId || previous.Revision > profile.Revision || previous.DataEpoch > profile.DataEpoch)
                    throw new InvalidDataException("旧修订不能覆盖当前队友档案。");
                if (previous.Revision == profile.Revision)
                {
                    byte[] candidate = Encode(profile);
                    // Old encodings of the same revision remain idempotent.
                    // A migration is written only with a new resource revision.
                    if (EqualBytes(candidate, Encode(previous))) return;
                    throw new InvalidDataException("同一队友修订包含不同资源。");
                }
            }
            Write(document, Encode(profile), character: true);
        }

        private static bool EqualBytes(byte[] first, byte[] second)
        {
            if (first.Length != second.Length) return false;
            for (int i = 0; i < first.Length; i++)
                if (first[i] != second[i]) return false;
            return true;
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
        private void Write(Document document, byte[] bytes, bool character = false)
        {
            var result = document.Storage.Write(document.Identity, bytes);
            if (result.Status != PreferenceWriteStatus.Saved)
            {
                if (character && (result.Status == PreferenceWriteStatus.Conflict || result.CommitUnconfirmed) &&
                    !deletionSourceConflicts.ContainsKey(document.Path))
                    deletionSourceConflicts.Add(document.Path, document.Contents == null ? null : Hash(document.Contents));
                Release(document);
                throw new IOException("队友档案保存失败：" + result.Error + (result.CommitUnconfirmed ? "（提交结果未确认）" : ""));
            }
            // Only a confirmed write from the previously trusted source reclaims its
            // deletion proof. Reopening replacement bytes alone establishes no proof.
            if (character && deletionSourceConflicts.TryGetValue(document.Path, out var expected) &&
                MatchKnownHash(expected, document.Contents == null ? null : Hash(document.Contents)))
                deletionSourceConflicts.Remove(document.Path);
            document.Identity = result.Identity; document.Contents = bytes;
        }
        private void Release(Document document)
        { document.Storage.Dispose(); documents.Remove(document.Path); }

        private static byte[] Encode(CompanionProfile profile)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
            {
                writer.Write("JueMingR.Companion"); writer.Write(6);
                writer.Write(profile.Id.ToByteArray()); writer.Write(profile.OwnerId.ToByteArray());
                writer.Write(profile.Name); writer.Write(profile.Revision); writer.Write(profile.DataEpoch);
                var behavior = profile.Behavior;
                writer.Write(behavior.Follow); writer.Write(behavior.AttackEnabled); writer.Write(behavior.AutoRespawn); writer.Write(profile.PermanentlyDead);
                writer.Write((byte)behavior.MainWeapons.Length);
                foreach (var weapon in behavior.MainWeapons)
                {
                    writer.Write(weapon.Type); writer.Write((byte)weapon.Prefix); writer.Write((byte)weapon.SlotHint);
                }
                writer.Write(behavior.HealthRecoveryEnabled); writer.Write((byte)behavior.HealthRecoveryMode);
                writer.Write(behavior.ManaRecoveryEnabled); writer.Write(behavior.AllowNurse);
                writer.Write(behavior.BuffEnabled); writer.Write(behavior.GrowthEnabled); writer.Write(behavior.FurnitureEnabled);
                WriteTypes(writer, behavior.ExcludedHealingTypes);
                WriteTypes(writer, behavior.ExcludedManaTypes);
                WriteTypes(writer, behavior.AllowedGrowthTypes);
                WriteTypes(writer, behavior.AllowedBuffTypes);
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
                if (version < 1 || version > 6) throw new InvalidDataException("不支持的队友档案格式。");
                Guid id = new Guid(reader.ReadBytes(16)), owner = new Guid(reader.ReadBytes(16));
                string name = reader.ReadString(); long revision = reader.ReadInt64(), epoch = reader.ReadInt64();
                var behavior = new CompanionBehavior { Follow = ReadBoolean(reader), AttackEnabled = ReadBoolean(reader) };
                bool permanentlyDead = version >= 4 ? ReadSupplyPreferences(reader, version, behavior) : ReadLegacy(reader, version, behavior);
                int length = reader.ReadInt32();
                if (length <= 0 || length > MaximumBytes || length != stream.Length - stream.Position)
                    throw new InvalidDataException("队友人物数据长度不符。");
                return new CompanionProfile(id, owner, name, revision, epoch, behavior, reader.ReadBytes(length), permanentlyDead);
            }
        }

        private static bool ReadLegacy(BinaryReader reader, int version, CompanionBehavior behavior)
        {
            // Consume retired fields in their original positions. The default
            // constructor supplies all settings absent from older versions.
            ReadBoolean(reader); // SelfDefense
            ReadBoolean(reader); // PreferOwner
            bool permanentlyDead = version >= 2 && ReadBoolean(reader);
            if (version < 3) return permanentlyDead;
            behavior.AutoRespawn = ReadBoolean(reader);
            int slot = reader.ReadInt32(), type = reader.ReadInt32(), prefix = reader.ReadByte();
            if (slot == -1 && (type != 0 || prefix != 0))
                throw new InvalidDataException("无效的旧队友武器设置。");
            if (slot != -1) behavior.MainWeapons = new[] { new CompanionWeaponPreference(type, prefix, slot) };
            return permanentlyDead;
        }

        private static bool ReadSupplyPreferences(BinaryReader reader, int version, CompanionBehavior behavior)
        {
            behavior.AutoRespawn = ReadBoolean(reader);
            bool permanentlyDead = ReadBoolean(reader);
            int count = reader.ReadByte();
            if (count > CompanionBehavior.MaxMainWeapons) throw new InvalidDataException("无效的队友主要武器数量。");
            if (count != 0)
            {
                var weapons = new CompanionWeaponPreference[count];
                for (int i = 0; i < weapons.Length; i++)
                    weapons[i] = new CompanionWeaponPreference(reader.ReadInt32(), reader.ReadByte(), reader.ReadByte());
                behavior.MainWeapons = weapons;
            }
            behavior.HealthRecoveryEnabled = ReadBoolean(reader);
            behavior.HealthRecoveryMode = (CompanionHealthRecoveryMode)reader.ReadByte();
            behavior.ManaRecoveryEnabled = ReadBoolean(reader);
            behavior.AllowNurse = ReadBoolean(reader);
            // Until v6 the healing switch also gated paid nurse visits. Preserve
            // the effective old permission when the two controls separate.
            if (version < 6) behavior.AllowNurse &= behavior.HealthRecoveryEnabled;
            // v4's shared permission must consume just one byte before the type lists.
            behavior.BuffEnabled = ReadBoolean(reader);
            behavior.GrowthEnabled = version >= 5 ? ReadBoolean(reader) : behavior.BuffEnabled;
            if (version >= 6) behavior.FurnitureEnabled = ReadBoolean(reader);
            behavior.ExcludedHealingTypes = ReadTypes(reader);
            behavior.ExcludedManaTypes = ReadTypes(reader);
            behavior.AllowedGrowthTypes = ReadTypes(reader);
            behavior.AllowedBuffTypes = ReadTypes(reader);
            return permanentlyDead;
        }

        private static int[] ReadTypes(BinaryReader reader)
        {
            int count = reader.ReadUInt16();
            if (count > CompanionBehavior.MaxSupplyTypes) throw new InvalidDataException("无效的队友补给列表。");
            if (count == 0) return Array.Empty<int>();
            var types = new int[count];
            for (int i = 0; i < types.Length; i++) types[i] = reader.ReadInt32();
            return types;
        }

        private static void WriteTypes(BinaryWriter writer, int[] types)
        {
            writer.Write((ushort)types.Length);
            foreach (int type in types) writer.Write(type);
        }
        private static bool ReadBoolean(BinaryReader reader)
        { byte value = reader.ReadByte(); if (value > 1) throw new InvalidDataException("无效的队友设置。"); return value == 1; }
        public void Dispose()
        { foreach (var document in documents.Values) document.Storage.Dispose(); documents.Clear(); deletionSourceConflicts.Clear(); }
    }
}
