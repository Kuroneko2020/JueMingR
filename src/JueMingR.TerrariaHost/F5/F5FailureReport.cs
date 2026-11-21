using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    // The first actual failure of each surface can be recovered after closing
    // the game. Healthy input and drawing never format or write a report.
    internal sealed class F5FailureReport
    {
        private readonly string directory;
        private bool shellRecorded, creationRecorded;

        internal F5FailureReport(string gameDirectory)
        { directory = Path.Combine(gameDirectory, "JueMingRData", "diagnostics", "f5"); }

        internal void Record(bool creation, string phase, Exception primary, Exception cleanup, string state)
        {
            if (primary == null || (creation ? creationRecorded : shellRecorded)) return;
            if (creation) creationRecorded = true; else shellRecorded = true;
            try
            {
                var assembly = typeof(F5Shell).Assembly;
                string build = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.FullName;
                string report = $"utc={DateTime.UtcNow:O}\nbuild={build}\nterraria={Main.versionNumber}\nphase={phase}\n{state}\nfirst={Bounded(primary)}\n";
                if (cleanup != null) report += "cleanup=" + Bounded(cleanup) + "\n";
                string file = creation ? "first-companion-creation.txt" : "first-f5-shell.txt";
                // The worker receives text only. It never touches Terraria objects.
                ThreadPool.QueueUserWorkItem(_ => Write(file, report));
            }
            catch (Exception) { /* Preserve the original failure and the independent UI cleanup. */ }
        }

        private static string Bounded(Exception error)
        {
            const int limit = 3000;
            string text = error.ToString();
            return text.Length <= limit ? text : text.Substring(0, limit) + "\n[truncated]";
        }

        private void Write(string file, string report)
        {
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, file), report);
            }
            catch (Exception) { /* A file-system failure cannot turn the UI failure into another exception. */ }
        }
    }
}
