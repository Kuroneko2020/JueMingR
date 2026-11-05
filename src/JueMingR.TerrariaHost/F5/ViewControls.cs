using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    internal interface IViewControls
    {
        bool ControlsEnabled { get; }
        bool UnlockEnabled { get; }
        bool SetUnlockEnabled(bool value);
        void TakeFeedback(Action<string> display);
    }

    // The row projects input permission, not the current zoom/rendering duty.
    // OFF-Retained must stay visibly off while its native camera remains wide.
    internal sealed class ViewControls
    {
        internal const string ActionId = "view-unlock.toggle";
        private static readonly F5RowDescription description =
            new F5RowDescription("view-unlock", "可以调整到更大的视野范围");
        private readonly IViewControls owner;
        internal ViewControls(IViewControls owner) { this.owner = owner; }

        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y)
        {
            new F5RowLayout(elements, measure).Row(ref y, 0, 522, "视距解锁", new[] { "开启", "关闭", "键" },
                text => text == "开启" ? F5Command.EnableViewUnlock : text == "关闭" ? F5Command.DisableViewUnlock : F5Command.None, description);
            var key = elements[elements.Count - 1];
            elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, ActionId);
        }

        internal static bool Owns(F5Command command)
        { return command == F5Command.EnableViewUnlock || command == F5Command.DisableViewUnlock; }
        internal bool Available(F5Command command) { return Owns(command) && owner.ControlsEnabled; }
        internal Color? Selected(F5Command command)
        {
            if (command == F5Command.EnableViewUnlock && owner.UnlockEnabled) return Color.LightGreen;
            if (command == F5Command.DisableViewUnlock && !owner.UnlockEnabled) return Color.IndianRed;
            return null;
        }
        internal void Execute(F5Command command)
        { if (Available(command)) owner.SetUnlockEnabled(command == F5Command.EnableViewUnlock); }
        internal string Hint(F5Command command)
        { return Owns(command) && !Available(command) ? "视距解锁暂不可用。" : null; }
    }
}
