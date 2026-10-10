using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI43 separates attack RNG from motor RNG. The 20 possible bee /
    // stinger origins also enter CanHit and raw component pursuit comparisons.
    // Continue only when every legal origin produces exactly the same motor;
    // do not replay RNG or substitute a representative future attack.
    internal static class NpcQueenBeeMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==43 && n.EffectiveType==222;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return !n.TargetCaptured || !n.PlayerNativeDead || n.HasClosestPlayer;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return true;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!n.TargetCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(n.Target<0 || n.Target==255 || n.PlayerNativeDead || !n.PlayerActive)Query(ref n,ref e,confused,elapsed);
            if(!n.HasPlayer){stop=PredictionStop.MissingDependency;return false;}
            if(!Ready(n,elapsed,out stop))return false;
            bool dead=n.PlayerNativeDead;float enrage=0;
            if((double)(n.Y/16f)<e.GravityWorldSurface)enrage++;
            if(!e.Jungle)enrage++;if(e.GoodWorld)enrage+=.5f;
            if(e.Expert)n.Health.Defense=n.Health.DefaultDefense+(int)(20f*(1f-(float)n.Life/n.LifeMax));
            float distance=Distance(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY);
            if(n.A0!=5){n.TimeLeft=Math.Max(n.TimeLeft,60);if(distance>3000)n.A0=4;}
            if(dead)n.A0=5;
            if(n.A0==5)
            {
                n.Vy*=.98f;n.Direction=n.Vx<0?-1:1;n.SpriteDirection=n.Direction;
                if(n.X<e.WorldWidth*8){if(n.Vx>0)n.Vx*=.98f;else n.L0=1;n.Vx-=.08f;}
                else{if(n.Vx<0)n.Vx*=.98f;else n.L0=1;n.Vx+=.08f;}
                n.TimeLeft=Math.Min(n.TimeLeft,10);return true;
            }
            if(n.A0==-1){if(e.Multiplayer)return true;stop=PredictionStop.RandomDecision;return false;}
            if(n.A0==0)return Charge(ref n,ref e,enrage,elapsed,confused,out stop);
            if(n.A0==2)
            {
                Query(ref n,ref e,confused,elapsed);if(!Ready(n,elapsed,out stop))return false;n.SpriteDirection=n.Direction;
                float x=PlayerX(e)-n.Bounds.CenterX,y=PlayerY(e)-200-n.Bounds.CenterY;
                if(ManualLength(x,y)<200){n.A0=1;n.A1=0;return true;}
                // Original computes 12/length but never applies it to x/y.
                Fly(ref n,x,y,e.Expert?.1f:.07f,1);return true;
            }
            if(n.A0==1 || n.A0==3)
            {
                bool bees=n.A0==1;
                if(bees){n.L0=0;Query(ref n,ref e,confused,elapsed);if(!Ready(n,elapsed,out stop))return false;}
                n.A1++;
                if(bees && e.Expert)
                {
                    int nearby;if(!Nearby(n,e,elapsed,out nearby,out stop))return false;n.A1+=nearby/2;
                    if(Low(n,.75))n.A1+=.25f;if(Low(n,.5))n.A1+=.25f;if(Low(n,.25))n.A1+=.25f;if(Low(n,.1))n.A1+=.25f;
                }
                int period=bees?(int)(40f-18f*enrage):Period(n,e,enrage);
                if(bees && n.A1>period){n.A1=0;n.A2++;}
                var prefix=n;NpcMotionState result=default(NpcMotionState);
                // CanHit starts from integer tile centers. At most three X
                // start cells occur across these 20 contiguous legal origins;
                // reuse their pure terrain answers without storing native RNG.
                int cell0=int.MinValue,cell1=int.MinValue,cell2=int.MinValue;bool clear0=false,clear1=false,clear2=false;
                for(int origin=0;origin<20;origin++)
                {
                    var candidate=prefix;var candidateEnvironment=e;
                    float ox=n.X+n.Width/2+origin*n.Direction,oy=n.Y+n.Height*.8f;
                    bool clear=true;
                    float x=PlayerX(e)-n.Bounds.CenterX,y=PlayerY(e)-n.Bounds.CenterY-(bees?0:300);
                    float length=ManualLength(x,y);
                    if(!bees || length<=400)
                    {
                        int cell=(int)ox/16;
                        if(cell==cell0)clear=clear0;else if(cell==cell1)clear=clear1;else if(cell==cell2)clear=clear2;
                        else
                        {
                            if(terrain==null){stop=PredictionStop.TerrainUnavailable;return false;}
                            var source=new MotionRect(ox,oy-30,1,1);var target=PlayerArea(e);
                            if(!terrain.CanHit(source,target,out clear,out stop))return false;
                            if(cell0==int.MinValue){cell0=cell;clear0=clear;}else if(cell1==int.MinValue){cell1=cell;clear1=clear;}else{cell2=cell;clear2=clear;}
                        }
                    }
                    if(bees)
                    {
                        if(length>400 || !clear)Fly(ref candidate,PlayerX(e)-ox,PlayerY(e)-oy,.1f,1);
                        else{candidate.Vx*=.9f;candidate.Vy*=.9f;}
                        candidate.SpriteDirection=candidate.Direction;if(candidate.A2>5){candidate.A0=-1;candidate.A1=1;}
                    }
                    else
                    {
                        if(!clear)Fly(ref candidate,PlayerX(e)-ox,PlayerY(e)-oy,enrage>0?.5f:.1f,1);
                        else if(length>100)
                        {
                            // The original reads raw desired components before
                            // TargetClosest. Preserve those old target values.
                            Query(ref candidate,ref candidateEnvironment,confused,elapsed);if(!Ready(candidate,elapsed,out stop))return false;
                            candidate.SpriteDirection=candidate.Direction;Fly(ref candidate,x,y,(e.Expert?.075f:.05f)+.2f*enrage,2);
                        }
                        if(candidate.A1>period*(20f-5f*enrage)){candidate.A0=-1;candidate.A1=3;}
                    }
                    if(origin==0)result=candidate;
                    else if(!SameMotor(result,candidate)){stop=PredictionStop.RandomDecision;return false;}
                }
                n=result;return true;
            }
            if(n.A0==4)
            {
                n.L0=1;float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY;
                float root=Distance(x,y);if(root==0){stop=PredictionStop.InvalidState;return false;}
                double inverse=1d/root;x=(float)(x*inverse);y=(float)(y*inverse);x*=14;y*=14;
                float vx=n.Vx*14,vy=n.Vy*14;vx+=x;vy+=y;n.Vx=vx/15;n.Vy=vy/15;
                n.Direction=n.Vx<0?-1:1;n.SpriteDirection=n.Direction;if(distance<2000){n.A0=-1;n.L0=0;}return true;
            }
            // Nondefault observed clocks outside the native branches are
            // inert, just as the original else-if chain is.
            return true;
        }
        private static bool Charge(ref NpcMotionState n,ref PredictionEnvironment e,float enrage,int elapsed,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int charges=2;
            if(e.Expert){if(n.Life<n.LifeMax/2)charges++;if(n.Life<n.LifeMax/3)charges++;if(n.Life<n.LifeMax/5)charges++;}
            charges+=(int)enrage;
            if(n.A1>2*charges && n.A1%2==0){n.A0=-1;n.A1=n.A2=0;return true;}
            if(n.A1%2==0)
            {
                Query(ref n,ref e,confused,elapsed);if(!Ready(n,elapsed,out stop))return false;
                if(Math.Abs(n.Bounds.CenterY-e.PlayerY)<20+20*enrage)
                {
                    n.L0=1;n.A1++;n.A2=0;float speed=12;
                    if(e.Expert){speed=16;if(Low(n,.75))speed+=2;if(Low(n,.5))speed+=2;if(Low(n,.25))speed+=2;if(Low(n,.1))speed+=2;}
                    speed+=7*enrage;float x=PlayerX(e)-n.Bounds.CenterX,y=PlayerY(e)-n.Bounds.CenterY;
                    float factor=speed/ManualLength(x,y);n.Vx=x*factor;n.Vy=y*factor;n.SpriteDirection=n.Direction;return true;
                }
                n.L0=0;float cap=12,acceleration=.15f;
                if(e.Expert){if(Low(n,.75)){cap++;acceleration+=.05f;}if(Low(n,.5)){cap++;acceleration+=.05f;}if(Low(n,.25)){cap+=2;acceleration+=.05f;}if(Low(n,.1)){cap+=2;acceleration+=.1f;}}
                cap+=3*enrage;acceleration+=.5f*enrage;n.Vy+=n.Y+n.Height/2<PlayerY(e)?acceleration:-acceleration;n.Vy=Math.Max(-cap,Math.Min(cap,n.Vy));
                float dx=Math.Abs(n.X+n.Width/2-PlayerX(e));if(dx>600)n.Vx+=.15f*n.Direction;else if(dx<300)n.Vx-=.15f*n.Direction;else n.Vx*=.8f;
                n.Vx=Math.Max(-16,Math.Min(16,n.Vx));n.SpriteDirection=n.Direction;return true;
            }
            n.Direction=n.Vx<0?-1:1;n.SpriteDirection=n.Direction;int limit=600;
            if(e.Expert)limit=Low(n,.1)?300:Low(n,.25)?450:Low(n,.5)?500:Low(n,.75)?550:600;
            int side=n.X+n.Width/2<PlayerX(e)?-1:1;limit-=(int)(100*enrage);bool crossed=false;
            if(n.Direction==side && Math.Abs(n.Bounds.CenterX-e.PlayerX)>limit){n.A2=1;crossed=true;}
            if(Math.Abs(n.Bounds.CenterY-e.PlayerY)>limit*1.5f){n.A2=1;crossed=true;}
            if(enrage>0 && crossed){n.Vx*=.5f;n.Vy*=.5f;}
            if(n.A2==1)
            {
                Query(ref n,ref e,confused,elapsed);if(!Ready(n,elapsed,out stop))return false;n.SpriteDirection=n.Direction;n.L0=0;n.Vx*=.9f;n.Vy*=.9f;float epsilon=.1f;
                if(e.Expert){if(n.Life<n.LifeMax/2){n.Vx*=.9f;n.Vy*=.9f;epsilon+=.05f;}if(n.Life<n.LifeMax/3){n.Vx*=.9f;n.Vy*=.9f;epsilon+=.05f;}if(n.Life<n.LifeMax/5){n.Vx*=.9f;n.Vy*=.9f;epsilon+=.05f;}}
                if(enrage>0){n.Vx*=.7f;n.Vy*=.7f;}if(Math.Abs(n.Vx)+Math.Abs(n.Vy)<epsilon){n.A2=0;n.A1++;}
            }
            else n.L0=1;return true;
        }
        private static bool Nearby(NpcMotionState n,PredictionEnvironment e,int elapsed,out int count,out PredictionStop stop)
        {
            count=0;stop=PredictionStop.None;if(e.Players==null || !e.Players.SearchCaptured){stop=PredictionStop.MissingDependency;return false;}
            for(int i=0;i<e.Players.Count;i++)
            {
                if(e.Players.SearchDeadAt(i))continue;var area=e.Players.SearchAreaAt(i);
                if(e.Players.SlotAt(i)==e.PlayerIndex)area=PlayerArea(e);
                else if(elapsed>1){stop=PredictionStop.MissingDependency;return false;}
                // The native count includes active ghosts: SearchAlive excludes
                // them, whereas native dead and active are separate facts.
                if(Distance(n.Bounds.CenterX-area.CenterX,n.Bounds.CenterY-area.CenterY)<1000)count++;
            }
            return true;
        }
        private static bool Ready(NpcMotionState n,int elapsed,out PredictionStop stop)
        {stop=PredictionStop.None;if(n.TargetChoiceUnavailable || n.TargetCaptured && n.PlayerGhost && elapsed>1){stop=PredictionStop.MissingDependency;return false;}return true;}
        private static void Query(ref NpcMotionState n,ref PredictionEnvironment e,bool confused,int elapsed)
        {
            // Closest is an observation of THIS actor position, not a future
            // winner. A second player's unpredicted motion becomes necessary
            // when native actually retargets, including charge slowdown.
            if(elapsed>1 && e.Players!=null)
            {
                if(!e.Players.SearchCaptured)n.TargetChoiceUnavailable=true;
                else for(int i=0;i<e.Players.Count;i++)
                    if(e.Players.SearchAliveAt(i) && e.Players.SlotAt(i)!=e.PlayerIndex){n.TargetChoiceUnavailable=true;break;}
            }
            NpcTargeting.Face(ref n,ref e,true,confused);
        }
        private static bool Low(NpcMotionState n,double fraction){return (double)n.Life<n.LifeMax*fraction;}
        private static int Period(NpcMotionState n,PredictionEnvironment e,float enrage)
        {int period=40;if(e.Expert)period=Low(n,.1)?15:n.Life<n.LifeMax/3?25:n.Life<n.LifeMax/2?30:35;return period-(int)(5*enrage);}
        private static MotionRect PlayerArea(PredictionEnvironment e){return new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight);}
        private static float PlayerX(PredictionEnvironment e){return e.PlayerX-e.PlayerWidth*.5f+(int)e.PlayerWidth/2;}
        private static float PlayerY(PredictionEnvironment e){return e.PlayerY-e.PlayerHeight*.5f+(int)e.PlayerHeight/2;}
        private static float ManualLength(float x,float y){return (float)Math.Sqrt(x*x+y*y);}
        private static float Distance(float x,float y){return (float)Math.Sqrt((double)x*x+(double)y*y);}
        private static void Fly(ref NpcMotionState n,float x,float y,float acceleration,int opposite)
        {
            if(n.Vx<x){n.Vx+=acceleration;if(n.Vx<0 && x>0)n.Vx+=acceleration*opposite;}else if(n.Vx>x){n.Vx-=acceleration;if(n.Vx>0 && x<0)n.Vx-=acceleration*opposite;}
            if(n.Vy<y){n.Vy+=acceleration;if(n.Vy<0 && y>0)n.Vy+=acceleration*opposite;}else if(n.Vy>y){n.Vy-=acceleration;if(n.Vy>0 && y<0)n.Vy-=acceleration*opposite;}
        }
        private static bool SameMotor(NpcMotionState a,NpcMotionState b)
        {return a.Vx==b.Vx && a.Vy==b.Vy && a.A0==b.A0 && a.A1==b.A1 && a.A2==b.A2 && a.L0==b.L0 && a.Direction==b.Direction && a.DirectionY==b.DirectionY && a.SpriteDirection==b.SpriteDirection && a.Target==b.Target && a.PlayerIndex==b.PlayerIndex;}
    }
}
