using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static object failureBuildContext, failureBuildHost, failureBuildTools, failureBuildPreparation, failureBuildFlight;
        private static Item failureBuildMaterial;
        private static long failureBuildToken, failureBuildCandidates, failureBuildChoices, failureBuildSourceReads;
        private static bool failureBuildArmed, failureBuildSeen, failureBuildRetained, failureBuildReplaceWorld;
        private static int failureBuildCompleted, failureBuildNativeEnds;

        // Reuse the real construction fixture: production discovery, priority
        // material preparation, original ApplyPaint and native inventory tail.
        // Only the fault and unrelated achievements are controlled here.
        internal static void RunCommonFailureBuild(object context, bool replaceWorld = false, bool manualTakeover = false, int selectionTakeover = 0)
        {
            var host = Get(context, "Blueprint"); var tools = Get(context, "Tools"); var input = Get(context, "Input");
            NativeToolExecutionChecks.Initialize(context, null);
            Require((bool)Get(tools, "Available"), "common-build fixture initializes real tool hooks");
            Framing.Initialize(); Main.SetupTileMerge(); TorchID.Initialize();
            var lighting = Lighting.Mode; Lighting.Mode = Terraria.Graphics.Light.LightMode.Color;
            var world = Main.ActiveWorldFileData;
            bool blockInput = Main.blockInput;
            var patch = new Harmony("JueMingR.Tests.RuntimeFailureBuild");
            foreach (string name in new[] { "HandleSpecialEvent", "HandleMining", "HandleRunning" })
                patch.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name, F), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SkipOutlet)));
            patch.Patch(AccessTools.Method(typeof(Player), "ApplyPaint", new[] { typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(Item) }),
                postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(FailInsideOriginalBuild)));
            patch.Patch(host.GetType().GetMethod("EndAutomaticBuild", F), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ObserveFailureBuildCompleted)));
            var use = Get(tools, "Use");
            patch.Patch(use.GetType().GetMethod("End", F), postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ObserveFailureBuildNativeEnd)));
            failureBuildContext = context; failureBuildHost = host; failureBuildTools = tools;
            failureBuildPreparation = Get(host, "buildMaterialPreparation");
            failureBuildArmed = failureBuildSeen = failureBuildRetained = false;
            failureBuildReplaceWorld = replaceWorld; failureBuildCompleted = failureBuildNativeEnds = 0;
            constructionChecksActive = true;
            var inputTail = failureBuildPreparation.GetType().GetMethod("NativeInputTailForChecks", F);
            Require(inputTail != null, "common-build fixture uses the locked original inventory input tail");
            constructionInputTail = () => inputTail.Invoke(null, null);
            try
            {
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled"); });
                using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 200, 160))
                {
                    var p = Reset(context, host, tools, input); p.ResetFallDamage();
                    p.inventory[12].SetDefaults(ItemID.Paintbrush);
                    p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 4; var red = p.inventory[20];
                    p.inventory[54].SetDefaults(ItemID.BluePaint); p.inventory[54].stack = 7; var blue = p.inventory[54];
                    NativeToolsChecks.Tile(44, 42, TileID.Stone);
                    Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = red.paint }), 44, 42);
                    failureBuildMaterial = red; failureBuildArmed = true;
                    SetAutomatic(host, input, true);
                    Until(context, input, () => failureBuildSeen);
                    Console.WriteLine("COMMON BUILD seen=" + failureBuildSeen + " retainedInsideNative=" + failureBuildRetained +
                        " completed=" + failureBuildCompleted + " nativeEnds=" + failureBuildNativeEnds + " phase=" + Get(failureBuildPreparation, "phase") +
                        " red=" + red.stack + " old54=" + blue.stack + " worldChanged=" + replaceWorld);
                    Require(failureBuildRetained, "common failure retains the same real BuildFlight/token and material use until its original native consumer exits");
                    Require(failureBuildNativeEnds == 1, "the original in-flight ItemCheck finalizer completes exactly once after the common failure");
                    if (replaceWorld)
                    {
                        Require(!(bool)Get(use, "Active") && GetOptional(host, "activeBuild") == null && !(bool)Get(failureBuildPreparation, "HasWork"),
                            "real world replacement retires the old build receipt without admitting a successor");
                        Require(ReferenceEquals(p.inventory[54], red) && red.stack == 3 && blue.stack == 7 &&
                            p.inventory.Any(i => ReferenceEquals(i, blue)) && Main.mouseItem.IsAir,
                            "successor world cannot replay the old inverse material swaps into the unchanged native containers");
                    }
                    else
                    {
                        long clicks = (long)Get(failureBuildPreparation, "NativeClicks");
                        var slot20 = p.inventory[20]; var slot54 = p.inventory[54];
                        // The original animation/selection buffer must finish
                        // before ToolUse can transfer its material token back.
                        // Keep the real input barrier throughout that wait and
                        // prove no inverse write occurs on any native step.
                        NativeQuickItemChecks.Until(() =>
                        {
                            Step(context, input);
                            Require((long)Get(failureBuildPreparation, "NativeClicks") == clicks && ReferenceEquals(p.inventory[20], slot20) &&
                                ReferenceEquals(p.inventory[54], slot54) && Main.mouseItem.IsAir,
                                "blocked original selection completion cannot perform any inverse material write");
                            return (bool)Get(failureBuildPreparation, "Returning");
                        });
                        Require(!(bool)Get(use, "Active") && !p.selectedItemState.HasActiveOverride && !p.selectedItemState.HasBufferedChange,
                            "the actual original selection buffer has been consumed before the material return starts");
                        for (int i = 0; i < 4; i++) Step(context, input);
                        Require((bool)Get(failureBuildPreparation, "Returning") && (long)Get(failureBuildPreparation, "NativeClicks") == clicks &&
                            ReferenceEquals(p.inventory[20], slot20) && ReferenceEquals(p.inventory[54], slot54) && Main.mouseItem.IsAir,
                            "the real native blockInput gate preserves the old inverse material duty without any inventory write");
                        Main.blockInput = false;
                        if (selectionTakeover != 0)
                        {
                            long selection = (long)Get(tools, "SelectionIntent");
                            var oldWorld=Main.ActiveWorldFileData;var oldPlayer=Main.player[Main.myPlayer];var oldInventory=p.inventory;
                            int oldMode=Main.netMode;
                            Action<string,Action,Action> foreign=(label,enter,leave)=>
                            {
                                enter();
                                try
                                {
                                    p.selectedItemState.Select(1);p.selectedItemState.Update();
                                    Require((long)Get(tools,"SelectionIntent")==selection,
                                        "a failed return cannot observe another current "+label+" as its retained player selection");
                                }
                                finally{leave();}
                            };
                            foreign("world",()=>Main.ActiveWorldFileData=new Terraria.IO.WorldFileData("foreign-selection.wld",false),()=>Main.ActiveWorldFileData=oldWorld);
                            foreign("player",()=>Main.player[Main.myPlayer]=new Player(),()=>Main.player[Main.myPlayer]=oldPlayer);
                            foreign("inventory array",()=>p.inventory=(Item[])oldInventory.Clone(),()=>p.inventory=oldInventory);
                            foreign("connection mode",()=>Main.netMode=1,()=>Main.netMode=oldMode);
                            int selected = selectionTakeover == 1 ? 2 : (p.selectedItem + 1) % 10;
                            // Both keyboard slot selection and scrolling enter
                            // this actual vanilla SelectedItemState consumer.
                            // OS sampling/caller mapping is outside this fixture.
                            p.selectedItemState.Select(selected);
                            p.selectedItemState.Update();
                            Require((long)Get(tools, "SelectionIntent") > selection,
                                "actual vanilla Select records the player's intent during failed-session material return");
                            for (int i = 0; i < 8; i++) Step(context, input);
                            Require((long)Get(failureBuildPreparation, "NativeClicks") == clicks &&
                                ReferenceEquals(p.inventory[20], slot20) && ReferenceEquals(p.inventory[54], slot54) &&
                                red.stack == 3 && blue.stack == 7 && Main.mouseItem.IsAir,
                                "failed-session manual selection relinquishes every old inverse click and preserves real remainder references");
                            Require(failureBuildCompleted == 1 && failureBuildNativeEnds == 1,
                                "manual selection does not duplicate the completed native use/finalizer");
                        }
                        else if (manualTakeover)
                        {
                            // The player takes the real native inventory before
                            // the next automatic input tail can undo a swap.
                            p.ToggleInv(); ManualPreparationClick(context, input, p, 54); ManualPreparationClick(context, input, p, 22); p.ToggleInv();
                            slot20 = p.inventory[20]; slot54 = p.inventory[54];
                            for (int i = 0; i < 8; i++) Step(context, input);
                            Require(ReferenceEquals(p.inventory[22], red) && red.stack == 3 && ReferenceEquals(p.inventory[20], slot20) &&
                                ReferenceEquals(p.inventory[54], slot54) && blue.stack == 7 && p.inventory.Any(i => ReferenceEquals(i, blue)) && Main.mouseItem.IsAir &&
                                (long)Get(failureBuildPreparation, "NativeClicks") == clicks,
                                "actual manual ItemSlot takeover owns the unchanged remainder and prevents every later automatic inverse write");
                        }
                        else
                        {
                            Until(context, input, () => !(bool)Get(use, "Active") && !(bool)Get(failureBuildPreparation, "HasWork"));
                            Require(failureBuildCompleted == 1 && GetOptional(host, "activeBuild") == null,
                                "one exact original BuildFlight completion releases its finite material use");
                            Require(ReferenceEquals(p.inventory[20], red) && red.stack == 3 && ReferenceEquals(p.inventory[54], blue) && blue.stack == 7 && Main.mouseItem.IsAir,
                                "real inverse ItemSlot inputs return both original material objects with exactly one native paint debit");
                        }
                    }
                    Main.blockInput = blockInput;
                    for (int i = 0; i < 8; i++) Step(context, input);
                    Require((long)Get(host, "BuildCandidateChecks") == failureBuildCandidates && (long)Get(host, "BuildNativeChoices") == failureBuildChoices &&
                        (long)Get(host, "BuildReachSourceReads") == failureBuildSourceReads,
                        "terminal build return runs no new candidate discovery, source scan or native build choice");
                    Require(!(bool)Get(Get(Get(context, "Runtime"), "SharedRuntime"), "IsSessionActive"),
                        "completed old material duty does not reactivate the terminal session");
                    Console.WriteLine("PASS common BuildFlight original ApplyPaint/finalizer and exact material return, changedWorld=" + replaceWorld + " manualTakeover=" + manualTakeover + " selectionTakeover=" + selectionTakeover + "; CPU native input tail, no Draw/network/gameplay claim.");
                }
            }
            finally
            {
                failureBuildArmed = false; constructionChecksActive = false; constructionInputTail = null;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                Main.ActiveWorldFileData = world; Main.blockInput = blockInput; Lighting.Clear(); Lighting.Mode = lighting;
                failureBuildContext = failureBuildHost = failureBuildTools = failureBuildPreparation = failureBuildFlight = null;
                failureBuildMaterial = null;
            }
        }

        private static void FailInsideOriginalBuild(Player __instance, Item __4)
        {
            if (!failureBuildArmed || !ReferenceEquals(__instance, Main.LocalPlayer) || !ReferenceEquals(__4, failureBuildMaterial)) return;
            failureBuildArmed = false;
            var use = Get(failureBuildTools, "Use");
            failureBuildFlight = GetOptional(failureBuildHost, "activeBuild"); failureBuildToken = (long)Get(use, "Operation");
            Require(failureBuildFlight != null && failureBuildToken > 0 && (bool)Get(use, "InNativeUse") && (bool)Get(failureBuildPreparation, "Using") &&
                ReferenceEquals(Get(failureBuildPreparation, "Material"), __4) && __4.stack == 3,
                "fixture fault enters the genuine prepared ApplyPaint after its one real debit");
            failureBuildCandidates = (long)Get(failureBuildHost, "BuildCandidateChecks"); failureBuildChoices = (long)Get(failureBuildHost, "BuildNativeChoices");
            failureBuildSourceReads = (long)Get(failureBuildHost, "BuildReachSourceReads");
            Main.blockInput = true;
            typeof(NativeRuntimeFailureReturnChecks).GetMethod("CommonFault", F).Invoke(null, new[] { failureBuildContext });
            failureBuildRetained = (bool)Get(use, "Active") && (bool)Get(use, "InNativeUse") && (long)Get(use, "Operation") == failureBuildToken &&
                ReferenceEquals(GetOptional(failureBuildHost, "activeBuild"), failureBuildFlight) && (bool)Get(failureBuildPreparation, "Using");
            failureBuildSeen = true;
            if (failureBuildReplaceWorld)
            {
                Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath, "build-successor.wld"), false) { UniqueId = Guid.NewGuid() };
                Call(failureBuildContext, "UpdateRuntime");
            }
        }
        private static void ObserveFailureBuildCompleted(object __0)
        { if (__0 != null && ReferenceEquals(__0, failureBuildFlight) && (long)Get(__0, "Token") == failureBuildToken) failureBuildCompleted++; }
        private static void ObserveFailureBuildNativeEnd(long operation)
        { if (failureBuildSeen && operation == failureBuildToken) failureBuildNativeEnds++; }
    }
}
