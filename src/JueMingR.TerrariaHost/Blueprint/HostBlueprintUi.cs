using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        internal long UiSession { get { return generation; } }
        internal bool AutoBuildEnabled { get { return preference.Snapshot.Value.AutoBuild; } }
        internal void SetAutoBuild(bool value)
        {
            if (!ControlsEnabled || !preference.Set(new BlueprintPreferences(ToolbarEnabled, value))) return;
            if (!value) CancelAutomaticBuild();
            AutomaticBuildChanged();
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
