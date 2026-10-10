using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI16's independent observed actions precede its shared swim. The random
    // 615 choice stays bounded; its already selected leap/surface and 688's
    // deterministic hit response continue on immutable local liquid facts.
    internal static class NpcAquaticActionMotion
    {
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,out bool handled,out PredictionStop stop)
        {
            handled=false;stop=PredictionStop.None;int type=n.EffectiveType;
            if(type==688)
            {
                if(!e.Multiplayer)
                {
                    if(n.JustHit && n.A2==0){n.A2=1;n.L0=180;}
                    else if(--n.L0<=0){n.L0=120;if(n.A2==1)n.A2=0;if(n.JustHit)n.A2=1;}
                }
                if(n.A2!=1)return Common(n,e,out stop);
                handled=true;n.Vx*=.98f;n.Vy*=.98f;bool found;float line;
                if(!WaterLine(n,e,terrain,out found,out line,out stop))return false;
                if(found)
                {
                    float top=n.Y-5;
                    if(n.Bounds.CenterY>line){n.Vy=Math.Max(-2,n.Vy-.4f);if(top+n.Vy<line)n.Vy=line-top;}
                    else{n.Vy=Math.Min(n.Vy,line-top);if(Math.Abs(top-line)<2)n.Vy=0;}
                }
                else n.Vy=n.Wet?Math.Max(-10,n.Vy-.3f):Math.Min(10,n.Vy+.3f);
                return true;
            }
            if(type!=615)return true;
            if(n.A2==0)
            {
                // Each native action draws its threshold in [300,1200).
                // Below 300 every draw shares this exact clock/motor. No
                // random destination or representative chosen action is used.
                if(++n.A3>=300){stop=PredictionStop.RandomDecision;return false;}
                return Common(n,e,out stop);
            }
            if(n.A2!=1 && n.A2!=2)return Common(n,e,out stop);
            handled=true;
            if(n.CollideX || n.CollideY){n.A2=n.A3=0;return true;}
            if(n.A2==1)
            {
                if(n.Wet){n.Vy=Math.Max(-6,n.Vy-.4f);if(n.A3==1)n.A2=n.A3=0;}
                else{n.A3=1;n.Vy=Math.Min(10,n.Vy+.3f);}
                return true;
            }
            if(!n.Wet){n.A2=n.A3=0;n.Vy=Math.Min(10,n.Vy+.3f);return true;}
            n.Vy=Math.Max(-6,n.Vy-.4f);bool surface;float height;
            if(!WaterLine(n,e,terrain,out surface,out height,out stop))return false;
            if(surface)
            {
                n.Vy=Math.Max(-2,Math.Min(.5f,height-n.Y));n.Vx*=.95f;
                if(++n.A3>=300){n.A2=n.A3=0;n.Vy=4;}
                // Native ai3==60 sound RNG does not feed this motor.
            }
            return true;
        }
        private static bool Common(NpcMotionState n,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            // These two repaired families must also end before their real
            // server-side dry flop chooses a new velocity. The shared legacy
            // representative for other fish is not changed by this repair.
            if(!n.Wet && n.Vy==0 && !e.Multiplayer){stop=PredictionStop.RandomDecision;return false;}return true;
        }
        private static bool WaterLine(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,out bool found,out float line,out PredictionStop stop)
        {
            found=false;line=0;stop=PredictionStop.None;
            int x=(int)(n.Bounds.CenterX/16),y=(int)(n.Y/16);
            if(x<10 || x>=e.WorldWidth-10 || y<10 || y>=e.WorldHeight-10)return true;
            if(terrain==null){stop=PredictionStop.MissingDependency;return false;}
            PredictionTile tile;if(!terrain.Tile(x,y-2,out tile,out stop))return false;if(tile.Liquid>0)return true;
            for(int row=y-1;row<=y+1;row++)
            {
                if(!terrain.Tile(x,row,out tile,out stop))return false;if(tile.Liquid==0)continue;
                // Collision.GetWaterLine uses INTEGER liquid/16; do not use
                // the fractional player water-walk support height here.
                line=(row+1)*16-tile.Liquid/16;found=true;return true;
            }
            return true;
        }
    }
}
