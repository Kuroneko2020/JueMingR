using System;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Movement;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.NetModules;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private enum DoorContactResult { Approaching, Blocked, Ready }
        private static readonly Action<Player, int, int> launchCart = AccessTools.MethodDelegate<Action<Player, int, int>>(
            AccessTools.Method(typeof(Player), "LaunchMinecartHook"));

        private void RefreshMobilitySources(Actor actor)
        {
            var state = actor.Mobility;
            var p = actor.Body;
            // Item transactions invalidate planning immediately; native pickup
            // is discovered on a low-frequency refresh. Start always rechecks
            // the exact array, source reference, prefix and live item gate.
            if (Main.GameUpdateCount < state.NextSourceScan && state.SourceGestureRevision == ItemGestureRevision &&
                state.SourceChaos == p.chaosState && !actor.TileReachPending) return;
            state.NextSourceScan = Main.GameUpdateCount + 8;
            state.SourceGestureRevision = ItemGestureRevision;
            state.SourceChaos = p.chaosState;
            var hook = p.QuickGrapple_GetItemToUse();
            var mount = p.QuickMount_GetItemToUse();
            state.Observed.HookType = hook != null && !hook.IsAir ? hook.shoot : 0;
            state.Observed.HookRange = SmartFallGrapple.Range(state.Observed.HookType);
            state.Observed.DefaultMountType = ValidMountItem(mount) ? mount.mountType : -1;
            state.Observed.DefaultMountSlot = -1;
            state.Observed.FlightMountType = state.Observed.FlightMountSlot = -1;
            state.Observed.WaterMountType = state.Observed.WaterMountSlot = -1;
            state.Observed.NarrowMountType = state.Observed.NarrowMountSlot = -1;
            state.Observed.NarrowMountWidth = state.Observed.NarrowMountHeight = 0;
            state.Observed.FlightMountBudget = state.Observed.WaterMountSpeed = 0f;
            if (ValidMountItem(mount) && ReferenceEquals(mount, p.miscEquips[3]) && SupplyAdmitted(actor, p.miscEquips, -1, 3))
                RankMount(ref state.Observed, p, mount, -1);
            state.Observed.RodSlot = -1;
            int discord = -1;
            for (int slot = 0; slot < CompanionBehavior.MainInventorySlots; slot++)
            {
                Item item = p.inventory[slot];
                if (item == null || item.IsAir) continue;
                if (ValidMountItem(item) && SupplyAdmitted(actor, p.inventory, 0, slot))
                {
                    if (state.Observed.DefaultMountType < 0 || ReferenceEquals(item, mount))
                    {
                        state.Observed.DefaultMountSlot = slot;
                        state.Observed.DefaultMountType = item.mountType;
                    }
                    RankMount(ref state.Observed, p, item, slot);
                }
                if (state.Observed.RodSlot < 0 && item.type == ItemID.RodOfHarmony && SupplyAdmitted(actor, p.inventory, 0, slot))
                    state.Observed.RodSlot = slot;
                if (discord < 0 && item.type == ItemID.RodofDiscord && SupplyAdmitted(actor, p.inventory, 0, slot)) discord = slot;
            }
            if (state.Observed.RodSlot < 0 && !p.chaosState) state.Observed.RodSlot = discord;
            state.Observed.CanUseRod = state.Observed.RodSlot >= 0 && !p.noItems && !p.cursed;
            var data = CompanionMobilityState.MountData(state.Observed.DefaultMountType);
            state.Observed.DefaultMountFly = data?.flightTimeMax > 0;
            state.Observed.DefaultMountHover = data?.usesHover ?? false;
        }

        private static void RankMount(ref CompanionMobilitySnapshot observed, Player body, Item item, int slot)
        {
            var data = CompanionMobilityState.MountData(item.mountType);
            if (data == null) return;
            float flight = data.usesHover && data.fatigueMax == 0 ? float.MaxValue : Math.Max(data.flightTimeMax, data.fatigueMax);
            if (flight > observed.FlightMountBudget)
            {
                observed.FlightMountBudget = flight;
                observed.FlightMountType = item.mountType;
                observed.FlightMountSlot = slot;
            }
            if (data.swimSpeed > observed.WaterMountSpeed)
            {
                observed.WaterMountSpeed = data.swimSpeed;
                observed.WaterMountType = item.mountType;
                observed.WaterMountSlot = slot;
            }
            var projection = data.delegations.PlayerSize;
            if (projection != null && projection(body, out var size) && size.HasValue)
            {
                var normal = body.DefaultSize;
                int width = (int)size.Value.X, height = (int)size.Value.Y;
                if (width > 0 && height > 0 && width <= normal.X && height < normal.Y &&
                    (observed.NarrowMountType < 0 || width * height < observed.NarrowMountWidth * observed.NarrowMountHeight))
                {
                    observed.NarrowMountType = item.mountType;
                    observed.NarrowMountSlot = slot;
                    observed.NarrowMountWidth = width;
                    observed.NarrowMountHeight = height;
                }
            }
        }

        private static bool ValidMountItem(Item item) => item != null && !item.IsAir && item.mountType >= 0 &&
            item.mountType < MountID.Sets.Cart.Length && !MountID.Sets.Cart[item.mountType] && (!item.expertOnly || Main.expertMode);

        private static bool ActiveRod(Player body, Item item) => item != null && !item.IsAir &&
            (item.type == ItemID.RodOfHarmony || item.type == ItemID.RodofDiscord && !body.chaosState);

        private bool BindMobilitySource(Actor actor, Item selected, int explicitSlot)
        {
            if (selected == null || selected.IsAir) return false;
            var p = actor.Body;
            int slot = explicitSlot >= 0 ? explicitSlot : Array.IndexOf(p.inventory, selected);
            var items = slot >= 0 ? p.inventory : p.miscEquips;
            if (slot < 0) slot = Array.IndexOf(items, selected);
            if (slot < 0 || slot >= items.Length || !ReferenceEquals(items[slot], selected) ||
                !SupplyAdmitted(actor, items, ReferenceEquals(items, p.inventory) ? 0 : -1, slot)) return false;
            actor.Mobility.BindSource(items, slot);
            return true;
        }

        private static bool IsMobilityHand(CompanionMovementActivation action) => action == CompanionMovementActivation.Rod ||
            action == CompanionMovementActivation.Hook || action == CompanionMovementActivation.QuickMount ||
            action == CompanionMovementActivation.Dismount || action == CompanionMovementActivation.QuickCart;

        private static bool HandlessMount(int type) => type == MountID.Rat || type == MountID.Bat || type == MountID.Pixie;

        private static bool HandlessShape(Player body) => body.mount.Active && HandlessMount(body.mount.Type);

        private static bool CanLeaveHandlessShape(Player body)
        {
            if (!HandlessShape(body) || body.cursed || body.CCed || body.tongued || body.shimmering || body.isLockedToATile ||
                body.gravDir < 0 || body.stairFall || body.velocity.Y < -.001f || body.velocity.Y > 1f ||
                body.wet && !body.gills && body.breath < body.breathMax / 3 || !DismountKnown(body)) return false;

            // Native hover uses -.001 Y even when Bat or Pixie is stationary.
            // Only actual support for the expanded body admits the exit.
            using (CompanionNavigation.Scratch.Save())
            {
                if (!Collision.TryChangingSizeFromBottomCenter(body.Hitbox, 20, 42, out var normal)) return false;
                var pose = new Vector2(normal.X, normal.Y);
                var next = pose + new Vector2(body.velocity.X, 0f);
                var down = new Vector2(0f, 2f);
                return CompanionMobilityGeometry.ClearBody(pose, normal.Width, normal.Height, 16) &&
                    CompanionMobilityGeometry.ClearBody(next, normal.Width, normal.Height, 16) &&
                    Collision.TileCollision(pose, down, normal.Width, normal.Height).Y < down.Y &&
                    Collision.TileCollision(next, down, normal.Width, normal.Height).Y < down.Y;
            }
        }

        private bool StageMobility(Actor actor, CompanionMobilityFrame frame, bool maintenance, bool handNeedsShapeExit, bool attackReady)
        {
            var p = actor.Body;
            var state = actor.Mobility;
            // AI keys do not run through TriggersSet.CopyInto. Only the real
            // unpressed frame arms the next native hotkey edge.
            p.releaseDash |= !p.controlDash;
            p.controlDash = frame.Dash != 0 && state.Observed.DashReady;
            p.controlLeft = (p.controlDash ? frame.Dash : frame.Move) < 0;
            p.controlRight = (p.controlDash ? frame.Dash : frame.Move) > 0;
            p.controlJump = frame.Jump;
            p.controlUp = frame.Vertical < 0;
            p.controlDown = frame.Drop || frame.Vertical > 0;

            state.Prepared = false;
            if (state.PreviousSelection >= 0) return true;
            if (state.Result == CompanionMobilityResult.Started)
            {
                // Mount.SetMount expands the cart's hitbox upward after the
                // track hook positions it. The native magnet may take a few
                // gravity steps to settle; an initial left/right edge while
                // on the wrong ground would immediately dismount it again.
                if (state.Action == CompanionMovementActivation.QuickCart && p.mount.Cart && !p.onTrack)
                    p.controlLeft = p.controlRight = p.controlUp = p.controlDown = p.controlJump = false;
                return false;
            }
            bool shapeExit = handNeedsShapeExit && !frame.EmergencyHand && !frame.Jump && !frame.Drop && frame.Dash == 0 &&
                (frame.Activation == CompanionMovementActivation.None || frame.Activation == CompanionMovementActivation.Dismount) &&
                CanLeaveHandlessShape(p);
            if (state.ShapeHandExit && state.Result == CompanionMobilityResult.Rejected)
            {
                if (shapeExit && state.Target == p.position) return false;
                if (!shapeExit) state.Result = CompanionMobilityResult.Idle;
            }
            if (shapeExit) frame.Activation = CompanionMovementActivation.Dismount;
            if (frame.Activation == CompanionMovementActivation.None) return false;
            // A hand-driven exit is admitted by the live handless mount.
            // A previous completed exit could not have left that mount active.
            // Keep the planner's command deduplication for route actions.
            if (!shapeExit && !state.ShapeHandExit && state.Sequence == frame.Sequence && state.Action == frame.Activation &&
                (state.Result == CompanionMobilityResult.Completed || state.Result == CompanionMobilityResult.Rejected)) return false;

            state.Sequence = frame.Sequence;
            state.Action = frame.Activation;
            state.ShapeHandExit = shapeExit;
            state.Target = shapeExit ? p.position : new Vector2(frame.WorldX, frame.WorldY);
            state.Result = CompanionMobilityResult.Deferred;
            if (!SupplyActorReady(actor) || Main.netMode == 1 && actor.Connection?.Finished != false || p.ghost || p.spectating >= 0 ||
                p.CCed || p.tongued || p.shimmering || p.isLockedToATile) return false;
            bool hand = IsMobilityHand(state.Action);
            // The queued manual use remains the hand owner, including the
            // native dismount it needs. Other movement actions still defer.
            if (hand && maintenance && !shapeExit) return false;
            if (hand && (!FreeHand(p) || !p.releaseUseItem))
                return true; // Take a real release step without cancelling the native animation.

            switch (state.Action)
            {
                case CompanionMovementActivation.Hook:
                    var hook = p.QuickGrapple_GetItemToUse();
                    if (hook == null || !CompanionMobilityGeometry.HookAnchor(p, state.Target, hook.shoot, SmartFallGrapple.Range(hook.shoot), swept: true) ||
                        !BindMobilitySource(actor, hook, -1)) return RejectMobility(state);
                    break;
                case CompanionMovementActivation.QuickCart:
                    if (!CompanionMobilityGeometry.TrackEntry(p, state.Target)) return RejectMobility(state);
                    break;
                case CompanionMovementActivation.QuickMount:
                    if (p.mount.Active || p.gravDir < 0 || p.noItems || p.cursed) return RejectMobility(state);
                    var mount = frame.ItemSlot >= 0 && frame.ItemSlot < CompanionBehavior.MainInventorySlots ? p.inventory[frame.ItemSlot] : p.QuickMount_GetItemToUse();
                    if (attackReady && mount != null && HandlessMount(mount.mountType)) return false;
                    if (!ValidMountItem(mount) || frame.MountTypePlusOne != 0 && frame.MountTypePlusOne != mount.mountType + 1 ||
                        !KnownMountSpace(p, mount.mountType) || !BindMobilitySource(actor, mount, frame.ItemSlot)) return RejectMobility(state);
                    break;
                case CompanionMovementActivation.Dismount:
                    if (!shapeExit && !CanDismountKnown(p)) return RejectMobility(state);
                    break;
                case CompanionMovementActivation.Rod:
                    int slot = frame.ItemSlot;
                    if (slot < 0 || slot >= CompanionBehavior.MainInventorySlots || !ActiveRod(p, p.inventory[slot]) ||
                        !BindMobilitySource(actor, p.inventory[slot], slot) ||
                        !CompanionMobilityGeometry.RodAim(p, state.Target, correct: true, out state.Aim)) return RejectMobility(state);
                    break;
                case CompanionMovementActivation.ReleaseHook:
                    if (p.grappling[0] < 0)
                    {
                        // Jump releases an attached hook. An in-flight hook
                        // is still a real projectile and can only retract or
                        // attach through its own native update.
                        if (!state.Observed.GrappleAttached && !state.Observed.GrappleInFlight) state.Result = CompanionMobilityResult.Completed;
                        p.controlJump = false;
                        return false;
                    }
                    p.controlJump = true;
                    if (!p.releaseJump) { p.controlJump = false; return false; }
                    break;
                case CompanionMovementActivation.Gravity:
                    if (p.gravDir == state.Target.Y) { state.Result = CompanionMobilityResult.Completed; return false; }
                    if (!state.Observed.CanChangeGravity) return RejectMobility(state);
                    p.controlUp = p.releaseUp;
                    if (!p.controlUp) return false;
                    break;
            }

            if (state.Action == CompanionMovementActivation.Rod || state.Action == CompanionMovementActivation.QuickMount && frame.ItemSlot >= 0)
            {
                state.PreviousSelection = p.selectedItem;
                state.HandPressed = true;
                p.selectedItemState.Select(state.SourceSlot);
                p.controlUseItem = true;
                state.Started = Main.GameUpdateCount;
                state.Result = CompanionMobilityResult.Started;
                return true;
            }
            state.Prepared = true;
            return hand;
        }

        private static bool RejectMobility(CompanionMobilityState state)
        { state.Result = CompanionMobilityResult.Rejected; state.NextSourceScan = 0; return false; }

        internal static void RetireMobilityHand(Actor actor)
        {
            var state = actor.Mobility;
            if (state.PreviousSelection >= 0) actor.Body.selectedItemState.Select(state.PreviousSelection);
            state.PreviousSelection = -1;
            state.HandPressed = state.Prepared = false;
        }

        private static bool FinishMobilityHand(Actor actor)
        {
            var state = actor.Mobility;
            if (state.PreviousSelection < 0) return false;
            if (!FreeHand(actor.Body) || state.HandPressed) return true;
            actor.Body.selectedItemState.Select(state.PreviousSelection);
            state.PreviousSelection = -1;
            return false;
        }

        internal bool PrepareMobilityItem(Actor actor, out Vector2 aim)
        {
            var state = actor.Mobility;
            var body = actor.Body;
            aim = state.Action == CompanionMovementActivation.Rod ? state.Aim : body.Center;
            if (state.PreviousSelection < 0) return false;
            body.controlUseItem = state.HandPressed && state.Generation == actor.Generation && SupplyActorReady(actor) &&
                state.SourceCurrent(body) && body.selectedItem == state.SourceSlot && !body.noItems && !body.cursed && !body.CCed;
            state.HandPressed = false;
            if (body.controlUseItem && state.Action == CompanionMovementActivation.Rod)
            {
                body.controlUseItem = ActiveRod(body, state.Source) &&
                    CompanionMobilityGeometry.RodAim(body, state.Target, correct: true, out aim);
                state.Aim = aim;
            }
            if (body.controlUseItem && state.Action == CompanionMovementActivation.QuickMount)
                body.controlUseItem = !body.mount.Active && body.gravDir > 0 && KnownMountSpace(body, state.Source.mountType);
            return true;
        }

        internal void SubmitMobility(Actor actor)
        {
            var state = actor.Mobility;
            var body = actor.Body;
            if (state.DoorNeedsClose && Vector2.DistanceSquared(state.DoorPoint, body.Center) > 64f * 64f)
            {
                state.DoorNeedsClose = false;
                // DoorOpeningHelper owns the earlier touched door. Do not
                // let its close reframe a partially unloaded neighborhood.
                if (CompanionMobilityGeometry.KnownBox(state.DoorPoint - new Vector2(56f, 104f), state.DoorPoint + new Vector2(56f, 104f)))
                    using (CompanionHooks.Enter(actor, state.DoorPoint)) body.doorHelper.LookForDoorsToClose(body);
            }
            if (!state.Prepared || state.Generation != actor.Generation) return;
            state.Prepared = false;
            if (!SupplyActorReady(actor) || body.CCed || body.tongued || body.shimmering || Main.netMode == 1 && actor.Connection?.Finished != false)
            { RejectMobility(state); return; }

            state.Started = Main.GameUpdateCount;
            state.Result = CompanionMobilityResult.Started;
            Point tile = state.Target.ToTileCoordinates();
            switch (state.Action)
            {
                case CompanionMovementActivation.Hook:
                    if (!state.SourceCurrent(body) || !ReferenceEquals(state.Source, body.QuickGrapple_GetItemToUse())) { RejectMobility(state); break; }
                    using (CompanionHooks.Enter(actor, state.Target)) CompanionHooks.MobilityGrapple(body);
                    if (!state.LiveHook(body)) RejectMobility(state);
                    break;
                case CompanionMovementActivation.QuickCart:
                    if (!CompanionMobilityGeometry.TrackEntry(body, state.Target)) { RejectMobility(state); break; }
                    using (CompanionHooks.Enter(actor, state.Target)) launchCart(body, tile.X, tile.Y);
                    if (!state.LiveHook(body)) RejectMobility(state);
                    break;
                case CompanionMovementActivation.QuickMount:
                    if (body.mount.Active || body.gravDir < 0 || body.noItems || body.cursed || !state.SourceCurrent(body) ||
                        !ReferenceEquals(state.Source, body.QuickMount_GetItemToUse()) || !KnownMountSpace(body, state.Source.mountType))
                    { RejectMobility(state); break; }
                    using (CompanionHooks.Enter(actor, body.Center)) CompanionHooks.MobilityQuickMount(body);
                    break;
                case CompanionMovementActivation.Dismount:
                    if (state.ShapeHandExit ? !CanLeaveHandlessShape(body) : !DismountKnown(body))
                    { RejectMobility(state); break; }
                    using (CompanionHooks.Enter(actor, body.Center))
                        if (body.mount.TryDismountWithResult(body) != Mount.DismountCheckResult.Succeeded) RejectMobility(state);
                    break;
                case CompanionMovementActivation.Door:
                    if (!CompanionMobilityGeometry.Known(tile.X, tile.Y)) { RejectMobility(state); break; }
                    var door = Main.tile[tile.X, tile.Y];
                    if (!ClosedDoor(door)) { RejectMobility(state); break; }
                    var contact = DoorContact(body, tile, door);
                    if (contact == DoorContactResult.Approaching) { state.Result = CompanionMobilityResult.Deferred; break; }
                    if (contact == DoorContactResult.Blocked) { RejectMobility(state); break; }
                    using (CompanionHooks.Enter(actor, state.Target)) CompanionHooks.MobilityDoor(body, tile);
                    if (ClosedDoor(Main.tile[tile.X, tile.Y])) RejectMobility(state);
                    else
                    {
                        state.DoorNeedsClose = true;
                        state.DoorPoint = state.Target;
                        state.Result = CompanionMobilityResult.Completed;
                    }
                    break;
                case CompanionMovementActivation.Pylon:
                    if (!CompanionMobilityGeometry.PylonAt(body, state.Target, out var info)) { RejectMobility(state); break; }
                    if (Main.netMode == 0)
                    {
                        using (CompanionHooks.Enter(actor, state.Target)) Main.PylonSystem.RequestTeleportation(info, body);
                        if (state.Result != CompanionMobilityResult.Completed) RejectMobility(state);
                    }
                    else if (!SendPylon(actor, info)) RejectMobility(state);
                    break;
                case CompanionMovementActivation.Portal:
                    if (!CompanionMobilityGeometry.PortalEntry(body, state.Target, out var start, out var end))
                    { state.Result = CompanionMobilityResult.Deferred; break; }
                    using (CompanionHooks.Enter(actor, state.Target)) CompanionHooks.MobilityPortal(body, start, end);
                    if (state.Result != CompanionMobilityResult.Completed) RejectMobility(state);
                    break;
                case CompanionMovementActivation.Teleporter:
                    if (!CompanionMobilityWiring.TryDestination(body, tile, out state.ExpectedExit)) { RejectMobility(state); break; }
                    using (var scope = CompanionHooks.Enter(actor, state.Target))
                    {
                        if (Main.netMode == 0) CompanionHooks.MobilitySwitch(tile);
                        else
                        {
                            // The server's 59 echo is consumed by the primary
                            // world decoder. Running HitSwitch here as well
                            // would toggle the shared switch frame twice.
                            NetMessage.SendData(59, -1, -1, null, tile.X, tile.Y);
                            if (!scope.PacketAccepted) RejectMobility(state);
                        }
                    }
                    break;
            }
        }

        private static bool ClosedDoor(Tile tile) => tile.nactive() && (tile.type == TileID.ClosedDoor || tile.type == TileID.TallGateClosed);

        private static DoorContactResult DoorContact(Player body, Point point, Tile tile)
        {
            int rows = tile.type == TileID.ClosedDoor ? 3 : 5;
            int y = point.Y - tile.frameY % (rows * 18) / 18;
            int direction = (body.controlRight ? 1 : 0) - (body.controlLeft ? 1 : 0);
            if (direction == 0) return DoorContactResult.Approaching;
            float ahead = direction + body.GetAutoDoorVelocityContribution();
            var box = body.Hitbox;
            box.X += Math.Sign(ahead) * (int)Math.Ceiling(Math.Abs(ahead));
            box.Y++;
            box.Height -= 2;
            var area = new Rectangle(point.X * 16, (body.gravDir < 0 ? y - 1 : y) * 16, 16, (rows + 1) * 16);
            if (!area.Intersects(box) || box.Top < area.Top || box.Bottom > area.Bottom) return DoorContactResult.Approaching;
            // WorldGen reads two tiles above the door and reframes its swing
            // and immediate neighbors. Native auto opening may try either
            // direction and cut plants, stalactites and drip tiles as it swings.
            if (!CompanionMobilityGeometry.KnownBox(new Vector2((point.X - 3) * 16, (y - 2) * 16),
                new Vector2((point.X + 3) * 16, (y + rows + 1) * 16))) return DoorContactResult.Blocked;
            if (rows == 3)
                for (int offset = 0; offset < rows; offset++)
                    if (CutByDoor(Main.tile[point.X - 1, y + offset]) || CutByDoor(Main.tile[point.X + 1, y + offset]))
                        return DoorContactResult.Blocked;
            return DoorContactResult.Ready;
        }

        private static bool CutByDoor(Tile tile) => tile.active() && (Main.tileCut[tile.type] || tile.type == TileID.Stalactite ||
            TileID.Sets.IsADripTile[tile.type]);

        private static bool SendPylon(Actor actor, TeleportPylonInfo info)
        {
            // NetManager's module path bypasses NetMessage.SendPacketToServer.
            // Transfer only the initialized frame before the native pool recycles.
            var packet = NetTeleportPylonModule.SerializeUseRequest(info);
            try
            {
                packet.ShrinkToFit();
                var frame = new byte[packet.Length];
                Buffer.BlockCopy(packet.Buffer.Data, 0, frame, 0, frame.Length);
                return actor.Connection.TrySend(frame);
            }
            finally { packet.Recycle(); }
        }

        private void ObserveMobility(Actor actor)
        {
            var state = actor.Mobility;
            var body = actor.Body;
            if (state.HandPressed)
            {
                // This Update skipped the item stage altogether. The input
                // expired with that world step and must not claim a later hand.
                state.HandPressed = false;
                RejectMobility(state);
            }
            state.ObserveBody(body, !FreeHand(body) || ManualHandBusy(actor));
            state.Observed.CanUseCart = !body.mount.Active && body.gravDir > 0 && !state.Observed.Controlled;
            state.Observed.CanDismount = CanDismountKnown(body);
            if (state.Result != CompanionMobilityResult.Started) return;

            bool done;
            int allowance = 0;
            switch (state.Action)
            {
                case CompanionMovementActivation.Hook:
                    done = state.ReachedHookAnchor(body) && ProjectileActor(state.Hook) == actor;
                    allowance = state.LiveHook(body) && state.Hook.ai[0] == 0f ? 120 : 0;
                    break;
                case CompanionMovementActivation.ReleaseHook: done = !state.Observed.GrappleAttached && !state.Observed.GrappleInFlight; break;
                case CompanionMovementActivation.QuickMount: done = body.mount.Active && body.mount.Type == state.Source?.mountType; break;
                case CompanionMovementActivation.Dismount: done = !body.mount.Active; break;
                case CompanionMovementActivation.QuickCart:
                    done = body.mount.Cart && body.onTrack;
                    allowance = body.mount.Cart || state.LiveHook(body) ? 120 : 0;
                    break;
                case CompanionMovementActivation.Gravity: done = body.gravDir == state.Target.Y; break;
                case CompanionMovementActivation.Pylon:
                case CompanionMovementActivation.Teleporter: done = false; allowance = 120; break;
                case CompanionMovementActivation.Rod: done = false; allowance = 2; break;
                default: return;
            }
            if (done)
            {
                if (state.ShapeHandExit) state.PositionRevision++;
                state.Result = CompanionMobilityResult.Completed;
            }
            else if (Main.GameUpdateCount - state.Started >= allowance) RejectMobility(state);
        }

        private static bool CanDismountKnown(Player body)
        {
            if (!body.mount.Active || !DismountKnown(body)) return false;
            using (CompanionNavigation.Scratch.Save()) return body.mount.CanDismount(body);
        }

        private static bool DismountKnown(Player body)
        {
            // Terraria tests the whole size increase in both directions
            // before choosing how much of the extra body fits on each side.
            var margin = new Vector2(16 + Math.Max(0, 20 - body.width), 16 + Math.Max(0, 42 - body.height));
            return CompanionMobilityGeometry.KnownBox(body.TopLeft - margin, body.BottomRight + margin);
        }

        private static bool KnownMountSpace(Player body, int type)
        {
            var data = CompanionMobilityState.MountData(type);
            if (data == null) return false;
            var size = body.Size + new Vector2(0f, data.heightBoost);
            var projection = data.delegations.PlayerSize;
            if (projection != null && projection(body, out var requested) && requested.HasValue) size = requested.Value;
            var topLeft = body.Bottom - new Vector2(size.X * .5f, size.Y) + body.velocity;
            return CompanionMobilityGeometry.KnownBox(topLeft - new Vector2(16f), topLeft + size + new Vector2(16f));
        }
    }
}
