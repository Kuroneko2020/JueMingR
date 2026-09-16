using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JueMingR.Features.Footprints;
using JueMingR.Infrastructure.Footprints;
using JueMingR.Platform.Footprints;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class FootprintFileChecks
    {
        internal static void Check(IList<string> failures)
        {
            string area = Path.Combine(Path.GetTempPath(), "JueMingR-footprints-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(area);
            try { RoundTrip(area); Corruption(area); ClearRecovery(area); GrowingHistory(area); ClearBoundaries(area); }
            catch (Exception e) { failures.Add("footprints isolated files: " + e); }
            finally { Directory.Delete(area, true); } // Exclusively generated test root; never installation data.
        }
        private static void RoundTrip(string area)
        {
            string path = Path.Combine(area, "roundtrip"); string pair = new string('a', 64);
            using (var files = new CountedFiles(path, pair))
            {
                var archive = new FootprintArchive(files, pair, 8400, 2400); archive.Load();
                var recorder = new FootprintRecorder(archive.Count, archive.End, archive.Segment, (items, count) => { archive.Append(items, count); return true; }, () => 0);
                for (int i = 0; i < 2050; i++) recorder.Observe(100 + i / 1000f, 200, FootprintPosition.Valid, i == 1024);
                recorder.Observe(102.049f, 200, FootprintPosition.Valid, false, 43416000); recorder.Flush();
                FootprintCoreChecks.Require(archive.Count == 2050 && archive.End == 43418050, "all movements and 201h stay persisted");
                var points = archive.Query(512, 520).Samples;
                FootprintCoreChecks.Require(points.Any(p => p.Sequence == 256) && points.Any(p => p.Sequence == 257), "cross-block adjacency is available");
                for (int i = 1; i < points.Length; i++) FootprintCoreChecks.Require(points[i].Follows(points[i - 1]), "block boundaries never cut or invent adjacency");
                var frozen = archive.Query(900, 400); long reads = files.BlockReads;
                for (int i = 0; i < 100; i++) archive.Query(900, 400);
                FootprintCoreChecks.Require(files.BlockReads == reads && frozen.Samples.Length <= 400, "bounded immutable cache avoids repeated reads of fixed history");
            }
            using (var files = new CountedFiles(path, pair))
            {
                var archive = new FootprintArchive(files, pair, 8400, 2400); archive.Load();
                FootprintCoreChecks.Require(archive.Count == 2050 && archive.End == 43418050, "reopen retains full simulation time");
                FootprintCoreChecks.Require(archive.Query(1, 20).Samples[0].Sequence == 1, "oldest time remains reachable");
                var late = archive.Query(archive.End, 20).Samples;
                FootprintCoreChecks.Require(late[late.Length - 1].End == archive.End, "long stay remains reachable at latest");
            }
        }
        private static void Corruption(string area)
        {
            string path = Path.Combine(area, "corrupt"); string pair = new string('b', 64); string generation;
            using (var files = new CountedFiles(path, pair))
            { var a = new FootprintArchive(files, pair, 100, 100); a.Load(); a.Append(new[] { new FootprintSample(1, 0, 1, 1, 2, 3, FootprintPosition.Valid) }, 1); generation = a.Generation; }
            string root = Path.Combine(path, pair, generation, "root.bin"); byte[] before = File.ReadAllBytes(root); before[0] ^= 1; File.WriteAllBytes(root, before);
            bool rejected = false;
            using (var files = new CountedFiles(path, pair))
            { try { new FootprintArchive(files, pair, 100, 100).Load(); } catch (InvalidDataException) { rejected = true; } }
            FootprintCoreChecks.Require(rejected && before.SequenceEqual(File.ReadAllBytes(root)), "corrupt root stays byte-for-byte protected");
        }
        private static void ClearRecovery(string area)
        {
            string path = Path.Combine(area, "clear"); string pair = new string('c', 64); string old, operation = Guid.NewGuid().ToString("N");
            string sentinel = Path.Combine(path, "other-feature.bin"); Directory.CreateDirectory(path); File.WriteAllText(sentinel, "do not touch");
            using (var files = new CountedFiles(path, pair))
            {
                var a = new FootprintArchive(files, pair, 100, 100); a.Load();
                for (int i = 0; i < 600; i++) a.Append(new[] { new FootprintSample(i + 1, i, i + 1, 1, 2, 3, FootprintPosition.Valid) }, 1);
                old = a.Generation; a.BeginClear(old, operation);
                // A real filesystem failure after durable intent must not claim success.
                File.WriteAllText(Path.Combine(path, pair, old, "unknown.bin"), "external");
                bool failed = false; try { while (!a.ClearStep(1)) { } } catch (InvalidDataException) { failed = true; }
                FootprintCoreChecks.Require(failed && a.Clearing && a.Generation == old, "partial clear remains bound to old generation");
                File.Delete(Path.Combine(path, pair, old, "unknown.bin"));
            }
            using (var files = new CountedFiles(path, pair))
            {
                var a = new FootprintArchive(files, pair, 100, 100); a.Load();
                FootprintCoreChecks.Require(a.Clearing, "restart discovers exact durable intent");
                while (!a.ClearStep(1)) { }
                FootprintCoreChecks.Require(a.Generation != old && a.Count == 0 && !Directory.Exists(Path.Combine(path, pair, old)), "clear removes all old body and recovery files");
                a.Append(new[] { new FootprintSample(1, 0, 1, 1, 2, 3, FootprintPosition.Valid) }, 1);
                bool stale = false; try { a.BeginClear(old, operation); } catch (InvalidOperationException) { stale = true; }
                FootprintCoreChecks.Require(stale && a.Count == 1 && File.ReadAllText(sentinel) == "do not touch", "old retry cannot delete new history or adjacent domains");
            }
        }
        private static void GrowingHistory(string area)
        {
            string path = Path.Combine(area, "growth"), pair = new string('5', 64);
            using (var files = new CountedFiles(path, pair))
            {
                var archive = new FootprintArchive(files, pair, 100, 100); archive.Load(); long priorBytes = 0;
                for (int n = 0; n < 129; n++)
                {
                    var batch = new FootprintSample[256];
                    for (int i = 0; i < batch.Length; i++) { long p = n * 256 + i + 1; batch[i] = new FootprintSample(p, p - 1, p, 1, i % 90, n % 90, FootprintPosition.Valid); }
                    archive.Append(batch, batch.Length);
                    if (n > 0) FootprintCoreChecks.Require(files.BlockBytesSubmitted - priorBytes <= 16384, "append submits one bounded block regardless of total N");
                    priorBytes = files.BlockBytesSubmitted;
                }
                FootprintCoreChecks.Require(archive.Count == 33024 && archive.Query(0).Samples[0].Sequence == 1 && archive.Query(archive.End).Partial, "N=33024 oldest and latest remain reachable with bounded partial query");
                var middle = archive.Query(16000); long reads = files.BlockReads;
                for (int i = 0; i < 2000; i++) archive.Query(16000);
                FootprintCoreChecks.Require(middle.Samples.Length == 8192 && files.BlockReads == reads, "2000 fixed queries use bounded cache without full-history disk scans");
            }
            using (var files = new CountedFiles(path, pair))
            {
                var archive = new FootprintArchive(files, pair, 100, 100); long previous = 0; int steps = 0;
                while (!archive.LoadStep()) { FootprintCoreChecks.Require(files.BlockReads - previous <= 8, "first recovery decodes at most eight blocks per step"); previous = files.BlockReads; steps++; }
                FootprintCoreChecks.Require(steps > 16 && archive.Count == 33024, "large first recovery is cancellable and complete");
            }
            string catalog = Path.Combine(path, pair, "catalog.json"), before = File.ReadAllText(catalog); string future = before.Replace("\"version\":1", "\"version\":2"); File.WriteAllText(catalog, future);
            bool rejected = false; using (var files = new CountedFiles(path, pair)) { try { new FootprintArchive(files, pair, 100, 100).Load(); } catch (InvalidDataException) { rejected = true; } }
            FootprintCoreChecks.Require(rejected && File.ReadAllText(catalog) == future, "future catalog remains unchanged and unwritable");
        }
        private static void ClearBoundaries(string area)
        {
            string path = Path.Combine(area, "boundaries"), pair = new string('6', 64), other = new string('7', 64);
            using (var neighbor = new FileFootprintArchive(path, other))
            { var a = new FootprintArchive(neighbor, other, 100, 100); a.Load(); a.Append(new[] { new FootprintSample(1, 0, 1, 1, 9, 8, FootprintPosition.Valid) }, 1); }
            var protectedFiles = Directory.GetFiles(Path.Combine(path, other), "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
            foreach (string domain in new[] { "markers", "deaths", "notes", "hotkeys", "original.map", "original.plr", "original.wld" })
            { string p = Path.Combine(path, domain); File.WriteAllBytes(p, new byte[] { 41, 0, 199, 255 }); protectedFiles.Add(p, File.ReadAllBytes(p)); }
            using (var files = new CountedFiles(path, pair))
            {
                var a = new FootprintArchive(files, pair, 100, 100); a.Load();
                for (int i = 0; i < 600; i++) a.Append(new[] { new FootprintSample(i + 1, i, i + 1, 1, 2, 3, FootprintPosition.Valid) }, 1);
                string generation = a.Generation, target = Path.Combine(path, pair, generation), blocks = Path.Combine(target, "blocks");
                string parked = Path.Combine(area, "owned-test-blocks"), external = Path.Combine(area, "outside-footprints"); Directory.CreateDirectory(external);
                string sentinel = Path.Combine(external, "00000000000000000000.bin"); File.WriteAllText(sentinel, "outside must survive");
                a.BeginClear(generation, Guid.NewGuid().ToString("N"));
                Directory.Move(blocks, parked);
                try
                {
                    // Only GUID-isolated test paths enter this native Windows
                    // junction fixture. No production path or user data is used.
                    var info = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c mklink /J \"" + blocks + "\" \"" + external + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    using (var process = System.Diagnostics.Process.Start(info)) { process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd(); process.WaitForExit(); FootprintCoreChecks.Require(process.ExitCode == 0, "isolated directory junction fixture created"); }
                    bool refused = false; try { a.ClearStep(32); } catch (InvalidDataException) { refused = true; }
                    FootprintCoreChecks.Require(refused && File.ReadAllText(sentinel) == "outside must survive" && a.Generation == generation && a.Clearing, "clear rejects junction before deleting external numeric block");
                }
                finally { if (Directory.Exists(blocks)) Directory.Delete(blocks, false); Directory.Move(parked, blocks); }
                string locked = Directory.GetFiles(blocks)[0];
                using (var handle = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    bool busy = false; try { a.ClearStep(32); } catch (IOException) { busy = true; }
                    FootprintCoreChecks.Require(busy && a.Clearing && a.Generation == generation, "real locked block cannot become a false clear success");
                }
                string root = Path.Combine(target, "root.bin"); File.SetAttributes(root, FileAttributes.ReadOnly);
                try
                {
                    bool denied = false; try { while (!a.ClearStep(32)) { } } catch (UnauthorizedAccessException) { denied = true; }
                    FootprintCoreChecks.Require(denied && a.Clearing && a.Generation == generation, "real delete permission denial preserves partial-clear identity");
                }
                finally { if (File.Exists(root)) File.SetAttributes(root, FileAttributes.Normal); }
                while (!a.ClearStep(32)) { }
                FootprintCoreChecks.Require(!Directory.Exists(target), "old body and all recoverable root files physically removed after retry");
            }
            foreach (var file in protectedFiles) FootprintCoreChecks.Require(File.Exists(file.Key) && file.Value.SequenceEqual(File.ReadAllBytes(file.Key)), "other pair and adjacent domain sentinel unchanged: " + Path.GetFileName(file.Key));
        }
        // Counts actual mechanical port calls while preserving the production
        // filesystem adapter. Submitted bytes are not claimed as physical bytes
        // on idempotent retries. No counters are installed in ordinary Release.
        private sealed class CountedFiles : IFootprintArchiveFiles
        {
            private readonly FileFootprintArchive inner;
            internal long BlockReads, BlockBytesSubmitted;
            internal CountedFiles(string path, string pair) { inner = new FileFootprintArchive(path, pair); }
            public PreferenceReadResult ReadCatalog() { return inner.ReadCatalog(); }
            public PreferenceWriteResult WriteCatalog(string i, byte[] b) { return inner.WriteCatalog(i, b); }
            public PreferenceReadResult OpenRoot(string g) { return inner.OpenRoot(g); }
            public PreferenceWriteResult WriteRoot(string i, byte[] b) { return inner.WriteRoot(i, b); }
            public byte[] ReadBlock(long n) { BlockReads++; return inner.ReadBlock(n); }
            public void CreateBlock(long n, byte[] b) { BlockBytesSubmitted += b.Length; inner.CreateBlock(n, b); }
            public bool ValidateBlockNamesStep(long n, int budget) { return inner.ValidateBlockNamesStep(n, budget); }
            public bool DeleteGenerationStep(string g, int n) { return inner.DeleteGenerationStep(g, n); }
            public void Dispose() { inner.Dispose(); }
        }
    }
}
