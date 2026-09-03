using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace JueMingR.Bootstrap
{
    // One-time Phase 0-S diagnostic only. This is not a general logger or a future load-chain component.
    internal static class Phase0SDiagnosticSentinel
    {
        internal const string FileName = "phase-0-s-diagnostic.sentinel";

        private const long MaximumLength = 32768;
        private const int MaximumEntries = 1;
        private static readonly object Gate = new object();
        private static int writeAttempts;

        internal static void TryWrite(string path, string packageId, string eventName, string state)
        {
            if (String.IsNullOrEmpty(path) || String.IsNullOrEmpty(packageId) ||
                !IsAllowed(eventName, state))
            {
                return;
            }
            if (Interlocked.Increment(ref writeAttempts) > MaximumEntries)
            {
                return;
            }

            try
            {
                string line = String.Join(
                    "|",
                    "PHASE0S-DIAGNOSTIC",
                    "1",
                    packageId,
                    eventName,
                    state,
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture)) +
                    Environment.NewLine;
                byte[] bytes = new UTF8Encoding(false, true).GetBytes(line);
                lock (Gate)
                {
                    using (FileStream stream = new FileStream(
                        path,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.Read))
                    {
                        if (stream.Length + bytes.Length > MaximumLength)
                        {
                            return;
                        }

                        stream.Position = stream.Length;
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                }
            }
            catch (Exception)
            {
                // Diagnostic observation is best-effort and must never affect Terraria.
            }
        }

        private static bool IsAllowed(string eventName, string state)
        {
            return String.Equals(eventName, "RELOGIC_ASSEMBLY_LOAD_OBSERVED", StringComparison.Ordinal) &&
                String.Equals(state, "OBSERVED", StringComparison.Ordinal);
        }
    }
}
