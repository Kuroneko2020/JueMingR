using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked Terraria 1.4.5.8 AI73 (NPC.cs35417–35558). TargetClosest(false)
    // still chooses the numbered target and applies native confusion. Shot435
    // RNG writes only the outside projectile; it never changes this motor,
    // receiving state, or firing clock. No native AI/RNG/birth runs here.
    internal static class NpcMartianTurretMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==73 && n.EffectiveType==387;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return true;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {
            if(!n.TargetCaptured)return true;
            return n.HasClosestPlayer?n.ClosestPlayerActive && !n.ClosestPlayerNativeDead:n.PlayerActive && !n.PlayerNativeDead;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.TargetCaptured){stop=PredictionStop.MissingDependency;return false;}
            NpcTargeting.Face(ref n,ref e,false,confused);n.SpriteDirection=n.Direction;
            n.Vx*=.93f;if(n.Vx>-.1f && n.Vx<.1f)n.Vx=0;
            bool living=n.PlayerActive && !n.PlayerNativeDead;
            // A ghost is native-active and not native-dead. Geometry consumed
            // by firing requires its own future timeline; never recast it as
            // a dead target to let a frozen surrogate pass.
            if(living && (!n.HasPlayer || n.PlayerGhost || e.PlayerTimelineActive && n.PlayerIndex!=e.PlayerIndex))
            {stop=PredictionStop.MissingDependency;return false;}
            bool firing=living && FiringDistance(n,e);
            if(n.A1<120)
            {
                n.A1++;
                if(n.A1>60){float progress=(n.A1-60f)/60f;n.Alpha=(int)((1f-progress)*255f);}else n.Alpha=255;
                n.Health.DontTakeDamage=true;n.CanReceive=false;
                return true; // Native returns before ai0, justHit, and shooting.
            }
            if(n.A1==120)n.A1++;
            n.Health.DontTakeDamage=false;n.CanReceive=!n.Health.Immortal && !n.Friendly;
            n.Alpha=0;
            if(n.A0==0 && !firing)n.A0=-120;
            if(n.A0<60)n.A0++;
            if(n.JustHit)n.A0=-30;
            if(n.A0==60 && !e.Multiplayer)n.A0=-120;
            return true;
        }
        private static bool FiringDistance(NpcMotionState n,PredictionEnvironment e)
        {
            // Main.MaxWorldViewSize is readonly Point(1920,1200), not viewport
            // settings. Native uses both Centers.ToPoint(), CenteredRectangle,
            // then Inflate(-50); the right/bottom edge is strictly exclusive.
            int left=(int)e.PlayerX-910,top=(int)e.PlayerY-550;
            int x=(int)n.Bounds.CenterX,y=(int)n.Bounds.CenterY;
            return x>=left && x<left+1820 && y>=top && y<top+1100;
        }
    }
}