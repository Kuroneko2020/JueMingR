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
            if(n.Identity.Type==371 || n.Identity.Type==166 || n.Identity.Type==685 || n.Style==16 && NpcAquaticMotion.Known(n.Identity.Type))result|=PredictionAssumption.RandomRepresentative;
            // Terrain contacts and unlisted variants are qualified separately
            // from the audited air-motion families. A shared aiStyle alone is
            // not evidence that every variant has the same movement formula.
            if(!n.NoTileCollide || n.Style==3 || n.Style==6 || n.Style==37 || n.Style==1 || !KnownMotion(n))result|=PredictionAssumption.ApproximateMechanism;
            if(n.BuffFingerprint!=0)result|=PredictionAssumption.ApproximateMechanism|PredictionAssumption.UnmodeledStatusEffects;
            if(n.TrackingKind>=2)result|=PredictionAssumption.ObservedTrackingMotion|PredictionAssumption.ApproximateMechanism;
            return result;
        }
        public static bool Step(ref NpcMotionState n,NpcMotionState[] group,int count,PredictionEnvironment env,IPredictionTerrain terrain,int elapsed,out PredictionStop stop)
        {return Step(ref n,group,count,env,terrain,elapsed,false,out stop);}
        public static bool Step(ref NpcMotionState n,NpcMotionState[] group,int count,PredictionEnvironment env,IPredictionTerrain terrain,int elapsed,bool rolling,out PredictionStop stop)
        {
            stop=PredictionStop.None;n.NewSegment=false;
            env=NpcTargeting.Player(n,env);
            if(!n.Active){stop=PredictionStop.Despawn;return false;}
            // Explicit structural models can continue from their observed
            // state under a qualified unknown-effect premise until expiry.
            // Unmodeled families still refuse a potentially motion-changing
            // effect; no unknown effect is silently declared harmless.
            if(n.BuffFingerprint!=0 && (!AllowsUnmodeledMotionEffect(n) || n.BuffExpires<=elapsed))
            {stop=PredictionStop.BuffTransition;return false;}
            if(!Finite(n.X) || !Finite(n.Y) || !Finite(n.Vx) || !Finite(n.Vy)){stop=PredictionStop.InvalidState;return false;}
            // Native UpdateNPC smooths the last received offset before AI.
            // Future packets remain unknown under NetworkObservation; no
            // live entity or network state is advanced by this scalar copy.
            if(n.ResetNetOffset || n.SmoothingRange<=0)n.NetOffsetX=n.NetOffsetY=0;
            else if(n.NetOffsetX!=0 || n.NetOffsetY!=0)
            {
                float length=(float)Math.Sqrt(n.NetOffsetX*n.NetOffsetX+n.NetOffsetY*n.NetOffsetY);
                if(length>n.SmoothingRange){float inverse=1/length;n.NetOffsetX*=inverse;n.NetOffsetY*=inverse;n.NetOffsetX*=n.SmoothingRange;n.NetOffsetY*=n.SmoothingRange;length=(float)Math.Sqrt(n.NetOffsetX*n.NetOffsetX+n.NetOffsetY*n.NetOffsetY);}
                float step=2+length/n.SmoothingRange*2,normal=1/length;n.NetOffsetX-=n.NetOffsetX*normal*step;n.NetOffsetY-=n.NetOffsetY*normal*step;
                if((float)Math.Sqrt(n.NetOffsetX*n.NetOffsetX+n.NetOffsetY*n.NetOffsetY)<step)n.NetOffsetX=n.NetOffsetY=0;
            }
            if(!NpcHealth.Step(ref n,group,count,env,out stop))return false;
            // Native fixes gravity from the pre-AI position and old wet state.
            // A teleport or liquid entry later in this update cannot alter it.
            float gravity=.3f,fall=10,worldScale=env.WorldWidth/4200f;worldScale*=worldScale;
            gravity*=Clamp((n.Y/16-(60+10*worldScale))/Math.Max(1,env.WorldSurface/6),.25f,1);
            if(n.Wet){gravity=n.Shimmer?.15f:n.Honey?.1f:.2f;fall=n.Shimmer?5.5f:n.Honey?4:7;}
            n.Gravity=gravity;n.MaxFall=fall;
            if(n.Identity.Type==488){n.Vx=n.Vy=0;return true;}
            int facingOldTarget=n.Target;if(n.Style==2 || n.Style==5 || n.Style==14)NpcTargeting.Retarget(ref n,ref env);
            bool face=NpcTargeting.CanFace(n,env,facingOldTarget);var tracking=NpcTargeting.Area(n,env,1);
            if(n.TrackingKind>=2)n.TrackingArea=tracking;
            int direction=face?(int)tracking.X+(int)tracking.Width/2<n.X+n.Width/2?-1:1:n.Direction;
            int vertical=face?(int)tracking.Y+(int)tracking.Height/2<n.Y+n.Height/2?-1:1:n.DirectionY;
            bool confused=n.ConfusedTicks>0;if(confused){direction=-direction;n.ConfusedTicks--;}
            bool linked=false;
            if(n.Style==16 && NpcAquaticMotion.Known(n.Identity.Type))
            {if(!NpcAquaticMotion.Step(ref n,env,terrain,direction,vertical,out stop))return false;}
            else if(n.Style==13 && NpcAnchoredMotion.Known(n.Identity.Type))
            {if(!NpcAnchoredMotion.Step(ref n,env,terrain,out stop))return false;}
            else if(n.Identity.Type==371)Bubble(ref n,env);
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
                int wormType=n.Identity.Type;
                int oldTarget=n.Target;
                // Life regen has already used the previous shared-life owner.
                // Native worm AI then updates that owner for the next tick.
                if(n.Style==6 && wormType>=13 && wormType<=15)n.Health.RealLife=-1;
                else if(n.A3>0)n.Health.RealLife=(int)n.A3;
                if(n.Target<0 || n.Target>=255 || env.PlayerDead || n.Style==6 && (wormType==10 || wormType==39 || wormType==95) && env.PlayerY-env.PlayerHeight/2<env.WorldSurface*16)
                    NpcWormMotion.Target(ref n,ref env,oldTarget,confused);
                if(n.Style==37 && !NpcWormMotion.Head(ref n,env,terrain,oldTarget,confused,out stop))return false;
                if(!env.Multiplayer && (wormType==7 || wormType==8 || wormType==10 || wormType==11 || wormType==13 || wormType==14 || wormType==39 || wormType==40 || wormType==95 || wormType==96 || wormType==98 || wormType==99 || n.Style==37 && wormType!=136))
                {
                    bool child=false;for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.ChildIdentity) && group[i].Active && group[i].Style==n.Style){child=true;break;}
                    if(!child){stop=PredictionStop.MissingDependency;return false;}
                }
                if(n.ParentSlot>=0)
                {
                    int parent=-1;for(int i=0;i<count;i++)if(group[i].Identity.Slot==n.ParentSlot){parent=i;break;}
                    if(parent<0 || !group[parent].Active || group[parent].Style!=n.Style){stop=PredictionStop.MissingDependency;return false;}
                    // AI37 uses integer half dimensions for the parent; AI6
                    // uses its floating Center, including odd dimensions.
                    float dx=group[parent].X+(n.Style==37?group[parent].Width/2:group[parent].Width*.5f)-n.Bounds.CenterX,dy=group[parent].Y+(n.Style==37?group[parent].Height/2:group[parent].Height*.5f)-n.Bounds.CenterY;
                    float distance=(float)Math.Sqrt(dx*dx+dy*dy),spacing=n.Width;
                    int type=n.Identity.Type;
                    if(n.Style==37)spacing=(int)(44*n.Scale);
                    else if(type>=87 && type<=92)spacing=42;
                    else if(type>=454 && type<=459)spacing=36;
                    else if(type>=513 && type<=515)spacing-=6;
                    else if(type>=412 && type<=414)spacing+=6;
                    else if(type>=621 && type<=623)spacing=24;
                    if(env.GoodWorld && type>=13 && type<=15)spacing=62;
                    if(distance<=.001f){stop=PredictionStop.InvalidState;return false;}
                    float amount=(distance-spacing)/distance;dx*=amount;dy*=amount;n.X+=dx;n.Y+=dy;
                    n.Vx=n.Vy=0;linked=true;
                }
                else if(n.Style==6 && NpcWormMotion.KnownHead(wormType))
                {if(!NpcWormMotion.Head(ref n,env,terrain,oldTarget,confused,out stop))return false;}
                else if(n.Style==37){}
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
            {if(n.A0==-999){stop=PredictionStop.PhaseBoundary;return false;}if(!Slime(ref n,env,terrain,direction,vertical,out stop))return false;}
            else if(n.Style==40 && NpcWallMotion.Wall(n.EffectiveType))
            {if(!NpcWallMotion.Step(ref n,env,terrain,confused,out stop))return false;}
            else if(n.Style==3 && NpcGroundMotion.Known(n.EffectiveType))
            {if(!NpcGroundMotion.Step(ref n,env,terrain,confused,out stop))return false;}
            else if(n.Style==3)
            {if(!NpcGroundMotion.Fallback(ref n,env,confused,out stop))return false;}
            else if(n.Style==2 && (n.Identity.Type==2 || n.Identity.Type==133 || n.Identity.Type>=190 && n.Identity.Type<=194))
            {
                n.NoGravity=true;
                if(!n.NoTileCollide)Bounce(ref n);
                int eyeDirection=direction,eyeVertical=vertical;
                if(env.Day && !env.Remix && !env.Graveyard && n.Y<=env.WorldSurface*16)
                {n.TimeLeft=Math.Min(n.TimeLeft,10);eyeDirection=n.Vx>0?1:-1;eyeVertical=-1;}
                n.Direction=eyeDirection;n.DirectionY=eyeVertical;
                bool wandering=n.Identity.Type==133,damaged=wandering && n.Life<n.LifeMax*.5f;
                float sx=wandering?(damaged?6:4):4*(2-n.Scale),sy=wandering?(damaged?4:1.5f):1.5f*(2-n.Scale);
                EyeAxis(ref n.Vx,eyeDirection,sx,.1f,.1f,.05f);EyeAxis(ref n.Vy,eyeVertical,sy,damaged?.1f:.04f,damaged?.1f:.05f,damaged?.05f:.03f);
                if(n.Wet){if(n.Vy>0)n.Vy*=.95f;n.Vy=Math.Max(-4,n.Vy-.5f);n.Direction=direction;n.DirectionY=vertical;}
            }
            else if(rolling && n.Style==41 && n.Identity.Type==177)NpcRollingMotion.Derpling(ref n,env,direction);
            else if(rolling && n.Style==39 && n.Identity.Type==153)
            {if(!NpcRollingMotion.Tortoise(ref n,env,terrain,direction,vertical,out stop))return false;}
            else if(n.Style==5 && (FlyingType(n.Identity.Type) || rolling && n.Identity.Type==176))Flying(ref n,env,direction,vertical);
            else if(n.Style==14 && BatType(n.Identity.Type))
            {
                if(n.A1+1>200 && n.A1+1<=1000 && !env.PlayerWet)
                {bool clear;if(!terrain.CanHit(n.Bounds,new MotionRect(env.PlayerX-env.PlayerWidth/2,env.PlayerY-env.PlayerHeight/2,env.PlayerWidth,env.PlayerHeight),out clear,out stop))return false;env.ClearLine=clear;}
                Bat(ref n,env,direction,vertical);
            }
            else if(n.Style==17 && n.Identity.Type==61)Vulture(ref n,env,direction,vertical);
            // A structural AI needs its root/liquid/wall constraints. A free
            // trend would invent a long path while silently ignoring them.
            else if(n.Style==13 || n.Style==16 || n.Style==40){stop=PredictionStop.UnsupportedMechanism;return false;}
            else if(rolling)NpcRollingMotion.Trend(ref n,elapsed);
            else if(elapsed>12){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.Active){stop=PredictionStop.Despawn;return false;}
            // Linked AI already attached the segment and zeroed velocity. It
            // still enters native free movement's water-extinguish phase.
            if(!linked && !n.NoGravity)n.Vy=Math.Min(fall,n.Vy+gravity);
            if(Math.Abs(n.Vx)<.005f)n.Vx=0;
            // oldVelocity belongs to native UpdateCollision. NoTileCollide
            // bypasses that owner; changing it here would defeat rolling reuse
            // for a correctly predicted Sharkron/Duke step.
            env=NpcTargeting.Player(n,env);
            if(!terrain.Move(ref n,env,out stop))return false;
            if(n.Style==3 && NpcGroundMotion.Known(n.EffectiveType))NpcGroundMotion.AfterMove(ref n);
            if(n.Style==69 || n.Identity.Type==371 || n.Identity.Type==372 || n.Identity.Type==373)n.Health.DontTakeDamage=!n.CanReceive;
            n.JustHit=false;return CheckActive(ref n,env,out stop);
        }
        internal static bool CurrentPlayerPremise(NpcMotionState n)
        {return n.Style==13 && NpcAnchoredMotion.Known(n.EffectiveType) || n.Style==16 && NpcAquaticMotion.Known(n.EffectiveType) || n.Style==3 && (n.EffectiveType==77 || NpcWallMotion.Ground(n.EffectiveType)) || n.Style==40 && NpcWallMotion.Wall(n.EffectiveType);}
        internal static bool AllowsUnmodeledMotionEffect(NpcMotionState n)
        {return CurrentPlayerPremise(n);}
        internal static bool StructuredModel(NpcMotionState n)
        {return KnownMotion(n) || CurrentPlayerPremise(n) || n.Style==69 || n.Style==39 && n.EffectiveType==153 || n.Style==41 && n.EffectiveType==177;}
        internal static bool NeedsPlayerMotion(NpcMotionState n)
        {
            if(n.EffectiveType==488)return false;
            if(n.TrackingKind>=2 && (n.Style==2 || n.Style==5 || n.Style==14))return false;
            if(StructuredModel(n) || n.Style==3 || n.Style==40 || n.Style==13 || n.Style==16)return true;
            // These native collision predicates directly read the numbered
            // player even when the AI itself is only an observed trend.
            int type=n.EffectiveType;return type==50 || type==657 || type==245 || type==620 || n.Style==26 || n.Style==87;
        }
        private static bool CheckActive(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.Identity.Type;
            if(n.InactivityImmune || type==668 || type==690 && n.A0==0)return true;
            bool keep=false;int count=e.Players==null?1:e.Players.Count;
            var far=new MotionRect((int)(n.X+n.Width/2-4032),(int)(n.Y+n.Height/2-2520),8064,5040);
            var near=new MotionRect((int)(n.X+n.Width/2-960-n.Width),(int)(n.Y+n.Height/2-600-n.Height),1920+2*n.Width,1200+2*n.Height);
            for(int i=0;i<count;i++)
            {
                var player=e.Players==null?new MotionRect((int)(e.PlayerX-e.PlayerWidth/2),(int)(e.PlayerY-e.PlayerHeight/2),e.PlayerWidth,e.PlayerHeight):e.Players[i];
                if(Intersects(far,player) || n.Boss || type==7 || type==10 || type==13 || type==39 || type==87)keep=true;
                if(Intersects(near,player))n.TimeLeft=750;
            }
            if(--n.TimeLeft<=0)keep=false;
            if(!keep && !e.Multiplayer){n.Active=false;stop=PredictionStop.Despawn;return false;}return true;
        }
        private static bool Intersects(MotionRect a,MotionRect b){return a.X<b.X+b.Width && a.X+a.Width>b.X && a.Y<b.Y+b.Height && a.Y+a.Height>b.Y;}
        private static bool FlyingType(int t){return t==6 || t==173 || t==42 || t>=231 && t<=235;}
        private static bool BatType(int t){return t==49 || t==51 || t==60 || t==62 || t==66 || t==93 || t==137 || t==150 || t==151 || t==152 || t==634;}
        private static bool KnownMotion(NpcMotionState n)
        {int t=n.Identity.Type;return t==488 || t>=370 && t<=373 || n.Style==1 || n.Style==3 || n.Style==6 || n.Style==8 || n.Style==37 || n.Style==17 && t==61 || n.Style==2 && (t==2 || t==133 || t>=190 && t<=194) || n.Style==5 && FlyingType(t) || n.Style==14 && BatType(t);}
        private static void Vulture(ref NpcMotionState n,PredictionEnvironment env,int direction,int vertical)
        {
            n.NoGravity=true;
            if(n.A0==0)
            {
                // The launch update still has gravity. Pursuit starts only on
                // the next update, after native has published the flying phase.
                n.NoGravity=false;n.Direction=direction;n.DirectionY=vertical;
                if(!env.Multiplayer)
                {
                    if(n.Vx!=0 || n.Vy<0 || (double)n.Vy>.3)n.A0=1;
                    else if((int)n.X-100<env.PlayerX+env.PlayerWidth/2 && (int)n.X+n.Width+100>env.PlayerX-env.PlayerWidth/2 &&
                        (int)n.Y-100<env.PlayerY+env.PlayerHeight/2 && (int)n.Y+n.Height+100>env.PlayerY-env.PlayerHeight/2 || n.Life<n.LifeMax)
                    {n.A0=1;n.Vy-=6;}
                }
            }
            else
            {
                Bounce(ref n,.5f,2,1);n.Direction=direction;n.DirectionY=vertical;
                float toward=n.Vx*direction;
                if(toward<3){toward+=.1f;if(toward<-3)toward+=.1f;else if(toward<0)toward+=.05f;n.Vx=Math.Min(3,toward)*direction;}
                float height=env.PlayerY-env.PlayerHeight/2-n.Height/2;
                if(Math.Abs(n.Bounds.CenterX-env.PlayerX)>50)height-=100;
                if(n.Y<height){n.Vy+=.05f;if(n.Vy<0)n.Vy+=.01f;}
                else{n.Vy-=.05f;if(n.Vy>0)n.Vy-=.01f;}
                n.Vy=Clamp(n.Vy,-3,3);
            }
            if(n.Wet){if(n.Vy>0)n.Vy*=.95f;n.Vy=Math.Max(-4,n.Vy-.5f);n.Direction=direction;n.DirectionY=vertical;}
        }
        private static void Flying(ref NpcMotionState n,PredictionEnvironment env,int direction,int vertical)
        {
            n.NoGravity=true;
            bool eater=n.Identity.Type==6 || n.Identity.Type==173;
            bool moss=n.Identity.Type==176;
            float speed=moss?4:eater?(env.Remix?5:4):(n.Identity.Type==231?3:3.5f)*(2-n.Scale);
            float acc=moss?.017f:eater?(env.Remix?.06f:n.Identity.Type==6 && env.Expert?.035f:.02f):(n.Identity.Type==231?.017f:.021f)*(2-n.Scale);
            float targetTop=env.PlayerY-env.PlayerHeight*.5f;
            if(!eater && !moss && n.Y/16<env.WorldSurface && (targetTop-n.Y>300 && n.Vy<0 || targetTop-n.Y<80 && n.Vy>0))n.Vy*=.97f;
            float dx=(int)(env.PlayerX/8)*8-(int)(n.Bounds.CenterX/8)*8,dy=(int)(env.PlayerY/8)*8-(int)(n.Bounds.CenterY/8)*8;
            float distance=(float)Math.Sqrt(dx*dx+dy*dy);
            if(distance==0){dx=n.Vx;dy=n.Vy;}else{dx*=speed/distance;dy*=speed/distance;}
            if(!eater || distance>100){n.A0++;n.Vy+=n.A0>0?.023f:-.023f;n.Vx+=n.A0<-100 || n.A0>100?.023f:-.023f;if(n.A0>200)n.A0=-200;}
            if(eater && distance<150){n.Vx+=dx*.007f;n.Vy+=dy*.007f;}
            if(n.Vx<dx){n.Vx+=acc;if(moss && n.Vx<0 && dx>0)n.Vx+=acc;}else if(n.Vx>dx){n.Vx-=acc;if(moss && n.Vx>0 && dx<0)n.Vx-=acc;}
            if(n.Vy<dy){n.Vy+=acc;if(moss && n.Vy<0 && dy>0)n.Vy+=acc;}else if(n.Vy>dy){n.Vy-=acc;if(moss && n.Vy>0 && dy<0)n.Vy-=acc;}
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
        private static bool Slime(ref NpcMotionState n,PredictionEnvironment env,IPredictionTerrain terrain,int direction,int vertical,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;
            bool aggressive=!env.Day || n.Life!=n.LifeMax || n.Y>env.WorldSurface*16 || env.SlimeRain;
            if(env.Remix && type==59 && n.Life==n.LifeMax)aggressive=false;
            if(type==81 || type==183 || type==304 || type==667 || type==244 || type==184 || type==535 || type==204 || type==658 || type==659)aggressive=true;
            if((type==377 || type==446) && !env.PlayerDead && !n.Wet && (n.Bounds.CenterX-env.PlayerX)*(n.Bounds.CenterX-env.PlayerX)+(n.Bounds.CenterY-env.PlayerY)*(n.Bounds.CenterY-env.PlayerY)<=40000)aggressive=true;
            // Crystal's pre-ground increment is independent of active pursuit.
            if(type==244)n.A0+=2;
            if(type==184 || type==535 || type==204 || type==658 || type==659)
            {
                if(n.L0>0)n.L0--;
                // Their attack opportunity also resets their own jump clock
                // and damps velocity, irrespective of projectile cooldown.
                // Retain that deterministic motion; do not create an attack.
                float dx=env.PlayerX-n.Bounds.CenterX,dy=env.PlayerY-n.Bounds.CenterY;
                bool rectangular=type==658 || type==659;
                // The older shooters measure to the player's top, while
                // 658/659 use a center-to-center rectangular opportunity.
                if(!rectangular)dy-=env.PlayerHeight/2;
                float distanceSquared=dx*dx+dy*dy;
                bool inRange=rectangular?Math.Abs(dx)<500 && Math.Abs(dy)<550:distanceSquared<(type==204?160000:40000);
                if(!n.Wet && !env.PlayerDead && !n.TargetNoAggro && n.Vy==0 && inRange)
                {
                    var origin=type==204?new MotionRect(n.X,n.Y-20,n.Width,n.Height+20):n.Bounds;
                    bool clear;if(!terrain.CanHit(origin,new MotionRect(env.PlayerX-env.PlayerWidth/2,env.PlayerY-env.PlayerHeight/2,env.PlayerWidth,env.PlayerHeight),out clear,out stop))return false;
                    if(clear)
                    {
                        // 204's expert close attack and ordinary attack are
                        // two independent native ifs: both damp velocity, and
                        // the latter owns the final -80 jump clock.
                        if(type==204 && env.Expert && distanceSquared<40000)n.Vx*=.9f;
                        n.A0=type==204?-80:-40;n.Vx*=.9f;
                    }
                }
            }
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
                n.A0+=aggressive?2:1;if(type==59 && !env.Remix || type==138)n.A0+=2;if(type==71 || type==667 || type==659 || type==377 || type==446)n.A0+=3;
                if(type==183)n.A0++;if(type==658)n.A0+=5;if(type==304)n.A0+=(1-n.Life/Math.Max(1,n.LifeMax))*10;if(type==81)n.A0+=n.Scale>=0?4:1;
                float rhythm=type==659?-500:type==667?-400:-1000;
                int jump=n.A0>=0?1:n.A0>=rhythm && n.A0<=rhythm*.5f?2:n.A0>=rhythm*2 && n.A0<=rhythm*1.5f?3:0;
                if(jump!=0)
                {
                    if(aggressive && n.A2==1)n.Direction=direction;n.Vy=jump==3?-8:-6;n.Vx+=(jump==3?3:2)*n.Direction;
                    if(type==59 && !env.Remix){if(jump==3)n.Vy-=2;n.Vx+=(jump==3?.5f:2)*n.Direction;}
                    n.A0=jump==3?-200:-120+rhythm*(jump==1?1:2);if(jump==3)n.A3=n.X;
                    if(type==659){n.Vy*=1.6f;n.Vx*=1.2f;}if(type==141){n.Vy*=1.3f;n.Vx*=1.2f;}
                    // 685's first impulse is deterministic; retaining its
                    // current post-jump direction is one qualified RNG branch.
                    if(type==685){n.Vy*=.5f;n.Vx*=.2f;}
                    if(type==377 || type==446)
                    {
                        n.Vy*=.9f;n.Vx*=.6f;if(aggressive){n.Direction*=-1;n.Vx*=-1;}
                        PredictionTile ceiling;if(!terrain.Tile((int)(n.Bounds.CenterX/16),(int)(n.Bounds.CenterY/16)-1,out ceiling,out stop))return false;
                        if(ceiling.Active && ceiling.Solid && !ceiling.SolidTop && !ceiling.Half && ceiling.Slope==0 && -n.Vy+n.Height>16)n.Vy=-(16-n.Height);
                    }
                }
            }
            else if(n.Direction==1 && n.Vx<3 || n.Direction==-1 && n.Vx>-3)
            {if(n.CollideX && Math.Abs(n.Vx)==.2f)n.X-=1.4f*n.Direction;if(n.Direction==-1 && n.Vx<.01f || n.Direction==1 && n.Vx>-.01f)n.Vx+=.2f*n.Direction;else n.Vx*=.93f;}
            return true;
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
            float distanceX=env.PlayerX-n.Bounds.CenterX,distanceY=env.PlayerY-n.Bounds.CenterY;
            if(env.PlayerDead || distanceX*distanceX+distanceY*distanceY>5600*5600)
            {n.Vy-=.4f;n.TimeLeft=Math.Min(n.TimeLeft,10);n.A0=n.A0>4?5:0;n.A2=0;}
            if(n.L0==0){n.L0=1;if(!env.Multiplayer)n.A0=-1;}
            int wait=env.Expert?40:60,dash=env.Expert?28:30;
            float speed=env.Expert?8.5f:7.5f,acc=env.Expert?.55f:.45f,burst=env.Expert?17:16;
            if(phase3){wait=30;speed=12;acc=.7f;dash=25;burst=27;}
            else if(phase2 && early){wait=env.Expert?40:20;speed=env.Expert?10:8;acc=env.Expert?.6f:.5f;dash=env.Expert?27:30;if(env.Expert)burst=21;}
            else if(early)wait=30;
            if(env.Enraged){wait=10;burst+=6;}
            if((n.A0==0 || n.A0==5 || n.A0==10) && !env.PlayerDead)
            {
                DukeHover(ref n,env,phase3?360:300,speed,acc);
                if(++n.A2>=wait)
                {
                    int stage=(int)n.A3,next=0;
                    if(phase3)next=stage==1 || stage==4 || stage==8?12:11;
                    else if(phase2)
                    {
                        if(stage<6)next=6;else if(stage==6){next=7;n.A3=1;}else if(stage==7){next=8;n.A3=0;}
                        if(env.Enraged && next==7)next=8;
                        if(env.Expert && n.Life<=n.LifeMax*.15f)next=9;
                    }
                    else
                    {
                        if(stage<10)next=1;else if(stage==10){next=2;n.A3=1;}else if(stage==11){next=3;n.A3=0;}
                        if(env.Enraged && next==2)next=3;
                        if(n.Life<=n.LifeMax*.5f)next=4;
                    }
                    n.A0=next;n.A1=n.A2=0;
                    if(next==1 || next==6 || next==11 || next==7)
                    {float dx=env.PlayerX-n.Bounds.CenterX,dy=env.PlayerY-n.Bounds.CenterY;Normalize(ref dx,ref dy,next==7?20:burst);n.Vx=dx;n.Vy=dy;DukeDirection(ref n,env);}
                    if(next==3 && env.Enraged)n.A2=50;
                }
            }
            else if(n.A0==1 || n.A0==6 || n.A0==11)
            {if(++n.A2>=dash){n.A0--;n.A1=n.A2=0;n.A3+=phase3?1:2;}}
            else if(n.A0==2)
            {DukeHover(ref n,env,300,5,.3f);if(++n.A2>=80){n.A0=0;n.A1=n.A2=0;}}
            else if(n.A0==3 || n.A0==8)
            {DukeDamp(ref n);if(++n.A2>=90){n.A0=n.A0==3?0:5;n.A1=n.A2=0;}}
            else if(n.A0==4 || n.A0==9)
            {
                // Damage immunity belongs to the branch executed this update,
                // including the last frame that publishes the next phase.
                n.CanReceive=false;DukeDamp(ref n);
                if(++n.A2>=180){n.A0=n.A0==4?5:10;n.A1=n.A2=n.A3=0;}
            }
            else if(n.A0==7 || n.A0==13)
            {
                float angle=-(float)Math.PI*2/(120/2)*n.Direction,c=(float)Math.Cos(angle),s=(float)Math.Sin(angle),x=n.Vx,y=n.Vy;
                n.Vx=x*c-y*s;n.Vy=x*s+y*c;
                if(++n.A2>=120){bool last=n.A0==13;n.A0=last?10:5;n.A1=n.A2=0;if(last)n.A3++;}
            }
            else if(n.A0==12)
            {
                n.CanReceive=false;DukeDamp(ref n);
                if(n.A2==15)
                {
                    if(env.Multiplayer){stop=PredictionStop.PhaseBoundary;return false;}
                    if(n.A1==0)n.A1=300*Math.Sign(n.Bounds.CenterX-env.PlayerX);
                    n.X=env.PlayerX-n.A1-n.Width*.5f;n.Y=env.PlayerY-200-n.Height*.5f;n.NewSegment=true;DukeDirection(ref n,env);
                }
                if(++n.A2>=30){n.A0=10;n.A1=n.A2=0;if(++n.A3>=9)n.A3=0;}
            }
            else if(n.A0==-1)
            {n.CanReceive=false;n.Vx*=.98f;n.Vy*=.98f;if(n.A2>20)n.Vy=-2;DukeDirection(ref n,env);if(++n.A2>=75){n.A0=0;n.A1=n.A2=0;}}
            else if(env.PlayerDead && (n.A0==0 || n.A0==5 || n.A0==10)){}
            else{stop=PredictionStop.PhaseBoundary;return false;}
            return true;
        }
        private static void DukeHover(ref NpcMotionState n,PredictionEnvironment env,int offset,float speed,float acceleration)
        {if(n.A1==0)n.A1=offset*Math.Sign(n.Bounds.CenterX-env.PlayerX);float x=env.PlayerX+n.A1-n.Bounds.CenterX-n.Vx,y=env.PlayerY-200-n.Bounds.CenterY-n.Vy;Normalize(ref x,ref y,speed);FlyAxis(ref n.Vx,x,acceleration);FlyAxis(ref n.Vy,y,acceleration);DukeDirection(ref n,env);}
        private static void DukeDirection(ref NpcMotionState n,PredictionEnvironment env)
        {int direction=Math.Sign(env.PlayerX-n.Bounds.CenterX);if(direction!=0){n.Direction=direction;n.SpriteDirection=-direction;}}
        private static void DukeDamp(ref NpcMotionState n)
        {n.Vx*=.98f;n.Vy*=.98f;n.Vy+=(0-n.Vy)*.02f;}
        private static void Bounce(ref NpcMotionState n,float scale=.5f,float yMin=1,float ySpeed=1)
        {if(n.CollideX){n.Vx=-n.OldVx*scale;if(n.Direction==-1 && n.Vx>0 && n.Vx<2)n.Vx=2;if(n.Direction==1 && n.Vx<0 && n.Vx>-2)n.Vx=-2;}if(n.CollideY){n.Vy=-n.OldVy*scale;if(n.Vy>0 && n.Vy<yMin)n.Vy=ySpeed;if(n.Vy<0 && n.Vy>-yMin)n.Vy=-ySpeed;}}
        private static void FlyAxis(ref float v,float target,float a){if(v<target){v+=a;if(v<0 && target>0)v+=a;}else if(v>target){v-=a;if(v>0 && target<0)v-=a;}}
        private static void EyeAxis(ref float v,int direction,float limit,float acceleration,float over,float reverse)
        {float toward=v*direction;if(toward>=limit)return;toward+=acceleration;if(toward<-limit)toward+=over;else if(toward<0)toward-=reverse;v=Math.Min(limit,toward)*direction;}
        private static void Seek(ref NpcMotionState n,float x,float y,float speed,float acceleration)
        {x-=n.Bounds.CenterX;y-=n.Bounds.CenterY;Normalize(ref x,ref y,speed);n.Vx=Approach(n.Vx,x,acceleration);n.Vy=Approach(n.Vy,y,acceleration);}
        internal static float Approach(float v,float target,float amount){return v<target?Math.Min(target,v+amount):Math.Max(target,v-amount);}
        private static float Clamp(float v,float lo,float hi){return Math.Max(lo,Math.Min(hi,v));}
        private static bool Finite(float v){return !float.IsNaN(v) && !float.IsInfinity(v);}
        private static void Normalize(ref float x,ref float y,float length){float d=(float)Math.Sqrt(x*x+y*y);if(d<.0001f){x=y=0;return;}x*=length/d;y*=length/d;}
    }
}
