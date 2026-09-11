using System;
using System.Collections.Generic;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.Hotkeys;
using Terraria.GameInput;

namespace Terraria
{
    internal static class HotkeyAdvisoryChecks
    {
        internal static HotkeyAdvisory Notice(int count)
        {
            var items = new List<HotkeyOverlap>();
            for (int i = 0; i < count; i++) items.Add(new HotkeyOverlap("test:" + i, "原版动作示例 " + (i + 1), new[] { i % 2 == 0 ? "LCtrl" : "LShift" }));
            return new HotkeyAdvisory(items, new string[0]);
        }
        internal static void Run()
        {
            var original = PlayerInput.CurrentProfile;
            var action = new HotkeyAction("test.complete", "测试动作", HotkeyContext.Gameplay, () => true, () => { });
            try
            {
                var profile = new PlayerInputProfile(); PlayerInput.CurrentProfile = profile;
                var map = profile.InputModes[InputMode.Keyboard].KeyStatus; map.Clear();
                map["SmartSelect"] = new List<string> { "LeftShift", "K", "K" };
                map["QuickHeal"] = new List<string> { "K" };
                map["SmartCursor"] = new List<string> { "LeftControl" };
                var report = VanillaHotkeyConflicts.Check(action, Parse("LeftControl+LeftShift+K"));
                Require(report.Complete && report.Items.Count == 3, "main and both modifiers report every distinct action");
                Require(report.Items[0].Id == "mapping:QuickHeal" && report.Items[1].Id == "mapping:SmartCursor" && report.Items[2].Id == "mapping:SmartSelect", "deterministic source/action ordering");
                Require(report.Items[2].Keys.Count == 2 && report.Items[2].Keys[0] == "LShift" && report.Items[2].Keys[1] == "K", "same action merges distinct matching keys, without duplicate tokens");
                string originalReport = report.Message;
                var reversed = new List<KeyValuePair<string, List<string>>>(map); reversed.Reverse(); map.Clear();
                foreach (var row in reversed) map.Add(row.Key, row.Value);
                Require(VanillaHotkeyConflicts.Check(action, Parse("LeftControl+LeftShift+K")).Message == originalReport, "dictionary insertion order does not change results");
                map["QuickHeal"].Clear(); map["QuickMana"] = null;
                var partial = VanillaHotkeyConflicts.Check(action, Parse("LeftControl+LeftShift+K"));
                Require(!partial.Complete && partial.Items.Count == 2 && partial.UncheckedParts.Count == 1 && partial.Summary.Contains("未完整"), "unread supported row preserves known matches before and after it");
                Require(report.Message == originalReport && report.Items.Count == 3, "result does not retain mutable profile lists");
                map.Clear(); map["MenuUp"] = null;
                Require(VanillaHotkeyConflicts.Check(action, Parse("K")).Complete, "irrelevant menu row does not mark supported gameplay scope unreadable");
                map["SmartCursor"] = new List<string> { "LeftControl", "F7" };
                report = VanillaHotkeyConflicts.Check(action, Parse("LeftControl+F7"));
                Require(report.Items.Count == 2 && report.Items[0].Id == "fixed:F7" && report.Items[1].Keys.Count == 2, "fixed handler plus mapped main and modifier are all retained");
                PlayerInput.CurrentProfile = null;
                report = VanillaHotkeyConflicts.Check(action, Parse("LeftControl+F7"));
                Require(!report.Complete && report.Items.Count == 1 && report.Items[0].Id == "fixed:F7", "unavailable profile does not erase independent fixed handler");
                PlayerInput.CurrentProfile = profile; map.Clear();
                map["SmartCursor"] = new List<string> { "K" }; map["SmartSelect"] = new List<string> { "K" };
                Localization.Language.Values["LegacyMenu.160"] = Localization.Language.Values["LegacyMenu.161"] = "相同名称";
                report = VanillaHotkeyConflicts.Check(action, Parse("K"));
                Require(report.Items.Count == 2 && report.Items[0].Id != report.Items[1].Id && report.Items[0].Name == report.Items[1].Name, "display labels are not action identity");
                var keys = new List<string> { "K" }; var items = new List<HotkeyOverlap> { new HotkeyOverlap("mapping:a", "动作", keys) }; var gaps = new List<string> { "缺项" };
                report = new HotkeyAdvisory(items, gaps); keys.Clear(); items.Clear(); gaps.Clear();
                Require(report.Items.Count == 1 && report.Items[0].Keys.Count == 1 && report.UncheckedParts.Count == 1, "public result owns immutable collection snapshots");
            }
            finally { PlayerInput.CurrentProfile = original; Localization.Language.Values.Clear(); }
        }
        private static HotkeyChord Parse(string text) { HotkeyChord c; string reason; if (!HotkeyChord.TryParse(text, out c, out reason)) throw new Exception(reason); return c; }
        private static void Require(bool value, string reason) { if (!value) throw new Exception("Hotkey complete advisory: " + reason); }
    }
}
