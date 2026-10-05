using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcPositionMotion
    {
        internal static bool Known(NpcMotionState n){return n.PositionRelation>=1 && n.PositionRelation<=4;}
        internal static bool Step(ref NpcMotionState n,NpcMotionState[] group,int count,int elapsed,out PredictionStop stop)
        {
            stop=PredictionStop.None;int index=-1;
            for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.PositionOwner)){index=i;break;}
            if(index<0 || !group[index].Active || group[index].Life<=0){stop=PredictionStop.MissingDependency;return false;}
            var owner=group[index];
            switch(n.PositionRelation)
            {
                case 1:
                    n.X=owner.X;n.Direction=n.SpriteDirection=owner.Direction;
                    if(n.Y>n.PositionParameter+1)n.Vy=-1;
                    else if(n.Y<n.PositionParameter-1)n.Vy=1;
                    else{n.Vy=0;n.Y=n.PositionParameter;}
                    // Native eye leaves its observed Vx alone. Assigning the
                    // body's velocity here would integrate that X twice.
                    break;
                case 2:
                    n.X=owner.Bounds.CenterX-n.Width/2;n.Y=owner.Bounds.CenterY-n.Height/2;n.Vx=n.Vy=0;break;
                case 3:
                    n.X=owner.Bounds.CenterX-n.Width*.5f;n.Y=owner.Bounds.CenterY-400-n.Height*.5f;n.Vx=n.Vy=0;break;
                case 4:
                    // Hand velocity remains a qualified observed trend; the
                    // native parent-relative range constraint still applies.
                    NpcRollingMotion.Trend(ref n,elapsed);float sign=n.PositionParameter;
                    float left=owner.Bounds.CenterX+330*sign,right=left+370*sign;if(left>right){float value=left;left=right;right=value;}
                    n.X=Math.Max(left,Math.Min(right,n.Bounds.CenterX+n.Vx))-n.Vx-n.Width*.5f;
                    n.Y=Math.Max(owner.Bounds.CenterY-210,Math.Min(owner.Bounds.CenterY-60,n.Bounds.CenterY+n.Vy))-n.Vy-n.Height*.5f;break;
                case 5:
                    // The leech interpolates to a ProjectileKey(456) future,
                    // not only to this head. We do not fabricate that projectile
                    // timeline or treat the owner alone as sufficient evidence.
                    stop=PredictionStop.UnsupportedMechanism;return false;
                case 6:NpcRollingMotion.Trend(ref n,elapsed);break;
            }
            return true;
        }
    }
}
