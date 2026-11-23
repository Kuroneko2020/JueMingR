using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionMobilityGeometry
    {
        private enum RodPosition { Clear, FootprintBlocked, Unknown, Unreachable }
        private delegate void PortalEdges(Vector2 position, float angle, out Vector2 start, out Vector2 end);
        private delegate Vector2 PortalOut(Vector2 size, Vector2 point, float angle, out int dx, out int dy);

        private static readonly AccessTools.FieldRef<int[,]> portals = AccessTools.StaticFieldRefAccess<int[,]>(AccessTools.Field(typeof(PortalHelper), "FoundPortals"));
        private static readonly AccessTools.FieldRef<bool> anyPortal = AccessTools.StaticFieldRefAccess<bool>(AccessTools.Field(typeof(PortalHelper), "anyPortalAtAll"));
        private static readonly AccessTools.FieldRef<int[]> portalCooldown = AccessTools.StaticFieldRefAccess<int[]>(AccessTools.Field(typeof(PortalHelper), "PortalCooldownForPlayers"));
        private static readonly PortalEdges portalEdges = AccessTools.MethodDelegate<PortalEdges>(AccessTools.Method(typeof(PortalHelper), "GetPortalEdges"));
        private static readonly PortalOut portalOut = AccessTools.MethodDelegate<PortalOut>(AccessTools.Method(typeof(PortalHelper), "GetPortalOutingPoint"));

        internal static bool HasActivePortal => anyPortal();

        internal static bool MayReachPylon(Rectangle tileArea)
        {
            var pylons = Main.PylonSystem?.Pylons;
            if (pylons == null || pylons.Count == 0) return false;
            if (pylons.Count > 24) return true;
            TileReachCheckSettings.Pylons.GetRanges(out int x, out int y);
            tileArea.Inflate(x + 1, y + 1);
            foreach (var pylon in pylons)
                if (tileArea.Intersects(new Rectangle(pylon.PositionInTiles.X, pylon.PositionInTiles.Y, 3, 4))) return true;
            return false;
        }

        internal static bool Known(int x, int y) => Main.tile != null && x >= 0 && y >= 0 && x < Main.maxTilesX && y < Main.maxTilesY &&
            (Main.netMode != 1 || Main.sectionManager != null && Main.sectionManager.TileLoaded(x, y)) && Main.tile[x, y] != null;

        internal static bool KnownBox(Vector2 start, Vector2 end)
        {
            Point first = start.ToTileCoordinates(), last = end.ToTileCoordinates();
            for (int y = first.Y; y <= last.Y; y++)
                for (int x = first.X; x <= last.X; x++)
                    if (!Known(x, y)) return false;
            return true;
        }

        internal static bool ClearBody(Vector2 position, int width, int height, int margin = 0)
        {
            var border = new Vector2(margin);
            Vector2 end = position + new Vector2(width, height);
            if (!KnownBox(position - border, end + border) || Collision.SolidCollision(position, width, height) ||
                Collision.LavaCollision(position, width, height)) return false;
            Point first = position.ToTileCoordinates(), last = end.ToTileCoordinates();
            for (int y = first.Y; y <= last.Y; y++)
                for (int x = first.X; x <= last.X; x++)
                    if (Main.tile[x, y].liquid > 0 && Main.tile[x, y].shimmer()) return false;
            return true;
        }

        private static bool Anchor(Tile tile, int type) => tile.nactive() && !TileID.Sets.CrackedBricks[tile.type] &&
            (type == ProjectileID.TrackHook ? tile.type == TileID.MinecartTrack :
            Main.tileSolid[tile.type] || tile.type == TileID.MinecartTrack || type == 865 &&
            (TileID.Sets.IsATreeTrunk[tile.type] || tile.type == 323));

        internal static bool HookAnchor(Player body, Vector2 target, int type, float range, bool swept)
        {
            var anchor = target.ToTileCoordinates();
            float distance = Vector2.Distance(body.Center, target);
            if (distance < 8f || distance > range || !Known(anchor.X, anchor.Y) || !Anchor(Main.tile[anchor.X, anchor.Y], type) ||
                body.IsBlacklistedForGrappling(anchor)) return false;
            if (!swept) return true;

            // Hook first contact can differ from a center-to-center ray. Test
            // the projectile footprint and the larger body stopping at the
            // selected surface. Terrarian projectiles still decide attachment.
            Vector2 delta = (target - body.Center) / distance;
            var halfSize = body.Size * .5f;
            float bodyDistance = distance - 8f - Math.Abs(delta.X) * halfSize.X - Math.Abs(delta.Y) * halfSize.Y;
            for (float d = 0; d < distance; d += 6f)
            {
                var point = body.Center + delta * d;
                var first = (point - new Vector2(5f)).ToTileCoordinates();
                // AI_007's overlap test is strict at the far edges. A hook
                // touching the next cell boundary cannot latch to it yet.
                var last = (point + new Vector2(4.999f)).ToTileCoordinates();
                for (int y = first.Y; y <= last.Y; y++)
                    for (int x = first.X; x <= last.X; x++)
                    {
                        if (!Known(x, y)) return false;
                        var tile = Main.tile[x, y];
                        if (tile.nactive() && TileID.Sets.CrackedBricks[tile.type] ||
                            (x != anchor.X || y != anchor.Y) && Anchor(tile, type)) return false;
                    }
                // The original position is already occupied by the original
                // body. Native floor contact can retain a fractional overlap;
                // require clearance along the new motion from the first step.
                if (d > 0 && d <= bodyDistance && !ClearBody(point - halfSize, body.width, body.height)) return false;
            }
            return true;
        }

        internal static bool TrackEntry(Player body, Vector2 target)
        {
            Point tile = target.ToTileCoordinates();
            return body.gravDir > 0 && !body.mount.Active && Known(tile.X, tile.Y) &&
                Main.tile[tile.X, tile.Y].nactive() && Main.tile[tile.X, tile.Y].type == TileID.MinecartTrack &&
                body.IsInTileInteractionRange(tile.X, tile.Y, TileReachCheckSettings.Simple) &&
                KnownBox(Vector2.Min(body.Center, target) - new Vector2(16), Vector2.Max(body.Center, target) + new Vector2(16)) &&
                Collision.CanHitLine(body.Center, 0, 0, target, 0, 0);
        }

        internal static bool TryTeleporter(Player body, Vector2 desiredCenter, out Vector2 point, out Vector2 exit) =>
            CompanionMobilityWiring.Find(body, desiredCenter, out point, out exit);

        internal static bool TryPylon(Player body, Vector2 desiredCenter, out Vector2 point, out Vector2 exit)
        {
            point = exit = default(Vector2);
            var pylons = Main.PylonSystem?.Pylons;
            if (pylons == null || !NearPylon(body)) return false;
            float nearest = 96f * 96f;
            bool found = false;
            foreach (var info in pylons)
            {
                var destination = PylonTopLeft(body, info) + body.Size * .5f;
                float distance = Vector2.DistanceSquared(destination, desiredCenter);
                if (distance >= nearest || Vector2.DistanceSquared(body.Center, destination) < 112f * 112f || !PylonExit(body, info)) continue;
                nearest = distance;
                point = info.PositionInTiles.ToWorldCoordinates();
                exit = PylonTopLeft(body, info);
                found = true;
            }
            return found;
        }

        internal static bool PylonAt(Player body, Vector2 target, out TeleportPylonInfo destination)
        {
            destination = default(TeleportPylonInfo);
            var pylons = Main.PylonSystem?.Pylons;
            if (pylons == null || !NearPylon(body)) return false;
            Point tile = target.ToTileCoordinates();
            foreach (var info in pylons)
                if (info.PositionInTiles.X == tile.X && info.PositionInTiles.Y == tile.Y && PylonExit(body, info))
                { destination = info; return true; }
            return false;
        }

        private static bool NearPylon(Player body)
        {
            foreach (var info in Main.PylonSystem.Pylons)
                if (PylonPresent(info) &&
                    body.InTileEntityInteractionRange(info.PositionInTiles.X, info.PositionInTiles.Y, 3, 4, TileReachCheckSettings.Pylons)) return true;
            return false;
        }

        private static Vector2 PylonTopLeft(Player body, TeleportPylonInfo info) => info.PositionInTiles.ToWorldCoordinates() - new Vector2(0, body.HeightOffsetBoost);
        private static bool PylonExit(Player body, TeleportPylonInfo info) => PylonPresent(info) &&
            ClearBody(PylonTopLeft(body, info), body.width, body.height);
        private static bool PylonPresent(TeleportPylonInfo info)
        {
            int x = info.PositionInTiles.X, y = info.PositionInTiles.Y;
            return Known(x, y) && Main.tile[x, y].nactive() && Main.tile[x, y].type == TileID.TeleportationPylon;
        }

        internal static bool TryPortal(Player body, Vector2 desiredCenter, out Vector2 entry, out Vector2 exitTopLeft)
        {
            entry = exitTopLeft = Vector2.Zero;
            if (!anyPortal() || body.mount.Active) return false;
            var known = portals();
            float nearest = 112f * 112f;
            bool found = false;
            for (int owner = 0; owner < known.GetLength(0); owner++)
                for (int side = 0; side < 2; side++)
                {
                    if (!PortalPair(known, owner, side, out var from, out var to) ||
                        Vector2.DistanceSquared(body.Center, from.Center) > 192f * 192f) continue;
                    var exit = portalOut(body.Size, to.Center, to.ai[0], out _, out _);
                    float distance = Vector2.DistanceSquared(exit + body.Size * .5f, desiredCenter);
                    if (distance >= nearest || !ClearBody(exit, body.width, body.height, 32)) continue;
                    nearest = distance;
                    entry = from.Center;
                    exitTopLeft = exit;
                    found = true;
                }
            return found;
        }

        private static bool PortalPair(int[,] known, int owner, int side, out Projectile from, out Projectile to)
        {
            from = to = null;
            int first = known[owner, side], second = known[owner, 1 - side];
            if (first < 0 || second < 0 || portalCooldown()[owner] > 0) return false;
            from = Main.projectile[first];
            to = Main.projectile[second];
            return from.active && to.active && from.owner == owner && to.owner == owner && from.ai[1] == side && to.ai[1] == 1 - side &&
                from.type == ProjectileID.PortalGunGate && to.type == ProjectileID.PortalGunGate;
        }

        internal static bool PortalEntry(Player body, Vector2 target, out Vector2 start, out Vector2 end)
        {
            start = end = default(Vector2);
            if (!anyPortal() || body.mount.Active || Vector2.DistanceSquared(body.Center, target) > 96f * 96f) return false;
            var known = portals();
            for (int owner = 0; owner < known.GetLength(0); owner++)
                for (int side = 0; side < 2; side++)
                {
                    if (!PortalPair(known, owner, side, out var from, out var to) || from.Center != target) continue;
                    portalEdges(from.Center, from.ai[0], out start, out end);
                    float collision = 0f;
                    if (!Collision.CheckAABBvLineCollision(body.position + body.velocity, body.Size, start, end, 2f, ref collision)) continue;
                    var exit = portalOut(body.Size, to.Center, to.ai[0], out int dx, out int dy) +
                        Vector2.Normalize(new Vector2(dx, dy)) * body.Hitbox.Distance(from.Center);
                    return !float.IsNaN(exit.X) && !float.IsNaN(exit.Y) && ClearBody(exit, body.width, body.height, 18);
                }
            return false;
        }

        internal static bool RodAim(Player body, Vector2 bottom, bool correct, out Vector2 aim)
        {
            aim = default(Vector2);
            if (float.IsNaN(bottom.X) || float.IsNaN(bottom.Y) || float.IsInfinity(bottom.X) || float.IsInfinity(bottom.Y)) return false;
            Vector2 original = bottom - new Vector2(body.width / 2, body.height);
            int limit = correct ? 5 : 0;
            // A correction stays in the route's immediate landing area. Each
            // pixel target is validated after the real Integer mouse rounding.
            for (int r = 0; r <= limit; r++)
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                    {
                        if (Math.Max(Math.Abs(x), Math.Abs(y)) != r) continue;
                        var position = original + new Vector2(x * 8, y * 8);
                        aim = position + new Vector2(body.width / 2, body.gravDir > 0 ? body.height : 0);
                        var pixel = aim - Main.screenPosition;
                        position.X = (int)pixel.X + Main.screenPosition.X - body.width / 2;
                        position.Y = body.gravDir > 0 ? (int)pixel.Y + Main.screenPosition.Y - body.height :
                            Main.screenPosition.Y + Main.screenHeight - (int)(Main.screenHeight - pixel.Y);
                        var result = RodDestination(body, position);
                        if (result == RodPosition.Clear) return true;
                        // A collision correction stays inside this known
                        // destination area. A missing section or a native wall
                        // veto cannot be converted into an adjacent guess.
                        if (r == 0 && result != RodPosition.FootprintBlocked) return false;
                        if (r != 0 && result == RodPosition.Unreachable) return false;
                    }
            return false;
        }

        private static RodPosition RodDestination(Player body, Vector2 point)
        {
            if (point.X <= 50 || point.Y <= 50 || point.X >= Main.maxTilesX * 16f - 50 || point.Y >= Main.maxTilesY * 16f - 50) return RodPosition.Unreachable;
            Vector2 limited = point;
            body.LimitPointToPlayerReachableArea(ref limited);
            if (Vector2.DistanceSquared(point, limited) > .0001f) return RodPosition.Unreachable;
            if (!KnownBox(point - new Vector2(16), point + body.Size + new Vector2(16))) return RodPosition.Unknown;
            Point tile = point.ToTileCoordinates();
            if (!NPC.downedPlantBoss && Main.tile[tile.X, tile.Y].wall == 87 && (Main.remixWorld || tile.Y > Main.worldSurface)) return RodPosition.Unreachable;
            if (!ClearBody(point, body.width, body.height)) return RodPosition.FootprintBlocked;
            Point from = body.Center.ToTileCoordinates(), to = (point + new Vector2(body.width / 2, body.height / 2)).ToTileCoordinates();
            int steps = Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
            // Terraria's wall query reads neighboring cells during each line
            // step. Validate their loaded section before calling the native ray.
            for (int step = 0; step <= steps; step++)
            {
                int x = from.X + (steps == 0 ? 0 : (to.X - from.X) * step / steps);
                int y = from.Y + (steps == 0 ? 0 : (to.Y - from.Y) * step / steps);
                if (!KnownBox(new Vector2((x - 2) * 16, (y - 2) * 16), new Vector2((x + 2) * 16, (y + 2) * 16))) return RodPosition.Unknown;
            }
            return Collision.AnyWallOfTypeOnLine(from.X, from.Y, to.X, to.Y, 350) ? RodPosition.Unreachable : RodPosition.Clear;
        }
    }
}
