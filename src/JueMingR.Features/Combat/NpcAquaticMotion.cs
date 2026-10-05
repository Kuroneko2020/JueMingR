using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcAquaticMotion
    {
        // All locked AI16 defaults enter here. Special action phases stop at
        // their own boundary, rather than refusing a member's ordinary swim.
        internal static bool Known(int type){return type==55 || type==57 || type==58 || type==65 || type==102 || type==157 || type==241 || type==465 || type==592 || type==607 || type==615 || type==688 || type==692;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,int direction,int vertical,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;bool large=type==157,shark=type==65 || type==692,fast=shark || type==102;
            if(type==615)
            {
                if(n.A2!=0){stop=PredictionStop.UnsupportedMechanism;return false;}
                // The native threshold is drawn in [300,1200) each update.
                // Below 300 no draw can choose an action; stop before the
                // first possible independent leap/bob, never invent its RNG.
                if(++n.A3>=300){stop=PredictionStop.RandomDecision;return false;}
            }
            if(type==688)
            {
                if(!e.Multiplayer)
                {
                    if(n.JustHit && n.A2==0){stop=PredictionStop.PhaseBoundary;return false;}
                    if(--n.L0<=0){n.L0=120;if(n.A2==1)n.A2=0;if(n.JustHit){stop=PredictionStop.PhaseBoundary;return false;}}
                }
                if(n.A2==1){stop=PredictionStop.UnsupportedMechanism;return false;}
            }
            if(n.Direction==0){n.Direction=direction;n.DirectionY=vertical;}
            // AI16 owns dry gravity even though these NPCs set NoGravity.
            // The original dry flop draws from discrete [-5,-2.1]/[-2,1.9]
            // ranges. Use a qualified interior representative, never live RNG.
            if(!n.Wet)
            {if(n.Vy==0){if(shark){n.Vx*=.94f;if(Math.Abs(n.Vx)<.2f)n.Vx=0;}else if(!e.Multiplayer){n.Direction=n.Direction<0?-1:1;n.Vy=-3.6f;n.Vx=n.Direction*.8f;}}n.Vy=Math.Min(10,n.Vy+.3f);n.A0=1;return true;}
            bool pursue=false;
            if(type!=55 && type!=592 && type!=607 && type!=615 && type!=688 && e.PlayerWet && !e.PlayerDead && !t.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out pursue,out stop))return false;
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
                n.Vx+=n.Direction*(large?.25f:fast?.15f:.1f);n.Vy+=n.DirectionY*(large?.2f:fast?.15f:.1f);
                if(large){if(Math.Abs(n.Vx)>8)n.Vx=Math.Sign(n.Vx)*7;if(Math.Abs(n.Vy)>5)n.Vy=Math.Sign(n.Vy)*4;}
                else{n.Vx=Math.Max(fast?-5:-3,Math.Min(fast?5:3,n.Vx));n.Vy=Math.Max(fast?-3:-2,Math.Min(fast?3:2,n.Vy));}
            }
            else
            {
                if(n.A0==0)n.A0=1;
                if(large)n.DirectionY=e.PlayerY-e.PlayerHeight/2>n.Y?1:-1;
                n.Vx+=n.Direction*(large?.2f:.1f);if(Math.Abs(n.Vx)>(large?2:type==615?3:1))n.Vx*=.95f;
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
