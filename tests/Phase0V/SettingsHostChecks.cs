using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Terraria
{
    internal static class SettingsHostChecks
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static void PrepareUnrelatedWorkingDirectory()
        {
            string root = GameRoot();
            string cursor = root;
            while (cursor != null && !File.Exists(Path.Combine(cursor, ".phase0s-test-root")))
                cursor = Path.GetDirectoryName(cursor);
            F5InputChecks.Check(cursor != null && root.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase),
                "settings host checks require the marked isolated fixture root");
            string unrelated = Path.Combine(root, "unrelated-cwd");
            Directory.CreateDirectory(unrelated);
            Environment.CurrentDirectory = unrelated;
        }

        internal static void PrepareLegacyScene(Action applyUpdate)
        {
            object context = Context(), preferences = Get(context, "preferences");
            WaitForLoaded(preferences);
            // Existing consumers specify a centered, enabled starting scene.
            // Prepare it explicitly through Settings; a prior fixture process is
            // now allowed to have left a different preference on this installation.
            Invoke(preferences, "SetBiomeEnabled", true);
            Invoke(preferences, "SetPosition", null);
            Invoke(Get(Get(context, "Shell"), "State"), "RestorePosition", null);
            // Restart this test's cadence from its first enabled observation,
            // independent of whether background loading won the handoff race.
            Invoke(Get(context, "Runtime"), "SetFeatureEnabled", false);
            applyUpdate();
        }

        internal static void Run(Main main, string mode)
        {
            object context = Context(), preferences = Get(context, "preferences"), runtime = Get(context, "Runtime");
            WaitForLoaded(preferences);
            if (mode == "expect-settings-save") PreferenceFeedbackChecks.Run(context);
            main.RunUpdateLoop(1);
            main.SetupAndDrawBiomeLayer();
            bool corruptUi = mode == "expect-settings-corrupt-ui";
            bool corruptBiome = mode == "expect-settings-corrupt-biome";
            bool restored = mode != "expect-settings-save";
            string protectedPath = corruptUi ? UiPath() : BiomePath();
            byte[] original = corruptUi || corruptBiome ? File.ReadAllBytes(protectedPath) : null;
            if (mode == "expect-settings-save")
                Check((bool)Get(preferences, "BiomeEnabled") && Get(preferences, "Position") == null,
                    "missing independent documents retain accepted enabled and centered defaults");
            if (mode == "expect-settings-restore")
                Check(!(bool)Get(preferences, "BiomeEnabled") && !(bool)Get(runtime, "FeatureEnabled") && !(bool)Get(runtime, "FeatureFailed"),
                    "restarted saved false is user-off, never the host failure latch");
            if (corruptUi)
                Check((bool)Get(preferences, "BiomeEnabled") && Get(preferences, "Position") == null && Status(preferences, "ui") == "Invalid",
                    "invalid UI document cannot block the legal biome document");
            if (corruptBiome)
                Check((bool)Get(preferences, "BiomeEnabled") && !(bool)Get(runtime, "FeatureFailed") && Status(preferences, "biome") == "Invalid",
                    "invalid biome document uses an in-memory default without failing the Feature");

            // This component boundary exercises the real composed Settings and
            // Runtime without a graphics device. F5ConsumerChecks separately
            // retains the actual input/renderer contract in the default suite.
            if (restored && !corruptUi)
            {
                object restoredPosition = Get(preferences, "Position");
                Check(restoredPosition != null && (int)Get(restoredPosition, "X") == 1100 && (int)Get(restoredPosition, "Y") == 250,
                    "new process restores the exact saved logical position in the composed Settings owner");
            }
            if (mode == "expect-settings-restore")
            {
                Invoke(preferences, "SetBiomeEnabled", true); main.RunUpdateLoop(1);
                Check((bool)Get(preferences, "BiomeEnabled") && (bool)Get(runtime, "FeatureEnabled") && !(bool)Get(runtime, "FeatureFailed"),
                    "restored off remains usable and the Settings command can re-enable it");
                WaitForSaved(preferences, "biome");
                Main.FixtureThrowOnDraw = true; main.DrawBiomeLayer(); Main.FixtureThrowOnDraw = false;
                Check((bool)Get(runtime, "FeatureFailed") && (bool)Get(preferences, "BiomeEnabled"),
                    "a real draw failure must not rewrite the user's desired enabled value");
                main.RunUpdateLoop(1);
                Check((bool)Get(preferences, "BiomeEnabled") && (bool)Get(runtime, "FeatureFailed") && !(bool)Get(runtime, "FeatureEnabled"),
                    "applying saved enabled after failure cannot clear the Feature failure latch");
            }
            else
            {
                Invoke(preferences, "SetBiomeEnabled", false); main.RunUpdateLoop(1);
                Check(!(bool)Get(preferences, "BiomeEnabled") && !(bool)Get(runtime, "FeatureEnabled") && !(bool)Get(runtime, "FeatureFailed"),
                    "the composed command applies desired off and leaves the Feature usable");
                MethodInfo setPosition = preferences.GetType().GetMethod("SetPosition", Instance);
                // The linked layout tests use their fixture copy of the position
                // model; the actual Host command must receive its loaded type.
                object position = Activator.CreateInstance(setPosition.GetParameters()[0].ParameterType, new object[] { 1100, 250 });
                setPosition.Invoke(preferences, new[] { position });
                if (!corruptBiome) WaitForSaved(preferences, "biome");
                if (!corruptUi) WaitForSaved(preferences, "ui");
                Main.gameMenu = true; main.RunUpdateLoop(1);
                Main.ConfigureDesertWorld(); main.RunUpdateLoop(1);
                Check(!(bool)Get(preferences, "BiomeEnabled") && !(bool)Get(runtime, "FeatureEnabled") &&
                    (int)Get(Get(preferences, "Position"), "X") == 1100,
                    "changing world/player cannot reset installation-scoped user preferences");
            }
            Check(File.Exists(UiPath()) && File.Exists(BiomePath()), "the two actual documents belong beside the verified fixture executable");
            Check(!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "JueMingRData")) &&
                !Directory.Exists(Path.Combine(GameRoot(), "JueMingR.Validation", "JueMingRData")),
                "neither current directory nor Host sidecar is a competing data root");
            if (original != null)
                Check(Convert.ToBase64String(original) == Convert.ToBase64String(File.ReadAllBytes(protectedPath)),
                    "in-memory adjustments must preserve the invalid original byte-for-byte");
            Console.WriteLine("PASS: real Host settings mode {0}, independent documents, path and fault boundaries.", mode);
        }

        private static void WaitForLoaded(object preferences)
        {
            int updates = Main.FixtureUpdateCount;
            Wait(() => (bool)Get(preferences, "IsLoaded"), "settings background load did not finish");
            Check(Main.FixtureUpdateCount == updates, "waiting for settings cannot consume the old cadence scenario's ticks");
        }
        private static void WaitForSaved(object preferences, string document)
        { Wait(() => Status(preferences, document) == "Saved", document + " did not reach its actual saved state"); }
        private static string Status(object preferences, string document)
        { return Get(Get(Get(preferences, document), "Snapshot"), "Status").ToString(); }
        private static void Wait(Func<bool> ready, string failure)
        {
            var elapsed = Stopwatch.StartNew();
            while (!ready() && elapsed.ElapsedMilliseconds < 5000) Thread.Sleep(10);
            Check(ready(), failure);
        }
        private static object Context()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == "JueMingR.TerrariaHost")
                    return assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true)
                        .GetField("postfixContext", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            throw new InvalidOperationException("The actual Host assembly was not loaded.");
        }
        private static object Get(object value, string name)
        {
            FieldInfo field = value.GetType().GetField(name, Instance);
            return field != null ? field.GetValue(value) : value.GetType().GetProperty(name, Instance).GetValue(value);
        }
        private static void Invoke(object value, string name, object argument)
        { value.GetType().GetMethod(name, Instance).Invoke(value, new[] { argument }); }
        private static void Check(bool value, string message) { F5InputChecks.Check(value, message); }
        private static string GameRoot() { return Path.GetDirectoryName(typeof(Main).Assembly.Location); }
        private static string UiPath() { return Path.Combine(GameRoot(), "JueMingRData", "config", "ui.json"); }
        private static string BiomePath() { return Path.Combine(GameRoot(), "JueMingRData", "config", "features", "biome-display.json"); }
    }
}
