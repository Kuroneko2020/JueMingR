using System;
using System.Collections.Generic;
using System.IO;

namespace JueMingR.ArchitectureTests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 1 && (args[0] == "--dynamic-hotkeys" || args[0] == "--quick-items"))
                {
                    var checks = new List<string>(); HotkeyCoreChecks.Check(checks); DynamicHotkeyChecks.Check(checks);
                    if (args[0] == "--quick-items") QuickItemChecks.Check(checks);
                    foreach (string failure in checks) Console.Error.WriteLine(failure);
                    Console.WriteLine("Dynamic hotkey checks: failures=" + checks.Count); return checks.Count == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--item-browser")
                {
                    var checks = new List<string>(); BrowserCoreChecks.Check(checks); RelationChecks.Check(checks); BrowserHistoryChecks.Check(checks); ChestKnowledgeChecks.Check(checks); AnnouncementChecks.Check(checks);
                    foreach (string failure in checks) Console.Error.WriteLine(failure);
                    return checks.Count == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--footprints")
                {
                    var checks = new List<string>(); FootprintCoreChecks.Check(checks); FootprintFileChecks.Check(checks); FootprintWorkerChecks.Check(checks);
                    foreach (string failure in checks) Console.Error.WriteLine(failure);
                    Console.WriteLine("Footprint checks: failures=" + checks.Count); return checks.Count == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--map-markers-exploration")
                {
                    var checks = new List<string>(); MapAssetChecks.Check(checks); ExplorationChecks.Check(checks); MapPersistenceChecks.Check(checks);
                    foreach (string failure in checks) Console.Error.WriteLine(failure);
                    Console.WriteLine("Map checks: failures=" + checks.Count); return checks.Count == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--death-history")
                {
                    var checks = new List<string>(); DeathArchiveChecks.Check(checks); DeathHistoryWorkerChecks.Check(checks); WorldTimeChecks.Check(checks); DeathPreferenceChecks.Check(checks); DeathWorkloadChecks.Check(checks);
                    foreach (string failure in checks) Console.Error.WriteLine(failure);
                    Console.WriteLine("Death history checks: failures=" + checks.Count);
                    return checks.Count == 0 ? 0 : 1;
                }
                if (args.Length == 1 && args[0] == "--guidance")
                {
                    var checks = new List<string>(); GuidanceTests.Check(checks);
                    foreach (string failure in checks) Console.Error.WriteLine(failure);
                    return checks.Count == 0 ? 0 : 1;
                }
                if (args.Length == 2 && args[0] == "--preference-storage-probe")
                    return PreferenceStorageChecks.RunWriterProbe(args[1]);
                if (args.Length == 2 && (args[0] == "--workload-core" || args[0] == "--workload-storage"))
                {
                    if (IntPtr.Size != 4 || typeof(object).Assembly.GetName().Name != "mscorlib") throw new InvalidOperationException("Workload requires .NET Framework x86.");
                    var workloadFailures = new List<string>();
                    if (args[0] == "--workload-core") { ObjectDiscoveryChecks.Check(workloadFailures); OpenedConcurrencyChecks.Run(); }
                    else { NotesConcurrencyChecks.Check(workloadFailures); HotkeyStorageChecks.Check(Path.GetFullPath(args[1]), workloadFailures); PreferenceConcurrencyChecks.Check(workloadFailures); PreferenceStorageChecks.Check(workloadFailures); }
                    foreach (string failure in workloadFailures) Console.Error.WriteLine(failure);
                    Console.WriteLine("Workload runtime: " + Environment.Version + "; x86; " + args[0] + "; failures=" + workloadFailures.Count);
                    return workloadFailures.Count == 0 ? 0 : 1;
                }
                if (args.Length != 1)
                {
                    throw new ArgumentException("Usage: JueMingR.ArchitectureTests <repository-root>");
                }

                string repositoryRoot = Path.GetFullPath(args[0]);
                if (!Directory.Exists(repositoryRoot))
                {
                    throw new DirectoryNotFoundException("Repository root does not exist: " + repositoryRoot);
                }

                RepositoryModel model = RepositoryModel.Load(repositoryRoot);
                var failures = new List<string>();
                ArchitectureChecks.Check(model, failures);
                BrowserCoreChecks.Check(failures);
                RelationChecks.Check(failures);
                BrowserHistoryChecks.Check(failures);
                ChestKnowledgeChecks.Check(failures); AnnouncementChecks.Check(failures);
                FootprintCoreChecks.Check(failures);
                FootprintFileChecks.Check(failures);
                FootprintWorkerChecks.Check(failures);
                MapAssetChecks.Check(failures);
                ExplorationChecks.Check(failures);
                MapPersistenceChecks.Check(failures);
                OperationContractChecks.Check(failures);
                HotkeyCoreChecks.Check(failures);
                DynamicHotkeyChecks.Check(failures);
                QuickItemChecks.Check(failures);
                HotkeyStorageChecks.Check(repositoryRoot, failures);
                Phase0TArchitectureChecks.Check(failures);
                PreferenceChecks.Check(failures);
                InformationTests.Check(failures);
                GuidanceTests.Check(failures);
                DeathArchiveChecks.Check(failures);
                DeathHistoryWorkerChecks.Check(failures);
                WorldTimeChecks.Check(failures);
                DeathPreferenceChecks.Check(failures);
                DeathWorkloadChecks.Check(failures);
                PreferenceConcurrencyChecks.Check(failures);
                EntityLabelSettingsChecks.Check(failures);
                EntityLabelRulesChecks.Check(failures);
                WorldTargetRulesChecks.Check(failures);
                WorldTargetSettingsChecks.Check(failures);
                ObjectRulesChecks.Check(failures);
                ObjectSettingsChecks.Check(failures);
                ObjectTextChecks.Check(failures);
                ObjectDiscoveryChecks.Check(failures);
                OpenedPositionChecks.Check(failures);
                PreferenceStorageChecks.Check(failures);
                ItemAutomationSettingsChecks.Check(failures);
                ItemAutomationChecks.Check(failures);
                ItemRuntimeChecks.Check(failures);
                NotesDomainChecks.Check(failures);
                NotesEditingChecks.Check(failures);
                NotesStorageChecks.Check(failures);
                NotesConcurrencyChecks.Check(failures);
                NotesRevisionChecks.Check(failures);
#if PHASE0T_MAPPING_TEST || PHASE0T_ALL_TESTS
                BiomeFeatureChecks.CheckMapping(failures);
#endif
#if PHASE0T_LIFECYCLE_TEST || PHASE0T_ALL_TESTS
                BiomeFeatureChecks.CheckLifecycle(failures);
#endif

                if (failures.Count == 0)
                {
                    Console.WriteLine("PASS: all architecture checks passed.");
                    return 0;
                }

                Console.Error.WriteLine(
                    "FAIL: architecture checks found {0} violation(s):",
                    failures.Count);
                foreach (string failure in failures)
                {
                    Console.Error.WriteLine("- {0}", failure);
                }

                return 1;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: architecture checks could not run: {0}", exception.Message);
                return 2;
            }
        }
    }
}
