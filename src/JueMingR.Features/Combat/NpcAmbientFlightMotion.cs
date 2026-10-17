using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked .8 ambient motors. Only copied scalar state and the existing
    // finite terrain/group owners are accessible; no native AI or RNG runs.
    internal static class NpcAmbientFlightMotion
    {
        internal static bool Known(NpcMotionState n)
        {
            int type=n.EffectiveType;
            return n.Style==86 && (type==472 || type==521) || n.Style==42 && type==195 ||
                n.Style==122 && type==662 || n.Style==125 && type==686;
        }
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return a.A0==b.A0;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return n.Style!=122 || ValidTarget(n,e) || n.HasClosestPlayer;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain=null)
        {
            if(n.Style==42)return true;
            if(n.Style==122)return !ValidTarget(n,e);
            if(n.Style==125)return n.L0==0 && !e.Multiplayer || n.CollideY;
            return n.A0==0 || n.A0==1 || n.L0==0 || n.A0==2 && Math.Abs(n.Vx)<1 || n.A0==3 && Math.Abs(n.Vy)<1;
        }
        private static bool ValidTarget(NpcMotionState n,PredictionEnvironment e)
        {return n.AimedTargetCaptured?n.AimedTargetValid:n.TrackingKind>=2 || n.Target>=0 && n.Target<255 && (!n.TargetCaptured || n.HasPlayer && !n.PlayerDead) && !e.PlayerDead;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0)
        {
            stop=PredictionStop.None;
            if(n.Style==42)return LostGirl(ref n,e,terrain,confused,out stop);
            if(n.Style==125)return Balloon(ref n,e,terrain,confused,out stop);
            if(n.Style==122)return Ghost(ref n,e,confused,group,count,out stop);
            return Apparition(ref n,e,confused,group,count,out stop);
        }
        private static float Length(float x,float y){return (float)Math.Sqrt(x*x+y*y);}
        private static void MoveTowards(ref float x,ref float y,float targetX,float targetY,float max)
        {
            float dx=targetX-x,dy=targetY-y,d=Length(dx,dy);
            if(d<max){x=targetX;y=targetY;}
            else if(d>0){float inverse=1/d;x+=dx*inverse*max;y+=dy*inverse*max;}
        }
        private static bool Crowd(ref NpcMotionState n,NpcMotionState[] group,int count,bool reciprocal,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            // Source must close over ALL current same-type actors before
            // entering this loop, even distant ones that can approach later.
            // A current push scalar cannot stand in for future crowd geometry.
            if(!n.CrowdCaptured || !n.CrowdComplete || group==null || count<1 || count>group.Length){stop=PredictionStop.MissingDependency;return false;}
            for(int i=0;i<count;i++)
            {
                var other=group[i];if(!other.Active || other.Identity.Slot==n.Identity.Slot || other.EffectiveType!=n.EffectiveType)continue;
                float x=other.Bounds.CenterX-n.Bounds.CenterX,y=other.Bounds.CenterY-n.Bounds.CenterY,d=Length(x,y);
                if(d>=50)continue;
                if(d>0){float inverse=1/d;x*=inverse;y*=inverse;}
                // Native XNA Normalize(Zero) produces NaN before the source's
                // post-normalize zero fallback. Never publish a made-up finite
                // slot-direction escape from that invalid geometry.
                else if(reciprocal){stop=PredictionStop.InvalidState;return false;}
                else{stop=PredictionStop.InvalidState;return false;}
                if(reciprocal)
                {
                    x*=.4f;y*=.4f;n.Vx-=x;n.Vy-=y;
                    // Group work is evolved in original actor-slot order.
                    // Earlier actors already moved; later actors keep this
                    // impulse when their own finite motor is reached.
                    other.Vx+=x;other.Vy+=y;group[i]=other;
                }
                else{x*=.1f;y*=.1f;n.Vx-=x;n.Vy-=y;n.Vx-=x;}
            }
            return true;
        }
        private static bool Apparition(ref NpcMotionState n,PredictionEnvironment e,bool confused,NpcMotionState[] group,int count,out PredictionStop stop)
        {
            stop=PredictionStop.None;bool squid=n.EffectiveType==521;
            n.Alpha=Math.Max(0,n.Alpha-30);n.NoGravity=n.NoTileCollide=true;
            if(!Crowd(ref n,group,count,true,out stop))return false;
            // Both locked AI86 members have the same one-time facing/impulse
            // motor. Their separate native blocks differ in cosmetic dust,
            // which never authorizes omitting Squid's first movement action.
            if(n.L0<120)
            {
                if(n.L0==0){NpcTargeting.Face(ref n,ref e,true,confused);n.Vx+=n.Direction>0?2:-2;}
                n.L0++;
            }
            if(n.A0==0){NpcTargeting.Face(ref n,ref e,true,confused);n.A0=1;n.A1=n.Direction;}
            else if(n.A0==1)
            {
                NpcTargeting.Face(ref n,ref e,true,confused);
                float acceleration=squid?.7f:.3f,speed=squid?14:7,vertical=squid?6:4,overshoot=squid?500:660,inertia=squid?3:4;
                n.Vx=Math.Max(-speed,Math.Min(speed,n.Vx+n.A1*acceleration));
                float y=e.PlayerY-n.Bounds.CenterY;if(Math.Abs(y)>vertical)inertia=15;
                y=Math.Max(-vertical,Math.Min(vertical,y));n.Vy=(n.Vy*(inertia-1)+y)/inertia;
                float x=e.PlayerX-n.Bounds.CenterX;
                if(n.A1>0 && x< -overshoot || n.A1<0 && x>overshoot)
                {n.A0=2;n.A1=n.Bounds.CenterY+20>e.PlayerY?-1:1;}
            }
            else if(n.A0==2)
            {
                n.Vy+=n.A1*(squid?.3f:.4f);
                if(Length(n.Vx,n.Vy)>(squid?7:5)){n.Vx*=squid?.9f:.95f;n.Vy*=squid?.9f:.95f;}
                if(n.Vx> -1 && n.Vx<1){NpcTargeting.Face(ref n,ref e,true,confused);n.A0=3;n.A1=n.Direction;}
            }
            else if(n.A0==3)
            {
                n.Vx+=n.A1*(squid?.6f:.4f);n.Vy+=n.Bounds.CenterY>e.PlayerY?-(squid?.3f:.2f):(squid?.3f:.2f);
                if(Length(n.Vx,n.Vy)>(squid?7:5)){n.Vx*=squid?.9f:.95f;n.Vy*=squid?.9f:.95f;}
                if(n.Vy> -1 && n.Vy<1){NpcTargeting.Face(ref n,ref e,true,confused);n.A0=0;n.A1=n.Direction;}
            }
            else{stop=PredictionStop.UnsupportedMechanism;return false;}
            return true;
        }
        private static void RetargetAimed(ref NpcMotionState n,ref PredictionEnvironment e,bool faceTarget,bool confused)
        {
            NpcTargeting.Face(ref n,ref e,faceTarget,confused);
            if(n.AimedTargetCaptured && n.HasClosestPlayer && n.PlayerIndex==n.ClosestPlayerIndex && !n.ClosestPlayerDead)n.AimedTargetValid=true;
        }
        private static bool Ghost(ref NpcMotionState n,PredictionEnvironment e,bool confused,NpcMotionState[] group,int count,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(n.L0==0)n.L0=1;
            if(!ValidTarget(n,e))RetargetAimed(ref n,ref e,true,confused);
            if(!ValidTarget(n,e))
            {
                n.Vx*=.9f;n.Vy*=.9f;n.Alpha=Math.Min(255,n.Alpha+5);
                // Original strikes for 9999 here. Do not synthesize damage,
                // drops or a surviving actor after that terminal action.
                if(n.Alpha>=255){n.Active=false;stop=PredictionStop.Despawn;return false;}return true;
            }
            n.Alpha=Math.Max(0,n.Alpha-5);var target=NpcTargeting.Area(n,e);
            float x=0,y=0;MoveTowards(ref x,ref y,target.CenterX-n.Bounds.CenterX,target.CenterY-n.Bounds.CenterY,4);
            MoveTowards(ref n.Vx,ref n.Vy,x,y,2f/15f);
            return Crowd(ref n,group,count,false,out stop);
        }
        private static bool LostGirl(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;NpcTargeting.Face(ref n,ref e,true,confused);
            if(n.A0==0)
            {
                float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-e.PlayerHeight*.5f-n.Bounds.CenterY;
                if(n.Target>=0 && Length(x,y)<200)
                {
                    bool clear;if(!terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
                    if(clear)n.A0=1;
                }
                if(n.Vx!=0 || n.Vy<0 || n.Vy>2 || n.Life!=n.LifeMax)n.A0=1;
            }
            else if(++n.A0>=21)
            {
                n.A0=21;
                if(!e.Multiplayer)return NpcPhasedFlightMotion.TransformKnown(ref n,ref e,196,18,40,3,16,false,out stop);
            }
            return true;
        }
        private static bool Balloon(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.L0==0 && !e.Multiplayer){RetargetAimed(ref n,ref e,true,confused);n.L0=1;}
            n.SpriteDirection=n.Direction;
            float distance=float.PositiveInfinity;int extra=0;
            if(ValidTarget(n,e))
            {
                var target=NpcTargeting.Area(n,e);float x=target.CenterX-n.Bounds.CenterX,y=target.CenterY-(n.Y+n.Height);
                if(y<0)extra=(int)y/-16;distance=Length(x,y);if(n.Direction!=Math.Sign(x))extra=0;
            }
            if(n.Wet || n.CollideX || n.CollideY && n.OldVy<0)
            {
                n.X-=n.OldVx+n.Direction*8;RetargetAimed(ref n,ref e,false,confused);n.Direction*= -1;n.Vx=n.Direction*2;n.Life= -1;n.Active=false;
                stop=PredictionStop.Despawn;return false;
            }
            if(n.CollideY){n.Vy=n.OldVy>0?1:-1;RetargetAimed(ref n,ref e,false,confused);}
            float speed=3+Math.Abs(e.WindTarget)*2;
            if(Math.Sign(n.Vx)!=n.Direction || Math.Abs(n.Vx)<speed)
            {
                n.Vx+=n.Direction*.04f;
                if(n.Vx*n.Direction<0)n.Vx+=n.Direction*(Math.Abs(n.Vx)>speed?.15f:.1f);
                else if(Math.Abs(n.Vx)>speed)n.Vx=n.Direction*speed;
            }
            int tileX=(int)((n.X+n.Width/2)/16)+n.Direction,tileY=(int)((n.Y+n.Height)/16);
            bool air=true,close=false;
            // Reads remain with the bounded terrain owner. Out-of-world or
            // budget exhaustion terminates; missing cells never become air.
            for(int y=tileY;y<tileY+8+extra;y++)
            {
                PredictionTile tile;if(!terrain.Tile(tileX,y,out tile,out stop))return false;
                if(tile.Active && tile.Solid || tile.Liquid>0){close=y<tileY+5+extra;air=false;break;}
            }
            bool clear=false;
            if(distance<400 && !terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
            if(distance<400 && clear)
            {
                float npcY=n.Bounds.CenterY+n.Height/4,playerY=e.PlayerY-e.PlayerHeight*.5f+(int)e.PlayerHeight/4;
                if(npcY>playerY && n.Vy> -2){n.Vy-=.035f;if(n.Vy>0)n.Vy-=.035f;}
                else if(npcY<playerY && n.Vy<2){n.Vy+=.035f;if(n.Vy<0)n.Vy+=.035f;}
            }
            else{n.Vy+=air?.05f:-.1f;if(close)n.Vy-=.2f;n.Vy=Math.Max(-4,Math.Min(2,n.Vy));}
            return true;
        }
    }
}
