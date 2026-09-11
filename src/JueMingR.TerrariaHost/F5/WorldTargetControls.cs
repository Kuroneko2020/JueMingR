using System;
using System.Collections.Generic;
using JueMingR.Platform.WorldTargets;
using JueMingR.TerrariaHost.Hotkeys;
using JueMingR.TerrariaHost.WorldTargets;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class WorldTargetControls
    {
        private readonly HostWorldTargets host;
        internal WorldTargetControls(HostWorldTargets host) { this.host = host; }
        internal static string Name(WorldTargetKind kind)
        {
            switch (kind)
            {
                case WorldTargetKind.LifeCrystal: return "显示生命水晶";
                case WorldTargetKind.LifeFruit: return "显示生命果";
                case WorldTargetKind.ManaCrystal: return "显示魔力水晶";
                case WorldTargetKind.SleepingDigtoise: return "显示碎岩龟";
                default: return "显示龙蛋";
            }
        }
        internal static void AddRow(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y, WorldTargetKind kind)
        {
            var commands = Commands(kind);
            new F5RowLayout(elements, measure).Row(ref y, 0, 522, Name(kind), new[] { "配置", "开启", "关闭", "键" },
                text => text == "配置" ? commands[0] : text == "开启" ? commands[1] : text == "关闭" ? commands[2] : F5Command.None);
            F5Element key = elements[elements.Count - 1];
            elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, HotkeyActionIds.WorldTarget(kind));
        }
        private static F5Command[] Commands(WorldTargetKind kind)
        {
            switch (kind)
            {
                case WorldTargetKind.LifeCrystal: return new[] { F5Command.ConfigureLifeCrystal, F5Command.EnableLifeCrystal, F5Command.DisableLifeCrystal };
                case WorldTargetKind.LifeFruit: return new[] { F5Command.ConfigureLifeFruit, F5Command.EnableLifeFruit, F5Command.DisableLifeFruit };
                case WorldTargetKind.ManaCrystal: return new[] { F5Command.ConfigureManaCrystal, F5Command.EnableManaCrystal, F5Command.DisableManaCrystal };
                case WorldTargetKind.SleepingDigtoise: return new[] { F5Command.ConfigureDigtoise, F5Command.EnableDigtoise, F5Command.DisableDigtoise };
                default: return new[] { F5Command.ConfigureChilletEgg, F5Command.EnableChilletEgg, F5Command.DisableChilletEgg };
            }
        }
        internal static WorldTargetKind? Target(F5Command command)
        {
            switch (command)
            {
                case F5Command.ConfigureLifeCrystal: case F5Command.EnableLifeCrystal: case F5Command.DisableLifeCrystal: return WorldTargetKind.LifeCrystal;
                case F5Command.ConfigureLifeFruit: case F5Command.EnableLifeFruit: case F5Command.DisableLifeFruit: return WorldTargetKind.LifeFruit;
                case F5Command.ConfigureManaCrystal: case F5Command.EnableManaCrystal: case F5Command.DisableManaCrystal: return WorldTargetKind.ManaCrystal;
                case F5Command.ConfigureDigtoise: case F5Command.EnableDigtoise: case F5Command.DisableDigtoise: return WorldTargetKind.SleepingDigtoise;
                case F5Command.ConfigureChilletEgg: case F5Command.EnableChilletEgg: case F5Command.DisableChilletEgg: return WorldTargetKind.ChilletEgg;
                default: return null;
            }
        }
        internal static bool IsStyle(F5Command command)
        { return command == F5Command.ConfigureLifeCrystal || command == F5Command.ConfigureLifeFruit || command == F5Command.ConfigureManaCrystal || command == F5Command.ConfigureDigtoise || command == F5Command.ConfigureChilletEgg; }
        private static bool IsEnable(F5Command command)
        { return command == F5Command.EnableLifeCrystal || command == F5Command.EnableLifeFruit || command == F5Command.EnableManaCrystal || command == F5Command.EnableDigtoise || command == F5Command.EnableChilletEgg; }
        internal bool Available(F5Command command) { return Target(command).HasValue && (IsStyle(command) ? host.CanConfigure : host.ControlsEnabled); }
        internal Color? Selected(F5Command command)
        {
            var kind = Target(command);
            if (!kind.HasValue || IsStyle(command) || !Available(command) || IsEnable(command) != host.Preferences.Value.Enabled(kind.Value)) return null;
            return IsEnable(command) ? Color.LightGreen : Color.IndianRed;
        }
        internal void Execute(F5Command command)
        { if (Available(command) && !IsStyle(command)) host.SetEnabled(Target(command).Value, IsEnable(command)); }
        internal string Hint(F5Command command)
        {
            if (!Target(command).HasValue) return null;
            if (!Available(command)) return "附近目标设置暂不可用";
            return IsStyle(command) ? "调整本项箭头颜色" : "标记附近完整目标；需要原版金属探测能力生效";
        }
    }
}
