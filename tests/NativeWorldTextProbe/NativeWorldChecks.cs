using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.WorldObjectText;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;
using JueMingR.TerrariaHost.World;
using JueMingR.TerrariaHost.WorldObjectText;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    internal static class NativeWorldChecks
    {
        internal static void Run(ProbeGraphics graphics, string output)
        {
            Main.gameMenu = Main.dedServ = Main.hideUI = Main.mapFullscreen = Main.inFancyUI = Main.onlyDrawFancyUI = Main.ingameOptionsWindow = false;
            Main.netMode = Main.myPlayer = 0; Main.screenWidth = 960; Main.screenHeight = 640; Main.screenPosition = Vector2.Zero;
            Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(graphics.GraphicsDevice);
            Main.player[0] = new Player { active = true, accOreFinder = true, position = new Vector2(820, 520) };
            Main.maxTilesX = 256; Main.maxTilesY = 128; Main.tile = new Tile[256, 128]; Main.sign = new Sign[32000];
            var world = new WorldTileObservation(() => true); var source = new WorldObjectHostObservation(world);
            Put(10, 10, 21, 10, 2); Put(14, 10, 88, 0, 3); Put(17, 10, 88, 0, 3); Put(21, 10, 467, 4, 2);
            WorldObjectView view; world.BeginTick(); source.TryBegin(true, false, out view);
            WorldObject ivy, dresser, dead; Resolve(source, 11, 11, out ivy); Resolve(source, 16, 11, out dresser); Resolve(source, 22, 11, out dead);
            string text; source.TryText(ivy, out text); Require(text == "Ivy Chest", "real Host resolves exact Ivy Chest name");
            source.TryText(dresser, out text); Require(text == "Dresser" && dresser.TileX == 14 && dresser.Width == 3, "real native dresser table and six-cell origin");
            source.TryText(dead, out text); Require(text == "Dead Man's Chest", "real native special 467/3988 name");
            var chest = Register(0, 10, 10); chest.name = "My chest"; source.TryText(ivy, out text); Require(text == "My chest", "native coordinate dictionary supplies current custom name immediately");
            chest.name = String.Empty; source.TryText(ivy, out text); Require(text == "Ivy Chest", "clearing custom name updates outer label source immediately");
            var discovery = new WorldObjectDiscovery(source); var always = WorldObjectSettings.Default.WithMode(WorldObjectKind.Chest, WorldObjectMode.Always);
            for (int i = 0; i < 8; i++) { world.BeginTick(); discovery.Update(always, null); }
            Require(discovery.Candidates.Count == 4 && discovery.Candidates.Count(c => c.Text == "Dresser") == 2, "adjacent same-name dressers remain two single labels");
            ObserveOpened(world);
            SelectionAndRecovery(graphics, world, source, output);
            Console.WriteLine("PASS: actual Host native names/dictionary, full dresser geometry, lazy all-off observer, real history file, >K selection and cold-job recovery.");
        }
        private static void ObserveOpened(WorldTileObservation world)
        {
            var player = Main.LocalPlayer; player.chest = -1; Main.playerInventory = false;
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(Terraria.Program.SavePath, "probe.plr"), false) { Player = player };
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath, "probe.wld"), false) { UniqueId = new Guid("00000000-0000-0000-0000-000000000063") };
            Main.ServerSideCharacter = false;
            int factories = 0; CountingStorage storage = null;
            string directory = Path.Combine(Terraria.Program.SavePath, "history-probe");
            var history = new OpenedPositionHistory(pair => { Interlocked.Increment(ref factories); return storage = new CountingStorage(new AtomicFileDocument(Path.Combine(directory, pair + ".json"), OpenedPositionCodec.MaximumBytes, true)); });
            var runtime = new SingleFeatureRuntime(new ProbeSession(), new EmptyFeature()); runtime.Update(0);
            bool automatic = false; var observer = new OpenedContainerObserver(runtime, history, world, () => automatic); observer.Start();
            try
            {
                for (int i = 0; i < 20; i++) { world.BeginTick(); observer.Update(false); }
                Require(factories == 0 && !history.HasPair, "all-off/no-open never cold-reads pair history");
                Put(24, 20, 21, 0, 2); Register(1, 24, 20); player.chest = 1; player.chestX = 24; player.chestY = 20; Main.playerInventory = true;
                world.BeginTick(); observer.Update(false);
                Require(history.Contains(WorldObject.PositionKey(24, 20)), "normal committed fields register while all displays are off");
                Until(() => { history.Poll(); return history.Status == PreferenceStatus.Saved; });
                for (int i = 0; i < 100; i++) { world.BeginTick(); observer.Update(false); }
                Main.playerInventory = false; observer.Update(false); Main.playerInventory = true; observer.Update(false);
                Require(storage.Writes == 1, "holding and reopening a confirmed coordinate do not rewrite");
                Put(28, 20, 21, 0, 2); Register(2, 28, 20); player.chest = 2; player.chestX = 28;
                automatic = true; world.BeginTick(); observer.Update(false); automatic = false;
                Require(!history.Contains(WorldObject.PositionKey(28, 20)), "automatic operation scope cannot create an open fact");
                player.chest = -2; observer.Update(false); Require(!history.Contains(WorldObject.PositionKey(28, 20)), "bank state cannot register a world coordinate");
                player.chest = -1; Main.playerInventory = false;
            }
            finally { observer.End(); Require(history.Stop(5000), "isolated actual history worker closes"); }
            var files = Directory.GetFiles(directory, "*.json"); Require(files.Length == 1, "one natural pair file");
            var pairKey = Path.GetFileNameWithoutExtension(files[0]); var saved = new OpenedPositionCodec().Decode(File.ReadAllBytes(files[0]), pairKey);
            Require(saved.Count == 1 && saved.Contains(WorldObject.PositionKey(24, 20)), "actual Host observation persists only the legitimate position");
        }
        private static void SelectionAndRecovery(ProbeGraphics graphics, WorldTileObservation world, WorldObjectHostObservation source, string output)
        {
            Main.tile = new Tile[256, 128]; Main.chest = new Chest[8000]; Main.sign = new Sign[32000]; source.EndSession();
            for (int y = 0; y < 12; y++) for (int x = 0; x < 25; x++) Put(4 + x * 2, 10 + y * 2, 21, 0, 2);
            var discovery = new WorldObjectDiscovery(source); var layer = new WorldObjectTextWorldLayer(discovery, () => true); discovery.SetPresentationGate(layer.MayPresent);
            var settings = WorldObjectSettings.Default.WithMode(WorldObjectKind.Chest, WorldObjectMode.Always);
            for (int i = 0; i < 80; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            Require(discovery.Candidates.Count == 272, "real source retains bounded nearest reserve from 300 complete containers");
            double previous = -1;
            foreach (var candidate in discovery.Candidates)
            { double dx = candidate.Value.CenterX - Main.LocalPlayer.Center.X, dy = candidate.Value.CenterY - Main.LocalPlayer.Center.Y; double distance = dx * dx + dy * dy; Require(distance >= previous, "actual native candidates use player-center distance"); previous = distance; }
            graphics.DrawWorld(layer, Path.Combine(output, "native-world-k.png"));
            Require(layer.Failure == null && layer.LastDrawn == 240, "production world consumer draws K=240 after nearby selection");
            // More than the reserve can be geometrically visible at the top edge
            // while their text is above it. They cannot lock out farther text.
            Main.tile = new Tile[256, 128]; Main.sign = new Sign[32000]; source.EndSession(); discovery.Clear(); layer.Clear();
            for (int i = 0; i < 60; i++) { Put(i * 2, 0, 55, 0, 2); Main.sign[i] = new Sign { x = i * 2, y = 0, text = "top" }; }
            for (int i = 0; i < 40; i++) { int x = 2 + i % 20 * 2, y = 15 + i / 20 * 4; Put(x, y, 55, 0, 2); Main.sign[60 + i] = new Sign { x = x, y = y, text = "visible" }; }
            Main.LocalPlayer.position = Vector2.Zero;
            settings = WorldObjectSettings.Default.WithMode(WorldObjectKind.Sign, WorldObjectMode.Lines);
            for (int i = 0; i < 70; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            graphics.DrawWorld(layer, Path.Combine(output, "native-world-backfill.png"));
            Require(layer.Failure == null && layer.LastDrawn == 40, "fully cropped nearest objects cannot consume the sign reserve forever");
            // Keep a chest consumer alive so a sign-only disable does not use the
            // easy all-off Clear path. Long tags make preparation genuinely cold.
            Put(50, 20, 21, 0, 2);
            for (int i = 60; i < 68; i++) Main.sign[i].text = "[unknown:" + new string('x', 5000) + "]";
            settings = settings.WithMode(WorldObjectKind.Chest, WorldObjectMode.Always);
            Main.LocalPlayer.accOreFinder = true;
            for (int i = 0; i < 2; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            var off = settings.WithMode(WorldObjectKind.Sign, WorldObjectMode.Off); world.BeginTick(); discovery.Update(off, null); layer.Prepare(off);
            for (int i = 0; i < 180; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            graphics.DrawWorld(layer, Path.Combine(output, "native-world-recovery.png"));
            Require(layer.Failure == null && layer.LastDrawn == 41, "cancelled cold sign jobs recover while another kind stays enabled");
            layer.Clear(); discovery.Clear();
        }
        private static Chest Register(int index, int x, int y)
        {
            var chest = Chest.CreateOutOfArray(index, x, y, 40);
            typeof(Chest).GetMethod("Assign", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { chest }); return chest;
        }
        private static void Put(int x, int y, int type, int style, int width)
        { for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < width; dx++) { var tile = new Tile { type = (ushort)type, frameX = (short)(style * width * 18 + dx * 18), frameY = (short)(dy * 18) }; tile.active(true); Main.tile[x + dx, y + dy] = tile; } }
        private static void Resolve(WorldObjectHostObservation source, int x, int y, out WorldObject value)
        { Require(WorldObjectResolver.TryResolve(x, y, source.Read(x, y), source.Read, out value), "real complete object resolves"); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Until(Func<bool> condition)
        { var clock = System.Diagnostics.Stopwatch.StartNew(); while (!condition()) { if (clock.ElapsedMilliseconds > 5000) throw new TimeoutException("native history boundary"); Thread.Sleep(1); } }
        private sealed class ProbeSession : IGameSessionProbe { public bool IsSessionActive { get { return true; } } }
        private sealed class EmptyFeature : IRuntimeFeature { public bool Enabled { get { return true; } } public void OnSessionStarted() { } public void OnSessionEnded() { } public void Update(ulong tick) { } public void FailClosed() { } }
        private sealed class CountingStorage : IPreferenceStorage
        {
            private readonly IPreferenceStorage inner; internal int Writes;
            internal CountingStorage(IPreferenceStorage inner) { this.inner = inner; }
            public PreferenceReadResult Read() { return inner.Read(); }
            public PreferenceWriteResult Write(string identity, byte[] bytes) { Interlocked.Increment(ref Writes); return inner.Write(identity, bytes); }
            public void Dispose() { inner.Dispose(); }
        }
    }
}
