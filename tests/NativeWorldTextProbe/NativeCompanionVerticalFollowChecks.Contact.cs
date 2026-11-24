using System;
using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private static void NativeContactAndDescent(object host, object profiles, Player human)
        {
            const int high = 80, lower = 81, tileX = 100;
            const float flatCenter = tileX * 16 + 18;
            using (var run = new Scenario(host, profiles, human, "follow-floor-pair", flatCenter, high, () =>
            {
                Fill(tileX - 8, tileX + 8, high, TileID.Platforms);
                Fill(tileX - 8, tileX + 8, lower, TileID.Platforms);
                Fill(tileX - 8, tileX + 8, lower + 6, TileID.Stone);
            }))
            {
                VerifyNativePlatformPhase(run.Body, lower);
                VerifyNativeStairCarry(run.Body, tileX, lower + 4);
                FollowToContact(run, human, new Vector2(flatCenter, lower * 16), "neighboring flat platform");
            }

            for (byte slope = 1; slope <= 2; slope++)
            {
                byte shape = slope;
                float center = shape == 1 ? flatCenter : tileX * 16 - 2;
                using (var run = new Scenario(host, profiles, human, "follow-slope-" + shape, center, high, () =>
                {
                    Fill(tileX - 8, tileX + 8, high, TileID.Platforms);
                    Fill(tileX, tileX, lower, TileID.Stone);
                    Main.tile[tileX, lower].slope(shape);
                    Fill(tileX - 8, tileX + 8, lower + 6, TileID.Stone);
                }))
                {
                    var target = new Vector2(center, lower * 16 + 8);
                    Vector2 pose = target - new Vector2(run.Body.width * .5f, run.Body.height);
                    var support = NavigationType(host.GetType().Assembly, "CompanionNativeContact").GetMethod("Supported", ReflectionMembers);
                    bool bodySupport = (bool)support.Invoke(null, new object[] { run.Body, pose, true });
                    var native = ContactProbe(run.Body, pose);
                    NativeContactStep(native, false, false);
                    Console.WriteLine($"FOLLOW contact slope={shape} bodySupport={bodySupport} nativePos={native.position} original={pose}");
                    Require(Math.Abs(native.position.Y - pose.Y) <= .1f && Math.Abs(native.velocity.Y) <= .01f &&
                        bodySupport, "the supported target and current-body queries agree with the native slope phase");
                    FollowToContact(run, human, target, "lower slope " + shape, shape == 1 ? 1 : -1);
                }
            }
        }

        private static void VerifyNativePlatformPhase(Player body, int targetRow)
        {
            // Small native comparator: hold Down until the incoming velocity
            // exceeds 1. Only the special ignorePlats phase may bypass the
            // lower flat platform at that speed.
            var ordinary = ContactProbe(body, body.position);
            var strong = ContactProbe(body, body.position);
            for (int tick = 0; tick < 10; tick++)
            {
                NativeContactStep(ordinary, true, false);
                NativeContactStep(strong, true, true);
            }
            Console.WriteLine($"FOLLOW native platform ordinary={ordinary.Bottom.Y:F2}/{ordinary.stairFall} ignorePlats={strong.Bottom.Y:F2}/{strong.stairFall}");
            Require(Math.Abs(ordinary.Bottom.Y - targetRow * 16) < .1f && strong.Bottom.Y > ordinary.Bottom.Y + 5,
                "native Down retains the next faster flat contact while ignorePlats bypasses it");
        }

        private static void VerifyNativeStairCarry(Player body, int tileX, int row)
        {
            Fill(tileX, tileX, row, TileID.Platforms);
            Main.tile[tileX, row].slope(1);
            var pose = new Vector2(tileX * 16 + 8, row * 16 + 8.25f - body.height);
            var falling = ContactProbe(body, pose);
            var neutral = ContactProbe(body, pose);
            falling.stairFall = true;
            NativeContactStep(falling, false, false);
            NativeContactStep(neutral, false, false);
            Console.WriteLine($"FOLLOW native stair releasedDown={falling.Bottom.Y:F2}/{falling.stairFall} fresh={neutral.Bottom.Y:F2}/{neutral.stairFall}");
            Require(falling.stairFall && !neutral.stairFall && falling.Bottom.Y > neutral.Bottom.Y + .4f,
                "a native sloped platform still sees the inherited stairFall after Down has been released");
            Main.tile[tileX, row] = new Tile();
        }

        private static Player ContactProbe(Player source, Vector2 pose) => new Player
        {
            position = pose,
            width = source.width,
            height = source.height,
            gravity = source.gravity,
            gravDir = source.gravDir
        };

        private static void NativeContactStep(Player native, bool down, bool ignore)
        {
            native.velocity.Y += native.gravity;
            native.controlDown = down;
            native.DryCollision(down, ignore);
            native.SlopingCollision(down, ignore);
        }

        private static void FollowToContact(Scenario run, Player human, Vector2 goal, string geometry, int slopeDirection = 0)
        {
            human.Bottom = goal;
            int firstDown = -1, stair = -1, settled = -1, stable = 0;
            for (int tick = 0; tick < 110; tick++)
            {
                run.StepOwner(Vector2.Zero);
                Player body = run.Body;
                if (body.controlDown && firstDown < 0) firstDown = tick;
                if (body.stairFall && stair < 0) stair = tick;
                // The same native diagonal support changes its contact Y with X.
                float horizontal = body.Center.X - goal.X;
                float contactY = goal.Y + horizontal * slopeDirection;
                bool correct = Math.Abs(body.Bottom.Y - contactY) < .3f && Math.Abs(horizontal) < 8 &&
                    Math.Abs(body.velocity.Y) < .05f && !body.stairFall;
                stable = correct ? stable + 1 : 0;
                if (correct && settled < 0) settled = tick;
            }
            Console.WriteLine($"FOLLOW contact target={geometry} down={firstDown} stair={stair} settled={settled} stable={stable} feet={run.Body.Bottom}");
            Require(firstDown >= 0 && stair >= firstDown && settled > firstDown && stable >= 30,
                "normal Follow leaves the starting platform and stays on the intended lower support");
        }

        private static void RunUpJumpAndHighExit(object host, object profiles, Player human)
        {
            VerifyNativeRunUp(host, profiles, human);
            FollowWithRunUp(host, profiles, human);
            const int floor = 82, intermediate = 76, crossing = 70, lower = 92, wall = 74;
            using (var run = new Scenario(host, profiles, human, "follow-high-exit", 59 * 16, floor, () =>
            {
                Fill(47, wall, floor, TileID.Stone);
                Fill(53, 63, intermediate, TileID.Platforms);
                Fill(62, 83, crossing, TileID.Platforms);
                for (int row = 74; row <= lower; row++) Fill(wall, wall, row, TileID.Stone);
                for (int row = 65; row <= lower + 8; row++) Fill(47, 47, row, TileID.Stone);
                Fill(wall + 1, 111, lower, TileID.Stone);
            }))
            {
                var goal = new Vector2(91 * 16, lower * 16);
                human.Bottom = goal;
                int launch = -1, firstLanding = -1, wallTop = -1, crossed = -1, landed = -1, stable = 0, resumed = 0;
                float launchSpeed = 0, lowestFeet = float.PositiveInfinity;
                for (int tick = 0; tick < 670 && stable < 24; tick++)
                {
                    var body = run.Body;
                    float priorX = body.velocity.X;
                    float priorY = body.Bottom.Y;
                    run.StepOwner(Vector2.Zero);
                    if (launch < 0 && body.controlJump && body.Bottom.Y < priorY - 1 && body.velocity.Y < -1)
                    { launch = tick; launchSpeed = priorX; }
                    lowestFeet = Math.Min(lowestFeet, body.Bottom.Y);
                    if (firstLanding < 0 && OnFloor(body, intermediate)) firstLanding = tick;
                    // The original solid crown is a legal intermediate support before the far drop.
                    if (wallTop < 0 && OnFloor(body, 74) && body.Right.X > wall * 16 && body.Left.X < (wall + 1) * 16)
                        wallTop = tick;
                    if (crossed < 0 && body.Left.X >= (wall + 1) * 16 && (body.Bottom.Y < 74 * 16 || wallTop >= 0))
                    {
                        crossed = tick;
                        Console.WriteLine($"FOLLOW high exit far edge step={tick} feet={body.Bottom}/{body.velocity} crown={wallTop} route={RouteStep(run.Actor)}");
                    }
                    if (crossed >= 0 && landed < 0 && OnFloor(body, lower)) landed = tick;
                    stable = ContactNearOwner(body, goal) ? stable + 1 : 0;
                    if (tick % 110 == 0 || firstLanding >= 0 && tick - firstLanding <= 14)
                    {
                        object nav = Navigator(run.Actor);
                        Console.WriteLine($"FOLLOW high exit step={tick} feet={body.Bottom}/{body.velocity} route={RouteStep(run.Actor)} " +
                            $"plan={PlanStatus(nav)} {RefinementTrace(nav)} {RouteTrace(nav)}");
                    }
                }
                var next = goal + new Vector2(64, 0);
                for (int tick = 0; tick < 140 && resumed < 22; tick++)
                {
                    run.StepToward(next, new Vector2(2.4f, 0));
                    resumed = ContactNearOwner(run.Body, next) ? resumed + 1 : 0;
                }
                Console.WriteLine($"FOLLOW high exit launch={launch} speed={launchSpeed:F2} intermediate={firstLanding} crown={wallTop} upperCross={crossed} lower={landed} nearestHeight={lowestFeet:F2} stable={stable} resumed={resumed} feet={run.Body.Bottom}");
                Require(launch >= 0 && firstLanding > launch && crossed > firstLanding && landed > crossed &&
                    stable >= 24 && resumed >= 22, "ordinary Follow must jump upward to the only upper exit, descend and settle by the lower owner twice");
            }
        }

        private static bool ContactNearOwner(Player body, Vector2 goal) =>
            Math.Abs(body.Bottom.Y - goal.Y) < 1 && Math.Abs(body.Center.X - goal.X) <= OrdinaryFollowSpacing + .01f &&
            Math.Abs(body.velocity.X) < .35f && Math.Abs(body.velocity.Y) < .1f;

        private static void VerifyNativeRunUp(object host, object profiles, Player human)
        {
            const int floor = 80, bank = 73;
            using (var run = new Scenario(host, profiles, human, "jump-real-runup", 1026, floor, () =>
            {
                Fill(49, 64, floor, TileID.Stone);
                Fill(bank, 88, floor, TileID.Stone);
                Fill(49, 88, floor + 12, TileID.Stone);
            }))
            {
                var body = run.Body;
                float rollingSpeed = body.maxRunSpeed;
                Vector2 slow = JumpToNativeBank(body, 0, out float slowLaunch);
                Vector2 fast = JumpToNativeBank(body, rollingSpeed, out float fastLaunch);
                Console.WriteLine($"FOLLOW native runup speed={rollingSpeed:F2} takeoffY={slowLaunch:F2}/{fastLaunch:F2} slow={slow} fast={fast}");
                Require(slowLaunch < -1 && Math.Abs(slowLaunch - fastLaunch) < .01f && slow.Y > floor * 16 + 40 &&
                    Math.Abs(fast.Y - floor * 16) < .1f && fast.X >= bank * 16 - body.width * .5f,
                    "original JumpMovement gains enough horizontal reach from a running entry, not from a stationary entry");
            }
        }

        private static void FollowWithRunUp(object host, object profiles, Player human)
        {
            const int launch = 80, landing = 77, farBank = 73;
            using (var run = new Scenario(host, profiles, human, "follow-runup-gap", 968, launch, () =>
            {
                Fill(48, 64, launch, TileID.Stone);
                Fill(farBank, 89, landing, TileID.Stone);
                Fill(48, 89, 115, TileID.Stone);
            }))
            {
                var goal = new Vector2(80 * 16, landing * 16);
                human.Bottom = goal;
                int running = -1, jumping = -1, crossed = -1, stable = 0;
                float actualSpeed = 0;
                bool walkPrecedesJump = false;
                var nav = Navigator(run.Actor);
                var refinement = Get(nav, "refine");
                for (int tick = 0; tick < 440 && stable < 22; tick++)
                {
                    Player body = run.Body;
                    float speed = body.velocity.X;
                    run.StepOwner(Vector2.Zero);
                    if (running < 0 && OnFloor(body, launch) && body.controlRight && body.velocity.X > 1.2f) running = tick;
                    if (jumping < 0 && running >= 0 && body.controlJump && body.velocity.Y < -1 && speed > 1.2f)
                    {
                        jumping = tick;
                        actualSpeed = speed;
                        walkPrecedesJump = ObservedActionKind(Navigator(run.Actor)) == "Jump";
                    }
                    if (jumping >= 0 && crossed < 0 && OnFloor(body, landing) && body.Right.X > farBank * 16) crossed = tick;
                    stable = ContactNearOwner(body, goal) ? stable + 1 : 0;
                    if (tick % 80 == 0 || tick < 80 && tick % 6 == 0)
                    {
                        var candidate = Get(refinement, "Edge");
                        Console.WriteLine($"FOLLOW runup step={tick} feet={body.Bottom}/{body.velocity} route={RouteStep(run.Actor)} " +
                            $"status={Get(run.Actor, "Status")} plan={PlanStatus(nav)} {RouteTrace(nav)} " +
                            $"refinement={Get(refinement, "Active")}/{Get(Get(refinement, "Witness"), "Valid")} " +
                            $"candidate={Get(candidate, "Kind")}:{Get(candidate, "From")}→{Get(candidate, "To")} refinedTicks={Get(refinement, "ticks")}");
                    }
                }
                Console.WriteLine($"FOLLOW runup walk={running} jump={jumping} actualEntry={actualSpeed:F2} ownAction={walkPrecedesJump} crossed={crossed} stable={stable} feet={run.Body.Bottom}");
                if (crossed < 0)
                {
                    var nativeStart = new Vector2(1032 - run.Body.width * .5f, launch * 16 - run.Body.height);
                    Vector2 slow = JumpToNativeBank(run.Body, 0, out _, nativeStart);
                    Vector2 fast = JumpToNativeBank(run.Body, run.Body.maxRunSpeed, out _, nativeStart);
                    Console.WriteLine($"FOLLOW runup native geometry launch={nativeStart} stationary={slow} rolling={fast}");
                }
                Require(running >= 0 && jumping > running && actualSpeed > 1.2f && walkPrecedesJump && crossed > jumping && stable >= 22,
                    "an actual supported walk feeds its running entry to the planned jump and reaches the higher far support");
            }
        }

        private static Vector2 JumpToNativeBank(Player source, float speed, out float takeoff, Vector2? start = null)
        {
            var native = ContactProbe(source, start ?? source.position);
            native.velocity.X = speed;
            native.maxRunSpeed = source.maxRunSpeed;
            native.accRunSpeed = source.accRunSpeed;
            native.runAcceleration = source.runAcceleration;
            native.runSlowdown = source.runSlowdown;
            native.releaseJump = true;
            native.controlRight = true;
            takeoff = 0;
            for (int tick = 0; tick < 93; tick++)
            {
                native.controlJump = tick == 0 || native.jump > 0 && native.velocity.Y < 0;
                native.HorizontalMovement();
                native.JumpMovement();
                native.velocity.Y = Math.Min(source.maxFallSpeed, native.velocity.Y + native.gravity);
                native.DryCollision(false, false);
                native.SlopingCollision(false, false);
                if (tick == 0) takeoff = native.velocity.Y;
                if (tick > 0 && native.velocity.Y == 0) break;
            }
            return native.Bottom;
        }
    }
}
