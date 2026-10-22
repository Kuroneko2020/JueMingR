using System;

namespace JueMingR.Features.Movement
{
    public enum SmartFallFamily { DoubleJump, RocketBoots, Carpet, Wings, Horseshoe, Umbrella, Grapple, FlyingMount, DjinnLegs, SafeMount, TeleportRod, GravityGlobe }
    public enum SmartFallResult { None, Prepared, Consumed, Protected, Returning, Returned, TakenOver, Failed, Unconfirmed }

    public struct SmartFallContact
    {
        public bool Found, Platform, SafeSurface;
        public float Ticks, X, Y;
        public int TileX, TileY;
        public byte Slope;
        public ushort Type;
        public short FrameY;
        public bool HalfBrick;
    }

    public struct SmartFallCandidate
    {
        public SmartFallFamily Family;
        public bool Ready;
        // Native consumers needed before the collision, including equip and
        // release preparation. Zero denotes a current-update consumer.
        public int Delay;
        public int Interference;
    }

    public static class SmartFallDecision
    {
        public static bool Risk(float y, int fallStart, float gravityDirection, int extraFall, int mountExtraFall,
            float downwardSpeed, bool stoned, bool immuneToFall, bool naturalReset, float mountDamage)
        {
            if (downwardSpeed <= 0 || naturalReset) return false;
            float distance = ((int)(y / 16) - fallStart) * gravityDirection;
            if (stoned) return distance > 2;
            return !immuneToFall && mountDamage > .001f && distance > 25 + extraFall + mountExtraFall;
        }

        // A ready, less disruptive primary retains its own last effective
        // window. An expiring slower backup never forces that primary early.
        public static int Choose(SmartFallCandidate[] candidates, int count, float contactTicks)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                var c = candidates[i];
                if (!c.Ready || contactTicks <= c.Delay) continue;
                if (best < 0 || c.Interference < candidates[best].Interference ||
                    c.Interference == candidates[best].Interference && c.Delay < candidates[best].Delay) best = i;
            }
            return best;
        }
        public static bool Due(SmartFallCandidate candidate, float contactTicks)
        { return candidate.Ready && contactTicks > candidate.Delay && contactTicks <= candidate.Delay + 1; }

        // Surface under the *leading edge of the full box*, not its centre.
        // Slope 1/2 are floor triangles; 3/4 are ceiling triangles in .8.
        public static float Surface(int tileX, int tileY, byte slope, bool halfBrick, float left, float right, float gravityDirection)
        {
            float low = Math.Max(tileX * 16, left), high = Math.Min(tileX * 16 + 16, right);
            if (gravityDirection > 0)
            {
                if (slope == 1) return tileY * 16 + Math.Max(0, low - tileX * 16);
                if (slope == 2) return tileY * 16 + Math.Max(0, tileX * 16 + 16 - high);
                return tileY * 16 + (halfBrick ? 8 : 0);
            }
            if (slope == 3) return tileY * 16 + 16 - Math.Max(0, low - tileX * 16);
            if (slope == 4) return tileY * 16 + 16 - Math.Max(0, tileX * 16 + 16 - high);
            return tileY * 16 + 16;
        }
    }
}
