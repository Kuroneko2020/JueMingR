using System;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class PredictionTerrain
    {
        private bool Conveyor(ref NpcMotionState n,bool player,bool onTrack,int grav,out PredictionStop stop)
        {
            stop=PredictionStop.None;var box=n.Bounds;if(player){box.Y+=5;box.Height-=5;}
            int left=(int)box.X,top=(int)box.Y,right=(int)(box.X+box.Width),bottom=(int)(box.Y+box.Height);
            int playerBottom=(int)box.Y+(int)box.Height;
            if(left%16==0)left--;if(top%16==0)top--;if(right%16==0)right++;if(bottom%16==0)bottom++;
            left/=16;right/=16;top/=16;bottom/=16;
            int sx=0,sy=0;bool contact=false;
            for(int x=left;x<=right;x++)for(int edge=0;edge<2;edge++)
            {
                int y=edge==0?top:bottom;
                if(x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY || player && onTrack && y<playerBottom)continue;
                Cell tile;if(!CellAt(x,y,out tile,out stop))return false;if(!tile.Active || tile.Conveyor==0)continue;
                var lowA=new Vector2(x*16,y*16+16);var lowB=lowA+new Vector2(16,0);
                var highA=new Vector2(x*16,y*16);var highB=highA+new Vector2(16,0);
                switch(tile.Slope)
                {
                    case 1:highB.Y+=16;break;
                    case 2:highA.Y+=16;break;
                    case 3:lowB.Y-=16;break;
                    case 4:lowA.Y-=16;break;
                    default:if(tile.Half){highA.Y+=8;highB.Y+=8;}break;
                }
                // This native helper is scalar geometry only: it reads no Main,
                // entity or collision scratch. All tile/query ownership stays
                // in this bounded snapshot, including both edge rows.
                var pos=new Vector2(box.X-.0001f,box.Y-.0001f);var size=new Vector2(box.Width+.0002f,box.Height+.0002f);
                int side=0;if(!tile.StairPlatform && Collision.CheckAABBvLineCollision2(pos,size,lowA,lowB))side--;
                if(Collision.CheckAABBvLineCollision2(pos,size,highA,highB))side++;
                if(side==0)continue;contact=true;sx+=tile.Conveyor*side*grav;
                if(tile.Slope==2 || tile.Slope==4)sy+=grav*-tile.Conveyor;
                if(tile.Slope==1 || tile.Slope==3)sy-=grav*-tile.Conveyor;
            }
            if(!contact || sx==0)return true;
            var delta=Vector2.Normalize(new Vector2(Math.Sign(sx)*grav,Math.Sign(sy)))*2.5f;
            float dx,dy;bool up,down;
            if(!TileContact(n.X,n.Y,delta.X,delta.Y,n.Width,n.Height,false,out dx,out dy,out up,out down,out stop,false,grav))return false;
            n.X+=dx;n.Y+=dy;
            if(!TileContact(n.X,n.Y,0,2.5f*grav,n.Width,n.Height,false,out dx,out dy,out up,out down,out stop,false,grav))return false;
            n.X+=dx;n.Y+=dy;
            if(player)n.PlayerHeadCollision=grav>0?up:down;
            return true;
        }
    }
}
