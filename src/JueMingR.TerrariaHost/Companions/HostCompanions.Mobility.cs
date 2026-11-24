using System;
using System.Collections.Generic;
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
            state.Observed.HookType = hook != null && !hook.IsAir ? hook.shoot : 0;
            state.Observed.HookRange = SmartFallGrapple.Range(state.Observed.HookType);
            var mountChoices = state.MountChoiceScratch;
            mountChoices.Clear();
            Item equippedMount = p.miscEquips[3];
            if (ValidMountItem(equippedMount) && SupplyAdmitted(actor, p.miscEquips, -1, 3))
                ObserveMount(p, equippedMount, -1, mountChoices);
            state.Observed.RodSlot = -1;
            int discord = -1;
            for (int slot = 0; slot < CompanionBehavior.MainInventorySlots; slot++)
            {
                Item item = p.inventory[slot];
                if (item == null || item.IsAir) continue;
                if (ValidMountItem(item) && SupplyAdmitted(actor, p.inventory, 0, slot))
                    ObserveMount(p, item, slot, mountChoices);
                if (state.Observed.RodSlot < 0 && item.type == ItemID.RodOfHarmony && SupplyAdmitted(actor, p.inventory, 0, slot))
                    state.Observed.RodSlot = slot;
                if (discord < 0 && item.type == ItemID.RodofDiscord && SupplyAdmitted(actor, p.inventory, 0, slot)) discord = slot;
            }
            if (state.Observed.RodSlot < 0 && !p.chaosState) state.Observed.RodSlot = discord;
            state.Observed.CanUseRod = state.Observed.RodSlot >= 0 && !p.noItems && !p.cursed;
            // Share a stable, immutable observation with in-flight queries.
            // Repeated scans of unchanged equipment must not restart them.
            var previous = state.Observed.MountChoices;
            bool changed = previous == null || previous.Length != mountChoices.Count;
            for (int i = 0; !changed && i < previous.Length; i++) changed = !previous[i].Equals(mountChoices[i]);
            if (changed) state.Observed.MountChoices = mountChoices.Count == 0 ? Array.Empty<CompanionMountChoice>() : mountChoices.ToArray();
        }

        private static void ObserveMount(Player body, Item item, int slot, List<CompanionMountChoice> choices)
        {
            var data = CompanionMobilityState.MountData(item.mountType);
            if (data == null) return;
            // Two sources of the same admitted type offer the same native
            // motion. Keep the first current source; unlike different types,
            // these alternatives share shape, physics and startup conditions.
            for (int i = 0; i < choices.Count; i++) if (choices[i].Type == item.mountType) return;
            int type = item.mountType;
            // The book uses Hover instead of CanFly. The shark's hover is
            // limited to native swimming frames and isn't dry flight.
            bool hover = data.usesHover && type != MountID.LavaShark;
            int flyTime = type == MountID.DarkMageBook ? 0 : data.flightTimeMax;
            int flight = hover && data.fatigueMax == 0 ? CompanionMountChoice.UnboundedFlight :
                Math.Max(flyTime, hover ? data.fatigueMax : 0);
            var normal = body.DefaultSize;
            var shape = CompanionMobilityState.MountSize(body, normal, data);
            int width = (int)shape.X, height = (int)shape.Y;
            if (width <= 0 || height <= 0) return;
            bool compact = width <= normal.X && height < normal.Y;
            choices.Add(new CompanionMountChoice(slot, item.mountType, width, height, flight,
                data.runSpeed, data.acceleration, data.swimSpeed, hover, compact));
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
            using (CompanionCollisionScope.Save())
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

        private bool StageMobility(Actor actor, CompanionMobilityFrame frame, bool maintenance, bool handNeedsShapeExit, bool attackReady,
            out CompanionMovementDelivery delivery)
        {
            bool occupiesHand = StageMobilityInputs(actor, frame, maintenance, handNeedsShapeExit, attackReady, out var deferral);
            delivery = new CompanionMovementDelivery(actor.Body, actor.Mobility, frame, deferral);
            return occupiesHand;
        }

        private bool StageMobilityInputs(Actor actor, CompanionMobilityFrame frame, bool maintenance, bool handNeedsShapeExit, bool attackReady,
            out CompanionMovementDeferral deferral)
        {
            var p = actor.Body;
            var state = actor.Mobility;
            deferral = CompanionMovementDeferral.None;
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
            if (state.PreviousSelection >= 0)
            {
                if (frame.Activation != CompanionMovementActivation.None) deferral = CompanionMovementDeferral.HandBusy;
                return true;
            }
            if (state.Result == CompanionMobilityResult.Started)
            {
                // Mount.SetMount expands the cart's hitbox upward after the
                // track hook positions it. The native magnet may take a few
                // gravity steps to settle; an initial left/right edge while
                // on the wrong ground would immediately dismount it again.
                if (state.Action == CompanionMovementActivation.QuickCart && p.mount.Cart && !p.onTrack)
                {
                    p.controlLeft = p.controlRight = p.controlUp = p.controlDown = p.controlJump = false;
                    deferral = CompanionMovementDeferral.NativeTransition;
                }
                else if (frame.Activation != CompanionMovementActivation.None && frame.Sequence != state.Sequence)
                    deferral = CompanionMovementDeferral.NativeTransition;
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
            if (state.Action == CompanionMovementActivation.Door)
            {
                state.DoorFamily = frame.DoorFamily;
                state.DoorPass = frame.DoorPass != 0 ? frame.DoorPass : frame.Move;
            }
            state.Result = CompanionMobilityResult.Deferred;
            if (!SupplyActorReady(actor) || Main.netMode == 1 && actor.Connection?.Finished != false || p.ghost || p.spectating >= 0 ||
                p.CCed || p.tongued || p.shimmering || p.isLockedToATile)
            {
                deferral = CompanionMovementDeferral.NativeCondition;
                return false;
            }
            bool hand = IsMobilityHand(state.Action);
            // The queued manual use remains the hand owner, including the
            // native dismount it needs. Other movement actions still defer.
            if (hand && maintenance && !shapeExit)
            {
                deferral = CompanionMovementDeferral.HandBusy;
                return false;
            }
            if (hand && (!FreeHand(p) || !p.releaseUseItem))
            {
                deferral = CompanionMovementDeferral.ReleaseInput;
                return true; // Take a real release step without cancelling the native animation.
            }

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
                    var mount = frame.ItemSlot >= 0 && frame.ItemSlot < CompanionBehavior.MainInventorySlots ? p.inventory[frame.ItemSlot] :
                        frame.MountTypePlusOne != 0 ? p.miscEquips[3] : p.QuickMount_GetItemToUse();
                    if (attackReady && mount != null && HandlessMount(mount.mountType))
                    {
                        deferral = CompanionMovementDeferral.HandBusy;
                        return false;
                    }
                    if (!ValidMountItem(mount) || frame.MountTypePlusOne != 0 && frame.MountTypePlusOne != mount.mountType + 1 ||
                        !CanMountKnown(p, mount.mountType) || !BindMobilitySource(actor, mount, frame.ItemSlot)) return RejectMobility(state);
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
                    if (!p.releaseJump) { p.controlJump = false; deferral = CompanionMovementDeferral.ReleaseInput; return false; }
                    break;
                case CompanionMovementActivation.Gravity:
                    if (p.gravDir == state.Target.Y) { state.Result = CompanionMobilityResult.Completed; return false; }
                    if (!state.Observed.CanChangeGravity) return RejectMobility(state);
                    p.controlUp = p.releaseUp;
                    if (!p.controlUp) { deferral = CompanionMovementDeferral.ReleaseInput; return false; }
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
                body.controlUseItem = !body.mount.Active && body.gravDir > 0 && CanMountKnown(body, state.Source.mountType);
            return true;
        }

        internal void SubmitMobility(Actor actor)
        {
            var state = actor.Mobility;
            var body = actor.Body;
            if (state.DoorNeedsClose && state.Generation == actor.Generation) ClosePassedDoor(actor);
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
                        !ReferenceEquals(state.Source, body.QuickMount_GetItemToUse()) || !CanMountKnown(body, state.Source.mountType))
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
                    OpenRouteDoor(actor, tile);
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

        private void OpenRouteDoor(Actor actor, Point tile)
        {
            var body = actor.Body;
            var state = actor.Mobility;
            if (!CompanionDoor.TryRead(tile, out var door) || state.DoorFamily != CompanionDoorFamily.None && state.DoorFamily != door.Family)
            { RejectMobility(state); return; }
            if (door.Open) { state.Result = CompanionMobilityResult.Completed; return; }
            if (state.DoorPass == 0 || !door.AtApproach(body, state.DoorPass))
            { state.Result = CompanionMobilityResult.Deferred; return; }
            if (!door.InReach(body, ActorTileReach(body))) { RejectMobility(state); return; }
            state.DoorFamily = door.Family;
            state.Target = door.Target;
            int direction = state.DoorPass;
            if (!door.SwingClear(direction)) direction = -direction;
            if (!door.SwingClear(direction)) { RejectMobility(state); return; }
            if (door.Family == CompanionDoorFamily.Gate && !DoorApertureEmpty(door)) { RejectMobility(state); return; }

            using (var scope = CompanionHooks.Enter(actor, door.Target))
            {
                bool sent = scope.TileInteractionReady && CompanionHooks.MobilityDoor(door, direction, close: false);
                if (!sent || Main.netMode == 1 && !scope.PacketAccepted) RejectMobility(state);
            }
        }

        private static bool DoorApertureEmpty(CompanionDoor door)
        {
            Rectangle aperture = door.Aperture;
            for (int x = aperture.Left / 16; x < aperture.Right / 16; x++)
                for (int y = aperture.Top / 16; y < aperture.Bottom / 16; y++)
                    if (!Collision.EmptyTile(x, y, ignoreTiles: true)) return false;
            return true;
        }

        private void ClosePassedDoor(Actor actor)
        {
            var body = actor.Body;
            var state = actor.Mobility;
            var opened = state.DoorOpened;
            if (Main.GameUpdateCount - state.DoorOpenedAt > 120 || !SupplyActorReady(actor) || body.CCed || body.tongued ||
                body.shimmering || body.isLockedToATile || Main.netMode == 1 && actor.Connection?.Finished != false ||
                !CompanionDoor.TryRead(opened.Hinge, out var door) || !door.Open || door.Family != opened.Family || door.Tiles != opened.Tiles)
            { state.DoorNeedsClose = false; return; }

            // Clear the actual closing footprint and one body step on the
            // far side before releasing this actor's one close opportunity.
            Rectangle aperture = door.Aperture;
            int passage = state.DoorOpenedPass;
            bool crossed = door.Family == CompanionDoorFamily.Trapdoor ?
                (passage > 0 ? body.Top.Y > aperture.Bottom + 4 : body.Bottom.Y < aperture.Top - 4) :
                (passage > 0 ? body.Left.X > aperture.Right + 12 : body.Right.X < aperture.Left - 12);
            if (!crossed || !door.InReach(body, ActorTileReach(body)) || !DoorApertureEmpty(door)) return;
            state.DoorNeedsClose = false;
            using (var scope = CompanionHooks.Enter(actor, door.Target))
                if (scope.TileInteractionReady) CompanionHooks.MobilityDoor(door, passage, close: true);
        }

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
                case CompanionMovementActivation.Door:
                    done = CompanionDoor.TryRead(state.Target.ToTileCoordinates(), out var door) && door.Open &&
                        door.Family == state.DoorFamily && door.Target == state.Target;
                    if (done)
                    {
                        state.DoorNeedsClose = true;
                        state.DoorOpened = door;
                        state.DoorOpenedPass = state.DoorPass;
                        state.DoorOpenedAt = Main.GameUpdateCount;
                    }
                    allowance = Main.netMode == 1 ? 45 : 0;
                    break;
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
            using (CompanionCollisionScope.Save()) return body.mount.CanDismount(body);
        }

        private static bool DismountKnown(Player body)
        {
            // Terraria tests the whole size increase in both directions
            // before choosing how much of the extra body fits on each side.
            var margin = new Vector2(16 + Math.Max(0, 20 - body.width), 16 + Math.Max(0, 42 - body.height));
            return CompanionMobilityGeometry.KnownBox(body.TopLeft - margin, body.BottomRight + margin);
        }

        private static bool CanMountKnown(Player body, int type)
        {
            var data = CompanionMobilityState.MountData(type);
            if (data == null) return false;
            var size = CompanionMobilityState.MountSize(body, body.Size, data);
            var topLeft = body.Bottom - new Vector2(size.X * .5f, size.Y) + body.velocity;
            if (!CompanionMobilityGeometry.KnownBox(topLeft - new Vector2(16f), topLeft + size + new Vector2(16f))) return false;
            using (CompanionCollisionScope.Save()) return body.mount.CanMount(type, body);
        }
    }
}
