using System;
using System.Collections.Generic;
using JueMingR.Features.WorldTargets;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.ArchitectureTests
{
    internal static class WorldTargetRulesChecks
    {
        internal static void Check(IList<string> failures)
        {
            try { Objects(); LifetimeAndBudget(); Tuning(); Geometry(); }
            catch (Exception e) { failures.Add("World targets: " + e.Message); }
        }
        private static void Objects()
        {
            var source = new Source(); var feature = new WorldTargetFeature(source);
            feature.OnSessionStarted(); feature.Update(0);
            Require(source.Begins == 0 && source.Reads == 0, "all off does no observation");
            feature.Configure(All());
            int[] ids = { 12, 236, 639, 751, 752 };
            for (int i = 0; i < ids.Length; i++) source.Put(2 + i * 4, 2, ids[i], i == 1 ? 2 : 0);
            source.Put(2, 4, 12); // Touching but independent, never one connected group.
            source.Put(26, 2, 702); source.Put(30, 2, 665); // Replica and unrelated boulder type.
            Discover(feature); Require(feature.Targets.Count == 6, "five kinds plus adjacent second heart; no replica/boulder");
            int reads = source.Reads; feature.Configure(All().WithColor(WorldTargetKind.LifeCrystal, 0x123456));
            Require(source.Reads == reads, "color edit never reads tiles");
            source.Tiles.Remove(Key(3, 3)); feature.Update(20);
            Require(feature.Targets.Count == 5, "one missing cell withdraws known object in next update, no TTL");
            source.Put(2, 2, 12); source.Tiles[Key(3, 3)] = Tile(12, 0, 18); feature.Update(21);
            Require(feature.Targets.Count == 5, "wrong offset is not a complete object");
            source.Put(2, 2, 639); Discover(feature);
            Require(feature.Targets.Count == 6 && Find(feature, 2, 2).Kind == WorldTargetKind.ManaCrystal, "same coordinate replacement is newly classified");
            source.Unknown.Add(Key(18, 3)); feature.Update(40);
            Require(feature.Targets.Count == 5, "one unreadable cell removes egg without claiming absent on server");
            source.Unknown.Clear(); Discover(feature); Require(feature.Targets.Count == 6, "readable egg is discovered again");
            source.Put(6, 2, 236, 3); feature.Update(50);
            Require(feature.Targets.Count == 5, "fruit fourth style is invalid");
            for (int style = 0; style < 3; style++) { source.Put(6, 2, 236, style); Discover(feature); Require(Find(feature, 6, 2).Style == style, "all three native fruit styles"); }
            source.Put(14, 2, 751); var cell = source.Tiles[Key(14, 2)]; cell.Inactive = true; source.Tiles[Key(14, 2)] = cell;
            feature.Update(60); Require(Find(feature, 14, 2).Kind == WorldTargetKind.SleepingDigtoise, "sleeping tile uses native active semantics");
            source.Put(2, 2, 12); cell = source.Tiles[Key(2, 2)]; cell.Inactive = true; source.Tiles[Key(2, 2)] = cell;
            feature.Update(61); Require(feature.Targets.Count == 5, "orb actuator inactive is not nactive");
            source = new Source { View = new WorldTargetView(3, 3, 1, 1, 1) }; source.Put(2, 2, 12);
            feature = new WorldTargetFeature(source); feature.Configure(All()); feature.OnSessionStarted(); Discover(feature);
            Require(feature.Targets.Count == 1 && feature.Targets[0].TileX == 2, "any visible subcell resolves origin outside view");
        }
        private static void LifetimeAndBudget()
        {
            var source = new Source(); source.Put(2, 2, 12);
            var feature = new WorldTargetFeature(source); feature.Configure(All()); feature.OnSessionStarted(); Discover(feature);
            source.Ability = false; int reads = source.Reads; feature.Update(20);
            Require(feature.Targets.Count == 0 && source.Reads == reads, "ability loss clears output without tile reads");
            source.Ability = true; Discover(feature); Require(feature.Targets.Count == 1, "ability regain retains intent");
            feature.Configure(WorldTargetSettings.Default.WithEnabled(WorldTargetKind.LifeFruit, true)); Discover(feature);
            Require(feature.Targets.Count == 0, "one enabled category never projects unrelated targets");
            feature.Configure(All()); Discover(feature); feature.OnSessionEnded();
            Require(feature.Targets.Count == 0, "session end clears output");
            feature.OnSessionStarted(); source.View = new WorldTargetView(100, 100, 32, 16, 1); feature.Update(31);
            Require(feature.Targets.Count == 0, "teleport drops old objects and discovery work");
            source.Put(100, 100, 752); Discover(feature); Require(feature.Targets.Count == 1, "new viewport is not behind old queue");
            source.View = new WorldTargetView(100, 100, 16, 16, 2); feature.Update(32);
            Require(feature.Targets.Count <= 1, "resize/read revision has bounded new pass");
            source = new Source { View = new WorldTargetView(0, 0, 64, 64, 1) };
            feature = new WorldTargetFeature(source); feature.Configure(All()); feature.OnSessionStarted();
            for (int y = 0; y < 64; y += 2) for (int x = 0; x < 64; x += 2) source.Put(x, y, 12);
            Discover(feature); Require(feature.Targets.Count == 1024, "high density is not truncated to first N");
            reads = source.Reads; feature.Update(50);
            Require(source.Reads - reads <= 4356, "known-object rechecks and discovery share current read cache");
            feature.Configure(WorldTargetSettings.Default); reads = source.Reads; feature.Update(51);
            Require(feature.Targets.Count == 0 && source.Reads == reads, "last disable exits all domain work");
            source = new Source { View = new WorldTargetView(0, 0, 64, 64, 1) };
            feature = new WorldTargetFeature(source); feature.Configure(All()); feature.OnSessionStarted(); feature.Update(0);
            Require(source.Reads <= 512, "empty view scans only one eighth per update");
            source.Put(70, 60, 12);
            for (int i = 0; i < 24; i++) { source.View = new WorldTargetView(i + 1, 0, 64, 64, 1); feature.Update((ulong)i); }
            Require(feature.Targets.Count == 1, "continuous small camera motion does not starve later rows");
            source = new Source { View = new WorldTargetView(0, 0, 64, 64, 1) }; source.Put(60, 60, 12);
            feature = new WorldTargetFeature(source); feature.Configure(All()); feature.OnSessionStarted();
            for (int i = 0; i < 24; i++) { source.View = new WorldTargetView(i / 2, 0, 64 + i % 2, 64, 1); feature.Update((ulong)i); }
            Require(feature.Targets.Count == 1, "floor/ceiling width alternation during sub-tile camera motion cannot starve the last rows");
        }
        private static void Tuning()
        {
            var target = new WorldTarget { TileX = 20, TileY = 30, X = 320, Y = 480, Width = 32, Height = 32 };
            var adjacent = target; adjacent.TileX += 2; adjacent.X += 32;
            var first = WorldTargetArrows.At(target, new WorldTargetAnimation(0, 1), 0);
            Require(first.Length >= 24, "owner tuning: small arrows are visibly larger than the original 18px");
            bool staggered = false, afterOrbitDiffers = false;
            for (int step = 0; step < 60; step++)
            {
                long ticks = step * 1000000L;
                var a = WorldTargetArrows.At(target, new WorldTargetAnimation(ticks, 1), 0);
                var b = WorldTargetArrows.At(adjacent, new WorldTargetAnimation(ticks, 1), 0);
                staggered |= Math.Abs((a.X - target.CenterX) - (b.X - adjacent.CenterX)) > 3;
                var later = WorldTargetArrows.At(target, new WorldTargetAnimation(ticks + 70000000L, 1), 0);
                Require(Math.Abs(a.X - later.X) < .001, "one full orbit restores horizontal phase");
                afterOrbitDiffers |= Math.Abs(a.Y - later.Y) > 6;
            }
            Require(staggered, "neighboring objects must not march in the same orbital phase");
            Require(afterOrbitDiffers, "one orbit must not restart the enlarged independent vertical wave");
            float low = float.MaxValue, high = float.MinValue;
            for (int step = 0; step <= 135; step++)
            {
                var animation = new WorldTargetAnimation(step * 200000L, 1);
                float centerY = 0;
                for (int i = 0; i < 3; i++) centerY += WorldTargetArrows.At(target, animation, i).Y / 3;
                low = Math.Min(low, centerY - target.CenterY); high = Math.Max(high, centerY - target.CenterY);
                var pose = WorldTargetArrows.At(target, animation, 0);
                double dx = pose.X - target.CenterX, dy = pose.Y - centerY;
                Require(Math.Sqrt(dx * dx + dy * dy) < 35, "larger small arrows orbit closer than the old 38.63px");
            }
            Require(low < -5.9 && high > 5.9 && low >= -6.001 && high <= 6.001, "group centroid proves 12px peak-to-peak pure vertical motion");
            foreach (var offset in new[] { new[] { 2, 0 }, new[] { 0, 2 }, new[] { 2, 2 } })
            {
                adjacent = target; adjacent.TileX += offset[0]; adjacent.TileY += offset[1];
                double distance = 0;
                var animation = new WorldTargetAnimation(9000000L, 1);
                for (int i = 0; i < 3; i++)
                {
                    var a = WorldTargetArrows.At(target, animation, i); double best = double.MaxValue;
                    for (int j = 0; j < 3; j++)
                    {
                        var b = WorldTargetArrows.At(adjacent, animation, j);
                        best = Math.Min(best, Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y));
                    }
                    distance += best;
                }
                Require(distance > 6, "horizontal, vertical and diagonal neighbors have distinct position sets, not merely relabeled arrows");
            }
        }
        private static void Geometry()
        {
            foreach (var target in new[] {
                new WorldTarget { X = 100, Y = 200, Width = 32, Height = 32 },
                new WorldTarget { X = 100, Y = 200, Width = 56, Height = 46 },
                new WorldTarget { X = 100, Y = 200, Width = 36, Height = 38 } })
            foreach (float gravity in new[] { 1f, -1f })
            foreach (long ticks in GeometryTimes())
            {
                var animation = new WorldTargetAnimation(ticks, gravity);
                for (int i = 0; i < 3; i++)
                {
                    ArrowPose pose = WorldTargetArrows.At(target, animation, i);
                    double dx = target.CenterX - pose.X, dy = target.CenterY - pose.Y;
                    Require(Math.Abs(dx * pose.DirectionY - dy * pose.DirectionX) < .001, "floated arrow still points at fixed true center");
                    Require(dx * pose.DirectionX + dy * pose.DirectionY > 0, "arrow points inward, never tangent/outward");
                    // The owner's closer/larger tuning intentionally retires the
                    // old external-circle gap. Protect the central body; actual
                    // transparent corners/edge overlap require native previews.
                    Require(Math.Sqrt(dx * dx + dy * dy) - pose.Length / 2 > Math.Min(target.Width, target.Height) / 2 - 1,
                        "innermost arrow tip stays outside the target central body");
                    ArrowPose next = WorldTargetArrows.At(target, animation, (i + 1) % 3);
                    double ax = pose.X - target.CenterX, ay = pose.Y - target.CenterY - WorldTargetArrows.Bob(target, animation);
                    double bx = next.X - target.CenterX, by = next.Y - target.CenterY - WorldTargetArrows.Bob(target, animation);
                    Require(Math.Abs((ax * bx + ay * by) / (ax * ax + ay * ay) + .5) < .00001, "three baseline positions remain 120 degrees apart");
                    Require(Math.Sqrt(dx * dx + dy * dy) + pose.Length * .61 <= WorldTargetArrows.Extent(target), "larger complete arrow plus bob fits the culling extent");
                }
            }
            var t = new WorldTarget { Width = 32, Height = 32 };
            var a = WorldTargetArrows.At(t, new WorldTargetAnimation(17500000, 1), 0);
            var b = WorldTargetArrows.At(t, new WorldTargetAnimation(17500000, 1), 0);
            Require(a.X == b.X && a.Y == b.Y, "same elapsed time is independent of frame count");
            Require(a.Y > t.CenterY, "quarter orbit is clockwise in ordinary screen coordinates");
        }
        private static IEnumerable<long> GeometryTimes()
        { for (long ticks = 0; ticks <= 1890000000L; ticks += 1000000L) yield return ticks; yield return TimeSpan.MaxValue.Ticks - 1; }
        private static WorldTargetSettings All()
        { var value = WorldTargetSettings.Default; foreach (WorldTargetKind kind in Enum.GetValues(typeof(WorldTargetKind))) value = value.WithEnabled(kind, true); return value; }
        private static void Discover(WorldTargetFeature f) { for (ulong i = 0; i < 16; i++) f.Update(i); }
        private static WorldTarget Find(WorldTargetFeature f, int x, int y)
        { foreach (var target in f.Targets) if (target.TileX == x && target.TileY == y) return target; throw new Exception("expected target missing at " + x + "," + y); }
        internal static long Key(int x, int y) { return ((long)x << 32) | (uint)y; }
        internal static WorldTargetTile Tile(int type, int x, int y) { return new WorldTargetTile { Readable = true, Active = true, Type = type, FrameX = x, FrameY = y }; }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private sealed class Source : IWorldTargetSource
        {
            internal readonly Dictionary<long, WorldTargetTile> Tiles = new Dictionary<long, WorldTargetTile>();
            internal readonly HashSet<long> Unknown = new HashSet<long>();
            internal WorldTargetView View = new WorldTargetView(0, 0, 32, 16, 1);
            internal bool Ability = true; internal int Reads, Begins;
            public bool TryBegin(out WorldTargetView view) { Begins++; view = View; return Ability; }
            public WorldTargetTile Read(int x, int y)
            { Reads++; WorldTargetTile tile; return Unknown.Contains(Key(x, y)) ? default(WorldTargetTile) : Tiles.TryGetValue(Key(x, y), out tile) ? tile : new WorldTargetTile { Readable = true }; }
            internal void Put(int x, int y, int type, int style = 0)
            { for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) Tiles[Key(x + dx, y + dy)] = Tile(type, style * 36 + dx * 18, dy * 18); }
        }
    }
}
