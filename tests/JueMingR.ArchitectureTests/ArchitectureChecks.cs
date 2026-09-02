using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace JueMingR.ArchitectureTests
{
    internal static class ArchitectureChecks
    {
        private static readonly KeyValuePair<string, string>[] ExpectedProjects =
        {
            Pair("JueMingR.Bootstrap", "src/JueMingR.Bootstrap/JueMingR.Bootstrap.csproj"),
            Pair("JueMingR.Platform", "src/JueMingR.Platform/JueMingR.Platform.csproj"),
            Pair("JueMingR.Features", "src/JueMingR.Features/JueMingR.Features.csproj"),
            Pair("JueMingR.TerrariaHost", "src/JueMingR.TerrariaHost/JueMingR.TerrariaHost.csproj"),
            Pair("JueMingR.Infrastructure", "src/JueMingR.Infrastructure/JueMingR.Infrastructure.csproj"),
            Pair("JueMingR.Setup", "src/JueMingR.Setup/JueMingR.Setup.csproj"),
            Pair("JueMingR.ArchitectureTests", "tests/JueMingR.ArchitectureTests/JueMingR.ArchitectureTests.csproj")
        };

        private static readonly IDictionary<string, string[]> ExpectedProjectReferences =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                { ExpectedProjects[0].Value, new string[0] },
                { ExpectedProjects[1].Value, new string[0] },
                { ExpectedProjects[2].Value, new[] { ExpectedProjects[1].Value } },
                { ExpectedProjects[3].Value, new[] { ExpectedProjects[1].Value, ExpectedProjects[2].Value, ExpectedProjects[4].Value } },
                { ExpectedProjects[4].Value, new[] { ExpectedProjects[1].Value } },
                { ExpectedProjects[5].Value, new string[0] },
                { ExpectedProjects[6].Value, new[] { ExpectedProjects[1].Value } }
            };

        private static readonly IDictionary<string, string> ExpectedGameReferences =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Terraria", "Terraria.exe" },
                { "ReLogic", "ReLogic.dll" },
                { "Microsoft.Xna.Framework.Game", "Microsoft.Xna.Framework.Game.dll" }
            };

        private static readonly string[] RequiredFiles =
        {
            "scripts/prepare-terraria-references.ps1",
            "scripts/build.ps1",
            "scripts/verify-reproducible-build.ps1",
            "eng/TerrariaReferences.baseline.json",
            "JueMingR.sln"
        };

        internal static IEnumerable<string> ExpectedProjectPaths
        {
            get { return ExpectedProjects.Select(project => project.Value); }
        }

        internal static void Check(RepositoryModel model, IList<string> failures)
        {
            CheckSolution(model, failures);
            CheckProjectReferenceGraph(model, failures);
            CheckBuildProperties(model, failures);
            CheckPackages(model, failures);
            CheckNeutralProjects(model, failures);
            CheckGameReferenceBoundary(model, failures);
            CheckLegacyNames(model, failures);
            CheckTrackedFiles(model, failures);
            CheckRequiredFiles(model, failures);
        }

        private static void CheckSolution(RepositoryModel model, IList<string> failures)
        {
            bool matches = model.SolutionProjects.Count == ExpectedProjects.Length &&
                ExpectedProjects.All(expected => model.SolutionProjects.Count(actual =>
                    string.Equals(actual.Name, expected.Key, StringComparison.Ordinal) &&
                    string.Equals(actual.RelativePath, expected.Value, StringComparison.OrdinalIgnoreCase)) == 1) &&
                model.SolutionProjects.All(actual => ExpectedProjects.Any(expected =>
                    string.Equals(actual.Name, expected.Key, StringComparison.Ordinal) &&
                    string.Equals(actual.RelativePath, expected.Value, StringComparison.OrdinalIgnoreCase)));
            if (!matches)
            {
                failures.Add("solution must contain exactly the six production projects and ArchitectureTests.");
            }
        }

        private static void CheckProjectReferenceGraph(RepositoryModel model, IList<string> failures)
        {
            foreach (KeyValuePair<string, string[]> expected in ExpectedProjectReferences)
            {
                XDocument project;
                if (!model.Projects.TryGetValue(expected.Key, out project))
                {
                    failures.Add("required project file is missing: " + expected.Key);
                    continue;
                }

                List<string> actual = Elements(project, "ProjectReference")
                    .Select(element => (string)element.Attribute("Include") ?? string.Empty)
                    .Select(include => model.ResolveProjectReference(expected.Key, include))
                    .ToList();
                List<string> expectedPaths = expected.Value.Select(model.GetFullPath).ToList();
                if (actual.Count != expectedPaths.Count ||
                    !new HashSet<string>(actual, StringComparer.OrdinalIgnoreCase).SetEquals(expectedPaths))
                {
                    failures.Add("ProjectReference graph mismatch: " + expected.Key);
                }
            }
        }

        private static void CheckBuildProperties(RepositoryModel model, IList<string> failures)
        {
            bool sharedLanguageVersionIsValid = model.SharedBuildProperties != null &&
                PropertyValues(model.SharedBuildProperties, "LangVersion")
                    .SequenceEqual(new[] { "7.3" }, StringComparer.OrdinalIgnoreCase);
            if (!sharedLanguageVersionIsValid)
            {
                failures.Add("Directory.Build.props must set LangVersion exactly to 7.3.");
            }

            foreach (KeyValuePair<string, XDocument> project in model.Projects)
            {
                bool frameworkIsValid = PropertyValues(project.Value, "TargetFramework")
                    .SequenceEqual(new[] { "net472" }, StringComparer.OrdinalIgnoreCase);
                bool platformIsValid = PropertyValues(project.Value, "PlatformTarget")
                    .SequenceEqual(new[] { "x86" }, StringComparer.OrdinalIgnoreCase);
                string[] languageOverrides = PropertyValues(project.Value, "LangVersion").ToArray();
                bool languageIsValid = sharedLanguageVersionIsValid &&
                    languageOverrides.All(value => string.Equals(value, "7.3", StringComparison.OrdinalIgnoreCase));
                if (!frameworkIsValid || !platformIsValid || !languageIsValid)
                {
                    failures.Add(project.Key + " must use net472, C# 7.3, and x86.");
                }
            }
        }

        private static void CheckPackages(RepositoryModel model, IList<string> failures)
        {
            foreach (KeyValuePair<string, XDocument> project in model.Projects)
            {
                if (Elements(project.Value, "PackageReference").Any())
                {
                    failures.Add("PackageReference is forbidden: " + project.Key);
                }
            }
        }

        private static void CheckNeutralProjects(RepositoryModel model, IList<string> failures)
        {
            foreach (string path in new[] { ExpectedProjects[0].Value, ExpectedProjects[1].Value })
            {
                XDocument project;
                if (model.Projects.TryGetValue(path, out project) &&
                    (Elements(project, "ProjectReference").Any() || Elements(project, "Reference").Any()))
                {
                    failures.Add(path + " must not have project or explicit assembly references.");
                }
            }
        }

        private static void CheckGameReferenceBoundary(RepositoryModel model, IList<string> failures)
        {
            string hostPath = ExpectedProjects[3].Value;
            foreach (KeyValuePair<string, XDocument> project in model.Projects)
            {
                List<XElement> references = Elements(project.Value, "Reference").ToList();
                if (!string.Equals(project.Key, hostPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (references.Any(IsGameReference))
                    {
                        failures.Add("TerrariaHost must be the only project with game assembly references: " + project.Key);
                    }

                    continue;
                }

                List<string> simpleNames = references.Select(GetSimpleReferenceName).ToList();
                if (references.Count != ExpectedGameReferences.Count ||
                    !new HashSet<string>(simpleNames, StringComparer.OrdinalIgnoreCase)
                        .SetEquals(ExpectedGameReferences.Keys))
                {
                    failures.Add("TerrariaHost must reference exactly Terraria, ReLogic, and Microsoft.Xna.Framework.Game.");
                    continue;
                }

                foreach (XElement reference in references)
                {
                    string simpleName = GetSimpleReferenceName(reference);
                    string hintPath = ChildValue(reference, "HintPath");
                    string privateValue = ChildValue(reference, "Private");
                    string expectedFile;
                    if (!ExpectedGameReferences.TryGetValue(simpleName, out expectedFile) ||
                        !string.Equals(GetFileName(hintPath), expectedFile, StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add("TerrariaHost game reference has an unexpected HintPath: " + simpleName);
                    }

                    if (!string.Equals(privateValue, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        failures.Add("TerrariaHost game reference must set Private=false: " + simpleName);
                    }
                }
            }
        }

        private static void CheckLegacyNames(RepositoryModel model, IList<string> failures)
        {
            foreach (KeyValuePair<string, XDocument> project in model.Projects)
            {
                IEnumerable<string> values = Elements(project.Value, "ProjectReference")
                    .Concat(Elements(project.Value, "Reference"))
                    .Select(element => (string)element.Attribute("Include") ?? string.Empty)
                    .Concat(Elements(project.Value, "HintPath").Select(element => element.Value));
                if (values.Any(ContainsLegacyName))
                {
                    failures.Add("project references must not contain JueMingZ or TerrariaHelper: " + project.Key);
                }
            }
        }

        private static void CheckTrackedFiles(RepositoryModel model, IList<string> failures)
        {
            foreach (string relativePath in model.TrackedFiles)
            {
                if (ExpectedGameReferences.Values.Contains(GetFileName(relativePath), StringComparer.OrdinalIgnoreCase))
                {
                    failures.Add("Git tracks a known game file name: " + relativePath);
                }
            }

            if (model.BaselineHashes.Count != ExpectedGameReferences.Count)
            {
                failures.Add("reference baseline must contain exactly three unique SHA-256 values.");
                return;
            }

            foreach (string relativePath in model.TrackedFiles)
            {
                if (model.BaselineHashes.Contains(model.GetTrackedFileHash(relativePath)))
                {
                    failures.Add("Git tracks a file whose content matches a game reference: " + relativePath);
                }
            }
        }

        private static void CheckRequiredFiles(RepositoryModel model, IList<string> failures)
        {
            foreach (string relativePath in RequiredFiles)
            {
                if (!File.Exists(model.GetFullPath(relativePath)))
                {
                    failures.Add("required Phase 0-R file is missing: " + relativePath);
                }
            }
        }

        private static IEnumerable<XElement> Elements(XDocument document, string localName)
        {
            return document.Descendants().Where(element => element.Name.LocalName == localName);
        }

        private static IEnumerable<string> PropertyValues(XDocument document, string propertyName)
        {
            return Elements(document, propertyName).Select(element => element.Value.Trim());
        }

        private static string ChildValue(XElement element, string childName)
        {
            XElement child = element.Elements().FirstOrDefault(item => item.Name.LocalName == childName);
            return child == null ? string.Empty : child.Value.Trim();
        }

        private static string GetSimpleReferenceName(XElement reference)
        {
            string include = (string)reference.Attribute("Include") ?? string.Empty;
            return include.Split(',')[0].Trim();
        }

        private static bool IsGameReference(XElement reference)
        {
            return ExpectedGameReferences.ContainsKey(GetSimpleReferenceName(reference)) ||
                ExpectedGameReferences.Values.Contains(
                    GetFileName(ChildValue(reference, "HintPath")),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static string GetFileName(string path)
        {
            return Path.GetFileName((path ?? string.Empty).Replace('/', '\\'));
        }

        private static bool ContainsLegacyName(string value)
        {
            return value.IndexOf("JueMingZ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("TerrariaHelper", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static KeyValuePair<string, string> Pair(string name, string path)
        {
            return new KeyValuePair<string, string>(name, path);
        }
    }
}
