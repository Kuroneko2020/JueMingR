using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI109 owns deterministic casting and hover motion. Corpse consumption
    // is a private slot-ordered value event; newborn actors are never invented.
    // A real healing effect ends the forecast before its next NPC consumer.
    internal static class NpcDarkMageMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==109 && (n.EffectiveType==564 || n.EffectiveType==565);}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e){return NpcInvasionTargeting.NeedsPlayer(n);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return n.A0<=0;}
        internal static int FirstReadyAction(NpcMotionState n,int maximum)
        {
            if(!Known(n) || maximum<1)return -1;
            float clock=n.A0,cast=n.A1,fade=n.L3;
            for(int action=1;action<=maximum;action++)
            {
                if(clock<0)clock=Math.Min(clock+1,0);
                if(clock>0)
                {
                    clock--;if(clock<=0){float old=cast;cast++;if(cast>=3)cast=0;clock=old==0?-20:-120;}
                }
                if(clock==0 && fade>=60)return action;
                if(fade<60)fade++;
            }
            return -1;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(terrain==null || group==null || count<1 || count>group.Length || !n.MageRelationsCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(elapsed>NpcPredictionCache.Horizon){stop=PredictionStop.MissingDependency;return false;}
            if(n.MageHealingAt<=elapsed){stop=PredictionStop.PhaseBoundary;return false;}
            // Native reads the old target before its conditional retarget. Only
            // ready selection and the attack0 clock32 facing consume that value.
            NpcInvasionTarget oldTarget=default(NpcInvasionTarget);
            if(n.A0<=0 || n.A1==0 && n.A0==33)
                if(!NpcInvasionTargeting.ReadTarget(n,e,elapsed,group,count,out oldTarget,out stop))return false;
            bool casting=false,retarget=true;float damping=.96f;
            n.SpriteDirection=n.Direction>0?1:-1;n.L2=0;
            if(n.A0<0)n.A0=Math.Min(n.A0+1,0);
            if(n.A0>0)
            {
                retarget=false;casting=true;damping=.9f;n.A0--;
                if(n.A1==2 && n.A0==64 && !e.Multiplayer)
                {
                    if(e.MageCorpses==null || !e.MageCorpses.Captured){stop=PredictionStop.MissingDependency;return false;}
                    // All spots in850 are consumed before spawn RNG. That RNG
                    // only chooses new actors; this cast still has known motion.
                    bool mayCreate=AvailableCorpses(n,e.MageCorpses,group,count,722500,1);
                    n.MageRaiseOccurred=true;n.MageRaiseX=n.Bounds.CenterX;n.MageRaiseY=n.Bounds.CenterY;
                    if(mayCreate)
                    {
                        n.MageRelationsComplete=false;
                        for(int i=0;i<count;i++)if(Known(group[i]) && !group[i].Identity.Equals(n.Identity)){var other=group[i];other.MageRelationsComplete=false;group[i]=other;}
                    }
                }
                if(n.A1==0 && n.A0==32)
                {
                    float dx,dy;Unit(oldTarget.Area.CenterX-(n.Bounds.CenterX+n.Direction*10),oldTarget.Area.CenterY-(n.Bounds.CenterY-16),out dx,out dy);dx*=14;n.Direction=dx>0?1:-1;
                }
                if(n.A1==1 && !e.Multiplayer && (n.A0==40 || n.A0==48 || n.A0==56))
                {
                    bool found;if(!HealGround(n,e,terrain,out found,out stop))return false;
                    if(found)
                    {
                        int boundary=elapsed+40;n.MageHealingAt=Math.Min(n.MageHealingAt,boundary);
                        for(int i=0;i<count;i++)if(Known(group[i]) && !group[i].Identity.Equals(n.Identity))
                        {var other=group[i];other.MageHealingAt=Math.Min(other.MageHealingAt,boundary);group[i]=other;}
                    }
                }
                if(n.A0<=0){float old=n.A1;n.A1++;if(n.A1>=3)n.A1=0;n.A0=old==0?-20:-120;}
            }
            if(n.A0==0 && n.L3>=60)
            {
                if(!n.MageRelationsComplete || e.MageCorpses==null || !e.MageCorpses.Captured){stop=PredictionStop.MissingDependency;return false;}
                int wounded=0;float cx=n.Bounds.CenterX,cy=n.Bounds.CenterY;
                for(int i=0;i<count;i++)
                {
                    var other=group[i];if(other.Identity.Equals(n.Identity))other=n;
                    if(other.Active && other.LifeMax!=other.Life && other.Bounds.CenterX>=cx-600 && other.Bounds.CenterX<=cx+600 && other.Bounds.CenterY>=cy-200 && other.Bounds.CenterY<=cy+200 && ++wounded>=2)break;
                }
                if(wounded<2)n.A1=2;
                if(n.A1==2 && !CanRaise(n,e.MageCorpses,group,count))n.A1=0;
                bool ready=true;
                if(n.A1==0)
                {
                    if(Length(oldTarget.Area.CenterX-cx,oldTarget.Area.CenterY-cy)>=1000)ready=false;
                    else if(!NpcFighterClockMotion.Line(terrain,e,new MotionRect(cx,cy,0,0),new MotionRect(oldTarget.Area.CenterX,oldTarget.Area.CenterY,0,0),out ready,out stop))return false;
                }
                if(ready){if(n.A1==0)n.A0=97;else if(n.A1==1)n.A0=127;else if(n.A1==2)n.A0=183;casting=true;}
            }
            if(n.L3==0)n.Alpha=255;if(n.L3<60){n.L3++;n.Alpha=Math.Max(0,n.Alpha-5);}
            // These two native identities reset L2 before the general stuck
            // test, so the30/60/-180 branches cannot accumulate naturally.
            float window=16;bool nearX=n.X>n.L0-window && n.X<n.L0+window;
            if(!nearX && (n.Vx<0 && n.Direction>0 || n.Vx>0 && n.Direction<0)){nearX=true;window+=24;}
            bool nearY=n.Y>n.L1-window && n.Y<n.L1+window;
            if(nearX && nearY)n.L2++;else{n.L0=n.X;n.L1=n.Y;n.L2=0;}
            if(retarget){NpcInvasionTarget ignored;if(!NpcInvasionTargeting.TryTarget(ref n,e,terrain,elapsed,group,count,true,confused,out ignored,out stop))return false;}
            if(casting){n.Vx*=damping;n.Vy*=damping;return true;}
            int forward=(int)((n.X+n.Width/2)/16)+n.Direction*2,bottom=(int)((n.Y+n.Height)/16),ownX=(int)n.Bounds.CenterX/16,ownY=(int)(n.Y+n.Height)/16;
            bool air=true,close=false;
            for(int y=bottom;y<bottom+4;y++){PredictionTile tile;if(!terrain.Tile(forward,y,out tile,out stop))return false;if(tile.Active && tile.Solid || tile.Liquid>0){close=y<=bottom+1;air=false;break;}}
            for(int y=ownY;y<ownY+2;y++){PredictionTile tile;if(!terrain.Tile(ownX,y,out tile,out stop))return false;if(tile.Active && tile.Solid || tile.Liquid>0){close=true;air=false;break;}}
            if(air)n.Vy=Math.Min(.2f,n.Vy+.05f);else{if(n.DirectionY<0 && n.Vy>0 || close)n.Vy-=.05f;if(n.Vy<-.4f)n.Vy=-.4f;}
            if(n.CollideX){n.Vx=n.OldVx*-.4f;if(n.Direction==-1 && n.Vx>0 && n.Vx<1)n.Vx=1;if(n.Direction==1 && n.Vx<0 && n.Vx>-1)n.Vx=-1;}
            if(n.CollideY){n.Vy=n.OldVy*-.25f;if(n.Vy>0 && n.Vy<1)n.Vy=1;if(n.Vy<0 && n.Vy>-1)n.Vy=-1;}
            if(n.Direction==-1 && n.Vx>-.5f){n.Vx-=.1f;if(n.Vx>.5f)n.Vx-=.1f;else if(n.Vx>0)n.Vx+=.1f/2;if(n.Vx<-.5f)n.Vx=-.5f;}
            else if(n.Direction==1 && n.Vx<.5f){n.Vx+=.1f;if(n.Vx<-.5f)n.Vx+=.1f;else if(n.Vx<0)n.Vx-=.1f/2;if(n.Vx>.5f)n.Vx=.5f;}
            if(n.DirectionY==-1 && n.Vy>-.5f){n.Vy-=.02f;if(n.Vy>.5f)n.Vy-=.02f*1.25f;else if(n.Vy>0)n.Vy+=.02f*.75f;if(n.Vy<-.5f)n.Vy=-.5f;}
            else if(n.DirectionY==1 && n.Vy<.5f){n.Vy+=.02f;if(n.Vy<-.5f)n.Vy+=.02f*1.25f;else if(n.Vy<0)n.Vy-=.02f*.75f;if(n.Vy>.5f)n.Vy=.5f;}
            return true;
        }
        private static bool CanRaise(NpcMotionState n,PredictionMageCorpses corpses,NpcMotionState[] group,int count)
        {return AvailableCorpses(n,corpses,group,count,640000,3);}
        private static bool AvailableCorpses(NpcMotionState n,PredictionMageCorpses corpses,NpcMotionState[] group,int count,float range,int required)
        {
            int found=0;for(int at=0;at<corpses.Count;at++)
            {
                float x=corpses.XAt(at),y=corpses.YAt(at);bool consumed=n.MageRaiseOccurred && Squared(x-n.MageRaiseX,y-n.MageRaiseY)<=722500;
                for(int i=0;!consumed && i<count;i++){var other=group[i];if(Known(other) && other.MageRaiseOccurred && Squared(x-other.MageRaiseX,y-other.MageRaiseY)<=722500)consumed=true;}
                if(!consumed && Squared(x-n.Bounds.CenterX,y-n.Bounds.CenterY)<=range && ++found>=required)return true;
            }
            return false;
        }
        private static bool HealGround(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,out bool found,out PredictionStop stop)
        {
            found=false;stop=PredictionStop.None;int x=(int)((n.Bounds.CenterX+n.Direction*240)/16),top=(int)(n.Bounds.CenterY/16);
            for(int i=0;i<50 && top+i<e.WorldHeight;i++){int y=top+i;if(x<10 || x>=e.WorldWidth-10 || y<10 || y>=e.WorldHeight-10)continue;PredictionTile tile;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.RawActive && tile.RawSolid){found=true;break;}}
            return true;
        }
        private static float Squared(float x,float y){return (float)((double)x*x+(double)y*y);}
        private static float Length(float x,float y){return (float)Math.Sqrt((double)x*x+(double)y*y);}
        private static void Unit(float x,float y,out float dx,out float dy)
        {if(x==0 && y==0 || float.IsNaN(x) || float.IsNaN(y)){dx=0;dy=1;return;}float length=Length(x,y);double inverse=1d/length;dx=(float)(x*inverse);dy=(float)(y*inverse);}
    }
}
