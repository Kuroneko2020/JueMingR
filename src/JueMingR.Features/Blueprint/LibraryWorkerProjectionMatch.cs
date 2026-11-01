using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private sealed class ProjectionSourceRequest
        {
            internal BlueprintProjectionIdentity Identity;
            internal long Generation, Serial, CapabilityRevision;
            internal BlueprintProjectionSourceDemand[] Demands;
            internal BlueprintProjectionView View;
        }
        private sealed class ProjectionSourceBuild
        {
            internal BlueprintProjectionBounds Bounds;
            internal long Block, PlannedBytes;
            internal int ObjectId, Offset, NextOffset, PlannedCells;
            internal bool TileOnly, Complete = true;
            internal CaptureObject Metadata;
            internal IReadOnlyList<long> MemberBlocks;
            internal List<CaptureCell> Cells;
            internal BlueprintProjectionWindow Window;
            internal IReadOnlyList<BlueprintProjectionWindow> DoorContexts;
            internal long DoorContextBytes;
        }
        private readonly struct ProjectionSourcePlan
        {
            internal ProjectionSourcePlan(BlueprintProjectionSourceDemand demand, int logicalObjectId, ProjectionSourceBuild build)
            { Demand = demand; LogicalObjectId = logicalObjectId; Build = build; }
            internal BlueprintProjectionSourceDemand Demand { get; }
            internal int LogicalObjectId { get; }
            internal ProjectionSourceBuild Build { get; }
        }
        private readonly Queue<ProjectionSourceRequest> projectionSourceRequests = new Queue<ProjectionSourceRequest>();
        private readonly Dictionary<BlueprintProjectionIdentity, long> projectionSourceSerials = new Dictionary<BlueprintProjectionIdentity, long>();
        private readonly Queue<BlueprintProjectionSourceReceipt> projectionSourceReceipts = new Queue<BlueprintProjectionSourceReceipt>();
        private ProjectionSourceRequest projectionSourceActive;
        private ProjectionOwner projectionSourceOwner;
        private IEnumerator<bool> projectionSourceReading;
        private long projectionSourceSerial;
        private long projectionSourceReceiptBytes;
#if DEBUG
        private long projectionSourceRequestsAccepted, projectionSourcePublished, projectionSourceParts, projectionSourceMemberBlocks, projectionSourceMemberRecords;
        private long projectionSourceWorkingBytes, projectionSourceWorkingPeak, projectionSourceReceiptPeak;
        internal BlueprintProjectionSourceStatistics ProjectionSourceStatistics
        {
            get
            {
                lock (gate) return new BlueprintProjectionSourceStatistics(Interlocked.Read(ref projectionSourceRequestsAccepted),
                    Interlocked.Read(ref projectionSourcePublished), Interlocked.Read(ref projectionSourceParts), Interlocked.Read(ref projectionSourceMemberBlocks),
                    Interlocked.Read(ref projectionSourceMemberRecords), Interlocked.Read(ref projectionSourceWorkingBytes), Interlocked.Read(ref projectionSourceWorkingPeak),
                    projectionSourceReceiptBytes, projectionSourceReceiptPeak, projectionSourceRequests.Count + (projectionSourceActive == null ? 0 : 1));
            }
        }
