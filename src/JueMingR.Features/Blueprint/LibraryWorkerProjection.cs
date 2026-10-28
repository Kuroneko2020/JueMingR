using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private const long ProjectionCacheLimit = 8L * 1024 * 1024;
        private const long ProjectionWindowLimit = 32L * 1024 * 1024;
        private const int ProjectionRequestLimit = 32;
        private sealed class ProjectionRequest
        {
            internal BlueprintProjectionIdentity Identity;
            internal BlueprintProjectionBounds Bounds;
            internal long Generation, Serial;
        }
        private sealed class ProjectionReference
        {
            internal BlueprintProjectionIdentity Identity;
            internal bool Opened;
            internal BlueprintProjectionMask RestoreMask;
            internal bool RestoreQueued, RestoreValidated;
            internal ProjectionRequest Latest;
            internal long Serial;
        }
        private sealed class ProjectionCacheBlock
        {
            internal ProjectionOwner Owner;
            internal long Number, Bytes;
            internal CaptureChunk Chunk;
        }
        private sealed class ProjectionOwner : IDisposable
        {
            internal BlueprintProjectionIdentity Identity;
            internal LibraryRoot Root;
            internal string Name;
            internal Stream Index;
            internal bool Ready;
            internal long TotalLayers, TotalConstructionLayers;
            internal long SourceIndexBytes;
            // Exact counts admit a bounded prefix before copying payload.
            // Actual block IDs give the batch its exact bounded member union
            // without repeating per-object bbox queries. Shared numeric geometry
            // only: metadata and cell bodies remain in the decoded cache.
            internal Dictionary<int, ProjectionSourceObjectIndex> SourceObjects;
            internal IEnumerator<bool> Opening;
            internal readonly ProjectionDirectory Directory = new ProjectionDirectory();
            internal readonly List<BlueprintProjectionSourceRange> SourceRanges = new List<BlueprintProjectionSourceRange>();
            internal readonly List<BlueprintProjectionSourceRange> MatchRanges = new List<BlueprintProjectionSourceRange>();
            internal BlueprintPlacementCatalog RequestedQualificationCatalog, QualificationCatalog;
            internal IReadOnlyList<BlueprintProjectionSourceRange> QualificationSourceRanges, QualificationMatchRanges;
            internal long QualificationGeometryBytes;
            public void Dispose() { if (Opening != null) Opening.Dispose(); Opening = null; if (Index != null) Index.Dispose(); Index = null; }
        }
        private sealed class ProjectionSourceObjectIndex
        {
            internal int Members;
            internal readonly List<long> Blocks = new List<long>();
            internal BlueprintPlacementCatalog QualificationCatalog;
            internal BlueprintTargetQualification Qualification;
        }
        // One files owner and one global decoded cache serve every instance.
        // References scale with actual scene instances; queued payloads do not.
        private readonly Dictionary<BlueprintTemplateKey, int> projectionTemplateReferences = new Dictionary<BlueprintTemplateKey, int>();
        private readonly Queue<BlueprintProjectionReceipt> projectionFailures = new Queue<BlueprintProjectionReceipt>();
        private readonly Dictionary<BlueprintProjectionIdentity, ProjectionReference> projectionReferences = new Dictionary<BlueprintProjectionIdentity, ProjectionReference>();
        private readonly Dictionary<BlueprintTemplateKey, ProjectionOwner> projectionOwners = new Dictionary<BlueprintTemplateKey, ProjectionOwner>();
        private readonly Queue<BlueprintProjectionReceipt> projectionReceipts = new Queue<BlueprintProjectionReceipt>();
        private readonly List<ProjectionCacheBlock> projectionCache = new List<ProjectionCacheBlock>();
        private readonly Queue<BlueprintProjectionIdentity> projectionRequests = new Queue<BlueprintProjectionIdentity>();
        private readonly Queue<ProjectionOwner> projectionOpenings = new Queue<ProjectionOwner>();
        private ProjectionRequest projectionActiveRequest;
        private ProjectionOwner projectionReadingOwner;
        private IEnumerator<bool> projectionReading;
        private long projectionCacheSize, projectionReceiptBytes, projectionWindowSerial;
        private bool projectionWake, projectionRetireWake, projectionAwaitingReceiptSpace;
        private int projectionUnopenedReferences;
#if DEBUG
        private long projectionWindowWorkingBytes;
#endif
        private readonly HashSet<Guid> projectionUncertainAssets = new HashSet<Guid>();
#if DEBUG
        private long projectionBlockReads, projectionDecodedRecords, projectionDirectoryComparisons, projectionWindowBlocks, projectionSlices, projectionRetiredRequests, projectionCachePeak, projectionDirectoryBytes, projectionDirectoryBlocks, projectionCacheBytes, projectionSourceIndexBytes;
        internal BlueprintProjectionStatistics ProjectionStatistics
        { get { lock (gate) return new BlueprintProjectionStatistics(Interlocked.Read(ref projectionBlockReads), Interlocked.Read(ref projectionDecodedRecords), Interlocked.Read(ref projectionDirectoryComparisons), Interlocked.Read(ref projectionWindowBlocks), Interlocked.Read(ref projectionSlices), Interlocked.Read(ref projectionRetiredRequests), Interlocked.Read(ref projectionCachePeak), Interlocked.Read(ref projectionDirectoryBytes), Interlocked.Read(ref projectionDirectoryBlocks), Interlocked.Read(ref projectionCacheBytes), projectionOwners.Count, projectionReferences.Count, projectionRequests.Count + (projectionActiveRequest == null ? 0 : 1) + projectionSourceRequests.Count + (projectionSourceActive == null ? 0 : 1) + projectionQualificationRequests.Count + (projectionQualifying == null ? 0 : 1), projectionReceiptBytes, Interlocked.Read(ref projectionWindowWorkingBytes) + Interlocked.Read(ref projectionQualificationWorkingBytes), Interlocked.Read(ref projectionSourceIndexBytes)); } }
