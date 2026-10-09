using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcSupportMotion
    {
        internal static bool Known(int type){return type==69;}
        internal static bool NeedsPlayer(NpcMotionState n){return n.Vx!=0;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            NpcTargeting.Face(ref n,ref e,true,confused);
            // Native's (Vx > -.1 || Vx < .1) is always true for finite Vx.
            // Facing upwards therefore zeros X, rather than only damping it.
            // With Vx already zero this aim/attack direction needs no player
            // motion future; the actual path is solely the support rule.
            if(n.DirectionY<0 && n.Vx!=0)n.Vx=0;
            if(n.A0>0)n.A0--;
            stop=PredictionStop.None;bool supported=false;
            int left=(int)n.X/16,mid=(int)(n.X+n.Width/2)/16,right=(int)(n.X+n.Width)/16,row=(int)(n.Y+n.Height)/16;
            for(int i=0;i<3;i++)
            {
                PredictionTile cell;
                if(!terrain.Tile(i==0?left:i==1?mid:right,row,out cell,out stop))return false;
                supported|=cell.Active && cell.Solid;
            }
            // Native reads/initializes all three cells BEFORE its support OR;
            // a missing neighbor is necessary even beside known solid. Keep
            // the source/bounds safeguard and never reproduce native Tile writes.
            if(supported){n.NoGravity=n.NoTileCollide=true;n.Vy=-.2f;return true;}
            n.NoGravity=n.NoTileCollide=false;return true;
        }
    }
}
