using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionPlannedMovementChecks
    {
        private const int RemovableBank = 84, BankFloor = 78;
        private static Player bankProbeBody;
        private static int bankRemovalTick;
        internal static void SampleFastMotion(object host, object actor, Player body, Player human)
        {
            var armor = (Item[])body.armor.Clone();
            const int floor = 86;
            try
            {
                Fill(50, 115, floor);
                Setup(host, actor, body, human, Array.Empty<Point>(), 54, floor, ItemID.EoCShield);
                Warm(host, body);
                human.position = AtGround(human, 109, floor);
                Measure("shield", host, body, 24);

                Setup(host, actor, body, human, Array.Empty<Point>(), 51, floor, ItemID.HermesBoots);
                Warm(host, body);
                human.position = AtGround(human, 112, floor);
                for (int i = 0; i < 140 && body.velocity.X < 5; i++) Step(host, body);
                Measure("run", host, body, 40);
            }
            finally { Array.Copy(armor, body.armor, armor.Length); }
        }

        private static void Measure(string activity, object host, Player body, int samples)
        {
            long total = 0, maximum = 0;
            int active = 0;
            float peakSpeed = 0;
            for (int i = 0; i < samples; i++)
            {
                long start = Stopwatch.GetTimestamp();
                Step(host, body);
                long elapsed = Stopwatch.GetTimestamp() - start;
                total += elapsed;
                maximum = Math.Max(maximum, elapsed);
                if (activity == "shield" ? body.dashDelay < 0 : body.velocity.X >= 5) active++;
                peakSpeed = Math.Max(peakSpeed, body.velocity.X);
            }
            double factor = 1000d / Stopwatch.Frequency;
            Console.WriteLine(FormattableString.Invariant($"NAV_FAST activity={activity} samples={samples} active={active} peakSpeed={peakSpeed:F2} totalMs={total * factor:F3} maxMs={maximum * factor:F3}"));
            Require(active >= samples / 2, "same-scene comparison samples sustained physical speed for " + activity);
        }

        internal static void Run(object host, object actor, Player body, Player human)
        {
            var previousTiles = new Dictionary<Point, Tile>();
            for (int x = 45; x <= 115; x++)
                for (int y = 43; y <= 114; y++) previousTiles.Add(new Point(x, y), Main.tile[x, y]);
            var armor = (Item[])body.armor.Clone();
            var position = body.position;
            var ownerPosition = human.position;
            var velocity = body.velocity;
            try
            {
                DashNearCliff(host, actor, body, human, previousTiles.Keys);
                SupportedWaterTakeoff(host, actor, body, human, previousTiles.Keys);
                InvertedTakeoff(host, actor, body, human, previousTiles.Keys);
                CarpetLanding(host, actor, body, human, previousTiles.Keys);
                CarpetLosesBank(host, actor, body, human, previousTiles.Keys);
            }
            finally
            {
                foreach (var entry in previousTiles) Main.tile[entry.Key.X, entry.Key.Y] = entry.Value;
                Array.Copy(armor, body.armor, armor.Length);
                int buff = body.FindBuffIndex(BuffID.Gravitation);
                if (buff >= 0) body.DelBuff(buff);
                body.gravDir = 1;
                body.position = position;
                body.velocity = velocity;
                human.position = ownerPosition;
                Call(Get(Get(actor, "Behavior"), "Navigator"), "ResetPosition");
            }
        }

        private static void DashNearCliff(object host, object actor, Player body, Player human, IEnumerable<Point> area)
        {
            Setup(host, actor, body, human, area, 62, 76, ItemID.EoCShield);
            Fill(50, 68, 76);
            Fill(88, 109, 76);
            Warm(host, body);
            Require(body.dashType == 2 && body.wingsLogic == 0 && body.rocketBoots == 0, "cliff approach has the real effective shield only");
            human.position = AtGround(human, 96, 76);
            float farthest = body.position.X;
            bool dashed = false;
            float highestSpeed = 0;
            for (int i = 0; i < 150; i++)
            {
                Step(host, body);
                dashed |= body.dashDelay < 0 || body.eocDash > 0;
                farthest = Math.Max(farthest, body.position.X);
                highestSpeed = Math.Max(highestSpeed, Math.Abs(body.velocity.X));
            }
            Console.WriteLine($"NAV DASH CLIFF initialRoom={69 * 16 - 62 * 16} dash={dashed} farthest={farthest:F2} lip={69 * 16} speed={highestSpeed:F2} final={body.position}");
            Require(highestSpeed > .5f && !dashed && farthest < 69 * 16 && body.position.Y <= 76 * 16 - body.height + 3,
                "production shield follow approaches then brakes before the real deep cliff instead of dashing from the short ledge");

            Setup(host, actor, body, human, area, 58, 76, ItemID.EoCShield);
            const int runwayLip = 73 * 16;
            Fill(50, 72, 76);
            Fill(95, 113, 76);
            Warm(host, body);
            human.position = AtGround(human, 100, 76);
            int firstDash = -1, firstBrake = -1, removedAt = -1;
            float peak = 0, brakingSpeed = 0, furthestOnRunway = body.position.X;
            bool brakingAtEdit = false;
            var dashTrace = new List<string>();
            object navigation = Get(Get(actor, "Behavior"), "Navigator");
            object mobility = Get(actor, "Mobility");
            for (int i = 0; i < 105; i++)
            {
                bool wasDashing = body.dashDelay < 0;
                float priorSpeed = body.velocity.X;
                // Remove one support tile with enough remaining stopping
                // room but too little room to continue the proven prefix.
                if (firstDash >= 0 && removedAt < 0 && i == firstDash + 4)
                {
                    Require(wasDashing && priorSpeed > 12, "terrain changes during an actual shield dash");
                    Main.tile[72, 76] = new Tile();
                    removedAt = i;
                }
                Step(host, body);
                if (i == removedAt)
                    brakingAtEdit = wasDashing && priorSpeed > 12 && !body.controlRight && body.velocity.X < priorSpeed;
                peak = Math.Max(peak, body.velocity.X);
                furthestOnRunway = Math.Max(furthestOnRunway, body.position.X);
                if (firstDash < 0 && body.dashDelay < 0 && body.eocDash > 0) firstDash = i;
                if (wasDashing && firstBrake < 0 && !body.controlRight)
                { firstBrake = i; brakingSpeed = priorSpeed; }
                if (i < 30)
                {
                    object observed = Get(mobility, "Observed");
                    object staged = GetOptional(navigation, "LastMovementDelivery");
                    dashTrace.Add($"NAV DASH sample={i} feet={body.Bottom}/{body.velocity} delay={body.dashDelay} right={body.controlRight} " +
                        $"ready={Get(observed, "DashReady")} type={Get(observed, "DashType")} prepared={Get(navigation, "dashPreparedUntil")} " +
                        $"nextCheck={Get(navigation, "nextDashCheck")} slowdown={body.runSlowdown:F2} kind={Get(navigation, "ActionKind")} " +
                        $"staged={(staged == null ? null : Get(staged, "Dash"))}");
                }
            }
            Console.WriteLine($"NAV DASH RUNWAY firstDash={firstDash} peak={peak:F2} removedAt={removedAt} firstBrake={firstBrake} " +
                $"brakingAtEdit={brakingAtEdit} brakingSpeed={brakingSpeed:F2} farthest={furthestOnRunway:F2} newLip={runwayLip - 16}");
            if (firstDash < 0 || firstBrake < 0 || !brakingAtEdit) foreach (string sample in dashTrace) Console.WriteLine(sample);
            Require(firstDash >= 0 && peak >= 14 && removedAt >= 0 && brakingAtEdit && brakingSpeed > 12 &&
                furthestOnRunway > 58 * 16 + 160 && furthestOnRunway < runwayLip - 16 && body.position.Y <= 76 * 16 - body.height + 3,
                "a long supported runway permits a native dash and releases it while fast enough to require real braking room before its cliff");

            Setup(host, actor, body, human, area, 58, 76, ItemID.EoCShield);
            body.armor[4] = new Item(ItemID.SandBoots);
            Fill(50, 70, 76);
            for (int x = 50; x <= 62; x++) Main.tile[x, 76].type = TileID.HardenedSand;
            Fill(95, 113, 76);
            Warm(host, body);
            float sandSlowdown = body.runSlowdown;
            Require(body.desertBoots && body.runningOnSand && sandSlowdown > .3f,
                "actual Dunerider movement raises braking friction on hardened sand");
            human.position = AtGround(human, 100, 76);
            bool stone = false, launchedAcross = false;
            for (int i = 0; i < 135; i++)
            {
                Step(host, body);
                stone |= body.position.X >= 63 * 16 && !body.runningOnSand && body.runSlowdown < sandSlowdown;
                launchedAcross |= body.dashDelay < 0;
            }
            Console.WriteLine($"NAV DASH SAND slowdown={sandSlowdown:F2}/{body.runSlowdown:F2} crossed={stone} dash={launchedAcross} final={body.position}");
            Require(stone && !launchedAcross && body.position.X < 71 * 16 && body.position.Y <= 76 * 16 - body.height + 3,
                "hardened sand friction cannot justify a dash that must stop on lower-friction stone before the gap");
        }

        private static void SupportedWaterTakeoff(object host, object actor, Player body, Player human, IEnumerable<Point> area)
        {
            Setup(host, actor, body, human, area, 72, 79);
            Fill(54, 103, 79);
            for (int y = 76; y < 79; y++) Fill(76, 103, y);
            for (int x = 65; x <= 75; x++) for (int y = 77; y <= 78; y++) Main.tile[x, y].liquid = 255;
            Warm(host, body);
            Require(body.wet && body.breath > body.breathMax / 2 && body.wingsLogic == 0 && !body.accFlipper,
                "shore scene starts on a supported wet bottom with normal breath and no swim/flight gear");
            human.position = AtGround(human, 85, 76);
            int jump = -1, landed = -1;
            int initialBreath = body.breath;
            for (int i = 0; i < 250; i++)
            {
                bool wet = body.wet;
                float oldY = body.position.Y;
                Step(host, body);
                if (jump < 0 && wet && body.controlJump && body.velocity.Y < 0 && body.position.Y < oldY) jump = i;
                if (jump >= 0 && !body.wet && body.velocity.Y == 0 && body.position.X > 77 * 16 &&
                    Math.Abs(body.Bottom.Y - 76 * 16) < 3) { landed = i; break; }
            }
            Console.WriteLine($"NAV SHALLOW SHORE jump={jump} landed={landed} breath={initialBreath}/{body.breath} body={body.position} velocity={body.velocity}");
            Require(jump >= 0 && landed > jump && body.breath > body.breathMax / 2,
                "production follows out of shallow supported water using actual takeoff and dry landing, without missing-air escape");
        }

        private static void InvertedTakeoff(object host, object actor, Player body, Player human, IEnumerable<Point> area)
        {
            Setup(host, actor, body, human, area, 68, 70);
            Fill(53, 106, 62);
            for (int y = 63; y <= 65; y++) Fill(76, 77, y);
            body.AddBuff(BuffID.Gravitation, 360);
            body.gravDir = -1;
            body.position = new Vector2(68 * 16, 63 * 16);
            human.position = body.position;
            Warm(host, body);
            Require(body.gravDir == -1 && body.velocity.Y == 0 && Math.Abs(body.position.Y - 63 * 16) < 2,
                "gravity fixture uses real retained inversion and loaded overhead support");
            human.position = new Vector2(89 * 16, 63 * 16);
            int jump = -1, landed = -1;
            float maximumY = body.position.Y;
            object nav = Get(Get(actor, "Behavior"), "Navigator");
            long previousAction = -1;
            for (int i = 0; i < 260; i++)
            {
                Step(host, body);
                maximumY = Math.Max(maximumY, body.position.Y);
                if (jump < 0 && body.controlJump && body.gravDir == -1 && body.velocity.Y > 0) jump = i;
                long action = (long)Get(nav, "ActionId");
                if (i % 35 == 0 || i == jump || action != previousAction || i >= 255)
                    Console.WriteLine($"NAV INVERTED trace step={i} pos={body.position}/{body.velocity} " +
                        $"support={Get(nav, "Supported")} keys={body.controlLeft}/{body.controlRight}/{body.controlJump} " +
                        $"action={action}/{Get(nav, "ActionKind")}/{Get(nav, "ActionOutcome")} query={Get(nav, "QueryStatus")}/{Get(nav, "QueryGeneration")} " +
                        $"{NativeCompanionVerticalFollowChecks.RefinementTrace(nav)} {NativeCompanionVerticalFollowChecks.RouteTrace(nav)}");
                previousAction = action;
                if (jump >= 0 && body.gravDir == -1 && body.position.X > 79 * 16 && (bool)Get(nav, "Supported") &&
                    Math.Abs(body.position.Y - 63 * 16) < 2) { landed = i; break; }
            }
            int settled = -1;
            if (landed >= 0)
            {
                // Native collision can already establish ceiling contact while leaving
                // a clipped nonzero velocity for that first original update.
                for (int i = 0; i <= 4; i++)
                {
                    if (i > 0) Step(host, body);
                    if (body.velocity.Y == 0 && body.gravDir == -1 && (bool)Get(nav, "Supported") &&
                        body.position.X > 79 * 16 && Math.Abs(body.position.Y - 63 * 16) < 2)
                    { settled = landed + i; break; }
                }
            }
            Console.WriteLine($"NAV INVERTED jump={jump} landed={landed} settled={settled} maxY={maximumY:F2} body={body.position} gravity={body.gravDir}");
            Require(jump >= 0 && landed > jump && settled >= landed && maximumY > 66 * 16,
                "production jumps downward away from the inverted ceiling and returns to its physical support beyond the obstruction");
            int buff = body.FindBuffIndex(BuffID.Gravitation);
            if (buff >= 0) body.DelBuff(buff);
        }

        private static void CarpetLanding(object host, object actor, Player body, Player human, IEnumerable<Point> area)
        {
            const int first = 66, lip = 70, destination = 84;
            Setup(host, actor, body, human, area, first, 78);
            Fill(54, lip - 1, 78);
            Fill(destination, 109, 78);
            Fill(lip, destination - 1, 110);
            Warm(host, body);
            human.position = AtGround(human, 93, 78);
            float ordinaryMax = body.position.X;
            for (int i = 0; i < 160; i++)
            {
                Step(host, body);
                ordinaryMax = Math.Max(ordinaryMax, body.position.X);
            }
            Require(ordinaryMax < destination * 16 && body.carpetFrame < 0, "same gap is not crossed by ordinary equipped movement");

            Setup(host, actor, body, human, area, first, 78, ItemID.FlyingCarpet);
            Fill(54, lip - 1, 78);
            Fill(destination, 109, 78);
            Fill(lip, destination - 1, 110);
            Warm(host, body);
            Require(body.canCarpet && body.carpetTime == 0 && body.wingsLogic == 0 && body.rocketBoots == 0,
                "only active flight equipment is a fresh carpet with no allocated ticks yet");
            human.position = AtGround(human, 93, 78);
            int jump = -1, glide = -1, landed = -1, previousFuel = 0, spent = 0;
            object navigation = Get(Get(actor, "Behavior"), "Navigator");
            for (int i = 0; i < 420; i++)
            {
                Step(host, body);
                if (i % 60 == 0)
                {
                    object refine = Get(navigation, "refine");
                    object edge = Get(refine, "Edge");
                    object caps = Get(Get(navigation, "route"), "capabilities");
                    object geometry = Get(caps, "Geometry");
                    Console.WriteLine($"NAV CARPET sample={i} feet={body.Bottom}/{body.velocity} jump={body.controlJump}/{body.releaseJump} " +
                        $"carpet={body.canCarpet}/{body.carpetFrame}/{body.carpetTime} query={Get(navigation, "QueryStatus")}/{Get(navigation, "QueryGeneration")} " +
                        $"capability={Get(caps, "Modes")}/{Get(geometry, "CarpetAvailable")} ticks={Get(caps, "FlightTicks")}/{Get(caps, "MaxFlightTicks")} " +
                        $"action={Get(navigation, "ActionKind")}/{Get(navigation, "ActionOutcome")} " +
                        $"proof={Get(refine, "Active")}/{Get(edge, "Detail")}:{Get(edge, "From")}->{Get(edge, "To")} " +
                        $"steps={Get(refine, "ticks")} reserve={Get(refine, "flightTicks")}");
                }
                if (jump < 0 && body.controlJump && body.velocity.Y < 0 && body.carpetTime == 0) jump = i;
                if (body.carpetFrame >= 0 && body.carpetTime > 0)
                {
                    if (glide < 0) glide = i;
                    else if (body.carpetTime < previousFuel) spent++;
                    previousFuel = body.carpetTime;
                }
                if (glide >= 0 && body.position.X > destination * 16 && body.velocity.Y == 0 &&
                    Math.Abs(body.Bottom.Y - 78 * 16) < 3) { landed = i; break; }
            }
            Console.WriteLine($"NAV CARPET GAP ordinaryMax={ordinaryMax:F2} width={(destination - lip) * 16} jump={jump} glide={glide} spent={spent} landed={landed} body={body.position}");
            Require(jump >= 0 && glide > jump && spent >= 12 && landed > glide,
                "production chooses a normal jump, starts and consumes the actual carpet, then lands on the far shore");
        }

        private static void CarpetLosesBank(object host, object actor, Player body, Player human, IEnumerable<Point> area)
        {
            const int lip = 70;
            Setup(host, actor, body, human, area, 66, BankFloor, ItemID.FlyingCarpet);
            Fill(54, lip - 1, BankFloor);
            Fill(RemovableBank, 109, BankFloor);
            // Losing the glide's destination leaves a genuinely lethal
            // alternative here. The ordinary survivable descent contract
            // is tested independently with native health and impact.
            Fill(lip, RemovableBank - 1, 114);
            Warm(host, body);
            human.position = AtGround(human, 93, BankFloor);
            var observer = new Harmony("JueMingR.Tests.PlannedCarpetBank");
            var action = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionMotionAction", true);
            var execute = AccessTools.Method(action, "Input");
            Require(execute != null, "the chosen glide is visible at the actual native input boundary");
            bankProbeBody = body;
            bankRemovalTick = -1;
            bool jumped = false;
            float furthest = body.position.X;
            int origin = (int)Main.GameUpdateCount;
            observer.Patch(execute, prefix: new HarmonyMethod(typeof(NativeCompanionPlannedMovementChecks), nameof(RemoveBankBeforeGlide)));
            try
            {
                for (int tick = 0; tick < 260; tick++)
                {
                    Step(host, body);
                    jumped |= body.controlJump && body.velocity.Y < 0;
                    furthest = Math.Max(furthest, body.position.X);
                }
            }
            finally { observer.Unpatch(execute, HarmonyPatchType.Prefix, observer.Id); bankProbeBody = null; }
            Console.WriteLine($"NAV CARPET BANK removedBeforeTakeoff={bankRemovalTick - origin} airborneJump={jumped} farthest={furthest:F2} lip={lip * 16}");
            Require(bankRemovalTick >= origin && !jumped && furthest < lip * 16 && body.carpetTime == 0,
                "the production action rechecks the missing far shore before sending the carpet takeoff input");
        }

        private static void RemoveBankBeforeGlide(object __instance, Player body)
        {
            if (bankRemovalTick >= 0 || !ReferenceEquals(body, bankProbeBody) || Get(Get(__instance, "Edge"), "Kind").ToString() != "Flight") return;
            // Change actual terrain after production has picked a glide edge,
            // while leaving the plan, inputs and real Player.Update in charge.
            for (int x = RemovableBank; x <= 109; x++) Main.tile[x, BankFloor] = new Tile();
            bankRemovalTick = (int)Main.GameUpdateCount;
        }

        private static void Setup(object host, object actor, Player body, Player human, IEnumerable<Point> area, int x, int y, int accessory = 0)
        {
            foreach (Point point in area) Main.tile[point.X, point.Y] = new Tile();
            Call(Get(Get(actor, "Behavior"), "Navigator"), "ResetPosition");
            Call(Get(actor, "Mobility"), "Reset", Get(actor, "Generation"));
            for (int i = 0; i < body.armor.Length; i++) body.armor[i] = new Item();
            if (accessory != 0) body.armor[3].SetDefaults(accessory);
            body.gravDir = 1;
            body.dash = body.dashType = body.dashDelay = body.eocDash = 0;
            body.breath = body.breathMax;
            body.position = AtGround(body, x, y);
            body.velocity = Vector2.Zero;
            human.position = body.position;
        }

        private static Vector2 AtGround(Player body, int x, int y) => new Vector2(x * 16, y * 16 - body.height);

        private static void Fill(int left, int right, int y)
        {
            for (int x = left; x <= right; x++)
            {
                Main.tile[x, y].active(true);
                Main.tile[x, y].type = TileID.Stone;
            }
        }

        private static void Warm(object host, Player body)
        {
            for (int i = 0; i < 5; i++) Step(host, body);
        }

        private static void Step(object host, Player body)
        {
            NativeQuickItemChecks.BeginWorldStep();
            Call(host, "Update", (ulong)Main.GameUpdateCount);
            body.Update(body.whoAmI);
        }
    }
}
