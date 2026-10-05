using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed class PredictionTerrain : IPredictionTerrain,IPredictionResizeTerrain,IPredictionWaterSurfaceTerrain
    {
        private struct Cell : IEquatable<Cell>
        {
            internal bool Active,RawActive,RawSolid,Solid,Platform,StairPlatform,PlatformFrame,ProperPlatformFrame,Half;internal byte Slope,Liquid,Kind;internal ushort Type,Wall;
            public bool Equals(Cell b){return Active==b.Active && RawActive==b.RawActive && RawSolid==b.RawSolid && Wall==b.Wall && Type==b.Type && ProperPlatformFrame==b.ProperPlatformFrame && Solid==b.Solid && Platform==b.Platform && StairPlatform==b.StairPlatform && PlatformFrame==b.PlatformFrame && Half==b.Half && Slope==b.Slope && Liquid==b.Liquid && Kind==b.Kind;}
        }
        private readonly Dictionary<int,Cell> cells=new Dictionary<int,Cell>(512);
        // Small direct hot cache for repeated liquid/contact/slope queries.
        // The dictionary remains the authoritative distinct-cell/bounds owner.
        // Epoch changes on every real sample, so no old tile becomes current.
        private readonly int[] hotKeys=new int[256];
        private readonly long[] hotEpochs=new long[256];
        private readonly Cell[] hotCells=new Cell[256];
        private long epoch=1;
        private int width,height;
#if DEBUG
        internal int Reads {get;private set;}
#endif
        public void Reset(){cells.Clear();width=Main.maxTilesX;height=Main.maxTilesY;if(epoch==long.MaxValue){Array.Clear(hotEpochs,0,hotEpochs.Length);epoch=0;}epoch++;}
        public bool Unchanged
        {
            get{if(width!=Main.maxTilesX || height!=Main.maxTilesY)return false;foreach(var pair in cells){Cell current;if(!Read(pair.Key%width,pair.Key/width,out current) || !current.Equals(pair.Value))return false;}return true;}
        }
        private static bool Read(int x,int y,out Cell value)
        {
            value=default(Cell);if(Main.tile==null || x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY)return false;
            var tile=Main.tile[x,y];if(tile==null)return false;
            bool active=tile.active() && !tile.inActive();
            int frame=tile.frameX/18;
            value=new Cell{Active=active,RawActive=tile.active(),RawSolid=Main.tileSolid[tile.type],Wall=tile.wall,Type=tile.type,ProperPlatformFrame=frame>=0 && frame<=7 || frame>=12 && frame<=16 || frame>=25 && frame<=26,Solid=active && Main.tileSolid[tile.type],Platform=active && Main.tileSolidTop[tile.type],StairPlatform=TileID.Sets.Platforms[tile.type],PlatformFrame=tile.frameY==0,Half=tile.halfBrick(),Slope=tile.slope(),Liquid=tile.liquid,Kind=(byte)tile.liquidType()};return true;
        }
        public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop)
        {
            Cell c;tile=default(PredictionTile);if(!CellAt(x,y,out c,out stop))return false;
            tile=new PredictionTile{Active=c.Active,RawActive=c.RawActive,RawSolid=c.RawSolid,Wall=c.Wall,Solid=c.Solid,SolidTop=c.Platform,Platform=c.StairPlatform,Half=c.Half,Slope=c.Slope,Liquid=c.Liquid,Type=c.Type,ProperPlatformFrame=c.ProperPlatformFrame,SurfacePlatform=c.PlatformFrame};return true;
        }
        private bool CellAt(int x,int y,out Cell cell,out PredictionStop stop)
        {
            stop=PredictionStop.None;cell=default(Cell);
            if(x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY){stop=PredictionStop.TerrainUnavailable;return false;}
            int key=y*Main.maxTilesX+x,hot=(key^(key>>7)^(key>>16))&255;
            if(hotEpochs[hot]==epoch && hotKeys[hot]==key){cell=hotCells[hot];return true;}
            if(cells.TryGetValue(key,out cell)){Hot(hot,key,cell);return true;}
            if(cells.Count>=4096){stop=PredictionStop.TerrainLimit;return false;}
            if(!Read(x,y,out cell)){stop=PredictionStop.TerrainUnavailable;return false;}
            cells.Add(key,cell);
            Hot(hot,key,cell);
#if DEBUG
            Reads++;
#endif
            return true;
        }
        private void Hot(int index,int key,Cell cell){hotKeys[index]=key;hotCells[index]=cell;hotEpochs[index]=epoch;}
        public bool Resize(MotionRect box,int width,int height,out MotionRect adjusted,out bool canResize,out PredictionStop stop)
        {
            adjusted=new MotionRect((int)box.X,(int)box.Y,(int)box.Width,(int)box.Height);canResize=false;stop=PredictionStop.None;
            // Locked spider growth is at most one tile per axis. Keep the
            // original bottom-first height and symmetric-then-left width fit.
            int dy=height-(int)box.Height,dx=width-(int)box.Width;
            if(dy>16 || dx>16 || width<1 || height<1){stop=PredictionStop.InvalidState;return false;}
            if(dy>0)
            {
                int down,up;if(!ResizeDistance(adjusted,0,1,dy,out down,out stop) || !ResizeDistance(adjusted,0,-1,dy,out up,out stop))return false;
                if(up+down<dy)return true;
                int rise=Math.Min(dy,up);adjusted.Y-=rise;adjusted.Height=height;
            }
            else{adjusted.Y-=dy;adjusted.Height=height;}
            if(dx>0)
            {
                int right,left;if(!ResizeDistance(adjusted,1,0,dx,out right,out stop) || !ResizeDistance(adjusted,-1,0,dx,out left,out stop))return false;
                if(left+right<dx)return true;
                int a=Math.Min(dx/2,Math.Min(left,right)),b=a;a+=Math.Min(dx-a-b,left-a);b+=Math.Min(dx-a-b,right-b);
                adjusted.X-=a;adjusted.Width=width;
            }
            else{adjusted.X+=dx/2;adjusted.Width=width;}
            canResize=true;return true;
        }
        private bool ResizeDistance(MotionRect box,int dx,int dy,int amount,out int distance,out PredictionStop stop)
        {
            distance=0;stop=PredictionStop.None;
            for(int at=0;at<=1;at++)
            {
                float x=box.X+1+dx*amount*at,y=box.Y+dy*amount*at;
                var slope=new NpcMotionState{X=x,Y=y,Vx=dx*amount,Vy=dy*amount,Width=(int)box.Width-1,Height=(int)box.Height};
                if(!Slopes(ref slope,false,out stop))return false;
                if(slope.X!=x || slope.Y!=y || slope.Vx!=dx*amount || slope.Vy!=dy*amount)
                {distance=(int)(at*amount-Math.Sqrt((slope.X-x)*(slope.X-x)+(slope.Y-y)*(slope.Y-y)));return true;}
            }
            float rx,ry;bool up;if(!TileContact(box.X,box.Y,dx*amount,dy*amount,(int)box.Width,(int)box.Height,false,out rx,out ry,out up,out stop))return false;
            distance=(int)Math.Sqrt(rx*rx+ry*ry);return true;
        }
        private static bool Area(float x,float y,float w,float h,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(!Finite(x) || !Finite(y) || !Finite(w) || !Finite(h) || w<1 || h<1 || w>1024 || h>1024){stop=PredictionStop.InvalidState;return false;}
            if(x<0 || y<0 || x+w>Main.maxTilesX*16f || y+h>Main.maxTilesY*16f){stop=PredictionStop.TerrainUnavailable;return false;}return true;
        }
        private static bool QueryArea(float x,float y,float w,float h,out PredictionStop stop)
        {
            // Native scans clip the border and exclude forty bottom tiles.
            // Reject the unknown portion before clipping or an empty loop can
            // incorrectly turn it into air, including free-movement liquids.
            if(!Area(x,y,w,h,out stop))return false;
            if((int)(x/16)-1<0 || (int)(y/16)-1<0 || (int)((x+w)/16)+2>Main.maxTilesX-1 || (int)((y+h)/16)+2>Main.maxTilesY-40)
            {stop=PredictionStop.TerrainUnavailable;return false;}return true;
        }
        private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
        public bool Solid(MotionRect box,out bool solid,out PredictionStop stop)
        {
            solid=false;stop=PredictionStop.None;
            if(!Area(box.X,box.Y,box.Width,box.Height,out stop))return false;
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
        public bool CanHit(MotionRect source,MotionRect target,out bool clear,out PredictionStop stop)
        {
            clear=false;stop=PredictionStop.None;
            if(!Area(source.X,source.Y,source.Width,source.Height,out stop) || !Area(target.X,target.Y,target.Width,target.Height,out stop))return false;
            int x=Math.Max(1,Math.Min(Main.maxTilesX-1,((int)source.X+(int)source.Width/2)/16)),y=Math.Max(1,Math.Min(Main.maxTilesY-40,((int)source.Y+(int)source.Height/2)/16));
            int tx=Math.Max(1,Math.Min(Main.maxTilesX-1,((int)target.X+(int)target.Width/2)/16)),ty=Math.Max(1,Math.Min(Main.maxTilesY-40,((int)target.Y+(int)target.Height/2)/16));
            // Vanilla CanHit follows the larger remaining axis, checking the
            // two side neighbours as well as the entered cell. CanHitLine is
            // a different predicate. Every future query joins this snapshot's
            // dependency set instead of reusing the initial visibility bit.
            while(x!=tx || y!=ty)
            {
                Cell a,b,center;
                if(Math.Abs(x-tx)>Math.Abs(y-ty))
                {x+=x<tx?1:-1;if(!CellAt(x,y-1,out a,out stop) || !CellAt(x,y+1,out b,out stop))return false;}
                else
                {y+=y<ty?1:-1;if(x+1>=Main.maxTilesX)return true;if(!CellAt(x-1,y,out a,out stop) || !CellAt(x+1,y,out b,out stop))return false;}
                if(a.Solid && !a.Platform && !a.Half && a.Slope==0 && b.Solid && !b.Platform && !b.Half && b.Slope==0)return true;
                if(!CellAt(x,y,out center,out stop))return false;if(center.Solid && !center.Platform)return true;
            }
            clear=true;return true;
        }
        public bool Move(ref NpcMotionState n,PredictionEnvironment environment,out PredictionStop stop)
        {return Move(ref n,environment,false,false,false,out stop);}
        public bool MoveWaterWalkingPlayer(ref NpcMotionState n,PredictionEnvironment environment,bool fallThrough,bool lavaWalk,out PredictionStop stop)
        {return Move(ref n,environment,true,fallThrough,lavaWalk,out stop);}
        private bool Move(ref NpcMotionState n,PredictionEnvironment environment,bool waterWalk,bool fallThrough,bool lavaWalk,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            if(!Area(n.X,n.Y,n.Width,n.Height,out stop) || !Area(n.X+n.Vx,n.Y+n.Vy,n.Width,n.Height,out stop))return false;
            // Native UpdateNPC bypasses UpdateCollision altogether here. In
            // particular Duke/Sharkron do not receive liquid movement drag.
            if(n.NoTileCollide)
            {
                var free=n;free.OldX=free.X;free.OldY=free.Y;free.X+=free.Vx;free.Y+=free.Vy;
                bool wetContact;byte kind;if(!Wet(free,false,out wetContact,out kind,out stop))return false;
                if(wetContact && !environment.Multiplayer)NpcHealth.Extinguish(ref free.Health);
                n=free;return true;
            }
            if(n.Width<1 || n.Height<1 || n.Width>1024 || n.Height>1024 || Math.Abs(n.Vx)>512 || Math.Abs(n.Vy)>512){stop=PredictionStop.InvalidState;return false;}
            var next=n;if(!WalkDown(ref next,out stop))return false;
            bool lava,wet;byte liquid;
            if(!Wet(next,true,out lava,out liquid,out stop) || !Wet(next,false,out wet,out liquid,out stop))return false;
            if(next.Identity.Type==441)lava=false;
            if(lava)
            {
                next.Lava=true;
                if(!next.Health.LavaImmune && !next.Health.DontTakeDamage && !environment.Multiplayer && next.Health.Immune255==0)
                {
                    bool onlyFire=environment.Remix && !next.Friendly;
                    // Strike has additional ownership/AI side effects in these
                    // families. Never treat their live lava strike as plain
                    // subtraction or continue through an unmodeled new phase.
                    if(!onlyFire && (next.Health.RealLife>=0 || next.Style==8 || next.Style==87 || next.Style==97 || next.Identity.Type==184 || next.Identity.Type==185 || next.Identity.Type==535))
                    {stop=PredictionStop.LiquidEffect;return false;}
                    next.Health.Immune255=30;if(!next.Health.FireImmune)NpcHealth.ApplyFire(ref next.Health,onlyFire?180:420);
                    if(!onlyFire && !next.Health.Immortal)
                    {
                        int damage=(int)Math.Max(1,50-next.Health.Defense*.5);if(next.Health.DamageMultiplier>1)damage=(int)(damage*next.Health.DamageMultiplier);
                        next.Life-=Math.Max(1,damage);next.JustHit=true;
                        if(next.Life<=0){stop=PredictionStop.Despawn;return false;}
                    }
                }
            }
            if(next.Wet && !wet)next.Vx*=.5f;
            next.Wet=wet;next.Honey=wet && (liquid==2 || next.Honey);next.Shimmer=wet && (liquid==3 || next.Shimmer);next.Lava=wet && next.Lava;
            if(wet && !environment.Multiplayer)
            {if(!lava)NpcHealth.Extinguish(ref next.Health);if(next.Shimmer && !next.Health.ShimmerImmune && next.Health.ShimmerTicks<=10)next.Health.ShimmerTicks=100;}
            next.OldVx=next.Vx;next.OldVy=next.Vy;
            bool fall=waterWalk?fallThrough:next.Style==2 || next.Style==5 || next.Style==14 || next.Style==3 && next.DirectionY==1;
            float rx,ry;bool up;
            if(!TileContact(next.X,next.Y,next.Vx,next.Vy,next.Width,next.Height,fall,out rx,out ry,out up,out stop))return false;
            if(waterWalk && !WaterSurface(next.Bounds,rx,ref ry,fallThrough,lavaWalk,out stop))return false;
            if(up)ry=.01f;next.CollideX=rx!=next.Vx;next.CollideY=ry!=next.Vy;next.Vx=rx;next.Vy=ry;
            float slowdown=wet?(next.Shimmer?next.ShimmerSpeed:next.Honey?next.HoneySpeed:next.Lava?next.LavaSpeed:next.WaterSpeed):1;
            next.OldX=next.X;next.OldY=next.Y;next.X+=next.CollideX?rx:rx*slowdown;next.Y+=next.CollideY?ry:ry*slowdown;
            if(fall)next.StairFall=true;
            if(!Slopes(ref next,fall,out stop))return false;
            n=next;return true;
        }
        private bool WaterSurface(MotionRect box,float vx,ref float vy,bool fallThrough,bool lavaWalk,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(fallThrough)return true;
            if(!QueryArea(box.X,box.Y,box.Width,box.Height,out stop))return false;
            // Locked WaterCollision uses the old bottom and discrete liquid
            // height; no support while submerged or moving up through surface.
            float x=box.X+vx,y=box.Y+vy;
            for(int tx=(int)(box.X/16)-1;tx<(int)((box.X+box.Width)/16)+2;tx++)
            for(int ty=(int)(box.Y/16)-1;ty<(int)((box.Y+box.Height)/16)+2;ty++)
            {
                Cell c,above;if(!CellAt(tx,ty,out c,out stop))return false;
                if(c.Liquid==0 || c.Kind==1 && !lavaWalk)continue;
                if(!CellAt(tx,ty-1,out above,out stop))return false;
                if(above.Liquid!=0)continue;
                int height=c.Liquid/32*2+2;float surface=ty*16+16-height;
                if(x+box.Width>tx*16 && x<tx*16+16 && y+box.Height>surface && y<surface+height && box.Y+box.Height<=surface)vy=surface-(box.Y+box.Height);
            }
            return true;
        }
        private bool Wet(NpcMotionState n,bool lavaOnly,out bool wet,out byte kind,out PredictionStop stop)
        {
            wet=false;kind=0;stop=PredictionStop.None;
            if(!QueryArea(n.X,n.Y,n.Width,n.Height,out stop))return false;
            int sensorWidth=lavaOnly?n.Width:Math.Min(10,n.Width),sensorHeight=lavaOnly?n.Height:n.Height/2;
            float sensorX=lavaOnly?n.X:n.X+n.Width/2-sensorWidth/2,sensorY=lavaOnly?n.Y:n.Y+n.Height/2-sensorHeight/2;
            int left=Math.Max(0,(int)(n.X/16)-1),right=Math.Min(Main.maxTilesX-1,(int)((n.X+n.Width)/16)+2);
            int top=Math.Max(0,(int)(n.Y/16)-1),bottom=Math.Min(Main.maxTilesY-40,(int)((n.Y+n.Height)/16)+2);
            for(int x=left;x<right;x++)for(int y=top;y<bottom;y++)
            {
                Cell c;if(!CellAt(x,y,out c,out stop))return false;float surface=y*16;int liquidHeight=16;byte liquidKind=c.Kind;
                if(c.Liquid>0)
                {if(lavaOnly && c.Kind!=1)continue;float gap=(256-c.Liquid)/16f;surface+=gap;liquidHeight-= (int)gap;}
                else
                {
                    if(lavaOnly || !c.RawActive || c.Slope==0 || y==0)continue;
                    Cell above;if(!CellAt(x,y-1,out above,out stop))return false;if(above.Liquid==0)continue;liquidKind=above.Kind;
                }
                if(sensorX+sensorWidth>x*16 && sensorX<x*16+16 && sensorY+sensorHeight>surface && sensorY<surface+liquidHeight)
                {wet=true;kind=liquidKind;return true;}
            }
            return true;
        }
        private bool TileContact(float px0,float py0,float vx,float vy,int w,int h,bool fall,out float rx,out float ry,out bool up,out PredictionStop stop)
        {
            rx=vx;ry=vy;up=false;stop=PredictionStop.None;
            if(!QueryArea(px0,py0,w,h,out stop) || !Area(px0+vx,py0+vy,w,h,out stop))return false;
            float px=px0+vx,py=py0+vy;
            int sideX=-1,sideY=-1,verticalX=-1,verticalY=-1;
            // Ordinary native rectangular contacts: projected full velocity,
            // x-major tile order and contact tie rules. No extra swept substeps.
            int x0=Math.Max(0,(int)(px0/16)-1),x1=Math.Min(Main.maxTilesX-1,(int)((px0+w)/16)+2);
            int y0=Math.Max(0,(int)(py0/16)-1),y1=Math.Min(Main.maxTilesY-40,(int)((py0+h)/16)+2);float nearest=(y1+3)*16;
            for(int x=x0;x<x1;x++)for(int y=y0;y<y1;y++)
            {
                Cell c;if(!CellAt(x,y,out c,out stop))return false;if(!c.Solid && !(c.Platform && c.PlatformFrame))continue;
                float tx=x*16,ty=y*16+(c.Half?8:0),height=c.Half?8:16;
                if(px+w<=tx || px>=tx+16 || py+h<=ty || py>=ty+height)continue;
                bool topSlope=c.Slope==1 || c.Slope==2;
                if(c.Slope==3 && py0+Math.Abs(vx)>=ty && px0>=tx || c.Slope==4 && py0+Math.Abs(vx)>=ty && px0+w<=tx+16 ||
                    c.Slope==1 && py0+h-Math.Abs(vx)<=ty+height && px0>=tx || c.Slope==2 && py0+h-Math.Abs(vx)<=ty+height && px0+w<=tx+16)continue;
                if(py0+h<=ty)
                {if(!(c.Platform && fall) && nearest>ty){verticalX=x;verticalY=y+(c.Half?1:0);if(verticalX!=sideX && !topSlope){ry=ty-(py0+h);nearest=ty;}}}
                else if(px0+w<=tx && !c.Platform)
                {Cell neighbor;if(x>0){if(!CellAt(x-1,y,out neighbor,out stop))return false;if(neighbor.Slope==2 || neighbor.Slope==4)continue;}sideX=x;sideY=y;if(sideY!=verticalY)rx=tx-(px0+w);if(verticalX==sideX)ry=vy;}
                else if(px0>=tx+16 && !c.Platform)
                {Cell neighbor;if(!CellAt(x+1,y,out neighbor,out stop))return false;if(neighbor.Slope==1 || neighbor.Slope==3)continue;sideX=x;sideY=y;if(sideY!=verticalY)rx=tx+16-px0;if(verticalX==sideX)ry=vy;}
                else if(py0>=ty+height && !c.Platform)
                {up=true;verticalX=x;verticalY=y;ry=ty+height-py0+.01f;if(verticalY==sideY)rx=vx;}
            }
            return true;
        }
        private bool WalkDown(ref NpcMotionState n,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(n.Vy!=n.Gravity)return true;
            if(!QueryArea(n.X,n.Y,n.Width,n.Height+20,out stop))return false;
            int row=Math.Max(0,Math.Min(Main.maxTilesY-42,(int)((n.Y+n.Height+4)/16))),chosenX=-1,chosenY=-1;byte chosenSlope=0;float nearest=(row+3)*16;
            for(int x=Math.Max(0,(int)(n.X/16));x<=Math.Min(Main.maxTilesX-1,(int)((n.X+n.Width)/16));x++)for(int y=row;y<=row+1;y++)
            {
                Cell c;if(!CellAt(x,y,out c,out stop))return false;if(!c.Solid && !c.Platform)continue;
                float surface=y*16+(c.Half?8:0);
                if((int)n.X+n.Width<=x*16 || (int)n.X>=x*16+16 || (int)n.Y+n.Height<=y*16-17 || (int)n.Y>=y*16-1 || surface>nearest)continue;
                if(surface==nearest && (c.Slope==0 || chosenX!=-1 && chosenSlope!=0 && c.Slope!=(n.Vx<0?2:1)))continue;
                chosenX=x;chosenY=y;chosenSlope=c.Slope;nearest=surface;
            }
            if(chosenSlope==1 && n.Vx>0 && n.Y+n.Height>=chosenY*16+n.X-chosenX*16 || chosenSlope==2 && n.Vx<0 && n.Y+n.Height>=chosenY*16+chosenX*16+16-(n.X+n.Width))n.Vy+=Math.Abs(n.Vx);
            return true;
        }
        private bool Slopes(ref NpcMotionState n,bool fall,out PredictionStop stop)
        {
            stop=PredictionStop.None;float x=n.X,y=n.Y,w=n.Width,h=n.Height,correctX=x,correctY=y,upper=y,lower=y,vx=n.Vx,vy=n.Vy;int mask=0;bool stairFall=false;
            if(!QueryArea(x,y,w,h,out stop))return false;
            for(int i=Math.Max(0,(int)(x/16)-1);i<Math.Min(Main.maxTilesX-1,(int)((x+w)/16)+2);i++)
            for(int j=Math.Max(0,(int)(y/16)-1);j<Math.Min(Main.maxTilesY-40,(int)((y+h)/16)+2);j++)
            {
                Cell c;if(!CellAt(i,j,out c,out stop))return false;if(!c.Solid && !(c.Platform && c.PlatformFrame))continue;
                float tx=i*16,ty=j*16+(c.Half?8:0),th=c.Half?8:16;
                if(x+w<=tx || x>=tx+16 || y+h<=ty || y>=ty+th)continue;
                if(c.StairPlatform && (n.Vy<0 || y+h<j*16 || y+h-(1+Math.Abs(n.Vx))>j*16+16 || ((c.Slope==1 && n.Vx>=0) || (c.Slope==2 && n.Vx<=0)) && (y+h)/16-1==j))continue;
                bool pass=n.StairFall && c.StairPlatform;ty=j*16;
                if(x+w<=tx || x>=tx+16 || y+h<=ty || y>=ty+16)continue;
                float offset=c.Slope==1 || c.Slope==3?x-tx:tx+16-(x+w);
                if(c.Slope==3 || c.Slope==4)
                {
                    if(offset>=0){float candidate=ty+16-offset;if(y<=candidate && candidate>lower){correctY=lower=candidate;vy=Math.Max(vy,.0101f);mask|=1<<c.Slope;}}
                    else if(y>ty && correctY<ty+16){correctY=ty+16;vy=Math.Max(vy,.0101f);}
                }
                else if(c.Slope==1 || c.Slope==2)
                {
                    if(offset>=0){float candidate=ty-h+offset;if(y+h<ty+offset || candidate>=upper)continue;if(pass){stairFall=true;continue;}correctY=upper=candidate;vy=Math.Min(vy,0);mask|=1<<c.Slope;}
                    else
                    {if(c.StairPlatform && y+h-4-Math.Abs(n.Vx)>ty){if(pass)stairFall=true;continue;}float candidate=ty-h;if(correctY<=candidate)continue;if(pass){stairFall=true;continue;}correctY=candidate;vy=Math.Min(vy,0);}
                }
            }
            float rx,ry;bool up;if(!TileContact(x,y,correctX-x,correctY-y,n.Width,n.Height,false,out rx,out ry,out up,out stop))return false;
            float shift=correctY-y;
            if(ry>shift){float delta=shift-ry;correctY=y+ry;if((mask&2)!=0)correctX=x-delta;if((mask&4)!=0)correctX=x+delta;vx=vy=0;}
            else if(ry<shift){float delta=ry-shift;correctY=y+ry;if((mask&8)!=0)correctX=x-delta;if((mask&16)!=0)correctX=x+delta;vx=vy=0;}
            if(!Area(correctX,correctY,w,h,out stop))return false;
            n.X=correctX;n.Y=correctY;n.Vx=vx;n.Vy=vy;if(stairFall)n.StairFall=true;else if(!fall)n.StairFall=false;return true;
        }
    }
}
