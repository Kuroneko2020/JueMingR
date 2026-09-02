using System;
using System.Collections.Generic;
using System.IO;

namespace Terraria
{
    public class Main
    {
        public static bool FixtureInitializeMarker { get; private set; }

        public void RunFirstInitialize()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            FixtureInitializeMarker = true;
            Console.WriteLine("FIXTURE_MAIN_INITIALIZE_ORIGINAL");
        }
    }

    internal static class Program
    {
        private static readonly string[] ExpectedEvents =
        {
            "TERRARIA_ASSEMBLY_READY",
            "HARMONY_READY",
            "HOOK_INSTALLED",
            "MAIN_INITIALIZE_POSTFIX_FIRED",
            "RUNTIME_HANDOFF_COMPLETE"
        };

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3 ||
                    (args[0] != "expect-handoff" && args[0] != "expect-no-handoff"))
                {
                    throw new ArgumentException(
                        "Usage: Terraria expect-handoff|expect-no-handoff <evidence-path> <package-id>");
                }

                string mode = args[0];
                string evidencePath = Path.GetFullPath(args[1]);
                string packageId = args[2];
                if (String.IsNullOrWhiteSpace(packageId))
                {
                    throw new ArgumentException("Package id must not be empty.");
                }

                // The fixture's first lifecycle action is its first, parameterless Main.Initialize call.
                var main = new Main();
                main.RunFirstInitialize();

                if (!global::Terraria.Main.FixtureInitializeMarker)
                {
                    throw new InvalidOperationException("The fixture Main.Initialize marker was not reached.");
                }

                string[] evidenceLines = File.Exists(evidencePath)
                    ? File.ReadAllLines(evidencePath)
                    : new string[0];
                if (mode == "expect-handoff")
                {
                    AssertCompleteHandoff(evidenceLines, packageId);
                }
                else
                {
                    AssertNoHandoffSuccess(evidenceLines, packageId);
                }

                Console.WriteLine("PASS: fixture mode {0} validated.", mode);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("FAIL: fixture validation failed: {0}", exception.Message);
                return 1;
            }
        }

        private static void AssertCompleteHandoff(IList<string> evidenceLines, string packageId)
        {
            if (evidenceLines.Count != ExpectedEvents.Length)
            {
                throw new InvalidOperationException("A complete handoff requires exactly five evidence lines.");
            }

            for (int index = 0; index < ExpectedEvents.Length; index++)
            {
                string[] fields = evidenceLines[index].Split('|');
                if (fields.Length != 7 ||
                    fields[0] != "PHASE0S" ||
                    fields[1] != "1" ||
                    fields[2] != packageId ||
                    fields[3] != (index + 1).ToString("D2") ||
                    fields[4] != ExpectedEvents[index] ||
                    String.IsNullOrWhiteSpace(fields[5]) ||
                    String.IsNullOrWhiteSpace(fields[6]))
                {
                    throw new InvalidOperationException(
                        "Evidence line " + (index + 1) + " is not the required strict Phase 0-S event.");
                }
            }
        }

        private static void AssertNoHandoffSuccess(IList<string> evidenceLines, string packageId)
        {
            foreach (string line in evidenceLines)
            {
                string[] fields = line.Split('|');
                if (fields.Length >= 5 &&
                    fields[0] == "PHASE0S" &&
                    fields[1] == "1" &&
                    fields[2] == packageId &&
                    fields[4] == "RUNTIME_HANDOFF_COMPLETE")
                {
                    throw new InvalidOperationException(
                        "Negative fixture mode rejects RUNTIME_HANDOFF_COMPLETE.");
                }
            }
        }
    }
}
