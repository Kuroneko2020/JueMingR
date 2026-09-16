using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using JueMingR.Features.Footprints;
using JueMingR.Infrastructure.Footprints;
using JueMingR.Platform.Footprints;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class FootprintWorkerChecks
    {
        internal static void Check(IList<string> failures)
        {
            string area = Path.Combine(Path.GetTempPath(), "JueMingR-footprint-worker-" + Guid.NewGuid().ToString("N"));
            FootprintStore worker = null;
            var entered = new ManualResetEventSlim(); var resume = new ManualResetEventSlim();
            try
            {
                string pair = new string('d', 64); var files = new DelayedFiles(new FileFootprintArchive(area, pair), entered, resume);
                worker = new FootprintStore(() => files, pair, 100, 100); Until(() => worker.Snapshot.Loaded, "initial file worker load");
                string generation = worker.Snapshot.Generation;
                FootprintCoreChecks.Require(worker.Offer(new[] { Sample(1) }, 1), "first batch accepted");
                FootprintCoreChecks.Require(entered.Wait(3000), "real root write controlled boundary reached");
                for (int i = 2; i <= 9; i++) FootprintCoreChecks.Require(worker.Offer(new[] { Sample(i) }, 1), "eight finite queued batches");
                FootprintCoreChecks.Require(!worker.Offer(new[] { Sample(10) }, 1) && worker.PendingBatches == 8, "slow write cannot create unbounded backlog");
                FootprintCoreChecks.Require(worker.Clear(generation, Guid.NewGuid().ToString("N")), "third-stage command accepted behind real in-flight save");
                FootprintCoreChecks.Require(!worker.Offer(new[] { Sample(10) }, 1), "old producer rejected as soon as clear is accepted");
                resume.Set(); Until(() => worker.Snapshot.Generation != generation, "clear completes after in-flight write");
                FootprintCoreChecks.Require(worker.Snapshot.Count == 0 && !Directory.Exists(Path.Combine(area, pair, generation)), "old writer cannot resurrect deleted generation");
                FootprintCoreChecks.Require(worker.Offer(new[] { Sample(1) }, 1), "recording may start fresh");
                Until(() => worker.Snapshot.Count == 1, "fresh archive commits"); worker.RequestQuery(1);
                Until(() => worker.QueryResult != null, "query publishes new generation");
                FootprintCoreChecks.Require(worker.QueryResult.Generation == worker.Snapshot.Generation && !worker.Clear(generation, Guid.NewGuid().ToString("N")), "old clear rejected after new generation");
                worker.Dispose(); Until(() => worker.Finished, "bounded normal stop");
                FaultRecovery(area); UnknownCommit(area); ClearRootRetry(area); CancelLoad(area); ClearIntentRetry(area); QueryFailure(area);
            }
            catch (Exception e) { failures.Add("footprint real worker: " + e); }
            finally
            {
                resume.Set(); if (worker != null) { worker.Dispose(); Until(() => worker.Finished, "test worker cleanup"); }
                entered.Dispose(); resume.Dispose(); if (Directory.Exists(area)) Directory.Delete(area, true);
            }
        }
        private static FootprintSample Sample(long i) { return new FootprintSample(i, i - 1, i, 1, 2, 3, FootprintPosition.Valid); }
        private static void FaultRecovery(string area)
        {
            string pair = new string('e', 64); var files = new FaultFiles(new FileFootprintArchive(Path.Combine(area, "retry"), pair)) { FailWrites = true };
            var worker = new FootprintStore(() => files, pair, 100, 100);
            try
            {
                Until(() => worker.Snapshot.Loaded, "retry load");
                for (int i = 1; i <= 8; i++) FootprintCoreChecks.Require(worker.Offer(new[] { Sample(i) }, 1), "facts accepted before failure");
                Until(() => worker.Snapshot.Error != null, "finite retry failure published");
                FootprintCoreChecks.Require(files.Writes == 3 && worker.HasUnsavedFacts && !worker.Finished, "three failed attempts retain pending facts and worker lease");
                files.FailWrites = false; worker.RetrySave(); Until(() => worker.Snapshot.Count == 8 && !worker.HasUnsavedFacts, "same worker retries entire accepted FIFO");
                worker.Dispose(); Until(() => worker.Finished, "retry stop");
            }
            finally { files.FailWrites = false; worker.RetrySave(); Until(() => !worker.HasUnsavedFacts, "retry cleanup drain"); worker.Dispose(); Until(() => worker.Finished, "retry cleanup"); }
        }
        private static void UnknownCommit(string area)
        {
            string pair = new string('f', 64);
            using (var files = new FaultFiles(new FileFootprintArchive(Path.Combine(area, "unknown"), pair)) { Unknown = true })
            {
                var archive = new FootprintArchive(files, pair, 100, 100); archive.Load(); bool failed = false;
                try { archive.Append(new[] { Sample(1) }, 1); } catch (IOException) { failed = true; }
                bool rejected = false; try { archive.Append(new[] { Sample(1) }, 1); } catch (InvalidOperationException) { rejected = true; }
                FootprintCoreChecks.Require(failed && rejected && archive.Protected && files.Writes == 1, "unknown commit protects bytes without replay");
            }
        }
        private static void ClearRootRetry(string area)
        {
            string pair = new string('1', 64);
            using (var files = new FaultFiles(new FileFootprintArchive(Path.Combine(area, "newroot"), pair)))
            {
                var archive = new FootprintArchive(files, pair, 100, 100); archive.Load(); archive.Append(new[] { Sample(1) }, 1);
                string old = archive.Generation; archive.BeginClear(old, Guid.NewGuid().ToString("N")); files.FailOpen = true;
                bool failed = false; try { while (!archive.ClearStep(32)) { } } catch (IOException) { failed = true; }
                FootprintCoreChecks.Require(failed && archive.Clearing && archive.Generation == old, "post-catalog failure keeps exact old clear identity");
                files.FailOpen = false; while (!archive.ClearStep(32)) { }
                archive.Append(new[] { Sample(1) }, 1);
                FootprintCoreChecks.Require(archive.Generation != old && !archive.Clearing && archive.Count == 1, "same fresh root reopens after known failure");
            }
        }
        private static void CancelLoad(string area)
        {
            string pair = new string('2', 64); var files = new FaultFiles(new FileFootprintArchive(Path.Combine(area, "cancel"), pair)) { ScanForever = true };
            var worker = new FootprintStore(() => files, pair, 100, 100);
            Until(() => files.Scans > 3, "incremental scan entered"); worker.Dispose(); Until(() => worker.Finished, "recovery cancels between batches");
            FootprintCoreChecks.Require(!worker.Snapshot.Loaded && files.LargestBudget == 32, "cancelled recovery publishes no partial writable archive");
        }
        private static void ClearIntentRetry(string area)
        {
            string pair = new string('3', 64); var files = new FaultFiles(new FileFootprintArchive(Path.Combine(area, "intent"), pair));
            var worker = new FootprintStore(() => files, pair, 100, 100); Until(() => worker.Snapshot.Loaded, "intent load");
            string old = worker.Snapshot.Generation; files.FailCatalog = true;
            worker.Clear(old, Guid.NewGuid().ToString("N")); Until(() => worker.Snapshot.Error != null, "pre-intent failure");
            FootprintCoreChecks.Require(worker.Snapshot.Clearing && !worker.HasUnsavedFacts && !worker.Finished, "accepted clear remains an in-flight command before durable intent");
            files.FailCatalog = false; worker.RetryClear(); Until(() => worker.Snapshot.Generation != old, "same accepted command retried");
            worker.Dispose(); Until(() => worker.Finished, "intent cleanup");
        }
        private static void QueryFailure(string area)
        {
            string pair = new string('4', 64), path = Path.Combine(area, "query");
            using (var seed = new FileFootprintArchive(path, pair))
            {
                var archive = new FootprintArchive(seed, pair, 100, 100); archive.Load();
                for (int n = 0; n < 66; n++) { var batch = new FootprintSample[256]; for (int i = 0; i < batch.Length; i++) batch[i] = Sample(n * 256 + i + 1); archive.Append(batch, batch.Length); }
            }
            var files = new FaultFiles(new FileFootprintArchive(path, pair)); var worker = new FootprintStore(() => files, pair, 100, 100);
            Until(() => worker.Snapshot.Loaded, "query load"); files.FailRead = true; worker.RequestQuery(0);
            Until(() => worker.Snapshot.Error != null, "query read failure surfaced");
            FootprintCoreChecks.Require(worker.Snapshot.Protected && !worker.Snapshot.RetryableSave && !worker.RetrySave() && !worker.Offer(new[] { Sample(16897) }, 1), "historical read failure cannot be dismissed as a save retry");
            worker.Dispose(); Until(() => worker.Finished, "query failure cleanup");
        }
        private sealed class FaultFiles : IFootprintArchiveFiles
        {
            private readonly IFootprintArchiveFiles inner;
            internal volatile bool FailWrites, Unknown, FailOpen, ScanForever, FailCatalog, FailRead;
            internal volatile int Writes, Scans, LargestBudget;
            internal FaultFiles(IFootprintArchiveFiles inner) { this.inner = inner; }
            public PreferenceReadResult ReadCatalog() { return inner.ReadCatalog(); }
            public PreferenceWriteResult WriteCatalog(string i, byte[] b) { return FailCatalog ? new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, i, "injected-intent-write") : inner.WriteCatalog(i, b); }
            public PreferenceReadResult OpenRoot(string g) { if (FailOpen) throw new IOException("injected-new-root-open"); return inner.OpenRoot(g); }
            public PreferenceWriteResult WriteRoot(string i, byte[] b) { Writes++; return FailWrites || Unknown ? new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, i, "injected-root-write", Unknown) : inner.WriteRoot(i, b); }
            public byte[] ReadBlock(long n) { if (FailRead) throw new IOException("injected-history-read"); return inner.ReadBlock(n); }
            public void CreateBlock(long n, byte[] b) { inner.CreateBlock(n, b); }
            public bool ValidateBlockNamesStep(long n, int budget) { Scans++; LargestBudget = Math.Max(LargestBudget, budget); if (ScanForever) { Thread.Sleep(1); return false; } return inner.ValidateBlockNamesStep(n, budget); }
            public bool DeleteGenerationStep(string g, int n) { return inner.DeleteGenerationStep(g, n); }
            public void Dispose() { inner.Dispose(); }
        }
        private static void Until(Func<bool> predicate, string why) { var clock = Stopwatch.StartNew(); while (!predicate()) { if (clock.ElapsedMilliseconds > 5000) throw new TimeoutException(why); Thread.Sleep(5); } }
        private sealed class DelayedFiles : IFootprintArchiveFiles
        {
            private readonly IFootprintArchiveFiles inner; private readonly ManualResetEventSlim entered, resume; private bool first = true;
            internal DelayedFiles(IFootprintArchiveFiles inner, ManualResetEventSlim entered, ManualResetEventSlim resume) { this.inner = inner; this.entered = entered; this.resume = resume; }
            public PreferenceReadResult ReadCatalog() { return inner.ReadCatalog(); }
            public PreferenceWriteResult WriteCatalog(string i, byte[] b) { return inner.WriteCatalog(i, b); }
            public PreferenceReadResult OpenRoot(string g) { return inner.OpenRoot(g); }
            public PreferenceWriteResult WriteRoot(string i, byte[] b) { if (first) { first = false; entered.Set(); if (!resume.Wait(5000)) throw new TimeoutException("controlled write release missing"); } return inner.WriteRoot(i, b); }
            public byte[] ReadBlock(long n) { return inner.ReadBlock(n); }
            public void CreateBlock(long n, byte[] b) { inner.CreateBlock(n, b); }
            public bool ValidateBlockNamesStep(long n, int budget) { return inner.ValidateBlockNamesStep(n, budget); }
            public bool DeleteGenerationStep(string g, int n) { return inner.DeleteGenerationStep(g, n); }
            public void Dispose() { inner.Dispose(); }
        }
    }
}
