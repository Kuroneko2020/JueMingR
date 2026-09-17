using System.Collections.Generic;

namespace JueMingR.Features.ChestLocator
{
    // Local receive evidence, never a server transaction or freshness claim.
    // Capacity starts a coverage cycle; only distinct successfully applied slots fill it.
    public sealed class ChestKnowledge
    {
        private sealed class Record
        {
            internal object Identity; internal int X, Y, Received; internal bool[] Slots;
            internal long Revision, Tick;
        }
        private readonly Dictionary<int, Record> entries = new Dictionary<int, Record>();
        private long serial;
        public void Capacity(int index, object identity, int x, int y, int capacity, long tick)
        {
            entries.Remove(index); serial++;
            if (index < 0 || index >= 8000 || identity == null || capacity <= 0 || capacity > 256) return;
            entries[index] = new Record { Identity = identity, X = x, Y = y, Slots = new bool[capacity], Revision = serial, Tick = tick };
        }
        public void Slot(int index, object identity, int slot, long tick)
        {
            Record record;
            if (!entries.TryGetValue(index, out record) || !ReferenceEquals(identity, record.Identity) || slot < 0 || slot >= record.Slots.Length) return;
            if (tick < record.Tick) { entries.Remove(index); serial++; return; }
            if (!record.Slots[slot]) { record.Slots[slot] = true; record.Received++; }
            record.Tick = tick; record.Revision = ++serial;
        }
        public bool Complete(int index, object identity, int x, int y)
        { Record r; return entries.TryGetValue(index, out r) && ReferenceEquals(identity, r.Identity) && r.X == x && r.Y == y && r.Received == r.Slots.Length; }
        public long Revision(int index) { Record r; return entries.TryGetValue(index, out r) ? r.Revision : -1; }
        public long Tick(int index) { Record r; return entries.TryGetValue(index, out r) ? r.Tick : -1; }
        public void Forget(int index) { entries.Remove(index); serial++; }
        public void Clear() { entries.Clear(); serial++; }
    }
}
