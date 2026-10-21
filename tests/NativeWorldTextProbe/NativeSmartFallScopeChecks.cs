using System;
using System.Reflection;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Direct shared-scope responsibility: this uses the real production
    // session, input admission, priority owner and native hook finalizer.
    internal static class NativeSmartFallScopeChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(object context)
        {
            var movement = Get(context, "Movement"); var input = Get(context, "Input"); var jump = Get(movement, "JumpHost");
            NativeQuickItemChecks.Until(() => (bool)Get(movement, "ControlsEnabled"));
            var switches = new bool[4]; for (int i = 0; i < 4; i++) { switches[i] = (bool)Call(movement, "IsEnabled", i); Call(movement, "SetEnabled", i, i == 3); }
            Player saved = Main.LocalPlayer; bool blocked = Main.blockInput;
            var p = new Player { whoAmI = Main.myPlayer, active = true }; Main.player[Main.myPlayer] = p;
            try
            {
                NativeQuickItemChecks.Sample(input, new Microsoft.Xna.Framework.Input.Keys[0]);
                Require((bool)Call(movement, "BeginUpdate", p), "Only rescue ON admits its own real local scope");
                Call(movement, "CaptureInput", p);
                Require((bool)Call(jump, "ReserveFallProtection", p), "Rescue-only mode has a writable Jump priority owner");
                var remote = new Player { whoAmI = Main.myPlayer, active = true };
                Require(!(bool)Call(movement, "BeginUpdate", remote), "Same-index clone cannot enter the real owner");
                Require(Get(Get(jump, "ownership"), "Owner").ToString() == "FallProtection", "Remote admission cannot clear the local reservation");
                Call(movement, "BeforeJump", p);
                Require(!p.controlJump && !p.autoJump && !p.rocketRelease, "Rescue-only admission does not enable simulated jumping");

                var hooks = movement.GetType().Assembly.GetType("JueMingR.TerrariaHost.Movement.MovementHooks", true);
                var stateType = hooks.GetNestedType("UpdateScope", BindingFlags.NonPublic); object state = Activator.CreateInstance(stateType);
                stateType.GetField("Active", F).SetValue(state, true);
                var nativeError = new InvalidOperationException("owned fixture native failure");
                var result = hooks.GetMethod("EndPlayer", F).Invoke(null, new[] { (object)p, state, nativeError });
                Require(ReferenceEquals(result, nativeError) && !(bool)Call(movement, "CanAct", p), "Native finalizer preserves the original exception and ends only its scope");

                Call(movement, "SetEnabled", 3, false);
                Require(!(bool)Call(movement, "BeginUpdate", p), "All switches OFF and no debt rejects a new scope");
                Call(movement, "SetEnabled", 3, true); Main.blockInput = true;
                Require(!(bool)Call(movement, "BeginUpdate", p), "True input takeover blocks new rescue admission");
                Console.WriteLine("PASS SMART-FALL shared scope: rescue-only admission/priority, remote clone rejection, OFF/input gate and exact exception finalizer");
            }
            finally
            {
                Call(movement, "EndUpdate", p); Main.blockInput = blocked; Main.player[Main.myPlayer] = saved;
                for (int i = 0; i < 4; i++) Call(movement, "SetEnabled", i, switches[i]);
            }
        }
    }
}
