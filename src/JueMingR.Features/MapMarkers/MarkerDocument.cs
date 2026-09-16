using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Features.Text;

namespace JueMingR.Features.MapMarkers
{
    public static class MarkerName
    {
        public const int MaximumElements = 20;
        public const string LimitMessage = "名称最多 10 个汉字或 20 个英文字母，混合输入按相同长度计算。";
        public static bool IsValid(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length > MaximumElements * VisibleTextBoundary.MaximumElementUnits || value.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0) return false;
            try
            {
                // Count complete visible elements, never UTF-16 units. ASCII
                // letters/digits/punctuation use half the Chinese-name budget;
                // all other clusters (including emoji) retain whole boundaries.
                int units = 0; var boundaries = TextElements.Boundaries(value);
                for (int i = 0; i < boundaries.Length - 1; i++)
                    units += boundaries[i + 1] - boundaries[i] == 1 && value[boundaries[i]] <= 0x7f ? 1 : 2;
                return units <= MaximumElements;
            }
            catch (ArgumentException) { return false; }
        }
        public static string Normalize(string value, DateTime localTime)
        { value = (value ?? "").Trim(); if (value.Length == 0) value = localTime.ToString("yyMMddHHmm", CultureInfo.InvariantCulture); if (!IsValid(value)) throw new ArgumentException("marker-name-invalid"); return value; }
    }
    public sealed class MarkerRecord
    {
        public MarkerRecord(string id, double x, double y, int icon, string name)
        {
            Guid parsed; if (id == null || id.Length != 32 || !Guid.TryParseExact(id, "N", out parsed) || id != id.ToLowerInvariant()) throw new ArgumentException("marker-id-invalid");
            if (Double.IsNaN(x) || Double.IsInfinity(x) || Double.IsNaN(y) || Double.IsInfinity(y) || x < 0 || y < 0) throw new ArgumentException("marker-point-invalid");
            if (!IsIcon(icon) || !MarkerName.IsValid(name)) throw new ArgumentException("marker-content-invalid");
            Id = id; X = x; Y = y; Icon = icon; Name = name;
        }
        public string Id { get; }
        public double X { get; }
        public double Y { get; }
        public int Icon { get; }
        public string Name { get; }
        public static bool IsIcon(int value) { return value == 8 || value == 48 || value == 50 || value == 224 || value == 171 || value == 393 || value == 966 || value == 29; }
    }
    public sealed class MarkerDocument
    {
        public const int MaximumNew = 120;
        public MarkerDocument(string pair, int width, int height, long revision, IEnumerable<MarkerRecord> records)
        {
            ValidatePair(pair);
            if (width <= 0 || height <= 0 || width > 20000 || height > 20000 || revision < 0 || records == null) throw new ArgumentException("marker-document-invalid");
            var copy = new List<MarkerRecord>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in records)
            {
                if (record == null || record.X >= width || record.Y >= height || !ids.Add(record.Id) || copy.Count == 4096) throw new ArgumentException("marker-records-invalid");
                copy.Add(record);
            }
            Pair = pair; Width = width; Height = height; Revision = revision; Records = copy.AsReadOnly();
        }
        public string Pair { get; }
        public int Width { get; }
        public int Height { get; }
        public long Revision { get; }
        public IReadOnlyList<MarkerRecord> Records { get; }
        public bool IsReadOnly { get { return Records.Count > MaximumNew || Revision == Int64.MaxValue; } }
        public static void ValidatePair(string pair)
        { if (pair == null || pair.Length != 64) throw new ArgumentException("pair-invalid"); foreach (char c in pair) if (!(c >= 'a' && c <= 'f' || c >= '0' && c <= '9')) throw new ArgumentException("pair-invalid"); }
    }
}
