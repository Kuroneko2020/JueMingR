using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private readonly MovementCoordinator coordinator;
        private ActionExecutor actions => coordinator.Actions;
        private GoalTracker goals => coordinator.Goals;
        internal bool AcceptsCombatIntent => !actions.HasStep;

        internal CompanionNavigation()
        {
            coordinator = new MovementCoordinator(this);
        }

        internal CompanionGoalSample ObserveGoal(Player body, Player owner, bool follow, CompanionOwnerTrail trail) =>
            goals.Sample(body, owner, follow, trail);

        internal void RecordMovementDelivery(CompanionMovementDelivery delivery) => actions.Record(delivery);

        internal void ObserveMovement(Player body, CompanionMobilityState native) => actions.Observe(body, native);

        internal void RetirePosition(Player body)
        {
            goals.Reanchor(body);
            coordinator.Retire(body);
        }

        internal void CancelRoute() => coordinator.Cancel();

        internal CompanionMobilityFrame Resolve(Player body, CompanionMobilityState state, CompanionGoalSample goal,
            bool grounded, bool allowPlanning, Vector2 localTarget, bool danger, bool needsAir, bool lava, int escape,
            bool escapeJump, bool combat, bool combatJump) =>
            coordinator.Resolve(body, state, goal, grounded, allowPlanning, localTarget, danger, needsAir, lava,
                escape, escapeJump, combat, combatJump);

        private sealed class MovementCoordinator
        {
            private readonly CompanionNavigation navigation;
            private Player body;
            private object world;
            private long generation;
            private int positionRevision;
            private bool invalidated;
            internal bool ProofGrantUsed { get; private set; }
            internal void ConsumeProofGrant() => ProofGrantUsed = true;
            internal long GoalEpoch { get; private set; }
            internal readonly GoalTracker Goals;
            internal readonly ActionExecutor Actions;

            internal MovementCoordinator(CompanionNavigation navigation)
            {
                this.navigation = navigation;
                Actions = new ActionExecutor(navigation);
                Goals = new GoalTracker();
            }

            internal void Retire(Player retired)
            {
                Actions.RetireBody(retired);
                invalidated = true;
            }

            internal void Cancel()
            {
                if (body != null) Actions.RetireBody(body);
                // Explicit cancellation drops the search, as callers that
                // replace an entire terrain fixture expect. Live position
                // changes use Retire and keep the current world's proofs.
                navigation.PositionBindingChanged(worldChanged: true);
                invalidated = true;
            }

            private void Bind(Player currentBody, CompanionMobilityState state, CompanionGoalSample goal, bool grounded)
            {
                if (Actions.TryTakeOutcome(out var observed)) navigation.ReportActionOutcome(observed);
                bool worldChanged = !ReferenceEquals(world, Main.ActiveWorldFileData);
                bool changedBody = invalidated || worldChanged || !ReferenceEquals(body, currentBody) || generation != state.Generation ||
                    positionRevision != state.PositionRevision;
                if (changedBody)
                {
                    if (body != null) Actions.RetireBody(body);
                    body = currentBody;
                    world = Main.ActiveWorldFileData;
                    generation = state.Generation;
                    positionRevision = state.PositionRevision;
                    invalidated = false;
                    navigation.advancing = navigation.followingOutlet = false;
                    navigation.roofGravity = 0;
                    navigation.jumpProbeAt = navigation.fallHookAt = 0;
                    navigation.descentObstruction = null;
                    navigation.cached = false;
                    navigation.leftWalk.Valid = navigation.rightWalk.Valid = navigation.dropQuery.Valid = false;
                }
                else if (GoalEpoch != goal.Epoch)
                {
                    Actions.RetireGoal(currentBody, state, CurrentMotion(currentBody, state.Observed, grounded));
                    navigation.advancing = navigation.followingOutlet = false;
                }

                GoalEpoch = goal.Epoch;
                if (changedBody) navigation.PositionBindingChanged(worldChanged);
                navigation.SynchronizePlanning(GoalEpoch, changedBody);
                if (Actions.TryTakeOutcome(out var retired)) navigation.ReportActionOutcome(retired);
            }

            internal CompanionMobilityFrame Resolve(Player currentBody, CompanionMobilityState state, CompanionGoalSample goal,
                bool grounded, bool allowPlanning, Vector2 localTarget, bool danger, bool needsAir, bool lava, int escape,
                bool escapeJump, bool combat, bool combatJump)
            {
                Bind(currentBody, state, goal, grounded);
                ProofGrantUsed = false;
                var frame = Choose(currentBody, state, goal, grounded, allowPlanning, localTarget, danger, needsAir, lava,
                    escape, escapeJump, combat, combatJump);
                return Actions.Issue(currentBody, frame);
            }

            private CompanionMobilityFrame Choose(Player currentBody, CompanionMobilityState state, CompanionGoalSample goal,
                bool grounded, bool allowPlanning, Vector2 localTarget, bool danger, bool needsAir, bool lava, int escape,
                bool escapeJump, bool combat, bool combatJump)
            {
                if (currentBody.dead || !currentBody.active || currentBody.CCed || state.Observed.Controlled || state.Observed.Shimmer)
                {
                    Actions.RetireGoal(currentBody, state, CurrentMotion(currentBody, state.Observed, grounded));
                    return new CompanionMobilityFrame { Status = "暂时受控" };
                }
                if (!CurrentKnown(currentBody)) return new CompanionMobilityFrame { Status = "等待地形就绪" };
                if (danger || needsAir)
                    return navigation.Escape(currentBody, state, localTarget, grounded, needsAir, lava, escape, escapeJump);
                if (!state.Observed.Ready)
                {
                    // The first native pass supplies equipment, contact and
                    // resource facts. Only a verified ordinary step may use
                    // the current geometry before those facts exist.
                    int move = Math.Abs(goal.Position.X - currentBody.Center.X) > 56 ? Math.Sign(goal.Position.X - currentBody.Center.X) : 0;
                    if (grounded && move != 0 && QuickWalk(currentBody, move, out _))
                        return new CompanionMobilityFrame { Move = move, Status = "移动中" };
                    return new CompanionMobilityFrame { Status = "停留" };
                }
                if (!grounded && navigation.TryFallRescue(currentBody, state, out var rescue)) return rescue;
                if (combat && grounded && !Actions.HasStep)
                {
                    if (combatJump && navigation.TryCombatJump(currentBody, state, localTarget, out var jump)) return jump;
                    int move = Math.Sign(localTarget.X - currentBody.Center.X);
                    if (Math.Abs(localTarget.X - currentBody.Center.X) > 10 && QuickWalk(currentBody, move, out _))
                        return new CompanionMobilityFrame { Move = move, Status = "移动中" };
                }
                return navigation.FollowRoute(currentBody, state, goal.Position, goal.Velocity, allowPlanning, grounded, 32, 56, goal.Trail);
            }
        }
    }
}
