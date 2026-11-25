using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionNativeContact
    {
#if DEBUG
        [ThreadStatic] private static int reads, predictions, brakingTicks;
        internal static int Reads => reads;
        internal static int Predictions => predictions;
        internal static int BrakingTicks => brakingTicks;
        internal static void RecordBrakingTick() => brakingTicks++;
#endif
        internal static Vector2 Feet(Player body) => body.gravDir < 0 ? body.Top : body.Bottom;
        internal static Vector2 TopLeft(Player body, Vector2 feet) =>
            feet - new Vector2(body.width * .5f, body.gravDir < 0 ? 0f : body.height);

        internal struct Step
        {
            internal Vector2 Position, Velocity;
            internal bool Ground, Wall, Known, WaterSurface, StairFall;
        }
        private const float NativeSlopeRestSpeed = .0101f;
        internal readonly struct PlatformContact
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
        internal static bool Loaded(int x,int y)
        {
#if DEBUG
            reads++;
#endif
            return Main.tile != null && x>=1 && y>=1 && x<Main.maxTilesX-1 && y<Main.maxTilesY-42 && Main.tile[x,y]!=null &&
                (Main.netMode!=1 || Main.sectionManager!=null && Main.sectionManager.TileLoaded(x,y));
        }
        private static bool Footprint(Player body,Vector2 from,Vector2 velocity)
        { return Footprint(body.width, body.height, from, velocity); }
        private static bool Footprint(int width,int height,Vector2 from,Vector2 velocity)
        {
            // Two tiles cover Tile/SlopeCollision neighbors plus StepUp headroom
            // and StepDown's two foot rows, including the swept body.
            int x0=(int)Math.Floor(Math.Min(from.X,from.X+velocity.X)/16)-2;
            int x1=(int)Math.Floor((Math.Max(from.X,from.X+velocity.X)+width)/16)+2;
            int y0=(int)Math.Floor(Math.Min(from.Y,from.Y+velocity.Y)/16)-2;
            int y1=(int)Math.Floor((Math.Max(from.Y,from.Y+velocity.Y)+height)/16)+2;
            for(int x=x0;x<=x1;x++) for(int y=y0;y<=y1;y++)
            {
                if (!Loaded(x, y)) return false;
            }
            return true;
        }
        internal static bool TryMove(Player body, Vector2 position, Vector2 velocity, PlatformContact contact,
            bool steps, out Step result, bool throughDoor = false)
        {
#if DEBUG
            predictions++;
#endif
            using (CompanionCollisionScope.Save())
                return Move(body, position, velocity, contact, steps, out result, throughDoor);
        }

        private static bool Move(Player body, Vector2 position, Vector2 velocity, PlatformContact contact,
            bool steps, out Step result, bool throughDoor)
        {
            result = default;
            if(!Footprint(body,position,velocity))return false;
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
                if(!Footprint(body,position,velocity))return false;
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
                Known=Footprint(body,position,Vector2.Zero),WaterSurface=waterSurface && position.Y == advanced.Y,StairFall=stairFall};
            return result.Known && ClearBody(body,position,throughDoor);
        }
        internal static bool Supported(Player body,Vector2 position,bool platforms=true)
        {
            // Liquid forbids a dry trajectory prediction, but still permits
            // native bottom support and a normal released jump input.
            if (body == null || body.shimmering || body.grappling[0] >= 0 || body.carpetFrame != -1 || body.IsRidingTracks)
                return false;
            return TryMove(body, position, new Vector2(0f, body.gravDir < 0 ? -2 : 2),
                PlatformContact.Geometry(platforms), false, out var contact) && contact.Ground;
        }

        internal static bool ObservedSupport(Player body) => TryObserveSupport(body, out bool supported) && supported;

        private static bool TryObserveSupport(Player body, out bool supported)
        {
            supported = false;
            if (body == null || body.shimmering || body.grappling[0] >= 0 || body.carpetFrame != -1 || body.IsRidingTracks)
                return true;
            float vertical = body.velocity.Y * body.gravDir;
            if (vertical < -NativeSlopeRestSpeed) return true;
            var contact = PlatformContact.Native(body, body.controlDown, body.stairFall);
            float stepY = MathHelper.Clamp(body.gravity + Math.Max(0, vertical), .05f,
                Math.Max(.05f, body.maxFallSpeed)) * body.gravDir;
            if (!TryMove(body, body.position, new Vector2(0, stepY), contact, false, out Step step))
                return false;
            // Native Y velocity can still be the previous clipped
            // displacement in the first frame on a floor. Admit that
            // precise contact, never an approaching sweep's endpoint.
            supported = step.Ground && (vertical <= 1 || Vector2.DistanceSquared(step.Position, body.position) <= .0001f);
            return true;
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

        internal static bool CanObserveWetSupport(Player body, CompanionMobilitySnapshot mobility)
        {
            float vertical = body.velocity.Y * body.gravDir;
            return !mobility.GrappleAttached && !mobility.Pulley && !mobility.Track && !mobility.Shimmer && vertical >= 0 && vertical <= 1;
        }

        internal static bool TryOwnerSupport(Player body, out bool supported)
        {
            if (body.pulley || body.stairFall)
            {
                supported = false;
                return true;
            }
            return TryObserveSupport(body, out supported);
        }

    }
}
