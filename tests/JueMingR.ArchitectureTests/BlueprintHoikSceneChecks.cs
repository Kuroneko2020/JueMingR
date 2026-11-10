using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class BlueprintHoikSceneChecks
    {
        private const string Scope = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const int Width = 1000, Height = 500;
        internal static void Check(IList<string> failures)
        {
            string area = Path.Combine(Path.GetTempPath(), "JMR-hoik-scene-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(area);
            try
            {
                Formats(); Archives(area, false); Archives(area, true); MissingPrimaryRecovery(area);
                Console.WriteLine("BlueprintHoikScene PASS: literal v1/v2 decode; exact v3 pending including final ObjectId; duplicate/out-of-world/future rejection; same-document v1->v2->v3 and direct v2->v3 exact source archives; schema2-only recovery protects missing primary.");
            }
            catch (Exception error) { failures.Add("BlueprintHoikScene " + error + "; retained=" + area); }
        }
        private static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static CaptureCell First()
        { return new CaptureCell { X = 7, Y = 9, ObjectId = 31, Layers = CaptureLayer.Tile, TileType = 1, Slope = 3, TilePaint = 18, Inactive = true, InvisibleTile = true, FullbrightTile = true }; }
        private static CaptureCell Second()
        { return new CaptureCell { X = 8, Y = 9, ObjectId = 32, Layers = CaptureLayer.Tile, TileType = 1, HalfBrick = true }; }
        private static void Formats()
        {
            byte[] v1 = Literal(1), v2 = Literal(2);
            var one = BlueprintSceneCodec.Decode(v1, Scope, Width, Height); var two = BlueprintSceneCodec.Decode(v2, Scope, Width, Height);
            Need(one.SourceVersion == 1 && !one.Records[0].MirroredHorizontally && one.Records[0].HoikPending.Count == 0, "literal v1 reads with normal pose and no invented pending");
            Need(two.SourceVersion == 2 && two.Records[0].MirroredHorizontally && two.Records[0].HoikPending.Count == 0, "literal v2 reads with pose and no invented pending");
            byte[] encoded = BlueprintSceneCodec.Encode(Pending(two));
            Need(BitConverter.ToInt32(encoded, 8) == 3, "only v3 writer after a real pending mutation");
            var restored = BlueprintSceneCodec.Decode(encoded, Scope, Width, Height); var record = restored.Records.Single();
            Need(restored.SourceVersion == 3 && restored.Revision == 11 && record.MirroredHorizontally && record.Order == 2 && record.OriginX == 100 && record.OriginY == 120 && record.Template.AssetId == two.Records[0].Template.AssetId && record.Template.ContentRevision == two.Records[0].Template.ContentRevision && record.Template.ContentHash == two.Records[0].Template.ContentHash, "pending cannot change template/pose/order/world origin");
            Need(record.HoikPending.Count == 2, "two distinct pending facts persist");
            Same(First(), record.HoikPending[0].Expected); Same(Second(), record.HoikPending[1].Expected);
            var masked = new CaptureCell { X = 3, Y = 1, Layers = CaptureLayer.Wall, WallType = 7 };
            Need(!record.Mask.Apply(masked).HasContent, "existing source-local permanent mask survives v3");
            bool duplicateRejected = false;
            try { new BlueprintSceneRecord(record.Template, 100, 120, 2, record.Mask, true, new[] { new BlueprintHoikPending(First()), new BlueprintHoikPending(First()) }); }
            catch (ArgumentException) { duplicateRejected = true; }
            Need(duplicateRejected, "constructor rejects duplicate pending coordinates");
            Protected(Literal(3, new[] { First(), First() }), PreferenceStatus.Invalid);
            CaptureCell outside = First(); outside.X = 900;
            Protected(Literal(3, new[] { outside }), PreferenceStatus.Invalid);
            byte[] future = v2.Take(12).ToArray(); Buffer.BlockCopy(BitConverter.GetBytes(4), 0, future, 8, 4);
            Protected(future, PreferenceStatus.UnsupportedVersion);
        }
        private static BlueprintSceneDocument Pending(BlueprintSceneDocument source)
        {
            BlueprintSceneRecord old = source.Records[0];
            return new BlueprintSceneDocument(Scope, Width, Height, 11, new[] { new BlueprintSceneRecord(old.Template, old.OriginX, old.OriginY, old.Order, old.Mask, old.MirroredHorizontally,
                new[] { new BlueprintHoikPending(First()), new BlueprintHoikPending(Second()) }) });
        }
        private static void Same(CaptureCell expected, CaptureCell actual)
        {
            Need(actual.X == expected.X && actual.Y == expected.Y && actual.ObjectId == expected.ObjectId && actual.Layers == CaptureLayer.Tile && actual.TileType == expected.TileType && actual.Slope == expected.Slope && actual.TilePaint == expected.TilePaint && actual.HalfBrick == expected.HalfBrick && actual.Inactive == expected.Inactive && actual.InvisibleTile == expected.InvisibleTile && actual.FullbrightTile == expected.FullbrightTile,
                "relative intermediate foreground shape/paint/coatings/actuation and final template ObjectId round trip exactly");
        }
        private static void MissingPrimaryRecovery(string area)
        {
            string directory = Path.Combine(area, "schema2-only"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "scene.bin"); byte[] archive = Literal(2);
            File.WriteAllBytes(path + ".schema2-original", archive);
            // Recreate the real Host storage declaration in a fresh owner. An
            // old default schema1 path alone cannot discover schema2 recovery.
            using (var document = new AtomicFileDocument(path, Int32.MaxValue, true, ".schema1-original", new[] { ".schema2-original" }))
            {
                var loaded = document.Read();
                Need(loaded.Status == PreferenceReadStatus.IoFailure && loaded.Error == "missing-document-with-recovery-material", "schema2-only recovery is protected instead of reliable Missing");
                byte[] empty = BlueprintSceneCodec.Encode(new BlueprintSceneDocument(Scope, Width, Height, 0, Array.Empty<BlueprintSceneRecord>()));
                var written = document.Write(loaded.Identity, empty);
                Need(written.Status != PreferenceWriteStatus.Saved && written.IsProtected && !File.Exists(path) && File.ReadAllBytes(path + ".schema2-original").SequenceEqual(archive),
                    "protected missing primary cannot publish a new empty scene or change original schema2 bytes");
            }
        }
        private static void Archives(string area, bool directV2)
        {
            string directory = Path.Combine(area, directV2 ? "direct-v2" : "v1-v2-v3"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "scene.bin"); byte[] v1 = Literal(1), v2 = Literal(2);
            File.WriteAllBytes(path, directV2 ? v2 : v1);
            // Same mechanical document instance crosses both version transitions.
            // The second retention must follow the current accepted bytes, not
            // its initial Read snapshot or the already archived schema1 source.
            using (var document = new AtomicFileDocument(path, Int32.MaxValue, true))
            {
                var loaded = document.Read(); Need(loaded.Status == PreferenceReadStatus.Loaded, "real old scene is loaded");
                var decoded = BlueprintSceneCodec.Decode(loaded.Contents, Scope, Width, Height);
                Need(decoded.SourceVersion == (directV2 ? 2 : 1) && !File.Exists(path + ".schema1-original") && !File.Exists(path + ".schema2-original"), "read alone never upgrades or invents archives");
                string identity = loaded.Identity;
                if (!directV2)
                {
                    document.RetainLoadedSource(".schema1-original");
                    var written = document.Write(identity, v2); Need(written.Status == PreferenceWriteStatus.Saved, "historical v1 to literal v2 reliable save"); identity = written.Identity;
                    Need(File.ReadAllBytes(path + ".schema1-original").SequenceEqual(v1), "schema1 archive contains exact original bytes");
                }
                byte[] schema1Hash = directV2 ? null : Hash(File.ReadAllBytes(path + ".schema1-original"));
                var current = BlueprintSceneCodec.Decode(File.ReadAllBytes(path), Scope, Width, Height);
                Need(current.SourceVersion == 2, "current source is known v2 before v3 mutation");
                document.RetainLoadedSource(".schema2-original"); byte[] v3 = BlueprintSceneCodec.Encode(Pending(current));
                var saved = document.Write(identity, v3); Need(saved.Status == PreferenceWriteStatus.Saved, "real v2 to v3 reliable save");
                Need(File.ReadAllBytes(path + ".schema2-original").SequenceEqual(v2), "schema2 archive contains exact displaced source, never reencoding");
                byte[] schema2Hash = Hash(File.ReadAllBytes(path + ".schema2-original"));
                Need(BlueprintSceneCodec.Decode(File.ReadAllBytes(path), Scope, Width, Height).Records[0].HoikPending.Count == 2, "real reliable primary contains pending");
                // A later ordinary save rotates .bak, never either source archive.
                var next = new BlueprintSceneDocument(Scope, Width, Height, 12, Pending(current).Records);
                Need(document.Write(saved.Identity, BlueprintSceneCodec.Encode(next)).Status == PreferenceWriteStatus.Saved, "normal subsequent v3 write succeeds");
                Need(Hash(File.ReadAllBytes(path + ".schema2-original")).SequenceEqual(schema2Hash) && (directV2 || Hash(File.ReadAllBytes(path + ".schema1-original")).SequenceEqual(schema1Hash)), "both source archive hashes survive normal backup rotation");
                if (directV2) Need(!File.Exists(path + ".schema1-original"), "direct v2 upgrade never invents schema1 history");
            }
        }
        // Independently written original schemas. Expected old bytes never come
        // from the new encoder or from stripping fields out of its v3 result.
        private static byte[] Literal(int version, IReadOnlyList<CaptureCell> pending = null)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(0x3130435342524d4aUL); writer.Write(version); writer.Write(Enumerable.Repeat((byte)0xbb, 32).ToArray()); writer.Write(Width); writer.Write(Height); writer.Write(10L); writer.Write(1);
                    writer.Write(new Guid("11111111-2222-3333-4444-555555555555").ToByteArray()); writer.Write(new Guid("66666666-7777-8888-9999-aaaaaaaaaaaa").ToByteArray()); writer.Write(Enumerable.Repeat((byte)0x31, 32).ToArray());
                    writer.Write(100); writer.Write(120); writer.Write(2L); if (version >= 2) writer.Write((byte)1);
                    writer.Write(1L); writer.Write(1L); writer.Write(1); writer.Write((1L << 32) | 3L); writer.Write((ushort)2); writer.Write(0);
                    if (version >= 3)
                    {
                        writer.Write(pending?.Count ?? 0);
                        if (pending != null) foreach (CaptureCell cell in pending)
                        { writer.Write(cell.X); writer.Write(cell.Y); writer.Write(cell.TileType); writer.Write(cell.Slope); writer.Write(cell.TilePaint); writer.Write((byte)((cell.HalfBrick ? 1 : 0) | (cell.Inactive ? 2 : 0) | (cell.InvisibleTile ? 4 : 0) | (cell.FullbrightTile ? 8 : 0))); writer.Write(cell.ObjectId); }
                    }
                }
                byte[] bytes = stream.ToArray(), hash = Hash(bytes); stream.Write(hash, 0, hash.Length); return stream.ToArray();
            }
        }
        private static byte[] Hash(byte[] bytes) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }
        private static void Protected(byte[] bytes, PreferenceStatus status)
        {
            try { BlueprintSceneCodec.Decode(bytes, Scope, Width, Height); throw new InvalidOperationException("invalid/future scene was accepted"); }
            catch (PreferenceFormatException error) { Need(error.Status == status, "precise scene protected status"); }
        }
    }
}
