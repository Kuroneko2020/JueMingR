using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Exploration;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Map;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeExplorationCosts
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static readonly Func<long> allocated = AllocationReader();
        internal static void Run(object context, object host, string output)
        {
            Directory.CreateDirectory(output);
            var rows = new List<string> { "configuration\tsize\tobserver\tdetails\tscenario\tU\texpectedS\texpectedC\thookS\tenabledS\tdirtySignals\tcellReads\tblockReads\tmetadata\tfullScans\tformat\tdetailFormat\tlayout\tsubmit\tcommit\ttotal_ms\tp50_us\tp95_us\tmax_us\tcurrent_thread_bytes\tbatch_ms" };
            var scanRows = new List<string> { "configuration\tsize\tmode\tupdates\ttotal_ms\tp50_us\tp95_us\tmax_us" };
            var update = (Action)Delegate.CreateDelegate(typeof(Action), context, context.GetType().GetMethod("UpdateRuntime", Flags));
            var ui = (Action)Delegate.CreateDelegate(typeof(Action), context, context.GetType().GetMethod("UpdateShell", Flags));
            object observation = Get(host, "observation"), shell = Get(context, "Shell"), state = Get(shell, "State"), popup = Get(shell, "MapPopup"), renderer = Get(shell, "renderer");
            var history = (ExplorationHistory)Get(host, "history"); Type hooks = observation.GetType();
            long now = 0; Set(host, "milliseconds", (Func<long>)(() => now));
            var graphicsGate = renderer.GetType().GetMethod("RefreshResources", Flags); var isolation = new Harmony("JueMingR.Tests.ExplorationCpuUi");
            bool debug = typeof(ExplorationCounter).GetField("CellReads", Flags) != null;
            var elapsed = new long[2000];
            Main.hideUI = Main.mapFullscreen = false; Main.mouseX = Main.mouseY = -100; Main.LocalPlayer.mouseInterface = false;
            Set(shell, "LayersReady", true); Set(state, "Ready", true); Set(shell, "matrix", Main.UIScaleMatrix);
            try
            {
                // Keep actual native font and every CPU layout call. Only the
                // final availability bool for GPU resources is substituted.
                isolation.Patch(graphicsGate, postfix: new HarmonyMethod(typeof(NativeExplorationCosts).GetMethod(nameof(Resources), Flags)));
                foreach (int width in new[] { 4200, 8400 })
                {
                    int height = width == 4200 ? 1200 : 2400;
                    Main.gameMenu = true; update(); Main.Map = null; Main.tile = null; GC.Collect(); GC.WaitForPendingFinalizers();
                    Main.maxTilesX = width; Main.maxTilesY = height; Main.Map = new WorldMap(width, height); Main.tile = new Tile[width, height];
                    for (int i = 0; i < 2000; i++) { int x = 100 + i % 100, y = 100 + i / 100; var t = new Tile(); t.active(true); t.type = 1; Main.tile[x, y] = t; }
                    Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(Terraria.Program.SavePath, "cost-" + width + ".wld"), false) { UniqueId = Guid.NewGuid() }; Main.gameMenu = false;
                    Until(() => { update(); return GetOptional(host, "Counter") != null && (bool)Get(host, "ControlsEnabled"); });
                    foreach (string arm in new[] { "native", "off", "on" })
                    {
                        Call(host, "SetDynamic", false);
                        if (arm == "native") Call(observation, "Dispose");
                        else if (!(bool)Get(observation, "Ready")) { Call(observation, "Install"); Require((bool)Get(observation, "Ready"), "actual observation reinstall"); }
                        var counter = (ExplorationCounter)Get(host, "Counter");
                        Call(observation, "Bind", Main.Map, counter); if (arm == "on") Call(host, "SetDynamic", true);
                        foreach (bool details in new[] { false, true }) foreach (string scenario in new[] { "idle", "repeat-no-store", "revealed-store", "same-set", "new", "bulk-clear" })
                        {
                            Main.Map.Clear();
                            if (scenario == "revealed-store" || scenario == "repeat-no-store" || scenario == "same-set" || scenario == "bulk-clear")
                                for (int i = 0; i < 2000; i++) Put(100 + i % 100, 100 + i / 100, scenario == "revealed-store" ? (byte)20 : (byte)255);
                            Call(host, "PauseScan", false); Set(host, "FastScan", true); Call(host, "Recount");
                            Until(() => { now += 17; update(); return counter.Complete && !counter.Pending && !counter.Scanning; }); Set(host, "FastScan", false);
                            now += 10001; Until(() => { update(); return history.Saved; });
                            Call(popup, "Suspend"); Call(state, "Close");
                            if (details) { Set(state, "Ready", true); Call(state, "Navigate", 2); Call(state, "RestoreVisible"); Call(popup, "Open", true); }
                            for (int warm = 0; warm < 10; warm++) { now += 17; update(); ui(); }
                            Require(!(bool)Get(shell, "Failed") && (!details || (bool)Get(popup, "Visible")), "actual F5 CPU details prepared without drawing");
                            long[] before = Snapshot(counter, host, popup, history, hooks);
                            long bytes = allocated?.Invoke() ?? 0, total = 0; double batchMs = 0;
                            for (int i = 0; i < 2000; i++)
                            {
                                long start = Stopwatch.GetTimestamp();
                                if (scenario == "new" || scenario == "same-set") Put(100 + i % 100, 100 + i / 100, 255);
                                else if (scenario == "revealed-store") Main.Map.UpdateLighting(100 + i % 100, 100 + i / 100, 21);
                                else if (scenario == "repeat-no-store") Main.Map.UpdateLighting(100, 100, 255);
                                else if (scenario == "bulk-clear" && i == 0) { long b = Stopwatch.GetTimestamp(); Main.Map.Clear(); batchMs = (Stopwatch.GetTimestamp() - b) * 1000d / Stopwatch.Frequency; }
                                now += 17; update(); ui(); elapsed[i] = Stopwatch.GetTimestamp() - start; total += elapsed[i];
                            }
                            long allocation = allocated == null ? -1 : allocated() - bytes;
                            long[] after = Snapshot(counter, host, popup, history, hooks); var delta = after.Zip(before, (a, b) => a - b).ToArray();
                            if (debug)
                            {
                                bool stable = scenario == "idle" || scenario == "repeat-no-store" || scenario == "revealed-store";
                                Require((arm != "on" || !stable || delta[3] == 0) && (arm == "on" || delta[3] == 0), "stable/off actual Host adds zero map cells: " + arm + "/" + scenario);
                                Require(scenario == "bulk-clear" || delta[6] == 0, "ordinary steady changes never restart full scan");
                                Require(delta[9] <= (scenario == "bulk-clear" ? 2 : 0), "dynamic text never rebuilds controls; bulk scan start/end may change meaningful buttons");
                                Require(details || delta[8] == 0, "hidden details never formats scan text");
                                if (arm != "native" && (scenario == "new" || scenario == "same-set" || scenario == "revealed-store")) Require(delta[0] == 2000, "real store observer called for every expected store");
                                if (arm != "on") Require(delta[1] == 0 && delta[2] == 0, "off gate stops added fields and dirty notifications");
                                if (stable) Require(delta[7] == 0 && delta[8] == 0 && delta[10] == 0, "unchanged values cause no formatting or saves");
                            }
                            if (arm == "on" && scenario == "new") Require(counter.Count > 1500, "continuous exploration publishes timely progress before writer stops");
                            Array.Sort(elapsed); int u = scenario == "idle" ? 0 : scenario == "bulk-clear" ? 1 : 2000, s = scenario == "repeat-no-store" || scenario == "idle" || scenario == "bulk-clear" ? 0 : 2000;
                            string counters = debug ? String.Join("\t", delta.Select(v => v.ToString(CultureInfo.InvariantCulture))) : String.Join("\t", Enumerable.Repeat("NA", 12));
                            rows.Add((debug ? "Debug-workload" : "Release-timing") + "\t" + width + "x" + height + "\t" + arm + "\t" + details + "\t" + scenario + "\t" + u + "\t" + s + "\t" + (scenario == "new" || scenario == "bulk-clear" ? 2000 : 0) + "\t" + counters + "\t" + F(total * 1000d / Stopwatch.Frequency) + "\t" + F(elapsed[999] * 1000000d / Stopwatch.Frequency) + "\t" + F(elapsed[1899] * 1000000d / Stopwatch.Frequency) + "\t" + F(elapsed[1999] * 1000000d / Stopwatch.Frequency) + "\t" + allocation + "\t" + F(batchMs));
                            if (arm == "on")
                            {
                                Set(host, "FastScan", true); Until(() => { now += 17; update(); return counter.Current && !counter.Scanning; }); Set(host, "FastScan", false);
                                long answer = 0; for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) if (Main.Map[x, y].Light > 0) answer++;
                                Require(counter.Count == answer, "post-scenario independent entire logical-map enumeration: " + scenario);
                            }
                            now += 10001; Until(() => { update(); return history.Saved; });
                        }
                    }
                    Call(popup, "Suspend"); Set(state, "Visible", false); Call(host, "SetDynamic", false); Call(host, "PauseScan", false);
                    foreach (bool fast in new[] { false, true })
                    {
                        Set(host, "FastScan", fast); Call(host, "Recount"); var counter = (ExplorationCounter)Get(host, "Counter"); var steps = new List<long>(); long total = 0;
                        while (counter.Scanning)
                        {
                            int before = counter.CompletedBlocks; long start = Stopwatch.GetTimestamp(); now += 17; update(); ui(); long duration = Stopwatch.GetTimestamp() - start; total += duration; steps.Add(duration);
                            Require(counter.CompletedBlocks - before <= (fast ? 8 : 1) && steps.Count <= counter.BlockCount * 4 + 4, "actual Host scan batch and finite completion schedule");
                        }
                        steps.Sort(); scanRows.Add((debug ? "Debug-workload" : "Release-timing") + "\t" + width + "x" + height + "\t" + (fast ? "fast" : "performance") + "\t" + steps.Count + "\t" + F(total * 1000d / Stopwatch.Frequency) + "\t" + F(steps[steps.Count / 2] * 1000000d / Stopwatch.Frequency) + "\t" + F(steps[(int)(steps.Count * .95)] * 1000000d / Stopwatch.Frequency) + "\t" + F(steps[steps.Count - 1] * 1000000d / Stopwatch.Frequency));
                    }
                    Set(host, "FastScan", false); Call(host, "SetDynamic", true); Set(host, "FastScan", true); Until(() => { now += 17; update(); return ((ExplorationCounter)Get(host, "Counter")).Current; }); Set(host, "FastScan", false);
                    Console.WriteLine("PASS: " + width + "x" + height + " actual Host three-arm 2000-update cost matrix, six shapes, both bounded full-scan schedules, hidden/visible F5; counter arrays=" + (((width + 63) / 64) * ((height + 63) / 64) * 16) + " bytes plus headers.");
                }
            }
            finally { isolation.Unpatch(graphicsGate, HarmonyPatchType.All, isolation.Id); File.WriteAllLines(Path.Combine(output, debug ? "exploration-debug.tsv" : "exploration-release.tsv"), rows); File.WriteAllLines(Path.Combine(output, "scan-budgets.tsv"), scanRows); }
        }
        private static long[] Snapshot(ExplorationCounter counter, object host, object popup, object history, Type hooks)
        { return new[] { Static(hooks, "ObservationCalls"), Static(hooks, "EnabledCalls"), Static(hooks, "DirtySignals"), Number(counter, "CellReads"), Number(counter, "BlockReads"), Number(counter, "MetadataVisits"), Number(counter, "FullScans"), Number(host, "FormattedValues"), Number(host, "FormattedDetails"), Number(popup, "LayoutBuilds"), Number(history, "Submitted"), Number(history, "Committed") }; }
        private static long Number(object value, string name) { return (long?)value.GetType().GetField(name, Flags)?.GetValue(value) ?? 0; }
        private static long Static(Type type, string name) { return (long?)type.GetField(name, Flags)?.GetValue(null) ?? 0; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void Put(int x, int y, byte light) { var tile = new MapTile { Light = light }; Main.Map.SetTile(x, y, ref tile); }
        private static void Resources(ref bool __result) { __result = true; }
        private static string F(double value) { return value.ToString("F3", CultureInfo.InvariantCulture); }
        private static Func<long> AllocationReader() { var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static); return method == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method); }
        private static void Until(Func<bool> done) { var timer = Stopwatch.StartNew(); while (!done()) { if (timer.ElapsedMilliseconds > 15000) throw new TimeoutException("exploration cost setup/drain"); Thread.Yield(); } }
    }
}
