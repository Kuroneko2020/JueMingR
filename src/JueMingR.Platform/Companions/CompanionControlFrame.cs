namespace JueMingR.Platform.Companions
{
    public struct CompanionControlFrame
    {
        public int Move;
        public bool Jump, Drop, Heal;
        public string Status;
    }

    public struct CompanionMovementObservation
    {
        public float X, Y, TargetX, TargetY, VelocityX, VelocityY;
        public bool Grounded, Controlled, Dead, WalkSafe, Wall, JumpSafe, DropSafe;
        public bool Danger, EscapeLeft, EscapeRight, NeedsAir, CanHeal;
        public int EscapeDirection, JumpHoldTicks;
    }
}
