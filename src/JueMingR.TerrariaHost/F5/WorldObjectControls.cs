using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class WorldObjectControls
    {
        private readonly IWorldObjectControls host;
        internal WorldObjectControls(IWorldObjectControls host) { this.host = host; }
        internal static string Name(WorldObjectKind kind) { return kind == WorldObjectKind.Chest ? "宝箱显名" : kind == WorldObjectKind.Sign ? "牌子显示" : "墓碑显示"; }
        internal static void AddRow(List<F5Element> elements, Func<string, float, F5Size> measure, Func<string, float, F5Size> dynamicMeasure, ref float y, WorldObjectStyle style)
        {
            var kind = style.Kind;
            string[] labels = kind == WorldObjectKind.Chest ? new[] { "配置", "始终", "开过", "关闭", "键" } : new[] { "配置", "全部", "前几行", "前几字", "关闭", "键" };
            new F5RowLayout(elements, measure).Row(ref y, 0, 522, Name(kind), labels, text => Command(kind, text));
            var key = elements[elements.Count - 1];
            elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, Hotkeys.HotkeyActionIds.WorldObject(kind));
            if (kind != WorldObjectKind.Chest && (style.Mode == WorldObjectMode.Lines || style.Mode == WorldObjectMode.Characters))
            {
                string number = (style.Mode == WorldObjectMode.Lines ? "行数：" + style.Lines.ToString(CultureInfo.InvariantCulture) : "字数：" + style.Characters.ToString(CultureInfo.InvariantCulture));
                // Parameter strings have a changing domain. Measure the current
                // row on rebuild instead of retaining all 1200 in fixed-text cache.
                new F5RowLayout(elements, dynamicMeasure).Row(ref y, 0, 522, number, new[] { "−", "+" },
                    text => kind == WorldObjectKind.Sign ? text == "−" ? F5Command.SignLess : F5Command.SignMore : text == "−" ? F5Command.TombstoneLess : F5Command.TombstoneMore);
            }
        }
        private static F5Command Command(WorldObjectKind kind, string text)
        {
            if (kind == WorldObjectKind.Chest) return text == "配置" ? F5Command.ConfigureChest : text == "始终" ? F5Command.ChestAlways : text == "开过" ? F5Command.ChestOpened : text == "关闭" ? F5Command.ChestOff : F5Command.None;
            if (kind == WorldObjectKind.Sign) return text == "配置" ? F5Command.ConfigureSign : text == "全部" ? F5Command.SignAll : text == "前几行" ? F5Command.SignLines : text == "前几字" ? F5Command.SignCharacters : text == "关闭" ? F5Command.SignOff : F5Command.None;
            return text == "配置" ? F5Command.ConfigureTombstone : text == "全部" ? F5Command.TombstoneAll : text == "前几行" ? F5Command.TombstoneLines : text == "前几字" ? F5Command.TombstoneCharacters : text == "关闭" ? F5Command.TombstoneOff : F5Command.None;
        }
        internal static WorldObjectKind? Target(F5Command command)
        {
            switch (command)
            {
                case F5Command.ConfigureChest: case F5Command.ChestAlways: case F5Command.ChestOpened: case F5Command.ChestOff: return WorldObjectKind.Chest;
                case F5Command.ConfigureSign: case F5Command.SignAll: case F5Command.SignLines: case F5Command.SignCharacters: case F5Command.SignOff: case F5Command.SignLess: case F5Command.SignMore: return WorldObjectKind.Sign;
                case F5Command.ConfigureTombstone: case F5Command.TombstoneAll: case F5Command.TombstoneLines: case F5Command.TombstoneCharacters: case F5Command.TombstoneOff: case F5Command.TombstoneLess: case F5Command.TombstoneMore: return WorldObjectKind.Tombstone;
                default: return null;
            }
        }
        internal static bool IsStyle(F5Command command) { return command == F5Command.ConfigureChest || command == F5Command.ConfigureSign || command == F5Command.ConfigureTombstone; }
        private static int Step(F5Command command) { return command == F5Command.SignLess || command == F5Command.TombstoneLess ? -1 : command == F5Command.SignMore || command == F5Command.TombstoneMore ? 1 : 0; }
        private static WorldObjectMode Mode(F5Command command)
        {
            switch (command)
            {
                case F5Command.ChestAlways: return WorldObjectMode.Always;
                case F5Command.ChestOpened: return WorldObjectMode.Opened;
                case F5Command.SignAll: case F5Command.TombstoneAll: return WorldObjectMode.All;
                case F5Command.SignLines: case F5Command.TombstoneLines: return WorldObjectMode.Lines;
                case F5Command.SignCharacters: case F5Command.TombstoneCharacters: return WorldObjectMode.Characters;
                default: return WorldObjectMode.Off;
            }
        }
        internal bool Available(F5Command command)
        {
            var kind = Target(command); if (!kind.HasValue) return false;
            if (IsStyle(command)) return host.CanConfigure;
            if (!host.ControlsEnabled) return false;
            int step = Step(command); if (step == 0) return true;
            var style = host.Settings.Style(kind.Value);
            return style.Mode == WorldObjectMode.Lines ? style.Lines + step >= 1 && style.Lines + step <= 10 :
                style.Mode == WorldObjectMode.Characters && style.Characters + step >= 1 && style.Characters + step <= 1200;
        }
        internal void Execute(F5Command command)
        {
            if (!Available(command) || IsStyle(command)) return;
            var kind = Target(command).Value; int step = Step(command);
            if (step == 0) { host.SetMode(kind, Mode(command)); return; }
            var style = host.Settings.Style(kind);
            host.SetLimits(kind, style.Lines + (style.Mode == WorldObjectMode.Lines ? step : 0), style.Characters + (style.Mode == WorldObjectMode.Characters ? step : 0));
        }
        internal Color? Selected(F5Command command)
        { var kind = Target(command); if (!kind.HasValue || IsStyle(command) || Step(command) != 0 || !Available(command) || host.Settings.Style(kind.Value).Mode != Mode(command)) return null;
            return Mode(command) == WorldObjectMode.Off ? Color.IndianRed : Color.LightGreen; }
        internal string Hint(F5Command command)
        {
            if (!Target(command).HasValue) return null;
            if (!Available(command)) return "此项暂不可用或已到数量边界";
            if (IsStyle(command)) return "调整本项颜色与字号";
            if (command == F5Command.ChestAlways) return "显示附近完整容器；需要原版金属探测能力";
            if (command == F5Command.ChestOpened) return "显示本角色在此世界开过的位置上的当前容器";
            if (command == F5Command.ChestOff) return "关闭显名；正常开箱仍会登记位置";
            if (Step(command) != 0) return "调整当前模式的数量";
            switch (Mode(command))
            {
                case WorldObjectMode.Off: return "关闭本项正文显示";
                case WorldObjectMode.All: return "显示当前已知正文，最多十个排版行，超出以省略号提示";
                case WorldObjectMode.Lines: return "按换行后的实际排版行数截取，保留内部空行";
                default: return "按完整可见字符截取，图标计一字；最多十个排版行";
            }
        }
    }
}
