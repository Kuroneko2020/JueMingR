using System;
using JueMingR.Features.EntityLabels;
using JueMingR.Platform.WorldTargets;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.WorldTargets;

namespace JueMingR.TerrariaHost.EntityLabels
{
    // The two real style families share color editing. Optional size is an
    // explicit capability, not a fake world-target size preference.
    internal sealed class StyleTarget
    {
        internal EntityLabelKind? Entity;
        internal WorldTargetKind? World;
        internal string Title;
        internal Func<bool> CanConfigure;
        internal Func<int> Color;
        internal Func<string> Message;
        internal Func<int, bool> SetColor;
        internal Func<int> Size;
        internal Func<int, bool> StepSize;
        internal Action Reset;
        internal bool Same(StyleTarget other) { return other != null && Entity == other.Entity && World == other.World; }
        internal static StyleTarget For(HostEntityLabels host, EntityLabelKind kind)
        { return new StyleTarget { Entity = kind, Title = StylePopupLayout.Name(kind), CanConfigure = () => host.CanConfigure,
            Color = () => host.Preferences.Value.Style(kind).Rgb, Message = () => host.PreferenceMessage, SetColor = rgb => host.SetColor(kind, rgb),
            Size = () => host.Preferences.Value.Style(kind).NameSize, StepSize = direction => host.StepSize(kind, direction), Reset = () => host.ResetStyle(kind) }; }
        internal static StyleTarget For(HostWorldTargets host, WorldTargetKind kind)
        { return new StyleTarget { World = kind, Title = WorldTargetControls.Name(kind), CanConfigure = () => host.CanConfigure,
            Color = () => host.Preferences.Value.Color(kind), Message = () => host.PreferenceMessage, SetColor = rgb => host.SetColor(kind, rgb), Reset = () => host.ResetColor(kind) }; }
    }
}
