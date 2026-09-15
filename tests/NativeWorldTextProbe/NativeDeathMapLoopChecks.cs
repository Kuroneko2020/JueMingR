using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.DeathHistory;
using JueMingR.Platform.DeathHistory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeDeathMapLoopChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static int draws;
        internal static void Run(object context, object host)
        {
            var history = (DeathHistory)Get(host, "History");
            var epoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            for (int i = 0; i < 1040; i++)
            {
                var fact = new DeathFact(DeathEventId.Create(epoch.AddSeconds(-i), new Guid(i + 1, 0, 0, new byte[8])), TimeSpan.Zero, true, (i % 50 + 10) * 16, (i / 50 + 10) * 16, "地图隔离记录 " + i);
                try { Until(() => { Call(context, "UpdateRuntime"); return history.Accept(fact); }); }
                catch (Exception e) { throw new Exception("map fixture admission " + i + ", count=" + history.Snapshot.Count + ", pending=" + history.Snapshot.Pending + ", error=" + history.Error + "/" + history.Snapshot.Error, e); }
            }
            Until(() => history.Snapshot.Count == 1040 && history.Snapshot.Pending == 0);
            object map = Get(host, "Map"); Type type = map.GetType(); var method = type.GetMethod("Draw", Flags);
            var isolation = new Harmony("JueMingR.Tests.DeathMapGraphicsSink");
            try
            {
                // Skip only native GPU resource acquisition and replace texture
                // size/terminal SpriteBatch.Draw. Preserve the production K
                // loop, projection, clipping, hit selection and demand changes.
                isolation.Patch(method, transpiler: new HarmonyMethod(typeof(NativeDeathMapLoopChecks).GetMethod(nameof(IsolateGraphics), Flags)));
                Main.screenWidth = 1000; Main.screenHeight = 800; Main.mouseX = Main.mouseY = -100;
                Main.mapFullscreen = true; Call(host, "SetEnabled", true);
                Action<Vector2> draw = offset =>
                {
                    type.GetMethod("BeginMap", Flags).Invoke(null, null);
                    try { type.GetMethod("IconsDrawn", Flags).Invoke(null, new object[] { Main.MapIcons, Vector2.Zero, offset, null, 1f, 1f, 255, "native", true }); }
                    finally { type.GetMethod("EndMap", Flags).Invoke(null, new object[] { null }); }
                    Require(GetOptional(map, "Failure") == null, "production map adapter has no failure with isolated graphics sink: " + GetOptional(map, "Failure"));
                };
                foreach (int k in new[] { 128, 256, 512, 1024 })
                {
                    Call(host, "SetCount", k); Call(context, "UpdateRuntime"); Until(() => history.Snapshot.Request == history.RequestId && history.Snapshot.Markers.Count == k);
                    long before = (long)Get(map, "Projections"); draws = 0;
                    for (int tick = 0; tick < 20; tick++) draw(Vector2.Zero);
                    Require((long)Get(map, "Projections") - before == 20L * k && draws == 20 * k, "actual stable map visits exactly K candidates, independent of N=1040");
                    draws = 0; before = (long)Get(map, "Projections"); draw(new Vector2(-10000, -10000));
                    Require(draws == 0 && (long)Get(map, "Projections") - before == k && history.Snapshot.Count == 1040, "offscreen recent candidates stay in K and do not fall back to older visible records");
                }
                Require(history.Snapshot.Markers.Count == 1024, "stale large result is present before lowering quantity");
                Call(host, "SetCount", 128); long prior = (long)Get(map, "Projections"); draws = 0; draw(Vector2.Zero);
                Require(draws == 128 && (long)Get(map, "Projections") - prior == 128, "lower current quantity immediately bounds still-published 1024 result");
                var newest = history.Snapshot.Markers[0]; Main.mouseX = (int)(newest.X / 16); Main.mouseY = (int)(newest.Y / 16);
                draw(Vector2.Zero); Require((string)Get(map, "hoveredId") == newest.EventId, "overlapping hits choose newest occurrence, including UTC rollback");
                long request = history.RequestId; draw(Vector2.Zero); Require(history.RequestId == request, "stable hover retains one selection request");
                foreach (string gate in new[] { "map", "hide", "ready", "enabled" })
                {
                    Main.mapFullscreen = gate != "map"; Main.hideUI = gate == "hide"; Set(map, "Ready", gate != "ready"); Call(host, "SetEnabled", gate != "enabled");
                    Call(context, "UpdateRuntime"); Until(() => history.Snapshot.Request == history.RequestId);
                    prior = (long)Get(map, "Projections"); draw(Vector2.Zero);
                    Require(history.Snapshot.Markers.Count == 0 && (long)Get(map, "Projections") == prior, "closed/hidden/unavailable/disabled map cancels candidates and projections: " + gate);
                }
                Console.WriteLine("PASS: actual map CPU loop N=1040, each K at 20*K projections; clipping, stale 1024->128, overlap/latest hover, and all closed gates. GPU calls isolated; no pixel claim.");
            }
            finally { isolation.Unpatch(method, HarmonyPatchType.All, isolation.Id); Main.hideUI = Main.mapFullscreen = false; Set(map, "Ready", true); Call(host, "SetEnabled", false); }
        }
        private static IEnumerable<CodeInstruction> IsolateGraphics(IEnumerable<CodeInstruction> input, ILGenerator generator)
        {
            var code = input.ToList();
            int resource = code.FindIndex(c => c.opcode == OpCodes.Ldsfld && (c.operand as FieldInfo)?.Name == "MapDeath");
            int view = code.FindIndex(resource + 1, c => (c.operand as ConstructorInfo)?.DeclaringType?.Name == "F5Rect");
            Require(resource >= 0 && view >= resource + 4 && code[view - 6].opcode == OpCodes.Ldc_R4 && code[view - 5].opcode == OpCodes.Ldc_R4, "fixed GPU acquisition boundary before viewport rectangle: " + String.Join("; ", code.Skip(Math.Max(0, resource)).Take(35)));
            int start = view - 6;
            if (code[view].opcode == OpCodes.Call) { Require(code[start - 1].opcode == OpCodes.Ldloca_S || code[start - 1].opcode == OpCodes.Ldloca, "viewport value-type address"); start--; }
            var target = generator.DefineLabel(); code[start].labels.Add(target);
            var jump = new CodeInstruction(OpCodes.Br, target); jump.labels.AddRange(code[resource].labels); code[resource].labels.Clear(); code.Insert(resource, jump);
            int sizes = 0, sinks = 0;
            foreach (var instruction in code)
            {
                var called = instruction.operand as MethodInfo; if (called == null) continue;
                if (called.DeclaringType == typeof(Texture2D) && (called.Name == "get_Width" || called.Name == "get_Height"))
                { instruction.opcode = OpCodes.Call; instruction.operand = typeof(NativeDeathMapLoopChecks).GetMethod(nameof(Size), Flags); sizes++; }
                if (called.DeclaringType == typeof(SpriteBatch) && called.Name == "Draw")
                { instruction.opcode = OpCodes.Call; instruction.operand = typeof(NativeDeathMapLoopChecks).GetMethod(nameof(DrawSink), Flags); sinks++; }
            }
            Require(sizes == 4 && sinks == 1, "only four texture size reads and one terminal draw replaced"); return code;
        }
        private static int Size(Texture2D texture) { return 32; }
        private static void DrawSink(SpriteBatch batch, Texture2D texture, Vector2 position, Rectangle? source, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float depth) { draws++; }
        private static void Until(Func<bool> done)
        { var timer = System.Diagnostics.Stopwatch.StartNew(); while (!done()) { if (timer.ElapsedMilliseconds > 10000) throw new TimeoutException("death map CPU result"); Thread.Sleep(2); } }
    }
}
