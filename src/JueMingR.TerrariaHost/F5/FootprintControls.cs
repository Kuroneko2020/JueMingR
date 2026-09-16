using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal interface IFootprintControls
    {
        long Session { get; }
        bool ControlsEnabled { get; }
        bool Display { get; }
        bool Recording { get; }
        string Generation { get; }
        string StatusMessage { get; }
        bool CanClear { get; }
        bool Clearing { get; }
        bool CanRetryClear { get; }
        bool CanRetrySave { get; }
        bool HasIssue { get; }
        bool SetDisplay(bool value);
        bool SetRecording(bool value);
        bool Clear(string generation);
        void RetryClear();
        void RetrySave();
        void TakeFeedback(Action<string> show);
        void PrepareConfiguration();
    }
    internal sealed class FootprintControls
    {
        internal const string ActionId = "footprints.toggle";
        private static readonly F5RowDescription description = new F5RowDescription(ActionId, "在大地图查看这个角色在当前世界的路线；录制开关和清除在配置中。");
        private readonly IFootprintControls host;
        internal FootprintControls(IFootprintControls host) { this.host = host; }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y)
        {
            new F5RowLayout(elements, measure).Row(ref y, 0, 522, "足迹", new[] { "配置", "开启", "关闭", "键" }, text => text == "配置" ? F5Command.FootprintConfigure : text == "开启" ? F5Command.FootprintEnable : text == "关闭" ? F5Command.FootprintDisable : F5Command.None, description);
            var key = elements[elements.Count - 1]; elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, ActionId);
        }
        internal static bool Owns(F5Command command) { return command >= F5Command.FootprintConfigure && command <= F5Command.FootprintDisable; }
        internal bool Available(F5Command command) { return Owns(command) && host.ControlsEnabled; }
        internal Color? Selected(F5Command command) { return command == F5Command.FootprintEnable && host.Display ? (Color?)Color.LightGreen : command == F5Command.FootprintDisable && !host.Display ? (Color?)Color.IndianRed : null; }
        internal void Execute(F5Command command) { if (Available(command) && command != F5Command.FootprintConfigure) host.SetDisplay(command == F5Command.FootprintEnable); }
        internal string Hint(F5Command command) { return Owns(command) && !Available(command) ? "足迹暂不可用。" : null; }
    }
}
