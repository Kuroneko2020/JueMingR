using System;
using System.Runtime.CompilerServices;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI60. Random shot directions never feed this actor's motor.
    // The server's +1..3 attack clock is a set of possible values, not a
    // fabricated RNG result. Only its actual mode consumer ends the horizon.
    internal static class NpcIceQueenMotion
    {
        private struct CenterValue {internal float X,Y;}
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static CenterValue Center(NpcMotionState n)
        {return new CenterValue{X=n.X+n.Width*.5f,Y=n.Y+n.Height*.5f};}
        internal static bool Known(NpcMotionState n){return n.EffectiveType==345 && n.Style==60;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {return n.A0==-1 || !e.Day && (n.A0==0 || n.A0==1 || n.A0==2);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e){return Retargets(n,e);}
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e)
        {return Retargets(n,e) && n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!Finite(n.A1) || n.IceQueenClockRange && (!Finite(n.IceQueenClockLower) || !Finite(n.IceQueenClockUpper) || n.IceQueenClockLower>n.IceQueenClockUpper))
            {stop=PredictionStop.InvalidState;return false;}
            if(e.Day){if(n.Vx>0)n.Vx+=.25f;else n.Vx-=.25f;n.Vy-=.1f;}
            else if(n.A0==0)
            {
                if(n.A2==0)
                {if(!Query(ref n,ref e,confused,out stop))return false;n.A2=Center(n).X<e.PlayerX?1:-1;}
                if(!Query(ref n,ref e,confused,out stop))return false;
                float distance=Math.Abs(Center(n).X-e.PlayerX);
                if(Center(n).X<e.PlayerX && n.A2<0 && distance>800)n.A2=0;
                if(Center(n).X>e.PlayerX && n.A2>0 && distance>800)n.A2=0;
                float acceleration=.45f,limit=7;
                if((double)n.Life<(double)n.LifeMax*.75){acceleration=.55f;limit=8;}
                if((double)n.Life<(double)n.LifeMax*.5){acceleration=.7f;limit=10;}
                if((double)n.Life<(double)n.LifeMax*.25){acceleration=.8f;limit=11;}
                n.Vx+=n.A2*acceleration;if(n.Vx>limit)n.Vx=limit;if(n.Vx<-limit)n.Vx=-limit;
                float height=n.PlayerPreciseArea.Y-(n.Y+n.Height);
                if(height<150)n.Vy-=.2f;if(height>200)n.Vy+=.2f;if(n.Vy>8)n.Vy=8;if(n.Vy<-8)n.Vy=-8;
                if((distance<500 || n.A3<0) && n.Y<n.PlayerPreciseArea.Y)
                {
                    n.A3++;int cadence=13;
                    if((double)n.Life<(double)n.LifeMax*.75)cadence=12;
                    if((double)n.Life<(double)n.LifeMax*.5)cadence=11;
                    if((double)n.Life<(double)n.LifeMax*.25)cadence=10;
                    cadence++;if(n.A3>cadence)n.A3=-cadence;
                }
                else if(n.A3<0)n.A3++;
                if(!Clock(ref n,ref e,800,distance<600,confused,out stop))return false;
            }
            else if(n.A0==1)
            {
                if(!Query(ref n,ref e,confused,out stop))return false;
                float acceleration=.15f,limit=7;
                if((double)n.Life<(double)n.LifeMax*.75){acceleration=.17f;limit=8;}
                if((double)n.Life<(double)n.LifeMax*.5){acceleration=.2f;limit=9;}
                if((double)n.Life<(double)n.LifeMax*.25){acceleration=.25f;limit=10;}
                acceleration-=.05f;limit-=1;
                if(Center(n).X<e.PlayerX){n.Vx+=acceleration;if(n.Vx<0)n.Vx*=.98f;}
                if(Center(n).X>e.PlayerX){n.Vx-=acceleration;if(n.Vx>0)n.Vx*=.98f;}
                if(n.Vx>limit || n.Vx<-limit)n.Vx*=.95f;
                float height=n.PlayerPreciseArea.Y-(n.Y+n.Height);
                if(height<180)n.Vy-=.1f;if(height>200)n.Vy+=.1f;if(n.Vy>6)n.Vy=6;if(n.Vy<-6)n.Vy=-6;
                if(!e.Multiplayer)
                {
                    n.A3++;int cadence=15;
                    if((double)n.Life<(double)n.LifeMax*.75)cadence=14;
                    if((double)n.Life<(double)n.LifeMax*.5)cadence=12;
                    if((double)n.Life<(double)n.LifeMax*.25)cadence=10;
                    if((double)n.Life<(double)n.LifeMax*.1)cadence=8;
                    cadence+=3;if(n.A3>=cadence)n.A3=0;
                }
                // SolidTile/random projectile variant only decide another
                // actor's birth. Neither result changes this motor or clock.
                if(!Clock(ref n,ref e,600,true,confused,out stop))return false;
            }
            else if(n.A0==2)
            {
                if(!Query(ref n,ref e,confused,out stop))return false;
                n.Vx*=.95f;n.Vy*=.95f;n.A3++;int cadence=7;
                if((double)n.Life<(double)n.LifeMax*.75)cadence--;
                if((double)n.Life<(double)n.LifeMax*.5)cadence-=2;
                if((double)n.Life<(double)n.LifeMax*.25)cadence-=3;
                if((double)n.Life<(double)n.LifeMax*.1)cadence-=4;
                if(n.A3>cadence)n.A3=0;
                if(!Clock(ref n,ref e,500,true,confused,out stop))return false;
            }
            else if(n.A0!=-1){stop=PredictionStop.UnsupportedMechanism;return false;}
            // Native runs this tail even on a client and during daytime. A
            // far player overwrites ALL three random results with phase zero.
            if(n.A0==-1)return Choose(ref n,ref e,confused,out stop);
            return true;
        }
        private static bool Query(ref NpcMotionState n,ref PredictionEnvironment e,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;NpcTargeting.Face(ref n,ref e,true,confused);
            if(n.TargetChoiceUnavailable){stop=PredictionStop.TerrainUnavailable;return false;}
            if(!n.TargetCaptured || !n.HasPlayer || !n.PlayerPreciseAreaCaptured)
            {stop=PredictionStop.MissingDependency;return false;}return true;
        }
        private static bool Clock(ref NpcMotionState n,ref PredictionEnvironment e,int limit,bool consumer,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(e.Multiplayer)return true;
            if(!n.IceQueenClockRange){n.IceQueenClockLower=n.IceQueenClockUpper=n.A1;n.IceQueenClockRange=true;}
            n.IceQueenClockLower+=1;n.IceQueenClockUpper+=3;
            // A1 remains the last observed scalar. These separate bounds are
            // the only future clock input; no one random value is published.
            if(!consumer || n.IceQueenClockUpper<=limit)return true;
            if(n.IceQueenClockLower<=limit){stop=PredictionStop.RandomDecision;return false;}
            n.A0=-1;return Choose(ref n,ref e,confused,out stop);
        }
        private static bool Choose(ref NpcMotionState n,ref PredictionEnvironment e,bool confused,out PredictionStop stop)
        {
            if(!Query(ref n,ref e,confused,out stop))return false;
            if(Math.Abs(Center(n).X-e.PlayerX)<=1000){stop=PredictionStop.RandomDecision;return false;}
            n.A0=n.A1=n.A2=n.A3=0;n.IceQueenClockRange=false;n.IceQueenClockLower=n.IceQueenClockUpper=0;return true;
        }
        private static bool Finite(float x){return !float.IsNaN(x) && !float.IsInfinity(x);}
    }
}
