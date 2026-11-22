using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
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
                // Beyond ordinary run speed the native wing branch only
                // accelerates by 2 × .2 per update. Opposite braking still
                // uses full acceleration. A released air axis uses half drag.
                if (Math.Abs(body.velocity.X) >= body.maxRunSpeed &&
                    Math.Sign(body.velocity.X) == Math.Sign(goal.X - body.position.X))
                    acceleration *= .4f;
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
