using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionHoikContact
    {
        internal static bool Matches(Player body, Vector2 startFeet, Vector2 endFeet)
        {
            if (body.gravDir != 1 || body.wet || body.mount.Active || body.pulley || body.grappling[0] >= 0 ||
                body.width != Player.defaultWidth || body.height != Player.defaultHeight) return false;
            var from = CompanionNativeContact.TopLeft(body, startFeet);
            var to = CompanionNativeContact.TopLeft(body, endFeet);
            int direction = Math.Sign(to.X - from.X);
            if (direction == 0 || Math.Abs(to.X - from.X) > 84f || Math.Abs(to.Y - from.Y) > .125f) return false;
            int ceiling = (int)Math.Floor(from.Y / 16f);
            int floor = (int)Math.Round(startFeet.Y / 16f);
            float overlap = (ceiling + 1) * 16f - from.Y;
            if (overlap < 4f || overlap > 12f || Math.Abs(startFeet.Y - floor * 16f) > .125f || floor - ceiling < 2)
                return false;
            int first = (int)Math.Floor(Math.Min(from.X, to.X) / 16f);
            int last = (int)Math.Floor((Math.Max(from.X, to.X) + body.width - .01f) / 16f);
            int teeth = 0;
            for (int x = first; x <= last; x++)
                for (int y = ceiling; y <= floor; y++)
                {
                    if (!CompanionNativeContact.Loaded(x, y)) return false;
                    var tile = Main.tile[x, y];
                    if (tile.liquid > 0) return false;
                    bool active = tile.nactive();
                    if (active && (TileID.Sets.TouchDamageImmediate[tile.type] > 0 || TileID.Sets.TouchDamageBleeding[tile.type] ||
                        TileID.Sets.TouchDamageHot[tile.type] && !body.fireWalk || TileID.Sets.ConveyorDirection[tile.type] != 0))
                        return false;
                    bool solid = active && Main.tileSolid[tile.type] && !Main.tileSolidTop[tile.type];
                    if (y == floor)
                    {
                        if (!solid || tile.slope() != 0 || tile.halfBrick()) return false;
                    }
                    else if (solid)
                    {
                        if (y != ceiling || !IsTooth(tile, x - direction, y, direction)) return false;
                        teeth++;
                    }
                }
            // An unobstructed ordinary lane is not a native wedge transfer.
            return teeth != 0;
        }

        private static bool IsTooth(Tile tile, int entranceX, int y, int direction)
        {
            if (tile.slope() != (direction > 0 ? 3 : 4) || !CompanionNativeContact.Loaded(entranceX, y)) return false;
            Tile entrance = Main.tile[entranceX, y];
            return entrance.active() && entrance.inActive() && entrance.slope() == (direction > 0 ? 4 : 3);
        }
    }
}
