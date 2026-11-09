using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private sealed class BusinessSource
        {
            internal Item Item;
            internal int Slot, Type;
        }
        private sealed class BusinessGoal
        {
            internal int X, Y, Type, TileType, WallType, Height = 1, Width = 1, OriginX, Style;
            internal Func<bool> CompleteBody;
            internal short ChairFrameX, ChairTopY, ChairBottomY;
            internal bool Complete
            {
                get
                {
                    var tile = Main.tile[X, Y];
                    if (CompleteBody != null) return CompleteBody();
                    if (WallType != 0) return tile.wall == WallType;
                    if (Height == 2)
                    {
                        var top = Main.tile[X, Y - 1];
                        return top.active() && tile.active() && top.type == TileType && tile.type == TileType &&
                            top.frameX == ChairFrameX && tile.frameX == ChairFrameX && top.frameY == ChairTopY && tile.frameY == ChairBottomY;
                    }
                    return tile.active() && tile.type == TileType && tile.slope() == 0 && !tile.halfBrick() &&
                        (TileType != TileID.Platforms || tile.frameY == Style * 18);
                }
            }
        }
        private sealed class BusinessEvent
        {
            internal uint World;
            internal long Input, Host, Token, Native, Flight;
            internal int Type, Slot, X, Y, BeforeStack, Count, Time, Animation;
            internal Item Item;
            internal object Owner;
            internal string Consumer;
            internal bool Original, Exact, PreviousRunnable;
            internal bool[] BeforeGoals;
            internal int[] ChangedGoals;
        }
        private static BusinessScope businessScope;

        // The only order oracle is this small known goal table, native reach,
        // the real destination bodies/supports and the fixture's actual Items.
        // Focus/cursors/NoRunnable are never used to prove target absence.
        private sealed class BusinessScope : IDisposable
        {
            private readonly Harmony patch = new Harmony("JueMingR.Tests.BlueprintBusinessContinuity");
            private readonly bool smartMouse, smartPad;
            internal readonly object Context, Host, Tools, Input;
            internal readonly List<BusinessEvent> Events = new List<BusinessEvent>();
            private readonly List<BusinessEvent> pending = new List<BusinessEvent>();
            internal BusinessGoal[] Goals = Array.Empty<BusinessGoal>();
            internal BusinessSource[] Sources = Array.Empty<BusinessSource>();
            // A fixture with dependent supports or liquid supplies its own
            // real-world/native oracle; production Focus is never an oracle.
            internal Func<BusinessGoal, bool> RunnableOracle;
            internal bool Watching, Automatic;
            internal bool OrderCheckedAtAdmission;
            internal int CurrentType;
            private long unknown;

            internal BusinessScope(object context, object host, object tools, object input)
            {
                Require(businessScope == null, "business continuity has one bounded observation owner");
                Context = context; Host = host; Tools = tools; Input = input;
                smartMouse = Main.SmartCursorWanted_Mouse; smartPad = Main.SmartCursorWanted_GamePad;
                Main.SmartCursorWanted_Mouse = Main.SmartCursorWanted_GamePad = false;
                businessScope = this;
                try
                {
                    foreach (string name in new[] { "PlaceThing_Tiles_PlaceIt", "PlaceThing_Walls" })
                        patch.Patch(typeof(Player).GetMethod(name, F), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(BusinessBeforeNative)),
                            postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(BusinessAfterNative)));
                }
                catch { Dispose(); throw; }
            }
            internal void Begin(bool automatic)
            {
                Automatic = automatic; Watching = true; CurrentType = 0; Events.Clear(); pending.Clear();
                unknown = (long)Get(Host, "BuildUnknownResults");
            }
            internal bool Runnable(BusinessGoal goal)
            {
                if (RunnableOracle != null) return RunnableOracle(goal);
                var p = Main.LocalPlayer;
                var source = Sources.FirstOrDefault(value => value.Type == goal.Type && ReferenceEquals(p.inventory[value.Slot], value.Item) &&
                    value.Item.type == value.Type && !value.Item.IsAir && value.Item.stack > 0);
                if (source == null || goal.Complete || p.noBuilding || !p.IsInTileInteractionRange(goal.X, goal.Y,
                    TileReachCheckSettings.Simple, source.Item.tileBoost + p.blockRange)) return false;
                if (goal.WallType != 0)
                    return Main.tile[goal.X, goal.Y].wall == 0 && new[] { new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1) }
                        .Any(offset => { var tile = Main.tile[goal.X + offset.X, goal.Y + offset.Y]; return tile.wall != 0 || tile.active() && Main.tileSolid[tile.type]; });
                // Permanent setup supplies each real floor. Check every
                // furniture member and its native click-origin offset.
                for (int x = goal.X-goal.OriginX; x < goal.X-goal.OriginX+goal.Width; x++)
                    for (int y = goal.Y - goal.Height + 1; y <= goal.Y; y++) if (Main.tile[x, y].active()) return false;
                var bounds = new Rectangle((goal.X-goal.OriginX) * 16, (goal.Y - goal.Height + 1) * 16, goal.Width * 16, goal.Height * 16);
                return !Main.player.Any(player => player != null && player.active && !player.dead && bounds.Intersects(player.Hitbox)) &&
                    Enumerable.Range(goal.X-goal.OriginX,goal.Width).All(x=>WorldGen.SolidTile(x, goal.Y + 1));
            }
            internal void Step(Vector2? pointer = null, bool held = false, int direction = 0, bool preparationOnly = false)
            {
                var p = Main.LocalPlayer;
                long steps = (long)Get(Host, "ProjectionMatchSourceSteps"), checks = (long)Get(Host, "BuildCandidateChecks");
                NativeToolExecutionChecks.Sample(Context, Input, pointer ?? new Vector2(480, 540), held);
                p.controlLeft = direction < 0; p.controlRight = direction > 0;
                PlayerInput.Triggers.Current.Left = direction < 0; PlayerInput.Triggers.Current.Right = direction > 0;
                pending.Clear(); NativeQuickItemChecks.BeginWorldStep();
                if (!preparationOnly) { p.Update(0); FinishNativeStep(); }
                Call(Context, "UpdateRuntime");
                if (constructionChecksActive) constructionInputTail?.Invoke();
                Require((long)Get(Host, "ProjectionMatchSourceSteps") - steps <= 256 && (long)Get(Host, "BuildCandidateChecks") - checks <= 64 &&
                    (int)Get(Host, "materialBuildSourceBudget") >= 0 && (int)Get(Get(Host, "buildSources"), "Count") <= 4 &&
                    (long)Get(Host, "buildSourceBytes") <= 4L * BlueprintProjectionSourceReceipt.TargetBytes,
                    "business grouping shares existing per-update source/candidate, four-transport and retained-byte bounds");
                if (Watching) Require((long)Get(Host, "BuildUnknownResults") == unknown, "known native business effects do not add unknown consumption");
            }
            internal BusinessEvent Before(Player p, string consumer)
            {
                if (!Watching || !ReferenceEquals(p, Main.LocalPlayer)) return null;
                var item = p.HeldItem;
                var source = Sources.FirstOrDefault(value => ReferenceEquals(value.Item, item) && value.Type == item.type);
                if (source == null || consumer == "PlaceThing_Walls" && item.createWall <= 0 ||
                    consumer == "PlaceThing_Tiles_PlaceIt" && item.createTile < 0) return null;
                var flight = Automatic ? GetOptional(Host, "activeBuild") : null;
                var actual = flight == null ? null : Get(flight, "Source");
                var entry = new BusinessEvent { World = Main.GameUpdateCount, Input = (long)Get(Input, "Frame"),
                    Host = checked((long)(ulong)Get(Host, "projectionMatchTick")), Consumer = consumer,
                    Item = item, Type = item.type, Slot = Array.IndexOf(p.inventory, item), BeforeStack = item.stack,
                    X = Player.tileTargetX, Y = Player.tileTargetY, BeforeGoals = Goals.Select(goal => goal.Complete).ToArray(),
                    Token = Automatic ? (long)Get(Get(Tools, "Use"), "Operation") : 0,
                    Native = Automatic ? (long)Get(Get(Tools, "Use"), "NativeGeneration") : 0,
                    Flight = flight == null ? 0 : (long)Get(flight, "Generation"),
                    Owner = flight == null ? null : Get(Get(Get(flight, "Target"), "Range"), "Owner"),
                    Exact = actual == null ? !Automatic : ReferenceEquals(Get(actual, "Item"), item) && (int)Get(actual, "Slot") == source.Slot,
                    PreviousRunnable = CurrentType != 0 && Goals.Any(goal => goal.Type == CurrentType && Runnable(goal)) };
                pending.Add(entry); return entry;
            }
            private void FinishNativeStep()
            {
                foreach (var entry in pending)
                {
                    int remaining = entry.Item.IsAir ? 0 : entry.Item.stack;
                    entry.Count = entry.BeforeStack - remaining;
                    if (entry.Count <= 0) continue;
                    entry.ChangedGoals = Enumerable.Range(0, Goals.Length).Where(index => !entry.BeforeGoals[index] && Goals[index].Complete).ToArray();
                    if(!entry.Original || !entry.Exact || entry.ChangedGoals.Length!=entry.Count || entry.ChangedGoals.Any(index=>Goals[index].Type!=entry.Type))
                        Console.Error.WriteLine("BlueprintBusiness exact-effect mismatch automatic="+Automatic+" type="+entry.Type+" xy="+entry.X+","+entry.Y+" debit="+entry.Count+" changed="+string.Join(",",entry.ChangedGoals)+" direction="+Main.LocalPlayer.direction);
                    Require(entry.Original && entry.Exact && entry.ChangedGoals.Length == entry.Count &&
                        entry.ChangedGoals.All(index => Goals[index].Type == entry.Type),
                        "one original consumer pairs every exact Item debit with its complete real body/wall goals, including whole furniture members");
                    Require(!Automatic || entry.Token > 0 && entry.Native > 0 && entry.Flight > 0,
                        "each automatic native business event owns its current exact use and flight generation");
                    if (Automatic && !OrderCheckedAtAdmission && CurrentType != 0 && CurrentType != entry.Type && entry.PreviousRunnable)
                    {
                        Report("before forbidden switch");
                        Console.Error.WriteLine("BlueprintBusiness forbidden actual consumer world="+entry.World+" input="+entry.Input+" host="+entry.Host+
                            " previous="+CurrentType+" next="+entry.Type+" source="+entry.Slot+" target="+entry.X+","+entry.Y+
                            " debit="+entry.Count+" token/native/flight="+entry.Token+"/"+entry.Native+"/"+entry.Flight+" previousRunnable="+entry.PreviousRunnable);
                    }
                    Require(!Automatic || OrderCheckedAtAdmission || CurrentType == 0 || CurrentType == entry.Type || !entry.PreviousRunnable,
                        "known real reachable supplied same-material goals must precede an actual foreign consumer; current=" + CurrentType + " next=" + entry.Type);
                    entry.Time = Main.LocalPlayer.itemTime; entry.Animation = Main.LocalPlayer.itemAnimation;
                    CurrentType = entry.Type; Events.Add(entry);
                }
            }
            internal void Report(string name)
            {
                Console.WriteLine("BlueprintBusiness " + name + " events=world/input/host/type-slot/xy/count/time-animation/token-native-flight/goals " +
                    string.Join(";", Events.Select(entry => entry.World + "/" + entry.Input + "/" + entry.Host + "/" + entry.Type + "@" + entry.Slot + "/" +
                        entry.X + "," + entry.Y + "/" + entry.Count + "/" + entry.Time + "-" + entry.Animation + "/" + entry.Token + "-" + entry.Native + "-" + entry.Flight +
                        "/" + string.Join(",", entry.ChangedGoals))));
            }
            public void Dispose()
            {
                Watching = false;
                try
                {
                    SetAutomatic(Host, Input, false);
                    for (int n = 0; n < 80 && (bool)Get(Get(Tools, "Use"), "Active"); n++) Step();
                }
                finally
                {
                    foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                    businessScope = null; Main.SmartCursorWanted_Mouse = smartMouse; Main.SmartCursorWanted_GamePad = smartPad;
                }
            }
        }
        private static void BusinessBeforeNative(Player __instance, MethodBase __originalMethod, out BusinessEvent __state)
        { __state = businessScope?.Before(__instance, __originalMethod.Name); }
        private static void BusinessAfterNative(bool __runOriginal, BusinessEvent __state)
        { if (__state != null) __state.Original = __runOriginal; }
        private static BusinessSource BusinessStock(Player p, int slot, int type, int count)
        {
            p.inventory[slot].SetDefaults(type); p.inventory[slot].stack = count;
            return new BusinessSource { Slot = slot, Type = type, Item = p.inventory[slot] };
        }
        private static BusinessGoal BusinessTileGoal(int x, int y, BusinessSource source)
        { return new BusinessGoal { X = x, Y = y, Type = source.Type, TileType = source.Item.createTile, Style = source.Item.placeStyle }; }
        private static BusinessGoal BusinessWallGoal(int x, int y, BusinessSource source)
        { return new BusinessGoal { X = x, Y = y, Type = source.Type, WallType = source.Item.createWall }; }
        private static LibraryEntry BusinessTileAsset(object host, BusinessSource source)
        {
            return Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = (ushort)source.Item.createTile,
                FrameY = source.Item.createTile == TileID.Platforms ? (short)(source.Item.placeStyle * 18) : (short)0 });
        }
        private static LibraryEntry BusinessWallAsset(object host, BusinessSource source)
        { return Asset(host, new CaptureCell { Layers = CaptureLayer.Wall, WallType = (ushort)source.Item.createWall }); }

        private static void BusinessMixedSetup(BusinessScope scope)
        {
            var p = Reset(scope.Context, scope.Host, scope.Tools, scope.Input); p.position.X = 42 * 16;
            var platform = BusinessStock(p, 12, ItemID.WoodPlatform, 8);
            var chair = BusinessStock(p, 13, ItemID.WoodenChair, 8);
            var wall = BusinessStock(p, 14, ItemID.StoneWall, 8);
            scope.Sources = new[] { platform, chair, wall };
            Require(WorldGen.PlaceObject(60, 42, TileID.Chairs, true, chair.Item.placeStyle, direction: 1), "mixed business chair comes from the genuine native object writer");
            short chairX = Main.tile[60, 41].frameX, topY = Main.tile[60, 41].frameY, bottomY = Main.tile[60, 42].frameY;
            var chairAsset = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(scope.Context, scope.Host, scope.Input,
                (LibraryState)Get(scope.Host, "Library"), () => Step(scope.Context, scope.Input), 60, 41, 1, 2, "业务组完整原生椅子");
            var platformAsset = BusinessTileAsset(scope.Host, platform); var wallAsset = BusinessWallAsset(scope.Host, wall);
            scope.Goals = new[] {
                new BusinessGoal { X = 40, Y = 42, Type = chair.Type, TileType = TileID.Chairs, Height = 2, ChairFrameX = chairX, ChairTopY = topY, ChairBottomY = bottomY },
                BusinessWallGoal(41, 40, wall), BusinessTileGoal(44, 40, platform),
                new BusinessGoal { X = 46, Y = 42, Type = chair.Type, TileType = TileID.Chairs, Height = 2, ChairFrameX = chairX, ChairTopY = topY, ChairBottomY = bottomY },
                BusinessWallGoal(45, 40, wall), BusinessTileGoal(47, 40, platform) };
            // Real supports exist before all three groups are selectable.
            foreach (var goal in scope.Goals)
            {
                if (goal.WallType != 0) WorldGen.PlaceWall(goal.X, goal.Y - 1, WallID.Stone, true);
                else if (goal.Height == 1) Require(WorldGen.PlaceTile(goal.X, goal.Y + 1, TileID.Stone, true, true), "mixed platform has a real independent support");
            }
            foreach (var goal in scope.Goals)
                Place(scope.Context, scope.Host, scope.Input, goal.Height == 2 ? chairAsset : goal.WallType != 0 ? wallAsset : platformAsset,
                    goal.X, goal.Y - goal.Height + 1);
            Require(scope.Goals.All(scope.Runnable), "both walls, both platforms and both complete chairs are simultaneously legal, supplied and in native reach");
        }

        internal static void BusinessMixedGroups(object context, object host, object tools, object input)
        {
            BusinessEvent[] automatic;
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                BusinessMixedSetup(scope); scope.Begin(true); SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() => { scope.Step(); return Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"); });
                scope.Report("mixed walls/platforms/chairs automatic");
                Require(scope.Goals.All(goal => goal.Complete) && scope.Events.Sum(entry => entry.Count) == 6 &&
                    scope.Events.Select(entry => entry.Type).Distinct().Count() == 3 && scope.Sources.All(source => source.Item.stack == 6),
                    "each interleaved material completes its two actual goals, with full chairs costing one native item per object");
                automatic = scope.Events.ToArray();
            }
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                BusinessMixedSetup(scope); scope.Begin(false); int cursor = 0;
                NativeQuickItemChecks.Until(() =>
                {
                    Require(cursor < automatic.Length, "native mixed replay needs no extra consumer beyond the actual complete goal sequence");
                    var expected = automatic[cursor]; var source = scope.Sources.Single(value => value.Type == expected.Type); var p = Main.LocalPlayer;
                    if (p.selectedItem != source.Slot)
                    {
                        if (!p.selectedItemState.HasBufferedChange) p.selectedItemState.Select(source.Slot);
                        // Before vanilla can consume the buffered selection,
                        // held at the new target would still use the OLD Item.
                        // The first legal selection frame below is not empty.
                        if (!p.selectedItemState.CanChangeSelectedItemImmediately) { scope.Step(); return false; }
                    }
                    int before = scope.Events.Count;
                    bool held = p.itemAnimation > 0 || p.releaseUseItem || source.Item.autoReuse;
                    scope.Step(new Vector2(expected.X * 16 + 8, expected.Y * 16 + 8), held);
                    if (scope.Events.Count > before) cursor++;
                    return scope.Goals.All(goal => goal.Complete);
                });
                scope.Report("mixed walls/platforms/chairs original input");
                Require(scope.Events.Count == automatic.Length && scope.Events.Zip(automatic,
                    (native, actual) => native.Type == actual.Type && native.Count == actual.Count && native.ChangedGoals.SequenceEqual(actual.ChangedGoals)).All(same => same),
                    "same initial mixed world/inventory and legal original input reproduce all three consumers' exact goals and item costs");
                for (int i = 1; i < automatic.Length; i++)
                    Require(unchecked(automatic[i].World - automatic[i - 1].World) == unchecked(scope.Events[i].World - scope.Events[i - 1].World),
                        "each mixed competition handoff, including a real material change, matches its original input interval");
            }
        }

        internal static void BusinessPreparedForeign(object context, object host, object tools, object input)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                var p = Reset(context, host, tools, input);
                // The right side is exactly on vanilla's Ceiling boundary.
                // A real 0.08px right step admits the next tile; no teleport or
                // artificial timer is needed after foreign preparation exists.
                p.position.X = 44 * 16 - p.width; p.velocity = Vector2.Zero;
                var stone = BusinessStock(p, 12, ItemID.StoneBlock, 6);
                var dirt = new BusinessSource { Slot = 13, Type = ItemID.DirtBlock, Item = p.inventory[13] };
                int left, top, right, bottom;
                TileReachCheckSettings.Simple.GetTileRegion(p, out left, out top, out right, out bottom, stone.Item.tileBoost + p.blockRange);
                var first = BusinessTileGoal(40, 42, stone); var entered = BusinessTileGoal(right + 1, 42, stone);
                var foreign = new BusinessGoal { X = 45, Y = 42, Type = ItemID.DirtBlock, TileType = TileID.Dirt };
                scope.Sources = new[] { stone, dirt }; scope.Goals = new[] { first, entered, foreign };
                var stoneAsset = BusinessTileAsset(host, stone);
                Place(context, host, input, stoneAsset, first.X, first.Y); Place(context, host, input, stoneAsset, entered.X, entered.Y);
                Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Dirt }), foreign.X, foreign.Y);
                Require(scope.Runnable(first) && !scope.Runnable(entered), "the current Stone begins with one legal goal and a known same-source goal just outside exact native reach");
                scope.Begin(true); SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Events.Count != 0; });
                Require(scope.Events.Count == 1 && first.Complete, "the initial supplied group is established by a real Stone consumer");
                // A real inventory refill makes the already defined foreign
                // requirement legal. It changes no destination or source proof.
                var refill=new Item();refill.SetDefaults(ItemID.DirtBlock);refill.stack=4;
                Require(p.GetItem(refill,new GetItemSettings(NoText:true,NoSound:true)).IsAir,"original GetItem accepts the four real competing Dirt items");
                dirt.Slot=Enumerable.Range(0,50).Single(slot=>p.inventory[slot].type==ItemID.DirtBlock && p.inventory[slot].stack==4);
                dirt.Item=p.inventory[dirt.Slot];
                object prepared = null;
                NativeQuickItemChecks.Until(() =>
                {
                    scope.Step(); prepared = GetOptional(host, "preparedBuild");
                    return prepared != null && ReferenceEquals(Get(Get(prepared, "Source"), "Item"), dirt.Item);
                });
                long foreignFlight = (long)Get(prepared, "Generation"); float beforeMove = p.position.X;
                Require(scope.Events.Count == 1 && scope.Runnable(foreign) && !scope.Runnable(entered),
                    "a genuine supplied reachable foreign flight is already prepared while the current group's next goal is still outside reach");
                scope.Step(direction: 1);
                Require(p.position.X > beforeMove && (scope.Runnable(entered) || entered.Complete), "original controls and triggers move the player across the actual native reach boundary");
                NativeQuickItemChecks.Until(() => { scope.Step(); return Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"); });
                scope.Report("prepared foreign then native entered near");
                Require(scope.Events.Count == 3 && scope.Events[0].Type == stone.Type && scope.Events[1].Type == stone.Type &&
                    scope.Events[1].ChangedGoals.Contains(1) && scope.Events[2].Type == dirt.Type && stone.Item.stack == 4 && dirt.Item.stack == 3,
                    "the newly reachable known same-group goal actually consumes before the already prepared foreign candidate, with exact remaining stock");
                Console.WriteLine("BlueprintBusiness preparedForeignFlight=" + foreignFlight + " beforeX=" + beforeMove + " afterX=" + p.position.X +
                    " entered=" + entered.X + "," + entered.Y + " actualOrder=" + string.Join(",", scope.Events.Select(entry => entry.Type)));
            }
        }

        internal static void BusinessMixedSourceGroups(object context, object host, object tools, object input)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                var stone = BusinessStock(p, 12, ItemID.StoneBlock, 8); var wall = BusinessStock(p, 13, ItemID.StoneWall, 8);
                scope.Sources = new[] { stone, wall };
                var stones = new[] { 40, 41, 44, 45, 46, 47 }.Select(x => BusinessTileGoal(x, 42, stone)).ToArray();
                var walls = new[] { new Point(40, 38), new Point(42, 38), new Point(44, 38), new Point(46, 38), new Point(40, 40), new Point(46, 40) }
                    .Select(point => BusinessWallGoal(point.X, point.Y, wall)).ToArray();
                scope.Goals = Enumerable.Range(0, 6).SelectMany(index => new[] { stones[index], walls[index] }).ToArray();
                var stoneAsset = BusinessTileAsset(host, stone); var wallAsset = BusinessWallAsset(host, wall);
                foreach (var goal in walls) WorldGen.PlaceWall(goal.X, goal.Y == 38 ? 39 : goal.Y - 1, WallID.Stone, true);
                foreach (var goal in scope.Goals) Place(context, host, input, goal.WallType != 0 ? wallAsset : stoneAsset, goal.X, goal.Y);
                Require(Scene(host).Length == 12 && scope.Goals.All(scope.Runnable), "six goals of each competing supplied material occupy twelve independently legal nearby source instances");
                scope.Begin(true); SetAutomatic(host, input, true);
                // Startup only: reproduce the existing full-cache fixture's
                // ordinary request preparation, without seeding any source,
                // cursor or receipt. The measured interval below contains one
                // real Player.Update for every world epoch.
                NativeQuickItemChecks.Until(() => { scope.Step(preparationOnly: true); return GetOptional(host, "preparedBuild") != null &&
                    (int)Get(Get(host, "buildSources"), "Count") == 4; });
                var retained = ((IEnumerable)Get(host, "buildSources")).Cast<object>().Select(value => Get(value, "Owner")).ToArray();
                Require(scope.Events.Count == 0 && retained.Length == 4 && stone.Item.stack == 8 && wall.Item.stack == 8,
                    "four actual source transports are retained before the first native business event, with neither group preselected by the oracle");
                NativeQuickItemChecks.Until(() => { scope.Step(); return Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"); });
                scope.Report("mixed six-per-group with four retained sources");
                Require(scope.Goals.All(goal => goal.Complete) && scope.Events.Sum(entry => entry.Count) == 12 && stone.Item.stack == 2 && wall.Item.stack == 2,
                    "both real groups finish all twelve native goals without extra or duplicated material consumption");
                int firstType = scope.Events[0].Type;
                var firstGroup = scope.Events.TakeWhile(entry => entry.Type == firstType).ToArray();
                Require(firstGroup.Sum(entry => entry.Count) == 6 && firstGroup.Select(entry => entry.Owner).Distinct().Count() == 6 &&
                    firstGroup.Select(entry => entry.Owner).Distinct().Count(owner => !retained.Contains(owner)) >= 2,
                    "whichever material starts actually visits six distinct sources, including at least its fifth/sixth sources outside the four retained transports, before legal competing material service");
                for (int index = 1; index < firstGroup.Length; index++)
                    Require(unchecked(firstGroup[index].World - firstGroup[index - 1].World) == firstGroup[index - 1].Time,
                        "the continuous first group, including cache-external sources, serves each preceding original consumer itemTime opportunity");
            }
        }
    }
}
