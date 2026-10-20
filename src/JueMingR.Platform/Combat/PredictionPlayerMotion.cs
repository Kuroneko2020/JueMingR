namespace JueMingR.Platform.Combat
{
    // Sampled scalars only. No live Player, Mount, synthetic input or native update
    // is allowed inside a forecast. Complex mechanisms remain conditional.
    public struct PredictionPlayerMotion
    {
        public float X,Y,Vx,Vy,Gravity,GravityDirection,MaxFall,Acceleration,Slowdown,MaxSpeed,FastMaxSpeed,JumpSpeed;
        public int Width,Height,Jump,JumpHeight,DashDelay;
        // ResetEffects consumes the clock once before the native gravity-source
        // query. GravityAction belongs to this trajectory, never a live frame.
        public int ForcedGravityTicks,GravityAction;
        public bool GravityControlCaptured,GravityControl,GravityControl2,GravityReleaseUp,GravitySourceExpected,IndependentIgnorePlatforms;
        // Buff clocks are sampled separately from this completed action's
        // flags. WallAction is bound to the same trajectory/player token.
        public bool WallFactsCaptured,WallSourceExpected,WallGross,WallTongued,WallLocal;
        public int WallHorrifiedTicks,WallTonguedTicks,WallAction;
        public bool WallHealthCaptured,WallHealthKnown,WallImmunityCaptured,WallImmune;
        public int WallImmuneTime;
        public int WallLife,WallLifeMax,WallRegenCount;
        public float WallRegenTime;
        // Native fall damage reads the preceding completed effects and gravity
        // before ResetEffects/buffs. These facts belong to that same sample.
        public bool WallFallCaptured,WallFallKnown,WallNoFallDamage,WallHasWings;
        public int WallFallStart,WallExtraFall;
        public bool Wings,CanFly,OnWrongGround,PortalPhysics;
        public bool Left,Right,Up,Down,HoldJump,ReleaseJump,AutoJump,Hover,Complex,WaterWalk,LavaWalk;
        // Continuous trusted intent is separate from one native release bit.
        // Its version retires old trajectories on ownership/session changes.
        public bool AssistedJump;
        // Frozen eligibility of the existing held span, not equipment type.
        // Future flight/extra-jump handoff remains conditional. Active thrust
        // and pending fuel transfer cannot borrow this ordinary prefix.
        public bool AssistedOrdinarySpan;
        public long AssistedJumpVersion;
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
        // Only the two native umbrella items change player velocity from
        // ItemCheck. Their pure held-input clock runs AFTER movement; it is
        // not a MaxFall override or permission to execute item/attack logic.
        public bool UmbrellaHold,UmbrellaUseHeld,UmbrellaReleaseUse,UmbrellaUseAllowed,UmbrellaAutoReuse,UmbrellaNativeAutoReuse,UmbrellaItemCheckBlocked;
        public int UmbrellaAnimation,UmbrellaAnimationDuration,UmbrellaReuseDelay;
        // BordersMovement reads world pixel edges, not maxTiles or terrain
        // size. Preserve its completed-player tail facts separately from AI.
        public bool BordersCaptured,BordersNoLimits,BordersRemix,BordersSkyblock,BordersDead,BordersGodMode,BordersForcedGravity;
        public float BorderLeft,BorderRight,BorderTop,BorderBottom;
        // Compare only the bounded pure-value sample already acquired for this
        // forecast; this is not a scan or hash of the live world.
        public bool SameSample(PredictionPlayerMotion b)
        {return AssistedJump==b.AssistedJump && AssistedOrdinarySpan==b.AssistedOrdinarySpan && AssistedJumpVersion==b.AssistedJumpVersion && SameNativeSample(b);}
        private bool SameNativeSample(PredictionPlayerMotion b)
        {return WallFactsCaptured==b.WallFactsCaptured && WallSourceExpected==b.WallSourceExpected && WallGross==b.WallGross && WallTongued==b.WallTongued && WallLocal==b.WallLocal && WallHorrifiedTicks==b.WallHorrifiedTicks && WallTonguedTicks==b.WallTonguedTicks && WallAction==b.WallAction && WallImmunityCaptured==b.WallImmunityCaptured && WallImmune==b.WallImmune && WallImmuneTime==b.WallImmuneTime && WallHealthCaptured==b.WallHealthCaptured && WallHealthKnown==b.WallHealthKnown && WallLife==b.WallLife && WallLifeMax==b.WallLifeMax && WallRegenCount==b.WallRegenCount && WallRegenTime==b.WallRegenTime && WallFallCaptured==b.WallFallCaptured && WallFallKnown==b.WallFallKnown && WallNoFallDamage==b.WallNoFallDamage && WallHasWings==b.WallHasWings && WallFallStart==b.WallFallStart && WallExtraFall==b.WallExtraFall && ForcedGravityTicks==b.ForcedGravityTicks && GravityAction==b.GravityAction && GravityControlCaptured==b.GravityControlCaptured && GravityControl==b.GravityControl && GravityControl2==b.GravityControl2 && GravityReleaseUp==b.GravityReleaseUp && GravitySourceExpected==b.GravitySourceExpected && IndependentIgnorePlatforms==b.IndependentIgnorePlatforms && BordersCaptured==b.BordersCaptured && BordersNoLimits==b.BordersNoLimits && BordersRemix==b.BordersRemix && BordersSkyblock==b.BordersSkyblock && BordersDead==b.BordersDead && BordersGodMode==b.BordersGodMode && BordersForcedGravity==b.BordersForcedGravity && BorderLeft==b.BorderLeft && BorderRight==b.BorderRight && BorderTop==b.BorderTop && BorderBottom==b.BorderBottom && UmbrellaHold==b.UmbrellaHold && UmbrellaUseHeld==b.UmbrellaUseHeld && UmbrellaReleaseUse==b.UmbrellaReleaseUse && UmbrellaUseAllowed==b.UmbrellaUseAllowed && UmbrellaAutoReuse==b.UmbrellaAutoReuse && UmbrellaNativeAutoReuse==b.UmbrellaNativeAutoReuse && UmbrellaItemCheckBlocked==b.UmbrellaItemCheckBlocked && UmbrellaAnimation==b.UmbrellaAnimation && UmbrellaAnimationDuration==b.UmbrellaAnimationDuration && UmbrellaReuseDelay==b.UmbrellaReuseDelay && X==b.X && Y==b.Y && Vx==b.Vx && Vy==b.Vy && Gravity==b.Gravity && GravityDirection==b.GravityDirection && MaxFall==b.MaxFall && Acceleration==b.Acceleration && Slowdown==b.Slowdown && MaxSpeed==b.MaxSpeed && FastMaxSpeed==b.FastMaxSpeed && JumpSpeed==b.JumpSpeed && Width==b.Width && Height==b.Height && Jump==b.Jump && JumpHeight==b.JumpHeight && DashDelay==b.DashDelay && Wings==b.Wings && CanFly==b.CanFly && OnWrongGround==b.OnWrongGround && PortalPhysics==b.PortalPhysics && Left==b.Left && Right==b.Right && Up==b.Up && Down==b.Down && HoldJump==b.HoldJump && ReleaseJump==b.ReleaseJump && AutoJump==b.AutoJump && Hover==b.Hover && Complex==b.Complex && WaterWalk==b.WaterWalk && LavaWalk==b.LavaWalk && IgnorePlatforms==b.IgnorePlatforms && IgnoreWater==b.IgnoreWater && Merman==b.Merman && Trident==b.Trident && OnTrack==b.OnTrack && Cart==b.Cart && SkipSlope==b.SkipSlope && SkipConveyor==b.SkipConveyor && RidingTracks==b.RidingTracks && StepMount==b.StepMount && Carpet==b.Carpet && Grappled==b.Grappled && UnsupportedGeometry==b.UnsupportedGeometry && StairFall==b.StairFall && FloatInWater==b.FloatInWater && GfxOffset==b.GfxOffset && StepSpeed==b.StepSpeed && ReferenceEquals(PlayerToken,b.PlayerToken) && PlayerIndex==b.PlayerIndex && ObservationMechanism==b.ObservationMechanism && Rope==b.Rope && InvalidMechanism==b.InvalidMechanism && WindSpeed==b.WindSpeed && TrackBoost==b.TrackBoost && WindPushed==b.WindPushed && FloatingNow==b.FloatingNow && FloatMount37==b.FloatMount37 && ShimmerImmune==b.ShimmerImmune && VerticalProfile==b.VerticalProfile && Wet==b.Wet && Honey==b.Honey && Lava==b.Lava && Shimmer==b.Shimmer && JumpBoost==b.JumpBoost && WereWolf==b.WereWolf && MoonLordLegs==b.MoonLordLegs && Sticky==b.Sticky && Dazed==b.Dazed && SlowFall==b.SlowFall && Vortex==b.Vortex && DownDash==b.DownDash && Flipper==b.Flipper && DefaultGravity==b.DefaultGravity && JumpSpeedBoost==b.JumpSpeedBoost && SwimTime==b.SwimTime && WetSlime==b.WetSlime && Mounted==b.Mounted && MountAdditive==b.MountAdditive && MerfolkEquipment==b.MerfolkEquipment && BaseFlipper==b.BaseFlipper && HoverUp==b.HoverUp && HoverDown==b.HoverDown && MountJumpHeight==b.MountJumpHeight && JumpHeightExtra==b.JumpHeightExtra && MountJumpSpeed==b.MountJumpSpeed;}

    }
}
