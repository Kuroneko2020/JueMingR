using System;
using System.Runtime.CompilerServices;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI121 consumes only its numbered player and local terrain. Spawned
    // slime/projectile RNG does not feed its own movement or receive state.
    internal static class NpcQueenSlimeMotion
    {
        private struct V {internal float X,Y;}
        [MethodImpl(MethodImplOptions.NoInlining)] private static V Store(float x,float y){return new V{X=x,Y=y};}
        private static V Center(NpcMotionState n){return Store(n.X+n.Width*.5f,n.Y+n.Height*.5f);}
        private static V Sub(V a,V b){return Store(a.X-b.X,a.Y-b.Y);}
        private static float Length(V v){return (float)Math.Sqrt((double)v.X*v.X+(double)v.Y*v.Y);}
        private static V Normalize(V v)
        {float length=Length(v);if(length==0 || float.IsNaN(length))return default(V);double inverse=1d/length;return Store((float)(v.X*inverse),(float)(v.Y*inverse));}
        private static V Scale(V v,float scale){return Store(v.X*scale,v.Y*scale);}
        internal static bool Known(NpcMotionState n){return n.EffectiveType==657 && n.Style==121;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return !n.TargetCaptured || !n.PlayerNativeDead || n.HasClosestPlayer;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {
            bool flight=n.Life<=n.LifeMax/2;int phase=(int)n.A0;
            return n.L0==0 || n.PlayerNativeDead || Math.Abs(Center(n).X-CurrentCenterX(n,e))/16f>500 ||
                flight && (phase==0 || phase==5 && n.A2!=1) ||
                phase==1 && n.A1+1>=(!e.Multiplayer?30:60) ||
                phase==3 && n.Vy==0 && n.A1+JumpClock(n)>=0 ||
                !e.Multiplayer && (phase==4 || phase==5) && n.A2!=1 && n.A1==0 ||
                !flight && n.TimeLeft>10 && n.A3>=300 && n.A0==0 && n.Vy==0;
        }
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e)
        {return Retargets(n,e) && n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;}
        private static float CurrentCenterX(NpcMotionState n,PredictionEnvironment e)
        {return n.TargetCaptured && n.PlayerVelocityCaptured?n.PlayerCenterX:e.PlayerX;}
        private static bool Dead(NpcMotionState n,PredictionEnvironment e){return n.TargetCaptured?n.PlayerNativeDead:e.PlayerDead;}
        private static bool Active(NpcMotionState n){return !n.TargetCaptured || n.PlayerActive;}
        private static void Query(ref NpcMotionState n,ref PredictionEnvironment e,bool face,bool confused)
        {NpcTargeting.Face(ref n,ref e,face,confused);}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            bool flight=n.Life<=n.LifeMax/2,hidden=false;float size=1;
            if(n.L0==0){n.A1=-100;n.L0=n.LifeMax;Query(ref n,ref e,true,confused);}
            if(Dead(n,e) || Math.Abs(Center(n).X-e.PlayerX)/16f>500)
            {
                Query(ref n,ref e,true,confused);
                if(Dead(n,e) || Math.Abs(Center(n).X-e.PlayerX)/16f>500)
                {n.TimeLeft=Math.Min(n.TimeLeft,10);n.Direction=e.PlayerX<Center(n).X?1:-1;}
            }
            if(!Dead(n,e) && n.TimeLeft>10 && !flight && n.A3>=300 && n.A0==0 && n.Vy==0)
            {
                n.A0=2;n.A1=0;
                if(!e.Multiplayer)
                {
                    Query(ref n,ref e,false,confused);bool anti=n.A3>=360 || Length(Sub(Store(e.PlayerX,e.PlayerY),Center(n)))>2000;
                    if(n.A3>=360)n.A3=360;bool found=false;
                    if(!anti && !NpcKingSlimeMotion.Cache(ref n,e,terrain,10,7,out found,out stop))return false;
                    if(!anti && !found && !NpcKingSlimeMotion.Cache(ref n,e,terrain,6,2,out found,out stop))return false;
                    if(!found && !NpcKingSlimeMotion.ClosestBottom(ref n,e,elapsed,out stop))return false;
                    if(!found)n.A3=0;
                }
            }
            if(!flight)
            {
                V center=Center(n);bool clear;
                if(!NpcFighterClockMotion.Line(terrain,e,new MotionRect(center.X,center.Y,0,0),new MotionRect(e.PlayerX,e.PlayerY,0,0),out clear,out stop))return false;
                if(!clear || Math.Abs(n.Y-PlayerBottom(n,e))>320)n.A3+=1.5f;else n.A3=Math.Max(0,n.A3-1);
            }
            else n.A3=Math.Max(0,n.A3-1);
            if(n.TimeLeft<=10 && (flight?n.A0!=0:n.A0!=3)){n.A0=flight?0:3;n.A1=n.A2=n.A3=0;}
            n.NoTileCollide=n.NoGravity=false;
            // local3 is a drawing frame; it never feeds AI121 geometry/receive.
            switch((int)n.A0)
            {
                case 0:
                    if(flight){if(!Fly(ref n,ref e,terrain,confused,out stop))return false;}
                    else if(n.Vy==0)Brake(ref n);
                    if(n.TimeLeft<=10 || !flight && n.Vy!=0)break;
                    n.A1++;if(n.A1>(flight?120:60))
                    {
                        // An above/distant player can collapse BOTH flight
                        // choices to the same phase5/A2=0. Retain that exact
                        // state only when the other choice also keeps A2=0.
                        if(flight && n.A2==0 && Active(n) && !Dead(n,e) &&
                            (PlayerBottom(n,e)<Store(0,n.Y+n.Height).Y || Math.Abs(e.PlayerX-Center(n).X)>250))
                        {n.A0=5;n.A1=0;}
                        else{stop=PredictionStop.RandomDecision;return false;}
                    }
                    break;
                case 1:
                    n.A1++;size=.5f+Clamp(n.A1/30f)*.5f;
                    if(n.A1>=(!e.Multiplayer?30:60)){n.A0=n.A1=0;Query(ref n,ref e,true,confused);}break;
                case 2:
                    n.A1++;size=.5f+Clamp((60-n.A1)/60f)*.5f;hidden=n.A1>=60;
                    if(n.A1>=60 && !e.Multiplayer)
                    {V bottom=Store(n.L1,n.L2);V pos=Store(bottom.X-n.Width*.5f,bottom.Y-n.Height);n.X=pos.X;n.Y=pos.Y;n.NewSegment=true;n.A0=1;n.A1=0;}
                    if(e.Multiplayer && n.A1>=120){n.A0=1;n.A1=0;}break;
                case 3:
                    if(n.Vy==0)
                    {
                        Brake(ref n);n.A1+=4;
                        if((double)n.Life<(double)n.LifeMax*.66)n.A1+=4;
                        if((double)n.Life<(double)n.LifeMax*.33)n.A1+=4;
                        if(n.A1<0)break;Query(ref n,ref e,true,confused);
                        if(n.A2==3){n.Vy=-13;n.Vx+=3.5f*n.Direction;n.A1=n.A2=0;if(n.TimeLeft>10)n.A0=0;else n.A1=-60;}
                        else if(n.A2==2){n.Vy=-6;n.Vx+=4.5f*n.Direction;n.A1=-40;n.A2++;}
                        else{n.Vy=-8;n.Vx+=4*n.Direction;n.A1=-40;n.A2++;}
                    }
                    else if(n.Target<255)
                    {
                        float speed=e.GoodWorld?7:3;
                        if(n.Direction==1 && n.Vx<speed || n.Direction==-1 && n.Vx>-speed)
                        {if(n.Direction==-1 && (double)n.Vx<.1 || n.Direction==1 && (double)n.Vx>-.1)n.Vx+=.2f*n.Direction;else n.Vx*=.93f;}
                    }
                    break;
                case 4:
                    n.NoTileCollide=n.NoGravity=true;
                    if(n.A2==1)
                    {
                        n.NoTileCollide=n.NoGravity=false;int wait=e.GoodWorld?0:flight?10:30;
                        if(n.Vy==0)n.A0=n.A1=n.A2=0;
                        n.Vx*=.8f;n.A1++;
                        if(n.A1>=wait)
                        {
                            if(flight && n.A1>wait+120){n.A0=n.A1=n.A2=0;n.Vy*=.8f;break;}
                            n.Vy+=1;if(e.GoodWorld)n.Vy+=1;if(n.Vy==0)n.Vy=.01f;n.Vy=Math.Min(e.GoodWorld?15.99f:14,n.Vy);
                        }
                        else n.Vy*=.8f;
                        break;
                    }
                    if(!e.Multiplayer && n.A1==0)Query(ref n,ref e,true,confused);n.A1++;if(n.A1<30)break;
                    if(n.A1>=60){n.A1=60;if(!e.Multiplayer){n.A1=0;n.A2=1;n.Vy=-3;}}
                    V aim=Center(n);if(!Dead(n,e) && Active(n) && Math.Abs(aim.X-e.PlayerX)/16f<=500)aim=Store(e.PlayerX,e.PlayerY);
                    aim=Store(aim.X,aim.Y-384);
                    if(n.Vy==0){V desired=Scale(Normalize(Sub(aim,Center(n))),20);n.Vx=desired.X;n.Vy=desired.Y;}else n.Vy*=.95f;
                    break;
                case 5:
                    n.NoTileCollide=n.NoGravity=true;if(flight)n.A3=0;
                    if(n.A2==1){n.A1++;if(n.A1>=10)n.A0=n.A1=n.A2=0;break;}
                    if(!e.Multiplayer && n.A1==0)Query(ref n,ref e,true,confused);n.A1++;
                    if(n.A1>=50){n.A1=50;if(!e.Multiplayer){n.A1=0;n.A2=1;}}
                    if(flight && !Fly(ref n,ref e,terrain,confused,out stop))return false;
                    break;
            }
            n.Health.DontTakeDamage=hidden;n.CanReceive=!hidden && !n.Health.Immortal;
            if(size!=n.Scale)
            {n.X+=n.Width/2;n.Y+=n.Height;n.Scale=size;n.Width=(int)(114f*size);n.Height=(int)(100f*size);n.X-=n.Width/2;n.Y-=n.Height;}
            if(n.Life<=0 || e.Multiplayer)return true;
            // This actual health transition resets the controller AFTER the
            // old phase action, with gravity flags from that same old action.
            if(n.L0>=n.LifeMax/2 && n.Life<n.LifeMax/2){n.L0=n.Life;n.A0=n.A1=n.A2=0;}
            int delta=(int)((float)n.LifeMax*(flight?.015f:.02f));if((float)(n.Life+delta)<n.L0)n.L0=n.Life;
            return true;
        }
        private static float Clamp(float v){return Math.Max(0,Math.Min(1,v));}
        private static float PlayerBottom(NpcMotionState n,PredictionEnvironment e)
        {return n.PlayerPreciseAreaCaptured && n.PlayerIndex==e.PlayerIndex?Store(0,n.PlayerPreciseArea.Y+n.PlayerPreciseArea.Height).Y:Store(0,e.PlayerY+e.PlayerHeight*.5f).Y;}
        private static int JumpClock(NpcMotionState n)
        {int clock=4;if((double)n.Life<(double)n.LifeMax*.66)clock+=4;if((double)n.Life<(double)n.LifeMax*.33)clock+=4;return clock;}
        private static void Brake(ref NpcMotionState n){n.Vx*=.8f;if((double)n.Vx>-.1 && (double)n.Vx<.1)n.Vx=0;}
        private static bool Fly(ref NpcMotionState n,ref PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;n.NoTileCollide=n.NoGravity=true;Query(ref n,ref e,true,confused);V center=Center(n),desired=center;
            if(n.TimeLeft>10)
            {
                bool clear;MotionRect target=n.PlayerPreciseAreaCaptured && n.PlayerIndex==e.PlayerIndex?n.PlayerPreciseArea:new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight);
                if(!terrain.CanHit(n.Bounds,target,out clear,out stop))return false;
                if(!clear)
                {
                    bool found=false;for(int i=0;i<16;i++)
                    {
                        V scan=Store(e.PlayerX,e.PlayerY-16*i);int x=(int)(scan.X/16f),y=(int)(scan.Y/16f);PredictionTile tile;
                        if(!terrain.Tile(x,y,out tile,out stop))return false;
                        if(tile.Active && tile.Solid && !tile.SolidTop){desired=Sub(Store(e.PlayerX,e.PlayerY-16*i+16),center);found=true;break;}
                    }
                    if(!found)desired=Sub(Store(e.PlayerX,e.PlayerY),center);
                }
                else desired=Sub(Store(e.PlayerX,e.PlayerY-250),center);
            }
            else desired=Sub(Store(center.X+500*n.Direction,center.Y-250),center);
            float distance=Length(desired),acc=.085f;
            if(Math.Abs(desired.X)<40)desired.X=n.Vx;
            if(distance>100 && (n.Vx<-10 && desired.X>0 || n.Vx>10 && desired.X<0))acc*=2;
            if(distance<40)desired=Store(n.Vx,n.Vy);else desired=Scale(Normalize(desired),distance<80?12*.65f:12);
            Axis(ref n.Vx,desired.X,acc);Axis(ref n.Vy,desired.Y,acc);return true;
        }
        private static void Axis(ref float v,float desired,float acceleration)
        {if(v<desired){v+=acceleration;if(v<0 && desired>0)v+=acceleration;}else if(v>desired){v-=acceleration;if(v>0 && desired<0)v-=acceleration;}}
    }
}
