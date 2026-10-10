using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked .8 AI51/52/53. The native global owner is a slot-ordered
    // relation, independent of an encoded tentacle position owner and life.
    // These kernels use a complete scalar closure, never native AI or RNG.
    internal static class NpcPlanteraMotion
    {
        internal static bool Known(NpcMotionState n)
        {return n.EffectiveType==262 && n.Style==51 || n.EffectiveType==263 && n.Style==52 || n.EffectiveType==264 && n.Style==53;}
        internal static bool NeedsPlayer(NpcMotionState n){return n.Style==51;}
        internal static bool Retargets(NpcMotionState n){return n.Style==51 || n.Style==53;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return a.Style==b.Style;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            // GoodWorld AI53 rolls its receiving reflection before any motor.
            // Preserve the known path; attack reachability must respect this
            // unknown future receiving choice, rather than freeze today's roll.
            if(n.Style==53)n.ProjectileReflectionUnknown=e.GoodWorld;
            if(!n.PlantRelationsCaptured || !n.PlantRelationsComplete || group==null || count<1 || count>group.Length)
            {stop=PredictionStop.MissingDependency;return false;}
            if(n.Style==51)return Body(ref n,e,group,count,confused,out stop);
            NpcMotionState boss;bool absent;
            if(!Boss(n,group,count,elapsed,out boss,out absent)){stop=PredictionStop.MissingDependency;return false;}
            if(absent){n.Active=false;stop=PredictionStop.Despawn;return false;}
            if(n.Style==52)return Hook(ref n,boss,e,out stop);
            return Tentacle(ref n,boss,e,group,count,confused,out stop);
        }
        private static bool Boss(NpcMotionState n,NpcMotionState[] group,int count,int elapsed,out NpcMotionState boss,out bool absent)
        {
            int before=-1,last=-1;
            for(int i=0;i<count;i++)if(group[i].Active && group[i].Style==51 && group[i].EffectiveType==262)
            {
                if(last<0 || group[i].Identity.Slot>group[last].Identity.Slot)last=i;
                if(group[i].Identity.Slot<n.Identity.Slot && (before<0 || group[i].Identity.Slot>group[before].Identity.Slot))before=i;
            }
            // Rows before this actor have already advanced in this action.
            // The first action without such a row preserves the actual observed
            // global, including a newly born higher-slot boss. Later actions
            // start from the previous action's last body. No live global is read.
            int selected=before>=0?before:elapsed>1?last:-1;absent=false;
            if(selected>=0){boss=group[selected];return true;}
            boss=default(NpcMotionState);
            if(!n.PlantBossValid){absent=true;return true;}
            for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.PlantBossIdentity))
            {boss=group[i];if(!boss.Active || boss.EffectiveType!=262)absent=true;return true;}
            return false;
        }
        private static bool Body(ref NpcMotionState n,PredictionEnvironment e,NpcMotionState[] group,int count,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(!e.Multiplayer && n.L0==0){stop=PredictionStop.PhaseBoundary;return false;}
            NpcTargeting.Face(ref n,ref e,true,confused);
            if(!n.TargetCaptured || !n.HasPlayer || !n.PlayerVelocityCaptured){stop=PredictionStop.MissingDependency;return false;}
            bool dead=n.PlayerDead,enraged=dead || Outside(n,e);
            if(!dead && !e.Multiplayer && n.TimeLeft<750 && Distance(n.Bounds.CenterX,n.Bounds.CenterY,e.PlayerX,e.PlayerY)<4800)n.TimeLeft=750;
            float x=0,y=0;int hooks=0;
            for(int i=0;i<count && hooks<3;i++)if(group[i].Active && group[i].Style==52)
            {x+=group[i].Bounds.CenterX;y+=group[i].Bounds.CenterY;hooks++;}
            if(hooks==0){stop=PredictionStop.InvalidState;return false;}x/=hooks;y/=hooks;
            float speed=2.5f,accel=.025f;
            if(n.Life<n.LifeMax/2){speed=5;accel=.05f;}if(n.Life<n.LifeMax/4)speed=7;
            if(Outside(n,e)){speed+=8;accel=.15f;}
            if(e.Expert){speed+=1;speed*=1.1f;accel+=.01f;accel*=1.1f;}
            if(e.GoodWorld){speed*=1.15f;accel*=1.15f;}
            float dx=e.PlayerX-x,dy=e.PlayerY-y;if(dead){dx*=-1;dy*=-1;speed+=8;}
            float length=Length(dx,dy),radius=500+(enraged?350:0)+(e.Expert?150:0);
            if(length>=radius){float ratio=radius/length;dx*=ratio;dy*=ratio;}
            dx=x+dx-n.Bounds.CenterX;dy=y+dy-n.Bounds.CenterY;length=Length(dx,dy);
            if(length<speed){dx=n.Vx;dy=n.Vy;}else{float ratio=speed/length;dx*=ratio;dy*=ratio;}
            BodyAxis(ref n.Vx,dx,accel);BodyAxis(ref n.Vy,dy,accel);
            // Attack/projectile RNG does not feed this body motor. Its actual
            // second-phase child birth also leaves the first-three hook mean
            // unchanged; selected children acquire their own real identities.
            if(!e.Multiplayer && n.Life<=n.LifeMax/2 && n.L0==1)n.L0=2;
            return true;
        }
        private static bool Hook(ref NpcMotionState n,NpcMotionState boss,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(!boss.TargetCaptured || !boss.HasPlayer || !boss.PlayerVelocityCaptured){stop=PredictionStop.MissingDependency;return false;}
            bool dead=boss.PlayerDead,enraged=dead || Outside(boss,e);
            if(enraged)n.L0-=4;
            if(e.Multiplayer)
            {if(n.A0==0)n.A0=(int)(n.Bounds.CenterX/16);if(n.A1==0)n.A1=(int)(n.Bounds.CenterX/16);}
            else
            {
                if(n.A0==0 || n.A1==0)n.L0=0;n.L0--;
                if(boss.Life<boss.LifeMax/2)n.L0-=2;if(boss.Life<boss.LifeMax/4)n.L0-=2;if(enraged)n.L0-=6;
                // Both the moving-peer delay and the tile search consume RNG.
                // A positive chosen clock continues through exact arrival.
                if(n.L0<=0){stop=PredictionStop.RandomDestination;return false;}
            }
            if(n.A0<=0 || n.A1<=0)return true;
            float speed=6;if(boss.Life<boss.LifeMax/2)speed=8;if(boss.Life<boss.LifeMax/4)speed=10;
            if(e.Expert)speed++;if(e.Expert && boss.Life<boss.LifeMax/2)speed++;if(enraged)speed*=2;if(dead)speed*=2;
            float dx=n.A0*16-8-n.Bounds.CenterX,dy=n.A1*16-8-n.Bounds.CenterY,length=Length(dx,dy);
            if(length<12+speed)
            {
                if(!e.Multiplayer && e.GoodWorld && n.L3==1){stop=PredictionStop.PhaseBoundary;return false;}
                n.Vx=dx;n.Vy=dy;
            }
            else
            {if(!e.Multiplayer && e.GoodWorld)n.L3=1;float ratio=speed/length;n.Vx=dx*ratio;n.Vy=dy*ratio;}
            return true;
        }
        private static bool Tentacle(ref NpcMotionState n,NpcMotionState boss,PredictionEnvironment e,NpcMotionState[] group,int count,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;var owner=boss;
            if(!e.Multiplayer){n.L0--;if(n.L0<=0){stop=PredictionStop.RandomDestination;return false;}}
            NpcTargeting.Face(ref n,ref e,true,confused);
            float accel=.2f,range=200;
            if((double)boss.Life<boss.LifeMax*.25)range+=100;if((double)boss.Life<boss.LifeMax*.1)range+=100;
            if(e.Expert){range+=(1-(float)n.Life/n.LifeMax)*300;accel+=.3f;}if(e.GoodWorld)accel+=4;
            if(n.A3>0)
            {
                if(n.PositionParameter<0){n.Active=false;stop=PredictionStop.Despawn;return false;}
                int index=-1;for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.PositionOwner)){index=i;break;}
                if(index<0){stop=PredictionStop.MissingDependency;return false;}owner=group[index];
                if(!owner.Active){n.Active=false;stop=PredictionStop.Despawn;return false;}
            }
            float x=owner.X+owner.Width/2,y=owner.Y+owner.Height/2,dx=(x+n.A0)-x,dy=(y+n.A1)-y,length=Length(dx,dy);
            if(length==0){stop=PredictionStop.InvalidState;return false;}float ratio=range/length;dx*=ratio;dy*=ratio;
            TentacleAxis(ref n.Vx,n.X,x+dx,dx,accel);TentacleAxis(ref n.Vy,n.Y,y+dy,dy,accel);
            if(dx>0)n.SpriteDirection=1;if(dx<0)n.SpriteDirection=-1;
            return true;
        }
        private static bool Outside(NpcMotionState playerOwner,PredictionEnvironment e)
        {float y=playerOwner.PlayerCenterY-playerOwner.PlayerArea.Height*.5f;return !playerOwner.PlayerJungle || (double)y<e.GravityWorldSurface*16 || y>(e.WorldHeight-200)*16f;}
        private static float Length(float x,float y){return (float)Math.Sqrt(x*x+y*y);}
        private static float Distance(float x,float y,float a,float b){return Length(x-a,y-b);}
        private static void BodyAxis(ref float velocity,float desired,float accel)
        {if(velocity<desired){velocity+=accel;if(velocity<0 && desired>0)velocity+=accel*2;}else if(velocity>desired){velocity-=accel;if(velocity>0 && desired<0)velocity-=accel*2;}}
        private static void TentacleAxis(ref float velocity,float position,float desired,float offset,float accel)
        {if(position<desired){velocity+=accel;if(velocity<0 && offset>0)velocity*=.9f;}else if(position>desired){velocity-=accel;if(velocity>0 && offset<0)velocity*=.9f;}velocity=Math.Max(-8,Math.Min(8,velocity));}
    }
}
