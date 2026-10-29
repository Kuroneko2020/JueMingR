using System;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        // Seeds own no transport, old operation, world proof or consumption
        // baseline. Rebind this lane from the current world before service.
        private sealed class DeferredBuildSeed
        {
            internal readonly BlueprintProjectionPlacement Owner;
            internal readonly BlueprintProjectionMatchRange Range;
            internal readonly BlueprintProjectionIdentity Identity;
            internal readonly BlueprintProjectionMask Mask;
            internal readonly int X, Y, Ordinal;
            internal readonly long Catalog;
            internal readonly CaptureCell Cell;
            internal readonly CaptureObject? Object;
            internal readonly BlueprintWorldObject Body;
            internal readonly BlueprintPlacementDescriptor Placement;
            internal DeferredBuildSeed(BlueprintBuildTarget target)
            {
                Owner = target.Range.Owner; Range = target.Range; Identity = target.Identity; Mask = target.Mask;
                X = target.OriginX; Y = target.OriginY; Ordinal = target.Ordinal; Catalog = target.PlacementRevision;
                Cell = target.Cell; Object = target.Body == null ? (CaptureObject?)null : target.Object;
                Body = target.Body; Placement = target.Placement;
            }
        }
        private enum PreferredBuildSearch { Found, BudgetPending, SourcePending, PassComplete, CurrentGroupInvalid }
        private sealed class BuildContinuation
        {
            internal Player Player;
            internal BuildSource Source;
            internal BuildSource[] Dependencies;
            internal BlueprintBuildOperation Operation;
            internal ToolBuildConsumer Consumer;
            internal ToolBuildCursorMode CursorMode;
            internal int SmartDirection;
            internal long Session, Selection, Group, Batch, Boundary;
            internal int Remaining, SceneLimit;
            internal bool DebtDue, Pending;
        }
        private DeferredBuildSeed deferredBuild, repeatedBuild;
        private BuildContinuation buildContinuation;
        private long buildContinuationGroup, buildContinuationBatch;

        private BuildSource CurrentBuildSourceHint(Player p)
        {
            var current = activeBuild; var use = buildTools?.Use;
            if (current == null || use?.Building != true || current.Token <= 0 || current.Token != use.Operation ||
                !ReferenceEquals(p, player) || !ReferenceEquals(current.Player, p) || current.Target.Identity.SessionGeneration != generation) return null;
            var source = current.Source;
            if (source.Slot < 0 || source.Slot >= 50 || !ReferenceEquals(p.inventory[source.Slot], source.Item) ||
                source.Item.type != source.Type || source.Item.stack <= 0 || !use.OwnsBuildMaterial(source.Item) ||
                !buildTools.BuildInventoryEligible(source.Slot, source.Item)) return null;
            return source;
        }
        private BuildSource PreferredBuildSourceHint(Player p)
        {
            var actual = CurrentBuildSourceHint(p);
            // A resumed intent is only a selection hint, never an owned lease.
            return actual ?? (BuildContinuationCurrent(p) ? buildContinuation.Source : null);
        }
        private bool BuildContinuationCurrent(Player p)
        {
            var group = buildContinuation;
            if (group == null || !ReferenceEquals(group.Player, p) || !ReferenceEquals(p, player) || group.Session != generation ||
                group.Selection != buildTools.SelectionIntent || buildMaterialPreparation?.Abandoning == true) return false;
            var source = group.Source;
            if (source.Slot < 0 || source.Slot >= 50 || !ReferenceEquals(p.inventory[source.Slot], source.Item) ||
                source.Item.type != source.Type || source.Item.stack <= 0 || !buildTools.BuildInventoryEligible(source.Slot, source.Item)) return false;
            foreach (var dependency in group.Dependencies)
                if (dependency.Slot < 0 || dependency.Slot >= 58 || !ReferenceEquals(p.inventory[dependency.Slot], dependency.Item) ||
                    !dependency.Cleanup && (dependency.Item.type != dependency.Type || dependency.Item.stack <= 0) ||
                    p.inventoryChestStack[dependency.Slot] || buildTools.Items.World.ManualMaterials.Contains(dependency.Item) ||
                    buildTools.ProtectedAfterYield(dependency.Item) || buildMaterialPreparation?.IsDisplaced(dependency.Item) == true && !dependency.Cleanup) return false;
            return true;
        }
        private static ToolBuildConsumer BuildConsumer(BlueprintBuildOperation operation, BlueprintBuildTarget target)
        { return operation?.Consumer ?? (target.Placement.Kind == BlueprintPlacementKind.Wall ? ToolBuildConsumer.Walls : ToolBuildConsumer.Tiles); }
        private static bool SameBuildContinuationOperation(BlueprintBuildOperation a, BlueprintBuildOperation b)
        {
            // PlaceBase and ordinary placement share the actual Tiles path.
            // Hammer Mode is an observed shape, not a borrowed tool setting.
            bool baseA = a == null || a.Kind == BlueprintBuildOperationKind.PlaceBase || a.Kind == BlueprintBuildOperationKind.ConvertSubstrate;
            bool baseB = b == null || b.Kind == BlueprintBuildOperationKind.PlaceBase || b.Kind == BlueprintBuildOperationKind.ConvertSubstrate;
            if (baseA || baseB) return baseA && baseB;
            if (a.Kind == BlueprintBuildOperationKind.Hammer && b.Kind == BlueprintBuildOperationKind.Hammer) return true;
            return a.Kind == b.Kind && a.Consumer == b.Consumer && a.Mode == b.Mode && a.SupplyType == b.SupplyType && a.Cycle == b.Cycle && a.BucketType == b.BucketType;
        }
        private static bool SameBuildSource(BuildFlight current, BuildFlight next, ulong materialSlots)
        {
            return current != null && next != null && next.Source.Slot == current.Source.Slot && ReferenceEquals(next.Source.Item, current.Source.Item) &&
                next.Source.Type == current.Source.Type && BuildConsumer(current.Operation, current.Target) == BuildConsumer(next.Operation, next.Target) &&
                next.CursorMode == current.CursorMode && next.SmartDirection == current.SmartDirection &&
                next.DependencySlots == materialSlots && SameBuildContinuationOperation(current.Operation, next.Operation) && SameBuildDependencies(current, next);
        }
        private static bool SameBuildOperation(BlueprintBuildOperation a, BlueprintBuildOperation b)
        { return a == null ? b == null : b != null && a.Kind == b.Kind && a.Consumer == b.Consumer && a.Mode == b.Mode && a.SupplyType == b.SupplyType && a.Cycle == b.Cycle; }
        private static bool SameBuildDependencies(BuildFlight a, BuildFlight b)
        { return SameBuildDependencies(a.Dependencies, b.Dependencies); }
        private static bool SameBuildDependencies(BuildSource[] a, BuildSource[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].Slot != b[i].Slot || a[i].Type != b[i].Type || !ReferenceEquals(a[i].Item, b[i].Item)) return false;
            return true;
        }
        private static bool SameBuildTarget(BlueprintBuildTarget left, BlueprintBuildTarget right)
        {
            return left != null && right != null && ReferenceEquals(left.Range, right.Range) && left.Identity.Equals(right.Identity) &&
                ReferenceEquals(left.Mask, right.Mask) && left.OriginX == right.OriginX && left.OriginY == right.OriginY && left.PlacementRevision == right.PlacementRevision &&
                left.Ordinal == right.Ordinal && SameBuildOperation(left.Operation, right.Operation) && left.Placement?.Kind == right.Placement?.Kind && left.WorldX == right.WorldX && left.WorldY == right.WorldY;
        }
        private bool IsUnfinishedBuildTarget(BlueprintBuildTarget target)
        { return activeBuild != null && !activeBuild.Checked && !activeBuild.Rejected && SameBuildTarget(activeBuild.Target, target); }
        private bool PrepareBuildCandidate(BlueprintBuildTarget target, Player p, ref int allowance, out BuildFlight result, BuildOperationPlan plan = null)
        {
            result = null; if (allowance <= 0) return false; allowance--;
#if DEBUG
            BuildCandidateChecks++;
#endif
            if (plan != null) target.Operation = CopyBuildOperation(plan.Operation);
            bool ready = TryPrepareSelectedBuild(target, p, out result, plan);
            if (!ready) CompletePreferredAttempt(target);
            return ready;
        }
        private void CompletePreferredAttempt(BlueprintBuildTarget target)
        {
            var value = target?.Range.Materials; var group = buildContinuation;
            if (target?.LocalPriority == true && group != null && value != null && value.PreferredGroup == group.Group && value.PreferredCursor == target.Ordinal)
                value.PreferredCursor = NextBuildSourceOrdinal(target.Ordinal, target.Cell, target.Body != null);
        }
        private bool PlanContinuesBuild(BlueprintBuildTarget target, BuildOperationPlan plan)
        {
            var group = buildContinuation;
            if (group == null || plan.ToolSlot != group.Source.Slot || plan.ToolType != group.Source.Type || !ReferenceEquals(plan.Tool, group.Source.Item) ||
                BuildConsumer(plan.Operation, target) != group.Consumer || plan.CursorMode != group.CursorMode || plan.SmartDirection != group.SmartDirection ||
                !SameBuildContinuationOperation(group.Operation, plan.Operation)) return false;
            if (plan.SupplyType > 0)
            {
                if (plan.SupplySlot < 0) return false;
                bool primary = false;
                foreach (var dependency in group.Dependencies)
                    if (dependency.Primary && dependency.Slot == plan.SupplySlot && dependency.Type == plan.SupplyType &&
                        ReferenceEquals(dependency.Item, plan.Supply) && ReferenceEquals(plan.Supply, group.Player.inventory[plan.SupplySlot])) primary = true;
                if (!primary && !(group.Source.Slot == plan.SupplySlot && group.Source.Type == plan.SupplyType)) return false;
            }
            return true;
        }
        private void RememberBuildContinuation(BuildFlight flight)
        {
            var plan = new BuildOperationPlan { Operation = flight.Operation, Tool = flight.Source.Item, ToolType = flight.Source.Type, ToolSlot = flight.Source.Slot,
                SupplyType = flight.Operation?.SupplyType ?? 0, SupplySlot = -1, CursorMode=flight.CursorMode, SmartDirection=flight.SmartDirection };
            foreach (var dependency in flight.Dependencies) if (dependency.Primary) { plan.SupplySlot = dependency.Slot; plan.Supply = dependency.Item; }
            bool current = BuildContinuationCurrent(flight.Player), samePlan = current && PlanContinuesBuild(flight.Target, plan);
            if (samePlan) CompletePreferredAttempt(flight.Target);
            if (samePlan && SameBuildDependencies(buildContinuation.Dependencies, flight.Dependencies))
            {
                if (buildContinuation.DebtDue)
                {
                    buildContinuation.Batch = ++buildContinuationBatch; buildContinuation.Boundary = projectionGeneration;
                    buildContinuation.Remaining = materialAll.Expected; buildContinuation.DebtDue = buildContinuation.Pending = false;
                    buildContinuation.SceneLimit = projectionScene.Count;
                }
                return;
            }
            // Budget/source pauses survive one ordinary service and new token.
            // Changing lease identity alone cannot restart the member walk.
            if (current && !samePlan && buildContinuation.Pending)
            {
                if (buildContinuation.DebtDue && deferredBuild == null)
                {
                    buildContinuation.Batch = ++buildContinuationBatch; buildContinuation.Boundary = projectionGeneration;
                    buildContinuation.Remaining = materialAll.Expected; buildContinuation.SceneLimit = projectionScene.Count;
                    buildContinuation.DebtDue = false;
                }
                return;
            }
            buildContinuation = new BuildContinuation { Player = flight.Player, Source = flight.Source, Dependencies = flight.Dependencies,
                Operation = CopyBuildOperation(flight.Operation), Consumer = BuildConsumer(flight.Operation, flight.Target), CursorMode=flight.CursorMode, SmartDirection=flight.SmartDirection, Session = generation,
                Selection = buildTools.SelectionIntent, Group = ++buildContinuationGroup, Batch = ++buildContinuationBatch,
                Boundary = projectionGeneration, Remaining = materialAll.Expected, SceneLimit = projectionScene.Count };
            repeatedBuild = null;
        }
        private void CompletePreferredRange(BlueprintProjectionMatchRange range)
        {
            var group = buildContinuation; var value = range.Materials;
            if (group == null || range.Owner.Identity.ProjectionGeneration > group.Boundary || range.BuildContinuationVisit == group.Batch) return;
            range.BuildContinuationVisit = group.Batch;
            if (value != null) value.PreferredVisitBatch = group.Batch;
            if (group.Remaining > 0) group.Remaining--;
        }
        private void RetirePreferredRange(BlueprintProjectionMatchRange range) { CompletePreferredRange(range); }
        private void ClearBuildContinuation() { buildContinuation = null; repeatedBuild = null; deferredBuild = null; }
        private PreferredBuildSearch TryPreferredRange(BlueprintProjectionMatchRange range, Player p, out BlueprintBuildTarget target, out BuildOperationPlan plan)
        {
            target = null; plan = null; var group = buildContinuation; var value = range.Materials;
            if (range.Owner.Identity.ProjectionGeneration > group.Boundary) return PreferredBuildSearch.PassComplete;
            if (value == null) range.Materials = value = new BlueprintRangeMaterials { OwnerTotals = range.Owner.Match.Materials, CatalogRevision = MaterialCatalog.Revision };
            if (range.BuildContinuationVisit == group.Batch && (value.PreferredGroup != group.Group || value.PreferredComplete)) return PreferredBuildSearch.PassComplete;
            if (!BuildBoundsOverlap(range.Source.Bounds, BuildNearBounds(range.Owner)))
            { CompletePreferredRange(range); return PreferredBuildSearch.PassComplete; }
            if (value.PreferredGroup != group.Group)
            {
                value.PreferredGroup = group.Group; value.PreferredCursor = 0; value.PreferredComplete = false; value.PreferredInitialized = false;
                value.PreferredRevisit = false;
            }
            var part = FindBuildSource(range); long support;
            if (part == null || !range.Group.Ready || !ProjectionMatchReadCurrent(range, range.RawVersion) ||
                !projectionChanges.TryGetBuildStamp(range.Watch, range.PartIndex, out support))
            { DemandProjectionMatchRange(range); CompletePreferredRange(range); return PreferredBuildSearch.SourcePending; }
            if (value.PreferredVersion != range.Version || value.PreferredPlayer != MaterialBuildEligibilityRevision ||
                value.PreferredWake != value.BuildWakeRevision || value.PreferredSupply != buildSupplyRevision)
            {
                value.PreferredVersion = range.Version; value.PreferredPlayer = MaterialBuildEligibilityRevision;
                value.PreferredWake = value.BuildWakeRevision; value.PreferredSupply = buildSupplyRevision; value.PreferredRevisit = true;
            }
            if (value.PreferredComplete && value.PreferredRevisit && value.PreferredCompletedBatch < group.Batch)
            { value.PreferredCursor = 0; value.PreferredInitialized = value.PreferredComplete = value.PreferredRevisit = false; }
            if (value.PreferredComplete)
            {
                // Own writes only merge a later revisit. They cannot expand
                // this finite batch and postpone ordinary service forever.
                CompletePreferredRange(range); return PreferredBuildSearch.PassComplete;
            }
            var bounds = BuildNearBounds(range.Owner); var cells = part.Window.Cells;
            if (!value.PreferredInitialized && part.Offset == 0)
            { value.PreferredInitialized = true; value.PreferredRevisit = false; value.PreferredCursor = BuildMemberOrdinal(BuildCellLowerBound(cells, bounds.Left, bounds.Top)); }
            if (BuildMemberIndex(value.PreferredCursor) < part.Offset)
            { DemandProjectionMatchRange(range); CompletePreferredRange(range); return PreferredBuildSearch.SourcePending; }
            int steps = BuildSourceSlice;
            while (materialBuildSourceBudget > 0 && steps-- > 0)
            {
                int index = BuildMemberIndex(value.PreferredCursor) - part.Offset;
                if (index >= cells.Count)
                {
                    if (part.Complete) { value.PreferredComplete = true; value.PreferredCompletedBatch = group.Batch; CompletePreferredRange(range); return PreferredBuildSearch.PassComplete; }
                    DemandProjectionMatchRange(range); CompletePreferredRange(range); return PreferredBuildSearch.SourcePending;
                }
                var cell = cells[index];
                if (part.LogicalObjectId == 0)
                {
                    if (cell.Y >= bounds.Bottom) { value.PreferredComplete = true; value.PreferredCompletedBatch = group.Batch; CompletePreferredRange(range); return PreferredBuildSearch.PassComplete; }
                    if (cell.X < bounds.Left || cell.X >= bounds.Right || cell.Y < bounds.Top)
                    { value.PreferredCursor = BuildMemberOrdinal(part.Offset + BuildCellLowerBound(cells, bounds.Left, cell.X >= bounds.Right ? cell.Y + 1 : Math.Max(cell.Y, bounds.Top))); continue; }
                }
                int ordinal = value.PreferredCursor;
                int next = NextBuildSourceOrdinal(ordinal, cell, part.LogicalObjectId > 0);
                materialBuildSourceBudget--;
#if DEBUG
                ProjectionMatchSourceSteps++;
#endif
                BlueprintBuildTarget candidate;
                if (TryCreateConstructionTarget(range.Owner, range, part, index, ordinal, support, out candidate) && !IsUnfinishedBuildTarget(candidate))
                {
                    BuildOperationPlan option; BlueprintBuildWait wait; ulong slots;
                    if (!TryPlanBuild(candidate, p, group.Source, out option, out wait, out slots)) RegisterPlannedBuildWait(candidate, wait, slots);
                    else if (PlanContinuesBuild(candidate, option))
                    {
                        // Found is only a prepared suggestion. A preceding
                        // native write can revoke its proof before admission;
                        // retain this ordinal until admitted or fully rejected,
                        // then replan it from current world without rewinding.
                        candidate.LocalPriority = true; target = candidate; plan = option; return PreferredBuildSearch.Found;
                    }
                }
                // Incompatibility skips only this candidate, never the rest of
                // the pass. Full preflight belongs only to the final winner.
                value.PreferredCursor = next;
            }
            return PreferredBuildSearch.BudgetPending;
        }
        private PreferredBuildSearch TryPreferredBuild(Player p, out BlueprintBuildTarget target, out BuildOperationPlan plan)
        {
            target = null; plan = null;
            if (!BuildContinuationCurrent(p)) return PreferredBuildSearch.CurrentGroupInvalid;
            var group = buildContinuation;
            if (group.DebtDue) return PreferredBuildSearch.PassComplete;
            if (repeatedBuild != null)
            {
                BlueprintBuildTarget repeated; BuildOperationPlan repeatPlan; bool pending;
                if (TryRebindBuildSeed(repeatedBuild, p, group.Source, out repeated, out repeatPlan, out pending) && PlanContinuesBuild(repeated, repeatPlan))
                { repeatedBuild = null; target = repeated; plan = repeatPlan; return PreferredBuildSearch.Found; }
                if (!pending) repeatedBuild = null;
            }
            bool pendingSource = false;
            // Four retained windows are a cache, not the candidate universe.
            int visits = buildSources.Count; var node = buildSources.First;
            for (int n = 0; n < visits && node != null && materialBuildSourceBudget > 0; n++)
            {
                var source = node.Value; node = node.Next;
                var result = TryPreferredRange(source.Range, p, out target, out plan);
                if (result == PreferredBuildSearch.Found) { group.Pending = true; return result; }
                pendingSource |= result == PreferredBuildSearch.SourcePending;
            }
            // Scene append cannot lengthen this batch's discovery ring. Old
            // removals compact into the prefix; admission identity excludes any
            // newly appended tail that later shifts inside that finite prefix.
            int count = Math.Min(projectionScene.Count, group.SceneLimit);
            bool pendingWalk = materialBuildSourceBudget <= 0;
            while ((group.Remaining > 0 || group.Pending) && materialBuildSearchBudget > 0 && materialBuildSourceBudget > 0 && count > 0)
            {
                materialBuildSearchBudget--;
                materialBuildSceneCursor %= count; var owner = projectionScene[materialBuildSceneCursor]; materialBuildSceneCursor = (materialBuildSceneCursor + 1) % count;
                var match = owner.Match; if (match == null || match.Ranges.Length == 0 || owner.Identity.ProjectionGeneration > group.Boundary) continue;
                match.MaterialBuildCursor %= match.Ranges.Length;
                var range = match.Ranges[match.MaterialBuildCursor]; match.MaterialBuildCursor = (match.MaterialBuildCursor + 1) % match.Ranges.Length;
                var result = TryPreferredRange(range, p, out target, out plan);
                if (result == PreferredBuildSearch.Found) { group.Pending = true; return result; }
                pendingSource |= result == PreferredBuildSearch.SourcePending;
                pendingWalk |= result == PreferredBuildSearch.BudgetPending;
            }
            if (group.Remaining == 0 && !pendingSource && !pendingWalk)
            { group.DebtDue = true; group.Pending = false; return PreferredBuildSearch.PassComplete; }
            group.Pending = true; return pendingSource ? PreferredBuildSearch.SourcePending : PreferredBuildSearch.BudgetPending;
        }
        private void RegisterPlannedBuildWait(BlueprintBuildTarget target, BlueprintBuildWait wait, ulong slots)
        {
            if ((wait & BlueprintBuildWait.Dynamic) != 0) WaitForDynamicBuild(target);
            if (wait != BlueprintBuildWait.None) WaitForBuild(target, wait, slots);
        }
        private bool DeferredBuildIdentityCurrent(DeferredBuildSeed seed)
        {
            var owner = seed.Owner; var range = seed.Range;
            return HasSession && seed.Identity.SessionGeneration == generation && seed.Catalog == MaterialCatalog?.Revision &&
                projectionScene.Contains(owner) && owner.Identity.Equals(seed.Identity) && ReferenceEquals(owner.Mask, seed.Mask) && owner.OriginX == seed.X && owner.OriginY == seed.Y &&
                ProjectionQualificationCurrent(owner) && owner.Summary.TargetVersion == MaterialCatalog.TargetVersion && owner.Match != null && range.Index >= 0 &&
                range.Index < owner.Match.Ranges.Length && ReferenceEquals(owner.Match.Ranges[range.Index], range);
        }
        private bool TryRebindBuildSeed(DeferredBuildSeed seed, Player p, BuildSource preferred, out BlueprintBuildTarget target, out BuildOperationPlan plan, out bool pending)
        {
            target = null; plan = null; pending = false;
            if (!DeferredBuildIdentityCurrent(seed)) return false;
            var range = seed.Range; long support;
            if (!range.Group.Ready || !ProjectionMatchReadCurrent(range, range.RawVersion) || !projectionChanges.TryGetBuildStamp(range.Watch, range.PartIndex, out support))
            { DemandProjectionMatchRange(range); pending = true; return false; }
            target = CreateMaterialBuildTarget(seed.Owner, range, seed.Cell, seed.Placement, seed.Object, seed.Body, seed.Ordinal, support, seed.Catalog);
            if (BuildLane(seed.Ordinal) != BlueprintBuildLane.TileBody)
            { target.TargetX = seed.X + seed.Cell.X; target.TargetY = seed.Y + seed.Cell.Y; target.Bounds = new BlueprintProjectionBounds(target.TargetX, target.TargetY, 1, 1); }
            target.LocalProof = target.LocalPriority = true;
            BlueprintBuildOperation operation;
            if (!TryPlanConstructionOperation(target, BuildLane(seed.Ordinal), out operation)) return false;
            target.Operation = operation;
            BlueprintBuildWait wait; ulong slots;
            if (!TryPlanBuild(target, p, preferred, out plan, out wait, out slots)) { RegisterPlannedBuildWait(target, wait, slots); return false; }
            return true;
        }
        private bool TryDeferredBuild(Player p, ref int allowance, out BuildFlight result)
        {
            result = null; var seed = deferredBuild; if (seed == null) return false;
            BlueprintBuildTarget target; BuildOperationPlan plan; bool pending;
            if (!TryRebindBuildSeed(seed, p, PreferredBuildSourceHint(p), out target, out plan, out pending))
            {
                if (!pending)
                {
                    var failedCandidate = seed.Range.Materials?.Candidate;
                    if (SeedBuildTarget(seed, failedCandidate)) NotifyBuildAttempt(failedCandidate);
                    deferredBuild = null;
                }
                return false;
            }
            var candidate = seed.Range.Materials?.Candidate;
            if (SeedBuildTarget(seed, candidate)) seed.Range.Materials.Candidate = target;
            if (PrepareBuildCandidate(target, p, ref allowance, out result, plan)) return true;
            NotifyBuildAttempt(target); deferredBuild = null; return false;
        }
        private void ServiceDeferredBuild(BlueprintBuildTarget target)
        {
            var seed = deferredBuild;
            if (seed != null && ReferenceEquals(seed.Range, target.Range) && seed.Ordinal == target.Ordinal && seed.Identity.Equals(target.Identity) &&
                ReferenceEquals(seed.Mask, target.Mask) && seed.X == target.OriginX && seed.Y == target.OriginY && seed.Catalog == target.PlacementRevision) deferredBuild = null;
        }
        private static bool SeedBuildTarget(DeferredBuildSeed seed, BlueprintBuildTarget target)
        {
            return target != null && ReferenceEquals(seed.Range, target.Range) && seed.Ordinal == target.Ordinal && seed.Identity.Equals(target.Identity) &&
                ReferenceEquals(seed.Mask, target.Mask) && seed.X == target.OriginX && seed.Y == target.OriginY && seed.Catalog == target.PlacementRevision;
        }
    }
}
