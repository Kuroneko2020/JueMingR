using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using JueMingR.Features.Footprints;
using JueMingR.Infrastructure.Footprints;
using JueMingR.Platform.Footprints;

namespace JueMingR.ArchitectureTests
{
    internal static class FootprintOverviewChecks
    {
        internal static void Check(IList<string> failures)
        {
            // The normal runner's output path can itself approach MAX_PATH.
            // Match existing file checks with a short, exclusively owned temp root.
            string root = Path.Combine(Path.GetTempPath(), "jmr-ov-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(root);
            try { LongHistory(root); TimeBoundary(root); LodAndCrossing(root); }
            catch (Exception e) { failures.Add("footprint overview: " + e); }
            finally { Directory.Delete(root, true); } // Solely this generated test root.
        }
        private static void Require(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
        private static FootprintOverviewRequest Request(FootprintArchive archive, long serial, float zoom = .1f, long until = Int64.MaxValue,
            float minX = 0, float minY = 0, float maxX = 8400, float maxY = 2400)
        { return new FootprintOverviewRequest(archive.Generation, serial, minX, minY, maxX, maxY, zoom, until); }
        private static void Build(FootprintOverviewIndex index) { while (!index.BuildStep(8)) { } }
        private static bool Region(FootprintOverviewSnapshot snapshot, float x, float y)
        {
            foreach (var edge in snapshot.Edges)
                if (Math.Min(edge.X1, edge.X2) <= x + 150 && Math.Max(edge.X1, edge.X2) >= x - 150 &&
                    Math.Min(edge.Y1, edge.Y2) <= y + 150 && Math.Max(edge.Y1, edge.Y2) >= y - 150) return true;
            return false;
        }
        private static void LongHistory(string root)
        {
            string path = Path.Combine(root, "h"), pair = new string('6', 64); string generation;
            using (var files = new FileFootprintArchive(path, pair))
            {
                var archive = new FootprintArchive(files, pair, 8400, 2400); archive.Load(); generation = archive.Generation;
                const int count = 48000; long duration = TimeSpan.FromSeconds(15).Ticks; var batch = new FootprintSample[256]; int used = 0;
                float previousX = 100, previousY = 100; long segment = 1;
                // Equal-duration movement spans the full 200h; four distant
                // regions contain curves, reversals, long runs, stays and repeats.
                for (int i = 0; i < count; i++)
                {
                    int region = i / 12000, phase = i % 80;
                    float originX = region == 0 ? 100 : region == 1 ? 3000 : region == 2 ? 7000 : 600;
                    float originY = region == 3 ? 1800 : 100 + region * 500;
                    if (i % 12000 == 0) { segment++; previousX = originX; previousY = originY; }
                    float x, y;
                    if (phase < 20) { x = originX + phase * 5; y = originY; }
                    else if (phase < 40) { x = originX + 95; y = originY + (phase - 20) * 5; }
                    else if (phase < 60) { x = originX + (59 - phase) * 5; y = originY + 95 + (float)Math.Sin((phase - 40) * Math.PI / 10) * 6; }
                    else if (phase < 75) { x = originX; y = originY + (74 - phase) * 95f / 14; }
                    else { x = previousX; y = previousY; }
                    batch[used++] = new FootprintSample(i + 1L, i * duration, (i + 1L) * duration, segment, previousX, previousY, x, y, FootprintPosition.Valid);
                    previousX = x; previousY = y;
                    if (used == batch.Length) { archive.Append(batch, used); used = 0; }
                }
                if (used != 0) archive.Append(batch, used);
                Require(archive.Count == count && archive.End == TimeSpan.FromHours(200).Ticks, "rich movement spans 200 hours, not a stationary duration surrogate");
                var watch = Stopwatch.StartNew(); var index = new FootprintOverviewIndex(archive, 8 * 1024 * 1024, 1024 * 1024); Build(index);
                var latest = index.Query(Request(archive, 1), archive.SnapshotTail()); watch.Stop();
                long prepareMs = watch.ElapsedMilliseconds;
                Require(latest.Complete && latest.SourceSequence == count && latest.CoveredEnd == archive.End, "all source facts are covered beyond the old 8192 window");
                Require(Region(latest, 100, 100) && Region(latest, 3000, 600) && Region(latest, 7000, 1100) && Region(latest, 600, 1800), "old, middle and late distant routes coexist at latest");
                Require(latest.Edges.Length < count / 4, "repeated base routes share visible coverage rather than multiplying every traversal");
                long builds = index.GeometryBuilds, reads = index.SourcePageReads;
                index.CancelQuery(); watch.Restart(); var reopened = index.Query(Request(archive, 2), archive.SnapshotTail()); watch.Stop(); long reopenMs = watch.ElapsedMilliseconds;
                Require(reopened.Complete && index.GeometryBuilds == builds && index.SourcePageReads == reads, "reopening reads reusable derived pages without source recompression");
                var early = index.Query(Request(archive, 3, .1f, TimeSpan.FromHours(75).Ticks), archive.SnapshotTail());
                Require(Region(early, 100, 100) && Region(early, 3000, 600) && !Region(early, 7000, 1100) && !Region(early, 600, 1800), "historical cursor retains all earlier branches and excludes future regions");
                long selections = index.SelectionBuilds, cacheReads = index.CachePageReads;
                index.Query(Request(archive, 31, .1f, TimeSpan.FromHours(75).Ticks + TimeSpan.FromMilliseconds(250).Ticks), archive.SnapshotTail());
                Require(index.SelectionBuilds <= selections + 1 && index.CachePageReads <= cacheReads + 1, "moving cursor reuses full past pages and known future pages, revisiting only boundary");
                var local = index.Query(Request(archive, 4, 4, Int64.MaxValue, 70, 70, 230, 230), archive.SnapshotTail());
                Require(local.Complete && Region(local, 100, 100) && !Region(local, 3000, 600), "panning directly retrieves oldest region without moving cursor");
                Require(index.CacheBytes <= 8 * 1024 * 1024 && index.MetadataBytes <= 1024 * 1024 && index.SelectionBytes <= 8 * 1024 * 1024, "resident page, spatial and prepared-geometry caches respect byte budgets");
                // Metadata exhaustion degrades to numbered derived-page streaming,
                // never to excluding old routes or refusing subsequent recording.
                var small = new FootprintOverviewIndex(archive, 65536, 1024); Build(small);
                var complete = small.Query(Request(archive, 5), archive.SnapshotTail());
                Require(complete.Complete && Region(complete, 100, 100) && Region(complete, 600, 1800) && small.MetadataBytes <= 1024, "metadata ceiling preserves complete coverage");
                index.CancelQuery();
                string cache = Path.Combine(path, pair, generation, "overview", "00000000000000000000.bin");
                byte[] damaged = File.ReadAllBytes(cache); damaged[damaged.Length - 1] ^= 1; File.WriteAllBytes(cache, damaged);
                var repaired = index.Query(Request(archive, 6), archive.SnapshotTail());
                Require(repaired.Complete && index.GeometryBuilds == builds + 1 && !archive.Protected, "corrupt derived page rebuilds from protected source facts");
                Console.WriteLine("overview-200h: intervals=" + count + ", blocks=" + archive.BlockCount + ", edges=" + latest.Edges.Length + ", prepareMs=" + prepareMs + ", reopenMs=" + reopenMs +
                    ", geometryBytes=" + index.CacheBytes + ", metadataBytes=" + index.MetadataBytes + ", sourceReads=" + index.SourcePageReads + ", derivedReads=" + index.CachePageReads);
            }
            // A cache from an earlier worker cannot certify a newly damaged
            // immutable source page. Its original bytes must remain untouched.
            string sourcePath = Path.Combine(path, pair, generation, "blocks", "00000000000000000000.bin");
            byte[] sourceBytes = File.ReadAllBytes(sourcePath); sourceBytes[0] ^= 1; File.WriteAllBytes(sourcePath, sourceBytes);
            bool rejected = false;
            using (var files = new FileFootprintArchive(path, pair))
            {
                var archive = new FootprintArchive(files, pair, 8400, 2400);
                try { archive.Load(); new FootprintOverviewIndex(archive).BuildStep(); }
                catch (InvalidDataException) { rejected = true; }
            }
            byte[] preserved = File.ReadAllBytes(sourcePath);
            Require(rejected && preserved.Length == sourceBytes.Length, "existing derived cache cannot hide damaged source after reopening");
            for (int i = 0; i < preserved.Length; i++) Require(preserved[i] == sourceBytes[i], "source corruption remains byte-for-byte protected");
        }

        private static void TimeBoundary(string root)
        {
            string pair = new string('7', 64);
            using (var files = new FileFootprintArchive(Path.Combine(root, "t"), pair))
            {
                var archive = new FootprintArchive(files, pair, 8400, 2400); archive.Load(); long second = TimeSpan.TicksPerSecond;
                archive.Append(new[] {
                    new FootprintSample(1, 0, 10 * second, 1, 10, 10, 110, 10, FootprintPosition.Valid),
                    new FootprintSample(2, 10 * second, 30 * second, 1, 110, 10, FootprintPosition.Valid),
                    new FootprintSample(3, 30 * second, 40 * second, 1, 110, 10, 110, 110, FootprintPosition.Valid),
                    new FootprintSample(4, 40 * second, 50 * second, 2, 7000, 700, 7100, 700, FootprintPosition.Valid) }, 4);
                var index = new FootprintOverviewIndex(archive); Build(index);
                var moving = index.Query(Request(archive, 1, .01f, 5 * second), archive.SnapshotTail());
                Require(moving.Edges.Length == 1 && Math.Abs(moving.Edges[0].X2 - 60) < .001 && moving.Edges[0].Y2 == 10, "cursor inside simplified parent descends to original time leaf");
                var stay = index.Query(Request(archive, 2, .01f, 20 * second), archive.SnapshotTail());
                Require(stay.Edges.Length == 1 && stay.Edges[0].X2 == 110 && stay.Edges[0].Y2 == 10, "long stay preserves previous full route without slow travel or future corner");
                var corner = index.Query(Request(archive, 3, .01f, 35 * second), archive.SnapshotTail());
                Require(corner.Edges.Length == 2 && corner.Edges[1].X2 == 110 && corner.Edges[1].Y2 == 60, "boundary position follows actual second movement interval");
                var latest = index.Query(Request(archive, 4, 1), archive.SnapshotTail());
                foreach (var edge in latest.Edges) Require(edge.Segment == 1 ? edge.X2 <= 110 : edge.X1 >= 7000, "teleport and source segment boundaries never gain a bridge");
                archive.Append(new[] {
                    new FootprintSample(5, 50 * second, 51 * second, 3, 3000, 1000, 3001, 1000, FootprintPosition.Valid),
                    new FootprintSample(6, 51 * second, 52 * second, 3, 3001, 1000, 3000, 1000, FootprintPosition.Valid) }, 2);
                var loop = index.Query(Request(archive, 5, .001f), archive.SnapshotTail());
                Require(Region(loop, 3000, 1000), "a tiny closed moving branch retains coverage instead of collapsing to an omitted zero chord");
            }
        }

        private static void LodAndCrossing(string root)
        {
            string pair = new string('8', 64);
            using (var files = new FileFootprintArchive(Path.Combine(root, "l"), pair))
            {
                var archive = new FootprintArchive(files, pair, 8400, 2400); archive.Load(); var samples = new FootprintSample[256];
                float px = 100, py = 100;
                for (int i = 0; i < samples.Length; i++)
                {
                    float x = 100 + (i + 1) * 2, y = 100 + (float)Math.Sin((i + 1) * .2) * 5;
                    samples[i] = new FootprintSample(i + 1, i * 100L, (i + 1) * 100L, 1, px, py, x, y, FootprintPosition.Valid); px = x; py = y;
                }
                archive.Append(samples, samples.Length);
                archive.Append(new[] { new FootprintSample(257, 25600, 25700, 2, 100, 1200, 8000, 1200, FootprintPosition.Valid) }, 1);
                var index = new FootprintOverviewIndex(archive); Build(index);
                var coarse = index.Query(Request(archive, 1, .02f), archive.SnapshotTail()); long builds = index.GeometryBuilds;
                var fine = index.Query(Request(archive, 2, 8), archive.SnapshotTail());
                Require(fine.Edges.Length > coarse.Edges.Length * 4 && index.GeometryBuilds == builds, "zoom selects saved shape details without recompressing archive");
                var cross = index.Query(Request(archive, 3, 1, Int64.MaxValue, 3990, 1190, 4010, 1210), archive.SnapshotTail());
                Require(cross.Complete && cross.Edges.Length == 1 && cross.Edges[0].X1 < 3990 && cross.Edges[0].X2 > 4010, "line crossing visible interior remains visible with both endpoints outside");
                long cells = index.SpatialCellVisits;
                var stable = index.Result;
                for (int i = 0; i < 1000; i++) Require(Object.ReferenceEquals(stable, index.QueryStep()), "stable query returns same immutable snapshot");
                Require(cells == index.SpatialCellVisits && builds == index.GeometryBuilds, "stable viewport performs no repeated spatial scan or geometry build");
                var request = Request(archive, 4, 1);
                index.Query(request, archive.SnapshotTail()); long reads = index.SourcePageReads;
                archive.Append(new[] { new FootprintSample(258, 25700, 25800, 2, 8000, 1200, 8100, 1200, FootprintPosition.Valid) }, 1);
                index.BuildStep(); index.BeginQuery(request, archive.SnapshotTail()); while (index.QueryPending) index.QueryStep();
                Require(index.Result.SourceSequence == 258 && index.Result.Complete && index.SourcePageReads == reads, "root append replaces only finite tail, retaining prepared historical output");
                long geometryBuilds = index.GeometryBuilds; var more = new FootprintSample[256]; long from = archive.End;
                px = 8100; py = 1200;
                for (int i = 0; i < more.Length; i++)
                {
                    float x = 8100 + (i + 1) * .1f;
                    more[i] = new FootprintSample(259 + i, from + i * 100, from + (i + 1) * 100, 2, px, py, x, py, FootprintPosition.Valid); px = x;
                }
                archive.Append(more, more.Length); index.BuildStep(); index.BeginQuery(request, archive.SnapshotTail()); while (index.QueryPending) index.QueryStep();
                Require(index.Result.Complete && index.Result.SourceSequence == 514 && index.GeometryBuilds == geometryBuilds + 1 && index.SourcePageReads == reads + 1,
                    "sealing another physical block builds only new geometry and incrementally extends the same request");
                index.CancelQuery(); Require(index.Result == null && !index.QueryPending && index.CacheBytes == 0 && index.SelectionBytes == 0, "closing cancels demand and releases reclaimable geometry caches");
            }
        }
    }
}
