using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI38: all three Frost Legion enemies share the same hop motor.
    // Projectile births never feed this actor's movement or consume private RNG.
    internal static class NpcSnowmanMotion
    {
        internal static bool Known(NpcMotionState n)
        {return n.Style==38 && n.EffectiveType>=143 && n.EffectiveType<=145;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {return a.EffectiveType==b.EffectiveType && a.Style==b.Style;}
        internal static bool Retargets(NpcMotionState n)
        {return n.EffectiveType!=143 && n.A1>=3 || n.Vy==0 && n.A3==0 && n.L2!=n.X;}
        internal static bool NeedsPlayer(NpcMotionState n)
        {return Retargets(n);}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;
            float cap=type==143?3:type==145?3.5f:4,acceleration=type==143?.7f:type==145?.8f:1;
            if(type==143){n.A2++;if(confused || n.A2>=120)n.A2=0;}
            if(type==144 && n.A1>=3)
            {
                Query(ref n,ref e,confused);n.SpriteDirection=n.Direction;
                if(n.Vy==0)
                {
                    n.Vx*=.9f;n.A2++;
                    // Native Stabby uses double constants; Balla below uses
                    // float constants. Values exactly at either edge differ.
                    if((double)n.Vx>-.3 && (double)n.Vx<.3)n.Vx=0;
                    if(!e.Multiplayer && n.A2>=200)n.A2=n.A1=0;
                }
            }
            else if(type==145 && n.A1>=3)
            {
                Query(ref n,ref e,confused);n.SpriteDirection=n.Direction;
                bool canFire=(n.TargetCaptured?n.PlayerActive && !n.PlayerNativeDead:!e.PlayerDead) && FiringDistance(n,e);
                if(n.Vy==0 && n.A2==0 && !canFire)n.A1=0;
                else if(n.Vy==0)
                {
                    n.A2++;n.Vx*=.9f;if(n.Vx>-.3f && n.Vx<.3f)n.Vx=0;
                    if(!e.Multiplayer && n.A2>=16)n.A2=n.A1=0;
                }
                // This action remains in the receiving pause branch even
                // when its clock resets; hopping starts on the next action.
            }
            else
            {
                if(n.Vy==0)
                {
                    if(n.L2==n.X){n.Direction=-n.Direction;n.A3=60;}
                    n.L2=n.X;if(n.A3==0)Query(ref n,ref e,confused);
                    n.A0++;
                    if(n.A0>2){n.A0=0;n.A1++;n.Vy=-8.2f;n.Vx+=n.Direction*acceleration*1.1f;}
                    else{n.Vy=-6;n.Vx+=n.Direction*acceleration*.9f;}
                    n.SpriteDirection=n.Direction;if(confused)n.A0=n.A1=0;
                }
                n.Vx+=n.Direction*acceleration*.01f;
            }
            if(n.A3>0)n.A3--;
            if(n.Vx>cap && n.Direction>0)n.Vx=cap;
            if(n.Vx<-cap && n.Direction<0)n.Vx=-cap;
            return true;
        }
        private static bool FiringDistance(NpcMotionState n,PredictionEnvironment e)
        {
            // Main.MaxWorldViewSize is the locked readonly Point(1920,1200),
            // not camera size. NPC.AI_GlobalFiringDistanceCheck casts both
            // centers to Point, centers this rectangle, then Inflate(-50).
            int left=(int)e.PlayerX-960+50,top=(int)e.PlayerY-600+50;
            int x=(int)n.Bounds.CenterX,y=(int)n.Bounds.CenterY;
            return x>=left && x<left+1820 && y>=top && y<top+1100;
        }
        private static void Query(ref NpcMotionState n,ref PredictionEnvironment e,bool confused)
        {
            int oldTarget=n.Target;NpcTargeting.Retarget(ref n,ref e);
            if(NpcTargeting.CanFace(n,e,oldTarget))
            {
                // TargetClosest uses its integer targetRect, separately from
                // the precise player Center consumed by the firing predicate.
                var area=NpcTargeting.Area(n,e);n.Direction=(int)area.X+(int)area.Width/2<n.X+n.Width/2?-1:1;
                n.DirectionY=(int)area.Y+(int)area.Height/2<n.Y+n.Height/2?-1:1;
            }
            if(confused)n.Direction=-n.Direction;
        }
    }
}
