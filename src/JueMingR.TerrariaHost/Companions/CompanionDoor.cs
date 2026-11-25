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

        internal static bool AtHorizontalInlet(Player body, int direction, out CompanionDoor door)
        {
            door = default;
            if (direction == 0) return false;
            float front = direction > 0 ? body.Right.X : body.Left.X;
            int first = (int)Math.Floor((front + direction * .1f) / 16f);
            int last = (int)Math.Floor((front + direction * 16f) / 16f);
            int row = (int)Math.Floor(body.Center.Y / 16f);
            for (int x = first; x != last + direction; x += direction)
                for (int y = row - 1; y <= row + 1; y++)
                    if (TryRead(new Point(x, y), out door) && !door.Open && door.Family != CompanionDoorFamily.Trapdoor &&
                        door.AtApproach(body, direction)) return true;
            door = default;
            return false;
        }

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
                    if (!CompanionMobilityGeometry.Known(Hinge.X + direction, y) || Main.tile[Hinge.X + direction, y].active()) return false;
            }
            else
            {
                int y = Hinge.Y + direction;
                for (int x = Tiles.Left; x < Tiles.Right; x++)
                    if (!CompanionMobilityGeometry.Known(x, y) || Main.tile[x, y].active()) return false;
            }
            return true;
        }
    }

}
