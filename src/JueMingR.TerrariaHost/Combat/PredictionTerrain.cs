using System;
using System.Collections.Generic;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class PredictionTerrain : IPredictionTerrain
    {
        private struct Cell : IEquatable<Cell>
        {
            internal bool Solid,Platform,PlatformFrame,Half;internal byte Slope,Liquid,Kind;
            public bool Equals(Cell b){return Solid==b.Solid && Platform==b.Platform && PlatformFrame==b.PlatformFrame && Half==b.Half && Slope==b.Slope && Liquid==b.Liquid && Kind==b.Kind;}
        }
        private readonly Dictionary<int,Cell> cells=new Dictionary<int,Cell>(512);
        private int width;
#if DEBUG
        internal int Reads {get;private set;}
#endif
        public void Reset(){cells.Clear();width=Main.maxTilesX;}
        public bool Unchanged
        {
            get{if(width!=Main.maxTilesX)return false;foreach(var pair in cells){Cell current;if(!Read(pair.Key%width,pair.Key/width,out current) || !current.Equals(pair.Value))return false;}return true;}
        }
        private static bool Read(int x,int y,out Cell value)
        {
            value=default(Cell);if(Main.tile==null || x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY)return false;
            var tile=Main.tile[x,y];if(tile==null)return false;
            bool active=tile.active() && !tile.inActive();
            value=new Cell{Solid=active && Main.tileSolid[tile.type],Platform=active && Main.tileSolidTop[tile.type],PlatformFrame=tile.frameY==0,Half=tile.halfBrick(),Slope=tile.slope(),Liquid=tile.liquid,Kind=(byte)tile.liquidType()};return true;
        }
        private bool CellAt(int x,int y,out Cell cell,out PredictionStop stop)
        {
            stop=PredictionStop.None;cell=default(Cell);
            if(x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY){stop=PredictionStop.TerrainUnavailable;return false;}
            int key=y*Main.maxTilesX+x;if(cells.TryGetValue(key,out cell))return true;
            if(cells.Count>=4096){stop=PredictionStop.TerrainLimit;return false;}
            if(!Read(x,y,out cell)){stop=PredictionStop.TerrainUnavailable;return false;}
            cells.Add(key,cell);
#if DEBUG
            Reads++;
#endif
            return true;
        }
        public bool Solid(MotionRect box,out bool solid,out PredictionStop stop)
        {
            solid=false;stop=PredictionStop.None;
            if(box.Width<1 || box.Height<1 || box.Width>1024 || box.Height>1024){stop=PredictionStop.InvalidState;return false;}
            for(int y=(int)Math.Floor(box.Y/16);y<=(int)Math.Floor((box.Y+box.Height-.001f)/16);y++)
            for(int x=(int)Math.Floor(box.X/16);x<=(int)Math.Floor((box.X+box.Width-.001f)/16);x++)
            {
                Cell c;if(!CellAt(x,y,out c,out stop))return false;
                // SolidCollision treats slopes as solid and never queries
                // liquid; callers such as Sharkron use precisely that meaning.
                if(!c.Solid || c.Platform)continue;
                if(!c.Half || box.Y+box.Height>y*16+8)solid=true;
            }
            return true;
        }
        public bool Move(ref NpcMotionState n,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            // Native UpdateNPC bypasses UpdateCollision altogether here. In
            // particular Duke/Sharkron do not receive liquid movement drag.
            if(n.NoTileCollide){n.X+=n.Vx;n.Y+=n.Vy;return true;}
            if(n.Width<1 || n.Height<1 || n.Width>1024 || n.Height>1024 || Math.Abs(n.Vx)>512 || Math.Abs(n.Vy)>512){stop=PredictionStop.InvalidState;return false;}
            // Preflight the native current/final collision neighbourhoods.
            // Slopes, hoiks and special liquids terminate before changing a
            // local step; unknown cells are not silently converted into air.
            int left=Math.Max(0,(int)(Math.Min(n.X,n.X+n.Vx)/16)-1),right=Math.Min(Main.maxTilesX-1,(int)((Math.Max(n.X,n.X+n.Vx)+n.Width)/16)+2);
            int top=Math.Max(0,(int)(Math.Min(n.Y,n.Y+n.Vy)/16)-1),bottom=Math.Min(Main.maxTilesY-40,(int)((Math.Max(n.Y,n.Y+n.Vy)+n.Height+4)/16)+3);
            for(int x=left;x<right;x++)for(int y=top;y<bottom;y++)
            {
                Cell c;if(!CellAt(x,y,out c,out stop))return false;
                if(c.Slope!=0){stop=PredictionStop.Slope;return false;}
                if(c.Liquid>0 && (c.Kind==1 || c.Kind==3)){stop=PredictionStop.LiquidEffect;return false;}
            }
            bool wet=false,honey=false;int sensorWidth=Math.Min(10,n.Width),sensorHeight=n.Height/2;
            float sensorX=n.X+n.Width/2-sensorWidth/2,sensorY=n.Y+n.Height/2-sensorHeight/2;
            for(int x=left;x<right && !wet;x++)for(int y=top;y<bottom;y++)
            {
                Cell c;if(!CellAt(x,y,out c,out stop))return false;if(c.Liquid==0)continue;
                float gap=(256-c.Liquid)/16f,liquidTop=y*16+gap;int liquidHeight=16-(int)gap;
                if(sensorX+sensorWidth>x*16 && sensorX<x*16+16 && sensorY+sensorHeight>liquidTop && sensorY<liquidTop+liquidHeight){wet=true;honey=c.Kind==2;break;}
            }
            var next=n;if(next.Wet && !wet)next.Vx*=.5f;
            next.Wet=wet;next.Honey=wet && (honey || next.Honey);next.OldVx=next.Vx;next.OldVy=next.Vy;
            float vx=next.Vx,vy=next.Vy,px=next.X+vx,py=next.Y+vy,rx=vx,ry=vy,nearest=(bottom+3)*16;
            int sideX=-1,sideY=-1,verticalX=-1,verticalY=-1;bool up=false,fall=next.Style==2 || next.Style==5 || next.Style==14;
            // Ordinary native rectangular contacts: projected full velocity,
            // x-major tile order and contact tie rules. No extra swept substeps.
            int x0=Math.Max(0,(int)(next.X/16)-1),x1=Math.Min(Main.maxTilesX-1,(int)((next.X+next.Width)/16)+2);
            int y0=Math.Max(0,(int)(next.Y/16)-1),y1=Math.Min(Main.maxTilesY-40,(int)((next.Y+next.Height)/16)+2);
            for(int x=x0;x<x1;x++)for(int y=y0;y<y1;y++)
            {
                Cell c;if(!CellAt(x,y,out c,out stop))return false;if(!c.Solid && !(c.Platform && c.PlatformFrame))continue;
                float tx=x*16,ty=y*16+(c.Half?8:0),height=c.Half?8:16;
                if(px+next.Width<=tx || px>=tx+16 || py+next.Height<=ty || py>=ty+height)continue;
                if(next.Y+next.Height<=ty)
                {if(!(c.Platform && fall) && nearest>ty){verticalX=x;verticalY=y+(c.Half?1:0);if(verticalX!=sideX){ry=ty-(next.Y+next.Height);nearest=ty;}}}
                else if(next.X+next.Width<=tx && !c.Platform)
                {sideX=x;sideY=y;if(sideY!=verticalY)rx=tx-(next.X+next.Width);if(verticalX==sideX)ry=vy;}
                else if(next.X>=tx+16 && !c.Platform)
                {sideX=x;sideY=y;if(sideY!=verticalY)rx=tx+16-next.X;if(verticalX==sideX)ry=vy;}
                else if(next.Y>=ty+height && !c.Platform)
                {up=true;verticalX=x;verticalY=y;ry=ty+height-next.Y+.01f;if(verticalY==sideY)rx=vx;}
            }
            if(up)ry=.01f;next.Vx=rx;next.Vy=ry;next.CollideX=rx!=vx;next.CollideY=ry!=vy;
            float slowdown=wet?(next.Honey?next.HoneySpeed:next.WaterSpeed):1;
            next.X+=next.CollideX?rx:rx*slowdown;next.Y+=next.CollideY?ry:ry*slowdown;n=next;
            return true;
        }
    }
}
