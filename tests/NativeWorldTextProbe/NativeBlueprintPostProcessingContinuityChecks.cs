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
        // The goal table is fixture input, independent of the production focus,
        // preferred cursor and discovery result. Only native consumers record
        // effects; no observation below grants permission or completes a goal.
        private sealed class PostProcessingGoal
        {
            internal int X, Y, Value, SupplyType;
            internal string Kind;
            internal string Group { get { return Kind == "Hammer" ? Kind : Kind + ":" + SupplyType + ":" + Value; } }
            internal bool Complete
            {
                get
                {
                    var tile = Main.tile[X, Y];
                    if (tile == null) return false;
                    bool body = Kind == "Wire" || (Kind.StartsWith("Wall", StringComparison.Ordinal) ? tile.wall == WallID.Stone : tile.active() && tile.type == TileID.Stone);
                    return body && PostProcessingValue(tile, Kind) == Value;
                }
            }
            internal CaptureCell Cell(int left, int top)
            {
                var cell = new CaptureCell { X = X - left, Y = Y - top, Layers = CaptureLayer.Tile, TileType = TileID.Stone };
                if (Kind == "Hammer") { cell.Slope = (byte)(Value & 7); cell.HalfBrick = (Value & 8) != 0; }
                if (Kind == "TilePaint") cell.TilePaint = (byte)Value;
                if (Kind == "WallPaint") { cell.Layers = CaptureLayer.Wall; cell.TileType = 0; cell.WallType = WallID.Stone; cell.WallPaint = (byte)Value; }
                if (Kind == "TileCoating") { cell.FullbrightTile = (Value & 1) != 0; cell.InvisibleTile = (Value & 2) != 0; }
                if (Kind == "WallCoating") { cell.Layers = CaptureLayer.Wall; cell.TileType = 0; cell.WallType = WallID.Stone; cell.FullbrightWall = (Value & 1) != 0; cell.InvisibleWall = (Value & 2) != 0; }
                if (Kind == "Wire") { cell.Layers = CaptureLayer.Wire; cell.TileType = 0; cell.Wires = (byte)Value; }
                return cell;
            }
        }

        private sealed class PostProcessingEvent
        {
            internal string Kind, Group, Consumer;
            internal int Frame, X, Y, Before, After, Time, ToolTime, Animation, ToolSlot, ToolType, SupplySlot, SupplyType, SupplyBefore, SupplyAfter;
            internal uint World;
            internal long Input, Host, Token, Native, Flight, PreparationClicks;
            internal Item Tool, Supply;
            internal bool PreviousRunnable, OtherRunnable, PoundRelease, Held, ExactTool, ExactSupply, TargetWasComplete;
        }

        private sealed class PostProcessingClock
        {
            internal uint World;
            internal int Time, ToolTime, Animation;
            internal bool Hammer, PoundRelease, Held, Potential;
        }

        private static PostProcessingProbe postProcessingProbe;

        private sealed class PostProcessingProbe : IDisposable
        {
            private readonly Harmony patch = new Harmony("JueMingR.Tests.BlueprintPostProcessing");
            private readonly bool priorChecks;
            private readonly Action priorTail;
            internal readonly object Context, Host, Tools, Input;
            internal readonly List<PostProcessingEvent> Events = new List<PostProcessingEvent>();
            internal readonly List<PostProcessingClock> Clocks = new List<PostProcessingClock>();
            internal readonly Dictionary<Point, int> HammerAttempts = new Dictionary<Point, int>();
            internal PostProcessingGoal[] Goals = Array.Empty<PostProcessingGoal>();
            internal Item[] Supplies = Array.Empty<Item>(), ToolItems = Array.Empty<Item>();
            internal string CurrentGroup;
            internal int Frame, NativeNeutrals;
            internal long UnknownAtBegin;
            internal bool Watching, Automatic, TraceSupply;
            private string priorSupplyTrace;

            internal PostProcessingProbe(object context, object host, object tools, object input)
            {
                Require(postProcessingProbe == null, "post-processing observations have one scoped owner");
                Context = context; Host = host; Tools = tools; Input = input;
                priorChecks = constructionChecksActive; priorTail = constructionInputTail;
                var tail = Get(host, "buildMaterialPreparation").GetType().GetMethod("NativeInputTailForChecks", F);
                Require(tail != null, "post-processing uses the existing locked original inventory UI tail");
                constructionChecksActive = true; constructionInputTail = () => tail.Invoke(null, null);
                postProcessingProbe = this;
                var self = typeof(NativeBlueprintBuildChecks);
                try
                {
                    patch.Patch(typeof(Player).GetMethod("ItemCheck_UseMiningTools_TryPoundingTile", F),
                        prefix: new HarmonyMethod(self, nameof(PostProcessingHammerBefore)), postfix: new HarmonyMethod(self, nameof(PostProcessingHammerAfter)));
                    foreach (string method in new[] { "ApplyPaint", "ApplyCoating" })
                        patch.Patch(typeof(Player).GetMethod(method, F), prefix: new HarmonyMethod(self, nameof(PostProcessingPaintBefore)),
                            postfix: new HarmonyMethod(self, nameof(PostProcessingPaintAfter)));
                    patch.Patch(typeof(Player).GetMethod("ItemCheck_UseMiningTools", F), prefix: new HarmonyMethod(self, nameof(PostProcessingMiningClock)),
                        postfix: new HarmonyMethod(self, nameof(PostProcessingMiningTail)));
                    foreach (string method in new[] { "PlaceThing_Paintbrush", "PlaceThing_PaintRoller" })
                        patch.Patch(typeof(Player).GetMethod(method, F), prefix: new HarmonyMethod(self, nameof(PostProcessingPaintClock)));
                    patch.Patch(typeof(Player).GetMethod("ItemCheck_UseWiringTools", F), prefix: new HarmonyMethod(self, nameof(PostProcessingWireBefore)),
                        postfix: new HarmonyMethod(self, nameof(PostProcessingWireAfter)));
                }
                catch { Dispose(); throw; }
            }

            internal void Begin(bool automatic)
            {
                Events.Clear(); Clocks.Clear(); HammerAttempts.Clear(); CurrentGroup = null; Frame = NativeNeutrals = 0;
                UnknownAtBegin = (long)Get(Host, "BuildUnknownResults");
                Automatic = automatic; Watching = true;
            }

            internal void Advance(Vector2? pointer = null, bool held = false, int horizontal = 0)
            {
                var p = Main.LocalPlayer;
                long checks = (long)Get(Host, "BuildCandidateChecks"), steps = (long)Get(Host, "ProjectionMatchSourceSteps");
                NativeToolExecutionChecks.Sample(Context, Input, pointer ?? new Vector2(480, 540), held);
                p.controlLeft = horizontal < 0; p.controlRight = horizontal > 0;
                PlayerInput.Triggers.Current.Left = horizontal < 0; PlayerInput.Triggers.Current.Right = horizontal > 0;
                NativeQuickItemChecks.BeginWorldStep(); p.Update(0); Call(Context, "UpdateRuntime");
                if (constructionChecksActive) constructionInputTail?.Invoke();
                if (Watching && TraceSupply)
                {
                    var focus=GetOptional(Host,"buildContinuation");var operation=focus==null?null:GetOptional(focus,"Operation");
                    var prep=Get(Host,"buildMaterialPreparation");
                    string value="focus="+(focus==null?"none":Get(focus,"Group")+":"+(operation==null?"Body":Get(operation,"Kind")+":"+Get(operation,"SupplyType")))+
                        " phase="+Get(prep,"phase")+" inventoryReady="+Get(Get(Host,"MaterialInventory"),"Ready")+
                        " selection="+Get(Tools,"SelectionIntent")+" focusSelection="+(focus==null?"none":Get(focus,"Selection").ToString())+
                        " supply="+string.Join(",",Supplies.Select(item=>item.type+"@"+Array.IndexOf(p.inventory,item)+":"+item.stack));
                    if(value!=priorSupplyTrace){Console.WriteLine("PostProcessingSupply frame="+Frame+" "+value);priorSupplyTrace=value;}
                }
                Require((long)Get(Host, "BuildCandidateChecks") - checks <= 64 && (long)Get(Host, "ProjectionMatchSourceSteps") - steps <= 256 &&
                    (int)Get(Host, "materialBuildSourceBudget") >= 0, "post-processing retains existing candidate and shared source step bounds");
                if (Watching) Require((long)Get(Host, "BuildUnknownResults") == UnknownAtBegin, "known exact native post-processing effects do not create unknown-consumption responsibility");
                Frame++;
            }

            internal bool Runnable(PostProcessingGoal goal)
            {
                var p = Main.LocalPlayer; var tile = Main.tile[goal.X, goal.Y];
                if (p.noBuilding || goal.Complete || tile == null) return false;
                bool hammer = goal.Kind == "Hammer", wall = goal.Kind.StartsWith("Wall", StringComparison.Ordinal);
                if (goal.Kind != "Wire" && (wall ? tile.wall != WallID.Stone : !tile.active() || tile.type != TileID.Stone)) return false;
                var tool = ToolItems.FirstOrDefault(item => !item.IsAir && Array.IndexOf(p.inventory, item) >= 0 &&
                    (hammer ? item.hammer > 0 && item.pick == 0 : goal.Kind == "Wire" ? item.type == (goal.Value == 2 ? ItemID.BlueWrench : ItemID.GreenWrench) :
                    wall ? item.type == ItemID.PaintRoller : item.type == ItemID.Paintbrush) &&
                    p.IsInTileInteractionRange(goal.X, goal.Y, TileReachCheckSettings.Simple, item.tileBoost + (hammer ? 0 : p.blockRange)));
                if (tool == null) return false;
                if (!hammer) return Supplies.Any(item => !item.IsAir && item.type == goal.SupplyType && item.stack > 0 && Array.IndexOf(p.inventory, item) >= 0);
                for (int y = goal.Y - 1; y <= goal.Y + 1; y++) for (int x = goal.X - 1; x <= goal.X + 1; x++) if (Main.tile[x, y] == null) return false;
                if (WorldGen.IsLockedDoor(goal.X, goal.Y - 1) || WorldGen.IsLockedDoor(goal.X, goal.Y + 1)) return false;
                var bounds = new Rectangle(goal.X * 16, goal.Y * 16, 16, 16);
                return !Main.player.Any(player => player != null && player.active && !player.dead && bounds.Intersects(player.Hitbox)) && WorldGen.CanPoundTile(goal.X, goal.Y);
            }

            internal PostProcessingEvent Before(Player p, int x, int y, string kind, Item supply)
            {
                if (!Watching || !ReferenceEquals(p, Main.LocalPlayer) || !Goals.Any(goal => goal.X == x && goal.Y == y && goal.Kind == kind)) return null;
                var flight = Automatic ? GetOptional(Host, "activeBuild") : null;
                var source = flight == null ? null : Get(flight, "Source");
                var tool = p.HeldItem;
                int value = kind == "Hammer" ? 0 : kind == "Wire" ? 0 : kind.EndsWith("Coating", StringComparison.Ordinal) ? supply.paintCoating : supply.paint;
                string group = kind == "Hammer" ? kind : kind + ":" + supply.type + ":" + value;
                return new PostProcessingEvent { Frame = Frame, World = Main.GameUpdateCount, Input = (long)Get(Input, "Frame"),
                    Host = checked((long)(ulong)Get(Host, "projectionMatchTick")), X = x, Y = y, Kind = kind, Group = group,
                    Before = PostProcessingValue(Main.tile[x, y], kind), Time = p.itemTime, ToolTime = p.toolTime, Animation = p.itemAnimation,
                    Tool = tool, ToolType = tool.type, ToolSlot = Array.IndexOf(p.inventory, tool), Supply = supply,
                    SupplySlot = supply == null ? -1 : Array.IndexOf(p.inventory, supply), SupplyType = supply == null ? 0 : supply.type,
                    SupplyBefore = supply == null ? 0 : supply.stack, PoundRelease = p.poundRelease, Held = p.controlUseItem,
                    TargetWasComplete = Goals.Single(goal => goal.X == x && goal.Y == y && goal.Kind == kind).Complete,
                    Token = Automatic ? (long)Get(Get(Tools, "Use"), "Operation") : 0,
                    Native = Automatic ? (long)Get(Get(Tools, "Use"), "NativeGeneration") : 0,
                    Flight = flight == null ? 0 : (long)Get(flight, "Generation"),
                    PreparationClicks = (long)Get(Get(Host, "buildMaterialPreparation"), "NativeClicks"),
                    ExactTool = source == null || ReferenceEquals(Get(source, "Item"), tool) && (int)Get(source, "Slot") == Array.IndexOf(p.inventory, tool),
                    ExactSupply = flight == null || supply == null || ((IEnumerable)Get(flight, "Dependencies")).Cast<object>().Any(dependency =>
                        ReferenceEquals(Get(dependency, "Item"), supply) && (int)Get(dependency, "Slot") == Array.IndexOf(p.inventory, supply)),
                    PreviousRunnable = CurrentGroup != null && Goals.Any(goal => goal.Group == CurrentGroup && Runnable(goal)),
                    OtherRunnable = Goals.Any(goal => goal.Group != group && Runnable(goal)) };
            }

            internal void After(PostProcessingEvent entry, bool ranOriginal, string consumer)
            {
                if (entry == null || !ranOriginal) return;
                entry.After = PostProcessingValue(Main.tile[entry.X, entry.Y], entry.Kind);
                if (entry.Before == entry.After) return;
                entry.Consumer = consumer; entry.SupplyAfter = entry.Supply == null ? 0 : entry.Supply.stack;
                if (entry.Kind == "Wire")
                {
                    int added = entry.After & ~entry.Before;
                    Require(added == 2 || added == 4, "wire representative adds exactly its existing blue or green native color");
                    entry.Group = "Wire:" + ItemID.Wire + ":" + added;
                }
                Require(!entry.TargetWasComplete && Goals.Any(goal => goal.X == entry.X && goal.Y == entry.Y && goal.Group == entry.Group),
                    "a real native effect belongs to the independently requested target/effect and never reworks an already correct target");
                if (Automatic && CurrentGroup != null && CurrentGroup != entry.Group && entry.PreviousRunnable)
                {
                    Report("before wrong group " + CurrentGroup + " -> " + entry.Group);
                    foreach (var owner in Scene(Host)) if (GetOptional(owner,"Match") != null) foreach (var range in EligibilityRanges(owner))
                    {
                        var value=GetOptional(range,"Materials");if(value==null)continue;
                        Console.Error.WriteLine("PostProcessing sharedNear="+NearRangeSummary(Host,range)+" waits="+Get(value,"BuildWaits"));
                    }
                }
                Require(!Automatic || CurrentGroup == null || CurrentGroup == entry.Group || !entry.PreviousRunnable,
                    "independent live world/reach/stock oracle forbids changing group while a known same-group target remains runnable: " + CurrentGroup + " -> " + entry.Group);
                Require(!Automatic || entry.Token > 0 && entry.Native > 0 && entry.Flight > 0 && entry.ExactTool && entry.ExactSupply,
                    "each real post-processing effect keeps its own exact tool, supply, native generation and use permission");
                Require(entry.Held && entry.Animation > 0 && (entry.Kind == "Hammer" ? entry.ToolTime == 0 && entry.PoundRelease : entry.Time == 0),
                    "effect enters through its corresponding natural native clock and input condition");
                Require(entry.Kind == "Hammer" ? entry.Supply == null && entry.Tool.stack == 1 :
                    entry.SupplyBefore - entry.SupplyAfter == 1 && Supplies.Any(item => ReferenceEquals(item, entry.Supply)),
                    "shape is a zero-material world event; each paint/coating/wire event debits its exact approved original Item once");
                CurrentGroup = entry.Group; Events.Add(entry);
            }

            internal void Report(string name)
            {
                Console.WriteLine("BlueprintPostProcessing " + name + " events=world/input/host/frame/xy/kind/before-after/tool-slot/supply-slot:before-after/token-native-flight/clicks " +
                    string.Join(";", Events.Select(entry => entry.World + "/" + entry.Input + "/" + entry.Host + "/" + entry.Frame + "/" + entry.X + "," + entry.Y + "/" +
                        entry.Group + "/" + entry.Before + "-" + entry.After + "/" + entry.ToolType + "@" + entry.ToolSlot + "/" + entry.SupplyType + "@" + entry.SupplySlot + ":" +
                        entry.SupplyBefore + "-" + entry.SupplyAfter + "/" + entry.Token + "-" + entry.Native + "-" + entry.Flight + "/" + entry.PreparationClicks)) +
                    " nativeNeutralTransitions=" + NativeNeutrals + " nativeHammerAttempts=" + string.Join(",", HammerAttempts.Select(pair => pair.Key + ":" + pair.Value)));
            }

            public void Dispose()
            {
                Watching = false;
                try
                {
                    SetAutomatic(Host, Input, false);
                    for (int frame = 0; frame < 160 && ((bool)Get(Get(Tools, "Use"), "Active") || (bool)Get(Get(Host, "buildMaterialPreparation"), "HasWork")); frame++) Advance();
                }
                finally
                {
                    foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                    postProcessingProbe = null; constructionChecksActive = priorChecks; constructionInputTail = priorTail;
                }
            }
        }

        private static int PostProcessingValue(Tile tile, string kind)
        {
            if (kind == "Hammer") return tile.slope() | (tile.halfBrick() ? 8 : 0);
            if (kind == "TilePaint") return tile.color();
            if (kind == "WallPaint") return tile.wallColor();
            if (kind == "TileCoating") return (tile.fullbrightBlock() ? 1 : 0) | (tile.invisibleBlock() ? 2 : 0);
            if (kind == "WallCoating") return (tile.fullbrightWall() ? 1 : 0) | (tile.invisibleWall() ? 2 : 0);
            return (tile.wire() ? 1 : 0) | (tile.wire2() ? 2 : 0) | (tile.wire3() ? 4 : 0) | (tile.wire4() ? 8 : 0);
        }

        private static void PostProcessingHammerBefore(Player __instance, int __3, int __4, out PostProcessingEvent __state)
        { __state = postProcessingProbe?.Before(__instance, __3, __4, "Hammer", null); }
        private static void PostProcessingHammerAfter(bool __runOriginal, PostProcessingEvent __state)
        {
            var probe = postProcessingProbe;
            if (probe == null) return;
            if (__runOriginal && __state != null && __state.PoundRelease && __state.Held && __state.Tool.hammer > 0)
            {
                int count; var point = new Point(__state.X, __state.Y);
                probe.HammerAttempts[point] = probe.HammerAttempts.TryGetValue(point, out count) ? count + 1 : 1;
            }
            probe.After(__state, __runOriginal, "ItemCheck_UseMiningTools_TryPoundingTile");
        }
        private static void PostProcessingPaintBefore(Player __instance, int __0, int __1, bool __2, Item __4, MethodBase __originalMethod, out PostProcessingEvent __state)
        { __state = postProcessingProbe?.Before(__instance, __0, __1, (__2 ? "Wall" : "Tile") + (__originalMethod.Name == "ApplyCoating" ? "Coating" : "Paint"), __4); }
        private static void PostProcessingPaintAfter(bool __runOriginal, MethodBase __originalMethod, PostProcessingEvent __state)
        { postProcessingProbe?.After(__state, __runOriginal, __originalMethod.Name); }
        private static void PostProcessingWireBefore(Player __instance, out PostProcessingEvent __state)
        {
            var wire = __instance.inventory.Take(58).FirstOrDefault(item => !item.IsAir && item.type == ItemID.Wire && item.stack > 0);
            __state = wire == null ? null : postProcessingProbe?.Before(__instance, Player.tileTargetX, Player.tileTargetY, "Wire", wire);
        }
        private static void PostProcessingWireAfter(bool __runOriginal, PostProcessingEvent __state)
        { postProcessingProbe?.After(__state, __runOriginal, "ItemCheck_UseWiringTools"); }
        private static void PostProcessingMiningClock(Player __instance, out bool __state)
        {
            __state = __instance.poundRelease;
            PostProcessingObserveClock(__instance, true);
        }
        private static void PostProcessingMiningTail(Player __instance, bool __state, bool __runOriginal)
        {
            var probe = postProcessingProbe;
            if (probe != null && probe.Watching && __runOriginal && ReferenceEquals(__instance, Main.LocalPlayer) && !__state &&
                __instance.poundRelease && !__instance.controlUseItem && __instance.releaseUseItem) probe.NativeNeutrals++;
        }
        private static void PostProcessingPaintClock(Player __instance) { PostProcessingObserveClock(__instance, false); }
        private static void PostProcessingObserveClock(Player p, bool hammer)
        {
            var probe = postProcessingProbe;
            if (probe == null || !probe.Watching || !ReferenceEquals(p, Main.LocalPlayer) ||
                !probe.ToolItems.Any(item => ReferenceEquals(item, p.HeldItem)) || (hammer ? p.HeldItem.hammer <= 0 :
                p.HeldItem.type != ItemID.Paintbrush && p.HeldItem.type != ItemID.PaintRoller)) return;
            // This is the real consumer's clock, after the ordinary ItemCheck
            // timer update. A neutral needed to restore poundRelease is not a
            // missed hammer opportunity. No fixture writes these clocks.
            probe.Clocks.Add(new PostProcessingClock { World = Main.GameUpdateCount, Time = p.itemTime, ToolTime = p.toolTime,
                Animation = p.itemAnimation, Hammer = hammer, PoundRelease = p.poundRelease, Held = p.controlUseItem,
                Potential = p.reuseDelay == 0 && (p.itemAnimation > 0 || hammer && p.releaseUseItem) &&
                    (hammer ? p.toolTime == 0 && p.poundRelease : p.ItemTimeIsZero) && probe.Goals.Any(probe.Runnable) });
        }

        private static PostProcessingGoal PostProcessingTarget(string kind, int x, int y, int value, int supply = 0)
        { return new PostProcessingGoal { Kind = kind, X = x, Y = y, Value = value, SupplyType = supply }; }
        private static void PostProcessingSeedShape(int x, int y, int shape)
        {
            NativeToolsChecks.Tile(x, y, TileID.Stone);
            if ((shape & 8) != 0) Require(WorldGen.PoundTile(x, y), "initial half brick is a real native setup shape");
            else if (shape != 0) Require(WorldGen.SlopeTile(x, y, shape), "initial slope is a real native setup shape");
        }
        private static void PostProcessingPlace(object context, object host, object input, PostProcessingGoal[] goals)
        {
            int left = goals.Min(goal => goal.X), top = goals.Min(goal => goal.Y);
            // Several requested effects may belong to one actual tile. Merge
            // only fixture cells, before any automatic permission is enabled.
            var cells = goals.GroupBy(goal => new Point(goal.X, goal.Y)).Select(group =>
            {
                var result = group.First().Cell(left, top);
                foreach (var goal in group.Skip(1))
                {
                    var next = goal.Cell(left, top); result.Layers |= next.Layers;
                    if (goal.Kind == "TilePaint") result.TilePaint = next.TilePaint;
                    if (goal.Kind == "WallPaint") { result.WallType = next.WallType; result.WallPaint = next.WallPaint; }
                    if (goal.Kind == "TileCoating") { result.FullbrightTile = next.FullbrightTile; result.InvisibleTile = next.InvisibleTile; }
                    if (goal.Kind == "WallCoating") { result.WallType = next.WallType; result.FullbrightWall = next.FullbrightWall; result.InvisibleWall = next.InvisibleWall; }
                }
                return result;
            }).OrderBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray();
            Place(context, host, input, Asset(host, cells), left, top);
        }

        internal static void PostProcessingHammerGroups(object context, object host, object tools, object input)
        {
            PostProcessingHammerContinuity(context, host, tools, input, false);
            PostProcessingHammerContinuity(context, host, tools, input, true);
        }

        private static void PostProcessingHammerContinuity(object context, object host, object tools, object input, bool moving)
        {
            using (var probe = new PostProcessingProbe(context, host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                p.inventory[12].SetDefaults(ItemID.WoodenHammer); var hammer = p.inventory[12];
                p.inventory[14].SetDefaults(ItemID.Paintbrush); var brush = p.inventory[14];
                p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 4; var paint = p.inventory[20];
                int y = moving ? 38 : 42;
                var goals = moving ? new[] {
                    PostProcessingTarget("Hammer", 40, y, 8), PostProcessingTarget("Hammer", 44, y, 3),
                    PostProcessingTarget("Hammer", 45, y, 4), PostProcessingTarget("Hammer", 46, y, 8),
                    PostProcessingTarget("Hammer", 51, y, 3), PostProcessingTarget("TilePaint", 47, y, paint.paint, ItemID.RedPaint) } : new[] {
                    PostProcessingTarget("Hammer", 40, y, 1), PostProcessingTarget("Hammer", 44, y, 3),
                    PostProcessingTarget("Hammer", 45, y, 8), PostProcessingTarget("Hammer", 46, y, 4),
                    PostProcessingTarget("TilePaint", 47, y, paint.paint, ItemID.RedPaint) };
                var starts = moving ? new[] { 0, 0, 8, 1, 0 } : new[] { 0, 8, 1, 2 };
                var shapes = goals.Where(goal => goal.Kind == "Hammer").ToArray();
                for (int index = 0; index < shapes.Length; index++) PostProcessingSeedShape(shapes[index].X, y, starts[index]);
                NativeToolsChecks.Tile(47, y, TileID.Stone);
                probe.Goals = goals; probe.ToolItems = new[] { hammer, brush }; probe.Supplies = new[] { paint };
                var remote = Main.player[1];
                try
                {
                    if (moving)
                    {
                        Main.player[1] = new Player { whoAmI = 1, active = true, position = new Vector2(40 * 16, y * 16) };
                        Require(!probe.Runnable(goals[0]) && !probe.Runnable(shapes.Last()) &&
                            goals.Where(goal => !ReferenceEquals(goal, goals[0]) && !ReferenceEquals(goal, shapes.Last())).All(probe.Runnable),
                            "one real occupant blocks its goal, the future hammer goal begins outside reach, and the other shape/paint goals are legal");
                    }
                    else Require(goals.All(probe.Runnable), "every stationary shape and competing paint goal is independently legal before selection");
                    PostProcessingPlace(context, host, input, goals); probe.Advance(); probe.Begin(true); SetAutomatic(host, input, true);
                    bool walking = false;
                    for (int frame = 0; frame < 2200; frame++)
                    {
                        if (moving && probe.Events.Any(entry => entry.Kind == "Hammer")) walking = true;
                        probe.Advance(horizontal: moving && walking && p.position.X < 46 * 16 ? 1 : 0);
                        if (goals.Where(goal => !moving || !ReferenceEquals(goal, goals[0])).All(goal => goal.Complete) && !(bool)Get(Get(tools, "Use"), "Active")) break;
                    }
                    probe.Report("hammer moving=" + moving);
                    Console.WriteLine("BlueprintPostProcessing hammer position=" + p.position + " velocity=" + p.velocity +
                        " futureReach=" + p.IsInTileInteractionRange(shapes.Last().X, y, TileReachCheckSettings.Simple, hammer.tileBoost));
                    Require(goals.Where(goal => !moving || !ReferenceEquals(goal, goals[0])).All(goal => goal.Complete) &&
                        (moving ? !goals[0].Complete && p.position.X >= 46 * 16 : Scene(host).Length == 0), "all legal different-shape goals complete; a real blocked goal remains untouched without locking the hammer group");
                    Require(probe.Events.Count(entry => entry.Kind == "Hammer") > shapes.Length && probe.Events.Any(entry => entry.Kind == "Hammer" && entry.OtherRunnable) &&
                        probe.NativeNeutrals > 0 && hammer.type == ItemID.WoodenHammer && hammer.stack == 1 && paint.stack == 3,
                        "multiple real zero-material shape changes and genuine neutral phases continue while another legal business group exists");
                    if (moving) Require(probe.Events.Any(entry => entry.Kind == "Hammer" && entry.X == 51) &&
                        !probe.HammerAttempts.ContainsKey(new Point(40, y)), "actual native right movement admits a new same-group goal; the occupied goal receives no pounding attempt");
                    int effects = probe.Events.Count, attempts = probe.HammerAttempts.Values.Sum();
                    for (int frame = 0; frame < 80; frame++) probe.Advance();
                    Require(probe.Events.Count == effects && probe.HammerAttempts.Values.Sum() == attempts,
                        "correct shapes and the persistent blocker create no later native pounding attempt or world effect");
                }
                finally { Main.player[1] = remote; }
            }
        }

        private static void PostProcessingPaintContinuity(object context, object host, object tools, object input)
        {
            using (var probe = new PostProcessingProbe(context, host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                p.inventory[12].SetDefaults(ItemID.Paintbrush); var brush = p.inventory[12];
                p.inventory[13].SetDefaults(ItemID.PaintRoller); var roller = p.inventory[13];
                p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 5; var nextRed = p.inventory[20];
                p.inventory[21].SetDefaults(ItemID.RedPaint); p.inventory[21].stack = 2; var firstRed = p.inventory[21];
                p.inventory[22].SetDefaults(ItemID.BluePaint); p.inventory[22].stack = 4; var blue = p.inventory[22];
                p.inventory[23].SetDefaults(ItemID.GlowPaint); p.inventory[23].stack = 3; var glow = p.inventory[23];
                p.inventory[24].SetDefaults(ItemID.EchoCoating); p.inventory[24].stack = 3; var echo = p.inventory[24];
                p.inventory[54].SetDefaults(ItemID.BluePaint); p.inventory[54].stack = 7; var priority = p.inventory[54];
                var goals = new[] {
                    PostProcessingTarget("TilePaint", 40, 42, nextRed.paint, ItemID.RedPaint), PostProcessingTarget("TilePaint", 41, 42, blue.paint, ItemID.BluePaint),
                    PostProcessingTarget("TilePaint", 44, 42, nextRed.paint, ItemID.RedPaint), PostProcessingTarget("TilePaint", 45, 42, blue.paint, ItemID.BluePaint),
                    PostProcessingTarget("TilePaint", 46, 42, nextRed.paint, ItemID.RedPaint),
                    PostProcessingTarget("WallPaint", 40, 39, nextRed.paint, ItemID.RedPaint), PostProcessingTarget("WallPaint", 44, 39, nextRed.paint, ItemID.RedPaint),
                    PostProcessingTarget("WallPaint", 46, 39, nextRed.paint, ItemID.RedPaint),
                    PostProcessingTarget("TileCoating", 40, 40, 1, ItemID.GlowPaint), PostProcessingTarget("TileCoating", 46, 40, 1, ItemID.GlowPaint),
                    PostProcessingTarget("WallCoating", 41, 39, 2, ItemID.EchoCoating), PostProcessingTarget("WallCoating", 45, 39, 2, ItemID.EchoCoating) };
                foreach (var goal in goals)
                    if (goal.Kind.StartsWith("Wall", StringComparison.Ordinal)) WorldGen.PlaceWall(goal.X, goal.Y, WallID.Stone);
                    else NativeToolsChecks.Tile(goal.X, goal.Y, TileID.Stone);
                probe.Goals = goals; probe.ToolItems = new[] { brush, roller }; probe.Supplies = new[] { nextRed, firstRed, blue, glow, echo }; probe.TraceSupply = true;
                Require(goals.All(probe.Runnable), "mixed foreground colors, same-color walls and both coating effects are independently supplied and reachable");
                PostProcessingPlace(context, host, input, goals); probe.Advance(); probe.Begin(true); SetAutomatic(host, input, true);
                for (int frame = 0; frame < 2400; frame++)
                {
                    probe.Advance();
                    if (Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active") && ReferenceEquals(p.inventory[54], priority) && Main.mouseItem.IsAir) break;
                }
                probe.Report("mixed paint/coatings and exact two-stack return");
                Require(goals.All(goal => goal.Complete) && Scene(host).Length == 0 && probe.Events.Count == goals.Length &&
                    new[] { nextRed, firstRed }.Where(item => !item.IsAir).Sum(item => item.stack) == 1 &&
                    (nextRed.IsAir || firstRed.IsAir) && blue.stack == 2 && glow.stack == 1 && echo.stack == 1,
                    "every requested layer/effect has one real native result and exact total cost; the exhausted red Item stays Air");
                Require((nextRed.IsAir || ReferenceEquals(p.inventory[20], nextRed)) && (firstRed.IsAir || ReferenceEquals(p.inventory[21], firstRed)) &&
                    ReferenceEquals(p.inventory[22], blue) && ReferenceEquals(p.inventory[23], glow) &&
                    ReferenceEquals(p.inventory[24], echo) && ReferenceEquals(p.inventory[54], priority) && priority.stack == 7 &&
                    brush.stack == 1 && roller.stack == 1 && Main.mouseItem.IsAir, "native preparation returns each remaining exact source and the complete unrelated original54 Item");
                Require(probe.Events.GroupBy(entry => entry.Group).Any(group => group.Select(entry => entry.Supply).Distinct().Count() == 2),
                    "one continuous same-color business group crosses an actual exact Item stack boundary");
                for (int index = 1; index < probe.Events.Count; index++)
                {
                    var previous = probe.Events[index - 1]; var current = probe.Events[index];
                    if (previous.Group == current.Group && ReferenceEquals(previous.Supply, current.Supply) && ReferenceEquals(previous.Tool, current.Tool))
                        Require(previous.PreparationClicks == current.PreparationClicks, "a stable same-effect exact source has no per-target return/reprepare clicks");
                }
                Require(probe.Events.All(entry => !ReferenceEquals(entry.Supply, priority)), "displaced original54 is never adopted as blueprint supply");
            }
        }

        private static void PostProcessingWireContinuity(object context, object host, object tools, object input)
        {
            using (var probe = new PostProcessingProbe(context, host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                p.inventory[12].SetDefaults(ItemID.BlueWrench); var blue = p.inventory[12];
                p.inventory[13].SetDefaults(ItemID.GreenWrench); var green = p.inventory[13];
                p.inventory[20].SetDefaults(ItemID.Wire); p.inventory[20].stack = 6; var wire = p.inventory[20];
                var goals = new[] { PostProcessingTarget("Wire", 40, 42, 2, ItemID.Wire), PostProcessingTarget("Wire", 41, 42, 4, ItemID.Wire),
                    PostProcessingTarget("Wire", 44, 42, 2, ItemID.Wire), PostProcessingTarget("Wire", 45, 42, 4, ItemID.Wire) };
                probe.Goals = goals; probe.ToolItems = new[] { blue, green }; probe.Supplies = new[] { wire };
                Require(goals.All(probe.Runnable), "interleaved existing blue and green wire goals are legal before choosing the first color");
                PostProcessingPlace(context, host, input, goals); probe.Advance(); probe.Begin(true); SetAutomatic(host, input, true);
                for (int frame = 0; frame < 1200 && (Scene(host).Length != 0 || (bool)Get(Get(tools, "Use"), "Active")); frame++) probe.Advance();
                probe.Report("existing blue/green wire consumers");
                Require(goals.All(goal => goal.Complete) && Scene(host).Length == 0 && probe.Events.Count == 4 && wire.stack == 2 &&
                    probe.Events.All(entry => ReferenceEquals(entry.Supply, wire)) && blue.stack == 1 && green.stack == 1,
                    "same-color native wiring continues across alternating source positions, spends four exact wires and preserves both real tools");
            }
        }
    }
}
