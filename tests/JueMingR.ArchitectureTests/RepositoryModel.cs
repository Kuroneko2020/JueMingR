using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JueMingR.ArchitectureTests
{
    internal sealed class RepositoryModel
    {
        private static readonly Regex SolutionProjectPattern = new Regex(
            "^Project\\(\\\"[^\\\"]+\\\"\\) = \\\"(?<name>[^\\\"]+)\\\", \\\"(?<path>[^\\\"]+\\.csproj)\\\", \\\"[^\\\"]+\\\"$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex BaselineHashPattern = new Regex(
            "\\\"sha256\\\"\\s*:\\s*\\\"(?<hash>[0-9a-fA-F]{64})\\\"",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private RepositoryModel(string repositoryRoot)
        {
            RepositoryRoot = repositoryRoot;
            SolutionProjects = new List<SolutionProject>();
            Projects = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase);
            TrackedFiles = new List<string>();
            BaselineHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        internal string RepositoryRoot { get; private set; }

        internal IList<SolutionProject> SolutionProjects { get; private set; }

        internal IDictionary<string, XDocument> Projects { get; private set; }

        internal XDocument SharedBuildProperties { get; private set; }

        internal IList<string> TrackedFiles { get; private set; }

        internal ISet<string> BaselineHashes { get; private set; }

        internal static RepositoryModel Load(string repositoryRoot)
        {
            var model = new RepositoryModel(repositoryRoot);
            model.LoadSolution();
            model.LoadProjects();
            model.LoadSharedBuildProperties();
            model.LoadBaselineHashes();
            model.TrackedFiles = ReadTrackedFiles(repositoryRoot);
            return model;
        }

        internal string GetFullPath(string relativePath)
        {
            return Path.GetFullPath(
                Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        internal string ResolveProjectReference(string ownerProject, string include)
        {
            string ownerDirectory = Path.GetDirectoryName(GetFullPath(ownerProject));
            return Path.GetFullPath(Path.Combine(ownerDirectory, include));
        }

        internal string GetTrackedFileHash(string relativePath)
        {
            string path = GetFullPath(relativePath);
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private void LoadSolution()
        {
            string path = GetFullPath("JueMingR.sln");
            if (!File.Exists(path))
            {
                return;
            }

            foreach (string line in File.ReadAllLines(path))
            {
                Match match = SolutionProjectPattern.Match(line.Trim());
                if (match.Success)
                {
                    SolutionProjects.Add(new SolutionProject(
                        match.Groups["name"].Value,
                        NormalizeRelativePath(match.Groups["path"].Value)));
                }
            }
        }

        private void LoadProjects()
        {
            foreach (string relativePath in ArchitectureChecks.ExpectedProjectPaths)
            {
                string path = GetFullPath(relativePath);
                if (File.Exists(path))
                {
                    Projects.Add(relativePath, XDocument.Load(path, LoadOptions.None));
                }
            }
        }

        private void LoadSharedBuildProperties()
        {
            string path = GetFullPath("Directory.Build.props");
            if (File.Exists(path))
            {
                SharedBuildProperties = XDocument.Load(path, LoadOptions.None);
            }
        }

        private void LoadBaselineHashes()
        {
            string path = GetFullPath("eng/TerrariaReferences.baseline.json");
            if (!File.Exists(path))
            {
                return;
            }

            foreach (Match match in BaselineHashPattern.Matches(File.ReadAllText(path)))
            {
                BaselineHashes.Add(match.Groups["hash"].Value);
            }
        }

        private static IList<string> ReadTrackedFiles(string repositoryRoot)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "-C " + QuoteArgument(repositoryRoot) + " ls-files -z",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = new UTF8Encoding(false, true),
                StandardErrorEncoding = Encoding.UTF8
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("git ls-files failed: " + error.Trim());
                }

                return output
                    .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(NormalizeRelativePath)
                    .ToList();
            }
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }

    internal sealed class SolutionProject
    {
        internal SolutionProject(string name, string relativePath)
        {
            Name = name;
            RelativePath = relativePath;
        }

        internal string Name { get; private set; }

        internal string RelativePath { get; private set; }
    }
}
