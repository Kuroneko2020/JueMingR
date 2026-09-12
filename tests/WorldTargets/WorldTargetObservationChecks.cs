using System;
using JueMingR.Features.WorldTargets;
using JueMingR.Platform.WorldTargets;
using JueMingR.TerrariaHost.WorldTargets;
using Microsoft.Xna.Framework;

namespace Terraria
{
    internal static class WorldTargetObservationChecks
    {
        internal static void Prepare()
        {
            Main.gameMenu = Main.dedServ = false; Main.netMode = 0; Main.screenPosition = Vector2.Zero;
            Main.LocalPlayer = new Player { active = true, accOreFinder = true };
            Main.maxTilesX = 200; Main.maxTilesY = 100; Main.tile = new Tile[200, 100];
            for (int y = 0; y < 100; y++) for (int x = 0; x < 200; x++) Main.tile[x, y] = new Tile();
            Main.sectionManager = new WorldSections(); Main.screenWidth = 800; Main.screenHeight = 600;
            Main.GameViewMatrix.ZoomMatrix = Matrix.Identity;
        }
        internal static void Put(int x, int y, int type, int style = 0)
        { for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) Main.tile[x + dx, y + dy] = new Tile { Active = true, type = (ushort)type, frameX = (short)(style * 36 + dx * 18), frameY = (short)(dy * 18) }; }
        internal static void Run()
        {
            Prepare(); bool session = true; var source = new WorldTargetHostObservation(() => session);
            var feature = new WorldTargetFeature(source); var settings = WorldTargetSettings.Default;
            feature.Configure(settings.WithEnabled(WorldTargetKind.LifeCrystal, true)); feature.OnSessionStarted();
            Put(4, 4, 12); Discover(feature); Check(feature.Targets.Count == 1, "real tile reader finds one complete heart");
            Main.LocalPlayer.accOreFinder = false; feature.Update(17); Check(feature.Targets.Count == 0, "native field false stops observation");
            Main.LocalPlayer.accOreFinder = true; Main.LocalPlayer.dead = true; Discover(feature);
            Check(feature.Targets.Count == 1, "dead active world uses same observation contract");
            // Hidden information text has no reader dependency; hideUI gates only
            // rendering, not desired state or availability of the native field.
            Main.hideUI = true; Discover(feature); Check(feature.Targets.Count == 1, "UI visibility does not erase native detection ability"); Main.hideUI = false;
            Main.tile[5, 5] = null; feature.Update(18); Check(feature.Targets.Count == 0 && Main.tile[5, 5] == null, "null stays null without GetTileSafely mutation");
            Put(4, 4, 12); Main.netMode = 1; Main.sectionManager.Unknown.Add(((long)5 << 32) | 5); Discover(feature);
            Check(feature.Targets.Count == 0, "one unsynchronized cell prevents complete client marker");
            Main.sectionManager.Unknown.Clear(); Discover(feature); Check(feature.Targets.Count == 1, "current synchronized data recovers");
            int reads = Main.sectionManager.Reads; Main.LocalPlayer.accOreFinder = false; feature.Update(19);
            Check(Main.sectionManager.Reads == reads, "no ability performs no section/tile reads"); Main.LocalPlayer.accOreFinder = true;
            foreach (WorldTargetKind kind in Enum.GetValues(typeof(WorldTargetKind))) settings = settings.WithEnabled(kind, true);
            feature.Configure(settings); Put(8, 4, 236, 2); Put(12, 4, 639); Put(16, 4, 751); Put(20, 4, 752); Discover(feature);
            Check(feature.Targets.Count == 5, "all five share one real reader");
            Main.tile[20, 4].Active = false; Main.tile[16, 5].type = 1; feature.Update(30);
            Check(feature.Targets.Count == 3, "removed egg and changed sleeping turtle stop; no item/NPC fallback");
            feature.Configure(WorldTargetSettings.Default.WithEnabled(WorldTargetKind.SleepingDigtoise, true));
            Put(52, 10, 751); Discover(feature);
            Check(feature.Targets.Count == 1 && feature.Targets[0].TileX == 52, "offscreen turtle whose arrows still intersect screen must survive observation culling");
            var edge = feature.Targets[0]; bool visibleArrow = false;
            for (long ticks = 0; ticks < 70000000L; ticks += 1000000L) for (int i = 0; i < 3; i++)
            { var arrow = WorldTargetArrows.At(edge, new WorldTargetAnimation(ticks, 1), i); visibleArrow |= arrow.X - arrow.Length / 2 < 800; }
            Check(visibleArrow, "counterexample has visible arrow pixels during its stable local phase before target enters screen");
            feature.Configure(settings);
            Main.GameViewMatrix.ZoomMatrix = Matrix.CreateTranslation(-400, -300, 0) * Matrix.CreateScale(2) * Matrix.CreateTranslation(400, 300, 0);
            WorldTargetView view; Check(source.TryBegin(out view) && view.X == 6 && view.Width == 38, "inverse centered zoom and enlarged complete arrow margin change actual tile region");
            long geometry = view.GeometryRevision; Main.screenPosition.X = 8; source.TryBegin(out view);
            Check(view.GeometryRevision == geometry, "sub-tile motion never reports resize");
            Main.screenWidth = 900; source.TryBegin(out view); Check(view.GeometryRevision != geometry, "actual screen resize changes geometry identity");
            Main.screenPosition = new Vector2(2000, 0); feature.Update(31); Check(feature.Targets.Count == 0, "teleport immediately withdraws old candidates");
            session = false; reads = Main.sectionManager.Reads; feature.Update(32); Check(Main.sectionManager.Reads == reads, "invalid shared session does no tile work");
            Console.WriteLine("PASS: production world tile adapter, complete objects, native ability gate, client unknown, death, removal and centered zoom.");
        }
        internal static void Discover(WorldTargetFeature feature) { for (ulong i = 0; i < 16; i++) feature.Update(i); }
        internal static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
