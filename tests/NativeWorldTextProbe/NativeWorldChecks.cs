using System;
using System.Collections.Generic;
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
            BlankPrefixProgress(world, source);
            RepresentativeScene(graphics, output, world, source);
            EmptyCandidateFontRecovery(graphics, world, source);
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
            var discovery = new WorldObjectDiscovery(source); var layer = new WorldObjectTextWorldLayer(discovery, () => true); discovery.SetPresentationGate(layer.MayPresent, layer.IsPrepared);
            var settings = WorldObjectSettings.Default.WithMode(WorldObjectKind.Chest, WorldObjectMode.Always);
            for (int i = 0; i < 80; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            Require(discovery.Candidates.Count == 272, "real source retains bounded nearest reserve from 300 complete containers");
            double previous = -1;
            foreach (var candidate in discovery.Candidates)
            { double dx = candidate.Value.CenterX - Main.LocalPlayer.Center.X, dy = candidate.Value.CenterY - Main.LocalPlayer.Center.Y; double distance = dx * dx + dy * dy; Require(distance >= previous, "actual native candidates use player-center distance"); previous = distance; }
            graphics.DrawWorld(layer, Path.Combine(output, "native-world-k.png"));
            Require(layer.Failure == null && layer.LastDrawn == 240, "production world consumer draws K=240 after nearby selection");
            var all = new List<Point>();
            for (int y = 0; y < 12; y++) for (int x = 0; x < 25; x++) all.Add(new Point(4 + x * 2, 10 + y * 2));
            AssertNearest(discovery, layer, all, "far-first/near-last");
            long[] initial = discovery.Candidates.Take(240).Select(c => c.Value.Key).ToArray();
            Main.LocalPlayer.position = new Vector2(100, 180);
            for (int i = 0; i < 120; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            AssertNearest(discovery, layer, all, "player moved without resetting discovery");
            Require(!initial.SequenceEqual(discovery.Candidates.Take(240).Select(c => c.Value.Key)), "movement replaces the actual visible list");
            for (int i = 0; i < 10; i++) { var point = new Point(4 + i * 2, 7); all.Add(point); Put(point.X, point.Y, 21, 0, 2); }
            for (int i = 0; i < 120; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            AssertNearest(discovery, layer, all, "new nearer objects replace already displayed farther objects");
            var removed = discovery.Candidates[0].Value;
            all.Remove(new Point(removed.TileX, removed.TileY));
            for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) Main.tile[removed.TileX + dx, removed.TileY + dy].active(false);
            world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings);
            Require(!discovery.Candidates.Any(c => c.Value.Key == removed.Key), "confirmed removed object leaves the actual list immediately");
            for (int i = 0; i < 120; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            AssertNearest(discovery, layer, all, "removed nearest object is backfilled");
            graphics.DrawFrame(layer);
            Require(layer.Failure == null && layer.LastDrawn == 240, "updated exact list still draws the full K");
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
            for (int i = 0; i < 60; i++) { Put(i * 2, 2, 55, 0, 2); Main.sign[i] = new Sign { x = i * 2, y = 2, text = "A\n\n\n\n\n\n\n\n\n" }; }
            settings = settings.WithMode(WorldObjectKind.Sign, WorldObjectMode.All);
            for (int i = 0; i < 90; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            Require(discovery.Candidates.Count(c => c.Value.Kind == WorldObjectKind.Sign && c.Value.TileY >= 15) == 40, "offscreen ink with onscreen blank lines cannot hold nearest slots");
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
        private static void AssertNearest(WorldObjectDiscovery discovery, WorldObjectTextWorldLayer layer, List<Point> all, string context)
        {
            // All fixtures and their short text lie in the viewport. This oracle
            // sorts the COMPLETE input, independently of the production reserve.
            var center = Main.LocalPlayer.Center;
            long[] expected = all.OrderBy(p => Math.Pow(p.X * 16 + 16 - center.X, 2) + Math.Pow(p.Y * 16 + 16 - center.Y, 2))
                .ThenBy(p => p.X).ThenBy(p => p.Y).Take(272).Select(p => WorldObject.PositionKey(p.X, p.Y)).ToArray();
            Require(discovery.SelectedCount == expected.Length && discovery.Candidates.Take(discovery.SelectedCount).Select(c => c.Value.Key).SequenceEqual(expected), "complete-input nearest oracle: " + context);
            var packets = (Array)typeof(WorldObjectTextWorldLayer).GetField("packets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layer);
            long[] rendered = Enumerable.Range(0, 240).Select(i => { var packet = packets.GetValue(i); return ((WorldObject)packet.GetType().GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(packet)).Key; }).ToArray();
            Require(rendered.SequenceEqual(expected.Take(240)), "prepared Draw list matches exact nearest 240: " + context);
        }
        private static void BlankPrefixProgress(WorldTileObservation world, WorldObjectHostObservation source)
        {
            Main.screenWidth = 1280; Main.screenHeight = 960; Main.screenPosition = Vector2.Zero; Main.LocalPlayer.position = Vector2.Zero;
            Main.tile = new Tile[256, 128]; Main.sign = new Sign[32000]; source.EndSession();
            string blank = "[c/ff0000:" + new string(' ', 16384) + "]";
            for (int i = 0; i < 600; i++) { int x = 2 + i % 24 * 2, y = 2 + i / 24 * 2; Put(x, y, 55, 0, 2); Main.sign[i] = new Sign { x = x, y = y, text = blank }; }
            Put(70, 54, 55, 0, 2); Main.sign[600] = new Sign { x = 70, y = 54, text = "A" };
            var discovery = new WorldObjectDiscovery(source); var layer = new WorldObjectTextWorldLayer(discovery, () => true); discovery.SetPresentationGate(layer.MayPresent, layer.IsPrepared);
            var settings = WorldObjectSettings.Default.WithMode(WorldObjectKind.Sign, WorldObjectMode.Characters).With(WorldObjectSettings.Default.Style(WorldObjectKind.Sign).WithMode(WorldObjectMode.Characters).WithLimits(3, 1));
            for (int i = 0; i < 3200; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            Require(discovery.Candidates.Any(c => c.Value.TileX == 70 && c.Value.TileY == 54), "more than rejection capacity of cold blank text cannot permanently starve a farther valid sign");
            Require(layer.Failure == null, "blank-prefix progress has no layout failure");
            layer.Clear(); discovery.Clear(); Main.screenWidth = 960; Main.screenHeight = 640;
        }
        private static void RepresentativeScene(ProbeGraphics graphics, string output, WorldTileObservation world, WorldObjectHostObservation source)
        {
            Main.tile = new Tile[256, 128]; Main.sign = new Sign[32000]; Main.chest = new Chest[8000]; source.EndSession();
            Main.screenPosition = Vector2.Zero; Main.LocalPlayer.position = new Vector2(450, 400);
            Put(8, 12, 21, 10, 2); Put(24, 12, 88, 0, 3); Put(27, 12, 88, 0, 3); Put(44, 12, 467, 4, 2);
            Put(18, 28, 55, 0, 2); Put(46, 28, 85, 0, 2);
            Main.sign[0] = new Sign { x = 18, y = 28, text = "中文牌子\n[c/66ff99:color] [i/s20:8]\n\nÁ emoji 😀\n" + new string('W', 1000) };
            Main.sign[1] = new Sign { x = 46, y = 28, text = "墓碑 Tombstone\nRemember this place." };
            var discovery = new WorldObjectDiscovery(source); var layer = new WorldObjectTextWorldLayer(discovery, () => true); discovery.SetPresentationGate(layer.MayPresent, layer.IsPrepared);
            var settings = WorldObjectSettings.Default.WithMode(WorldObjectKind.Chest, WorldObjectMode.Always).WithMode(WorldObjectKind.Sign, WorldObjectMode.All).WithMode(WorldObjectKind.Tombstone, WorldObjectMode.Characters);
            for (int i = 0; i < 80; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            graphics.Scene(layer, Path.Combine(output, "native-objects-and-text.png")); Require(layer.LastDrawn == 6, "representative native scene contains two separate dresser labels and two texts");
            AssertLanguage(discovery, layer, new[] { "Ivy Chest", "Dresser", "Dresser", "Dead Man's Chest" });
            Terraria.Localization.LanguageManager.Instance.SetLanguage("de-DE");
            for (int i = 0; i < 80; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            AssertLanguage(discovery, layer, new[] { "Efeutruhe", "Kommode", "Kommode", "Truhe des toten Mannes" });
            Terraria.Localization.LanguageManager.Instance.SetLanguage("en-US");
            for (int i = 0; i < 80; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            AssertLanguage(discovery, layer, new[] { "Ivy Chest", "Dresser", "Dresser", "Dead Man's Chest" });
            Main.LocalPlayer.gravDir = -1; Main.screenPosition = new Vector2(20, 0); Main.GameViewMatrix.Zoom = new Vector2(1.25f);
            for (int i = 0; i < 80; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
            graphics.Scene(layer, Path.Combine(output, "native-objects-gravity-zoom.png")); Require(layer.Failure == null && layer.LastDrawn > 0, "native zoom/gravity projection draws without stale world results");
            Main.GameViewMatrix.Zoom = Vector2.One; Main.LocalPlayer.gravDir = 1; Main.screenPosition = Vector2.Zero; layer.Clear(); discovery.Clear();
        }
        private static void AssertLanguage(WorldObjectDiscovery discovery, WorldObjectTextWorldLayer layer, string[] names)
        {
            Require(discovery.Candidates.Where(c => c.Value.Kind == WorldObjectKind.Chest).OrderBy(c => c.Value.TileX).Select(c => c.Text).SequenceEqual(names), "language changes actual current names without manual cache clearing");
            var packets = (Array)typeof(WorldObjectTextWorldLayer).GetField("packets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layer);
            int count = (int)typeof(WorldObjectTextWorldLayer).GetField("packetCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(layer);
            var actual = new SortedDictionary<int, string>();
            for (int i = 0; i < count; i++)
            {
                var packet = packets.GetValue(i); var type = packet.GetType();
                var value = (WorldObject)type.GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(packet);
                if (value.Kind != WorldObjectKind.Chest) continue;
                var layout = (NativeWorldTextLayout)type.GetField("Layout", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(packet);
                actual.Add(value.TileX, String.Concat(layout.Snippets.Select(s => s.Snippet.Text)));
            }
            Require(actual.Values.SequenceEqual(names), "language reaches actual cached Draw snippets without stale outer layout");
        }
        private static void EmptyCandidateFontRecovery(ProbeGraphics graphics, WorldTileObservation world, WorldObjectHostObservation source)
        {
            Main.tile = new Tile[256, 128]; Main.sign = new Sign[32000]; source.EndSession();
            Put(20, 20, 55, 0, 2); Main.sign[0] = new Sign { x = 20, y = 20, text = "A" };
            // A deliberately wide CPU metric font makes one indivisible unit
            // fail the layout bound. It is never drawn; recovery uses real XNB.
            var wide = new ReLogic.Graphics.DynamicSpriteFont(0, 20, '?');
            Type pageType = typeof(ReLogic.Graphics.DynamicSpriteFont).Assembly.GetType("ReLogic.Graphics.FontPage", true);
            var glyphs = new List<Rectangle> { new Rectangle(0, 0, 1000, 20), new Rectangle(0, 0, 1000, 20) };
            var padding = new List<Rectangle>(glyphs); var characters = new List<char> { '?', 'A' };
            var kerning = new List<Vector3> { new Vector3(0, 1000, 0), new Vector3(0, 1000, 0) };
            object page = Activator.CreateInstance(pageType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { null, glyphs, padding, characters, kerning }, null);
            Array pages = Array.CreateInstance(pageType, 1); pages.SetValue(page, 0);
            typeof(ReLogic.Graphics.DynamicSpriteFont).GetMethod("SetPages", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wide, new object[] { pages });
            var original = Terraria.GameContent.FontAssets.MouseText;
            var discovery = new WorldObjectDiscovery(source); var layer = new WorldObjectTextWorldLayer(discovery, () => true); discovery.SetPresentationGate(layer.MayPresent, layer.IsPrepared);
            var settings = WorldObjectSettings.Default.WithMode(WorldObjectKind.Sign, WorldObjectMode.Lines);
            try
            {
                graphics.SetMouseFont(wide);
                for (int i = 0; i < 100; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
                Require(discovery.Candidates.Count == 0 && layer.Failure == null, "overwide complete unit leaves no candidates and no drawing failure");
                Terraria.GameContent.FontAssets.MouseText = original;
                // No mode, geometry, text or manual cache invalidation changes.
                for (int i = 0; i < 100; i++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
                Require(discovery.SelectedCount == 1, "font replacement revives a previously rejected zero-candidate world");
                graphics.DrawFrame(layer); Require(layer.LastDrawn == 1 && layer.Failure == null, "recovered candidate actually draws with the original font");
            }
            finally { Terraria.GameContent.FontAssets.MouseText = original; layer.Clear(); discovery.Clear(); }
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
