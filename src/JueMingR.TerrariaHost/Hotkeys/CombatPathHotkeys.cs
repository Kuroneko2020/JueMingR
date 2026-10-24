using System;
using JueMingR.Features.HiddenFeatures;
using JueMingR.Platform.Hotkeys;

namespace JueMingR.TerrariaHost.Hotkeys
{
    // Qualification publishes the one display action, not its saved preference.
    // An absent action leaves old disk records inert in the common binding owner.
    internal sealed class CombatPathHotkeys
    {
        private readonly DynamicHotkeyOwner registration;
        private readonly HiddenFeatureAccess access;
        private readonly HotkeyAction[] action;
        private static readonly HotkeyAction[] none = new HotkeyAction[0];
        private bool published;
        internal CombatPathHotkeys(HotkeyRegistry registry, HiddenFeatureAccess access, HotkeyAction action)
        {
            this.access = access;
            registration = registry.CreateDynamicActionOwner(F5.CombatObservationControls.Actions[1]);
            this.action = new[] { action };
            Refresh();
        }
        internal void Refresh()
        {
            bool visible = access != null && access.IsUnlocked;
            if (visible == published) return;
            string reason;
            if (!registration.TryReplace(visible ? action : none, out reason)) throw new InvalidOperationException(reason);
            published = visible;
        }
    }
}
