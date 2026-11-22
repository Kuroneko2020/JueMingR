using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private static readonly AccessTools.FieldRef<int[]> railLeft =
            AccessTools.StaticFieldRefAccess<int[]>(AccessTools.Field(typeof(Minecart), "_leftSideConnection"));
        private static readonly AccessTools.FieldRef<int[]> railRight =
            AccessTools.StaticFieldRefAccess<int[]>(AccessTools.Field(typeof(Minecart), "_rightSideConnection"));
        private static readonly AccessTools.FieldRef<int[][]> railHeight =
            AccessTools.StaticFieldRefAccess<int[][]>(AccessTools.Field(typeof(Minecart), "_tileHeight"));

        private static int FlightBudget(CompanionMobilitySnapshot mobility)
        {
            if (mobility.UnlimitedWings || mobility.UnlimitedMountHover) return 120;
            // Vanilla spends one rocket unit per burst, not per update. With
            // wings the same units become six wing updates instead of ten.
            int rocket = mobility.RocketAvailable ? mobility.RocketTime * (mobility.Wings > 0 ? 6 : 10) + mobility.RocketDelay : 0;
            int flight = (int)mobility.WingTime + rocket + mobility.MountFlightTime;
            int extra = mobility.ExtraJumps;
            int jumps = 0;
            while (extra != 0) { jumps++; extra &= extra - 1; }
            // Refresh only after observing the real landing or attachment. The
            // graph subtracts predicted use and never restores the native budget.
            return Math.Min(120, flight + jumps * 12 + (mobility.Sliding ? 10 : 0));
        }

        private static int GlideBudget(CompanionMobilitySnapshot mobility)
        {
            // CarpetMovement supplies 300 ticks on first use. The local graph
            // reserves a smaller window, without changing the actual counter.
            return mobility.CarpetAvailable ? 120 : Math.Min(120, mobility.CarpetTime);
        }

        private static int WorkForCandidate(RouteMotion motion, int candidate)
        {
            switch (candidate)
            {
                case 0: case 1: return motion == RouteMotion.Ground ? 20 : 1;
                case 2: case 3: case 4: return motion == RouteMotion.Ground || motion == RouteMotion.Rope ? 94 : 1;
                case 5: case 6: case 7: return motion == RouteMotion.Ground ? DescentWork : 1;
                case 8: case 9: case 10: case 11: case 33: case 34: return 10;
                case 12: case 13: return 10;
                case 14: case 15: return 16;
                case 16: case 17: case 18: case 19: case 27: return 9;
                case 20: case 22: case 23: case 24: case 26: return 32;
                case 29: case 30: return 152;
                default: return 4;
            }
        }

        private bool TryEdge(Player body, CompanionMobilitySnapshot mobility, RouteNode node, Vector2 target, int candidate,
            out RouteNode next, out bool observe, out bool pending)
        {
            next = default(RouteNode);
            observe = pending = false;
            Vector2 from = node.Position;
            int facing = target.X < from.X ? -1 : 1;
            int direction = (candidate & 1) == 0 ? facing : -facing;
            RouteStep step = new RouteStep { From = from, To = from, Direction = direction, ItemSlot = -1 };
            var motion = node.Motion;
            int fuel = node.Fuel;
            int glideFuel = node.GlideFuel;
            int gravity = body.gravDir < 0 ? -1 : 1;
            using (Scratch.Save())
            {
                switch (candidate)
                {
                    case 0: case 1:
                        if (motion != RouteMotion.Ground || !TryWalkEdge(body, from, direction, search.Terrain, out step.To)) return false;
                        step.Kind = RouteKind.Walk; step.Cost = 16;
                        break;
                    case 2: case 3: case 4:
                        if (motion != RouteMotion.Ground && motion != RouteMotion.Rope) return false;
                        if (candidate == 4) step.Direction = 0;
                        float targetHeight = (target.Y - from.Y) * gravity;
                        if (motion == RouteMotion.Ground && (step.Direction == 0 && targetHeight >= -36 ||
                            step.Direction != 0 && targetHeight >= -24 &&
                            TryWalkEdge(body, from, step.Direction, search.Terrain, out _))) return false;
                        if (!TryJumpEdge(body, mobility, from, step.Direction, node.Previous < 0, 0, search.Terrain,
                            out step.To, out step.Duration, out _)) return false;
                        step.Kind = motion == RouteMotion.Rope ? RouteKind.RopeExit : RouteKind.Jump;
                        step.Launch = new JumpInputs(body, mobility);
                        step.Cost = 32 + Vector2.Distance(step.To, from) * 1.05f;
                        motion = RouteMotion.Ground;
                        break;
                    case 5: case 6: case 7:
                        if (motion != RouteMotion.Ground) return false;
                        var launchVelocity = node.Previous < 0 && Vector2.DistanceSquared(body.position, from) <= 16 * 16 ?
                            body.velocity : Vector2.Zero;
                        var result = ProbeDescent(body, mobility, from, candidate == 5 ? 0 : candidate == 6 ? facing : -facing,
                            launchVelocity, search.Terrain, ref search.Descent, ref step, out motion);
                        pending = result == DescentResult.Pending;
                        if (result != DescentResult.Found) return false;
                        // The landing or the short controlled-air pose is
                        // confirmed from the live body before the next edge.
                        observe = true;
                        // The recorded ground contact is conditional. A
                        // suffix can count native wing refill at that contact,
                        // but publication stops on this side of the observation.
                        if (step.DescentLands && !step.DescentEntersLiquid && mobility.WingAbilities)
                            fuel = Math.Max(fuel, Math.Min(120, mobility.WingTimeMax));
                        break;
                    case 8: case 9: case 10: case 11: case 33: case 34:
                        if (!TryAirEdge(body, mobility, node, candidate, facing, search.Terrain, ref step, ref fuel, ref glideFuel, out motion)) return false;
                        observe = node.Motion == RouteMotion.Liquid && mobility.Lava && !mobility.LavaImmune;
                        break;
                    case 12: case 13:
                        if (mobility.MountType >= 0 || gravity != 1 || motion == RouteMotion.Track || motion == RouteMotion.Hooked) return false;
                        step.Vertical = candidate == 12 ? -1 : 1;
                        if (!TryRopeEdge(body, from, step.Vertical, search.Terrain, out step.To)) return false;
                        step.Direction = 0; step.Kind = RouteKind.Rope; step.Cost = 27;
                        motion = RouteMotion.Rope;
                        break;
                    case 14: case 15:
                        if (!TryRailEdge(body, mobility, motion, from, direction, target, search.Terrain, ref step)) return false;
                        motion = RouteMotion.Track;
                        observe = step.Kind == RouteKind.Cart;
                        break;
                    case 16: case 17:
                        if (!TryDoorEdge(body, from, direction, search.Terrain, out step.Operand, out step.To)) return false;
                        step.Kind = RouteKind.Door; step.Cost = 46; observe = true;
                        break;
                    case 18: case 19: case 27:
                        if (candidate == 27) direction = step.Direction = 0;
                        if (!TryHookEdge(body, mobility, node, target, direction, search.Terrain, ref step)) return false;
                        motion = RouteMotion.Hooked; observe = true;
                        break;
                    case 20:
                        if (node.Previous >= 0 || !TryPostureChange(body, mobility, motion, target, ref step)) return false;
                        // A body-size change has no speculative outgoing edges.
                        // The projected point is used only to rank this prefix.
                        observe = true;
                        break;
                    case 21:
                        if (motion != RouteMotion.Ground || !mobility.CanChangeGravity || mobility.MountType >= 0 ||
                            (target.Y - from.Y) * gravity >= -48 || !OppositeSupport(body, from, gravity, search.Terrain, out step.To)) return false;
                        step.Kind = RouteKind.FlipGravity; step.Direction = 0; step.Vertical = -1; step.Cost = 46;
                        observe = true;
                        break;
                    case 22: case 23: case 24: case 26:
                        if (node.Previous >= 0 || motion == RouteMotion.Hooked || !TryTransit(body, mobility, node, target, candidate, ref step)) return false;
                        observe = true;
                        break;
                    case 25: case 28:
                        if (node.Previous >= 0 || motion != RouteMotion.Hooked || !mobility.GrappleAttached ||
                            !TryHookRelease(body, from, target, candidate == 28 ? 0 : facing, search.Terrain, ref step)) return false;
                        observe = true;
                        break;
                    case 29: case 30:
                        step.Direction = candidate == 29 ? facing : -facing;
                        if (TryWalkEdge(body, from, step.Direction, search.Terrain, out _) ||
                            !TryJumpEdge(body, mobility, from, step.Direction, node.Previous < 0, glideFuel, search.Terrain,
                                out step.To, out step.Duration, out int glideSpend) || glideSpend == 0) return false;
                        step.Kind = RouteKind.GlideJump;
                        step.Launch = new JumpInputs(body, mobility);
                        step.Cost = 58 + Vector2.Distance(step.To, from) + glideSpend * .3f;
                        glideFuel -= glideSpend;
                        break;
                    case 31: case 32:
                        if (!TryTrapdoorEdge(body, from, candidate == 31 ? -1 : 1, search.Terrain, ref step, out motion)) return false;
                        observe = true;
                        break;
                }
            }
            step.ObserveAfter = observe;
            next = new RouteNode { Position = step.To, Motion = motion, Fuel = fuel, GlideFuel = glideFuel, Step = step, Observe = observe };
            return true;
        }

        private static bool SolidForBody(Player body, Vector2 position, ReadSet terrain, bool allowLava = false)
        {
            return Footprint(body, position, Vector2.Zero, terrain) && ClearBody(body, position) &&
                (body.lavaImmune || allowLava || !Collision.LavaCollision(position, body.width, body.height));
        }

        private static bool Swept(Player body, Vector2 from, Vector2 delta, ReadSet terrain, bool passPlatform = false,
            bool door = false, bool allowLava = false)
        {
            if (!Footprint(body, from, delta, terrain)) return false;
            var actual = Collision.TileCollision(from, delta, body.width, body.height, passPlatform, passPlatform,
                body.gravDir < 0 ? -1 : 1, ignoreDoors: door);
            return Vector2.DistanceSquared(delta, actual) < .25f && SolidForBody(body, from + delta, terrain, allowLava);
        }

        private static bool Surface(Player body, Vector2 from, ReadSet terrain, bool platforms = true) =>
            HasSurface(body, from, terrain, platforms);

        private static bool TryWalkEdge(Player body, Vector2 from, int direction, ReadSet terrain, out Vector2 to) =>
            TrySupportedTravel(body, from, direction, 16, terrain, out to, out _);

        private bool TryJumpEdge(Player body, CompanionMobilitySnapshot mobility, Vector2 from, int direction, bool root,
            int glideFuel, ReadSet terrain, out Vector2 to, out int hold, out int glideSpend)
        {
            to = from;
            glideSpend = 0;
            hold = mobility.JumpHeight > 0 ? mobility.JumpHeight : jumpHold;
            if (mobility.MountCart || mobility.Shimmer || mobility.Lava && !mobility.LavaImmune) return false;
            int gravity = body.gravDir < 0 ? -1 : 1;
            float speed = mobility.JumpSpeed > 0 ? mobility.JumpSpeed : jumpSpeed;
            var velocity = new Vector2(root ? body.velocity.X : 0, -speed * gravity);
            bool holding = true;
            bool wet = root ? mobility.Wet : WetAt(body, from);
            if (glideFuel > 0 && wet) return false;
            float worldTop = Main.topWorld + 640;
            bool useBorders = !Terraria.Testing.DebugOptions.noLimits && Main.rightWorld > Main.leftWorld + 1280;
            for (int tick = 0; tick < 90; tick++)
            {
                if (direction != 0)
                    velocity.X = MathHelper.Clamp(velocity.X + direction * Math.Max(.01f, body.runAcceleration),
                        -Math.Max(1, body.maxRunSpeed), Math.Max(1, body.maxRunSpeed));
                else velocity.X *= Math.Max(0, 1 - body.runSlowdown);
                float stepGravity = mobility.Wet && !wet ? Math.Max(body.gravity, Player.defaultGravity) : body.gravity;
                float launchSpeed = mobility.Wet && !wet ? Math.Min(speed, 5.01f) : speed;
                velocity.Y = (holding && tick < hold ? -launchSpeed :
                    Math.Min(body.maxFallSpeed, velocity.Y * gravity + Math.Max(.05f, stepGravity))) * gravity;
                if (glideFuel > 0 && tick >= hold && velocity.Y >= 0)
                    return TryGlideLanding(body, from, to, direction, glideFuel, terrain, out to, out glideSpend);
                if (!MoveJump(body, to, velocity, wet, out var step, terrain)) return false;
                if (velocity.Y * gravity < 0 && step.Velocity.Y * gravity >= 0) holding = false;
                if (step.Wall && step.Velocity.Y * gravity >= 0) return false;
                to = step.Position; velocity = step.Velocity;
                if (useBorders && to.Y < worldTop)
                {
                    // The ordinary native upper border cancels upward
                    // momentum without becoming a solid landing. Low-gravity
                    // trajectories would otherwise spend the whole horizon
                    // above the playable sky and miss the actual return.
                    if (gravity < 0 || Main.remixWorld || body.forcedGravity > 0) return false;
                    to.Y = worldTop;
                    velocity.Y = Math.Max(velocity.Y, .11f);
                }
                if (step.Ground && tick > 0)
                    return (to.Y - from.Y) * gravity <= 48 &&
                        (direction == 0 ? (to.Y - from.Y) * gravity < -12 : direction * (to.X - from.X) >= 24);
                if (wet || mobility.Wet) wet = WetAt(body, to);
            }
            return false;
        }

        private static bool TryGlideLanding(Player body, Vector2 launch, Vector2 from, int direction, int fuel,
            ReadSet terrain, out Vector2 to, out int spent)
        {
            to = from;
            spent = 0;
            // A dry carpet cancels descent, but adds no ascent. Require an open descent to
            // loaded ground before the search can choose the takeoff.
            Vector2 across = new Vector2(direction * 12, 0);
            float down = Math.Min(150, launch.Y - from.Y + 48);
            float planningSpeed = Math.Max(.1f, body.maxRunSpeed * .75f);
            int landingSteps = 30;
            for (int i = 1; i <= 18; i++)
            {
                int updates = 8 + (int)Math.Ceiling(i * 12 / planningSpeed);
                if (updates > fuel || !Swept(body, from, across, terrain)) return false;
                from += across;
                if (i < 3 || !TryGlideDescent(body, from, down, terrain, ref landingSteps, out Vector2 contact)) continue;
                if (!GlideLandingClear(body, contact, across, terrain)) continue;
                to = contact;
                spent = updates;
                return true;
            }
            return false;
        }

        private static bool GlideLandingClear(Player body, Vector2 contact, Vector2 across, ReadSet terrain)
        {
            return SolidForBody(body, contact, terrain) && Surface(body, contact, terrain) && !WetAt(body, contact) &&
                Swept(body, contact, across, terrain) && Surface(body, contact + across, terrain);
        }

        private static bool TryGlideDescent(Player body, Vector2 from, float depth, ReadSet terrain, ref int steps, out Vector2 to)
        {
            to = from;
            bool support = false;
            int left = (int)from.X / 16, right = (int)(from.X + body.width - .01f) / 16;
            int top = (int)(from.Y + body.height) / 16, bottom = (int)(from.Y + body.height + depth) / 16;
            for (int y = top; !support && y <= bottom; y++) for (int x = left; x <= right; x++)
            {
                if (!terrain.Record(x, y)) return false;
                Tile tile = Main.tile[x, y];
                if (tile.active() && !tile.inActive() && (Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type])) support = true;
            }
            if (!support) return false;
            // TileCollision only enumerates neighbors of the starting body.
            // A single long vector cannot see a ledge well below the apex.
            while (depth > 0 && steps > 0)
            {
                steps--;
                Vector2 delta = new Vector2(0, Math.Min(12, depth));
                if (!Footprint(body, to, delta, terrain)) return false;
                Vector2 actual = Collision.TileCollision(to, delta, body.width, body.height);
                to += actual;
                if (actual.Y < delta.Y) return true;
                depth -= actual.Y;
            }
            return false;
        }

        private static bool WetAt(Player body, Vector2 position)
        { return Collision.WetCollision(position, body.width, body.height); }

        private static bool MoveJump(Player body, Vector2 from, Vector2 velocity, bool wet, out Step step, ReadSet terrain)
        {
            if (!wet && body.gravDir > 0) return Move(body, from, velocity, true, true, out step, terrain);
            step = default(Step);
            if (!Footprint(body, from, velocity, terrain)) return false;
            int gravity = body.gravDir < 0 ? -1 : 1;
            Vector2 actual = Collision.TileCollision(from, velocity, body.width, body.height, gravity < 0, gravity < 0, gravity);
            // WetCollision scales unobstructed displacement, while collision
            // still receives the unscaled velocity and owns contact velocity.
            float scale = !wet || body.ignoreWater || body.merman || body.trident ? 1 : body.honeyWet ? .25f : .5f;
            var displacement = new Vector2(actual.X == velocity.X ? actual.X * scale : actual.X,
                actual.Y == velocity.Y ? actual.Y * scale : actual.Y);
            step.Position = from + displacement;
            step.Velocity = actual;
            step.Wall = Math.Abs(actual.X - velocity.X) > .1f;
            step.Ground = velocity.Y * gravity > 0 && (velocity.Y - actual.Y) * gravity > 0;
            return SolidForBody(body, step.Position, terrain);
        }

        private static bool TryAirEdge(Player body, CompanionMobilitySnapshot mobility, RouteNode node, int candidate, int facing, ReadSet terrain,
            ref RouteStep step, ref int fuel, ref int glideFuel, out RouteMotion motion)
        {
            motion = node.Motion;
            if (motion == RouteMotion.Track || motion == RouteMotion.Rope || motion == RouteMotion.Hooked) return false;
            int gravity = body.gravDir < 0 ? -1 : 1;
            bool wet = motion == RouteMotion.Liquid;
            bool ascent = candidate == 8;
            bool descent = candidate == 9 || candidate == 33 || candidate == 34;
            bool unlimited = mobility.UnlimitedWings || mobility.UnlimitedMountHover;
            bool wingPower = mobility.WingAbilities && (mobility.UnlimitedWings || fuel > 0);
            bool sustained = wingPower || mobility.RocketAvailable || mobility.MountFly;
            bool airborne = motion == RouteMotion.Air || (node.Previous < 0 && mobility.Sliding);
            bool advance = !ascent && !descent;
            step.Direction = candidate == 33 ? 0 : candidate == 11 || candidate == 34 ? -facing :
                ascent ? node.Previous < 0 && mobility.Sliding ? -Math.Sign(mobility.WallSide) : 0 : facing;
            step.Vertical = ascent ? -gravity : descent ? gravity : 0;
            step.Duration = mobility.Honey ? 24 : wet ? 16 : 9;
            bool hover = mobility.MountHover || mobility.WingHover && wingPower;
            bool glide = advance && !wet && !hover && (mobility.CarpetReady || mobility.CarpetAvailable) && glideFuel >= step.Duration;
            // Budget is a conservative reachability filter. Native consumers
            // check again and observe the actual burn/edge every update.
            if (wet && ascent && !mobility.CanSwim && !mobility.MountSwim) return false;
            if (!wet && (ascent || advance) && !unlimited && !glide &&
                (fuel < step.Duration || !sustained && mobility.ExtraJumps == 0 && !mobility.Sliding)) return false;
            if (!wet && advance && !airborne) return false;
            if (descent && !wet && !airborne) return false;
            int x = step.Direction * (descent ? 8 : 24);
            int y = ascent ? -24 * gravity : descent ? 24 * gravity : wet || hover || glide ? 0 : -8 * gravity;
            Vector2 delta = new Vector2(x, y);
            // Lava protection spends a real update each unsafe contact. Admit
            // only this short observed step while enough reserve remains.
            bool protectedLava = wet && mobility.Lava && node.Previous < 0 && mobility.LavaTime >= step.Duration + 24;
            if (!Swept(body, node.Position, delta, terrain, passPlatform: advance && !wet && gravity == 1, allowLava: protectedLava))
            {
                if (!descent || step.Direction != 0 || !FindNearSurface(body, node.Position, terrain, out step.To)) return false;
            }
            else step.To += delta;

            if (glide) glideFuel -= step.Duration;
            else if (!wet && !unlimited && !descent) fuel -= step.Duration;
            bool ground = !ascent && Surface(body, step.To, terrain, platforms: true);
            motion = ground ? RouteMotion.Ground : wet ? RouteMotion.Liquid : RouteMotion.Air;
            step.Kind = wet ? RouteKind.Swim : ascent ? RouteKind.Ascend : descent ? RouteKind.Descend : RouteKind.AirAcross;
            step.Cost = Math.Abs(x) + Math.Abs(y) + (wet ? 25 : 18);
            return true;
        }

        private static bool FindNearSurface(Player body, Vector2 from, ReadSet terrain, out Vector2 to)
        {
            Vector2 gravity = new Vector2(0, body.gravDir < 0 ? -24 : 24);
            to = from;
            if (!Footprint(body, from, gravity, terrain)) return false;
            var actual = Collision.TileCollision(from, gravity, body.width, body.height, false, false, body.gravDir < 0 ? -1 : 1);
            to = from + actual;
            return Math.Abs(actual.Y) >= 4 && Math.Abs(actual.Y) < 24 && SolidForBody(body, to, terrain);
        }

        private static bool RopeCell(int x, int y, ReadSet terrain)
        {
            if (terrain == null ? !Loaded(x, y) : !terrain.Record(x, y)) return false;
            Tile tile = Main.tile[x, y];
            if (!tile.active() || tile.inActive()) return false;
            if (Main.tileRope[tile.type]) return true;
            if (tile.type != TileID.MinecartTrack && !TileID.Sets.Platforms[tile.type] && tile.type != 380) return false;
            for (int offset = -5; offset <= 5; offset++)
                if (terrain == null ? !Loaded(x, y + offset) : !terrain.Record(x, y + offset)) return false;
            return WorldGen.IsRope(x, y);
        }

        private static bool TryRopeEdge(Player body, Vector2 from, int direction, ReadSet terrain, out Vector2 to)
        {
            int x = (int)(from.X + body.width / 2f) / 16;
            int y = (int)(from.Y - 8) / 16;
            to = new Vector2(x * 16 + 8 - body.width / 2, from.Y + direction * 16);
            return (RopeCell(x, y, terrain) || direction < 0 && RopeCell(x, y + 1, terrain)) && RopeCell(x, y + direction, terrain) &&
                Swept(body, from, to - from, terrain, passPlatform: direction > 0);
        }

        private static bool TrackCell(int x, int y, ReadSet terrain)
        {
            if (terrain == null ? !Loaded(x, y) : !terrain.Record(x, y)) return false;
            Tile tile = Main.tile[x, y];
            return tile.active() && !tile.inActive() && tile.type == TileID.MinecartTrack;
        }

        private static bool TryRailEdge(Player body, CompanionMobilitySnapshot mobility, RouteMotion motion, Vector2 from,
            int direction, Vector2 target, ReadSet terrain, ref RouteStep step)
        {
            if (mobility.MountType >= 0 && !mobility.MountCart || motion == RouteMotion.Hooked || motion == RouteMotion.Rope) return false;
            bool riding = motion == RouteMotion.Track;
            if (!riding && (!mobility.CanUseCart || Math.Abs(target.X - from.X) < 112)) return false;
            if (riding) return direction != 0 && TryRailConnection(body, from, direction, target, terrain, ref step);
            int x = (int)(from.X + body.width / 2f) / 16;
            int y = (int)(from.Y + body.height - 8) / 16;
            int preference = target.Y <= from.Y ? -1 : 1;
            for (int i = 0; i < 3; i++)
            {
                int height = i == 0 ? 0 : i == 1 ? preference : -preference;
                if (!TrackCell(x + direction, y + height, terrain)) continue;
                step.Operand = new Vector2((x + direction) * 16 + 8, (y + height) * 16 + 8);
                step.To = from + new Vector2(direction * 16, height * 16);
                if (!Swept(body, from, step.To - from, terrain, passPlatform: height >= 0) ||
                    !CompanionMobilityGeometry.TrackEntry(body, step.Operand)) continue;
                step.Kind = RouteKind.Cart;
                step.Vertical = height;
                step.Cost = 32;
                return true;
            }
            return false;
        }

        private static Vector2 RailMagnetOffset(Player body)
        {
            var settings = body.MinecartSettings;
            return new Vector2(body.width / 2 - settings.MinecartTextureWidth / 2f, body.height / 2) + settings.MagnetOffset;
        }

        private static bool TryRailConnection(Player body, Vector2 from, int direction, Vector2 target, ReadSet terrain, ref RouteStep step)
        {
            Vector2 offset = RailMagnetOffset(body);
            Vector2 magnet = from + offset;
            int x = (int)magnet.X / 16, y = (int)magnet.Y / 16;
            if (!TrackCell(x, y, terrain)) return false;
            int[] exits = direction > 0 ? railRight() : railLeft();
            int[] entrances = direction > 0 ? railLeft() : railRight();
            int[][] heights = railHeight();
            if (exits == null || entrances == null || heights == null) return false;

            Tile start = Main.tile[x, y];
            float best = float.PositiveInfinity;
            for (int branch = 0; branch < 2; branch++)
            {
                int frame = branch == 0 ? start.frameX : start.frameY;
                int sample = (int)magnet.X % 16 / 2;
                if (frame < 0 || frame >= exits.Length || heights[frame][sample] < 0 ||
                    Math.Abs(magnet.Y - (y * 16 + heights[frame][sample])) > 5) continue;
                int connection = exits[frame];
                if (connection < 0) continue;
                int nextX = x + direction;
                int nextY = y + connection - 1;
                if (!TrackCell(nextX, nextY, terrain)) continue;
                Tile next = Main.tile[nextX, nextY];
                for (int incoming = 0; incoming < 2; incoming++)
                {
                    int endFrame = incoming == 0 ? next.frameX : next.frameY;
                    if (endFrame < 0 || endFrame >= entrances.Length || entrances[endFrame] != 2 - connection || heights[endFrame][4] < 0) continue;
                    Vector2 destination = new Vector2(nextX * 16 + 8, nextY * 16 + heights[endFrame][4]) - offset;
                    float score = Estimate(destination, target);
                    if (score >= best || !Swept(body, from, destination - from, terrain, passPlatform: true)) continue;
                    best = score;
                    step.Operand = new Vector2(nextX * 16 + 8, nextY * 16 + 8);
                    step.To = destination;
                    step.Kind = RouteKind.Track;
                    // Native followUp/Down choose the lower/higher rail surface at
                    // a fork even when both tracks occupy the same tile row.
                    int alternative = incoming == 0 ? next.frameY : next.frameX;
                    int sameRow = alternative >= 0 && alternative < heights.Length ? Math.Sign(heights[endFrame][4] - heights[alternative][4]) : 0;
                    step.Vertical = connection != 1 ? connection - 1 : sameRow;
                    step.Cost = 10;
                }
            }
            return !float.IsPositiveInfinity(best);
        }

        private static bool TryHookEdge(Player body, CompanionMobilitySnapshot mobility, RouteNode node, Vector2 target,
            int direction, ReadSet terrain, ref RouteStep step)
        {
            if (node.Previous >= 0 || mobility.HookRange < 32 || mobility.GrappleAttached || mobility.GrappleInFlight ||
                mobility.MountType >= 0 || mobility.Controlled) return false;
            int originX = (int)body.Center.X / 16;
            int originY = (int)body.Center.Y / 16;
            int towardY = Math.Sign(target.Y - node.Position.Y);
            if (Math.Abs(target.Y - node.Position.Y) < 32) towardY = -(int)body.gravDir;
            // A small ray fan chooses actual material; the native outlet also
            // checks the full swept body before creating the real projectile.
            for (int i = 2; i <= 7; i++)
            {
                int tx = originX + direction * i;
                int ty = originY + towardY * (i - 1);
                if ((terrain == null ? !Loaded(tx, ty) : !terrain.Record(tx, ty)) || !Main.tile[tx, ty].active()) continue;
                Vector2 anchor = new Vector2(tx * 16 + 8, ty * 16 + 8);
                Vector2 difference = anchor - body.Center;
                float length = difference.Length();
                if (length < 36 || !CompanionMobilityGeometry.HookAnchor(body, anchor, mobility.HookType, mobility.HookRange, swept: false)) continue;
                Vector2 delta = difference * Math.Max(0, (length - body.height) / length);
                if (delta.LengthSquared() < 16 * 16 || !Swept(body, node.Position, delta, terrain)) continue;
                step.Operand = anchor; step.To += delta; step.Kind = RouteKind.Hook; step.Cost = 50 + length * .7f;
                return true;
            }
            return false;
        }

        private static bool TryHookRelease(Player body, Vector2 from, Vector2 target, int direction, ReadSet terrain, ref RouteStep step)
        {
            int gravity = (int)body.gravDir;
            int vertical = (target.Y - from.Y) * gravity < -16 ? -1 : 1;
            if (direction == 0 && Math.Abs(target.Y - from.Y) < 20) return false;
            // Native GrappleMovement uses Down+Jump to leave without the pop.
            // Up+Jump admits the normal upward release in a vertical opening.
            // Prove the matching body sweep even when neither side of the shaft fits.
            Vector2 delta = new Vector2(direction * 16, vertical * gravity * 20);
            if (!Swept(body, from, delta, terrain))
            {
                vertical = -vertical;
                delta.Y = -delta.Y;
                if (!Swept(body, from, delta, terrain)) return false;
            }
            step.Kind = RouteKind.ReleaseHook;
            step.Direction = direction;
            step.Vertical = vertical;
            step.To = from + delta;
            step.Cost = 31;
            return true;
        }

        private bool TryPostureChange(Player body, CompanionMobilitySnapshot mobility, RouteMotion motion, Vector2 target, ref RouteStep step)
        {
            if (motion == RouteMotion.Rope || motion == RouteMotion.Hooked || mobility.Controlled) return false;
            int direction = Math.Sign(target.X - body.position.X);
            if (mobility.MountType >= 0)
            {
                bool cartOnGround = mobility.MountCart && !mobility.Track && motion == RouteMotion.Ground;
                if (!mobility.CanDismount || motion != RouteMotion.Track && !cartOnGround && !(motion == RouteMotion.Ground && direction != 0 &&
                    !TryWalkEdge(body, body.position, direction, null, out _))) return false;
                if (motion == RouteMotion.Track && Math.Abs(body.velocity.X) > 1.5f) return false;
                step.Kind = RouteKind.Dismount;
            }
            else
            {
                float height = (body.position.Y - target.Y) * body.gravDir;
                // Native flight from the present form is useful before a tool
                // switch. Near exhaustion, hand over before dropping back to
                // the floor when a real remaining ascent still needs thrust.
                int remainingFlight = FlightBudget(mobility);
                bool needsMount = motion == RouteMotion.Ground ? !CanPowerAscent(mobility) :
                    motion == RouteMotion.Air && remainingFlight <= 10 && height > remainingFlight * 4 + 24;
                bool flight = mobility.FlightMountType >= 0 && body.gravDir > 0 && height > 48 && needsMount;
                bool water = motion == RouteMotion.Liquid && mobility.WaterMountType >= 0;
                bool narrow = motion == RouteMotion.Ground && mobility.NarrowMountType >= 0 && direction != 0 &&
                    !TryWalkEdge(body, body.position, direction, null, out _) && NarrowLane(body, mobility, direction);
                bool running = mobility.DefaultMountType >= 0 && motion == RouteMotion.Ground &&
                    Math.Abs(target.X - body.position.X) >= 270 && SupportedLane(body, direction, 80, steps: false, out _);
                if (mobility.HandBusy || !flight && !running && !water && !narrow ||
                    unchecked(Main.GameUpdateCount - postureChangedAt) < 90 && postureChangedAt != 0) return false;
                step.Kind = RouteKind.Mount;
                step.ItemSlot = narrow ? mobility.NarrowMountSlot : water ? mobility.WaterMountSlot : flight ? mobility.FlightMountSlot : mobility.DefaultMountSlot;
                step.MountTypePlusOne = 1 + (narrow ? mobility.NarrowMountType : water ? mobility.WaterMountType :
                    flight ? mobility.FlightMountType : mobility.DefaultMountType);
            }
            step.Direction = 0;
            step.To += new Vector2(MathHelper.Clamp(target.X - step.From.X, -32, 32), MathHelper.Clamp(target.Y - step.From.Y, -18, 18));
            step.Cost = 48;
            return step.To != step.From;
        }

        private static bool NarrowLane(Player body, CompanionMobilitySnapshot mobility, int direction)
        {
            if (body.gravDir != 1 || mobility.NarrowMountWidth <= 0 || mobility.NarrowMountHeight <= 0) return false;
            int width = mobility.NarrowMountWidth, height = mobility.NarrowMountHeight;
            Vector2 feet = body.Bottom;
            var position = feet - new Vector2(width * .5f, height);
            var step = new Vector2(direction * 12, 0);
            // Native shape data determines the hypothetical hitbox. This scan
            // holds the actual player's body, gear, and mount state untouched.
            for (int i = 0; i < 4; i++)
            {
                if (!Footprint(width, height, position, step) || Collision.SolidCollision(position, width, height) ||
                    Vector2.DistanceSquared(Collision.TileCollision(position, step, width, height), step) > .25f) return false;
                position += step;
                if (Collision.TileCollision(position, new Vector2(0, 2), width, height).Y >= 2) return false;
            }
            return true;
        }

        private static bool OppositeSupport(Player body, Vector2 from, int gravity, ReadSet terrain, out Vector2 to)
        {
            to = from;
            Vector2 delta = new Vector2(0, -gravity * 96);
            if (!Footprint(body, from, delta, terrain)) return false;
            var actual = Collision.TileCollision(from, delta, body.width, body.height, false, false, -gravity);
            to += actual;
            return Math.Abs(actual.Y) >= 32 && Math.Abs(actual.Y) < 96 && SolidForBody(body, to, terrain);
        }

        private static bool InGoalRegion(Player body, Vector2 position, Vector2 goal, ReadSet terrain)
        {
            using (Scratch.Save())
            {
                // A nearby owner at the far side of a solid wall is a distinct
                // region. Sample toward the goal without requiring an exact pose.
                var difference = goal - position;
                var sample = difference * .65f;
                if (Math.Abs(sample.Y) < 12) sample.Y = 0;
                return Swept(body, position, sample, terrain, passPlatform: true);
            }
        }

        private bool TryTransit(Player body, CompanionMobilitySnapshot mobility, RouteNode node, Vector2 target, int candidate, ref RouteStep step)
        {
            var delta = target - node.Position;
            if (delta.LengthSquared() < 160 * 160) return false;
            var center = target + new Vector2(body.width / 2f, body.height / 2f);
            if (candidate == 26)
            {
                if (!CompanionMobilityGeometry.TryTeleporter(body, center, out step.Operand, out step.To)) return false;
                step.Kind = RouteKind.Teleporter;
            }
            else if (candidate == 22)
            {
                if (!CompanionMobilityGeometry.TryPortal(body, center, out step.Operand, out step.To)) return false;
                Vector2 approach = step.Operand - body.Center;
                // Near a standing portal, leave room for native momentum to
                // cross the entry line. A visible exit alone cannot authorize
                // a route to an entrance above an obstruction.
                if (Math.Abs(approach.Y) > 38 || !Footprint(body, node.Position,
                    new Vector2(approach.X * .75f, 0), search.Terrain)) return false;
                step.Kind = RouteKind.Portal;
            }
            else if (candidate == 23)
            {
                if (!CompanionMobilityGeometry.TryPylon(body, center, out step.Operand, out step.To)) return false;
                step.Kind = RouteKind.Pylon;
            }
            else
            {
                if (!mobility.CanUseRod || mobility.HandBusy || mobility.RodSlot < 0 || delta.LengthSquared() > 500 * 500 ||
                    !SolidForBody(body, target, search.Terrain) || !Surface(body, target, search.Terrain)) return false;
                step.Kind = RouteKind.Rod;
                step.Operand = center + new Vector2(0, body.height / 2f);
                step.ItemSlot = mobility.RodSlot;
                step.To = target;
            }
            if (Estimate(step.To, target) + 60 >= Estimate(node.Position, target)) return false;
            step.Direction = 0;
            step.Cost = step.Kind == RouteKind.Rod ? 170 : 90;
            return true;
        }
    }
}
