using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private bool roofProbeSet;
        private Vector2 roofProbePosition;

        private static bool HasWingThrust(CompanionMobilitySnapshot mobility) =>
            mobility.WingAbilities && (mobility.WingTime > 0 || mobility.UnlimitedWings);

        private static int FollowHorizontal(Player body, float targetX, float targetSpeed, float speedLimit,
            float acceleration, float neutralDeceleration, int spacing)
        {
            float offset = targetX - body.position.X;
            float error = Math.Sign(offset) * Math.Max(0, Math.Abs(offset) - spacing);
            float velocity = body.velocity.X;
            float adjustment = error * .2f;
            float desired = MathHelper.Clamp(targetSpeed + adjustment, -speedLimit, speedLimit);
            int heading = Math.Sign(velocity);
            int wanted = Math.Abs(desired) >= .4f ? Math.Sign(desired) : 0;

            // A moving target carries its own speed through the near band.
            // Coast when caught up. Reverse is a braking input only when the
            // actual relative stopping room warrants it or pursuit reversed.
            if (heading != 0 && heading == wanted && velocity * heading >= desired * heading)
            {
                float closing = (velocity - targetSpeed) * heading;
                float room = Math.Max(0, offset * heading) + 6f;
                if (closing > 1f && closing * closing > 2f * neutralDeceleration * room &&
                    closing * closing > 2f * (neutralDeceleration + acceleration) * (room - 4f))
                    return -heading;
                return 0;
            }
            if (wanted != 0) return wanted;
            float speed = Math.Abs(velocity);
            float available = Math.Max(0, offset * heading) + 6f;
            return speed > Math.Max(.8f, neutralDeceleration) && speed * speed > 2f * neutralDeceleration * available ?
                -heading : 0;
        }

        private static int FlightHeight(Player body, CompanionMobilitySnapshot mobility, float targetY, float targetSpeed, int tolerance)
        {
            float height = (targetY - body.position.Y) * body.gravDir;
            float relative = (body.velocity.Y - targetSpeed) * body.gravDir;
            bool wingHover = mobility.WingHover && HasWingThrust(mobility);
            float response = wingHover || mobility.MountHover ? 4f : 6f;
            // Use the previous native hover phase for a spatial deadband:
            // capture near the requested height, then keep that phase until
            // the actual owner and relative motion require another height.
            if (wingHover) tolerance += mobility.WingHovering ? 6 : -3;
            float offset = height - relative * response;
            return offset < -tolerance ? -1 : offset > tolerance ? 1 : 0;
        }

        private static bool TryAirFollow(Player body, CompanionMobilitySnapshot mobility, Vector2 goal, Vector2 targetVelocity,
            int spacing, out CompanionMobilityFrame frame)
        {
            int height = SurfaceArrival(body, goal, targetVelocity, spacing) ? 1 :
                FlightHeight(body, mobility, goal.Y, targetVelocity.Y, 14);
            bool clear = DriveFlight(body, mobility, RouteMotion.Air, goal, targetVelocity, spacing, height, out frame);
            return clear && (frame.Move != 0 || frame.Vertical != 0 || frame.Jump ||
                Math.Abs(goal.X - body.position.X) <= spacing + 8 && Math.Abs(goal.Y - body.position.Y) <= 32);
        }

        private bool TryAirRetreat(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
            Vector2 goal, bool allowPlanning, out CompanionMobilityFrame frame, out bool spentPlanning)
        {
            frame = default;
            spentPlanning = false;
            int gravity = body.gravDir < 0 ? -1 : 1;
            int facing = Math.Sign(goal.X - body.position.X);
            using (Scratch.Save())
            {
                bool ahead = facing != 0 && !NextClear(body, facing, 0);
                bool above = (goal.Y - body.position.Y) * gravity < 0 && !NextClear(body, 0, -gravity);
                if (!ahead && !above) return false;

                var step = new RouteStep { From = body.position, To = body.position, Retreat = true, ObserveAfter = true };
                bool foundSide = false;
                if (above && !ahead && allowPlanning && (mobility.MountHover || mobility.WingHover && HasWingThrust(mobility)) &&
                    (!roofProbeSet || Vector2.DistanceSquared(body.position, roofProbePosition) >= 16 * 16))
                {
                    roofProbeSet = true;
                    roofProbePosition = body.position;
                    foundSide = TryOverheadExit(body, goal, gravity, ref step);
                    spentPlanning = true;
                }
                if (!foundSide && (!ahead || !ProveAirRetreat(body, mobility, facing, ref step)) ||
                    RecentlyRejected(step)) return false;

                // A verified release is an ordinary short route action. It
                // keeps owning input until a real pose below the obstruction
                // is reached, and a selected planning slice can look ahead.
                ReleaseRoute();
                route.Add(step);
                routeGoal = goal;
                if (ExecuteStep(body, state, mobility, RouteMotion.Air, step, out frame, out _)) return true;
                RejectStep(step);
                return false;
            }
        }

        private bool TryOverheadExit(Player body, Vector2 goal, int gravity, ref RouteStep step)
        {
            int away = goal.X >= body.position.X ? -1 : 1;
            bool awayClear = true, towardClear = true;
            const int stride = 16;
            // Only an actual head obstruction gets this selected slice.
            // Each side's tests stop at its first blocked native sweep.
            for (int span = stride; span <= 96 && (awayClear || towardClear); span += stride)
            {
                int direction = 0;
                if (awayClear && ProbeOverheadSide(body, away, gravity, span, ref awayClear)) direction = away;
                else if (towardClear && ProbeOverheadSide(body, -away, gravity, span, ref towardClear)) direction = -away;
                if (direction == 0) continue;
                step.Kind = RouteKind.AirAcross;
                step.Direction = direction;
                step.Vertical = -gravity;
                step.To += new Vector2(direction * span, 0);
                return true;
            }
            return false;
        }

        private bool ProbeOverheadSide(Player body, int direction, int gravity, int span, ref bool open)
        {
#if DEBUG
            RouteCandidateChecks++;
            RouteWork += 12;
#endif
            var delta = new Vector2(direction * 16, 0);
            var at = body.position + new Vector2(direction * span, 0);
            open = Swept(body, at - delta, delta, null);
            return open && Swept(body, at, new Vector2(0, -20 * gravity), null);
        }

        private static bool ContinueAirRetreat(Player body, CompanionMobilitySnapshot mobility, Vector2 goal, ref RouteStep step)
        {
            int facing = Math.Sign(goal.X - body.position.X);
            var across = new Vector2(facing * 28, 0);
            if (facing == 0) return false;
            step.From = step.To = body.position;
            if (Swept(body, body.position, across, null))
            {
                // Clear the side of the obstruction before pointing up
                // again. The next leg uses a mode that can actually hold its
                // altitude; ordinary wings can still search a released arc.
                if (!mobility.MountHover && !(mobility.WingHover && HasWingThrust(mobility))) return false;
                step.Kind = RouteKind.AirAcross;
                step.Direction = facing;
                step.Vertical = 0;
                step.To += across;
                return true;
            }
            // A blocked side still owns this descent, even when a momentary
            // upward input can be issued underneath the same roof. Keep the
            // next verified short vertical piece rather than returning to it.
            return ProveAirRetreat(body, mobility, facing, ref step);
        }

        private static bool ProveAirRetreat(Player body, CompanionMobilitySnapshot mobility, int facing, ref RouteStep step)
        {
            var node = new RouteNode { Position = body.position, Motion = RouteMotion.Air, Previous = -1 };
            int fuel = FlightBudget(mobility), glide = GlideBudget(mobility);
            return TryAirEdge(body, mobility, node, 33, facing, null, ref step, ref fuel, ref glide, out RouteMotion exit) &&
                (exit != RouteMotion.Air || CanPowerAscent(mobility) || SafeFreeFlight(body, mobility));
        }

        private static bool SurfaceArrival(Player body, Vector2 goal, Vector2 velocity, int spacing)
        {
            float drop = goal.Y - body.position.Y;
            if (drop * body.gravDir <= 0 || Math.Abs(drop) > 48 || velocity.Y * body.gravDir < -.1f ||
                Math.Abs(goal.X - body.position.X) > spacing + 8 || !Surface(body, goal, null)) return false;
            // Arrival on a real surface finishes with native contact. An
            // airborne target at this same offset keeps the normal hover band.
            var fall = new Vector2(0, drop);
            return Swept(body, body.position, fall, null) && Surface(body, body.position + fall, null);
        }

        private static bool WantsAirCatch(Player body, CompanionMobilitySnapshot mobility, Vector2 goal, Vector2 velocity)
        {
            float offset = goal.X - body.position.X;
            float height = (goal.Y - body.position.Y) * body.gravDir;
            float groundSpeed = Math.Max(body.accRunSpeed, body.maxRunSpeed);
            float airSpeed = mobility.WingHover && mobility.WingHoverRun ? mobility.WingHoverSpeed : mobility.WingSpeed;
            // Use powered catch-up for a sustained speed gap. Normal running
            // off a lip still favors the verified walking descent.
            float catchSpeed = groundSpeed + Math.Max(.75f, groundSpeed * .25f);
            return HasWingThrust(mobility) && airSpeed > groundSpeed + .5f && Math.Abs(offset) > 48 &&
                velocity.X * Math.Sign(offset) > catchSpeed && Math.Abs(velocity.Y) <= 1f &&
                height > -44 && height <= 12 &&
                Footprint(body, goal, Vector2.Zero) && !Surface(body, goal, null);
        }

        private static bool DriveFlight(Player body, CompanionMobilitySnapshot mobility, RouteMotion motion, Vector2 goal,
            Vector2 targetVelocity, int spacing, int height, out CompanionMobilityFrame frame)
        {
            float speed = body.accRunSpeed;
            float acceleration = body.runAcceleration;
            float neutral = body.runSlowdown;
            if (!mobility.MountHover && mobility.WingAbilities && mobility.WingSpeed > 0)
            {
                bool hoverRun = height == 0 && mobility.WingHover && mobility.WingHoverRun && HasWingThrust(mobility);
                speed = Math.Max(body.maxRunSpeed, hoverRun ? mobility.WingHoverSpeed : mobility.WingSpeed);
                acceleration = hoverRun ? mobility.WingHoverAcceleration : mobility.WingAcceleration;
                // Brake with the native opposite-direction acceleration.
                // Beyond running speed the .4 scale only limits accelerating
                // farther in the same direction. A released air axis uses half drag.
                neutral = mobility.WingSlowdown;
            }
            int horizontal = FollowHorizontal(body, goal.X, targetVelocity.X, speed, acceleration, neutral * .5f, spacing);
            if (horizontal != 0 && !SafeFreeFlight(body, mobility) && !mobility.CarpetReady &&
                !FindNearSurface(body, body.position + new Vector2(horizontal * 12, 0), null, out _))
            {
                frame = default(CompanionMobilityFrame);
                return false;
            }
            bool quickAscent = height < 0 && Math.Abs(goal.Y - body.position.Y) > 64 &&
                (goal.Y - body.position.Y) * body.gravDir < 0;
            if (!AirInput(body, mobility, motion, horizontal, height, out frame, quickAscent))
            {
                if (motion == RouteMotion.Air && height < 0)
                    AirInput(body, mobility, motion, horizontal, 0, out frame);
                return false;
            }
            // A hover is still a valid safe input, but cannot monopolize the
            // update when its horizontal lane is physically blocked.
            return horizontal == 0 || frame.Move != 0 || height < 0 && (frame.Jump || frame.Vertical != 0);
        }

        private static bool AirInput(Player body, CompanionMobilitySnapshot mobility, RouteMotion motion,
            int direction, int heightIntent, out CompanionMobilityFrame frame, bool quickAscent = false)
        {
            frame = new CompanionMobilityFrame { Status = "空中移动" };
            if (motion == RouteMotion.Hooked || motion == RouteMotion.Track || motion == RouteMotion.Rope) return false;
            int vertical = heightIntent * (int)body.gravDir;
            if (direction != 0 && !NextClear(body, direction, vertical)) direction = 0;
            if (vertical != 0 && !NextClear(body, 0, vertical))
            {
                // Releasing thrust is still a legal descent/brake near a
                // floor. A blocked ascent cannot authorize another jump.
                if (heightIntent < 0) return false;
                vertical = 0;
            }
            frame.Move = direction;
            if (mobility.MountHover) frame.Vertical = vertical;
            else if (heightIntent < 0)
            {
                if (!CanPowerAscent(mobility) && mobility.JumpTime <= 0) return false;
                if (motion == RouteMotion.Ground)
                    frame.Jump = body.velocity.Y == 0 && (body.releaseJump || body.autoJump);
                else
                {
                    bool wings = HasWingThrust(mobility);
                    frame.Jump = mobility.JumpTime > 0 && body.velocity.Y * body.gravDir < 0 || wings ||
                        mobility.MountFly || mobility.RocketReady || mobility.ReleaseJump && (mobility.ExtraJumps != 0 || mobility.Sliding);
                    if (wings && mobility.WingFastAscent && quickAscent) frame.Vertical = -1;
                }
            }
            else if (heightIntent == 0 && motion != RouteMotion.Ground)
            {
                // Native Down+Jump holds height independently of the two
                // horizontal keys. Carpets still require horizontal input.
                if (mobility.WingHover && HasWingThrust(mobility)) { frame.Vertical = 1; frame.Jump = true; }
                else if (direction != 0 && mobility.CarpetReady) frame.Jump = true;
            }
            if (heightIntent > 0) frame.Status = "下降中";
            return true;
        }
    }
}
