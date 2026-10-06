using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class PlayerHorizontalMotion
    {
        internal static void Step(ref PredictionPlayerMotion p)
        {
            float wind=0;
            if(p.WindPushed)
            {wind=Math.Sign(p.WindSpeed)*.06f;if(Math.Abs(p.WindSpeed)>.5f)wind*=1.37f;if(p.Vy!=0)wind*=1.5f;if(p.Left || p.Right)wind=Math.Max(-.072f,Math.Min(.072f,wind*.8f));}
            if(p.TrackBoost!=0){p.Vx=Math.Max(-p.MaxSpeed,Math.Min(p.MaxSpeed,p.Vx+p.TrackBoost));p.TrackBoost=0;}
            // maxRunSpeed and accRunSpeed are different branch thresholds.
            // Keep branch entry, reversal and wrong-ground order; reaching a
            // threshold may enter native friction on the following update.
            if(p.Left && p.Vx>-p.MaxSpeed)
            {
                if(!p.Cart || p.Vy==0){if(p.Vx>p.Slowdown)p.Vx-=p.Slowdown;p.Vx-=p.Acceleration;}
                if(p.OnWrongGround)p.Vx=p.Vx< -p.Slowdown?p.Vx+p.Slowdown:0;
            }
            else if(p.Right && p.Vx<p.MaxSpeed)
            {
                if(!p.Cart || p.Vy==0){if(p.Vx< -p.Slowdown)p.Vx+=p.Slowdown;p.Vx+=p.Acceleration;}
                if(p.OnWrongGround)p.Vx=p.Vx>p.Slowdown?p.Vx-p.Slowdown:0;
            }
            else if(p.Left && p.Vx>-p.FastMaxSpeed && p.DashDelay>=0)
            {
                if(p.Vy==0 || p.Wings || p.CanFly){if(p.Vx>p.Slowdown)p.Vx-=p.Slowdown;p.Vx-=p.Acceleration*.2f;if(p.Wings)p.Vx-=p.Acceleration*.2f;}
                // The asymmetry is present in locked .8; it is not a typo.
                if(p.OnWrongGround)p.Vx=p.Vx<p.Slowdown?p.Vx+p.Slowdown:0;
            }
            else if(p.Right && p.Vx<p.FastMaxSpeed && p.DashDelay>=0)
            {
                if(p.Vy==0 || p.Wings || p.CanFly){if(p.Vx< -p.Slowdown)p.Vx+=p.Slowdown;p.Vx+=p.Acceleration*.2f;if(p.Wings)p.Vx+=p.Acceleration*.2f;}
                if(p.OnWrongGround)p.Vx=p.Vx>p.Slowdown?p.Vx-p.Slowdown:0;
            }
            else if(p.Cart && Math.Abs(p.Vx)>=1)
            {if(p.OnWrongGround)p.Vx=NpcMotion.Approach(p.Vx,0,p.Slowdown);p.Vx=Math.Max(-p.MaxSpeed,Math.Min(p.MaxSpeed,p.Vx));}
            else if(p.Vy==0 || !p.PortalPhysics)p.Vx=NpcMotion.Approach(p.Vx,0,p.Vy==0?p.Slowdown:p.Slowdown*.5f);
            if(wind<0 && p.Vx>wind)p.Vx=Math.Max(wind,p.Vx+wind);
            if(wind>0 && p.Vx<wind)p.Vx=Math.Min(wind,p.Vx+wind);
        }
    }
}
