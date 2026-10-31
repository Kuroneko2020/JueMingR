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
            internal RepeatedBuildSeed(BlueprintBuildTarget target)
            {
                target = target.Requirement ?? target;
                Owner = target.Range.Owner; Range = target.Range; Identity = target.Identity; Mask = target.Mask;
                X = target.OriginX; Y = target.OriginY; Ordinal = target.Ordinal; Catalog = target.PlacementRevision;
                Cell = target.Cell; Object = target.Body == null ? (CaptureObject?)null : target.Object;
                Body = target.Body; Placement = target.Placement;
            }
        }
        private enum PreferredBuildSearch { Found, BudgetPending, SourcePending, NoRunnable, CurrentGroupInvalid }
        private BlueprintBuildLane? PreferredBuildLane()
        {
            var consumer=buildContinuation.Consumer;
            switch(buildContinuation.Operation?.Kind??BlueprintBuildOperationKind.Body)
            {
                case BlueprintBuildOperationKind.Body:
                    if(consumer==ToolBuildConsumer.Walls)return BlueprintBuildLane.WallBody;
                    if(consumer==ToolBuildConsumer.Tiles)return BlueprintBuildLane.TileBody;break;
                case BlueprintBuildOperationKind.PlaceBase:case BlueprintBuildOperationKind.ConvertSubstrate:
                    if(consumer==ToolBuildConsumer.Tiles)return BlueprintBuildLane.TileBody;break;
                case BlueprintBuildOperationKind.Hammer:
                    if(consumer==ToolBuildConsumer.Hammer)return BlueprintBuildLane.Shape;break;
                case BlueprintBuildOperationKind.PaintTile:
                    if(consumer==ToolBuildConsumer.Paintbrush)return BlueprintBuildLane.Appearance;break;
                case BlueprintBuildOperationKind.PaintWall:
                    if(consumer==ToolBuildConsumer.PaintRoller)return BlueprintBuildLane.Appearance;break;
                case BlueprintBuildOperationKind.Scrape:
                    if(consumer==ToolBuildConsumer.PaintScraper)return BlueprintBuildLane.Appearance;break;
                case BlueprintBuildOperationKind.AddWire:
                    if(consumer==ToolBuildConsumer.Wiring || consumer==ToolBuildConsumer.GrandDesign)return BlueprintBuildLane.Wire;break;
                case BlueprintBuildOperationKind.AddActuator:case BlueprintBuildOperationKind.SetInactive:case BlueprintBuildOperationKind.RemoveOwnedActuator:
                    if(consumer==ToolBuildConsumer.Wiring || consumer==ToolBuildConsumer.GrandDesign)return BlueprintBuildLane.Actuation;break;
            }
            return null; // Unmapped/inconsistent semantics retain the full walk.
        }
        // Business preference has no inventory/input ownership. Its source is
        // a rebindable hint; every admitted flight keeps its own exact lease.
        private sealed class BuildContinuation
        {
            internal Player Player;
            internal BuildSource Source;
            internal BlueprintBuildOperation Operation;
            internal ToolBuildConsumer Consumer;
            internal BlueprintMaterialInventory HintInventory;
            internal BuildFocusDiscovery Discovery;
            internal long Session, Selection, Group, Policy;
            internal bool Pending;
        }
        private RepeatedBuildSeed repeatedBuild;
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
            if(target.LocalPriority && target.Range.Materials!=null)target.Range.Materials.PreferredAttemptNegativeOrdinal=-1;
            bool ready = TryPrepareSelectedBuild(target, p, out result, plan);
            if (!ready) CompletePreferredAttempt(target);
            return ready;
        }
        private void CompletePreferredAttempt(BlueprintBuildTarget target,bool rejected=true)
        {
            var value = target?.Range.Materials; var group = buildContinuation;
            if (target?.LocalPriority != true || group == null || value == null || value.PreferredGroup != group.Group) return;
            int next = NextBuildSourceOrdinal(target.Ordinal, target.Cell, target.Body != null);
            if (value.PreferredCursor == target.Ordinal) value.PreferredCursor = next;
            if (value.PreferredFrontier && value.PreferredFrontierCursor == target.Ordinal) value.PreferredFrontierCursor = next;
            // A rejected preparation may have a current dependency or native
            // refusal. Cache only that negative permission, never completion.
            if (rejected && value.PreferredAttemptNegativeOrdinal == target.Ordinal)
                value.PreferredNegativeOrdinals?.Add(target.Ordinal);
            else value.PreferredPassUnknown = true;
        }
        private bool PlanContinuesBuild(BlueprintBuildTarget target, BuildOperationPlan plan)
        {return plan!=null && OperationContinuesBuild(target,plan.Operation,plan.ToolType,plan.SupplyType);}
        private bool FlightContinuesBuild(BuildFlight flight)
        {return flight!=null && OperationContinuesBuild(flight.Target,flight.Operation,flight.Source.Type,flight.Operation?.SupplyType??0);}
        private bool OperationContinuesBuild(BlueprintBuildTarget target,BlueprintBuildOperation operation,int toolType,int supplyType)
        {
            var group = buildContinuation;
            if(group==null)return false;
            var a=group.Operation;var b=operation;
            bool ordinaryA=a==null || a.Kind==BlueprintBuildOperationKind.PlaceBase || a.Kind==BlueprintBuildOperationKind.ConvertSubstrate;
            bool ordinaryB=b==null || b.Kind==BlueprintBuildOperationKind.PlaceBase || b.Kind==BlueprintBuildOperationKind.ConvertSubstrate;
            if(ordinaryA || ordinaryB)return ordinaryA && ordinaryB && group.Source.Type==toolType && group.Consumer==BuildConsumer(b,target);
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
        private bool PreparedBuildPriorityCurrent(BuildFlight next,Player p)
        {
            if(!BuildContinuationCurrent(p) || FlightContinuesBuild(next))return true;
            BlueprintBuildTarget target;BuildOperationPlan plan;
            // Legality of an already prepared foreign candidate does not
            // establish its order. Recheck the complete current near domain
            // immediately before it may acquire another native generation.
            return TryPreferredBuild(p,out target,out plan)==PreferredBuildSearch.NoRunnable;
        }
        private void RememberBuildContinuation(BuildFlight flight)
        {
            var plan = new BuildOperationPlan { Operation = flight.Operation, Tool = flight.Source.Item, ToolType = flight.Source.Type, ToolSlot = flight.Source.Slot,
                SupplyType = flight.Operation?.SupplyType ?? 0, SupplySlot = -1, CursorMode=flight.CursorMode, SmartDirection=flight.SmartDirection, SmartDirectionY=flight.SmartDirectionY };
            foreach (var dependency in flight.Dependencies) if (dependency.Primary) { plan.SupplySlot = dependency.Slot; plan.Supply = dependency.Item; }
            bool samePlan=BuildContinuationCurrent(flight.Player) && PlanContinuesBuild(flight.Target,plan);
            if(samePlan)
            {CompletePreferredAttempt(flight.Target,false);buildContinuation.Source=flight.Source;buildContinuation.HintInventory=MaterialInventory;return;}
            // Match ranges can outlive a business group. Release the old
            // group's derived negative proofs before replacing its only owner.
            ClearBuildContinuation();
            buildContinuation = new BuildContinuation { Player=flight.Player,Source=flight.Source,HintInventory=MaterialInventory,
                Operation=CopyBuildOperation(flight.Operation),Consumer=BuildConsumer(flight.Operation,flight.Target),Session=generation,
                Selection=buildTools.SelectionIntent,Policy=substitutionRevision,Group=++buildContinuationGroup,Discovery=new BuildFocusDiscovery() };
            repeatedBuild = null;
        }
        private void RetirePreferredRange(BlueprintProjectionMatchRange range)
        {
            var discovery=buildContinuation?.Discovery;if(discovery==null)return;
            if(range.Materials!=null)range.Materials.PreferredNegativeOrdinals=null;
            if(range.Materials!=null)range.Materials.PreferredPositionNegatives=null;
            discovery.Members.Remove(range);discovery.Ranges.Remove(range);
        }
        private void ClearBuildContinuation()
        {
            if(buildContinuation?.Discovery!=null)foreach(var range in buildContinuation.Discovery.Ranges)
                if(range.Materials!=null){range.Materials.PreferredNegativeOrdinals=null;range.Materials.PreferredPositionNegatives=null;}
            buildContinuation = null; repeatedBuild = null;
        }
        private void ReopenPreferredRange(BlueprintProjectionMatchRange range)
        {
            var value=range.Materials;
            if(value!=null && value.PreferredGroup==buildContinuation?.Group)
            {
                value.PreferredRevisit=true;value.PreferredCoveredBounds=default(BlueprintProjectionBounds);value.PreferredNegativeOrdinals?.Clear();
                value.PreferredPositionNegatives?.Clear();value.PreferredWaits=BlueprintBuildWait.None;value.PreferredRetryAt=value.PreferredDependencySlots=0;
                value.PreferredContainer=buildContainerRevision;value.PreferredRestoration=appearanceRestorationRevision;value.PreferredPermission=buildFocusPermissionRevision;
            }
        }
        private PreferredBuildSearch TryPreferredRange(BlueprintProjectionMatchRange range, Player p, out BlueprintBuildTarget target, out BuildOperationPlan plan)
        {
            target=null;plan=null;var group=buildContinuation;
            var bounds=IntersectBuildBounds(BuildFocusBounds(range.Owner,p),range.Source.Bounds);
            if(EmptyBuildBounds(bounds))return PreferredBuildSearch.NoRunnable;
            var value=range.Materials;
            if(value==null)range.Materials=value=new BlueprintRangeMaterials{OwnerTotals=range.Owner.Match.Materials,CatalogRevision=MaterialCatalog.Revision};
            TryWakeBuildEligibility(range);
            if(value.PreferredGroup!=group.Group)
            {
                value.PreferredGroup=group.Group;value.PreferredCursor=0;
                value.PreferredComplete=value.PreferredInitialized=value.PreferredRevisit=value.PreferredFrontier=false;
                value.PreferredCoveredBounds=default(BlueprintProjectionBounds);
                value.PreferredNegativeOrdinals=new System.Collections.Generic.HashSet<int>();
                value.PreferredPositionNegatives=null;
                value.PreferredWaits=BlueprintBuildWait.None;value.PreferredRetryAt=value.PreferredDependencySlots=0;
                value.PreferredContainer=buildContainerRevision;value.PreferredRestoration=appearanceRestorationRevision;value.PreferredPermission=buildFocusPermissionRevision;
                value.PreferredVersion=value.PreferredWake=value.PreferredSupply=value.PreferredPlayer=-1;
            }
            if(value.PreferredNegativeOrdinals==null){value.PreferredNegativeOrdinals=new System.Collections.Generic.HashSet<int>();ReopenPreferredRange(range);}
            RefreshPreferredPositionNegatives(value,p);
            if ((value.PreferredWaits & BlueprintBuildWait.Player) != 0 && value.PreferredPermission != buildFocusPermissionRevision ||
                (value.PreferredWaits & BlueprintBuildWait.Container) != 0 && value.PreferredContainer != buildContainerRevision ||
                (value.PreferredWaits & BlueprintBuildWait.Restoration) != 0 && value.PreferredRestoration != appearanceRestorationRevision ||
                (value.PreferredWaits & BlueprintBuildWait.Dependencies) != 0 && BuildDependencyReleased(value.PreferredDependencySlots) ||
                value.PreferredRetryAt != 0 && materialTick >= value.PreferredRetryAt) ReopenPreferredRange(range);
            if(value.PreferredVersion!=range.Version || value.PreferredWake!=value.BuildWakeRevision || value.PreferredSupply!=buildSupplyRevision)
            {
                value.PreferredVersion=range.Version;value.PreferredWake=value.BuildWakeRevision;
                value.PreferredSupply=buildSupplyRevision;value.PreferredPlayer=MaterialBuildEligibilityRevision;value.PreferredRevisit=true;
                value.PreferredCoveredBounds=default(BlueprintProjectionBounds);
                value.PreferredNegativeOrdinals?.Clear();
                value.PreferredPositionNegatives?.Clear();
            }
            // A completed negative result needs no transport pin. Only its
            // actual local dependencies can reopen it; eviction is not a new
            // discovery obligation and a lease boundary is not a new group.
            // An unfinished entered strip no longer in the current near
            // domain cannot withhold a negative proof for the covered interior.
            // Keep that strip's ordinal for the next entry instead of restarting.
            if(!value.PreferredRevisit && !value.PreferredFrontier && BuildBoundsContain(value.PreferredCoveredBounds,bounds))return PreferredBuildSearch.NoRunnable;
            if(value.PreferredComplete)
            {
                if (value.PreferredRevisit) { value.PreferredWaits=BlueprintBuildWait.None;value.PreferredRetryAt=value.PreferredDependencySlots=0; }
                value.PreferredCompletedBatch++;value.PreferredComplete=value.PreferredInitialized=value.PreferredRevisit=false;value.PreferredCursor=0;
            }
            var part=FindBuildSource(range);long support;
            if(part==null || !range.Group.Ready || !ProjectionMatchReadCurrent(range,range.RawVersion) ||
                !projectionChanges.TryGetBuildStamp(range.Watch,range.PartIndex,out support))
            {DemandProjectionMatchRange(range);return PreferredBuildSearch.SourcePending;}
            var cells=ProjectionSourceCells(part.Window);
            if(!value.PreferredInitialized && part.Offset==0)
            {
                value.PreferredBounds=NextBuildFocusCoverage(value,bounds);value.PreferredPassCovered=value.PreferredBounds;
                value.PreferredPassUnknown=false;
                value.PreferredInitialized=true;value.PreferredRevisit=false;
                value.PreferredCursor=BuildMemberOrdinal(BuildCellLowerBound(cells,value.PreferredBounds.Left,value.PreferredBounds.Top));
            }
            int steps=BuildSourceSlice;
            if(value.PreferredFrontier)
            {
                var frontier=value.PreferredFrontierBounds;
                if(BuildMemberIndex(value.PreferredFrontierCursor)<part.Offset){DemandProjectionMatchRange(range);return PreferredBuildSearch.SourcePending;}
                var result=WalkPreferredSource(range,part,p,frontier,ref value.PreferredFrontierCursor,ref steps,support,out target,out plan);
                if(result==PreferredBuildSearch.Found || result==PreferredBuildSearch.BudgetPending || result==PreferredBuildSearch.SourcePending)return result;
                value.PreferredFrontier=false;
            }
            if(BuildMemberIndex(value.PreferredCursor)<part.Offset)
            {DemandProjectionMatchRange(range);return PreferredBuildSearch.SourcePending;}
            var found=WalkPreferredSource(range,part,p,value.PreferredBounds,ref value.PreferredCursor,ref steps,support,out target,out plan);
            if(found!=PreferredBuildSearch.NoRunnable)return found;
            if(value.PreferredRevisit)
            {
                // Merge changes behind the absolute cursor. Finish the current
                // finite calculation pass, then revisit within this same Focus.
                value.PreferredCompletedBatch++;value.PreferredInitialized=value.PreferredRevisit=false;value.PreferredCursor=0;
                return PreferredBuildSearch.BudgetPending;
            }
            if(!value.PreferredPassUnknown)CompleteBuildFocusCoverage(value);
            value.PreferredComplete=true;value.PreferredCompletedBatch++;
            return BuildBoundsContain(value.PreferredCoveredBounds,bounds)?PreferredBuildSearch.NoRunnable:PreferredBuildSearch.BudgetPending;
        }
        private PreferredBuildSearch WalkPreferredSource(BlueprintProjectionMatchRange range,BlueprintProjectionSourcePart part,Player p,
            BlueprintProjectionBounds bounds,ref int cursor,ref int steps,long support,out BlueprintBuildTarget target,out BuildOperationPlan plan)
        {
            target=null;plan=null;var cells=ProjectionSourceCells(part.Window);
            var value=range.Materials;var near=BuildFocusBounds(range.Owner,p);var lane=PreferredBuildLane();
            while(materialBuildSourceBudget>0 && steps>0)
            {
                // Align source ordinals before reading any member. Other
                // lanes cannot belong to this group; a member without this
                // lane still reaches the constructor and spends one step.
                if(lane.HasValue && BuildLane(cursor)!=lane.Value)
                {
                    int relevant=BuildMemberOrdinal(BuildMemberIndex(cursor))+(int)lane.Value;
                    cursor=relevant>=cursor?relevant:BuildMemberOrdinal(BuildMemberIndex(cursor)+1)+(int)lane.Value;
                }
                int index=BuildMemberIndex(cursor)-part.Offset;
                if(index<0){DemandProjectionMatchRange(range);return PreferredBuildSearch.SourcePending;}
                if(index>=cells.Count)
                {
                    if(part.Complete)return PreferredBuildSearch.NoRunnable;
                    cursor=Math.Max(cursor,BuildMemberOrdinal(part.NextOffset));DemandProjectionMatchRange(range);return PreferredBuildSearch.SourcePending;
                }
                var cell=cells[index];
                if(part.LogicalObjectId==0)
                {
                    if(cell.Y>=bounds.Bottom)return PreferredBuildSearch.NoRunnable;
                    if(cell.X<bounds.Left || cell.X>=bounds.Right || cell.Y<bounds.Top)
                    {cursor=BuildMemberOrdinal(part.Offset+BuildCellLowerBound(cells,bounds.Left,cell.X>=bounds.Right?cell.Y+1:Math.Max(cell.Y,bounds.Top)));continue;}
                }
                int ordinal=cursor,next=NextBuildSourceOrdinal(ordinal,cell,part.LogicalObjectId>0);
                materialBuildSourceBudget--;steps--;
#if DEBUG
                ProjectionMatchSourceSteps++;
#endif
                if(value.PreferredNegativeOrdinals?.Contains(ordinal)==true){cursor=next;continue;}
                if(part.LogicalObjectId==0 && (cell.X<near.Left || cell.X>=near.Right || cell.Y<near.Top || cell.Y>=near.Bottom))
                {value.PreferredPassUnknown=true;cursor=next;continue;}
                BlueprintBuildTarget candidate;
                // Reuse only this visit's known Missing/Conflict sample to skip
                // dependent no-op lanes. A found/prepared target returns with
                // its ordinal intact; its later real write is never skipped.
                if(TryCreateConstructionTargetAndAdvance(range.Owner,range,part,index,ordinal,support,out candidate,out next,localPriority:true) && !IsUnfinishedBuildTarget(candidate) && CandidateMayContinueBuild(candidate))
                {
                    BuildOperationPlan option;BlueprintBuildWait wait;ulong slots;
                    if(!TryPlanContinuationBuild(candidate,p,PreferredBuildSourceHint(p),out option,out wait,out slots))
                    {if(substitutionPlanningPending)return PreferredBuildSearch.BudgetPending;RegisterPlannedBuildWait(candidate,wait,slots);}
                    else if(PlanContinuesBuild(candidate,option))
                    {candidate.LocalPriority=true;target=candidate;plan=option;return PreferredBuildSearch.Found;}
                }
                // This is a bounded current-near query proof. It is discarded
                // with that Focus/range or any world/supply/permission recovery;
                // it cannot update Match, materials totals or target completion.
                value.PreferredNegativeOrdinals?.Add(ordinal);
                cursor=next;
            }
            return PreferredBuildSearch.BudgetPending;
        }
        private PreferredBuildSearch TryPreferredBuild(Player p, out BlueprintBuildTarget target, out BuildOperationPlan plan)
        {
            target=null;plan=null;
            if(!BuildContinuationCurrent(p))return PreferredBuildSearch.CurrentGroupInvalid;
            var group=buildContinuation;bool nearCurrent=RefreshBuildFocusNear(p);
            if(repeatedBuild!=null)
            {
                BlueprintBuildTarget repeated;BuildOperationPlan repeatPlan;bool pending;
                if(TryRebindBuildSeed(repeatedBuild,p,PreferredBuildSourceHint(p),out repeated,out repeatPlan,out pending,BuildPlanningIntent.ContinuationProbe) && PlanContinuesBuild(repeated,repeatPlan))
                {repeatedBuild=null;target=repeated;plan=repeatPlan;group.Pending=true;return PreferredBuildSearch.Found;}
                if(!pending)repeatedBuild=null;
            }
            bool pendingSource=!nearCurrent,pendingWalk=false;var discovery=group.Discovery;
            // Prefer live nearby transports, then the complete geometric near
            // domain. The latter includes fifth/sixth sources not in the cache.
            for(var node=buildSources.First;node!=null && materialBuildSourceBudget>0;node=node.Next)
            {
                if(!discovery.Members.Contains(node.Value.Range))continue;
                var result=TryPreferredRange(node.Value.Range,p,out target,out plan);
                if(result==PreferredBuildSearch.Found){group.Pending=true;return result;}
                pendingSource|=result==PreferredBuildSearch.SourcePending;pendingWalk|=result==PreferredBuildSearch.BudgetPending;
            }
            int count=discovery.Ranges.Count,visits=count;
            while(visits-->0 && count>0 && materialBuildSourceBudget>0)
            {
                discovery.Cursor%=count;var range=discovery.Ranges[discovery.Cursor];discovery.Cursor=(discovery.Cursor+1)%count;
                var result=TryPreferredRange(range,p,out target,out plan);
                if(result==PreferredBuildSearch.Found){group.Pending=true;return result;}
                pendingSource|=result==PreferredBuildSearch.SourcePending;pendingWalk|=result==PreferredBuildSearch.BudgetPending;
            }
            if(visits>=0)pendingWalk=true;
            // A current native consumer is a real obligation, not proof that
            // its unfinished target is absent from this business group.
            pendingWalk|=activeBuild!=null && !activeBuild.Checked && !activeBuild.Rejected;
            group.Pending=pendingSource || pendingWalk;
            return pendingSource?PreferredBuildSearch.SourcePending:pendingWalk?PreferredBuildSearch.BudgetPending:PreferredBuildSearch.NoRunnable;
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
            BlueprintWorldMatchSample actual;
            if (!TryBindConstructionTarget(target, BuildLane(seed.Ordinal), out actual)) return false;
            target.LocalProof = target.LocalPriority = true;
            BlueprintBuildOperation operation;
            if (!TryPlanConstructionOperationFromSample(target, BuildLane(seed.Ordinal), actual, out operation)) return false;
            target.Operation = operation;
            BlueprintBuildWait wait; ulong slots;
            if (!TryPlanBuildCore(target, p, preferred, intent, out plan, out wait, out slots))
            { if (substitutionPlanningPending) { pending = true; return false; } RegisterPlannedBuildWait(target, wait, slots); return false; }
            return true;
        }
    }
}
