using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked .8 AI_007: these six selectable TownCritter variants are not
    // townNPC/isLikeATownNPC. Town attacks, home teleport and falling avoidance
    // therefore do not apply. Random clocks/direction choices are boundaries,
    // while already observed clocks and ordinary collision motors continue.
    internal static class NpcTownCritterMotion
    {
        internal static bool Known(NpcMotionState n)
        {int t=n.EffectiveType;return n.Style==7 && (t==230 || t==303 || t==337 || t==540 || t==625 || t==687);}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return n.Target==255 || n.EffectiveType==230 || TalkPlayer(n,e)>=0 || StinkyPlayer(e)>=0;}
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e)
        {int talk=TalkPlayer(n,e),stinky=StinkyPlayer(e);return talk>=0?talk:stinky>=0?stinky:n.Target==255 && n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;}
        private static int StinkyPlayer(PredictionEnvironment e)
        {if(e.Players==null || !e.Players.TownCaptured)return -1;for(int i=0;i<e.Players.Count;i++)if(!e.Players.SearchDeadAt(i) && e.Players.StinkyAt(i))return e.Players.SlotAt(i);return -1;}
        private static int TalkPlayer(NpcMotionState n,PredictionEnvironment e)
        {if(n.A0==24 || e.Players==null || !e.Players.TownCaptured)return -1;int result=-1;for(int i=0;i<e.Players.Count;i++)if(e.Players.TalkNpcAt(i)==n.Identity.Slot)result=e.Players.SlotAt(i);return result;}
        private static bool Fail(PredictionStop reason,out PredictionStop stop){stop=reason;return false;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n))return Fail(PredictionStop.UnsupportedMechanism,out stop);
            if(!n.TownFactsCaptured || !n.TownRelationsCaptured || !n.TownRelationsComplete || !e.TownWeatherCaptured || e.Players==null || !e.Players.TownCaptured)return Fail(PredictionStop.MissingDependency,out stop);
            if(n.Town)return Fail(PredictionStop.UnsupportedMechanism,out stop);
            int type=n.EffectiveType;bool waterBreath=type==625 || type==687;
            if(n.Target==255)
            {
                NpcTargeting.Face(ref n,ref e,true,confused);
                if(!n.HasPlayer || e.PlayerTimelineActive && n.PlayerIndex!=e.PlayerIndex)return Fail(PredictionStop.MissingDependency,out stop);
                var player=NpcTargeting.Area(n,e);if(n.X<player.X)n.Direction=1;if(n.X>player.X)n.Direction=-1;n.SpriteDirection=n.Direction;
                if(n.TownHomeTileX==-1)n.TownHomeTileX=(int)((n.X+(int)n.Width/2)/16);
            }
            else if(n.TownHomeTileX==-1 && n.HomeTileY==-1 && n.Vy==0 && !n.Shimmer)
            {n.TownHomeTileX=(int)n.Bounds.CenterX/16;n.HomeTileY=(int)(n.Y+n.Height+4)/16;}
            int myX=(int)(n.X+(int)n.Width/2)/16,myY=(int)(n.Y+n.Height+1)/16,floorX,floorY;
            if(!Rest(n,e,terrain,group,count,elapsed,myX,myY,out floorX,out floorY,out stop))return false;
            n.DirectionY=-1;if(n.Direction==0)n.Direction=1;
            bool talk=false;int talkDirection=0;
            if(n.A0!=24)for(int i=0;i<e.Players.Count;i++)if(e.Players.TalkNpcAt(i)==n.Identity.Slot)
            {
                talk=true;if(n.A0==10 || n.A0==12 || n.A0==14 || n.A0==15)continue;
                int slot=e.Players.SlotAt(i);if(elapsed>0 && e.PlayerTimelineActive && slot!=e.PlayerIndex)return Fail(PredictionStop.MissingDependency,out stop);
                var p=PlayerArea(e,i);n.A0=0;n.A1=300;n.L3=100;talkDirection=p.X+(int)p.Width/2<n.X+(int)n.Width/2?-1:1;
            }
            if(n.A3==1){n.Life=-1;n.Active=false;return Fail(PredictionStop.Despawn,out stop);}
            PredictionTile under;if(!Tile(terrain,e,myX,myY,out under,out stop))return false;
            if(under.Type==666 && !e.Multiplayer && !n.TownStinkyImmune)n.TownStinkyTicks=Math.Max(n.TownStinkyTicks,180);
            bool danger=false;int away=0;
            if(type!=625 && !e.Multiplayer && !Danger(n,e,terrain,group,count,elapsed,talk,out danger,out away,out stop))return false;
            if(danger)
            {
                if(n.A0==8 && n.Direction==away)return Fail(PredictionStop.RandomDecision,out stop);
                if(n.A0!=10 && n.A0!=12 && n.A0!=13 && n.A0!=14 && n.A0!=15)
                {if(n.A0!=1)return Fail(PredictionStop.RandomDecision,out stop);n.Direction=away;}
            }
            bool weather=e.Raining || !e.Day || e.Eclipse || e.SlimeRain || e.Storming && (double)(n.Y/16)<e.GravityWorldSurface;
            if(n.A0==0)
            {
                if(talk && talkDirection!=0 && !danger)n.Direction=talkDirection;
                if(n.TownStinky)n.A0=1; // Same native branch still executes idle motor this action.
                if(n.L3>0)n.L3--;
                if(type==687 && n.Wet)return Fail(PredictionStop.RandomDecision,out stop);
                if(n.Vx>.1f)n.Vx-=.1f;else if(n.Vx<-.1f)n.Vx+=.1f;else n.Vx=0;
                if(!e.Multiplayer)
                {
                    if(n.A1>0)n.A1--;
                    if(n.Wet && !waterBreath){bool drowning;if(!Drown(n,e,terrain,out drowning,out stop))return false;if(drowning)return Fail(PredictionStop.RandomDecision,out stop);}
                    if(n.A1<=0)return Fail(PredictionStop.RandomDecision,out stop);
                    bool rest=myX==floorX && myY==floorY;
                    if(!weather || rest)
                    {if(myX<floorX-25 || myX>floorX+25){if(n.L3==0){if(myX<floorX-50 && n.Direction==-1)n.Direction=1;else if(myX>floorX+50 && n.Direction==1)n.Direction=-1;}}else if(n.L3==0)return Fail(PredictionStop.RandomDecision,out stop);}
                }
            }
            else if(n.A0==1)
            {
                bool drowning=false;if(!waterBreath && !Drown(n,e,terrain,out drowning,out stop))return false;
                if(!drowning)
                {
                    bool dungeon=under.Type==41 || under.Type==43 || under.Type==44 || under.Type==677 || under.Type==678;
                    if(!e.Multiplayer && !n.TownHomeless && !dungeon && (myX<floorX-35 || myX>floorX+35) && (n.X<floorX*16 && n.Direction==-1 || n.X>floorX*16 && n.Direction==1))n.A1-=5;
                    n.A1--; // This decrement also runs on clients.
                }
                if(n.A1<=0)return Fail(PredictionStop.RandomDecision,out stop);
                if(n.TownCloseDoor && (n.Bounds.CenterX/16>n.TownDoorX+2 || n.Bounds.CenterX/16<n.TownDoorX-2))return Fail(PredictionStop.PhaseBoundary,out stop);
                float speed=1,acceleration=.07f;
                if(type==625){speed=n.Wet?2.5f:.2f;acceleration=n.Wet?1:.07f;}
                if(n.Friendly && (danger || drowning)){speed=1.5f+(1-(float)n.Life/n.LifeMax)*.9f;acceleration=.1f;}
                if(type==687 && n.Wet){if(Math.Abs(n.Vx)<.05f && Math.Abs(n.Vy)<.05f)n.Vx+=speed*10*n.Direction;else n.Vx*=.9f;}
                else if(n.Vx<-speed || n.Vx>speed){if(n.Vy==0){n.Vx*=.8f;n.Vy*=.8f;}}
                else if(n.Vx<speed && n.Direction==1){n.Vx+=acceleration;if(n.Vx>speed)n.Vx=speed;}
                else if(n.Vx>-speed && n.Direction==-1){n.Vx-=acceleration;if(n.Vx>speed)n.Vx=speed;}
                bool platforms=!(n.HomeTileY*16-32>n.Y);
                if(!platforms && n.Vy==0 && !StepDown(ref n,e,terrain,out stop))return false;
                if(n.Vy>=0 && !StepUp(ref n,e,terrain,platforms,out stop))return false;
                if(n.Vy==0)
                {
                    if(n.X==n.L3){n.Direction*=-1;n.L3=180;}
                    if(drowning){if(n.L3>180)n.L3=180;if(n.L3>0)n.L3--;}else n.L3=-1;
                    if(n.Vx*n.Direction>0 && !Jump(ref n,e,terrain,danger,drowning,out stop))return false;
                }
                if(n.OldX==n.X && n.OldY==n.Y && n.OldVx==n.Vx && n.OldVy==n.Vy+n.Gravity && !danger)
                {float old=n.L2++;if(old>=30){n.L2=0;if(!e.Multiplayer)return Fail(PredictionStop.RandomDecision,out stop);}}
            }
            else if(n.A0==2 || n.A0==11)
            {
                if(!e.Multiplayer){n.L3--;if(n.L3==0)return Fail(PredictionStop.RandomDecision,out stop);}
                n.A1--;n.Vx*=.8f;if(n.A1<=0)return Fail(PredictionStop.RandomDecision,out stop);
            }
            else if(n.A0==3 || n.A0==4 || n.A0==5 || n.A0==8 || n.A0==9 || n.A0==16 || n.A0==17 || n.A0>=20 && n.A0<=23)
            {
                n.Vx*=.8f;n.A1--;if(n.A0==8 && n.A1<60 && danger)n.A1=180;
                if(n.A0==5){PredictionTile chair;if(!Tile(terrain,e,(int)n.Bounds.CenterX/16,(int)(n.Y+n.Height-2)/16,out chair,out stop))return false;if(chair.Type!=15 && chair.Type!=497)n.A1=0;}
                if(n.A1<=0)return Fail(PredictionStop.RandomDecision,out stop);
            }
            else return Fail(PredictionStop.UnsupportedMechanism,out stop);
            if(waterBreath && n.Wet)
            {
                // AI007's floating tail applies after every observed clock
                // branch, including idle. It uses GetWaterLine's four cells,
                // not DrownCollision or a permanently captured surface.
                if(type==687)n.A1=50;
                int column=(int)(n.Bounds.CenterX/16);
                if(n.CollideX || column<5 && n.Direction==-1 || column>e.WorldWidth-5 && n.Direction==1){n.Direction*=-1;n.Vx*=-.25f;}
                bool found;float waterLine;
                if(!WaterLine(n,e,terrain,out found,out waterLine,out stop))return false;
                if(!found)n.Vy-=.2f;
                else
                {
                    float center=n.Bounds.CenterY+1;
                    if(n.Bounds.CenterY>waterLine){n.Vy=Math.Max(-4,n.Vy-.8f);if(center+n.Vy<waterLine)n.Vy=waterLine-center;}
                    else n.Vy=Math.Min(n.Vy,waterLine-center);
                }
            }
            return true;
        }
        private static bool WaterLine(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,out bool found,out float height,out PredictionStop stop)
        {
            found=false;height=0;stop=PredictionStop.None;int x=(int)(n.Bounds.CenterX/16),y=(int)(n.Bounds.CenterY/16);
            if(x<10 || y<10 || x>=e.WorldWidth-10 || y>=e.WorldHeight-10)return true;
            PredictionTile top,a,b,c;
            if(!t.Tile(x,y-2,out top,out stop) || !t.Tile(x,y-1,out a,out stop) || !t.Tile(x,y,out b,out stop) || !t.Tile(x,y+1,out c,out stop))return false;
            if(top.Liquid>0)return true;
            if(a.Liquid>0){height=y*16-a.Liquid/16;found=true;}
            else if(b.Liquid>0){height=(y+1)*16-b.Liquid/16;found=true;}
            else if(c.Liquid>0){height=(y+2)*16-c.Liquid/16;found=true;}
            return true;
        }
        private static MotionRect PlayerArea(PredictionEnvironment e,int i)
        {return e.PlayerTimelineActive && e.Players.SlotAt(i)==e.PlayerIndex?new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight):e.Players.SearchAreaAt(i);}
        private static bool Tile(IPredictionTerrain t,PredictionEnvironment e,int x,int y,out PredictionTile cell,out PredictionStop stop)
        {cell=default(PredictionTile);stop=PredictionStop.None;if(x<0 || y<0 || x>=e.WorldWidth || y>=e.WorldHeight)return true;return t.Tile(x,y,out cell,out stop);}
        private static bool Rest(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,NpcMotionState[] group,int count,int elapsed,int myX,int myY,out int x,out int y,out PredictionStop stop)
        {
            x=n.TownHomeTileX;y=n.HomeTileY;stop=PredictionStop.None;if(x==-1 || y==-1)return true;
            PredictionTile cell;while(y<e.WorldHeight-20){if(!Tile(t,e,x,y,out cell,out stop))return false;if(cell.Active && (cell.Solid && !cell.SolidTop || cell.Platform))break;y++;}
            if(e.Day || n.A0==5)return true;
            int baseX=x,baseY=y,best=-1;
            for(int dx=baseX-7;dx<=baseX+7;dx++)for(int dy=baseY+2;dy>=baseY-6;dy-=2)
            {if(!Tile(t,e,dx,dy,out cell,out stop))return false;if(!cell.RawActive || !cell.CanSit || !(cell.FrameY%40!=0 || dy+1<=baseY+2))continue;int distance=Math.Abs(baseX-dx)+Math.Abs(baseY-dy);if(best!=-1 && distance>=best)continue;best=distance;x=dx;y=dy;if(cell.FrameY%40!=0)y--;y+=2;}
            if(best==-1)return true;
            if(group!=null)for(int i=0;i<count;i++){var seat=group[i];if(!seat.Active || !seat.Town || seat.Style!=7 || seat.A0!=5 || (int)seat.Bounds.CenterX/16!=x || (int)(seat.Y+seat.Height-2)/16!=y)continue;
                // Seated town actors have no motion role in this closure. Never
                // freeze their occupancy beyond their observed remaining clock.
                if(elapsed>0)return Fail(PredictionStop.MissingDependency,out stop);x=baseX;y=baseY;break;}
            return true;
        }
        private static bool Danger(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,NpcMotionState[] group,int count,int elapsed,bool talk,out bool danger,out int away,out PredictionStop stop)
        {
            danger=false;away=0;stop=PredictionStop.None;float left=-1,right=-1;
            if(group!=null)for(int i=0;i<count;i++)
            {
                var other=group[i];if(!other.Active || other.Identity.Equals(n.Identity) || other.CritterTurns || other.EffectiveType==690 || (other.Friendly || !other.CanHarm) && !other.TownStinky)continue;
                if(elapsed>0 && !NpcMotion.StructuredModel(other))return Fail(PredictionStop.MissingDependency,out stop);
                float dx=other.Bounds.CenterX-n.Bounds.CenterX,dy=other.Bounds.CenterY-n.Bounds.CenterY;
                if((float)Math.Sqrt((double)dx*dx+(double)dy*dy)>=n.TownDangerRange)continue;bool clear=true;
                if(!other.NoTileCollide && !t.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,0,0),new MotionRect(other.Bounds.CenterX,other.Bounds.CenterY,0,0),out clear,out stop))return false;if(!clear)continue;
                danger=true;if(dx<0 && (left==-1 || dx>left))left=dx;if(dx>0 && (right==-1 || dx<right))right=dx;
            }
            if(!danger && !talk)for(int i=0;i<e.Players.Count;i++)
            {
                if(e.Players.SearchDeadAt(i) || !e.Players.StinkyAt(i))continue;
                if(elapsed>0 && (e.Players.StinkyTicksAt(i)<=elapsed || e.PlayerTimelineActive && e.Players.SlotAt(i)!=e.PlayerIndex))return Fail(PredictionStop.MissingDependency,out stop);
                var p=PlayerArea(e,i);float dx=p.CenterX-n.Bounds.CenterX,dy=p.CenterY-n.Bounds.CenterY;if((float)Math.Sqrt((double)dx*dx+(double)dy*dy)>=n.TownDangerRange)continue;
                
                danger=true;if(dx<0 && (left==-1 || dx>left))left=dx;if(dx>0 && (right==-1 || dx<right))right=dx;
            }
            away=left==-1?-1:right!=-1 && right<-left?-1:1;
            float distance=away==1?-left:right;if(n.TownPrettySafe>=0 && distance>n.TownPrettySafe)danger=false;
            return true;
        }
        internal static bool Drown(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,out bool drowning,out PredictionStop stop)
        {
            drowning=false;stop=PredictionStop.None;int width=Math.Min(10,(int)n.Width),height=Math.Min(12,(int)n.Height);float hx=n.X+(int)n.Width/2-width/2,hy=n.Y-2;
            int startX=Math.Max(0,(int)(n.X/16)-1),endX=Math.Min(e.WorldWidth-1,(int)((n.X+n.Width)/16)+2),startY=Math.Max(0,(int)(n.Y/16)-1),endY=Math.Min(e.WorldHeight-40,(int)((n.Y+n.Height)/16)+2);
            for(int x=startX;x<endX;x++)for(int y=startY;y<endY;y++)
            {PredictionTile c;if(!t.Tile(x,y,out c,out stop))return false;if(c.Liquid==0 || c.Lava || c.ShimmerLiquid || y==startY && c.RawActive && c.RawSolid && !c.SolidTop && !c.Half && c.Slope==0)continue;float depth=(256-c.Liquid)/32f*2,ly=y*16+depth,lh=16-(int)depth;if(hx+width>x*16 && hx<x*16+16 && hy+height>ly && hy<ly+lh){drowning=true;return true;}}
            return true;
        }
        private static bool StepDown(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,out PredictionStop stop)
        {
            stop=PredictionStop.None;float nextX=n.X+n.Vx,nextY=(float)Math.Floor((n.Y+n.Height)/16)*16-n.Height;int x0=(int)(nextX/16),x1=(int)((nextX+n.Width)/16),y0=(int)((nextY+n.Height+4)/16),rows=(int)n.Height/16+((int)n.Height%16!=0?1:0);float surface=(y0+rows)*16;
            for(int x=x0;x<=x1;x++)for(int y=y0;y<=y0+1;y++)
            {if(x<1 || y<1 || x>=e.WorldWidth-1 || y>=e.WorldHeight-1)continue;PredictionTile c;if(!t.Tile(x,y,out c,out stop))return false;if(y>=e.WorldHeight-42 || c.Active && (c.Solid || c.SolidTop)){float top=y*16+(c.Half?8:0);if(x*16+16>n.X && x*16<n.X+n.Width && y*16-1>n.Y && y*16-17<n.Y+n.Height && top<surface)surface=top;}}
            float fall=surface-(n.Y+n.Height);if(fall>7 && fall<17)n.Y=surface-n.Height;return true;
        }
        private static bool Pass(PredictionTile c){return !c.Active || !c.Solid || c.SolidTop;}
        private static bool StepUp(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool platforms,out PredictionStop stop)
        {
            stop=PredictionStop.None;int direction=Math.Sign(n.Vx),x=(int)((n.X+n.Vx+(int)n.Width/2+((int)n.Width/2+1)*direction)/16),y=(int)((n.Y+n.Height-1)/16),rows=(int)n.Height/16+((int)n.Height%16!=0?1:0);
            if(x<1 || x>=e.WorldWidth-1 || y<1 || y>=e.WorldHeight-40)return true;
            PredictionTile c,a,top,behind;if(!Tile(t,e,x,y,out c,out stop) || !Tile(t,e,x,y-1,out a,out stop) || !Tile(t,e,x,y-rows-1,out top,out stop) || !Tile(t,e,x-direction,y-rows,out behind,out stop))return false;
            bool clear=Pass(behind);for(int i=2;i<=rows;i++){PredictionTile above;if(!Tile(t,e,x,y-i,out above,out stop))return false;clear&=Pass(above);}
            float center=n.X+(int)n.Width/2;
            bool upper=Pass(a) || a.Slope==1 && center>x*16 || a.Slope==2 && center<x*16+16 || a.Half && Pass(top);
            bool ignored=c.Type==14 || c.Type==469 || c.Type==18 || c.Type==16 || c.Type==134;
            bool slope=!c.TopSlope || c.Slope==1 && center<x*16 || c.Slope==2 && center>x*16+16;
            bool support=c.Active && slope && (!c.TopSlope || n.Y+n.Height>y*16) && (c.Solid && !c.SolidTop || platforms && (c.SolidTop && c.SurfacePlatform || c.Platform || c.Type==380) && Pass(a) && !ignored) || a.Active && a.Half;
            support&=!c.SolidTop || !a.SolidTop;
            if(!clear || !upper || !support || x*16>=n.X+n.Vx+n.Width || x*16+16<=n.X+n.Vx)return true;
            float surface=y*16-(a.Half?8:0)+(a.Half?0:c.Half?8:0),rise=n.Y+n.Height-surface;if(surface<n.Y+n.Height && (double)rise<=16.1)n.Y=surface-n.Height;return true;
        }
        private static bool SolidRange(IPredictionTerrain t,PredictionEnvironment e,int x0,int x1,int y0,int y1,bool versatile,out bool solid,out PredictionStop stop)
        {
            solid=false;stop=PredictionStop.None;int start=versatile?Math.Min(x0,x1):x0,end=versatile?Math.Max(x0,x1):x1;if(start<0 || end>=e.WorldWidth || y0<0 || y1>=e.WorldHeight-40){solid=true;return true;}
            for(int x=start;x<=end;x++)for(int y=y0;y<=y1;y++){PredictionTile c;if(!Tile(t,e,x,y,out c,out stop))return false;if(c.Active && c.Solid && !c.SolidTop){solid=true;return true;}}return true;
        }
        private static bool Jump(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool danger,bool drowning,out PredictionStop stop)
        {
            stop=PredictionStop.None;int x=(int)((n.X+(int)n.Width/2+15*n.Direction)/16),y=(int)((n.Y+n.Height-16)/16);PredictionTile lower,one,two;
            if(!Tile(t,e,x,y,out lower,out stop) || !Tile(t,e,x,y-1,out one,out stop) || !Tile(t,e,x,y-2,out two,out stop))return false;
            bool block=false,blocked=false,behind=false,ceiling=false;float jump=0;
            if(two.Active && two.Solid && !two.SolidTop && one.Active && one.Solid && !one.SolidTop)
            {if(!SolidRange(t,e,x-n.Direction*2,x-n.Direction,y-5,y-1,true,out behind,out stop) || !SolidRange(t,e,x,x,y-5,y-3,false,out ceiling,out stop))return false;jump=-6;block=true;}
            else if(one.Active && one.Solid && !one.SolidTop)
            {if(!SolidRange(t,e,x-n.Direction*2,x-n.Direction,y-4,y-1,true,out behind,out stop) || !SolidRange(t,e,x,x,y-4,y-2,false,out ceiling,out stop))return false;jump=-5;block=true;}
            else if(n.Y+n.Height-y*16>20 && lower.Active && lower.Solid && !lower.TopSlope)
            {if(!SolidRange(t,e,x-n.Direction*2,x,y-3,y-1,true,out behind,out stop))return false;jump=-4.4f;block=true;}
            if(block){blocked=behind || ceiling;if(!blocked)n.Vy=jump;else{if(danger){n.Vx=0;n.A0=8;n.A1=240;drowning=false;}n.Direction*=-1;n.Vx*=-1;}}
            if(drowning)n.A1=90;if(n.Vy<0)n.L3=n.X;if(n.Vy<0 && n.Wet)n.Vy*=1.2f;if(n.Vy<0)n.Vy*=1.2f;return true;
        }
        internal static bool AfterMove(ref NpcMotionState n,ref PredictionEnvironment e,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(e.Multiplayer)return true;
            if(n.EffectiveType==230 && n.Wet){if(!TransformFish(ref n,ref e,55,20,18,16,true,out stop))return false;if(n.Vy<0)n.Vy=0;n.TownHomeTileX=n.HomeTileY=-1;n.TownHomeless=n.TownCloseDoor=false;n.CanHarm=false;n.NoContactDamage=true;n.TownStinky=false;n.Health.ShimmerAction=true;}
            else if(n.EffectiveType==55 && !n.Wet && e.Raining){if(!TransformFish(ref n,ref e,230,18,20,7,false,out stop))return false;n.TownFactsCaptured=true;n.TownHomeTileX=(int)(n.X/16)+10*n.Direction;n.HomeTileY=-1;n.TownHomeless=false;n.TownCloseDoor=false;n.CanHarm=false;n.NoContactDamage=true;n.TownStinky=false;n.Health.ShimmerAction=n.SpawnedFromStatue;}
            return true;
        }
        private static bool TransformFish(ref NpcMotionState n,ref PredictionEnvironment e,int type,int width,int height,int style,bool noGravity,out PredictionStop stop)
        {
            // This actor can cycle through both forms within one forecast.
            // Transform's TargetClosest selects the captured closest player;
            // preserve that player's old-form mask for the reverse transform.
            bool target=n.HasClosestPlayer?n.ClosestPlayerNoAggro:n.TargetNoAggro,closest=n.ClosestPlayerNoAggro;
            if(!NpcPhasedFlightMotion.TransformKnown(ref n,ref e,type,width,height,style,0,noGravity,out stop))return false;
            n.FormTargetNoAggro=target;n.FormClosestNoAggro=closest;return true;
        }
    }
}
