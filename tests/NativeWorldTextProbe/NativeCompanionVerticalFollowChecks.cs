using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using JueMingR.Platform.Companions;
using JueMingR.Testing;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Only the fixture's owner trajectory is driven after native readiness.
    // Each sample owns a saved companion and consumes the normal Follow input.
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private const int Starboard = 4954;
        private const int Width = 160;
        private const int Height = 160;
        // The normal same-support Follow spacing from CompanionBehaviorHost.
        private const float OrdinaryFollowSpacing = 34f;

        internal static void Run(object context, string comparisonFocus = null, bool historical = false)
        {
            new Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(AccessTools.Method(typeof(Player), "Spawn"),
                HarmonyPatchType.Prefix, "JueMingR.Tests.QuickItemOutlets");
            Main.spawnTileX = 60; Main.spawnTileY = 58;
            Main.leftWorld = Main.topWorld = 0; Main.rightWorld = Main.maxTilesX * 16; Main.bottomWorld = Main.maxTilesY * 16;
            Fill(2, 118, 60, TileID.Stone);
            NativeCompanionCoreLifecycleChecks.PrepareHuman();
            var host = Get(context, "Companions");
            var profiles = Get(host, "profiles");
            NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); Call(host, "Update", 0UL); return (bool)Get(profiles, "Ready"); });
            var originalTiles = Main.tile;
            int oldWidth = Main.maxTilesX, oldHeight = Main.maxTilesY;
            float oldRight = Main.rightWorld, oldBottom = Main.bottomWorld;
            var human = Main.LocalPlayer;
            var ownerStart = human.position;
            var ownerSpeed = human.velocity;
            var wingStats = ArmorIDs.Wing.Sets.Stats;
            ExceptionDispatchInfo firstFailure = null;
            try
            {
                Main.maxTilesX = Width; Main.maxTilesY = Height;
                Main.rightWorld = Width * 16; Main.bottomWorld = Height * 16;
                Terraria.Initializers.WingStatsInitializer.Load();
                var existing = new Action<object, object, Player>[]
                    { Platform, Ledge, UnsafeLedge, StarboardFlight, Sequence, HoverTransitions, ChangedFallEquipment, ChangedShaftLiquid, ChangedLedgeWall };
                var terrain = new Action<object, object, Player>[]
                    { NativeCompanionDoorFollowChecks.HalfBrick, LowCeiling, DescendingDetour, RetainedPlan, LiquidEntry, NativeCompanionFacilityFollowChecks.PylonFollow };
                var practicalFollow = new Action<object, object, Player>[]
                    { AdjacentPlatforms, ControlledDrop, TolerableFalls, TrailFollow };
                var longerFollow = new Action<object, object, Player>[] { (h, p, owner) => LongAutonomousDetour(context, h, p, owner), RouteGraphContracts };
                var contact = new Action<object, object, Player>[] { NativeContactAndDescent, RunUpJumpAndHighExit };
                var hoik = new Action<object, object, Player>[] { (h, p, owner) => NativeHoikBaseline(context, owner) };
                var structure = new Action<object, object, Player>[] { (h, p, owner) => HoikFollow(context, h, p, owner),
                    (h, p, owner) => MixedNativeFollow(context, h, p, owner) };
                var moving = new Action<object, object, Player>[]
                {
                    (h, p, owner) => MovingRoom(context, h, p, owner),
                    (h, p, owner) => MovingRoom(context, h, p, owner, interruptedTeam: true),
                    (h, p, owner) => TriangleProxyFollow(context, h, p, owner),
                    (h, p, owner) => MovingStarboardRoom(context, h, p, owner)
                };
                var timing = moving;
                if (!historical)
                {
                    var partition = new Action<object, object, Player>[]
                    {
                        (h, p, owner) => NativeOwnerReverses(context, h, p, owner),
                        (h, p, owner) => NativeMovingFall(context, h, p, owner),
                        (h, p, owner) => NativeAirDashSelection(context, h, p, owner),
                        (h, p, owner) => NativeGroundMountSelection(context, h, p, owner),
                        (h, p, owner) => MovingNearPartition(context, h, p, owner),
                        (h, p, owner) => MovingNearPartition(context, h, p, owner, interruptJump: true),
                        (h, p, owner) => MovingStarboardCeiling(context, h, p, owner),
                        (h, p, owner) => SlicedLongHookProof(context, h, p, owner),
                        (h, p, owner) => NativeMovedActionRetires(context, h, p, owner)
                    };
                    moving = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(moving, partition));
                }
                bool fixedBudgetAvailable = host.GetType().GetField("NavigationBudgetOverride", ReflectionMembers) != null;
                if (comparisonFocus == null)
                    Require(fixedBudgetAvailable, "the formal navigation leaf runs with the real Debug measurement and pressure seam");
                Console.WriteLine("FOLLOW VERTICAL checks: native behavior scenarios; " +
                    (fixedBudgetAvailable ? "including fixed Debug budget and counters" : "ordinary Host with no Debug budget override"));
                if (fixedBudgetAvailable)
                {
                    var pressure = new Action<object, object, Player>[] { (h, p, owner) => MovingRoom(context, h, p, owner, stress: true) };
                    moving = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(moving, pressure));
                }
                var full = System.Linq.Enumerable.Concat(
                    System.Linq.Enumerable.Concat(moving, System.Linq.Enumerable.Concat(existing, terrain)),
                    System.Linq.Enumerable.Concat(practicalFollow, System.Linq.Enumerable.Concat(contact,
                        System.Linq.Enumerable.Concat(longerFollow, System.Linq.Enumerable.Concat(hoik, structure)))));
                var scenarios = comparisonFocus == "terrain" ? terrain : comparisonFocus == "vertical" ? existing :
                    comparisonFocus == "wormhole-follow" ? practicalFollow :
                    comparisonFocus == "detour-descent" ? longerFollow : comparisonFocus == "contact-descent" ? contact :
                    comparisonFocus == "hoik-baseline" ? hoik : comparisonFocus == "movement-structure" ? structure :
                    comparisonFocus == "graph" ? new Action<object, object, Player>[] { RouteGraphContracts } :
                    comparisonFocus == "timing" ? timing :
                    comparisonFocus == "moving-room" ?
                        historical ? moving : System.Linq.Enumerable.Concat(moving, new Action<object, object, Player>[] { RouteGraphContracts }) :
                        full;
                foreach (Action<object, object, Player> scenario in scenarios)
                {
                    try { scenario(host, profiles, human); }
                    catch (BusinessAssertionException failure)
                    {
                        Console.WriteLine("FOLLOW VERTICAL FAILURE " + scenario.Method.Name + ": " + failure);
                        if (firstFailure == null) firstFailure = ExceptionDispatchInfo.Capture(failure);
                    }
                }
                firstFailure?.Throw();
                Console.WriteLine("PASS production Follow: observed native exits, search handoff and continuous motion.");
            }
            finally
            {
                Main.tile = originalTiles;
                Main.maxTilesX = oldWidth; Main.maxTilesY = oldHeight;
                Main.rightWorld = oldRight; Main.bottomWorld = oldBottom;
                human.position = ownerStart; human.velocity = ownerSpeed;
                ArmorIDs.Wing.Sets.Stats = wingStats;
            }
        }

        private static void Platform(object host, object profiles, Player human)
        {
            const int high = 80, low = 86, x = 60 * 16 + 8;
            using (var run = new Scenario(host, profiles, human, "vertical-platform", x, high, () =>
            {
                Fill(45, 83, high, TileID.Platforms);
                Fill(45, 83, low, TileID.Stone);
            }))
            {
                int input = -1, crossed = -1, landed = -1, stable = 0, longestStable = 0, resumed = -1;
                for (int tick = 0; tick < 188; tick++)
                {
                    run.StepToward(new Vector2(tick < 108 ? x : x + 128, low * 16), new Vector2(3.2f, 4));
                    if (input < 0 && run.Body.controlDown) input = tick;
                    if (crossed < 0 && run.Body.Bottom.Y > high * 16 + 16 && run.Body.velocity.Y > 0) crossed = tick;
                    if (crossed >= 0 && OnFloor(run.Body, low))
                    {
                        if (landed < 0) landed = tick;
                        if (tick >= 108 && resumed < 0 && run.Body.controlRight && run.Body.Center.X > x + 16) resumed = tick;
                    }
                    if (tick < 108)
                    {
                        stable = crossed >= 0 && OnFloor(run.Body, low) && Math.Abs(run.Body.Center.X - x) <= 20 ? stable + 1 : 0;
                        longestStable = Math.Max(longestStable, stable);
                    }
                }
                Console.WriteLine(FormattableString.Invariant($"FOLLOW A platform lowerDelta=96 down={input} entered={crossed} landing={landed} settledBeforeMove={longestStable} resumed={resumed} actor={run.Body.Bottom} status={Get(run.Actor, "Status")}"));
                Require(input >= 0 && crossed > input && landed > crossed && longestStable >= 20 && resumed >= 108 &&
                    OnFloor(run.Body, low) && Math.Abs(run.Body.Center.X - human.Center.X) <= 28,
                    "A: an aligned owner 96 pixels below gets a native platform crossing, a stable lower landing and resumed lower-floor Follow");
            }
        }

        private static void ChangedFallEquipment(object host, object profiles, Player human)
        {
            const int high = 70, low = 114, column = 60, x = column * 16 + 8, storeSlot = 41;
            Action terrain = () =>
            {
                Fill(45, 83, high, TileID.Platforms);
                Fill(45, 83, low, TileID.Stone);
            };
            var destination = new Vector2(x, low * 16);
            int down = -1, landed = -1;
            using (var protectedFall = new Scenario(host, profiles, human, "drop-horseshoe", x, high, terrain, ItemID.LuckyHorseshoe))
            {
                // The owner starts on the real lower support in both trials.
                // This isolates equipment timing from acquiring a falling target.
                human.Bottom = destination;
                Require(protectedFall.Body.noFallDmg && protectedFall.Body.wingsLogic == 0, "the deep drop uses an actual horseshoe and ordinary gravity");
                int life = protectedFall.Body.statLife;
                for (int tick = 0; tick < 245 && landed < 0; tick++)
                {
                    protectedFall.StepOwner(Vector2.Zero);
                    if (down < 0 && protectedFall.Body.controlDown) down = tick;
                    if (down >= 0 && OnFloor(protectedFall.Body, low)) landed = tick;
                }
                Console.WriteLine($"FOLLOW protected shaft owner={human.Bottom} body={protectedFall.Body.Bottom}/{protectedFall.Body.velocity} down={down} landed={landed} life={protectedFall.Body.statLife}/{life} {PlanStatus(Navigator(protectedFall.Actor))}");
                Require(down >= 0 && landed > down && protectedFall.Body.statLife == life,
                    "shared descent planning reaches a real protected landing over 512 px below the start");
            }

            using (var changed = new Scenario(host, profiles, human, "remove-horseshoe", x, high, terrain, ItemID.LuckyHorseshoe))
            {
                human.Bottom = destination;
                for (int tick = 0; tick < down; tick++) changed.StepOwner(Vector2.Zero);
                Require(changed.Body.noFallDmg && OnFloor(changed.Body, high) && !changed.Body.controlDown,
                    "the matching Follow step still stands above the protected shaft before an equipment commit");
                var id = ((CompanionProfile)Get(changed.Actor, "Profile")).Id;
                var gestureType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionItemGesture", true);
                var left = Enum.Parse(gestureType, "Left");
                Require((bool)Call(host, "ItemGesture", id, false, CompanionInventoryArea.Armor, 3, left) &&
                    (bool)Call(host, "ItemGesture", id, false, CompanionInventoryArea.Inventory, storeSlot, left) &&
                    changed.Body.armor[3].IsAir && changed.Body.inventory[storeSlot].type == ItemID.LuckyHorseshoe &&
                    changed.Body.noFallDmg && (bool)Get(changed.Actor, "TileReachPending"),
                    "the real equipment gesture commits before the native effect can refresh");
                changed.StepOwner(Vector2.Zero);
                Require(!changed.Body.controlDown && !changed.Body.noFallDmg && OnFloor(changed.Body, high),
                    "Follow waits for native equipment observation on the exact former start step");
                for (int tick = down + 1; tick < 245; tick++)
                {
                    changed.StepOwner(Vector2.Zero);
                    Require(!changed.Body.controlDown && OnFloor(changed.Body, high), "the now-ordinary body refuses the deep drop");
                }
                Console.WriteLine($"FOLLOW equipment drop={(low - high) * 16} protectedDown={down} protectedLanding={landed} removedDown=False remainingOnPlatform=True");
            }

            CheckNewShaftLiquid(host, profiles, human, "deep-shaft-liquid", high, low, column, high + 35,
                down, terrain, ItemID.LuckyHorseshoe, ownerOnLanding: true);
        }

        private static void ChangedShaftLiquid(object host, object profiles, Player human)
        {
            const int high = 80, low = 86, column = 60;
            const int x = column * 16 + 8;
            Action terrain = () =>
            {
                Fill(45, 83, high, TileID.Platforms);
                Fill(45, 83, low, TileID.Stone);
            };
            var destination = new Vector2(x, low * 16);
            var approach = new Vector2(0, 4);
            int down = 0;
            using (var dry = new Scenario(host, profiles, human, "dry-drop-commit", x, high, terrain))
            {
                for (; down < 120; down++)
                {
                    dry.StepToward(destination, approach);
                    if (dry.Body.controlDown) break;
                }
                Require(down < 120 && Math.Abs(dry.Body.Bottom.Y - high * 16) <= 1,
                    "the real dry Follow route begins with Down from its platform");
            }
            CheckNewShaftLiquid(host, profiles, human, "shaft-liquid-commit", high, low, column, high + 3, down, terrain);
            ContinueAfterNativeRelease(host, profiles, human, high, low, x, terrain);
        }

        private static void ContinueAfterNativeRelease(object host, object profiles, Player human, int high, int low,
            int x, Action terrain)
        {
            using (var run = new Scenario(host, profiles, human, "drop-after-release", x, high, terrain, ItemID.LuckyHorseshoe))
            {
                var body = run.Body;
                var nav = Navigator(run.Actor);
                var destination = new Vector2(x, low * 16);
                long action = 0;
                int release = -1, refreshed = -1, contact = -1, landed = -1;
                int life = body.statLife;
                for (int tick = 0; tick < 120 && landed < 0; tick++)
                {
                    run.StepToward(destination, new Vector2(0, 4));
                    if (release < 0 && body.controlDown && body.Bottom.Y > high * 16 &&
                        body.Bottom.Y < high * 16 + 6 && body.velocity.Y > 0)
                    {
                        release = tick;
                        action = (long)Get(nav, "ActionId");
                        Require(action > 0 && body.stairFall && body.noFallDmg,
                            "the real native Down and stairFall have released the protected body before the six-pixel marker");
                        var id = ((CompanionProfile)Get(run.Actor, "Profile")).Id;
                        object left = Enum.Parse(NavigationType(host.GetType().Assembly, "CompanionItemGesture"), "Left");
                        Require((bool)Call(host, "ItemGesture", id, false, CompanionInventoryArea.Armor, 3, left) &&
                            (bool)Call(host, "ItemGesture", id, false, CompanionInventoryArea.Inventory, 41, left) &&
                            body.armor[3].IsAir && body.inventory[41].type == ItemID.LuckyHorseshoe,
                            "the real equipment transaction reaches the same already-descending body");
                    }
                    else if (release >= 0)
                    {
                        if (refreshed < 0 && !body.noFallDmg) refreshed = tick;
                        if (contact < 0 && (bool)Get(nav, "Supported") && Math.Abs(body.Bottom.Y - low * 16) < .25f) contact = tick;
                        if (OnFloor(body, low)) landed = tick;
                        // A real support contact can complete the old action in this same update.
                        Require((contact >= 0 || (long)Get(nav, "ActionId") == action) && Get(nav, "ActionOutcome").ToString() != "Superseded",
                            $"the released body preserves the viable landing after equipment changes (release={release}/{action} tick={tick} " +
                            $"current={Get(nav, "ActionId")}/{Get(nav, "ActionOutcome")} feet={body.Bottom} actualNoFall={body.noFallDmg})");
                    }
                }
                Console.WriteLine($"FOLLOW release gear change down={release}/{action} nativeRefresh={refreshed} contact={contact} settled={landed} life={life}->{body.statLife}");
                Require(release >= 0 && refreshed > release && contact > refreshed && landed >= contact && body.statLife == life && !body.dead &&
                    ReferenceEquals(body, Get(run.Actor, "Body")), "the original committed drop finishes on native support with unchanged life");
            }
        }

        private static void CheckNewShaftLiquid(object host, object profiles, Player human, string name, int high,
            int low, int column, int hazardRow, int down, Action terrain, int accessory = 0, bool ownerOnLanding = false)
        {
            int x = column * 16 + 8;
            var destination = new Vector2(x, low * 16);
            var approach = new Vector2(0, 4);
            using (var edited = new Scenario(host, profiles, human, name, x, high, terrain, accessory))
            {
                if (ownerOnLanding) human.Bottom = destination;
                for (int tick = 0; tick < down; tick++) Step();
                Require(OnFloor(edited.Body, high) && !edited.Body.controlDown, "the second body is still at the same platform release pose");
                for (int tileX = column - 1; tileX <= column + 1; tileX++)
                {
                    Tile hazard = Main.tile[tileX, hazardRow];
                    hazard.liquid = byte.MaxValue;
                    hazard.lava(true);
                }
                for (int tick = 0; tick < 24; tick++)
                {
                    Step();
                    Require(!edited.Body.controlDown && OnFloor(edited.Body, high),
                        $"new lava between unchanged release and landing blocks the exact old Down and its retries (tick={tick}, feet={edited.Body.Bottom}, velocity={edited.Body.velocity}, down={edited.Body.controlDown}, route={RouteStep(edited.Actor)})");
                }
                for (int tileX = column - 1; tileX <= column + 1; tileX++) Main.tile[tileX, hazardRow].liquid = 0;
                int resumed = 0, clearDown = -1;
                for (; resumed < 135 && !OnFloor(edited.Body, low); resumed++)
                {
                    Step();
                    if (clearDown < 0 && edited.Body.controlDown) clearDown = resumed;
                }
                Console.WriteLine($"FOLLOW shaft liquid depth={(low - high) * 16} hazardBelow={(hazardRow - high) * 16} dryDown={down} changedDown=False clearDown={clearDown} clearLanding={resumed} body={edited.Body.Bottom}/{edited.Body.velocity} status={Get(edited.Actor, "Status")} route={RouteStep(edited.Actor)}");
                Require(clearDown >= 0 && resumed > clearDown && resumed < 135,
                    "removing the new liquid restores native Down followed by the lower landing");

                void Step()
                {
                    if (ownerOnLanding) edited.StepOwner(Vector2.Zero);
                    else edited.StepToward(destination, approach);
                }
            }
        }

        private static void ChangedLedgeWall(object host, object profiles, Player human)
        {
            const int high = 80, lower = 86, deep = 115, edgeColumn = 65, edge = edgeColumn * 16;
            const int wallColumn = edgeColumn + 2, start = edge - 53, target = edge + 55;
            Action terrain = () =>
            {
                Fill(47, edgeColumn - 1, high, TileID.Stone);
                Fill(wallColumn, wallColumn + 2, lower, TileID.Stone);
                Fill(47, 90, deep, TileID.Stone);
            };
            Action<Scenario> step = run => run.StepToward(new Vector2(target, (human.Left.X < edge ? high : lower) * 16), new Vector2(3.2f, 4));
            const int updates = 180;
            int editAt = -1, release = -1, landing = -1;
            float dryLeft = 0;
            using (var dry = new Scenario(host, profiles, human, "dry-ledge-side", start, high, terrain))
            {
                var navigator = Navigator(dry.Actor);
                long priorAction = -1;
                for (int tick = 0; tick < updates; tick++)
                {
                    step(dry);
                    if (release < 0 && OnFloor(dry.Body, high) && dry.Body.velocity.X > 1 &&
                        edge - dry.Body.Left.X > OriginalBrakeRoom(dry.Body) + 3 &&
                        RouteStep(dry.Actor).StartsWith("Drop:", StringComparison.Ordinal)) editAt = tick + 1;
                    if (release < 0 && dry.Body.Left.X >= edge && dry.Body.Bottom.Y > high * 16 + .1f) release = tick;
                    long action = (long)Get(navigator, "ActionId");
                    if (tick % 20 == 0 || action != priorAction || tick == editAt || tick == release)
                    {
                        var refinement = Get(navigator, "refine");
                        var candidate = Get(refinement, "Edge");
                        Console.WriteLine($"FOLLOW side wall dry tick={tick} owner={human.Bottom}/{human.velocity} body={dry.Body.Bottom}/{dry.Body.velocity} " +
                            $"inputs={dry.Body.controlLeft}/{dry.Body.controlRight}/{dry.Body.controlJump} edit={editAt} release={release} " +
                            $"route={RouteStep(dry.Actor)} refinement={Get(refinement, "Active")}/{Get(Get(refinement, "Witness"), "Valid")} " +
                            $"candidate={Get(candidate, "Kind")}:{Get(candidate, "From")}→{Get(candidate, "To")} {PlanStatus(navigator)} {RouteTrace(navigator)}");
                    }
                    priorAction = action;
                    if (release >= 0 && OnFloor(dry.Body, lower)) { landing = tick; dryLeft = dry.Body.Left.X; break; }
                }
                Require(editAt >= 0 && release > editAt && landing > release && dryLeft + dry.Body.width > wallColumn * 16,
                    $"the moving body commits from a still-brakeable ledge and lands on the narrow dry shelf (edit={editAt}, release={release}, landing={landing}, left={dryLeft})");
            }

            void AddWall(Scenario run, int lastRow)
            {
                for (int tick = 0; tick < editAt; tick++) step(run);
                Vector2 before = run.Body.Bottom;
                float speed = run.Body.velocity.X;
                string route = RouteStep(run.Actor);
                Require(OnFloor(run.Body, high) && edge - run.Body.Left.X > OriginalBrakeRoom(run.Body) + 3 &&
                    speed > 1 && route.StartsWith("Drop:", StringComparison.Ordinal),
                    $"the published ledge route still has enough original braking room (body={before}, speed={speed}, route={route})");
                for (int row = high - 4; row <= lastRow; row++) Fill(wallColumn, wallColumn, row, TileID.Stone);
            }

            bool safeShortWall;
            using (var shortWall = new Scenario(host, profiles, human, "ledge-short-wall", start, high, terrain))
            {
                // The body can regain horizontal motion once its head
                // clears this wall, just before reaching the narrow shelf.
                AddWall(shortWall, high + 2);
                int life = shortWall.Body.statLife;
                int leftEdge = -1, underWall = -1, landed = -1, stable = 0;
                for (int tick = 0; tick < updates && stable < 12; tick++)
                {
                    step(shortWall);
                    if (leftEdge < 0 && shortWall.Body.Bottom.Y > high * 16 + .1f) leftEdge = tick;
                    bool overlapsWall = shortWall.Body.Right.X > wallColumn * 16 && shortWall.Body.Left.X < (wallColumn + 1) * 16;
                    if (leftEdge >= 0 && underWall < 0 && shortWall.Body.Top.Y >= (high + 3) * 16 - .25f && overlapsWall &&
                        shortWall.Body.controlRight && shortWall.Body.velocity.X > 0)
                        underWall = tick;
                    bool onShelf = OnFloor(shortWall.Body, lower) && shortWall.Body.Right.X > wallColumn * 16 &&
                        shortWall.Body.Left.X < (wallColumn + 3) * 16;
                    if (onShelf && landed < 0) landed = tick;
                    stable = onShelf ? stable + 1 : 0;
                    if (tick == leftEdge || tick == underWall || tick == landed)
                    {
                        var lastBlock = Main.tile[wallColumn, high + 2];
                        var firstOpen = Main.tile[wallColumn, high + 3];
                        Console.WriteLine($"FOLLOW short wall tick={tick} bounds={shortWall.Body.Left.X:F2},{shortWall.Body.Top.Y:F2}→{shortWall.Body.Right.X:F2},{shortWall.Body.Bottom.Y:F2} " +
                            $"velocity={shortWall.Body.velocity} wall={wallColumn},{high + 2}:{lastBlock.active()}/{lastBlock.type}/{lastBlock.slope()}/{lastBlock.halfBrick()} " +
                            $"below={firstOpen.active()}/{firstOpen.type}/{firstOpen.slope()}/{firstOpen.halfBrick()} route={RouteStep(shortWall.Actor)}");
                    }
                }
                Console.WriteLine($"FOLLOW side wall with room leftEdge={leftEdge} underWall={underWall} landing={landed} stable={stable} life={shortWall.Body.statLife}/{life} body={shortWall.Body.Bottom}");
                safeShortWall = leftEdge >= 0 && underWall > leftEdge && landed >= underWall && stable >= 12 &&
                    shortWall.Body.statLife == life && !shortWall.Body.dead;
            }

            using (var edited = new Scenario(host, profiles, human, "ledge-side-wall", start, high, terrain))
            {
                // Extending the wall by one tile removes the space to turn
                // beneath it before the body could contact the same shelf.
                AddWall(edited, high + 3);
                Vector2 before = edited.Body.Bottom;
                float speed = edited.Body.velocity.X;
                string route = RouteStep(edited.Actor);
                float maxDrop = 0;
                bool braked = false, coastBraked = false, supportedThroughout = true;
                float minimumClearance = edge - edited.Body.Left.X;
                for (int tick = 0; tick < 32; tick++)
                {
                    float beforeSpeed = edited.Body.velocity.X;
                    step(edited);
                    braked |= edited.Body.controlLeft;
                    float clearance = edge - edited.Body.Left.X;
                    supportedThroughout &= clearance > 0 && OnFloor(edited.Body, high);
                    minimumClearance = Math.Min(minimumClearance, clearance);
                    coastBraked |= !edited.Body.controlLeft && !edited.Body.controlRight && beforeSpeed > .2f &&
                        edited.Body.velocity.X < beforeSpeed - .05f && clearance > 0 && OnFloor(edited.Body, high);
                    maxDrop = Math.Max(maxDrop, edited.Body.Bottom.Y - high * 16);
                }
                Console.WriteLine(FormattableString.Invariant($"FOLLOW side wall dryRelease={release} dryLanding={landing} dryLeft={dryLeft:F2} before={before} speed={speed:F2} route={route} bottom={edited.Body.Bottom} maxDrop={maxDrop:F2} reverseBrake={braked} neutralBrake={coastBraked} minSupport={minimumClearance:F2}"));
                Require(maxDrop < 3 && supportedThroughout && (braked || coastBraked),
                    "a wall added beside the fall cannot steer the companion past its verified narrow landing");

                for (int row = high - 4; row <= high + 3; row++) Main.tile[wallColumn, row].active(false);
                int resumed = 0;
                for (; resumed < updates && !OnFloor(edited.Body, lower); resumed++) step(edited);
                Console.WriteLine($"FOLLOW side wall clearLanding={resumed} body={edited.Body.Bottom}/{edited.Body.velocity} route={RouteStep(edited.Actor)}");
                Require(resumed < updates, "removing the wall restores Follow to the original narrow landing");
            }
            Require(safeShortWall, "the same native body passes underneath the shorter wall and stays safely on the narrow shelf");
        }

        private static float OriginalBrakeRoom(Player body)
        {
            // Mirror the grounded opposite-input branch only to choose an
            // editable fixture phase with a real remaining stopping distance.
            float distance = 0;
            for (float speed = body.velocity.X; speed > 0;)
            {
                speed -= (speed > body.runSlowdown ? body.runSlowdown : 0) + body.runAcceleration;
                distance += Math.Max(0, speed);
            }
            return distance;
        }

        private static void Ledge(object host, object profiles, Player human)
        {
            const int high = 80, low = 86, edge = 65 * 16;
            // The near foot still contacts the last real tile at +4; +16 is
            // already unsupported. Old fall starts clip ground; old walks stop.
            using (var run = new Scenario(host, profiles, human, "vertical-ledge", edge + 1, high, () =>
            {
                Fill(50, 64, high, TileID.Stone);
                Fill(58, 87, low, TileID.Stone);
            }))
            {
                float contact = edge - run.Body.Left.X;
                Require(contact > 4 && contact < 16 && OnFloor(run.Body, high), "B starts grounded strictly inside the 4/16 pixel ledge candidate gap");
                int approach = -1, departed = -1, landed = -1, stable = 0;
                bool jumpBeforeDeparture = false;
                float initialX = run.Body.Center.X;
                for (int tick = 0; tick < 150; tick++)
                {
                    run.StepToward(new Vector2(edge + 104, (human.Left.X < edge ? high : low) * 16), new Vector2(3, 4));
                    if (approach < 0 && run.Body.controlRight && run.Body.Center.X > initialX + 1) approach = tick;
                    if (departed < 0)
                    {
                        jumpBeforeDeparture |= run.Body.velocity.Y < -.5f;
                        if (run.Body.Left.X >= edge && run.Body.Bottom.Y > high * 16 + 4 && run.Body.velocity.Y > 0) departed = tick;
                    }
                    if (departed >= 0 && OnFloor(run.Body, low)) { if (landed < 0) landed = tick; stable++; }
                    else stable = 0;
                }
                Console.WriteLine(FormattableString.Invariant($"FOLLOW B ledge overlap={contact:F1} approach={approach} departure={departed} landing={landed} settledTail={stable} jumpedBeforeDeparture={jumpBeforeDeparture} actor={run.Body.Bottom} status={Get(run.Actor, "Status")}"));
                Require(approach >= 0 && departed > approach && landed > departed && stable >= 20 && !jumpBeforeDeparture,
                    "B: production walks through the narrower supported approach, leaves the solid edge and descends to the real lower landing without a jump");
            }
        }

        private static void UnsafeLedge(object host, object profiles, Player human)
        {
            const int high = 80, deep = 115, edge = 65 * 16;
            using (var run = new Scenario(host, profiles, human, "vertical-pit", edge + 1, high, () =>
            {
                Fill(50, 64, high, TileID.Stone);
                Fill(99, 125, high, TileID.Stone);
                Fill(51, 125, deep, TileID.Stone);
            }, ItemID.Tabi))
            {
                Require(run.Body.dashType > 0 && run.Body.wingsLogic == 0, "B guard begins with a real dash accessory and no ability to fly the 544px gap");
                bool committedDash = false;
                float maxDrop = 0, furthest = run.Body.Left.X;
                for (int tick = 0; tick < 140; tick++)
                {
                    run.StepToward(new Vector2(104 * 16, high * 16), new Vector2(5, 0));
                    committedDash |= run.Body.dashDelay < 0;
                    maxDrop = Math.Max(maxDrop, run.Body.Bottom.Y - high * 16);
                    furthest = Math.Max(furthest, run.Body.Left.X);
                }
                Console.WriteLine(FormattableString.Invariant($"FOLLOW B pit width=544 depth=560 maxDrop={maxDrop:F1} furthestLeft={furthest:F1} edge={edge} dash={committedDash} actor={run.Body.Bottom}"));
                Require(maxDrop < 16 && !committedDash && furthest < edge && !run.Body.dead,
                    "B guard: ordinary travel and native dash may not carry a non-flying companion off a real deep gap");
            }
        }

        private static void StarboardFlight(object host, object profiles, Player human)
        {
            const int ground = 86;
            using (var run = new Scenario(host, profiles, human, "vertical-starboard", 61 * 16, ground,
                () => Fill(44, 119, ground, TileID.Stone), Starboard))
            {
                var body = run.Body;
                float full = body.wingTime;
                Require(body.wingsLogic == 45 && full == 180 && !body.empressBrooch && !body.mount.Active,
                    "C uses real 4954 wings with the native 180-tick finite resource and no substitutes");
                var air = new FlightWindow();
                var hold = new FlightWindow();
                var resume = new FlightWindow();
                var reverse = new FlightWindow();
                var down = new FlightWindow();
                float lowestFuel = full;
                int airborneRefill = 0, descent = -1, landing = -1, reversed = -1, reverseRun = 0;
                long updateTicks = 0, peakUpdateTicks = 0;
                var home = human.Bottom;
                for (int tick = 0; tick < 245; tick++)
                {
                    // 4954 w45's ordinary flying speed is 8 and native hover
                    // can reach 16. The owner passes 8 without asking the AI to
                    // outrun its legal native hover speed or leave the map.
                    Vector2 wanted = tick < 34 ? new Vector2(0, -4.6f) : tick < 91 ? new Vector2(9.5f, 0) :
                        tick < 122 ? Vector2.Zero : tick < 145 ? new Vector2(9.5f, 0) : tick < 180 ? new Vector2(-9.5f, 0) :
                        tick < 216 ? new Vector2(0, Math.Min(4.5f, home.Y - human.Bottom.Y)) : Vector2.Zero;
                    wanted.X = human.velocity.X + MathHelper.Clamp(wanted.X - human.velocity.X, -1.25f, 1.25f);
                    var before = body.Bottom;
                    float fuel = body.wingTime;
                    long cost = run.StepOwner(wanted, measure: true);
                    updateTicks += cost; peakUpdateTicks = Math.Max(peakUpdateTicks, cost);
                    lowestFuel = Math.Min(lowestFuel, body.wingTime);
                    if (fuel < full && body.wingTime > fuel && !OnFloor(body, ground)) airborneRefill++;
                    if (tick >= 56 && tick < 91) air.Read(human, body, before);
                    if (tick >= 102 && tick < 122) hold.Read(human, body, before);
                    if (tick >= 130 && tick < 145) resume.Read(human, body, before);
                    if (tick >= 154 && tick < 180) reverse.Read(human, body, before);
                    if (tick >= 187 && tick < 216) down.Read(human, body, before);
                    if (tick >= 145 && tick < 180 && reversed < 0)
                    {
                        reverseRun = body.velocity.X < -1.25f ? reverseRun + 1 : 0;
                        if (reverseRun == 4) reversed = tick - 3;
                    }
                    if (descent < 0 && tick >= 180 && body.velocity.Y > 1.5f && !body.controlJump) descent = tick;
                    if (landing < 0 && tick >= 180 && OnFloor(body, ground)) landing = tick;
                    if (tick % 15 == 0 || tick == 244)
                        Console.WriteLine(FormattableString.Invariant($"FOLLOW C TRACE t={tick} owner={human.Bottom}/{human.velocity} actor={body.Bottom}/{body.velocity} up={body.controlUp} down={body.controlDown} jump={body.controlJump} wing={body.wingTime:F1} status={Get(run.Actor, "Status")}"));
                }
                Console.WriteLine(FormattableString.Invariant($"FOLLOW C starboard level={air} hold={hold} resume={resume} reverse={reverse} descend={down} reverseLatency={(reversed < 0 ? -1 : reversed-145)} fuelMin={lowestFuel:F1}/{full:F1} airborneRefill={airborneRefill} descent={descent} landing={landing}"));
                Console.WriteLine(FormattableString.Invariant($"FOLLOW C nativeCost updates=245 totalMs={updateTicks*1000d/Stopwatch.Frequency:F3} maxUpdateMs={peakUpdateTicks*1000d/Stopwatch.Frequency:F3}"));
                // Four tiles across and three vertically are a local pursuit
                // envelope. A real tail of movement above 8 needs native hover.
                Require(air.MeanY <= 40 && air.MeanX <= 64 && air.PeakX <= 112 && air.LongestNear >= 20 &&
                    air.Oscillations <= 4 && air.InputTurns <= 3 && air.WrongInputs <= 5 && air.LongestFast >= 14,
                    "C: same-height high-speed pursuit stays in a consecutive near window, uses the native fast mode and avoids repeated wrong direction inputs");
                Require(hold.LongestNeutral >= 12 && hold.LongestNear >= 12 && hold.MeanY <= 35,
                    "C: the moving owner stops in open air and the real wing sustains a centered neutral hover");
                Require(resume.Right - resume.Left >= 48 && reverse.Left - reverse.Right >= 105 && reverse.LongestNear >= 12 &&
                    reversed >= 145 && reversed <= 171 && reverse.InputTurns <= 3,
                    "C: the same original actor resumes and reverses actual travel along the new owner trajectory");
                Require(descent >= 180 && descent < 202 && landing > descent && down.LongestNear >= 12 &&
                    lowestFuel < full - 45 && airborneRefill == 0 && body.wingTime == full,
                    "C: real reserve is spent in air, descent releases ascent in time and only actual landing recovers wings");
            }
        }

        private static void Sequence(object host, object profiles, Player human)
        {
            const int platform = 75, shelf = 81, bottom = 87, edge = 67 * 16;
            using (var run = new Scenario(host, profiles, human, "vertical-handoffs", 49 * 16, platform, () =>
            {
                Fill(42, 50, platform, TileID.Stone);
                Fill(51, 63, platform, TileID.Platforms);
                Fill(53, 66, shelf, TileID.Stone);
                Fill(63, 119, bottom, TileID.Stone);
            }, Starboard))
            {
                int ground = -1, drop = -1, approached = -1, left = -1, prematureRise = -1, flew = -1, returned = -1, pressed = -1;
                int stable = 0, idle = 0, longestIdle = 0, poweredTravel = 0;
                float furthestApproach = 0, maxRetreat = 0;
                for (int tick = 0; tick < 568; tick++)
                {
                    var wanted = tick < 100 ? new Vector2(58 * 16, platform * 16) : tick < 205 ? new Vector2(58 * 16, shelf * 16) :
                        // The observed owner actually clears the shelf before
                        // descending. No synthetic owner position tunnels through stone.
                        tick < 365 ? new Vector2(72 * 16, (human.Left.X < edge ? shelf : bottom) * 16) :
                        tick < 478 ? new Vector2(86 * 16, (bottom - 7) * 16) :
                        new Vector2(92 * 16, bottom * 16);
                    var before = run.Body.Bottom;
                    run.StepToward(wanted, new Vector2(3.7f, 3.7f));
                    bool followingTravel = human.velocity.LengthSquared() > 1 && Vector2.DistanceSquared(human.Bottom, run.Body.Bottom) > 48 * 48;
                    idle = followingTravel && Vector2.DistanceSquared(before, run.Body.Bottom) < .25f * .25f ? idle + 1 : 0;
                    longestIdle = Math.Max(longestIdle, idle);
                    if (ground < 0 && run.Body.controlRight && run.Body.Center.X > 53 * 16 && OnFloor(run.Body, platform)) ground = tick;
                    if (ground >= 0 && pressed < 0 && run.Body.controlDown && before.Y <= platform * 16 + 4) pressed = tick;
                    if (pressed >= 0 && drop < 0 && run.Body.Center.X < 63 * 16 && OnFloor(run.Body, shelf)) drop = tick;
                    if (drop >= 0 && left < 0 && run.Body.Left.X < edge)
                    {
                        if (approached < 0 && run.Body.controlRight && run.Body.Center.X >= edge - 40 && OnFloor(run.Body, shelf)) approached = tick;
                        if (approached >= 0)
                        {
                            furthestApproach = Math.Max(furthestApproach, run.Body.Center.X);
                            maxRetreat = Math.Max(maxRetreat, furthestApproach - run.Body.Center.X);
                        }
                        if (prematureRise < 0 && run.Body.Bottom.Y < shelf * 16 - 4 && run.Body.velocity.Y < -1) prematureRise = tick;
                    }
                    if (drop >= 0 && left < 0 && run.Body.Left.X >= edge && run.Body.Bottom.Y > shelf * 16 + 4 && run.Body.velocity.Y > 0) left = tick;
                    if (left >= 0 && flew < 0)
                    {
                        // The native ground jump can reach the requested height
                        // first. Actual travel at altitude with wing consumption
                        // and held native lift distinguishes powered flight.
                        var body = run.Body;
                        bool wingTravel = body.wingsLogic == 45 && body.wingTime < body.wingTimeMax - 10 &&
                            body.Bottom.Y <= bottom * 16 - 72 && body.controlJump && body.Bottom.X - before.X >= 1;
                        poweredTravel = wingTravel ? poweredTravel + 1 : 0;
                        if (poweredTravel == 6) flew = tick - 5;
                    }
                    if (flew >= 0 && run.Body.Center.X > 85 * 16 && OnFloor(run.Body, bottom))
                    { if (returned < 0) returned = tick; stable++; }
                    else stable = 0;
                    if (tick % 32 == 0 || tick >= 235 && tick <= 273 || tick == approached || tick == left || tick == returned)
                        Console.WriteLine(FormattableString.Invariant($"FOLLOW D TRACE t={tick} owner={human.Bottom}/{human.velocity} actor={run.Body.Bottom}/{run.Body.velocity} left={run.Body.controlLeft} right={run.Body.controlRight} down={run.Body.controlDown} jump={run.Body.controlJump} wing={run.Body.wingTime:F1} status={Get(run.Actor, "Status")} route={RouteStep(run.Actor)}"));
                }
                Console.WriteLine(FormattableString.Invariant($"FOLLOW D sequence walked={ground} down={pressed} platformAndShelf={drop} groundedApproach={approached} leftEdge={left} prematureRise={prematureRise} approachRetreat={maxRetreat:F1} flew={flew} landed={returned} stableTail={stable} movingOwnerIdleMax={longestIdle} actor={run.Body.Bottom}"));
                // Forty pixels is the measured approach band above. At native
                // grounded running speed this fits well inside a second, with
                // room to resolve contact and take the next safe descent step.
                Require(ground >= 0 && pressed > ground && drop > pressed && drop < 205 && approached > drop && left > approached && left < 365 &&
                    left - approached <= 60 && maxRetreat <= run.Body.width && prematureRise < 0 && flew > left && flew < 478 && returned > flew &&
                    stable >= 22 && longestIdle <= 28,
                    "D: one undisturbed Follow actor walks onto platform, passes through onto shelf, leaves its edge, flies and settles on the next level in order");
            }
        }

        private static string RouteStep(object actor)
        {
            object navigator = Navigator(actor);
            long identity = (long)Get(navigator, "ActionId");
            if (identity == 0) return "local";
            object step = Get(navigator, "ActionTransition");
            return FormattableString.Invariant($"{Get(step, "Kind")}:{Get(step, "From")}→{Get(step, "To")} id={identity} outcome={Get(navigator, "ActionOutcome")}");
        }

        private sealed class FlightWindow
        {
            private int frames, near, fast, neutral, lastVertical, lastHorizontal;
            private float errorY, errorX;
            internal int LongestNear, LongestNeutral, LongestFast, Oscillations, InputTurns, WrongInputs;
            internal float Right, Left, PeakX;
            internal float MeanY => errorY / Math.Max(1, frames);
            internal float MeanX => errorX / Math.Max(1, frames);

            internal void Read(Player human, Player body, Vector2 before)
            {
                frames++;
                float deltaY = Math.Abs(human.Bottom.Y - body.Bottom.Y), deltaX = Math.Abs(human.Bottom.X - body.Bottom.X);
                errorY += deltaY; errorX += deltaX; PeakX = Math.Max(PeakX, deltaX);
                near = deltaY <= 48 && deltaX <= 64 ? near + 1 : 0;
                LongestNear = Math.Max(LongestNear, near);
                float dx = body.Bottom.X - before.X;
                Right += Math.Max(0, dx); Left += Math.Max(0, -dx);
                int direction = (body.controlRight ? 1 : 0) - (body.controlLeft ? 1 : 0);
                if (direction != 0 && lastHorizontal != 0 && direction != lastHorizontal) InputTurns++;
                if (direction != 0) lastHorizontal = direction;
                if (Math.Abs(human.velocity.X) > 1 && direction == -Math.Sign(human.velocity.X)) WrongInputs++;
                fast = Math.Abs(dx) > 8.1f ? fast + 1 : 0;
                LongestFast = Math.Max(LongestFast, fast);
                int vertical = Math.Abs(body.velocity.Y) > 1.25f ? Math.Sign(body.velocity.Y) : 0;
                if (vertical != 0 && lastVertical != 0 && vertical != lastVertical) Oscillations++;
                if (vertical != 0) lastVertical = vertical;
                bool nativeHover = body.controlDown && body.controlJump && !body.controlLeft && !body.controlRight && body.wingTime > 0 &&
                    Math.Abs(body.velocity.X) < 1.25f && Math.Abs(body.velocity.Y) < 1.25f && deltaY <= 35 && deltaX <= 40;
                neutral = nativeHover ? neutral + 1 : 0;
                LongestNeutral = Math.Max(LongestNeutral, neutral);
            }

            public override string ToString() => FormattableString.Invariant($"[frames={frames} meanY={MeanY:F1} meanX={MeanX:F1} peakX={PeakX:F1} nearRun={LongestNear} neutralRun={LongestNeutral} fastRun={LongestFast} turnsY={Oscillations} turnsInputX={InputTurns} wrongInputX={WrongInputs} right={Right:F1} left={Left:F1}]");
        }

        internal sealed class Scenario : IDisposable
        {
            private readonly object host, profiles;
            private readonly Player human;
            private readonly Guid id;
            internal readonly Player Body;
            internal readonly object Actor;

            internal Scenario(object host, object profiles, Player human, string name, float x, int floor, Action terrain, int accessory = 0)
            {
                this.host = host; this.profiles = profiles; this.human = human;
                Main.tile = new Tile[Width, Height];
                for (int row = 0; row < Height; row++)
                    for (int column = 0; column < Width; column++) Main.tile[column, row] = new Tile();
                terrain();
                Main.spawnTileX = (int)(x / 16); Main.spawnTileY = floor - 2;
                human.Bottom = new Vector2(x, floor * 16);
                human.velocity = Vector2.Zero;
                Require((bool)Call(host, "Create", name), "create the owned Follow fixture: " + Get(host, "Feedback"));
                id = (Guid)Get(host, "LastCreatedId");
                NativeQuickItemChecks.Until(() => { Call(profiles, "Pump"); return (bool)Call(profiles, "IsSaved", id); });
                Require((bool)Call(host, "Join", id), "join the genuine native Follow actor: " + Get(host, "Feedback"));
                Body = (Player)Call(host, "GetBody", id);
                Actor = Call(host, "Owned", Body);
                try
                {
                    // Exact start placement is the only adjustment to the AI's
                    // body. Subsequent position, velocity and resources are native.
                    Body.Bottom = human.Bottom;
                    if (accessory != 0) Body.armor[3] = new Item(accessory);
                    Require((bool)Call(host, "SetBehavior", id, new CompanionBehavior
                    { Follow = true, AttackEnabled = false, HealthRecoveryEnabled = false }), "the saved companion accepts its ordinary Follow setting");
                    for (int tick = 0; tick < 8; tick++) StepOwner(Vector2.Zero);
                    Require(Body.active && !Body.dead && OnFloor(Body, floor), "the actual joined actor is warm and supported at the scenario start");
                }
                catch { Dispose(); throw; }
            }

            internal long StepToward(Vector2 target, Vector2 maximum, bool measure = false, Player peer = null)
            {
                var delta = target - human.Bottom;
                return StepOwner(Vector2.Clamp(delta, -maximum, maximum), measure, peer);
            }

            internal long StepOwner(Vector2 motion, bool measure = false, Player peer = null)
            {
                human.velocity = motion;
                human.position += motion;
                NativeQuickItemChecks.BeginWorldStep();
                long start = measure ? Stopwatch.GetTimestamp() : 0;
                Body.Update(Body.whoAmI);
                if (peer != null) peer.Update(peer.whoAmI);
                Call(host, "Update", (ulong)Main.GameUpdateCount);
                long elapsed = measure ? Stopwatch.GetTimestamp() - start : 0;
                Require(!(bool)Get(Actor, "Faulted"), "the genuine Player.Update faulted: " + Get(Actor, "Status"));
                return elapsed;
            }

            public void Dispose()
            {
                Call(host, "Recall", id);
                NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return !((IDictionary)Get(host, "actors")).Contains(id); });
            }
        }

        private static bool OnFloor(Player body, int row) => Math.Abs(body.Bottom.Y - row * 16) < 2 && Math.Abs(body.velocity.Y) < .25f;

        private static void Fill(int start, int end, int y, ushort type)
        {
            for (int x = start; x <= end; x++)
            {
                Tile tile = Main.tile[x, y];
                tile.active(true); tile.type = type;
                if (type == TileID.Platforms) tile.frameX = tile.frameY = 0;
            }
        }
    }
}
