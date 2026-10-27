using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.Blueprint
{
    // Blueprint chooses current targets. ToolUse alone owns the temporary
    // selection, native input and use token. Neither owner records completion:
    // actual native writes invalidate the existing full world-match proof.
    internal sealed partial class HostBlueprint
    {
        private sealed class BuildSource
        {
            internal int Slot, Type, Stack;
            internal Item Item;
        }
        private sealed class BuildFlight
        {
            internal BlueprintBuildTarget Target;
            internal Player Player;
            internal BuildSource Source;
            internal BuildSource[] Dependencies;
            internal ulong DependencySlots;
            internal int Direction;
            internal long Token, Generation;
            internal bool Checked, Started, Chosen, Entered;
            internal CaptureCell[,] TorchPreparation;
            internal long TorchPreparationGeneration;
        }
        internal sealed class BuildContainerReceipt
        {
            internal BlueprintProjectionIdentity Identity;
            internal long Session, Token, Flight;
            internal int X, Y, Type, Style, Action;
            internal BlueprintProjectionBounds Bounds;
            internal bool Sent, Resolved;
        }
        private sealed class BuildUnconfirmed
        {
            internal int Left, Top, Right, Bottom;
            internal BlueprintPlacementKind Kind;
        }
        private HostTools buildTools;
        private BuildFlight preparedBuild, activeBuild;
        private long buildFlightGeneration;
        // Packet 34 is the one original container request with an explicit
        // success/failure reply. It has no user token, so retain its coordinate
        // obligation even after a projection is removed; a later same-position
        // request cannot accidentally consume the old reply. Session exit ends it.
        private BuildContainerReceipt buildContainer;
        private readonly List<BuildUnconfirmed> buildUnconfirmed = new List<BuildUnconfirmed>();
        private Player buildUnknownPlayer;
        private object buildUnknownWorld, buildUnknownSocket;
        private int buildUnknownMode;
#if DEBUG
        internal long BuildCandidateChecks { get; private set; }
        internal long BuildNativeChoices { get; private set; }
        internal long BuildNativeChecks { get; private set; }
        internal long BuildUsesStarted { get; private set; }
        internal long BuildUnknownResults { get; private set; }
        internal long BuildEligibilityReads { get; private set; }
#endif
        internal void AttachBuildTools(HostTools tools)
        {
            buildTools = tools;
            if (tools == null) return;
            tools.BuildEnabled = () => AutoBuildEnabled && HasSession;
            tools.BuildChoice = ChooseAutomaticBuild;
            tools.BuildReady = () => preparedBuild != null && CanBuildAutomatically(tools.Player);
            MaterialBuildDemand = () => AutoBuildEnabled && HasSession;
        }
        private bool CanBuildAutomatically(Player p, bool prepareOnly = false)
        {
            return AutoBuildEnabled && ControlsEnabled && buildTools != null && buildTools.Available && HasSession &&
                ReferenceEquals(p, player) && !State.Active && !HasActiveCaptureWork && !ProjectionEditing && pendingProjection == null && !deletePending &&
                buildTools.Admit(p, false) && (prepareOnly || !buildTools.ManualLeft) && !buildTools.ManualRight && !p.ghost && !p.noBuilding;
        }
        internal bool OwnsAutomaticBuildInput(Player p)
        { return ReferenceEquals(p, player) && buildTools?.Use.Building == true && buildTools.Use.ActionValid; }
        private void AdvanceAutomaticBuild()
        {
            var p = buildTools?.Player;
            if (p == null || !CanBuildAutomatically(p, true) || MaterialInventory?.Ready != true) { preparedBuild = null; return; }
            // Preparation has no input/selection ownership. A previous native
            // body's animation must not hide the next legal placement window.
            if (preparedBuild != null && BuildFlightCurrent(preparedBuild, false)) return;
            preparedBuild = null;
            // The source/match owner provides a bounded round-robin prefix.
            // A blocked first material must yield to another layer or instance.
            for (int n = 0; n < 64; n++)
            {
                BlueprintBuildTarget target;
                if (!TryGetBuildTarget(out target)) break;
                if (ReferenceEquals(activeBuild?.Target, target) && !activeBuild.Checked) break;
#if DEBUG
                BuildCandidateChecks++;
#endif
                BuildFlight next;
                if (TryPrepareBuild(target, p, out next)) { preparedBuild = next; break; }
                NotifyBuildAttempt(target);
            }
        }
        private bool TryPrepareBuild(BlueprintBuildTarget target, Player p, out BuildFlight result)
        {
            result = null;
            if (!IsBuildTargetCurrent(target) || !BasicPlacement(target.Placement) || IsUnconfirmedTarget(target) ||
                target.Bounds.Left < 6 || target.Bounds.Top < 6 || target.Bounds.Right >= Main.maxTilesX - 6 || target.Bounds.Bottom >= Main.maxTilesY - 6) return false;
            if (!BuildRetryReady(target)) return false;
            if (target.Body != null && TileID.Sets.IsAContainer[target.Placement.TileType] && buildContainer != null)
            { WaitForBuild(target, BlueprintBuildWait.Container); return false; }
            int slot = -1;
            foreach (var source in MaterialInventory.Slots)
                if (source.Eligible && source.ItemType == target.Placement.UseItemType && source.Stack > 0 && buildTools.BuildInventoryEligible(source.Slot, p.inventory[source.Slot])) { slot = source.Slot; break; }
            if (slot < 0) { WaitForBuild(target, BlueprintBuildWait.Material); return false; }
            var item = p.inventory[slot];
            int direction = target.Placement.Direction == (int)TileObjectDirection.PlaceLeft ? -1 :
                target.Placement.Direction == (int)TileObjectDirection.PlaceRight ? 1 : p.direction;
            if (p.pulley && p.pulleyDir == 2 && direction != p.direction ||
                !p.IsInTileInteractionRange(target.WorldX, target.WorldY, TileReachCheckSettings.Simple, item.tileBoost + p.blockRange))
            { WaitForBuild(target, BlueprintBuildWait.Player); return false; }
            if (!NativeBuildGeometry(target, p, item, direction)) return false;
            BuildSource[] dependencies; ulong slots, waiting;
            if (!CaptureBuildDependencies(p, out dependencies, out slots, out waiting))
            { WaitForBuild(target, BlueprintBuildWait.Dependencies, waiting); return false; }
            result = new BuildFlight { Target = target, Player = p, Source = SnapshotSource(slot, item), Dependencies = dependencies, DependencySlots = slots, Direction = direction,
                Generation = ++buildFlightGeneration };
            return true;
        }
        private static bool BasicPlacement(BlueprintPlacementDescriptor value)
        { return value != null && !value.Flexible && value.UseItemType > 0 && value.UseItemType == value.ConsumeItemType && value.Units == 1; }
        private static BuildSource SnapshotSource(int slot, Item item)
        { return new BuildSource { Slot = slot, Item = item, Type = item.type, Stack = item.stack }; }
        private bool CaptureBuildDependencies(Player p, out BuildSource[] dependencies, out ulong slots, out ulong waiting)
        {
            slots = waiting = 0; dependencies = Array.Empty<BuildSource>();
            bool paint = p.autoPaint && p.builderAccStatus[3] == 0, actuator = p.autoActuator && p.builderAccStatus[2] == 0;
            if (!paint && !actuator) return true;
            var sources = new List<BuildSource>();
            // A multi-member object or FillEmptySpace can exhaust the first
            // stack and select its successor during this same native call.
            // Lease only those finite potential accessory sources; all other
            // inventory slots retain their existing protection and ownership.
            for (int i = 0; i < 58; i++)
            {
                var item = p.inventory[i];
                if (item == null || item.IsAir || !(paint && item.PaintOrCoating || actuator && item.type == ItemID.Actuator)) continue;
                if (buildTools.Items.Ownership.IsProtected(i)) continue; // The original guarded selector also excludes it.
                if (p.inventoryChestStack[i] || buildTools.Items.World.ManualMaterials.Contains(item) || buildTools.ProtectedAfterYield(item))
                { waiting = 1UL << i; return false; }
                sources.Add(SnapshotSource(i, item)); slots |= 1UL << i;
            }
            dependencies = sources.ToArray(); return true;
        }
        private bool NativeBuildGeometry(BlueprintBuildTarget target, Player p, Item item, int direction)
        {
            var placement = target.Placement;
            if (item == null || item.IsAir || item.type != placement.UseItemType || item.GetFlexibleTileWand() != null || item.tileWand > 0 ||
                !p.IsInTileInteractionRange(target.WorldX, target.WorldY, TileReachCheckSettings.Simple, item.tileBoost + p.blockRange)) return false;
            if (placement.Kind == BlueprintPlacementKind.Wall)
            {
                if (item.createWall != placement.WallType || !BuildEmpty(target.WorldX, target.WorldY, true)) return false;
                return BuildNeighbor(target.WorldX - 1, target.WorldY, true) || BuildNeighbor(target.WorldX + 1, target.WorldY, true) ||
                    BuildNeighbor(target.WorldX, target.WorldY - 1, true) || BuildNeighbor(target.WorldX, target.WorldY + 1, true);
            }
            if (item.createTile != placement.TileType || item.placeStyle != placement.Style) return false;
            // These original helpers only derive the current biome style.
            // Refuse a substitution that differs from the captured body, while
            // allowing ordinary surface torches with Torch God's Favor on.
            int nativeType = item.createTile, nativeStyle = item.placeStyle;
            if (nativeType == TileID.Torches) p.BiomeTorchPlaceStyle(ref nativeType, ref nativeStyle);
            if (nativeType == TileID.Campfire) p.BiomeCampfirePlaceStyle(ref nativeType, ref nativeStyle);
            if (nativeType != placement.TileType || nativeStyle != placement.Style)
            { RequestBuildEligibilityRetry(target); return false; }
            if (nativeType == 724) return false;
            // Capture may attach a one-member metadata Body to a platform,
            // while its existing catalog descriptor deliberately remains an
            // ordinary recipe. Player routes that descriptor to legacy
            // PlaceTile, never custom CanPlace/TileObject.Place.
            bool platform = placement.Platform && TileID.Sets.Platforms[nativeType];
            if (platform && !BuildPlatformForm(target, nativeType)) return false;
            if (target.Body != null && !platform)
            {
                var data = TileObjectData.GetTileData(item.createTile, item.placeStyle, placement.Alternate);
                if (data == null || target.Bounds.Width != data.Width || target.Bounds.Height != data.Height ||
                    target.WorldX - data.Origin.X != target.Bounds.Left || target.WorldY - data.Origin.Y != target.Bounds.Top) return false;
                return PreflightBuildObject(target, p, item, direction);
            }
            if (!BuildEmpty(target.WorldX, target.WorldY, false)) return false;
            if (Main.tileFrameImportant[item.createTile] && !TileID.Sets.Platforms[item.createTile]) return false;
            if (!PreflightBuildOrdinary(target, item)) return false;
            // Player's special four-neighbour branch accepts every active
            // Tile (including non-solid furniture), but not a wall only in
            // the destination. This changes eligibility, never the catalog.
            if (BuildUsesAnyCardinalNeighbor(item.createTile))
                return BuildNeighbor(target.WorldX - 1, target.WorldY, true) || BuildNeighbor(target.WorldX + 1, target.WorldY, true) ||
                    BuildNeighbor(target.WorldX, target.WorldY - 1, true) || BuildNeighbor(target.WorldX, target.WorldY + 1, true);
            bool connected = BuildNeighbor(target.WorldX, target.WorldY, false) || BuildNeighbor(target.WorldX - 1, target.WorldY, false) ||
                BuildNeighbor(target.WorldX + 1, target.WorldY, false) || BuildNeighbor(target.WorldX, target.WorldY - 1, false) || BuildNeighbor(target.WorldX, target.WorldY + 1, false);
            if (connected) return true;
            if (platform)
            {
                for (int x = target.WorldX - 1; x <= target.WorldX + 1; x++) for (int y = target.WorldY - 1; y <= target.WorldY + 1; y++)
                {
                    BlueprintWorldMatchSample sample;
                    // The platform-only diagonal extension is active Tile,
                    // unlike Player's preceding cardinal/current-wall gate.
                    if (ReadBuildCell(x, y, out sample) && (sample.Cell.Layers & CaptureLayer.Tile) != 0) return true;
                }
            }
            return false;
        }
        private static bool BuildUsesAnyCardinalNeighbor(int type)
        {
            switch (type)
            {
                case 51: case 697: case 330: case 331: case 332: case 333: case 336:
                case 340: case 342: case 341: case 343: case 344: case 379: case 351: return true;
                default: return false;
            }
        }
        private static bool BuildPlatformForm(BlueprintBuildTarget target, int type)
        {
            return target.Placement.Platform && !target.Placement.RequiresObject && target.Cell.TileType == type &&
                target.Cell.FrameY == 18 * target.Placement.Style && target.Bounds.Width == 1 && target.Bounds.Height == 1 &&
                target.WorldX == target.Bounds.Left && target.WorldY == target.Bounds.Top &&
                (target.Body == null || target.Body.Kind == type && target.Body.Width == 1 && target.Body.Height == 1);
        }
        private bool BuildEmpty(int x, int y, bool wall)
        {
#if DEBUG
            BuildEligibilityReads++;
#endif
            BlueprintWorldMatchSample sample;
            return projectionMatchSource != null && projectionMatchSource.TryRead(x, y, out sample) && sample.Available &&
                (sample.Cell.Layers & (wall ? CaptureLayer.Wall : CaptureLayer.Tile)) == 0;
        }
        private bool BuildNeighbor(int x, int y, bool wall)
        {
#if DEBUG
            BuildEligibilityReads++;
#endif
            BlueprintWorldMatchSample sample;
            if (projectionMatchSource == null || !projectionMatchSource.TryRead(x, y, out sample) || !sample.Available) return false;
            if ((sample.Cell.Layers & CaptureLayer.Wall) != 0) return true;
            if ((sample.Cell.Layers & CaptureLayer.Tile) == 0) return false;
            int type = sample.Cell.TileType;
            return wall || Main.tileSolid[type] || TileID.Sets.IsBeam[type] || Main.tileRope[type] || type == TileID.MinecartTrack;
        }
        private ToolIntent ChooseAutomaticBuild(Player p)
        {
            var next = preparedBuild;
            if (next == null || !ReferenceEquals(p, next.Player) || !CanBuildAutomatically(p) || p.itemAnimation > 0 || p.itemTime > 0 || p.channel || p.reuseDelay > 0 ||
                !p.releaseUseItem || p.selectedItemState.HasActiveOverride || p.selectedItemState.HasBufferedChange || !BuildFlightCurrent(next, false)) return null;
            preparedBuild = null;
            var intent = new ToolIntent {
                Kind = ToolKind.Build, Slot = next.Source.Slot, Target = new Vector2(next.Target.WorldX * 16 + 8, next.Target.WorldY * 16 + 8),
                PlayerDirection = next.Direction, MaterialSlots = next.DependencySlots, MaximumConsumption = next.Target.Placement.Kind == BlueprintPlacementKind.Wall ? 5 : 1,
                Valid = () => CanBuildAutomatically(p) && BuildLeaseCurrent(activeBuild ?? next),
                Admitted = () => { next.Token = buildTools.Use.Operation; activeBuild = next; },
                Used = () => { if (activeBuild == null) return; activeBuild.Started = true;
#if DEBUG
                    BuildUsesStarted++;
#endif
                },
                NativeBeginning = () => BeginAutomaticBuildCheck(activeBuild),
                NativePreparing = BeginNativeBuildPreparation,
                NativePlacement = wall => NativeBuildConsumer(activeBuild, wall),
                NativeTileChoice = (objectType, placement, type) => NativeBuildChoice(activeBuild, objectType, placement, type),
                OwnsMaterial = (array, slot) => activeBuild != null && OwnsBuildDependency(activeBuild, array, slot),
                OwnsMaterialIdentity = item => activeBuild != null && OwnsBuildDependency(activeBuild, item),
                ConsumptionValid = () => activeBuild != null && BuildConsumptionValid(activeBuild),
                NativeChecked = () => CheckedAutomaticBuild(activeBuild),
                Completed = (started, unknown) => EndAutomaticBuild(activeBuild, unknown)
            };
            intent.Readiness = () => RefreshAutomaticBuild(p, intent);
            intent.ResolvedTarget = (x,y) => ResolveAutomaticBuildTarget(p,intent,x,y);
            return intent;
        }
        private void BeginAutomaticBuildCheck(BuildFlight flight)
        {
            if (flight == null) return;
            flight.TorchPreparation = null;
            if (flight.Checked) return;
            flight.Entered = false; flight.Source.Stack = flight.Source.Item.stack;
            foreach (var dependency in flight.Dependencies) dependency.Stack = dependency.Item.stack;
        }
        private ToolReadiness RefreshAutomaticBuild(Player p, ToolIntent intent)
        {
            var current = activeBuild;
            if (current == null || current.Token != buildTools.Use.Operation || !CanBuildAutomatically(p) || !BuildLeaseCurrent(current)) return ToolReadiness.Invalid;
            if (!current.Checked) return BuildFlightCurrent(current, true) ? ToolReadiness.Ready : ToolReadiness.Invalid;
            var next = preparedBuild;
            if (next == null) return BuildDiscoveryPending() ? ToolReadiness.Pending : ToolReadiness.Yield;
            if (next.Source.Slot != current.Source.Slot || !ReferenceEquals(next.Source.Item, current.Source.Item) || next.Source.Type != current.Source.Type ||
                next.DependencySlots != intent.MaterialSlots) return ToolReadiness.Yield;
            if (!BuildFlightCurrent(next, false)) { preparedBuild = null; return BuildDiscoveryPending() ? ToolReadiness.Pending : ToolReadiness.Yield; }
            // One native ItemCheck has fully returned before this handoff.
            // The lease stays, but target, baselines and cleanup responsibility
            // belong to the new flight. Original selection/timers stay intact.
            preparedBuild = null; next.Token = current.Token; activeBuild = next;
            intent.Target = new Vector2(next.Target.WorldX * 16 + 8, next.Target.WorldY * 16 + 8);
            intent.PlayerDirection = next.Direction;
            intent.MaximumConsumption = next.Target.Placement.Kind == BlueprintPlacementKind.Wall ? 5 : 1;
            return ToolReadiness.Ready;
        }
        private bool BuildLeaseCurrent(BuildFlight flight)
        {
            if (flight == null || !ReferenceEquals(flight.Player,player) || !CanBuildAutomatically(flight.Player)) return false;
            var source=flight.Source;var p=flight.Player;
            if(source.Slot<0 || source.Slot>=50 || !ReferenceEquals(p.inventory[source.Slot],source.Item) || source.Item.type!=source.Type || source.Item.stack<=0 ||
                !buildTools.BuildInventoryEligible(source.Slot,source.Item))return false;
            foreach(var dependency in flight.Dependencies)
                if(!ReferenceEquals(p.inventory[dependency.Slot],dependency.Item) || dependency.Item.type!=dependency.Type || dependency.Item.stack<=0 ||
                    p.inventoryChestStack[dependency.Slot] || buildTools.Items.World.ManualMaterials.Contains(dependency.Item) || buildTools.ProtectedAfterYield(dependency.Item))return false;
            // A checked target may already have naturally retired. Its old
            // world proof grants no new input; only the exact live source can wait.
            return flight.Checked || BuildFlightCurrent(flight,true);
        }
        private bool ResolveAutomaticBuildTarget(Player p,ToolIntent intent,int x,int y)
        {
            var current=activeBuild;
            if(current==null || current.Checked || !BuildLeaseCurrent(current))return false;
            if(current.Target.WorldX==x && current.Target.WorldY==y)return true;
            // Vanilla smart cursor may continue to a different neighbour.
            // Accept that natural target only through the same source/candidate
            // owner and full preflight; never write SmartCursorX/Y or its locks.
            BlueprintBuildTarget target;BuildFlight next;
            if(!TryLocalBuildTargetAt(x,y,current.Source.Type,out target) || !TryPrepareBuild(target,p,out next) ||
                next.Source.Slot!=current.Source.Slot || !ReferenceEquals(next.Source.Item,current.Source.Item) || next.DependencySlots!=intent.MaterialSlots)return false;
            next.Token=current.Token;activeBuild=next;
            intent.Target=new Vector2(x*16+8,y*16+8);intent.PlayerDirection=next.Direction;
            intent.MaximumConsumption=next.Target.Placement.Kind==BlueprintPlacementKind.Wall?5:1;
            return true;
        }
        private bool BuildFlightCurrent(BuildFlight flight, bool mayOwn)
        {
            var p = flight.Player; var source = flight.Source;
            if ((!IsBuildTargetCurrent(flight.Target) && !NativeTorchPreparationCurrent(flight)) || !ReferenceEquals(p, player)) return false;
            if (source.Slot < 0 || source.Slot >= 50 ||
                !ReferenceEquals(p.inventory[source.Slot], source.Item) || source.Item.type != source.Type || source.Item.stack <= 0 ||
                !buildTools.BuildInventoryEligible(source.Slot, source.Item))
            { WaitForBuild(flight.Target, BlueprintBuildWait.Material, retryPending: true); return false; }
            if (mayOwn && activeBuild != null && (!ReferenceEquals(activeBuild, flight) || flight.Token != buildTools.Use.Operation)) return false;
            foreach (var dependency in flight.Dependencies)
                if (!ReferenceEquals(p.inventory[dependency.Slot], dependency.Item) || dependency.Item.type != dependency.Type || dependency.Item.stack <= 0 ||
                    p.inventoryChestStack[dependency.Slot] || buildTools.Items.World.ManualMaterials.Contains(dependency.Item) || buildTools.ProtectedAfterYield(dependency.Item) ||
                    buildTools.Items.Ownership.IsProtected(dependency.Slot) && !buildTools.Use.OwnsBuildSource(p.inventory, dependency.Slot))
                { WaitForBuild(flight.Target, BlueprintBuildWait.Dependencies, 1UL << dependency.Slot, true); return false; }
            if (p.pulley && p.pulleyDir == 2 && flight.Direction != p.direction ||
                !p.IsInTileInteractionRange(flight.Target.WorldX, flight.Target.WorldY, TileReachCheckSettings.Simple, source.Item.tileBoost + p.blockRange))
            { WaitForBuild(flight.Target, BlueprintBuildWait.Player, retryPending: true); return false; }
            return NativeBuildGeometry(flight.Target, p, source.Item, flight.Direction);
        }
        private bool NativeBuildConsumer(BuildFlight flight, bool wall)
        {
            if (flight == null || !ReferenceEquals(activeBuild, flight) || flight.Token != buildTools.Use.Operation || flight.Checked ||
                (flight.Target.Placement.Kind == BlueprintPlacementKind.Wall) != wall || !BuildFlightCurrent(flight, true)) return false;
            if (!flight.Player.ItemTimeIsZero || flight.Player.itemAnimation <= 0 || !flight.Player.controlUseItem) return false;
            flight.Entered = true;
            // Once the consumer starts, its natural observer transaction owns
            // all neighbor/member side effects. No second per-cell success bit.
            return true;
        }
        private bool NativeBuildChoice(BuildFlight flight, bool objectType, TileObject placement, int type)
        {
            if (!NativeBuildConsumer(flight, false) || type != flight.Target.Placement.TileType) return false;
            if (flight.Target.Placement.Platform) return !objectType && BuildPlatformForm(flight.Target, type);
            if (flight.Target.Body == null) return !objectType;
            if (!objectType) return NativeLegacyBuildChoice(flight, type);
            var data = TileObjectData.GetTileData(type, placement.style, placement.alternate);
            if (data == null || placement.xCoord != flight.Target.Bounds.Left || placement.yCoord != flight.Target.Bounds.Top ||
                data.Width != flight.Target.Bounds.Width || data.Height != flight.Target.Bounds.Height) return false;
            int style = data.CalculatePlacementStyle(placement.style, placement.alternate, placement.random), line = 0;
            if (data.StyleWrapLimit > 0) { line = style / data.StyleWrapLimit * data.StyleLineSkip; style %= data.StyleWrapLimit; }
            var cell = new CaptureCell { X = placement.xCoord, Y = placement.yCoord, Layers = CaptureLayer.Tile, TileType = (ushort)type,
                FrameX = checked((short)(data.CoordinateFullWidth * (data.StyleHorizontal ? style : line))),
                FrameY = checked((short)(data.CoordinateFullHeight * (data.StyleHorizontal ? line : style))) };
            var shape = BlueprintCaptureSource.DescribeRead(cell, BlueprintWorldObjectSemantics.TileValue(cell)); BlueprintWorldObject actual;
            if (!BlueprintWorldObjectSemantics.TryCreate(cell, shape, out actual) || actual == null || !flight.Target.Body.SamePlacement(actual)) return false;
            flight.Chosen = true;
#if DEBUG
            BuildNativeChoices++;
#endif
            return true;
        }
        private bool NativeLegacyBuildChoice(BuildFlight flight, int type, bool predictFlattening = false)
        {
            if (type == 82 || type == 227)
            {
                // .8 excludes these two declared metadata types from custom
                // CanPlace. Their original PlaceAlch/PlaceDye qualification is
                // checked separately; the actual legacy write must still have
                // the exact captured descriptor and allowed starting frame.
                var plant = new CaptureCell { X = flight.Target.WorldX, Y = flight.Target.WorldY, Layers = CaptureLayer.Tile, TileType = (ushort)type,
                    FrameX = checked((short)((type == 82 ? 18 : 34) * flight.Target.Placement.Style)), FrameY = 0 };
                return SameLegacyBuildBody(flight, plant);
            }
            // .8 Player explicitly routes the normal torch around custom
            // TileObject.CanPlace. Its deterministic CheckTorch attachment
            // priority must still match the captured metadata form before use.
            if (type != TileID.Torches) return false;
            CaptureCell[,] cells; bool flattened;
            return ReadBuildTorchCells(flight, predictFlattening, out cells, out flattened) && MatchBuildTorchCells(flight, cells);
        }
        private static bool SameLegacyBuildBody(BuildFlight flight, CaptureCell cell)
        {
            var shape = BlueprintCaptureSource.DescribeRead(cell, BlueprintWorldObjectSemantics.TileValue(cell)); BlueprintWorldObject actual;
            return BlueprintWorldObjectSemantics.TryCreate(cell, shape, out actual) && actual != null && flight.Target.Body.SamePlacement(actual);
        }
        private static bool BuildActive(CaptureCell cell) { return (cell.Layers & CaptureLayer.Tile) != 0 && !cell.Inactive; }
        private static bool BuildTree(int type, CaptureCell above, CaptureCell below)
        { return type >= 0 && WorldGen.IsTreeType(type) && BuildActive(above) && WorldGen.IsTreeType(above.TileType) && BuildActive(below) && WorldGen.IsTreeType(below.TileType); }
        private static bool OwnsBuildDependency(BuildFlight flight, Item[] array, int slot)
        {
            if (!ReferenceEquals(array, flight.Player.inventory)) return false;
            foreach (var source in flight.Dependencies) if (source.Slot == slot && ReferenceEquals(source.Item, array[slot]) && (source.Item.type == source.Type || source.Item.IsAir)) return true;
            return false;
        }
        private static bool OwnsBuildDependency(BuildFlight flight, Item item)
        {
            foreach (var source in flight.Dependencies)
                if (ReferenceEquals(source.Item, item) && ReferenceEquals(flight.Player.inventory[source.Slot], item) && (item.type == source.Type || item.IsAir)) return true;
            return false;
        }
        private static bool BuildConsumptionValid(BuildFlight flight)
        {
            foreach (var source in flight.Dependencies)
            {
                Item current = flight.Player.inventory[source.Slot];
                if (!ReferenceEquals(current, source.Item) || current.stack > source.Stack || current.stack < 0 || !current.IsAir && current.type != source.Type) return false;
            }
            return true;
        }
        private void CheckedAutomaticBuild(BuildFlight flight)
        {
            if (flight == null || !ReferenceEquals(activeBuild, flight) || flight.Token != buildTools.Use.Operation || flight.Checked || !flight.Entered) return;
            flight.Checked = true;
            flight.TorchPreparation = null;
#if DEBUG
            BuildNativeChecks++;
#endif
            bool spent = flight.Source.Item.stack < flight.Source.Stack;
            foreach (var source in flight.Dependencies) spent |= source.Item.stack < source.Stack;
            if (spent && BuildEmpty(flight.Target.Bounds.Left, flight.Target.Bounds.Top, flight.Target.Placement.Kind == BlueprintPlacementKind.Wall))
            {
                // Consumption without an observable body is unknown, never a
                // retry key renewed by the decreasing stack. Protect the real
                // sources and this target; independent targets can still run.
                HoldUnconfirmedBuild(flight);
            }
            // No-consumption native rejection is not an unknown result. Its
            // prerequisite may change outside our local Tile dependencies.
            // Unknown/spent outcomes retain their existing protection and
            // can never be renewed by this bounded eligibility retry.
            if (!spent) RequestBuildEligibilityRetry(flight.Target);
            else RememberBuildFrontier(flight.Target);
            NotifyBuildAttempt(flight.Target);
        }
        private void HoldUnconfirmedBuild(BuildFlight flight)
        {
            // A later native failure can make an earlier no-spend check
            // unknown. Revoke only this target's retry permission; another
            // rejected sibling in the same range keeps its own recovery.
            var materials = flight.Target.Range.Materials;
            if (materials != null && materials.EligibilityRetryOrdinal == flight.Target.Ordinal) materials.EligibilityRetry = false;
            if (!IsUnconfirmedTarget(flight.Target)) buildUnconfirmed.Add(new BuildUnconfirmed { Left = flight.Target.Bounds.Left, Top = flight.Target.Bounds.Top,
                Right = flight.Target.Bounds.Right, Bottom = flight.Target.Bounds.Bottom, Kind = flight.Target.Placement.Kind });
            buildTools.HoldUnknown(flight.DependencySlots | (1UL << flight.Source.Slot));
            projectionFeedback = "一次施工结果未确认，相关材料已受保护；其他可建部分继续。";
#if DEBUG
            BuildUnknownResults++;
#endif
        }
        private bool IsUnconfirmedTarget(BlueprintBuildTarget target)
        {
            // Unknown belongs to the actual world layer/footprint, not the
            // disposable projection or a newly selected same-type source.
            // Keep only finite scalar bounds, never retired source/range DTOs.
            foreach (var failure in buildUnconfirmed)
                if (failure.Kind == target.Placement.Kind && failure.Left < target.Bounds.Right && target.Bounds.Left < failure.Right &&
                    failure.Top < target.Bounds.Bottom && target.Bounds.Top < failure.Bottom) return true;
            return false;
        }
        private void EndAutomaticBuild(BuildFlight flight, bool unknown)
        {
            if (flight == null || !ReferenceEquals(activeBuild, flight) || flight.Token != buildTools.Use.Operation) return;
            if (unknown) HoldUnconfirmedBuild(flight);
            else if (!flight.Checked) WaitForBuild(flight.Target, BlueprintBuildWait.Player, retryPending: true);
            activeBuild = null; NotifyBuildAttempt(flight.Target);
        }
        internal void CancelAutomaticBuild()
        { preparedBuild = null; if (buildTools?.Use.Building == true) buildTools.Use.Cancel(); }
        internal void AutomaticBuildChanged()
        { preparedBuild = null; if (AutoBuildEnabled) buildActivationRevision++; else RemoveBuildSources(); MaterialBuildEligibilityRevision++; ObserveMaterialDemand(); }
        private void ClearAutomaticBuild()
        {
            CancelAutomaticBuild(); if (buildTools?.Use.Building == true) buildTools.Use.Retire(); preparedBuild = activeBuild = null;
            // RemoveAll/Clear retires projection work, not an original packet
            // already sent in this connection. Keep that finite responsibility
            // until its decoder reply or the actual session boundary below.
            MaterialBuildEligibilityRevision++; ClearBuildProgressSignals(); RemoveBuildSources();
        }
        private void ClearBuildSession() { buildContainer = null; }
        private void StartBuildUnknownSession()
        {
            // Match Tools' real identity boundary. A menu/UI interruption or
            // projection removal cannot renew unknown consumption in the same
            // player/world/connection; an actual replacement releases it.
            object socket = Main.netMode == 1 ? Netplay.Connection.Socket : null;
            if (!ReferenceEquals(buildUnknownPlayer, Main.LocalPlayer) || !ReferenceEquals(buildUnknownWorld, Main.ActiveWorldFileData) ||
                !ReferenceEquals(buildUnknownSocket, socket) || buildUnknownMode != Main.netMode) buildUnconfirmed.Clear();
            buildUnknownPlayer = Main.LocalPlayer; buildUnknownWorld = Main.ActiveWorldFileData; buildUnknownSocket = socket; buildUnknownMode = Main.netMode;
        }
        private void RetireAutomaticBuild(BlueprintProjectionIdentity identity)
        {
            RemoveBuildSources(identity);
            if (preparedBuild?.Target.Identity.Equals(identity) == true) preparedBuild = null;
            // Natural completion can retire the just-checked instance between
            // two legal placements. Keep only that returned lease available
            // for Refresh into another current target; unfinished/native work
            // still cancels, and editing/clear/session keep their own gates.
            if (activeBuild?.Target.Identity.Equals(identity) == true && buildTools?.Use.Building == true &&
                (!activeBuild.Checked || buildTools.Use.InNativeUse)) buildTools.Use.Cancel();
        }
        private bool BuildBlocksRetirement(BlueprintProjectionPlacement item)
        {
            if (activeBuild != null && activeBuild.Target.Identity.Equals(item.Identity) && buildTools?.Use.InNativeUse == true) return true;
            var receipt = buildContainer;
            if (receipt == null || receipt.Resolved) return false;
            // A newer projection over the same predicted body cannot turn an
            // old in-flight container into proven construction. This O(1)
            // coarse overlap only delays retirement; it creates no Match.
            return receipt.Identity.Equals(item.Identity) || item.OriginX < receipt.Bounds.Right && receipt.Bounds.Left < (long)item.OriginX + item.Summary.Width &&
                item.OriginY < receipt.Bounds.Bottom && receipt.Bounds.Top < (long)item.OriginY + item.Summary.Height;
        }
        internal BuildContainerReceipt BeginBuildContainer(int x, int y, int type, int style)
        {
            var flight = activeBuild;
            if (Main.netMode != 1 || flight == null || buildTools?.Use.InNativeUse != true || !flight.Chosen ||
                flight.Token != buildTools.Use.Operation || flight.Target.WorldX != x || flight.Target.WorldY != y || flight.Target.Placement.TileType != type ||
                flight.Target.Placement.Style != style || buildContainer != null) return null;
            return buildContainer = new BuildContainerReceipt { Identity = flight.Target.Identity, Session = generation, Token = flight.Token, Flight = flight.Generation, X = x, Y = y, Type = type, Style = style,
                Bounds = flight.Target.Bounds, Action = type == TileID.Containers ? 0 : type == TileID.Containers2 ? 4 : 2 };
        }
        internal void EndBuildContainer(BuildContainerReceipt receipt, int result, Exception error)
        {
            if (receipt == null || !ReferenceEquals(receipt, buildContainer)) return;
            if (error != null) { if (activeBuild != null && activeBuild.Token == receipt.Token && activeBuild.Generation == receipt.Flight) HoldUnconfirmedBuild(activeBuild); return; }
            if (result < 0) { receipt.Resolved = true; buildContainer = null; buildContainerRevision++; return; }
            receipt.Sent = true;
        }
        internal BuildContainerReceipt MatchBuildContainer(byte[] bytes, int start, int length)
        {
            var receipt = buildContainer;
            if (receipt == null || !receipt.Sent || receipt.Resolved || Main.netMode != 1 || receipt.Session != generation || !HasSession || bytes == null || start < 0 || length < 10 || start > bytes.Length - length || bytes[start] != 34) return null;
            return bytes[start + 1] == receipt.Action && ReadBuildShort(bytes, start + 2) == receipt.X && ReadBuildShort(bytes, start + 4) == receipt.Y && ReadBuildShort(bytes, start + 6) == receipt.Style ? receipt : null;
        }
        private static short ReadBuildShort(byte[] data, int at) { return unchecked((short)(data[at] | data[at + 1] << 8)); }
        internal void ReceiveBuildContainer(BuildContainerReceipt receipt, bool accepted, Exception error)
        {
            if (receipt == null || !ReferenceEquals(receipt, buildContainer) || receipt.Session != generation) return;
            if (error != null) { projectionFeedback = "容器施工回执未能确认，相关图纸等待原版同步。"; return; }
            receipt.Resolved = true; buildContainer = null; buildContainerRevision++;
            if (!accepted) projectionFeedback = "原版未接受这次容器放置，图纸已保留。";
        }
    }
}
