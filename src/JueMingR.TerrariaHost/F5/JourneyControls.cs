using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    // The Journey owner alone owns activation and native-window state. A true
    // enable/open result means the native window was confirmed, not requested.
    internal interface IJourneyControls
    {
        bool IsActive { get; }
        bool IsMenuOpen { get; }
        bool CanEnable { get; }
        string UnavailableReason { get; }
        string LastFailure { get; }
        bool SetEnabled(bool enabled);
        bool Toggle();
        bool Open();
        bool CloseMenu();
    }

    internal sealed class JourneyControls
    {
        internal const string ActionId = "journey.toggle";
        private static readonly F5RowDescription description =
            new F5RowDescription(ActionId, "单人临时使用原版旅行菜单");
        private readonly IJourneyControls owner;
        internal JourneyControls(IJourneyControls owner) { this.owner = owner; }
        internal static bool Owns(F5Command command)
        { return command == F5Command.EnableJourney || command == F5Command.DisableJourney; }
        internal static void AddRows(List<F5Element> elements, Func<string, float, F5Size> measure, ref float y, bool visible)
        {
            if (!visible) return;
            new F5RowLayout(elements, measure).Row(ref y, 0, 522, "旅行菜单", new[] { "开启", "关闭", "键" },
                text => text == "开启" ? F5Command.EnableJourney : text == "关闭" ? F5Command.DisableJourney : F5Command.None, description);
            var key = elements[elements.Count - 1];
            elements[elements.Count - 1] = new F5Element(key.Kind, key.Rect, key.Text, key.TextSize, key.TextScale, F5Command.None, ActionId);
        }
        internal bool Available(F5Command command)
        { return Owns(command) && (command == F5Command.DisableJourney ? owner.IsActive : owner.CanEnable); }
        internal Color? Selected(F5Command command)
        {
            if (command == F5Command.EnableJourney && owner.IsActive) return Color.LightGreen;
            if (command == F5Command.DisableJourney && !owner.IsActive) return Color.IndianRed;
            return null;
        }
        internal string Hint(F5Command command)
        { return !Owns(command) || Available(command) ? null : owner.LastFailure ?? owner.UnavailableReason; }
    }
}
