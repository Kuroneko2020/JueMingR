using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using JueMingR.Features.Footprints;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFootprintTimingChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run()
        {
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            var type = assembly.GetType("JueMingR.TerrariaHost.Footprints.FootprintPlayingClock", true);
            foreach (int frequency in new[] { 30, 60, 120 })
            {
                long now = 0, elapsed = 0; object clock = Activator.CreateInstance(type, Flags, null, new object[] { (Func<long>)(() => now), 3600L }, null);
                Call(clock, "Begin");
                for (int i = 0; i < 60 * frequency; i++)
                {
                    now += 3600 / frequency; elapsed += (long)Call(clock, "Completed");
                    Require((long)Call(clock, "Completed") == 0, "same-time callback cannot count an interval twice");
                    Call(clock, "Begin"); // A normal outer/frame skip must keep its anchor.
                }
                Require(elapsed == 60 * TimeSpan.TicksPerSecond, "one real minute at " + frequency + " callbacks/s");
                Call(clock, "Suspend"); now += 3600 * 600; Call(clock, "Begin");
                Require((long)Call(clock, "Completed") == 0, "pause/offline/recording-off interval is excluded");
                now += 3600; Require((long)Call(clock, "Completed") == TimeSpan.TicksPerSecond, "normal frame wait is included after resume");
                now += 3600 * 3; Call(clock, "Begin"); Require((long)Call(clock, "Completed") == 3 * TimeSpan.TicksPerSecond, "ordinary throttled updates do not truncate real time");
            }
            long coarseNow = 0, total = 0;
            object fractional = Activator.CreateInstance(type, Flags, null, new object[] { (Func<long>)(() => coarseNow), 3L }, null);
            Call(fractional, "Begin"); for (int i = 0; i < 3; i++) { coarseNow++; total += (long)Call(fractional, "Completed"); }
            Require(total == TimeSpan.TicksPerSecond, "fractional source conversion retains remainder");
            var playback = new FootprintPlayback(); long end = 200L * 3600 * TimeSpan.TicksPerSecond;
            playback.Show(end); playback.Toggle(end); playback.Advance(.125, end, true);
            Require(playback.Cursor == TimeSpan.TicksPerSecond / 8d && FootprintPlayback.FormatTime(end) == "200:00:00", "playback uses fixed real duration and full hours");
            playback.Advance(.125, end, false); Require(!playback.Playing, "loss of UI input/focus pauses playback without deferred catch-up");
            playback.GoLatest(end); playback.CycleSpeed(); Require(playback.Latest && !playback.Playing && playback.Speed == 10, "latest and speed controls retain their independent behavior");
            Console.WriteLine("PASS: production footprint real-time clock 30/60/120 Hz, duplicate callbacks, pause/re-entry, normal waits, fractional conversion and timeline units.");
        }
        internal static void RunHost(object host)
        {
            var field = host.GetType().GetField("playingClock", Flags); object previousClock = field.GetValue(host);
            var type = previousClock.GetType(); long now = 0;
            object controlled = Activator.CreateInstance(type, Flags, null, new object[] { (Func<long>)(() => now), 1000L }, null);
            var preparation = typeof(Main).GetField("_worldPreparationState", Flags); object oldPreparation = preparation.GetValue(null);
            bool oldGameplay = Main.CanUpdateGameplay, oldMenu = Main.gameMenu, oldPaused = Main.gamePaused, oldMap = Main.mapFullscreen;
            Main previousMain = Main.instance;
            try
            {
                field.SetValue(host, controlled);
                if (Main.instance == null) { Main.instance = (Main)FormatterServices.GetUninitializedObject(typeof(Main)); GC.SuppressFinalize(Main.instance); }
                preparation.SetValue(null, Main.WorldPreparationState.Ready); Main.ToggleGameplayUpdates(true); Main.gameMenu = Main.gamePaused = false;
                Call(controlled, "Begin"); now += 1000;
                Call(host, "BeforeOuterUpdate"); Call(host, "AfterOuterUpdate", true);
                Require((long)Call(controlled, "Completed") == TimeSpan.TicksPerSecond, "ordinary outer early return retains normal frame interval");
                Main.gamePaused = true; Call(host, "AfterOuterUpdate", true); now += 600000; Main.gamePaused = false;
                Call(controlled, "Begin"); Require((long)Call(controlled, "Completed") == 0, "native true pause invalidates anchor");
                Main.mapFullscreen = true; now += 1000; Call(host, "BeforeOuterUpdate"); Call(host, "AfterOuterUpdate", true);
                Require((long)Call(controlled, "Completed") == TimeSpan.TicksPerSecond, "map flag alone does not stop actual play time");
                Main.ToggleGameplayUpdates(false); Call(host, "BeforeOuterUpdate"); now += 600000; Main.ToggleGameplayUpdates(true);
                Call(controlled, "Begin"); Require((long)Call(controlled, "Completed") == 0, "native gameplay-preparation gate excludes interval");
                Main.gameMenu = true; Call(host, "BeforeOuterUpdate"); now += 600000; Main.gameMenu = false;
                Call(controlled, "Begin"); Require((long)Call(controlled, "Completed") == 0, "native menu gate excludes interval");
            }
            finally
            {
                Call(previousClock, "Suspend"); field.SetValue(host, previousClock); preparation.SetValue(null, oldPreparation);
                Main.ToggleGameplayUpdates(oldGameplay); Main.gameMenu = oldMenu; Main.gamePaused = oldPaused; Main.instance = previousMain; Main.mapFullscreen = oldMap;
            }
            Console.WriteLine("PASS: production Host native gates distinguish ordinary skipped update, true pause, running map, preparation and menu.");
        }
    }
}
