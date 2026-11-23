using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private RouteSearch search;
        private readonly RouteConnector connector = new RouteConnector();
        private readonly RouteRecovery recovery = new RouteRecovery();
        private RouteCandidate pending;
        private RoutePlan ready;
        private long planningGoalEpoch, nextPlanId;
        private long checkpointPlan;
        private ProofHandle checkpointProof;
        private RouteStep checkpointEnd;
        private bool compactAfterContact;
        private bool recoveryPending;

        private bool PlannerHasWork => search != null && (search.Active || recoveryPending);
        private bool PlannerHasCandidate => pending.End.IsSet || connector.Active || ready.PlanId != 0;
        private bool PlannerConnecting => connector.Active;
        private bool PlannerTrailClosed => search != null && search.Initialized && search.Exhausted && !PlannerHasCandidate;
        private string PlannerStopStatus => search != null && search.Terrain.HasUnknown ? "等待地形就绪" :
            search != null && search.Exhausted && search.FullyKnownClosure && !recoveryPending ? "暂无可行路线" : "寻找路线";
        private bool PlannerShouldConnectEarly(RouteMotion motion) => search != null &&
            (pending.End.IsSet && search.NeedsLiveRoot || motion == RouteMotion.Ground &&
                (connector.Active || pending.End.IsSet && pending.Kind >= RouteResultKind.Corridor));

        // Read-only diagnostics keep the candidate/failed-action provenance
        // visible without shadowing either owner's state.
        private int pendingResult => pending.End.IsSet ? pending.End.Slot : -1;
        private int publishing => connector.Active ? connector.Candidate.End.Slot : -1;
        private RouteStep blockedStep => recovery.MostRecent;
        private uint blockedAt => recovery.LastRejectedAt;

        private void SynchronizePlanning(long goalEpoch, bool bodyChanged)
        {
            if (planningGoalEpoch != goalEpoch)
            {
                planningGoalEpoch = goalEpoch;
                RetirePlannedCandidate();
            }
            if (bodyChanged && search != null) search.NeedsLiveRoot = true;
        }

        private void PositionBindingChanged(bool worldChanged)
        {
            RetirePlannedCandidate();
            recovery.ForgetBody();
            recoveryPending = compactAfterContact = false;
            checkpointPlan = 0;
            checkpointProof = default;
            if (worldChanged) search = null;
            else if (search != null) search.NeedsLiveRoot = true;
        }

        private void RetirePlannedCandidate()
        { pending = default; ready = default; connector.Retire(); }

        private bool TakeConnectedRoute(out RoutePlan plan)
        {
            plan = ready;
            ready = default;
            return plan.PlanId != 0 && plan.GoalEpoch == planningGoalEpoch;
        }

        private bool RecentlyRejected(Player body, RouteStep step) => recovery.Suppresses(step, body);

        private void ReportActionOutcome(in ActionOutcome outcome)
        {
            if (checkpointPlan != 0 && outcome.PlanId == checkpointPlan)
            {
                switch (outcome.Result)
                {
                    case ActionResult.Rejected: case ActionResult.Deviated: case ActionResult.LandedElsewhere:
                    case ActionResult.Reanchor: case ActionResult.Superseded: case ActionResult.Retired:
                        ReleaseCheckpoint();
                        break;
                    case ActionResult.Reached:
                        if (!outcome.LastStep) break;
                        if (outcome.PrefixTrimmed || outcome.Step.Kind != checkpointEnd.Kind || outcome.Step.To != checkpointEnd.To)
                        { ReleaseCheckpoint(); break; }
                        // An original contact, not a connected projection,
                        // releases the old fine-grained ancestors.
                        checkpointPlan = 0;
                        checkpointProof = default;
                        compactAfterContact = recoveryPending = true;
                        break;
                }
            }
            switch (outcome.Result)
            {
                case ActionResult.Rejected: case ActionResult.Deviated:
                    recovery.Record(outcome, Main.GameUpdateCount);
                    recoveryPending = true;
                    connector.Retire();
                    break;
                case ActionResult.LandedElsewhere: case ActionResult.Reanchor:
                    // The suffix expected a different support or departure.
                    // Keep remote proofs; seed a real root from this receipt.
                    recoveryPending = true;
                    connector.Retire();
                    break;
                case ActionResult.Reached:
                    if (outcome.Step.ObserveAfter) recoveryPending = true;
                    break;
            }
        }

        private void ReleaseCheckpoint()
        {
            search?.ReleaseUnreachableCheckpoint(checkpointProof);
            checkpointPlan = 0;
            checkpointProof = default;
        }

        private void ReportDescentObstructionChanged(Point tile, bool nearby)
        {
            if (search == null) return;
            search.Dirty(new Rectangle(tile.X, tile.Y, 1, 1));
            search.NeedsLiveRoot = true;
            search.PrioritizeDescentWake |= nearby;
            recoveryPending = true;
            connector.Retire();
            if (nearby) pending = default;
        }

        private bool OfferResult(Player body, CompanionMobilitySnapshot mobility, RouteMotion motion, int node,
            Vector2 goal, RouteResultKind kind, bool publish, ref int remaining)
        {
            var candidate = new RouteCandidate(search.Reference(node), goal, kind, planningGoalEpoch);
            if (!search.TryRead(candidate.End, out var proof) || proof.Previous < 0 || recovery.Suppresses(candidate.End, body)) return false;
            if (publish) return Propose(body, mobility, motion, candidate, ref remaining);
            StoreResult(candidate);
            return kind == RouteResultKind.Goal;
        }

        private void StoreResult(RouteCandidate candidate)
        {
            if (!search.TryRead(candidate.End, out var node)) return;
            if (!search.TryRead(pending.End, out var previous) || candidate.Kind > pending.Kind ||
                candidate.Kind == pending.Kind && Estimate(node.Position, candidate.Goal) < Estimate(previous.Position, candidate.Goal))
            {
                pending = candidate;
                search.Protect(pending.End, connector.Candidate.End);
            }
        }

        private bool Propose(Player body, CompanionMobilitySnapshot mobility, RouteMotion motion, RouteCandidate candidate, ref int remaining)
        {
            if (candidate.GoalEpoch != planningGoalEpoch || !search.TryRead(candidate.End, out _) || recovery.Suppresses(candidate.End, body)) return false;
            connector.Start(candidate);
            ContinuePublication(body, mobility, motion, ref remaining);
            // One attempt per selected slice. Further attempts include the
            // connector's charged work in the same team allowance.
            return true;
        }

        private bool ContinuePublication(Player body, CompanionMobilitySnapshot mobility, RouteMotion motion, ref int remaining)
        {
            var candidate = connector.Candidate;
            int before = remaining;
            var result = connector.Advance(search, body, mobility, motion, ref remaining, out List<RouteStep> steps, out bool suffix);
#if DEBUG
            RouteWork += before - remaining;
#endif
            if (result == ConnectionResult.Wait) return false;
            connector.Retire();
            if (result == ConnectionResult.Ready || result == ConnectionResult.Align)
            {
                ready = new RoutePlan(steps, candidate, ++nextPlanId);
#if DEBUG
                if (connector.UsedPassages)
                {
                    RoutePassageConnections++;
                    foreach (var step in steps)
                    {
                        if (!step.ObserveAfter && Math.Abs(step.To.Y - step.From.Y) < 8) continue;
                        RoutePassageTransitions++;
                        break;
                    }
                }
#endif
                if (candidate.Kind == RouteResultKind.Checkpoint)
                {
                    checkpointPlan = ready.PlanId;
                    checkpointProof = candidate.End;
                    checkpointEnd = steps[steps.Count - 1];
                }
                else if (suffix) StoreResult(candidate);
                return true;
            }
            if (result == ConnectionResult.Reanchor || result == ConnectionResult.NoConnection)
            {
                recovery.Record(candidate.End, body, Main.GameUpdateCount);
                if (candidate.Kind == RouteResultKind.Checkpoint) search.ReleaseUnreachableCheckpoint(candidate.End);
                if (result == ConnectionResult.Reanchor) search.NeedsLiveRoot = true;
            }
            return false;
        }

#if DEBUG
        internal int RoutePassageConnections { get; private set; }
        internal int RoutePassageTransitions { get; private set; }
#endif
    }
}
