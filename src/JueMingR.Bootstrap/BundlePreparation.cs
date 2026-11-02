using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace JueMingR.Bootstrap
{
    // The only owner of the runtime directory. No host type may be bound before
    // this BCL-only preparation completes; CLR caches failed binds permanently.
    internal static class BundlePreparation
    {
        private static readonly string[] Names =
        {
            "0Harmony.dll", "Harmony-2.4.2-LICENSE.txt", "JueMingR.Features.dll",
            "JueMingR.Infrastructure.dll", "JueMingR.Platform.dll",
            "JueMingR.PredictionWorker.exe", "JueMingR.PredictionWorker.exe.config",
            "JueMingR.TerrariaHost.dll", "THIRD-PARTY-NOTICES.md", "phase-0-s-runtime.manifest"
        };

        internal static string Prepare()
        {
            Assembly container = typeof(BundlePreparation).Assembly;
            string root = NormalizeDirectory(AppDomain.CurrentDomain.BaseDirectory);
            if (!String.Equals(Path.GetFullPath(container.Location), Path.Combine(root, "JueMingR.dll"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The entry container is outside the application root.");
            byte[] index = ReadResource(container, "JueMingR.Bundle.index", 16384);
            List<Member> members = ParseIndex(index);
            string payloadId = Hash(index).ToLowerInvariant();
            byte[] config = ReadResource(container, "JueMingR.Bundle.config", 32768);
            string configPath = Path.GetFullPath(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
            if (!DirectoryEquals(Path.GetDirectoryName(configPath), root))
                throw new InvalidDataException("The application configuration path is unexpected.");
            RequireOrdinaryFile(configPath);
            if (!String.Equals(Hash(File.ReadAllBytes(configPath)), Hash(config), StringComparison.Ordinal))
                throw new InvalidDataException("The configuration and container are from different payloads.");

            string runtimeRoot = Path.Combine(root, "JueMingR.Runtime");
            string directory = Path.Combine(runtimeRoot, payloadId);
            RequireOrdinaryAncestors(root);
            string mutexKey = Hash(Encoding.UTF8.GetBytes(root.ToUpperInvariant() + "|" + payloadId));
            using (Mutex mutex = new Mutex(false, @"Local\JueMingR.Bundle." + mutexKey))
            {
                bool held;
                try { held = mutex.WaitOne(TimeSpan.FromSeconds(30)); }
                catch (AbandonedMutexException) { held = true; }
                if (!held) throw new IOException("Another process is preparing this payload.");
                try
                {
                    RequireOrdinaryAncestors(directory);
                    Directory.CreateDirectory(directory);
                    RequireOrdinaryAncestors(directory);
                    using (Stream resource = container.GetManifestResourceStream("JueMingR.Bundle.payload.zip"))
                    {
                        if (resource == null) throw new InvalidDataException("The payload resource is missing.");
                        using (ZipArchive archive = new ZipArchive(resource, ZipArchiveMode.Read))
                        {
                            if (archive.Entries.Count != members.Count) throw new InvalidDataException("Unexpected payload member count.");
                            for (int i = 0; i < members.Count; i++)
                            {
                                Member member = members[i];
                                ZipArchiveEntry entry = archive.Entries[i];
                                if (!String.Equals(entry.FullName, member.Name, StringComparison.Ordinal) || entry.Length != member.Length)
                                    throw new InvalidDataException("Unexpected payload member.");
                                string target = Path.Combine(directory, member.Name);
                                if (Matches(target, member)) continue;
                                string temporary = Path.Combine(directory, ".new-" + Guid.NewGuid().ToString("N"));
                                try
                                {
                                    using (Stream source = entry.Open())
                                    using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                                    {
                                        byte[] buffer = new byte[65536];
                                        long total = 0;
                                        int read;
                                        while ((read = source.Read(buffer, 0, buffer.Length)) != 0)
                                        {
                                            total += read;
                                            if (total > member.Length) throw new InvalidDataException("The payload member exceeds its declared length.");
                                            output.Write(buffer, 0, read);
                                        }
                                        output.Flush(true);
                                    }
                                    if (!Matches(temporary, member)) throw new InvalidDataException("The embedded payload hash does not match.");
                                    RequireOrdinaryAncestors(directory);
                                    if (File.Exists(target)) { RequireOrdinaryFile(target); File.Replace(temporary, target, null); }
                                    else File.Move(temporary, target);
                                }
                                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                            }
                        }
                    }
                    // All members, including the manifest, are verified before any bind.
                    foreach (Member member in members)
                        if (!Matches(Path.Combine(directory, member.Name), member)) throw new InvalidDataException("The prepared payload is incomplete.");
                }
                finally { mutex.ReleaseMutex(); }
            }
            return directory;
        }

        internal static void RecordPreparationFailure(Exception exception)
        {
            // Preparation precedes the manifest and the five-event Host log.
            // Keep one bounded startup error, independent of Runtime and frames.
            string reason = "JueMingR BUNDLE_PREPARE: " + exception.GetType().Name + ": " + exception.Message;
            System.Diagnostics.Trace.TraceError(reason);
            try
            {
                string root = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                string logs = Path.Combine(root, "JueMingRData", "logs");
                RequireOrdinaryAncestors(logs);
                Directory.CreateDirectory(logs);
                string path = Path.Combine(logs, "bootstrap-error.txt");
                if (File.Exists(path)) RequireOrdinaryFile(path);
                if (reason.Length > 2048) reason = reason.Substring(0, 2048);
                File.WriteAllText(path, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + "\n" + reason + "\n", new UTF8Encoding(false));
            }
            catch (Exception)
            {
                // A read-only target may prevent local evidence as well. The
                // Trace outlet remains available and activation stays terminal.
            }
        }

        internal static string NormalizeDirectory(string directory)
        {
            string full = Path.GetFullPath(directory);
            int minimumLength = Path.GetPathRoot(full).Length;
            int length = full.Length;
            // Preserve the root itself: trimming D:\ into D: changes where files resolve.
            while (length > minimumLength &&
                (full[length - 1] == Path.DirectorySeparatorChar || full[length - 1] == Path.AltDirectorySeparatorChar)) length--;
            return length == full.Length ? full : full.Substring(0, length);
        }

        internal static bool DirectoryEquals(string left, string right)
        {
            return String.Equals(NormalizeDirectory(left), NormalizeDirectory(right), StringComparison.OrdinalIgnoreCase);
        }

        private static List<Member> ParseIndex(byte[] bytes)
        {
            string text = new UTF8Encoding(false, true).GetString(bytes);
            string[] lines = text.Split('\n');
            if (lines.Length != Names.Length + 2 || lines[0] != "schemaVersion=1" || lines[lines.Length - 1] != "")
                throw new InvalidDataException("Invalid bundle index.");
            var result = new List<Member>(Names.Length);
            for (int i = 0; i < Names.Length; i++)
            {
                string[] fields = lines[i + 1].Split('|');
                long length;
                if (fields.Length != 3 || fields[0] != Names[i] ||
                    !Int64.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out length) || length < 1 || length > 67108864 ||
                    length.ToString(CultureInfo.InvariantCulture) != fields[1] || fields[2].Length != 64)
                    throw new InvalidDataException("Invalid bundle member identity.");
                foreach (char c in fields[2]) if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                    throw new InvalidDataException("Invalid bundle member hash.");
                result.Add(new Member { Name = fields[0], Length = length, Sha256 = fields[2] });
            }
            return result;
        }

        private static bool Matches(string path, Member member)
        {
            if (!File.Exists(path))
            {
                if (Directory.Exists(path)) throw new InvalidDataException("A payload file is a directory.");
                return false;
            }
            RequireOrdinaryFile(path);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return stream.Length == member.Length && String.Equals(Hash(stream), member.Sha256, StringComparison.Ordinal);
        }

        private static byte[] ReadResource(Assembly assembly, string name, int maximumLength)
        {
            using (Stream stream = assembly.GetManifestResourceStream(name))
            {
                if (stream == null || stream.Length > maximumLength) throw new InvalidDataException("Missing or oversized bundle resource.");
                using (MemoryStream output = new MemoryStream()) { stream.CopyTo(output); return output.ToArray(); }
            }
        }

        private static void RequireOrdinaryFile(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new InvalidDataException("A bundle path is not an ordinary file.");
        }

        private static void RequireOrdinaryAncestors(string path)
        {
            for (DirectoryInfo item = new DirectoryInfo(path); item != null; item = item.Parent)
            {
                if (File.Exists(item.FullName)) throw new InvalidDataException("A bundle directory is a file.");
                if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("A bundle directory is a reparse point.");
            }
        }

        private static string Hash(byte[] bytes) { using (var stream = new MemoryStream(bytes, false)) return Hash(stream); }
        private static string Hash(Stream stream)
        {
            using (SHA256 algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        }

        private sealed class Member
        {
            internal string Name;
            internal long Length;
            internal string Sha256;
        }
    }
}
