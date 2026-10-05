using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI13's target is clamped around its root; the body is not clamped there.
    // Inertia and native contact rebound can overshoot and return naturally.
    internal static class NpcAnchoredMotion
    {
        internal static bool Known(int type){return type==43 || type==56 || type==101 || type==175 || type==259 || type==260;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,out PredictionStop stop)
        {
            stop=PredictionStop.None;int x=(int)n.A0,y=(int)n.A1;
            if(x<5 || y<5 || x>=e.WorldWidth-5 || y>=e.WorldHeight-5){stop=PredictionStop.InvalidState;return false;}
            PredictionTile root;if(!t.Tile(x,y,out root,out stop))return false;
            if(!root.RawActive){n.Active=false;n.Life=-1;stop=PredictionStop.Despawn;return false;}
            int type=n.Identity.Type;float range=type==43?(e.GoodWorld?350:250):type==101?175:type==259?100:type==175?500:type==260?350:150;
            float acc=type==175?.05f:type==260?.15f:.035f;
            if(++n.A2>300){range=(int)(range*1.3);if(n.A2>450)n.A2=0;}
            float rootX=x*16+8,rootY=y*16+8,dx=e.PlayerX-n.Width/2-rootX,dy=e.PlayerY-n.Height/2-rootY;
            float length=(float)Math.Sqrt(dx*dx+dy*dy);if(length>range){dx*=range/length;dy*=range/length;}
            Axis(ref n.Vx,n.X,rootX+dx,dx,acc);Axis(ref n.Vy,n.Y,rootY+dy,dy,acc);
            float cap=type==43?(e.GoodWorld?3.5f:3):type==175?4:2;
            n.Vx=Math.Max(-cap,Math.Min(cap,n.Vx));n.Vy=Math.Max(-cap,Math.Min(cap,n.Vy));
            if(n.CollideX)n.Vx=Rebound(n.OldVx);if(n.CollideY)n.Vy=Rebound(n.OldVy);
            return true;
        }
        private static float Rebound(float old){float value=old*-.7f;return value>0 && value<2?2:value<0 && value>-2?-2:value;}
        private static void Axis(ref float speed,float position,float target,float delta,float acc)
        {if(position<target){speed+=acc;if(speed<0 && delta>0)speed+=acc*1.5f;}else if(position>target){speed-=acc;if(speed>0 && delta<0)speed-=acc*1.5f;}}
    }
}
