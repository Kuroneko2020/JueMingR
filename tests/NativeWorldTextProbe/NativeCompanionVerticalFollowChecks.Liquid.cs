using System;
using System.Runtime.ExceptionServices;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        internal static void LiquidEntry(object host, object profiles, Player human)
        {
            ExceptionDispatchInfo first = null;
            try { OrdinaryWater(host, profiles, human); }
            catch (Exception error) { Console.WriteLine(error); first = ExceptionDispatchInfo.Capture(error); }
            try { WaterSurface(host, profiles, human); }
            catch (Exception error) { Console.WriteLine(error); if (first == null) first = ExceptionDispatchInfo.Capture(error); }
            first?.Throw();
        }

        private static void OrdinaryWater(object host, object profiles, Player human)
        {
            const int upper = 80, bottom = 86, x = 60 * 16 + 8;
            using (var run = new Scenario(host, profiles, human, "ordinary-water-drop", x, upper, () =>
            {
                Fill(45, 88, upper, TileID.Platforms);
                Fill(45, 88, bottom, TileID.Stone);
                for (int column = 45; column <= 88; column++)
                    for (int row = bottom - 2; row < bottom; row++) Main.tile[column, row].liquid = byte.MaxValue;
            }))
            {
                int down = -1, wet = -1, landed = -1, walked = -1, stable = 0;
                float lastX = x;
                for (int tick = 0; tick < 240; tick++)
                {
                    int desired = tick < 110 ? x : x + 124;
                    run.StepToward(new Vector2(desired, bottom * 16), new Vector2(2.3f, 4));
                    Player body = run.Body;
                    if (down < 0 && body.controlDown) down = tick;
                    if (wet < 0 && body.wet && !body.lavaWet && !body.honeyWet && !body.shimmerWet) wet = tick;
                    if (wet >= 0 && landed < 0 && OnFloor(body, bottom)) { landed = tick; lastX = body.Center.X; }
                    if (tick >= 110 && landed >= 0 && walked < 0 && body.Center.X - lastX > 32 && body.controlRight) walked = tick;
                    stable = walked >= 0 && body.wet && OnFloor(body, bottom) && Math.Abs(body.Center.X - desired) <= 28 ? stable + 1 : 0;
                }
                Console.WriteLine($"FOLLOW ordinary water down={down} wet={wet} landed={landed} walked={walked} stable={stable} body={run.Body.Bottom} status={Get(run.Actor, "Status")}");
                Require(down >= 0 && wet > down && landed >= wet && walked >= 110 && stable >= 16,
                    "the same actual actor drops from dry support, enters breathable ordinary water and follows on the submerged bottom");
            }

        }

        private static void WaterSurface(object host, object profiles, Player human)
        {
            const int row = 83, shore = 65, farShore = 84, start = 61 * 16, goal = 91 * 16;
            using (var run = new Scenario(host, profiles, human, "water-walk-surface", start, row, () =>
            {
                Fill(46, shore, row, TileID.Stone);
                Fill(farShore, 106, row, TileID.Stone);
                Fill(46, 106, row + 24, TileID.Stone);
                for (int x = shore + 1; x < farShore; x++)
                    for (int y = row; y < row + 24; y++) Main.tile[x, y].liquid = byte.MaxValue;
            }, ItemID.WaterWalkingBoots))
            {
                int wet = 0, supportedRun = 0, longestSupport = 0, reached = -1;
                Require(run.Body.waterWalk || run.Body.waterWalk2, "actual equipped boots apply a native water-walking effect");
                for (int tick = 0; tick < 270; tick++)
                {
                    run.StepToward(new Vector2(goal, row * 16), new Vector2(3.2f, 0));
                    var body = run.Body;
                    if (body.wet) wet++;
                    bool inBasin = body.Left.X > (shore + 1) * 16 + 12 && body.Right.X < farShore * 16 - 12;
                    supportedRun = inBasin && OnFloor(body, row) && body.velocity.X > .6f ? supportedRun + 1 : 0;
                    longestSupport = Math.Max(longestSupport, supportedRun);
                    if (body.Center.X > goal - 28 && OnFloor(body, row)) { reached = tick; break; }
                }
                Console.WriteLine($"FOLLOW water-walking boots nativeSupportRun={longestSupport} wet={wet} reached={reached} body={run.Body.Bottom} status={Get(run.Actor, "Status")}");
                Require(longestSupport >= 20 && wet == 0 && reached >= 0,
                    "follow uses actual water-walking support across a real deep basin and returns to the far solid shore");
            }
        }
    }
}
