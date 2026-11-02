using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace JueMingR.Setup
{
    internal sealed class OwnedFile { internal string Path; internal string Hash; internal long Length; }
    internal sealed class OwnedSet { internal readonly List<OwnedFile> Files = new List<OwnedFile>(); internal string Directory; internal string Receipt; internal string ConfigHash; }

    // Evidence grants ownership of exact bytes only; unknown entries and all user data remain untouched.
    internal static class OwnedFiles
    {
        private static readonly string[] LegacyNames = {
            "JueMingR.Bootstrap.dll", "JueMingR.Validation/0Harmony.dll", "JueMingR.Validation/JueMingR.Features.dll",
            "JueMingR.Validation/JueMingR.Infrastructure.dll", "JueMingR.Validation/JueMingR.Platform.dll",
            "JueMingR.Validation/JueMingR.PredictionWorker.exe", "JueMingR.Validation/JueMingR.PredictionWorker.exe.config",
            "JueMingR.Validation/JueMingR.TerrariaHost.dll", "JueMingR.Validation/phase-0-s-runtime.manifest", "Terraria.exe.config" };
        private static readonly string[] BundleNames = { "0Harmony.dll", "Harmony-2.4.2-LICENSE.txt", "JueMingR.Features.dll", "JueMingR.Infrastructure.dll", "JueMingR.Platform.dll", "JueMingR.PredictionWorker.exe", "JueMingR.PredictionWorker.exe.config", "JueMingR.TerrariaHost.dll", "THIRD-PARTY-NOTICES.md", "phase-0-s-runtime.manifest" };

        internal static OwnedSet ReadLegacy(string root, List<string> retained)
        {
            string directory = System.IO.Path.Combine(root, "JueMingR.Validation");
            string receipt = System.IO.Path.Combine(directory, "phase-0-s-install-manifest.json");
            if (!Directory.Exists(directory)) return null;
            try
            {
                TargetSafety.CheckPath(directory, true, false); TargetSafety.CheckPath(receipt, false, false);
                var document = ReadJson(receipt);
                string source = (string)document["sourceCommit"], package = (string)document["packageId"];
                if (Convert.ToInt32(document["schemaVersion"]) != 1 || !Regex.IsMatch(source, "^[0-9a-f]{40}$") || !package.EndsWith("-" + source, StringComparison.Ordinal) || !Regex.IsMatch(package, "^(phase0[s-w]|phase0t-biome|phase0u-f5-ui|phase0v-settings|phase0w-notes|item-automation|unified-hotkeys|entity-labels|world-targets|world-object-text|information-summary|direction-equipment|death-history|map-markers-exploration|footprints|item-browser|favorite-quick-items|coin-deposit|about-help-feedback|recovery-buffs-services|continuous-processing)-")) throw new InvalidDataException();
                var target = (Dictionary<string, object>)document["target"];
                if ((string)target["simpleName"] != "Terraria" || (string)target["version"] != "1.4.5.8" || (string)target["mvid"] != "2c29f6c3-4bd9-4add-9c58-da159804e083" || (string)target["sha256"] != TargetSafety.TerrariaHash) throw new InvalidDataException();
                object[] entries = ((System.Collections.IEnumerable)document["payload"]).Cast<object>().ToArray();
                if (entries.Length != LegacyNames.Length) throw new InvalidDataException();
                var set = new OwnedSet { Directory = directory, Receipt = receipt };
                for (int i = 0; i < entries.Length; i++)
                {
                    var entry = (Dictionary<string, object>)entries[i];
                    string relative = (string)entry["installRelativePath"], hash = (string)entry["sha256"];
                    long length = Convert.ToInt64(entry["length"]);
                    if (relative != LegacyNames[i] || !Regex.IsMatch(hash, "^[0-9A-F]{64}$") || length < 0) throw new InvalidDataException();
                    if (relative != "Terraria.exe.config") set.Files.Add(new OwnedFile { Path = System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)), Hash = hash, Length = length });
                }
                return set;
            }
            catch (Exception error) when (!(error is OutOfMemoryException || error is StackOverflowException)) { retained.Add("旧 JueMingR.Validation：回执不可确认，旧文件已保留且不会作为新版本后备。"); return null; }
        }

        internal static OwnedSet ReadCurrentBundle(string dll, string root, List<string> retained)
        {
            if (!File.Exists(dll)) return null;
            try
            {
                TargetSafety.CheckPath(dll, false, false);
                if (AssemblyName.GetAssemblyName(dll).Name != "JueMingR") return null;
                AppDomain domain = AppDomain.CreateDomain("JueMingR.Setup.Metadata." + Guid.NewGuid().ToString("N"));
                byte[] index, config;
                try
                {
                    var reader = (BundleMetadataReader)domain.CreateInstanceFromAndUnwrap(typeof(BundleMetadataReader).Assembly.Location, typeof(BundleMetadataReader).FullName);
                    index = reader.Read(dll, "JueMingR.Bundle.index"); config = reader.Read(dll, "JueMingR.Bundle.config");
                }
                finally { AppDomain.Unload(domain); }
                if (!ConfigOwnership.IsR(config)) return null;
                string indexText = new UTF8Encoding(false, true).GetString(index);
                string[] lines = indexText.Split('\n');
                if (lines.Length != BundleNames.Length + 2 || lines[0] != "schemaVersion=1" || lines[lines.Length - 1] != "" || indexText.Contains("\r")) return null;
                string directory = System.IO.Path.Combine(root, "JueMingR.Runtime", SetupPayload.Hash(index).ToLowerInvariant());
                var set = new OwnedSet { Directory = directory, ConfigHash = SetupPayload.Hash(config) };
                set.Files.Add(new OwnedFile { Path = dll, Hash = SetupPayload.FileHash(dll), Length = new FileInfo(dll).Length });
                for (int i = 0; i < BundleNames.Length; i++)
                {
                    string[] parts = lines[i + 1].Split('|'); long length;
                    if (parts.Length != 3 || parts[0] != BundleNames[i] || !long.TryParse(parts[1], out length) || length < 0 || length.ToString(System.Globalization.CultureInfo.InvariantCulture) != parts[1] || !Regex.IsMatch(parts[2], "^[0-9A-F]{64}$")) return null;
                    set.Files.Add(new OwnedFile { Path = System.IO.Path.Combine(directory, parts[0]), Length = length, Hash = parts[2] });
                }
                return set;
            }
            catch (Exception error) when (!(error is OutOfMemoryException || error is StackOverflowException)) { retained.Add("程序展开目录：无法确认当前容器清单，已保留。"); return null; }
        }

        internal static void RemoveConfirmed(OwnedSet set, SetupResult result)
        {
            if (set == null) return;
            foreach (OwnedFile file in set.Files)
            {
                if (!File.Exists(file.Path)) continue;
                try
                {
                    TargetSafety.CheckPath(file.Path, false, false); TargetSafety.CheckUnlocked(file.Path);
                    if (new FileInfo(file.Path).Length != file.Length || SetupPayload.FileHash(file.Path) != file.Hash) { result.retained.Add(System.IO.Path.GetFileName(file.Path) + "：身份已改变，保留。"); continue; }
                    File.Delete(file.Path); result.completed.Add("remove-owned:" + System.IO.Path.GetFileName(file.Path));
                }
                catch (IOException) { result.retained.Add(System.IO.Path.GetFileName(file.Path) + "：无法清理，已保留。"); }
                catch (UnauthorizedAccessException) { result.retained.Add(System.IO.Path.GetFileName(file.Path) + "：无清理权限，已保留。"); }
            }
            try
            {
                // Migration owns the old program bytes, not the historical
                // installation evidence. Keep the original receipt in place
                // after both upgrade and later uninstall; this is no backup.
                if (set.Receipt != null && File.Exists(set.Receipt)) result.retained.Add("历史安装回执：原样保留。");
                if (Directory.Exists(set.Directory))
                {
                    TargetSafety.CheckPath(set.Directory, true, false);
                    if (!Directory.EnumerateFileSystemEntries(set.Directory).Any()) Directory.Delete(set.Directory);
                    else result.retained.Add(System.IO.Path.GetFileName(set.Directory) + "：保留历史记录或未确认的非活动文件。");
                }
            }
            catch (IOException) { result.retained.Add("程序目录仍有占用或未知内容，已保留。"); }
            catch (UnauthorizedAccessException) { result.retained.Add("程序目录无清理权限，已保留。"); }
        }

        internal static bool IsOwnReceipt(string path)
        {
            try { var r = ReadJson(path); return (string)r["schema"] == "JueMingR.Install.1" && Regex.IsMatch((string)r["dllSha256"], "^[0-9A-F]{64}$") && Regex.IsMatch((string)r["configSha256"], "^[0-9A-F]{64}$"); }
            catch (Exception e) when (!(e is OutOfMemoryException || e is StackOverflowException)) { return false; }
        }
        internal static bool ReceiptMatches(string receipt, string dll)
        {
            try { return File.Exists(dll) && IsOwnReceipt(receipt) && (string)ReadJson(receipt)["dllSha256"] == SetupPayload.FileHash(dll); }
            catch (IOException) { return false; }
        }
        internal static bool ReceiptConfigMatches(string receipt, string config)
        {
            try { return File.Exists(config) && IsOwnReceipt(receipt) && (string)ReadJson(receipt)["configSha256"] == SetupPayload.FileHash(config); }
            catch (IOException) { return false; }
        }
        private static Dictionary<string, object> ReadJson(string path)
        {
            TargetSafety.CheckPath(path, false, false);
            if (new FileInfo(path).Length > 65536) throw new InvalidDataException();
            return new JavaScriptSerializer { MaxJsonLength = 65536 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, new UTF8Encoding(false, true)));
        }
    }

    public sealed class BundleMetadataReader : MarshalByRefObject
    {
        private Assembly loaded;
        public byte[] Read(string path, string resource)
        {
            // Reflection-only metadata inspection never runs the selected DLL's entry point or initializers.
            return ReadBytes(File.ReadAllBytes(path), resource);
        }
        public byte[] ReadBytes(byte[] bytes, string resource) { return SetupPayload.Resource(Load(bytes), resource); }
        public string AssemblyName(byte[] bytes) { return Load(bytes).GetName().Name; }
        private Assembly Load(byte[] bytes) { return loaded ?? (loaded = Assembly.ReflectionOnlyLoad(bytes)); }
    }
}
