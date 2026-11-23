using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private const int DescentWork = 64;
        private const int DescentStride = 8;
        private const int LedgeApproach = 40;
        private const int ProtectedDescentPrefix = 80;
        private const int DescentSlice = 16;
        private const int LocalDescentCommitDepth = 512;
        private const int DescentCommitColumns = 12;
        private bool descentCommitRead;
        private DescentObstruction? descentObstruction;
        private GroundQuery localDescent;
        private DescentProbe localDescentProbe;
        private Vector2 localDescentOrigin, localDescentGoal;
        private int localDescentKey, localDescentSlices, localDescentRequiredLife;

        private enum DescentResult { Rejected, Pending, Found }

        private struct DescentProbe
        {
            internal bool Ready;
            internal Vector2 Release, Position, Velocity, Prefix, LaunchVelocity;
            internal int FallStart, Steer;
            internal float AimX;
        }

        private struct DescentProjection
        {
            internal float Gravity, Terminal, Fall, Friction, MovingTime, StopTime, Drift;
        }

        private struct DescentObstruction
        {
            internal Point Tile;
            internal Vector2 Departure;
            internal bool SideWall;
            internal bool AcceptsWater;
            internal int Shape;
        }

        private static bool CanArrestDescent(CompanionMobilitySnapshot mobility) => mobility.UnlimitedMountHover ||
            HasWingThrust(mobility) && (mobility.UnlimitedWings || mobility.WingTime >= 40);

        private static bool DescentDestination(Player body, RouteStep step, float x, bool airborne = false)
        {
            var target = new Vector2(x, step.To.Y);
            if (!step.DescentLands)
            {
                var down = new Vector2(0, DescentStride);
                for (int distance = 0; distance < ProtectedDescentPrefix / 2; distance += DescentStride)
                {
                    if (!Swept(body, target, down, null, passPlatform: true)) return false;
                    target += down;
                }
                return true;
            }

            // Check the predicted body's actual contact, including an edge
            // profile or newly blocked floor. The read rectangle is not a
            // proof that sideways drift still lands safely.
            var before = target - new Vector2(0, 4);
            if (!Move(body, before, new Vector2(0, 8), true, false, out Step contact) || !contact.Ground ||
                LandingFallLoss(body, contact.Position, airborne ? body.fallStart : (int)(step.From.Y / 16f)) >= body.statLife) return false;
            return step.DescentEntersLiquid ? BreathableWater(body, contact.Position) :
                contact.WaterSurface == step.DescentWaterSurface && !Collision.WetCollision(contact.Position, body.width, body.height);
        }

        private static DescentProjection ProjectDescent(Player body, float depth, float speedX)
        {
            float gravity = Math.Max(.05f, body.gravity);
            float terminal = Math.Max(gravity, body.maxFallSpeed);
            float fall = Math.Max(0, body.velocity.Y);
            float accelerate = Math.Max(0, (terminal - fall) / gravity);
            float firstDepth = (fall + terminal) * .5f * accelerate;
            float time = firstDepth < depth ? accelerate + (depth - firstDepth) / terminal :
                ((float)Math.Sqrt(fall * fall + 2 * gravity * depth) - fall) / gravity;
            float friction = Math.Max(.01f, body.runSlowdown * .5f);
            float speed = Math.Abs(speedX);
            float stopTime = speed / friction;
            float movingTime = Math.Min(time + 1, stopTime);
            float drift = Math.Sign(speedX) * movingTime * (speed - .5f * friction * movingTime);
            return new DescentProjection { Gravity = gravity, Terminal = terminal, Fall = fall, Friction = friction,
                MovingTime = movingTime, StopTime = stopTime, Drift = drift };
        }

        private static bool CanJoinLedgeDeparture(Player body, RouteStep step)
        {
            // Being close to the lip is insufficient when the planned shelf
            // needs a running approach. Keep the preceding walk when the
            // current speed and remaining runway cannot reach its support.
            float runway = (step.Operand.X - body.position.X) * step.Direction;
            float speed = Math.Max(0, body.velocity.X * step.Direction);
            float launch = Math.Max(speed, Math.Min((float)Math.Sqrt(speed * speed + 2 * body.runAcceleration * runway),
                Math.Max(body.maxRunSpeed, body.accRunSpeed)));
            var projected = ProjectDescent(body, Math.Max(0, step.To.Y - body.position.Y), step.Direction * launch);
            float x = step.DescentSteer == 0 ? step.Operand.X + projected.Drift : step.To.X;
            return x >= (step.DescentArea.Left + 1) * 16 && x + body.width <= (step.DescentArea.Right - 1) * 16 &&
                DescentDestination(body, step, x);
        }

        private bool DescentDepartureClear(Player body, RouteStep step, Vector2 departure, float speedX, bool sharedCheck)
        {
            // Project neutral drift using live speed before committing
            // across the lip, then recheck the destination under that pose.
            float depth = Math.Max(0, step.To.Y - departure.Y);
            var projected = ProjectDescent(body, depth, speedX);
            float speed = Math.Abs(speedX);
            if (Math.Abs(projected.Drift) + body.width > DescentCommitColumns * 16) return false;
            descentCommitRead |= sharedCheck;

            // Replay only while this body still drifts sideways. A dry side
            // wall can stop the drift before its projected column, leaving
            // the old destination on a ledge the body cannot actually reach.
            float launchX = step.DescentSteer == 0 && step.Direction == 0 && body.velocity.Y == 0 ?
                Math.Sign(speedX) * Math.Max(0, speed - projected.Friction) : speedX;
            var probe = new DescentProbe { Release = departure, Position = departure, Velocity = new Vector2(launchX, projected.Fall),
                FallStart = (int)(body.position.Y / 16f), Steer = step.DescentSteer, AimX = step.DescentAimX };
            int ticks = step.DescentSteer == 0 ? (int)Math.Ceiling(projected.MovingTime) + 2 : 56;
            Point? wall = null;
            bool wet = false;
            float minX = departure.X, maxX = departure.X;
            while ((probe.Velocity.X != 0 || probe.Steer != 0) && probe.Position.Y < step.To.Y)
            {
                int heading = Math.Sign(probe.Velocity.X);
                if (ticks-- == 0 || !AdvanceDescent(body, step.Direction, projected.Friction, projected.Gravity, projected.Terminal,
                    null, ref probe, out Step contact)) return false;
                if (contact.Wall) wall = DescentWallTile(body, contact.Position, heading);
                minX = Math.Min(minX, probe.Position.X);
                maxX = Math.Max(maxX, probe.Position.X);
                wet = Collision.WetCollision(contact.Position, body.width, body.height);
                if (wet) break;
                if (!contact.Ground) continue;
                if (!step.DescentLands || Math.Abs(contact.Position.Y - step.To.Y) > 4) return false;
                break;
            }
            float x = probe.Position.X;
            if (wet && step.DescentEntersLiquid && !BreathableWater(body, probe.Position)) return false;
            if (x < (step.DescentArea.Left + 1) * 16 || x + body.width > (step.DescentArea.Right - 1) * 16 ||
                !DescentDestination(body, step, x))
            {
                if (wall.HasValue)
                {
                    Point tile = wall.Value;
                    descentObstruction = new DescentObstruction
                    { Tile = tile, Departure = departure, SideWall = true, Shape = TileValue(Main.tile[tile.X, tile.Y]) };
                }
                return false;
            }

            // A tile edit can occur after publication and before the rolling
            // read cursor reaches it. Check the dry body corridor from the
            // current release pose, covering the whole neutral drift, before
            // the input that takes support away. Only the selected planning
            // body can repeat this proof for a long committed descent.
            float checkedDepth = step.DescentLands ? depth : depth + ProtectedDescentPrefix / 2;
            float driftSlack = speed * .5f;
            // Water dampens actual drift after contact. The dry projection
            // bounds its residual reach, while the first wet pose is observed.
            float far = wet && step.DescentEntersLiquid ? departure.X + projected.Drift : x;
            int left = (int)Math.Floor(Math.Min(minX, Math.Min(x, far) - driftSlack) / 16);
            int right = (int)Math.Floor((Math.Max(maxX, Math.Max(x, far) + driftSlack) + body.width - .01f) / 16);
            int top = (int)Math.Floor(departure.Y / 16);
            int bottom = (int)Math.Floor((departure.Y + checkedDepth + body.height - .01f) / 16);
            int surfaceRow = step.DescentWaterSurface ? (int)Math.Floor((step.To.Y + body.height + .01f) / 16) : -1;
            if (right - left + 1 > DescentCommitColumns) return false;
            // After enough vertical travel that even terminal speed gives
            // neutral friction time to settle, only the final body width can
            // meet the remaining rows. Vanilla height bounds the narrow scan.
            int settledRow = step.DescentSteer != 0 || step.DescentEntersLiquid ? bottom + 1 : speed == 0 ? top :
                (int)Math.Ceiling((departure.Y + projected.Terminal * (projected.StopTime + 2) + body.height) / 16);
            for (int row = top; row <= bottom; row++)
            {
                if (row == settledRow)
                {
                    left = (int)Math.Floor((x - driftSlack) / 16);
                    right = (int)Math.Floor((x + driftSlack + body.width - .01f) / 16);
                }
                // A partial water surface occupies the very tile containing
                // the last few pixels of the dry body's feet. Only the
                // confirmed native support row may be wet for this landing.
                bool acceptsWater = step.DescentEntersLiquid || row == surfaceRow;
                for (int column = left; column <= right; column++)
                    if (!DescentTileClear(column, row, acceptsWater))
                    {
                        descentObstruction = new DescentObstruction
                        { Tile = new Point(column, row), Departure = departure, AcceptsWater = acceptsWater };
                        return false;
                    }
            }
            return !wet || step.DescentEntersLiquid && DescentDestination(body, step, far);
        }

        private static bool DescentTileClear(int column, int row, bool acceptsWater)
        {
            if (!Loaded(column, row)) return false;
            Tile tile = Main.tile[column, row];
            return tile.liquid == 0 || acceptsWater && tile.water();
        }

        private static Point? DescentWallTile(Player body, Vector2 position, int heading)
        {
            int column = (int)Math.Floor((heading < 0 ? position.X - .01f : position.X + body.width + .01f) / 16);
            int first = (int)Math.Floor(position.Y / 16), last = (int)Math.Floor((position.Y + body.height - .01f) / 16);
            for (int row = first; row <= last; row++)
            {
                if (!Loaded(column, row)) continue;
                Tile tile = Main.tile[column, row];
                if (tile.active() && !tile.inActive() && Main.tileSolid[tile.type] && !Main.tileSolidTop[tile.type])
                    return new Point(column, row);
            }
            return null;
        }

        private bool WakeChangedDescentObstruction(Player body, RouteMotion motion, Vector2 goal)
        {
            // The blocking cell can be much deeper than the normal read
            // cursor has reached. One read on the shared grant wakes its
            // existing route frontier as soon as the real cell clears.
            if (!descentObstruction.HasValue) return false;
            var obstruction = descentObstruction.Value;
            Point tile = obstruction.Tile;
            if (obstruction.SideWall ? !Loaded(tile.X, tile.Y) || TileValue(Main.tile[tile.X, tile.Y]) == obstruction.Shape :
                !DescentTileClear(tile.X, tile.Y, obstruction.AcceptsWater)) return false;
            descentObstruction = null;
            if ((blockedStep.Kind == RouteKind.Drop || blockedStep.Kind == RouteKind.LedgeDrop) &&
                blockedStep.DescentArea.Contains(tile)) blockedAt = 0;
            if (search != null)
            {
                search.Dirty(new Rectangle(tile.X, tile.Y, 1, 1));
                search.NeedsLiveRoot = true;
            }
            lastRejectedPublication = -1;
            RetirePublication();
            // A nearby body on the same upper level may still be following
            // a lateral route that was useful only while the drop was blocked.
            return motion == RouteMotion.Ground && goal.Y > body.position.Y + 38 &&
                Math.Abs(body.position.Y - obstruction.Departure.Y) <= 12 &&
                Math.Abs(body.position.X - obstruction.Departure.X) <= LedgeApproach + body.width;
        }

        private static void HoldDescent(Player body, ref CompanionMobilityFrame frame)
        {
            int brake = -Math.Sign(body.velocity.X);
            if (brake != 0 && NextClear(body, brake, 0) &&
                DescentSupport(body, body.position + new Vector2(brake * 4, 0), null)) frame.Move = brake;
        }

        private static bool DescentSupport(Player body, Vector2 position, ReadSet terrain, bool platforms = true) =>
            Move(body, position, Vector2.UnitY, platforms, false, out Step contact, terrain) && contact.Ground;

        private static int DescentSteering(Player body, float x, float speed, float aim)
        {
            float offset = aim - x;
            float brake = Math.Max(.05f, body.runSlowdown + body.runAcceleration);
            float stopping = speed * Math.Abs(speed) / (2 * brake);
            return Math.Abs(offset) <= 2.5f && Math.Abs(speed) <= .6f || Math.Abs(offset - stopping) <= 1.5f ?
                0 : Math.Sign(offset - stopping);
        }

        private static float DescentHorizontal(Player body, float speed, int move, float friction)
        {
            if (move == 0) return Math.Sign(speed) * Math.Max(0, Math.Abs(speed) - friction);
            float along = speed * move;
            if (along >= body.maxRunSpeed)
            {
                if (along < body.accRunSpeed && body.dashDelay >= 0 && body.wingsLogic > 0)
                    return (along + body.runAcceleration * .4f) * move;
                return speed;
            }
            if (along < -body.runSlowdown) along += body.runSlowdown;
            return (along + body.runAcceleration) * move;
        }

        private static bool AdvanceDescent(Player body, int direction, float friction, float gravity, float terminal,
            ReadSet terrain, ref DescentProbe probe, out Step step)
        {
            bool platforms = direction != 0 || probe.Position.Y >= probe.Release.Y + 16;
            int move = probe.Steer == 0 ? 0 : DescentSteering(body, probe.Position.X, probe.Velocity.X, probe.AimX);
            float x = DescentHorizontal(body, probe.Velocity.X, move, friction);
            var next = new Vector2(x, Math.Min(terminal, probe.Velocity.Y + gravity));
            if (!Move(body, probe.Position, next, platforms, false, out step, terrain)) return false;
            // Approximate the world-height reset during prediction. Other
            // timed state is credited through the body's observed fallStart.
            if (LowGravityFallReset(probe.Position.Y)) probe.FallStart = (int)(probe.Position.Y / 16f);
            probe.Position = step.Position;
            probe.Velocity = new Vector2(step.Wall ? 0 : x, next.Y);
            return true;
        }

        private static bool BreathableWater(Player body, Vector2 position) =>
            BodyLiquidClear(body, position, position, ordinaryOnly: true) && Collision.WetCollision(position, body.width, body.height) &&
            !Collision.DrownCollision(position, body.width, body.height, body.gravDir);

        private static bool ShallowWaterBottom(Player body, Step entry, ReadSet terrain, out Vector2 landing)
        {
            landing = entry.Position;
            if (!BreathableWater(body, landing)) return false;
            if (entry.Ground) return true;
            // Wet travel takes over once the actual body reaches the basin.
            // Require breathable water and a reachable solid bottom first.
            for (int distance = 0; distance < body.height; distance += 4)
            {
                if (!Move(body, landing, new Vector2(0, 4), true, false, out Step next, terrain) ||
                    !BreathableWater(body, next.Position)) return false;
                landing = next.Position;
                if (next.Ground) return true;
            }
            return false;
        }

        private static bool BeginDescent(Player body, CompanionMobilitySnapshot mobility, Vector2 from, int direction,
            Vector2 velocity, ReadSet terrain, ref DescentProbe probe)
        {
            if (body.gravDir != 1 || mobility.Controlled || mobility.Wet || !DescentSupport(body, from, terrain)) return false;
            Vector2 release = from;
            if (direction == 0)
            {
                // Down is a legal outlet only when the current full body has
                // platform support and the same probe without platforms clears.
                if (!Move(body, from, Vector2.UnitY, false, false, out Step clear, terrain) || clear.Ground) return false;
            }
            else
            {
                // Support under one more 4px step is still part of a ledge
                // approach. Stop at the first genuinely unsupported pose.
                bool leftSupport = false;
                var across = new Vector2(direction * 4, 0);
                for (int reach = 0; reach < LedgeApproach; reach += 4)
                {
                    if (!Move(body, release, across, true, false, out Step walk, terrain) || walk.Wall) return false;
                    release = walk.Position;
                    if (DescentSupport(body, release, terrain)) continue;
                    if (!Footprint(body, release, new Vector2(0, 2), terrain)) return false;
                    leftSupport = true;
                    break;
                }
                if (!leftSupport) return false;

                float distance = Math.Abs(release.X - from.X);
                float start = Math.Max(0, velocity.X * direction);
                float speed = (float)Math.Sqrt(start * start + 2 * body.runAcceleration * distance);
                velocity.X = direction * Math.Max(start, Math.Min(speed, Math.Max(body.maxRunSpeed, body.accRunSpeed)));
            }

            probe = new DescentProbe { Ready = true, Release = release, Position = release, Velocity = velocity,
                LaunchVelocity = velocity, FallStart = (int)(from.Y / 16f) };
            return true;
        }

        private static DescentResult ProbeDescent(Player body, CompanionMobilitySnapshot mobility, Vector2 from, int direction,
            Vector2 velocity, ReadSet terrain, ref DescentProbe probe, ref RouteStep step, out RouteMotion motion, out int requiredLife,
            float aimX)
        {
            motion = RouteMotion.Ground;
            requiredLife = 0;
            if (!probe.Ready && !BeginDescent(body, mobility, from, direction, velocity, terrain, ref probe)) return DescentResult.Rejected;
            bool controllable = CanArrestDescent(mobility);
            float friction = Math.Max(.01f, body.runSlowdown * .5f);
            float gravity = Math.Max(.05f, body.gravity);
            float fallLimit = Math.Max(gravity, Math.Min(body.maxFallSpeed, 12f));
            int groundLookahead = Math.Max(ProtectedDescentPrefix * 2, FallTolerance(body) * 16);
            // A search grant advances the same known descent, including the
            // original neutral air friction. Losing the grant leaves this
            // pose and its tile dependencies available for the next slice.
            for (int work = 0; work < DescentSlice; work++)
            {
                if (!AdvanceDescent(body, direction, friction, gravity, fallLimit, terrain, ref probe, out Step fall))
                {
                    if (controllable && probe.Position.Y - probe.Release.Y >= ProtectedDescentPrefix * 2)
                        return CompleteFreeDescent(from, direction, probe, ref step, out motion);
                    return RetryDescent(body, mobility, from, aimX, ref probe);
                }
                if (Collision.WetCollision(fall.Position, body.width, body.height))
                {
                    if (!ShallowWaterBottom(body, fall, terrain, out Vector2 landing) || landing.Y - from.Y < 12)
                        return RetryDescent(body, mobility, from, aimX, ref probe);
                    step.DescentDamage = LandingFallLoss(body, landing, probe.FallStart);
                    if (step.DescentDamage >= body.statLife)
                    { requiredLife = step.DescentDamage + 1; return RetryDescent(body, mobility, from, aimX, ref probe); }
                    step.DescentLands = step.DescentEntersLiquid = true;
                    step.To = landing;
                    motion = RouteMotion.Liquid;
                    return CompleteDescent(from, direction, probe, ref step);
                }
                float depth = probe.Position.Y - probe.Release.Y;
                if (fall.Ground)
                {
                    if (probe.Position.Y - from.Y < 12) return RetryDescent(body, mobility, from, aimX, ref probe);
                    step.DescentDamage = LandingFallLoss(body, probe.Position, probe.FallStart);
                    if (step.DescentDamage >= body.statLife)
                    { requiredLife = step.DescentDamage + 1; return RetryDescent(body, mobility, from, aimX, ref probe); }
                    step.DescentLands = true;
                    step.DescentWaterSurface = fall.WaterSurface;
                    step.To = probe.Position;
                    return CompleteDescent(from, direction, probe, ref step);
                }
                if (controllable && depth >= ProtectedDescentPrefix && probe.Prefix == Vector2.Zero) probe.Prefix = probe.Position;
                // Prefer known landing support within the ordinary safe-fall
                // range. Reaching an earlier flyable pose does not make a
                // ground connection disappear from the shared frontier.
                if (controllable && depth >= groundLookahead)
                    return CompleteFreeDescent(from, direction, probe, ref step, out motion);
                // A nearby cloud landing or native liquid reset can still
                // change an otherwise lethal estimate. Keep only a short
                // additional contact window beyond the survivable depth.
                if (!controllable && NativeFallLoss(body, probe.Position.Y - 48, probe.FallStart) >= body.statLife)
                {
                    requiredLife = NativeFallLoss(body, probe.Position.Y, probe.FallStart) + 1;
                    return RetryDescent(body, mobility, from, aimX, ref probe);
                }
            }
            return DescentResult.Pending;
        }

        private static DescentResult ProbeDescent(Player body, CompanionMobilitySnapshot mobility, Vector2 from, int direction,
            Vector2 velocity, ReadSet terrain, ref DescentProbe probe, ref RouteStep step, out RouteMotion motion)
        {
            // The pre-observation Boolean caller cannot carry a paid fall
            // through execution. Only a live route can authorize that plan.
            var result = ProbeDescent(body, mobility, from, direction, velocity, terrain, ref probe, ref step, out motion, out _, from.X);
            return result == DescentResult.Found && step.DescentDamage > 0 ? DescentResult.Rejected : result;
        }

        private static DescentResult RetryDescent(Player body, CompanionMobilitySnapshot mobility, Vector2 from,
            float aimX, ref DescentProbe probe)
        {
            if (probe.Steer != 0 || body.mount.Active || mobility.Controlled || Math.Abs(aimX - probe.Release.X) < 6)
                return DescentResult.Rejected;
            probe.Steer = Math.Sign(aimX - probe.Release.X);
            probe.AimX = probe.Release.X + MathHelper.Clamp(aimX - probe.Release.X, -72, 72);
            probe.Position = probe.Release;
            probe.Velocity = probe.LaunchVelocity;
            probe.Prefix = Vector2.Zero;
            probe.FallStart = (int)(from.Y / 16f);
            return DescentResult.Pending;
        }

        private static DescentResult CompleteFreeDescent(Vector2 from, int direction, DescentProbe probe,
            ref RouteStep step, out RouteMotion motion)
        {
            step.To = probe.Prefix;
            motion = RouteMotion.Air;
            return CompleteDescent(from, direction, probe, ref step);
        }

        private static DescentResult CompleteDescent(Vector2 from, int direction, DescentProbe probe, ref RouteStep step)
        {
            step.Operand = probe.Release;
            step.Direction = direction;
            step.DescentSteer = probe.Steer;
            step.DescentAimX = probe.AimX;
            step.Kind = direction == 0 ? RouteKind.Drop : RouteKind.LedgeDrop;
            step.Cost = 26 + Vector2.Distance(step.To, from) + step.DescentDamage * 1.5f;
            return DescentResult.Found;
        }

        private bool TryLocalDescentPrefix(Player body, CompanionMobilityState state, Vector2 goal, bool allowPlanning,
            out RouteStep step, out bool spent)
        {
            step = default;
            spent = false;
            if (!allowPlanning || state.AwaitingEquipmentObservation || body.velocity.Y != 0 || Math.Abs(body.velocity.X) > 1 ||
                goal.Y - body.position.Y < 16 || goal.Y - body.position.Y > 120 || Math.Abs(goal.X - body.position.X) > 88) return false;
            using (Scratch.Save())
            {
                bool solid = DescentSupport(body, body.position, null, false);
                int direction = solid ? Math.Sign(goal.X - body.position.X) : 0;
                if (direction != 0 && DescentSupport(body, body.position + new Vector2(direction * WalkProbeDistance, 0), null)) return false;
                if (direction == 0 && solid) return false;

                localDescent = localDescent ?? new GroundQuery();
                int key = Capabilities(body) * 31 + DescentSafetyKey(body);
                bool same = localDescentOrigin == body.position && localDescentKey == key &&
                    Vector2.DistanceSquared(localDescentGoal, goal) < 16 * 16 && localDescent.Matches(body, key) &&
                    (localDescentRequiredLife == 0 || body.statLife < localDescentRequiredLife);
                if (!same)
                {
                    localDescent.Prepare(body, key);
                    localDescent.Valid = true;
                    localDescentOrigin = body.position;
                    localDescentGoal = goal;
                    localDescentKey = key;
                    localDescentProbe = default;
                    localDescentSlices = 0;
                    localDescentRequiredLife = 0;
                }
                if (localDescentSlices >= 3) return false;
                spent = true;
                localDescentSlices++;
                step.From = body.position;
                Rectangle prior = localDescent.Reads.BeginNestedArea();
                var result = ProbeDescent(body, state.Observed, body.position, direction, body.velocity, localDescent.Reads,
                    ref localDescentProbe, ref step, out _, out int requiredLife, goal.X);
                localDescent.Reads.EndNestedArea(prior);
                if (requiredLife > 0 && (localDescentRequiredLife == 0 || requiredLife < localDescentRequiredLife))
                    localDescentRequiredLife = requiredLife;
                if (result != DescentResult.Pending) localDescentSlices = 3;
                if (result != DescentResult.Found || (step.To.Y - goal.Y) > 48) return false;
                step.DescentArea = localDescent.Reads.EndArea();
                step.ObserveAfter = true;
                return true;
            }
        }

        private bool DriveDescentStep(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
            RouteMotion motion, RouteStep step, bool allowDescentCommit, out CompanionMobilityFrame frame, out bool finished)
        {
            frame = new CompanionMobilityFrame { Status = "下降中" };
            finished = false;
            if (step.DescentEntersLiquid && mobility.Wet && body.position.Y > actionOrigin.Y + 4)
            { finished = !mobility.Lava && !mobility.Shimmer && !mobility.Honey; return false; }
            if (motion != RouteMotion.Ground && motion != RouteMotion.Air || body.gravDir != 1 ||
                mobility.Controlled || !step.DescentLands && !CanArrestDescent(mobility)) return false;
            if (age == 1 && !DescentDestination(body, step, step.To.X)) return false;
            if (search != null)
            {
                // Share the graph's read cursor and repairs. An executing
                // descent samples a fixed number of facts each body update.
                var edits = search.Terrain.Changes(ref search.TerrainCursor, 12);
                search.Dirty(edits);
                if (!actionStarted && edits.Intersects(step.DescentArea)) return false;
            }

            float descended = body.position.Y - actionOrigin.Y;
            actionStarted |= motion == RouteMotion.Air && body.velocity.Y > 0 && descended > 1;
            if (actionStarted && step.DescentLands && NativeFallLoss(body, step.To.Y, body.fallStart) >= body.statLife &&
                !DescentDestination(body, step, step.To.X, airborne: true)) return false;
            if (state.AwaitingEquipmentObservation && !actionStarted && !body.stairFall && motion == RouteMotion.Ground)
            {
                // An equipped item changed before this Player.Update has
                // refreshed its own fall protection. Wait on the existing
                // support and brake existing motion through native input.
                HoldDescent(body, ref frame);
                return true;
            }
            if (actionStarted && noProgress > 24) return false;
            if (actionStarted && motion == RouteMotion.Ground && descended > 10 &&
                body.velocity.Y >= 0 && !body.stairFall)
            { finished = true; return false; }
            if (actionStarted && !step.DescentLands && body.position.Y >= step.To.Y - 4 && body.velocity.Y >= 0)
            { finished = true; return false; }
            if (body.position.Y > step.To.Y + 16) return false;

            bool sharedCheck = step.DescentLands && step.To.Y - body.position.Y > LocalDescentCommitDepth;
            if (!actionStarted && step.Kind == RouteKind.LedgeDrop && motion == RouteMotion.Ground)
            {
                float remaining = (step.Operand.X - body.position.X) * step.Direction;
                if (remaining < -12) return false;
                // This step owns an intentional loss of support. The ordinary
                // walking/braking rule keeps its existing continuous support.
                float reach = Math.Max(4, Math.Abs(body.velocity.X) + body.runAcceleration);
                if (!Move(body, body.position, new Vector2(step.Direction * reach, 0), true, false, out Step at, null) || at.Wall ||
                    Collision.WetCollision(at.Position, body.width, body.height))
                    return false;
                bool releases = !DescentSupport(body, at.Position, null);
                float speed = Math.Max(0, body.velocity.X * step.Direction);
                float brake = Math.Max(.01f, body.runAcceleration + body.runSlowdown);
                // Once another forward input would use the remaining native
                // stopping room, a rejection must still be able to hold the lip.
                bool committing = releases || remaining <= reach + speed + speed * speed / (2 * brake);
                if (committing)
                {
                    if (sharedCheck && !allowDescentCommit)
                    {
                        HoldDescent(body, ref frame);
                        return true;
                    }
                    Vector2 launch = at.Position;
                    float launchX = body.velocity.X + step.Direction * body.runAcceleration;
                    if (!releases)
                    {
                        DescentProbe outlet = default;
                        if (!BeginDescent(body, mobility, body.position, step.Direction, body.velocity, null, ref outlet)) return false;
                        launch = outlet.Release;
                        launchX = outlet.Velocity.X;
                    }
                    if (!DescentDepartureClear(body, step, launch, launchX, sharedCheck))
                    {
                        if (body.velocity.X == 0) return false;
                        HoldDescent(body, ref frame);
                        return true;
                    }
                }
                frame.Move = step.Direction;
                return true;
            }

            bool throughPlatform = step.Kind == RouteKind.Drop && body.position.Y < step.Operand.Y + 16;
            if (throughPlatform && !actionStarted && !body.stairFall)
            {
                if (sharedCheck && !allowDescentCommit)
                {
                    HoldDescent(body, ref frame);
                    return true;
                }
                if (!DescentSupport(body, body.position, null) || DescentSupport(body, body.position, null, false) ||
                    !DescentDepartureClear(body, step, body.position, body.velocity.X, sharedCheck)) return false;
            }
            int steering = step.DescentSteer == 0 ? 0 : DescentSteering(body, body.position.X, body.velocity.X, step.DescentAimX);
            float nextX = steering == 0 ? body.velocity.X : DescentHorizontal(body, body.velocity.X, steering, body.runSlowdown * .5f);
            var delta = new Vector2(nextX, Math.Max(2, body.velocity.Y + mobility.Gravity));
            if (!Move(body, body.position, delta, !throughPlatform && !body.stairFall, false, out Step next, null) ||
                Collision.WetCollision(next.Position, body.width, body.height) &&
                (!step.DescentEntersLiquid || !BreathableWater(body, next.Position))) return false;
            frame.Drop = throughPlatform;
            frame.Move = steering;
            if (!throughPlatform && motion == RouteMotion.Air && step.DescentSteer == 0)
            {
                if (AirInput(body, mobility, motion, steering, 1, out var descent))
                { frame.Move = descent.Move; frame.Vertical = descent.Vertical; }
            }
            return true;
        }
    }
}
