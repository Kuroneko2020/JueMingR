using System;
using System.IO;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using JueMingR.Features.Footprints;
using JueMingR.Infrastructure.Footprints;
using JueMingR.Platform.Footprints;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFootprint200HourVisualChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        // Called inside the existing isolated full Host/XNB fixture. The three
        // time-separated branches deliberately bypass the fine-query window;
        // producer coverage is independently checked by the archive tests.
        internal static void Run(ProbeGraphics graphics, object host, Assembly assembly, string output)
        {
            object originalRecorder = GetOptional(host, "Recorder"); Set(host, "Recorder", null);
            try
            {
            object layer = Get(host, "Layer"); var playback = (FootprintPlayback)Get(layer, "Playback");
            long hour = 3600L * TimeSpan.TicksPerSecond, end = 200 * hour;
            var edges = new[] {
                new FootprintOverviewEdge(100.25f, 80.25f, 300.25f, 80.25f, 0, hour, 1, 1, 100),
                new FootprintOverviewEdge(100.25f, 120.25f, 300.25f, 120.25f, 99 * hour, 100 * hour, 2, 10001, 10100),
                new FootprintOverviewEdge(100.25f, 160.25f, 300.25f, 160.25f, 199 * hour, end, 3, 20001, 20100),
                new FootprintOverviewEdge(350.25f, 80.25f, 350.25f, 250.25f, 0, hour, 1, 101, 200),
                new FootprintOverviewEdge(400.25f, 80.25f, 550.25f, 230.25f, 0, hour, 1, 201, 300) };
            string generation = (string)Get(host, "Generation");
            Set(layer, "overview", new FootprintOverviewSnapshot(generation, 1, Int64.MaxValue, 20100, end, true, edges));
            Set(layer, "query", null); playback.Show(end);
            var frameType = assembly.GetType("JueMingR.TerrariaHost.Map.MapView", true);
            object frame = Activator.CreateInstance(frameType, Flags, null, new object[] { Vector2.Zero, Vector2.Zero, 1f, 1f, 255 }, null);
            Color[] pixels = graphics.Pixels(() => Call(layer, "DrawRoutes", frame), Matrix.Identity);
            Require(GreenRows(pixels, 200, 60, 100) == 2, "production horizontal route is two screen pixels");
            Require(GreenRows(pixels, 200, 100, 140) == 2 && GreenRows(pixels, 200, 140, 180) == 2, "early/middle/late 200h branches draw together without a fine-query window");
            long projectedBefore = (long)Get(layer, "ProjectedPoints");
            var appended = new FootprintOverviewEdge[edges.Length + 1]; Array.Copy(edges, appended, edges.Length);
            appended[edges.Length] = new FootprintOverviewEdge(600, 80, 700, 80, 199 * hour, end, 4, 20101, 20101);
            Set(layer, "overview", new FootprintOverviewSnapshot(generation, 2, Int64.MaxValue, 20101, end, true, appended));
            graphics.Pixels(() => Call(layer, "DrawRoutes", frame), Matrix.Identity);
            Require((long)Get(layer, "ProjectedPoints") == projectedBefore + 2, "appended overview projects only the new edge and reuses stable historical screen coordinates");
            Set(layer, "overview", new FootprintOverviewSnapshot(generation, 3, Int64.MaxValue, 20100, end, true, edges));
            graphics.Image(Path.Combine(output, "footprints-200h-two-pixel.png"), () => Call(layer, "DrawRoutes", frame), Matrix.Identity);
            graphics.Image(Path.Combine(output, "footprints-200h-one-pixel-reference.png"), () => {
                foreach (var edge in edges) DrawReference(edge, 1f);
            }, Matrix.Identity);
            playback.Seek(100 * hour, end);
            pixels = graphics.Pixels(() => Call(layer, "DrawRoutes", frame), Matrix.Identity);
            Require(GreenRows(pixels, 200, 60, 100) == 2 && GreenRows(pixels, 200, 100, 140) == 2 && GreenRows(pixels, 200, 140, 180) == 0, "historical cursor keeps past branches and excludes future branch");
            playback.Show(end);
            object zoomedOut = Activator.CreateInstance(frameType, Flags, null, new object[] { Vector2.Zero, new Vector2(50, 40), .25f, 1f, 255 }, null);
            pixels = graphics.Pixels(() => Call(layer, "DrawRoutes", zoomedOut), Matrix.Identity);
            Require(GreenRows(pixels, 100, 54, 68) == 2, "zoomed-out production line retains two screen pixels");
            graphics.Image(Path.Combine(output, "footprints-200h-quarter-zoom.png"), () => Call(layer, "DrawRoutes", zoomedOut), Matrix.Identity);

            // A single explicit ten-second Move must be halfway at five
            // seconds; its following Stay must keep the endpoint unchanged.
            var drawSample = layer.GetType().GetMethod("DrawSample", Flags);
            var move = new FootprintSample(1, 0, 10 * TimeSpan.TicksPerSecond, 1, 100, 300, 300, 300, FootprintPosition.Valid);
            object[] moveArgs = { TextureAssets.MagicPixel.Value, move, new Vector2(100, 300), new Vector2(300, 300), 5d * TimeSpan.TicksPerSecond, frame, true, null };
            graphics.Pixels(() => drawSample.Invoke(layer, moveArgs), Matrix.Identity);
            Require((Vector2?)moveArgs[7] == new Vector2(200, 300), "long compressed move marker interpolates by duration");
            var stay = new FootprintSample(2, 10 * TimeSpan.TicksPerSecond, 30 * TimeSpan.TicksPerSecond, 1, 300, 300, FootprintPosition.Valid);
            object[] stayArgs = { TextureAssets.MagicPixel.Value, stay, new Vector2(300, 300), new Vector2(300, 300), 20d * TimeSpan.TicksPerSecond, frame, true, null };
            graphics.Pixels(() => drawSample.Invoke(layer, stayArgs), Matrix.Identity);
            Require((Vector2?)stayArgs[7] == new Vector2(300, 300), "stay marker remains at arrival instead of slow movement");
            Console.WriteLine("PASS: actual Host map consumer draws independent 200h overview, past-only cursor, 2px at 1/.25 zoom, ten-second movement and separate stay; XNB reference PNGs saved.");
            }
            finally { Set(host, "Recorder", originalRecorder); }
        }
        private static int GreenRows(Color[] pixels, int x, int from, int to)
        { int result = 0; for (int y = from; y < to; y++) if (pixels[y * 960 + x].G > 100) result++; return result; }
        private static void DrawReference(FootprintOverviewEdge edge, float width)
        {
            Vector2 from = new Vector2(edge.X1, edge.Y1), delta = new Vector2(edge.X2, edge.Y2) - from;
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, from, new Rectangle(0, 0, 1, 1), Color.LimeGreen, (float)Math.Atan2(delta.Y, delta.X), new Vector2(0, .5f), new Vector2(delta.Length(), width), SpriteEffects.None, 0);
        }
        internal static void RunLongArchive(ProbeGraphics graphics, object host, Assembly assembly, string output, string generatedArchiveDirectory, object context)
        {
            const string pair = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
            string archiveDirectory = Path.GetFullPath(generatedArchiveDirectory);
            Require(Directory.Exists(Path.Combine(archiveDirectory, pair)), "real generated 200h archive is present");
            object oldStore = Get(host, "store"), oldRecorder = GetOptional(host, "Recorder"), layer = Get(host, "Layer");
            var playback = (FootprintPlayback)Get(layer, "Playback");
            FootprintStore store = null;
            try
            {
                Call(layer, "Invalidate"); Set(host, "Recorder", null);
                var timer = Stopwatch.StartNew();
                store = new FootprintStore(() => new FileFootprintArchive(archiveDirectory, pair), pair, 8400, 2400); Set(host, "store", store);
                Wait(() => store.Snapshot.Loaded || store.Snapshot.Error != null, "200h real Store load");
                Require(store.Snapshot.Error == null && store.Snapshot.End == 200L * 3600 * TimeSpan.TicksPerSecond && store.Snapshot.Count > 8192, "real 200h rich archive admitted without detail-window truncation");
                double restoreMs = timer.Elapsed.TotalMilliseconds;
                var frameType = assembly.GetType("JueMingR.TerrariaHost.Map.MapView", true);
                Func<Vector2, float, object> frame = (position, zoom) => Activator.CreateInstance(frameType, Flags, null, new object[] { position, new Vector2(40, 20), zoom, 1f, 255 }, null);
                object full = frame(Vector2.Zero, .1f);
                Main.mapFullscreen = true; Call(layer, "Update");
                timer.Restart(); var overview = Prepare(graphics, layer, full); double firstMs = timer.Elapsed.TotalMilliseconds;
                Require(overview.Complete && overview.SourceSequence == store.Snapshot.Count && overview.Edges.Length > 0, "real Host consumes completed public overview for every source sequence");
                int green = 0; Color[] pixels = graphics.Pixels(() => Call(layer, "DrawRoutes", full), Matrix.Identity);
                foreach (var pixel in pixels) if (pixel.G > 100 && pixel.G > pixel.R * 1.2) green++;
                Require(green > 100, "rich 200h archive actually draws visible routes");
                graphics.Image(Path.Combine(output, "footprints-real-200h-overview.png"), () => Call(layer, "DrawRoutes", full), Matrix.Identity);
                long projections = (long)Get(layer, "ProjectedPoints"), elapsedTicks = 0;
                for (int i = 0; i < 40; i++) graphics.Pixels(() => { long from = Stopwatch.GetTimestamp(); Call(layer, "DrawRoutes", full); elapsedTicks += Stopwatch.GetTimestamp() - from; }, Matrix.Identity);
                Require((long)Get(layer, "ProjectedPoints") == projections, "steady real 200h view reuses projected geometry");
                double drawMs = elapsedTicks * 1000d / Stopwatch.Frequency / 40;

                object local = frame(new Vector2(100, 200), 1f); timer.Restart(); var localOverview = Prepare(graphics, layer, local); double localMs = timer.Elapsed.TotalMilliseconds;
                Require(localOverview.Complete && localOverview.SourceSequence == store.Snapshot.Count, "local pan/zoom retains full history coverage metadata");
                graphics.Image(Path.Combine(output, "footprints-real-200h-local.png"), () => Call(layer, "DrawRoutes", local), Matrix.Identity);
                playback.Seek(store.Snapshot.End / 2, store.Snapshot.End); timer.Restart(); var historic = Prepare(graphics, layer, full); double historyMs = timer.Elapsed.TotalMilliseconds;
                foreach (var edge in historic.Edges) Require(edge.End <= playback.Cursor, "public historical overview contains no future branch");
                graphics.Image(Path.Combine(output, "footprints-real-100h-playback.png"), () => Call(layer, "DrawRoutes", full), Matrix.Identity);
                var playbackPreparation = new List<double>(); var playbackDraw = new List<double>(); var playbackEdges = new List<int>();
                double preparationSum = 0, preparationMax = 0, playbackDrawSum = 0, playbackDrawMax = 0;
                for (int step = 1; step <= 20; step++)
                {
                    playback.Seek(store.Snapshot.End / 2 + step * TimeSpan.TicksPerSecond, store.Snapshot.End);
                    Set(layer, "nextOverview", 0d); timer.Restart(); var advancing = Prepare(graphics, layer, full);
                    double preparation = timer.Elapsed.TotalMilliseconds; long callbackTicks = 0;
                    graphics.Pixels(() => { long from = Stopwatch.GetTimestamp(); Call(layer, "DrawRoutes", full); callbackTicks = Stopwatch.GetTimestamp() - from; }, Matrix.Identity);
                    double callback = callbackTicks * 1000d / Stopwatch.Frequency;
                    foreach (var edge in advancing.Edges) Require(edge.End <= playback.Cursor, "advancing real playback never leaks future geometry");
                    playbackPreparation.Add(preparation); playbackDraw.Add(callback); playbackEdges.Add(advancing.Edges.Length);
                    preparationSum += preparation; preparationMax = Math.Max(preparationMax, preparation); playbackDrawSum += callback; playbackDrawMax = Math.Max(playbackDrawMax, callback);
                }
                File.WriteAllText(Path.Combine(output, "footprints-real-200h-playback-cost.json"), "{\"steps\":20,\"cursorStepSeconds\":1,\"preparationLatencyMs\":[" + String.Join(",", playbackPreparation.ConvertAll(F)) + "],\"drawCallbackMs\":[" + String.Join(",", playbackDraw.ConvertAll(F)) + "],\"outputEdges\":[" + String.Join(",", playbackEdges) + "],\"preparationMeanMs\":" + F(preparationSum / 20) + ",\"preparationMaxMs\":" + F(preparationMax) + ",\"drawCallbackMeanMs\":" + F(playbackDrawSum / 20) + ",\"drawCallbackMaxMs\":" + F(playbackDrawMax) + "}");
                foreach (int rate in new[] { 30, 60 }) ContinuousPlayback(graphics, host, layer, store, full, output, rate);

                Main.mapFullscreen = false; Call(layer, "Update");
                Require(GetOptional(layer, "overview") == null && store.OverviewResult == null, "closing map releases draw snapshot and cancels query");
                Main.mapFullscreen = true; Call(layer, "Update"); timer.Restart(); overview = Prepare(graphics, layer, full); double reopenMs = timer.Elapsed.TotalMilliseconds;
                Require(overview.Complete && overview.SourceSequence == store.Snapshot.Count, "reopening uses same complete archive and worker index");
                long count = store.Snapshot.Count, end = store.Snapshot.End; Call(layer, "Invalidate"); store.Dispose(); Wait(() => store.Finished, "200h Store close");
                timer.Restart(); store = new FootprintStore(() => new FileFootprintArchive(archiveDirectory, pair), pair, 8400, 2400); Set(host, "store", store);
                Wait(() => store.Snapshot.Loaded || store.Snapshot.Error != null, "200h Store re-entry");
                Require(store.Snapshot.Error == null && store.Snapshot.Count == count && store.Snapshot.End == end, "real re-entry preserves complete generated history");
                Call(layer, "Update"); overview = Prepare(graphics, layer, full); double reentryMs = timer.Elapsed.TotalMilliseconds;
                Require(overview.Complete && overview.SourceSequence == count, "re-entry rebuilds or reuses valid derived history and displays all coverage");

                string report = "{\"sourceCount\":" + count + ",\"endTicks\":" + end + ",\"overviewEdges\":" + overview.Edges.Length + ",\"greenPixels\":" + green +
                    ",\"storeLoadMs\":" + F(restoreMs) + ",\"firstPreparationMs\":" + F(firstMs) + ",\"localPreparationMs\":" + F(localMs) + ",\"historyPreparationMs\":" + F(historyMs) +
                    ",\"reopenPreparationMs\":" + F(reopenMs) + ",\"reentryAndPreparationMs\":" + F(reentryMs) + ",\"drawCallbackMeanMs\":" + F(drawMs) + ",\"stableDraws\":40,\"lineWidthPixels\":2,\"ownerGameRun\":false}";
                File.WriteAllText(Path.Combine(output, "footprints-real-200h-host.json"), report); Console.WriteLine(report);
            }
            finally
            {
                Call(layer, "Invalidate"); if (store != null) { store.Dispose(); Wait(() => store.Finished, "200h generated Store retirement"); }
                Set(host, "store", oldStore); Set(host, "Recorder", oldRecorder); Main.mapFullscreen = true; Call(layer, "Update");
            }
        }
        private static FootprintOverviewSnapshot Prepare(ProbeGraphics graphics, object layer, object frame)
        {
            graphics.Pixels(() => Call(layer, "DrawRoutes", frame), Matrix.Identity);
            FootprintOverviewSnapshot result = null;
            Wait(() => {
                Call(layer, "Update"); result = GetOptional(layer, "overview") as FootprintOverviewSnapshot;
                return result != null && result.Complete && result.RequestSerial == (long)Get(layer, "requestedOverviewSerial");
            }, "200h actual map geometry preparation");
            return result;
        }
        private static string F(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
        private static void ContinuousPlayback(ProbeGraphics graphics, object host, object layer, FootprintStore store, object frame, string output, int rate)
        {
            var input = Get(layer, "input"); var playback = (FootprintPlayback)Get(layer, "Playback");
            bool wasMapped = (bool)Get(input, "mapped"), wasFocused = (bool)Get(input, "IsFocused"), wasPermitted = (bool)Get(input, "nativePermission");
            object uiOwner = GetOptional(layer, "UiOwnsInput");
            var rows = new List<string> { "wallMs,cursorTicks,requestedSerial,displaySerial,displayUntil,displayComplete,sourceSequence,displayEdges,drawCallbackMs" };
            long initialUntil = ((FootprintOverviewSnapshot)Get(layer, "overview")).Until;
            long lastRequest = -1, lastComplete = -1, completeUntil = initialUntil, source = -1; int requests = 0, completions = 0, partials = 0, frames = 0;
            object previousPartial = null; double drawSum = 0, drawMax = 0;
            var timer = Stopwatch.StartNew();
            try
            {
                // Supply a qualified input sample to the same production Update
                // used by normal UI playback. Physical gestures are separately
                // covered by the retained native input fixture.
                Set(input, "mapped", true); Set(input, "IsFocused", true); Set(input, "nativePermission", true); Set(layer, "UiOwnsInput", null);
                playback.Toggle(store.Snapshot.End); Set(layer, "lastUi", (double)Get(host, "UiSeconds")); Set(layer, "nextOverview", 0d);
                double initialCursor = playback.Cursor;
                while (timer.Elapsed.TotalSeconds < 10)
                {
                    Call(layer, "Update"); var shown = (FootprintOverviewSnapshot)Get(layer, "overview");
                    long serial = (long)Get(layer, "requestedOverviewSerial");
                    if (serial != lastRequest) { requests++; lastRequest = serial; }
                    var prepared = store.OverviewResult;
                    if (prepared != null && !prepared.Complete && !ReferenceEquals(prepared, previousPartial)) { partials++; previousPartial = prepared; }
                    if (shown.Complete && shown.RequestSerial != lastComplete) { completions++; lastComplete = shown.RequestSerial; completeUntil = Math.Max(completeUntil, shown.Until); }
                    Require(shown.Complete, "ongoing playback retains complete past history during new request prefixes");
                    Require(shown.SourceSequence >= source, "ongoing playback source waterline never retreats to a partial prefix"); source = shown.SourceSequence;
                    bool early = false, middleRegion = false;
                    foreach (var edge in shown.Edges)
                    {
                        Require(edge.End <= playback.Cursor, "continuous display snapshot contains no future edge");
                        early |= edge.X1 < 500 && edge.Y1 < 500;
                        middleRegion |= edge.X1 >= 5200 && edge.X1 <= 5500 && edge.Y1 >= 290 && edge.Y1 <= 340;
                    }
                    // The generator revisits the same regions. Their earliest
                    // representative may carry an older timestamp after coverage
                    // deduplication; spatial presence is the independent oracle.
                    Require(early && middleRegion, "continuous playback preserves both early-region and middle-cursor-region branches");
                    long callbackTicks = 0;
                    graphics.Pixels(() => { long from = Stopwatch.GetTimestamp(); Call(layer, "DrawRoutes", frame); callbackTicks = Stopwatch.GetTimestamp() - from; }, Matrix.Identity);
                    double draw = callbackTicks * 1000d / Stopwatch.Frequency; drawSum += draw; drawMax = Math.Max(drawMax, draw); frames++;
                    rows.Add(String.Join(",", F(timer.Elapsed.TotalMilliseconds), F(playback.Cursor), serial, shown.RequestSerial, shown.Until, shown.Complete, shown.SourceSequence, shown.Edges.Length, F(draw)));
                    double delay = frames * 1000d / rate - timer.Elapsed.TotalMilliseconds;
                    if (delay >= 1) Thread.Sleep((int)Math.Min(50, delay));
                }
                Require(playback.Playing && playback.Cursor - initialCursor >= 9 * TimeSpan.TicksPerSecond, "real monotonic Advance continues during the ten-second worker window");
                Require(completions >= 2 && completeUntil > initialUntil + TimeSpan.TicksPerSecond, "complete historical publication advances while playback requests remain live");
            }
            finally { playback.Seek(playback.Cursor, store.Snapshot.End); Set(input, "mapped", wasMapped); Set(input, "IsFocused", wasFocused); Set(input, "nativePermission", wasPermitted); Set(layer, "UiOwnsInput", uiOwner); }
            string label = "footprints-real-200h-continuous-" + rate;
            File.WriteAllLines(Path.Combine(output, label + ".csv"), rows);
            File.WriteAllText(Path.Combine(output, label + ".json"), "{\"requestedHz\":" + rate + ",\"elapsedSeconds\":" + F(timer.Elapsed.TotalSeconds) + ",\"drawFrames\":" + frames + ",\"requests\":" + requests + ",\"completePublications\":" + completions + ",\"observedPartialPublications\":" + partials + ",\"initialUntil\":" + initialUntil + ",\"finalCompleteUntil\":" + completeUntil + ",\"drawCallbackMeanMs\":" + F(drawSum / frames) + ",\"drawCallbackMaxMs\":" + F(drawMax) + "}");
        }
        private static void Wait(Func<bool> ready, string context)
        { var timer = Stopwatch.StartNew(); while (!ready()) { if (timer.ElapsedMilliseconds > 120000) throw new TimeoutException(context); Thread.Sleep(5); } }
    }
}
