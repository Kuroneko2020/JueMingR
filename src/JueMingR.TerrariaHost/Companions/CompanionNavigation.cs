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
            private readonly Dictionary<int, int> positions = new Dictionary<int, int>();
            internal ICollection<int> Keys => positions.Keys;
            private readonly bool retainUnknown;
            internal bool Known;
            private bool trackArea;
            private Rectangle area, evictedArea;
            private int unknown, nextEviction;
            internal bool HasUnknown => unknown != 0;
            internal bool Truncated { get; private set; }
#if DEBUG
            internal int Moves, VerticalMoves;
#endif
            private int worldWidth, worldHeight;
            internal ReadSet(bool retainUnknown = false) { this.retainUnknown = retainUnknown; }
            internal void Clear() { Cells.Clear(); positions.Clear(); Known=true; trackArea=false; unknown=nextEviction=0;
                evictedArea=Rectangle.Empty; Truncated=false; worldWidth=Main.maxTilesX; worldHeight=Main.maxTilesY; }
            internal Rectangle TakeEvictedArea()
            {
                Rectangle result = evictedArea;
                evictedArea = Rectangle.Empty;
                return result;
            }
            internal void BeginArea() { area = Rectangle.Empty; trackArea = true; }
            internal Rectangle EndArea() { trackArea = false; return area; }
            internal Rectangle BeginNestedArea()
            { Rectangle outer = area; BeginArea(); return outer; }
            internal Rectangle EndNestedArea(Rectangle outer)
            {
                Rectangle result = area;
                if (outer.Width != 0) area = result.Width == 0 ? outer : Rectangle.Union(outer, result);
                return result;
            }
            internal bool Record(int x, int y)
            {
                if (trackArea)
                {
                    var readSquare = new Rectangle(x, y, 1, 1);
                    area = area.Width == 0 ? readSquare : Rectangle.Union(area, readSquare);
                }
                bool loaded = Loaded(x, y);
                if (!loaded && !retainUnknown) { Known = false; return false; }
                if (!loaded && (x < 1 || y < 1 || x >= worldWidth - 1 || y >= worldHeight - 42)) return false;
                int key = x * worldHeight + y;
                if (positions.TryGetValue(key, out int index))
                {
                    if (retainUnknown)
                    {
                        var previous = Cells[index];
                        int value = loaded ? TileValue(Main.tile[x, y]) : int.MinValue;
                        if (value != previous.Value)
                        {
                            // Old and newly read proofs cannot share this key
                            // across an intervening tile edit, even if the
                            // tile changes back before the rolling cursor.
                            InvalidateEvidence(x, y);
                            UpdateDependency(index, previous, value);
                        }
                    }
                    return loaded;
                }
                var cell = new Dependency { X = x, Y = y, Value = loaded ? TileValue(Main.tile[x, y]) : int.MinValue };
                if (Cells.Count < 2048)
                {
                    index = Cells.Count;
                    Cells.Add(cell);
                }
                else
                {
                    Truncated = true;
                    if (!retainUnknown) { Known = false; return false; }
                    // A retained frontier may track a new tile only after the
                    // search is told which old evidence can no longer prove it.
                    index = nextEviction;
                    var old = Cells[index];
                    positions.Remove(old.X * worldHeight + old.Y);
                    if (old.Value == int.MinValue) unknown--;
                    InvalidateEvidence(old.X, old.Y);
                    Cells[index] = cell;
                    nextEviction = (nextEviction + 1) % Cells.Count;
                }
                positions.Add(key, index);
                if (!loaded) unknown++;
                return loaded;
            }
            private void InvalidateEvidence(int x, int y)
            {
                var square = new Rectangle(x, y, 1, 1);
                evictedArea = evictedArea.Width == 0 ? square : Rectangle.Union(evictedArea, square);
            }
            private void UpdateDependency(int index, Dependency previous, int value)
            {
                if (previous.Value == int.MinValue) unknown--;
                if (value == int.MinValue) unknown++;
                previous.Value = value;
                Cells[index] = previous;
            }
            internal Rectangle Changes(ref int cursor, int count)
            {
                Rectangle changed = Rectangle.Empty;
                for (int i = 0; i < count && i < Cells.Count; i++)
                {
                    if (cursor >= Cells.Count) cursor = 0;
                    var cell = Cells[cursor++];
                    int value = Loaded(cell.X, cell.Y) ? TileValue(Main.tile[cell.X, cell.Y]) : int.MinValue;
                    if (value == cell.Value) continue;
                    UpdateDependency(cursor - 1, cell, value);
                    var square = new Rectangle(cell.X, cell.Y, 1, 1);
                    changed = changed.Width == 0 ? square : Rectangle.Union(changed, square);
                }
                return changed;
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
        private struct Step { internal Vector2 Position, Velocity; internal bool Ground, Wall, Known, WaterSurface, StairFall; }
        private const float NativeSlopeRestSpeed = .0101f;
        private readonly struct PlatformContact
        {
            internal readonly bool FallThrough, IgnorePlatforms, StairFall;

            internal PlatformContact(bool fallThrough, bool ignorePlatforms, bool stairFall)
            { FallThrough = fallThrough; IgnorePlatforms = ignorePlatforms; StairFall = stairFall; }

            // A geometry query may explicitly omit platforms. That is
            // stronger than native Down and must not simulate its input.
            internal static PlatformContact Geometry(bool platforms) => new PlatformContact(!platforms, !platforms, !platforms);

            internal static PlatformContact Native(Player body, bool down, bool stairFall)
            {
                int mount = body.mount.Type;
                bool forcedFall = body.gravDir < 0 || body.GoingDownWithGrapple || body.pulley ||
                    body.mount.Active && (body.mount.Cart || mount == 7 || mount == 8 || mount == 12 || mount == 23 ||
                        mount == 44 || mount == 48);
                bool ignorePlatforms = forcedFall || body.mount.Active && mount == 55 && body.slideDir != 0;
                return new PlatformContact(down || forcedFall, ignorePlatforms, stairFall || ignorePlatforms || down ||
                    body.grappling[0] >= 0);
            }
        }
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
        private static bool Move(Player body,Vector2 position,Vector2 velocity,bool platforms,bool steps,out Step result,ReadSet record=null,
            bool throughDoor=false) => MoveWithContact(body, position, velocity, PlatformContact.Geometry(platforms),
                steps, out result, record, throughDoor);

        private static bool MoveWithContact(Player body, Vector2 position, Vector2 velocity, PlatformContact contact,
            bool steps, out Step result, ReadSet record = null, bool throughDoor = false)
        {
#if DEBUG
            if(record!=null)record.Moves++;
#endif
            result=default(Step);
            if(!Footprint(body,position,velocity,record))return false;
            int direction = body.gravDir < 0 ? -1 : 1;
            if(steps && !contact.IgnorePlatforms && direction == 1)
            {
                Vector4 down=throughDoor ? CompanionDoorCollision.WalkDownSlope(position,velocity,body.width,body.height,body.gravity) :
                    Collision.WalkDownSlope(position,velocity,body.width,body.height,body.gravity);
                position=new Vector2(down.X,down.Y);velocity=new Vector2(down.Z,down.W);
                float stepSpeed=0,gfx=0;
                bool waterWalk = body.waterWalk || body.waterWalk2;
                if(velocity.Y==body.gravity)
                {
                    if(throughDoor) CompanionDoorCollision.StepDown(ref position,ref velocity,body.width,body.height,ref stepSpeed,ref gfx,1,waterWalk);
                    else Collision.StepDown(ref position,ref velocity,body.width,body.height,ref stepSpeed,ref gfx,1,waterWalk);
                }
                if(velocity.Y>=body.gravity && !contact.FallThrough)
                {
                    if(throughDoor) CompanionDoorCollision.StepUp(ref position,ref velocity,body.width,body.height,ref stepSpeed,ref gfx,1,body.controlUp,0);
                    else Collision.StepUp(ref position,ref velocity,body.width,body.height,ref stepSpeed,ref gfx,1,body.controlUp);
                }
                if(!Footprint(body,position,velocity,record))return false;
            }
            Vector2 actual=ResolveGroundContact(body,position,velocity,contact,throughDoor,out bool waterSurface);
            bool ground=velocity.Y * direction > 0 && actual.Y * direction < velocity.Y * direction;
            bool wall=Math.Abs(actual.X-velocity.X)>.1f;
            position+=actual; Vector2 advanced=position;
            Vector4 slope=throughDoor ? CompanionDoorCollision.SlopeCollision(position,actual,body.width,body.height,body.gravity,contact.StairFall,false) :
                Collision.SlopeCollision(position,actual,body.width,body.height,body.gravity,contact.StairFall);
            bool stairFall = Collision.stairFall || contact.FallThrough && contact.StairFall;
            position=new Vector2(slope.X,slope.Y);
            ground |= velocity.Y * direction >= 0 && (position.Y - advanced.Y) * direction < -.001f &&
                slope.W * direction <= NativeSlopeRestSpeed;
            result=new Step{Position=position,Velocity=new Vector2(slope.Z,slope.W),Ground=ground,Wall=wall,
                Known=Footprint(body,position,Vector2.Zero,record),WaterSurface=waterSurface && position.Y == advanced.Y,StairFall=stairFall};
            return result.Known && ClearBody(body,position,throughDoor) && (body.lavaImmune || !Collision.LavaCollision(position,body.width,body.height));
        }
        internal static bool Supported(Player body,Vector2 position,bool platforms=true)
        {
            // Liquid forbids a dry trajectory prediction, but still permits
            // native bottom support and a normal released jump input.
            if (body == null || body.shimmering || body.grappling[0] >= 0 || body.carpetFrame != -1 || body.IsRidingTracks)
                return false;
            using (Scratch.Save()) return HasSurface(body, position, null, platforms);
        }

        internal static bool ObservedSupport(Player body)
        {
            if (body == null || body.shimmering || body.grappling[0] >= 0 || body.carpetFrame != -1 || body.IsRidingTracks)
                return false;
            float vertical = body.velocity.Y * body.gravDir;
            if (vertical < -NativeSlopeRestSpeed) return false;
            using (Scratch.Save())
            {
                var contact = PlatformContact.Native(body, body.controlDown, body.stairFall);
                float stepY = MathHelper.Clamp(body.gravity + Math.Max(0, vertical), .05f,
                    Math.Max(.05f, body.maxFallSpeed)) * body.gravDir;
                if (!MoveWithContact(body, body.position, new Vector2(0, stepY), contact, false, out Step step) || !step.Ground)
                    return false;
                // Native Y velocity can still be the previous clipped
                // displacement in the first frame on a floor. Admit that
                // precise contact, never an approaching sweep's endpoint.
                return vertical <= 1 || Vector2.DistanceSquared(step.Position, body.position) <= .0001f;
            }
        }
        internal static bool SafeBody(Player body,Vector2 position)
        {
            using(Scratch.Save())return Footprint(body,position,Vector2.Zero) && ClearBody(body,position) &&
                (body.lavaImmune || !Collision.LavaCollision(position,body.width,body.height));
        }
        private static bool ClearBody(Player body,Vector2 position,bool throughDoor=false)
        {
            bool overlaps = throughDoor ? CompanionDoorCollision.SolidCollision(position,body.width,body.height) :
                Collision.SolidCollision(position,body.width,body.height);
            if(!overlaps)return true;
            int minX=(int)position.X/16,maxX=(int)(position.X+body.width-.01f)/16;
            int minY=(int)position.Y/16,maxY=(int)(position.Y+body.height-.01f)/16;
            for(int x=minX;x<=maxX;x++)for(int y=minY;y<=maxY;y++)
            {
                var tile=throughDoor ? CompanionDoorCollision.ReadTile(Main.tile,x,y) : Main.tile[x,y];
                if(!tile.active() || tile.inActive() || !Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type] || tile.slope()!=0)continue;
                if(position.Y+body.height>y*16+(tile.halfBrick()?8:0) && position.Y<(y+1)*16)return false;
            }
            Vector4 slope=throughDoor ? CompanionDoorCollision.SlopeCollision(position,Vector2.Zero,body.width,body.height,body.gravity,false,false) :
                Collision.SlopeCollision(position,Vector2.Zero,body.width,body.height,body.gravity);
            return Vector2.DistanceSquared(position,new Vector2(slope.X,slope.Y))<=4;
        }

        private static Vector2 ResolveGroundContact(Player body, Vector2 from, Vector2 velocity, PlatformContact contact,
            bool throughDoor, out bool waterSurface)
        {
            waterSurface = false;
            int direction = body.gravDir < 0 ? -1 : 1;
            bool hoik = body.width > 16 && body.height > 16;
            var actual = throughDoor ? CompanionDoorCollision.TileCollision(from, velocity, body.width, body.height,
                contact.FallThrough, contact.IgnorePlatforms, direction, false, false, hoik) :
                Collision.TileCollision(from, velocity, body.width, body.height, contact.FallThrough, contact.IgnorePlatforms,
                    direction, hoik: hoik);
            if (direction < 0 || actual.Y <= 0 || body.wet || !(body.waterWalk || body.waterWalk2)) return actual;
            // DryCollision checks the liquid surface after solid collision.
            // Limit shared support to the confirmed ordinary-water contact.
            var liquid = Collision.WaterCollision(from, actual, body.width, body.height, contact.FallThrough, lavaWalk: false);
            if (liquid.Y == actual.Y) return actual;
            float bottom = from.Y + liquid.Y + body.height;
            int row = (int)Math.Floor((bottom + .01f) / 16);
            int left = (int)Math.Floor((from.X + liquid.X) / 16);
            int right = (int)Math.Floor((from.X + liquid.X + body.width - .01f) / 16);
            for (int x = left; x <= right; x++)
            {
                Tile tile = Main.tile[x, row];
                if (tile.liquid > 0 && tile.water() && Math.Abs((row + 1) * 16 - (tile.liquid / 32 * 2 + 2) - bottom) < .1f)
                { waterSurface = true; return liquid; }
            }
            return actual;
        }

        private static bool HasSurface(Player body, Vector2 from, ReadSet terrain, bool platforms = true, bool throughDoor = false)
            => ProbeSurface(body, from, terrain, PlatformContact.Geometry(platforms), throughDoor, out Step step) && step.Ground;

        private static bool ProbeSurface(Player body, Vector2 from, ReadSet terrain, PlatformContact contact, bool throughDoor,
            out Step step) => MoveWithContact(body, from, new Vector2(0, body.gravDir < 0 ? -2 : 2), contact, false,
                out step, terrain, throughDoor);

        private static bool UnsupportedAt(Player body, Vector2 from, ReadSet terrain)
            => ProbeSurface(body, from, terrain, PlatformContact.Geometry(true), false, out Step step) && !step.Ground;

        private static bool TrySupportedTravel(Player body, Vector2 from, int direction, float distance, ReadSet terrain,
            out Vector2 to, out bool wall, bool steps = true, bool proveBrakingSurface = false, bool throughDoor = false,
            bool rejectDangerLiquids = false)
        {
            to = from;
            wall = false;
            // The same short native steps produce the projected support pose
            // for local control, graph connections and a horizontal fixture.
            // A dash or other no-step query has to keep its horizontal
            // braking lane. StepUp and small falls cannot extend its proof.
            int stride = steps ? 4 : 8;
            for (float traveled = 0; traveled < distance; traveled += stride)
            {
                float dx = direction * Math.Min(stride, distance - traveled);
                Vector2 before = to;
                if (body.gravDir < 0 || !steps)
                {
                    var across = new Vector2(dx, 0);
                    if (throughDoor)
                    {
                        if (!Footprint(body, to, across, terrain) ||
                            Vector2.DistanceSquared(ResolveGroundContact(body, to, across, PlatformContact.Geometry(true), true, out _), across) >= .25f ||
                            !ClearBody(body, to + across, throughDoor: true)) return false;
                    }
                    else if (!Swept(body, to, across, terrain)) return false;
                    if (!HasSurface(body, to + across, terrain, throughDoor: throughDoor)) return false;
                    to += across;
                }
                else
                {
                    if (!Move(body, to, new Vector2(dx, Math.Max(.1f, body.gravity)), true, true, out Step step, terrain, throughDoor)) return false;
                    if (step.Wall) { wall = true; return false; }
                    if (!step.Ground && !HasSurface(body, step.Position, terrain, throughDoor: throughDoor)) return false;
                    to = step.Position;
                }
                if (rejectDangerLiquids && !BodyLiquidClear(body, before, to, ordinaryOnly: false)) return false;
                if (proveBrakingSurface && !SteadyBrakingSurface(body, to)) return false;
            }
            return true;
        }

        private static bool BodyLiquidClear(Player body, Vector2 from, Vector2 to, bool ordinaryOnly)
        {
            Vector2 near = Vector2.Min(from, to), far = Vector2.Max(from, to) + body.Size - new Vector2(.01f);
            Point first = near.ToTileCoordinates(), last = far.ToTileCoordinates();
            for (int x = first.X; x <= last.X; x++)
                for (int y = first.Y; y <= last.Y; y++)
                {
                    Tile tile = Main.tile[x, y];
                    if (tile.liquid > 0 && (ordinaryOnly ? !tile.water() : tile.lava() || tile.shimmer())) return false;
                }
            return true;
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
                bool stairFall = body.stairFall;
                for(float distance=0;distance<ahead;distance+=4)
                {
                    Step step;
                    if(!MoveWithContact(body,position,new Vector2(direction*4,body.gravity),
                        PlatformContact.Native(body, false, stairFall),true,out step,record))
                        return step.Known?WalkOutcome.Unsafe:WalkOutcome.Unknown;
                    if(step.Wall)return WalkOutcome.Wall;
                    position=step.Position;
                    stairFall=step.StairFall;
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
                            if(!MoveWithContact(body,position,Vector2.UnitY,PlatformContact.Native(body, false, stairFall),
                                false,out step,record))return step.Known?WalkOutcome.Unsafe:WalkOutcome.Unknown;
                            stairFall=step.StairFall;
                            position=step.Position;if(step.Ground){found=true;break;}
                        }
                        if(!found)return WalkOutcome.Unsafe;
                    }
                }
                return WalkOutcome.Clear;
            }
        }
        // Combat must see the same imminent wall that stops normal walking.
        private const int WalkProbeDistance = 6;
        internal static bool Wall(Player body,int direction)
        {
            if(direction==0 || !Ordinary(body))return false;
            using(Scratch.Save()) return MoveWithContact(body,body.position,new Vector2(direction*WalkProbeDistance,body.gravity),
                PlatformContact.Native(body, false, body.stairFall),true,out Step step) && step.Wall;
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
                Vector2 position=body.position,velocity=body.velocity;bool holding=true;
                for(int tick=0;tick<90;tick++)
                {
                    velocity = JumpVelocity(body, default, velocity, direction, tick != 0, holding && tick <= jumpHold, jumpSpeed, body.gravity);
                    bool falling=velocity.Y>0;Step step;
                    if(!MoveJump(body,position,velocity,false,out step,jumpReads))return false; // unknown is never a stable negative cache
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
        { return ProbePlatformDrop(body,null); }
        internal bool PlanDrop(Player body,bool allowPlanning)
        {
            if(!Ordinary(body) || body.controlDown || body.stairFall){dropQuery.Valid=false;return false;}
            int cap=Capabilities(body)*31+DescentSafetyKey(body);if(dropQuery.Matches(body,cap))return dropQuery.Drop;
            dropQuery.Valid=false;if(!allowPlanning)return false;
            // This remains the first-observation compatibility fallback.
            // It uses the same ground-to-platform proof as the route solver.
            dropQuery.Prepare(body,cap);
#if DEBUG
            DropBuilds++;
#endif
            dropQuery.Drop=ProbePlatformDrop(body,dropQuery.Reads);
            dropQuery.Valid=dropQuery.Reads.Known;return dropQuery.Drop;
        }
        private static bool ProbePlatformDrop(Player body,ReadSet record)
        {
            if(!Ordinary(body) || body.controlDown || body.stairFall)return false;
            using(Scratch.Save())
            {
                var candidate = new RouteStep { From = body.position };
                var probe = default(DescentProbe);
                for (int slice = 0; slice < 4; slice++)
                {
                    var result = ProbeDescent(body, default, body.position, 0, body.velocity, record, ref probe, ref candidate, out _);
                    if (result != DescentResult.Pending) return result == DescentResult.Found;
                }
                return false;
            }
        }
    }
}
