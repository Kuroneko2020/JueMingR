using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcJellyfishMotion
    {
        internal static bool Known(int type){return type==63 || type==64 || type==103 || type==221 || type==242 || type==256;}
        internal static bool Retargets(NpcMotionState n){return n.Direction==0 || n.Wet && n.A1!=1 && !n.Friendly;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return n.Wet && n.A1!=1 && !n.Friendly;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;
            // Native freezes this gate before advancing 420/120 clocks. The
            // action that enters or leaves the phase still uses the old gate.
            bool frozen=n.Wet && n.A1==1;if(!frozen){n.Health.DontTakeDamage=false;n.CanReceive=true;}
            if(e.Expert && (type==63 || type==64 || type==103 || type==242))
            {
                if(n.Wet)
                {
                    // A frozen action does not need future player geometry.
                    // Without a prepared timeline advance its clock at the
                    // maximum possible rate (1), retaining only guaranteed
                    // frozen motion. At the earliest possible exit the owner
                    // either replays once from point zero with a valid player
                    // timeline, or keeps the prefix and stops before pursuit.
                    if((!frozen || e.PlayerTimelineActive) && n.Target>=0 && e.PlayerWet && !e.PlayerDead)
                    {
                        bool visible;if(!terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out visible,out stop))return false;
                        float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY;
                        if(visible && dx*dx+dy*dy<150*150)n.A2+=n.A1==0?2:-.25f;
                    }
                    if(frozen){n.Health.DontTakeDamage=true;n.CanReceive=false;if(++n.A2>=120)n.A1=0;}
                    else if(++n.A2>=420){n.A1=1;n.A2=0;}
                }
                else n.A1=n.A2=0;
            }
            if(n.Direction==0)NpcTargeting.Face(ref n,ref e,true,confused);
            if(frozen)return true;
            if(!n.Wet)
            {if(n.Vy==0){n.Vx*=.98f;if(n.Vx>-.01f && n.Vx<.01f)n.Vx=0;}n.Vy=Math.Min(10,n.Vy+.2f);n.A0=1;return true;}
            int cx=(int)n.Bounds.CenterX/16,foot=(int)(n.Y+n.Height)/16;PredictionTile tile,below;
            if(!terrain.Tile(cx,foot,out tile,out stop) || !terrain.Tile(cx,foot+1,out below,out stop))return false;
            var slope=tile.TopSlope?tile:below;if(slope.TopSlope){n.Direction=slope.Slope==2?-1:1;n.Vx=Math.Abs(n.Vx)*n.Direction;}
            if(n.CollideX){n.Vx=-n.Vx;n.Direction=-n.Direction;}
            if(n.CollideY && n.Vy!=0){n.Vy=-n.Vy;n.DirectionY=n.Vy<0?-1:1;n.A0=n.DirectionY;}
            bool pursue=false;
            if(!n.Friendly)
            {
                NpcTargeting.Face(ref n,ref e,false,confused);
                if(e.PlayerWet && !e.PlayerDead && !terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out pursue,out stop))return false;
            }
            if(pursue)
            {
                n.L2=1;n.Vx*=.98f;n.Vy*=.98f;float threshold=.2f;
                float damping=type==103?.98f:type==221?.99f:type==242?.995f:1;n.Vx*=damping;n.Vy*=damping;
                if(type==103)threshold=.6f;else if(type==221)threshold=1;else if(type==242)threshold=3;
                if(n.Vx>-threshold && n.Vx<threshold && n.Vy>-threshold && n.Vy<threshold)
                {
                    if(type==221)n.L0=1;NpcTargeting.Face(ref n,ref e,true,confused);
                    float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY,length=(float)Math.Sqrt(dx*dx+dy*dy);
                    if(length==0){stop=PredictionStop.InvalidState;return false;}float factor=(type==103?9:7)/length;n.Vx=dx*factor;n.Vy=dy*factor;
                }
                return true;
            }
            n.L2=0;n.Vx+=n.Direction*.02f;if(n.Vx<-1 || n.Vx>1)n.Vx*=.95f;
            if(n.A0==-1){n.Vy-=.01f;if(n.Vy<-1)n.A0=1;}else{n.Vy+=.01f;if(n.Vy>1)n.A0=-1;}
            int cy=(int)(n.Y+n.Height/2)/16;PredictionTile above,down1,down2;
            if(!terrain.Tile(cx,cy-1,out above,out stop) || !terrain.Tile(cx,cy+1,out down1,out stop) || !terrain.Tile(cx,cy+2,out down2,out stop))return false;
            if(above.Liquid>128){if(down1.RawActive || down2.RawActive)n.A0=-1;}else n.A0=1;
            if(n.Vy>1.2f || n.Vy<-1.2f)n.Vy*=.99f;return true;
        }
    }
}
