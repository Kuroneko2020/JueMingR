using System;
using System.Collections.Generic;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.Features.WorldTargets
{
    public sealed class WorldTargetFeature : IRuntimeFeature
    {
        private readonly IWorldTargetSource source;
        private readonly List<WorldTarget> targets = new List<WorldTarget>();
        private readonly System.Collections.ObjectModel.ReadOnlyCollection<WorldTarget> output;
        private readonly HashSet<long> origins = new HashSet<long>();
        private readonly Dictionary<long, WorldTargetTile> reads = new Dictionary<long, WorldTargetTile>();
        private readonly Func<int, int, WorldTargetTile> read;
        private WorldTargetSettings settings = WorldTargetSettings.Default;
        private WorldTargetView pass;
        private int cursor;
        private bool active, hasPass;
        public WorldTargetFeature(IWorldTargetSource source)
        { this.source = source ?? throw new ArgumentNullException(nameof(source)); output = targets.AsReadOnly(); read = Read; }
        public IReadOnlyList<WorldTarget> Targets { get { return output; } }
        public bool Enabled { get { return settings.AnyEnabled && !HasFailed; } }
        public bool HasFailed { get; private set; }
        public void Configure(WorldTargetSettings value)
        {
            settings = value ?? throw new ArgumentNullException(nameof(value));
            if (!Enabled) Clear();
            else for (int i = targets.Count - 1; i >= 0; i--) if (!settings.Enabled(targets[i].Kind)) targets.RemoveAt(i);
        }
        public void OnSessionStarted() { active = true; Clear(); }
        public void OnSessionEnded() { active = false; Clear(); }
        public void FailClosed() { HasFailed = true; Clear(); }
        public void Clear() { targets.Clear(); origins.Clear(); reads.Clear(); hasPass = false; cursor = 0; }
        public void Update(ulong tick)
        {
            if (!active || !Enabled) return; // No context/tile/animation work while all off.
            WorldTargetView view;
            if (!source.TryBegin(out view) || view.Width <= 0 || view.Height <= 0 || view.Width > 512 || view.Height > 512)
            { Clear(); return; }
            if (hasPass && (pass.ReadRevision != view.ReadRevision || pass.GeometryRevision != view.GeometryRevision || !pass.Intersects(view))) Clear();
            reads.Clear(); origins.Clear();
            // Recheck current four-cell objects now, independently of discovery.
            // Unknown client data withdraws a marker, never a server-absence claim.
            int kept = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                WorldTarget old = targets[i], current;
                if (!Near(old.TileX, old.TileY, view) || !WorldTargetResolver.TryResolve(old.TileX, old.TileY,
                    Read(old.TileX, old.TileY), settings, read, out current)) continue;
                targets[kept++] = current; origins.Add(Key(current.TileX, current.TileY));
            }
            if (kept < targets.Count) targets.RemoveRange(kept, targets.Count - kept);
            if (!hasPass) { pass = view; cursor = 0; hasPass = true; }
            int area = pass.Width * pass.Height, end = Math.Min(area, cursor + (area + 7) / 8);
            // Small camera motion finishes this bounded pass instead of resetting
            // to row zero each frame. New view follows within two short passes.
            for (; cursor < end; cursor++)
            {
                int x = pass.X + cursor % pass.Width, y = pass.Y + cursor / pass.Width;
                if (!Near(x, y, view)) continue;
                WorldTarget target;
                if (WorldTargetResolver.TryResolve(x, y, Read(x, y), settings, read, out target) && origins.Add(Key(target.TileX, target.TileY))) targets.Add(target);
            }
            if (cursor == area) { pass = view; cursor = 0; }
        }
        private static bool Near(int x, int y, WorldTargetView view)
        { return x < view.Right && x + 2 > view.X && y < view.Bottom && y + 2 > view.Y; }
        private WorldTargetTile Read(int x, int y)
        {
            long key = Key(x, y); WorldTargetTile tile;
            if (!reads.TryGetValue(key, out tile)) { tile = source.Read(x, y); reads.Add(key, tile); }
            return tile;
        }
        private static long Key(int x, int y) { return ((long)x << 32) | (uint)y; }
    }
}
