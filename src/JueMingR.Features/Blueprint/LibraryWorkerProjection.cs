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
        private sealed class ProjectionRequest
        {
            internal BlueprintProjectionIdentity Identity;
            internal BlueprintProjectionBounds Bounds;
            internal long Generation, Serial;
        }
        private sealed class ProjectionCacheBlock
        {
            internal long Number, Bytes;
            internal CaptureChunk Chunk;
        }
        private sealed class ProjectionOwner : IDisposable
        {
            internal BlueprintProjectionIdentity Identity;
            internal long Epoch, CacheBytes;
            internal LibraryRoot Root;
            internal Stream Index;
            internal readonly ProjectionDirectory Directory = new ProjectionDirectory();
            internal readonly List<ProjectionCacheBlock> Cache = new List<ProjectionCacheBlock>();
            public void Dispose() { if (Index != null) Index.Dispose(); Index = null; Cache.Clear(); CacheBytes = 0; }
        }
        // These mailboxes are separate from the accepted capture/library slot.
        // Replacing or closing a projection cannot cancel an accepted save.
        private BlueprintProjectionIdentity projectionDesired;
        private BlueprintProjectionReceipt projectionReceipt;
        private ProjectionRequest projectionLatest;
        private ProjectionRequest projectionActiveRequest;
        private ProjectionOwner projectionOwner;
        private IEnumerator<bool> projectionOpening, projectionReading;
        private long projectionEpoch, projectionWindowSerial;
        private bool projectionWake;
        private readonly HashSet<Guid> projectionUncertainAssets = new HashSet<Guid>();
#if DEBUG
        private long projectionBlockReads, projectionDecodedRecords, projectionDirectoryComparisons, projectionWindowBlocks, projectionSlices, projectionRetiredRequests, projectionCachePeak, projectionDirectoryBytes, projectionDirectoryBlocks, projectionCacheBytes;
        internal BlueprintProjectionStatistics ProjectionStatistics
        {
            get
            {
                return new BlueprintProjectionStatistics(Interlocked.Read(ref projectionBlockReads), Interlocked.Read(ref projectionDecodedRecords), Interlocked.Read(ref projectionDirectoryComparisons), Interlocked.Read(ref projectionWindowBlocks), Interlocked.Read(ref projectionSlices), Interlocked.Read(ref projectionRetiredRequests), Interlocked.Read(ref projectionCachePeak), Interlocked.Read(ref projectionDirectoryBytes), Interlocked.Read(ref projectionDirectoryBlocks), Interlocked.Read(ref projectionCacheBytes));
            }
        }
