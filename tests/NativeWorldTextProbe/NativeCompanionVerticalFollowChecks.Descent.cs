using System;
using System.Runtime.ExceptionServices;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using JueMingR.Platform.Companions;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private static void TolerableFalls(object host, object profiles, Player human)
        {
            ExceptionDispatchInfo first = null;
            foreach (Action check in new Action[]
            {
                () => FallThreshold(host, profiles, human, "fall-one-life", 61, ItemID.CloudinaBottle, expectedLoss: 60),
                () => FallThreshold(host, profiles, human, "fall-lethal", 60, 0, expectedLoss: null),
                () => FallThreshold(host, profiles, human, "fall-actual-defense", 60, ItemID.Shackle, expectedLoss: 59),
                () => FallInjury(host, profiles, human)
            })
                try { check(); }
                catch (JueMingR.Testing.BusinessAssertionException failure)
                { Console.WriteLine(failure); if (first == null) first = ExceptionDispatchInfo.Capture(failure); }
            first?.Throw();
        }

        private static void FallInjury(object host, object profiles, Player human)
        {
            const int upper = 80, bottom = 111, x = 60 * 16 + 8;
            using (var run = new Scenario(host, profiles, human, "fall-health-change", x, upper, () => FallShaft(upper, bottom), ItemID.CloudinaBottle))
            {
                Player body = run.Body;
                body.statLife = 80;
                body.AddBuff(BuffID.Bleeding, 600);
                for (int wait = 0; body.immune && wait < 150; wait++) run.StepOwner(Vector2.Zero);
                Require(!body.immune, "spawn protection expired through the original updates before the actual injury");
                int departure = -1, hit = -1, saved = -1;
                int start = -1;
                for (int tick = 0; tick < 185 && saved < 0; tick++)
                {
                    int target = hit < 0 ? x : x + 24;
                    run.StepToward(new Vector2(target, bottom * 16), new Vector2(2, Math.Min(10, (tick + 1) * .4f)));
                    if (departure < 0 && body.Bottom.Y > upper * 16 + 24 && body.velocity.Y > 0)
                    { departure = tick; start = body.fallStart; }
                    if (departure >= 0 && hit < 0 && body.Bottom.Y > upper * 16 + 120)
                    {
                        double actual = body.Hurt(PlayerDeathReason.ByOther(0), 24, 0, quiet: true, dodgeable: false);
                        Require(body.statLife == 56 && !body.dead, $"the ongoing fall has a real intervening life loss (reported={actual}, life={body.statLife}, immune={body.immune})");
                        hit = tick;
                    }
                    if (hit >= 0 && body.controlJump && body.velocity.Y < 0) saved = tick;
                    if (departure >= 0 && hit >= 0 && saved < 0)
                        Require(body.fallStart == start, "changing the live Follow target did not reset the native fall-start tile");
                }
                Console.WriteLine($"FOLLOW fall escalation departed={departure} injured={hit} rescued={saved} originalFallStart={start} life={body.statLife} at={body.Bottom}");
                Require(departure >= 0 && hit > departure && saved > hit && !body.dead && body.statLife == 56,
                    "a fall made lethal in progress uses the real saved jump with cumulative fall distance");
            }
        }

        private static void FallShaft(int upper, int bottom)
        {
            Fill(48, 84, upper, TileID.Platforms);
            Fill(48, 84, bottom, TileID.Stone);
        }

        private static void FallThreshold(object host, object profiles, Player human, string name, int life, int accessory, int? expectedLoss)
        {
            const int upper = 80, bottom = 111, x = 60 * 16 + 8;
            using (var run = new Scenario(host, profiles, human, name, x, upper, () => FallShaft(upper, bottom), accessory))
            {
                const int potion = 33;
                Player body = run.Body;
                body.statLife = life;
                body.AddBuff(BuffID.Bleeding, 600);
                if (!expectedLoss.HasValue) body.inventory[potion] = new Item(ItemID.LesserHealingPotion);
                Require(Main.CalculateDamagePlayersTake(60, body.statDefense) > 0 && body.statDefense == (accessory == ItemID.Shackle ? 1 : 0),
                    "the fixture gets its actual defense through native accessory observation");
                int down = -1, landing = -1, damaged = -1, rescue = -1;
                for (int tick = 0; tick < 185; tick++)
                {
                    float previousY = body.velocity.Y;
                    int previousStart = body.fallStart;
                    // Give the first, otherwise identical falling player its
                    // native acceleration and terminal speed. It reaches the
                    // lower support before the delayed follower does.
                    run.StepToward(new Vector2(x, bottom * 16), new Vector2(0, Math.Min(10, (tick + 1) * .4f)));
                    if (down < 0 && body.controlDown) down = tick;
                    if (down >= 0 && body.controlJump && rescue < 0) rescue = tick;
                    if (landing < 0 && OnFloor(body, bottom)) landing = tick;
                    if (body.statLife < life && damaged < 0) damaged = tick;
                    if (expectedLoss.HasValue && (tick == rescue || tick == landing || tick == damaged || body.Bottom.Y > (bottom - 1) * 16 && landing < 0))
                        Console.WriteLine($"FOLLOW fall contact name={name} tick={tick} y={body.Bottom.Y} vy={previousY}->{body.velocity.Y} fallStart={previousStart}->{body.fallStart} jump={body.controlJump} life={body.statLife} status={Get(run.Actor, "Status")} route={RouteStep(run.Actor)}");
                    if (expectedLoss.HasValue && landing >= 0 && damaged > landing) break;
                }
                Console.WriteLine($"FOLLOW fall threshold name={name} startLife={life} endLife={body.statLife} defense={body.statDefense} down={down} land={landing} hurt={damaged} rescue={rescue} bottom={body.Bottom}");
                if (expectedLoss.HasValue)
                    Require(down >= 0 && landing > down && damaged >= landing && rescue < 0 && !body.dead && body.statLife == life - expectedLoss.Value,
                        "Follow admits the survivable original fall, permits real Hurt and does not waste a rescue");
                else
                {
                    Require(down < 0 && landing < 0 && damaged < 0 && !body.dead && OnFloor(body, upper) && body.statLife == life,
                        "the same original fall is refused when its guaranteed loss equals current life");
                    var profile = (CompanionProfile)Get(run.Actor, "Profile");
                    Require((bool)Call(host, "UseItemOnce", profile.Id, CompanionInventoryArea.Inventory, potion),
                        "the same actor accepts an ordinary manual potion while the fatal route remains refused");
                    int healed = -1, recoveredRoute = -1, retainedLanding = -1;
                    for (int tick = 0; tick < 160 && retainedLanding < 0; tick++)
                    {
                        run.StepToward(new Vector2(x, bottom * 16), Vector2.Zero);
                        if (healed < 0 && body.statLife > life && body.inventory[potion].IsAir) healed = tick;
                        if (recoveredRoute < 0 && body.controlDown) recoveredRoute = tick;
                        if (body.Bottom.Y == bottom * 16 && body.velocity.Y == 0 && body.statLife < life) retainedLanding = tick;
                        if (tick < 4 || tick == 20 || tick == recoveredRoute)
                        {
                            var navigation = Navigator(run.Actor);
                            var search = GetOptional(navigation, "search");
                            Console.WriteLine($"FOLLOW fall wake tick={tick} body={body.Bottom} life={body.statLife} threshold={(search == null ? null : GetOptional(search, "RootMinimumLife"))} plan={PlanStatus(navigation)} route={RouteStep(run.Actor)}");
                        }
                    }
                    Console.WriteLine($"FOLLOW fall healed threshold potion={healed} down={recoveredRoute} landing={retainedLanding} life={body.statLife}");
                    Require(healed >= 0 && recoveredRoute > healed && retainedLanding > recoveredRoute && !body.dead,
                        "the local rejected descent wakes after actual treatment without changing position before the new Down");
                }
            }
        }

        private static void ControlledDrop(object host, object profiles, Player human)
        {
            const int upper = 80, bottom = 88, wall = 62, x = wall * 16 - 13;
            using (var run = new Scenario(host, profiles, human, "fall-side-landing", x, upper, () =>
            {
                Fill(48, wall - 1, upper, TileID.Platforms);
                Fill(wall, 86, bottom, TileID.Stone);
                Fill(48, 86, 115, TileID.Stone);
                for (int y = 69; y < upper; y++) Fill(wall, wall, y, TileID.Stone);
            }))
            {
                Player body = run.Body;
                body.statLife = 40;
                int entered = -1, steered = -1, landed = -1;
                float start = body.Center.X;
                for (int tick = 0; tick < 230; tick++)
                {
                    run.StepToward(new Vector2((wall + 3) * 16, bottom * 16), new Vector2(2.8f, 4));
                    if (entered < 0 && body.Bottom.Y > upper * 16 + 12 && body.velocity.Y > 0) entered = tick;
                    if (entered >= 0 && steered < 0 && body.controlRight && body.velocity.Y > 1 && body.Center.X > start + 2)
                        steered = tick;
                    if (steered >= 0 && OnFloor(body, bottom)) { landed = tick; break; }
                }
                Console.WriteLine($"FOLLOW controlled drop entered={entered} actualAirRight={steered} landed={landed} body={body.Bottom} route={RouteStep(run.Actor)}");
                Require(entered >= 0 && steered >= entered && landed > steered && body.Center.X > start + 8 && !body.dead && body.wingsLogic == 0,
                    "one unpowered body steers during the native platform descent and lands sideways on actual support");
            }
        }
    }
}
