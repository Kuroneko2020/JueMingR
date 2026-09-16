using System;
using System.Threading;
using JueMingR.Features.MapMarkers;
using JueMingR.Features.Exploration;
using JueMingR.Platform.Settings;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeMapFeedbackChecks
    {
        internal static void Run(object host)
        {
            Call(host, "TakeFeedback", (Action<string>)(_ => { })); // Drain the preceding input scenario's creation message.
            string a = new string('a', 64), b = new string('b', 64);
            var originalMarkers = Get(host, "Markers"); var originalHistory = Get(host, "history");
            var gate = new Storage(true); var second = new Storage(false); long now = 0;
            var markers = new MarkerLibrary(key => key == a ? gate : second);
            var history = new ExplorationHistory(key => key == a ? gate : second, () => now);
            try
            {
                Set(host, "<Markers>k__BackingField", markers); Set(host, "history", history);
                markers.BeginSession(1, 128, 64); markers.UsePair(a); Until(() => { markers.Poll(); return markers.Loaded; });
                markers.Create(new MarkerRecord(new string('1', 32), 1.25, 2.5, 8, "旧世界")); Require(gate.Entered.Wait(3000), "retired marker write reached storage");
                markers.BeginSession(2, 128, 64); markers.UsePair(b); Until(() => { markers.Poll(); return markers.Loaded; });
                gate.Release.Set(); Until(() => { markers.Poll(); return markers.BackgroundError != null; });
                int displayed = 0; Action<string> display = value => { Require(value.Contains("先前世界") && value.Contains("标记"), "actual Chinese marker feedback"); displayed++; };
                Call(host, "TakeFeedback", display); Call(host, "TakeFeedback", display);
                Require(displayed == 1 && markers.Error == null && markers.CanEdit, "old marker failure visible once without poisoning B");
                gate.Entered.Reset(); gate.Release.Reset();
                history.BeginSession(128, 64); history.UsePair(a); Until(() => { history.Poll(); return history.Loaded; }); history.Offer(9, DateTime.UtcNow.Ticks); now = 10000; history.Poll(); Require(gate.Entered.Wait(3000), "retired summary write reached storage");
                history.BeginSession(128, 64); history.UsePair(b); Until(() => { history.Poll(); return history.Loaded; }); history.Offer(12, DateTime.UtcNow.Ticks); now = 20000; Until(() => { history.Poll(); return history.Saved; });
                gate.Release.Set(); Until(() => { history.Poll(); return history.BackgroundError != null; });
                displayed = 0; display = value => { Require(value.Contains("先前世界") && value.Contains("统计"), "actual Chinese summary feedback"); displayed++; };
                Call(host, "TakeFeedback", display); Call(host, "TakeFeedback", display);
                Require(displayed == 1 && history.Error == null && history.Saved, "old summary failure visible once after B save succeeds");
                Console.WriteLine("PASS: retired world asset/summary storage failures reach actual Host feedback once and preserve healthy B state.");
            }
            finally { gate.Release.Set(); markers.Stop(5000); history.Stop(5000); Set(host, "<Markers>k__BackingField", originalMarkers); Set(host, "history", originalHistory); }
        }
        private sealed class Storage : IPreferenceStorage
        {
            internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim(), Release = new ManualResetEventSlim();
            private readonly bool fail;
            internal Storage(bool fail) { this.fail = fail; }
            public PreferenceReadResult Read() { return new PreferenceReadResult(PreferenceReadStatus.Missing, null, "missing", null); }
            public PreferenceWriteResult Write(string expected, byte[] bytes)
            { if (fail) { Entered.Set(); if (!Release.Wait(5000)) throw new TimeoutException("feedback storage"); return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, expected, "isolated-write-failure"); } return new PreferenceWriteResult(PreferenceWriteStatus.Saved, "saved", null); }
            public void Dispose() { }
        }
        private static void Until(Func<bool> done) { if (!SpinWait.SpinUntil(done, 5000)) throw new TimeoutException("feedback result"); }
    }
}
