using System;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class PredictionTerrain
    {
        // Fixed .8 private scalar, cached once. This accessor is read-only at
        // every call site; it must never be used to modify the live player.
        private static readonly HarmonyLib.AccessTools.FieldRef<Player,bool> shimmerImmunity=HarmonyLib.AccessTools.FieldRefAccess<Player,bool>("shimmerImmune");
        internal static bool PlayerShimmerImmune(Player p){return shimmerImmunity(p);}
        internal static bool FloatingObserved(Player p)
        {
            if(!p.ShouldFloatInWater || !p.wet || p.shimmerWet && !PlayerShimmerImmune(p) || !(p.shimmerWet || p.honeyWet && !p.ignoreWater || !p.merman && !p.ignoreWater && !p.trident))return false;
            int x=(int)(p.Center.X/16),y=(int)(p.Center.Y/16);float line=0;bool exists=false;
            if(FloatLineInWorld(x,y))
            {
                Cell a,b,c,d;
                // Original GetWaterLine allocates missing real Tiles. Never
                // call it from observation. Unknown player-only line input
                // instead chooses the explicit finite observed premise.
                if(!Read(x,y-2,out a) || !Read(x,y-1,out b) || !Read(x,y,out c) || !Read(x,y+1,out d))return true;
                exists=FloatLine(y,a.Liquid,b.Liquid,c.Liquid,d.Liquid,out line);
            }
            return !exists || p.Center.Y-(p.mount.Active && p.mount.Type==37?6:0)+8+p.velocity.Y>=line;
        }
        private bool FloatingConstraint(NpcMotionState n,bool mount37,out bool constrained,out PredictionStop stop)
        {
            stop=PredictionStop.None;constrained=true;int x=(int)(n.Bounds.CenterX/16),y=(int)(n.Bounds.CenterY/16);float line;
            if(!FloatLineInWorld(x,y))return true;
            Cell a,b,c,d;
            if(!CellAt(x,y-2,out a,out stop) || !CellAt(x,y-1,out b,out stop) || !CellAt(x,y,out c,out stop) || !CellAt(x,y+1,out d,out stop))return false;
            if(FloatLine(y,a.Liquid,b.Liquid,c.Liquid,d.Liquid,out line))constrained=n.Bounds.CenterY-(mount37?6:0)+8+n.Vy>=line;
            return true;
        }
        private static bool FloatLineInWorld(int x,int y)
        {return x>=10 && y>=10 && x<Main.maxTilesX-10 && y<Main.maxTilesY-10;}
        private static bool FloatLine(int y,byte aboveTwo,byte above,byte at,byte below,out float line)
        {
            line=0;if(aboveTwo>0)return false;
            if(above>0){line=y*16-above/16;return true;}
            if(at>0){line=(y+1)*16-at/16;return true;}
            if(below>0){line=(y+2)*16-below/16;return true;}
            return false;
        }
        // Player geometry has its own phase order and predicates. Tile values
        // join the same bounded snapshot, but NPC body/AI helpers are not used.
        private bool PlayerSteps(ref NpcMotionState n,ref PredictionPlayerMotion p,bool segment,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(!p.SkipSlope){n.Gravity=p.Gravity*p.GravityDirection;if(!WalkDown(ref n,out stop))return false;}
            if(n.Vy==p.Gravity && !p.RidingTracks && !p.SkipSlope && !p.StepMount)
                if(!PlayerStepDown(ref n,ref p,out stop))return false;
            bool up=p.GravityDirection<0?(p.Carpet || n.Vy<=p.Gravity) && !p.Up:
                segment?p.StepMount || (p.Carpet || n.Vy>=p.Gravity) && !p.Down && !p.Cart:
                (p.Carpet || n.Vy>=p.Gravity) && !p.Down && !p.RidingTracks && !p.StepMount && !p.Grappled;
            return !up || PlayerStepUp(ref n,ref p,out stop);
        }
        private bool PlayerStepDown(ref NpcMotionState n,ref PredictionPlayerMotion p,out PredictionStop stop)
        {
            stop=PredictionStop.None;float projectedX=n.X+n.Vx,baseY=(float)Math.Floor((n.Y+n.Height)/16)*16-n.Height;
            int left=(int)(projectedX/16),right=(int)((projectedX+n.Width)/16),row=(int)((baseY+n.Height+4)/16),rows=(n.Height+15)/16;float surface=(row+rows)*16;
            for(int x=left;x<=right;x++)for(int y=row;y<=row+1;y++)
            {
                if(x<1 || y<1 || x>=Main.maxTilesX-1 || y>=Main.maxTilesY-1)continue;
                Cell c,above;if(!CellAt(x,y,out c,out stop) || !CellAt(x,y-1,out above,out stop))return false;
                if(p.WaterWalk && c.Liquid>0 && above.Liquid==0 && Rectangles((int)n.X,(int)n.Y,n.Width,n.Height,x*16,y*16-17,16,16))
                    surface=Math.Min(surface,y*16+16-(c.Liquid/32*2+2));
                if((y>=Main.bottomWorld/16-42 || c.Solid || c.Platform) && Rectangles(n.X,n.Y,n.Width,n.Height,x*16,y*16-17,16,16))surface=Math.Min(surface,y*16+(c.Half?8:0));
            }
            float drop=surface-(n.Y+n.Height);
            if(drop>7 && drop<17){p.StepSpeed=drop>9?2.5f:1.5f;p.GfxOffset-=drop;n.Y=surface-n.Height;}
            return true;
        }
        private bool PlayerStepUp(ref NpcMotionState n,ref PredictionPlayerMotion p,out PredictionStop stop)
        {
            stop=PredictionStop.None;int direction=Math.Sign(n.Vx),grav=(int)p.GravityDirection;
            float projectedX=n.X+n.Vx;int x=(int)((projectedX+n.Width/2+(n.Width/2+1)*direction)/16),y=grav>0?(int)((n.Y+n.Height-1)/16):(int)(((double)n.Y+.1)/16),rows=(n.Height+15)/16;
            if(x<1 || y<1 || x>=Main.maxTilesX-1 || y>=Main.maxTilesY-40)return true;
            Cell body,near,far,behind;if(!CellAt(x,y,out body,out stop) || !CellAt(x,y-grav,out near,out stop) || !CellAt(x,y-(rows+1)*grav,out far,out stop) || !CellAt(x-direction,y-rows*grav,out behind,out stop))return false;
            bool pass=true;for(int row=1;row<rows+2;row++){Cell c;if(!CellAt(x,y-row*grav,out c,out stop))return false;if(row>=2 && row<=rows)pass&=PlayerPass(c);}
            bool nearPass,step;
            if(grav>0)
            {
                float center=n.X+n.Width/2;
                nearPass=PlayerPass(near) || near.Slope==1 && center>x*16 || near.Slope==2 && center<x*16+16 || near.Half && PlayerPass(far);
                bool top=body.Slope==1 || body.Slope==2;
                step=body.Active && (!top || body.Slope==1 && center<x*16 || body.Slope==2 && center>x*16+16) && (!top || n.Y+n.Height>y*16) &&
                    (body.RawSolid && !body.RawPlatform || p.Up && (body.RawPlatform && body.PlatformFrame || body.StairPlatform || body.Type==380) && (!near.RawSolid || !near.Active)) || near.Half && near.Active;
                step&=!body.RawPlatform || !near.RawPlatform;
            }
            else
            {nearPass=PlayerPass(near) || near.Slope!=0 || near.Half && PlayerPass(far);step=body.Active && (body.RawSolid && !body.RawPlatform || p.Up && body.RawPlatform && body.PlatformFrame && (!near.RawSolid || !near.Active)) || near.Half && near.Active;}
            if(!pass || !PlayerPass(behind) || !nearPass || !step || projectedX+n.Width<=x*16 || projectedX>=x*16+16)return true;
            if(grav>0)
            {float surface=y*16+(near.Half?-8:body.Half?8:0),rise=n.Y+n.Height-surface;if(rise>0 && rise<=16.1f){p.GfxOffset+=rise;n.Y=surface-n.Height;p.StepSpeed=rise<9?1:2;}}
            else if(body.Slope!=3 && body.Slope!=4 && !near.StairPlatform)
            {float surface=y*16+16,rise=surface-n.Y;if(rise>0 && rise<=16.1f){p.GfxOffset-=rise;n.Y=surface;n.Vy=0;p.StepSpeed=rise<9?1:2;}}
            return true;
        }
        private static bool PlayerPass(Cell c){return !c.Active || !c.RawSolid || c.RawPlatform;}
        private bool PlayerTail(ref NpcMotionState n,ref PredictionPlayerMotion p,bool fall,int grav,out PredictionStop stop)
        {
            // DryCollision owns slope/belt per segment. Player.Update still
            // performs this separate final pair after the summed velocity is
            // restored; it also owns the last up/down collision scratch.
            if(p.IgnorePlatforms || p.Down || p.Grappled || grav<0)n.StairFall=true;
            if(!Slopes(ref n,fall,out stop,true,grav))return false;
            return p.SkipConveyor || Math.Abs(p.GfxOffset)>2 || Conveyor(ref n,true,p.OnTrack,grav,out stop);
        }
        private static bool Rectangles(float x,float y,float w,float h,float tx,float ty,float tw,float th)
        {return x<tx+tw && x+w>tx && y<ty+th && y+h>ty;}
        private bool PlayerSegments(ref NpcMotionState n,PredictionEnvironment e,ref PredictionPlayerMotion p,bool waterWalk,bool fall,bool lavaWalk,int grav,out PredictionStop stop)
        {
            float length=(float)Math.Sqrt(n.Vx*n.Vx+n.Vy*n.Vy),limit=Math.Min(16,Math.Min(n.Width-.5f,n.Height-.5f));
            if(limit<=0 || length/limit>128){stop=PredictionStop.InvalidState;return false;}
            float dx,dy;bool up,down;int tileHeight=n.Height-(p.OnTrack?10:0);
            if(!TileContact(n.X,n.Y,n.Vx,n.Vy,n.Width,tileHeight,fall,out dx,out dy,out up,out down,out stop,p.IgnorePlatforms,grav))return false;
            float ux=n.Vx/length,uy=dy==0?0:n.Vy/length,sumX=0,sumY=0;bool dryHead=false;
            while(length>0)
            {
                float amount=Math.Min(length,limit);length-=amount;n.Vx=ux*amount;n.Vy=uy*amount;
                if(!Move(ref n,e,true,waterWalk,fall,lavaWalk,grav,ref p,out stop,true,false))return false;
                dryHead|=n.PlayerDryHeadCollision;if(n.PlayerDryHeadCollision)p.Jump=0;sumX+=n.Vx;sumY+=n.Vy;
            }
            n.Vx=sumX;n.Vy=sumY;n.PlayerDryHeadCollision=dryHead;return true;
        }
    }
}
