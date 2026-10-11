using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI15 owns a four-hop clock and observed teleport shrink/recovery. Only
    // a choice between genuinely different legal cache tiles ends the prefix.
    internal static class NpcKingSlimeMotion
    {
        internal static bool Known(NpcMotionState n){return n.EffectiveType==50 && n.Style==15;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return !n.TargetCaptured || !n.PlayerNativeDead || n.HasClosestPlayer;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {
            return n.L3==0 && !e.Multiplayer || n.TargetCaptured && n.PlayerNativeDead || Distance(n,e)>3000 ||
                !e.Multiplayer && n.A1==6 && n.A0+1>=30 || e.Multiplayer && n.A1==6 && n.A0+1>=60 ||
                n.Vy==0 && n.A1<5 && n.A0+Clock(n)>=0 || !e.Multiplayer && n.Vy==0 && n.TimeLeft>10 && n.A2>=300 && n.A1<5;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;bool first=false,teleporting=false,hidden=false;float size=1,seedScale=1;
            if(e.GoodWorld)seedScale*=2-(1-(float)n.Life/n.LifeMax);
            if(n.A3==0 && n.Life>0)n.A3=n.LifeMax;
            if(n.L3==0){n.L3=1;first=true;if(!e.Multiplayer){n.A0=-100;Query(ref n,ref e,true,confused);}}
            bool dead=n.TargetCaptured?n.PlayerNativeDead:e.PlayerDead;
            if(dead || Distance(n,e)>3000)
            {
                Query(ref n,ref e,true,confused);dead=n.TargetCaptured?n.PlayerNativeDead:e.PlayerDead;
                if(dead || Distance(n,e)>3000)
                {
                    n.TimeLeft=Math.Min(n.TimeLeft,10);n.Direction=e.PlayerX<n.Bounds.CenterX?1:-1;
                    if(!e.Multiplayer && n.A1!=5){n.A2=n.A0=0;n.A1=5;n.L1=e.WorldWidth*16;n.L2=e.WorldHeight*16;}
                }
            }
            if(!dead && n.TimeLeft>10 && n.A2>=300 && n.A1<5 && n.Vy==0)
            {
                n.A2=n.A0=0;n.A1=5;
                if(!e.Multiplayer)
                {
                    Query(ref n,ref e,false,confused);bool antiCheese=n.L0>=360 || Distance(n,e)>2000;
                    if(n.L0>=360)n.L0=360;
                    bool found=false;
                    if(!antiCheese && !Cache(ref n,e,terrain,10,7,out found,out stop))return false;
                    if(!antiCheese && !found && !Cache(ref n,e,terrain,6,2,out found,out stop))return false;
                    if(!found && !ClosestBottom(ref n,e,elapsed,out stop))return false;
                }
            }
            bool clear;if(!NpcFighterClockMotion.Line(terrain,e,new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,0,0),new MotionRect(e.PlayerX,e.PlayerY,0,0),out clear,out stop))return false;
            if(!clear || Math.Abs(n.Y-(e.PlayerY+e.PlayerHeight*.5f))>160){n.A2++;if(!e.Multiplayer)n.L0++;}
            else if(!e.Multiplayer)n.L0=Math.Max(0,n.L0-1);
            if(n.TimeLeft<10 && (n.A0!=0 || n.A1!=0))n.A0=n.A1=0;
            if(n.A1==5)
            {
                teleporting=true;n.A0++;size=.5f+Clamp((60-n.A0)/60)*.5f;hidden=n.A0>=60;
                if(n.A0>=60 && !e.Multiplayer){n.X=n.L1-n.Width*.5f;n.Y=n.L2-n.Height;n.NewSegment=true;n.A1=6;n.A0=0;}
                if(e.Multiplayer && n.A0>=120){n.A1=6;n.A0=0;}
            }
            else if(n.A1==6)
            {
                teleporting=true;n.A0++;size=.5f+Clamp(n.A0/30)*.5f;
                if(n.A0>=(!e.Multiplayer?30:60)){n.A1=n.A0=0;Query(ref n,ref e,true,confused);}
            }
            n.Health.DontTakeDamage=hidden;n.CanReceive=!hidden && !n.Health.Immortal;
            if(n.Vy==0)
            {
                n.Vx*=.8f;if(n.Vx>-.1f && n.Vx<.1f)n.Vx=0;
                if(!teleporting)
                {
                    n.A0+=Clock(n);
                    if(n.A0>=0)
                    {
                        Query(ref n,ref e,true,confused);
                        if(n.A1==3){n.Vy=-13;n.Vx+=3.5f*n.Direction;n.A0=-200;n.A1=0;}
                        else if(n.A1==2){n.Vy=-6;n.Vx+=4.5f*n.Direction;n.A0=-120;n.A1++;}
                        else{n.Vy=-8;n.Vx+=4*n.Direction;n.A0=-120;n.A1++;}
                    }
                }
            }
            else if(n.Target<255)
            {
                float speed=e.GoodWorld?6:3;
                if(n.Direction==1 && n.Vx<speed || n.Direction==-1 && n.Vx>-speed)
                {if(n.Direction==-1 && n.Vx<.1f || n.Direction==1 && n.Vx>-.1f)n.Vx+=.2f*n.Direction;else n.Vx*=.93f;}
            }
            if(n.Life<=0)return true;
            float scale=(float)n.Life/n.LifeMax;scale=scale*.5f+.75f;scale*=size;scale*=seedScale;
            if(scale!=n.Scale || first)
            {
                // Native keeps its integer-half X anchor and whole Bottom,
                // separately from the float-half teleport Bottom assignment.
                n.X+=n.Width/2;n.Y+=n.Height;n.Scale=scale;n.Width=(int)(98*scale);n.Height=(int)(92*scale);n.X-=n.Width/2;n.Y-=n.Height;
            }
            if(!e.Multiplayer && (float)(n.Life+(int)((float)n.LifeMax*.05f))<n.A3)n.A3=n.Life;
            return true;
        }
        private static float Clamp(float value){return Math.Max(0,Math.Min(1,value));}
        private static float Distance(NpcMotionState n,PredictionEnvironment e)
        {float dx=n.Bounds.CenterX-e.PlayerX,dy=n.Bounds.CenterY-e.PlayerY;return (float)Math.Sqrt(dx*dx+dy*dy);}
        private static int Clock(NpcMotionState n)
        {int clock=2;float life=n.Life,max=n.LifeMax;if(life<max*.8f)clock++;if(life<max*.6f)clock++;if(life<max*.4f)clock+=2;if(life<max*.2f)clock+=3;if(life<max*.1f)clock+=4;return clock;}
        private static void Query(ref NpcMotionState n,ref PredictionEnvironment e,bool face,bool confused)
        {
            int old=n.Target;NpcTargeting.Retarget(ref n,ref e);
            if(face && NpcTargeting.CanFace(n,e,old))
            {var a=NpcTargeting.Area(n,e);n.Direction=(int)a.X+(int)a.Width/2<n.X+n.Width/2?-1:1;n.DirectionY=(int)a.Y+(int)a.Height/2<n.Y+n.Height/2?-1:1;}
            if(confused)n.Direction=-n.Direction;
        }
        internal static bool ClosestBottom(ref NpcMotionState n,PredictionEnvironment e,int elapsed,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(e.Players==null || !e.Players.SearchCaptured || e.Players.Count==0){stop=PredictionStop.MissingDependency;return false;}
            int chosen=0;float nearest=-1;MotionRect selected=SearchArea(e,0);
            for(int i=0;i<e.Players.Count;i++)
            {
                if(e.Players.SearchDeadAt(i))continue;
                if(elapsed>0 && e.Players.SlotAt(i)!=e.PlayerIndex){stop=PredictionStop.MissingDependency;return false;}
                var p=SearchArea(e,i);float distance=Math.Abs(p.X+(int)p.Width/2-(n.X+n.Width/2))+Math.Abs(p.Y+(int)p.Height/2-(n.Y+n.Height/2));
                if(nearest==-1 || distance<nearest){nearest=distance;chosen=i;selected=p;}
            }
            if(!e.Players.SearchDeadAt(chosen) && (!e.PlayerTimelineActive || e.Players.SlotAt(chosen)!=e.PlayerIndex)){stop=PredictionStop.MissingDependency;return false;}
            n.L1=selected.CenterX;n.L2=selected.Y+selected.Height;return true;
        }
        private static MotionRect SearchArea(PredictionEnvironment e,int index)
        {
            var area=e.Players.SearchAreaAt(index);
            return e.PlayerTimelineActive && e.Players.SlotAt(index)==e.PlayerIndex?new MotionRect(e.PlayerX-area.Width*.5f,e.PlayerY-area.Height*.5f,area.Width,area.Height):area;
        }
        internal static bool Cache(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int outer,int inner,out bool found,out PredictionStop stop)
        {
            found=false;stop=PredictionStop.None;int x=(int)(e.PlayerX/16),y=(int)(e.PlayerY/16),entries=0,firstX=0,firstY=0;
            for(int side=0;side<4;side++)
            {
                int x0=side==0?x-outer:side==1?x+inner:x-inner,x1=side==0?x-inner:side==1?x+outer:x+inner;
                int y0=side<2?y-outer:side==2?y-outer:y+inner,y1=side<2?y+outer:side==2?y-inner:y+outer;
                for(int tx=x0;tx<=x1;tx++)for(int ty=y0;ty<=y1;ty++)
                {
                    if(entries>=50)continue;
                    PredictionTile tile,above;if(!terrain.Tile(tx,ty,out tile,out stop))return false;
                    if(!tile.Active || !(tile.Solid && !tile.SolidTop || tile.Platform))continue;
                    if(!terrain.Tile(tx,ty-1,out above,out stop))return false;
                    if(above.Active && above.Solid && !above.SolidTop && !above.Half && above.Slope==0 || tile.Lava)continue;
                    bool clear;if(!NpcFighterClockMotion.Line(terrain,e,new MotionRect(tx*16+8,ty*16-n.Height/2,0,0),new MotionRect(e.PlayerX,e.PlayerY,0,0),out clear,out stop))return false;
                    if(!clear)continue;
                    if(entries>0 && (firstX!=tx || firstY!=ty)){stop=PredictionStop.RandomDestination;return false;}
                    firstX=tx;firstY=ty;entries++;
                }
            }
            if(entries>0){found=true;n.L1=firstX*16+8;n.L2=firstY*16;}
            return true;
        }
    }
}
