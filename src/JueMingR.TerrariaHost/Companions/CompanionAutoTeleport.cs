using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Companions
{
    // One activation's simulation clock and recent physical follow positions.
    // Navigation can change its desired route without changing this clock.
    internal sealed class CompanionAutoTeleport
    {
        private const int TicksPerSecond = 60;
        private const int DistanceHold = 2 * TicksPerSecond;
        private const float PhysicalStep = 8f;
        private const float RecentRadiusSquared = 10f * 10f;
        private const int RecentCapacity = 24;
        private const int SharedTravelPassages = 4;

        private struct Passage
        {
            internal Vector2 Feet, Owner, OwnerTravel;
        }

        private Passage[] recent;
        private CompanionBehavior settings;
        private int next, count, pairedPassages;
        private bool enabled, blocked, distance, following;
        private int blockedSeconds, distanceTiles;
        private long blockedUpdates;
        private int distantUpdates;
        private Vector2 before, offeredAxis, checkpoint, ownerCheckpoint, axis;
        private bool captured, temporary;

        internal uint ObservedAt { get; private set; }
        internal bool HasCandidate { get; private set; }
#if DEBUG
        internal int Samples { get; private set; }
        internal int PassageComparisons { get; private set; }
#endif

        internal void Configure(CompanionBehavior options)
        {
            if (ReferenceEquals(settings, options)) return;
            settings = options;
            bool active = options.AutoTeleportEnabled && options.Follow;
            if (enabled != active) ResetObservation();
            if (blocked != options.AutoTeleportBlockedEnabled || blockedSeconds != options.AutoTeleportBlockedSeconds)
            {
                blockedUpdates = 0;
                count = next = pairedPassages = 0;
                captured = false;
                axis = Vector2.Zero;
            }
            if (distance != options.AutoTeleportDistanceEnabled || distanceTiles != options.AutoTeleportDistanceTiles)
                distantUpdates = 0;
            enabled = active;
            blocked = options.AutoTeleportBlockedEnabled;
            blockedSeconds = options.AutoTeleportBlockedSeconds;
            distance = options.AutoTeleportDistanceEnabled;
            distanceTiles = options.AutoTeleportDistanceTiles;
            if (!ReachedBlocked && distantUpdates < DistanceHold) HasCandidate = false;
        }

        internal void ResetObservation()
        {
            following = captured = HasCandidate = false;
            blockedUpdates = distantUpdates = count = next = pairedPassages = 0;
            axis = Vector2.Zero;
        }

        internal void CaptureMovement(Vector2 feet, Vector2 direction, bool temporaryGoal)
        {
            before = feet;
            offeredAxis = direction;
            temporary = temporaryGoal;
            captured = true;
        }

        internal void Observe(uint tick, Vector2 ownerCenter, Vector2 center, Vector2 feet, bool settled, bool controlled)
        {
            if (following && ObservedAt == tick) return;
            following = true;
            ObservedAt = tick;
#if DEBUG
            Samples++;
#endif
            if (distance) distantUpdates = Far(ownerCenter, center) ? System.Math.Min(DistanceHold, distantUpdates + 1) : 0;
            if (blocked)
            {
                if (settled)
                {
                    blockedUpdates = 0;
                    next = count = pairedPassages = 0;
                    axis = Vector2.Zero;
                }
                else if (!controlled)
                {
                    if (captured && Advance(feet, ownerCenter + feet - center)) blockedUpdates = 0;
                    else if (blockedUpdates < BlockedHold) blockedUpdates++;
                }
            }
            captured = false;
            HasCandidate = ReachedBlocked || distantUpdates == DistanceHold;
        }

        private bool Advance(Vector2 feet, Vector2 owner)
        {
            if (count == 0)
            {
                recent = recent ?? new Passage[RecentCapacity];
                Remember(new Passage { Feet = before, Owner = owner });
                checkpoint = before;
                ownerCheckpoint = owner;
            }
            if (offeredAxis == Vector2.Zero || Vector2.Dot(feet - before, offeredAxis) <= 0f) return false;
            if (axis == Vector2.Zero || Vector2.Dot(axis, offeredAxis) < .5f)
            {
                axis = offeredAxis;
                checkpoint = before;
            }
            if (Vector2.Dot(feet - checkpoint, axis) < PhysicalStep) return false;
            checkpoint = feet;
            Vector2 ownerTravel = owner - ownerCheckpoint;
            ownerCheckpoint = owner;
            if (temporary) pairedPassages = 0;
            if (temporary && Vector2.DistanceSquared(feet, owner) >= Vector2.DistanceSquared(feet - axis * PhysicalStep, owner))
                return false;

            // Only inspect the ring on a real physical advance. A newly
            // planned action cannot reset it; repeatedly reaching the same
            // little pocket doesn't earn more time. A returning owner can
            // make an old position useful again by actually crossing it.
            // On a loop the owner can already be rounding the next turn.
            // Compare both the current direction and recent footprints the
            // owner physically traversed in the body's direction. Sustained
            // shared travel can reuse positions without aging out the ring.
            int revisit = -1;
            bool together = !temporary && Vector2.Dot(ownerTravel, axis) >= PhysicalStep / 4f;
            bool canMatchTrail = !temporary && ownerTravel.LengthSquared() >= PhysicalStep * PhysicalStep / 16f;
            for (int i = 0; i < count; i++)
            {
#if DEBUG
                PassageComparisons++;
#endif
                var mark = recent[i];
                if (revisit < 0 && Vector2.DistanceSquared(feet, mark.Feet) < RecentRadiusSquared) revisit = i;
                if (!together && canMatchTrail && Vector2.DistanceSquared(feet, mark.Owner) < RecentRadiusSquared &&
                    Vector2.Dot(mark.OwnerTravel, axis) >= PhysicalStep / 4f) together = true;
                if (revisit >= 0 && (together || !canMatchTrail)) break;
            }
            if (!together) pairedPassages = 0;
            else if (pairedPassages < SharedTravelPassages) pairedPassages++;
            var current = new Passage { Feet = feet, Owner = owner, OwnerTravel = ownerTravel };
            if (revisit >= 0)
            {
                float previousSide = Vector2.Dot(recent[revisit].Owner - feet, axis);
                float currentSide = Vector2.Dot(owner - feet, axis);
                if (pairedPassages < SharedTravelPassages && (previousSide >= -PhysicalStep || currentSide <= PhysicalStep)) return false;
                recent[revisit] = current;
                return true;
            }
            Remember(current);
            return true;
        }

        private void Remember(Passage passage)
        {
            recent[next] = passage;
            next = (next + 1) % RecentCapacity;
            if (count < RecentCapacity) count++;
        }

        internal bool StillWants(Vector2 owner, Vector2 center, bool settled)
        {
            if (settled) blockedUpdates = 0;
            if (distance && !Far(owner, center)) distantUpdates = 0;
            return HasCandidate = ReachedBlocked || distantUpdates == DistanceHold;
        }

        private long BlockedHold => (long)blockedSeconds * TicksPerSecond;
        private bool ReachedBlocked => blocked && blockedUpdates >= BlockedHold;

        private bool Far(Vector2 owner, Vector2 center)
        {
            double limit = (double)distanceTiles * 16;
            return Vector2.DistanceSquared(owner, center) >= limit * limit;
        }
    }
}
