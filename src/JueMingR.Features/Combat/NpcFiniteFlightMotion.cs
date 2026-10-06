using System;
using JueMingR.Platform.Combat;
namespace JueMingR.Features.Combat
{
    // Locked 1.4.5.8 AI10/22/49/50 movement only. Attack births do not alter
    // these velocities; unknown AI694 branch choice ends before that action.
    // Tile scans use the existing finite terrain owner, never the live world.
    internal static class NpcFiniteFlightMotion
    {
        internal static bool Known(NpcMotionState n)
        {
            int t=n.EffectiveType;
            return n.Style==50 && (t==261 || t==265) || n.Style==49 && t==250 || n.Style==10 && (t==34 || t==289 || t==694) || n.Style==22 && (t==75 || t==82 || t==122 || t==169 || t==182 || t==268 || t==316 || t==330 || t==490 || t==253);
        }
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {
            if(n.Style==10)return n.EffectiveType!=694 || !n.HasPlayer || n.PlayerDead;
            if(n.Style!=22)return true;
            int t=n.EffectiveType;bool far=n.PlayerDead || Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY)>3000;
            if(t==316 && far)return n.A3!=1;
            if(t==330 && !e.PumpkinMoon || t==253 && !e.Eclipse || t==490 && e.Day)return false;
            return n.A2>=0 || t==253 || t==330;
        }
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {if(n.Style==22 && n.EffectiveType==316 && n.A3==1 && (n.PlayerDead || Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY)>3000))return false;return n.Style!=10 || n.EffectiveType!=694 || n.A3!=3 && n.A3!=4;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {
            if(a.Style==10)return a.EffectiveType!=694 || a.A3==b.A3;
            // AI22 ai0/ai1 are remembered positions, ai2 is a stuck timer;
            // AI49 ai0 is a firing timer, AI50 has no movement phase there.
            return a.Style!=22 || (a.A2<0)==(b.A2<0);
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;
            if(n.Style!=22 && Retargets(n,e)){NpcTargeting.Face(ref n,ref e,true,confused);}
            e=NpcTargeting.Player(n,e);
            if(n.Style==50)
            {
                n.TimeLeft=Math.Min(n.TimeLeft,5);n.NoTileCollide=type!=261;
                if(type==261 && (n.CollideX || n.CollideY)){n.Active=false;stop=PredictionStop.Despawn;return false;}
                n.Vy+=.02f;if(n.Vy<0 && e.PlayerY-e.PlayerHeight/2>n.Y+100)n.Vy*=.95f;n.Vy=Math.Min(1,n.Vy);
                int side=n.X+n.Width<e.PlayerX-e.PlayerWidth/2?1:n.X>e.PlayerX+e.PlayerWidth/2?-1:0;
                if(side!=0){if(n.Vx*side<0){n.Vx*=.98f;if(e.Expert && n.Vx*side<0)n.Vx*=.98f;}n.Vx+=side*(e.Expert?.2f:.1f);}
                if(Math.Abs(n.Vx)>5)n.Vx*=.97f;return true;
            }
            if(n.Style==49)
            {
                n.NoGravity=true;float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY-200,d=Length(x,y);
                if(d<20){x=n.Vx;y=n.Vy;}else{x*=4/d;y*=4/d;}
                ReverseAxis(ref n.Vx,x,.25f);ReverseAxis(ref n.Vy,y,.25f);return true;
            }
            if(n.Style==10)return Spirit(ref n,e,out stop);
            return Hover(ref n,e,terrain,confused,out stop);
        }
        private static bool Spirit(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;bool dungeon=n.EffectiveType==694;float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY,d=Length(x,y);
            if(n.A3!=3)n.A1++;
            if(n.A3==3){n.Vx=n.Vy=0;if(n.JustHit)n.A3=4;return true;}
            if(n.A3==4){n.Vx=n.Vy=0;if(n.A1>80){n.A1=0;n.A3=0;}return true;}
            bool dash=n.A2>=0 && n.A3==2,shot=n.A2>=0 && n.A3==1,fast=n.A1>600,escape=dungeon && !dash && !shot && !fast && n.A1< -30;
            float speed=1,acc=.011f;
            if(!dash)
            {
                if(fast){speed=4;acc*=8;if(n.A1>650)n.A1=0;}
                else if(dungeon && d<100 && n.A1>=0)n.A1=-60;
                else if(d<250){n.A0+=.9f;n.Vy+=n.A0>0?.019f:-.019f;n.Vx+=n.A0< -100 || n.A0>100?.019f:-.019f;if(n.A0>200)n.A0=-200;}
            }
            if(escape){speed=8;acc=.25f;}else if(d>350){speed=5;acc=.3f;}else if(d>300){speed=3;acc=.2f;}else if(d>250){speed=1.5f;acc=.1f;}
            if(d>.0001f){x*=speed/d;y*=speed/d;}else{x=0;y=0;}
            if(escape){x=-x;y=-y;}if(e.PlayerDead){x=n.Direction*speed/2;y=-speed/2;}
            if(dash)
            {
                if(n.A2<10){n.Vx*=.5f;n.Vy*=.5f;}
                else{float vx=n.Vx,vy=n.Vy,v=Length(vx,vy);if(v<.1f){vx=e.PlayerX-n.Bounds.CenterX;vy=e.PlayerY-n.Bounds.CenterY;v=Length(vx,vy);}if(v<.0001f){vx=0;vy=1;v=1;}n.Vx=vx/v*14;n.Vy=vy/v*14;}
            }
            else{Axis(ref n.Vx,x,acc);Axis(ref n.Vy,y,acc);}
            if(n.EffectiveType==289)
            {if(n.JustHit)n.A2=n.A3=0;if(d<=500){n.A2++;if(n.A3==0 && n.A2>120){n.A2=0;n.A3=1;}else if(n.A3!=0 && n.A2>40)n.A3=0;}else n.A2=n.A3=0;return true;}
            if(!dungeon || e.Multiplayer)return true;
            if(n.JustHit)n.A2=n.A3=0;
            float decisionDistance=Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY-10);
            bool chooseDash=decisionDistance>=100 && decisionDistance<=300 && n.A2>=0 && (n.A3==0 || n.A3==2);
            bool chooseShot=decisionDistance<=500 && n.A2>=0 && (n.A3==0 || n.A3==1);
            if(n.A3==0 && (chooseDash || chooseShot) && n.A2+1>120){stop=chooseDash && chooseShot?PredictionStop.RandomDecision:PredictionStop.PhaseBoundary;return false;}
            if(chooseDash){n.A2++;if(n.A3==2 && n.A2>60){n.A2=-300;n.A3=0;}}
            else if(chooseShot){n.A2++;if(n.A3==1 && n.A2>30)n.A2=n.A3=0;}
            else{n.A2=Math.Min(0,n.A2+1);n.A3=0;}return true;
        }
        private static bool Hover(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int t=n.EffectiveType;bool descending=false,leaving=t==330 && !e.PumpkinMoon || t==253 && !e.Eclipse || t==490 && e.Day;
            if(n.JustHit)n.A2=0;
            if(t==316 && (e.PlayerDead || Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY)>3000))
            {leaving=true;descending=n.A3!=1;if(n.A3==1){n.Alpha=Math.Min(255,n.Alpha+6);if(n.Alpha>=255){stop=PredictionStop.Despawn;return false;}}else n.TimeLeft=Math.Min(n.TimeLeft,10);}
            if(leaving && n.Vx==0){stop=PredictionStop.RandomDecision;return false;}
            if(!leaving)
            {
                if(n.A2>=0)
                {bool stuck=(Math.Abs(n.X-n.A0)<16 || n.Vx*n.Direction<0) && Math.Abs(n.Y-n.A1)<40;if(stuck){n.A2++;if(n.A2>=60){n.A2=-200;n.Direction=-n.Direction;n.Vx=-n.Vx;n.CollideX=false;}}else{n.A0=n.X;n.A1=n.Y;n.A2=0;}NpcTargeting.Face(ref n,ref e,true,confused);}
                else if(t==253){NpcTargeting.Face(ref n,ref e,true,confused);n.A2+=2;}
                else{n.A2+=t==330?.1f:1;n.Direction=e.PlayerX>n.Bounds.CenterX?-1:1;}
            }
            int depth=t==122?8:t==75?4:t==169?10:t==268?(e.PlayerY<n.Bounds.CenterY?12:6):t==490?4+(int)Math.Min(8,Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY)/70):3;
            int x=(int)(n.Bounds.CenterX/16)+n.Direction*2,y=(int)((n.Y+n.Height)/16);bool fall=true,near=false;
            // 169 overwrites horizontal facing after the scan column was fixed.
            if(t==169)n.Direction=e.PlayerX>n.Bounds.CenterX?1:-1;
            if(n.Y+n.Height>e.PlayerY-e.PlayerHeight/2)
            {if(t==330)fall=false;else for(int j=y;j<y+depth;j++){PredictionTile cell;if(!terrain.Tile(x,j,out cell,out stop))return false;if(cell.Active && cell.Solid || cell.Liquid>0){near=j<=y+1;fall=false;break;}}}
            if(n.TargetNoAggro)
            {bool support=false;for(int j=y;j<y+depth-2;j++){PredictionTile cell;if(!terrain.Tile(x,j,out cell,out stop))return false;if(cell.Active && cell.Solid || cell.Liquid>0){support=true;break;}}n.DirectionY=support?-1:1;}
            if(t==169 || t==268)for(int j=y-3;j<y;j++){PredictionTile cell;if(!terrain.Tile(x,j,out cell,out stop))return false;if(cell.Active && cell.Solid && !cell.Platform || cell.Liquid>0){near=false;descending=true;break;}}
            if(descending){near=false;fall=true;if(t==268)n.Vy+=2;}
            if(fall){float a=t==75 || t==169?.2f:t==490?.03f:t==316 && leaving?.05f:.1f;float max=t==75 || t==169?2:t==490?.75f:t==316 && leaving?6:3;n.Vy=Math.Min(max,n.Vy+a);}
            else{if((t==75 || t==169 || t==490) && (n.DirectionY<0 && n.Vy>0 || near))n.Vy-=t==490?.075f:.2f;else if(t!=75 && t!=169 && t!=490 && n.DirectionY<0 && n.Vy>0)n.Vy-=.1f;n.Vy=Math.Max(t==490?-.75f:-4,n.Vy);}
            if(t==75 && n.Wet)n.Vy=Math.Max(-2,n.Vy-.2f);
            if(n.CollideX){n.Vx=n.OldVx*-.4f;if(n.Direction==-1 && n.Vx>0 && n.Vx<1)n.Vx=1;if(n.Direction==1 && n.Vx<0 && n.Vx> -1)n.Vx=-1;}
            if(n.CollideY){n.Vy=n.OldVy*-.25f;if(n.Vy>0 && n.Vy<1)n.Vy=1;if(n.Vy<0 && n.Vy> -1)n.Vy=-1;}
            float sx=t==75?3:t==253 || t==330?4:t==490?1.5f:2;
            if(t==330){if(!leaving)NpcTargeting.Face(ref n,ref e,true,confused);else n.TimeLeft=Math.Min(n.TimeLeft,10);if(n.Vx*n.Direction<0)n.Vx*=.9f;}
            if(n.Direction==-1 && n.Vx> -sx){n.Vx-=.1f;if(n.Vx>sx)n.Vx-=.1f;else if(n.Vx>0)n.Vx+=.05f;n.Vx=Math.Max(-sx,n.Vx);}
            else if(n.Direction==1 && n.Vx<sx){n.Vx+=.1f;if(n.Vx< -sx)n.Vx+=.1f;else if(n.Vx<0)n.Vx-=.05f;n.Vx=Math.Min(sx,n.Vx);}
            float sy=t==490?1:1.5f;
            if(n.DirectionY==-1 && n.Vy> -sy){n.Vy-=.04f;if(n.Vy>sy)n.Vy-=.05f;else if(n.Vy>0)n.Vy+=.03f;n.Vy=Math.Max(-sy,n.Vy);}
            else if(n.DirectionY==1 && n.Vy<sy){n.Vy+=.04f;if(n.Vy< -sy)n.Vy+=.05f;else if(n.Vy<0)n.Vy-=.03f;n.Vy=Math.Min(sy,n.Vy);}return true;
        }
        private static float Length(float x,float y){return (float)Math.Sqrt(x*x+y*y);}
        private static void Axis(ref float v,float target,float acceleration){if(v<target)v+=acceleration;else if(v>target)v-=acceleration;}
        private static void ReverseAxis(ref float v,float target,float acceleration){if(v<target){v+=acceleration;if(v<0 && target>0)v+=2*acceleration;}else if(v>target){v-=acceleration;if(v>0 && target<0)v-=2*acceleration;}}
    }
}
