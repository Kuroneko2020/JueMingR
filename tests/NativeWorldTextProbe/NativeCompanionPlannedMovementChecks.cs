using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionPlannedMovementChecks
    {
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
                Call(Get(Get(actor, "Behavior"), "navigation"), "CancelRoute");
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
            int firstDash = -1, firstBrake = -1;
            float peak = 0, brakingSpeed = 0, furthestOnRunway = body.position.X;
            var dashTrace = new List<string>();
            for (int i = 0; i < 105; i++)
            {
                bool wasDashing = body.dashDelay < 0;
                float priorSpeed = body.velocity.X;
                // Remove one support tile with enough remaining stopping
                // room but too little room to continue the proven prefix.
                if (i == 4)
                {
                    Require(wasDashing && priorSpeed > 12, "terrain changes during an actual shield dash");
                    Main.tile[72, 76] = new Tile();
                }
                Step(host, body);
                peak = Math.Max(peak, body.velocity.X);
                furthestOnRunway = Math.Max(furthestOnRunway, body.position.X);
                if (firstDash < 0 && body.dashDelay < 0 && body.eocDash > 0) firstDash = i;
                if (wasDashing && firstBrake < 0 && !body.controlRight)
                { firstBrake = i; brakingSpeed = priorSpeed; }
                if (i < 30) dashTrace.Add($"{i}:{body.position.X:F1}/{body.velocity.X:F1}/{body.dashDelay}/{(body.controlRight ? 'R' : '-')}");
            }
            Console.WriteLine($"NAV DASH RUNWAY firstDash={firstDash} peak={peak:F2} firstBrake={firstBrake} brakingSpeed={brakingSpeed:F2} farthest={furthestOnRunway:F2} lip={runwayLip}");
            if (firstDash < 0 || firstBrake < 0) Console.WriteLine(string.Join(" ", dashTrace));
            Require(firstDash >= 0 && peak >= 14 && firstBrake == 4 && brakingSpeed > 12 &&
                furthestOnRunway > 58 * 16 + 160 && furthestOnRunway < runwayLip - 16 && body.position.Y <= 76 * 16 - body.height + 3,
                "a long supported runway permits a native dash and releases it while fast enough to require real braking room before its cliff");
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
            for (int i = 0; i < 260; i++)
            {
                Step(host, body);
                maximumY = Math.Max(maximumY, body.position.Y);
                if (jump < 0 && body.controlJump && body.gravDir == -1 && body.velocity.Y > 0) jump = i;
                if (jump >= 0 && body.gravDir == -1 && body.position.X > 79 * 16 && body.velocity.Y == 0 &&
                    Math.Abs(body.position.Y - 63 * 16) < 2) { landed = i; break; }
            }
            Console.WriteLine($"NAV INVERTED jump={jump} landed={landed} maxY={maximumY:F2} body={body.position} gravity={body.gravDir}");
            Require(jump >= 0 && landed > jump && maximumY > 66 * 16,
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
            for (int i = 0; i < 420; i++)
            {
                Step(host, body);
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

        private static void Setup(object host, object actor, Player body, Player human, IEnumerable<Point> area, int x, int y, int accessory = 0)
        {
            foreach (Point point in area) Main.tile[point.X, point.Y] = new Tile();
            Call(Get(Get(actor, "Behavior"), "navigation"), "CancelRoute");
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
