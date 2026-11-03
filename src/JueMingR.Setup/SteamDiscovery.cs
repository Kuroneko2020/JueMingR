using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace JueMingR.Setup
{
    internal static class SteamDiscovery
    {
        internal static string[] FindInstalled()
        {
            var roots = new List<string>();
            AddRegistry(roots, RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath");
            AddRegistry(roots, RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Valve\Steam", "InstallPath");
            AddRegistry(roots, RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Valve\Steam", "InstallPath");
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            return FindInstalled(roots);
        }

        private static void AddRegistry(List<string> roots, RegistryHive hive, RegistryView view, string path, string value)
        {
            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(path))
                {
                    string root = key == null ? null : key.GetValue(value) as string;
                    if (!string.IsNullOrWhiteSpace(root)) roots.Add(root);
                }
            }
            catch (System.Security.SecurityException) { }
            catch (UnauthorizedAccessException) { }
        }

        // Only Steam's declared roots/libraries are read. No drive scan and no directory is
        // accepted merely because its name is Terraria. The same identity rule gates installation.
        internal static string[] FindInstalled(IEnumerable<string> steamRoots)
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in steamRoots)
            {
                TryLibrary(libraries, root);
                try
                {
                    var document = ReadVdf(Path.Combine(root, "steamapps", "libraryfolders.vdf"));
                    var folders = Section(document, "libraryfolders");
                    foreach (var entry in folders)
                    {
                        int index;
                        if (!int.TryParse(entry.Key, out index) || index < 0) continue;
                        string path = entry.Value as string;
                        if (path == null) path = Value(entry.Value as Dictionary<string, object>, "path");
                        TryLibrary(libraries, path);
                    }
                }
                catch (Exception error) when (Recoverable(error)) { }
            }
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string library in libraries)
            {
                try
                {
                    string manifest = Path.Combine(library, "steamapps", "appmanifest_105600.acf");
                    string folder = "Terraria";
                    if (File.Exists(manifest))
                    {
                        var app = Section(ReadVdf(manifest), "AppState");
                        if (Value(app, "appid") != "105600") continue;
                        folder = Value(app, "installdir");
                        if (string.IsNullOrWhiteSpace(folder) || folder == "." || folder == ".." || folder.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) continue;
                    }
                    string candidate = Path.Combine(library, "steamapps", "common", folder);
                    result.Add(TargetSafety.ValidateGameDirectory(candidate, true));
                }
                catch (Exception error) when (Recoverable(error)) { }
            }
            return result.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static string AutomaticSelection(string[] candidates) { return candidates.Length == 1 ? candidates[0] : ""; }

        private static bool Recoverable(Exception error)
        {
            return error is IOException || error is UnauthorizedAccessException || error is ArgumentException ||
                error is System.Security.SecurityException || error is BadImageFormatException;
        }

        private static void TryLibrary(HashSet<string> libraries, string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path)) libraries.Add(Path.GetFullPath(path));
            }
            catch (Exception error) when (Recoverable(error)) { }
        }

        private static string Value(Dictionary<string, object> section, string key)
        {
            object value;
            return section != null && section.TryGetValue(key, out value) ? value as string : null;
        }

        private static Dictionary<string, object> Section(Dictionary<string, object> document, string key)
        {
            object value;
            if (!document.TryGetValue(key, out value) || !(value is Dictionary<string, object>)) throw new InvalidDataException("Invalid Steam metadata.");
            return (Dictionary<string, object>)value;
        }

        private static Dictionary<string, object> ReadVdf(string path)
        {
            TargetSafety.CheckPath(path, false, false);
            if (new FileInfo(path).Length > 1048576) throw new InvalidDataException("Steam metadata too large.");
            string text = File.ReadAllText(path);
            var tokens = new List<string>();
            int position = 0;
            // Steam KeyValues uses quoted keys/values and nested braces; comments and escaped
            // backslashes/quotes are consumed as tokens, so app/path fields cannot bleed sections.
            var lexer = new Regex(@"\G(?:\s+|//[^\r\n]*|(?<brace>[{}])|""(?<value>(?:\\[\\""]|[^""\\])*)"")");
            while (position < text.Length)
            {
                Match match = lexer.Match(text, position);
                if (!match.Success) throw new InvalidDataException("Invalid Steam metadata token.");
                position += match.Length;
                if (match.Groups["brace"].Success) tokens.Add(match.Value);
                else if (match.Groups["value"].Success) tokens.Add(match.Groups["value"].Value.Replace("\\\"", "\"").Replace("\\\\", "\\"));
            }
            int cursor = 0;
            return Parse(tokens, ref cursor, false, 0);
        }

        private static Dictionary<string, object> Parse(List<string> tokens, ref int cursor, bool nested, int depth)
        {
            if (depth > 16) throw new InvalidDataException("Steam metadata nesting too deep.");
            var section = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            while (cursor < tokens.Count)
            {
                string key = tokens[cursor++];
                if (key == "}" && nested) return section;
                if (key == "{" || key == "}" || cursor == tokens.Count || section.ContainsKey(key)) throw new InvalidDataException("Invalid Steam section.");
                string value = tokens[cursor++];
                if (value == "}") throw new InvalidDataException("Missing Steam value.");
                section.Add(key, value == "{" ? (object)Parse(tokens, ref cursor, true, depth + 1) : value);
            }
            if (nested) throw new InvalidDataException("Unclosed Steam section.");
            return section;
        }
    }
}
