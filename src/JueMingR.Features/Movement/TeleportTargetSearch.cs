using System;
using System.Collections.Generic;

namespace JueMingR.Features.Movement
{
    public interface ITeleportTargetValidator
    {
        bool IsValid(float x, float y);
    }

    // Only geometry is retained. World, progression and player dimensions are
    // deliberately queried for each real use; a cached vacancy becomes stale.
    public static class TeleportTargetSearch
    {
        public const int Radius = 208;
        public const int Step = 8;
        private static readonly Offset[] offsets = CreateOffsets();
        public static int CandidateCount => offsets.Length;

        public static bool TryFind<T>(float originX, float originY, ref T validator,
            out float x, out float y, out int enumerated) where T : struct, ITeleportTargetValidator
        {
            x = originX; y = originY; enumerated = 0;
            if (!Finite(originX) || !Finite(originY)) return false;
            // Legal original destinations do not enumerate even one offset.
            if (validator.IsValid(originX, originY)) return false;
            for (int i = 0; i < offsets.Length; i++)
            {
                float nextX = originX + offsets[i].X, nextY = originY + offsets[i].Y;
                enumerated++;
                if (!validator.IsValid(nextX, nextY)) continue;
                x = nextX; y = nextY; return true;
            }
            return false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static Offset[] CreateOffsets()
        {
            var result = new List<Offset>();
            for (int y = -Radius; y <= Radius; y += Step)
                for (int x = -Radius; x <= Radius; x += Step)
                    if ((x != 0 || y != 0) && x * x + y * y <= Radius * Radius)
                        result.Add(new Offset(x, y));
            result.Sort((a, b) =>
            {
                int distance = a.DistanceSquared.CompareTo(b.DistanceSquared);
                if (distance != 0) return distance;
                int vertical = a.Y.CompareTo(b.Y);
                return vertical != 0 ? vertical : a.X.CompareTo(b.X);
            });
            return result.ToArray();
        }
        private struct Offset
        {
            internal readonly int X, Y, DistanceSquared;
            internal Offset(int x, int y) { X = x; Y = y; DistanceSquared = x * x + y * y; }
        }
    }
}
