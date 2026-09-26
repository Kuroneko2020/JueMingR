using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ObjectData;
using JueMingR.TerrariaHost.World;

namespace JueMingR.TerrariaHost.Processing
{
    internal struct ExtractionMachine
    {
        internal int X,Y,Type;
        internal static bool Find(Player p,Item material,out ExtractionMachine result)
        {
            result=default(ExtractionMachine);bool found=false;float best=float.MaxValue;
            var region=TileReachCheckSettings.Simple.GetTileRegion(p,material.tileBoost+p.blockRange);
            // An idle probe is bounded by actual reach; every admitted use reads
            // live parts again. No world scan, tile repair or cached authority.
            for(int y=System.Math.Max(0,region.Top);y<System.Math.Min(Main.maxTilesY,region.Bottom);y++)
            for(int x=System.Math.Max(0,region.Left);x<System.Math.Min(Main.maxTilesX,region.Right);x++)
            {
                var t=WorldTileObservation.ReadCurrent(x,y);
                if(!t.Readable || !t.Active || t.Inactive || t.Type!=219 && t.Type!=642)continue;
                float distance=Vector2.DistanceSquared(p.Center,new Vector2(x*16+8,y*16+8));
                if(distance>=best || !Valid(p,material,x,y))continue;
                result=new ExtractionMachine{X=x,Y=y,Type=t.Type};best=distance;found=true;
            }
            return found;
        }
        internal static bool Valid(Player p,Item item,int x,int y)
        {
            var t=WorldTileObservation.ReadCurrent(x,y);
            if(!t.Readable || !t.Active || t.Inactive || t.Type!=219 && t.Type!=642 || t.FrameX<0 || t.FrameY<0 ||
                !p.IsInTileInteractionRange(x,y,TileReachCheckSettings.Simple,item.tileBoost+p.blockRange))return false;
            var data=TileObjectData.GetTileData(Main.tile[x,y]);
            if(data==null || data.Width!=3 || data.Height!=3 || data.CoordinateFullWidth!=54 || data.CoordinateFullHeight!=54)return false;
            int dx=t.FrameX%54,dy=t.FrameY%54;
            if(dx%18!=0 || dy%18!=0)return false;
            int left=x-dx/18,top=y-dy/18;
            for(int yy=0;yy<3;yy++)for(int xx=0;xx<3;xx++)
            {
                var part=WorldTileObservation.ReadCurrent(left+xx,top+yy);
                if(!part.Readable || !part.Active || part.Inactive || part.Type!=t.Type || part.FrameX!=t.FrameX-dx+xx*18 || part.FrameY!=t.FrameY-dy+yy*18)return false;
            }
            return true;
        }
    }
}
