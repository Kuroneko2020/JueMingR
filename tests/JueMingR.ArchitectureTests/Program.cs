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
                if (args.Length != 5)
                {
                    throw new ArgumentException(
                        "Architecture tests must be invoked by the formal build entry with repository, configuration, both locked build roots, and the owned tool-state root.");
                }

                string repositoryRoot = Path.GetFullPath(args[0]);
                string configuration = args[1];
                if (!string.Equals(configuration, "Debug", StringComparison.Ordinal) &&
                    !string.Equals(configuration, "Release", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Architecture configuration must be Debug or Release.");
                }

                string lockedSdkRoot = args[2];
                string targetingPackRoot = args[3];
                string ownedToolStateRoot = args[4];
                ProjectGraphFacts facts = ProjectGraphFacts.Load(
                    repositoryRoot,
                    configuration,
                    lockedSdkRoot,
                    targetingPackRoot,
                    ownedToolStateRoot);
                var failures = new List<string>();
                ArchitectureAssertions.Check(facts, failures);
                OperationContractChecks.Check(failures);

                if (failures.Count == 0)
                {
                    Console.WriteLine("PASS: all Phase 0-R architecture checks passed.");
                    return 0;
                }

                Console.Error.WriteLine("FAIL: Phase 0-R architecture checks found {0} violation(s):", failures.Count);
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
