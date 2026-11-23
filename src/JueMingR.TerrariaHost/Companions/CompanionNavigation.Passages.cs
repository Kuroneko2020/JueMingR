using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        // The persistent graph stores directed connections and their short
        // native witnesses, independently of the pose arena's ancestry. A
        // support run is joined only at an exact, consecutively proved pose.
        private sealed class PassageGraph
        {
            internal const int Entries = 128;
            private const int Witnesses = 3;
            private readonly Passage[] passages = new Passage[Entries];
            private readonly PassageWitness[] witnesses = new PassageWitness[Entries * Witnesses];
            private readonly Dictionary<(Vector2, int), int> supportEntrances = new Dictionary<(Vector2, int), int>();
            private long serial;
            private int cursor;

            internal struct Passage
            {
                internal long Id, From;
                internal Vector2 Entrance, Exit;
                internal RouteMotion Start, End;
                internal Rectangle Terrain;
                internal RouteKind Kind;
                internal int Direction, Count;
                internal float EntrySpeed, ExitSpeed;
                internal bool ObservedExit, Sealed;
            }

            internal struct PassageWitness
            {
                internal RouteStep Step;
                internal RouteMotion Start, End;
                internal float ExitSpeed;
            }

            internal void Clear()
            { Array.Clear(passages, 0, passages.Length); supportEntrances.Clear(); cursor = 0; }

            internal long Remember(RouteNode origin, RouteNode destination, out bool newOutlet)
            {
                var step = destination.Step;
                int prior = Slot(origin.Passage);
                var previous = prior < 0 ? default : passages[prior];
                bool continuation = prior >= 0 && previous.Exit == origin.Position && previous.End == origin.Motion &&
                    previous.ExitSpeed == origin.VelocityX && !previous.Sealed && !previous.ObservedExit && !destination.Observe &&
                    previous.Kind == step.Kind && previous.Direction == step.Direction &&
                    previous.Count < Witnesses && (step.Kind == RouteKind.Walk || step.Kind == RouteKind.Rope || step.Kind == RouteKind.Track);
                var witness = new PassageWitness { Step = step, Start = origin.Motion, End = destination.Motion, ExitSpeed = destination.VelocityX };
                if (continuation)
                {
                    witnesses[prior * Witnesses + previous.Count++] = witness;
                    previous.Exit = destination.Position;
                    previous.End = destination.Motion;
                    previous.ExitSpeed = destination.VelocityX;
                    previous.Terrain = Cover(previous.Terrain, destination.EdgeReads);
                    passages[prior] = previous;
                    newOutlet = previous.Count == Witnesses;
                    return previous.Id;
                }

                int slot = cursor;
                cursor = (cursor + 1) % Entries;
                // A fixed exit has an independent outgoing connection. It
                // cannot move if the pose graph later takes another branch.
                if (prior >= 0)
                {
                    previous.Sealed = true;
                    passages[prior] = previous;
                }
                Forget(slot);
                long id = ++serial * Entries + slot;
                passages[slot] = new Passage { Id = id, From = origin.Passage, Entrance = origin.Position, Exit = destination.Position,
                    Start = origin.Motion, End = destination.Motion, Terrain = destination.EdgeReads, Kind = step.Kind,
                    Direction = step.Direction, Count = 1, EntrySpeed = origin.VelocityX, ExitSpeed = destination.VelocityX,
                    ObservedExit = destination.Observe };
                witnesses[slot * Witnesses] = witness;
                if (step.Kind == RouteKind.Walk) supportEntrances[(origin.Position, step.Direction)] = slot;
                // Turns and genuine support/mode changes may be useful
                // immediately, even when the new footprint is only a tile.
                newOutlet = prior >= 0 && (origin.Motion != destination.Motion || origin.Position.Y != destination.Position.Y ||
                    previous.Kind != step.Kind && step.Kind != RouteKind.Walk || previous.Direction != step.Direction &&
                    Math.Abs(previous.Exit.X - previous.Entrance.X) >= 32 && Math.Abs(destination.Position.X - previous.Entrance.X) >= 20);
                return id;
            }

            internal bool TryEnd(long id, Vector2 position, RouteMotion motion, float velocity, out Passage passage)
            {
                int index = Slot(id);
                passage = index < 0 ? default : passages[index];
                if (index < 0) return false;
                if (passage.Exit == position && passage.End == motion && passage.ExitSpeed == velocity) return true;
                // A terminal may have been found before this contiguous
                // segment grew another witness. Select its original exact
                // boundary, never the newest end or a nearby launch speed.
                for (int i = 0; i < passage.Count - 1; i++)
                {
                    var witness = ReadWitness(passage, i);
                    if (witness.Step.To != position || witness.End != motion || witness.ExitSpeed != velocity) continue;
                    passage.Count = i + 1;
                    return true;
                }
                return false;
            }

            internal PassageWitness ReadWitness(Passage passage, int index) => witnesses[(int)(passage.Id % Entries) * Witnesses + index];

            internal bool TryKnownSupport(Vector2 position, int direction, float speed, out RouteStep step)
            {
                step = default;
                if (!supportEntrances.TryGetValue((position, direction), out int slot) || Math.Abs(passages[slot].EntrySpeed - speed) > .05f)
                    return false;
                step = witnesses[slot * Witnesses].Step;
                return true;
            }

            internal bool RetireChanged(Rectangle changed, ref int work, ref int next)
            {
                // Called as part of the same selected repair slice. Never
                // publish cached passages while their dependencies are dirty.
                while (work > 0 && next < Entries)
                {
                    work--;
                    if (passages[next].Id != 0 && passages[next].Terrain.Intersects(changed)) Forget(next);
                    next++;
                }
                return next == Entries;
            }

            private int Slot(long id)
            {
                int index = (int)(id % Entries);
                return id != 0 && passages[index].Id == id ? index : -1;
            }

            private void Forget(int slot)
            {
                var old = passages[slot];
                if (old.Id != 0 && old.Kind == RouteKind.Walk && supportEntrances.TryGetValue((old.Entrance, old.Direction), out int index) && index == slot)
                    supportEntrances.Remove((old.Entrance, old.Direction));
                passages[slot] = default;
            }

            private static Rectangle Cover(Rectangle left, Rectangle right) => left.Width == 0 ? right :
                right.Width == 0 ? left : Rectangle.Union(left, right);
        }

        private bool TryPassageEdge(Player body, CompanionMobilitySnapshot mobility, RouteNode node, Vector2 target,
            int candidate, out RouteNode next, out bool found)
        {
            next = default;
            found = false;
            if (node.Motion != RouteMotion.Ground || candidate != 0 && candidate != 1 && candidate != 35 && candidate != 36) return false;
            int direction = target.X < node.Position.X ? -1 : 1;
            if (candidate == 1 || candidate == 36) direction = -direction;
            if (!search.Passages.TryKnownSupport(node.Position, direction, node.VelocityX, out RouteStep step)) return false;
            float distance = Math.Abs(step.To.X - node.Position.X);
            if (candidate < 35 ? distance != 16 : distance <= 16 || distance > 32) return false;
            Vector2 contact;
            using (Scratch.Save())
            {
                if (!TrySupportedTravel(body, node.Position, direction, distance, search.Terrain, out contact, out _) || contact != step.To) return true;
            }
            // The short, same-stance sweep revalidates the old witness. Only
            // current native capability can establish its new launch speed.
            step.ExitVelocityX = RunToDistance(body, mobility, node.VelocityX, direction, Math.Max(0, distance - 3));
            next = new RouteNode { Position = contact, Motion = node.Motion, Fuel = node.Fuel, GlideFuel = node.GlideFuel, Step = step };
            found = true;
            return true;
        }
    }
}
