using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private const int SliceLimit = 160;
        private const int LocalNodes = 768;
        private const int RouteCandidates = 41;
        private const int IsolationWork = 120, IsolationCells = 96;
        private static readonly int[] supportCandidates = { 39, 40, 35, 36, 5, 6, 7 };
        private static readonly int[] afterSupportCandidates =
            { 0, 1, 6, 7, 2, 3, 4, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26,
                27, 28, 29, 30, 31, 32, 33, 34, 37, 38 };
        private enum RouteMotion : byte { Ground, Air, Rope, Track, Liquid, Hooked }
        private enum RouteResultKind : byte { Progress, Corridor, Checkpoint, Continuation, Goal }
        private enum RouteAdmission : byte { Added, Dominated, StoragePressure, EquivalentPosePressure }
        private enum RouteKind : byte
        {
            Walk, Jump, Drop, Descend, Ascend, AirAcross, Rope, RopeExit, Track, Swim,
            Hook, ReleaseHook, Mount, Dismount, Cart, Door, Rod, Pylon, Portal, FlipGravity, Teleporter, GlideJump, LedgeDrop, Hoik
        }

        private struct RouteStep
        {
            internal RouteKind Kind;
            internal Vector2 From, To, Operand;
            internal int Direction, Vertical, Duration, ItemSlot, MountTypePlusOne;
            internal int DescentDamage, DescentSteer;
            internal float DescentAimX, EntryVelocityX, ExitVelocityX;
            internal bool HasEntryVelocity, HasExitVelocity;
            internal float Cost;
            internal bool EmergencyHand, DescentLands, DescentEntersLiquid, DescentWaterSurface, ObserveAfter, Retreat;
            internal Rectangle DescentArea, Reads;
            internal JumpInputs Launch;
            internal DoorPassageProof DoorProof;
            internal HoikPassage HoikProof;
        }

        private readonly struct JumpInputs : IEquatable<JumpInputs>
        {
            private readonly float gravity, direction, speed, acceleration, slowdown, runSpeed, fallSpeed;
            private readonly int hold, size;
            private readonly bool wet;

            internal JumpInputs(Player body, CompanionMobilitySnapshot observed)
            {
                var run = MovementRun(body, observed, airborne: false);
                gravity = body.gravity; direction = body.gravDir; speed = observed.JumpSpeed; hold = observed.JumpHeight;
                acceleration = run.Acceleration; slowdown = run.Slowdown; runSpeed = Math.Max(run.Maximum, run.Speed); fallSpeed = body.maxFallSpeed;
                size = body.width | body.height << 16;
                wet = observed.Wet;
            }

            internal bool Captured => size != 0;

            public bool Equals(JumpInputs other) => gravity == other.gravity && direction == other.direction && speed == other.speed &&
                acceleration == other.acceleration && slowdown == other.slowdown && runSpeed == other.runSpeed &&
                fallSpeed == other.fallSpeed && hold == other.hold && size == other.size && wet == other.wet;
        }

        private struct RouteNode
        {
            internal long Version, Passage;
            internal bool Outlet;
            internal Vector2 Position;
            internal RouteMotion Motion;
            internal int Fuel, GlideFuel, Previous, Root, PriorityRevision, Alternatives, FirstChild, Sibling, SiblingBefore, Observation;
            internal int HeapPosition;
            internal bool Observe, Pending, InAdvancedHeap, Invalid, Vacant, Dominated, LocalEdgesDone, WaitingForCapacity;
            internal byte StableDirection, ProbedLedge;
            internal Rectangle EdgeReads, ExpandedReads, PathReads;
            internal RouteStep Step;
            internal float Cost, Score, VelocityX, SupportedRun;
        }

        private readonly struct RouteState : IEquatable<RouteState>
        {
            private readonly Vector2 position;
            private readonly int velocity, root, observation;
            private readonly RouteMotion motion;

            internal RouteState(RouteNode node)
            {
                position = node.Position;
                velocity = VelocityClass(node.VelocityX);
                root = node.Previous < 0 ? -1 : node.Root;
                observation = node.Observation;
                motion = node.Motion;
            }

            // Position is the actual proved body pose. Even a small change
            // can move its feet across the edge of a half brick or platform.
            public bool Equals(RouteState other) => position == other.position && velocity == other.velocity &&
                root == other.root && motion == other.motion && observation == other.observation;

            public override bool Equals(object other) => other is RouteState state && Equals(state);
            public override int GetHashCode() => (((position.GetHashCode() * 397 ^ velocity) * 397 ^ root) * 397 ^ observation) * 397 ^ (int)motion;
        }

        private sealed class RouteSearch
        {
            internal readonly ReadSet Terrain = new ReadSet(retainUnknown: true);
            internal readonly List<RouteNode> Nodes = new List<RouteNode>();
            internal readonly PassageGraph Passages = new PassageGraph();
            private readonly List<int> heap = new List<int>();
            private readonly List<int> advanced = new List<int>();
            private readonly Dictionary<RouteState, int> alternatives = new Dictionary<RouteState, int>();
            private readonly Stack<int> vacant = new Stack<int>();
            private readonly Queue<int> invalidating = new Queue<int>();
            private readonly Queue<ProofHandle> capacityWaiting = new Queue<ProofHandle>();
            private int reclaimCursor, reclaimRemaining, capacityExamined, liveRoot = -1, repairChild = -1;
            private long nextVersion;
            internal int LiveCount, DominatedCount;
            private int supportSinceAdvanced;
            private bool waitingForRoot;
            private int protectedEnd = -1, protectedJoin = -1;
            private Vector2 reservedPosition, reservedVelocity;
            private RouteMotion reservedMotion;
            private int reservedRisk, reservedLife;
            internal Vector2 Origin, Goal, ExpansionGoal;
            internal RouteMotion OriginMotion;
            internal int OriginFuel, OriginGlideFuel;
            private float gravityDirection;
            internal bool RodAvailable, AttachedAtRoot;
            internal JumpInputs OriginJumpInputs;
            internal int RootVelocityBand, RootMinimumLife, RootRiskKey;
            internal bool RootVelocityRead;
            private Vector2 priorityGoal, scoreOrigin;
            private int priorityRevision, nextRedirect;
            private int resumedRoot = -1;
            internal int Expanding, Candidate, Closest, TerrainCursor, Window, Capabilities;
            internal bool AscentFirst, DescentFirst, AirFirst, CheckTerminal, CapacityLimited, SupportsFirst;
            internal DescentProbe Descent;
            internal Rectangle DescentReads;
            private Rectangle repairing, queuedRepair;
            private int repairCursor, passageRepair;
            internal uint RetryAt;
            internal bool Initialized, Active, Exhausted, NeedsLiveRoot, WindowLimited, AwaitsNativeChange, PrioritizeDescentWake;
            internal bool IsolationTried;
            internal Rectangle IsolatedInterior;
            internal ProofHandle CapacityOutlet;

            internal void Protect(ProofHandle pending, ProofHandle attached)
            {
                protectedEnd = TryRead(pending, out _) ? pending.Slot : -1;
                protectedJoin = TryRead(attached, out _) ? attached.Slot : -1;
            }

            internal ProofHandle Reference(int index) => index >= 0 && index < Nodes.Count && !Nodes[index].Vacant ?
                new ProofHandle(index, Nodes[index].Version) : default;

            internal bool TryRead(ProofHandle reference, out RouteNode node)
            {
                node = reference.IsSet && reference.Slot >= 0 && reference.Slot < Nodes.Count ? Nodes[reference.Slot] : default;
                return reference.IsSet && node.Version == reference.Version && !node.Vacant && !node.Invalid;
            }

            internal void Begin(Vector2 origin, Vector2 goal, RouteMotion motion, int fuel, int glideFuel,
                float velocity, float gravity, int window, int capabilities)
                => Start(origin, goal, motion, fuel, glideFuel, velocity, gravity, window, capabilities, keepPassages: false);

            internal void Start(Vector2 origin, Vector2 goal, RouteMotion motion, int fuel, int glideFuel,
                float velocity, float gravity, int window, int capabilities, bool keepPassages)
            {
                Nodes.Clear(); heap.Clear(); advanced.Clear(); alternatives.Clear(); vacant.Clear(); invalidating.Clear(); capacityWaiting.Clear();
                if (!keepPassages) { Terrain.Clear(); Passages.Clear(); }
                liveRoot = repairChild = -1;
                protectedEnd = protectedJoin = -1;
                CapacityOutlet = default;
                LiveCount = DominatedCount = reclaimCursor = reclaimRemaining = supportSinceAdvanced = capacityExamined = 0;
                Origin = scoreOrigin = origin; Goal = priorityGoal = goal; Window = window;
                gravityDirection = gravity;
                Capabilities = capabilities;
                Expanding = Closest = -1;
                resumedRoot = -1;
                ResetDescent();
                Candidate = TerrainCursor = nextRedirect = repairCursor = passageRepair = 0;
                repairing = queuedRepair = Rectangle.Empty;
                Initialized = Active = true; Exhausted = NeedsLiveRoot = false;
                WindowLimited = AwaitsNativeChange = CapacityLimited = false;
                IsolationTried = false;
                IsolatedInterior = Rectangle.Empty;
                PrioritizeDescentWake = false;
                waitingForRoot = false;
                RootVelocityRead = false;
                RootMinimumLife = 0;
                Join(origin, motion, fuel, glideFuel, velocity);
            }

            internal void ReopenPhysicalRoot(Vector2 origin, RouteMotion motion, int fuel, int glideFuel, float velocity, int velocityBand, bool prioritize)
            {
                waitingForRoot = false;
                if (Expanding >= 0 && (prioritize || Nodes[Expanding].Previous < 0))
                {
                    if (Nodes[Expanding].Previous >= 0) Enqueue(Expanding);
                    Expanding = -1;
                    ResetDescent();
                }
                RootVelocityBand = velocityBand;
                RootMinimumLife = 0;
                RootVelocityRead = false;
                int live = Join(origin, motion, fuel, glideFuel, velocity);
                if (prioritize && live >= 0)
                {
                    Reconsider(live);
                    resumedRoot = live;
                }
            }

            private int Join(Vector2 origin, RouteMotion motion, int fuel, int glideFuel, float velocity)
            {
                Origin = origin;
                OriginMotion = motion;
                OriginFuel = fuel; OriginGlideFuel = glideFuel;
                var candidate = new RouteNode
                {
                    Position = origin, Motion = motion, Fuel = fuel, GlideFuel = glideFuel,
                    VelocityX = velocity, Previous = -1, Root = -1, Observation = -1
                };
                if (Add(candidate, out int index) == RouteAdmission.Added) return liveRoot = index;
                if (index >= 0)
                {
                    liveRoot = index;
                    Enqueue(index);
                }
                return index;
            }

            internal void Retarget(Vector2 goal)
            {
                if (Vector2.DistanceSquared(Goal, goal) >= 48 * 48) capacityExamined = 0;
                Goal = goal;
                if (nextRedirect > 0) nextRedirect--;
                if (nextRedirect != 0 || Vector2.DistanceSquared(priorityGoal, goal) < 40 * 40) return;
                // Latest acceptance is immediate. Bounded lazy reprioritizing
                // lets a moving target redirect the queue without a heap rebuild.
                priorityGoal = goal;
                priorityRevision++;
                nextRedirect = 8;
                Closest = -1;
            }

            internal RouteAdmission Add(RouteNode node, out int index)
            {
                index = -1;
                bool observation = node.Observe;
                if (node.Previous >= 0)
                {
                    var parent = Nodes[node.Previous];
                    node.Observation = parent.Observation;
                    node.PathReads = Cover(parent.PathReads, node.EdgeReads);
                    if (node.Step.HasExitVelocity) node.VelocityX = node.Step.ExitVelocityX;
                    if (node.Step.Kind == RouteKind.Walk || node.Step.Kind == RouteKind.Hoik)
                        node.SupportedRun = Math.Abs(node.Position.X - parent.Position.X) +
                            (node.Step.Direction == parent.Step.Direction ? parent.SupportedRun : 0);
                }
                // Before native confirmation neither landing resources nor a
                // door/size transition can dominate an unconditional state.
                // Same exact predicted boundary may retain a better witness.
                var key = new RouteState(node);
                int head = alternatives.TryGetValue(key, out int first) ? first : -1;
                int distinct = 0, replace = -1;
                for (int old = head; old >= 0; old = Nodes[old].Alternatives)
                {
                    var other = Nodes[old];
                    if (other.Observe != observation || observation && !SameObservation(other.Step, node.Step)) continue;
                    if (Dominates(other, node))
                    {
                        index = old;
                        DominatedCount++;
                        return RouteAdmission.Dominated;
                    }
                    if (!Dominates(node, other)) distinct++;
                    else if (replace < 0 && old != node.Previous && ReclaimableLeaf(other, old)) replace = old;
                }
                // Two finite reservoirs have only a small useful Pareto
                // set in a native-equivalent pose. The frontier remains open
                // when either bounded storage rejects an additional state.
                if (LiveCount >= LocalNodes && replace >= 0 && distinct < 8)
                {
                    ReleaseLeaf(replace);
                    head = alternatives.TryGetValue(key, out first) ? first : -1;
                }
                if (LiveCount >= LocalNodes || distinct >= 8)
                {
                    CapacityLimited = true;
                    return distinct >= 8 ? RouteAdmission.EquivalentPosePressure : RouteAdmission.StoragePressure;
                }
                index = vacant.Count != 0 ? vacant.Pop() : Nodes.Count;
                node.Version = ++nextVersion;
                if (node.Previous >= 0)
                    node.Passage = Passages.Remember(Nodes[node.Previous], node, out node.Outlet);
                if (node.Previous < 0) node.Root = index;
                if (observation) node.Observation = index;
                // A physical root uses the same dictionary entry as before
                // indexing; observed nodes are indexed by the preceding
                // confirmation until descendants inherit the new boundary.
                node.Alternatives = head;
                node.FirstChild = node.Sibling = node.SiblingBefore = -1;
                if (index == Nodes.Count) Nodes.Add(node);
                else Nodes[index] = node;
                alternatives[key] = index;
                if (node.Previous >= 0)
                {
                    var parent = Nodes[node.Previous];
                    node.Sibling = parent.FirstChild;
                    if (node.Sibling >= 0)
                    {
                        var following = Nodes[node.Sibling];
                        following.SiblingBefore = index;
                        Nodes[node.Sibling] = following;
                    }
                    Nodes[index] = node;
                    parent.FirstChild = index;
                    Nodes[node.Previous] = parent;
                }
                LiveCount++;
                for (int old = head; old >= 0;)
                {
                    var other = Nodes[old];
                    int peer = other.Alternatives;
                    if (other.Observe == observation && (!observation || SameObservation(other.Step, node.Step)) &&
                        Dominates(node, other))
                    {
                        Unindex(old);
                        other.Dominated = true;
                        Nodes[old] = other;
                        DominatedCount++;
                        reclaimRemaining = Nodes.Count;
                    }
                    old = peer;
                }
                Enqueue(index);
                return RouteAdmission.Added;
            }

            private static bool SameObservation(RouteStep left, RouteStep right) =>
                left.Kind == right.Kind && left.Operand == right.Operand && left.Direction == right.Direction &&
                left.Vertical == right.Vertical && left.ItemSlot == right.ItemSlot && left.MountTypePlusOne == right.MountTypePlusOne &&
                left.DescentDamage == right.DescentDamage &&
                left.DescentSteer == right.DescentSteer && left.DescentLands == right.DescentLands &&
                left.DescentEntersLiquid == right.DescentEntersLiquid && left.DescentWaterSurface == right.DescentWaterSurface;

            private static bool Dominates(RouteNode left, RouteNode right) =>
                left.Cost <= right.Cost && left.Fuel >= right.Fuel && left.GlideFuel >= right.GlideFuel &&
                Math.Abs(left.VelocityX - right.VelocityX) < .05f &&
                (left.PathReads.Width == 0 || right.PathReads.Contains(left.PathReads));

            private RouteState IndexedState(RouteNode node)
            {
                if (node.Observe && node.Previous >= 0) node.Observation = Nodes[node.Previous].Observation;
                return new RouteState(node);
            }

            private void Unindex(int index)
            {
                var key = IndexedState(Nodes[index]);
                if (!alternatives.TryGetValue(key, out int current)) return;
                int previous = -1;
                while (current >= 0 && current != index)
                {
                    previous = current;
                    current = Nodes[current].Alternatives;
                }
                if (current < 0) return;
                int next = Nodes[index].Alternatives;
                if (previous < 0)
                {
                    if (next < 0) alternatives.Remove(key);
                    else alternatives[key] = next;
                }
                else
                {
                    var old = Nodes[previous];
                    old.Alternatives = next;
                    Nodes[previous] = old;
                }
            }

            internal void Reclaim(ref int work, int pending, int proposed, int rejected)
            {
                bool pressure = LiveCount >= LocalNodes && (capacityWaiting.Count != 0 || waitingForRoot);
                // Reserve the final unit for the bounded replacement or
                // retry. A 24-unit slice must not inspect 24 leaves and then
                // leave itself unable to make the promised exchange.
                int checks = Math.Min(Math.Max(0, work - (pressure ? 1 : 0)),
                    Math.Min(24, pressure ? Math.Max(0, Nodes.Count - capacityExamined) : reclaimRemaining));
                int deferredLeaf = -1;
                for (int inspected = 0; inspected < checks; inspected++)
                {
                    if (reclaimCursor >= Nodes.Count) reclaimCursor = 0;
                    int index = reclaimCursor++;
                    var node = Nodes[index];
                    if (!ReclaimableLeaf(node, index) || index == pending || index == proposed || index == rejected) continue;
                    if (!node.Pending && !node.WaitingForCapacity && (node.Invalid || node.Dominated || pressure))
                        ReleaseLeaf(index);
                    else if (pressure && (deferredLeaf < 0 || node.Score > Nodes[deferredLeaf].Score)) deferredLeaf = index;
                }
                work -= checks;
                reclaimRemaining = Math.Max(0, reclaimRemaining - checks);
                capacityExamined = pressure && LiveCount >= LocalNodes ? capacityExamined + checks : 0;
                // If all inspected leaves are active, exchange a less useful
                // leaf for the pending candidate. Its retained parent can
                // re-expand the discarded edge. A sole pinned chain uses a
                // witnessed checkpoint instead of overwriting an ancestor.
                if (pressure && LiveCount >= LocalNodes && deferredLeaf >= 0 && work > 0)
                {
                    work--;
                    int parent = Nodes[deferredLeaf].Previous;
                    ReleaseLeaf(deferredLeaf);
                    capacityExamined = 0;
                    if (parent >= 0) Reconsider(parent);
                }
                while (work > 0 && LiveCount < LocalNodes && capacityWaiting.Count > 0)
                {
                    work--;
                    var retry = capacityWaiting.Dequeue();
                    if (!TryRead(retry, out var node)) continue;
                    node.WaitingForCapacity = false;
                    Nodes[retry.Slot] = node;
                    Reconsider(retry.Slot);
                    break;
                }
            }

            private bool ReclaimableLeaf(RouteNode node, int index) => !node.Vacant && node.FirstChild < 0 &&
                index != Expanding && index != Closest && index != liveRoot && index != resumedRoot &&
                index != protectedEnd && index != protectedJoin;

            private void ReleaseLeaf(int index)
            {
                var node = Nodes[index];
                if (node.Pending) RemoveFromHeap(index);
                Unindex(index);
                if (node.Previous >= 0)
                {
                    if (node.SiblingBefore >= 0)
                    {
                        var before = Nodes[node.SiblingBefore];
                        before.Sibling = node.Sibling;
                        Nodes[node.SiblingBefore] = before;
                    }
                    else
                    {
                        var parent = Nodes[node.Previous];
                        parent.FirstChild = node.Sibling;
                        Nodes[node.Previous] = parent;
                    }
                    if (node.Sibling >= 0)
                    {
                        var after = Nodes[node.Sibling];
                        after.SiblingBefore = node.SiblingBefore;
                        Nodes[node.Sibling] = after;
                    }
                }
                Nodes[index] = new RouteNode { Vacant = true };
                vacant.Push(index);
                LiveCount--;
            }
            internal void DeferCapacity(int index)
            {
                var node = Nodes[index];
                if (node.WaitingForCapacity || capacityWaiting.Count >= LocalNodes) return;
                node.WaitingForCapacity = true;
                Nodes[index] = node;
                capacityWaiting.Enqueue(Reference(index));
                if (node.Previous >= 0) CapacityOutlet = Reference(index);
            }

            internal void ReleaseUnreachableCheckpoint(ProofHandle checkpoint)
            {
                if (!CapacityOutlet.Equals(checkpoint)) return;
                // A checkpoint with no proved entry cannot pin the last live
                // leaf. Reclaim may now give its real parent another turn.
                CapacityOutlet = default;
                if (Closest == checkpoint.Slot) Closest = -1;
                if (Expanding == checkpoint.Slot) Expanding = -1;
                capacityExamined = 0;
            }

            internal bool SameRootReservation(Vector2 position, RouteMotion motion, Vector2 velocity, int risk, int life) =>
                waitingForRoot && reservedMotion == motion && Vector2.DistanceSquared(velocity, reservedVelocity) <= .0025f &&
                reservedRisk == risk && reservedLife == life && Vector2.DistanceSquared(position, reservedPosition) <= .01f;

            internal void ReserveLiveRoot(Vector2 position, RouteMotion motion, Vector2 velocity, int risk, int life)
            {
                if (SameRootReservation(position, motion, velocity, risk, life)) return;
                waitingForRoot = NeedsLiveRoot = true;
                reservedPosition = position; reservedMotion = motion;
                reservedVelocity = velocity; reservedRisk = risk; reservedLife = life;
                capacityExamined = 0;
            }

            internal void DeferAdvanced(int index)
            {
                var node = Nodes[index];
                node.LocalEdgesDone = true;
                Nodes[index] = node;
                Enqueue(index);
            }

            internal void ObserveSupportExpansion(int index, int candidate, byte side, bool found)
            {
                var node = Nodes[index];
                if (candidate == 35 || candidate == 36)
                {
                    if (found) node.StableDirection |= side;
                }
                else if (candidate == 6 || candidate == 7)
                    node.ProbedLedge |= side;
                Nodes[index] = node;
            }

            private void Enqueue(int index)
            {
                var node = Nodes[index];
                if (node.Pending || node.Invalid || node.Dominated) return;
                node.Pending = true;
                node.PriorityRevision = priorityRevision;
                node.Score = node.Cost + Estimate(node.Position, priorityGoal) - Estimate(scoreOrigin, priorityGoal);
                // A low/high owner must use another connected surface.
                // While exploring inexpensive supports, prefer extending a
                // continuous proved run to its end over redrawing short
                // reversals near the same corridor entrance. Action phases
                // and ordinary target acceptance still use the actual goal.
                if (!node.LocalEdgesDone && Math.Abs(node.Position.Y - priorityGoal.Y) > 42)
                    node.Score -= node.SupportedRun * 2.05f;
                if (node.Previous >= 0 && node.Motion == RouteMotion.Ground)
                {
                    float rootY = Nodes[node.Root].Position.Y;
                    float rise = (rootY - node.Position.Y) * gravityDirection;
                    if (rise > 38 && (priorityGoal.Y - rootY) * gravityDirection > 42)
                    {
                        // Climbing to a proved different floor opens a
                        // different corridor even when the owner is below it.
                        // Give that floor a bounded turn before asking the
                        // original dead-end level for another jump variant.
                        node.Score -= Math.Min(1024, rise * 4.25f);
                    }
                }
                Nodes[index] = node;
                var queue = node.LocalEdgesDone ? advanced : heap;
                int insertion = queue.Count;
                queue.Add(index);
                node.InAdvancedHeap = node.LocalEdgesDone;
                Nodes[index] = node;
                Place(queue, insertion, index);
                SiftUp(queue, insertion, index);
            }

            private void Place(List<int> queue, int position, int index)
            {
                queue[position] = index;
                var node = Nodes[index];
                node.HeapPosition = position;
                Nodes[index] = node;
            }

            private void SiftUp(List<int> queue, int position, int index)
            {
                float score = Nodes[index].Score;
                while (position > 0)
                {
                    int parent = (position - 1) / 2;
                    if (Nodes[queue[parent]].Score <= score) break;
                    Place(queue, position, queue[parent]);
                    position = parent;
                }
                Place(queue, position, index);
            }

            private void RemoveFromHeap(int index)
            {
                var removed = Nodes[index];
                var queue = removed.InAdvancedHeap ? advanced : heap;
                int hole = removed.HeapPosition;
                int last = queue.Count - 1, replacement = queue[last];
                queue.RemoveAt(last);
                removed.Pending = false;
                Nodes[index] = removed;
                if (hole == last) return;
                float score = Nodes[replacement].Score;
                if (hole > 0 && score < Nodes[queue[(hole - 1) / 2]].Score)
                {
                    SiftUp(queue, hole, replacement);
                    return;
                }
                while (hole * 2 + 1 < last)
                {
                    int child = hole * 2 + 1;
                    if (child + 1 < last && Nodes[queue[child + 1]].Score < Nodes[queue[child]].Score) child++;
                    if (score <= Nodes[queue[child]].Score) break;
                    Place(queue, hole, queue[child]);
                    hole = child;
                }
                Place(queue, hole, replacement);
            }

            internal int Next(ref int work)
            {
                if (resumedRoot >= 0)
                {
                    if (work == 0) return -2;
                    int current = resumedRoot;
                    resumedRoot = -1;
                    if (!Nodes[current].Invalid)
                    {
                        // A measured change to this body's failed departure
                        // deserves the next ordinary expansion, ahead of
                        // older remote estimates in the same frontier.
                        if (Nodes[current].Pending) RemoveFromHeap(current);
                        work--;
                        return current;
                    }
                }
                bool redirected = false;
                while (heap.Count != 0 || advanced.Count != 0)
                {
                    if (work == 0) return -2;
                    work--;
                    // Walkable corridors have a cheap graph pass. Give
                    // deferred jumps and other local mechanisms regular turns.
                    bool useAdvanced = advanced.Count != 0 && (heap.Count == 0 || supportSinceAdvanced >= 5);
                    var queue = useAdvanced ? advanced : heap;
                    int index = queue[0];
                    RemoveFromHeap(index);
                    var node = Nodes[index];
                    if (node.Invalid || node.Dominated) continue;
                    if (!redirected && node.PriorityRevision != priorityRevision)
                    { Enqueue(index); redirected = true; continue; }
                    supportSinceAdvanced = useAdvanced ? 0 : supportSinceAdvanced + 1;
                    capacityExamined = 0;
                    return index;
                }
                return -1;
            }

            internal void Dirty(Rectangle area)
            {
                if (area.Width == 0) return;
                capacityExamined = 0;
                if (DescentReads.Intersects(area)) ResetDescent();
                queuedRepair = Cover(queuedRepair, area);
            }

            internal void ResetDescent()
            { Descent = default; DescentReads = Rectangle.Empty; }

            internal Rectangle RememberDescent(Rectangle reads)
            { return DescentReads = Cover(DescentReads, reads); }

            internal int Repair(int budget)
            {
                if (repairing.Width == 0 && queuedRepair.Width != 0)
                {
                    repairing = queuedRepair; queuedRepair = Rectangle.Empty; repairCursor = passageRepair = 0;
                }
                int passageWork = 0;
                if (repairing.Width != 0 && passageRepair < PassageGraph.Entries)
                {
                    int work = Math.Min(12, budget);
                    int before = work;
                    Passages.RetireChanged(repairing, ref work, ref passageRepair);
                    passageWork = before - work;
                    budget -= passageWork;
                }
                int used = 0;
                while (used < budget)
                {
                    if (repairChild < 0 && invalidating.Count != 0)
                        repairChild = Nodes[invalidating.Dequeue()].FirstChild;
                    if (repairChild >= 0)
                    {
                        int child = repairChild;
                        repairChild = Nodes[child].Sibling;
                        Invalidate(child);
                    }
                    else if (repairing.Width != 0 && repairCursor < Nodes.Count)
                    {
                        int index = repairCursor++;
                        var node = Nodes[index];
                        if (!node.Invalid && !node.Vacant)
                        {
                            if (node.Previous >= 0 && node.EdgeReads.Intersects(repairing))
                            {
                                Invalidate(index);
                                Reconsider(node.Previous);
                            }
                            else if (node.ExpandedReads.Intersects(repairing)) Reconsider(index);
                        }
                    }
                    else break;
                    used++;
                }
                if (repairCursor >= Nodes.Count && passageRepair >= PassageGraph.Entries) repairing = Rectangle.Empty;
                return used + passageWork;
            }

            private void Invalidate(int index)
            {
                var node = Nodes[index];
                if (node.Invalid) return;
                Unindex(index);
                node.Invalid = true;
                Nodes[index] = node;
                reclaimRemaining = Nodes.Count;
                if (node.FirstChild >= 0) invalidating.Enqueue(index);
                if (Expanding == index) { Expanding = -1; ResetDescent(); }
                if (Closest == index) Closest = -1;
            }

            internal void RequestRoot()
            { if (liveRoot >= 0) Reconsider(liveRoot); }

            private void Reconsider(int index)
            {
                var node = Nodes[index];
                node.LocalEdgesDone = false;
                node.StableDirection = node.ProbedLedge = 0;
                Nodes[index] = node;
                if (Expanding == index) { Expanding = -1; ResetDescent(); }
                Enqueue(index);
            }

            internal bool RepairPending => repairing.Width != 0 || queuedRepair.Width != 0 ||
                repairChild >= 0 || invalidating.Count != 0;
            internal bool CanExpand => heap.Count != 0 || advanced.Count != 0 || resumedRoot >= 0 || TryRead(CapacityOutlet, out _) ||
                (capacityWaiting.Count != 0 || waitingForRoot) && (LiveCount < LocalNodes || capacityExamined < Nodes.Count);
            internal bool NeedsReclaim => reclaimRemaining != 0 || capacityWaiting.Count != 0 || waitingForRoot;

            internal void WaitForChange(uint now)
            {
                // An empty active queue can be a suspension with incomplete
                // reachability. Only real input/dependency changes reopen it.
                Active = CanExpand;
                Exhausted = !Active && !CapacityLimited && !WindowLimited;
                RetryAt = now + (uint)(Window < 1024 ? 12 : 150);
            }

            internal bool FullyKnownClosure => IsolatedInterior.Width != 0 ||
                !WindowLimited && !AwaitsNativeChange && !CapacityLimited && !Terrain.HasUnknown;

            private static Rectangle Cover(Rectangle left, Rectangle right) => left.Width == 0 ? right :
                right.Width == 0 ? left : Rectangle.Union(left, right);

            internal void RecordExpansion(int parent, Rectangle reads)
            {
                var node = Nodes[parent];
                node.ExpandedReads = Cover(node.ExpandedReads, reads);
                Nodes[parent] = node;
            }

        }

        private static float Estimate(Vector2 position, Vector2 target)
        { return Math.Abs(position.X - target.X) + Math.Abs(position.Y - target.Y) * 1.4f; }

        private static bool Reached(Vector2 position, Vector2 target, int distance)
        { return Math.Abs(position.X - target.X) <= distance && Math.Abs(position.Y - target.Y) <= 38; }

        private void SearchRoute(Player body, CompanionMobilitySnapshot mobility, Vector2 target, int stop, RouteMotion root, bool publish = true)
        {
            if (search == null) search = new RouteSearch();
            int capabilities = RouteCapabilityKey(mobility);
            int riskKey = DescentSafetyKey(body);
            bool sameGeometry = search.Capabilities == capabilities;
            var changed = search.Initialized && sameGeometry ?
                search.Terrain.Changes(ref search.TerrainCursor, search.Active ? 24 : 8) : Rectangle.Empty;
            bool recovered = recovery.Wake(body, changed, Main.GameUpdateCount) || recoveryPending;
            recoveryPending = false;
            // A landing creates new launch/ground states even when its exact
            // position is near the previous airborne planning root.
            bool moved = recovered || search.NeedsLiveRoot || root != search.OriginMotion ||
                root == RouteMotion.Hooked && mobility.GrappleAttached != search.AttachedAtRoot ||
                Vector2.DistanceSquared(body.position, search.Origin) > 42 * 42;
            int fuel = FlightBudget(mobility), glide = GlideBudget(mobility);
            var jumpInputs = new JumpInputs(body, mobility);
            int velocityBand = TraversalVelocityBand(body);
            bool descentWake = search.PrioritizeDescentWake && root == RouteMotion.Ground;
            bool rootChanged = sameGeometry && (descentWake || search.OriginMotion == root &&
                Vector2.DistanceSquared(search.Origin, body.position) < 42 * 42 &&
                (riskKey != search.RootRiskKey || search.RootVelocityRead && velocityBand != search.RootVelocityBand ||
                 search.RootMinimumLife > 0 && body.statLife >= search.RootMinimumLife));
            bool changedAbility = search.Initialized && !search.Active && (fuel > search.OriginFuel || glide > search.OriginGlideFuel ||
                mobility.CanUseRod && !search.RodAvailable || !search.OriginJumpInputs.Equals(jumpInputs));
            bool hadIsolation = search.IsolatedInterior.Width != 0;
            bool expiresIsolation = hadIsolation && (mobility.CanUseRod || CompanionMobilityGeometry.HasActivePortal ||
                CompanionMobilityGeometry.MayReachPylon(search.IsolatedInterior) ||
                !OutsideIsolatedInterior(body, target, stop, search.IsolatedInterior));
            bool refreshIsolation = moved || changedAbility || changed.Width != 0 || expiresIsolation;
            if (search.IsolationTried && refreshIsolation)
            {
                moved |= hadIsolation;
                search.IsolationTried = false;
                search.IsolatedInterior = Rectangle.Empty;
            }
            moved |= changedAbility;
            if (!recovered && !search.Active && changed.Width == 0 && !search.RepairPending && !changedAbility && !(hadIsolation && refreshIsolation) &&
                search.SameRootReservation(body.position, root, body.velocity, riskKey, body.statLife))
                moved = rootChanged = false;
            search.RodAvailable = mobility.CanUseRod;
            bool unchangedClosure = search.Initialized && !search.Active && sameGeometry && !moved && !rootChanged && changed.Width == 0 &&
                Vector2.DistanceSquared(target, search.Goal) < 48 * 48 &&
                (search.Exhausted && search.FullyKnownClosure || !RouteRecovery.Due(Main.GameUpdateCount, search.RetryAt));
            if (unchangedClosure && (!publish || !PlannerHasCandidate)) return;
            if (!unchangedClosure && compactAfterContact && search.Initialized && sameGeometry && changed.Width == 0 && !search.RepairPending)
            {
                compactAfterContact = false;
                RetirePlannedCandidate();
                search.Start(body.position, target, root, fuel, glide, body.velocity.X, body.gravDir, 256, capabilities, keepPassages: true);
                search.RootVelocityBand = velocityBand;
                search.RootRiskKey = riskKey;
                search.OriginJumpInputs = jumpInputs;
            }
            else if (!unchangedClosure && (!search.Initialized || !sameGeometry))
            {
                RetirePlannedCandidate();
                search.Begin(body.position, target, root, fuel, glide, body.velocity.X, body.gravDir, 256, capabilities);
                search.PrioritizeDescentWake = descentWake;
                search.RootVelocityBand = velocityBand;
                search.RootRiskKey = riskKey;
                search.OriginJumpInputs = jumpInputs;
#if DEBUG
                RouteSearchStarts++;
#endif
            }
            else if (!unchangedClosure)
            {
                search.Dirty(changed);
                if (changed.Width != 0) connector.Retire();
                search.Retarget(target);
                // The actual body can establish an additional root while
                // earlier connections remain available to reconnect or repair.
                if ((moved || rootChanged) && search.LiveCount >= LocalNodes)
                    search.ReserveLiveRoot(body.position, root, body.velocity, riskKey, body.statLife);
                else if (rootChanged || recovered)
                {
                    search.ReopenPhysicalRoot(body.position, root, fuel, glide, body.velocity.X, velocityBand, prioritize: true);
                    search.OriginJumpInputs = jumpInputs;
                    search.RootRiskKey = riskKey;
                    search.NeedsLiveRoot = false;
                }
                else if (moved)
                {
                    // The current standing body has an immediate question.
                    // Give its new root one turn when no connected candidate
                    // exists; old prefixes keep their place during background
                    // work and when the connector can still use one.
                    search.ReopenPhysicalRoot(body.position, root, fuel, glide, body.velocity.X, velocityBand,
                        prioritize: publish && root == RouteMotion.Ground && !PlannerHasCandidate);
                    search.OriginJumpInputs = jumpInputs;
                    search.RootRiskKey = riskKey;
                    search.NeedsLiveRoot = false;
                }
                // A complete earlier calculation can be reused for a new
                // live goal, while unobserved native/terrain conditions
                // periodically request the current root without clearing it.
                if (!search.Active && !search.CanExpand && !moved && !rootChanged && changed.Width == 0)
                    search.RequestRoot();
                search.Active = true; search.Exhausted = false;
            }
            search.AttachedAtRoot = mobility.GrappleAttached;

            int remaining = SliceLimit - localPlanningWork;
            search.AwaitsNativeChange |= mobility.GrappleInFlight || mobility.HandBusy || mobility.RocketAvailable && !mobility.RocketReady ||
                postureChangedAt != 0 && unchecked(Main.GameUpdateCount - postureChangedAt) < 90;
            int repair = search.Repair(Math.Min(40, Math.Max(0, remaining)));
            remaining -= repair;
#if DEBUG
            RouteWork += repair;
#endif
            if (search.RepairPending) return;
            search.Protect(pending.End, connector.Candidate.End);
            if (publish && root == RouteMotion.Ground && search.LiveCount >= 24 && !search.IsolationTried &&
                remaining >= IsolationWork && Math.Abs(body.velocity.X) <= .2f && body.velocity.Y == 0 &&
                Math.Abs(body.position.Y - target.Y) < 30 && !mobility.CanUseRod && !mobility.Controlled &&
                !CompanionMobilityGeometry.HasActivePortal)
            {
                search.IsolationTried = true;
                ChargePublication(ref remaining, IsolationWork);
                search.Terrain.BeginArea();
                bool sealedComponent = TryProveIsolatedGround(body, target, stop, search.Terrain, out Rectangle interior);
                search.Terrain.EndArea();
                if (RetireEvictedTerrain().Width != 0) return;
                if (sealedComponent && !CompanionMobilityGeometry.MayReachPylon(interior))
                {
                    search.IsolatedInterior = interior;
                    search.Active = false;
                    search.Exhausted = true;
                    RetirePlannedCandidate();
                    return;
                }
            }
            if (search.NeedsReclaim)
            {
                int beforeReclaim = remaining;
                // Preserve the grant of an already resumed expensive edge.
                // Reclaim shares its remaining work and cannot starve it.
                int reclaim = search.Expanding >= 0 && !search.CapacityLimited ? Math.Max(0, remaining - 152) : remaining;
                int reclaimAvailable = reclaim;
                search.Reclaim(ref reclaim, pendingResult, publishing, -1);
                remaining -= reclaimAvailable - reclaim;
#if DEBUG
                RouteWork += beforeReclaim - remaining;
#endif
            }
            if (connector.Active && GoalRedirects(body.position, connector.Candidate.Goal, target)) connector.Retire();
            if (pending.End.IsSet && (!search.TryRead(pending.End, out _) || GoalRedirects(body.position, pending.Goal, target))) pending = default;
            if (!publish && search.TryRead(pending.End, out var end) && pending.Kind == RouteResultKind.Goal && Reached(end.Position, target, stop)) return;
            if (publish && connector.Active && ContinuePublication(body, mobility, root, ref remaining)) return;
            if (remaining <= 0 || publish && connector.Active) return;
            if (publish && pending.End.IsSet)
            {
                var result = pending;
                pending = default;
                if (Propose(body, mobility, root, result, ref remaining)) return;
            }
            if (search.LiveCount >= LocalNodes && search.TryRead(search.CapacityOutlet, out _) &&
                OfferResult(body, mobility, root, search.CapacityOutlet.Slot, target, RouteResultKind.Checkpoint, publish, ref remaining)) return;
            if (unchangedClosure) return;
            int candidates = 0;
            while (remaining > 0 && candidates < 24)
            {
                if (search.Expanding < 0)
                {
#if DEBUG
                    int before = remaining;
#endif
                    int nodeIndex = search.Next(ref remaining);
#if DEBUG
                    RouteWork += before - remaining;
#endif
                    if (nodeIndex == -2) return;
                    search.Expanding = nodeIndex;
                    if (search.Expanding < 0)
                    {
                        FinishSearch(body, mobility, root, search.Closest, target, publish, ref remaining);
                        return;
                    }
                    search.Candidate = 0;
                    search.ResetDescent();
                    search.ExpansionGoal = target;
                    var expanded = search.Nodes[search.Expanding];
                    bool wokenDescent = search.PrioritizeDescentWake && expanded.Previous < 0 &&
                        expanded.Motion == root && Vector2.DistanceSquared(expanded.Position, body.position) <= 16 * 16;
                    if (wokenDescent) search.PrioritizeDescentWake = false;
                    search.AirFirst = expanded.Motion == RouteMotion.Air || expanded.Motion == RouteMotion.Liquid;
                    search.AscentFirst = expanded.Motion == RouteMotion.Ground && expanded.Fuel > 0 &&
                        (target.Y - expanded.Position.Y) * body.gravDir < -40;
                    search.DescentFirst = expanded.Motion == RouteMotion.Ground &&
                        (wokenDescent || (target.Y - expanded.Position.Y) * body.gravDir > 40 ||
                         expanded.Previous < 0 && Vector2.DistanceSquared(expanded.Position, body.position) <= 16 * 16 &&
                         Wall(body, Math.Sign(target.X - body.position.X)));
                    if (search.DescentFirst) search.AscentFirst = false;
                    search.SupportsFirst = expanded.Motion == RouteMotion.Ground && !expanded.LocalEdgesDone && !wokenDescent &&
                        (target.Y - expanded.Position.Y) * body.gravDir > 42;
                    search.CheckTerminal = !expanded.LocalEdgesDone;
                }

                int parent = search.Expanding;
                RouteNode node = search.Nodes[parent];
                if (search.CheckTerminal)
                {
                    // Popping a node is cheaper than proving its supported
                    // arrival connection. If this slice cannot pay for that
                    // proof, keep the node for the next selected slice.
                    if (node.Previous >= 0 && Reached(node.Position, target, stop))
                    {
                        if (GoalProofWork(node.Position, target) > remaining) return;
                        search.CheckTerminal = false;
                        bool arrived = ReachedGoal(body, node, parent, target, stop, ref remaining);
                        if (RetireEvictedTerrain().Width != 0)
                        {
                            search.CheckTerminal = true;
                            return;
                        }
                        if (arrived && OfferResult(body, mobility, root, parent, target, RouteResultKind.Goal, publish, ref remaining)) return;
                    }
                    search.CheckTerminal = false;
                    if (node.Observe && (!CanPlanBeyondObservation(node) ||
                        OutsideSearchWindow(node.Position, search.Nodes[node.Root].Position, search.Window)))
                    {
                        search.AwaitsNativeChange = true;
                        search.Expanding = -1;
                        continue;
                    }
                }
                if (search.SupportsFirst && search.Candidate == supportCandidates.Length)
                {
                    search.DeferAdvanced(parent);
                    search.Expanding = -1;
                    continue;
                }
                int count = search.SupportsFirst ? supportCandidates.Length :
                    node.LocalEdgesDone ? afterSupportCandidates.Length : RouteCandidates;
                int candidate = search.SupportsFirst ? supportCandidates[search.Candidate] :
                    node.LocalEdgesDone ? afterSupportCandidates[search.Candidate] :
                    SearchCandidate(search.Candidate, search.AscentFirst, search.DescentFirst, search.AirFirst);
                bool nearRoot = node.Previous < 0 && node.Motion == root && Vector2.DistanceSquared(node.Position, body.position) <= 16 * 16;
                int facing = search.ExpansionGoal.X < node.Position.X ? -1 : 1;
                byte side = (byte)((candidate == 35 || candidate == 6 ? facing : -facing) < 0 ? 1 : 2);
                // A proved extended support lane already moves toward that
                // end. Probe its drop when a later node can actually approach
                // the edge; retain the head's other launch tools for its turn.
                if (candidate == 6 || candidate == 7)
                {
                    if (search.SupportsFirst && (node.StableDirection & side) != 0 ||
                        node.LocalEdgesDone && (node.ProbedLedge & side) != 0)
                    {
                        if (++search.Candidate == count && !search.SupportsFirst) search.Expanding = -1;
                        continue;
                    }
                }
                if (!CanConsider(node, mobility, candidate, nearRoot))
                {
                    if (++search.Candidate == count && !search.SupportsFirst) search.Expanding = -1;
                    continue;
                }
                if (candidate == 39 || candidate == 40)
                {
                    const int mouthWork = 6;
                    if (remaining < mouthWork) break;
                    ChargePublication(ref remaining, mouthWork);
                    search.Terrain.BeginArea();
                    bool mouth = HasHoikMouth(body, node.Position, candidate == 39 ? facing : -facing, search.Terrain);
                    search.RecordExpansion(parent, search.Terrain.EndArea());
                    if (RetireEvictedTerrain().Width != 0) return;
                    if (!mouth)
                    {
                        if (++search.Candidate == count && !search.SupportsFirst) search.Expanding = -1;
                        continue;
                    }
                }
                int charge = WorkForCandidate(node.Motion, candidate);
                if (charge > remaining) break;
                remaining -= charge;
                candidates++;
#if DEBUG
                RouteCandidateChecks++;
                RouteWork += charge;
#endif
                search.Terrain.BeginArea();
                bool observe = false, pending = false;
                int requiredLife = 0;
                if (!TryPassageEdge(body, mobility, node, search.ExpansionGoal, candidate, out RouteNode next, out bool found))
                    found = TryEdge(body, mobility, node, search.ExpansionGoal, candidate, out next, out observe, out pending, out requiredLife);
                if (nearRoot && (candidate >= 2 && candidate <= 7 || candidate >= 37 && candidate <= 38) && node.Motion == RouteMotion.Ground)
                {
                    search.RootVelocityRead = true;
                    if (requiredLife > body.statLife && (search.RootMinimumLife == 0 || requiredLife < search.RootMinimumLife))
                        search.RootMinimumLife = requiredLife;
                }
                next.EdgeReads = search.Terrain.EndArea();
                search.RecordExpansion(parent, next.EdgeReads);
                var expiredReads = RetireEvictedTerrain();
                if (expiredReads.Width != 0)
                {
                    if (found && next.EdgeReads.Intersects(expiredReads)) found = false;
                }
                if (candidate >= 5 && candidate <= 7 || candidate >= 37 && candidate <= 38)
                {
                    next.EdgeReads = search.RememberDescent(next.EdgeReads);
                    if (pending)
                    {
                        if (expiredReads.Width != 0) return;
                        continue;
                    }
                    search.ResetDescent();
                }
                if (search.SupportsFirst) search.ObserveSupportExpansion(parent, candidate, side, found);
                if (++search.Candidate == count && !search.SupportsFirst) search.Expanding = -1;
                if (!found) { if (expiredReads.Width != 0) return; continue; }
                next.Step.Reads = next.EdgeReads;
                if (next.Step.Kind == RouteKind.Drop || next.Step.Kind == RouteKind.LedgeDrop)
                    next.Step.DescentArea = next.EdgeReads;
                if (RecentlyRejected(body, next.Step)) { search.AwaitsNativeChange = true; continue; }
                if (!InPlayableBounds(body, next.Position)) continue;
                var origin = search.Nodes[node.Root].Position;
                if (OutsideSearchWindow(next.Position, origin, search.Window))
                {
                    // Widen around the same retained root and frontier. A
                    // new local window is not permission to repeat the old one.
                    while (search.Window < 1024 && OutsideSearchWindow(next.Position, origin, search.Window))
                        search.Window *= 2;
                    search.WindowLimited |= OutsideSearchWindow(next.Position, origin, search.Window);
                    // A proven single native transition can outspan local
                    // exploration. Keep its executable prefix, but do not
                    // expand from that speculative position outside the box.
                    if (!observe && OutsideSearchWindow(next.Position, origin, search.Window))
                    {
                        if (expiredReads.Width == 0 && node.Outlet &&
                            OfferResult(body, mobility, root, parent, target, RouteResultKind.Corridor, publish, ref remaining)) return;
                        continue;
                    }
                }
                next.Previous = parent;
                next.Root = node.Root;
                next.Cost = node.Cost + next.Step.Cost;
                RouteAdmission admitted = search.Add(next, out int index);
                if (admitted != RouteAdmission.Added)
                {
                    if (admitted == RouteAdmission.StoragePressure) search.DeferCapacity(parent);
                    if (admitted == RouteAdmission.StoragePressure || admitted == RouteAdmission.EquivalentPosePressure)
                    {
                        if (expiredReads.Width == 0 && node.Outlet &&
                            OfferResult(body, mobility, root, parent, target, RouteResultKind.Corridor, publish, ref remaining)) return;
                    }
                    if (expiredReads.Width != 0) return;
                    continue;
                }
                if (expiredReads.Width != 0) return;
                if (search.Nodes[index].Outlet && OfferIndependentOutlet(body, target, node, next) &&
                    OfferResult(body, mobility, root, index, target, RouteResultKind.Corridor, publish, ref remaining)) return;
                if (search.Closest < 0 || Estimate(next.Position, target) + 12 < Estimate(search.Nodes[search.Closest].Position, target)) search.Closest = index;
                if (next.Step.DescentLands && !next.Step.DescentEntersLiquid && node.SupportedRun > 0 &&
                    Math.Abs(origin.Y - target.Y) - Math.Abs(next.Position.Y - target.Y) >= 64 &&
                    Estimate(origin, target) - Estimate(next.Position, target) < 20)
                {
                    // The native landing proves an outlet from this support
                    // chain, even if using it first increases the direct
                    // distance. Its length is not a publication condition.
                    // An observed crossing still stops the published prefix.
                    if (OfferResult(body, mobility, root, index, target, RouteResultKind.Continuation, publish, ref remaining)) return;
                }
                if (next.Step.DescentLands && !next.Step.DescentEntersLiquid &&
                    Estimate(origin, target) - Estimate(next.Position, target) < 20 &&
                    OfferDescentContinuation(body, mobility, root, node, next, index, target, publish, ref remaining)) return;
                if (next.Step.Kind == RouteKind.GlideJump && Estimate(origin, target) - Estimate(next.Position, target) >= 96)
                {
                    // The far-bank landing is proved by this edge. Start the
                    // crossing without also requiring the owner's final path.
                    if (OfferResult(body, mobility, root, index, target, RouteResultKind.Progress, publish, ref remaining)) return;
                }
                if (!observe && next.Motion == RouteMotion.Ground &&
                    Math.Abs(origin.Y - target.Y) - Math.Abs(next.Position.Y - target.Y) >= 48 &&
                    Estimate(origin, target) - Estimate(next.Position, target) >= 32)
                {
                    // A proven landing on the required higher/lower terrain
                    // is usable now. Resume search from the real landing pose
                    // without making the body wait for the last remote segment.
                    if (OfferResult(body, mobility, root, index, target, RouteResultKind.Progress, publish, ref remaining)) return;
                }
                if (observe)
                {
                    // Tool activation or a change of body size must be observed
                    // before searching from the resulting real state. Queue the
                    // legal prefix once it is a real route alternative.
                    bool terminal = ReachedGoal(body, next, index, target, stop, ref remaining);
                    if (RetireEvictedTerrain().Width != 0) return;
                    if (terminal || search.Closest == index && next.Step.Kind != RouteKind.Mount && next.Step.Kind != RouteKind.Dismount)
                    {
                        if (OfferResult(body, mobility, root, index, target, terminal ? RouteResultKind.Goal : RouteResultKind.Progress,
                            publish, ref remaining)) return;
                    }
                }
            }
        }

        private Rectangle RetireEvictedTerrain()
        {
            var area = search.Terrain.TakeEvictedArea();
            if (area.Width == 0) return area;
            // Eviction is a loss of evidence rather than a tile change.
            // Both require reproof before a previous branch may win again.
            search.Dirty(area);
            recovery.Dirty(area);
            connector.Retire();
            return area;
        }

        private static bool TryProveIsolatedGround(Player body, Vector2 target, int stop, ReadSet terrain, out Rectangle interior)
        {
            const int span = 17;
            Point center = body.Center.ToTileCoordinates();
            Point origin = new Point(center.X - span / 2, center.Y - span / 2);
            var seen = new bool[span * span];
            var queue = new int[IsolationCells];
            int seed = span / 2 * (span + 1);
            seen[seed] = true;
            queue[0] = seed;
            int end = 1, reads = 0;
            interior = Rectangle.Empty;

            // Use an over-approximation of physical reach. Even point-sized
            // diagonal openings, half bricks and interactive tiles remain open.
            // Only full active colliders can establish a separating boundary.
            for (int i = 0; i < end; i++)
            {
                int ix = queue[i] % span, iy = queue[i] / span;
                int x = ix + origin.X, y = iy + origin.Y;
                if (!terrain.Record(x, y)) return false;
                reads++;
                var tile = Main.tile[x, y];
                if (tile.liquid != 0 && tile.shimmer() || tile.type == TileID.Teleporter && tile.nactive()) return false;
                // Native hoik collision can bypass a full tile beside a
                // slope. Its motion needs the original graph's precise sweep.
                if (tile.nactive() && tile.slope() != 0) return false;
                if (tile.nactive() && Main.tileSolid[tile.type] && !Main.tileSolidTop[tile.type] && !tile.halfBrick() &&
                    !TileID.Sets.CrackedBricks[tile.type] && tile.type != TileID.ClosedDoor &&
                    tile.type != TileID.TallGateClosed && tile.type != TileID.TrapdoorClosed) continue;
                if (ix == 0 || iy == 0 || ix == span - 1 || iy == span - 1) return false;
                var cell = new Rectangle(x, y, 1, 1);
                interior = interior.Width == 0 ? cell : Rectangle.Union(interior, cell);
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int next = queue[i] + dx + dy * span;
                    if (seen[next]) continue;
                    if (end == queue.Length) return false;
                    seen[next] = true;
                    queue[end++] = next;
                }
            }
            if (interior.Width == 0 || !OutsideIsolatedInterior(body, target, stop, interior)) return false;

            // A wire pad's original contact box reaches three tiles upward
            // and includes exact touching. A pad may therefore be outside
            // the empty component, including just past a supporting tile.
            for (int x = interior.Left - 1; x <= interior.Right; x++)
            for (int y = interior.Top - 1; y <= interior.Bottom + 3; y++)
            {
                int ix = x - origin.X, iy = y - origin.Y;
                if (ix < 0 || ix >= span || iy < 0 || iy >= span || !seen[iy * span + ix])
                {
                    if (++reads > IsolationCells || !terrain.Record(x, y)) return false;
                }
                if (Main.tile[x, y].nactive() && Main.tile[x, y].type == TileID.Teleporter) return false;
            }
            return true;
        }

        private static bool OutsideIsolatedInterior(Player body, Vector2 target, int stop, Rectangle interior)
        {
            var near = new Rectangle((int)Math.Floor(target.X - stop), (int)Math.Floor(target.Y - 38),
                stop * 2 + body.width + 2, 78 + body.height);
            var component = new Rectangle(interior.X * 16, interior.Y * 16, interior.Width * 16, interior.Height * 16);
            return !component.Intersects(near);
        }

        private static bool OfferIndependentOutlet(Player body, Vector2 target, RouteNode current, RouteNode next)
        {
            if (Vector2.DistanceSquared(body.position, next.Position) < 24 * 24 || Math.Abs(target.Y - body.position.Y) < 38) return false;
            // Continued support is valuable to the graph, but it does not
            // establish that this end of the same shelf is an exit. Require
            // a new contact/transition unless storage genuinely needs a
            // certified checkpoint or a connected lower lane is discovered.
            return current.Motion != next.Motion || Math.Abs(next.Position.Y - current.Position.Y) >= 12;
        }

        private bool ReachedGoal(Player body, RouteNode node, int index, Vector2 target, int stop, ref int remaining)
        {
            if (!Reached(node.Position, target, stop)) return false;
            int proof = GoalProofWork(node.Position, target);
            if (proof > remaining) return false;
            ChargePublication(ref remaining, proof);
            search.Terrain.BeginArea();
            bool arrived = InGoalRegion(body, node.Position, target, node.Motion, search.Terrain);
            search.RecordExpansion(index, search.Terrain.EndArea());
            return arrived;
        }

        private static int GoalProofWork(Vector2 position, Vector2 target) =>
            14 + 4 * (int)Math.Ceiling(Math.Abs(position.X - target.X) / 4);

        private static int TraversalVelocityBand(Player body) => VelocityClass(body.velocity.X);

        private static int VelocityClass(float horizontal)
        {
            float speed = Math.Abs(horizontal);
            return speed < .3f ? 0 : Math.Sign(horizontal) * (speed < 1.25f ? 1 : speed < 3.5f ? 2 : 3);
        }

        private bool OfferDescentContinuation(Player body, CompanionMobilitySnapshot mobility, RouteMotion root, RouteNode upper, RouteNode lower, int index,
            Vector2 goal, bool publish, ref int remaining)
        {
            const int walks = 80;
            int direction = Math.Sign(goal.X - upper.Position.X);
            if (root != RouteMotion.Ground || direction == 0 || remaining < walks || search.LiveCount >= LocalNodes ||
                Vector2.DistanceSquared(body.position, upper.Position) > 20 * 20) return false;

            // A descent that costs direct distance can still be the exit:
            // show that legal movement toward the owner is blocked above
            // and possible below, with a supported, reusable successor.
            ChargePublication(ref remaining, walks);
            using (Scratch.Save())
            {
                search.Terrain.BeginArea();
                bool blocked = !TrySupportedTravel(body, upper.Position, direction, 16, search.Terrain, out _, out bool wall) && wall;
                var upperRead = search.Terrain.EndArea();
                search.RecordExpansion(lower.Previous, upperRead);
                if (!blocked) return false;
                search.Terrain.BeginArea();
                float distance = Math.Min(48, Math.Abs(goal.X - lower.Position.X));
                Vector2 to = lower.Position;
                bool clear = distance >= 16 &&
                    TrySupportedTravel(body, lower.Position, direction, distance, search.Terrain, out to, out _);
                var lowerRead = search.Terrain.EndArea();
                search.RecordExpansion(index, lowerRead);
                if (RetireEvictedTerrain().Width != 0) return true;
                if (!clear) return false;
                if (lower.Step.DescentDamage != 0)
                    return OfferResult(body, mobility, root, index, goal, RouteResultKind.Continuation, publish, ref remaining);
                var next = new RouteNode
                {
                    Position = to, Motion = RouteMotion.Ground, Previous = index, Root = lower.Root,
                    Fuel = lower.Fuel, GlideFuel = lower.GlideFuel, Cost = lower.Cost + distance, EdgeReads = lowerRead,
                    Step = new RouteStep { Kind = RouteKind.Walk, From = lower.Position, To = to, Direction = direction }
                };
                if (search.Add(next, out int added) == RouteAdmission.Added) index = added;
                return OfferResult(body, mobility, root, index, goal, RouteResultKind.Continuation, publish, ref remaining);
            }
        }

        private static int SearchCandidate(int cursor, bool ascentFirst, bool descentFirst, bool airFirst)
        {
            if (cursor >= 35) return cursor;
            if (airFirst)
            {
                if (cursor < 4) return 8 + cursor;
                if (cursor < 6) return 29 + cursor;
                return cursor < 14 ? cursor - 6 : cursor - 2;
            }
            if (descentFirst) return cursor < 3 ? 5 + cursor : cursor < 8 ? cursor - 3 : cursor;
            if (!ascentFirst) return cursor;
            return cursor < 4 ? 8 + cursor : cursor < 12 ? cursor - 4 : cursor;
        }

        private static int RouteCapabilityKey(CompanionMobilitySnapshot mobility)
        {
            unchecked
            {
                int result = mobility.MountType;
                result = result * 31 + mobility.Width; result = result * 31 + mobility.Height;
                result = result * 31 + mobility.GravityDirection.GetHashCode();
                result = result * 31 + mobility.NarrowMountType;
                result = result * 31 + mobility.FlightMountType; result = result * 31 + mobility.WaterMountType;
                result = result * 31 + mobility.Wings; result = result * 31 + mobility.HookType;
                result = result * 31 + mobility.RocketBoots; result = result * 31 + mobility.Carpet.GetHashCode();
                return result;
            }
        }

        private static bool CanConsider(RouteNode node, CompanionMobilitySnapshot mobility, int candidate, bool nearRoot)
        {
            switch (candidate)
            {
                case 0: case 1: case 5: case 6: case 7: case 35: case 36: case 37: case 38: case 39: case 40: return node.Motion == RouteMotion.Ground;
                case 2: case 3: case 4: return node.Motion == RouteMotion.Ground || node.Motion == RouteMotion.Rope;
                case 9: case 33: case 34:
                    return node.Motion == RouteMotion.Air || node.Motion == RouteMotion.Liquid ||
                        nearRoot && node.Motion == RouteMotion.Ground && mobility.Sliding;
                case 8: case 10: case 11:
                    return node.Motion == RouteMotion.Liquid || node.Motion == RouteMotion.Air || node.Fuel > 0 ||
                        node.GlideFuel > 0 || mobility.UnlimitedWings || mobility.UnlimitedMountHover;
                case 14: case 15: return node.Motion == RouteMotion.Track || mobility.CanUseCart;
                case 16: case 17: return node.Motion == RouteMotion.Ground;
                case 18: case 19: case 27: return nearRoot && mobility.HookRange > 0;
                case 20: return nearRoot && (mobility.MountType >= 0 || mobility.DefaultMountType >= 0 || mobility.FlightMountType >= 0);
                case 21: return mobility.CanChangeGravity;
                case 22: case 23: case 26: return nearRoot;
                case 24: return nearRoot && mobility.CanUseRod;
                case 25: case 28: return nearRoot && node.Motion == RouteMotion.Hooked;
                case 29: case 30: return node.Motion == RouteMotion.Ground && node.GlideFuel > 0 && mobility.CarpetAvailable &&
                    !mobility.Wet && mobility.GravityDirection > 0 && mobility.MountType < 0 &&
                    mobility.ExtraJumps == 0 && mobility.WingTime == 0 && !mobility.RocketAvailable;
                case 31: case 32: return node.Motion == RouteMotion.Ground || node.Motion == RouteMotion.Air;
                default: return true;
            }
        }

        private void FinishSearch(Player body, CompanionMobilitySnapshot mobility, RouteMotion motion, int last, Vector2 target, bool publish, ref int remaining)
        {
            search.WaitForChange(Main.GameUpdateCount);
            if (last >= 0 && Estimate(body.position, target) - Estimate(search.Nodes[last].Position, target) >= 20)
                OfferResult(body, mobility, motion, last, target, RouteResultKind.Progress, publish, ref remaining);
        }

        private static bool CanPlanBeyondObservation(RouteNode node)
        {
            // A geometric descent or walkable door has a known conditional
            // pose, as does the supported native exit of a proved Hoik.
            // Publication still stops before these observed boundaries.
            return (node.Step.Kind == RouteKind.Drop || node.Step.Kind == RouteKind.LedgeDrop) && node.Step.DescentDamage == 0 ||
                node.Step.Kind == RouteKind.Door && node.Motion == RouteMotion.Ground && node.Step.Vertical == 0 ||
                node.Step.Kind == RouteKind.Hoik && node.Motion == RouteMotion.Ground;
        }

        private static bool OutsideSearchWindow(Vector2 position, Vector2 origin, int window) =>
            Math.Abs(position.X - origin.X) > window || Math.Abs(position.Y - origin.Y) > window;

        private static bool IsOnDescentApproach(Player body, CompanionMobilitySnapshot mobility, RouteStep step)
        {
            if (step.Kind != RouteKind.LedgeDrop || Math.Abs(body.position.Y - step.From.Y) > 4) return false;
            // The walking prefix has already been consumed when the live
            // body is ahead of its starting pose on this same approach.
            // DriveDescentStep still checks real support, braking and the
            // departure/landing from the actual launch pose.
            return (body.position.X - step.From.X) * step.Direction >= 0 &&
                (step.Operand.X - body.position.X) * step.Direction >= 0 &&
                CanJoinLedgeDeparture(body, mobility, step);
        }

        private void ChargePublication(ref int remaining, int cost)
        {
            remaining -= cost;
#if DEBUG
            RouteWork += cost;
#endif
        }

#if DEBUG
        internal int RouteSearchStarts { get; private set; }
        internal int RouteCandidateChecks { get; private set; }
        internal int RouteWork { get; private set; }
#endif
    }
}
