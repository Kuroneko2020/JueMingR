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
                string repositoryRoot = args.Length > 0
                    ? Path.GetFullPath(args[0])
                    : FindRepositoryRoot();

                ProjectGraphFacts facts = ProjectGraphFacts.Load(repositoryRoot);
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

        private static string FindRepositoryRoot()
        {
            string[] startingPoints =
            {
                Environment.CurrentDirectory,
                AppDomain.CurrentDomain.BaseDirectory
            };

            foreach (string startingPoint in startingPoints)
            {
                DirectoryInfo current = new DirectoryInfo(Path.GetFullPath(startingPoint));
                while (current != null)
                {
                    if (File.Exists(Path.Combine(current.FullName, "JueMingR.sln")) &&
                        (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                         File.Exists(Path.Combine(current.FullName, ".git"))))
                    {
                        return current.FullName;
                    }

                    current = current.Parent;
                }
            }

            throw new InvalidOperationException(
                "Repository root was not found. Pass the repository root as the first argument.");
        }
    }
}
