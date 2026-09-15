using System;
using System.Collections.Generic;
using JueMingR.Features.Information;
using JueMingR.Platform.Information;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Hotkeys;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Information
{
    // The shared editor needs preference commands, not native observation or
    // the concrete lifetime owner. This is also its real fixture boundary.
    internal interface IInformationControls
    {
        InformationPreferences Settings { get; }
        bool CanConfigure { get; }
        bool PositionReady { get; }
        string PreferenceMessage { get; }
        string PositionMessage { get; }
        bool Enabled(InformationKind kind);
        bool SetEnabled(InformationKind kind, bool enabled);
        bool SetColor(InformationKind kind, int rgb);
        bool StepSize(InformationKind kind, int direction);
        void ResetStyle(InformationKind kind);
    }
    internal sealed class InformationControls
    {
        private static readonly F5RowDescription[] descriptions = {
            new F5RowDescription(HotkeyActionIds.Information(InformationKind.Biome), "显示当前所在的群系。"),
            new F5RowDescription(HotkeyActionIds.Information(InformationKind.Infection), "显示世界感染比例。需要当前世界存在树妖。"),
            new F5RowDescription(HotkeyActionIds.Information(InformationKind.Luck), "显示当前幸运值及来源明细。需要在此世界解救过巫师，或当前有巫师。"),
            new F5RowDescription(HotkeyActionIds.Information(InformationKind.Angler), "显示今日任务鱼、捕获地点、个人累计完成次数和今日完成状态。需要在此世界解救过渔夫，或当前有渔夫。") };
        private static readonly F5RowDescription adjustmentDescription = new F5RowDescription(HotkeyActionIds.AdjustInformation,
            "调整信息窗的位置，拖动后松手完成。");
        internal static F5RowDescription Description(InformationKind? kind)
        { return kind.HasValue ? descriptions[(int)kind.Value] : adjustmentDescription; }
        private readonly IInformationControls host;
        internal InformationControls(IInformationControls host) { this.host = host; }
        internal static string Name(InformationKind kind)
        { switch (kind) { case InformationKind.Biome: return "群系显示"; case InformationKind.Infection: return "世界感染"; case InformationKind.Luck: return "幸运值"; default: return "渔夫任务"; } }
        internal static void AddRow(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y, InformationKind? kind)
        {
            new F5RowLayout(elements, measure).Row(ref y, 0, 522, kind.HasValue ? Name(kind.Value) : "调整信息窗位置",
                kind.HasValue ? new[] { "配置", "开启", "关闭", "键" } : new[] { "开始", "键" },
                text => text == "键" ? F5Command.None : !kind.HasValue ? F5Command.AdjustInformation : Command(kind.Value, text), Description(kind));
            F5Element key = elements[elements.Count - 1];
            elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None,
                kind.HasValue ? HotkeyActionIds.Information(kind.Value) : HotkeyActionIds.AdjustInformation);
        }
        private static F5Command Command(InformationKind kind, string text)
        {
            switch (kind)
            {
                case InformationKind.Biome: return text == "配置" ? F5Command.ConfigureBiome : text == "开启" ? F5Command.EnableBiome : F5Command.DisableBiome;
                case InformationKind.Infection: return text == "配置" ? F5Command.ConfigureInfection : text == "开启" ? F5Command.EnableInfection : F5Command.DisableInfection;
                case InformationKind.Luck: return text == "配置" ? F5Command.ConfigureLuck : text == "开启" ? F5Command.EnableLuck : F5Command.DisableLuck;
                default: return text == "配置" ? F5Command.ConfigureAngler : text == "开启" ? F5Command.EnableAngler : F5Command.DisableAngler;
            }
        }
        internal static InformationKind? Target(F5Command command)
        {
            switch (command)
            {
                case F5Command.ConfigureBiome: case F5Command.EnableBiome: case F5Command.DisableBiome: return InformationKind.Biome;
                case F5Command.ConfigureInfection: case F5Command.EnableInfection: case F5Command.DisableInfection: return InformationKind.Infection;
                case F5Command.ConfigureLuck: case F5Command.EnableLuck: case F5Command.DisableLuck: return InformationKind.Luck;
                case F5Command.ConfigureAngler: case F5Command.EnableAngler: case F5Command.DisableAngler: return InformationKind.Angler;
                default: return null;
            }
        }
        internal static bool IsStyle(F5Command command) { return command == F5Command.ConfigureBiome || command == F5Command.ConfigureInfection || command == F5Command.ConfigureLuck || command == F5Command.ConfigureAngler; }
        private static bool IsEnable(F5Command command) { return command == F5Command.EnableBiome || command == F5Command.EnableInfection || command == F5Command.EnableLuck || command == F5Command.EnableAngler; }
        internal bool Available(F5Command command) { return command == F5Command.AdjustInformation ? host.PositionReady : Target(command).HasValue && host.CanConfigure; }
        internal Color? Selected(F5Command command)
        {
            var kind = Target(command);
            return kind.HasValue && !IsStyle(command) && Available(command) && host.Enabled(kind.Value) == IsEnable(command) ?
                (Color?)(IsEnable(command) ? Color.LightGreen : Color.IndianRed) : null;
        }
        internal void Execute(F5Command command) { var kind = Target(command); if (kind.HasValue && !IsStyle(command) && Available(command)) host.SetEnabled(kind.Value, IsEnable(command)); }
        internal string Hint(F5Command command)
        {
            if (!Target(command).HasValue && command != F5Command.AdjustInformation) return null;
            if (!Available(command)) return (command == F5Command.AdjustInformation ? host.PositionMessage : host.PreferenceMessage) ?? "信息设置暂不可用";
            return null;
        }
    }
}
