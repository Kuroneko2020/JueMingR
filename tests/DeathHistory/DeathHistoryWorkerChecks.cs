using System;
using System.Collections.Generic;
using System.Threading;
using JueMingR.Features.DeathHistory;
using JueMingR.Platform.DeathHistory;

namespace JueMingR.ArchitectureTests
{
    internal static class DeathHistoryWorkerChecks
    {
        internal static void Check(IList<string> failures)
        {
            try { LateIdentityCapacity(); RetiredOccurrenceOrder(); FailedPage(); } catch (Exception e) { failures.Add("death handoff/query regression: " + e.Message); }
            var entered = new ManualResetEvent(false); var release = new ManualResetEvent(false);
            int foreground = Thread.CurrentThread.ManagedThreadId; bool background = false;
            var history = new DeathHistory(pair => { background = Thread.CurrentThread.ManagedThreadId != foreground; entered.Set(); release.WaitOne(); return new DeathArchiveChecks.MemoryArchive(); });
            try
            {
                history.BeginSession(1); history.UsePair(new string('a', 64));
                DeathArchiveChecks.Require(entered.WaitOne(3000), "background load starts");
                DeathArchiveChecks.Require(!history.Snapshot.Known, "loading must not display fake zero");
                var a = DeathArchiveChecks.Fact(20, 1); var b = DeathArchiveChecks.Fact(10, 2);
                DeathArchiveChecks.Require(history.Accept(a) && history.Accept(b), "cold read cannot prevent accepting two different facts");
                history.Request(0, 128, null); release.Set();
                Wait(() => history.Snapshot.Known && history.Snapshot.Count == 2 && history.Snapshot.Rows.Count == 2);
                var state = history.Snapshot;
                DeathArchiveChecks.Require(background && state.Rows[0].EventId == b.EventId && state.Markers[0].EventId == b.EventId, "one background owner publishes consistent views");
                history.Accept(a); Wait(() => history.Snapshot.Pending == 0);
                DeathArchiveChecks.Require(history.Snapshot.Count == 2, "replayed callback cannot inflate frontend count");
                history.BeginSession(2); history.UsePair(new string('b', 64)); history.Request(0, 128, null);
                Wait(() => history.Snapshot.Known);
                DeathArchiveChecks.Require(history.Snapshot.Count == 0 && history.Snapshot.Rows.Count == 0 && history.Snapshot.Markers.Count == 0, "old pair results cannot enter new session");
                history.Accept(a); Wait(() => history.Snapshot.Count == 1);
                history.Accept(new DeathFact(a.EventId, a.Time.Offset, true, a.X, a.Y, "different reason"));
                Wait(() => history.Snapshot.Error != null);
                DeathArchiveChecks.Require(history.Snapshot.Count == 1, "identity conflict reports failure without replacing original fact");
            }
            catch (Exception e) { failures.Add("death history worker: " + e.Message); }
            finally { release.Set(); if (!history.Stop(3000)) failures.Add("death worker retains storage after stop timeout"); else { entered.Dispose(); release.Dispose(); } }
        }
        private static void LateIdentityCapacity()
        {
            var entered = new ManualResetEvent(false); var release = new ManualResetEvent(false);
            var files = new Dictionary<string, DeathArchiveChecks.MemoryArchive>();
            var history = new DeathHistory(pair => { entered.Set(); release.WaitOne(); var value = new DeathArchiveChecks.MemoryArchive(); files.Add(pair, value); return value; });
            try
            {
                for (int i = 0; i < 8; i++) { history.BeginSession(i); history.UsePair(new string((char)('0' + i), 64)); }
                DeathArchiveChecks.Require(entered.WaitOne(3000), "all archive admission slots can be held while first load is blocked");
                history.BeginSession(9); history.UsePair(new string('f', 63) + '9');
                var fact = DeathArchiveChecks.Fact(1, 91);
                DeathArchiveChecks.Require(history.Accept(fact) && history.Accept(fact), "reserved handoff admits identical replay once even when archives are busy");
                DeathArchiveChecks.Require(!history.Accept(new DeathFact(fact.EventId, fact.Time.Offset, true, fact.X, fact.Y, "conflict")) && history.Error != null, "unbound same identity with different content is explicit conflict");
                history.EndSession(); release.Set();
                history.BeginSession(10); history.UsePair(new string('f', 63) + '9');
                Wait(() => { if (!history.HasPair) history.UsePair(new string('f', 63) + '9'); return history.Snapshot.Known && history.Snapshot.Count == 1; });
                DeathArchiveChecks.Require(history.Snapshot.Count == 1, "reliable retired handoff survives full lease table and session exit");
            }
            finally { release.Set(); DeathArchiveChecks.Require(history.Stop(3000), "retired death handoff drain"); entered.Dispose(); release.Dispose(); }
        }
        private static void FailedPage()
        {
            var files = new DeathArchiveChecks.MemoryArchive(); var archive = new DeathArchive(files, new string('a', 64)); archive.Load();
            var facts = new DeathFact[12]; for (int i = 0; i < facts.Length; i++) facts[i] = DeathArchiveChecks.Fact(i, i + 1); archive.Append(facts);
            var history = new DeathHistory(_ => files);
            try
            {
                history.BeginSession(1); history.UsePair(new string('a', 64)); history.Request(0, 0, null); Wait(() => history.Snapshot.Known && history.Snapshot.Rows.Count == 6);
                files.FailReads = true; history.Request(6, 0, null); Wait(() => history.Snapshot.Request == history.RequestId);
                DeathArchiveChecks.Require(history.Snapshot.Error != null && history.Snapshot.Rows.Count == 0 && history.Snapshot.Count == 12, "failed second page cannot relabel first-page rows");
            }
            finally { files.FailReads = false; DeathArchiveChecks.Require(history.Stop(3000), "failed page worker drain"); }
        }
        private static void RetiredOccurrenceOrder()
        {
            var entered = new ManualResetEvent(false); var release = new ManualResetEvent(false); var files = new DeathArchiveChecks.MemoryArchive();
            var history = new DeathHistory(_ => { entered.Set(); release.WaitOne(); return files; }); string pair = new string('c', 64);
            try
            {
                history.BeginSession(1); history.UsePair(pair); DeathArchiveChecks.Require(entered.WaitOne(3000), "old history load blocked");
                for (int i = 0; i < 64; i++) DeathArchiveChecks.Require(history.Accept(DeathArchiveChecks.Fact(i, i + 1)), "fill old lease admission quota");
                var b = DeathArchiveChecks.Fact(3, 65); var c = DeathArchiveChecks.Fact(2, 66); var d = DeathArchiveChecks.Fact(1, 67);
                history.BeginSession(2); history.Accept(b); history.UsePair(pair);
                history.BeginSession(3); history.Accept(c); history.UsePair(pair);
                history.BeginSession(4); history.UsePair(pair); DeathArchiveChecks.Require(!history.Accept(d) && history.Error != null, "full archive explicitly refuses new acceptance instead of hiding it outside its view");
                release.Set(); history.Request(0, 128, null); Wait(() => history.Snapshot.Count == 66);
                DeathArchiveChecks.Require(history.Accept(d), "current acceptance resumes after earlier tails are admitted");
                Wait(() => history.Snapshot.Count == 67);
                var markers = history.Snapshot.Markers;
                DeathArchiveChecks.Require(markers[0].EventId == d.EventId && markers[1].EventId == c.EventId && markers[2].EventId == b.EventId, "retired and current admission preserve occurrence FIFO despite UTC rollback");
            }
            finally { release.Set(); DeathArchiveChecks.Require(history.Stop(3000), "ordered handoff drain"); entered.Dispose(); release.Dispose(); }
        }
        internal static void Wait(Func<bool> predicate)
        { var clock = System.Diagnostics.Stopwatch.StartNew(); while (!predicate()) { if (clock.ElapsedMilliseconds > 5000) throw new TimeoutException("death worker result unavailable"); Thread.Sleep(5); } }
    }
}
