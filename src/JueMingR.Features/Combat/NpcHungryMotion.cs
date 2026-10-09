using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcHungryMotion
    {
        internal static bool NeedsPlayer(NpcMotionState n){return !n.JustHit && n.A1==0;}
        internal static bool Step(ref NpcMotionState n,NpcMotionState owner,PredictionEnvironment e,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(n.JustHit)n.A1=10;
            NpcTargeting.Face(ref n,ref e,true,confused);
            float acc=.1f,radius=300;
            if(owner.Life<owner.LifeMax*.5f){n.Health.Defense=30;if(e.Expert)acc+=.066f;else radius=700;}
            else if(owner.Life<owner.LifeMax*.75f){n.Health.Defense=20;if(e.Expert)acc+=.033f;else radius=500;}
            if(e.Expert)
            {
                // Normal mode leaves previously raised defense alone at >=75%;
                // only Expert restores actual defDefense before collision.
                n.Health.Defense=n.Health.DefaultDefense;
                int slot=n.Identity.Slot;if(slot%4==0)radius*=1.75f;else if(slot%4==1)radius*=1.5f;else if(slot%4==2)radius*=1.25f;if(slot%3==0)radius*=1.5f;else if(slot%3==1)radius*=1.25f;radius*=.75f;
            }
            // The >200 action still uses this action's enlarged radius; only
            // the next action sees the reset clock/base radius.
            if(++n.A2>100){radius=(int)(radius*1.3f);if(n.A2>200)n.A2=0;}
            float anchorX=owner.X+owner.Width/2,anchorY=n.PositionParameter;
            float dx=e.PlayerX-n.Width/2-anchorX,dy=e.PlayerY-n.Height/2-anchorY;
            if(n.A1==0)
            {
                float distance=(float)Math.Sqrt(dx*dx+dy*dy);if(distance>radius){float scale=radius/distance;dx*=scale;dy*=scale;}
                Axis(ref n.Vx,n.X,anchorX+dx,dx,acc);Axis(ref n.Vy,n.Y,anchorY+dy,dy,acc);
                float max=4;
                if(e.Expert)
                {
                    // Locked native operands are int BEFORE assignment to a
                    // float. Fractional life here would change native speed.
                    float ratio=owner.Life/owner.LifeMax,bonus=1.5f;
                    if(ratio<.75f)bonus+=.7f;if(ratio<.5f)bonus+=.7f;if(ratio<.25f)bonus+=.9f;if(ratio<.1f)bonus+=.9f;
                    bonus=bonus*1.25f+.3f;max+=bonus*.35f;
                    if(n.Bounds.CenterX<owner.Bounds.CenterX && owner.Vx>0 || n.Bounds.CenterX>owner.Bounds.CenterX && owner.Vx<0)max+=6;
                }
                n.Vx=Math.Max(-max,Math.Min(max,n.Vx));n.Vy=Math.Max(-max,Math.Min(max,n.Vy));
            }
            else if(n.A1>0)n.A1--;else n.A1=0;
            if(dx>0)n.SpriteDirection=1;else if(dx<0)n.SpriteDirection=-1;return true;
        }
        private static void Axis(ref float velocity,float position,float goal,float offset,float acceleration)
        {if(position<goal){velocity+=acceleration;if(velocity<0 && offset>0)velocity+=acceleration*2.5f;}else if(position>goal){velocity-=acceleration;if(velocity>0 && offset<0)velocity-=acceleration*2.5f;}}
    }
}
