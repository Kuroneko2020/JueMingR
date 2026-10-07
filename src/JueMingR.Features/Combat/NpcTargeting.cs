using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcTargeting
    {
        internal static MotionRect Area(NpcMotionState n,PredictionEnvironment e,int elapsed=0)
        {
            if(n.TrackingKind>=2)return new MotionRect(n.TrackingArea.X+n.TrackingVx*elapsed,n.TrackingArea.Y+n.TrackingVy*elapsed,n.TrackingArea.Width,n.TrackingArea.Height);
            return new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight);
        }
        internal static bool CanFace(NpcMotionState n,PredictionEnvironment e,int oldTarget=-2)
        {if(oldTarget==-2)oldTarget=n.Target;return n.TrackingKind>=2 || !e.PlayerDead && !(n.TargetNoAggro && n.Direction!=0) && !(e.PlayerIdleWithNegativeAggro && oldTarget>=0 && oldTarget<255 && !n.Boss);}
        internal static void Retarget(ref NpcMotionState n,ref PredictionEnvironment e,bool requiresArea=true)
        {
            if(!n.TargetCaptured || !n.HasClosestPlayer || n.TrackingKind==3)return;
            n.Target=n.ClosestPlayerIndex;
            if(n.PlayerIndex!=n.ClosestPlayerIndex)
            {n.PlayerIndex=n.ClosestPlayerIndex;n.PlayerArea=n.ClosestPlayerArea;n.HasPlayer=true;n.PlayerDead=n.ClosestPlayerDead;n.PlayerWet=n.ClosestPlayerWet;n.PlayerIdle=n.ClosestPlayerIdle;n.PlayerGraveyard=n.ClosestPlayerGraveyard;}
            n.TargetNoAggro=n.ClosestPlayerNoAggro;e=Player(n,e);
            n.PlayerAttackHidden=n.ClosestPlayerAttackHidden;
            n.PlayerDesert=n.ClosestPlayerDesert;n.PlayerSandstorm=n.ClosestPlayerSandstorm;
            if(requiresArea && n.TargetChoiceUnknown)n.TargetChoiceUnavailable=true;
        }
        internal static void Face(ref NpcMotionState n,ref PredictionEnvironment e,bool faceTarget=true,bool confused=false)
        {
            int old=n.Target;Retarget(ref n,ref e);
            // Native guardian selection writes both directions even when
            // faceTarget is false. Numbered-player selection respects that flag.
            if(n.TrackingKind>=2 || faceTarget && CanFace(n,e,old))
            {
                var area=Area(n,e);
                if(n.Style==3 && n.TrackingKind!=2)
                {
                    // Native AI3 compares integer targetRect Bottom, including
                    // equality. This direction controls the next pit jump;
                    // guardian selection still uses its separate Center rule.
                    n.Direction=(int)area.X+(int)area.Width/2<n.X+n.Width/2?-1:1;
                    n.DirectionY=(int)area.Y+(int)area.Height<=n.Y+n.Height?-1:1;
                }
                else{n.Direction=area.CenterX<n.Bounds.CenterX?-1:1;n.DirectionY=area.CenterY<n.Bounds.CenterY?-1:1;}
            }
            // Native applies confusion after either selection branch, even
            // when faceTarget suppressed numbered-player facing.
            if(confused)n.Direction=-n.Direction;
        }
        internal static PredictionEnvironment Player(NpcMotionState n,PredictionEnvironment e)
        {
            if(!n.TargetCaptured)return e;
            e.Graveyard=n.PlayerGraveyard;if(n.PlayerIndex==e.PlayerIndex)return e;
            e.PlayerIndex=n.PlayerIndex;e.PlayerX=n.PlayerArea.CenterX;e.PlayerY=n.PlayerArea.CenterY;e.PlayerWidth=n.PlayerArea.Width;e.PlayerHeight=n.PlayerArea.Height;e.PlayerDead=!n.HasPlayer || n.PlayerDead;e.PlayerWet=n.PlayerWet;e.PlayerIdleWithNegativeAggro=n.PlayerIdle;return e;
        }
    }
}
