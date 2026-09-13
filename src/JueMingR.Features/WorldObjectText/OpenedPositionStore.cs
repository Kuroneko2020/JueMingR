using System;
using System.Collections.Generic;
using System.Threading;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.WorldObjectText
{
    // One in-flight thread-pool operation for this record domain, no dedicated
    // feature/pair threads. Only immutable coordinate batches cross the gate.
    internal sealed class OpenedPositionStore
    {
        internal sealed class State
        {
            internal State(OpenedPositionIndex index, bool loaded, PreferenceStatus status, string error = null, bool protection = false, bool unconfirmed = false)
            { Index = index; Loaded = loaded; Status = status; Error = error; Protected = protection; Unconfirmed = unconfirmed; }
            internal readonly OpenedPositionIndex Index;
            internal readonly bool Loaded, Protected, Unconfirmed;
            internal readonly PreferenceStatus Status;
            internal readonly string Error;
        }
        internal sealed class Lease
        {
            internal string Key, Identity;
            internal IPreferenceStorage Storage; // Worker only, including Dispose.
            internal State Current = new State(new OpenedPositionIndex(), false, PreferenceStatus.Loading);
            internal HashSet<long> Pending = new HashSet<long>();
            internal List<Deferred> Deferred = new List<Deferred>();
            internal List<OpenedPositionIndex> RetryingOverlays = new List<OpenedPositionIndex>();
            // Game-thread accepted facts remain queryable on same-pair reentry
            // while the separately published disk snapshot is still pending.
            internal readonly OpenedPositionIndex Accepted = new OpenedPositionIndex();
            internal OpenedPositionIndex[] Overlays = new OpenedPositionIndex[0];
            internal bool InUse = true, Writable, Read, AdmissionBlocked;
            internal int Failures, Reservation, InFlight;
            internal DateTime Due, FirstPending;
        }
        internal sealed class Deferred { internal List<long> Values; internal int Start; internal OpenedPositionIndex Index; }
        private readonly object gate = new object();
        private readonly Func<string, IPreferenceStorage> factory;
        private readonly Dictionary<string, Lease> leases = new Dictionary<string, Lease>(StringComparer.Ordinal);
        private readonly OpenedPositionCodec codec = new OpenedPositionCodec();
        private readonly Timer timer;
        private readonly ManualResetEventSlim finished = new ManualResetEventSlim(true);
        private const int MaximumQueuedUnits = 1048576;
        private int queuedUnits, overlayUnits;
        private bool running, stopping, cancelled, completed;
        private string lastFailure, admissionFailure;
        private readonly Dictionary<string, string> pairFailures = new Dictionary<string, string>(StringComparer.Ordinal);
        internal OpenedPositionStore(Func<string, IPreferenceStorage> factory)
        { this.factory = factory ?? throw new ArgumentNullException(nameof(factory)); timer = new Timer(Drain, null, Timeout.Infinite, Timeout.Infinite); }
        internal bool Terminated { get { lock (gate) return cancelled; } }
        internal string TakeFailure()
        {
            lock (gate)
            {
                if (admissionFailure != null) { string value = admissionFailure; admissionFailure = null; return value; }
                string key = null, valueForPair = null;
                foreach (var entry in pairFailures) { key = entry.Key; valueForPair = entry.Value; break; }
                if (key != null) { pairFailures.Remove(key); return valueForPair; }
                string failure = lastFailure; lastFailure = null; return failure;
            }
        }
        internal bool HasPending(Lease lease) { lock (gate) return lease.Reservation != 0 || lease.InFlight != 0; }
        internal void ReadView(Lease lease, out State state, out OpenedPositionIndex[] overlays)
        { lock (gate) { state = lease.Current; overlays = lease.Overlays; } }
        internal Lease Acquire(string key)
        {
            lock (gate)
            {
                if (stopping || cancelled || completed) return null;
                Lease value;
                if (!leases.TryGetValue(key, out value))
                {
                    if (leases.Count == 8) return null;
                    value = new Lease { Key = key }; leases.Add(key, value);
                }
                value.InUse = true; RetryLocked(value); ScheduleLocked(0); return value;
            }
        }
        internal static State Snapshot(Lease lease) { return Volatile.Read(ref lease.Current); }
        internal bool Add(Lease lease, long key)
        {
            lock (gate)
            {
                if (stopping || cancelled || completed || lease.Current.Protected) return false;
                if (lease.Accepted.Contains(key) || lease.Current.Index.Contains(key)) return true;
                if (queuedUnits == MaximumQueuedUnits || lease.Accepted.Count == OpenedPositionIndex.MaximumPositions)
                { if (!lease.AdmissionBlocked) admissionFailure = "opened-queue-capacity-session-only"; lease.AdmissionBlocked = true; return false; }
                if (!lease.Pending.Add(key)) return true;
                lease.AdmissionBlocked = false;
                queuedUnits++; lease.Reservation++; lease.Accepted.Add(key);
                DateTime now = DateTime.UtcNow;
                if (lease.Pending.Count == 1) lease.FirstPending = now.AddMilliseconds(1000);
                lease.Due = Earlier(now.AddMilliseconds(150), lease.FirstPending); ScheduleLocked(150); return true;
            }
        }
        internal void Release(Lease lease, List<long> frozenRemainder, int start, OpenedPositionIndex frozenIndex)
        {
            lock (gate)
            {
                // The caller has ended that session and transfers this list's
                // ownership. No H-sized copy or enumeration occurs on the game thread.
                if (frozenRemainder != null && start < frozenRemainder.Count)
                {
                    // Reserve the full retained List, including any sent prefix,
                    // not merely its tail. In-flight reservations remain charged.
                    if (!cancelled && !completed && !lease.Current.Protected && frozenRemainder.Count <= MaximumQueuedUnits - queuedUnits &&
                        lease.Overlays.Length < 8 && frozenIndex.Count <= MaximumQueuedUnits - overlayUnits)
                    {
                        queuedUnits += frozenRemainder.Count; lease.Reservation += frozenRemainder.Count;
                        lease.Deferred.Add(new Deferred { Values = frozenRemainder, Start = start, Index = frozenIndex });
                        // Transfer the existing spatial index too: identity can
                        // arrive shortly before exit, leaving more than one 64-row
                        // replay batch. Same-pair reentry must see that tail now.
                        var overlays = new OpenedPositionIndex[lease.Overlays.Length + 1];
                        Array.Copy(lease.Overlays, overlays, lease.Overlays.Length); overlays[overlays.Length - 1] = frozenIndex;
                        overlayUnits += frozenIndex.Count; lease.Overlays = overlays;
                    }
                    else { admissionFailure = "opened-unaccepted-tail-session-only"; }
                }
                lease.InUse = false; lease.Due = DateTime.UtcNow; ScheduleLocked(0);
            }
        }
        internal void Retry(Lease lease) { lock (gate) { if (!cancelled && !stopping && !completed) { RetryLocked(lease); ScheduleLocked(0); } } }
        private static void RetryLocked(Lease lease)
        { if (!lease.Current.Protected && lease.Current.Status == PreferenceStatus.IoFailure) { lease.Failures = 0; lease.Due = DateTime.UtcNow; } }
        internal bool Stop(int milliseconds)
        {
            lock (gate)
            {
                if (completed) return true;
                if (cancelled) return false;
                stopping = true; finished.Reset();
                foreach (var lease in leases.Values) { lease.InUse = false; lease.Due = DateTime.UtcNow; }
                ScheduleLocked(0);
            }
            if (finished.Wait(Math.Max(0, milliseconds)))
            { lock (gate) { if (cancelled) return false; completed = true; timer.Dispose(); } return true; }
            lock (gate) { cancelled = true; ScheduleLocked(0); }
            return false; // An already-entered OS write retains worker ownership.
        }
        private void ScheduleLocked(int delay)
        { if (!cancelled && !completed) timer.Change(Math.Max(0, delay), Timeout.Infinite); }
        private void Drain(object unused)
        {
            lock (gate) { if (running || completed) return; running = true; }
            try
            {
                while (true)
                {
                    Lease lease = null; HashSet<long> batch = null; List<Deferred> deferred = null; List<OpenedPositionIndex> overlays = null; int reservation = 0;
                    lock (gate)
                    {
                        if (cancelled) break;
                        DateTime now = DateTime.UtcNow;
                        foreach (var candidate in leases.Values)
                            if (!candidate.Read || (!candidate.InUse && candidate.Pending.Count == 0 && candidate.Deferred.Count == 0) ||
                                (candidate.Pending.Count != 0 || candidate.Deferred.Count != 0) && (stopping || candidate.Due <= now))
                            { lease = candidate; break; }
                        if (lease == null) break;
                        batch = lease.Pending; lease.Pending = new HashSet<long>();
                        deferred = lease.Deferred; lease.Deferred = new List<Deferred>();
                        overlays = lease.RetryingOverlays; lease.RetryingOverlays = new List<OpenedPositionIndex>();
                        reservation = lease.Reservation; lease.Reservation = 0; lease.InFlight = reservation;
                    }
                    foreach (var tail in deferred) { overlays.Add(tail.Index); for (int i = tail.Start; i < tail.Values.Count; i++) batch.Add(tail.Values[i]); }
                    bool retry;
                    int retained = Process(lease, batch, out retry);
                    bool close;
                    lock (gate)
                    {
                        queuedUnits -= reservation - retained;
                        lease.InFlight = 0;
                        // Retry carries the frozen-index ownership, not its old
                        // source List. Success releases the exact segments it
                        // covered even when the retry consisted only of keys.
                        if (retry) lease.RetryingOverlays.AddRange(overlays);
                        else if (lease.Current.Status == PreferenceStatus.Saved && overlays.Count != 0)
                        {
                            var remaining = new List<OpenedPositionIndex>(lease.Overlays);
                            foreach (var overlay in overlays) if (remaining.Remove(overlay)) overlayUnits -= overlay.Count;
                            lease.Overlays = remaining.ToArray();
                        }
                        // Stop accelerates due times, but cannot erase B that was
                        // accepted while A was in flight. Known write failures get
                        // their finite retries before unresolved exit is reported.
                        close = !lease.InUse && (lease.Current.Protected || lease.Pending.Count == 0 && lease.Deferred.Count == 0 || stopping && lease.Failures >= 3);
                        if (close) { queuedUnits -= lease.Reservation; lease.Reservation = 0; foreach (var overlay in lease.Overlays) overlayUnits -= overlay.Count; leases.Remove(lease.Key); }
                    }
                    if (close) lease.Storage?.Dispose();
                }
            }
            catch (Exception)
            { lock (gate) { lastFailure = "opened-store-worker-failed"; cancelled = true; } }
            finally
            {
                // File-handle cleanup remains here, never in game callbacks.
                if (cancelled)
                {
                    Lease[] remaining;
                    lock (gate) { remaining = new List<Lease>(leases.Values).ToArray(); leases.Clear(); queuedUnits = overlayUnits = 0; }
                    foreach (var lease in remaining) { try { lease.Storage?.Dispose(); } catch { } }
                }
                lock (gate)
                {
                    running = false;
                    if (stopping && leases.Count == 0 || cancelled) finished.Set();
                    else
                    {
                        DateTime earliest = DateTime.MaxValue;
                        foreach (var lease in leases.Values)
                            if (!lease.Read || !lease.InUse && lease.Pending.Count == 0 && lease.Deferred.Count == 0) { earliest = DateTime.UtcNow; break; }
                            else if (lease.Pending.Count != 0 || lease.Deferred.Count != 0) earliest = Earlier(earliest, lease.Due);
                        if (earliest != DateTime.MaxValue) ScheduleLocked((int)Math.Max(0, Math.Min(1000, (earliest - DateTime.UtcNow).TotalMilliseconds)));
                    }
                }
            }
        }
        private int Process(Lease lease, HashSet<long> batch, out bool retry)
        {
            retry = false;
            bool writeEntered = false;
            try
            {
                if (!lease.Read)
                {
                    lease.Storage = factory(lease.Key); PreferenceReadResult read = lease.Storage.Read();
                    lease.Read = true; lease.Identity = read.Identity;
                    if (read.Status != PreferenceReadStatus.Missing && read.Status != PreferenceReadStatus.Loaded)
                    {
                        Publish(lease, new State(new OpenedPositionIndex(), true,
                            read.Status == PreferenceReadStatus.Busy ? PreferenceStatus.Busy : read.Status == PreferenceReadStatus.TooLarge ? PreferenceStatus.Invalid : PreferenceStatus.IoFailure,
                            read.Error ?? "opened-load-failed", true)); return 0;
                    }
                    var index = read.Status == PreferenceReadStatus.Missing ? new OpenedPositionIndex() : codec.Decode(read.Contents, lease.Key);
                    lease.Writable = true;
                    Publish(lease, new State(index, true, read.Status == PreferenceReadStatus.Missing ? PreferenceStatus.Missing : PreferenceStatus.Saved));
                }
                if (batch.Count == 0 || !lease.Writable) return 0;
                var previous = Snapshot(lease).Index; var candidate = new OpenedPositionIndex(previous.All);
                foreach (long key in batch) candidate.Add(key);
                if (candidate.Count == previous.Count) return 0;
                byte[] bytes = codec.Encode(lease.Key, candidate); codec.Decode(bytes, lease.Key);
                lock (gate) { if (cancelled) return 0; }
                writeEntered = true;
                PreferenceWriteResult written = lease.Storage.Write(lease.Identity, bytes);
                if (written.Status == PreferenceWriteStatus.Saved)
                {
                    lease.Identity = written.Identity; lease.Failures = 0;
                    Publish(lease, new State(candidate, true, PreferenceStatus.Saved));
                }
                else
                {
                    if (written.IsProtected || written.CommitUnconfirmed) lease.Writable = false;
                    Publish(lease, new State(previous, true, written.Status == PreferenceWriteStatus.Conflict ? PreferenceStatus.Conflict : written.Status == PreferenceWriteStatus.Busy ? PreferenceStatus.Busy : PreferenceStatus.IoFailure,
                        written.Error ?? "opened-write-failed", written.IsProtected, written.CommitUnconfirmed));
                    if (lease.Writable)
                        lock (gate)
                        {
                            // A concurrent same-pair replay may already have
                            // enqueued every key. Zero new reservation still
                            // means the failed batch's overlays need that retry.
                            retry = true;
                            int kept = 0; foreach (long key in batch) if (lease.Pending.Add(key)) kept++;
                            lease.Reservation += kept;
                            lease.Failures++; lease.Due = lease.Failures < 3 ? DateTime.UtcNow.AddMilliseconds(250 * lease.Failures) : DateTime.MaxValue;
                            return kept;
                        }
                }
            }
            catch (PreferenceFormatException e)
            { lease.Read = true; lease.Writable = false; Publish(lease, new State(Snapshot(lease).Index, true, e.Status, "opened-format-" + e.Status, true)); }
            catch (Exception)
            { lease.Read = true; lease.Writable = false; Publish(lease, new State(Snapshot(lease).Index, true, PreferenceStatus.IoFailure, writeEntered ? "opened-write-threw" : "opened-load-or-prepare-failed", true, writeEntered)); }
            return 0;
        }
        private void Publish(Lease lease, State state)
        {
            lock (gate)
            {
                if (state.Error != null && state.Error != lease.Current.Error)
                {
                    // At most eight pending pair notices. Recovery of B cannot
                    // erase A; repeated polls/failures of A do not chat-spam.
                    if (pairFailures.ContainsKey(lease.Key) || pairFailures.Count < 8) pairFailures[lease.Key] = state.Error;
                    else lastFailure = "opened-additional-pair-save-failures";
                }
                else if (state.Error == null) pairFailures.Remove(lease.Key);
                Volatile.Write(ref lease.Current, state);
            }
        }
        private static DateTime Earlier(DateTime a, DateTime b) { return a < b ? a : b; }
    }
}
