using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class PredictionTerrain : IPredictionTerrain,IPredictionResizeTerrain,IPredictionWaterSurfaceTerrain,IPredictionPlayerTerrain
    {
        private struct Cell : IEquatable<Cell>
        {
            internal bool Active,RawActive,RawSolid,RawPlatform,Solid,Platform,StairPlatform,PlatformFrame,ProperPlatformFrame,CuttableFrame,Half,CanSit,FallSafe,Bouncy,DetonatorArmed;internal byte Slope,Liquid,Kind;internal ushort Type,Wall;internal short FrameX,FrameY;internal int Conveyor;
            public bool Equals(Cell b){return Active==b.Active && RawActive==b.RawActive && RawSolid==b.RawSolid && RawPlatform==b.RawPlatform && Wall==b.Wall && Type==b.Type && ProperPlatformFrame==b.ProperPlatformFrame && CuttableFrame==b.CuttableFrame && Solid==b.Solid && Platform==b.Platform && StairPlatform==b.StairPlatform && PlatformFrame==b.PlatformFrame && Half==b.Half && Slope==b.Slope && Liquid==b.Liquid && Kind==b.Kind && Conveyor==b.Conveyor && CanSit==b.CanSit && FallSafe==b.FallSafe && Bouncy==b.Bouncy && DetonatorArmed==b.DetonatorArmed && FrameX==b.FrameX && FrameY==b.FrameY;}
        }
        private readonly Dictionary<int,Cell> cells=new Dictionary<int,Cell>(512);
        // Small direct hot cache for repeated liquid/contact/slope queries.
        // The dictionary remains the authoritative distinct-cell/bounds owner.
        // Epoch changes on every real sample, so no old tile becomes current.
        private readonly int[] hotKeys=new int[256];
        private readonly long[] hotEpochs=new long[256];
        private readonly Cell[] hotCells=new Cell[256];
        private struct DryRegion
        {internal int Left,Right,Top,Bottom;internal long Epoch;}
        private readonly DryRegion[] dryRegions=new DryRegion[32];
        private struct SlopeFreeRegion
        {internal int Left,Right,Top,Bottom;internal long Epoch;}
        private readonly SlopeFreeRegion[] slopeFreeRegions=new SlopeFreeRegion[32];
        private long epoch=1;
        private int width,height;
        private bool continuing;
#if DEBUG
        internal int Reads {get;private set;}
#endif
        public void Reset(){if(continuing)return;cells.Clear();width=Main.maxTilesX;height=Main.maxTilesY;if(epoch==long.MaxValue){Array.Clear(hotEpochs,0,hotEpochs.Length);Array.Clear(dryRegions,0,dryRegions.Length);Array.Clear(slopeFreeRegions,0,slopeFreeRegions.Length);epoch=0;}epoch++;}
        public bool Unchanged
        {
            get{if(width!=Main.maxTilesX || height!=Main.maxTilesY)return false;foreach(var pair in cells){Cell current;if(!Read(pair.Key%width,pair.Key/width,out current) || !current.Equals(pair.Value))return false;}return true;}
        }
        // A candidate can leave this owner before it is considered again.
        // Freeze only the bounded cells actually read, never a second world.
        // Reset/candidate switching cannot erase this independent read lease.
        internal sealed class CellLease
        {
            private readonly int width,height;
            private readonly KeyValuePair<int,Cell>[] values;
            internal int Count {get{return values.Length;}}
            private CellLease(int width,int height,Dictionary<int,Cell> cells)
            {this.width=width;this.height=height;values=new KeyValuePair<int,Cell>[cells.Count];int at=0;foreach(var pair in cells)values[at++]=pair;}
            internal bool Current
            {
                get
                {
                    if(width<=0 || width!=Main.maxTilesX || height!=Main.maxTilesY)return false;
                    foreach(var pair in values){Cell current;if(!Read(pair.Key%width,pair.Key/width,out current) || !current.Equals(pair.Value))return false;}
                    return true;
                }
            }
            internal bool DisplayCurrent(bool alreadyValidated)
            {
                if(!alreadyValidated)return Current;
                // A repeated presentation entry still checks EVERY dependency:
                // GUC is not a tile revision. This bounded mandatory guard has
                // no producer/clock checks that can turn quota into a terrain
                // change; its time stays charged to the enclosing work owner.
                // Acquisition caps this set at 4096 cells. New-update and
                // candidate validation continue through ordinary budget checks.
                if(width<=0 || width!=Main.maxTilesX || height!=Main.maxTilesY)return false;
                foreach(var pair in values){Cell current;if(!ReadCell(pair.Key%width,pair.Key/width,out current) || !current.Equals(pair.Value))return false;}
                return true;
            }
            internal static CellLease Capture(PredictionTerrain terrain)
            {return new CellLease(terrain.width,terrain.height,terrain.cells);}
            internal void Restore(PredictionTerrain terrain)
            {foreach(var pair in values)terrain.cells.Add(pair.Key,pair.Value);}
        }
        internal CellLease FreezeCells(){return CellLease.Capture(this);}
        // A verified deterministic suffix still depends on every old cell.
        // Keep that bounded lease while the two new endpoint actions acquire
        // additional cells. The ordinary 4096-cell limit forces a fresh full
        // calculation if a travelling window accumulates too much history.
        internal bool BeginContinuation(CellLease lease)
        {
            if(continuing || lease==null || !lease.Current)return false;
            Reset();lease.Restore(this);continuing=true;return true;
        }
        internal void EndContinuation(){continuing=false;}
        private static bool Read(int x,int y,out Cell value)
        {
            CombatPredictionWork.Check();
            return ReadCell(x,y,out value);
        }
        private static bool ReadCell(int x,int y,out Cell value)
        {
            value=default(Cell);if(Main.tile==null || x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY)return false;
            var tile=Main.tile[x,y];if(tile==null)return false;
            bool active=tile.active() && !tile.inActive();
            int frame=tile.frameX/18;
            // AI007 uses full furniture frames. Unrelated animated frames
            // must not invalidate every collision-only terrain dependency.
            bool canSit=TileID.Sets.CanBeSatOnForNPCs[tile.type];
            value=new Cell{Active=active,RawActive=tile.active(),RawSolid=Main.tileSolid[tile.type],RawPlatform=Main.tileSolidTop[tile.type],Wall=tile.wall,Type=tile.type,ProperPlatformFrame=frame>=0 && frame<=7 || frame>=12 && frame<=16 || frame>=25 && frame<=26,CuttableFrame=tile.type!=254 || tile.frameX>=144,Solid=active && Main.tileSolid[tile.type],Platform=active && Main.tileSolidTop[tile.type],StairPlatform=TileID.Sets.Platforms[tile.type],PlatformFrame=tile.frameY==0,Half=tile.halfBrick(),Slope=tile.slope(),Liquid=tile.liquid,Kind=(byte)tile.liquidType(),Conveyor=TileID.Sets.ConveyorDirection[tile.type],CanSit=canSit,FallSafe=tile.active() && (TileID.Sets.Clouds[tile.type] || tile.type==666 || tile.type==19 && tile.frameY/18==49),Bouncy=Main.tileBouncy[tile.type],DetonatorArmed=tile.type==411 && tile.frameY==0 && tile.frameX<36,FrameX=canSit?tile.frameX:(short)0,FrameY=canSit?tile.frameY:(short)0};return true;
        }
        public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop)
        {
            Cell c;tile=default(PredictionTile);if(!CellAt(x,y,out c,out stop))return false;
            tile=new PredictionTile{Active=c.Active,RawActive=c.RawActive,RawSolid=c.RawSolid,Wall=c.Wall,Solid=c.Solid,SolidTop=c.Platform,Platform=c.StairPlatform,Half=c.Half,Slope=c.Slope,Liquid=c.Liquid,Lava=c.Kind==1,ShimmerLiquid=c.Kind==3,Type=c.Type,ProperPlatformFrame=c.ProperPlatformFrame,SurfacePlatform=c.PlatformFrame,CanSit=c.CanSit,FallSafe=c.FallSafe,FrameX=c.FrameX,FrameY=c.FrameY};return true;
        }
        private bool CellAt(int x,int y,out Cell cell,out PredictionStop stop)
        {
            CombatPredictionWork.CheckGeometry();
            stop=PredictionStop.None;cell=default(Cell);
            if(x<0 || y<0 || x>=Main.maxTilesX || y>=Main.maxTilesY){stop=PredictionStop.TerrainUnavailable;return false;}
            int key=y*Main.maxTilesX+x,hot=(key^(key>>7)^(key>>16))&255;
            // Ice changes are private forecast values. Check before either
            // cache so a player cannot silently consume the old world cell.
            if(icePlayerGuard!=null && icePlayerGuard.Contains(key)){stop=PredictionStop.TerrainUnavailable;return false;}
            if(icePathReads!=null)icePathReads.Record(x,y);
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
            if(!FreeBody(box.X,box.Y,box.Width,box.Height,out stop))return false;
            int w=(int)box.Width,h=(int)box.Height;
            int left=Math.Max(0,Math.Min(Main.maxTilesX-1,(int)(box.X/16)-1)),right=Math.Max(0,Math.Min(Main.maxTilesX-1,(int)((box.X+w)/16)+2));
            int top=Math.Max(0,Math.Min(Main.maxTilesY-40,(int)(box.Y/16)-1)),bottom=Math.Max(0,Math.Min(Main.maxTilesY-40,(int)((box.Y+h)/16)+2));
            for(int x=left;x<right;x++)for(int y=top;y<bottom;y++)
            {
                Cell c;if(!CellAt(x,y,out c,out stop))return false;
                // Original SolidCollision clips the scan and uses strict
                // positive overlap, including a single float ULP. Subtracting
                // an epsilon can change Santa-NK1's support/velocity branch.
                // Slopes count as full solids; platforms and liquid do not.
                if(!c.Solid || c.Platform)continue;
                float surface=y*16+(c.Half?8:0);
                if(box.X+w>x*16 && box.X<x*16+16 && box.Y+h>surface && box.Y<y*16+16){solid=true;return true;}
            }
            return true;
        }
        public bool CanHit(MotionRect source,MotionRect target,out bool clear,out PredictionStop stop)
        {
            clear=false;stop=PredictionStop.None;
            // Original CanHit truncates each position and clamps its center
            // before tracing. A body outside the world is not missing terrain:
            // retreating actors still receive this defined border query. Keep
            // finite state validation and fail on genuinely missing cells.
            if(!FreeBody(source.X,source.Y,source.Width,source.Height,out stop,true) || !FreeBody(target.X,target.Y,target.Width,target.Height,out stop,true))return false;
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
        internal bool MeleeLine(float x,float y,float nx,float ny)
        {
            // Locked CanHitLine only reads tiles/local variables. Record its
            // finite melee rectangle including side neighbours first, so the
            // native pure query cannot escape this preparation's terrain lease.
            int left=Math.Max(1,(int)(Math.Min(x,nx)/16)-1),right=Math.Min(Main.maxTilesX-2,(int)(Math.Max(x,nx)/16)+1);
            int top=Math.Max(1,(int)(Math.Min(y,ny)/16)-1),bottom=Math.Min(Main.maxTilesY-41,(int)(Math.Max(y,ny)/16)+1);
            if(right-left>64 || bottom-top>64)return false;
            for(int tx=left;tx<=right;tx++)for(int ty=top;ty<=bottom;ty++){Cell cell;PredictionStop stop;if(!CellAt(tx,ty,out cell,out stop))return false;}
            return Collision.CanHitLine(new Microsoft.Xna.Framework.Vector2(x,y),0,0,new Microsoft.Xna.Framework.Vector2(nx,ny),0,0);
        }
        public bool Move(ref NpcMotionState n,PredictionEnvironment environment,out PredictionStop stop)
        {var player=default(PredictionPlayerMotion);return Move(ref n,environment,false,false,false,false,1,ref player,out stop);}
        public bool MoveWaterWalkingPlayer(ref NpcMotionState n,PredictionEnvironment environment,bool fallThrough,bool lavaWalk,out PredictionStop stop)
        {var player=new PredictionPlayerMotion{GravityDirection=1};return Move(ref n,environment,true,true,fallThrough,lavaWalk,1,ref player,out stop);}
        public bool MovePlayer(ref NpcMotionState n,PredictionEnvironment environment,ref PredictionPlayerMotion player,out PredictionStop stop)
        {
            if(player.UnsupportedGeometry){stop=PredictionStop.UnsupportedMechanism;return false;}
            float gfxStep=(1+Math.Abs(n.Vx)/3)*player.StepSpeed;
            player.GfxOffset=player.GfxOffset<0?Math.Min(0,player.GfxOffset+gfxStep):Math.Max(0,player.GfxOffset-gfxStep);
            n.StairFall=player.StairFall;
            if(player.WallFactsCaptured && player.WallTongued)
            {if(!PlayerTongueMove(ref n,ref player,out stop))return false;}
            else if(!Move(ref n,environment,true,player.WaterWalk,player.Down || player.IgnorePlatforms,player.LavaWalk,(int)player.GravityDirection,ref player,out stop))return false;
            player.StairFall=n.StairFall;
            // DryCollision clears normal-gravity jump at its first tile
            // contact, before later slope/belt queries overwrite collision
            // scratch. Final head contact separately owns next-tick Vy.
            if(n.PlayerDryHeadCollision)player.Jump=0;
            if(n.PlayerHeadCollision)
            {
                // Position consumes the clipped contact displacement first.
                // The final Player.Update head response owns next-tick velocity
                // and jump state; a clipped displacement is not that velocity.
                n.Vy=.01f*player.GravityDirection;
                if(!player.Merman)player.Jump=0;
            }
            return true;
        }
        private bool Move(ref NpcMotionState n,PredictionEnvironment environment,bool playerMode,bool waterWalk,bool fallThrough,bool lavaWalk,int gravDir,ref PredictionPlayerMotion player,out PredictionStop stop,bool playerSegment=false,bool allowSplit=true)
        {
            stop=PredictionStop.None;
            // Native UpdateNPC bypasses UpdateCollision altogether here. In
            // particular Duke/Sharkron do not receive liquid movement drag.
            if(n.NoTileCollide)
            {
                // Native free flight may cross the world edge before its own
                // later AI/lifetime exit. Its only geometry query is the native
                // clipped WetCollision scan; an empty clipped scan is known.
                // Other NPC collision scans use the same clipping below;
                // projectile/player acquisition keeps its separate boundary.
                if(!FreeBody(n.X,n.Y,n.Width,n.Height,out stop) || !Finite(n.Vx) || !Finite(n.Vy) || Math.Abs(n.Vx)>512 || Math.Abs(n.Vy)>512){stop=PredictionStop.InvalidState;return false;}
                var free=n;free.OldX=free.X;free.OldY=free.Y;free.X+=free.Vx;free.Y+=free.Vy;
                bool wetContact;byte kind;if(!Wet(free,false,out wetContact,out kind,out stop,true))return false;
                if(!playerMode && wetContact && !environment.Multiplayer)NpcHealth.Extinguish(ref free.Health);
                n=free;return true;
            }
            // NPCs can legitimately teleport/retreat through a world edge.
            // The original collision helpers clip each query, including an
            // empty known scan beyond the bottom forty tiles. This does not
            // invent missing in-world cells or relax player/projectile leases.
            bool nativeClip=!playerMode;
            if(nativeClip?(!FreeBody(n.X,n.Y,n.Width,n.Height,out stop) || !FreeBody(n.X+n.Vx,n.Y+n.Vy,n.Width,n.Height,out stop)):(!Area(n.X,n.Y,n.Width,n.Height,out stop) || !Area(n.X+n.Vx,n.Y+n.Vy,n.Width,n.Height,out stop)))return false;
            if(n.Width<1 || n.Height<1 || n.Width>1024 || n.Height>1024 || Math.Abs(n.Vx)>512 || Math.Abs(n.Vy)>512){stop=PredictionStop.InvalidState;return false;}
            var next=n;
            if(playerMode){if(!PlayerSteps(ref next,ref player,playerSegment,out stop))return false;}
            else if(!WalkDown(ref next,out stop,true))return false;
            bool lava,wet;byte liquid;
            if(!Wet(next,true,out lava,out liquid,out stop,nativeClip) || !Wet(next,false,out wet,out liquid,out stop,nativeClip))return false;
            if(next.Identity.Type==441)lava=false;
            if(lava)
            {
                next.Lava=true;
                if(!playerMode && !next.Health.LavaImmune && !next.Health.DontTakeDamage && !environment.Multiplayer && next.Health.Immune255==0)
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
            // Native lava damage/effects precede water eligibility. A family
            // opting out of wetness must not erase that already-consumed lava.
            if(!playerMode && !NpcCollisionRules.LiquidEligible(next)){wet=lava=false;next.WetCount=0;}
            if(!playerMode && next.Style==116)next.WetCount=10;
            if(!playerMode && wet!=next.Wet && next.WetCount==0)next.WetCount=10;
            if(!playerMode && next.LiquidTargetKind==3)next.LiquidTargetY+=next.LiquidTargetVy;
            if(!playerMode && next.Wet && !wet)
            {
                next.Vx*=.5f;
                float targetY=next.LiquidTargetKind==1 && next.LiquidTargetPlayer==environment.PlayerIndex?environment.PlayerY:next.LiquidTargetY;
                if(next.EffectiveType==620 && targetY<next.Bounds.CenterY)next.Vy-=8;
            }
            next.Wet=wet;next.Honey=wet && (liquid==2 || !playerMode && next.Honey);next.Shimmer=wet && (liquid==3 || !playerMode && next.Shimmer);next.Lava=wet && next.Lava;
            if(!playerMode && next.WetCount>0)next.WetCount--;
            if(!playerMode && wet && !environment.Multiplayer)
            {if(!next.Lava)NpcHealth.Extinguish(ref next.Health);if(liquid==3 && !next.Health.ShimmerImmune && next.Health.ShimmerTicks<=10)next.Health.ShimmerTicks=100;}
            next.OldVx=next.Vx;next.OldVy=next.Vy;
            bool fall=playerMode?fallThrough:NpcCollisionRules.FallThrough(next,environment);
            bool wetPlayer=wet && (next.Shimmer || next.Honey && !player.IgnoreWater || !player.Merman && !player.IgnoreWater && !player.Trident);
            if(playerMode && allowSplit && !wetPlayer && next.Vx*next.Vx+next.Vy*next.Vy>Math.Pow(Math.Min(16,Math.Min(next.Width-.5f,next.Height-.5f)),2))
            {
                if(!PlayerSegments(ref next,environment,ref player,waterWalk,fall,lavaWalk,gravDir,out stop) || !PlayerTail(ref next,ref player,fall,gravDir,out stop))return false;
                n=next;return true;
            }
            float rx,ry;bool up,down;
            var moveBox=playerMode?next.Bounds:NpcCollisionRules.MovementBounds(next);
            if(playerMode && player.OnTrack)moveBox.Height-=10;
            bool wheel=!playerMode && next.EffectiveType==72,sand=!playerMode && next.EffectiveType>=542 && next.EffectiveType<=545;
            if(wheel)moveBox=new MotionRect(next.X+next.Width/2-6,next.Y+next.Height/2-6,12,12);
            if(sand && environment.Remix){rx=next.Vx;ry=next.Vy;up=down=false;}
            else if(!TileContact(moveBox.X,moveBox.Y,next.Vx,next.Vy,(int)moveBox.Width,(int)moveBox.Height,wheel || fall,out rx,out ry,out up,out down,out stop,wheel || (playerMode?player.IgnorePlatforms:fall),gravDir,wheel,sand,nativeClip))return false;
            next.PlayerHeadCollision=playerMode && (gravDir>0?up:down);
            next.PlayerDryHeadCollision=playerMode && !wetPlayer && gravDir>0 && up;
            // Native Stardust cells rebound before translating the body. The
            // contact velocity is not their resulting movement velocity.
            if(next.EffectiveType==405 || next.EffectiveType==406)
            {if(rx!=0 && rx!=next.OldVx)rx=-next.OldVx*.8f;if(ry!=0 && ry!=next.OldVy)ry=-next.OldVy*.8f;}
            if(!playerMode && next.EffectiveType==417 && next.A0==6 && (rx!=next.OldVx || ry!=next.OldVy))
            {
                // 2/3/4 all rebound at contact one; contact two may exhaust
                // only the lower branch. Stop before publishing that choice.
                if(next.UncertainBounceCount && next.A2<=1){stop=PredictionStop.RandomDecision;return false;}
                next.A2--;next.A3=1;if(next.A2>0){if(rx!=0 && rx!=next.OldVx){rx=-next.OldVx*.9f;next.Direction=-next.Direction;}if(ry!=0 && ry!=next.OldVy)ry=-next.OldVy*.9f;}
            }
            if(waterWalk && !WaterSurface(next.Bounds,rx,ref ry,fallThrough,lavaWalk,out stop))return false;
            if(up && !playerMode)ry=.01f;next.CollideX=rx!=next.Vx;next.CollideY=ry!=next.Vy;next.Vx=rx;next.Vy=ry;
            float slowdown=wet?(next.Shimmer?next.ShimmerSpeed:next.Honey?next.HoneySpeed:next.Lava?next.LavaSpeed:next.WaterSpeed):1;
            if(playerMode)slowdown=wet?(next.Shimmer?.375f:next.Honey && !player.IgnoreWater?.25f:!player.Merman && !player.IgnoreWater && !player.Trident?.5f:1):1;
            next.OldX=next.X;next.OldY=next.Y;next.X+=next.CollideX?rx:rx*slowdown;next.Y+=next.CollideY?ry:ry*slowdown;
            // Native WetCollision translates before TryFloatingInFluid, then
            // Player.Update runs slope/belt. Capability alone is not action;
            // dry movement and above-line ascent keep ordinary geometry.
            if(playerMode && wetPlayer && player.FloatInWater && (!next.Shimmer || player.ShimmerImmune))
            {
                bool constrained;player.FloatingNow=true;
                if(!FloatingConstraint(next,player.FloatMount37,out constrained,out stop))return false;
                player.FloatingNow=constrained;
                if(constrained){stop=PredictionStop.UnsupportedMechanism;return false;}
            }
            if(fall)next.StairFall=true;
            int movingType=next.EffectiveType;
            if(playerMode || movingType!=72 && movingType!=247 && movingType!=248 && (movingType<542 || movingType>545) && (!NPCID.Sets.BelongsToInvasionOldOnesArmy[movingType] || !next.NoGravity))
            {
                if(!playerMode && !TownStair(ref next,environment,out stop))return false;
                // SlopeCollision returns coordinates in the movement box. Keep
                // the original body dimensions/offset and receive box intact.
                moveBox=playerMode?next.Bounds:NpcCollisionRules.MovementBounds(next);var slope=next;
                float dx=next.X-moveBox.X,dy=next.Y-moveBox.Y;slope.X=moveBox.X;slope.Y=moveBox.Y;slope.Width=(int)moveBox.Width;slope.Height=(int)moveBox.Height;
                if(playerMode && (player.IgnorePlatforms || player.Down || player.Grappled || gravDir<0))slope.StairFall=true;
                if(!Slopes(ref slope,fall,out stop,playerMode,gravDir,nativeClip))return false;
                next.X=slope.X+dx;next.Y=slope.Y+dy;next.Vx=slope.Vx;next.Vy=slope.Vy;next.StairFall=slope.StairFall;
                if(playerMode)next.PlayerHeadCollision=slope.PlayerHeadCollision;
                // Native applies belt contact after movement-box slopes have
                // returned to body coordinates. Belt displacement never owns V.
                if(playerMode?!player.SkipConveyor && Math.Abs(player.GfxOffset)<=2:next.Style!=67 && (next.Town || next.LifeMax==5 && next.NoContactDamage || NPCID.Sets.ConveyorBeltCollision[movingType]))
                    if(!Conveyor(ref next,playerMode,player.OnTrack,gravDir,out stop))return false;
            }
            n=next;return true;
        }
        private bool TownStair(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(n.Style!=7)return true;
            Cell tile;if(!CellAt((int)n.Bounds.CenterX/16,(int)n.Y/16,out tile,out stop))return false;
            bool home=tile.RawActive && tile.RawSolid && n.Town;
            if(!e.Day || e.Eclipse)home=true;else if(n.HomeTileY-(int)(n.Y+n.Height)/16>16)home=true;
            if(home)n.StairFall=(n.Y+n.Height-8)/16f<n.HomeTileY;return true;
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
        private static bool FreeBody(float x,float y,float w,float h,out PredictionStop stop,bool allowPoint=false)
        {
            stop=PredictionStop.None;
            if(!Finite(x) || !Finite(y) || !Finite(w) || !Finite(h) || w<(allowPoint?0:1) || h<(allowPoint?0:1) || w>1024 || h>1024 || Math.Abs(x)>int.MaxValue-4096f || Math.Abs(y)>int.MaxValue-4096f){stop=PredictionStop.InvalidState;return false;}return true;
        }
        private bool Wet(NpcMotionState n,bool lavaOnly,out bool wet,out byte kind,out PredictionStop stop,bool nativeClip=false)
        {
            CombatPredictionWork.Check();
            wet=false;kind=0;stop=PredictionStop.None;
            if(nativeClip?!FreeBody(n.X,n.Y,n.Width,n.Height,out stop):!QueryArea(n.X,n.Y,n.Width,n.Height,out stop))return false;
            int sensorWidth=lavaOnly?n.Width:Math.Min(10,n.Width),sensorHeight=lavaOnly?n.Height:n.Height/2;
            float sensorX=lavaOnly?n.X:n.X+n.Width/2-sensorWidth/2,sensorY=lavaOnly?n.Y:n.Y+n.Height/2-sensorHeight/2;
            int left=Math.Max(0,(int)(n.X/16)-1),right=Math.Min(Main.maxTilesX-1,(int)((n.X+n.Width)/16)+2);
            int top=Math.Max(0,(int)(n.Y/16)-1),bottom=Math.Min(Main.maxTilesY-40,(int)((n.Y+n.Height)/16)+2);
            // This proof covers the WHOLE native scan rectangle, not merely
            // this body's sensor. Floating motion within that same rectangle
            // cannot acquire liquid from an all-dry captured region. Keep the
            // original area/clipping gate above and the complete cell lease;
            // private ice guards/read tracking must still observe every cell.
            bool cacheable=width==Main.maxTilesX && height==Main.maxTilesY && icePlayerGuard==null && icePathReads==null;
            int slot=(left^(right*397)^(top*17)^(bottom*31))&31;
            var dry=dryRegions[slot];
            if(cacheable && dry.Epoch==epoch && dry.Left==left && dry.Right==right && dry.Top==top && dry.Bottom==bottom)return true;
            bool allDry=true;
            for(int x=left;x<right;x++)for(int y=top;y<bottom;y++)
            {
                Cell c;if(!CellAt(x,y,out c,out stop))return false;float surface=y*16;int liquidHeight=16;byte liquidKind=c.Kind;
                // A sloped active cell can borrow liquid from the cell above.
                // Conservatively leave every such region on the normal scan.
                if(c.Liquid!=0 || c.RawActive && c.Slope!=0)allDry=false;
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
            if(cacheable && allDry)dryRegions[slot]=new DryRegion{Left=left,Right=right,Top=top,Bottom=bottom,Epoch=epoch};
            return true;
        }
        internal bool ProjectileWet(float x,float y,int width,int height,out bool wet,out byte kind,bool nativeClip=false)
        {
            PredictionStop stop;return Wet(new NpcMotionState{X=x-width*.5f,Y=y-height*.5f,Width=width,Height=height},false,out wet,out kind,out stop,nativeClip);
        }
        internal bool ProjectileLava(float x,float y,int width,int height,out bool lava,bool nativeClip=false)
        {
            byte kind;PredictionStop stop;return Wet(new NpcMotionState{X=x-width*.5f,Y=y-height*.5f,Width=width,Height=height},true,out lava,out kind,out stop,nativeClip);
        }
        internal bool ProjectileCenterLava(float x,float y,out bool lava)
        {
            lava=false;if(!Finite(x) || !Finite(y) || x<0 || y<0)return false;
            // WaterBolt AI_008 tests the centre tile's lava bit, including
            // a retained liquid kind when its amount is zero. Keep this read
            // in the same dependency lease as collision and liquid queries.
            Cell cell;PredictionStop stop;if(!CellAt((int)x/16,(int)y/16,out cell,out stop))return false;lava=cell.Kind==1;return true;
        }
        internal bool ProjectilePassage(float x,float y,float nx,float ny,int width,int height)
        {
            float rx,ry;bool up;PredictionStop stop;float vx=nx-x,vy=ny-y;
            // Same pure rectangular contact rules already used by the first
            // batch. Native TileCollision also writes up/down and fills null
            // neighboring live tiles; a preparation query must do neither.
            return TileContact(x-width*.5f,y-height*.5f,vx,vy,width,height,true,out rx,out ry,out up,out stop) && Math.Abs(rx-vx)<.0001f && Math.Abs(ry-vy)<.0001f;
        }
        internal bool ProjectileCollision(float x,float y,float vx,float vy,int width,int height,out float rx,out float ry,bool fallThrough=true)
        {
            // A failed terrain acquisition is not a known impact trigger.
            // Callers receive native contact components only on success.
            bool up;PredictionStop stop;return TileContact(x-width*.5f,y-height*.5f,vx,vy,width,height,fallThrough,out rx,out ry,out up,out stop);
        }
        internal bool ProjectileSlope(ref float x,ref float y,ref float vx,ref float vy,int width,int height)
        {
            // Native ordinary projectile movement applies SlopeCollision with
            // gravity zero before its type-specific reflection. Reuse the
            // private terrain rules and dependency reads, never live Collision.
            var state=new NpcMotionState{X=x-width*.5f,Y=y-height*.5f,Vx=vx,Vy=vy,Width=width,Height=height};PredictionStop stop;
            if(!Slopes(ref state,true,out stop))return false;x=state.X+width*.5f;y=state.Y+height*.5f;vx=state.Vx;vy=state.Vy;return true;
        }
        private bool TileContact(float px0,float py0,float vx,float vy,int w,int h,bool fall,out float rx,out float ry,out bool up,out PredictionStop stop,bool fall2=true,int gravDir=1)
        {bool down;return TileContact(px0,py0,vx,vy,w,h,fall,out rx,out ry,out up,out down,out stop,fall2,gravDir);}
        private bool TileContact(float px0,float py0,float vx,float vy,int w,int h,bool fall,out float rx,out float ry,out bool up,out bool down,out PredictionStop stop,bool fall2=true,int gravDir=1,bool noSlope=false,bool sand=false,bool nativeClip=false,bool signedProjectile=false)
        {
            rx=vx;ry=vy;up=down=false;stop=PredictionStop.None;
            if(signedProjectile?(!SignedProjectileArea(px0,py0,w,h,true,out stop,nativeClip) || !SignedProjectileArea(px0+vx,py0+vy,w,h,false,out stop,nativeClip)):nativeClip?(!FreeBody(px0,py0,w,h,out stop) || !FreeBody(px0+vx,py0+vy,w,h,out stop)):(!QueryArea(px0,py0,w,h,out stop) || !Area(px0+vx,py0+vy,w,h,out stop)))return false;
            float px=px0+vx,py=py0+vy;
            int sideX=-1,sideY=-1,verticalX=-1,verticalY=-1;
            // Ordinary native rectangular contacts: projected full velocity,
            // x-major tile order and contact tie rules. No extra swept substeps.
            int x0=Math.Max(0,(int)(px0/16)-1),x1=Math.Min(Main.maxTilesX-1,(int)((px0+w)/16)+2);
            int y0=Math.Max(0,(int)(py0/16)-1),y1=Math.Min(Main.maxTilesY-40,(int)((py0+h)/16)+2);float nearest=(y1+3)*16;
            for(int x=x0;x<x1;x++)for(int y=y0;y<y1;y++)
            {
                Cell c;if(!CellAt(x,y,out c,out stop))return false;
                if(noSlope){if(!c.RawActive || !c.RawSolid && !(c.RawPlatform && c.PlatformFrame))continue;c.Platform=c.RawPlatform;}
                else if(!c.Solid && !(c.Platform && c.PlatformFrame) || sand && TileID.Sets.ForAdvancedCollision.ForSandshark[c.Type])continue;
                float tx=x*16,ty=y*16+(c.Half?8:0),height=c.Half?8:16;
                if(px+w<=tx || px>=tx+16 || py+h<=ty || py>=ty+height)continue;
                bool topSlope=!noSlope && (c.Slope==1 || c.Slope==2);
                if(!noSlope && (c.Slope==3 && py0+Math.Abs(vx)>=ty && px0>=tx || c.Slope==4 && py0+Math.Abs(vx)>=ty && px0+w<=tx+16 ||
                    c.Slope==1 && py0+h-Math.Abs(vx)<=ty+height && px0>=tx || c.Slope==2 && py0+h-Math.Abs(vx)<=ty+height && px0+w<=tx+16))continue;
                if(py0+h<=ty)
                {down=true;if(!(c.Platform && fall && (vy<=1 || fall2)) && nearest>ty){verticalX=x;verticalY=y+(c.Half?1:0);if(verticalX!=sideX && !topSlope){ry=ty-(py0+h)+(gravDir<0?-.01f:0);nearest=ty;}}}
                else if(px0+w<=tx && !c.Platform)
                {Cell neighbor;if(!noSlope && x>0){if(!CellAt(x-1,y,out neighbor,out stop))return false;if(neighbor.Slope==2 || neighbor.Slope==4)continue;}sideX=x;sideY=y;if(sideY!=verticalY)rx=tx-(px0+w);if(verticalX==sideX)ry=vy;}
                else if(px0>=tx+16 && !c.Platform)
                {Cell neighbor;if(!noSlope){if(!CellAt(x+1,y,out neighbor,out stop))return false;if(neighbor.Slope==1 || neighbor.Slope==3)continue;}sideX=x;sideY=y;if(sideY!=verticalY)rx=tx+16-px0;if(verticalX==sideX)ry=vy;}
                else if(py0>=ty+height && !c.Platform)
                {up=true;verticalX=x;verticalY=y;ry=ty+height-py0+(gravDir==1?.01f:0);if(verticalY==sideY)rx=vx;}
            }
            return true;
        }
        private bool WalkDown(ref NpcMotionState n,out PredictionStop stop,bool nativeClip=false)
        {
            stop=PredictionStop.None;if(n.Vy!=n.Gravity)return true;
            if(nativeClip?!FreeBody(n.X,n.Y,n.Width,n.Height+20,out stop):!QueryArea(n.X,n.Y,n.Width,n.Height+20,out stop))return false;
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
        private bool Slopes(ref NpcMotionState n,bool fall,out PredictionStop stop,bool playerMode=false,int gravDir=1,bool nativeClip=false,bool signedProjectile=false,bool rawPlayerProbe=false)
        {
            stop=PredictionStop.None;float x=n.X,y=n.Y,w=n.Width,h=n.Height,correctX=x,correctY=y,upper=y,lower=y,vx=n.Vx,vy=n.Vy;int mask=0;bool stairFall=false;
            if(signedProjectile?!SignedProjectileArea(x,y,w,h,true,out stop,nativeClip):nativeClip?!FreeBody(x,y,w,h,out stop):!QueryArea(x,y,w,h,out stop))return false;
            CombatPredictionWork.Check();
            int left=Math.Max(0,(int)(x/16)-1),right=Math.Min(Main.maxTilesX-1,(int)((x+w)/16)+2);
            int top=Math.Max(0,(int)(y/16)-1),bottom=Math.Min(Main.maxTilesY-40,(int)((y+h)/16)+2);
            bool cacheable=w>0 && h>0 && width==Main.maxTilesX && height==Main.maxTilesY && icePlayerGuard==null && icePathReads==null;
            int slot=(left^(right*397)^(top*17)^(bottom*31))&31;var flat=slopeFreeRegions[slot];
            bool flatMatch=cacheable && flat.Epoch==epoch && flat.Left==left && flat.Right==right && flat.Top==top && flat.Bottom==bottom;
            // A COMPLETE scan proving every leased cell has Slope==0 cannot
            // write any correction/mask/stair state, regardless of float pose
            // inside the same discrete bounds. Retain acquisition and the
            // whole cell lease; unknowns and even inactive nonzero slopes deny
            // this proof. Ice capture/guards must still visit each cell. A hit
            // saves only this first scan, never entry/exit validation or tails.
            bool allFlat=true;
            for(int i=left;!flatMatch && i<right;i++)
            for(int j=top;j<bottom;j++)
            {
                Cell c;if(!CellAt(i,j,out c,out stop))return false;if(c.Slope!=0)allFlat=false;if(!c.Solid && !(c.Platform && c.PlatformFrame))continue;
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
            float rx=0,ry=0;bool up=false,down=false;
            // With positive body dimensions and exactly zero slope correction,
            // TileContact's strict overlap excludes every edge-response branch.
            // The first scan already leased the identical cells (and checked
            // ice guards); repeating it cannot change the zero result. Keep the
            // final area validation and all flags/velocity/stair handling below.
            if((correctX!=x || correctY!=y || w<=0 || h<=0) && !TileContact(x,y,correctX-x,correctY-y,n.Width,n.Height,false,out rx,out ry,out up,out down,out stop,true,1,false,false,nativeClip,signedProjectile))return false;
            float shift=correctY-y;
            if(ry>shift){float delta=shift-ry;correctY=y+ry;if((mask&2)!=0)correctX=x-delta;if((mask&4)!=0)correctX=x+delta;vx=vy=0;up=false;}
            else if(ry<shift){float delta=ry-shift;correctY=y+ry;if((mask&8)!=0)correctX=x-delta;if((mask&16)!=0)correctX=x+delta;vx=vy=0;}
            if(signedProjectile?!SignedProjectileArea(correctX,correctY,w,h,false,out stop,nativeClip):nativeClip?!FreeBody(correctX,correctY,w,h,out stop):!Area(correctX,correctY,w,h,out stop))return false;
            if(cacheable && !flatMatch && allFlat)slopeFreeRegions[slot]=new SlopeFreeRegion{Left=left,Right=right,Top=top,Bottom=bottom,Epoch=epoch};
            // Native SlopeCollision's final TileCollision replaces the shared
            // flags even for zero correction. Early Dry Jump remains separate.
            if(playerMode)n.PlayerHeadCollision=gravDir>0?up:down;
            n.X=correctX;n.Y=correctY;n.Vx=vx;n.Vy=playerMode && !rawPlayerProbe && gravDir<0 && vy==.0101f?0:vy;if(stairFall)n.StairFall=true;else if(!fall)n.StairFall=false;return true;
        }
    }
}
