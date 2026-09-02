using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace JueMingR.Bootstrap
{
    public sealed class Phase0SAppDomainManager : AppDomainManager
    {
        private const string TargetAssemblySimpleName = "Terraria";
        private const string HostAssemblyFullName =
            "JueMingR.TerrariaHost, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        private const string HostTypeName = "JueMingR.TerrariaHost.Phase0SLoadChainHost";

        private int targetAttempted;
        private int errorRecorded;

        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs eventArgs)
        {
            Assembly targetAssembly = eventArgs.LoadedAssembly;
            string simpleName;
            try
            {
                simpleName = targetAssembly.GetName().Name;
            }
            catch (Exception)
            {
                return;
            }

            if (!String.Equals(simpleName, TargetAssemblySimpleName, StringComparison.Ordinal) ||
                Interlocked.CompareExchange(ref targetAttempted, 1, 0) != 0)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;

            RuntimeManifest manifest = null;
            string evidencePath = null;
            bool evidenceCreated = false;
            string stage = "BOOTSTRAP_MANIFEST";
            try
            {
                string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                string sidecarDirectory = Path.Combine(baseDirectory, "JueMingR.Validation");
                string manifestPath = Path.Combine(sidecarDirectory, "phase-0-s-runtime.manifest");
                manifest = RuntimeManifest.Read(manifestPath);
                evidencePath = Path.Combine(sidecarDirectory, manifest.EvidenceFileName);

                stage = "TARGET_IDENTITY";
                string targetPath = Path.Combine(baseDirectory, "Terraria.exe");
                AssemblyIdentity.VerifyLoaded(
                    targetAssembly,
                    targetPath,
                    manifest.TargetAssemblySimpleName,
                    manifest.TargetAssemblyVersion,
                    manifest.TargetAssemblyMvid,
                    manifest.TargetAssemblySha256);

                stage = "EVIDENCE_CREATE";
                EvidenceWriter.CreateFirstEvent(
                    evidencePath,
                    manifest.PackageId,
                    "TERRARIA_ASSEMBLY_READY");
                evidenceCreated = true;

                stage = "HOST_LOAD";
                string hostPath = Path.Combine(sidecarDirectory, "JueMingR.TerrariaHost.dll");
                AssemblyIdentity.VerifyFileNameAndVersion(
                    hostPath,
                    manifest.HostAssemblySimpleName,
                    manifest.HostAssemblyVersion);
                AssemblyIdentity.VerifyFileHash(hostPath, manifest.HostAssemblySha256);

                Assembly hostAssembly = Assembly.Load(new AssemblyName(HostAssemblyFullName));
                AssemblyIdentity.VerifyUniqueLoaded(
                    hostAssembly,
                    hostPath,
                    manifest.HostAssemblySimpleName,
                    manifest.HostAssemblyVersion,
                    manifest.HostAssemblyMvid,
                    manifest.HostAssemblySha256);

                stage = "HOST_ENTRY";
                Type hostType = hostAssembly.GetType(HostTypeName, true, false);
                MethodInfo install = hostType.GetMethod(
                    "Install",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                    null,
                    new[] { typeof(Assembly) },
                    null);
                if (install == null ||
                    install.ReturnType != typeof(void) ||
                    install.IsGenericMethod ||
                    install.ContainsGenericParameters)
                {
                    throw new InvalidOperationException("The fixed Phase 0-S host entry is invalid.");
                }

                install.Invoke(null, new object[] { targetAssembly });
            }
            catch (Exception exception)
            {
                if (evidenceCreated && manifest != null && evidencePath != null &&
                    Interlocked.CompareExchange(ref errorRecorded, 1, 0) == 0)
                {
                    EvidenceWriter.TryAppendError(
                        evidencePath,
                        manifest.PackageId,
                        stage,
                        GetErrorCode(stage),
                        exception.GetType().Name);
                }
            }
        }

        private static string GetErrorCode(string stage)
        {
            if (String.Equals(stage, "HOST_ENTRY", StringComparison.Ordinal))
            {
                return "VERIFY_FAILED";
            }

            if (String.Equals(stage, "EVIDENCE_CREATE", StringComparison.Ordinal))
            {
                return "APPEND_FAILED";
            }

            return "IDENTITY_MISMATCH";
        }
    }

    internal sealed class RuntimeManifest
    {
        private static readonly string[] Keys =
        {
            "schemaVersion",
            "packageId",
            "sourceCommit",
            "targetAssemblySimpleName",
            "targetAssemblyVersion",
            "targetAssemblyMvid",
            "targetAssemblySha256",
            "targetTypeName",
            "targetMethodName",
            "targetMethodMetadataToken",
            "targetMethodIsStatic",
            "targetMethodReturnType",
            "targetMethodParameterCount",
            "hostAssemblySimpleName",
            "hostAssemblyVersion",
            "hostAssemblyMvid",
            "hostAssemblySha256",
            "harmonyAssemblySimpleName",
            "harmonyAssemblyVersion",
            "harmonyAssemblyMvid",
            "harmonyAssemblySha256",
            "patchOwner",
            "evidenceFileName"
        };

        private RuntimeManifest(
            string packageId,
            string targetAssemblySimpleName,
            Version targetAssemblyVersion,
            Guid targetAssemblyMvid,
            string targetAssemblySha256,
            string hostAssemblySimpleName,
            Version hostAssemblyVersion,
            Guid hostAssemblyMvid,
            string hostAssemblySha256,
            string evidenceFileName)
        {
            PackageId = packageId;
            TargetAssemblySimpleName = targetAssemblySimpleName;
            TargetAssemblyVersion = targetAssemblyVersion;
            TargetAssemblyMvid = targetAssemblyMvid;
            TargetAssemblySha256 = targetAssemblySha256;
            HostAssemblySimpleName = hostAssemblySimpleName;
            HostAssemblyVersion = hostAssemblyVersion;
            HostAssemblyMvid = hostAssemblyMvid;
            HostAssemblySha256 = hostAssemblySha256;
            EvidenceFileName = evidenceFileName;
        }

        internal string PackageId { get; private set; }

        internal string TargetAssemblySimpleName { get; private set; }

        internal Version TargetAssemblyVersion { get; private set; }

        internal Guid TargetAssemblyMvid { get; private set; }

        internal string TargetAssemblySha256 { get; private set; }

        internal string HostAssemblySimpleName { get; private set; }

        internal Version HostAssemblyVersion { get; private set; }

        internal Guid HostAssemblyMvid { get; private set; }

        internal string HostAssemblySha256 { get; private set; }

        internal string EvidenceFileName { get; private set; }

        internal static RuntimeManifest Read(string path)
        {
            string[] values = StrictManifestReader.ReadValues(path, Keys);

            RequireExact(values[0], "1");
            RequirePackageId(values[1]);
            RequireCharacters(values[2], 40, IsLowerHex);
            RequireExact(values[3], "Terraria");
            RequireExact(values[4], "1.4.5.8");
            Guid targetMvid = ParseGuid(values[5]);
            RequireCharacters(values[6], 64, IsUpperHex);
            RequireExact(values[7], "Terraria.Main");
            RequireExact(values[8], "Initialize");
            RequireToken(values[9]);
            RequireExact(values[10], "false");
            RequireExact(values[11], "System.Void");
            RequireExact(values[12], "0");
            RequireExact(values[13], "JueMingR.TerrariaHost");
            RequireExact(values[14], "0.0.0.0");
            Guid hostMvid = ParseGuid(values[15]);
            RequireCharacters(values[16], 64, IsUpperHex);
            RequireExact(values[17], "0Harmony");
            RequireExact(values[18], "2.4.2.0");
            RequireExact(values[19], "024a0e6e-c8c2-437e-ad04-7b6279389c23");
            RequireExact(
                values[20],
                "7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C");
            RequireExact(values[21], "JueMingR.Phase0S.MainInitialize");
            RequireExact(values[22], "phase-0-s-evidence.log");

            return new RuntimeManifest(
                values[1],
                values[3],
                new Version(values[4]),
                targetMvid,
                values[6],
                values[13],
                new Version(values[14]),
                hostMvid,
                values[16],
                values[22]);
        }

        private static void RequireExact(string actual, string expected)
        {
            if (!String.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The Phase 0-S runtime manifest has an invalid fixed value.");
            }
        }

        private static Guid ParseGuid(string value)
        {
            Guid result;
            if (!Guid.TryParseExact(value, "D", out result) ||
                !String.Equals(result.ToString("D"), value, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The Phase 0-S runtime manifest has an invalid GUID.");
            }

            return result;
        }

        private static void RequirePackageId(string value)
        {
            if (value.Length < 1 || value.Length > 64)
            {
                throw new InvalidDataException("The Phase 0-S package id has an invalid length.");
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!IsAsciiLetterOrDigit(character) && character != '.' && character != '-')
                {
                    throw new InvalidDataException("The Phase 0-S package id has an invalid character.");
                }
            }
        }

        private static void RequireToken(string value)
        {
            if (value.Length != 10 || value[0] != '0' || value[1] != 'x')
            {
                throw new InvalidDataException("The Phase 0-S method token has an invalid format.");
            }

            for (int index = 2; index < value.Length; index++)
            {
                if (!IsUpperHex(value[index]))
                {
                    throw new InvalidDataException("The Phase 0-S method token has an invalid format.");
                }
            }
        }

        private static void RequireCharacters(string value, int length, Func<char, bool> predicate)
        {
            if (value.Length != length)
            {
                throw new InvalidDataException("The Phase 0-S runtime manifest field has an invalid length.");
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (!predicate(value[index]))
                {
                    throw new InvalidDataException("The Phase 0-S runtime manifest field has an invalid character.");
                }
            }
        }

        private static bool IsAsciiLetterOrDigit(char character)
        {
            return (character >= 'A' && character <= 'Z') ||
                (character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9');
        }

        private static bool IsLowerHex(char character)
        {
            return (character >= '0' && character <= '9') ||
                (character >= 'a' && character <= 'f');
        }

        private static bool IsUpperHex(char character)
        {
            return (character >= '0' && character <= '9') ||
                (character >= 'A' && character <= 'F');
        }
    }

    internal static class StrictManifestReader
    {
        internal static string[] ReadValues(string path, string[] expectedKeys)
        {
            RejectUtf8Bom(path);
            string[] lines = File.ReadAllLines(path, new UTF8Encoding(false, true));
            if (lines.Length != expectedKeys.Length)
            {
                throw new InvalidDataException("The Phase 0-S runtime manifest has an invalid line count.");
            }

            string[] values = new string[lines.Length];
            for (int index = 0; index < lines.Length; index++)
            {
                int separator = lines[index].IndexOf('=');
                if (separator <= 0 || separator != lines[index].LastIndexOf('=') ||
                    !String.Equals(
                        lines[index].Substring(0, separator),
                        expectedKeys[index],
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The Phase 0-S runtime manifest has an invalid field.");
                }

                values[index] = lines[index].Substring(separator + 1);
            }

            return values;
        }

        private static void RejectUtf8Bom(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length >= 3 &&
                    stream.ReadByte() == 0xEF &&
                    stream.ReadByte() == 0xBB &&
                    stream.ReadByte() == 0xBF)
                {
                    throw new InvalidDataException("The Phase 0-S runtime manifest must not have a BOM.");
                }
            }
        }
    }

    internal static class AssemblyIdentity
    {
        internal static void VerifyFileNameAndVersion(
            string path,
            string expectedSimpleName,
            Version expectedVersion)
        {
            AssemblyName name = AssemblyName.GetAssemblyName(path);
            if (!String.Equals(name.Name, expectedSimpleName, StringComparison.Ordinal) ||
                !Equals(name.Version, expectedVersion))
            {
                throw new InvalidDataException("A Phase 0-S assembly file identity does not match.");
            }
        }

        internal static void VerifyFileHash(string path, string expectedSha256)
        {
            if (!String.Equals(ComputeSha256(path), expectedSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("A Phase 0-S assembly hash does not match.");
            }
        }

        internal static void VerifyLoaded(
            Assembly assembly,
            string expectedPath,
            string expectedSimpleName,
            Version expectedVersion,
            Guid expectedMvid,
            string expectedSha256)
        {
            string fullExpectedPath = Path.GetFullPath(expectedPath);
            string fullActualPath = Path.GetFullPath(assembly.Location);
            AssemblyName name = assembly.GetName();
            if (!String.Equals(fullActualPath, fullExpectedPath, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(name.Name, expectedSimpleName, StringComparison.Ordinal) ||
                !Equals(name.Version, expectedVersion) ||
                assembly.ManifestModule.ModuleVersionId != expectedMvid)
            {
                throw new InvalidDataException("A loaded Phase 0-S assembly identity does not match.");
            }

            VerifyFileHash(fullExpectedPath, expectedSha256);
        }

        internal static void VerifyUniqueLoaded(
            Assembly expectedAssembly,
            string expectedPath,
            string expectedSimpleName,
            Version expectedVersion,
            Guid expectedMvid,
            string expectedSha256)
        {
            int count = 0;
            Assembly matchedAssembly = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (String.Equals(assembly.GetName().Name, expectedSimpleName, StringComparison.Ordinal))
                {
                    count++;
                    matchedAssembly = assembly;
                }
            }

            if (count != 1 || !ReferenceEquals(matchedAssembly, expectedAssembly))
            {
                throw new InvalidDataException("A loaded Phase 0-S assembly is not unique.");
            }

            VerifyLoaded(
                expectedAssembly,
                expectedPath,
                expectedSimpleName,
                expectedVersion,
                expectedMvid,
                expectedSha256);
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = algorithm.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("X2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }

    internal static class EvidenceWriter
    {
        private static readonly string[] EventNames =
        {
            "TERRARIA_ASSEMBLY_READY",
            "HARMONY_READY",
            "HOOK_INSTALLED",
            "MAIN_INITIALIZE_POSTFIX_FIRED",
            "RUNTIME_HANDOFF_COMPLETE"
        };

        internal static void CreateFirstEvent(string path, string packageId, string eventName)
        {
            if (!String.Equals(eventName, EventNames[0], StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The first Phase 0-S evidence event is invalid.");
            }

            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                WriteAndFlush(stream, FormatEvent(packageId, 1, eventName));
            }
        }

        internal static void TryAppendError(
            string path,
            string packageId,
            string stage,
            string code,
            string exceptionType)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    string[] lines = ReadAllLines(stream);
                    if (lines.Length < 1 || lines.Length > 4)
                    {
                        return;
                    }

                    VerifySuccessPrefix(lines, packageId);
                    stream.Position = stream.Length;
                    WriteAndFlush(
                        stream,
                        String.Join(
                            "|",
                            "PHASE0S",
                            "1",
                            packageId,
                            "ERROR",
                            stage,
                            code,
                            exceptionType));
                }
            }
            catch (Exception)
            {
                // Error evidence is best-effort and must never escape into the game.
            }
        }

        private static string FormatEvent(string packageId, int sequence, string eventName)
        {
            return String.Join(
                "|",
                "PHASE0S",
                "1",
                packageId,
                sequence.ToString("D2", CultureInfo.InvariantCulture),
                eventName,
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteAndFlush(FileStream stream, string line)
        {
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false, true), 1024, true))
            {
                writer.WriteLine(line);
                writer.Flush();
                stream.Flush(true);
            }
        }

        private static string[] ReadAllLines(FileStream stream)
        {
            if (stream.Length >= 3)
            {
                int first = stream.ReadByte();
                int second = stream.ReadByte();
                int third = stream.ReadByte();
                if (first == 0xEF && second == 0xBB && third == 0xBF)
                {
                    throw new InvalidDataException("The Phase 0-S evidence must not have a BOM.");
                }
            }

            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false, true), false, 1024, true))
            {
                string contents = reader.ReadToEnd();
                if (contents.Length == 0)
                {
                    return new string[0];
                }

                string normalized = contents.Replace("\r\n", "\n");
                if (!normalized.EndsWith("\n", StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The Phase 0-S evidence line is incomplete.");
                }

                normalized = normalized.Substring(0, normalized.Length - 1);
                if (normalized.IndexOf('\r') >= 0)
                {
                    throw new InvalidDataException("The Phase 0-S evidence has invalid line endings.");
                }

                return normalized.Split(new[] { '\n' }, StringSplitOptions.None);
            }
        }

        private static void VerifySuccessPrefix(string[] lines, string packageId)
        {
            for (int index = 0; index < lines.Length; index++)
            {
                string[] fields = lines[index].Split('|');
                int threadId;
                DateTimeOffset timestamp;
                if (fields.Length != 7 ||
                    !String.Equals(fields[0], "PHASE0S", StringComparison.Ordinal) ||
                    !String.Equals(fields[1], "1", StringComparison.Ordinal) ||
                    !String.Equals(fields[2], packageId, StringComparison.Ordinal) ||
                    !String.Equals(fields[3], (index + 1).ToString("D2", CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
                    !String.Equals(fields[4], EventNames[index], StringComparison.Ordinal) ||
                    !fields[5].EndsWith("Z", StringComparison.Ordinal) ||
                    !DateTimeOffset.TryParseExact(
                        fields[5],
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out timestamp) ||
                    timestamp.Offset != TimeSpan.Zero ||
                    !Int32.TryParse(fields[6], NumberStyles.None, CultureInfo.InvariantCulture, out threadId) ||
                    threadId <= 0 ||
                    !String.Equals(fields[6], threadId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The Phase 0-S evidence prefix is invalid.");
                }
            }
        }
    }
}
