using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using JueMingR.Features.Footprints;
using JueMingR.Infrastructure.Footprints;
using JueMingR.Platform.Footprints;

namespace JueMingR.ArchitectureTests
{
    // Explicit offline scale check. The independent generator is replayed after
    // persistence; it never retains the 43.2 million raw observations in memory.
    internal static class FootprintLongHistoryChecks
    {
        internal const string Pair = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        private const long Observations = 43200000;
        internal static int Run(string area)
        {
            area = Path.GetFullPath(area); if (Directory.Exists(area)) throw new InvalidOperationException("200h output must be fresh");
            Directory.CreateDirectory(area); long now = 0, batches = 0, compressed;
            var timer = Stopwatch.StartNew(); double recordSeconds, maxSed = 0;
            using (var files = new FileFootprintArchive(Path.Combine(area, "footprints-v2"), Pair))
            {
                var archive = new FootprintArchive(files, Pair, 8400, 2400); archive.Load();
                var accepted = new List<FootprintSample>(256);
                var recorder = new FootprintRecorder(0, 0, 0, (values, count) =>
                {
                    batches++;
                    for (int n = 0; n < count; n++)
                    {
                        var value = values[n];
                        if (accepted.Count != 0 && accepted[accepted.Count - 1].Sequence == value.Sequence)
                        { FootprintCoreChecks.Require(accepted[accepted.Count - 1].SameIdentity(value) && accepted[accepted.Count - 1].End == value.End, "published checkpoint frozen"); continue; }
                        if (value.Sequence <= archive.Count) continue;
                        accepted.Add(value);
                        if (accepted.Count == 256) { archive.Append(accepted.ToArray(), accepted.Count); accepted.Clear(); }
                    }
                    return true;
                }, () => now);
                for (long i = 1; i <= Observations; i++)
                {
                    float x, y; FootprintPosition position; bool discontinuity; Point(i, out x, out y, out position, out discontinuity);
                    long time = i * FootprintSample.TicksPerSecond / 60, prior = (i - 1) * FootprintSample.TicksPerSecond / 60;
                    FootprintCoreChecks.Require(recorder.Observe(x, y, position, discontinuity, time - prior), "scale source accepted");
                    if (i % 600 == 0) { now = i * 1000 / 60; recorder.FlushDue(); }
                }
                recorder.Flush(); if (accepted.Count != 0) archive.Append(accepted.ToArray(), accepted.Count);
                compressed = archive.Count; recordSeconds = timer.Elapsed.TotalSeconds;
                FootprintCoreChecks.Require(archive.End == 200L * 3600 * FootprintSample.TicksPerSecond && compressed > 10000 && compressed < Observations / 10, "200h rich movement compactly persisted");
            }
            double restoreSeconds, validationSeconds, overviewSeconds, requerySeconds; long edges, builds, reads, cacheBytes, metadataBytes;
            using (var files = new FileFootprintArchive(Path.Combine(area, "footprints-v2"), Pair))
            {
                var archive = new FootprintArchive(files, Pair, 8400, 2400); var stage = Stopwatch.StartNew(); archive.Load(); restoreSeconds = stage.Elapsed.TotalSeconds; stage.Restart();
                long pageNumber = 0; var source = archive.BlockCount == 0 ? archive.SnapshotTail() : archive.ReadOverviewBlock(0); int at = 0;
                for (long i = 1; i <= Observations; i++)
                {
                    long time = i * FootprintSample.TicksPerSecond / 60;
                    while (source[at].End < time)
                    {
                        at++; if (at == source.Length) { pageNumber++; source = pageNumber < archive.BlockCount ? archive.ReadOverviewBlock(pageNumber) : archive.SnapshotTail(); at = 0; }
                    }
                    float x, y; FootprintPosition position; bool discontinuity; Point(i, out x, out y, out position, out discontinuity);
                    var sample = source[at]; FootprintCoreChecks.Require(sample.Position == position, "position availability preserved");
                    if (position == FootprintPosition.Valid)
                    {
                        double u = (time - sample.Start) / (double)(sample.End - sample.Start);
                        double dx = sample.FromX + (sample.X - (double)sample.FromX) * u - x;
                        double dy = sample.FromY + (sample.Y - (double)sample.FromY) * u - y;
                        double sed = Math.Sqrt(dx * dx + dy * dy); maxSed = Math.Max(maxSed, sed);
                        if (sed > FootprintRecorder.RecordErrorTiles + .00001) throw new InvalidOperationException("independent SED exceeded at " + i + ": " + sed.ToString("R", CultureInfo.InvariantCulture));
                    }
                }
                validationSeconds = stage.Elapsed.TotalSeconds; stage.Restart();
                var index = new FootprintOverviewIndex(archive); while (!index.BuildStep(8)) { }
                var request = new FootprintOverviewRequest(archive.Generation, 1, 0, 0, 8400, 2400, .12f, archive.End);
                var result = index.Query(request, archive.SnapshotTail()); overviewSeconds = stage.Elapsed.TotalSeconds;
                FootprintCoreChecks.Require(result.Complete && result.SourceSequence == archive.Count && result.Edges.Length > 0, "full 200h public overview coverage");
                bool early = false, middle = false, late = false;
                foreach (var edge in result.Edges) { early |= edge.Start < archive.End / 10; middle |= edge.Start > archive.End * 4 / 10 && edge.End < archive.End * 6 / 10; late |= edge.End > archive.End * 9 / 10; }
                // Repeated coverage may coalesce into older representative edges;
                // independent temporal queries prove late source remains queryable.
                FootprintCoreChecks.Require(early && (middle || late || index.Query(new FootprintOverviewRequest(archive.Generation, 2, 0, 0, 8400, 2400, .12f, archive.End / 2), archive.SnapshotTail()).Complete) && archive.Query(archive.End).End == archive.End, "old/middle/new source remains reachable");
                edges = result.Edges.Length; builds = index.GeometryBuilds; reads = index.SourcePageReads; cacheBytes = index.CacheBytes; metadataBytes = index.MetadataBytes;
                stage.Restart(); index.Query(request, archive.SnapshotTail()); requerySeconds = stage.Elapsed.TotalSeconds;
                FootprintCoreChecks.Require(index.GeometryBuilds == builds, "repeat overview reuses prepared simplification");
            }
            long bytes = 0; foreach (string file in Directory.EnumerateFiles(area, "*", SearchOption.AllDirectories)) bytes += new FileInfo(file).Length;
            string summary = "{\"observations\":" + Observations + ",\"hours\":200,\"segments\":" + compressed + ",\"checkpointBatches\":" + batches + ",\"maxSedTiles\":" + F(maxSed) + ",\"recordAndWriteSeconds\":" + F(recordSeconds) + ",\"restoreSeconds\":" + F(restoreSeconds) + ",\"independentReplaySeconds\":" + F(validationSeconds) + ",\"firstOverviewSeconds\":" + F(overviewSeconds) + ",\"repeatOverviewSeconds\":" + F(requerySeconds) + ",\"overviewEdges\":" + edges + ",\"geometryBuilds\":" + builds + ",\"sourcePageReads\":" + reads + ",\"cacheBytes\":" + cacheBytes + ",\"metadataBytes\":" + metadataBytes + ",\"archiveAndDerivedBytes\":" + bytes + "}";
            File.WriteAllText(Path.Combine(area, "summary.json"), summary); Console.WriteLine(summary); return 0;
        }
        private static string F(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
        private static void Point(long i, out float x, out float y, out FootprintPosition position, out bool discontinuity)
        {
            long cycle = (i - 1) / 36000; double t = ((i - 1) % 36000) / 60.0;
            int kind = (int)(cycle % 6); double baseX = 200 + cycle % 7 * 1000, baseY = 300 + cycle % 3 * 500;
            discontinuity = (i - 1) % 36000 == 0; position = FootprintPosition.Valid;
            double px, py;
            if (kind == 0) { px = t * .4; py = t * .05; }
            else if (kind == 1) { px = 80 * Math.Cos(t * .2); py = 80 * Math.Sin(t * .2); }
            else if (kind == 2) { px = 100 - Math.Abs(t % 20 - 10) * 10; py = t * .1; }
            else if (kind == 3) { px = 80 * Math.Sin(t * .2); py = 80 * Math.Sin(t * .4); }
            else if (kind == 4) { double phase = t % 60; px = phase < 20 ? 0 : phase < 40 ? (phase - 20) * 3 : 60; py = 0; }
            else { px = t * .001; py = Math.Sin(t * .002) * 2; }
            x = (float)(baseX + px); y = (float)(baseY + py);
        }
    }
}