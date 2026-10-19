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
        bool SetEnabled(int feature, bool enabled);
        bool SetDashMode(ContinuousDashMode mode);
        void TakeFeedback(Action<string> display);
    }
    internal sealed class MovementControls
    {
        internal static readonly string[] ActionIds = { "movement.simulated-jump", "movement.continuous-dash", "movement.teleport-correction" };
        internal static readonly string[] Names = { "模拟连跳", "连续冲刺", "传送修正" };
        private static readonly F5RowDescription jump = new F5RowDescription(ActionIds[0], "按住跳跃键连跳，类似蛙腿效果");
        private static readonly F5RowDescription hold = new F5RowDescription(ActionIds[1], "按住方向键冲刺");
        private static readonly F5RowDescription doubleTap = new F5RowDescription(ActionIds[1], "双击按住方向键冲刺");
        private static readonly F5RowDescription teleport = new F5RowDescription(ActionIds[2], "传送杖点击到物块内时，传送到附近空地");
        private readonly IMovementControls owner;
        internal MovementControls(IMovementControls owner) { this.owner = owner; }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y, ContinuousDashMode mode)
        {
            var rows = new F5RowLayout(elements, measure);
            for (int i = 0; i < 3; i++)
            {
                int feature = i;
                rows.Row(ref y, 0, 522, Names[i], i == 1 ? new[] { mode == ContinuousDashMode.Hold ? "按住" : "双击", "开启", "关闭", "键" } : new[] { "开启", "关闭", "键" },
                    text => text == "开启" ? On(feature) : text == "关闭" ? Off(feature) : text == "键" ? F5Command.None : F5Command.MovementDashMode,
                    i == 0 ? jump : i == 2 ? teleport : mode == ContinuousDashMode.Hold ? hold : doubleTap);
                var key = elements[elements.Count - 1];
                elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, key.Command, ActionIds[i], key.Description, key.HintRect);
            }
        }
        private static F5Command On(int feature) { return feature == 0 ? F5Command.MovementJumpOn : feature == 1 ? F5Command.MovementDashOn : F5Command.MovementTeleportOn; }
        private static F5Command Off(int feature) { return feature == 0 ? F5Command.MovementJumpOff : feature == 1 ? F5Command.MovementDashOff : F5Command.MovementTeleportOff; }
        internal static bool Owns(F5Command command) { return command >= F5Command.MovementJumpOn && command <= F5Command.MovementDashMode; }
        private static int Feature(F5Command command) { return command == F5Command.MovementJumpOn || command == F5Command.MovementJumpOff ? 0 : command == F5Command.MovementTeleportOn || command == F5Command.MovementTeleportOff ? 2 : 1; }
        internal bool Available(F5Command command) { return Owns(command) && (command == F5Command.MovementDashMode ? owner.ControlsEnabled : owner.Available(Feature(command))); }
        internal Color? Selected(F5Command command)
        { if (!Owns(command) || command == F5Command.MovementDashMode) return null; int feature = Feature(command); return command == On(feature) && owner.IsEnabled(feature) ? Color.LightGreen : command == Off(feature) && !owner.IsEnabled(feature) ? (Color?)Color.IndianRed : null; }
        internal void Execute(F5Command command)
        {
            if (!Available(command)) return;
            if (command == F5Command.MovementDashMode) owner.SetDashMode(owner.DashMode == ContinuousDashMode.Hold ? ContinuousDashMode.DoubleTap : ContinuousDashMode.Hold);
            else { int feature = Feature(command); owner.SetEnabled(feature, command == On(feature)); }
        }
        internal string Hint(F5Command command)
        { return !Owns(command) ? null : !Available(command) ? Names[Feature(command)] + "暂不可用。" : command == F5Command.MovementDashMode ? (owner.DashMode == ContinuousDashMode.Hold ? hold.Text : doubleTap.Text) : null; }
    }
}
