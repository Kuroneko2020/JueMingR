using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using JueMingR.Platform.Footprints;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Footprints
{
    // Small roots and independently checksummed immutable blocks. Decode never
    // repairs input; unsupported/unknown bytes remain protected at their source.
    internal static class FootprintCodec
    {
        internal const int MaximumBytes = 16384;
        internal static byte[] Catalog(string pair, string generation, string operation, int width, int height)
        {
            return Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.FootprintCatalog\",\"version\":1,\"pair\":\"" + pair + "\",\"generation\":\"" + generation + "\",\"operation\":\"" + operation + "\",\"width\":" + width.ToString(CultureInfo.InvariantCulture) + ",\"height\":" + height.ToString(CultureInfo.InvariantCulture) + "}");
        }
        internal static void ReadCatalog(byte[] bytes, string pair, int width, int height, out string generation, out string operation)
        {
            try
            {
                var root = PreferenceJson.Read(bytes, "JueMingR.FootprintCatalog", "format", "version", "pair", "generation", "operation", "width", "height");
                generation = PreferenceJson.Required(root, "generation", "string").Value;
                operation = PreferenceJson.Required(root, "operation", "string").Value;
                if (PreferenceJson.Required(root, "pair", "string").Value != pair || !GuidText(generation) || operation != "" && !GuidText(operation) ||
                    PreferenceJson.Integer(PreferenceJson.Required(root, "width", "number")) != width || PreferenceJson.Integer(PreferenceJson.Required(root, "height", "number")) != height) throw Invalid();
            }
            catch (PreferenceFormatException) { throw Invalid(); }
        }
        internal static bool GuidText(string value) { Guid parsed; return value != null && value.Length == 32 && Guid.TryParseExact(value, "N", out parsed) && parsed.ToString("N") == value; }
        internal static byte[] Encode(string pair, string generation, int width, int height, long blocks, FootprintSample[] samples, int count, bool root)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(root ? 0x3152464A : 0x3142464A); writer.Write(1); writer.Write(pair); writer.Write(generation); writer.Write(width); writer.Write(height); writer.Write(blocks); writer.Write(count);
                    for (int i = 0; i < count; i++)
                    {
                        var p = samples[i]; writer.Write(p.Sequence); writer.Write(p.Start); writer.Write(p.End); writer.Write(p.Segment); writer.Write(p.X); writer.Write(p.Y); writer.Write((byte)p.Position);
                    }
                }
                byte[] body = stream.ToArray(); using (var sha = SHA256.Create()) { byte[] digest = sha.ComputeHash(body); stream.Write(digest, 0, digest.Length); } return stream.ToArray();
            }
        }
        internal static FootprintSample[] Decode(byte[] bytes, string pair, string generation, int width, int height, bool root, out long blocks)
        {
            if (bytes == null || bytes.Length < 64 || bytes.Length > MaximumBytes) throw Invalid();
            using (var sha = SHA256.Create()) { byte[] hash = sha.ComputeHash(bytes, 0, bytes.Length - 32); for (int i = 0; i < 32; i++) if (hash[i] != bytes[bytes.Length - 32 + i]) throw Invalid(); }
            try
            {
                using (var stream = new MemoryStream(bytes, 0, bytes.Length - 32, false))
                using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
                {
                    if (reader.ReadInt32() != (root ? 0x3152464A : 0x3142464A) || reader.ReadInt32() != 1 || reader.ReadString() != pair || reader.ReadString() != generation || reader.ReadInt32() != width || reader.ReadInt32() != height) throw Invalid();
                    blocks = reader.ReadInt64(); int count = reader.ReadInt32();
                    if (blocks < 0 || blocks > (Int64.MaxValue - 256) / 256 || count < 1 || count > 256 || !root && count != 256) throw Invalid();
                    var result = new FootprintSample[count];
                    for (int i = 0; i < count; i++)
                    {
                        long sequence = reader.ReadInt64(), start = reader.ReadInt64(), end = reader.ReadInt64(), segment = reader.ReadInt64();
                        float x = reader.ReadSingle(), y = reader.ReadSingle(); byte kind = reader.ReadByte();
                        if (sequence != blocks * 256 + i + 1 || start < 0 || end <= start || segment <= 0 || kind > 3 || !FootprintSample.Finite(x) || !FootprintSample.Finite(y) ||
                            kind == 0 && (x < 0 || y < 0 || x >= width || y >= height) || kind != 0 && (x != 0 || y != 0)) throw Invalid();
                        result[i] = new FootprintSample(sequence, start, end, segment, x, y, (FootprintPosition)kind);
                        if (i > 0) Adjacent(result[i - 1], result[i]); else if (sequence == 1 && start != 0) throw Invalid();
                    }
                    if (stream.Position != stream.Length) throw Invalid(); return result;
                }
            }
            catch (EndOfStreamException) { throw Invalid(); }
            catch (DecoderFallbackException) { throw Invalid(); }
            catch (FormatException) { throw Invalid(); }
        }
        internal static void Adjacent(FootprintSample previous, FootprintSample next)
        { if (next.Sequence != previous.Sequence + 1 || next.Start != previous.End || next.Segment < previous.Segment || next.Position != previous.Position && next.Segment == previous.Segment) throw Invalid(); }
        internal static InvalidDataException Invalid() { return new InvalidDataException("footprint-format-or-identity-invalid"); }
    }
}
