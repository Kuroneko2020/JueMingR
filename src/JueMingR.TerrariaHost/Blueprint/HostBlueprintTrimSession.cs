using System;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Hotkeys;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private enum TrimConfirmationSource { PublicBinding, CertifiedMouse1, F5Control, ToolbarControl }
        private BlueprintTrimGeometry trimManualGeometry, trimObjectGeometry, trimPendingGeometry;
        private BlueprintProjectionCommitRequest trimFlight;
        private BlueprintProjectionTrimPlan trimFrozenPlan;
        private long trimSealedVersion, trimVerifiedVersion, trimConfirmedVersion, trimConfirmedRemoved;
        private bool trimFinalizing;
        private long trimCanceledMapping = -1;
        private long trimReadyMapping = -1, trimReadyRevision, trimReadyScene, trimReadyTransaction;
        private TrimConfirmationSource trimReadySource;
        private long trimConfirmMapping = -1, trimConfirmRevision, trimConfirmScene, trimConfirmTransaction;
        private HotkeyChord trimConfirmChord;
        internal BlueprintTrimGeometry TrimManualGeometry { get { return trimManualGeometry; } }
        internal BlueprintTrimGeometry TrimObjectGeometry { get { return trimObjectGeometry; } }
        private bool HasUnverifiedTrimSelection { get { return trimFlight != null || trimPendingGeometry?.HasContent == true || trimVerifiedVersion != trimSealedVersion; } }

        private void BeginTrimSession()
        {
            trimManualGeometry = new BlueprintTrimGeometry(); trimObjectGeometry = new BlueprintTrimGeometry(true);
            trimPendingGeometry = new BlueprintTrimGeometry(); trimSealedVersion = trimVerifiedVersion = 0;
            trimPlan = trimFrozenPlan = null; trimFlight = null; trimFinalizing = false;
        }
        private void ClearTrimSession()
        {
            if (trimManualGeometry != null) Library?.CancelProjectionCommit(generation, editorTransaction);
            trimManualGeometry?.Clear(); trimObjectGeometry?.Clear(); trimPendingGeometry?.Clear();
            trimManualGeometry = trimObjectGeometry = trimPendingGeometry = null;
            trimFlight = null; trimFrozenPlan = null; trimFinalizing = false;
            trimSealedVersion = trimVerifiedVersion = trimConfirmedVersion = trimConfirmedRemoved = 0;
            DropUnadmittedTrimConfirmations();
        }
        private void DropUnadmittedTrimConfirmations(bool retainCurrentReady = false)
        {
            trimConfirmMapping = -1; trimConfirmChord = null;
            if (!retainCurrentReady || trimReadyMapping != Input.Mapping) trimReadyMapping = -1;
        }
        private void SealTrimRectangle()
        {
            // Only manual all-layer geometry participates in the difference.
            // Prior furniture Tile extensions must not swallow later wall/wire.
            var added = trimManualGeometry.AddRectangle(trimDisplayBounds);
            trimDisplayBounds = default(BlueprintProjectionBounds); trimPreviewRevision++;
            if (added.Count != 0)
            {
                foreach (var span in added)
                {
                    var source = editorTarget.View.SourceBounds(new BlueprintProjectionBounds(span.Left, span.Y, span.RightExclusive - span.Left, 1));
                    trimPendingGeometry.AddRectangle(source);
                }
                trimSealedVersion++;
            }
            UpdateTrimFeedback(); TryDispatchTrim();
        }
        private void UpdateTrimFeedback()
        {
            if (trimFinalizing) projectionFeedback = "正在应用修剪…";
            else if (HasUnverifiedTrimSelection) projectionFeedback = "正在核对修剪范围…";
            else if (!TrimDragging && trimPlan != null)
                projectionFeedback = trimPlan.HasContent ? "红色范围仅为预览；请点“确定修剪”，或使用已设置的确认键。" : "选区内没有可修剪内容";
        }
        private void TryDispatchTrim()
        {
            if (!EditorTargetCurrent() || Library == null || trimFlight != null || trimFrozenPlan != null) return;
            if (!trimFinalizing && trimPendingGeometry?.HasContent != true) return;
            var request = new BlueprintProjectionCommitRequest(generation, projectionTransaction, editorSceneRevision, editorTarget.Snapshot(),
                Array.Empty<JueMingR.Features.Blueprint.BlueprintProjectionPlacement>(), BlueprintProjectionEditKind.Trim, previewRevision: trimPreviewRevision,
                trimGeometry: trimFinalizing ? null : trimPendingGeometry,
                trimPhase: trimFinalizing ? BlueprintProjectionTrimPhase.Freeze : BlueprintProjectionTrimPhase.Accumulate,
                expectedTrimVersion: trimVerifiedVersion, throughTrimVersion: trimFinalizing ? trimConfirmedVersion : trimSealedVersion,
                confirmedTrimRemovedCount: trimFinalizing ? trimConfirmedRemoved : 0);
            // Admission is O(1). Failure retains this suffix in place; success
            // transfers the old container and only then installs a new suffix.
            if (!Library.RequestProjectionCommit(request)) return;
            trimFlight = request;
            if (!trimFinalizing) trimPendingGeometry = new BlueprintTrimGeometry();
        }
        private bool TakeProjectionEditorReceipt(BlueprintProjectionCommitReceipt receipt)
        {
            if (receipt.Request.Kind != BlueprintProjectionEditKind.Trim) return false;
            // Preview revision belongs to gestures. New sealed work or an active
            // drag cannot invalidate an exact, already accepted worker prefix.
            if (!OwnTrimCancellation || !ReferenceEquals(receipt.Request, trimFlight) || !EditorTargetCurrent()) return true;
            var request = trimFlight; var plan = receipt.TrimPlan;
            if (!receipt.Succeeded || plan == null || !ReferenceEquals(plan.OriginalMask, editorMask) ||
                request.ExpectedTrimVersion != trimVerifiedVersion || plan.VerifiedVersion != request.ThroughTrimVersion ||
                plan.RemovedLayerCount < (trimPlan?.RemovedLayerCount ?? 0))
            { CancelProjection(); projectionFeedback = "未能确认修剪范围，原蓝图仍保留。"; return true; }
            trimFlight = null;
            if (request.TrimPhase == BlueprintProjectionTrimPhase.Freeze)
            {
                if (!trimFinalizing || !plan.Frozen || plan.VerifiedVersion != trimConfirmedVersion || plan.RemovedLayerCount != trimConfirmedRemoved)
                { CancelProjection(); projectionFeedback = "修剪状态已变化，原蓝图仍保留。"; return true; }
                // Poll/Update may receive this, but cannot publish a scene edit.
                // The next trusted input mapping still arbitrates cancellation.
                trimFrozenPlan = plan;
            }
            else
            {
                if (plan.Frozen || trimFinalizing) { CancelProjection(); return true; }
                trimVerifiedVersion = request.ThroughTrimVersion; trimPlan = plan;
                foreach (var source in plan.ObjectBounds) trimObjectGeometry.AddRectangle(editorTarget.View.DisplayBounds(source));
            }
            trimPreviewRevision++; UpdateTrimFeedback();
            return true;
        }
        internal void RequestTrimConfirm(HotkeyChord chord)
        {
            if (!Input.CanStartActions || !CanInvokeConfirmTrim || chord == null || trimCanceledMapping == Input.Mapping) return;
            if (chord.MainKey != 256) { QueueTrimConfirmation(TrimConfirmationSource.PublicBinding); return; }
            // Mouse1 alone retains the original same-map native Draw proof.
            trimConfirmMapping = Input.Mapping; trimConfirmChord = chord;
            trimConfirmRevision = trimPreviewRevision; trimConfirmScene = projectionSceneRevision; trimConfirmTransaction = projectionTransaction;
        }
        internal bool CanRetainTrimConfirmation(long mapping)
        {
            if (mapping != trimConfirmMapping || trimConfirmChord == null || trimConfirmChord.MainKey != 256 ||
                (Input.Mapping != mapping && Input.Mapping != mapping + 1) || trimConfirmRevision != trimPreviewRevision ||
                trimConfirmScene != projectionSceneRevision || trimConfirmTransaction != projectionTransaction || !CanInvokeConfirmTrim || !CanRetainWorldInput) return false;
            return CanRetainEdit || Shell?.CanRetainBlueprintTrimConfirmation == true;
        }
        internal bool ConsumeTrimConfirm(long mapping, bool certified)
        {
            if (trimConfirmMapping < 0 || mapping < trimConfirmMapping) return false;
            bool exact = mapping == trimConfirmMapping;
            bool current = exact && certified && CanRetainTrimConfirmation(mapping);
            trimConfirmMapping = -1; trimConfirmChord = null;
            if (current) QueueTrimConfirmation(TrimConfirmationSource.CertifiedMouse1);
            // Rejected confirmation never falls through as a new drag.
            return exact;
        }
        // These are reached only after their actual controls validate release,
        // layout/preview identity and physical ownership; no public matcher or
        // broad world-editor force gate is applied to a real UI command.
        internal void ConfirmTrimProjection() { QueueTrimConfirmation(TrimConfirmationSource.F5Control); }
        internal void ConfirmTrimToolbar() { QueueTrimConfirmation(TrimConfirmationSource.ToolbarControl); }
        private void QueueTrimConfirmation(TrimConfirmationSource source)
        {
            if (!Input.CanStartActions || !CanInvokeConfirmTrim || trimCanceledMapping == Input.Mapping) return;
            trimReadyMapping = Input.Mapping; trimReadyRevision = trimPreviewRevision; trimReadyScene = projectionSceneRevision;
            trimReadyTransaction = projectionTransaction; trimReadySource = source;
        }
        private void ArbitrateCurrentTrim()
        {
            if (!OwnTrimCancellation) { DropUnadmittedTrimConfirmations(); return; }
            if (editorTarget == null) { trimReadyMapping = -1; return; }
            if (!EditorTargetCurrent()) { CancelProjection(); return; }
            if (trimReadyMapping >= 0)
            {
                bool current = trimReadyMapping == Input.Mapping && trimCanceledMapping != Input.Mapping &&
                    trimReadyRevision == trimPreviewRevision && trimReadyScene == projectionSceneRevision && trimReadyTransaction == projectionTransaction &&
                    Input.CanStartActions && CanRetainWorldInput && CanInvokeConfirmTrim;
                // Public/certified bindings retain their original narrow gate;
                // UI releases have already proved their actual owning control.
                if (current && (trimReadySource == TrimConfirmationSource.F5Control || trimReadySource == TrimConfirmationSource.ToolbarControl ||
                    CanRetainEdit || Shell?.CanRetainBlueprintTrimConfirmation == true))
                {
                    trimFinalizing = true; trimConfirmedVersion = trimVerifiedVersion; trimConfirmedRemoved = trimPlan.RemovedLayerCount;
                    trimPreviewRevision++; DropUnadmittedTrimConfirmations(); UpdateTrimFeedback(); TryDispatchTrim();
                }
                trimReadyMapping = -1;
            }
            // Accepting a command and publishing its immutable result have
            // distinct gates. Finalizing disables new confirmation, while the
            // accepted command may wait through ordinary UI/focus suspension.
            if (trimFrozenPlan == null || !trimFinalizing || !Input.CanStartActions || !CanRetainWorldInput ||
                !(CanRetainEdit || Shell?.CanRetainBlueprintTrimConfirmation == true) || trimCanceledMapping == Input.Mapping) return;
            if (TrimDragging || trimFlight != null || trimPendingGeometry?.HasContent == true || trimConfirmedVersion != trimSealedVersion ||
                trimVerifiedVersion != trimConfirmedVersion || trimFrozenPlan.VerifiedVersion != trimConfirmedVersion ||
                trimFrozenPlan.RemovedLayerCount != trimConfirmedRemoved || !ReferenceEquals(trimFrozenPlan.OriginalMask, editorMask))
            { CancelProjection(); return; }
            CommitTrimPreview();
        }
        private void CommitTrimPreview()
        {
            if (!trimFinalizing || trimFrozenPlan?.Frozen != true || !EditorTargetCurrent()) return;
            var target = editorTarget; var plan = trimFrozenPlan;
            CancelAutomaticBuild(); ReleaseProjectionMatch(target);
            target.Mask = plan.Mask; target.Qualification = null; target.QualificationRequestedMask = null; target.QualificationRequestedRevision = -1;
            RebuildProjectionMask(target);
            if (target.Mask.RemovedLayerCount >= target.TotalLayerCount) RetireProjection(target, false);
            projectionSceneRevision++; PublishProjectionScene();
            ClearProjectionEditor(); projectionEditKind = BlueprintProjectionEditKind.None;
            ProjectionMode = projectionScene.Count == 0 ? BlueprintProjectionMode.None : BlueprintProjectionMode.Fixed;
            InterruptProjection(); Pointer.Cancel(); Toolbar.Cancel();
            projectionFeedback = "蓝图已修剪"; AcceptProjectionSceneSnapshot();
        }
    }
}
