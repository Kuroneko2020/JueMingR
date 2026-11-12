using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.IO;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeJourneyChecks
    {
        private static ManualResetEvent singleExitFinished;
        private static Exception singleExitFailure;

        // A bounded investigation of the reported single-player exit path.
        // The real asynchronous exit owns the actual final files. Calling the
        // feature's Disable command would erase the lifecycle being examined.
        internal static int RunSinglePlayerExit(string content, string output)
        {
            output = Path.GetFullPath(output); Directory.CreateDirectory(output); NewRoot(output, "single-exit");
            NativeQuickItemChecks.Run(context =>
            {
                using (var graphics = new ProbeGraphics(content))
                {
                    Prepare(context); NativeJourneyUiChecks.PrepareNativeMenu(graphics);
                    NativeHiddenEntryChecks.RevealForComposition(context);
                    ConfigureFiles(0, 0, "single-exit"); CreativePowerManager.Instance.Reset();
                    // The shared micro-world uses surface=45. Vanilla treats
                    // <=50 as a no-surface world with a fixed clock, unrelated
                    // to FreezeTime. Give this clock consumer a real surface.
                    Main.worldSurface = 60; Call(context, "UpdateRuntime");
                    // Main.Initialize normally constructs every rain particle.
                    // The existing CPU fixture leaves these slots empty, but
                    // the real exit clears them before writing the world.
                    for (int i = 0; i < Main.maxRain; i++) if (Main.rain[i] == null) Main.rain[i] = new Rain();
                    var freeze = CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>();
                    Require(!freeze.Enabled, "single-player investigation starts from an actually unfrozen native baseline");
                    byte[] world = CaptureWorld(), player = CapturePlayer(Main.LocalPlayer);
                    object journey = Get(context, "Journey");
                    Require((bool)Call(journey, "SetEnabled", true), "single-player activation succeeds: " + GetOptional(journey, "LastFailure"));
                    var buttons = new List<UIElement>();
                    freeze.ProvidePowerButtons(new CreativePowerUIElementRequestInfo(), buttons);
                    Require(buttons.Count == 1, "native FreezeTime supplies its original toggle button");
                    buttons[0].LeftClick(new UIMouseEvent(buttons[0], Vector2.Zero));
                    Require(freeze.Enabled, "native FreezeTime button and single-player packet loopback actually freeze time");
                    Require((bool)Call(journey, "CloseMenu") && (bool)Get(journey, "IsActive"), "hiding the native menu retains the borrowed power");
                    Main.LocalPlayer.inventory[12].SetDefaults(Terraria.ID.ItemID.Torch); Main.LocalPlayer.inventory[12].stack = 37;
                    CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>().SetEnabledState(Main.myPlayer, true);
                    // Exercise the changed rename save while borrowed personal
                    // and world powers are live. Read this write immediately;
                    // a later exit overwrite would not prove rename protection.
                    using (var map = NativeRenameMapChecks.Begin())
                    {
                        object rename = Get(Get(context, "Fishing"), "Rename");
                        Require((bool)Call(rename, "Rename", "Journey009") && (bool)Get(rename, "PersistedVerified"), "map-enabled shared rename owner confirms its actual character save");
                        map.AssertSkipped();
                        Require(Main.LocalPlayer.name == "Journey009" && freeze.Enabled && (bool)Get(journey, "IsActive"), "rename keeps the borrowed session and its selected live powers");
                    }
                    WorldFile.SaveWorld(); WriteManifest(output, "single-active-rename", 0, 0, world, player);
                    ReadInNewProcess(content, output);
                    Console.WriteLine("JOURNEY-SINGLE R map-enabled rename actual encrypted player write retains baseline personal powers before exit overwrite");
                    Main.dayTime = true; Main.time = 12000; Main.UpdateTimeRate();
                    Require(Main.dayRate == 0, "normal UpdateTimeRate consumes the borrowed freeze before exit");
                    Console.WriteLine("JOURNEY-SINGLE before-exit freeze=" + freeze.Enabled + " rate=" + Main.dayRate + " time=" + Main.time);
                    var h = new Harmony("NativeJourneyProbe.SinglePlayerExit");
                    var callback = typeof(WorldGen).GetMethod("SaveAndQuitCallBack", Flags);
                    singleExitFinished = new ManualResetEvent(false); singleExitFailure = null;
                    h.Patch(callback, finalizer: new HarmonyMethod(typeof(NativeJourneyChecks), nameof(SingleExitCompleted)));
                    try
                    {
                        WorldGen.SaveAndQuit();
                        // The real game loop observes gameMenu on its next
                        // update; keep that edge while the native worker saves.
                        Call(context, "UpdateRuntime");
                        Require(singleExitFinished.WaitOne(20000), "real SaveAndQuit worker completes within the bounded probe");
                        if (singleExitFailure != null) throw new InvalidOperationException("Original SaveAndQuit worker failed", singleExitFailure);
                    }
                    finally
                    {
                        h.Unpatch(callback, HarmonyPatchType.All, h.Id);
                        // On timeout the worker may still own its finalizer;
                        // don't dispose the event beneath that original call.
                        if (singleExitFinished.WaitOne(0)) singleExitFinished.Dispose();
                    }
                    Require(File.Exists(Main.ActiveWorldFileData.Path) && File.Exists(Main.ActivePlayerFileData.Path), "real exit produced final owned player/world files");
                    WriteManifest(output, "single-exit", 0, 0, world, player);
                    // This child loads no R. It reads the actual final .wld
                    // section, not SaveToWorld inside a protected SaveView.
                    ReadInNewProcess(content, output);
                    Console.WriteLine("JOURNEY-SINGLE A final-file independent-original-reader=baseline(false)");
                    Require(!(bool)Get(journey, "IsActive"), "real menu/runtime exit retires the borrowed session");
                    CreativePowerManager.Instance.Reset();
                    WorldFile.LoadWorld();
                    Require(!WorldGen.loadFailed, "original complete LoadWorld accepts the final file: " + WorldFile.LastThrownLoadException);
                    WorldGen.isGeneratingOrLoadingWorld = false; Main.gameMenu = false; Main.gamePaused = false; Main.playerInventory = false;
                    Call(context, "UpdateRuntime");
                    Main.UpdateTimeRate();
                    Console.WriteLine("JOURNEY-SINGLE B loaded-live freeze=" + freeze.Enabled + " target=" + CreativePowerManager.Instance.GetPower<CreativePowers.ModifyTimeRate>().TargetTimeRate + " dayRate=" + Main.dayRate + " time=" + Main.time);
                    Require(!freeze.Enabled && Main.dayRate > 0, "loaded live native power and time rate are unfrozen without re-enabling Journey");
                    double at = Main.time;
                    typeof(Main).GetMethod("UpdateTime", Flags).Invoke(null, null);
                    Console.WriteLine("JOURNEY-SINGLE C original-UpdateTime before=" + at + " after=" + Main.time + " paused=" + Main.gamePaused);
                    Require(Main.time > at, "real original UpdateTime advances the reloaded world's clock");
                    Require(Main.time >= 12000, "actual advanced world time is retained rather than rolled back to activation");
                }
            }, processing: true, shortFeedback: true);
            Console.WriteLine("PASS JOURNEY SINGLE PLAYER EXIT: native button/loopback, real SaveAndQuit worker and independent final-file reader, original LoadWorld and UpdateTime. Isolated fixture; owner reproduction and ordinary client loop remain unverified.");
            return 0;
        }

        private static Exception SingleExitCompleted(Exception __exception)
        {
            // Keep an original worker failure as a failing probe result on its
            // owning test thread, rather than terminating the whole CLR before
            // the exact exception can be recorded. This is test-only handling.
            singleExitFailure = __exception; singleExitFinished.Set(); return null;
        }
    }
}
