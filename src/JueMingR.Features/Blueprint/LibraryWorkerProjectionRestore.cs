using System;
using System.Collections.Generic;
using System.IO;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private readonly Queue<ProjectionReference> projectionRestoreRequests = new Queue<ProjectionReference>();
        private IEnumerator<bool> projectionRestoring;
        private ProjectionReference projectionRestoreReference;
        private void CloseProjectionRestore()
        { projectionRestoring?.Dispose(); projectionRestoring = null; projectionRestoreReference = null; projectionRestoreRequests.Clear(); }
        private void CheckProjectionRestore(ProjectionReference reference)
        {
            lock (gate)
            { ProjectionReference current; if (stopping || !projectionReferences.TryGetValue(reference.Identity, out current) || !ReferenceEquals(reference, current)) throw new OperationCanceledException(); }
        }
        private bool ProjectionRestoreStep(bool turn)
        {
            if (!turn) return false;
            if (projectionRestoring == null)
            {
                lock (gate)
                {
                    while (projectionRestoreRequests.Count != 0)
                    {
                        var next = projectionRestoreRequests.Dequeue(); ProjectionReference current; ProjectionOwner owner;
                        if (!projectionReferences.TryGetValue(next.Identity, out current) || !ReferenceEquals(next, current) || !projectionOwners.TryGetValue(next.Identity.TemplateKey, out owner) || !owner.Ready) continue;
                        projectionRestoreReference = next; projectionRestoring = ValidateProjectionRestore(owner, next).GetEnumerator(); break;
                    }
                }
            }
            if (projectionRestoring == null) return false;
            try
            {
                if (projectionRestoring.MoveNext()) return true;
                lock (gate) { CheckProjectionRestore(projectionRestoreReference); projectionRestoreReference.RestoreValidated = true; projectionWake = true; }
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                lock (gate)
                {
                    var reference = projectionRestoreReference; var id = reference.Identity;
                    ProjectionReference current; ProjectionOwner owner;
                    if (projectionReferences.TryGetValue(id, out current) && ReferenceEquals(current, reference) &&
                        projectionOwners.TryGetValue(id.TemplateKey, out owner))
                    {
                        var scope = ProjectionFailureScope(error);
                        if (scope == BlueprintProjectionFailureScope.SharedAsset)
                            FailProjectionAssetLocked(owner, error, BlueprintProjectionFailureStage.Restore);
                        else
                        {
                            // A bad saved mask or pose is an exact record
                            // failure; an oversized proof is a demand refusal.
                            if (scope != BlueprintProjectionFailureScope.Demand) scope = BlueprintProjectionFailureScope.SavedInstance;
                            projectionFailures.Enqueue(new BlueprintProjectionReceipt(BlueprintProjectionReceiptKind.Failed, id,
                                error: Reason(error), failureCode: ProjectionFailureCode(error), identityRetired: true,
                                failureScope: scope, failureStage: BlueprintProjectionFailureStage.Restore, failureDetail: ProjectionFailureDetail(error)));
                            RemoveProjectionReferenceLocked(id);
                        }
                    }
                }
            }
            projectionRestoring.Dispose(); projectionRestoring = null; projectionRestoreReference = null; return true;
        }
        private IEnumerable<bool> ValidateProjectionRestore(ProjectionOwner owner, ProjectionReference reference)
        {
            var mask = reference.RestoreMask; var summary = owner.Root.Summary; long removed = 0, construction = 0; int processed = 0;
            foreach (var pair in mask.RawCells)
            {
                CheckProjectionRestore(reference); int x = (int)pair.Key, y = (int)(pair.Key >> 32);
                if (x < 0 || y < 0 || x >= summary.Width || y >= summary.Height) throw new InvalidDataException("blueprint-scene-mask-bounds");
                int sx = checked(x + summary.Left), sy = checked(y + summary.Top); bool found = false;
                foreach (long number in owner.Directory.Query(sx, sy, checked(sx + 1), checked(sy + 1), ProjectionComparison))
                {
                    CaptureChunk chunk = ProjectionChunk(owner, number); long key = ProjectionDirectory.Key(sx, sy); int low = 0, high = chunk.Count - 1;
                    while (low <= high)
                    {
                        int mid = low + (high - low) / 2; CaptureCell cell = chunk.Cells[mid]; long current = ProjectionDirectory.Key(cell.X, cell.Y);
                        if (current < key) low = mid + 1; else if (current > key) high = mid - 1;
                        else { if ((pair.Value & ~BlueprintProjectionMask.Layers(cell)) != 0 || cell.ObjectId > 0 && (pair.Value & 1) != 0) throw new InvalidDataException("blueprint-scene-mask-layer"); found = true; break; }
                    }
                }
                if (!found) throw new InvalidDataException("blueprint-scene-mask-cell");
                removed += BlueprintProjectionMask.Count(pair.Value); construction += BlueprintProjectionMask.Count(BlueprintConstructionTargets.FilterBits(pair.Value));
                if ((++processed & 31) == 0) yield return true;
            }
            foreach (int id in mask.RawObjects)
            {
                CheckProjectionRestore(reference); ProjectionSourceObjectIndex obj;
                if (owner.SourceObjects == null || !owner.SourceObjects.TryGetValue(id, out obj)) throw new InvalidDataException("blueprint-scene-mask-object");
                removed = checked(removed + obj.Members); construction = checked(construction + obj.Members);
                if ((++processed & 31) == 0) yield return true;
            }
            if (removed != mask.RemovedLayerCount || construction != mask.RemovedConstructionLayerCount || removed >= owner.TotalLayers) throw new InvalidDataException("blueprint-scene-mask-count");
            if (reference.RestoreMirroredProfile != null)
            {
                var view = new BlueprintProjectionView(summary, true, mask, reference.RestoreMirroredProfile);
                foreach (bool step in ValidateProjectionView(owner, view, () => CheckProjectionRestore(reference))) yield return step;
            }
        }
    }
}
