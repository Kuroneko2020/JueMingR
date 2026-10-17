using System;
using System.Collections.Generic;
using JueMingR.Features.Footprints;
using JueMingR.Platform.Footprints;

namespace JueMingR.ArchitectureTests
{
    internal static class FootprintCoreChecks
    {
        internal static void Check(IList<string> failures)
        {
            Run(failures, "recording time and continuity", Recording);
            Run(failures, "bounded acceptance and deadline", Capacity);
            Run(failures, "playback independent clock", Playback);
            Run(failures, "three presented confirmations", Confirmation);
        }
        private static void Run(IList<string> failures, string name, Action check)
        { try { check(); } catch (Exception e) { failures.Add("footprints " + name + ": " + e.Message); } }
        internal static void Require(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
        private static void Recording()
        {
            var batches = new List<FootprintSample[]>(); long now = 0;
            var recorder = new FootprintRecorder(0, 0, 0, (items, count) => { var copy = new FootprintSample[count]; Array.Copy(items, copy, count); batches.Add(copy); return true; }, () => now);
            long hours = 201L * 3600 * FootprintSample.TicksPerSecond;
            recorder.Observe(10, 20, FootprintPosition.Valid, false, hours);
            long geometry = recorder.GeometryVersion;
            recorder.Observe(10, 20, FootprintPosition.Valid, false, 1);
            Require(recorder.Count == 1 && recorder.End == hours + 1 && recorder.GeometryVersion == geometry, "long stay uses fixed real-time units without geometry churn");
            now = 86400000; recorder.FlushDue();
            Require(recorder.End == hours + 1 && batches.Count == 1, "save/UI time cannot add unobserved play time");
            recorder.Observe(11, 20, FootprintPosition.Valid, false, 100);
            recorder.Observe(12, 20, FootprintPosition.Valid, false, 100);
            recorder.Observe(12, 20, FootprintPosition.Valid, false, 1000);
            recorder.Observe(12, 20, FootprintPosition.Valid, false, 1000);
            recorder.Observe(10, 20, FootprintPosition.Valid, false, 100);
            recorder.Observe(10.1f, 20, FootprintPosition.Valid, true, 100);
            recorder.Observe(1000, 20, FootprintPosition.Valid, false, 100);
            recorder.Observe(0, 0, FootprintPosition.Dead, false, 100);
            recorder.Observe(10, 20, FootprintPosition.Valid, false, 100);
            recorder.Flush(); var values = batches[batches.Count - 1];
            Require(values[1].Moving && values[1].FromX == 10 && values[1].X == 12 && values[1].End - values[1].Start == 200, "constant movement is a timed interval");
            Require(!values[2].Moving && values[2].End - values[2].Start == 2000 && values[2].X == 12, "arrival and long dwell do not smear into slow motion");
            Require(values[3].Moving && values[3].X == 10 && values[3].Segment == values[2].Segment, "reversal retains endpoint without a false break");
            Require(values[4].Segment != values[3].Segment && !values[4].Moving, "short teleport starts a new position without bridging");
            Require(values[5].Segment == values[4].Segment && values[5].Moving, "ordinary high speed is not distance-based teleport");
            Require(!values[6].HasPosition && values[7].Segment != values[5].Segment, "dead interval has no invented position");
            Require(FootprintPlayback.FormatTime(hours) == "201:00:00", "hours do not wrap or clip");
            long prior = recorder.End; recorder.Observe(1, 1, FootprintPosition.Valid, true, 0); Require(recorder.End == prior, "zero-time events never invent duration");
            var drift = new FootprintRecorder(0, 0, 0, (items, count) => true, () => 0);
            for (int i = 0; i < 3600; i++) drift.Observe(6000f + i * .001f / 60, 900, FootprintPosition.Valid, false, FootprintSample.TicksPerSecond / 60);
            Require(drift.Count < 20 && drift.At(drift.ActiveCount - 1).X > 6000.05f, "float-quantized slow drift stays compact and accumulates genuine displacement");
        }
        private static void Capacity()
        {
            bool accept = false; int offers = 0; long now = 0;
            var recorder = new FootprintRecorder(0, 0, 0, (items, count) => { offers++; return accept; }, () => now);
            for (int i = 0; i < 256; i++) Require(recorder.Observe(i, 10, FootprintPosition.Valid, i % 2 == 0), "within bounded active tail");
            long end = recorder.End; Require(!recorder.Observe(999, 10, FootprintPosition.Valid, false), "full queue refuses new sample honestly");
            Require(recorder.End == end && recorder.Count == 256 && recorder.Blocked, "refusal does not fabricate time or discard accepted facts");
            accept = true; Require(recorder.Flush() && recorder.Observe(999, 10, FootprintPosition.Valid, false), "queue recovery resumes in a new segment");
            int before = offers; now = 9999; recorder.FlushDue(); Require(offers == before, "deadline not early");
            now = 10000; recorder.Observe(999.01f, 10, FootprintPosition.Valid, true); recorder.FlushDue();
            Require(offers == before + 1, "new segment does not push first dirty deadline back");
        }
        private static void Playback()
        {
            var player = new FootprintPlayback(); player.Show(600 * FootprintSample.TicksPerSecond); Require(player.Cursor == 600 * FootprintSample.TicksPerSecond && player.Latest && !player.Playing && player.Speed == 1, "open latest paused 1x");
            player.Toggle(600 * FootprintSample.TicksPerSecond); Require(player.Cursor == 0 && player.Playing, "play from latest starts at beginning");
            for (int i = 0; i < 1000; i++) player.Advance(.001, 600 * FootprintSample.TicksPerSecond, true);
            Require(Math.Abs(player.Cursor - FootprintSample.TicksPerSecond) < .01, "sub-frame remainder survives UI updates");
            player.Seek(120 * FootprintSample.TicksPerSecond, 600 * FootprintSample.TicksPerSecond); player.Advance(100, 1200 * FootprintSample.TicksPerSecond, true); Require(player.Cursor == 120 * FootprintSample.TicksPerSecond && player.DisplayEnd == 600 * FootprintSample.TicksPerSecond, "paused history does not move or rescale on append");
            player.CycleSpeed(); player.Toggle(1200 * FootprintSample.TicksPerSecond); player.Advance(1, 1200 * FootprintSample.TicksPerSecond, true); Require(player.Cursor == 130 * FootprintSample.TicksPerSecond && player.Speed == 10, "continue from history with selected speed");
            player.Advance(200, 1200 * FootprintSample.TicksPerSecond, true); Require(player.Cursor == 1200 * FootprintSample.TicksPerSecond && !player.Playing, "end stops without looping");
            player.GoLatest(1300 * FootprintSample.TicksPerSecond); Require(player.Cursor == 1300 * FootprintSample.TicksPerSecond && player.Speed == 10 && !player.Playing, "latest preserves speed");
            player.Hide(); player.Show(1400 * FootprintSample.TicksPerSecond); Require(player.Cursor == 1400 * FootprintSample.TicksPerSecond && player.Speed == 1 && !player.Playing, "reopen resets, never catches up invisible wall time");
        }
        private static void Confirmation()
        {
            var confirm = new FootprintClearConfirmation(); confirm.Open("archive-a");
            Require(!confirm.Release("archive-a") && confirm.Stage == 0, "old release does not activate");
            confirm.Presented(); confirm.Press("archive-a"); Require(!confirm.Release("archive-a") && confirm.Stage == 1 && confirm.Label == "确定？", "first click only stage one");
            confirm.Press("archive-a"); Require(!confirm.Release("archive-a") && confirm.Stage == 1, "without another presentation no next stage");
            confirm.Presented(); confirm.Press("archive-a"); Require(!confirm.Release("archive-a") && confirm.Stage == 2 && confirm.Label == "不可恢复，确定？", "second click only stage two");
            Require(!confirm.Release("archive-a"), "duplicate callback does not cross final stage");
            confirm.Presented(); confirm.Press("archive-a"); Require(confirm.Release("archive-a"), "third separately presented click accepts exactly once");
            Require(!confirm.Release("archive-a"), "command not repeated");
            confirm.Open("archive-a"); confirm.Presented(); confirm.Press("archive-a"); Require(!confirm.Release("archive-b") && confirm.Stage == 0, "different archive cancels");
        }
    }
}
