using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    // One observed trail per owner session. These points are hints; they
    // cannot authorize controls or supply another body's movement resources.
    internal sealed class CompanionOwnerTrail
    {
        private const int Capacity = 256;
        private const uint Lifetime = 1800;
        [Flags]
        internal enum Contact : byte
        { Air = 0, Supported = 1, Down = 2, Wet = 4, Rope = 8, Rail = 16, Mount = 32, Door = 64 }

        internal struct Sample
        {
            internal long Sequence;
            internal uint Tick;
            internal Vector2 Feet, Velocity;
            internal Contact Mode;
            internal bool Landmark;
        }

        private readonly Sample[] points = new Sample[Capacity];
        private Sample previous, latest;
        private bool hasObservation;
        private long write, first = 1;
        internal int Segment { get; private set; }
        internal long Latest => first <= write ? write : 0;
        internal long First => first;
        internal bool RecentlyGrounded => hasObservation && (previous.Mode & Contact.Supported) != 0;

        internal void BreakSegment()
        {
            if (!hasObservation && first > write) return;
            Segment++;
            hasObservation = false;
            first = write + 1;
        }

        internal bool TryRead(long sequence, uint tick, out Sample point)
        {
            point = default;
            if (sequence < first || sequence > write) return false;
            point = points[(int)(sequence % Capacity)];
            return unchecked(tick - point.Tick) <= Lifetime;
        }

        internal void Observe(Player owner, uint tick)
        {
            if (owner == null || !owner.active || owner.dead) { BreakSegment(); return; }
            if (hasObservation && previous.Tick == tick) return;
            bool supported = CompanionNavigation.OwnerHasSupport(owner);
            // Point at the contacted side, including inverted gravity.
            var feet = owner.gravDir >= 0 ? owner.Bottom : owner.Top;
            var sample = new Sample { Tick = tick, Feet = feet, Velocity = owner.velocity, Mode = ReadMode(owner, supported) };
            if (hasObservation)
            {
                uint elapsed = unchecked(tick - previous.Tick);
                // Native teleports notify the host directly. This also
                // bounds discontinuities without splitting fast native runs.
                float travel = 48 + Math.Max(sample.Velocity.Length(), previous.Velocity.Length()) * Math.Min(12u, elapsed) * 2;
                if (elapsed > 12 || Vector2.DistanceSquared(sample.Feet, previous.Feet) > travel * travel) BreakSegment();
            }
            bool newSegment = !hasObservation;
            sample.Landmark = newSegment || sample.Mode != previous.Mode ||
                DirectionChanged(previous.Velocity, sample.Velocity);
            if (!newSegment && sample.Landmark &&
                Vector2.DistanceSquared(latest.Feet, previous.Feet) >= 6 * 6)
            {
                // The last pose before a different support/medium/heading
                // is useful, even if no full sample stride was travelled.
                previous.Landmark = true;
                Append(previous);
            }
            if (sample.Landmark || Vector2.DistanceSquared(latest.Feet, sample.Feet) >= 16 * 16) Append(sample);
            previous = sample;
            hasObservation = true;
        }

        private void Append(Sample point)
        {
            point.Sequence = ++write;
            points[(int)(write % Capacity)] = latest = point;
            if (write - first >= Capacity) first = write - Capacity + 1;
        }

        private static bool DirectionChanged(Vector2 before, Vector2 after) =>
            before.LengthSquared() >= 1 && after.LengthSquared() >= 1 &&
            Vector2.Dot(before, after) < before.Length() * after.Length() * .65f;

        private static Contact ReadMode(Player player, bool supported)
        {
            Contact result = supported ? Contact.Supported : Contact.Air;
            if (player.stairFall) result |= Contact.Down;
            if (player.wet) result |= Contact.Wet;
            if (player.pulley) result |= Contact.Rope;
            if (player.onTrack) result |= Contact.Rail;
            if (player.mount.Active) result |= Contact.Mount;
            // Open leaves are still a useful landmark at a narrow door.
            Point at = player.Center.ToTileCoordinates();
            var tile = WorldGen.InWorld(at.X, at.Y) ? Main.tile[at.X, at.Y] : null;
            if (tile != null && tile.nactive() && (tile.type == TileID.OpenDoor || tile.type == TileID.ClosedDoor))
                result |= Contact.Door;
            return result;
        }
    }
}
