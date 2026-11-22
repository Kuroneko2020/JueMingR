using System;
using JueMingR.Features.Movement;
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

        private enum DescentResult { Rejected, Pending, Found }

        private struct DescentProbe
        {
            internal bool Ready;
            internal Vector2 Release, Position, Velocity, Prefix;
        }

        private struct DescentObstruction
        {
            internal Point Tile;
            internal Vector2 Departure;
        }

        private static bool ImmuneToDrop(Player body) => body.noFallDmg || body.hasWings || body.mount.CanFly(body) ||
            body.mount.Active && body.mount.Type == 1;

        private static int FallTolerance(Player body) => 25 + body.extraFall + (body.mount.Active ? body.mount.ExtraFall : 0);

        private static int DescentSafetyKey(Player body) => (FallTolerance(body) << 2) |
            (ImmuneToDrop(body) ? 1 : 0) | (body.mount.Active && body.mount.FallDamage <= .001f ? 2 : 0);

        private static bool CanArrestDescent(CompanionMobilitySnapshot mobility) => mobility.UnlimitedMountHover ||
            HasWingThrust(mobility) && (mobility.UnlimitedWings || mobility.WingTime >= 40);

        private static bool SafeDropLanding(Player body, Vector2 from, Vector2 landing) => !SmartFallDecision.Risk(
            landing.Y, (int)(from.Y / 16), body.gravDir, body.extraFall, body.mount.Active ? body.mount.ExtraFall : 0,
            DescentStride, body.stoned, ImmuneToDrop(body), false, body.mount.Active ? body.mount.FallDamage : 1f);

        private static bool DescentDestination(Player body, RouteStep step, float x)
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
            return Move(body, before, new Vector2(0, 8), true, false, out Step contact) && contact.Ground &&
                !Collision.WetCollision(contact.Position, body.width, body.height) && SafeDropLanding(body, step.From, contact.Position);
        }

        private bool DescentDepartureClear(Player body, RouteStep step, Vector2 departure, float speedX, bool sharedCheck)
        {
            // Project neutral drift using live speed before committing
            // across the lip, then recheck the destination under that pose.
            float depth = Math.Max(0, step.To.Y - departure.Y);
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
            float x = departure.X + drift;
            if (x < (step.DescentArea.Left + 1) * 16 || x + body.width > (step.DescentArea.Right - 1) * 16 ||
                !DescentDestination(body, step, x)) return false;

            // A tile edit can occur after publication and before the rolling
            // read cursor reaches it. Check the dry body corridor from the
            // current release pose, covering the whole neutral drift, before
            // the input that takes support away. Only the selected planning
            // body can repeat this proof for a long committed descent.
            float checkedDepth = step.DescentLands ? depth : depth + ProtectedDescentPrefix / 2;
            float driftSlack = speed * .5f;
            int left = (int)Math.Floor(Math.Min(departure.X, x - driftSlack) / 16);
            int right = (int)Math.Floor((Math.Max(departure.X, x + driftSlack) + body.width - .01f) / 16);
            int top = (int)Math.Floor(departure.Y / 16);
            int bottom = (int)Math.Floor((departure.Y + checkedDepth + body.height - .01f) / 16);
            if (right - left + 1 > DescentCommitColumns) return false;
            descentCommitRead |= sharedCheck;

            // After enough vertical travel that even terminal speed gives
            // neutral friction time to settle, only the final body width can
            // meet the remaining rows. Vanilla height bounds the narrow scan.
            int settledRow = speed == 0 ? top : (int)Math.Ceiling((departure.Y + terminal * (stopTime + 2) + body.height) / 16);
            for (int row = top; row <= bottom; row++)
            {
                if (row == settledRow)
                {
                    left = (int)Math.Floor((x - driftSlack) / 16);
                    right = (int)Math.Floor((x + driftSlack + body.width - .01f) / 16);
                }
                for (int column = left; column <= right; column++)
                    if (!DryDescentTile(column, row))
                    {
                        descentObstruction = new DescentObstruction { Tile = new Point(column, row), Departure = departure };
                        return false;
                    }
            }
            return true;
        }

        private static bool DryDescentTile(int column, int row) => Loaded(column, row) && Main.tile[column, row].liquid == 0;

        private bool WakeChangedDescentObstruction(Player body, RouteMotion motion, Vector2 goal)
        {
            // The blocking cell can be much deeper than the normal read
            // cursor has reached. One read on the shared grant wakes its
            // existing route frontier as soon as the real cell clears.
            if (!descentObstruction.HasValue) return false;
            var obstruction = descentObstruction.Value;
            Point tile = obstruction.Tile;
            if (!DryDescentTile(tile.X, tile.Y)) return false;
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

            probe = new DescentProbe { Ready = true, Release = release, Position = release, Velocity = velocity };
            return true;
        }

        private static DescentResult ProbeDescent(Player body, CompanionMobilitySnapshot mobility, Vector2 from, int direction,
            Vector2 velocity, ReadSet terrain, ref DescentProbe probe, ref RouteStep step, out RouteMotion motion)
        {
            motion = RouteMotion.Ground;
            if (!probe.Ready && !BeginDescent(body, mobility, from, direction, velocity, terrain, ref probe)) return DescentResult.Rejected;
            bool controllable = CanArrestDescent(mobility);
            float friction = Math.Max(.01f, body.runSlowdown * .5f);
            float gravity = Math.Max(.05f, body.gravity);
            float fallLimit = Math.Max(gravity, Math.Min(body.maxFallSpeed, 12f));
            // A search grant advances the same known descent, including the
            // original neutral air friction. Losing the grant leaves this
            // pose and its tile dependencies available for the next slice.
            for (int work = 0; work < DescentSlice; work++)
            {
                bool platforms = direction != 0 || probe.Position.Y >= probe.Release.Y + 16;
                float velocityX = probe.Velocity.X;
                velocityX = Math.Sign(velocityX) * Math.Max(0, Math.Abs(velocityX) - friction);
                var next = new Vector2(velocityX, Math.Min(fallLimit, probe.Velocity.Y + gravity));
                if (!Move(body, probe.Position, next, platforms, false, out Step fall, terrain) ||
                    Collision.WetCollision(fall.Position, body.width, body.height)) return DescentResult.Rejected;
                probe.Position = fall.Position;
                probe.Velocity = new Vector2(fall.Wall ? 0 : velocityX, next.Y);
                float depth = probe.Position.Y - probe.Release.Y;
                if (fall.Ground)
                {
                    if (probe.Position.Y - from.Y < 12 || !SafeDropLanding(body, from, probe.Position)) return DescentResult.Rejected;
                    step.DescentLands = true;
                    step.To = probe.Position;
                    return CompleteDescent(from, direction, probe.Release, ref step);
                }
                if (controllable && depth >= ProtectedDescentPrefix && probe.Prefix == Vector2.Zero) probe.Prefix = probe.Position;
                if (controllable && depth >= ProtectedDescentPrefix * 2)
                {
                    step.To = probe.Prefix;
                    motion = RouteMotion.Air;
                    return CompleteDescent(from, direction, probe.Release, ref step);
                }
                if (!controllable && !SafeDropLanding(body, from, probe.Position)) return DescentResult.Rejected;
            }
            return DescentResult.Pending;
        }

        private static DescentResult CompleteDescent(Vector2 from, int direction, Vector2 release, ref RouteStep step)
        {
            step.Operand = release;
            step.Direction = direction;
            step.Kind = direction == 0 ? RouteKind.Drop : RouteKind.LedgeDrop;
            step.Cost = 26 + Vector2.Distance(step.To, from);
            return DescentResult.Found;
        }

        private bool DriveDescentStep(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
            RouteMotion motion, RouteStep step, bool allowDescentCommit, out CompanionMobilityFrame frame, out bool finished)
        {
            frame = new CompanionMobilityFrame { Status = "下降中" };
            finished = false;
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
                if (sharedCheck && !allowDescentCommit)
                {
                    float speed = Math.Max(0, body.velocity.X * step.Direction);
                    float brake = Math.Max(.01f, Math.Min(body.runAcceleration, body.runSlowdown));
                    if (releases || remaining <= reach + speed * speed / (2 * brake))
                    {
                        HoldDescent(body, ref frame);
                        return true;
                    }
                }
                if (releases && !DescentDepartureClear(body, step, at.Position,
                    body.velocity.X + step.Direction * body.runAcceleration, sharedCheck))
                {
                    if (body.velocity.X == 0) return false;
                    HoldDescent(body, ref frame);
                    return true;
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
            var delta = new Vector2(body.velocity.X, Math.Max(2, body.velocity.Y + mobility.Gravity));
            if (!Move(body, body.position, delta, !throughPlatform && !body.stairFall, false, out Step next, null) ||
                Collision.WetCollision(next.Position, body.width, body.height)) return false;
            frame.Drop = throughPlatform;
            if (!throughPlatform && motion == RouteMotion.Air)
            {
                if (AirInput(body, mobility, motion, 0, 1, out var descent))
                { frame.Move = descent.Move; frame.Vertical = descent.Vertical; }
            }
            return true;
        }
    }
}
