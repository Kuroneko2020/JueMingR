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
        public bool FloatingNow,FloatMount37,ShimmerImmune;
        // Completed owned effects, not Player.jumpSpeed/jumpHeight scratch.
        // Previous fluid flags travel through each collision into the NEXT
        // movement parameter selection; geometry does not own current gravity.
        public bool VerticalProfile,Wet,Honey,Lava,Shimmer,JumpBoost,WereWolf,MoonLordLegs,Sticky,Dazed,SlowFall,Vortex,DownDash,Flipper;
        public float DefaultGravity,JumpSpeedBoost;
        public int SwimTime,WetSlime;
        public bool Mounted,MountAdditive,MerfolkEquipment,BaseFlipper,HoverUp,HoverDown;
        public int MountJumpHeight,JumpHeightExtra;
        public float MountJumpSpeed;
        // Compare only the bounded pure-value sample already acquired for this
        // forecast; this is not a scan or hash of the live world.
        public bool SameSample(PredictionPlayerMotion b)
        {return X==b.X && Y==b.Y && Vx==b.Vx && Vy==b.Vy && Gravity==b.Gravity && GravityDirection==b.GravityDirection && MaxFall==b.MaxFall && Acceleration==b.Acceleration && Slowdown==b.Slowdown && MaxSpeed==b.MaxSpeed && FastMaxSpeed==b.FastMaxSpeed && JumpSpeed==b.JumpSpeed && Width==b.Width && Height==b.Height && Jump==b.Jump && JumpHeight==b.JumpHeight && DashDelay==b.DashDelay && Wings==b.Wings && CanFly==b.CanFly && OnWrongGround==b.OnWrongGround && PortalPhysics==b.PortalPhysics && Left==b.Left && Right==b.Right && Up==b.Up && Down==b.Down && HoldJump==b.HoldJump && ReleaseJump==b.ReleaseJump && AutoJump==b.AutoJump && Hover==b.Hover && Complex==b.Complex && WaterWalk==b.WaterWalk && LavaWalk==b.LavaWalk && IgnorePlatforms==b.IgnorePlatforms && IgnoreWater==b.IgnoreWater && Merman==b.Merman && Trident==b.Trident && OnTrack==b.OnTrack && Cart==b.Cart && SkipSlope==b.SkipSlope && SkipConveyor==b.SkipConveyor && RidingTracks==b.RidingTracks && StepMount==b.StepMount && Carpet==b.Carpet && Grappled==b.Grappled && UnsupportedGeometry==b.UnsupportedGeometry && StairFall==b.StairFall && FloatInWater==b.FloatInWater && GfxOffset==b.GfxOffset && StepSpeed==b.StepSpeed && ReferenceEquals(PlayerToken,b.PlayerToken) && PlayerIndex==b.PlayerIndex && ObservationMechanism==b.ObservationMechanism && Rope==b.Rope && InvalidMechanism==b.InvalidMechanism && WindSpeed==b.WindSpeed && TrackBoost==b.TrackBoost && WindPushed==b.WindPushed && FloatingNow==b.FloatingNow && FloatMount37==b.FloatMount37 && ShimmerImmune==b.ShimmerImmune && VerticalProfile==b.VerticalProfile && Wet==b.Wet && Honey==b.Honey && Lava==b.Lava && Shimmer==b.Shimmer && JumpBoost==b.JumpBoost && WereWolf==b.WereWolf && MoonLordLegs==b.MoonLordLegs && Sticky==b.Sticky && Dazed==b.Dazed && SlowFall==b.SlowFall && Vortex==b.Vortex && DownDash==b.DownDash && Flipper==b.Flipper && DefaultGravity==b.DefaultGravity && JumpSpeedBoost==b.JumpSpeedBoost && SwimTime==b.SwimTime && WetSlime==b.WetSlime && Mounted==b.Mounted && MountAdditive==b.MountAdditive && MerfolkEquipment==b.MerfolkEquipment && BaseFlipper==b.BaseFlipper && HoverUp==b.HoverUp && HoverDown==b.HoverDown && MountJumpHeight==b.MountJumpHeight && JumpHeightExtra==b.JumpHeightExtra && MountJumpSpeed==b.MountJumpSpeed;}

    }
}
