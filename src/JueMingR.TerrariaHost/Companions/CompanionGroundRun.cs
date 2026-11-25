using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum CompanionBrakingRoom { Unproved, Clear, Wall, Ledge }

    internal static class CompanionGroundRun
    {
        internal static float DirectTravelTicks(Player body, CompanionMobilitySnapshot state, NavigationAction action,
            Vector2 feet, Vector2 target)
        {
            float distance = Math.Abs(target.X - feet.X);
            int heading = Math.Sign(target.X - feet.X);
            bool air = action == NavigationAction.Flight;
            float speed = CompanionMotionProfile.HorizontalSpeed(body, state, air, air && state.WingHover);
            float acceleration = CompanionMotionProfile.HorizontalAcceleration(body, state, air, air && state.WingHover);
            float initial = body.velocity.X * heading;
            float time = TravelTicks(distance, speed, acceleration, initial);
            if (air)
            {
                float lift = Math.Max(0, (feet.Y - target.Y) * body.gravDir);
                time = Math.Max(time, lift / Math.Max(3f, state.JumpSpeed));
                if (body.velocity.Y == 0f) time += 6f;
                // Equal-time flight spends finite reserve. Hover-speed gear
                // can still win clear, long horizontal stretches.
                return time + (state.UnlimitedWings || state.UnlimitedMountHover ? 0 : 4f);
            }
            return time;
        }

        internal static float TravelTicks(float distance, float speed, float acceleration, float current)
        {
            if (distance <= 0) return 0;
            current = MathHelper.Clamp(current, -speed, speed);
            float accelerationTicks = (speed - current) / acceleration;
            float accelerationDistance = (speed + current) * .5f * accelerationTicks;
            return distance <= accelerationDistance ?
                ((float)Math.Sqrt(current * current + 2f * acceleration * distance) - current) / acceleration :
                accelerationTicks + (distance - accelerationDistance) / speed;
        }

        internal static CompanionBrakingRoom BrakingRoom(Player body, int heading, float speed, int dash,
            bool includeForwardStride = false)
        {
            if (!TryStopDistance(body, speed, dash, out float distance)) return CompanionBrakingRoom.Unproved;
            float span = distance + speed + 2f;
            if (distance < 16f)
            {
                if (includeForwardStride)
                {
                    // A held key advances before neutral friction can act.
                    // Preserve the full stride at a ledge even below 16px;
                    // a wall still uses the ordinary short-step behavior.
                    var forward = StepLane(body, heading * span, 18f, span);
                    if (forward == CompanionBrakingRoom.Clear || forward == CompanionBrakingRoom.Ledge) return forward;
                }
                return StepLane(body, heading * Math.Max(3f, distance), 18f, Math.Max(3f, distance));
            }
            var flat = FlatLane(body, heading * span, true, trailingSupport: true);
            return flat == CompanionBrakingRoom.Clear || dash != 0 ? flat :
                StepLane(body, heading * span, 18f, 48f);
        }

        internal static bool CanCoastToStop(Player body, int heading, float speed)
        {
            // ResetEffects restores 0.2 before the floor flags are applied.
            // Do not spend faster friction from a surface just left behind.
            if (!TryStopDistance(body, speed, 0, out float distance, neutralStop: true)) return false;
            float span = distance + Math.Max(3f, speed);
            return TryFlatLane(body, heading * span, true, trailingSupport: true, neutralStop: true);
        }

        internal static bool ConnectGroundTarget(Player body, Vector2 target)
        {
            const float contactTolerance = 3f;
            var feet = CompanionNativeContact.Feet(body);
            float delta = target.X - feet.X;
            // The ordinary following band can span two support levels.
            // The sweep must reach the target's actual contact height.
            return Math.Abs(target.Y - feet.Y) <= contactTolerance && TryFlatLane(body, delta, false) ||
                Math.Abs(delta) <= 48f &&
                StepLane(body, delta, 18f, 48f, target.Y, contactTolerance) == CompanionBrakingRoom.Clear;
        }

        private static CompanionBrakingRoom StepLane(Player body, float delta, float heightTolerance, float reach,
            float? goalY = null, float goalTolerance = 0f)
        {
            Vector2 position = body.position;
            float distance = Math.Abs(delta);
            float remaining = Math.Min(distance, reach);
            int heading = Math.Sign(delta);
            var contact = CompanionNativeContact.PlatformContact.Native(body, false, body.stairFall);
            while (remaining > .01f)
            {
                float step = Math.Min(remaining, 8f);
                var move = new Vector2(heading * step, Math.Max(.05f, body.gravity) * body.gravDir);
                bool moved = CompanionNativeContact.TryMove(body, position, move, contact, true, out var result);
                if (result.Known && result.Wall)
                    return CompanionBrakingRoom.Wall;
                if (!moved) return CompanionBrakingRoom.Unproved;
                if (CompanionFallRisk.OutsideModeledLiquid(body, result.Position, body.width, body.height))
                    return CompanionBrakingRoom.Wall;
                if (!result.Ground && !CompanionNativeContact.Supported(body, result.Position, !contact.IgnorePlatforms))
                    return CompanionBrakingRoom.Ledge;
                position = result.Position;
                remaining -= step;
            }
            float feetY = position.Y + (body.gravDir > 0 ? body.height : 0);
            return distance <= reach && Math.Abs(position.Y - body.position.Y) <= heightTolerance &&
                (!goalY.HasValue || Math.Abs(feetY - goalY.Value) <= goalTolerance)
                ? CompanionBrakingRoom.Clear : CompanionBrakingRoom.Unproved;
        }

        internal static bool HasShallowStepOff(Player body, int heading)
        {
            float speed = Math.Abs(body.velocity.X);
            if (heading == 0 || speed < .5f || speed > 4f || body.velocity.Y * body.gravDir > 3f) return false;
            var contact = CompanionNativeContact.PlatformContact.Native(body, false, false);
            Vector2 position = body.position;
            // Let ordinary step control reach an immediately adjacent low
            // shelf. Whole-body traversal and low support must be present;
            // this cannot authorize a deep drop or an unproved gap.
            for (int step = 0; step < 4; step++)
            {
                var take = new Vector2(heading * 8f, 0f);
                if (!CompanionNativeContact.TryMove(body, position, take, contact, false, out var across) || across.Wall) return false;
                position = across.Position;
            }
            for (int step = 0; step < 5; step++)
            {
                if (!CompanionNativeContact.TryMove(body, position, new Vector2(0, 4f * body.gravDir), contact, false, out var down)) return false;
                if (down.Ground)
                {
                    return !CompanionFallRisk.OutsideModeledLiquid(body, down.Position, body.width, body.height);
                }
                position = down.Position;
            }
            return false;
        }

        internal static bool TryFlatLane(Player body, float delta, bool proveBrakingSurface, bool trailingSupport = false,
            bool neutralStop = false) =>
            FlatLane(body, delta, proveBrakingSurface, trailingSupport, neutralStop) == CompanionBrakingRoom.Clear;

        private static CompanionBrakingRoom FlatLane(Player body, float delta, bool proveBrakingSurface, bool trailingSupport,
            bool neutralStop = false)
        {
            bool upright = body.gravDir > 0;
            float supportY = body.position.Y + (upright ? body.height : 0);
            int edge = (int)Math.Round(supportY / 16);
            if (Math.Abs(edge * 16 - supportY) > .001f || body.wet) return CompanionBrakingRoom.Unproved;
            int floor = upright ? edge : edge - 1;
            int top = upright ? (int)Math.Floor(body.position.Y / 16) : edge;
            int bottom = upright ? edge - 1 : (int)Math.Floor((body.position.Y + body.height - .01f) / 16);
            int left = (int)Math.Floor((body.position.X + Math.Min(0, delta)) / 16);
            int right = (int)Math.Floor((body.position.X + Math.Max(0, delta) + body.width - .01f) / 16);
            int supportLeft = left, supportRight = right;
            if (trailingSupport)
            {
                // The original floor collision holds a body that still has
                // trailing-foot overlap at its proven stop. The swept body
                // itself still needs clearance all the way to its front edge.
                float contactedAtStart = body.position.X + (delta > 0 ? body.width - .01f : .01f);
                float contactedAtStop = body.position.X + delta + (delta > 0 ? .01f : body.width - .01f);
                supportLeft = (int)Math.Floor(Math.Min(contactedAtStart, contactedAtStop) / 16f);
                supportRight = (int)Math.Floor(Math.Max(contactedAtStart, contactedAtStop) / 16f);
            }
            // Final pre-input revalidation is one bounded runway scan. An
            // unusually fast body beyond this range may brake, never launch.
            if (right - left + 1 > 32 || bottom - top + 2 > 8) return CompanionBrakingRoom.Unproved;
            if (neutralStop)
            {
                // FloorVisuals may select either neighbor of the foot
                // center. Check those and the whole swept foot interval,
                // including support that may apply conveyor motion.
                float center = body.position.X + body.width / 2;
                int firstContact = Math.Min(left, (int)Math.Floor((center + Math.Min(0, delta)) / 16f) - 1);
                int lastContact = Math.Max(right, (int)Math.Floor((center + Math.Max(0, delta)) / 16f) + 1);
                for (int x = firstContact; x <= lastContact; x++)
                {
                    if (!CompanionNativeContact.Loaded(x, floor)) return CompanionBrakingRoom.Unproved;
                    Tile tile = Main.tile[x, floor];
                    if (tile.liquid != 0 || tile.nactive() && ChangesGroundSpeed(body, tile.type))
                        return CompanionBrakingRoom.Unproved;
                }
            }
            var contact = CompanionNativeContact.PlatformContact.Native(body, false, body.stairFall);
            // Aligned platform tops are continuous support with the native
            // hold input. Stair/platform shapes still use native contacts.
            for (int x = left; x <= right; x++)
            {
                if (x >= supportLeft && x <= supportRight)
                {
                    if (!CompanionNativeContact.Loaded(x, floor)) return CompanionBrakingRoom.Unproved;
                    Tile support = Main.tile[x, floor];
                    bool solid = Main.tileSolid[support.type] && !Main.tileSolidTop[support.type];
                    bool platform = AlignedPlatform(support, contact);
                    if (!support.nactive() || support.slope() != 0 || support.halfBrick() ||
                        !(solid || platform) || support.liquid != 0 ||
                        ChangesGroundSpeed(body, support.type) && proveBrakingSurface)
                        return CompanionBrakingRoom.Unproved;
                }
                for (int y = top; y <= bottom; y++)
                {
                    if (!CompanionNativeContact.Loaded(x, y)) return CompanionBrakingRoom.Unproved;
                    Tile cell = Main.tile[x, y];
                    if (cell.liquid > 0 || cell.nactive() && (Main.tileSolid[cell.type] || Main.tileSolidTop[cell.type]))
                        return CompanionBrakingRoom.Unproved;
                }
            }
            return CompanionBrakingRoom.Clear;
        }

        internal static bool ChangesGroundSpeed(Player body, int type)
        {
            return TileID.Sets.IceSkateSlippery[type] || type == 197 || type == 198 || type == 229 ||
                type == TileID.MinecartTrack || TileID.Sets.ConveyorDirection[type] != 0 ||
                body.desertBoots && (TileID.Sets.Conversion.Sand[type] || TileID.Sets.Conversion.Sandstone[type] ||
                    TileID.Sets.Conversion.HardenedSand[type]);
        }

        internal static bool AlignedPlatform(Tile tile, CompanionNativeContact.PlatformContact contact) =>
            !contact.IgnorePlatforms && !contact.StairFall && TileID.Sets.Platforms[tile.type] &&
            WorldGen.PlatformProperTopFrame(tile.frameX);

        private static bool TryStopDistance(Player body, float speed, int dash, out float distance, bool neutralStop = false)
        {
            distance = 0;
            float slowdown = neutralStop ? Math.Min(.2f, body.runSlowdown) : body.runSlowdown;
            if (!(slowdown > 0) || body.windPushed || body.trackBoost != 0 || float.IsInfinity(speed) || float.IsNaN(speed)) return false;
            float runSpeed = Math.Max(body.accRunSpeed, body.maxRunSpeed);
            // HorizontalMovement applies the neutral ground friction before
            // DashMovement damps overspeed or snaps back to ordinary running.
            // Failure to prove a stop inside this window never admits a dash.
            for (int tick = 0; speed > 0 && tick < 96; tick++)
            {
#if DEBUG
                CompanionNativeContact.RecordBrakingTick();
#endif
                speed = Math.Max(0, speed - slowdown);
                if (dash > 0 && speed > 0)
                {
                    if (speed > (dash == 3 ? 14f : 12f) || speed > runSpeed) speed = CompanionDashMotion.Decay(speed, dash, runSpeed);
                    else { speed = runSpeed; dash = 0; }
                }
                distance += speed;
            }
            return speed == 0;
        }

    }
}
