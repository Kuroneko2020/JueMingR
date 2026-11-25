using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionDashMotion
    {
        internal static int Source(Player body)
        {
            int mount = body.mount.Active ? body.mount.Type : -1;
            // CanDash allows equipped sources to operate while mounted. Only
            // the four Chillets replace the equipped source in DashMovement.
            if (mount >= MountID.Chillet && mount <= MountID.TrustyChilletIgnis) return 6;
            if (mount >= 0 && !MountID.Sets.CanDash[mount]) return 0;
            switch (body.dashType)
            {
                case 1: case 2: case 5: return body.dashType;
                case 3: return body.solarShields > 0 || body.dashDelay < 0 ? 3 : 0;
                // Native type 4 has an active tail but no acquisition or start.
                default: return 0;
            }
        }

        internal static float Impulse(int source)
        {
            switch (source)
            {
                case 1: case 5: return 16.9f;
                case 2: return 14.5f;
                case 3: return 21.9f;
                case 6: return 16f;
                default: return 0f;
            }
        }

        internal static float Decay(float speed, int source, float runSpeed)
        {
            bool strong = source == 2 || source == 3 || source == 4;
            return speed > (source == 3 ? 14f : 12f) ? speed * (strong ? .985f : .992f) :
                speed > runSpeed ? speed * (strong ? .94f : .96f) : runSpeed;
        }

        internal readonly struct Physics
        {
            internal readonly float Run, Acceleration, Slowdown;
            private readonly float launchSpeed;
            internal readonly int Mount;

            internal Physics(Player body, CompanionMobilitySnapshot state, bool airborne)
            {
                Mount = body.mount.Active ? body.mount.Type : -1;
                // Wing run bonuses are from the last aerial frame. Use the
                // greatest native exit cap and least input braking available
                // across coast, hold and hover when proving the envelope.
                Run = Math.Max(body.maxRunSpeed, body.accRunSpeed);
                Acceleration = body.runAcceleration;
                Slowdown = body.runSlowdown;
                if (airborne && state.WingAbilities && state.WingBaseAcceleration > 0)
                {
                    Run = Math.Max(Run, Math.Max(state.WingSpeed, state.WingHoverRun ? state.WingHoverSpeed : 0f));
                    Acceleration = Math.Min(Acceleration, Math.Min(state.WingBaseAcceleration, state.WingAcceleration));
                    if (state.WingHoverRun) Acceleration = Math.Min(Acceleration, state.WingHoverAcceleration);
                    Slowdown = Math.Min(Slowdown, state.WingBaseSlowdown);
                }
                launchSpeed = Math.Max(body.maxRunSpeed, body.accRunSpeed);
                if (airborne && state.WingAbilities && state.WingBaseSpeed > 0)
                    launchSpeed = Math.Max(body.maxRunSpeed, state.WingBaseSpeed);
            }

            internal bool Same(Physics other) => Run == other.Run && Acceleration == other.Acceleration &&
                Slowdown == other.Slowdown && Mount == other.Mount &&
                (Mount < 0 || !MountID.Sets.IsRollerSkates[Mount] || launchSpeed == other.launchSpeed);

            internal float Brake(float speed)
            {
                float drag = Math.Max(0f, Slowdown);
                if (Mount >= 0 && MountID.Sets.IsRollerSkates[Mount])
                    drag = Math.Min(drag, Math.Max(0f, speed - launchSpeed));
                return Math.Max(0f, speed - Acceleration - (speed > drag ? drag : 0f));
            }
        }

        private struct Stop
        {
            internal float Speed, Distance;
            internal int Source, Ticks;

            internal void Step(Physics physics)
            {
#if DEBUG
                CompanionNativeContact.RecordBrakingTick();
#endif
                Speed = physics.Brake(Speed);
                if (Source != 0)
                {
                    // HorizontalMovement runs before DashMovement. The
                    // latter can snap back UP to ordinary speed at exit.
                    bool exits = Speed <= physics.Run && Speed <= (Source == 3 ? 14f : 12f);
                    Speed = Decay(Speed, Source, physics.Run);
                    if (exits)
                        Source = 0;
                }
                Distance += Speed;
                Ticks++;
            }
        }

        private static bool Eligible(Player body, CompanionMobilitySnapshot state, int direction) =>
            direction != 0 && state.DashReady && Source(body) == state.DashType && !state.Controlled &&
            !state.Wet && !state.Pulley && !state.GrappleAttached && !state.GrappleInFlight &&
            !body.windPushed && body.trackBoost == 0f;

        private static float BrakeDistance(Physics physics, float speed, int source)
        {
            // Future animation can lock the mount's facing. Only the
            // immediate reverse input may use Chillet's faster native exit.
            var stop = new Stop { Speed = speed, Source = source };
            // The tail ends once X is inside the ordinary run cap. This is
            // a bounded immediate computation only for a pulse in progress;
            // the admission forecast uses the shared, resumable cursor.
            for (int i = 0; stop.Source != 0 && i < 96; i++) stop.Step(physics);
            return stop.Source == 0 ? stop.Distance + NormalStop(physics, stop.Speed) : float.PositiveInfinity;
        }

        private static bool CanTurn(Player body) => (body.itemAnimation == 0 || body.inventory[body.selectedItem].useTurn) &&
            body.mount.AllowDirectionChange && !body.sandStorm;

        private static float NormalStop(Physics physics, float speed)
        {
            // At worst the remaining opposite-input frames spend only the
            // acceleration term. Use the full known friction on ordinary
            // terrain; skates clamp it to zero near their running speed.
            if (!(physics.Acceleration > 0f)) return float.PositiveInfinity;
            float drag = physics.Mount >= 0 && MountID.Sets.IsRollerSkates[physics.Mount] ? 0f : physics.Slowdown;
            float force = Math.Max(.001f, physics.Acceleration + drag);
            int ticks = Math.Max(0, (int)Math.Ceiling((speed - drag) / force) - 1);
            float tail = Math.Max(0f, speed - ticks * force);
            int tailTicks = Math.Max(0, (int)Math.Ceiling(tail / physics.Acceleration) - 1);
            return ticks * speed - force * ticks * (ticks + 1) * .5f +
                tailTicks * tail - physics.Acceleration * tailTicks * (tailTicks + 1) * .5f;
        }

        internal readonly struct Plan
        {
            private readonly float end, minimum, pulseDistance;
            private readonly int direction, source, width, height, top, bottom, floor, gravity, pulseTicks, reserveTicks;
            private readonly bool supported;
            private readonly Physics physics;
            internal Plan(Player body, Physics physics, int direction, int source, bool supported, float end,
                float minimum, int top, int bottom, int floor, float pulseDistance, int pulseTicks)
            {
                this.physics = physics;
                this.direction = direction;
                this.source = source;
                this.supported = supported;
                this.end = end;
                this.minimum = minimum;
                this.top = top;
                this.bottom = bottom;
                this.floor = floor;
                gravity = (int)body.gravDir;
                this.pulseDistance = pulseDistance;
                this.pulseTicks = pulseTicks;
                reserveTicks = pulseTicks + 7;
                width = body.width;
                height = body.height;
            }

            internal float TravelTicks(Player body, CompanionMobilitySnapshot state, Vector2 feet, Vector2 target, bool airborne)
            {
                bool hover = airborne && state.WingHover && Math.Abs(target.Y - feet.Y) <= 18f;
                float speed = CompanionMotionProfile.HorizontalSpeed(body, state, airborne, hover);
                if (direction == 0 || !state.DashReady || Impulse(state.DashType) <= speed) return float.PositiveInfinity;
                // Rank the pulse already proved by preparation. Progress
                // after its overspeed tail belongs to the ordinary gait.
                float distance = Math.Abs(target.X - feet.X);
                float horizontal = pulseTicks + Math.Max(0f, distance - pulseDistance) / Math.Max(1f, speed);
                float vertical = airborne ? Math.Max(0f, (feet.Y - target.Y) * body.gravDir) /
                    Math.Max(3f, state.JumpSpeed) : 0f;
                return Math.Max(horizontal, vertical) + 3f;
            }

            internal bool CanLaunch(Player body, CompanionMobilitySnapshot state, bool onGround, int heading, float targetX,
                bool recheck, bool down = false)
            {
                float x = body.Center.X;
                if (heading != direction || supported != onGround || state.DashType != source || body.width != width ||
                    body.height != height || body.gravDir != gravity || !Eligible(body, state, heading) ||
                    !physics.Same(new Physics(body, state, !onGround)) ||
                    body.velocity.X * heading >= Impulse(source) ||
                    (targetX - x) * direction < minimum || (end - x) * direction < minimum ||
                    floor < 0 && FlightReserve(state) < reserveTicks ||
                    (int)Math.Floor(body.position.Y / 16f) < top ||
                    (int)Math.Floor((body.position.Y + height - .01f) / 16f) > bottom)
                    return false;
                if (!recheck) return true;
                // Native tiles can change after the earlier slice. Rescan
                // just the committed stop envelope before the hotkey edge.
                return LaneCurrent(body, direction, (end - x) * direction) &&
                    (!down || StepClear(body, heading * Impulse(source), onGround, false, true));
            }

            internal bool LandsOn(float y) => floor >= 0 && source != 6 &&
                Math.Abs(y - (floor + (gravity < 0 ? 1 : 0)) * 16f) <= 3f;

            internal int ActiveInput(Player body, CompanionMobilitySnapshot state, bool supported, float targetX, bool yield, bool down)
            {
                int momentum = Math.Sign(body.velocity.X);
                if (momentum == 0) return 0;
                var live = new Physics(body, state, !supported);
                float speed = Math.Abs(body.velocity.X);
                int active = state.DashDelay < 0 ? state.DashKind : 0;
                // The shield's contact tail can outlast horizontal dash
                // damping. Let native friction settle the last fraction
                // instead of reversing it on every cooldown update.
                if (active == 0 && speed <= live.Slowdown * (supported ? 1f : .5f)) return 0;
                float room = Math.Min((targetX - body.Center.X) * momentum, (end - body.Center.X) * momentum);
                bool keepLane = !yield && momentum == direction && source == state.DashKind &&
                    !state.Controlled && !state.Wet && live.Mount == physics.Mount && body.gravDir == gravity &&
                    (floor >= 0 || FlightReserve(state) > 0f) &&
                    (int)Math.Floor(body.position.Y / 16f) >= top &&
                    (int)Math.Floor((body.Bottom.Y - .01f) / 16f) <= bottom;
                if (keepLane)
                {
                    float stopAfterStride = speed + (active != 0 ?
                        BrakeDistance(live, Decay(speed, active, live.Run), active) : NormalStop(live, speed));
                    // Proof below uses current tiles: a removed block or
                    // built wall must interrupt the still-accelerating
                    // choice before it reaches the final native step.
                    if (room > stopAfterStride && LaneCurrent(body, momentum, (end - body.Center.X) * momentum) &&
                        StepClear(body, momentum * speed, supported, true, down)) return momentum;
                }

                float braking = live.Brake(speed);
                bool mountBrake = state.DashKind == 6 && CanTurn(body);
                if (active != 0) braking = Decay(braking * (mountBrake ? .3f : 1f), active, live.Run);
                float step = braking > 0f ? momentum * braking : -momentum * live.Acceleration;
                if (StepClear(body, step, supported, false, down) || step * momentum > 0f)
                {
                    // If the incoming sweep still hits an obstacle, the
                    // reducing key minimizes that same native contact. Only
                    // a true reversal requires clearance behind the body.
                    return -momentum;
                }
                return 0;
            }

            private bool LaneCurrent(Player body, int momentum, float distance)
            {
                int first = FirstColumn(body, momentum);
                int last = LastColumn(body, momentum, body.Center.X + momentum * distance);
                for (int column = first; (last - column) * momentum >= 0; column += momentum)
                    if (!Column(body, column, top, bottom, floor, out _)) return false;
                return true;
            }
        }

        internal sealed class Preparation
        {
            private Physics physics;
            private Stop stop;
            private int source, direction, width, height, top, bottom, floor, gravity, first, cursor;
            private bool supported, reserve, hasEnvelope;
            private float minimum;

            internal void Reset()
            {
                direction = 0;
                minimum = 0f;
                hasEnvelope = false;
            }

            internal bool Advance(Player body, CompanionMobilitySnapshot state, bool onGround, int heading, Vector2 target,
                NavigationBudget budget, out Plan plan, out bool rejected)
            {
                plan = default;
                rejected = false;
                if (!Eligible(body, state, heading)) return false;
                var livePhysics = new Physics(body, state, !onGround);
                float impulse = Impulse(state.DashType);
                if (!(livePhysics.Acceleration > 0f) || impulse <= Math.Abs(body.velocity.X)) return false;
                bool flying = !onGround && (CompanionMotionProfile.Wings(state) || CompanionMotionProfile.MountFlight(state));
                if (heading != direction || state.DashType != source || width != body.width || height != body.height ||
                    !physics.Same(livePhysics) || onGround != supported || reserve != flying || body.gravDir != gravity)
                {
                    Reset();
                    direction = heading;
                    source = state.DashType;
                    width = body.width;
                    height = body.height;
                    gravity = (int)body.gravDir;
                    physics = livePhysics;
                    supported = onGround;
                    reserve = flying;
                    stop = new Stop { Speed = impulse, Distance = impulse, Source = source };
                }

                // Slice the actual predicted updates. Moving owner and
                // follower positions leave this physical stop model reusable.
                while (stop.Source != 0)
                {
                    if (!budget.TryPredict()) return false;
                    stop.Step(physics);
                    if (stop.Distance > 4096f) { rejected = true; return false; }
                }
                minimum = stop.Distance + NormalStop(physics, stop.Speed) + 8f;
                float gap = (target.X - body.Center.X) * heading;
                if (gap < minimum) return false;

                if (!hasEnvelope || !Fits(body))
                {
                    if (!budget.TryPredict()) return false;
                    int? surface = FindFloor(body, onGround, reserve, budget, out bool known);
                    if (!surface.HasValue) { rejected = known; return false; }
                    floor = surface.Value;
                    // Native Chillet start rises before gravity; an ordinarily
                    // airborne body keeps its launch-height headroom.
                    float speedY = body.velocity.Y * gravity;
                    float up = source == 6 || !onGround ? Math.Max(48f, Math.Min(96f, -speedY * 12f)) : 0f;
                    float down = Math.Max(48f, Math.Min(96f, speedY * 8f));
                    top = floor >= 0 && gravity < 0 ? floor + 1 :
                        (int)Math.Floor((body.position.Y - (gravity > 0 ? up : down)) / 16f);
                    bottom = floor >= 0 && gravity > 0 ? floor - 1 :
                        (int)Math.Floor((body.Bottom.Y + (gravity > 0 ? down : up) - .01f) / 16f);
                    cursor = first = FirstColumn(body, heading);
                    hasEnvelope = true;
                }
                int current = FirstColumn(body, heading);
                int reserveTicks = stop.Ticks + 8;
                if (floor < 0 && FlightReserve(state) < reserveTicks) return false;
                if ((first - current) * heading > 0 || (current - cursor) * heading > 0)
                    cursor = first = current;
                int needed = LastColumn(body, heading, body.Center.X + minimum * heading);
                int preferred = LastColumn(body, heading, body.Center.X + Math.Min(gap, minimum + 96f) * heading);
                while ((preferred - cursor) * heading >= 0)
                {
                    int cells = bottom - top + 1 + (floor >= 0 ? 1 : 0);
                    if (!budget.TryPredict() || !budget.TryTiles(cells)) break;
                    if (!Column(body, cursor, top, bottom, floor, out bool known))
                    {
                        if (known && (needed - cursor) * heading >= 0) { rejected = true; hasEnvelope = false; }
                        break;
                    }
                    cursor += heading;
                }
                if ((cursor - needed) * heading <= 0) return false;
                float end = (cursor + (heading < 0 ? 1 : 0)) * 16f - heading * width * .5f;
                plan = new Plan(body, physics, heading, source, supported, end, minimum, top, bottom, floor,
                    stop.Distance, stop.Ticks + 1);
                return true;
            }

            private bool Fits(Player body) => (int)Math.Floor(body.position.Y / 16f) >= top &&
                (int)Math.Floor((body.Bottom.Y - .01f) / 16f) <= bottom &&
                (!supported || Math.Abs(CompanionNativeContact.Feet(body).Y -
                    (floor + (gravity < 0 ? 1 : 0)) * 16f) < .02f);
        }

        private static float FlightReserve(CompanionMobilitySnapshot state)
        {
            if (state.UnlimitedWings || state.UnlimitedMountHover) return float.PositiveInfinity;
            float wing = state.WingAbilities ? state.WingTime / (state.WingFastAscent ? 2 : 1) : 0f;
            float mount = state.MountFly ? state.MountFlightTime : 0f;
            return Math.Max(wing, mount);
        }

        private static int FirstColumn(Player body, int direction) => (int)Math.Floor(
            (direction > 0 ? body.position.X : body.position.X + body.width - .01f) / 16f);

        private static int LastColumn(Player body, int direction, float end) =>
            (int)Math.Floor((end + direction * body.width * .5f - (direction > 0 ? .01f : 0f)) / 16f);

        private static int? FindFloor(Player body, bool supported, bool reserve, NavigationBudget budget, out bool known)
        {
            known = true;
            Vector2 feet = CompanionNativeContact.Feet(body);
            int gravity = (int)body.gravDir;
            if (supported)
            {
                int row = (int)Math.Round(feet.Y / 16f);
                return Math.Abs(feet.Y - row * 16f) < .02f ? row - (gravity < 0 ? 1 : 0) : (int?)null;
            }
            int column = (int)Math.Floor(body.Center.X / 16f);
            int start = (int)Math.Floor((feet.Y - gravity * .01f) / 16f) + gravity;
            for (int i = 0; i < 10; i++)
            {
                int row = start + i * gravity;
                if (!budget.TryTiles() || !CompanionNativeContact.Loaded(column, row))
                {
                    known = false;
                    return null;
                }
                Tile tile = Main.tile[column, row];
                if (tile.liquid != 0) return null;
                if (!tile.nactive() || !Main.tileSolid[tile.type] && !Main.tileSolidTop[tile.type]) continue;
                return Support(body, tile) && CompanionFallRisk.CanLand(body, feet,
                    new Vector2(body.Center.X, (row + (gravity < 0 ? 1 : 0)) * 16f), false) ? row : (int?)null;
            }
            return reserve ? -1 : (int?)null;
        }

        private static bool Support(Player body, Tile tile)
        {
            if (tile.liquid != 0 || !tile.nactive() || tile.halfBrick() || tile.slope() != 0) return false;
            if (Main.tileSolid[tile.type] && !Main.tileSolidTop[tile.type])
                return !CompanionGroundRun.ChangesGroundSpeed(body, tile.type);
            var contact = CompanionNativeContact.PlatformContact.Native(body, false, body.stairFall);
            return CompanionGroundRun.AlignedPlatform(tile, contact);
        }

        private static bool Column(Player body, int column, int top, int bottom, int floor, out bool known)
        {
            known = true;
            if (floor >= 0)
            {
                known = CompanionNativeContact.Loaded(column, floor);
                if (!known || !Support(body, Main.tile[column, floor])) return false;
            }
            for (int row = top; row <= bottom; row++)
            {
                known = CompanionNativeContact.Loaded(column, row);
                if (!known) return false;
                Tile tile = Main.tile[column, row];
                if (tile.liquid != 0 || tile.nactive() && (Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type]))
                    return false;
            }
            return true;
        }

        private static bool StepClear(Player body, float dx, bool supported, bool keepSupport, bool down)
        {
            float dy = (supported ? body.gravity : MathHelper.Clamp(body.velocity.Y * body.gravDir + body.gravity,
                -16f, body.maxFallSpeed)) * body.gravDir;
            var platform = CompanionNativeContact.PlatformContact.Native(body, down, body.stairFall);
            return CompanionNativeContact.TryMove(body, body.position, new Vector2(dx, dy), platform, supported, out var step) &&
                !step.Wall && (!keepSupport || !supported || step.Ground) &&
                !CompanionFallRisk.OutsideModeledLiquid(body, step.Position, body.width, body.height);
        }
    }
}
