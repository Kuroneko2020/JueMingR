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
        private const int DoorWork = 64, DoorProbeReach = 18, MaxDoorProofTiles = 256;
        private ReadSet doorQueryTerrain;
        private RouteStep doorQuery;
        private object doorQueryWorld;
        private bool doorQueryPass;
#if DEBUG
        internal int DoorPassageProbes { get; private set; }
#endif

        private struct DoorPassageProof
        {
            internal Rectangle Area;
            internal ulong Tiles;
            internal Vector2 Start;
            internal float Gravity, GravityDirection;
            internal int Shape, Flags;
            internal uint At;

            internal bool MatchesBody(Player body) => Area.Width != 0 && Gravity == body.gravity && GravityDirection == body.gravDir &&
                Shape == (body.width | body.height << 16) && Flags == DoorBodyFlags(body);
        }

        private static int DoorBodyFlags(Player body) => (body.wet ? 1 : 0) | (body.waterWalk || body.waterWalk2 ? 2 : 0) |
            (body.controlDown ? 4 : 0) | (body.controlUp ? 8 : 0) | (body.stairFall ? 16 : 0);

        private static bool FindClosedHorizontalDoor(Player body, Vector2 from, int direction, ReadSet terrain, out CompanionDoor door)
        {
            door = default;
            float front = from.X + (direction > 0 ? body.width : 0);
            int first = (int)Math.Floor((front + direction * .1f) / 16), last = (int)Math.Floor((front + direction * DoorProbeReach) / 16);
            int center = (int)(from.Y + body.height * .5f) / 16;
            for (int x = first; x != last + direction; x += direction)
                for (int offset = 0; offset < 3; offset++)
                {
                    int y = center + (offset == 2 ? -1 : offset);
                    if (!(terrain == null ? Loaded(x, y) : terrain.Record(x, y)) || !CompanionDoor.TryRead(new Point(x, y), out var fixture) || fixture.Open ||
                        fixture.Family == CompanionDoorFamily.Trapdoor) continue;
                    // All rows of a known frame name the same hinge. Only
                    // the first fixture in the body's passage can be proved.
                    door = fixture;
                    return true;
                }
            return false;
        }

        private bool TryDoorEdge(Player body, Vector2 from, int direction, ReadSet terrain, ref RouteStep step) =>
            FindClosedHorizontalDoor(body, from, direction, terrain, out var door) &&
            ProveDoorPassage(body, from, direction, door, terrain, ref step);

        private bool TryDoorImmediate(Player body, int direction, bool allowPlanning, ref RouteStep step, out bool spent, out bool approach)
        {
            spent = approach = false;
            if (!FindClosedHorizontalDoor(body, body.position, direction, null, out var door)) return false;
            approach = !door.AtApproach(body, direction) && TrySupportedTravel(body, body.position, direction, 4, null,
                out _, out _, rejectDangerLiquids: true);
            if (!allowPlanning) return false;
            if (ReferenceEquals(doorQueryWorld, Main.ActiveWorldFileData) && doorQuery.Operand == door.Target &&
                direction == doorQuery.Direction && body.position == doorQuery.DoorProof.Start && DoorProofCurrent(body, doorQuery.DoorProof))
            {
                if (doorQueryPass) step = doorQuery;
                return doorQueryPass;
            }

            spent = true;
            doorQueryTerrain = doorQueryTerrain ?? new ReadSet();
            doorQueryTerrain.Clear();
            doorQueryWorld = Main.ActiveWorldFileData;
            doorQueryPass = ProveDoorPassage(body, body.position, direction, door, doorQueryTerrain, ref step);
            doorQuery = step;
            return doorQueryPass;
        }

        private bool ProveDoorPassage(Player body, Vector2 from, int direction, CompanionDoor door, ReadSet terrain, ref RouteStep step)
        {
#if DEBUG
            DoorPassageProbes++;
#endif
            step.Operand = door.Target;
            Rectangle preceding = terrain.BeginNestedArea();
            bool pass = DoorGroundTravel(body, from, direction, door, terrain, out step.To);
            Rectangle area = terrain.EndNestedArea(preceding);
            if (!DoorStamp(area, out ulong stamp)) return false;
            step.DoorProof = new DoorPassageProof { Area = area, Tiles = stamp, Start = from, At = Main.GameUpdateCount,
                Gravity = body.gravity, GravityDirection = body.gravDir, Shape = body.width | body.height << 16, Flags = DoorBodyFlags(body) };
            return pass;
        }

        private static bool DoorStamp(Rectangle area, out ulong stamp)
        {
            stamp = 14695981039346656037UL;
            if (area.Width == 0 || area.Width * area.Height > MaxDoorProofTiles) return false;
            for (int x = area.Left; x < area.Right; x++)
                for (int y = area.Top; y < area.Bottom; y++)
                {
                    if (!Loaded(x, y)) return false;
                    stamp = unchecked((stamp ^ (uint)TileValue(Main.tile[x, y])) * 1099511628211UL);
                }
            return true;
        }

        private static bool DoorProofCurrent(Player body, DoorPassageProof proof) => proof.MatchesBody(body) &&
            (proof.At == Main.GameUpdateCount || DoorStamp(proof.Area, out ulong stamp) && stamp == proof.Tiles);

        private static bool DoorProofApplies(Player body, int direction, DoorPassageProof proof)
        {
            if (!DoorProofCurrent(body, proof)) return false;
            float dx = body.position.X - proof.Start.X;
            if (Math.Abs(dx) > DoorProbeReach + 3 || dx * direction < -3) return false;
            // Rejoin no farther than the short approach which was proved.
            // Actual support height must agree even on a half-brick.
            if (Math.Abs(dx) < .1f) return Math.Abs(body.position.Y - proof.Start.Y) <= 1 && HasSurface(body, body.position, null);
            return TrySupportedTravel(body, proof.Start, Math.Sign(dx), Math.Abs(dx), null, out Vector2 contact, out _) &&
                Math.Abs(contact.Y - body.position.Y) <= 1 && HasSurface(body, body.position, null);
        }

        private static bool DoorGroundTravel(Player body, Vector2 from, int direction, CompanionDoor door, ReadSet terrain, out Vector2 to)
        {
            to = from;
            Rectangle aperture = door.Aperture;
            float across = (direction > 0 ? aperture.Right + 6 - from.X : from.X + body.width + 6 - aperture.Left);
            if (across <= 0 || across > body.width + aperture.Width + 28) return false;
            for (int x = door.Tiles.Left - 1; x <= door.Tiles.Right; x++)
                for (int y = door.Tiles.Top; y < door.Tiles.Bottom; y++)
                    if (!terrain.Record(x, y)) return false;
            // Predict the actual tread instead of requiring equal height.
            // Ignore only this verified closed fixture inside native queries.
            using (new CompanionDoorCollision.Fixture(door.Tiles))
                return TrySupportedTravel(body, from, direction, across, terrain, out to, out _,
                    throughDoor: true, rejectDangerLiquids: true) &&
                    (door.SwingClear(direction) || door.SwingClear(-direction));
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

        private sealed partial class ActionExecutor
        {
            private bool DriveDoorStep(Player body, CompanionMobilityState state, RouteStep step, bool allowPlanning,
                out CompanionMobilityFrame frame, out bool finished)
            {
                frame = new CompanionMobilityFrame { Status = "接近门口" };
                finished = false;
                if (!CompanionDoor.TryRead(step.Operand.ToTileCoordinates(), out var door) || door.Target != step.Operand ||
                    (door.Family == CompanionDoorFamily.Trapdoor) != (step.Vertical != 0)) return false;
                bool trap = step.Vertical != 0;
                int passage = trap ? step.Vertical : step.Direction;
                float remaining = trap ? (step.To.Y - body.position.Y) * passage : (step.To.X - body.position.X) * passage;
                if (door.Open && remaining <= 4 && (trap || HasSurface(body, body.position, null)))
                { finished = true; return false; }
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
                                { frame.Jump = true; awaitingJumpDelivery = true; }
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
                    // When open, the world has the final hinge and floor. All
                    // crossing input is checked against that actual body tread.
                    bool approaching = !door.Open && !door.AtApproach(body, passage);
                    if (door.Open || approaching)
                    {
                        if (!TrySupportedTravel(body, body.position, passage, 5, null, out _, out _, rejectDangerLiquids: true)) return false;
                        frame.Move = passage;
                    }
                    else if (commandSequence == 0 || state.Sequence != commandSequence || state.Result != CompanionMobilityResult.Started)
                    {
                        var proof = commandSequence != 0 && doorActivationSequence == commandSequence ? doorActivationProof : step.DoorProof;
                        if (!DoorProofApplies(body, passage, proof))
                        {
                            // A stale forecast cannot open a new route. Wait on
                            // the known approach until this body has the grant.
                            if (state.AwaitingEquipmentObservation) { deferral = CompanionMovementDeferral.EquipmentObservation; return true; }
                            if (!allowPlanning || navigation.coordinator.ProofGrantUsed)
                            { deferral = CompanionMovementDeferral.PlanningSlice; return true; }
                            navigation.coordinator.ConsumeProofGrant();
                            navigation.doorQueryTerrain = navigation.doorQueryTerrain ?? new ReadSet();
                            navigation.doorQueryTerrain.Clear();
                            if (!navigation.ProveDoorPassage(body, body.position, passage, door, navigation.doorQueryTerrain, ref step)) return false;
                            proof = step.DoorProof;
                        }
                        if (state.AwaitingEquipmentObservation) { deferral = CompanionMovementDeferral.EquipmentObservation; return true; }
                        if (commandSequence == 0) commandSequence = ++sequence;
                        doorActivationSequence = commandSequence;
                        doorActivationProof = proof;
                    }
                }

                if (door.Open) { frame.Status = "正在过门"; return true; }
                if (!door.AtApproach(body, passage)) return !trap;
                if (commandSequence == 0) commandSequence = ++sequence;
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
}
