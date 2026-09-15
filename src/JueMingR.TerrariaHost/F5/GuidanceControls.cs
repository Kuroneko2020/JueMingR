using System;
using System.Collections.Generic;
using JueMingR.Features.Guidance;
using JueMingR.TerrariaHost.Hotkeys;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal interface IGuidanceControls
    {
        bool ControlsEnabled { get; }
        bool IsEnabled(GuidanceKind kind);
        bool SetEnabled(GuidanceKind kind, bool enabled);
        GuidancePreferences Settings { get; }
        string PreferenceMessage { get; }
        bool SetColor(GuidanceKind kind, int rgb);
        bool StepSize(GuidanceKind kind, int direction);
        void ResetStyle(GuidanceKind kind);
        string SummonReason { get; }
        void RequestMerchant();
    }
    internal sealed class GuidanceControls
    {
        private static readonly F5RowDescription[] descriptions = {
            new F5RowDescription(HotkeyActionIds.RareDirection, "箭头指向附近稀有生物，需要生命体分析仪。"),
            new F5RowDescription(HotkeyActionIds.MerchantDirection, "显示画面外旅商大致位置。"),
            new F5RowDescription(HotkeyActionIds.EquipmentWarning, "Boss战或指定事件中，提醒正在穿戴的部分非战斗用品。血月本身不触发。"),
            new F5RowDescription("merchant-test.once", "在单人世界尝试一次原版条件下的旅商到访。") };
        private readonly IGuidanceControls host;
        internal GuidanceControls(IGuidanceControls host) { this.host = host; }
        internal static string Name(GuidanceKind kind) { return kind == GuidanceKind.Rare ? "稀有生物方向" : kind == GuidanceKind.Merchant ? "旅商方向" : "装备提示"; }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y, int page)
        {
            var rows = new F5RowLayout(elements, measure);
            if (page == 1)
            {
                rows.Row(ref y, 0, 522, "游商测试", new[] { "召唤" }, text => F5Command.SummonMerchant, descriptions[3]);
                y += 6; return;
            }
            int first = page == 2 ? 0 : 2, last = page == 2 ? 1 : 2;
            for (int i = first; i <= last; i++)
            {
                int index = i;
                rows.Row(ref y, 0, 522, Name((GuidanceKind)i), i < 2 ? new[] { "配置", "开启", "关闭", "键" } : new[] { "开启", "关闭", "键" },
                    text => text == "配置" ? (index == 0 ? F5Command.ConfigureRare : F5Command.ConfigureMerchant) : text == "开启" ? (F5Command)((int)F5Command.EnableRare + index * 2) : text == "关闭" ? (F5Command)((int)F5Command.DisableRare + index * 2) : F5Command.None, descriptions[i]);
                var key = elements[elements.Count - 1];
                elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, HotkeyActionIds.Guidance[i]);
            }
        }
        internal static bool Owns(F5Command command) { return command >= F5Command.EnableRare && command <= F5Command.SummonMerchant || IsStyle(command); }
        internal static bool IsStyle(F5Command command) { return command == F5Command.ConfigureRare || command == F5Command.ConfigureMerchant; }
        internal static GuidanceKind? Target(F5Command command) { return command == F5Command.ConfigureRare ? GuidanceKind.Rare : command == F5Command.ConfigureMerchant ? (GuidanceKind?)GuidanceKind.Merchant : null; }
        private static GuidanceKind Kind(F5Command command) { return (GuidanceKind)(((int)command - (int)F5Command.EnableRare) / 2); }
        private static bool Enable(F5Command command) { return ((int)command - (int)F5Command.EnableRare) % 2 == 0; }
        internal bool Available(F5Command command) { return Owns(command) && host.ControlsEnabled && (command != F5Command.SummonMerchant || host.SummonReason == null); }
        internal Color? Selected(F5Command command)
        {
            if (!Owns(command) || command == F5Command.SummonMerchant || IsStyle(command)) return null;
            bool enabled = Enable(command); return host.IsEnabled(Kind(command)) == enabled ? (Color?)(enabled ? Color.LightGreen : Color.IndianRed) : null;
        }
        internal void Execute(F5Command command)
        { if (!Available(command) || IsStyle(command)) return; if (command == F5Command.SummonMerchant) host.RequestMerchant(); else host.SetEnabled(Kind(command), Enable(command)); }
        internal string Hint(F5Command command) { return !Owns(command) ? null : command == F5Command.SummonMerchant ? host.SummonReason : !host.ControlsEnabled ? "当前暂不可用。" : IsStyle(command) ? "设置文字颜色和字号" : null; }
    }
}
