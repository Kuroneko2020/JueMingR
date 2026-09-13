using System;
using JueMingR.Platform.WorldTargets;
using JueMingR.Features.WorldTargets;
using JueMingR.TerrariaHost.World;
using Terraria;

namespace JueMingR.TerrariaHost.WorldTargets
{
    internal sealed class WorldTargetHostObservation : IWorldTargetSource
    {
        private readonly Func<bool> sessionActive;
        private readonly WorldTileObservation world;
        private readonly bool ownsEpoch;
        internal WorldTargetHostObservation(Func<bool> sessionActive, WorldTileObservation world = null)
        { this.sessionActive = sessionActive; ownsEpoch = world == null; this.world = world ?? new WorldTileObservation(sessionActive); }
        internal void EndSession() { if (ownsEpoch) world.EndSession(); }
        public bool TryBegin(out WorldTargetView view)
        {
            view = default(WorldTargetView);
            // Native Update already computed accessories and team sharing. This
            // gate belongs to arrows, never to the shared fact reader.
            if (!sessionActive() || Main.LocalPlayer == null || !Main.LocalPlayer.accOreFinder) return false;
            if (ownsEpoch) world.BeginTick();
            return world.TryView(WorldTargetArrows.ObservationPadding, out view);
        }
        public WorldTargetTile Read(int x, int y) { return world.Read(x, y); }
    }
}
