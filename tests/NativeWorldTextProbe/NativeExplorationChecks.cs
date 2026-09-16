using System;
using System.IO;
using System.Reflection;
using JueMingR.Features.Exploration;
using Microsoft.Xna.Framework;
using Terraria.Graphics.Light;
using Terraria;
using Terraria.Map;

namespace NativeWorldTextProbe
{
    internal static class NativeExplorationChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run()
        {
            string config = Program.ProductionConfiguration;
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/" + config + "/work/bin/JueMingR.TerrariaHost/x86/" + config + "/net472/JueMingR.TerrariaHost.dll"));
            var type = assembly.GetType("JueMingR.TerrariaHost.Map.ExplorationMapHooks", true);
            object hooks = Activator.CreateInstance(type, true);
            var map = new WorldMap(128, 128); int reads = 0;
            var count = new ExplorationCounter(128, 128, (x, y) => { reads++; return map.IsRevealed(x, y); });
            Main.maxTilesX = Main.maxTilesY = 128; Main.Map = map; Main.tile = new Tile[128, 128];
            Main.sectionManager = new WorldSections(1, 1); Main.sectionManager.SetAllSectionsLoaded(); MapHelper.Initialize();
            for (int x = 0; x < 128; x++) for (int y = 0; y < 128; y++) { var tile = new Tile(); tile.active(true); tile.type = 1; Main.tile[x, y] = tile; }
            try
            {
                type.GetMethod("Install", Flags).Invoke(hooks, null);
                Require((bool)type.GetProperty("Ready", Flags).GetValue(hooks), "actual store matcher installs: " + type.GetProperty("Failure", Flags).GetValue(hooks));
                type.GetMethod("Bind", Flags).Invoke(hooks, new object[] { map, count }); count.SetDynamic(true); Drain(count);
                Require(count.Count == 0, "independent empty baseline");
                var lit = new MapTile { Light = 255 }; var empty = new MapTile(); map.SetTile(60, 60, ref lit); Drain(count); Require(count.Count == 1, "actual SetTile reveals one");
                map.UpdateLighting(70, 70, 255); Drain(count); Require(count.Count == 2, "actual UpdateLighting adds one");
                int stable = reads; for (int i = 0; i < 2000; i++) { map.UpdateLighting(70, 70, 255); count.Advance(4096, 64); }
                Require(reads == stable && count.Count == 2, "same native revealed input causes no recount");
                map.SetTile(60, 60, ref empty); Drain(count); Require(count.Count == 1, "actual native true to false");
                var queued = map[70, 70]; queued.UpdateQueued = true; map.SetTile(70, 70, ref queued); Drain(count);
                Main.tile[70, 70] = null; map.UpdateType(70, 70); Drain(count); Require(count.Count == 0, "actual UpdateType can erase reveal for missing Tile");
                map.Update(71, 71, 200); Drain(count); Require(count.Count == 1, "actual direct Update observed");
                map.Clear(); Drain(count); Require(count.Count == 0, "actual Clear invalidates and rebuilds");
                count.SetDynamic(false); stable = reads; for (int i = 0; i < 2000; i++) { map.SetTile(60, 60, ref lit); count.Advance(4096, 64); }
                Require(reads == stable, "disabled observer adds no map reads");
                count.SetDynamic(true); Drain(count); Require(count.Count == 1, "re-enable independently rebuilds actual map");
                map.Clear(); Drain(count); Main.mapEnabled = true; MapHelper.sceneArea = new Rectangle(0, 0, 128, 128);
                var lighting = new LightingEngine(); object light = lighting.GetType().GetField("_activeLightMap", Flags).GetValue(lighting);
                light.GetType().GetMethod("SetSize", Flags).Invoke(light, new object[] { 128, 128 });
                var indexer = light.GetType().GetProperty("Item", Flags);
                for (int x = 0; x < 128; x++) for (int y = 0; y < 128; y++) indexer.SetValue(light, Vector3.One, new object[] { x, y });
                Main.tile[70, 70] = new Tile(); Main.tile[70, 70].active(true); Main.tile[70, 70].type = 1;
                lighting.GetType().GetField("_activeProcessedArea", Flags).SetValue(lighting, new Rectangle(0, 0, 128, 128));
                var export = (Action)Delegate.CreateDelegate(typeof(Action), lighting, lighting.GetType().GetMethod("ExportToMiniMap", Flags)); export(); Drain(count);
                long independent = 0; for (int x = 0; x < 128; x++) for (int y = 0; y < 128; y++) if (map[x, y].Light > 0) independent++;
                Require(independent == 2304 && count.Count == independent, "actual LightingEngine to FastParallel writers converge to independent full answer");
                Console.WriteLine("Native lighting partitions=" + Math.Min(Math.Max(1, Environment.ProcessorCount - 1), 48) + "; independently revealed=" + independent + ".");
                NativeExplorationBoundaries.Run(count);
                Console.WriteLine("PASS: actual WorldMap store hooks, reveal/unreveal, UpdateType, bulk Clear, disabled gate and 2000 unchanged native calls.");
            }
            finally { ((IDisposable)hooks).Dispose(); }
        }
        private static void Drain(ExplorationCounter value)
        { for (int i = 0; i < 10000 && (!value.Complete || value.Pending); i++) value.Advance(32768, 64); Require(value.Complete && !value.Pending, "native count converges"); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
