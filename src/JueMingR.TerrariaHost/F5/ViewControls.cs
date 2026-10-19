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
        private static readonly F5RowDescription description =
            new F5RowDescription("view-unlock", "可以调整到更大的视野范围");
        private readonly IViewControls owner;
        internal ViewControls(IViewControls owner) { this.owner = owner; }

        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y)
        {
            // This feature deliberately uses only the native mapped zoom keys;
            // the owner requested no R binding entry or separate zoom control.
            new F5RowLayout(elements, measure).Row(ref y, 0, 522, "视距解锁", new[] { "开启", "关闭" },
                text => text == "开启" ? F5Command.EnableViewUnlock : F5Command.DisableViewUnlock, description);
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
