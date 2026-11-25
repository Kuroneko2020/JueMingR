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
        { Air = 0, Supported = 1, Down = 2, Wet = 4, Rope = 8, Rail = 16, Mount = 32, Door = 64, Ledge = 128 }

        internal struct Sample
        {
            internal long Sequence;
            internal uint Tick;
            internal Vector2 Center, Feet, Velocity;
            internal int Width;
            internal float Gravity;
            internal Contact Mode;
            internal bool Landmark, SupportKnown;
        }

        private readonly Sample[] points = new Sample[Capacity];
        private Sample previous, latest;
        private Player observedOwner;
        private bool hasObservation;
        private long write, first = 1;
        internal int Segment { get; private set; }
        internal long Latest => first <= write ? write : 0;
        internal long First => first;
        internal bool TryGetCurrent(out Sample pose)
        {
            pose = previous;
            return hasObservation;
        }

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

        internal bool TryOutletHint(Vector2 from, Vector2 goal, uint tick, out Vector2 feet)
        {
            const Contact portalContact = Contact.Down | Contact.Rope | Contact.Rail | Contact.Door | Contact.Ledge;
            long newest = Latest;
            feet = default;
            for (long sample = newest; sample >= first && sample > newest - 20; sample--)
            {
                if (!TryRead(sample, tick, out var point) || unchecked(tick - point.Tick) > 150) break;
                if (!point.Landmark || (point.Mode & portalContact) == 0 ||
                    Vector2.DistanceSquared(from, point.Feet) > 320f * 320f ||
                    Vector2.DistanceSquared(goal, point.Feet) > 256f * 256f) continue;
                feet = point.Feet;
                return true;
            }
            return false;
        }

        internal void Observe(Player owner, uint tick)
        {
            if (owner == null || !owner.active || owner.dead) { BreakSegment(); return; }
            if (!ReferenceEquals(observedOwner, owner)) BreakSegment();
            // Point at the contacted side, including inverted gravity.
            var feet = CompanionNativeContact.Feet(owner);
            var center = owner.Center;
            var velocity = owner.velocity;
            float gravity = owner.gravDir;
            // More than one body can read this native pose in one world
            // step. A real warp invalidates it even when the tick is unchanged.
            const Contact changingContact = Contact.Down | Contact.Wet | Contact.Rope | Contact.Rail | Contact.Mount;
            var medium = ReadMedium(owner);
            if (hasObservation && previous.Tick == tick && previous.Center == center && previous.Feet == feet &&
                previous.Velocity == velocity && previous.Gravity == gravity && previous.Width == owner.width &&
                (previous.Mode & changingContact) == medium) return;
            bool supportKnown = CompanionNativeContact.TryOwnerSupport(owner, out bool supported);
            var sample = new Sample { Tick = tick, Center = center, Feet = feet, Velocity = velocity, Width = owner.width,
                Gravity = gravity, Mode = ReadMode(owner, medium, supported), SupportKnown = supportKnown };
            if (hasObservation)
            {
                uint elapsed = unchecked(tick - previous.Tick);
                // Native teleports notify the host directly. This also
                // bounds discontinuities without splitting fast native runs.
                float travel = 48 + Math.Max(sample.Velocity.Length(), previous.Velocity.Length()) * Math.Min(12u, elapsed) * 2;
                if (elapsed > 12 || previous.Gravity != gravity ||
                    Vector2.DistanceSquared(sample.Feet, previous.Feet) > travel * travel) BreakSegment();
            }
            bool newSegment = !hasObservation;
            bool leftLedge = !newSegment && supportKnown && (previous.Mode & Contact.Supported) != 0 && !supported &&
                sample.Velocity.Y * owner.gravDir > .1f;
            sample.Landmark = newSegment || sample.Mode != previous.Mode || supportKnown != previous.SupportKnown ||
                DirectionChanged(previous.Velocity, sample.Velocity);
            if (!newSegment && sample.Landmark &&
                Vector2.DistanceSquared(latest.Feet, previous.Feet) >= 6 * 6)
            {
                // The last pose before a different support/medium/heading
                // is useful, even if no full sample stride was travelled.
                previous.Landmark = true;
                if (leftLedge) previous.Mode |= Contact.Ledge;
                Append(previous);
            }
            else if (leftLedge)
            {
                // Tag an already sampled nearby contact rather than append
                // a duplicate. The real departure remains a route hint.
                latest.Mode |= Contact.Ledge;
                latest.Landmark = true;
                points[(int)(latest.Sequence % Capacity)] = latest;
            }
            if (sample.Landmark || Vector2.DistanceSquared(latest.Feet, sample.Feet) >= 16 * 16) Append(sample);
            previous = sample;
            observedOwner = owner;
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

        private static Contact ReadMedium(Player player)
        {
            Contact result = Contact.Air;
            if (player.stairFall) result |= Contact.Down;
            if (player.wet) result |= Contact.Wet;
            if (player.pulley) result |= Contact.Rope;
            if (player.onTrack) result |= Contact.Rail;
            if (player.mount.Active) result |= Contact.Mount;
            return result;
        }

        private static Contact ReadMode(Player player, Contact medium, bool supported)
        {
            Contact result = supported ? medium | Contact.Supported : medium;
            // Open leaves are still a useful landmark at a narrow door.
            Point at = player.Center.ToTileCoordinates();
            var tile = CompanionMobilityGeometry.Known(at.X, at.Y) ? Main.tile[at.X, at.Y] : null;
            if (tile != null && tile.nactive() && (tile.type == TileID.OpenDoor || tile.type == TileID.ClosedDoor))
                result |= Contact.Door;
            return result;
        }
    }
}
