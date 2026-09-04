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
        private const string ReLogicAssemblySimpleName = "ReLogic";
        private const string HostAssemblyFullName =
            "JueMingR.TerrariaHost, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        private const string HostTypeName = "JueMingR.TerrariaHost.Phase0SLoadChainHost";

        private readonly object gate = new object();
        private InstallState state = InstallState.Waiting;
        private bool initialized;
        private bool scanning;
        private int assemblyLoadHandlerDepth;
        private Assembly targetAssembly;
        private Assembly reLogicAssembly;
        private RuntimeManifest manifest;
        private string evidencePath;
        private bool evidenceCreated;
        private int errorRecorded;

        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            lock (gate)
            {
                if (initialized || state != InstallState.Waiting)
                {
                    return;
                }

                initialized = true;
                scanning = true;
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                ObserveAssembly(assembly);
            }

            InstallRequest request = null;
            try
            {
                lock (gate)
                {
                    scanning = false;
                    request = SelectInstallLocked();
                }
            }
            catch (Exception exception)
            {
                Fail(exception, "READINESS_IDENTITY", "IDENTITY_MISMATCH");
                return;
            }

            QueueInstall(request);
        }

        private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs eventArgs)
        {
            lock (gate)
            {
                assemblyLoadHandlerDepth++;
                try
                {
                    ObserveAssembly(eventArgs.LoadedAssembly);
                }
                finally
                {
                    assemblyLoadHandlerDepth--;
                }
            }
        }

        private void ObserveAssembly(Assembly assembly)
        {
            InstallRequest request = null;
            try
            {
                string simpleName = assembly.GetName().Name;
                if (!String.Equals(simpleName, TargetAssemblySimpleName, StringComparison.Ordinal) &&
                    !String.Equals(simpleName, ReLogicAssemblySimpleName, StringComparison.Ordinal))
                {
                    return;
                }

                lock (gate)
                {
                    if (state != InstallState.Waiting)
                    {
                        return;
                    }

                    EnsureContextLocked();
                    if (String.Equals(simpleName, TargetAssemblySimpleName, StringComparison.Ordinal))
                    {
                        ObserveTargetLocked(assembly);
                    }
                    else
                    {
                        ObserveReLogicLocked(assembly);
                    }

                    request = SelectInstallLocked();
                }
            }
            catch (Exception exception)
            {
                Fail(exception, "READINESS_IDENTITY", "IDENTITY_MISMATCH");
                return;
            }

            QueueInstall(request);
        }

        private void QueueInstall(InstallRequest request)
        {
            if (request == null)
            {
                return;
            }

            try
            {
                if (!ThreadPool.QueueUserWorkItem(ExecuteInstallWorkItem, request))
                {
                    throw new InvalidOperationException("The Phase 0-S install work item was not queued.");
                }
            }
            catch (Exception exception)
            {
                Fail(exception, "HOST_LOAD", "VERIFY_FAILED");
            }
        }

        private static void ExecuteInstallWorkItem(object stateObject)
        {
            InstallRequest request = stateObject as InstallRequest;
            if (request == null)
            {
                return;
            }

            request.Manager.ExecuteInstallWorkItem(request);
        }

        private void ExecuteInstallWorkItem(InstallRequest request)
        {
            try
            {
                lock (gate)
                {
                    if (state != InstallState.Queued)
                    {
                        throw new InvalidOperationException("The Phase 0-S install work item state is invalid.");
                    }

                    state = InstallState.Installing;
                    if (assemblyLoadHandlerDepth != 0)
                    {
                        throw new InvalidOperationException(
                            "The Phase 0-S install work item overlapped an AssemblyLoad handler.");
                    }

                    if (!ReferenceEquals(AppDomain.CurrentDomain, request.AppDomain) ||
                        !ReferenceEquals(targetAssembly, request.TargetAssembly) ||
                        !ReferenceEquals(reLogicAssembly, request.ReLogicAssembly) ||
                        !ContainsAssemblyReference(request.AppDomain, request.TargetAssembly) ||
                        !ContainsAssemblyReference(request.AppDomain, request.ReLogicAssembly))
                    {
                        throw new InvalidDataException(
                            "The Phase 0-S install work item lost its exact AppDomain or Assembly objects.");
                    }

                    string targetPath = Path.Combine(
                        Path.GetFullPath(request.AppDomain.BaseDirectory),
                        "Terraria.exe");
                    AssemblyIdentity.VerifyLoaded(
                        request.TargetAssembly,
                        targetPath,
                        manifest.TargetAssemblySimpleName,
                        manifest.TargetAssemblyVersion,
                        manifest.TargetAssemblyMvid,
                        manifest.TargetAssemblySha256);
                    AssemblyIdentity.VerifyCapturedReLogicBinding(
                        request.TargetAssembly,
                        request.ReLogicAssembly,
                        manifest);
                }

                Install(request.TargetAssembly, request.ReLogicAssembly);
            }
            catch (Exception exception)
            {
                Fail(exception, "READINESS_IDENTITY", "IDENTITY_MISMATCH");
            }
        }

        private static bool ContainsAssemblyReference(AppDomain appDomain, Assembly expectedAssembly)
        {
            foreach (Assembly assembly in appDomain.GetAssemblies())
            {
                if (ReferenceEquals(assembly, expectedAssembly))
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureContextLocked()
        {
            if (manifest != null)
            {
                return;
            }

            string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            string sidecarDirectory = Path.Combine(baseDirectory, "JueMingR.Validation");
            manifest = RuntimeManifest.Read(Path.Combine(sidecarDirectory, "phase-0-s-runtime.manifest"));
            evidencePath = Path.Combine(sidecarDirectory, manifest.EvidenceFileName);
        }

        private void ObserveTargetLocked(Assembly candidate)
        {
            if (targetAssembly != null)
            {
                if (!ReferenceEquals(targetAssembly, candidate))
                {
                    throw new InvalidDataException("A second Terraria assembly was observed.");
                }

                return;
            }

            string targetPath = Path.Combine(
                Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory),
                "Terraria.exe");
            AssemblyIdentity.VerifyLoaded(
                candidate,
                targetPath,
                manifest.TargetAssemblySimpleName,
                manifest.TargetAssemblyVersion,
                manifest.TargetAssemblyMvid,
                manifest.TargetAssemblySha256);
            targetAssembly = candidate;
            EvidenceWriter.CreateFirstEvent(
                evidencePath,
                manifest.PackageId,
                "TERRARIA_ASSEMBLY_READY");
            evidenceCreated = true;
        }

        private void ObserveReLogicLocked(Assembly candidate)
        {
            AssemblyIdentity.VerifyLoadedReLogic(
                candidate,
                manifest.ReLogicAssemblySimpleName,
                manifest.ReLogicAssemblyVersion,
                manifest.ReLogicAssemblyPublicKeyToken,
                manifest.ReLogicAssemblyMvid);
            if (reLogicAssembly != null && !ReferenceEquals(reLogicAssembly, candidate))
            {
                throw new InvalidDataException("A second ReLogic assembly was observed.");
            }

            reLogicAssembly = candidate;
        }

        private InstallRequest SelectInstallLocked()
        {
            if (state != InstallState.Waiting || scanning ||
                targetAssembly == null || reLogicAssembly == null)
            {
                return null;
            }

            AssemblyIdentity.VerifyReLogicBinding(targetAssembly, reLogicAssembly, manifest);
            state = InstallState.Queued;
            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            return new InstallRequest(
                this,
                AppDomain.CurrentDomain,
                targetAssembly,
                reLogicAssembly);
        }

        private void Install(Assembly target, Assembly reLogic)
        {
            string stage = "HOST_LOAD";
            try
            {
                string baseDirectory = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                string sidecarDirectory = Path.Combine(baseDirectory, "JueMingR.Validation");
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
                    new[] { typeof(Assembly), typeof(Assembly) },
                    null);
                if (install == null ||
                    install.ReturnType != typeof(void) ||
                    install.IsGenericMethod ||
                    install.ContainsGenericParameters)
                {
                    throw new InvalidOperationException("The fixed Phase 0-S host entry is invalid.");
                }

                install.Invoke(null, new object[] { target, reLogic });
                lock (gate)
                {
                    state = InstallState.Installed;
                }
            }
            catch (Exception exception)
            {
                Fail(exception, stage, GetErrorCode(stage));
            }
        }

        private void Fail(Exception exception, string stage, string code)
        {
            lock (gate)
            {
                if (state == InstallState.Installed || state == InstallState.Failed)
                {
                    return;
                }

                state = InstallState.Failed;
                AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            }

            if (evidenceCreated && manifest != null && evidencePath != null &&
                Interlocked.CompareExchange(ref errorRecorded, 1, 0) == 0)
            {
                EvidenceWriter.TryAppendError(
                    evidencePath,
                    manifest.PackageId,
                    stage,
                    code,
                    EffectiveException(exception).GetType().Name);
            }
        }

        private static Exception EffectiveException(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            return exception;
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

        private enum InstallState
        {
            Waiting,
            Queued,
            Installing,
            Installed,
            Failed
        }

        private sealed class InstallRequest
        {
            internal InstallRequest(
                Phase0SAppDomainManager manager,
                AppDomain appDomain,
                Assembly targetAssembly,
                Assembly reLogicAssembly)
            {
                Manager = manager;
                AppDomain = appDomain;
                TargetAssembly = targetAssembly;
                ReLogicAssembly = reLogicAssembly;
            }

            internal Phase0SAppDomainManager Manager { get; private set; }

            internal AppDomain AppDomain { get; private set; }

            internal Assembly TargetAssembly { get; private set; }

            internal Assembly ReLogicAssembly { get; private set; }
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
            "reLogicAssemblySimpleName",
            "reLogicAssemblyVersion",
            "reLogicAssemblyPublicKeyToken",
            "reLogicAssemblyMvid",
            "reLogicResourceName",
            "reLogicResourceSha256",
            "targetTypeName",
            "targetMethodName",
            "targetMethodMetadataToken",
            "targetMethodIsStatic",
            "targetMethodReturnType",
            "targetMethodParameterCount",
            "targetMethodParameterType",
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
            string reLogicAssemblySimpleName,
            Version reLogicAssemblyVersion,
            string reLogicAssemblyPublicKeyToken,
            Guid reLogicAssemblyMvid,
            string reLogicResourceName,
            string reLogicResourceSha256,
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
            ReLogicAssemblySimpleName = reLogicAssemblySimpleName;
            ReLogicAssemblyVersion = reLogicAssemblyVersion;
            ReLogicAssemblyPublicKeyToken = reLogicAssemblyPublicKeyToken;
            ReLogicAssemblyMvid = reLogicAssemblyMvid;
            ReLogicResourceName = reLogicResourceName;
            ReLogicResourceSha256 = reLogicResourceSha256;
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

        internal string ReLogicAssemblySimpleName { get; private set; }

        internal Version ReLogicAssemblyVersion { get; private set; }

        internal string ReLogicAssemblyPublicKeyToken { get; private set; }

        internal Guid ReLogicAssemblyMvid { get; private set; }

        internal string ReLogicResourceName { get; private set; }

        internal string ReLogicResourceSha256 { get; private set; }

        internal string HostAssemblySimpleName { get; private set; }

        internal Version HostAssemblyVersion { get; private set; }

        internal Guid HostAssemblyMvid { get; private set; }

        internal string HostAssemblySha256 { get; private set; }

        internal string EvidenceFileName { get; private set; }

        internal static RuntimeManifest Read(string path)
        {
            string[] values = StrictManifestReader.ReadValues(path, Keys);

            RequireExact(values[0], "2");
            RequirePackageId(values[1]);
            RequireCharacters(values[2], 40, IsLowerHex);
            RequireExact(values[3], "Terraria");
            RequireExact(values[4], "1.4.5.8");
            Guid targetMvid = ParseGuid(values[5]);
            RequireCharacters(values[6], 64, IsUpperHex);
            RequireExact(values[7], "ReLogic");
            RequireExact(values[8], "1.0.0.0");
            RequireExact(values[9], "null");
            Guid reLogicMvid = ParseGuid(values[10]);
            RequireExact(values[10], "ee258be9-88a4-423d-b3ce-84b6c35b141a");
            RequireExact(values[11], "Terraria.Libraries.ReLogic.ReLogic.dll");
            RequireExact(values[12], "E1C5DCCEFFF5FD1C789FF712BABFA1A305FCED0D03C96EF30F2C14D99AA0AF29");
            RequireExact(values[13], "Terraria.Main");
            RequireExact(values[14], "Update");
            RequireToken(values[15]);
            RequireExact(values[16], "false");
            RequireExact(values[17], "System.Void");
            RequireExact(values[18], "1");
            RequireExact(values[19], "Microsoft.Xna.Framework.GameTime");
            RequireExact(values[20], "JueMingR.TerrariaHost");
            RequireExact(values[21], "0.0.0.0");
            Guid hostMvid = ParseGuid(values[22]);
            RequireCharacters(values[23], 64, IsUpperHex);
            RequireExact(values[24], "0Harmony");
            RequireExact(values[25], "2.4.2.0");
            RequireExact(values[26], "024a0e6e-c8c2-437e-ad04-7b6279389c23");
            RequireExact(
                values[27],
                "7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C");
            RequireExact(values[28], "JueMingR.Phase0S.MainUpdate");
            RequireExact(values[29], "phase-0-s-evidence.log");

            return new RuntimeManifest(
                values[1],
                values[3],
                new Version(values[4]),
                targetMvid,
                values[6],
                values[7],
                new Version(values[8]),
                values[9],
                reLogicMvid,
                values[11],
                values[12],
                values[20],
                new Version(values[21]),
                hostMvid,
                values[23],
                values[29]);
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
        private const int ReLogicResourceLength = 176128;

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

        internal static void VerifyLoadedReLogic(
            Assembly assembly,
            string expectedSimpleName,
            Version expectedVersion,
            string expectedPublicKeyToken,
            Guid expectedMvid)
        {
            AssemblyName name = assembly.GetName();
            byte[] token = name.GetPublicKeyToken();
            string expectedFullName = expectedSimpleName + ", Version=" + expectedVersion +
                ", Culture=neutral, PublicKeyToken=" + expectedPublicKeyToken;
            if (!String.Equals(name.FullName, expectedFullName, StringComparison.Ordinal) ||
                !String.Equals(name.Name, expectedSimpleName, StringComparison.Ordinal) ||
                !Equals(name.Version, expectedVersion) ||
                !String.Equals(expectedPublicKeyToken, "null", StringComparison.Ordinal) ||
                (token != null && token.Length != 0) ||
                assembly.ManifestModule.ModuleVersionId != expectedMvid)
            {
                throw new InvalidDataException("The loaded ReLogic identity does not match.");
            }
        }

        internal static void VerifyReLogicBinding(
            Assembly targetAssembly,
            Assembly reLogicAssembly,
            RuntimeManifest manifest)
        {
            VerifyCapturedReLogicBinding(targetAssembly, reLogicAssembly, manifest);

            int count = 0;
            Assembly match = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (String.Equals(
                        assembly.GetName().Name,
                        manifest.ReLogicAssemblySimpleName,
                        StringComparison.Ordinal))
                {
                    count++;
                    match = assembly;
                }
            }

            if (count != 1 || !ReferenceEquals(match, reLogicAssembly))
            {
                throw new InvalidDataException("The loaded ReLogic assembly is not unique.");
            }
        }

        internal static void VerifyCapturedReLogicBinding(
            Assembly targetAssembly,
            Assembly reLogicAssembly,
            RuntimeManifest manifest)
        {
            VerifyLoadedReLogic(
                reLogicAssembly,
                manifest.ReLogicAssemblySimpleName,
                manifest.ReLogicAssemblyVersion,
                manifest.ReLogicAssemblyPublicKeyToken,
                manifest.ReLogicAssemblyMvid);

            using (Stream stream = targetAssembly.GetManifestResourceStream(manifest.ReLogicResourceName))
            {
                if (stream == null || stream.Length != ReLogicResourceLength ||
                    !String.Equals(ComputeSha256(stream), manifest.ReLogicResourceSha256, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The Terraria ReLogic resource does not match.");
                }
            }
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

        private static string ComputeSha256(Stream stream)
        {
            using (SHA256 algorithm = SHA256.Create())
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
            "MAIN_UPDATE_POSTFIX_FIRED",
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
