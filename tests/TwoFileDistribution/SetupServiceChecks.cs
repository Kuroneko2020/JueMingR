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
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--form-contract") { SetupFormChecks.Run(); return 0; }
            if (args.Length == 1 && args[0] == "--wait") { Console.WriteLine("ready"); Console.ReadLine(); return 0; }
            if (args.Length == 2 && args[0] == "--path-snapshot") { Console.WriteLine(PathSnapshot(args[1])); return 0; }
            if (args.Length == 2 && args[0] == "--path-contract")
            {
                root = Path.GetFullPath(args[1]); Directory.CreateDirectory(root); PathChecks();
                Console.WriteLine(new JavaScriptSerializer().Serialize(new { status = "PASS", checks = count, scope = "production pure Windows path contracts; no root/share filesystem access" })); return 0;
            }
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
            if (args.Length == 5 && args[4] == "--presentation") { PresentationChecks(); Console.WriteLine(new JavaScriptSerializer().Serialize(new { status = "PASS", checks = count, scope = "isolated production pair reader and result projection; no game started" })); return 0; }
            if (args.Length == 5 && args[4] == "--ui-increment") { IncrementChecks(); Console.WriteLine(new JavaScriptSerializer().Serialize(new { status = "PASS", checks = count, scope = "Steam discovery and exact preference reset only" })); return 0; }
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
            string helper = Path.Combine(running, "JueMingR.Runtime", "test-payload", "Worker", "JueMingR.PredictionWorker.exe"); Directory.CreateDirectory(Path.GetDirectoryName(helper)); File.Copy(Assembly.GetExecutingAssembly().Location, helper);
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
            string legacyReceipt = Path.Combine(legacy, "JueMingR.Validation", "phase-0-s-install-manifest.json");
            byte[] originalLegacyReceipt = File.ReadAllBytes(legacyReceipt);
            var migrated = Service().Execute("install", legacy);
            Check(migrated.exitCode == 0 && Pair(legacy) && !File.Exists(Path.Combine(legacy, "JueMingR.Bootstrap.dll")) && !File.Exists(Path.Combine(legacy, "JueMingR.Validation", "JueMingR.Features.dll")) && File.Exists(Path.Combine(legacy, "JueMingR.Validation", "unknown.txt")) && migrated.retained.Count != 0, "legacy-known-clean-unknown-preserved");
            Check(File.ReadAllBytes(legacyReceipt).SequenceEqual(originalLegacyReceipt), "legacy-receipt-original-bytes-preserved-after-migration");
            Check(Service().Execute("uninstall", legacy).exitCode == 0 && File.ReadAllBytes(legacyReceipt).SequenceEqual(originalLegacyReceipt), "legacy-receipt-original-bytes-preserved-after-uninstall");
            string unknown = Fresh("unknown-legacy"); Directory.CreateDirectory(Path.Combine(unknown, "JueMingR.Validation")); File.WriteAllText(Path.Combine(unknown, "JueMingR.Validation", "unknown.txt"), "keep");
            Check(Service().Execute("install", unknown).exitCode == 0 && File.Exists(Path.Combine(unknown, "JueMingR.Validation", "unknown.txt")), "no-receipt-migration-no-fallback");
            string collision = Fresh("ordinary-file-check"); Directory.CreateDirectory(Path.Combine(collision, "JueMingR.dll"));
            Check(Service().Execute("install", collision).exitCode == 2 && !File.Exists(Path.Combine(collision, "Terraria.exe.config")), "nonordinary-target-rejected");
            Console.WriteLine(new JavaScriptSerializer().Serialize(new { status = "PASS", checks = count, outputRoot = root, scope = "isolated production service filesystem semantics; no game process started" })); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static InstallService Service() { return new InstallService(() => payload, null); }
    private static void PresentationChecks()
    {
        string empty = Fresh("pair-empty");
        Check(SetupTargetState.Read(empty).Pair == SetupPairState.NotInstalled, "state-no-pair");
        File.WriteAllBytes(Path.Combine(empty, "Terraria.exe.config"), Foreign);
        Check(SetupTargetState.Read(empty).Pair == SetupPairState.NotInstalled, "state-foreign-config-no-r");
        string manual = Fresh("pair-manual"); File.WriteAllBytes(Path.Combine(manual, "JueMingR.dll"), payload.Dll); File.WriteAllBytes(Path.Combine(manual, "Terraria.exe.config"), payload.Config);
        var state = SetupTargetState.Read(manual);
        Check(state.Pair == SetupPairState.Installed && state.CanMaintain && state.CanInstall && !File.Exists(Path.Combine(manual, "JueMingR.install.json")), "state-manual-pair-without-receipt-or-runtime");
        File.Delete(Path.Combine(manual, "Terraria.exe.config"));
        Check(SetupTargetState.Read(manual).Pair == SetupPairState.Incomplete, "state-only-dll");
        File.WriteAllBytes(Path.Combine(manual, "Terraria.exe.config"), Foreign);
        Check(SetupTargetState.Read(manual).Pair == SetupPairState.Incomplete, "state-known-r-dll-foreign-entry");
        File.WriteAllText(Path.Combine(manual, "Terraria.exe.config"), Encoding.UTF8.GetString(payload.Config).Replace("</configuration>", "<appSettings><add key=\"external\" value=\"keep\"/></appSettings></configuration>"));
        Check(SetupTargetState.Read(manual).Pair == SetupPairState.Unknown, "state-shared-extra-settings-not-called-damaged");
        File.WriteAllBytes(Path.Combine(manual, "Terraria.exe.config"), payload.Config); File.Delete(Path.Combine(manual, "JueMingR.dll"));
        Check(SetupTargetState.Read(manual).Pair == SetupPairState.Incomplete, "state-only-r-config");
        File.WriteAllText(Path.Combine(manual, "JueMingR.dll"), "unrecognized");
        Check(SetupTargetState.Read(manual).Pair == SetupPairState.Unknown, "state-unrecognized-dll-not-absence");
        File.WriteAllBytes(Path.Combine(manual, "JueMingR.dll"), payload.Dll);
        using (new FileStream(Path.Combine(manual, "Terraria.exe.config"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Check(SetupTargetState.Read(manual).Pair == SetupPairState.Unknown && SetupTargetState.Read(manual).CanMaintain, "state-unreadable-config-still-maintainable");
        using (var append = new FileStream(Path.Combine(manual, "Terraria.exe"), FileMode.Append)) append.WriteByte(0);
        state = SetupTargetState.Read(manual);
        Check(state.CanMaintain && !state.CanInstall && state.Pair == SetupPairState.Installed, "state-updated-game-maintenance-independent");
        var removed = Service().Execute("uninstall", manual);
        Check(removed.status == "success" && SetupTargetState.Read(manual).CanMaintain && SetupTargetState.Read(manual).Pair == SetupPairState.NotInstalled, "state-after-uninstall-clear-remains-available");
        string partial = Fresh("pair-partial"); File.WriteAllBytes(Path.Combine(partial, "Terraria.exe.config"), Foreign);
        var failed = new InstallService(() => payload, step => { if (step == "replace-config") throw new IOException("controlled-config-failure"); }).Execute("install", partial);
        Check(failed.status == "partial" && SetupTargetState.Read(partial).Pair == SetupPairState.Incomplete && SetupResultPresentation.Summary(failed).Contains("只完成了一部分") && SetupResultPresentation.Details(failed, partial).Contains("replace-dll") && SetupResultPresentation.Details(failed, partial).Contains("replace-config"), "state-partial-pair-and-real-result");
        string receipt = Fresh("pair-receipt-failure");
        var receiptFailed = new InstallService(() => payload, step => { if (step == "write-receipt") throw new IOException("controlled-receipt-failure"); }).Execute("install", receipt);
        Check(receiptFailed.status == "partial" && SetupTargetState.Read(receipt).Pair == SetupPairState.Installed && SetupResultPresentation.Summary(receiptFailed).Contains("只完成了一部分") && SetupResultPresentation.Details(receiptFailed, receipt).Contains("write-receipt"), "state-complete-pair-does-not-erase-partial-receipt-result");
        var result = new SetupResult { operation = "uninstall", status = "success", gameDirectory = receipt, message = "removed" }; result.retained.Add("keep-external"); result.completed.Add("future-step");
        Check(SetupResultPresentation.Summary(result).Contains("已保留") && SetupResultPresentation.Details(result, "other").Contains(receipt) && SetupResultPresentation.Details(result, "other").Contains("future-step") && SetupResultPresentation.Details(result, "other").Contains("keep-external"), "result-retained-target-and-unknown-steps-preserved");
        result.operation = "clear-config"; result.status = "noop";
        Check(SetupResultPresentation.Summary(result).Contains("无需处理"), "result-noop-neutral");
        result.status = "failure"; result.gameDirectory = null; result.message = "specific-failure";
        Check(SetupResultPresentation.Summary(result).Contains("specific-failure") && SetupResultPresentation.Details(result, "selected").Contains("目标核验未完成"), "result-failure-reason-and-unverified-target");
    }
    private static void PathChecks()
    {
        foreach (Func<string, string> normalize in new Func<string, string>[] { TargetSafety.NormalizeDirectory, JueMingR.Bootstrap.BundlePreparation.NormalizeDirectory })
        {
            Check(normalize(@"D:\") == @"D:\" && normalize("D:/") == @"D:\", "drive-root-remains-absolute");
            Check(normalize(@"D:\Games\Terraria\") == @"D:\Games\Terraria" && normalize(@"D:\Games\.\Terraria\") == @"D:\Games\Terraria", "ordinary-directory-normalization");
            Check(normalize(@"\\server\share") == normalize(@"\\server\share\") && Path.IsPathRooted(normalize(@"\\server\share")), "unc-root-equivalence");
            foreach (string leaf in new[] { "Terraria.exe", "JueMingR.dll", "Terraria.exe.config", @"JueMingRData\config" })
                Check(Path.Combine(normalize(@"D:\"), leaf) == @"D:\" + leaf, "root-combination-" + leaf);
        }
        Check(JueMingR.Bootstrap.BundlePreparation.DirectoryEquals(Path.GetDirectoryName(@"D:\Terraria.exe.config"), @"D:\"), "bootstrap-root-config-directory-equal");
        Check(!JueMingR.Bootstrap.BundlePreparation.DirectoryEquals(Path.GetDirectoryName(@"D:\child\Terraria.exe.config"), @"D:\"), "bootstrap-child-config-directory-rejected");
        Check(JueMingR.Bootstrap.BundlePreparation.DirectoryEquals(@"D:\Games\Terraria\", @"d:\games\terraria"), "bootstrap-ordinary-case-and-separator-equal");
        Check(!JueMingR.Bootstrap.BundlePreparation.DirectoryEquals(@"\\server\share2", @"\\server\share"), "bootstrap-unc-share-boundary");
        Check(TargetSafety.IsProcessInDirectory(@"D:\", @"D:\Terraria.exe") && TargetSafety.IsProcessInDirectory(@"D:\", @"D:\JueMingR.Runtime\id\Worker\JueMingR.PredictionWorker.exe"), "root-process-and-runtime-worker-owned");
        Check(!TargetSafety.IsProcessInDirectory(@"D:\", @"E:\Terraria.exe"), "different-drive-not-owned");
        Check(!TargetSafety.IsProcessInDirectory(@"D:\Games\Terraria", @"D:\Games\Terraria2\Terraria.exe"), "adjacent-game-directory-not-owned");
        Check(!TargetSafety.IsProcessInDirectory(@"\\server\share", @"\\server\share2\Terraria.exe") && TargetSafety.IsProcessInDirectory(@"\\server\share\", @"\\server\share\runtime\Worker.exe"), "unc-process-share-boundary");
        string first = Path.Combine(root, "cwd-A"), second = Path.Combine(root, "cwd-B"); Directory.CreateDirectory(first); Directory.CreateDirectory(second);
        string volume = Path.GetPathRoot(root);
        Check(ChildSnapshot(first, volume) == ChildSnapshot(second, volume) && ChildSnapshot(first, volume) == PathSnapshot(volume), "absolute-root-independent-of-two-child-working-directories");
    }
    private static string PathSnapshot(string input)
    {
        return string.Join("|", new[] { TargetSafety.NormalizeDirectory(input), JueMingR.Bootstrap.BundlePreparation.NormalizeDirectory(input),
            Path.GetFullPath(Path.Combine(TargetSafety.NormalizeDirectory(input), "Terraria.exe")),
            Path.GetFullPath(Path.Combine(JueMingR.Bootstrap.BundlePreparation.NormalizeDirectory(input), "Terraria.exe.config")) });
    }
    private static string ChildSnapshot(string cwd, string volume)
    {
        // The test workspace's local volume root contains no spaces or quotes. No mapping
        // or root files are created; only the child process's owned CWD differs.
        using (var child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--path-snapshot " + volume)
            { WorkingDirectory = cwd, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
        {
            string output = child.StandardOutput.ReadToEnd().Trim(); child.WaitForExit(); if (child.ExitCode != 0) throw new Exception("Path child failed."); return output;
        }
    }
    private static void IncrementChecks()
    {
        string target = Fresh("configuration-reset");
        string configRoot = Path.Combine(target, "JueMingRData", "config");
        foreach (string doc in ConfigurationReset.Documents)
        foreach (string suffix in new[] { "", ".bak", ".tmp", ".lock" }) Write(Path.Combine(configRoot, doc) + suffix, "preference");
        foreach (string doc in new[] { "guidance", "information-display", "item-automation", "recovery-potions" }) Write(Path.Combine(configRoot, "features", doc + ".json.schema1-original"), "source");
        string[] retained = { "notes/notes.json", "notes/notes.json.bak", "blueprints/exchange-state.json", "blueprints/instances/a/scene.bin.schema1-original", "behavior/onboarding/a.json", "config/future.json", "config/features/future.json", "config/features/combat.json.unknown", "records/a.bin" };
        foreach (string path in retained) Write(Path.Combine(target, "JueMingRData", path), "keep-" + path);
        Write(Path.Combine(target, "Terraria.exe.config"), "keep-game-config");
        var reset = Service().Execute("clear-config", target);
        Check(reset.exitCode == 0 && reset.status == "success" && reset.completed.Count == ConfigurationReset.Documents.Length * 4 + 4, "all-real-preferences-and-recovery-material-cleared");
        Check(retained.All(path => File.ReadAllText(Path.Combine(target, "JueMingRData", path)) == "keep-" + path) && File.ReadAllText(Path.Combine(target, "Terraria.exe.config")) == "keep-game-config", "notes-blueprints-onboarding-unknown-and-game-config-preserved");
        Check(Service().Execute("clear-config", target).status == "noop", "repeated-clear-is-noop");
        string blocked = Fresh("locked-preferences");
        string first = Path.Combine(blocked, "JueMingRData/config/ui.json"), locked = Path.Combine(blocked, "JueMingRData/config/hotkeys.json.lock");
        Write(first, "original"); Write(locked, "lease");
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Check(Service().Execute("clear-config", blocked).status == "failure" && File.ReadAllText(first) == "original", "occupied-lease-rejected-before-any-deletion");
        string odd = Fresh("non-file-recovery"); Write(Path.Combine(odd, "JueMingRData/config/ui.json"), "keep"); Directory.CreateDirectory(Path.Combine(odd, "JueMingRData/config/hotkeys.json.bak"));
        Check(Service().Execute("clear-config", odd).exitCode == 2 && File.Exists(Path.Combine(odd, "JueMingRData/config/ui.json")), "nonordinary-recovery-rejected-before-any-deletion");
        string partial = Fresh("partial-reset"); Write(Path.Combine(partial, "JueMingRData/config/ui.json"), "first"); Write(Path.Combine(partial, "JueMingRData/config/hotkeys.json"), "second");
        int deletions = 0;
        var interrupted = new InstallService(() => payload, path => { if (++deletions == 2) throw new IOException("injected delete boundary"); }).Execute("clear-config", partial);
        Check(interrupted.exitCode == 3 && interrupted.status == "partial" && interrupted.completed.Count == 1 && File.Exists(Path.Combine(partial, "JueMingRData/config/hotkeys.json")), "partial-reset-reports-actual-completed-file");
        string steam = Path.Combine(root, "Steam"), library = Path.Combine(root, "Other Library 中文");
        string app1 = Path.GetFullPath(Path.Combine(steam, "steamapps/common/Terraria")), app2 = Path.GetFullPath(Path.Combine(library, "steamapps/common/Custom Terraria"));
        Directory.CreateDirectory(app1); File.Copy(terraria, Path.Combine(app1, "Terraria.exe"));
        Directory.CreateDirectory(app2); File.Copy(terraria, Path.Combine(app2, "Terraria.exe"));
        Write(Path.Combine(steam, "steamapps/libraryfolders.vdf"), "\"libraryfolders\" { \"0\" { \"path\" \"" + Vdf(steam) + "\" } \"1\" { \"path\" \"" + Vdf(library) + "\" } }");
        Write(Path.Combine(steam, "steamapps/appmanifest_105600.acf"), "\"AppState\" { \"appid\" \"105600\" \"installdir\" \"Terraria\" }");
        Write(Path.Combine(library, "steamapps/appmanifest_105600.acf"), "\"AppState\" { \"appid\" \"105600\" \"installdir\" \"Custom Terraria\" }");
        var found = SteamDiscovery.FindInstalled(new[] { steam, steam });
        Check(found.Length == 2 && found.Contains(app1) && found.Contains(app2) && SteamDiscovery.AutomaticSelection(found) == "", "multiple-libraries-manifest-folder-and-explicit-choice");
        Check(SteamDiscovery.AutomaticSelection(new[] { app2 }) == app2 && SteamDiscovery.AutomaticSelection(new string[0]) == "", "single-auto-select-zero-manual-browse");
        Write(Path.Combine(steam, "steamapps/libraryfolders.vdf"), "\"libraryfolders\" { \"1\" \"" + Vdf(library) + "\" }");
        Check(SteamDiscovery.FindInstalled(new[] { steam }).Length == 2, "legacy-libraryfolders-layout");
        Write(Path.Combine(library, "steamapps/appmanifest_105600.acf"), "\"AppState\" { \"appid\" \"other\" \"installdir\" \"Custom Terraria\" }");
        Check(SteamDiscovery.FindInstalled(new[] { steam }).SequenceEqual(new[] { app1 }), "wrong-appid-rejected");
        Write(Path.Combine(steam, "steamapps/appmanifest_105600.acf"), "\"AppState\" { \"appid\" \"105600\" \"installdir\" \"../escape\" }");
        Check(SteamDiscovery.FindInstalled(new[] { steam }).Length == 0, "manifest-traversal-rejected");
        File.Delete(Path.Combine(steam, "steamapps/appmanifest_105600.acf")); File.WriteAllText(Path.Combine(app1, "Terraria.exe"), "not-a-game");
        Check(SteamDiscovery.FindInstalled(new[] { steam }).Length == 0, "game-identity-not-just-exe-filename");
    }
    private static string Vdf(string path) { return path.Replace("\\", "\\\\"); }
    private static void Write(string path, string value) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, value); }
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
