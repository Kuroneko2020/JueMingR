using System;
using System.Collections.Generic;
using JueMingR.Platform.Hotkeys;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Hotkeys
{
    // Verified against Terraria 1.4.5.8. This adapter is called only while
    // setting a candidate, never by load, update, dispatch or a profile monitor.
    // Every result is advisory: overlaps and unreadable mappings do not veto saves.
    internal static class VanillaHotkeyConflicts
    {
        private static readonly HashSet<string> gameplay = new HashSet<string>(new[] {
            "MouseLeft", "MouseRight", "Up", "Down", "Left", "Right", "Jump", "Grapple", "SmartSelect", "SmartCursor",
            "QuickMount", "QuickHeal", "QuickMana", "QuickBuff", "Throw", "Inventory", "ViewZoomIn", "ViewZoomOut",
            "Loadout1", "Loadout2", "Loadout3", "NextLoadout", "PreviousLoadout", "ToggleCreativeMenu", "ToggleCameraMode", "ArmorSetAbility", "Dash",
            "HotbarMinus", "HotbarPlus", "Hotbar1", "Hotbar2", "Hotbar3", "Hotbar4", "Hotbar5", "Hotbar6", "Hotbar7", "Hotbar8", "Hotbar9", "Hotbar10",
            "MapZoomIn", "MapZoomOut", "MapAlphaUp", "MapAlphaDown", "MapFull", "MapStyle", "DpadRadial1", "DpadRadial2", "DpadRadial3", "DpadRadial4", "RadialHotbar", "RadialQuickbar"
        }, StringComparer.Ordinal);
        internal static HotkeyAdvisory Check(HotkeyAction action, HotkeyChord chord)
        {
            var found = new SortedDictionary<string, HotkeyOverlap>(StringComparer.Ordinal);
            var unread = new List<string>();
            int key = chord.MainKey;
            bool shift = (chord.Modifiers & (HotkeyModifiers.LeftShift | HotkeyModifiers.RightShift)) != 0;
            bool alt = (chord.Modifiers & (HotkeyModifiers.LeftAlt | HotkeyModifiers.RightAlt)) != 0;
            // Fixed handlers are independent of the editable profile. Failure to
            // read one part must not discard facts already collected elsewhere.
            if (key == 118 || key == 119 || key == 121 || key == 122) AddFixed(found, HotkeyChord.KeyName(key));
            if (key == 120 && shift) AddFixed(found, "Shift+F9", chord, HotkeyModifiers.LeftShift | HotkeyModifiers.RightShift);
            if (key == 13) AddFixed(found, alt ? "Alt+Enter" : "Enter", chord, alt ? HotkeyModifiers.LeftAlt | HotkeyModifiers.RightAlt : HotkeyModifiers.None);
            try
            {
                if (key >= 96 && key <= 105 && Terraria.Testing.DebugOptions.enableDebugCommands)
                    found.Add("fixed:Debug" + HotkeyChord.KeyName(key), new HotkeyOverlap("fixed:Debug" + HotkeyChord.KeyName(key), "原版数字区调试入口", new[] { HotkeyChord.DisplayName(key) }));
            }
            catch { unread.Add("未能核对原版数字区调试入口。"); }
            try
            {
                var profile = PlayerInput.CurrentProfile;
                KeyConfiguration keyboard;
                if (profile == null || profile.InputModes == null || !profile.InputModes.TryGetValue(InputMode.Keyboard, out keyboard) || keyboard == null || keyboard.KeyStatus == null)
                    unread.Add("无法读取当前原版键盘配置。");
                else
                {
                    var mappings = keyboard.KeyStatus;
                    var actions = new List<string>(mappings.Keys); actions.Sort(StringComparer.Ordinal);
                    bool mainReports = ReportsToken(key, shift, alt), lockOn = LockOnHelper.ForceUsability;
                    foreach (string id in actions)
                    {
                        if (!gameplay.Contains(id) && !(id == "LockOn" && lockOn)) continue;
                        try
                        {
                            List<string> values;
                            if (!mappings.TryGetValue(id, out values) || values == null)
                            { unread.Add("无法读取原版动作「" + DisplayAction(id) + "」的键位（" + id + "）。"); continue; }
                            var keys = new List<string>(4);
                            for (int i = 0; i < 6; i++)
                                if (((int)chord.Modifiers & (1 << i)) != 0 && values.Contains(HotkeyChord.KeyName(HotkeyChord.ModifierCode(i))))
                                    keys.Add(HotkeyChord.ModifierLabel(i));
                            if (mainReports && values.Contains(HotkeyChord.KeyName(key))) keys.Add(HotkeyChord.DisplayName(key));
                            if (keys.Count != 0) found.Add("mapping:" + id, new HotkeyOverlap("mapping:" + id, DisplayAction(id), keys));
                        }
                        catch { unread.Add("未能完整核对原版动作（" + id + "）。"); }
                    }
                }
            }
            catch { unread.Add("未能完整读取原版键盘配置。"); }
            return new HotkeyAdvisory(found.Values, unread);
        }
        private static void AddFixed(IDictionary<string, HotkeyOverlap> found, string handler, HotkeyChord chord = null, HotkeyModifiers relevant = HotkeyModifiers.None)
        {
            var keys = new List<string>(3);
            if (chord != null)
                for (int i = 0; i < 6; i++) if (((int)(chord.Modifiers & relevant) & (1 << i)) != 0) keys.Add(HotkeyChord.ModifierLabel(i));
            keys.Add(chord == null ? handler : HotkeyChord.DisplayName(chord.MainKey));
            found.Add("fixed:" + handler, new HotkeyOverlap("fixed:" + handler, "原版固定快捷键 " + handler, keys));
        }
        private static bool ReportsToken(int key, bool shift, bool alt)
        { return key != 9 || !alt && (Terraria.Social.SocialAPI.Mode != Terraria.Social.SocialMode.Steam || !shift); }
        // Display keys verified against 1.4.5.8 UIKeybindingListItem.GetFriendlyName.
        // Localization failure is separate from a profile-read failure and never
        // changes detection or vetoes the edit. Resolve only at submit time.
        internal static string DisplayAction(string action)
        {
            string key = null;
            string[] legacy = { "Up", "Down", "Left", "Right", "Jump", "Throw", "Inventory", "Grapple", "QuickMana", "QuickBuff", "QuickMount", "QuickHeal", "SmartSelect", "SmartCursor", "MouseLeft", "MouseRight" };
            int index = Array.IndexOf(legacy, action);
            if (index >= 0) key = "LegacyMenu." + (148 + index);
            else
            {
                string[] map = { "MapZoomIn", "MapZoomOut", "MapAlphaDown", "MapAlphaUp", "MapStyle", "MapFull", "HotbarMinus", "HotbarPlus", "Hotbar1", "Hotbar2", "Hotbar3", "Hotbar4", "Hotbar5", "Hotbar6", "Hotbar7", "Hotbar8", "Hotbar9", "Hotbar10", "DpadRadial1", "DpadRadial2", "DpadRadial3", "DpadRadial4", "RadialHotbar" };
                index = Array.IndexOf(map, action);
                if (index >= 0) key = "LegacyMenu." + (168 + index);
                else if (action == "LockOn") key = "LegacyMenu.231";
                else if (action == "RadialQuickbar") key = "LegacyMenu.244";
                else if (action == "ViewZoomIn") key = "UI.ZoomIn";
                else if (action == "ViewZoomOut") key = "UI.ZoomOut";
                else if (gameplay.Contains(action)) key = "UI." + action;
            }
            try
            {
                string label = key == null ? null : Terraria.Localization.Language.GetTextValue(key);
                if (!String.IsNullOrWhiteSpace(label) && label != key) return label;
            }
            catch { /* Preserve the identifiable action when language assets are unavailable. */ }
            return "原版动作（" + action + "）";
        }
    }
}
