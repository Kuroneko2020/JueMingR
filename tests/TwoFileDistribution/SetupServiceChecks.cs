using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using JueMingR.Setup;

internal static class SetupServiceChecks
{
    private static int count;
    private static string root, terraria;
    private static SetupPayload payload;
    private static readonly byte[] Foreign = Encoding.UTF8.GetBytes("<configuration><runtime><appDomainManagerAssembly value=\"OtherTool\"/><appDomainManagerType value=\"OtherTool.Manager\"/></runtime><appSettings><add key=\"x\" value=\"keep\"/></appSettings></configuration>");
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--wait") { Console.WriteLine("ready"); Console.ReadLine(); return 0; }
            if (args.Length == 3 && args[0] == "--extract")
            {
                Assembly source = Assembly.ReflectionOnlyLoad(File.ReadAllBytes(args[1]));
                foreach (string name in new[] { "JueMingR.dll", "Terraria.exe.config" }) File.WriteAllBytes(Path.Combine(args[2], name), SetupPayload.Resource(source, "JueMingR.Setup.Payload." + name));
                return 0;
            }
            root = Path.GetFullPath(args[0]); terraria = args[1];
            byte[] dll = File.ReadAllBytes(args[2]), config = File.ReadAllBytes(args[3]);
            payload = new SetupPayload { Dll = dll, Config = config, DllHash = SetupPayload.Hash(dll), ConfigHash = SetupPayload.Hash(config), Version = "isolated-service-test" };
            Directory.CreateDirectory(root);
            string target = Fresh("中文 新装");
            byte[] data = Encoding.UTF8.GetBytes("笔记、蓝图和未来格式原样保留"), outsider = Encoding.UTF8.GetBytes("third-party-harmony");
            Directory.CreateDirectory(Path.Combine(target, "JueMingRData", "blueprints"));
            File.WriteAllBytes(Path.Combine(target, "JueMingRData", "blueprints", "future.bin"), data);
            File.WriteAllBytes(Path.Combine(target, "0Harmony.dll"), outsider);
            var installed = Service().Execute("install", target); Check(installed.exitCode == 0 && Pair(target), "new-install");
            Check(Directory.GetFiles(target, "*.bak").Length == 0 && Directory.GetFiles(target, ".juemingr-new-*").Length == 0, "no-backup-or-temp");
            Check(File.ReadAllBytes(Path.Combine(target, "JueMingRData", "blueprints", "future.bin")).SequenceEqual(data) && File.ReadAllBytes(Path.Combine(target, "0Harmony.dll")).SequenceEqual(outsider), "data-and-foreign-preserved");
            Check(Service().Execute("install", target).exitCode == 0 && Pair(target), "same-version-reinstall");
            File.WriteAllBytes(Path.Combine(target, "Terraria.exe.config"), Foreign);
            Check(Service().Execute("install", target).exitCode == 0 && Pair(target), "arbitrary-third-party-config-overwrite");
            File.WriteAllText(Path.Combine(target, "JueMingR.dll"), "old-or-mismatched-version");
            Check(Service().Execute("install", target).exitCode == 0 && Pair(target), "update-and-mismatch-repair");

