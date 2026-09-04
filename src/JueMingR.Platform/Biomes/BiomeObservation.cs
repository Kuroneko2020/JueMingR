using System;

namespace JueMingR.Platform.Biomes
{
    public readonly struct BiomeObservation : IEquatable<BiomeObservation>
    {
        public BiomeObservation(BiomeFlags flags)
        {
            Flags = flags;
        }

        public BiomeFlags Flags { get; }

        public bool Equals(BiomeObservation other)
        {
            return Flags == other.Flags;
        }

        public override bool Equals(object obj)
        {
            return obj is BiomeObservation && Equals((BiomeObservation)obj);
        }

        public override int GetHashCode()
        {
            return (int)Flags;
        }

        public static bool operator ==(BiomeObservation left, BiomeObservation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BiomeObservation left, BiomeObservation right)
        {
            return !left.Equals(right);
        }
    }
}
