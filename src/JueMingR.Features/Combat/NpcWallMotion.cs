using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // The two forms share one real origin identity. Only this private body
    // changes dimensions/style/gravity; no live NPC Transform is involved.
    internal static class NpcWallMotion
    {
        internal static bool GroundAttachment(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(!e.Multiplayer && !confused && n.Vy==0 && n.L1==0)
            {MotionRect adjusted;bool fit,wall;if(!Attachment(n,t,36,36,out adjusted,out fit,out wall,out stop))return false;if(wall && fit)Transform(ref n,adjusted,237,e);}
            if(!e.Multiplayer && n.EffectiveType==236)n.L1=Math.Max(0,n.L1-1);return true;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            float dx=(int)(e.PlayerX/8)*8-(int)(n.Bounds.CenterX/8)*8,dy=(int)(e.PlayerY/8)*8-(int)(n.Bounds.CenterY/8)*8;
            if(confused){dx*=-2;dy*=-2;}float distance=(float)Math.Sqrt(dx*dx+dy*dy);
            if(distance==0){dx=n.Vx;dy=n.Vy;}else{dx*=3/distance;dy*=3/distance;}
            if(e.PlayerDead){dx=n.Direction*1.5f;dy=-1.5f;}
            bool clear;if(!t.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
            if(clear){Axis(ref n.Vx,dx);Axis(ref n.Vy,dy);}
            else
            {
                n.A0++;n.Vy+=n.A0>0?.023f:-.023f;n.Vx+=n.A0< -100 || n.A0>100?.023f:-.023f;if(n.A0>200)n.A0=-200;
                n.Vx+=dx*.007f;n.Vy+=dy*.007f;if(Math.Abs(n.Vx)>1.5f)n.Vx*=.9f;if(Math.Abs(n.Vy)>1.5f)n.Vy*=.9f;
                n.Vx=Math.Max(-3,Math.Min(3,n.Vx));n.Vy=Math.Max(-3,Math.Min(3,n.Vy));
            }
            if(n.CollideX){n.Vx=n.OldVx*-.5f;if(n.Direction==-1 && n.Vx>0 && n.Vx<2)n.Vx=2;if(n.Direction==1 && n.Vx<0 && n.Vx>-2)n.Vx=-2;}
            if(n.CollideY){n.Vy=n.OldVy*-.5f;if(n.Vy>0 && n.Vy<1.5f)n.Vy=2;if(n.Vy<0 && n.Vy>-1.5f)n.Vy=-2;}
            n.SpriteDirection=-1;
            if(!e.Multiplayer && n.L1==0)
            {MotionRect adjusted;bool fit,wall;if(!Attachment(n,t,50,20,out adjusted,out fit,out wall,out stop))return false;if(!wall && fit)Transform(ref n,adjusted,236,e);}
            if(n.EffectiveType==237)n.L1=Math.Max(0,n.L1-1);return true;
        }
        private static void Axis(ref float speed,float target)
        {if(speed<target){speed+=.12f;if(speed<0 && target>0)speed+=.12f;}else if(speed>target){speed-=.12f;if(speed>0 && target<0)speed-=.12f;}}
        private static bool Attachment(NpcMotionState n,IPredictionTerrain t,int width,int height,out MotionRect adjusted,out bool fit,out bool wall,out PredictionStop stop)
        {
            adjusted=n.Bounds;fit=wall=false;stop=PredictionStop.None;var resize=t as IPredictionResizeTerrain;
            if(resize==null){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!resize.Resize(n.Bounds,width,height,out adjusted,out fit,out stop))return false;
            if(fit && !Walls(adjusted,t,out wall,out stop))return false;
            // Native falls back to the unadjusted center even when fitting
            // failed or the adjusted center did not have enough wall cells.
            if(!wall && !Walls(n.Bounds,t,out wall,out stop))return false;return true;
        }
        private static bool Walls(MotionRect box,IPredictionTerrain t,out bool wall,out PredictionStop stop)
        {
            wall=false;stop=PredictionStop.None;int count=0,x=(int)box.CenterX/16,y=(int)box.CenterY/16;
            for(int i=x-1;i<=x+1;i++)for(int j=y-1;j<=y+1;j++)
            {PredictionTile tile;if(!t.Tile(i,j,out tile,out stop))return false;if(tile.Wall>0 && (!tile.RawActive || !tile.RawSolid || tile.Platform) && ++count>4){wall=true;return true;}}
            return true;
        }
        private static void Transform(ref NpcMotionState n,MotionRect adjusted,int type,PredictionEnvironment e)
        {
            n.MotionType=type;n.X=adjusted.X;n.Y=adjusted.Y;n.Width=(int)adjusted.Width;n.Height=(int)adjusted.Height;
            n.Style=type==237?40:3;n.NoGravity=type==237;n.NoTileCollide=false;n.Scale=1;
            n.A0=n.A1=n.A2=n.A3=n.L0=n.L2=n.L3=0;n.L1=12;n.CollideX=n.CollideY=n.JustHit=false;
            n.Direction=e.PlayerX<n.Bounds.CenterX?-1:1;n.DirectionY=e.PlayerY<n.Bounds.CenterY?-1:1;n.NewSegment=true;
        }
    }
}
