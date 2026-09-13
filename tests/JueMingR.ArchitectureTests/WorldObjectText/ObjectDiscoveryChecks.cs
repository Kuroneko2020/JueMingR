using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.ArchitectureTests
{
    internal static class ObjectDiscoveryChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                var source = new Source(); var feature = new WorldObjectDiscovery(source);
                feature.Update(WorldObjectSettings.Default, null); Require(source.Begins == 0 && source.Reads == 0, "all off has no label source work");
                var settings = WorldObjectSettings.Default.WithMode(WorldObjectKind.Chest, WorldObjectMode.Always);
                feature.Update(settings, null); Require(source.Begins == 0, "Always gate precedes source and names");
                feature.Update(settings.WithMode(WorldObjectKind.Chest, WorldObjectMode.Opened), null); Require(source.Begins == 0, "empty history precedes discovery");
                settings = WorldObjectSettings.Default.WithMode(WorldObjectKind.Sign, WorldObjectMode.Lines);
                for (int i = 0; i < 100; i++) { source.Camera = i % 8; feature.Update(settings, null); }
                Require(feature.Candidates.Count == 56 && feature.Candidates[0].Value.TileY == 98, "continuous small motion reaches late row and nearer candidates replace early far ones");
                foreach (var candidate in feature.Candidates) Require(candidate.Text.Length != 0, "empty sources cannot occupy the bounded reserve");
                long removed = feature.Candidates[0].Value.Key; source.Removed = removed; feature.Update(settings, null);
                foreach (var candidate in feature.Candidates) Require(candidate.Value.Key != removed, "removed current object is immediately withdrawn");
                var shifted = new ShiftedOrigin(); var other = new WorldObjectDiscovery(shifted);
                var always = WorldObjectSettings.Default.WithMode(WorldObjectKind.Chest, WorldObjectMode.Always);
                for (int i = 0; i < 4; i++) other.Update(always, null);
                Require(other.Candidates.Count == 1 && other.Candidates[0].Value.TileX == 10, "old chest is initially one object");
                shifted.Dresser = true; other.Update(always, null);
                Require(other.Candidates.Count == 1 && other.Candidates[0].Value.TileX == 9 && other.Candidates[0].Value.Width == 3, "replaced origin now inside a dresser cannot retain a ghost second label");
            }
            catch (Exception e) { failures.Add("World object discovery: " + e.Message); }
            try { StableAndChanges(); }
            catch (Exception e) { failures.Add("World object stable discovery: " + e.Message); }
        }
        private static void StableAndChanges()
        {
            var source = new MutableSource(); source.Put(10, 10, 21, 0, 2); source.Put(20, 10, 21, 0, 2); source.Put(10, 20, 55, 0, 2); source.Put(20, 20, 85, 0, 2);
            var discovery = new WorldObjectDiscovery(source); bool ready = false;
            discovery.SetPresentationGate((v, t, s) => true, (v, t, s) => ready);
            var all = WorldObjectSettings.Default.WithMode(WorldObjectKind.Chest, WorldObjectMode.Always).WithMode(WorldObjectKind.Sign, WorldObjectMode.All).WithMode(WorldObjectKind.Tombstone, WorldObjectMode.All);
            discovery.Update(all, null); Require(discovery.SelectedCount == 0 && discovery.Candidates.Count == 4, "cold objects remain outside qualified selection");
            ready = true; for (int i = 0; i < 4; i++) discovery.Update(all, null);
            var expected = discovery.Candidates.ToArray(); Require(discovery.SelectedCount == 4, "finite fixture fully discovered and prepared");
#if DEBUG
            int sorts = discovery.DebugSortCount, repairs = discovery.DebugRepairCount;
#endif
            int reads = source.Reads;
            for (int tick = 0; tick < 100; tick++)
            {
                discovery.Update(all, null);
                Require(discovery.Candidates.SequenceEqual(expected), "stable output identity, six-field payload, text and order");
            }
            Require(source.Reads > reads, "stability must retain existence review and scan");
#if DEBUG
            Console.WriteLine("Discovery stable 100 updates: sorts=" + (discovery.DebugSortCount - sorts) + "; repairs=" + (discovery.DebugRepairCount - repairs));
            Require(discovery.DebugSortCount == sorts && discovery.DebugRepairCount == repairs, "repeated same-value offers must not sort or repair through Discovery.Update");
            sorts = discovery.DebugSortCount;
#endif
            source.PlayerX = 21 * 16; discovery.Update(all, null);
            Require(discovery.Candidates[0].Value.TileX == 20, "player-only change reranks current objects");
#if DEBUG
            Require(discovery.DebugSortCount - sorts == 3, "new player view sorted once per active owner, not again after review"); sorts = discovery.DebugSortCount;
#endif
            source.Camera = 12; discovery.Update(all, null);
            Require(discovery.Candidates[0].Value.TileX == 20, "viewport-only change preserves visible-first");
#if DEBUG
            Require(discovery.DebugSortCount - sorts == 3, "new viewport sorted without redundant second invalidation");
#endif
            foreach (var fields in new[] { new[] { 467, 0, 2 }, new[] { 467, 10, 2 }, new[] { 88, 10, 3 }, new[] { 55, 0, 2 }, new[] { 85, 0, 2 } })
            {
                source.Put(10, 10, fields[0], fields[1], fields[2]); discovery.Update(all, null);
                var actual = discovery.Candidates.Single(c => c.Value.Key == WorldObject.PositionKey(10, 10)).Value;
                Require(actual.Type == fields[0] && actual.Style == fields[1] && actual.Width == fields[2] && actual.Kind == (fields[0] == 55 ? WorldObjectKind.Sign : fields[0] == 85 ? WorldObjectKind.Tombstone : WorldObjectKind.Chest), "same-address reachable type/style/width/kind replacement refreshes ordered payload");
            }
            source.Text = "changed body"; discovery.Update(all, null); Require(discovery.Candidates.All(c => c.Text == source.Text), "text-only change survives unchanged geometry");
            source.Put(22, 10, 21, 0, 2); discovery.Update(all, null); Require(discovery.SelectedCount == 5, "member added after consumed output is published");
            source.Remove(20, 10); discovery.Update(all, null); Require(discovery.SelectedCount == 4 && discovery.Candidates.All(c => c.Value.Key != WorldObject.PositionKey(20, 10)), "second real change after output is not capped per tick");
            source.Text = String.Empty; discovery.Update(all, null); Require(discovery.SelectedCount == 0 && discovery.Candidates.Count == 0, "new empty text withdraws existing objects");
            source.Text = "restored"; discovery.Update(all, null); Require(discovery.SelectedCount == 4, "valid text restores candidates");
            source.Detector = false; discovery.Update(all, null); Require(discovery.Candidates.All(c => c.Value.Kind != WorldObjectKind.Chest), "detector loss withdraws chests");
            discovery.Update(WorldObjectSettings.Default, null); Require(discovery.Candidates.Count == 0, "modes off withdraw all");
        }
        private sealed class MutableSource : IWorldObjectSource
        {
            private readonly Dictionary<long, WorldTargetTile> tiles = new Dictionary<long, WorldTargetTile>();
            internal int Reads, Camera; internal float PlayerX = 11 * 16; internal string Text = "fixed name"; internal bool Detector = true;
            public bool HasDetector { get { return Detector; } }
            internal void Remove(int x, int y) { WorldTargetTile old; int width = tiles.TryGetValue(WorldObject.PositionKey(x, y), out old) && old.Type == 88 ? 3 : 2; for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < width; dx++) tiles.Remove(WorldObject.PositionKey(x + dx, y + dy)); }
            internal void Put(int x, int y, int type, int style, int width)
            { Remove(x, y); for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < width; dx++) tiles[WorldObject.PositionKey(x + dx, y + dy)] = new WorldTargetTile { Readable = true, Active = true, Type = type, FrameX = type == 55 || type == 85 ? dx * 18 : style * width * 18 + dx * 18, FrameY = (type == 55 || type == 85 ? style * 36 : 0) + dy * 18 }; }
            public bool TryBegin(bool chestNames, bool signText, out WorldObjectView view)
            { view = new WorldObjectView { Visible = new WorldTargetView(Camera, 0, 40, 40, 1), Discovery = new WorldTargetView(0, 0, 60, 40, 1), PlayerX = PlayerX, PlayerY = 11 * 16 }; return true; }
            public WorldTargetTile Read(int x, int y) { Reads++; WorldTargetTile value; return tiles.TryGetValue(WorldObject.PositionKey(x, y), out value) ? value : default(WorldTargetTile); }
            public bool TryText(WorldObject value, out string text) { text = Text; return true; }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private sealed class Source : IWorldObjectSource
        {
            internal int Begins, Reads, Camera; internal long Removed = -1;
            public bool HasDetector { get { return false; } }
            public bool TryBegin(bool chestNames, bool signText, out WorldObjectView view)
            { Begins++; view = new WorldObjectView { Visible = new WorldTargetView(Camera, 0, 200, 100, 1), Discovery = new WorldTargetView(Camera, 0, 200, 100, 1), PlayerX = 1600, PlayerY = 1600 }; return true; }
            public WorldTargetTile Read(int x, int y)
            { Reads++; int ox = x / 2 * 2, oy = y / 2 * 2; if (x < 0 || y < 0 || x >= 200 || y >= 100 || WorldObject.PositionKey(ox, oy) == Removed) return default(WorldTargetTile);
                return new WorldTargetTile { Readable = true, Active = true, Type = 55, FrameX = x % 2 * 18, FrameY = y % 2 * 18 }; }
            public bool TryText(WorldObject value, out string text) { text = value.TileY < 96 ? String.Empty : "visible"; return true; }
        }
        private sealed class ShiftedOrigin : IWorldObjectSource
        {
            internal bool Dresser;
            public bool HasDetector { get { return true; } }
            public bool TryBegin(bool chestNames, bool signText, out WorldObjectView view)
            { view = new WorldObjectView { Visible = new WorldTargetView(0, 0, 30, 30, 1), Discovery = new WorldTargetView(0, 0, 30, 30, 1) }; return true; }
            public WorldTargetTile Read(int x, int y)
            { int left = Dresser ? 9 : 10; return x < left || x >= 12 || y < 10 || y >= 12 ? default(WorldTargetTile) : new WorldTargetTile {
                Readable = true, Active = true, Type = Dresser ? 88 : 21, FrameX = (x - left) * 18, FrameY = (y - 10) * 18 }; }
            public bool TryText(WorldObject value, out string text) { text = value.Type == 88 ? "Dresser" : "Chest"; return true; }
        }
    }
}
