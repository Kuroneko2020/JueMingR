using System;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    // The original collision routines update static scratch even during read-only forecasts.
    internal readonly struct CompanionCollisionScope : IDisposable
    {
        private readonly bool up, down, stair, fall, slope, honey, shimmer, mine;

        private CompanionCollisionScope(bool up, bool down, bool stair, bool fall, bool slope, bool honey, bool shimmer, bool mine)
        {
            this.up = up;
            this.down = down;
            this.stair = stair;
            this.fall = fall;
            this.slope = slope;
            this.honey = honey;
            this.shimmer = shimmer;
            this.mine = mine;
        }

        internal static CompanionCollisionScope Save() => new CompanionCollisionScope(Collision.up, Collision.down,
            Collision.stair, Collision.stairFall, Collision.sloping, Collision.honey, Collision.shimmer, Collision.landMine);

        public void Dispose()
        {
            Collision.up = up;
            Collision.down = down;
            Collision.stair = stair;
            Collision.stairFall = fall;
            Collision.sloping = slope;
            Collision.honey = honey;
            Collision.shimmer = shimmer;
            Collision.landMine = mine;
        }
    }
}
