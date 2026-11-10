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
        float ZoomTarget { get; }
        float MinimumZoomTarget { get; }
        float MaximumZoomTarget { get; }
        bool TrySetZoomTarget(float proposed);
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
            int first = elements.Count;
            new F5RowLayout(elements, measure).Row(ref y, 0, 522, "视距解锁", new[] { "开启", "关闭", "键" },
                text => text == "开启" ? F5Command.EnableViewUnlock : text == "关闭" ? F5Command.DisableViewUnlock : F5Command.None, description);
            var key = elements[elements.Count - 1];
            elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, ActionId);
            var panel = elements[first]; var title = elements[first + 1];
            float right = elements[elements.Count - 3].Rect.X - 2;
            float left = title.Rect.Right + 1;
            float numberWidth = Math.Max(measure("1000%", .70f).Width, measure("999.9%", .70f).Width) + 2;
            float buttonWidth = Math.Max(14, Math.Max(measure("-", .70f).Width, measure("+", .70f).Width) + 2);
            float height = elements[elements.Count - 3].Rect.Height;
            float top = panel.Rect.Y + (panel.Rect.Height - height) / 2;
            // The fixed percentage precedes the track; glyph-only minus/plus
            // remain together on its right. Five-unit clearances protect both
            // handle endpoints without taking width from the previous track.
            float trackWidth = right - left - 2 * buttonWidth - numberWidth - 11;
            float trackX = left + numberWidth + 5;
            if (trackWidth < 20) throw new InvalidOperationException("F5 view controls cannot fit the current font.");
            elements.Add(new F5Element(F5ElementKind.Field, new F5Rect(left, top, numberWidth, height), null, default(F5Size), .70f, F5Command.ViewZoomValue));
            elements.Add(new F5Element(F5ElementKind.Field, new F5Rect(trackX, top, trackWidth, height), null, default(F5Size), .70f, F5Command.ViewZoomTrack));
            elements.Add(new F5Element(F5ElementKind.Field, new F5Rect(trackX + trackWidth + 5, top, buttonWidth, height), "-", measure("-", .70f), .70f, F5Command.ViewZoomMinus));
            elements.Add(new F5Element(F5ElementKind.Field, new F5Rect(trackX + trackWidth + buttonWidth + 6, top, buttonWidth, height), "+", measure("+", .70f), .70f, F5Command.ViewZoomPlus));
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
