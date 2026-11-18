using System;
using JueMingR.Platform.Companions;

namespace JueMingR.Features.Companions
{
    // Native Update consumes the input and owns all physical state. Planning
    // can propose a route, but only a later observed body can confirm takeoff.
    public sealed class CompanionBehaviorBrain
    {
        private int jumpRemaining, jumpDirection;
        private bool attemptingTakeoff, airborne, walking;
        private float takeoffY, progressX, progressY;
        private int noProgress;
        public int DirectionTo(float delta, int stopDistance, int resumeDistance)
        {
            int threshold = walking ? stopDistance : resumeDistance;
            return Math.Abs(delta) > threshold ? Math.Sign(delta) : 0;
        }
        public CompanionControlFrame Decide(CompanionMovementObservation observation)
        {
            var result = new CompanionControlFrame();
            if (observation.Dead || observation.Controlled)
            {
                attemptingTakeoff = airborne = walking = false;
                jumpRemaining = noProgress = 0;
                if (observation.Controlled) result.Status = "暂时受控";
                return result;
            }
            bool releaseFrame = false;
            if (attemptingTakeoff)
            {
                attemptingTakeoff = false;
                airborne = !observation.Grounded && observation.VelocityY < 0 && observation.Y < takeoffY;
                // On rejection, this decision must reach native JumpMovement
                // with Jump=false even when the original obstacle is unchanged.
                releaseFrame = !airborne;
            }
            if (airborne && observation.Grounded) { airborne = false; jumpRemaining = 0; }
            int direction = DirectionTo(observation.TargetX - observation.X, observation.StopDistance, observation.ResumeDistance);
            walking = direction != 0;
            if (airborne && !observation.Danger && !observation.NeedsAir)
            {
                result.Move = jumpDirection;
                result.Jump = jumpRemaining > 0 && observation.VelocityY < 0;
                if (jumpRemaining > 0) jumpRemaining--;
                result.Status = "跳跃中";
                return result;
            }
            if (observation.Danger || observation.NeedsAir)
            {
                direction = observation.EscapeDirection;
                if (direction == 0) direction = observation.EscapeLeft ? -1 : observation.EscapeRight ? 1 : 0;
                result.Move = direction;
                result.Jump = !releaseFrame && (observation.NeedsAir && !observation.Grounded || observation.CanTakeOff && observation.JumpSafe);
                result.Status = observation.NeedsAir ? "寻找可呼吸位置" : "避险中";
                if (result.Jump && observation.Grounded) StartJump(direction, observation.Y, observation.JumpHoldTicks);
                return result;
            }
            if (Math.Abs(observation.X - progressX) + Math.Abs(observation.Y - progressY) >= 4)
            { progressX = observation.X; progressY = observation.Y; noProgress = 0; }
            else if (direction != 0 && observation.Grounded) noProgress++;
            else noProgress = 0;
            bool targetAbove = observation.TargetY < observation.Y - 36;
            if (!releaseFrame && observation.CanTakeOff && observation.JumpSafe && (observation.Wall || !observation.WalkSafe || targetAbove || noProgress >= 24))
            {
                StartJump(direction, observation.Y, observation.JumpHoldTicks); result.Move = direction; result.Jump = true; result.Status = "跨越障碍"; noProgress = 0; return result;
            }
            if (direction != 0 && (!observation.Grounded || observation.WalkSafe) && !observation.Wall)
            { result.Move = direction; result.Status = "移动中"; }
            else if (direction != 0 || targetAbove) result.Status = "前方受阻";
            else result.Status = "停留";
            if (observation.TargetY > observation.Y + 56 && observation.DropSafe) result.Drop = true;
            return result;
        }
        private void StartJump(int direction, float y, int hold)
        { attemptingTakeoff = true; takeoffY = y; jumpDirection = direction; jumpRemaining = Math.Max(1, hold) - 1; }
    }
}
