using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcWormMotion
    {
        internal static bool KnownHead(int type){return type==7 || type==10 || type==13 || type==39 || type==95 || type==98;}
        internal static void Target(ref NpcMotionState n,PredictionEnvironment e,int oldTarget,bool confused)
        {
            // FixedTarget is an explicit forecast premise. Match native facing
            // only where this AI actually calls TargetClosest; body segments
            // with a valid target must retain their prior facing. Negative
            // aggro uses the target from before AI, not the newly chosen one.
            n.Target=e.PlayerIndex;
            if(!e.PlayerDead && !(n.TargetNoAggro && n.Direction!=0) && !(e.PlayerIdleWithNegativeAggro && oldTarget>=0 && oldTarget<255 && !n.Boss))
            {n.Direction=(int)(e.PlayerX-e.PlayerWidth/2)+(int)e.PlayerWidth/2<n.X+n.Width/2?-1:1;n.DirectionY=(int)(e.PlayerY-e.PlayerHeight/2)+(int)e.PlayerHeight/2<n.Y+n.Height/2?-1:1;}
            if(confused)n.Direction=-n.Direction;
        }
        internal static bool Head(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int oldTarget,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.Identity.Type;
            bool destroyer=n.Style==37;
            if(destroyer && e.MechQueenUp){stop=PredictionStop.MissingDependency;return false;}
            if(!destroyer && (e.PlayerDead || (type==10 || type==39 || type==95) && e.PlayerY-e.PlayerHeight/2<e.WorldSurface*16))
            {n.TimeLeft=Math.Min(n.TimeLeft,300);if(type==10 || type==39 || type==95)n.Vy+=.2f;}
            bool earth=e.SkyblockLowTiles && type==13;
            // Worm terrain sensing deliberately uses whole tile squares,
            // including liquid >64. It differs from body/triangle collision.
            for(int x=Math.Max(0,(int)(n.X/16)-1);x<Math.Min(e.WorldWidth,(int)((n.X+n.Width)/16)+2) && !earth;x++)
            for(int y=Math.Max(0,(int)(n.Y/16)-1);y<(e.WorldHeight>0?Math.Min(e.WorldHeight,(int)((n.Y+n.Height)/16)+2):(int)((n.Y+n.Height)/16)+2);y++)
            {
                PredictionTile tile;if(!terrain.Tile(x,y,out tile,out stop))return false;
                if((tile.Active && (tile.Solid || tile.SolidTop && tile.SurfacePlatform) || tile.Liquid>64) && n.X<x*16+16 && n.X+n.Width>x*16 && n.Y<y*16+16 && n.Y+n.Height>y*16){earth=true;break;}
            }
            if(destroyer)n.L1=earth?0:1;
            if(!earth && (!destroyer || n.Y>e.PlayerY-e.PlayerHeight/2))
            {
                bool close=false;int count=e.Players==null?1:e.Players.Count;
                for(int i=0;i<count;i++)
                {var p=e.Players==null?new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight):e.Players[i];if((int)n.X<(int)p.X+1000 && (int)n.X+n.Width>(int)p.X-1000 && (int)n.Y<(int)p.Y+1000 && (int)n.Y+n.Height>(int)p.Y-1000){close=true;break;}}
                earth=!close;
            }
            float speed=destroyer?16:type==10?6:type==13?(e.Expert?12:10):type==95?5.5f:type==98?8:9;
            float acceleration=type==10?.05f:type==13?(e.Expert?.15f:.07f):type==95?.045f:type==98?.07f:.1f,additional=.15f;
            if(e.GoodWorld){if(destroyer){acceleration*=1.2f;additional*=1.2f;}else if(type==13){speed+=4;acceleration+=.05f;}else if(type==39){speed=10;acceleration=.12f;}}
            if(destroyer && (e.Day && !e.Remix || e.PlayerDead))
            {earth=false;n.Vy++;if(n.Y>e.WorldSurface*16){n.Vy++;speed=32;}if(n.Y>e.RockLayer*16){stop=PredictionStop.Despawn;return false;}}
            // Body/tail share wet-earth sensing and linked retreat, then use
            // their parent constraint. Body L0 is a random shooting clock,
            // not the head's movement-state flag; never overwrite it here.
            if(destroyer && n.ParentSlot>=0)return true;
            float dx=(int)(e.PlayerX/16)*16-(int)(n.Bounds.CenterX/16)*16,dy=(int)(e.PlayerY/16)*16-(int)(n.Bounds.CenterY/16)*16;
            if(!earth)
            {
                Target(ref n,e,oldTarget,confused);
                n.Vy+=destroyer?.15f:type==39 && n.Vy<0?.08f:.11f;n.Vy=Math.Min(speed,n.Vy);
                if(Math.Abs(n.Vx)+Math.Abs(n.Vy)<speed*.4)n.Vx+=n.Vx<0?-acceleration*1.1f:acceleration*1.1f;
                else if(n.Vy==speed)Toward(ref n.Vx,dx,acceleration);
                else if(n.Vy>4)n.Vx+=n.Vx<0?acceleration*.9f:-acceleration*.9f;
            }
            else
            {
                float ax=Math.Abs(dx),ay=Math.Abs(dy),distance=(float)Math.Sqrt(dx*dx+dy*dy);dx*=speed/distance;dy*=speed/distance;
                if(!destroyer && (e.PlayerDead || (type==7 || type==13) && !e.Corrupt && !e.Crimson) && !e.AnyLivingCorrupt)
                {dx=0;dy=speed;if(!e.Multiplayer && e.WorldHeight>0 && n.Y/16>(e.RockLayer+e.WorldHeight)/2){stop=PredictionStop.Despawn;return false;}}
                // Do not normalize away the native zero-distance NaN locals:
                // comparisons then skip acceleration and enter the low-speed
                // fallback. Persisted position/velocity remain finite.
                if(destroyer && SameSign(n.Vx,dx) && SameSign(n.Vy,dy)){Toward(ref n.Vx,dx,additional);Toward(ref n.Vy,dy,additional);}
                if(SameSign(n.Vx,dx) || SameSign(n.Vy,dy))
                {
                    Toward(ref n.Vx,dx,acceleration);Toward(ref n.Vy,dy,acceleration);
                    if(Math.Abs(dy)<speed*.2f && Opposite(n.Vx,dx))n.Vy+=n.Vy>0?acceleration*2:-acceleration*2;
                    if(Math.Abs(dx)<speed*.2f && Opposite(n.Vy,dy))n.Vx+=n.Vx>0?acceleration*2:-acceleration*2;
                }
                else if(ax>ay){Toward(ref n.Vx,dx,acceleration*1.1f);if(Math.Abs(n.Vx)+Math.Abs(n.Vy)<speed*.5f)n.Vy+=n.Vy>0?acceleration:-acceleration;}
                else{Toward(ref n.Vy,dy,acceleration*1.1f);if(Math.Abs(n.Vx)+Math.Abs(n.Vy)<speed*.5f)n.Vx+=n.Vx>0?acceleration:-acceleration;}
            }
            n.L0=earth?1:0;return true;
        }
        private static bool SameSign(float a,float b){return a>0 && b>0 || a<0 && b<0;}
        private static bool Opposite(float a,float b){return a>0 && b<0 || a<0 && b>0;}
        private static void Toward(ref float value,float target,float amount){if(value<target)value+=amount;else if(value>target)value-=amount;}
    }
}
