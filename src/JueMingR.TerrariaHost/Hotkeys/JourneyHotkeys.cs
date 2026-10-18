using System;
using JueMingR.Features.HiddenFeatures;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.Hotkeys
{
    // Reserve once before the binding table freezes. Locked actions are absent,
    // so an old saved binding has neither a conflict slot nor a dispatch target.
    internal sealed class JourneyHotkeys
    {
        private readonly DynamicHotkeyOwner registration;
        private readonly HiddenFeatureAccess access;
        private readonly HotkeyAction[] action;
        private static readonly HotkeyAction[] none = new HotkeyAction[0];
        private bool published;
        internal JourneyHotkeys(HotkeyRegistry registry, HiddenFeatureAccess access, IJourneyControls owner, Action toggle)
        {
            this.access = access;
            registration = registry.CreateDynamicOwner("journey.");
            action = new[] { new HotkeyAction(JourneyControls.ActionId, "旅行菜单", HotkeyContext.SinglePlayer,
                () => access != null && access.IsUnlocked && (owner.IsActive || owner.CanEnable), toggle,
                () => access != null && access.IsUnlocked) };
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
