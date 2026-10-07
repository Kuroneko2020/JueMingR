using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI25's ordinary chest family has known wait/jump clocks. A client's
    // dormant observation remains dormant until a new authoritative sample;
    // prediction never invents the server's activation packet.
    internal static class NpcMimicMotion
    {
        internal static bool Known(int type){return type==85 || type==341 || type==629;}
        private static bool FrozenTarget(NpcMotionState n,PredictionEnvironment e){return n.EffectiveType==341 && !e.SnowMoon;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {
            return n.A3==0 && n.Y/16<=e.WorldHeight-200 && n.Y/16>e.WorldSurface ||
                !FrozenTarget(n,e) && (n.A0==0 || n.Vy==0 && n.A2+1>=(n.A1==0?12:20));
        }
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e,int remaining)
        {
            if(n.A0==0)return !e.Multiplayer;
            if(FrozenTarget(n,e))return false;
            // Air steering and waiting consume no future target geometry.
            // The rolling owner rechecks each actual phase, and starts a
            // single timeline from point zero when a real jump first needs it.
            return n.Vy==0 && n.A2+1>=(n.A1==0?12:20);
        }
        internal static void Step(ref NpcMotionState n,PredictionEnvironment e,bool confused)
        {
            if(n.A3==0)
            {
                n.X+=8;
                if(n.Y/16>e.WorldHeight-200)n.A3=3;
                else if(n.Y/16>e.WorldSurface){NpcTargeting.Face(ref n,ref e,true,confused);n.A3=2;}
                else n.A3=1;
            }
            if(n.EffectiveType==341 || n.EffectiveType==629)n.A3=1;
            bool fixedTarget=FrozenTarget(n,e);
            if(n.A0==0)
            {
                if(!fixedTarget)NpcTargeting.Face(ref n,ref e,true,confused);
                if(e.Multiplayer)return;
                if(n.Vx!=0 || n.Vy<0 || n.Vy>.3f){n.A0=1;return;}
                float px=(int)(e.PlayerX-e.PlayerWidth/2),py=(int)(e.PlayerY-e.PlayerHeight/2);
                if((int)n.X-100<px+e.PlayerWidth && (int)n.X+n.Width+100>px && (int)n.Y-100<py+e.PlayerHeight && (int)n.Y+n.Height+100>py || n.Life<n.LifeMax)n.A0=1;
            }
            else if(n.Vy==0)
            {
                if(++n.A2<(n.A1==0?12:20)){n.Vx*=.9f;return;}
                n.A2=0;if(!fixedTarget)NpcTargeting.Face(ref n,ref e,true,confused);
                if(n.Direction==0)n.Direction=-1;n.SpriteDirection=n.Direction;
                if(++n.A1==2){n.Vx=n.Direction*2.5f;n.Vy=-8;n.A1=0;}
                else{n.Vx=n.Direction*3.5f;n.Vy=-4;}
            }
            else if(n.Direction==1 && n.Vx<1)n.Vx+=.1f;
            else if(n.Direction==-1 && n.Vx>-1)n.Vx-=.1f;
        }
    }
}
