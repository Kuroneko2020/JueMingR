using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JueMingR.ArchitectureTests
{
    internal static class ArchitectureAssertions
    {
        private const string Bootstrap = "src/JueMingR.Bootstrap/JueMingR.Bootstrap.csproj";
        private const string Platform = "src/JueMingR.Platform/JueMingR.Platform.csproj";
        private const string Features = "src/JueMingR.Features/JueMingR.Features.csproj";
        private const string Host = "src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj";
        private const string Infrastructure = "src/JueMingR.Infrastructure/JueMingR.Infrastructure.csproj";
        private const string Setup = "src/JueMingR.Setup/JueMingR.Setup.csproj";
        private const string Tests = "tests/JueMingR.ArchitectureTests/JueMingR.ArchitectureTests.csproj";

        private static readonly string[] ExpectedProjects =
        {
            Bootstrap,
            Platform,
            Features,
            Host,
            Infrastructure,
            Setup,
            Tests
        };

        internal static void Check(ProjectGraphFacts facts, IList<string> failures)
        {
            CheckExactSet("solution projects", facts.SolutionProjects, ExpectedProjects, failures);
            CheckExactSet("physical src/tests projects", facts.DiscoveredProjects, ExpectedProjects, failures);
            CheckProjectReferenceGraph(facts, failures);
            CheckAcyclic(facts, failures);
            CheckNeutralProjects(facts, failures);
            CheckFeatureAndInfrastructureEdges(facts, failures);
            CheckGameReferences(facts, failures);
            CheckSetupIsolation(facts, failures);
            CheckLegacyIsolation(facts, failures);
            CheckTrackedFiles(facts, failures);
            CheckNoPackages(facts, failures);
            CheckBuildProperties(facts, failures);
            CheckNoProductionDependencyOnTests(facts, failures);
            CheckSingleGameHostBoundary(facts, failures);
            CheckRequiredBuildInputs(facts, failures);
        }

        private static void CheckProjectReferenceGraph(ProjectGraphFacts facts, IList<string> failures)
        {
            var expected = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { Bootstrap, new string[0] },
                { Platform, new string[0] },
                { Features, new[] { Platform } },
                { Infrastructure, new[] { Platform } },
                { Host, new[] { Platform, Features, Infrastructure } },
                { Setup, new string[0] },
                { Tests, new[] { Platform } }
            };

            foreach (KeyValuePair<string, string[]> item in expected)
            {
                ProjectFacts project;
                if (!facts.Projects.TryGetValue(item.Key, out project))
                {
                    failures.Add("required project is missing: " + item.Key);
                    continue;
                }

                CheckExactSet(item.Key + " ProjectReference", project.ProjectReferences, item.Value, failures);
            }
        }

        private static void CheckAcyclic(ProjectGraphFacts facts, IList<string> failures)
        {
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string project in facts.Projects.Keys)
            {
                if (HasCycle(project, facts, visiting, visited))
                {
                    failures.Add("ProjectReference graph contains a cycle involving " + project);
                    return;
                }
            }
        }

        private static bool HasCycle(
            string projectPath,
            ProjectGraphFacts facts,
            ISet<string> visiting,
            ISet<string> visited)
        {
            if (visiting.Contains(projectPath))
            {
                return true;
            }

            if (visited.Contains(projectPath))
            {
                return false;
            }

            visiting.Add(projectPath);
            ProjectFacts project;
            if (facts.Projects.TryGetValue(projectPath, out project))
            {
                foreach (string dependency in project.ProjectReferences)
                {
                    if (HasCycle(dependency, facts, visiting, visited))
                    {
                        return true;
                    }
                }
            }

            visiting.Remove(projectPath);
            visited.Add(projectPath);
            return false;
        }

        private static void CheckNeutralProjects(ProjectGraphFacts facts, IList<string> failures)
        {
            CheckNoDependencies(facts, Bootstrap, "Bootstrap must remain BCL-only", failures);
            CheckNoDependencies(facts, Platform, "Platform must remain host-neutral", failures);
        }

        private static void CheckNoDependencies(
            ProjectGraphFacts facts,
            string projectPath,
            string message,
            IList<string> failures)
        {
            ProjectFacts project;
            if (!facts.Projects.TryGetValue(projectPath, out project))
            {
                return;
            }

            if (project.ProjectReferences.Count != 0 ||
                project.PackageReferences.Count != 0 ||
                project.AssemblyReferences.Any(IsGameReference))
            {
                failures.Add(message + ": " + projectPath);
            }
        }

        private static void CheckFeatureAndInfrastructureEdges(ProjectGraphFacts facts, IList<string> failures)
        {
            CheckOnlyPlatformReference(facts, Features, "Features", failures);
            CheckOnlyPlatformReference(facts, Infrastructure, "Infrastructure", failures);
        }

        private static void CheckOnlyPlatformReference(
            ProjectGraphFacts facts,
            string projectPath,
            string label,
            IList<string> failures)
        {
            ProjectFacts project;
            if (facts.Projects.TryGetValue(projectPath, out project) &&
                !SetsEqual(project.ProjectReferences, new[] { Platform }))
            {
                failures.Add(label + " may reference only Platform; actual: " + Join(project.ProjectReferences));
            }
        }

        private static void CheckGameReferences(ProjectGraphFacts facts, IList<string> failures)
        {
            string[] expectedHostReferences =
            {
                "Microsoft.Xna.Framework.Game",
                "ReLogic",
                "Terraria"
            };

            foreach (ProjectFacts project in facts.Projects.Values)
            {
                IList<AssemblyReferenceFacts> gameReferences = project.AssemblyReferences
                    .Where(IsGameReference)
                    .ToList();
                if (!string.Equals(project.RelativePath, Host, StringComparison.OrdinalIgnoreCase))
                {
                    if (gameReferences.Count != 0)
                    {
                        failures.Add("non-Host project has a forbidden game reference: " + project.RelativePath);
                    }

                    continue;
                }

                CheckExactSet(
                    "TerrariaHost game references",
                    gameReferences.Select(GetSimpleReferenceName),
                    expectedHostReferences,
                    failures);
                foreach (AssemblyReferenceFacts reference in gameReferences)
                {
                    if (!string.Equals(reference.CopyLocal, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add("TerrariaHost game reference must set Private=false: " + reference.Include);
                    }
                }
            }
        }

        private static void CheckSetupIsolation(ProjectGraphFacts facts, IList<string> failures)
        {
            ProjectFacts setup;
            if (facts.Projects.TryGetValue(Setup, out setup) &&
                (setup.ProjectReferences.Count != 0 || setup.AssemblyReferences.Any(IsGameReference)))
            {
                failures.Add("Setup must not depend on game-side projects or game assemblies.");
            }
        }

        private static void CheckLegacyIsolation(ProjectGraphFacts facts, IList<string> failures)
        {
            foreach (ProjectFacts project in facts.Projects.Values)
            {
                foreach (string reference in project.ProjectReferences)
                {
                    if (ContainsLegacy(reference))
                    {
                        failures.Add("ProjectReference must not point to JueMingZ: " + project.RelativePath);
                    }
                }

                foreach (AssemblyReferenceFacts reference in project.AssemblyReferences)
                {
                    if (ContainsLegacy(reference.Include) || ContainsLegacy(reference.HintPath))
                    {
                        failures.Add("Reference/HintPath must not point to JueMingZ: " + project.RelativePath);
                    }
                }
            }
        }

        private static void CheckTrackedFiles(ProjectGraphFacts facts, IList<string> failures)
        {
            foreach (string path in facts.TrackedFiles)
            {
                string fileName = Path.GetFileName(path);
                if (string.Equals(fileName, "Terraria.exe", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "ReLogic.dll", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fileName, "0Harmony.dll", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add("Git tracks a forbidden game/runtime binary: " + path);
                }
            }
        }

        private static void CheckNoPackages(ProjectGraphFacts facts, IList<string> failures)
        {
            foreach (ProjectFacts project in facts.Projects.Values)
            {
                if (project.PackageReferences.Count != 0)
                {
                    failures.Add("PackageReference is forbidden in Phase 0-R: " + project.RelativePath);
                }

                if (project.ExplicitImports.Count != 0)
                {
                    failures.Add("explicit MSBuild imports may not inject Phase 0-R dependency facts: " + project.RelativePath);
                }
            }

            foreach (string violation in facts.SharedBuildItemViolations)
            {
                failures.Add("shared build files may not inject project, package, or game references: " + violation);
            }
        }

        private static void CheckBuildProperties(ProjectGraphFacts facts, IList<string> failures)
        {
            foreach (ProjectFacts project in facts.Projects.Values)
            {
                if (!string.Equals(project.TargetFramework, "net472", StringComparison.Ordinal))
                {
                    failures.Add(project.RelativePath + " must explicitly target net472.");
                }

                if (!string.Equals(project.PlatformTarget, "x86", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(project.RelativePath + " must explicitly target x86.");
                }

                string expectedOutputType = string.Equals(project.RelativePath, Tests, StringComparison.OrdinalIgnoreCase)
                    ? "Exe"
                    : "Library";
                if (!string.Equals(project.OutputType, expectedOutputType, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(project.RelativePath + " must explicitly set OutputType=" + expectedOutputType + ".");
                }

                if (!string.Equals(project.GenerateAssemblyInfo, "true", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(project.AllowUnsafeBlocks, "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(project.TreatWarningsAsErrors, "true", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(project.RelativePath + " must explicitly set assembly info, unsafe, and warning policy.");
                }
            }
        }

        private static void CheckNoProductionDependencyOnTests(ProjectGraphFacts facts, IList<string> failures)
        {
            foreach (ProjectFacts project in facts.Projects.Values.Where(item => item.RelativePath.StartsWith("src/", StringComparison.OrdinalIgnoreCase)))
            {
                if (project.ProjectReferences.Contains(Tests, StringComparer.OrdinalIgnoreCase))
                {
                    failures.Add("production project must not reference ArchitectureTests: " + project.RelativePath);
                }
            }
        }

        private static void CheckSingleGameHostBoundary(ProjectGraphFacts facts, IList<string> failures)
        {
            IList<ProjectFacts> gameBoundaryProjects = facts.Projects.Values
                .Where(project => project.AssemblyReferences.Any(IsGameReference))
                .ToList();
            if (gameBoundaryProjects.Count != 1 ||
                !string.Equals(gameBoundaryProjects[0].RelativePath, Host, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add("TerrariaHost must be the sole physical project that carries game references; no concrete Composition Root is asserted in Phase 0-R.");
            }
        }

        private static void CheckRequiredBuildInputs(ProjectGraphFacts facts, IList<string> failures)
        {
            string buildScript = Path.Combine(facts.RepositoryRoot, "scripts", "build.ps1");
            string baseline = Path.Combine(facts.RepositoryRoot, "eng", "TerrariaReferences.baseline.json");
            if (!File.Exists(buildScript))
            {
                failures.Add("formal build entry is missing: scripts/build.ps1");
            }

            if (!File.Exists(baseline))
            {
                failures.Add("reference baseline is missing: eng/TerrariaReferences.baseline.json");
            }
        }

        private static bool IsGameReference(AssemblyReferenceFacts reference)
        {
            if (IsGameReference(GetSimpleReferenceName(reference)))
            {
                return true;
            }

            string normalizedHintPath = reference.HintPath.Replace('/', '\\');
            return IsGameBinaryFileName(Path.GetFileName(normalizedHintPath));
        }

        private static bool IsGameReference(string include)
        {
            return string.Equals(include, "Terraria", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(include, "ReLogic", StringComparison.OrdinalIgnoreCase) ||
                include.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(include, "0Harmony", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetSimpleReferenceName(AssemblyReferenceFacts reference)
        {
            int comma = reference.Include.IndexOf(',');
            return (comma < 0 ? reference.Include : reference.Include.Substring(0, comma)).Trim();
        }

        private static bool IsGameBinaryFileName(string fileName)
        {
            return string.Equals(fileName, "Terraria.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "ReLogic.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase) &&
                    fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "0Harmony.dll", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsLegacy(string text)
        {
            return text != null && text.IndexOf("JueMingZ", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CheckExactSet(
            string label,
            IEnumerable<string> actual,
            IEnumerable<string> expected,
            IList<string> failures)
        {
            IList<string> actualList = actual.ToList();
            IList<string> expectedList = expected.ToList();
            bool hasDuplicate = actualList.Count != actualList.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (hasDuplicate || actualList.Count != expectedList.Count || !SetsEqual(actualList, expectedList))
            {
                failures.Add(label + " mismatch; expected: " + Join(expectedList) + "; actual: " + Join(actualList));
            }
        }

        private static bool SetsEqual(IEnumerable<string> left, IEnumerable<string> right)
        {
            return new HashSet<string>(left, StringComparer.OrdinalIgnoreCase)
                .SetEquals(right);
        }

        private static string Join(IEnumerable<string> values)
        {
            return string.Join(", ", values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        }
    }
}
