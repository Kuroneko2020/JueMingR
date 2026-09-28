using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // These are local motion models, not a second world simulator. No model
    // receives a live entity or calls native AI, damage, RNG, spawn or network.
    // Unknown decisions end the horizon before that decision. Approximate
    // families remain explicitly different from audited deterministic phases.
    public static class NpcMotion
    {
        public static PredictionAssumption Assumptions(NpcMotionState n)
        {
            var result=PredictionAssumption.TargetPlayerStationary|PredictionAssumption.FixedTarget|PredictionAssumption.NoNewHits|PredictionAssumption.LocalTerrain;
            if(n.Identity.Type==371)result|=PredictionAssumption.RandomRepresentative;
            // Terrain contacts and unlisted variants are qualified separately
            // from the audited air-motion families. A shared aiStyle alone is
            // not evidence that every variant has the same movement formula.
            if(!n.NoTileCollide || n.Style==3 || n.Style==6 || n.Style==37 || n.Style==1 || !KnownMotion(n))result|=PredictionAssumption.ApproximateMechanism;
            if(n.BuffFingerprint!=0)result|=PredictionAssumption.ApproximateMechanism;
            return result;
        }
        public static bool Step(ref NpcMotionState n,NpcMotionState[] group,int count,PredictionEnvironment env,IPredictionTerrain terrain,int elapsed,out PredictionStop stop)
        {
            stop=PredictionStop.None;n.NewSegment=false;
            if(!n.Active || n.TimeLeft<=elapsed){stop=PredictionStop.Despawn;return false;}
            if(n.BuffExpires>0 && elapsed>=n.BuffExpires){stop=PredictionStop.BuffTransition;return false;}
            if(!Finite(n.X) || !Finite(n.Y) || !Finite(n.Vx) || !Finite(n.Vy)){stop=PredictionStop.InvalidState;return false;}
            if(n.Identity.Type==488){n.Vx=n.Vy=0;return true;}
            int direction=env.PlayerX<n.Bounds.CenterX?-1:1, vertical=env.PlayerY<n.Bounds.CenterY?-1:1;
            if(n.ConfusedTicks>0){direction=-direction;n.ConfusedTicks--;}
            bool linked=false;
            if(n.Identity.Type==371)Bubble(ref n,env);
            else if(n.Identity.Type==372 || n.Identity.Type==373)
            {if(!Shark(ref n,env,terrain,out stop))return false;}
            else if(n.Style==69)
            {if(!Duke(ref n,env,out stop))return false;}
            else if(n.Style==8)
            {
                // A destination already sent by vanilla is a known jump. A
                // future random search is not a licence to invent a tile.
                if(n.A2!=0 && n.A3!=0){n.X=n.A2*16-n.Width/2+8;n.Y=n.A3*16-n.Height;n.Vx=n.Vy=0;n.A2=n.A3=0;n.NewSegment=true;}
                n.Vx*=.93f;if(n.A0==0)n.A0=500;n.A0++;
                int type=n.Identity.Type;
                float boundary=type==283 || type==284?450:type==281 || type==282?540:type==285 || type==286?401:type==533?360:650;
                if(n.A0>=boundary){stop=PredictionStop.RandomDestination;return false;}
            }
            else if(n.Style==6 || n.Style==37)
            {
                if(n.ParentSlot>=0)
                {
                    int parent=-1;for(int i=0;i<count;i++)if(group[i].Identity.Slot==n.ParentSlot){parent=i;break;}
                    if(parent<0 || !group[parent].Active){stop=PredictionStop.MissingDependency;return false;}
                    float dx=group[parent].Bounds.CenterX-n.Bounds.CenterX,dy=group[parent].Bounds.CenterY-n.Bounds.CenterY;
                    float distance=(float)Math.Sqrt(dx*dx+dy*dy),spacing=n.Width;
                    int type=n.Identity.Type;
                    if(n.Style==37)spacing=(int)(44*n.Scale);
                    else if(type>=87 && type<=92)spacing=42;
                    else if(type>=454 && type<=459)spacing=36;
                    else if(type>=513 && type<=515)spacing-=6;
                    else if(type>=412 && type<=414)spacing+=6;
                    else if(type>=621 && type<=623)spacing=24;
                    if(distance>.001f){float amount=(distance-spacing)/distance;n.X+=dx*amount;n.Y+=dy*amount;}
                    n.Vx=n.Vy=0;linked=true;
                }
                else
                {
                    // Burrowing heads react to the local solid/air phase. The
                    // per-species turn rate is approximate, not a body integrator.
                    bool solid;if(!terrain.Solid(n.Bounds,out solid,out stop))return false;
                    if(solid || n.Style==37)Seek(ref n,env.PlayerX,env.PlayerY,8,.08f);
                    else{n.Vy=Math.Min(8,n.Vy+.11f);n.Vx*=.99f;}
                }
            }
            else if(n.Style==1)
            {if(n.A0==-999){stop=PredictionStop.PhaseBoundary;return false;}Slime(ref n,env,direction,vertical);}
            else if(n.Style==3)
            {
                n.Direction=direction;
                n.Vx=Approach(n.Vx,direction*1.5f,.07f);
                if(n.CollideX && n.CollideY){n.Vy=-6;n.CollideY=false;}
                if(n.A3>0){stop=PredictionStop.PhaseBoundary;return false;}
            }
            else if(n.Style==2 && (n.Identity.Type==2 || n.Identity.Type==133 || n.Identity.Type>=190 && n.Identity.Type<=194))
            {
                n.NoGravity=true;
                if(env.Day){stop=PredictionStop.PhaseBoundary;return false;}
                Bounce(ref n);n.Direction=direction;n.DirectionY=vertical;
                float sx=4*(2-n.Scale),sy=1.5f*(2-n.Scale);
                EyeAxis(ref n.Vx,direction,sx,.1f,.1f,.05f);EyeAxis(ref n.Vy,vertical,sy,.04f,.05f,.03f);
                if(n.Wet){if(n.Vy>0)n.Vy*=.95f;n.Vy=Math.Max(-4,n.Vy-.5f);}
            }
            else if(n.Style==5 && FlyingType(n.Identity.Type))Flying(ref n,env,direction,vertical);
            else if(n.Style==14 && BatType(n.Identity.Type))Bat(ref n,env,direction,vertical);
            else if(elapsed>12){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.Active){stop=PredictionStop.Despawn;return false;}
            if(linked)return true;
            // Vanilla gravity uses the previous wet state before AI/movement.
            float gravity=.3f,fall=10;
            float worldScale=env.WorldWidth/4200f;worldScale*=worldScale;
            gravity*=Clamp((n.Y/16-(60+10*worldScale))/Math.Max(1,env.WorldSurface/6),.25f,1);
            if(n.Wet){gravity=n.Honey?.1f:.2f;fall=n.Honey?4:7;}
            n.Gravity=gravity;n.MaxFall=fall;
            if(!n.NoGravity)n.Vy=Math.Min(fall,n.Vy+gravity);
            if(Math.Abs(n.Vx)<.005f)n.Vx=0;
            // oldVelocity belongs to native UpdateCollision. NoTileCollide
            // bypasses that owner; changing it here would defeat rolling reuse
            // for a correctly predicted Sharkron/Duke step.
            return terrain.Move(ref n,out stop);
        }
        private static bool FlyingType(int t){return t==6 || t==173 || t==42 || t>=231 && t<=235;}
        private static bool BatType(int t){return t==49 || t==51 || t==60 || t==62 || t==66 || t==93 || t==137 || t==150 || t==151 || t==152 || t==634;}
        private static bool KnownMotion(NpcMotionState n)
        {int t=n.Identity.Type;return t==488 || t>=370 && t<=373 || n.Style==1 || n.Style==3 || n.Style==6 || n.Style==8 || n.Style==37 || n.Style==2 && (t==2 || t==133 || t>=190 && t<=194) || n.Style==5 && FlyingType(t) || n.Style==14 && BatType(t);}
        private static void Flying(ref NpcMotionState n,PredictionEnvironment env,int direction,int vertical)
        {
            n.NoGravity=true;
            bool eater=n.Identity.Type==6 || n.Identity.Type==173;
            float speed=eater?(env.Remix?5:4):(n.Identity.Type==231?3:3.5f)*(2-n.Scale);
            float acc=eater?(env.Remix?.06f:n.Identity.Type==6 && env.Expert?.035f:.02f):(n.Identity.Type==231?.017f:.021f)*(2-n.Scale);
            float targetTop=env.PlayerY-env.PlayerHeight*.5f;
            if(!eater && n.Y/16<env.WorldSurface && (targetTop-n.Y>300 && n.Vy<0 || targetTop-n.Y<80 && n.Vy>0))n.Vy*=.97f;
            float dx=(int)(env.PlayerX/8)*8-(int)(n.Bounds.CenterX/8)*8,dy=(int)(env.PlayerY/8)*8-(int)(n.Bounds.CenterY/8)*8;
            float distance=(float)Math.Sqrt(dx*dx+dy*dy);
            if(distance==0){dx=n.Vx;dy=n.Vy;}else{dx*=speed/distance;dy*=speed/distance;}
            if(!eater || distance>100){n.A0++;n.Vy+=n.A0>0?.023f:-.023f;n.Vx+=n.A0<-100 || n.A0>100?.023f:-.023f;if(n.A0>200)n.A0=-200;}
            if(eater && distance<150){n.Vx+=dx*.007f;n.Vy+=dy*.007f;}
            if(n.Vx<dx)n.Vx+=acc;else if(n.Vx>dx)n.Vx-=acc;
            if(n.Vy<dy)n.Vy+=acc;else if(n.Vy>dy)n.Vy-=acc;
            Bounce(ref n,eater?.4f:.7f,1.5f,2);
            n.Direction=direction;n.DirectionY=vertical;
            if(n.Wet){if(n.Vy>0)n.Vy*=.95f;n.Vy=Math.Max(eater?-2:-4,n.Vy-(eater?.3f:.5f));}
        }
        private static void Bat(ref NpcMotionState n,PredictionEnvironment env,int direction,int vertical)
        {
            n.NoGravity=true;Bounce(ref n,.5f,1,1);n.Direction=direction;n.DirectionY=vertical;
            EyeAxis(ref n.Vx,direction,4,.1f,.1f,.05f);EyeAxis(ref n.Vy,vertical,1.5f,.04f,.05f,.03f);
            if(n.Wet){if(n.Vy>0)n.Vy*=.95f;n.Vy=Math.Max(-4,n.Vy-.5f);}
            // Vanilla deliberately applies the second axis pass in the same AI
            // update. The periodic block still runs after it resets its timer.
            bool hell=n.Identity.Type==60;
            EyeAxis(ref n.Vx,direction,4,.1f,hell?.07f:.1f,hell?.03f:.05f);
            EyeAxis(ref n.Vy,vertical,1.5f,.04f,hell?.03f:.05f,hell?.02f:.03f);
            if(++n.A1>200)
            {
                if(!env.PlayerWet && env.ClearLine || n.A1>1000)n.A1=0;
                bool slow=n.Identity.Type==62 || n.Identity.Type==66;
                float limitX=slow?3:4,limitY=slow?1.25f:1.5f,ax=slow?.12f:.2f,ay=slow?.07f:.1f;
                n.A2++;if(n.A2>0){if(n.Vy<limitY)n.Vy+=ay;}else if(n.Vy>-limitY)n.Vy-=ay;
                if(n.A2<-150 || n.A2>150){if(n.Vx<limitX)n.Vx+=ax;}else if(n.Vx>-limitX)n.Vx-=ax;
                if(n.A2>300)n.A2=-300;
            }
        }
        private static void Slime(ref NpcMotionState n,PredictionEnvironment env,int direction,int vertical)
        {
            bool aggressive=!env.Day || n.Life!=n.LifeMax || n.Y>env.WorldSurface*16 || env.SlimeRain;
            if(n.A2>1)n.A2--;
            if(n.Wet)
            {
                if(n.CollideY)n.Vy=-2;
                if(n.Vy<0 && n.A3==n.X){n.Direction*=-1;n.A2=200;}if(n.Vy>0)n.A3=n.X;
                bool lava=n.Identity.Type==59 && !env.Remix;
                if(n.Vy>2)n.Vy*=.9f;else if(lava && vertical<0)n.Vy-=.8f;
                n.Vy=Math.Max(lava?-10:-4,n.Vy-.5f);
                if(n.A2==1 && aggressive)n.Direction=direction;
            }
            if(n.A2==0){n.A0=-100;n.A2=1;n.Direction=direction;}
            if(n.Vy==0)
            {
                if(n.A3==n.X){n.Direction*=-1;n.A2=200;}n.A3=0;
                n.Vx*=.8f;if(Math.Abs(n.Vx)<.1f)n.Vx=0;
                // Ordinary slime rhythm; specials retain an approximation tag.
                n.A0+=aggressive?2:1;if(n.Identity.Type==59 || n.Identity.Type==138)n.A0+=2;if(n.Identity.Type==71)n.A0+=3;
                int jump=n.A0>=0?1:n.A0>=-1000 && n.A0<=-500?2:n.A0>=-2000 && n.A0<=-1500?3:0;
                if(jump!=0){if(aggressive && n.A2==1)n.Direction=direction;n.Vy=jump==3?-8:-6;n.Vx+=(jump==3?3:2)*n.Direction;n.A0=jump==3?-200:jump==1?-1120:-2120;if(jump==3)n.A3=n.X;}
            }
            else if(n.Direction==1 && n.Vx<3 || n.Direction==-1 && n.Vx>-3)
            {if(n.CollideX && Math.Abs(n.Vx)==.2f)n.X-=1.4f*n.Direction;if(n.Direction==-1 && n.Vx<.01f || n.Direction==1 && n.Vx>-.01f)n.Vx+=.2f*n.Direction;else n.Vx*=.93f;}
        }
        private static void Bubble(ref NpcMotionState n,PredictionEnvironment env)
        {
            float dx=env.PlayerX-n.Bounds.CenterX,dy=env.PlayerY-n.Bounds.CenterY;Normalize(ref dx,ref dy,20);
            n.Vx=(n.Vx*40+dx)/41;n.Vy=(n.Vy*40+dy)/41;
            // Zero perturbation is one representative path, never the real RNG
            // stream or a mathematically claimed expected trajectory.
            n.Vx=(n.Vx*50+env.Wind*2)/51;n.Vy=(n.Vy*50-.25f)/51;if(n.Vy>0)n.Vy-=.04f;
            if(n.A0==0 && Math.Abs(env.PlayerX-n.X)<40+n.Width && Math.Abs(env.PlayerY-n.Y)<40+n.Height){n.A0=1;n.A1=4;}
            if(n.A0==0 && ++n.A1>=150){n.A0=1;n.A1=4;}
            if(n.A0==1 && --n.A1<=0){n.Active=false;return;}
            if(n.JustHit || n.A0==1){n.CanReceive=false;float x=n.Bounds.CenterX,y=n.Bounds.CenterY;n.Width=n.Height=100;n.X=x-50;n.Y=y-50;}
            n.JustHit=false;
        }
        private static bool Shark(ref NpcMotionState n,PredictionEnvironment env,IPredictionTerrain terrain,out PredictionStop stop)
        {
            stop=PredictionStop.None;n.NoTileCollide=true;n.NoGravity=true;
            bool solid;if(!terrain.Solid(n.Bounds,out solid,out stop))return false;
            if(n.A0==0)
            {
                n.A1++;n.CanReceive=false;n.Vy=n.A3;
                if(n.Identity.Type==373){double angle=Math.PI/30;float before=(float)(Math.Cos(angle*n.L1)-.5)*n.A2;n.L1++;float after=(float)(Math.Cos(angle*n.L1)-.5)*n.A2;n.X+=(after-before)*-n.Direction;}
                if(n.A1>=90){n.A0=1;n.A1=solid?0:1;float dx=env.PlayerX-n.Bounds.CenterX,dy=env.PlayerY-n.Bounds.CenterY;Normalize(ref dx,ref dy,16);n.Vx=dx;n.Vy=dy;}
            }
            else if(n.A0==1)
            {if(!solid && n.A1<1)n.A1=1;if(n.A1>=1){n.CanReceive=true;n.A1++;if(solid){n.Active=false;stop=PredictionStop.Despawn;return false;}}if(n.A1>=60)n.NoGravity=false;}
            else{stop=PredictionStop.PhaseBoundary;return false;}
            return true;
        }
        private static bool Duke(ref NpcMotionState n,PredictionEnvironment env,out PredictionStop stop)
        {
            stop=PredictionStop.None;bool phase2=n.A0>4,phase3=n.A0>9,early=n.A3<(phase2?6:10);
            n.CanReceive=true;
            if(n.L0==0){stop=PredictionStop.PhaseBoundary;return false;}
            int wait=env.Expert?40:60,dash=env.Expert?28:30;
            float speed=env.Expert?8.5f:7.5f,acc=env.Expert?.55f:.45f,burst=env.Expert?17:16;
            if(phase3){wait=30;speed=12;acc=.7f;dash=25;burst=27;}
            else if(phase2 && early){wait=env.Expert?40:20;speed=env.Expert?10:8;acc=env.Expert?.6f:.5f;dash=env.Expert?27:30;if(env.Expert)burst=21;}
            else if(early)wait=30;
            if(env.Enraged){wait=10;burst+=6;}
            if(n.A0==0 || n.A0==5 || n.A0==10)
            {
                if(n.A1==0)n.A1=(phase3?360:300)*Math.Sign(n.Bounds.CenterX-env.PlayerX);
                float hx=env.PlayerX+n.A1-n.Bounds.CenterX-n.Vx,hy=env.PlayerY-200-n.Bounds.CenterY-n.Vy;Normalize(ref hx,ref hy,speed);
                FlyAxis(ref n.Vx,hx,acc);FlyAxis(ref n.Vy,hy,acc);
                if(++n.A2>=wait)
                {
                    if(!phase3 && (!early || !phase2 && n.Life<=n.LifeMax*.5f || phase2 && env.Expert && n.Life<=n.LifeMax*.15f)){stop=PredictionStop.PhaseBoundary;return false;}
                    bool teleport=phase3 && (n.A3==1 || n.A3==4 || n.A3==8);
                    n.A0=teleport?12:n.A0+1;n.A1=n.A2=0;
                    if(!teleport){float dx=env.PlayerX-n.Bounds.CenterX,dy=env.PlayerY-n.Bounds.CenterY;Normalize(ref dx,ref dy,burst);n.Vx=dx;n.Vy=dy;}
                }
            }
            else if(n.A0==1 || n.A0==6 || n.A0==11)
            {if(++n.A2>=dash){n.A0--;n.A1=n.A2=0;n.A3+=phase3?1:2;}}
            else if(n.A0==12)
            {
                n.CanReceive=false;n.Vx*=.98f;n.Vy*=.98f;n.Vy+=(0-n.Vy)*.02f;
                if(n.A2==15)
                {
                    if(env.Multiplayer){stop=PredictionStop.PhaseBoundary;return false;}
                    if(n.A1==0)n.A1=300*Math.Sign(n.Bounds.CenterX-env.PlayerX);
                    n.X=env.PlayerX-n.A1-n.Width*.5f;n.Y=env.PlayerY-200-n.Height*.5f;n.NewSegment=true;
                }
                if(++n.A2>=30){n.A0=10;n.A1=n.A2=0;if(++n.A3>=9)n.A3=0;}
            }
            else{stop=PredictionStop.PhaseBoundary;return false;}
            return true;
        }
        private static void Bounce(ref NpcMotionState n,float scale=.5f,float yMin=1,float ySpeed=1)
        {if(n.CollideX){n.Vx=-n.OldVx*scale;if(n.Direction==-1 && n.Vx>0 && n.Vx<2)n.Vx=2;if(n.Direction==1 && n.Vx<0 && n.Vx>-2)n.Vx=-2;}if(n.CollideY){n.Vy=-n.OldVy*scale;if(n.Vy>0 && n.Vy<yMin)n.Vy=ySpeed;if(n.Vy<0 && n.Vy>-yMin)n.Vy=-ySpeed;}}
        private static void FlyAxis(ref float v,float target,float a){if(v<target){v+=a;if(v<0 && target>0)v+=a;}else if(v>target){v-=a;if(v>0 && target<0)v-=a;}}
        private static void EyeAxis(ref float v,int direction,float limit,float acceleration,float over,float reverse)
        {float toward=v*direction;if(toward>=limit)return;toward+=acceleration;if(toward<-limit)toward+=over;else if(toward<0)toward-=reverse;v=Math.Min(limit,toward)*direction;}
        private static void Seek(ref NpcMotionState n,float x,float y,float speed,float acceleration)
        {x-=n.Bounds.CenterX;y-=n.Bounds.CenterY;Normalize(ref x,ref y,speed);n.Vx=Approach(n.Vx,x,acceleration);n.Vy=Approach(n.Vy,y,acceleration);}
        private static float Approach(float v,float target,float amount){return v<target?Math.Min(target,v+amount):Math.Max(target,v-amount);}
        private static float Clamp(float v,float lo,float hi){return Math.Max(lo,Math.Min(hi,v));}
        private static bool Finite(float v){return !float.IsNaN(v) && !float.IsInfinity(v);}
        private static void Normalize(ref float x,ref float y,float length){float d=(float)Math.Sqrt(x*x+y*y);if(d<.0001f){x=y=0;return;}x*=length/d;y*=length/d;}
    }
}
