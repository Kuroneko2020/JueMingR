using System;
using System.Collections.Generic;
using System.Threading;
using JueMingR.Features.MapMarkers;
using JueMingR.Features.Exploration;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class MapPersistenceChecks
    {
        private static readonly string A = new string('a', 64), B = new string('b', 64);
        internal static void Check(List<string> failures)
        {
            Run(failures, "marker late identity", MarkerIdentity);
            Run(failures, "summary late identity", SummaryIdentity);
            Run(failures, "summary exit protection", SummaryExit);
            Run(failures, "marker unknown receipt reentry", MarkerUnknown);
            Run(failures, "summary workerless retirement", SummaryRetirement);
            Run(failures, "summary old failure isolation", SummaryIsolation);
        }
        private static void Run(List<string> failures, string name, Action action)
        { try { action(); } catch (Exception e) { failures.Add(name + ": " + e.Message); } }
        private static void MarkerIdentity()
        {
            var gate = new Gate(holdRead: true); var owner = new MarkerLibrary(_ => gate);
            try
            {
                owner.BeginSession(1, 128, 64); owner.UsePair(A); Require(gate.ReadEntered.Wait(3000), "read entered"); owner.UsePair(B); gate.ReadRelease.Set(); Until(owner.Poll, () => owner.Loaded);
                Require(owner.Protected && !owner.CanEdit && owner.Error == "marker-identity-changed", "late original load cannot undo identity fence");
            }
            finally { gate.Release(); Require(owner.Stop(5000), "stop"); Require(gate.Writes == 0, "no protected write"); gate.DisposeEvents(); }
        }
        private static void SummaryIdentity()
        {
            long clock = 0; var gate = new Gate(holdRead: true); var owner = new ExplorationHistory(_ => gate, () => clock);
            try
            {
                owner.BeginSession(128, 64); owner.UsePair(A); Require(gate.ReadEntered.Wait(3000), "read entered"); owner.Offer(9, DateTime.UtcNow.Ticks); owner.UsePair(B); gate.ReadRelease.Set(); Until(owner.Poll, () => owner.Loaded);
                clock = 10001; owner.Poll(); Require(owner.Error == "exploration-identity-changed", "late load keeps identity error");
            }
            finally { gate.Release(); Require(owner.Stop(5000), "stop"); Require(gate.Writes == 0, "protected summary never writes on exit"); gate.DisposeEvents(); }
        }
        private static void SummaryExit()
        {
            var gate = new Gate(); var owner = new ExplorationHistory(_ => gate, () => 0);
            try { owner.BeginSession(128, 64); owner.UsePair(A); Until(owner.Poll, () => owner.Loaded); owner.Offer(9, DateTime.UtcNow.Ticks); owner.UsePair(B); owner.EndSession(); }
            finally { gate.Release(); Require(owner.Stop(5000), "stop"); Require(gate.Writes == 0, "identity fence also controls final update"); gate.DisposeEvents(); }
        }
        private static void MarkerUnknown()
        {
            var gate = new Gate(fail: true, unknown: true); int factories = 0; var owner = new MarkerLibrary(_ => { factories++; return gate; });
            try
            {
                owner.BeginSession(1, 128, 64); owner.UsePair(A); Until(owner.Poll, () => owner.Loaded);
                var added = new MarkerRecord("00000000000000000000000000000001", 1.25, 2.5, 8, "新点"); long id = owner.Create(added); Require(id > 0, "admitted"); Until(owner.Poll, () => owner.LastOperation == id);
                Require(owner.Protected && owner.CommitUnconfirmed && owner.Saved.Records.Count == 0 && MarkerCodec.Decode(gate.Inner.Bytes, A, 128, 64).Records.Count == 1, "unknown means bytes may differ from trusted Saved");
                owner.BeginSession(2, 128, 64); owner.UsePair(A); Until(owner.Poll, () => owner.Loaded);
                Require(owner.Protected && owner.CommitUnconfirmed && factories == 1 && gate.Writes == 1, "same-process reentry preserves unknown and never replays");
            }
            finally { gate.Release(); Require(owner.Stop(5000), "stop"); gate.DisposeEvents(); }
        }
        private static void SummaryRetirement()
        {
            long clock = 0; var leases = new List<Gate>();
            var owner = new ExplorationHistory(_ => { var gate = new Gate(leases.Count == 0 ? null : leases[leases.Count - 1].Inner.Bytes, holdWrite: leases.Count == 0); leases.Add(gate); return gate; }, () => clock);
            try
            {
                owner.BeginSession(128, 64); owner.UsePair(A); Until(owner.Poll, () => owner.Loaded); owner.Offer(10, DateTime.UtcNow.Ticks); clock = 10000; owner.Poll(); Require(leases[0].WriteEntered.Wait(3000), "first write held");
                owner.BeginSession(128, 64); owner.UsePair(A); Require(leases.Count == 1, "same-pair lease serialized"); owner.Offer(20, DateTime.UtcNow.Ticks); owner.EndSession(); leases[0].WriteRelease.Set();
                Until(() => { }, () => !leases[0].Worker.IsAlive); Require(owner.Stop(5000), "all summaries drained");
                Require(ExplorationSummary.Decode(leases[leases.Count - 1].Inner.Bytes, A, 128, 64).Count == 20, "workerless accepted tail retained");
            }
            finally { foreach (var gate in leases) gate.Release(); Require(owner.Stop(5000), "cleanup stop"); foreach (var gate in leases) gate.DisposeEvents(); }
        }
        private static void SummaryIsolation()
        {
            long clock = 0; var first = new Gate(holdWrite: true, fail: true); var second = new Gate(); var owner = new ExplorationHistory(key => key == A ? first : second, () => clock);
            try
            {
                owner.BeginSession(128, 64); owner.UsePair(A); Until(owner.Poll, () => owner.Loaded); owner.Offer(10, DateTime.UtcNow.Ticks); clock = 10000; owner.Poll(); Require(first.WriteEntered.Wait(3000), "A write held");
                owner.BeginSession(128, 64); owner.UsePair(B); Until(owner.Poll, () => owner.Loaded); owner.Offer(30, DateTime.UtcNow.Ticks); clock = 20000; Until(owner.Poll, () => owner.Saved);
                first.WriteRelease.Set(); Until(() => { }, () => !first.Worker.IsAlive); owner.Poll();
                Require(owner.Error == null && owner.Saved && ExplorationSummary.Decode(second.Inner.Bytes, B, 128, 64).Count == 30, "A late failure cannot change B state");
            }
            finally { first.Release(); second.Release(); Require(owner.Stop(5000), "stop"); first.DisposeEvents(); second.DisposeEvents(); }
        }
        private static void Until(Action pump, Func<bool> condition)
        { if (!SpinWait.SpinUntil(() => { pump(); return condition(); }, 5000)) throw new TimeoutException("map persistence result"); }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        private sealed class Gate : IPreferenceStorage
        {
            internal readonly HotkeyCoreChecks.MemoryStorage Inner;
            internal readonly ManualResetEventSlim ReadEntered = new ManualResetEventSlim(), WriteEntered = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim ReadRelease, WriteRelease;
            internal Thread Worker;
            internal int Writes;
            internal Gate(byte[] seed = null, bool holdRead = false, bool holdWrite = false, bool fail = false, bool unknown = false)
            { Inner = new HotkeyCoreChecks.MemoryStorage { Bytes = seed, Fail = fail, Unknown = unknown }; ReadRelease = new ManualResetEventSlim(!holdRead); WriteRelease = new ManualResetEventSlim(!holdWrite); }
            public PreferenceReadResult Read() { Worker = Thread.CurrentThread; ReadEntered.Set(); if (!ReadRelease.Wait(5000)) throw new TimeoutException("read gate"); return Inner.Read(); }
            public PreferenceWriteResult Write(string identity, byte[] bytes) { Interlocked.Increment(ref Writes); WriteEntered.Set(); if (!WriteRelease.Wait(5000)) throw new TimeoutException("write gate"); return Inner.Write(identity, bytes); }
            public void Dispose() { Inner.Dispose(); }
            internal void Release() { ReadRelease.Set(); WriteRelease.Set(); }
            internal void DisposeEvents() { ReadEntered.Dispose(); WriteEntered.Dispose(); ReadRelease.Dispose(); WriteRelease.Dispose(); }
        }
    }
}
