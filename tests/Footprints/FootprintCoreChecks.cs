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
            recorder.Observe(10, 20, FootprintPosition.Valid, false, 60L * 60 * 201 * 60);
            long geometry = recorder.GeometryVersion;
            recorder.Observe(10, 20, FootprintPosition.Valid, false, 1);
            Require(recorder.Count == 1 && recorder.End == 43416001L && recorder.GeometryVersion == geometry, "201 hours plus one step is one stationary sample; duration does not invalidate geometry");
            // No observations during a real pause. Advancing a save/UI clock is not simulation.
            now = 86400000; recorder.FlushDue();
            Require(recorder.End == 43416001L && batches.Count == 1, "wall time never becomes recording time, but pending stay is saved while paused");
            recorder.Observe(10.001f, 20, FootprintPosition.Valid, false);
            recorder.Observe(10.001f, 20.001f, FootprintPosition.Valid, false);
            recorder.Observe(10, 20, FootprintPosition.Valid, false);
            recorder.Observe(10.1f, 20, FootprintPosition.Valid, true);
            recorder.Observe(1000, 20, FootprintPosition.Valid, false);
            recorder.Observe(0, 0, FootprintPosition.Dead, false, 60);
            recorder.Observe(10, 20, FootprintPosition.Valid, false);
            recorder.Flush(); var values = batches[batches.Count - 1];
            Require(values.Length == 8 && recorder.Count == 8, "small movement, turn and reversal each survive");
            Require(values[3].Segment == values[2].Segment && values[4].Segment != values[3].Segment, "short teleport breaks; turns do not");
            Require(values[5].Segment == values[4].Segment, "normal high speed is not a distance-based teleport");
            Require(values[6].Position == FootprintPosition.Dead && values[6].End - values[6].Start == 60 && values[7].Segment != values[5].Segment, "known dead interval retains time without bridging respawn");
            Require(FootprintPlayback.FormatTime(43416000) == "201:00:00", "hours never wrap at 24 or clip at 200");
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
            var player = new FootprintPlayback(); player.Show(600); Require(player.Cursor == 600 && player.Latest && !player.Playing && player.Speed == 1, "open latest paused 1x");
            player.Toggle(600); Require(player.Cursor == 0 && player.Playing, "play from latest starts at beginning");
            for (int i = 0; i < 1000; i++) player.Advance(.001, 600, true);
            Require(Math.Abs(player.Cursor - 60) < .000001, "sub-frame remainder survives UI updates");
            player.Seek(120, 600); player.Advance(100, 1200, true); Require(player.Cursor == 120 && player.DisplayEnd == 600, "paused history does not move or rescale on append");
            player.CycleSpeed(); player.Toggle(1200); player.Advance(1, 1200, true); Require(player.Cursor == 720 && player.Speed == 10, "continue from history with selected speed");
            player.Advance(100, 1200, true); Require(player.Cursor == 1200 && !player.Playing, "end stops without looping");
            player.GoLatest(1300); Require(player.Cursor == 1300 && player.Speed == 10 && !player.Playing, "latest preserves speed");
            player.Hide(); player.Show(1400); Require(player.Cursor == 1400 && player.Speed == 1 && !player.Playing, "reopen resets, never catches up invisible wall time");
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
