using System;
using System.IO;
using System.Reflection;
using Terraria.Localization;

namespace NativeWorldTextProbe
{
    internal static class NativeDeathSourceChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run()
        {
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            var safe = assembly.GetType("JueMingR.TerrariaHost.DeathHistory.NativeDeathText", true);
            var text = NetworkText.FromFormattable("{0} / {1}", NetworkText.FromLiteral("[i:1] 玩家"), NetworkText.FromFormattable("{0}!", "测试"));
            string frozen = (string)safe.GetMethod("Freeze", Flags).Invoke(null, new object[] { text });
            NativeInformationChecks.Require(frozen == "[i:1] 玩家 / 测试!", "freeze original nested text as plain text");
            var malformed = NetworkText.FromFormattable("{oops", "child");
            string before = (string)typeof(NetworkText).GetField("_text", Flags).GetValue(malformed);
            safe.GetMethod("Freeze", Flags).Invoke(null, new object[] { malformed });
            NativeInformationChecks.Require((string)typeof(NetworkText).GetField("_text", Flags).GetValue(malformed) == before, "format failure cannot mutate vanilla NetworkText");
            NativeDeathCauseChecks.Run(safe);
            var deaths = assembly.GetType("JueMingR.TerrariaHost.DeathHistory.DeathSourceHooks", true);
            var time = assembly.GetType("JueMingR.TerrariaHost.WorldTime.WorldTimeSourceHooks", true);
            var token = new object(); var captured = new System.Collections.Generic.List<JueMingR.Platform.DeathHistory.DeathFact>();
            object deathHooks = Activator.CreateInstance(deaths, Flags, null, new object[] { new Func<Terraria.Player, object>(player => token), new Action<object, JueMingR.Platform.DeathHistory.DeathFact, DateTime>((owner, fact, stamp) => captured.Add(fact)) }, null);
            object timeHooks = Activator.CreateInstance(time, Flags, null, new object[] { new Action<double>(value => { }) }, null);
            try
            {
                NativeInformationChecks.Call(deathHooks, "Install"); NativeInformationChecks.Call(timeHooks, "Install");
                NativeInformationChecks.Require((bool)NativeInformationChecks.Get(deathHooks, "Ready"), "actual native KillMe transition/text matcher must install");
                NativeInformationChecks.Require((bool)NativeInformationChecks.Get(timeHooks, "Ready"), "actual native time contribution matcher must install");
            }
            finally { ((IDisposable)deathHooks).Dispose(); ((IDisposable)timeHooks).Dispose(); }
            NativeDeathExecutionChecks.Run(assembly);
            string root = Path.Combine(Terraria.Program.SavePath, "death-composition"); Directory.CreateDirectory(root);
            Terraria.Main.gameMenu = Terraria.Main.dedServ = false; Terraria.Main.netMode = Terraria.Main.myPlayer = 0;
            Terraria.Main.player[0] = new Terraria.Player { active = true, name = "fixture" };
            Terraria.Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(root, "fixture.plr"), false) { Player = Terraria.Main.player[0] };
            Terraria.Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "fixture.wld"), false) { UniqueId = Guid.NewGuid() };
            var worker = assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true);
            object context = Activator.CreateInstance(worker.GetNestedType("PostfixContext", Flags), Flags, null,
                new object[] { "death-history-" + new string('8', 40), Path.Combine(root, "evidence.txt"), root }, null);
            try
            {
                NativeInformationChecks.Call(context, "InstallInformationSources"); NativeInformationChecks.Call(context, "InitializeRuntime", true);
                NativeInformationChecks.Require(NativeInformationChecks.GetOptional(context, "DeathRecords") != null, "complete death profile composes real records/time owner");
                var map = NativeInformationChecks.Get(NativeInformationChecks.Get(context, "DeathRecords"), "Map");
                NativeInformationChecks.Require((bool)NativeInformationChecks.Get(map, "Ready"), "native fullscreen map hooks install: " + NativeInformationChecks.GetOptional(map, "Failure"));
                NativeInformationChecks.Require(NativeInformationChecks.GetOptional(context, "Guidance") != null && NativeInformationChecks.GetOptional(context, "items") != null, "complete death profile preserves prior feature chain");
                NativeDeathHostChecks.Run(context, root);
            }
            finally
            {
                var records = NativeInformationChecks.GetOptional(context, "DeathRecords"); if (records != null) NativeInformationChecks.Call(records, "OnExit", null, EventArgs.Empty);
                NativeInformationChecks.StopContext(context);
            }
            Console.WriteLine("PASS: native death text freezing preserves source and nested plain text.");
        }
    }
}
