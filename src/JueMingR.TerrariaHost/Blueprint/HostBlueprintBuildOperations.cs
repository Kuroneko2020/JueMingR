using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;

namespace JueMingR.TerrariaHost.Blueprint
{
    // One finite lane identity is shared by forward, nearby, frontier and
    // continuity walks. A furniture body is placed once; its members retain
    // their own appearance/wire/actuation work in the same bounded source.
    internal enum BlueprintBuildLane { TileBody, WallBody, Shape, Appearance, Wire, Actuation }
    internal enum BlueprintBuildOperationKind
    { Body, Hammer, PaintTile, PaintWall, Scrape, AddWire, AddActuator, SetInactive, RemoveOwnedActuator, PlaceBase, ConvertSubstrate, HoikBase, HoikSwap }
    internal sealed class BlueprintBuildOperation
    {
        internal BlueprintBuildOperationKind Kind;
        internal ToolBuildConsumer Consumer;
        internal int Mode, SupplyType, Cycle, BucketType;
        internal int SupplyDestination = -1, PrimaryMaximum = 1, HeldMaximum;
        internal bool Structure;
        internal bool Hoik;
        internal int HoikWaitTool; // 1 hammer, 2 pick, 3 current-policy final-platform supply.
        // This native step may place a recipe's base rather than its final
        // target. The immutable target Cell/Body and final Match never change.
        internal BlueprintPlacementDescriptor NativePlacement;
        // The selected allowed final platform is separate from the temporary
        // stone's native placement. It is a current plan, never persisted stock.
        internal BlueprintPlacementDescriptor HoikPlatform;
        // Exact current occupied pose, in addition to canonical building
        // identity. A door opening/closing revokes this single-cell action.
        internal BlueprintWorldObject DoorForm;
        internal BlueprintDoorBuildProof DoorProof;
    }
    internal sealed partial class HostBlueprint
    {
        // A candidate description, not a prepared use. Planning may read the
        // frozen capability index and exact live Items, but cannot move supply,
        // borrow a mode/input, acquire a token or mutate another candidate.
        private sealed class BuildOperationPlan
        {
            internal BlueprintBuildTarget ExecutionTarget;
            internal BlueprintBuildOperation Operation;
            internal Item Tool, Supply;
            internal int ToolType;
            internal int ToolSlot = -1, SupplySlot = -1, SupplyType, SupplyDestination = -1;
            internal int HeldMaximum, PrimaryMaximum;
            internal ToolBuildCursorMode CursorMode = ToolBuildCursorMode.Precise;
            internal int SmartDirection;
            internal int SmartDirectionY;
            internal SmartPlatformInputProof SmartProof;
        }
        private static BlueprintBuildOperation CopyBuildOperation(BlueprintBuildOperation value)
        {
            return value == null ? null : new BlueprintBuildOperation { Kind = value.Kind, Consumer = value.Consumer,
                Mode = value.Mode, SupplyType = value.SupplyType, Cycle = value.Cycle, BucketType = value.BucketType,
                SupplyDestination = value.SupplyDestination, PrimaryMaximum = value.PrimaryMaximum,
                HeldMaximum = value.HeldMaximum, NativePlacement = value.NativePlacement, HoikPlatform=value.HoikPlatform, DoorForm = value.DoorForm, DoorProof = value.DoorProof, Structure = value.Structure, Hoik = value.Hoik, HoikWaitTool = value.HoikWaitTool };
        }
        private int PlannedBuildToolSlot(Player p, BuildOperationPlan plan, bool afterOwnedPreparation = false)
        {
            if (plan == null || p == null || plan.Tool == null || plan.Tool.IsAir || plan.Tool.type != plan.ToolType) return -1;
            int slot = plan.ToolSlot;
            if (slot < 0 || slot >= 50 || !ReferenceEquals(p.inventory[slot], plan.Tool))
            {
                if (!afterOwnedPreparation || buildMaterialPreparation?.HasWork != true) return -1;
                slot = Array.IndexOf(p.inventory, plan.Tool);
                if (slot < 0 || slot >= 50 || !buildMaterialPreparation.OwnsEndpoint(p.inventory, slot)) return -1;
            }
            return buildTools.BuildInventoryEligible(slot, plan.Tool) ? slot : -1;
        }
        private static bool PlannedBuildSupplyCurrent(Player p, BuildOperationPlan plan, int slot)
        {
            return plan == null || plan.SupplySlot < 0 || slot == plan.SupplySlot && slot < p.inventory.Length &&
                ReferenceEquals(p.inventory[slot], plan.Supply) && plan.Supply.type == plan.SupplyType && plan.Supply.stack > 0;
        }
        private enum BuildPlanningIntent { NewSelection, ContinuationProbe }
        private bool TryPlanBuild(BlueprintBuildTarget target, Player p, BuildSource preferred, out BuildOperationPlan plan,
            out BlueprintBuildWait wait, out ulong dependencySlots)
        { return EvaluateBuildPlan(target, p, preferred, BuildPlanningIntent.NewSelection, out plan, out wait, out dependencySlots); }
        private bool TryPlanContinuationBuild(BlueprintBuildTarget target, Player p, BuildSource source, out BuildOperationPlan plan,
            out BlueprintBuildWait wait, out ulong dependencySlots)
        { return EvaluateBuildPlan(target, p, source, BuildPlanningIntent.ContinuationProbe, out plan, out wait, out dependencySlots); }
        private bool EvaluateBuildPlan(BlueprintBuildTarget target,Player p,BuildSource source,BuildPlanningIntent intent,
            out BuildOperationPlan plan,out BlueprintBuildWait wait,out ulong slots)
        {
            target.Readiness=BlueprintBuildReadiness.Deferred;
            long reads=buildUnavailableReads;
            bool ready=TryPlanBuildCore(target,p,source,intent,out plan,out wait,out slots);
            if(ready){target.Readiness=BlueprintBuildReadiness.Ready;return true;}
            if(substitutionPlanningPending || reads!=buildUnavailableReads)return DeferBuildTarget(target);
            if(wait!=BlueprintBuildWait.None)target.Readiness=BlueprintBuildReadiness.BlockedNow;
            return false;
        }
        private bool TryPlanBuildCore(BlueprintBuildTarget target, Player p, BuildSource preferred, BuildPlanningIntent intent,
            out BuildOperationPlan plan, out BlueprintBuildWait wait, out ulong dependencySlots)
        {
            plan = null; wait = BlueprintBuildWait.None; dependencySlots = 0; substitutionPlanningPending = false;
            if (!IsBuildTargetCurrent(target) || !ReferenceEquals(p, player)) return DeferBuildTarget(target);
            if(IsUnconfirmedTarget(target)){target.Readiness=BlueprintBuildReadiness.BlockedNow;return false;}
            var operation = target.Operation;
            if (HoikOperation(operation) && !HoikCapabilities(target, p, operation.Kind == BlueprintBuildOperationKind.HoikBase, out wait)) return false;
            if(HoikOperation(operation) && operation.Kind!=BlueprintBuildOperationKind.Hammer)
            {
                // Reuse the same policy choices, current supply, finite cursor
                // and budget as ordinary platform substitution. The execution
                // target retains Requirement; it never rewrites the blueprint.
                bool ready=TryPlanSubstitutionBuild(target,p,preferred,intent,out plan,out wait);
                if(!ready && (wait&BlueprintBuildWait.Material)!=0)
                {operation.HoikWaitTool=3;wait|=BlueprintBuildWait.Capability;}
                return ready;
            }
            if (WiringOperation(operation)) return TryPlanWiringTool(target, p, preferred, out plan, out wait, out dependencySlots);
            int tool = -1, primary = -1;
            if (AppearanceOperation(operation))
            {
                bool outside;
                tool = FindAppearanceTool(target, p, out outside, preferred);
                if (tool < 0) { // A matching longer tool can recover reach without any player movement.
                    wait = outside ? BlueprintBuildWait.Player | BlueprintBuildWait.Capability : BlueprintBuildWait.Material | BlueprintBuildWait.Capability; return false; }
                operation = CopyBuildOperation(operation);
            }
            else if (operation?.NativePlacement?.Producer != null)
            {
                // Prefer this exact producer before exploring another bucket.
                // TryCycle only solves the native option; no +/-1 input occurs.
                BlueprintBuildWait failures = BlueprintBuildWait.None; ulong blocked = 0;
                for (int preferencePass = 0; preferencePass < 2; preferencePass++)
                foreach (var placement in MaterialCatalog.Producers(target.Cell, target.Body == null ? (CaptureObject?)null : target.Object))
                {
                    if (placement.Producer == null) continue;
                    int slot = ConstructionItemSlot(p, placement.UseItemType);
                    if (preferencePass == 0)
                    {
                        if (preferred == null || preferred.Type != placement.UseItemType || preferred.Slot < 0 || preferred.Slot >= 50 ||
                            !ReferenceEquals(p.inventory[preferred.Slot], preferred.Item) || !buildTools.BuildInventoryEligible(preferred.Slot, preferred.Item)) continue;
                        slot = preferred.Slot;
                    }
                    else if (slot >= 0 && preferred != null && slot == preferred.Slot && ReferenceEquals(p.inventory[slot], preferred.Item)) continue;
                    if (slot < 0) { failures |= BlueprintBuildWait.Material | BlueprintBuildWait.Capability; continue; }
                    var candidate = SpecialOperation(placement);
                    if (!placement.Producer.TryCycle(Player.FlexibleWandCycleOffset, out candidate.Cycle)) continue;
                    int x = target.Body == null ? target.WorldX : target.Bounds.Left + placement.OriginX;
                    int y = target.Body == null ? target.WorldY : target.Bounds.Top + placement.OriginY;
                    if (!p.IsInTileInteractionRange(x, y, TileReachCheckSettings.Simple, p.inventory[slot].tileBoost + p.blockRange))
                    { failures |= BlueprintBuildWait.Player | BlueprintBuildWait.Capability; continue; }
                    BlueprintBuildWait supplyWait; ulong supplySlots;
                    if (!TryPlanBuildSupply(target, p, candidate.SupplyType, candidate.SupplyDestination, out primary, out supplyWait, out supplySlots, intent == BuildPlanningIntent.ContinuationProbe))
                    { failures |= supplyWait; blocked |= supplySlots; continue; }
                    plan = new BuildOperationPlan { Operation = candidate, Tool = p.inventory[slot], ToolType = p.inventory[slot].type, ToolSlot = slot,
                        Supply = primary < 0 ? null : p.inventory[primary], SupplySlot = primary, SupplyType = candidate.SupplyType, SupplyDestination = candidate.SupplyDestination,
                        HeldMaximum = candidate.HeldMaximum, PrimaryMaximum = candidate.PrimaryMaximum };
                    return true;
                }
                wait = failures == BlueprintBuildWait.None ? BlueprintBuildWait.Material | BlueprintBuildWait.Capability : failures;
                dependencySlots = blocked; return false;
            }
            else
            {
                var placement = operation?.NativePlacement ?? target.Placement;
                if (placement == null || !BasicPlacement(placement)) return InvalidBuildTarget(target);
                // The frozen ChoiceSet is the applicability gate, including
                // effective torch output. A global ON switch alone does not
                // justify building candidate lists for unrelated exact blocks.
                if (operation == null && (MaterialCatalog.Substitution?.Candidates(placement, substitutionPolicy).Count ?? 0) > 0)
                    return TryPlanSubstitutionBuild(target, p, preferred, intent, out plan, out wait);
                if (preferred != null && preferred.Type == placement.UseItemType && preferred.Slot >= 0 && preferred.Slot < 50 &&
                    ReferenceEquals(p.inventory[preferred.Slot], preferred.Item) && buildTools.BuildInventoryEligible(preferred.Slot, preferred.Item)) tool = preferred.Slot;
                else tool = ConstructionItemSlot(p, placement.UseItemType);
                if (tool < 0) { wait = BlueprintBuildWait.Material; return false; }
                if (!p.IsInTileInteractionRange(target.WorldX, target.WorldY, TileReachCheckSettings.Simple, p.inventory[tool].tileBoost + p.blockRange))
                { wait = BlueprintBuildWait.Player; return false; }
                operation = CopyBuildOperation(operation);
            }
            if (operation?.SupplyType > 0 && !TryPlanBuildSupply(target, p, operation.SupplyType, operation.SupplyDestination, out primary, out wait, out dependencySlots, intent == BuildPlanningIntent.ContinuationProbe)) return false;
            plan = new BuildOperationPlan { Operation = operation, Tool = p.inventory[tool], ToolType = p.inventory[tool].type, ToolSlot = tool,
                Supply = primary < 0 ? null : p.inventory[primary], SupplySlot = primary,
                SupplyType = operation?.SupplyType ?? 0, SupplyDestination = operation?.SupplyDestination ?? -1,
                HeldMaximum = operation?.HeldMaximum ?? (target.Placement.Kind == BlueprintPlacementKind.Wall ? 5 : 1), PrimaryMaximum = operation?.PrimaryMaximum ?? 0 };
            PlanSmartPlatformInput(target, p, plan);
            return true;
        }
        private const int BuildLaneCount = 6;
        private static int BuildMemberOrdinal(int member) { return checked(member * BuildLaneCount); }
        private static int BuildMemberIndex(int ordinal) { return ordinal / BuildLaneCount; }
        private static BlueprintBuildLane BuildLane(int ordinal) { return (BlueprintBuildLane)(ordinal % BuildLaneCount); }
        private static int NextBuildOrdinal(int ordinal, CaptureCell cell)
        {
            int next = ordinal + 1;
            while (next / BuildLaneCount == ordinal / BuildLaneCount && !BuildLanePresent(cell, BuildLane(next))) next++;
            return next;
        }
        private static bool BuildLanePresent(CaptureCell cell, BlueprintBuildLane lane)
        {
            switch (lane)
            {
                case BlueprintBuildLane.TileBody: case BlueprintBuildLane.Shape: return (cell.Layers & CaptureLayer.Tile) != 0;
                case BlueprintBuildLane.WallBody: return (cell.Layers & CaptureLayer.Wall) != 0;
                case BlueprintBuildLane.Appearance: return (cell.Layers & (CaptureLayer.Tile | CaptureLayer.Wall)) != 0;
                case BlueprintBuildLane.Wire: return (cell.Layers & CaptureLayer.Wire) != 0 && cell.Wires != 0;
                default: return (cell.Layers & CaptureLayer.Actuator) != 0 && cell.Actuator || (cell.Layers & CaptureLayer.Tile) != 0;
            }
        }
        private static bool AppearanceOperation(BlueprintBuildOperation operation)
        { return operation != null && (operation.Kind == BlueprintBuildOperationKind.Hammer || operation.Kind == BlueprintBuildOperationKind.PaintTile || operation.Kind == BlueprintBuildOperationKind.PaintWall || operation.Kind == BlueprintBuildOperationKind.Scrape); }
        private static bool WiringOperation(BlueprintBuildOperation operation)
        { return operation != null && (operation.Kind == BlueprintBuildOperationKind.AddWire || operation.Kind == BlueprintBuildOperationKind.AddActuator || operation.Kind == BlueprintBuildOperationKind.SetInactive || operation.Kind == BlueprintBuildOperationKind.RemoveOwnedActuator); }
        private bool TryPrepareConstructionBuild(BlueprintBuildTarget target, Player p, out BuildFlight flight, BuildOperationPlan plan)
        {
            if (HoikOperation(target.Operation)) return TryPrepareHoikBuild(target, p, out flight, plan);
            if (AppearanceOperation(target.Operation)) return TryPrepareAppearanceBuild(target, p, out flight, plan);
            if (WiringOperation(target.Operation)) return TryPrepareWiringBuild(target, p, out flight, plan);
            return TryPrepareSpecialBuild(target, p, out flight, plan);
        }
        private BlueprintBuildReadiness CreateConstructionCandidate(BlueprintProjectionPlacement owner,BlueprintProjectionMatchRange range,
            BlueprintProjectionSourcePart part,int index,int ordinal,long support,out BlueprintBuildTarget target,out int following,bool localPriority)
        {
            target=null;following=ordinal+1;var catalog=MaterialCatalog;
            if(catalog==null || !ProjectionQualificationCurrent(owner))return BlueprintBuildReadiness.Deferred;
            if(index<0 || index>=part.Window.Cells.Count)return BlueprintBuildReadiness.Deferred;
            var lane=BuildLane(ordinal);var raw=EffectiveProjectionCell(owner,part.Window,index);
            CaptureObject? obj=part.LogicalObjectId>0 && part.Window.Objects.Count==1?part.Window.Objects[0]:(CaptureObject?)null;
            var selection=SelectProjectionTarget(owner,raw,obj);var cell=selection.Apply(raw);
            following=NextBuildSourceOrdinal(ordinal,raw,part.LogicalObjectId>0);
            cell=new BlueprintConstructionSelection((ushort)(selection.RequiredBits&~selection.UnknownBits),0,catalog.Revision).Apply(cell);
            if(!BuildLanePresent(cell,lane) || part.LogicalObjectId>0 && lane==BlueprintBuildLane.TileBody && index!=0 ||
                part.LogicalObjectId==0 && cell.ObjectId>0 && (lane==BlueprintBuildLane.TileBody || lane==BlueprintBuildLane.Shape))
                return BlueprintBuildReadiness.DoneInvalid;
            BlueprintWorldObject body=null;
            if(obj.HasValue && (cell.Layers&CaptureLayer.Tile)!=0 && !projectionMatchSource.TryDescribeTarget(obj.Value,cell,owner.OriginX,owner.OriginY,out body))
                return BlueprintBuildReadiness.DoneInvalid; // Qualified immutable description is unsupported, not a world read.
            var descriptor=lane==BlueprintBuildLane.WallBody?catalog.Wall(cell.WallType):
                (cell.Layers&CaptureLayer.Tile)!=0?catalog.Substrate(cell)??catalog.Tile(cell,obj):null;
            if((lane==BlueprintBuildLane.TileBody || lane==BlueprintBuildLane.WallBody) && descriptor==null)return BlueprintBuildReadiness.DoneInvalid;
            var next=CreateMaterialBuildTarget(owner,range,cell,descriptor,obj,body,ordinal,support,catalog.Revision);
            next.LocalProof=true;next.LocalPriority=localPriority;
            CaptureNearBuildSeed(next);
            long reads=buildUnavailableReads;BlueprintWorldMatchSample actual;
            if(!TryBindConstructionTarget(next,lane,out actual))
            {
                // Door source context has its own transport owner. Ordinary
                // binding reads the exact cell; known pose/no-action results
                // can be excluded, but failed reads/context cannot.
                if(reads!=buildUnavailableReads || next.Body?.Family==BlueprintWorldObjectFamily.Door && DoorSourceWindow(range)==null)
                    return BlueprintBuildReadiness.Deferred;
                return BlueprintBuildReadiness.BlockedNow;
            }
            if(actual.ObjectUnknown && lane!=BlueprintBuildLane.WallBody)return BlueprintBuildReadiness.Deferred;
            following=NextNeededBuildOrdinal(next,actual,ordinal,following);
            BlueprintBuildOperation operation;
            reads=buildUnavailableReads;
            if(!TryPlanConstructionOperationFromSample(next,lane,actual,out operation))
                return reads!=buildUnavailableReads?BlueprintBuildReadiness.Deferred:BlueprintBuildReadiness.BlockedNow;
            next.Operation=operation;
            if(operation==null && !HasBuildSupply(descriptor))
            {WaitForBuild(next,BlueprintBuildWait.Material);return BlueprintBuildReadiness.BlockedNow;}
            if(!localPriority && !BuildNearBounds(owner).Contains(next.WorldX-owner.OriginX,next.WorldY-owner.OriginY))
            {WaitForBuild(next,BlueprintBuildWait.Player);return BlueprintBuildReadiness.BlockedNow;}
            target=next;target.Readiness=BlueprintBuildReadiness.Ready;return BlueprintBuildReadiness.Ready;
        }
        private int NextNeededBuildOrdinal(BlueprintBuildTarget target,BlueprintWorldMatchSample actual,int ordinal,int following)
        {
            // One current source visit proves this ordinary body cannot yet
            // host its foreground operations. Reuse that sample only while
            // choosing its next lane;
            // no observation or skip mask survives an Update or world write.
            // Unknown and object members keep their complete walks. A known
            // different body, like a missing body, cannot be painted/shaped.
            var cell=target.Cell;
            if(cell.ObjectId!=0 || !actual.Available || actual.ObjectUnknown ||
                (cell.Layers&CaptureLayer.Tile)==0)return following;
            var body=BlueprintMaterialSteps.SameBody(cell,actual,MaterialCatalog,policy:substitutionPolicy);
            if(body!=BlueprintBodyState.Missing && body!=BlueprintBodyState.Conflict)return following;
            while(BuildMemberIndex(following)==BuildMemberIndex(ordinal))
            {
                var lane=BuildLane(following);
                bool skip=lane==BlueprintBuildLane.Shape ||
                    lane==BlueprintBuildLane.Appearance && ((cell.Layers&CaptureLayer.Wall)==0 || BlueprintMaterialSteps.SameBody(cell,actual,MaterialCatalog,true,substitutionPolicy)!=BlueprintBodyState.Same) ||
                    lane==BlueprintBuildLane.Actuation && !((cell.Layers&CaptureLayer.Actuator)!=0 && cell.Actuator) && PeekOwnedActuator(target.WorldX,target.WorldY)==null;
                // Independent wall body/appearance, wire, permanent actuator
                // and owned cleanup retain their own current-world planning.
                if(!skip)return following;
                following=NextBuildOrdinal(following,cell);
            }
            return following;
        }
        private bool TryPlanConstructionOperation(BlueprintBuildTarget target, BlueprintBuildLane lane, out BlueprintBuildOperation operation)
        {
            operation = null; BlueprintWorldMatchSample actual;
            if (!ReadBuildCell(target.WorldX, target.WorldY, out actual)) return false;
            return TryPlanConstructionOperationFromSample(target,lane,actual,out operation);
        }
        private bool TryPlanConstructionOperationFromSample(BlueprintBuildTarget target,BlueprintBuildLane lane,BlueprintWorldMatchSample actual,out BlueprintBuildOperation operation)
        {
            operation=null;
            if (HoikTarget(target.Cell) && (lane == BlueprintBuildLane.TileBody || lane == BlueprintBuildLane.Shape))
                return TryPlanHoikBuild(target, actual, lane, out operation);
            if (lane == BlueprintBuildLane.TileBody)
            {
                if (!TryPlanSpecialBuild(target, actual, out operation) &&
                    (!BasicPlacement(target.Placement) || target.Placement.RequiresSubstrate || !BuildEmpty(target.Bounds.Left, target.Bounds.Top, false))) return false;
            }
            else if (lane == BlueprintBuildLane.WallBody)
            { if (!BasicPlacement(target.Placement) || !BuildEmpty(target.WorldX, target.WorldY, true)) return false; }
            else if (lane == BlueprintBuildLane.Shape || lane == BlueprintBuildLane.Appearance)
            { if (!TryPlanAppearanceBuild(target, actual, lane, out operation)) return false; }
            else if (!TryPlanWiringBuild(target, actual, lane, out operation)) return false;
            return true;
        }
        private bool BuildOperationBodySame(BlueprintBuildTarget target, BlueprintWorldMatchSample actual)
        {
            if (target == null || !actual.Available) return false;
            if (target.Cell.ObjectId <= 0) return BlueprintMaterialSteps.SameBody(target.Cell, actual, MaterialCatalog,policy:substitutionPolicy) == BlueprintBodyState.Same;
            var value = target.Range?.Materials; BlueprintBodyState body;
            if(value?.BuildObjectBodies==null || value.BuildBodyVersion!=target.Range.Version ||
                !projectionChanges.IsMaterialCurrent(target.Range.Watch,target.Range.PartIndex,value.BuildBodyStamp) ||
                !value.BuildObjectBodies.TryGetValue(target.Cell.ObjectId,out body) || actual.ObjectUnknown)
            {
                // Object-wide body work belongs to the existing material read.
                // Its absent/currently stale result is not a known body mismatch.
                buildUnavailableReads++;DemandProjectionMatchRange(target.Range);return false;
            }
            return body==BlueprintBodyState.Same && actual.Object!=null &&
                (target.Body==null || SameSubstitutionBody(target.Body,actual.Object));
        }
        private bool ConstructionBuildCurrent(BuildFlight flight)
        {
            if (HoikOperation(flight.Operation)) return HoikBuildCurrent(flight);
            if (AppearanceOperation(flight.Operation)) return AppearanceBuildCurrent(flight);
            if (WiringOperation(flight.Operation)) return WiringBuildCurrent(flight);
            return SpecialBuildCurrent(flight);
        }
        private bool ConstructionBuildApplied(BuildFlight flight)
        {
            if (HoikOperation(flight.Operation)) return HoikBuildApplied(flight);
            if (AppearanceOperation(flight.Operation)) return AppearanceBuildApplied(flight);
            if (WiringOperation(flight.Operation)) return WiringBuildApplied(flight);
            return SpecialBuildApplied(flight);
        }
        private bool NativeConstructionConsumer(BuildFlight flight, ToolBuildConsumer consumer, Item material, bool wall, bool animation)
        {
            if (flight != null && !animation && !flight.Checked && !flight.Rejected && flight.Entered &&
                ReferenceEquals(activeBuild,flight) && flight.Token==buildTools.Use.Operation &&
                (flight.Operation==null || flight.Operation.Kind==BlueprintBuildOperationKind.Body || flight.Operation.Kind==BlueprintBuildOperationKind.PlaceBase || flight.Operation.Kind==BlueprintBuildOperationKind.ConvertSubstrate || HoikOperation(flight.Operation)))
                return material==null || OwnsBuildDependency(flight,material);
            if (flight == null || flight.Operation == null || !ReferenceEquals(activeBuild, flight) || flight.Token != buildTools.Use.Operation ||
                flight.Checked || flight.Rejected || !BuildFlightCurrent(flight, true) ||
                Player.tileTargetX != flight.Target.WorldX || Player.tileTargetY != flight.Target.WorldY) return false;
            bool allowed = HoikOperation(flight.Operation) ? NativeHoikConsumer(flight, consumer, material, wall, animation) : AppearanceOperation(flight.Operation) ? NativeAppearanceConsumer(flight, consumer, material, wall, animation) :
                WiringOperation(flight.Operation) ? NativeWiringConsumer(flight, consumer, material, wall, animation) : NativeSpecialConsumer(flight, consumer, material, wall, animation);
            if (allowed && consumer != ToolBuildConsumer.ApplyPaint && consumer != ToolBuildConsumer.ApplyCoating) flight.Entered = true;
            return allowed;
        }
        private Action<Exception> BeginConstructionConsumer(BuildFlight flight,ToolBuildConsumer consumer)
        {
            if(flight?.Operation==null)return null;
            if(HoikOperation(flight.Operation))return BeginHoikConsumer(flight,consumer);
            return WiringOperation(flight.Operation)?BeginWiringBuildConsumer(flight,consumer):
                AppearanceOperation(flight.Operation)?null:BeginSpecialBuildConsumer(flight,consumer);
        }
        private static ushort BuildTargetLayers(BlueprintBuildTarget target)
        {
            switch(target.Operation?.Kind)
            {
                case BlueprintBuildOperationKind.AddWire:return (ushort)(target.Operation.Mode<<4);
                case BlueprintBuildOperationKind.AddActuator:case BlueprintBuildOperationKind.RemoveOwnedActuator:return 8;
                case BlueprintBuildOperationKind.PaintWall:return 2;
                case BlueprintBuildOperationKind.Scrape:return target.Operation.Mode==(int)BlueprintAppearanceAction.ScrapeWallPaint || target.Operation.Mode==(int)BlueprintAppearanceAction.ScrapeWallCoatings?(ushort)2:(ushort)1;
                default:return target.Operation==null && target.Placement?.Kind==BlueprintPlacementKind.Wall?(ushort)2:(ushort)1;
            }
        }
        private static bool BuildSourceSpent(BuildFlight flight,BuildSource source)
        {
            var current=flight.Player.inventory[source.Slot];
            return current!=null && (current.IsAir?0:current.stack)<source.Stack;
        }
        private bool TryCreateOperationFlight(BlueprintBuildTarget target, Player p, int toolSlot, int primarySlot,
            int heldMaximum, int primaryMaximum, out BuildFlight flight)
        {
            flight = null;
            if(target.Operation==null)return InvalidBuildTarget(target);
            if(!IsBuildTargetCurrent(target))return DeferBuildTarget(target);
            if(!BuildRetryReady(target))return false;
            if(toolSlot<0 || toolSlot>=50)return BlockBuildTarget(target,BlueprintBuildWait.Material);
            var tool = p.inventory[toolSlot];
            if (tool == null || tool.IsAir || !buildTools.BuildInventoryEligible(toolSlot, tool))return BlockBuildTarget(target,BlueprintBuildWait.Material|BlueprintBuildWait.Dependencies);
            var dependencies = new List<BuildSource>(); ulong slots = 0;
            if (primarySlot >= 0 && primarySlot != toolSlot)
            {
                if (primarySlot >= 58)return InvalidBuildTarget(target);
                var material = p.inventory[primarySlot];
                if (material == null || material.IsAir || p.inventoryChestStack[primarySlot] ||
                    buildTools.Items.World.ManualMaterials.Contains(material) || buildTools.ProtectedAfterYield(material))
                    return BlockBuildTarget(target,BlueprintBuildWait.Dependencies);
                var primary = SnapshotSource(primarySlot, material); primary.Maximum = primaryMaximum; primary.Primary = true;
                dependencies.Add(primary); slots |= 1UL << primarySlot;
            }
            target.Operation.HeldMaximum = heldMaximum; target.Operation.PrimaryMaximum = primaryMaximum;
            int accessoryMaximum=0;
            if(target.Operation.Consumer==ToolBuildConsumer.Tiles)
            {
                BuildSource[] accessory;ulong accessorySlots,waiting;
                if(!CaptureBuildDependencies(p,out accessory,out accessorySlots,out waiting))
                {WaitForBuild(target,BlueprintBuildWait.Dependencies,waiting);return false;}
                accessoryMaximum=Math.Max(1,target.Body?.MemberCount??1);
                if(!BuildAccessoryRestorationReady(target,p,accessoryMaximum))return false;
                foreach(var dependency in accessory)if(dependency.Slot!=toolSlot && (slots&(1UL<<dependency.Slot))==0)
                {dependency.Maximum=accessoryMaximum;dependencies.Add(dependency);slots|=1UL<<dependency.Slot;}
            }
            if(!CaptureAppearanceRestoreDependencies(target,p,dependencies,ref slots))return false;
            if(HoikOperation(target.Operation) && HoikPickCurrent(p) && hoikPickSlot!=toolSlot && (slots&(1UL<<hoikPickSlot))==0)
            {var pick=SnapshotSource(hoikPickSlot,hoikPick);pick.Maximum=0;dependencies.Add(pick);slots|=1UL<<hoikPickSlot;}
            AddPreparedBuildEndpoints(p,dependencies,toolSlot,ref slots);
            var source = SnapshotSource(toolSlot, tool); source.Maximum = heldMaximum;
            var placement=target.Operation.NativePlacement??target.Placement;
            int direction=placement?.Direction==(int)TileObjectDirection.PlaceLeft?-1:
                placement?.Direction==(int)TileObjectDirection.PlaceRight?1:p.direction;
            flight = new BuildFlight { Target = target, Player = p, Source = source, Dependencies = dependencies.ToArray(),
                DependencySlots = slots, Direction = direction, Generation = ++buildFlightGeneration, InputGesture = ToolInputGesture.HeldPress,
                CursorMode = ToolBuildCursorMode.Precise, AccessoryMaximum=accessoryMaximum };
            long reads=buildUnavailableReads;
            return FinishBuildPredicate(target,ConstructionBuildCurrent(flight),reads);
        }
    }
}
