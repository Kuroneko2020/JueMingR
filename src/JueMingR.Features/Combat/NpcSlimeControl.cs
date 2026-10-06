using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Shared pure-value slime clocks/target choices. Source and actual AI
    // control use the same first-action rules; no live target is modified.
    internal static class NpcSlimeControl
    {
        internal static bool Aggressive(NpcMotionState n,PredictionEnvironment env)
        {
            int type=n.EffectiveType;
            bool aggressive=!env.Day || n.Life!=n.LifeMax || n.Y>env.WorldSurface*16 || env.SlimeRain;
            if(env.Remix && type==59 && n.Life==n.LifeMax)aggressive=false;
            if(type==81 || type==183 || type==304 || type==667 || type==244 || type==184 || type==535 || type==204 || type==658 || type==659)aggressive=true;
            if((type==377 || type==446) && !env.PlayerDead && !n.Wet && (n.Bounds.CenterX-env.PlayerX)*(n.Bounds.CenterX-env.PlayerX)+(n.Bounds.CenterY-env.PlayerY)*(n.Bounds.CenterY-env.PlayerY)<=40000)aggressive=true;
            return aggressive;
        }
        internal static int PlayerPremiseTarget(NpcMotionState n,PredictionEnvironment env)
        {
            // Original DOT settlement precedes slime aggression and jump
            // clocks. Project the same health rules on this value copy only;
            // the real Step still owns health advancement and group damage.
            if(!NpcHealth.ProjectForTargetChoice(ref n,env))return n.PlayerIndex;
            int type=n.EffectiveType;
            // Preserve old-number consumers before any possible Retarget.
            // Shooting/range and ceiling-controlled special actions are not
            // permission to pretend their old prerequisite was the closest.
            if(!env.PlayerDead && !n.Wet && (type==377 || type==446 || n.Vy==0 && !n.TargetNoAggro && (type==184 || type==535 || type==204 || type==658 || type==659)))return n.PlayerIndex;
            if(type==59 && !env.Remix && n.Wet)return n.PlayerIndex;
            bool aggressive=Aggressive(n,env);
            if(type==244)n.A0+=2;
            BeforeGround(ref n,ref env,aggressive,n.DirectionY,false);
            if(n.Vy==0)GroundDecision(ref n,ref env,aggressive,false);
            return n.PlayerIndex;
        }
        internal static void BeforeGround(ref NpcMotionState n,ref PredictionEnvironment env,bool aggressive,int vertical,bool confused)
        {
            if(n.A2>1)n.A2--;
            if(n.Wet)
            {
                if(n.CollideY)n.Vy=-2;
                if(n.Vy<0 && n.A3==n.X){n.Direction*=-1;n.A2=200;}if(n.Vy>0)n.A3=n.X;
                bool lava=n.Identity.Type==59 && !env.Remix;
                if(n.Vy>2)n.Vy*=.9f;else if(lava && vertical<0)n.Vy-=.8f;
                n.Vy=Math.Max(lava?-10:-4,n.Vy-.5f);
                if(n.A2==1 && aggressive)NpcTargeting.Face(ref n,ref env,true,confused);
            }
            if(n.A2==0){n.A0=-100;n.A2=1;NpcTargeting.Face(ref n,ref env,true,confused);}
        }
        internal static int GroundDecision(ref NpcMotionState n,ref PredictionEnvironment env,bool aggressive,bool confused)
        {
            int type=n.EffectiveType;
            if(n.A3==n.X){n.Direction*=-1;n.A2=200;}n.A3=0;
            n.Vx*=.8f;if(Math.Abs(n.Vx)<.1f)n.Vx=0;
            n.A0+=aggressive?2:1;if(type==59 && !env.Remix || type==138)n.A0+=2;if(type==71 || type==667 || type==659 || type==377 || type==446)n.A0+=3;
            if(type==183)n.A0++;if(type==658)n.A0+=5;if(type==304)n.A0+=(1-n.Life/Math.Max(1,n.LifeMax))*10;if(type==81)n.A0+=n.Scale>=0?4:1;
            float rhythm=type==659?-500:type==667?-400:-1000;
            int jump=n.A0>=0?1:n.A0>=rhythm && n.A0<=rhythm*.5f?2:n.A0>=rhythm*2 && n.A0<=rhythm*1.5f?3:0;
            if(jump!=0 && aggressive && n.A2==1)NpcTargeting.Face(ref n,ref env,true,confused);
            return jump;
        }
    }
}
