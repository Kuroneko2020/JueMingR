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
            PlayerVerticalMotion.AfterFluid(ref p,body.Wet,body.Honey,body.Lava,body.Shimmer);return true;
        }
        private static bool Finite(float n){return !float.IsNaN(n) && !float.IsInfinity(n);}
    }
}
