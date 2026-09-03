using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using HarmonyLib;

namespace Terraria
{
    public class Main
    {
        public static bool FixtureInitializeMarker { get; private set; }

        public void RunFirstInitialize()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            // Keep the real failure shape: Harmony must JIT a method whose body directly
            // references ReLogic before Terraria's entry point has installed its resolver.
            Type platformType = typeof(ReLogic.OS.Platform);
            if (platformType == null)
            {
                throw new InvalidOperationException("The fixture ReLogic dependency was not resolved.");
            }

            FixtureInitializeMarker = true;
            Console.WriteLine("FIXTURE_MAIN_INITIALIZE_ORIGINAL");
        }
    }

    internal static class Program
    {
        private static Assembly driverReLogic;
        private static bool embeddedResolverActive;
        private static Assembly embeddedLoadEventAssembly;
        private static Assembly embeddedLoadReturnedAssembly;
        private static bool hookPresentWhenEmbeddedLoadReturned;
        private static string activeEvidencePath;

        private static readonly string[] ExpectedEvents =
        {
            "TERRARIA_ASSEMBLY_READY",
            "HARMONY_READY",
            "HOOK_INSTALLED",
            "MAIN_INITIALIZE_POSTFIX_FIRED",
            "RUNTIME_HANDOFF_COMPLETE"
        };

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3 ||
                    (args[0] != "expect-handoff" &&
                     args[0] != "expect-no-handoff" &&
                     args[0] != "expect-legacy-primary" &&
                     !args[0].StartsWith("driver-", StringComparison.Ordinal)))
                {
                    throw new ArgumentException(
                        "Usage: Terraria expect-handoff|expect-no-handoff|expect-legacy-primary <evidence-path> <package-id>");
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

                if (mode == "expect-legacy-primary")
                {
                    RunLegacyEarlyPatchProbe(evidencePath, packageId);
                    AssertLegacyPrimaryBeforeCleanup(File.ReadAllLines(evidencePath), packageId);
                    Console.WriteLine("PASS: fixture mode {0} validated.", mode);
                    return 0;
                }

                // Match WindowsLaunch.Main: install the embedded dependency resolver only
                // after the executable entry point starts, then enter code that needs ReLogic.
                activeEvidencePath = evidencePath;
                AppDomain.CurrentDomain.AssemblyLoad += ObserveEmbeddedReLogicLoad;
                AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;

                // Match LaunchGame's earlier Platform reference. Main.Initialize still has
                // its own direct ReLogic reference, but it is not the load-triggering JIT.
                TriggerLaunchGameReLogicReference();

                // The fixture's first lifecycle action is its first, parameterless Main.Initialize call.
                var main = new Main();
                main.RunFirstInitialize();

                if (mode == "expect-handoff")
                {
                    AssertEmbeddedLoadContract();
                }

                if (!global::Terraria.Main.FixtureInitializeMarker)
                {
                    throw new InvalidOperationException("The fixture Main.Initialize marker was not reached.");
                }

                string[] evidenceLines = File.Exists(evidencePath)
                    ? File.ReadAllLines(evidencePath)
                    : new string[0];
                if (mode == "expect-handoff")
                {
                    AssertCompleteHandoff(evidenceLines, packageId);
                }
                else if (mode == "expect-legacy-primary")
                {
                    AssertLegacyPrimaryBeforeCleanup(evidenceLines, packageId);
                }
                else
                {
                    AssertNoHandoffSuccess(evidenceLines, packageId);
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

        private static void RunLegacyEarlyPatchProbe(string evidencePath, string packageId)
        {
            string[] initial = File.Exists(evidencePath) ? File.ReadAllLines(evidencePath) : new string[0];
            if (initial.Length != 1)
            {
                throw new InvalidOperationException(
                    "The repaired readiness gate must wait at event 01 before the legacy probe.");
            }

            File.AppendAllText(
                evidencePath,
                String.Join(
                    "|",
                    "PHASE0S",
                    "1",
                    packageId,
                    "02",
                    "HARMONY_READY",
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture)) + Environment.NewLine,
                new UTF8Encoding(false, true));

            MethodInfo target = typeof(Main).GetMethod(
                "Initialize",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            MethodInfo postfix = typeof(Program).GetMethod(
                "LegacyProbePostfix",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var harmony = new Harmony("JueMingR.Phase0S.LegacyProbe");
            Exception primary = null;
            Exception cleanup = null;
            try
            {
                harmony.Patch(target, null, new HarmonyMethod(postfix), null, null);
            }
            catch (Exception exception)
            {
                primary = exception;
            }

            if (primary == null)
            {
                throw new InvalidOperationException(
                    "The legacy early-Patch probe unexpectedly succeeded before ReLogic loaded.");
            }

            try
            {
                harmony.Unpatch(target, HarmonyPatchType.All, "JueMingR.Phase0S.LegacyProbe");
            }
            catch (Exception exception)
            {
                cleanup = exception;
            }

            if (cleanup == null)
            {
                cleanup = new FileNotFoundException(
                    "Fixture cleanup secondary.",
                    "ReLogic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            }

            Assembly host = Assembly.Load(
                "JueMingR.TerrariaHost, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
            Type hostType = host.GetType(
                "JueMingR.TerrariaHost.Phase0SLoadChainHost",
                true,
                false);
            MethodInfo recorder = hostType.GetMethod(
                "TryRecordPatchFailure",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (recorder == null)
            {
                throw new InvalidOperationException("The primary/cleanup recorder is unavailable.");
            }

            recorder.Invoke(
                null,
                new object[]
                {
                    evidencePath,
                    packageId,
                    new TargetInvocationException(primary),
                    cleanup
                });
        }

        private static void LegacyProbePostfix()
        {
        }

        private static void RunDriverMode(string mode, string evidencePath, string packageId)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string targetPath = Path.Combine(baseDirectory, "Terraria.exe");
            string reLogicPath = Path.Combine(baseDirectory, "fixture-relogic.bin");
            byte[] reLogicBytes = File.ReadAllBytes(reLogicPath);
            object manager = null;
            Assembly target = null;

            if (mode == "driver-relogic-then-terraria")
            {
                manager = CreateManager();
                LoadDriverReLogic(reLogicBytes);
                target = Assembly.LoadFrom(targetPath);
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
            else
            {
                throw new ArgumentException("Unknown driver mode: " + mode);
            }

            bool expectSuccess = mode == "driver-relogic-then-terraria" ||
                mode == "driver-both-before-subscription" ||
                mode == "driver-duplicate-scan";
            if (expectSuccess)
            {
                string[] beforeInitialize = File.ReadAllLines(evidencePath);
                if (beforeInitialize.Length != 3 ||
                    beforeInitialize[2].Split('|')[4] != "HOOK_INSTALLED")
                {
                    throw new InvalidOperationException(
                        "HOOK_INSTALLED must be synchronously present before the first Initialize. Evidence: " +
                        String.Join(" || ", beforeInitialize));
                }

                object instance = Activator.CreateInstance(target.GetType("Terraria.Main", true, false));
                target.GetType("Terraria.Main", true, false).GetMethod("RunFirstInitialize").Invoke(
                    instance,
                    null);
                AssertCompleteHandoff(File.ReadAllLines(evidencePath), packageId);
            }
            else
            {
                AssertNoHookSuccess(File.Exists(evidencePath) ? File.ReadAllLines(evidencePath) : new string[0]);
            }
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
                string[] evidence = File.Exists(activeEvidencePath)
                    ? File.ReadAllLines(activeEvidencePath)
                    : new string[0];
                hookPresentWhenEmbeddedLoadReturned = evidence.Length == 3 &&
                    evidence[2].Split('|')[4] == "HOOK_INSTALLED";

                return loaded;
            }
        }

        private static void AssertEmbeddedLoadContract()
        {
            if (!ReferenceEquals(embeddedLoadEventAssembly, embeddedLoadReturnedAssembly) ||
                embeddedLoadReturnedAssembly == null ||
                embeddedLoadReturnedAssembly.Location.Length != 0 ||
                CountLoaded("ReLogic") != 1 ||
                !hookPresentWhenEmbeddedLoadReturned)
            {
                throw new InvalidOperationException(
                    "The embedded ReLogic load contract failed: callbackSame=" +
                    ReferenceEquals(embeddedLoadEventAssembly, embeddedLoadReturnedAssembly) +
                    ", returned=" + (embeddedLoadReturnedAssembly != null) +
                    ", locationEmpty=" + (embeddedLoadReturnedAssembly != null && embeddedLoadReturnedAssembly.Location.Length == 0) +
                    ", count=" + CountLoaded("ReLogic") +
                    ", hookAtReturn=" + hookPresentWhenEmbeddedLoadReturned + ".");
            }
        }

        private static void ObserveEmbeddedReLogicLoad(object sender, AssemblyLoadEventArgs eventArgs)
        {
            if (embeddedResolverActive &&
                String.Equals(eventArgs.LoadedAssembly.GetName().Name, "ReLogic", StringComparison.Ordinal))
            {
                embeddedLoadEventAssembly = eventArgs.LoadedAssembly;
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

        private static void AssertCompleteHandoff(IList<string> evidenceLines, string packageId)
        {
            if (evidenceLines.Count != ExpectedEvents.Length)
            {
                throw new InvalidOperationException("A complete handoff requires exactly five evidence lines.");
            }

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

        private static void AssertLegacyPrimaryBeforeCleanup(
            IList<string> evidenceLines,
            string packageId)
        {
            if (evidenceLines.Count < 3)
            {
                throw new InvalidOperationException(
                    "Legacy early-Patch evidence must contain 01/02 and a primary failure.");
            }

            AssertEvent(evidenceLines[0], packageId, "01", "TERRARIA_ASSEMBLY_READY");
            AssertEvent(evidenceLines[1], packageId, "02", "HARMONY_READY");
            for (int index = 0; index < evidenceLines.Count; index++)
            {
                string[] fields = evidenceLines[index].Split('|');
                if (fields.Length >= 5 &&
                    fields[0] == "PHASE0S" &&
                    fields[1] == "1" &&
                    fields[2] == packageId &&
                    (fields[3] == "03" || fields[3] == "04" || fields[3] == "05"))
                {
                    throw new InvalidOperationException(
                        "Legacy early-Patch must not claim 03/04/05 success.");
                }
            }

            string[] primary = evidenceLines[2].Split('|');
            if ((primary.Length != 7 && primary.Length != 8) ||
                primary[0] != "PHASE0S" ||
                primary[1] != "1" ||
                primary[2] != packageId ||
                primary[3] != "ERROR" ||
                primary[4] != "PATCH" ||
                primary[5] != "PATCH_FAILED" ||
                primary[6] != "FileNotFoundException" ||
                (primary.Length == 8 &&
                 primary[7] != "ReLogic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"))
            {
                throw new InvalidOperationException(
                    "Legacy early-Patch must preserve the missing-ReLogic primary before optional cleanup. Evidence: " +
                    String.Join(" || ", evidenceLines));
            }

            if (evidenceLines.Count > 4)
            {
                throw new InvalidOperationException(
                    "Legacy failure may retain only one primary and one cleanup secondary.");
            }

            if (evidenceLines.Count == 4)
            {
                string[] cleanup = evidenceLines[3].Split('|');
                if ((cleanup.Length != 7 && cleanup.Length != 8) || cleanup[3] != "ERROR" ||
                    cleanup[4] != "PATCH_CLEANUP" || cleanup[5] != "CLEANUP_FAILED" ||
                    (cleanup.Length == 8 &&
                     cleanup[7] != "ReLogic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"))
                {
                    throw new InvalidOperationException(
                        "Cleanup evidence must be optional and follow the primary.");
                }
            }
        }

        private static void AssertEvent(
            string line,
            string packageId,
            string sequence,
            string eventName)
        {
            string[] fields = line.Split('|');
            if (fields.Length != 7 || fields[0] != "PHASE0S" || fields[1] != "1" ||
                fields[2] != packageId || fields[3] != sequence || fields[4] != eventName)
            {
                throw new InvalidOperationException("Legacy evidence is missing required event " + sequence + ".");
            }
        }
    }
}
