using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.Map;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Executes the locked game's actual fullscreen geometry and ping inverse.
    // Skip only map/background/icon GPU effects; no production formula is copied.
    internal static class NativeMapDrawOracle
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static Vector2 ping;
        private static int pings;
        private static object completedFrame;
        private static void Completed(object value) { completedFrame = value; }
        internal static void Run(object host)
        {
            var isolation = new Harmony("JueMingR.Tests.MapDrawOracle");
            var draw = typeof(Main).GetMethod("DrawMap", Flags);
            var icons = typeof(MapIconOverlay).GetMethod("Draw", Flags);
            var trigger = typeof(Main).GetMethod("TriggerPing", Flags);
            int screenW = Main.screenWidth, screenH = Main.screenHeight; float oldUi = Main.UIScale;
            bool oldEnabled = Main.mapEnabled, oldReady = Main.mapReady;
            var receiver = (Main)FormatterServices.GetUninitializedObject(typeof(Main));
            var drawing = Get(Get(host, "Layer"), "drawing"); var completed = drawing.GetType().GetEvent("Completed", Flags);
            var observer = Delegate.CreateDelegate(completed.EventHandlerType, typeof(NativeMapDrawOracle).GetMethod(nameof(Completed), Flags));
            completed.GetAddMethod(true).Invoke(drawing, new object[] { observer });
            try
            {
                isolation.Patch(draw, transpiler: Hook(nameof(Isolate)));
                isolation.Patch(icons, transpiler: Hook(nameof(IconSink)));
                isolation.Patch(trigger, prefix: Hook(nameof(PingSink)));
                Main.mapEnabled = Main.mapReady = Main.mapFullscreen = true;
                int cases = 0, snapRejected = 0;
                foreach (int screen in new[] { 960, 1280 }) foreach (float ui in new[] { .75f, 1f, 1.5f })
                foreach (float zoom in new[] { .01f, 2.5f, 12.3f, 40f }) foreach (Vector2 center in new[] { new Vector2(-20, -20), new Vector2(128.25f, 64.5f), new Vector2(280, 150) })
                {
                    Main.screenWidth = screen; Main.screenHeight = screen == 960 ? 640 : 720; SetUi(ui);
                    Main.mapFullscreenScale = zoom; Main.mapFullscreenPos = center; Main.resetMapFull = Main.PanTargetMapFullscreen = false;
                    Main.mouseX = screen / 2 - 11; Main.mouseY = Main.screenHeight / 2 + 7; Main.mouseLeft = Main.mouseLeftRelease = true;
                    typeof(Main).GetField("_lastPingMouseDownTime", Flags).SetValue(null, 0d); pings = 0;
                    receiver.DrawMap(new GameTime(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(.016)));
                    receiver.DrawMap(new GameTime(TimeSpan.FromSeconds(10.1), TimeSpan.FromSeconds(.016)));
                    Require(pings == 1, "actual native double-click path reached once");
                    object frame = completedFrame; Require(frame != null, "successful native draw publishes completed view to consumers");
                    var method = frame.GetType().GetMethod("TryPoint", Flags);
                    object[] args = { (float)Main.mouseX, (float)Main.mouseY, Main.maxTilesX, Main.maxTilesY, 0d, 0d };
                    bool accepted = (bool)method.Invoke(frame, args);
                    bool inside = ping.X >= 0 && ping.Y >= 0 && ping.X < Main.maxTilesX && ping.Y < Main.maxTilesY;
                    Require(accepted == inside, "production rejects only native points outside logical world");
                    if (inside)
                    {
                        var projected = (Vector2)Call(frame, "Project", (double)ping.X, (double)ping.Y);
                        Require(Vector2.Distance(projected, new Vector2(Main.mouseX, Main.mouseY)) <= 1, "actual DrawMap parameters agree with independent original ping within one pixel");
                        Require(Math.Abs((double)args[4] - ping.X) * (float)Get(frame, "Zoom") <= 1 && Math.Abs((double)args[5] - ping.Y) * (float)Get(frame, "Zoom") <= 1, "input inverse agrees with original ping");
                        Vector2 snapped = (Vector2)Call(frame, "Project", Math.Floor((double)args[4]) + .5, Math.Floor((double)args[5]) + .5);
                        if (Vector2.Distance(snapped, projected) > 1) snapRejected++;
                        Require(Vector2.Distance(projected + new Vector2(17, 0), new Vector2(Main.mouseX, Main.mouseY)) > 1, "stale mouse offset negative control rejected");
                    }
                    cases++;
                }
                Require(snapRejected > 0, "finite floor+half-tile mutation is detected");
                Main.screenWidth = 960; Main.screenHeight = 640; Main.mouseLeft = false; Main.mapFullscreenScale = 2.5f;
                Main.screenPosition = new Vector2(1000, 200); Main.resetMapFull = true;
                receiver.DrawMap(new GameTime()); Require(!Main.resetMapFull && completedFrame != null, "first-open reset publishes real completed geometry");
                Console.WriteLine("PASS: actual DrawMap/ping geometry " + cases + " zoom/UI/edge/viewport cases plus first-open reset; finite stale-mouse and half-tile controls rejected. GPU effects isolated.");
            }
            finally
            {
                completed.GetRemoveMethod(true).Invoke(drawing, new object[] { observer }); completedFrame = null;
                isolation.UnpatchAll(isolation.Id); Main.screenWidth = screenW; Main.screenHeight = screenH; SetUi(oldUi);
                Main.mapEnabled = oldEnabled; Main.mapReady = oldReady; Main.mapFullscreen = Main.mouseLeft = false; Main.LocalPlayer.mouseInterface = false;
            }
        }
        private static void SetUi(float value) { typeof(Main).GetField("_uiScaleUsed", Flags).SetValue(null, value); typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.CreateScale(value, value, 1)); }
        private static HarmonyMethod Hook(string name) { return new HarmonyMethod(typeof(NativeMapDrawOracle).GetMethod(name, Flags)); }
        private static bool PingSink(Vector2 position) { ping = position; pings++; return false; }
        private static IEnumerable<CodeInstruction> IconSink(IEnumerable<CodeInstruction> input) { return new[] { new CodeInstruction(OpCodes.Ret) }; }
        private static bool CallTo(CodeInstruction instruction, string type, string name)
        { var method = instruction.operand as MethodInfo; return method != null && method.DeclaringType.Name == type && method.Name == name; }
        private static int Local(CodeInstruction instruction)
        {
            if (instruction.operand is LocalBuilder) return ((LocalBuilder)instruction.operand).LocalIndex;
            if (instruction.operand is byte) return (byte)instruction.operand;
            if (instruction.operand is int) return (int)instruction.operand;
            if (instruction.opcode == OpCodes.Ldloc_0 || instruction.opcode == OpCodes.Stloc_0) return 0;
            if (instruction.opcode == OpCodes.Ldloc_1 || instruction.opcode == OpCodes.Stloc_1) return 1;
            if (instruction.opcode == OpCodes.Ldloc_2 || instruction.opcode == OpCodes.Stloc_2) return 2;
            if (instruction.opcode == OpCodes.Ldloc_3 || instruction.opcode == OpCodes.Stloc_3) return 3;
            return -1;
        }
        private static IEnumerable<CodeInstruction> Isolate(IEnumerable<CodeInstruction> input, ILGenerator generator)
        {
            var code = input.ToList();
            int check = Unique(code, i => CallTo(code[i], "MapRenderer", "CheckMapTargets"));
            int geometry = Unique(code, i => i + 4 < code.Count && (code[i].operand as FieldInfo)?.Name == "maxTilesX" && code[i + 1].opcode == OpCodes.Ldc_I4 && Equals(code[i + 1].operand, 840) && Local(code[i + 4]) == 33);
            int p = Unique(code, i => CallTo(code[i], "Main", "TriggerPing"));
            int u = Unique(code, i => i + 1 < code.Count && CallTo(code[i], "Main", "get_UIScale") && Local(code[i + 1]) == 76);
            Require(code.Count(c => CallTo(c, "MapIconOverlay", "Draw")) == 3, "fixed map has exactly three native icon calls");
            int d = Unique(code, i => i >= 20 && CallTo(code[i], "MapIconOverlay", "Draw") && Local(code[i - 3]) == 76 && Local(code[i - 4]) == 6);
            Require((code[p - 34].operand as FieldInfo)?.Name == "mouseLeft" && (code[p + 1].operand as FieldInfo)?.Name == "_lastPingMouseDownTime" && (code[p + 3].operand as FieldInfo)?.Name == "_lastPingMousePosition", "native ping stack and branch extent");
            Require(Local(code[geometry - 6]) == 3 && Local(code[geometry - 1]) == 3 && (code[d - 20].operand as FieldInfo)?.Name == "MapIcons", "native geometry finish and fullscreen parameter start");
            Label pingLabel = Target(code, p - 34, generator), scaleLabel = Target(code, u - 17, generator), iconLabel = Target(code, d - 20, generator);
            code[check].opcode = OpCodes.Nop; code[check].operand = null;
            Insert(code, d + 1, new CodeInstruction(OpCodes.Ret)); Insert(code, u + 2, new CodeInstruction(OpCodes.Br, iconLabel));
            Insert(code, p + 4, new CodeInstruction(OpCodes.Br, scaleLabel)); Insert(code, geometry, new CodeInstruction(OpCodes.Br, pingLabel)); return code;
        }
        private static int Unique(List<CodeInstruction> code, Func<int, bool> predicate)
        { var matches = Enumerable.Range(0, code.Count).Where(predicate).ToArray(); Require(matches.Length == 1, "unique fixed native DrawMap isolation boundary"); return matches[0]; }
        private static Label Target(List<CodeInstruction> code, int index, ILGenerator generator)
        { Require(code[index].blocks.Count == 0, "no exception scope crosses isolated target"); Label label = generator.DefineLabel(); code[index].labels.Add(label); return label; }
        private static void Insert(List<CodeInstruction> code, int index, CodeInstruction instruction)
        { Require(code[index].blocks.Count == 0, "no exception scope crosses isolated jump"); instruction.labels.AddRange(code[index].labels); code[index].labels.Clear(); code.Insert(index, instruction); }
    }
}
