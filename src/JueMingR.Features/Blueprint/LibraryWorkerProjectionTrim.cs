using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        // The gate owns only revocable permission. Mutable geometry/layer
        // collections belong exclusively to the existing LibraryWorker thread.
        private BlueprintProjectionCommitRequest projectionTrimLease;
        private bool projectionTrimCleanup;
        private ProjectionTrimOperation projectionTrimOperation;
#if DEBUG
        private long projectionTrimWorkingBytes, projectionTrimWorkingPeak, projectionTrimAccumulateSteps,
            projectionTrimRecordVisits, projectionTrimFreezeCount, projectionTrimMaskCopies;
#endif
        private sealed class ProjectionTrimOperation
        {
            internal readonly BlueprintProjectionCommitRequest Key;
            internal readonly Dictionary<long, ushort> Cells = new Dictionary<long, ushort>();
            internal readonly HashSet<int> Objects = new HashSet<int>();
            internal long VerifiedVersion, Removed, RemovedConstruction;
            internal bool Frozen;
            internal ProjectionTrimOperation(BlueprintProjectionCommitRequest key) { Key = key; }
        }
        private static bool SameTrimOperation(BlueprintProjectionCommitRequest left, BlueprintProjectionCommitRequest right)
        {
            var a = left.Candidate; var b = right.Candidate;
            return left.SessionGeneration == right.SessionGeneration && left.TransactionId == right.TransactionId && left.SceneRevision == right.SceneRevision &&
                a.Identity.Equals(b.Identity) && ReferenceEquals(a.Mask, b.Mask) && a.OriginX == b.OriginX && a.OriginY == b.OriginY &&
                a.CommitOrder == b.CommitOrder && a.MirroredHorizontally == b.MirroredHorizontally && ReferenceEquals(a.MirrorProfile, b.MirrorProfile);
        }
        private void RevokeProjectionTrimLocked()
        { if (projectionTrimLease == null) return; projectionTrimLease = null; projectionTrimCleanup = true; Monitor.Pulse(gate); }
        private void RetireProjectionCommitIdentityLocked(BlueprintProjectionIdentity identity)
        {
            if (projectionTrimLease?.Candidate.Identity.Equals(identity) == true) RevokeProjectionTrimLocked();
            var request = projectionActiveCommit ?? projectionCommit ?? projectionCommitReceipt?.Request;
            if (request == null) return;
            bool affected = request.Candidate.Identity.Equals(identity);
            foreach (var old in request.Previous) if (old.Identity.Equals(identity)) affected = true;
            if (affected) CancelAllProjectionCommitsLocked();
        }
        private void ReleaseProjectionTrimWorker()
        {
            // Never Clear a collection handed to a final mask. Freeze builds
            // independent containers and cleanup drops only private references.
            projectionTrimOperation = null; UpdateProjectionTrimBytes(0);
        }
        private void UpdateProjectionTrimBytes(long value)
        {
#if DEBUG
            Interlocked.Exchange(ref projectionTrimWorkingBytes, value);
            if (value > Interlocked.Read(ref projectionTrimWorkingPeak)) Interlocked.Exchange(ref projectionTrimWorkingPeak, value);
#endif
        }
        private static long TrimOverlayBytes(ProjectionTrimOperation operation)
        { return operation == null ? 0 : 160L + operation.Cells.Count * 32L + operation.Objects.Count * 24L; }
        private void CheckProjectionTrim(BlueprintProjectionCommitRequest request)
        {
            CheckProjectionCommit(request);
            lock (gate) if (projectionTrimLease == null || !SameTrimOperation(projectionTrimLease, request)) throw new OperationCanceledException();
        }
        private ProjectionTrimOperation CurrentProjectionTrim(BlueprintProjectionCommitRequest request)
        {
            CheckProjectionTrim(request);
            if (projectionTrimOperation == null)
            {
                if (request.TrimPhase != BlueprintProjectionTrimPhase.Accumulate || request.ExpectedTrimVersion != 0)
                    throw new InvalidDataException("blueprint-trim-prefix-missing");
                projectionTrimOperation = new ProjectionTrimOperation(request);
            }
            var operation = projectionTrimOperation;
            if (!SameTrimOperation(operation.Key, request) || operation.Frozen || operation.VerifiedVersion != request.ExpectedTrimVersion)
                throw new InvalidDataException("blueprint-trim-prefix-changed");
            return operation;
        }
        private static ushort RemainingTrimLayers(ProjectionTrimOperation operation, CaptureCell cell)
        {
            ushort bits = BlueprintProjectionMask.Layers(operation.Key.Candidate.Mask.Apply(cell)), removed;
            operation.Cells.TryGetValue(ProjectionDirectory.Key(cell.X, cell.Y), out removed);
            if (cell.ObjectId > 0 && operation.Objects.Contains(cell.ObjectId)) removed |= 1;
            return (ushort)(bits & ~removed);
        }
        private static void AddTrimCell(ProjectionTrimOperation operation, CaptureCell cell, ushort bits)
        {
            bits &= RemainingTrimLayers(operation, cell); if (bits == 0) return;
            long coordinate = ProjectionDirectory.Key(cell.X, cell.Y); ushort previous;
            operation.Cells.TryGetValue(coordinate, out previous); operation.Cells[coordinate] = (ushort)(previous | bits);
            operation.Removed = checked(operation.Removed + BlueprintProjectionMask.Count(bits));
            operation.RemovedConstruction = checked(operation.RemovedConstruction + BlueprintProjectionMask.Count(BlueprintConstructionTargets.FilterBits(bits)));
        }
        private IEnumerable<bool> ComputeProjectionTrim(BlueprintProjectionCommitRequest request, ProjectionOwner owner)
        {
            var operation = CurrentProjectionTrim(request);
            if (request.TrimPhase == BlueprintProjectionTrimPhase.Freeze)
            {
                foreach (bool step in FreezeProjectionTrim(request, operation)) yield return step;
                yield break;
            }
            if (request.ThroughTrimVersion <= request.ExpectedTrimVersion) throw new InvalidDataException("blueprint-trim-prefix-order");
            var geometry = request.TrimGeometry;
            if (geometry == null && request.TrimBounds.HasValue)
            {
                // The legacy request shape is a single delta, not a second
                // trim implementation; all work still uses Accumulate/Freeze.
                geometry = new BlueprintTrimGeometry(); geometry.AddRectangle(request.Candidate.View.SourceBounds(request.TrimBounds.Value));
            }
            if (geometry == null || !geometry.HasContent) throw new InvalidDataException("blueprint-trim-bounds");
            var summary = owner.Root.Summary; var blocks = new HashSet<long>(); var newObjects = new List<int>(); var distinctObjects = new HashSet<int>();
            int checkpoint = 0;
            foreach (var span in geometry.EnumerateSpans())
            {
                CheckProjectionTrim(request);
                if (span.Left < 0 || span.Y < 0 || span.RightExclusive > summary.Width || span.Y >= summary.Height || span.Left >= span.RightExclusive)
                    throw new InvalidDataException("blueprint-trim-bounds");
                foreach (long block in owner.Directory.Query(checked(span.Left + summary.Left), checked(span.Y + summary.Top), checked(span.RightExclusive + summary.Left), checked(span.Y + summary.Top + 1), ProjectionComparison)) blocks.Add(block);
                if ((++checkpoint & 31) == 0) yield return true;
            }
            foreach (long block in blocks)
            {
                CheckProjectionTrim(request); var chunk = ProjectionChunk(owner, block);
                RecordProjectionObjectMemberBlock();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if ((i & 31) == 0) { CheckProjectionTrim(request); yield return true; }
                    RecordProjectionObjectMemberRecord();
#if DEBUG
                    Interlocked.Increment(ref projectionTrimRecordVisits);
#endif
                    var cell = chunk.Cells[i]; cell.X -= summary.Left; cell.Y -= summary.Top;
                    if (!geometry.Contains(cell.X, cell.Y)) continue;
                    ushort bits = RemainingTrimLayers(operation, cell);
                    if ((bits & 1) != 0 && cell.ObjectId > 0)
                    { if (distinctObjects.Add(cell.ObjectId)) newObjects.Add(cell.ObjectId); bits &= 0xfe; }
                    AddTrimCell(operation, cell, bits);
                }
                UpdateProjectionTrimBytes(TrimOverlayBytes(operation) + blocks.Count * 24L + newObjects.Count * 8L);
                yield return true;
            }
            var extensions = new List<BlueprintProjectionBounds>(); int next = 0;
            while (next < newObjects.Count)
            {
                CheckProjectionTrim(request);
                using (var batch = PlanProjectionObjectMemberBatch(owner, newObjects, ref next))
                {
                    foreach (bool step in CollectProjectionObjectMembersSteps(owner, batch, () => CheckProjectionTrim(request),
                        bytes => UpdateProjectionTrimBytes(TrimOverlayBytes(operation) + blocks.Count * 24L + newObjects.Count * 8L + bytes))) yield return step;
                    foreach (var entry in batch.Entries.Values)
                    {
                        CheckProjectionTrim(request); var obj = entry.Metadata; long removed = 0; int count = 0;
                        foreach (var member in entry.Members)
                        {
                            if ((++count & 31) == 0) { CheckProjectionTrim(request); yield return true; }
                            if ((RemainingTrimLayers(operation, member) & 1) != 0) removed++;
                        }
                        if (removed == 0) continue;
                        if (!obj.Reliable || removed != (long)obj.Width * obj.Height) throw new InvalidDataException("blueprint-trim-object-incomplete");
                        operation.Objects.Add(obj.Id); operation.Removed = checked(operation.Removed + removed); operation.RemovedConstruction = checked(operation.RemovedConstruction + removed);
                        // Only Tile expands. This geometry never enters the
                        // Host's all-layer manual-union difference baseline.
                        extensions.Add(new BlueprintProjectionBounds(obj.X, obj.Y, obj.Width, obj.Height));
                        yield return true;
                    }
                }
            }
            CheckProjectionTrim(request); operation.VerifiedVersion = request.ThroughTrimVersion;
            UpdateProjectionTrimBytes(TrimOverlayBytes(operation));
