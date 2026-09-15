using System;
using System.Collections.Generic;
using JueMingR.Platform.DeathHistory;

namespace JueMingR.Features.DeathHistory
{
    public sealed class DeathHistory
    {
        private readonly DeathHistoryStore store;
        private DeathHistoryStore.Handoff handoff;
        private DeathHistoryStore.Lease lease;
        private DeathHistoryStore.Query query;
        private string pair;
        private bool active, stopping;
        private long request;
        public DeathHistory(Func<string, IDeathArchiveFiles> factory) { store = new DeathHistoryStore(factory); }
        public DeathHistorySnapshot Snapshot { get { return lease == null ? DeathHistorySnapshot.Empty : store.Snapshot(lease); } }
        public long Generation { get; private set; }
        public bool HasPair { get { return lease != null; } }
        public bool HasPendingHandoff { get { return handoff != null && handoff.Facts.Count != 0; } }
        private string error;
        public string Error { get { return store.Fatal ?? error; } private set { error = value; } }
        public string BackgroundError { get { return store.BackgroundError; } }
        public void BeginSession(long generation)
        { EndSession(); if (stopping) return; Generation = generation; active = true; Error = null; handoff = store.ReserveHandoff(); query = new DeathHistoryStore.Query { Id = ++request, Offset = -1 }; }
        public void EndSession()
        { if (lease != null) store.Release(lease); store.RetireHandoff(handoff); handoff = null; lease = null; pair = null; active = false; query = null; }
        public void UsePair(string value)
        {
            if (!active || stopping) return;
            if (!DeathArchiveCodec.PageId(value)) throw new ArgumentException("invalid-death-pair");
            if (pair != null && pair != value) { Error = "death-identity-changed-within-session"; return; }
            pair = value;
            if (handoff != null) handoff.Pair = value;
            if (lease == null) { lease = store.Acquire(value); if (lease == null) { Error = "death-history-pair-busy"; return; } store.Request(lease, query); }
            if (handoff != null) while (handoff.Facts.Count != 0)
            { if (!store.Add(lease, handoff.Facts[0])) { Error = "death-history-pending-full"; return; } handoff.Facts.RemoveAt(0); }
            Error = null;
        }
        public bool Accept(DeathFact fact)
        {
            if (!active || stopping || fact == null) return false;
            // Once bound, acceptance must fit the queryable archive quota.
            // Earlier identity-pending tails retain their reservation; a full
            // protected archive must not claim new, permanently hidden facts.
            if (lease != null)
            {
                if (HasPendingHandoff || !store.Add(lease, fact)) { Error = "death-history-pending-full"; return false; }
                return true;
            }
            if (handoff == null) { Error = "death-history-handoff-capacity"; return false; }
            foreach (var old in handoff.Facts) if (old.EventId == fact.EventId)
            { if (old.SameSource(fact)) return true; Error = "death-event-identity-conflict"; return false; }
            if (handoff.Facts.Count == 64) { Error = "death-history-identity-pending-full"; return false; }
            handoff.Facts.Add(fact); return true;
        }
        public void Request(long pageOffset, int markers, string selected)
        {
            if (!active || stopping) return;
            if (pageOffset < -1 || markers != 0 && markers != 128 && markers != 256 && markers != 512 && markers != 1024) throw new ArgumentException("invalid-death-query");
            if (query.Offset == pageOffset && query.Markers == markers && query.Selected == selected) return;
            query = new DeathHistoryStore.Query { Id = ++request, Offset = pageOffset, Markers = markers, Selected = selected };
            if (lease != null) store.Request(lease, query);
        }
        public long RequestId { get { return query?.Id ?? 0; } }
        public bool Stop(int milliseconds) { if (!stopping) { EndSession(); stopping = true; } return store.Stop(milliseconds); }
    }
}
