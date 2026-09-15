using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.DeathHistory;
using JueMingR.Features.WorldTime;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Creative;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Executes the actual full composition and native observation callbacks in
    // the isolated probe. No player/world file is opened and no game is run.
    internal static class NativeDeathHostChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run(object context, string root)
        {
            object host = Get(context, "DeathRecords");
            var history = (DeathHistory)Get(host, "History"); var time = (WorldTimeHistory)Get(host, "Time");
            var player = Main.LocalPlayer; player.position = new Vector2(320, 640);
            Main.maxTilesX = 4200; Main.maxTilesY = 1200; Main.mapFullscreen = Main.hideUI = false;
            Main.dayTime = true; Main.fastForwardTimeToDawn = false; Main.fastForwardTimeToDusk = true; Main.time = 100;
            CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(false);
            NativeDeathExecutionChecks.WithIsolation(() =>
            {
                // These facts arrive before the Runtime postfix admits Session.
                player.KillMe(PlayerDeathReason.ByCustomReason("当次 [i:1] 原句"), 1, 0);
                typeof(Main).GetMethod("UpdateTime", Flags).Invoke(null, null);
                Until(() => { Call(context, "UpdateRuntime"); return history.Snapshot.Known && time.Known && (bool)Get(host, "ControlsEnabled"); });
                Call(host, "RequestDetails", 0L);
                Until(() => history.Snapshot.Count == 1 && history.Snapshot.Rows.Count == 1);
                long session = (long)Get(host, "Session");
                Require(time.Total == 60 && history.Snapshot.Rows[0].Reason == "当次 [i:1] 原句", "native before-postfix facts reach full Host owners exactly once");
                Require((string)Get(host, "CountText") == "1" && (string)Get(host, "DaysText") == "0", "actual fixed rows consume admitted scalar values");
                player.KillMe(PlayerDeathReason.ByCustomReason("重复"), 1, 0);
                new Player { active = true, name = "其它玩家" }.KillMe(PlayerDeathReason.ByCustomReason("其它玩家"), 1, 0);
                Call(context, "UpdateRuntime");
                Require((long)Get(host, "Session") == session && history.Snapshot.Count == 1, "death keeps full Session; repeat and foreign player add no local record");
                player.dead = false; player.KillMe(PlayerDeathReason.ByCustomReason("第二次"), 1, 0);
                Until(() => { Call(context, "UpdateRuntime"); return history.Snapshot.Count == 2; });
                string firstDays = (string)Get(host, "DaysText");
                typeof(Main).GetMethod("UpdateTime", Flags).Invoke(null, null); Main.SkipToTime(1234, Main.dayTime);
                Require(time.Total == 120 && ReferenceEquals(firstDays, Get(host, "DaysText")), "actual contributions count while dead; direct set and unchanged integer create no day text");
                // Native fast-forward takes precedence over FreezeTime. End it
                // before exercising the ordinary frozen branch.
                Main.fastForwardTimeToDusk = false;
                CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>().SetPowerInfo(true);
                typeof(Main).GetMethod("UpdateTime", Flags).Invoke(null, null);
                Require(time.Total == 120, "full Host freeze contributes zero");
            });
            Until(() => history.Snapshot.Pending == 0); Require(history.Snapshot.Error == null, "real Host archive committed before re-entry");
            Preferences(context, host, root);
            MapCpu(host);
            var pair = (string)Get(host, "pair");
            Main.gameMenu = true; Call(context, "UpdateRuntime");
            Require((long)Get(host, "Session") == -1 && !history.Snapshot.Known, "leaving world clears current record view");
            Main.gameMenu = false; player.dead = false;
            Until(() => { Call(context, "UpdateRuntime"); return history.Snapshot.Known && time.Known; });
            Require((string)Get(host, "pair") == pair && history.Snapshot.Count == 2 && time.Total == 120, "re-entry restores same reliable identity and observed tail");
            NativeDeathInputChecks.Run(context, host);
            Call(host, "MarkMissed"); Require((string)Get(host, "CountText") == "记录可能不完整", "missed acceptance marks its own reliable pair");
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "another.wld"), false) { UniqueId = Guid.NewGuid() };
            Until(() => { Call(context, "UpdateRuntime"); return (string)Get(host, "pair") != pair && history.Snapshot.Known && time.Known; });
            Require(history.Snapshot.Count == 0 && time.Total == 0 && history.Snapshot.Markers.Count == 0 && (string)Get(host, "CountText") == "0", "world change cannot expose earlier identity rows/time/markers/completeness error");
            NativeDeathMapLoopChecks.Run(context, host);
            Console.WriteLine("PASS: full Host native pre-admission/dead/revive/foreign-player chain, scalar rows, session re-entry, world isolation, preferences and map CPU geometry.");
        }
        private static void Preferences(object context, object host, string root)
        {
            var before = (PreferenceSnapshot<DeathDisplayPreferences>)Get(host, "Preferences");
            Require(!before.Value.Enabled && before.Value.Count == 256, "full Host defaults off/256");
            Call(host, "SetCount", 256);
            Require(ReferenceEquals(before, Get(host, "Preferences")), "same-value actual preference creates no revision or save");
            var bindings = (HotkeyBindings)Get(Get(Get(context, "Shell"), "hotkeys"), "Bindings");
            Until(() => { bindings.Poll(); return bindings.Loaded; });
            Require(bindings.Get("death-markers.toggle") == null, "new common action starts unbound");
            foreach (int count in new[] { 128, 1024, 128, 512 }) Call(host, "SetCount", count);
            Require(!((DeathDisplayPreferences)Get(host, "Settings")).Enabled && bindings.Get("death-markers.toggle") == null, "quantity changes neither switch nor binding");
            Until(() => ((PreferenceSnapshot<DeathDisplayPreferences>)Get(host, "Preferences")).Status == PreferenceStatus.Saved);
            Require((bool)Call(Get(host, "preferences"), "Stop", 750), "settings worker retires before fresh reader");
            var fresh = new PreferenceDocument<DeathDisplayPreferences>(new AtomicFileDocument(Path.Combine(root, "JueMingRData/config/features/death-markers.json"), 65536, true), new DeathDisplayCodec(), DeathDisplayPreferences.Default);
            Set(host, "preferences", fresh); Until(() => fresh.Snapshot.IsLoaded);
            Require(fresh.Snapshot.Value.Count == 512 && !fresh.Snapshot.Value.Enabled, "real file writer and fresh reader retain final quantity independently");
        }
        private static void MapCpu(object host)
        {
            var map = Get(host, "Map"); var type = map.GetType(); var assembly = type.Assembly;
            var geometry = assembly.GetType("JueMingR.TerrariaHost.DeathHistory.DeathMapGeometry", true);
            foreach (float zoom in new[] { .5f, 1f, 3f }) foreach (float scale in new[] { .8f, 1f, 1.5f })
            {
                object value = geometry.GetMethod("Project", Flags).Invoke(null, new object[] { 1600f, 800f, new Vector2(10, 5), new Vector2(30, 40), zoom, scale, 32, 32 });
                var center = (Vector2)Get(value, "Center"); object hit = Get(value, "Hit"), icon = Get(value, "Icon");
                Require(Math.Abs(center.X - (90 * zoom + 30)) < .001f && Math.Abs(center.Y - (45 * zoom + 38 + zoom * .4f)) < .001f, "native pixel/tile anchor and death Y correction");
                Require(Math.Abs((float)Get(icon, "Width") - 32 * scale) < .001f, "icon UI scale is independent of map zoom");
                Require((bool)Call(value, "Contains", (float)Get(hit, "X"), (float)Get(hit, "Y")) && (bool)Call(value, "Contains", (float)Get(hit, "Right"), (float)Get(hit, "Bottom")), "native hit rectangle includes both edges");
                Require(!(bool)Call(value, "Contains", (float)Get(hit, "X") - 1, (float)Get(hit, "Y")), "outside hit does not claim pointer");
            }
            long projections = (long)Get(map, "Projections"), draws = (long)Get(map, "Draws"), hits = (long)Get(map, "Hits");
            var icons = type.GetMethod("IconsDrawn", Flags); var begin = type.GetMethod("BeginMap", Flags); var end = type.GetMethod("EndMap", Flags);
            for (int i = 0; i < 100; i++)
            {
                begin.Invoke(null, null);
                icons.Invoke(null, new object[] { Main.MapIcons, Vector2.Zero, Vector2.Zero, null, 1f, 1f, 255, "vanilla", true });
                end.Invoke(null, new object[] { null });
            }
            Require((long)Get(map, "Projections") == projections && (long)Get(map, "Draws") == draws && (long)Get(map, "Hits") == hits, "closed map actual adapter performs zero projection/draw/hit");
            Call(host, "SetEnabled", true); Main.mapFullscreen = true;
            begin.Invoke(null, null); type.GetMethod("NativeDrawn", Flags).Invoke(null, new object[] { Main.myPlayer, true });
            Require((string)Get(map, "nativeId") == (string)Get(host, "NativeEventId"), "same native event is recognized only after actual-drawn receipt");
            begin.Invoke(null, null); end.Invoke(null, new object[] { null });
            Require((string)Get(map, "nativeId") == (string)Get(host, "NativeEventId"), "ignored nested map pass cannot erase outer native receipt");
            var failure = new Exception("native-map-test"); Require(ReferenceEquals(end.Invoke(null, new object[] { failure }), failure) && GetOptional(map, "nativeId") == null, "map finalizer clears frame identity and preserves original exception");
            Main.hideUI = true; Call(host, "Update", 1UL); Require(!(bool)Get(map, "Active"), "hidden map cancels its demand");
            Main.hideUI = false; Main.mapFullscreen = false; Call(host, "SetEnabled", false);
        }
        private static void Until(Func<bool> done)
        { var clock = System.Diagnostics.Stopwatch.StartNew(); while (!done()) { if (clock.ElapsedMilliseconds > 5000) throw new TimeoutException("death Host readiness"); Thread.Sleep(5); } }
    }
}
