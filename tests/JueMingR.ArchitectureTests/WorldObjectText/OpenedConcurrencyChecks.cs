using System;
using System.IO;
using System.Reflection;
using System.Threading;
using JueMingR.Features.WorldObjectText;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.ArchitectureTests
{
    internal static class OpenedConcurrencyChecks
    {
        internal static void Run()
        {
#if DEBUG
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-opened-concurrency-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); string pair = new string('d', 64), path = Path.Combine(root, "record.json");
            var readEntered = new ManualResetEventSlim(); var readRelease = new ManualResetEventSlim();
            var writeEntered = new ManualResetEventSlim(); var writeRelease = new ManualResetEventSlim();
            var retryEntered = new ManualResetEventSlim(); var retryRelease = new ManualResetEventSlim(); var foregroundDone = new ManualResetEventSlim();
            var storage = new HeldFirstWrite(new AtomicFileDocument(path, OpenedPositionCodec.MaximumBytes, true), readEntered, readRelease, writeEntered, writeRelease);
            var owner = new OpenedPositionHistory(_ => storage);
            var store = typeof(OpenedPositionHistory).GetField("store", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
            var retryProbe = store.GetType().GetProperty("DebugRetryCoordinateVisit", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(retryProbe != null, "Debug retry probe is present in the actual Features assembly");
            int visits = 0, workerThread = 0, foregroundThread = 0; bool locked = false, coordinationTimedOut = false, stopped = false;
            Exception foregroundError = null; Thread foreground = null;
            retryProbe.SetValue(store, (Action<bool>)(holdsGate =>
            {
                if (Interlocked.Increment(ref visits) != 1) return;
                locked = holdsGate; workerThread = Thread.CurrentThread.ManagedThreadId; retryEntered.Set();
                coordinationTimedOut = !retryRelease.Wait(10000);
            }));
            try
            {
                owner.BeginSession(1); owner.UsePair(pair); Require(readEntered.Wait(5000), "real worker entered isolated read");
                for (int i = 0; i < 4096; i++) Require(owner.Opened(i, 10), "initial batch accepted");
                readRelease.Set(); Require(writeEntered.Wait(5000), "large batch entered first write");
                Require(owner.Opened(5000, 10) && owner.Opened(5001, 10), "new pending facts coexist with in-flight batch");
                owner.EndSession(); owner.BeginSession(2); owner.UsePair(pair); // Empty delta forces real shared-store reads.
                writeRelease.Set(); Require(retryEntered.Wait(5000), "actual retry coordinate probe must execute");
                foreground = new Thread(() =>
                {
                    try
                    {
                        foregroundThread = Thread.CurrentThread.ManagedThreadId; owner.Poll();
                        Require(owner.Contains(WorldObject.PositionKey(0, 10)), "same-pair accepted facts survive retry");
                        var query = owner.Query(new WorldTargetView(0, 0, 512, 20, 1));
                        Require(query.MoveNext(64) == OpenedQueryStep.Position, "query remains available during retry work");
                        Require(owner.Opened(5002, 10) && !owner.Opened(5002, 10), "new fact accepted once during retry work");
                        var status = owner.Status; Require(status != PreferenceStatus.Stopped, "status remains available");
                    }
                    catch (Exception e) { foregroundError = e; }
                    finally { foregroundDone.Set(); }
                });
                foreground.Start();
                // The timeout is a deadlock guard, not a latency/performance budget.
                bool progressed = foregroundDone.Wait(5000);
                Require(progressed && !locked && foregroundThread != workerThread, "foreground must finish while real retry work is held outside shared gate");
                if (foregroundError != null) throw foregroundError;
                Require((int)store.GetType().GetField("queuedUnits", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(store) == 4099,
                    "retry ownership transfer and duplicate admission preserve the exact queued reservation");
                retryRelease.Set(); Require(owner.Stop(5000), "bounded stop drains retry and later fact"); stopped = true;
                var actual = new OpenedPositionCodec().Decode(File.ReadAllBytes(path), pair);
                Require(actual.Count == 4099 && actual.Contains(WorldObject.PositionKey(5002, 10)) && actual.Contains(WorldObject.PositionKey(4095, 10)), "real atomic file contains each old and concurrent position exactly once");
                Require(!coordinationTimedOut && storage.Writes >= 2, "controlled known failure recovered through real write");
                Console.WriteLine("PASS: opened retry work outside gate; foreground Poll/Query/Opened/Status progresses; 4099 unique positions persisted.");
            }
            finally
            {
                readRelease.Set(); writeRelease.Set(); retryRelease.Set();
                if (foreground != null) foreground.Join(5000);
                if (!stopped) stopped = owner.Stop(5000);
                if (stopped)
                {
                    readEntered.Dispose(); readRelease.Dispose(); writeEntered.Dispose(); writeRelease.Dispose(); retryEntered.Dispose(); retryRelease.Dispose(); foregroundDone.Dispose();
                    string resolved = Path.GetFullPath(root), temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("JueMingR-opened-concurrency-", StringComparison.Ordinal)) Directory.Delete(resolved, true);
                }
            }
#else
            throw new InvalidOperationException("Opened concurrency workload requires the Debug retry probe.");
#endif
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private sealed class HeldFirstWrite : IPreferenceStorage
        {
            private readonly IPreferenceStorage inner;
            private readonly ManualResetEventSlim readEntered, readRelease, writeEntered, writeRelease;
            internal int Writes;
            internal HeldFirstWrite(IPreferenceStorage inner, ManualResetEventSlim readEntered, ManualResetEventSlim readRelease, ManualResetEventSlim writeEntered, ManualResetEventSlim writeRelease)
            { this.inner = inner; this.readEntered = readEntered; this.readRelease = readRelease; this.writeEntered = writeEntered; this.writeRelease = writeRelease; }
            public PreferenceReadResult Read() { readEntered.Set(); if (!readRelease.Wait(10000)) throw new TimeoutException("test read coordination"); return inner.Read(); }
            public PreferenceWriteResult Write(string identity, byte[] bytes)
            {
                if (Interlocked.Increment(ref Writes) == 1)
                {
                    writeEntered.Set(); if (!writeRelease.Wait(10000)) throw new TimeoutException("test write coordination");
                    return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, identity, "test-known-precommit-failure");
                }
                return inner.Write(identity, bytes);
            }
            public void Dispose() { inner.Dispose(); }
        }
    }
}
