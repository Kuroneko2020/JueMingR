using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // A single conditional observed player movement step, shared by NPC
    // forecasts and attack-origin preparation. Never Player.Update or item AI.
    public static class PlayerMotionContinuation
    {
        public static bool Advance(ref PredictionPlayerMotion p,PredictionEnvironment e,IPredictionTerrain terrain,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(!BeginGravity(ref p,e,out stop))return false;
            bool wallHandled;if(!BeginWall(ref p,e,terrain,out wallHandled,out stop))return false;if(wallHandled)return true;
            if(!p.Complex || p.Hover)
            {
                PlayerHorizontalMotion.Step(ref p);
                if(p.Hover){float target=p.Up || p.HoldJump?-p.MaxSpeed:p.Down?p.MaxSpeed:0;p.Vy=NpcMotion.Approach(p.Vy,target,p.Acceleration);}
                else PlayerVerticalMotion.Step(ref p,e);
            }
            var body=new NpcMotionState{X=p.X,Y=p.Y,Vx=p.Vx,Vy=p.Vy,Width=p.Width,Height=p.Height,Active=true,Friendly=true,
                Wet=p.Wet,Honey=p.Honey,Lava=p.Lava,Shimmer=p.Shimmer,
                Health=new NpcHealthState{Immortal=true,DontTakeDamage=true,LavaImmune=true,ShimmerImmune=true},WaterSpeed=1,HoneySpeed=1,LavaSpeed=1,ShimmerSpeed=1};
            var playerTerrain=terrain as IPredictionPlayerTerrain;
            if(playerTerrain!=null){if(!playerTerrain.MovePlayer(ref body,e,ref p,out stop))return false;}
            else if(p.WaterWalk)
            {var surface=terrain as IPredictionWaterSurfaceTerrain;if(surface==null){stop=PredictionStop.TerrainUnavailable;return false;}if(!surface.MoveWaterWalkingPlayer(ref body,e,p.Down,p.LavaWalk,out stop))return false;}
            else if(!terrain.Move(ref body,e,out stop))return false;
            if(!Finite(body.X) || !Finite(body.Y) || !Finite(body.Vx) || !Finite(body.Vy)){stop=PredictionStop.InvalidState;return false;}
            p.X=body.X;p.Y=body.Y;p.Vx=body.Vx;p.Vy=body.Vy;
            PlayerVerticalMotion.AfterFluid(ref p,body.Wet,body.Honey,body.Lava,body.Shimmer);
            if(!ClampBorders(ref p,out stop))return false;CompleteItemCheck(ref p);return true;
        }
        // Called after the one gravity clock, before any ordinary controls or
        // settled shortcut. Tongue owns its complete native movement branch.
        public static bool BeginWall(ref PredictionPlayerMotion p,PredictionEnvironment e,IPredictionTerrain terrain,out bool handled,out PredictionStop stop)
        {
            handled=false;stop=PredictionStop.None;
            bool needed=p.WallSourceExpected || e.WallSources!=null || e.WallSource.Captured || p.WallGross || p.WallTongued || p.WallHorrifiedTicks>0 || p.WallTonguedTicks>0;
            if(!needed)return true;if(!p.WallFactsCaptured){stop=PredictionStop.MissingDependency;return false;}
            var body=e.WallSource;
            if(e.WallSources!=null)
            {
                if(!ReferenceEquals(p.PlayerToken,e.WallSources.PlayerToken) || p.PlayerIndex!=e.WallSources.PlayerIndex || p.WallAction<0 || p.WallAction>=e.WallSources.Count)
                {stop=PredictionStop.MissingDependency;return false;}
                body=e.WallSources[p.WallAction];
            }
            if(!body.Captured){stop=PredictionStop.MissingDependency;return false;}
            if(!NpcWallBossMotion.AdvancePlayer(ref p,e,terrain,body,out handled,out stop))return false;
            p.WallAction++;return true;
        }
        public static bool BeginGravity(ref PredictionPlayerMotion p,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(!p.GravityControlCaptured)
            {if(p.GravitySourceExpected || p.ForcedGravityTicks>0){stop=PredictionStop.MissingDependency;return false;}return true;}
            bool required=p.GravitySourceExpected || p.ForcedGravityTicks>0 || e.GravitySourceValid;
            bool captured=e.GravitySourceCaptured,valid=e.GravitySourceValid;float x=e.GravitySourceX,y=e.GravitySourceY;
            if(e.GravitySources!=null)
            {
                if(!ReferenceEquals(p.PlayerToken,e.GravitySources.PlayerToken) || p.PlayerIndex!=e.GravitySources.PlayerIndex || p.GravityAction<0 || p.GravityAction>=e.GravitySources.Count)
                {stop=PredictionStop.MissingDependency;return false;}
                var source=e.GravitySources[p.GravityAction];captured=source.Captured;valid=source.Valid;x=source.X;y=source.Y;
            }
            if(required && !captured){stop=PredictionStop.MissingDependency;return false;}
            if(p.ForcedGravityTicks>0)p.ForcedGravityTicks--;
            if(captured && valid)
            {
                if(!Finite(x) || !Finite(y)){stop=PredictionStop.InvalidState;return false;}
                // Vector2.Distance stores its double-component squared result
                // through the native float root before the strict 4000 test.
                double dx=(double)(p.X+p.Width*.5f)-x,dy=(double)(p.Y+p.Height*.5f)-y;
                float distance=(float)Math.Sqrt(dx*dx+dy*dy);
                if(distance<4000f)p.ForcedGravityTicks=10;
            }
            if(p.ForcedGravityTicks>0)p.GravityDirection=-1;
            else if((p.GravityControl || p.GravityControl2) && !p.Mounted)
            {if(p.Up && p.GravityReleaseUp){p.GravityDirection=-p.GravityDirection;p.Jump=0;}}
            else p.GravityDirection=1;
            p.GravityReleaseUp=!p.Up;p.BordersForcedGravity=p.ForcedGravityTicks>0;
            p.IgnorePlatforms=p.GravityDirection<0 || p.IndependentIgnorePlatforms;p.GravityAction++;return true;
        }
        public static bool ClampBorders(ref PredictionPlayerMotion p,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(!p.BordersCaptured || p.BordersNoLimits)return true;
            if(!Finite(p.BorderLeft) || !Finite(p.BorderRight) || !Finite(p.BorderTop) || !Finite(p.BorderBottom))
            {stop=PredictionStop.InvalidState;return false;}
            // Native applies left then right, before either vertical branch.
            // Clearing velocity belongs to the completed player sample and
            // therefore affects the NPC's next motor read on this action.
            float left=p.BorderLeft+640f,right=p.BorderRight-640f-p.Width;
            if(p.X<left){p.X=left;p.Vx=0;}if(p.X>right){p.X=right;p.Vx=0;}
            float top=p.BorderTop+640f;
            if(p.Y<top)
            {
                if(p.BordersRemix || p.BordersForcedGravity)
                {
                    if(p.Y<top-p.Height && !p.BordersDead && !p.BordersGodMode)
                    {stop=PredictionStop.Despawn;return false;}
                    float upper=p.BorderTop+320f;
                    if(p.Y<upper){p.Y=upper;if(p.Vy<0)p.Vy=0;p.GravityDirection=1;}
                }
                else{p.Y=top;if(p.Vy<.11f)p.Vy=.11f;p.GravityDirection=1;}
            }
            float bottom=p.BorderBottom-640f;
            if(p.Y>bottom)
            {
                if(p.BordersSkyblock && !p.BordersDead && !p.BordersGodMode)
                {stop=PredictionStop.Despawn;return false;}
                p.Y=bottom;p.Vy=0;
            }
            if((p.BordersGodMode || !p.BordersSkyblock) && p.Y>bottom-p.Height){p.Y=bottom-p.Height;p.Vy=0;}
            return true;
        }
        public static void CompleteItemCheck(ref PredictionPlayerMotion p)
        {
            if(!p.UmbrellaHold)return;
            if(p.UmbrellaItemCheckBlocked){p.UmbrellaAnimation=0;return;}
            // Native AutoReuseLogic precedes reuse delay / TryStartUse, and
            // the animation decrements on the same action that starts it.
            if(p.UmbrellaNativeAutoReuse)
            {p.UmbrellaReleaseUse=true;if(p.UmbrellaAnimation==1)p.UmbrellaAnimation=0;}
            if(p.UmbrellaAutoReuse)p.UmbrellaReleaseUse=true;
            if(p.UmbrellaAnimation<0)p.UmbrellaAnimation=0;
            if(p.UmbrellaAnimation==0 && p.UmbrellaReuseDelay>0)
            {p.UmbrellaAnimation=p.UmbrellaReuseDelay;p.UmbrellaReuseDelay=0;}
            if(p.UmbrellaUseHeld && p.UmbrellaReleaseUse && p.UmbrellaAnimation==0 && p.UmbrellaUseAllowed)
                p.UmbrellaAnimation=p.UmbrellaAnimationDuration;
            if(p.UmbrellaAnimation>0)p.UmbrellaAnimation--;
            p.UmbrellaReleaseUse=!p.UmbrellaUseHeld;
            // ApplyHoldStyle is skipped during UseStyle3 and on a pulley.
            // Clamp completed velocity only, preserving this action's move.
            if(p.UmbrellaAnimation==0 && !p.Rope && !p.Down && p.Vy*p.GravityDirection>2)
                p.Vy=2*p.GravityDirection;
        }
        private static bool Finite(float n){return !float.IsNaN(n) && !float.IsInfinity(n);}
    }
}
