using System.Collections.Generic;

namespace JueMingR.Platform.DeathHistory
{
    public sealed class DeathHistorySnapshot
    {
        public static readonly DeathHistorySnapshot Empty = new DeathHistorySnapshot(false, 0, 0, 0, 0, new DeathFact[0], new DeathMarker[0], null, null, false);
        public DeathHistorySnapshot(bool known, long count, long revision, long request, int pending, IReadOnlyList<DeathFact> rows, IReadOnlyList<DeathMarker> markers, DeathFact selected, string error, bool unconfirmed, DeathReadText selectedText = null)
        { Known = known; Count = count; Revision = revision; Request = request; Pending = pending; Rows = rows; Markers = markers; Selected = selected; Error = error; CommitUnconfirmed = unconfirmed; SelectedText = selectedText; }
        public bool Known { get; }
        public long Count { get; }
        public long Revision { get; }
        public long Request { get; }
        public int Pending { get; }
        public IReadOnlyList<DeathFact> Rows { get; }
        public IReadOnlyList<DeathMarker> Markers { get; }
        public DeathFact Selected { get; }
        public DeathReadText SelectedText { get; }
        public string Error { get; }
        public bool CommitUnconfirmed { get; }
    }
}
