using System;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private void BeginMaterialSample(BlueprintProjectionMatchRead read)
        {
            var catalog = MaterialCatalog;
            if (!DynamicMaterialDemand || catalog == null) return;
            var value = read.Range.Materials;
            if (value == null) read.Range.Materials = value = new BlueprintRangeMaterials { OwnerTotals = read.Owner.Match.Materials, BuildEpoch = MaterialBuildEligibilityRevision };
            if (value.BuildEpoch != MaterialBuildEligibilityRevision)
            { value.BuildEpoch = MaterialBuildEligibilityRevision; value.BuildExhausted = false; value.CandidateCursor = 0; }
            // A placement-only refresh does not invalidate an unchanged final
            // target proof. Retain its contribution while this same bounded
            // read rebuilds candidates; catalog changes still revoke it.
            read.MaterialReuseContribution = value.Current && read.Range.Current && read.Range.Unknown == 0 && value.CatalogRevision == catalog.Revision;
            if (value.Current && value.CatalogRevision != catalog.Revision) RevokeMaterialRange(read.Range);
            if (read.Range.Offset == 0 || value.Progress == null || value.CatalogRevision != catalog.Revision)
            {
                value.Progress = new BlueprintMaterialAccumulator(); value.CatalogRevision = catalog.Revision;
                value.Candidate = value.CandidateFallback = null;
            }
            read.MaterialCatalog = catalog; read.Materials = value.Progress; read.MaterialBuildEpoch = value.BuildEpoch;
            read.MaterialBuildWakeRevision = value.BuildWakeRevision;
            read.MaterialSupportReady = MaterialBuildDemand?.Invoke() == true &&
                projectionChanges.TryGetBuildStamp(read.Range.Watch, read.Range.PartIndex, out read.MaterialSupportStamp);
        }
        private void SampleMaterialCell(BlueprintProjectionMatchRead read, CaptureCell target, BlueprintWorldMatchSample sample)
        {
            if (read.Materials == null || read.MaterialCatalog == null || target.ObjectId > 0 && target.Layers == CaptureLayer.Tile) return;
            // Complete-object Tile and member appearance belong to the object
            // proof below. A coincident wall/wire/actuator keeps its own cell.
            if (target.ObjectId > 0) target = target.WithoutTile();
            if (!target.HasContent) return;
            BlueprintMaterialSteps.RequiredSteps(target, sample, read.MaterialCatalog, read.Materials);
            if (MaterialBuildDemand?.Invoke() != true || !sample.Available) return;
            if ((target.Layers & CaptureLayer.Tile) != 0 && BlueprintMaterialSteps.SameBody(target, sample, read.MaterialCatalog) == BlueprintBodyState.Missing)
                OfferMaterialBuildTarget(read, target, read.MaterialCatalog.Tile(target), null, null, checked((read.Batch.Offset + read.CellIndex) * 2));
            if ((target.Layers & CaptureLayer.Wall) != 0 && BlueprintMaterialSteps.SameBody(target, sample, true) == BlueprintBodyState.Missing)
                OfferMaterialBuildTarget(read, target, read.MaterialCatalog.Wall(target.WallType), null, null, checked((read.Batch.Offset + read.CellIndex) * 2 + 1));
        }
        private void SampleMaterialObjectRead(BlueprintProjectionMatchRead read, BlueprintWorldMatchSample sample)
        {
            if (read.Materials == null || read.MaterialCatalog == null || read.ObjectTarget == null || read.ObjectTargets == null) return;
            if (read.MaterialObjectProof == null) { read.MaterialObjectProof = new BlueprintObjectBodyProof(read.ObjectTarget); read.MaterialObjectAppearance = new BlueprintMaterialAccumulator(); }
            read.MaterialObjectProof.Observe(sample);
            if (!sample.Available) return;
            int row = sample.Cell.Y - read.ObjectBounds.Top, column = sample.Cell.X - read.ObjectBounds.Left;
            if (row < 0 || row >= read.ObjectTarget.Height) return;
            if (read.ObjectTarget.Family == BlueprintWorldObjectFamily.Door)
            {
                if (read.ObjectBounds.Width == 1)
                {
                    for (int x = 0; x < read.ObjectTarget.Width; x++) AddMaterialObjectAppearance(read, row * read.ObjectTarget.Width + x, sample);
                    return;
                }
                if (read.ObjectTarget.Width == 1)
                {
                    // A source closed door counts one member per row. The
                    // current hinge appearance is its canonical material step.
                    if (sample.Cell.X == read.ObjectTarget.AnchorX) AddMaterialObjectAppearance(read, row, sample);
                    return;
                }
                int targetHinge = read.ObjectTarget.AnchorX - read.ObjectTarget.WorldX;
                int observedHinge = read.ObjectTarget.AnchorX - read.ObjectBounds.Left;
                column = column == observedHinge ? targetHinge : 1 - targetHinge;
            }
            if (column >= 0 && column < read.ObjectTarget.Width) AddMaterialObjectAppearance(read, row * read.ObjectTarget.Width + column, sample);
        }
        private static void AddMaterialObjectAppearance(BlueprintProjectionMatchRead read, int index, BlueprintWorldMatchSample sample)
        {
            var target = read.ObjectTargets.Cells[index]; target.Layers = CaptureLayer.Tile;
            BlueprintMaterialSteps.RequiredSteps(target, sample, read.MaterialCatalog, read.MaterialObjectAppearance, BlueprintBodyState.Same);
        }
        private void CompleteMaterialObject(BlueprintProjectionMatchRead read)
        {
            if (read.Materials == null || read.MaterialCatalog == null || read.ObjectTarget == null || read.ObjectTargets == null) return;
            BlueprintBodyState body = read.MaterialObjectProof?.Complete() ?? BlueprintBodyState.Unknown;
            read.MaterialObjectBodies[read.ObjectId] = body;
            var first = read.ObjectTargets.Cells[0];
            BlueprintMaterialSteps.ObjectBody(body, first, read.MaterialCatalog, read.Materials);
            if (body == BlueprintBodyState.Same && read.MaterialObjectAppearance != null)
            {
                // At most the locked paint/coating identities (31 + 2) occur
                // in this reducer, independent of the object's member count.
                foreach (var pair in read.MaterialObjectAppearance.Counts) read.Materials.Add(pair.Key, pair.Value);
                if (read.MaterialObjectAppearance.Status != BlueprintMaterialStatus.Exact) read.Materials.Unresolved(read.MaterialObjectAppearance.Error);
            }
            if (MaterialBuildDemand?.Invoke() == true && body == BlueprintBodyState.Missing)
                OfferMaterialBuildTarget(read, first, read.MaterialCatalog.Tile(first, read.ObjectTargets.Objects[0]),
                    read.ObjectTargets.Objects[0], read.ObjectTarget, 0);
            read.MaterialObjectProof = null; read.MaterialObjectAppearance = null;
        }
        private void SampleMaterialObjectCell(BlueprintProjectionMatchRead read, CaptureCell cell)
        {
            if (read.Materials == null || (cell.Layers & CaptureLayer.Tile) == 0 || cell.ObjectId <= 0) return;
            BlueprintBodyState body;
            if (!read.MaterialObjectBodies.TryGetValue(cell.ObjectId, out body) || body == BlueprintBodyState.Unknown) { read.Materials.Unresolved("对象尚未完整确认"); return; }
            if (body == BlueprintBodyState.Same) return;
            cell.Layers = CaptureLayer.Tile;
            BlueprintMaterialSteps.RequiredSteps(cell, new BlueprintWorldMatchSample(true, new CaptureCell()), read.MaterialCatalog, read.Materials, body);
        }
        private void CompleteMaterialSample(BlueprintProjectionMatchRead read)
        {
            var value = read.Range.Materials;
            if (read.Materials == null || value == null || !ReferenceEquals(value.Progress, read.Materials) || read.MaterialCatalog.Revision != materialPlacementIdentity) return;
            bool keepContribution = value.Current && read.MaterialReuseContribution && read.Range.ProgressUnknown == 0 &&
                value.Unresolved == (read.Materials.Status != BlueprintMaterialStatus.Exact);
            if (!keepContribution)
            {
                if (value.Current)
                {
                    // Availability/status really changed. Revoke that bill,
                    // but retain candidates proven by this fresh read.
                    var candidate = value.Candidate; var fallback = value.CandidateFallback;
                    RevokeMaterialRange(read.Range); value.Candidate = candidate; value.CandidateFallback = fallback;
                }
                value.Desired = read.Materials.Counts; value.NextStatus = read.Materials.Status;
            }
            value.Progress = null;
            if (MaterialBuildDemand?.Invoke() == true && (read.MaterialBuildEpoch != value.BuildEpoch ||
                read.MaterialBuildEpoch != MaterialBuildEligibilityRevision || read.MaterialBuildWakeRevision != value.BuildWakeRevision ||
                !read.MaterialSupportReady || !projectionChanges.IsBuildCurrent(read.Range.Watch, read.Range.PartIndex, read.MaterialSupportStamp)))
            { value.Candidate = value.CandidateFallback = null; value.CandidateCursor = 0; value.BuildExhausted = false; DemandProjectionMatchRange(read.Range); }
            // Falling below the cursor would repeat this epoch's first failed
            // candidate. Keep the range dormant until an actual eligibility or
            // world change, while later source ranges retain their own turns.
            else if (value.Candidate == null && value.CandidateCursor > 0 || value.Candidate == null && value.CandidateFallback == null) value.BuildExhausted = true;
            value.CandidateFallback = null;
            if (value.Candidate != null) value.Candidate.RangeStamp = read.Range.Version;
            if (!keepContribution) { QueueMaterialChange(read.Range); CancelMaterialPublication(); }
        }
        private void OfferMaterialBuildTarget(BlueprintProjectionMatchRead read, CaptureCell cell, BlueprintPlacementDescriptor descriptor,
            CaptureObject? obj, BlueprintWorldObject body, int ordinal)
        {
            if (descriptor == null || descriptor.Flexible || descriptor.RequiresSubstrate || descriptor.UseItemType <= 0 ||
                read.Range.Materials == null || read.Range.Materials.BuildExhausted || !read.MaterialSupportReady) return;
            var value = read.Range.Materials;
            int x = read.Owner.OriginX + cell.X, y = read.Owner.OriginY + cell.Y;
            var bounds = body == null ? new BlueprintProjectionBounds(x, y, 1, 1) : new BlueprintProjectionBounds(body.WorldX, body.WorldY, body.Width, body.Height);
            if (body?.Family == BlueprintWorldObjectFamily.Door)
            {
                // The accepted source can be an open 2x3 door, while the
                // original item places its closed 1x3 body on the same hinge.
                // Keep the source body's canonical identity and proof; only
                // this operation's footprint and click use the closed profile.
                bounds = new BlueprintProjectionBounds(body.AnchorX, body.WorldY, descriptor.Width, descriptor.Height);
                x = checked(body.AnchorX + descriptor.OriginX);
                y = checked(body.WorldY + descriptor.OriginY);
            }
            else if (obj.HasValue)
            {
                x = checked(read.Owner.OriginX + obj.Value.X + descriptor.OriginX);
                y = checked(read.Owner.OriginY + obj.Value.Y + descriptor.OriginY);
            }
            var target = new BlueprintBuildTarget { Identity = read.Owner.Identity, Mask = read.Owner.Mask, Range = read.Range,
                RangeStamp = read.Range.Version, SupportStamp = read.MaterialSupportStamp, PlacementRevision = read.MaterialCatalog.Revision, OriginX = read.Owner.OriginX, OriginY = read.Owner.OriginY,
                TargetX = x, TargetY = y, Ordinal = ordinal, Cell = cell, Object = obj ?? default(CaptureObject), Body = body,
                Bounds = bounds, Placement = descriptor };
            if (ordinal >= value.CandidateCursor)
            { if (value.Candidate == null) value.Candidate = target; }
            else if (value.CandidateFallback == null) value.CandidateFallback = target;
        }
        internal bool TryGetBuildTarget(out BlueprintBuildTarget target)
        {
            target = null;
            if (!HasSession || MaterialBuildDemand?.Invoke() != true || ProjectionEditing || ProjectionCaptureBlocked) return false;
            int count = projectionScene.Count; if (count == 0) return false;
            while (materialBuildSearchBudget-- > 0)
            {
                materialBuildSceneCursor %= count; var item = projectionScene[materialBuildSceneCursor]; var match = item.Match;
                materialBuildSceneCursor = (materialBuildSceneCursor + 1) % count;
                if (match == null || match.Ranges.Length == 0) continue;
                match.MaterialBuildCursor %= match.Ranges.Length;
                var range = match.Ranges[match.MaterialBuildCursor]; match.MaterialBuildCursor = (match.MaterialBuildCursor + 1) % match.Ranges.Length;
                var candidate = range.Materials?.Candidate;
                if (candidate != null && IsBuildTargetCurrent(candidate)) { target = candidate; return true; }
            }
            return false;
        }
        internal bool IsBuildTargetCurrent(BlueprintBuildTarget target)
        {
            if (target == null || !HasSession || MaterialBuildDemand?.Invoke() != true || ProjectionEditing || ProjectionCaptureBlocked ||
                target.Identity.SessionGeneration != generation || target.PlacementRevision != MaterialCatalog?.Revision) return false;
            var owner = target.Range?.Owner;
            return owner != null && projectionScene.Contains(owner) && owner.Identity.Equals(target.Identity) && ReferenceEquals(owner.Mask, target.Mask) &&
                owner.OriginX == target.OriginX && owner.OriginY == target.OriginY && owner.Match != null && target.Range.Current &&
                target.Range.Version == target.RangeStamp && ProjectionMatchReadCurrent(target.Range, target.Range.RawVersion) &&
                projectionChanges.IsBuildCurrent(target.Range.Watch, target.Range.PartIndex, target.SupportStamp);
        }
        internal void NotifyBuildAttempt(BlueprintBuildTarget target)
        {
            if (target == null || target.Range?.Materials == null) return;
            var value = target.Range.Materials;
            if (!ReferenceEquals(value.Candidate, target)) return;
            value.CandidateCursor = target.Ordinal == int.MaxValue ? 0 : target.Ordinal + 1; value.Candidate = null;
            if (!value.BuildExhausted && IsBuildTargetCurrent(target)) DemandProjectionMatchRange(target.Range);
        }
        internal void InvalidateBuildTargets(BlueprintProjectionIdentity identity = null)
        {
            foreach (var item in projectionScene)
            {
                if (identity != null && !identity.Equals(item.Identity) || item.Match == null) continue;
                foreach (var range in item.Match.Ranges) if (range.Materials != null) range.Materials.Candidate = range.Materials.CandidateFallback = null;
            }
        }
    }
}
