using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked .8 terrain controllers. Tile semantics belong to AI, separately
    // from the host's existing collision rules. No native entity/AI/RNG is used.
    internal static class NpcTerrainFlightMotion
    {
        internal static bool Known(NpcMotionState n)
        {return n.Style==102 && n.EffectiveType==541 || n.Style==103 && n.EffectiveType>=542 && n.EffectiveType<=545 || n.Style==119 && n.EffectiveType==628;}
        internal static bool NeedsPlayer(NpcMotionState n){return n.Style!=119;}
        internal static bool Retargets(NpcMotionState n){return n.Style==103 || n.Style==102 && n.A0<=0;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return a.A0==b.A0;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.Style==119)
            {
                if(!e.HappyWindyCaptured){stop=PredictionStop.MissingDependency;return false;}
                if(!e.HappyWindy && n.TimeLeft>10)n.TimeLeft=10;
                // Downwind acquisition, attack clocks and projectile836 birth
                // never change this actor's own body/velocity. The common
                // gravity/collision/lifetime tail still runs; do not force V0.
                return true;
            }
            if(terrain==null){stop=PredictionStop.MissingDependency;return false;}
            if(!e.PlayerVelocityCaptured){stop=PredictionStop.MissingDependency;return false;}
            return n.Style==102?Elemental(ref n,e,terrain,confused,out stop):Sandshark(ref n,e,terrain,confused,out stop);
        }
        private static bool Elemental(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;bool brake=false,retarget=true;
            float life=(float)n.Life/n.LifeMax,maxX=2+(1-life)*2,acceleration=.1f+(1-life)*.02f;
            // The actual member541 resets this before the generic stuck check;
            // its style's L2>=30/60 reversal is unreachable, not a new mode.
            n.L2=0;
            if(n.A0<0)n.A0=Math.Min(n.A0+1,0);
            if(n.A0>0)
            {
                retarget=false;brake=true;n.A0++;
                if(n.A0>=135)n.A0=-300;
                if(n.A0==54 && !e.Multiplayer)
                {
                    float dx=e.PlayerX+e.PlayerVx*30-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY;
                    if(dx*dx+dy*dy>=900*900)n.A0=-200;
                    // Random sandnado placement/projectile creation does not
                    // mutate this actor's motor. Forecast no extra attacks.
                }
            }
            if(n.A0==0){n.A0=1;brake=true;}
            float span=16;bool sameX=n.X>n.L0-span && n.X<n.L0+span;
            if(!sameX && (n.Vx<0 && n.Direction>0 || n.Vx>0 && n.Direction<0)){sameX=true;span+=24;}
            bool sameY=n.Y>n.L1-span && n.Y<n.L1+span;
            if(sameX && sameY)n.L2++;
            else{n.L0=n.X;n.L1=n.Y;n.L2=0;}
            if(retarget)NpcTargeting.Face(ref n,ref e,true,confused);
            if(brake){n.Vx*=.96f;n.Vy*=.96f;return true;}
            int x=(int)((n.X+n.Width/2)/16)+n.Direction*2,y=(int)((n.Y+n.Height)/16);
            bool air=true,close=false;
            for(int row=y;row<y+4;row++)
            {
                PredictionTile tile;if(!terrain.Tile(x,row,out tile,out stop))return false;
                if(tile.Active && tile.Solid || tile.Liquid>0){close=row<=y+1;air=false;break;}
            }
            int bottomX=(int)n.Bounds.CenterX/16,bottomY=(int)(n.Y+n.Height)/16;
            for(int row=bottomY;row<bottomY+3;row++)
            {
                PredictionTile tile;if(!terrain.Tile(bottomX,row,out tile,out stop))return false;
                if(tile.Active && tile.Solid || tile.Liquid>0){close=true;air=false;break;}
            }
            if(air)n.Vy=Math.Min(n.Vy+.1f,2);
            else{if(n.DirectionY<0 && n.Vy>0 || close)n.Vy-=.1f;if(n.Vy< -4)n.Vy=-4;}
            if(n.CollideX)
            {
                n.Vx=n.OldVx*-.4f;
                if(n.Direction==-1 && n.Vx>0 && n.Vx<1)n.Vx=1;
                if(n.Direction==1 && n.Vx<0 && n.Vx> -1)n.Vx=-1;
            }
            if(n.CollideY)
            {n.Vy=n.OldVy*-.25f;if(n.Vy>0 && n.Vy<1)n.Vy=1;if(n.Vy<0 && n.Vy> -1)n.Vy=-1;}
            if(n.Direction==-1 && n.Vx> -maxX)
            {n.Vx-=acceleration;if(n.Vx>maxX)n.Vx-=acceleration;else if(n.Vx>0)n.Vx+=acceleration/2;if(n.Vx< -maxX)n.Vx=-maxX;}
            else if(n.Direction==1 && n.Vx<maxX)
            {n.Vx+=acceleration;if(n.Vx< -maxX)n.Vx+=acceleration;else if(n.Vx<0)n.Vx-=acceleration/2;if(n.Vx>maxX)n.Vx=maxX;}
            if(n.DirectionY==-1 && n.Vy> -1)
            {
                n.Vy-=.04f;if(n.Vy>1)n.Vy-=.04f*1.25f;else if(n.Vy>0)n.Vy+=.04f*.75f;
                // Preserve the original -Xmax assignment at the Y threshold.
                if(n.Vy< -1)n.Vy=-maxX;
            }
            else if(n.DirectionY==1 && n.Vy<1)
            {n.Vy+=.04f;if(n.Vy< -1)n.Vy+=.04f*1.25f;else if(n.Vy<0)n.Vy-=.04f*.75f;if(n.Vy>1)n.Vy=1;}
            return true;
        }
        private static bool Sand(PredictionTile tile)
        {
            if(!tile.Active)return false;
            switch(tile.Type)
            {case 53:case 112:case 116:case 234:case 397:case 398:case 402:case 399:case 396:case 400:case 403:case 401:return true;default:return false;}
        }
        private static bool Sandshark(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(n.Direction==0)NpcTargeting.Face(ref n,ref e,true,confused);
            PredictionTile tile;int x=(int)(n.Bounds.CenterX/16),y=(int)(n.Bounds.CenterY/16);
            if(!terrain.Tile(x,y,out tile,out stop))return false;bool inside=Sand(tile) || n.Wet;
            NpcTargeting.Face(ref n,ref e,false,confused);var target=NpcTargeting.Area(n,e);
            float targetX=(int)target.X+(int)target.Width/2,targetY=(int)target.Y+(int)target.Height/2;
            float dx=targetX-n.Bounds.CenterX,dy=targetY-n.Bounds.CenterY,distance=(float)Math.Sqrt(dx*dx+dy*dy);
            bool pursue=e.PlayerVy> -.1f && !(n.TargetCaptured?n.PlayerDead:e.PlayerDead) && distance>150;
            if(n.L0==-1 && !inside)n.L0=20;if(n.L0>0)n.L0--;
            if(inside)
            {
                x=(int)(n.Bounds.CenterX/16);y=(int)((n.Bounds.CenterY+24)/16)-2;
                if(!terrain.Tile(x,y,out tile,out stop))return false;bool buried=Sand(tile);n.A1=buried?1:0;
                if(n.A2<30)n.A2++;
                if(pursue)
                {
                    NpcTargeting.Face(ref n,ref e,true,confused);
                    n.Vx=Math.Max(-5,Math.Min(5,n.Vx+n.Direction*.15f));n.Vy=Math.Max(-3,Math.Min(3,n.Vy+n.DirectionY*.15f));
                    float speed=(float)Math.Sqrt(n.Vx*n.Vx+n.Vy*n.Vy),half=(float)Math.Sqrt(n.Width*n.Width+n.Height*n.Height)/2;
                    float nextX=n.Bounds.CenterX+n.Vx+(speed>0?n.Vx/speed*half:0),nextY=n.Bounds.CenterY+n.Vy+(speed>0?n.Vy/speed*half:0);
                    if(!terrain.Tile((int)(nextX/16),(int)(nextY/16),out tile,out stop))return false;
                    bool continues=Sand(tile) || n.Wet && tile.Liquid>0;
                    if(!continues && Math.Sign(n.Vx)==n.Direction && distance<(e.Remix?700:400) && (n.A2>=30 || n.A2<0))
                    {
                        if(n.L0==0)n.L0=-1;n.A2=-30;
                        dy-=80;float length=(float)Math.Sqrt(dx*dx+dy*dy);
                        if(length==0){stop=PredictionStop.InvalidState;return false;}float inverse=1/length;n.Vx=dx*inverse*12;n.Vy=dy*inverse*12;
                    }
                }
                else
                {
                    if(n.CollideX){n.Vx*=-1;n.Direction*=-1;}
                    if(n.CollideY){n.Vy*=-1;n.DirectionY=Math.Sign(n.Vy);n.A0=n.DirectionY;}
                    n.Vx+=n.Direction*.1f;if(n.Vx< -6 || n.Vx>6)n.Vx*=.95f;
                    n.A0=buried?-1:1;
                    if(n.A0==-1){n.Vy-=.01f;if(n.Vy< -.06f)n.A0=1;}
                    else{n.Vy+=.01f;if(n.Vy>.06f)n.A0=-1;}
                    if(n.Vy>.4f || n.Vy< -.4f)n.Vy*=.95f;
                }
            }
            else
            {
                if(n.Vy==0)
                {if(pursue)NpcTargeting.Face(ref n,ref e,true,confused);n.Vx+=n.Direction*.1f;if(n.Vx< -1 || n.Vx>1)n.Vx*=.95f;}
                n.Vy=Math.Min(n.Vy+.3f,10);n.A0=1;
            }
            return true;
        }
    }
}
