using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionGroundRun
    {
        internal static bool CanDash(Player body, int direction, int dash, float goalX)
        {
            float impulse;
            switch (dash)
            {
                case 1: case 5: impulse = 16.9f; break;
                case 2: impulse = 14.5f; break;
                case 3: impulse = 21.9f; break;
                // The mounted impulse couples horizontal and vertical
                // movement. Do not borrow the unmounted braking constants.
                default: return false;
            }
            if (body.wet || body.velocity.Y != 0 || !TryStopDistance(body, impulse, dash, out float braking)) return false;
            float span = impulse + braking + 6f;
            return (goalX - body.Center.X) * direction >= span && TryFlatLane(body, direction * span, true, trailingSupport: true);
        }

        internal static bool HasBrakingRoom(Player body, int heading, float speed, int dash)
        {
            if (!TryStopDistance(body, speed, dash, out float distance)) return false;
            if (distance < 16f)
                return CompanionMotionAction.ClearAhead(body, heading, Math.Max(3f, distance), true);
            float span = distance + speed + 2f;
            return TryFlatLane(body, heading * span, true, trailingSupport: true) ||
                dash == 0 && span <= 48f && TryStepLane(body, heading * span, 18f);
        }

        internal static bool TryStepLane(Player body, float delta, float heightTolerance)
        {
            if (Math.Abs(delta) > 48f) return false;
            Vector2 position = body.position;
            float remaining = Math.Abs(delta);
            int heading = Math.Sign(delta);
            var contact = CompanionNativeContact.PlatformContact.Native(body, false, false);
            while (remaining > .01f)
            {
                float step = Math.Min(remaining, 8f);
                var move = new Vector2(heading * step, Math.Max(.05f, body.gravity) * body.gravDir);
                if (!CompanionNativeContact.TryMove(body, position, move, contact, true, out var result) || result.Wall ||
                    !result.Ground && !CompanionNativeContact.Supported(body, result.Position) ||
                    !body.lavaImmune && !body.lavaWet && body.lavaTime <= 24 &&
                    CompanionMobilityGeometry.TouchesLava(result.Position, body.width, body.height) ||
                    CompanionMobilityGeometry.TouchesShimmer(result.Position, body.width, body.height)) return false;
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
                    if (!body.lavaImmune && body.lavaTime <= 24 && CompanionMobilityGeometry.TouchesLava(down.Position, body.width, body.height) ||
                        CompanionMobilityGeometry.TouchesShimmer(down.Position, body.width, body.height)) return false;
                    return true;
                }
                position = down.Position;
            }
            return false;
        }

        internal static bool TryFlatLane(Player body, float delta, bool proveBrakingSurface, bool trailingSupport = false)
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
            // On an aligned, unbroken slab the swept box and its support can
            // be proved from one tile rectangle. Slopes, ledges and moving
            // surfaces stay on the native per-step geometry path below.
            for (int x = left; x <= right; x++)
            {
                if (x >= supportLeft && x <= supportRight)
                {
                    if (!CompanionNativeContact.Loaded(x, floor)) return false;
                    Tile support = Main.tile[x, floor];
                    if (!support.active() || support.inActive() || support.slope() != 0 || support.halfBrick() ||
                        !Main.tileSolid[support.type] || Main.tileSolidTop[support.type] || ChangesGroundSpeed(body, support.type) && proveBrakingSurface)
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

        private static bool TryStopDistance(Player body, float speed, int dash, out float distance)
        {
            distance = 0;
            float slowdown = body.runSlowdown;
            if (!(slowdown > 0) || body.windPushed || body.trackBoost != 0 || float.IsInfinity(speed) || float.IsNaN(speed)) return false;
            float runSpeed = Math.Max(body.accRunSpeed, body.maxRunSpeed);
            bool strongerDecay = dash == 2 || dash == 3;
            float fastThreshold = dash == 3 ? 14 : 12;
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
                    if (speed > fastThreshold) speed *= strongerDecay ? .985f : .992f;
                    else if (speed > runSpeed) speed *= strongerDecay ? .94f : .96f;
                    else { speed = runSpeed; dash = 0; }
                }
                distance += speed;
            }
            return speed == 0;
        }

    }
}
