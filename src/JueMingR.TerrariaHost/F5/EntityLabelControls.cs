using System;
using System.Collections.Generic;
using JueMingR.Features.EntityLabels;
using JueMingR.TerrariaHost.EntityLabels;
using JueMingR.TerrariaHost.Hotkeys;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    // These are the three existing information rows, with stable typed commands.
    // Row order and localized button text never dispatch gameplay commands.
    internal sealed class EntityLabelControls
    {
        private readonly HostEntityLabels host;
        internal EntityLabelControls(HostEntityLabels host) { this.host = host; }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y)
        {
            var rows = new F5RowLayout(elements, measure);
            Add(rows, elements, ref y, "敌怪显名", new[] { "配置", "开启", "关闭", "键" },
                text => text == "配置" ? F5Command.ConfigureEnemy : text == "开启" ? F5Command.EnableEnemy : text == "关闭" ? F5Command.DisableEnemy : F5Command.None, HotkeyActionIds.EnemyLabels);
            Add(rows, elements, ref y, "动物显名", new[] { "配置", "开启", "关闭", "键" },
                text => text == "配置" ? F5Command.ConfigureCritter : text == "开启" ? F5Command.EnableCritter : text == "关闭" ? F5Command.DisableCritter : F5Command.None, HotkeyActionIds.CritterLabels);
            Add(rows, elements, ref y, "NPC显名", new[] { "配置", "名字", "类型", "关闭", "键" },
                text => text == "配置" ? F5Command.ConfigureNpc : text == "名字" ? F5Command.NpcName : text == "类型" ? F5Command.NpcType : text == "关闭" ? F5Command.DisableNpc : F5Command.None, HotkeyActionIds.NpcLabels);
        }
        private static void Add(F5RowLayout rows, List<F5Element> elements, ref float y, string title, string[] actions, Func<string, F5Command> command, string hotkey)
        {
            rows.Row(ref y, 0, 522, title, actions, command);
            F5Element key = elements[elements.Count - 1];
            elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, hotkey);
        }
        internal static EntityLabelKind? Target(F5Command command)
        {
            switch (command)
            {
                case F5Command.ConfigureEnemy: case F5Command.EnableEnemy: case F5Command.DisableEnemy: return EntityLabelKind.Enemy;
                case F5Command.ConfigureCritter: case F5Command.EnableCritter: case F5Command.DisableCritter: return EntityLabelKind.Critter;
                case F5Command.ConfigureNpc: case F5Command.NpcName: case F5Command.NpcType: case F5Command.DisableNpc: return EntityLabelKind.Npc;
                default: return null;
            }
        }
        internal static bool IsStyle(F5Command command)
        { return command == F5Command.ConfigureEnemy || command == F5Command.ConfigureCritter || command == F5Command.ConfigureNpc; }
        internal bool Available(F5Command command) { return Target(command).HasValue && (IsStyle(command) ? host.CanConfigure : host.ControlsEnabled); }
        internal Color? Selected(F5Command command)
        {
            if (!Available(command) || IsStyle(command)) return null;
            var value = host.Preferences.Value;
            bool selected = command == F5Command.EnableEnemy && value.EnemyEnabled || command == F5Command.DisableEnemy && !value.EnemyEnabled ||
                command == F5Command.EnableCritter && value.CritterEnabled || command == F5Command.DisableCritter && !value.CritterEnabled ||
                command == F5Command.NpcName && value.NpcMode == NpcLabelMode.Name || command == F5Command.NpcType && value.NpcMode == NpcLabelMode.Type ||
                command == F5Command.DisableNpc && value.NpcMode == NpcLabelMode.Off;
            if (!selected) return null;
            return command == F5Command.DisableEnemy || command == F5Command.DisableCritter || command == F5Command.DisableNpc ? Color.IndianRed : Color.LightGreen;
        }
        internal void Execute(F5Command command)
        {
            if (!Available(command)) return;
            switch (command)
            {
                case F5Command.EnableEnemy: host.SetEnabled(EntityLabelKind.Enemy, true); break;
                case F5Command.DisableEnemy: host.SetEnabled(EntityLabelKind.Enemy, false); break;
                case F5Command.EnableCritter: host.SetEnabled(EntityLabelKind.Critter, true); break;
                case F5Command.DisableCritter: host.SetEnabled(EntityLabelKind.Critter, false); break;
                case F5Command.NpcName: host.SetNpcMode(NpcLabelMode.Name); break;
                case F5Command.NpcType: host.SetNpcMode(NpcLabelMode.Type); break;
                case F5Command.DisableNpc: host.SetNpcMode(NpcLabelMode.Off); break;
            }
        }
        internal string Hint(F5Command command)
        {
            if (!Target(command).HasValue) return null;
            if (!Available(command)) return "显名设置暂不可用";
            return IsStyle(command) ? "调整本项颜色与字号" : command == F5Command.NpcName ? "显示 NPC 的名字" :
                command == F5Command.NpcType ? "显示 NPC 的类型" :
                command == F5Command.DisableEnemy || command == F5Command.DisableCritter || command == F5Command.DisableNpc ? "关闭本项显名" : "开启本项显名";
        }
    }
}