#endif
        internal bool RequestProjectionOpen(BlueprintProjectionIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            lock (gate)
            {
                if (stopping) return false;
                projectionEpoch++; projectionDesired = identity; projectionLatest = null; projectionReceipt = null; projectionWindowSerial++; projectionWake = true; Monitor.Pulse(gate); return true;
            }
        }
        internal bool RequestProjectionWindow(BlueprintProjectionIdentity identity, long generation, BlueprintProjectionBounds bounds)
        {
            lock (gate)
            {
                if (stopping || projectionDesired == null || !projectionDesired.Equals(identity)) return false;
#if DEBUG
                if (projectionLatest != null) Interlocked.Increment(ref projectionRetiredRequests);
#endif
                projectionLatest = new ProjectionRequest { Identity = identity, Generation = generation, Bounds = bounds, Serial = ++projectionWindowSerial };
                if (projectionReceipt != null && projectionReceipt.Kind == BlueprintProjectionReceiptKind.Window) projectionReceipt = null;
                projectionWake = true; Monitor.Pulse(gate); return true;
            }
        }
        internal void CloseProjection()
        {
            lock (gate) { projectionEpoch++; projectionWindowSerial++; projectionDesired = null; projectionLatest = null; projectionReceipt = null; projectionWake = true; Monitor.Pulse(gate); }
        }
        internal bool TryTakeProjectionReceipt(out BlueprintProjectionReceipt value)
        { lock (gate) { value = projectionReceipt; projectionReceipt = null; if (value != null) Monitor.Pulse(gate); return value != null; } }
        private bool ProjectionHasWorkLocked()
        {
            return projectionWake || projectionOpening != null || projectionReading != null || projectionLatest != null &&
                (projectionReceipt == null || projectionReceipt.Kind != BlueprintProjectionReceiptKind.Opened);
        }
        private bool ProjectionCurrent(ProjectionOwner owner, ProjectionRequest request = null)
        {
            lock (gate) return !stopping && owner.Epoch == projectionEpoch && projectionDesired != null && owner.Identity.Equals(projectionDesired) && (request == null || request.Serial == projectionWindowSerial);
        }
        private void CheckProjection(ProjectionOwner owner, ProjectionRequest request = null) { if (!ProjectionCurrent(owner, request)) throw new OperationCanceledException(); }
        private void PublishProjection(ProjectionOwner owner, BlueprintProjectionReceipt receipt, ProjectionRequest request = null)
        { lock (gate) { if (ProjectionCurrent(owner, request)) projectionReceipt = receipt; } }
        private void CloseProjectionWorker()
        {
            // Dispose iterators first: layout enumerators and validation lookup
            // caches may still be live at their last cooperative yield.
            if (projectionOpening != null) projectionOpening.Dispose(); projectionOpening = null;
            if (projectionReading != null) projectionReading.Dispose(); projectionReading = null;
            projectionActiveRequest = null;
            if (projectionOwner != null) projectionOwner.Dispose(); projectionOwner = null;
#if DEBUG
            Interlocked.Exchange(ref projectionDirectoryBytes, 0); Interlocked.Exchange(ref projectionDirectoryBlocks, 0); Interlocked.Exchange(ref projectionCacheBytes, 0);
#endif
        }
        private void RetireProjectionAsset(Guid id)
        {
            lock (gate)
            {
                if (projectionDesired != null && projectionDesired.AssetId == id)
                { projectionEpoch++; projectionWindowSerial++; projectionDesired = null; projectionLatest = null; projectionReceipt = null; projectionWake = false; }
            }
            if (projectionOwner != null && projectionOwner.Identity.AssetId == id) CloseProjectionWorker();
        }
        private void ReleaseRetiredProjection()
        { if (projectionOwner != null && !ProjectionCurrent(projectionOwner)) CloseProjectionWorker(); }
        private void ProjectionStep()
        {
            BlueprintProjectionIdentity desired; long epoch;
            lock (gate) { desired = projectionDesired; epoch = projectionEpoch; projectionWake = false; }
            if (desired == null || IsStopping())
            {
                BlueprintProjectionIdentity previous = projectionOwner == null ? null : projectionOwner.Identity;
                CloseProjectionWorker();
                lock (gate) if (!stopping && projectionDesired == null && epoch == projectionEpoch) projectionReceipt = new BlueprintProjectionReceipt(BlueprintProjectionReceiptKind.Closed, previous);
                return;
            }
            if (projectionOwner == null || projectionOwner.Epoch != epoch)
            {
                CloseProjectionWorker(); projectionOwner = new ProjectionOwner { Identity = desired, Epoch = epoch };
                projectionOpening = OpenProjection(projectionOwner).GetEnumerator();
            }
            ProjectionOwner owner = projectionOwner;
            try
            {
                if (!opened && rootFailure == null) { try { files.Open(); opened = true; } catch (Exception e) { rootFailure = Reason(e); } }
                if (rootFailure != null) throw new IOException(rootFailure);
#if DEBUG
                Interlocked.Increment(ref projectionSlices);
#endif
                if (projectionOpening != null)
                {
                    if (!projectionOpening.MoveNext()) { projectionOpening.Dispose(); projectionOpening = null; }
                    return;
                }
                ProjectionRequest request;
                lock (gate)
                {
                    // One bounded receipt cannot overwrite the initial open
                    // result with a prequeued window. Taking it wakes this
                    // worker; ordinary library commands remain admissible.
                    if (projectionReceipt != null && projectionReceipt.Kind == BlueprintProjectionReceiptKind.Opened) return;
                    request = projectionLatest; projectionLatest = null;
                }
                if (request != null)
                {
                    if (projectionReading != null) projectionReading.Dispose();
                    projectionActiveRequest = request;
                    projectionReading = ReadProjectionWindow(owner, request).GetEnumerator();
                }
                if (projectionReading != null && !projectionReading.MoveNext()) { projectionReading.Dispose(); projectionReading = null; projectionActiveRequest = null; }
            }
            catch (OperationCanceledException)
            {
#if DEBUG
                Interlocked.Increment(ref projectionRetiredRequests);
#endif
                if (!ProjectionCurrent(owner)) CloseProjectionWorker();
                else { if (projectionReading != null) projectionReading.Dispose(); projectionReading = null; projectionActiveRequest = null; }
            }
            catch (Exception e)
            {
                ProjectionRequest failed = projectionActiveRequest;
                if (failed != null && !ProjectionCurrent(owner, failed))
                {
                    // Even an I/O error belongs to its request generation.
                    // A retired window cannot fail a newer queued window.
                    if (projectionReading != null) projectionReading.Dispose(); projectionReading = null; projectionActiveRequest = null; return;
                }
                PublishProjection(owner, new BlueprintProjectionReceipt(BlueprintProjectionReceiptKind.Failed, owner.Identity, failed == null ? 0 : failed.Generation, error: Reason(e), failureCode: Code(e)), failed);
                // A failed or partial halo has no published window and is never
                // cached as air. Explicit reopening retries its complete proof.
                lock (gate) if (owner.Epoch == projectionEpoch && (failed == null || failed.Serial == projectionWindowSerial)) { projectionDesired = null; projectionLatest = null; }
                CloseProjectionWorker();
            }
            finally { files.CloseMetadata(); }
        }
        private IEnumerable<bool> OpenProjection(ProjectionOwner owner)
        {
            CheckProjection(owner);
            if (projectionUncertainAssets.Contains(owner.Identity.AssetId)) throw new InvalidDataException("blueprint-projection-delete-unconfirmed");
            PreferenceReadResult read = files.OpenMetadata(owner.Identity.AssetId);
            if (read.Status != PreferenceReadStatus.Loaded) throw new InvalidDataException("blueprint-projection-metadata");
            LibraryMetadata metadata = LibraryCodec.DecodeMetadata(read.Contents, owner.Identity.AssetId);
            if (metadata.Deleted || metadata.ContentRevision != owner.Identity.ContentRevision || metadata.ContentHash != owner.Identity.ContentHash) throw new InvalidDataException("blueprint-projection-identity");
            owner.Root = ReadAssetRoot(metadata); files.CloseMetadata();
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
            using (var objects = new ObjectLookup(this, owner.Identity.AssetId, false, owner.Root.BlockCount, owner.Index))
            {
                for (long number = 0; number < owner.Root.BlockCount; number++)
                {
                    CheckProjection(owner); CaptureChunk chunk = ProjectionChunk(owner, number); CaptureSummary summary = owner.Root.Summary;
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        CaptureCell cell = chunk.Cells[i]; long key = Key(cell);
                        if (sawObjects || key <= previous || cell.X < summary.Left || cell.Y < summary.Top || (long)cell.X >= (long)summary.Left + summary.Width || (long)cell.Y >= (long)summary.Top + summary.Height) throw new InvalidDataException("blueprint-cell-order-bounds-or-duplicate");
                        previous = key;
                        if (cell.ObjectId > 0)
                        {
                            CaptureObject obj = objects.Find(cell.ObjectId);
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
#if DEBUG
                    Interlocked.Exchange(ref projectionDirectoryBytes, owner.Directory.EstimatedBytes); Interlocked.Exchange(ref projectionDirectoryBlocks, owner.Directory.CellBlockCount + owner.Directory.ObjectBlockCount);
#endif
                    yield return true;
                }
            }
            if (totalCells != owner.Root.Summary.ContentCount || unreliable != owner.Root.Summary.UnreliableObjectCount) throw new InvalidDataException("blueprint-content-count");
            CheckProjection(owner);
            PublishProjection(owner, new BlueprintProjectionReceipt(BlueprintProjectionReceiptKind.Opened, owner.Identity, summary: owner.Root.Summary));
        }
        private IEnumerable<bool> ReadProjectionWindow(ProjectionOwner owner, ProjectionRequest request)
        {
            CheckProjection(owner, request); CaptureSummary summary = owner.Root.Summary; BlueprintProjectionBounds bounds = request.Bounds;
            int left = Math.Min(summary.Width, Math.Max(0, bounds.Left)), top = Math.Min(summary.Height, Math.Max(0, bounds.Top));
            int right = Math.Max(left, Math.Min(summary.Width, bounds.Right)), bottom = Math.Max(top, Math.Min(summary.Height, bounds.Bottom));
            // Window retention is O(actual queried area), independent of asset
            // size. Do not impose a hidden 4K/zoom limit on legitimate views.
            long area = checked((long)(right - left) * (bottom - top));
            var cells = new List<CaptureCell>(); var objectIds = new HashSet<int>();
            if (left < right && top < bottom)
            {
                foreach (long number in owner.Directory.Query(checked(left + summary.Left), checked(top + summary.Top), checked(right + summary.Left), checked(bottom + summary.Top), ProjectionComparison))
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
                        cells.Add(cell); if (cell.ObjectId != 0) objectIds.Add(cell.ObjectId);
                    }
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
                        obj.X -= summary.Left; obj.Y -= summary.Top; objects.Add(obj); found = true; break;
                    }
                }
                if (!found) throw new InvalidDataException("blueprint-missing-object-reference");
                yield return true;
            }
            CheckProjection(owner, request);
            var window = new BlueprintProjectionWindow(owner.Identity, request.Generation, summary, bounds, cells.ToArray(), objects.ToArray(), true);
            PublishProjection(owner, new BlueprintProjectionReceipt(BlueprintProjectionReceiptKind.Window, owner.Identity, request.Generation, summary, window), request);
        }
        private CaptureChunk ProjectionChunk(ProjectionOwner owner, long number)
        {
            for (int i = 0; i < owner.Cache.Count; i++) if (owner.Cache[i].Number == number)
            { ProjectionCacheBlock hit = owner.Cache[i]; owner.Cache.RemoveAt(i); owner.Cache.Add(hit); return hit.Chunk; }
            owner.Index.Position = checked(number * 32L); byte[] expected = new byte[32]; ReadExactly(owner.Index, expected);
            byte[] bytes = files.ReadBlock(owner.Identity.AssetId, false, number);
            if (!LibraryCodec.Equal(LibraryCodec.Hash(bytes), expected)) throw new InvalidDataException("blueprint-chunk-hash");
            CaptureChunk chunk = LibraryCodec.DecodeChunk(bytes);
#if DEBUG
            Interlocked.Increment(ref projectionBlockReads); Interlocked.Add(ref projectionDecodedRecords, chunk.Count + chunk.ObjectCount);
#endif
            // Charge conservative decoded arrays/string sizes, not only encoded
            // bytes. One maximum block fits; LRU retention stays under 8 MiB.
            long retained = 128L + chunk.Count * 64L + chunk.ObjectCount * 128L;
            for (int i = 0; i < chunk.ObjectCount; i++) retained += (long)(chunk.Objects[i].Reason.Length + chunk.Objects[i].MissingPayload.Length) * 2;
            while (owner.Cache.Count != 0 && (owner.Cache.Count >= 64 || owner.CacheBytes + retained > ProjectionCacheLimit)) { owner.CacheBytes -= owner.Cache[0].Bytes; owner.Cache.RemoveAt(0); }
            if (retained <= ProjectionCacheLimit) { owner.Cache.Add(new ProjectionCacheBlock { Number = number, Bytes = retained, Chunk = chunk }); owner.CacheBytes += retained; }
#if DEBUG
            Interlocked.Exchange(ref projectionCacheBytes, owner.CacheBytes);
            if (owner.CacheBytes > Interlocked.Read(ref projectionCachePeak)) Interlocked.Exchange(ref projectionCachePeak, owner.CacheBytes);
#endif
            return chunk;
        }
        private void ProjectionComparison()
        {
#if DEBUG
            Interlocked.Increment(ref projectionDirectoryComparisons);
#endif
        }
    }
}
