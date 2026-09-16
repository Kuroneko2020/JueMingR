using System;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Exploration;
using Terraria;
using Terraria.Map;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeExplorationBoundaries
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static bool clearOnPreserve;
        private static int clears, loads;
        internal static void Run(ExplorationCounter counter)
        {
            var isolation = new Harmony("JueMingR.Tests.ExplorationBoundaries");
            var restart = typeof(ExplorationCounter).GetMethod("Restart", Flags, null, new[] { typeof(bool) }, null);
            var load = typeof(WorldMap).GetMethod("Load", Flags);
            try
            {
                isolation.Patch(restart, prefix: new HarmonyMethod(typeof(NativeExplorationBoundaries).GetMethod(nameof(BetweenDecisionAndRestart), Flags)));
                var hook = new HarmonyMethod(typeof(NativeExplorationBoundaries).GetMethod(nameof(LoadBytesSink), Flags)) { priority = Priority.Last };
                isolation.Patch(load, prefix: hook);
                Main.Map.Clear(); Drain(counter); var lit = new MapTile { Light = 255 }; Main.Map.SetTile(1, 1, ref lit); Drain(counter);
                counter.Paused = true; clearOnPreserve = true; counter.Restart();
                Require(clears == 1 && !counter.Current, "batch between baseline decision and restart cannot bless stale epoch");
                counter.Paused = false; Drain(counter); Require(counter.Count == 0, "raced full reset converges to independent zero");
                Main.Map.SetTile(1, 1, ref lit); Drain(counter); var oldReceiver = new WorldMap(128, 128); oldReceiver.Load(); Drain(counter);
                Require(loads == 1 && counter.Count == 0, "old Load receiver invalidates current Main.Map target after isolated native file-load effect");
            }
            finally { clearOnPreserve = false; isolation.UnpatchAll(isolation.Id); }
        }
        private static void BetweenDecisionAndRestart(bool preserveBaseline)
        {
            if (!preserveBaseline || !clearOnPreserve) return; clearOnPreserve = false;
            // Controlled join puts a genuine native batch after the public
            // baseline decision but before the retained-epoch body.
            Exception failure = null; var writer = new Thread(() => { try { Main.Map.Clear(); clears++; } catch (Exception ex) { failure = ex; } }); writer.Start(); Require(writer.Join(3000), "finite batch writer"); if (failure != null) throw failure;
        }
        private static bool LoadBytesSink()
        { loads++; var empty = new MapTile(); Main.Map.SetTile(1, 1, ref empty); return false; }
        private static void Drain(ExplorationCounter counter)
        { for (int i = 0; i < 10000 && (!counter.Current || counter.Scanning); i++) counter.Advance(32768, 64); Require(counter.Current && !counter.Scanning, "boundary converges"); }
    }
}
