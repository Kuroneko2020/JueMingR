using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.Localization;

namespace JueMingR.TerrariaHost.Companions
{
    internal static partial class CompanionHooks
    {
        [ThreadStatic] private static Player mobilityQuickGrapple;
        [ThreadStatic] private static Player mobilityQuickMount;
        [ThreadStatic] private static bool mobilityPortal;
        [ThreadStatic] private static Vector2 mobilityPortalStart, mobilityPortalEnd;
        private static readonly AccessTools.FieldRef<int> wireUser = AccessTools.StaticFieldRefAccess<int>(AccessTools.Field(typeof(Wiring), "CurrentUser"));

        private static void InstallMobilityHooks()
        {
            Patch(typeof(Player), nameof(Player.QuickGrapple), transpiler: nameof(ActorGrappleInput));
            Patch(typeof(Player), "QuickMinecart", prefix: nameof(AllowQuickCart));
            Patch(typeof(Player), "QuickMinecartSnap", prefix: nameof(AllowQuickCart));
            Patch(typeof(Player), "DoCommonDashHandle", transpiler: nameof(ActorDashInput));
            Patch(typeof(Player), "WingAirLogicTweaks", prefix: nameof(ObserveWingRunInputs));
            Patch(typeof(Player), nameof(Player.Teleport), prefix: nameof(MobilityTeleportBefore), postfix: nameof(MobilityTeleportAfter));
            Patch(typeof(PortalHelper), nameof(PortalHelper.TryGoingThroughPortals), transpiler: nameof(ActorPortalEntry));
            Patch(typeof(TeleportPylonsSystem), nameof(TeleportPylonsSystem.HandleTeleportRequest), transpiler: nameof(PylonFailureReceiver));
        }

        private static void ObserveWingRunInputs(Player __instance)
        {
            var actor = playerStep?.Actor;
            if (actor == null || !ReferenceEquals(actor.Body, __instance)) return;
            actor.Mobility.ObserveWingRunInputs(__instance);
        }

