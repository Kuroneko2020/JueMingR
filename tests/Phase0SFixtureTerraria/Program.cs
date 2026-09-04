using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;

namespace Microsoft.Xna.Framework
{
    public sealed class GameTime
    {
    }
}

namespace Terraria
{
    public class Main
    {
        public static int FixtureUpdateCount { get; private set; }

        protected virtual void Initialize()
        {
            Console.WriteLine("FIXTURE_MAIN_INITIALIZE_ORIGINAL");
        }

        public void RunUpdateLoop(int count)
        {
            var gameTime = new Microsoft.Xna.Framework.GameTime();
            for (int index = 0; index < count; index++)
            {
                Update(gameTime);
            }
        }

        protected virtual void Update(Microsoft.Xna.Framework.GameTime gameTime)
        {
            if (gameTime == null)
            {
                throw new ArgumentNullException("gameTime");
            }

            FixtureUpdateCount++;
            Console.WriteLine("FIXTURE_MAIN_UPDATE_ORIGINAL");
        }
    }

    internal static class Program
    {
        private static Assembly driverReLogic;
        private static bool embeddedResolverActive;
        private static Assembly embeddedLoadEventAssembly;
        private static Assembly embeddedLoadReturnedAssembly;
        private static int embeddedAssemblyLoadThreadId;

        private static readonly string[] ExpectedEvents =
        {
            "TERRARIA_ASSEMBLY_READY",
            "HARMONY_READY",
            "HOOK_INSTALLED",
            "MAIN_UPDATE_POSTFIX_FIRED",
            "RUNTIME_HANDOFF_COMPLETE"
        };

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3 ||
                    (args[0] != "expect-handoff" &&
                     args[0] != "expect-no-handoff" &&
                     !args[0].StartsWith("driver-", StringComparison.Ordinal)))
                {
                    throw new ArgumentException(
                        "Usage: Terraria expect-handoff|expect-no-handoff <evidence-path> <package-id>");
                }

                string mode = args[0];
                string evidencePath = Path.GetFullPath(args[1]);
                string packageId = args[2];
                if (String.IsNullOrWhiteSpace(packageId))
                {
                    throw new ArgumentException("Package id must not be empty.");
                }

                if (mode.StartsWith("driver-", StringComparison.Ordinal))
                {
                    RunDriverMode(mode, evidencePath, packageId);
                    Console.WriteLine("PASS: fixture mode {0} validated.", mode);
                    return 0;
                }

                // Match WindowsLaunch.Main: install the embedded dependency resolver only
                // after the executable entry point starts, then enter code that needs ReLogic.
                AppDomain.CurrentDomain.AssemblyLoad += ObserveEmbeddedReLogicLoad;
                AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;

                // Match LaunchGame's earlier Platform reference before the normal Update loop.
                TriggerLaunchGameReLogicReference();

                var main = new Main();
                byte[] evidenceAfterFirstUpdate = null;
                if (mode == "expect-handoff")
                {
                    WaitForEvidenceEvent(evidencePath, "HOOK_INSTALLED");
                    main.RunUpdateLoop(1);
                    WaitForEvidenceEvent(evidencePath, "RUNTIME_HANDOFF_COMPLETE");
                    evidenceAfterFirstUpdate = File.ReadAllBytes(evidencePath);
                    int updateCountAfterHandoff = global::Terraria.Main.FixtureUpdateCount;
                    main.RunUpdateLoop(4);
                    if (global::Terraria.Main.FixtureUpdateCount != updateCountAfterHandoff + 4)
                    {
                        throw new InvalidOperationException(
                            "The fixture trailing Main.Update loop did not run exactly four times.");
                    }

                    AssertEmbeddedLoadContract();
                }
                else
                {
                    main.RunUpdateLoop(5);
                }

                string[] evidenceLines = File.Exists(evidencePath)
                    ? File.ReadAllLines(evidencePath)
                    : new string[0];
                if (mode == "expect-handoff")
                {
                    AssertCompleteHandoff(
                        evidenceLines,
                        packageId,
                        embeddedAssemblyLoadThreadId,
                        Thread.CurrentThread.ManagedThreadId);
                    AssertBytesEqual(
                        evidenceAfterFirstUpdate,
                        File.ReadAllBytes(evidencePath),
                        "Second and later Update calls changed formal evidence.");
                    AssertPatchContract(typeof(global::Terraria.Main));
                    AssertOneShotState();
                    AssertBootstrapSchedulingState(AppDomain.CurrentDomain.DomainManager, "Installed");
                    AssertNoDiagnosticArtifact(evidencePath);
                }
                else
                {
                    AssertNoHandoffSuccess(evidenceLines, packageId);
                    AssertBootstrapSchedulingState(AppDomain.CurrentDomain.DomainManager, "Failed");
                }

