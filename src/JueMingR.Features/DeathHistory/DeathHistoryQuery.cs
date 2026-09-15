using System;
using System.Collections.Generic;
using JueMingR.Platform.DeathHistory;

namespace JueMingR.Features.DeathHistory
{
    // At most 64 not-yet-confirmed facts augment the one archive. Rank lookups
    // merge that finite overlay without loading or sorting committed history.
    internal static class DeathHistoryQuery
    {
        internal static DeathFact[] Page(DeathArchive archive, bool known, List<DeathFact> pending, long offset)
        {
            if (offset < 0) return new DeathFact[0];
            var sorted = pending.ToArray(); Array.Sort(sorted, (a, b) => String.CompareOrdinal(a.EventId, b.EventId));
            var ranks = new long[sorted.Length];
            for (int i = 0; i < sorted.Length; i++) ranks[i] = (known ? archive.Before(sorted[i].EventId) : 0) + i;
            long total = checked((known ? archive.Count : 0) + sorted.Length); var result = new List<DeathFact>(6);
            for (long at = offset; at < total && result.Count < 6; at++)
            {
                int before = 0; while (before < ranks.Length && ranks[before] < at) before++;
                if (before < ranks.Length && ranks[before] == at) result.Add(sorted[before]);
                else { var row = archive.Page(at - before, 1); if (row.Length != 1) throw new InvalidOperationException("death-page-rank-inconsistent"); result.Add(row[0]); }
            }
            return result.ToArray();
        }
        internal static DeathMarker[] Markers(DeathArchive archive, bool known, List<DeathFact> pending, int count)
        {
            if (count == 0) return new DeathMarker[0]; var result = new List<DeathMarker>(count);
            for (int i = pending.Count - 1; i >= 0 && result.Count < count; i--)
            { var fact = pending[i]; if (fact.HasPosition) result.Add(new DeathMarker(fact.EventId, (known ? archive.Count : 0) + i + 1, fact.X, fact.Y)); }
            if (known && result.Count < count)
                foreach (var marker in archive.Recent(count)) { if (result.Count == count) break; result.Add(marker); }
            return result.ToArray();
        }
        internal static DeathFact Selected(DeathArchive archive, bool known, List<DeathFact> pending, string id)
        { if (id == null) return null; foreach (var fact in pending) if (fact.EventId == id) return fact; return known ? archive.Find(id) : null; }
    }
}
