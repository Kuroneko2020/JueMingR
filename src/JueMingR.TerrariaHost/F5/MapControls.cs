using System;
using System.Collections.Generic;
using JueMingR.Features.MapMarkers;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal interface IMapControls
    {
        long Session { get; }
        bool ControlsEnabled { get; }
        bool MarkersEnabled { get; }
        bool DynamicEnabled { get; }
        bool FastScan { get; set; }
        bool ScanPaused { get; }
        bool ScanActive { get; }
        string ExplorationText { get; }
        string ScanText { get; }
        string StatusMessage { get; }
        MarkerLibrary Markers { get; }
        MarkerWorkspace Workspace { get; }
        bool SetMarkers(bool value);
        bool SetDynamic(bool value);
        void PauseScan(bool pause);
        void Recount();
        bool Locate(string id);
        void TakeFeedback(Action<string> display);
    }
    internal sealed class MapControls
    {
        internal const string ActionId = "map-markers.toggle";
        private static readonly F5RowDescription markers = new F5RowDescription(ActionId, "大地图新按右键选点，选择图标创建标记；管理中可改名、定位或删除。");
        private static readonly F5RowDescription exploration = new F5RowDescription("exploration", "当前本地角色揭示的地图比例。详情中可暂停、重新统计或开启动态更新。");
        private readonly IMapControls host;
        internal MapControls(IMapControls host) { this.host = host; }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y)
        {
            var rows = new F5RowLayout(elements, measure);
            rows.Row(ref y, 0, 522, "地图标记", new[] { "管理", "开启", "关闭", "键" }, text => text == "管理" ? F5Command.MarkerManage : text == "开启" ? F5Command.MarkerEnable : text == "关闭" ? F5Command.MarkerDisable : F5Command.None, markers);
            var key = elements[elements.Count - 1]; elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, ActionId);
            int start = elements.Count;
            rows.Row(ref y, 0, 522, "揭示区域统计", new[] { "详情" }, _ => F5Command.ExplorationDetails, exploration);
            // The changing summary shares the feature row but never enters the
            // permanent text cache or invalidates a held details action.
            var label = elements[start + 1]; var action = elements[elements.Count - 1]; var panel = elements[start];
            float left = label.Rect.Right + 16;
            elements.Add(new F5Element(F5ElementKind.Text, new F5Rect(left, panel.Rect.Y, action.Rect.X - left - 12, panel.Rect.Height), "", default(F5Size), .70f, F5Command.ExplorationValue));
        }
        internal static bool Owns(F5Command command) { return command >= F5Command.MarkerManage && command <= F5Command.ExplorationValue; }
        internal bool Available(F5Command command) { return host.ControlsEnabled && command != F5Command.ExplorationValue; }
        internal Color? Selected(F5Command command) { return command == F5Command.MarkerEnable && host.MarkersEnabled ? (Color?)Color.LightGreen : command == F5Command.MarkerDisable && !host.MarkersEnabled ? (Color?)Color.IndianRed : null; }
        internal void Execute(F5Command command) { if (Available(command) && (command == F5Command.MarkerEnable || command == F5Command.MarkerDisable)) host.SetMarkers(command == F5Command.MarkerEnable); }
        internal string Value { get { return host.ExplorationText; } }
        internal string Hint(F5Command command) { return Owns(command) && !Available(command) ? "当前暂不可用。" : null; }
    }
}
