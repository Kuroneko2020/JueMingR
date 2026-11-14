using System;
using JueMingR.Platform.Companions;

namespace JueMingR.Features.Companions
{
    // This brain emits intent only. Collision, debit, damage and healing remain
    // native operations; a planning delay never clears the current jump.
    public sealed class CompanionBehaviorBrain
    {
        private int jumpRemaining, jumpDirection;
        private bool jumping, leftGround;
        private float progressX, progressY;
        private int noProgress;
        public CompanionControlFrame Decide(CompanionMovementObservation observation)
        {
            var result = new CompanionControlFrame { Heal = observation.CanHeal };
            if (observation.Dead) { jumping = leftGround = false; jumpRemaining = 0; return result; }
            if (observation.Controlled) { result.Status = "暂时受控"; return result; }
            if (jumping)
            {
                if (!observation.Grounded) leftGround = true;
                // A native gate or low ceiling can reject takeoff. Retire that
                // unstarted request when its own planned hold is consumed; an
                // airborne jump still keeps its prefix until actual landing.
                if (leftGround && observation.Grounded || !leftGround && jumpRemaining <= 0)
                { jumping = false; jumpRemaining = 0; }
                else
                {
                    result.Move = jumpDirection;
                    result.Jump = jumpRemaining > 0 && (!leftGround || observation.VelocityY < 0);
                    if (jumpRemaining > 0) jumpRemaining--;
                    result.Status = "跳跃中"; return result;
                }
            }
            int direction = Math.Abs(observation.TargetX - observation.X) > 40 ? Math.Sign(observation.TargetX - observation.X) : 0;
            if (observation.Danger || observation.NeedsAir)
            {
                direction = observation.EscapeDirection;
                if (direction == 0) direction = observation.EscapeLeft ? -1 : observation.EscapeRight ? 1 : 0;
                result.Move = direction;
                result.Jump = observation.NeedsAir || observation.Grounded && observation.JumpSafe;
                result.Status = observation.NeedsAir ? "寻找可呼吸位置" : "避险中";
                if (result.Jump && observation.Grounded) StartJump(direction, observation.JumpHoldTicks);
                return result;
            }
            if (Math.Abs(observation.X - progressX) + Math.Abs(observation.Y - progressY) >= 4)
            { progressX = observation.X; progressY = observation.Y; noProgress = 0; }
            else if (direction != 0 && observation.Grounded) noProgress++;
            else noProgress = 0;
            bool targetAbove = observation.TargetY < observation.Y - 36;
            if (observation.Grounded && observation.JumpSafe && (observation.Wall || !observation.WalkSafe || targetAbove || noProgress >= 24))
            {
                StartJump(direction, observation.JumpHoldTicks); result.Move = direction; result.Jump = true; result.Status = "跨越障碍"; noProgress = 0; return result;
            }
            if (direction != 0 && (!observation.Grounded || observation.WalkSafe) && !observation.Wall)
            { result.Move = direction; result.Status = "移动中"; }
            else if (direction != 0 || targetAbove) result.Status = "前方受阻";
            else result.Status = "停留";
            if (observation.TargetY > observation.Y + 56 && observation.DropSafe) result.Drop = true;
            return result;
        }
        private void StartJump(int direction, int hold)
        { jumping = true; leftGround = false; jumpDirection = direction; jumpRemaining = Math.Max(1, hold) - 1; }
    }
}
