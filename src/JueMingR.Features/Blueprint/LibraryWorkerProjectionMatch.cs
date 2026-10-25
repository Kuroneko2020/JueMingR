using System;
using System.Collections.Generic;
using System.Threading;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private sealed class ProjectionSourceRequest
        {
            internal BlueprintProjectionIdentity Identity;
            internal long Generation, Serial;
            internal int Range, ObjectId, Offset;
        }
        private readonly Queue<ProjectionSourceRequest> projectionSourceRequests = new Queue<ProjectionSourceRequest>();
        private readonly Dictionary<BlueprintProjectionIdentity, long> projectionSourceSerials = new Dictionary<BlueprintProjectionIdentity, long>();
        private readonly Queue<BlueprintProjectionSourceReceipt> projectionSourceReceipts = new Queue<BlueprintProjectionSourceReceipt>();
        private ProjectionSourceRequest projectionSourceActive;
        private ProjectionOwner projectionSourceOwner;
        private IEnumerator<bool> projectionSourceReading;
        private long projectionSourceSerial;

        internal bool RequestProjectionSource(BlueprintProjectionIdentity identity, long generation, int range, int objectId, int offset)
        {
            lock (gate)
            {
                if (stopping || !projectionReferences.ContainsKey(identity) || projectionSourceSerials.ContainsKey(identity) ||
                    projectionRequests.Count + projectionSourceRequests.Count + (projectionActiveRequest == null ? 0 : 1) + (projectionSourceActive == null ? 0 : 1) >= ProjectionRequestLimit ||
                    projectionReceipts.Count + projectionSourceReceipts.Count + projectionFailures.Count >= ProjectionRequestLimit) return false;
                var request = new ProjectionSourceRequest { Identity = identity, Generation = generation, Range = range, ObjectId = objectId, Offset = offset, Serial = ++projectionSourceSerial };
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
                while (count-- > 0) { var next = projectionSourceReceipts.Dequeue(); if (next.Identity.Equals(identity)) projectionReceiptBytes -= next.Window?.EstimatedBytes ?? 0; else projectionSourceReceipts.Enqueue(next); }
                projectionAwaitingReceiptSpace = false; projectionWake = true; Monitor.Pulse(gate);
            }
        }
        internal bool TryTakeProjectionSource(out BlueprintProjectionSourceReceipt receipt)
        {
            lock (gate)
            {
                receipt = projectionSourceReceipts.Count == 0 ? null : projectionSourceReceipts.Dequeue();
                if (receipt == null) return false;
                projectionReceiptBytes -= receipt.Window?.EstimatedBytes ?? 0; projectionAwaitingReceiptSpace = false; projectionWake = true; Monitor.Pulse(gate); return true;
            }
        }
        private bool ProjectionSourceCurrent(ProjectionOwner owner, ProjectionSourceRequest request)
        {
            lock (gate)
            {
                long serial;
                return !stopping && projectionReferences.ContainsKey(request.Identity) && projectionSourceSerials.TryGetValue(request.Identity, out serial) && serial == request.Serial && ProjectionCurrent(owner);
            }
        }
        private void CheckProjectionSource(ProjectionOwner owner, ProjectionSourceRequest request)
        { if (!ProjectionSourceCurrent(owner, request)) throw new OperationCanceledException(); }
        private void CloseProjectionSourceWorker()
        {
            projectionSourceReading?.Dispose(); projectionSourceReading = null; projectionSourceActive = null; projectionSourceOwner = null;
            lock (gate) { projectionSourceRequests.Clear(); projectionSourceSerials.Clear(); projectionSourceReceipts.Clear(); }
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
            if (request.Range < 0 || request.Range >= owner.MatchRanges.Count) throw new System.IO.InvalidDataException("blueprint-source-range");
            var range = owner.MatchRanges[request.Range]; var summary = owner.Root.Summary;
            var cells = new List<CaptureCell>(); var metadata = new List<CaptureObject>(); var ids = new HashSet<int>();
            BlueprintProjectionBounds bounds = range.Bounds;
            int nextOffset = 0; bool complete = true;
            bool objectRange = request.ObjectId == 0 && range.ObjectId > 0;
            if (request.ObjectId == 0 && !objectRange)
            {
                CaptureChunk chunk = ProjectionChunk(owner, range.Block);
                if (request.Offset < 0 || request.Offset >= chunk.Count) throw new System.IO.InvalidDataException("blueprint-source-offset");
                nextOffset = Math.Min(chunk.Count, request.Offset + 128); complete = nextOffset == chunk.Count;
                for (int i = request.Offset; i < nextOffset; i++)
                {
                    var cell = chunk.Cells[i]; if (cell.ObjectId > 0) cell = cell.WithoutTile();
                    if (!cell.HasContent) continue; cell.X -= summary.Left; cell.Y -= summary.Top; cells.Add(cell);
                }
                yield return true;
            }
            else ids.Add(objectRange ? range.ObjectId : request.ObjectId);
            foreach (int id in ids)
            {
                CheckProjectionSource(owner, request); var chunk = ProjectionChunk(owner, owner.Directory.FindObject(id, ProjectionComparison)); CaptureObject found = default(CaptureObject); bool have = false;
                for (int i = 0; i < chunk.ObjectCount; i++) if (chunk.Objects[i].Id == id) { found = chunk.Objects[i]; have = true; break; }
                if (!have) throw new System.IO.InvalidDataException("blueprint-missing-object-reference");
                if (request.ObjectId > 0 || objectRange)
                {
                    bounds = new BlueprintProjectionBounds(found.X - summary.Left, found.Y - summary.Top, found.Width, found.Height);
                    foreach (long number in ProjectionBlocks(owner, found.X, found.Y, checked(found.X + found.Width), checked(found.Y + found.Height)))
                    {
                        CheckProjectionSource(owner, request); var members = ProjectionChunk(owner, number);
                        for (int i = 0; i < members.Count; i++) if (members.Cells[i].ObjectId == id)
                        {
                            var cell = members.Cells[i]; if (objectRange) cell.Layers = CaptureLayer.Tile;
                            cell.X -= summary.Left; cell.Y -= summary.Top; cells.Add(cell);
                            if(objectRange)break;
                        }
                        yield return true;
                        // A logical object's primary source slice needs only
                        // its anchor member. The existing positive ObjectId
                        // request transfers its one complete target workset;
                        // never retain two complete furniture payloads.
                        if(objectRange && cells.Count!=0)break;
                    }
                }
                found.X -= summary.Left; found.Y -= summary.Top; metadata.Add(found); yield return true;
            }
            cells.Sort(CellComparer.Instance);
            var window = new BlueprintProjectionWindow(request.Identity, request.Generation, summary, bounds, cells.ToArray(), metadata.ToArray(), true);
            if (window.EstimatedBytes > ProjectionWindowLimit) throw new System.IO.InvalidDataException("blueprint-projection-window-budget");
            var receipt = new BlueprintProjectionSourceReceipt(request.Identity, request.Generation, request.Range, request.ObjectId, window, request.Offset, nextOffset, complete);
            while (true)
            {
                CheckProjectionSource(owner, request);
                lock (gate)
                {
                    if (projectionReceipts.Count + projectionSourceReceipts.Count < ProjectionRequestLimit && projectionReceiptBytes + window.EstimatedBytes <= ProjectionWindowLimit)
                    { projectionSourceReceipts.Enqueue(receipt); projectionReceiptBytes += window.EstimatedBytes; projectionSourceSerials.Remove(request.Identity); break; }
                    projectionAwaitingReceiptSpace = true;
                }
                yield return true;
            }
        }
    }
}
