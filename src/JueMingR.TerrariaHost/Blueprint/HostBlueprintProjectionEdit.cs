using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private IBlueprintHorizontalMirrorProfile mirrorProfile;
        private BlueprintProjectionPlacement editorTarget;
        private BlueprintProjectionMask editorMask;
        private long editorSceneRevision, editorTransaction;
        private int editorOriginX, editorOriginY;
        private bool editorMirrored, editorPickPending, trimPlanPending;
        private BlueprintProjectionPlacement[] editorPickOrder;
        private int editorPickIndex, editorPickX, editorPickY;
        private float editorPickSubX, editorPickSubY;
        private int trimStartX, trimStartY, trimEndX, trimEndY;
        private BlueprintProjectionBounds trimDisplayBounds;
        private BlueprintProjectionTrimPlan trimPlan;
        private long trimPreviewRevision;
        private long trimConfirmMapping = -1, trimConfirmRevision, trimConfirmScene, trimConfirmTransaction;
        private HotkeyChord trimConfirmChord;
        internal bool OwnMirrorCancellation { get { return ProjectionEditing && projectionEditKind == BlueprintProjectionEditKind.Mirror; } }
        internal bool OwnTrimCancellation { get { return ProjectionEditing && projectionEditKind == BlueprintProjectionEditKind.Trim; } }
        internal bool CanInvokeMirror { get { return ControlsEnabled && (OwnMirrorCancellation || CanMoveProjection); } }
        internal bool CanInvokeTrim { get { return ControlsEnabled && (OwnTrimCancellation || CanRemoveProjection); } }
        internal bool CanInvokeConfirmTrim { get { return ControlsEnabled && OwnTrimCancellation && ProjectionMode == BlueprintProjectionMode.TrimPreview &&
            !trimPlanPending && trimPlan?.HasContent == true && EditorTargetCurrent() && ReferenceEquals(trimPlan.OriginalMask, editorTarget.Mask); } }
        internal string MirrorDisabledReason { get { return CanInvokeMirror ? null : ProjectionActionHint; } }
        internal string TrimDisabledReason { get { return CanInvokeTrim ? null : ProjectionActionHint; } }
        internal string ConfirmTrimDisabledReason { get { return CanInvokeConfirmTrim ? null : !ControlsEnabled ? "请先进入可用世界" :
            !OwnTrimCancellation ? "请先进入局部修剪" : trimPlanPending ? "正在核对修剪范围…" : ProjectionMode != BlueprintProjectionMode.TrimPreview ? "先拖出修剪范围并松开鼠标" :
            trimPlan != null && !trimPlan.HasContent ? "选区内没有可修剪内容" : "请先选择可靠的修剪范围"; } }
        internal long TrimPreviewRevision { get { return trimPreviewRevision; } }
        internal BlueprintProjectionPlacement ProjectionEditTarget { get { return editorTarget ?? hoveredProjection; } }
        internal bool WorldEditorDragging { get { return State.Dragging || OwnTrimCancellation && ProjectionMode == BlueprintProjectionMode.TrimDragging; } }
        internal bool TrimPointerVisible { get { return OwnTrimCancellation && editorTarget != null &&
            (ProjectionMode == BlueprintProjectionMode.TrimReady || ProjectionMode == BlueprintProjectionMode.TrimDragging || ProjectionMode == BlueprintProjectionMode.TrimPreview); } }
        internal BlueprintProjectionBounds TrimDisplayBounds { get { return trimDisplayBounds; } }
        internal BlueprintProjectionTrimPlan TrimPlan { get { return trimPlan; } }
        internal bool TrimDragging { get { return OwnTrimCancellation && ProjectionMode == BlueprintProjectionMode.TrimDragging; } }

        internal void MirrorProjection() { RequestProjectionEditor(BlueprintProjectionEditKind.Mirror); }
        internal void TrimProjection() { RequestProjectionEditor(BlueprintProjectionEditKind.Trim); }
        private void RequestProjectionEditor(BlueprintProjectionEditKind kind)
        {
            if (!Input.CanStartActions || !(kind == BlueprintProjectionEditKind.Mirror ? CanInvokeMirror : CanInvokeTrim)) return;
            if (kind == projectionEditKind && ProjectionEditing) { CancelProjection(); return; }
            long session = generation, transaction = projectionTransaction, scene = projectionSceneRevision;
            Action accepted = () =>
            {
                if (!HasSession || generation != session || projectionTransaction != transaction || projectionSceneRevision != scene || !CanPlaceProjection || projectionScene.Count == 0) return;
                if (State.Active) State.Exit(); CancelAutomaticBuild();
                projectionTransaction++; projectionEditKind = kind;
                ProjectionMode = kind == BlueprintProjectionEditKind.Mirror ? BlueprintProjectionMode.SelectingMirror : BlueprintProjectionMode.SelectingTrim;
                projectionFeedback = kind == BlueprintProjectionEditKind.Mirror ? "点击需要镜像的蓝图" : "点击需要修剪的蓝图";
                InterruptProjection(); Pointer.Cancel(); Toolbar.Cancel();
            };
            if (Shell != null) Shell.RequestBlueprintEditLeave(kind == BlueprintProjectionEditKind.Mirror ? BlueprintControls.MirrorAction : BlueprintControls.TrimAction, accepted);
            else accepted();
        }
        internal bool AcceptsWorldEditMousePrimary(HotkeyChord chord)
        {
            if (chord == null || !ProjectionEditing || !CanRetainEdit) return true;
            if (chord.MainKey == 257) return false;
            return chord.MainKey != 256 || CanInvokeConfirmTrim;
        }
        internal void RequestTrimConfirm(HotkeyChord chord)
        {
            if (!Input.CanStartActions || !CanInvokeConfirmTrim || chord == null) return;
            if (chord.MainKey != 256) { ConfirmTrimProjection(); return; }
            // Public matching establishes intent only. The very same mapping
            // still needs the original native UI certificate before deletion.
            trimConfirmMapping = Input.Mapping; trimConfirmChord = chord;
            trimConfirmRevision = trimPreviewRevision; trimConfirmScene = projectionSceneRevision; trimConfirmTransaction = projectionTransaction;
        }
        internal bool CanRetainTrimConfirmation(long mapping)
        {
            // This is a certificate lease for one admitted public command,
            // never a relaxation of CanRetainEdit. Its frozen preview/scene/
            // transaction must survive both Draw and the immediately next map.
            if (mapping != trimConfirmMapping || trimConfirmChord == null || trimConfirmChord.MainKey != 256 ||
                (Input.Mapping != mapping && Input.Mapping != mapping + 1) || trimConfirmRevision != trimPreviewRevision ||
                trimConfirmScene != projectionSceneRevision || trimConfirmTransaction != projectionTransaction || !CanInvokeConfirmTrim || !CanRetainWorldInput) return false;
            return CanRetainEdit || Shell?.CanRetainBlueprintTrimConfirmation == true;
        }
        internal bool ConsumeTrimConfirm(long mapping, bool certified)
        {
            if (trimConfirmMapping < 0) return false;
            if (mapping < trimConfirmMapping) return false;
            bool exact = mapping == trimConfirmMapping; var chord = trimConfirmChord;
            bool current = exact && certified && chord != null && CanRetainTrimConfirmation(mapping);
            trimConfirmMapping = -1; trimConfirmChord = null;
            if (current) CommitTrimPreview();
            // A rejected confirm gesture never turns into a replacement drag.
            return exact;
        }
        internal void ConfirmTrimProjection()
        { if (Input.CanStartActions && CanInvokeConfirmTrim) CommitTrimPreview(); }
        private void CommitTrimPreview()
        {
            if (!CanInvokeConfirmTrim) return;
            var target = editorTarget; var plan = trimPlan;
            if (!ReferenceEquals(target.Mask, plan.OriginalMask) || plan.RemovedLayerCount <= 0) return;
            CancelAutomaticBuild(); ReleaseProjectionMatch(target);
            target.Mask = plan.Mask; target.Qualification = null; target.QualificationRequestedMask = null; target.QualificationRequestedRevision = -1;
            RebuildProjectionMask(target);
            // Trim preserves order/origin/pose and cannot cover another instance.
            if (target.Mask.RemovedLayerCount >= target.TotalLayerCount) RetireProjection(target, false);
            projectionSceneRevision++; PublishProjectionScene();
            ClearProjectionEditor(); projectionEditKind = BlueprintProjectionEditKind.None;
            ProjectionMode = projectionScene.Count == 0 ? BlueprintProjectionMode.None : BlueprintProjectionMode.Fixed;
            InterruptProjection(); Pointer.Cancel(); Toolbar.Cancel();
            projectionFeedback = "蓝图已修剪"; AcceptProjectionSceneSnapshot();
        }
        private void ClearProjectionEditor()
        {
            editorTarget = null; editorMask = null; editorPickOrder = null; editorPickPending = trimPlanPending = false; editorPickIndex = 0;
            trimPlan = null; trimDisplayBounds = default(BlueprintProjectionBounds); trimPreviewRevision++;
            trimConfirmMapping = -1; trimConfirmChord = null;
        }
        private bool EditorTargetCurrent()
        {
            return editorTarget != null && HasSession && projectionScene.Contains(editorTarget) && editorTarget.Opened &&
                editorTransaction == projectionTransaction && editorSceneRevision == projectionSceneRevision &&
                ReferenceEquals(editorMask, editorTarget.Mask) && editorOriginX == editorTarget.OriginX && editorOriginY == editorTarget.OriginY && editorMirrored == editorTarget.MirroredHorizontally;
        }
        private void LockEditorTarget(BlueprintProjectionPlacement target)
        {
            editorTarget = target; editorMask = target.Mask; editorOriginX = target.OriginX; editorOriginY = target.OriginY; editorMirrored = target.MirroredHorizontally;
            editorSceneRevision = projectionSceneRevision; editorTransaction = projectionTransaction;
        }
        private bool AcceptProjectionEditorPointer(bool left, int x, int y, float subX, float subY, bool pressed, bool released)
        {
            if (!OwnMirrorCancellation && !OwnTrimCancellation) return false;
            if (editorPickPending || ProjectionMode == BlueprintProjectionMode.Committing || ProjectionMode == BlueprintProjectionMode.MirrorPending) return true;
            if (ProjectionMode == BlueprintProjectionMode.SelectingMirror || ProjectionMode == BlueprintProjectionMode.SelectingTrim)
            {
                hoveredProjection = HitProjection(x, y, subX, subY);
                if (!pressed) return true;
                editorPickPending = true; editorPickX = x; editorPickY = y; editorPickSubX = subX; editorPickSubY = subY;
                editorPickOrder = projectionScene.ToArray(); editorPickIndex = editorPickOrder.Length - 1;
                editorSceneRevision = projectionSceneRevision; editorTransaction = projectionTransaction;
                if (OwnMirrorCancellation) ProjectionMode = BlueprintProjectionMode.MirrorPending;
                PumpProjectionEditor(); return true;
            }
            if (!OwnTrimCancellation || !EditorTargetCurrent()) return true;
            if (ProjectionMode == BlueprintProjectionMode.TrimDragging)
            {
                trimEndX = x - editorTarget.OriginX; trimEndY = y - editorTarget.OriginY; UpdateTrimRectangle();
                if (released)
                {
                    ProjectionMode = BlueprintProjectionMode.TrimPreview; trimPlanPending = true;
                    projectionFeedback = "正在核对修剪范围…"; RequestTrimPlan();
                }
                return true;
            }
            if (pressed && (ProjectionMode == BlueprintProjectionMode.TrimReady || ProjectionMode == BlueprintProjectionMode.TrimPreview))
            {
                Library?.CancelProjectionCommit(generation, projectionTransaction);
                trimPlan = null; trimPlanPending = false; trimPreviewRevision++; trimConfirmMapping = -1; trimConfirmChord = null;
                trimStartX = trimEndX = x - editorTarget.OriginX; trimStartY = trimEndY = y - editorTarget.OriginY;
                ProjectionMode = BlueprintProjectionMode.TrimDragging; UpdateTrimRectangle();
            }
            return true;
        }
        private void UpdateTrimRectangle()
        { trimDisplayBounds = BlueprintProjectionView.InclusiveRectangle(trimStartX, trimStartY, trimEndX, trimEndY, editorTarget.Summary.Width, editorTarget.Summary.Height); }
        private void RequestTrimPlan()
        {
            if (!trimPlanPending || !EditorTargetCurrent()) return;
            if (Library.RequestProjectionCommit(new BlueprintProjectionCommitRequest(generation, projectionTransaction, projectionSceneRevision, editorTarget.Snapshot(),
                Array.Empty<JueMingR.Features.Blueprint.BlueprintProjectionPlacement>(), BlueprintProjectionEditKind.Trim, trimDisplayBounds, trimPreviewRevision))) trimPlanPending = false;
        }
        private bool TakeProjectionEditorReceipt(BlueprintProjectionCommitReceipt receipt)
        {
            if (receipt.Request.Kind != BlueprintProjectionEditKind.Trim) return false;
            if (!OwnTrimCancellation || receipt.Request.SessionGeneration != generation || receipt.Request.TransactionId != projectionTransaction ||
                receipt.Request.PreviewRevision != trimPreviewRevision || !EditorTargetCurrent() || ProjectionMode != BlueprintProjectionMode.TrimPreview) return true;
            trimPlanPending = false;
            if (!receipt.Succeeded || receipt.TrimPlan == null)
            { projectionFeedback = "未能确认修剪范围，原蓝图仍保留。"; CancelProjection(); return true; }
            trimPlan = receipt.TrimPlan;
            projectionFeedback = trimPlan.HasContent ? "红色范围仅是预览；请在 F5 蓝图页点“确定修剪”，或使用已设置的确认键。" : "选区内没有可修剪内容";
            return true;
        }
        private void PumpProjectionEditor()
        {
            if (!OwnMirrorCancellation && !OwnTrimCancellation) return;
            if ((editorPickPending || editorTarget != null) && (editorTransaction != projectionTransaction || editorSceneRevision != projectionSceneRevision))
            { CancelProjection(); projectionFeedback = "场景已变化，本次蓝图编辑已取消。"; return; }
            if (editorTarget != null && !EditorTargetCurrent()) { CancelProjection(); projectionFeedback = "这份蓝图已变化，本次编辑已取消。"; return; }
            if (editorPickPending)
            {
                while (editorPickIndex >= 0)
                {
                    var item = editorPickOrder[editorPickIndex]; int sx = item.View.SourceX(editorPickX - item.OriginX), sy = editorPickY - item.OriginY;
                    if (sx < 0 || sy < 0 || sx >= item.Summary.Width || sy >= item.Summary.Height) { editorPickIndex--; continue; }
                    BlueprintProjectionWindow loaded = null; foreach (var pair in item.Windows) if (pair.Key.Contains(sx, sy)) { loaded = pair.Value; break; }
                    if (loaded == null) { SetProjectionPoint(item, sx, sy); projectionFeedback = "正在核对这份蓝图…"; return; }
                    bool hit = false; int low = 0, high = loaded.Cells.Count - 1;
                    while (low <= high)
                    {
                        int mid = low + (high - low) / 2; var cell = loaded.Cells[mid]; int comparison = cell.Y == sy ? cell.X.CompareTo(sx) : cell.Y.CompareTo(sy);
                        if (comparison < 0) low = mid + 1; else if (comparison > 0) high = mid - 1;
                        else { hit = HitCell(item.Mask.Apply(cell), item.MirroredHorizontally ? 1f - editorPickSubX : editorPickSubX, editorPickSubY); break; }
                    }
                    if (!hit) { editorPickIndex--; continue; }
                    LockEditorTarget(item); editorPickPending = false; editorPickOrder = null; hoveredProjection = item;
                    if (OwnTrimCancellation)
                    { ProjectionMode = BlueprintProjectionMode.TrimReady; InterruptProjection(); Pointer.Cancel(); projectionFeedback = "拖出红色修剪范围，松开后在 F5 蓝图页点“确定修剪”。"; return; }
                    break;
                }
                if (editorPickPending) { editorPickPending = false; editorPickOrder = null; ProjectionMode = OwnMirrorCancellation ? BlueprintProjectionMode.SelectingMirror : BlueprintProjectionMode.SelectingTrim; return; }
            }
            if (OwnTrimCancellation) { if (trimPlanPending) RequestTrimPlan(); return; }
            if (editorTarget == null || ProjectionMode == BlueprintProjectionMode.Committing) return;
            if (!EnsureMirrorProfile()) { CancelProjection(); projectionFeedback = "原版镜像资料暂未就绪，原蓝图仍保留。"; return; }
            if (!CheckProjectionMove(editorTarget))
            {
                if (ProjectionMoveEligibility(editorTarget) == BlueprintProjectionMoveEligibility.Matched)
                { CancelProjection(); projectionFeedback = "这份蓝图已有已完成部分，不能镜像"; }
                else projectionFeedback = "正在核对这份蓝图…";
                return;
            }
            var candidate = editorTarget.Copy(); candidate.MirrorProfile = mirrorProfile; candidate.MirroredHorizontally = !candidate.MirroredHorizontally;
            candidate.Qualification = null; candidate.QualificationRequestedMask = null; candidate.QualificationRequestedRevision = -1; candidate.Match = null;
            var opponents = new List<JueMingR.Features.Blueprint.BlueprintProjectionPlacement>();
            foreach (var old in projectionScene) if (!ReferenceEquals(old, editorTarget) && Intersects(candidate, old))
            { if (!old.Opened) return; opponents.Add(old.Snapshot()); }
            if (!Library.RequestProjectionCommit(new BlueprintProjectionCommitRequest(generation, projectionTransaction, projectionSceneRevision, candidate.Snapshot(), opponents, BlueprintProjectionEditKind.Mirror))) return;
            candidateProjection = candidate; restoreProjection = editorTarget; commitSceneRevision = projectionSceneRevision; ProjectionMode = BlueprintProjectionMode.Committing;
        }
        private bool EnsureMirrorProfile()
        {
            if (mirrorProfile?.Ready != true) mirrorProfile = BlueprintHorizontalMirrorResolver.Create();
            if (mirrorProfile?.Ready != true) return false;
            foreach (var item in projectionScene) if (item.MirrorProfile == null) item.MirrorProfile = mirrorProfile;
            return true;
        }
        private static CaptureCell EffectiveProjectionCell(BlueprintProjectionPlacement owner, BlueprintProjectionWindow window, int index)
        {
            if (window.View == null) return owner.Mask.Apply(window.Cells[index]);
            if (!window.CellsInSourceOrder && window.View.MirroredHorizontally && window.Objects.Count == 1 && window.SourceWindow != null)
            {
                var source = window.SourceWindow.Cells[index]; var obj = window.Objects[0];
                int column = window.View.DisplayX(source.X) - obj.X, row = source.Y - obj.Y;
                if (column >= 0 && row >= 0 && column < obj.Width && row < obj.Height) index = checked(row * obj.Width + column);
            }
            return window.Cells[index];
        }
        private static IReadOnlyList<CaptureCell> ProjectionSourceCells(BlueprintProjectionWindow window)
        { return window.SourceWindow?.Cells ?? window.Cells; }
        private static CaptureCell EffectiveProjectionCell(BlueprintProjectionPlacement owner, BlueprintProjectionWindow window, CaptureCell cell)
        { return window.View == null ? owner.Mask.Apply(cell) : cell; }
        private static BlueprintProjectionBounds ProjectionSourceWorldBounds(BlueprintProjectionView view, int originX, int originY, BlueprintProjectionBounds world)
        {
            var source = view.SourceBounds(new BlueprintProjectionBounds(world.Left - originX, world.Top - originY, world.Width, world.Height));
            return new BlueprintProjectionBounds(checked(source.Left + originX), checked(source.Top + originY), source.Width, source.Height);
        }
        private void InterruptProjectionEditorGesture(bool retainTrimConfirmation = false)
        {
            if (!retainTrimConfirmation) { trimConfirmMapping = -1; trimConfirmChord = null; }
            if (ProjectionMode == BlueprintProjectionMode.TrimDragging)
            { ProjectionMode = BlueprintProjectionMode.TrimReady; trimPlan = null; trimPlanPending = false; trimDisplayBounds = default(BlueprintProjectionBounds); trimPreviewRevision++; }
        }
    }
}
