using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

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
            CheckFormalJsonEncodingBoundary(facts, failures);
            CheckFinalBuildRecordSourceGate(facts, failures);
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
                    !string.Equals(evaluated["DeterministicSourcePaths"], "false", StringComparison.OrdinalIgnoreCase) ||
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
                    !string.Equals(evaluated["EnableSourceControlManagerQueries"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(evaluated["EnableSourceLink"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(evaluated["EmbedUntrackedSources"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(evaluated["PublishRepositoryUrl"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(evaluated["GenerateRepositoryUrlAttribute"], "false", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrEmpty(evaluated["RepositoryUrl"]) ||
                    !string.IsNullOrEmpty(evaluated["PrivateRepositoryUrl"]) ||
                    !string.IsNullOrEmpty(evaluated["ScmRepositoryUrl"]) ||
                    !string.IsNullOrEmpty(evaluated["SourceLink"]) ||
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

        private static void CheckFormalJsonEncodingBoundary(ProjectGraphFacts facts, IList<string> failures)
        {
            foreach (string relativePath in new[]
            {
                "scripts/build.ps1",
                "scripts/prepare-terraria-references.ps1",
                "scripts/verify-reproducible-build.ps1"
            })
            {
                string scriptPath = Path.Combine(
                    facts.RepositoryRoot,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(scriptPath))
                {
                    failures.Add("formal JSON reader script is missing: " + relativePath);
                    continue;
                }

                string script = File.ReadAllText(scriptPath);
                if (script.IndexOf("Read-StrictUtf8Json -Path", StringComparison.Ordinal) < 0 ||
                    script.IndexOf("[System.IO.File]::ReadAllBytes(", StringComparison.Ordinal) < 0)
                {
                    failures.Add(relativePath + " must route persisted JSON through its byte-level UTF-8 reader.");
                }

                if (script.IndexOf("-Raw | ConvertFrom-Json", StringComparison.Ordinal) >= 0)
                {
                    failures.Add(relativePath + " must not decode persisted JSON through the Windows PowerShell ANSI default.");
                }

                ProbeFormalJsonEncodingBoundary(facts, scriptPath, relativePath, failures);
            }
        }

        private static void ProbeFormalJsonEncodingBoundary(
            ProjectGraphFacts facts,
            string scriptPath,
            string relativePath,
            IList<string> failures)
        {
            string probeRoot = Path.Combine(
                facts.OwnedToolStateRoot,
                "architecture-tests",
                "json-encoding-probe-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(probeRoot);
            try
            {
                string goodPath = Path.Combine(probeRoot, "good.json");
                string invalidPath = Path.Combine(probeRoot, "invalid.json");
                string utf16Path = Path.Combine(probeRoot, "utf16.json");
                string probePath = Path.Combine(probeRoot, "probe.ps1");
                var strictUtf8 = new UTF8Encoding(false, true);
                File.WriteAllBytes(goodPath, strictUtf8.GetBytes("{\"value\":\"路径\"}"));
                File.WriteAllBytes(invalidPath, new byte[]
                {
                    0x7B, 0x22, 0x76, 0x61, 0x6C, 0x75, 0x65, 0x22, 0x3A, 0x22,
                    0xC3, 0x28,
                    0x22, 0x7D
                });
                var utf16 = new UnicodeEncoding(false, true, true);
                byte[] utf16Body = utf16.GetBytes("{\"value\":\"x\"}");
                File.WriteAllBytes(utf16Path, utf16.GetPreamble().Concat(utf16Body).ToArray());
                File.WriteAllText(probePath, GetFormalJsonEncodingProbeScript(), new UTF8Encoding(false));

                string systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? string.Empty;
                string powerShellPath = Path.Combine(
                    systemRoot,
                    "System32",
                    "WindowsPowerShell",
                    "v1.0",
                    "powershell.exe");
                string arguments = string.Join(" ", new[]
                {
                    "-NoLogo",
                    "-NoProfile",
                    "-NonInteractive",
                    "-ExecutionPolicy Bypass",
                    "-File " + QuoteProcessArgument(probePath),
                    "-TargetScript " + QuoteProcessArgument(scriptPath),
                    "-GoodPath " + QuoteProcessArgument(goodPath),
                    "-InvalidPath " + QuoteProcessArgument(invalidPath),
                    "-Utf16Path " + QuoteProcessArgument(utf16Path)
                });
                ProcessStartInfo startInfo = ProjectGraphFacts.CreateIsolatedProcessStartInfo(
                    powerShellPath,
                    arguments,
                    facts.RepositoryRoot,
                    probeRoot,
                    new string[0]);
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        failures.Add(relativePath + " strict UTF-8 behavior probe could not start.");
                        return;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        failures.Add(
                            relativePath + " strict UTF-8 behavior probe failed (exit " +
                            process.ExitCode + "): " + (error + " " + output).Trim());
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Add(relativePath + " strict UTF-8 behavior probe failed: " + exception.Message);
            }
            finally
            {
                if (Directory.Exists(probeRoot))
                {
                    Directory.Delete(probeRoot, true);
                }
            }
        }

        private static string QuoteProcessArgument(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string GetFormalJsonEncodingProbeScript()
        {
            return string.Join("\r\n", new[]
            {
                "param([string] $TargetScript, [string] $GoodPath, [string] $InvalidPath, [string] $Utf16Path)",
                "$ErrorActionPreference = 'Stop'",
                "$tokens = $null",
                "$errors = $null",
                "$ast = [System.Management.Automation.Language.Parser]::ParseFile($TargetScript, [ref] $tokens, [ref] $errors)",
                "if ($errors.Count -ne 0) { throw 'target script parser errors' }",
                "$functions = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ieq 'Read-StrictUtf8Json' }, $true))",
                "$assignments = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -ieq '$script:StrictUtf8' }, $true))",
                "if ($functions.Count -ne 1 -or $assignments.Count -ne 1) { throw 'strict UTF-8 reader definition is not unique' }",
                "$readerCommands = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ieq 'Read-StrictUtf8Json' }, $true))",
                "$leaf = [System.IO.Path]::GetFileName($TargetScript).ToLowerInvariant()",
                "$expectedCalls = switch ($leaf) {",
                "    'build.ps1' { @('Read-StrictUtf8Json -Path $baselinePath', 'Read-StrictUtf8Json -Path $globalJsonPath', 'Read-StrictUtf8Json -Path $markerPath') }",
                "    'prepare-terraria-references.ps1' { @('Read-StrictUtf8Json -Path $markerPath', 'Read-StrictUtf8Json -Path $markerPath', 'Read-StrictUtf8Json -Path $script:BaselinePath') }",
                "    'verify-reproducible-build.ps1' { @('Read-StrictUtf8Json -Path $baselinePath', 'Read-StrictUtf8Json -Path $markerPath', 'Read-StrictUtf8Json -Path $recordPath') }",
                "    default { throw 'unexpected formal script' }",
                "}",
                "$actualCalls = @($readerCommands | ForEach-Object { $_.Extent.Text.Trim() } | Sort-Object)",
                "$expectedCalls = @($expectedCalls | Sort-Object)",
                "if (($actualCalls -join [char]10) -cne ($expectedCalls -join [char]10)) { throw 'persisted JSON call sites are not exclusively bound to the strict reader' }",
                "$jsonCommands = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] -and $null -ne $node.GetCommandName() -and $node.GetCommandName().Split([char]92)[-1] -ieq 'ConvertFrom-Json' }, $true))",
                "$jsonSignatures = @($jsonCommands | ForEach-Object {",
                "    $parent = $_.Parent",
                "    while ($null -ne $parent -and -not ($parent -is [System.Management.Automation.Language.FunctionDefinitionAst])) { $parent = $parent.Parent }",
                "    if ($null -eq $parent) { throw 'ConvertFrom-Json is outside an approved function' }",
                "    $parent.Name + '|' + $_.Parent.Extent.Text.Trim()",
                "} | Sort-Object)",
                "$expectedJsonSignatures = if ($leaf -eq 'build.ps1') {",
                "    @('Get-EvaluatedProjectBuildFacts|($output -join [Environment]::NewLine) | ConvertFrom-Json', 'Read-StrictUtf8Json|ConvertFrom-Json -InputObject $text')",
                "} else {",
                "    @('Read-StrictUtf8Json|ConvertFrom-Json -InputObject $text')",
                "}",
                "$expectedJsonSignatures = @($expectedJsonSignatures | Sort-Object)",
                "if (($jsonSignatures -join [char]10) -cne ($expectedJsonSignatures -join [char]10)) { throw 'ConvertFrom-Json AST allowlist was bypassed' }",
                "Invoke-Expression $assignments[0].Extent.Text",
                "Invoke-Expression $functions[0].Extent.Text",
                "$expected = [string]([char]0x8DEF) + [string]([char]0x5F84)",
                "$good = Read-StrictUtf8Json -Path $GoodPath",
                "if ([string] $good.value -cne $expected) { throw 'UTF-8 no-BOM Unicode round-trip failed' }",
                "foreach ($badPath in @($InvalidPath, $Utf16Path)) {",
                "    $accepted = $false",
                "    try { $null = Read-StrictUtf8Json -Path $badPath; $accepted = $true } catch { }",
                "    if ($accepted) { throw 'non-UTF-8 input was accepted' }",
                "}",
                "if ($leaf -ne 'prepare-terraria-references.ps1') {",
                "    $restoreFunctions = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ieq 'Restore-EnvironmentSnapshot' }, $true))",
                "    if ($restoreFunctions.Count -ne 1) { throw 'Git environment restore definition is not unique' }",
                "    Invoke-Expression $restoreFunctions[0].Extent.Text",
                "    Get-ChildItem Env: | Where-Object { $_.Name.StartsWith('GIT_', [StringComparison]::OrdinalIgnoreCase) } | ForEach-Object { Remove-Item -LiteralPath ('Env:' + $_.Name) -ErrorAction Stop }",
                "    $snapshot = [ordered]@{",
                "        GIT_ORIGINAL = [ordered]@{ exists = $true; value = 'before' }",
                "        GIT_ABSENT = [ordered]@{ exists = $false; value = '' }",
                "    }",
                "    Set-Item -LiteralPath 'Env:GIT_ORIGINAL' -Value 'before'",
                "    Set-Item -LiteralPath 'Env:GIT_ABSENT' -Value 'locked'",
                "    Set-Item -LiteralPath 'Env:GIT_NEW_AFTER_SNAPSHOT' -Value 'injected'",
                "    Restore-EnvironmentSnapshot -Snapshot $snapshot",
                "    $restoredGit = @(Get-ChildItem Env: | Where-Object { $_.Name.StartsWith('GIT_', [StringComparison]::OrdinalIgnoreCase) })",
                "    if ($restoredGit.Count -ne 1 -or $restoredGit[0].Name -ine 'GIT_ORIGINAL' -or [string] $restoredGit[0].Value -cne 'before') { throw 'Git environment restore did not remove post-snapshot variables exactly' }",
                "}",
                "if ($leaf -eq 'build.ps1') {",
                "    function Test-ExecutableProbeNode([System.Management.Automation.Language.Ast] $Node) {",
                "        $parent = $Node.Parent",
                "        while ($null -ne $parent) {",
                "            if ($parent -is [System.Management.Automation.Language.FunctionDefinitionAst] -or",
                "                $parent -is [System.Management.Automation.Language.IfStatementAst] -or",
                "                $parent -is [System.Management.Automation.Language.LoopStatementAst] -or",
                "                $parent -is [System.Management.Automation.Language.SwitchStatementAst] -or",
                "                $parent -is [System.Management.Automation.Language.ScriptBlockExpressionAst] -or",
                "                $parent -is [System.Management.Automation.Language.CatchClauseAst] -or",
                "                $parent -is [System.Management.Automation.Language.TrapStatementAst] -or",
                "                $parent -is [System.Management.Automation.Language.SubExpressionAst] -or",
                "                $parent -is [System.Management.Automation.Language.BinaryExpressionAst]) { return $false }",
                "            $parent = $parent.Parent",
                "        }",
                "        return $true",
                "    }",
                "    function Test-MandatorySourceGateNode([System.Management.Automation.Language.Ast] $Node) {",
                "        if (-not (Test-ExecutableProbeNode $Node)) { return $false }",
                "        $parent = $Node.Parent",
                "        while ($null -ne $parent) {",
                "            if ($parent -is [System.Management.Automation.Language.SubExpressionAst] -or",
                "                $parent -is [System.Management.Automation.Language.BinaryExpressionAst] -or",
                "                ($parent -is [System.Management.Automation.Language.TryStatementAst] -and $parent.CatchClauses.Count -ne 0)) { return $false }",
                "            $parent = $parent.Parent",
                "        }",
                "        return $true",
                "    }",
                "    function Get-DirectBodyStatement([System.Management.Automation.Language.Ast] $Node, [System.Management.Automation.Language.StatementBlockAst] $Body) {",
                "        $current = $Node",
                "        while ($null -ne $current -and $current.Parent -ne $Body) { $current = $current.Parent }",
                "        return $current",
                "    }",
                "    $traps = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.TrapStatementAst] }, $true))",
                "    if ($traps.Count -ne 0) { throw 'formal build script may not contain trap statements' }",
                "    $shortCircuitTokens = $null",
                "    $shortCircuitErrors = $null",
                "    $shortCircuitText = '$null = $false -and $(Restore-EnvironmentSnapshot -Snapshot $x)' + [Environment]::NewLine + '$null = $true -or $([System.IO.File]::Move(''a'', ''b''))'",
                "    $shortCircuitAst = [System.Management.Automation.Language.Parser]::ParseInput($shortCircuitText, [ref] $shortCircuitTokens, [ref] $shortCircuitErrors)",
                "    if ($shortCircuitErrors.Count -ne 0) { throw 'short-circuit control probe did not parse' }",
                "    $shortRestore = @($shortCircuitAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ieq 'Restore-EnvironmentSnapshot' }, $true))",
                "    $shortMove = @($shortCircuitAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -and $node.Member.Value -ieq 'Move' }, $true))",
                "    if ($shortRestore.Count -ne 1 -or $shortMove.Count -ne 1 -or (Test-ExecutableProbeNode $shortRestore[0]) -or (Test-ExecutableProbeNode $shortMove[0])) { throw 'short-circuit publication operation was treated as executable' }",
                "    $commands = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] }, $true))",
                "    $cleanup = @($commands | Where-Object { $_.GetCommandName() -ieq 'Remove-OwnedDotnetStateRoot' -and (Test-ExecutableProbeNode $_) } | Sort-Object { $_.Extent.StartOffset } | Select-Object -Last 1)",
                "    $open = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -and $node.Expression.Extent.Text -ieq '[System.IO.File]' -and $node.Member.Value -ieq 'Open' -and (Test-MandatorySourceGateNode $node) }, $true) | Sort-Object { $_.Extent.StartOffset } | Select-Object -Last 1)",
                "    $flush = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -and $node.Member.Value -ieq 'Flush' -and (Test-MandatorySourceGateNode $node) }, $true) | Sort-Object { $_.Extent.StartOffset } | Select-Object -Last 1)",
                "    $restore = @($commands | Where-Object { $_.GetCommandName() -ieq 'Restore-EnvironmentSnapshot' -and (Test-ExecutableProbeNode $_) } | Sort-Object { $_.Extent.StartOffset } | Select-Object -Last 1)",
                "    $move = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -and $node.Expression.Extent.Text -ieq '[System.IO.File]' -and $node.Member.Value -ieq 'Move' -and (Test-ExecutableProbeNode $node) }, $true) | Sort-Object { $_.Extent.StartOffset } | Select-Object -Last 1)",
                "    if ($cleanup.Count -ne 1 -or $open.Count -ne 1 -or $flush.Count -ne 1 -or $restore.Count -ne 1 -or $move.Count -ne 1 -or",
                "        $cleanup[0].Extent.StartOffset -ge $open[0].Extent.StartOffset -or",
                "        $open[0].Extent.StartOffset -ge $flush[0].Extent.StartOffset -or",
                "        $flush[0].Extent.StartOffset -ge $restore[0].Extent.StartOffset -or",
                "        $restore[0].Extent.StartOffset -ge $move[0].Extent.StartOffset -or",
                "        $open[0].Extent.Text -notmatch 'FileMode\\]::CreateNew' -or",
                "        $flush[0].Extent.Text -notmatch '\\$true') { throw 'build-record atomic publication AST contract is incomplete' }",
                "    $closingTry = $open[0].Parent",
                "    while ($null -ne $closingTry -and -not ($closingTry -is [System.Management.Automation.Language.TryStatementAst])) { $closingTry = $closingTry.Parent }",
                "    if ($null -eq $closingTry -or $closingTry.CatchClauses.Count -ne 0 -or $closingTry.Parent -ne $ast.EndBlock) { throw 'closing source gate must use one uncaught top-level try body' }",
                "    $openStatement = Get-DirectBodyStatement $open[0] $closingTry.Body",
                "    if (-not ($openStatement -is [System.Management.Automation.Language.AssignmentStatementAst]) -or $openStatement.Left.Extent.Text -cne '$recordStream') { throw 'record staging is not a direct closing-body assignment' }",
                "    $restoreBody = $restore[0].Parent",
                "    while ($null -ne $restoreBody -and -not ($restoreBody -is [System.Management.Automation.Language.StatementBlockAst])) { $restoreBody = $restoreBody.Parent }",
                "    $moveBody = $move[0].Parent",
                "    while ($null -ne $moveBody -and -not ($moveBody -is [System.Management.Automation.Language.StatementBlockAst])) { $moveBody = $moveBody.Parent }",
                "    if ($null -eq $restoreBody -or $null -eq $moveBody) { throw 'publication operation body was not found' }",
                "    $restoreStatement = Get-DirectBodyStatement $restore[0] $restoreBody",
                "    $moveStatement = Get-DirectBodyStatement $move[0] $moveBody",
                "    if (-not ($restoreStatement -is [System.Management.Automation.Language.PipelineAst]) -or $restoreStatement.Extent.Text.TrimStart().IndexOf('Restore-EnvironmentSnapshot', [StringComparison]::OrdinalIgnoreCase) -ne 0) { throw 'closing environment restore is not a direct pipeline' }",
                "    if (-not ($moveStatement -is [System.Management.Automation.Language.PipelineAst]) -or $moveStatement.Extent.Text.TrimStart().IndexOf('[System.IO.File]::Move', [StringComparison]::OrdinalIgnoreCase) -ne 0) { throw 'record publication is not a direct pipeline' }",
                "    $restoreTry = $restoreBody.Parent",
                "    $moveTry = $moveBody.Parent",
                "    if (-not ($restoreTry -is [System.Management.Automation.Language.TryStatementAst]) -or $restoreTry.CatchClauses.Count -ne 1 -or $restoreTry.Parent -ne $closingTry.Finally) { throw 'closing environment restore is not guarded by the approved finally structure' }",
                "    $restoreCatchStatements = @($restoreTry.CatchClauses[0].Body.Statements)",
                "    if ($restoreCatchStatements.Count -ne 1 -or -not ($restoreCatchStatements[0] -is [System.Management.Automation.Language.AssignmentStatementAst]) -or $restoreCatchStatements[0].Left.Extent.Text -cne '$closingEnvironmentRestoreError' -or $restoreCatchStatements[0].Right.Extent.Text -cne '$_') { throw 'closing environment restore failure is not captured exactly' }",
                "    $restoreFailureGuards = @($closingTry.Finally.Statements | Where-Object { $_ -is [System.Management.Automation.Language.IfStatementAst] -and $_.Extent.Text.TrimStart().StartsWith('if ($null -ne $closingEnvironmentRestoreError)', [StringComparison]::Ordinal) -and @($_.FindAll({ param($node) $node -is [System.Management.Automation.Language.ThrowStatementAst] }, $true)).Count -ne 0 })",
                "    if ($restoreFailureGuards.Count -ne 1) { throw 'closing environment restore failure is not propagated before publication' }",
                "    if (-not ($moveTry -is [System.Management.Automation.Language.TryStatementAst]) -or $moveTry.CatchClauses.Count -ne 1 -or $moveTry.Parent -ne $ast.EndBlock) { throw 'record publication is not guarded by one top-level propagating catch' }",
                "    $moveCatchStatements = @($moveTry.CatchClauses[0].Body.Statements)",
                "    if ($moveCatchStatements.Count -eq 0 -or -not ($moveCatchStatements[$moveCatchStatements.Count - 1] -is [System.Management.Automation.Language.ThrowStatementAst])) { throw 'record publication failure is not rethrown' }",
                "    foreach ($requiredName in @('Assert-PhysicalGitBindingUnchanged', 'Get-GitRecordedSourcePaths', 'Assert-NoIgnoredFormalInputFiles', 'Assert-GitIndexAndAttributesSafe', 'Get-RawRepositoryContentInventory', 'Get-GitSourceIdentity')) {",
                "        $matches = @($commands | Where-Object { $_.GetCommandName() -ieq $requiredName -and (Test-MandatorySourceGateNode $_) -and $_.Extent.StartOffset -gt $cleanup[0].Extent.StartOffset -and $_.Extent.StartOffset -lt $open[0].Extent.StartOffset })",
                "        if ($matches.Count -ne 1) { throw ('closing source gate AST is missing or ambiguous: ' + $requiredName) }",
                "        $statement = Get-DirectBodyStatement $matches[0] $closingTry.Body",
                "        if ($null -eq $statement) { throw ('closing source gate is not a direct statement: ' + $requiredName) }",
                "        if ($requiredName.StartsWith('Assert-', [StringComparison]::Ordinal)) {",
                "            if (-not ($statement -is [System.Management.Automation.Language.PipelineAst]) -or $statement.Extent.Text.TrimStart().IndexOf($requiredName, [StringComparison]::OrdinalIgnoreCase) -ne 0) { throw ('closing assertion is not a direct pipeline: ' + $requiredName) }",
                "        } else {",
                "            $expectedLeft = switch ($requiredName) { 'Get-GitRecordedSourcePaths' { '$closingRecordedSourcePaths' } 'Get-RawRepositoryContentInventory' { '$closingSourceContent' } 'Get-GitSourceIdentity' { '$closingSourceIdentity' } }",
                "            if (-not ($statement -is [System.Management.Automation.Language.AssignmentStatementAst]) -or $statement.Left.Extent.Text -cne $expectedLeft) { throw ('closing source fact is not assigned directly: ' + $requiredName) }",
                "        }",
                "    }",
                "}",
                "exit 0",
                string.Empty
            });
        }

        private static void CheckFinalBuildRecordSourceGate(ProjectGraphFacts facts, IList<string> failures)
        {
            string buildScriptPath = Path.Combine(facts.RepositoryRoot, "scripts", "build.ps1");
            if (!File.Exists(buildScriptPath))
            {
                return;
            }

            string script = File.ReadAllText(buildScriptPath);
            int cleanupIndex = script.LastIndexOf(
                "Remove-OwnedDotnetStateRoot -ScratchRoot",
                StringComparison.Ordinal);
            int closingEnvironmentIndex = script.LastIndexOf(
                "$closingGitEnvironment = Set-LockedGitEnvironment",
                StringComparison.Ordinal);
            int recordStageIndex = script.LastIndexOf(
                "[System.IO.File]::Open(",
                StringComparison.Ordinal);
            int closingEnvironmentRestoreIndex = script.LastIndexOf(
                "Restore-EnvironmentSnapshot -Snapshot $closingGitEnvironment",
                StringComparison.Ordinal);
            int recordPublishIndex = script.LastIndexOf(
                "[System.IO.File]::Move($recordTemporaryPath, $completedRecordPath)",
                StringComparison.Ordinal);
            if (cleanupIndex < 0 ||
                closingEnvironmentIndex <= cleanupIndex ||
                recordStageIndex <= closingEnvironmentIndex ||
                closingEnvironmentRestoreIndex <= recordStageIndex ||
                recordPublishIndex <= closingEnvironmentRestoreIndex)
            {
                failures.Add("scripts/build.ps1 must stage and flush the build record after the closing source gate, restore the Git environment, and only then atomically publish it.");
                return;
            }

            string closingGate = script.Substring(
                closingEnvironmentIndex,
                recordStageIndex - closingEnvironmentIndex);
            foreach (string requiredOperation in new[]
            {
                "Assert-PhysicalGitBindingUnchanged",
                "Get-GitRecordedSourcePaths",
                "Assert-NoIgnoredFormalInputFiles",
                "Assert-GitIndexAndAttributesSafe",
                "Get-RawRepositoryContentInventory",
                "Get-GitSourceIdentity"
            })
            {
                if (closingGate.IndexOf(requiredOperation, StringComparison.Ordinal) < 0)
                {
                    failures.Add(
                        "scripts/build.ps1 final build-record source gate is missing " +
                        requiredOperation + ".");
                }
            }

            string stagedPublication = script.Substring(
                recordStageIndex,
                recordPublishIndex - recordStageIndex);
            if (stagedPublication.IndexOf("[System.IO.FileMode]::CreateNew", StringComparison.Ordinal) < 0 ||
                stagedPublication.IndexOf(".Flush($true)", StringComparison.Ordinal) < 0 ||
                stagedPublication.IndexOf("Restore-EnvironmentSnapshot -Snapshot $closingGitEnvironment", StringComparison.Ordinal) < 0)
            {
                failures.Add("scripts/build.ps1 must CreateNew and durably flush a non-final record before restoring the closing Git environment.");
            }

            if (script.IndexOf("[System.IO.File]::WriteAllBytes($completedRecordPath", StringComparison.Ordinal) >= 0)
            {
                failures.Add("scripts/build.ps1 must not create or truncate the formal build-record path before atomic publication.");
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
