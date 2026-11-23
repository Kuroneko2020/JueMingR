using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private const int HoikWork = 40;

        private enum HoikOutcome : byte { Running, Reached, Blocked, LandedElsewhere }

        private readonly struct HoikPassage
        {
            internal readonly int First, Last, Ceiling, Floor, Direction, Width, Height;

            internal HoikPassage(int first, int last, int ceiling, int floor, int direction, Player body)
            {
                First = first; Last = last; Ceiling = ceiling; Floor = floor;
                Direction = direction; Width = body.width; Height = body.height;
            }

            internal bool ContainsTooth(int x) => (x - First) * Direction >= 0 &&
                (Last - x) * Direction >= 0 && (x - First) % 2 == 0;

            internal float FloorY => Floor * 16 - Height;
            internal float ExitX => Direction > 0 ? (Last + 1) * 16 + 2 : Last * 16 - Width - 2;
        }

        private static bool HoikBodySize(Player body) => body.width == Player.defaultWidth && body.height == Player.defaultHeight;

        private static bool CanHoik(Player body, CompanionMobilitySnapshot mobility) => body.gravDir == 1 &&
            HoikBodySize(body) && !body.wet && !body.mount.Active &&
            !body.pulley && !mobility.DashActive && body.grappling[0] < 0;

        private static bool HasHoikMouth(Player body, Vector2 from, int direction, ReadSet terrain = null)
        {
            if (direction == 0 || !HoikBodySize(body) || body.gravDir != 1) return false;
            int ceiling = (int)Math.Floor(from.Y / 16);
            float overlap = (ceiling + 1) * 16 - from.Y;
            if (overlap < 4 || overlap > 12) return false;
            int front = (int)Math.Floor((from.X + (direction > 0 ? body.width : 0)) / 16);
            for (int offset = -1; offset <= 1; offset++)
                if (HoikTooth(front + direction * offset, ceiling, direction, terrain)) return true;
            return false;
        }

        private static bool StopWalkAtHoik(Player body, CompanionMobilitySnapshot mobility, Vector2 from, int direction,
            ReadSet terrain, ref Vector2 to)
        {
            if (!CanHoik(body, mobility) || Math.Abs(to.Y - from.Y) > .125f) return true;
            int row = (int)Math.Floor(from.Y / 16);
            int floor = (int)Math.Round((from.Y + body.height) / 16);
            float overlap = (row + 1) * 16 - from.Y;
            if (overlap < 4 || overlap > 12 || Math.Abs(from.Y + body.height - floor * 16) > .125f) return true;

            // The ordinary Walk edge estimates native run velocity at its
            // endpoint. It cannot continue that estimate across a roof tooth
            // which resets velocity and requires continued directional input.
            // Its last clear position remains available to join the real Hoik.
            float front = direction > 0 ? body.width : 0;
            int near = (int)Math.Floor(from.X / 16);
            int far = (int)Math.Floor((to.X + body.width - .01f) / 16);
            if (direction < 0)
            {
                near = (int)Math.Floor((from.X + body.width - .01f) / 16);
                far = (int)Math.Floor(to.X / 16);
            }
            for (int tooth = near; (far - tooth) * direction >= 0; tooth += direction)
            {
                if (!HoikTooth(tooth, row, direction, terrain)) continue;
                float edge = (tooth + (direction < 0 ? 1 : 0)) * 16 - front;
                if ((to.X - edge) * direction <= 0) continue;
                // A paired tooth above flat floor contradicts the ordinary
                // constant-speed witness at this boundary. The separately
                // charged Hoik edge decides whether the whole lane is safe.
                if (!ReadHoikTile(tooth, floor, terrain)) continue;
                Tile footing = Main.tile[tooth, floor];
                if (!footing.active() || footing.inActive() || !Main.tileSolid[footing.type] || Main.tileSolidTop[footing.type] ||
                    footing.slope() != 0 || footing.halfBrick()) continue;
                Vector2 approach = new Vector2(edge - .25f * direction, from.Y);
                if ((approach.X - from.X) * direction < 2 || !HasSurface(body, approach, terrain, platforms: true)) return false;
                to = approach;
                break;
            }
            return true;
        }

        private static bool TryHoikEdge(Player body, CompanionMobilitySnapshot mobility, RouteNode node, int direction,
            ReadSet terrain, ref RouteStep step)
        {
            if (node.Motion != RouteMotion.Ground || !CanHoik(body, mobility)) return false;
            Vector2 from = node.Position;
            int floor = (int)Math.Round((from.Y + body.height) / 16);
            if (Math.Abs(from.Y + body.height - floor * 16) > .125f) return false;
            int ceiling = (int)Math.Floor(from.Y / 16);
            float overlap = (ceiling + 1) * 16 - from.Y;
            // A flat floor must block the tooth's downward correction. The
            // clear body's head enters the tooth from below its upper face.
            if (overlap < 4 || overlap > 12 || floor - ceiling < 2) return false;
            int headColumn = (int)Math.Floor((from.X + (direction > 0 ? body.width : 0)) / 16);
            for (int offset = -1; offset <= 1; offset++)
            {
                int first = headColumn + direction * offset;
                float boundary = (first + (direction < 0 ? 1 : 0)) * 16;
                float leading = from.X + (direction > 0 ? body.width : 0);
                if ((boundary - leading) * direction > 20 || (leading - boundary) * direction > body.width ||
                    !HoikTooth(first, ceiling, direction, terrain)) continue;
                if (!TryHoikPassage(body, from.X, first, ceiling, floor, direction, terrain, out var proof)) continue;
                float span = (proof.ExitX - from.X) * direction;
                if (!TryHoikTick(body, mobility, proof, from, node.VelocityX, out _, terrain)) continue;
                float creep = MovementRun(body, mobility, airborne: false).Advance(0, direction) * direction;
                if (creep < .04f) return false;
                step.HoikProof = proof;
                step.To = new Vector2(proof.ExitX, proof.FloorY);
                step.Direction = direction;
                // A roof tooth can spend updates at one acceleration unit
                // per tick because native collision cancels horizontal speed.
                // Only this action keeps that physically slow progress alive.
                step.Duration = (int)Math.Ceiling(span / creep) + 24;
                step.Cost = span + 24;
                return true;
            }
            return false;
        }

        private static bool TryHoikPassage(Player body, float fromX, int first, int ceiling, int floor, int direction,
            ReadSet terrain, out HoikPassage proof)
        {
            int last = HoikTooth(first + 2 * direction, ceiling, direction, terrain) ? first + 2 * direction : first;
            proof = new HoikPassage(first, last, ceiling, floor, direction, body);
            float span = (proof.ExitX - fromX) * direction;
            return span > 0 && span <= 80 && KnownHoikLane(body, proof, fromX, proof.ExitX, terrain);
        }

        private static bool ReadHoikTile(int x, int y, ReadSet terrain) =>
            terrain == null ? Loaded(x, y) : terrain.Record(x, y);

        private static bool HoikTooth(int x, int y, int direction, ReadSet terrain)
        {
            if (!ReadHoikTile(x, y, terrain) || !ReadHoikTile(x - direction, y, terrain)) return false;
            Tile tooth = Main.tile[x, y], entrance = Main.tile[x - direction, y];
            return tooth.active() && !tooth.inActive() && Main.tileSolid[tooth.type] && !Main.tileSolidTop[tooth.type] &&
                tooth.slope() == (direction > 0 ? 3 : 4) && entrance.active() && entrance.inActive() &&
                entrance.slope() == (direction > 0 ? 4 : 3);
        }

        private static bool KnownHoikLane(Player body, HoikPassage proof, float fromX, float toX, ReadSet terrain)
        {
            int left = (int)Math.Floor(Math.Min(fromX, toX) / 16);
            int right = (int)Math.Floor((Math.Max(fromX, toX) + proof.Width - .01f) / 16);
            for (int x = left; x <= right; x++)
                for (int y = proof.Ceiling; y <= proof.Floor; y++)
                {
                    if (!ReadHoikTile(x, y, terrain)) return false;
                    Tile tile = Main.tile[x, y];
                    if (tile.liquid > 0) return false;
                    bool active = tile.active() && !tile.inActive();
                    if (active && HoikHazard(body, tile.type)) return false;
                    bool solid = active && Main.tileSolid[tile.type] && !Main.tileSolidTop[tile.type];
                    if (y == proof.Floor)
                    {
                        if (!solid || tile.slope() != 0 || tile.halfBrick()) return false;
                    }
                    else if (solid && (y != proof.Ceiling || !proof.ContainsTooth(x) ||
                        !HoikTooth(x, y, proof.Direction, terrain))) return false;
                }
            return true;
        }

        private static bool HoikHazard(Player body, int type) => TileID.Sets.TouchDamageImmediate[type] > 0 ||
            TileID.Sets.TouchDamageBleeding[type] || TileID.Sets.TouchDamageHot[type] && !body.fireWalk ||
            type == TileID.ConveyorBeltLeft || type == TileID.ConveyorBeltRight;

        private static bool TryHoikTick(Player body, CompanionMobilitySnapshot mobility, HoikPassage proof,
            Vector2 from, float velocityX, out Step next, ReadSet terrain = null)
        {
            next = default;
            float horizontal = MovementRun(body, mobility, airborne: false).Advance(velocityX, proof.Direction);
            if (Math.Abs(horizontal) > 8 || horizontal * proof.Direction <= 0) return false;
            using (Scratch.Save())
            {
                // Move first obtains the original TileCollision, SlopeCollision
                // and Step results. Its ordinary Clearance guard can reject
                // a temporary overlap under an active tooth; only this proven
                // constant floor corridor can accept that specific contact.
                MoveWithContact(body, from, new Vector2(horizontal, body.gravity), PlatformContact.Native(body, false, false),
                    true, out next, terrain);
            }
            return next.Known && next.Ground && Math.Abs(next.Position.Y - proof.FloorY) <= .125f &&
                (next.Position.X - from.X) * proof.Direction > .01f &&
                KnownHoikLane(body, proof, from.X, next.Position.X, terrain);
        }

        private static bool HoikDisplaced(Player body, RouteStep step, Vector2 previous, int deliveredDirection)
        {
            var proof = step.HoikProof;
            if (deliveredDirection != proof.Direction || Math.Abs(body.position.Y - proof.FloorY) > .125f ||
                (body.position.X - previous.X) * proof.Direction <= Math.Abs(body.velocity.X) + 2) return false;
            for (int tooth = proof.First; (proof.Last - tooth) * proof.Direction >= 0; tooth += 2 * proof.Direction)
            {
                float face = proof.Direction > 0 ? (tooth + 1) * 16 : tooth * 16 - proof.Width;
                float expelled = face - proof.Direction * (proof.FloorY - proof.Ceiling * 16);
                if (Math.Abs(body.position.X - expelled) <= .25f && HoikTooth(tooth, proof.Ceiling, proof.Direction, null)) return true;
            }
            return false;
        }

        private static bool HoikEngaged(Player body, CompanionMobilitySnapshot mobility, RouteStep step)
        {
            var proof = step.HoikProof;
            if (!CanHoik(body, mobility) || Math.Abs(body.position.Y - proof.FloorY) > .125f) return false;
            // A goal change can occur during the original slow approach with
            // the head already inside a live tooth. Keep that action in charge
            // only while it can still advance over its known support.
            for (int tooth = proof.First; (proof.Last - tooth) * proof.Direction >= 0; tooth += 2 * proof.Direction)
                if (body.position.X < (tooth + 1) * 16 && body.position.X + body.width > tooth * 16 &&
                    HoikTooth(tooth, proof.Ceiling, proof.Direction, null))
                    return TryHoikTick(body, mobility, proof, body.position, body.velocity.X, out _);
            return false;
        }

        private static void DriveHoikStep(Player body, CompanionMobilitySnapshot mobility, RouteMotion motion,
            RouteStep step, bool displaced, out CompanionMobilityFrame frame, out HoikOutcome outcome)
        {
            frame = new CompanionMobilityFrame { Status = "通过斜齿" };
            var proof = step.HoikProof;
            outcome = HoikOutcome.Blocked;
            if (!CanHoik(body, mobility) || body.width != proof.Width || body.height != proof.Height) return;
            if (Math.Abs(body.position.Y - proof.FloorY) > .125f)
            {
                if (motion == RouteMotion.Ground) outcome = HoikOutcome.LandedElsewhere;
                return;
            }
            if ((body.position.X - step.To.X) * proof.Direction >= -.125f)
            {
                if (motion == RouteMotion.Ground && SafeBody(body, body.position))
                    outcome = displaced ? HoikOutcome.Reached : HoikOutcome.LandedElsewhere;
                return;
            }
            if ((body.position.X - step.From.X) * proof.Direction < -6 || Math.Abs(body.velocity.Y) > NativeSlopeRestSpeed ||
                !TryHoikTick(body, mobility, proof, body.position, body.velocity.X, out _)) return;
            // Keep the original walking input; the slope collision itself
            // supplies the displacement when the body meets the active tooth.
            frame.Move = proof.Direction;
            outcome = HoikOutcome.Running;
        }
    }
}
