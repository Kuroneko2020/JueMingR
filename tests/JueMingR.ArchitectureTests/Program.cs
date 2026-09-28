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
                if (args.Length == 1 && args[0] == "--list-checks") { CheckCatalog.List(); return 0; }
                if ((args.Length == 2 && args[0] == "--structure") || (args.Length == 3 && args[0] == "--check"))
                {
                    var selectedFailures = new List<string>();
                    if (args[0] == "--structure") CheckCatalog.Structure(Path.GetFullPath(args[1]), selectedFailures);
                    else CheckCatalog.Run(args[1], args[2], selectedFailures);
                    foreach (var failure in selectedFailures) Console.Error.WriteLine(failure);
                    Console.WriteLine("Selected checks: failures=" + selectedFailures.Count);
                    return selectedFailures.Count == 0 ? 0 : 1;
                }
                if(args.Length==1 && args[0]=="--combat")
                {var checks=new List<string>();CombatChecks.Check(checks);foreach(var failure in checks)Console.Error.WriteLine(failure);Console.WriteLine("Combat failures="+checks.Count);return checks.Count==0?0:1;}
                if(args.Length==1 && args[0]=="--fishing")
                {var checks=new List<string>();FishingChecks.Check(checks);FishingStorageChecks.Check(checks);InformationTests.Check(checks);foreach(var failure in checks)Console.Error.WriteLine(failure);Console.WriteLine("Fishing failures="+checks.Count);return checks.Count==0?0:1;}
                if(args.Length==1 && args[0]=="--tools")
                {var checks=new List<string>();ToolsChecks.Check(checks);foreach(var failure in checks)Console.Error.WriteLine(failure);Console.WriteLine("Tools failures="+checks.Count);return checks.Count==0?0:1;}
                if(args.Length==1 && args[0]=="--processing")
                {var checks=new List<string>();ItemAutomationChecks.Check(checks);ProcessingChecks.Check(checks);foreach(var failure in checks)Console.Error.WriteLine(failure);Console.WriteLine("Processing rules/storage failures="+checks.Count);return checks.Count==0?0:1;}
                if (args.Length == 1 && args[0] == "--recovery")
                { var checks = new List<string>(); RecoveryChecks.Check(checks); foreach(string failure in checks) Console.Error.WriteLine(failure); return checks.Count == 0 ? 0 : 1; }
                if (args.Length == 1 && args[0] == "--onboarding")
                { var checks = new List<string>(); OnboardingChecks.Check(checks); foreach (string failure in checks) Console.Error.WriteLine(failure); return checks.Count == 0 ? 0 : 1; }
                if (args.Length == 1 && args[0] == "--coin-deposit")
                {
                    var checks = new List<string>(); CoinDepositChecks.Check(checks);
                    foreach (string failure in checks) Console.Error.WriteLine(failure);
                    Console.WriteLine("Coin checks: failures=" + checks.Count); return checks.Count == 0 ? 0 : 1;
                }
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

                var failures = new List<string>();
                CheckCatalog.Structure(repositoryRoot, failures);
                CheckCatalog.Run("*", repositoryRoot, failures);

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
