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
            var owned = CurrentOwnedActuator(target.WorldX, target.WorldY);
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
                if (!permanent && ownedBuildActuators.Count != 0 && owned == null) return false;
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
        private bool TryPrepareWiringBuild(BlueprintBuildTarget target, Player p, out BuildFlight flight)
        {
            flight = null; var operation = target.Operation; if (operation == null) return false;
            int tool = -1, primary = -1;
            switch (operation.Kind)
            {
                case BlueprintBuildOperationKind.AddWire:
                    tool = ConstructionItemSlot(p, WireToolType(operation.Mode));
                    if (tool < 0) tool = ConstructionItemSlot(p, 3625);
                    if (tool < 0) tool = ConstructionItemSlot(p, 3611);
                    if (tool < 0 || !TryPrepareBuildSupply(target, p, ItemID.Wire, -1, out primary)) break;
                    operation.HeldMaximum = 0; operation.PrimaryMaximum = 1;
                    break;
                case BlueprintBuildOperationKind.AddActuator:
                    tool = ConstructionItemSlot(p, ItemID.Actuator);
                    if (tool >= 0) { operation.HeldMaximum = 1; operation.PrimaryMaximum = 0; operation.SupplyType = 0; }
                    else
                    {
                        tool = ConstructionItemSlot(p, 3611);
                        if (tool >= 0 && TryPrepareBuildSupply(target, p, ItemID.Actuator, -1, out primary))
                        { operation.HeldMaximum = 0; operation.PrimaryMaximum = 1; operation.SupplyType = ItemID.Actuator; }
                        else tool = -1;
                    }
                    break;
                case BlueprintBuildOperationKind.SetInactive: tool = ConstructionItemSlot(p, 3620); break;
                case BlueprintBuildOperationKind.RemoveOwnedActuator:
                    tool = ConstructionItemSlot(p, 510); if (tool < 0) tool = ConstructionItemSlot(p, 3611); break;
            }
            if (tool < 0) { WaitForBuild(target, BlueprintBuildWait.Material); return false; }
            if (operation.Kind == BlueprintBuildOperationKind.AddWire && primary < 0) return false;
            bool grand = p.inventory[tool].type == 3611;
            if (Main.netMode == 1 && (grand || p.inventory[tool].type == 3625) && (unownedWiringReplies != 0 || wiringCorrelationLost))
            { WaitForDynamicBuild(target); return false; }
            operation.Consumer = grand ? ToolBuildConsumer.GrandDesign : ToolBuildConsumer.Wiring;
            if (grand && (grandBuild != null || wiringReply != null)) { WaitForDynamicBuild(target); return false; }
            if (p.inventory[tool].type == 3625 && wiringReply != null) { WaitForDynamicBuild(target); return false; }
            return TryCreateOperationFlight(target, p, tool, primary, operation.HeldMaximum, operation.PrimaryMaximum, out flight);
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
            if (tool.type == 3611)
            {
                if (!GrandBuildPointVisible(p, target.WorldX, target.WorldY)) return false;
            }
            else if (!p.IsInTileInteractionRange(target.WorldX, target.WorldY, TileReachCheckSettings.Simple, tool.tileBoost + p.blockRange))
            { WaitForBuild(target, BlueprintBuildWait.Player); return false; }
            switch (operation.Kind)
            {
                case BlueprintBuildOperationKind.AddWire:
                    if ((target.Cell.Wires & operation.Mode) == 0 || (actual.Cell.Wires & operation.Mode) != 0 ||
                        tool.type != WireToolType(operation.Mode) && tool.type != 3625 && tool.type != 3611) return false;
                    return WiringPrimaryCurrent(flight, ItemID.Wire);
                case BlueprintBuildOperationKind.AddActuator:
                    if (actual.Cell.Actuator || tool.type != ItemID.Actuator && tool.type != 3611) return false;
                    if (operation.Mode == 1 && (!WiringBodySame(target, actual) || actual.Cell.Inactive == target.Cell.Inactive ||
                        ConstructionItemSlot(p, 3620) < 0 || ConstructionItemSlot(p, 510) < 0 && ConstructionItemSlot(p, 3611) < 0)) return false;
                    return tool.type == ItemID.Actuator || WiringPrimaryCurrent(flight, ItemID.Actuator);
                case BlueprintBuildOperationKind.SetInactive:
                    if (tool.type != 3620 || !actual.Cell.Actuator || !WiringBodySame(target, actual) || actual.Cell.Inactive == target.Cell.Inactive || !CanSwitchBuildInactive(target)) return false;
                    flight.InputGesture = p.ActuationRodLock && p.ActuationRodLockSetting != actual.Cell.Inactive ? ToolInputGesture.NeutralRelease : ToolInputGesture.HeldPress;
                    return true;
                case BlueprintBuildOperationKind.RemoveOwnedActuator:
                    return (tool.type == 510 || tool.type == 3611) && actual.Cell.Actuator && WiringBodySame(target, actual) &&
                        actual.Cell.Inactive == target.Cell.Inactive && CurrentOwnedActuator(target.WorldX, target.WorldY)?.Confirmed == true;
                default: return false;
            }
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
            OwnedBuildActuator receipt;
            if (!ownedBuildActuators.TryGetValue(WiringCoordinate(x, y), out receipt)) return null;
            if (receipt.Invalid || receipt.Removed || receipt.Session != generation || !ReferenceEquals(Main.tile[x, y], receipt.Tile) ||
                !receipt.Tile.actuator() || !receipt.Tile.active() || receipt.Tile.type != receipt.Type)
            { ownedBuildActuators.Remove(WiringCoordinate(x, y)); return null; }
            return receipt;
        }
        private void ObserveActuatorPlacement(int x, int y, bool before, bool changed)
        {
            if (!changed || before) return;
            var flight = wiringNativeFlight;
            if (flight == null || flight.Operation.Kind != BlueprintBuildOperationKind.AddActuator || flight.Operation.Mode != 1 ||
                flight.Target.WorldX != x || flight.Target.WorldY != y || !CanBuildAutomatically(flight.Player)) return;
            var tile = Main.tile[x, y];
            ownedBuildActuators[WiringCoordinate(x, y)] = new OwnedBuildActuator { X = x, Y = y, Tile = tile, Type = tile.type,
                Session = generation, Token = flight.Token, Identity = flight.Target.Identity };
        }
        private void ObserveActuatorBit(Tile tile, bool before, bool after)
        {
            if (before == after || ownedBuildActuators.Count == 0) return;
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
        private void ObserveOwnedActuatorStore(int x, int y, int kind, int oldValue, int newValue)
        {
            OwnedBuildActuator receipt;
            if (oldValue == newValue || !ownedBuildActuators.TryGetValue(WiringCoordinate(x, y), out receipt)) return;
            int delta = oldValue ^ newValue;
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
                grandBuild = null; wiringReply = null; ownedBuildActuators.Clear();
                unownedWiringReplies = unownedWiringStage = 0; wiringCorrelationLost = false;
                constructionInventory = null; constructionPlayer = null; constructionSlots.Clear();
            }
        }
        private void CancelWiringBuildIdentity(BlueprintProjectionIdentity identity)
        {
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
