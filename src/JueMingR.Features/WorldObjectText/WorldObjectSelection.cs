using System;
using System.Collections.Generic;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.Features.WorldObjectText
{
    // Streaming best candidates, including bounded reserves for layout/culling
    // rejection. Every discovered candidate competes; arrival never wins a tie.
    public sealed class WorldObjectSelection
    {
        private readonly int capacity;
        private readonly Dictionary<long, Ranked> positions = new Dictionary<long, Ranked>();
        private readonly SortedSet<Ranked> ranked = new SortedSet<Ranked>(new Order());
        private WorldTargetView visible;
        private float playerX, playerY;
        public WorldObjectSelection(int capacity)
        { if (capacity < 1 || capacity > 4096) throw new ArgumentOutOfRangeException(nameof(capacity)); this.capacity = capacity; }
        public int Count { get { return ranked.Count; } }
        public IEnumerable<WorldObject> Ordered { get { foreach (var entry in ranked) yield return entry.Value; } }
        public void Clear() { ranked.Clear(); positions.Clear(); }
        public void SetView(WorldTargetView currentVisible, float currentPlayerX, float currentPlayerY)
        {
            if (visible.X == currentVisible.X && visible.Y == currentVisible.Y && visible.Width == currentVisible.Width &&
                visible.Height == currentVisible.Height && playerX == currentPlayerX && playerY == currentPlayerY) return;
            visible = currentVisible; playerX = currentPlayerX; playerY = currentPlayerY;
            // At most capacity entries, never H or C. Rediscovery supplies objects
            // not retained in this bounded reserve after the player moves.
            ranked.Clear();
            foreach (var entry in positions.Values) { Rank(entry); ranked.Add(entry); }
        }
        public void Offer(WorldObject value)
        {
            Ranked entry;
            if (positions.TryGetValue(value.Key, out entry)) { ranked.Remove(entry); entry.Value = value; Rank(entry); ranked.Add(entry); return; }
            entry = new Ranked { Value = value }; Rank(entry);
            if (ranked.Count == capacity)
            {
                Ranked worst = ranked.Max;
                if (ranked.Comparer.Compare(entry, worst) >= 0) return;
                ranked.Remove(worst); positions.Remove(worst.Value.Key);
            }
            positions.Add(value.Key, entry); ranked.Add(entry);
        }
        public void Remove(long key)
        { Ranked entry; if (positions.TryGetValue(key, out entry)) { ranked.Remove(entry); positions.Remove(key); } }
        private void Rank(Ranked entry)
        {
            var value = entry.Value;
            entry.Visible = value.TileX < visible.Right && value.TileX + value.Width > visible.X && value.TileY < visible.Bottom && value.TileY + 2 > visible.Y;
            double dx = value.CenterX - playerX, dy = value.CenterY - playerY;
            entry.Distance = dx * dx + dy * dy;
        }
        private sealed class Ranked { internal WorldObject Value; internal bool Visible; internal double Distance; }
        private sealed class Order : IComparer<Ranked>
        {
            public int Compare(Ranked a, Ranked b)
            {
                int order = b.Visible.CompareTo(a.Visible); if (order != 0) return order;
                order = a.Distance.CompareTo(b.Distance); if (order != 0) return order;
                order = a.Value.TileX.CompareTo(b.Value.TileX); return order != 0 ? order : a.Value.TileY.CompareTo(b.Value.TileY);
            }
        }
    }
}
