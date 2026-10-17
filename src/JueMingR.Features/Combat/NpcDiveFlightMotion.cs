using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked NPC.cs AI74: both members share the same ordered hover/prepare/
    // charge motor. Drone charge scatter is an actual motor random boundary;
    // Corite's Next(0,1) has the sole result zero and keeps its known direction.
    internal static class NpcDiveFlightMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==74 && (n.EffectiveType==388 || n.EffectiveType==418);}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return true;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e){return n.A0!=3;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            bool corite=n.EffectiveType==418;
            if(n.A0!=0 && n.A0!=1 && n.A0!=2 && n.A0!=3 && n.A0!=4){stop=PredictionStop.UnsupportedMechanism;return false;}
            NpcTargeting.Face(ref n,ref e,false,confused);
            if(n.TargetChoiceUnavailable){stop=PredictionStop.MissingDependency;return false;}
            if(corite)n.SpriteDirection=Math.Sign(n.Vx);else if(n.Vx!=0)n.SpriteDirection=-Math.Sign(n.Vx);
            if(!corite && n.A0!=3){n.X+=n.NetOffsetX;n.Y+=n.NetOffsetY;n.X-=n.NetOffsetX;n.Y-=n.NetOffsetY;}
            // Corite conditionally applies that draw translation inside its
            // dust RNG. A nonzero sampled offset can affect stored position;
            // no cosmetic draw is executed or guessed inside this motor.
            if(corite && (n.NetOffsetX!=0 || n.NetOffsetY!=0)){stop=PredictionStop.RandomDecision;return false;}
            float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY;
            if(n.A0==0)
            {
                if(terrain==null){stop=PredictionStop.MissingDependency;return false;}
                float speed=corite?8:10,dx,dy,hx,hy;
                Unit(x,y,out dx,out dy);dx*=speed;dy*=speed;
                Unit(x+(x<0?(corite?175:0):-(corite?175:0)),y-(corite?175:200),out hx,out hy);hx*=speed;hy*=speed;
                bool clear;if(!terrain.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(e.PlayerX,e.PlayerY,1,1),out clear,out stop))return false;
                if(n.A3>=120)clear=true;float angle=(float)Math.Atan2(dy,dx);
                clear=clear && angle>(float)Math.PI/8 && angle<(float)Math.PI-(float)Math.PI/8;
                float distance=Length(x,y);bool near=distance<(corite?80:0),far=distance>(corite?600:750);
                if(near || far || !clear)
                {
                    float smooth=corite?60:30;n.Vx=(n.Vx*(smooth-1)+hx)/smooth;n.Vy=(n.Vy*(smooth-1)+hy)/smooth;
                    if(!clear){if(!near && !far)n.A3++;}else n.A3=0;
                }
                else{n.A0=1;n.A2=dx;n.A3=dy;}
            }
            else if(n.A0==1)
            {
                bool brake=true;
                if(corite)
                {
                    brake=Length(n.Vx,n.Vy)>2;
                    if(!brake && n.Target>=0 && !n.PlayerNativeDead)
                    {float dx,dy;SafeUnit(x,y,0,0,out dx,out dy);dx*=.1f;dy*=.1f;n.Vx=n.Vx+(dx-n.Vx)*.25f;n.Vy=n.Vy+(dy-n.Vy)*.25f;}
                }
                if(brake){n.Vx*=corite?.75f:.95f;n.Vy*=corite?.75f:.95f;}
                n.A1++;
                if(n.A1>=(corite?20:30))
                {
                    if(!corite){stop=PredictionStop.RandomDecision;return false;}
                    n.A0=2;n.A1=0;Unit(n.A2,n.A3,out n.Vx,out n.Vy);n.Vx*=9;n.Vy*=9;
                }
            }
            else if(n.A0==2)
            {
                n.A1++;bool passed=Length(x,y)>(corite?150:100) && n.Bounds.CenterY>e.PlayerY;
                if(n.A1>=30 && passed || Length(n.Vx,n.Vy)<7)
                {n.A0=0;n.A1=n.A2=n.A3=0;n.Vx/=2;n.Vy/=2;if(corite){n.A1=45;n.A0=4;}}
                else
                {
                    float dx,dy;SafeUnit(x,y,n.Direction,0,out dx,out dy);float speed=Length(n.Vx,n.Vy)+(corite?(4f/15f)*60:0);
                    // Vector2 operators store each product and sum before
                    // dividing; extended fused arithmetic changes this curve.
                    float vx=n.Vx*(corite?59:19),vy=n.Vy*(corite?59:19);dx*=speed;dy*=speed;vx+=dx;vy+=dy;double inverse=1d/(corite?60:20);n.Vx=(float)(vx*inverse);n.Vy=(float)(vy*inverse);
                }
                if(!corite){bool solid;if(!Solid(n,e,terrain,out solid,out stop))return false;if(solid){n.A0=3;n.A1=n.A2=n.A3=0;}}
            }
            else if(n.A0==4){n.A1-=3;if(n.A1<=0){n.A0=0;n.A1=0;}n.Vx*=.95f;n.Vy*=.95f;}
            if(!corite && n.A0!=3 && Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY)<64){n.A0=3;n.A1=n.A2=n.A3=0;}
            if(n.A0==3)
            {
                n.X=n.Bounds.CenterX;n.Y=n.Bounds.CenterY;n.Width=n.Height=192;n.X-=(int)n.Width/2;n.Y-=(int)n.Height/2;n.Vx=n.Vy=0;n.Alpha=255;n.A1++;
                if(n.A1>=3){n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;}
            }
            if(float.IsNaN(n.Vx) || float.IsNaN(n.Vy) || float.IsInfinity(n.Vx) || float.IsInfinity(n.Vy)){stop=PredictionStop.InvalidState;return false;}return true;
        }
        private static bool Solid(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,out bool solid,out PredictionStop stop)
        {
            solid=false;stop=PredictionStop.None;if(terrain==null){stop=PredictionStop.MissingDependency;return false;}
            if(e.WorldWidth<1 || e.WorldHeight<=40){stop=PredictionStop.MissingDependency;return false;}
            int left=Math.Max(0,Math.Min(e.WorldWidth-1,(int)(n.X/16)-1)),right=Math.Max(0,Math.Min(e.WorldWidth-1,(int)((n.X+n.Width)/16)+2)),top=Math.Max(0,Math.Min(e.WorldHeight-40,(int)(n.Y/16)-1)),bottom=Math.Max(0,Math.Min(e.WorldHeight-40,(int)((n.Y+n.Height)/16)+2));
            for(int x=left;x<right;x++)for(int y=top;y<bottom;y++)
            {PredictionTile cell;if(!terrain.Tile(x,y,out cell,out stop))return false;if(!cell.Active || !cell.Solid || cell.SolidTop)continue;float ty=y*16,height=16;if(cell.Half){ty+=8;height-=8;}if(n.X+n.Width>x*16 && n.X<x*16+16 && n.Y+n.Height>ty && n.Y<ty+height){solid=true;return true;}}
            return true;
        }
        private static float Length(float x,float y){return (float)Math.Sqrt((double)x*x+(double)y*y);}
        private static void Unit(float x,float y,out float dx,out float dy){float length=Length(x,y);double inverse=1.0/(double)length;dx=(float)((double)x*inverse);dy=(float)((double)y*inverse);}
        private static void SafeUnit(float x,float y,float fallbackX,float fallbackY,out float dx,out float dy){Unit(x,y,out dx,out dy);if(float.IsNaN(dx) || float.IsNaN(dy)){dx=fallbackX;dy=fallbackY;}}
    }
}
