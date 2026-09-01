using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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
                project.AssemblyReferences.Count != 0)
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
            var expectedHostReferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Microsoft.Xna.Framework.Game", "Microsoft.Xna.Framework.Game.dll" },
                { "ReLogic", "ReLogic.dll" },
                { "Terraria", "Terraria.exe" }
            };

            foreach (ProjectFacts project in facts.Projects.Values)
            {
                if (!string.Equals(project.RelativePath, Host, StringComparison.OrdinalIgnoreCase))
                {
                    if (project.AssemblyReferences.Count != 0)
                    {
                        failures.Add("non-Host project has a forbidden direct assembly reference: " + project.RelativePath);
                    }

                    continue;
                }

                CheckExactSet(
                    "TerrariaHost direct assembly references",
                    project.AssemblyReferences.Select(GetSimpleReferenceName),
                    expectedHostReferences.Keys,
                    failures);
                foreach (AssemblyReferenceFacts reference in project.AssemblyReferences)
                {
                    string simpleName = GetSimpleReferenceName(reference);
                    string logicalName;
                    if (!expectedHostReferences.TryGetValue(simpleName, out logicalName))
                    {
                        continue;
                    }

                    string expectedHintPath = "$(TerrariaReferencesDirectory)\\" + logicalName;
                    if (!string.Equals(reference.Include, simpleName, StringComparison.Ordinal) ||
                        !string.Equals(reference.HintPath, expectedHintPath, StringComparison.Ordinal))
                    {
                        failures.Add("TerrariaHost reference identity or HintPath mismatch: " + reference.Include);
                    }

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
                (setup.ProjectReferences.Count != 0 || setup.AssemblyReferences.Count != 0))
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
                    if (ContainsForbiddenProjectName(reference))
                    {
                        failures.Add("ProjectReference must not point to Legacy or TerrariaHelper: " + project.RelativePath);
                    }
                }

                foreach (AssemblyReferenceFacts reference in project.AssemblyReferences)
                {
                    if (ContainsForbiddenProjectName(reference.Include) || ContainsForbiddenProjectName(reference.HintPath))
                    {
                        failures.Add("Reference/HintPath must not point to Legacy or TerrariaHelper: " + project.RelativePath);
                    }
                }
            }
        }

        private static void CheckTrackedFiles(ProjectGraphFacts facts, IList<string> failures)
        {
            if (facts.ForbiddenBinaryHashes.Count != 3)
            {
                failures.Add("reference baseline must expose exactly three unique forbidden binary hashes.");
            }

            foreach (string path in facts.TrackedFiles)
            {
                string fileName = Path.GetFileName(path);
                string absolutePath = Path.Combine(facts.RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar));
                if (IsForbiddenBinaryFileName(fileName))
                {
                    failures.Add("Git tracks a forbidden game/runtime binary: " + path);
                }

                if (!File.Exists(absolutePath))
                {
                    continue;
                }

                if (facts.ForbiddenBinaryHashes.Contains(ProjectGraphFacts.GetFileSha256(absolutePath)))
                {
                    failures.Add("Git tracks a forbidden game binary under matching content: " + path);
                }

                try
                {
                    AssemblyName assemblyName = AssemblyName.GetAssemblyName(absolutePath);
                    if (IsForbiddenAssemblyName(assemblyName.Name))
                    {
                        failures.Add("Git tracks a forbidden managed assembly identity: " + path);
                    }
                }
                catch (BadImageFormatException)
                {
                    // Non-managed tracked files have no assembly identity to inspect.
                }
                catch (FileLoadException exception)
                {
                    failures.Add("Could not inspect tracked managed assembly identity for " + path + ": " + exception.Message);
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
                foreach (string violation in project.FactViolations)
                {
                    failures.Add(project.RelativePath + ": " + violation);
                }

                if (!string.Equals(project.ProjectSdk, "Microsoft.NET.Sdk", StringComparison.Ordinal))
                {
                    failures.Add(project.RelativePath + " must use exactly Microsoft.NET.Sdk.");
                }

                string expectedName = Path.GetFileNameWithoutExtension(project.RelativePath);
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

                if (!string.Equals(project.AssemblyName, expectedName, StringComparison.Ordinal) ||
                    !string.Equals(project.RootNamespace, expectedName, StringComparison.Ordinal))
                {
                    failures.Add(project.RelativePath + " must explicitly use its approved AssemblyName and RootNamespace.");
                }

                if (!string.Equals(project.EvaluatedTargetFramework, "net472", StringComparison.Ordinal) ||
                    !string.Equals(project.EvaluatedPlatformTarget, "x86", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(project.EvaluatedLangVersion, "7.3", StringComparison.Ordinal) ||
                    !string.Equals(project.EvaluatedOutputType, expectedOutputType, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(project.EvaluatedAssemblyName, expectedName, StringComparison.Ordinal) ||
                    !string.Equals(project.EvaluatedRootNamespace, expectedName, StringComparison.Ordinal) ||
                    !string.Equals(project.EvaluatedGenerateAssemblyInfo, "true", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(project.EvaluatedAllowUnsafeBlocks, "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(project.EvaluatedTreatWarningsAsErrors, "true", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(project.UsingMicrosoftNetSdk, "true", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(project.EvaluatedDeterministic, "true", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(project.EvaluatedContinuousIntegrationBuild, "true", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(project.EvaluatedCopyLocalLockFileAssemblies, "false", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(project.RelativePath + " effective MSBuild properties do not match the approved Phase 0-R build contract.");
                }

                IDictionary<string, string> evaluated = project.EvaluatedProperties;
                string projectDirectory = Path.GetDirectoryName(Path.Combine(
                    facts.RepositoryRoot,
                    project.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                string evaluationRoot = Path.Combine(
                    facts.RepositoryRoot,
                    "artifacts",
                    "architecture-evaluation",
                    facts.Configuration);
                string expectedPathMap = projectDirectory + "=/_/" + expectedName + "," +
                    evaluationRoot + "=/_/build";
                if (evaluated == null ||
                    !string.Equals(evaluated["DeterministicSourcePaths"], "true", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(evaluated["DebugType"], "portable", StringComparison.Ordinal) ||
                    !string.Equals(evaluated["DebugSymbols"], "true", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(evaluated["CodePage"], "65001", StringComparison.Ordinal) ||
                    !string.Equals(evaluated["PathMap"], expectedPathMap, StringComparison.Ordinal) ||
                    !string.Equals(evaluated["SourceRevisionId"], "architecture-evaluation", StringComparison.Ordinal) ||
                    !string.Equals(evaluated["Version"], "0.0.0-dev", StringComparison.Ordinal) ||
                    !string.Equals(evaluated["AssemblyVersion"], "0.0.0.0", StringComparison.Ordinal) ||
                    !string.Equals(evaluated["FileVersion"], "0.0.0.0", StringComparison.Ordinal) ||
                    !string.Equals(evaluated["InformationalVersion"], "0.0.0-dev+architecture-evaluation", StringComparison.Ordinal) ||
                    !string.Equals(evaluated["IncludeSourceRevisionInInformationalVersion"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(evaluated["UseSharedCompilation"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrEmpty(evaluated["ErrorLog"]) ||
                    !string.IsNullOrEmpty(evaluated["DocumentationFile"]) ||
                    !string.Equals(evaluated["GenerateDocumentationFile"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(evaluated["EmitCompilerGeneratedFiles"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrEmpty(evaluated["CompilerGeneratedFilesOutputPath"]) ||
                    !string.IsNullOrEmpty(evaluated["PdbFile"]) ||
                    !string.IsNullOrEmpty(evaluated["PreBuildEvent"]) ||
                    !string.IsNullOrEmpty(evaluated["PostBuildEvent"]) ||
                    !string.Equals(evaluated["RunPostBuildEvent"], "Never", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(evaluated["GeneratePackageOnBuild"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(evaluated["DeployOnBuild"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrEmpty(evaluated["RestoreGraphOutputPath"]) ||
                    !string.IsNullOrEmpty(evaluated["CscToolPath"]) ||
                    !string.IsNullOrEmpty(evaluated["CscToolExe"]) ||
                    !string.Equals(evaluated["MSBuildRuntimeType"], "Core", StringComparison.Ordinal) ||
                    !string.Equals(
                        evaluated["NETCoreSdkVersion"],
                        Path.GetFileName(facts.LockedSdkRoot.TrimEnd(Path.DirectorySeparatorChar)),
                        StringComparison.Ordinal) ||
                    !string.Equals(evaluated["ImportDirectoryPackagesProps"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrEmpty(evaluated["DirectoryPackagesPropsPath"]))
                {
                    failures.Add(project.RelativePath + " effective deterministic, compiler, or package-import identity is not approved.");
                }

                string sdkRoot = facts.LockedSdkRoot;
                string targetingPackRoot = facts.TargetingPackRoot;
                foreach (var expectedToolPath in new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "RoslynTargetsPath", Path.Combine(sdkRoot, "Roslyn") },
                    { "FrameworkPathOverride", targetingPackRoot },
                    { "MSBuildSDKsPath", Path.Combine(sdkRoot, "Sdks") },
                    { "MSBuildExtensionsPath", sdkRoot },
                    { "MSBuildExtensionsPath32", sdkRoot },
                    { "MSBuildExtensionsPath64", sdkRoot },
                    { "MSBuildUserExtensionsPath", sdkRoot },
                    { "MSBuildToolsPath", sdkRoot },
                    { "MSBuildBinPath", sdkRoot },
                    { "CSharpCoreTargetsPath", Path.Combine(sdkRoot, "Roslyn", "Microsoft.CSharp.Core.targets") }
                })
                {
                    if (!PathsEqual(evaluated[expectedToolPath.Key], expectedToolPath.Value))
                    {
                        failures.Add(project.RelativePath + " effective toolchain path is not locked: " + expectedToolPath.Key);
                    }
                }

                foreach (string wildcardProperty in new[]
                {
                    "ImportByWildcardBeforeMicrosoftCommonProps",
                    "ImportByWildcardAfterMicrosoftCommonProps",
                    "ImportUserLocationsByWildcardBeforeMicrosoftCommonProps",
                    "ImportUserLocationsByWildcardAfterMicrosoftCommonProps",
                    "ImportByWildcardBeforeMicrosoftCommonTargets",
                    "ImportByWildcardAfterMicrosoftCommonTargets",
                    "ImportUserLocationsByWildcardBeforeMicrosoftCommonTargets",
                    "ImportUserLocationsByWildcardAfterMicrosoftCommonTargets",
                    "ImportByWildcardBeforeMicrosoftCSharpTargets",
                    "ImportByWildcardAfterMicrosoftCSharpTargets",
                    "ImportUserLocationsByWildcardBeforeMicrosoftCSharpTargets",
                    "ImportUserLocationsByWildcardAfterMicrosoftCSharpTargets",
                    "ImportByWildcardBeforeMicrosoftNetFrameworkProps",
                    "ImportByWildcardAfterMicrosoftNetFrameworkProps",
                    "ImportUserLocationsByWildcardBeforeMicrosoftNetFrameworkProps",
                    "ImportUserLocationsByWildcardAfterMicrosoftNetFrameworkProps",
                    "ImportByWildcardBeforeMicrosoftNetFrameworkTargets",
                    "ImportByWildcardAfterMicrosoftNetFrameworkTargets",
                    "ImportUserLocationsByWildcardBeforeMicrosoftNetFrameworkTargets",
                    "ImportUserLocationsByWildcardAfterMicrosoftNetFrameworkTargets",
                    "ImportByWildcardBeforeMicrosoftCommonCrossTargetingTargets",
                    "ImportByWildcardAfterMicrosoftCommonCrossTargetingTargets",
                    "ImportByWildcardBeforeMicrosoftVisualBasicTargets",
                    "ImportByWildcardAfterMicrosoftVisualBasicTargets",
                    "ImportUserLocationsByWildcardBeforeMicrosoftVisualBasicTargets",
                    "ImportUserLocationsByWildcardAfterMicrosoftVisualBasicTargets"
                })
                {
                    if (!string.Equals(evaluated[wildcardProperty], "false", StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add(project.RelativePath + " effective wildcard import is not disabled: " + wildcardProperty);
                    }
                }

                string expectedBaseOutput = Path.Combine(evaluationRoot, "bin", expectedName);
                string expectedOutput = Path.Combine(expectedBaseOutput, "x86", facts.Configuration, "net472");
                string expectedBaseIntermediate = Path.Combine(evaluationRoot, "obj", expectedName);
                string expectedIntermediate = Path.Combine(expectedBaseIntermediate, "x86", facts.Configuration, "net472");
                if (!PathsEqual(project.EvaluatedBaseOutputPath, expectedBaseOutput) ||
                    !PathsEqual(project.EvaluatedOutputPath, expectedOutput) ||
                    !PathsEqual(evaluated["OutDir"], expectedOutput) ||
                    !PathsEqual(project.EvaluatedBaseIntermediateOutputPath, expectedBaseIntermediate) ||
                    !PathsEqual(project.EvaluatedIntermediateOutputPath, expectedIntermediate) ||
                    !PathsEqual(project.EvaluatedProjectExtensionsPath, expectedBaseIntermediate))
                {
                    failures.Add(project.RelativePath + " effective MSBuild output paths must remain inside the guarded evaluation root.");
                }
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
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
                .Where(project => project.AssemblyReferences.Count != 0)
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
            string buildTargets = Path.Combine(facts.RepositoryRoot, "Directory.Build.targets");
            if (!File.Exists(buildScript))
            {
                failures.Add("formal build entry is missing: scripts/build.ps1");
            }

            if (!File.Exists(baseline))
            {
                failures.Add("reference baseline is missing: eng/TerrariaReferences.baseline.json");
            }

            if (!File.Exists(buildTargets))
            {
                failures.Add("root MSBuild target import boundary is missing: Directory.Build.targets");
            }
        }

        private static string GetSimpleReferenceName(AssemblyReferenceFacts reference)
        {
            int comma = reference.Include.IndexOf(',');
            return (comma < 0 ? reference.Include : reference.Include.Substring(0, comma)).Trim();
        }

        private static bool IsForbiddenBinaryFileName(string fileName)
        {
            return string.Equals(fileName, "Terraria.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "ReLogic.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase) &&
                    fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "0Harmony.dll", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsForbiddenAssemblyName(string name)
        {
            return string.Equals(name, "Terraria", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "ReLogic", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "0Harmony", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "TerrariaHelper", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("TerrariaHelper.", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "JueMingZ", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("JueMingZ.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsForbiddenProjectName(string text)
        {
            return text != null &&
                (text.IndexOf("JueMingZ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 text.IndexOf("TerrariaHelper", StringComparison.OrdinalIgnoreCase) >= 0);
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
