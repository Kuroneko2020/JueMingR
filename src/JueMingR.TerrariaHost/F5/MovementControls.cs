using System;
using System.Collections.Generic;
using JueMingR.Features.Movement;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal interface IMovementControls
    {
        event Action DashModeChanged;
        bool ControlsEnabled { get; }
        bool Available(int feature);
        bool IsEnabled(int feature);
        ContinuousDashMode DashMode { get; }
        SmartFallProtectionOptions SmartFallProtection { get; }
        bool SetEnabled(int feature, bool enabled);
        bool SetDashMode(ContinuousDashMode mode);
        bool SetSmartFallPermission(int capability, bool allowed);
        void TakeFeedback(Action<string> display);
    }
    internal sealed class MovementControls
    {
        internal static readonly string[] ActionIds = { "movement.simulated-jump", "movement.continuous-dash", "movement.teleport-correction", "movement.smart-fall-protection" };
        internal static readonly string[] Names = { "模拟连跳", "连续冲刺", "传送修正", "智能防摔" };
        internal static readonly string[] CapabilityNames = { "二段跳", "火箭靴", "飞毯", "翅膀", "马掌", "雨伞", "抓钩", "飞行坐骑", "精灵腿", "免摔坐骑", "传送杖", "重力球" };
        private static readonly F5RowDescription jump = new F5RowDescription(ActionIds[0], "按住跳跃键连跳，类似蛙腿效果");
        private static readonly F5RowDescription hold = new F5RowDescription(ActionIds[1], "按住方向键冲刺");
        private static readonly F5RowDescription doubleTap = new F5RowDescription(ActionIds[1], "双击按住方向键冲刺");
        private static readonly F5RowDescription teleport = new F5RowDescription(ActionIds[2], "传送杖点击到物块内时，传送到附近空地");
        private static readonly F5RowDescription fall = new F5RowDescription(ActionIds[3], "触底前使用可用能力防摔，临时借物及时归还；抓钩由你手动解除");
        internal bool ConfigurationExpanded { get; private set; }
        private readonly IMovementControls owner;
        internal MovementControls(IMovementControls owner) { this.owner = owner; }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y, ContinuousDashMode mode, bool expanded = false)
        {
            var rows = new F5RowLayout(elements, measure);
            for (int i = 0; i < ActionIds.Length; i++)
            {
                int feature = i;
                var labels = i == 1 ? new[] { mode == ContinuousDashMode.Hold ? "按住" : "双击", "开启", "关闭", "键" } : i == 3 ? new[]
                {
                    // "配置",
                    "开启", "关闭", "键"
                } : new[] { "开启", "关闭", "键" };
                rows.Row(ref y, 0, 522, Names[i], labels,
                    text => text == "开启" ? On(feature) : text == "关闭" ? Off(feature) : text == "键" ? F5Command.None : text == "配置" ? F5Command.MovementFallConfigure : F5Command.MovementDashMode,
                    i == 0 ? jump : i == 2 ? teleport : i == 3 ? fall : mode == ContinuousDashMode.Hold ? hold : doubleTap);
                var key = elements[elements.Count - 1];
                elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, key.Command, ActionIds[i], key.Description, key.HintRect);
            }
            if (expanded)
                for (int i = 0; i < CapabilityNames.Length; i++)
                {
                    int capability = i;
                    rows.Row(ref y, 12, 510, CapabilityNames[i], new[] { "允许", "禁用" },
                        text => (F5Command)((int)F5Command.MovementFallAllow0 + capability * 2 + (text == "允许" ? 0 : 1)));
                }
        }
        private static F5Command On(int feature) { return feature == 0 ? F5Command.MovementJumpOn : feature == 1 ? F5Command.MovementDashOn : feature == 2 ? F5Command.MovementTeleportOn : F5Command.MovementFallOn; }
        private static F5Command Off(int feature) { return feature == 0 ? F5Command.MovementJumpOff : feature == 1 ? F5Command.MovementDashOff : feature == 2 ? F5Command.MovementTeleportOff : F5Command.MovementFallOff; }
        internal static bool Owns(F5Command command) { return command >= F5Command.MovementJumpOn && command <= F5Command.MovementFallDeny11; }
        private static bool Permission(F5Command command) => command >= F5Command.MovementFallAllow0 && command <= F5Command.MovementFallDeny11;
        private static int Feature(F5Command command) { return command == F5Command.MovementJumpOn || command == F5Command.MovementJumpOff ? 0 : command == F5Command.MovementTeleportOn || command == F5Command.MovementTeleportOff ? 2 : command >= F5Command.MovementFallOn ? 3 : 1; }
        internal bool Available(F5Command command) { return Owns(command) && (command == F5Command.MovementDashMode || command == F5Command.MovementFallConfigure || Permission(command) ? owner.ControlsEnabled : owner.Available(Feature(command))); }
        internal Color? Selected(F5Command command)
        {
            if (!Owns(command) || command == F5Command.MovementDashMode || command == F5Command.MovementFallConfigure) return null;
            if (Permission(command)) { int index = (int)command - (int)F5Command.MovementFallAllow0; bool allowed = owner.SmartFallProtection.Allows(index / 2); return index % 2 == 0 && allowed ? Color.LightGreen : index % 2 == 1 && !allowed ? (Color?)Color.IndianRed : null; }
            int feature = Feature(command); return command == On(feature) && owner.IsEnabled(feature) ? Color.LightGreen : command == Off(feature) && !owner.IsEnabled(feature) ? (Color?)Color.IndianRed : null;
        }
        internal void Execute(F5Command command)
        {
            if (!Available(command)) return;
            if (command == F5Command.MovementDashMode) owner.SetDashMode(owner.DashMode == ContinuousDashMode.Hold ? ContinuousDashMode.DoubleTap : ContinuousDashMode.Hold);
            else if (command == F5Command.MovementFallConfigure) ConfigurationExpanded = !ConfigurationExpanded;
            else if (Permission(command)) { int index = (int)command - (int)F5Command.MovementFallAllow0; owner.SetSmartFallPermission(index / 2, index % 2 == 0); }
            else { int feature = Feature(command); owner.SetEnabled(feature, command == On(feature)); }
        }
        internal string Hint(F5Command command)
        { return !Owns(command) ? null : !Available(command) ? Names[Feature(command)] + "暂不可用。" : command == F5Command.MovementDashMode ? (owner.DashMode == ContinuousDashMode.Hold ? hold.Text : doubleTap.Text) : null; }
    }
}
