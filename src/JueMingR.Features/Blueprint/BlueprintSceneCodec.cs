using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    // One binary document is the atomic scene publication unit. Its size follows
    // actual sparse facts, not the unrelated 64 KiB preference allowance. Counts
    // are bounded by the bytes still present before any payload allocation.
    public static class BlueprintSceneCodec
    {
        private const ulong Magic = 0x3130435342524d4aUL; // JMRBSC01
        public static byte[] Encode(BlueprintSceneDocument value)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(Magic); writer.Write(2); writer.Write(Hex(value.Scope)); writer.Write(value.Width); writer.Write(value.Height); writer.Write(value.Revision); writer.Write(value.Records.Count);
                    foreach (var record in value.Records)
                    {
                        writer.Write(record.Template.AssetId.ToByteArray()); writer.Write(record.Template.ContentRevision.ToByteArray()); writer.Write(Convert.FromBase64String(record.Template.ContentHash));
                        writer.Write(record.OriginX); writer.Write(record.OriginY); writer.Write(record.Order);
                        writer.Write((byte)(record.MirroredHorizontally ? 1 : 0));
                        writer.Write(record.Mask.RemovedLayerCount); writer.Write(record.Mask.RemovedConstructionLayerCount);
                        var cells = record.Mask.CopyCells(); var keys = new List<long>(cells.Keys); keys.Sort(); writer.Write(keys.Count);
                        foreach (long key in keys) { writer.Write(key); writer.Write(cells[key]); }
                        var objects = new List<int>(record.Mask.CopyObjects()); objects.Sort(); writer.Write(objects.Count); foreach (int id in objects) writer.Write(id);
                    }
                    writer.Flush();
                }
                byte[] payload = stream.ToArray(); byte[] hash; using (var sha = SHA256.Create()) hash = sha.ComputeHash(payload);
                stream.Write(hash, 0, hash.Length); return stream.ToArray();
            }
        }
        public static BlueprintSceneDocument Decode(byte[] bytes, string scope, int width, int height)
        {
            try { return DecodeKnown(bytes, scope, width, height); }
            catch (PreferenceFormatException) { throw; }
            catch (Exception error) when (error is InvalidDataException || error is IOException || error is ArgumentException || error is OverflowException || error is FormatException)
            { throw new PreferenceFormatException(PreferenceStatus.Invalid, "blueprint-scene-invalid"); }
        }
        private static BlueprintSceneDocument DecodeKnown(byte[] bytes, string scope, int width, int height)
        {
            if (bytes == null || bytes.Length < 12) throw new InvalidDataException("blueprint-scene-truncated");
            // Read only the stable format header before interpreting a version's
            // payload/checksum. Future versions are protected as unsupported,
            // rather than being mislabeled as current-format corruption.
            int version;
            using (var header = new BinaryReader(new MemoryStream(bytes, false)))
            {
                if (header.ReadUInt64() != Magic) throw new InvalidDataException("blueprint-scene-format");
                version = header.ReadInt32();
                if (version != 1 && version != 2) throw new PreferenceFormatException(PreferenceStatus.UnsupportedVersion, "blueprint-scene-version");
            }
            if (bytes == null || bytes.Length < 96) throw new InvalidDataException("blueprint-scene-truncated");
            using (var sha = SHA256.Create())
            { var hash = sha.ComputeHash(bytes, 0, bytes.Length - 32); for (int i = 0; i < hash.Length; i++) if (hash[i] != bytes[bytes.Length - 32 + i]) throw new InvalidDataException("blueprint-scene-hash"); }
            using (var stream = new MemoryStream(bytes, 0, bytes.Length - 32, false))
            using (var reader = new BinaryReader(stream))
            {
                reader.ReadUInt64(); reader.ReadInt32();
                if (Text(reader.ReadBytes(32)) != scope || reader.ReadInt32() != width || reader.ReadInt32() != height) throw new PreferenceFormatException(PreferenceStatus.Conflict, "blueprint-scene-scope");
                long revision = reader.ReadInt64(); int count = Count(reader, version == 1 ? 104 : 105); var records = new List<BlueprintSceneRecord>();
                for (int n = 0; n < count; n++)
                {
                    var template = new BlueprintTemplateKey(new Guid(reader.ReadBytes(16)), new Guid(reader.ReadBytes(16)), Convert.ToBase64String(reader.ReadBytes(32)));
                    int x = reader.ReadInt32(), y = reader.ReadInt32(); long order = reader.ReadInt64();
                    byte pose = version == 1 ? (byte)0 : reader.ReadByte();
                    // Unknown pose bits are future semantics, never silently normal.
                    if (pose > 1) throw new PreferenceFormatException(PreferenceStatus.UnsupportedVersion, "blueprint-scene-pose");
                    long removed = reader.ReadInt64(), construction = reader.ReadInt64();
                    if (removed < 0 || construction < 0 || construction > removed || removed > (long)width * height * 8) throw new InvalidDataException("blueprint-scene-mask-count");
                    int cellCount = Count(reader, 10); var cells = new Dictionary<long, ushort>(); long previous = -1;
                    for (int i = 0; i < cellCount; i++)
                    {
                        long key = reader.ReadInt64(); ushort bits = reader.ReadUInt16(); int cx = (int)key, cy = (int)(key >> 32);
                        if (key <= previous || cx < 0 || cy < 0 || cx >= width || cy >= height || bits == 0 || bits > 255) throw new InvalidDataException("blueprint-scene-mask-cell");
                        previous = key; cells.Add(key, bits);
                    }
                    int objectCount = Count(reader, 4); var objects = new HashSet<int>(); int priorId = 0;
                    for (int i = 0; i < objectCount; i++) { int id = reader.ReadInt32(); if (id <= priorId) throw new InvalidDataException("blueprint-scene-mask-object"); priorId = id; objects.Add(id); }
                    if (cellCount == 0 && objectCount == 0 && removed != 0 || (cellCount != 0 || objectCount != 0) && removed == 0) throw new InvalidDataException("blueprint-scene-mask-count");
                    records.Add(new BlueprintSceneRecord(template, x, y, order, new BlueprintProjectionMask(cells, objects, removed, construction), pose == 1));
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("blueprint-scene-trailing");
                return new BlueprintSceneDocument(scope, width, height, revision, records, version);
            }
        }
        private static int Count(BinaryReader reader, int minimumBytes)
        { int count = reader.ReadInt32(); if (count < 0 || count > (reader.BaseStream.Length - reader.BaseStream.Position) / minimumBytes) throw new InvalidDataException("blueprint-scene-count"); return count; }
        private static byte[] Hex(string value)
        { BlueprintSceneDocument.ValidateScope(value); var bytes = new byte[32]; for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16); return bytes; }
        private static string Text(byte[] bytes) { if (bytes.Length != 32) throw new InvalidDataException("blueprint-scene-truncated"); return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
    }
}
