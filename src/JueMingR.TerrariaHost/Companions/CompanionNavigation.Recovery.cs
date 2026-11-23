using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        // An execution result and a failed connection are facts at the
        // actual input state. Only this planner-owned ledger suppresses a
        // retry. No radius or speed bucket can prolong a temporary failure.
        private sealed class RouteRecovery
        {
            private const int Capacity = 8;
            private readonly Ticket[] rejected = new Ticket[Capacity];
            private readonly ConnectionTicket[] missed = new ConnectionTicket[4];
            private int next, nextConnect;
            private Rectangle dirty;
            private RouteStep facility;
            private uint facilityUntil;
            private int facilityTile;
            internal RouteStep MostRecent { get; private set; }
            internal uint LastRejectedAt { get; private set; }

            private struct Ticket
            {
                internal RouteStep Step;
                internal Vector2 Position, Velocity;
                internal uint Until;
                internal int Health, Risk;
                internal bool Active;
            }

            private struct ConnectionTicket
            {
                internal ProofHandle Candidate;
                internal Vector2 Position, Velocity;
                internal Rectangle EntryTiles;
                internal uint Until;
            }

            internal void Record(in ActionOutcome outcome, uint now)
            {
                MostRecent = outcome.Step;
                LastRejectedAt = now;
                if (outcome.Step.Kind == RouteKind.Pylon && outcome.ToolAttempted)
                {
                    facility = outcome.Step;
                    facilityTile = FacilityTileValue(facility.Operand);
                    facilityUntil = now + 600;
                }
                rejected[next] = new Ticket { Step = outcome.Step, Position = outcome.Position, Velocity = outcome.Velocity,
                    Until = now + 48, Health = outcome.Health, Risk = outcome.FallKey, Active = true };
                next = (next + 1) % Capacity;
            }

            internal void Record(ProofHandle candidate, Player body, uint now)
            {
                // Connection tries only nearby entrances. A distant edit can
                // invalidate the planner's proof, but cannot change this join.
                int x = (int)body.position.X / 16, y = (int)body.position.Y / 16;
                missed[nextConnect] = new ConnectionTicket { Candidate = candidate, Position = body.position, Velocity = body.velocity,
                    EntryTiles = new Rectangle(x - 4, y - 3, (body.width + 15) / 16 + 9, (body.height + 15) / 16 + 7), Until = now + 24 };
                nextConnect = (nextConnect + 1) % missed.Length;
            }

            internal bool Suppresses(ProofHandle candidate, Player body)
            {
                foreach (var ticket in missed)
                    if (ticket.Candidate.IsSet && ticket.Candidate.Equals(candidate) && SamePose(ticket.Position, ticket.Velocity, body) &&
                        !Due(Main.GameUpdateCount, ticket.Until)) return true;
                return false;
            }

            internal bool Suppresses(RouteStep step, Player body)
            {
                if (step.Kind == RouteKind.Pylon && facilityUntil != 0 && facility.Operand == step.Operand &&
                    Vector2.DistanceSquared(facility.From, step.From) < 48 * 48 && FacilityTileValue(step.Operand) == facilityTile &&
                    !Due(Main.GameUpdateCount, facilityUntil)) return true;
                for (int i = 0; i < rejected.Length; i++)
                {
                    ref var ticket = ref rejected[i];
                    if (ticket.Active && !Due(Main.GameUpdateCount, ticket.Until) && SameStep(ticket.Step, step) &&
                        Applicable(ticket, body)) return true;
                }
                return false;
            }

            internal bool Wake(Player body, Rectangle changes, uint now)
            {
                if (dirty.Width != 0) changes = changes.Width == 0 ? dirty : Rectangle.Union(changes, dirty);
                dirty = Rectangle.Empty;
                bool woke = false;
                for (int i = 0; i < missed.Length; i++)
                {
                    ref var connection = ref missed[i];
                    if (!connection.Candidate.IsSet || !(Due(now, connection.Until) || !SamePose(connection.Position, connection.Velocity, body) ||
                        changes.Intersects(connection.EntryTiles))) continue;
                    connection = default;
                    woke = true;
                }
                if (facilityUntil != 0 && (Due(now, facilityUntil) || FacilityTileValue(facility.Operand) != facilityTile))
                { facilityUntil = 0; woke = true; }
                for (int i = 0; i < rejected.Length; i++)
                {
                    ref var ticket = ref rejected[i];
                    if (!ticket.Active || !(Due(now, ticket.Until) || !Applicable(ticket, body) ||
                        changes.Intersects(Dependencies(ticket.Step)))) continue;
                    ticket = default;
                    woke = true;
                }
                return woke;
            }

            internal void Dirty(Rectangle changes)
            { if (changes.Width != 0) dirty = dirty.Width == 0 ? changes : Rectangle.Union(dirty, changes); }

            internal void ForgetBody()
            {
                Array.Clear(rejected, 0, rejected.Length);
                Array.Clear(missed, 0, missed.Length);
                nextConnect = next = 0;
                dirty = Rectangle.Empty;
                facilityUntil = LastRejectedAt = 0;
                MostRecent = default;
            }

            private static bool SameStep(RouteStep left, RouteStep right) => left.Kind == right.Kind && left.From == right.From &&
                left.To == right.To && left.Direction == right.Direction && left.Vertical == right.Vertical && left.Operand == right.Operand &&
                left.DescentDamage == right.DescentDamage && left.DescentEntersLiquid == right.DescentEntersLiquid && left.Launch.Equals(right.Launch);

            private static bool SamePose(Vector2 position, Vector2 velocity, Player body) =>
                Vector2.DistanceSquared(position, body.position) <= .01f && Vector2.DistanceSquared(velocity, body.velocity) <= .0025f;

            private static bool Applicable(Ticket ticket, Player body) => SamePose(ticket.Position, ticket.Velocity, body) &&
                (ticket.Step.Kind != RouteKind.Drop && ticket.Step.Kind != RouteKind.LedgeDrop ||
                    ticket.Risk == DescentSafetyKey(body) && (ticket.Health <= ticket.Step.DescentDamage) == (body.statLife <= ticket.Step.DescentDamage));

            private static Rectangle Dependencies(RouteStep step) => step.Reads.Width != 0 ? step.Reads : step.DescentArea.Width != 0 ? step.DescentArea :
                new Rectangle((int)Math.Min(step.From.X, step.To.X) / 16 - 2, (int)Math.Min(step.From.Y, step.To.Y) / 16 - 2,
                    (int)Math.Abs(step.From.X - step.To.X) / 16 + 6, (int)Math.Abs(step.From.Y - step.To.Y) / 16 + 7);

            private static int FacilityTileValue(Vector2 point)
            {
                int x = (int)point.X / 16, y = (int)point.Y / 16;
                return Loaded(x, y) ? TileValue(Main.tile[x, y]) : int.MinValue;
            }

            internal static bool Due(uint now, uint deadline) => unchecked((int)(now - deadline)) >= 0;
        }
    }
}
