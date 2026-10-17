using System;
using JueMingR.Platform.Combat;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class PredictionTerrain
    {
        // Tongue owns movement before ordinary player acceleration. Native
        // still queries fluids/steps, then translates without Dry/WetCollision
        // or final slopes/belts. UpdateTouchingTiles overwrites collision
        // scratch afterwards; its final head flag must survive into the tail.
        private bool PlayerTongueMove(ref NpcMotionState n,ref PredictionPlayerMotion p,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            p.GfxOffset=Math.Max(-32,Math.Min(32,p.GfxOffset));
            if(!FreeBody(n.X,n.Y,n.Width,n.Height,out stop))return false;
            if(!Finite(n.Vx) || !Finite(n.Vy) || Math.Abs(n.Vx)>512 || Math.Abs(n.Vy)>512){stop=PredictionStop.InvalidState;return false;}
            bool lava,wet;byte liquid;
            if(!Wet(n,true,out lava,out liquid,out stop,true) || !Wet(n,false,out wet,out liquid,out stop,true))return false;
            n.Wet=wet;n.Honey=wet && (liquid==2 || n.Honey);n.Shimmer=wet && (liquid==3 || n.Shimmer);n.Lava=wet && (lava || n.Lava);
            if(!PlayerSteps(ref n,ref p,false,out stop))return false;
            n.OldX=n.X;n.OldY=n.Y;n.OldVx=n.Vx;n.OldVy=n.Vy;n.X+=n.Vx;n.Y+=n.Vy;
            bool head;int bounceY;
            if(!PlayerTouchingTiles(n,p.GravityDirection,out head,out bounceY,out stop))return false;
            n.PlayerDryHeadCollision=false;n.PlayerHeadCollision=head;
            if(!n.Wet && Math.Abs(n.Vy)>=5 && bounceY!=int.MinValue)
            {
                n.Vy*= -.8f;if(p.HoldJump)n.Vy=Math.Max(-13,Math.Min(13,n.Vy));
                n.Y=bounceY*16-(n.Vy<0?n.Height:-16);n.Vy=Math.Max(-20,Math.Min(20,n.Vy));
            }
            if(p.WallLocal && n.Vy>=3)
            {
                int x=(int)((n.X+n.Width*.5f)/16),y=(int)((n.Y+n.Height+.01f)/16);Cell c;
                if(!CellAt(x,y,out c,out stop))return false;
                // Wiring is a real world operation. Stop before it and let
                // the next natural observation resume on the changed world.
                if(c.RawActive && c.DetonatorArmed){stop=PredictionStop.PhaseBoundary;return false;}
            }
            return true;
        }
        internal bool PlayerTouchingTiles(NpcMotionState n,float gravity,out bool head,out int bounceY,out PredictionStop stop)
        {
            head=false;bounceY=int.MinValue;stop=PredictionStop.None;bool clear;int grav=(int)gravity,direction=n.Vy>0?1:-1;
            if(!PlayerClearSpot(n.X+n.Vx,n.Y+n.Vy,16,n.Width,n.Height,grav,ref head,out clear,out stop))return false;
            if(!clear && !PlayerFindVerticalTiles(n.X+n.Vx,n.Y+n.Vy,16,n.Width,n.Height,direction,grav,false,ref head,ref bounceY,out stop))return false;
            if(!PlayerClearSpot(n.X,n.Y,Math.Abs(n.Vy),n.Width,n.Height,grav,ref head,out clear,out stop))return false;
            if(!clear && !PlayerFindVerticalTiles(n.X,n.Y,Math.Abs(n.Vy),n.Width,n.Height,direction,grav,true,ref head,ref bounceY,out stop))return false;
            return true;
        }
        private bool PlayerClearSpot(float x,float y,float amount,int width,int height,int grav,ref bool head,out bool clear,out PredictionStop stop)
        {
            clear=false;stop=PredictionStop.None;
            for(int phase=0;phase<2;phase++)for(int direction=0;direction<4;direction++)
            {
                float vx=direction==0?amount:direction==1?-amount:0,vy=direction==2?amount:direction==3?-amount:0;
                if(phase==0)
                {
                    float rx,ry;bool up,down;
                    if(!TileContact(x-vx,y-vy,vx,vy,width,height,false,out rx,out ry,out up,out down,out stop,false,grav,nativeClip:true))return false;
                    head=grav>0?up:down;if(rx!=vx || ry!=vy)return true;
                }
                else
                {
                    var query=new NpcMotionState{X=x,Y=y,Vx=vx,Vy=vy,Width=width,Height=height};
                    if(!Slopes(ref query,false,out stop,true,grav,true,rawPlayerProbe:true))return false;
                    head=query.PlayerHeadCollision;if(query.X!=x || query.Y!=y || query.Vx!=vx || query.Vy!=vy)return true;
                }
            }
            clear=true;return true;
        }
        private bool PlayerFindVerticalTiles(float x,float y,float amount,int width,int height,int direction,int grav,bool slopes,ref bool head,ref int bounceY,out PredictionStop stop)
        {
            stop=PredictionStop.None;float first=Math.Min(16-x%16,width),offset=first,vy=direction*amount;int row=(int)(y+(direction>0?height:0))/16;
            if(!PlayerTouchSlice(x,y,vy,(int)first,height,row,grav,slopes,ref head,ref bounceY,out stop))return false;
            for(;offset+16<=width-16;offset+=16)
                if(!PlayerTouchSlice(x+offset,y,vy,16,height,row,grav,slopes,ref head,ref bounceY,out stop))return false;
            return PlayerTouchSlice(x+offset,y,vy,width-(int)offset,height,row,grav,slopes,ref head,ref bounceY,out stop);
        }
        private bool PlayerTouchSlice(float x,float y,float vy,int width,int height,int row,int grav,bool slopes,ref bool head,ref int bounceY,out PredictionStop stop)
        {
            float rx,ry;bool up,down;
            // FindCollisionTile deliberately queries its final zero-width
            // slice too. Preserve that native query and its scratch writes.
            if(!TileContact(x,y-vy,0,vy,width,height,false,out rx,out ry,out up,out down,out stop,false,grav,nativeClip:true,signedProjectile:width<1))return false;
            head=grav>0?up:down;bool touched=rx!=0 || ry!=vy;
            if(!touched && slopes)
            {
                var query=new NpcMotionState{X=x,Y=y,Vx=0,Vy=vy,Width=width,Height=height};
                if(!Slopes(ref query,false,out stop,true,grav,true,width<1,true))return false;
                head=query.PlayerHeadCollision;touched=query.Y!=y || query.Vx!=0 || query.Vy!=vy;
            }
            // Native merges ordered unique rows. For its first bouncy tile,
            // duplicate rows cannot change the first match; no list or second
            // collision pass is needed in every projected player action.
            if(touched && bounceY==int.MinValue)
            {Cell c;if(!CellAt((int)x/16,row,out c,out stop))return false;if(c.Active && c.Bouncy)bounceY=row;}
            return true;
        }
    }
}
