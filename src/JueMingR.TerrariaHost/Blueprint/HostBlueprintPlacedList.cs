using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        internal string PlacedListDisabledReason
        {
            get { return !ControlsEnabled ? "蓝图当前不可用" : Library == null ? "蓝图库暂不可用" :
                ProjectionCaptureBlocked || HasActiveCaptureWork ? "请先保存或退出当前采集" :
                ProjectionEditing ? "请先完成或取消当前蓝图操作" :
                !ProjectionPersistenceReady ? "正在恢复当前世界蓝图" : deletePending ? "正在删除蓝图模板" : null; }
        }
        internal bool CanOpenPlacedList { get { return PlacedListDisabledReason == null; } }
        internal void OpenPlacedList()
        {
            if (!CanOpenPlacedList || !Input.CanStartActions) return;
            Pointer.Cancel(); Toolbar.Cancel(); State.Interrupt();
            Shell?.OpenBlueprintPlacedList();
        }
        internal bool PlacedInstanceExists(long session, long instance)
        { return session == generation && HasSession && projectionScene.Exists(value => value.Identity.SessionGeneration == session && value.Identity.ProjectionGeneration == instance); }
        internal bool OpenPlacedInstanceMaterials(long session, long instance, F5Rect anchor)
        {
            if (!CanOpenMaterials || !Input.CanStartActions || !PlacedInstanceExists(session, instance)) return false;
            Pointer.Cancel(); Toolbar.Cancel(); State.Interrupt();
            return Shell?.OpenBlueprintPlacedMaterials(session, instance, anchor) == true;
        }
        internal string PlacedRemoveDisabledReason { get { return CanRemoveProjection ? null : ProjectionActionHint; } }
        internal bool TryRemovePlacedInstance(long session, long instance)
        {
            if (!CanRemoveProjection || !Input.CanStartActions || session != generation) return false;
            var target = projectionScene.Find(value => value.Identity.SessionGeneration == session && value.Identity.ProjectionGeneration == instance);
            if (target == null) return false;
            RemoveCommittedProjection(target); return true;
        }
        // Both UI and world selection retire exactly one live scene operand.
        // In-flight native effects/returns retain their existing finite owner;
        // persistence is an asynchronous Offer, not a UI rollback transaction.
        private void RemoveCommittedProjection(BlueprintProjectionPlacement target)
        {
            if (target == null || !projectionScene.Contains(target)) return;
            RetireProjection(target); PublishProjectionScene(); AcceptProjectionSceneSnapshot();
            ProjectionMode = projectionScene.Count == 0 ? BlueprintProjectionMode.None : BlueprintProjectionMode.Fixed;
            InterruptProjection(); Pointer.Cancel(); Toolbar.Cancel();
        }
        internal void PlacedInstanceNoLongerAvailable()
        { projectionFeedback = "这份蓝图已完成或已移除"; }
    }
}