            string running = Fresh("running-test-owned-worker");
            string helper = Path.Combine(running, "JueMingR.PredictionWorker.exe"); File.Copy(Assembly.GetExecutingAssembly().Location, helper);
            using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(helper, "--wait") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true }))
            {
                if (process.StandardOutput.ReadLine() != "ready") throw new Exception("Test-owned helper did not start.");
                try { Check(Service().Execute("install", running).exitCode == 2 && !File.Exists(Path.Combine(running, "JueMingR.dll")), "running-worker-target-refused"); }
                finally { process.StandardInput.WriteLine("exit"); if (!process.WaitForExit(5000)) throw new Exception("Test-owned helper did not exit."); }
            }

            string locked = Fresh("locked"); File.WriteAllBytes(Path.Combine(locked, "Terraria.exe.config"), Foreign);
            using (new FileStream(Path.Combine(locked, "Terraria.exe.config"), FileMode.Open, FileAccess.Read, FileShare.Read))
                Check(Service().Execute("install", locked).exitCode == 2 && !File.Exists(Path.Combine(locked, "JueMingR.dll")) && File.ReadAllBytes(Path.Combine(locked, "Terraria.exe.config")).SequenceEqual(Foreign), "locked-before-consume");
            string partial = Fresh("partial"); File.WriteAllBytes(Path.Combine(partial, "Terraria.exe.config"), Foreign);
            var failed = new InstallService(() => payload, step => { if (step == "replace-config") throw new IOException("controlled commit boundary"); }).Execute("install", partial);
            Check(failed.exitCode == 3 && failed.status == "partial" && failed.failedStep == "replace-config" && failed.completed.SequenceEqual(new[] { "replace-dll" }) && File.ReadAllBytes(Path.Combine(partial, "Terraria.exe.config")).SequenceEqual(Foreign), "real-commit-partial-failure");
            Check(Service().Execute("install", partial).exitCode == 0 && Pair(partial), "partial-reinstall-repair");
            Check(Directory.GetFiles(partial, ".juemingr-new-*").Length == 0 && Directory.GetFiles(partial).All(f => !f.EndsWith(".bak") && !f.EndsWith(".old")), "partial-no-hidden-backup");

            File.WriteAllBytes(Path.Combine(target, "Terraria.exe.config"), Foreign);
            var removed = Service().Execute("uninstall", target);
            Check(removed.exitCode == 0 && File.ReadAllBytes(Path.Combine(target, "Terraria.exe.config")).SequenceEqual(Foreign) && !File.Exists(Path.Combine(target, "JueMingR.dll")), "uninstall-later-tool-config-preserved");
            Check(File.ReadAllBytes(Path.Combine(target, "JueMingRData", "blueprints", "future.bin")).SequenceEqual(data), "uninstall-data-preserved");
            Check(Service().Execute("uninstall", target).status == "noop", "repeat-uninstall");

            string manual = Fresh("manual"); File.WriteAllBytes(Path.Combine(manual, "JueMingR.dll"), dll); File.WriteAllBytes(Path.Combine(manual, "Terraria.exe.config"), config);
            var manualRemoved = Service().Execute("uninstall", manual);
            Check(manualRemoved.exitCode == 0 && !File.Exists(Path.Combine(manual, "JueMingR.dll")) && !File.Exists(Path.Combine(manual, "Terraria.exe.config")), "manual-no-receipt-managed");
            string expanded = Fresh("expanded"); File.WriteAllBytes(Path.Combine(expanded, "JueMingR.dll"), dll); File.WriteAllBytes(Path.Combine(expanded, "Terraria.exe.config"), config);
            var product = Assembly.ReflectionOnlyLoad(dll);
            byte[] index = SetupPayload.Resource(product, "JueMingR.Bundle.index"); string runtime = Path.Combine(expanded, "JueMingR.Runtime", SetupPayload.Hash(index).ToLowerInvariant()); Directory.CreateDirectory(runtime);
            using (var archive = new System.IO.Compression.ZipArchive(new MemoryStream(SetupPayload.Resource(product, "JueMingR.Bundle.payload.zip")), System.IO.Compression.ZipArchiveMode.Read))
                foreach (var entry in archive.Entries) using (var input = entry.Open()) using (var output = File.Create(Path.Combine(runtime, entry.FullName))) input.CopyTo(output);
            File.WriteAllText(Path.Combine(runtime, "unknown.bin"), "keep");
            Check(Service().Execute("uninstall", expanded).exitCode == 0 && Directory.GetFiles(runtime).Length == 1 && File.Exists(Path.Combine(runtime, "unknown.bin")), "runtime-exact-members-clean-unknown-preserved");
            string edited = Fresh("edited-config"); File.WriteAllBytes(Path.Combine(edited, "JueMingR.dll"), dll);
            string editedConfig = Encoding.UTF8.GetString(config).Replace("</configuration>", "<appSettings><add key=\"external\" value=\"keep\"/></appSettings></configuration>");
            File.WriteAllText(Path.Combine(edited, "Terraria.exe.config"), editedConfig, new UTF8Encoding(false));
            var editedRemoved = Service().Execute("uninstall", edited);
            Check(editedRemoved.exitCode == 0 && File.ReadAllText(Path.Combine(edited, "Terraria.exe.config")).Contains("external") && !File.ReadAllText(Path.Combine(edited, "Terraria.exe.config")).Contains("appDomainManagerType"), "modified-shared-config-keeps-external-settings");

            string updated = Fresh("game-updated"); File.WriteAllBytes(Path.Combine(updated, "JueMingR.dll"), dll); File.WriteAllBytes(Path.Combine(updated, "Terraria.exe.config"), config);
            using (var append = new FileStream(Path.Combine(updated, "Terraria.exe"), FileMode.Append)) append.WriteByte(0);
            Check(Service().Execute("install", updated).exitCode == 2 && Service().Execute("uninstall", updated).exitCode == 0 && !File.Exists(Path.Combine(updated, "JueMingR.dll")), "updated-game-can-uninstall-not-install");

            string legacy = Fresh("legacy"); WriteLegacy(legacy);
            var migrated = Service().Execute("install", legacy);
            Check(migrated.exitCode == 0 && Pair(legacy) && !File.Exists(Path.Combine(legacy, "JueMingR.Bootstrap.dll")) && !File.Exists(Path.Combine(legacy, "JueMingR.Validation", "JueMingR.Features.dll")) && File.Exists(Path.Combine(legacy, "JueMingR.Validation", "unknown.txt")) && migrated.retained.Count != 0, "legacy-known-clean-unknown-preserved");
            string unknown = Fresh("unknown-legacy"); Directory.CreateDirectory(Path.Combine(unknown, "JueMingR.Validation")); File.WriteAllText(Path.Combine(unknown, "JueMingR.Validation", "unknown.txt"), "keep");
            Check(Service().Execute("install", unknown).exitCode == 0 && File.Exists(Path.Combine(unknown, "JueMingR.Validation", "unknown.txt")), "no-receipt-migration-no-fallback");
            string collision = Fresh("ordinary-file-check"); Directory.CreateDirectory(Path.Combine(collision, "JueMingR.dll"));
            Check(Service().Execute("install", collision).exitCode == 2 && !File.Exists(Path.Combine(collision, "Terraria.exe.config")), "nonordinary-target-rejected");
            Console.WriteLine(new JavaScriptSerializer().Serialize(new { status = "PASS", checks = count, outputRoot = root, scope = "isolated production service filesystem semantics; no game process started" })); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static InstallService Service() { return new InstallService(() => payload, null); }
    private static string Fresh(string name) { string path = Path.Combine(root, name); if (Directory.Exists(path)) throw new IOException("Test target already exists: " + path); Directory.CreateDirectory(path); File.Copy(terraria, Path.Combine(path, "Terraria.exe")); return path; }
    private static bool Pair(string target) { return SetupPayload.FileHash(Path.Combine(target, "JueMingR.dll")) == payload.DllHash && SetupPayload.FileHash(Path.Combine(target, "Terraria.exe.config")) == payload.ConfigHash; }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL " + name); count++; Console.WriteLine("PASS " + name); }
    private static void WriteLegacy(string target)
    {
        string[] names = { "JueMingR.Bootstrap.dll", "JueMingR.Validation/0Harmony.dll", "JueMingR.Validation/JueMingR.Features.dll", "JueMingR.Validation/JueMingR.Infrastructure.dll", "JueMingR.Validation/JueMingR.Platform.dll", "JueMingR.Validation/JueMingR.PredictionWorker.exe", "JueMingR.Validation/JueMingR.PredictionWorker.exe.config", "JueMingR.Validation/JueMingR.TerrariaHost.dll", "JueMingR.Validation/phase-0-s-runtime.manifest", "Terraria.exe.config" };
        var entries = new List<object>();
        foreach (string name in names) { string path = Path.Combine(target, name); Directory.CreateDirectory(Path.GetDirectoryName(path)); byte[] data = Encoding.UTF8.GetBytes("old-owned:" + name); File.WriteAllBytes(path, data); entries.Add(new { installRelativePath = name, length = data.Length, sha256 = SetupPayload.Hash(data) }); }
        string source = new string('a', 40);
        var manifest = new { schemaVersion = 1, packageId = "continuous-processing-" + source, sourceCommit = source, target = new { simpleName = "Terraria", version = "1.4.5.8", mvid = "2c29f6c3-4bd9-4add-9c58-da159804e083", sha256 = TargetSafety.TerrariaHash }, payload = entries };
        File.WriteAllText(Path.Combine(target, "JueMingR.Validation", "phase-0-s-install-manifest.json"), new JavaScriptSerializer().Serialize(manifest));
        File.WriteAllText(Path.Combine(target, "JueMingR.Validation", "unknown.txt"), "leave unknown");
    }
}
