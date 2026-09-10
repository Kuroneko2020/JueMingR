using System;
using System.Collections.Generic;
using JueMingR.Platform.Hotkeys;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Hotkeys
{
    // Verified against Terraria 1.4.5.8. This adapter is called only while
    // setting a candidate, never by load, update, dispatch or a profile monitor.
    internal static class VanillaHotkeyConflicts
    {
        private static readonly HashSet<string> gameplay = new HashSet<string>(new[] {
            "MouseLeft", "MouseRight", "Up", "Down", "Left", "Right", "Jump", "Grapple", "SmartSelect", "SmartCursor",
            "QuickMount", "QuickHeal", "QuickMana", "QuickBuff", "Throw", "Inventory", "ViewZoomIn", "ViewZoomOut",
            "Loadout1", "Loadout2", "Loadout3", "NextLoadout", "PreviousLoadout", "ToggleCreativeMenu", "ToggleCameraMode", "ArmorSetAbility", "Dash",
            "HotbarMinus", "HotbarPlus", "Hotbar1", "Hotbar2", "Hotbar3", "Hotbar4", "Hotbar5", "Hotbar6", "Hotbar7", "Hotbar8", "Hotbar9", "Hotbar10",
            "MapZoomIn", "MapZoomOut", "MapAlphaUp", "MapAlphaDown", "MapFull", "MapStyle", "DpadRadial1", "DpadRadial2", "DpadRadial3", "DpadRadial4", "RadialHotbar", "RadialQuickbar"
        }, StringComparer.Ordinal);
        internal static string Check(HotkeyAction action, HotkeyChord chord)
        {
            try
            {
                var profile = PlayerInput.CurrentProfile;
                KeyConfiguration keyboard;
                if (profile == null || profile.InputModes == null || !profile.InputModes.TryGetValue(InputMode.Keyboard, out keyboard) || keyboard == null || keyboard.KeyStatus == null)
                    return "无法读取当前原版键盘配置，旧绑定保留。";
                int key = chord.MainKey;
                bool shift = (chord.Modifiers & (HotkeyModifiers.LeftShift | HotkeyModifiers.RightShift)) != 0;
                bool alt = (chord.Modifiers & (HotkeyModifiers.LeftAlt | HotkeyModifiers.RightAlt)) != 0;
                if (key == 118 || key == 119 || key == 121 || key == 122) return "与原版固定功能 " + HotkeyChord.KeyName(key) + " 冲突，额外修饰键不会阻止该功能。";
                if (key == 120 && shift) return "与原版 Shift+F9 光照切换冲突。";
                if (key == 13) return alt ? "与原版 Alt+Enter 全屏切换冲突。" : "与原版 Enter 打开聊天冲突。";
                if (key >= 96 && key <= 105 && Terraria.Testing.DebugOptions.enableDebugCommands) return "与已开启的原版数字区调试命令冲突。";
                foreach (var mapping in keyboard.KeyStatus)
                {
                    if (!gameplay.Contains(mapping.Key) && !(mapping.Key == "LockOn" && LockOnHelper.ForceUsability)) continue;
                    if (mapping.Value == null) return "原版键位数据不完整，旧绑定保留。";
                    // Processkey ignores modifiers. Each held modifier can itself
                    // trigger a mapped action (default LeftControl/LeftShift).
                    if (ReportsToken(key, shift, alt) && mapping.Value.Contains(HotkeyChord.KeyName(key))) return Conflict(mapping.Key, HotkeyChord.KeyName(key));
                    for (int i = 0; i < 6; i++)
                        if (((int)chord.Modifiers & (1 << i)) != 0 && mapping.Value.Contains(HotkeyChord.KeyName(HotkeyChord.ModifierCode(i))))
                            return Conflict(mapping.Key, HotkeyChord.KeyName(HotkeyChord.ModifierCode(i)));
                }
                return null;
            }
            catch { return "无法可靠读取当前原版键位，旧绑定保留。"; }
        }
        private static bool ReportsToken(int key, bool shift, bool alt)
        { return key != 9 || !alt && (Terraria.Social.SocialAPI.Mode != Terraria.Social.SocialMode.Steam || !shift); }
        private static string Conflict(string action, string token)
        { return "「" + token + "」同时触发原版「" + action + "」，请更换主键或修饰键。"; }
    }
}
