namespace JueMingR.Platform.Combat
{
    // Sampled scalars only. No live Player, Mount, input edge or native update
    // is allowed inside a forecast. Complex mechanisms remain conditional.
    public struct PredictionPlayerMotion
    {
        public float X,Y,Vx,Vy,Gravity,GravityDirection,MaxFall,Acceleration,Slowdown,MaxSpeed,JumpSpeed;
        public int Width,Height,Jump,JumpHeight;
        public bool Left,Right,Up,Down,HoldJump,ReleaseJump,AutoJump,Hover,Complex,WaterWalk,LavaWalk;
        public bool IgnorePlatforms,IgnoreWater,Merman,Trident,OnTrack,Cart,SkipSlope,SkipConveyor;
        public bool RidingTracks,StepMount,Carpet,Grappled,UnsupportedGeometry,StairFall;
        public float GfxOffset,StepSpeed;
    }
}
