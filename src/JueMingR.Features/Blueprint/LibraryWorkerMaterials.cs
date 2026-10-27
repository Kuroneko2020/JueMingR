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
        private sealed class MaterialRequest
        { internal BlueprintMaterialKey Key; internal BlueprintPlacementCatalog Catalog; }
        private sealed class MaterialResult
        { internal BlueprintMaterialKey Key; internal BlueprintMaterialSnapshot Snapshot; internal long Bytes; internal bool ReadFailed; }
        private const int MaterialRequestLimit = 8, MaterialCacheCount = 16;
        private const long MaterialCacheBytes = 2L * 1024 * 1024;
        // This lane belongs to the one existing file worker. It does not use
        // the UI read generation or the accepted mutation/result mailbox.
        private readonly List<MaterialRequest> materialRequests = new List<MaterialRequest>();
        private readonly List<MaterialResult> materialResults = new List<MaterialResult>();
        private MaterialRequest materialRequest;
        private IEnumerator<bool> materialReading;
        private BlueprintMaterialAccumulator materialAccumulator;
        private long materialRevision, materialBytes;
        private bool materialLane, materialCancelled;
#if DEBUG
        internal long MaterialSlices, MaterialRequests, MaterialCacheHits;
#endif
        internal bool RequestMaterials(BlueprintMaterialKey key, BlueprintPlacementCatalog catalog)
        {
            if (key == null || catalog == null || key.PlacementRevision != catalog.Revision) return false;
            lock (gate)
            {
                if (stopping) return false;
                foreach (var cached in materialResults) if (cached.Key.Equals(key)) return true;
                if (!materialCancelled && materialRequest?.Key.Equals(key) == true) return true;
                foreach (var queued in materialRequests) if (queued.Key.Equals(key)) return true;
                if (materialRequests.Count >= MaterialRequestLimit) return false;
                materialRequests.Add(new MaterialRequest { Key = key, Catalog = catalog }); Monitor.Pulse(gate);
#if DEBUG
                MaterialRequests++;
#endif
                return true;
            }
        }
        internal BlueprintMaterialSnapshot GetMaterials(BlueprintMaterialKey key)
        {
            lock (gate) for (int i = 0; i < materialResults.Count; i++) if (materialResults[i].Key.Equals(key))
            {
                var hit = materialResults[i]; materialResults.RemoveAt(i); materialResults.Add(hit);
#if DEBUG
                MaterialCacheHits++;
#endif
                return hit.Snapshot;
            }
            return BlueprintMaterialSnapshot.Pending;
        }
        internal void CancelMaterials()
        {
            lock (gate)
            {
                materialRequests.Clear(); materialCancelled = true;
                // Keep a failed read stable while its view is open. A new
                // explicit view retries a transient file failure, rather than
                // either caching it forever or polling the disk every frame.
                for (int i = materialResults.Count - 1; i >= 0; i--) if (materialResults[i].ReadFailed)
                { materialBytes -= materialResults[i].Bytes; materialResults.RemoveAt(i); }
                Monitor.Pulse(gate);
            }
        }
        private void RetireMaterialSource(Guid id, bool isStage)
        {
            // An accepted mutation wins before rename/delete/promote reaches
            // the filesystem. Release only this derived reader's handles;
            // ordinary Preview and other identities retain their owners.
            lock (gate)
            {
                materialRequests.RemoveAll(request => request.Key.Id == id && request.Key.Stage == isStage);
                if (materialRequest?.Key.Id == id && materialRequest.Key.Stage == isStage)
                { materialReading?.Dispose(); materialReading = null; materialRequest = null; materialAccumulator = null; }
                if (!isStage) for (int i = materialResults.Count - 1; i >= 0; i--) if (materialResults[i].Key.Id == id)
                { materialBytes -= materialResults[i].Bytes; materialResults.RemoveAt(i); }
            }
        }
        private bool MaterialHasWorkLocked() { return materialReading != null || materialRequests.Count != 0; }
        private bool MaterialTurn()
        {
            lock (gate)
            {
                if (!MaterialHasWorkLocked()) return false;
                if (!ProjectionHasWorkLocked()) return true;
                materialLane = !materialLane; return materialLane;
            }
        }
        private void MaterialStep()
        {
            try
            {
                lock (gate)
                {
                    if (materialCancelled)
                    { materialReading?.Dispose(); materialReading = null; materialAccumulator = null; materialRequest = null; materialCancelled = false; }
                    if (materialRequest == null)
                    {
                        if (materialRequests.Count == 0) return;
                        materialRequest = materialRequests[0]; materialRequests.RemoveAt(0);
                        materialAccumulator = new BlueprintMaterialAccumulator(); materialReading = ReadMaterials(materialRequest, materialAccumulator).GetEnumerator();
                    }
                }
#if DEBUG
                MaterialSlices++;
#endif
                if (materialReading.MoveNext()) return;
                PublishMaterials(materialAccumulator.Snapshot(++materialRevision));
            }
            catch (Exception error)
            {
                // A failed derived read never protects, rewrites or changes the
                // underlying v1 asset or an accepted Save/Delete transaction.
                materialAccumulator?.Unresolved(Reason(error));
                PublishMaterials(materialAccumulator?.Snapshot(++materialRevision) ?? new BlueprintMaterialSnapshot(++materialRevision,
                    BlueprintMaterialStatus.Unresolved, Array.Empty<BlueprintMaterialRow>(), false, Reason(error)), true);
            }
        }
        private void PublishMaterials(BlueprintMaterialSnapshot snapshot, bool readFailed = false)
        {
            materialReading?.Dispose(); materialReading = null;
            lock (gate)
            {
                if (materialRequest != null && !materialCancelled) StoreMaterials(materialRequest.Key, snapshot, readFailed);
                materialRequest = null; materialAccumulator = null;
                materialCancelled = false;
            }
        }
        private void StoreMaterials(BlueprintMaterialKey key, BlueprintMaterialSnapshot snapshot, bool readFailed = false)
        {
            lock (gate)
            {
                long bytes = checked(256L + snapshot.Rows.Count * 80L + (snapshot.Error?.Length ?? 0) * 2L);
                if (bytes > MaterialCacheBytes) return;
                for (int i = materialResults.Count - 1; i >= 0; i--) if (materialResults[i].Key.Equals(key)) { materialBytes -= materialResults[i].Bytes; materialResults.RemoveAt(i); }
                while (materialResults.Count != 0 && (materialResults.Count >= MaterialCacheCount || materialBytes + bytes > MaterialCacheBytes))
                { materialBytes -= materialResults[0].Bytes; materialResults.RemoveAt(0); }
                materialResults.Add(new MaterialResult { Key = key, Snapshot = snapshot, Bytes = bytes, ReadFailed = readFailed }); materialBytes += bytes;
            }
        }
        private MaterialRequest TakePreviewMaterials(LibraryCommand command, LibraryRoot root)
        {
            lock (gate)
            {
                for (int i = 0; i < materialRequests.Count; i++)
                {
                    var request = materialRequests[i]; var key = request.Key;
                    if (key.Id != command.Id || key.Stage != command.Stage) continue;
                    var expected = new BlueprintMaterialKey(command.Id, command.Stage, command.Stage ? Guid.Empty : root.ContentRevision,
                        command.Stage ? "" : command.Identity, command.Exclude, command.Excluded, key.PlacementRevision, root.Summary.TargetVersion, key.ClassifierVersion);
                    if (!key.Equals(expected)) continue;
                    materialRequests.RemoveAt(i); return request;
                }
                return null;
            }
        }
        private void CheckMaterials(MaterialRequest request)
        { lock (gate) if (stopping || materialCancelled || !ReferenceEquals(request, materialRequest)) throw new OperationCanceledException(); }
        private IEnumerable<bool> ReadMaterials(MaterialRequest request, BlueprintMaterialAccumulator into)
        {
            CheckMaterials(request);
            if (!opened && rootFailure == null) { try { files.Open(); opened = true; } catch (Exception e) { rootFailure = Reason(e); } }
            if (rootFailure != null) throw new IOException(rootFailure);
            Guid physical; LibraryRoot root; BlueprintMaterialKey key = request.Key;
            if (key.Stage) { RequireStage(key.Id, true); physical = stage.Physical; root = stage.Root; }
            else
            {
                PreferenceReadResult read = files.OpenMetadata(key.Id);
                if (read.Status != PreferenceReadStatus.Loaded) throw new InvalidDataException("蓝图不可读取");
                try
                {
                    var metadata = LibraryCodec.DecodeMetadata(read.Contents, key.Id);
                    if (metadata.Deleted || metadata.ContentRevision != key.ContentRevision || metadata.ContentHash != key.ContentHash) throw new InvalidDataException("蓝图内容身份已改变");
                    physical = key.Id; root = ReadAssetRoot(metadata);
                }
                finally { files.CloseMetadata(); }
            }
            if (root.Summary.TargetVersion != key.TargetVersion || request.Catalog.TargetVersion != key.TargetVersion)
            { into.Unresolved("目标版本的材料解释尚未确认"); yield break; }
            var layout = files as IBlueprintLibraryLayoutSteps;
            if (layout != null) foreach (int ignored in layout.ValidateLayoutSteps(physical, key.Stage, root.BlockCount, 32)) { CheckMaterials(request); yield return true; }
            else { files.ValidateLayout(physical, key.Stage, root.BlockCount); yield return true; }
            // A static view owns its verified index, not a world projection's
            // lifetime. Recheck the exact shared owner at each actual read:
            // its final instance may retire between these cooperative yields.
            // Returning null selects VisitSteps' ordinary verified read/cache;
            // it neither keeps the retired projection alive nor starts a worker.
            var template = key.Stage ? null : new BlueprintTemplateKey(key.Id, key.ContentRevision, key.ContentHash);
            Func<long, CaptureChunk> reader = template == null ? null : new Func<long, CaptureChunk>(number =>
            {
                ProjectionOwner shared;
                return projectionOwners.TryGetValue(template, out shared) && shared.Ready && shared.Index != null && ProjectionCurrent(shared)
                    ? ProjectionChunk(shared, number) : null;
            });
            Stream index = files.OpenIndex(physical, key.Stage);
            bool transferred = false;
            try
            {
                if (index.Length != checked(root.BlockCount * 32L)) throw new InvalidDataException("blueprint-index-hash");
                using (var hash = SHA256.Create())
                {
                    byte[] buffer = new byte[32768]; int count;
                    while ((count = index.Read(buffer, 0, buffer.Length)) > 0) { hash.TransformBlock(buffer, 0, count, buffer, 0); CheckMaterials(request); yield return true; }
                    hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    if (!LibraryCodec.Equal(hash.Hash, root.IndexHash)) throw new InvalidDataException("blueprint-index-hash");
                }
                transferred = true;
                foreach (bool ignored in VisitSteps(physical, key.Stage, root, null, null, verifiedIndex: index, reader: reader,
                    materialKey: key, catalog: request.Catalog, materials: into)) { CheckMaterials(request); yield return true; }
            }
            finally { if (!transferred) index.Dispose(); }
        }
        private void CloseMaterialWorker()
        { materialReading?.Dispose(); materialReading = null; lock (gate) { materialRequest = null; materialRequests.Clear(); materialResults.Clear(); materialBytes = 0; } }
    }
}
