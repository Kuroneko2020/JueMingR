using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Ordinary fighter decisions consume immutable tile values, never a live
    // NPC, WorldGen operation or RNG. Support is sampled before StepUp, as in
    // native AI; rechecking it afterwards changes obstacle and pit decisions.
    internal static class NpcGroundMotion
    {
        internal static bool Known(int type){return type==3 || type==21 || type==27 || type==109 || type==120 || type==166;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.Identity.Type;
            if(e.PlayerY+e.PlayerHeight/2==n.Y+n.Height)n.DirectionY=-1;
            if(type==166 && n.A2<0)
            {
                Target(ref n,e,confused);bool clear;
                if(!t.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(e.PlayerX,e.PlayerY,1,1),out clear,out stop))return false;
                if(n.JustHit || clear)n.A2=0;
                else{n.Vx*=.9f;if(n.Vx>-.1f && n.Vx<.1f)n.Vx=0;if(++n.A2==0)n.Vx=n.Direction*.1f;return true;}
            }
            bool wasStopped=n.Vx==0 && !n.JustHit;int limit=type==120?180:60;
            if(type==120 && n.A3==-120){n.Vx=n.Vy=0;n.A3=0;}
            if(n.X==n.OldX || n.A3>=limit || n.Vy==0 && (n.Vx>0 && n.Direction<0 || n.Vx<0 && n.Direction>0))n.A3++;
            else if(Math.Abs(n.Vx)>.9f && n.A3>0)n.A3--;
            if(n.A3>limit*10 || n.JustHit || Intersects(n,e))n.A3=0;
            bool pursue=type==120 || e.Eclipse || !e.Day || e.Remix || n.SpawnedFromStatue || n.Y>e.WorldSurface*16 || e.Graveyard || type==27 && e.InvasionType==1;
            if(n.A3<limit && pursue)
            {Target(ref n,e,confused);if(n.DirectionY>0 && e.PlayerY<=n.Y+n.Height)n.DirectionY=-1;}
            else if(!(type==166 && n.A2>0))
            {if(e.Day && !e.Remix && n.Y/16<e.WorldSurface)n.TimeLeft=Math.Min(n.TimeLeft,10);if(n.Vx==0){if(n.Vy==0 && ++n.A0>=2){n.Direction*=-1;n.SpriteDirection=n.Direction;n.A0=0;}}else n.A0=0;if(n.Direction==0)n.Direction=1;}
            float speed=type==3?2-n.Scale:type==21?1.5f*(2-n.Scale):type==27 || type==109?2:3,acc=type==109?.04f:.07f;
            if(n.Vx<-speed || n.Vx>speed){if(n.Vy==0){n.Vx*=.8f;n.Vy*=.8f;}}
            else
            {
                if((type==120 || type==166) && n.Vy==0 && (n.Vx>0 && n.Direction<0 || n.Vx<0 && n.Direction>0))n.Vx*=.99f;
                if(n.Direction==1)n.Vx=Math.Min(speed,n.Vx+acc);else if(n.Direction==-1)n.Vx=Math.Max(-speed,n.Vx-acc);
            }
            bool supported=false;
            if(n.Vy==0)
            {
                int row=(int)(n.Y+n.Height+7)/16,head=(int)(n.Y-9)/16;
                for(int x=(int)(n.X+8)/16;x<=(int)(n.X+n.Width-8)/16;x++)
                {PredictionTile foot,ceiling;if(!t.Tile(x,row,out foot,out stop) || !t.Tile(x,head,out ceiling,out stop))return false;if(ceiling.SolidNoPlatform){supported=false;break;}if(foot.Active && foot.Solid)supported=true;}
            }
            if(n.Vy>=0 && !StepUp(ref n,t,out stop))return false;
            if(supported)
            {
                int x=(int)((n.X+n.Width/2+(type==109?n.Width/2+16:15)*n.Direction)/16),y=(int)((n.Y+n.Height-15)/16);
                PredictionTile cell,one,two,three;
                if(!t.Tile(x,y,out cell,out stop) || !t.Tile(x,y-1,out one,out stop) || !t.Tile(x,y-2,out two,out stop) || !t.Tile(x,y-3,out three,out stop))return false;
                if((type==3 || type==21 || type==27) && one.Active && (one.Type==10 || one.Type==388))
                {
                    n.A3=0;if(++n.A2>=60)
                    {
                        // Ordinary repeated knocking is deterministic. A
                        // graveyard roll or an actual world-changing opening
                        // ends the local fixed-terrain forecast at that step.
                        if(e.Graveyard){stop=PredictionStop.RandomDecision;return false;}
                        if((!e.BloodMoon || e.GoodWorld) && (type==3 || type==21) && !e.PlayerProtected)n.A1=0;
                        n.Vx=-.5f*n.Direction;n.A1+=one.Type==388?2:5;n.A1+=e.PlayerProtected?6:type==27?1:0;n.A2=0;
                        if(n.A1>=10){n.A1=10;if(!e.Multiplayer){stop=PredictionStop.PhaseBoundary;return false;}}
                    }
                }
                else
                {
                    if(n.Vx<0 && n.SpriteDirection==-1 || n.Vx>0 && n.SpriteDirection==1)
                    {
                        PredictionTile down,forward;
                        if(!t.Tile(x,y+1,out down,out stop) || !t.Tile(x+n.Direction,y+1,out forward,out stop))return false;
                        if(n.Height>=32 && two.SolidNoPlatform)n.Vy=three.SolidNoPlatform?-8:-7;
                        else if(one.SolidNoPlatform)n.Vy=-6;
                        else if(n.Y+n.Height-y*16>20 && !cell.TopSlope && cell.SolidNoPlatform)n.Vy=-5;
                        else if(n.DirectionY<0 && !down.SolidBottomSlope && !forward.SolidBottomSlope){n.Vy=-8;n.Vx*=1.5f;}
                        else if(type==3 || type==21 || type==27)n.A1=n.A2=0;
                    if(n.Vy==0 && wasStopped && n.A3==1)n.Vy=-5;
                    if(n.Vy==0 && e.Expert && e.PlayerY+e.PlayerHeight/2<n.Y && Math.Abs(n.Bounds.CenterX-e.PlayerX)<e.PlayerWidth*3)
                    {
                        bool clear;if(!t.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
                        if(clear)
                        {
                            bool platform=n.Y-(e.PlayerY+e.PlayerHeight/2)<96;
                            for(int dy=0;dy<6 && !platform;dy++){PredictionTile p;if(!t.Tile((int)(n.Bounds.CenterX/16),(int)((n.Y+n.Height)/16)-1-dy,out p,out stop))return false;platform=p.Active && p.Platform;}
                            if(platform)n.Vy=-7.9f;
                        }
                    }
                    }
                    if(type==120 && n.Vy<0)n.Vy*=1.1f;
                }
            }
            else if(type==3 || type==21 || type==27)n.A1=n.A2=0;
            if(type==120 && !e.Multiplayer && n.A3>=180){stop=PredictionStop.RandomDestination;return false;}
            return true;
        }
        internal static bool StepUp(ref NpcMotionState n,IPredictionTerrain t,out PredictionStop stop,bool platforms=false)
        {
            stop=PredictionStop.None;int direction=Math.Sign(n.Vx),x=(int)((n.X+n.Vx+n.Width/2+(n.Width/2+1)*direction)/16),y=(int)((n.Y+n.Height-1)/16);
            PredictionTile c,a,b,d,f,behind;
            if(!t.Tile(x,y,out c,out stop) || !t.Tile(x,y-1,out a,out stop) || !t.Tile(x,y-2,out b,out stop) || !t.Tile(x,y-3,out d,out stop) || !t.Tile(x,y-4,out f,out stop) || !t.Tile(x-direction,y-3,out behind,out stop))return false;
            if(n.X+n.Vx+n.Width<=x*16 || n.X+n.Vx>=x*16+16 ||
                !(c.Active && !c.TopSlope && !a.TopSlope && (c.Solid && !c.SolidTop || platforms && c.SolidTop && (!a.Solid || !a.Active) && c.Type!=16 && c.Type!=18 && c.Type!=134) || a.Half && a.Active) ||
                !(Pass(a) || a.Half && Pass(f)) || !Pass(b) || !Pass(d) || behind.Active && behind.Solid && (!platforms || !behind.SolidTop))return true;
            float surface=y*16+(c.Half?8:0)-(a.Half?8:0),rise=n.Y+n.Height-surface;
            if(surface<n.Y+n.Height && rise<=16.1f)n.Y=surface-n.Height;
            return true;
        }
        private static bool Pass(PredictionTile t){return !t.Active || !t.Solid || t.SolidTop;}
        private static bool Intersects(NpcMotionState n,PredictionEnvironment e)
        {return (int)n.X<(int)(e.PlayerX-e.PlayerWidth/2)+(int)e.PlayerWidth && (int)n.X+n.Width>(int)(e.PlayerX-e.PlayerWidth/2) && (int)n.Y<(int)(e.PlayerY-e.PlayerHeight/2)+(int)e.PlayerHeight && (int)n.Y+n.Height>(int)(e.PlayerY-e.PlayerHeight/2);}
        private static void Target(ref NpcMotionState n,PredictionEnvironment e,bool confused)
        {if(!e.PlayerDead && !(n.TargetNoAggro && n.Direction!=0) && !(e.PlayerIdleWithNegativeAggro && n.Target>=0 && n.Target<255 && !n.Boss)){n.Direction=(int)(e.PlayerX-e.PlayerWidth/2)+(int)e.PlayerWidth/2<n.X+n.Width/2?-1:1;n.DirectionY=(int)(e.PlayerY-e.PlayerHeight/2)+(int)e.PlayerHeight<=n.Y+n.Height?-1:1;}if(confused)n.Direction=-n.Direction;}
        internal static void AfterMove(ref NpcMotionState n)
        {if(n.Vy==0 && (n.Direction==-1 || n.Direction==1) && (n.Identity.Type!=109 || n.Vx==0 || n.Vx*n.Direction>0))n.SpriteDirection=n.Direction;}
    }
}
