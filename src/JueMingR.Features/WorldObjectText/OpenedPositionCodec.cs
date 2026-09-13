using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;

namespace JueMingR.Features.WorldObjectText
{
    public sealed class OpenedPositionCodec
    {
        public const int MaximumBytes = 8 * 1024 * 1024;
        public static bool IsPairKey(string key)
        { if (key == null || key.Length != 64) return false; foreach (char c in key) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false; return true; }
        public static string PairKey(string playerStorageIdentity, Guid world)
        {
            if (String.IsNullOrWhiteSpace(playerStorageIdentity) || playerStorageIdentity.Length > 32768 || world == Guid.Empty) throw new ArgumentException("opened-pair-identity-unavailable");
            // Called on identity admission, never on every update or duplicate
            // open. Only this digest crosses the storage filename/schema boundary.
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(new UTF8Encoding(false, true).GetBytes("opened-v1\0" + playerStorageIdentity + "\0" + world.ToString("N")));
                var key = new StringBuilder(64); foreach (byte value in hash) key.Append(value.ToString("x2", CultureInfo.InvariantCulture)); return key.ToString();
            }
        }
        public OpenedPositionIndex Decode(byte[] bytes, string pair)
        {
            if (!IsPairKey(pair)) throw new ArgumentException("invalid-opened-pair");
            try
            {
                if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes) throw PreferenceJson.Invalid();
                new UTF8Encoding(false, true).GetCharCount(bytes);
                var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 5, MaxStringContentLength = 128,
                    MaxArrayLength = OpenedPositionIndex.MaximumPositions, MaxBytesPerRead = 4096, MaxNameTableCharCount = 1024 };
                // Stream rows instead of constructing a millions-node XElement
                // tree. Quotas and row count bound hostile small-node expansion.
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, quotas))
                {
                    reader.MoveToContent(); Typed(reader, "object"); reader.ReadStartElement("root");
                    int fields = 0; var result = new OpenedPositionIndex();
                    while (reader.MoveToContent() == XmlNodeType.Element)
                    {
                        string name = reader.LocalName;
                        if (name == "format") { Once(ref fields, 1); if (Value(reader, "string") != "JueMingR.OpenedContainers") throw PreferenceJson.Invalid(); }
                        else if (name == "version") { Once(ref fields, 2); if (Number(reader) != 1) throw new PreferenceFormatException(PreferenceStatus.UnsupportedVersion, "unsupported-opened-version"); }
                        else if (name == "pair") { Once(ref fields, 4); if (Value(reader, "string") != pair) throw PreferenceJson.Invalid(); }
                        else if (name == "positions")
                        {
                            Once(ref fields, 8); Typed(reader, "array");
                            if (reader.IsEmptyElement) { reader.Read(); continue; }
                            reader.ReadStartElement();
                            while (reader.MoveToContent() == XmlNodeType.Element)
                            {
                                if (reader.LocalName != "item") throw PreferenceJson.Invalid(); Typed(reader, "object");
                                reader.ReadStartElement(); int coordinates = 0, x = -1, y = -1;
                                while (reader.MoveToContent() == XmlNodeType.Element)
                                {
                                    if (reader.LocalName == "x") { Once(ref coordinates, 1); x = Number(reader); }
                                    else if (reader.LocalName == "y") { Once(ref coordinates, 2); y = Number(reader); }
                                    else throw Unknown();
                                }
                                reader.ReadEndElement();
                                if (coordinates != 3 || !result.Add(WorldObject.PositionKey(x, y))) throw PreferenceJson.Invalid();
                            }
                            reader.ReadEndElement();
                        }
                        else throw Unknown();
                    }
                    reader.ReadEndElement();
                    if (fields != 15 || reader.MoveToContent() != XmlNodeType.None) throw PreferenceJson.Invalid();
                    return result;
                }
            }
            catch (PreferenceFormatException) { throw; }
            catch (Exception e) when (e is XmlException || e is ArgumentException || e is InvalidOperationException) { throw PreferenceJson.Invalid(); }
        }
        public byte[] Encode(string pair, OpenedPositionIndex index)
        {
            if (!IsPairKey(pair) || index == null) throw new ArgumentException("invalid-opened-document");
            // Only the background storage owner calls this H-sized operation.
            long[] keys = index.All.ToArray(); Array.Sort(keys);
            var json = new StringBuilder("{\"format\":\"JueMingR.OpenedContainers\",\"version\":1,\"pair\":\"");
            json.Append(pair).Append("\",\"positions\":[");
            for (int i = 0; i < keys.Length; i++)
            {
                if (i != 0) json.Append(','); json.Append("{\"x\":").Append(((int)(keys[i] >> 32)).ToString(CultureInfo.InvariantCulture))
                    .Append(",\"y\":").Append(((int)keys[i]).ToString(CultureInfo.InvariantCulture)).Append('}');
                if (json.Length > MaximumBytes - 2) throw new InvalidOperationException("opened-document-too-large");
            }
            return new UTF8Encoding(false, true).GetBytes(json.Append("]}").ToString());
        }
        private static void Once(ref int fields, int bit) { if ((fields & bit) != 0) throw PreferenceJson.Invalid(); fields |= bit; }
        private static void Typed(XmlReader reader, string type)
        {
            if (reader.NodeType != XmlNodeType.Element || reader.GetAttribute("type") != type) throw PreferenceJson.Invalid();
            if (reader.MoveToFirstAttribute())
            { do { if (reader.Name != "type") throw Unknown(); } while (reader.MoveToNextAttribute()); reader.MoveToElement(); }
        }
        private static string Value(XmlReader reader, string type) { Typed(reader, type); return reader.ReadElementContentAsString(); }
        private static int Number(XmlReader reader)
        { int value; if (!Int32.TryParse(Value(reader, "number"), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)) throw PreferenceJson.Invalid(); return value; }
        private static PreferenceFormatException Unknown() { return new PreferenceFormatException(PreferenceStatus.UnknownFields, "unknown-opened-field"); }
    }
}