#endif

        internal bool RequestProjectionSource(BlueprintProjectionIdentity identity, long generation, int range, int objectId, int offset, long capabilityRevision = 0, BlueprintProjectionView view = null)
        { return RequestProjectionSources(identity, generation, new[] { new BlueprintProjectionSourceDemand(range, objectId, offset) }, capabilityRevision, view); }
        internal bool RequestProjectionSources(BlueprintProjectionIdentity identity, long generation, IReadOnlyList<BlueprintProjectionSourceDemand> demands, long capabilityRevision = 0, BlueprintProjectionView view = null)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (demands == null) throw new ArgumentNullException(nameof(demands));
            if (demands.Count == 0 || demands.Count > BlueprintProjectionSourceReceipt.MaximumPartCount) return false;
            var owned = new BlueprintProjectionSourceDemand[demands.Count];
            for (int i = 0; i < owned.Length; i++) owned[i] = demands[i];
            lock (gate)
            {
                if (stopping || !projectionReferences.ContainsKey(identity) || projectionSourceSerials.ContainsKey(identity) ||
                    projectionRequests.Count + projectionSourceRequests.Count + projectionQualificationRequests.Count + (projectionActiveRequest == null ? 0 : 1) + (projectionSourceActive == null ? 0 : 1) + (projectionQualifying == null ? 0 : 1) >= ProjectionRequestLimit ||
                    projectionReceipts.Count + projectionSourceReceipts.Count + projectionQualificationReceipts.Count + projectionFailures.Count >= ProjectionRequestLimit) return false;
                ProjectionOwner owner;
                if (capabilityRevision != 0 && (!projectionOwners.TryGetValue(identity.TemplateKey, out owner) || owner.QualificationCatalog?.Revision != capabilityRevision || owner.RequestedQualificationCatalog?.Revision != capabilityRevision)) return false;
                var request = new ProjectionSourceRequest { Identity = identity, Generation = generation, Demands = owned, Serial = ++projectionSourceSerial, CapabilityRevision = capabilityRevision, View = view };
#if DEBUG
                Interlocked.Increment(ref projectionSourceRequestsAccepted);
#endif
                projectionSourceSerials.Add(identity, request.Serial); projectionSourceRequests.Enqueue(request); projectionWake = true; Monitor.Pulse(gate); return true;
            }
        }
        internal void ClearProjectionSource(BlueprintProjectionIdentity identity)
        {
            lock (gate)
            {
                projectionSourceSerials.Remove(identity);
                int count = projectionSourceRequests.Count;
                while (count-- > 0) { var next = projectionSourceRequests.Dequeue(); if (!next.Identity.Equals(identity)) projectionSourceRequests.Enqueue(next); }
                count = projectionSourceReceipts.Count;
                while (count-- > 0) { var next = projectionSourceReceipts.Dequeue(); if (next.Identity.Equals(identity)) ChargeProjectionSourceReceipt(-next.EstimatedBytes); else projectionSourceReceipts.Enqueue(next); }
                projectionAwaitingReceiptSpace = false; projectionWake = true; Monitor.Pulse(gate);
            }
        }
        internal bool TryTakeProjectionSource(out BlueprintProjectionSourceReceipt receipt)
        {
            lock (gate)
            {
                receipt = projectionSourceReceipts.Count == 0 ? null : projectionSourceReceipts.Dequeue();
                if (receipt == null) return false;
                ChargeProjectionSourceReceipt(-receipt.EstimatedBytes); projectionAwaitingReceiptSpace = false; projectionWake = true; Monitor.Pulse(gate); return true;
            }
        }
        private bool ProjectionSourceCurrent(ProjectionOwner owner, ProjectionSourceRequest request)
        {
            lock (gate)
            {
                long serial;
                return !stopping && projectionReferences.ContainsKey(request.Identity) && projectionSourceSerials.TryGetValue(request.Identity, out serial) && serial == request.Serial && ProjectionCurrent(owner) &&
                    (request.CapabilityRevision == 0 || owner.QualificationCatalog?.Revision == request.CapabilityRevision && owner.RequestedQualificationCatalog?.Revision == request.CapabilityRevision);
            }
        }
        private void CheckProjectionSource(ProjectionOwner owner, ProjectionSourceRequest request)
        { if (!ProjectionSourceCurrent(owner, request)) throw new OperationCanceledException(); }
        private void CloseProjectionSourceWorker()
        {
            projectionSourceReading?.Dispose(); projectionSourceReading = null; projectionSourceActive = null; projectionSourceOwner = null;
            lock (gate) { projectionSourceRequests.Clear(); projectionSourceSerials.Clear(); ClearProjectionSourceReceipts(); }
        }
        private void ClearProjectionSourceReceipts()
        { while (projectionSourceReceipts.Count != 0) ChargeProjectionSourceReceipt(-projectionSourceReceipts.Dequeue().EstimatedBytes); }
        // gate owns both totals. The source-specific total is observational;
        // the shared total continues to enforce viewport/source backpressure.
        private void ChargeProjectionSourceReceipt(long bytes)
        {
            projectionReceiptBytes += bytes; projectionSourceReceiptBytes += bytes;
#if DEBUG
            if (projectionSourceReceiptBytes > projectionSourceReceiptPeak) projectionSourceReceiptPeak = projectionSourceReceiptBytes;
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        private void UpdateProjectionSourceWorkingBytes(long bytes)
        {
#if DEBUG
            Interlocked.Exchange(ref projectionSourceWorkingBytes, bytes);
            if (bytes > Interlocked.Read(ref projectionSourceWorkingPeak)) Interlocked.Exchange(ref projectionSourceWorkingPeak, bytes);
#endif
        }
        private void RetireProjectionSourceOwner(ProjectionOwner owner)
        {
            if (!ReferenceEquals(owner, projectionSourceOwner)) return;
            projectionSourceReading?.Dispose(); projectionSourceReading = null; projectionSourceActive = null; projectionSourceOwner = null;
        }
        private bool ProjectionSourceStep(bool preferred)
        {
            if (!preferred) return false;
            if (projectionSourceReading == null)
            {
                lock (gate)
                {
                    int count = projectionSourceRequests.Count;
                    while (count-- > 0)
                    {
                        var request = projectionSourceRequests.Dequeue(); ProjectionReference reference; ProjectionOwner owner; long serial;
                        if (!projectionSourceSerials.TryGetValue(request.Identity, out serial) || serial != request.Serial || !projectionReferences.TryGetValue(request.Identity, out reference)) continue;
                        if (!reference.Opened || !projectionOwners.TryGetValue(request.Identity.TemplateKey, out owner) || !owner.Ready) { projectionSourceRequests.Enqueue(request); continue; }
                        projectionSourceActive = request; projectionSourceOwner = owner; projectionSourceReading = ReadProjectionSource(owner, request).GetEnumerator(); break;
                    }
                }
            }
            if (projectionSourceReading == null) return false;
            try
            {
                if (projectionSourceReading.MoveNext()) return true;
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                // A source/hash failure is the same immutable asset failure as
                // a viewport read. Do not turn it into successful empty coverage.
                var owner = projectionSourceOwner; RetireProjectionSourceOwner(owner); FailProjection(owner, null, error); return true;
            }
            projectionSourceReading.Dispose(); projectionSourceReading = null; projectionSourceOwner = null; projectionSourceActive = null; return true;
        }
        private IEnumerable<bool> ReadProjectionSource(ProjectionOwner owner, ProjectionSourceRequest request)
        {
            CheckProjectionSource(owner, request);
            var summary = owner.Root.Summary;
            long summaryReferenceBytes = BlueprintProjectionSourcePart.SummaryReferenceBytes(summary);
            var plans = new List<ProjectionSourcePlan>(); var builds = new List<ProjectionSourceBuild>();
            var unique = new Dictionary<Tuple<long, int, int>, ProjectionSourceBuild>();
            long retained = BlueprintProjectionSourceReceipt.DirectoryBytes;
            // Plans/keys and their bounded container capacities are charged
            // separately from immutable payload. The raw cache is still shared.
            long working = 1024L + request.Demands.LongLength * 32L;
            UpdateProjectionSourceWorkingBytes(working);
            try
            {
                foreach (var demand in request.Demands)
                {
                    CheckProjectionSource(owner, request);
                    ProjectionQualifiedGeometry qualified;
                    var matchRanges = request.CapabilityRevision == 0 ? (IReadOnlyList<BlueprintProjectionSourceRange>)owner.MatchRanges :
                        owner.QualificationViews.TryGetValue(request.View?.MirroredHorizontally == true, out qualified) && ReferenceEquals(qualified.Profile, request.View?.Profile) ? qualified.Proofs : owner.QualificationMatchRanges;
                    if (demand.Range < 0 || demand.Range >= matchRanges.Count) throw new InvalidDataException("blueprint-source-range");
                    var range = matchRanges[demand.Range];
                    bool tileOnly = demand.ObjectId == 0 && range.ObjectId > 0;
                    int objectId = tileOnly ? range.ObjectId : demand.ObjectId;
                    var key = objectId == 0 ? Tuple.Create(range.Block, 0, demand.Offset) : Tuple.Create(-1L, objectId, tileOnly ? 1 : 2);
                    ProjectionSourceBuild build; bool shared = unique.TryGetValue(key, out build);
                    if (!shared)
                    {
                        build = PlanProjectionSource(owner, range, demand, objectId, tileOnly);
                        foreach (bool ignored in PlanProjectionDoorContexts(owner, request, build, working + 768)) yield return true;
                    }
                    long nextRetained = checked(retained + BlueprintProjectionSourcePart.DescriptorBytes + (shared ? 0 : build.PlannedBytes + summaryReferenceBytes));
                    // A full transport is a legal prefix, never a fatal asset
                    // error. A large first item retains the old single-window
                    // capacity and is served alone rather than being starved.
                    if (plans.Count != 0 && nextRetained > BlueprintProjectionSourceReceipt.TargetBytes) break;
                    if (build.PlannedBytes > ProjectionWindowLimit) throw new InvalidDataException("blueprint-projection-window-budget");
                    if (!shared)
                    {
                        unique.Add(key, build); builds.Add(build);
                        working += 768L + build.DoorContextBytes + (long)((build.Metadata.Reason ?? "").Length + (build.Metadata.MissingPayload ?? "").Length) * 2;
                    }
                    plans.Add(new ProjectionSourcePlan(demand, range.ObjectId, build)); retained = nextRetained; working += 128;
                    UpdateProjectionSourceWorkingBytes(working); yield return true;
                    if (retained > BlueprintProjectionSourceReceipt.TargetBytes) break;
                }

                var selected = new HashSet<long>(); var blocks = new List<long>();
                var ordinary = new Dictionary<long, List<ProjectionSourceBuild>>();
                var objects = new Dictionary<int, List<ProjectionSourceBuild>>();
                foreach (var build in builds)
                {
                    CheckProjectionSource(owner, request);
                    build.Cells = new List<CaptureCell>(build.PlannedCells); working += 64L + build.PlannedCells * 64L;
                    if (build.ObjectId == 0)
                    {
                        AddProjectionSourceRoute(ordinary, build.Block, build);
                        if (selected.Add(build.Block)) { blocks.Add(build.Block); working += 64; }
                    }
                    else
                    {
                        AddProjectionSourceRoute(objects, build.ObjectId, build);
                        foreach (long number in build.MemberBlocks)
                        {
                            CheckProjectionSource(owner, request);
                            if (selected.Add(number)) { blocks.Add(number); working += 64; }
                            UpdateProjectionSourceWorkingBytes(working); yield return true;
                        }
                    }
                    working += 128; UpdateProjectionSourceWorkingBytes(working);
                }
                // Consume all cache hits before decoding misses, as viewport
                // reads do. Each physical member block is scanned once for
                // the whole batch; ObjectId dispatch is independent of the
                // number of furniture items sharing that block.
                var ordered = new List<long>(blocks.Count); working += 32L + blocks.Count * 8L;
                foreach (long number in blocks) if (FindProjectionBlock(owner, number) != null) ordered.Add(number);
                foreach (long number in blocks) if (FindProjectionBlock(owner, number) == null) ordered.Add(number);
                UpdateProjectionSourceWorkingBytes(working);
                foreach (long number in ordered)
                {
                    CheckProjectionSource(owner, request); var chunk = ProjectionChunk(owner, number);
                    List<ProjectionSourceBuild> ordinaryTargets; ordinary.TryGetValue(number, out ordinaryTargets);
                    RecordProjectionObjectMemberBlock();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        RecordProjectionObjectMemberRecord();
                        var raw = chunk.Cells[i];
                        if (ordinaryTargets != null)
                        {
                            // Tile ownership stays in the complete-object lane.
                            // Preserve its captured identity/appearance only as
                            // same-cell material context; Host strips it before
                            // ordinary Match comparison. No extra DTO is kept.
                            var cell = raw; if (cell.ObjectId > 0) cell.Layers &= ~CaptureLayer.Tile;
                            if (cell.HasContent)
                            {
                                cell.X -= summary.Left; cell.Y -= summary.Top;
                                foreach (var build in ordinaryTargets) if (i >= build.Offset && i < build.NextOffset) build.Cells.Add(cell);
                            }
                        }
                        List<ProjectionSourceBuild> objectTargets;
                        if (raw.ObjectId > 0 && objects.TryGetValue(raw.ObjectId, out objectTargets))
                        {
                            raw.X -= summary.Left; raw.Y -= summary.Top;
                            foreach (var build in objectTargets)
                            {
                                var cell = raw;
                                // A wall scrape can remove this member's Tile
                                // appearance first. The object reducer needs
                                // the captured wall context, while Host's Match
                                // input remains Tile-only for this logical lane.
                                if (build.TileOnly) cell.Layers &= CaptureLayer.Tile | CaptureLayer.Wall;
                                build.Cells.Add(cell);
                            }
                        }
                        if ((i & 31) == 31) { CheckProjectionSource(owner, request); yield return true; }
                    }
                    yield return true;
                }

                foreach (var build in builds)
                {
                    CheckProjectionSource(owner, request);
                    if (build.ObjectId != 0 && build.Cells.Count != build.PlannedCells) throw new InvalidDataException("blueprint-object-member-reference");
                    foreach (bool ignored in SortProjectionObjectMembersSteps(build.Cells, () => CheckProjectionSource(owner, request))) yield return true;
                    // ToArray briefly coexists with the mutable construction
                    // buffer. Include both arrays and metadata/string payload
                    // in the working peak, then drop this build's staging list.
                    UpdateProjectionSourceWorkingBytes(working + build.PlannedBytes + summaryReferenceBytes);
                    build.Window = new BlueprintProjectionWindow(request.Identity, request.Generation, summary, build.Bounds,
                        build.Cells.ToArray(), build.ObjectId == 0 ? Array.Empty<CaptureObject>() : new[] { build.Metadata }, true);
                    if (build.Window.EstimatedBytes > ProjectionWindowLimit) throw new InvalidDataException("blueprint-projection-window-budget");
                    working += build.Window.EstimatedBytes + summaryReferenceBytes - (64L + build.Cells.Capacity * 64L); build.Cells = null;
                    UpdateProjectionSourceWorkingBytes(working); yield return true;
                }
                if (request.View != null)
                {
                    long rawBytes = 0;
                    foreach (var build in builds) rawBytes += build.Window.EstimatedBytes + build.DoorContextBytes;
                    // Discover IDs from the accepted raw prefix and all of its
                    // contexts, then share finite complete-member batches. The
                    // transport arrays retain their physical offset contract.
                    foreach (bool ignored in TransformProjectionSourceViewsSteps(owner, request, builds, working)) yield return true;
                    foreach (var build in builds)
                    {
                        CheckProjectionSource(owner, request);
                        build.DoorContextBytes = BlueprintProjectionSourcePart.DoorContextBytes(build.DoorContexts);
                        if (build.Window.EstimatedBytes > ProjectionWindowLimit) throw new InvalidDataException("blueprint-projection-window-budget");
                        working += build.Window.EstimatedBytes + build.DoorContextBytes;
                    }
                    working -= rawBytes;
                    UpdateProjectionSourceWorkingBytes(working); yield return true;
                }
                var parts = new BlueprintProjectionSourcePart[plans.Count];
                for (int i = 0; i < parts.Length; i++)
                { var plan = plans[i]; parts[i] = new BlueprintProjectionSourcePart(plan.Demand, plan.LogicalObjectId, plan.Build.Window, plan.Build.NextOffset, plan.Build.Complete, plan.Build.DoorContexts); }
                var receipt = new BlueprintProjectionSourceReceipt(request.Identity, request.Generation, parts, capabilityRevision: request.CapabilityRevision, view: request.View);
                UpdateProjectionSourceWorkingBytes(working + BlueprintProjectionSourceReceipt.DirectoryBytes + parts.LongLength * BlueprintProjectionSourcePart.DescriptorBytes);
                if (receipt.EstimatedBytes > BlueprintProjectionSourceReceipt.MaximumEstimatedBytes) throw new InvalidDataException("blueprint-projection-window-budget");
                while (true)
                {
                    CheckProjectionSource(owner, request);
                    lock (gate)
                    {
                        // Clear/re-request may race the last outside check.
                        // Test the exact serial again under the publication lock
                        // so an old read cannot erase or publish over its heir.
                        CheckProjectionSource(owner, request);
                        if (projectionReceipts.Count + projectionSourceReceipts.Count + projectionQualificationReceipts.Count < ProjectionRequestLimit && projectionReceiptBytes + receipt.EstimatedBytes <= BlueprintProjectionSourceReceipt.MaximumEstimatedBytes)
                        {
                            projectionSourceReceipts.Enqueue(receipt); ChargeProjectionSourceReceipt(receipt.EstimatedBytes); projectionSourceSerials.Remove(request.Identity);
#if DEBUG
                            Interlocked.Increment(ref projectionSourcePublished); Interlocked.Add(ref projectionSourceParts, parts.Length);
#endif
                            UpdateProjectionSourceWorkingBytes(0); break;
                        }
                        projectionAwaitingReceiptSpace = true;
                    }
                    yield return true;
                }
            }
            finally { UpdateProjectionSourceWorkingBytes(0); }
        }
        private static void AddProjectionSourceRoute<TKey>(Dictionary<TKey, List<ProjectionSourceBuild>> routes, TKey key, ProjectionSourceBuild build)
        { List<ProjectionSourceBuild> values; if (!routes.TryGetValue(key, out values)) { values = new List<ProjectionSourceBuild>(); routes.Add(key, values); } values.Add(build); }
        private ProjectionSourceBuild PlanProjectionSource(ProjectionOwner owner, BlueprintProjectionSourceRange range,
            BlueprintProjectionSourceDemand demand, int objectId, bool tileOnly)
        {
            var build = new ProjectionSourceBuild { Bounds = range.Bounds, Block = range.Block, Offset = demand.Offset, ObjectId = objectId, TileOnly = tileOnly };
            if (objectId == 0)
            {
                var chunk = ProjectionChunk(owner, range.Block);
                if (demand.Offset < 0 || demand.Offset >= chunk.Count) throw new InvalidDataException("blueprint-source-offset");
                build.PlannedCells = chunk.Count - demand.Offset; build.NextOffset = chunk.Count;
                build.PlannedBytes = 256L + build.PlannedCells * 64L; return build;
            }
            var metadata = ProjectionChunk(owner, owner.Directory.FindObject(objectId, ProjectionComparison));
            int low = 0, high = metadata.ObjectCount - 1; bool found = false;
            while (low <= high)
            {
                int at = low + (high - low) / 2; var obj = metadata.Objects[at];
                if (obj.Id < objectId) low = at + 1; else if (obj.Id > objectId) high = at - 1;
                else { build.Metadata = obj; found = true; break; }
            }
            if (!found) throw new InvalidDataException("blueprint-missing-object-reference");
            ProjectionSourceObjectIndex source = null;
            if (owner.SourceObjects != null) owner.SourceObjects.TryGetValue(objectId, out source);
            int members = source == null ? 0 : source.Members;
            build.MemberBlocks = source == null ? (IReadOnlyList<long>)Array.Empty<long>() : source.Blocks;
            var target = build.Metadata; target.X -= owner.Root.Summary.Left; target.Y -= owner.Root.Summary.Top; build.Metadata = target;
            build.Bounds = new BlueprintProjectionBounds(target.X, target.Y, target.Width, target.Height); build.PlannedCells = members;
            build.PlannedBytes = checked(384L + members * 64L + (long)((target.Reason ?? "").Length + (target.MissingPayload ?? "").Length) * 2);
            return build;
        }
    }
}
