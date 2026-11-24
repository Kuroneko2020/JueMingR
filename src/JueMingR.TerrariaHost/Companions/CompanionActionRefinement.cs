using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum CompanionRefinementResult { Pending, Ready, Failed }

    internal readonly struct CompanionMotionWitness
    {
        internal readonly Vector2 Position, Velocity;
        internal readonly bool Grounded, Valid;
        internal readonly int FlightTicks, RemainingLife;

        internal CompanionMotionWitness(Vector2 position, Vector2 velocity, bool grounded, int flightTicks, int life)
        { Position = position; Velocity = velocity; Grounded = grounded; FlightTicks = flightTicks; RemainingLife = life; Valid = true; }
    }

    // Refine the head of a corridor only. Prediction carries the actual
    // launch velocity and native contact from sample to sample across slices.
    internal sealed class CompanionActionRefinement
    {
        private int ticks, hookType, hookPullStep, ledgeProbeOffset, runwaySteps, flightTicks, life;
        private Vector2 position, velocity;
        private bool grounded, continuing, dropPrepared, facilityPrepared, runwayReady, checkedFlightEnd, clearedFlightBank;
        private CompanionFallRisk.ReleaseCorridor releaseCorridor;
        internal NavigationTransition Edge { get; private set; }
        internal bool Active { get; private set; }
        internal Vector2 Entry { get; private set; }
        internal CompanionMotionWitness Witness { get; private set; }
        internal int EntryLife => life;
        internal int EntryProtection { get; private set; }
        internal bool FlightLanding { get; private set; }
        internal CompanionFallRisk.ReleaseCorridor FallRelease => releaseCorridor;
        internal float FallOutletX { get; private set; }

        internal void Begin(Player body, NavigationTransition edge, bool supported)
        {
            Edge = edge;
            Entry = edge.From;
            Active = true;
            ticks = 0;
            hookType = hookPullStep = ledgeProbeOffset = runwaySteps = 0;
            flightTicks = -1;
            life = body.statLife;
            EntryProtection = CompanionFallRisk.ProtectionKey(body);
            continuing = false;
            dropPrepared = facilityPrepared = false;
            runwayReady = false;
            checkedFlightEnd = clearedFlightBank = FlightLanding = false;
            releaseCorridor = default;
            FallOutletX = edge.To.X;
            Witness = default;
            Vector2 actual = CompanionNativeContact.Feet(body);
            bool runningJump = edge.Detail == NavigationTraversalDetail.RunningJump;
            bool near = CompanionMotionAction.Near(actual, edge.From, runningJump ? 26f : 10f, 14f);
            position = near ? body.position : CompanionNativeContact.TopLeft(body, edge.From);
            velocity = near ? body.velocity : Vector2.Zero;
            grounded = supported;
        }

        internal bool BeginContinuation(Player body, NavigationTransition edge, CompanionMotionWitness previous)
        {
            var expected = CompanionNativeContact.TopLeft(body, edge.From);
            if (!previous.Valid || !CompanionMotionAction.Near(previous.Position, expected, 28f, 20f)) return false;
            Edge = edge;
            Entry = edge.From;
            Active = true;
            continuing = true;
            ticks = hookType = hookPullStep = ledgeProbeOffset = runwaySteps = 0;
            position = previous.Position;
            velocity = previous.Velocity;
            grounded = previous.Grounded;
            flightTicks = previous.FlightTicks;
            life = previous.RemainingLife;
            EntryProtection = CompanionFallRisk.ProtectionKey(body);
            dropPrepared = facilityPrepared = false;
            runwayReady = false;
            checkedFlightEnd = clearedFlightBank = FlightLanding = false;
            releaseCorridor = default;
            FallOutletX = edge.To.X;
            Witness = default;
            return true;
        }

        internal void Reset() => Active = false;

        internal CompanionRefinementResult Advance(Player body, CompanionMobilitySnapshot state, NavigationBudget budget)
        {
            if (flightTicks < 0) flightTicks = CompanionMotionProfile.Read(body, state, grounded).FlightTicks;
            if (Edge.Kind == NavigationAction.Drop || Edge.Kind == NavigationAction.PlatformDrop)
                return RefineDrop(body, state, budget);
            if (Edge.Kind == NavigationAction.Hook)
            {
                var center = position + body.Size * .5f;
                var anchor = CompanionMotionAction.TileCenter(Edge.ExitTile);
                float distance = Vector2.Distance(center, anchor);
                if (state.HookRange < distance || state.HookType <= 0) return CompanionRefinementResult.Failed;
                if (hookType != state.HookType)
                {
                    if (!budget.TryTiles(1)) return CompanionRefinementResult.Pending;
                    if (!CompanionMobilityGeometry.HookAnchorAt(body, center, anchor, state.HookType, state.HookRange, swept: false))
                        return CompanionRefinementResult.Failed;
                    hookType = state.HookType;
                    ticks = hookPullStep = 0;
                }
                // Each segment pays its own work. A long hook stays pending
                // across small slices; the hand stage rechecks the real ray.
                while (ticks * 6f < distance)
                {
                    if (!budget.TryTiles(20) || !budget.TryPredict()) return CompanionRefinementResult.Pending;
                    if (!CompanionMobilityGeometry.HookSweepStep(body, center, anchor, hookType, distance, ticks * 6f))
                        return CompanionRefinementResult.Failed;
                    ticks++;
                }
                if (continuing)
                {
                    var pullExit = CompanionNativeContact.TopLeft(body, Edge.To);
                    // A projectile can reach an anchor through a slot that
                    // rejects a player's shoulders. The continuation must
                    // prove the entire body follows the already-proved ray.
                    int steps = Math.Max(1, (int)Math.Ceiling(Vector2.Distance(position, pullExit) / 6f));
                    var contact = CompanionNativeContact.PlatformContact.Native(body, false, false);
                    while (hookPullStep < steps)
                    {
                        if (!budget.TryPredict()) return CompanionRefinementResult.Pending;
                        var from = Vector2.Lerp(position, pullExit, hookPullStep / (float)steps);
                        var to = Vector2.Lerp(position, pullExit, (hookPullStep + 1) / (float)steps);
                        if (!CompanionNativeContact.TryMove(body, from, to - from, contact, false, out var step) ||
                            Vector2.DistanceSquared(to, step.Position) > 1f || Hazard(body, step.Position))
                            return CompanionRefinementResult.Failed;
                        hookPullStep++;
                    }
                    return Ready(body, state, pullExit, Vector2.Zero, false);
                }
                return CompanionRefinementResult.Ready;
            }
            if (Edge.Kind == NavigationAction.Hoik)
            {
                if (!budget.TryPredict() || !budget.TryTiles(40)) return CompanionRefinementResult.Pending;
                return CompanionHoikContact.Matches(body, Edge.From, Edge.To) ? CompanionRefinementResult.Ready : CompanionRefinementResult.Failed;
            }
            if (Edge.Kind == NavigationAction.Door) return RefineDoor(body, state, budget);
            if (Edge.Kind == NavigationAction.Facility) return RefineFacility(body, state, budget);
            if (Edge.Kind != NavigationAction.Jump && Edge.Kind != NavigationAction.Flight)
            {
                if (continuing && (Edge.Kind == NavigationAction.Walk || Edge.Kind == NavigationAction.Dash))
                    return RefineGroundContinuation(body, state, budget);
                if (continuing && !grounded && (Edge.Kind == NavigationAction.Mount || Edge.Kind == NavigationAction.Dismount))
                    return CompanionRefinementResult.Failed;
                if (!budget.TryPredict()) return CompanionRefinementResult.Pending;
                return CompanionRefinementResult.Ready;
            }
            return RefineAscent(body, state, budget, Edge.Kind == NavigationAction.Flight);
        }

        private CompanionRefinementResult RefineFacility(Player body, CompanionMobilitySnapshot state, NavigationBudget budget)
        {
            var arrival = CompanionNativeContact.TopLeft(body, Edge.To);
            if (!facilityPrepared)
            {
                if (!budget.TryPredict()) return CompanionRefinementResult.Pending;
                if (!CompanionMobilityGeometry.ClearBody(arrival, body.width, body.height) || Hazard(body, arrival))
                    return CompanionRefinementResult.Failed;
                position = arrival;
                if (Edge.Detail != NavigationTraversalDetail.Portal) velocity = Vector2.Zero;
                facilityPrepared = true;
                if (CompanionNativeContact.Supported(body, arrival)) return Ready(body, state, position, velocity, true);
                // A pylon's real entry point can be a small distance above
                // the destination support. Prove the passive landing before
                // passing a grounded witness to the next Walk.
                if (Edge.Detail != NavigationTraversalDetail.Pylon) return Ready(body, state, position, velocity, false);
            }
            int gravity = Math.Sign(body.gravDir);
            var contact = CompanionNativeContact.PlatformContact.Native(body, false, false);
            while (ticks < 16 && Math.Abs(position.Y - arrival.Y) <= 24f)
            {
                if (!budget.TryPredict()) return CompanionRefinementResult.Pending;
                var move = new Vector2(0, velocity.Y + gravity * Math.Max(.05f, body.gravity));
                if (!CompanionNativeContact.TryMove(body, position, move, contact, false, out var step) || Hazard(body, step.Position))
                    return CompanionRefinementResult.Failed;
                position = step.Position;
                velocity = step.Velocity;
                ticks++;
                if (step.Ground) return Ready(body, state, position, velocity, true);
            }
            return Ready(body, state, position, velocity, false);
        }

        private CompanionRefinementResult RefineAscent(Player body, CompanionMobilitySnapshot state, NavigationBudget budget,
            bool flight, bool throughDoor = false, bool hatch = false)
        {
            int gravity = body.gravDir < 0 ? -1 : 1;
            float rise = (Edge.From.Y - Edge.To.Y) * gravity;
            float horizontal = Edge.To.X - Edge.From.X;
            var destination = CompanionNativeContact.TopLeft(body, Edge.To);
            bool wing = CompanionMotionProfile.Wings(state), poweredMount = CompanionMotionProfile.MountFlight(state);
            bool carpet = !wing && !state.RocketAvailable && !poweredMount &&
                (state.CarpetReady || state.CarpetAvailable && grounded || state.CarpetTime > 0);
            bool hover = flight && (state.WingHover && wing || state.MountHover);
            float acceleration = CompanionMotionProfile.HorizontalAcceleration(body, state, true, hover);
            float speed = Math.Max(2f, CompanionMotionProfile.HorizontalSpeed(body, state, true, hover));
            float pull = Math.Max(.05f, body.gravity);
            float jump = Math.Max(3f, state.JumpSpeed);
            int duration = Math.Max(1, state.JumpHeight);
            if (flight && !CompanionMotionProfile.CanFly(state) && !carpet) return CompanionRefinementResult.Failed;
            bool runningJump = Edge.Detail == NavigationTraversalDetail.RunningJump;
            if (runningJump && !runwayReady)
            {
                var runway = RefineRunway(body, state, budget, Math.Sign(horizontal), gravity);
                if (runway != CompanionRefinementResult.Ready) return runway;
                runwayReady = true;
            }
            if (flight && !hatch && !checkedFlightEnd)
            {
                if (!budget.TryPredict()) return CompanionRefinementResult.Pending;
                FlightLanding = CompanionNativeContact.Supported(body, destination);
                checkedFlightEnd = true;
            }
            bool bank = FlightLanding && !carpet;
            bool landingRequired = runningJump || carpet || bank || continuing && !flight;
            int horizon = carpet ? Math.Min(240, (int)Math.Ceiling(Math.Abs(horizontal) / speed + speed / acceleration +
                Math.Sqrt(2f * (Math.Abs(rise) + jump * duration + jump * jump / (2f * pull)) / pull)) + duration + 8) : 85;

            while (ticks < horizon)
            {
                if (!budget.TryPredict()) return CompanionRefinementResult.Pending;
                float x = destination.X - position.X;
                float y = (destination.Y - position.Y) * gravity;
                int direction = Math.Sign(x);
                float vx = MathHelper.Clamp(velocity.X + direction * acceleration, -speed, speed);
                float vy = velocity.Y * gravity;
                if (flight)
                {
                    // For a real bank, first get the whole foot above the
                    // contact plane, then release lift to verify landing.
                    // An air approach's hover band would otherwise stop just
                    // below the overhang, where no next Walk can attach.
                    if (bank && y >= CompanionMotionAction.FlightBankClearance) clearedFlightBank = true;
                    bool hasFuel = flightTicks > 0;
                    if (grounded && (carpet && ticks < duration || (hatch ? ticks < duration : ticks == 0) && rise > 8)) vy = -jump;
                    else if (hasFuel && (bank ? !clearedFlightBank : y < -10) && (wing || state.RocketAvailable || poweredMount))
                    {
                        vy = Math.Max(-jump * 1.5f, vy - pull);
                        flightTicks--;
                    }
                    else if (!bank && hasFuel && hover && Math.Abs(y) <= 10f)
                    {
                        vy = MathHelper.Clamp(y * .2f, -2f, 2f);
                        flightTicks--;
                    }
                    else if (hasFuel && carpet && Math.Abs(x) > 10f && vy >= -pull)
                    {
                        // Native CarpetMovement consumes one tick while
                        // holding its vertical velocity against gravity.
                        vy = 0;
                        flightTicks--;
                    }
                    else vy = Math.Min(Math.Max(2f, state.MaxFallSpeed), vy + pull);
                }
                else if (ticks < duration) vy = -jump;
                else if (state.ExtraJumps > 0 && vy >= 0 && y < -10 && ticks < duration * 2 + 15) vy = -jump;
                else vy = Math.Min(Math.Max(2f, state.MaxFallSpeed), vy + pull);
                ticks++;

                if (Math.Abs(x) < Math.Abs(vx) + 2f) vx = MathHelper.Clamp(x, -speed, speed);
                var move = new Vector2(vx, vy * gravity);
                bool clear = CompanionNativeContact.TryMove(body, position, move,
                    CompanionNativeContact.PlatformContact.Native(body, false, false), false, out var result, throughDoor);
                if (!clear) return CompanionRefinementResult.Failed;
                if ((body.lavaImmune || body.lavaTime > 24 || body.lavaWet ||
                    !CompanionMobilityGeometry.TouchesLava(result.Position, body.width, body.height)) &&
                    !CompanionMobilityGeometry.TouchesShimmer(result.Position, body.width, body.height))
                {
                    position = result.Position;
                    velocity = result.Velocity;
                    bool reached = CompanionMotionAction.Near(position, destination, flight ? 22f : 15f, flight ? 20f : 17f) &&
                        (!hatch || position.Y + body.height < Edge.EntryTile.Y * 16 - 4f);
                    if (reached && (result.Ground || !landingRequired &&
                        (flight || (position.Y - destination.Y) * gravity <= 4f)))
                        return Ready(body, state, position, velocity, result.Ground);
                }
                else return CompanionRefinementResult.Failed;
                if (result.Ground && ticks > duration && !flight)
                    return CompanionRefinementResult.Failed;
                // A short vertical launch intentionally waits for its first
                // clearing sample; a ceiling cannot be declared a new inlet.
                if (Math.Abs(vy) > .4f && result.Velocity.Y == 0f && vy < 0 && Math.Abs(horizontal) <= 12f)
                    return CompanionRefinementResult.Failed;
            }
            return CompanionRefinementResult.Failed;
        }

        private CompanionRefinementResult RefineRunway(Player body, CompanionMobilitySnapshot state, NavigationBudget budget, int direction, int gravity)
        {
            if (!grounded) return CompanionRefinementResult.Failed;
            float launch = Entry.X - body.width * .5f;
            float speed = CompanionMotionProfile.HorizontalSpeed(body, state, false, false);
            float acceleration = CompanionMotionProfile.HorizontalAcceleration(body, state, false, false);
            var contact = CompanionNativeContact.PlatformContact.Native(body, false, false);
            while ((launch - position.X) * direction > 2.5f)
            {
                if (runwaySteps >= 32) return CompanionRefinementResult.Failed;
                if (!budget.TryPredict()) return CompanionRefinementResult.Pending;
                float vx = MathHelper.Clamp(velocity.X + direction * acceleration, -speed, speed);
                if (!CompanionNativeContact.TryMove(body, position, new Vector2(vx, Math.Max(.05f, body.gravity) * gravity),
                    contact, true, out var step) || step.Wall || !step.Ground && !CompanionNativeContact.Supported(body, step.Position) ||
                    Hazard(body, step.Position)) return CompanionRefinementResult.Failed;
                position = step.Position;
                velocity = step.Velocity;
                runwaySteps++;
            }
            return velocity.X * direction >= 1.2f ? CompanionRefinementResult.Ready : CompanionRefinementResult.Failed;
        }

        private CompanionRefinementResult RefineGroundContinuation(Player body, CompanionMobilitySnapshot state, NavigationBudget budget)
        {
            if (!grounded) return CompanionRefinementResult.Failed;
            var exit = CompanionNativeContact.TopLeft(body, Edge.To);
            var contact = CompanionNativeContact.PlatformContact.Native(body, false, false);
            while (Math.Abs(position.X - exit.X) > 4f && ticks < 24)
            {
                if (!budget.TryPredict()) return CompanionRefinementResult.Pending;
                float dx = MathHelper.Clamp(exit.X - position.X, -6f, 6f);
                var v = new Vector2(dx, body.gravDir * Math.Max(.05f, body.gravity));
                if (!CompanionNativeContact.TryMove(body, position, v, contact, true, out var step) || step.Wall ||
                    !step.Ground && !CompanionNativeContact.Supported(body, step.Position) || Hazard(body, step.Position))
                    return CompanionRefinementResult.Failed;
                position = step.Position;
                velocity = step.Velocity;
                ticks++;
            }
            return CompanionMotionAction.Near(position, exit, 4f, 17f) ? Ready(body, state, position, velocity, true) :
                CompanionRefinementResult.Failed;
        }

        private CompanionRefinementResult Ready(Player body, CompanionMobilitySnapshot state, Vector2 exit, Vector2 exitVelocity,
            bool support, int fallLoss = 0)
        {
            if (life <= fallLoss) return CompanionRefinementResult.Failed;
            int remaining = flightTicks;
            if (support)
            {
                // Grounded native motion can restore reserves. An airborne
                // witness carries the very same consumed reserve to its exit.
                remaining = CompanionMotionProfile.Read(body, state, true).MaxFlightTicks;
            }
            // Do not borrow possible healing from a future native update.
            // The same loss which proves this landing feeds its successor.
            Witness = new CompanionMotionWitness(exit, exitVelocity, support, remaining, life - fallLoss);
            return CompanionRefinementResult.Ready;
        }

        private CompanionRefinementResult RefineDoor(Player body, CompanionMobilitySnapshot state, NavigationBudget budget)
        {
            if (!budget.TryTiles(18)) return CompanionRefinementResult.Pending;
            if (!CompanionDoor.TryRead(Edge.EntryTile, out var door)) return CompanionRefinementResult.Failed;
            if (door.Family == CompanionDoorFamily.Trapdoor)
            {
                int passage = Math.Sign(Edge.To.Y - Edge.From.Y);
                if (passage == 0 || !door.Open && !door.SwingClear(passage) && !door.SwingClear(-passage))
                    return CompanionRefinementResult.Failed;
                using (new CompanionDoorCollision.Fixture(door.Open ? Rectangle.Empty : door.Tiles))
                    return passage > 0 ? RefineDrop(body, state, budget, !door.Open, door.Aperture.Bottom + 4) :
                        RefineAscent(body, state, budget, CompanionMotionProfile.CanFly(state), !door.Open, hatch: true);
            }
            int direction = Math.Sign(Edge.To.X - Edge.From.X);
            if (direction == 0 || !door.Open && !door.SwingClear(direction) && !door.SwingClear(-direction))
                return CompanionRefinementResult.Failed;
            var exit = CompanionNativeContact.TopLeft(body, Edge.To);
            using (new CompanionDoorCollision.Fixture(door.Open ? Rectangle.Empty : door.Tiles))
            {
                float remaining = (exit.X - position.X) * direction;
                var contact = CompanionNativeContact.PlatformContact.Native(body, false, false);
                while (remaining > .05f && ticks < 18)
                {
                    if (!budget.TryPredict()) return CompanionRefinementResult.Pending;
                    float stride = Math.Min(remaining, 4f);
                    var velocity = new Vector2(direction * stride, Math.Max(.05f, body.gravity) * body.gravDir);
                    if (!CompanionNativeContact.TryMove(body, position, velocity, contact, true, out var step, throughDoor: !door.Open) ||
                        step.Wall || Hazard(body, step.Position))
                        return CompanionRefinementResult.Failed;
                    if (!step.Ground && (!CompanionNativeContact.TryMove(body, step.Position,
                            new Vector2(0f, body.gravDir * 2), contact, false, out var support, throughDoor: !door.Open) || !support.Ground))
                        return CompanionRefinementResult.Failed;
                    position = step.Position;
                    remaining = (exit.X - position.X) * direction;
                    ticks++;
                }
                return remaining <= .05f && Math.Abs(exit.Y - position.Y) <= 10f ?
                    Ready(body, state, position, velocity, true) : CompanionRefinementResult.Failed;
            }
        }

        private static bool Hazard(Player body, Vector2 position) =>
            !body.lavaImmune && !body.lavaWet && body.lavaTime <= 24 &&
            CompanionMobilityGeometry.TouchesLava(position, body.width, body.height) ||
            CompanionMobilityGeometry.TouchesShimmer(position, body.width, body.height);

        private CompanionRefinementResult RefineDrop(Player body, CompanionMobilitySnapshot state, NavigationBudget budget,
            bool throughDoor = false, float clearTop = float.NegativeInfinity)
        {
            int gravity = body.gravDir < 0 ? -1 : 1;
            bool platform = Edge.Kind == NavigationAction.PlatformDrop;
            var exit = CompanionNativeContact.TopLeft(body, Edge.To);
            if (!dropPrepared)
            {
                if (platform)
                {
                    if (!budget.TryTiles(30)) return CompanionRefinementResult.Pending;
                    if (!FindRelease(body, out var launch)) return CompanionRefinementResult.Failed;
                    if (Math.Abs(launch.X - Entry.X) > .1f)
                    {
                        Entry = launch;
                        position = CompanionNativeContact.TopLeft(body, launch);
                        velocity = Vector2.Zero;
                    }
                }
                else if (Edge.Kind == NavigationAction.Drop && grounded)
                {
                    var outlet = FindLedgeOutlet(body, gravity, budget);
                    if (outlet != CompanionRefinementResult.Ready) return outlet;
                }
                if (!releaseCorridor.Begin(body, position, exit, FallOutletX - body.width * .5f))
                    return CompanionRefinementResult.Failed;
                dropPrepared = true;
            }
            var dependency = releaseCorridor.Capture(budget);
            if (dependency != CompanionRefinementResult.Ready) return dependency;
            var source = CompanionNativeContact.TopLeft(body, Entry);
            if (CompanionFallRisk.ProjectedLoss(body, Edge.From, Edge.To, grounded) >= life) return CompanionRefinementResult.Failed;
            float pull = Math.Max(.05f, body.gravity);
            var maxFall = Math.Max(4f, body.maxFallSpeed);
            float run = CompanionMotionProfile.HorizontalSpeed(body, state, false, false);
            float accelerate = CompanionMotionProfile.HorizontalAcceleration(body, state, false, false);
            int ledgeDirection = Edge.Kind == NavigationAction.Drop && grounded ? Math.Sign(FallOutletX - Entry.X) : 0;
            while (ticks < 96)
            {
                if (!budget.TryPredict()) return CompanionRefinementResult.Pending;
                bool released = (position.Y - source.Y) * gravity > 6f;
                float aim = released ? exit.X : platform ? source.X : FallOutletX - body.width * .5f;
                float dx = aim - position.X;
                int heading = !released && ledgeDirection != 0 ? ledgeDirection : Math.Sign(dx);
                float vx = MathHelper.Clamp(velocity.X + heading * accelerate, -run, run);
                if ((released || ledgeDirection == 0) && Math.Abs(dx) < Math.Abs(vx) + 1f) vx = dx;
                float vy = MathHelper.Clamp(velocity.Y * gravity + pull, pull, maxFall) * gravity;
                bool pressDown = platform && !released && gravity > 0;
                var contact = CompanionNativeContact.PlatformContact.Native(body, pressDown, pressDown);
                if (!CompanionNativeContact.TryMove(body, position, new Vector2(vx, vy), contact, false, out var step, throughDoor))
                    return CompanionRefinementResult.Failed;
                if (Hazard(body, step.Position)) return CompanionRefinementResult.Failed;

                ticks++;
                position = step.Position;
                velocity = step.Velocity;
                float depth = (position.Y - source.Y) * gravity;
                if (step.Ground && depth > 6f)
                {
                    var landed = position + new Vector2(body.width * .5f, gravity > 0 ? body.height : 0);
                    return position.Y > clearTop && CompanionMotionAction.Near(position, exit, 28f, 12f) ?
                        Ready(body, state, position, velocity, true,
                            CompanionFallRisk.ProjectedLoss(body, Edge.From, landed, grounded)) : CompanionRefinementResult.Failed;
                }
                if ((position.Y - exit.Y) * gravity > 13f || ticks >= 30 && depth <= 6f)
                    return CompanionRefinementResult.Failed;
            }
            return CompanionRefinementResult.Failed;
        }

        private CompanionRefinementResult FindLedgeOutlet(Player body, int gravity, NavigationBudget budget)
        {
            int direction = Math.Sign(Edge.To.X - Edge.From.X);
            if (direction == 0) return CompanionRefinementResult.Failed;
            Vector2 from = CompanionNativeContact.TopLeft(body, Entry);
            var contact = CompanionNativeContact.PlatformContact.Native(body, false, false);
            for (; ledgeProbeOffset <= 34; ledgeProbeOffset += 2)
            {
                if (!budget.TryPredict()) return CompanionRefinementResult.Pending;
                var at = from + new Vector2(ledgeProbeOffset * direction, 0);
                if (!CompanionNativeContact.TryMove(body, at, new Vector2(0f, 2f * gravity), contact, false, out var step))
                    return CompanionRefinementResult.Failed;
                if (step.Ground || (step.Position.Y - at.Y) * gravity < 1.5f) continue;
                // A center beyond the edge can still hang from its trailing
                // foot. Prove that the full body has released before steering
                // back toward a closer, verified lower landing.
                FallOutletX = at.X + body.width * .5f;
                return CompanionRefinementResult.Ready;
            }
            return CompanionRefinementResult.Failed;
        }

        private bool FindRelease(Player body, out Vector2 launch)
        {
            launch = Edge.From;
            if (body.gravDir < 0) return true;
            int row = Edge.EntryTile.Y;
            // A tile-center graph inlet can still straddle a neighboring
            // solid slope. Shift only along this source platform until the
            // body's entire release footprint clears that side.
            for (int offset = 0; offset <= 8; offset += 2)
                for (int sign = offset == 0 ? 1 : -1; sign <= 1; sign += 2)
                {
                    float center = Edge.From.X + offset * sign;
                    int left = (int)Math.Floor((center - body.width * .5f + .1f) / 16f);
                    int right = (int)Math.Floor((center + body.width * .5f - .1f) / 16f);
                    bool clear = true;
                    for (int x = left; x <= right; x++)
                    {
                        if (!CompanionNativeContact.Loaded(x, row) || !CompanionNativeContact.Loaded(x, row - 1)) { clear = false; break; }
                        var tile = Main.tile[x, row];
                        var above = Main.tile[x, row - 1];
                        if (tile.nactive() && Main.tileSolid[tile.type] && !Main.tileSolidTop[tile.type] ||
                            above.nactive() && Main.tileSolid[above.type] && !Main.tileSolidTop[above.type])
                        { clear = false; break; }
                    }
                    if (!clear) continue;
                    launch.X = center;
                    return true;
                }
            return false;
        }
    }
}
