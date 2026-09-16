using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JueMingR.Platform.Footprints;

namespace JueMingR.Features.Footprints
{
    public sealed class FootprintStoreState
    {
        internal FootprintStoreState(bool loaded, string generation, long count, long end, long segment, bool clearing, bool stopped, string error, bool protectedWrite, bool retryableSave = false)
        { Loaded = loaded; Generation = generation; Count = count; End = end; Segment = segment; Clearing = clearing; Stopped = stopped; Error = error; Protected = protectedWrite; RetryableSave = retryableSave; }
        public bool Loaded { get; }
        public string Generation { get; }
        public long Count { get; }
        public long End { get; }
        public long Segment { get; }
        public bool Clearing { get; }
        public bool Stopped { get; }
        public string Error { get; }
        public bool Protected { get; }
        public bool RetryableSave { get; }
    }

    // Bounded FIFO for facts; a replaceable slot only for viewport requests.
    // File I/O and deletion never run while holding the foreground handoff lock.
    public sealed class FootprintStore : IDisposable
    {
        public const int QueueCapacity = 8;
        private readonly object gate = new object();
        private readonly Queue<FootprintSample[]> queue = new Queue<FootprintSample[]>();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Func<IFootprintArchiveFiles> open;
        private readonly string pair;
        private readonly int width, height;
        private FootprintStoreState state = new FootprintStoreState(false, null, 0, 0, 0, false, false, null, false);
        private FootprintQuery query;
        private bool stopping, finished, clearRequested, clearPending, retry, retrySave;
        private FootprintSample[] pending;
        private string clearGeneration, clearOperation;
        private long querySerial, deliveredSerial, cursor;
        public FootprintStore(Func<IFootprintArchiveFiles> open, string pair, int width, int height)
        {
            this.open = open ?? throw new ArgumentNullException(nameof(open)); this.pair = pair; this.width = width; this.height = height;
            new Thread(Run) { IsBackground = true, Name = "JueMingR.Footprints" }.Start();
        }
        public FootprintStoreState Snapshot { get { lock (gate) return state; } }
        public FootprintQuery QueryResult { get { lock (gate) return query; } }
        public int PendingBatches { get { lock (gate) return queue.Count; } }
        public bool Finished { get { lock (gate) return finished; } }
        public bool HasUnsavedFacts { get { lock (gate) return pending != null || queue.Count != 0; } }
        public bool RetrySave()
        { lock (gate) { if (!finished && !stopping && state.RetryableSave && !state.Protected && !state.Clearing) { retrySave = true; wake.Set(); return true; } return false; } }
        public bool Offer(FootprintSample[] values, int count)
        {
            if (values == null || count < 1 || count > 256 || count > values.Length) throw new ArgumentException("footprint-offer-size");
            lock (gate)
            {
                if (!state.Loaded || state.Error != null || state.Clearing || clearPending || stopping || finished || queue.Count >= QueueCapacity) return false;
                var frozen = new FootprintSample[count]; Array.Copy(values, frozen, count); queue.Enqueue(frozen); wake.Set(); return true;
            }
        }
        public void RequestQuery(long value)
        { lock (gate) { if (finished || stopping || clearRequested || state.Clearing) return; cursor = value; querySerial++; wake.Set(); } }
        public void CancelQuery() { lock (gate) { deliveredSerial = ++querySerial; query = null; } }
        public bool Clear(string generation, string operation)
        {
            lock (gate)
            {
                if (!state.Loaded || finished || stopping || state.Protected || generation != state.Generation) return false;
                if (clearRequested || state.Clearing) return false;
                clearGeneration = generation; clearOperation = operation; clearRequested = clearPending = true; query = null; deliveredSerial = ++querySerial;
                // The third confirmation explicitly authorizes discarding the old
                // accepted tail. An in-flight save finishes before the intent write.
                queue.Clear(); wake.Set(); return true;
            }
        }
        public void RetryClear()
        { lock (gate) { if (!finished && !stopping && state.Clearing && !state.Protected) { retry = clearRequested = true; wake.Set(); } } }
        public void Dispose() { lock (gate) { if (finished || stopping) return; stopping = true; deliveredSerial = ++querySerial; query = null; wake.Set(); } }
        private void Run()
        {
            FootprintArchive archive = null;
            try
            {
                using (var files = open())
                {
                    archive = new FootprintArchive(files, pair, width, height);
                    while (!archive.LoadStep()) { lock (gate) if (stopping) return; }
                    Publish(archive, null);
                    string failure = null; int attempts = 0; bool clearFailed = false;
                    while (true)
                    {
                        bool clear, stop, retryNow; long serial, target; string generation, operation;
                        lock (gate)
                        {
                            clear = clearRequested; clearRequested = false; generation = clearGeneration; operation = clearOperation;
                            stop = stopping; retryNow = retry; retry = false; serial = querySerial; target = cursor;
                            if (retrySave && !archive.Protected && !archive.Clearing) { failure = null; attempts = 0; }
                            retrySave = false;
                            if (!clear && !archive.Clearing && failure == null && pending == null && queue.Count > 0) pending = queue.Dequeue();
                        }
                        if (clear)
                        {
                            pending = null; failure = null; attempts = 0;
                            try { archive.BeginClear(generation, operation); clearFailed = false; }
                            catch (Exception e) when (Expected(e)) { failure = e.Message; clearFailed = true; }
                            Publish(archive, failure);
                        }
                        if (archive.Clearing && (!clearFailed || retryNow))
                        {
                            try
                            {
                                bool done = archive.ClearStep(32); failure = null; clearFailed = false;
                                if (done) { lock (gate) { clearPending = false; query = null; deliveredSerial = ++querySerial; } }
                                Publish(archive, null);
                                if (!done) continue;
                            }
                            catch (Exception e) when (Expected(e)) { failure = e.Message; clearFailed = true; Publish(archive, failure); }
                        }
                        if (!archive.Clearing && pending != null && failure == null)
                        {
                            try { archive.Append(pending, pending.Length); pending = null; attempts = 0; Publish(archive, null); }
                            catch (Exception e) when (Expected(e))
                            {
                                // Ambiguous/protected commits never enter the retry loop.
                                if (!archive.Protected && !(e is InvalidDataException) && ++attempts < 3) { wake.WaitOne(1000); continue; }
                                failure = e.Message; Publish(archive, failure, !archive.Protected && !(e is InvalidDataException), e is InvalidDataException);
                            }
                        }
                        bool wantQuery; lock (gate) wantQuery = serial != deliveredSerial && !stopping && !clearRequested;
                        if (wantQuery && !archive.Clearing && failure == null)
                        {
                            try
                            {
                                var result = archive.Query(target);
                                lock (gate) { if (serial == querySerial && !clearRequested && !stopping) { query = result; deliveredSerial = serial; } }
                            }
                            // A failed historical read is not an uncommitted save.
                            // Preserve its files and stop writing until a verified
                            // reload; the save retry button must not bypass it.
                            catch (Exception e) when (Expected(e)) { failure = e.Message; Publish(archive, failure, false, true); }
                        }
                        lock (gate)
                        {
                            // A normal session exit may not discard facts already
                            // accepted from the recorder. Failed pending/FIFO data
                            // stays owned here until same-worker recovery or exit.
                            if (stopping && pending == null && queue.Count == 0 && (!archive.Clearing || failure != null)) break;
                            if (failure == null && (queue.Count > 0 || clearRequested || querySerial != deliveredSerial)) continue;
                        }
                        // Clear failure holds the old intent and waits for explicit
                        // retry; it never starts a new archive or spins every frame.
                        if (stop && clearFailed) break;
                        wake.WaitOne();
                    }
                }
            }
            catch (Exception e) when (Expected(e)) { Publish(archive, e.Message); }
            finally
            {
                lock (gate) { finished = true; state = new FootprintStoreState(state.Loaded, state.Generation, state.Count, state.End, state.Segment, state.Clearing, true, state.Error, state.Protected); wake.Dispose(); }
            }
        }
        private void Publish(FootprintArchive archive, string error, bool retryableSave = false, bool protect = false)
        {
            lock (gate) state = archive == null ? new FootprintStoreState(false, null, 0, 0, 0, false, false, error, true) :
                new FootprintStoreState(archive.Loaded, archive.Generation, archive.Count, archive.End, archive.Segment, archive.Clearing || clearPending, false, error, archive.Protected || !archive.Loaded || protect, retryableSave);
        }
        private static bool Expected(Exception e)
        { return e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is InvalidOperationException || e is ArgumentException || e is OverflowException; }
    }
}
