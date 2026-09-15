using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JueMingR.Features.WorldTime;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class WorldTimeChecks
    {
        internal static void Check(IList<string> failures)
        {
            try { ReenterDuringFinalWrite(); } catch (Exception e) { failures.Add("world time retirement: " + e.Message); }
            try { FinalFailureIsNotForgotten(); } catch (Exception e) { failures.Add("world time final receipt: " + e.Message); }
            try { FinalRetrySuccess(); } catch (Exception e) { failures.Add("world time final retry: " + e.Message); }
            string folder = Path.Combine(Path.GetTempPath(), "JueMingR-time-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            long now = 0; var time = new WorldTimeHistory(pair => new AtomicFileDocument(Path.Combine(folder, pair + ".json"), 65536, true), () => now);
            bool stopped = false;
            try
            {
                time.BeginSession(1); time.Advance(86400.75); time.UsePair(new string('a', 64));
                DeathHistoryWorkerChecks.Wait(() => { time.Poll(); return time.Known; });
                DeathArchiveChecks.Require(time.Days == 1 && Math.Abs(time.Total - 86400.75) < 0.001, "late load preserves observed fractional contribution");
                now = 15000; time.Poll(); // no further positive Advance: freeze
                DeathHistoryWorkerChecks.Wait(() => { time.Poll(); return time.Saved; });
                DeathArchiveChecks.Require(time.Days == 1, "frozen dirty contribution saved without advancing integer");
                time.Advance(86399.25); time.EndSession(); time.BeginSession(2); time.UsePair(new string('a', 64));
                DeathHistoryWorkerChecks.Wait(() => { time.Poll(); return time.Known; });
                DeathArchiveChecks.Require(time.Days == 2 && time.Total == 172800, "normal end flushes last fraction exactly once");
                time.BeginSession(3); time.UsePair(new string('b', 64));
                DeathHistoryWorkerChecks.Wait(() => { time.Poll(); return time.Known; });
                DeathArchiveChecks.Require(time.Total == 0, "time separated by reliable pair");
                time.Advance(0); now += 20000; time.Poll();
                DeathArchiveChecks.Require(!File.Exists(Path.Combine(folder, new string('b', 64) + ".json")), "zero contribution does not create a default file");
            }
            catch (Exception e) { failures.Add("world time: " + e.Message); }
            finally { stopped = time.Stop(3000); if (!stopped) failures.Add("world time worker did not stop"); if (stopped) Directory.Delete(folder, true); }
        }
        private static void ReenterDuringFinalWrite()
        {
            var entered = new ManualResetEvent(false); var release = new ManualResetEvent(false); byte[] saved = null; int writes = 0;
            var time = new WorldTimeHistory(pair => new BlockingTime(() => saved, bytes => { if (Interlocked.Increment(ref writes) == 1) { entered.Set(); release.WaitOne(); } saved = bytes; }), () => 0);
            try
            {
                string pair = new string('c', 64); time.BeginSession(1); time.UsePair(pair);
                DeathHistoryWorkerChecks.Wait(() => { time.Poll(); return time.Known; }); time.Advance(10); time.EndSession();
                DeathArchiveChecks.Require(entered.WaitOne(3000), "old final save is in flight");
                time.BeginSession(2); time.UsePair(pair); time.Advance(20); time.EndSession();
                release.Set(); time.BeginSession(3); time.UsePair(pair);
                DeathHistoryWorkerChecks.Wait(() => { time.Poll(); return time.Known; });
                DeathArchiveChecks.Require(time.Total == 30, "same-pair session awaiting previous lease must preserve its accepted tail");
            }
            finally { release.Set(); if (!time.Stop(3000)) throw new TimeoutException("time retirement drain"); entered.Dispose(); release.Dispose(); }
        }
        private sealed class BlockingTime : IPreferenceStorage
        {
            private readonly Func<byte[]> read; private readonly Action<byte[]> write;
            internal BlockingTime(Func<byte[]> read, Action<byte[]> write) { this.read = read; this.write = write; }
            public PreferenceReadResult Read() { var value = read(); return new PreferenceReadResult(value == null ? PreferenceReadStatus.Missing : PreferenceReadStatus.Loaded, value, "value", null); }
            public PreferenceWriteResult Write(string identity, byte[] bytes) { write(bytes); return new PreferenceWriteResult(PreferenceWriteStatus.Saved, "value", null); }
            public void Dispose() { }
        }
        private static void FinalFailureIsNotForgotten()
        {
            var time = new WorldTimeHistory(pair => new FailedFinalTime(), () => 0);
            try
            {
                string pair = new string('e', 64); time.BeginSession(1); time.UsePair(pair);
                DeathHistoryWorkerChecks.Wait(() => { time.Poll(); return time.Known; }); time.Advance(50); time.EndSession();
                time.BeginSession(2); time.UsePair(pair);
                DeathHistoryWorkerChecks.Wait(() => { time.Poll(); return time.Known || time.Error != null; });
                DeathArchiveChecks.Require(!time.Known && time.Error != null && time.CommitUnconfirmed, "final unknown commit keeps pair protected after quick re-entry");
            }
            finally { if (!time.Stop(3000)) throw new TimeoutException("failed-final worker drain"); }
        }
        private sealed class FailedFinalTime : IPreferenceStorage
        {
            public PreferenceReadResult Read() { return new PreferenceReadResult(PreferenceReadStatus.Missing, null, "missing", null); }
            public PreferenceWriteResult Write(string identity, byte[] bytes) { return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "unconfirmed-final", true, true); }
            public void Dispose() { }
        }
        private static void FinalRetrySuccess()
        {
            var entered = new ManualResetEvent(false); var release = new ManualResetEvent(false); byte[] saved = null; int writes = 0; long now = 0;
            var time = new WorldTimeHistory(_ => new RetryTime(() => saved, bytes =>
            {
                if (Interlocked.Increment(ref writes) == 1) return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, "missing", "known-failure");
                entered.Set(); release.WaitOne(); saved = bytes; return new PreferenceWriteResult(PreferenceWriteStatus.Saved, "saved", null);
            }), () => now);
            try
            {
                string pair = new string('f', 64); time.BeginSession(1); time.UsePair(pair); DeathHistoryWorkerChecks.Wait(() => { time.Poll(); return time.Known; });
                time.Advance(100); now = 15000; time.Poll(); DeathHistoryWorkerChecks.Wait(() => { time.Poll(); return time.Error != null; });
                now = 20000; time.Poll(); DeathArchiveChecks.Require(entered.WaitOne(3000), "retry is in flight before leaving");
                time.EndSession(); release.Set(); time.BeginSession(2); time.UsePair(pair);
                DeathHistoryWorkerChecks.Wait(() => { time.Poll(); return time.Known || time.Error != null; });
                DeathArchiveChecks.Require(time.Known && time.Total == 100 && time.Error == null && !time.CommitUnconfirmed, "successful final retry clears earlier known failure");
            }
            finally { release.Set(); DeathArchiveChecks.Require(time.Stop(3000), "successful retry drain"); entered.Dispose(); release.Dispose(); }
        }
        private sealed class RetryTime : IPreferenceStorage
        {
            private readonly Func<byte[]> read; private readonly Func<byte[], PreferenceWriteResult> write;
            internal RetryTime(Func<byte[]> read, Func<byte[], PreferenceWriteResult> write) { this.read = read; this.write = write; }
            public PreferenceReadResult Read() { byte[] bytes = read(); return new PreferenceReadResult(bytes == null ? PreferenceReadStatus.Missing : PreferenceReadStatus.Loaded, bytes, bytes == null ? "missing" : "saved", null); }
            public PreferenceWriteResult Write(string identity, byte[] bytes) { return write(bytes); }
            public void Dispose() { }
        }
    }
}
