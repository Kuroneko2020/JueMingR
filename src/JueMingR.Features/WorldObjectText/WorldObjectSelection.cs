using System;
using System.Collections.Generic;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.Features.WorldObjectText
{
    // A fixed max-heap keeps the worst admitted candidate at the root. Motion
    // re-ranks at most N in place; a rejected late candidate allocates nothing.
    public sealed class WorldObjectSelection
    {
        private readonly Ranked[] heap, ordered;
        private readonly Dictionary<long, int> positions = new Dictionary<long, int>();
        private int count;
        private bool dirty;
        private WorldTargetView visible;
        private float playerX, playerY;
        public WorldObjectSelection(int capacity)
        { if (capacity < 1 || capacity > 4096) throw new ArgumentOutOfRangeException(nameof(capacity)); heap = new Ranked[capacity]; ordered = new Ranked[capacity]; }
        public int Count { get { return count; } }
#if DEBUG
        // Finite executable regression evidence only; absent from Release.
        public int DebugSortCount { get; private set; }
        public int DebugRepairCount { get; private set; }
#endif
        public IEnumerable<WorldObject> Ordered { get { Sort(); for (int i = 0; i < count; i++) yield return ordered[i].Value; } }
        public int CopyTo(WorldObject[] destination, int start)
        { Sort(); for (int i = 0; i < count; i++) destination[start + i] = ordered[i].Value; return count; }
        public void Clear() { count = 0; positions.Clear(); dirty = true; }
        public void SetView(WorldTargetView view, float x, float y)
        {
            if (visible.X == view.X && visible.Y == view.Y && visible.Width == view.Width && visible.Height == view.Height && playerX == x && playerY == y) return;
            visible = view; playerX = x; playerY = y;
            for (int i = 0; i < count; i++) heap[i] = Rank(heap[i].Value);
            for (int i = count / 2 - 1; i >= 0; i--) Down(i);
            dirty = true;
        }
        public bool WouldAdmit(WorldObject value)
        { return positions.ContainsKey(value.Key) || count < heap.Length || Order.Instance.Compare(Rank(value), heap[0]) < 0; }
        public void Offer(WorldObject value)
        {
            int index;
            if (positions.TryGetValue(value.Key, out index))
            {
                // SetView already owns re-ranking. Identical payload adds no
                // work, but must preserve dirty from any unconsumed change.
                // Equal rank alone is insufficient: type/style can change at
                // the same position, and ordered must receive the new payload.
                if (SameValue(heap[index].Value, value)) return;
                heap[index] = Rank(value); Repair(index); dirty = true; return;
            }
            Ranked rank = Rank(value);
            if (count == heap.Length)
            {
                if (Order.Instance.Compare(rank, heap[0]) >= 0) return;
                positions.Remove(heap[0].Value.Key); heap[0] = rank; positions[value.Key] = 0; Down(0);
            }
            else { heap[count] = rank; positions.Add(value.Key, count); Up(count++); }
            dirty = true;
        }
        public void Remove(long key)
        {
            int index; if (!positions.TryGetValue(key, out index)) return;
            positions.Remove(key); count--;
            if (index != count) { heap[index] = heap[count]; positions[heap[index].Value.Key] = index; Repair(index); }
            dirty = true;
        }
        private static bool SameValue(WorldObject a, WorldObject b)
        { return a.Kind == b.Kind && a.TileX == b.TileX && a.TileY == b.TileY && a.Type == b.Type && a.Style == b.Style && a.Width == b.Width; }
        private void Repair(int index)
        {
#if DEBUG
            DebugRepairCount++;
#endif
            if (index > 0 && Order.Instance.Compare(heap[index], heap[(index - 1) / 2]) > 0) Up(index); else Down(index);
        }
        private void Up(int index)
        { while (index > 0) { int parent = (index - 1) / 2; if (Order.Instance.Compare(heap[index], heap[parent]) <= 0) break; Swap(index, parent); index = parent; } }
        private void Down(int index)
        {
            while (index * 2 + 1 < count)
            { int child = index * 2 + 1; if (child + 1 < count && Order.Instance.Compare(heap[child + 1], heap[child]) > 0) child++;
                if (Order.Instance.Compare(heap[index], heap[child]) >= 0) break; Swap(index, child); index = child; }
        }
        private void Swap(int a, int b)
        { Ranked temp = heap[a]; heap[a] = heap[b]; heap[b] = temp; positions[heap[a].Value.Key] = a; positions[heap[b].Value.Key] = b; }
        private Ranked Rank(WorldObject value)
        { double dx = value.CenterX - playerX, dy = value.CenterY - playerY; return new Ranked { Value = value,
            Visible = value.TileX < visible.Right && value.TileX + value.Width > visible.X && value.TileY < visible.Bottom && value.TileY + 2 > visible.Y, Distance = dx * dx + dy * dy }; }
        private void Sort()
        {
            if (!dirty) return;
#if DEBUG
            DebugSortCount++;
#endif
            Array.Copy(heap, ordered, count); Array.Sort(ordered, 0, count, Order.Instance); dirty = false;
        }
        private struct Ranked { internal WorldObject Value; internal bool Visible; internal double Distance; }
        private sealed class Order : IComparer<Ranked>
        {
            internal static readonly Order Instance = new Order();
            public int Compare(Ranked a, Ranked b)
            { int order = b.Visible.CompareTo(a.Visible); if (order != 0) return order; order = a.Distance.CompareTo(b.Distance); if (order != 0) return order;
                order = a.Value.TileX.CompareTo(b.Value.TileX); return order != 0 ? order : a.Value.TileY.CompareTo(b.Value.TileY); }
        }
    }
}
