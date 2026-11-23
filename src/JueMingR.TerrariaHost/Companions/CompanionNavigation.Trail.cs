using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal readonly struct CompanionGoalSample
    {
        internal readonly Vector2 Position, Velocity;
        internal readonly CompanionOwnerTrail Trail;
        internal readonly long Epoch;

        internal CompanionGoalSample(Vector2 position, Vector2 velocity, CompanionOwnerTrail trail, long epoch)
        {
            Position = position;
            Velocity = velocity;
            Trail = trail;
            Epoch = epoch;
        }
    }

    internal sealed partial class CompanionNavigation
    {
        private sealed class GoalTracker
        {
            private Player owner;
            private object world;
            private CompanionOwnerTrail observedTrail;
            private bool initialized, follow;
            private Vector2 anchor;
            private long epoch;
            private int trailSegment;
            private long trailConsumed, trailJoined, nextTrailProbe;
            private uint nextTrailSelection;
            private Vector2 trailOwnerAtJoin;
            internal bool Joined => trailJoined != 0;

            internal CompanionGoalSample Sample(Player body, Player currentOwner, bool follows, CompanionOwnerTrail trail)
            {
                if (!follows || currentOwner == null || !currentOwner.active || currentOwner.dead)
                {
                    currentOwner = null;
                    trail = null;
                }

                int segment = trail?.Segment ?? 0;
                bool sourceChanged = !initialized || follow != follows || !ReferenceEquals(owner, currentOwner) ||
                    !ReferenceEquals(world, Main.ActiveWorldFileData);
                if (sourceChanged || !ReferenceEquals(observedTrail, trail) || segment != trailSegment)
                {
                    epoch++;
                    ClearTrailProgress();
                    observedTrail = trail;
                    trailSegment = segment;
                    if (sourceChanged && currentOwner == null) anchor = body.Center;
                    initialized = true;
                    owner = currentOwner;
                    world = Main.ActiveWorldFileData;
                    follow = follows;
                }

                Vector2 position = owner?.Center ?? anchor;
                if (trail != null && trail.RecentlyGrounded && body.gravDir == owner.gravDir)
                    position.Y = owner.gravDir > 0 ? owner.Bottom.Y - body.height * .5f : owner.Top.Y + body.height * .5f;
                return new CompanionGoalSample(position, owner?.velocity ?? Vector2.Zero, trail, epoch);
            }

            internal void Reanchor(Player body)
            {
                anchor = body.Center;
                if (initialized && owner == null) epoch++;
                ClearTrailProgress();
            }

            private void ClearTrailProgress()
            {
                trailConsumed = trailJoined = nextTrailProbe = 0;
                nextTrailSelection = 0;
            }

            private static Vector2 TrailPosition(Player body, CompanionOwnerTrail.Sample sample) =>
                sample.Feet - new Vector2(body.width * .5f, body.gravDir > 0 ? body.height : 0);

            internal bool RetainTrailGoal(Player body, CompanionOwnerTrail trail, RouteMotion motion, Vector2 currentOwner,
                bool hasAction, ref Vector2 goal, out bool retired)
            {
                retired = false;
                if (trail == null || trailJoined == 0) return false;
                if (!trail.TryRead(trailJoined, Main.GameUpdateCount, out var sample) ||
                    GoalRedirects(body.position, trailOwnerAtJoin, currentOwner))
                {
                    trailConsumed = trailJoined = nextTrailProbe = 0;
                    retired = true;
                    return false;
                }

                var at = TrailPosition(body, sample);
                if (!hasAction && Reached(body.position, at, 12) &&
                    InGoalRegion(body, body.position, at, motion, null))
                {
                    trailConsumed = trailJoined;
                    trailJoined = 0;
                    return false;
                }
                if (!hasAction && Reached(body.position, currentOwner, 32) &&
                    InGoalRegion(body, body.position, currentOwner, motion, null))
                {
                    trailConsumed = trail.Latest;
                    trailJoined = 0;
                    retired = true;
                    return false;
                }
                goal = at;
                return true;
            }

            internal bool OfferTrailGoal(Player body, CompanionOwnerTrail trail, RouteMotion motion, Vector2 ownerGoal, out Vector2 goal, out int work)
            {
                goal = ownerGoal;
                work = 0;
                uint tick = Main.GameUpdateCount;
                long newest = trail.Latest;
                if (trailJoined != 0 || newest < trail.First + 2 ||
                    unchecked((int)(tick - nextTrailSelection)) < 0 || motion != RouteMotion.Ground && motion != RouteMotion.Air)
                    return false;
                if (trailConsumed < trail.First || trailConsumed >= newest) trailConsumed = 0;
                bool connected = trailConsumed != 0;
                long first = connected ? trailConsumed + 1 : trail.First;
                long cursor = connected ? first : nextTrailProbe >= first && nextTrailProbe < newest ? nextTrailProbe : newest - 1;
                for (int inspected = 0; inspected < 6 && cursor >= first && cursor < newest; inspected++, cursor += connected ? 1 : -1)
                {
                    if (!trail.TryRead(cursor, tick, out var sample)) break;
                    Vector2 at = TrailPosition(body, sample);
                    var delta = at - body.position;
                    bool standing = (sample.Mode & CompanionOwnerTrail.Contact.Supported) != 0;
                    bool near = Math.Abs(delta.X) <= 48 && Math.Abs(delta.Y) <= (connected ? 64 : 16);
                    if (!near || !standing && motion == RouteMotion.Ground) continue;
                    if (Math.Abs(delta.Y) < 4 && delta.LengthSquared() < 10 * 10)
                    {
                        if (connected) trailConsumed = cursor;
                        continue;
                    }
                    if (Vector2.DistanceSquared(at, ownerGoal) < 12 * 12) break;
                    // Proof of the first join belongs to the selected planning
                    // slice. Later trail points remain goals for this same graph,
                    // including a nearby lower support that needs an actual drop.
                    if (!connected)
                    {
                        work = 58;
                        nextTrailProbe = cursor - 1;
                        if (!InGoalRegion(body, body.position, at, motion, null)) return false;
                    }
                    trailJoined = cursor;
                    trailOwnerAtJoin = ownerGoal;
                    goal = at;
                    return true;
                }
                if (!connected) nextTrailProbe = cursor < first ? 0 : cursor;
                nextTrailSelection = tick + (connected ? 5u : 2u);
                return false;
            }

            internal void RejectHint()
            {
                if (trailJoined == 0) return;
                // An unjoinable waypoint only expires the hint. Its terrain
                // remains useful for the live destination and other exits.
                nextTrailProbe = trailJoined - 1;
                trailConsumed = trailJoined = 0;
                nextTrailSelection = Main.GameUpdateCount + 18;
            }
        }
    }
}
