using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal interface IAnnouncementControls
    {
        bool Enabled { get; }
        bool CanConfigure { get; }
        bool SetEnabled(bool value);
    }
    internal sealed class AnnouncementControls
    {
        internal const string ActionId = "announcement.send";
        private readonly IAnnouncementControls host;
        internal AnnouncementControls(IAnnouncementControls host) { this.host = host; }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y)
        {
            var description = new F5RowDescription(ActionId, "按自设快捷键宣告指向内容；默认关闭且未绑定。普通多人客户端使用原版聊天。 ");
            new F5RowLayout(elements, measure).Row(ref y, 0, 522, "快捷宣告", new[] { "开启", "关闭", "键" }, text => text == "开启" ? F5Command.AnnouncementEnable : text == "关闭" ? F5Command.AnnouncementDisable : F5Command.None, description);
            var key = elements[elements.Count - 1]; elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, ActionId);
        }
        internal static bool Owns(F5Command command) { return command == F5Command.AnnouncementEnable || command == F5Command.AnnouncementDisable; }
        internal bool Available(F5Command command) { return Owns(command) && host.CanConfigure; }
        internal Color? Selected(F5Command command) { return command == F5Command.AnnouncementEnable && host.Enabled ? (Color?)Color.LightGreen : command == F5Command.AnnouncementDisable && !host.Enabled ? (Color?)Color.IndianRed : null; }
        internal void Execute(F5Command command) { if (Available(command)) host.SetEnabled(command == F5Command.AnnouncementEnable); }
        internal string Hint(F5Command command) { return Owns(command) && !Available(command) ? "宣告设置暂不可用。" : null; }
    }
}
