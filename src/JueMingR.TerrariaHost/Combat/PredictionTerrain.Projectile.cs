using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class PredictionTerrain
    {
        // Locked Collision.IsWorldPointSolid(point, true), including its
        // inclusive slope edges and the one-tile world exclusion. The cell
        // joins this preparation's normal terrain lease; no live query is
        // repeated during each candidate or during presentation.
        internal bool ProjectileWorldPointSolid(Vector2 point,out bool solid)
        {
            solid=false;int x=(int)(point.X/16),y=(int)(point.Y/16);
            if(x<1 || y<1 || x>=Main.maxTilesX-1 || y>=Main.maxTilesY-1)return true;
            Cell cell;PredictionStop stop;if(!CellAt(x,y,out cell,out stop))return false;
            if(!cell.Solid || cell.Type>0 && (cell.StairPlatform || cell.Type==380))return true;
            if(cell.Half)solid=point.X>=x*16 && point.X<=x*16+16 && point.Y>=y*16+8 && point.Y<=y*16+16;
            else if(cell.Slope==0)solid=point.X>=x*16 && point.X<=x*16+16 && point.Y>=y*16 && point.Y<=y*16+16;
            else
            {
                float localX=point.X%16,localY=point.Y%16;
                switch(cell.Slope){case 1:solid=localY>=localX;break;case 2:solid=localX+localY>=16;break;case 3:solid=localX+localY<=16;break;case 4:solid=localY<=localX;break;}
            }
            return true;
        }
        // AI016's damaging acorn retires on a plantable contact instead of
        // exploding. The two possible ground cells and CanCutTile's below-cell
        // facts join this lease; no sapling is placed and no item is dropped.
        internal bool AcornPlant(Vector2 center,out bool plants)
        {
            plants=false;int x=(int)(center.X/16),y=(int)(center.Y/16);
            if(x<5 || y<5 || x>=Main.maxTilesX-5 || y>=Main.maxTilesY-5)return true;
            for(int i=0;i<2;i++)
            {
                Cell ground,one,two;PredictionStop stop;
                if(!CellAt(x,y+i,out ground,out stop))return false;
                if(!ground.Active || ground.Half || ground.Slope==1 || ground.Slope==2)continue;
                if(!CellAt(x,y+i-1,out one,out stop) || !CellAt(x,y+i-2,out two,out stop))return false;
                if(one.RawActive && !Terraria.Main.tileCut[one.Type] || two.RawActive && !Terraria.Main.tileCut[two.Type] || !AcornCut(one,ground) || !AcornCut(two,one))continue;
                switch(ground.Type)
                {
                    case 2:case 23:case 53:case 60:case 109:case 112:case 116:case 147:case 199:case 234:case 477:case 492:case 633:case 661:case 662:plants=true;return true;
                }
            }
            return true;
        }
        private static bool AcornCut(Cell cell,Cell below)
        {return cell.Wall!=350 && cell.CuttableFrame && below.Type!=78 && below.Type!=380 && below.Type!=579;}

        // The collision phase is separate from the type's hit response and
        // final UpdatePosition. Native dry motion subdivides fast movement,
        // applies slopes after every piece and once again at the endpoint.
        // A single full-velocity TileCollision misses those slope contacts.
        // Position is the original top-left body; wet displacement remains
        // independent because native reflection does not rewrite wetVelocity.
        // This is the ordinary collision branch, not AI10 falling blocks,
        // AI137's Y-only slope branch, pets ignoring doors, or the separate
        // correctSlopeCollision post-position pass. Callers own fall-through,
        // tileCollide/phase bypasses and all per-type collision responses.
        // Full autonomous family replays may explicitly use native clipped
        // scans when one sibling leaves the world. This keeps that sibling's
        // real lifetime/history without declaring the whole family unknown;
        // missing cells inside the clipped scan still invalidate the lease.
        internal bool ProjectileTileMotion(Projectile sample,ref Vector2 position,ref Vector2 velocity,bool wet,bool honey,out Vector2 wetVelocity,out bool hit,bool fallThrough=true,bool shimmer=false,bool nativeClip=false)
        {
            wetVelocity=velocity;hit=false;
            if(sample.aiStyle==10 || sample.aiStyle==137 || Main.projPet[sample.type] || sample.correctSlopeCollision)return false;
            Vector2 anchor;int width,height;sample.GetCollisionParams(out anchor,out width,out height);
            Vector2 original=velocity,collisionPosition=position+new Vector2(sample.width,sample.height)*.5f-new Vector2(width,height)*anchor;
            if(wet)
            {
                if(!ProjectileTileContact(collisionPosition,velocity,width,height,fallThrough,out velocity,nativeClip))return false;
                // Native liquid flags can coexist at a boundary. Shimmer
                // wins before honey, before axis corrections and slopes.
                wetVelocity=velocity*(shimmer?.375f:honey?.25f:.5f);
                if(velocity.X!=original.X)wetVelocity.X=velocity.X;
                if(velocity.Y!=original.Y)wetVelocity.Y=velocity.Y;
                if(!sample.correctSlopeCollision && !ProjectileSlopePhase(ref position,ref collisionPosition,ref velocity,width,height,ref hit,nativeClip))return false;
            }
            else
            {
                int size=Math.Max(3,Math.Min(16,Math.Min(width,height)));
                if(velocity.Length()>size)
                {
                    Vector2 full;if(!ProjectileTileContact(collisionPosition,velocity,width,height,fallThrough,out full,nativeClip))return false;
                    float left=velocity.Length();Vector2 direction=velocity.SafeNormalize(Vector2.Zero),sum=Vector2.Zero;
                    if(full.Y==0)direction.Y=0;
                    for(int pieces=0;left>0 && pieces<300;pieces++)
                    {
                        float length=Math.Min(left,size);left-=length;Vector2 piece;
                        if(!ProjectileTileContact(collisionPosition,direction*length,width,height,fallThrough,out piece,nativeClip))return false;
                        collisionPosition+=piece;velocity=piece;
                        if(!sample.correctSlopeCollision && !ProjectileSlopePhase(ref position,ref collisionPosition,ref velocity,width,height,ref hit,nativeClip))return false;
                        sum+=velocity;
                    }
                    velocity=sum;
                    if(Math.Abs(velocity.X-original.X)<.0001f)velocity.X=original.X;
                    if(Math.Abs(velocity.Y-original.Y)<.0001f)velocity.Y=original.Y;
                }
                else if(!ProjectileTileContact(collisionPosition,velocity,width,height,fallThrough,out velocity,nativeClip))return false;
                if(!sample.correctSlopeCollision && !ProjectileSlopePhase(ref position,ref collisionPosition,ref velocity,width,height,ref hit,nativeClip))return false;
            }
            if(velocity!=original)hit=true;
            return true;
        }
        private bool ProjectileTileContact(Vector2 position,Vector2 velocity,int width,int height,bool fallThrough,out Vector2 result,bool nativeClip)
        {
            float x,y;bool up,down;PredictionStop stop;
            bool valid=TileContact(position.X,position.Y,velocity.X,velocity.Y,width,height,fallThrough,out x,out y,out up,out down,out stop,nativeClip:nativeClip,signedProjectile:width<1 || height<1);
            result=new Vector2(x,y);return valid;
        }
        private static bool SignedProjectileArea(float x,float y,float width,float height,bool query,out PredictionStop stop,bool nativeClip=false)
        {
            // IceSickle's native 34px body subtracts 36 in GetCollisionParams.
            // Validate the signed shape's enclosing terrain only. The contact
            // and slope equations must retain -2, not clamp it to a new body.
            // NPC/player inputs still require positive dimensions.
            stop=PredictionStop.InvalidState;
            if(!Finite(width) || !Finite(height) || width< -2 || height< -2 || width>1024 || height>1024)return false;
            x+=Math.Min(0,width);y+=Math.Min(0,height);width=Math.Max(1,Math.Abs(width));height=Math.Max(1,Math.Abs(height));
            return nativeClip?FreeBody(x,y,width,height,out stop):query?QueryArea(x,y,width,height,out stop):Area(x,y,width,height,out stop);
        }
        private bool ProjectileSlopePhase(ref Vector2 position,ref Vector2 collisionPosition,ref Vector2 velocity,int width,int height,ref bool hit,bool nativeClip)
        {
            var n=new NpcMotionState{X=collisionPosition.X,Y=collisionPosition.Y,Vx=velocity.X,Vy=velocity.Y,Width=width,Height=height};PredictionStop stop;
            if(!Slopes(ref n,true,out stop,nativeClip:nativeClip,signedProjectile:width<1 || height<1))return false;
            Vector2 nextPosition=new Vector2(n.X,n.Y),nextVelocity=new Vector2(n.Vx,n.Vy),offset=position-collisionPosition;
            if(nextPosition!=collisionPosition || nextVelocity!=velocity)hit=true;
            collisionPosition=nextPosition;position=collisionPosition+offset;velocity=nextVelocity;return true;
        }
    }
}
