using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcAquaticMotion
    {
        internal static bool Known(int type){return type==58 || type==157;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,int direction,int vertical,out PredictionStop stop)
        {
            stop=PredictionStop.None;bool large=n.Identity.Type==157;
            if(n.Direction==0){n.Direction=direction;n.DirectionY=vertical;}
            // AI16 owns dry gravity even though these NPCs set NoGravity.
            // The original dry flop draws from discrete [-5,-2.1]/[-2,1.9]
            // ranges. Use a qualified interior representative, never live RNG.
            if(!n.Wet)
            {if(n.Vy==0 && !e.Multiplayer){n.Direction=n.Direction<0?-1:1;n.Vy=-3.6f;n.Vx=n.Direction*.8f;}n.Vy=Math.Min(10,n.Vy+.3f);n.A0=1;return true;}
            bool pursue=false;
            if(e.PlayerWet && !e.PlayerDead && !t.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out pursue,out stop))return false;
            int cx=(int)n.Bounds.CenterX/16,foot=(int)(n.Y+n.Height)/16;
            PredictionTile bottom,below;if(!t.Tile(cx,foot,out bottom,out stop) || !t.Tile(cx,foot+1,out below,out stop))return false;
            var slope=bottom.TopSlope?bottom:below;
            if(slope.TopSlope){n.Direction=slope.Slope==2?-1:1;n.Vx=Math.Abs(n.Vx)*n.Direction;}
            if(!pursue)
            {if(n.CollideX){n.Vx=-n.Vx;n.Direction=-n.Direction;}if(n.CollideY && n.Vy!=0){n.Vy=-n.Vy;n.DirectionY=n.Vy<0?-1:1;n.A0=n.DirectionY;}}
            if(pursue)
            {
                n.A0=0;n.Direction=direction;n.DirectionY=vertical;
                if(large && n.Vx*n.Direction<0)n.Vx*=.95f;
                n.Vx+=n.Direction*(large?.25f:.1f);n.Vy+=n.DirectionY*(large?.2f:.1f);
                if(large){if(Math.Abs(n.Vx)>8)n.Vx=Math.Sign(n.Vx)*7;if(Math.Abs(n.Vy)>5)n.Vy=Math.Sign(n.Vy)*4;}
                else{n.Vx=Math.Max(-3,Math.Min(3,n.Vx));n.Vy=Math.Max(-2,Math.Min(2,n.Vy));}
            }
            else
            {
                if(n.A0==0)n.A0=1;
                if(large)n.DirectionY=e.PlayerY-e.PlayerHeight/2>n.Y?1:-1;
                n.Vx+=n.Direction*(large?.2f:.1f);if(Math.Abs(n.Vx)>(large?2:1))n.Vx*=.95f;
                if(n.A0==-1){n.Vy-=large?.02f:.01f;float threshold=large?(n.DirectionY<0?-1:n.DirectionY>0?-.2f:-.6f):-.3f;if(n.Vy<threshold)n.A0=1;}
                else{n.Vy+=large?.02f:.01f;float threshold=large?(n.DirectionY<0?.2f:n.DirectionY>0?1:.6f):.3f;if(n.Vy>threshold)n.A0=-1;}
                int cy=(int)(n.Y+n.Height/2)/16;PredictionTile above,down1,down2;
                if(!t.Tile(cx,cy-1,out above,out stop) || !t.Tile(cx,cy+1,out down1,out stop) || !t.Tile(cx,cy+2,out down2,out stop))return false;
                if(above.Liquid>128 && (down1.RawActive || down2.RawActive))n.A0=-1;
                if(!large && Math.Abs(n.Vy)>.4f)n.Vy*=.95f;
            }
            return true;
        }
    }
}