                Console.WriteLine("PASS: fixture mode {0} validated.", mode);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: fixture validation failed: {0}", exception.Message);
                return 1;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TriggerLaunchGameReLogicReference()
        {
            if (typeof(ReLogic.OS.Platform) == null)
            {
                throw new InvalidOperationException("The fixture LaunchGame dependency was not resolved.");
            }
        }

        private static void RunDriverMode(string mode, string evidencePath, string packageId)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string targetPath = Path.Combine(baseDirectory, "Terraria.exe");
            string reLogicPath = Path.Combine(baseDirectory, "fixture-relogic.bin");
            byte[] reLogicBytes = File.ReadAllBytes(reLogicPath);
            object manager = null;
            Assembly target = null;
            int updatesBeforeInstall = 0;

            if (mode == "driver-relogic-then-terraria")
            {
                manager = CreateManager();
                LoadDriverReLogic(reLogicBytes);
                target = Assembly.LoadFrom(targetPath);
            }
            else if (mode == "driver-terraria-then-relogic")
            {
                manager = CreateManager();
                target = Assembly.LoadFrom(targetPath);
                LoadDriverReLogic(reLogicBytes);
            }
            else if (mode == "driver-both-before-subscription" ||
                     mode == "driver-duplicate-scan")
            {
                LoadDriverReLogic(reLogicBytes);
                target = Assembly.LoadFrom(targetPath);
                manager = CreateManager();
                if (mode == "driver-duplicate-scan")
                {
                    InitializeManager(manager);
                    MethodInfo observe = manager.GetType().GetMethod(
                        "ObserveAssembly",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    observe.Invoke(manager, new object[] { target });
                    observe.Invoke(manager, new object[] { driverReLogic });
                }
            }
            else if (mode == "driver-wrong-relogic")
            {
                manager = CreateManager();
                target = Assembly.LoadFrom(targetPath);
                var wrongName = new AssemblyName("ReLogic") { Version = new Version(9, 9, 9, 9) };
                AppDomain.CurrentDomain.DefineDynamicAssembly(wrongName, AssemblyBuilderAccess.Run);
                LoadDriverReLogic(reLogicBytes);
            }
            else if (mode == "driver-two-relogic")
            {
                LoadDriverReLogic(reLogicBytes);
                Assembly.Load(reLogicBytes);
                target = Assembly.LoadFrom(targetPath);
                manager = CreateManager();
            }
            else if (mode == "driver-relogic-never")
            {
                manager = CreateManager();
                target = Assembly.LoadFrom(targetPath);
            }
            else if (mode == "driver-update-before-install")
            {
                manager = CreateManager();
                target = Assembly.LoadFrom(targetPath);
                Type mainType = target.GetType("Terraria.Main", true, false);
                object instance = Activator.CreateInstance(mainType);
                mainType.GetMethod("RunUpdateLoop").Invoke(instance, new object[] { 1 });
                updatesBeforeInstall = 1;
                LoadDriverReLogic(reLogicBytes);
            }
            else if (mode == "driver-worker-failure")
            {
                File.Delete(Path.Combine(
                    Path.GetDirectoryName(evidencePath),
                    "JueMingR.TerrariaHost.dll"));
                manager = CreateManager();
                target = Assembly.LoadFrom(targetPath);
                LoadDriverReLogic(reLogicBytes);
            }
            else
            {
                throw new ArgumentException("Unknown driver mode: " + mode);
            }

            bool expectSuccess = mode == "driver-relogic-then-terraria" ||
                mode == "driver-terraria-then-relogic" ||
                mode == "driver-both-before-subscription" ||
                mode == "driver-duplicate-scan" ||
                mode == "driver-update-before-install";
            if (expectSuccess)
            {
                WaitForEvidenceEvent(evidencePath, "HOOK_INSTALLED");
                AssertBootstrapSchedulingState(manager, "Installed");

                Type mainType = target.GetType("Terraria.Main", true, false);
                object instance = Activator.CreateInstance(mainType);
                MethodInfo runUpdateLoop = mainType.GetMethod("RunUpdateLoop");
                runUpdateLoop.Invoke(instance, new object[] { 1 });
                WaitForEvidenceEvent(evidencePath, "RUNTIME_HANDOFF_COMPLETE");
                AssertCompleteHandoff(
                    File.ReadAllLines(evidencePath),
                    packageId,
                    Thread.CurrentThread.ManagedThreadId,
                    Thread.CurrentThread.ManagedThreadId);
                byte[] evidenceAfterFirstUpdate = File.ReadAllBytes(evidencePath);
                runUpdateLoop.Invoke(instance, new object[] { 4 });
                AssertBytesEqual(
                    evidenceAfterFirstUpdate,
                    File.ReadAllBytes(evidencePath),
                    "Driver mode observed repeated evidence after the first Update.");
                AssertPatchContract(mainType);
                AssertOneShotState();
                AssertNoDiagnosticArtifact(evidencePath);
                int expectedUpdateCount = updatesBeforeInstall + 5;
                int actualUpdateCount = (int)mainType.GetProperty(
                    "FixtureUpdateCount",
                    BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                if (actualUpdateCount != expectedUpdateCount)
                {
                    throw new InvalidOperationException(
                        "The driver Update count did not preserve the pre-install call and four trailing calls.");
                }
            }
            else if (mode == "driver-worker-failure")
            {
                WaitForBootstrapState(manager, "Failed");
                WaitForEvidenceError(evidencePath);
                AssertBootstrapSchedulingState(manager, "Failed");
                string[] lines = File.Exists(evidencePath)
                    ? File.ReadAllLines(evidencePath)
                    : new string[0];
                AssertSingleWorkerFailure(lines, packageId);
                byte[] failedEvidence = File.ReadAllBytes(evidencePath);
                InvokeObservedAssembly(manager, target);
                InvokeObservedAssembly(manager, driverReLogic);
                Thread.Sleep(250);
                AssertBytesEqual(
                    failedEvidence,
                    File.ReadAllBytes(evidencePath),
                    "A permanently failed install retried or changed evidence.");
            }
            else
            {
                AssertNoHookSuccess(File.Exists(evidencePath) ? File.ReadAllLines(evidencePath) : new string[0]);
                AssertBootstrapSchedulingState(manager, "Failed", mode == "driver-relogic-never");
            }
        }

        private static void InvokeObservedAssembly(object manager, Assembly assembly)
        {
            MethodInfo observe = manager.GetType().GetMethod(
                "ObserveAssembly",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            observe.Invoke(manager, new object[] { assembly });
        }

        private static Assembly LoadDriverReLogic(byte[] bytes)
        {
            if (driverReLogic == null)
            {
                AppDomain.CurrentDomain.AssemblyResolve += ResolveDriverReLogic;
            }

            Assembly loaded = Assembly.Load(bytes);
            if (driverReLogic == null)
            {
                driverReLogic = loaded;
            }

            return loaded;
        }

        private static Assembly ResolveDriverReLogic(object sender, ResolveEventArgs eventArgs)
        {
            AssemblyName requested = new AssemblyName(eventArgs.Name);
            return String.Equals(requested.Name, "ReLogic", StringComparison.Ordinal)
                ? driverReLogic
                : null;
        }

        private static object CreateManager()
        {
            Assembly bootstrap = Assembly.Load(
                "JueMingR.Bootstrap, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            object manager = Activator.CreateInstance(bootstrap.GetType(
                "JueMingR.Bootstrap.Phase0SAppDomainManager",
                true,
                false));
            InitializeManager(manager);
            return manager;
        }

        private static void InitializeManager(object manager)
        {
            manager.GetType().GetMethod("InitializeNewDomain").Invoke(
                manager,
                new object[] { AppDomain.CurrentDomain.SetupInformation });
        }

        private static void AssertNoHookSuccess(IList<string> lines)
        {
            foreach (string line in lines)
            {
                string[] fields = line.Split('|');
                if (fields.Length >= 5 &&
                    (fields[3] == "02" || fields[3] == "03" ||
                     fields[3] == "04" || fields[3] == "05"))
                {
                    throw new InvalidOperationException(
                        "A fail-closed readiness case claimed Host/Hook success.");
                }
            }
        }

        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs eventArgs)
        {
            var requestedName = new AssemblyName(eventArgs.Name);
            if (!String.Equals(requestedName.Name, "ReLogic", StringComparison.Ordinal))
            {
                return null;
            }

            if (embeddedResolverActive)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (String.Equals(
                            assembly.GetName().FullName,
                            "ReLogic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                            StringComparison.Ordinal))
                    {
                        return assembly;
                    }
                }

                throw new InvalidOperationException(
                    "The in-flight embedded ReLogic assembly was not visible to recursive binding.");
            }

            Assembly executable = Assembly.GetExecutingAssembly();
            using (Stream stream = executable.GetManifestResourceStream(
                "Terraria.Libraries.ReLogic.ReLogic.dll"))
            {
                if (stream == null || stream.Length != 176128)
                {
                    throw new InvalidOperationException("The fixture embedded ReLogic resource is invalid.");
                }

                byte[] bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0)
                    {
                        throw new EndOfStreamException("The fixture embedded ReLogic resource is truncated.");
                    }

                    offset += read;
                }

