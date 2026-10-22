using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class BlueprintControls
    {
        internal const string CaptureAction = "blueprint.capture", SaveAction = "blueprint.save", ToolbarAction = "blueprint.toolbar";
        internal const string StartHint = "点击开始键执行选区，反选取消选区。";
        internal const string ExitHint = "退出选区，并清空已有选区";
        internal const string SaveHint = "点击保存键保存世界上已有的蓝图选区。";
        private readonly Blueprint.HostBlueprint owner;
        internal BlueprintControls(Blueprint.HostBlueprint owner) { this.owner = owner; }
        internal bool Active { get { return owner.State.Active; } }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y, bool active)
        {
            var rows = new F5RowLayout(elements, measure);
            rows.Row(ref y, 0, 522, active ? "退出采集" : "采集蓝图", new[] { active ? "退出" : "开始", "键" },
                text => text == "键" ? F5Command.None : F5Command.BlueprintCapture,
                new F5RowDescription(CaptureAction, active ? ExitHint : StartHint));
            Key(elements, CaptureAction);
            rows.Row(ref y, 0, 522, "保存采集", new[] { "保存", "键" }, text => text == "键" ? F5Command.None : F5Command.BlueprintSave,
                new F5RowDescription(SaveAction, SaveHint));
            Key(elements, SaveAction);
            rows.Row(ref y, 0, 522, "手持凝胶显示快捷栏", new[] { "开启", "关闭", "键" },
                text => text == "键" ? F5Command.None : text == "开启" ? F5Command.BlueprintToolbarOn : F5Command.BlueprintToolbarOff);
            Key(elements, ToolbarAction);
        }
        private static void Key(List<F5Element> elements, string id)
        {
            var key = elements[elements.Count - 1];
            elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, id);
        }
        internal static bool Owns(F5Command command) { return command >= F5Command.BlueprintCapture && command <= F5Command.BlueprintToolbarOff; }
        internal bool Available(F5Command command) { return command != F5Command.BlueprintSave && owner.ControlsEnabled; }
        internal Color? Selected(F5Command command)
        { return command == F5Command.BlueprintToolbarOn && owner.ToolbarEnabled ? (Color?)Color.LightGreen : command == F5Command.BlueprintToolbarOff && !owner.ToolbarEnabled ? (Color?)Color.IndianRed : null; }
        internal void Execute(F5Command command)
        {
            if (!Owns(command) || !Available(command)) return;
            if (command == F5Command.BlueprintCapture) owner.Toggle();
            else owner.SetToolbar(command == F5Command.BlueprintToolbarOn);
        }
    }
}
