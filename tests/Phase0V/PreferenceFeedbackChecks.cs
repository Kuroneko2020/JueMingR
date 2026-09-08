using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Terraria
{
    // Real Host owners and F5 -> Main.NewText, on children of the already marked
    // fixture installation. No graphics device, default data root or game input.
    internal static class PreferenceFeedbackChecks
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object runtime;
        private static Assembly host;
        private static string root;

        internal static void Run(object context)
        {
            runtime = Get(context, "Runtime"); host = context.GetType().Assembly;
            root = Path.Combine(Path.GetDirectoryName(typeof(Main).Assembly.Location), "feedback-checks");
            Directory.CreateDirectory(root);
            bool menu = Main.gameMenu, hidden = Main.hideUI;
            var failures = new List<string>();
            try
            {
                Main.gameMenu = false; Main.hideUI = false;
                Run(failures, "silent save and restart", Normal);
                Run(failures, "independent persistent errors", IndependentErrors);
                Run(failures, "deferred and failed presentation", Deferred);
                Run(failures, "real conflict and new cause", Conflict);
                Run(failures, "unsupported, unknown, I/O and busy", OtherErrors);
            }
            finally { Main.gameMenu = menu; Main.hideUI = hidden; }
            if (failures.Count != 0) throw new InvalidOperationException(String.Join("; ", failures));
            Console.WriteLine("PASS: real Host/F5 silent saves, independent error deduplication and delivery boundaries.");
        }

        private static void Normal()
        {
            string path = Path.Combine(root, "normal");
            using (var test = new Session(path))
            {
                Check(test.Status("biome") == "Missing" && test.Status("ui") == "Missing", "first missing documents");
                Check(Capture(test.Pump) == "", "missing defaults must be silent");
                // Hold only the test's command lock so the real worker cannot
                // race past Pending before the observation under test.
                object gate = Get(Get(test.Preferences, "biome"), "gate");
                lock (gate)
                {
                    Call(test.Preferences, "SetBiomeEnabled", false);
                    Check(test.Status("biome") == "Pending", "real toggle enters Pending");
                    Check(Capture(test.Pump) == "", "toggle Pending must be silent");
                }
                test.Saved("biome"); Check(Capture(test.Pump) == "", "toggle Saved must be silent");
                gate = Get(Get(test.Preferences, "ui"), "gate");
                lock (gate)
                {
                    test.Position(91, 63);
                    Check(test.Status("ui") == "Pending", "real position enters Pending");
                    Check(Capture(test.Pump) == "", "position Pending must be silent");
                }
                test.Saved("ui"); Check(Capture(test.Pump) == "", "position Saved must be silent");
                Call(test.Preferences, "SetBiomeEnabled", false); test.Position(91, 63);
                for (int i = 0; i < 100; i++) Check(Capture(test.Pump) == "", "same value and stable idle are silent");
                Check(File.Exists(test.BiomePath) && File.Exists(test.UiPath), "silent operation really saved both files");
            }
            using (var test = new Session(path))
            {
                Check(!(bool)Get(test.Preferences, "BiomeEnabled") && (int)Get(Get(test.Preferences, "Position"), "X") == 91,
                    "new real owners restore saved choices");
                Check(test.Status("biome") == "Saved" && test.Status("ui") == "Saved" && Capture(test.Pump) == "",
                    "legal restore is silent");
            }
        }

        private static void IndependentErrors()
        {
            string path = Path.Combine(root, "invalid"); Prepare(path, "{bad-biome", "{bad-ui");
            using (var test = new Session(path))
            {
                Check(Capture(test.Pump).Contains("群系选择"), "first biome failure reaches Main.NewText");
                Check(Capture(test.Pump).Contains("F5 位置"), "second document failure is not swallowed");
                for (int i = 0; i < 30; i++)
                {
                    Call(test.Preferences, "SetBiomeEnabled", i % 2 == 0); test.Position(100 + i, 80);
                    Main.gameMenu = true; test.Pump(); Main.gameMenu = false;
                    Call(test.Shell, "CloseAndSubmitPosition");
                    Check(Capture(test.Pump) == "", "same persistent errors must ignore revision/world/window changes");
                }
                Check(File.ReadAllText(test.BiomePath) == "{bad-biome" && File.ReadAllText(test.UiPath) == "{bad-ui",
                    "notification policy cannot overwrite protected originals");
                // Production write protection is permanent for this owner. This
                // immutable result projection tests only the feedback policy for
                // a different cause, not an invented disk recovery transition.
                test.ProjectStatus("biome", "IoFailure");
                Check(Capture(test.Pump).Contains("访问权限"), "a new actionable cause is still visible");
                Check(Capture(test.Pump) == "", "new cause is also deduplicated");
            }
        }

        private static void Deferred()
        {
            string path = Path.Combine(root, "deferred"); Prepare(path, "{bad", null);
            using (var test = new Session(path))
            {
                Main.gameMenu = true; Check(Capture(test.Pump) == "", "menu must defer notification");
                Main.gameMenu = false; Main.hideUI = true;
                Check(Capture(test.Pump) == "", "hidden UI must defer notification"); Main.hideUI = false;
                // The fixture's actual Main.NewText writes to Console. A failed
                // sink must not acknowledge a message that never reached it.
                TextWriter output = Console.Out;
                try { Console.SetOut(new FailingWriter()); test.Pump(); }
                finally { Console.SetOut(output); }
                Check(Capture(test.Pump).Contains("群系选择"), "failed presentation must leave the error pending");
                Check(Capture(test.Pump) == "", "successfully delivered error is consumed once");
            }
        }

        private static void Conflict()
        {
            using (var test = new Session(Path.Combine(root, "conflict")))
            {
                Check(Capture(test.Pump) == "", "normal startup silent before new error");
                File.WriteAllText(test.BiomePath, BiomeJson(1, ""), new UTF8Encoding(false));
                Call(test.Preferences, "SetBiomeEnabled", false);
                Wait(() => test.Status("biome") == "Conflict", "real first-create conflict");
                Check(Capture(test.Pump).Contains("停止覆盖"), "real conflict still notifies");
                Call(test.Preferences, "SetBiomeEnabled", true);
                Check(Capture(test.Pump) == "", "conflict does not repeat after another choice");
                Check(File.ReadAllText(test.BiomePath) == BiomeJson(1, ""), "external bytes preserved");
            }
        }

        private static void OtherErrors()
        {
            foreach (string reason in new[] { "UnsupportedVersion", "UnknownFields", "IoFailure" })
            {
                string path = Path.Combine(root, reason);
                Prepare(path, reason == "UnsupportedVersion" ? BiomeJson(2, "") :
                    reason == "UnknownFields" ? BiomeJson(1, ",\"extra\":1") : null, null);
                if (reason == "IoFailure") Directory.CreateDirectory(Path.Combine(path, "JueMingRData", "config", "features", "biome-display.json"));
                using (var test = new Session(path))
                {
                    Check(test.Status("biome") == reason, "real storage/codec failure classification " + reason);
                    Check(Capture(test.Pump).Contains("群系选择"), "necessary error retained " + reason);
                    Call(test.Preferences, "SetBiomeEnabled", false);
                    Check(Capture(test.Pump) == "", "persistent error deduplicated " + reason);
                }
            }
            string busy = Path.Combine(root, "busy");
            using (var owner = new Session(busy))
            using (var other = new Session(busy))
            {
                Check(other.Status("biome") == "Busy" && other.Status("ui") == "Busy", "real second writer rejected");
                Check(Capture(other.Pump).Contains("另一进程") && Capture(other.Pump).Contains("F5 位置"), "both busy documents notify");
                Check(Capture(other.Pump) == "", "busy does not flood");
            }
        }

        private sealed class Session : IDisposable
        {
            internal readonly object Preferences, Shell;
            private readonly object notes;
            internal readonly string BiomePath, UiPath;
            internal Session(string path)
            {
                // Caller supplies an isolated root before Host construction.
                Preferences = Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Settings.HostPreferences", true), Instance,
                    null, new object[] { path }, null);
                Wait(() => (bool)Get(Preferences, "IsLoaded"), "real Host load completes");
                // The shell now composes Notes as well as Settings. Keep its worker
                // on this same isolated installation and join it before cleanup.
                notes = Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.Notes.HostNotes", true), Instance,
                    null, new object[] { path }, null);
                Shell = Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.F5.F5Shell", true), Instance,
                    null, new[] { runtime, Preferences, notes }, null);
                BiomePath = Path.Combine(path, "JueMingRData", "config", "features", "biome-display.json");
                UiPath = Path.Combine(path, "JueMingRData", "config", "ui.json");
            }
            internal void Pump() { Call(Shell, "AfterUpdate"); }
            internal string Status(string document) { return Get(Get(Get(Preferences, document), "Snapshot"), "Status").ToString(); }
            internal void Saved(string document) { Wait(() => Status(document) == "Saved", "real save completes " + document); }
            internal void Position(int x, int y)
            {
                Type type = Preferences.GetType().GetMethod("SetPosition", Instance).GetParameters()[0].ParameterType;
                Call(Preferences, "SetPosition", Activator.CreateInstance(type, new object[] { x, y }));
            }
            internal void ProjectStatus(string document, string status)
            {
                object owner = Get(Preferences, document);
                Check((bool)Call(owner, "Stop", 3000), "worker stopped before policy-only projection");
                object snapshot = Get(owner, "Snapshot");
                object projected = Activator.CreateInstance(snapshot.GetType(), Instance, null, new[] {
                    Get(snapshot, "Value"), true, Get(snapshot, "Revision"), Enum.Parse(Get(snapshot, "Status").GetType(), status) }, null);
                owner.GetType().GetField("snapshot", Instance).SetValue(owner, projected);
            }
            public void Dispose()
            {
                Main.gameMenu = false; Main.hideUI = false;
                foreach (string document in new[] { "biome", "ui" })
                    Check((bool)Call(Get(Preferences, document), "Stop", 3000), "worker joined before fixture directory cleanup");
                Call(Preferences, "OnProcessExit", null, EventArgs.Empty);
                // Notes also supports a final-update overload. Bind the existing
                // bounded stop contract, not an ambiguous method-name lookup.
                object notesWorker = Get(notes, "worker");
                MethodInfo stop = notesWorker.GetType().GetMethod("Stop", Instance, null, new[] { typeof(int) }, null);
                Check((bool)stop.Invoke(notesWorker, new object[] { 3000 }), "notes worker joined before fixture directory cleanup");
                Call(notes, "OnExit", null, EventArgs.Empty);
            }
        }

        private sealed class FailingWriter : StringWriter
        { public override void WriteLine(string value) { throw new InvalidOperationException("test sink unavailable"); } }
        private static string Capture(Action action)
        {
            TextWriter original = Console.Out;
            using (var output = new StringWriter())
            { try { Console.SetOut(output); action(); return output.ToString(); } finally { Console.SetOut(original); } }
        }
        private static string BiomeJson(int version, string extra)
        { return "{\"format\":\"JueMingR.BiomeDisplay\",\"version\":" + version + ",\"enabled\":true" + extra + "}"; }
        private static void Prepare(string path, string biome, string ui)
        {
            string config = Path.Combine(path, "JueMingRData", "config"); Directory.CreateDirectory(Path.Combine(config, "features"));
            if (biome != null) File.WriteAllText(Path.Combine(config, "features", "biome-display.json"), biome, new UTF8Encoding(false));
            if (ui != null) File.WriteAllText(Path.Combine(config, "ui.json"), ui, new UTF8Encoding(false));
        }
        private static object Get(object value, string name)
        {
            FieldInfo field = value.GetType().GetField(name, Instance);
            return field != null ? field.GetValue(value) : value.GetType().GetProperty(name, Instance).GetValue(value);
        }
        private static object Call(object value, string name, params object[] arguments)
        { return value.GetType().GetMethod(name, Instance).Invoke(value, arguments); }
        private static void Wait(Func<bool> ready, string message)
        {
            var elapsed = Stopwatch.StartNew(); while (!ready() && elapsed.ElapsedMilliseconds < 5000) Thread.Sleep(5);
            Check(ready(), message);
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Run(List<string> failures, string name, Action test)
        { try { test(); } catch (Exception error) { failures.Add(name + ": " + error.Message); } }
    }
}
