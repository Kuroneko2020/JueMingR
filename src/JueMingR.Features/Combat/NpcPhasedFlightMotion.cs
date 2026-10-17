using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked .8 steering families. Every action evolves its own clock and
    // motor; the observed tangent is never substituted for a future turn.
    // Future random branch/destination choices end before that action. No
    // actor, player, RNG, spawn or damage owner is reachable from this model.
    internal static class NpcPhasedFlightMotion
    {
        internal static bool Known(NpcMotionState n)
        {
            int t=n.EffectiveType;
            return n.Style==44 && (t==224 || t==509 || t==581 || t==587) ||
                n.Style==85 && (t==405 || t==421 || t==467) ||
                n.Style==88 && t==477 || n.Style==89 && t==478 || n.Style==90 && t==479 ||
                n.Style==56 && t==288 || n.Style==62 && t==347 || n.Style==63 && t==352 || n.Style==95 && t==406 ||
                n.Style==96 && t==407 || n.Style==97 && t==420 || n.Style==99 && t==519 || n.Style==100 && t==522 || n.Style==104 && t==547 || n.Style==91 && t==483;
        }
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {return a.A0==b.A0;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain=null)
        {
            if(n.Style==89 || n.Style==95)return !e.Multiplayer;
            if(n.Style==56 || n.Style==62 || n.Style==63 || n.Style==96 || n.Style==97)return true;
            if(n.Style==99 || n.Style==100 || n.Style==104)return false;
            if(n.Style==44)
            {
                if(n.Target==255 || n.PlayerDead || n.EffectiveType!=587 && n.PlayerWet || n.A0>0)return true;
                // This first CanHit reads the existing numbered target BEFORE
                // TargetClosest. The premise environment has not acquired its
                // player timeline yet, so it cannot supply that query point.
                bool clear;PredictionStop stop;
                return terrain!=null && n.HasPlayer && terrain.CanHit(
                    new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),
                    new MotionRect(n.PlayerArea.CenterX,n.PlayerArea.CenterY,1,1),out clear,out stop) && clear;
            }
            if(n.Style==85 || n.Style==91)return n.A0==0;
            return n.A0==0 || n.Target<0 || n.Target==255 || n.PlayerDead || n.Style==88 && n.A0==4;
        }
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {
            // Transform's first future query needs the captured player from
            // point zero, including clocks whose current motor is autonomous.
            if(n.Style==89 || n.Style==95)return !e.Multiplayer;
            if(n.Style==99 || n.Style==100 || n.Style==104)return false;
            if(n.Style==97 && n.A1==1)return false;
            if(n.Style==91 && n.A0==-1)return false;
            return !(n.Style==88 || n.Style==90) || n.A0!=-1 && (e.Eclipse || e.Multiplayer && n.Style==88);
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool confused,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0)
        {
            stop=PredictionStop.None;
            bool crowd=n.EffectiveType==405 || n.EffectiveType==421 || n.EffectiveType==509 || n.EffectiveType==581 || n.EffectiveType==479;
            if(crowd && (!n.CrowdCaptured || !n.CrowdComplete || group==null || count<=0)){stop=PredictionStop.MissingDependency;return false;}
            if(n.Style==44)return Hover(ref n,e,t,confused,elapsed,out stop,group,count);
            if(n.Style==85 || n.Style==91)return Sphere(ref n,e,t,confused,elapsed,out stop,group,count);
            if(n.Style==63)return Flock(ref n,e,confused,out stop);
            if(n.Style==56 || n.Style==62 || n.Style==95 || n.Style==96 || n.Style==97 || n.Style==99 || n.Style==100 || n.Style==104)
                return Event(ref n,e,t,confused,out stop);
            if(n.Style==89)
            {
                if(n.JustHit){stop=PredictionStop.RandomDecision;return false;}
                n.Vx*=n.Vy==0?.9f:.99f;int hatch=e.Expert?600:900;
                if(++n.A0>=hatch && !e.Multiplayer)
                    return TransformKnown(ref n,ref e,479,46,30,90,14,false,out stop);
                if(!e.Multiplayer && n.Vy==0 && Math.Abs(n.Vx)<.2f && n.A0>=hatch*.75f)
                {stop=PredictionStop.RandomDecision;return false;}
                return true;
            }
            return Moth(ref n,e,t,confused,elapsed,out stop,group,count);
        }
        private static bool Crowd(ref NpcMotionState n,NpcMotionState[] group,int count,bool radial,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            for(int i=0;i<count;i++)
            {
                var other=group[i];if(!other.Active || other.Identity.Slot==n.Identity.Slot || other.EffectiveType!=n.EffectiveType)continue;
                if(radial)
                {
                    float dx=other.Bounds.CenterX-n.Bounds.CenterX,dy=other.Bounds.CenterY-n.Bounds.CenterY,d=Length(dx,dy);
                    if(d>=n.Width+n.Height)continue;if(d==0){stop=PredictionStop.InvalidState;return false;}
                    dx*=.1f/d;dy*=.1f/d;n.Vx-=dx;n.Vy-=dy;other.Vx+=dx;other.Vy+=dy;group[i]=other;
                }
                else if(Math.Abs(n.X-other.X)+Math.Abs(n.Y-other.Y)<n.Width)
                {n.Vx+=n.X<other.X?-.05f:.05f;n.Vy+=n.Y<other.Y?-.05f:.05f;}
            }
            return true;
        }
        private static bool Clear(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,out bool clear,out PredictionStop stop)
        {return t.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(e.PlayerX,e.PlayerY,1,1),out clear,out stop);}
        private static float Length(float x,float y){return (float)Math.Sqrt(x*x+y*y);}
        private static void Motor(ref NpcMotionState n,float x,float y,float speed,float inertia,bool clamp=false)
        {
            float length=Length(x,y);if(length>0){x*=speed/length;y*=speed/length;}else{x=y=0;}
            n.Vx=(n.Vx*(inertia-1)+x)/inertia;n.Vy=(n.Vy*(inertia-1)+y)/inertia;
            if(clamp){length=Length(n.Vx,n.Vy);if(length>speed){n.Vx*=speed/length;n.Vy*=speed/length;}}
        }
        private static void Contact(ref NpcMotionState n)
        {
            if(n.CollideX)n.Vx=Math.Max(-4,Math.Min(4,n.Vx*-n.OldVx*.5f));
            if(n.CollideY)n.Vy=Math.Max(-4,Math.Min(4,n.Vy*-n.OldVy*.5f));
        }
        private static void Reset(ref NpcMotionState n,float phase=0){n.A0=phase;n.A1=n.A2=n.A3=0;}
        internal static bool TransformKnown(ref NpcMotionState n,ref PredictionEnvironment e,int type,int width,int height,int style,int defense,bool noGravity,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.KnownFormLifeMax<=0 || n.LifeMax<=0){stop=PredictionStop.MissingDependency;return false;}
            int life=n.Life,oldMax=n.LifeMax;
            // Native Transform preserves X, Bottom, velocity, buff slots and
            // remaining immunity. SetDefaults clears only their derived flags
            // for this action; next action reapplies the preserved timers.
            n.Y+=n.Height-height;n.Width=width;n.Height=height;n.MotionType=type;n.Style=style;
            n.NoGravity=noGravity;n.NoTileCollide=false;n.Scale=1;n.Alpha=0;n.TimeLeft=750;
            n.A0=n.A1=n.A2=n.A3=n.L0=n.L1=n.L2=n.L3=0;
            n.CollideX=n.CollideY=n.JustHit=false;n.WetCount=0;
            n.LifeMax=n.KnownFormLifeMax;n.Life=Math.Max(1,life*n.LifeMax/oldMax);
            n.Health.Defense=n.Health.DefaultDefense=defense;n.Health.RealLife=-1;n.Health.DamageMultiplier=1;
            n.Health.Regen=n.Health.RegenCount=0;n.Health.DontTakeDamage=n.Health.Immortal=n.Health.LavaImmune=n.Health.FireImmune=false;
            n.Health.ShimmerImmune=type==405;n.Health.ShimmerAction=n.SpawnedFromStatue;
            n.Friendly=n.Town=n.Boss=n.CritterTurns=n.NoContactDamage=n.CollisionPart=false;
            n.CanReceive=n.CanHarm=true;n.WaterSpeed=n.LavaSpeed=.5f;n.HoneySpeed=.25f;
            n.Target=255;n.TargetNoAggro=n.FormTargetNoAggro;n.ClosestPlayerNoAggro=n.FormClosestNoAggro;
            NpcTargeting.Face(ref n,ref e,true,false);n.NewSegment=true;
            // No second AI action: the caller retains this action's pre-AI
            // gravity and ordinary movement tail with the new collision box.
            return true;
        }
        private static void FlyAxis(ref float velocity,float desired,float acceleration)
        {if(velocity<desired){velocity+=acceleration;if(velocity<0 && desired>0)velocity+=acceleration;}else if(velocity>desired){velocity-=acceleration;if(velocity>0 && desired<0)velocity-=acceleration;}}
        private static bool Flock(ref NpcMotionState n,PredictionEnvironment e,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;NpcTargeting.Face(ref n,ref e,true,confused);
            float x=e.PlayerX-(n.Bounds.CenterX+n.Direction*20),y=e.PlayerY-(n.Bounds.CenterY+6),distance=Length(x,y);
            n.A0--;
            // Native normalizes before this return, but the near/coasting
            // branch never consumes that result. Its exact zero delta remains
            // a finite observed velocity, not a fabricated normalization.
            if(distance<200 || n.A0>0)
            {if(distance<200)n.A0=20;n.Direction=n.Vx<0?-1:1;return true;}
            float normal=11/distance;x*=normal;y*=normal;if(e.Day){x=-x;y=-y;}
            n.Vx=(n.Vx*50+x)/51;n.Vy=(n.Vy*50+y)/51;
            // These are successive native motors, not mutually exclusive
            // speed bands. A continuing trajectory must retain both below300.
            if(distance<350){n.Vx=(n.Vx*10+x)/11;n.Vy=(n.Vy*10+y)/11;}
            if(distance<300){n.Vx=(n.Vx*7+x)/8;n.Vy=(n.Vy*7+y)/8;}
            return true;
        }
        private static bool Event(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(n.Style==104){n.Active=false;stop=PredictionStop.Despawn;return false;}
            if(n.Style==95)
            {
                if(Length(n.Vx,n.Vy)>4){n.Vx*=.95f;n.Vy*=.95f;}n.Vx*=.99f;n.Vy*=.99f;
                n.A0++;n.Scale=1+.3f*Math.Max(0,Math.Min(1,n.A0/300));
                if(n.A0>=300 && !e.Multiplayer)return TransformKnown(ref n,ref e,405,44,44,85,50,true,out stop);return true;
            }
            if(n.Style==99 || n.Style==100)
            {
                bool light=n.Style==100;
                if(n.Vy==0 && (light?n.A0>=0:n.A0==0)){n.A0=light?-1:1;n.A1=0;return true;}
                if(n.A0==(light?-1:1)){n.Vx=n.Vy=0;n.X=n.OldX;n.Y=n.OldY;if(++n.A1>=5){n.Active=false;stop=PredictionStop.Despawn;return false;}return true;}
                if(!light){n.Vy=Math.Min(12,n.Vy+.2f);return true;}
                if(n.L0==0){n.L0=1;n.Vx=n.A2;n.Vy=n.A3;}
                if(n.A0>=0)
                {
                    n.A0++;
                    if(n.A0>60){float c=(float)Math.Cos(n.A1),s=(float)Math.Sin(n.A1),vx=n.Vx;n.Vx=vx*c-n.Vy*s;n.Vy=vx*s+n.Vy*c;}
                    if(n.A0>120){n.Vx*=.98f;n.Vy*=.98f;}if(Length(n.Vx,n.Vy)<.2f)n.Vx=n.Vy=0;
                }
                return true;
            }
            NpcTargeting.Face(ref n,ref e,true,confused);
            float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY;
            if(n.Style==97)
            {
                if(n.L2<180)n.L2++;
                // The hit-triggered roll changes the teleport clock. Neither
                // cosmetic/projectile RNG nor a guessed outcome is sampled.
                if(n.L1==1){stop=PredictionStop.RandomDecision;return false;}
                if(!e.Multiplayer && e.PlayerDead){n.A0=n.A2=n.A3=0;n.A1=1;}
                if(n.A1==1)
                {if(n.L3==0){n.L3=1;n.A3=3;}if(--n.A3<=0){n.Active=false;stop=PredictionStop.Despawn;return false;}return true;}
                bool clear;if(!Clear(n,e,t,out clear,out stop))return false;
                x-=n.Direction*20;y-=6;
                bool brake=Length(x,y)<=400 && clear;
                if(brake){n.Vx*=.98f;n.Vy*=.98f;}
                else
                {float distance=Length(x,y);if(distance>7){x*=7/distance;y*=7/distance;}n.Vx=(n.Vx*29+x)/30;n.Vy=(n.Vy*29+y)/30;}
                // A previously chosen destination is deterministic observed
                // state. The next unchosen terrain search is not.
                if(n.A2!=0 && n.A3!=0){n.X=n.A2*16-n.Width*.5f;n.Y=n.A3*16-n.Height*.5f;n.Vx=n.Vy=n.A2=n.A3=0;}
                if(++n.A0>=480 && !e.Multiplayer){stop=PredictionStop.RandomDestination;return false;}
                if(brake && Length(n.Vx,n.Vy)<2 && !e.Multiplayer)n.L0++;
                return true;
            }
            if(n.Style==56){Motor(ref n,x,y,12,101);return true;}
            if(n.Style==62)
            {
                x-=n.Direction*20;y-=e.PlayerHeight*.5f+6;bool clear;
                if(!Clear(n,e,t,out clear,out stop))return false;
                if(e.Day){Motor(ref n,-x,-y,7,60);n.TimeLeft=Math.Min(n.TimeLeft,10);}
                else if(Length(x,y)>600 || !clear)Motor(ref n,x,y,7,60);
                else{n.Vx*=.98f;n.Vy*=.98f;}
                // The shot clock changes no NPC motor; projectile birth is
                // outside this conditional target-trajectory responsibility.
                return true;
            }
            y-=250;float d=Length(x,y);
            if(d<20){x=n.Vx;y=n.Vy;}
            else{float speed=d<40?1.75f:d<80?3.25f:5;x*=speed/d;y*=speed/d;}
            FlyAxis(ref n.Vx,x,.15f);FlyAxis(ref n.Vy,y,.15f);if(++n.A0>=70)n.A0=0;return true;
        }
        private static bool Hover(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool confused,int elapsed,out PredictionStop stop,NpcMotionState[] group,int count)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;n.NoGravity=true;
            if(n.CollideX){n.Direction=n.OldVx>0?-1:1;n.Vx=n.Direction;}
            if(n.CollideY){n.DirectionY=n.OldVy>0?-1:1;n.Vy=n.DirectionY;}
            if(type==587){if(n.Alpha==255)n.Vy=-6;n.Alpha=Math.Max(0,n.Alpha-15);}
            bool clear=false;
            if(n.Target!=255 && !(type!=587 && e.PlayerWet) && !e.PlayerDead && !Clear(n,e,t,out clear,out stop))return false;
            if(n.Target==255 || type!=587 && e.PlayerWet || e.PlayerDead || clear)
            {n.A0=90;NpcTargeting.Face(ref n,ref e,true,confused);}
            else if(n.A0>0){n.A0--;NpcTargeting.Face(ref n,ref e,true,confused);}
            float ax=.05f,ay=.01f,sx=3,sy=1,near=30,far=100;
            float targetY=e.PlayerY-e.PlayerHeight*.5f-n.Height/2;
            if(type==509 || type==581)
            {ax=type==509?.08f:.06f;ay=type==509?.03f:.02f;sx=type==509?4.5f:4;sy=2;near=40;far=150;targetY=e.PlayerY-n.Height/2;}
            else if(type==587)
            {ax=.13f;ay=.09f;sx=6.5f;sy=3.5f;near=0;far=250;targetY=e.Day?0:e.PlayerY-e.PlayerHeight*.5f;if(e.Day)n.Direction=-n.Direction;}
            if((type==509 || type==581) && !Crowd(ref n,group,count,false,out stop))return false;
            float distance=Math.Abs(n.X+n.Width/2-(e.PlayerX-e.PlayerWidth*.5f+(int)e.PlayerWidth/2));
            if(n.A0<=0){sx*=.8f;ax*=.7f;targetY=n.Bounds.CenterY+n.DirectionY*1000;if(n.Vx<0)n.Direction=-1;else if(n.Vx>0 || n.Direction==0)n.Direction=1;}
            if(distance>near)
            {
                if(n.Direction==-1 && n.Vx>-sx){n.Vx-=ax;if(n.Vx>sx)n.Vx-=ax;else if(n.Vx>0)n.Vx-=ax/2;n.Vx=Math.Max(-sx,n.Vx);}
                else if(n.Direction==1 && n.Vx<sx){n.Vx+=ax;if(n.Vx<-sx)n.Vx+=ax;else if(n.Vx<0)n.Vx+=ax/2;n.Vx=Math.Min(sx,n.Vx);}
            }
            if(distance>far)targetY-=far/2;
            if(n.Y<targetY){n.Vy+=ay;if(n.Vy<0)n.Vy+=ay;}else{n.Vy-=ay;if(n.Vy>0)n.Vy-=ay;}
            n.Vy=Math.Max(-sy,Math.Min(sy,n.Vy));
            if(type!=587 && n.Wet){if(n.Vy>0)n.Vy*=.95f;n.Vy=Math.Max(-4,n.Vy-.5f);}
            return true;
        }
        private static bool Sphere(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool confused,int elapsed,out PredictionStop stop,NpcMotionState[] group,int count)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;bool stardust=n.Style==91;n.NoTileCollide=false;
            if(stardust)
            {
                n.NoGravity=true;n.Health.DontTakeDamage=false;n.CanReceive=true;
                if(n.JustHit && !e.Multiplayer && e.Expert){stop=PredictionStop.RandomDecision;return false;}
                if(n.A0==-1){n.Health.DontTakeDamage=true;n.CanReceive=false;n.NoGravity=false;n.Vx*=.98f;if(++n.A1>=120)Reset(ref n);return true;}
            }
            float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY;bool clear,solid;
            if(n.A0==0)
            {
                NpcTargeting.Face(ref n,ref e,true,confused);x=e.PlayerX-n.Bounds.CenterX;y=e.PlayerY-n.Bounds.CenterY;
                if(!Clear(n,e,t,out clear,out stop))return false;
                if(clear)n.A0=1;
                else if(!stardust && Length(x,y-(int)e.PlayerHeight/4)>800)n.A0=2;
                else
                {
                    var center=new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1);bool first,second;
                    if(!t.CanHit(center,new MotionRect(e.PlayerX,n.Bounds.CenterY,1,1),out first,out stop))return false;
                    if(Math.Abs(x)>8 && first)
                    {
                        n.A0=3;n.A1=e.PlayerX;n.A2=n.Bounds.CenterY;
                        if(!t.CanHit(center,new MotionRect(n.Bounds.CenterX,e.PlayerY,1,1),out second,out stop))return false;
                        if(second){if(!t.CanHit(new MotionRect(n.Bounds.CenterX,e.PlayerY,1,1),new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,1,1),out second,out stop))return false;if(second){n.A1=n.Bounds.CenterX;n.A2=e.PlayerY;}}
                    }
                    else
                    {if(!t.CanHit(center,new MotionRect(n.Bounds.CenterX,e.PlayerY,1,1),out second,out stop))return false;if(Math.Abs(y)>8 && second){n.A0=3;n.A1=n.Bounds.CenterX;n.A2=e.PlayerY;}}
                    if(n.A0==0){n.L0=0;float d=Length(x,y-(int)e.PlayerHeight/4);if(d>0){n.Vx+=x*.5f/d;n.Vy+=(y-(int)e.PlayerHeight/4)*.5f/d;}n.A0=4;n.A1=0;}
                }
            }
            else if(n.A0==1)
            {
                float chaseY=y-(type==421?e.PlayerHeight*.5f:0),d=Length(x,chaseY);
                Motor(ref n,x,chaseY,stardust?2+d/200:(type==405?8:5.5f)+d/100,50);
                if(!Clear(n,e,t,out clear,out stop))return false;if(!clear){n.A0=n.A1=0;}
                if(type==421 && d<40 && !e.PlayerDead)
                {
                    bool occupied=false;
                    for(int i=0;i<count;i++)if(group[i].Active && group[i].EffectiveType==421 && group[i].A0==5 && group[i].Target==n.Target){occupied=true;break;}
                    if(!occupied)
                    {n.X=e.PlayerX-n.Width*.5f;n.Y=e.PlayerY-e.PlayerHeight*.5f-n.Height*.5f;n.Vx=n.Vy=0;n.A0=5;n.A1=0;}
                }
            }
            else if(n.A0==2)
            {
                n.NoTileCollide=true;float d=Length(x,y);Motor(ref n,x,y,stardust?2:type==405?6:3,stardust?4:3);
                if(d<600){if(!t.Solid(n.Bounds,out solid,out stop))return false;if(!solid)n.A0=0;}
            }
            else if(n.A0==3)
            {
                float dx=n.A1-n.Bounds.CenterX,dy=n.A2-n.Bounds.CenterY,d=Length(dx,dy),speed=stardust?1:type==405?3:2;Motor(ref n,dx,dy,speed,3);
                if(n.CollideX || n.CollideY){n.A0=4;n.A1=0;}
                if(!Clear(n,e,t,out clear,out stop))return false;if(d<speed || d>800 || clear)n.A0=0;
            }
            else if(n.A0==4)
            {
                if(n.CollideX)n.Vx*= -.8f;if(n.CollideY)n.Vy*= -.8f;
                if(n.Vx==0 && n.Vy==0){float d=Length(x,y-(int)e.PlayerHeight/4);if(d>0){n.Vx=x*.1f/d;n.Vy=(y-(int)e.PlayerHeight/4)*.1f/d;}}
                Motor(ref n,n.Vx,n.Vy,stardust?1.5f:type==405?3:2,20);
                if(++n.A1>180){n.A0=n.A1=0;}
                if(!Clear(n,e,t,out clear,out stop))return false;if(clear)n.A0=0;
                if(++n.L0>=5)
                {
                    if(!t.Solid(new MotionRect(n.X-10,n.Y-10,n.Width+20,n.Height+20),out solid,out stop))return false;
                    if(!solid)
                    {
                        n.L0=0;var start=new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1);var cross=new MotionRect(e.PlayerX,n.Bounds.CenterY,1,1);bool a,b;
                        if(!t.CanHit(start,cross,out a,out stop) || !t.CanHit(new MotionRect(e.PlayerX,e.PlayerY,1,1),cross,out b,out stop))return false;
                        if(a && b){n.A0=3;n.A1=cross.X;n.A2=cross.Y;}
                        else{cross=new MotionRect(n.Bounds.CenterX,e.PlayerY,1,1);if(!t.CanHit(start,cross,out a,out stop) || !t.CanHit(new MotionRect(e.PlayerX,e.PlayerY,1,1),cross,out b,out stop))return false;if(a && b){n.A0=3;n.A1=cross.X;n.A2=cross.Y;}}
                    }
                }
            }
            else if(n.A0==5)
            {
                if(e.PlayerDead){n.A0=n.A1=0;}
                else
                {
                    if(!n.TargetCaptured || !n.HasPlayer || Math.Abs(n.PlayerDirection)!=1 || Math.Abs(n.PlayerGravityDirection)!=1)
                    {stop=PredictionStop.MissingDependency;return false;}
                    n.X=e.PlayerX+n.PlayerDirection*4-n.Width*.5f;
                    n.Y=e.PlayerY+(n.PlayerGravityDirection==1?-e.PlayerHeight*.5f:e.PlayerHeight*.5f)-n.Height*.5f;
                    n.Vx=n.Vy=0;
                    // Native's blackout buff is presentation, not movement;
                    // never apply it to a real or forecast Player actor here.
                }
            }
            else{stop=PredictionStop.UnsupportedMechanism;return false;}
            if((type==405 || type==421) && !Crowd(ref n,group,count,false,out stop))return false;
            return true;
        }
        private static bool Moth(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,bool confused,int elapsed,out PredictionStop stop,NpcMotionState[] group,int count)
        {
            stop=PredictionStop.None;bool baby=n.Style==90;n.NoGravity=true;n.NoTileCollide=false;
            if(baby && e.Eclipse && (n.A0==0 || n.A0==1) && !Crowd(ref n,group,count,true,out stop))return false;
            if(!e.Eclipse && (baby || !e.Multiplayer))
            {if(baby){n.TimeLeft=Math.Min(n.TimeLeft,5);n.Vy=Math.Max(-8,n.Vy-.2f);n.NoTileCollide=true;return true;}n.A0=-1;}
            if(n.Target<0 || n.Target==255 || e.PlayerDead)
            {NpcTargeting.Face(ref n,ref e,true,confused);if(!e.Multiplayer || baby){if(e.PlayerDead || Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY)>3000)n.A0=-1;}}
            else if((!e.Multiplayer || baby) && n.A0>1 && Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY)>1000)n.A0=1;
            float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY;bool clear,solid;
            if(n.A0==-1){Motor(ref n,0,-8,8,10);n.NoTileCollide=true;n.CanReceive=false;n.Health.DontTakeDamage=true;return true;}
            if(n.A0==0)
            {
                if(!baby && !e.Multiplayer && n.JustHit){stop=PredictionStop.RandomDecision;return false;}
                if(!baby && !e.Multiplayer && n.A1+1>=180){stop=PredictionStop.RandomDecision;return false;}
                NpcTargeting.Face(ref n,ref e,true,confused);x=e.PlayerX-n.Bounds.CenterX;y=e.PlayerY-n.Bounds.CenterY-(baby?0:200);
                Contact(ref n);float d=Length(x,y);
                if(d>800)Reset(ref n,1);
                else if(d>(baby?200:80))Motor(ref n,x,y,baby?5.5f+d/100+n.A1/15:6,baby?40:30);
                else{float speed=Length(n.Vx,n.Vy);if(speed>2){n.Vx*=.95f;n.Vy*=.95f;}else if(speed<1){n.Vx*=1.05f;n.Vy*=1.05f;}}
                if(baby){if(++n.A1>=90){n.A1=0;n.A0=2;}}else if(!e.Multiplayer)n.A1++;
            }
            else if(n.A0==1)
            {
                n.CollideX=n.CollideY=false;n.NoTileCollide=true;float d=Length(x,y);
                if((baby || !e.Multiplayer) && d<300){if(!t.Solid(n.Bounds,out solid,out stop))return false;if(!solid)Reset(ref n);}
                if(baby)n.A2+=1/60f;Motor(ref n,x,y,baby?5.5f+n.A2+d/150:7+d/100,baby?35:25);
            }
            else if(n.A0==2)
            {
                if(baby)
                {
                    n.NoTileCollide=true;y-=8;Motor(ref n,x,y,9,8);
                    if(++n.A1>10){float d=Length(x,y);if(d>0){n.Vx=x*9/d;n.Vy=y*9/d;}n.A0=2.1f;n.A1=0;}
                }
                else
                {
                    Contact(ref n);y-=20;n.A2+=1/45f;if(e.Expert)n.A2+=1/60f;Motor(ref n,x,y,4+n.A2+Length(x,y)/120,20);
                    if(!e.Multiplayer){if(!Clear(n,e,t,out clear,out stop))return false;if(++n.A1>240 || !clear)Reset(ref n);}
                }
            }
            else if(baby && n.A0==2.1f)
            {
                n.NoTileCollide=true;n.Vx*=1.01f;n.Vy*=1.01f;
                if(++n.A1>45){if(!t.Solid(n.Bounds,out solid,out stop))return false;if(!solid){n.A0=n.A1=n.A2=0;}else if(n.A1>90){n.A0=1;n.A1=n.A2=0;}}
            }
            else if(!baby && n.A0==3)
            {
                n.NoTileCollide=true;y-=12;x+=n.Bounds.CenterX>e.PlayerX?400:-400;
                if(!e.Multiplayer && Math.Abs(n.Bounds.CenterX-e.PlayerX)>350 && Math.Abs(n.Bounds.CenterY-e.PlayerY)<20){n.A0=3.1f;n.A1=0;}
                n.A1+=1/30f;Motor(ref n,x,y,8+n.A1,4);
            }
            else if(!baby && n.A0==3.1f)
            {
                n.NoTileCollide=true;y-=12;Motor(ref n,x,y,16,8);
                n.A1++;if(!e.Multiplayer && n.A1>10){float d=Length(x,y);if(d>0){n.Vx=x*16/d;n.Vy=y*16/d;}n.Direction=n.Vx<0?-1:1;n.A0=3.2f;n.A1=n.Direction;}
            }
            else if(!baby && n.A0==3.2f)
            {
                n.CollideX=n.CollideY=false;n.NoTileCollide=true;n.A2+=1/30f;n.Vx=(16+n.A2)*n.A1;
                if(!e.Multiplayer && (n.A1>0 && n.Bounds.CenterX>e.PlayerX+260 || n.A1<0 && n.Bounds.CenterX<e.PlayerX-260))
                {if(!t.Solid(n.Bounds,out solid,out stop))return false;if(!solid)Reset(ref n);else if(Math.Abs(n.Bounds.CenterX-e.PlayerX)>800)Reset(ref n,1);}
            }
            else if(!baby && n.A0==4){stop=PredictionStop.RandomDestination;return false;}
            else if(!baby && (n.A0==4.1f || n.A0==4.2f))
            {
                n.NoTileCollide=true;x=(int)n.A1*16+8-n.Bounds.CenterX;y=(int)n.A2*16-20-n.Bounds.CenterY;float d=Length(x,y);
                if(n.A0==4.1f){float speed=Math.Min(10,6+d/150);if(d<10)n.A0=4.2f;Motor(ref n,x,y,speed,10,true);}
                else
                {
                    if(!e.Multiplayer && d<4){int wait=e.Expert?52:70;if(n.A3+1==wait*2){stop=PredictionStop.RandomDecision;return false;}n.A3++;}
                    // Egg birth is not a movement consumer. The forecast does
                    // not create it; the later random re-entry is a hard bound.
                    if(d>4)Motor(ref n,x,y,4,2,true);
                    else{n.Vx=(n.Vx+x)/2;n.Vy=(n.Vy+y)/2;float speed=Length(n.Vx,n.Vy);if(speed>4){n.Vx*=4/speed;n.Vy*=4/speed;}}
                }
            }
            else{stop=PredictionStop.UnsupportedMechanism;return false;}
            n.SpriteDirection=n.Direction;return true;
        }
    }
}
