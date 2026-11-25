using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionGroundRun
    {
        internal static bool CanDash(Player body, int direction, int dash, float goalX) =>
            TryGroundDash(body, direction, dash, goalX, out _);

        internal static bool TryGroundDash(Player body, int direction, int dash, float goalX, out float requiredGap)
        {
            requiredGap = 0f;
            float impulse = DashImpulse(dash);
            if (direction == 0 || impulse <= 0 || body.mount.Active || body.wet || body.velocity.Y != 0 ||
                !TryStopDistance(body, impulse, dash, out float braking)) return false;
            requiredGap = impulse + braking + 6f;
            return (goalX - body.Center.X) * direction >= requiredGap &&
                TryFlatLane(body, direction * requiredGap, true, trailingSupport: true);
        }

        private static float DashImpulse(int dash)
        {
            switch (dash)
            {
                case 1: case 5: return 16.9f;
                case 2: return 14.5f;
                case 3: return 21.9f;
                // Native mounted dashes also change vertical speed.
                default: return 0f;
            }
        }

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
            return action == NavigationAction.Dash ? DashTravelTicks(body, state, feet, target, false) : time;
        }

        // Admission and the terrain proof belong to the immediate/refined
        // candidate. Scoring the admitted pulse does not scan its lane again.
        internal static float DashTravelTicks(Player body, CompanionMobilitySnapshot state,
            Vector2 feet, Vector2 target, bool airborne)
        {
            float distance = Math.Abs(target.X - feet.X);
            bool hover = airborne && state.WingHover && Math.Abs(target.Y - feet.Y) <= 18f;
            float speed = CompanionMotionProfile.HorizontalSpeed(body, state, airborne, hover);
            float acceleration = CompanionMotionProfile.HorizontalAcceleration(body, state, airborne, hover);
            float impulse = DashImpulse(state.DashType);
            if (!state.DashReady || impulse <= speed) return float.PositiveInfinity;
            float projected = impulse;
            float advance = impulse;
            int ticks = 1;
            while (projected > speed && advance < distance && ticks < 48)
            {
                projected = DashDecay(projected, state.DashType, speed);
                advance += projected;
                ticks++;
            }
            float horizontal = ticks + TravelTicks(Math.Max(0, distance - advance), speed, acceleration, projected);
            float vertical = airborne ? Math.Max(0, (feet.Y - target.Y) * body.gravDir) / Math.Max(3f, state.JumpSpeed) : 0f;
            return Math.Max(horizontal, vertical) + 3f;
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

        internal static bool CanAirDash(Player body, CompanionMobilitySnapshot state, int direction, Vector2 target)
        {
            float impulse = DashImpulse(state.DashType);
            var feet = CompanionNativeContact.Feet(body);
            bool hover = state.WingHover && Math.Abs(target.Y - feet.Y) <= 18f;
            float speed = CompanionMotionProfile.HorizontalSpeed(body, state, true, hover);
            if (!state.DashReady || direction == 0 || state.Controlled || body.mount.Active || body.wet || body.pulley ||
                state.GrappleAttached || body.windPushed || body.trackBoost != 0 || impulse <= speed ||
                !CompanionMotionProfile.Wings(state) || (target.X - body.Center.X) * direction < impulse * 5f) return false;

            float runSpeed = Math.Max(body.maxRunSpeed, body.accRunSpeed);
            float vx = impulse;
            float distance = impulse;
            int ticks = 1;
            // A held forward key need not brake during the native pulse.
            // Predict the longer inertial envelope without idle friction.
            while (DashDecaying(vx, state.DashType, runSpeed) && ticks < 48)
            {
                vx = DashDecay(vx, state.DashType, runSpeed);
                distance += vx;
                ticks++;
            }
            distance += runSpeed;
            int wingCost = state.WingFastAscent ? 2 : 1;
            if (ticks >= 48 || (target.X - body.Center.X) * direction < distance ||
                !state.UnlimitedWings && state.WingTime < ticks * wingCost + 8) return false;
            // This launch requires recoverable airspace for the same body's
            // momentum. The frame can still choose lift, coast or hover.
            // Check a bounded box enclosing the pulse and the intended band.
            float gravity = body.gravDir;
            float y = (target.Y - feet.Y) * gravity;
            float peak = MathHelper.Clamp(y, -48f, 48f) * gravity;
            float verticalMomentum = body.velocity.Y * 5f;
            var start = body.position + new Vector2(Math.Min(0f, distance * direction),
                Math.Min(0f, Math.Min(peak, verticalMomentum)) - 13f);
            var end = body.BottomRight + new Vector2(Math.Max(0f, distance * direction),
                Math.Max(0f, Math.Max(peak, verticalMomentum)) + 13f);
            return AirDashLane(start, end);
        }

        private static bool AirDashLane(Vector2 start, Vector2 end)
        {
            int left = (int)Math.Floor(start.X / 16f), right = (int)Math.Floor((end.X - .01f) / 16f);
            int top = (int)Math.Floor(start.Y / 16f), bottom = (int)Math.Floor((end.Y - .01f) / 16f);
            if (right - left >= 32 || bottom - top >= 10) return false;
            for (int x = left; x <= right; x++)
                for (int y = top; y <= bottom; y++)
                {
                    if (!CompanionNativeContact.Loaded(x, y)) return false;
                    var tile = Main.tile[x, y];
                    if (tile.liquid != 0 || tile.nactive() &&
                        (Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type])) return false;
                }
            return true;
        }

        private static float DashDecay(float speed, int dash, float runSpeed)
        {
            bool strong = dash == 2 || dash == 3;
            return speed > DashThreshold(dash) ? speed * (strong ? .985f : .992f) :
                speed > runSpeed ? speed * (strong ? .94f : .96f) : runSpeed;
        }

        private static float DashThreshold(int dash) => dash == 3 ? 14f : 12f;

        private static bool DashDecaying(float speed, int dash, float runSpeed) =>
            speed > DashThreshold(dash) || speed > runSpeed;

        internal static bool HasBrakingRoom(Player body, int heading, float speed, int dash) =>
            HasBrakingRoom(body, heading, speed, dash, out _);

        internal static bool HasBrakingRoom(Player body, int heading, float speed, int dash, out bool unsupported,
            bool includeForwardStride = false)
        {
            unsupported = false;
            if (!TryStopDistance(body, speed, dash, out float distance)) return false;
            float span = distance + speed + 2f;
            if (distance < 16f)
            {
                if (includeForwardStride)
                {
                    // A held key advances before neutral friction can act.
                    // Preserve the full stride at a ledge even below 16px;
                    // a wall still uses the ordinary short-step behavior.
                    bool room = CompanionMotionAction.ClearAhead(body, heading, span, true, out unsupported);
                    if (room || unsupported) return room;
                }
                return CompanionMotionAction.ClearAhead(body, heading, Math.Max(3f, distance), true, out unsupported);
            }
            return TryFlatLane(body, heading * span, true, trailingSupport: true) ||
                dash == 0 && span <= 48f && TryStepLane(body, heading * span, 18f, out unsupported);
        }

        internal static bool CanCoastToStop(Player body, int heading, float speed)
        {
            // ResetEffects restores 0.2 before the floor flags are applied.
            // Do not spend faster friction from a surface just left behind.
            if (!TryStopDistance(body, speed, 0, out float distance, neutralStop: true)) return false;
            float span = distance + Math.Max(3f, speed);
            return TryFlatLane(body, heading * span, true, trailingSupport: true, neutralStop: true);
        }

        internal static bool TryStepLane(Player body, float delta, float heightTolerance) =>
            TryStepLane(body, delta, heightTolerance, out _);

        private static bool TryStepLane(Player body, float delta, float heightTolerance, out bool unsupported)
        {
            unsupported = false;
            if (Math.Abs(delta) > 48f) return false;
            Vector2 position = body.position;
            float remaining = Math.Abs(delta);
            int heading = Math.Sign(delta);
            var contact = CompanionNativeContact.PlatformContact.Native(body, false, false);
            while (remaining > .01f)
            {
                float step = Math.Min(remaining, 8f);
                var move = new Vector2(heading * step, Math.Max(.05f, body.gravity) * body.gravDir);
                if (!CompanionNativeContact.TryMove(body, position, move, contact, true, out var result) || result.Wall) return false;
                unsupported = !result.Ground && !CompanionNativeContact.Supported(body, result.Position);
                if (unsupported || CompanionFallRisk.OutsideModeledLiquid(body, result.Position, body.width, body.height)) return false;
                position = result.Position;
                remaining -= step;
            }
            return Math.Abs(position.Y - body.position.Y) <= heightTolerance;
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
            bool neutralStop = false)
        {
            bool upright = body.gravDir > 0;
            float supportY = body.position.Y + (upright ? body.height : 0);
            int edge = (int)Math.Round(supportY / 16);
            if (Math.Abs(edge * 16 - supportY) > .001f || body.wet) return false;
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
            if (right - left + 1 > 32 || bottom - top + 2 > 8) return false;
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
                    if (!CompanionNativeContact.Loaded(x, floor)) return false;
                    Tile tile = Main.tile[x, floor];
                    if (tile.liquid != 0 || tile.nactive() && ChangesGroundSpeed(body, tile.type)) return false;
                }
            }
            // On an aligned, unbroken slab the swept box and its support can
            // be proved from one tile rectangle. Slopes, ledges and moving
            // surfaces stay on the native per-step geometry path below.
            for (int x = left; x <= right; x++)
            {
                if (x >= supportLeft && x <= supportRight)
                {
                    if (!CompanionNativeContact.Loaded(x, floor)) return false;
                    Tile support = Main.tile[x, floor];
                    bool solid = Main.tileSolid[support.type] && !Main.tileSolidTop[support.type];
                    if (!support.active() || support.inActive() || support.slope() != 0 || support.halfBrick() ||
                        !(solid || neutralStop && TileID.Sets.Platforms[support.type]) ||
                        ChangesGroundSpeed(body, support.type) && proveBrakingSurface)
                        return false;
                }
                for (int y = top; y <= bottom; y++)
                {
                    if (!CompanionNativeContact.Loaded(x, y)) return false;
                    Tile cell = Main.tile[x, y];
                    if (cell.liquid > 0 || cell.active() && !cell.inActive() && (Main.tileSolid[cell.type] || Main.tileSolidTop[cell.type])) return false;
                }
            }
            return true;
        }

        private static bool ChangesGroundSpeed(Player body, int type)
        {
            return TileID.Sets.IceSkateSlippery[type] || type == 197 || type == 198 || type == 229 ||
                type == TileID.MinecartTrack || TileID.Sets.ConveyorDirection[type] != 0 ||
                body.desertBoots && (TileID.Sets.Conversion.Sand[type] || TileID.Sets.Conversion.Sandstone[type] ||
                    TileID.Sets.Conversion.HardenedSand[type]);
        }

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
                    if (DashDecaying(speed, dash, runSpeed)) speed = DashDecay(speed, dash, runSpeed);
                    else { speed = runSpeed; dash = 0; }
                }
                distance += speed;
            }
            return speed == 0;
        }

    }
}
