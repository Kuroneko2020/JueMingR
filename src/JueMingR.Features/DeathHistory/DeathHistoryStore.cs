using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JueMingR.Platform.DeathHistory;

namespace JueMingR.Features.DeathHistory
{
    internal sealed class DeathHistoryStore
    {
        internal sealed class Query
        { internal long Id, Offset; internal int Markers; internal string Selected; }
        internal sealed class Handoff
        { internal string Pair; internal bool Retired; internal readonly List<DeathFact> Facts = new List<DeathFact>(); }
        internal sealed class Lease
        {
            internal string Pair;
            internal bool InUse, Closing, Initialized, Known, Conflict;
            internal int Outstanding, Failures;
            internal long Due, Revision;
            internal string Error;
            internal List<DeathFact> Incoming = new List<DeathFact>();
            internal readonly List<DeathFact> Pending = new List<DeathFact>(); // worker only
            internal Query Desired, Processed;
            internal IDeathArchiveFiles Files;
            internal DeathArchive Archive;
            internal DeathHistorySnapshot Current = DeathHistorySnapshot.Empty;
            internal long CachedRevision = -1, CachedOffset = -2;
            internal int CachedMarkers = -1;
            internal string CachedSelected;
            internal IReadOnlyList<DeathFact> Rows = Array.AsReadOnly(new DeathFact[0]);
            internal IReadOnlyList<DeathMarker> Markers = Array.AsReadOnly(new DeathMarker[0]);
            internal DeathFact Selected;
            internal DeathReadText SelectedText;
            internal bool RowsDirty = true, MarkersDirty = true;
        }
        private readonly object gate = new object();
        private readonly List<Lease> leases = new List<Lease>();
        private readonly List<Handoff> handoffs = new List<Handoff>();
        private readonly Func<string, IDeathArchiveFiles> factory;
        private readonly Thread thread;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private bool stopping, eviction;
        private volatile bool cancelled;
        internal string Fatal { get; private set; }
        internal volatile string BackgroundError;
        internal Handoff ReserveHandoff()
        { lock (gate) { if (stopping || handoffs.Count == 8) return null; var value = new Handoff(); handoffs.Add(value); return value; } }
        internal void RetireHandoff(Handoff value)
        {
            if (value == null) return;
            lock (gate)
            {
                // Reservation happens before acceptance. EndSession transfers
                // only this reference; a reliable pair never loses its tail
                // because its archive is still busy with the previous session.
                if (value.Pair == null || value.Facts.Count == 0) handoffs.Remove(value);
                else value.Retired = true;
                Monitor.Pulse(gate);
            }
        }
        internal DeathHistoryStore(Func<string, IDeathArchiveFiles> factory)
        { this.factory = factory ?? throw new ArgumentNullException(nameof(factory)); thread = new Thread(Run) { IsBackground = true, Name = "JueMingR death history" }; thread.Start(); }
        internal Lease Acquire(string pair)
        {
            lock (gate)
            {
                if (stopping) return null;
                foreach (var lease in leases) if (lease.Pair == pair) { if (lease.Closing) return null; lease.InUse = true; return lease; }
                if (leases.Count == 8) { eviction = true; Monitor.Pulse(gate); return null; }
                var added = new Lease { Pair = pair, InUse = true }; leases.Add(added); Monitor.Pulse(gate); return added;
            }
        }
        internal void Release(Lease lease) { lock (gate) { lease.InUse = false; lease.Desired = null; Monitor.Pulse(gate); } }
        internal bool Add(Lease lease, DeathFact fact)
        {
            lock (gate)
            {
                if (stopping || lease.Closing) return false;
                PumpHandoffs();
                foreach (var tail in handoffs) if (tail.Retired && tail.Pair == lease.Pair) return false;
                if (lease.Outstanding == 64) return false;
                // Only references are handed over under this shared gate. Text,
                // index traversal, retry preparation and I/O stay on the worker.
                lease.Incoming.Add(fact); lease.Outstanding++; Monitor.Pulse(gate); return true;
            }
        }
        internal void Request(Lease lease, Query query) { lock (gate) { if (stopping || lease.Closing) return; lease.Desired = query; Monitor.Pulse(gate); } }
        internal DeathHistorySnapshot Snapshot(Lease lease) { return Volatile.Read(ref lease.Current); }
        internal bool Stop(int milliseconds)
        { lock (gate) { stopping = true; Monitor.Pulse(gate); } if (thread.Join(Math.Max(0, milliseconds))) return true; cancelled = true; return false; }
        private void Run()
        {
            try
            {
                while (!cancelled)
                {
                    Lease lease = null; List<DeathFact> incoming = null; Query query = null; bool close = false;
                    lock (gate)
                    {
                        PumpHandoffs();
                        foreach (var item in leases)
                        {
                            if (item.Closing) continue;
                            if (!item.Initialized || item.Incoming.Count != 0 || !ReferenceEquals(item.Desired, item.Processed) ||
                                item.Pending.Count != 0 && item.Known && !item.Archive.IsProtected && item.Failures < 3 && (stopping || clock.ElapsedMilliseconds >= item.Due))
                            { lease = item; break; }
                            if (eviction && !item.InUse && item.Outstanding == 0) { lease = item; close = item.Closing = true; break; }
                        }
                        if (lease == null) { if (stopping) break; Monitor.Wait(gate, NextWait()); continue; }
                        if (!close) { incoming = lease.Incoming; lease.Incoming = new List<DeathFact>(); query = lease.Desired; }
                    }
                    if (close)
                    { lease.Files?.Dispose(); lock (gate) { leases.Remove(lease); eviction = false; } continue; }
                    Process(lease, incoming, query);
                }
            }
            catch (Exception e)
            {
                Fatal = "death-worker-terminated: " + e.GetType().Name;
                lock (gate) { stopping = true; }
            }
            finally
            {
                // Even a timed-out caller never disposes the lease while native
                // file I/O is still executing. This thread remains its owner.
                foreach (var lease in leases) try { lease.Files?.Dispose(); } catch { }
            }
        }
        private void PumpHandoffs()
        {
            // Called under the reference-handoff gate. FIFO preserves actual
            // occurrence order across retired sessions and current acceptance.
            // No encoding, archive traversal or file work is allowed here.
            for (int i = 0; i < handoffs.Count; i++)
            {
                var handoff = handoffs[i]; if (!handoff.Retired) continue;
                Lease target = null;
                foreach (var lease in leases) if (lease.Pair == handoff.Pair && !lease.Closing) { target = lease; break; }
                if (target == null)
                {
                    if (leases.Count == 8) { eviction = true; continue; }
                    target = new Lease { Pair = handoff.Pair }; leases.Add(target);
                }
                int take = Math.Min(64 - target.Outstanding, handoff.Facts.Count);
                for (int n = 0; n < take; n++) target.Incoming.Add(handoff.Facts[n]);
                target.Outstanding += take; handoff.Facts.RemoveRange(0, take);
                if (handoff.Facts.Count == 0) handoffs.RemoveAt(i--);
            }
        }
        private int NextWait()
        {
            long wait = Int32.MaxValue;
            foreach (var item in leases) if (item.Pending.Count != 0 && item.Known && !item.Archive.IsProtected && item.Failures < 3) wait = Math.Min(wait, Math.Max(1, item.Due - clock.ElapsedMilliseconds));
            return wait == Int32.MaxValue ? Timeout.Infinite : (int)wait;
        }
        private void Process(Lease lease, List<DeathFact> incoming, Query query)
        {
            int released = 0;
            if (!lease.Initialized)
            {
                try { lease.Files = factory(lease.Pair); lease.Archive = new DeathArchive(lease.Files, lease.Pair, () => cancelled); lease.Archive.Load(); lease.Known = true; }
                catch (Exception e) { lease.Error = e.Message; }
                lease.Initialized = true; lease.Revision++;
            }
            foreach (var fact in incoming)
            {
                DeathFact existing = null;
                foreach (var pending in lease.Pending) if (pending.EventId == fact.EventId) { existing = pending; break; }
                try { if (existing == null && lease.Known) existing = lease.Archive.Find(fact.EventId); }
                catch (Exception e) { lease.Error = e.Message; lease.Failures = 3; }
                if (existing != null)
                {
                    released++;
                    if (!existing.SameSource(fact)) { lease.Conflict = true; lease.Error = "death-event-identity-conflict"; lease.Archive?.Protect(); lease.Failures = 3; }
                }
                else
                {
                    lease.Pending.Add(fact); lease.Revision++;
                    // An append ordered after a full visible page cannot change
                    // that page. UTC rollback inserts before its last key and
                    // correctly invalidates it, including shifted later pages.
                    lease.RowsDirty |= lease.Rows.Count < 6 || StringComparer.Ordinal.Compare(fact.EventId, lease.Rows[lease.Rows.Count - 1].EventId) <= 0;
                    lease.MarkersDirty |= fact.HasPosition;
                }
            }
            if (lease.Pending.Count != 0 && lease.Known && !lease.Conflict && !lease.Archive.IsProtected && lease.Failures < 3 && (stopping || clock.ElapsedMilliseconds >= lease.Due))
            {
                try { lease.Archive.Append(lease.Pending.ToArray()); released += lease.Pending.Count; lease.Pending.Clear(); lease.Failures = 0; lease.Error = null; }
                catch (Exception e) { lease.Error = e.Message; lease.Failures++; lease.Due = clock.ElapsedMilliseconds + (lease.Failures == 1 ? 250 : 1000); }
            }
            try
            {
                long offset = query?.Offset ?? -1; int markers = query?.Markers ?? 0; string selected = query?.Selected;
                bool changed = lease.CachedRevision != lease.Revision;
                if (lease.RowsDirty || lease.CachedOffset != offset)
                { lease.Rows = Array.AsReadOnly(DeathHistoryQuery.Page(lease.Archive, lease.Known, lease.Pending, offset)); lease.CachedOffset = offset; lease.RowsDirty = false; }
                if (lease.MarkersDirty || lease.CachedMarkers != markers)
                { lease.Markers = Array.AsReadOnly(DeathHistoryQuery.Markers(lease.Archive, lease.Known, lease.Pending, markers)); lease.CachedMarkers = markers; lease.MarkersDirty = false; }
                if (lease.CachedSelected != selected || changed && lease.Selected == null)
                {
                    lease.Selected = DeathHistoryQuery.Selected(lease.Archive, lease.Known, lease.Pending, selected);
                    lease.SelectedText = lease.Selected == null ? null : new DeathReadText(lease.Selected.Reason); lease.CachedSelected = selected;
                }
                lease.CachedRevision = lease.Revision;
            }
            catch (Exception e)
            {
                lease.Error = e.Message; lease.Failures = 3;
                // A failed target page must never relabel the previous page as
                // the new request. Reliable count survives; failed views clear.
                lease.Rows = Array.AsReadOnly(new DeathFact[0]); lease.Markers = Array.AsReadOnly(new DeathMarker[0]);
                lease.CachedOffset = -2; lease.CachedMarkers = -1; lease.CachedRevision = -1;
            }
            var snapshot = new DeathHistorySnapshot(lease.Known, checked((lease.Known ? lease.Archive.Count : 0) + lease.Pending.Count), lease.Revision, query?.Id ?? 0,
                lease.Pending.Count, lease.Rows, lease.Markers, lease.Selected, lease.Error, lease.Archive != null && lease.Archive.CommitUnconfirmed, lease.SelectedText);
            lock (gate)
            {
                lease.Outstanding -= released; lease.Processed = query;
                if (!lease.InUse && lease.Error != null) BackgroundError = lease.Pair + ": " + lease.Error;
                // New requests can arrive during disk work. Publish only the
                // matching query; the next pass observes the latest intent.
                if (ReferenceEquals(query, lease.Desired)) Volatile.Write(ref lease.Current, snapshot);
            }
        }
    }
}
