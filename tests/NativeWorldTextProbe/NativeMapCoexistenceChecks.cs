using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.MapMarkers;
using JueMingR.Features.DeathHistory;
using JueMingR.Features.Exploration;
using JueMingR.Platform.DeathHistory;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Map;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeMapCoexistenceChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static int markerDraws;
        private static object visibleBounds;
        internal static void Run(object context, object host)
        {
            Require(Main.maxTilesX == 8400 && Main.maxTilesY == 2400, "coexistence uses actual large local map");
            var markers = (MarkerLibrary)Get(host, "Markers"); var deathHost = Get(context, "DeathRecords"); var deaths = (DeathHistory)Get(deathHost, "History");
            Until(() => { Call(context, "UpdateRuntime"); return markers.Loaded && deaths.Snapshot.Known; });
            for (int i = markers.Saved.Records.Count; i < 120; i++)
            { long op = markers.Create(new MarkerRecord((i + 1).ToString("x32"), 50.25 + i % 20 * 30, 100.5 + i / 20 * 30, 8, "共存")); Until(() => { markers.Poll(); return markers.LastOperation == op; }); }
            var epoch = new DateTimeOffset(2026, 9, 17, 0, 0, 0, TimeSpan.Zero);
            for (int i = 0; i < 1024; i++)
            { var fact = new DeathFact(DeathEventId.Create(epoch.AddSeconds(i), new Guid(i + 1, 0, 0, new byte[8])), TimeSpan.Zero, true, (50 + i % 40 * 15) * 16, (100 + i / 40 * 15) * 16, "地图共存隔离事件"); Until(() => { Call(context, "UpdateRuntime"); return deaths.Accept(fact); }); }
            Main.screenWidth = 1000; Main.screenHeight = 800; Main.mapFullscreen = true; Main.mouseX = Main.mouseY = -100;
            Call(host, "SetMarkers", true); Call(host, "SetDynamic", true); Call(deathHost, "SetEnabled", true); Call(deathHost, "SetCount", 1024);
            Until(() => { Call(context, "UpdateRuntime"); return deaths.Snapshot.Markers.Count == 1024; });
            var map = Get(deathHost, "Map"); var drawing = Get(map, "drawing").GetType(); var deathMethod = map.GetType().GetMethod("Draw", Flags);
            var iconMethod = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Map.MarkerIcons").GetMethod("Draw", Flags);
            var boundsType = iconMethod.GetParameters()[5].ParameterType.GetElementType();
            visibleBounds = Activator.CreateInstance(boundsType, Flags, null, new object[] { 100f, 100f, 28f, 28f }, null);
            var begin = (Action)Delegate.CreateDelegate(typeof(Action), drawing.GetMethod("BeginMap", Flags));
            var end = (Func<Exception, Exception>)Delegate.CreateDelegate(typeof(Func<Exception, Exception>), drawing.GetMethod("EndMap", Flags));
            var update = (Action)Delegate.CreateDelegate(typeof(Action), context, context.GetType().GetMethod("UpdateRuntime", Flags));
            var isolation = new Harmony("JueMingR.Tests.MapCoexistence"); var oldIcons = Main.MapIcons; Main.MapIcons = new MapIconOverlay();
            try
            {
                // CPU workload only: preserve both production loops and shared
                // actual MapIcons callback; replace resource/draw leaves only.
                isolation.Patch(deathMethod, transpiler: new HarmonyMethod(typeof(NativeDeathMapLoopChecks).GetMethod("IsolateGraphics", Flags)));
                isolation.Patch(iconMethod, prefix: new HarmonyMethod(typeof(NativeMapCoexistenceChecks).GetMethod(nameof(Icon), Flags)));
                var projections = map.GetType().GetField("Projections", Flags); long before = projections == null ? 0 : (long)projections.GetValue(map); markerDraws = 0;
                for (int tick = 0; tick < 2000; tick++)
                {
                    var tile = new MapTile { Light = (byte)(tick % 2 == 0 ? 255 : 0) }; Main.Map.SetTile(10, 10, ref tile); update();
                    begin(); try { string text = "native"; Main.MapIcons.Draw(Vector2.Zero, Vector2.Zero, null, 1, 1, 255, ref text); } finally { end(null); }
                }
                Require(markerDraws == 2000 * 120 && (projections == null || (long)projections.GetValue(map) - before == 2000L * 1024), "shared draw visits exactly 120 markers and 1024 death candidates per frame");
                var counter = (ExplorationCounter)Get(host, "Counter"); Until(() => { update(); return counter.Current; });
                long answer = 0; for (int y = 0; y < Main.maxTilesY; y++) for (int x = 0; x < Main.maxTilesX; x++) if (Main.Map[x, y].Light > 0) answer++;
                Require(counter.Count == answer && markers.Saved.Records.Count == 120 && deaths.Snapshot.Count >= 1024, "simultaneous changing map converges without changing marker/death assets");
                Console.WriteLine("PASS: large map + 120 markers + 1024 death points + dynamic changes, 2000 actual shared callbacks; GPU leaves isolated, no render timing claim.");
            }
            finally { isolation.Unpatch(deathMethod, HarmonyPatchType.All, isolation.Id); isolation.Unpatch(iconMethod, HarmonyPatchType.All, isolation.Id); Main.MapIcons = oldIcons; Main.mapFullscreen = false; visibleBounds = null; }
        }
        private static bool Icon(object[] __args, ref bool __result) { markerDraws++; __args[5] = visibleBounds; __result = true; return false; }
        private static void Until(Func<bool> done) { if (!SpinWait.SpinUntil(done, 10000)) throw new TimeoutException("map coexistence state"); }
    }
}
