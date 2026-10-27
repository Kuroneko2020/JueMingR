using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.ItemBrowser;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed class BlueprintBuildTarget
    {
        internal BlueprintProjectionIdentity Identity;
        internal BlueprintProjectionMask Mask;
        internal BlueprintProjectionMatchRange Range;
        internal long RangeStamp, SupportStamp, PlacementRevision;
        internal int OriginX, OriginY, TargetX, TargetY, Ordinal;
        internal CaptureCell Cell;
        internal CaptureObject Object;
        internal BlueprintWorldObject Body;
        internal long CatalogRevision { get { return PlacementRevision; } }
        internal int WorldX { get { return TargetX; } }
        internal int WorldY { get { return TargetY; } }
        internal BlueprintProjectionBounds Bounds;
        internal BlueprintPlacementDescriptor Placement;
        internal bool LocalProof;
    }
    internal sealed class BlueprintMaterialTotals
    {
        internal readonly SortedDictionary<int, long> Counts = new SortedDictionary<int, long>();
        internal long Revision;
        internal int Expected, Current, Unresolved, Updating;
        internal bool Failed;
        internal void Delta(int item, long delta)
        {
            if (Failed || delta == 0) return;
            try
            {
                long old; Counts.TryGetValue(item, out old); long next = checked(old + delta);
                if (next < 0) throw new OverflowException();
                if (next == 0) Counts.Remove(item); else Counts[item] = next;
                Revision++;
            }
            catch (OverflowException) { Failed = true; Revision++; }
        }
    }
    internal sealed class BlueprintRangeMaterials
    {
        internal BlueprintMaterialAccumulator Progress;
        internal IReadOnlyDictionary<int, long> Published;
        internal IReadOnlyDictionary<int, long> Next;
        internal IReadOnlyDictionary<int, long> Desired;
        internal BlueprintMaterialTotals OwnerTotals;
        internal IEnumerator<KeyValuePair<int, long>> Change;
        internal bool Adding, Current, Unresolved, Queued;
        internal BlueprintMaterialStatus NextStatus;
        internal long CatalogRevision;
        internal BlueprintBuildTarget Candidate;
        internal int CandidateCursor;
        internal long BuildEpoch, BuildWakeRevision;
        internal long BuildPlayerRevision, BuildSupplyRevision, BuildContainerRevision, BuildActivationRevision;
        internal BlueprintBuildWait BuildWaits;
        internal ulong BuildDependencyWaitSlots;
        internal bool BuildRetryPending;
        internal bool BuildExhausted;
        internal bool EligibilityRetry;
        internal int EligibilityRetryOrdinal;
        internal ulong EligibilityRetryAt;
        internal ulong DynamicRetryAt;
    }
    internal sealed partial class HostBlueprint
    {
        private HostItemKnowledge materialKnowledge;
        private Func<int, Item, bool> materialInventoryEligible;
        internal Func<bool> MaterialBuildDemand;
        internal long MaterialBuildEligibilityRevision;
        private bool materialViewDemand, materialStaticDemand;
        private long materialPlacementIdentity = -1, materialInventoryRevision, materialSnapshotRevision, materialSceneIdentity = -1;
        private long materialScope;
        private ulong materialTick;
        private bool materialTickSet;
        private bool materialCatalogTransition, materialContextTransition, materialObservedDynamicDemand;
        private long materialDemandEpoch, materialTransitionRemaining;
        private Item[] materialInventoryArray;
        private readonly BlueprintMaterialInventorySlot[] materialSlots = new BlueprintMaterialInventorySlot[50];
        private BlueprintMaterialInventory materialInventory;
        private readonly BlueprintMaterialTotals materialAll = new BlueprintMaterialTotals();
        private readonly LinkedList<BlueprintProjectionMatchRange> materialChanges = new LinkedList<BlueprintProjectionMatchRange>();
        private IReadOnlyList<BlueprintMaterialInstance> materialInstances = Array.Empty<BlueprintMaterialInstance>();
        private int materialDemandSceneCursor, materialBuildSceneCursor, materialBuildSearchBudget;
        private int materialMatchOwners;
        // A native placement condition can depend on another player's remote
        // structure, global capacity, or derived biome. Local watch/inventory
        // stamps cannot certify that such a rejection remains permanent.
        // Back off only the range that actually encountered that condition;
        // the existing 64-entry search and source/Match budgets own aggregate
        // work. This is not a global one-range-per-delay throughput limit.
        private const ulong BuildEligibilityRetryDelay = 240;
        private BlueprintMaterialTotals materialPublishingTotals;
        private IEnumerator<KeyValuePair<int, long>> materialPublishing;
        private List<BlueprintMaterialRow> materialPublishingRows = new List<BlueprintMaterialRow>();
        private long materialPublishingTotal;
        private long materialPublishingRevision, materialPublishingInventoryRevision;
        private BlueprintMaterialSnapshot materialWorldSnapshot = BlueprintMaterialSnapshot.Pending;
        private BlueprintMaterialKey materialDraftKey;
        private int[] materialDraftExclusions;
        private BlueprintMaterialSnapshot materialDraftSnapshot = BlueprintMaterialSnapshot.Pending;
        private readonly Dictionary<Guid, BlueprintMaterialKey> materialAssetKeys = new Dictionary<Guid, BlueprintMaterialKey>();
#if DEBUG
        internal long MaterialInventoryReads, MaterialContributionSteps, MaterialPublicationSteps, MaterialSourceDemands;
        internal long BuildEligibilityRetryWakes;
#endif
        internal void AttachMaterialKnowledge(HostItemKnowledge knowledge, Func<int, Item, bool> eligible)
        { materialKnowledge = knowledge; materialInventoryEligible = eligible; }
        internal BlueprintMaterialInventory MaterialInventory { get { return materialInventory; } }
        internal long MaterialNameRevision { get { return materialKnowledge?.Native.Revision ?? 0; } }
        internal string MaterialName(int type) { return type > 0 ? Lang.GetItemNameValue(type) : ""; }
        internal IReadOnlyList<BlueprintMaterialInstance> MaterialInstances { get { EnsureMaterialInstances(); return materialInstances; } }
        internal void SetMaterialViewDemand(bool requested)
        {
            if (materialViewDemand == requested) return; materialViewDemand = requested;
            ObserveMaterialDemand();
            if (!requested) { materialScope = 0; CancelMaterialPublication(); }
        }
        internal void CancelStaticMaterialViews()
        { materialStaticDemand = false; Library?.CancelMaterials(); }
        internal BlueprintMaterialSnapshot MaterialDraft()
        {
            materialStaticDemand = true; var catalog = MaterialCatalog;
            if (Library == null || Flow.ContentHandle.IsEmpty || Flow.Summary == null || catalog == null) return BlueprintMaterialSnapshot.Pending;
            bool sameKey = materialDraftKey != null && materialDraftKey.Id == Flow.ContentHandle.Id &&
                materialDraftKey.ExcludeUnreliable == Flow.ExcludeUnreliable && ReferenceEquals(materialDraftExclusions, Flow.ExcludedObjectIds) &&
                materialDraftKey.PlacementRevision == catalog.Revision && materialDraftKey.TargetVersion == Flow.Summary.TargetVersion;
            // A handle is allocated before capture, and Summary already exists
            // during Sealing. Only the completed seal receipt authorizes reads.
            // Accepted Create retires its reader before promotion; Saving may
            // retain this exact key's published value but must never reopen it.
            if (Flow.Status == BlueprintCaptureStatus.Saving) return sameKey ? materialDraftSnapshot : BlueprintMaterialSnapshot.Pending;
            if (Flow.Status != BlueprintCaptureStatus.DraftReady) return BlueprintMaterialSnapshot.Pending;
            if (!sameKey)
            {
                materialDraftExclusions = Flow.ExcludedObjectIds;
                materialDraftKey = new BlueprintMaterialKey(Flow.ContentHandle.Id, true, Guid.Empty, "", Flow.ExcludeUnreliable,
                    Flow.ExcludedObjectIds, catalog.Revision, Flow.Summary.TargetVersion);
                materialDraftSnapshot = BlueprintMaterialSnapshot.Pending;
            }
            var key = materialDraftKey;
            Library.RequestMaterials(key, catalog); return materialDraftSnapshot = Library.Materials(key);
        }
        internal BlueprintMaterialSnapshot MaterialAsset(LibraryEntry entry)
        {
            materialStaticDemand = true; var catalog = MaterialCatalog; Guid revision;
            if (Library == null || entry == null || catalog == null || !Guid.TryParse(entry.ContentRevision, out revision)) return BlueprintMaterialSnapshot.Pending;
            BlueprintMaterialKey key;
            if (!materialAssetKeys.TryGetValue(entry.AssetId, out key) || key.ContentRevision != revision || key.ContentHash != entry.ContentHash || key.PlacementRevision != catalog.Revision)
            {
                if (materialAssetKeys.Count >= 16) materialAssetKeys.Clear();
                materialAssetKeys[entry.AssetId] = key = new BlueprintMaterialKey(entry.AssetId, false, revision, entry.ContentHash, false, null, catalog.Revision);
            }
            Library.RequestMaterials(key, catalog); return Library.Materials(key);
        }
        internal BlueprintMaterialSnapshot MaterialWorld(long projectionGeneration = 0)
        {
            SetMaterialViewDemand(true); EnsureMaterialInstances();
            if (projectionGeneration != 0)
            {
                bool exists = false; foreach (var instance in materialInstances) if (instance.ProjectionGeneration == projectionGeneration) { exists = true; break; }
                if (!exists) projectionGeneration = 0;
            }
            if (materialScope != projectionGeneration) { materialScope = projectionGeneration; CancelMaterialPublication(); }
            if (projectionScene.Count > 0 && (MaterialCatalog == null || materialScope == 0 && materialMatchOwners != projectionScene.Count)) CancelMaterialPublication();
            return materialWorldSnapshot;
        }
        private BlueprintPlacementCatalog MaterialCatalog { get { return materialKnowledge?.Native.MaterialPlacement; } }
        private bool DynamicMaterialDemand { get { return materialViewDemand || MaterialBuildDemand?.Invoke() == true; } }
        private void ObserveMaterialDemand()
        {
            bool demand = DynamicMaterialDemand;
            if (materialObservedDynamicDemand == demand) return;
            materialObservedDynamicDemand = demand;
            if (!demand) return;
            // Context-only layers were deliberately unwatched while closed.
            // Revalidate coupled parts through the existing bounded cursor
            // before offering Exact again; final Match remains independent.
            materialContextTransition = materialAll.Expected > 0;
            materialDemandEpoch++; materialTransitionRemaining = materialAll.Expected;
            CancelMaterialPublication();
        }
        // Placement anchors are one cell outside the accepted target body.
        // Project only real tile/wall/liquid changes onto the existing shared
        // proof geometry; no second source directory or world scan is needed.
        private bool MaterialSupportInterest(BlueprintProjectionSourceRange source, BlueprintProjectionMask mask,
            int originX, int originY, BlueprintProjectionBounds changed, ushort layers)
        {
            BlueprintProjectionBounds local;
            return MaterialBuildDemand?.Invoke() == true && (layers & 7) != 0 &&
                TryMaterialSupportBounds(source.Bounds, originX, originY, changed, out local) && source.IntersectsProofs(local, 3, mask);
        }
        private void RouteMaterialSupport(BlueprintProjectionSourceRange source, BlueprintProjectionMask mask,
            int originX, int originY, BlueprintProjectionBounds changed, ushort layers, Action<int> emit)
        {
            BlueprintProjectionBounds local;
            if (MaterialBuildDemand?.Invoke() == true && (layers & 7) != 0 &&
                TryMaterialSupportBounds(source.Bounds, originX, originY, changed, out local)) source.RouteProofs(local, 3, mask, emit);
        }
        private static bool TryMaterialSupportBounds(BlueprintProjectionBounds source, int originX, int originY,
            BlueprintProjectionBounds changed, out BlueprintProjectionBounds local)
        {
            // Clip in long before constructing bounds: a section/coarse write
            // can span coordinates well outside this instance's local space.
            long left = Math.Max(source.Left, (long)changed.Left - originX - 1), top = Math.Max(source.Top, (long)changed.Top - originY - 1);
            long right = Math.Min(source.Right, (long)changed.Right - originX + 1), bottom = Math.Min(source.Bottom, (long)changed.Bottom - originY + 1);
            if (right <= left || bottom <= top) { local = default(BlueprintProjectionBounds); return false; }
            local = new BlueprintProjectionBounds((int)left, (int)top, (int)(right - left), (int)(bottom - top)); return true;
        }
        private void WakeMaterialBuildRange(BlueprintProjectionMatchRange range)
        {
            if (MaterialBuildDemand?.Invoke() != true) return;
            var value = range.Materials;
            if (value != null)
            {
                value.BuildWakeRevision++; value.Candidate = null;
                value.BuildRetryPending = true; value.EligibilityRetry = false;
            }
            // This demand uses the original bounded source/world scheduler.
            // Its final Match proof and material contribution remain current.
            DemandProjectionMatchRange(range);
        }
        private void RequestBuildEligibilityRetry(BlueprintBuildTarget target)
        {
            if (target?.Range.Materials == null || !IsBuildTargetCurrent(target) || IsUnconfirmedTarget(target)) return;
            var value = target.Range.Materials;
            if (value.EligibilityRetry) return;
            value.EligibilityRetry = true; value.EligibilityRetryOrdinal = target.Ordinal;
            value.EligibilityRetryAt = materialTick + BuildEligibilityRetryDelay;
        }
        internal void UpdateMaterials(ulong tick)
        {
            if (materialTickSet && materialTick == tick) return; materialTick = tick; materialTickSet = true;
            bool dynamicDemand = DynamicMaterialDemand;
            if (!HasSession) return;
            ObserveMaterialDemand();
            materialBuildSearchBudget = MaterialBuildDemand?.Invoke() == true ? 64 : 0;
            if (dynamicDemand || materialStaticDemand) materialKnowledge?.Native.StepPlacement();
            materialStaticDemand = false;
            var catalog = MaterialCatalog;
            if (catalog == null && projectionScene.Count > 0) CancelMaterialPublication();
            if (catalog != null && materialPlacementIdentity != catalog.Revision)
            {
                materialPlacementIdentity = catalog.Revision;
                materialCatalogTransition = materialAll.Expected > 0; materialDemandEpoch++; materialTransitionRemaining = materialAll.Expected;
                // The bounded source cursor below requests only missing
                // material evidence. Existing Match.Current stays untouched.
                CancelMaterialPublication();
            }
            if (dynamicDemand) ObserveMaterialInventory(tick);
            else { materialInventory = null; materialInventoryArray = null; }
            if (MaterialBuildDemand?.Invoke() == true) ObserveBuildSignals();
            DrainMaterialChanges(64);
            if (dynamicDemand && catalog != null) DemandMaterialSources(64);
            if (materialViewDemand) PublishMaterialWorld(64);
        }
        private void ObserveMaterialInventory(ulong tick)
        {
            var inventory = player?.inventory; bool readyInventory = inventory != null && inventory.Length >= 50 && materialInventoryEligible != null;
            bool changed = materialInventory == null || materialInventory.Ready != readyInventory || !ReferenceEquals(inventory, materialInventoryArray) || materialInventory.SessionGeneration != generation;
            if (readyInventory) for (int i = 0; i < 50; i++)
            {
                Item item = inventory[i]; int type = item == null || item.IsAir || item.stack <= 0 ? 0 : item.type;
                bool eligible = type > 0 && materialInventoryEligible(i, item); long stack = type > 0 ? item.stack : 0;
                var old = materialSlots[i]; if (old.ItemType != type || old.Stack != stack || old.Eligible != eligible) changed = true;
                materialSlots[i] = new BlueprintMaterialInventorySlot(i, type, stack, eligible);
#if DEBUG
                MaterialInventoryReads++;
#endif
            }
            if (!changed) return;
            materialInventoryArray = inventory;
            materialInventory = new BlueprintMaterialInventory(generation, ++materialInventoryRevision, tick, readyInventory,
                readyInventory ? (BlueprintMaterialInventorySlot[])materialSlots.Clone() : Array.Empty<BlueprintMaterialInventorySlot>());
        }
        private void EnsureMaterialInstances()
        {
            if (materialSceneIdentity == projectionSceneRevision) return;
            materialSceneIdentity = projectionSceneRevision; var result = new List<BlueprintMaterialInstance>();
            foreach (var item in projectionScene)
            {
                // The moving candidate and its restore are never two entries.
                // During editing the last fixed scene remains a pending view.
                string name = item.Name ?? "蓝图";
                result.Add(new BlueprintMaterialInstance(item.Identity.SessionGeneration, item.Identity.ProjectionGeneration,
                    item.Identity.ProjectionGeneration, name, item.OriginX, item.OriginY));
            }
            materialInstances = result.AsReadOnly(); CancelMaterialPublication();
        }
        private void InitializeMaterialMatch(BlueprintProjectionPlacement item)
        {
            item.Match.Materials = new BlueprintMaterialTotals { Expected = item.Match.Ranges.Length };
            materialMatchOwners++;
            materialAll.Expected += item.Match.Ranges.Length; materialAll.Revision++;
            if (materialCatalogTransition || materialContextTransition) materialTransitionRemaining += item.Match.Ranges.Length;
            CancelMaterialPublication();
        }
        private void ReleaseMaterialMatch(BlueprintProjectionPlacement item)
        {
            var state = item.Match; if (state?.Materials == null) return;
            materialMatchOwners--;
            materialAll.Expected -= state.Ranges.Length;
            foreach (var range in state.Ranges)
            {
                if ((materialCatalogTransition || materialContextTransition) && range.MaterialDemandEpoch != materialDemandEpoch && materialTransitionRemaining > 0) materialTransitionRemaining--;
                RevokeMaterialRange(range, true);
            }
            if (materialTransitionRemaining == 0) materialCatalogTransition = materialContextTransition = false;
            materialAll.Revision++; CancelMaterialPublication();
        }
        private void RevokeMaterialRange(BlueprintProjectionMatchRange range, bool retiring = false, bool resetBuild = false)
        {
            var value = range.Materials; if (value == null) return;
            if (value.Current)
            {
                value.Current = false;
                range.Owner.Match.Materials.Current--; materialAll.Current--;
                if (value.Unresolved) { range.Owner.Match.Materials.Unresolved--; materialAll.Unresolved--; }
            }
            value.Candidate = null; value.Progress = null;
            // A bill/context refresh keeps the no-spend native rejection's
            // bounded recovery. Only a changed placement identity/target or
            // retirement discards that wait together with its candidate walk.
            if (retiring || resetBuild)
            {
                ResetMaterialBuildWalk(range, true);
            }
            value.Desired = null; QueueMaterialChange(range);
            if (retiring) value.CatalogRevision = -1;
            CancelMaterialPublication();
        }
        private void QueueMaterialChange(BlueprintProjectionMatchRange range)
        {
            var value = range.Materials;
            if (value.Queued) return; value.Queued = true; value.Next = value.Desired; materialChanges.AddLast(range);
            range.Owner.Match.Materials.Updating++; materialAll.Updating++;
        }
        private void DrainMaterialChanges(int budget)
        {
            while (budget > 0 && materialChanges.First != null)
            {
                var range = materialChanges.First.Value; var value = range.Materials;
                // Match may have been retired/replaced; the old totals remain
                // owned by this cleanup, while only shared totals are live.
                var ownerTotals = value.OwnerTotals;
                if (value.Change == null)
                {
                    if (!value.Adding && value.Published != null) value.Change = value.Published.GetEnumerator();
                    else if (value.Next != null) { value.Adding = true; value.Change = value.Next.GetEnumerator(); }
                }
                if (value.Change != null && value.Change.MoveNext())
                {
                    var pair = value.Change.Current; long delta = value.Adding ? pair.Value : -pair.Value;
                    ownerTotals.Delta(pair.Key, delta); materialAll.Delta(pair.Key, delta); budget--;
#if DEBUG
                    MaterialContributionSteps++;
#endif
                    continue;
                }
                value.Change?.Dispose(); value.Change = null;
                if (!value.Adding) { value.Published = null; value.Adding = true; continue; }
                value.Published = value.Next; value.Next = null; value.Adding = false;
                if (!ReferenceEquals(value.Published, value.Desired)) { value.Next = value.Desired; continue; }
                value.Queued = false;
                ownerTotals.Updating--; materialAll.Updating--; materialChanges.RemoveFirst();
                if (value.Published != null && value.CatalogRevision == materialPlacementIdentity && range.Owner.Match?.Materials == ownerTotals)
                {
                    value.Current = true; value.Unresolved = value.NextStatus != BlueprintMaterialStatus.Exact;
                    ownerTotals.Current++; materialAll.Current++;
                    if (value.Unresolved) { ownerTotals.Unresolved++; materialAll.Unresolved++; }
                }
                ownerTotals.Revision++; materialAll.Revision++; budget--;
            }
        }
        private void DemandMaterialSources(int budget)
        {
            if (!materialCatalogTransition && !materialContextTransition && materialAll.Current == materialAll.Expected && materialAll.Updating == 0) return;
            int count = projectionScene.Count; if (count == 0) return;
            while (budget-- > 0 && count != 0)
            {
                materialDemandSceneCursor %= count; var item = projectionScene[materialDemandSceneCursor]; var match = item.Match;
                materialDemandSceneCursor = (materialDemandSceneCursor + 1) % count;
                if (match == null || match.Ranges.Length == 0) continue;
                match.MaterialDemandCursor %= match.Ranges.Length;
                var range = match.Ranges[match.MaterialDemandCursor]; match.MaterialDemandCursor = (match.MaterialDemandCursor + 1) % match.Ranges.Length;
                if (range.MaterialDemandEpoch != materialDemandEpoch)
                {
                    if (materialContextTransition && range.MaterialCrossLayers) InvalidateMaterialCrossLayer(range);
                    range.MaterialDemandEpoch = materialDemandEpoch;
                    if (materialTransitionRemaining > 0 && --materialTransitionRemaining == 0) materialCatalogTransition = materialContextTransition = false;
                }
                var value = range.Materials;
                if (value != null && value.CatalogRevision != materialPlacementIdentity) { RevokeMaterialRange(range, resetBuild: true); value.CatalogRevision = materialPlacementIdentity; }
                if (value == null || value.CatalogRevision != materialPlacementIdentity || !value.Current && !value.Queued && value.Progress == null)
                {
                    DemandProjectionMatchRange(range);
#if DEBUG
                    MaterialSourceDemands++;
#endif
                }
                // A complete stable scene does not need a recurring range
                // walk. The cursor resumes only while evidence remains missing.
                if (!materialCatalogTransition && !materialContextTransition && materialAll.Current == materialAll.Expected && materialAll.Updating == 0) break;
            }
        }
        private void CancelMaterialPublication()
        {
            materialPublishing?.Dispose(); materialPublishing = null; materialPublishingRows.Clear(); materialPublishingTotals = null;
            if (materialWorldSnapshot.Status == BlueprintMaterialStatus.Pending && materialWorldSnapshot.Rows.Count == 0) return;
            materialWorldSnapshot = new BlueprintMaterialSnapshot(++materialSnapshotRevision, BlueprintMaterialStatus.Pending, Array.Empty<BlueprintMaterialRow>());
        }
        private void PublishMaterialWorld(int budget)
        {
            EnsureMaterialInstances(); BlueprintMaterialTotals totals = materialAll;
            if (materialScope != 0)
            {
                foreach (var item in projectionScene) if (item.Identity.ProjectionGeneration == materialScope) { totals = item.Match?.Materials; break; }
            }
            // A fixed scene can precede its first Match owner/source arrival.
            // Missing ownership or a rebuilding catalog is Pending, not an
            // empty exact bill and never a reason to delay the original Match.
            if (totals == null || ProjectionEditing || materialCatalogTransition || materialContextTransition || totals.Updating > 0 ||
                projectionScene.Count > 0 && MaterialCatalog == null || materialScope == 0 && materialMatchOwners != projectionScene.Count) return;
            long inventoryRevision = materialInventory?.Revision ?? 0;
            if (ReferenceEquals(materialPublishingTotals, totals) && materialPublishingRevision == totals.Revision && materialPublishingInventoryRevision == inventoryRevision && materialPublishing == null) return;
            if (!ReferenceEquals(materialPublishingTotals, totals) || materialPublishingRevision != totals.Revision || materialPublishingInventoryRevision != inventoryRevision)
            {
                materialPublishing?.Dispose(); materialPublishingRows.Clear(); materialPublishingTotals = totals;
                materialPublishingRevision = totals.Revision; materialPublishingInventoryRevision = inventoryRevision;
                materialPublishingTotal = 0;
                materialPublishing = totals.Counts.GetEnumerator();
                materialWorldSnapshot = new BlueprintMaterialSnapshot(++materialSnapshotRevision, BlueprintMaterialStatus.Pending, Array.Empty<BlueprintMaterialRow>());
            }
            while (budget-- > 0 && materialPublishing.MoveNext())
            {
                var pair = materialPublishing.Current;
                try { materialPublishingTotal = checked(materialPublishingTotal + pair.Value); }
                catch (OverflowException) { totals.Failed = true; materialPublishing.Dispose(); materialPublishing = null; materialWorldSnapshot = new BlueprintMaterialSnapshot(++materialSnapshotRevision, BlueprintMaterialStatus.Unresolved, Array.Empty<BlueprintMaterialRow>(), false, "材料数量超出整数范围"); return; }
                materialPublishingRows.Add(new BlueprintMaterialRow(pair.Key, pair.Value, materialInventory?.Held(pair.Key) ?? 0, materialInventory?.Ready == true));
#if DEBUG
                MaterialPublicationSteps++;
#endif
            }
            if (budget < 0) return;
            materialPublishing.Dispose(); materialPublishing = null;
            var status = totals.Failed || totals.Unresolved > 0 ? BlueprintMaterialStatus.Unresolved : totals.Current < totals.Expected ? BlueprintMaterialStatus.Pending : BlueprintMaterialStatus.Exact;
            materialWorldSnapshot = BlueprintMaterialSnapshot.FromOwnedRows(++materialSnapshotRevision, status, materialPublishingRows, materialPublishingTotal, materialInventory?.Ready == true, totals.Failed ? "材料数量超出整数范围" : null);
            materialPublishingRows = new List<BlueprintMaterialRow>();
        }
        internal void ClearMaterials()
        {
            materialInventory = null; materialInventoryArray = null; materialTickSet = false; materialViewDemand = false; materialStaticDemand = false;
            // A scene can exit halfway through an epoch walk. Its remaining
            // count belongs to that scene and must not hold the next scene
            // pending when the same frozen placement catalog is reused.
            materialCatalogTransition = materialContextTransition = materialObservedDynamicDemand = false; materialTransitionRemaining = 0;
            materialDemandEpoch++; materialScope = 0;
            materialMatchOwners = 0;
            materialAll.Counts.Clear(); materialAll.Expected = materialAll.Current = materialAll.Unresolved = materialAll.Updating = 0; materialAll.Failed = false; materialAll.Revision++;
            foreach (var range in materialChanges) range.Materials.Change?.Dispose(); materialChanges.Clear();
            materialInstances = Array.Empty<BlueprintMaterialInstance>(); materialSceneIdentity = -1;
            materialDraftKey = null; materialDraftExclusions = null; materialDraftSnapshot = BlueprintMaterialSnapshot.Pending; materialAssetKeys.Clear();
            materialDemandSceneCursor = materialBuildSceneCursor = materialBuildSearchBudget = 0;
            Library?.CancelMaterials(); CancelMaterialPublication();
        }
    }
}
