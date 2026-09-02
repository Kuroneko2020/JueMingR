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
                OperationContractChecks.Check(failures);

                if (failures.Count == 0)
                {
                    Console.WriteLine("PASS: all Phase 0-R architecture checks passed.");
                    return 0;
                }

                Console.Error.WriteLine(
                    "FAIL: Phase 0-R architecture checks found {0} violation(s):",
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
