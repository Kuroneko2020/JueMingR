using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Tests the actual version-bound world substream and receiving decoder,
    // including a fresh process, without claiming an entire NPC family audit.
    internal static class NativeJourneyPredictionChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static int Run(string content, string output)
        {
            output = Path.GetFullPath(output); Directory.CreateDirectory(output);
            Terraria.Program.SavePath = Path.Combine(output, "isolated-root"); Directory.CreateDirectory(Terraria.Program.SavePath);
            NativeTestAudio.Install(typeof(Main).Assembly);
            NativeQuickItemChecks.Run(context =>
            {
                NativeCombatAimChecks.Initialize();
                Assembly host = context.GetType().Assembly;
                var snapshot = host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeWorldSnapshot", true);
                var field = typeof(Main).GetField("_gameModeDifficultyOverride", Flags);
                object prior = field.GetValue(null); int mode = Main.GameMode; bool good = Main.getGoodWorld;
                try
                {
                    Main.GameMode = 0; Main.getGoodWorld = false;
                    float?[] values = { null, 0.5f, 1f, 2f, 3f };
                    for (int i = 0; i < values.Length; i++)
                    {
                        field.SetValue(null, values[i]);
                        float expected = values[i] ?? 1f;
                        Require(Main.Difficulty == expected, "native source exposes effective difficulty independently of base mode");
                        using (var stream = File.Create(Path.Combine(output, "world-" + i + ".bin")))
                        using (var writer = new BinaryWriter(stream)) snapshot.GetMethod("Write", Flags).Invoke(null, new object[] { writer });
                    }
                    var npc = new NPC(); npc.SetDefaults(NPCID.Zombie); npc.active = true; npc.whoAmI = 0; Main.npc[0] = npc;
                    var alignment = host.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativePredictionAlignment", true);
                    var observe = alignment.GetMethod("Observe", Flags); var difference = alignment.GetMethod("Difference", Flags);
                    field.SetValue(null, 1f);
                    object first = observe.Invoke(null, new object[] { 10L, new[] { 0 }, new int[0], 0 });
                    object equal = observe.Invoke(null, new object[] { 10L, new[] { 0 }, new int[0], 0 });
                    Require(difference.Invoke(null, new[] { first, equal }) == null, "unchanged native world retains the same continuity premise");
                    field.SetValue(null, 2f);
                    object changed = observe.Invoke(null, new object[] { 10L, new[] { 0 }, new int[0], 0 });
                    Require((string)difference.Invoke(null, new[] { first, changed }) == "world premise" && Main.GameMode == 0,
                        "effective difficulty change invalidates the actual old future while persistent GameMode is unchanged");
                }
                finally { field.SetValue(null, prior); Main.GameMode = mode; Main.getGoodWorld = good; }
            }, shortFeedback: true);
            var start = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location)
            {
                Arguments = Quote(Program.Repository) + " " + Quote(content) + " " + Quote(output) + " JourneyPredictionRead",
                WorkingDirectory = output, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var child = Process.Start(start))
            {
                var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
                if (!child.WaitForExit(45000)) { child.Kill(); throw new TimeoutException("Owned prediction world receiver timed out."); }
                string log = stdout.Result + stderr.Result; File.WriteAllText(Path.Combine(output, "receiver.log"), log);
                Require(child.ExitCode == 0, "fresh actual snapshot decoder and native difficulty consumers: " + log);
            }
            Console.WriteLine("PASS JOURNEY PREDICTION world snapshot null/x0.5/x1/x2/x3 -> fresh receiver; stable and changed continuity controls; no whole-NPC/worker-family claim.");
            return 0;
        }

        internal static int ReadChild(string output)
        {
            output = Path.GetFullPath(output);
            Terraria.Program.SavePath = Path.Combine(output, "receiver-root"); Directory.CreateDirectory(Terraria.Program.SavePath);
            NativeTestAudio.Install(typeof(Main).Assembly);
            NativeQuickItemChecks.Run(context =>
            {
                NativeCombatAimChecks.Initialize();
                var snapshot = context.GetType().Assembly.GetType("JueMingR.TerrariaHost.Combat.Prediction.NativeWorldSnapshot", true);
                var field = typeof(Main).GetField("_gameModeDifficultyOverride", Flags);
                float?[] values = { null, 0.5f, 1f, 2f, 3f };
                for (int i = 0; i < values.Length; i++)
                {
                    field.SetValue(null, 3f); Main.GameMode = 2;
                    using (var stream = File.OpenRead(Path.Combine(output, "world-" + i + ".bin")))
                    using (var reader = new BinaryReader(stream)) snapshot.GetMethod("Read", Flags).Invoke(null, new object[] { reader });
                    Require(Main.GameMode == 0 && Equals(field.GetValue(null), values[i]), "receiver restores exact nullable override with source base mode");
                    Require(Main.Difficulty == (values[i] ?? 1f), "original Main.Difficulty consumes the received override");
                    var received = new NPC(); received.SetDefaults(NPCID.Zombie);
                    int actualLife = received.lifeMax, actualDamage = received.damage;
                    field.SetValue(null, values[i]); var control = new NPC(); control.SetDefaults(NPCID.Zombie);
                    Require(actualLife == control.lifeMax && actualDamage == control.damage, "native NPC stat consumer matches the same exact difficulty control");
                    Console.WriteLine("JOURNEY-PREDICTION-RECEIVE mode=" + Main.GameMode + " override=" + (values[i]?.ToString() ?? "null") + " difficulty=" + Main.Difficulty + " life=" + actualLife);
                }
            }, shortFeedback: true);
            return 0;
        }
        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    }
}
