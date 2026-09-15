using System;
using System.Collections.Generic;
using JueMingR.Features.DeathHistory;
using JueMingR.Platform.DeathHistory;

namespace JueMingR.ArchitectureTests
{
    internal static class DeathWorkloadChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                foreach (int total in new[] { 128, 8192 })
                {
                    var files = new DeathArchiveChecks.MemoryArchive(); var archive = new DeathArchive(files, new string('e', 64)); archive.Load();
                    for (int first = 0; first < total; first += 64)
                    {
                        var batch = new DeathFact[Math.Min(64, total - first)];
                        for (int i = 0; i < batch.Length; i++) batch[i] = DeathArchiveChecks.Fact(total - first - i, first + i + 1);
                        archive.Append(batch);
                    }
                    files.PageReads = 0; var page = archive.Page(42, 6);
                    DeathArchiveChecks.Require(page.Length == 6 && page[0].EventId == DeathArchiveChecks.Fact(43, total - 42).EventId && files.PageReads <= 24, "six-row page is bounded independently of full history, ascending after UTC rollback");
                    foreach (int k in new[] { 128, 256, 512, 1024 })
                    {
                        files.PageReads = 0; int writes = files.RootWrites; var markers = archive.Recent(k);
                        DeathArchiveChecks.Require(markers.Length == Math.Min(k, total) && files.PageReads == markers.Length && files.RootWrites == writes && archive.Count == total, "K reads valid occurrence candidates; changing display quantity never edits history");
                        DeathArchiveChecks.Require(markers[0].EventId == DeathArchiveChecks.Fact(1, total).EventId, "UTC rollback cannot evict the latest occurrence");
                    }
                    var history = new DeathHistory(_ => files);
                    try
                    {
                        history.BeginSession(1); history.UsePair(new string('e', 64)); history.Request(0, 128, null);
                        DeathHistoryWorkerChecks.Wait(() => history.Snapshot.Known && history.Snapshot.Request == history.RequestId);
                        var before = history.Snapshot; int reads = files.PageReads, writes = files.RootWrites;
                        for (int tick = 0; tick < 2000; tick++)
                        { history.Request(0, 128, null); var value = history.Snapshot; DeathArchiveChecks.Require(ReferenceEquals(value, before), "stable frontend reuses its published snapshot"); }
                        DeathArchiveChecks.Require(files.PageReads == reads && files.RootWrites == writes, "stable foreground cannot read or rewrite full history");
                        history.Request(0, 128, before.Rows[0].EventId);
                        DeathHistoryWorkerChecks.Wait(() => history.Snapshot.Request == history.RequestId);
                        var selected = history.Snapshot;
                        DeathArchiveChecks.Require(ReferenceEquals(before.Rows, selected.Rows) && ReferenceEquals(before.Markers, selected.Markers) && files.PageReads - reads <= 14, "selected reason does not reload page or K candidates");
                        history.Request(6, 128, null); DeathHistoryWorkerChecks.Wait(() => history.Snapshot.Request == history.RequestId);
                        DeathArchiveChecks.Require(ReferenceEquals(selected.Markers, history.Snapshot.Markers), "page turn reuses unrelated map candidates");
                        long revision = history.Snapshot.Revision; history.Accept(before.Rows[0]);
                        history.Request(6, 256, null); DeathHistoryWorkerChecks.Wait(() => history.Snapshot.Request == history.RequestId);
                        DeathArchiveChecks.Require(history.Snapshot.Count == total && history.Snapshot.Revision == revision, "same real event cannot create another count/version on replay");
                        var unchangedRows = history.Snapshot.Rows;
                        history.Accept(DeathArchiveChecks.Fact(total + 100, total + 1));
                        DeathHistoryWorkerChecks.Wait(() => history.Snapshot.Count == total + 1);
                        DeathArchiveChecks.Require(ReferenceEquals(unchangedRows, history.Snapshot.Rows), "ordinary later death cannot rebuild an unaffected full page");
                    }
                    finally { DeathArchiveChecks.Require(history.Stop(3000), "bounded history worker exit"); }
                    Console.WriteLine("PASS: death workload N=" + total + ", P=6, K=128/256/512/1024; stable 2000 requests perform zero page reads/root writes.");
                }
            }
            catch (Exception e) { failures.Add("death workload: " + e.Message); }
        }
    }
}
