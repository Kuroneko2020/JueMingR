using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ObjectData;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum CompanionDoorFamily : byte { None, Door, Gate, Trapdoor }

    internal struct CompanionDoor
    {
        internal CompanionDoorFamily Family;
        internal Point Hinge;
        internal Rectangle Tiles;
        internal int Style;
        internal bool Open;

        internal Vector2 Target => new Vector2(Hinge.X * 16 + 8, Hinge.Y * 16 + 8);
        internal Rectangle Aperture => new Rectangle(Hinge.X * 16, Hinge.Y * 16,
            Family == CompanionDoorFamily.Trapdoor ? 32 : 16, Family == CompanionDoorFamily.Gate ? 80 : Family == CompanionDoorFamily.Door ? 48 : 16);

        internal static bool TryRead(Point point, out CompanionDoor door)
        {
            door = default;
            if (!CompanionMobilityGeometry.Known(point.X, point.Y)) return false;
            var tile = Main.tile[point.X, point.Y];
            if (!tile.nactive() || tile.frameX < 0 || tile.frameY < 0 || tile.frameX % 18 != 0) return false;
            int x = point.X, y = point.Y, spanX = 1, spanY, frameX = tile.frameX, frameY = tile.frameY;
            TileObjectData gate = null;
            switch (tile.type)
            {
                case TileID.ClosedDoor:
                case TileID.OpenDoor:
                    door.Family = CompanionDoorFamily.Door;
                    door.Open = tile.type == TileID.OpenDoor;
                    spanY = 3;
                    frameY -= frameY % 54;
                    y -= tile.frameY % 54 / 18;
                    if (door.Open)
                    {
                        spanX = 2;
                        x -= tile.frameX % 36 / 18;
                        frameX -= frameX % 36;
                    }
                    door.Hinge = new Point(x + (door.Open ? frameX / 36 % 2 : 0), y);
                    break;
                case TileID.TallGateClosed:
                case TileID.TallGateOpen:
                    door.Family = CompanionDoorFamily.Gate;
                    door.Open = tile.type == TileID.TallGateOpen;
                    gate = TileObjectData.GetTileData(TileID.TallGateClosed, frameX / 18);
                    if (gate == null || gate.Width != 1 || gate.Height != 5) return false;
                    spanY = gate.Height;
                    int rowFrame = frameY % gate.CoordinateFullHeight;
                    frameY -= rowFrame;
                    int row = 0;
                    while (row < spanY && rowFrame > 0) rowFrame -= gate.CoordinateHeights[row++] + gate.CoordinatePadding;
                    if (row == spanY || rowFrame != 0) return false;
                    y -= row;
                    door.Hinge = new Point(x, y);
                    break;
                case TileID.TrapdoorClosed:
                case TileID.TrapdoorOpen:
                    door.Family = CompanionDoorFamily.Trapdoor;
                    door.Open = tile.type == TileID.TrapdoorOpen;
                    spanX = 2;
                    spanY = door.Open ? 2 : 1;
                    x -= tile.frameX % 36 / 18;
                    y -= tile.frameY % (spanY * 18) / 18;
                    frameX -= frameX % 36;
                    frameY -= frameY % (spanY * 18);
                    door.Style = frameX / 36;
                    if (door.Style > (door.Open ? 1 : 0)) return false;
                    door.Hinge = new Point(x, y + (door.Open && door.Style == 0 ? 1 : 0));
                    break;
                default: return false;
            }
            if (gate == null && tile.frameY % 18 != 0) return false;
            door.Tiles = new Rectangle(x, y, spanX, spanY);
            for (int ix = 0; ix < spanX; ix++)
            {
                int expectedY = frameY;
                for (int iy = 0; iy < spanY; iy++)
                {
                    if (!CompanionMobilityGeometry.Known(x + ix, y + iy)) return false;
                    var part = Main.tile[x + ix, y + iy];
                    if (!part.nactive() || part.type != tile.type || part.frameY != expectedY ||
                        (tile.type == TileID.ClosedDoor ? part.frameX < 0 || part.frameX % 18 != 0 || part.frameX / 54 != frameX / 54 :
                            part.frameX != frameX + ix * 18))
                        return false;
                    expectedY += gate == null ? 18 : gate.CoordinateHeights[iy] + gate.CoordinatePadding;
                }
            }
            return true;
        }

        internal bool InReach(Player body, TileReachCheckSettings reach)
        {
            Rectangle opening = Aperture;
            Point cell = new Point(Math.Max(Tiles.Left, Math.Min(Tiles.Right - 1, (int)body.Center.X / 16)),
                Math.Max(Tiles.Top, Math.Min(Tiles.Bottom - 1, (int)body.Center.Y / 16)));
            return body.IsInTileInteractionRange(cell.X, cell.Y, reach) &&
                CompanionMobilityGeometry.KnownBox(new Vector2(opening.Left - 32, opening.Top - 32),
                    new Vector2(opening.Right + 32, opening.Bottom + 32));
        }

        internal bool AtApproach(Player body, int passage)
        {
            Rectangle area = Aperture;
            if (Family == CompanionDoorFamily.Trapdoor)
                return body.Right.X > area.Left && body.Left.X < area.Right &&
                    (passage > 0 ? Math.Abs(body.Bottom.Y - area.Top) : Math.Abs(body.Top.Y - area.Bottom)) <= 40;
            int front = (int)(passage > 0 ? body.Right.X : body.Left.X);
            int edge = passage > 0 ? area.Left : area.Right;
            return Math.Abs(front - edge) <= 8 && body.Bottom.Y > area.Top && body.Top.Y < area.Bottom;
        }

        internal bool SwingClear(int direction)
        {
            if (Family == CompanionDoorFamily.Gate) return true;
            if (Family == CompanionDoorFamily.Door)
            {
                if (WorldGen.IsLockedDoor(Main.tile[Hinge.X, Hinge.Y])) return false;
                for (int y = Tiles.Top; y < Tiles.Bottom; y++)
                    if (Main.tile[Hinge.X + direction, y].active()) return false;
            }
            else
            {
                int y = Hinge.Y + direction;
                for (int x = Tiles.Left; x < Tiles.Right; x++)
                    if (Main.tile[x, y].active()) return false;
            }
            return true;
        }
    }

    internal sealed partial class CompanionNavigation
    {
        private static bool TryDoorEdge(Player body, Vector2 from, int direction, ReadSet terrain, out Vector2 door, out Vector2 to)
        {
            door = to = default;
            float front = from.X + (direction > 0 ? body.width : 0);
            int first = (int)Math.Floor((front + direction * .1f) / 16), last = (int)Math.Floor((front + direction * 18) / 16);
            int center = (int)(from.Y + body.height * .5f) / 16;
            for (int x = first; x != last + direction; x += direction)
                for (int y = center - 1; y <= center + 1; y++)
                {
                    if (!(terrain == null ? Loaded(x, y) : terrain.Record(x, y)) || !CompanionDoor.TryRead(new Point(x, y), out var fixture) || fixture.Open ||
                        fixture.Family == CompanionDoorFamily.Trapdoor) continue;
                    Rectangle aperture = fixture.Aperture;
                    if (from.Y < aperture.Top || from.Y + body.height > aperture.Bottom) continue;
                    to = new Vector2(direction > 0 ? aperture.Right + 4 : aperture.Left - body.width - 4, from.Y);
                    if (!DoorCorridor(body, from, to, fixture, terrain) || !Surface(body, to, terrain)) continue;
                    door = fixture.Target;
                    return true;
                }
            return false;
        }

        private static bool TryTrapdoorEdge(Player body, Vector2 from, int vertical, ReadSet terrain, ref RouteStep step, out RouteMotion motion)
        {
            motion = RouteMotion.Air;
            if (body.gravDir != 1 || body.width > 32 || body.wet) return false;
            int x = (int)(from.X + body.width * .5f) / 16;
            float at = from.Y + (vertical > 0 ? body.height : 0);
            int first = (int)Math.Floor((at + vertical * .2f) / 16);
            for (int y = first; Math.Abs(y - first) <= 2; y += vertical)
            {
                if (!(terrain == null ? Loaded(x, y) : terrain.Record(x, y)) || !CompanionDoor.TryRead(new Point(x, y), out var fixture) ||
                    fixture.Family != CompanionDoorFamily.Trapdoor || fixture.Open) continue;
                Rectangle aperture = fixture.Aperture;
                float center = aperture.Center.X - body.width * .5f;
                if (Math.Abs(from.X - center) > 12) continue;
                Vector2 to = new Vector2(center, vertical > 0 ? aperture.Bottom + 4 : aperture.Top - body.height - 4);
                if (!DoorCorridor(body, from, new Vector2(center, from.Y), fixture, terrain) ||
                    !DoorCorridor(body, new Vector2(center, from.Y), to, fixture, terrain)) continue;
                if (vertical > 0 && !TrapdoorLanding(body, to, fixture, terrain)) continue;
                step.Kind = RouteKind.Door;
                step.Operand = fixture.Target;
                step.To = to;
                step.Direction = 0;
                step.Vertical = vertical;
                step.Cost = 45 + Vector2.Distance(from, to);
                return true;
            }
            return false;
        }

        private static bool TrapdoorLanding(Player body, Vector2 start, CompanionDoor door, ReadSet terrain)
        {
            // TileCollision looks in the current neighborhood. Follow short
            // segments until it actually finds support below the opening.
            var down = new Vector2(0, 8);
            for (int step = 0; step < 8; step++)
            {
                Vector2 from = start + down * step;
                if (!Footprint(body, from, down, terrain)) return false;
                Vector2 contact = Collision.TileCollision(from, down, body.width, body.height, fallThrough: true, fall2: true);
                if (!DoorCorridor(body, from, from + contact, door, terrain)) return false;
                if (contact.Y < down.Y) return true;
            }
            return false;
        }

        private static bool DoorCorridor(Player body, Vector2 from, Vector2 to, CompanionDoor door, ReadSet terrain)
        {
            // Only the confirmed fixture is hypothetically passable. Other
            // doors, solids and hazards keep their actual state in the sweep.
            if (!Footprint(body, from, to - from, terrain)) return false;
            Vector2 near = Vector2.Min(from, to), far = Vector2.Max(from, to) + body.Size - new Vector2(.05f);
            Point min = near.ToTileCoordinates(), max = far.ToTileCoordinates();
            for (int x = min.X; x <= max.X; x++)
                for (int y = min.Y; y <= max.Y; y++)
                {
                    var tile = Main.tile[x, y];
                    if (tile.liquid > 0 && (tile.lava() || tile.shimmer())) return false;
                    if (door.Tiles.Contains(x, y)) continue;
                    if (tile.nactive() && Main.tileSolid[tile.type] && !Main.tileSolidTop[tile.type])
                    {
                        if (tile.halfBrick() && far.Y < y * 16 + 8) continue;
                        return false;
                    }
                }
            return true;
        }

        private bool DriveDoorStep(Player body, CompanionMobilityState state, RouteStep step,
            out CompanionMobilityFrame frame, out bool finished)
        {
            frame = new CompanionMobilityFrame { Status = "接近门口" };
            finished = false;
            if (!CompanionDoor.TryRead(step.Operand.ToTileCoordinates(), out var door) || door.Target != step.Operand ||
                (door.Family == CompanionDoorFamily.Trapdoor) != (step.Vertical != 0)) return false;
            bool trap = step.Vertical != 0;
            int passage = trap ? step.Vertical : step.Direction;
            float remaining = trap ? (step.To.Y - body.position.Y) * passage : (step.To.X - body.position.X) * passage;
            if (door.Open && remaining <= 4) { finished = true; return false; }
            if (!door.Open && commandSequence != 0 && state.Sequence == commandSequence && state.Action == CompanionMovementActivation.Door &&
                (state.Result == CompanionMobilityResult.Rejected || state.Result == CompanionMobilityResult.Completed)) return false;

            if (trap)
            {
                float dx = step.To.X - body.position.X;
                if (Math.Abs(dx) > 3 && (DoorCorridor(body, body.position, body.position + new Vector2(Math.Sign(dx) * 4, 0), door, null)))
                    frame.Move = Math.Sign(dx);
                if (door.Open && !DoorCorridor(body, body.position, body.position + new Vector2(0, passage * 4), door, null)) return false;
                if (passage < 0)
                {
                    var mobility = state.Observed;
                    bool grounded = body.velocity.Y == 0f && Surface(body, body.position, null);
                    var motion = CurrentMotion(body, mobility, grounded);
                    if (door.Open)
                    {
                        if (!jumpStarted && (motion == RouteMotion.Ground || body.sliding) && body.releaseJump)
                            jumpStarted = frame.Jump = true;
                        else if (motion == RouteMotion.Air && AirInput(body, mobility, motion, frame.Move, -1, out var ascent))
                        {
                            // The door transition can start in the middle of
                            // legitimate wing flight or a powered ascent.
                            frame.Move = ascent.Move;
                            frame.Jump = ascent.Jump;
                            frame.Vertical = ascent.Vertical;
                        }
                    }
                    else if (motion == RouteMotion.Air && mobility.JumpTime <= 0 && body.velocity.Y >= 0 && !CanPowerAscent(mobility))
                        return false;
                }
                if (!door.Open && Math.Abs(dx) > 5) return frame.Move != 0;
            }
            else
            {
                Vector2 advance = body.position + new Vector2(passage * 5, 0);
                if (!DoorCorridor(body, body.position, advance, door, null)) return false;
                frame.Move = passage;
            }

            if (door.Open) { frame.Status = "正在过门"; return true; }
            if (!door.AtApproach(body, passage)) return !trap;
            if (commandSequence == 0) commandSequence = ++sequence;
            actionStarted = true;
            frame.Activation = CompanionMovementActivation.Door;
            frame.WorldX = step.Operand.X;
            frame.WorldY = step.Operand.Y;
            frame.Sequence = commandSequence;
            frame.DoorFamily = door.Family;
            frame.DoorPass = passage;
            frame.Status = "等待开门";
            return true;
        }
    }
}
