using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        internal static void ConstructionOperationGroups(object context, object host, object tools, object input)
        {
            ConstructionConsumerCase("same platform material crosses Smart/Precise segments", () => OperationGroupPlatformModes(context, host, tools, input));
            ConstructionConsumerCase("same tile-wand recipe precedes a legal ordinary competitor", () => OperationGroupTileWand(context, host, tools, input));
            // Preserve the existing real installation -> rod -> owned cleanup
            // regression without introducing another actuator protocol fixture.
            ConstructionConsumerCase("existing single temporary actuator responsibility", () => ConstructionActuator(context, host, tools, input, false));
            Console.WriteLine("PASS BlueprintOperationGroups: native platform input-mode boundary, exact source leases and neutral Lookup, repeated tile-wand recipe with real competing material, existing finite temporary actuator responsibility; GPU/FPS/sockets/owner acceptance NOT_RUN");
        }

        private sealed class OperationGroupGoal
        {
            internal int X, Y, Type;
            internal byte Slope;
            internal string Group;
            internal bool Complete;
        }

        private sealed class OperationGroupChoice
        {
            internal long Frame, Token, UseToken, Flight;
            internal uint World;
            internal int X, Y, Slot, Type, PrimarySlot, PrimaryType;
            internal string Mode, Producer;
            internal Item Source, Primary;
            internal bool SourceLeased, PrimaryLeased, SourceCurrent, PrimaryCurrent;
        }

        private sealed class OperationGroupLookup
        {
            internal long Frame, Token, Flight;
            internal uint World;
            internal int X, Y, DirectionX, DirectionY;
            internal string Mode;
            internal bool Held, ExactSmart;
        }

        private sealed class OperationGroupEvent
        {
            internal OperationGroupGoal Goal;
            internal OperationGroupChoice Choice;
            internal int Debit;
            internal uint World;
        }

        private static OperationGroupProbe operationGroupProbe;

        // All patches below observe successful original choices/Lookups. The
        // order oracle uses the fixed fixture goals, vanilla reach/support and
        // actual stock/world results, never production focus or search status.
        private sealed class OperationGroupProbe : IDisposable
        {
            internal readonly object Context, Host, Tools, Input;
            internal readonly List<OperationGroupChoice> Choices = new List<OperationGroupChoice>();
            internal readonly List<OperationGroupLookup> Lookups = new List<OperationGroupLookup>();
            internal readonly List<OperationGroupEvent> Events = new List<OperationGroupEvent>();
            internal OperationGroupGoal[] Goals;
            internal Func<OperationGroupGoal, bool> Runnable;
            internal string CurrentGroup;
            internal Action<OperationGroupEvent> AfterEvent;
            private readonly Harmony patch;

            internal OperationGroupProbe(object context, object host, object tools, object input)
            {
                Context = context; Host = host; Tools = tools; Input = input;
                Require(operationGroupProbe == null, "one bounded native operation-group observer owns this test leaf");
                operationGroupProbe = this; patch = new Harmony("JueMingR.Tests.BlueprintOperationGroups");
                patch.Patch(host.GetType().GetMethod("NativeBuildChoice", F),
                    postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(OperationGroupChoiceAfter)));
                patch.Patch(typeof(Terraria.GameContent.SmartCursorHelper).GetMethod("SmartCursorLookup", F),
                    postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(OperationGroupLookupAfter)));
            }

            internal void Advance()
            {
                var p = Main.LocalPlayer;
                var before = Goals.Select(goal => Main.tile[goal.X, goal.Y].active()).ToArray();
                int platforms = OperationGroupStock(p, ItemID.WoodPlatform), wood = OperationGroupStock(p, ItemID.Wood), stone = OperationGroupStock(p, ItemID.StoneBlock);
                NativeToolExecutionChecks.Sample(Context, Input, new Vector2(480, 540), false);
                NativeQuickItemChecks.BeginWorldStep(); p.Update(0);
                int platformDebit = platforms - OperationGroupStock(p, ItemID.WoodPlatform);
                int woodDebit = wood - OperationGroupStock(p, ItemID.Wood), stoneDebit = stone - OperationGroupStock(p, ItemID.StoneBlock);
                int platformEffects = 0, wandEffects = 0, stoneEffects = 0;
                for (int index = 0; index < Goals.Length; index++)
                {
                    var goal = Goals[index]; var tile = Main.tile[goal.X, goal.Y];
                    if (before[index] || !tile.active()) continue;
                    Require(tile.type == goal.Type && tile.slope() == goal.Slope && !tile.halfBrick(),
                        "a real original placement produces the exact independently specified body/shape at " + goal.X + "," + goal.Y);
                    if (CurrentGroup == null) CurrentGroup = goal.Group;
                    if (goal.Group != CurrentGroup)
                        Require(!Goals.Any(other => other.Group == CurrentGroup && !other.Complete && Runnable(other)),
                            "a genuinely supplied/reachable earlier group has no remaining legal goal before another group performs a world write");
                    var choice = Choices.LastOrDefault(value => value.World == Main.GameUpdateCount && value.X == goal.X && value.Y == goal.Y);
                    Require(choice != null && choice.Frame == (long)Get(Input, "Frame") && choice.Token > 0 && choice.Token == choice.UseToken &&
                        choice.SourceLeased && choice.SourceCurrent && (choice.Primary == null || choice.PrimaryLeased && choice.PrimaryCurrent),
                        "each actual result has its own same-update admitted source/primary identities under the exact real ToolUse token");
                    int debit = goal.Group == "Platform" ? platformDebit : goal.Group == "TileWand" ? woodDebit : stoneDebit;
                    var entry = new OperationGroupEvent { Goal = goal, Choice = choice, Debit = debit, World = Main.GameUpdateCount };
                    Events.Add(entry); goal.Complete = true;
                    if (goal.Group == "Platform") platformEffects++; else if (goal.Group == "TileWand") wandEffects++; else stoneEffects++;
                    Console.WriteLine("OPERATION_GROUP event=" + goal.Group + " world=" + entry.World + " input=" + choice.Frame + " target=" + goal.X + "," + goal.Y +
                        " mode=" + choice.Mode + " token=" + choice.Token + " flight=" + choice.Flight + " source=" + choice.Slot + ":" + choice.Type +
                        " primary=" + choice.PrimarySlot + ":" + choice.PrimaryType + " debit=" + debit);
                    AfterEvent?.Invoke(entry);
                }
                Require(platformDebit == platformEffects && woodDebit == wandEffects && stoneDebit == stoneEffects,
                    "each real native body write pairs with exactly one real recipe debit, and no unpaired consumption is hidden");
                Call(Context, "UpdateRuntime");
                if (constructionChecksActive) constructionInputTail?.Invoke();
            }

            internal void Until(Func<bool> condition)
            { NativeQuickItemChecks.Until(() => { Advance(); return condition(); }); }

            public void Dispose()
            {
                Call(Host, "SetAutoBuild", false); operationGroupProbe = null;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                for (int frame = 0; frame < 80 && ((bool)Get(Get(Tools, "Use"), "Active") ||
                    (bool)Get(Get(Host, "buildMaterialPreparation"), "HasWork")); frame++) Step(Context, Input);
            }
        }

        private static void OperationGroupChoiceAfter(object __0, bool __result)
        {
            var probe = operationGroupProbe; if (probe == null || !__result || probe.Goals == null) return;
            var target = Get(__0, "Target"); int x = (int)Get(target, "WorldX"), y = (int)Get(target, "WorldY");
            if (!probe.Goals.Any(goal => goal.X == x && goal.Y == y)) return;
            var source = Get(__0, "Source"); var item = (Item)Get(source, "Item");
            var primary = ((Array)Get(__0, "Dependencies")).Cast<object>().FirstOrDefault(value => (bool)Get(value, "Primary"));
            var material = primary == null ? null : (Item)Get(primary, "Item");
            var operation = GetOptional(__0, "Operation"); var placement = operation == null ? null : GetOptional(operation, "NativePlacement");
            var producer = placement == null ? null : GetOptional(placement, "Producer"); var use = Get(probe.Tools, "Use"); var p = Main.LocalPlayer;
            int slot = (int)Get(source, "Slot"), primarySlot = primary == null ? -1 : (int)Get(primary, "Slot");
            probe.Choices.Add(new OperationGroupChoice { Frame = (long)Get(probe.Input, "Frame"), World = Main.GameUpdateCount,
                Token = (long)Get(__0, "Token"), UseToken = (long)Get(use, "Operation"), Flight = (long)Get(__0, "Generation"),
                X = x, Y = y, Mode = Get(__0, "CursorMode").ToString(), Source = item, Slot = slot, Type = (int)Get(source, "Type"),
                Primary = material, PrimarySlot = primarySlot, PrimaryType = primary == null ? 0 : (int)Get(primary, "Type"),
                Producer = producer == null ? "ordinary" : Get(producer, "Kind").ToString(),
                SourceCurrent = slot >= 0 && slot < 50 && ReferenceEquals(p.inventory[slot], item) && ReferenceEquals(p.HeldItem, item),
                PrimaryCurrent = material == null || primarySlot >= 0 && primarySlot < p.inventory.Length && ReferenceEquals(p.inventory[primarySlot], material),
                SourceLeased = (bool)Call(use, "OwnsBuildMaterial", item), PrimaryLeased = material == null || (bool)Call(use, "OwnsBuildMaterial", material) });
        }

        private static void OperationGroupLookupAfter(bool __runOriginal)
        {
            var probe = operationGroupProbe; if (probe == null || !__runOriginal || probe.Goals == null) return;
            var flight = GetOptional(probe.Host, "activeBuild"); if (flight == null) return;
            var target = Get(flight, "Target"); int x = (int)Get(target, "WorldX"), y = (int)Get(target, "WorldY");
            if (!probe.Goals.Any(goal => goal.X == x && goal.Y == y)) return;
            var direction = Terraria.GameContent.SmartCursorHelper.LockedDesiredDirection;
            probe.Lookups.Add(new OperationGroupLookup { Frame = (long)Get(probe.Input, "Frame"), World = Main.GameUpdateCount,
                Token = (long)Get(flight, "Token"), Flight = (long)Get(flight, "Generation"), X = x, Y = y,
                Mode = Get(flight, "CursorMode").ToString(), Held = Main.LocalPlayer.controlUseItem,
                DirectionX = direction.HasValue ? direction.Value.X : 0, DirectionY = direction.HasValue ? direction.Value.Y : 0,
                ExactSmart = Main.SmartCursorIsUsed && Main.SmartCursorShowing && Main.SmartCursorX == x && Main.SmartCursorY == y &&
                    Player.tileTargetX == x && Player.tileTargetY == y });
        }

        private static int OperationGroupStock(Player p, int type)
        { return p.inventory.Take(58).Concat(new[] { Main.mouseItem }).Where(item => item != null).Distinct().Where(item => item.type == type).Sum(item => Math.Max(0, item.stack)); }

        private static void OperationGroupStonePickup(Player p)
        {
            var item = new Item(); item.SetDefaults(ItemID.StoneBlock); item.stack = 1;
            Require(p.GetItem(item, new GetItemSettings(NoText: true, NoSound: true)).IsAir && OperationGroupStock(p, ItemID.StoneBlock) == 1,
                "original GetItem introduces one real competing material before successor planning");
        }

        private static bool OperationGroupFlatGoalRunnable(Player p, OperationGroupGoal goal, Item item)
        {
            return !goal.Complete && !Main.tile[goal.X, goal.Y].active() && item != null && !item.IsAir &&
                p.IsInTileInteractionRange(goal.X, goal.Y, Terraria.DataStructures.TileReachCheckSettings.Simple, item.tileBoost + p.blockRange) &&
                WorldGen.SolidTile(goal.X, goal.Y + 1) && !p.Hitbox.Intersects(new Rectangle(goal.X * 16, goal.Y * 16, 16, 16));
        }

        private static void OperationGroupPlatformModes(object context, object host, object tools, object input)
        {
            bool oldMouse = Main.SmartCursorWanted_Mouse, oldPad = Main.SmartCursorWanted_GamePad;
            try
            {
                using (var probe = new OperationGroupProbe(context, host, tools, input))
                {
                    var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                    p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 4; var source = p.inventory[12];
                    SeedSmartPlatform(source, 43, 38, 1);
                    var smart = new OperationGroupGoal { Group = "Platform", X = 44, Y = 39, Type = source.createTile, Slope = 1 };
                    var precise = new OperationGroupGoal { Group = "Platform", X = 44, Y = 42, Type = source.createTile };
                    var other = new OperationGroupGoal { Group = "Stone", X = 46, Y = 42, Type = TileID.Stone };
                    probe.Goals = new[] { smart, precise, other };
                    probe.Runnable = goal => goal == smart ? !goal.Complete && !Main.tile[44, 39].active() && source.stack > 0 &&
                        p.IsInTileInteractionRange(44, 39, Terraria.DataStructures.TileReachCheckSettings.Simple, source.tileBoost + p.blockRange) &&
                        Main.tile[43, 38].active() && Main.tile[43, 38].type == source.createTile && Main.tile[43, 38].slope() == 1 &&
                        Player.GetSmartPlatformSlope(44, 39) == 1 : OperationGroupFlatGoalRunnable(p, goal, source);
                    Require(probe.Runnable(smart) && probe.Runnable(precise) && WorldGen.SolidTile(other.X, other.Y + 1),
                        "both independent platform modes are legal from the start, and the competitor has original solid support");
                    bool supplied = false;
                    probe.AfterEvent = entry =>
                    {
                        if (entry.Goal.Group == "Platform" && !supplied)
                        {
                            OperationGroupStonePickup(p); supplied = true;
                            var stone = p.inventory.First(item => item.type == ItemID.StoneBlock && item.stack > 0);
                            Require(OperationGroupFlatGoalRunnable(p, other, stone) && probe.Goals.Any(goal => goal.Group == "Platform" && probe.Runnable(goal)),
                                "real Stone competes with a still legal same-material platform before the input-mode handoff");
                        }
                    };
                    Place(context, host, input, Asset(host, SmartPlatformTarget(source, 0, 0, 1), SmartPlatformTarget(source, 0, 3),
                        new CaptureCell { X = 2, Y = 3, Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 44, 39);
                    SetAutomatic(host, input, true); probe.Until(() => probe.Goals.All(goal => goal.Complete) && Scene(host).Length == 0);
                    var platforms = probe.Events.Where(entry => entry.Goal.Group == "Platform").ToArray();
                    Require(probe.Events.Count == 3 && probe.Events.Take(2).All(entry => entry.Goal.Group == "Platform") && platforms.Length == 2 &&
                        platforms.Select(entry => entry.Choice.Mode).Distinct().Count() == 2 && platforms.Any(entry => entry.Choice.Mode == "SmartPlatform") &&
                        platforms.Any(entry => entry.Choice.Mode == "Precise") && platforms.All(entry => ReferenceEquals(entry.Choice.Source, source) && entry.Choice.Slot == 12) &&
                        platforms[0].Choice.Flight != platforms[1].Choice.Flight && source.stack == 2,
                        "one material completes both real Precise/Smart segments before the legal competitor, each with its own exact admitted flight/lease permission");
                    foreach (var entry in platforms.Where(value => value.Choice.Mode == "SmartPlatform"))
                    {
                        var held = probe.Lookups.LastOrDefault(value => value.World == entry.World && value.Frame == entry.Choice.Frame &&
                            value.Token == entry.Choice.Token && value.Flight == entry.Choice.Flight && value.Held && value.ExactSmart);
                        Require(held != null && held.DirectionX == 1 && held.DirectionY == 1 &&
                            probe.Lookups.Any(value => value.Token == entry.Choice.Token && value.Flight == entry.Choice.Flight &&
                            value.Mode == "SmartPlatform" && !value.Held && value.Frame < held.Frame &&
                            (entry == platforms[0] || value.Frame > platforms[0].Choice.Frame)),
                            "a Smart result follows an actually executed earlier unpressed original Lookup, then its exact held native coordinates on the same lease");
                    }
                    Require(Main.tile[43, 38].active() && Main.tile[43, 38].slope() == 1 && OperationGroupStock(p, ItemID.StoneBlock) == 0,
                        "the original slope endpoint is preserved and only one competing Stone is spent");
                }
            }
            finally { Main.SmartCursorWanted_Mouse = oldMouse; Main.SmartCursorWanted_GamePad = oldPad; }
        }

        private static void OperationGroupTileWand(object context, object host, object tools, object input)
        {
            using (var probe = new OperationGroupProbe(context, host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                p.inventory[12].SetDefaults(ItemID.LivingWoodWand); var wand = p.inventory[12];
                p.inventory[20].SetDefaults(ItemID.Wood); p.inventory[20].stack = 5; var wood = p.inventory[20];
                var goals = new[] { 44, 46, 48 }.Select(x => new OperationGroupGoal { Group = "TileWand", X = x, Y = 42, Type = wand.createTile }).ToArray();
                var other = new OperationGroupGoal { Group = "Stone", X = 45, Y = 42, Type = TileID.Stone };
                probe.Goals = goals.Concat(new[] { other }).ToArray();
                probe.Runnable = goal => OperationGroupFlatGoalRunnable(p, goal, wand) && OperationGroupStock(p, ItemID.Wood) > 0;
                Require(goals.All(probe.Runnable) && wand.tileWand == ItemID.Wood,
                    "three independently specified original floor targets share the same true wand/wood recipe");
                bool supplied = false;
                probe.AfterEvent = entry =>
                {
                    if (entry.Goal.Group == "TileWand" && !supplied)
                    {
                        OperationGroupStonePickup(p); supplied = true;
                        var stone = p.inventory.First(item => item.type == ItemID.StoneBlock && item.stack > 0);
                        Require(OperationGroupFlatGoalRunnable(p, other, stone) && goals.Any(probe.Runnable),
                            "real ordinary Stone is legal while the currently used wand still has known supplied recipe targets");
                    }
                };
                var cells = goals.Select(goal => new CaptureCell { X = goal.X - 44, Layers = CaptureLayer.Tile, TileType = (ushort)goal.Type })
                    .Concat(new[] { new CaptureCell { X = 1, Layers = CaptureLayer.Tile, TileType = TileID.Stone } }).OrderBy(cell => cell.X).ToArray();
                Place(context, host, input, Asset(host, cells), 44, 42); long unknown = (long)Get(host, "BuildUnknownResults");
                SetAutomatic(host, input, true); probe.Until(() => probe.Goals.All(goal => goal.Complete) && Scene(host).Length == 0);
                var recipe = probe.Events.Where(entry => entry.Goal.Group == "TileWand").ToArray();
                Require(probe.Events.Count == 4 && probe.Events.Take(3).All(entry => entry.Goal.Group == "TileWand") && recipe.Length == 3 &&
                    recipe.All(entry => entry.Choice.Producer == "TileWand" && ReferenceEquals(entry.Choice.Source, wand) && entry.Choice.Slot == 12 &&
                        ReferenceEquals(entry.Choice.Primary, wood) && entry.Choice.PrimaryType == ItemID.Wood && entry.Choice.PrimarySlot == 20 && entry.Debit == 1) &&
                    ReferenceEquals(p.inventory[12], wand) && wand.stack == 1 && ReferenceEquals(p.inventory[20], wood) && wood.stack == 2 &&
                    OperationGroupStock(p, ItemID.StoneBlock) == 0 && (long)Get(host, "BuildUnknownResults") == unknown,
                    "all same-recipe native writes precede a genuine ordinary competitor, each spends its exact leased Wood and preserves the real wand/source without Unknown");
            }
        }
    }
}
