using System;
using System.Collections.Generic;
using System.Threading;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private sealed class ProjectionQualificationRequest
        {
            internal BlueprintProjectionIdentity Identity;
            internal BlueprintPlacementCatalog Catalog;
            internal BlueprintProjectionMask Mask;
            internal BlueprintProjectionView View;
        }
        // The existing file worker owns this finite demand lane. At most one
        // request per instance is queued; superseding masks/catalogs cancel the
        // old pass at its next bounded yield. Completed geometry is shared per
        // template and contains numeric positions only, never another cell table.
        private readonly Dictionary<BlueprintProjectionIdentity, ProjectionQualificationRequest> projectionQualificationLatest = new Dictionary<BlueprintProjectionIdentity, ProjectionQualificationRequest>();
        private readonly List<ProjectionQualificationRequest> projectionQualificationRequests = new List<ProjectionQualificationRequest>();
        private readonly Queue<BlueprintProjectionQualificationReceipt> projectionQualificationReceipts = new Queue<BlueprintProjectionQualificationReceipt>();
        private ProjectionQualificationRequest projectionQualificationActive;
        private ProjectionOwner projectionQualificationOwner;
        private IEnumerator<bool> projectionQualifying;
#if DEBUG
        private long projectionQualificationWorkingBytes;
#endif
        [System.Diagnostics.Conditional("DEBUG")]
        private void UpdateProjectionQualificationWorkingBytes(long bytes)
        {
#if DEBUG
            Interlocked.Exchange(ref projectionQualificationWorkingBytes, bytes);
#endif
        }

        internal bool RequestProjectionQualification(BlueprintProjectionIdentity identity, BlueprintPlacementCatalog catalog, BlueprintProjectionMask mask, BlueprintProjectionView view = null)
        {
            if (identity == null || catalog == null) return false;
            mask = mask ?? BlueprintProjectionMask.Empty;
            lock (gate)
            {
                if (stopping || !projectionReferences.ContainsKey(identity)) return false;
                ProjectionOwner owner; projectionOwners.TryGetValue(identity.TemplateKey, out owner);
                // Reject a stale catalog before touching the accepted newer
                // queue/result. Rejection must not cancel its successor.
                if (owner?.RequestedQualificationCatalog != null && owner.RequestedQualificationCatalog.Revision > catalog.Revision) return false;
                ProjectionQualificationRequest old;
                if (projectionQualificationLatest.TryGetValue(identity, out old))
                {
                    if (old.Catalog.Revision > catalog.Revision) return false;
                    if (ReferenceEquals(old.Catalog, catalog) && ReferenceEquals(old.Mask, mask) && ReferenceEquals(old.View, view)) return true;
                }
                int queued = projectionQualificationRequests.FindIndex(value => value.Identity.Equals(identity));
                if (projectionRequests.Count + projectionSourceRequests.Count + projectionQualificationRequests.Count - (queued < 0 ? 0 : 1) +
                    (projectionActiveRequest == null ? 0 : 1) + (projectionSourceActive == null ? 0 : 1) + (projectionQualifying == null ? 0 : 1) >= ProjectionRequestLimit ||
                    projectionReceipts.Count + projectionSourceReceipts.Count + projectionQualificationReceipts.Count + projectionFailures.Count >= ProjectionRequestLimit) return false;
                var request = new ProjectionQualificationRequest { Identity = identity, Catalog = catalog, Mask = mask, View = view };
                if (queued >= 0) projectionQualificationRequests.RemoveAt(queued);
                projectionQualificationLatest[identity] = request; projectionQualificationRequests.Add(request);
                if (owner != null)
                {
                    owner.RequestedQualificationCatalog = catalog;
                    int retained = projectionQualificationReceipts.Count;
                    while (retained-- > 0)
                    {
                        var receipt = projectionQualificationReceipts.Dequeue();
                        if (!receipt.Identity.TemplateKey.Equals(identity.TemplateKey) || ReferenceEquals(receipt.Catalog, catalog)) projectionQualificationReceipts.Enqueue(receipt);
                        else projectionReceiptBytes -= BlueprintProjectionQualificationReceipt.DescriptorBytes;
                    }
                }
                RemoveProjectionQualificationReceipts(identity); projectionWake = true; Monitor.Pulse(gate); return true;
            }
        }
        internal bool TryTakeProjectionQualification(out BlueprintProjectionQualificationReceipt receipt)
        {
            lock (gate)
            {
                receipt = projectionQualificationReceipts.Count == 0 ? null : projectionQualificationReceipts.Dequeue();
                if (receipt != null) { projectionReceiptBytes -= BlueprintProjectionQualificationReceipt.DescriptorBytes; projectionAwaitingReceiptSpace = false; projectionWake = true; Monitor.Pulse(gate); }
                return receipt != null;
            }
        }
        private void RemoveProjectionQualificationReceipts(BlueprintProjectionIdentity identity)
        {
            int count = projectionQualificationReceipts.Count;
            while (count-- > 0)
            {
                var receipt = projectionQualificationReceipts.Dequeue();
                if (!receipt.Identity.Equals(identity)) projectionQualificationReceipts.Enqueue(receipt);
                else projectionReceiptBytes -= BlueprintProjectionQualificationReceipt.DescriptorBytes;
            }
            projectionAwaitingReceiptSpace = false;
        }
        private void RetireProjectionQualification(BlueprintProjectionIdentity identity)
        {
            projectionQualificationLatest.Remove(identity); projectionQualificationRequests.RemoveAll(value => value.Identity.Equals(identity));
            RemoveProjectionQualificationReceipts(identity);
        }
        private void CloseProjectionQualification()
        {
            projectionQualifying?.Dispose(); projectionQualifying = null; projectionQualificationActive = null; projectionQualificationOwner = null;
            projectionQualificationLatest.Clear(); projectionQualificationRequests.Clear();
            while (projectionQualificationReceipts.Count != 0) { projectionQualificationReceipts.Dequeue(); projectionReceiptBytes -= BlueprintProjectionQualificationReceipt.DescriptorBytes; }
        }
        private bool ProjectionQualificationCurrent(ProjectionOwner owner, ProjectionQualificationRequest request)
        {
            lock (gate)
            {
                ProjectionQualificationRequest current;
                return !stopping && projectionReferences.ContainsKey(request.Identity) && ProjectionCurrent(owner) &&
                    projectionQualificationLatest.TryGetValue(request.Identity, out current) && ReferenceEquals(current, request) &&
                    ReferenceEquals(owner.RequestedQualificationCatalog, request.Catalog);
            }
        }
        private void CheckProjectionQualification(ProjectionOwner owner, ProjectionQualificationRequest request)
        { if (!ProjectionQualificationCurrent(owner, request)) throw new OperationCanceledException(); }
        private bool ProjectionQualificationStep(bool preferred)
        {
            if (!preferred) return false;
            if (projectionQualifying == null)
            {
                lock (gate)
                {
                    for (int i = 0; i < projectionQualificationRequests.Count; i++)
                    {
                        var request = projectionQualificationRequests[i]; ProjectionOwner owner;
                        if (!projectionReferences.ContainsKey(request.Identity)) { projectionQualificationRequests.RemoveAt(i--); continue; }
                        if (!projectionOwners.TryGetValue(request.Identity.TemplateKey, out owner) || !owner.Ready) continue;
                        if (owner.RequestedQualificationCatalog != null && owner.RequestedQualificationCatalog.Revision > request.Catalog.Revision)
                        { projectionQualificationRequests.RemoveAt(i--); continue; }
                        projectionQualificationRequests.RemoveAt(i); owner.RequestedQualificationCatalog = request.Catalog;
                        projectionQualificationActive = request; projectionQualificationOwner = owner;
                        projectionQualifying = QualifyProjection(owner, request).GetEnumerator(); break;
                    }
                }
            }
            if (projectionQualifying == null) return false;
            try { if (projectionQualifying.MoveNext()) return true; }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                // A validated source becoming unreadable is still an asset
                // failure. Never publish zero targets after a partial pass.
                FailProjection(projectionQualificationOwner, null, error);
            }
            projectionQualifying.Dispose(); projectionQualifying = null; projectionQualificationOwner = null; projectionQualificationActive = null;
            return true;
        }
        private BlueprintConstructionSelection SelectProjectionConstruction(ProjectionOwner owner, CaptureCell cell, BlueprintPlacementCatalog catalog, BlueprintProjectionView view)
        {
            if (cell.ObjectId <= 0)
            {
                if (view?.MirroredHorizontally == true)
                {
                    var normalized = cell; normalized.X -= owner.Root.Summary.Left; normalized.Y -= owner.Root.Summary.Top; string reason;
                    if (!view.Profile.TryMirrorCell(normalized, view.Width, out normalized, out reason))
                        return new BlueprintConstructionSelection(BlueprintConstructionTargets.Layers(cell), BlueprintConstructionTargets.Layers(cell), catalog.Revision);
                    return catalog.Select(normalized);
                }
                return catalog.Select(cell);
            }
            ProjectionSourceObjectIndex entry = owner.SourceObjects[cell.ObjectId];
            if (!ProjectionObjectQualificationCurrent(entry, catalog, view)) throw new InvalidOperationException("blueprint-object-qualification-not-prepared");
            ushort bits = BlueprintConstructionTargets.FilterBits(BlueprintProjectionMask.Layers(cell)), unknown = 0;
            if (entry.Qualification.Disposition == BlueprintTargetDisposition.DisplayOnly) bits &= 0xfe;
            else if (entry.Qualification.Disposition == BlueprintTargetDisposition.Unresolved) unknown |= 1;
            if ((bits & 2) != 0)
            {
                var wall = catalog.QualifyWall(cell.WallType);
                if (wall.Disposition == BlueprintTargetDisposition.DisplayOnly) bits &= 0xfd;
                else if (wall.Disposition == BlueprintTargetDisposition.Unresolved) unknown |= 2;
            }
            return new BlueprintConstructionSelection(bits, unknown, catalog.Revision);
        }
        private static bool ProjectionObjectQualificationCurrent(ProjectionSourceObjectIndex entry, BlueprintPlacementCatalog catalog, BlueprintProjectionView view)
        { return ReferenceEquals(entry.QualificationCatalog, catalog) && ReferenceEquals(entry.QualificationProfile, view?.Profile) && entry.QualificationMirrored == (view?.MirroredHorizontally == true); }

        private IEnumerable<bool> PrepareProjectionObjectQualificationsSteps(ProjectionOwner owner, ProjectionQualificationRequest request,
            CaptureChunk chunk, long working)
        {
            var ids = new List<int>(); var firstCells = new Dictionary<int, CaptureCell>();
            Action check = () => CheckProjectionQualification(owner, request);
            RecordProjectionObjectMemberBlock();
            for (int i = 0; i < chunk.Count; i++)
            {
                RecordProjectionObjectMemberRecord(); var cell = chunk.Cells[i]; ProjectionSourceObjectIndex entry;
                if (cell.ObjectId > 0 && owner.SourceObjects.TryGetValue(cell.ObjectId, out entry) &&
                    !ProjectionObjectQualificationCurrent(entry, request.Catalog, request.View) && !firstCells.ContainsKey(cell.ObjectId))
                { ids.Add(cell.ObjectId); firstCells.Add(cell.ObjectId, cell); }
                if ((i & 31) == 31) { check(); yield return true; }
            }
            long temporary = 256L + ids.Capacity * 4L + firstCells.Count * 128L;
            UpdateProjectionQualificationWorkingBytes(working + temporary);
            // With no profile this is the old metadata/first-cell selection;
            // no complete body is needed or acquired merely for a cold cache.
            if (request.View?.Profile == null)
            {
                foreach (int id in ids)
                {
                    check(); var entry = owner.SourceObjects[id];
                    entry.Qualification = request.Catalog.QualifyTile(firstCells[id], ProjectionObject(owner, id));
                    entry.QualificationCatalog = request.Catalog; entry.QualificationProfile = null; entry.QualificationMirrored = request.View?.MirroredHorizontally == true;
                    yield return true;
                }
                yield break;
            }
            int next = 0;
            while (next < ids.Count)
            {
                check();
                using (var batch = PlanProjectionObjectMemberBatch(owner, ids, ref next))
                {
                    foreach (bool ignored in CollectProjectionObjectMembersSteps(owner, batch, check,
                        bytes => UpdateProjectionQualificationWorkingBytes(working + temporary + bytes))) yield return true;
                    foreach (var pair in batch.Entries)
                    {
                        check(); var body = pair.Value; var obj = body.Metadata; var first = body.Members[0]; string reason;
                        bool interpreted; RecordProjectionObjectInterpretation();
                        if (request.View.MirroredHorizontally)
                        {
                            CaptureCell[] targets;
                            interpreted = request.View.Profile.TryMirrorObject(obj, body.Members, request.View.Width, out obj, out targets, out reason);
                            if (interpreted)
                            {
                                if (targets == null || targets.Length != body.Members.Count) throw new System.IO.InvalidDataException("blueprint-mirror-object-members");
                                first = targets[0];
                            }
                        }
                        else interpreted = request.View.Profile.TryNormalizeObject(obj, body.Members, out obj, out reason);
                        check(); var entry = owner.SourceObjects[pair.Key];
                        entry.Qualification = interpreted ? request.Catalog.QualifyTile(first, obj) : new BlueprintTargetQualification(BlueprintTargetDisposition.Unresolved,
                            BlueprintPlacementResolutionKind.Unsupported, null, BlueprintTargetReason.TargetShapeUnresolved, request.Catalog.Revision);
                        entry.QualificationCatalog = request.Catalog; entry.QualificationProfile = request.View.Profile; entry.QualificationMirrored = request.View.MirroredHorizontally;
                        // Only the small catalog/profile/pose result survives.
                        // Later blocks reuse it without retaining this body.
                        yield return true;
                    }
                }
            }
        }
        private IEnumerable<bool> QualifyProjection(ProjectionOwner owner, ProjectionQualificationRequest request)
        {
            try
            {
            CheckProjectionQualification(owner, request);
            var catalog = request.Catalog; var summary = owner.Root.Summary;
            ProjectionQualifiedGeometry geometry;
            bool mirrored = request.View?.MirroredHorizontally == true;
            bool buildGeometry = !owner.QualificationViews.TryGetValue(mirrored, out geometry) || !ReferenceEquals(geometry.Catalog, catalog) || !ReferenceEquals(geometry.Profile, request.View?.Profile);
            var sources = buildGeometry ? new List<BlueprintProjectionSourceRange>() : null;
            var proofs = buildGeometry ? new List<BlueprintProjectionSourceRange>() : null;
            var indexed = buildGeometry ? new HashSet<int>() : null;
            long total = 0, effective = 0, unknown = 0, geometryBytes = 0;
            for (int group = 0; group < owner.SourceRanges.Count; group++)
            {
                CheckProjectionQualification(owner, request); var rawRange = owner.SourceRanges[group];
                CaptureChunk chunk = ProjectionChunk(owner, rawRange.Block);
                // Current block staging and its temporary geometry arrays are
                // bounded by the existing decoded block. Completed numeric
                // geometry is charged while this incremental pass retains it.
                long working = geometryBytes + chunk.Count * 256L + 1024L +
                    (buildGeometry ? (sources.Capacity + proofs.Capacity) * 64L + indexed.Count * 64L : 0L);
                UpdateProjectionQualificationWorkingBytes(working);
                foreach (bool ignored in PrepareProjectionObjectQualificationsSteps(owner, request, chunk, working)) yield return true;
                var positions = buildGeometry ? new List<long>() : null; var bits = buildGeometry ? new List<ushort>() : null;
                var ids = buildGeometry ? new List<int>() : null; var objectIds = buildGeometry ? new HashSet<int>() : null;
                for (int i = 0; i < chunk.Count; i++)
                {
                    CaptureCell cell = chunk.Cells[i]; var selection = SelectProjectionConstruction(owner, cell, catalog, request.View);
                    if (summary.TargetVersion != catalog.TargetVersion) selection = new BlueprintConstructionSelection(BlueprintConstructionTargets.Layers(cell), BlueprintConstructionTargets.Layers(cell), catalog.Revision);
                    total += BlueprintProjectionMask.Count(selection.RequiredBits);
                    ushort remaining = request.Mask.RemainingLayers(cell.X - summary.Left, cell.Y - summary.Top, cell.ObjectId, selection.RequiredBits);
                    effective += BlueprintProjectionMask.Count(remaining); unknown += BlueprintProjectionMask.Count((ushort)(remaining & selection.UnknownBits));
                    if (buildGeometry && selection.RequiredBits != 0)
                    {
                        positions.Add(((long)(cell.Y-summary.Top)<<32)|(uint)(cell.X-summary.Left)); bits.Add(selection.RequiredBits); ids.Add(cell.ObjectId);
                        if (cell.ObjectId > 0 && (selection.RequiredBits & 1) != 0) objectIds.Add(cell.ObjectId);
                    }
                    if ((i & 31) == 31) { CheckProjectionQualification(owner, request); yield return true; }
                }
                if (buildGeometry)
                {
                    var dependencies = new List<BlueprintProjectionSourceDependency>();
                    foreach (int id in objectIds) dependencies.Add(new BlueprintProjectionSourceDependency(ProjectionSourceObjectBounds(ProjectionObject(owner,id),summary),id));
                    long targets = 0; foreach (ushort value in bits) targets += BlueprintProjectionMask.Count(value);
                    var source = new BlueprintProjectionSourceRange(rawRange.Block,rawRange.Bounds,targets,positions.ToArray(),bits.ToArray(),ids.ToArray(),dependencies,group);
                    var ordinaryPositions = new List<long>(); var ordinaryBits = new List<ushort>(); var ordinaryIds = new List<int>(); long ordinaryTargets = 0;
                    for (int i = 0; i < positions.Count; i++)
                    {
                        ushort value = (ushort)(bits[i] & (ids[i] > 0 ? 0xfe : 0xff)); if (value == 0) continue;
                        ordinaryPositions.Add(positions[i]); ordinaryBits.Add(value); ordinaryIds.Add(0); ordinaryTargets += BlueprintProjectionMask.Count(value);
                    }
                    int proofStart = proofs.Count; BlueprintProjectionSourceRange? ordinary = null;
                    var routes = new List<int>(); var routedObjects = new List<int>();
                    if (ordinaryPositions.Count != 0)
                    {
                        ordinary = new BlueprintProjectionSourceRange(rawRange.Block,rawRange.Bounds,ordinaryTargets,ordinaryPositions.ToArray(),ordinaryBits.ToArray(),ordinaryIds.ToArray(),Array.Empty<BlueprintProjectionSourceDependency>(),group);
                        proofs.Add(ordinary.Value); geometryBytes += ordinary.Value.SourceIndexBytes;
                    }
                    foreach (int id in objectIds) if (indexed.Add(id))
                    {
                        var obj = ProjectionObject(owner,id); var bounds = ProjectionSourceObjectBounds(obj,summary);
                        var proof = new BlueprintProjectionSourceRange(rawRange.Block,bounds,(long)obj.Width*obj.Height,
                            new[]{((long)(obj.Y-summary.Top)<<32)|(uint)(obj.X-summary.Left)},new ushort[]{1},new[]{id},new[]{new BlueprintProjectionSourceDependency(bounds,id)},group,id);
                        routedObjects.Add(id); routes.Add(proofs.Count-proofStart); proofs.Add(proof); geometryBytes += proof.SourceIndexBytes;
                        CheckProjectionQualification(owner, request); yield return true;
                    }
                    source = source.WithRouting(ordinary,routedObjects.ToArray(),routes.ToArray()); sources.Add(source); geometryBytes += source.SourceIndexBytes;
                }
                yield return true;
            }
            CheckProjectionQualification(owner, request);
            lock (gate)
            {
                CheckProjectionQualification(owner, request);
                if (buildGeometry)
                {
                    if (!ReferenceEquals(owner.QualificationCatalog, catalog) || !ReferenceEquals(owner.QualificationProfile, request.View?.Profile)) owner.QualificationViews.Clear();
                    geometry = new ProjectionQualifiedGeometry { Catalog = catalog, Profile = request.View?.Profile,
                        Sources = Array.AsReadOnly(sources.ToArray()), Proofs = Array.AsReadOnly(proofs.ToArray()), Bytes = geometryBytes + 128L + (sources.Count + proofs.Count)*64L };
                    owner.QualificationViews[mirrored] = geometry;
                }
                owner.QualificationCatalog = catalog; owner.QualificationProfile = request.View?.Profile;
                owner.QualificationSourceRanges = geometry.Sources; owner.QualificationMatchRanges = geometry.Proofs;
                owner.QualificationGeometryBytes = 0; foreach (var cached in owner.QualificationViews.Values) owner.QualificationGeometryBytes += cached.Bytes;
            }
            var completed = new BlueprintProjectionQualificationReceipt(request.Identity,catalog,request.Mask,total,effective,unknown,
                geometry.Sources,geometry.Proofs,view: request.View);
            while (true)
            {
                CheckProjectionQualification(owner, request);
                lock (gate)
                {
                    CheckProjectionQualification(owner, request);
                    if (projectionReceipts.Count + projectionSourceReceipts.Count + projectionQualificationReceipts.Count < ProjectionRequestLimit &&
                        projectionReceiptBytes + BlueprintProjectionQualificationReceipt.DescriptorBytes <= BlueprintProjectionSourceReceipt.MaximumEstimatedBytes)
                    { projectionQualificationReceipts.Enqueue(completed); projectionReceiptBytes += BlueprintProjectionQualificationReceipt.DescriptorBytes; break; }
                    projectionAwaitingReceiptSpace = true;
                }
                yield return true;
            }
            UpdateProjectionStatistics();
            }
            finally { UpdateProjectionQualificationWorkingBytes(0); }
        }
    }
}
