using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed class CompanionFishingCast
    {
        internal const int AnglesPerTurn = 2;
        internal const int FlightLimit = 180;
        private const int Size = 14;
        private const int Half = Size / 2;
        private readonly List<float> angles = new List<float>(95);
        private readonly Dictionary<Point, int> footprint = new Dictionary<Point, int>();
        private readonly int direction;
        private readonly Vector2 origin, bodyCenter;
        private readonly float speed;
        private int nextAngle;
        internal readonly int RodType, BobberType;
        internal readonly float Gravity;
        internal readonly CompanionFishingWater.Entry Entry;
        internal Vector2 Aim { get; private set; }
        internal int Facing { get; private set; }
        internal bool Found { get; private set; }
        internal bool Exhausted => nextAngle >= angles.Count;
        internal int LastSteps { get; private set; }

        internal CompanionFishingCast(Player actor, Item rod, CompanionFishingWater.Entry entry)
        {
            RodType = rod.type;
            BobberType = Bobber(actor, rod);
            Gravity = actor.gravDir;
            Entry = entry;
            bodyCenter = actor.Center;
            origin = actor.RotatedRelativePoint(actor.MountedCenter);
            speed = rod.shootSpeed;
            Vector2 delta = entry.Surface - origin;
            direction = delta.X < 0 ? -1 : 1;
            angles.Add((float)Math.Atan2(delta.Y, Math.Abs(delta.X)));
            AddBallisticAngles(delta);
            // The direct and ballistic shots cover open water. The bounded
            // sweep follows native flight through openings above the entry.
            for (int degrees = 88; degrees >= -88; degrees -= 2)
                angles.Add(MathHelper.ToRadians(degrees));
            angles.Add(MathHelper.ToRadians(-89.8f));
            angles.Add(MathHelper.ToRadians(89.8f));
        }

        internal static int Bobber(Player actor, Item rod) => actor.overrideFishingBobber >= 0 ? actor.overrideFishingBobber : rod.shoot;

        private void AddBallisticAngles(Vector2 delta)
        {
            if (speed <= 0f) return;
            float xSum = 0, drag = 1, lastPower = float.MaxValue;
            Vector2 last = Vector2.Zero;
            for (int step = 1; step <= FlightLimit; step++)
            {
                drag *= .98f;
                xSum += drag;
                var required = new Vector2(Math.Abs(delta.X) / xSum, delta.Y / step - .1f * (step + 1));
                float power = required.Length() - speed;
                if (step > 1 && (power <= 0f) != (lastPower <= 0f))
                {
                    float t = lastPower / (lastPower - power);
                    Vector2 solution = Vector2.Lerp(last, required, t);
                    float angle = (float)Math.Atan2(solution.Y, solution.X);
                    angles.Add(angle);
                    angles.Add(angle - .008f);
                    angles.Add(angle + .008f);
                }
                last = required;
                lastPower = power;
            }
        }

        internal bool Matches(Player actor, Item rod) => RodType == rod.type && BobberType == Bobber(actor, rod) &&
            Gravity == actor.gravDir && speed == rod.shootSpeed && Vector2.DistanceSquared(origin, actor.RotatedRelativePoint(actor.MountedCenter)) <= 9;

        internal bool TerrainCurrent(Func<int, int, Tile> read)
        {
            foreach (var cell in footprint)
            {
                var tile = read(cell.Key.X, cell.Key.Y);
                if (tile == null || cell.Value != Stamp(tile)) return false;
            }
            return true;
        }

        internal void Reject()
        { Found = false; footprint.Clear(); }

        internal CompanionFishingWater.Result Advance(Func<int, int, Tile> read, int anglesPerTurn = AnglesPerTurn)
        {
            LastSteps = 0;
            using (CompanionCollisionScope.Save())
            {
                while (anglesPerTurn-- > 0 && !Exhausted && !Found)
                {
                    float angle = angles[nextAngle];
                    var heading = new Vector2((float)Math.Cos(angle) * direction, (float)Math.Sin(angle));
                    var result = Test(heading, read);
                    if (result == CompanionFishingWater.Result.Unknown) return result;
                    nextAngle++;
                    if (result != CompanionFishingWater.Result.Ready) continue;
                    Aim = origin + heading * 420;
                    Facing = heading.X > 0 ? 1 : -1;
                    Found = true;
                }
            }
            return Found || Exhausted ? CompanionFishingWater.Result.Ready : CompanionFishingWater.Result.Pending;
        }

        private CompanionFishingWater.Result Test(Vector2 heading, Func<int, int, Tile> read)
        {
            // ItemCheck_Shoot uses RotatedRelativePoint(MountedCenter) and
            // normalizes the real cursor vector at shootSpeed for these rods.
            // All 1.4.6.1 vanilla AI-61 bobbers use a 14x14 collision box.
            Vector2 velocity = heading * speed;
            Vector2 center = origin;
            footprint.Clear();
            int stalled = 0;
            for (int i = 0; i < FlightLimit; i++)
            {
                LastSteps++;
                if (center.X < 32 || center.Y < 32 || center.X > (Main.maxTilesX - 2) * 16 ||
                    center.Y > (Main.maxTilesY - 2) * 16 || Vector2.DistanceSquared(center, bodyCenter) > 900 * 900)
                    break;
                if (velocity.Y == 0f) velocity.X *= .95f;
                velocity.X *= .98f;
                velocity.Y = Math.Min(15.9f, velocity.Y + .2f);
                Vector2 position = center - new Vector2(Half);
                if (!ObserveSpan(position, velocity, read)) return CompanionFishingWater.Result.Unknown;
                if (Collision.WetCollision(position, Size, Size))
                {
                    var cell = center.ToTileCoordinates();
                    // Initial wet detection can straddle the air tile above
                    // the surface, but cannot stand in for an adjacent pool.
                    return cell.X == Entry.Cell.X && cell.Y >= Entry.Cell.Y - 1 && cell.Y <= Entry.Cell.Y + 1 ?
                        CompanionFishingWater.Result.Ready : CompanionFishingWater.Result.Pending;
                }
                Vector2 moved = Move(position, ref velocity);
                center += moved;
                stalled = moved.LengthSquared() < .02f ? stalled + 1 : 0;
                if (stalled >= 4 || center.Y > (Entry.Cell.Y + 3) * 16 && velocity.Y >= 0 ||
                    direction * (center.X - (Entry.Cell.X * 16 + (direction > 0 ? 24 : -8))) > 0)
                    break;
            }
            return CompanionFishingWater.Result.Pending;
        }

        private static Vector2 Move(Vector2 position, ref Vector2 velocity)
        {
            // Vanilla subdivides fast shots by their collision size. Bobbers
            // normally pass downward through platforms, unlike player feet.
            Vector2 start = position;
            Vector2 incoming = velocity;
            float distance = incoming.Length();
            if (distance <= Size)
            {
                Vector2 step = Collision.TileCollision(position, incoming, Size, Size, true, true);
                Vector4 slope = Collision.SlopeCollision(position, step, Size, Size, 0f, fall: true);
                velocity = new Vector2(slope.Z, slope.W);
                position = new Vector2(slope.X, slope.Y) + velocity;
            }
            else
            {
                Vector2 heading = incoming / distance;
                if (Collision.TileCollision(position, incoming, Size, Size, true, true).Y == 0f) heading.Y = 0f;
                velocity = Vector2.Zero;
                while (distance > 0f)
                {
                    float length = Math.Min(distance, Size);
                    distance -= length;
                    Vector2 step = Collision.TileCollision(position, heading * length, Size, Size, true, true, 1,
                        ignoreDoors: false, ignoreAetheriumPlatforms: false, hoik: true);
                    position += step;
                    Vector4 slope = Collision.SlopeCollision(position, step, Size, Size, 0f, fall: true);
                    position = new Vector2(slope.X, slope.Y);
                    velocity += new Vector2(slope.Z, slope.W);
                }
            }
            // Collision and the bobber's reflected speed are separate native
            // results. Use a bounded forecast and confirm with the real wet bit.
            if (Math.Abs(velocity.X - incoming.X) > .0001f) velocity.X = incoming.X * -.3f;
            if (Math.Abs(velocity.Y - incoming.Y) > .0001f && incoming.Y > 1f) velocity.Y = incoming.Y * -.3f;
            return position - start;
        }

        private bool ObserveSpan(Vector2 position, Vector2 velocity, Func<int, int, Tile> read)
        {
            int left = (int)(Math.Min(position.X, position.X + velocity.X) / 16) - 1;
            int right = (int)((Math.Max(position.X, position.X + velocity.X) + Size) / 16) + 1;
            int top = (int)(Math.Min(position.Y, position.Y + velocity.Y) / 16) - 1;
            int bottom = (int)((Math.Max(position.Y, position.Y + velocity.Y) + Size) / 16) + 1;
            for (int x = left; x <= right; x++) for (int y = top; y <= bottom; y++)
            {
                var key = new Point(x, y);
                if (footprint.ContainsKey(key)) continue;
                var tile = read(x, y);
                if (tile == null) return false;
                footprint.Add(key, Stamp(tile));
            }
            return true;
        }

        private static int Stamp(Tile tile) => tile.liquid | (tile.liquidType() << 8) | (tile.slope() << 10) |
            (tile.halfBrick() ? 1 << 13 : 0) | (tile.inActive() ? 1 << 14 : 0) | (tile.active() ? (tile.type + 1) << 15 : 0);
    }
}
