using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JueMingR.ArchitectureTests
{
    internal sealed class ProjectGraphFacts
    {
        private ProjectGraphFacts(
            string repositoryRoot,
            string configuration,
            string lockedSdkRoot,
            string targetingPackRoot,
            string ownedToolStateRoot,
            IDictionary<string, ProjectFacts> projects,
            IList<string> solutionProjects,
            IList<string> discoveredProjects,
            IList<string> trackedFiles,
            ISet<string> forbiddenBinaryHashes,
            IList<string> sharedBuildItemViolations)
        {
            RepositoryRoot = repositoryRoot;
            Configuration = configuration;
            LockedSdkRoot = lockedSdkRoot;
            TargetingPackRoot = targetingPackRoot;
            OwnedToolStateRoot = ownedToolStateRoot;
            Projects = projects;
            SolutionProjects = solutionProjects;
            DiscoveredProjects = discoveredProjects;
            TrackedFiles = trackedFiles;
            ForbiddenBinaryHashes = forbiddenBinaryHashes;
            SharedBuildItemViolations = sharedBuildItemViolations;
        }

        internal string RepositoryRoot { get; private set; }

        internal string Configuration { get; private set; }

        internal string LockedSdkRoot { get; private set; }

        internal string TargetingPackRoot { get; private set; }

        internal string OwnedToolStateRoot { get; private set; }

        internal IDictionary<string, ProjectFacts> Projects { get; private set; }

        internal IList<string> SolutionProjects { get; private set; }

        internal IList<string> DiscoveredProjects { get; private set; }

        internal IList<string> TrackedFiles { get; private set; }

        internal ISet<string> ForbiddenBinaryHashes { get; private set; }

        internal IList<string> SharedBuildItemViolations { get; private set; }

        internal static ProjectGraphFacts Load(
            string repositoryRoot,
            string configuration,
            string lockedSdkRoot,
            string targetingPackRoot,
            string ownedToolStateRoot)
        {
            AssertFormalEnvironmentIsClosed();
            string root = Path.GetFullPath(repositoryRoot);
            string toolStateRoot = Path.GetFullPath(ownedToolStateRoot);
            if (!Directory.Exists(toolStateRoot) ||
                (File.GetAttributes(toolStateRoot) & FileAttributes.ReparsePoint) != 0 ||
                PathsOverlap(root, toolStateRoot))
            {
                throw new InvalidOperationException(
                    "The formal ArchitectureTests tool-state root must exist, be non-reparse, and remain outside the repository.");
            }
            Tuple<string, string> lockedRoots = ProjectFacts.ResolveLockedBuildRoots(
                root,
                lockedSdkRoot,
                targetingPackRoot);
            var discovered = new List<string>();
            CollectProjects(root, root, discovered);
            discovered.Sort(StringComparer.OrdinalIgnoreCase);

            var projects = new Dictionary<string, ProjectFacts>(StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in discovered)
            {
                projects.Add(relativePath, ProjectFacts.Load(
                    root,
                    relativePath,
                    configuration,
                    lockedRoots.Item1,
                    lockedRoots.Item2,
                    toolStateRoot));
            }

            var sharedBuildViolations = new List<string>(ReadSharedBuildItemViolations(root));
            sharedBuildViolations.AddRange(ReadNestedBuildFileViolations(root, discovered));

            return new ProjectGraphFacts(
                root,
                configuration,
                lockedRoots.Item1,
                lockedRoots.Item2,
                toolStateRoot,
                projects,
                ReadSolutionProjects(root),
                discovered,
                ReadTrackedFiles(root, toolStateRoot),
                ReadForbiddenBinaryHashes(root),
                sharedBuildViolations);
        }

        internal static string NormalizeRelativePath(string root, string path)
        {
            string absolutePath = Path.GetFullPath(path);
            string rootWithSeparator = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            Uri rootUri = new Uri(rootWithSeparator, UriKind.Absolute);
            Uri pathUri = new Uri(absolutePath, UriKind.Absolute);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString())
                .Replace('\\', '/');
        }

        private static bool PathsOverlap(string first, string second)
        {
            string firstRoot = Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar);
            string secondRoot = Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar);
            return string.Equals(firstRoot, secondRoot, StringComparison.OrdinalIgnoreCase) ||
                firstRoot.StartsWith(secondRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                secondRoot.StartsWith(firstRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static readonly string[] BaseEnvironmentNames =
        {
            "SystemRoot", "WINDIR", "ComSpec", "PATH", "PATHEXT", "PSModulePath", "ProgramFiles",
            "ProgramFiles(x86)", "ProgramData", "USERPROFILE", "HOME", "APPDATA", "LOCALAPPDATA",
            "TEMP", "TMP"
        };

        private static readonly string[] DotnetEnvironmentNames =
        {
            "DOTNET_ROOT", "DOTNET_MULTILEVEL_LOOKUP", "DOTNET_CLI_HOME",
            "DOTNET_CLI_TELEMETRY_OPTOUT", "DOTNET_NOLOGO", "DOTNET_SKIP_FIRST_TIME_EXPERIENCE",
            "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE", "NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH",
            "NUGET_SCRATCH", "NUGET_PLUGINS_CACHE_PATH", "MSBuildSDKsPath", "MSBuildExtensionsPath",
            "MSBuildExtensionsPath32", "MSBuildExtensionsPath64", "MSBuildUserExtensionsPath"
        };

        private static readonly string[] GitEnvironmentNames =
        {
            "GIT_CONFIG_NOSYSTEM", "GIT_CONFIG_GLOBAL", "GIT_OPTIONAL_LOCKS", "GIT_TERMINAL_PROMPT",
            "GIT_CONFIG_COUNT", "GIT_CONFIG_KEY_0", "GIT_CONFIG_VALUE_0", "GIT_CONFIG_KEY_1",
            "GIT_CONFIG_VALUE_1"
        };

        private static void AssertFormalEnvironmentIsClosed()
        {
            var expected = new HashSet<string>(
                BaseEnvironmentNames.Concat(DotnetEnvironmentNames).Concat(GitEnvironmentNames),
                StringComparer.OrdinalIgnoreCase);
            var actual = new HashSet<string>(
                Environment.GetEnvironmentVariables().Keys.Cast<string>(),
                StringComparer.OrdinalIgnoreCase);
            if (!expected.SetEquals(actual))
            {
                throw new InvalidOperationException(
                    "ArchitectureTests must inherit the exact closed-allowlist-v1 formal build environment.");
            }
        }

        internal static ProcessStartInfo CreateIsolatedProcessStartInfo(
            string executablePath,
            string arguments,
            string workingDirectory,
            string processTemp,
            IEnumerable<string> toolEnvironmentNames)
        {
            string fullExecutablePath = Path.GetFullPath(executablePath);
            if (!Path.IsPathRooted(executablePath) ||
                !File.Exists(fullExecutablePath) ||
                (File.GetAttributes(fullExecutablePath) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("An isolated child executable must be an absolute, regular file.");
            }
            if (!Directory.Exists(processTemp) ||
                (File.GetAttributes(processTemp) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("The isolated child TEMP directory is unavailable or unsafe.");
            }

            string[] allowedNames = BaseEnvironmentNames.Concat(toolEnvironmentNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in allowedNames)
            {
                string value = Environment.GetEnvironmentVariable(name);
                if (value == null)
                {
                    throw new InvalidOperationException("The formal environment is missing an approved variable: " + name);
                }
                values.Add(name, value);
            }
            values["TEMP"] = processTemp;
            values["TMP"] = processTemp;

            var startInfo = new ProcessStartInfo
            {
                FileName = fullExecutablePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            startInfo.EnvironmentVariables.Clear();
            foreach (KeyValuePair<string, string> value in values)
            {
                startInfo.EnvironmentVariables[value.Key] = value.Value;
            }
            return startInfo;
        }

        internal static IEnumerable<string> GetDotnetEnvironmentNames()
        {
            return DotnetEnvironmentNames;
        }

        private static string ResolveGitExecutableFromLockedPath()
        {
            string lockedPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string firstDirectory = lockedPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            string candidate = string.IsNullOrWhiteSpace(firstDirectory)
                ? string.Empty
                : Path.GetFullPath(Path.Combine(firstDirectory, "git.exe"));
            if (string.IsNullOrEmpty(candidate) ||
                !File.Exists(candidate) ||
                (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "The first locked PATH directory must contain the formal regular git.exe.");
            }
            return candidate;
        }

        private static void CollectProjects(string root, string directory, IList<string> destination)
        {
            string directoryName = new DirectoryInfo(directory).Name;
            string[] excludedNames = { ".git", ".local", ".vs", "artifacts", "bin", "external", "obj", "outputs" };
            if (!string.Equals(directory, root, StringComparison.OrdinalIgnoreCase) &&
                excludedNames.Contains(directoryName, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (string path in Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly))
            {
                destination.Add(NormalizeRelativePath(root, path));
            }

            foreach (string child in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                CollectProjects(root, child, destination);
            }
        }

        private static IList<string> ReadSolutionProjects(string root)
        {
            string solutionPath = Path.Combine(root, "JueMingR.sln");
            var projects = new List<string>();
            if (!File.Exists(solutionPath))
            {
                return projects;
            }

            var pattern = new Regex(
                "^Project\\(\"\\{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC\\}\"\\) = \"[^\"]+\", \"([^\"]+\\.csproj)\", \"\\{[0-9A-Fa-f-]+\\}\"$",
                RegexOptions.CultureInvariant);
            foreach (string line in File.ReadAllLines(solutionPath))
            {
                if (!line.StartsWith("Project(", StringComparison.Ordinal))
                {
                    continue;
                }

                Match match = pattern.Match(line);
                if (!match.Success)
                {
                    throw new InvalidOperationException(
                        "Only approved C# project entries are allowed in JueMingR.sln: " + line);
                }

                string absolute = Path.GetFullPath(Path.Combine(root, match.Groups[1].Value));
                projects.Add(NormalizeRelativePath(root, absolute));
            }

            projects.Sort(StringComparer.OrdinalIgnoreCase);
            return projects;
        }

        private static IList<string> ReadTrackedFiles(string root, string ownedToolStateRoot)
        {
            string processTemp = Path.Combine(ownedToolStateRoot, "architecture-tests", "temp");
            Directory.CreateDirectory(processTemp);
            ProcessStartInfo startInfo = CreateIsolatedProcessStartInfo(
                ResolveGitExecutableFromLockedPath(),
                "ls-files -z",
                root,
                processTemp,
                GitEnvironmentNames);

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Could not start git ls-files.");
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("git ls-files failed: " + error.Trim());
                }

                return output.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => path.Replace('\\', '/'))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        private static IList<string> ReadSharedBuildItemViolations(string root)
        {
            string[] fileNames = { "Directory.Build.props", "Directory.Build.targets" };
            string[] allowedPropsElements =
            {
                "LangVersion",
                "Deterministic",
                "ContinuousIntegrationBuild",
                "DeterministicSourcePaths",
                "DebugType",
                "DebugSymbols",
                "CodePage",
                "PathMap",
                "SourceRevisionId",
                "Version",
                "AssemblyVersion",
                "FileVersion",
                "InformationalVersion",
                "IncludeSourceRevisionInInformationalVersion",
                "TreatWarningsAsErrors",
                "CopyLocalLockFileAssemblies",
                "DefaultItemExcludes",
                "BaseOutputPath",
                "BaseIntermediateOutputPath"
            };
            var violations = new List<string>();
            foreach (string fileName in fileNames)
            {
                string path = Path.Combine(root, fileName);
                if (!File.Exists(path))
                {
                    violations.Add("missing root shared build file: " + fileName);
                    continue;
                }

                XDocument document = XDocument.Load(path, LoadOptions.None);
                XElement project = document.Root;
                if (project == null ||
                    project.Name.Namespace != XNamespace.None ||
                    !string.Equals(project.Name.LocalName, "Project", StringComparison.Ordinal) ||
                    project.HasAttributes)
                {
                    violations.Add(fileName + ": root must be an unnamespaced Project without attributes");
                    continue;
                }

                if (string.Equals(fileName, "Directory.Build.targets", StringComparison.OrdinalIgnoreCase))
                {
                    if (project.HasElements || !string.IsNullOrWhiteSpace(project.Value))
                    {
                        violations.Add(fileName + ": must remain an empty import boundary");
                    }

                    continue;
                }

                var allowedNames = new HashSet<string>(allowedPropsElements, StringComparer.Ordinal);
                allowedNames.Add("PropertyGroup");
                foreach (XElement element in project.Descendants())
                {
                    if (element.Name.Namespace != XNamespace.None || !allowedNames.Contains(element.Name.LocalName))
                    {
                        violations.Add(fileName + ": unapproved element " + element.Name.LocalName);
                    }

                    if (element.Value.Contains("$(["))
                    {
                        violations.Add(fileName + ": property functions are not allowed in " + element.Name.LocalName);
                    }

                    foreach (XAttribute attribute in element.Attributes())
                    {
                        bool allowedCondition =
                            string.Equals(attribute.Name.LocalName, "Condition", StringComparison.Ordinal) &&
                            ((string.Equals(element.Name.LocalName, "SourceRevisionId", StringComparison.Ordinal) &&
                              string.Equals(attribute.Value.Trim(), "'$(SourceRevisionId)' == ''", StringComparison.Ordinal)) ||
                             (string.Equals(element.Name.LocalName, "PropertyGroup", StringComparison.Ordinal) &&
                              string.Equals(attribute.Value.Trim(), "'$(JueMingRBuildRoot)' != ''", StringComparison.Ordinal)));
                        if (!allowedCondition || attribute.Value.Contains("$(["))
                        {
                            violations.Add(fileName + ": unapproved attribute on " + element.Name.LocalName);
                        }
                    }
                }

                IList<XElement> propertyGroups = project.Elements()
                    .Where(element => string.Equals(element.Name.LocalName, "PropertyGroup", StringComparison.Ordinal))
                    .ToList();
                XElement unconditionalGroup = propertyGroups.SingleOrDefault(group => !group.HasAttributes);
                XElement buildRootGroup = propertyGroups.SingleOrDefault(group =>
                    group.Attributes().Count() == 1 &&
                    string.Equals(group.Attributes().Single().Name.LocalName, "Condition", StringComparison.Ordinal) &&
                    string.Equals(
                        group.Attributes().Single().Value.Trim(),
                        "'$(JueMingRBuildRoot)' != ''",
                        StringComparison.Ordinal));
                if (propertyGroups.Count != 2 || unconditionalGroup == null || buildRootGroup == null)
                {
                    violations.Add(fileName + ": must contain exactly the approved unconditional and guarded property groups");
                }
                else
                {
                    var expectedUnconditionalProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { "LangVersion", "7.3" },
                        { "Deterministic", "true" },
                        { "ContinuousIntegrationBuild", "true" },
                        { "DeterministicSourcePaths", "true" },
                        { "DebugType", "portable" },
                        { "DebugSymbols", "true" },
                        { "CodePage", "65001" },
                        { "PathMap", "$(MSBuildProjectDirectory)=/_/$(MSBuildProjectName)" },
                        { "SourceRevisionId", "unidentified" },
                        { "Version", "0.0.0-dev" },
                        { "AssemblyVersion", "0.0.0.0" },
                        { "FileVersion", "0.0.0.0" },
                        { "InformationalVersion", "0.0.0-dev+$(SourceRevisionId)" },
                        { "IncludeSourceRevisionInInformationalVersion", "false" },
                        { "TreatWarningsAsErrors", "true" },
                        { "CopyLocalLockFileAssemblies", "false" },
                        { "DefaultItemExcludes", "$(DefaultItemExcludes);$(MSBuildProjectDirectory)\\bin\\**;$(MSBuildProjectDirectory)\\obj\\**" }
                    };
                    var expectedBuildRootProperties = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { "PathMap", "$(PathMap),$(JueMingRBuildRoot)=/_/build" },
                        { "BaseOutputPath", "$(JueMingRBuildRoot)\\bin\\$(MSBuildProjectName)\\" },
                        { "BaseIntermediateOutputPath", "$(JueMingRBuildRoot)\\obj\\$(MSBuildProjectName)\\" }
                    };
                    CheckExactSharedPropertyGroup(
                        fileName,
                        "unconditional",
                        unconditionalGroup,
                        expectedUnconditionalProperties,
                        true,
                        violations);
                    CheckExactSharedPropertyGroup(
                        fileName,
                        "build-root",
                        buildRootGroup,
                        expectedBuildRootProperties,
                        false,
                        violations);
                }

                foreach (var expectedOutputProperty in new[]
                {
                    new { Name = "BaseOutputPath", Value = "$(JueMingRBuildRoot)\\bin\\$(MSBuildProjectName)\\" },
                    new { Name = "BaseIntermediateOutputPath", Value = "$(JueMingRBuildRoot)\\obj\\$(MSBuildProjectName)\\" }
                })
                {
                    IList<XElement> matches = project.Descendants()
                        .Where(element => string.Equals(element.Name.LocalName, expectedOutputProperty.Name, StringComparison.Ordinal))
                        .ToList();
                    if (matches.Count != 1 || !string.Equals(matches[0].Value.Trim(), expectedOutputProperty.Value, StringComparison.Ordinal))
                    {
                        violations.Add(fileName + ": unapproved output routing " + expectedOutputProperty.Name);
                    }
                }
            }

            return violations;
        }

        private static void CheckExactSharedPropertyGroup(
            string fileName,
            string label,
            XElement group,
            IDictionary<string, string> expectedProperties,
            bool hasSourceRevisionCondition,
            IList<string> violations)
        {
            IList<XElement> children = group.Elements().ToList();
            if (children.Count != expectedProperties.Count)
            {
                violations.Add(fileName + ": unapproved " + label + " property count");
            }

            foreach (KeyValuePair<string, string> expected in expectedProperties)
            {
                IList<XElement> matches = children
                    .Where(element => string.Equals(element.Name.LocalName, expected.Key, StringComparison.Ordinal))
                    .ToList();
                if (matches.Count != 1 ||
                    !string.Equals(matches[0].Value.Trim(), expected.Value, StringComparison.Ordinal))
                {
                    violations.Add(fileName + ": unapproved " + label + " property " + expected.Key);
                    continue;
                }

                XElement property = matches[0];
                if (hasSourceRevisionCondition &&
                    string.Equals(expected.Key, "SourceRevisionId", StringComparison.Ordinal))
                {
                    if (property.Attributes().Count() != 1 ||
                        !string.Equals(property.Attributes().Single().Name.LocalName, "Condition", StringComparison.Ordinal) ||
                        !string.Equals(
                            property.Attributes().Single().Value.Trim(),
                            "'$(SourceRevisionId)' == ''",
                            StringComparison.Ordinal))
                    {
                        violations.Add(fileName + ": unapproved SourceRevisionId condition");
                    }
                }
                else if (property.HasAttributes)
                {
                    violations.Add(fileName + ": unapproved attributes on " + expected.Key);
                }
            }
        }

        private static IEnumerable<string> ReadNestedBuildFileViolations(
            string root,
            IEnumerable<string> projectPaths)
        {
            var checkedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var violations = new List<string>();
            foreach (string relativeProjectPath in projectPaths)
            {
                string current = Path.GetDirectoryName(Path.Combine(
                    root,
                    relativeProjectPath.Replace('/', Path.DirectorySeparatorChar)));
                while (!string.IsNullOrEmpty(current) &&
                    !string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
                {
                    if (checkedDirectories.Add(current))
                    {
                        foreach (string fileName in new[] { "Directory.Build.props", "Directory.Build.targets" })
                        {
                            string candidate = Path.Combine(current, fileName);
                            if (File.Exists(candidate))
                            {
                                violations.Add(
                                    "nested shared build file: " + NormalizeRelativePath(root, candidate));
                            }
                        }
                    }

                    current = Path.GetDirectoryName(current);
                }
            }

            return violations;
        }

        private static ISet<string> ReadForbiddenBinaryHashes(string root)
        {
            string baselinePath = Path.Combine(root, "eng", "TerrariaReferences.baseline.json");
            if (!File.Exists(baselinePath))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            MatchCollection matches = Regex.Matches(
                File.ReadAllText(baselinePath),
                "\\\"sha256\\\"\\s*:\\s*\\\"([0-9A-Fa-f]{64})\\\"",
                RegexOptions.CultureInvariant);
            return new HashSet<string>(
                matches.Cast<Match>().Select(match => match.Groups[1].Value),
                StringComparer.OrdinalIgnoreCase);
        }

        internal static string GetFileSha256(string path)
        {
            using (SHA256 algorithm = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }
    }

    internal sealed class ProjectFacts
    {
        private ProjectFacts()
        {
            ProjectReferences = new List<string>();
            AssemblyReferences = new List<AssemblyReferenceFacts>();
            PackageReferences = new List<string>();
            ExplicitImports = new List<string>();
            FactViolations = new List<string>();
        }

        internal string RelativePath { get; private set; }

        internal string ProjectSdk { get; private set; }

        internal string AssemblyName { get; private set; }

        internal string RootNamespace { get; private set; }

        internal string TargetFramework { get; private set; }

        internal string PlatformTarget { get; private set; }

        internal string OutputType { get; private set; }

        internal string GenerateAssemblyInfo { get; private set; }

        internal string AllowUnsafeBlocks { get; private set; }

        internal string TreatWarningsAsErrors { get; private set; }

        internal string EvaluatedTargetFramework { get; private set; }

        internal string EvaluatedPlatformTarget { get; private set; }

        internal string EvaluatedLangVersion { get; private set; }

        internal string EvaluatedOutputType { get; private set; }

        internal string EvaluatedAssemblyName { get; private set; }

        internal string EvaluatedRootNamespace { get; private set; }

        internal string EvaluatedGenerateAssemblyInfo { get; private set; }

        internal string EvaluatedAllowUnsafeBlocks { get; private set; }

        internal string EvaluatedTreatWarningsAsErrors { get; private set; }

        internal string UsingMicrosoftNetSdk { get; private set; }

        internal string EvaluatedDeterministic { get; private set; }

        internal string EvaluatedContinuousIntegrationBuild { get; private set; }

        internal string EvaluatedCopyLocalLockFileAssemblies { get; private set; }

        internal string EvaluatedBaseOutputPath { get; private set; }

        internal string EvaluatedOutputPath { get; private set; }

        internal string EvaluatedBaseIntermediateOutputPath { get; private set; }

        internal string EvaluatedIntermediateOutputPath { get; private set; }

        internal string EvaluatedProjectExtensionsPath { get; private set; }

        internal IDictionary<string, string> EvaluatedProperties { get; private set; }

        internal IList<string> ProjectReferences { get; private set; }

        internal IList<AssemblyReferenceFacts> AssemblyReferences { get; private set; }

        internal IList<string> PackageReferences { get; private set; }

        internal IList<string> ExplicitImports { get; private set; }

        internal IList<string> FactViolations { get; private set; }

        internal static ProjectFacts Load(
            string root,
            string relativePath,
            string configuration,
            string lockedSdkRoot,
            string targetingPackRoot,
            string ownedToolStateRoot)
        {
            string projectPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            XDocument document = XDocument.Load(projectPath, LoadOptions.None);
            XElement projectRoot = document.Root;
            var facts = new ProjectFacts
            {
                RelativePath = relativePath,
                ProjectSdk = projectRoot == null || projectRoot.Attribute("Sdk") == null
                    ? string.Empty
                    : projectRoot.Attribute("Sdk").Value.Trim(),
                AssemblyName = ReadProperty(document, "AssemblyName") ?? Path.GetFileNameWithoutExtension(projectPath),
                RootNamespace = ReadProperty(document, "RootNamespace") ?? string.Empty,
                TargetFramework = ReadProperty(document, "TargetFramework") ?? string.Empty,
                PlatformTarget = ReadProperty(document, "PlatformTarget") ?? string.Empty,
                OutputType = ReadProperty(document, "OutputType") ?? string.Empty,
                GenerateAssemblyInfo = ReadProperty(document, "GenerateAssemblyInfo") ?? string.Empty,
                AllowUnsafeBlocks = ReadProperty(document, "AllowUnsafeBlocks") ?? string.Empty,
                TreatWarningsAsErrors = ReadProperty(document, "TreatWarningsAsErrors") ?? string.Empty
            };

            if (projectRoot == null ||
                projectRoot.Name.Namespace != XNamespace.None ||
                !string.Equals(projectRoot.Name.LocalName, "Project", StringComparison.Ordinal) ||
                projectRoot.Attributes().Count() != 1 ||
                projectRoot.Attribute("Sdk") == null)
            {
                facts.FactViolations.Add(
                    "project root must be an unnamespaced Project element with exactly one Sdk attribute.");
            }
            if (document.Descendants().Any(element => element.Name.Namespace != XNamespace.None))
            {
                facts.FactViolations.Add("project elements may not use external XML namespaces.");
            }

            string[] allowedProjectElements =
            {
                "Project",
                "PropertyGroup",
                "ItemGroup",
                "TargetFramework",
                "PlatformTarget",
                "OutputType",
                "AssemblyName",
                "RootNamespace",
                "GenerateAssemblyInfo",
                "AllowUnsafeBlocks",
                "TreatWarningsAsErrors",
                "TerrariaReferencesDirectory",
                "ProjectReference",
                "Reference",
                "HintPath",
                "Private"
            };
            var allowedElementNames = new HashSet<string>(allowedProjectElements, StringComparer.Ordinal);
            if (projectRoot != null)
            {
                foreach (XElement element in projectRoot.DescendantsAndSelf())
                {
                    if (!allowedElementNames.Contains(element.Name.LocalName))
                    {
                        facts.FactViolations.Add("project contains an unapproved build element: " + element.Name.LocalName + ".");
                    }

                    if (element.Value.Contains("$([") || element.Attributes().Any(attribute => attribute.Value.Contains("$([")))
                    {
                        facts.FactViolations.Add("project property functions are not allowed: " + element.Name.LocalName + ".");
                    }
                }
            }

            string[] requiredSingleProperties =
            {
                "TargetFramework",
                "PlatformTarget",
                "OutputType",
                "AssemblyName",
                "RootNamespace",
                "GenerateAssemblyInfo",
                "AllowUnsafeBlocks",
                "TreatWarningsAsErrors"
            };
            foreach (string propertyName in requiredSingleProperties)
            {
                IList<XElement> propertyElements = document.Descendants()
                    .Where(element => element.Name.LocalName == propertyName)
                    .ToList();
                if (propertyElements.Count != 1)
                {
                    facts.FactViolations.Add(
                        propertyName + " must appear exactly once in the project file; actual: " + propertyElements.Count);
                    continue;
                }

                if (HasCondition(propertyElements[0]))
                {
                    facts.FactViolations.Add(propertyName + " may not be conditional in the project file.");
                }
            }

            if (string.Equals(
                relativePath,
                "src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj",
                StringComparison.OrdinalIgnoreCase))
            {
                IList<XElement> referenceRootProperties = document.Descendants()
                    .Where(element => element.Name.LocalName == "TerrariaReferencesDirectory")
                    .ToList();
                if (referenceRootProperties.Count != 1 ||
                    !string.Equals(
                        referenceRootProperties[0].Value.Trim(),
                        "$(MSBuildThisFileDirectory)..\\..\\external\\TerrariaRefs",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        (string) referenceRootProperties[0].Attribute("Condition"),
                        "'$(TerrariaReferencesDirectory)' == ''",
                        StringComparison.Ordinal))
                {
                    facts.FactViolations.Add(
                        "TerrariaReferencesDirectory must have exactly the approved default declaration.");
                }
            }

            IEnumerable<XElement> allElements = projectRoot == null
                ? Enumerable.Empty<XElement>()
                : projectRoot.DescendantsAndSelf();
            foreach (XAttribute condition in allElements.Attributes()
                .Where(attribute => string.Equals(attribute.Name.LocalName, "Condition", StringComparison.Ordinal)))
            {
                bool isAllowedHostReferenceRoot =
                    string.Equals(relativePath, "src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(condition.Parent.Name.LocalName, "TerrariaReferencesDirectory", StringComparison.Ordinal) &&
                    string.Equals(condition.Value.Trim(), "'$(TerrariaReferencesDirectory)' == ''", StringComparison.Ordinal) &&
                    string.Equals(condition.Parent.Value.Trim(), "$(MSBuildThisFileDirectory)..\\..\\external\\TerrariaRefs", StringComparison.Ordinal);
                if (!isAllowedHostReferenceRoot)
                {
                    facts.FactViolations.Add(
                        "project Condition is not allowed on " + condition.Parent.Name.LocalName + ".");
                }
            }

            string[] forbiddenControlElements = { "Choose", "When", "Otherwise", "Target", "UsingTask", "Sdk" };
            foreach (XElement element in document.Descendants().Where(element =>
                forbiddenControlElements.Contains(element.Name.LocalName, StringComparer.Ordinal)))
            {
                facts.FactViolations.Add("project control element is not allowed: " + element.Name.LocalName);
            }

            IDictionary<string, string> evaluated = ReadEvaluatedProperties(
                root,
                projectPath,
                configuration,
                lockedSdkRoot,
                targetingPackRoot,
                ownedToolStateRoot);
            facts.EvaluatedTargetFramework = evaluated["TargetFramework"];
            facts.EvaluatedPlatformTarget = evaluated["PlatformTarget"];
            facts.EvaluatedLangVersion = evaluated["LangVersion"];
            facts.EvaluatedOutputType = evaluated["OutputType"];
            facts.EvaluatedAssemblyName = evaluated["AssemblyName"];
            facts.EvaluatedRootNamespace = evaluated["RootNamespace"];
            facts.EvaluatedGenerateAssemblyInfo = evaluated["GenerateAssemblyInfo"];
            facts.EvaluatedAllowUnsafeBlocks = evaluated["AllowUnsafeBlocks"];
            facts.EvaluatedTreatWarningsAsErrors = evaluated["TreatWarningsAsErrors"];
            facts.UsingMicrosoftNetSdk = evaluated["UsingMicrosoftNETSdk"];
            facts.EvaluatedDeterministic = evaluated["Deterministic"];
            facts.EvaluatedContinuousIntegrationBuild = evaluated["ContinuousIntegrationBuild"];
            facts.EvaluatedCopyLocalLockFileAssemblies = evaluated["CopyLocalLockFileAssemblies"];
            facts.EvaluatedBaseOutputPath = evaluated["BaseOutputPath"];
            facts.EvaluatedOutputPath = evaluated["OutputPath"];
            facts.EvaluatedBaseIntermediateOutputPath = evaluated["BaseIntermediateOutputPath"];
            facts.EvaluatedIntermediateOutputPath = evaluated["IntermediateOutputPath"];
            facts.EvaluatedProjectExtensionsPath = evaluated["MSBuildProjectExtensionsPath"];
            facts.EvaluatedProperties = evaluated;

            string projectDirectory = Path.GetDirectoryName(projectPath);
            foreach (XElement reference in document.Descendants().Where(element => element.Name.LocalName == "ProjectReference"))
            {
                XAttribute include = reference.Attribute("Include");
                if (include == null || string.IsNullOrWhiteSpace(include.Value))
                {
                    facts.FactViolations.Add("ProjectReference must have a non-empty Include.");
                }
                else
                {
                    string absolute = Path.GetFullPath(Path.Combine(projectDirectory, include.Value));
                    facts.ProjectReferences.Add(ProjectGraphFacts.NormalizeRelativePath(root, absolute));
                }

                if (HasCondition(reference))
                {
                    facts.FactViolations.Add("ProjectReference may not be conditional.");
                }
                if (reference.Elements().Any())
                {
                    facts.FactViolations.Add("ProjectReference may not contain metadata elements.");
                }
                if (reference.Attributes().Count() != 1 || include == null)
                {
                    facts.FactViolations.Add("ProjectReference may contain only its Include attribute.");
                }
            }

            foreach (XElement reference in document.Descendants().Where(element => element.Name.LocalName == "Reference"))
            {
                XAttribute include = reference.Attribute("Include");
                if (include == null || string.IsNullOrWhiteSpace(include.Value))
                {
                    facts.FactViolations.Add("Reference must have a non-empty Include.");
                    continue;
                }

                facts.AssemblyReferences.Add(new AssemblyReferenceFacts(
                    include.Value,
                    ReadChild(reference, "HintPath"),
                    ReadChild(reference, "Private")));
                IList<XElement> hintPaths = reference.Elements()
                    .Where(element => element.Name.LocalName == "HintPath")
                    .ToList();
                IList<XElement> privateValues = reference.Elements()
                    .Where(element => element.Name.LocalName == "Private")
                    .ToList();
                if (hintPaths.Count != 1 || privateValues.Count != 1 ||
                    reference.Elements().Count() != 2)
                {
                    facts.FactViolations.Add(
                        "Reference must contain exactly one HintPath and one Private element: " + include.Value);
                }
                if (reference.Attributes().Count() != 1 || include == null)
                {
                    facts.FactViolations.Add("Reference may contain only its Include attribute: " + include.Value);
                }
                if (HasCondition(reference))
                {
                    facts.FactViolations.Add("Reference may not be conditional: " + include.Value);
                }
            }

            foreach (XElement reference in document.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
            {
                XAttribute include = reference.Attribute("Include");
                facts.PackageReferences.Add(include == null ? "<missing Include>" : include.Value);
                if (HasCondition(reference))
                {
                    facts.FactViolations.Add("PackageReference may not be conditional.");
                }
            }

            foreach (XElement import in document.Descendants().Where(element => element.Name.LocalName == "Import"))
            {
                XAttribute project = import.Attribute("Project");
                facts.ExplicitImports.Add(project == null ? "<missing Project>" : project.Value);
            }

            facts.ProjectReferences = facts.ProjectReferences.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
            facts.AssemblyReferences = facts.AssemblyReferences.OrderBy(item => item.Include, StringComparer.OrdinalIgnoreCase).ToList();
            facts.PackageReferences = facts.PackageReferences.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            facts.ExplicitImports = facts.ExplicitImports.OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList();
            facts.FactViolations = facts.FactViolations.OrderBy(item => item, StringComparer.Ordinal).ToList();
            return facts;
        }

        private static bool HasCondition(XElement element)
        {
            XElement current = element;
            while (current != null)
            {
                if (current.Attributes().Any(attribute =>
                    string.Equals(attribute.Name.LocalName, "Condition", StringComparison.Ordinal)))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        private static IDictionary<string, string> ReadEvaluatedProperties(
            string root,
            string projectPath,
            string configuration,
            string lockedSdkRoot,
            string targetingPackRoot,
            string ownedToolStateRoot)
        {
            string[] wildcardPropertyNames =
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
            };
            string[] propertyNames = new[]
            {
                "TargetFramework",
                "PlatformTarget",
                "LangVersion",
                "OutputType",
                "AssemblyName",
                "RootNamespace",
                "GenerateAssemblyInfo",
                "AllowUnsafeBlocks",
                "TreatWarningsAsErrors",
                "UsingMicrosoftNETSdk",
                "Deterministic",
                "ContinuousIntegrationBuild",
                "DeterministicSourcePaths",
                "DebugType",
                "DebugSymbols",
                "CodePage",
                "PathMap",
                "SourceRevisionId",
                "Version",
                "AssemblyVersion",
                "FileVersion",
                "InformationalVersion",
                "IncludeSourceRevisionInInformationalVersion",
                "CopyLocalLockFileAssemblies",
                "UseSharedCompilation",
                "ErrorLog",
                "DocumentationFile",
                "GenerateDocumentationFile",
                "EmitCompilerGeneratedFiles",
                "CompilerGeneratedFilesOutputPath",
                "PdbFile",
                "PreBuildEvent",
                "PostBuildEvent",
                "RunPostBuildEvent",
                "GeneratePackageOnBuild",
                "DeployOnBuild",
                "RestoreGraphOutputPath",
                "CscToolPath",
                "CscToolExe",
                "RoslynTargetsPath",
                "CSharpCoreTargetsPath",
                "FrameworkPathOverride",
                "MSBuildSDKsPath",
                "MSBuildExtensionsPath",
                "MSBuildExtensionsPath32",
                "MSBuildExtensionsPath64",
                "MSBuildUserExtensionsPath",
                "MSBuildToolsPath",
                "MSBuildBinPath",
                "MSBuildRuntimeType",
                "NETCoreSdkVersion",
                "ImportDirectoryPackagesProps",
                "DirectoryPackagesPropsPath",
                "BaseOutputPath",
                "OutputPath",
                "OutDir",
                "BaseIntermediateOutputPath",
                "IntermediateOutputPath",
                "MSBuildProjectExtensionsPath"
            }.Concat(wildcardPropertyNames).ToArray();

            string sdkRoot = Path.GetFullPath(lockedSdkRoot);
            targetingPackRoot = Path.GetFullPath(targetingPackRoot);
            string buildRoot = Path.Combine(root, "artifacts", "architecture-evaluation", configuration);
            string sdkAfterDirectoryBuildProps = Path.Combine(
                sdkRoot,
                "Sdks",
                "Microsoft.NET.Sdk",
                "Sdk",
                "UseArtifactsOutputPath.props");
            var arguments = new StringBuilder();
            arguments.Append("msbuild \"").Append(projectPath).Append("\" -noAutoResponse -nologo -nodeReuse:false");
            arguments.Append(" -p:Configuration=").Append(configuration);
            arguments.Append(" -p:Platform=x86");
            arguments.Append(" -p:JueMingRBuildRoot=\"").Append(buildRoot).Append("\"");
            arguments.Append(" -p:TerrariaReferencesDirectory=\"")
                .Append(Path.Combine(root, "external", "TerrariaRefs")).Append("\"");
            arguments.Append(" -p:SourceRevisionId=architecture-evaluation");
            arguments.Append(" -p:DirectoryBuildPropsPath=\"")
                .Append(Path.Combine(root, "Directory.Build.props")).Append("\"");
            arguments.Append(" -p:DirectoryBuildTargetsPath=\"")
                .Append(Path.Combine(root, "Directory.Build.targets")).Append("\"");
            arguments.Append(" -p:ImportDirectoryBuildProps=true -p:ImportDirectoryBuildTargets=true");
            arguments.Append(" -p:ImportDirectoryPackagesProps=false -p:DirectoryPackagesPropsPath=");
            arguments.Append(" -p:ImportProjectExtensionProps=false -p:ImportProjectExtensionTargets=false");
            arguments.Append(" -p:AlternateCommonProps=");
            arguments.Append(" -p:CustomBeforeDirectoryBuildProps=");
            arguments.Append(" -p:CustomAfterDirectoryBuildProps=\"").Append(sdkAfterDirectoryBuildProps).Append("\"");
            arguments.Append(" -p:CustomBeforeDirectoryBuildTargets= -p:CustomAfterDirectoryBuildTargets=");
            arguments.Append(" -p:CustomBeforeMicrosoftCommonProps= -p:CustomAfterMicrosoftCommonProps=");
            arguments.Append(" -p:CustomBeforeMicrosoftCommonTargets= -p:CustomAfterMicrosoftCommonTargets=");
            arguments.Append(" -p:CustomBeforeMicrosoftCSharpTargets= -p:CustomAfterMicrosoftCSharpTargets=");
            arguments.Append(" -p:CustomBeforeMicrosoftCommonCrossTargetingTargets= -p:CustomAfterMicrosoftCommonCrossTargetingTargets=");
            arguments.Append(" -p:MSBuildSDKsPath=\"").Append(Path.Combine(sdkRoot, "Sdks")).Append("\"");
            arguments.Append(" -p:MSBuildExtensionsPath=\"").Append(sdkRoot).Append("\"");
            arguments.Append(" -p:MSBuildExtensionsPath32=\"").Append(sdkRoot).Append("\"");
            arguments.Append(" -p:MSBuildExtensionsPath64=\"").Append(sdkRoot).Append("\"");
            arguments.Append(" -p:MSBuildUserExtensionsPath=\"").Append(sdkRoot).Append("\"");
            arguments.Append(" -p:RoslynTargetsPath=\"").Append(Path.Combine(sdkRoot, "Roslyn")).Append("\"");
            arguments.Append(" -p:CSharpCoreTargetsPath=\"")
                .Append(Path.Combine(sdkRoot, "Roslyn", "Microsoft.CSharp.Core.targets")).Append("\"");
            arguments.Append(" -p:CscToolPath= -p:CscToolExe= -p:UseSharedCompilation=false");
            arguments.Append(" -p:ErrorLog= -p:DocumentationFile= -p:GenerateDocumentationFile=false");
            arguments.Append(" -p:EmitCompilerGeneratedFiles=false -p:CompilerGeneratedFilesOutputPath= -p:PdbFile=");
            arguments.Append(" -p:PreBuildEvent= -p:PostBuildEvent= -p:RunPostBuildEvent=Never");
            arguments.Append(" -p:GeneratePackageOnBuild=false -p:DeployOnBuild=false -p:RestoreGraphOutputPath=");
            arguments.Append(" -p:FrameworkPathOverride=\"").Append(targetingPackRoot).Append("\"");
            arguments.Append(" -p:RestoreSources= -p:RestoreAdditionalProjectSources= -p:RestoreFallbackFolders=");
            foreach (string propertyName in wildcardPropertyNames)
            {
                arguments.Append(" -p:").Append(propertyName).Append("=false");
            }
            arguments.Append(" -getProperty:").Append(string.Join(",", propertyNames));

            string processStateRoot = Path.Combine(ownedToolStateRoot, "architecture-tests");
            string processTemp = Path.Combine(processStateRoot, "temp");
            Directory.CreateDirectory(processTemp);
            string dotnetPath = Path.GetFullPath(Path.Combine(sdkRoot, "..", "..", "dotnet.exe"));
            ProcessStartInfo startInfo = ProjectGraphFacts.CreateIsolatedProcessStartInfo(
                dotnetPath,
                arguments.ToString(),
                root,
                processTemp,
                ProjectGraphFacts.GetDotnetEnvironmentNames());

            string output;
            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Could not start dotnet msbuild property evaluation.");
                }

                output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "dotnet msbuild property evaluation failed for " + projectPath + ": " + error.Trim());
                }
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string propertyName in propertyNames)
            {
                MatchCollection matches = Regex.Matches(
                    output,
                    "\\\"" + Regex.Escape(propertyName) +
                        "\\\"\\s*:\\s*\\\"((?:\\\\[\\\"\\\\/bfnrt]|\\\\u[0-9A-Fa-f]{4}|[^\\\"\\\\])*)\\\"",
                    RegexOptions.CultureInvariant);
                if (matches.Count != 1)
                {
                    throw new InvalidOperationException(
                        "MSBuild did not report exactly one evaluated " + propertyName + " for " + projectPath + ".");
                }

                result.Add(propertyName, DecodeJsonString(matches[0].Groups[1].Value));
            }

            return result;
        }

        private static string DecodeJsonString(string encoded)
        {
            var result = new StringBuilder(encoded.Length);
            for (int index = 0; index < encoded.Length; index++)
            {
                char current = encoded[index];
                if (current != '\\')
                {
                    if (current < 0x20)
                    {
                        throw new InvalidOperationException("MSBuild property JSON contains an unescaped control character.");
                    }

                    result.Append(current);
                    continue;
                }

                if (++index >= encoded.Length)
                {
                    throw new InvalidOperationException("MSBuild property JSON ends with an incomplete escape.");
                }

                switch (encoded[index])
                {
                    case '\"': result.Append('\"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        int codeUnit = ReadJsonCodeUnit(encoded, ref index);
                        if (codeUnit >= 0xD800 && codeUnit <= 0xDBFF)
                        {
                            if (index + 2 >= encoded.Length || encoded[index + 1] != '\\' || encoded[index + 2] != 'u')
                            {
                                throw new InvalidOperationException("MSBuild property JSON contains an unpaired high surrogate.");
                            }

                            index += 2;
                            int lowSurrogate = ReadJsonCodeUnit(encoded, ref index);
                            if (lowSurrogate < 0xDC00 || lowSurrogate > 0xDFFF)
                            {
                                throw new InvalidOperationException("MSBuild property JSON contains an invalid surrogate pair.");
                            }

                            result.Append(char.ConvertFromUtf32(
                                0x10000 + ((codeUnit - 0xD800) << 10) + (lowSurrogate - 0xDC00)));
                        }
                        else if (codeUnit >= 0xDC00 && codeUnit <= 0xDFFF)
                        {
                            throw new InvalidOperationException("MSBuild property JSON contains an unpaired low surrogate.");
                        }
                        else
                        {
                            result.Append((char) codeUnit);
                        }
                        break;
                    default:
                        throw new InvalidOperationException("MSBuild property JSON contains an unsupported escape.");
                }
            }

            return result.ToString();
        }

        private static int ReadJsonCodeUnit(string encoded, ref int escapeIndex)
        {
            if (escapeIndex + 4 >= encoded.Length)
            {
                throw new InvalidOperationException("MSBuild property JSON contains an incomplete Unicode escape.");
            }

            string digits = encoded.Substring(escapeIndex + 1, 4);
            int codeUnit;
            if (!int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out codeUnit))
            {
                throw new InvalidOperationException("MSBuild property JSON contains an invalid Unicode escape.");
            }

            escapeIndex += 4;
            return codeUnit;
        }

        internal static Tuple<string, string> ResolveLockedBuildRoots(
            string root,
            string requestedSdkRoot,
            string requestedTargetingPackRoot)
        {
            string globalJsonPath = Path.Combine(root, "global.json");
            Match versionMatch = Regex.Match(
                File.ReadAllText(globalJsonPath),
                "\\\"version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
                RegexOptions.CultureInvariant);
            if (!versionMatch.Success)
            {
                throw new InvalidOperationException("global.json does not contain the locked SDK version.");
            }

            if (string.IsNullOrWhiteSpace(requestedSdkRoot) ||
                string.IsNullOrWhiteSpace(requestedTargetingPackRoot))
            {
                throw new InvalidOperationException("The formal build entry must provide both locked build roots.");
            }

            string sdkVersion = versionMatch.Groups[1].Value;
            string sdkRoot = Path.GetFullPath(requestedSdkRoot);
            string targetingPackRoot = Path.GetFullPath(requestedTargetingPackRoot);

            if (!string.Equals(Path.GetFileName(sdkRoot.TrimEnd(Path.DirectorySeparatorChar)), sdkVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The supplied SDK root does not match the version locked by global.json.");
            }
            if (!File.Exists(Path.Combine(sdkRoot, "MSBuild.dll")) ||
                !File.Exists(Path.Combine(sdkRoot, "Roslyn", "Microsoft.CSharp.Core.targets")) ||
                !File.Exists(Path.Combine(targetingPackRoot, "mscorlib.dll")))
            {
                throw new InvalidOperationException("The locked SDK or .NET Framework 4.7.2 Targeting Pack is unavailable.");
            }

            return Tuple.Create(sdkRoot, targetingPackRoot);
        }

        private static string ReadProperty(XDocument document, string localName)
        {
            XElement element = document.Descendants().FirstOrDefault(item => item.Name.LocalName == localName);
            return element == null ? null : element.Value.Trim();
        }

        private static string ReadChild(XElement parent, string localName)
        {
            XElement element = parent.Elements().FirstOrDefault(item => item.Name.LocalName == localName);
            return element == null ? null : element.Value.Trim();
        }
    }

    internal sealed class AssemblyReferenceFacts
    {
        internal AssemblyReferenceFacts(string include, string hintPath, string copyLocal)
        {
            Include = include;
            HintPath = hintPath ?? string.Empty;
            CopyLocal = copyLocal ?? string.Empty;
        }

        internal string Include { get; private set; }

        internal string HintPath { get; private set; }

        internal string CopyLocal { get; private set; }
    }
}
