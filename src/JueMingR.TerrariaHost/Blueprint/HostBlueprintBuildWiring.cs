using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.UI;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private sealed class OwnedBuildActuator
        {
            internal int X, Y, Type;
            internal long Session, Token;
            internal Tile Tile;
            internal BlueprintProjectionIdentity Identity;
            internal bool Confirmed, Removed, Invalid;
        }
        private readonly Dictionary<long, OwnedBuildActuator> ownedBuildActuators = new Dictionary<long, OwnedBuildActuator>();
        private BuildFlight wiringNativeFlight;
        private byte wiringNativeWires;
        private bool wiringNativeActuator;
        private static long WiringCoordinate(int x, int y) { return (long)(uint)x << 32 | (uint)y; }
        private void InitializeWiringBuild() { ConstructionNativeHooks.Install(this); }

        private bool TryPlanWiringBuild(BlueprintBuildTarget target, BlueprintWorldMatchSample actual, BlueprintBuildLane lane, out BlueprintBuildOperation operation)
        {
            operation = null; if (!actual.Available) return false;
            if (wiringReply != null && wiringReply.Flight.Target.WorldX == target.WorldX && wiringReply.Flight.Target.WorldY == target.WorldY) return false;
            if (lane == BlueprintBuildLane.Wire)
            {
                int missing = target.Cell.Wires & ~actual.Cell.Wires & 15;
                if (missing == 0) return false;
                operation = new BlueprintBuildOperation { Kind = BlueprintBuildOperationKind.AddWire, Consumer = ToolBuildConsumer.Wiring,
                    Mode = missing & -missing, SupplyType = ItemID.Wire, PrimaryMaximum = 1 };
                return true;
            }
            if (lane != BlueprintBuildLane.Actuation) return false;
            bool permanent = (target.Cell.Layers & CaptureLayer.Actuator) != 0 && target.Cell.Actuator;
            var owned = PeekOwnedActuator(target.WorldX, target.WorldY);
            if (!permanent && owned != null && owned.Confirmed && WiringBodySame(target, actual) && actual.Cell.Inactive == target.Cell.Inactive)
            {
                operation = new BlueprintBuildOperation { Kind = BlueprintBuildOperationKind.RemoveOwnedActuator, Consumer = ToolBuildConsumer.Wiring, PrimaryMaximum = 0 };
                return true;
            }
            if (permanent && !actual.Cell.Actuator)
            {
                operation = new BlueprintBuildOperation { Kind = BlueprintBuildOperationKind.AddActuator, Consumer = ToolBuildConsumer.Wiring,
                    SupplyType = ItemID.Actuator, PrimaryMaximum = 0, HeldMaximum = 1 };
                return true;
            }
            if ((target.Cell.Layers & CaptureLayer.Tile) == 0 || !WiringBodySame(target, actual) || target.Cell.Inactive == actual.Cell.Inactive) return false;
            if (!CanSwitchBuildInactive(target)) return false;
            if (!actual.Cell.Actuator)
            {
                // Do not create a known cleanup debt when the switching or
                // removal tool is absent. Existing player actuators are never
                // enrolled in this ownership record.
                if (ConstructionItemSlot(player, 3620) < 0 || !permanent && ConstructionItemSlot(player, 510) < 0 && ConstructionItemSlot(player, 3611) < 0) return false;
                // One unfinished temporary chain is enough. It may yield for
                // range/tool recovery while unrelated construction continues;
                // walking away must not accumulate a map of cleanup debts.
                if (!permanent && owned == null && (ownedBuildActuators.Count != 0 || wiringReply?.TemporaryActuator != null)) return false;
                operation = new BlueprintBuildOperation { Kind = BlueprintBuildOperationKind.AddActuator, Consumer = ToolBuildConsumer.Wiring,
                    Mode = permanent ? 0 : 1, SupplyType = ItemID.Actuator, PrimaryMaximum = 0, HeldMaximum = 1 };
            }
            else operation = new BlueprintBuildOperation { Kind = BlueprintBuildOperationKind.SetInactive, Consumer = ToolBuildConsumer.Wiring,
                Mode = target.Cell.Inactive ? 1 : 0, PrimaryMaximum = 0 };
            return true;
        }
        private bool WiringBodySame(BlueprintBuildTarget target, BlueprintWorldMatchSample actual)
        {
            return BuildOperationBodySame(target, actual);
        }
        private bool TryPrepareWiringBuild(BlueprintBuildTarget target, Player p, out BuildFlight flight, BuildOperationPlan selectedPlan)
        {
            flight = null;
            BuildOperationPlan plan = selectedPlan; BlueprintBuildWait wait; ulong dependencySlots;
            if (plan == null && !TryPlanWiringTool(target, p, PreferredBuildSourceHint(p), out plan, out wait, out dependencySlots))
            {
                WaitForWiringOptions(target, wait, dependencySlots);
                return false;
            }
            // A rejected option never changes the requested operation or its
            // budget. Only this selected exact tool may enter real preparation.
            target.Operation = CopyBuildOperation(plan.Operation);
            if (PlannedBuildToolSlot(p, plan, buildMaterialPreparation?.HasWork == true) < 0) { WaitForBuild(target, BlueprintBuildWait.Material, retryPending: true); return false; }
            int primary = plan.SupplySlot;
            if (plan.SupplyType > 0 && !TryPrepareBuildSupply(target, p, plan.SupplyType, plan.SupplyDestination, out primary, plan)) return false;
            if (!PlannedBuildSupplyCurrent(p, plan, primary)) { WaitForDynamicBuild(target); return false; }
            int tool = PlannedBuildToolSlot(p, plan, true);
            if (tool < 0) { WaitForBuild(target, BlueprintBuildWait.Material, retryPending: true); return false; }
            return TryCreateOperationFlight(target, p, tool, primary, plan.HeldMaximum, plan.PrimaryMaximum, out flight);
        }
        private bool TryPlanWiringTool(BlueprintBuildTarget target, Player p, BuildSource preferred,
            out BuildOperationPlan plan, out BlueprintBuildWait wait, out ulong dependencySlots)
        {
            plan = null; wait = BlueprintBuildWait.None; dependencySlots = 0;
            if (target?.Operation == null || p == null || MaterialInventory?.Ready != true || !IsBuildTargetCurrent(target)) return false;
            BlueprintWorldMatchSample actual;
            if (!ReadBuildCell(target.WorldX, target.WorldY, out actual) || !p.CanDoWireStuffHere(target.WorldX, target.WorldY)) return false;
            BlueprintBuildWait localWait; ulong localSlots; bool found = false;
            if (preferred != null && preferred.Slot >= 0 && preferred.Slot < 50 &&
                ReferenceEquals(p.inventory[preferred.Slot], preferred.Item) && preferred.Item.type == preferred.Type &&
                WiringToolMatches(target.Operation, preferred.Type))
            {
                found = true;
                if (TryPlanWiringOption(target, p, preferred.Slot, actual, out plan, out localWait, out localSlots)) return true;
                wait |= localWait; dependencySlots |= localSlots;
            }
            // The inventory owner already indexes every eligible slot of each
            // type. Reuse that frozen mask, then recheck each exact live Item;
            // neither a short tool nor an ineligible twin ends the search.
            for (int option = 0; option < 3; option++)
            {
                int type = WiringToolType(target.Operation, option);
                if (type <= 0) continue;
                ConstructionItemSlot(p, type);
                ulong candidates;
                if (!constructionSlots.TryGetValue(type, out candidates)) continue;
                for (int slot = 0; slot < 50 && candidates != 0; slot++)
                {
                    ulong bit = 1UL << slot; if ((candidates & bit) == 0) continue; candidates &= ~bit;
                    if (preferred != null && slot == preferred.Slot && ReferenceEquals(p.inventory[slot], preferred.Item)) continue;
                    found = true;
                    if (TryPlanWiringOption(target, p, slot, actual, out plan, out localWait, out localSlots))
                    { wait = BlueprintBuildWait.None; dependencySlots = 0; return true; }
                    wait |= localWait; dependencySlots |= localSlots;
                }
            }
            if (!found && wait == BlueprintBuildWait.None) wait = BlueprintBuildWait.Material;
            return false;
        }
        private bool TryPlanWiringOption(BlueprintBuildTarget target, Player p, int slot, BlueprintWorldMatchSample actual,
            out BuildOperationPlan plan, out BlueprintBuildWait wait, out ulong dependencySlots)
        {
            plan = null; wait = BlueprintBuildWait.None; dependencySlots = 0;
            var tool = p.inventory[slot];
            if (tool == null || tool.IsAir || !WiringToolMatches(target.Operation, tool.type))
            { wait = BlueprintBuildWait.Material; return false; }
            if (!buildTools.BuildInventoryEligible(slot, tool))
            { wait = BlueprintBuildWait.Dependencies; dependencySlots = 1UL << slot; return false; }
            var requested = target.Operation;
            var operation = new BlueprintBuildOperation { Kind = requested.Kind, Mode = requested.Mode,
                Consumer = tool.type == 3611 ? ToolBuildConsumer.GrandDesign : ToolBuildConsumer.Wiring,
                HeldMaximum = requested.Kind == BlueprintBuildOperationKind.AddActuator && tool.type == ItemID.Actuator ? 1 : 0,
                PrimaryMaximum = requested.Kind == BlueprintBuildOperationKind.AddWire ||
                    requested.Kind == BlueprintBuildOperationKind.AddActuator && tool.type == 3611 ? 1 : 0,
                SupplyType = requested.Kind == BlueprintBuildOperationKind.AddWire ? ItemID.Wire :
                    requested.Kind == BlueprintBuildOperationKind.AddActuator && tool.type == 3611 ? ItemID.Actuator : 0 };
            if (!WiringToolCurrent(target, p, tool, operation, actual, out wait, out dependencySlots)) return false;
            if (WiringToolPending(tool.type)) { wait = BlueprintBuildWait.Dynamic; return false; }
            int supply = -1;
            if (operation.SupplyType > 0 && !TryPlanBuildSupply(target, p, operation.SupplyType, -1, out supply, out wait, out dependencySlots)) return false;
            plan = new BuildOperationPlan { Operation = operation, Tool = tool, ToolType = tool.type, ToolSlot = slot,
                Supply = supply < 0 ? null : p.inventory[supply], SupplySlot = supply,
                SupplyType = operation.SupplyType, SupplyDestination = -1,
                HeldMaximum = operation.HeldMaximum, PrimaryMaximum = operation.PrimaryMaximum };
            return true;
        }
        private static int WiringToolType(BlueprintBuildOperation operation, int option)
        {
            switch (operation.Kind)
            {
                case BlueprintBuildOperationKind.AddWire: return option == 0 ? WireToolType(operation.Mode) : option == 1 ? 3625 : 3611;
                case BlueprintBuildOperationKind.AddActuator: return option == 0 ? ItemID.Actuator : option == 1 ? 3611 : 0;
                case BlueprintBuildOperationKind.RemoveOwnedActuator: return option == 0 ? 510 : option == 1 ? 3611 : 0;
                case BlueprintBuildOperationKind.SetInactive: return option == 0 ? 3620 : 0;
                default: return 0;
            }
        }
        private static bool WiringToolMatches(BlueprintBuildOperation operation, int type)
        {
            for (int option = 0; option < 3; option++) if (WiringToolType(operation, option) == type) return type > 0;
            return false;
        }
        private bool WiringToolPending(int type, BuildFlight ownFlight = null)
        {
            // Final validation of this installation may see its own active651.
            // That exact gesture can settle before cleanup; every other live
            // gesture and every uncorrelated reply remains a real dependency.
            return Main.netMode == 1 && (type == 3611 || type == 3625) && (unownedWiringReplies != 0 || wiringCorrelationLost) ||
                type == 3611 && grandBuild != null && (ownFlight == null || !ReferenceEquals(grandBuild.Flight, ownFlight)) ||
                (type == 3611 || type == 3625) && wiringReply != null && (ownFlight == null || !ReferenceEquals(wiringReply.Flight, ownFlight));
        }
        private void WaitForWiringOptions(BlueprintBuildTarget target, BlueprintBuildWait wait, ulong dependencySlots)
        {
            if ((wait & BlueprintBuildWait.Dynamic) != 0) WaitForDynamicBuild(target);
            wait &= ~BlueprintBuildWait.Dynamic;
            if ((wait & BlueprintBuildWait.Material) != 0) wait |= BlueprintBuildWait.Capability;
            if (wait != BlueprintBuildWait.None) WaitForBuild(target, wait, dependencySlots);
        }
        private static int WireToolType(int bit)
        { switch (bit) { case 1: return 509; case 2: return 850; case 4: return 851; case 8: return 3612; default: return 0; } }
        private static WiresUI.Settings.MultiToolMode NativeWireMode(int bit)
        {
            // Blueprint uses R,B,G,Y; the original enum uses R,G,B,Y.
            switch (bit)
            {
                case 1: return WiresUI.Settings.MultiToolMode.Red;
                case 2: return WiresUI.Settings.MultiToolMode.Blue;
                case 4: return WiresUI.Settings.MultiToolMode.Green;
                case 8: return WiresUI.Settings.MultiToolMode.Yellow;
                default: return 0;
            }
        }
        private static WiresUI.Settings.MultiToolMode WiringMode(BlueprintBuildOperation operation)
        {
            if (operation.Kind == BlueprintBuildOperationKind.AddWire) return NativeWireMode(operation.Mode);
            return operation.Kind == BlueprintBuildOperationKind.RemoveOwnedActuator ?
                WiresUI.Settings.MultiToolMode.Actuator | WiresUI.Settings.MultiToolMode.Cutter : WiresUI.Settings.MultiToolMode.Actuator;
        }
        private bool WiringBuildCurrent(BuildFlight flight)
        {
            var target = flight.Target; var operation = flight.Operation; var p = flight.Player; var tool = flight.Source.Item;
            BlueprintWorldMatchSample actual;
            if (!ReadBuildCell(target.WorldX, target.WorldY, out actual) || !p.CanDoWireStuffHere(target.WorldX, target.WorldY)) return false;
            BlueprintBuildWait wait; ulong dependencySlots;
            if (!WiringToolCurrent(target, p, tool, operation, actual, out wait, out dependencySlots, flight))
            {
                WaitForWiringOptions(target, wait, dependencySlots);
                return false;
            }
            if (operation.Kind == BlueprintBuildOperationKind.AddWire) return WiringPrimaryCurrent(flight, ItemID.Wire);
            if (operation.Kind == BlueprintBuildOperationKind.AddActuator) return tool.type == ItemID.Actuator || WiringPrimaryCurrent(flight, ItemID.Actuator);
            if (operation.Kind == BlueprintBuildOperationKind.SetInactive)
                flight.InputGesture = p.ActuationRodLock && p.ActuationRodLockSetting != actual.Cell.Inactive ? ToolInputGesture.NeutralRelease : ToolInputGesture.HeldPress;
            return true;
        }
        private bool WiringToolCurrent(BlueprintBuildTarget target, Player p, Item tool, BlueprintBuildOperation operation,
            BlueprintWorldMatchSample actual, out BlueprintBuildWait wait, out ulong dependencySlots, BuildFlight ownFlight = null)
        {
            wait = BlueprintBuildWait.None; dependencySlots = 0;
            if (!WiringToolInRange(target, p, tool)) { wait = BlueprintBuildWait.Player; return false; }
            switch (operation.Kind)
            {
                case BlueprintBuildOperationKind.AddWire:
                    return (target.Cell.Wires & operation.Mode) != 0 && (actual.Cell.Wires & operation.Mode) == 0 && WiringToolMatches(operation, tool.type);
                case BlueprintBuildOperationKind.AddActuator:
                    if (actual.Cell.Actuator || !WiringToolMatches(operation, tool.type)) return false;
                    if (operation.Mode != 1) return true;
                    if (wiringReply?.TemporaryActuator != null && (ownFlight == null || !ReferenceEquals(wiringReply.Flight, ownFlight)))
                    { wait = BlueprintBuildWait.Dynamic; return false; }
                    if (!WiringBodySame(target, actual) || actual.Cell.Inactive == target.Cell.Inactive || !CanSwitchBuildInactive(target)) return false;
                    // A temporary installation earns a cleanup obligation. Prove
                    // the existing rod and a real removal route at this target
                    // before creating it; future movement may still yield.
                    if (!WiringSupportToolReady(target, p, 3620, 0, out wait, out dependencySlots, ownFlight)) return false;
                    return WiringSupportToolReady(target, p, 510, 3611, out wait, out dependencySlots, ownFlight);
                case BlueprintBuildOperationKind.SetInactive:
                    return tool.type == 3620 && actual.Cell.Actuator && WiringBodySame(target, actual) &&
                        actual.Cell.Inactive != target.Cell.Inactive && CanSwitchBuildInactive(target);
                case BlueprintBuildOperationKind.RemoveOwnedActuator:
                    return WiringToolMatches(operation, tool.type) && actual.Cell.Actuator && WiringBodySame(target, actual) &&
                        actual.Cell.Inactive == target.Cell.Inactive && PeekOwnedActuator(target.WorldX, target.WorldY)?.Confirmed == true;
                default: return false;
            }
        }
        private static bool WiringToolInRange(BlueprintBuildTarget target, Player p, Item tool)
        {
            if (tool.type == 3611)
                return GrandBuildPointVisible(p, target.WorldX, target.WorldY);
            return p.IsInTileInteractionRange(target.WorldX, target.WorldY, TileReachCheckSettings.Simple, tool.tileBoost + p.blockRange);
        }
        private bool WiringSupportToolReady(BlueprintBuildTarget target, Player p, int firstType, int alternateType,
            out BlueprintBuildWait wait, out ulong dependencySlots, BuildFlight ownFlight)
        {
            wait = BlueprintBuildWait.None; dependencySlots = 0; bool found = false;
            for (int option = 0; option < 2; option++)
            {
                int type = option == 0 ? firstType : alternateType;
                if (type <= 0) continue;
                ConstructionItemSlot(p, type);
                ulong candidates;
                if (!constructionSlots.TryGetValue(type, out candidates)) continue;
                for (int slot = 0; slot < 50 && candidates != 0; slot++)
                {
                    ulong bit = 1UL << slot; if ((candidates & bit) == 0) continue; candidates &= ~bit;
                    var item = p.inventory[slot];
                    if (item == null || item.IsAir || item.type != type) continue;
                    found = true;
                    if (!buildTools.BuildInventoryEligible(slot, item))
                    { wait |= BlueprintBuildWait.Dependencies; dependencySlots |= bit; continue; }
                    if (!WiringToolInRange(target, p, item)) { wait |= BlueprintBuildWait.Player; continue; }
                    if (WiringToolPending(type, ownFlight)) { wait |= BlueprintBuildWait.Dynamic; continue; }
                    wait = BlueprintBuildWait.None; dependencySlots = 0; return true;
                }
            }
            if (!found) wait |= BlueprintBuildWait.Material;
            return false;
        }
        private static bool WiringPrimaryCurrent(BuildFlight flight, int type)
        {
            foreach (var dependency in flight.Dependencies)
                if (dependency.Primary) return dependency.Item.type == type && dependency.Item.stack > 0 &&
                    ReferenceEquals(FirstSpecialMaterial(flight.Player, type), dependency.Item);
            return false;
        }
        private bool CanSwitchBuildInactive(BlueprintBuildTarget target)
        {
            if (!target.Cell.Inactive) return true; // Original ReActive has no DeActive restrictions.
            var tile = Main.tile[target.WorldX, target.WorldY];
            if (tile == null || !tile.active() || !Main.tileSolid[tile.type] || TileID.Sets.NotReallySolid[tile.type] ||
                tile.type == 226 && target.WorldY > Main.worldSurface && !NPC.downedPlantBoss) return false;
            switch (tile.type) { case 314: case 379: case 386: case 387: case 388: case 389: case 476: return false; }
            BlueprintWorldMatchSample above;
            if (!ReadBuildCell(target.WorldX, target.WorldY - 1, out above)) return false;
            if ((above.Cell.Layers & CaptureLayer.Tile) == 0) return true;
            if (TileID.Sets.PreventsActuationUnder[above.Cell.TileType]) return false;
            // CanKillTile is a read-only original query. Prove the local cells
            // used by its ordinary above-object checks before entering it.
            for (int x = target.WorldX - 2; x <= target.WorldX + 2; x++)
                for (int y = target.WorldY - 1; y <= target.WorldY + 1; y++)
                { BlueprintWorldMatchSample sample; if (!ReadBuildCell(x, y, out sample)) return false; }
            return WorldGen.CanKillTile(target.WorldX, target.WorldY);
        }
        private bool NativeWiringConsumer(BuildFlight flight, ToolBuildConsumer consumer, Item material = null, bool wall = false, bool applyItemAnimation = true)
        {
            if (wiringReply?.Flight == flight || consumer != flight.Operation.Consumer || !WiringBuildCurrent(flight)) return false;
            if (consumer == ToolBuildConsumer.GrandDesign) return flight.Source.Type == 3611;
            return flight.InputGesture == ToolInputGesture.HeldPress && flight.Player.ItemTimeIsZero &&
                flight.Player.itemAnimation > 0 && flight.Player.controlUseItem;
        }
        private Action<Exception> BeginWiringBuildConsumer(BuildFlight flight, ToolBuildConsumer consumer)
        {
            if (consumer != ToolBuildConsumer.Wiring || flight == null || !WiringOperation(flight.Operation)) return null;
            var previous = wiringNativeFlight; var previousWires = wiringNativeWires; bool previousActuator = wiringNativeActuator;
            wiringNativeFlight = flight;
            var tile = Main.tile[flight.Target.WorldX, flight.Target.WorldY];
            wiringNativeWires = WiringBits(tile); wiringNativeActuator = tile.actuator();
            var originalMode = WiresUI.Settings.ToolMode;
            bool modeBorrowed = flight.Source.Type == 3625;
            var ownMode = WiringMode(flight.Operation);
            if (modeBorrowed) WiresUI.Settings.ToolMode = ownMode;
            bool ended = false;
            return error => {
                if (ended) return; ended = true;
                if (modeBorrowed && WiresUI.Settings.ToolMode == ownMode) WiresUI.Settings.ToolMode = originalMode;
                if ((wiringNativeWires & WiringBits(Main.tile[flight.Target.WorldX, flight.Target.WorldY])) != wiringNativeWires)
                { flight.Rejected = true; HoldUnconfirmedBuild(flight); }
                if (error != null && ReferenceEquals(activeBuild, flight)) HoldUnconfirmedBuild(flight);
                wiringNativeFlight = previous; wiringNativeWires = previousWires; wiringNativeActuator = previousActuator;
            };
        }
        private bool WiringBuildApplied(BuildFlight flight)
        {
            if (WiringBuildPending(flight)) return false;
            var tile = Main.tile[flight.Target.WorldX, flight.Target.WorldY]; if (tile == null) return false;
            switch (flight.Operation.Kind)
            {
                case BlueprintBuildOperationKind.AddWire: return (WiringBits(tile) & flight.Operation.Mode) != 0;
                case BlueprintBuildOperationKind.AddActuator:
                    var owned = CurrentOwnedActuator(flight.Target.WorldX, flight.Target.WorldY);
                    if (owned != null && owned.Token == flight.Token) owned.Confirmed = true;
                    return tile.actuator();
                case BlueprintBuildOperationKind.SetInactive: return tile.inActive() == flight.Target.Cell.Inactive;
                case BlueprintBuildOperationKind.RemoveOwnedActuator:
                    if (tile.actuator()) return false;
                    ownedBuildActuators.Remove(WiringCoordinate(flight.Target.WorldX, flight.Target.WorldY)); return true;
                default: return false;
            }
        }
        private static byte WiringBits(Tile tile)
        { return tile == null ? (byte)0 : (byte)((tile.wire() ? 1 : 0) | (tile.wire2() ? 2 : 0) | (tile.wire3() ? 4 : 0) | (tile.wire4() ? 8 : 0)); }
        private OwnedBuildActuator CurrentOwnedActuator(int x, int y)
        {
            var receipt = PeekOwnedActuator(x, y);
            if (receipt == null) ownedBuildActuators.Remove(WiringCoordinate(x, y));
            return receipt;
        }
        private OwnedBuildActuator PeekOwnedActuator(int x, int y)
        {
            OwnedBuildActuator receipt;
            return ownedBuildActuators.TryGetValue(WiringCoordinate(x, y), out receipt) && !receipt.Invalid && !receipt.Removed &&
                receipt.Session == generation && ReferenceEquals(Main.tile[x, y], receipt.Tile) && receipt.Tile.actuator() &&
                receipt.Tile.active() && receipt.Tile.type == receipt.Type ? receipt : null;
        }
        private static bool TemporaryGrandActuatorInstall(BuildFlight flight)
        {
            return flight?.Operation?.Consumer == ToolBuildConsumer.GrandDesign && flight.Source.Type == 3611 &&
                flight.Operation.Kind == BlueprintBuildOperationKind.AddActuator && flight.Operation.Mode == 1;
        }
        private PendingBuildActuator CapturePendingActuator(BuildFlight flight)
        {
            if (!TemporaryGrandActuatorInstall(flight)) return null;
            var tile = Main.tile[flight.Target.WorldX, flight.Target.WorldY];
            if (tile == null || !tile.active() || tile.actuator()) return null;
            return new PendingBuildActuator { X = flight.Target.WorldX, Y = flight.Target.WorldY, Tile = tile, Type = tile.type,
                Active = tile.active(), FrameX = tile.frameX, FrameY = tile.frameY, Token = flight.Token, Identity = flight.Target.Identity };
        }
        private bool PendingActuatorRequestCurrent(WiringReply reply)
        {
            var candidate = reply.TemporaryActuator;
            return reply.Session == generation && ReferenceEquals(reply.Connection, connection) && ReferenceEquals(connection, Netplay.Connection) &&
                candidate != null && reply.Flight.Token == candidate.Token && reply.Flight.Target.Identity.Equals(candidate.Identity);
        }
        private static bool PendingActuatorBodyCurrent(PendingBuildActuator candidate)
        {
            var tile = Main.tile[candidate.X, candidate.Y];
            return ReferenceEquals(tile, candidate.Tile) && tile != null && tile.type == candidate.Type && tile.active() == candidate.Active &&
                (!Main.tileFrameImportant[candidate.Type] || tile.frameX == candidate.FrameX && tile.frameY == candidate.FrameY);
        }
        private bool ConfirmPendingActuator(WiringReply reply)
        {
            var candidate = reply.TemporaryActuator;
            if (candidate == null || candidate.Invalid || candidate.Retired || !candidate.SawInstall || !candidate.BitObserved ||
                reply.Stage != 2 || reply.WireCount != 0 || reply.ActuatorCount != 1 || !PendingActuatorRequestCurrent(reply) ||
                !PendingActuatorBodyCurrent(candidate) || !candidate.Tile.actuator() || ownedBuildActuators.Count != 0) return false;
            bool live = false;
            foreach (var projection in projectionScene) if (projection.Identity.Equals(candidate.Identity)) { live = true; break; }
            if (!live) return false;
            ownedBuildActuators[WiringCoordinate(candidate.X, candidate.Y)] = new OwnedBuildActuator {
                X = candidate.X, Y = candidate.Y, Tile = candidate.Tile, Type = candidate.Type, Session = reply.Session,
                Token = candidate.Token, Identity = candidate.Identity, Confirmed = true };
            return true;
        }
        private struct ActuatorPlacementObservation
        {
            internal Tile Tile;
            internal bool Before;
            internal WiringReply Receive;
        }
        private ActuatorPlacementObservation BeginActuatorPlacement(int x, int y)
        {
            var tile = Main.tile[x, y];
            var observation = new ActuatorPlacementObservation { Tile = tile, Before = tile?.actuator() == true };
            var reply = wiringInstallReceive; var candidate = reply?.TemporaryActuator;
            if (candidate == null || x != candidate.X || y != candidate.Y) return observation;
            observation.Receive = reply;
            if (candidate.PlacementEntered || observation.Before || !PendingActuatorBodyCurrent(candidate)) candidate.Invalid = true;
            candidate.PlacementEntered = true;
            return observation;
        }
        private bool ExpectedPendingActuatorInstall(PendingBuildActuator candidate)
        {
            return !candidate.Invalid && !candidate.Retired && candidate.PlacementEntered && !candidate.SawInstall && !candidate.BitObserved &&
                ReferenceEquals(wiringInstallReceive, wiringReply) && ReferenceEquals(wiringInstallReceive?.TemporaryActuator, candidate);
        }
        private void ObserveActuatorPlacement(int x, int y, ActuatorPlacementObservation observation, bool changed)
        {
            var candidate = observation.Receive?.TemporaryActuator;
            if (candidate != null)
            {
                if (!changed || observation.Before || !ReferenceEquals(observation.Tile, candidate.Tile) || !candidate.BitObserved ||
                    !PendingActuatorBodyCurrent(candidate) || !candidate.Tile.actuator()) candidate.Invalid = true;
                else if (!candidate.Invalid) candidate.SawInstall = true;
                return;
            }
            if (!changed || observation.Before || wiringInstallReceive != null) return;
            var flight = wiringNativeFlight;
            if (flight == null || flight.Operation.Kind != BlueprintBuildOperationKind.AddActuator || flight.Operation.Mode != 1 ||
                flight.Target.WorldX != x || flight.Target.WorldY != y || !CanBuildAutomatically(flight.Player)) return;
            var tile = Main.tile[x, y];
            ownedBuildActuators[WiringCoordinate(x, y)] = new OwnedBuildActuator { X = x, Y = y, Tile = tile, Type = tile.type,
                Session = generation, Token = flight.Token, Identity = flight.Target.Identity };
        }
        private void ObserveActuatorBit(Tile tile, bool before, bool after)
        {
            if (before == after) return;
            var candidate = wiringReply?.TemporaryActuator;
            if (Main.netMode == 1 && candidate != null && ReferenceEquals(candidate.Tile, tile))
            {
                if (!before && after && ExpectedPendingActuatorInstall(candidate)) candidate.BitObserved = true;
                else candidate.Invalid = true;
            }
            if (ownedBuildActuators.Count == 0) return;
            long invalid = 0; bool remove = false;
            foreach (var receipt in ownedBuildActuators.Values)
            {
                if (!ReferenceEquals(receipt.Tile, tile)) continue;
                if (wiringNativeFlight?.Operation.Kind == BlueprintBuildOperationKind.RemoveOwnedActuator &&
                    receipt.X == wiringNativeFlight.Target.WorldX && receipt.Y == wiringNativeFlight.Target.WorldY) receipt.Removed = true;
                else { receipt.Invalid = true; invalid = WiringCoordinate(receipt.X, receipt.Y); remove = true; }
            }
            if (remove) ownedBuildActuators.Remove(invalid);
        }
        internal static void ObserveConstructionWorldStore(int x, int y, int kind, int oldValue, int newValue)
        { ConstructionNativeHooks.ObserveStore(x, y, kind, oldValue, newValue); }
        internal static void ObserveConstructionTileReplacement(int x, int y)
        { ConstructionNativeHooks.ObserveTileReplacement(x, y); }
        private void ObserveActuatorTileReplacement(int x, int y)
        {
            var candidate = wiringReply?.TemporaryActuator;
            if (Main.netMode == 1 && candidate != null && candidate.X == x && candidate.Y == y) candidate.Invalid = true;
            OwnedBuildActuator receipt;
            if (ownedBuildActuators.TryGetValue(WiringCoordinate(x, y), out receipt))
            { receipt.Invalid = true; ownedBuildActuators.Remove(WiringCoordinate(x, y)); }
        }
        private void ObserveOwnedActuatorStore(int x, int y, int kind, int oldValue, int newValue)
        {
            if (oldValue == newValue) return;
            int delta = oldValue ^ newValue;
            var candidate = wiringReply?.TemporaryActuator;
            if (Main.netMode == 1 && candidate != null && candidate.X == x && candidate.Y == y)
            {
                // Only the expected native17/8 false->true is exempt. Any
                // later remove/re-add or body ABA permanently loses this
                // candidate, even when the last observed values match again.
                if (kind == 3 && (delta & 0x800) != 0 &&
                    (!ExpectedPendingActuatorInstall(candidate) || (oldValue & 0x800) != 0 || (newValue & 0x800) == 0)) candidate.Invalid = true;
                if (kind == 0 || kind == 3 && (delta & 0x20) != 0 ||
                    (kind == 6 || kind == 7) && Main.tileFrameImportant[candidate.Type]) candidate.Invalid = true;
            }
            OwnedBuildActuator receipt;
            if (!ownedBuildActuators.TryGetValue(WiringCoordinate(x, y), out receipt)) return;
            // FieldKinds is locked to .8: 0=type, 3=sTileHeader,
            // 6/7=frameX/Y. Inactive is bit0x40; actuator is0x800 and
            // active is0x20. An owned rod's Inactive write retains provenance.
            if (kind == 3 && (delta & 0x800) != 0)
            {
                if (wiringNativeFlight?.Operation.Kind == BlueprintBuildOperationKind.RemoveOwnedActuator &&
                    wiringNativeFlight.Target.WorldX == x && wiringNativeFlight.Target.WorldY == y) receipt.Removed = true;
                else receipt.Invalid = true;
            }
            if (kind == 0 || kind == 3 && (delta & 0x20) != 0 ||
                (kind == 6 || kind == 7) && Main.tileFrameImportant[receipt.Type]) receipt.Invalid = true;
            if (receipt.Invalid) ownedBuildActuators.Remove(WiringCoordinate(x, y));
        }
        private void StopWiringBuild(bool sessionEnding)
        {
            if (grandBuild != null) grandBuild.CancelRequested = true;
            StopSpecialSelection(sessionEnding);
            if (sessionEnding)
            {
                foreach (var receipt in ownedBuildActuators.Values)
                    if (!receipt.Invalid && !receipt.Removed && receipt.Tile.actuator())
                    { projectionFeedback = "前置致动器尚未回收（" + receipt.X + "，" + receipt.Y + "）；已停止后续修改。"; break; }
                grandBuild = null; wiringReply = wiringInstallReceive = null; ownedBuildActuators.Clear();
                unownedWiringReplies = unownedWiringStage = 0; wiringCorrelationLost = false;
                constructionInventory = null; constructionPlayer = null; constructionSlots.Clear();
            }
        }
        private void CancelWiringBuildIdentity(BlueprintProjectionIdentity identity)
        {
            var candidate = wiringReply?.TemporaryActuator;
            if (candidate != null && candidate.Identity.Equals(identity)) candidate.Retired = true;
            var use = grandBuild;
            if (use?.Flight?.Target.Identity.Equals(identity) == true)
            {
                use.CancelRequested = true;
                // The exact projectile/key and cancellation/submission gate
                // suffice now. Do not pin a retired source/range for cleanup.
                use.Flight = null;
            }
            long key = 0; bool found = false;
            foreach (var receipt in ownedBuildActuators.Values)
            {
                if (!receipt.Identity.Equals(identity)) continue;
                receipt.Invalid = true; key = WiringCoordinate(receipt.X, receipt.Y); found = true;
                if (!receipt.Removed && receipt.Tile.actuator())
                    projectionFeedback = "前置致动器尚未回收（" + receipt.X + "，" + receipt.Y + "）；原施工目标已改变。";
                break;
            }
            if (found) ownedBuildActuators.Remove(key);
            // An already-sent109 still owns its finite material decoder
            // responsibility. Canceling a projection cannot cancel that packet.
        }
    }
}
