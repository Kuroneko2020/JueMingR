using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    // Blueprint schema belongs to its feature. Blocks and their SHA-256 index
    // are immutable; only the small metadata document publishes visibility/name.
    internal static class LibraryCodec
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        internal static byte[] EncodeMetadata(LibraryMetadata value)
        {
            string error; if (!LibraryName.TryValidate(value.Name, out error)) throw new InvalidDataException(error);
            string text = "{\"format\":\"JueMingR.BlueprintAsset\",\"version\":1,\"assetId\":" + Quote(value.AssetId.ToString("N")) + ",\"name\":" + Quote(value.Name) +
                ",\"contentRevision\":" + Quote(value.ContentRevision.ToString("N")) + ",\"contentHash\":" + Quote(value.ContentHash) +
                ",\"width\":" + Number(value.Width) + ",\"height\":" + Number(value.Height) + ",\"contentCount\":" + Number(value.ContentCount) + ",\"blockCount\":" + Number(value.BlockCount) +
                ",\"missingSummary\":" + Quote(value.MissingSummary ?? "") + ",\"createdTicks\":" + Number(value.CreatedTicks) + ",\"updatedTicks\":" + Number(value.UpdatedTicks) + ",\"deleted\":" + (value.Deleted ? "true" : "false") + "}\n";
            byte[] bytes = Utf8.GetBytes(text); DecodeMetadata(bytes, value.AssetId); return bytes;
        }
        internal static LibraryMetadata DecodeMetadata(byte[] bytes, Guid expectedId)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.BlueprintAsset", "format", "version", "assetId", "name", "contentRevision", "contentHash", "width", "height", "contentCount", "blockCount", "missingSummary", "createdTicks", "updatedTicks", "deleted");
            var value = new LibraryMetadata { AssetId = Id(Text(root, "assetId")), Name = Text(root, "name"), ContentRevision = Id(Text(root, "contentRevision")), ContentHash = Text(root, "contentHash"), Width = PreferenceJson.Integer(PreferenceJson.Required(root, "width", "number")), Height = PreferenceJson.Integer(PreferenceJson.Required(root, "height", "number")), ContentCount = Long(root, "contentCount"), BlockCount = Long(root, "blockCount"), MissingSummary = Text(root, "missingSummary"), CreatedTicks = Long(root, "createdTicks"), UpdatedTicks = Long(root, "updatedTicks") };
            string deleted = PreferenceJson.Required(root, "deleted", "boolean").Value;
            if (deleted != "true" && deleted != "false") throw PreferenceJson.Invalid(); value.Deleted = deleted == "true";
            string error;
            if (value.AssetId != expectedId || !LibraryName.TryValidate(value.Name, out error) || value.Width < 1 || value.Height < 1 || value.ContentCount < 1 || value.ContentCount > (long)value.Width * value.Height || value.BlockCount < 1 || value.BlockCount > Int64.MaxValue / 32L || value.CreatedTicks < 0 || value.UpdatedTicks < value.CreatedTicks || value.MissingSummary.Length > 8192 || !HashText(value.ContentHash)) throw PreferenceJson.Invalid();
            return value;
        }
        internal static byte[] EncodeRoot(LibraryRoot root)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, Utf8, true))
            {
                writer.Write(0x42524D4A); writer.Write(1); writer.Write(root.ContentRevision.ToByteArray()); writer.Write(root.BlockCount); writer.Write(root.IndexHash);
                CaptureSummary s = root.Summary; writer.Write(s.Left); writer.Write(s.Top); writer.Write(s.Width); writer.Write(s.Height); writer.Write(s.ContentCount); writer.Write(s.UnreliableObjectCount); writer.Write(s.StartTick); writer.Write(s.EndTick);
                WriteText(writer, s.TargetVersion ?? "", 64); string[] missing = s.MissingPayloads ?? Array.Empty<string>(); if (missing.Length > 16) throw Bad("missing-categories"); writer.Write(missing.Length); foreach (string text in missing) WriteText(writer, text, 256);
                writer.Flush(); byte[] bytes = stream.ToArray(); DecodeRoot(bytes); return bytes;
            }
        }
        internal static LibraryRoot DecodeRoot(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes, false)) using (var reader = new BinaryReader(stream, Utf8, true))
            {
                if (reader.ReadInt32() != 0x42524D4A || reader.ReadInt32() != 1) throw Bad("root-format-or-version");
                Guid revision = new Guid(ReadExact(reader, 16)); long blocks = reader.ReadInt64(); byte[] hash = ReadExact(reader, 32);
                var s = new CaptureSummary { Left = reader.ReadInt32(), Top = reader.ReadInt32(), Width = reader.ReadInt32(), Height = reader.ReadInt32(), ContentCount = reader.ReadInt64(), UnreliableObjectCount = reader.ReadInt64(), StartTick = reader.ReadUInt64(), EndTick = reader.ReadUInt64(), TargetVersion = ReadText(reader, 64) };
                int count = reader.ReadInt32(); if (count < 0 || count > 16) throw Bad("missing-count"); s.MissingPayloads = new string[count]; var seen = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < count; i++) { s.MissingPayloads[i] = ReadText(reader, 256); if (!seen.Add(s.MissingPayloads[i])) throw Bad("duplicate-missing-category"); }
                if (stream.Position != stream.Length || revision == Guid.Empty || blocks < 1 || blocks > Int64.MaxValue / 32L || s.Left < 0 || s.Top < 0 || s.Width < 1 || s.Height < 1 || (long)s.Left + s.Width > Int32.MaxValue || (long)s.Top + s.Height > Int32.MaxValue || s.ContentCount < 1 || s.ContentCount > (long)s.Width * s.Height || s.UnreliableObjectCount < 0 || s.EndTick < s.StartTick || String.IsNullOrWhiteSpace(s.TargetVersion)) throw Bad("root-fields");
                return new LibraryRoot { ContentRevision = revision, Summary = s, BlockCount = blocks, IndexHash = hash };
            }
        }
        internal static byte[] EncodeChunk(CaptureChunk chunk)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, Utf8, true))
            {
                writer.Write(0x43424D4A); writer.Write(1); writer.Write(chunk.Count); writer.Write(chunk.ObjectCount);
                for (int i = 0; i < chunk.Count; i++)
                {
                    CaptureCell c = chunk.Cells[i]; writer.Write(c.X); writer.Write(c.Y); writer.Write(c.ObjectId); writer.Write((byte)c.Layers); writer.Write(c.TileType); writer.Write(c.WallType); writer.Write(c.FrameX); writer.Write(c.FrameY); writer.Write(c.TilePaint); writer.Write(c.WallPaint); writer.Write(c.Slope); writer.Write(c.Wires); writer.Write(c.LiquidAmount); writer.Write(c.LiquidKind);
                    int flags = (c.HalfBrick ? 1 : 0) | (c.Inactive ? 2 : 0) | (c.Actuator ? 4 : 0) | (c.InvisibleTile ? 8 : 0) | (c.FullbrightTile ? 16 : 0) | (c.InvisibleWall ? 32 : 0) | (c.FullbrightWall ? 64 : 0) | (c.UnreliableObject ? 128 : 0); writer.Write((byte)flags);
                }
                for (int i = 0; i < chunk.ObjectCount; i++)
                {
                    CaptureObject o = chunk.Objects[i]; writer.Write(o.Id); writer.Write(o.Type); writer.Write(o.X); writer.Write(o.Y); writer.Write(o.Width); writer.Write(o.Height); writer.Write(o.Style); writer.Write(o.Alternate); writer.Write(o.PlacementStyle); writer.Write(o.RandomVariant); writer.Write(o.Direction); writer.Write(o.OriginX); writer.Write(o.OriginY); writer.Write(o.Reliable); WriteText(writer, o.Reason ?? "", 2048); WriteText(writer, o.MissingPayload ?? "", 256);
                }
                writer.Flush(); byte[] bytes = stream.ToArray(); if (bytes.Length > 1048576) throw Bad("block-too-large"); DecodeChunk(bytes); return bytes;
            }
        }
        internal static CaptureChunk DecodeChunk(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 16 || bytes.Length > 1048576) throw Bad("block-size");
            using (var stream = new MemoryStream(bytes, false)) using (var reader = new BinaryReader(stream, Utf8, true))
            {
                if (reader.ReadInt32() != 0x43424D4A || reader.ReadInt32() != 1) throw Bad("block-format-or-version");
                int count = reader.ReadInt32(), objectCount = reader.ReadInt32(); if (count < 0 || objectCount < 0 || (long)count + objectCount > CaptureChunk.MaximumRecords) throw Bad("record-count");
                var cells = new CaptureCell[count]; var coordinates = new HashSet<long>();
                for (int i = 0; i < count; i++)
                {
                    var c = new CaptureCell { X = reader.ReadInt32(), Y = reader.ReadInt32(), ObjectId = reader.ReadInt32(), Layers = (CaptureLayer)reader.ReadByte(), TileType = reader.ReadUInt16(), WallType = reader.ReadUInt16(), FrameX = reader.ReadInt16(), FrameY = reader.ReadInt16(), TilePaint = reader.ReadByte(), WallPaint = reader.ReadByte(), Slope = reader.ReadByte(), Wires = reader.ReadByte(), LiquidAmount = reader.ReadByte(), LiquidKind = reader.ReadByte() };
                    byte flags = reader.ReadByte(); c.HalfBrick = (flags & 1) != 0; c.Inactive = (flags & 2) != 0; c.Actuator = (flags & 4) != 0; c.InvisibleTile = (flags & 8) != 0; c.FullbrightTile = (flags & 16) != 0; c.InvisibleWall = (flags & 32) != 0; c.FullbrightWall = (flags & 64) != 0; c.UnreliableObject = (flags & 128) != 0;
                    if (c.X < 0 || c.Y < 0 || c.ObjectId < 0 || c.Layers == CaptureLayer.None || ((int)c.Layers & ~31) != 0 || c.Slope > 5 || c.Wires > 15 || c.LiquidKind > 3 || !coordinates.Add(((long)c.Y << 32) | (uint)c.X)) throw Bad("cell-fields-or-duplicate");
                    if ((c.Layers & CaptureLayer.Tile) == 0 && (c.ObjectId != 0 || c.UnreliableObject || c.TileType != 0 || c.FrameX != 0 || c.FrameY != 0 || c.TilePaint != 0 || c.Slope != 0 || c.HalfBrick || c.Inactive || c.InvisibleTile || c.FullbrightTile)) throw Bad("tile-without-host");
                    if ((c.Layers & CaptureLayer.Wall) == 0 && (c.WallType != 0 || c.WallPaint != 0 || c.InvisibleWall || c.FullbrightWall) || (c.Layers & CaptureLayer.Wall) != 0 && c.WallType == 0 || ((c.Layers & CaptureLayer.Wire) != 0) != (c.Wires != 0) || ((c.Layers & CaptureLayer.Actuator) != 0) != c.Actuator || ((c.Layers & CaptureLayer.Liquid) != 0) != (c.LiquidAmount != 0) || c.LiquidAmount == 0 && c.LiquidKind != 0 || c.UnreliableObject && c.ObjectId == 0) throw Bad("layer-fields");
                    cells[i] = c;
                }
                var objects = new CaptureObject[objectCount]; var objectIds = new HashSet<int>();
                for (int i = 0; i < objectCount; i++)
                {
                    var o = new CaptureObject { Id = reader.ReadInt32(), Type = reader.ReadUInt16(), X = reader.ReadInt32(), Y = reader.ReadInt32(), Width = reader.ReadInt32(), Height = reader.ReadInt32(), Style = reader.ReadInt32(), Alternate = reader.ReadInt32(), PlacementStyle = reader.ReadInt32(), RandomVariant = reader.ReadInt32(), Direction = reader.ReadInt32(), OriginX = reader.ReadInt32(), OriginY = reader.ReadInt32(), Reliable = ReadBool(reader), Reason = ReadText(reader, 2048), MissingPayload = ReadText(reader, 256) };
                    if (o.Id < 1 || !objectIds.Add(o.Id) || o.X < 0 || o.Y < 0 || o.Width < 1 || o.Height < 1 || (long)o.X + o.Width > Int32.MaxValue || (long)o.Y + o.Height > Int32.MaxValue || o.Style < 0 || o.Alternate < 0 || o.PlacementStyle < 0 || o.RandomVariant < 0 || o.Direction < -1 || o.Direction > 1 || o.OriginX < 0 || o.OriginY < 0 || !o.Reliable && String.IsNullOrWhiteSpace(o.Reason)) throw Bad("object-fields");
                    objects[i] = o;
                }
                if (stream.Position != stream.Length) throw Bad("block-trailing-fields"); return new CaptureChunk(cells, count, objects, objectCount);
            }
        }
        internal static byte[] Hash(byte[] bytes) { using (var hash = SHA256.Create()) return hash.ComputeHash(bytes); }
        internal static byte[] Hash(Stream stream) { using (var hash = SHA256.Create()) return hash.ComputeHash(stream); }
        internal static string HashString(byte[] bytes) { return Convert.ToBase64String(Hash(bytes)); }
        internal static bool Equal(byte[] a, byte[] b) { if (a == null || b == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static bool HashText(string value) { try { return value != null && value.Length == 44 && Convert.FromBase64String(value).Length == 32; } catch (FormatException) { return false; } }
        private static Guid Id(string value) { Guid id; if (value == null || value.Length != 32 || !Guid.TryParseExact(value, "N", out id) || id == Guid.Empty || value != id.ToString("N")) throw PreferenceJson.Invalid(); return id; }
        private static string Text(System.Xml.Linq.XElement root, string name) { return PreferenceJson.Required(root, name, "string").Value; }
        private static long Long(System.Xml.Linq.XElement root, string name) { var node = PreferenceJson.Required(root, name, "number"); long value; if (node.HasElements || !Int64.TryParse(node.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)) throw PreferenceJson.Invalid(); return value; }
        private static string Number(long value) { return value.ToString(CultureInfo.InvariantCulture); }
        private static string Quote(string value) { var output = new StringBuilder("\""); foreach (char c in value ?? "") { if (c == '"' || c == '\\') output.Append('\\').Append(c); else if (c < 32) output.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); else output.Append(c); } return output.Append('"').ToString(); }
        private static void WriteText(BinaryWriter writer, string value, int maximum) { byte[] bytes = Utf8.GetBytes(value ?? ""); if (bytes.Length > maximum) throw Bad("string-size"); writer.Write(bytes.Length); writer.Write(bytes); }
        private static string ReadText(BinaryReader reader, int maximum) { int length = reader.ReadInt32(); if (length < 0 || length > maximum) throw Bad("string-size"); return Utf8.GetString(ReadExact(reader, length)); }
        private static byte[] ReadExact(BinaryReader reader, int length) { byte[] bytes = reader.ReadBytes(length); if (bytes.Length != length) throw new EndOfStreamException(); return bytes; }
        private static bool ReadBool(BinaryReader reader) { byte value = reader.ReadByte(); if (value > 1) throw Bad("boolean"); return value != 0; }
        private static InvalidDataException Bad(string reason) { return new InvalidDataException("blueprint-" + reason); }
    }
}
