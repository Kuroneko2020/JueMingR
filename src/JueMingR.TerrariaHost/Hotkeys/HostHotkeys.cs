using System;
using System.IO;
using JueMingR.Platform.Hotkeys;
using JueMingR.Infrastructure.Storage;
using JueMingR.Features.Items;
using JueMingR.TerrariaHost.Items;
using JueMingR.TerrariaHost.Settings;
using Terraria;

namespace JueMingR.TerrariaHost.Hotkeys
{
    internal sealed class HostHotkeys
    {
        internal readonly HotkeyRegistry Registry = new HotkeyRegistry();
        internal readonly HotkeyBindings Bindings;
        internal HostHotkeys(string gameDirectory, Phase0TBiomeRuntime biome, HostPreferences preferences, HostItems items, EntityLabels.HostEntityLabels labels = null, WorldTargets.HostWorldTargets targets = null, WorldObjectText.HostWorldObjectText worldObjects = null)
        {
            Registry.Register(new HotkeyAction(HotkeyActionIds.Biome, "群系显示", HotkeyContext.SinglePlayer,
                () => preferences.BiomeLoaded && !biome.FeatureFailed && Main.netMode == 0,
                () => preferences.SetBiomeEnabled(!preferences.BiomeEnabled)));
            if (items != null)
                for (int i = 0; i < HotkeyActionIds.Items.Length; i++)
                {
                    var action = (ItemActionKind)i;
                    Registry.Register(new HotkeyAction(HotkeyActionIds.Items[i], ItemsPresentation.Name(action), HotkeyContext.Gameplay,
                        () => items.ControlsEnabled,
                        () => items.Change(items.Preferences.Value.WithEnabled(action, !items.Preferences.Value.Enabled(action)))));
                }
            if (labels != null)
                for (int i = 0; i < HotkeyActionIds.EntityLabels.Length; i++)
                {
                    var kind = (Features.EntityLabels.EntityLabelKind)i;
                    Registry.Register(new HotkeyAction(HotkeyActionIds.EntityLabels[i], EntityLabels.StylePopupLayout.Name(kind), HotkeyContext.Gameplay,
                        () => labels.ControlsEnabled, () => labels.Toggle(kind)));
                }
            if (targets != null)
                foreach (Platform.WorldTargets.WorldTargetKind target in Enum.GetValues(typeof(Platform.WorldTargets.WorldTargetKind)))
                {
                    var kind = target;
                    Registry.Register(new HotkeyAction(HotkeyActionIds.WorldTarget(kind), F5.WorldTargetControls.Name(kind), HotkeyContext.Gameplay,
                        () => targets.ControlsEnabled, () => targets.Toggle(kind)));
                }
            if (worldObjects != null)
                for (int i = 0; i < 3; i++)
                {
                    var kind = (Platform.WorldObjectText.WorldObjectKind)i;
                    Registry.Register(new HotkeyAction(HotkeyActionIds.WorldObject(kind), F5.WorldObjectControls.Name(kind), HotkeyContext.Gameplay,
                        () => worldObjects.ControlsEnabled, () => worldObjects.Toggle(kind)));
                }
            Bindings = new HotkeyBindings(Registry, new AtomicFileDocument(Path.Combine(gameDirectory, "JueMingRData", "config", "hotkeys.json"), 65536, true));
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }
        private void OnExit(object sender, EventArgs args)
        { AppDomain.CurrentDomain.ProcessExit -= OnExit; Bindings.Stop(750); }
    }
}
