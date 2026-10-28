using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.Enums;

namespace JueMingR.TerrariaHost.Blueprint
{
    // One finite lane identity is shared by forward, nearby, frontier and
    // continuity walks. A furniture body is placed once; its members retain
    // their own appearance/wire/actuation work in the same bounded source.
    internal enum BlueprintBuildLane { TileBody, WallBody, Shape, Appearance, Wire, Actuation }
    internal enum BlueprintBuildOperationKind
    { Body, Hammer, PaintTile, PaintWall, Scrape, AddWire, AddActuator, SetInactive, RemoveOwnedActuator, PlaceBase, ConvertSubstrate }
    internal sealed class BlueprintBuildOperation
    {
        internal BlueprintBuildOperationKind Kind;
        internal ToolBuildConsumer Consumer;
        internal int Mode, SupplyType, Cycle, BucketType;
        internal int SupplyDestination = -1, PrimaryMaximum = 1, HeldMaximum;
        // This native step may place a recipe's base rather than its final
        // target. The immutable target Cell/Body and final Match never change.
        internal BlueprintPlacementDescriptor NativePlacement;
    }
    internal sealed partial class HostBlueprint
    {
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
        private bool TryPrepareConstructionBuild(BlueprintBuildTarget target, Player p, out BuildFlight flight)
        {
            if (AppearanceOperation(target.Operation)) return TryPrepareAppearanceBuild(target, p, out flight);
            if (WiringOperation(target.Operation)) return TryPrepareWiringBuild(target, p, out flight);
            return TryPrepareSpecialBuild(target, p, out flight);
        }
        private bool TryCreateConstructionTarget(BlueprintProjectionPlacement owner, BlueprintProjectionMatchRange range,
            BlueprintProjectionSourcePart part, int index, int ordinal, long support, out BlueprintBuildTarget target, int preferredItemType = 0)
        {
            target = null; var catalog = MaterialCatalog;
            if (catalog == null || index < 0 || index >= part.Window.Cells.Count || !ProjectionQualificationCurrent(owner)) return false;
            var lane = BuildLane(ordinal);
            var raw = owner.Mask.Apply(part.Window.Cells[index]);
            CaptureObject? obj = part.LogicalObjectId > 0 && part.Window.Objects.Count == 1 ? part.Window.Objects[0] : (CaptureObject?)null;
            var selection = SelectProjectionTarget(owner, raw, obj);
            var cell = selection.Apply(raw);
            // Unknown declarations grant no automatic action. Independent
            // confirmed layers on the same member still retain their lanes.
            cell = new BlueprintConstructionSelection((ushort)(selection.RequiredBits & ~selection.UnknownBits),0,catalog.Revision).Apply(cell);
            if (!BuildLanePresent(cell, lane) || part.LogicalObjectId > 0 && lane == BlueprintBuildLane.TileBody && index != 0 ||
                part.LogicalObjectId == 0 && cell.ObjectId > 0 && (lane == BlueprintBuildLane.TileBody || lane == BlueprintBuildLane.Shape)) return false;
            BlueprintWorldObject body = null;
            if (obj.HasValue && (cell.Layers & CaptureLayer.Tile) != 0 && !projectionMatchSource.TryDescribeTarget(obj.Value, cell, owner.OriginX, owner.OriginY, out body)) return false;
            BlueprintPlacementDescriptor descriptor = lane == BlueprintBuildLane.WallBody ? catalog.Wall(cell.WallType) :
                (cell.Layers & CaptureLayer.Tile) != 0 ? catalog.Substrate(cell) ?? catalog.Tile(cell, obj) : null;
            if (lane == BlueprintBuildLane.TileBody || lane == BlueprintBuildLane.WallBody)
            { if (descriptor == null) return false; }
            var next = CreateMaterialBuildTarget(owner, range, cell, descriptor, obj, body, ordinal, support, catalog.Revision);
            if (lane != BlueprintBuildLane.TileBody)
            {
                next.TargetX = owner.OriginX + cell.X; next.TargetY = owner.OriginY + cell.Y;
                next.Bounds = new BlueprintProjectionBounds(next.TargetX, next.TargetY, 1, 1);
            }
            next.LocalProof = true;
            if (!BuildNearBounds(owner).Contains(next.WorldX - owner.OriginX, next.WorldY - owner.OriginY))
            { range.Materials.BuildWaits |= BlueprintBuildWait.Player; return false; }
            BlueprintWorldMatchSample actual;
            if (!ReadBuildCell(next.WorldX, next.WorldY, out actual)) return false;
            BlueprintBuildOperation operation = null;
            if (lane == BlueprintBuildLane.TileBody)
            {
                if (!TryPlanSpecialBuild(next, actual, out operation))
                {
                    if (!BasicPlacement(descriptor) || descriptor.RequiresSubstrate || !BuildEmpty(next.Bounds.Left, next.Bounds.Top, false)) return false;
                }
            }
            else if (lane == BlueprintBuildLane.WallBody)
            { if (!BasicPlacement(descriptor) || !BuildEmpty(next.WorldX, next.WorldY, true)) return false; }
            else if (lane == BlueprintBuildLane.Shape || lane == BlueprintBuildLane.Appearance)
            { if (!TryPlanAppearanceBuild(next, actual, lane, out operation)) return false; }
            else if (!TryPlanWiringBuild(next, actual, lane, out operation)) return false;
            next.Operation = operation;
            // The old direct path can reject absent supplies without another
            // native preflight. Tool operations select their actual capability
            // and independent primary supply only in preparation.
            if (operation == null && !buildSupply.ContainsKey(descriptor.UseItemType))
            { range.Materials.BuildWaits |= BlueprintBuildWait.Material; return false; }
            if (preferredItemType > 0 && operation == null && descriptor.UseItemType != preferredItemType) return false;
            target = next; return true;
        }
        private bool BuildOperationBodySame(BlueprintBuildTarget target, BlueprintWorldMatchSample actual)
        {
            if (target == null || !actual.Available) return false;
            if (target.Cell.ObjectId <= 0) return BlueprintMaterialSteps.SameBody(target.Cell, actual, MaterialCatalog) == BlueprintBodyState.Same;
            var value = target.Range?.Materials; BlueprintBodyState body;
            return value?.BuildObjectBodies != null && value.BuildBodyVersion == target.Range.Version &&
                projectionChanges.IsMaterialCurrent(target.Range.Watch, target.Range.PartIndex, value.BuildBodyStamp) &&
                value.BuildObjectBodies.TryGetValue(target.Cell.ObjectId, out body) && body == BlueprintBodyState.Same &&
                actual.Object != null && (target.Body == null || target.Body.SamePlacement(actual.Object));
        }
        private bool ConstructionBuildCurrent(BuildFlight flight)
        {
            if (AppearanceOperation(flight.Operation)) return AppearanceBuildCurrent(flight);
            if (WiringOperation(flight.Operation)) return WiringBuildCurrent(flight);
            return SpecialBuildCurrent(flight);
        }
        private bool ConstructionBuildApplied(BuildFlight flight)
        {
            if (AppearanceOperation(flight.Operation)) return AppearanceBuildApplied(flight);
            if (WiringOperation(flight.Operation)) return WiringBuildApplied(flight);
            return SpecialBuildApplied(flight);
        }
        private bool NativeConstructionConsumer(BuildFlight flight, ToolBuildConsumer consumer, Item material, bool wall, bool animation)
        {
            if (flight != null && !animation && !flight.Checked && !flight.Rejected && flight.Entered &&
                ReferenceEquals(activeBuild,flight) && flight.Token==buildTools.Use.Operation &&
                (flight.Operation==null || flight.Operation.Kind==BlueprintBuildOperationKind.Body || flight.Operation.Kind==BlueprintBuildOperationKind.PlaceBase || flight.Operation.Kind==BlueprintBuildOperationKind.ConvertSubstrate))
                return material==null || OwnsBuildDependency(flight,material);
            if (flight == null || flight.Operation == null || !ReferenceEquals(activeBuild, flight) || flight.Token != buildTools.Use.Operation ||
                flight.Checked || flight.Rejected || !BuildFlightCurrent(flight, true) ||
                Player.tileTargetX != flight.Target.WorldX || Player.tileTargetY != flight.Target.WorldY) return false;
            bool allowed = AppearanceOperation(flight.Operation) ? NativeAppearanceConsumer(flight, consumer, material, wall, animation) :
                WiringOperation(flight.Operation) ? NativeWiringConsumer(flight, consumer, material, wall, animation) : NativeSpecialConsumer(flight, consumer, material, wall, animation);
            if (allowed && consumer != ToolBuildConsumer.ApplyPaint && consumer != ToolBuildConsumer.ApplyCoating) flight.Entered = true;
            return allowed;
        }
        private Action<Exception> BeginConstructionConsumer(BuildFlight flight,ToolBuildConsumer consumer)
        {
            if(flight?.Operation==null)return null;
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
            if (target.Operation == null || toolSlot < 0 || toolSlot >= 50 || !IsBuildTargetCurrent(target) || !BuildRetryReady(target)) return false;
            var tool = p.inventory[toolSlot];
            if (tool == null || tool.IsAir || !buildTools.BuildInventoryEligible(toolSlot, tool)) return false;
            var dependencies = new List<BuildSource>(); ulong slots = 0;
            if (primarySlot >= 0 && primarySlot != toolSlot)
            {
                if (primarySlot >= 58) return false;
                var material = p.inventory[primarySlot];
                if (material == null || material.IsAir || p.inventoryChestStack[primarySlot] ||
                    buildTools.Items.World.ManualMaterials.Contains(material) || buildTools.ProtectedAfterYield(material)) return false;
                var primary = SnapshotSource(primarySlot, material); primary.Maximum = primaryMaximum; primary.Primary = true;
                dependencies.Add(primary); slots |= 1UL << primarySlot;
            }
            target.Operation.HeldMaximum = heldMaximum; target.Operation.PrimaryMaximum = primaryMaximum;
            int accessoryMaximum=0;
            if(target.Operation.Consumer==ToolBuildConsumer.Tiles)
            {
                BuildSource[] accessory;ulong accessorySlots,waiting;
                if(!CaptureBuildDependencies(p,out accessory,out accessorySlots,out waiting))return false;
                accessoryMaximum=Math.Max(1,target.Body?.MemberCount??1);
                foreach(var dependency in accessory)if(dependency.Slot!=toolSlot && (slots&(1UL<<dependency.Slot))==0)
                {dependency.Maximum=accessoryMaximum;dependencies.Add(dependency);slots|=1UL<<dependency.Slot;}
            }
            AddPreparedBuildEndpoints(p,dependencies,toolSlot,ref slots);
            var source = SnapshotSource(toolSlot, tool); source.Maximum = heldMaximum;
            var placement=target.Operation.NativePlacement??target.Placement;
            int direction=placement?.Direction==(int)TileObjectDirection.PlaceLeft?-1:
                placement?.Direction==(int)TileObjectDirection.PlaceRight?1:p.direction;
            flight = new BuildFlight { Target = target, Player = p, Source = source, Dependencies = dependencies.ToArray(),
                DependencySlots = slots, Direction = direction, Generation = ++buildFlightGeneration, InputGesture = ToolInputGesture.HeldPress, AccessoryMaximum=accessoryMaximum };
            return ConstructionBuildCurrent(flight);
        }
    }
}
