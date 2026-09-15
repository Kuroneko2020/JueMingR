using System;
using System.Globalization;
using System.IO;
using System.Text;
using JueMingR.Platform.DeathHistory;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.DeathHistory
{
    internal sealed class DeathHeader
    {
        internal string Id, Text, Previous, PreviousPosition;
        internal long Occurrence;
        internal short Offset;
        internal bool Position;
        internal float X, Y;
        internal DeathMarker Marker { get { return new DeathMarker(Id, Occurrence, X, Y); } }
    }
    internal static class DeathArchiveCodec
    {
        internal const int MaximumText = 262144;
        internal static bool PageId(string id)
        { if (id == null || id.Length != 64) return false; foreach (char c in id) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false; return true; }
        internal static void Ref(string id) { if (id != "" && !PageId(id)) throw PreferenceJson.Invalid(); }
        internal static BinaryWriter Writer(MemoryStream stream, int kind, int version = 1)
        { var writer = new BinaryWriter(stream, Encoding.UTF8, true); writer.Write(0x4A524448); writer.Write(version); writer.Write(kind); return writer; }
        internal static BinaryReader Reader(byte[] bytes, int kind)
        { int version; return Reader(bytes, kind, 1, out version); }
        private static BinaryReader Reader(byte[] bytes, int kind, int maximumVersion, out int version)
        {
            if (bytes == null || bytes.Length < 12 || bytes.Length > 1048576) throw PreferenceJson.Invalid();
            var reader = new BinaryReader(new MemoryStream(bytes, false), Encoding.UTF8);
            if (reader.ReadInt32() != 0x4A524448) { reader.Dispose(); throw PreferenceJson.Invalid(); }
            version = reader.ReadInt32();
            if (version < 1 || version > maximumVersion) { reader.Dispose(); throw new PreferenceFormatException(PreferenceStatus.UnsupportedVersion, "unsupported-death-page-version"); }
            if (reader.ReadInt32() != kind) { reader.Dispose(); throw PreferenceJson.Invalid(); }
            return reader;
        }
        // Preserve original UTF-16 code units. Invalid surrogates and markup
        // are sanitized only by the plain-text display projection.
        internal static void String(BinaryWriter writer, string value)
        { writer.Write(value.Length); foreach (char c in value) writer.Write((ushort)c); }
        internal static string String(BinaryReader reader, int max)
        {
            int length = reader.ReadInt32(); if (length < 0 || length > max || length * 2L > reader.BaseStream.Length - reader.BaseStream.Position) throw PreferenceJson.Invalid();
            var chars = new char[length]; for (int i = 0; i < length; i++) chars[i] = (char)reader.ReadUInt16(); return new string(chars);
        }
        internal static void End(BinaryReader reader) { if (reader.BaseStream.Position != reader.BaseStream.Length) throw PreferenceJson.Invalid(); }
        internal static byte[] Text(string reason, string directCause)
        {
            if (reason != null && reason.Length > MaximumText) throw new ArgumentException("death-reason-too-long");
            ValidateCause(directCause);
            using (var stream = new MemoryStream()) using (var writer = Writer(stream, 1, 2))
            {
                writer.Write(reason != null); if (reason != null) String(writer, reason);
                writer.Write(directCause != null); if (directCause != null) String(writer, directCause);
                writer.Flush(); return stream.ToArray();
            }
        }
        internal static string Text(byte[] bytes)
        { string directCause; return Text(bytes, out directCause); }
        internal static string Text(byte[] bytes, out string directCause)
        {
            // Only text pages advance to v2. Old immutable pages are not
            // rewritten, and header/index future versions remain protected.
            int version;
            using (var reader = Reader(bytes, 1, 2, out version))
            {
                string text = reader.ReadBoolean() ? String(reader, MaximumText) : null;
                directCause = version == 2 && reader.ReadBoolean() ? String(reader, DeathFact.MaximumDirectCauseLength) : null;
                ValidateCause(directCause); End(reader); return text;
            }
        }
        private static void ValidateCause(string value)
        { if (value != null && (System.String.IsNullOrWhiteSpace(value) || value.Length > DeathFact.MaximumDirectCauseLength)) throw PreferenceJson.Invalid(); }
        internal static byte[] Header(DeathHeader value)
        {
            using (var stream = new MemoryStream()) using (var writer = Writer(stream, 2))
            {
                String(writer, value.Id); String(writer, value.Text); String(writer, value.Previous); String(writer, value.PreviousPosition);
                writer.Write(value.Occurrence); writer.Write(value.Offset); writer.Write(value.Position); writer.Write(value.X); writer.Write(value.Y);
                writer.Flush(); return stream.ToArray();
            }
        }
        internal static DeathHeader Header(byte[] bytes)
        {
            using (var reader = Reader(bytes, 2))
            {
                var value = new DeathHeader { Id = String(reader, 52), Text = String(reader, 64), Previous = String(reader, 64), PreviousPosition = String(reader, 64),
                    Occurrence = reader.ReadInt64(), Offset = reader.ReadInt16(), Position = reader.ReadBoolean(), X = reader.ReadSingle(), Y = reader.ReadSingle() };
                DeathEventId.Time(value.Id, TimeSpan.FromMinutes(value.Offset)); Ref(value.Text); Ref(value.Previous); Ref(value.PreviousPosition);
                if (value.Text == "" || value.Occurrence <= 0 || value.Position && (!Finite(value.X) || !Finite(value.Y) || value.X < 0 || value.Y < 0 || value.X > 16 * 8400 || value.Y > 16 * 2400)) throw PreferenceJson.Invalid();
                End(reader); return value;
            }
        }
        private static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
        internal static byte[] Root(string pair, long count, string tree, string last, string position)
        {
            return Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.Deaths\",\"version\":1,\"pair\":\"" + pair + "\",\"count\":\"" + count.ToString(CultureInfo.InvariantCulture) +
                "\",\"tree\":\"" + tree + "\",\"last\":\"" + last + "\",\"position\":\"" + position + "\"}");
        }
        internal static void Root(byte[] bytes, string pair, out long count, out string tree, out string last, out string position)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.Deaths", "format", "version", "pair", "count", "tree", "last", "position");
            if (PreferenceJson.Required(root, "pair", "string").Value != pair || !Int64.TryParse(PreferenceJson.Required(root, "count", "string").Value, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 0) throw PreferenceJson.Invalid();
            tree = PreferenceJson.Required(root, "tree", "string").Value; last = PreferenceJson.Required(root, "last", "string").Value; position = PreferenceJson.Required(root, "position", "string").Value;
            Ref(tree); Ref(last); Ref(position); if ((count == 0) != (tree == "") || (count == 0) != (last == "") || count == 0 && position != "") throw PreferenceJson.Invalid();
        }
    }
}
