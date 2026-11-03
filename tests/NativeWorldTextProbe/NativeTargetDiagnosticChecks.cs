using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Collector mechanics only, with an owned export path. Never arm a game
    // installation or execute native Draw/PrepareMethod to test observation.
    internal static class NativeTargetDiagnosticChecks
    {
        private const BindingFlags F = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static void Run(string output, bool allowCandidate = false)
        {
            // Formal workload evidence always describes its own prepared Host.
            // Explicit diagnostic candidates belong to a separate specialist
            // process whose raw record binds that candidate's actual bytes.
            string candidate = allowCandidate ? Environment.GetEnvironmentVariable("JMR_TARGET_DIAGNOSTIC_CANDIDATE") :
                Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll");
            Require(!string.IsNullOrEmpty(candidate), "candidate specialist requires an explicit Host path");
            candidate = Path.GetFullPath(candidate);
            var assembly = Assembly.LoadFrom(candidate);
            Require(string.Equals(Path.GetFullPath(assembly.Location), candidate, StringComparison.OrdinalIgnoreCase),
                "actual loaded Host is the scope-selected candidate, without assembly binding substitution");
            using (var stream = File.OpenRead(assembly.Location))
            using (var sha = System.Security.Cryptography.SHA256.Create())
                Console.WriteLine("TARGET-DIAGNOSTIC Host=" + assembly.Location + " MVID=" + assembly.ManifestModule.ModuleVersionId +
                    " SHA256=" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") + " allowCandidate=" + allowCandidate);
            var trace = assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.TargetGestureTrace");
            bool diagnostic = allowCandidate && Environment.GetEnvironmentVariable("JMR_TARGET_DIAGNOSTIC_EXPECTED") == "1";
            Require((trace != null) == diagnostic, "ordinary Host excludes the diagnostic type; diagnostic Host includes it");
            if (!diagnostic) { Console.WriteLine("PASS ordinary Host has no TargetGestureTrace type."); return; }
            output = Path.Combine(Path.GetFullPath(output), "collector-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(output);
            Action<string, object> set = (name, value) => trace.GetField(name, F).SetValue(null, value);
            Func<string, object> get = name => trace.GetField(name, F).GetValue(null);
            Action<string, object[]> call = (name, args) => trace.GetMethod(name, F).Invoke(null, args);
            Action<string> reset = file =>
            {
                ((Timer)get("timer"))?.Dispose();
                set("count", 0); set("request", 0); set("kind", 0); set("pass", 0L);
                set("active", true); set("exported", false); set("requestActive", false);
                set("output", file); set("identity", "owned collector fixture\n");
                set("deadline", Stopwatch.GetTimestamp() + 600L * Stopwatch.Frequency);
                set("timer", new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite));
            };
            ulong world = Main.GameUpdateCount; int x = Main.mouseX, y = Main.mouseY;
            string path = Path.Combine(output, "bounded.log");
            try
            {
                reset(path);
                call("Resource", new object[] { true, 1 });
                Require((int)get("count") == 0, "resource events outside a target request are discarded");
                call("Request", new object[] { 2, 41L, 17L, 45L });
                call("Record", new object[] { 3, 42L, 18L, 1, 4, 0 });
                for (int i = 0; i < 600; i++) call("Resource", new object[] { true, 1 });
                Require(!(bool)get("active") && (int)get("count") == 512 && File.Exists(path), "512 fixed rows stop and export once");
                string first = File.ReadAllText(path);
                call("Stop", new object[] { "second-stop" }); call("Request", new object[] { 1, 99L, 99L, 103L });
                Require(File.ReadAllText(path) == first && (int)get("request") == 1, "stopped recorder cannot append, rearm or export twice");
                reset(Path.Combine(output, "requests.log"));
                for (int i = 0; i < 4; i++)
                { call("Request", new object[] { 1, (long)i, (long)i, (long)i + 4 }); call("Record", new object[] { 11, (long)i, (long)i, 0, 0, 0 }); }
                Require(!(bool)get("active") && (int)get("request") == 4 && (int)get("count") == 8, "four terminal requests stop without waiting for a fifth action");
                reset(Path.Combine(output, "deadline.log")); set("deadline", 0L);
                call("Record", new object[] { 1, 0L, 0L, 0, 0, 0 });
                Require(!(bool)get("active") && (int)get("count") == 0, "expired monotonic deadline stops before collecting another row");
                // Keep the reset's timer dormant: callback scheduling cannot
                // serve as the deadline oracle at the Request entry itself.
                reset(Path.Combine(output, "expired-request.log"));
                set("deadline", Stopwatch.GetTimestamp() - 1);
                string identityBeforeRequest = (string)get("identity");
                call("Request", new object[] { 2, 51L, 21L, 55L });
                Require(!(bool)get("active") && (int)get("request") == 0 && (int)get("count") == 0 &&
                    !(bool)get("requestActive") && (string)get("identity") == identityBeforeRequest,
                    "expired Request cannot revive recording, append owners or add a request row while timer delivery is delayed");
                reset(Path.Combine(output, "late-first-request.log"));
                long installationDeadline = Stopwatch.GetTimestamp() + 60L * Stopwatch.Frequency;
                set("deadline", installationDeadline);
                call("Request", new object[] { 1, 61L, 31L, 65L });
                Require((bool)get("active") && (int)get("request") == 1 && (int)get("count") == 1 &&
                    (long)get("deadline") == installationDeadline,
                    "first request with sixty seconds remaining preserves the earlier absolute installation deadline");
                // Opening a directory as a file fails on every supported OS.
                // Failure stays inside the observer and never escapes a hook.
                reset(output); call("Stop", new object[] { "forced-export-failure" });
                Require(!(bool)get("active"), "export failure disables only the observer");
                call("Resource", new object[] { false, 1 });
                Require(Main.GameUpdateCount == world && Main.mouseX == x && Main.mouseY == y, "observer calls neither advance native time nor move the target pointer");
                Console.WriteLine("PASS diagnostic collector: inactive-request discard, fixed capacity, terminal-request/deadline bounds, one export, failure isolation; no business proof or ordinary-load claim.");
            }
            finally { call("Fail", Array.Empty<object>()); }
        }
    }
}
