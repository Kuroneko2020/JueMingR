using System;
using System.Collections.Generic;
using System.IO;

namespace JueMingR.ArchitectureTests
{
    // One executable assertion identity per registration. Structural checks stay
    // outside this business catalogue and run against each actual build.
    internal static class CheckCatalog
    {
        private sealed class Entry
        {
            internal string Name, Group;
            internal Action<string, List<string>> Run;
        }
        private static readonly List<Entry> entries = new List<Entry>();
        static CheckCatalog()
        {
            Add("CombatChecks", "combat-host", (root, failures) => CombatChecks.Check(failures));
            Add("BrowserCoreChecks", "browser-host", (root, failures) => BrowserCoreChecks.Check(failures));
            Add("RelationChecks", "browser-host", (root, failures) => RelationChecks.Check(failures));
            Add("BrowserHistoryChecks", "browser-host", (root, failures) => BrowserHistoryChecks.Check(failures));
            Add("ChestKnowledgeChecks", "browser-host", (root, failures) => ChestKnowledgeChecks.Check(failures));
            Add("AnnouncementChecks", "browser-host", (root, failures) => AnnouncementChecks.Check(failures));
            Add("FootprintCoreChecks", "footprints-host", (root, failures) => FootprintCoreChecks.Check(failures));
            Add("FootprintFileChecks", "footprints-host", (root, failures) => FootprintFileChecks.Check(failures));
            Add("FootprintWorkerChecks", "footprints-host", (root, failures) => FootprintWorkerChecks.Check(failures));
            Add("MapAssetChecks", "map-host", (root, failures) => MapAssetChecks.Check(failures));
            Add("ExplorationChecks", "map-host", (root, failures) => ExplorationChecks.Check(failures));
            Add("MapPersistenceChecks", "map-host", (root, failures) => MapPersistenceChecks.Check(failures));
            Add("HotkeyCoreChecks", "hotkeys", (root, failures) => HotkeyCoreChecks.Check(failures));
            Add("DynamicHotkeyChecks", "hotkeys", (root, failures) => DynamicHotkeyChecks.Check(failures));
            Add("HotkeyStorageChecks", "hotkeys", (root, failures) => HotkeyStorageChecks.Check(root, failures));
            Add("QuickItemChecks", "quick-items-host", (root, failures) => QuickItemChecks.Check(failures));
            Add("CoinDepositChecks", "coin-deposit-host", (root, failures) => CoinDepositChecks.Check(failures));
            Add("RecoveryChecks", "recovery-host", (root, failures) => RecoveryChecks.Check(failures));
            Add("ToolsChecks", "tools-host", (root, failures) => ToolsChecks.Check(failures));
            Add("FishingChecks", "fishing-host", (root, failures) => FishingChecks.Check(failures));
            Add("FishingStorageChecks", "fishing-host", (root, failures) => FishingStorageChecks.Check(failures));
            Add("OnboardingChecks", "about-host", (root, failures) => OnboardingChecks.Check(failures));
            Add("PreferenceChecks", "preferences", (root, failures) => PreferenceChecks.Check(failures));
            Add("PreferenceConcurrencyChecks", "preferences", (root, failures) => PreferenceConcurrencyChecks.Check(failures));
            Add("PreferenceStorageChecks", "preferences", (root, failures) => PreferenceStorageChecks.Check(failures));
            Add("InformationTests", "information", (root, failures) => InformationTests.Check(failures));
            Add("GuidanceTests", "guidance", (root, failures) => GuidanceTests.Check(failures));
            Add("DeathArchiveChecks", "death-host", (root, failures) => DeathArchiveChecks.Check(failures));
            Add("DeathHistoryWorkerChecks", "death-host", (root, failures) => DeathHistoryWorkerChecks.Check(failures));
            Add("WorldTimeChecks", "death-host", (root, failures) => WorldTimeChecks.Check(failures));
            Add("DeathPreferenceChecks", "death-host", (root, failures) => DeathPreferenceChecks.Check(failures));
            Add("DeathWorkloadChecks", "death-host", (root, failures) => DeathWorkloadChecks.Check(failures));
            Add("EntityLabelSettingsChecks", "entity", (root, failures) => EntityLabelSettingsChecks.Check(failures));
            Add("EntityLabelRulesChecks", "entity", (root, failures) => EntityLabelRulesChecks.Check(failures));
            Add("WorldTargetRulesChecks", "world-host", (root, failures) => WorldTargetRulesChecks.Check(failures));
            Add("WorldTargetSettingsChecks", "world-host", (root, failures) => WorldTargetSettingsChecks.Check(failures));
            Add("ObjectRulesChecks", "world-host", (root, failures) => ObjectRulesChecks.Check(failures));
            Add("ObjectSettingsChecks", "world-host", (root, failures) => ObjectSettingsChecks.Check(failures));
            Add("ObjectTextChecks", "world-host", (root, failures) => ObjectTextChecks.Check(failures));
            Add("ObjectDiscoveryChecks", "records", (root, failures) => ObjectDiscoveryChecks.Check(failures));
            Add("OpenedPositionChecks", "records", (root, failures) => OpenedPositionChecks.Check(failures));
            Add("OpenedConcurrencyChecks", "records", (root, failures) => OpenedConcurrencyChecks.Run());
            Add("ItemAutomationSettingsChecks", "items", (root, failures) => ItemAutomationSettingsChecks.Check(failures));
            Add("ItemAutomationChecks", "items", (root, failures) => ItemAutomationChecks.Check(failures));
            Add("ItemRuntimeChecks", "items", (root, failures) => ItemRuntimeChecks.Check(failures));
            Add("ProcessingChecks", "processing-host", (root, failures) => ProcessingChecks.Check(failures));
            Add("NotesDomainChecks", "notes-host", (root, failures) => NotesDomainChecks.Check(failures));
            Add("NotesEditingChecks", "notes-host", (root, failures) => NotesEditingChecks.Check(failures));
            Add("NotesStorageChecks", "notes-host", (root, failures) => NotesStorageChecks.Check(failures));
            Add("NotesConcurrencyChecks", "notes-host", (root, failures) => NotesConcurrencyChecks.Check(failures));
            Add("NotesRevisionChecks", "notes-host", (root, failures) => NotesRevisionChecks.Check(failures));
            Add("BiomeMapping", "biomes", (root, failures) => BiomeFeatureChecks.CheckMapping(failures));
            Add("BiomeLifecycle", "biomes", (root, failures) => BiomeFeatureChecks.CheckLifecycle(failures));
        }
        private static void Add(string name, string group, Action<string, List<string>> run)
        {
            if (entries.Exists(e => e.Name == name)) throw new InvalidOperationException("Duplicate check: " + name);
            entries.Add(new Entry { Name = name, Group = group, Run = run });
        }
        internal static void List()
        {
            foreach (var entry in entries) Console.WriteLine(entry.Name + "|" + entry.Group);
        }
        internal static void Structure(string root, List<string> failures)
        {
            ArchitectureChecks.Check(RepositoryModel.Load(root), failures);
            OperationContractChecks.Check(failures);
            Phase0TArchitectureChecks.Check(failures);
        }
        internal static void Run(string name, string root, List<string> failures)
        {
            if (IntPtr.Size != 4 || typeof(object).Assembly.GetName().Name != "mscorlib")
                throw new InvalidOperationException("Checks require .NET Framework x86.");
            var selected = entries.FindAll(e => name == "*" || e.Name == name);
            if (selected.Count == 0) throw new ArgumentException("Unknown check: " + name);
            foreach (var entry in selected)
            {
                int before = failures.Count;
                entry.Run(Path.GetFullPath(root), failures);
                Console.WriteLine(entry.Name + ": " + (failures.Count == before ? "PASS" : "FAIL"));
            }
        }
    }
}
