namespace JueMingR.Features.Movement
{
    // One mapped sample advances edges once. Actual Update ticks, rather than
    // draws or wall time, bound the double tap and neutral grace intervals.
    public struct ContinuousDashGesture
    {
        private bool left, right, observed, neutral;
        private long mapping, leftTick, rightTick, firstTick, neutralTick;
        private int firstDirection, armed;
        public void Reset() { this = default(ContinuousDashGesture); }
        public int Sample(long tick, long sample, bool heldLeft, bool heldRight, int facing, ContinuousDashMode mode)
        {
            if (observed && sample == mapping) return Direction(heldLeft, heldRight, facing, mode);
            bool pressedLeft = heldLeft && !left, pressedRight = heldRight && !right;
            if (pressedLeft) leftTick = tick;
            if (pressedRight) rightTick = tick;
            left = heldLeft; right = heldRight; mapping = sample; observed = true;
            int direction = Select(heldLeft, heldRight, facing);
            if (direction == 0)
            {
                if (!neutral) { neutralTick = tick; neutral = true; }
                if (tick - neutralTick >= 2 && armed != 0) { armed = 0; firstDirection = 0; }
                return 0;
            }
            neutral = false;
            bool pressed = direction == -1 ? pressedLeft : pressedRight;
            if (mode == ContinuousDashMode.Hold) return direction;
            if (pressed)
            {
                if (armed != 0 && armed != direction) armed = 0;
                if (firstDirection == direction && tick - firstTick <= 18 && tick >= firstTick) { armed = direction; firstDirection = 0; }
                else { firstDirection = direction; firstTick = tick; }
            }
            return armed == direction ? direction : 0;
        }
        private int Direction(bool heldLeft, bool heldRight, int facing, ContinuousDashMode mode)
        { int direction = Select(heldLeft, heldRight, facing); return mode == ContinuousDashMode.Hold || armed == direction ? direction : 0; }
        private int Select(bool heldLeft, bool heldRight, int facing)
        { return heldLeft && heldRight ? leftTick > rightTick ? -1 : rightTick > leftTick ? 1 : facing == -1 ? -1 : 1 : heldLeft ? -1 : heldRight ? 1 : 0; }
    }
}
