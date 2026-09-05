using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using JueMingR.Infrastructure.Settings;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class PreferenceStorageChecks
    {
        internal static void Check(IList<string> failures)
        {
            Run("missing, creation, round trip and bounded backup", RoundTrip, failures);
            Run("independent documents and second writer", IndependentWriters, failures);
            Run("real second process cannot acquire writer lease", SecondProcess, failures);
            Run("external edit preserves formal and backup", ExternalChange, failures);
            Run("first creation does not overwrite newcomer", CreationConflict, failures);
            Run("replace failure preserves formal and backup", ReplaceFailure, failures);
            Run("interrupted temp is never adopted or overwritten", InterruptedCandidate, failures);
            Run("read failure differs from missing and bounded reads", ReadFailures, failures);
            Run("stale identity and disposed storage cannot write", InvalidLifecycle, failures);
            Run("stale accepted revision never overwrites later commit", StaleWrite, failures);
            Run("oversized candidate preserves existing data", CandidateLimit, failures);
            Run("path cannot resolve through the working directory", RejectRelativePaths, failures);
        }

        internal static int RunWriterProbe(string documentPath)
        {
            using (var storage = new FilePreferenceStorage(documentPath))
                return storage.Read().Status == PreferenceReadStatus.Busy ? 0 : 1;
        }

        private static void RoundTrip(string root)
        {
            string path = Path.Combine(root, "config", "ui.json");
            using (var storage = new FilePreferenceStorage(path))
            {
                var read = storage.Read();
                Require(read.Status == PreferenceReadStatus.Missing && read.Identity != null && !File.Exists(path),
                    "first read must distinguish missing without writing defaults");
                var write = storage.Write(read.Identity, Bytes("first"));
                Require(write.Status == PreferenceWriteStatus.Saved && File.ReadAllText(path) == "first",
                    "accepted first write must commit bytes");
                Require(!File.Exists(path + ".bak"), "first creation has no invented backup");
                write = storage.Write(write.Identity, Bytes("second"));
                Require(write.Status == PreferenceWriteStatus.Saved && File.ReadAllText(path + ".bak") == "first",
                    "replace must preserve immediate prior bytes");
                write = storage.Write(write.Identity, Bytes("third"));
                Require(write.Status == PreferenceWriteStatus.Saved && File.ReadAllText(path + ".bak") == "second",
                    "later replace must rotate only one bounded backup");
                Require(!File.Exists(path + ".tmp"), "committed candidate must not remain as temp");
            }
            using (var fresh = new FilePreferenceStorage(path))
            {
                var read = fresh.Read();
                Require(read.Status == PreferenceReadStatus.Loaded && Text(read.Contents) == "third",
                    "new instance must recover committed bytes");
                byte[] exposed = read.Contents;
                exposed[0] = 0;
                Require(Text(read.Contents) == "third", "read result must not expose mutable byte ownership");
            }
            Require(Directory.GetFiles(Path.GetDirectoryName(path)).Length == 3,
                "only document, persistent lease file and one backup should exist");
        }

        private static void IndependentWriters(string root)
        {
            string first = Path.Combine(root, "ui.json");
            string other = Path.Combine(root, "biome.json");
            using (var owner = new FilePreferenceStorage(first))
            using (var competitor = new FilePreferenceStorage(first))
            using (var independent = new FilePreferenceStorage(other))
            {
                Require(owner.Read().Status == PreferenceReadStatus.Missing, "owner obtains lease");
                Require(competitor.Read().Status == PreferenceReadStatus.Busy, "second owner must report busy");
                Require(competitor.Write("missing", Bytes("bad")).Status == PreferenceWriteStatus.Busy,
                    "busy reader cannot force write");
                var read = independent.Read();
                Require(read.Status == PreferenceReadStatus.Missing &&
                    independent.Write(read.Identity, Bytes("independent")).Status == PreferenceWriteStatus.Saved,
                    "one busy document must not block the other");
            }
            using (var reopened = new FilePreferenceStorage(first))
                Require(reopened.Read().Status == PreferenceReadStatus.Missing, "Dispose must release lease");
        }

        private static void SecondProcess(string root)
        {
            string path = Path.Combine(root, "ui.json");
            using (var storage = new FilePreferenceStorage(path))
            {
                Require(storage.Read().Status == PreferenceReadStatus.Missing, "parent must hold lease");
                var start = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,
                    "--preference-storage-probe \"" + path + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process child = Process.Start(start))
                {
                    if (!child.WaitForExit(10000))
                    {
                        // Only this test-owned helper is terminated; it must not outlive
                        // the isolated root even when its expected bounded operation fails.
                        child.Kill();
                        child.WaitForExit();
                        throw new InvalidOperationException("writer probe must exit within 10 seconds");
                    }
                    Require(child.ExitCode == 0, "real child storage must receive Busy");
                }
            }
        }

        private static void ExternalChange(string root)
        {
            string path = Path.Combine(root, "ui.json");
            File.WriteAllText(path, "original");
            File.WriteAllText(path + ".bak", "known-good");
            using (var storage = new FilePreferenceStorage(path))
            {
                var read = storage.Read();
                File.WriteAllText(path, "external");
                Require(storage.Write(read.Identity, Bytes("stale")).Status == PreferenceWriteStatus.Conflict,
                    "external bytes require conflict");
                Require(File.ReadAllText(path) == "external" && File.ReadAllText(path + ".bak") == "known-good",
                    "detected external change cannot touch formal or backup bytes");
                File.WriteAllText(path, "original");
                Require(storage.Write(read.Identity, Bytes("retry")).Status == PreferenceWriteStatus.Conflict,
                    "conflict must latch and reject a later automatic retry");
                Require(File.ReadAllText(path) == "original" && File.ReadAllText(path + ".bak") == "known-good",
                    "conflict cannot replace formal or the known-good backup");
            }
        }

        private static void CreationConflict(string root)
        {
            string path = Path.Combine(root, "ui.json");
            using (var storage = new FilePreferenceStorage(path))
            {
                var read = storage.Read();
                File.WriteAllText(path, "newcomer");
                Require(storage.Write(read.Identity, Bytes("stale")).Status == PreferenceWriteStatus.Conflict &&
                    File.ReadAllText(path) == "newcomer", "first write cannot overwrite newly created file");
            }
        }

        private static void ReplaceFailure(string root)
        {
            string path = Path.Combine(root, "ui.json");
            File.WriteAllText(path, "original");
            File.WriteAllText(path + ".bak", "known-good");
            using (var storage = new FilePreferenceStorage(path))
            {
                var read = storage.Read();
                // A real sharing violation injects failure without changing ACLs or filling a disk.
                using (var locked = new FileStream(path + ".bak", FileMode.Open, FileAccess.Read, FileShare.Read))
                    Require(storage.Write(read.Identity, Bytes("candidate")).Status == PreferenceWriteStatus.IoFailure,
                        "locked backup must reject replace");
                Require(File.ReadAllText(path) == "original" && File.ReadAllText(path + ".bak") == "known-good",
                    "failed replace must preserve formal and recoverable backup");
            }
        }

        private static void InterruptedCandidate(string root)
        {
            string path = Path.Combine(root, "ui.json");
            File.WriteAllText(path, "original");
            File.WriteAllText(path + ".tmp", "interrupted");
            using (var storage = new FilePreferenceStorage(path))
            {
                var read = storage.Read();
                Require(storage.Write(read.Identity, Bytes("candidate")).Status == PreferenceWriteStatus.IoFailure,
                    "preexisting temp must block rather than be adopted");
                Require(File.ReadAllText(path) == "original" && File.ReadAllText(path + ".tmp") == "interrupted",
                    "only current operation owns cleanup, never preexisting candidate");
            }
        }

        private static void ReadFailures(string root)
        {
            string parentFile = Path.Combine(root, "not-a-directory");
            File.WriteAllText(parentFile, "protected");
            using (var storage = new FilePreferenceStorage(Path.Combine(parentFile, "ui.json")))
                Require(storage.Read().Status == PreferenceReadStatus.IoFailure,
                    "unusable directory must not masquerade as first missing file");
            string path = Path.Combine(root, "large.json");
            File.WriteAllBytes(path, new byte[65537]);
            using (var storage = new FilePreferenceStorage(path))
            {
                Require(storage.Read().Status == PreferenceReadStatus.TooLarge, "read must enforce 64 KiB bound");
                Require(storage.Write("missing", Bytes("small")).Status != PreferenceWriteStatus.Saved &&
                    new FileInfo(path).Length == 65537, "bounded read failure must protect original");
            }
            string lockedPath = Path.Combine(root, "locked.json");
            File.WriteAllText(lockedPath, "protected");
            using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (var storage = new FilePreferenceStorage(lockedPath))
                Require(storage.Read().Status == PreferenceReadStatus.IoFailure,
                    "external file access failure must be reported distinctly");
        }

        private static void InvalidLifecycle(string root)
        {
            string path = Path.Combine(root, "ui.json");
            using (var storage = new FilePreferenceStorage(path))
                Require(storage.Write("missing", Bytes("early")).Status == PreferenceWriteStatus.Conflict,
                    "write requires successful initial read");
            using (var storage = new FilePreferenceStorage(path))
            {
                var read = storage.Read();
                storage.Dispose();
                Require(storage.Write(read.Identity, Bytes("late")).Status != PreferenceWriteStatus.Saved && !File.Exists(path),
                    "disposed storage cannot create data");
            }
        }

        private static void StaleWrite(string root)
        {
            string path = Path.Combine(root, "ui.json");
            File.WriteAllText(path, "original");
            using (var storage = new FilePreferenceStorage(path))
            {
                var first = storage.Read();
                Require(storage.Write(first.Identity, Bytes("newer")).Status == PreferenceWriteStatus.Saved,
                    "first valid revision must commit");
                Require(storage.Write(first.Identity, Bytes("older")).Status == PreferenceWriteStatus.Conflict &&
                    File.ReadAllText(path) == "newer" && File.ReadAllText(path + ".bak") == "original",
                    "old expected token cannot overwrite a newer formal revision or backup");
            }
        }

        private static void CandidateLimit(string root)
        {
            string path = Path.Combine(root, "ui.json");
            File.WriteAllText(path, "original");
            File.WriteAllText(path + ".bak", "known-good");
            using (var storage = new FilePreferenceStorage(path))
            {
                var read = storage.Read();
                Require(storage.Write(read.Identity, new byte[65537]).Status == PreferenceWriteStatus.IoFailure &&
                    File.ReadAllText(path) == "original" && File.ReadAllText(path + ".bak") == "known-good" &&
                    !File.Exists(path + ".tmp"), "oversized candidate must fail before any candidate file is created");
            }
        }

        private static void RejectRelativePaths(string root)
        {
            foreach (string path in new[] { "ui.json", @"C:ui.json", @"\ui.json" })
            {
                bool rejected = false;
                try { using (var storage = new FilePreferenceStorage(path)) { } }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "relative and drive-relative paths must be rejected before any I/O");
            }
            Require(Directory.GetFiles(root).Length == 0, "constructor must not create data");
        }

        private static void Run(string label, Action<string> test, IList<string> failures)
        {
            // Each case injects a fresh explicit root before constructing any production storage.
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-PreferenceStorage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try { test(root); }
            catch (Exception exception) { failures.Add("Preference storage " + label + ": " + exception.Message); }
            finally { Directory.Delete(root, true); }
        }

        private static byte[] Bytes(string value) { return Encoding.UTF8.GetBytes(value); }
        private static string Text(byte[] value) { return Encoding.UTF8.GetString(value); }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
