using JueMingR.Platform.Combat;

namespace JueMingR.TerrariaHost.Combat
{
    // Locked 1.4.5.8 collision predicates consume the predicted form, never
    // the immutable selection identity. Player collision has a separate entry.
    internal static class NpcCollisionRules
    {
        internal static MotionRect MovementBounds(NpcMotionState n)
        {
            int w=n.Width,h=n.Height,type=n.EffectiveType;float x=n.X,y=n.Y;
            if(type==594){int extra=(int)(44+20*n.A1);y+=extra;h+=extra;x+=w/2;w=(int)(6+26*n.A1);x-=w/2;}
            if(type==686){y+=64;h+=64;x+=w/2;w=32;x-=w/2;}
            if(type==243)h=90;
            if(type==290 || type==351 || type==482 || type==343 || type==348 || type==349)h=40;
            if((type==391 || type==415) && n.CollisionPart)h=62;
            if(type==576 || type==577){x+=32;w-=64;}
            if(h!=n.Height)y+=n.Height-h;
            return new MotionRect(x,y,w,h);
        }
        internal static bool FallThrough(NpcMotionState n,PredictionEnvironment e)
        {
            int type=n.EffectiveType;
            bool below=n.Target>=0 && e.PlayerY-e.PlayerHeight*.5f>n.Y+n.Height;
            // 620 overwrites the accumulated result at the end of the original
            // method; do not accidentally preserve an earlier style decision.
            if(type==620)return below;
            switch(type)
            {
                case 2:case -43:case 190:case 191:case 192:case 193:case 194:
                case 317:case 318:case 133:case 467:case 477:case 173:
                case 210:case 211:case 247:case 248:case 542:case 543:
                case 544:case 545:case 418:case 405:case 406:case 490:case 301:return true;
                case 469:if(n.A2==1)return true;break;
                case 50:case 657:case 245:if(below)return true;break;
            }
            switch(n.Style)
            {
                case 10:case 5:case 40:case 44:case 22:case 49:case 14:return true;
                case 3:case 107:if(n.DirectionY==1)return true;break;
                case 26:if(n.Target>=0 && e.PlayerY+e.PlayerHeight*.5f-n.Vy>n.Y+n.Height)return true;break;
                case 87:if(below)return true;break;
                case 7:
                    bool home=n.Town && (!e.Day || e.InvasionType>0 || e.Eclipse);
                    return home?(n.Y+n.Height-8)/16f<n.HomeTileY-1:n.HomeTileY-(int)(n.Y+n.Height)/16>16;
            }
            return false;
        }
    }
}
