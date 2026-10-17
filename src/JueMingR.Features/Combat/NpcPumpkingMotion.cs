using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI58/59. Blades consume their clamped parent's actual slot state;
    // the body never consumes blade geometry. Projectile RNG has no motor
    // feedback. The server's mode choice is a necessary night motor boundary.
    internal static class NpcPumpkingMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==58 && n.EffectiveType==327 || n.Style==59 && n.EffectiveType==328;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b) && a.EffectiveType==b.EffectiveType;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group=null,int count=0,int remaining=120)
        {
            if(e.Day)return false;
            if(n.Style==58)return n.A1!=2 && (!n.TargetCaptured || !n.PlayerNativeDead || n.HasClosestPlayer && !n.ClosestPlayerNativeDead);
            if(n.PositionOwnerCaptured && !n.PositionOwnerValid)return false;
            // The ascent/side sweep only consumes its owner until launch. A
            // whole forecast still needs the player that a future launch uses.
            if(n.A2==1 || n.A2==4)return remaining>1 || Retargets(n,e,group,count);
            return true;
        }
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group=null,int count=0)
        {
            if(n.Style==59)
            {
                int owner=Owner(n,group,count);if(e.Day || owner<0 || !group[owner].Active || group[owner].Style!=58)return false;
                var body=group[owner];return n.A2==1 && n.Y<body.Y-200 || n.A2==4 && (n.Bounds.CenterX<body.Bounds.CenterX-500 || n.Bounds.CenterX>body.Bounds.CenterX+500);
            }
            return !e.Multiplayer && n.A0==0 || !e.Day && n.A1==0 && n.A2+1>=300 && n.A3==1 || n.TargetCaptured && (n.PlayerNativeDead || n.PlayerPreciseAreaCaptured && Far(n));
        }
        internal static int PlayerPremise(NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group,int count)
        {
            // No old-target position is read during 1/4 before the real query.
            // Prepare its future launch's closest timeline without performing
            // a premature TargetClosest or changing this actor's native target.
            bool query=Retargets(n,e,group,count) || n.Style==59 && (n.A2==1 || n.A2==4);
            return query && n.HasClosestPlayer?n.ClosestPlayerIndex:n.PlayerIndex;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            return n.Style==58?Body(ref n,e,confused,out stop):Blade(ref n,e,group,count,confused,out stop);
        }
        private static bool Body(ref NpcMotionState n,PredictionEnvironment e,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;n.L0++;
            if(n.L0>6){n.L0=0;n.L1++;if(n.L1>4)n.L1=0;}
            if(!e.Multiplayer)
            {
                n.L2++;
                if(n.L2>300)
                {
                    if(!e.Day){stop=PredictionStop.RandomDecision;return false;}
                    n.L2=0;n.PumpkingModeUnknown=true;
                }
            }
            // Native birth creates two blades, but does not read them back.
            // Their future identities are observed when they are consumers.
            if(n.A0==0 && !e.Multiplayer){NpcTargeting.Face(ref n,ref e,true,confused);n.A0=1;}
            if(n.TargetCaptured && n.HasPlayer && !n.PlayerPreciseAreaCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(n.TargetCaptured && (n.PlayerNativeDead || Far(n)))
            {
                NpcTargeting.Face(ref n,ref e,true,confused);
                if(n.HasPlayer && !n.PlayerPreciseAreaCaptured){stop=PredictionStop.MissingDependency;return false;}
                if(n.PlayerNativeDead || Far(n))n.A1=2;
            }
            if(e.Day){n.Vy+=.3f;n.Vx*=.9f;return true;}
            if(n.PumpkingModeUnknown){stop=PredictionStop.RandomDecision;return false;}
            if(n.TargetCaptured && !n.HasPlayer){stop=PredictionStop.MissingDependency;return false;}
            if(n.A1==0)
            {
                n.A2++;
                if(n.A2>=300){n.A2=0;if(n.A3==1){n.A1=1;NpcTargeting.Face(ref n,ref e,true,confused);}else n.A1=0;}
                float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-200-n.Bounds.CenterY,length=Length(dx,dy);int speed=6;
                if(n.A3==1)speed=length>900?12:length>600?10:length>300?8:6;
                if(length>50){float ratio=speed/length;n.Vx=(n.Vx*14+dx*ratio)/15;n.Vy=(n.Vy*14+dy*ratio)/15;}
            }
            else if(n.A1==1)
            {
                n.A2++;if(n.A2>=600 || n.A3!=1)n.A1=n.A2=0;
                float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY,ratio=16/Length(dx,dy);
                n.Vx=(n.Vx*49+dx*ratio)/50;n.Vy=(n.Vy*49+dy*ratio)/50;
            }
            else if(n.A1==2){n.Vy+=.1f;if(n.Vy<0)n.Vy*=.95f;n.Vx*=.95f;n.TimeLeft=Math.Min(n.TimeLeft,500);}
            else{stop=PredictionStop.UnsupportedMechanism;return false;}
            return true;
        }
        private static bool Blade(ref NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group,int count,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int side=(int)n.A0;n.SpriteDirection=-side;
            if(!n.PositionOwnerCaptured){stop=PredictionStop.MissingDependency;return false;}
            int owner=Owner(n,group,count);
            if(n.PositionOwnerValid && owner<0){stop=PredictionStop.MissingDependency;return false;}
            if(!n.PositionOwnerValid || !group[owner].Active || group[owner].Style!=58)
            {n.Vx*=.9f;n.Vy*=.9f;if(!e.Multiplayer){n.Life=-1;n.Active=false;stop=PredictionStop.Despawn;return false;}return true;}
            var body=group[owner];
            if(!e.Multiplayer && body.A3==2){n.L1++;if(n.L1>90)n.L1=0;}
            if(e.Day){n.Vy+=.3f;n.Vx*=.9f;return true;}
            if(n.A2==0 || n.A2==3)
            {
                if(body.A1==2)n.TimeLeft=Math.Min(n.TimeLeft,10);
                n.A3++;if(n.A3>=180){n.A2++;n.A3=0;}
                if(n.TargetCaptured && !n.HasPlayer){stop=PredictionStop.MissingDependency;return false;}
                float dx=(e.PlayerX+body.Bounds.CenterX)/2,dy=(e.PlayerY+body.Bounds.CenterY)/2;
                dx+=-170*side-n.Bounds.CenterX;dy+=90-n.Bounds.CenterY;
                if(Math.Abs(e.PlayerX-body.Bounds.CenterX)+Math.Abs(e.PlayerY-body.Bounds.CenterY)>700){dx=body.Bounds.CenterX-170*side-n.Bounds.CenterX;dy=body.Bounds.CenterY+90-n.Bounds.CenterY;}
                float length=Math.Max(1,Length(dx,dy));int speed=length>1000?21:length>800?18:length>600?15:length>400?12:length>200?9:6;
                if(side<0 && n.Bounds.CenterX>body.Bounds.CenterX)dx-=4;if(side>0 && n.Bounds.CenterX<body.Bounds.CenterX)dx+=4;
                float ratio=speed/length;n.Vx=(n.Vx*14+dx*ratio)/15;n.Vy=(n.Vy*14+dy*ratio)/15;
            }
            else if(n.A2==1)
            {
                n.Vx*=.95f;n.Vy-=.3f;if(n.Vy<-14)n.Vy=-14;
                if(n.Y<body.Y-200){NpcTargeting.Face(ref n,ref e,true,confused);n.A2=2;Launch(ref n,e,18);}
            }
            else if(n.A2==2)
            {if(!n.PlayerPreciseAreaCaptured){stop=PredictionStop.MissingDependency;return false;}if(n.Y>n.PlayerPreciseArea.Y || n.Vy<0 || Distance(n,body)>800)n.A2=3;}
            else if(n.A2==4)
            {
                n.Vy*=.95f;n.Vx+=.3f*-side;if(n.Vx<-14)n.Vx=-14;if(n.Vx>14)n.Vx=14;
                if(n.Bounds.CenterX<body.Bounds.CenterX-500 || n.Bounds.CenterX>body.Bounds.CenterX+500){NpcTargeting.Face(ref n,ref e,true,confused);n.A2=5;Launch(ref n,e,17);}
            }
            else if(n.A2==5)
            {if(n.Vx>0 && n.Bounds.CenterX>e.PlayerX || n.Vx<0 && n.Bounds.CenterX<e.PlayerX || Distance(n,body)>800)n.A2=0;}
            else{stop=PredictionStop.UnsupportedMechanism;return false;}
            return true;
        }
        private static void Launch(ref NpcMotionState n,PredictionEnvironment e,float speed)
        {float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY,ratio=speed/Math.Max(1,Length(dx,dy));n.Vx=dx*ratio;n.Vy=dy*ratio;}
        private static float Distance(NpcMotionState a,NpcMotionState b){return Math.Abs(a.Bounds.CenterX-b.Bounds.CenterX)+Math.Abs(a.Bounds.CenterY-b.Bounds.CenterY);}
        private static int Owner(NpcMotionState n,NpcMotionState[] group,int count)
        {if(group!=null)for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.PositionOwner))return i;return -1;}
        private static bool Far(NpcMotionState n){return n.PlayerPreciseAreaCaptured && (Math.Abs(n.X-n.PlayerPreciseArea.X)>2000 || Math.Abs(n.Y-n.PlayerPreciseArea.Y)>2000);}
        private static float Length(float x,float y){return (float)Math.Sqrt(x*x+y*y);}
    }
}
