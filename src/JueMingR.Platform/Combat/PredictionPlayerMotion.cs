namespace JueMingR.Platform.Combat
{
    // Sampled scalars only. No live Player, Mount, input edge or native update
    // is allowed inside a forecast. Complex mechanisms remain conditional.
    public struct PredictionPlayerMotion
    {
        public float X,Y,Vx,Vy,Gravity,GravityDirection,MaxFall,Acceleration,Slowdown,MaxSpeed,FastMaxSpeed,JumpSpeed;
        public int Width,Height,Jump,JumpHeight,DashDelay;
        public bool Wings,CanFly,OnWrongGround,PortalPhysics;
        public bool Left,Right,Up,Down,HoldJump,ReleaseJump,AutoJump,Hover,Complex,WaterWalk,LavaWalk;
        public bool IgnorePlatforms,IgnoreWater,Merman,Trident,OnTrack,Cart,SkipSlope,SkipConveyor;
        public bool RidingTracks,StepMount,Carpet,Grappled,UnsupportedGeometry,StairFall,FloatInWater;
        public float GfxOffset,StepSpeed;
        // Source-owned identity and explicit ordinary complex mechanism. The
        // bounded observation policy changes only a necessary player premise.
        public object PlayerToken;
        public int PlayerIndex,ObservationMechanism;
        public bool Rope,InvalidMechanism;
        public float WindSpeed,TrackBoost;
        public bool WindPushed;
    }
}
