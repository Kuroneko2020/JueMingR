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
            read.MaterialStampReady = projectionChanges.TryGetMaterialStamp(read.Range.Watch, read.Range.PartIndex, out read.MaterialStamp);
            var value = read.Range.Materials;
            if (value == null) read.Range.Materials = value = new BlueprintRangeMaterials { OwnerTotals = read.Owner.Match.Materials, CatalogRevision = catalog.Revision };
            if (MaterialBuildDemand?.Invoke() == true && value.BuildEpoch == 0) ResetMaterialBuildWalk(read.Range, false);
            // A placement-only refresh does not invalidate an unchanged final
            // target proof. Retain its contribution while this same bounded
            // read rebuilds candidates; catalog changes still revoke it.
            read.MaterialReuseContribution = value.Current && read.Range.Current && read.Range.Unknown == 0 && value.CatalogRevision == catalog.Revision;
            // A context-invalidated bill may already be Pending when its
            // catalog changes. Its old rejection/cursor still belongs to that
            // old placement identity and must not survive the new catalog.
            if (value.CatalogRevision != catalog.Revision) RevokeMaterialRange(read.Range, resetBuild: true);
            if (read.Range.Offset == 0 || value.Progress == null || value.CatalogRevision != catalog.Revision)
            {
                value.Progress = new BlueprintMaterialAccumulator(); value.CatalogRevision = catalog.Revision;
            }
            read.MaterialCatalog = catalog; read.Materials = value.Progress;
            if(read.Owner.Summary.TargetVersion!=catalog.TargetVersion)read.Materials.Unresolved("此蓝图版本的施工内容尚未确认");
            RetainBuildSource(read);
        }
        private void SampleMaterialCell(BlueprintProjectionMatchRead read, CaptureCell target, BlueprintWorldMatchSample sample)
        {
            if (read.Materials == null || read.MaterialCatalog == null || target.ObjectId > 0 && target.Layers == CaptureLayer.Tile) return;
            // Complete-object Tile and member appearance belong to the object
            // proof below. A coincident wall/wire/actuator keeps its own cell.
            var context = MaterialSourceTarget(read, read.CellIndex);
            bool objectForeground = context.ObjectId > 0 && (context.Layers & CaptureLayer.Tile) != 0 &&
                read.MaterialCatalog.QualifyTile(context).Disposition==BlueprintTargetDisposition.Construction;
            // Even an unselected/masked foreground can block the native wall
            // scraper. Its later removal must release that Unknown result.
            read.MaterialCrossLayerSeen |= (context.Layers & CaptureLayer.Wall) != 0;
            if (target.ObjectId > 0) target = target.WithoutTile();
            if (!target.HasContent) return;
            BlueprintMaterialSteps.RequiredSteps(target, sample, read.MaterialCatalog, read.Materials, foregroundHandledElsewhere: objectForeground);
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
            var target = MaterialSourceTarget(read, index);
            read.MaterialCrossLayerSeen |= (target.Layers & (CaptureLayer.Tile | CaptureLayer.Wall)) == (CaptureLayer.Tile | CaptureLayer.Wall);
            // Canonical door forms can map two saved members to one hinge.
            // Only a wall at this actual member's coordinate can require its
            // foreground restoration; a different saved wing is not that wall.
            if (target.X + read.Owner.OriginX != sample.Cell.X || target.Y + read.Owner.OriginY != sample.Cell.Y)
                target.Layers &= ~CaptureLayer.Wall;
            BlueprintMaterialSteps.ObjectMemberAppearance(target, sample, read.MaterialCatalog, read.MaterialObjectAppearance,
                read.ObjectTargets.Objects.Count==1?read.ObjectTargets.Objects[0]:(CaptureObject?)null);
        }
        private static CaptureCell MaterialSourceTarget(BlueprintProjectionMatchRead read, int index)
        {
            var target = read.Batch.Window.Cells[index];
            // Ordinary source cells retain only context for the separately
            // owned furniture Tile. Apply this instance's same immutable mask
            // before deciding that the foreground was actually selected.
            if (read.Batch.LogicalObjectId == 0 && target.ObjectId > 0) target.Layers |= CaptureLayer.Tile;
            return read.Owner.Mask.Apply(target);
        }
        private void CompleteMaterialObject(BlueprintProjectionMatchRead read)
        {
            if (read.Materials == null || read.MaterialCatalog == null || read.ObjectTarget == null || read.ObjectTargets == null) return;
            BlueprintBodyState body = read.MaterialObjectProof?.Complete() ?? BlueprintBodyState.Unknown;
            read.MaterialObjectBodies[read.ObjectId] = body;
            var first = read.ObjectTargets.Cells[0];
            BlueprintMaterialSteps.ObjectBody(body, first, read.MaterialCatalog, read.Materials,
                read.ObjectTargets.Objects.Count==1?read.ObjectTargets.Objects[0]:(CaptureObject?)null);
            if (body == BlueprintBodyState.Same && read.MaterialObjectAppearance != null)
            {
                // At most the locked paint/coating identities (31 + 2) occur
                // in this reducer, independent of the object's member count.
                foreach (var pair in read.MaterialObjectAppearance.Counts) read.Materials.Add(pair.Key, pair.Value);
                if (read.MaterialObjectAppearance.Status != BlueprintMaterialStatus.Exact) read.Materials.Unresolved(read.MaterialObjectAppearance.Error);
            }
            read.MaterialObjectProof = null; read.MaterialObjectAppearance = null;
        }
        private void SampleMaterialObjectCell(BlueprintProjectionMatchRead read, CaptureCell cell)
        {
            if (read.Materials == null || (cell.Layers & CaptureLayer.Tile) == 0 || cell.ObjectId <= 0) return;
            BlueprintBodyState body;
            if (!read.MaterialObjectBodies.TryGetValue(cell.ObjectId, out body) || body == BlueprintBodyState.Unknown) { read.Materials.Unresolved("对象尚未完整确认"); return; }
            if (body == BlueprintBodyState.Same) return;
            cell.Layers = CaptureLayer.Tile;
            BlueprintMaterialSteps.RequiredSteps(cell, new BlueprintWorldMatchSample(true, new CaptureCell()), read.MaterialCatalog, read.Materials, body,
                obj:read.Batch.Window.Objects.Count==1?read.Batch.Window.Objects[0]:(CaptureObject?)null);
        }
        private void CompleteMaterialSample(BlueprintProjectionMatchRead read)
        {
            var value = read.Range.Materials;
            if (read.Materials == null || value == null || !ReferenceEquals(value.Progress, read.Materials) || read.MaterialCatalog.Revision != materialPlacementIdentity) return;
            if (!read.MaterialStampReady || !projectionChanges.IsMaterialCurrent(read.Range.Watch, read.Range.PartIndex, read.MaterialStamp))
            { InvalidateMaterialCrossLayer(read.Range); return; }
            read.Range.MaterialCrossLayers = read.MaterialCrossLayerSeen;
            bool keepContribution = value.Current && read.MaterialReuseContribution && read.Range.ProgressUnknown == 0 &&
                value.Unresolved == (read.Materials.Status != BlueprintMaterialStatus.Exact);
            if (!keepContribution)
            {
                if (value.Current)
                {
                    // Availability/status really changed. Revoke that bill,
                    // but retain candidates proven by this fresh read.
                    var candidate = value.Candidate;
                    RevokeMaterialRange(read.Range); value.Candidate = candidate;
                }
                value.Desired = read.Materials.Counts; value.NextStatus = read.Materials.Status;
            }
            value.Progress = null;
            // Source/world sampling completion is not construction exhaustion.
            // The independent ordinal consumes the same retained immutable
            // source, and only it may declare every candidate served.
            if (!keepContribution) { QueueMaterialChange(read.Range); CancelMaterialPublication(); }
        }
        // The existing immutable proof geometry routes just this native write.
        // A cold range remains conservative until its same-source material read
        // has established whether a wall target needs foreground context.
        // Known independent ranges do not inherit a neighbour's extra work.
        private Action<int> materialCrossLayerSink, materialCrossLayerEmit;
        private BlueprintProjectionMatchGroup materialCrossLayerGroup;
        private bool materialCrossLayerFound;
        private bool MaterialCrossLayerInterest(BlueprintProjectionSourceRange source, BlueprintProjectionMask mask,
            int originX, int originY, BlueprintProjectionBounds changed, ushort layers, BlueprintProjectionMatchGroup group)
        {
            if (!DynamicMaterialDemand || (layers & 3) == 0) return false;
            return RouteMaterialCrossLayers(source, mask, originX, originY, changed, group, null);
        }
        private void RouteMaterialCrossLayer(BlueprintProjectionSourceRange source, BlueprintProjectionMask mask,
            int originX, int originY, BlueprintProjectionBounds changed, ushort layers, BlueprintProjectionMatchGroup group, Action<int> emit)
        {
            if (!DynamicMaterialDemand || (layers & 3) == 0) return;
            RouteMaterialCrossLayers(source, mask, originX, originY, changed, group, emit);
        }
        private bool RouteMaterialCrossLayers(BlueprintProjectionSourceRange source, BlueprintProjectionMask mask,
            int originX, int originY, BlueprintProjectionBounds changed, BlueprintProjectionMatchGroup group, Action<int> emit)
        {
            var local = new BlueprintProjectionBounds(changed.Left - originX, changed.Top - originY, changed.Width, changed.Height);
            // Native write routing is synchronous on this Host's game thread.
            // The immutable geometry visits only this sink, whose optional emit
            // marks observer bits and never writes the world. Reuse one sink;
            // per-store captured delegates would allocate on paint hot paths.
            if (materialCrossLayerSink == null) materialCrossLayerSink = AcceptMaterialCrossLayer;
            materialCrossLayerGroup = group; materialCrossLayerEmit = emit; materialCrossLayerFound = false;
            try
            {
                source.RouteProofs(local, 3, mask, materialCrossLayerSink);
                return materialCrossLayerFound;
            }
            finally { materialCrossLayerGroup = null; materialCrossLayerEmit = null; }
        }
        private void AcceptMaterialCrossLayer(int ordinal)
        {
            var group = materialCrossLayerGroup;
            if (group == null || ordinal < 0 || ordinal >= group.Parts.Count || !group.Parts[ordinal].MaterialCrossLayers) return;
            materialCrossLayerFound = true; materialCrossLayerEmit?.Invoke(ordinal);
        }
        private void InvalidateMaterialCrossLayer(BlueprintProjectionMatchRange range)
        {
            if (!range.MaterialCrossLayers) return;
            // Keep the final target proof and its version. Only this material
            // dependency and partial sample are obsolete; the immutable source
            // transport/other completed siblings remain reusable.
            ResetProjectionMatchRead(range); range.Reading = false; range.Offset = 0;
            RevokeMaterialRange(range); DemandProjectionMatchRange(range);
        }
        private static BlueprintBuildTarget CreateMaterialBuildTarget(BlueprintProjectionPlacement owner, BlueprintProjectionMatchRange range,
            CaptureCell cell, BlueprintPlacementDescriptor descriptor, CaptureObject? obj, BlueprintWorldObject body, int ordinal, long support, long catalog)
        {
            int x = owner.OriginX + cell.X, y = owner.OriginY + cell.Y;
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
                x = checked(owner.OriginX + obj.Value.X + descriptor.OriginX);
                y = checked(owner.OriginY + obj.Value.Y + descriptor.OriginY);
            }
            return new BlueprintBuildTarget { Identity = owner.Identity, Mask = owner.Mask, Range = range,
                RangeStamp = range.Version, SupportStamp = support, PlacementRevision = catalog, OriginX = owner.OriginX, OriginY = owner.OriginY,
                TargetX = x, TargetY = y, Ordinal = ordinal, Cell = cell, Object = obj ?? default(CaptureObject), Body = body,
                Bounds = bounds, Placement = descriptor };
        }
        internal bool TryGetBuildTarget(out BlueprintBuildTarget target)
        {
            target = null;
            if (!HasSession || MaterialBuildDemand?.Invoke() != true || ProjectionEditing || ProjectionCaptureBlocked) return false;
            int count = projectionScene.Count; if (count == 0) return false;
            if (TryRetainedBuildTarget(out target)) return true;
            while (materialBuildSearchBudget-- > 0)
            {
                materialBuildSceneCursor %= count; var item = projectionScene[materialBuildSceneCursor]; var match = item.Match;
                materialBuildSceneCursor = (materialBuildSceneCursor + 1) % count;
                if (match == null || match.Ranges.Length == 0) continue;
                match.MaterialBuildCursor %= match.Ranges.Length;
                var range = match.Ranges[match.MaterialBuildCursor]; match.MaterialBuildCursor = (match.MaterialBuildCursor + 1) % match.Ranges.Length;
                if (TryMaterialBuildTarget(range, out target)) return true;
            }
            return false;
        }
        private bool TryMaterialBuildTarget(BlueprintProjectionMatchRange range, out BlueprintBuildTarget target)
        {
            TryWakeBuildEligibility(range);
            var candidate = range.Materials?.Candidate;
            if (candidate != null && IsBuildTargetCurrent(candidate)) { target = candidate; return true; }
            if (candidate != null) range.Materials.Candidate = null;
            return ContinueMaterialBuildSource(range, out target);
        }
        internal bool IsBuildTargetCurrent(BlueprintBuildTarget target)
        {
            return IsBuildTargetIdentityCurrent(target) && projectionChanges.IsBuildCurrent(target.Range.Watch, target.Range.PartIndex, target.SupportStamp);
        }
        private bool IsBuildTargetIdentityCurrent(BlueprintBuildTarget target)
        {
            // This half is never an admission by itself. Only the exact
            // native torch-preparation receipt may replace the support stamp;
            // scene/mask/catalog/raw-target identity is still mandatory.
            if (target == null || !HasSession || MaterialBuildDemand?.Invoke() != true || ProjectionEditing || ProjectionCaptureBlocked ||
                target.Identity.SessionGeneration != generation || target.PlacementRevision != MaterialCatalog?.Revision) return false;
            var owner = target.Range?.Owner;
            return owner != null && ProjectionQualificationCurrent(owner) && owner.Summary.TargetVersion==MaterialCatalog.TargetVersion && projectionScene.Contains(owner) && owner.Identity.Equals(target.Identity) && ReferenceEquals(owner.Mask, target.Mask) &&
                owner.OriginX == target.OriginX && owner.OriginY == target.OriginY && owner.Match != null && (target.LocalProof || target.Range.Current) &&
                target.Range.Version == target.RangeStamp && ProjectionMatchReadCurrent(target.Range, target.Range.RawVersion);
        }
        internal void NotifyBuildAttempt(BlueprintBuildTarget target)
        {
            if (target == null || target.Range?.Materials == null) return;
            var value = target.Range.Materials;
            if (!ReferenceEquals(value.Candidate, target)) return;
            if(!target.LocalPriority)value.CandidateCursor = NextBuildSourceOrdinal(target.Ordinal, target.Cell, target.Body != null);
            value.Candidate = null;
        }
        internal void InvalidateBuildTargets(BlueprintProjectionIdentity identity = null)
        {
            foreach (var item in projectionScene)
            {
                if (identity != null && !identity.Equals(item.Identity) || item.Match == null) continue;
                foreach (var range in item.Match.Ranges) if (range.Materials != null) range.Materials.Candidate = null;
            }
        }
    }
}
