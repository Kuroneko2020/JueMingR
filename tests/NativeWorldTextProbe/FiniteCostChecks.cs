using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using JueMingR.Features.WorldObjectText;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;
using JueMingR.TerrariaHost.World;
using JueMingR.TerrariaHost.WorldObjectText;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    // One finite test run. No sampling entry, counter or clock enters Release.
    internal static class FiniteCostChecks
    {
        private static readonly List<string> rows = new List<string>();
        private static readonly Func<long> allocated = AllocationReader();
        internal static void Run(ProbeGraphics graphics, string output)
        {
            rows.Clear(); rows.Add("scenario\tsamples\tp50_us\tp95_us\tp99_us\tmax_us\tthread_bytes_per_sample\tGC_0_1_2\tdetail");
            Main.maxTilesX = 512; Main.maxTilesY = 256; Main.tile = new Tile[512, 256]; Main.chest = new Chest[8000]; Main.sign = new Sign[32000];
            Main.screenPosition = Vector2.Zero; Main.LocalPlayer.position = new Vector2(450, 450); Main.LocalPlayer.gravDir = 1; Main.LocalPlayer.accOreFinder = true;
            for (int y = 0; y < 10; y++) for (int x = 0; x < 30; x++) Put(x * 2, 18 + y * 2, 21);
            for (int i = 0; i < 60; i++) { int x = i % 20 * 2, y = 2 + i / 20 * 4; Put(x, y, 55); Main.sign[i] = new Sign { x = x, y = y, text = "[c/55ffaa:sign] A[i:8]" }; }
            for (int i = 0; i < 60; i++) { int x = 40 + i % 10 * 2, y = 2 + i / 10 * 2; Put(x, y, 85); Main.sign[60 + i] = new Sign { x = x, y = y, text = "tomb\nsecond" }; }
            var world = new WorldTileObservation(() => true); var raw = new WorldObjectHostObservation(world); var source = new CountedSource(raw);
            var discovery = new WorldObjectDiscovery(source); var layer = new WorldObjectTextWorldLayer(discovery, () => true); discovery.SetPresentationGate(layer.MayPresent, layer.IsPrepared);
            var off = WorldObjectSettings.Default; var always = off.WithMode(WorldObjectKind.Chest, WorldObjectMode.Always);
            Action<WorldObjectSettings, OpenedPositionHistory> update = (setting, history) => { world.BeginTick(); discovery.Update(setting, history); layer.Prepare(setting); };
            Sample("all-off-discovery-prepare", 600, i => update(off, null), () => source.Detail(discovery, layer));
            Require(source.Reads == 0 && source.Texts == 0 && source.Begins == 0, "off has zero display source work");
            Main.LocalPlayer.accOreFinder = false; source.Reset();
            Sample("Always-no-detector", 600, i => update(always, null), () => source.Detail(discovery, layer));
            Require(source.Reads == 0 && source.Texts == 0 && source.Begins == 0, "no detector exits before names/discovery"); Main.LocalPlayer.accOreFinder = true;
            foreach (int far in new[] { -1, 0, 32768 })
            {
                string pair = new string(far < 0 ? 'a' : far == 0 ? 'b' : 'c', 64), path = Path.Combine(Terraria.Program.SavePath, "cost", pair + ".json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
                var keys = new List<long>();
                if (far >= 0) for (int i = 0; i < 32; i++) keys.Add(WorldObject.PositionKey(i % 16 * 2, 18 + i / 16 * 2));
                for (int i = 0; i < far; i++) keys.Add(WorldObject.PositionKey(10000 + i % 256, 10000 + i / 256));
                if (keys.Count != 0) File.WriteAllBytes(path, new OpenedPositionCodec().Encode(pair, new OpenedPositionIndex(keys)));
                var history = new OpenedPositionHistory(key => new AtomicFileDocument(path, OpenedPositionCodec.MaximumBytes, true)); var cold = Stopwatch.StartNew();
                try
                {
                    history.BeginSession(1); history.UsePair(pair); Until(() => { history.Poll(); return history.Loaded; });
                    rows.Add("history-load-H=" + keys.Count + "\t1\t\t\t\t" + (cold.Elapsed.TotalMilliseconds * 1000).ToString("F3", CultureInfo.InvariantCulture) + "\tNA\tNA\tbackground read+parse+index+publish+poll latency; bytes=" + (File.Exists(path) ? new FileInfo(path).Length : 0));
                    discovery.Clear(); layer.Clear(); var opened = off.WithMode(WorldObjectKind.Chest, WorldObjectMode.Opened);
                    for (int i = 0; i < 60; i++) update(opened, history); source.Reset();
                    Sample("Opened-H=" + keys.Count, 600, i => update(opened, history), () => source.Detail(discovery, layer));
                    if (keys.Count == 0) Require(source.Reads == 0 && source.Texts == 0 && source.Begins == 0, "loaded empty H has zero display source work");
                }
                finally { Require(history.Stop(5000), "cost history drained"); }
            }
            discovery.Clear(); layer.Clear(); var all = always.WithMode(WorldObjectKind.Sign, WorldObjectMode.All).WithMode(WorldObjectKind.Tombstone, WorldObjectMode.Lines);
            source.Reset(); Sample("dense-cold-discovery-prepare", 100, i => update(all, null), () => source.Detail(discovery, layer));
            source.Reset(); Sample("dense-standing-discovery", 600, i => { world.BeginTick(); discovery.Update(all, null); }, () => source.Detail(discovery, layer));
            Sample("dense-standing-prepare", 600, i => layer.Prepare(all), () => "N=" + discovery.Candidates.Count + "; last-source-work=" + layer.PreparationWork);
            graphics.DrawFrame(layer); Sample("dense-standing-draw-CPU", 300, i => graphics.DrawFrame(layer), () => "K=" + layer.LastDrawn + "; includes shared test Begin/End+clear; no GPU/FPS claim");
            source.Reset(); Sample("dense-moving-update", 600, i => { Main.screenPosition = new Vector2(i % 80 * 1.25f, i % 40 * 0.5f); Main.LocalPlayer.position = new Vector2(150 + i % 80 * 7, 400); update(all, null); }, () => source.Detail(discovery, layer));
            Main.screenPosition = Vector2.Zero;
            foreach (string text in new[] { "short [c/00ff00:color] [i:8]", "[unknown:" + new string('x', 5000) + "]", new string('W', 1000000) })
            {
                int work = 0, updates = 0;
                Sample("text-cold-L=" + text.Length, 30, i => { var layout = new NativeWorldTextLayout(graphics.Font, text, all.Style(WorldObjectKind.Sign), 322); int count = 0; while (!layout.Ready) { layout.Step(512); count++; } work = layout.SourceWork; updates = count; }, () => "bounded prepared-source=" + work + "; 512-budget-steps=" + updates);
            }
            ObserverCosts(world);
            Require(layer.Failure == null, "finite cost draw remained healthy");
            File.WriteAllLines(Path.Combine(output, "finite-costs.tsv"), rows); foreach (var row in rows) Console.WriteLine(row);
            Console.WriteLine("PASS: finite cost invariants; numbers are local CPU/allocation evidence, not gameplay FPS.");
        }
        private static void ObserverCosts(WorldTileObservation world)
        {
            int factories = 0; Storage storage = null; string root = Path.Combine(Terraria.Program.SavePath, "observer-cost");
            var history = new OpenedPositionHistory(key => { Interlocked.Increment(ref factories); return storage = new Storage(new AtomicFileDocument(Path.Combine(root, key + ".json"), OpenedPositionCodec.MaximumBytes, true)); });
            var runtime = new SingleFeatureRuntime(new Session(), new Feature()); runtime.Update(0); var observer = new OpenedContainerObserver(runtime, history, world, () => false); observer.Start();
            try
            {
                Main.LocalPlayer.chest = -1; Main.playerInventory = false;
                Sample("all-off-open-observer-idle", 600, i => { world.BeginTick(); observer.Update(false); }, () => "factory=" + factories + "; fixed local-state observation");
                Require(factories == 0, "idle observer never cold-reads history");
                for (int i = 0; i < 65; i++)
                {
                    int x = 100 + i % 32 * 2, y = 100 + i / 32 * 2; Put(x, y, 21);
                    var chest = Chest.CreateOutOfArray(i, x, y, 40); typeof(Chest).GetMethod("Assign", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, new object[] { chest });
                }
                Open(0, world, observer); Until(() => { history.Poll(); return history.Status == PreferenceStatus.Saved; });
                Sample("held-open", 600, i => { world.BeginTick(); observer.Update(false); }, () => "H=1; writes=" + storage.Writes);
                Sample("repeat-close-open", 600, i => { Main.playerInventory = false; observer.Update(false); Main.playerInventory = true; world.BeginTick(); observer.Update(false); }, () => "H=1; writes=" + storage.Writes);
                Require(storage.Writes == 1, "confirmed duplicate opens do not rewrite");
                AppDomain.MonitoringIsEnabled = true; long allocatedBefore = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize; var time = Stopwatch.StartNew();
                Sample("64-new-open-facts", 64, i => Open(i + 1, world, observer), () => "observation + geometry + enqueue only");
                Until(() => { history.Poll(); return history.Status == PreferenceStatus.Saved; });
                rows.Add("coalesced-save-65-total\t1\t\t\t\t" + (time.Elapsed.TotalMilliseconds * 1000).ToString("F3", CultureInfo.InvariantCulture) + "\t" + (AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - allocatedBefore) + "\tNA\twrites=" + storage.Writes + "; bytes=" + storage.Bytes + "; storage-Write-total-us=" + storage.Microseconds.ToString("F3", CultureInfo.InvariantCulture) + "; allocation entire AppDomain window incl worker; latency includes debounce/serialize/verify/commit");
                Require(storage.Writes < 10, "short burst coalesces instead of writing per open");
            }
            finally { observer.End(); Require(history.Stop(5000), "observer cost store closes"); Main.LocalPlayer.chest = -1; Main.playerInventory = false; }
        }
        private static void Open(int index, WorldTileObservation world, OpenedContainerObserver observer)
        {
            int x = 100 + index % 32 * 2, y = 100 + index / 32 * 2;
            Main.LocalPlayer.chest = index; Main.LocalPlayer.chestX = x; Main.LocalPlayer.chestY = y; Main.playerInventory = true;
            world.BeginTick(); observer.Update(false);
        }
        private static void Put(int x, int y, int type)
        { for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++) { var tile = new Tile { type = (ushort)type, frameX = (short)(dx * 18), frameY = (short)(dy * 18) }; tile.active(true); Main.tile[x + dx, y + dy] = tile; } }
        private static void Sample(string name, int count, Action<int> action, Func<string> details)
        {
            var times = new double[count]; int[] gc = { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) }; long before = allocated == null ? 0 : allocated();
            for (int i = 0; i < count; i++) { long start = Stopwatch.GetTimestamp(); action(i); times[i] = (Stopwatch.GetTimestamp() - start) * 1000000.0 / Stopwatch.Frequency; }
            long bytes = allocated == null ? -1 : allocated() - before; Array.Sort(times);
            rows.Add(String.Join("\t", name, count.ToString(), times[count / 2].ToString("F3", CultureInfo.InvariantCulture), times[(count - 1) * 95 / 100].ToString("F3", CultureInfo.InvariantCulture), times[(count - 1) * 99 / 100].ToString("F3", CultureInfo.InvariantCulture), times[count - 1].ToString("F3", CultureInfo.InvariantCulture), bytes < 0 ? "NA" : (bytes / (double)count).ToString("F1", CultureInfo.InvariantCulture), (GC.CollectionCount(0) - gc[0]) + "/" + (GC.CollectionCount(1) - gc[1]) + "/" + (GC.CollectionCount(2) - gc[2]), details()));
        }
        private static Func<long> AllocationReader() { var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Static | BindingFlags.Public); return method == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method); }
        private static void Until(Func<bool> test) { var time = Stopwatch.StartNew(); while (!test()) { if (time.ElapsedMilliseconds > 5000) throw new TimeoutException("finite cost wait"); Thread.Sleep(1); } }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private sealed class CountedSource : IWorldObjectSource
        {
            private readonly IWorldObjectSource inner; internal long Reads, Texts, Begins;
            internal CountedSource(IWorldObjectSource inner) { this.inner = inner; }
            public bool HasDetector { get { return inner.HasDetector; } }
            public bool TryBegin(bool chestNames, bool signText, out WorldObjectView view) { Begins++; return inner.TryBegin(chestNames, signText, out view); }
            public WorldTargetTile Read(int x, int y) { Reads++; return inner.Read(x, y); }
            public bool TryText(WorldObject value, out string text) { Texts++; return inner.TryText(value, out text); }
            internal void Reset() { Reads = Texts = Begins = 0; }
            internal string Detail(WorldObjectDiscovery discovery, WorldObjectTextWorldLayer layer) { return "C=8000; S=32000; T=requests=" + Reads + "; begins=" + Begins + "; name/text=" + Texts + "; N-last=" + discovery.Candidates.Count + "; prep-last=" + layer.PreparationWork; }
        }
        private sealed class Session : IGameSessionProbe { public bool IsSessionActive { get { return true; } } }
        private sealed class Feature : IRuntimeFeature { public bool Enabled { get { return true; } } public void OnSessionStarted() { } public void OnSessionEnded() { } public void Update(ulong tick) { } public void FailClosed() { } }
        private sealed class Storage : IPreferenceStorage
        {
            private readonly IPreferenceStorage inner; internal int Writes, Bytes; internal double Microseconds;
            internal Storage(IPreferenceStorage inner) { this.inner = inner; }
            public PreferenceReadResult Read() { return inner.Read(); }
            public PreferenceWriteResult Write(string identity, byte[] bytes) { long start = Stopwatch.GetTimestamp(); var result = inner.Write(identity, bytes); Microseconds += (Stopwatch.GetTimestamp() - start) * 1000000.0 / Stopwatch.Frequency; Interlocked.Add(ref Bytes, bytes.Length); Interlocked.Increment(ref Writes); return result; }
            public void Dispose() { inner.Dispose(); }
        }
    }
}
