using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Hotkeys;
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
        private IReadOnlyList<BlueprintMaterialInstance> placedVisibleInstances, placedLiveInstances;
        private long placedFailureRevision = -1;
        internal IReadOnlyList<BlueprintMaterialInstance> PlacedInstances
        {
            get
            {
                var current = MaterialInstances;
                if (failedSceneRecords.Count == 0) return current;
                if (ReferenceEquals(current, placedLiveInstances) && placedFailureRevision == failedSceneRevision) return placedVisibleInstances;
                var all = new List<BlueprintMaterialInstance>(current);
                foreach (var row in failedSceneRecords.Values)
                {
                    var record = row.Record;
                    all.Add(new BlueprintMaterialInstance(generation, row.Instance, row.Instance, row.Name ?? ("蓝图 " + record.Template.AssetId.ToString("N").Substring(0, 8)),
                        record.OriginX, record.OriginY, commitOrder: record.Order, template: record.Template));
                }
                all.Sort((left, right) => left.CommitOrder.CompareTo(right.CommitOrder));
                placedLiveInstances = current; placedFailureRevision = failedSceneRevision;
                return placedVisibleInstances = all.AsReadOnly();
            }
        }
        internal string PlacedRecoveryReason(long instance)
        {
            if (!failedSceneRecords.TryGetValue(instance, out var value)) return null;
            if (ReferenceEquals(value, repairingSceneRecord)) return "正在核对原存档";
            if (value.FailureScope == BlueprintProjectionFailureScope.Demand) return "恢复所需资料超出当前处理容量";
            if (value.Code == "blueprint-scene-template-bounds") return "保存的位置超出当前世界范围";
            if (value.Code?.Contains("mask") == true) return "保存的裁剪范围无法验证";
            if (value.Code?.Contains("mirror") == true) return "保存的镜像布局无法验证";
            return "蓝图模板缺失或无法读取";
        }
        internal string PlacedRecoveryDisabledReason => repairingSceneRecord != null ? "正在核对原存档" :
            projectionHistory.CanRecoverRecord ? null : "原存档目前受保护";
        internal bool RetryPlacedInstance(long session, long instance) { return RequestSceneRecordRecovery(session, instance, false); }
        internal bool RemoveFailedPlacedInstance(long session, long instance) { return RequestSceneRecordRecovery(session, instance, true); }
        internal void OpenPlacedList() { OpenPlacedListFromGesture(null); }
        private void OpenPlacedListFromGesture(HotkeyChord gesture)
        {
            if (!CanOpenPlacedList || !Input.CanStartActions) return;
            Pointer.Cancel(); Toolbar.Cancel(); State.Interrupt();
            Shell?.OpenBlueprintPlacedList(gesture);
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
