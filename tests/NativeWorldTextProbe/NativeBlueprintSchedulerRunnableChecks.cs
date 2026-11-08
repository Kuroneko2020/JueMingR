using System;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using JueMingR.Features.Blueprint;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        // This oracle owns only the fixture's three real destinations. In
        // particular it does not inherit BusinessScope's independent-floor
        // simplification, nor read a production cursor/coverage/Focus result.
        private static bool SchedulerBlockRunnable(BusinessGoal goal, BusinessSource source)
        {
            var p = Main.LocalPlayer; var tile = Main.tile[goal.X, goal.Y];
            if (goal.Complete || tile.active() || tile.liquid != 0 || p.noBuilding ||
                !ReferenceEquals(p.inventory[source.Slot], source.Item) || source.Item.IsAir || source.Item.stack <= 0 ||
                !p.IsInTileInteractionRange(goal.X, goal.Y, TileReachCheckSettings.Simple, source.Item.tileBoost + p.blockRange)) return false;
            var body = new Rectangle(goal.X * 16, goal.Y * 16, 16, 16);
            if (Main.player.Any(player => player != null && player.active && !player.dead && body.Intersects(player.Hitbox))) return false;
            return new[] { new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1) }
                .Any(offset => Main.tile[goal.X + offset.X, goal.Y + offset.Y].active());
        }

        private static void SchedulerDependencySetup(BusinessScope scope, bool horizontal)
        {
            var p = Reset(scope.Context, scope.Host, scope.Tools, scope.Input);
            p.position.X = 42 * 16;
            var wood = BusinessStock(p, 12, ItemID.Wood, 4);
            var stone = BusinessStock(p, 13, ItemID.StoneBlock, 3);
            scope.Sources = new[] { wood, stone };
            scope.RunnableOracle = goal => SchedulerBlockRunnable(goal, scope.Sources.Single(source => source.Type == goal.Type));
            // Reset laid a complete floor at y43. Remove the hidden support
            // as well as side tiles/walls before installing the sole root.
            for (int x = 44; x <= 48; x++) for (int y = 38; y <= 44; y++) Main.tile[x, y].ClearEverything();
            int rootX = 45, rootY = 42;
            NativeToolsChecks.Tile(rootX, rootY + 1, TileID.Stone);
            scope.Goals = horizontal ? new[] {
                BusinessTileGoal(rootX, rootY, wood), BusinessTileGoal(rootX + 1, rootY, stone), BusinessTileGoal(rootX + 2, rootY, wood)
            } : new[] {
                BusinessTileGoal(rootX, rootY, wood), BusinessTileGoal(rootX, rootY - 1, stone), BusinessTileGoal(rootX, rootY - 2, wood)
            };
            var cells = scope.Goals.Select(goal => new CaptureCell {
                X = goal.X - rootX, Y = goal.Y - (horizontal ? rootY : rootY - 2),
                Layers = CaptureLayer.Tile, TileType = (ushort)goal.TileType
            }).ToArray();
            Place(scope.Context, scope.Host, scope.Input, Asset(scope.Host, cells), rootX, horizontal ? rootY : rootY - 2);
            Require(SchedulerBlockRunnable(scope.Goals[0], wood) && !SchedulerBlockRunnable(scope.Goals[1], stone) &&
                !SchedulerBlockRunnable(scope.Goals[2], wood), "only the real rooted A is initially legal; no hidden floor, wall, liquid or cardinal side support unlocks its successors");
        }

        internal static void SchedulerDependencyChain(object context, object host, object tools, object input, bool horizontal = false)
        {
            BusinessEvent[] automatic;
            using (var scope = new BusinessScope(context, host, tools, input))
            using (var timeline = new SchedulerTimeline(host, tools, input))
            {
                SchedulerDependencySetup(scope, horizontal); scope.Begin(true); SetAutomatic(host, input, true);
                try
                {
                    NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Events.Count != 0; });
                    Require(scope.Events.Count == 1 && scope.Goals[0].Complete && !scope.Goals[1].Complete && !scope.Goals[2].Complete,
                        "the current A group is established by the actual rooted Wood consumer and debit");
                    Require(!SchedulerBlockRunnable(scope.Goals[2], scope.Sources[0]) && SchedulerBlockRunnable(scope.Goals[1], scope.Sources[1]),
                        "after real lower/left A, the remaining A has no cardinal connection while supplied B is independently legal");
                    NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Goals.All(goal => goal.Complete) && !(bool)Get(Get(tools, "Use"), "Active"); });
                    Require(scope.Events.Count == 3 && scope.Events.Select(value => value.Type).SequenceEqual(new int[] { ItemID.Wood, ItemID.StoneBlock, ItemID.Wood }) &&
                        scope.Events.SelectMany(value => value.ChangedGoals).SequenceEqual(new[] { 0, 1, 2 }) && scope.Sources[0].Item.stack == 2 && scope.Sources[1].Item.stack == 2,
                        "the real dependency order is rooted A then connecting B then released A, exactly two Wood and one Stone, with no extra debit");
                    scope.Report("scheduler dependency automatic horizontal=" + horizontal);
                    timeline.Report("scheduler dependency automatic horizontal=" + horizontal); automatic = scope.Events.ToArray();
                }
                catch { scope.Report("scheduler dependency FAILURE horizontal=" + horizontal); timeline.Report("scheduler dependency FAILURE horizontal=" + horizontal); throw; }
            }
            using (var scope = new BusinessScope(context, host, tools, input))
            using (var timeline = new SchedulerTimeline(host, tools, input))
            {
                SchedulerDependencySetup(scope, horizontal); scope.Begin(false); int cursor = 0;
                NativeQuickItemChecks.Until(() =>
                {
                    var goal = scope.Goals[cursor]; var source = scope.Sources.Single(value => value.Type == goal.Type); var p = Main.LocalPlayer;
                    if (p.selectedItem != source.Slot)
                    {
                        if (!p.selectedItemState.HasBufferedChange) p.selectedItemState.Select(source.Slot);
                        if (!p.selectedItemState.CanChangeSelectedItemImmediately) { scope.Step(); return false; }
                    }
                    int before = scope.Events.Count;
                    // Selection is buffered legally, and ItemCheck later in
                    // this same Update decides whether held can really act.
                    bool held = p.itemAnimation > 0 || p.releaseUseItem || source.Item.autoReuse;
                    scope.Step(new Vector2(goal.X * 16 + 8, goal.Y * 16 + 8), held);
                    if (scope.Events.Count > before) cursor++;
                    return cursor == scope.Goals.Length;
                });
                scope.Report("scheduler dependency original horizontal=" + horizontal);
                timeline.Report("scheduler dependency original horizontal=" + horizontal);
                Require(scope.Events.Count == automatic.Length && scope.Goals.All(goal => goal.Complete) && scope.Sources[0].Item.stack == 2 && scope.Sources[1].Item.stack == 2,
                    "same initial real world and inventory complete the same legal A/B/A chain using original selectedItemState and held input");
                for (int i = 1; i < automatic.Length; i++)
                    Require(unchecked(automatic[i].World - automatic[i - 1].World) == unchecked(scope.Events[i].World - scope.Events[i - 1].World),
                        "every dependency handoff, including both material changes, uses the same original-input opportunity interval");
            }
        }

        internal static void SchedulerKnownRefusalCompetition(object context, object host, object tools, object input, int kind)
        {
            Require(kind >= 0 && kind <= 2, "known refusal fixture selects wall support, chair anchor or solid lava");
            using (var scope = new BusinessScope(context, host, tools, input))
            using (var timeline = new SchedulerTimeline(host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                int material = kind == 0 ? ItemID.WoodWall : kind == 1 ? ItemID.WoodenChair : ItemID.Wood;
                var current = BusinessStock(p, 12, material, 4);
                var foreign = new BusinessSource { Slot = 13, Type = ItemID.StoneBlock, Item = p.inventory[13] };
                scope.Sources = new[] { current, foreign };
                LibraryEntry asset;
                if (kind == 0)
                {
                    asset = BusinessWallAsset(host, current);
                    scope.Goals = new[] { BusinessWallGoal(44, 40, current), BusinessWallGoal(47, 40, current),
                        new BusinessGoal { X = 45, Y = 42, Type = ItemID.StoneBlock, TileType = TileID.Stone } };
                    WorldGen.PlaceWall(44, 39, WallID.Stone, true);
                    scope.RunnableOracle = goal => goal.Type == foreign.Type ? SchedulerBlockRunnable(goal, foreign) :
                        !goal.Complete && !current.Item.IsAir && current.Item.stack > 0 &&
                        p.IsInTileInteractionRange(goal.X, goal.Y, TileReachCheckSettings.Simple, current.Item.tileBoost + p.blockRange) &&
                        new[] { new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1) }.Any(offset => {
                            var tile = Main.tile[goal.X + offset.X, goal.Y + offset.Y]; return tile.active() || tile.wall != 0; });
                }
                else if (kind == 1)
                {
                    asset = PreflightChairAsset(context, host, input);
                    var top = Main.tile[60, 41]; var bottom = Main.tile[60, 42];
                    scope.Goals = new[] {
                        new BusinessGoal { X = 44, Y = 42, Height = 2, Type = current.Type, TileType = TileID.Chairs,
                            ChairFrameX = top.frameX, ChairTopY = top.frameY, ChairBottomY = bottom.frameY },
                        new BusinessGoal { X = 46, Y = 42, Height = 2, Type = current.Type, TileType = TileID.Chairs,
                            ChairFrameX = top.frameX, ChairTopY = top.frameY, ChairBottomY = bottom.frameY },
                        new BusinessGoal { X = 47, Y = 42, Type = ItemID.StoneBlock, TileType = TileID.Stone } };
                    Main.tile[46, 43].ClearEverything();
                    scope.RunnableOracle = goal => goal.Type == foreign.Type ? SchedulerBlockRunnable(goal, foreign) :
                        !goal.Complete && !current.Item.IsAir && current.Item.stack > 0 &&
                        p.IsInTileInteractionRange(goal.X, goal.Y, TileReachCheckSettings.Simple, current.Item.tileBoost + p.blockRange) &&
                        PreflightChairCanPlace(goal.X, goal.Y, current.Item);
                }
                else
                {
                    asset = BusinessTileAsset(host, current);
                    scope.Goals = new[] { BusinessTileGoal(44, 42, current), BusinessTileGoal(46, 42, current),
                        new BusinessGoal { X = 47, Y = 42, Type = ItemID.StoneBlock, TileType = TileID.Stone } };
                    PreflightLiquidStore(46, 42, 80, 1);
                    Require(PreflightLavaBlocked(p, 46, 42, current.Item), "the original Player lava gate independently rejects this supported solid block");
                    scope.RunnableOracle = goal => SchedulerBlockRunnable(goal, goal.Type == foreign.Type ? foreign : current);
                }
                Place(context, host, input, asset, scope.Goals[0].X, scope.Goals[0].Y - scope.Goals[0].Height + 1);
                Place(context, host, input, asset, scope.Goals[1].X, scope.Goals[1].Y - scope.Goals[1].Height + 1);
                Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), scope.Goals[2].X, scope.Goals[2].Y);
                Require(scope.Runnable(scope.Goals[0]) && !scope.Runnable(scope.Goals[1]), "the first A is legal while the second has its explicit independent native refusal");
                scope.Begin(true); SetAutomatic(host, input, true);
                try
                {
                    NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Events.Count > 0; });
                    Require(scope.Events.Count == 1 && scope.Goals[0].Complete && !scope.Goals[1].Complete, "the first actual A consumer establishes the group without completing the blocked A");
                    var refill = new Item(); refill.SetDefaults(ItemID.StoneBlock); refill.stack = 3;
                    Require(p.GetItem(refill, new GetItemSettings(NoText: true, NoSound: true)).IsAir, "the original pickup enables the real competing B after A is established");
                    foreign.Slot = Enumerable.Range(0, 50).Single(slot => p.inventory[slot].type == ItemID.StoneBlock); foreign.Item = p.inventory[foreign.Slot];
                    Require(!scope.Runnable(scope.Goals[1]) && scope.Runnable(scope.Goals[2]), "A remains known rejected and the exact supplied B is independently legal");
                    NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Goals[2].Complete; });
                    Require(scope.Events.Count == 2 && scope.Events[1].Type == foreign.Type && !scope.Goals[1].Complete && current.Item.stack == 3,
                        "known refusal converges and admits B without a rejected A action or debit");
                    if (kind == 0) WorldGen.PlaceWall(47, 39, WallID.Stone, true);
                    else if (kind == 1) Require(WorldGen.PlaceTile(46, 43, TileID.Stone, true, true), "the real writer restores only the missing chair anchor");
                    else PreflightLiquidStore(46, 42, 0, 0);
                    Require(scope.Runnable(scope.Goals[1]), "the actual support/anchor/liquid recovery restores A eligibility without touching product proofs");
                    NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Goals.All(goal => goal.Complete) && !(bool)Get(Get(tools, "Use"), "Active"); });
                    Require(scope.Events.Select(value => value.Type).SequenceEqual(new[] { current.Type, foreign.Type, current.Type }) &&
                        current.Item.stack == 2 && foreign.Item.stack == 2, "A/B/A recovery consumes precisely two current items and one foreign item");
                    scope.Report("scheduler refusal kind=" + kind); timeline.Report("scheduler refusal kind=" + kind);
                }
                catch { scope.Report("scheduler refusal FAILURE kind=" + kind); timeline.Report("scheduler refusal FAILURE kind=" + kind); throw; }
            }
        }

        private static SchedulerWarmObservation schedulerWarm;
        private sealed class SchedulerWarmObservation
        {
            internal object Host, Owner;
            internal BusinessScope Scope;
            internal int Expected, Visits, Calls, MeasuredCalls, TotalVisits, MaxVisits;
            internal bool InCall, SourceWait, Failed;
            internal string Failure;
        }
        private static void SchedulerWarmBeforeSearch(object __instance)
        {
            var state = schedulerWarm;
            if (state == null || !ReferenceEquals(state.Host, __instance)) return;
            var active = GetOptional(state.Host, "activeBuild");
            state.InCall = state.Scope.Goals[0].Complete && !state.Scope.Goals[1].Complete &&
                (active == null || (bool)Get(active, "Checked"));
            state.Visits = state.Calls = 0; state.SourceWait = false;
        }
        private static void SchedulerWarmBeforeWalk(object __0, ref int __5, out int __state)
        { __state = __5; }
        private static void SchedulerWarmAfterWalk(object __0, ref int __5, int __state, object __result)
        {
            var state = schedulerWarm;
            if (state == null || !state.InCall) return;
            state.SourceWait |= __result.ToString() == "SourcePending";
            if (!ReferenceEquals(Get(__0, "Owner"), state.Owner)) return;
            state.Visits += __state - __5; state.Calls++;
        }
        private static void SchedulerWarmAfterSearch(object __instance, object __result)
        {
            var state = schedulerWarm;
            if (state == null || !state.InCall || !ReferenceEquals(state.Host, __instance)) return;
            state.InCall = false;
            if (state.Visits == 0 || state.SourceWait) return;
            int remainder = (int)Get(state.Host, "materialBuildSourceBudget");
            state.MeasuredCalls++;
            state.TotalVisits += state.Visits; state.MaxVisits = Math.Max(state.MaxVisits, state.Visits);
            Console.WriteLine("BlueprintSchedulerWarm expectedActualMembers=" + state.Expected + " visited=" + state.Visits + " walkCalls=" + state.Calls +
                " result=" + __result + " sourceRemaining=" + remainder + " searchRemaining=" + Get(state.Host, "materialBuildSearchBudget") +
                " host=" + Get(state.Host, "projectionMatchTick") + " world=" + Main.GameUpdateCount + " activeChecked=true actualIOWait=false");
            // All A's destinations are known independently: the only WoodWall
            // has really completed, the other records require unavailable
            // StoneWall. This counter measures the actual preferred visits;
            // the production result is the subject, never the absence oracle.
            if (remainder > 0 && (int)Get(state.Host, "materialBuildSearchBudget") > 0 && __result.ToString() == "BudgetPending")
            {
                state.Failed = true; state.Failure = "warm exhausted A left synchronous member work or a resolved Pending despite shared source budget: expected=" +
                    state.Expected + " actual=" + state.Visits + " result=" + __result + " remainder=" + remainder;
            }
        }

        internal static void SchedulerWarmMemberBoundary(object context, object host, object tools, object input, int members)
        {
            Require(members == 9 || members == 17 || members == 18, "warm slice boundary is an actual 9 or 17/18 member query");
            using (var scope = new BusinessScope(context, host, tools, input))
            using (var timeline = new SchedulerTimeline(host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                var wall = BusinessStock(p, 12, ItemID.WoodWall, 4); var stone = BusinessStock(p, 13, ItemID.StoneBlock, 3);
                scope.Sources = new[] { wall, stone };
                scope.Goals = new[] { BusinessWallGoal(38, 39, wall), BusinessTileGoal(47, 42, stone) };
                var cells = Enumerable.Range(0, members).Select(index => new CaptureCell { X = index % 9, Y = index / 9,
                    Layers = CaptureLayer.Wall, WallType = index == 0 ? (ushort)WallID.Wood : (ushort)WallID.Stone }).ToArray();
                WorldGen.PlaceWall(37, 39, WallID.Stone, true);
                var owner = Place(context, host, input, Asset(host, cells), 38, 39);
                var other = Place(context, host, input, BusinessTileAsset(host, stone), 47, 42);
                SetAutomatic(host, input, true);
                // Only immutable source/qualification/publication and the
                // initial prepared A are warmed; no Player.Update means no
                // group has been admitted and no preferred negative was run.
                int warmUpdates=0;
                Action<string> reportWarm=phase=>
                {
                    var prepared=GetOptional(host,"preparedBuild");
                    var ranges=new[]{owner,other}.Where(value=>GetOptional(value,"Match")!=null).SelectMany(EligibilityRanges).ToArray();
                    Console.WriteLine("BlueprintSchedulerWarm setup phase="+phase+" members="+members+" updates="+warmUpdates+
                        " input="+Get(input,"CanRunAutomaticActions")+" enabled="+Get(host,"AutoBuildEnabled")+" controls="+Get(host,"ControlsEnabled")+
                        " catalog="+(GetOptional(host,"MaterialCatalog")!=null)+" preparedType="+(prepared==null?"none":Get(Get(prepared,"Source"),"Type").ToString())+
                        " current="+string.Join(",",ranges.Select(range=>Get(range,"Current")))+
                        " facts="+string.Join(";",ranges.SelectMany(range=>NearRangeFacts(host,range)).Select(fact=>
                            Get(Get(fact,"Seed"),"Ordinal")+":"+Get(fact,"Ready")+":"+Get(fact,"Pending")+":"+Get(fact,"Wait")))+
                        " source/search="+Get(host,"materialBuildSourceBudget")+"/"+Get(host,"materialBuildSearchBudget"));
                };
                try
                {
                    NativeQuickItemChecks.Until(() => { scope.Step(preparationOnly: true);warmUpdates++;
                        if(warmUpdates==1)reportWarm("first");
                        var prepared = GetOptional(host, "preparedBuild");
                        return prepared != null && ReferenceEquals(Get(Get(prepared, "Source"), "Item"), wall.Item) &&
                            GetOptional(owner, "Match") != null && GetOptional(other, "Match") != null &&
                            EligibilityRanges(owner).Concat(EligibilityRanges(other)).All(range => (bool)Get(range, "Current")); });
                }
                catch {reportWarm("failure");throw;}
                Require(GetOptional(host, "buildContinuation") == null && scope.Goals.All(goal => !goal.Complete) && wall.Item.stack == 4 && stone.Item.stack == 3,
                    "source warmup performed no current-group negative query, actual consumer or debit");
                schedulerWarm = new SchedulerWarmObservation { Host = host, Owner = owner, Scope = scope, Expected = members };
                var patch = new Harmony("JueMingR.Tests.BlueprintSchedulerWarm");
                try
                {
                    patch.Patch(host.GetType().GetMethod("TryPreferredBuild", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerWarmBeforeSearch)),
                        postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerWarmAfterSearch)));
                    patch.Patch(host.GetType().GetMethod("WalkPreferredSource", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerWarmBeforeWalk)),
                        postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerWarmAfterWalk)));
                    scope.Begin(true);
                    NativeQuickItemChecks.Until(() => { scope.Step(); Require(!schedulerWarm.Failed, schedulerWarm.Failure ?? "warm member work is current");
                        return scope.Goals.All(goal => goal.Complete) && !(bool)Get(Get(tools, "Use"), "Active"); });
                    Require(schedulerWarm.MeasuredCalls > 0 && schedulerWarm.TotalVisits >= members && scope.Events.Count == 2 && scope.Events[0].Type == wall.Type && scope.Events[1].Type == stone.Type &&
                        wall.Item.stack == 3 && stone.Item.stack == 2 && Main.tile[38, 39].wall == WallID.Wood,
                        "a naturally established checked A exhausts its actual warm member query and the legal B really consumes once");
                    scope.Report("scheduler warm members=" + members); timeline.Report("scheduler warm members=" + members);
                }
                catch { scope.Report("scheduler warm FAILURE members=" + members); timeline.Report("scheduler warm FAILURE members=" + members); throw; }
                finally { patch.UnpatchAll(patch.Id); schedulerWarm = null; }
            }
        }

        internal static void SchedulerLateCurrentSource(object context, object host, object tools, object input)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            using (var timeline = new SchedulerTimeline(host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                var wood = BusinessStock(p, 12, ItemID.Wood, 4); var stone = BusinessStock(p, 13, ItemID.StoneBlock, 3);
                scope.Sources = new[] { wood, stone };
                var initialBounds=(BlueprintProjectionBounds)Call(host,"BuildNearOrdinaryWorldBounds",p);
                int lateX=initialBounds.Right;
                scope.Goals = new[] { BusinessTileGoal(40, 42, wood), BusinessTileGoal(lateX, 42, wood), BusinessTileGoal(39, 42, stone) };
                scope.RunnableOracle = goal => SchedulerBlockRunnable(goal, scope.Sources.Single(source => source.Type == goal.Type));
                var woodAsset = BusinessTileAsset(host, wood);
                NativeToolsChecks.Tile(lateX, 42, TileID.Dirt);
                Place(context, host, input, woodAsset, 40, 42);
                var late = Place(context, host, input, woodAsset, lateX, 42);
                Place(context, host, input, BusinessTileAsset(host, stone), 39, 42);
                NativeQuickItemChecks.Until(()=>{scope.Step(preparationOnly:true);return GetOptional(late,"Match")!=null;});
                var lateRange=EligibilityRanges(late).Single();
                Require(!initialBounds.Contains(lateX,42) && NearRangeFacts(host,lateRange).Length==0,
                    "the future A owner begins just outside ordinary discovery with no acquired semantic facts");
                scope.Begin(true); SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Events.Count != 0; });
                Require(scope.Events.Count == 1 && scope.Goals[0].Complete && scope.Events[0].Type == wood.Type,
                    "late-source case begins with a genuine A action, exact debit and active current group");
                NativeQuickItemChecks.Until(() => { scope.Step(preparationOnly: true); var prepared = GetOptional(host, "preparedBuild");
                    return prepared != null && ReferenceEquals(Get(Get(prepared, "Source"), "Item"), stone.Item); });
                Require(scope.Events.Count == 1, "B is really prepared while no second native action has been introduced during setup");
                var originalGroup=Get(host,"buildContinuation");
                var warmBounds=(BlueprintProjectionBounds)Call(host,"BuildNearOrdinaryWorldBounds",p);
                Require(NearRangeFacts(host,lateRange).Length==0 && !warmBounds.Contains(lateX,42),
                    "B warmup has never classified the still-outside A range");
                var patch = new Harmony("JueMingR.Tests.BlueprintSchedulerLateSource");
                eligibilityWorker = Get(Get(host, "Library"), "worker");
                eligibilitySourceEntered = new System.Threading.ManualResetEventSlim(); eligibilitySourceContinue = new System.Threading.ManualResetEventSlim();
                try
                {
                    patch.Patch(AccessTools.Method(eligibilityWorker.GetType(), "ProjectionSourceStep"),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(PauseEligibilitySource)));
                    // This range has never entered the near semantic plan.
                    // Transport eviction alone cannot make an already-known
                    // fact unknown; actual movement now introduces this owner.
                    Call(host, "RemoveBuildSources", Get(late, "Identity"), lateRange);
                    WorldGen.KillTile(lateX, 42, fail: false, effectOnly: false, noItem: true);
                    NativeQuickItemChecks.Until(() => { scope.Step(direction:1);
                        Require(scope.Events.Count==1 && ReferenceEquals(GetOptional(host,"buildContinuation"),originalGroup),
                            "entering unknown A's near domain preserves the same actual group and admits no B");
                        return eligibilitySourceEntered.IsSet && SchedulerBlockRunnable(scope.Goals[1],wood); });
                    Require(SchedulerBlockRunnable(scope.Goals[1], wood) && SchedulerBlockRunnable(scope.Goals[2], stone) && scope.Events.Count == 1,
                        "the late A source has one outstanding real worker request; known world/stock make A and prepared B physically legal");
                    lock(Get(eligibilityWorker,"gate"))
                        Require(((System.Collections.IEnumerable)Get(eligibilityWorker,"projectionSourceRequests")).Cast<object>()
                            .Any(request=>Equals(Get(request,"Identity"),Get(late,"Identity"))),
                            "the held original source request belongs to the newly entered A owner");
                    Require(NearRangeState(host,lateRange)!=null && NearRangeFacts(host,lateRange).Length==0 &&
                        ((BlueprintProjectionBounds)Call(host,"BuildNearOrdinaryWorldBounds",p)).Contains(lateX,42) &&
                        ReferenceEquals(GetOptional(host,"buildContinuation"),originalGroup) && (bool)Call(host,"BuildContinuationCurrent",p),
                        "the paused original worker has published no semantic A fact and the established group remains current");
                    long requests = (long)Get(host, "ProjectionMatchSourceRequests");
                    int pendingTimelineStart = timeline.Events.Count;
                    int naturalWindow = wood.Item.useAnimation + 2;
                    for (int frame = 0; frame < naturalWindow; frame++) scope.Step();
                    Require(scope.Events.Count == 1 && NearRangeFacts(host,lateRange).Length==0 &&
                        ReferenceEquals(GetOptional(host,"buildContinuation"),originalGroup) && !scope.Goals[1].Complete && !scope.Goals[2].Complete &&
                        wood.Item.stack == 3 && stone.Item.stack == 3 && (long)Get(host, "ProjectionMatchSourceRequests") == requests &&
                        !timeline.Events.Skip(pendingTimelineStart).Any(value => value.Kind == "admitted" && value.Type == stone.Type),
                        "a full natural use interval with unknown current-A source neither admits/consumes B nor resubmits the outstanding source request");
                    uint released = Main.GameUpdateCount;
                    Console.WriteLine("BlueprintSchedulerLateSource release world=" + released + " host=" + Get(host, "projectionMatchTick") +
                        " blockedActualUpdates=" + naturalWindow + " originalRequestCount=" + requests + " preparedForeignWasObserved=true neverClassifiedA=true lateX="+lateX+" nativePosition="+p.position);
                    eligibilitySourceContinue.Set();
                    NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Goals.All(goal => goal.Complete) && !(bool)Get(Get(tools, "Use"), "Active"); });
                    Require(scope.Events.Select(value => value.Type).SequenceEqual(new[] { wood.Type, wood.Type, stone.Type }) &&
                        wood.Item.stack == 2 && stone.Item.stack == 2,
                        "the original late receipt resumes actual A before B with no duplicate resource promise or extra debit");
                    var preparedA = timeline.Events.First(value => value.Kind == "prepared" && value.Type == wood.Type && value.World >= released);
                    var selection = timeline.Events.First(value => value.Kind == "selection-before" && value.World > preparedA.World && value.CanSelect);
                    Require(timeline.Events.Any(value => value.Kind == "admitted" && value.Flight == preparedA.Flight && value.World == selection.World),
                        "once the late-source A is actually prepared, its next original legal selection callback admits that exact flight without an extra Host gap");
                    scope.Report("scheduler late current source"); timeline.Report("scheduler late current source");
                }
                catch { scope.Report("scheduler late current source FAILURE"); timeline.Report("scheduler late current source FAILURE"); throw; }
                finally
                {
                    eligibilitySourceContinue.Set(); patch.UnpatchAll(patch.Id); eligibilityWorker = null;
                    // The released original worker may still be returning
                    // from Wait; its signal remains valid through that tail.
                }
            }
        }

        private static SchedulerSubstitutionReadObservation schedulerSubstitutionRead;
        private sealed class SchedulerSubstitutionReadObservation
        {
            internal object Host, Reader;
            internal int ActiveItem, UnavailableReads;
            internal bool Fault = true;
            internal readonly System.Collections.Generic.List<int> GeometryItems = new System.Collections.Generic.List<int>();
        }
        private static bool SchedulerSuppressPlanningDispatch(object __instance)
        { return schedulerSubstitutionRead == null || !ReferenceEquals(__instance, schedulerSubstitutionRead.Host); }
        private static void SchedulerSubstitutionGeometryBefore(object __instance, Item __2, out int __state)
        {
            var state = schedulerSubstitutionRead; __state = state == null ? 0 : state.ActiveItem;
            if (state == null || !ReferenceEquals(__instance, state.Host)) return;
            state.ActiveItem = __2.type; state.GeometryItems.Add(__2.type);
        }
        private static void SchedulerSubstitutionGeometryAfter(int __state)
        { if (schedulerSubstitutionRead != null) schedulerSubstitutionRead.ActiveItem = __state; }
        private static bool SchedulerSubstitutionReadFault(object __instance, int __0, int __1, ref BlueprintWorldMatchSample __2, ref bool __result)
        {
            var state = schedulerSubstitutionRead;
            if (state == null || !state.Fault || !ReferenceEquals(__instance, state.Reader) || state.ActiveItem != ItemID.WoodPlatform || __0 != 44 || __1 != 40) return true;
            // This finite availability-port fault is an input to the ORIGINAL
            // ReadBuildCell. Its own unavailable serial and typed adapter run;
            // no Readiness, proof, negative set or cursor is written here.
            state.UnavailableReads++; __2 = default(BlueprintWorldMatchSample); __result = false; return false;
        }
        private static object[] SchedulerInvokePlanning(object host, object target, Player player, int budget, out bool ready)
        {
            // Component boundary: simulate this pass starting with one of the
            // shared 64 credits left, then an ordinary full next allowance.
            // These are not claims about 64 natural Player-update candidates.
            Set(host, "substitutionCandidateBudget", budget);
            object[] arguments = { target, player, null, null, null, 0UL };
            ready = (bool)Call(host, "TryPlanBuild", arguments); return arguments;
        }

        private static object[] SchedulerSubstitutionCursorStamp(object cursor)
        {
            var sourceField = cursor.GetType().GetField("Source", F);
            Require(sourceField != null, "the cursor Source identity field exists even when a new selection has no preferred Item");
            return new[] { Get(cursor, "Ordinal"), Get(cursor, "Range"), Get(cursor, "Support"), Get(cursor, "Supply"),
                Get(cursor, "Player"), Get(cursor, "Policy"), sourceField.GetValue(cursor), Get(cursor, "Slot"), Get(cursor, "Type") };
        }

        internal static void SchedulerSubstitutionDeferredBudget(object context, object host, object tools, object input, bool positiveTail = false, bool materialTail = false)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                var p = SubstitutionReset(context, host, tools, input); SubstitutionCatalog(context, host, input);
                Call(host, "SetMaterialViewDemand", false);
                var wood = BusinessStock(p, 12, ItemID.WoodPlatform, 3); var bone = BusinessStock(p, 13, ItemID.BonePlatform, 3);
                int originalBoneStack = bone.Item.stack;
                scope.Sources = new[] { wood, bone }; scope.Goals = new[] { BusinessTileGoal(44, 40, wood) };
                if (positiveTail) Require(WorldGen.PlaceTile(44, 41, TileID.Stone, true, true), "the positive component has a genuine common platform support");
                else for (int x = 43; x <= 45; x++) for (int y = 39; y <= 41; y++) Main.tile[x, y].ClearEverything();
                var owner = Place(context, host, input, BusinessTileAsset(host, wood), 44, 40);
                var patch = new Harmony("JueMingR.Tests.BlueprintSubstitutionDeferredBudget");
                int oldBudget = (int)Get(host, "substitutionCandidateBudget");
                schedulerSubstitutionRead = new SchedulerSubstitutionReadObservation { Host = host, Reader = Get(host, "projectionMatchSource") };
                try
                {
                    // Only automatic dispatch is suppressed while normal
                    // source/qualification/world publication creates the real
                    // candidate. The assertions below concern the actual
                    // planning component, not an automatic/native consumer.
                    patch.Patch(host.GetType().GetMethod("AdvanceAutomaticBuild", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerSuppressPlanningDispatch)));
                    patch.Patch(host.GetType().GetMethod("NativeBuildGeometry", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerSubstitutionGeometryBefore)),
                        postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerSubstitutionGeometryAfter)));
                    patch.Patch(schedulerSubstitutionRead.Reader.GetType().GetMethod("TryRead", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerSubstitutionReadFault)));
                    SetAutomatic(host, input, true); object target = null;
                    NativeQuickItemChecks.Until(() => { scope.Step(preparationOnly: true);
                        if (GetOptional(owner, "Match") == null || !EligibilityRanges(owner).All(range => (bool)Get(range, "Current"))) return false;
                        object[] arguments = { null }; bool found = (bool)Call(host, "TryGetBuildTarget", arguments); target = arguments[0]; return found; });
                    Require(target != null && (int)Get(target, "WorldX") == 44 && (int)Get(target, "WorldY") == 40 &&
                        GetOptional(Get(Get(target, "Range"), "Materials"), "SubstitutionSelection") == null,
                        "normal source/world qualification creates the untouched first real substitution planning cursor");
                    long beforeChecks = (long)Get(host, "SubstitutionCandidateChecks"); bool ready;
                    SchedulerInvokePlanning(host, target, p, 1, out ready);
                    var cursor = Get(Get(Get(target, "Range"), "Materials"), "SubstitutionSelection");
                    Require(!ready && (bool)Get(host, "substitutionPlanningPending") && Get(target, "Readiness").ToString() == "Deferred" &&
                        (int)Get(cursor, "Cursor") == 1 && schedulerSubstitutionRead.UnavailableReads == 1 &&
                        (long)Get(host, "SubstitutionCandidateChecks") - beforeChecks == 1,
                        "one actual Wood candidate observes unavailable input and naturally pauses at cursor one with no hand-written progress");
                    int paused = (int)Get(cursor, "Cursor");
                    object inventory = Get(host, "MaterialInventory");
                    long inventoryRevision = (long)Get(inventory, "Revision");
                    var pausedStamp = SchedulerSubstitutionCursorStamp(cursor);
                    if (materialTail)
                    {
                        Require(!positiveTail && ReferenceEquals(p.inventory[bone.Slot], bone.Item) && !bone.Item.IsAir &&
                            (long)Get(cursor, "Supply") == inventoryRevision,
                            "the material-tail component starts with the genuine published Bone stock and original paused supply stamp");
                        // Component input only: the live fixture Item becomes
                        // unavailable before another Host inventory publication.
                        // This is not a native debit or an inventory operation
                        // claim; neither progress nor any typed result is set.
                        bone.Item.stack = 0;
                        Require(bone.Item.IsAir && ReferenceEquals(Get(host, "MaterialInventory"), inventory) &&
                            (long)Get(Get(host, "MaterialInventory"), "Revision") == inventoryRevision &&
                            pausedStamp.SequenceEqual(SchedulerSubstitutionCursorStamp(cursor)),
                            "actual Bone availability changes while published inventory and every original cursor identity stamp remain unchanged");
                    }
                    var tail = SchedulerInvokePlanning(host, target, p, 64, out ready);
                    Console.WriteLine("BlueprintSchedulerDeferredBudget positiveTail=" + positiveTail + " materialTail=" + materialTail + " boundaryCredits=1/64 cursor=0->" + paused + "->" + Get(cursor, "Cursor") +
                        " actualCandidates=" + string.Join(",", schedulerSubstitutionRead.GeometryItems) + " unavailable=" + schedulerSubstitutionRead.UnavailableReads +
                        " actualCandidateChecks=" + ((long)Get(host, "SubstitutionCandidateChecks") - beforeChecks) +
                        " ready=" + ready + " readiness=" + Get(target, "Readiness") + " wait=" + tail[4] +
                        " boneLiveAir=" + bone.Item.IsAir + " inventoryRevision=" + inventoryRevision + "->" + Get(Get(host, "MaterialInventory"), "Revision") +
                        " sourceStampUnchanged=" + pausedStamp.SequenceEqual(SchedulerSubstitutionCursorStamp(cursor)) +
                        " nativeConsumer=NOT_EVALUATED componentOnly=true");
                    Require(schedulerSubstitutionRead.GeometryItems.SequenceEqual(materialTail ? new[] { wood.Type } : new[] { wood.Type, bone.Type }) &&
                        schedulerSubstitutionRead.UnavailableReads == 1 && (long)Get(host, "SubstitutionCandidateChecks") - beforeChecks == 2 &&
                        (int)Get(cursor, "Cursor") == 0, "the original cursor resumes only the exact Bone tail, with stable original candidates and no replay of the unknown prefix");
                    if (materialTail)
                        Require(bone.Item.IsAir && ReferenceEquals(p.inventory[bone.Slot], bone.Item) &&
                            ReferenceEquals(Get(Get(Get(target, "Range"), "Materials"), "SubstitutionSelection"), cursor) &&
                            ReferenceEquals(Get(host, "MaterialInventory"), inventory) && (long)Get(inventory, "Revision") == inventoryRevision &&
                            pausedStamp.SequenceEqual(SchedulerSubstitutionCursorStamp(cursor)),
                            "the resumed original Bone candidate is known missing before geometry, without a new publication or cursor identity");
                    if (positiveTail)
                        Require(ready && tail[3] != null && (int)Get(tail[3], "ToolType") == bone.Type && Get(target, "Readiness").ToString() == "Ready",
                            "a genuine readable supported Bone positive can succeed despite an earlier unknown Wood candidate");
                    else
                        Require(!ready && tail[3] == null && !(bool)Get(host, "substitutionPlanningPending") && Get(target, "Readiness").ToString() == "Deferred",
                            "a known-negative tail cannot erase an unresolved unknown prefix merely because the shared budget split the pass");
                    if (materialTail) bone.Item.stack = originalBoneStack;
                    schedulerSubstitutionRead.Fault = false;
                    var recovered = SchedulerInvokePlanning(host, target, p, 64, out ready);
                    Require((int)Get(cursor, "Cursor") == 0 && (positiveTail ? ready && recovered[3] != null && (int)Get(recovered[3], "ToolType") == wood.Type :
                        !ready && Get(target, "Readiness").ToString() == "BlockedNow"),
                        "after read recovery a fresh complete pass clears old Deferred: exact Wood succeeds or all actual candidates are now known blocked");
                    Require(!scope.Goals[0].Complete && wood.Item.stack == 3 && bone.Item.stack == 3 && !(bool)Get(Get(tools, "Use"), "Active"),
                        "planning evidence does not pretend to place a platform, debit stock or acquire a native use");
                }
                finally
                {
                    if (materialTail) bone.Item.stack = originalBoneStack;
                    SetAutomatic(host, input, false); patch.UnpatchAll(patch.Id); schedulerSubstitutionRead = null;
                    Set(host, "substitutionCandidateBudget", oldBudget); Call(host, "SetSameTypeReplacement", false);
                }
            }
        }
    }
}
