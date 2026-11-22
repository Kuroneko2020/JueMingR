using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionDoorFollowChecks
    {
        internal static void Run(object host, object actor, Player body, Player human)
        {
            const int doorX = 96, floor = 73;
            var profile = (CompanionProfile)Get(actor, "Profile");
            var prefs = profile.Behavior.Clone();
            var before = body.position;
            var speed = body.velocity;
            var owner = human.position;
            var wing = body.armor[3];
            var wingStats = ArmorIDs.Wing.Sets.Stats;
            var area = new Dictionary<Point, Tile>();
            for (int x = 73; x <= 116; x++)
                for (int y = 61; y <= 79; y++)
                    area.Add(new Point(x, y), Main.tile[x, y]);
            try
            {
                Require(!body.mount.Active && body.width == 20 && body.height == 42, "normal doorway has the actual full-size player");
                Call(host, "SetBehavior", profile.Id, new CompanionBehavior { Follow = true, AttackEnabled = false, HealthRecoveryEnabled = false });
                foreach (int height in new[] { 3, 5 })
                    foreach (bool fromContact in height == 3 ? new[] { true, false } : new[] { false })
                    {
                        Clear(area.Keys);
                        for (int x = 76; x <= 112; x++)
                            for (int y = 64; y <= floor; y++)
                                if (y == floor || y < floor - height) TileAt(x, y, TileID.Stone);
                        int closed = height == 3 ? TileID.ClosedDoor : TileID.TallGateClosed;
                        int open = height == 3 ? TileID.OpenDoor : TileID.TallGateOpen;
                        var gate = height == 5 ? TileObjectData.GetTileData(TileID.TallGateClosed, 0) : null;
                        int frameY = 0;
                        for (int row = 0; row < height; row++)
                        {
                            TileAt(doorX, floor - height + row, closed, 0, frameY);
                            frameY += gate == null ? 18 : gate.CoordinateHeights[row] + gate.CoordinatePadding;
                        }
                        Place(host, actor, human, body, new Vector2(doorX * 16 - (fromContact ? 24 : 158), floor * 16 - body.height));
                        human.Bottom = new Vector2(107 * 16, floor * 16);
                        bool opened = false;
                        bool crossed = false;
                        for (int tick = 0; tick < 170 && (!crossed || Main.tile[doorX, floor - 2].type != closed); tick++)
                        {
                            Step(host, body);
                            opened |= Main.tile[doorX, floor - 2].type == open;
                            crossed |= body.Left.X > (doorX + 1) * 16 + 18;
                        }
                        Require(opened && crossed && Main.tile[doorX, floor - 2].type == closed && !(bool)Get(actor, "Faulted"),
                            $"real Follow opens, crosses and closes {closed} from {(fromContact ? "4px" : "afar")}: open={opened} crossed={crossed} tile={Main.tile[doorX, floor - 2].type} body={body.position} status={Get(actor, "Status")}");
                        Console.WriteLine($"DOOR FOLLOW horizontal tile={closed} approach={(fromContact ? "4px" : "afar")} body={body.position} opened={opened} crossed={crossed} restored={Main.tile[doorX, floor - 2].type == closed}");
                        if (fromContact)
                        {
                            human.Bottom = new Vector2(83 * 16, floor * 16);
                            bool reopened = false;
                            for (int tick = 0; tick < 170 && body.Right.X >= doorX * 16 - 16; tick++)
                            {
                                Step(host, body);
                                reopened |= Main.tile[doorX, floor - 2].type == open;
                            }
                            Require(reopened && body.Right.X < doorX * 16 - 16,
                                $"normal reverse Follow reopens the actual randomized closed-door frames and walks back: open={reopened} body={body.position} status={Get(actor, "Status")}");
                        }
                    }

                foreach (int kind in new[] { 1, -1, -2 })
                {
                    Clear(area.Keys);
                    const int tx = 94, ty = 67;
                    int direction = Math.Sign(kind), lower = kind == -2 ? 79 : 72;
                    bool flying = kind == -2;
                    if (flying)
                    {
                        Terraria.Initializers.WingStatsInitializer.Load();
                        body.armor[3] = new Item(ItemID.FishronWings);
                    }
                    for (int x = 76; x <= 112; x++)
                    {
                        TileAt(x, lower, TileID.Stone);
                        if (x == tx || x == tx + 1) continue;
                        for (int y = ty; y < lower; y++) TileAt(x, y, TileID.Stone);
                    }
                    TileAt(tx, ty, TileID.TrapdoorClosed);
                    TileAt(tx + 1, ty, TileID.TrapdoorClosed, 18);
                    float center = (tx + 1) * 16;
                    Place(host, actor, human, body, new Vector2(center - body.width * .5f, (direction > 0 ? ty : lower) * 16 - body.height));
                    human.Bottom = new Vector2(center, (direction > 0 ? lower : ty - 2) * 16);
                    bool opened = false, crossed = false, wrongShape = false;
                    bool wingApproach = false, wingThrough = false;
                    for (int tick = 0; tick < 145 && (!crossed || Main.tile[tx, ty].type != TileID.TrapdoorClosed); tick++)
                    {
                        float beforeFlight = body.wingTime;
                        Step(host, body);
                        if (flying && body.wingTime < beforeFlight && body.velocity.Y < 0)
                        {
                            if (opened) wingThrough = true;
                            else if (Main.tile[tx, ty].type == TileID.TrapdoorClosed) wingApproach = true;
                        }
                        var hinge = Main.tile[tx, ty];
                        if (hinge.active() && hinge.type == TileID.TrapdoorOpen)
                        {
                            opened = true;
                            int leafRow = ty + direction;
                            wrongShape |= !Main.tile[tx, leafRow].active() || Main.tile[tx, leafRow].type != TileID.TrapdoorOpen ||
                                Main.tile[tx + 1, leafRow].frameX % 36 != 18;
                        }
                        crossed |= direction > 0 ? body.Top.Y > (ty + 1) * 16 + 4 : body.Bottom.Y < ty * 16 - 4;
                    }
                    Require(opened && crossed && !wrongShape && Main.tile[tx, ty].type == TileID.TrapdoorClosed && !(bool)Get(actor, "Faulted"),
                        $"real Follow opens real trapdoor geometry, passes {(flying ? "up in flight" : direction > 0 ? "down" : "up")} and closes: open={opened} crossed={crossed} wrongLeaf={wrongShape} tile={Main.tile[tx, ty].type} body={body.position} velocity={body.velocity} status={Get(actor, "Status")}");
                    if (flying) Require(wingApproach && wingThrough, "actual wings lift the body before the hatch opens and continue lifting through its opening");
                }
                Console.WriteLine("PASS native companion Follow doors: 4px and remote ordinary door, tall gate, and ground or wing-flight passage through the native trapdoor.");
            }
            finally
            {
                foreach (var entry in area) Main.tile[entry.Key.X, entry.Key.Y] = entry.Value;
                body.position = before;
                body.velocity = speed;
                body.armor[3] = wing;
                ArmorIDs.Wing.Sets.Stats = wingStats;
                human.position = owner;
                Call(Get(Get(actor, "Behavior"), "navigation"), "CancelRoute");
                Call(host, "SetBehavior", profile.Id, prefs);
            }
        }

        private static void Place(object host, object actor, Player human, Player body, Vector2 position)
        {
            body.position = position;
            body.velocity = Vector2.Zero;
            human.Center = body.Center;
            // Each geometry is independent. Retire the old route without
            // reusing its command sequence on the same live actor.
            Call(Get(Get(actor, "Behavior"), "navigation"), "CancelRoute");
            Step(host, body);
        }

        private static void Clear(IEnumerable<Point> area)
        { foreach (Point cell in area) Main.tile[cell.X, cell.Y] = new Tile(); }

        private static void TileAt(int x, int y, int type, int frameX = 0, int frameY = 0)
        {
            var tile = new Tile { type = (ushort)type, frameX = (short)frameX, frameY = (short)frameY };
            tile.active(true);
            Main.tile[x, y] = tile;
        }

        private static void Step(object host, Player body)
        {
            NativeQuickItemChecks.BeginWorldStep();
            Call(host, "Update", (ulong)Main.GameUpdateCount);
            body.Update(body.whoAmI);
        }

    }
}
