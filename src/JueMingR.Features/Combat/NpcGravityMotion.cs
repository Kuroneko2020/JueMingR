using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcGravityMotion
    {
        internal static void BeforeAi(ref NpcMotionState n,PredictionEnvironment e)
        {
            // UpdateNPC fixes this from the current form/phase before AI.
            // A Transform inside AI changes the NEXT update's parameters,
            // not this update's gravity or its already performed speed clip.
            int type=n.EffectiveType;float gravity=.3f,fall=10;
            if(type==258){gravity=.1f;if(n.Vy>3)n.Vy=3;}
            else if(type==425 && n.A2==1)gravity=.1f;
            else if((type==576 || type==577) && n.A0>0 && n.A1==2){gravity=.45f;if(n.Vy>32)n.Vy=32;}
            else if(type==427 && n.A2==1){gravity=.1f;if(n.Vy>4)n.Vy=4;}
            else if(type==426){gravity=.1f;if(n.Vy>3)n.Vy=3;}
            else if(type==541 || n.Style==7 && n.A0==25)gravity=0;
            float scale=e.WorldWidth/4200f;scale*=scale;
            double surface=e.GravityWorldSurface!=0?e.GravityWorldSurface:e.WorldSurface;
            float height=(float)((n.Y/16f-(60f+10f*scale))/(surface/6.0));
            if(height<.25f)height=.25f;if(height>1)height=1;
            gravity*=height;
            // This is the OLD wet state. AI and later collision may enter or
            // leave a fluid, but cannot reselect this update's public values.
            if(n.Wet){gravity=n.Shimmer?.15f:n.Honey?.1f:.2f;fall=n.Shimmer?5.5f:n.Honey?4:7;}
            n.Gravity=gravity;n.MaxFall=fall;
        }
    }
}
