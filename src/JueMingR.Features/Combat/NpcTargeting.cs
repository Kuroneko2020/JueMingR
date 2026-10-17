using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcTargeting
    {
        internal static MotionRect Area(NpcMotionState n,PredictionEnvironment e,int elapsed=0)
        {
            // Controllers that explicitly capture native top-left must retain
            // its field store when TargetClosest builds an integer face box.
            if(n.TrackingKind<2 && n.PlayerPreciseAreaCaptured && n.PlayerIndex==e.PlayerIndex)return n.PlayerPreciseArea;
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
            {n.PlayerPreciseAreaCaptured=n.ClosestPlayerPreciseAreaCaptured;n.PlayerPreciseArea=n.ClosestPlayerPreciseArea;n.PlayerActive=n.ClosestPlayerActive;n.PlayerNativeDead=n.ClosestPlayerNativeDead;n.PlayerGhost=n.ClosestPlayerGhost;n.PlayerIndex=n.ClosestPlayerIndex;n.PlayerArea=n.ClosestPlayerArea;n.HasPlayer=true;n.PlayerDead=n.ClosestPlayerDead;n.PlayerWet=n.ClosestPlayerWet;n.PlayerIdle=n.ClosestPlayerIdle;n.PlayerGraveyard=n.ClosestPlayerGraveyard;n.PlayerCrimson=n.ClosestPlayerCrimson;n.PlayerJungle=n.ClosestPlayerJungle;n.PlayerTemple=n.ClosestPlayerTemple;n.PlayerGross=n.ClosestPlayerGross;n.PlayerCenterX=n.ClosestPlayerCenterX;n.PlayerCenterY=n.ClosestPlayerCenterY;n.PlayerVelocityCaptured=n.ClosestPlayerVelocityCaptured;n.PlayerVx=n.ClosestPlayerVx;n.PlayerVy=n.ClosestPlayerVy;n.PlayerDirection=n.ClosestPlayerDirection;n.PlayerGravityDirection=n.ClosestPlayerGravityDirection;}
            n.TargetNoAggro=n.ClosestPlayerNoAggro;n.FormTargetNoAggro=n.FormClosestNoAggro;e=Player(n,e);
            n.PlayerAttackHidden=n.ClosestPlayerAttackHidden;
            n.PlayerDesert=n.ClosestPlayerDesert;n.PlayerSandstorm=n.ClosestPlayerSandstorm;n.PlayerUndergroundDesert=n.ClosestPlayerUndergroundDesert;
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
                else
                {
                    // Native TargetClosest materializes an integer targetRect,
                    // even when its player or guardian has a subpixel position.
                    // Keep exact motor Center reads separate from this facing.
                    n.Direction=(int)area.X+(int)area.Width/2<n.X+n.Width/2?-1:1;
                    n.DirectionY=(int)area.Y+(int)area.Height/2<n.Y+n.Height/2?-1:1;
                }
            }
            // Native applies confusion after either selection branch, even
            // when faceTarget suppressed numbered-player facing.
            if(confused)n.Direction=-n.Direction;
        }
        internal static PredictionEnvironment Player(NpcMotionState n,PredictionEnvironment e)
        {
            if(!n.TargetCaptured)return e;
            e.Graveyard=n.PlayerGraveyard;e.Crimson=n.PlayerCrimson;e.Jungle=n.PlayerJungle;e.Temple=n.PlayerTemple;e.PlayerGross=n.PlayerGross;if(n.PlayerIndex==e.PlayerIndex)return e;
            e.PlayerVelocityCaptured=n.PlayerVelocityCaptured;e.PlayerVx=n.PlayerVx;e.PlayerVy=n.PlayerVy;e.PlayerIndex=n.PlayerIndex;e.PlayerX=n.PlayerVelocityCaptured?n.PlayerCenterX:n.PlayerArea.CenterX;e.PlayerY=n.PlayerVelocityCaptured?n.PlayerCenterY:n.PlayerArea.CenterY;e.PlayerWidth=n.PlayerArea.Width;e.PlayerHeight=n.PlayerArea.Height;e.PlayerDead=!n.HasPlayer || n.PlayerDead;e.PlayerWet=n.PlayerWet;e.PlayerIdleWithNegativeAggro=n.PlayerIdle;return e;
        }
    }
}
