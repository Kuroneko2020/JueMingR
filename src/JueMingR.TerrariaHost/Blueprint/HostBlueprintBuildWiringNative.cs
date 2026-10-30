using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private sealed class GrandBuildUse
        {
            internal BuildFlight Flight;
            internal Projectile Projectile;
            internal int Key, X, Y;
            internal long Session, Token;
            internal bool OriginReady, CommitRequested, CancelRequested, Submitted;
        }
        private sealed class WiringReply
        {
            internal BuildFlight Flight;
            internal long Session;
            internal object Connection;
            internal int Stage, WireCount, ActuatorCount, DecodeType, DecodeCount;
            internal bool Decoding;
            internal PendingBuildActuator TemporaryActuator;
        }
        // This candidate belongs only to the one outstanding109. It is not a
        // cleanup right: ordered17/8 + both actually debited110s must earn that.
        private sealed class PendingBuildActuator
        {
            internal Tile Tile;
            internal int X, Y, Type;
            internal short FrameX, FrameY;
            internal long Token;
            internal BlueprintProjectionIdentity Identity;
            internal bool Active, Invalid, Retired, PlacementEntered, BitObserved, SawInstall;
        }
        private GrandBuildUse grandBuild, grandKill;
        private WiringReply wiringReply, wiringInstallReceive;
        private int unownedWiringReplies, unownedWiringStage;
        private bool wiringCorrelationLost;

        private static bool GrandBuildPointVisible(Player p, int x, int y)
        {
            Vector2 screen = Main.ReverseGravitySupport(new Vector2(x * 16 + 8, y * 16 + 8) - Main.screenPosition);
            return screen.X >= 0 && screen.Y >= 0 && screen.X < Main.screenWidth && screen.Y < Main.screenHeight;
        }
        private bool WiringBuildPending(BuildFlight flight)
        { return grandBuild?.Flight == flight && !grandBuild.Submitted || wiringReply?.Flight == flight; }
        private bool WiringBuildBlocksRetirement(BlueprintProjectionIdentity identity)
        {
            if (grandBuild?.Flight?.Target.Identity.Equals(identity) == true || wiringReply?.Flight?.Target.Identity.Equals(identity) == true) return true;
            var pending = wiringReply?.Flight?.Target;
            if (pending != null) foreach (var projection in projectionScene)
                if (projection.Identity.Equals(identity) && pending.WorldX >= projection.OriginX && pending.WorldY >= projection.OriginY &&
                    pending.WorldX < (long)projection.OriginX + projection.Summary.Width && pending.WorldY < (long)projection.OriginY + projection.Summary.Height) return true;
            foreach (var receipt in ownedBuildActuators.Values)
                if (!receipt.Invalid && !receipt.Removed && receipt.Identity.Equals(identity)) return true;
            return false;
        }
        private ToolReadiness WiringBuildReadiness(BuildFlight flight)
        {
            if (wiringReply?.Flight == flight) return ToolReadiness.Yield;
            if (flight?.Operation?.Consumer != ToolBuildConsumer.GrandDesign) return ToolReadiness.Ready;
            var use = grandBuild;
            if (use == null) { flight.InputGesture = ToolInputGesture.HeldPress; return ToolReadiness.Ready; }
            if (use.Flight != flight) return ToolReadiness.Yield;
            if (use.CancelRequested) { flight.InputGesture = ToolInputGesture.NeutralRelease; return ToolReadiness.Yield; }
            if (!WiringBuildCurrent(flight)) { use.CancelRequested = true; return ToolReadiness.Yield; }
            use.CommitRequested |= use.OriginReady;
            flight.InputGesture = use.CommitRequested ? ToolInputGesture.NeutralRelease : ToolInputGesture.HeldPress;
            return ToolReadiness.Ready;
        }
        private void EndWiringBuildFlight(BuildFlight flight, bool unknown)
        {
            EndSpecialSelection(flight);
            if (grandBuild?.Flight == flight && !grandBuild.Submitted) grandBuild.CancelRequested = true;
            if (unknown && wiringReply?.Flight == flight) FailWiringReply();
            else if (wiringReply?.Flight == flight) wiringReply.Flight = DetachedWiringFlight(flight);
        }
        private static BuildSource DetachedWiringSource(BuildSource source)
        { return new BuildSource { Slot = source.Slot, Type = source.Type, Stack = source.Stack, Maximum = source.Maximum,
            Primary = source.Primary, Cleanup = source.Cleanup, Item = source.Item }; }
        private static BuildFlight DetachedWiringFlight(BuildFlight flight)
        {
            var target = flight.Target;
            var dependencies = new BuildSource[flight.Dependencies.Length];
            for (int i = 0; i < dependencies.Length; i++) dependencies[i] = DetachedWiringSource(flight.Dependencies[i]);
            // No Range, Mask, Body, Object or owner/source-window reference can
            // survive projection deletion. The late decoder needs only this
            // scalar result and the finite actual inventory vectors.
            return new BuildFlight { Target = new BlueprintBuildTarget { Identity = target.Identity, Cell = target.Cell,
                Bounds = target.Bounds, TargetX = target.WorldX, TargetY = target.WorldY, Operation = target.Operation,
                Placement = target.Placement }, Player = flight.Player, Source = DetachedWiringSource(flight.Source),
                Dependencies = dependencies, DependencySlots = flight.DependencySlots, Token = flight.Token, Generation = flight.Generation,
                Entered = flight.Entered, AccessoryMaximum = flight.AccessoryMaximum };
        }
        private void BindGrandBuild(Player p, Projectile projectile)
        {
            var flight = activeBuild;
            if (grandBuild != null || projectile == null || projectile.type != 651 || projectile.owner != Main.myPlayer ||
                !ReferenceEquals(p, player) || !buildTools.Use.InNativeUse || flight?.Operation?.Consumer != ToolBuildConsumer.GrandDesign ||
                flight.Source.Type != 3611 || !ReferenceEquals(p.HeldItem, flight.Source.Item) || flight.Token != buildTools.Use.Operation ||
                !BuildFlightCurrent(flight, true)) return;
            grandBuild = new GrandBuildUse { Flight = flight, Projectile = projectile, Key = (int)projectile.key,
                X = flight.Target.WorldX, Y = flight.Target.WorldY, Session = generation, Token = flight.Token };
        }
        private bool OwnGrand(GrandBuildUse use, Projectile projectile)
        { return use != null && use.Session == generation && ReferenceEquals(use.Projectile, projectile) && projectile.type == 651 &&
            projectile.owner == Main.myPlayer && (int)projectile.key == use.Key; }
        private Action<Exception> BeginGrandAI(Projectile projectile)
        {
            var use = grandBuild;
            if (!OwnGrand(use, projectile)) return null;
            var p = Main.player[projectile.owner];
            bool permitted = !use.CancelRequested && use.Flight != null && ReferenceEquals(activeBuild, use.Flight) &&
                CanBuildAutomatically(p) && buildTools.Use.Operation == use.Token && WiringBuildCurrent(use.Flight);
            if (!permitted) use.CancelRequested = true;
            int oldX = Main.mouseX, oldY = Main.mouseY;
            bool oldRight = Main.mouseRight, ownRight = oldRight;
            bool oldInterface = p.mouseInterface, oldBlock = Main.blockMouse;
            Vector2 screen = Main.ReverseGravitySupport(new Vector2(use.X * 16 + 8, use.Y * 16 + 8) - Main.screenPosition);
            int ownX = (int)screen.X, ownY = (int)screen.Y;
            if (permitted) { Main.mouseX = ownX; Main.mouseY = ownY; }
            // Never set mouseRightRelease. A neutral AI opportunity lets the
            // real input pipeline rearm it; the next owned AI can then cancel.
            bool cancelPress = use.CancelRequested && !oldRight && Main.mouseRightRelease;
            if (cancelPress) Main.mouseRight = ownRight = true;
            bool ended = false;
            return error => {
                if (ended) return; ended = true;
                if (permitted && Main.mouseX == ownX) Main.mouseX = oldX;
                if (permitted && Main.mouseY == ownY) Main.mouseY = oldY;
                if (cancelPress && Main.mouseRight == ownRight) Main.mouseRight = oldRight;
                if (cancelPress && p.mouseInterface) p.mouseInterface = oldInterface;
                if (cancelPress && Main.blockMouse) Main.blockMouse = oldBlock;
                if (error != null) { use.CancelRequested = true; if (use.Flight != null) HoldUnconfirmedBuild(use.Flight); }
                if (projectile.active && !use.CancelRequested && projectile.localAI[0] == 0f &&
                    projectile.ai[0] == use.X && projectile.ai[1] == use.Y && projectile.Center.ToTileCoordinates() == new Point(use.X, use.Y)) use.OriginReady = true;
            };
        }
        private Action<Exception> BeginGrandKill(Projectile projectile)
        {
            var use = grandBuild;
            if (!OwnGrand(use, projectile)) return null;
            var oldKill = grandKill; grandKill = use;
            bool valid = !use.CancelRequested && use.CommitRequested && use.OriginReady && use.Flight != null &&
                ReferenceEquals(activeBuild, use.Flight) && use.Flight.Token == buildTools.Use.Operation && CanBuildAutomatically(use.Flight.Player) &&
                WiringBuildCurrent(use.Flight) && projectile.localAI[0] == 1f && projectile.ai[0] == use.X && projectile.ai[1] == use.Y &&
                projectile.Center.ToTileCoordinates() == new Point(use.X, use.Y);
            if (!valid) use.CancelRequested = true;
            var oldMode = WiresUI.Settings.ToolMode; var ownMode = use.Flight == null ? oldMode : WiringMode(use.Flight.Operation);
            var oldFlight = wiringNativeFlight;
            if (valid)
            {
                WiresUI.Settings.ToolMode = ownMode; wiringNativeFlight = use.Flight;
                BeginAutomaticBuildCheck(use.Flight); use.Flight.Entered = true;
            }
            bool ended = false;
            return error => {
                if (ended) return; ended = true;
                if (valid && WiresUI.Settings.ToolMode == ownMode) WiresUI.Settings.ToolMode = oldMode;
                wiringNativeFlight = oldFlight; grandKill = oldKill;
                if (ReferenceEquals(grandBuild, use)) grandBuild = null;
                if (use.Flight == null) return;
                if (error != null) { HoldUnconfirmedBuild(use.Flight); return; }
                if (valid && use.Submitted && wiringReply == null) FinishDetachedWiring(use.Flight);
                else if (!use.Submitted)
                { use.Flight.Rejected = true; NotifyBuildAttempt(use.Flight.Target); }
            };
        }
        private bool AllowNativeMassWire(Point start, Point end, Player p)
        {
            var use = grandKill;
            if (use == null) return true;
            if (use.CancelRequested || !use.CommitRequested || use.Flight == null || !ReferenceEquals(p, use.Flight.Player) ||
                start != new Point(use.X, use.Y) || end != start || WiresUI.Settings.ToolMode != WiringMode(use.Flight.Operation)) return false;
            use.Submitted = true; return true;
        }
        private bool ObserveWiringSend(int message, int x, float y, float endX, float endY, int mode)
        {
            if (message != 109 || Main.netMode != 1) return true;
            var flight = wiringNativeFlight;
            if (grandKill != null)
            {
                var use = grandKill;
                if (use.CancelRequested || !use.CommitRequested || use.Flight == null || x != use.X || y != use.Y || endX != x || endY != y ||
                    mode != (int)WiringMode(use.Flight.Operation)) return false;
                flight = use.Flight; use.Submitted = true;
            }
            bool owned = flight != null && flight.Token > 0 && x == flight.Target.WorldX && y == flight.Target.WorldY && endX == x && endY == y &&
                mode == (int)WiringMode(flight.Operation);
            if (!owned)
            {
                if (wiringReply != null) { FailWiringReply(); wiringCorrelationLost = true; }
                if (unownedWiringReplies < int.MaxValue) unownedWiringReplies++;
                return true;
            }
            if (wiringReply != null || unownedWiringReplies != 0 || wiringCorrelationLost)
            { HoldUnconfirmedBuild(flight); return false; }
            wiringReply = new WiringReply { Flight = flight, Session = generation, Connection = connection,
                TemporaryActuator = CapturePendingActuator(flight) };
            return true;
        }
        private Action<Exception> BeginWiringReceive(MessageBuffer buffer, int start, int length)
        {
            if (Main.netMode != 1 || buffer == null || buffer.whoAmI != 256 || length < 1 || start < 0 ||
                start >= buffer.readBuffer.Length || length > buffer.readBuffer.Length - start) return null;
            if (buffer.readBuffer[start] == 17) return BeginWiringActuatorReceive(buffer, start, length);
            if (buffer.readBuffer[start] != 110 || length < 6) return null;
            int type = (short)(buffer.readBuffer[start + 1] | buffer.readBuffer[start + 2] << 8);
            int count = (short)(buffer.readBuffer[start + 3] | buffer.readBuffer[start + 4] << 8);
            int owner = buffer.readBuffer[start + 5];
            if (owner != Main.myPlayer) return null;
            var reply = wiringReply;
            if (reply == null)
            {
                if (unownedWiringReplies > 0 && type == (unownedWiringStage == 0 ? 530 : 849))
                { if (++unownedWiringStage == 2) { unownedWiringStage = 0; unownedWiringReplies--; } }
                return null;
            }
            var flight = reply.Flight;
            int maximum = flight.Operation.Kind == BlueprintBuildOperationKind.AddWire ? (type == 530 ? 1 : 0) :
                flight.Operation.Kind == BlueprintBuildOperationKind.AddActuator ? (type == 849 ? 1 : 0) : 0;
            if (reply.Session != generation || !ReferenceEquals(reply.Connection, connection) || type != (reply.Stage == 0 ? 530 : 849) || count < 0 || count > maximum ||
                !WiringPendingSourcesCurrent(reply))
            { FailWiringReply(); return null; }
            if (count > 0)
            {
                bool found = false;
                foreach (var source in flight.Dependencies)
                    if (source.Primary && source.Type == type && !source.Item.IsAir && source.Item.stack >= count &&
                        ReferenceEquals(FirstSpecialMaterial(flight.Player, type), source.Item)) found = true;
                if (!found) { FailWiringReply(); return null; }
            }
            reply.Decoding = true; reply.DecodeType = type; reply.DecodeCount = count; bool ended = false;
            return error => {
                if (ended) return; ended = true; reply.Decoding = false;
                if (!ReferenceEquals(wiringReply, reply)) return;
                if (error != null || !BuildConsumptionValid(flight) || !WiringPendingSourcesCurrent(reply, type, count)) { FailWiringReply(); return; }
                int spent = 0;
                foreach (var source in flight.Dependencies) if (source.Type == type) spent += source.Stack - (source.Item.IsAir ? 0 : source.Item.stack);
                if (spent != count) { FailWiringReply(); return; }
                if (type == 530) reply.WireCount = count; else reply.ActuatorCount = count;
                reply.Stage++;
                if (reply.Stage == 2) { wiringReply = null; FinishDetachedWiring(flight, reply.WireCount + reply.ActuatorCount, reply); }
            };
        }
        private Action<Exception> BeginWiringActuatorReceive(MessageBuffer buffer, int start, int length)
        {
            var reply = wiringReply; var candidate = reply?.TemporaryActuator;
            if (candidate == null || length < 9) return null;
            int action = buffer.readBuffer[start + 1];
            int x = (short)(buffer.readBuffer[start + 2] | buffer.readBuffer[start + 3] << 8);
            int y = (short)(buffer.readBuffer[start + 4] | buffer.readBuffer[start + 5] << 8);
            if ((action != 8 && action != 9) || x != candidate.X || y != candidate.Y) return null;
            //17 has no owner/nonce. Observe only this exact native write; its
            // provenance still depends on the later finite, ordered110 pair.
            if (action != 8 || reply.Stage != 0 || candidate.SawInstall || candidate.PlacementEntered ||
                !PendingActuatorRequestCurrent(reply) || !PendingActuatorBodyCurrent(candidate) || candidate.Tile.actuator())
            { candidate.Invalid = true; return null; }
            if (candidate.Invalid || candidate.Retired) return null;
            var previous = wiringInstallReceive;
            if (previous != null) { candidate.Invalid = true; return null; }
            wiringInstallReceive = reply; bool ended = false;
            return error => {
                if (ended) return; ended = true;
                if (ReferenceEquals(wiringInstallReceive, reply)) wiringInstallReceive = previous;
                if (error != null || !candidate.SawInstall || !PendingActuatorBodyCurrent(candidate) || !candidate.Tile.actuator()) candidate.Invalid = true;
            };
        }
        private bool WiringPendingSourcesCurrent(WiringReply reply, int decodingType = 0, int decodingCount = 0)
        {
            foreach (var source in reply.Flight.Dependencies)
            {
                var current = reply.Flight.Player.inventory[source.Slot];
                // Stack remains the submitted native baseline. Only this
                // pair's already-confirmed110s, plus the exact decoder now
                // returning, may explain a change. Re-basing from current
                // inventory would hide same-Item loss (including death's
                // TurnToAir) before/between two otherwise accurate zeros.
                int confirmed = source.Type == 530 ? reply.WireCount : source.Type == 849 ? reply.ActuatorCount : 0;
                int spent = source.Primary ? confirmed : 0;
                if (source.Primary && source.Type == decodingType) spent += decodingCount;
                if (!ReferenceEquals(current, source.Item) || !current.IsAir && current.type != source.Type || current.stack < 0 ||
                    source.Stack - spent < 0 || (current.IsAir ? 0 : current.stack) != source.Stack - spent) return false;
            }
            return true;
        }
        internal bool ProtectWiringPending(Item item)
        {
            var reply = wiringReply;
            if (reply == null || reply.Session != generation || item == null) return false;
            foreach (var source in reply.Flight.Dependencies) if (ReferenceEquals(source.Item, item)) return true;
            return false;
        }
        internal bool WiringPendingMaterial(Item item) { return ProtectWiringPending(item); }
        internal bool AllowsWiringPending(Item[] array, int slot)
        {
            var reply = wiringReply;
            if (reply == null || !reply.Decoding || reply.DecodeCount <= 0 || !ReferenceEquals(array, reply.Flight.Player.inventory)) return false;
            foreach (var source in reply.Flight.Dependencies)
                if (source.Primary && source.Type == reply.DecodeType && source.Slot == slot && ReferenceEquals(source.Item, array[slot])) return true;
            return false;
        }
        internal bool AllowsWiringReplyMaterial(Item[] array, int slot)
        {
            if (AllowsWiringPending(array, slot)) return true;
            var use = grandKill;
            var flight = use?.Flight;
            // This original Kill runs after ItemCheck, outside InNativeUse.
            // Its earned single-point scope may read only its own primary,
            // while the exact use token still owns it. Do not re-run the
            // missing-world-bit check after MassWire has written that bit.
            if (flight == null || use.CancelRequested || !use.CommitRequested || !use.OriginReady || !use.Submitted ||
                use.Session != generation || !ReferenceEquals(wiringNativeFlight, flight) || !ReferenceEquals(activeBuild, flight) ||
                !ReferenceEquals(flight.Player, player) || !ReferenceEquals(array, flight.Player.inventory) || slot < 0 || slot >= array.Length ||
                flight.Token != use.Token || buildTools.Use.Operation != use.Token ||
                !buildTools.Items.Ownership.OwnsUse(buildTools.Runtime.Generation, use.Token) ||
                WiresUI.Settings.ToolMode != WiringMode(flight.Operation)) return false;
            foreach (var source in flight.Dependencies)
                if (source.Primary && source.Maximum > 0 && source.Slot == slot && ReferenceEquals(source.Item, array[slot]) &&
                    !array[slot].IsAir && array[slot].type == source.Type && source.Stack - array[slot].stack >= 0 &&
                    source.Stack - array[slot].stack < source.Maximum) return true;
            return false;
        }
        private void FailWiringReply()
        {
            var reply = wiringReply; if (reply == null) return;
            // 110 has no action id. Losing either member cannot establish that
            // the old tail drained, so no later automatic109 in this session
            // may borrow its type/order as a new correlation. Manual native
            // traffic and independent direct-color/body consumers still run.
            wiringCorrelationLost = true;
            wiringReply = null; HoldUnconfirmedBuild(reply.Flight);
        }
        private void FinishDetachedWiring(BuildFlight flight, int actualSpent = -1, WiringReply reply = null)
        {
            if (!BuildConsumptionValid(flight)) { HoldUnconfirmedBuild(flight); return; }
            if (reply != null && TemporaryGrandActuatorInstall(flight))
            {
                // The original decoder has already proved the exact debit.
                // Zero means this request installed nothing, even when another
                // actuator is now visible. Missing individual provenance must
                // not reclassify known consumption as an unknown material loss.
                if (reply.ActuatorCount == 1 && !ConfirmPendingActuator(reply))
                    projectionFeedback = "前置致动器来源未确认（" + flight.Target.WorldX + "，" + flight.Target.WorldY + "）；已停止自动回收。";
                bool installed = WiringBuildApplied(flight);
                flight.Checked = true;
                if (flight.Target.Range != null)
                { if (installed) RememberBuildFrontier(flight.Target); else RequestBuildEligibilityRetry(flight.Target); }
                NotifyBuildAttempt(flight.Target);
                return;
            }
            bool applied = WiringBuildApplied(flight);
            bool spent = actualSpent > 0;
            foreach (var source in flight.Dependencies)
            { var current = flight.Player.inventory[source.Slot]; spent |= (current.IsAir ? 0 : current.stack) < source.Stack; }
            bool needsMaterial = flight.Operation.Kind == BlueprintBuildOperationKind.AddWire || flight.Operation.Kind == BlueprintBuildOperationKind.AddActuator;
            // The correlated native decoder has proved both ordered110s and
            // their actual debit. Zero can mean another writer won the target
            // before this109, so its visible result needs no debit from us.
            // This receipt releases only consumption duty, never temporary
            // actuator ownership or success for a still-missing world bit.
            bool confirmedZero = reply != null && ReferenceEquals(reply.Flight, flight) && reply.Stage == 2 &&
                reply.WireCount == 0 && reply.ActuatorCount == 0 && actualSpent == 0;
            if (!applied && spent || applied && needsMaterial && !spent && !confirmedZero) { HoldUnconfirmedBuild(flight); return; }
            flight.Checked = true;
            if (flight.Target.Range != null)
            { if (applied) RememberBuildFrontier(flight.Target); else RequestBuildEligibilityRetry(flight.Target); }
            NotifyBuildAttempt(flight.Target);
        }

        // These hooks borrow only the exact original consumer's short scope.
        // A canceled owned 651 still executes its normal Kill cleanup; only
        // its now-unauthorized MassWire/109 submission is denied.
        private static class ConstructionNativeHooks
        {
            private static readonly Harmony harmony = new Harmony("JueMingR.Blueprint.Construction");
            private static HostBlueprint owner;
            internal static void ObserveStore(int x, int y, int kind, int oldValue, int newValue)
            { owner?.ObserveOwnedActuatorStore(x, y, kind, oldValue, newValue); }
            internal static void ObserveTileReplacement(int x, int y) { owner?.ObserveActuatorTileReplacement(x, y); }
            internal static void Install(HostBlueprint value)
            {
                if (ReferenceEquals(owner, value)) return;
                harmony.UnpatchAll(harmony.Id); owner = value;
                Patch(typeof(Player), "TryUpdateChannel", new[] { typeof(Projectile) }, null, nameof(Created));
                Patch(typeof(Player), "UpdateControlHolds", Type.EmptyTypes, nameof(ControlBefore), nameof(ScopeAfter), nameof(ScopeFinal));
                Patch(typeof(Player), "Update", new[] { typeof(int) }, null, nameof(PlayerAfter));
                Patch(typeof(Player), "ItemCheck_CheckCanUse_Inner", new[] { typeof(Item), typeof(bool) }, nameof(CanUseBefore), nameof(ScopeAfter), nameof(ScopeFinal));
                Patch(typeof(Projectile), "AI", Type.EmptyTypes, nameof(AiBefore), nameof(ScopeAfter), nameof(ScopeFinal));
                Patch(typeof(Projectile), "Kill", Type.EmptyTypes, nameof(KillBefore), nameof(ScopeAfter), nameof(ScopeFinal));
                Patch(typeof(Wiring), "MassWireOperation", new[] { typeof(Point), typeof(Point), typeof(Player) }, nameof(MassBefore));
                Patch(typeof(WorldGen), "PlaceActuator", new[] { typeof(int), typeof(int) }, nameof(ActuatorBefore), nameof(ActuatorAfter));
                Patch(typeof(Tile), "actuator", new[] { typeof(bool) }, nameof(BitBefore), nameof(BitAfter));
                Patch(typeof(MessageBuffer), "GetData", new[] { typeof(int), typeof(int), typeof(int).MakeByRefType() }, nameof(ReceiveBefore), nameof(ScopeAfter), nameof(ScopeFinal));
                var send = typeof(NetMessage).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Single(m => m.Name == "SendData" && m.GetParameters().Length == 11);
                harmony.Patch(send, new HarmonyMethod(typeof(ConstructionNativeHooks), nameof(SendBefore)));
            }
            private static void Patch(Type type, string method, Type[] args, string before = null, string after = null, string final = null)
            {
                var native = type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance, null, args, null);
                if (native?.GetMethodBody() == null) throw new MissingMethodException("blueprint-construction-abi:" + method);
                harmony.Patch(native, Hook(before), Hook(after), null, Hook(final));
            }
            private static HarmonyMethod Hook(string name)
            { return name == null ? null : new HarmonyMethod(typeof(ConstructionNativeHooks).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)) { priority = Priority.First }; }
            private static void Created(Player __instance, Projectile __0) { owner?.BindGrandBuild(__instance, __0); }
            private static void ControlBefore(Player __instance, out Action<Exception> __state) { __state = owner?.BeginSpecialControlHolds(__instance); }
            private static void PlayerAfter(Player __instance) { owner?.EndSpecialControlEpoch(__instance); }
            private static void AiBefore(Projectile __instance, out Action<Exception> __state) { __state = owner?.BeginGrandAI(__instance); }
            private static void KillBefore(Projectile __instance, out Action<Exception> __state) { __state = owner?.BeginGrandKill(__instance); }
            private static void ScopeAfter(Action<Exception> __state) { __state?.Invoke(null); }
            private static Exception ScopeFinal(Action<Exception> __state, Exception __exception) { __state?.Invoke(__exception); return __exception; }
            private static bool MassBefore(Point __0, Point __1, Player __2) { return owner?.AllowNativeMassWire(__0, __1, __2) ?? true; }
            private static bool SendBefore(int __0, int __4, float __5, float __6, float __7, int __8)
            { return owner?.ObserveWiringSend(__0, __4, __5, __6, __7, __8) ?? true; }
            private static void ActuatorBefore(int __0, int __1, out ActuatorPlacementObservation __state)
            { __state = owner == null ? default(ActuatorPlacementObservation) : owner.BeginActuatorPlacement(__0, __1); }
            private static void ActuatorAfter(int __0, int __1, ActuatorPlacementObservation __state, bool __result)
            { owner?.ObserveActuatorPlacement(__0, __1, __state, __result); }
            private static void BitBefore(Tile __instance, out bool __state) { __state = __instance.actuator(); }
            private static void BitAfter(Tile __instance, bool __state) { owner?.ObserveActuatorBit(__instance, __state, __instance.actuator()); }
            private static void ReceiveBefore(MessageBuffer __instance, int __0, int __1, out Action<Exception> __state) { __state = owner?.BeginWiringReceive(__instance, __0, __1); }
            private static void CanUseBefore(Player __instance, Item __0, out Action<Exception> __state)
            {
                __state = null; var flight = owner?.activeBuild;
                if (flight?.Operation?.Consumer != ToolBuildConsumer.GrandDesign || __0.type != 3611 || !ReferenceEquals(__instance, flight.Player) ||
                    !ReferenceEquals(__0, flight.Source.Item) || !owner.BuildFlightCurrent(flight, true)) return;
                var oldMode = WiresUI.Settings.ToolMode; var ownMode = WiringMode(flight.Operation); WiresUI.Settings.ToolMode = ownMode; bool ended = false;
                __state = error => { if (ended) return; ended = true; if (WiresUI.Settings.ToolMode == ownMode) WiresUI.Settings.ToolMode = oldMode; };
            }
        }
    }
}
