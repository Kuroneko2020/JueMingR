using System;
using System.Collections.Generic;
using System.Text;
using JueMingR.Features.MapMarkers;

namespace JueMingR.ArchitectureTests
{
    internal static class MapAssetChecks
    {
        internal static void Check(List<string> failures)
        {
            string pair = new string('a', 64);
            var first = new MarkerRecord("00000000000000000000000000000001", 120.12345678901234, 45.987654321, 8, "👩‍👩‍👦你好");
            var doc = new MarkerDocument(pair, 4200, 1200, 1, new[] { first });
            var restored = MarkerCodec.Decode(MarkerCodec.Encode(doc), pair, 4200, 1200);
            Require(restored.Records[0].X == first.X && restored.Records[0].Y == first.Y && restored.Records[0].Name == first.Name, "Marker coordinates and complete name survive exact persistence.", failures);
            Reject(() => MarkerCodec.Decode(MarkerCodec.Encode(doc), new string('b', 64), 4200, 1200), "pair mismatch", failures);
            Reject(() => new MarkerDocument(pair, 4200, 1200, 0, new[] { first, first }), "duplicate stable ID", failures);
            Reject(() => new MarkerRecord(Guid.NewGuid().ToString("N"), Double.NaN, 1, 8, "x"), "NaN", failures);
            Reject(() => new MarkerDocument(pair, 4200, 1200, 0, new[] { new MarkerRecord(Guid.NewGuid().ToString("N"), 4200, 1, 8, "x") }), "outside world", failures);
            Reject(() => new MarkerRecord(Guid.NewGuid().ToString("N"), 1, 1, 9, "x"), "unknown icon", failures);
            Reject(() => new MarkerRecord(Guid.NewGuid().ToString("N"), 1, 1, 8, "12345678901"), "eleventh element", failures);
            Reject(() => MarkerCodec.Decode(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(MarkerCodec.Encode(doc)).Replace("\"version\":1", "\"version\":2")), pair, 4200, 1200), "future schema", failures);
            Require(MarkerName.Normalize("", new DateTime(2026, 9, 16, 21, 32, 0)) == "2609162132", "Default uses local wall time without timezone conversion.", failures);
            var many = new List<MarkerRecord>(); for (int i = 0; i < 121; i++) many.Add(new MarkerRecord(i.ToString("x32"), i + .25, 1.5, 8, "同名"));
            var oversized = MarkerCodec.Decode(MarkerCodec.Encode(new MarkerDocument(pair, 4200, 1200, 1, many)), pair, 4200, 1200);
            Require(oversized.Records.Count == 121 && oversized.IsReadOnly, "Legal historical excess is retained and protected, never truncated.", failures);
        }
        private static void Require(bool condition, string message, List<string> failures) { if (!condition) failures.Add(message); }
        private static void Reject(Action action, string reason, List<string> failures)
        { try { action(); failures.Add("Marker accepted " + reason); } catch (ArgumentException) { } catch (System.IO.InvalidDataException) { } }
    }
}
