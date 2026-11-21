using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    // Bounded value geometry only: the one real Player.Update owns movement.
    // Native queries mutate scratch and may allocate null tiles; scope their
    // scratch and validate their loaded, non-null footprint before calling.
    internal sealed partial class CompanionNavigation
    {
        private float jumpSpeed = 5.01f;
        private int jumpHold = 15;
        private Vector2 launch, landing, initialVelocity;
        private bool hasLanding, cached, cachedResult;
        private int plannedDirection, capabilities;
        private float targetHeight;
        private Player plannedBody;
        private object plannedWorld;
        private readonly ReadSet jumpReads = new ReadSet();
        private readonly GroundQuery leftWalk = new GroundQuery(), rightWalk = new GroundQuery(), dropQuery = new GroundQuery();
        internal int JumpHold { get { return jumpHold; } }
#if DEBUG
        // Native workload assertions only. Ordinary Release packages contain
        // neither these counters nor their per-query increments or outputs.
        internal int TrajectoryBuilds { get; private set; }
        internal int WalkBuilds { get; private set; }
        internal int DropBuilds { get; private set; }
        internal int WalkSearchSteps { get { return leftWalk.Reads.VerticalMoves + rightWalk.Reads.VerticalMoves; } }
        internal int WalkGeometryMoves { get { return leftWalk.Reads.Moves + rightWalk.Reads.Moves; } }
        internal int DropGeometryMoves { get { return dropQuery.Reads.Moves; } }
#endif
        private struct Dependency { internal int X, Y, Value; }
        internal enum WalkOutcome { Clear, Wall, Unsafe, Unknown, Deferred }
        private sealed class ReadSet
        {
            internal readonly List<Dependency> Cells = new List<Dependency>();
            internal readonly HashSet<int> Keys = new HashSet<int>();
            private readonly bool retainUnknown;
            internal bool Known;
#if DEBUG
            internal int Moves, VerticalMoves;
#endif
            private int worldWidth, worldHeight;
            internal ReadSet(bool retainUnknown = false) { this.retainUnknown = retainUnknown; }
            internal void Clear() { Cells.Clear(); Keys.Clear(); Known=true; worldWidth=Main.maxTilesX; worldHeight=Main.maxTilesY; }
            internal bool Record(int x, int y)
            {
                bool loaded = Loaded(x, y);
                if (!loaded && !retainUnknown) { Known = false; return false; }
                if (Cells.Count >= 2048) { if (!retainUnknown) Known = false; return retainUnknown && loaded; }
                if (Keys.Add(x * Main.maxTilesY + y))
                    Cells.Add(new Dependency { X = x, Y = y, Value = loaded ? TileValue(Main.tile[x, y]) : int.MinValue });
                return loaded;
            }
            internal bool Changed(ref int cursor, int count)
            {
                for (int i = 0; i < count && i < Cells.Count; i++)
                {
                    if (cursor >= Cells.Count) cursor = 0;
                    var cell = Cells[cursor++];
                    int value = Loaded(cell.X, cell.Y) ? TileValue(Main.tile[cell.X, cell.Y]) : int.MinValue;
                    if (value != cell.Value) return true;
                }
                return false;
            }
            internal bool Current()
            {
                if(!Known || worldWidth!=Main.maxTilesX || worldHeight!=Main.maxTilesY)return false;
                foreach(var cell in Cells)if(!Loaded(cell.X,cell.Y) || TileValue(Main.tile[cell.X,cell.Y])!=cell.Value)return false;
                return true;
            }
        }
        // Three actor-owned ordinary queries (left/right walk and drop), not a
        // world snapshot. Exact input keys plus their own finite native reads
        // keep a known failure cheap; unknown/deferred work never becomes one.
        private sealed class GroundQuery
        {
            internal readonly ReadSet Reads = new ReadSet();
            internal bool Valid, Drop;
            internal WalkOutcome Walk;
            private Player body;
            private object world;
            private Vector2 position, velocity;
            private int capabilities;
            internal bool Matches(Player current,int cap)
            { return Valid && ReferenceEquals(body,current) && ReferenceEquals(world,Main.ActiveWorldFileData) &&
                current.position==position && current.velocity==velocity && capabilities==cap && Reads.Current(); }
            internal void Prepare(Player current,int cap)
            { Valid=false;body=current;world=Main.ActiveWorldFileData;position=current.position;velocity=current.velocity;capabilities=cap;Reads.Clear(); }
        }
        private struct Step { internal Vector2 Position, Velocity; internal bool Ground, Wall, Known; }
        internal struct Scratch : IDisposable
        {
            private bool up, down, stair, fall, slope, honey, shimmer, mine;
            internal static Scratch Save() { return new Scratch { up=Collision.up, down=Collision.down, stair=Collision.stair, fall=Collision.stairFall,
                slope=Collision.sloping, honey=Collision.honey, shimmer=Collision.shimmer, mine=Collision.landMine }; }
            public void Dispose() { Collision.up=up; Collision.down=down; Collision.stair=stair; Collision.stairFall=fall;
                Collision.sloping=slope; Collision.honey=honey; Collision.shimmer=shimmer; Collision.landMine=mine; }
        }
        private static bool GroundContactBody(Player body)
        { return body != null && body.gravDir == 1 && !body.mount.Active && !body.shimmering &&
            body.grappling[0] == -1 && body.carpetFrame == -1 && !body.IsRidingTracks; }
        private static bool Ordinary(Player body) { return GroundContactBody(body) && !body.wet; }
        internal void ObserveCapabilities(Player body)
        { if (Ordinary(body)) { jumpSpeed=Math.Max(0,Player.jumpSpeed); jumpHold=Math.Max(1,Player.jumpHeight); } }
        private static int TileValue(Tile tile)
        {
            unchecked { int value=tile.type; value=value*31+(tile.active()?1:0); value=value*31+(tile.inActive()?1:0);
                value=value*31+tile.slope(); value=value*31+(tile.halfBrick()?1:0); value=value*31+tile.frameX; value=value*31+tile.frameY;
                value=value*31+tile.liquid; return value*31+tile.liquidType(); }
        }
        private static bool Loaded(int x,int y)
        { return x>=1 && y>=1 && x<Main.maxTilesX-1 && y<Main.maxTilesY-42 && Main.tile[x,y]!=null &&
            (Main.netMode!=1 || Main.sectionManager!=null && Main.sectionManager.TileLoaded(x,y)); }
        private static bool Footprint(Player body,Vector2 from,Vector2 velocity,ReadSet record=null)
        { return Footprint(body.width, body.height, from, velocity, record); }
        private static bool Footprint(int width,int height,Vector2 from,Vector2 velocity,ReadSet record=null)
        {
            // Two tiles cover Tile/SlopeCollision neighbors plus StepUp headroom
            // and StepDown's two foot rows, including the swept body.
            int x0=(int)Math.Floor(Math.Min(from.X,from.X+velocity.X)/16)-2;
            int x1=(int)Math.Floor((Math.Max(from.X,from.X+velocity.X)+width)/16)+2;
            int y0=(int)Math.Floor(Math.Min(from.Y,from.Y+velocity.Y)/16)-2;
            int y1=(int)Math.Floor((Math.Max(from.Y,from.Y+velocity.Y)+height)/16)+2;
            for(int x=x0;x<=x1;x++) for(int y=y0;y<=y1;y++)
            {
                if (record == null ? !Loaded(x, y) : !record.Record(x, y)) return false;
            }
            return true;
        }
        private static bool Move(Player body,Vector2 position,Vector2 velocity,bool platforms,bool steps,out Step result,ReadSet record=null)
        {
#if DEBUG
            if(record!=null)record.Moves++;
#endif
            result=default(Step);
            if(!Footprint(body,position,velocity,record))return false;
            if(steps && platforms)
            {
                Vector4 down=Collision.WalkDownSlope(position,velocity,body.width,body.height,body.gravity);
                position=new Vector2(down.X,down.Y);velocity=new Vector2(down.Z,down.W);
                float stepSpeed=0,gfx=0;
                if(velocity.Y==body.gravity)Collision.StepDown(ref position,ref velocity,body.width,body.height,ref stepSpeed,ref gfx,1,body.waterWalk||body.waterWalk2);
                if(velocity.Y>=body.gravity && !body.controlDown)Collision.StepUp(ref position,ref velocity,body.width,body.height,ref stepSpeed,ref gfx,1,body.controlUp);
                if(!Footprint(body,position,velocity,record))return false;
            }
            Vector2 actual=Collision.TileCollision(position,velocity,body.width,body.height,!platforms,!platforms);
            bool ground=velocity.Y>0 && actual.Y<velocity.Y;
            bool wall=Math.Abs(actual.X-velocity.X)>.1f;
            position+=actual; Vector2 advanced=position;
            Vector4 slope=Collision.SlopeCollision(position,actual,body.width,body.height,body.gravity,!platforms);
            position=new Vector2(slope.X,slope.Y);
            ground |= velocity.Y>=0 && position.Y<advanced.Y-.001f && slope.W<=0;
            result=new Step{Position=position,Velocity=new Vector2(slope.Z,slope.W),Ground=ground,Wall=wall,Known=Footprint(body,position,Vector2.Zero,record)};
            return result.Known && ClearBody(body,position) && (body.lavaImmune || !Collision.LavaCollision(position,body.width,body.height));
        }
        internal static bool Supported(Player body,Vector2 position,bool platforms=true)
        {
            // Liquid forbids a dry trajectory prediction, but still permits
            // native bottom support and a normal released jump input.
            if(!GroundContactBody(body))return false;
            using(Scratch.Save()) { Step step; return Move(body,position,new Vector2(0,1),platforms,false,out step) && step.Ground; }
        }
        internal static bool SafeBody(Player body,Vector2 position)
        {
            using(Scratch.Save())return Footprint(body,position,Vector2.Zero) && ClearBody(body,position) &&
                (body.lavaImmune || !Collision.LavaCollision(position,body.width,body.height));
        }
        private static bool ClearBody(Player body,Vector2 position)
        {
            if(!Collision.SolidCollision(position,body.width,body.height))return true;
            int minX=(int)position.X/16,maxX=(int)(position.X+body.width-.01f)/16;
            int minY=(int)position.Y/16,maxY=(int)(position.Y+body.height-.01f)/16;
            for(int x=minX;x<=maxX;x++)for(int y=minY;y<=maxY;y++)
            {
                var tile=Main.tile[x,y];
                if(!tile.active() || tile.inActive() || !Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type] || tile.slope()!=0)continue;
                if(position.Y+body.height>y*16+(tile.halfBrick()?8:0) && position.Y<(y+1)*16)return false;
            }
            Vector4 slope=Collision.SlopeCollision(position,Vector2.Zero,body.width,body.height,body.gravity);
            return Vector2.DistanceSquared(position,new Vector2(slope.X,slope.Y))<=4;
        }
        internal static bool CurrentKnown(Player body)
        { return !Ordinary(body) || Footprint(body,body.position,body.velocity); }
        internal static bool WalkSafe(Player body,int direction)
        { var outcome=InspectWalk(body,direction);return outcome==WalkOutcome.Clear || outcome==WalkOutcome.Wall; }
        internal static WalkOutcome InspectWalk(Player body,int direction)
        { return InspectWalkCore(body,direction,null,true); }
        internal WalkOutcome PlanWalk(Player body,int direction,bool allowPlanning)
        {
            if(direction==0)return WalkOutcome.Clear;if(!Ordinary(body))return WalkOutcome.Unsafe;
            var query=direction<0?leftWalk:rightWalk;int cap=Capabilities(body);
            if(query.Matches(body,cap))return query.Walk;
            query.Prepare(body,cap);
#if DEBUG
            WalkBuilds++;
#endif
            query.Walk=InspectWalkCore(body,direction,query.Reads,allowPlanning);
            query.Valid=query.Reads.Known && query.Walk!=WalkOutcome.Unknown && query.Walk!=WalkOutcome.Deferred;
            return query.Walk;
        }
        private static WalkOutcome InspectWalkCore(Player body,int direction,ReadSet record,bool allowSearch)
        {
            if(direction==0)return WalkOutcome.Clear;if(!Ordinary(body))return WalkOutcome.Unsafe;
            using(Scratch.Save())
            {
                float braking=body.velocity.X*body.velocity.X/Math.Max(.08f,2*body.runSlowdown);
                float ahead=Math.Min(128,Math.Max(16,braking+12)); Vector2 position=body.position;
                bool platforms=!body.controlDown && !body.stairFall;
                for(float distance=0;distance<ahead;distance+=4)
                {
                    Step step;
                    if(!Move(body,position,new Vector2(direction*4,body.gravity),platforms,true,out step,record))return step.Known?WalkOutcome.Unsafe:WalkOutcome.Unknown;
                    if(step.Wall)return WalkOutcome.Wall;
                    position=step.Position;
                    if(!step.Ground)
                    {
                        // A normal immediate walking prefix stays available to
                        // every actor. New multi-step ground searches consume
                        // the same team planning slice as jump/drop planning.
                        if(!allowSearch)return WalkOutcome.Deferred;
                        bool found=false;
                        for(int y=0;y<48;y++)
                        {
#if DEBUG
                            if(record!=null)record.VerticalMoves++;
#endif
                            if(!Move(body,position,new Vector2(0,1),platforms,false,out step,record))return step.Known?WalkOutcome.Unsafe:WalkOutcome.Unknown;
                            position=step.Position;if(step.Ground){found=true;break;}
                        }
                        if(!found)return WalkOutcome.Unsafe;
                    }
                }
                return WalkOutcome.Clear;
            }
        }
        internal static bool Wall(Player body,int direction)
        {
            if(direction==0 || !Ordinary(body))return false;
            using(Scratch.Save()) { Step step; return Move(body,body.position,new Vector2(direction*4,body.gravity),!body.controlDown && !body.stairFall,true,out step) && step.Wall; }
        }
        private int Capabilities(Player body)
        {
            unchecked { int value=body.width*31+body.height;value=value*31+body.gravity.GetHashCode();value=value*31+body.maxFallSpeed.GetHashCode();
                value=value*31+body.maxRunSpeed.GetHashCode();value=value*31+body.runAcceleration.GetHashCode();value=value*31+body.runSlowdown.GetHashCode();
                value=value*31+jumpSpeed.GetHashCode();value=value*31+jumpHold;value=value*31+(body.controlDown?1:0);
                value=value*31+(body.stairFall?1:0);
                value=value*31+(body.controlUp?1:0);value=value*31+(body.waterWalk||body.waterWalk2?1:0);return value*31+(body.lavaImmune?1:0); }
        }
        internal bool JumpSafe(Player body,Vector2 target,int direction,bool allowPlanning)
        {
            if(!Ordinary(body) || jumpSpeed<=0){cached=hasLanding=false;return false;}
            int cap=Capabilities(body);float height=target.Y;
            // One actor-owned trajectory cache. Its exact launch/body/world and
            // finite native read footprint are dependencies for either outcome;
            // the team grants at most one NEW trajectory, never a second update.
            if(cached && (!cachedResult || hasLanding) && ReferenceEquals(body,plannedBody) && ReferenceEquals(Main.ActiveWorldFileData,plannedWorld) &&
                direction==plannedDirection && height==targetHeight && cap==capabilities && body.position==launch && body.velocity==initialVelocity && jumpReads.Current())return cachedResult;
            cached=hasLanding=false;
            if(!allowPlanning || direction==0 && target.Y>=body.Center.Y-36)return false;
            launch=body.position;initialVelocity=body.velocity;plannedDirection=direction;targetHeight=height;capabilities=cap;
            plannedBody=body;plannedWorld=Main.ActiveWorldFileData;
            jumpReads.Clear();
#if DEBUG
            TrajectoryBuilds++;
#endif
            using(Scratch.Save())
            {
                Vector2 position=body.position,velocity=body.velocity;velocity.Y=-jumpSpeed;bool holding=true;
                for(int tick=0;tick<90;tick++)
                {
                    if(direction!=0)velocity.X=MathHelper.Clamp(velocity.X+direction*Math.Max(.01f,body.runAcceleration),-Math.Max(body.maxRunSpeed,Math.Abs(initialVelocity.X)),Math.Max(body.maxRunSpeed,Math.Abs(initialVelocity.X)));
                    else velocity.X*=Math.Max(0,1-body.runSlowdown);
                    if(holding && tick<jumpHold)velocity.Y=-jumpSpeed;else velocity.Y=Math.Min(body.maxFallSpeed,velocity.Y+Math.Max(.05f,body.gravity));
                    bool falling=velocity.Y>0;Step step;
                    if(!Move(body,position,velocity,!body.controlDown && !body.stairFall,true,out step,jumpReads))return false; // unknown is never a stable negative cache
                    // Native horizontal clipping against the face of a step
                    // can coexist with upward motion along it. Continue that
                    // ascent from the clipped pose; descending into a wall
                    // still cannot prove a usable landing on the far side.
                    if(step.Wall && !(velocity.Y<0 && step.Velocity.Y<0)){cached=true;cachedResult=false;return false;}
                    if(velocity.Y<0 && step.Velocity.Y>=0)holding=false; // native head contact retires the remaining hold
                    position=step.Position;velocity=step.Velocity;
                    if(falling && step.Ground)
                    {
                        cached=true;cachedResult=position.Y<=launch.Y+64 && (direction==0 || direction*(position.X-launch.X)>=24);
                        if(cachedResult){landing=position;hasLanding=true;}return cachedResult;
                    }
                }
                cached=true;cachedResult=false;return false;
            }
        }
        internal static bool DropSafe(Player body)
        { return DropCore(body,null); }
        internal bool PlanDrop(Player body,bool allowPlanning)
        {
            if(!Ordinary(body) || body.controlDown || body.stairFall){dropQuery.Valid=false;return false;}
            int cap=Capabilities(body);if(dropQuery.Matches(body,cap))return dropQuery.Drop;
            dropQuery.Valid=false;if(!allowPlanning)return false;
            // Unlike an immediate support probe, a new 160-step lower-landing
            // search belongs to the team's granted planning opportunity. Both
            // success and known failure retain their real finite read set.
            dropQuery.Prepare(body,cap);
#if DEBUG
            DropBuilds++;
#endif
            dropQuery.Drop=DropCore(body,dropQuery.Reads);
            dropQuery.Valid=dropQuery.Reads.Known;return dropQuery.Drop;
        }
        private static bool DropCore(Player body,ReadSet record)
        {
            if(!Ordinary(body) || body.controlDown || body.stairFall)return false;
            using(Scratch.Save())
            {
                Vector2 position=body.position;Step step;
                if(!Move(body,position,new Vector2(0,1),true,false,out step,record) || !step.Ground)return false;
                if(!Move(body,position,new Vector2(0,1),false,false,out step,record) || step.Ground)return false;
                for(int y=0;y<160;y++)
                { if(!Move(body,position,new Vector2(0,1),y>=16,false,out step,record))return false;position=step.Position;if(y>=16 && step.Ground)return true; }
                return false;
            }
        }
    }
}
