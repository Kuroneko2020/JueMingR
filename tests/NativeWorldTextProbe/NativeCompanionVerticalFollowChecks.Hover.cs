using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private static void HoverTransitions(object host, object profiles, Player human)
        {
            var previous = Player.Settings.HoverControl;
            try
            {
                CheckHoverTransition(host, profiles, human, ButtonControlMode.Hold);
                CheckHoverTransition(host, profiles, human, ButtonControlMode.Click);
            }
            finally { Player.Settings.HoverControl = previous; }
        }

        private static void CheckHoverTransition(object host, object profiles, Player human, ButtonControlMode control)
        {
            const int ground = 86;
            Player.Settings.HoverControl = control;
            using (var run = new Scenario(host, profiles, human, "starboard-" + control, 61 * 16, ground,
                () => Fill(44, 119, ground, TileID.Stone), Starboard))
            {
                int steady = 0;
                for (int tick = 0; tick < 75 && steady < 10; tick++)
                {
                    run.StepOwner(tick < 34 ? new Vector2(0, -4.6f) : Vector2.Zero);
                    if (run.Body.controlDown && run.Body.controlJump && Math.Abs(run.Body.velocity.Y) < 1)
                        steady++;
                    else steady = 0;
                }
                bool latched = run.Body.tryKeepingHoveringDown;
                // The option controls the human input block. A managed body
                // reaches the original wing consumer with its own held keys.
                Require(steady >= 10 && !latched, "the managed body hovers through either real UI setting");
                float start = run.Body.Bottom.Y;
                var target = human.Bottom - new Vector2(0, 32);
                int firstRise = -1, firstUp = -1, stable = 0;
                for (int tick = 0; tick < 58; tick++)
                {
                    run.StepToward(target, new Vector2(0, 2));
                    if (firstUp < 0 && run.Body.controlUp) firstUp = tick;
                    if (firstRise < 0 && run.Body.velocity.Y < -.6f && run.Body.Bottom.Y < start - 2) firstRise = tick;
                    if (run.Body.controlDown && run.Body.controlJump && Math.Abs(run.Body.velocity.Y) < 1 &&
                        Math.Abs(run.Body.Bottom.Y - target.Y) < 18) stable++;
                    else stable = 0;
                }
                Console.WriteLine(FormattableString.Invariant($"FOLLOW HOVER mode={control} latched={latched} up={firstUp} climb={firstRise} gain={start - run.Body.Bottom.Y:F1} stableTail={stable} wing={run.Body.wingTime:F1}"));
                Require(firstRise >= 0 && start - run.Body.Bottom.Y > 16 && stable >= 10 && firstUp < 0,
                    "a shallow owner ascent leaves this native hover mode, climbs and settles at the new height");
            }
        }
    }
}
