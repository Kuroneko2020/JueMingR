using System;
using System.Collections.Generic;

namespace JueMingR.Platform.Hotkeys
{
    [Flags]
    public enum HotkeyModifiers { None = 0, LeftControl = 1, RightControl = 2, LeftShift = 4, RightShift = 8, LeftAlt = 16, RightAlt = 32 }

    // Codes are the verified XNA virtual-key identities, not characters or scan
    // codes. Mouse identities occupy their own range; wheel is never a key.
    public sealed class HotkeyChord : IEquatable<HotkeyChord>
    {
        public const int KeyCount = 261;
        private static readonly string[] names = BuildNames();
        private static readonly Dictionary<string, int> codes = BuildCodes();
        private static readonly int[] modifierCodes = { 162, 163, 160, 161, 164, 165 };
        public int MainKey { get; }
        public HotkeyModifiers Modifiers { get; }
        public string Text { get; }
        public string DisplayText { get; }
        private HotkeyChord(int key, HotkeyModifiers modifiers)
        { MainKey = key; Modifiers = modifiers; Text = ModifierText(modifiers) + names[key]; DisplayText = ModifierDisplay(modifiers) + DisplayName(key); }
        public static string KeyName(int code) { return code >= 0 && code < names.Length ? names[code] : null; }
        public static string DisplayName(int code)
        { return code == 8 ? "Backspace" : code >= 256 && code <= 260 ? new[] { "鼠标左键", "鼠标右键", "鼠标中键", "鼠标侧键1", "鼠标侧键2" }[code - 256] : KeyName(code); }
        public static string ModifierDisplay(HotkeyModifiers modifiers)
        {
            string[] labels = { "LCtrl", "RCtrl", "LShift", "RShift", "LAlt", "RAlt" }; string result = "";
            for (int i = 0; i < labels.Length; i++) if (((int)modifiers & (1 << i)) != 0) result += labels[i] + "+";
            return result;
        }
        public static HotkeyModifiers Modifier(int code)
        { for (int i = 0; i < modifierCodes.Length; i++) if (modifierCodes[i] == code) return (HotkeyModifiers)(1 << i); return HotkeyModifiers.None; }
        public static int ModifierCode(int index) { return modifierCodes[index]; }
        public static bool TryCreate(int key, HotkeyModifiers modifiers, out HotkeyChord value, out string reason)
        {
            value = null; reason = null;
            int bits = (int)modifiers, count = 0;
            for (int i = 0; i < 6; i++) if ((bits & (1 << i)) != 0) count++;
            if ((bits & ~63) != 0 || count > 3) reason = "最多三个左右独立修饰键。";
            else if (key == 27) reason = "Esc 用于取消录入。";
            else if (key == 116) reason = "F5 已保留给控制界面。";
            else if (KeyName(key) == null || Modifier(key) != HotkeyModifiers.None) reason = "需要一个当前输入接口支持的键盘或鼠标主键。";
            if (reason != null) return false;
            value = new HotkeyChord(key, modifiers); return true;
        }
        public static bool TryParse(string text, out HotkeyChord value, out string reason)
        {
            value = null; reason = "组合格式无效：请选择零至三个左右修饰键和一个主键。";
            if (String.IsNullOrEmpty(text) || text.Length > 128) return false;
            string[] parts = text.Split('+'); if (parts.Length > 4) return false;
            HotkeyModifiers modifiers = HotkeyModifiers.None;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                int code; if (!codes.TryGetValue(parts[i], out code)) return false;
                HotkeyModifiers bit = Modifier(code);
                if (bit == HotkeyModifiers.None || (modifiers & bit) != 0) return false;
                modifiers |= bit;
            }
            int key; if (!codes.TryGetValue(parts[parts.Length - 1], out key)) return false;
            return TryCreate(key, modifiers, out value, out reason);
        }
        public static string ModifierText(HotkeyModifiers modifiers)
        {
            string result = "";
            for (int i = 0; i < 6; i++) if (((int)modifiers & (1 << i)) != 0) result += names[modifierCodes[i]] + "+";
            return result;
        }
        public bool Equals(HotkeyChord other) { return other != null && MainKey == other.MainKey && Modifiers == other.Modifiers; }
        public override bool Equals(object obj) { return Equals(obj as HotkeyChord); }
        public override int GetHashCode() { return MainKey * 64 + (int)Modifiers; }
        private static Dictionary<string, int> BuildCodes()
        { var result = new Dictionary<string, int>(StringComparer.Ordinal); for (int i = 0; i < names.Length; i++) if (names[i] != null) result.Add(names[i], i); return result; }
        private static string[] BuildNames()
        {
            var result = new string[KeyCount];
            string[] fixedNames = { "8:Back", "9:Tab", "13:Enter", "19:Pause", "20:CapsLock", "27:Escape", "32:Space", "33:PageUp", "34:PageDown", "35:End", "36:Home", "37:Left", "38:Up", "39:Right", "40:Down", "41:Select", "42:Print", "43:Execute", "44:PrintScreen", "45:Insert", "46:Delete", "47:Help", "93:Apps", "106:Multiply", "107:Add", "108:Separator", "109:Subtract", "110:Decimal", "111:Divide", "144:NumLock", "145:Scroll", "160:LeftShift", "161:RightShift", "162:LeftControl", "163:RightControl", "164:LeftAlt", "165:RightAlt", "166:BrowserBack", "167:BrowserForward", "168:BrowserRefresh", "169:BrowserStop", "170:BrowserSearch", "171:BrowserFavorites", "172:BrowserHome", "173:VolumeMute", "174:VolumeDown", "175:VolumeUp", "176:MediaNextTrack", "177:MediaPreviousTrack", "178:MediaStop", "179:MediaPlayPause", "180:LaunchMail", "181:SelectMedia", "182:LaunchApplication1", "183:LaunchApplication2", "186:OemSemicolon", "187:OemPlus", "188:OemComma", "189:OemMinus", "190:OemPeriod", "191:OemQuestion", "192:OemTilde", "219:OemOpenBrackets", "220:OemPipe", "221:OemCloseBrackets", "222:OemQuotes", "223:Oem8", "226:OemBackslash", "254:OemClear", "256:Mouse1", "257:Mouse2", "258:Mouse3", "259:Mouse4", "260:Mouse5" };
            foreach (string item in fixedNames) { int colon = item.IndexOf(':'); result[Int32.Parse(item.Substring(0, colon), System.Globalization.CultureInfo.InvariantCulture)] = item.Substring(colon + 1); }
            for (int i = 48; i <= 57; i++) result[i] = "D" + (i - 48);
            for (int i = 65; i <= 90; i++) result[i] = ((char)i).ToString();
            for (int i = 96; i <= 105; i++) result[i] = "NumPad" + (i - 96);
            for (int i = 112; i <= 135; i++) result[i] = "F" + (i - 111);
            return result;
        }
    }
}
