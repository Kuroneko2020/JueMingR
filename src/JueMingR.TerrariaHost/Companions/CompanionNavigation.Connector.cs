using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private readonly struct ProofHandle : IEquatable<ProofHandle>
        {
            internal readonly int Slot;
            internal readonly long Version;
            internal bool IsSet => Version != 0;

            internal ProofHandle(int slot, long version) { Slot = slot; Version = version; }
            public bool Equals(ProofHandle other) => Slot == other.Slot && Version == other.Version;
        }

        private readonly struct RouteCandidate
        {
            internal readonly ProofHandle End;
            internal readonly Vector2 Goal;
            internal readonly RouteResultKind Kind;
            internal readonly long GoalEpoch;

            internal RouteCandidate(ProofHandle end, Vector2 goal, RouteResultKind kind, long goalEpoch)
            { End = end; Goal = goal; Kind = kind; GoalEpoch = goalEpoch; }
        }

        private readonly struct RoutePlan
        {
            internal readonly List<RouteStep> Steps;
            internal readonly Vector2 Goal;
            internal readonly long GoalEpoch, PlanId;
            internal readonly bool Outlet;

            internal RoutePlan(List<RouteStep> steps, RouteCandidate candidate, long id)
            { Steps = steps; Goal = candidate.Goal; GoalEpoch = candidate.GoalEpoch; PlanId = id; Outlet = candidate.Kind >= RouteResultKind.Corridor && candidate.Kind != RouteResultKind.Goal; }
        }

        private enum ConnectionResult { Wait, Ready, Align, Reanchor, InvalidProof, NoConnection }

        // The connector owns only this transaction. It reads versioned
        // proofs and returns values; a reclaimed slot cannot redirect it,
        // and it never reads or advances the executor's current action.
        private sealed class RouteConnector
        {
            private const int ConnectionWork = 48;
            private readonly List<ConnectionPose> path = new List<ConnectionPose>();
            private ProofHandle cursor;
            private int entrance;
            private long passageCursor;
            private Vector2 passageEnd;
            private float passageSpeed;
            private RouteMotion passageMotion;
            private bool readingPassages, pathReady;
            internal bool UsedPassages { get; private set; }
            internal RouteCandidate Candidate { get; private set; }
            internal bool Active => Candidate.End.IsSet;

            private struct ConnectionPose
            {
                internal Vector2 Position;
                internal RouteMotion Motion;
                internal RouteStep Step;
            }

            internal void Start(RouteCandidate candidate)
            {
                Candidate = candidate;
                cursor = candidate.End;
                entrance = 1;
                passageCursor = 0;
                readingPassages = true;
                UsedPassages = pathReady = false;
                path.Clear();
            }

            internal void Retire()
            { Candidate = default; cursor = default; entrance = 1; path.Clear(); }

            internal ConnectionResult Advance(RouteSearch planner, Player body, CompanionMobilitySnapshot mobility,
                RouteMotion motion, ref int remaining, out List<RouteStep> prefix, out bool hasSuffix)
            {
                prefix = null;
                hasSuffix = false;
                if (!planner.TryRead(Candidate.End, out var terminal)) return ConnectionResult.InvalidProof;
                if (!pathReady && readingPassages)
                {
                    if (path.Count == 0)
                    {
                        passageCursor = terminal.Passage;
                        passageEnd = terminal.Position;
                        passageSpeed = terminal.VelocityX;
                        passageMotion = terminal.Motion;
                    }
                    while (passageCursor != 0 && remaining >= 5)
                    {
                        remaining--;
                        if (!planner.Passages.TryEnd(passageCursor, passageEnd, passageMotion, passageSpeed, out var passage))
                        {
                            readingPassages = false;
                            path.Clear();
                            break;
                        }
                        for (int w = passage.Count - 1; w >= 0; w--)
                        {
                            remaining--;
                            var proof = planner.Passages.ReadWitness(passage, w);
                            path.Add(new ConnectionPose { Position = proof.Step.To, Motion = proof.End, Step = proof.Step });
                        }
                        passageEnd = passage.Entrance;
                        passageSpeed = passage.EntrySpeed;
                        passageMotion = passage.Start;
                        passageCursor = passage.From;
                    }
                    if (readingPassages && passageCursor != 0) return ConnectionResult.Wait;
                    if (readingPassages && path.Count > 0)
                    {
                        path.Add(new ConnectionPose { Position = passageEnd, Motion = passageMotion });
                        UsedPassages = pathReady = true;
                    }
                    else readingPassages = false;
                }
                while (!pathReady && cursor.IsSet && remaining > 0)
                {
                    remaining--;
                    if (!planner.TryRead(cursor, out RouteNode node)) return ConnectionResult.InvalidProof;
                    path.Add(new ConnectionPose { Position = node.Position, Motion = node.Motion, Step = node.Step });
                    cursor = planner.Reference(node.Previous);
                }
                if (!pathReady && cursor.IsSet) return ConnectionResult.Wait;
                pathReady = true;

                // The chain is ordered from the goal back to its root. Use
                // the nearest executable suffix, not a return to the old root.
                using (Scratch.Save())
                while (entrance < path.Count && remaining >= ConnectionWork)
                {
                    remaining--;
                    int i = entrance++;
                    var node = path[i];
                    var next = path[i - 1];
                    Vector2 adjust = node.Position - body.position;
                    if (node.Motion != motion || Math.Abs(adjust.Y) > (motion == RouteMotion.Ground ? 16 : 8) ||
                        Math.Abs(adjust.X) > ConnectionWork) continue;
                    remaining -= ConnectionWork - 1;
                    var departure = next.Step;
                    if (departure.Kind == RouteKind.Jump && departure.HasEntryVelocity &&
                        departure.EntryVelocityX * departure.Direction > .8f &&
                        (Math.Abs(adjust.X) > 5 || !JumpEntryReady(body, mobility, departure) ||
                            !JumpLandingAtLiveEntry(body, mobility, departure))) continue;
                    int direction = Math.Sign(adjust.X);
                    Vector2 joined = body.position;
                    bool atDeparture = motion == RouteMotion.Ground && IsOnDescentApproach(body, mobility, departure);
                    bool align = !atDeparture && motion == RouteMotion.Ground && Math.Abs(adjust.X) > 5;
                    if (align)
                    {
                        if (!TrySupportedTravel(body, body.position, direction, Math.Abs(adjust.X), null, out joined, out _) ||
                            Math.Abs(joined.Y - node.Position.Y) > 4) continue;
                    }
                    else if (!atDeparture && (Math.Abs(adjust.X) > 6 || Math.Abs(adjust.Y) > 8 || !Swept(body, body.position, adjust, null))) continue;

                    prefix = new List<RouteStep>(14);
                    if (align) prefix.Add(new RouteStep { Kind = RouteKind.Walk, From = body.position, To = joined, Direction = direction });
                    int count = Math.Min(12, i);
                    if (count < i && (path[i - count - 1].Step.Kind == RouteKind.LedgeDrop || path[i - count - 1].Step.Kind == RouteKind.Jump) &&
                        path[i - count].Step.Kind == RouteKind.Walk) count++;
                    for (int step = i - 1; step >= i - count; step--)
                    {
                        var proven = path[step];
                        prefix.Add(proven.Step);
                        if (!proven.Step.ObserveAfter) continue;
                        hasSuffix = step > 0;
                        return align ? ConnectionResult.Align : ConnectionResult.Ready;
                    }
                    hasSuffix = i > count;
                    return align ? ConnectionResult.Align : ConnectionResult.Ready;
                }
                if (entrance < path.Count) return ConnectionResult.Wait;
                return Vector2.DistanceSquared(body.position, planner.Origin) > 4 || body.velocity.X != 0 ?
                    ConnectionResult.Reanchor : ConnectionResult.NoConnection;
            }
        }
    }
}
