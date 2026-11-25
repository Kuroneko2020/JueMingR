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
            if (!body.IsInTileInteractionRange(trigger.X, trigger.Y, TileReachCheckSettings.Simple) ||
                !TryPads(trigger, out var first, out var second)) return false;

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

        private static bool TryPads(Point trigger, out Point first, out Point second)
        {
            first = second = default;
            if (!CompanionMobilityGeometry.Known(trigger.X, trigger.Y)) return false;
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

            return pads == 2 && Pad(first) && Pad(second) && (Math.Abs(first.X - second.X) >= 3 || Math.Abs(first.Y - second.Y) >= 3);
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

        internal static void ReadLinks(Player body, Vector2 targetFeet, NavigationBudget budget, List<NavigationTransition> links)
        {
            if (body.gravDir < 0 || body.mount.Cart) return;
            TileReachCheckSettings.Simple.GetTileRegion(body, out int left, out int top, out int right, out int bottom);
            int candidates = 0;
            for (int y = top - 2; y <= bottom + 2; y++)
                for (int x = left - 6; x <= right + 6; x++)
                {
                    if (!budget.TryTiles()) return;
                    if (!CompanionMobilityGeometry.Known(x, y)) continue;
                    var tile = Main.tile[x, y];
                    if (!tile.nactive() || tile.type != TileID.Switches) continue;
                    if (++candidates > 2 || !budget.TryTiles(CircuitLimit * 5 + 20)) return;
                    var trigger = new Point(x, y);
                    if (!TryPads(trigger, out var first, out var second)) continue;
                    Offer(body, targetFeet, trigger, first, second, links);
                    Offer(body, targetFeet, trigger, second, first, links);
                }
        }

        private static void Offer(Player body, Vector2 targetFeet, Point trigger, Point source, Point destination,
            List<NavigationTransition> links)
        {
            TileReachCheckSettings.Simple.GetRanges(out int horizontal, out int vertical);
            if (Math.Abs(trigger.X - source.X - 1) > horizontal ||
                Math.Abs(trigger.Y - source.Y + body.height / 32) > vertical) return;
            var from = new Vector2(source.X * 16f + 24f, source.Y * 16f);
            var to = new Vector2(destination.X * 16f + 24f, destination.Y * 16f);
            if (Vector2.DistanceSquared(to, targetFeet) > 96f * 96f ||
                Vector2.DistanceSquared(to, targetFeet) + 60f * 60f >= Vector2.DistanceSquared(from, targetFeet) ||
                !CompanionMobilityGeometry.ClearBody(CompanionNativeContact.TopLeft(body, to), body.width, body.height)) return;
            links.Add(new NavigationTransition(NavigationAction.Facility, from, to, entryTile: trigger, exitTile: destination,
                irreversible: true, detail: NavigationTraversalDetail.Teleporter));
        }
    }
}
