using System;
using System.Reflection;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private static void NativeDashRangesByInput(object context, object host, object profiles, Player human)
        {
            const int floor = 86;
            var savedArmor = (Item[])human.armor.Clone();
            Item savedMount = human.miscEquips[3];
            try
            {
                foreach (int source in new[] { 2, 3, 5, 6 })
                {
                    var ranges = new float[3];
                    for (int control = 0; control < ranges.Length; control++)
                    {
                        using (var run = new NativeFollowWorld(context, host, profiles, human, "dash-range-" + source + "-" + control,
                            new Vector2(RoomX(44), floor * 16), new Vector2(RoomX(51), floor * 16),
                            () => PlaceFixtureStrip(41, 136, floor, TileID.Stone)))
                        {
                            var profile = (CompanionProfile)Get(run.Actor, "Profile");
                            Require((bool)Call(host, "SetBehavior", profile.Id, new CompanionBehavior
                            { Follow = false, AttackEnabled = false, HealthRecoveryEnabled = false }),
                                "the other original body waits while the isolated native input varies");
                            if (human.mount.Active) human.QuickMount();
                            for (int slot = 0; slot < 4; slot++) human.armor[slot] = new Item();
                            for (int tick = 0; tick < 3; tick++) run.Step();
                            if (source == 2) human.armor[3] = new Item(ItemID.EoCShield);
                            else if (source == 3)
                            {
                                human.armor[0] = new Item(ItemID.SolarFlareHelmet);
                                human.armor[1] = new Item(ItemID.SolarFlareBreastplate);
                                human.armor[2] = new Item(ItemID.SolarFlareLeggings);
                            }
                            else if (source == 5)
                            {
                                human.armor[0] = new Item(ItemID.CrystalNinjaHelmet);
                                human.armor[1] = new Item(ItemID.CrystalNinjaChestplate);
                                human.armor[2] = new Item(ItemID.CrystalNinjaLeggings);
                            }
                            else
                            {
                                human.miscEquips[3] = new Item(ItemID.PalworldPetChillet);
                                human.QuickMount();
                            }
                            for (int tick = 0; tick < 200 && (human.dashType != source || human.dashDelay != 0 || !human.releaseDash); tick++)
                                run.Step();
                            Require(human.dashType == source && human.dashDelay == 0 && human.releaseDash && OnFloor(human, floor),
                                "source, native released key and cooldown all come from actual original updates");
                            float start = human.Bottom.X;
                            run.Step(1, dash: true);
                            float peak = human.velocity.X;
                            Require(human.dashDelay < 0 && human.dash == source && peak > 13f,
                                "one independently mapped native key creates the real dash impulse on the staged source");
                            int active = 1;
                            int positiveWithReverse = 0;
                            for (; active < 100 && human.dashDelay < 0; active++)
                            {
                                int direction = active < 4 ? 1 : 1 - control;
                                float before = human.Bottom.X;
                                run.Step(direction);
                                if (active == 4)
                                    Require(human.controlLeft == (direction < 0) && human.controlRight == (direction > 0),
                                        "the original horizontal controls receive the selected strategy");
                                if (direction < 0 && human.controlLeft && human.Bottom.X > before && human.velocity.X > 0f)
                                    positiveWithReverse++;
                                ranges[control] = Math.Max(ranges[control], human.Bottom.X - start);
                            }
                            int cooldown = 0;
                            while (human.dashDelay > 0 && cooldown < 50) { run.Step(); cooldown++; }
                            Console.WriteLine($"FOLLOW DASH ORACLE type={source} input={1 - control} impulse={peak:F2} " +
                                $"maxForward={ranges[control]:F2} active={active} cooldown={cooldown} reverseWhileForward={positiveWithReverse}");
                            Require(human.dashDelay == 0 && cooldown > 0 && ranges[control] > 30f && !human.dead &&
                                (control != 2 || positiveWithReverse > 0),
                                "actual forward progress during reverse control ends in the original cooldown without forced velocity");
                        }
                    }
                    Require(Math.Min(ranges[0], ranges[1]) > ranges[2] + .1f,
                        "for this original source opposite input reduces the real forward envelope versus neutral or held-forward input");
                }
            }
            finally
            {
                if (human.mount.Active) human.QuickMount();
                human.miscEquips[3] = savedMount;
                Array.Copy(savedArmor, human.armor, savedArmor.Length);
            }
        }

        private static void NativeDashSourcePursuit(object context, object host, object profiles, Player human, int dashType)
        {
            const int floor = 85, mountSlot = 29;
            Item ownerAccessory = human.armor[3];
            try
            {
                human.armor[3] = new Item(ItemID.HermesBoots);
                using (var run = new NativeFollowWorld(context, host, profiles, human, "actual-dash-source-" + dashType,
                    new Vector2(RoomX(46), floor * 16), new Vector2(RoomX(72), floor * 16),
                    () => PlaceFixtureStrip(43, 132, floor, TileID.Stone)))
                {
                    Player body = run.Body;
                    var navigation = Navigator(run.Actor);
                    var profile = (CompanionProfile)Get(run.Actor, "Profile");
                    if (dashType == 3)
                    {
                        body.armor[0] = new Item(ItemID.SolarFlareHelmet);
                        body.armor[1] = new Item(ItemID.SolarFlareBreastplate);
                        body.armor[2] = new Item(ItemID.SolarFlareLeggings);
                        Require((bool)Call(host, "SetBehavior", profile.Id, new CompanionBehavior
                        { Follow = false, AttackEnabled = false, HealthRecoveryEnabled = false }), "stage native armor recovery before Follow");
                        run.Step();
                        Require(body.setSolar && body.solarShields == 0 && body.dashType != 3,
                            "equipping the full original Solar set does not immediately seed its flare or dash eligibility");
                        for (int tick = 0; tick < 200 && body.solarShields == 0; tick++) run.Step();
                        Require(body.setSolar && body.solarShields > 0 && body.dashType == 3,
                            "the original full armor set earns real Solar resources and exposes its dash type before the owner moves");
                        Require((bool)Call(host, "SetBehavior", profile.Id, new CompanionBehavior
                        { Follow = true, AttackEnabled = false, HealthRecoveryEnabled = false }), "resume the ordinary moving owner Follow");
                    }
                    else if (dashType == 5)
                    {
                        body.armor[0] = new Item(ItemID.CrystalNinjaHelmet);
                        body.armor[1] = new Item(ItemID.CrystalNinjaChestplate);
                        body.armor[2] = new Item(ItemID.CrystalNinjaLeggings);
                    }
                    else
                    {
                        body.inventory[mountSlot] = new Item(ItemID.PalworldPetChillet);
                        Require(body.inventory[mountSlot].mountType == MountID.Chillet && body.mount.CanMount(MountID.Chillet, body),
                            "the owned mount item and open native body admit the original mount command");
                        body.QuickMount();
                        Require(body.mount.Active && body.mount.Type == MountID.Chillet,
                            "the original mount command consumes the body's real Chillet source");
                    }

                    int eligible = -1, mounted = -1, impulse = -1, exited = -1, rearmed = -1, ordinary = 0;
                    int readySteps = 0, firstSolar = body.solarShields;
                    float peak = 0f, gapAtImpulse = 0f, impulseY = 0f, sourceX = body.Bottom.X;
                    for (int tick = 0; tick < 170; tick++)
                    {
                        int beforeDelay = body.dashDelay;
                        Vector2 before = body.Bottom;
                        int ownerDirection = tick < 105 ? 1 : tick < 118 ? 0 : -1;
                        run.Step(ownerDirection);
                        if (body.mount.Active && body.mount.Type == MountID.Chillet && mounted < 0) mounted = tick;
                        if (body.dashType == dashType && body.dashDelay == 0 && (tick < 105 || impulse >= 0))
                        {
                            if (eligible < 0) eligible = tick;
                            readySteps++;
                        }
                        if (body.dashDelay < 0 && body.dash == dashType)
                        {
                            peak = Math.Max(peak, Math.Abs(body.velocity.X));
                            if (beforeDelay >= 0 && impulse < 0)
                            {
                                impulse = tick;
                                gapAtImpulse = human.Bottom.X - before.X;
                                impulseY = body.velocity.Y;
                                Console.WriteLine($"FOLLOW DASH SOURCE launch={tick} type={dashType} mount={body.mount.Type} " +
                                    $"velocity={body.velocity} feet={body.Bottom} owner={human.Bottom}/{human.velocity} controls={body.controlLeft}/{body.controlRight}/{body.controlDash} " +
                                    $"solar={body.solarShields} {PlanStatus(navigation)}");
                            }
                        }
                        if (impulse >= 0 && exited < 0 && beforeDelay < 0 && body.dashDelay >= 0) exited = tick;
                        if (exited >= 0 && rearmed < 0 && body.dashDelay == 0 && body.releaseDash) rearmed = tick;
                        if (rearmed >= 0 && body.dashDelay >= 0 && Math.Abs(body.Bottom.X - before.X) > .75f) ordinary++;
                    }
                    Console.WriteLine($"FOLLOW DASH SOURCE type={dashType} eligibility={eligible}/{readySteps} mount={mounted} " +
                        $"launch={impulse} gap={gapAtImpulse:F2} peak={peak:F2} impulseY={impulseY:F2} exited={exited} rearmed={rearmed} " +
                        $"ordinary={ordinary} solar={firstSolar}/{body.solarShields} body={body.Bottom}/{body.velocity} owner={human.Bottom}/{human.velocity}");
                    Require(eligible >= 0 && readySteps >= 3 && !body.dead && run.NativeRelocations == 0 &&
                        (dashType != 6 || mounted >= 0), "the original body really has the identified equipped or mounted impulse source");
                    Require(impulse >= 0 && impulse < 105 && peak > (dashType == 3 ? 19f : 13f) && gapAtImpulse > 100f &&
                        body.Bottom.X - sourceX > 100f && exited > impulse && rearmed >= exited && ordinary >= 8,
                        "the moving-owner follow spends its real source on an original launch and resumes movement after cooldown");
                    if (dashType == 3)
                        Require(firstSolar > 0 && body.solarShields >= firstSolar,
                            "Solar on a clear runway has no collision that consumes a real flare");
                    if (dashType == 6)
                        Require(impulseY < -1f, "a native Chillet ground launch has the actual upwards component of its dash");
                }
            }
            finally { human.armor[3] = ownerAccessory; }
        }

        private static void NativeSlicedDashPursuit(object context, object host, object profiles, Player human)
        {
            FieldInfo budgetOverride = host.GetType().GetField("NavigationBudgetOverride", ReflectionMembers);
            Require(budgetOverride != null, "the sliced dash uses the existing Debug team planning budget");
            object prior = budgetOverride.GetValue(host);
            const int floor = 85;
            budgetOverride.SetValue(host, Make(budgetOverride.FieldType, 208, 4, 6));
            try
            {
                using (var run = new NativeFollowWorld(context, host, profiles, human, "sliced-native-dash",
                    new Vector2(RoomX(45), floor * 16), new Vector2(RoomX(75), floor * 16),
                    () => PlaceFixtureStrip(42, 137, floor, TileID.Stone)))
                {
                    var body = run.Body;
                    body.armor[3] = new Item(ItemID.EoCShield);
                    object navigation = Navigator(run.Actor);
                    object preparation = Get(navigation, "dashPreparation");
                    int priorSteps = 0, stopSlices = 0, priorColumn = 0, laneSlices = 0, resets = 0, launch = -1;
                    float bodyMovementBeforeDash = 0f, ownerMovementBeforeDash = 0f, speed = 0f;
                    for (int tick = 0; tick < 100 && launch < 0; tick++)
                    {
                        float before = body.Bottom.X, beforeOwner = human.Bottom.X;
                        run.Step(1);
                        int steps = (int)Get(Get(preparation, "stop"), "Ticks");
                        if (steps > priorSteps) stopSlices++;
                        if (steps != 0 && steps < priorSteps) resets++;
                        priorSteps = steps;
                        if ((bool)Get(preparation, "hasEnvelope"))
                        {
                            int column = (int)Get(preparation, "cursor");
                            if (priorColumn > 0 && column > priorColumn) laneSlices++;
                            if (priorColumn > 0 && column < priorColumn) resets++;
                            priorColumn = column;
                        }
                        if (body.controlDash && body.dashDelay < 0 && body.dash == 2)
                        { launch = tick; speed = body.velocity.X; }
                        else
                        {
                            bodyMovementBeforeDash += body.Bottom.X - before;
                            ownerMovementBeforeDash += human.Bottom.X - beforeOwner;
                        }
                        if (tick < 22)
                            Console.WriteLine($"FOLLOW SLICED DASH tick={tick} stopSteps={steps} nextColumn={priorColumn} " +
                                $"original={body.Bottom}/{body.velocity} owner={human.Bottom}/{human.velocity} {PlanStatus(navigation)}");
                    }
                    Console.WriteLine($"FOLLOW SLICED DASH launch={launch} velocity={speed:F2} stopSlices={stopSlices} " +
                        $"laneSlices={laneSlices} resets={resets} beforeLaunchMotion={bodyMovementBeforeDash:F2}/{ownerMovementBeforeDash:F2}");
                    Require(launch >= 0 && speed > 13f && stopSlices >= 2 && laneSlices >= 1 && resets == 0 &&
                        bodyMovementBeforeDash > .2f && ownerMovementBeforeDash > .2f && run.NativeRelocations == 0,
                        "the real shared slice advances the prepared stop and lane while both bodies move, then delivers an original dash");
                }
            }
            finally { budgetOverride.SetValue(host, prior); }
        }

        private static void NativeSolarFlareContact(object context, object host, object profiles, Player human)
        {
            const int floor = 85, slot = 19;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "solar-flare-contact",
                new Vector2(RoomX(45), floor * 16), new Vector2(RoomX(74), floor * 16),
                () => PlaceFixtureStrip(42, 134, floor, TileID.Stone)))
            {
                var body = run.Body;
                var profile = (CompanionProfile)Get(run.Actor, "Profile");
                var waiting = new CompanionBehavior { Follow = false, AttackEnabled = false, HealthRecoveryEnabled = false };
                Require((bool)Call(host, "SetBehavior", profile.Id, waiting), "the original Solar timer starts before the measured pursuit");
                body.armor[0] = new Item(ItemID.SolarFlareHelmet);
                body.armor[1] = new Item(ItemID.SolarFlareBreastplate);
                body.armor[2] = new Item(ItemID.SolarFlareLeggings);
                for (int tick = 0; tick < 196 && (body.solarShields == 0 || body.dashType != 3); tick++) run.Step();
                Require(body.setSolar && body.solarShields == 1 && body.dashType == 3,
                    "independent original Solar timer supplies exactly the first source resource");
                var enemy = new NPC();
                NativeCompanionNavigationChecks.InitializeTarget(enemy, slot, body.Center + new Vector2(94f, 0f));
                enemy.damage = 0;
                NPC previous = Main.npc[slot];
                Main.npc[slot] = enemy;
                try
                {
                    var follow = waiting.Clone();
                    follow.Follow = true;
                    Require((bool)Call(host, "SetBehavior", profile.Id, follow), "start real Follow with the single original flare");
                    int launch = -1, contact = -1, recovered = -1;
                    for (int tick = 0; tick < 270 && recovered < 0; tick++)
                    {
                        run.Step(tick < 112 ? 1 : tick < 127 ? 0 : -1);
                        if (launch < 0 && body.dashDelay < 0 && body.dash == 3) launch = tick;
                        if (contact < 0 && launch >= 0 && body.solarDashConsumedFlare && body.solarShields == 0 && enemy.life < enemy.lifeMax)
                        {
                            contact = tick;
                            enemy.active = false;
                        }
                        if (contact >= 0 && body.solarShields == 1 && body.dashType == 3 && body.dashDelay == 0) recovered = tick;
                    }
                    Console.WriteLine($"FOLLOW SOLAR CONTACT launch={launch} impact={contact} paidHealth={enemy.lifeMax - enemy.life} " +
                        $"nativeRegeneration={recovered} resource={body.solarShields} body={body.Bottom}/{body.velocity}");
                    Require(launch >= 0 && contact > launch && recovered > contact && !body.dead && run.NativeRelocations == 0,
                        "original Solar dash contact hits, spends its single flare and regenerates through the native armor timer");
                }
                finally { Main.npc[slot] = previous; }
            }
        }

        private static void NativeWinglessDashPursuit(object context, object host, object profiles, Player human)
        {
            const int floor = 92;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "wingless-air-dash",
                new Vector2(RoomX(46), (floor - 10) * 16), new Vector2(RoomX(69), floor * 16),
                () => PlaceFixtureStrip(42, 133, floor, TileID.Stone)))
            {
                var body = run.Body;
                body.armor[3] = new Item(ItemID.Tabi);
                object navigation = Navigator(run.Actor);
                int usefulAir = 0, launched = -1, landed = -1, continued = 0;
                float impulse = 0f, launchHeight = 0f;
                for (int tick = 0; tick < 145; tick++)
                {
                    float height = floor * 16 - body.Bottom.Y;
                    int beforeDelay = body.dashDelay;
                    float beforeX = body.Bottom.X;
                    if (body.dashType == 1 && beforeDelay == 0 && body.wingsLogic == 0 &&
                        height > 20f && body.velocity.Y > 0f) usefulAir++;
                    run.Step(tick < 103 ? 1 : -1);
                    if (tick < 18)
                        Console.WriteLine($"FOLLOW WINGLESS TRACE tick={tick} floorDistance={height:F2} owner={human.Bottom}/{human.velocity} " +
                            $"body={body.Bottom}/{body.velocity} dash={body.controlDash}/{body.dashDelay} " +
                            $"prepared={GetOptional(navigation, "dashPreparedUntil")} {PlanStatus(navigation)} {RouteTrace(navigation)}");
                    if (launched < 0 && beforeDelay >= 0 && body.dashDelay < 0 && body.dash == 1)
                    { launched = tick; impulse = body.velocity.X; launchHeight = height; }
                    if (landed < 0 && OnFloor(body, floor)) landed = tick;
                    if (landed >= 0 && body.dashDelay >= 0 && Math.Abs(body.Bottom.X - beforeX) > 1f &&
                        (body.controlRight || body.controlLeft)) continued++;
                }
                Console.WriteLine($"FOLLOW WINGLESS DASH originalReadyAir={usefulAir} launch={launched} " +
                    $"impulse={impulse:F2} launchHeight={launchHeight:F2} landed={landed} continued={continued} body={body.Bottom}/{body.velocity}");
                Require(usefulAir > 0 && body.wingsLogic == 0 && !body.mount.Active && run.NativeRelocations == 0,
                    "the original Tabi wearer actually starts airborne without wings or a mount");
                Require(launched > 0 && launchHeight > 20f && impulse > 14f && landed > launched && continued >= 10 && !body.dead,
                    "the original wingless dash advances a moving follow while airborne, lands on safe contact and continues");
            }
        }

        private static void NativeSkatesUseEquippedDash(object context, object host, object profiles, Player human)
        {
            const int floor = 87;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "skates-dash-source",
                new Vector2(RoomX(48), floor * 16), new Vector2(RoomX(76), floor * 16),
                () => PlaceFixtureStrip(43, 141, floor, TileID.Stone)))
            {
                var body = run.Body;
                body.inventory[29] = new Item(ItemID.RollerSkatesBlueMountItem);
                body.QuickMount();
                Require(body.mount.Active && body.mount.Type == MountID.RollerSkates,
                    "the original body activates the real owned skates before testing what they supply");
                object mobility = Get(run.Actor, "Mobility");
                int unarmedOriginal = -1, unarmedObserved = -1, armedOriginal = -1, armedObserved = -1, firstDash = -1;
                bool unarmedDash = false, mountedAtDash = false;
                float impulse = 0f;
                for (int tick = 0; tick < 110; tick++)
                {
                    if (tick == 18) body.armor[3] = new Item(ItemID.Tabi);
                    run.Step(1);
                    int observed = (int)Get(Get(mobility, "Observed"), "DashType");
                    if (tick == 17) { unarmedOriginal = body.dashType; unarmedObserved = observed; }
                    if (tick < 18) unarmedDash |= body.controlDash || body.dashDelay < 0;
                    if (tick >= 18 && body.dashType == 1 && armedOriginal < 0)
                    { armedOriginal = body.dashType; armedObserved = observed; }
                    if (tick >= 18 && firstDash < 0 && body.dashDelay < 0 && body.dash == 1)
                    { firstDash = tick; impulse = body.velocity.X; mountedAtDash = body.mount.Active && body.mount.Type == MountID.RollerSkates; }
                }
                Console.WriteLine($"FOLLOW SKATES DASH unarmed={unarmedOriginal}/{unarmedObserved} requestedWithoutGear={unarmedDash} " +
                    $"armed={armedOriginal}/{armedObserved} launch={firstDash} mounted={mountedAtDash} impulse={impulse:F2}");
                Require(unarmedOriginal == 0 && unarmedObserved == 0 && !unarmedDash && armedOriginal == 1 && armedObserved == 1,
                    "skates permit the equipped dash but do not manufacture a native mount dash of their own");
                Require(firstDash >= 18 && mountedAtDash && impulse > 14f && run.NativeRelocations == 0 && !body.dead,
                    "the same body can consume the equipped Tabi impulse while actually riding skates");
            }
        }

        private static void NativeRamDescent(object context, object host, object profiles, Player human)
        {
            const int upper = 80, lower = 94;
            float start = RoomX(65);
            using (var run = new NativeFollowWorld(context, host, profiles, human, "ram-rune-descent",
                new Vector2(start, upper * 16), new Vector2(start, lower * 16), () =>
                {
                    PlaceFixtureStrip(44, 119, upper, TileID.Platforms);
                    PlaceFixtureStrip(44, 131, lower, TileID.Stone);
                }))
            {
                var body = run.Body;
                body.armor[3] = new Item(ItemID.DeadCellsRamRune);
                body.armor[4] = new Item(ItemID.AngelWings);
                int firstDown = -1, firstAir = -1, nativeRam = -1, ended = -1, land = -1, resumed = -1;
                float observedFallSpeed = 0f, ramAdvance = 0f, retainedWing = 0f, peakJump = 0f;
                for (int tick = 0; tick < 150; tick++)
                {
                    bool wasRamming = body.isPerformingJump_DownDash;
                    float previousY = body.Bottom.Y;
                    run.Step(tick < 52 ? 0 : 1, jump: tick >= 103 && tick < 116);
                    if (firstDown < 0 && body.controlDown) firstDown = tick;
                    if (firstAir < 0 && body.Bottom.Y > upper * 16 + 2f && body.velocity.Y > 0f) firstAir = tick;
                    if (nativeRam < 0 && !wasRamming && body.isPerformingJump_DownDash)
                    {
                        nativeRam = tick;
                        observedFallSpeed = body.velocity.Y;
                        ramAdvance = body.Bottom.Y - previousY;
                        retainedWing = body.wingTime;
                        Require(body.controlJump && body.controlDown && ramAdvance > 12f && body.dashDelay == 0,
                            "a fresh native Down+Jump actually yields a vertical dash, without a horizontal hotkey or cooldown");
                    }
                    if (nativeRam >= 0 && ended < 0 && wasRamming && !body.isPerformingJump_DownDash) ended = tick;
                    if (land < 0 && OnFloor(body, lower)) land = tick;
                    if (resumed < 0 && land >= 0 && body.controlRight && body.Bottom.X > start + 14f) resumed = tick;
                    if (land >= 0 && body.controlJump) peakJump = Math.Max(peakJump, lower * 16 - body.Bottom.Y);
                }
                Console.WriteLine($"FOLLOW RAM RUNE Down={firstDown} free={firstAir} ram={nativeRam} speed={observedFallSpeed:F2} advance={ramAdvance:F2} " +
                    $"wing={retainedWing:F2} ended={ended} land={land} resumed={resumed} afterJumpHeight={peakJump:F2} body={body.Bottom}/{body.velocity}");
                Require(body.hasDeadCellsDownDash && body.wingsLogic > 0 && firstAir >= 0 && land > firstAir && resumed > land &&
                    !body.dead && run.NativeRelocations == 0,
                    "the equipped original body uses a real supported platform drop, then resumes the moving owner on the lower floor");
                // Player.Update caps the observed terminal speed after the
                // dash movement. The Ram flag retires on the following native contact update.
                Require(nativeRam >= firstAir && observedFallSpeed > 8f && ramAdvance > 12f && retainedWing > 0f &&
                    land > nativeRam && ended >= land && ended <= land + 2,
                    "the real Ram Rune descends on the chosen drop, ends at native contact, and retains the fitted wings");
            }
        }

        private static void NativeDashBrakingAgainstRearWall(object context, object host, object profiles, Player human)
        {
            const int floor = 76, removedSupport = 72;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "brake-by-rear-wall",
                new Vector2(RoomX(58) - 4f, floor * 16), new Vector2(RoomX(100), floor * 16), () =>
                {
                    PlaceFixtureStrip(50, removedSupport, floor, TileID.Stone);
                    PlaceFixtureStrip(95, 137, floor, TileID.Stone);
                }))
            {
                Player body = run.Body;
                body.armor[3] = new Item(ItemID.EoCShield);
                int launch = -1, edited = -1;
                float rearClearance = -1f, beforeSpeed = 0f, afterSpeed = 0f, actualMove = 0f, farthest = body.Left.X;
                bool appliedReverse = false, wallStayed = false;
                for (int tick = 0; tick < 115; tick++)
                {
                    bool editNow = launch >= 0 && tick == launch + 4;
                    float before = body.Left.X;
                    if (editNow)
                    {
                        beforeSpeed = body.velocity.X;
                        int wall = (int)Math.Floor(before / 16f) - 1;
                        rearClearance = before - (wall + 1) * 16;
                        for (int y = floor - 3; y < floor; y++) PlaceFixtureTile(wall, y, TileID.Stone);
                        Main.tile[removedSupport, floor] = new Tile();
                        edited = tick;
                    }
                    run.Step(1);
                    farthest = Math.Max(farthest, body.Left.X);
                    if (launch < 0 && body.dashDelay < 0 && body.eocDash > 0) launch = tick;
                    if (editNow)
                    {
                        afterSpeed = body.velocity.X;
                        actualMove = body.Left.X - before;
                        appliedReverse = body.controlLeft && !body.controlRight;
                        wallStayed = Main.tile[(int)Math.Floor(before / 16f) - 1, floor - 2].nactive();
                    }
                }
                Console.WriteLine($"FOLLOW REAR WALL launch={launch} edit={edited} gap={rearClearance:F3} wall={wallStayed} " +
                    $"reverse={appliedReverse} speed={beforeSpeed:F3}->{afterSpeed:F3} forwardDisplacement={actualMove:F3} " +
                    $"farthest={farthest:F2} end={body.Bottom}/{body.velocity}");
                Require(launch >= 0 && edited == launch + 4 && beforeSpeed > 11f && wallStayed &&
                    rearClearance >= 0 && rearClearance < 3f,
                    "a real dash still has momentum when the tail wall blocks a three-pixel backward step and the front runway changes");
                Require(appliedReverse && actualMove > 0f && afterSpeed > 0f && afterSpeed < beforeSpeed &&
                    farthest < removedSupport * 16f && OnFloor(body, floor) && !body.dead && run.NativeRelocations == 0,
                    "the same native update applies reverse braking based on its true forward sweep despite the close rear wall");
            }
        }
    }
}
