using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JueMingR.ArchitectureTests
{
    internal sealed class ProjectGraphFacts
    {
        private ProjectGraphFacts(
            string repositoryRoot,
            IDictionary<string, ProjectFacts> projects,
            IList<string> solutionProjects,
            IList<string> discoveredProjects,
            IList<string> trackedFiles,
            IList<string> sharedBuildItemViolations)
        {
            RepositoryRoot = repositoryRoot;
            Projects = projects;
            SolutionProjects = solutionProjects;
            DiscoveredProjects = discoveredProjects;
            TrackedFiles = trackedFiles;
            SharedBuildItemViolations = sharedBuildItemViolations;
        }

        internal string RepositoryRoot { get; private set; }

        internal IDictionary<string, ProjectFacts> Projects { get; private set; }

        internal IList<string> SolutionProjects { get; private set; }

        internal IList<string> DiscoveredProjects { get; private set; }

        internal IList<string> TrackedFiles { get; private set; }

        internal IList<string> SharedBuildItemViolations { get; private set; }

        internal static ProjectGraphFacts Load(string repositoryRoot)
        {
            string root = Path.GetFullPath(repositoryRoot);
            var discovered = new List<string>();
            CollectProjects(root, root, discovered);
            discovered.Sort(StringComparer.OrdinalIgnoreCase);

            var projects = new Dictionary<string, ProjectFacts>(StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in discovered)
            {
                projects.Add(relativePath, ProjectFacts.Load(root, relativePath));
            }

            return new ProjectGraphFacts(
                root,
                projects,
                ReadSolutionProjects(root),
                discovered,
                ReadTrackedFiles(root),
                ReadSharedBuildItemViolations(root));
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
                "^Project\\(\"[^\"]+\"\\) = \"[^\"]+\", \"([^\"]+\\.csproj)\",",
                RegexOptions.CultureInvariant);
            foreach (string line in File.ReadAllLines(solutionPath))
            {
                Match match = pattern.Match(line);
                if (match.Success)
                {
                    string absolute = Path.GetFullPath(Path.Combine(root, match.Groups[1].Value));
                    projects.Add(NormalizeRelativePath(root, absolute));
                }
            }

            projects.Sort(StringComparer.OrdinalIgnoreCase);
            return projects;
        }

        private static IList<string> ReadTrackedFiles(string root)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files -z",
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

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
            string[] forbiddenItems = { "ProjectReference", "Reference", "PackageReference" };
            var violations = new List<string>();
            foreach (string fileName in fileNames)
            {
                string path = Path.Combine(root, fileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                XDocument document = XDocument.Load(path, LoadOptions.None);
                foreach (XElement element in document.Descendants())
                {
                    if (forbiddenItems.Contains(element.Name.LocalName, StringComparer.Ordinal))
                    {
                        violations.Add(fileName + ":" + element.Name.LocalName);
                    }
                }
            }

            return violations;
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
        }

        internal string RelativePath { get; private set; }

        internal string AssemblyName { get; private set; }

        internal string TargetFramework { get; private set; }

        internal string PlatformTarget { get; private set; }

        internal string OutputType { get; private set; }

        internal string GenerateAssemblyInfo { get; private set; }

        internal string AllowUnsafeBlocks { get; private set; }

        internal string TreatWarningsAsErrors { get; private set; }

        internal IList<string> ProjectReferences { get; private set; }

        internal IList<AssemblyReferenceFacts> AssemblyReferences { get; private set; }

        internal IList<string> PackageReferences { get; private set; }

        internal IList<string> ExplicitImports { get; private set; }

        internal static ProjectFacts Load(string root, string relativePath)
        {
            string projectPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            XDocument document = XDocument.Load(projectPath, LoadOptions.None);
            var facts = new ProjectFacts
            {
                RelativePath = relativePath,
                AssemblyName = ReadProperty(document, "AssemblyName") ?? Path.GetFileNameWithoutExtension(projectPath),
                TargetFramework = ReadProperty(document, "TargetFramework") ?? string.Empty,
                PlatformTarget = ReadProperty(document, "PlatformTarget") ?? string.Empty,
                OutputType = ReadProperty(document, "OutputType") ?? string.Empty,
                GenerateAssemblyInfo = ReadProperty(document, "GenerateAssemblyInfo") ?? string.Empty,
                AllowUnsafeBlocks = ReadProperty(document, "AllowUnsafeBlocks") ?? string.Empty,
                TreatWarningsAsErrors = ReadProperty(document, "TreatWarningsAsErrors") ?? string.Empty
            };

            string projectDirectory = Path.GetDirectoryName(projectPath);
            foreach (XElement reference in document.Descendants().Where(element => element.Name.LocalName == "ProjectReference"))
            {
                XAttribute include = reference.Attribute("Include");
                if (include != null)
                {
                    string absolute = Path.GetFullPath(Path.Combine(projectDirectory, include.Value));
                    facts.ProjectReferences.Add(ProjectGraphFacts.NormalizeRelativePath(root, absolute));
                }
            }

            foreach (XElement reference in document.Descendants().Where(element => element.Name.LocalName == "Reference"))
            {
                XAttribute include = reference.Attribute("Include");
                if (include == null)
                {
                    continue;
                }

                facts.AssemblyReferences.Add(new AssemblyReferenceFacts(
                    include.Value,
                    ReadChild(reference, "HintPath"),
                    ReadChild(reference, "Private")));
            }

            foreach (XElement reference in document.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
            {
                XAttribute include = reference.Attribute("Include");
                facts.PackageReferences.Add(include == null ? "<missing Include>" : include.Value);
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
            return facts;
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
