using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    // Blueprint-only invalidation, not a world-content cache. A watch belongs
    // to one Host source range; the Host owns its instance/placement identity.
    // Native hook callbacks register changed bounds/layers and visit the pure
    // shared dependency index; they do not run matcher/world-source reads.
    // The native store instrumentation retains original Tile field reads.
    internal sealed class BlueprintWorldChangeObserver : IDisposable
    {
        internal sealed class WatchState
        {
            internal BlueprintWorldChangeObserver Owner;
            internal bool Live = true;
            internal long Id, Version;
            internal BlueprintProjectionBounds Bounds, DirtyBounds;
            internal Func<BlueprintProjectionBounds, bool> IntersectsContent;
            internal Func<BlueprintProjectionBounds, ushort, bool> IntersectsLayers;
            internal ushort TargetLayers, DirtyLayers;
            internal int RouteCount;
            internal Action<BlueprintProjectionBounds, ushort, Action<int>> Route;
            internal Action<int> RouteSink;
            internal ulong[] RawRoutes, DirtyRoutes, PendingRoutes;
            internal Func<BlueprintProjectionBounds, ushort, bool> BuildInterest;
            internal Action<BlueprintProjectionBounds, ushort, Action<int>> BuildRoute;
            internal Action<int> BuildSink;
            internal ulong[] BuildDirtyRoutes;
            internal long[] BuildVersions;
            internal bool BuildDirty;
            internal Dictionary<int, FrameProof> Frames;
            internal int Pending, DoorCount;
            internal bool Dirty;
            private bool sawRoute;
            private bool sawBuildRoute;

            // Placement anchors are not final-match targets. Keep their
            // invalidation in this same physical watch, with separate bounded
            // bits/stamps; a support-only write must not revoke a complete
            // canonical witness or add materials for an unchanged body.
            internal void RecordBuildRoutes(BlueprintProjectionBounds changed, ushort layers)
            {
                sawBuildRoute = false;
                if (BuildRoute == null) MarkBuildRoute(0); else BuildRoute(changed, layers, BuildSink);
                if (!sawBuildRoute) MarkAllBuildRoutes();
            }
            internal void MarkBuildRoute(int ordinal)
            {
                sawBuildRoute = true;
                if (ordinal < 0 || ordinal >= RouteCount) { MarkAllBuildRoutes(); return; }
                BuildVersions[ordinal] = ++Owner.buildVersion;
                BuildDirtyRoutes[ordinal >> 6] |= 1UL << (ordinal & 63); BuildDirty = true;
            }
            internal void MarkAllBuildRoutes()
            {
                if (BuildVersions == null) return;
                sawBuildRoute = true;
                for (int ordinal = 0; ordinal < RouteCount; ordinal++) MarkBuildRoute(ordinal);
            }

            internal void RecordRoutes(BlueprintProjectionBounds changed, ushort layers)
            {
                sawRoute = false;
                if (Route == null) MarkRoute(0); else Route(changed, layers, RouteSink);
                // Fail closed if the immutable route index contradicts the
                // interest predicate. No native store may silently lose its
                // invalidation because a consumer emitted no/invalid ordinal.
                if (!sawRoute) MarkAllRoutes();
            }
            internal void MarkRoute(int ordinal)
            {
                sawRoute = true;
                if (ordinal < 0 || ordinal >= RouteCount) { MarkAllRoutes(); return; }
                ForgetFrame(ordinal);
                MarkRawRoute(ordinal);
                int word = ordinal >> 6; ulong bit = 1UL << (ordinal & 63);
                DirtyRoutes[word] |= bit;
            }
            internal void MarkRawRoute(int ordinal)
            {
                int word = ordinal >> 6; ulong bit = 1UL << (ordinal & 63);
                RawRoutes[word] |= bit; if (Pending != 0) PendingRoutes[word] |= bit;
            }
            internal void ForgetFrame(int ordinal)
            {
                FrameProof frame;
                if (Frames == null || !Frames.TryGetValue(ordinal, out frame)) return;
                frame.Invalid = true; Frames.Remove(ordinal);
                if (frame.Door != null && Live) { DoorCount--; Owner.rawSeedInterestCount--; }
            }
            internal void ForgetFrames()
            {
                if (Frames == null) return;
                foreach (FrameProof frame in Frames.Values) frame.Invalid = true;
                Frames.Clear();
                if (Live) Owner.rawSeedInterestCount -= DoorCount;
                DoorCount = 0;
            }
            internal void MarkAllRoutes()
            {
                sawRoute = true;
                ForgetFrames();
                for (int i = 0; i < DirtyRoutes.Length; i++)
                { RawRoutes[i] = DirtyRoutes[i] = ulong.MaxValue; if (Pending != 0) PendingRoutes[i] = ulong.MaxValue; }
            }
            internal void RepublishPendingRoutes()
            {
                for (int word = 0; word < DirtyRoutes.Length; word++)
                {
                    ulong bits = PendingRoutes[word]; RawRoutes[word] |= bits; int bit = 0;
                    while (bits != 0)
                    {
                        int ordinal = word * 64 + bit; FrameProof frame;
                        if ((bits & 1) != 0 && ordinal < RouteCount &&
                            (Frames == null || !Frames.TryGetValue(ordinal, out frame) || frame.Invalid)) DirtyRoutes[word] |= 1UL << bit;
                        bits >>= 1; bit++;
                    }
                }
                if (Pending == 0) Array.Clear(PendingRoutes, 0, PendingRoutes.Length);
            }
            internal void ClearDirty()
            { Dirty = false; DirtyLayers = 0; Array.Clear(RawRoutes, 0, RawRoutes.Length); Array.Clear(DirtyRoutes, 0, DirtyRoutes.Length); }
        }

        // Exactly one Host sample can be assembling a Door's nine raw
        // seeds. Its interest starts BEFORE the first native sample and lasts
        // through publication, so an ordinary/inactive wing's ignored drawing
        // frame cannot make that first immutable certificate stale. This lease
        // contains geometry/identity only; registration/disposal is not dirty.
        private sealed class DoorSeedLease : IDisposable
        {
            internal BlueprintWorldChangeObserver Owner;
            internal readonly WatchState Watch;
            internal readonly int Ordinal;
            internal readonly BlueprintProjectionBounds Bounds;
            internal DoorSeedLease(BlueprintWorldChangeObserver owner, WatchState watch, int ordinal, BlueprintProjectionBounds bounds)
            { Owner = owner; Watch = watch; Ordinal = ordinal; Bounds = bounds; }
            public void Dispose()
            {
                var current = Owner; if (current == null) return;
                lock (current.gate)
                {
                    if (Owner == null) return;
                    Owner = null;
                    if (ReferenceEquals(current.doorSeed, this)) current.doorSeed = null;
                    current.rawSeedInterestCount--;
                }
            }
        }

        // Published only after a complete game-thread Match. This retains a
        // finite immutable frame/door layout, never Tile/CaptureCell payloads.
        // Frames require complete phase coverage; a door keeps exactly nine
        // gated Tile-semantic scalar seeds and proves its whole final pose.
        // A different root or an uncertified/unknown change destroys the proof.
        internal sealed class FrameProof
        {
            internal readonly WatchState Watch;
            internal readonly int Ordinal;
            internal readonly BlueprintWorldObjectFrameWitness Layout;
            internal BlueprintWorldDoorWitness Door;
            internal readonly BlueprintWorldTileWriteState[] DoorMembers;
            internal readonly long[] Members;
            internal readonly bool[] Covered;
            internal long Phase;
            internal int Coverage;
            internal bool Invalid;
            internal MutationBatch Writer;
            internal FrameProof(WatchState watch, int ordinal, IBlueprintWorldObjectWriteWitness witness)
            {
                Watch = watch; Ordinal = ordinal; Layout = witness as BlueprintWorldObjectFrameWitness; Door = witness as BlueprintWorldDoorWitness;
                if (Layout != null)
                { Phase = Layout.InitialPhase; Members = new long[Layout.MemberCount]; Covered = new bool[Layout.MemberCount]; }
                else DoorMembers = new BlueprintWorldTileWriteState[Door.MemberCount];
            }
            internal void Begin(MutationBatch writer)
            {
                Writer = writer; Coverage = 0;
                if (Door != null) { for (int i = 0; i < DoorMembers.Length; i++) DoorMembers[i] = Door.InitialState(i); return; }
                Array.Clear(Covered, 0, Covered.Length); for (int i = 0; i < Members.Length; i++) Members[i] = Phase;
            }
            internal bool Apply(int x, int y, int kind, int oldValue, int newValue)
            {
                if (Door != null)
                {
                    if (Invalid) return false;
                    int doorColumn = x - Door.WorldX, doorRow = y - Door.WorldY;
                    if (doorColumn < 0 || doorRow < 0 || doorColumn >= Door.Width || doorRow >= Door.Height) return false;
                    int doorIndex = checked(doorRow * Door.Width + doorColumn), doorMember; BlueprintWorldTileWriteState next;
                    if (!Door.TryTransition(x, y, kind, oldValue, newValue, DoorMembers[doorIndex], out doorMember, out next) || doorMember != doorIndex) return false;
                    DoorMembers[doorIndex] = next; return true;
                }
                if (Invalid || kind != 6 && kind != 7) return false;
                int column = x - Layout.WorldX, row = y - Layout.WorldY;
                if (column < 0 || row < 0 || column >= Layout.Width || row >= Layout.Height) return false;
                int index = checked(row * Layout.Width + column), member; long phase;
                if (!Layout.TryTransition(x, y, kind == 6, oldValue, newValue, Members[index], out member, out phase) || member != index) return false;
                Members[index] = phase; if (!Covered[index]) { Covered[index] = true; Coverage++; }
                return true;
            }
            internal bool Complete()
            {
                if (Door != null)
                {
                    BlueprintWorldDoorWitness next;
                    if (Invalid || !Door.TryComplete(DoorMembers, out next) || next == null) return false;
                    Door = next; Writer = null; return true;
                }
                if (Invalid || Coverage != Members.Length) return false;
                long phase = Members[0]; for (int i = 1; i < Members.Length; i++) if (Members[i] != phase) return false;
                Phase = phase; Writer = null; return true;
            }
        }

        internal sealed class Mutation : IDisposable
        {
            private BlueprintWorldChangeObserver owner;
            private readonly WatchState[] affected;
            private readonly BlueprintProjectionBounds bounds;
            private readonly ushort layers;
            internal Mutation(BlueprintWorldChangeObserver owner, WatchState[] affected, BlueprintProjectionBounds bounds, ushort layers)
            { this.owner = owner; this.affected = affected; this.bounds = bounds; this.layers = layers; }
            public void Dispose()
            {
                var current = owner;
                if (current == null) return;
                lock (current.gate)
                {
                    if (owner == null) return;
                    owner = null;
                    for (int i = 0; i < affected.Length; i++)
                    {
                        WatchState state = affected[i];
                        state.Pending--;
                        state.RepublishPendingRoutes();
                        current.Invalidate(state, bounds, layers, route: false);
                    }
                }
            }
        }

        internal sealed class MutationBatch : IDisposable
        {
            private struct Impact
            {
                internal BlueprintProjectionBounds Bounds;
                internal ushort Layers;
            }
            private BlueprintWorldChangeObserver owner;
            private readonly Dictionary<WatchState, Impact> affected = new Dictionary<WatchState, Impact>();
            private readonly List<FrameProof> frames = new List<FrameProof>();
            private readonly Action<int> storeRoute;
            private WatchState storeWatch;
            private int storeX, storeY, storeKind, storeOld, storeNew;
            private bool sawStoreRoute, storeSeedOnly, storePacket, storeLeaseSeen;
            private BlueprintProjectionBounds storeBounds;
            private ushort storeLayers;
            internal MutationBatch(BlueprintWorldChangeObserver owner) { this.owner = owner; storeRoute = RecordStoreRoute; }
            internal void TouchStore(int x, int y, int kind, int oldValue, int newValue, ushort changedLayers, bool seedFrameOnly = false)
            {
                var current = owner; if (current == null) return;
                var bounds = new BlueprintProjectionBounds(x, y, 1, 1);
                lock (current.gate)
                {
                    PruneRetired(current);
                    foreach (WatchState state in current.watches.Values)
                    {
                        ushort buildLayers = BuildStoreLayers(kind, oldValue ^ newValue);
                        if (!seedFrameOnly && BuildRelevant(state, bounds, buildLayers)) state.RecordBuildRoutes(bounds, buildLayers);
                        bool seedLease = (changedLayers & 1) != 0 && current.SeedContains(state, x, y);
                        if (seedFrameOnly ? !state.Bounds.Contains(x, y) || state.DoorCount == 0 && !seedLease : !seedLease && !Relevant(state, bounds, changedLayers)) continue;
                        storeWatch = state; storeX = x; storeY = y; storeKind = kind; storeOld = oldValue; storeNew = newValue; sawStoreRoute = false;
                        storeSeedOnly = seedFrameOnly; storePacket = false; storeLeaseSeen = false; storeBounds = bounds; storeLayers = changedLayers;
                        if (!seedFrameOnly) EnsureStorePacket();
                        if (state.Route == null) RecordStoreRoute(0); else state.Route(bounds, changedLayers, storeRoute);
                        // The temporary read lease owns its exact ordinal even
                        // before the first immutable profile exists. Do not
                        // depend on a profile dictionary to route that sample.
                        if (seedLease && !storeLeaseSeen) RecordStoreRoute(current.doorSeed.Ordinal);
                        if (!sawStoreRoute)
                        {
                            if (seedFrameOnly) TouchSeedState(current, state, bounds);
                            else state.MarkAllRoutes();
                        }
                        storeWatch = null;
                    }
                }
            }
            private void RecordStoreRoute(int ordinal)
            {
                sawStoreRoute = true; WatchState state = storeWatch;
                if (ordinal < 0 || ordinal >= state.RouteCount)
                {
                    if (storeSeedOnly) TouchSeedState(owner, state, storeBounds);
                    else state.MarkAllRoutes();
                    return;
                }
                FrameProof frame = null;
                if (state.Frames != null) state.Frames.TryGetValue(ordinal, out frame);
                bool seedLease = (storeLayers & 1) != 0 && owner.SeedContains(state, storeX, storeY) && owner.doorSeed.Ordinal == ordinal;
                if (seedLease) storeLeaseSeen = true;
                // Ordinary terrain frames have no matcher meaning. Only a
                // Door raw seed (published or currently being sampled) owns
                // this bypass; an ordinary sibling gets no raw or hard event.
                if (storeSeedOnly && !seedLease && (frame == null || frame.Door == null || !DoorContains(frame.Door, storeX, storeY))) return;
                EnsureStorePacket(); state.MarkRawRoute(ordinal);
                if (frame == null || frame.Invalid || frame.Door == null && storeKind != 6 && storeKind != 7)
                { state.MarkRoute(ordinal); return; }
                // All nested native carriers share this outer transaction.
                // Two actual roots/threads touching one witness cannot borrow
                // each other's coverage or certify an interleaved operation.
                if (frame.Writer != null && !ReferenceEquals(frame.Writer, this)) { state.MarkRoute(ordinal); return; }
                if (frame.Writer == null) { frame.Begin(this); frames.Add(frame); }
                if (!frame.Apply(storeX, storeY, storeKind, storeOld, storeNew)) state.MarkRoute(ordinal);
            }
            private void EnsureStorePacket()
            {
                if (storePacket) return;
                storePacket = true;
                Packet(storeWatch, storeBounds, storeLayers);
                // Revoke native sampling BEFORE the original field store.
                // A normal final pose may preserve an existing proof later.
                owner.Invalidate(storeWatch, storeBounds, storeLayers, route: false);
            }
            internal void Touch(BlueprintProjectionBounds bounds, ushort changedLayers = 0xff)
            {
                var current = owner;
                if (current == null) return;
                lock (current.gate)
                {
                    PruneRetired(current);
                    foreach (WatchState state in current.watches.Values)
                    {
                        if (BuildRelevant(state, bounds, changedLayers)) state.RecordBuildRoutes(bounds, (ushort)(changedLayers & 7));
                        if (!Relevant(state, bounds, changedLayers)) continue;
                        TouchState(current, state, bounds, changedLayers);
                    }
                }
            }
            internal void TouchAll()
            {
                var current = owner;
                if (current == null) return;
                lock (current.gate)
                {
                    PruneRetired(current);
                    foreach (WatchState state in current.watches.Values)
                    {
                        if (BuildRelevant(state, state.Bounds, 7)) state.MarkAllBuildRoutes();
                        TouchState(current, state, state.Bounds, 0xff, allRoutes: true);
                    }
                }
            }
            internal bool TouchReads(ReadDependencies reads, ushort changedLayers, bool seedFrameOnly = false)
            {
                var current = owner; if (current == null || !ReferenceEquals(reads.Owner, current)) return false;
                bool touched = false;
                lock (current.gate)
                {
                    PruneRetired(current);
                    foreach (var pair in reads.Ranges)
                    {
                        WatchState live;
                        if (!current.watches.TryGetValue(pair.Key.Id, out live) || !ReferenceEquals(live, pair.Key)) continue;
                        if (!seedFrameOnly && ((pair.Value >> 8) & changedLayers) != 0 && BuildRelevant(live, live.Bounds, changedLayers))
                        { live.RecordBuildRoutes(live.Bounds, (ushort)(changedLayers & 7)); touched = true; }
                        if ((pair.Value & changedLayers) == 0) continue;
                        if (seedFrameOnly) touched |= TouchSeedState(current, live, live.Bounds);
                        else { TouchState(current, live, live.Bounds, changedLayers); touched = true; }
                    }
                }
                return touched;
            }
            private bool TouchSeedState(BlueprintWorldChangeObserver current, WatchState state, BlueprintProjectionBounds bounds)
            {
                // Pressure/unknown position loses only the finite Door seed
                // proofs in read-dependent watches, never ordinary siblings.
                // A real changed frame is required before entering this path.
                List<int> ordinals = null;
                if (state.Frames != null) foreach (var pair in state.Frames)
                    if (pair.Value.Door != null)
                    { if (ordinals == null) ordinals = new List<int>(); ordinals.Add(pair.Key); }
                bool lease = current.doorSeed != null && ReferenceEquals(current.doorSeed.Watch, state);
                if (ordinals == null && !lease) return false;
                Packet(state, bounds, 1); current.Invalidate(state, bounds, 1, route: false);
                if (ordinals != null) foreach (int ordinal in ordinals) state.MarkRoute(ordinal);
                if (lease) state.MarkRoute(current.doorSeed.Ordinal);
                return true;
            }
            private void PruneRetired(BlueprintWorldChangeObserver current)
            {
                // A long native call can overlap scene/session replacement.
                // Old watch objects have no consumer; do not retain a history
                // of them as new watches are touched by that same call.
                List<WatchState> retired = null;
                foreach (WatchState state in affected.Keys)
                {
                    WatchState live;
                    if (current.watches.TryGetValue(state.Id, out live) && ReferenceEquals(live, state)) continue;
                    if (retired == null) retired = new List<WatchState>(); retired.Add(state);
                }
                if (retired != null) foreach (WatchState state in retired) { state.Pending--; affected.Remove(state); }
                for (int i = frames.Count - 1; i >= 0; i--)
                {
                    FrameProof frame = frames[i]; WatchState live;
                    if (current.watches.TryGetValue(frame.Watch.Id, out live) && ReferenceEquals(live, frame.Watch) && !frame.Invalid) continue;
                    frame.Writer = null; frames.RemoveAt(i);
                }
            }
            private void TouchState(BlueprintWorldChangeObserver current, WatchState state, BlueprintProjectionBounds bounds, ushort layers, bool allRoutes = false)
            {
                Packet(state, bounds, layers);
                if (allRoutes) state.MarkAllRoutes();
                current.Invalidate(state, bounds, layers, route: !allRoutes);
            }
            private void Packet(WatchState state, BlueprintProjectionBounds bounds, ushort layers)
            {
                Impact old;
                if (!affected.TryGetValue(state, out old)) { state.Pending++; affected.Add(state, new Impact { Bounds = bounds, Layers = layers }); }
                else affected[state] = new Impact { Bounds = Union(old.Bounds, bounds), Layers = (ushort)(old.Layers | layers) };
            }
            public void Dispose() { Complete(true); }
            internal void Complete(bool normalExit)
            {
                var current = owner;
                if (current == null) return;
                lock (current.gate)
                {
                    if (owner == null) return;
                    owner = null;
                    foreach (FrameProof frame in frames)
                    {
                        FrameProof live; WatchState watch;
                        bool currentProof = current.watches.TryGetValue(frame.Watch.Id, out watch) && ReferenceEquals(watch, frame.Watch) &&
                            watch.Frames != null && watch.Frames.TryGetValue(frame.Ordinal, out live) && ReferenceEquals(live, frame);
                        if (currentProof && (!normalExit || !frame.Complete())) watch.MarkRoute(frame.Ordinal);
                        frame.Writer = null;
                    }
                    foreach (var pair in affected)
                    {
                        pair.Key.Pending--; pair.Key.RepublishPendingRoutes();
                        // The stored bbox/layer union is only a coarse packet.
                        // Re-routing it here would invent a cross-product of
                        // two distant raw writes and dirty untouched proofs.
                        current.Invalidate(pair.Key, pair.Value.Bounds, pair.Value.Layers, route: false);
                    }
                    affected.Clear(); frames.Clear();
                }
            }
        }

        // Only the pressure path keeps this summary: match/build layer bytes
        // per live watch, with no Tile values or event history. Merely reading
        // or reaching the coordinate cap never dirties or holds Pending. A
        // later actual, unlocated field change can conservatively revoke only
        // these range/layer dependencies until its native writer finally exits.
        internal sealed class ReadDependencies
        {
            internal readonly BlueprintWorldChangeObserver Owner;
            internal readonly Dictionary<WatchState, ushort> Ranges = new Dictionary<WatchState, ushort>();
            internal ReadDependencies(BlueprintWorldChangeObserver owner) { Owner = owner; }
            internal void Record(BlueprintProjectionBounds point)
            {
                lock (Owner.gate)
                {
                    List<WatchState> retired = null;
                    foreach (WatchState state in Ranges.Keys)
                    {
                        WatchState live;
                        if (Owner.watches.TryGetValue(state.Id, out live) && ReferenceEquals(live, state)) continue;
                        if (retired == null) retired = new List<WatchState>(); retired.Add(state);
                    }
                    if (retired != null) foreach (WatchState state in retired) Ranges.Remove(state);
                    foreach (WatchState state in Owner.watches.Values)
                    {
                        ushort layers = Owner.SeedContains(state, point.Left, point.Top) ? (ushort)1 : (ushort)0;
                        for (int bit = 1; bit <= 128; bit <<= 1) if (Relevant(state, point, (ushort)bit)) layers |= (ushort)bit;
                        for (int bit = 1; bit <= 4; bit <<= 1) if (BuildRelevant(state, point, (ushort)bit)) layers |= (ushort)(bit << 8);
                        if (layers == 0) continue;
                        ushort old; Ranges.TryGetValue(state, out old); Ranges[state] = (ushort)(old | layers);
                    }
                }
            }
            internal bool HasChanges(ushort changedLayers)
            {
                lock (Owner.gate)
                {
                    foreach (var pair in Ranges)
                    {
                        WatchState live;
                        if (((pair.Value | pair.Value >> 8) & changedLayers) != 0 && Owner.watches.TryGetValue(pair.Key.Id, out live) && ReferenceEquals(live, pair.Key)) return true;
                    }
                    return false;
                }
            }
        }

        private readonly object gate = new object();
        private readonly Dictionary<long, WatchState> watches = new Dictionary<long, WatchState>();
        private long session, version, buildVersion;
        private volatile int watchCount, rawSeedInterestCount;
        private bool disposed;
        private int interestLeft, interestTop, interestRight, interestBottom;
        private DoorSeedLease doorSeed;
        private readonly Action<int> seedInterestSink;
        private WatchState seedInterestWatch;
        private int seedInterestX, seedInterestY;
        private bool seedInterestFound, seedInterestEmitted;

        internal BlueprintWorldChangeObserver() { seedInterestSink = RecordSeedInterest; }

        internal int WatchCount { get { return watchCount; } }
        internal bool HasRawSeedInterests { get { return rawSeedInterestCount != 0; } }

        // Called at the Host's session boundary, before installing new range
        // identities. Late native finalizers retain only retired watch objects.
        internal void SetSession(long value)
        {
            lock (gate)
            {
                if (disposed || session == value) return;
                session = value; RetireWatches();
            }
            BlueprintWorldChangeHooks.ReleaseIdle(this);
        }

        internal void Watch(long id, BlueprintProjectionBounds worldBounds)
        { Watch(id, worldBounds, null); }

        // A source range's bbox may contain enormous sparse holes. The Host
        // supplies only immutable source/object dependency geometry, never a
        // world/scene/file lookup. This predicate runs under gate and must not
        // reenter the observer. Identity/section-wide invalidation can still
        // deliberately cover all relevant dependencies via their rectangles.
        internal void Watch(long id, BlueprintProjectionBounds worldBounds, Func<BlueprintProjectionBounds, bool> intersectsContent)
        { RegisterWatch(id, worldBounds, intersectsContent, null, 0xff); }

        // Bits share the permanent-mask counting unit: Tile=1, Wall=2,
        // Liquid=4, Actuator=8 and four independent wires=16/32/64/128.
        // TargetLayers is only a cheap union filter. The immutable predicate
        // must still check the changed layers at each actual source/member
        // coordinate, including the current mask and Tile-only object halo.
        internal void Watch(long id, BlueprintProjectionBounds worldBounds,
            Func<BlueprintProjectionBounds, ushort, bool> intersectsContent, ushort targetLayers)
        { RegisterWatch(id, worldBounds, null, intersectsContent, (ushort)(targetLayers & 0xff)); }

        // One physical/native watch can own several independent proof ranges.
        // The Host supplies a fixed ordinal count and a pure shared source
        // index visitor, not one additional native watch per object. A route
        // bit is set at each actual raw write before bounds/layers coalesce.
        internal void Watch(long id, BlueprintProjectionBounds worldBounds,
            Func<BlueprintProjectionBounds, ushort, bool> intersectsContent, ushort targetLayers,
            int routeCount, Action<BlueprintProjectionBounds, ushort, Action<int>> route)
        { Watch(id, worldBounds, intersectsContent, targetLayers, routeCount, route, null, null); }
        internal void Watch(long id, BlueprintProjectionBounds worldBounds,
            Func<BlueprintProjectionBounds, ushort, bool> intersectsContent, ushort targetLayers,
            int routeCount, Action<BlueprintProjectionBounds, ushort, Action<int>> route,
            Func<BlueprintProjectionBounds, ushort, bool> buildInterest,
            Action<BlueprintProjectionBounds, ushort, Action<int>> buildRoute)
        {
            if (routeCount <= 0) throw new ArgumentOutOfRangeException(nameof(routeCount));
            if (route == null && routeCount != 1) throw new ArgumentNullException(nameof(route));
            RegisterWatch(id, worldBounds, null, intersectsContent, (ushort)(targetLayers & 0xff), routeCount, route, buildInterest, buildRoute);
        }

        private void RegisterWatch(long id, BlueprintProjectionBounds worldBounds,
            Func<BlueprintProjectionBounds, bool> intersectsContent, Func<BlueprintProjectionBounds, ushort, bool> intersectsLayers, ushort targetLayers,
            int routeCount = 1, Action<BlueprintProjectionBounds, ushort, Action<int>> route = null,
            Func<BlueprintProjectionBounds, ushort, bool> buildInterest = null,
            Action<BlueprintProjectionBounds, ushort, Action<int>> buildRoute = null)
        {
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(BlueprintWorldChangeObserver));
                WatchState state;
                if (watches.TryGetValue(id, out state) && Same(state.Bounds, worldBounds) && state.TargetLayers == targetLayers &&
                    ReferenceEquals(state.IntersectsContent, intersectsContent) && ReferenceEquals(state.IntersectsLayers, intersectsLayers) &&
                    state.RouteCount == routeCount && ReferenceEquals(state.Route, route) && ReferenceEquals(state.BuildInterest, buildInterest) && ReferenceEquals(state.BuildRoute, buildRoute)) return;
                if (state != null) RetireWatch(state);
                int words = (routeCount - 1) / 64 + 1;
                state = new WatchState { Owner = this, Id = id, Bounds = worldBounds, IntersectsContent = intersectsContent,
                    IntersectsLayers = intersectsLayers, TargetLayers = targetLayers, Version = ++version,
                    RouteCount = routeCount, Route = route, RawRoutes = new ulong[words], DirtyRoutes = new ulong[words], PendingRoutes = new ulong[words],
                    BuildInterest = buildInterest, BuildRoute = buildRoute, BuildVersions = buildInterest == null ? null : new long[routeCount],
                    BuildDirtyRoutes = buildInterest == null ? null : new ulong[words] };
                state.RouteSink = state.MarkRoute; watches[id] = state;
                state.BuildSink = state.MarkBuildRoute;
                if (watchCount == 0)
                { interestLeft = worldBounds.Left; interestTop = worldBounds.Top; interestRight = worldBounds.Right; interestBottom = worldBounds.Bottom; }
                else
                { interestLeft = Math.Min(interestLeft, worldBounds.Left); interestTop = Math.Min(interestTop, worldBounds.Top); interestRight = Math.Max(interestRight, worldBounds.Right); interestBottom = Math.Max(interestBottom, worldBounds.Bottom); }
                watchCount = watches.Count;
            }
        }

        internal void Unwatch(long id)
        { lock (gate) { WatchState state; if (watches.TryGetValue(id, out state)) { RetireWatch(state); watches.Remove(id); } watchCount = watches.Count; } BlueprintWorldChangeHooks.ReleaseIdle(this); }

        internal void Clear()
        { lock (gate) RetireWatches(); BlueprintWorldChangeHooks.ReleaseIdle(this); }

        private void RetireWatch(WatchState state)
        {
            if (!state.Live) return;
            if (doorSeed != null && ReferenceEquals(doorSeed.Watch, state)) doorSeed.Dispose();
            state.ForgetFrames(); state.Live = false;
        }
        private void RetireWatches()
        {
            foreach (WatchState state in watches.Values) RetireWatch(state);
            watches.Clear(); watchCount = 0;
        }

        internal bool HasInterest(int x, int y)
        { return HasInterest(x, y, 0xff); }

        internal bool HasInterest(int x, int y, ushort changedLayers)
        {
            if (watchCount == 0 || x < interestLeft || x >= interestRight || y < interestTop || y >= interestBottom) return false;
            lock (gate)
            {
                var point = new BlueprintProjectionBounds(x, y, 1, 1);
                foreach (WatchState state in watches.Values) if (state.Bounds.Contains(x, y) && (Relevant(state, point, changedLayers) || BuildRelevant(state, point, changedLayers))) return true;
                return false;
            }
        }

        // This pure lookup is used only when the existing ordinary-frame
        // filter would otherwise return. The immutable dependency visitor
        // locates candidate ordinals; no Tile/metadata/source read is added.
        // No Door profile/lease means the original O(1) early return remains.
        internal bool HasRawSeedInterest(int x, int y)
        {
            if (rawSeedInterestCount == 0) return false;
            lock (gate)
            {
                if (disposed || rawSeedInterestCount == 0) return false;
                if (doorSeed != null && doorSeed.Bounds.Contains(x, y)) return true;
                var point = new BlueprintProjectionBounds(x, y, 1, 1);
                foreach (WatchState state in watches.Values)
                {
                    if (state.DoorCount == 0 || !Relevant(state, point, 1)) continue;
                    seedInterestWatch = state; seedInterestX = x; seedInterestY = y; seedInterestFound = false; seedInterestEmitted = false;
                    try { if (state.Route == null) RecordSeedInterest(0); else state.Route(point, 1, seedInterestSink); }
                    finally { seedInterestWatch = null; }
                    if (seedInterestFound || !seedInterestEmitted) return true;
                }
                return false;
            }
        }
        private void RecordSeedInterest(int ordinal)
        {
            seedInterestEmitted = true;
            WatchState state = seedInterestWatch; FrameProof frame;
            if (ordinal < 0 || ordinal >= state.RouteCount) { seedInterestFound = true; return; }
            if (state.Frames != null && state.Frames.TryGetValue(ordinal, out frame) && frame.Door != null && DoorContains(frame.Door, seedInterestX, seedInterestY)) seedInterestFound = true;
        }
        private static bool DoorContains(BlueprintWorldDoorWitness witness, int x, int y)
        { return x >= witness.WorldX && y >= witness.WorldY && (long)x < (long)witness.WorldX + witness.Width && (long)y < (long)witness.WorldY + witness.Height; }
        private bool SeedContains(WatchState state, int x, int y)
        { return doorSeed != null && ReferenceEquals(doorSeed.Watch, state) && doorSeed.Bounds.Contains(x, y); }

        internal IDisposable BeginDoorSeed(long id, long stamp, int ordinal, BlueprintProjectionBounds bounds)
        {
            lock (gate)
            {
                WatchState state;
                if (doorSeed != null || !IsCurrent(id, stamp) || !watches.TryGetValue(id, out state) || ordinal < 0 || ordinal >= state.RouteCount ||
                    bounds.Width != 3 || bounds.Height != 3 || bounds.Left < state.Bounds.Left || bounds.Top < state.Bounds.Top || bounds.Right > state.Bounds.Right || bounds.Bottom > state.Bounds.Bottom) return null;
                doorSeed = new DoorSeedLease(this, state, ordinal, bounds); rawSeedInterestCount++; return doorSeed;
            }
        }

        internal MutationBatch BeginBatch() { return new MutationBatch(this); }

        internal void Install() { BlueprintWorldChangeHooks.Install(this); }

        internal bool TryGetStamp(long id, out long stamp)
        {
            lock (gate)
            {
                WatchState state;
                bool ready = !disposed && !BlueprintWorldChangeHooks.NativeTransformPending && watches.TryGetValue(id, out state) && state.Pending == 0 && !state.Dirty;
                stamp = ready ? watches[id].Version : 0;
                return ready;
            }
        }

        // The game-thread consumer may publish only its already complete Match
        // witness at the exact native stamp it just checked. No in-flight raw
        // write can install or replace a canonical proof retroactively.
        internal bool TrySetWitness(long id, long stamp, int ordinal, IBlueprintWorldObjectWriteWitness witness)
        {
            if (!(witness is BlueprintWorldObjectFrameWitness) && !(witness is BlueprintWorldDoorWitness)) return false;
            lock (gate)
            {
                WatchState state;
                if (!IsCurrent(id, stamp) || !watches.TryGetValue(id, out state) || ordinal < 0 || ordinal >= state.RouteCount) return false;
                if (witness.MemberCount <= 0 || witness.Width <= 0 || witness.Height <= 0 ||
                    witness.WorldX < state.Bounds.Left || witness.WorldY < state.Bounds.Top ||
                    (long)witness.WorldX + witness.Width > state.Bounds.Right || (long)witness.WorldY + witness.Height > state.Bounds.Bottom) return false;
                if (state.Frames == null) state.Frames = new Dictionary<int, FrameProof>();
                state.ForgetFrame(ordinal); state.Frames[ordinal] = new FrameProof(state, ordinal, witness);
                if (witness is BlueprintWorldDoorWitness) { state.DoorCount++; rawSeedInterestCount++; }
                return true;
            }
        }

        internal bool IsCurrent(long id, long stamp)
        {
            lock (gate)
            {
                WatchState state;
                return !disposed && !BlueprintWorldChangeHooks.NativeTransformPending && watches.TryGetValue(id, out state) && state.Version == stamp && state.Pending == 0 && !state.Dirty;
            }
        }

        internal bool TryGetBuildStamp(long id, int ordinal, out long stamp)
        {
            lock (gate)
            {
                WatchState state;
                bool ready = !disposed && !BlueprintWorldChangeHooks.NativeTransformPending && watches.TryGetValue(id, out state) &&
                    state.BuildVersions != null && ordinal >= 0 && ordinal < state.RouteCount &&
                    (state.BuildDirtyRoutes[ordinal >> 6] & (1UL << (ordinal & 63))) == 0;
                stamp = ready ? watches[id].BuildVersions[ordinal] : 0; return ready;
            }
        }
        internal bool IsBuildCurrent(long id, int ordinal, long stamp)
        { long current; return TryGetBuildStamp(id, ordinal, out current) && current == stamp; }

        // The Host checks every range inside this same reentrant lock, then
        // retires the instance. A mutation prefix cannot slip between its last
        // check and that commit; an already entered mutation holds Pending.
        internal bool TryRunStable(long id, long stamp, Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            lock (gate)
            {
                if (!IsCurrent(id, stamp)) return false;
                action(); return true;
            }
        }

        // Compatibility/diagnostic coarse packets. Choose this family OR
        // DrainRoutes for a consumption pass. The union rectangle + OR layer
        // packet is conservative and cannot route independent object proofs.
        // Callbacks run under gate: ledger invalidation only, no world/IO/scene.
        internal void Drain(Action<long, BlueprintProjectionBounds> dirty)
        {
            if (dirty == null) throw new ArgumentNullException(nameof(dirty));
            Drain((id, bounds, layers) => dirty(id, bounds));
        }
        internal void Drain(Action<long, BlueprintProjectionBounds, ushort> dirty)
        {
            if (dirty == null) throw new ArgumentNullException(nameof(dirty));
            lock (gate)
            {
                foreach (WatchState state in watches.Values)
                {
                    if (!state.Dirty) continue;
                    dirty(state.Id, state.DirtyBounds, state.DirtyLayers);
                    state.ClearDirty();
                }
            }
        }

        // Fixed bits are bounded by the immutable proof count, not by event
        // history. Pending bits survive a drain and are republished on native
        // exit, so End never has to route the lossy coarse bbox/layer union.
        internal void DrainRoutes(Action<long, int> dirty)
        {
            if (dirty == null) throw new ArgumentNullException(nameof(dirty));
            DrainRoutes((id, ordinal, hard) => dirty(id, ordinal));
        }
        // Every raw route is delivered. A false hardDirty is permission only
        // to keep an EXISTING complete canonical proof; it never creates one
        // or validates unfinished raw samples. Native Pending/Version gates
        // remain independent and still revoke all in-flight raw reads.
        internal void DrainRoutes(Action<long, int, bool> dirty)
        { DrainRoutes(dirty, null); }
        internal void DrainRoutes(Action<long, int, bool> dirty, Action<long, int> buildDirty)
        {
            if (dirty == null) throw new ArgumentNullException(nameof(dirty));
            lock (gate)
            {
                foreach (WatchState state in watches.Values)
                {
                    if (state.BuildDirty)
                    {
                        for (int word = 0; word < state.BuildDirtyRoutes.Length; word++)
                        {
                            ulong bits = state.BuildDirtyRoutes[word]; int bit = 0;
                            while (bits != 0)
                            {
                                int ordinal = word * 64 + bit;
                                if ((bits & 1) != 0 && ordinal < state.RouteCount) buildDirty?.Invoke(state.Id, ordinal);
                                bits >>= 1; bit++;
                            }
                        }
                        Array.Clear(state.BuildDirtyRoutes, 0, state.BuildDirtyRoutes.Length); state.BuildDirty = false;
                    }
                    if (!state.Dirty) continue;
                    for (int word = 0; word < state.RawRoutes.Length; word++)
                    {
                        ulong bits = state.RawRoutes[word]; int bit = 0;
                        while (bits != 0)
                        {
                            int ordinal = word * 64 + bit;
                            if ((bits & 1) != 0 && ordinal < state.RouteCount) dirty(state.Id, ordinal, (state.DirtyRoutes[word] & (1UL << bit)) != 0);
                            bits >>= 1; bit++;
                        }
                    }
                    state.ClearDirty();
                }
            }
        }

        internal Mutation BeginMutation(BlueprintProjectionBounds worldBounds, ushort changedLayers = 0xff)
        {
            if (watchCount == 0 || worldBounds.Width <= 0 || worldBounds.Height <= 0) return null;
            lock (gate)
            {
                if (disposed || watches.Count == 0) return null;
                List<WatchState> affected = null;
                foreach (WatchState state in watches.Values)
                {
                    if (BuildRelevant(state, worldBounds, changedLayers)) state.RecordBuildRoutes(worldBounds, (ushort)(changedLayers & 7));
                    if (!Relevant(state, worldBounds, changedLayers)) continue;
                    if (affected == null) affected = new List<WatchState>();
                    affected.Add(state); state.Pending++;
                    // Revocation happens before the original writer runs,
                    // independently of when the Host next drains its ledger.
                    Invalidate(state, worldBounds, changedLayers);
                }
                return affected == null ? null : new Mutation(this, affected.ToArray(), worldBounds, changedLayers);
            }
        }

        private void Invalidate(WatchState state, BlueprintProjectionBounds changed, ushort layers, bool route = true)
        {
            int left = Math.Max(state.Bounds.Left, changed.Left), top = Math.Max(state.Bounds.Top, changed.Top);
            int right = Math.Min(state.Bounds.Right, changed.Right), bottom = Math.Min(state.Bounds.Bottom, changed.Bottom);
            if (state.Dirty)
            {
                left = Math.Min(left, state.DirtyBounds.Left); top = Math.Min(top, state.DirtyBounds.Top);
                right = Math.Max(right, state.DirtyBounds.Right); bottom = Math.Max(bottom, state.DirtyBounds.Bottom);
            }
            state.DirtyBounds = new BlueprintProjectionBounds(left, top, right - left, bottom - top);
            state.DirtyLayers = (ushort)(state.DirtyLayers | layers);
            if (route) state.RecordRoutes(changed, layers);
            state.Dirty = true; state.Version = ++version;
        }

        private static bool Intersects(BlueprintProjectionBounds a, BlueprintProjectionBounds b)
        { return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom; }
        private static bool Relevant(WatchState state, BlueprintProjectionBounds changed, ushort changedLayers)
        {
            ushort layers = (ushort)(state.TargetLayers & changedLayers);
            return layers != 0 && Intersects(state.Bounds, changed) &&
                (state.IntersectsContent == null || state.IntersectsContent(changed)) &&
                (state.IntersectsLayers == null || state.IntersectsLayers(changed, layers));
        }
        private static bool BuildRelevant(WatchState state, BlueprintProjectionBounds changed, ushort changedLayers)
        {
            ushort layers = (ushort)(changedLayers & 7);
            return layers != 0 && state.BuildInterest != null && Intersects(state.Bounds, changed) && state.BuildInterest(changed, layers);
        }
        private static ushort BuildStoreLayers(int kind, int delta)
        {
            // Native placement depends on presence/type, solid shape and
            // liquid, not a support tile's paint or animation frame. Locked
            // .8 Tile.cs puts active/inActive/halfBrick/slope in 0x7460.
            switch (kind)
            {
                case 0: return 1;
                case 1: return 2;
                case 2: return 4;
                case 3: return (ushort)((delta & 0x7460) != 0 ? 1 : 0);
                case 4: return (ushort)((delta & 0x60) != 0 ? 4 : 0);
                default: return 0;
            }
        }
        private static bool Same(BlueprintProjectionBounds a, BlueprintProjectionBounds b)
        { return a.Left == b.Left && a.Top == b.Top && a.Width == b.Width && a.Height == b.Height; }

        private static BlueprintProjectionBounds Union(BlueprintProjectionBounds a, BlueprintProjectionBounds b)
        {
            int left = Math.Min(a.Left, b.Left), top = Math.Min(a.Top, b.Top);
            return new BlueprintProjectionBounds(left, top, Math.Max(a.Right, b.Right) - left, Math.Max(a.Bottom, b.Bottom) - top);
        }

        public void Dispose()
        { lock (gate) { disposed = true; RetireWatches(); } BlueprintWorldChangeHooks.Uninstall(this); }
    }
}
