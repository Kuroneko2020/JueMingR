namespace JueMingR.TerrariaHost.Movement
{
    internal struct MovementInputFrame
    {
        internal long Frame, Mapping, Generation, Tick;
        internal bool Captured, JumpHeld, DownHeld, LeftHeld, RightHeld;
    }
}
