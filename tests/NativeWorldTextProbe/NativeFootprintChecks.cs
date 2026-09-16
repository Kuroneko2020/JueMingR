using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using JueMingR.Features.Footprints;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.Graphics.Capture;
using Terraria.Map;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFootprintChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static bool throwStep;
        private static Vector2 movement;
        internal static void Run(string output)
        {
            Main.dedServ = true; try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(CaptureManager).TypeHandle); } finally { Main.dedServ = false; }
            string root = Path.Combine(Terraria.Program.SavePath, "footprint-composition"); Directory.CreateDirectory(root);
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            Main.gameMenu = Main.hideUI = Main.mapFullscreen = Main.inFancyUI = Main.onlyDrawFancyUI = Main.ingameOptionsWindow = false;
            Main.netMode = Main.myPlayer = 0; Main.maxTilesX = 256; Main.maxTilesY = 128; Main.Map = new WorldMap(256, 128); Main.screenWidth = 960; Main.screenHeight = 640;
            Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null); Main.GameViewMatrix.SetViewportOverride(new Microsoft.Xna.Framework.Graphics.Viewport(0, 0, 960, 640));
            Main.player[0] = new Player { active = true, name = "footprint fixture", position = new Vector2(1600, 800), gravDir = 1 };
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(root, "fixture.plr"), false) { Player = Main.player[0] };
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "fixture.wld"), false) { UniqueId = Guid.NewGuid() };
            typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity); FiniteCostChecks.SetCpuFont(8);
            var worker = assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true);
            object context = Activator.CreateInstance(worker.GetNestedType("PostfixContext", Flags), Flags, null, new object[] { "footprints-" + new string('7', 40), Path.Combine(root, "evidence.txt"), root }, null);
            var isolation = new Harmony("JueMingR.Tests.FootprintNativeBody"); var target = typeof(Main).GetMethod("DoUpdateInWorld_Inner", Flags);
            try
            {
                Call(context, "InstallInformationSources"); Call(context, "InitializeRuntime", true); object host = Get(context, "Footprints");
                Until(() => { Call(context, "UpdateRuntime"); return GetOptional(host, "Recorder") != null; });
                Require((bool)Get(Get(host, "hooks"), "Ready"), "fixed native step/teleport/spawn hooks install");
                foreach (string name in new[] { "MapFeatures", "DeathRecords", "Guidance", "Information", "WorldObjects", "WorldTargets", "Labels", "items", "notes" }) Require(GetOptional(context, name) != null, "complete footprint profile retains " + name);
                Require((bool)Get(host, "Recording") && !(bool)Get(host, "Display"), "recording and display defaults are independent");
                var recorder = (FootprintRecorder)Get(host, "Recorder");
                // This neutral executable never starts/constructs Main or runs
                // a world. Only the native method boundary is exercised with a
                // controlled safe body; production Harmony observers remain real.
                isolation.Patch(target, transpiler: new HarmonyMethod(typeof(NativeFootprintChecks).GetMethod(nameof(SafeBody), Flags)));
                object receiver = FormatterServices.GetUninitializedObject(typeof(Main)); movement = new Vector2(.001f, 0);
                for (int i = 0; i < 60; i++) target.Invoke(receiver, null);
                Require(recorder.End == 60 && recorder.Count == 60, "sixty completed native boundaries produce one simulation second and retain sub-tile movement");
                long end = recorder.End; for (int i = 0; i < 100; i++) Call(context, "UpdateRuntime"); Require(recorder.End == end, "outer/runtime updates without simulation never create time");
                throwStep = true; try { target.Invoke(receiver, null); throw new Exception("native failure expected"); } catch (TargetInvocationException e) { Require(e.InnerException is InvalidOperationException, "original simulated failure is preserved"); } finally { throwStep = false; }
                Require(recorder.End == end, "failed native step contributes no duration");
                target.Invoke(receiver, null); Require(recorder.At(recorder.ActiveCount - 1).Segment != recorder.At(recorder.ActiveCount - 2).Segment, "observation failure breaks resumed continuity");
                long segment = recorder.Segment;
                var hooksType = Get(host, "hooks").GetType(); hooksType.GetMethod("Discontinuity", Flags).Invoke(null, new object[] { Main.LocalPlayer }); target.Invoke(receiver, null);
                Require(recorder.Segment > segment, "native short teleport/spawn observer cuts continuity without distance heuristic");
                Main.LocalPlayer.dead = true; target.Invoke(receiver, null); Require(!recorder.At(recorder.ActiveCount - 1).HasPosition, "dead interval never stores spawn/default position"); Main.LocalPlayer.dead = false; target.Invoke(receiver, null);
                Call(host, "SetRecording", false); end = recorder.End; target.Invoke(receiver, null); Require(recorder.End == end, "recording off adds neither position nor time"); Call(host, "SetRecording", true); target.Invoke(receiver, null);
                InputFiltering(context);
                ClipOracle(assembly);
                var layer = Get(host, "Layer"); long projections = (long)Get(layer, "ProjectedPoints"), queries = (long)Get(layer, "QueryRequests");
                for (int i = 0; i < 300; i++) Call(context, "UpdateRuntime");
                Require((long)Get(layer, "ProjectedPoints") == projections && (long)Get(layer, "QueryRequests") == queries, "hidden display performs zero route projection or history queries while recording remains on");
                Call(host, "SetDisplay", true); Main.mapFullscreen = true;
                Until(() => { Call(layer, "Update"); return GetOptional(layer, "query") != null; }); queries = (long)Get(layer, "QueryRequests");
                var stationary = recorder.At(recorder.ActiveCount - 1);
                for (int i = 0; i < 3000; i++) { recorder.Observe(stationary.X, stationary.Y, stationary.Position, false); Set(layer, "nextRequest", 0d); Call(context, "UpdateRuntime"); }
                Require((long)Get(layer, "QueryRequests") == queries, "3000 visible stationary updates do not requery stale persisted tail");
                TimelineInput(context, host, layer, assembly);
                Call(host, "SetDisplay", false); Main.mapFullscreen = false;
                recorder.Flush(); var store = (FootprintStore)Get(host, "store"); Until(() => !store.HasUnsavedFacts);
                long saved = recorder.End; string pair = (string)Get(host, "pair"); Main.gameMenu = true; Call(context, "UpdateRuntime"); Main.gameMenu = false;
                Until(() => { Call(context, "UpdateRuntime"); return GetOptional(host, "Recorder") != null; });
                Require(((FootprintRecorder)Get(host, "Recorder")).End == saved, "ordinary same-pair re-entry loads committed history with a fresh continuity segment");
                Require(Directory.Exists(Path.Combine(root, "JueMingRData", "footprints", pair)), "real isolated history belongs to admitted character/world pair");
                string oldGeneration = (string)Get(host, "Generation");
                Require((bool)Call(host, "Clear", oldGeneration), "actual Host accepts isolated current archive clear");
                Until(() => { Call(context, "UpdateRuntime"); return GetOptional(host, "Recorder") != null && (string)Get(host, "Generation") != oldGeneration; });
                Require(((FootprintRecorder)Get(host, "Recorder")).Count == 0 && (bool)Get(host, "Recording") && !Directory.Exists(Path.Combine(root, "JueMingRData", "footprints", pair, oldGeneration)), "actual Host removes old body, publishes fresh empty generation and retains recording preference");
                Console.WriteLine("PASS: actual complete Host, fixed native step hook boundary, failed/skipped time, continuity, off cost, physical mouse staging and isolated save/re-entry.");
            }
            finally { isolation.Unpatch(target, HarmonyPatchType.All, isolation.Id); Main.gameMenu = true; Call(context, "UpdateRuntime"); StopContext(context); Main.gameMenu = false; }
        }
        private static IEnumerable<CodeInstruction> SafeBody(IEnumerable<CodeInstruction> ignored)
        { return new[] { new CodeInstruction(OpCodes.Call, typeof(NativeFootprintChecks).GetMethod(nameof(Step), Flags)), new CodeInstruction(OpCodes.Ret) }; }
        private static void Step() { if (throwStep) throw new InvalidOperationException("controlled-native-step-failure"); Main.LocalPlayer.position += movement; }
        private static void InputFiltering(object context)
        {
            object input = Get(context, "Input"); bool focus = true;
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => focus ? new IntPtr(1) : IntPtr.Zero));
            Func<bool> old = (Func<bool>)Get(input, "ClaimsMapPointer"); bool claims = false; Set(input, "ClaimsMapPointer", (Func<bool>)(() => claims));
            FocusHelper.IsSelectedApplication = true; PlayerInput.Triggers.Initialize(); PlayerInput.RawMouseScale = new Vector2(1.5f, 2);
            Action<int, bool> sample = (mask, claim) =>
            {
                claims = claim; Call(input, "BeginUpdate"); PlayerInput.MouseInfo = new MouseState(100, 120, 0, (mask & 1) != 0 ? ButtonState.Pressed : ButtonState.Released, (mask & 4) != 0 ? ButtonState.Pressed : ButtonState.Released, (mask & 2) != 0 ? ButtonState.Pressed : ButtonState.Released, (mask & 8) != 0 ? ButtonState.Pressed : ButtonState.Released, (mask & 16) != 0 ? ButtonState.Pressed : ButtonState.Released);
                PlayerInput.MouseKeys.Clear(); for (int i = 0; i < 5; i++) if ((mask & 1 << i) != 0) PlayerInput.MouseKeys.Add("Mouse" + (i + 1));
                Call(input, "AfterNativeMouse", PlayerInput.MouseKeys); PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 120; Call(input, "AfterMapping"); Main.keyState = new KeyboardState(Keys.W); Call(input, "AfterKeyboardRefresh");
            };
            try
            {
                sample(0, false); sample(0, false);
                for (int i = 0; i < 5; i++)
                {
                    sample(1 << i, true); Require(PlayerInput.MouseKeys.Count == 0 && PlayerInput.ScrollWheelDelta == 0 && PlayerInput.ScrollWheelDeltaForUI == 0, "claimed native mouse token and both wheel deltas removed " + i);
                    Require(Main.keyState.IsKeyDown(Keys.W) && (int)Get(input, "PhysicalMapX") == 150 && (int)Get(input, "PhysicalMapY") == 240, "keyboard and scaled same-sample physical coordinates survive");
                    sample(1 << i, false); Require(PlayerInput.MouseKeys.Count == 0, "tail remains owned after UI hides"); sample(0, false); sample(0, false);
                }
                sample(1, false); sample(1, true); Require(PlayerInput.MouseKeys.Count == 1, "press begun outside is never stolen on hover"); sample(0, false);
            }
            finally { Set(input, "ClaimsMapPointer", old); PlayerInput.RawMouseScale = Vector2.One; }
        }
        private static void ClipOracle(Assembly assembly)
        {
            var type = assembly.GetType("JueMingR.TerrariaHost.Footprints.FootprintMapLayer", true); var clip = type.GetMethod("Clip", Flags);
            object[] diagonal = { new Vector2(-100, -100), new Vector2(1000, 1000), 960f, 640f };
            Require((bool)clip.Invoke(null, diagonal) && (Vector2)diagonal[0] == Vector2.Zero && (Vector2)diagonal[1] == new Vector2(639, 639), "real crossing edge clipped at independent screen bounds");
            object[] outside = { new Vector2(-2, 10), new Vector2(-2, 600), 960f, 640f }; Require(!(bool)clip.Invoke(null, outside), "offscreen edge cannot manufacture a chord");
        }
        private static void TimelineInput(object context, object host, object layer, Assembly assembly)
        {
            object input = Get(context, "Input");
            var frameType = assembly.GetType("JueMingR.TerrariaHost.Map.MapView");
            object frame = Activator.CreateInstance(frameType, Flags, null, new object[] { Vector2.Zero, Vector2.Zero, 1f, 1f, 255 }, null);
            Call(layer, "Layout", frame, Terraria.GameContent.FontAssets.MouseText.Value); Set(layer, "drewBar", true); Call(layer, "Completed", frame);
            var rect = Get(layer, "play"); int x = (int)((float)Get(rect, "X") + 4), y = (int)((float)Get(rect, "Y") + 4);
            var playback = (FootprintPlayback)Get(layer, "Playback");
            Action<bool> sample = down =>
            {
                Call(input, "BeginUpdate"); PlayerInput.MouseInfo = new MouseState(x, y, 0, down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                PlayerInput.MouseKeys.Clear(); if (down) PlayerInput.MouseKeys.Add("Mouse1");
                Call(input, "AfterNativeMouse", PlayerInput.MouseKeys); Call(input, "AfterMapping"); Main.keyState = new KeyboardState(); Call(input, "AfterKeyboardRefresh"); Call(layer, "ProcessInput");
            };
            sample(false); sample(false); sample(true); Require(PlayerInput.MouseKeys.Count == 0, "actual timeline claims its play button before native keyboard mapping");
            sample(false); Require(playback.Playing && playback.Cursor == 0, "actual production play gesture starts latest timeline at earliest");
            playback.GoLatest((long)Get(host, "End")); sample(true);
            var oldFont = Terraria.GameContent.FontAssets.MouseText; FiniteCostChecks.SetCpuFont(11); sample(false);
            Require(!playback.Playing, "font replacement rejects old physical release before draw"); Terraria.GameContent.FontAssets.MouseText = oldFont;
        }
        private static void Until(Func<bool> check)
        { var clock = System.Diagnostics.Stopwatch.StartNew(); while (!check()) { if (clock.ElapsedMilliseconds > 10000) throw new TimeoutException("native footprint condition"); System.Threading.Thread.Sleep(5); } }
    }
}