                embeddedResolverActive = true;
                Assembly loaded;
                try
                {
                    loaded = Assembly.Load(bytes);
                }
                finally
                {
                    embeddedResolverActive = false;
                }

                embeddedLoadReturnedAssembly = loaded;

                return loaded;
            }
        }

        private static void AssertEmbeddedLoadContract()
        {
            if (!ReferenceEquals(embeddedLoadEventAssembly, embeddedLoadReturnedAssembly) ||
                embeddedLoadReturnedAssembly == null ||
                embeddedLoadReturnedAssembly.Location.Length != 0 ||
                CountLoaded("ReLogic") != 1 ||
                embeddedAssemblyLoadThreadId <= 0)
            {
                throw new InvalidOperationException(
                    "The embedded ReLogic load contract failed: callbackSame=" +
                    ReferenceEquals(embeddedLoadEventAssembly, embeddedLoadReturnedAssembly) +
                    ", returned=" + (embeddedLoadReturnedAssembly != null) +
                    ", locationEmpty=" + (embeddedLoadReturnedAssembly != null && embeddedLoadReturnedAssembly.Location.Length == 0) +
                    ", count=" + CountLoaded("ReLogic") +
                    ", handlerThread=" + embeddedAssemblyLoadThreadId + ".");
            }
        }

        private static void ObserveEmbeddedReLogicLoad(object sender, AssemblyLoadEventArgs eventArgs)
        {
            if (embeddedResolverActive &&
                String.Equals(eventArgs.LoadedAssembly.GetName().Name, "ReLogic", StringComparison.Ordinal))
            {
                embeddedLoadEventAssembly = eventArgs.LoadedAssembly;
                embeddedAssemblyLoadThreadId = Thread.CurrentThread.ManagedThreadId;
            }
        }

        private static int CountLoaded(string simpleName)
        {
            int count = 0;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (String.Equals(assembly.GetName().Name, simpleName, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertCompleteHandoff(
            IList<string> evidenceLines,
            string packageId,
            int assemblyLoadThreadId,
            int updateThreadId)
        {
            if (evidenceLines.Count != ExpectedEvents.Length)
            {
                throw new InvalidOperationException("A complete handoff requires exactly five evidence lines.");
            }

            var threadIds = new int[ExpectedEvents.Length];
            DateTimeOffset previousTimestamp = DateTimeOffset.MinValue;
            for (int index = 0; index < ExpectedEvents.Length; index++)
            {
                string[] fields = evidenceLines[index].Split('|');
                if (fields.Length != 7 ||
                    fields[0] != "PHASE0S" ||
                    fields[1] != "1" ||
                    fields[2] != packageId ||
                    fields[3] != (index + 1).ToString("D2") ||
                    fields[4] != ExpectedEvents[index] ||
                    String.IsNullOrWhiteSpace(fields[5]) ||
                    String.IsNullOrWhiteSpace(fields[6]))
                {
                    throw new InvalidOperationException(
                        "Evidence line " + (index + 1) + " is not the required strict Phase 0-S event.");
                }

                DateTimeOffset timestamp;
                int threadId;
                if (!DateTimeOffset.TryParse(fields[5], out timestamp) ||
                    timestamp < previousTimestamp ||
                    !Int32.TryParse(fields[6], out threadId) ||
                    threadId <= 0)
                {
                    throw new InvalidOperationException("Evidence timestamp or managed thread id is invalid.");
                }

                previousTimestamp = timestamp;
                threadIds[index] = threadId;
            }

            if (assemblyLoadThreadId <= 0 || updateThreadId <= 0 ||
                threadIds[1] != threadIds[2] ||
                threadIds[1] == assemblyLoadThreadId)
            {
                throw new InvalidOperationException(
                    "HARMONY_READY and HOOK_INSTALLED must come from one worker outside the AssemblyLoad thread.");
            }

            if (threadIds[3] != updateThreadId || threadIds[4] != updateThreadId)
            {
                throw new InvalidOperationException(
                    "Postfix and handoff evidence must come from the actual Update caller thread.");
            }
        }

        private static void WaitForEvidenceEvent(string evidencePath, string eventName)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            {
                if (HasEvidenceEvent(evidencePath, eventName))
                {
                    return;
                }

                Thread.Sleep(10);
            }

            throw new InvalidOperationException("Timed out waiting for evidence event " + eventName + ".");
        }

        private static bool HasEvidenceEvent(string evidencePath, string eventName)
        {
            if (!File.Exists(evidencePath))
            {
                return false;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(evidencePath);
            }
            catch (IOException)
            {
                return false;
            }

            foreach (string line in lines)
            {
                string[] fields = line.Split('|');
                if (fields.Length >= 5 && fields[4] == eventName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WaitForEvidenceError(string evidencePath)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            {
                try
                {
                    if (File.Exists(evidencePath))
                    {
                        foreach (string line in File.ReadAllLines(evidencePath))
                        {
                            string[] fields = line.Split('|');
                            if (fields.Length >= 4 && fields[3] == "ERROR")
                            {
                                return;
                            }
                        }
                    }
                }
                catch (IOException)
                {
                    // The single worker can still be flushing its terminal error.
                }

                Thread.Sleep(10);
            }

            throw new InvalidOperationException("Timed out waiting for the worker failure evidence.");
        }

        private static void WaitForBootstrapState(object manager, string expectedState)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            {
                if (GetBootstrapState(manager) == expectedState)
                {
                    return;
                }

                Thread.Sleep(10);
            }

            throw new InvalidOperationException("Timed out waiting for Bootstrap state " + expectedState + ".");
        }

        private static string GetBootstrapState(object manager)
        {
            FieldInfo state = manager == null
                ? null
                : manager.GetType().GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
            return state == null ? null : state.GetValue(manager).ToString();
        }

        private static void AssertBootstrapSchedulingState(
            object manager,
            string expectedState,
            bool allowWaiting)
        {
            if (manager == null)
            {
                throw new InvalidOperationException("The fixture could not observe the Phase 0-S AppDomainManager.");
            }

            string[] states = Enum.GetNames(manager.GetType().GetField(
                "state",
                BindingFlags.Instance | BindingFlags.NonPublic).FieldType);
            if (states.Length != 5 ||
                states[0] != "Waiting" ||
                states[1] != "Queued" ||
                states[2] != "Installing" ||
                states[3] != "Installed" ||
                states[4] != "Failed")
            {
                throw new InvalidOperationException(
                    "Bootstrap must expose the monotonic Waiting/Queued/Installing/Installed/Failed lifecycle.");
            }

            string actualState = GetBootstrapState(manager);
            if ((!allowWaiting && actualState != expectedState) ||
                (allowWaiting && actualState != "Waiting"))
            {
                throw new InvalidOperationException(
                    "Unexpected Bootstrap scheduling state: " + actualState + ".");
            }

            FieldInfo handlerDepth = manager.GetType().GetField(
                "assemblyLoadHandlerDepth",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (handlerDepth == null || (int)handlerDepth.GetValue(manager) != 0)
            {
                throw new InvalidOperationException(
                    "Bootstrap install overlapped an active AssemblyLoad handler.");
            }
        }

        private static void AssertBootstrapSchedulingState(object manager, string expectedState)
        {
            AssertBootstrapSchedulingState(manager, expectedState, false);
        }

        private static void AssertSingleWorkerFailure(IList<string> lines, string packageId)
        {
            int errorCount = 0;
            foreach (string line in lines)
            {
                string[] fields = line.Split('|');
                if (fields.Length >= 4 && fields[0] == "PHASE0S" && fields[2] == packageId)
                {
                    if (fields[3] == "ERROR")
                    {
                        errorCount++;
                    }
                    else if (fields[3] != "01")
                    {
                        throw new InvalidOperationException(
                            "A worker failure emitted a later success event.");
                    }
                }
            }

            if (errorCount != 1)
            {
                throw new InvalidOperationException(
                    "A worker failure must record exactly one error and never retry.");
            }
        }

        private static void AssertBytesEqual(byte[] expected, byte[] actual, string message)
        {
            if (expected.Length != actual.Length)
            {
                throw new InvalidOperationException(message);
            }

            for (int index = 0; index < expected.Length; index++)
            {
                if (expected[index] != actual[index])
                {
                    throw new InvalidOperationException(message);
                }
            }
        }

        private static void AssertPatchContract(Type mainType)
        {
            const string owner = "JueMingR.Phase0S.MainUpdate";
            BindingFlags flags = BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;
            MethodInfo initialize = mainType.GetMethod(
                "Initialize",
                flags,
                null,
                Type.EmptyTypes,
                null);
            if (initialize == null || HasOwner(Harmony.GetPatchInfo(initialize), owner))
            {
                throw new InvalidOperationException("Main.Initialize still has the Phase 0-S patch owner.");
            }

            MethodInfo update = null;
            foreach (MethodInfo candidate in mainType.GetMethods(flags))
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                if (candidate.Name == "Update" &&
                    candidate.ReturnType == typeof(void) &&
                    !candidate.IsStatic &&
                    !candidate.IsGenericMethod &&
                    parameters.Length == 1 &&
                    parameters[0].ParameterType.FullName == "Microsoft.Xna.Framework.GameTime")
                {
                    if (update != null)
                    {
                        throw new InvalidOperationException("The fixture exposes more than one exact Update target.");
                    }

                    update = candidate;
                }
            }

            Patches patches = update == null ? null : Harmony.GetPatchInfo(update);
            if (patches == null ||
                patches.Owners.Count != 1 ||
                patches.Owners[0] != owner ||
                patches.Prefixes.Count != 0 ||
                patches.Postfixes.Count != 1 ||
                patches.Transpilers.Count != 0 ||
                patches.Finalizers.Count != 0 ||
                patches.InnerPrefixes.Count != 0 ||
                patches.InnerPostfixes.Count != 0 ||
                patches.Postfixes[0].owner != owner ||
                patches.Postfixes[0].PatchMethod.Name != "Postfix" ||
                patches.Postfixes[0].PatchMethod.DeclaringType.FullName !=
                    "JueMingR.TerrariaHost.Phase0SHarmonyWorker")
            {
                throw new InvalidOperationException("Main.Update does not have the exact one-postfix Phase 0-S patch set.");
            }

            foreach (MethodInfo candidate in mainType.GetMethods(flags))
            {
                if (!ReferenceEquals(candidate, update) && HasOwner(Harmony.GetPatchInfo(candidate), owner))
                {
                    throw new InvalidOperationException("The Phase 0-S owner patched a second Main method.");
                }
            }
        }

        private static bool HasOwner(Patches patches, string owner)
        {
            if (patches == null)
            {
                return false;
            }

            foreach (string actualOwner in patches.Owners)
            {
                if (actualOwner == owner)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertOneShotState()
        {
            Assembly host = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "JueMingR.TerrariaHost")
                {
                    host = assembly;
                    break;
                }
            }

            Type worker = host == null
                ? null
                : host.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", false, false);
            FieldInfo postfixGate = worker == null
                ? null
                : worker.GetField("postfixGate", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo handoffGate = worker == null
                ? null
                : worker.GetField("handoffGate", BindingFlags.Static | BindingFlags.NonPublic);
            if (postfixGate == null || handoffGate == null ||
                (int)postfixGate.GetValue(null) != 1 ||
                (int)handoffGate.GetValue(null) != 1)
            {
                throw new InvalidOperationException("The Update postfix or empty handoff gate was not consumed exactly once.");
            }
        }

        private static void AssertNoDiagnosticArtifact(string evidencePath)
        {
            string sentinelPath = Path.Combine(
                Path.GetDirectoryName(evidencePath),
                "phase-0-s-diagnostic.sentinel");
            if (File.Exists(sentinelPath))
            {
                throw new InvalidOperationException("The removed diagnostic sentinel was recreated.");
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if ((name == "JueMingR.Bootstrap" || name == "JueMingR.TerrariaHost") &&
                    assembly.GetType(name + ".Phase0SDiagnosticSentinel", false, false) != null)
                {
                    throw new InvalidOperationException("A removed diagnostic sentinel type remains in production output.");
                }
            }
        }

        private static void AssertNoHandoffSuccess(IList<string> evidenceLines, string packageId)
        {
            foreach (string line in evidenceLines)
            {
                string[] fields = line.Split('|');
                if (fields.Length >= 5 &&
                    fields[0] == "PHASE0S" &&
                    fields[1] == "1" &&
                    fields[2] == packageId &&
                    fields[4] == "RUNTIME_HANDOFF_COMPLETE")
                {
                    throw new InvalidOperationException(
                        "Negative fixture mode rejects RUNTIME_HANDOFF_COMPLETE.");
                }
            }
        }

    }
}
