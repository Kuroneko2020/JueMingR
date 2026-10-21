using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Movement;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Movement
{
    // The helper lends only an input edge. Vanilla owns final dashType, cooldown,
    // motion, collision, damage and shield consumption, including first type6.
    internal sealed class ContinuousDashHost
    {
        private const string Id = "JueMingR.Movement.Dash1458";
        private static ContinuousDashHost installed;
        private readonly MovementSession session;
        private ContinuousDashGesture gesture;
        private int direction;
        private Player player;
        private long sampledTick;
        private bool dashOwned, priorControl, priorRelease, inConsumer;
        private readonly List<MethodBase> targets = new List<MethodBase>();
        internal bool Available { get; private set; }
        internal ContinuousDashHost(MovementSession session) { this.session = session; }
        internal void Install()
        {
            if (installed != null) throw new InvalidOperationException("dash-already-installed");
            var harmony = new Harmony(Id);
            try
            {
                var dash = AccessTools.DeclaredMethod(typeof(Player), "DashMovement");
                var common = AccessTools.DeclaredMethod(typeof(Player), "DoCommonDashHandle");
                if (dash == null || common == null || common.GetParameters().Length != 3 || dash.GetMethodBody() == null || common.GetMethodBody() == null) throw new MissingMethodException("dash-native-boundary");
                targets.Add(dash); targets.Add(common);
                harmony.Patch(dash, prefix: new HarmonyMethod(typeof(ContinuousDashHost), "BeginDash"), finalizer: new HarmonyMethod(typeof(ContinuousDashHost), "EndDash"));
                harmony.Patch(common, prefix: new HarmonyMethod(typeof(ContinuousDashHost), "BeginConsumer"), finalizer: new HarmonyMethod(typeof(ContinuousDashHost), "EndConsumer"));
                installed = this; Available = true;
            }
            catch { foreach (var target in targets) harmony.Unpatch(target, HarmonyPatchType.All, Id); targets.Clear(); throw; }
        }
        internal void Uninstall()
        { Available = false; if (ReferenceEquals(installed, this)) installed = null; var harmony = new Harmony(Id); foreach (var target in targets) harmony.Unpatch(target, HarmonyPatchType.All, Id); targets.Clear(); }
        internal void Sample(Player value, MovementInputFrame frame)
        {
            if (!Available || !session.Value.ContinuousDashEnabled) { Reset(); return; }
            sampledTick = frame.Tick;
            direction = gesture.Sample(frame.Tick, frame.Mapping, frame.LeftHeld, frame.RightHeld, value.direction, session.Value.DashMode);
        }
        internal void Reset() { gesture.Reset(); direction = 0; sampledTick = 0; }
        internal void EndUpdate(Player value) { if (ReferenceEquals(player, value)) ReturnDash(value); }
        private static void BeginDash(Player __instance) { installed?.BorrowDash(__instance); }
        private void BorrowDash(Player value)
        {
            if (dashOwned || !Available || !session.Value.ContinuousDashEnabled || direction == 0 || sampledTick == 0 || !session.CanAct(value) || value.dashDelay != 0 || value.controlDash && value.releaseDash) return;
            int type = value.mount.Active && value.mount.Type >= 62 && value.mount.Type <= 65 ? 6 : value.dashType;
            if (type <= 0 || value.mount.Active && (value.mount.Type < 0 || value.mount.Type >= MountID.Sets.CanDash.Length || !MountID.Sets.CanDash[value.mount.Type])) return;
            player = value; priorControl = value.controlDash; priorRelease = value.releaseDash; dashOwned = true;
            value.controlDash = true; value.releaseDash = true;
        }
        private static Exception EndDash(Player __instance, Exception __exception)
        { installed?.ReturnDash(__instance); return __exception; }
        private void ReturnDash(Player value)
        {
            if (!dashOwned || !ReferenceEquals(player, value)) return;
            if (value.controlDash) value.controlDash = priorControl;
            // Native false means the borrowed edge was consumed. Preserve it;
            // restoring true would replay the player's genuine Dash gesture.
            if (value.releaseDash) value.releaseDash = priorRelease;
            player = null; dashOwned = inConsumer = false;
        }
        private struct DirectionScope { internal bool Active, Left, Right; internal int Direction; }
        private static void BeginConsumer(Player __instance, out DirectionScope __state)
        {
            __state = default(DirectionScope); var owner = installed;
            if (owner == null || !owner.dashOwned || owner.inConsumer || !ReferenceEquals(owner.player, __instance) || !owner.session.CanAct(__instance)) return;
            __state = new DirectionScope { Active = true, Left = __instance.controlLeft, Right = __instance.controlRight, Direction = owner.direction };
            owner.inConsumer = true;
            // Borrow directions only at DoCommonDashHandle's actual read. This
            // carries the selected dual-key winner without changing ordinary
            // horizontal movement or reverting native direction after launch.
            __instance.controlLeft = owner.direction == -1; __instance.controlRight = owner.direction == 1;
        }
        private static Exception EndConsumer(Player __instance, DirectionScope __state, Exception __exception)
        {
            var owner = installed;
            if (__state.Active && owner != null && owner.dashOwned && ReferenceEquals(owner.player, __instance))
            {
                if (__instance.controlLeft == (__state.Direction == -1)) __instance.controlLeft = __state.Left;
                if (__instance.controlRight == (__state.Direction == 1)) __instance.controlRight = __state.Right;
                owner.inConsumer = false;
            }
            return __exception;
        }
    }
}
