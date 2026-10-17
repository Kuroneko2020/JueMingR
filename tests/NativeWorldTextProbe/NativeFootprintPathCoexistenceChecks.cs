using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFootprintPathCoexistenceChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(object context, ProbeGraphics graphics, string output)
        {
            graphics.SetMouseFont(graphics.Font);
            NativeCombatAimChecks.Initialize();
            object combat = Get(context, "CombatObservation"), world = Get(combat, "World"), footprints = Get(context, "Footprints"), input = Get(context, "Input");
            var player = NativeToolExecutionChecks.Reset(context, Get(context, "Tools"), input, 1229, 0, 0);
            player.position = new Vector2(640, 640); player.velocity = Vector2.Zero; player.itemAnimation = player.itemTime = 0;
            foreach (var entity in Main.npc) entity.active = false;
            var npc = Main.npc[2]; npc.SetDefaults(2); npc.whoAmI = 2; npc.active = true; npc.position = new Vector2(880, 560); npc.target = 0; npc.life = npc.lifeMax = 10000;
            Main.screenWidth = 960; Main.screenHeight = 640; Main.screenPosition = new Vector2(400, 300); Main.GameViewMatrix.Zoom = Vector2.One; Main.hideUI = Main.mapFullscreen = false;
            Set(combat, "LayerStatus", Enum.Parse(Get(combat, "LayerStatus").GetType(), "Ready"));
            NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return GetOptional(footprints, "Recorder") != null; });
            Call(footprints, "SetRecording", true); Call(footprints, "SetDisplay", true);
            NativeCombatObservationChecks.Save(combat, new ObservationOptions(path: true));
            var target = typeof(Main).GetMethod("DoUpdateInWorld_Inner", Flags);
            var controlled = new Harmony("JueMingR.Tests.FootprintPathCoexistence");
            try
            {
                // Reuse the established safe native-body boundary. The real
                // footprint prefix/finalizer and real combat Source remain on.
                controlled.Patch(target, transpiler: new HarmonyMethod(typeof(NativeFootprintPathCoexistenceChecks).GetMethod(nameof(SafeBody), Flags)));
                for (int i = 0; i < 24; i++)
                {
                    Thread.Sleep(2); target.Invoke(Main.instance, null);
                    NativeCombatObservationChecks.Fresh(context, combat); Call(footprints, "Update", (ulong)Main.GameUpdateCount); Call(world, "Prepare");
                    if (GetOptional(combat, "DisplayPath") != null && i >= 8) break;
                }
                Require(GetOptional(combat, "DisplayPath") != null && (long)Get(Get(footprints, "Recorder"), "End") > 0, "actual path Source publishes while actual footprint hook records");
                int worldPixels = Ink(graphics.Pixels(() => Call(world, "Draw"), Main.GameViewMatrix.ZoomMatrix));
                Require((int)Get(world, "StrokeCount") > 0 && worldPixels > 0, "enabled footprint recording retains actual adjacent path draw");
                var layer = Get(footprints, "Layer"); Main.mapFullscreen = true; Call(layer, "Update");
                var frameType = footprints.GetType().Assembly.GetType("JueMingR.TerrariaHost.Map.MapView", true);
                object frame = Activator.CreateInstance(frameType, Flags, null, new object[] { Vector2.Zero, Vector2.Zero, 4f, 1f, 255 }, null);
                int mapPixels = Ink(graphics.Pixels(() => Call(layer, "DrawRoutes", frame), Matrix.Identity));
                Require(mapPixels > 8, "map draws the concurrently recorded trail");
                graphics.Image(Path.Combine(output, "footprints-path-coexistence-map.png"), () => Call(layer, "DrawRoutes", frame), Matrix.Identity);
                Main.mapFullscreen = false; Call(layer, "Update"); NativeCombatObservationChecks.Fresh(context, combat); Call(world, "Prepare");
                int resumedPixels = Ink(graphics.Pixels(() => Call(world, "Draw"), Main.GameViewMatrix.ZoomMatrix));
                Require((int)Get(world, "StrokeCount") > 0 && resumedPixels > 0, "closing map resumes actual path display with both features still enabled");
                File.WriteAllText(Path.Combine(output, "footprints-path-coexistence.json"), "{\"pathPixels\":" + worldPixels + ",\"mapTrailPixels\":" + mapPixels + ",\"resumedPathPixels\":" + resumedPixels + ",\"nativeFootprintEndTicks\":" + Get(Get(footprints, "Recorder"), "End") + ",\"weaponUse\":false,\"ownerGameRun\":false}");
                Console.WriteLine("PASS: actual path Source/Draw and actual footprint native hook/map consumer coexist across world-map-world in the complete profile; no weapon use.");
            }
            finally { controlled.Unpatch(target, HarmonyPatchType.All, controlled.Id); Main.mapFullscreen = false; NativeCombatObservationChecks.Save(combat, new ObservationOptions()); }
        }
        private static int Ink(Color[] pixels) { int result = 0; foreach (var pixel in pixels) if (pixel.A > 0) result++; return result; }
        private static IEnumerable<CodeInstruction> SafeBody(IEnumerable<CodeInstruction> ignored)
        { return new[] { new CodeInstruction(OpCodes.Call, typeof(NativeFootprintPathCoexistenceChecks).GetMethod(nameof(Step), Flags)), new CodeInstruction(OpCodes.Ret) }; }
        private static void Step() { Main.LocalPlayer.position += new Vector2(8, 0); }
    }
}
