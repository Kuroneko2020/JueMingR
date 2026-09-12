using System;
using System.Collections.Generic;
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
