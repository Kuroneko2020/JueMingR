using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private BlueprintProjectionCommitRequest projectionCommit;
        private BlueprintProjectionCommitReceipt projectionCommitReceipt;
        private IEnumerator<bool> projectionCommitting;
        private BlueprintProjectionCommitRequest projectionActiveCommit;
        internal bool RequestProjectionCommit(BlueprintProjectionCommitRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            lock (gate)
            {
                if (stopping || projectionCommit != null || projectionActiveCommit != null || projectionCommitReceipt != null) return false;
                if (!projectionReferences.ContainsKey(request.Candidate.Identity)) return false;
                projectionCommit = request; Monitor.Pulse(gate); return true;
            }
        }
        internal void CancelProjectionCommit(long session, long transaction)
        {
            lock (gate)
            {
                if (projectionCommit != null && projectionCommit.SessionGeneration == session && projectionCommit.TransactionId == transaction) projectionCommit = null;
                if (projectionActiveCommit != null && projectionActiveCommit.SessionGeneration == session && projectionActiveCommit.TransactionId == transaction) projectionActiveCommit = null;
                if (projectionCommitReceipt != null && projectionCommitReceipt.Request.SessionGeneration == session && projectionCommitReceipt.Request.TransactionId == transaction) projectionCommitReceipt = null;
                Monitor.Pulse(gate);
            }
        }
        internal bool TryTakeProjectionCommit(out BlueprintProjectionCommitReceipt receipt)
        { lock (gate) { receipt = projectionCommitReceipt; projectionCommitReceipt = null; return receipt != null; } }
        private void CancelAllProjectionCommitsLocked() { projectionCommit = null; projectionActiveCommit = null; projectionCommitReceipt = null; }
        private void RetireProjectionCommitAssetLocked(Guid id)
        {
            BlueprintProjectionCommitRequest request = projectionActiveCommit ?? projectionCommit;
            if (request == null) return;
            bool affected = request.Candidate.Identity.AssetId == id;
            foreach (BlueprintProjectionPlacement placement in request.Previous) if (placement.Identity.AssetId == id) affected = true;
            if (affected) CancelAllProjectionCommitsLocked();
        }
        private void CloseProjectionCommitWorker()
        { if (projectionCommitting != null) projectionCommitting.Dispose(); projectionCommitting = null; lock (gate) CancelAllProjectionCommitsLocked(); }
        private bool ProjectionCommitHasWorkLocked() { return projectionCommit != null || projectionCommitting != null; }
        private void CheckProjectionCommit(BlueprintProjectionCommitRequest request, BlueprintProjectionIdentity current = null, bool allReferences = false)
        {
            lock (gate)
            {
                if (stopping || !ReferenceEquals(projectionActiveCommit, request) || request.Candidate.Identity.SessionGeneration != request.SessionGeneration || !projectionReferences.ContainsKey(request.Candidate.Identity)) throw new OperationCanceledException();
                if (current != null && (current.SessionGeneration != request.SessionGeneration || !projectionReferences.ContainsKey(current))) throw new OperationCanceledException();
                // Full scene references are proved at entry/publication. Inner
                // checkpoints only need the active reader plus transaction;
                // rescanning M opponents per block would add quadratic work.
                if (allReferences) foreach (BlueprintProjectionPlacement old in request.Previous) if (old.Identity.SessionGeneration != request.SessionGeneration || !projectionReferences.ContainsKey(old.Identity)) throw new OperationCanceledException();
            }
        }
        private bool ProjectionCommitStep(bool turn)
        {
            if (!turn) return false;
            lock (gate)
            {
                if (projectionCommitting == null && projectionCommit != null)
                { projectionActiveCommit = projectionCommit; projectionCommit = null; projectionCommitting = ComputeProjectionCommit(projectionActiveCommit).GetEnumerator(); }
            }
            if (projectionCommitting == null) return false;
            try
            {
                if (projectionCommitting.MoveNext()) return true;
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                lock (gate) if (projectionActiveCommit != null) projectionCommitReceipt = new BlueprintProjectionCommitReceipt(projectionActiveCommit, new Dictionary<long, BlueprintProjectionMask>(), new List<long>(), Reason(error));
            }
            projectionCommitting.Dispose(); projectionCommitting = null; lock (gate) projectionActiveCommit = null; return true;
        }
        private sealed class ProjectionMaskBuilder
        {
            internal readonly Dictionary<long, ushort> Cells;
            internal readonly HashSet<int> Objects;
            internal long Removed, RemovedConstruction;
            internal readonly BlueprintProjectionMask View;
            internal ProjectionMaskBuilder(BlueprintProjectionMask source) { Cells = source.CopyCells(); Objects = source.CopyObjects(); Removed = source.RemovedLayerCount; RemovedConstruction = source.RemovedConstructionLayerCount; View = new BlueprintProjectionMask(Cells, Objects, Removed, RemovedConstruction); }
            internal BlueprintProjectionMask Mask { get { return new BlueprintProjectionMask(Cells, Objects, Removed, RemovedConstruction); } }
            internal void AddCell(CaptureCell visible, ushort bits)
            {
                bits &= BlueprintProjectionMask.Layers(visible); if (bits == 0) return;
                long key = ProjectionDirectory.Key(visible.X, visible.Y); ushort previous; Cells.TryGetValue(key, out previous); Cells[key] = (ushort)(previous | bits); Removed += BlueprintProjectionMask.Count(bits);
                RemovedConstruction += BlueprintProjectionMask.Count(BlueprintConstructionTargets.FilterBits(bits));
            }
        }
        private ProjectionOwner CommitOwner(BlueprintProjectionIdentity identity)
        {
            ProjectionOwner owner;
            if (!projectionOwners.TryGetValue(identity.TemplateKey, out owner) || !owner.Ready) throw new InvalidDataException("blueprint-projection-template-not-ready");
            return owner;
        }
        private IEnumerable<bool> ComputeProjectionCommit(BlueprintProjectionCommitRequest request)
        {
            CheckProjectionCommit(request, allReferences: true); ProjectionOwner incoming = CommitOwner(request.Candidate.Identity);
            if (request.Kind == BlueprintProjectionEditKind.Trim)
            { foreach (bool step in ComputeProjectionTrim(request, incoming)) yield return step; yield break; }
            if (request.Kind == BlueprintProjectionEditKind.Mirror)
                foreach (bool step in ValidateProjectionMirror(request, incoming)) yield return step;
            var masks = new Dictionary<long, BlueprintProjectionMask>(); var retired = new List<long>();
            foreach (BlueprintProjectionPlacement old in request.Previous)
            {
                CheckProjectionCommit(request, old.Identity);
                // Moving is a new commit of one identity. Its old position is a
                // rollback snapshot, never an overlap opponent or erase source.
                if (old.Identity.Equals(request.Candidate.Identity)) continue;
                long left = Math.Max((long)old.OriginX, request.Candidate.OriginX), top = Math.Max((long)old.OriginY, request.Candidate.OriginY);
                long right = Math.Min((long)old.OriginX + old.Summary.Width, (long)request.Candidate.OriginX + request.Candidate.Summary.Width);
                long bottom = Math.Min((long)old.OriginY + old.Summary.Height, (long)request.Candidate.OriginY + request.Candidate.Summary.Height);
                if (left >= right || top >= bottom) continue;
                ProjectionOwner previous = CommitOwner(old.Identity); var builder = new ProjectionMaskBuilder(old.Mask);
                CaptureSummary source = incoming.Root.Summary, target = previous.Root.Summary;
                var incomingBounds = request.Candidate.View.SourceBounds(new BlueprintProjectionBounds(checked((int)(left - request.Candidate.OriginX)), checked((int)(top - request.Candidate.OriginY)), checked((int)(right - left)), checked((int)(bottom - top))));
                foreach (long number in incoming.Directory.Query(checked(incomingBounds.Left + source.Left), checked(incomingBounds.Top + source.Top), checked(incomingBounds.Right + source.Left), checked(incomingBounds.Bottom + source.Top), ProjectionComparison))
                {
                    CheckProjectionCommit(request, old.Identity); CaptureChunk chunk = ProjectionChunk(incoming, number);
                    var occupied = new Dictionary<long, ushort>(); var targetBlocks = new HashSet<long>();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        CaptureCell cell = chunk.Cells[i]; cell.X -= source.Left; cell.Y -= source.Top; cell = request.Candidate.Mask.Apply(cell);
                        if (!cell.HasContent) continue;
                        long worldX = (long)request.Candidate.View.DisplayX(cell.X) + request.Candidate.OriginX, worldY = (long)cell.Y + request.Candidate.OriginY;
                        if (worldX < left || worldX >= right || worldY < top || worldY >= bottom) continue;
                        int x = checked(old.View.SourceX(checked((int)(worldX - old.OriginX))) + target.Left), y = checked((int)(worldY - old.OriginY + target.Top));
                        occupied[ProjectionDirectory.Key(x, y)] = BlueprintProjectionMask.Layers(cell);
                        // Exact cell intervals avoid joining distant source windows
                        // or sparse endpoints into an enormous read rectangle.
                        foreach (long block in previous.Directory.Query(x, y, checked(x + 1), checked(y + 1), ProjectionComparison)) targetBlocks.Add(block);
                    }
                    foreach (long targetNumber in targetBlocks)
                    {
                        CheckProjectionCommit(request, old.Identity); CaptureChunk oldChunk = ProjectionChunk(previous, targetNumber);
                        var touchedObjects = new HashSet<int>();
                        for (int i = 0; i < oldChunk.Count; i++)
                        {
                            CaptureCell raw = oldChunk.Cells[i]; ushort next;
                            if (!occupied.TryGetValue(ProjectionDirectory.Key(raw.X, raw.Y), out next)) continue;
                            raw.X -= target.Left; raw.Y -= target.Top; CaptureCell visible = builder.View.Apply(raw);
                            ushort overlap = (ushort)(BlueprintProjectionMask.Layers(visible) & next);
                            if ((overlap & 1) != 0 && visible.ObjectId > 0) { touchedObjects.Add(visible.ObjectId); overlap &= 254; }
                            builder.AddCell(visible, overlap);
                        }
                        foreach (int id in touchedObjects)
                        {
                            if (builder.Objects.Contains(id)) continue;
                            CaptureObject obj = ProjectionObject(previous, id);
                            // The entire object is queried, including members outside
                            // viewport and candidate overlap. Only its Tile is lost.
                            foreach (long memberBlock in previous.Directory.Query(obj.X, obj.Y, checked(obj.X + obj.Width), checked(obj.Y + obj.Height), ProjectionComparison))
                            {
                                CheckProjectionCommit(request, old.Identity); CaptureChunk members = ProjectionChunk(previous, memberBlock);
                                for (int i = 0; i < members.Count; i++)
                                {
                                    CaptureCell member = members.Cells[i]; if (member.ObjectId != id) continue;
                                    member.X -= target.Left; member.Y -= target.Top; member = builder.View.Apply(member);
                                    if ((member.Layers & CaptureLayer.Tile) != 0) { builder.Removed++; builder.RemovedConstruction++; }
                                }
                                yield return true;
                            }
                            builder.Objects.Add(id);
                        }
                        yield return true;
                    }
                    yield return true;
                }
                if (builder.Removed != old.Mask.RemovedLayerCount)
                {
                    masks.Add(old.Identity.ProjectionGeneration, builder.Mask);
                    if (builder.Removed >= previous.TotalLayers) retired.Add(old.Identity.ProjectionGeneration);
                }
            }
            CheckProjectionCommit(request, allReferences: true);
            lock (gate) if (ReferenceEquals(projectionActiveCommit, request)) projectionCommitReceipt = new BlueprintProjectionCommitReceipt(request, masks, retired);
        }
        private CaptureObject ProjectionObject(ProjectionOwner owner, int id)
        {
            CaptureChunk chunk = ProjectionChunk(owner, owner.Directory.FindObject(id, ProjectionComparison)); int low = 0, high = chunk.ObjectCount - 1;
            while (low <= high) { int mid = low + (high - low) / 2; CaptureObject obj = chunk.Objects[mid]; if (obj.Id < id) low = mid + 1; else if (obj.Id > id) high = mid - 1; else return obj; }
            throw new InvalidDataException("blueprint-missing-object-reference");
        }
    }
}
