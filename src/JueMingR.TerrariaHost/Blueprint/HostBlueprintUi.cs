using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        internal long UiSession { get { return generation; } }
        // A popup release belongs to one prepared preference snapshot. Reading
        // its revision neither changes settings nor starts storage work.
        internal long UiPreferenceRevision { get { return preference.Snapshot.Revision; } }
        internal bool AutoBuildEnabled { get { return preference.Snapshot.Value.AutoBuild; } }
        internal void SetAutoBuild(bool value)
        {
            if (!ControlsEnabled || !preference.Set(preference.Snapshot.Value.WithAutoBuild(value))) return;
            RefreshSubstitutionPolicy();
            if (!value) CancelAutomaticBuild();
            AutomaticBuildChanged();
        }
        internal bool SameTypeReplacementEnabled { get { return preference.Snapshot.Value.SameTypeReplacement; } }
        internal int ReplacementCategories { get { return preference.Snapshot.Value.ReplacementCategories; } }
        internal void SetSameTypeReplacement(bool value)
        {
            if (!ControlsEnabled || !preference.Set(preference.Snapshot.Value.WithSameTypeReplacement(value))) return;
            RefreshSubstitutionPolicy();
        }
        internal void SetReplacementCategories(int value)
        {
            if (!ControlsEnabled || value < 0 || value > BlueprintPreferences.AllReplacementCategories ||
                !preference.Set(preference.Snapshot.Value.WithReplacementCategories(value))) return;
            RefreshSubstitutionPolicy();
        }
        internal string MaterialsDisabledReason { get { return !ControlsEnabled ? "蓝图当前不可用" : Library == null ? "蓝图材料暂不可用" : null; } }
        internal bool CanOpenMaterials { get { return MaterialsDisabledReason == null; } }
        internal void OpenMaterials()
        {
            if (!CanOpenMaterials || !Input.CanStartActions || Shell?.BlueprintMaterialsUi == null) return;
            Pointer.Cancel(); Toolbar.Cancel(); State.Interrupt();
            Shell.OpenBlueprintMaterials(Shell.State.Visible ? BlueprintMaterialsSource.F5 : BlueprintMaterialsSource.World, Shell.BlueprintMaterialsAnchor());
        }
        internal void OpenMaterialsFromToolbar(F5Rect anchor)
        {
            if (!CanOpenMaterials || !Input.CanStartActions || Shell?.BlueprintMaterialsUi == null) return;
            Pointer.Cancel(); Toolbar.Cancel(); State.Interrupt();
            Shell.OpenBlueprintMaterials(BlueprintMaterialsSource.World, anchor);
        }
    }
}