        private static void ExtendMobilityUpdate(List<CodeInstruction> code)
        {
            var phase = AccessTools.Method(typeof(Player), "UpdateTeleportVisuals");
            var dash = AccessTools.Method(typeof(Player), nameof(Player.DashMovement));
            var forcedGravity = AccessTools.Field(typeof(Player), nameof(Player.forcedGravity));
            var up = AccessTools.Field(typeof(Player), nameof(Player.controlUp));
            var release = AccessTools.Field(typeof(Player), nameof(Player.releaseUp));
            int phaseCount = 0, dashCount = 0, gravityCount = 0;
            var horizontal = AccessTools.Method(typeof(Player), "HorizontalMovement");
            var holds = AccessTools.Method(typeof(Player), "UpdateControlHolds");
            int horizontalStep = code.FindIndex(instruction => instruction.Calls(horizontal));
            int gravityStart = horizontalStep < 0 ? -1 : code.FindIndex(horizontalStep + 1, instruction => instruction.LoadsField(forcedGravity));
            int gravityEnd = gravityStart < 0 ? -1 : code.FindIndex(gravityStart + 1, instruction => instruction.Calls(holds));
            for (int i = gravityStart + 1; gravityStart >= 0 && i < gravityEnd - 3; i++)
            {
                if (!code[i].LoadsField(up) || !code[i + 3].LoadsField(release)) continue;
                if (gravityCount == 2) break; // The following mount-hover Up is a distinct consumer.
                code[i].opcode = OpCodes.Call;
                code[i].operand = AccessTools.Method(typeof(CompanionHooks), nameof(ActorGravityControl));
                gravityCount++;
            }
            for (int i = code.Count - 1; i >= 0; i--)
            {
                if (code[i].Calls(phase))
                {
                    code.InsertRange(i + 1, new[] { new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompanionHooks), nameof(ApplyMobilityAtBodyStart))) });
                    phaseCount++;
                }
                else if (code[i].Calls(dash))
                {
                    code[i].opcode = OpCodes.Call;
                    code[i].operand = AccessTools.Method(typeof(CompanionHooks), nameof(ActorDash));
                    dashCount++;
                }
            }
            if (phaseCount != 1 || dashCount != 2 || gravityCount != 2)
                throw new InvalidOperationException("Companion mobility native layout changed: start=" + phaseCount + " dash=" + dashCount + " gravity=" + gravityCount);
        }

        private static bool ActorGravityControl(Player player)
        {
            if (!player.controlUp) return false;
            var motion = host?.Owned(player)?.Mobility;
            // Only the native gravity toggle consumes this explicit intent.
            // Ordinary Up remains available to ropes, cart forks and hover.
            return motion == null || motion.Action == CompanionMovementActivation.Gravity && motion.Result == CompanionMobilityResult.Started;
        }

        private static void ApplyMobilityAtBodyStart(Player player)
        {
            var actor = host?.Owned(player);
            if (actor?.Mobility != null && actor.Ready && !actor.Closing && !actor.Faulted && !player.dead) host.SubmitMobility(actor);
        }

        private static void ActorDash(Player player)
        {
            var actor = host?.Owned(player);
            if (actor != null && (player.controlDash || player.dashDelay < 0 || player.eocDash > 0))
            {
                using (Enter(actor, actor.Aim)) player.DashMovement();
            }
            else player.DashMovement();
        }

        private static Player.Settings.DashPreference DashPreference(Player player) => host?.Owned(player) != null ?
            Player.Settings.DashPreference.OnlyThroughHotkeys : Player.Settings.DashControl;

        private static IEnumerable<CodeInstruction> ActorDashInput(IEnumerable<CodeInstruction> input)
        {
            var original = AccessTools.Field(typeof(Player.Settings), nameof(Player.Settings.DashControl));
            int count = 0;
            foreach (var instruction in input)
            {
                if (!instruction.LoadsField(original)) { yield return instruction; continue; }
                var player = new CodeInstruction(OpCodes.Ldarg_0);
                player.labels.AddRange(instruction.labels);
                player.blocks.AddRange(instruction.blocks);
                yield return player;
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CompanionHooks), nameof(DashPreference)));
                count++;
            }
            if (count != 1) throw new InvalidOperationException("Companion dash input layout changed.");
        }

        internal static void MobilityGrapple(Player player)
        {
            var before = mobilityQuickGrapple;
            mobilityQuickGrapple = player;
            try { player.QuickGrapple(); }
            finally { mobilityQuickGrapple = before; }
        }

        private static bool SharesHumanGrappleButton() => mobilityQuickGrapple == null && PlayerInput.GrappleAndInteractAreShared;

        private static IEnumerable<CodeInstruction> ActorGrappleInput(IEnumerable<CodeInstruction> input)
        {
            var original = AccessTools.Field(typeof(PlayerInput), nameof(PlayerInput.GrappleAndInteractAreShared));
            int count = 0;
            foreach (var instruction in input)
            {
                if (instruction.LoadsField(original))
                {
                    // A route supplies a dedicated hook press and aim. Human
                    // profile bindings, hovered UI and tile use own a different key.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(CompanionHooks), nameof(SharesHumanGrappleButton));
                    count++;
                }
                yield return instruction;
            }
            if (count != 1) throw new InvalidOperationException("Companion grapple input layout changed.");
        }

        internal static void MobilityQuickMount(Player player)
        {
            var before = mobilityQuickMount;
            mobilityQuickMount = player;
            try { player.QuickMount(); }
            finally { mobilityQuickMount = before; }
        }

        // The AI chose a mount. The vanilla shared shortcut also handles a
        // nearby track, which has its own precise movement request.
        private static bool AllowQuickCart(Player __instance) => !ReferenceEquals(mobilityQuickMount, __instance);

        internal static bool MobilityDoor(CompanionDoor door, int direction, bool close)
        {
            Point tile = door.Hinge;
            // Player.TileInteractionsUse and MessageBuffer 19 share these
            // outlets. In a client the primary stream owns shared terrain;
            // let its native decode apply the change after the actor sends.
            if (Main.netMode == 1)
            {
                int action = door.Family == CompanionDoorFamily.Door ? (close ? 1 : 0) :
                    door.Family == CompanionDoorFamily.Trapdoor ? (close ? 3 : 2) : (close ? 5 : 4);
                NetMessage.SendData(19, -1, -1, null, action, tile.X, tile.Y, direction);
                return true;
            }
            switch (door.Family)
            {
                case CompanionDoorFamily.Door:
                    return close ? WorldGen.CloseDoor(tile.X, tile.Y) : WorldGen.OpenDoor(tile.X, tile.Y, direction);
                case CompanionDoorFamily.Gate:
                    return WorldGen.ShiftTallGate(tile.X, tile.Y, close);
                case CompanionDoorFamily.Trapdoor:
                    // The original selector is 1 for opening tile 387 and
                    // 0 for closing tile 386. Message 19 maps them to 2 and 3.
                    return WorldGen.ShiftTrapdoor(tile.X, tile.Y, direction > 0, close ? 0 : 1);
                default:
                    return false;
            }
        }

        internal static void MobilityPortal(Player player, Vector2 start, Vector2 end)
        {
            bool previous = mobilityPortal;
            Vector2 priorStart = mobilityPortalStart, priorEnd = mobilityPortalEnd;
            mobilityPortal = true;
            mobilityPortalStart = start;
            mobilityPortalEnd = end;
            try { player.TryPortalJumping(); }
            finally { mobilityPortal = previous; mobilityPortalStart = priorStart; mobilityPortalEnd = priorEnd; }
        }

        private static bool PortalCollision(Vector2 position, Vector2 size, Vector2 start, Vector2 end, float width, ref float collision) =>
            (!mobilityPortal || start == mobilityPortalStart && end == mobilityPortalEnd) &&
            Collision.CheckAABBvLineCollision(position, size, start, end, width, ref collision);

        private static IEnumerable<CodeInstruction> ActorPortalEntry(IEnumerable<CodeInstruction> input)
        {
            var original = AccessTools.Method(typeof(Collision), nameof(Collision.CheckAABBvLineCollision), new[] { typeof(Vector2), typeof(Vector2),
                typeof(Vector2), typeof(Vector2), typeof(float), typeof(float).MakeByRefType() });
            int count = 0;
            foreach (var instruction in input)
            {
                if (instruction.Calls(original))
                {
                    instruction.operand = AccessTools.Method(typeof(CompanionHooks), nameof(PortalCollision));
                    count++;
                }
                yield return instruction;
            }
            if (count != 1) throw new InvalidOperationException("Companion portal entry layout changed.");
        }

        private static IEnumerable<CodeInstruction> PylonFailureReceiver(IEnumerable<CodeInstruction> input)
        {
            var chat = AccessTools.Method(typeof(ChatHelper), nameof(ChatHelper.SendChatMessageToClient));
            int count = 0;
            foreach (var instruction in input)
            {
                if (instruction.Calls(chat))
                {
                    instruction.operand = AccessTools.Method(typeof(CompanionHooks), nameof(ReceivePylonFailure));
                    count++;
                }
                yield return instruction;
            }
            if (count != 1) throw new InvalidOperationException("Companion pylon rejection outlet changed.");
        }

        private static void ReceivePylonFailure(NetworkText text, Color color, int playerIndex)
        {
            var actor = current?.Actor;
            var state = actor?.Mobility;
            if (Main.netMode == 0 && actor?.Body.whoAmI == playerIndex && state.Action == CompanionMovementActivation.Pylon &&
                state.Generation == actor.Generation && state.Result == CompanionMobilityResult.Started)
            {
                // This original local loopback rejection belongs to the
                // requesting companion. Human pylons and remote chat retain
                // their original presentation outside this single outlet.
                state.Result = CompanionMobilityResult.Rejected;
                return;
            }
            ChatHelper.SendChatMessageToClient(text, color, playerIndex);
        }

        private static bool MobilityTeleportBefore(Player __instance, int Style, Vector2 newPos, out Vector2 __state)
        {
            __state = __instance.position;
            return host?.AllowWormholeDisplacement(__instance, Style, newPos) != false;
        }

        private static void MobilityTeleportAfter(Player __instance, int Style, Vector2 newPos, Vector2 __state, bool __runOriginal)
        {
            if (!__runOriginal || __instance.position != newPos) return;
            bool moved = __state != newPos;
            if (moved) host?.ObserveOwnerWarp(__instance);
            var actor = host?.Owned(__instance);
            if (actor == null || Style == 3 && !moved) return;
            var state = actor.Mobility;
            state.PositionRevision++;
            if (Style == 3 && moved) host.ObserveWormholeArrival(actor, newPos);
            // Movement has truly occurred here. Merely animating a rod or
            // submitting a packet does not retire any route as successful.
            if (state.Result != CompanionMobilityResult.Started || state.Generation != actor.Generation) return;
            if (Style == 1 && state.Action == CompanionMovementActivation.Rod && state.PreviousSelection >= 0 ||
                Style == 9 && state.Action == CompanionMovementActivation.Pylon && Vector2.DistanceSquared(state.Target, newPos) <= 96f * 96f ||
                Style == 0 && state.Action == CompanionMovementActivation.Teleporter && Vector2.DistanceSquared(state.ExpectedExit, newPos) <= 24f * 24f ||
                Style == 4 && state.Action == CompanionMovementActivation.Portal && mobilityPortal ||
                Style == 10 && state.Action == CompanionMovementActivation.Hook && state.ReachedHookAnchor(__instance) && state.Hook.type == 935)
                state.Result = CompanionMobilityResult.Completed;
        }

        internal static void MobilitySwitch(Point tile)
        {
            int previous = wireUser();
            Wiring.SetCurrentUser();
            try { Wiring.HitSwitch(tile.X, tile.Y); }
            finally { wireUser() = previous; }
        }
    }
}