#endif
        internal bool RequestProjectionOpen(BlueprintProjectionIdentity identity, BlueprintProjectionMask restoreMask = null)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            lock (gate)
            {
                if (stopping || projectionReceipts.Count + projectionSourceReceipts.Count + projectionQualificationReceipts.Count + projectionFailures.Count >= ProjectionRequestLimit) return false;
                ProjectionReference found;
                if (projectionReferences.TryGetValue(identity, out found)) return true;
                if (projectionUnopenedReferences >= ProjectionRequestLimit) return false;
                int references; projectionTemplateReferences.TryGetValue(identity.TemplateKey, out references); projectionTemplateReferences[identity.TemplateKey] = references + 1;
                projectionReferences.Add(identity, new ProjectionReference { Identity = identity, RestoreMask = restoreMask }); projectionUnopenedReferences++; projectionWake = true; Monitor.Pulse(gate); return true;
            }
        }
        internal bool RequestProjectionWindow(BlueprintProjectionIdentity identity, long generation, BlueprintProjectionBounds bounds)
        {
            lock (gate)
            {
                ProjectionReference reference;
                if (stopping || projectionReceipts.Count + projectionSourceReceipts.Count + projectionQualificationReceipts.Count + projectionFailures.Count >= ProjectionRequestLimit || !projectionReferences.TryGetValue(identity, out reference)) return false;
                if (reference.Latest == null)
                {
                    if (projectionRequests.Count + projectionSourceRequests.Count + projectionQualificationRequests.Count + (projectionActiveRequest == null ? 0 : 1) + (projectionSourceActive == null ? 0 : 1) + (projectionQualifying == null ? 0 : 1) >= ProjectionRequestLimit) return false;
                    projectionRequests.Enqueue(identity);
                }
#if DEBUG
                else Interlocked.Increment(ref projectionRetiredRequests);
#endif
                reference.Latest = new ProjectionRequest { Identity = identity, Generation = generation, Bounds = bounds, Serial = reference.Serial = ++projectionWindowSerial };
                RemoveProjectionReceipts(identity, true); projectionWake = true; Monitor.Pulse(gate); return true;
            }
        }
        internal void ClearProjectionWindow(BlueprintProjectionIdentity identity)
        {
            lock (gate)
            {
                ProjectionReference reference;
                if (projectionReferences.TryGetValue(identity, out reference)) { reference.Serial = ++projectionWindowSerial; reference.Latest = null; }
                RemoveProjectionReceipts(identity, true); projectionWake = true; Monitor.Pulse(gate);
            }
        }
        internal void ReleaseProjection(BlueprintProjectionIdentity identity)
        { lock (gate) { RemoveProjectionReferenceLocked(identity); RemoveProjectionReceipts(identity, false); projectionWake = true; Monitor.Pulse(gate); } }
        internal void CloseProjection()
        {
            lock (gate) { projectionReferences.Clear(); projectionTemplateReferences.Clear(); projectionUnopenedReferences = 0; projectionRetireWake = true; projectionRequests.Clear(); projectionReceipts.Clear(); projectionSourceRequests.Clear(); projectionSourceSerials.Clear(); ClearProjectionSourceReceipts(); projectionFailures.Clear(); projectionQualificationLatest.Clear(); projectionQualificationRequests.Clear(); projectionQualificationReceipts.Clear(); projectionReceiptBytes = 0; projectionAwaitingReceiptSpace = false; CancelAllProjectionCommitsLocked(); projectionWake = true; Monitor.Pulse(gate); }
        }
        private void RemoveProjectionReferenceLocked(BlueprintProjectionIdentity identity)
        {
            ProjectionReference reference; if (!projectionReferences.TryGetValue(identity, out reference)) return;
            RetireProjectionQualification(identity);
            ClearProjectionSource(identity);
            if (!reference.Opened) projectionUnopenedReferences--; projectionReferences.Remove(identity);
            int count = projectionTemplateReferences[identity.TemplateKey] - 1;
            if (count == 0) projectionTemplateReferences.Remove(identity.TemplateKey); else projectionTemplateReferences[identity.TemplateKey] = count;
            projectionRetireWake = true;
        }
        private void RemoveProjectionReceipts(BlueprintProjectionIdentity identity, bool windowsOnly)
        {
            int count = projectionReceipts.Count;
            while (count-- > 0)
            {
                BlueprintProjectionReceipt item = projectionReceipts.Dequeue();
                if (identity.Equals(item.Identity) && (!windowsOnly || item.Kind == BlueprintProjectionReceiptKind.Window)) { if (item.Window != null) projectionReceiptBytes -= item.Window.EstimatedBytes; }
                else projectionReceipts.Enqueue(item);
            }
            count = projectionFailures.Count;
            while (count-- > 0)
            {
                BlueprintProjectionReceipt failure = projectionFailures.Dequeue();
                if (!identity.Equals(failure.Identity) || windowsOnly && failure.WindowGeneration == 0) projectionFailures.Enqueue(failure);
            }
            projectionAwaitingReceiptSpace = false;
        }
        internal bool TryTakeProjectionReceipt(out BlueprintProjectionReceipt value)
        {
            lock (gate)
            {
                value = projectionReceipts.Count == 0 ? null : projectionReceipts.Dequeue();
                if (value != null) { if (value.Window != null) projectionReceiptBytes -= value.Window.EstimatedBytes; projectionWake = true; projectionAwaitingReceiptSpace = false; Monitor.Pulse(gate); }
                return value != null;
            }
        }
        private bool ProjectionHasWorkLocked()
        // A release can arrive during the previous disposal batch, before that
        // step consumes projectionWake. Retirement owns an independent wake
        // condition so the last reader cannot be stranded until a new action.
        { return projectionRetireWake || projectionWake || ProjectionCommitHasWorkLocked() || !projectionAwaitingReceiptSpace && (projectionRestoring != null || projectionRestoreRequests.Count != 0 || projectionQualifying != null || projectionQualificationRequests.Count != 0 || projectionOpenings.Count != 0 || projectionReading != null || projectionRequests.Count != 0 || projectionFailures.Count != 0 || projectionSourceReading != null || projectionSourceRequests.Count != 0); }
        private bool ProjectionCurrent(ProjectionOwner owner, ProjectionRequest request = null)
        {
            lock (gate)
            {
                if (stopping) return false;
                if (request != null) return projectionReferences.ContainsKey(request.Identity);
                return projectionTemplateReferences.ContainsKey(owner.Identity.TemplateKey);
            }
        }
        private void CheckProjection(ProjectionOwner owner, ProjectionRequest request = null)
        {
            if (!ProjectionCurrent(owner, request)) throw new OperationCanceledException();
            if (request != null) lock (gate)
            {
                ProjectionReference reference = projectionReferences[request.Identity];
                // A superseding demand does not invalidate immutable blocks already
                // read. Stop only once that instance has withdrawn all window demand.
                if (reference.Serial != request.Serial && reference.Latest == null) throw new OperationCanceledException();
            }
        }
        private bool PublishProjection(ProjectionOwner owner, BlueprintProjectionReceipt receipt, ProjectionRequest request = null)
        {
            lock (gate)
            {
                if (!ProjectionCurrent(owner, request)) return true;
                if (request != null)
                {
                    ProjectionReference reference = projectionReferences[request.Identity];
                    if (reference.Serial != request.Serial) return true;
                }
                long bytes = receipt.Window == null ? 0 : receipt.Window.EstimatedBytes;
                if (projectionReceipts.Count + projectionSourceReceipts.Count + projectionQualificationReceipts.Count >= ProjectionRequestLimit || projectionReceiptBytes + bytes > BlueprintProjectionSourceReceipt.MaximumEstimatedBytes) { projectionAwaitingReceiptSpace = true; return false; }
                projectionReceipts.Enqueue(receipt); projectionReceiptBytes += bytes; return true;
            }
        }
        private void CloseProjectionWorker()
        {
            CloseProjectionSourceWorker();
            CloseProjectionQualification();
            CloseProjectionRestore();
            if (projectionReading != null) projectionReading.Dispose(); projectionReading = null; projectionActiveRequest = null; projectionReadingOwner = null;
            foreach (ProjectionOwner owner in projectionOwners.Values) owner.Dispose(); projectionOwners.Clear(); projectionOpenings.Clear();
            projectionCache.Clear(); projectionCacheSize = 0; UpdateProjectionWindowWorkingBytes(0); CloseProjectionCommitWorker(); UpdateProjectionStatistics();
        }
        private void RetireProjectionAsset(Guid id)
        {
            lock (gate)
            {
                var retired = new List<BlueprintProjectionIdentity>(); foreach (BlueprintProjectionIdentity identity in projectionReferences.Keys) if (identity.AssetId == id) retired.Add(identity);
                foreach (BlueprintProjectionIdentity identity in retired) { RemoveProjectionReferenceLocked(identity); RemoveProjectionReceipts(identity, false); }
                RetireProjectionCommitAssetLocked(id);
            }
            ReleaseRetiredProjection();
        }
        private void ReleaseRetiredProjection()
        {
            lock (gate) { if (!projectionRetireWake) return; projectionRetireWake = false; }
            var retired = new List<BlueprintTemplateKey>();
            foreach (KeyValuePair<BlueprintTemplateKey, ProjectionOwner> pair in projectionOwners) if (!ProjectionCurrent(pair.Value)) retired.Add(pair.Key);
            foreach (BlueprintTemplateKey key in retired)
            {
                ProjectionOwner owner = projectionOwners[key];
                if (ReferenceEquals(owner, projectionReadingOwner)) { projectionReading.Dispose(); projectionReading = null; projectionActiveRequest = null; projectionReadingOwner = null; }
                RetireProjectionSourceOwner(owner); owner.Dispose(); projectionOwners.Remove(key);
                for (int i = projectionCache.Count - 1; i >= 0; i--) if (ReferenceEquals(projectionCache[i].Owner, owner)) { projectionCacheSize -= projectionCache[i].Bytes; projectionCache.RemoveAt(i); }
            }
            UpdateProjectionStatistics();
        }
        private int projectionLane;
        private void ProjectionStep()
        {
            try
            {
                ReleaseRetiredProjection();
                lock (gate)
                {
                    while (projectionFailures.Count != 0 && projectionReceipts.Count + projectionSourceReceipts.Count + projectionQualificationReceipts.Count < ProjectionRequestLimit)
                    { BlueprintProjectionReceipt failure = projectionFailures.Dequeue(); projectionReceipts.Enqueue(failure); }
                    if (projectionFailures.Count != 0) projectionAwaitingReceiptSpace = true;
                }
                lock (gate)
                {
                    if (projectionWake)
                    {
                        projectionWake = false;
                        foreach (ProjectionReference reference in projectionReferences.Values)
                        {
                            ProjectionOwner owner;
                            if (!projectionOwners.TryGetValue(reference.Identity.TemplateKey, out owner))
                            {
                                owner = new ProjectionOwner { Identity = reference.Identity }; projectionOwners.Add(reference.Identity.TemplateKey, owner);
                                owner.Opening = OpenProjection(owner).GetEnumerator(); projectionOpenings.Enqueue(owner);
                            }
                            if (owner.Ready && !reference.Opened)
                            {
                                if (reference.RestoreMask != null && !reference.RestoreValidated)
                                { if (!reference.RestoreQueued) { reference.RestoreQueued = true; projectionRestoreRequests.Enqueue(reference); } continue; }
                                if (PublishProjection(owner, new BlueprintProjectionReceipt(BlueprintProjectionReceiptKind.Opened, reference.Identity, summary: owner.Root.Summary, totalLayerCount: owner.TotalLayers, sourceRanges: owner.SourceRanges.AsReadOnly(), matchRanges: owner.MatchRanges.AsReadOnly(), totalConstructionLayerCount: owner.TotalConstructionLayers, name: owner.Name))) { reference.Opened = true; projectionUnopenedReferences--; }
                            }
                        }
                    }
                }
                if (!opened && rootFailure == null) { try { files.Open(); opened = true; } catch (Exception e) { rootFailure = Reason(e); } }
                if (rootFailure != null) { var failedOwners = new List<ProjectionOwner>(projectionOwners.Values); foreach (ProjectionOwner failedOwner in failedOwners) FailProjection(failedOwner, null, new IOException(rootFailure)); return; }
#if DEBUG
                Interlocked.Increment(ref projectionSlices);
#endif
                // Open, viewport, overlap and source matching receive cooperative turns;
                // accepted library commands retain priority in the single Run loop.
                projectionLane = (projectionLane + 1) % 4;
                if (ProjectionCommitStep(projectionLane == 0)) return;
                if (ProjectionQualificationStep(projectionLane == 2)) return;
                if (ProjectionSourceStep(projectionLane == 3)) return;
                if (ProjectionRestoreStep(projectionLane == 1)) return;
                if (projectionOpenings.Count != 0 && (projectionLane == 1 || projectionReading == null && projectionRequests.Count == 0))
                {
                    ProjectionOwner owner = projectionOpenings.Dequeue();
                    if (!ProjectionCurrent(owner) || owner.Opening == null) return;
                    try
                    {
                        if (owner.Opening.MoveNext()) projectionOpenings.Enqueue(owner);
                        else { owner.Opening.Dispose(); owner.Opening = null; owner.Ready = true; lock (gate) projectionWake = true; }
                    }
                    catch (Exception error) { FailProjection(owner, null, error); }
                    return;
                }
                if (projectionReading == null)
                {
                    lock (gate)
                    {
                        int pendingCount = projectionRequests.Count;
                        while (pendingCount-- > 0)
                        {
                            BlueprintProjectionIdentity identity = projectionRequests.Dequeue(); ProjectionReference reference; ProjectionOwner owner;
                            if (!projectionReferences.TryGetValue(identity, out reference) || reference.Latest == null) continue;
                            if (!reference.Opened || !projectionOwners.TryGetValue(identity.TemplateKey, out owner) || !owner.Ready) { projectionRequests.Enqueue(identity); continue; }
                            projectionActiveRequest = reference.Latest; reference.Latest = null; projectionReadingOwner = owner;
                            projectionReading = ReadProjectionWindow(owner, projectionActiveRequest).GetEnumerator(); break;
                        }
                    }
                }
                if (projectionReading != null)
                {
                    try
                    {
                        if (!projectionReading.MoveNext()) { projectionReading.Dispose(); projectionReading = null; projectionActiveRequest = null; projectionReadingOwner = null; UpdateProjectionWindowWorkingBytes(0); }
                    }
                    catch (Exception error) { ProjectionOwner owner = projectionReadingOwner; ProjectionRequest failed = projectionActiveRequest; projectionReading.Dispose(); projectionReading = null; projectionActiveRequest = null; projectionReadingOwner = null; UpdateProjectionWindowWorkingBytes(0); FailProjection(owner, failed, error); }
                }
            }
            finally { files.CloseMetadata(); }
        }
        private void FailProjection(ProjectionOwner owner, ProjectionRequest request, Exception error)
        {
            if (error is OperationCanceledException) { return; }
            lock (gate)
            {
                if (request != null)
                {
                    ProjectionReference reference;
                    if (!projectionReferences.TryGetValue(request.Identity, out reference) || reference.Serial != request.Serial) return;
                    bool retired = Code(error) != "blueprint-projection-window-budget";
                    projectionFailures.Enqueue(new BlueprintProjectionReceipt(BlueprintProjectionReceiptKind.Failed, request.Identity, request.Generation, error: retired ? Reason(error) : "投影窗口需分批读取", failureCode: Code(error), identityRetired: retired));
                    // Budget pressure is a demand-level retry, never loss of a
                    // scene instance or of its validated shared template.
                    if (retired) RemoveProjectionReferenceLocked(request.Identity);
                }
                else
                {
                    var affected = new List<BlueprintProjectionIdentity>(); foreach (ProjectionReference reference in projectionReferences.Values) if (reference.Identity.TemplateKey.Equals(owner.Identity.TemplateKey)) affected.Add(reference.Identity);
                    foreach (BlueprintProjectionIdentity identity in affected) { projectionFailures.Enqueue(new BlueprintProjectionReceipt(BlueprintProjectionReceiptKind.Failed, identity, error: Reason(error), failureCode: Code(error), identityRetired: true)); RemoveProjectionReferenceLocked(identity); }
                }
            }
            ReleaseRetiredProjection();
        }
        private static BlueprintProjectionBounds ProjectionSourceObjectBounds(CaptureObject obj,CaptureSummary summary)
        {
            int left=obj.X-summary.Left,top=obj.Y-summary.Top,width=obj.Width;
            if(obj.Type==10 || obj.Type==11)
            {
                // Locked original open-door metadata records PlaceLeft=1 /
                // OriginX=1 or PlaceRight=2 / OriginX=0. Its canonical hinge
                // has three possible columns, not sourceWidth+two wings:
                // a two-column source already contains one of those wings.
                // Unknown/inconsistent metadata keeps the conservative extent.
                if(obj.Reliable && obj.Type==11 && obj.Width==2 && obj.Height==3 &&
                    (obj.Direction==1 && obj.OriginX==1 || obj.Direction==2 && obj.OriginX==0))
                    return new BlueprintProjectionBounds(left+(obj.Direction==1?1:0)-1,top,3,3);
                left--;width+=2;
            }
            return new BlueprintProjectionBounds(left,top,width,obj.Height);
        }
        private IEnumerable<bool> OpenProjection(ProjectionOwner owner)
        {
            CheckProjection(owner);
            if (projectionUncertainAssets.Contains(owner.Identity.AssetId)) throw new InvalidDataException("blueprint-projection-delete-unconfirmed");
            PreferenceReadResult read = files.OpenMetadata(owner.Identity.AssetId);
            if (read.Status != PreferenceReadStatus.Loaded) throw new InvalidDataException("blueprint-projection-metadata");
            LibraryMetadata metadata = LibraryCodec.DecodeMetadata(read.Contents, owner.Identity.AssetId);
            if (metadata.Deleted || metadata.ContentRevision != owner.Identity.ContentRevision || metadata.ContentHash != owner.Identity.ContentHash) throw new InvalidDataException("blueprint-projection-identity");
            owner.Name = metadata.Name; owner.Root = ReadAssetRoot(metadata); files.CloseMetadata();
            IBlueprintLibraryLayoutSteps layout = files as IBlueprintLibraryLayoutSteps;
            if (layout != null) foreach (int ignored in layout.ValidateLayoutSteps(owner.Identity.AssetId, false, owner.Root.BlockCount, 32)) { CheckProjection(owner); yield return true; }
            else { files.ValidateLayout(owner.Identity.AssetId, false, owner.Root.BlockCount); yield return true; }
            CheckProjection(owner); owner.Index = files.OpenIndex(owner.Identity.AssetId, false);
            if (owner.Index.Length != checked(owner.Root.BlockCount * 32L)) throw new InvalidDataException("blueprint-index-hash");
            using (SHA256 hash = SHA256.Create())
            {
                byte[] buffer = new byte[32768]; int count;
                while ((count = owner.Index.Read(buffer, 0, buffer.Length)) != 0) { hash.TransformBlock(buffer, 0, count, buffer, 0); CheckProjection(owner); yield return true; }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                if (!LibraryCodec.Equal(hash.Hash, owner.Root.IndexHash)) throw new InvalidDataException("blueprint-index-hash");
            }
            // Keep the old complete v1 proof, including member references and
            // rectangular object completeness. Its cost can exceed O(N); only
            // directory construction itself is one streaming O(N+B) pass.
            long previous = -1, totalCells = 0, unreliable = 0; int previousObject = 0; bool sawObjects = false;
            var indexedMatchObjects = new HashSet<int>();
            using (var objects = new ObjectLookup(this, owner.Identity.AssetId, false, owner.Root.BlockCount, owner.Index, number => ProjectionChunk(owner, number)))
            {
                for (long number = 0; number < owner.Root.BlockCount; number++)
                {
                    CheckProjection(owner); CaptureChunk chunk = ProjectionChunk(owner, number); CaptureSummary summary = owner.Root.Summary;
                    int sourceLeft = Int32.MaxValue, sourceTop = Int32.MaxValue, sourceRight = 0, sourceBottom = 0; long sourceLayers = 0;
                    var coordinates = new long[chunk.Count]; var sourceBits = new ushort[chunk.Count]; var sourceIds = new int[chunk.Count]; var sourceObjects = new Dictionary<int, CaptureObject>(); var memberCounts = new Dictionary<int, int>();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        CaptureCell cell = chunk.Cells[i]; long key = Key(cell);
                        if (sawObjects || key <= previous || cell.X < summary.Left || cell.Y < summary.Top || (long)cell.X >= (long)summary.Left + summary.Width || (long)cell.Y >= (long)summary.Top + summary.Height) throw new InvalidDataException("blueprint-cell-order-bounds-or-duplicate");
                        previous = key; int layers = BlueprintProjectionMask.Count(BlueprintProjectionMask.Layers(cell)); owner.TotalLayers += layers; sourceLayers += layers;
                        // This non-liquid upper bound preserves the display-open
                        // transport. Only the later catalog+mask qualification
                        // receipt is authoritative for construction completion.
                        owner.TotalConstructionLayers += BlueprintConstructionTargets.Count(cell);
                        sourceLeft = Math.Min(sourceLeft, cell.X); sourceTop = Math.Min(sourceTop, cell.Y); sourceRight = Math.Max(sourceRight, cell.X + 1); sourceBottom = Math.Max(sourceBottom, cell.Y + 1);
                        coordinates[i] = ((long)(cell.Y - summary.Top) << 32) | (uint)(cell.X - summary.Left);
                        sourceBits[i] = BlueprintProjectionMask.Layers(cell); sourceIds[i] = cell.ObjectId;
                        if (cell.ObjectId > 0)
                        {
                            if (owner.SourceObjects == null) { owner.SourceObjects = new Dictionary<int, ProjectionSourceObjectIndex>(); owner.SourceIndexBytes += 128; }
                            ProjectionSourceObjectIndex sourceObject;
                            if (!owner.SourceObjects.TryGetValue(cell.ObjectId, out sourceObject))
                            { owner.SourceObjects.Add(cell.ObjectId, sourceObject = new ProjectionSourceObjectIndex()); owner.SourceIndexBytes += 256; }
                            sourceObject.Members = checked(sourceObject.Members + 1);
                            if (sourceObject.Blocks.Count == 0 || sourceObject.Blocks[sourceObject.Blocks.Count - 1] != number)
                            {
                                int capacity = sourceObject.Blocks.Capacity; sourceObject.Blocks.Add(number);
                                owner.SourceIndexBytes += (sourceObject.Blocks.Capacity - capacity) * 8L;
                            }
                            CaptureObject obj = objects.Find(cell.ObjectId);
                            int members; memberCounts.TryGetValue(obj.Id, out members); memberCounts[obj.Id] = members + 1; if (members == 0) sourceObjects.Add(obj.Id, obj);
                            sourceLeft = Math.Min(sourceLeft, obj.X); sourceTop = Math.Min(sourceTop, obj.Y); sourceRight = Math.Max(sourceRight, checked(obj.X + obj.Width)); sourceBottom = Math.Max(sourceBottom, checked(obj.Y + obj.Height));
                            if (obj.Type != cell.TileType || obj.Reliable == cell.UnreliableObject || cell.X < obj.X || cell.Y < obj.Y || (long)cell.X >= (long)obj.X + obj.Width || (long)cell.Y >= (long)obj.Y + obj.Height) throw new InvalidDataException("blueprint-object-member-reference");
                        }
                        if ((i & 31) == 31) { CheckProjection(owner); yield return true; }
                    }
                    totalCells = checked(totalCells + chunk.Count);
                    for (int i = 0; i < chunk.ObjectCount; i++)
                    {
                        sawObjects = true; CaptureObject obj = chunk.Objects[i];
                        if (obj.Id <= previousObject) throw new InvalidDataException("blueprint-object-order-or-duplicate");
                        previousObject = obj.Id; if (!obj.Reliable) unreliable++;
                        else foreach (bool ignored in objects.CheckCompleteSteps(obj)) { CheckProjection(owner); yield return true; }
                        CheckProjection(owner); yield return true;
                    }
                    owner.Directory.Add(number, chunk);
                    if (chunk.Count != 0)
                    {
                        var dependencies = new List<BlueprintProjectionSourceDependency>();
                        foreach (var pair in sourceObjects)
                        {
                            var obj = pair.Value; bool door = obj.Type == 10 || obj.Type == 11;
                            // Ordinary complete members are already indexed.
                            // Cross-block groups need the entire footprint;
                            // doors also depend on the other legal wing column.
                            if (door || memberCounts[obj.Id] != checked(obj.Width * obj.Height))
                                dependencies.Add(new BlueprintProjectionSourceDependency(ProjectionSourceObjectBounds(obj,summary),obj.Id));
                        }
                        int group = owner.SourceRanges.Count;
                        var range = new BlueprintProjectionSourceRange(number,
                            new BlueprintProjectionBounds(sourceLeft - summary.Left - 1, sourceTop - summary.Top - 1, sourceRight - sourceLeft + 2, sourceBottom - sourceTop + 2), sourceLayers, coordinates, sourceBits, sourceIds, dependencies, group);
                        if (sourceObjects.Count == 0) owner.MatchRanges.Add(range);
                        else
                        {
                            // Keep native observation at physical-block scale.
                            // Independent object Tile proofs stop a legitimate
                            // operating-frame change from restarting the same
                            // block's ordinary Tile/wall/circuit proof. Shared
                            // indices contain geometry only, never world values.
                            var ordinaryPositions = new List<long>(); var ordinaryBits = new List<ushort>(); var ordinaryIds = new List<int>(); long ordinaryTargets = 0;
                            int proofStart=owner.MatchRanges.Count; BlueprintProjectionSourceRange? ordinaryProof=null; var routeObjects=new List<int>();var routeOrdinals=new List<int>();
                            int left = Int32.MaxValue, top = Int32.MaxValue, right = 0, bottom = 0;
                            for (int i = 0; i < coordinates.Length; i++)
                            {
                                ushort bits = (ushort)(sourceBits[i] & (sourceIds[i] > 0 ? 0xfe : 0xff)); if (bits == 0) continue;
                                ordinaryPositions.Add(coordinates[i]); ordinaryBits.Add(bits); ordinaryIds.Add(0); ordinaryTargets += BlueprintProjectionMask.Count(bits);
                                int x = (int)(uint)coordinates[i], y = (int)(coordinates[i] >> 32);
                                left = Math.Min(left,x); top = Math.Min(top,y); right = Math.Max(right,x+1); bottom = Math.Max(bottom,y+1);
                                if ((i & 31) == 31) { CheckProjection(owner); yield return true; }
                            }
                            if (ordinaryPositions.Count != 0)
                            {
                                var ordinary = new BlueprintProjectionSourceRange(number, new BlueprintProjectionBounds(left,top,right-left,bottom-top), ordinaryTargets,
                                    ordinaryPositions.ToArray(),ordinaryBits.ToArray(),ordinaryIds.ToArray(),Array.Empty<BlueprintProjectionSourceDependency>(),group);
                                owner.MatchRanges.Add(ordinary); owner.SourceIndexBytes += ordinary.SourceIndexBytes;
                                ordinaryProof=ordinary;
                            }
                            foreach (var pair in sourceObjects) if (indexedMatchObjects.Add(pair.Key))
                            {
                                var obj = pair.Value; int x = obj.X-summary.Left, y = obj.Y-summary.Top;
                                var bounds = ProjectionSourceObjectBounds(obj,summary);
                                var proof = new BlueprintProjectionSourceRange(number,bounds,(long)obj.Width*obj.Height,
                                    new[]{((long)y<<32)|(uint)x},new ushort[]{1},new[]{obj.Id},new[]{new BlueprintProjectionSourceDependency(bounds,obj.Id)},group,obj.Id);
                                routeObjects.Add(obj.Id);routeOrdinals.Add(owner.MatchRanges.Count-proofStart);
                                owner.MatchRanges.Add(proof); owner.SourceIndexBytes += proof.SourceIndexBytes;
                            }
                            range=range.WithRouting(ordinaryProof,routeObjects.ToArray(),routeOrdinals.ToArray());
                        }
                        owner.SourceRanges.Add(range); owner.SourceIndexBytes += range.SourceIndexBytes;
                    }
#if DEBUG
                    UpdateProjectionStatistics();
#endif
                    yield return true;
                }
            }
            if (totalCells != owner.Root.Summary.ContentCount || unreliable != owner.Root.Summary.UnreliableObjectCount) throw new InvalidDataException("blueprint-content-count");
            CheckProjection(owner);
            // The shared template becomes ready once; each live reference gets its own Opened receipt.
        }
        private IEnumerable<bool> ReadProjectionWindow(ProjectionOwner owner, ProjectionRequest request)
        {
            CheckProjection(owner, request); CaptureSummary summary = owner.Root.Summary; BlueprintProjectionBounds bounds = request.Bounds;
            int left = Math.Min(summary.Width, Math.Max(0, bounds.Left)), top = Math.Min(summary.Height, Math.Max(0, bounds.Top));
            int right = Math.Max(left, Math.Min(summary.Width, bounds.Right)), bottom = Math.Max(top, Math.Min(summary.Height, bounds.Bottom));
            // Payload pressure asks Host to tile a large viewport; it never
            // imposes a template width/height or instance-count restriction.
            long area = checked((long)(right - left) * (bottom - top));
            var cells = new List<CaptureCell>(); var objectIds = new HashSet<int>(); long windowBytes = 256;
            if (left < right && top < bottom)
            {
                foreach (long number in ProjectionBlocks(owner, checked(left + summary.Left), checked(top + summary.Top), checked(right + summary.Left), checked(bottom + summary.Top)))
                {
                    CheckProjection(owner, request); CaptureChunk chunk = ProjectionChunk(owner, number);
#if DEBUG
                    Interlocked.Increment(ref projectionWindowBlocks);
#endif
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        CaptureCell cell = chunk.Cells[i]; cell.X -= summary.Left; cell.Y -= summary.Top;
                        if (!bounds.Contains(cell.X, cell.Y)) continue;
                        if ((long)cells.Count >= area) throw new InvalidDataException("blueprint-projection-window-duplicate");
                        windowBytes += 64; if (windowBytes > ProjectionWindowLimit) throw new InvalidDataException("blueprint-projection-window-budget");
                        cells.Add(cell); if (cell.ObjectId != 0) objectIds.Add(cell.ObjectId);
                    }
                    UpdateProjectionWindowWorkingBytes(256L + cells.Count * 64L + objectIds.Count * 32L);
                    yield return true;
                }
            }
            var objects = new List<CaptureObject>();
            foreach (int id in objectIds)
            {
                CheckProjection(owner, request); CaptureChunk chunk = ProjectionChunk(owner, owner.Directory.FindObject(id, ProjectionComparison));
                int low = 0, high = chunk.ObjectCount - 1; bool found = false;
                while (low <= high)
                {
                    int mid = low + (high - low) / 2; CaptureObject obj = chunk.Objects[mid];
                    if (obj.Id < id) low = mid + 1; else if (obj.Id > id) high = mid - 1;
                    else
                    {
                        windowBytes += 128L + (long)((obj.Reason ?? "").Length + (obj.MissingPayload ?? "").Length) * 2;
                        if (windowBytes > ProjectionWindowLimit) throw new InvalidDataException("blueprint-projection-window-budget");
                        obj.X -= summary.Left; obj.Y -= summary.Top; objects.Add(obj); found = true; break;
                    }
                }
                if (!found) throw new InvalidDataException("blueprint-missing-object-reference");
                yield return true;
            }
            CheckProjection(owner, request);
            cells.Sort(CellComparer.Instance);
            var window = new BlueprintProjectionWindow(request.Identity, request.Generation, summary, bounds, cells.ToArray(), objects.ToArray(), true);
            if (window.EstimatedBytes > ProjectionWindowLimit) throw new InvalidDataException("blueprint-projection-window-budget");
            while (!PublishProjection(owner, new BlueprintProjectionReceipt(BlueprintProjectionReceiptKind.Window, request.Identity, request.Generation, summary, window), request)) { CheckProjection(owner, request); yield return true; }
        }
        private IEnumerable<long> ProjectionBlocks(ProjectionOwner owner, int left, int top, int right, int bottom)
        {
            var misses = new List<long>();
            foreach (long number in owner.Directory.Query(left, top, right, bottom, ProjectionComparison))
            { if (FindProjectionBlock(owner, number) != null) yield return number; else misses.Add(number); }
            foreach (long number in misses) yield return number;
        }
        private ProjectionCacheBlock FindProjectionBlock(ProjectionOwner owner, long number)
        { for (int i = 0; i < projectionCache.Count; i++) if (ReferenceEquals(projectionCache[i].Owner, owner) && projectionCache[i].Number == number) return projectionCache[i]; return null; }
        private CaptureChunk ProjectionChunk(ProjectionOwner owner, long number)
        {
            ProjectionCacheBlock hit = FindProjectionBlock(owner, number);
            if (hit != null) { projectionCache.Remove(hit); projectionCache.Add(hit); return hit.Chunk; }
            owner.Index.Position = checked(number * 32L); byte[] expected = new byte[32]; ReadExactly(owner.Index, expected);
            byte[] bytes = files.ReadBlock(owner.Identity.AssetId, false, number);
            if (!LibraryCodec.Equal(LibraryCodec.Hash(bytes), expected)) throw new InvalidDataException("blueprint-chunk-hash");
            CaptureChunk chunk = LibraryCodec.DecodeChunk(bytes);
#if DEBUG
            Interlocked.Increment(ref projectionBlockReads); Interlocked.Add(ref projectionDecodedRecords, chunk.Count + chunk.ObjectCount);
#endif
            long retained = 128L + chunk.Count * 64L + chunk.ObjectCount * 128L;
            for (int i = 0; i < chunk.ObjectCount; i++) retained += (long)((chunk.Objects[i].Reason ?? "").Length + (chunk.Objects[i].MissingPayload ?? "").Length) * 2;
            while (projectionCache.Count != 0 && (projectionCache.Count >= 64 || projectionCacheSize + retained > ProjectionCacheLimit)) { projectionCacheSize -= projectionCache[0].Bytes; projectionCache.RemoveAt(0); }
            if (retained <= ProjectionCacheLimit) { projectionCache.Add(new ProjectionCacheBlock { Owner = owner, Number = number, Bytes = retained, Chunk = chunk }); projectionCacheSize += retained; }
            UpdateProjectionStatistics(); return chunk;
        }
        private void UpdateProjectionStatistics()
        {
#if DEBUG
            long directoryBytes = 0, blocks = 0, sourceIndexBytes = 0; foreach (ProjectionOwner owner in projectionOwners.Values) { directoryBytes += owner.Directory.EstimatedBytes + 128L + (owner.SourceRanges.Capacity + owner.MatchRanges.Capacity) * 48L; sourceIndexBytes += owner.SourceIndexBytes + owner.QualificationGeometryBytes; blocks += owner.Directory.CellBlockCount + owner.Directory.ObjectBlockCount; }
            Interlocked.Exchange(ref projectionSourceIndexBytes, sourceIndexBytes);
            Interlocked.Exchange(ref projectionDirectoryBytes, directoryBytes); Interlocked.Exchange(ref projectionDirectoryBlocks, blocks);
            Interlocked.Exchange(ref projectionCacheBytes, projectionCacheSize);
            if (projectionCacheSize > Interlocked.Read(ref projectionCachePeak)) Interlocked.Exchange(ref projectionCachePeak, projectionCacheSize);
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        private void UpdateProjectionWindowWorkingBytes(long bytes)
        {
#if DEBUG
            Interlocked.Exchange(ref projectionWindowWorkingBytes, bytes);
#endif
        }
        private void ProjectionComparison()
        {
#if DEBUG
            Interlocked.Increment(ref projectionDirectoryComparisons);
#endif
        }
    }
}
