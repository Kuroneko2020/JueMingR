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
        private const BindingFlags ReflectionMembers = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        private static object Make(Type type, params object[] arguments) =>
            Activator.CreateInstance(type, ReflectionMembers & ~BindingFlags.Static, null, arguments, null);

        private static Type NavigationType(Assembly assembly, string name) =>
            assembly.GetType("JueMingR.TerrariaHost.Companions." + name, true);

        private static void RouteGraphContracts(object host, object profiles, Player human)
        {
            Main.tile = new Tile[Width, Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++) Main.tile[x, y] = new Tile();
            PlaceFixtureStrip(48, 79, 80, TileID.Stone);
            PlaceFixtureStrip(81, 90, 80, TileID.Stone);
            PlaceFixtureStrip(54, 67, 75, TileID.Platforms);
            PlaceFixtureTile(71, 79, TileID.Stone);
            Main.tile[71, 79].halfBrick(true);
            const int blocker = 80;
            for (int y = 70; y <= 93; y++) PlaceFixtureTile(blocker, y, TileID.Stone);
            Main.tile[60, 71] = null;

            Assembly assembly = host.GetType().Assembly;
            Type budgetType = NavigationType(assembly, "NavigationBudget");
            Type transitionType = NavigationType(assembly, "NavigationTransition");
            Type actionType = NavigationType(assembly, "NavigationAction");
            object world = Make(NavigationType(assembly, "NavigationWorld"));
            object NewBudget(int tiles, int expansions) => Make(budgetType, tiles, expansions, expansions);

            object limited = NewBudget(3, 0);
            object partial = Call(world, "Discover", new Point(55, 75), limited);
            Require(!(bool)Get(partial, "Complete") && (int)Get(limited, "Tiles") == 3,
                "initial unseeded terrain discovery is incremental and obeys its actual tile allowance");
            object inspected = Call(world, "Discover", new Point(55, 75), NewBudget(128, 0));
            object adjacent = Call(world, "Discover", new Point(56, 75), NewBudget(128, 0));
            Require((bool)Get(inspected, "Complete") && (bool)Get(adjacent, "Complete") &&
                ((IList)Get(inspected, "Surfaces")).Count > 0 && ((IList)Get(adjacent, "Surfaces")).Count > 0 &&
                ((IList)Get(inspected, "AirSpans")).Count > 0,
                "platform support and open spans exist across a chunk join without a goal or a previously successful route");
            var unloaded = new[] { (object)new Point(60, 71), NewBudget(128, 0), null };
            Require((bool)Call(world, "Read", unloaded) && !(bool)Get(unloaded[2], "Known") && Main.tile[60, 71] == null,
                "a missing native tile is observed as unknown without allocating a tile or granting passage");
            var half = new[] { (object)new Point(71, 79), NewBudget(128, 0), null };
            Require((bool)Call(world, "Read", half) && Get(half[2], "Kind").ToString() == "Half",
                "the shared world stores the actual half-brick rather than flattening it into a full tile");
            Main.tile[71, 79].halfBrick(false);
            var verify = new[] { half[0], NewBudget(1, 0), (object)false };
            Require((bool)Call(world, "TryVerify", verify) && (bool)verify[2] &&
                (bool)Call(world, "Read", half) && Get(half[2], "Kind").ToString() == "Solid",
                "a local shape edit refreshes the cell that was actually read");

            var route = Make(NavigationType(assembly, "NavigationRoute"), world);
            object caps = Make(NavigationType(assembly, "NavigationCapabilities"), 20, 42, 1, 80f, 112f,
                0, 0, 0f, 0f, Enum.ToObject(NavigationType(assembly, "NavigationModes"), 0), true, false);
            Vector2 start = new Vector2(RoomX(52), 80 * 16);
            Vector2 target = new Vector2(RoomX(72), 80 * 16);
            Vector2 initialTarget = new Vector2(RoomX(83), 80 * 16);
            var tiny = NewBudget(4, 1);
            Call(route, "Advance", start, initialTarget, caps, tiny);
            Require(Get(route, "Status").ToString() == "PendingBudget", "an incomplete tiny slice remains pending");
            for (int tick = 0; tick < 90; tick++)
                Call(route, "Advance", start, initialTarget, caps, NewBudget(256, 24));
            int generation = (int)Get(route, "Generation");
            object offer = null;
            for (int tick = 0; tick < 90 && offer == null; tick++)
            {
                Call(route, "Advance", start, target, caps, NewBudget(256, 24));
                var selected = new[] { (object)start, Activator.CreateInstance(transitionType) };
                if ((bool)Call(route, "TrySelect", selected)) offer = selected[1];
            }
            Require(offer != null && (int)Get(route, "Generation") > generation &&
                ((Vector2)Get(offer, "To")).X > start.X,
                "the same retained terrain answers a new goal through real intermediate edges after an earlier query");
            Call(route, "MarkIssued", offer);
            Vector2 outside = new Vector2(RoomX(48), 80 * 16);
            object reversed = null;
            for (int tick = 0; tick < 90 && reversed == null; tick++)
            {
                Call(route, "Advance", start, outside, caps, NewBudget(256, 24));
                var selected = new[] { (object)start, Activator.CreateInstance(transitionType) };
                if ((bool)Call(route, "TrySelect", selected) && !(bool)Call(offer, "SameEdge", selected[1])) reversed = selected[1];
            }
            Require(Get(offer, "Kind").ToString() == "Walk" && reversed != null && ((Vector2)Get(reversed, "To")).X < start.X &&
                !(bool)Call(route, "IsRejected", offer),
                "a reversible issued walk yields to a verified reversed prefix without recording a geometric failure");
            object MakeDrop(int entry) => Make(transitionType, Enum.Parse(actionType, "PlatformDrop"),
                new Vector2(RoomX(entry), 75 * 16), new Vector2(RoomX(entry), 80 * 16),
                new Point(entry, 75), new Point(entry, 80), 0, true,
                Enum.ToObject(NavigationType(assembly, "NavigationTraversalDetail"), 0), Vector2.Zero);
            object drop = MakeDrop(55), other = MakeDrop(64);
            VerifyCommittedDrop(assembly, world, caps, transitionType, drop);
            Call(route, "Reject", drop, Get(drop, "From"));
            Require((bool)Call(route, "IsRejected", drop) && !(bool)Call(route, "IsRejected", other),
                "a rejected platform action does not block descent from a different entry");
            VerifyLateMovementReceipt(assembly, offer, start);
            ReopenKnownPassage(assembly, world, caps, transitionType);
            EnumerateJumpLandings(assembly, caps, transitionType);
            CumulativeDropLife(assembly, transitionType, actionType);
            Console.WriteLine($"NAV GRAPH independentChunks=True generation={generation}->{Get(route, "Generation")} status={Get(route, "Status")} next={Get(offer, "Kind")}");
        }

        private static void EnumerateJumpLandings(Assembly assembly, object caps, Type transitionType)
        {
            const int startColumn = 113, exitColumn = 117, row = 80;
            PlaceFixtureStrip(startColumn - 2, startColumn + 2, row, TileID.Stone);
            PlaceFixtureTile(exitColumn, row - 5, TileID.Platforms);
            PlaceFixtureTile(exitColumn, row - 2, TileID.Platforms);
            object world = Make(NavigationType(assembly, "NavigationWorld"));
            Type type = NavigationType(assembly, "NavigationTraversal");
            Type nodeType = NavigationType(assembly, "NavigationNode");
            object traversal = Make(type, world);
            Type budget = NavigationType(assembly, "NavigationBudget");
            Call(traversal, "Begin", caps, Make(budget, 1024, 16, 16), null);
            var start = new Vector2(RoomX(startColumn), row * 16);
            object entry = Call(traversal, "Anchor", start, true, false);
            int option = (int)type.GetField("BasicOptions", ReflectionMembers).GetValue(null) +
                Array.IndexOf((int[])type.GetField("JumpOffsets", ReflectionMembers).GetValue(null), exitColumn - startColumn);
            int firstBranch = (int)type.GetField("FirstBranch", ReflectionMembers).GetValue(null);
            var offered = new[] { entry, (object)option, firstBranch, Activator.CreateInstance(nodeType),
                Activator.CreateInstance(transitionType), 0f };
            Require((bool)Call(traversal, "OfferNext", offered), "one jump direction yields the highest clear landing");
            object high = offered[4];
            Require((bool)Call(traversal, "OfferNext", offered), "the same jump direction resumes at a lower landed pose");
            object low = offered[4];
            var highExit = (Point)Get(high, "ExitTile");
            var lowExit = (Point)Get(low, "ExitTile");
            object route = Make(NavigationType(assembly, "NavigationRoute"), world);
            var goal = (Vector2)Get(low, "To");
            // Bind the same body capabilities the ordinary rejection key records.
            Call(route, "Advance", start, goal, caps, Make(budget, 1, 0, 0));
            Call(route, "Defer", high, false);
            Require(!(bool)Call(route, "HasRejectedLaunchNearby", start), "temporary unavailability is not a geometric launch failure");
            var highFeet = (Vector2)Get(high, "To");
            var contact = new Player { width = 20, height = 42, gravDir = 1, Bottom = highFeet };
            var obstruction = new Point(exitColumn, highExit.Y - 2);
            var priorCell = new[] { (object)obstruction, Make(budget, 512, 0, 0), null };
            Require((bool)Call(world, "Read", priorCell) && Get(priorCell[2], "Kind").ToString() == "Empty",
                "the original landing is backed by a known empty headroom cell");
            Require(!Collision.SolidCollision(contact.position, contact.width, contact.height),
                "the first graph landing initially fits the original body");
            PlaceFixtureTile(obstruction.X, obstruction.Y, TileID.Stone);
            Main.tile[obstruction.X, obstruction.Y].halfBrick(true);
            Require(Collision.SolidCollision(contact.position, contact.width, contact.height),
                "the original landing body is clipped by the new half-brick above it");
            var verifyObstruction = new[] { (object)obstruction, Make(budget, 16, 0, 0), (object)false };
            Require((bool)Call(world, "TryVerify", verifyObstruction) && (bool)verifyObstruction[2],
                "the consulted landing envelope observes its actual shape edit");
            Call(route, "Reject", high, highFeet);
            Require(highExit == new Point(exitColumn, row - 5) && lowExit == new Point(exitColumn, row - 2) &&
                (bool)Call(route, "IsRejected", high) && !(bool)Call(route, "IsRejected", low),
                "rejection of the higher jump landing leaves the lower genuine platform available from the same entry");
            var selected = new[] { (object)start, Activator.CreateInstance(transitionType) };
            int published = -1;
            string last = "none";
            // Rejections are temporary. Exercise the ordinary search within its
            // active rejection window, using a bounded share of the normal slice.
            for (int slice = 0; slice < 40 && published < 0; slice++)
            {
                Call(route, "Advance", start, goal, caps, Make(budget, 256, 12, 8));
                Require((bool)Call(route, "IsRejected", high), $"the same capabilities retain the specific high outlet rejection (slice={slice})");
                if (!(bool)Call(route, "TrySelect", selected)) continue;
                Require(!(bool)Call(high, "SameEdge", selected[1]), "the ordinary publisher respects the already-rejected high outlet");
                last = $"{Get(selected[1], "Kind")}:{Get(selected[1], "From")}→{Get(selected[1], "To")}";
                // Live attachment may refine the entry feet within the same support tile.
                if (Get(selected[1], "Kind").ToString() == "Jump" && (Point)Get(selected[1], "ExitTile") == lowExit &&
                    (Point)Get(selected[1], "EntryTile") == (Point)Get(low, "EntryTile")) published = slice;
            }
            Require(published >= 0, $"the same ordinary route publishes the lower landing with the high outlet rejected (last={last}, status={Get(route, "Status")})");
            WorldGen.KillTile(obstruction.X, obstruction.Y, fail: false, effectOnly: false, noItem: true);
            verifyObstruction[1] = Make(budget, 16, 0, 0);
            Require((bool)Call(world, "TryVerify", verifyObstruction) && (bool)verifyObstruction[2] &&
                !Collision.SolidCollision(contact.position, contact.width, contact.height) && !(bool)Call(route, "IsRejected", high),
                "removing the native obstruction restores the original landing on the same route before its retry expires");
            Console.WriteLine($"NAV GRAPH jump branch from={start} high={highExit} lower={lowExit} publishedSlice={published} obstruction={obstruction} reopened=True");
        }

        private static void VerifyCommittedDrop(Assembly assembly, object world, object caps, Type transitionType, object drop)
        {
            object route = Make(NavigationType(assembly, "NavigationRoute"), world);
            Type budget = NavigationType(assembly, "NavigationBudget");
            var from = (Vector2)Get(drop, "From");
            var landing = (Vector2)Get(drop, "To");
            var selected = new[] { (object)from, Activator.CreateInstance(transitionType) };
            object offered = null;
            for (int step = 0; step < 90 && offered == null; step++)
            {
                Call(route, "Advance", from, landing, caps, Make(budget, 256, 24, 24));
                if ((bool)Call(route, "TrySelect", selected) && Get(selected[1], "Kind").ToString() == "PlatformDrop") offered = selected[1];
            }
            Require(offered != null, "known aligned platform has a real viable drop offer");
            Call(route, "MarkIssued", offered);
            // The motion consumer owns the choice of when to publish this phase.
            Call(route, "SetCommitted", true);
            Vector2 airborne = from + new Vector2(0, 18);
            Vector2 newGoal = from + new Vector2(48, 0);
            for (int step = 0; step < 24; step++)
            {
                Call(route, "Advance", airborne, newGoal, caps, Make(budget, 256, 24, 24));
                selected[0] = airborne;
                Require((bool)Call(route, "TrySelect", selected) && (bool)Call(offered, "SameEdge", selected[1]),
                    "after true platform release the graph preserves the same committed segment until the body's receipt");
            }
            Call(route, "Complete", offered, landing);
        }

        private static void CumulativeDropLife(Assembly assembly, Type transitionType, Type actionType)
        {
            const int column = 30, upper = 36, middle = 74, lower = 112, life = 185;
            double surface = Main.worldSurface;
            Main.worldSurface = 30;
            try
            {
                PlaceFixtureStrip(column - 1, column + 1, upper, TileID.Platforms);
                PlaceFixtureStrip(column - 1, column + 1, middle, TileID.Platforms);
                PlaceFixtureStrip(column - 1, column + 1, lower, TileID.Stone);
                var body = new Player { width = 20, height = 42, maxFallSpeed = 10f, gravity = .4f, gravDir = 1f, statLife = life };
                body.grappling[0] = -1;
                body.Bottom = new Vector2(RoomX(column), upper * 16);
                object observed = Activator.CreateInstance(NavigationType(assembly, "CompanionMobilitySnapshot"));
                Type budget = NavigationType(assembly, "NavigationBudget");
                Type refinement = NavigationType(assembly, "CompanionActionRefinement");
                var noDetail = Enum.ToObject(NavigationType(assembly, "NavigationTraversalDetail"), 0);
                object Drop(int source, int destination) => Make(transitionType, Enum.Parse(actionType, "PlatformDrop"),
                    new Vector2(RoomX(column), source * 16), new Vector2(RoomX(column), destination * 16),
                    new Point(column, source), new Point(column, destination), 0, true, noDetail, Vector2.Zero);
                object descent = Make(refinement);
                object first = Drop(upper, middle), second = Drop(middle, lower);
                Call(descent, "Begin", body, first, true);
                string initial = Call(descent, "Advance", body, observed, Make(budget, 512, 96, 128)).ToString();
                object witness = Get(descent, "Witness");
                int remainder = (int)Get(witness, "RemainingLife");
                Require(initial == "Ready" && remainder > 0 && remainder < life && body.statLife == life,
                    "the first local projection is survivable and leaves the live body untouched");

                // Re-sample from the next support with the post-landing life.
                // NativeMovingFall independently observes original Hurt in the real sequence.
                var isolated = Make(refinement);
                body.Bottom = new Vector2(RoomX(column), middle * 16);
                Call(isolated, "Begin", body, second, true);
                string alone = Call(isolated, "Advance", body, observed, Make(budget, 512, 96, 128)).ToString();
                Require(body.statLife == life, "the second projection at full life also leaves the live body untouched");
                body.statLife = remainder;
                Call(isolated, "Begin", body, second, true);
                string together = Call(isolated, "Advance", body, observed, Make(budget, 512, 96, 128)).ToString();
                Require(alone == "Ready" && together == "Unsafe" && body.statLife == remainder,
                    "the next native-shape drop is refused at the new inlet's lower life");
                Console.WriteLine($"NAV DROP LIFE initial={life} afterFirst={remainder} isolated={alone} chain={together}");
            }
            finally { Main.worldSurface = surface; }
        }

        private static void ReopenKnownPassage(Assembly assembly, object world, object caps, Type transitionType)
        {
            object route = Make(NavigationType(assembly, "NavigationRoute"), world);
            Type budget = NavigationType(assembly, "NavigationBudget");
            Vector2 feet = new Vector2(RoomX(77), 80 * 16);
            Vector2 target = new Vector2(RoomX(84), 80 * 16);
            bool waited = false;
            for (int slice = 0; slice < 70; slice++)
            {
                NativeQuickItemChecks.BeginWorldStep();
                Call(route, "Advance", feet, target, caps, Make(budget, 96, 4, 6));
                waited |= Get(route, "Status").ToString() == "PendingBudget";
                var offered = new[] { (object)feet, Activator.CreateInstance(transitionType) };
                Require(!(bool)Call(route, "TrySelect", offered) || ((Vector2)Get(offered[1], "To")).X < 81 * 16,
                    "the closed real wall cannot publish a crossing under pressure");
            }
            Require(waited, "the closed route consumes the small fixed slice and reports pending work");
            var cachedChunks = (IDictionary)Get(world, "chunks");
            Point openingChunk = new Point(80, 72);
            int beforeVersion = cachedChunks.Contains(openingChunk) ? (int)Get(cachedChunks[openingChunk], "Version") : -1;
            for (int row = 76; row < 80; row++)
            {
                WorldGen.KillTile(80, row, fail: false, effectOnly: false, noItem: true);
                Require(!Main.tile[80, row].nactive(), "the original tile operation opens the actual corridor");
            }
            for (int column = 77; column <= 84; column++)
            {
                var pose = new Vector2(RoomX(column) - 10, 80 * 16 - 42);
                Require(!Collision.SolidCollision(pose, 20, 42) &&
                    Collision.TileCollision(pose, new Vector2(0, 2), 20, 42).Y < .01f,
                    "the original collision admits an ordinary body and real support all the way across the new opening");
            }
            int recovered = -1;
            object allowance = null;
            for (int slice = 0; slice < 240; slice++)
            {
                NativeQuickItemChecks.BeginWorldStep();
                allowance = Make(budget, 96, 4, 6);
                Call(route, "Advance", feet, target, caps, allowance);
                var offered = new[] { (object)feet, Activator.CreateInstance(transitionType) };
                if (!(bool)Call(route, "TrySelect", offered)) continue;
                Call(route, "MarkIssued", offered[1]);
                feet = (Vector2)Get(offered[1], "To");
                Call(route, "Complete", offered[1], feet);
                if (feet.X < 82 * 16) continue;
                recovered = slice;
                break;
            }
            int afterVersion = cachedChunks.Contains(openingChunk) ? (int)Get(cachedChunks[openingChunk], "Version") : -1;
            object pending = GetOptional(route, "query");
            var cell = new[] { (object)new Point(80, 78), Make(budget, 96, 4, 6), null };
            bool read = (bool)Call(world, "Read", cell);
            Console.WriteLine($"NAV GRAPH budget opening firstAcrossSlice={recovered} feet={feet} status={Get(route, "Status")} " +
                $"generation={Get(route, "Generation")} worldRevision={Get(world, "Revision")} chunkVersion={beforeVersion}->{afterVersion} " +
                $"openCell={(read ? Get(cell[2], "Kind") : "unread")} queryOption={(pending == null ? null : GetOptional(pending, "option"))} " +
                $"expanded={(pending == null ? null : GetOptional(pending, "expanded"))} " +
                $"remainingTiles={Get(allowance, "TilesRemaining")} remainingExpansions={Get(allowance, "ExpansionsRemaining")}");
            Require(recovered >= 0, "the same route and cache discover and cross a genuine opening without resetting the cursor or raising its slice");
        }

        private static void SlicedLongHookProof(object context, object host, object profiles, Player human)
        {
            var entry = new Vector2(RoomX(57), 90 * 16);
            var anchor = new Point(69, 78);
            using (var run = new NativeFollowWorld(context, host, profiles, human, "sliced-hook-proof", entry, entry, () =>
            {
                PlaceFixtureStrip(48, 85, 90, TileID.Stone);
                PlaceFixtureTile(anchor.X, anchor.Y, TileID.Stone);
            }))
            {
                var gear = new Item(ItemID.GrapplingHook);
                bool prior = Main.projHook[gear.shoot];
                try
                {
                    Main.projHook[gear.shoot] = ContentSamples.ProjectilesByType[gear.shoot].aiStyle == 7;
                    run.Body.miscEquips[4] = gear;
                    for (int i = 0; i < 6; i++) run.Step();
                    object observed = Get(Get(run.Actor, "Mobility"), "Observed");
                    float reach = (float)Get(observed, "HookRange");
                    Vector2 anchorCenter = anchor.ToWorldCoordinates();
                    float distance = Vector2.Distance(run.Body.Center, anchorCenter);
                    Require((int)Get(observed, "HookType") == gear.shoot && distance > 200 && distance <= reach,
                        "the original equipped hook can reach the actual distant solid tile");
                    Assembly assembly = host.GetType().Assembly;
                    object transition = Make(NavigationType(assembly, "NavigationTransition"),
                        Enum.Parse(NavigationType(assembly, "NavigationAction"), "Hook"), run.Body.Bottom, anchorCenter,
                        default(Point), anchor, 0, false, Enum.ToObject(NavigationType(assembly, "NavigationTraversalDetail"), 0), Vector2.Zero);
                    object refinement = Make(NavigationType(assembly, "CompanionActionRefinement"));
                    Call(refinement, "Begin", run.Body, transition, true);
                    string result = "Pending";
                    int slices = 0, predicted = 0, spendingSlices = 0;
                    for (; slices < 90 && result == "Pending"; slices++)
                    {
                        object allowance = Make(NavigationType(assembly, "NavigationBudget"), 80, 4, 2);
                        result = Call(refinement, "Advance", run.Body, observed, allowance).ToString();
                        int spent = (int)Get(allowance, "Predictions");
                        if (spent > 0) spendingSlices++;
                        predicted += spent;
                        Require(spent <= 2 && (int)Get(allowance, "Tiles") <= 80, "a real long hook query only spends its actual small slice");
                    }
                    Console.WriteLine($"NAV LONG HOOK distance={distance:F1} nativeRange={reach:F1} slices={slices} advancingSlices={spendingSlices} predictions={predicted} result={result}");
                    Require(slices >= 4 && spendingSlices >= 4 && predicted >= 25 && result == "Ready" &&
                        run.Body.Bottom == entry && run.NativeRelocations == 0,
                        "a long native hook ray accumulates proof across fixed small slices without moving the live body");
                }
                finally { Main.projHook[gear.shoot] = prior; }
            }
        }

        private static void NativeMovedActionRetires(object context, object host, object profiles, Player human)
        {
            const int floor = 80;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "original-relocation",
                new Vector2(RoomX(55), floor * 16), new Vector2(RoomX(69), floor * 16),
                () => PlaceFixtureStrip(48, 106, floor, TileID.Stone)))
            {
                Player body = run.Body;
                object nav = Navigator(run.Actor);
                object native = Get(run.Actor, "Mobility");
                for (int tick = 0; tick < 55; tick++) run.Step(1);
                long priorId = (long)Get(nav, "ActionId");
                int priorRevision = (int)Get(native, "PositionRevision");
                Require(priorId > 0 && body.controlRight && body.Bottom.X < human.Bottom.X,
                    "the ordinary body has a real delivered movement action before separate relocation");
                Vector2 destination = new Vector2(RoomX(91) - body.width * .5f, floor * 16 - body.height);
                body.Teleport(destination, 0);
                Require(body.position == destination && (int)Get(native, "PositionRevision") == priorRevision + 1,
                    "the separate original non-wormhole teleport actually reaches its new native position");
                run.Step(-1);
                object delivery = GetOptional(nav, "LastMovementDelivery");
                long firstDelivered = delivery == null ? -1 : (long)Get(delivery, "ActionId");
                long after = (long)Get(nav, "ActionId");
                int resumed = -1;
                for (int tick = 0; tick < 35; tick++)
                {
                    float priorX = body.Bottom.X;
                    run.Step(-1);
                    if (resumed < 0 && body.controlLeft && body.Bottom.X < priorX - .5f &&
                        (long)Get(nav, "ActionId") != priorId) resumed = tick;
                }
                Console.WriteLine($"NAV NATIVE RELOCATION oldAction={priorId} firstDelivered={firstDelivered} after={after} " +
                    $"revision={priorRevision}->{Get(native, "PositionRevision")} resumedLeft={resumed} feet={body.Bottom}");
                Require(after != priorId && (delivery == null || firstDelivered != priorId) && resumed >= 0 &&
                    ReferenceEquals(body, Get(run.Actor, "Body")),
                    "the first effective body step after native relocation retires old movement and continues from today's real position");
            }
        }

        private static void NativePendingRunway(object context, object host, object profiles, Player human)
        {
            const int startColumn = 52, exitColumn = 60, upper = 84, lower = 93;
            var start = new Vector2(RoomX(startColumn), upper * 16);
            using (var run = new NativeFollowWorld(context, host, profiles, human, "pending-runway", start, start, () =>
                {
                    PlaceFixtureStrip(43, exitColumn - 2, upper, TileID.Stone);
                    PlaceFixtureStrip(exitColumn - 1, 100, upper, TileID.Platforms);
                    PlaceFixtureStrip(43, 100, lower, TileID.Stone);
                }))
            {
                Player body = run.Body;
                body.armor[3] = new Item(ItemID.EoCShield);
                var nav = Navigator(run.Actor);
                Assembly assembly = host.GetType().Assembly;
                Type transitionType = NavigationType(assembly, "NavigationTransition");
                Type budgetType = NavigationType(assembly, "NavigationBudget");
                var mobility = Get(run.Actor, "Mobility");
                for (int tick = 0; tick < 4 && body.dashType == 0; tick++) run.Step();
                var observed = Get(mobility, "Observed");
                Require(body.dashType != 0 && (bool)Get(observed, "DashReady") && OnFloor(body, upper),
                    "the same body has observed its real shield and stands on the original runway");
                var readCaps = NavigationType(assembly, "CompanionMotionProfile").GetMethod("Read", ReflectionMembers);
                object caps = readCaps.Invoke(null, new[] { body, observed, true });
                object world = Get(host, "navigationWorld");
                var traversal = Make(NavigationType(assembly, "NavigationTraversal"), world);
                Call(traversal, "Begin", caps, Make(budgetType, 512, 96, 96), null);
                var from = Call(traversal, "Anchor", body.Bottom, true, true);
                var runway = new[] { from, (object)32, Activator.CreateInstance(from.GetType()), Activator.CreateInstance(transitionType), 0f };
                Require((bool)Call(traversal, "IsApplicable", from, 32) && (bool)Call(traversal, "Offer", runway) &&
                    Get(runway[3], "Kind").ToString() == "Walk", "the live graph keeps the original support runway available as a walking edge");
                var outlet = new[] { runway[2], (object)4, Activator.CreateInstance(from.GetType()), Activator.CreateInstance(transitionType), 0f };
                Require((bool)Call(traversal, "Offer", outlet) && Get(outlet[3], "Kind").ToString() == "PlatformDrop",
                    $"the whole native body can pass through the runway's platform to a real lower contact from {Get(runway[3], "To")}");
                var route = Get(nav, "route");
                Call(route, "Advance", body.Bottom, human.Bottom, caps, Make(budgetType, 256, 24, 24));
                Call(route, "ClearCorridor");
                // Supply the graph's own two edges to the consumer at the
                // publication boundary, before the remaining verification slice.
                Call(route, "AddCorridorEdge", runway[3]);
                Call(route, "AddCorridorEdge", outlet[3]);
                var slice = Make(budgetType, 2, 1, 1);
                Call(route, "Prepare", body.Bottom, slice);
                var approach = new[] { (object)body.Bottom, Vector2.Zero };
                var offer = new[] { (object)body.Bottom, Activator.CreateInstance(transitionType) };
                Require((int)Get(slice, "Expansions") == 1 && !(bool)Call(route, "TrySelect", offer) &&
                    (bool)Call(route, "TryTraversalApproach", approach), "ordinary nonzero work leaves a real, still-unissued runway prefix");
                human.Bottom = new Vector2(RoomX(exitColumn + 5), lower * 16);
                float before = body.Bottom.X;
                run.Step(1);
                Console.WriteLine($"NAV PENDING RUNWAY from={Get(runway[3], "From")} to={Get(runway[3], "To")} " +
                    $"approach={approach[1]} nativeRight={body.controlRight} dx={body.Bottom.X - before:F2} nativeDash={body.dashDelay} {RouteTrace(nav)}");
                Require((Vector2)approach[1] == (Vector2)Get(runway[3], "To") && body.controlRight && !body.controlLeft &&
                    body.Bottom.X > before + .05f && body.dashDelay >= 0 && OnFloor(body, upper),
                    "a pending runway prefix keeps native walking toward its exit until the actual outlet is ready");
                int down = -1, land = -1;
                float lastGroundX = body.Bottom.X;
                bool borrowedDash = false;
                int directPulses = 0;
                for (int tick = 1; tick < 135 && land < 0; tick++)
                {
                    if (OnFloor(body, upper)) lastGroundX = body.Bottom.X;
                    run.Step(1);
                    if (body.controlDash)
                    {
                        // A later independently prepared direct dash is legal.
                        // The graph Walk itself cannot supply an impulse.
                        bool direct = (bool)Get(Get(nav, "action"), "Direct") && Get(nav, "ActionKind").ToString() == "Dash";
                        if (direct && body.dashDelay < 0) directPulses++;
                        else borrowedDash = true;
                    }
                    if (down < 0 && body.controlDown) down = tick;
                    if (down >= 0 && OnFloor(body, lower)) land = tick;
                }
                Console.WriteLine($"NAV RUNWAY OUTLET down={down} land={land} upperTravel={lastGroundX - before:F2} " +
                    $"directPulses={directPulses} borrowedDash={borrowedDash} body={body.Bottom}/{body.velocity} {RouteTrace(nav)}");
                Require(down >= 0 && land > down && lastGroundX > before + 32f && !borrowedDash && run.NativeRelocations == 0,
                    "a real walking runway leads to ordinary original down and lower contact without any borrowed dash");
            }
        }

        private static void VerifyLateMovementReceipt(Assembly assembly, object edge, Vector2 feet)
        {
            var body = new Player { active = true, width = 20, height = 42, gravDir = 1 };
            body.Bottom = feet;
            object movement = Make(NavigationType(assembly, "CompanionMotionAction"));
            object native = Make(NavigationType(assembly, "CompanionMobilityState"));
            Call(native, "ObserveBody", body, false);
            object MakeReceipt(long identity)
            {
                Type frameType = NavigationType(assembly, "CompanionMobilityFrame");
                object frame = Activator.CreateInstance(frameType);
                frameType.GetField("ActionId", ReflectionMembers).SetValue(frame, identity);
                object deferral = Enum.Parse(NavigationType(assembly, "CompanionMovementDeferral"), "HandBusy");
                return Make(NavigationType(assembly, "CompanionMovementDelivery"), body, native, frame, deferral);
            }
            Call(movement, "Begin", 7L, edge, false, feet);
            object oldReceipt = MakeReceipt(7L);
            Call(movement, "Begin", 8L, edge, false, feet);
            Call(movement, "Record", oldReceipt);
            string late = Call(movement, "Observe", body, native, true, feet, false).ToString();
            Call(movement, "Record", MakeReceipt(8L));
            string current = Call(movement, "Observe", body, native, true, feet, false).ToString();
            Require(late == "Running" && current == "Waiting" && (long)Get(movement, "Id") == 8L,
                "an obsolete input receipt cannot defer a newer action but its own real delivery can");
        }
    }
}
