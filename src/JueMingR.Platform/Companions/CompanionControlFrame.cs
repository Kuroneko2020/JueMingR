namespace JueMingR.Platform.Companions
{
    public struct CompanionControlFrame
    {
        public int Move;
        public bool Jump, Drop;
        public string Status;
    }

    public struct CompanionMovementObservation
    {
        public float X, Y, TargetX, TargetY, VelocityX, VelocityY;
        public bool Grounded, CanTakeOff, Controlled, Dead, WalkSafe, Wall, JumpSafe, DropSafe;
        public bool Danger, EscapeLeft, EscapeRight, NeedsAir;
        public int StopDistance, ResumeDistance;
        public int EscapeDirection, JumpHoldTicks;
    }
}
