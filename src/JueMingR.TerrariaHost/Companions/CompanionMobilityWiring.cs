using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionMobilityWiring
    {
        private const int CircuitLimit = 192;
        private static readonly Point[] neighbors = { new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1) };
        [ThreadStatic] private static Circuit scratch;

        private sealed class Circuit
        {
            internal readonly HashSet<Point> Visited = new HashSet<Point>();
            internal readonly Point[] Pending = new Point[CircuitLimit];
        }

        // This is the small, dedicated pressure-independent circuit that we
        // can prove from loaded vanilla tiles. Other devices may have arbitrary
        // effects, including triggering another net, and are not traversable.
        internal static bool TryDestination(Player body, Point trigger, out Vector2 destination)
        {
            destination = Vector2.Zero;
            if (!CompanionMobilityGeometry.Known(trigger.X, trigger.Y) || !body.IsInTileInteractionRange(trigger.X, trigger.Y, TileReachCheckSettings.Simple)) return false;
            var start = Main.tile[trigger.X, trigger.Y];
            if (!start.nactive() || start.type != TileID.Switches || start.actuator()) return false;
            int color = Wires(start);
            if (color != 1 && color != 2 && color != 4 && color != 8) return false;

            var circuit = scratch ?? (scratch = new Circuit());
            var visited = circuit.Visited;
            var pending = circuit.Pending;
            visited.Clear();
            visited.Add(trigger);
            pending[0] = trigger;
            int end = 1, pads = 0;
            Point first = default(Point), second = default(Point);
            for (int next = 0; next < end; next++)
            {
                Point point = pending[next];
                var tile = Main.tile[point.X, point.Y];
                if (Wires(tile) != color || tile.actuator() || tile.inActive()) return false;
                if (point != trigger && tile.active())
                {
                    if (tile.type != TileID.Teleporter || tile.halfBrick() || tile.frameX < 0 || tile.frameX > 36 ||
                        tile.wall == 87 && point.Y > Main.worldSurface && !NPC.downedPlantBoss) return false;
                    var origin = new Point(point.X - tile.frameX / 18, point.Y);
                    if (pads == 0) { first = origin; pads++; }
                    else if (origin != first && pads == 1) { second = origin; pads++; }
                    else if (origin != first && origin != second) return false;
                }

                foreach (var offset in neighbors)
                {
                    var candidate = new Point(point.X + offset.X, point.Y + offset.Y);
                    if (visited.Contains(candidate)) continue;
                    if (!CompanionMobilityGeometry.Known(candidate.X, candidate.Y)) return false;
                    if ((Wires(Main.tile[candidate.X, candidate.Y]) & color) == 0) continue;
                    if (end == pending.Length) return false;
                    visited.Add(candidate);
                    pending[end++] = candidate;
                }
            }

            if (pads != 2 || !Pad(first) || !Pad(second) || Math.Abs(first.X - second.X) < 3 && Math.Abs(first.Y - second.Y) < 3) return false;
            var box1 = new Rectangle(first.X * 16, first.Y * 16 - 48, 48, 48);
            var box2 = new Rectangle(second.X * 16, second.Y * 16 - 48, 48, 48);
            bool fromFirst = Touches(box1, body.Hitbox);
            if (fromFirst == Touches(box2, body.Hitbox)) return false;
            // A wire pulse travels through both pads and moves their occupants.
            // Don't select a route that would send a bystander the other way.
            foreach (var player in Main.player)
                if (player != null && player.active && !player.dead && !ReferenceEquals(player, body) &&
                    (Touches(box1, player.Hitbox) || Touches(box2, player.Hitbox))) return false;
            foreach (var npc in Main.npc)
                if (npc != null && npc.active && npc.lifeMax > 5 && !npc.boss && !npc.noTileCollide && !NPCID.Sets.TeleportationImmune[npc.type] &&
                    (Touches(box1, npc.Hitbox) || Touches(box2, npc.Hitbox))) return false;

            var delta = new Vector2((second.X - first.X) * 16, (second.Y - first.Y) * 16);
            destination = body.position + (fromFirst ? delta : -delta);
            return CompanionMobilityGeometry.ClearBody(destination, body.width, body.height);
        }

        private static bool Pad(Point point)
        {
            for (int segment = 0; segment < 3; segment++)
            {
                int x = point.X + segment;
                if (!CompanionMobilityGeometry.Known(x, point.Y)) return false;
                var tile = Main.tile[x, point.Y];
                if (!tile.nactive() || tile.type != TileID.Teleporter || tile.frameX != segment * 18 || tile.halfBrick() || tile.actuator()) return false;
            }
            return true;
        }

        private static bool Touches(Rectangle pad, Rectangle body)
        {
            var union = Rectangle.Union(pad, body);
            return union.Width <= pad.Width + body.Width && union.Height <= pad.Height + body.Height;
        }

        private static int Wires(Tile tile) => (tile.wire() ? 1 : 0) | (tile.wire2() ? 2 : 0) |
            (tile.wire3() ? 4 : 0) | (tile.wire4() ? 8 : 0);

        internal static bool Find(Player body, Vector2 desiredCenter, out Vector2 trigger, out Vector2 exit)
        {
            trigger = exit = Vector2.Zero;
            // A useful exit must improve a real separation. The native
            // interaction rectangle limits the switch search to nearby cells.
            if (Vector2.DistanceSquared(body.Center, desiredCenter) < 128f * 128f) return false;
            TileReachCheckSettings.Simple.GetTileRegion(body, out int left, out int top, out int right, out int bottom);
            int candidates = 0;
            for (int y = top; y <= bottom; y++)
                for (int x = left; x <= right; x++)
                {
                    if (!CompanionMobilityGeometry.Known(x, y)) continue;
                    var tile = Main.tile[x, y];
                    if (!tile.nactive() || tile.type != TileID.Switches) continue;
                    if (++candidates > 2) return false;
                    if (!TryDestination(body, new Point(x, y), out var position) ||
                        Vector2.DistanceSquared(position + body.Size * .5f, desiredCenter) > 96f * 96f) continue;
                    trigger = new Vector2(x * 16 + 8, y * 16 + 8);
                    exit = position;
                    return true;
                }
            return false;
        }
    }
}
