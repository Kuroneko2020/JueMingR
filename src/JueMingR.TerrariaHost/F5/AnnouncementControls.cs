using System;
using System.Collections.Generic;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal interface IAnnouncementControls
    {
        bool Enabled { get; }
        bool CanConfigure { get; }
        string SettingsMessage { get; }
        bool SetEnabled(bool value);
    }
    internal sealed class AnnouncementControls
    {
        internal const string SendActionId = "announcement.send", ToggleActionId = "announcement.toggle";
        private readonly IAnnouncementControls host;
        private readonly HotkeyBindings bindings;
        private string hintedValue, sendHint;
        internal AnnouncementControls(IAnnouncementControls host, HotkeyBindings bindings) { this.host = host; this.bindings = bindings; }
        internal string SendBindingText
        {
            get
            {
                if (bindings == null || !bindings.Loaded) return "正在加载";
                var chord = bindings.Get(SendActionId); if (chord != null) return chord.DisplayText;
                return bindings.Error(SendActionId) != null ? "绑定无效" : bindings.Protected ? "文件已保护" : "未设置触发键";
            }
        }
        internal string SendHint
        {
            get { string value = SendBindingText; if (hintedValue != value) { hintedValue = value; sendHint = "双击设置宣告触发快捷键；当前：" + value; } return sendHint; }
        }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y)
        {
            var description = new F5RowDescription(ToggleActionId, "按自设触发键宣告指向内容；左框设置触发键，右侧键盘设置功能开关快捷键。普通多人客户端使用原版聊天。");
            new F5RowLayout(elements, measure).Row(ref y, 0, 522, "快捷宣告", new[] { null, "开启", "关闭", "键" }, text => text == "开启" ? F5Command.AnnouncementEnable : text == "关闭" ? F5Command.AnnouncementDisable : F5Command.None, description);
            var field = elements[elements.Count - 4]; elements[elements.Count - 4] = new F5Element(field.Kind, field.Rect, field.Text, field.TextSize, field.TextScale, F5Command.None, SendActionId);
            var key = elements[elements.Count - 1]; elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, ToggleActionId);
        }
        internal static bool Owns(F5Command command) { return command == F5Command.AnnouncementEnable || command == F5Command.AnnouncementDisable; }
        internal bool Available(F5Command command) { return Owns(command) && host.CanConfigure; }
        internal Color? Selected(F5Command command) { return command == F5Command.AnnouncementEnable && host.Enabled ? (Color?)Color.LightGreen : command == F5Command.AnnouncementDisable && !host.Enabled ? (Color?)Color.IndianRed : null; }
        internal void Execute(F5Command command) { if (Available(command)) host.SetEnabled(command == F5Command.AnnouncementEnable); }
        internal string Hint(F5Command command) { return Owns(command) ? host.SettingsMessage : null; }
    }
}
