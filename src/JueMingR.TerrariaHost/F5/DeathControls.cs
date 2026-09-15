using System;
using System.Collections.Generic;
using JueMingR.Features.DeathHistory;
using JueMingR.Platform.DeathHistory;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal interface IDeathControls
    {
        long Session { get; }
        bool ControlsEnabled { get; }
        DeathDisplayPreferences Settings { get; }
        DeathHistorySnapshot Snapshot { get; }
        bool QueryReady { get; }
        string CountText { get; }
        string DaysText { get; }
        string PreferenceMessage { get; }
        bool SetEnabled(bool value);
        bool SetCount(int value);
        void RequestDetails(long offset);
        void RequestSelection(string id);
        void TakeFeedback(Action<string> display);
    }
    internal sealed class DeathControls
    {
        internal const string ActionId = "death-markers.toggle";
        private static readonly F5RowDescription count = new F5RowDescription("death-history", "查看这个角色在当前世界的死亡次数和记录。");
        private static readonly F5RowDescription days = new F5RowDescription("world-time", "累计这个角色在当前世界经历的游戏时间，包含睡觉等时间加速。");
        private static readonly F5RowDescription markers = new F5RowDescription(ActionId, "在大地图显示这个角色的历史死亡点，可设置显示数量。");
        private readonly IDeathControls host;
        internal DeathControls(IDeathControls host) { this.host = host; }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y, string countText, string daysText)
        {
            var rows = new F5RowLayout(elements, measure);
            rows.Row(ref y, 0, 522, "死亡信息：" + countText, new[] { "详情" }, _ => F5Command.DeathDetails, count);
            rows.Row(ref y, 0, 522, "世界天数：" + daysText, new string[0], description: days);
            rows.Row(ref y, 0, 522, "死亡点常驻", new[] { "配置", "开启", "关闭", "键" }, text => text == "配置" ? F5Command.DeathConfigure : text == "开启" ? F5Command.DeathEnable : text == "关闭" ? F5Command.DeathDisable : F5Command.None, markers);
            var key = elements[elements.Count - 1];
            elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, ActionId);
        }
        internal static bool Owns(F5Command command) { return command >= F5Command.DeathDetails && command <= F5Command.DeathDisable; }
        internal bool Available(F5Command command) { return Owns(command) && (command == F5Command.DeathDetails ? host.Session >= 0 : host.ControlsEnabled); }
        internal Color? Selected(F5Command command)
        { return command == F5Command.DeathEnable && host.Settings.Enabled ? (Color?)Color.LightGreen : command == F5Command.DeathDisable && !host.Settings.Enabled ? (Color?)Color.IndianRed : null; }
        internal void Execute(F5Command command) { if (Available(command) && (command == F5Command.DeathEnable || command == F5Command.DeathDisable)) host.SetEnabled(command == F5Command.DeathEnable); }
        internal string Hint(F5Command command) { return Owns(command) && !Available(command) ? "当前暂不可用。" : null; }
    }
}
