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
        private sealed class RepeatedBuildSeed
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
            internal RepeatedBuildSeed(BlueprintBuildTarget target,BlueprintWorldObject sharedBody=null)
            {
                target = target.Requirement ?? target;
                Owner = target.Range.Owner; Range = target.Range; Identity = target.Identity; Mask = target.Mask;
                X = target.OriginX; Y = target.OriginY; Ordinal = target.Ordinal; Catalog = target.PlacementRevision;
                Cell = target.Cell; Object = target.Body == null ? (CaptureObject?)null : target.Object;
                Body = sharedBody ?? target.Body; Placement = target.Placement;
            }
        }
        private enum PreferredBuildSearch { Found, BudgetPending, SourcePending, OwnerPending, NoRunnable, CurrentGroupInvalid }
        // Business preference has no inventory/input ownership. Its source is
        // a rebindable hint; every admitted flight keeps its own exact lease.
        private sealed class BuildContinuation
        {
            internal Player Player;
            internal BuildSource Source;
            internal BlueprintBuildOperation Operation;
            internal ToolBuildConsumer Consumer;
            internal BlueprintMaterialInventory HintInventory;
            internal long Session, Selection, Group, Policy;
            internal bool Pending=false;
        }
        private BuildContinuation buildContinuation;
        private long buildContinuationGroup;

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
            if(actual!=null)return actual;
            if(!BuildContinuationCurrent(p))return null;
            var group=buildContinuation;var source=group.Source;
            if(BuildHintCurrent(p,source))return source;
            // One exhausted/moved Item does not end its business material.
            // Rebind only through the existing qualified main-bag publication;
            // a returned/displaced endpoint never becomes a type-only permit.
            if(ReferenceEquals(group.HintInventory,MaterialInventory))return null;
            group.HintInventory=MaterialInventory;
            if(MaterialInventory?.Ready==true)foreach(var slot in MaterialInventory.Slots)
                if(slot.Eligible && slot.ItemType==source.Type && slot.Stack>0 && slot.Slot>=0 && slot.Slot<50 &&
                    buildTools.BuildInventoryEligible(slot.Slot,p.inventory[slot.Slot]))
                {group.Source=SnapshotSource(slot.Slot,p.inventory[slot.Slot]);return group.Source;}
            return null;
        }
        private bool BuildHintCurrent(Player p,BuildSource source)
        {return source!=null && source.Slot>=0 && source.Slot<50 && ReferenceEquals(p.inventory[source.Slot],source.Item) &&
            source.Item.type==source.Type && source.Item.stack>0 && buildTools.BuildInventoryEligible(source.Slot,source.Item);}
        private bool BuildContinuationCurrent(Player p)
        {
            var group = buildContinuation;
            if (group == null || !ReferenceEquals(group.Player, p) || !ReferenceEquals(p, player) || group.Session != generation ||
                group.Selection != buildTools.SelectionIntent || group.Policy!=substitutionRevision || buildMaterialPreparation?.Abandoning == true) return false;
            return true;
        }
        private static ToolBuildConsumer BuildConsumer(BlueprintBuildOperation operation, BlueprintBuildTarget target)
        { return operation?.Consumer ?? (target.Placement.Kind == BlueprintPlacementKind.Wall ? ToolBuildConsumer.Walls : ToolBuildConsumer.Tiles); }
        private static bool SameBuildContinuationOperation(BlueprintBuildOperation a, BlueprintBuildOperation b)
        {
            // PlaceBase and ordinary placement share the actual Tiles path.
            // Hammer Mode is an observed shape, not a borrowed tool setting.
            bool baseA = a == null || a.Kind == BlueprintBuildOperationKind.PlaceBase || a.Kind == BlueprintBuildOperationKind.ConvertSubstrate || a.Kind == BlueprintBuildOperationKind.HoikBase || a.Kind == BlueprintBuildOperationKind.HoikSwap;
            bool baseB = b == null || b.Kind == BlueprintBuildOperationKind.PlaceBase || b.Kind == BlueprintBuildOperationKind.ConvertSubstrate || b.Kind == BlueprintBuildOperationKind.HoikBase || b.Kind == BlueprintBuildOperationKind.HoikSwap;
            if (baseA || baseB) return baseA && baseB;
            if (a.Kind == BlueprintBuildOperationKind.Hammer && b.Kind == BlueprintBuildOperationKind.Hammer) return true;
            return a.Kind == b.Kind && a.Consumer == b.Consumer && a.Mode == b.Mode && a.SupplyType == b.SupplyType && a.Cycle == b.Cycle && a.BucketType == b.BucketType;
        }
        private static bool SameBuildSource(BuildFlight current, BuildFlight next, ulong materialSlots)
        {
            return current != null && next != null && next.Source.Slot == current.Source.Slot && ReferenceEquals(next.Source.Item, current.Source.Item) &&
                next.Source.Type == current.Source.Type && BuildConsumer(current.Operation, current.Target) == BuildConsumer(next.Operation, next.Target) &&
                next.CursorMode == current.CursorMode && next.SmartDirection == current.SmartDirection && next.SmartDirectionY == current.SmartDirectionY &&
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
            left = left?.Requirement ?? left; right = right?.Requirement ?? right;
            return left != null && right != null && ReferenceEquals(left.Range, right.Range) && left.Identity.Equals(right.Identity) &&
                ReferenceEquals(left.Mask, right.Mask) && left.OriginX == right.OriginX && left.OriginY == right.OriginY && left.PlacementRevision == right.PlacementRevision &&
                left.PolicyRevision == right.PolicyRevision &&
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
            target.Readiness=BlueprintBuildReadiness.Deferred;
            bool ready;
            nearWaitingFact=nearSelected!=null && ReferenceEquals(nearSelected.Seed.Range,target.Range) && nearSelected.Seed.Ordinal==target.Ordinal?nearSelected:null;
            if(nearWaitingFact!=null){nearWaitingFact.Wait=BlueprintBuildWait.None;nearWaitingFact.Dependencies=0;}
            try{ready=TryPrepareSelectedBuild(target,p,out result,plan);}finally{nearWaitingFact=null;}
            if(plan?.ExecutionTarget!=null)target.Readiness=plan.ExecutionTarget.Readiness;
            if(ready)target.Readiness=BlueprintBuildReadiness.Ready;
            if (!ready) RejectNearBuildCandidate(target);
            return ready;
        }
        private bool PlanContinuesBuild(BlueprintBuildTarget target, BuildOperationPlan plan)
        {return plan!=null && OperationContinuesBuild(target,plan.Operation,plan.ToolType,plan.SupplyType);}
        private bool FlightContinuesBuild(BuildFlight flight)
        {return flight!=null && OperationContinuesBuild(flight.Target,flight.Operation,flight.Source.Type,flight.Operation?.SupplyType??0);}
        private bool OperationContinuesBuild(BlueprintBuildTarget target,BlueprintBuildOperation operation,int toolType,int supplyType)
        { return OperationContinuesBuild(buildContinuation,operation,toolType,supplyType,BuildConsumer(operation,target)); }
        private static bool OperationContinuesBuild(BuildContinuation group,BlueprintBuildOperation operation,int toolType,int supplyType,ToolBuildConsumer consumer)
        {
            if(group==null)return false;
            var a=group.Operation;var b=operation;
            bool ordinaryA=a==null || a.Kind==BlueprintBuildOperationKind.PlaceBase || a.Kind==BlueprintBuildOperationKind.ConvertSubstrate || a.Kind==BlueprintBuildOperationKind.HoikBase || a.Kind==BlueprintBuildOperationKind.HoikSwap;
            bool ordinaryB=b==null || b.Kind==BlueprintBuildOperationKind.PlaceBase || b.Kind==BlueprintBuildOperationKind.ConvertSubstrate || b.Kind==BlueprintBuildOperationKind.HoikBase || b.Kind==BlueprintBuildOperationKind.HoikSwap;
            if(ordinaryA || ordinaryB)return ordinaryA && ordinaryB && group.Source.Type==toolType && group.Consumer==consumer;
            if(b==null || a.Kind!=b.Kind)return false;
            if(a.Kind==BlueprintBuildOperationKind.Hammer || a.Kind==BlueprintBuildOperationKind.Scrape)return true;
            if(a.Kind==BlueprintBuildOperationKind.PaintTile || a.Kind==BlueprintBuildOperationKind.PaintWall)
                return a.Mode==b.Mode && a.SupplyType==supplyType && a.Consumer==b.Consumer;
            if(a.Kind==BlueprintBuildOperationKind.AddWire || a.Kind==BlueprintBuildOperationKind.SetInactive)
                return a.Mode==b.Mode && a.SupplyType==b.SupplyType;
            if(a.Kind==BlueprintBuildOperationKind.AddActuator || a.Kind==BlueprintBuildOperationKind.RemoveOwnedActuator)
                return a.Mode==b.Mode && a.SupplyType==b.SupplyType;
            var left=a.NativePlacement;var right=b.NativePlacement;
            return group.Source.Type==toolType && a.SupplyType==supplyType && a.BucketType==b.BucketType &&
                left?.TileType==right?.TileType && left?.Style==right?.Style && left?.Producer?.Kind==right?.Producer?.Kind &&
                left?.Producer?.OptionIndex==right?.Producer?.OptionIndex && left?.Producer?.RandomResidue==right?.Producer?.RandomResidue;
        }
        private bool PreparedBuildPriorityCurrent(BuildFlight next,Player p,bool admission=false)
        {
            if(!BuildContinuationCurrent(p) || FlightContinuesBuild(next))return true;
            return NearBuildAbsenceCurrent(p,admission);
        }
        private bool PreferredBuildAbsenceCurrent(Player p)
        {
            return NearBuildAbsenceCurrent(p,true);
        }

        private void RememberBuildContinuation(BuildFlight flight)
        {
            var plan = new BuildOperationPlan { Operation = flight.Operation, Tool = flight.Source.Item, ToolType = flight.Source.Type, ToolSlot = flight.Source.Slot,
                SupplyType = flight.Operation?.SupplyType ?? 0, SupplySlot = -1, CursorMode=flight.CursorMode, SmartDirection=flight.SmartDirection, SmartDirectionY=flight.SmartDirectionY };
            foreach (var dependency in flight.Dependencies) if (dependency.Primary) { plan.SupplySlot = dependency.Slot; plan.Supply = dependency.Item; }
            bool samePlan=BuildContinuationCurrent(flight.Player) && PlanContinuesBuild(flight.Target,plan);
            if(samePlan)
            {buildContinuation.Source=flight.Source;buildContinuation.HintInventory=MaterialInventory;return;}
            // A natural material change only replaces business preference.
            // Shared source semantics and other groups keep their progress.
            buildContinuation = new BuildContinuation { Player=flight.Player,Source=flight.Source,HintInventory=MaterialInventory,
                Operation=CopyBuildOperation(flight.Operation),Consumer=BuildConsumer(flight.Operation,flight.Target),Session=generation,
                Selection=buildTools.SelectionIntent,Policy=substitutionRevision,Group=++buildContinuationGroup };
        }
        private void RetireNearBuildRange(BlueprintProjectionMatchRange range)
        {
            NearBuildRange state;if(nearBuildRanges.TryGetValue(range,out state))ReleaseNearBuildRange(state);
            nearBuildRanges.Remove(range);nearBuildDiscovery.Members.Remove(range);nearBuildDiscovery.Ranges.Remove(range);nearPlanDirty=nearVisitNeeded=true;
        }
        private void ClearBuildContinuation()
        {
            buildContinuation=null;ClearNearBuildPlan();
        }
        private void InvalidateNearBuildRange(BlueprintProjectionMatchRange range)
        {
            // Broad material publication may include unrelated C recovery.
            // Each fact validates its own wait, source and world stamps; this
            // notification requests proof aggregation without erasing semantics.
            if(nearBuildRanges.ContainsKey(range))nearPlanDirty=true;
        }
        private PreferredBuildSearch TryPreferredBuild(Player p, out BlueprintBuildTarget target, out BuildOperationPlan plan)
        {
            target=null;plan=null;
            if(!BuildContinuationCurrent(p))return PreferredBuildSearch.CurrentGroupInvalid;
            return ReadNearBuildPreference(p,out target,out plan);
        }

        private void RegisterPlannedBuildWait(BlueprintBuildTarget target, BlueprintBuildWait wait, ulong slots)
        {
            if ((wait & BlueprintBuildWait.Dynamic) != 0) WaitForDynamicBuild(target);
            if (wait != BlueprintBuildWait.None) WaitForBuild(target, wait, slots);
        }
        private bool RepeatedBuildIdentityCurrent(RepeatedBuildSeed seed)
        {
            var owner = seed.Owner; var range = seed.Range;
            return HasSession && seed.Identity.SessionGeneration == generation && seed.Catalog == MaterialCatalog?.Revision &&
                projectionScene.Contains(owner) && owner.Identity.Equals(seed.Identity) && ReferenceEquals(owner.Mask, seed.Mask) && owner.OriginX == seed.X && owner.OriginY == seed.Y &&
                ProjectionQualificationCurrent(owner) && owner.Summary.TargetVersion == MaterialCatalog.TargetVersion && owner.Match != null && range.Index >= 0 &&
                range.Index < owner.Match.Ranges.Length && ReferenceEquals(owner.Match.Ranges[range.Index], range);
        }
        private bool TryRebindBuildSeed(RepeatedBuildSeed seed, Player p, BuildSource preferred, out BlueprintBuildTarget target, out BuildOperationPlan plan, out bool pending,
            BuildPlanningIntent intent = BuildPlanningIntent.NewSelection)
        {
            target = null; plan = null; pending = false;
            if (!RepeatedBuildIdentityCurrent(seed)) return false;
            var range = seed.Range; long support;
            if (!range.Group.Ready || !ProjectionMatchReadCurrent(range, range.RawVersion) || !projectionChanges.TryGetBuildStamp(range.Watch, range.PartIndex, out support))
            { DemandProjectionMatchRange(range); pending = true; return false; }
            target = CreateMaterialBuildTarget(seed.Owner, range, seed.Cell, seed.Placement, seed.Object, seed.Body, seed.Ordinal, support, seed.Catalog);
            long reads=buildUnavailableReads;
            BlueprintWorldMatchSample actual;
            if (!TryBindConstructionTarget(target, BuildLane(seed.Ordinal), out actual))
            {pending=reads!=buildUnavailableReads;if(pending)DemandProjectionMatchRange(range);return false;}
            if(actual.ObjectUnknown && BuildLane(seed.Ordinal)!=BlueprintBuildLane.WallBody)
            {DemandProjectionMatchRange(range);pending=true;return false;}
            target.LocalProof = target.LocalPriority = true;
            BlueprintBuildOperation operation;
            reads=buildUnavailableReads;
            if (!TryPlanConstructionOperationFromSample(target, BuildLane(seed.Ordinal), actual, out operation))
            {pending=reads!=buildUnavailableReads;if(pending)DemandProjectionMatchRange(range);return false;}
            target.Operation = operation;
            BlueprintBuildWait wait; ulong slots;
            if (!EvaluateBuildPlan(target, p, preferred, intent, out plan, out wait, out slots))
            { if (substitutionPlanningPending || target.Readiness==BlueprintBuildReadiness.Deferred) { pending = true; return false; } RegisterPlannedBuildWait(target, wait, slots); return false; }
            return true;
        }
    }
}
