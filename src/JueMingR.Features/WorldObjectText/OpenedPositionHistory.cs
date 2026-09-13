using System;
using System.Collections.Generic;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.Features.WorldObjectText
{
    public sealed class OpenedPositionHistory
    {
        private readonly OpenedPositionStore store;
        private Session current;
        private bool stopping;
        private string localError;
        public OpenedPositionHistory(Func<string, IPreferenceStorage> storageFactory) { store = new OpenedPositionStore(storageFactory); }
        public long Revision { get; private set; }
        public bool Loaded { get { return current != null && current.State != null && current.State.Loaded; } }
        public bool HasAny { get { return current != null && (current.Delta.Count != 0 || current.Lease != null && (current.Lease.Accepted.Count != 0 || OpenedPositionStore.Snapshot(current.Lease).Index.Count != 0 || current.Lease.Overlays.Length != 0)); } }
        public bool HasPair { get { return !store.Terminated && current != null && current.Lease != null; } }
        public PreferenceStatus Status { get { if (store.Terminated) return PreferenceStatus.Stopped; var state = current?.State; if (state == null || !state.Loaded) return PreferenceStatus.Loading;
            if (!state.Protected && state.Error == null && (current.Sent < current.Events.Count || store.HasPending(current.Lease))) return PreferenceStatus.Pending; return state.Status; } }
        public bool CommitUnconfirmed { get { return current?.State != null && current.State.Unconfirmed; } }
        public string Error { get { return localError ?? current?.State?.Error; } }
        public string TakeBackgroundFailure() { return store.TakeFailure(); }
        public void BeginSession(long generation)
        { EndSession(); if (!stopping) { current = new Session { Generation = generation }; localError = null; Revision++; } }
        public void EndSession()
        {
            if (current == null) return;
            if (current.Lease != null) store.Release(current.Lease, current.Events, current.Sent, current.Delta);
            current = null; Revision++;
        }
        public bool Opened(int x, int y)
        {
            if (stopping || current == null) return false;
            long key = WorldObject.PositionKey(x, y);
            if (Contains(key)) { if (current.Lease != null) store.Retry(current.Lease); return false; }
            try { current.Delta.Add(key); }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException) { localError = "opened-session-capacity-or-coordinate"; return false; }
            if (current.Lease == null || !store.Add(current.Lease, key)) current.Events.Add(key);
            Revision++; return true;
        }
        public void UsePair(string key)
        {
            if (!OpenedPositionCodec.IsPairKey(key)) throw new ArgumentException("invalid-opened-pair");
            if (stopping || current == null) return;
            if (store.Terminated) { localError = "opened-store-unavailable-session-only"; return; }
            if (current.Pair != null && current.Pair != key) { localError = "opened-identity-changed-within-session"; return; }
            current.Pair = key; Poll();
        }
        public void Poll()
        {
            if (stopping || current == null) return;
            if (store.Terminated) { localError = "opened-store-unavailable-session-only"; return; }
            if (current.Lease == null && current.Pair != null)
            {
                current.Lease = store.Acquire(current.Pair);
                if (current.Lease == null) { localError = store.Terminated ? "opened-store-unavailable-session-only" : "opened-pair-queue-busy"; return; }
                if (localError == "opened-pair-queue-busy") localError = null;
            }
            if (current.Lease == null) return;
            var snapshot = OpenedPositionStore.Snapshot(current.Lease);
            if (!ReferenceEquals(current.State, snapshot))
            { if (current.State == null || !ReferenceEquals(current.State.Index, snapshot.Index)) Revision++; current.State = snapshot; }
            // Identity may arrive after many session-only facts. Replay bounded
            // deltas, never clone/index/serialize H inside the game callback.
            int end = Math.Min(current.Events.Count, current.Sent + 64);
            while (current.Sent < end)
            {
                long key = current.Events[current.Sent];
                if (snapshot.Index.Contains(key) || store.Add(current.Lease, key)) current.Sent++;
                else break;
            }
        }
        public bool Contains(long key)
        {
            if (current == null) return false;
            if (current.Delta.Contains(key)) return true;
            if (current.Lease == null) return false;
            OpenedPositionStore.State state; OpenedPositionIndex[] overlays; store.ReadView(current.Lease, out state, out overlays);
            if (current.Lease.Accepted.Contains(key) || state.Index.Contains(key)) return true;
            foreach (var overlay in overlays) if (overlay.Contains(key)) return true;
            return false;
        }
        public IEnumerable<long> Nearby(WorldTargetView view)
        {
            Session session = current; if (session == null) yield break;
            OpenedPositionStore.State state = null; OpenedPositionIndex[] overlays = new OpenedPositionIndex[0];
            if (session.Lease != null) store.ReadView(session.Lease, out state, out overlays);
            var baseline = state?.Index;
            if (baseline != null) foreach (long key in baseline.Nearby(view)) yield return key;
            var accepted = session.Lease?.Accepted;
            if (accepted != null) foreach (long key in accepted.Nearby(view)) if (baseline == null || !baseline.Contains(key)) yield return key;
            for (int i = 0; i < overlays.Length; i++) foreach (long key in overlays[i].Nearby(view))
                if ((baseline == null || !baseline.Contains(key)) && (accepted == null || !accepted.Contains(key)) && !InOverlays(overlays, i, key)) yield return key;
            foreach (long key in session.Delta.Nearby(view)) if ((baseline == null || !baseline.Contains(key)) && (accepted == null || !accepted.Contains(key)) && !InOverlays(overlays, overlays.Length, key)) yield return key;
        }
        public OpenedPositionQuery Query(WorldTargetView view)
        {
            if (current == null) return new OpenedPositionQuery(new OpenedPositionIndex[0], view);
            OpenedPositionStore.State state = null; OpenedPositionIndex[] overlays = new OpenedPositionIndex[0];
            if (current.Lease != null) store.ReadView(current.Lease, out state, out overlays);
            var sources = new OpenedPositionIndex[overlays.Length + 3]; sources[0] = state?.Index; sources[1] = current.Lease?.Accepted;
            Array.Copy(overlays, 0, sources, 2, overlays.Length); sources[sources.Length - 1] = current.Delta;
            return new OpenedPositionQuery(sources, view);
        }
        private static bool InOverlays(OpenedPositionIndex[] overlays, int count, long key)
        { for (int i = 0; i < count; i++) if (overlays[i].Contains(key)) return true; return false; }
        public bool Stop(int milliseconds)
        { if (!stopping) { EndSession(); stopping = true; } return store.Stop(milliseconds); }
        private sealed class Session
        {
            // A tag provided by the one Runtime, not a second session authority.
            internal long Generation;
            internal string Pair;
            internal readonly OpenedPositionIndex Delta = new OpenedPositionIndex();
            internal readonly List<long> Events = new List<long>();
            internal int Sent;
            internal OpenedPositionStore.Lease Lease;
            internal OpenedPositionStore.State State;
        }
    }
}