#if DEBUG
            Interlocked.Increment(ref projectionTrimAccumulateSteps);
#endif
            var plan = new BlueprintProjectionTrimPlan(request.Candidate.Mask, null, operation.VerifiedVersion, operation.Removed, operation.RemovedConstruction, extensions);
            PublishProjectionTrim(request, plan);
        }
        private void PublishProjectionTrim(BlueprintProjectionCommitRequest request, BlueprintProjectionTrimPlan plan)
        {
            lock (gate)
            {
                CheckProjectionTrim(request);
                projectionCommitReceipt = new BlueprintProjectionCommitReceipt(request, new Dictionary<long, BlueprintProjectionMask>(), new List<long>(), trimPlan: plan);
            }
        }
        private IEnumerable<bool> FreezeProjectionTrim(BlueprintProjectionCommitRequest request, ProjectionTrimOperation operation)
        {
            if (request.ThroughTrimVersion != operation.VerifiedVersion || request.ConfirmedTrimRemovedCount != operation.Removed || operation.Removed <= 0)
                throw new InvalidDataException("blueprint-trim-confirmation-changed");
            operation.Frozen = true;
            var baseline = request.Candidate.Mask; var cells = new Dictionary<long, ushort>(); var objects = new HashSet<int>(); int progress = 0;
#if DEBUG
            Interlocked.Increment(ref projectionTrimFreezeCount); Interlocked.Increment(ref projectionTrimMaskCopies);
#endif
            // Exactly one final composition, outside the gate and in cooperative
            // slices. Accumulation never copies or publishes baseline containers.
            foreach (var pair in baseline.RawCells)
            {
                cells.Add(pair.Key, pair.Value);
                if ((++progress & 31) == 0) { CheckProjectionTrim(request); yield return true; }
            }
            foreach (var pair in operation.Cells)
            {
                ushort old; cells.TryGetValue(pair.Key, out old); cells[pair.Key] = (ushort)(old | pair.Value);
                if ((++progress & 31) == 0) { CheckProjectionTrim(request); UpdateProjectionTrimBytes(TrimOverlayBytes(operation) + cells.Count * 32L + objects.Count * 24L); yield return true; }
            }
            foreach (int id in baseline.RawObjects)
            { objects.Add(id); if ((++progress & 31) == 0) { CheckProjectionTrim(request); yield return true; } }
            foreach (int id in operation.Objects)
            { objects.Add(id); if ((++progress & 31) == 0) { CheckProjectionTrim(request); yield return true; } }
            CheckProjectionTrim(request);
            var mask = new BlueprintProjectionMask(cells, objects, checked(baseline.RemovedLayerCount + operation.Removed), checked(baseline.RemovedConstructionLayerCount + operation.RemovedConstruction));
            var plan = new BlueprintProjectionTrimPlan(baseline, mask, operation.VerifiedVersion, operation.Removed, operation.RemovedConstruction, Array.Empty<BlueprintProjectionBounds>());
            PublishProjectionTrim(request, plan);
            ReleaseProjectionTrimWorker();
        }
    }
}
