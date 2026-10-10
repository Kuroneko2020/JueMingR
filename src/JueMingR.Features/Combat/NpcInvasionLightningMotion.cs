using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI111/type578: targeting and same-type spacing share the DD2
    // pure-value closure. Dust and shot spread use real RNG without changing
    // this motor; the deterministic facing write precedes the shot's draws.
    internal static class NpcInvasionLightningMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==111 && n.EffectiveType==578;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e){return NpcInvasionTargeting.NeedsPlayer(n);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return true;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.CrowdCaptured || !n.CrowdComplete || group==null || count<1 || count>group.Length || terrain==null){stop=PredictionStop.MissingDependency;return false;}
            // Native retains this pre-query, pre-drawing Center and the old
            // sprite direction for its projectile-origin offset. FaceTarget
            // only writes direction; it has not written spriteDirection yet.
            float centerX=n.Bounds.CenterX,centerY=n.Bounds.CenterY;int oldSprite=n.SpriteDirection;
            NpcInvasionTarget target;if(!NpcInvasionTargeting.TryTarget(ref n,e,terrain,elapsed,group,count,true,confused,out target,out stop))return false;
            centerX+=-oldSprite*20;centerY+=10;
            // Native's temporary netOffset draw translation can round each
            // stored position component. Do not replace it with a no-op.
            n.X+=n.NetOffsetX;n.Y+=n.NetOffsetY;n.X-=n.NetOffsetX;n.Y-=n.NetOffsetY;
            if(n.L1==0)n.Alpha=255;if(n.L1<60){n.L1++;n.Alpha=Math.Max(0,n.Alpha-5);}
            for(int i=0;i<count;i++)
            {
                var peer=group[i];if(!peer.Active || peer.Identity.Equals(n.Identity) || peer.EffectiveType!=578 || Math.Abs(n.X-peer.X)+Math.Abs(n.Y-peer.Y)>=n.Width)continue;
                n.Vx+=n.X<peer.X?-.1f:.1f;n.Vy+=n.Y<peer.Y?-.1f:.1f;
            }
            n.SpriteDirection=n.Direction;
            float x=target.Area.CenterX-centerX,y=target.Area.CenterY-centerY,dx,dy;Unit(x,y,out dx,out dy);dx*=4;dy*=4;
            bool clear;if(!terrain.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(target.Area.CenterX,target.Area.CenterY,1,1),out clear,out stop))return false;
            if(n.L0<0)n.L0++;
            if(Length(x,y)>200 || !clear){n.Vx=Smooth(n.Vx,dx);n.Vy=Smooth(n.Vy,dy);}
            else if(y<50)n.Vy-=.03f;
            else if(n.L0>=0)
            {
                n.Vx*=.96f;n.Vy*=.96f;
                if(Length(n.Vx,n.Vy)<1 && !e.Multiplayer && ++n.L0>=5)
                {n.L0=-30;n.Direction=n.SpriteDirection=dx>0?1:-1;}
            }
            bool rise=target.Area.CenterY<n.Bounds.CenterY;
            if(!rise)
            {
                int tileX=(int)(n.Bounds.CenterX/16),tileY=(int)(n.Bounds.CenterY/16);
                // WorldGen.InWorld(point,2) suppresses this lookup at edges;
                // it does not manufacture an unavailable outside-world tile.
                if(tileX>=2 && tileX<e.WorldWidth-2 && tileY>=2 && tileY<e.WorldHeight-2)
                {
                    PredictionTile tile;if(!terrain.Tile(tileX,tileY,out tile,out stop))return false;
                    rise=tile.RawActive && tile.Active && tile.Solid && !tile.SolidTop && !tile.Half && tile.Slope==0;
                }
            }
            if(rise)n.Vy=Math.Max(-10,n.Vy-.2f);
            return true;
        }
        private static void Unit(float x,float y,out float dx,out float dy)
        {
            if(x==0 && y==0 || float.IsNaN(x) || float.IsNaN(y)){dx=0;dy=1;return;}
            double squared=(double)x*x+(double)y*y;float root=(float)Math.Sqrt(squared);double inverse=1d/root;dx=(float)(x*inverse);dy=(float)(y*inverse);
        }
        private static float Length(float x,float y){return (float)Math.Sqrt((double)x*x+(double)y*y);}
        private static float Smooth(float old,float desired)
        {
            // Vector2 multiply and add each store a float component before
            // the division operator keeps its inverse until its result store.
            float multiplied=old*19,added=multiplied+desired;return (float)(added*(1d/20));
        }
    }
}
