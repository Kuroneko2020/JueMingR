using System;
using System.Collections.Generic;
using System.IO;

namespace JueMingR.Setup
{
    internal static class ConfigurationReset
    {
        // These are actual Host preference documents, not every JSON in the data root.
        // Notes, blueprint exchange/scene history, onboarding and unknown future files are content.
        internal static readonly string[] Documents = {
            "ui.json", "hotkeys.json", "information-window.json",
            "features/biome-display.json", "features/announcements.json", "features/footprints.json",
            "features/death-markers.json", "features/guidance.json", "features/entity-labels.json",
            "features/blueprint.json", "features/fishing.json", "features/coin-deposit.json",
            "features/world-targets.json", "features/continuous-bags.json", "features/extraction.json",
            "features/reforge.json", "features/recovery-potions.json", "features/recovery-buffs.json",
            "features/nearby-services.json", "features/world-object-text.json", "features/item-automation.json",
            "features/movement.json", "features/quick-items.json", "features/information-display.json",
            "features/view-unlock.json", "features/map-markers.json", "features/exploration-dynamic.json",
            "features/auto-capture.json", "features/herb-harvest.json", "features/auto-mining.json",
            "features/combat.json", "features/combat-observation.json"
        };

        internal static void Execute(string root, SetupResult result, Action<string> beforeDelete)
        {
            string config = Path.Combine(root, "JueMingRData", "config");
            TargetSafety.CheckPath(config, true, true);
            var files = new List<string>();
            foreach (string document in Documents)
            {
                var suffixes = new List<string> { "", ".bak", ".tmp", ".lock" };
                if (document == "features/guidance.json" || document == "features/information-display.json" ||
                    document == "features/item-automation.json" || document == "features/recovery-potions.json") suffixes.Add(".schema1-original");
                foreach (string suffix in suffixes)
                {
                    string path = Path.Combine(config, document.Replace('/', Path.DirectorySeparatorChar)) + suffix;
                    // Complete preflight before the first deletion: links, non-files and occupied
                    // recovery/lease files reject the operation, rather than silently restoring a setting.
                    TargetSafety.CheckUnlocked(path);
                    if (File.Exists(path)) files.Add(path);
                }
            }
            foreach (string path in files)
            {
                result.failedStep = "clear-configuration";
                if (beforeDelete != null) beforeDelete(path);
                TargetSafety.CheckUnlocked(path);
                File.Delete(path);
                result.completed.Add("clear:" + path.Substring(config.Length + 1).Replace('\\', '/'));
            }
            result.status = files.Count == 0 ? "noop" : "success";
            result.exitCode = 0;
            result.message = files.Count == 0 ? "没有需要清空的决明R配置。" : "决明R配置已清空，笔记和蓝图库保留。";
        }
    }
}
