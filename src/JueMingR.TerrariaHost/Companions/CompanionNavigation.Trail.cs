using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private CompanionOwnerTrail followedTrail;
        private int trailSegment, localPlanningWork;
        private long trailConsumed, trailJoined, nextTrailProbe;
        private uint nextTrailSelection;
        private Vector2 trailOwnerAtJoin;

        private void ClearTrailProgress()
        {
            followedTrail = null;
            trailConsumed = trailJoined = nextTrailProbe = 0;
            nextTrailSelection = 0;
        }

        private static Vector2 TrailPosition(Player body, CompanionOwnerTrail.Sample sample) =>
            sample.Feet - new Vector2(body.width * .5f, body.gravDir > 0 ? body.height : 0);

        private bool RetainTrailGoal(Player body, CompanionMobilityState state, CompanionOwnerTrail trail, RouteMotion motion,
            Vector2 currentOwner, ref Vector2 goal)
        {
            if (trail == null) return false;
            if (!ReferenceEquals(trail, followedTrail) || trail.Segment != trailSegment)
            {
                if (followedTrail != null)
                {
                    if (trailJoined != 0) RetireTrailGoal(body, state, motion);
                    else
                    {
                        // A live target already known to the planner can
                        // precede the owner's discontinuity observation.
                        // Retire only work that still names the old target.
                        if (publishing >= 0 && Vector2.DistanceSquared(publicationGoal, currentOwner) > 8 * 8)
                            RetirePublication();
                        if (pendingResult >= 0 && Vector2.DistanceSquared(pendingGoal, currentOwner) > 8 * 8)
                            pendingResult = -1;
                        if (routeIndex < route.Count && Vector2.DistanceSquared(routeGoal, currentOwner) > 8 * 8)
                            RetireTrailSuffix(body, state, motion);
                    }
                }
                ClearTrailProgress();
                followedTrail = trail;
                trailSegment = trail.Segment;
            }
            if (trailJoined == 0) return false;
            if (!trail.TryRead(trailJoined, Main.GameUpdateCount, out var sample) ||
                GoalRedirects(body.position, trailOwnerAtJoin, currentOwner))
            {
                RetireTrailGoal(body, state, motion);
                trailConsumed = nextTrailProbe = 0;
                return false;
            }
            var anchor = TrailPosition(body, sample);
            if (routeIndex >= route.Count && Reached(body.position, anchor, 12) &&
                InGoalRegion(body, body.position, anchor, motion, null))
            {
                trailConsumed = trailJoined;
                trailJoined = 0;
                return false;
            }
            // Near the current owner and on the same proven tier, take the
            // shortcut at a safe action boundary instead of replaying turns.
            if (routeIndex >= route.Count && Reached(body.position, currentOwner, 32) &&
                InGoalRegion(body, body.position, currentOwner, motion, null))
            { trailConsumed = trail.Latest; RetireTrailGoal(body, state, motion); return false; }
            goal = anchor;
            return true;
        }

        private void RetireTrailGoal(Player body, CompanionMobilityState state, RouteMotion motion)
        {
            // Retire the stale goal and its unissued suffix. The existing
            // real release/landing remains responsible for this body.
            trailJoined = 0;
            RetirePublication();
            pendingResult = -1;
            RetireTrailSuffix(body, state, motion);
        }

        private void RetireTrailSuffix(Player body, CompanionMobilityState state, RouteMotion motion)
        {
            if (routeIndex >= route.Count) return;
            var step = route[routeIndex];
            bool committed;
            switch (step.Kind)
            {
                case RouteKind.Drop: case RouteKind.LedgeDrop:
                    committed = actionStarted || body.stairFall || motion == RouteMotion.Air &&
                        body.velocity.Y * body.gravDir > 0 && (body.position.Y - actionOrigin.Y) * body.gravDir > 0;
                    break;
                case RouteKind.Jump: case RouteKind.GlideJump: case RouteKind.RopeExit:
                    committed = jumpAccepted || jumpStarted && body.velocity.Y * body.gravDir < 0;
                    break;
                default:
                    committed = actionStarted && commandSequence != 0 && state.Sequence == commandSequence &&
                        (state.Result == CompanionMobilityResult.Started || state.Result == CompanionMobilityResult.Completed);
                    break;
            }
            if (!committed) { ReleaseRoute(); return; }
            if (routeIndex + 1 < route.Count) route.RemoveRange(routeIndex + 1, route.Count - routeIndex - 1);
        }

        private bool OfferTrailGoal(Player body, CompanionOwnerTrail trail, RouteMotion motion, Vector2 ownerGoal, out Vector2 goal)
        {
            goal = ownerGoal;
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
                    localPlanningWork = 58;
#if DEBUG
                    RouteWork += localPlanningWork;
#endif
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

        private void RejectExhaustedTrail()
        {
            if (trailJoined == 0 || search == null || !search.Exhausted || search.Active || pendingResult >= 0 || publishing >= 0) return;
            // A failed owner waypoint is not a restriction on the current
            // live destination. Retry near that segment only after progress
            // or the ordinary connection/terrain rules make another join.
            nextTrailProbe = trailJoined - 1;
            trailConsumed = trailJoined = 0;
            nextTrailSelection = Main.GameUpdateCount + 18;
        }
    }
}
